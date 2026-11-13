<!-- The Singularity Workshop animated moniker -->
<p align="center">
  <a href="https://github.com/TrentBest">
    <img src="https://raw.githubusercontent.com/TrentBest/TheSingularityWorkshop.Visualize/development/docs/assets/moniker.svg" alt="The Singularity Workshop — animated workshop moniker" width="720" />
  </a>
</p>

# ✳️ SingularityBot

[![Sith Policy](https://raw.githubusercontent.com/TrentBest/TheSingularityWorkshop.SingularityBot/master/assets/sith-policy.svg)](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/workflows/sith-policy.yml)

**An FSM-driven StarCraft II bot, built on The Singularity Workshop.**

SingularityBot observes StarCraft II, maintains persistent entity contexts, evaluates hierarchical finite-state machines, arbitrates intents, validates commands against current observations and game data, and submits actions through the SC2 protocol adapter. The first product goal is a disciplined, measurable Terran player—not a generalized strategy editor, a neural-policy experiment, or a ladder framework.

## Current objective

Prove a sustainable Terran economy and production loop, then prove its tactical behavior in repeatable local matches:

1. Reconcile observations into stable player, worker, resource, structure, order, and game-loop facts.
2. Assign workers to persistent mineral routes using deterministic, saturation-aware allocation and SC2 pathing distances where available.
3. Produce SCVs while scheduling and verifying the first Depot/Barracks construction sequence.
4. Expand the opening through follow-up supply, additional Barracks, and a resource-derived friendly expansion.
5. Establish four Barracks and sustain Marine production without gas while protecting worker production and expansion reserves.
6. Keep the first Marine on early-warning duty; assemble the next four Marines at the producing Barracks before dispatching the first pressure squad.
7. Send the first Marine to the farthest safe, non-enemy sector adjacent to our starting sector to watch for cannon rushes, proxies, worker rushes and other cheese; keep it separate from the first four-Marine pressure squad.
8. Measure economy, production, movement, combat and outcomes from game-loop diagnostics; replay and compare controlled matches.

The first worker, construction and production stages have progressed beyond the original single-Depot prototype and have controller regression coverage. The current `development` head also has a regression test for the four-Marine rendezvous gate. These are implementation and automated-test facts—not proof that SC2 pathing, ramp safety, the complete four-Barracks economy, or the full opening has succeeded in a live match. The latest recorded GitHub Actions Build (#1152) and Sith Policy (#1366) runs passed for development commit [`87294079ad36d200df880d5f590b42acd3b7eb8a`](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/commit/87294079ad36d200df880d5f590b42acd3b7eb8a). The Pages artifact build also passed, but the separate GitHub Pages deploy job failed; documentation deployment is not counted as successful. Consult [the roadmap](docs/roadmap.md) and [CiAi evidence workflow](docs/ciai-evidence.md) for the exact proof boundaries.

## Architecture at a glance

| Responsibility | Owner |
|---|---|
| Protocol connection, observations, game data and action submission | SC2 adapter |
| Unit/ability/race vocabulary and capability facts | Bot game ontology |
| Persistent contexts and hierarchical execution | FSM_API |
| Shared executor / processing cadence | FSM_Foundry |
| Distances, routes and spatial queries | Physics |
| Authoring, inspection and map presentation | GUI / Renderer / Visualize |
| Match history and durable experience | DataWarehouse |
| Build, tests, replay evaluation and CI evidence | CiAi / HeadlessAi |

The runtime is authoritative. GUI is an authoring and inspection client; it does not contain a second bot engine. SC2 protocol types stay at the adapter boundary. FSM_API remains game-independent. CiAi observes and verifies CI-provider evidence; GitHub Actions remains the build/test executor. Neither a passing CI run nor an LLM assessment authorizes publication, deployment or release.

## Documentation

- [BotDefinition v1: portable bot DNA and bounded AI strategy consultations](docs/bot-definition-v1.md)
- [CommandAi integration v1: bot-owned command vocabulary and validation boundary](docs/commandai-integration-v1.md)
- [Architecture](docs/architecture.md)
- [Behavior and FSM model](docs/behavior-model.md)
- [Terran FSM catalog and processing pipeline](docs/terran-fsm-catalog.md)
- [Terran opening and worker logistics](docs/opening-economy-logistics.md)
- [Development diagnostics and trace format](docs/diagnostics.md)
- [CiAi evidence observation and acceptance boundaries](docs/ciai-evidence.md)
- [Strategy format](docs/strategy-format.md)
- [Engagement service and operational picture](docs/engagement-service.md)
- [Task Master, scouting and military force allocation](docs/task-master-and-force-allocation.md)
- [Economy Advisor and persistent mineral contexts](docs/economy-advisor.md)
- [Strategic advisors and game-phase scheduling](docs/strategic-advisors-and-game-phases.md)
- [Production Advisor and Marine Mode doctrine](docs/production-advisor-and-marine-mode.md)
- [Strategic intent and composition goals](docs/strategic-intent-and-composition-goals.md)
- [Spatial reasoning foundation](docs/spatial-reasoning.md)
- [Tactical spatial reasoning: spacing and platforms](docs/tactical-spatial-reasoning.md)
- [World model, platform doctrine and contingency planning](docs/world-model-and-maneuver-planning.md)
- [Roadmap and acceptance gates](docs/roadmap.md)
- [Windows development and local testing](docs/windows-development-setup.md)
- [Local game sessions](docs/local-game-sessions.md)
- [Lineage and licensing](docs/lineage.md)
- [Source migration audit](docs/migration-audit.md)

## Source dependencies

The bot builds against active ecosystem source branches. CI checks out CommandAi and FSM_Serialization from their `development` branches; no NuGet publication wait is required. For a local checkout, initialize the source dependencies before building:

```bash
mkdir -p dependencies
git clone --branch development https://github.com/TrentBest/TheSingularityWorkshop.CommandAi.git dependencies/CommandAi
git clone --branch development https://github.com/TrentBest/TheSingularityWorkshop.FSM_Serialization.git dependencies/FSM_Serialization
dotnet build src/SingularityBot.Runner/SingularityBot.Runner.csproj
```

Keep dependency integration source-based during active development. When an integration exposes a missing capability, identify and contribute the requirement to the owning repository instead of copying its implementation into SingularityBot.

## Development rules

- Exactly two long-lived branches: `master` and `development`.
- `development` is the sole integration branch; do not create feature branches.
- Keep changes small, tested, and continuously integrated into `development`.
- Fail closed when observations, abilities, targets, prerequisites, or action legality are uncertain.
- Distinguish source implementation, automated tests, CI evidence and live-match evidence.
- Do not claim a behavior works until automated tests and a local SC2 match/replay demonstrate it.
- NuGet publication, merge-to-master, external release and deployment require separate explicit authorization.

---

<p align="center">
  <img src="https://raw.githubusercontent.com/TrentBest/FSM_API/master/Documentation/Branding/TheSingularityWorkshop.png" alt="The Singularity Workshop" width="112" /><br />
  <strong>The Singularity Workshop</strong><br />
  <em>If a picture is worth a thousand words, what is a living picture worth?</em><br />
  <a href="https://github.com/TrentBest">GitHub</a> · <a href="https://www.patreon.com/TheSingularityWorkshop">Support the Workshop</a>
</p>