# Immutable bot-build packaging

The ServerSC2 host does not accept an executable or definition path from a browser or bot entrant. It resolves the admitted ServerApp BotBuildReference from a host-owned package store. This tool creates the package consumed by ServerSC2's FileSystemSc2BotRunnerResolver.

## Publish the Runner payload

Build the Windows runner payload for the host environment:

    dotnet publish src/SingularityBot.Runner/SingularityBot.Runner.csproj ^
      --configuration Release ^
      --runtime win-x64 ^
      --self-contained true ^
      --output artifacts/runner

Use the exact BotDefinition JSON exported from Studio. The checked-in Terran example is suitable for a smoke package; a real entrant build should use its own saved Studio export.

## Create the immutable package

    python tools/package_bot_build.py ^
      --bot-identity-id <registered-bot-identity-id> ^
      --build-id <immutable-build-id> ^
      --runner-publish-dir artifacts/runner ^
      --runner-executable-path SingularityBot.Runner.exe ^
      --definition path/to/studio-export.json ^
      --output-store C:/Workshop/BotPackages

The tool:

- Copies the complete published Runner directory and the selected BotDefinition into one immutable package.
- Rejects unsafe identity/build path segments, unsafe relative paths, invalid JSON, and symlinks in the Runner payload.
- Hashes every payload file and creates the strict version-1 bot-build.json manifest.
- Computes ContentSha256 using the exact canonical algorithm consumed by ServerSC2, including the Runner and BotDefinition entrypoint paths.
- Places the package at {store}/{BotIdentityId}/{BuildId}/{ContentSha256}.
- Is idempotent for an identical existing package and refuses to overwrite a different payload at an existing immutable digest path.

The emitted contentSha256 is the value to register as ServerApp BotBuildReference.ContentSha256. ServerApp must register the same BotIdentityId and BuildId. The package store must be transferred to or mounted by the ServerSC2 host and made writable only by the trusted build-registration pipeline.

## GitHub Actions packaging workflow

The manual **Package immutable bot build** workflow publishes a self-contained Windows Runner, packages the selected definition, and uploads the resulting package tree as a 30-day workflow artifact. Dispatch it from the development branch and provide:

- The already registered ServerApp BotIdentityId.
- A new immutable BuildId.
- The repository path to the exact Studio-exported BotDefinition JSON (the checked-in Terran definition is the smoke-test default).

Download and extract the artifact into the configured ServerSC2 package-store root, preserving the BotIdentityId/BuildId/ContentSha256 directory structure. Register the emitted ContentSha256 with ServerApp before admitting the build. The workflow does not write directly into a production host or change build eligibility.

## Validation

Run the packager contract tests:

    python -m unittest discover -s tests -p "test_package_bot_build.py"

ServerSC2 independently verifies the manifest, every payload hash, the aggregate digest, and both entrypoint paths before starting any SC2 or Runner process. The Runner then validates the BotDefinition's own schema and semantics before connecting to SC2.

This packaging step creates an immutable bot build artifact; it does not register the build in ServerApp, publish a NuGet package, deploy a website, or prove a live SC2 match.
