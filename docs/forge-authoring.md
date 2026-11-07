# Forge authoring workspace

Forge is the beginner-facing authoring and inspection surface in `src/SingularityBot.Gui`. The current WPF slice supports editing a local declarative draft without depending on the headless runtime.

## Authoring hierarchy

- Bot identity and race
- Strategy phases, objectives and priorities
- Groups/squads with membership selectors, shared objectives and coordination policies
- Unit profiles with catalog-pinned defaults and explicit overrides
- Reusable behavior references
- Behavior rules that refer to runtime-published facts and candidate domain intents
- Global constraints (represented in the draft model; richer editing follows)

The group owns coordination and a shared objective; each member retains its own unit profile. The commander sets intent, groups coordinate, and units execute through runtime-owned behavior.

## Current editable slice

The desktop shell can add/edit phases, groups, reusable behaviors and rules; edit bot identity and unit profile references; set the Marine retreat threshold override; and save/load JSON drafts locally. The included Terran sample is synthetic and deliberately labels all proposed catalog/fact/intent references as provisional.

## Contract boundaries

The GUI draft types are an authoring workspace model, not a claim that the runtime owner has finalized the serialized BotDefinition contract. Runtime-owned catalog identifiers, validation diagnostics, resolved defaults, snapshots, map documents and action intents must be versioned and supplied through contracts. The GUI's integration interfaces are seams, not implementations of the bot runtime.

Static map documents and dynamic match snapshots remain separate. The runtime must capture and normalize the exact map data used by a live SC2 session, and Forge must display that same document identity for live runs/replays. Forge does not decode SC2 protocol messages or map archives.

## Approval and safety

- Local save stores a draft; it does not apply it to a running bot.
- LLM output is a proposal with a base-definition hash, rationale and patch.
- Forge must present a scoped diff and require explicit approval before saving or applying a proposal.
- Runtime validation remains authoritative and may reject an approved proposal.
- No runner controls are exposed until real command and acknowledgement contracts exist.

## Remaining implementation

Authoritative catalog resolution and semantic validation; structured diagnostic presentation; authored-versus-resolved diff; proposal review and approval; typed constraint editor; map workspace/provenance; read-only runtime inspector and replay; AnyApp experience integration. Do not substitute fabricated data while live map capture is unavailable.