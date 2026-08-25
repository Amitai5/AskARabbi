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

    def testDeniedMiqraMevoarVersionIsRejectedAtEveryBoundary(self) -> None:
        versionTitle = "Miqra Mevoar, trans. and edited by David Kokhav, Jerusalem 2020"
        record = {"licenseStatus": "permissive", "license": "PD", "versionTitle": versionTitle}

        self.assertEqual("excluded", Downloader.licenseStatus(versionTitle, "PD"))
        self.assertFalse(Normalizer.isPermissiveRecord(record))
        self.assertFalse(ManifestGenerator.isPermissiveLicense(versionTitle, "PD"))

    def testLicenseLabelsMapToStableCategories(self) -> None:
        expectedCategories = {
            "PD": "publicDomain",
            "Public Domain": "publicDomain",
            "CC0": "cc0",
            "CC-BY": "ccBy",
            "CC BY-SA 4.0": "ccBySa",
        }
        for licenseName, expectedCategory in expectedCategories.items():
            with self.subTest(licenseName=licenseName):
                self.assertEqual(expectedCategory, ManifestGenerator.licenseCategory(licenseName))

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

    def testSupplementalClassificationIsLimitedToRequestedPrimaryWorks(self) -> None:
        requested = {
            "title": "Mishneh Torah, Foundations of the Torah",
            "language": "English",
            "categories": ["Halakhah", "Mishneh Torah", "Sefer Madda"],
        }
        unrelated = {
            "title": "Commentary on Mishneh Torah",
            "language": "Hebrew",
            "categories": ["Halakhah", "Commentary"],
        }

        self.assertEqual(("Halakhah", ["Mishneh Torah", "Mishneh Torah, Foundations of the Torah", "English"]), Downloader.classifyBook(requested))
        self.assertIsNone(Downloader.classifyBook(unrelated))

    def testPreferredSupplementalEditionFallsBackOnlyWithinPermissiveAllowlist(self) -> None:
        candidates = [
            {
                "title": "Mishneh Torah, Foundations of the Torah",
                "language": "Hebrew",
                "categories": ["Halakhah", "Mishneh Torah", "Sefer Madda"],
                "versionTitle": "Torat Emet 363",
                "json_url": "https://example.test/torat-emet.json",
            },
            {
                "title": "Mishneh Torah, Foundations of the Torah",
                "language": "Hebrew",
                "categories": ["Halakhah", "Mishneh Torah", "Sefer Madda"],
                "versionTitle": "Wikisource Mishneh Torah",
                "json_url": "https://example.test/wikisource.json",
            },
        ]

        selected = Downloader.selectPreferredBooks(candidates, {"https://example.test/wikisource.json"})

        self.assertEqual([candidates[1]], selected)

    def testRemaSmallTextIsLabeledDuringNormalization(self) -> None:
        normalized = Normalizer.normalizeText("Base <i data-commentator=\"Example\"></i> text <small>הגה Rema gloss</small> conclusion", "Rema")

        self.assertIn("**Rema:** הגה Rema gloss", normalized)
        self.assertNotIn("****", normalized)

    def testNestedComplexSchemaNodesAreNormalizedWithCanonicalReferences(self) -> None:
        payload = {"title": "Zohar", "text": {"Addenda": {"Volume I": [["First addendum"]]}}}
        schema = {
            "schema": {
                "nodes": [
                    {
                        "key": "Addenda",
                        "title": "Addenda",
                        "nodes": [
                            {
                                "key": "Volume I",
                                "title": "Volume I",
                                "nodeType": "JaggedArrayNode",
                                "addressTypes": ["Integer", "Integer"],
                            }
                        ],
                    }
                ]
            }
        }

        segments = list(Normalizer.iterateDocumentSegments(payload, schema))

        self.assertEqual([("Zohar, Addenda, Volume I 1:1", "First addendum")], segments)


if __name__ == "__main__":
    unittest.main()
