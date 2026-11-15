<!-- The Singularity Workshop animated moniker -->
<p align="center">
  <a href="https://github.com/TrentBest">
    <img src="https://raw.githubusercontent.com/TrentBest/TheSingularityWorkshop.Visualize/development/docs/assets/moniker.svg" alt="The Singularity Workshop — animated workshop moniker" width="720" />
  </a>
</p>

# SingularityBot

**A living, evidence-driven StarCraft II bot built on The Singularity Workshop.**

SingularityBot is a single decision system: observations become a normalized world picture, persistent contexts preserve entity behavior, strategic and tactical controllers propose intent, arbitration resolves competing intent, and a validated action pipeline submits commands to StarCraft II.

The first competitive target is a disciplined Terran opening. The architecture is being shaped so that additional civilizations, strategies, and authoring tools can grow around the same runtime without creating competing engines.

> **Engineering rule:** describe what the source does, prove what tests exercise, identify what CI verifies, and reserve claims of successful play for observed matches and replays.

## Project state

| Area | Current understanding |
|---|---|
| Runtime foundation | Observation normalization, persistent entity contexts, intent buffering, action validation and SC2 protocol integration are implemented. |
| Terran opening | Worker allocation, construction scheduling, production gates and Marine rendezvous behavior have regression coverage. Full opening success is not yet established by a live match. |
| Strategy boundary | A statically linked compiled-strategy seam is partially implemented. Objective/transition composition and remaining policy extraction are still open. |
| Civilization support | The runner requests Random, but the executable strategy remains Terran-specific. Random selection is not multi-race support. |
| ServerSC2 | Runner primitives and a host evidence contract exist. Three-client live topology, authoritative completion, replay validation and production cleanup still need live proof. |
| Release readiness | Not ladder-ready. No release or deployment is implied by a green build. |

The [roadmap](roadmap.md) is the authoritative checklist. The [CiAi evidence guide](ciai-evidence.md) defines the evidence levels and acceptance boundaries. Both must be read before interpreting a status claim.

## Start here

- **Understand the system:** [Architecture](architecture.md) · [Behavior and FSM model](behavior-model.md) · [Terran FSM catalog](terran-fsm-catalog.md)
- **Run and inspect it:** [Windows development setup](windows-development-setup.md) · [Local game sessions](local-game-sessions.md) · [Diagnostics and trace format](diagnostics.md)
- **Understand the bot's decisions:** [Opening economy and logistics](opening-economy-logistics.md) · [Military organization and game phases](military-organization-and-game-phases.md) · [Engagement allocation](engagement-allocation.md) · [Movement doctrine](movement-doctrine.md)
- **Extend the system:** [BotDefinition v1](bot-definition-v1.md) · [Compiled strategy and visual authoring](composable-command-layers-and-visual-authoring.md) · [CommandAi integration](commandai-integration-v1.md)
- **Verify the boundaries:** [CiAi evidence](ciai-evidence.md) · [ServerSC2 runner contract](server-sc2-runner-contract-v1.md) · [Roadmap and acceptance gates](roadmap.md)
- **Understand provenance:** [Lineage and licensing](lineage.md) · [Source migration audit](migration-audit.md)

## Architecture at a glance

| Layer | Responsibility |
|---|---|
| SC2 Runner / protocol adapter | Owns the SC2 connection, callbacks, frame lifecycle and protocol action submission. |
| SingularityBot runtime | Owns match state, decisions, intent arbitration and action translation. |
| FSM_API | Supplies reusable finite-state-machine and persistent-context mechanics. |
| FSM_COS | Composes startup dependencies and compiled runtime configuration; it is not a second live match loop. |
| CommandAi | Supplies the bot-owned command vocabulary and validation boundary. |
| ServerSC2 | Owns hosted process/session orchestration and host-authoritative match evidence. |
| CiAi | Observes CI evidence; it does not replace GitHub Actions or authorize releases. |

The live match loop remains singular. FSM_Foundry is not inserted into unit-action execution. Renderer, Visualization and GUI are supporting authoring/inspection surfaces, not alternate strategy engines.

## Development and evidence policy

- Work on `development`; keep exactly `master` and `development` as the long-lived branches.
- Keep changes small, source-backed and regression-tested.
- Fail closed when observations, abilities, targets, prerequisites or action legality are uncertain.
- Never treat a unit-test pass, successful build, runner log or diagnostic marker as a match result.
- Require exact-head required-workflow evidence before calling a commit CI-verified.
- Require local match/replay evidence before calling behavior match-verified.
- Keep publication, merge-to-master, release and deployment behind explicit owner authorization.

## Documentation map

The [documentation index](index.md) is also the site home. The [research archive](research/README.md) records evaluated directions and should be read as research, not as a description of shipped runtime behavior. The [Acolyte LE postmortem](match-postmortem-20261111.md) is a dated experiment record; newer evidence belongs in a new dated record rather than silently rewriting history.

---

<p align="center">
  <img src="https://raw.githubusercontent.com/TrentBest/FSM_API/master/Documentation/Branding/TheSingularityWorkshop.png" alt="The Singularity Workshop" width="112" /><br />
  <strong>The Singularity Workshop</strong><br />
  <em>If a picture is worth a thousand words, what is a living picture worth?</em><br />
  <a href="https://github.com/TrentBest">GitHub</a> · <a href="https://www.patreon.com/TheSingularityWorkshop">Support the Workshop</a>
</p>
