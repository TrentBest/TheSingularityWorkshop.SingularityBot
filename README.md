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
3. Select the closest suitable SCV for the first Supply Depot.
4. Complete the Depot and first Barracks through validated construction actions.
5. Establish four Barracks and sustain a pure-Marine production plan without gas.
6. Preserve mining throughput while builders and production consume resources.
7. Replay the same observations and reproduce the same decisions.

This is an implementation target, not a claim that the full opening is already complete. The repository currently has an FSM foundation, observation normalization/reconciliation and an initial gather/return action path on active development work. Construction, production orchestration, persistent mineral-route scheduling and live-match validation remain gates.

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
