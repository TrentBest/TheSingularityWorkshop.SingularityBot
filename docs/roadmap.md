# Roadmap

The shortest path to a bot that actually completes a local StarCraft II match remains the priority. Do not let editor polish, generalized strategy tooling, neural policy, ladder operations or additional races displace the first playable Terran economy.

## Evidence status (2026-11-15)

The previous documentation checkpoint, `6d15cbda5cd1abc13debf0851bd0a47b9d33ba72`, passed both required workflows: [Build #1626](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43695462969) and [Sith Policy #1869](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43695462970). The current `development` head is `42a2c5e7fc24f886b220865c0a175e5ef4dfff8e` (`fix: reset query state across match lifecycle`). At this exact SHA, [Sith Policy #1870](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43696098152) succeeds, but [Build #1627](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43696098151) fails while compiling the vendored CommandAi dependency: 11 `CS0246` errors report unresolved `CommandGrammarBody`, `CommandPrecondition`, and `CommandOutcomeField` types in `CommandInstruction.cs`, `CommandInstructionBuilder.cs`, and `CommandOutcomeContract.cs`. The current head is therefore **not CI-verified**. The preceding green checks apply only to `6d15cbd`; the older `96bd22e` checkpoint is historical evidence, not current status.

The immediately preceding checks at `6e6be71b32e889c257094061a1281a895616b871` exposed two sequential build blockers: Build #1615 reported `NU1201` because the Runner still targeted .NET 8 while source dependencies targeted .NET 10; the subsequent development commits aligned the Runner target and corrected the duplicate strategic/match-lifecycle phase naming. Those earlier failures are historical evidence, not the current head's status. See [Build #1615](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43688964756) and [Sith Policy #1857](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43688964753).

The documentation Pages workflow remains manual-only and has not been dispatched. No live SC2 match/replay has established the full opening, match completion, or ladder readiness. The current CI result is exact-head build/policy evidence only.
Evidence levels must remain distinct:

1. **Implemented** — source contains the behavior.
2. **Regression-tested** — automated tests exercise the behavior and its failure boundaries.
3. **CI-verified** — the exact candidate SHA has complete, successful required workflow evidence, observable through [CiAi](ciai-evidence.md).
4. **Match-verified** — a local SC2 match/replay demonstrates observed state transitions, action results, movement and arrival, and the intended outcome.
5. **Release-authorized** — the owner explicitly authorizes promotion/publication/deployment for the exact candidate. CI or model output never supplies this authorization.

