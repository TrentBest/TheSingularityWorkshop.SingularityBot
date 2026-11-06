# FSM-native behavior model

## Design rule

FSM_API is the execution heartbeat. Strategy, economic, squad and individual-unit
behavior are expressed as reusable FSM definitions bound to observation-derived
contexts. SC2 protocol types stay outside those contexts.

A behavior FSM emits a domain intent (for example, return cargo or attack a
visible threat). The SC2 adapter resolves that intent into a legal ability and
target using the current observation. It must reject stale, unavailable,
unaffordable, out-of-range or otherwise illegal actions. No FSM intent is itself
permission to submit a protocol action.

## Hierarchical composition

The bot is organized as cooperating FSM layers:

1. **Match / commander** — lifecycle, victory conditions, global priorities and
   phase changes.
2. **Economy** — worker allocation, resource balance, production capacity,
   construction scheduling and supply management.
3. **Army / squad** — composition, formation, objective selection, threat
   assessment, focus fire and retreat/regroup coordination.
4. **Unit** — per-entity behavior such as gathering, returning cargo, building,
   defending, advancing, engaging, retreating and regrouping.
5. **Action adapter** — translates selected intents into validated SC2 actions.

The parent layers own goals and constraints; child layers own local execution.
A child may report facts and outcomes upward, but cannot silently override a
hard parent constraint. Definitions are shared; contexts and live instances are
per match, squad or unit. This composition is the Workshop's hierarchy over the
FSM_API runtime, not a dependency on SC2 types inside FSM_API.

## First expertise set

| Domain | Initial profiles | Shared behavior |
|---|---|---|
| Workers | Terran SCV, Protoss Probe, Zerg Drone | Assign work, gather, return cargo, construct, defend |
| Protoss melee | Zealot | Assess, engage, advance, retreat, regroup |
| Terran ranged | Marine | Assess, maintain engagement spacing, focus fire, retreat, regroup |
| Zerg swarm melee | Zergling | Assess, close, surround/flank, engage, retreat, regroup |

The worker behavior graph is shared, but race-specific abilities and construction
semantics belong to the adapter/profile layer. Zealot, Marine and Zergling share
a combat lifecycle initially; their tactical policies and tuning remain distinct.

## Expertise is evidence-driven

The first profile values are provisional configuration, not claims of optimal
play. We will calibrate them using deterministic observation fixtures and
recorded replays. Metrics should include worker idle time, resource collection,
lost mining time, supply block duration, damage dealt/taken, survival, target
selection quality, overkill, retreat timing and objective completion.

Do not introduce a neural policy as the authority for actions at this stage.
A deterministic FSM policy is the baseline. NeuralNetworks may later propose or
rank candidate decisions; the same action validator and deterministic fallback
remain in control.

## GUI authoring contract

The editor should expose the same versioned definitions used by the runtime:

- State graph canvas with named states, transitions and guards.
- Reusable behavior patterns and nested behavior composition.
- Typed context facts, command intents and race/unit capability bindings.
- Strategy parameters with ranges, defaults and validation.
- Preview/simulation against recorded observations.
- Explanation of the active state, eligible transitions, rejected guards and
  selected intent.
- Versioned export/import so a saved strategy is reproducible in CI.

The GUI edits configuration and graphs; it does not embed a second behavior
engine. The runtime remains authoritative.

## Current implementation boundary

The first implementation registers shared worker and combat FSM definitions,
creates per-unit contexts/instances, and ticks their processing group once per
SC2 observation. The current no-op SC2 shell does not yet reconcile live
observations into those contexts or translate emitted intents into game actions.
Therefore this is a compiling FSM foundation, not yet an active-playing bot.

## Next implementation gates

1. Map SC2 raw units into stable unit tags, race/unit profiles and observation facts.
2. Attach/invalidate per-unit FSM contexts as units appear and disappear.
3. Implement intent arbitration and SC2 ability/target validation.
4. Add worker economy decisions and one safe legal action in a local match.
5. Add replay fixtures and metrics before tuning Zealot, Marine and Zergling tactics.
6. Add squad/commander hierarchy, then expose the versioned graph to GUI builders.
