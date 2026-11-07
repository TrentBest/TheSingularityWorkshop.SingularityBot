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
3. **Resource logistics** — worker dispatch, node service-window estimates,
   travel/queue overlap, reservations and measured cycle efficiency.
4. **Army / squad** — composition, formation, objective selection, threat
   assessment, focus fire and retreat/regroup coordination.
5. **Unit** — per-entity behavior such as gathering, returning cargo, building,
   defending, advancing, engaging, retreating and regrouping.
6. **Action adapter** — translates selected intents into validated SC2 actions.

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

## Opening economy is the first proof

Before tactical optimization, the bot must complete the opening economy:
- assign and schedule workers across mineral nodes;
- produce race-appropriate supply;
- construct the first production structure;
- train the first tier-one unit (Marine, Zealot or Zergling).

The logistics scheduler optimizes complete worker cycles, not just gather-command
speed. It estimates travel, node service availability, queue/wait, gathering,
return and unload. Dispatch should consider predicted availability at *arrival*:
a farther resource node can be productive when its service window opens as the
worker arrives, hiding travel behind another worker's resource interaction.
Availability is an estimate, not an assumed exclusive mineral-patch lock.

The economy controller owns global worker assignments and reservations. Each worker's mining-route FSM retains its route context (worker/node/town-hall identities, cached outbound/return geometry, phase-entry game loop and predicted remaining phase time). It updates that geometry only when relevant positions or route basis change; elapsed phase time is a cheap game-loop delta, reconciled against observed orders, cargo, positions and resource changes. The resource-line controller coordinates service windows and saturation across route FSMs. Unit FSMs execute assignments and report facts; they must not make conflicting independent node selections.

Mining is not interrupted for routine optimization. A higher-priority safety supervisor compares estimated enemy attack deadlines with the worker's remaining safe harvest/return time and may issue an evasion intent only when the threat warrants it. A returning worker's next town-hall/mineral assignment can be replanned without casually discarding its current return leg. For the first Depot, the builder is the closest eligible SCV to the placement from the non-mining pool; active miners are not pulled for ordinary construction.

Opening command order is explicit: first issue the Command Center SCV train command; on the next scheduling pass assign the existing SCVs to mineral routes; then select the first Depot builder from the closest eligible non-mining SCV. All actions remain subject to current-frame legality validation. Race-specific supply and construction
opportunity costs remain in ontology/capability data (including the distinct
Drone, SCV and Probe construction semantics).

See [Opening economy and resource logistics](opening-economy-logistics.md) for
the experiment protocol, scheduling model, metrics and implementation gates.

## Expertise is evidence-driven

The first profile values are provisional configuration, not claims of optimal
play. Calibrate using deterministic observation fixtures and recorded replays.
For the opening, measure time-to-first-unit alongside mineral/gas income,
worker idle/travel/queue/gather/return/construction time, reassignment waste,
resource banking/starvation, supply-block duration, structure timing and
invalid actions. A faster first unit is not an improvement if the economy stalls
afterward.

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
- Worker-to-node assignments, predicted service windows and measured cycle
  phases over a map/timeline.
- Preview/simulation against recorded observations.
- Explanation of the active state, eligible transitions, rejected guards and
  selected intent.
- Versioned export/import so a saved strategy is reproducible in CI.

The GUI edits configuration and graphs; it does not embed a second behavior
engine. The runtime remains authoritative.

## Current implementation boundary

The runtime now normalizes SC2 raw observations into protocol-independent unit
facts using the game-data unit catalog, orders facts deterministically by stable
unit tag, and reconciles visible own units into persistent per-unit FSM contexts.
Snapshot/hidden observations and mere absence do not retire a context; explicit
dead-unit events do. The context receives current health, position, order, build-progress and movement
facts; worker resource cargo is inferred from named game-data buffs rather than
transport cargo-space fields. Contexts retained through fog/absence are marked
not currently visible so later action validation can fail closed. Unsupported unit kinds are ignored rather
than guessed. FSM emissions are now collected once per observation and the
first protocol action path handles `AssignWorkerToMinerals`: it resolves the
`Harvest_Gather` ability from SC2 game data, requires a currently visible own
SCV and a currently visible mineral-field target, excludes dead tags, then
chooses the nearest mineral field with unit-tag tie-breaking. The translator
emits a raw unit command through the existing `GameConnection.Run` action
submission path. Missing ability metadata, stale units and invalid targets fail
closed.

This is deliberately a thin vertical slice, not a completed economy scheduler.
It does not yet query SC2's per-unit available-ability list, reserve/saturate
mineral nodes, preserve long-lived worker-to-node assignments, or measure
throughput. Construction, threat response and combat intents remain
untranslated. The return-cargo path is implemented but still requires a live
match/replay validation. The slice has not yet been validated in a live match.

Automated fixtures cover deterministic normalization, snapshot visibility,
explicit death evidence, stable context refresh, fog/absence retention and
authoritative retirement. New action-translation fixtures cover nearest-node selection, dead-target
rejection, missing gather metadata, nearest-town-hall return selection, and
fail-closed return behavior when the ability or target is missing. Context
reconciliation fixtures also cover a unit tag morphing from a worker profile
to a combat profile. The latest
workflow attempt failed in the independent Sith branch-policy job because the
repository currently has more than the required `master` and `development`
branches; that failure is not a build/test result.

The opening economy and resource scheduler described above remain
specifications, not implemented or experimentally validated behavior.

## Next implementation gates

1. Add per-unit available-ability validation and action-result/error handling.
2. Implement stable worker-to-node assignments, reservations, saturation and
   baseline throughput metrics.
3. Add arrival-window scheduling and compare against deterministic replays.
4. Implement return-cargo, race-specific construction and production intents.
5. Complete one race's first-unit opening end-to-end, then generalize through
   ontology/capabilities; add replay regression and expose versioned strategy data.
