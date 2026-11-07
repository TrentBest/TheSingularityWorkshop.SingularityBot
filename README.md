<!-- The Singularity Workshop moniker -->
<p align="center">
  <a href="https://github.com/TrentBest">
    <img src="https://raw.githubusercontent.com/TrentBest/TheSingularityWorkshop.Visualize/development/docs/assets/moniker.svg" alt="The Singularity Workshop — animated workshop moniker" width="720" />
  </a>
</p>

# ✳️ SingularityBot

[![Sith Policy](https://raw.githubusercontent.com/TrentBest/TheSingularityWorkshop.SingularityBot/master/assets/sith-policy.svg)](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/workflows/sith-policy.yml)

**An FSM-driven StarCraft II bot, built on The Singularity Workshop.**

SingularityBot observes the game, maintains persistent entity contexts, evaluates hierarchical finite-state machines, arbitrates intents, validates commands, and submits actions through the SC2 protocol adapter. The first objective is a bot that actually plays a disciplined Terran opening—not a generalized strategy editor, a neural-policy experiment, or a ladder framework.

## Current objective

Prove a deterministic, measurable Terran economy and production loop:

1. Reconcile each observation into stable player, worker, resource, structure, order, and game-loop facts.
2. Assign starting SCVs to persistent mineral routes.
3. At the 10-mineral bank crossing, pull the first SCV already returning to the Command Center and stage it for the first Supply Depot.
4. Complete the Depot and first Barracks through validated construction actions.
5. Establish four Barracks and sustain a pure-Marine production plan without gas.
6. Preserve mining throughput while builders and production consume resources.
7. Replay the same observations and reproduce the same decisions.

This is an implementation target, not a claim that the full opening is already complete. The `development` branch connects observation normalization, persistent unit-context reconciliation, intent buffering, SC2 game-data lookup, and gather/return actions for SCV/Probe/Drone workers. The local runner uses SC2 real-time mode for human-readable playback; ladder sessions retain stepped mode. The adapter returns each submitted action's `ResponseAction.Result`, correlates results with unit tags and ability IDs, and logs observation-reported action errors. The first protocol action is isolated Command Center SCV production; ability, cost and supply are read from SC2 game data, and the next observation must show the training order. The mineral planner adopts existing SC2 gather orders instead of ignoring workers that already have orders, then retains stable saturation-aware assignments. When SC2 game data and a per-SCV available-ability query both confirm Spray, the bot issues Spray and then explicitly reissues that same SCV's original mineral target; if Spray is absent or unavailable, it restores the worker assignment without inventing a command. At the 10-mineral bank crossing, the opening controller reserves an SCV on its return leg, queries SC2 for a legal forward-side Depot placement, moves the worker to the selected site, and issues the Depot build command once the game-data mineral cost is available. These production, worker-control and Depot paths have automated controller tests, but none is claimed live-match proven yet. Barracks scheduling, Marine production, replay evidence, and complete strategy-level validation remain open. Consult CI for the current test result.

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
| Build, tests, replay evaluation and automation | CiAi / HeadlessAi |

The runtime is authoritative. GUI is an authoring and inspection client; it does not contain a second bot engine. SC2 protocol types stay at the adapter boundary. FSM_API remains game-independent.

## Documentation

- [Architecture](docs/architecture.md)
- [Behavior and FSM model](docs/behavior-model.md)
- [Terran opening and worker logistics](docs/opening-economy-logistics.md)
- [Strategy format](docs/strategy-format.md)
- [Roadmap and acceptance gates](docs/roadmap.md)
- [Windows development and local testing](docs/windows-development-setup.md)
- [Local game sessions](docs/local-game-sessions.md)
- [Lineage and licensing](docs/lineage.md)
- [Source migration audit](docs/migration-audit.md)

## Development rules

- Exactly two long-lived branches: `master` and `development`.
- `development` is the sole integration branch; short-lived feature branches are not part of the workflow.
- Keep changes small, tested, and continuously integrated into `development`.
- Fail closed when observations, abilities, targets, prerequisites, or action legality are uncertain.
- Do not claim a behavior works until automated tests and a local SC2 match/replay demonstrate it.
- NuGet publication and external release remain separately authorized actions.

---

<p align="center">
  <a href="https://github.com/TrentBest/TheSingularityWorkshop" aria-label="The Singularity Workshop living avatar">
    <img src="https://raw.githubusercontent.com/TrentBest/WebPage/development/TheSingularityWorkshop/wwwroot/Images/TheSingularityWorkshopLivingAvatar.svg" alt="The Singularity Workshop living avatar" width="112" />
  </a><br />
  <strong>The Singularity Workshop</strong><br />
  <em>If a picture is worth a thousand words, what is a living picture worth?</em><br />
  <a href="https://github.com/TrentBest">GitHub</a> · <a href="https://www.patreon.com/TheSingularityWorkshop">Support the Workshop</a>
</p>