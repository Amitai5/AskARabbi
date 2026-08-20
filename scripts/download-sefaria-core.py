#!/usr/bin/env python3
"""Download and inventory the canonical Torah, Tanakh, Mishnah, and Talmud exports from Sefaria."""

from __future__ import annotations

import argparse
import concurrent.futures
import datetime as dt
import hashlib
import json
import os
import re
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
from collections import Counter
from pathlib import Path
from typing import Any


BooksIndexUrl = "https://raw.githubusercontent.com/Sefaria/Sefaria-Export/master/books.json"
TableOfContentsUrl = "https://storage.googleapis.com/sefaria-export/table_of_contents.json"
SchemaBaseUrl = "https://storage.googleapis.com/sefaria-export/schemas"
InvalidFileNameCharacters = re.compile(r'[<>:"/\\|?*\x00-\x1f]')
ReservedWindowsNames = {"CON", "PRN", "AUX", "NUL", *(f"COM{number}" for number in range(1, 10)), *(f"LPT{number}" for number in range(1, 10))}
PermissiveLicensePrefixes = ("public domain", "cc0", "cc-by", "cc by")


def parseArguments() -> argparse.Namespace:
    """Parse command-line arguments for a resumable Sefaria core-text download."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--data-root", type=Path, default=Path("Data"), help="Project data directory (default: Data).")
    parser.add_argument("--workers", type=int, default=8, help="Concurrent downloads (default: 8).")
    parser.add_argument("--refresh-index", action="store_true", help="Replace the local books.json with the latest upstream index.")
    parser.add_argument("--exclude-merged", action="store_true", help="Exclude merged files, which maximize coverage but omit a top-level license.")
    return parser.parse_args()


def safePathPart(value: str, maximumLength: int = 88) -> str:
    """Return a stable Windows-safe path component while preserving readable Unicode text."""
    cleaned = InvalidFileNameCharacters.sub("_", value).strip(" .")
    cleaned = re.sub(r"\s+", " ", cleaned) or "untitled"
    if cleaned.upper() in ReservedWindowsNames:
        cleaned = f"_{cleaned}"
    if len(cleaned) > maximumLength:
        cleaned = cleaned[:maximumLength].rstrip(" .")
    return cleaned


def encodeUrl(url: str) -> str:
    """Percent-encode an upstream URL without changing its scheme, host, query, or path separators."""
    parts = urllib.parse.urlsplit(url)
    encodedPath = urllib.parse.quote(urllib.parse.unquote(parts.path), safe="/!$&'()*+,;=:@-._~")
    return urllib.parse.urlunsplit((parts.scheme, parts.netloc, encodedPath, parts.query, parts.fragment))


def downloadBytes(url: str, attempts: int = 4) -> bytes:
    """Download one public artifact with bounded retries and return its bytes."""
    request = urllib.request.Request(encodeUrl(url), headers={"User-Agent": "AskRabbi-Sefaria-Importer/1.0"})
    lastError: Exception | None = None
    for attempt in range(attempts):
        try:
            with urllib.request.urlopen(request, timeout=180) as response:
                return response.read()
        except (TimeoutError, urllib.error.URLError, urllib.error.HTTPError) as error:
            lastError = error
            if attempt + 1 < attempts:
                time.sleep(2**attempt)
    raise RuntimeError(f"Unable to download {url}") from lastError


def writeBytesAtomically(path: Path, content: bytes) -> None:
    """Write downloaded bytes atomically so interrupted transfers never appear complete."""
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


def readJson(path: Path) -> Any:
    """Read and return one UTF-8 JSON document."""
    return json.loads(path.read_text(encoding="utf-8"))


def ensureJsonArtifact(path: Path, url: str, refresh: bool = False) -> Any:
    """Return a valid local JSON artifact, downloading it atomically when absent or stale."""
    if path.exists() and not refresh:
        try:
            return readJson(path)
        except (OSError, UnicodeError, json.JSONDecodeError, ValueError):
            pass
    content = downloadBytes(url)
    value = json.loads(content.decode("utf-8"))
    writeBytesAtomically(path, content)
    return value


def classifyBook(book: dict[str, Any]) -> tuple[str, list[str]] | None:
    """Map one exact Sefaria primary-text category path into the local collection layout."""
    categories = book.get("categories") or []
    title = safePathPart(str(book.get("title") or "untitled"))
    languageBucket = safePathPart(str(book.get("language") or "Unknown"))

    if categories == ["Tanakh", "Torah"]:
        return "Torah", [title, languageBucket]
    if len(categories) == 2 and categories[0] == "Tanakh" and categories[1] in {"Prophets", "Writings"}:
        return "Tanakh", [safePathPart(categories[1]), title, languageBucket]
    if len(categories) == 2 and categories[0] == "Mishnah" and str(categories[1]).startswith("Seder "):
        return "Mishnah", [safePathPart(categories[1]), title, languageBucket]
    if len(categories) == 3 and categories[0] == "Talmud" and categories[1] in {"Bavli", "Yerushalmi"} and (str(categories[2]).startswith("Seder ") or categories[2] == "Minor Tractates"):
        return "Talmud", [safePathPart(categories[1]), safePathPart(categories[2]), title, languageBucket]
    return None


def localTextPath(dataRoot: Path, book: dict[str, Any], relativeParts: list[str]) -> Path:
    """Build a readable collision-resistant local path for one versioned text."""
    sourceUrl = str(book["json_url"])
    versionTitle = safePathPart(str(book.get("versionTitle") or "untitled"))
    urlHash = hashlib.sha256(sourceUrl.encode("utf-8")).hexdigest()[:12]
    return dataRoot / "Raw" / "Sefaria" / str(book["localCollection"]) / Path(*relativeParts) / f"{versionTitle}--{urlHash}.json"


def licenseStatus(versionTitle: str, licenseName: str | None) -> str:
    """Classify a source license conservatively for downstream filtering."""
    if versionTitle.casefold() == "merged":
        return "review_required"
    normalized = (licenseName or "").strip().casefold()
    if not normalized or normalized == "unknown":
        return "review_required"
    if "-nc" in normalized or "noncommercial" in normalized or "non-commercial" in normalized:
        return "noncommercial"
    if normalized == "pd" or normalized.startswith(PermissiveLicensePrefixes):
        return "permissive"
    return "review_required"


def inspectText(path: Path, book: dict[str, Any], dataRoot: Path, downloadedAtUtc: str) -> dict[str, Any]:
    """Validate one text and return its provenance manifest record."""
    content = path.read_bytes()
    value = json.loads(content.decode("utf-8"))
    if not isinstance(value, dict) or "text" not in value:
        raise ValueError(f"Text payload is missing from {path}")
    versionTitle = str(value.get("versionTitle") or book.get("versionTitle") or "")
    licenseName = value.get("license")
    relativePath = path.relative_to(dataRoot).as_posix()
    record: dict[str, Any] = {
        "artifactType": "text",
        "collection": book["localCollection"],
        "title": value.get("title") or book.get("title"),
        "heTitle": value.get("heTitle"),
        "categories": value.get("categories") or book.get("categories"),
        "languageBucket": book.get("language"),
        "actualLanguage": value.get("actualLanguage") or value.get("language"),
        "languageFamilyName": value.get("languageFamilyName"),
        "versionTitle": versionTitle,
        "versionSource": value.get("versionSource"),
        "license": licenseName,
        "licenseStatus": licenseStatus(versionTitle, str(licenseName) if licenseName is not None else None),
        "sourceUrl": book["json_url"],
        "localPath": relativePath,
        "byteCount": len(content),
        "sha256": hashlib.sha256(content).hexdigest(),
        "downloadedAtUtc": downloadedAtUtc,
    }
    if versionTitle.casefold() == "merged":
        record["mergedVersions"] = value.get("versions") or []
    return record


def downloadText(dataRoot: Path, book: dict[str, Any], downloadedAtUtc: str) -> dict[str, Any]:
    """Download or reuse one Sefaria JSON text and return its validated manifest record."""
    classification = classifyBook(book)
    if classification is None:
        raise ValueError(f"Book is outside the core selection: {book.get('title')}")
    collection, relativeParts = classification
    selectedBook = dict(book)
    selectedBook["localCollection"] = collection
    path = localTextPath(dataRoot, selectedBook, relativeParts)
    if path.exists():
        try:
            return inspectText(path, selectedBook, dataRoot, downloadedAtUtc)
        except (OSError, UnicodeError, json.JSONDecodeError, ValueError):
            path.unlink(missing_ok=True)
    writeBytesAtomically(path, downloadBytes(str(selectedBook["json_url"])))
    return inspectText(path, selectedBook, dataRoot, downloadedAtUtc)


def downloadSchema(dataRoot: Path, title: str, downloadedAtUtc: str) -> dict[str, Any]:
    """Download or reuse one Sefaria structural schema and return its manifest record."""
    schemaObjectName = title.replace(" ", "_")
    sourceUrl = f"{SchemaBaseUrl}/{urllib.parse.quote(schemaObjectName, safe='')}.json"
    urlHash = hashlib.sha256(sourceUrl.encode("utf-8")).hexdigest()[:12]
    path = dataRoot / "Raw" / "Sefaria" / "Metadata" / "Schemas" / f"{safePathPart(title)}--{urlHash}.json"
    value = ensureJsonArtifact(path, sourceUrl)
    content = path.read_bytes()
    return {
        "artifactType": "schema",
        "title": title,
        "sourceUrl": sourceUrl,
        "localPath": path.relative_to(dataRoot).as_posix(),
        "byteCount": len(content),
        "sha256": hashlib.sha256(content).hexdigest(),
        "downloadedAtUtc": downloadedAtUtc,
        "schemaTitle": value.get("title"),
    }


def writeJson(path: Path, value: Any) -> None:
    """Write deterministic UTF-8 JSON with a trailing newline."""
    content = (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")
    writeBytesAtomically(path, content)


def writeJsonLines(path: Path, values: list[dict[str, Any]]) -> None:
    """Write deterministic UTF-8 JSON Lines sorted by local path and source URL."""
    ordered = sorted(values, key=lambda value: (str(value.get("localPath", "")), str(value.get("sourceUrl", ""))))
    content = "".join(json.dumps(value, ensure_ascii=False, sort_keys=True) + "\n" for value in ordered).encode("utf-8")
    writeBytesAtomically(path, content)


def summarize(records: list[dict[str, Any]], booksIndex: dict[str, Any], indexPath: Path, downloadedAtUtc: str, includeMerged: bool, errors: list[dict[str, Any]]) -> dict[str, Any]:
    """Build a compact reproducibility and licensing summary for the completed snapshot."""
    textRecords = [record for record in records if record.get("artifactType") == "text"]
    schemaRecords = [record for record in records if record.get("artifactType") == "schema"]
    return {
        "source": "https://github.com/Sefaria/Sefaria-Export",
        "sourceIndexUrl": BooksIndexUrl,
        "sourceIndexGeneratedAtUtc": booksIndex.get("generated_at"),
        "sourceIndexSha256": hashlib.sha256(indexPath.read_bytes()).hexdigest(),
        "downloadedAtUtc": downloadedAtUtc,
        "selection": {
            "collections": ["Torah", "Tanakh (Prophets and Writings)", "Mishnah", "Talmud (Bavli and Yerushalmi)"],
            "formats": ["json"],
            "includeMerged": includeMerged,
            "commentariesIncluded": False,
        },
        "textFileCount": len(textRecords),
        "schemaFileCount": len(schemaRecords),
        "downloadErrorCount": len(errors),
        "textByteCount": sum(int(record["byteCount"]) for record in textRecords),
        "countsByCollection": dict(sorted(Counter(str(record["collection"]) for record in textRecords).items())),
        "countsByLicenseStatus": dict(sorted(Counter(str(record["licenseStatus"]) for record in textRecords).items())),
        "countsByActualLanguage": dict(sorted(Counter(str(record.get("actualLanguage") or "unknown") for record in textRecords).items())),
    }


def main() -> int:
    """Download the selected core corpus, schemas, and provenance manifests."""
    arguments = parseArguments()
    if arguments.workers < 1 or arguments.workers > 32:
        raise ValueError("--workers must be between 1 and 32.")

    dataRoot = arguments.data_root.resolve()
    metadataRoot = dataRoot / "Raw" / "Sefaria" / "Metadata"
    indexPath = metadataRoot / "books.json"
    downloadedAtUtc = dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    booksIndex = ensureJsonArtifact(indexPath, BooksIndexUrl, arguments.refresh_index)
    if not isinstance(booksIndex, dict) or not isinstance(booksIndex.get("books"), list):
        raise ValueError("The Sefaria books index is not a JSON object with a books array.")
    ensureJsonArtifact(metadataRoot / "table_of_contents.json", TableOfContentsUrl, arguments.refresh_index)

    selectedBooks: list[dict[str, Any]] = []
    for book in booksIndex.get("books", []):
        if not isinstance(book, dict) or not book.get("json_url") or classifyBook(book) is None:
            continue
        if arguments.exclude_merged and str(book.get("versionTitle", "")).casefold() == "merged":
            continue
        selectedBooks.append(book)

    print(f"Selected {len(selectedBooks)} core text versions from the Sefaria index.", flush=True)
    records: list[dict[str, Any]] = []
    errors: list[dict[str, Any]] = []

    with concurrent.futures.ThreadPoolExecutor(max_workers=arguments.workers) as executor:
        futureToBook = {executor.submit(downloadText, dataRoot, book, downloadedAtUtc): book for book in selectedBooks}
        for completedCount, future in enumerate(concurrent.futures.as_completed(futureToBook), start=1):
            book = futureToBook[future]
            try:
                records.append(future.result())
            except Exception as error:
                errors.append({"artifactType": "text", "title": book.get("title"), "versionTitle": book.get("versionTitle"), "sourceUrl": book.get("json_url"), "error": repr(error)})
            if completedCount % 25 == 0 or completedCount == len(selectedBooks):
                print(f"Texts: {completedCount}/{len(selectedBooks)} processed; {len(errors)} errors.", flush=True)

    titles = sorted({str(book["title"]) for book in selectedBooks})
    with concurrent.futures.ThreadPoolExecutor(max_workers=arguments.workers) as executor:
        futureToTitle = {executor.submit(downloadSchema, dataRoot, title, downloadedAtUtc): title for title in titles}
        for completedCount, future in enumerate(concurrent.futures.as_completed(futureToTitle), start=1):
            title = futureToTitle[future]
            try:
                records.append(future.result())
            except Exception as error:
                schemaObjectName = title.replace(" ", "_")
                errors.append({"artifactType": "schema", "title": title, "sourceUrl": f"{SchemaBaseUrl}/{urllib.parse.quote(schemaObjectName, safe='')}.json", "error": repr(error)})
            if completedCount % 25 == 0 or completedCount == len(titles):
                print(f"Schemas: {completedCount}/{len(titles)} processed; {len(errors)} total errors.", flush=True)

    writeJsonLines(metadataRoot / "manifest.jsonl", records)
    if errors:
        writeJsonLines(metadataRoot / "download-errors.jsonl", errors)
    else:
        (metadataRoot / "download-errors.jsonl").unlink(missing_ok=True)
    summary = summarize(records, booksIndex, indexPath, downloadedAtUtc, not arguments.exclude_merged, errors)
    writeJson(metadataRoot / "summary.json", summary)
    print(json.dumps(summary, ensure_ascii=False, indent=2, sort_keys=True), flush=True)
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
