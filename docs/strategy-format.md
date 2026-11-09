# Strategy and opening configuration

## Purpose

Strategy is versioned data consumed by the bot runtime. It describes the selected opening and its parameters; it is not executable code, a serialized C# object graph, or a second decision engine.

The immediate consumer is the first playable Terran proof: a no-gas, four-Barracks, pure-Marine opening. Do not broaden the schema for hypothetical races, tactics or editor features until a real runtime capability needs the field.

## Current status

The repository contains a JSON Schema and example strategy document, and CI validates those files. The complete strategy compiler/runtime binding is not yet implemented. Schema validation must not be mistaken for proof that every field is consumed by the bot.

Until the runtime consumes a field, treat it as documented configuration intent only. Do not silently imply that an example build order is executable or optimal.

## Document rules

- JSON, UTF-8, with an explicit schema version.
- Stable textual identifiers for review and authoring; runtime code may map them to compact identifiers.
- Game facts such as costs, prerequisites, abilities and build times come from current SC2 game data/ontology, not guessed values embedded in a strategy.
- Time values must identify their units; authoritative execution time is measured in SC2 game loops.
- Unknown or unsupported directives fail validation with a useful path and reason.
- Strategy data never contains raw protocol requests, arbitrary executable code or permission to bypass action validation.

## First configuration boundary

The first opening configuration should express only what the working runtime can validate and execute:

- race and opening identity;
- ordered goals and hard constraints;
- SCV production and mineral assignment intent;
- Supply Depot and Barracks milestones;
- four-Barracks production target;
- pure-Marine production intent and no-gas constraint;
- resource/supply thresholds only when their runtime semantics are defined.

Do not encode assumed optimal timings as facts. The scheduler must respond to current resources, supply, prerequisites, production queues and observations.

## Compilation and validation direction

When the compiler is implemented, it should:

1. Parse and validate the versioned document.
2. Resolve referenced game data against the active SC2 data catalog.
3. Reject unsupported directives and impossible prerequisites.
4. Compile accepted goals/constraints into FSM definitions and runtime configuration.
5. Bind the definitions to the same observation, economy and action-validation services used by the headless runtime.
6. Produce diagnostics explaining accepted, rejected and unresolved configuration.

The compiler does not submit protocol actions. FSM behavior emits intents; the action pipeline remains the only route to SC2 commands.

## Future expansion

Add generalized race, scouting, composition or tactical fields only when they have a concrete implementation, tests and observable runtime effect. GUI should edit and inspect this same contract; it must not invent a parallel schema or execution model.


## Strategic transitions

A strategy is not a one-way build order. It is a policy that can move between named strategic modes when observed match conditions make another trajectory more valuable. **Transition** is a first-class strategy operation alongside initial strategy selection.

A strategy descriptor should identify:

- its declared playstyle and strategic objective;
- the conditions under which it is a good fit (race, matchup, map traits, map knowledge and current advantages);
- its goals, constraints, capabilities and production preferences;
- the transitions it may enter, including the evidence and guards required;
- the conditions under which it should remain committed, reconsider, or fall back.

A transition is a deliberate change from one strategy or playstyle to another. It is not merely a production-queue edit. It may change technology direction, unit composition, economy, map-control posture, risk tolerance and tactical priorities together. It must preserve global constraints and pass through the same validated action pipeline as every other decision.

### Example: defend the natural, then switch to Banshees

Suppose enemy scouting indicates a Zergling-heavy commitment. The bot estimates whether its current defensive force can hold the natural expansion with adequate safety margin. If the defense is sufficient, the strategy editor may authorize a transition away from the conventional follow-up and toward Banshee technology.

The transition is eligible only when the bot has enough evidence and the required prerequisites can be met: technology access, resources, production capacity, timing, and a viable plan to protect the transition. The decision must also account for enemy anti-air options and uncertainty. Zerglings cannot attack air units, but that does not make Banshees universally invulnerable: Queens, Spore Crawlers, Hydralisks and other anti-air threats can change the calculation.

This is an opportunity-driven branch:
1. Observe and classify the opponent's likely composition; retain confidence and evidence, not just a guessed label.
2. Estimate whether the current force can defend the natural over a defined horizon, including reinforcement arrival and uncertainty.
3. Compare the expected value of continuing the default pathway with the proposed Banshee transition.
4. Check affordability, prerequisites, production capacity, timing and exposure while teching.
5. Commit only when the transition's guards pass; then update strategic goals and production priorities coherently.
6. Reassess as new observations arrive. If the premise fails (for example, significant anti-air appears or the natural becomes unsafe), select an explicitly permitted response rather than blindly completing the original plan.

### Transition contract

A versioned transition definition should eventually express:

- stable transition ID and target strategy/playstyle;
- observable trigger predicates and their confidence/evidence requirements;
- hard guards (must be true) versus weighted evaluation factors (contribute to desirability);
- expected evaluation horizon and estimated commitment cost;
- minimum advantage over the current strategy before switching;
- cooldown or hysteresis to prevent oscillation;
- cancellation, recovery or fallback behavior;
- diagnostics explaining why the transition was selected, rejected or reconsidered.

Do not encode an unverified enemy composition as certainty, or a predicted outcome as a fact. Hard safety and legality constraints are never traded against a weighted score. If evidence is stale, prerequisites are unknown, or the transition cannot be executed safely, remain with the current valid strategy.

### Selection and transition are separate decisions

Initial selection asks: "Given the map, matchup, race, map knowledge and starting conditions, which strategy/playstyle should we begin with?"

Transition asks: "Given what has happened since then, is another strategy now sufficiently better to justify the cost and risk of changing course?"

Both decisions use the same strategy descriptors and evaluation vocabulary, but transition additionally accounts for sunk investment, time-to-impact, switching costs and the current tactical state. A strategy may expose multiple permitted transitions; the runtime chooses among eligible options, not the editor by itself. The editor authors the strategic possibilities and their guards; the runtime evaluates live evidence and makes the match-time decision.

These are design requirements until implemented and tested. The current Terran opening remains the first runtime proof; this model must not be mistaken for an implemented adaptive strategy engine.
