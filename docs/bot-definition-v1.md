# BotDefinition v1 — the bot's portable DNA

## Purpose

`BotDefinition` is the canonical, versioned authoring artifact for one bot. It is the source from which Forge, a text editor, an LLM-assisted authoring session, or a developer API can construct the same bot. It composes existing SingularityBot concepts instead of introducing a second scheduler or state-machine runtime.

The first implementation deliberately targets the current Terran runtime. The schema is versioned so additional races, team formats, and capability families can be introduced without pretending they are implemented today.

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
| Runtime | Enabled compiled advisors and their settings | `BotManifest` |
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

## Running an authored BotDefinition

The runner accepts a validated v1 JSON definition through `--bot-definition`. For a local match:

```powershell
dotnet run --project src/SingularityBot.Runner -- --bot-definition=examples/bot-definitions/terran-adaptive-marine.v1.json
```

For ladder mode, pass the same option alongside the SC2 ladder connection arguments:

```powershell
SingularityBot.Runner.exe --bot-definition=C:\\Bots\\terran-adaptive-marine.v1.json --GamePort 5000 --StartPort 5001 --OpponentId opponent
```

The runner parses and validates the file before connecting. The selected definition's `DefaultGoals` are now the live policy consumed by the existing `GoalActionPlanner`; `Runtime` is exposed as the bot's selected manifest, and `Opening` is exposed for the production adapter. The opening plan is **not yet the active SC2 production scheduler**: its observation adapter still needs game-data recipe wiring and integration with the existing action-arbitration pipeline. This distinction is deliberate; a loaded plan must not be mistaken for executed build orders.

## Current implementation boundary

The v1 aggregate C# contract, semantic validator, JSON schema, example definition, and bounded HeadlessAi consultation coordinator are implemented and regression-tested. The coordinator is not yet invoked by the live bot's `OnStart` or `OnFrame`; no network request is made from the frame heartbeat. A host must explicitly construct it with a configured HeadlessAi agent, create a visibility-safe snapshot and allowlist, and schedule consultations outside the synchronous frame path.

GrammarAi knowledge-graph construction, ProtocolAi stable symbol catalogs, and CommandAi proposal dispatch remain the next integration seams. They should be adopted only with bot-owned schemas and stable identifiers, not as generic dependencies added for decoration. No live ladder performance is claimed.
