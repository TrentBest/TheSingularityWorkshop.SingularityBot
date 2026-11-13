# SingularityBot

SingularityBot is a StarCraft II player runtime built on The Singularity Workshop. Its first objective is a reliable, measurable Terran opening: a sustainable economy, four Barracks and sustained Marine production, followed by verified tactical control.

## Current state

The `development` branch contains observation normalization, persistent entity-context reconciliation, profile-aware morph handling, per-observation intent buffering, worker/economy coordination, construction scheduling, game-data-backed action translation and diagnostic capture. The build workflow validates documentation, restores and builds the solution, and runs controller regression tests. The current integrated head has a green Build and Sith Policy result; see [CiAi evidence observation](ciai-evidence.md).

The implementation has advanced beyond the original worker-only prototype. The opening now includes persistent mineral assignment, first-Depot/first-Barracks construction, follow-up supply and expansion gates, and Marine rally behavior. A four-Marine rendezvous regression test protects the rule that the first three Marines assemble at the producing Barracks and do not dispatch until the fourth joins.

That does **not** establish live-match success. Four-Barracks sustainability, safe enemy-ramp scouting, observed movement arrival, pathing, combat outcomes and repeatable opening completion still require local SC2 match/replay evidence. Nothing here should be described as ladder-ready.

## Architecture

SC2 protocol handling stays at the adapter boundary. FSM_API owns reusable lifecycle and behavior; FSM_Foundry executes the composed FSM workload. The bot runtime is authoritative. GUI/Renderer/Visualize are supporting authoring and inspection surfaces, not alternate execution engines. CiAi is a read-only CI evidence observer; it does not replace GitHub Actions or grant release authorization.

## Explore

- [Scriptable bot research: AI package evaluation, architecture paths, and graphical authoring](research/README.md)
- [Architecture](architecture.md)
- [Behavior and FSM model](behavior-model.md)
- [Terran FSM catalog and processing pipeline](terran-fsm-catalog.md)
- [Terran opening and worker logistics](opening-economy-logistics.md)
- [Terran economy and cohesive-force doctrine](terran-economy-force-cohesion.md)
- [Development diagnostics and trace format](diagnostics.md)
- [Acolyte LE match postmortem (2026-11-11)](match-postmortem-20261111.md)
- [CiAi evidence observation and acceptance gates](ciai-evidence.md)
- [Spatial language: Side, Quadrant and Sector](spatial-language.md)
- [Base geometry and resource-line inference](base-geometry.md)
- [Strategy format](strategy-format.md)
- [MatchBuilder and conditional strategy adoption](match-builder.md)
- [Goal action planning](goal-action-planning.md)
- [Example strategy](../strategies/terran-opening.example.json)
- [Lineage and licensing](lineage.md)
- [Source migration audit](migration-audit.md)
- [Roadmap and acceptance gates](roadmap.md)
- [Windows development and local testing](windows-development-setup.md)
- [Local game sessions](local-game-sessions.md)
- [GitHub Pages publishing](pages.md)

## Design rule

**The bot plays first; tools support it.** Strategy and configuration remain versioned data, FSM execution remains code, and every SC2 action must pass current-observation validation. A test pass is not a match result; a match result is not ladder readiness.