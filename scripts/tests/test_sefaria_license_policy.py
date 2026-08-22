from __future__ import annotations

import importlib.util
import types
import unittest
from pathlib import Path


ScriptsRoot = Path(__file__).resolve().parent.parent


def loadScript(moduleName: str, fileName: str) -> types.ModuleType:
    """Load one data-pipeline script as a module despite its hyphenated filename."""
    specification = importlib.util.spec_from_file_location(moduleName, ScriptsRoot / fileName)
    if specification is None or specification.loader is None:
        raise RuntimeError(f"Unable to load {fileName}.")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


Downloader = loadScript("download_sefaria_core", "download-sefaria-core.py")
Normalizer = loadScript("normalize_sefaria_markdown", "normalize-sefaria-markdown.py")
ManifestGenerator = loadScript("create_sefaria_document_manifest", "create-sefaria-document-manifest.py")


class LicensePolicyTests(unittest.TestCase):
    def testExplicitPermissiveLicensesAreAcceptedAtEveryBoundary(self) -> None:
        for licenseName in ("PD", "Public Domain", "CC0", "CC0 1.0", "CC-BY", "CC BY 4.0", "CC-BY-SA", "CC BY-SA 4.0"):
            with self.subTest(licenseName=licenseName):
                record = {"licenseStatus": "permissive", "license": licenseName, "versionTitle": "Test Version"}
                self.assertEqual("permissive", Downloader.licenseStatus("Test Version", licenseName))
                self.assertTrue(Normalizer.isPermissiveRecord(record))
                self.assertTrue(ManifestGenerator.isPermissiveLicense("Test Version", licenseName))

    def testRestrictedOrAmbiguousLicensesAreRejectedAtEveryBoundary(self) -> None:
        expectedStatuses = {
            "CC-BY-NC": "noncommercial",
            "CC-BY-NC-SA": "noncommercial",
            "CC-BY-ND": "review_required",
            "Public Domain Mark": "review_required",
            "Unknown": "review_required",
            "": "review_required",
        }
        for licenseName, expectedStatus in expectedStatuses.items():
            with self.subTest(licenseName=licenseName):
                record = {"licenseStatus": "permissive", "license": licenseName, "versionTitle": "Test Version"}
                self.assertEqual(expectedStatus, Downloader.licenseStatus("Test Version", licenseName))
                self.assertFalse(Normalizer.isPermissiveRecord(record))
                self.assertFalse(ManifestGenerator.isPermissiveLicense("Test Version", licenseName))

    def testMergedVersionIsRejectedDespitePermissiveLicense(self) -> None:
        record = {"licenseStatus": "permissive", "license": "CC0", "versionTitle": "Merged"}

        self.assertEqual("review_required", Downloader.licenseStatus("Merged", "CC0"))
        self.assertFalse(Normalizer.isPermissiveRecord(record))
        self.assertFalse(ManifestGenerator.isPermissiveLicense("Merged", "CC0"))

    def testEquivalentApiPunctuationVariantsCanBeMatched(self) -> None:
        book = {"title": "Daniel", "language": "Hebrew", "versionTitle": "דניאל בתרגום עברי גורדון"}
        first = {
            "versionTitle": "דניאל בתרגום עברי(גורדון) ",
            "language": "he",
            "actualLanguage": "he",
            "license": "CC-BY-SA",
            "versionSource": "https://example.test/source",
        }
        second = dict(first, versionTitle="דניאל בתרגום עברי (גורדון)")

        result = Downloader.selectPermissiveVersionMetadata(book, {"Daniel": [first, second]})

        self.assertIn(result, (first, second))

    def testConflictingApiMatchesFailClosed(self) -> None:
        book = {"title": "Daniel", "language": "Hebrew", "versionTitle": "דניאל בתרגום עברי גורדון"}
        first = {
            "versionTitle": "דניאל בתרגום עברי(גורדון)",
            "language": "he",
            "actualLanguage": "he",
            "license": "CC-BY",
            "versionSource": "https://example.test/source",
        }
        second = dict(first, versionTitle="דניאל בתרגום עברי (גורדון)", license="CC-BY-SA")

        with self.assertRaises(ValueError):
            Downloader.selectPermissiveVersionMetadata(book, {"Daniel": [first, second]})


if __name__ == "__main__":
    unittest.main()
