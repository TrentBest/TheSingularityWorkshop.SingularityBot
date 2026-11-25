# SingularityBot Studio

SingularityBot Studio is the native WPF authoring and inspection client for the bot's declarative model. Its semantic shell is built with Workshop GUI Core and rendered by Workshop GUI WPF.

## Working Bot Definition editor

The Bot Definition tab is now an editable versioned JSON authoring surface:

- **Open** loads a saved definition and parses it before replacing the current editor contents.
- **Validate** uses the same `BotDefinition.Parse` parser and semantic validator used by the Runner.
- **Build Production Plan…** opens a guided table editor for the opening plan: add/remove steps, edit target and producer, desired count, earliest game loop, priority, dependency IDs, and the Buildings/Units lane. The plan is validated before it is applied to the open BotDefinition.
- **Edit Goals…** exposes the current strategic goal policy as typed controls: default role, scout count, fallback-health threshold, enemy-start exclusion, stutter-step, and visible-threat engagement. Explicit scout-sector selections are preserved.
- **Ctrl+S** saves the current definition; **F5** validates, saves, and launches a fresh local training session. Opening another file or closing Studio prompts before discarding unsaved edits.
- The production-plan race is displayed as a parent-definition constraint; the builder cannot silently change the bot's race independently of its mode and runtime manifest.
- **Save / Save As** writes the validated, normalized JSON artifact to disk.
- **Launch Local Match** saves and validates the current definition, then starts the existing Runner with the selected map.
- If a Studio-launched Runner is still active, launching again asks permission to stop that session and start a clean match. This is the map-change restart boundary; it does not mutate the live match.
- The document includes race and mode selection, opening production steps, default goals, advisor composition, strategy transitions, and bounded AI consultation policy. The guided plan builder is the first structured authoring workflow; the complete JSON source remains available for the rest of the schema.
- Invalid documents are not saved or passed to the Runner.

The editor intentionally saves an artifact rather than mutating a live match. Launch a fresh Runner process with the saved artifact to execute it:

```powershell
dotnet run --project src/SingularityBot.Runner/SingularityBot.Runner.csproj -- --definition="C:\\Bots\\my-bot.json"
```

An optional map override starts that new local session on the selected map:

```powershell
dotnet run --project src/SingularityBot.Runner/SingularityBot.Runner.csproj -- --definition="C:\\Bots\\my-bot.json" --map="AcolyteLE.SC2Map"
```

Changing maps is a new Runner process/match, not an in-place mutation of a running game. Studio starts the Runner through the local .NET CLI and finds the Runner project in the source checkout. The SC2 runtime's actual headless/invisible support remains a separate verification item; this first launch button does not claim to hide the SC2 window.

## Other workspaces

- **CommandAi** displays the current `BotCommandVocabulary` and identifies it as the low-level proposal bridge, not the final fluent capability language.
- **HeadlessAi** displays the default consultation policy, call budgets, trigger allowlist, and host-owned provider/model configuration boundary.
- **GrammarAi** and **ProtocolAi** are explicitly marked as proposed authoring/interchange integrations, not current runtime dependencies.

The UI does not execute expressions, mutate a running match, or persist credentials.

## Build

The Studio project targets `net10.0-windows` and references:

- `src/SingularityBot/SingularityBot.csproj`
- Workshop GUI Core
- Workshop GUI WPF adapter

For a local sibling-repository workspace, GUI references resolve from `../TheSingularityWorkshop.GUI`. In CI, the GUI repository is checked out to `dependencies/GUI`.

On Windows:

```powershell
dotnet restore src/SingularityBot.Studio/SingularityBot.Studio.csproj
dotnet build src/SingularityBot.Studio/SingularityBot.Studio.csproj --configuration Release --no-restore
dotnet run --project src/SingularityBot.Studio/SingularityBot.Studio.csproj
```

Pass a definition to open it on startup:

```powershell
dotnet run --project src/SingularityBot.Studio/SingularityBot.Studio.csproj -- --definition="C:\\Bots\\my-bot.json"
```

The `Studio Windows Build` workflow validates restore and compilation on `windows-latest`.
