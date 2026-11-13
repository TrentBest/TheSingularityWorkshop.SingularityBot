# 06 — A concrete scriptable bot

This is a **proposed authoring shape**, not a claim that the DSL/compiler/runtime is implemented. It demonstrates the desired experience.

## User intent

"Open with a safe Terran economy. Keep scouting. If the opponent shows early Roach technology, prepare a proportionate response, but don't overreact to one structure. Preserve worker production and defensive coverage."

## Illustrative definition

```yaml
bot:
  id: terran.adaptive.opening
  version: 1
  race: Terran
  modes: [1v1]

  opening:
    use: terran.safe-economy
    requireObservedCompletion: [SupplyDepot, Barracks]

  strategies:
    - id: standard.macro
      priority: 50
      adoptWhen:
        all: [OwnEconomyStable, ProductionCapacityAvailable]

    - id: anti_roach.preparation
      priority: 70
      adoptWhen:
        all:
          - EnemyStructureObserved:
              type: RoachWarren
              visibility: Direct
              maxAgeLoops: 96
          - EnemyRoachTechConfidence: ">= 0.75"
          - OwnDefensiveCoverage: ">= required"
          - CanAfford:
              builder: BansheeSupport
              withoutBreaking: [WorkerProduction, SupplyReserve]
          - ProductionPathAvailable: Banshee
      hardGuards:
        - CurrentObservationValid
        - NoContradictoryEnemyEvidence
        - ActionPipelineHealthy
      switching:
        minimumAdvantage: 0.15
        cooldownLoops: 224

  goals:
    - ProtectWorkers
    - MaintainProduction
    - ScoutEnemyTech
    - PreserveExpansionTiming
```

Numbers are illustrative, not calibrated SC2 thresholds.

## Runtime story

1. SC2 adapter normalizes observations.
2. Evidence layer records enemy structures with owner, visibility, source loop, and freshness.
3. Strategy evaluator builds context from authoritative facts.
4. Hard guards run first and cannot be traded away against a score.
5. Evidence identity, directness, confidence, age, and contradictions are checked.
6. Planner estimates feasibility and switching cost.
7. FSM_API-backed policy contexts evaluate on the strategy cadence.
8. Adoption activates a goal such as PrepareAntiRoachDefense.
9. Existing builder/economy systems determine whether a Banshee line is feasible.
10. Intents are arbitrated, validated against current observations/game data, then submitted to SC2.
11. Trace records why the transition fired and what actually happened.

## Package contributions

ProtocolAi can identify shared concepts; GrammarAi can constrain definition structure; the host resolves every reference. CommandAi can carry a bounded proposal such as increasing scouting priority. HeadlessAi can critique replays asynchronously. None can directly issue Train Banshee or declare that the enemy is attacking.

Without these packages, bot-owned JSON and a bot-owned validator can express the same intent safely. The tradeoff is maintaining those generic contracts yourself and limiting reuse across Workshop domains.

**The script defines desired behavior; the runtime proves what is currently possible.**
