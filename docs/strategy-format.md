# Strategy format

## Intent

A strategy is a declarative, versioned description of desired behavior. It is not a serialized C# object graph and it does not contain executable code.

The first schema is deliberately small. It captures identity, race, opening build intent, strategic goals, priorities, constraints and tactical triggers. It will expand only when a real engine feature consumes a field.

## Document rules

- JSON, UTF-8.
- Required `schemaVersion`; unsupported major versions fail validation.
- Stable textual IDs for authoring and review; the compiler maps them to compact runtime identifiers.
- Game data references canonical ontology IDs, never display names as machine keys.
- Time values use game loops or explicitly typed seconds; do not mix units.
- Unknown fields are rejected in strict validation unless a future schema version explicitly permits extensions.
- Strategy data never contains raw protocol requests or unrestricted code.

## Initial conceptual model

| Section | Purpose |
|---|---|
| metadata | Name, version, authoring notes and intended matchup |
| race | Player race |
| opening | Ordered build intents with prerequisites and timing windows |
| goals | Strategic outcomes the bot should pursue |
| priorities | Relative ordering when goals compete |
| constraints | Hard limits that must never be violated |
| tactics | Triggered behavior with explicit conditions and intent |
| scouting | Information requests and confidence/expiry rules |
| composition | Desired army composition and tolerance bands |
| evaluation | Metrics and scenario tags for testing |

## Machine-readable schema

The initial JSON Schema is available at [schemas/strategy.schema.json](../schemas/strategy.schema.json). GitHub Actions validates every strategy JSON file against it before building the runtime.

## Compilation pipeline

1. Parse JSON.
2. Validate schema version and required fields.
3. Resolve ontology references against the selected game-data version.
4. Validate build prerequisites and mutually exclusive intents.
5. Compile goals and tactical rules into FSM definitions.
6. Bind runtime services (observation, command, spatial query, memory).
7. Emit a diagnostic report before enabling the strategy.

A strategy compiler must explain validation failures with a JSON path and actionable message. It must not silently drop unsupported directives.
