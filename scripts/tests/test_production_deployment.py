from __future__ import annotations

import os
import re
import shutil
import subprocess
import sys
import textwrap
import unittest
from pathlib import Path


RepositoryRoot = Path(__file__).resolve().parents[2]
Workflow = (RepositoryRoot / ".github/workflows/deploy.yml").read_text(encoding="utf-8")
PreflightName = "      - name: Verify production runtime configuration before deployment"
ApiDeployName = "      - name: Deploy immutable API image"
JobDeployName = "      - name: Deploy immutable weekly Dvar Torah image"
AzureFixture = """
az() {
  case "$*" in
    *"account show"*) echo fixture-subscription ;;
    *"/jobs/"*)
      if [ "$MODE" = denied ]; then echo 'fixture authorization failure' >&2; return 1; fi
      if [ "$MODE" = wrongjob ]; then echo /retired/environment; else echo "$EXPECTED_ENVIRONMENT"; fi ;;
    *autoConfigureDataProtection*) if [ "$MODE" = keysdisabled ]; then echo false; else echo true; fi ;;
    *minReplicas*) if [ "$MODE" = alwayson ]; then echo 1; else echo 0; fi ;;
    *managedEnvironmentId*) if [ "$MODE" = wrongapi ]; then echo /retired/environment; else echo "$EXPECTED_ENVIRONMENT"; fi ;;
    *) echo 'unexpected Azure operation' >&2; return 97 ;;
  esac
}
"""


class ProductionDeploymentTests(unittest.TestCase):
    def setUp(self) -> None:
        self.bash = os.environ.get("DEPLOYMENT_TEST_BASH") or shutil.which("bash")
        self.assertIsNotNone(self.bash, "Bash is required to test the Linux deployment workflow.")
        start = Workflow.index(PreflightName)
        end = Workflow.index(ApiDeployName, start)
        self.preflight = textwrap.dedent(Workflow[start:end].split("        run: |\n", 1)[1])

    def runPreflight(self, mode: str) -> subprocess.CompletedProcess[str]:
        environment = {
            **os.environ,
            "MODE": mode,
            "AZURE_RESOURCE_GROUP": "AARProduction",
            "CONTAINER_APPS_ENVIRONMENT_NAME": "askarabbi-containerapps-production",
            "CONTAINER_APP_NAME": "askarabbi-api-production",
            "DVAR_TORAH_JOB_NAME": "askarabbi-dvar-torah-production",
            "EXPECTED_ENVIRONMENT": "/subscriptions/fixture-subscription/resourceGroups/AARProduction/providers/Microsoft.App/managedEnvironments/askarabbi-containerapps-production",
        }
        return subprocess.run([self.bash, "-c", AzureFixture + self.preflight], env=environment, capture_output=True, text=True, timeout=10, check=False)

    def testPreflightRunsBeforeEitherImageIsUpdated(self) -> None:
        self.assertLess(Workflow.index(PreflightName), Workflow.index(ApiDeployName))
        self.assertLess(Workflow.index(PreflightName), Workflow.index(JobDeployName))

    def testValidConsolidatedRuntimeCanDeploy(self) -> None:
        result = self.runPreflight("valid")

        self.assertEqual(0, result.returncode, result.stderr)

    def testInaccessibleJobReportsRequiredJobRole(self) -> None:
        result = self.runPreflight("denied")

        self.assertEqual(1, result.returncode, result.stderr)
        self.assertIn("Container Apps Jobs Contributor", result.stderr)

    def testUnsafeRuntimeConfigurationStopsDeployment(self) -> None:
        for mode, diagnostic in (
            ("wrongjob", "The generator must use the consolidated production environment"),
            ("keysdisabled", "Azure-managed Data Protection must be enabled"),
            ("alwayson", "retain scale-to-zero"),
            ("wrongapi", "retain scale-to-zero"),
        ):
            with self.subTest(mode=mode):
                result = self.runPreflight(mode)

                self.assertEqual(1, result.returncode, result.stderr)
                self.assertIn(diagnostic, result.stderr)

    def testWorkflowShellBlocksHaveValidSyntax(self) -> None:
        blocks = re.findall(r"^ {8}run: \|\n((?: {10}[^\n]*(?:\n|$)|\n)+)", Workflow, re.MULTILINE)
        self.assertTrue(blocks, "No workflow shell blocks were found.")
        for index, block in enumerate(blocks):
            with self.subTest(block=index):
                script = re.sub(r"\$\{\{[^}]+\}\}", "fixture-value", textwrap.dedent(block))
                result = subprocess.run([self.bash, "-n"], input=script, capture_output=True, text=True, timeout=10, check=False)

                self.assertEqual(0, result.returncode, result.stderr)

    def testRegistrationProbeRequiresBooleanAvailabilityIncludingClosedRegistration(self) -> None:
        probe = re.search(r"python3 -c '([^']*Registration availability[^']*)'", Workflow)
        self.assertIsNotNone(probe, "Deployment must validate the live registration response, not only process health.")
        for body, expected_code in (
            ('{"isOpen": true}', 0),
            ('{"isOpen": false}', 0),
            ('{"isOpen": "true"}', 1),
            ('{"isOpen": null}', 1),
            ('{"code": "server_error"}', 1),
        ):
            with self.subTest(body=body):
                result = subprocess.run([sys.executable, "-c", probe.group(1)], input=body, capture_output=True, text=True, timeout=10, check=False)

                self.assertEqual(expected_code, result.returncode, result.stderr)


if __name__ == "__main__":
    unittest.main()
