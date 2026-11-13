# BotDefinition v1 — the bot's portable DNA

## Purpose

`BotDefinition` is the canonical, versioned authoring artifact for one bot. It is the source from which Forge, a text editor, an LLM-assisted authoring session, or a developer API can construct the same bot. It composes existing SingularityBot concepts instead of introducing a second scheduler or state-machine runtime.

The first implementation deliberately targets the current Terran runtime. The schema is versioned so additional races, team formats, and capability families can be introduced without pretending they are implemented today.

**Refactor status:** `ICompiledBotStrategy` is now the compile-time personality contract. The Runner selects the statically linked `AdaptiveMarineStrategy`; the shared engine consumes its validated `BotDefinition` and executable initial-goal composition. `BotDefinitionCatalog` remains typed data, not the strategy implementation. Regression tests prove that two compiled strategies can select different goal policies through the same engine and CommandAi action vocabulary. Most macro/tactical behavior remains in shared controllers, and objective/transition composition is the next contract expansion. JSON remains an authoring/build interchange format only; runtime JSON loading, external strategy files, reflection-based discovery and dynamic assembly loading are excluded.

## Bot, match, and ladder are separate scopes

Do not turn one BotDefinition into a tournament configuration file. Keep three versioned artifacts:

- **BotDefinition:** one participant's identity, compiled capabilities, opening, goals, strategy policies, and AI consultation policy.
- **MatchDefinition:** one game instance's map, game mode, player slots, team membership, race assignments, and match rules. It may reference multiple BotDefinitions.
- **LadderDefinition:** the competition's map pool, match format, rounds, pairing policy, scoring, tie-breaks, and permitted BotDefinition versions.

That separation lets the same bot compete in 1v1, 2v2, 3v3, FFA, and multi-team formats without copying its strategy definition. Team roles and shared objectives are match-scoped inputs; each participant keeps its own observations, authority, FSM runtime, and AI call budget. A future ladder host can compare many BotDefinitions while preserving a reproducible record of the exact bot and match versions used.

## What it contains

| Section | Responsibility | Existing owner |
|---|---|---|
| Identity | Stable bot ID, display name, race, strategy profile | SingularityBot |
| Runtime | Enabled advisor declarations and their settings (data, not executable strategy code) | `BotManifest` |
| Opening | Deterministic production sequence | `ProductionPlanDefinition` / `ProductionPlanRuntime` / FSM_API |
| Default goals | Default unit/army posture | `GoalActionPlanDefinition` |
| Strategy transitions | Evidence, hard guards, advantage threshold, evaluation horizon, cooldown | `StrategyTransitionEvaluator` |
| AI policy | Provider/model selection and strict consultation budget | Host adapter / SingularityBot policy |

The runtime manifest, opening, goals, and strategy transitions remain bot-domain data. The AI policy is an opt-in capability declaration; it does not itself make network requests.

## Validation and trust

Loading is fail-closed:

1. JSON parsing rejects unknown properties.
2. Schema version must be supported.
3. Runtime manifest is validated by the existing compiled-advisor manifest validator.
4. Opening is validated by the existing production-plan validator.
5. Default goals are validated by the existing goal-plan validator.
6. Every strategy transition is validated, including unique IDs and evidence/guard requirements.
7. AI budgets are bounded: at most one `OnStart` consultation and at most three in-match consultations.

A structurally valid definition is not a promise that every requested capability is implemented. A future capability registry must resolve every referenced strategy/advisor/action against the current bot build before the match is admitted.

No definition field contains C#, scripts, reflection names, arbitrary method names, shell commands, or executable expressions. Settings are data interpreted by a known compiled capability.

## Marine posture and offensive dispatch

The first Marine force keeps a dedicated ramp scout. The remaining squad assembles into its initial formation before dispatch. With the default `contain` goal role, the squad continues its distributed friendly-side patrol. Selecting `engage` explicitly changes the rally controller to advance the assembled squad toward the observed enemy start position; SC2 pathfinding owns movement, while the combat planner still requires live target observations and revalidates attack range before issuing an attack. The example definition selects `engage` with `scoutCount: 0` because the ramp scout is assigned by the match controller independently of goal-role scouting.

This is an initial assault posture, not a claim of a complete macro strategy. It does not yet adapt composition, choose attack timing from opponent strength, or guarantee a win. A local match and replay remain necessary evidence.

## Opening worker timing

The first Barracks builder is not dispatched when the Supply Depot merely starts. The opening waits until the Depot is observed complete and the full Barracks mineral cost is available after strategic reservations. Only then does it reserve an eligible SCV and request a legal placement; the cost and reserve are checked again before the build command. This overlaps worker travel with the final approach to the resource threshold without leaving the worker idle at a speculative build site.

The second-Barracks controller follows the same resource-before-dispatch rule and selects the closest eligible unreserved SCV. The fixed post-first-Barracks delay has been removed; construction is paced by observed prerequisites and the actual mineral reserve rather than an arbitrary game-loop timer.

## The AI consultation model

The model is a strategic adviser, not a per-frame controller.

- **OnStart (0 or 1 call):** receive the selected BotDefinition, map/match metadata, allowed capability vocabulary, initial strategic posture, and a compact snapshot. Return a bounded startup recommendation or no change.
- **In-match (0–3 calls):** consult only at explicit, configured triggers. Examples: newly observed enemy tech with no current answer, a strategic stalemate, or a completed scouting pass that materially changes the plan.
- **Between calls:** deterministic FSMs, advisors, production plans, spatial reasoning, and tactical controllers continue operating. The bot does not wait for an LLM on every game loop.
- **Timeout/failure:** retain the last validated plan and continue deterministic behavior. Never block the game heartbeat waiting indefinitely.
- **Budget:** enforce the call count in host-owned match state, not in the model prompt. Enforce minimum spacing, timeout, payload limits, and cancellation outside the model.
- **Output:** model returns a typed recommendation (candidate strategy, goal priorities, resource allocation intent, rationale, evidence references). The host resolves it against a fixed capability registry, validates current observations and hard guards, and either accepts, partially accepts, or rejects it.

The model does not get raw action authority. It cannot invent a SC2 ability ID, bypass action arbitration, create arbitrary FSM contexts, or dispatch commands directly.

### Example: "I have no answer to this"

At a consultation trigger the host builds a snapshot:

- Our current economy, production queues, supply, army composition, upgrades, and available resources.
- Enemy structures and units currently visible.
- Enemy entities remembered from prior sightings, with last-seen game loop and confidence.
- Map topology, safe routes, expansion state, threat estimates, and scouting coverage.
- Current strategy, committed goals, active hard guards, and feasible known counter-strategies.
- The exact allowed response vocabulary and the remaining consultation budget.

The question is framed as a constrained planning problem: *Given this state and these known capabilities, which viable path most improves our chance of winning, and what evidence would invalidate it?*

A good answer might propose preparing a defensive ground-tech transition, preserving a specific resource reserve, increasing scouting coverage, and delaying an unsafe attack. The bot checks whether the required evidence is fresh and observed, whether production can execute the transition, whether defenses remain covered, and whether the strategy is actually registered. If any hard condition fails, the recommendation is rejected or narrowed.

The model should explain uncertainty and alternatives. It must not convert a single old sighting into certainty or hallucinate unseen enemy state.

## Where the AI packages fit

### GrammarAi — the knowledge map and structured authoring layer

GrammarAi's `GrammarStructureDefinition` and `GrammarStructureInstance` are a strong fit for exposing a typed graph of bot knowledge: capabilities, goals, evidence types, strategy relationships, prerequisites, and constraints. This is a graph, not a prompt full of untyped prose. GrammarAi validates structure; SingularityBot validates StarCraft meaning and feasibility.

### ProtocolAi — stable meaning

ProtocolAi can give stable integer identities to owned concepts such as `enemy.structure.roach-warren`, `goal.defend-natural`, `strategy.ground-tech-defense`, and `evidence.last-seen`. It should be the vocabulary registry, not the provider transport or execution layer. Do not renumber published IDs or change their meaning.

### CommandAi — bounded recommendations

CommandAi is appropriate when recommendations need a generic, immutable, typed proposal envelope. Its action/parameter IDs must resolve through a bot-owned registry. Use high-level goal/strategy requests, never raw SC2 actions. A proposal is not authorization; the host validates it against the current match state.

### HeadlessAi — asynchronous provider exchange

The first provider seam is now implemented through `TheSingularityWorkshop.HeadlessAi`. `BotAiConsultationCoordinator` accepts an injected `HeadlessAiAgent`, constructs a bounded strategic prompt, applies the BotDefinition call budget and timeout, and parses a strict recommendation contract. A host configures the endpoint-specific adapter and supplies credentials through HeadlessAi's request-time header provider. The core does not store API keys or depend on a specific model provider.

The coordinator serializes consultations, enforces one startup call and at most three in-match calls, checks allowlisted triggers and minimum game-loop spacing, and consumes budget even when a provider request fails. Tests use a fixture HTTP handler; they do not make billable live-provider calls.

### CiAi — engineering evidence

CiAi can build and test BotDefinition compatibility, schema conformance, deterministic replay scenarios, and adapter regressions. A green build is not evidence of ladder strength; actual match/replay results remain a separate evidence tier.

## Suggested first Forge experience

1. Select a BotDefinition and see its identity, opening, advisors, goals, transitions, and AI budget.
2. Visualize the capability/evidence graph.
3. Edit one value, such as the minimum confidence for a scouting fact.
4. Show the exact semantic diff and validation errors.
5. Run deterministic scenario simulations against recorded observations.
6. Export the same versioned JSON artifact used by the headless runner.

Renderer and Visualize remain MicroBundle presentation surfaces. They are not required dependencies of the bot runtime.

## Compiling and running a BotDefinition

The live runner currently selects the typed data aggregate returned by `BotDefinitionCatalog.CreateDefault()`. The definition, advisor manifest, opening, goals, transitions, and AI consultation policy are constructed by static C# catalog code and compiled into the bot assembly. This is a compiled data catalog, not yet the replaceable compiled strategy implementation described by the target architecture.

The match runtime does not read a JSON strategy file, load a strategy assembly, or evaluate executable configuration. This keeps the ladder runtime deterministic and independent of filesystem access. The JSON schema and example remain useful as portable editor/import/export representations; they are not loaded by the live runner.

The current implementation path is:

```text
Static C# BotDefinitionCatalog
          |
          v
BotDefinition data aggregate
          |
          v
FSM_COS startup composition
(BotDefinition + CommandAi vocabulary)
          |
          v
Shared SingularityBot controllers and planners
          |
          v
Validated intent/action pipeline -> SC2 Runner
```

The target personality path is a separate, incomplete refactor:

```text
Forge / GUI / text authoring
          |
          v
Versioned authoring data
          |
          v
Validation + code generation / authored strategy implementation
          |
          v
Statically compiled user-specific strategy in SingularityBot.dll
          |
          v
Shared bot engine invokes the strategy through a typed contract
```

The SC2 Runner remains the host and frame driver. FSM_COS may resolve startup capabilities, but it is not the match execution engine; FSM_API supplies reusable context and behavior mechanics.

A definition is not executable merely because it is structurally valid. Every referenced capability must resolve against the compiled bot's capability catalog, and the existing observation/action pipeline remains the only route to SC2 commands.

## Current implementation boundary

The v1 aggregate C# data contract, semantic validator, JSON schema, example definition, and bounded HeadlessAi consultation coordinator are implemented and regression-tested. The replaceable compiled strategy contract and its integration into the live decision path are not yet implemented. The coordinator is not yet invoked by the live bot's `OnStart` or `OnFrame`; no network request is made from the frame heartbeat. A host must explicitly construct it with a configured HeadlessAi agent, create a visibility-safe snapshot and allowlist, and schedule consultations outside the synchronous frame path.

GrammarAi knowledge-graph construction, ProtocolAi stable symbol catalogs, and CommandAi proposal dispatch remain the next integration seams. They should be adopted only with bot-owned schemas and stable identifiers, not as generic dependencies added for decoration. No live ladder performance is claimed.
