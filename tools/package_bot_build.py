#!/usr/bin/env python3
"""Create a content-addressed ServerSC2 bot-build package from a published Runner and BotDefinition."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import tempfile
from pathlib import Path, PurePosixPath
from typing import Any


_SAFE_SEGMENT = re.compile(r"^[A-Za-z0-9._-]+$")
_SHA256 = re.compile(r"^[0-9a-f]{64}$")


def _safe_segment(value: str, label: str) -> str:
    if value in {".", ".."} or not _SAFE_SEGMENT.fullmatch(value):
        raise ValueError(f"{label} must be a safe single path segment.")
    return value


def _relative_payload_path(value: str, label: str) -> str:
    path = PurePosixPath(value)
    if path.is_absolute() or not value or any(part in {"", ".", ".."} for part in value.split("/")):
        raise ValueError(f"{label} must be a normalized package-relative path using '/' separators.")
    if "\\" in value:
        raise ValueError(f"{label} cannot contain backslashes.")
    return path.as_posix()


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _canonical_digest(
    files: list[dict[str, str]],
    runner_executable_path: str,
    bot_definition_path: str,
) -> str:
    pieces = [
        "runnerExecutablePath\n",
        runner_executable_path,
        "\n",
        "botDefinitionPath\n",
        bot_definition_path,
        "\n",
    ]
    for item in sorted(files, key=lambda entry: entry["path"]):
        pieces.extend((item["path"], "\n", item["sha256"].lower(), "\n"))
    return hashlib.sha256("".join(pieces).encode("utf-8")).hexdigest()


def _verify_existing(destination: Path, expected_manifest: dict[str, Any]) -> None:
    manifest_path = destination / "bot-build.json"
    if not manifest_path.is_file():
        raise FileExistsError(f"Immutable package path already exists without a manifest: {destination}")
    actual_manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if actual_manifest != expected_manifest:
        raise FileExistsError(f"Immutable package path already contains different metadata: {destination}")
    for entry in expected_manifest["files"]:
        payload = destination / PurePosixPath(entry["path"])
        if not payload.is_file() or _sha256(payload) != entry["sha256"]:
            raise FileExistsError(f"Existing immutable package payload has changed: {payload}")


def build_package(
    bot_identity_id: str,
    build_id: str,
    runner_publish_dir: Path,
    runner_executable_path: str,
    definition_path: Path,
    output_store: Path,
) -> tuple[Path, str]:
    bot_identity_id = _safe_segment(bot_identity_id, "BotIdentityId")
    build_id = _safe_segment(build_id, "BuildId")
    runner_publish_dir = runner_publish_dir.resolve(strict=True)
    definition_path = definition_path.resolve(strict=True)
    if not runner_publish_dir.is_dir():
        raise ValueError("Runner publish path must be a directory.")
    if not definition_path.is_file():
        raise ValueError("BotDefinition path must be a file.")

    runner_relative = _relative_payload_path(runner_executable_path, "Runner executable path")
    runner_source = (runner_publish_dir / PurePosixPath(runner_relative)).resolve(strict=True)
    if runner_publish_dir not in runner_source.parents or not runner_source.is_file():
        raise ValueError("Runner executable must be a file inside the publish directory.")

    # Reject symlinks so the copied package cannot contain references outside its root.
    for source in runner_publish_dir.rglob("*"):
        if source.is_symlink():
            raise ValueError(f"Runner publish output cannot contain symlinks: {source}")

    try:
        definition_document = json.loads(definition_path.read_text(encoding="utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exception:
        raise ValueError("BotDefinition must be valid UTF-8 JSON.") from exception
    if not isinstance(definition_document, dict):
        raise ValueError("BotDefinition JSON must contain an object at its root.")

    output_store = output_store.resolve()
    package_parent = output_store / bot_identity_id / build_id
    package_parent.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix=".bot-build-", dir=package_parent) as temporary:
        staging = Path(temporary) / "package"
        shutil.copytree(runner_publish_dir, staging)
        shutil.copy2(definition_path, staging / "bot-definition.json")

        payload_files = sorted(path for path in staging.rglob("*") if path.is_file())
        files = [
            {
                "path": path.relative_to(staging).as_posix(),
                "sha256": _sha256(path),
            }
            for path in payload_files
        ]
        definition_relative = "bot-definition.json"
        digest = _canonical_digest(files, runner_relative, definition_relative)
        manifest = {
            "schemaVersion": 1,
            "contentSha256": digest,
            "runnerExecutablePath": runner_relative,
            "botDefinitionPath": definition_relative,
            "files": files,
        }
        (staging / "bot-build.json").write_text(
            json.dumps(manifest, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
        )

        destination = package_parent / digest
        if destination.exists():
            _verify_existing(destination, manifest)
        else:
            os.replace(staging, destination)

    return destination, digest


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bot-identity-id", required=True)
    parser.add_argument("--build-id", required=True)
    parser.add_argument("--runner-publish-dir", required=True, type=Path)
    parser.add_argument("--runner-executable-path", required=True)
    parser.add_argument("--definition", required=True, type=Path)
    parser.add_argument("--output-store", required=True, type=Path)
    args = parser.parse_args()

    package_path, digest = build_package(
        args.bot_identity_id,
        args.build_id,
        args.runner_publish_dir,
        args.runner_executable_path,
        args.definition,
        args.output_store,
    )
    print(json.dumps({"packagePath": str(package_path), "contentSha256": digest}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
