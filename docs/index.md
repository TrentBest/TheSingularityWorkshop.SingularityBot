# SingularityBot

SingularityBot is a StarCraft II player runtime built on The Singularity Workshop. Its first objective is a reliable, measurable Terran opening: no gas, four Barracks and sustained Marine production.

## Current state

The `development` branch contains the SC2 observation normalizer, persistent FSM context reconciliation, profile-aware morph handling, per-observation intent buffering and initial worker gather/return action translation. Regression tests are part of the build workflow.

The full opening is not complete. Persistent mineral routes, economy scheduling, construction, production orchestration, action-result diagnostics, replay evaluation and local-match proof remain active implementation gates. Nothing here should be described as ladder-ready.

## Architecture

SC2 protocol handling stays at the adapter boundary. FSM_API owns reusable lifecycle and behavior; FSM_Foundry executes the composed FSM workload. The bot's runtime is authoritative. GUI/Renderer/Visualize are supporting authoring and inspection surfaces, not alternate execution engines.

## Explore

- [Architecture](architecture.md)
- [Behavior and FSM model](behavior-model.md)
- [Terran opening and worker logistics](opening-economy-logistics.md)
- [Spatial language: Side, Quadrant and Sector](spatial-language.md)
- [Base geometry and resource-line inference](base-geometry.md)
- [Strategy format](strategy-format.md)
- [Goal action planning](goal-action-planning.md)
- [Example strategy](../strategies/terran-opening.example.json)
- [Lineage and licensing](lineage.md)
- [Source migration audit](migration-audit.md)
- [Roadmap and acceptance gates](roadmap.md)
- [Windows development and local testing](windows-development-setup.md)
- [Local game sessions](local-game-sessions.md)
- [GitHub Pages publishing](pages.md)

## Design rule

**The bot plays first; tools support it.** Strategy and configuration remain versioned data, FSM execution remains code, and every SC2 action must pass current-observation validation.
