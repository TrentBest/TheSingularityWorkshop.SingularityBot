# Roadmap

The shortest path to a bot that actually completes a local StarCraft II match remains the priority. Do not let editor polish, generalized strategy tooling, neural policy, ladder operations or additional races displace the first playable Terran economy.

## Evidence status (2026-11-15)

The historical 2026-11-11 baseline below remains useful as a dated test record, but it is not evidence for the current branch head. The latest reviewed source checkpoint, `1fd0aed45c6272eea5d4b7b561b879a7fd441276`, had successful Sith Policy #1847 but failed Build #1606 with compiler error `CS0101`: `SingularityBot.Behavior` already contains a definition for `GamePhase`. The same blocker is confirmed at the current documentation head, `db967e2e3f7eef611c25a2cee0f6fa3b1c42bfea`: Build #1614 failed with the identical duplicate-type error in `src/SingularityBot/Behavior/GamePhase.cs(4,13)`, while Sith Policy #1856 succeeded. The build's strategy/production/military-plan document validation and restore steps passed before compilation failed. See [Build #1614](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43687937554), [Sith Policy #1856](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43687937390), [earlier Build #1606](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43686312041) and [Sith Policy #1847](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43686312056).

The duplicate `GamePhase` declaration remains a source-level blocker; documentation-only commits have not corrected it. Do not call the current branch CI-verified or build-green. The documentation Pages workflow is manual-only and has not been dispatched. No live SC2 match/replay has been established by these CI runs.

Evidence levels must remain distinct:

1. **Implemented** — source contains the behavior.
2. **Regression-tested** — automated tests exercise the behavior and its failure boundaries.
3. **CI-verified** — the exact candidate SHA has complete, successful required workflow evidence, observable through [CiAi](ciai-evidence.md).
4. **Match-verified** — a local SC2 match/replay demonstrates observed state transitions, action results, movement and arrival, and the intended outcome.
5. **Release-authorized** — the owner explicitly authorizes promotion/publication/deployment for the exact candidate. CI or model output never supplies this authorization.

