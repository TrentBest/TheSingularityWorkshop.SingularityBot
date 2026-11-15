# Roadmap

The shortest path to a bot that actually completes a local StarCraft II match remains the priority. Do not let editor polish, generalized strategy tooling, neural policy, ladder operations or additional races displace the first playable Terran economy.

## Evidence status (2026-11-15)

The latest implementation checkpoint, `9c65f2199317e30d1fbe4ee075212b5564960714` (`fix: reset Barracks production correlation between matches`), passed both required workflows: [Build #1630](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43699565254) and [Sith Policy #1874](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43699565258). Build restored and compiled against the current `development` dependencies, then passed all 459 tests (0 failed, 0 skipped). The change explicitly clears secondary-Barracks pending producer correlation at both `OnStart` and `OnEnd`, with a regression test proving the controller can be reused in a subsequent match. The earlier CommandAi `CS0246` blocker is resolved: its current `development` source contains `CommandGrammarBody`, `CommandPrecondition`, and `CommandOutcomeField`, and CommandAi CI passes at `f956f45f2d4194469ff3cc1a90d960bfa6831b42`. The current `development` source checkpoint is therefore **CI-verified**. No live SC2 match/replay has established full-opening success, authoritative match completion, or ladder readiness.

The immediately preceding checks at `6e6be71b32e889c257094061a1281a895616b871` exposed two sequential build blockers: Build #1615 reported `NU1201` because the Runner still targeted .NET 8 while source dependencies targeted .NET 10; the subsequent development commits aligned the Runner target and corrected the duplicate strategic/match-lifecycle phase naming. Those earlier failures are historical evidence, not the current head's status. See [Build #1615](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43688964756) and [Sith Policy #1857](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/runs/43688964753).

The documentation Pages workflow remains manual-only and has not been dispatched. No live SC2 match/replay has established the full opening, match completion, or ladder readiness. The current CI result is exact-head build/policy evidence only.
Evidence levels must remain distinct:

1. **Implemented** — source contains the behavior.
2. **Regression-tested** — automated tests exercise the behavior and its failure boundaries.
3. **CI-verified** — the exact candidate SHA has complete, successful required workflow evidence, observable through [CiAi](ciai-evidence.md).
4. **Match-verified** — a local SC2 match/replay demonstrates observed state transitions, action results, movement and arrival, and the intended outcome.
5. **Release-authorized** — the owner explicitly authorizes promotion/publication/deployment for the exact candidate. CI or model output never supplies this authorization.

