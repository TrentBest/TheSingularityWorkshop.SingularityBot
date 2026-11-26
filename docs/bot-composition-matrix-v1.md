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


## Editor/runtime model refinement (design decision; implementation pending)

The initial hero-plus-layers vocabulary is a useful first representation, but the editor must not make that vocabulary mandatory. The underlying model is a configurable command hierarchy with presentation-independent roles.

### Bot-wide configuration

Bot-wide settings are authored once and apply to every matchup unless a future, explicit override scope is introduced. Examples include:

- Cheese policy (enabled/disabled, with any strategy-specific parameters owned by a registered method).
- Worker scouting policy.
- Opening chat/message, such as `GLHF`.
- Race selection policy and other participant-wide identity/presentation metadata.

These are data, not executable behavior. Settings must be typed, versioned, validated, and owned by registered setting definitions. A setting's editor label and control (dropdown, toggle, text field, slider, etc.) come from metadata; the serialized key and value semantics remain stable.

### Bot methods and behaviors

Bot methods are the registered behavior/capability catalog: the things the bot can do. A method can expose bot-wide behavior, commander behavior, subordinate behavior, or unit-level behavior according to its declared scope and capabilities.

Behavior transitions are a candidate use for the existing FSM/event model. They should be represented as explicit, validated state/transition definitions and invoke only trusted registered methods. Do not serialize arbitrary code, reflection names, or user-provided executable expressions into a bot artifact.

### Configurable command hierarchy

The command chain is a tree, not a fixed list of military ranks:

- Supreme commander layer: one or more primary strategic authorities.
- Subordinate commander layers: zero or more additional levels, with one or more commanders/controllers at each configured level.
- Unit FSM layer: leaf execution controllers assigned to units or unit groups.

The bot definition carries an explicit hierarchy-depth setting (or an equivalent versioned hierarchy policy). The editor creates the configured number of levels and lets the creator assign registered commander/controller implementations to each level. A one-level hierarchy is valid; deeper trees are opt-in. Validation must check parent/child handoff contracts, supported scope, race compatibility, capabilities, and unique stable node IDs rather than relying on rank names.

A supreme commander and a subordinate commander are not different hard-coded classes merely because of their titles. They are registrations of the same general commander/controller concept with different declared data, scopes, capabilities, and permitted responsibilities. The runtime enforces those capability boundaries. A commander may coordinate, delegate, or issue intents only within its registered authority.

The earlier singular `hero` field should therefore be treated as the initial UI/presentation concept for a primary commander, not as a permanent cardinality restriction. The next contract revision must support one-or-more primary commanders if the selected runtime policy permits it. The nine matchup entries remain independently configurable.

### Presentation and reskinning

The editor presents semantic roles through selectable labels and profiles. For example, a primary Zerg commander could be shown as “Hero”, “Overmind”, “Hive Mind”, or “Supreme Commander”; subordinate nodes could use “Generals”, “Broodmothers”, “Commanders”, or creator-defined terminology. These are presentation profiles over the same stable data and execution contracts.

Changing a label, theme, icon, lore description, or hierarchy vocabulary must not change serialized behavior, implementation identity, authority, or runtime capability. Conversely, changing the implementation selection is a real composition change and must be validated.

### Contract consequences

The current `BotCompositionMatrix` code is only a structural nine-matchup baseline. It does not yet implement bot-wide settings, bot methods, a variable-depth commander tree, multiple primary commanders, editor-provided role vocabularies, or unit-FSM leaf assignments. Do not pretend the current flat `Hero + CommandLayerComposition` shape already satisfies those requirements.

Before runtime catalog/resolver work proceeds, define a separately versioned composition contract that includes:

1. Bot-wide typed settings and identity/presentation metadata.
2. Registered bot methods/behaviors and their allowed scopes.
3. Nine independently authored matchup configurations.
4. A configurable command-tree depth and one-or-more primary commander nodes.
5. Zero-or-more subordinate commander nodes per parent, with explicit typed handoffs.
6. Unit-FSM leaf assignments.
7. Presentation profiles that rename/reskin semantic roles without altering runtime semantics.
8. Deterministic validation, trusted implementation resolution, serialization round-trip, and a migration path that leaves BotDefinition v1 unchanged.

The current nine-matchup implementation and its green CI remain a useful checkpoint, not the final composition schema. Do not build a hero-only catalog or wire the current matrix into BotDefinition v1 while this contract revision is being designed.


## Doctrine artifact v2 implementation checkpoint

The runtime contract is now being implemented as a separate data-only artifact rather than changing BotDefinition v1.

Implemented source files:
- `src/SingularityBot/Behavior/BotDoctrineDefinition.cs`: strict JSON parse/serialize, typed bot-wide settings and editor metadata, presentation profiles, service requirements, event-to-method bindings, threshold policies, ordered per-race initialization plans, nine matchup entries, configurable-depth commander trees, and unit-FSM leaves.
- `src/SingularityBot/Behavior/BotDoctrineRuntime.cs`: trusted compiled method/controller/service catalog, exact-matchup resolver, capability checks, and bounded deterministic event dispatch.

The validator rejects unknown schema versions, incomplete or duplicate matchup matrices, malformed typed settings, duplicate IDs, invalid initialization dependencies/order, invalid hierarchy depth, non-commander roots, non-leaf unit FSMs, invalid scopes, and incompatible declared parent/child intent handoffs. Runtime resolution additionally fails closed when required services, methods, or controller implementations are not registered by the host.

This is the first runtime-contract slice, not yet a completed in-game doctrine integration. It does not replace the existing observation/action pipeline, instantiate SC2 controllers, implement a Terran brigade/squad doctrine, implement Zergling swarm micro, or expose the new model in the Forge UI. The existing BotDefinition v1 and Terran runtime remain unchanged. The next gate is CI verification followed by integration with the actual match lifecycle and existing command/observation systems; only then should race-specific controller implementations be added.
