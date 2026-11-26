# Terran FSM catalog and frame-processing design

## Purpose and status

This document is the behavior-definition blueprint for the Terran runtime. It deliberately separates:
- **FSM definitions**: reusable state graphs registered once with FSM_API.
- **Entity contexts**: one persistent context per observed game entity; the context owns its FSM handles.
- **Processing groups**: ordered work queues advanced explicitly by the frame scheduler.
- **Strategy hypotheses**: estimates with confidence and time windows, never observations.
- **Intents**: proposed work that must pass the action-resolution boundary before becoming SC2 actions.

The catalog is a design inventory, not a claim that every graph below is already implemented or empirically tuned. Unit and ability identities, costs, prerequisites, weapon data, build times, morph abilities, and add-on compatibility must be resolved from the active SC2 ResponseData/game data. Unknown or ambiguous data fails closed.

## Frame processing

The frame scheduler is the only owner of the per-frame time budget. The intended order is:

| Order | Processing group | Responsibility | Typical output |
|---:|---|---|---|
| 1 | `DataSync` | Normalize the supplied observation; update cached facts; merge visible, snapshot, and hidden records; update last-seen timestamps; process explicit death events; reconcile entity contexts and handles | Current world snapshot and change events |
| 2 | `Strategy` | Evaluate our progression and opponent progression hypotheses against game-loop time, observed milestones, and uncertainty | Strategic goals, threat windows, confidence |
| 3 | `SubStrategy` | Decompose goals into coordinated plans: economy, production, scouting, defense, attack, and formations | Assignments, formation plans, reservations |
| 4 | `HighDPS` | Prioritize time-critical and high-impact reactions: lethal threats, tactical arrivals, emergency repairs, focus-fire opportunities | Urgent intents |
| 5 | `LowDPS` | Run routine mining, production maintenance, positioning, regrouping, scouting routes, and housekeeping | Routine intents |
| 6 | `ActionResolution` | Deduplicate and arbitrate intents; validate current ownership, target, visibility, ability, range, cost, prerequisites, cooldown/order state, and placement; translate accepted intents | The outgoing SC2 action list |

The first five are behavior/decision groups. Action resolution is the final safety boundary. A frame loop calls `FSM_API.Interaction.Update(group)` in this order. It checks the remaining time between groups; when the budget is exhausted it breaks out and sends the already prepared frame. It must not use `continue` to express stopping the loop.

DataSync must be the fastest stage. Do not put pathfinding, opponent simulation, formation search, or expensive target scoring in it. It should update compact facts and enqueue change events for later groups.

## Shared entity lifecycle FSM

Every entity with positive maximum hit points receives the same base **EntityExistence** FSM, regardless of alliance, race, unit type, or current visibility. This includes our units, enemy units, and neutral hit-point entities. Capability FSMs are attached in addition to it.

Suggested states:

| State | Meaning | Important transitions |
|---|---|---|
| `FirstSeen` | Entity identity/context created from an authoritative record | `FirstSeen -> AliveVisible` when currently visible; `FirstSeen -> AliveRemembered` for snapshot/hidden records |
| `AliveVisible` | Current frame contains live visible facts | `-> AliveRemembered` when only snapshot/hidden/last-known evidence remains |
| `AliveRemembered` | Entity is known but not currently visible; last-known facts remain available | `-> AliveVisible` on reacquisition; `-> Dead` only on explicit death evidence |
| `Dead` | Authoritative dead-unit event received | Terminal cleanup: destroy all entity-owned handles and remove context |

Absence from the current observation is not a death event. A snapshot is not a current visible target. Preserve last-known position, health, orders, type, owner, and observation loop separately from current-frame fields. Strategy may reason from remembered contacts; ActionResolution must enforce current visibility/targetability rules for commands that require them.

For entities without hit points (for example, resource nodes if the protocol exposes them that way), attach a domain lifecycle FSM based on their actual protocol facts rather than fabricating hit points.

## Global and shared FSM definitions

### Match / DataSync

- **MatchLifecycle**: `Initializing -> Running -> Ending -> Complete`; owns match start/end, player identity, game-loop clock, and teardown.
- **ObservationSync**: `AwaitingFrame -> ApplyingFrame -> Published`; applies one normalized observation atomically and publishes a frame version.
- **EntityRegistry**: `Reconciling -> Stable`; creates, refreshes, remembers, morph-reconfigures, and retires entity contexts.
- **WorldKnowledge**: `Unknown -> PartiallyKnown -> Updated`; maintains current and remembered contacts, map control, resource locations, and confidence.
- **OpponentProgression**: `Unclassified -> HypothesisSet -> UpdatedHypotheses`; scores candidate build paths against observed milestones and elapsed game time. Multiple hypotheses may coexist.
- **ThreatAssessment**: `NoElevatedThreat -> Suspected -> Imminent -> Confirmed -> Resolved`; distinguishes estimated earliest arrival from actual contact.
- **EconomyStrategy**: `Stabilize -> Expand -> Tech -> Sustain`; chooses resource/supply/production goals under constraints.
- **ArmyStrategy**: `Assemble -> Defend/Attack/Scout -> Regroup -> Reassess`; sets army objectives and force posture.
- **FormationCoordinator**: `Unassigned -> Forming -> Ready -> Moving -> Engaging -> Dispersing`; coordinates squads/platoons and re-forms after losses.
- **ActionArbitration**: `Collecting -> Ranked -> Validating -> Accepted/Rejected -> Submitted`; only this boundary emits protocol commands.

Opponent progression is probabilistic/heuristic, not clairvoyance. For example, a fast-tech-to-Battlecruiser hypothesis should estimate a plausible earliest arrival from the observed opening and game data. If no enemy units are visible, the bot can still prepare a defense when the expected loss of waiting exceeds the cost of preparation. A teleport/arrival response, however, is only triggered by observed evidence of that event.

## Terran unit FSM definitions

All Terran hit-point entities get **EntityExistence**. The following are capability graphs to attach to appropriate entity contexts. Definitions are reusable by unit class; unit-specific parameters come from game data and ontology.

### Workers and utility units

| Unit | Capability FSMs | Principal states and transitions |
|---|---|---|
| **SCV** | WorkerRoute, Construction, Repair, Survival | WorkerRoute: `Unassigned -> Assigned -> TravellingToMineral -> Mining -> Carrying -> Returning -> Unloading`; reassign on depleted/unsafe patch. Construction: `Available -> Reserved -> Travelling -> Building -> Verifying -> Released`; cancel/recover on invalid placement or destroyed target. Repair: `Idle -> Assigned -> Approaching -> Repairing -> Reassessing -> Complete`; survival can interrupt for retreat. |
| **MULE** | TimedWorker, RepairSupport, Survival | `Spawned -> Assigned -> Travelling -> Mining -> Returning/Unloading -> Expiring -> Retired`; respect timed lifetime and target/resource constraints. May be assigned to emergency repair only where game rules/abilities permit. |
| **AutoTurret** | TemporaryDefense, Targeting | `Deploying -> Acquiring -> Attacking -> Expiring -> Retired`; target selection and lifetime are data-driven. |
| **PointDefenseDrone** (if present in active game data) | TemporarySupport | `Deploying -> Protecting -> Depleted/Expired -> Retired`; resolve actual current ability/unit support before enabling. |

### Infantry and specialist units

| Unit | Capability FSMs | Principal states and transitions |
|---|---|---|
| **Marine** | InfantryCombat, FormationMember, Stim (if available) | `Rallying -> Forming -> Advancing -> Acquiring -> Firing -> Kiting/Regrouping -> Reassessing`; retreat/medivac pickup can interrupt. Stim is a guarded sub-capability, not unconditional behavior. |
| **Marauder** | HeavyInfantryCombat, FormationMember, SlowTargeting | `Rallying -> Forming -> Advancing -> PrioritizingArmored -> Firing -> Kiting/Regrouping`; concussion/slow effect is validated from game data. |
| **Reaper** | Harassment, Scouting, Mobility | `Staging -> Infiltrating -> Scouting -> Harassing -> Evading -> Regrouping`; grenade/mobility actions require authoritative ability data and tactical validation. |
| **Ghost** | Stealth, Spellcaster, PrecisionTargeting | `Concealed/Visible -> Positioning -> AcquiringPriorityTarget -> Casting/Sniping -> Relocating`; cloak, EMP, snipe, and tactical-nuke paths are independent guarded capabilities. |
| **Marauder/Marine squad** | SquadFormation | `Gathering -> Formed -> Moving -> Engaging -> Kiting -> Regrouping`; membership and formation geometry are owned by SubStrategy, not individual units. |

### Factory units

| Unit | Capability FSMs | Principal states and transitions |
|---|---|---|
| **Hellion** | MobileAreaCombat, Harassment | `Rallying -> Maneuvering -> AcquiringLightTargets -> Firing -> Disengaging`; exploit line/area opportunities without exposing the unit to unfavorable trades. |
| **Hellbat** | AssaultModeCombat, FormationMember | `Rallying -> Forming -> Advancing -> Engaging -> Withdrawing`; mode/transform transitions are explicit and game-data validated. |
| **Siege Tank** | ArtilleryCombat, SiegeMode, FormationMember | `Mobile -> Positioning -> Sieging -> Acquiring -> Firing -> Unsieging -> Relocating`; siege position, friendly-fire/overlap constraints, and threat windows gate transitions. |
| **Cyclone** | LockOnCombat, MobileKiting | `Patrolling -> Acquiring -> Locking -> MaintainingLock -> Firing/Chasing -> BreakingOff`; lock-on validity and range are observed/validated. |
| **Widow Mine** | Ambush, Burrow, DetectionResponse | `Mobile -> Positioning -> Burrowing -> Armed -> Triggered/Cooldown -> Repositioning`; friendly-fire and target eligibility are essential validation rules. |
| **Thor** | HeavyAntiAir, HeavyGroundCombat, FormationMember | `Rallying -> Forming -> Advancing -> SelectingWeaponMode -> Engaging -> Repositioning`; weapon mode and target class depend on current data and observed target composition. |

### Starport units

| Unit | Capability FSMs | Principal states and transitions |
|---|---|---|
| **Medivac** | Transport, HealSupport, PickupEvacuation | `Rallying -> Escorting -> Healing/Loading -> Transporting -> Unloading -> Rejoining`; emergency pickup can preempt normal escort. |
| **Viking** | Transform, AntiAir, Assault | `Rallying -> AirMode/AssaultMode -> Acquiring -> Engaging -> Transforming -> Repositioning`; mode changes require target mix and tactical context. |
| **Liberator** | Transform, AreaDenial | `Mobile -> Positioning -> DeployingZone -> DenyingArea -> Unsieging -> Relocating`; zone placement must be checked against current targets and map geometry. |
| **Raven** | Detection, Spellcaster, Support | `Escorting -> Scanning/Detecting -> AcquiringThreat -> DeployingSupport -> Repositioning`; each spell/construct is a separate validated capability with energy and cooldown constraints. |
| **Banshee** | Cloak, PrecisionAirCombat, Harassment | `Staging -> Infiltrating -> AcquiringGroundTarget -> Attacking -> Evading -> Regrouping`; cloak is guarded by energy and detection/threat assessment. |
| **Battlecruiser** | CapitalShipCombat, TacticalJump, RepairSupport | `Staging -> Advancing -> Acquiring -> Firing -> TacticalJumping -> Reacquiring -> Repairing/Regrouping`; tactical jump requires a legal destination and validated ability; repair support is coordinated with SCVs/MULEs where legal. |

## Terran structure FSM definitions

Structures also receive EntityExistence and one or more structure capabilities.

| Structure | Capability FSMs | Principal states and transitions |
|---|---|---|
| **Command Center** | TownHall, WorkerProduction, Rally, Morph | `Landmark/Constructing -> Operational -> TrainingSCV -> ProductionComplete -> Operational`; morph branch to Orbital or Planetary only when prerequisites/resources and ability data permit. Owns worker rally and production queue state. |
| **Orbital Command** | TownHall, SCVProduction, MULECallDown, Scan, LiftLand | `Operational -> Producing/CallingMULE/Scanning -> Cooldown/Operational`; energy is budgeted across competing abilities. Lift/land is a separate mobility capability. |
| **Planetary Fortress** | FortifiedTownHall, Defense, SCVProduction | `Operational -> AcquiringThreat -> Attacking -> Reassessing`; retains town-hall economy/production duties while exposing defensive capability. |
| **Supply Depot** | SupplyCapacity, RaiseLower, Construction | `Planned -> Building -> OnlineLowered -> RaisedForDefense -> LoweredForAccess`; raising/lowering must not block friendly pathing or trap units. |
| **Refinery** | GasProduction | `Planned -> Building -> Operational -> Saturating -> Depleted/Unavailable`; gas worker assignments and current contents are synchronized from observations. |
| **Barracks** | InfantryProduction, AddOn, Rally | `Idle -> CheckingResourcesAndSupply -> Training -> Completing -> Idle`; completion callback reconciles the newly created unit to this producer and attaches its capability FSMs. |
| **Factory** | VehicleProduction, AddOn, Rally | Same production lifecycle, with factory-specific product/prerequisite metadata and add-on compatibility. |
| **Starport** | AirProduction, AddOn, Rally | Same production lifecycle, with starport-specific product/prerequisite metadata and add-on compatibility. |
| **Engineering Bay** | Infantry/StructureUpgradeResearch | `Idle -> CheckingPrerequisites -> Researching -> Completing -> Idle`; upgrade state is shared with Strategy and relevant units. |
| **Armory** | Vehicle/ShipUpgradeResearch | Same research lifecycle; upgrades are selected by composition and strategic priorities. |
| **Ghost Academy** | GhostTechResearch, NukeProduction | `Idle -> Researching/Producing -> Completing -> Idle`; cloak/energy/nuke readiness feeds Ghost capabilities. |
| **Fusion Core** | CapitalTechResearch | `Idle -> Researching -> Completing -> Idle`; unlocks and upgrade metadata come from game data. |
| **Bunker** | GarrisonDefense, Evacuation | `Empty -> Loading -> Garrisoned -> Firing -> Unloading/Destroyed`; unit occupancy and weapon eligibility are observed facts. |
| **Missile Turret** | StaticAntiAir, Detection | `Constructing -> Operational -> AcquiringAirThreat -> Attacking -> Reassessing`; placement is driven by threat coverage and mineral-line defense goals. |
| **Sensor Tower** | ThreatDetection, Coverage | `Constructing -> Operational -> UpdatingCoverage -> ReportingContacts`; warning coverage informs Strategy/HighDPS but does not replace visible-target evidence. |
| **Tech Lab** | ResearchAddon | `Idle -> Researching -> Completing -> Idle`; parent producer relationship and compatibility are tracked. |
| **Reactor** | ParallelProductionAddon | `Idle -> QueueingParallelProduction -> Producing -> Completing -> Idle`; actual queue limits and compatibility are read from game data. |

## Producer completion and unit handoff

A producer's production FSM owns the production order. When the observed order disappears and a new unit is reconciled, the producer's completion transition performs the handoff from its exit callback: identify the newly created unit using the before/after entity set, owner, unit type, producer order, timing, and any available protocol linkage; then attach the new entity's base and role capabilities. Do not attribute by nearest distance alone. If attribution is ambiguous, create the entity context but leave producer assignment unresolved rather than guessing.

## Formation hierarchy

SubStrategy owns formations, not the individual unit FSM. A formation context owns:
- ordered membership and role slots;
- target objective and rally point;
- desired spacing/shape and movement mode;
- cohesion and regroup thresholds;
- threat and terrain constraints;
- replacement/reassignment after death, transport, or morph.

Unit FSMs receive formation assignments and report readiness, location, and availability. This enables squads, platoons, and larger formations without duplicating formation policy in every unit.

## Implementation order

1. Correct DataSync/entity lifecycle and make contexts own their handles.
2. Add the explicit ordered frame scheduler and remove nested group updates/early-return bypasses.
3. Add opponent progression hypotheses and threat windows, initially as deterministic rule-based FSMs with confidence and earliest/latest timing.
4. Add SubStrategy formation and defense planners.
5. Implement the shared unit capability definitions, starting with SCV, Command Center/Orbital, Supply Depot, Barracks, Marine, and Missile Turret.
6. Expand to Factory and Starport families and the remaining Terran roster.
7. Validate each capability with deterministic observation replay, unit tests, and controlled SC2 matches. No behavior is called complete based on a definition compiling alone.
