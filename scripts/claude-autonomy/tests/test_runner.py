"""No external services are needed to test the runner's path safety rules."""
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
from subprocess import CompletedProcess
from contextlib import redirect_stdout
import io

RUNNER = Path(__file__).resolve().parents[1] / "runner.py"
spec = importlib.util.spec_from_file_location("eveo_claude_runner", RUNNER)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class RunnerSafetyTests(unittest.TestCase):
    def test_allows_only_approved_source_test_audit_paths(self):
        self.assertTrue(module.permitted("src/Eve-O-Preview/Services/Implementation/ThumbnailManager.cs"))
        self.assertTrue(module.permitted("src/EveO.Tests/Windows/RegressionTests.cs"))
        self.assertTrue(module.permitted("docs/audits/tm-01.md"))
        for path in (".github/workflows/build-and-test.yml",
                     ".claude/settings.json",
                     ".agents/skills/a/SKILL.md",
                     "src/EveO.Tests/EveO.Tests.csproj",
                     "src/Eve-O-Preview/Eve-O-Preview.csproj",
                     "../secrets.txt", "docs/roadmap.md",
                     "src/EveO.Tests/../../.git/config",
                     "/etc/passwd"):
            with self.subTest(path=path):
                self.assertFalse(module.permitted(path))

    def test_changes_restricted_to_allowlist(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            subprocess.run(["git", "init", "-q"], cwd=root, check=True)
            (root / "README.md").write_text("original\n")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True)
            subprocess.run(["git", "-c", "user.name=Test",
                            "-c", "user.email=test@example.invalid",
                            "commit", "-qm", "baseline"], cwd=root, check=True)
            (root / "src/EveO.Tests").mkdir(parents=True)
            (root / "src/EveO.Tests/SmokeTests.cs").write_text("// Safe\n")
            paths = module.changed_paths(root)
            self.assertEqual(["src/EveO.Tests/SmokeTests.cs"], paths)
            module.check_scope(root, paths)

            (root / ".github").mkdir()
            (root / ".github/hook.yml").write_text("unsafe\n")
            paths = module.changed_paths(root)
            self.assertIn(".github/hook.yml", paths)
            with self.assertRaises(module.Blocked):
                module.check_scope(root, paths)

    def test_gateway_probe_does_not_require_anthropic_login(self):
        with patch.object(module, "run",
                          return_value=CompletedProcess(["claude"], 0, "OK\n", "")) as mocked:
            output = io.StringIO()
            with redirect_stdout(output):
                module.probe_provider(Path("/tmp"))
            called = mocked.call_args.args[0]
            self.assertEqual("claude", called[0])
            self.assertIn("-p", called)
            self.assertNotIn("auth", called)
            self.assertIn("passed", output.getvalue())
            self.assertNotIn("OK", output.getvalue())

    def test_gateway_probe_blocks_nonzero_exit(self):
        with patch.object(module, "run",
                          return_value=CompletedProcess(["claude"], 1, "", "invalid token")):
            with self.assertRaises(module.Blocked) as exc:
                module.probe_provider(Path("/tmp"))
            self.assertNotIn("invalid token", str(exc.exception))

    def test_gateway_probe_blocks_empty_success(self):
        with patch.object(module, "run",
                          return_value=CompletedProcess(["claude"], 0, "", "")):
            with self.assertRaises(module.Blocked):
                module.probe_provider(Path("/tmp"))

    def test_rejects_symlinks_even_in_allowed_directory(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            subprocess.run(["git", "init", "-q"], cwd=root, check=True)
            (root / "README.md").write_text("original\n")
            subprocess.run(["git", "add", "README.md"], cwd=root, check=True)
            subprocess.run(["git", "-c", "user.name=Test",
                            "-c", "user.email=test@example.invalid",
                            "commit", "-qm", "baseline"], cwd=root, check=True)
            target = root / "src/EveO.Tests"
            target.mkdir(parents=True)
            (target / "link.cs").symlink_to(root / "README.md")
            paths = module.changed_paths(root)
            with self.assertRaises(module.Blocked):
                module.check_scope(root, paths)


if __name__ == "__main__":
    unittest.main()
