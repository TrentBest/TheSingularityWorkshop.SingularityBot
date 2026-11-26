<!-- The Singularity Workshop animated moniker -->
<p align="center">
  <a href="https://github.com/TrentBest">
    <img src="https://raw.githubusercontent.com/TrentBest/TheSingularityWorkshop.Visualize/development/docs/assets/moniker.svg" alt="The Singularity Workshop — animated workshop moniker" width="720" />
  </a>
</p>

# CommandAi integration: bot-owned vocabulary v1

## Purpose

CommandAi is the provider-neutral proposal contract. SingularityBot owns the meaning of every action and parameter, validates proposals against the selected BotDefinition and current match state, and remains the only route to SC2 action submission.

The vocabulary in `BotCommandVocabulary` is the first integration step. It deliberately contains domain operations, not SC2 protocol ability IDs.

## Ownership boundary

| Layer | Responsibility |
|---|---|
| CommandAi | Bounded immutable proposal representation and wire encoding |
| SingularityBot vocabulary | Stable action/parameter identities and domain meanings |
| SingularityBot proposal adapter | Active-definition and match-profile checks, explicit host authorization input, target mapping, and in-process replay protection |
| ProductionPlanRuntime | Existing production objective, effective desired count/priority, dependency gates, and FSM lifecycle |
| ProductionPlanObservationAdapter | Reconciles objective facts from observations, game data, jobs, and resource reservations |
| BuildJobRuntime | Observed unit/structure job lifecycle and terminal evidence |
| SC2 adapter/controllers | Current-frame legality and protocol action construction |
| Observation reconciliation | Establishes progress, completion or failure |

A well-formed proposal is not authorization and is not evidence that an operation happened. The proposal adapter never submits an SC2 action.

## Vocabulary v1

Vocabulary identity: `SingularityBot.Commands/1`

| Action ID | Action | Required parameters |
|---:|---|---|
| 1001 | `SetProductionTarget` | ProductionTarget (2001, identifier), ProductionDesiredCount (2002, positive integer text) |
| 1002 | `ConstructBuilding` | BuildingTarget (2011, identifier), BuildingDesiredCount (2012, positive integer text) |
| 1003 | `AssignScout` | ScoutSector (2021, sector identifier) |
| 1004 | `AssignMilitaryOperation` | MilitaryOperation (2031, authored operation identifier) |
| 1005 | `TransitionStrategy` | StrategyTransition (2041, strategy transition identifier) |

Optional parameter:

| Parameter ID | Name | Type | Constraint |
|---:|---|---|---|
| 2003 | `ProductionPriority` | Literal text | Integer from 0 through 100 |

The action and parameter IDs are stable SingularityBot identities. Identifier values refer to bot-owned catalogs or definitions; they are not raw SC2 unit, ability, or order IDs. The current production adapter maps host identity `1` to Marine, and accepts it only when the selected BotDefinition has exactly one matching Marine/Barracks opening objective.

## Implemented proposal-to-objective boundary

SingularityBot consumes the actual `TheSingularityWorkshop.CommandAi` project from its `development` branch. The project reference is `dependencies/CommandAi/TheSingularityWorkshop.CommandAi.csproj`; GitHub Actions checks out that repository into the matching path before restore/build. Local setup instructions are in the repository README. No NuGet publication is required for active development.

`BotCommandProposalValidator` validates the real `CommandProposal` objects against the bot-owned vocabulary. It rejects unknown action IDs, undeclared parameters, missing required parameters, incorrect value kinds, invalid desired counts, and priorities outside 0–100.

`BotCommandProductionProposalAdapter.TryApply` is the next, host-side boundary. It requires:
- An explicit `authorized` decision supplied by the host; authorization is never inferred from the proposal.
- A stable host-supplied idempotency key.
- A valid active BotDefinition whose configured race matches the authoritative match race.
- A production profile that resolves target, producer, and prerequisite facts uniquely from authoritative match game data.
- Exactly one supported `SetProductionTarget` command and a known bot-owned target identity.
- Exactly one matching authored objective in the active opening plan, registered in the existing `ProductionPlanRuntime`.
- Dependency ancestry in that authored plan for every profile prerequisite.

The adapter changes the existing step context's effective desired count while retaining its immutable authored `ProductionStepDefinition`. Priority values are currently accepted only when they equal the authored priority: live action arbitration does not yet consume proposal priority, so a different value is rejected rather than silently ignored. The normal production FSM continues to own scheduling; the adapter does not append another step, create a second scheduler, create a BuildJob, or dispatch an SC2 action. Re-delivery with the same key and identical proposal is an acknowledged no-op; reuse of that key for different content is rejected. This replay ledger currently lives in the adapter instance and is not durable across process/match restarts.

## GrammarAi boundary: optional authoring structure, not runtime authority

GrammarAi is **not a current SingularityBot runtime dependency**. The implemented command path above uses the bot-owned `BotCommandVocabulary`, actual CommandAi proposal types, and bot-owned validation/adaptation. BotDefinition and its domain validators remain authoritative for the live bot.

GrammarAi is worth considering only if Forge, an LLM authoring tool, and another independent consumer need to exchange a shared, versioned knowledge graph—such as goal/subgoal relationships, prerequisite ancestry, capability roles, or bounded strategy composition—that is not already covered by BotDefinition. It must not duplicate BotDefinition's fixed schema, replace bot-domain validation, or become a second strategy engine.

If that use case is proven, the optional composition is:
- ProtocolAi owns stable qualified vocabulary/category/member identities.
- GrammarAi owns typed node kinds, contextual slots, cardinality, ordering, and structural validation. Its host-supplied external resolver must be bound to the exact request/catalog snapshot.
- An enriched CommandAi atomic command may carry an opaque GrammarAi body. CommandAi validates its framing and limits, but does not decode or validate the body.
- SingularityBot pins the exact GrammarAi definition/schema version, validates the structure, maps it into real BotDefinition/domain types, and applies its existing match-state, policy, authorization, idempotency, and SC2 execution gates.

The current SingularityBot adapter is not documented as consuming that enriched grammar-body path. Do not claim otherwise until an exact-revision consumer test exercises it.

See [GrammarAi and SingularityBot](https://github.com/TrentBest/TheSingularityWorkshop.GrammarAi/blob/development/docs/INTEGRATION_SINGULARITYBOT.md) for the complete cross-repository source audit, ProtocolAi boundary, versioning details, and acceptance tests. The related coordination points are [SingularityBot #12](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/issues/12) and [SingularityBot #15](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/issues/15).

## Remaining integration work

This is an implemented proposal-to-objective bridge, not yet the complete end-to-end production proof. The following acceptance work remains:
- Connect the active proposal adapter and production-plan runtime to the live `SingularityBot` host/frame lifecycle; the current live Marine controller still receives its existing host-owned desired-count setting.
- Carry proposal identity from accepted objective through BuildJobRuntime jobs and observed progress, completion, failure, rejection, and expiry. The current adapter does not yet create a durable proposal lifecycle/correlation record.
- Define durable replay/supersession policy across process and match restarts.
- Prove that rejected proposals produce no live runtime or SC2 side effects in an end-to-end consumer test.
- Prove that asynchronous proposal generation cannot block the frame loop.

Do not describe a proposal as completed when only an objective was accepted or an action was submitted. Terminal success must be established from authoritative observation/job evidence.

## Safety requirements

- Unknown action, parameter, or target identities fail closed.
- Structural validity does not imply semantic validity or authorization.
- Stale or absent observations cannot satisfy hard prerequisites.
- A submitted SC2 action is not a completed objective.
- Duplicate proposals must reconcile to an existing objective rather than create duplicate goals/jobs.
- CommandAi and model output never submit SC2 protocol actions directly.
- The live frame loop must remain deterministic and must not wait on an LLM.

## Verification

`BotCommandVocabularyTests` checks stable positive action/parameter identities, uniqueness, required parameter typing, and fail-closed lookup of unknown action IDs.

`BotCommandProposalValidatorTests` exercises the real CommandAi builder/types and covers valid Marine-production-shaped proposals, unknown actions/parameters, missing required values, invalid counts, and invalid priorities.

`BotCommandProductionProposalAdapterTests` covers authorized updates to the existing `marine-core` objective, same-key replay, conflicting key reuse, unauthorized rejection, race mismatch, and rejection of priority overrides that the live scheduler cannot yet honor. The adapter additionally validates the active definition, match-authoritative Marine/Barracks profile, and authored prerequisite dependency chain before mutation.
---

<!-- THE SINGULARITY WORKSHOP FOOTER -->
<p align="center"><img src="https://raw.githubusercontent.com/TrentBest/FSM_API/master/Documentation/Branding/TheSingularityWorkshop.png" alt="The Singularity Workshop" width="200" /></p>
