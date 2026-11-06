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

The economy controller owns global worker assignments and reservations. Unit
FSMs execute those assignments and report observed facts; they must not make
conflicting independent node selections. Race-specific supply and construction
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
dead-unit events do. The context receives current health, position, cargo, order,
build-progress and movement facts. Unsupported unit kinds are ignored rather
than guessed. The behavior callback is still intentionally a no-op: intents are
not yet translated or submitted as SC2 actions. The bot is therefore not yet
an active-playing bot.

Automated fixtures cover deterministic normalization, snapshot visibility,
explicit death evidence, stable context refresh, fog/absence retention and
authoritative retirement. These fixtures run in CI.
The opening economy and resource scheduler described above are specifications,
not implemented or experimentally validated behavior.

## Next implementation gates

1. Complete normalized economy facts for workers, mineral nodes, town halls,
   resources, supply, orders and construction.
2. Implement deterministic worker assignment, reservations and baseline metrics.
3. Add saturation-aware and arrival-window scheduling; compare through replay.
4. Implement race-specific supply, construction and production planning.
5. Add intent capture, validation and legal action translation as a separate gate.
6. Complete one race's first-unit opening end-to-end, then generalize through
   ontology/capabilities; add replay regression and expose versioned strategy data.
