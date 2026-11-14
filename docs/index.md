# SingularityBot

SingularityBot is a StarCraft II player runtime built on The Singularity Workshop. Its first objective is a reliable, measurable Terran opening: a sustainable economy, four Barracks and sustained Marine production, followed by verified tactical control.

## Current state

The `development` branch contains observation normalization, persistent entity-context reconciliation, profile-aware morph handling, per-observation intent buffering, worker/economy coordination, construction scheduling, game-data-backed action translation and diagnostic capture. The latest implementation commit with recorded successful Build and Sith Policy checks is `b9bbc5f460309484418b5398ed8cf65254316340` (Build #1358; Sith Policy #1583). Subsequent commits in this refactor are documentation/runner-help alignment changes; their workflow results must be checked against the exact current `development` head before calling that head CI-verified. See [CiAi evidence observation](ciai-evidence.md).

The implementation has advanced beyond the original worker-only prototype. The opening now includes persistent mineral assignment, first-Depot/first-Barracks construction, follow-up supply and expansion gates, and Marine rally behavior. A four-Marine rendezvous regression test protects the rule that the first three Marines assemble at the producing Barracks and do not dispatch until the fourth joins.

That does **not** establish live-match success. Four-Barracks sustainability, safe enemy-ramp scouting, observed movement arrival, pathing, combat outcomes and repeatable opening completion still require local SC2 match/replay evidence. Nothing here should be described as ladder-ready.

## Architecture

SC2 Runner is the live host: it owns the connection, callbacks, frame lifecycle and protocol action submission. SingularityBot.dll owns match state, decisions, intent arbitration and action translation. FSM_API supplies reusable context and hierarchical behavior mechanics. FSM_COS currently performs startup composition of the compiled definition and CommandAi vocabulary; it does not drive the match loop or execute a second strategy engine. The compiled strategy seam now supplies the validated definition, executable initial-goal composition, goal-assignment policy and Marine advance-to-enemy-start rally decision. The Runner selects statically linked `AdaptiveMarineStrategy`; two-strategy regression coverage verifies different goal and rally policies through the same engine and action vocabulary. Objective/transition composition and remaining user-specific policy are still in progress. FSM_Foundry is not part of live SC2 execution. GUI/Renderer/Visualize are supporting authoring and inspection surfaces, not alternate execution engines. CiAi is a read-only CI evidence observer; it does not replace GitHub Actions or grant release authorization.

## Explore

- [BotDefinition v1: portable bot DNA and bounded AI strategy consultations](bot-definition-v1.md)
- [Scriptable bot research: AI package evaluation, architecture paths, and graphical authoring](research/README.md)
- [Architecture](architecture.md)
- [Composable command layers and visual bot authoring](composable-command-layers-and-visual-authoring.md)
- [Military organization and extensible game phases](military-organization-and-game-phases.md)
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
- [ServerSC2 runner/observer integration contract v1](server-sc2-runner-contract-v1.md)
- [GitHub Pages publishing](pages.md)

## Design rule

**The bot plays first; tools support it.** Authoring data is validated and compiled into the bot's statically linked strategy implementation; the live runtime does not load external strategy files or dynamic assemblies. The compiled personality boundary is partially implemented: `ICompiledBotStrategy` owns initial-goal composition, goal assignment and Marine rally policy, while the catalog remains data and the goal planner/action pipeline remain shared. Do not describe the refactor as complete until objective/transition composition and remaining strategy policy move behind it. Every SC2 action must pass current-observation validation. A test pass is not a match result; a match result is not ladder readiness.
