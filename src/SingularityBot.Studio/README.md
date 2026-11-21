# SingularityBot Studio

SingularityBot Studio is the native WPF authoring and inspection client for the bot's declarative model.

## Current prototype

The first vertical slice is intentionally read-only. It proves that the desktop host can consume the Workshop GUI semantic model and WPF renderer while reading real bot-owned definitions:

- **Bot Definition** loads `BotDefinitionCatalog.CreateDefault()`, runs the existing validator, and displays the opening plan, default goals, strategy transitions, and compiled advisor composition.
- **CommandAi** displays the current `BotCommandVocabulary` and explicitly identifies it as the existing proposal bridge—not the final fluent capability language.
- **HeadlessAi** displays the default consultation policy, call budgets, trigger allowlist, and host-owned provider/model configuration boundary.
- **GrammarAi** and **ProtocolAi** are marked as proposed authoring/interchange integrations. They are not represented as current runtime dependencies.

The shell uses `GuiBuilder` and `WpfGuiRenderer` for its semantic content. The native WPF window is host-owned. The UI does not execute expressions, mutate a running match, or persist credentials.

## Build

The Studio project targets `net10.0-windows` and references:

- `src/SingularityBot/SingularityBot.csproj`
- Workshop GUI Core
- Workshop GUI WPF adapter

For a local sibling-repository workspace, the GUI references resolve from `../TheSingularityWorkshop.GUI`. In CI, the GUI repository is checked out to `dependencies/GUI`.

On Windows:

```powershell
dotnet restore src/SingularityBot.Studio/SingularityBot.Studio.csproj
dotnet build src/SingularityBot.Studio/SingularityBot.Studio.csproj --configuration Release --no-restore
dotnet run --project src/SingularityBot.Studio/SingularityBot.Studio.csproj
```

The `Studio Windows Build` workflow validates restore and compilation on `windows-latest`.

## Next implementation slices

1. Add typed authoring adapters for a narrow set of bot-owned resources, retaining the existing domain validators as the authority.
2. Add import/export, dirty state, semantic diff, and explicit persistence for versioned artifacts.
3. Add CRUD for supported bot-side capability declarations and advisor settings without editing package internals.
4. Design the bounded GrammarAi expression tree only after a real capability vertical slice establishes its required types and operators.

The initial shell is an inspection proof, not a claim that CRUD or the complete fluent language is already implemented.
