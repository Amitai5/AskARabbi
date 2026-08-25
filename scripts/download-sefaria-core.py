#!/usr/bin/env python3
"""Download and inventory the selected permissively licensed Sefaria corpus."""

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
import unicodedata
import urllib.error
import urllib.parse
import urllib.request
from collections import Counter
from pathlib import Path
from typing import Any


BooksIndexUrl = "https://raw.githubusercontent.com/Sefaria/Sefaria-Export/master/books.json"
TableOfContentsUrl = "https://storage.googleapis.com/sefaria-export/table_of_contents.json"
SchemaBaseUrl = "https://storage.googleapis.com/sefaria-export/schemas"
VersionsApiBaseUrl = "https://www.sefaria.org/api/texts/versions"
InvalidFileNameCharacters = re.compile(r'[<>:"/\\|?*\x00-\x1f]')
ReservedWindowsNames = {"CON", "PRN", "AUX", "NUL", *(f"COM{number}" for number in range(1, 10)), *(f"LPT{number}" for number in range(1, 10))}
PermissiveLicenseStatus = "permissive"
PermissiveLicensePatterns = (
    re.compile(r"public domain"),
    re.compile(r"pd"),
    re.compile(r"cc0(?:[ -]\d+(?:\.\d+)*)?"),
    re.compile(r"cc(?:-| )by(?:[ -]\d+(?:\.\d+)*)?"),
    re.compile(r"cc(?:-| )by(?:-| )sa(?:[ -]\d+(?:\.\d+)*)?"),
)
DeniedVersionTitles = frozenset({
    "Miqra Mevoar, trans. and edited by David Kokhav, Jerusalem 2020",
})
DeniedVersionTitleKeys = frozenset(unicodedata.normalize("NFKC", title).casefold() for title in DeniedVersionTitles)
CoreCollections = ("Torah", "Tanakh", "Mishnah", "Talmud")
SupplementalCollections = ("Halakhah", "Kabbalah", "Musar")
CorpusCollections = (*CoreCollections, *SupplementalCollections)
LanguageBucketCodes = {"english": "en", "hebrew": "he"}
ShulchanArukhTitles = frozenset({
    "Shulchan Arukh, Choshen Mishpat",
    "Shulchan Arukh, Even HaEzer",
    "Shulchan Arukh, Orach Chayim",
    "Shulchan Arukh, Yoreh De'ah",
})
ShulchanArukhHebrewEditions = {
    "Shulchan Arukh, Choshen Mishpat": "Shulhan Arukh, Hoshen ha-Mishpat, Lemberg, 1898",
    "Shulchan Arukh, Even HaEzer": "Apei Ravrevei Shulchan Aruch Even HaEzer, Lemberg, 1886",
    "Shulchan Arukh, Orach Chayim": "Maginei Eretz Shulchan Aruch Orach Chaim, Lemberg, 1893",
    "Shulchan Arukh, Yoreh De'ah": "Ashlei Ravrevei Shulchan Aruch Yoreh Deah, Lemberg, 1888",
}
SupplementalUsageNotes = {
    "rif": "The Rif is an early halakhic digest keyed to the Babylonian Talmud. Its English coverage is partial because Sefaria currently exposes permissively licensed English exports for only some tractates.",
    "mishneh_torah": "Mishneh Torah is Maimonides' systematic halakhic code. Later authorities may rule differently, so it should inform research rather than be treated as the sole source for a practical ruling.",
    "shulchan_arukh_with_rema": "The base Shulchan Arukh records Rabbi Yosef Karo's rulings; embedded Rema glosses chiefly record Ashkenazi practice. Neither layer is universally dispositive across Sephardi, Mizrahi, Ashkenazi, and other communities.",
    "zohar": "The Zohar is a foundational work of Jewish mysticism. Its symbolic teachings should not be presented as standalone practical halakhic rulings.",
    "zohar_chadash": "Zohar Chadash is a collection of additional Zoharic material. Its symbolic teachings should not be presented as standalone practical halakhic rulings.",
    "mesillat_yesharim": "Mesillat Yesharim is a Musar work on ethical and spiritual development, not a comprehensive code of practical halakhah.",
}


def parseArguments() -> argparse.Namespace:
    """Parse command-line arguments for a resumable selected-corpus download."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--data-root", type=Path, default=Path("Data"), help="Project data directory (default: Data).")
    parser.add_argument("--workers", type=int, default=8, help="Concurrent downloads (default: 8).")
    parser.add_argument("--refresh-index", action="store_true", help="Replace the local books.json with the latest upstream index.")
    parser.add_argument("--refresh-license-metadata", action="store_true", help="Refresh the permissive-version catalog from Sefaria's Versions API before downloading texts.")
    parser.add_argument("--exclude-merged", action="store_true", help=argparse.SUPPRESS)
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
    """Map one selected Sefaria primary-text category path into the local collection layout."""
    categories = book.get("categories") or []
    sourceTitle = str(book.get("title") or "untitled")
    title = safePathPart(sourceTitle)
    languageBucket = safePathPart(str(book.get("language") or "Unknown"))

    if categories == ["Tanakh", "Torah"]:
        return "Torah", [title, languageBucket]
    if len(categories) == 2 and categories[0] == "Tanakh" and categories[1] in {"Prophets", "Writings"}:
        return "Tanakh", [safePathPart(categories[1]), title, languageBucket]
    if len(categories) == 2 and categories[0] == "Mishnah" and str(categories[1]).startswith("Seder "):
        return "Mishnah", [safePathPart(categories[1]), title, languageBucket]
    if len(categories) == 3 and categories[0] == "Talmud" and categories[1] in {"Bavli", "Yerushalmi"} and (str(categories[2]).startswith("Seder ") or categories[2] == "Minor Tractates"):
        return "Talmud", [safePathPart(categories[1]), safePathPart(categories[2]), title, languageBucket]
    if len(categories) >= 4 and categories[:4] == ["Talmud", "Bavli", "Rishonim on Talmud", "Rif"] and sourceTitle.startswith("Rif "):
        return "Halakhah", ["Rif", title, languageBucket]
    if len(categories) >= 2 and categories[:2] == ["Halakhah", "Mishneh Torah"] and sourceTitle.startswith("Mishneh Torah, "):
        return "Halakhah", ["Mishneh Torah", title, languageBucket]
    if categories == ["Halakhah", "Shulchan Arukh"] and sourceTitle in ShulchanArukhTitles:
        return "Halakhah", ["Shulchan Arukh with Rema", title, languageBucket]
    if categories == ["Kabbalah", "Zohar"] and sourceTitle == "Zohar":
        return "Kabbalah", [title, languageBucket]
    if categories == ["Kabbalah", "Zohar"] and sourceTitle == "Zohar Chadash":
        return "Kabbalah", [title, languageBucket]
    if categories == ["Musar", "Acharonim"] and sourceTitle == "Mesillat Yesharim":
        return "Musar", [title, languageBucket]
    return None


def supplementalWorkKey(book: dict[str, Any]) -> str | None:
    """Return the curated supplemental work key for one export record."""
    title = str(book.get("title") or "")
    categories = book.get("categories") or []
    if len(categories) >= 4 and categories[:4] == ["Talmud", "Bavli", "Rishonim on Talmud", "Rif"] and title.startswith("Rif "):
        return "rif"
    if len(categories) >= 2 and categories[:2] == ["Halakhah", "Mishneh Torah"] and title.startswith("Mishneh Torah, "):
        return "mishneh_torah"
    if categories == ["Halakhah", "Shulchan Arukh"] and title in ShulchanArukhTitles:
        return "shulchan_arukh_with_rema"
    if categories == ["Kabbalah", "Zohar"] and title == "Zohar":
        return "zohar"
    if categories == ["Kabbalah", "Zohar"] and title == "Zohar Chadash":
        return "zohar_chadash"
    if categories == ["Musar", "Acharonim"] and title == "Mesillat Yesharim":
        return "mesillat_yesharim"
    return None


def editionPreferences(book: dict[str, Any]) -> tuple[str, ...]:
    """Return ordered edition-title preferences for one supplemental title and language bucket."""
    workKey = supplementalWorkKey(book)
    title = str(book.get("title") or "")
    languageBucket = str(book.get("language") or "").casefold()
    if workKey == "rif" and languageBucket == "hebrew":
        return ("Vilna Edition",)
    if workKey == "rif" and languageBucket == "english":
        return ("Sefaria Community Translation",)
    if workKey == "mishneh_torah" and languageBucket == "hebrew":
        return ("Torat Emet *", "Wikisource Mishneh Torah")
    if workKey == "mishneh_torah" and languageBucket == "english":
        return (
            "The Mishneh Torah by Maimonides. trans. by Moses Hyamson, 1937-1949",
            "Sefaria Community Translation",
            "Sefaria Edition. Translated by R. Francis Nataf, 2019",
        )
    if workKey == "shulchan_arukh_with_rema" and languageBucket == "hebrew":
        selectedTitle = ShulchanArukhHebrewEditions.get(title)
        return (selectedTitle,) if selectedTitle else ()
    if workKey == "shulchan_arukh_with_rema" and languageBucket == "english":
        return ("Sefaria Community Translation",)
    if workKey == "zohar" and languageBucket == "english":
        return ("The Zohar; London, Soncino Press, 1933",)
    if workKey == "zohar_chadash" and languageBucket == "hebrew":
        return ("Zohar Chadash",)
    if workKey == "zohar_chadash" and languageBucket == "english":
        return ("Sefaria Community Translation",)
    if workKey == "mesillat_yesharim" and languageBucket == "hebrew":
        return ("Sefaria Vocalized Edition",)
    if workKey == "mesillat_yesharim" and languageBucket == "english":
        return ("Path of the Just. Trans. Rabbi Yosef Sebag",)
    return ()


def editionPreferenceIndex(versionTitle: str, preferences: tuple[str, ...]) -> int | None:
    """Return an edition's zero-based preference index, supporting a trailing wildcard."""
    normalizedVersionTitle = normalizeVersionIdentifier(versionTitle)
    for index, preference in enumerate(preferences):
        isPrefix = preference.endswith("*")
        normalizedPreference = normalizeVersionIdentifier(preference.removesuffix("*").strip())
        if normalizedVersionTitle == normalizedPreference or (isPrefix and normalizedVersionTitle.startswith(normalizedPreference)):
            return index
    return None


def selectPreferredBooks(candidateBooks: list[dict[str, Any]], allowedSourceUrls: set[str]) -> list[dict[str, Any]]:
    """Select all permissive core editions and one preferred permissible supplemental edition per title and language."""
    selectedBooks = [
        book
        for book in candidateBooks
        if str(book.get("json_url") or "") in allowedSourceUrls and supplementalWorkKey(book) is None
    ]
    supplementalCandidates: dict[tuple[str, str, str], list[tuple[int, str, dict[str, Any]]]] = {}
    for book in candidateBooks:
        sourceUrl = str(book.get("json_url") or "")
        workKey = supplementalWorkKey(book)
        if sourceUrl not in allowedSourceUrls or workKey is None:
            continue
        preferenceIndex = editionPreferenceIndex(str(book.get("versionTitle") or ""), editionPreferences(book))
        if preferenceIndex is None:
            continue
        key = (workKey, str(book.get("title") or ""), str(book.get("language") or ""))
        supplementalCandidates.setdefault(key, []).append((preferenceIndex, sourceUrl, book))
    for candidates in supplementalCandidates.values():
        selectedBooks.append(min(candidates, key=lambda candidate: (candidate[0], candidate[1]))[2])
    return sorted(selectedBooks, key=lambda book: str(book.get("json_url") or ""))


def localTextPath(dataRoot: Path, book: dict[str, Any], relativeParts: list[str]) -> Path:
    """Build a readable collision-resistant local path for one versioned text."""
    sourceUrl = str(book["json_url"])
    versionTitle = safePathPart(str(book.get("versionTitle") or "untitled"))
    urlHash = hashlib.sha256(sourceUrl.encode("utf-8")).hexdigest()[:12]
    return dataRoot / "Raw" / "Sefaria" / str(book["localCollection"]) / Path(*relativeParts) / f"{versionTitle}--{urlHash}.json"


def licenseStatus(versionTitle: str, licenseName: str | None) -> str:
    """Classify a source license conservatively for downstream filtering."""
    if isDeniedVersionTitle(versionTitle):
        return "excluded"
    if versionTitle.casefold() == "merged":
        return "review_required"
    normalized = (licenseName or "").strip().casefold()
    if not normalized or normalized == "unknown":
        return "review_required"
    if "-nc" in normalized or "noncommercial" in normalized or "non-commercial" in normalized:
        return "noncommercial"
    if any(pattern.fullmatch(normalized) for pattern in PermissiveLicensePatterns):
        return PermissiveLicenseStatus
    return "review_required"


def isDeniedVersionTitle(versionTitle: str) -> bool:
    """Return whether a source edition is explicitly denied after a source-level license review."""
    normalizedTitle = unicodedata.normalize("NFKC", versionTitle).strip().casefold()
    return normalizedTitle in DeniedVersionTitleKeys


def normalizeVersionIdentifier(value: str) -> str:
    """Normalize a version title for matching API metadata to export filenames."""
    normalized = unicodedata.normalize("NFKC", value).casefold()
    return "".join(character for character in normalized if character.isalnum())


def readJsonLines(path: Path) -> list[dict[str, Any]]:
    """Read and validate one UTF-8 JSON Lines file."""
    records: list[dict[str, Any]] = []
    for lineNumber, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        if not line.strip():
            continue
        value = json.loads(line)
        if not isinstance(value, dict):
            raise ValueError(f"Expected a JSON object in {path} at line {lineNumber}.")
        records.append(value)
    return records


def createPermissiveCatalogFromRawManifest(manifestPath: Path, candidateBooks: list[dict[str, Any]], sourceIndexSha256: str, generatedAtUtc: str) -> dict[str, Any]:
    """Bootstrap a permissive source catalog from an already-audited raw manifest."""
    candidateUrls = {str(book["json_url"]) for book in candidateBooks}
    versions: list[dict[str, Any]] = []
    for record in readJsonLines(manifestPath):
        if record.get("artifactType") != "text" or record.get("licenseStatus") != PermissiveLicenseStatus:
            continue
        sourceUrl = str(record.get("sourceUrl") or "")
        versionTitle = str(record.get("versionTitle") or "")
        licenseName = str(record.get("license") or "")
        if sourceUrl not in candidateUrls or licenseStatus(versionTitle, licenseName) != PermissiveLicenseStatus:
            continue
        versions.append({
            "sourceUrl": sourceUrl,
            "title": record.get("title"),
            "versionTitle": versionTitle,
            "languageBucket": record.get("languageBucket"),
            "actualLanguage": record.get("actualLanguage"),
            "license": record.get("license"),
            "licenseStatus": PermissiveLicenseStatus,
        })
    return createPermissiveCatalog(versions, sourceIndexSha256, generatedAtUtc, "Raw/Sefaria/Metadata/manifest.jsonl")


def fetchPermissiveVersionMetadata(title: str) -> list[dict[str, Any]]:
    """Fetch only permissively licensed version metadata for one Sefaria index title."""
    sourceUrl = f"{VersionsApiBaseUrl}/{urllib.parse.quote(title, safe='')}"
    value = json.loads(downloadBytes(sourceUrl).decode("utf-8"))
    if not isinstance(value, list):
        raise ValueError(f"Sefaria Versions API did not return a list for {title!r}.")
    versions: list[dict[str, Any]] = []
    for item in value:
        if not isinstance(item, dict):
            continue
        versionTitle = str(item.get("versionTitle") or "")
        licenseName = str(item.get("license") or "")
        if licenseStatus(versionTitle, licenseName) == PermissiveLicenseStatus:
            versions.append(item)
    return versions


def selectPermissiveVersionMetadata(book: dict[str, Any], metadataByTitle: dict[str, list[dict[str, Any]]]) -> dict[str, Any] | None:
    """Match one export entry to unambiguous permissive metadata, tolerating equivalent punctuation variants."""
    title = str(book["title"])
    languageCode = LanguageBucketCodes.get(str(book.get("language") or "").casefold())
    versionIdentifier = normalizeVersionIdentifier(str(book.get("versionTitle") or ""))
    matches = [
        item
        for item in metadataByTitle.get(title, [])
        if str(item.get("language") or "").casefold() == languageCode and normalizeVersionIdentifier(str(item.get("versionTitle") or "")) == versionIdentifier
    ]
    if not matches:
        return None
    identities = {
        (
            str(item.get("license") or "").strip().casefold(),
            str(item.get("language") or "").strip().casefold(),
            str(item.get("actualLanguage") or "").strip().casefold(),
            str(item.get("versionSource") or "").strip(),
        )
        for item in matches
    }
    if len(identities) > 1:
        raise ValueError(f"Multiple conflicting permissive API versions matched {title!r} / {book.get('versionTitle')!r} / {book.get('language')!r}.")
    return min(matches, key=lambda item: str(item.get("versionTitle") or ""))


def createPermissiveCatalogFromApi(candidateBooks: list[dict[str, Any]], sourceIndexSha256: str, generatedAtUtc: str, workers: int) -> dict[str, Any]:
    """Build a fail-closed permissive source catalog from Sefaria's Versions API."""
    titles = sorted({str(book["title"]) for book in candidateBooks})
    metadataByTitle: dict[str, list[dict[str, Any]]] = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=min(workers, 4)) as executor:
        futureToTitle = {executor.submit(fetchPermissiveVersionMetadata, title): title for title in titles}
        for completedCount, future in enumerate(concurrent.futures.as_completed(futureToTitle), start=1):
            title = futureToTitle[future]
            metadataByTitle[title] = future.result()
            if completedCount % 25 == 0 or completedCount == len(titles):
                print(f"License metadata: {completedCount}/{len(titles)} titles processed.", flush=True)

    versions: list[dict[str, Any]] = []
    for book in candidateBooks:
        title = str(book["title"])
        metadata = selectPermissiveVersionMetadata(book, metadataByTitle)
        if metadata is None:
            continue
        versions.append({
            "sourceUrl": book["json_url"],
            "title": title,
            "versionTitle": metadata.get("versionTitle"),
            "exportVersionTitle": book.get("versionTitle"),
            "languageBucket": book.get("language"),
            "actualLanguage": metadata.get("actualLanguage"),
            "license": metadata.get("license"),
            "licenseStatus": PermissiveLicenseStatus,
        })
    return createPermissiveCatalog(versions, sourceIndexSha256, generatedAtUtc, VersionsApiBaseUrl)


def createPermissiveCatalog(versions: list[dict[str, Any]], sourceIndexSha256: str, generatedAtUtc: str, metadataSource: str) -> dict[str, Any]:
    """Create a deterministic allowlist containing permissive version metadata only."""
    orderedVersions = sorted(versions, key=lambda version: str(version.get("sourceUrl") or ""))
    sourceUrls = [str(version.get("sourceUrl") or "") for version in orderedVersions]
    if not sourceUrls or any(not sourceUrl for sourceUrl in sourceUrls):
        raise ValueError("The permissive source catalog must contain at least one valid source URL.")
    if len(sourceUrls) != len(set(sourceUrls)):
        raise ValueError("The permissive source catalog contains duplicate source URLs.")
    return {
        "schemaVersion": "1",
        "sourceProvider": "Sefaria",
        "generatedAtUtc": generatedAtUtc,
        "sourceIndexSha256": sourceIndexSha256,
        "licenseMetadataSource": metadataSource,
        "allowedLicenseStatus": PermissiveLicenseStatus,
        "deniedVersionTitles": sorted(DeniedVersionTitles),
        "description": "Allowlist of Sefaria export text versions whose version-level license is classified as permissive.",
        "versionCount": len(orderedVersions),
        "versions": orderedVersions,
    }


def loadPermissiveCatalog(path: Path, expectedSourceIndexSha256: str) -> dict[str, Any]:
    """Load and validate the permissive source catalog against the active books index."""
    value = readJson(path)
    if not isinstance(value, dict) or value.get("schemaVersion") != "1" or value.get("allowedLicenseStatus") != PermissiveLicenseStatus:
        raise ValueError(f"Invalid permissive source catalog: {path}")
    if value.get("sourceIndexSha256") != expectedSourceIndexSha256:
        raise ValueError(f"The permissive source catalog was built for a different books index. Run with --refresh-license-metadata: {path}")
    versions = value.get("versions")
    if not isinstance(versions, list) or value.get("versionCount") != len(versions):
        raise ValueError(f"Invalid version list in permissive source catalog: {path}")
    sanitizedVersions: list[dict[str, Any]] = []
    for version in versions:
        if not isinstance(version, dict):
            raise ValueError(f"Invalid version entry in permissive source catalog: {path}")
        versionTitle = str(version.get("versionTitle") or "")
        exportVersionTitle = str(version.get("exportVersionTitle") or "")
        if isDeniedVersionTitle(versionTitle) or isDeniedVersionTitle(exportVersionTitle):
            continue
        licenseName = str(version.get("license") or "")
        if version.get("licenseStatus") != PermissiveLicenseStatus or licenseStatus(versionTitle, licenseName) != PermissiveLicenseStatus or not version.get("sourceUrl"):
            raise ValueError(f"Non-permissive or incomplete entry in permissive source catalog: {path}")
        sanitizedVersions.append(version)
    expectedDeniedVersionTitles = sorted(DeniedVersionTitles)
    if len(sanitizedVersions) == len(versions) and value.get("deniedVersionTitles") == expectedDeniedVersionTitles:
        return value
    sanitizedCatalog = dict(value)
    sanitizedCatalog["deniedVersionTitles"] = expectedDeniedVersionTitles
    sanitizedCatalog["versionCount"] = len(sanitizedVersions)
    sanitizedCatalog["versions"] = sanitizedVersions
    writeJson(path, sanitizedCatalog)
    return sanitizedCatalog


def resolvePermissiveCatalog(catalogPath: Path, rawManifestPath: Path, candidateBooks: list[dict[str, Any]], sourceIndexSha256: str, generatedAtUtc: str, refresh: bool, workers: int) -> dict[str, Any]:
    """Load, bootstrap, or refresh the catalog used to prevent non-permissive text downloads."""
    if refresh:
        catalog = createPermissiveCatalogFromApi(candidateBooks, sourceIndexSha256, generatedAtUtc, workers)
        writeJson(catalogPath, catalog)
        return catalog
    if catalogPath.is_file():
        return loadPermissiveCatalog(catalogPath, sourceIndexSha256)
    if rawManifestPath.is_file():
        catalog = createPermissiveCatalogFromRawManifest(rawManifestPath, candidateBooks, sourceIndexSha256, generatedAtUtc)
        writeJson(catalogPath, catalog)
        return catalog
    catalog = createPermissiveCatalogFromApi(candidateBooks, sourceIndexSha256, generatedAtUtc, workers)
    writeJson(catalogPath, catalog)
    return catalog


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
    workKey = supplementalWorkKey(book)
    if workKey is not None:
        record["workKey"] = workKey
        record["usageNote"] = SupplementalUsageNotes[workKey]
    if versionTitle.casefold() == "merged":
        record["mergedVersions"] = value.get("versions") or []
    return record


def downloadText(dataRoot: Path, book: dict[str, Any], downloadedAtUtc: str) -> dict[str, Any]:
    """Download or reuse one allowlisted Sefaria JSON text and reject license drift."""
    classification = classifyBook(book)
    if classification is None:
        raise ValueError(f"Book is outside the selected corpus: {book.get('title')}")
    collection, relativeParts = classification
    selectedBook = dict(book)
    selectedBook["localCollection"] = collection
    path = localTextPath(dataRoot, selectedBook, relativeParts)
    if path.exists():
        try:
            record = inspectText(path, selectedBook, dataRoot, downloadedAtUtc)
            if record.get("licenseStatus") != PermissiveLicenseStatus:
                raise ValueError(f"Text is no longer permissively licensed: {book.get('title')} / {book.get('versionTitle')}")
            return record
        except (OSError, UnicodeError, json.JSONDecodeError, ValueError):
            path.unlink(missing_ok=True)
    writeBytesAtomically(path, downloadBytes(str(selectedBook["json_url"])))
    try:
        record = inspectText(path, selectedBook, dataRoot, downloadedAtUtc)
        if record.get("licenseStatus") != PermissiveLicenseStatus:
            raise ValueError(f"Downloaded text is not permissively licensed: {book.get('title')} / {book.get('versionTitle')}")
        return record
    except BaseException:
        path.unlink(missing_ok=True)
        raise


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


def pruneUnlistedFiles(root: Path, expectedPaths: set[Path], pattern: str) -> int:
    """Delete generated files absent from the new allowlisted manifest and remove empty directories."""
    if not root.is_dir():
        return 0
    resolvedRoot = root.resolve()
    removedCount = 0
    for path in root.rglob(pattern):
        resolvedPath = path.resolve()
        try:
            resolvedPath.relative_to(resolvedRoot)
        except ValueError as exception:
            raise ValueError(f"Refusing to prune a path outside {resolvedRoot}: {resolvedPath}") from exception
        if resolvedPath not in expectedPaths:
            path.unlink()
            removedCount += 1
    directories = sorted((path for path in root.rglob("*") if path.is_dir()), key=lambda path: len(path.parts), reverse=True)
    for directory in directories:
        try:
            directory.rmdir()
        except OSError:
            pass
    return removedCount


def pruneRawCorpus(dataRoot: Path, records: list[dict[str, Any]]) -> tuple[int, int]:
    """Prune raw text and schema files that are not referenced by the permissive manifest."""
    providerRoot = dataRoot / "Raw" / "Sefaria"
    expectedTextPaths = {(dataRoot / str(record["localPath"])).resolve() for record in records if record.get("artifactType") == "text"}
    expectedSchemaPaths = {(dataRoot / str(record["localPath"])).resolve() for record in records if record.get("artifactType") == "schema"}
    removedTextCount = sum(pruneUnlistedFiles(providerRoot / collection, expectedTextPaths, "*.json") for collection in CorpusCollections)
    removedSchemaCount = pruneUnlistedFiles(providerRoot / "Metadata" / "Schemas", expectedSchemaPaths, "*.json")
    return removedTextCount, removedSchemaCount


def summarize(records: list[dict[str, Any]], booksIndex: dict[str, Any], indexPath: Path, downloadedAtUtc: str, errors: list[dict[str, Any]], prunedTextFileCount: int, prunedSchemaFileCount: int) -> dict[str, Any]:
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
            "collections": ["Torah", "Tanakh (Prophets and Writings)", "Mishnah", "Talmud (Bavli and Yerushalmi)", "Rif", "Mishneh Torah", "Shulchan Arukh with Rema", "Zohar", "Zohar Chadash", "Mesillat Yesharim"],
            "formats": ["json"],
            "includeMerged": False,
            "commentariesIncluded": False,
            "licenseStatuses": [PermissiveLicenseStatus],
            "deniedVersionTitles": sorted(DeniedVersionTitles),
        },
        "textFileCount": len(textRecords),
        "schemaFileCount": len(schemaRecords),
        "prunedTextFileCount": prunedTextFileCount,
        "prunedSchemaFileCount": prunedSchemaFileCount,
        "downloadErrorCount": len(errors),
        "textByteCount": sum(int(record["byteCount"]) for record in textRecords),
        "countsByCollection": dict(sorted(Counter(str(record["collection"]) for record in textRecords).items())),
        "countsByLicenseStatus": dict(sorted(Counter(str(record["licenseStatus"]) for record in textRecords).items())),
        "countsByActualLanguage": dict(sorted(Counter(str(record.get("actualLanguage") or "unknown") for record in textRecords).items())),
        "countsByWorkKey": dict(sorted(Counter(str(record["workKey"]) for record in textRecords if record.get("workKey")).items())),
        "countsByWorkKeyAndActualLanguage": dict(sorted(Counter(f"{record['workKey']}:{record.get('actualLanguage') or 'unknown'}" for record in textRecords if record.get("workKey")).items())),
    }


def main() -> int:
    """Download the selected corpus, schemas, and provenance manifests."""
    arguments = parseArguments()
    if arguments.workers < 1 or arguments.workers > 32:
        raise ValueError("--workers must be between 1 and 32.")

    dataRoot = arguments.data_root.resolve()
    metadataRoot = dataRoot / "Raw" / "Sefaria" / "Metadata"
    indexPath = metadataRoot / "books.json"
    rawManifestPath = metadataRoot / "manifest.jsonl"
    permissiveCatalogPath = metadataRoot / "permissive-versions.json"
    downloadedAtUtc = dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    booksIndex = ensureJsonArtifact(indexPath, BooksIndexUrl, arguments.refresh_index)
    if not isinstance(booksIndex, dict) or not isinstance(booksIndex.get("books"), list):
        raise ValueError("The Sefaria books index is not a JSON object with a books array.")
    ensureJsonArtifact(metadataRoot / "table_of_contents.json", TableOfContentsUrl, arguments.refresh_index)

    candidateBooks: list[dict[str, Any]] = []
    for book in booksIndex.get("books", []):
        if not isinstance(book, dict) or not book.get("json_url") or classifyBook(book) is None:
            continue
        versionTitle = str(book.get("versionTitle", ""))
        if versionTitle.casefold() == "merged" or isDeniedVersionTitle(versionTitle):
            continue
        candidateBooks.append(book)

    sourceIndexSha256 = hashlib.sha256(indexPath.read_bytes()).hexdigest()
    permissiveCatalog = resolvePermissiveCatalog(
        permissiveCatalogPath,
        rawManifestPath,
        candidateBooks,
        sourceIndexSha256,
        downloadedAtUtc,
        arguments.refresh_license_metadata or arguments.refresh_index,
        arguments.workers,
    )
    allowedSourceUrls = {str(version["sourceUrl"]) for version in permissiveCatalog["versions"]}
    selectedBooks = selectPreferredBooks(candidateBooks, allowedSourceUrls)

    supplementalCount = sum(1 for book in selectedBooks if supplementalWorkKey(book) is not None)
    print(f"Selected {len(selectedBooks)} permissively licensed text versions, including {supplementalCount} curated supplemental versions, from {len(candidateBooks)} non-merged candidates.", flush=True)
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

    writeJsonLines(rawManifestPath, records)
    if errors:
        writeJsonLines(metadataRoot / "download-errors.jsonl", errors)
    else:
        (metadataRoot / "download-errors.jsonl").unlink(missing_ok=True)
    prunedTextFileCount = 0
    prunedSchemaFileCount = 0
    if not errors:
        prunedTextFileCount, prunedSchemaFileCount = pruneRawCorpus(dataRoot, records)
    summary = summarize(records, booksIndex, indexPath, downloadedAtUtc, errors, prunedTextFileCount, prunedSchemaFileCount)
    writeJson(metadataRoot / "summary.json", summary)
    print(json.dumps(summary, ensure_ascii=False, indent=2, sort_keys=True), flush=True)
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
