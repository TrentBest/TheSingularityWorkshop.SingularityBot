# Military organization and game-phase doctrine

## Purpose

SingularityBot needs two complementary structures:

1. A **command hierarchy** that turns strategic intent into coordinated unit assignments.
2. A **phase system** that selects which strategic doctrines and plans are active as the match evolves.

They must not be conflated. Game phases decide what the bot is trying to achieve. Commanders organize and direct forces toward those objectives. Unit AI executes the local behavior required by its assignment.

This document defines the target architecture and first implementation sequence. It does not claim that every echelon or phase transition is implemented.

## 1. Ownership boundaries

- **Unit AI** owns one observed unit's persistent behavior: local movement execution, target selection within its assignment, ability use, combat micro, survival, and reporting its state. It does not independently rewrite army strategy or steal units assigned elsewhere.
- **Squad** is the smallest reusable tactical formation. It groups units around one coherent role and objective: defense, escort, screening, scouting, assault, raid, drop, or harassment. A squad is defined by capability and purpose, not a fixed headcount or a rigid real-world table of organization.
- **Platoon** coordinates multiple squads for one local operation, such as a base defense, multi-angle assault, or expansion escort. It handles coordination that would be awkward to encode in every squad.
- **Task Force** is an optional higher-level grouping for a larger operation that combines different capabilities or operates across multiple map regions. It may contain platoons, squads, or specialist elements directly.
- **Army Commander** owns the match-wide military picture: force readiness, defense coverage, threat response, strategic force allocation, and the assignment of operations to subordinate formations.
- **Strategic Advisors and Strategic Planner** remain peer decision/planning systems, not military ranks. Advisors publish intent and needs; the planner arbitrates resources and eligible units. The Army Commander translates accepted military intent into an operational organization and assignments.

The hierarchy is intentionally flexible. A small bot may use Unit AI -> Squad -> Army Commander. A more capable bot may insert Platoons and Task Forces. Do not create empty organizational layers merely to satisfy a military analogy.

### Naming

Use **Squad** as the default tactical group. Use **Platoon** when multiple squads need a coordinating leader. Use **Task Force** for an operation assembled from mixed capabilities or multiple echelons. Reserve **Squadron** for a domain-specific air formation if that abstraction proves useful; it should not be the generic name for every group. Avoid assuming that real-world military unit sizes, ranks, or command relationships map directly to SC2.

### Command is not ownership

A unit has one authoritative active assignment at a time, enforced by planner reservations. A commander may request, propose, or release assignments through the planner, but must not silently duplicate a unit into incompatible squads or operations. Temporary support relationships (for example, a squad covered by another squad's fire) are references, not a second ownership claim.

Squads and higher formations should have stable identities, explicit lifecycle states, capability requirements, objectives, and evidence-backed completion/abort conditions. Membership changes are expected as units are produced, destroyed, recalled, loaded, or reassigned.

## 2. Match phases are strategy slots, not a single hard-coded strategy

The bot is a collection of phase-specific strategic options. Each phase can define its own economy, technology, composition, military posture, intelligence priorities, and transition policy. A developer may provide multiple plans within a phase and custom phases beyond the defaults.

The default phase sequence is:

1. **Opening** — execute the selected opening build/production plan, establish initial economy and infrastructure, and gather the first information.
2. **Early Game** — establish the first operational military capability, protect the economy, scout, and decide whether to pressure, defend, expand, or tech.
3. **Mid Game** — coordinate multiple production systems and formations, secure map control, expand, tech, and execute sustained operations.
4. **Late Game** — manage advanced compositions, upgrades, high-value operations, replacement economics, and decisive map-wide objectives.

These are default authoring slots, not universal clock-time intervals. A developer may add phases such as **Ultra Late Game**, replace the default transition policy, or provide alternate plans for any phase.

### Opening boundary

The default Opening -> Early Game transition is the first **completed, friendly, non-worker military unit** observed in the match.

Use completed-unit evidence, not a production order being queued, a producer being busy, or a unit merely being planned. This makes the boundary observable and race-neutral. Each race module supplies the authoritative classification of military units so that Probes, SCVs, and Drones are never mistaken for the first military unit.

A developer may override this condition when a strategy intentionally defines a different opening boundary. The default transition is one-way; a phase does not regress because its first unit later dies.

### Later transitions

Mid Game and Late Game must be selected from evidence and strategy policy, not fixed elapsed time. Candidate evidence includes completed tech structures, production capability, army supply/composition, expansion count, upgrades, opponent strength, map control, and strategic opportunities. No single building count should be treated as universally correct for all races or strategies.

The phase engine should support:
- Evidence predicates with freshness and confidence.
- Composite conditions (all/any/threshold) and explicit required facts.
- Entry and exit conditions, minimum dwell time, and hysteresis where regression is allowed.
- Priority for urgent survival/defense overrides without corrupting the strategic phase.
- Developer-defined phase IDs, display names, ordering, transition rules, and plan sets.
- A default monotonic progression policy, with explicit opt-in for phase regression or alternate phase graphs.

A phase transition selects a new strategic context. It does not destroy unit FSMs, squads, ongoing operations, or valid reservations. The new phase may continue, amend, cancel, or replace those plans through explicit reconciliation.

## 3. Plans within a phase

A phase is not one giant script. It contains a set of selectable strategic plans and policy modules. For example, Early Game might offer:
- Safe expansion.
- Early pressure.
- Defensive containment.
- Fast technology.
- Scouting-first adaptation.

Selection should be based on authored priorities, observed evidence, opponent information, feasibility, and resource constraints. The phase provides the default plan set; the Strategic Planner and validated action pipeline still arbitrate execution.

A plan may declare:
- Preconditions and selection predicates.
- Economy, technology, composition, and military objectives.
- Required capabilities and resource reserves.
- Preferred formations and operation templates.
- Success, failure, timeout, and abort conditions.
- What should persist or be handed off when the phase changes.

Do not hard-code one universal Terran opening or Marine composition into the phase engine. Race-specific production and unit capabilities belong to the selected race module; phase policy remains race-neutral where possible.

## 4. Example: Ultra Late Game asymmetric Battlecruiser operation

Ultra Late Game is a useful example of why custom phases matter. A developer might define a transition when the economy is entering a scarcity regime while a substantial bank still exists: income and replenishment capacity are falling relative to expected expenditure, and the remaining resources should be converted into a deliberate final force rather than stranded.

One possible Terran plan is an asymmetric Battlecruiser strike:

1. Reinvest a defined portion of the remaining bank into a small, high-value Battlecruiser force and its required technology.
2. Keep the force concealed or safely staged while energy and tactical readiness are established.
3. Use Intelligence and spatial reasoning to identify a high-value exposed target and a viable approach/escape corridor.
4. Trigger Yamato against a validated high-value target at the first actionable observation, subject to range, energy, cooldown, line-of-sight and ability validation.
5. Execute Tactical Jump to a prevalidated safer position when the ability is available and the threat model warrants disengagement.
6. Reposition along a low-exposure map-edge route toward a flank, then repeat against another exposed target or withdraw.

This is a **risk-minimizing objective**, not a guarantee of zero damage. The planner must account for detection, anti-air coverage, ability availability, command latency, path/landing safety, enemy prediction, and the possibility that the escape route is cut off. The unit AI owns Battlecruiser micro; the squad owns the raid; a platoon/task force coordinates distractions or supporting pressure; the Army Commander chooses whether the operation is worth the force and resource commitment.

The existing spatial side/edge tooling should be reused for route generation. The operation must use observed map bounds and current threat evidence, not hard-coded map coordinates or a promise that the edge is safe.

## 5. Runtime responsibilities and lifecycle

The target execution flow is:

1. Observation reconciliation updates unit, building, resource, visibility, and map facts.
2. The phase evaluator processes current evidence and determines the active phase.
3. Phase-selected advisors publish and update strategic intent.
4. The Strategic Planner arbitrates jobs, resources, and unit reservations.
5. The Army Commander organizes accepted military intent into operations and formations.
6. Task Force/Platoon/Squad commanders coordinate their subordinate assignments.
7. Unit AI executes local behavior and emits progress/evidence.
8. The action pipeline validates every proposed SC2 command against authoritative current facts.

This is synchronous frame-driven orchestration. The hierarchy describes logical responsibility, not background threads or concurrent processes.

## 6. Refactoring sequence

Implement in small, testable vertical slices:

1. **Phase model first:** introduce a phase definition/transition contract that supports Opening, Early, Mid, Late, and developer-defined phases. Preserve existing GameState FSM behavior through an adapter while migrating.
2. **Opening completion evidence:** add race-neutral military-unit classification supplied by race modules; test that the first completed non-worker military unit advances Opening -> Early exactly once.
3. **Unit behavior boundary:** keep individual behavior in Unit AI definitions and contexts; remove commander responsibilities that duplicate unit-level FSM capabilities.
4. **Squad primitive:** introduce stable squad identity, role, objective, membership, assignment ownership, lifecycle, and deterministic membership reconciliation.
5. **Army Commander:** move match-wide military assessment and operation orchestration behind a clear contract.
6. **Optional echelons:** add Platoon and Task Force only when real coordination needs them; avoid speculative empty layers.
7. **Phase plans:** attach plan sets and transition evidence to BotDefinition/strategy authoring, including custom Ultra Late Game.
8. **Operational validation:** test reservations, unit loss, phase transitions during active operations, stale information, emergency defense, and all three race modules.

Every stage must preserve fail-closed command validation and the current Terran gameplay baseline. Do not claim a strategic phase or operation is complete merely because it was selected or assigned; require observed execution evidence.

## Squad lifecycle vertical slice

The first tactical command context is implemented as an FSM_API-backed `SquadContext` with a stable squad ID, role, objective ID, explicit member roster, and lifecycle states: Forming, Ready, Executing, Regrouping, and Disbanding. `SquadBehaviorRuntime` owns the contexts and their FSM handles, enforces one squad membership per unit tag within the runtime, and removes membership only on explicit dead-unit evidence or an explicit reassignment/removal.

This is deliberately a tactical roster, not a second SC2 action authority. Recording a unit in a squad does not reserve it for an order and does not authorize an action. The existing action pipeline and authoritative reservation/legality checks remain the gate before issuing commands. The current bot still has a separate military-operation assignment map; connecting squad rosters to that path is the next integration step, and must replace—not duplicate—its ownership semantics.

The runtime preserves entity lifecycle semantics: temporary fog-of-war absence is not death, and squad FSM handles are destroyed when the squad is explicitly disbanded or the runtime is cleared. The FSM definition is registered idempotently and uses the existing strategy processing group.

