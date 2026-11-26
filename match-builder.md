# MatchBuilder and conditional strategy adoption

## Purpose

A match is authored as a composition of reusable builders and strategic policies. The opening is a production plan with explicit completion dependencies. Midgame and late-game entries are not mandatory chronological chapters: they are candidate strategy adoptions that must earn their place from current observations, hard feasibility guards and expected advantage.

`MatchBuilder` is the domain authoring surface. It does not implement a second state-machine engine. Production steps are executed by the existing `ProductionPlanRuntime`, whose per-step contexts and handles are created and advanced through FSM_API. Strategy adoption is represented as a validated policy contract; the host strategy runtime must own the adopted strategy state through FSM_API before a policy is allowed to change live bot behavior.

## Authoring shape

The intended fluent composition is:

```csharp
var opening = new ProductionPlanBuilder("terran-opening", "Terran Opening");
opening.Buildings.Add("supply-depot", "SupplyDepot", "SCV");
opening.Buildings.Add("first-barracks", "Barracks", "SCV");
opening.Units.AddUntil("marine-wave", "Marine", "Barracks", 4);

var match = new MatchBuilder("terran-adaptive", "Terran Adaptive")
    .WithOpening(opening.Build())
    .WithMidGame(new StrategyTransitionDefinition(
        Id: "zerg-roach-response",
        TargetStrategy: "Terran Banshee Defense",
        Evidence:
        [
            new StrategyEvidenceRequirement(
                "enemy-structure-observed", "RoachWarren",
                MinimumConfidence: 1.0f,
                MaximumAgeGameLoops: 32,
                RequiresObservedFact: true)
        ],
        RequiredGuards:
        [
            "banshee-tech-path-available",
            "natural-defense-covered",
            "anti-air-risk-acceptable"
        ],
        MinimumAdvantage: 0.15,
        EvaluationHorizonGameLoops: 224,
        CooldownGameLoops: 448))
    .Build();
```

This describes an opening plus a *conditional* response. It does not mean that a Roach Warren proves a Roach attack is coming, nor does it mean that a Banshee is automatically the correct answer. Seeing the Warren go down is a timely, directly observed signal that the opponent has opened that technology option. The response still needs a safe defensive posture, a viable tech path, resources and production capacity, a timing estimate, and an acceptable anti-air risk.

## Decision contract

A transition declares:

- **Evidence requirements:** stable evidence ID, subject, minimum confidence and maximum age. A requirement may insist on a directly observed fact. Inferred composition estimates must retain their confidence and freshness.
- **Hard guards:** non-negotiable prerequisites such as technology availability, defensive coverage, resource/production feasibility, and tolerable exposure. A weighted score cannot override a failed guard.
- **Advantage threshold:** the estimated improvement over continuing the current strategy, evaluated over a declared horizon and including switching/commitment costs.
- **Cooldown:** a minimum interval after the previous strategy adoption to reduce oscillation. A production command submission is not evidence that the intended structure or unit exists.

The current `StrategyTransitionEvaluator` is a pure, deterministic policy gate. It reports eligibility or a concrete rejection reason for missing/stale/uncertain evidence, failed guards, cooldown, or insufficient advantage. It does not mutate the active strategy, issue SC2 actions, infer missing game facts, or claim that a transition was executed.

## Builder responsibilities

A builder is a reusable behavior with a stable identity, declared inputs and outputs, and an FSM_API-owned lifecycle. For example, a Banshee response is not one opaque macro: it composes prerequisite technology, Starport/reactor or tech-lab requirements, Banshee production, detection/anti-air risk assessment, and an escort/defense plan. Each work item reports observed progress and resource/actor needs. The strategic planner arbitrates those needs; no builder independently steals a worker or bypasses action validation.

The same composition principle applies to the opening. The Supply Depot and Barracks are separate jobs with an observed-completion dependency. A submitted build command or an in-progress structure does not satisfy the dependency.

## Current implementation boundary

Implemented in this slice:

- `MatchBuilder` composes one validated opening `ProductionPlanDefinition` and midgame/late-game transition descriptors.
- Transition definitions validate stable IDs, evidence requirements, hard-guard identifiers, finite advantage thresholds, and positive evaluation horizons.
- The evaluator enforces evidence freshness/confidence, directly-observed requirements, hard guards, cooldown and advantage threshold, returning explicit rejection reasons.
- The production plan continues to execute through the existing FSM_API-backed `ProductionPlanRuntime`.
- The first Barracks controller no longer reserves or stages a second SCV while the Supply Depot is incomplete; worker selection begins only after the Depot is observed complete.

Not yet implemented:

- Binding transition evidence IDs and hard guards to authoritative SC2 observation/game-data providers.
- An FSM_API-backed match strategy runtime that consumes eligible decisions and owns adoption, commitment, cancellation, recovery and fallback states.
- Full strategy compilation from external JSON and GUI editing/inspection of this same contract.
- Banshee production/escort/defense builders as an end-to-end adaptive response.

Until those integrations exist, a `MatchDefinition` is an authoring artifact and an evaluator result is an eligibility decision—not a claim that the bot has adopted or executed the target strategy.

## Acceptance criteria for the first adaptive match

1. The opening builder cannot reserve or move a Barracks SCV before the Supply Depot is observed complete.
2. A submitted or in-progress Depot does not release the opening dependency.
3. A directly observed Roach Warren can satisfy the declared trigger only while fresh.
4. A stale or low-confidence signal cannot authorize adoption.
5. Missing tech, defensive coverage or anti-air hard guards always reject adoption, regardless of advantage score.
6. The response is not adopted merely because the trigger occurred; the estimated advantage must exceed the configured threshold after its evaluation horizon and commitment cost are considered.
7. The selected strategy is then owned and advanced by FSM_API-backed runtime contexts; all concrete SC2 commands continue through the existing action validation pipeline.
8. Diagnostics explain both adoption and rejection, including the evidence and guards involved.

## Next vertical slice

Bind the evidence requirement to the normalized observation stream and SC2 game-data catalog, then add the FSM_API match-strategy context and a single observable transition lifecycle. Only after that should the Banshee builder chain be connected. The first local match should verify the opening worker timing and capture the Roach Warren observation, transition decision, and rejection/adoption reasons in diagnostics.
