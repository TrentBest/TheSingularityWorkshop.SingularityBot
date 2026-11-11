# Windows Development Setup and Local Terran Testing

**Target:** local, private StarCraft II bot-vs-computer development on Windows  
**Current status:** the local Terran bot has a four-Barracks Marine-production path, observation-driven construction/production job tracking, and a Marine combat planner with focus fire, cooldown repositioning and withdrawal. Automated tests pass; live-match performance against the intended opponent is not yet established. A successful launch is not evidence of a win.

## 1. Install the development prerequisites

1. Finish installing StarCraft II through the official Battle.net client.
2. Install the **.NET 8 SDK** (not only the runtime). The runner targets `.NET 8`.
3. Install Git for Windows.
4. Visual Studio Community 2022 with the **.NET desktop development** workload.

Open a new PowerShell window after installing the SDK and verify:

```powershell
dotnet --info
git --version
```

The SDK list should include an `8.0.x` SDK.

## 2. Initialize the StarCraft II local API files

1. Let Battle.net finish the game installation and any updates.
2. Launch StarCraft II normally at least once, allow it to reach the main menu, then exit the game.
3. Confirm this file exists:

```text
%USERPROFILE%\Documents\StarCraft II\ExecuteInfo.txt
```

The current local launcher reads the executable path from this file. It does not assume a fixed install directory.

The local launcher starts the executable with `-listen 127.0.0.1` and a local API port, then connects over the local WebSocket. It creates a local game with the bot as one participant and the built-in computer as the opponent. This is not ladder mode.

## 3. Clone and select the active branch

In PowerShell:

```powershell
cd $HOME\source
git clone https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot.git
cd TheSingularityWorkshop.SingularityBot
git switch development
git pull
```

If you already cloned the repository, use the existing working copy and run only:

```powershell
git switch development
git pull
```

The repository's intended branch set is exactly `master` and `development`. Do not create a feature branch for this work unless we explicitly decide to change that policy.

## 4. Open the solution in Visual Studio Community

1. Open the repository folder after cloning and switching to `development`.
2. Double-click `TheSingularityWorkshop.SingularityBot.sln` in the repository root, or use **File → Open → Project/Solution** in Visual Studio.
3. In Solution Explorer, right-click **SingularityBot.Runner** and choose **Set as Startup Project**. The bot project is a class library; the Runner is the executable entry point, just as `DotnetCoreRunner` is the executable entry point in Simon Prins' ExampleBot solution.
4. Select **Debug** or **Release** and build the solution. The solution contains the protocol, API adapter, bot and runner projects.

## 5. Build without launching the game

From the repository root:

```powershell
dotnet restore .\src\SingularityBot.Runner\SingularityBot.Runner.csproj
dotnet build .\src\SingularityBot.Runner\SingularityBot.Runner.csproj --configuration Release
```

This builds the .NET 8 runner, bot, protocol adapter and referenced projects. It does not start StarCraft II.

## 6. Check the configured local map

The current local program is configured for:

```text
AcolyteLE.SC2Map
```

The adapter checks the StarCraft II installation's `Maps` directory first, then searches its subfolders for that exact filename. You can keep folders such as `Maps\\Ladder2017Season3\\` intact; do not delete them just to make the map lookup work. If the same filename exists in more than one subfolder, set `MapName` to a relative path (for example, `Ladder2017Season3\\AcolyteLE.SC2Map`) to select the intended copy. The local launcher reports a clear error if no matching map is found.

## 7. Launch a local session

From the repository root:

```powershell
dotnet run --project .\src\SingularityBot.Runner\SingularityBot.Runner.csproj --configuration Release
```

With no arguments, the runner selects the local single-player path. Do not pass ladder arguments. Close the game normally when the match ends; if startup fails, preserve the full console exception and the contents of `ExecuteInfo.txt` with personal paths redacted.

## 8. What to verify in the next Terran match

The first test is not about winning. We will record whether the bot:

1. Joins the intended local match as Terran.
2. Observes its Command Center, SCVs, mineral fields and resource/supply state.
3. Keeps each SCV's FSM context tied to its stable unit tag and retires it when the unit disappears.
4. Assigns workers without issuing duplicate or stale commands.
5. Completes the Supply Depot and Barracks construction chain, then expands toward four Barracks.
6. Produces Marines from secondary Barracks without duplicate or rejected training commands.
7. Uses Marine combat decisions to focus fire, reposition during cooldown, and withdraw away from threats when appropriate.
8. Records whether the army survives, reinforces, and converts production into effective engagements.

Run the first match as a systems and behavior smoke test. Compare subsequent matches with the same map, opponent, game speed and initial conditions; a passing unit suite does not establish combat readiness.

## Troubleshooting

| Symptom | First check |
|---|---|
| `dotnet` is not recognized | Install the .NET 8 SDK and open a new PowerShell window |
| `ExecuteInfo.txt` is missing | Launch StarCraft II once through Battle.net, reach the main menu, then exit |
| Game executable cannot start | Check that the game installation/update completed and inspect the executable path recorded in `ExecuteInfo.txt` |
| Map not found | Confirm the configured map exists under the installation's `Maps` directory; report the available map names |
| API connection times out | Ensure no stale SC2 process is holding the configured local port; retry after closing the game |
| Build fails | Capture the complete first error and the output of `dotnet --info` |

## FSM_API integration rule

The bot references `TheSingularityWorkshop.FSM_API` version `1.0.13`. The integration must use its public contracts:

- Shared behavior definitions are built once with `FSM_API.Create.CreateFiniteStateMachine(...)` and finalized with `BuildDefinition()`.
- Each live entity supplies its own POCO implementing `IStateContext`; the SC2 protocol types stay outside those contexts.
- Live instances are created with `FSM_API.Create.CreateInstance(...)` and retained as `FSMHandle` references for lifecycle/state inspection.
- The game observation loop performs one explicit `FSM_API.Interaction.Update(processingGroup)` pass per observation.
- When an entity disappears, its context is invalidated and its handle is explicitly retired with `FSM_API.Interaction.DestroyInstance(handle)`. The API's invalid-context behavior remains a safety net, not a substitute for clear ownership.

Do not create a second custom FSM scheduler beside FSM_API. Keep strategy decisions in FSM definitions and domain contexts; keep SC2 observation normalization and action serialization in the adapter.
