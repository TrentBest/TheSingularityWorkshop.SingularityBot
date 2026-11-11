# Development Diagnostics

Local development matches capture diagnostics by default. The intent is to preserve a causal timeline while the strategy is still changing; do not optimize away observability before we can explain both wins and losses.

## Output

Files are written beneath the local diagnostics directory configured by `Sc2Diagnostics.OutputDirectory` (currently the `extracted` directory in the developer workspace).

- `on-start-*.json` preserves the protocol inputs supplied at match start: game info, game data, ping response, and initial observation.
- `terrain-calibration-*` preserves raw terrain-height data and a separate candidate conversion for analysis.
- `opening-trace-*.csv` provides a human-readable per-frame economy/opening trace and named events.
- `sc2-events-*.bin` is the compact, append-only event timeline written through `FSM_Serialization`.
- Existing passive-baseline output remains separate.

## Binary timeline format

The file begins with an eight-byte header: ASCII `SC2T`, little-endian UInt16 format version (`1`), and two reserved zero bytes. It is followed by sequential records. Each record contains, in order, UInt64 sequence, Int64 UTC ticks, UInt32 SC2 game loop, UInt16 event type, UInt64 entity tag, Int64 value A, Int64 value B, UInt32 UTF-8 detail length, and the UTF-8 detail bytes. All numeric fields are little-endian. Detail payloads are capped at 1 MiB. Readers must reject unknown versions, truncated fields, oversized detail lengths, and invalid UTF-8.

Current event types:

| ID | Event | Meaning |
|---:|---|---|
| 1 | Frame summary | Minerals, used/cap supply, visible unit count and submitted action count |
| 2 | Build-job snapshot | Job ID, buildable kind, lifecycle status, worker/producer/result identity, progress and failure reason |
| 3 | Marine decision | Marine identity, selected target, and tactical decision |
| 4 | Submitted action | Ability, unit identity/count, target identity or position |
| 5 | Action result | Action index, ability and SC2 response result |

The binary timeline is diagnostic evidence, not a save-game or deterministic replay format. It does not claim to capture every hidden game state or guarantee that a match can be resumed. The CSV remains available for quick inspection.

## Dependency development

The build workflow checks out `TrentBest/TheSingularityWorkshop.FSM_Serialization` from its `development` branch into `dependencies/FSM_Serialization` and builds the project source directly. For a local source build, place a checkout of that repository at the same relative path. If it is absent, the project file falls back to the published 1.0.0 package so a normal standalone clone remains buildable.

No NuGet publication or ladder release is part of this integration. The serialization package and bot can continue evolving on their development branches until release approval.

## Disable capture

Local runs capture diagnostics by default. Pass `--no-diagnostics` to opt out for a specific local run. Ladder runs do not enable local file capture. Once the bot's decisions are validated and the diagnostic evidence is no longer needed, we can change the development default deliberately.
