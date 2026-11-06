# Local Game Sessions and Human-vs-Bot Play

**Status:** architecture proposal; no interactive human-vs-bot session is implemented or verified yet.

## Purpose

SingularityBot starts with StarCraft II because it provides a concrete protocol, observation model, action model, replay format, and local execution path. The longer-term Workshop capability should not be defined by StarCraft II's protocol.

The goal is a reusable local game-agent host in which a human can play a private game against one or more locally hosted agents. This is distinct from tournament, matchmaking, ladder, or public-server deployment.

## Separate the game from the agent

The architecture should have four boundaries:

1. **Game adapter** — translates a specific game's observations, entities, actions, clocks, maps, and lifecycle into a stable Workshop-facing contract.
2. **Agent runtime** — evaluates observations and proposes intents/actions. FSM_API owns deterministic lifecycle and behavior transitions; strategy data supplies goals and constraints.
3. **Session host** — starts/stops local game processes, assigns player slots, connects human and bot participants, enforces session configuration, and records outcomes.
4. **Human input/presentation adapter** — preserves the game's native controls and presentation for the human. The bot must not impersonate or intercept the human's input path.

The game adapter must not leak game-specific protocol types into FSM_API, CommandAi, or NeuralNetworks.

## Session modes

| Mode | Human participant | Bot participant | Purpose |
|---|---:|---:|---|
| Local bot vs built-in AI | No | Yes | First end-to-end integration and deterministic smoke tests |
| Local bot vs local bot | No | Yes, two or more | Repeatable strategy evaluation and self-play |
| Local human vs local bot | Yes | Yes | Private interactive play; the key longer-term experience |
| Ladder/tournament | No | Yes | Separate deployment adapter and release gate |

Local interactive play must not be implemented as a disguised ladder mode. It should have its own launch configuration, local process lifecycle, player-slot assignment, connection security, and shutdown behavior.

## StarCraft II first milestone

The imported client currently has a RunSinglePlayer path that launches a local SC2 process, creates a local map, adds a participant and a computer opponent, joins as the bot, and steps the game. That is the first supported target to verify.

The current implementation does **not** establish that a human can join the second slot. The CreateGame setup and client lifecycle must be investigated against the installed SC2 client and protocol before claiming human-vs-bot support. The first task is to prove a local bot-vs-built-in-AI match and capture its complete lifecycle.

After that, investigate whether SC2 supports a reliable local human participant alongside the bot in the same private session. If the supported client workflow requires a lobby/UI handoff, make that an explicit host adapter rather than embedding UI automation in the bot.

## Future FPS / first-person game adapter

A first-person shooter is a separate game integration, not an extension of the SC2 protocol. The reusable agent runtime should consume a bounded, game-neutral observation and emit a validated action intent; an FPS adapter translates that intent into the target game's supported bot/plugin/mod interface.

The human remains a normal local player using native game controls. The bot receives only the observations and action capabilities granted by the local game adapter. No process injection, anti-cheat bypass, hidden memory scraping, or unauthorized input interception is part of this design. Prefer documented modding, scripting, server-plugin, or official bot APIs.

A practical first FPS experiment should use a game with an accessible local mod/plugin API and a private offline/custom match mode. Select the title only after checking its supported interfaces, licensing, anti-cheat restrictions, and local-host requirements.

## Where the Workshop fits

- **FSM_API:** match, player, unit/actor and behavior lifecycles; no game protocol dependency.
- **CommandAi:** translates high-level goals into candidate commands and validates their meaning against the game ontology.
- **GrammarAi / ProtocolAi:** vocabulary and protocol knowledge; not game execution engines.
- **Physics:** shared spatial and physical reasoning only where its contracts fit the target game.
- **DataWarehouse:** bounded match history, observations, replay-derived records and experiment data.
- **NeuralNetworks:** optional bounded inference for a policy or evaluator experiment. It is not the authority that decides permissions or bypasses action validation.
- **FSM_Foundry:** eventually owns workflow/session execution and lifecycle orchestration through source-backed provider contracts.
- **GUI / Visualize / FSM_Bot_Editor:** strategy authoring, session status, map overlays and inspection; never the authoritative game simulator.
- **CiAi / HeadlessAi:** build, replay regression, deterministic evaluation and candidate comparison.

Neural inference should remain an optional advisor until a deterministic FSM/strategy baseline exists. Compare it against the baseline on the same recorded observations and scoring rules.

## Safety and correctness gates

Before enabling any interactive mode:

- Validate every proposed action against the adapter's legal action set and current observation.
- Keep human input and bot action channels separate.
- Make player identity, ownership, visibility and action authority explicit.
- Enforce time, memory, action-rate and session-duration budgets.
- Record enough evidence to reproduce failures without storing unnecessary personal data.
- Make local-only mode explicit and fail closed if the host unexpectedly resolves to a public/ladder endpoint.
- Treat game disconnects, stale observations, invalid actions and partial startup as explicit session outcomes.
- Do not advertise human-vs-bot or FPS support until a real local session has been run and tested.

## Implementation order

1. Verify the existing local SC2 bot-vs-built-in-AI path.
2. Normalize observations and actions behind a game adapter boundary.
3. Add match/player/unit FSM lifecycles and action validation.
4. Add headless match fixtures, replay capture and deterministic regression.
5. Prove local bot-vs-bot/self-play.
6. Investigate and prove SC2 local human-vs-bot slot assignment.
7. Select an FPS with an official offline/custom-game extension API and build a separate adapter.
8. Only then evaluate optional neural policies against the deterministic baseline.
