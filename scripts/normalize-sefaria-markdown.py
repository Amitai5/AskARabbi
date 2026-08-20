#!/usr/bin/env python3
"""Normalize the complete raw Sefaria corpus into provenance-rich Markdown documents."""

from __future__ import annotations

import argparse
import concurrent.futures
import datetime as dt
import hashlib
import html
import json
import os
import re
import tempfile
import unicodedata
from collections import Counter
from html.parser import HTMLParser
from pathlib import Path
from typing import Any, Iterable, Iterator


NormalizerVersion = "1"
HtmlTagPattern = re.compile(r"</?[A-Za-z][^>]*>")
AngleMarkupPattern = re.compile(r"<([^<>\n]+)>")
HorizontalWhitespacePattern = re.compile(r"[ \t\f\v]+")
SpaceAroundNewlinePattern = re.compile(r" *\n *")
ExcessBlankLinesPattern = re.compile(r"\n{3,}")


class MarkdownTextParser(HTMLParser):
    """Convert the small semantic subset of HTML used by Sefaria into Markdown text."""

    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.Parts: list[str] = []
        self.EndMarkers: dict[str, list[str]] = {}
        self.SuppressedTags: list[str] = []

    def handle_starttag(self, tag: str, attributes: list[tuple[str, str | None]]) -> None:
        """Translate one HTML start tag into a Markdown marker or structural break."""
        normalizedTag = tag.casefold()
        attributeMap = {name.casefold(): value or "" for name, value in attributes}
        classes = set(attributeMap.get("class", "").split())

        if normalizedTag in {"script", "style"}:
            self.SuppressedTags.append(normalizedTag)
            self.EndMarkers.setdefault(normalizedTag, []).append("")
            return
        if self.SuppressedTags:
            self.EndMarkers.setdefault(normalizedTag, []).append("")
            return
        if normalizedTag == "br":
            self.Parts.append("\n")
            return
        if normalizedTag == "img":
            alternativeText = attributeMap.get("alt", "").strip()
            if alternativeText:
                self.Parts.append(f"[Image: {alternativeText}]")
            return

        startMarker = ""
        endMarker = ""
        if normalizedTag in {"b", "strong"}:
            startMarker = "**"
            endMarker = "**"
        elif normalizedTag in {"i", "em"} and "footnote" in classes:
            startMarker = " (Footnote: "
            endMarker = ")"
        elif normalizedTag in {"i", "em"}:
            startMarker = "*"
            endMarker = "*"
        elif normalizedTag == "sup" and "footnote-marker" in classes:
            startMarker = "["
            endMarker = "]"
        elif normalizedTag in {"p", "div", "section", "blockquote"}:
            startMarker = "\n\n"
            endMarker = "\n\n"
        elif normalizedTag in {"ul", "ol", "table", "thead", "tbody"}:
            startMarker = "\n"
            endMarker = "\n"
        elif normalizedTag == "li":
            startMarker = "\n- "
        elif normalizedTag == "tr":
            startMarker = "\n"
        elif normalizedTag in {"td", "th"}:
            startMarker = " | "
        elif normalizedTag in {"h1", "h2", "h3", "h4", "h5", "h6"}:
            startMarker = "\n\n"
            endMarker = "\n\n"
        elif normalizedTag == "italic":
            startMarker = "*"
            endMarker = "*"
        elif normalizedTag.startswith("endnote"):
            startMarker = "["
            endMarker = "]"
        elif normalizedTag.startswith("figure_"):
            startMarker = f"[Figure: {normalizedTag}]"
        elif normalizedTag not in {"a", "big", "center", "code", "del", "font", "folio", "ftnote", "mark", "pre", "rp", "rt", "ruby", "s", "small", "span", "strike", "sub", "sup", "u"}:
            editorialParts = [normalizedTag, *(name for name, _ in attributes)]
            editorialText = " ".join(part for part in editorialParts if part).replace('=""', "").strip()
            if editorialText:
                startMarker = f"[{editorialText}]"

        self.Parts.append(startMarker)
        self.EndMarkers.setdefault(normalizedTag, []).append(endMarker)

    def handle_startendtag(self, tag: str, attributes: list[tuple[str, str | None]]) -> None:
        """Translate one self-closing HTML tag without leaving an unmatched end marker."""
        self.handle_starttag(tag, attributes)
        normalizedTag = tag.casefold()
        markers = self.EndMarkers.get(normalizedTag)
        if markers:
            self.Parts.append(markers.pop())

    def handle_endtag(self, tag: str) -> None:
        """Close one translated HTML element."""
        normalizedTag = tag.casefold()
        markers = self.EndMarkers.get(normalizedTag)
        if markers:
            endMarker = markers.pop()
            if endMarker in {"*", "**"} and self.Parts and self.Parts[-1].endswith(" "):
                self.Parts[-1] = self.Parts[-1].rstrip(" ")
                endMarker += " "
            self.Parts.append(endMarker)
        if self.SuppressedTags and self.SuppressedTags[-1] == normalizedTag:
            self.SuppressedTags.pop()

    def handle_data(self, data: str) -> None:
        """Preserve visible text outside suppressed elements."""
        if not self.SuppressedTags:
            self.Parts.append(data)

    def markdown(self) -> str:
        """Return the accumulated Markdown fragment."""
        return "".join(self.Parts)


def parseArguments() -> argparse.Namespace:
    """Parse command-line arguments for the Sefaria Markdown normalizer."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--data-root", type=Path, default=Path("Data"), help="Project data directory (default: Data).")
    parser.add_argument("--workers", type=int, default=4, help="Concurrent normalization workers (default: 4).")
    return parser.parse_args()


def writeBytesAtomically(path: Path, content: bytes) -> None:
    """Write generated bytes atomically so interrupted work never appears complete."""
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporaryName = tempfile.mkstemp(prefix=f".{path.name}.", suffix=".part", dir=path.parent)
    temporaryPath = Path(temporaryName)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
        temporaryPath.replace(path)
    except BaseException:
        temporaryPath.unlink(missing_ok=True)
        raise


def writeJson(path: Path, value: Any) -> None:
    """Write deterministic UTF-8 JSON with a trailing newline."""
    content = (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")
    writeBytesAtomically(path, content)


def writeJsonLines(path: Path, values: Iterable[dict[str, Any]]) -> None:
    """Write deterministic UTF-8 JSON Lines sorted by normalized path."""
    ordered = sorted(values, key=lambda value: str(value.get("normalizedPath", "")))
    content = "".join(json.dumps(value, ensure_ascii=False, sort_keys=True) + "\n" for value in ordered).encode("utf-8")
    writeBytesAtomically(path, content)


def normalizeText(value: str) -> str:
    """Normalize Unicode, embedded Sefaria HTML, and whitespace into readable Markdown."""
    normalized = value
    for _ in range(3):
        if not HtmlTagPattern.search(normalized):
            normalized = html.unescape(normalized)
            break
        parser = MarkdownTextParser()
        parser.feed(normalized)
        parser.close()
        parsed = parser.markdown()
        if parsed == normalized:
            break
        normalized = parsed

    def replaceAngleMarkup(match: re.Match[str]) -> str:
        editorialText = match.group(1).strip()
        if not editorialText or editorialText.startswith("/"):
            return ""
        if editorialText.casefold().startswith("br"):
            return "\n"
        editorialText = editorialText.replace('=""', "")
        if editorialText.casefold().startswith("figure_"):
            return f"[Figure: {editorialText}]"
        return f"[{editorialText}]"

    normalized = AngleMarkupPattern.sub(replaceAngleMarkup, normalized)
    normalized = unicodedata.normalize("NFC", normalized).replace("\u00a0", " ").replace("\r\n", "\n").replace("\r", "\n")
    normalized = HorizontalWhitespacePattern.sub(" ", normalized)
    normalized = SpaceAroundNewlinePattern.sub("\n", normalized)
    normalized = ExcessBlankLinesPattern.sub("\n\n", normalized)
    return normalized.strip()


def findSchemaNode(schemaDocument: dict[str, Any], nodeKey: str | None) -> dict[str, Any]:
    """Resolve a text payload or named complex node to its JaggedArray schema node."""
    root = schemaDocument.get("schema") or schemaDocument
    if root.get("nodeType") == "JaggedArrayNode":
        return root
    nodes = root.get("nodes") or []
    if nodeKey is None:
        for node in nodes:
            if node.get("default"):
                return node
    else:
        for node in nodes:
            if nodeKey == "" and node.get("default"):
                return node
            if nodeKey in {str(node.get("key") or ""), str(node.get("title") or "")}:
                return node
    raise ValueError(f"Unable to resolve schema node {nodeKey!r} for {root.get('title')!r}.")


def iterateLeafSegments(value: Any, indices: tuple[int, ...] = ()) -> Iterator[tuple[tuple[int, ...], str]]:
    """Yield index paths and string segments from one Sefaria jagged array."""
    if isinstance(value, str):
        yield indices, value
        return
    if isinstance(value, list):
        for index, child in enumerate(value):
            yield from iterateLeafSegments(child, (*indices, index))
        return
    if value is None:
        return
    raise ValueError(f"Unexpected {type(value).__name__} inside a Sefaria jagged array.")


def formatAddress(index: int, addressType: str) -> str:
    """Format a zero-based jagged-array index using Sefaria's address type."""
    if addressType == "Talmud":
        return f"{index // 2 + 1}{'a' if index % 2 == 0 else 'b'}"
    return str(index + 1)


def canonicalReference(baseTitle: str, indices: tuple[int, ...], addressTypes: list[str]) -> str:
    """Build a stable segment reference from a work title and Sefaria address schema."""
    if not indices:
        return baseTitle
    addresses = [formatAddress(index, addressTypes[depth] if depth < len(addressTypes) else "Integer") for depth, index in enumerate(indices)]
    return f"{baseTitle} {':'.join(addresses)}"


def iterateDocumentSegments(payload: dict[str, Any], schemaDocument: dict[str, Any]) -> Iterator[tuple[str, str]]:
    """Yield canonical references and normalized text from simple or complex Sefaria documents."""
    title = str(payload.get("title") or "Untitled")
    text = payload.get("text")
    if isinstance(text, dict):
        for nodeKey, nodeText in text.items():
            node = findSchemaNode(schemaDocument, str(nodeKey))
            nodeTitle = str(node.get("title") or nodeKey).strip()
            baseTitle = title if not nodeTitle else f"{title}, {nodeTitle}"
            addressTypes = [str(value) for value in node.get("addressTypes") or []]
            for indices, segment in iterateLeafSegments(nodeText):
                normalized = normalizeText(segment)
                if normalized:
                    yield canonicalReference(baseTitle, indices, addressTypes), normalized
        return
    if isinstance(text, list):
        node = findSchemaNode(schemaDocument, None)
        addressTypes = [str(value) for value in node.get("addressTypes") or []]
        for indices, segment in iterateLeafSegments(text):
            normalized = normalizeText(segment)
            if normalized:
                yield canonicalReference(title, indices, addressTypes), normalized
        return
    raise ValueError(f"Unexpected top-level text type {type(text).__name__} for {title}.")


def yamlScalar(value: Any) -> str:
    """Render a JSON-compatible value as a valid one-line YAML scalar."""
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (int, float)):
        return str(value)
    return json.dumps(value, ensure_ascii=False)


def renderFrontMatter(metadata: dict[str, Any]) -> str:
    """Render deterministic YAML front matter using JSON-compatible scalar syntax."""
    lines = ["---"]
    for key, value in metadata.items():
        lines.append(f"{key}: {yamlScalar(value)}")
    lines.append("---")
    return "\n".join(lines)


def renderMarkdown(payload: dict[str, Any], rawRecord: dict[str, Any], schemaDocument: dict[str, Any]) -> tuple[str, int, str | None, str | None]:
    """Render one Sefaria version as a provenance-rich Markdown document."""
    segments = list(iterateDocumentSegments(payload, schemaDocument))
    title = str(payload.get("title") or rawRecord.get("title") or "Untitled")
    metadata = {
        "document_id": f"sefaria:{rawRecord['sha256']}",
        "source_provider": "Sefaria",
        "collection": rawRecord.get("collection"),
        "title": title,
        "hebrew_title": payload.get("heTitle") or rawRecord.get("heTitle"),
        "categories": payload.get("categories") or rawRecord.get("categories") or [],
        "version_title": payload.get("versionTitle") or rawRecord.get("versionTitle"),
        "version_source": payload.get("versionSource") or rawRecord.get("versionSource"),
        "language_bucket": rawRecord.get("languageBucket"),
        "actual_language": payload.get("actualLanguage") or rawRecord.get("actualLanguage") or payload.get("language"),
        "license": payload.get("license") or rawRecord.get("license"),
        "license_status": rawRecord.get("licenseStatus"),
        "source_url": rawRecord.get("sourceUrl"),
        "raw_path": rawRecord.get("localPath"),
        "raw_sha256": rawRecord.get("sha256"),
        "normalizer_version": NormalizerVersion,
        "segment_count": len(segments),
    }
    parts = [renderFrontMatter(metadata), "", f"# {title}", ""]
    for reference, text in segments:
        parts.extend((f"## {reference}", "", text, ""))
    markdown = "\n".join(parts).rstrip() + "\n"
    firstReference = segments[0][0] if segments else None
    lastReference = segments[-1][0] if segments else None
    return markdown, len(segments), firstReference, lastReference


def normalizeDocument(dataRoot: Path, rawProviderRoot: Path, normalizedProviderRoot: Path, rawRecord: dict[str, Any], schemaDocument: dict[str, Any], normalizedAtUtc: str) -> dict[str, Any]:
    """Validate and normalize one raw Sefaria text into Markdown and return its manifest record."""
    rawPath = dataRoot / str(rawRecord["localPath"])
    relativePath = rawPath.relative_to(rawProviderRoot)
    normalizedPath = (normalizedProviderRoot / relativePath).with_suffix(".md")
    rawContent = rawPath.read_bytes()
    rawSha256 = hashlib.sha256(rawContent).hexdigest()
    if rawSha256 != rawRecord.get("sha256"):
        raise ValueError(f"Raw checksum mismatch for {rawPath}")
    payload = json.loads(rawContent.decode("utf-8"))
    if not isinstance(payload, dict):
        raise ValueError(f"Expected a JSON object in {rawPath}")
    markdown, segmentCount, firstReference, lastReference = renderMarkdown(payload, rawRecord, schemaDocument)
    normalizedContent = markdown.encode("utf-8")
    writeBytesAtomically(normalizedPath, normalizedContent)
    return {
        "artifactType": "normalized_text",
        "sourceProvider": "Sefaria",
        "collection": rawRecord.get("collection"),
        "title": rawRecord.get("title"),
        "versionTitle": rawRecord.get("versionTitle"),
        "actualLanguage": rawRecord.get("actualLanguage"),
        "license": rawRecord.get("license"),
        "licenseStatus": rawRecord.get("licenseStatus"),
        "rawPath": rawRecord.get("localPath"),
        "rawSha256": rawSha256,
        "normalizedPath": normalizedPath.relative_to(dataRoot).as_posix(),
        "normalizedSha256": hashlib.sha256(normalizedContent).hexdigest(),
        "byteCount": len(normalizedContent),
        "segmentCount": segmentCount,
        "firstReference": firstReference,
        "lastReference": lastReference,
        "normalizerVersion": NormalizerVersion,
        "normalizedAtUtc": normalizedAtUtc,
    }


def loadRawManifest(path: Path) -> list[dict[str, Any]]:
    """Load and validate the raw Sefaria JSON Lines manifest."""
    records = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]
    if not all(isinstance(record, dict) for record in records):
        raise ValueError(f"Invalid record in {path}")
    return records


def loadSchemas(dataRoot: Path, records: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    """Load one structural schema document for every title in the raw manifest."""
    schemas: dict[str, dict[str, Any]] = {}
    for record in records:
        if record.get("artifactType") != "schema":
            continue
        path = dataRoot / str(record["localPath"])
        content = path.read_bytes()
        if hashlib.sha256(content).hexdigest() != record.get("sha256"):
            raise ValueError(f"Schema checksum mismatch for {path}")
        value = json.loads(content.decode("utf-8"))
        if not isinstance(value, dict):
            raise ValueError(f"Expected a JSON object in {path}")
        schemas[str(record["title"])] = value
    return schemas


def summarize(records: list[dict[str, Any]], dataRoot: Path, rawManifestPath: Path, normalizedAtUtc: str, errors: list[dict[str, Any]]) -> dict[str, Any]:
    """Build a compact normalization summary for audit and downstream planning."""
    return {
        "sourceProvider": "Sefaria",
        "normalizerVersion": NormalizerVersion,
        "normalizedAtUtc": normalizedAtUtc,
        "rawManifestPath": rawManifestPath.relative_to(dataRoot).as_posix(),
        "rawManifestSha256": hashlib.sha256(rawManifestPath.read_bytes()).hexdigest(),
        "documentCount": len(records),
        "segmentCount": sum(int(record["segmentCount"]) for record in records),
        "byteCount": sum(int(record["byteCount"]) for record in records),
        "normalizationErrorCount": len(errors),
        "countsByCollection": dict(sorted(Counter(str(record.get("collection")) for record in records).items())),
        "countsByLicenseStatus": dict(sorted(Counter(str(record.get("licenseStatus")) for record in records).items())),
        "countsByActualLanguage": dict(sorted(Counter(str(record.get("actualLanguage") or "unknown") for record in records).items())),
    }


def main() -> int:
    """Normalize every raw Sefaria text and write normalized manifests and summaries."""
    arguments = parseArguments()
    if arguments.workers < 1 or arguments.workers > 32:
        raise ValueError("--workers must be between 1 and 32.")

    dataRoot = arguments.data_root.resolve()
    rawProviderRoot = dataRoot / "Raw" / "Sefaria"
    normalizedProviderRoot = dataRoot / "NormalizedData" / "Sefaria"
    rawManifestPath = rawProviderRoot / "Metadata" / "manifest.jsonl"
    normalizedMetadataRoot = normalizedProviderRoot / "Metadata"
    normalizedAtUtc = dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    rawRecords = loadRawManifest(rawManifestPath)
    textRecords = [record for record in rawRecords if record.get("artifactType") == "text"]
    schemas = loadSchemas(dataRoot, rawRecords)
    missingSchemas = sorted({str(record.get("title")) for record in textRecords if str(record.get("title")) not in schemas})
    if missingSchemas:
        raise ValueError(f"Missing schemas for {len(missingSchemas)} titles: {missingSchemas[:10]}")

    print(f"Normalizing {len(textRecords)} Sefaria text versions with {len(schemas)} schemas.", flush=True)
    records: list[dict[str, Any]] = []
    errors: list[dict[str, Any]] = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=arguments.workers) as executor:
        futureToRecord = {
            executor.submit(normalizeDocument, dataRoot, rawProviderRoot, normalizedProviderRoot, rawRecord, schemas[str(rawRecord["title"])], normalizedAtUtc): rawRecord
            for rawRecord in textRecords
        }
        for completedCount, future in enumerate(concurrent.futures.as_completed(futureToRecord), start=1):
            rawRecord = futureToRecord[future]
            try:
                records.append(future.result())
            except Exception as error:
                errors.append({
                    "title": rawRecord.get("title"),
                    "versionTitle": rawRecord.get("versionTitle"),
                    "rawPath": rawRecord.get("localPath"),
                    "error": repr(error),
                })
            if completedCount % 25 == 0 or completedCount == len(textRecords):
                print(f"Documents: {completedCount}/{len(textRecords)} processed; {len(errors)} errors.", flush=True)

    writeJsonLines(normalizedMetadataRoot / "manifest.jsonl", records)
    if errors:
        writeJsonLines(normalizedMetadataRoot / "normalization-errors.jsonl", errors)
    else:
        (normalizedMetadataRoot / "normalization-errors.jsonl").unlink(missing_ok=True)
    summary = summarize(records, dataRoot, rawManifestPath, normalizedAtUtc, errors)
    writeJson(normalizedMetadataRoot / "summary.json", summary)
    print(json.dumps(summary, ensure_ascii=False, indent=2, sort_keys=True), flush=True)
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
