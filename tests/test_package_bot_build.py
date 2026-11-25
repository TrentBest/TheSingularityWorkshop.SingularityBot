import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).parents[1] / "tools" / "package_bot_build.py"
SPEC = importlib.util.spec_from_file_location("package_bot_build", MODULE_PATH)
package_bot_build = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(package_bot_build)


class PackageBotBuildTests(unittest.TestCase):
    def test_builds_server_sc2_compatible_immutable_package(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            publish = root / "publish"
            publish.mkdir()
            (publish / "SingularityBot.Runner.exe").write_bytes(b"runner")
            (publish / "SingularityBot.dll").write_bytes(b"engine")
            definition = root / "studio-export.json"
            definition.write_text('{"schemaVersion":1,"id":"test"}', encoding="utf-8")
            store = root / "store"

            package, digest = package_bot_build.build_package(
                "bot-identity",
                "build-123",
                publish,
                "SingularityBot.Runner.exe",
                definition,
                store,
            )

            manifest = json.loads((package / "bot-build.json").read_text(encoding="utf-8"))
            self.assertEqual(manifest["schemaVersion"], 1)
            self.assertEqual(manifest["contentSha256"], digest)
            self.assertEqual(manifest["runnerExecutablePath"], "SingularityBot.Runner.exe")
            self.assertEqual(manifest["botDefinitionPath"], "bot-definition.json")
            self.assertEqual(
                {entry["path"] for entry in manifest["files"]},
                {"SingularityBot.Runner.exe", "SingularityBot.dll", "bot-definition.json"},
            )
            self.assertEqual(package.parent.name, "build-123")
            self.assertEqual(package.name, digest)

            # Repeating an identical build is safe and idempotent.
            repeated, repeated_digest = package_bot_build.build_package(
                "bot-identity",
                "build-123",
                publish,
                "SingularityBot.Runner.exe",
                definition,
                store,
            )
            self.assertEqual(repeated, package)
            self.assertEqual(repeated_digest, digest)

    def test_rejects_unsafe_identity_and_runner_paths(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            publish = root / "publish"
            publish.mkdir()
            (publish / "runner.exe").write_bytes(b"runner")
            definition = root / "definition.json"
            definition.write_text("{}", encoding="utf-8")

            with self.assertRaises(ValueError):
                package_bot_build.build_package(
                    "../outside", "build", publish, "runner.exe", definition, root / "store"
                )
            with self.assertRaises(ValueError):
                package_bot_build.build_package(
                    "bot", "build", publish, "../runner.exe", definition, root / "store"
                )

    def test_rejects_symlinks_in_runner_payload(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            publish = root / "publish"
            publish.mkdir()
            outside = root / "outside.dll"
            outside.write_bytes(b"external")
            (publish / "runner.exe").write_bytes(b"runner")
            (publish / "external.dll").symlink_to(outside)
            definition = root / "definition.json"
            definition.write_text("{}", encoding="utf-8")

            with self.assertRaises(ValueError):
                package_bot_build.build_package(
                    "bot", "build", publish, "runner.exe", definition, root / "store"
                )


if __name__ == "__main__":
    unittest.main()
