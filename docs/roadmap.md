# Roadmap

The shortest path to a bot that actually completes a local StarCraft II match remains the priority. Do not let editor polish, generalized strategy tooling, neural policy, ladder operations or additional races displace the first playable Terran economy.

## Evidence status (2026-11-22)

The latest source checkpoint, `8151eec24db4dbc2f030df3ced9cf8f89b6f345c`, wires the reserved Terran Marine scout to the terrain-aware enemy-ramp controller and adds evidence-based target priorities for observed builders of unfinished production structures and visible Probes. Exact-head [Build #1864](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/45009704034) restored and compiled the source and passed all 527 tests (0 failed, 0 skipped). Exact-head [Sith Policy #2125](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/45009704037) also passed.

The follow-up documentation/visual checkpoint, `721a92e9fb24722c9f55fa6bc88959e9a9c810cc`, adds a code-derived opening-pressure diagram to Forge and updates the race-support evidence. Exact-head [Build #1865](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/45009970765) and [Sith Policy #2126](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/45009970757) both passed. The current Forge diagram is explanatory, not live telemetry. The documentation Pages workflow remains manual-only and has not been dispatched.

This advances the Terran Marine scout/engagement slice only. The Zerg opener remains a bounded first-Zergling-pair path, Protoss still has no executable opener, and no live SC2 match/replay has established full-opening success, authoritative match completion, or ladder readiness. The default Random request must not be confused with three-race gameplay parity.

Evidence levels must remain distinct:

1. **Implemented** — source contains the behavior.
2. **Regression-tested** — automated tests exercise the behavior and its failure boundaries.
3. **CI-verified** — the exact candidate SHA has complete, successful required workflow evidence, observable through [CiAi](ciai-evidence.md).
4. **Match-verified** — a local SC2 match/replay demonstrates observed state transitions, action results, movement and arrival, and the intended outcome.
5. **Release-authorized** — the owner explicitly authorizes promotion/publication/deployment for the exact candidate. CI or model output never supplies this authorization.

