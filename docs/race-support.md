# Race support and first-tier pressure

## Identity: one Random-race bot, race-specific strategy configurations

SingularityBot is a **Random-race StarCraft II participant with an architecture for explicit race-specific strategy configurations**, not a claim that three complete strategies are already playable. The SC2 runner requests the Random race by default, and the runtime resolves the authoritative in-game `RaceActual` against a trusted race/mode catalogue. A requested race is never treated as proof of the race actually assigned by the match.

Race configuration is a property of the bot/match composition. It must select the race-specific economy, worker construction rules, production profile, unit capabilities, and strategy mode. The shared decision system—observation reconciliation, persistent contexts, scouting, threat assessment, task arbitration, spatial reasoning, and evidence-gated action submission—is intended to be reused wherever the domain contracts genuinely match. The current default Terran definition is not a universal definition that can be run unchanged under any Random result.

**Important capability distinction (current source state):**

| Race / mode | Current status | What this means |
|---|---|---|
| Terran / Marine Mode | Implemented baseline; regression-tested | The existing worker/economy, construction, Marine production, scouting, and tactical foundations are Terran-specific. A live match/replay is still required to prove the complete opening. |
| Zerg / Zergling opening | Experimental selectable path | The runner exposes `--mode=zerg.zergling.v1` and a first-Zergling-pair definition. This is not yet a complete Zerg economy, production, scouting, or combat composition. |
| Protoss / Zealot or Stalker | Registered mode identities only | The catalogue records intended modes, but they are not yet authorized to issue gameplay actions. |
| Other registered modes | Selection metadata only unless explicitly marked executable | A name in the catalogue is not proof of a working strategy. |

The default Random request must not silently fall through to the Terran Marine strategy when SC2 assigns Protoss or Zerg. The current default BotDefinition is Terran Marine; selecting a different race requires a matching definition/mode and an executable opener. Match startup must resolve actual race and a compatible executable composition; if none exists, it must fail closed without issuing cross-race commands. Forge v1 currently keeps its visual race selector disabled and Terran-only, even though the runtime schema/catalogue contain additional race identities. Until that authoring path and the chosen race's end-to-end behavior are verified, do not describe Random as three-race gameplay parity.

## Deterministic local baseline run

The runner now accepts an explicit Terran baseline mode so the first controlled match does not depend on SC2's Random race assignment:

```powershell
dotnet run --project src/SingularityBot -- --mode=terran.marine.v1
```

This requests Terran and uses the existing Adaptive Marine strategy on the configured local map/opponent settings. Omitting `--mode` intentionally retains the Random request; because the compiled default definition is Terran Marine, a non-Terran actual race must still fail closed. The experimental Zergling opener remains separately selectable with `--mode=zerg.zergling.v1`, but it is not the recommended first gameplay proof and does not represent full Zerg support.

## Strategic objective: fast first-tier pressure, intelligently

The first shared strategic objective is to establish the fastest sustainable first-tier combat force that the selected race can support, then project pressure toward the opponent without blind movement.

This is not a fixed build order shared across races. Each race has different workers, supply mechanics, producers, construction semantics, unit costs, prerequisites, and production cadence. The race profile supplies those facts; the shared planner reasons over them.

The intended decision loop is:

1. **Establish economy and production.** Keep worker production and resource collection healthy while opening the earliest legal first-tier production path. Avoid starving the economy or blocking supply.
2. **Maximize useful throughput.** Produce the strongest feasible first-tier force from current resources, production capacity, prerequisites, and map timing. Do not queue units that cannot complete or spend resources reserved for essential infrastructure.
3. **Scout before committing.** Assign low-cost/appropriate units to observe enemy approaches, likely proxy locations, expansions, and the opponent's exposed or defended routes. Preserve a safe route and report contacts with last-seen time and confidence.
4. **Classify the contact.** Distinguish visible enemies, remembered contacts, inferred threats, and unknown territory. Never treat stale sightings as current positions.
5. **Choose a capability-aware route and objective.** Consider unit speed, range, attack/armor attributes, air/ground capability, detection, transport needs, terrain, pathability, chokepoints, and enemy threat coverage. Fast units may screen or flank; fragile or short-ranged units should not be sent through a route that their capabilities cannot survive.
6. **Commit only when evidence supports it.** Attack, contain, harass, defend, or continue scouting based on observed force balance and objective value. Revalidate target visibility, range, legal ability, and friendly force state before every action.
7. **Reassess continuously.** Update the plan as new scouting information arrives, units are produced or lost, resources change, or an enemy response invalidates the route.

The goal is **rapid, informed pressure**, not maximum unit count at any cost. A force that arrives piecemeal into a prepared defense is not a successful rush.

## Shared and race-specific ownership

| Concern | Shared decision system | Race-specific composition |
|---|---|---|
| World picture | Normalize observations, track visibility and remembered contacts | Interpret race-specific units, structures, and abilities |
| Economy | Resource budgets, reservations, worker assignment goals, saturation signals | Worker lifecycle, resource routes, construction/consumption rules |
| Production | Prerequisite graph, desired counts, queue pressure, completion evidence | Producers, supply, morphing, add-ons, race-specific build mechanics |
| Scouting | Coverage, information value, contact freshness, safe-route selection | Scout unit choice and its movement/vision/detection capabilities |
| Military | Force objectives, threat comparison, route assessment, regroup/retreat policy | Unit stats, weapon/armor mechanics, abilities, composition roles |
| Execution | One validated action pipeline; fail closed on uncertainty | Translate approved intent into legal race-specific SC2 actions |

Do not duplicate the game loop or create a parallel scheduler for each race. Extract reusable policy only where semantics are truly shared; keep worker construction, production mechanics, and unit capabilities explicit in the race composition.

## First-tier pressure acceptance gates

A race is not considered playable merely because it has a mode ID or a valid BotDefinition.

- [ ] Match startup resolves the authoritative `RaceActual` and admits only a compatible executable composition.
- [ ] The selected race's worker/economy lifecycle is correct.
- [ ] The first legal supply and production prerequisites are built or satisfied.
- [ ] First-tier units are produced at the intended rate without invalid commands, avoidable supply blocks, or resource-reservation violations.
- [ ] Scouting tasks run independently of the main assault force and preserve contact freshness.
- [ ] The assault planner uses observed contacts and unit capabilities; it does not blindly attack the enemy start location.
- [ ] Movement, arrival, engagement, regroup, and retreat are observable in diagnostics and replay.
- [ ] No cross-race command leakage occurs under mismatched or missing race data.
- [ ] Required CI passes on the exact candidate SHA.
- [ ] A controlled local match/replay demonstrates the behavior for that race.

A green build is not match evidence. A race should be reported separately as **catalogued**, **composition implemented**, **regression-tested**, **CI-verified**, or **match-verified**.

## Current implementation record

The durable implementation checklist is [issue #14 — Three-race upgrade](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/issues/14). Preserve the working Terran baseline while progressing the Zerg and Protoss compositions. Do not change ServerSC2, promote branches, publish packages, release, deploy, or delete branches as part of this work without explicit authorization.
