# Match data and diagnostics

This directory is the repository-owned destination for local StarCraft II match evidence.

## Default capture destination

The runner writes diagnostic output to:

`data/diagnostics/`

The path is resolved from the SingularityBot repository root, not from the IDE's working directory or the executable's `bin/` directory. The startup session log prints the resolved absolute path. If the repository root cannot be identified, capture fails visibly instead of silently writing beside the executable.

Hosts may override the destination with either the command-line argument `--diagnostics-dir=<path>` or the environment variable `SINGULARITYBOT_DIAGNOSTICS_DIR`. The command-line argument takes precedence. Relative diagnostic paths are resolved from the repository root.

To collect files emitted by the game/runner, supply `--match-output-dir=<source-folder>`. After the SC2 connection returns (after the bot's `OnEnd` callback), the runner copies files in that source folder that were modified during the current run into a timestamped `data/diagnostics/match-output/<run-id>/` folder. It writes a `manifest.json` with relative source/destination paths, byte counts, and SHA-256 hashes. The source folder is never modified. The source folder must be supplied explicitly; the runner does not guess the platform-specific StarCraft II replay directory.

Example local launch:

```text
SingularityBot.exe --diagnostics-dir="D:\\SC2-Experiments\\BotRuns" --match-output-dir="D:\\Documents\\StarCraft II\\Accounts\\<account>\\Replays\\Multiplayer"
```

The handoff is best-effort: it retries transient file locks, but if SC2 writes a replay after the connection has already returned, that late file may not be included. The log and manifest make collection results visible. These options are local-run options and are rejected for ladder launches.

Environment variable path rules are:
- An absolute value is used as-is.
- A relative value is resolved from the repository root.

## Expected artifacts

Depending on the selected match mode, a run can produce:

- `singularitybot-session-*.log` — mirrored runner output and errors.
- `on-start-*.json` — complete initial SC2 protocol inputs.
- `terrain-calibration-*.json/.csv` — raw terrain image and coordinate-indexed samples.
- `opening-trace-*.csv` and `opening-trace-latest.csv` — Terran opening decisions and per-frame economy/build milestones.
- `worker-route-*.csv` and `worker-route-latest.csv` — worker assignments, orders, cargo, route phases and pathing distances.
- `sc2-events-*.bin` — versioned event timeline with economy, unit snapshots, decisions, actions and action results.
- `active-run-*.csv`, `active-run-workers-*.csv`, and `active-run-resources-*.csv` — match-end economy and worker summaries.
- Passive-baseline equivalents when passive capture is selected.

Not every artifact is expected for every race or run mode. Terran opening and worker-route CSVs are only started for Terran matches. Passive-baseline runs intentionally bypass active opening controllers.

## Sharing a local run

Local captures are written to disk; they are not automatically pushed to GitHub. After a match, verify that the session log reports the repository's `data/diagnostics` path and that the expected CSVs contain data rows. Then commit and push the relevant run files on `development` if they should be available for repository review and replay analysis. Do not commit credentials, bearer tokens, or unrelated personal files.

Keep the timestamped run artifacts as experiment evidence. The `*-latest.csv` files are convenience snapshots for the most recent run and are overwritten on the next corresponding capture.
