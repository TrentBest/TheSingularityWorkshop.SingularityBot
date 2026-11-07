# SingularityBot Forge

Forge is the beginner-facing authoring workspace for SingularityBot. It is a separate WPF desktop project and does not add a GUI dependency to the headless runtime.

## Current authoring slice

- Edit bot identity and race.
- Add and edit strategy phases, objectives and priorities.
- Add and edit groups/squads, membership selectors, shared objectives and coordination policies.
- Inspect and edit unit profiles, catalog references and a typed Marine retreat-threshold override.
- Add and edit reusable behavior references.
- Add and edit behavior rules: fact identifier, comparison operator, threshold/value and candidate intent identifier.
- Save and reopen local JSON drafts.

The sample is synthetic. Catalog identifiers, facts, intents and parameter ranges remain provisional until confirmed by the runtime owner. Local save does not validate against the runtime catalog or apply the definition to a running bot.

## Architecture boundary

Forge edits declarative intent and presents runtime-owned validation/snapshots when those contracts are available. It does not own a strategy engine, action legality, FSM execution, or SC2 protocol integration. A group owns a shared objective and coordination policy; member units retain their own profiles.

LLM-generated changes must be presented as proposals with a base hash, rationale and scoped diff. Explicit user approval is required before a proposal can be saved or applied. Runtime validation remains authoritative.

## Build

Requires the .NET 8 Windows Desktop SDK and access to the Workshop package feed configured for `TheSingularityWorkshop.GUI.WPF` 0.1.0-alpha.6.

```powershell
dotnet build src/SingularityBot.Gui/SingularityBot.Gui.csproj
dotnet run --project src/SingularityBot.Gui/SingularityBot.Gui.csproj
```

## Not connected yet

The current slice does not include authoritative catalog resolution, runtime schema/semantic validation, proposal diff/approval UI, live map capture/replay, match snapshots, runner commands, or AnyApp hosting. It does not parse SC2 protocol data or map archives. These are follow-on integrations behind versioned runtime-owned contracts.