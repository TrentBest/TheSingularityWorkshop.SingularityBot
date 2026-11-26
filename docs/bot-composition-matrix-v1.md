# Bot composition matrix v1

## Purpose

A bot definition describes the participant's identity and its available race policy. A composition matrix describes the concrete command composition selected after the participant's race and the opponent's race are known.

These are separate concerns:

- **Identity**: stable bot ID, display name, creator metadata and presentation.
- **Race policy**: Fixed, Random or a trusted custom selector. Random is not a playable race.
- **Matchup key**: one concrete own race and one concrete opponent race.
- **Hero selection**: the strategic execution authority for that own-race matchup.
- **Command composition**: subordinate modules and command layers delegated to by the hero.

The matrix is participant-scoped. It is not a MatchDefinition or LadderDefinition and must not absorb map, player-slot, team, scoring or tournament rules.

## Matrix dimensions

The initial supported authoring dimensions are:

| Own race | Opponent Terran | Opponent Zerg | Opponent Protoss |
|---|---|---|---|
| Terran | Terran vs Terran | Terran vs Zerg | Terran vs Protoss |
| Zerg | Zerg vs Terran | Zerg vs Zerg | Zerg vs Protoss |
| Protoss | Protoss vs Terran | Protoss vs Zerg | Protoss vs Protoss |

There are exactly nine concrete matchup keys. The Forge top-level Random tab is a view over the race-specific configurations; it does not add a fourth row or column.

## Versioned contract

The v1 matrix is a separately versioned object with these conceptual fields:

- `version`: integer contract version, initially 1.
- `entries`: exactly one entry for each concrete own-race/opponent-race pair.
- `ownRace` and `opponentRace`: concrete playable race identifiers only.
- `hero`: required stable `implementationId` plus independent `presentationProfileId`.
- `layers`: ordered, declarative subordinate command-layer selections. Reuse the existing CommandLayerSelection contract and its intent/capability declarations.
- `settings`: versioned data interpreted only by a registered implementation; never executable code.

Hero implementation and presentation are deliberately independent. For example, a Zerg-vs-Zerg entry may select a Kerrigan-themed presentation profile and a registered Zerg strategic-controller implementation, while Zerg-vs-Terran and Zerg-vs-Protoss select an Overmind-themed profile and a different registered implementation. Names, lore and artwork do not grant runtime capability.

A hero is the strategic authority for its race composition. It is not merely another label for the first tactical command layer. It owns strategic decisions and delegates typed intent to the selected subordinate layers. Existing Strategic/Army/TaskForce/Platoon/Squad/Unit layer kinds remain reusable below that hero.

## Example intent (illustrative, not an executable catalog)

```json
{
  "version": 1,
  "entries": [
    {
      "ownRace": "Zerg",
      "opponentRace": "Zerg",
      "hero": {
        "implementationId": "hero.zerg.swarm-commander.v1",
        "presentationProfileId": "hero.zerg.kerrigan"
      },
      "layers": []
    },
    {
      "ownRace": "Zerg",
      "opponentRace": "Terran",
      "hero": {
        "implementationId": "hero.zerg.overmind-strategist.v1",
        "presentationProfileId": "hero.zerg.overmind"
      },
      "layers": []
    }
  ]
}
```

The abbreviated example intentionally omits the other seven entries and is not a valid complete matrix. The implementation IDs above are illustrative identifiers, not a claim that those hero implementations already exist. A complete valid matrix must contain all nine pairs and resolve every selected implementation against the trusted compiled capability catalog before match admission.

## Unknown opponent race

An unknown, random, unresolved or unsupported opponent race is not one of the nine concrete keys. v1 does not guess, reuse the last matchup, or silently select a default hero. Match admission must remain pending or fail closed until authoritative opponent race information is available. A future explicit fallback policy can be added as a separately versioned capability; it must not weaken exact-match resolution implicitly.

## Trust and execution

- Definitions select stable IDs; they never contain class names, scripts, reflection targets, arbitrary method names or executable expressions.
- Only implementations registered by the compiled host are executable.
- Presentation profiles are metadata only and cannot alter behavior.
- Every hero and layer must declare supported races, accepted/emitted intent contracts, required/provided capabilities and a versioned settings schema in its trusted catalog entry.
- Unknown IDs, race mismatches, missing capabilities, invalid settings and disconnected declared handoffs fail validation.
- A structurally valid matrix is not proof that its selected heroes or modules are implemented, sustainable or competitive.
- Runtime action authority remains behind the existing observation, reservation, legality, arbitration and validated-action pipeline.

## BotDefinition compatibility and migration

Do not silently add matchup semantics to BotDefinition v1 or reinterpret its existing `race`, `modeId`, `runtime` or `opening` fields. Existing v1 documents retain their current meaning and continue to select the existing single-race baseline.

The matrix is introduced as its own v1 contract first. Integration into the portable bot artifact requires an explicit BotDefinition v2 migration decision:

1. Define how a v1 single-race definition is upgraded (one selected own-race row populated from its existing mode/opening; other entries explicitly unconfigured, not fabricated).
2. Define whether v2 requires all nine entries to be authored or permits clearly marked unavailable entries.
3. Preserve round-trip fidelity and reject unknown properties.
4. Keep the existing Terran Marine definition as the regression baseline.
5. Do not enable Random eligibility for a race until its selected compositions resolve to implemented, validated runtime capabilities and pass controlled match/replay acceptance.

## Forge authoring behavior

- The first row selects Terran, Zerg, Protoss or Random policy view.
- Fixed-race tabs edit that race's three opponent entries.
- The Random tab exposes the same three race configurations as the possible resolved own-race outcomes; it does not duplicate or fork their data.
- The second row selects the opponent race and edits only that exact matchup entry.
- Changing one entry must not mutate another matchup's hero, layer list or settings.
- The UI must distinguish registered/executable implementations from illustrative, unavailable or presentation-only choices.
- Import/export round-trips the complete contract. Browser validation is advisory; the host validator and capability resolver remain authoritative.

## Delivery gates

1. Contract and deterministic validation tests.
2. Trusted hero/module catalogs and exact matchup resolver.
3. BotDefinition v2 migration and runtime composition integration.
4. Forge nested tabs, independent editing, import/export and validation.
5. Controlled SC2 matches and replay evidence for each enabled composition.

Until a gate is met, show the capability as unavailable or experimental rather than implying it is ready.
