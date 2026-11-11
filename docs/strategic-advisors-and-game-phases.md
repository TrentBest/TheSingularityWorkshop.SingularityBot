# Strategic advisors and game-phase scheduling

## Purpose

The bot should expose a coherent strategic decision layer rather than accumulating unrelated controllers. Domain advisors reason about different questions, while a match-level Game State FSM determines which strategic era the match is in. The Task Master is the eventual arbitration and assignment authority.

The five advisor domains are:

- **Economy Advisor** — worker saturation, mineral/gas allocation, production affordability, expansion timing and economic reserves.
- **Military Advisor** — force readiness, defense posture, threat response, attack opportunities, force composition and recovery.
- **Science Advisor** — technology paths, research priorities, prerequisites, upgrade timing and the strategic value of tech investments.
- **Base Engineering Advisor** — base layout and infrastructure, supply capacity, production footprint, defenses, repair, expansion-site readiness and resilient base operation.
- **Intelligence Advisor** — scouting missions, information requirements, target-site revisit schedules, opponent progression evidence, threat confidence and information freshness.

These advisors are peers. No advisor directly steals units, spends resources or submits SC2 commands. Each publishes typed assessments and proposed jobs. The Task Master resolves competing requests, reserves actors/resources, and assigns executable work. The action pipeline remains the final legality boundary.

## Game State FSM

The match-level `SingularityBot.GameState` FSM owns three strategic states:

| State | Current evidence-driven entry conditions | Dominant concerns |
|---|---|---|
| EarlyGame | Initial state | Establish economy, first infrastructure, worker production, map discovery and early warning |
| MidGame | At least two observed friendly town halls, or an observed Factory, Starport, Engineering Bay, Armory or Ghost Academy | Expand safely, establish composition, defend map control, begin technology and maintain scouting |
| LateGame | At least three observed friendly town halls, or an observed Fusion Core or Battlecruiser | High-value force operations, advanced technology, upgrades, map-wide intelligence, defense and replacement economics |

These are deliberately conservative first-pass heuristics, not a claim that a fixed building count perfectly identifies strategic phase. They use current visible normalized facts, not elapsed time. The phase is monotonic in this initial FSM: it does not regress when a structure is destroyed or temporarily hidden. Future transitions should incorporate opponent strength, army supply, upgrade completion, economy saturation, tech availability and match-end conditions, with confidence and hysteresis to prevent oscillation.

The FSM is a match-level context, not a per-unit FSM. It is evaluated in the Strategy processing group after DataSync has applied the current observation.

## Phase-aware computation

Each advisor owns a dedicated FSM processing group. The frame scheduler advances each advisor group a weighted number of times per frame. The first allocation is intentionally simple and deterministic:

| Advisor | Early | Mid | Late |
|---|---:|---:|---:|
| Economy | 5 | 3 | 2 |
| Military | 2 | 4 | 5 |
| Science | 1 | 3 | 4 |
| Base Engineering | 3 | 3 | 2 |
| Intelligence | 4 | 3 | 3 |
| **Total work slots** | **15** | **16** | **16** |

The values are relative evaluation slots, not milliseconds or a guarantee of equal execution cost. They express the initial doctrine: early economy and scouting dominate; military and science rise through midgame; late-game force decisions and technology receive more attention. Base engineering remains consistently important.

The scheduler currently provides deterministic weighted FSM update opportunities. It does not yet measure wall-clock time, preempt an advisor midway through evaluation, or guarantee a fixed percentage of CPU time. Expensive advisors must be decomposed into bounded incremental FSM work. A later scheduler can add a measured frame budget, per-advisor cost estimates, deadline/urgency overrides and deferred work queues without changing the advisor contracts.

## Advisor contract and task flow

The initial shared contract exposes an advisor's domain, current game phase and phase-derived compute weight. The Economy Advisor implements this contract and retains its existing configurable mineral reservation policy. The remaining domains are established as processing groups and extension points; their domain-specific policy engines and task producers are not yet implemented.

Intended flow:

1. DataSync reconciles visible and remembered entities, resources, structures, orders and game-loop facts.
2. Game State FSM evaluates phase transitions from the normalized world state.
3. The scheduler allocates each advisor its phase-weighted evaluation slots.
4. Advisors update domain assessments and submit typed recommendations/jobs.
5. Task Master scores and arbitrates requests, checking prerequisites, deadlines, resource and unit reservations, and conflicts.
6. Unit/group/job FSMs execute assignments and report verified progress.
7. Action resolution validates each concrete intent against current authoritative observations and game data.

An advisor may request a scout for a location, but it cannot select a worker already reserved for construction. The Intelligence Advisor describes the information gap and proposes the scouting job; Task Master finds and reserves an eligible scout; the scout FSM executes the route; DataSync records the observation; Intelligence Advisor updates confidence and revisit timing.

Similarly, Science may recommend an upgrade, Base Engineering may request a defensive structure, and Military may request a defending force. Their recommendations are not commands and do not bypass shared resource or unit allocation.

## Implementation status and next slices

Implemented in this pass:

- Five named advisor domains and dedicated processing groups.
- A shared advisor contract.
- A match-level Game State FSM with Early/Mid/Late states.
- Observation-derived phase evidence and deterministic phase-aware weighted scheduling.
- Economy Advisor integration with the shared contract and current phase.
- Tests for phase evidence, advisor schedule membership and phase-dependent weights.

Not yet implemented:

- Military, Science, Base Engineering and Intelligence policy engines.
- Intelligence-owned base-site visit/revisit FSMs and scouting job generation.
- Task Master integration that arbitrates all advisor-produced jobs.
- Typed recommendation/event queues and per-advisor diagnostics.
- Measured wall-clock compute budgeting, incremental work quotas and starvation protection.
- GUI controls for advisor policy, priorities and compute budgets.

Next vertical slices should be:
1. Add the Intelligence Advisor over the existing spatial intelligence and persistent base-site model; create natural/third/fourth scouting jobs and report requirements.
2. Add Task Master intake and atomic reservations for those jobs, reusing existing unit contexts and scout capabilities.
3. Add Military assessment for force readiness, defense coverage and urgent threats.
4. Add Base Engineering assessment for supply, production footprint, repair and base defense.
5. Add Science assessment over authoritative game-data prerequisites and upgrade state.
6. Move Economy's opening reservations behind the same typed recommendation/task boundary.
7. Add bounded compute-cost telemetry and GUI authoring/inspection once the contracts stabilize.

The phase weights are defaults, not hard-coded strategic truths. They must become user-configurable policy only after diagnostics show their actual cost and effect. Keep every advisor deterministic and evidence-backed; uncertain opponent inferences carry confidence and expiry rather than being promoted to observed facts.
