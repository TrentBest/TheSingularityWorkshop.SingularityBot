# Forge authoring workspace

Forge is the beginner-facing authoring and inspection surface. The initial desktop shell is in `src/SingularityBot.Gui`.

## Authoring hierarchy

- Bot identity and global constraints
- Strategy phases and objectives
- Groups with membership selectors, shared objectives and coordination policy
- Unit profiles with catalog-pinned defaults and explicit overrides
- Reusable behavior definitions
- Typed behavior rules that refer to runtime-published facts and candidate domain intents

The group owns coordination and a shared objective; each member retains its own unit profile. The commander sets intent, groups coordinate, and units execute through runtime-owned behavior.

## Contract boundaries

The GUI draft types are an authoring workspace model, not a claim that the runtime owner has finalized the serialized BotDefinition contract. Runtime-owned catalog identifiers, validation diagnostics, resolved defaults, snapshots, map documents and action intents must be versioned and supplied through contracts. The interfaces in the GUI project are integration seams, not implementations of the bot runtime.

Static map documents and dynamic match snapshots remain separate. Map provenance must distinguish live run capture, replay capture, offline import and synthetic fixture. Missing map layers remain unavailable; Forge must not substitute fabricated data.

## Approval and safety

- Local save stores a draft; it does not apply it to a running bot.
- LLM output is a proposal with a base hash, rationale and patch.
- Forge presents a scoped diff and requires explicit approval before saving or applying a proposal.
- Runtime validation remains authoritative and may reject an approved proposal.
- No runner controls are exposed until real command and acknowledgement contracts exist.

## Current status

The first shell contains a clearly marked synthetic Terran example. It is not connected to live map ingestion, runtime snapshots, catalog resolution, schema validation or runner commands yet. Those are explicit next integrations, not implied capabilities.