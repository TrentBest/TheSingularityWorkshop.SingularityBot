# SingularityBot Forge

Forge is the authoring and inspection experience for SingularityBot. It is intentionally separate from the headless bot runtime.

## First slice
- WPF desktop host using the Workshop GUI.WPF package family.
- Beginner-first hierarchy: bot, strategy phases, groups, unit profiles, reusable behaviors and rules.
- Clearly labeled synthetic sample content while authoritative map and runtime contracts are being implemented.
- Runtime connection, map presentation and live diagnostics remain behind versioned contracts; this project does not parse SC2 protocol data or execute strategy.

## Ownership boundary
Forge edits declarative intent and presents authoritative validation/snapshots. It does not own a strategy engine, action legality, FSM execution, or SC2 protocol integration. A group owns a shared objective and coordination policy; member units retain their own profiles.

LLM-produced changes must be presented as proposals with a scoped diff and require explicit user approval before saving or applying.

## Build
Requires the .NET 8 Windows Desktop SDK and access to the Workshop package feed configured for TheSingularityWorkshop.GUI.WPF 0.1.0-alpha.6.

```powershell
dotnet build src/SingularityBot.Gui/SingularityBot.Gui.csproj
dotnet run --project src/SingularityBot.Gui/SingularityBot.Gui.csproj
```

This is an initial navigable shell, not a claim that live map, runner controls, runtime snapshots, or authoritative validation are connected yet.