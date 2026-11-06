# Source migration audit

## Imported upstream adapter

The first code port is taken from [SimonPrins/ExampleBot](https://github.com/SimonPrins/ExampleBot), upstream commit:

- Repository: `SimonPrins/ExampleBot`
- Commit: `053d5fa79a387153f96bd21928d8b94224813da8`
- Commit date: 2020-10-08
- Commit message: `Make sure we create an executable dotnet project.`
- Original project license: MIT, copyright Simon Prins (2018)
- SC2 protocol source notice: Blizzard Entertainment (2017), preserved separately

The adapter and generated protocol files have been copied into `src/` without intentional source edits. Project files are newly authored for .NET 8 and are not upstream copies.

### Imported files

| Upstream path | SingularityBot path |
|---|---|
| `SC2API-CSharp/Bot.cs` | `src/SC2API.CSharp/Bot.cs` |
| `SC2API-CSharp/CLArgs.cs` | `src/SC2API.CSharp/CLArgs.cs` |
| `SC2API-CSharp/GameConnection.cs` | `src/SC2API.CSharp/GameConnection.cs` |
| `SC2API-CSharp/ProtobufProxy.cs` | `src/SC2API.CSharp/ProtobufProxy.cs` |
| `SC2APIProtocol/*.cs` | `src/SC2APIProtocol/*.cs` (10 generated protocol files) |

License notices are retained in `licenses/ExampleBot-LICENSE` and `licenses/SC2APIProtocol-PROTOCOL_LICENSE`. The repository's top-level license is not a substitute for these third-party notices.

## Workshop evolution inspected

[FSM_Bot](https://github.com/TrentBest/FSM_Bot) contains the later Workshop implementation. Its first substantial architecture commit is `d9c12489eaf4016b642ef160363f805510cd6dba` (“Initial Build out”, 2025-12-27).

The following adapter files in FSM_Bot were byte-identical to the upstream ExampleBot files at the time of inspection (matching Git blob IDs), confirming that FSM_Bot retained the ExampleBot adapter rather than replacing it:

- `SC2API-CSharp/Bot.cs`
- `SC2API-CSharp/CLArgs.cs`
- `SC2API-CSharp/GameConnection.cs`
- `SC2API-CSharp/ProtobufProxy.cs`
- `SC2APIProtocol/Common.cs`
- `SC2APIProtocol/Data.cs`
- `SC2APIProtocol/Debug.cs`
- `SC2APIProtocol/Error.cs`
- `SC2APIProtocol/Query.cs`
- `SC2APIProtocol/Raw.cs`
- `SC2APIProtocol/Sc2Api.cs`
- `SC2APIProtocol/Score.cs`
- `SC2APIProtocol/Spatial.cs`
- `SC2APIProtocol/Ui.cs`

FSM_Bot's later strategy/runtime code is not copied wholesale. It contains known incomplete or malformed areas (including an unfinished Terran mining operations implementation), an obsolete .NET target matrix, and a stale runner project reference to `ExampleBot.csproj`. Those are migration work, not foundations to reproduce blindly.

## Current SingularityBot code

- The upstream SC2 client adapter and protocol source files are now present under `src/`.
- New SDK-style project files target .NET 8.
- A minimal no-op `SingularityBot` implementation and runner establish the protocol-facing entry point.
- The bot is intentionally not advertised as competitive or ladder-ready.
- GitHub Actions now restores and builds the runner. The first build result is pending; the outcome will be recorded after completion.

## Provenance policy

This repository does **not** claim to be a Git fork whose commit ancestry contains Simon Prins's history. The upstream code is imported with attribution and license notices, and this audit records the exact upstream snapshot. The Workshop's FSM_Bot history remains a separate lineage; its concepts and implementations will be selectively migrated with file-level provenance rather than merging unrelated histories.
