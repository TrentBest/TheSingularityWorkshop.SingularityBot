# Versioned map and match documents

Forge consumes protocol-independent JSON documents. SC2 protocol decoding and normalization stay inside SingularityBot; ServerSC2 remains authoritative for assignment, process lifecycle, adjudication and replay persistence. These documents are read-only visualization data and cannot authorize bot actions or declare a match complete.

## Static map: MapDocument v1

Schema: [`singularitybot-map-document-v1.schema.json`](../schemas/singularitybot-map-document-v1.schema.json)

A map document is immutable and contains:

- Stable `mapId`, display name, source and a SHA-256 `contentHash`.
- A declared 2D world coordinate system in SC2 world units; X increases east, Y increases north.
- Full-map bounds and playable bounds as separate rectangles. Both may have non-zero origins.
- Start locations in canonical ordinal-ID order.
- Independently described layers. Each available layer declares dimensions, world origin and extent, semantic meaning, encoding, units, compression, payload reference and SHA-256 digest. Unavailable/unsupported layers carry no payload digest and must never be fabricated.

Layer payload hashes cover the exact stored payload bytes (after compression, if any). Payload references are relative to the map document. The fixture's tiny terrain payload is hex text; its declared encoding makes that representation explicit. Terrain-height values remain raw bytes with uncalibrated physical units.

## Dynamic match: MatchSnapshot v1

Schema: [`singularitybot-match-snapshot-v1.schema.json`](../schemas/singularitybot-match-snapshot-v1.schema.json)

Each snapshot is a complete observation frame, not a delta. It binds to both `mapId` and the exact `mapContentHash`, and includes match identity, explicit `observerPlayerId`, the sorted `capturedFields` declaration, monotonically increasing observation sequence, game loop, lifecycle, player/economy facts, unit facts and authoritative dead-unit tags. `capturedFields` names the optional observation fields requested and populated by the normalizer; it prevents consumers from treating omitted values as zero or assuming that a field was captured. Unit alliance values (`self`, `ally`, `enemy`, `neutral`) are relative to that observer; the observer must appear in the player roster.

Contact semantics:

- `visible`: currently observed; position and captured values describe this frame.
- `remembered`: last known contact, with required `lastSeenGameLoop`; it is not current visibility.
- `unknown`: no current fact is asserted; position is omitted.
- Destruction is represented only in `authoritativelyDestroyedUnitTags`, sourced from explicit death evidence. Destroyed tags cannot also remain in the active/remembered unit array.

Absence from a snapshot is not destruction evidence. Unknown optional values are omitted rather than represented as zero. The v1 unit projection has explicit optional properties for health, shields, build progress, cargo/orders flags, mobility flags, weapon cooldown, collision radius, energy and Vespene contents; the mandatory health maximum remains part of the normalized baseline. Unit and player arrays are sorted by stable numeric identity; dead tags are unique and sorted. Orders retain their observed sequence.

`MatchSnapshotV1Factory` projects a `NormalizedObservation` plus explicit player metadata and retained `LastSeenContactV1` history; it binds the observer identity from the normalized observation. A contact omitted from the current observation remains remembered if live-visible history exists; that history is scoped to the exact match and observer player so alliance labels cannot leak across perspectives. Authoritative dead tags override both current and remembered contacts. Snapshot/hidden facts never invent their own last-seen timestamp. Visible units require captured position, so callers must include `ObservationFields.Position`; otherwise projection fails closed. `NormalizedObservation.CapturedFields` records which optional facts were actually captured.

## Deterministic JSON and hashes

The v1 .NET codec uses compact UTF-8 JSON, camelCase property names, declaration/schema property order, direct UTF-8 characters with JSON-required escaping, and omission of null properties. It does not write a BOM or indentation.

- Map `contentHash` is SHA-256 over the exact v1 serialized map document after setting `contentHash` to the empty string. The returned lowercase hex digest is then stored in that field.
- Layer `payloadSha256` is SHA-256 over the exact stored payload bytes.
- Match snapshots carry the map content hash; they do not redefine or recompute map identity.

Canonical array ordering is enforced by the codec: map starts/layers by ordinal ID; match players by player ID; units and destroyed tags by numeric tag. Non-canonical order and duplicate identities are rejected before serialization.

V1 is immutable. Incompatible shape or semantic changes require a new schema/version rather than silently changing the meaning of an existing field. Readers must reject unsupported schema versions and unknown enum values; optional fields may be absent, never guessed.

## Fixtures and current scope

- `examples/map-documents/translated-two-start.map.v1.json`: non-square full bounds, translated playable bounds, two starts, one payload-backed layer and one explicitly unavailable layer.
- `examples/match-snapshots/translated-two-start.frame-0001.snapshot.v1.json` and `frame-0002...`: a visible enemy becomes remembered with its last-seen loop; a separate tag is authoritatively destroyed.
- `tests/SingularityBot.Tests/VersionedDocumentSerializationTests.cs`: byte stability, map digest, round-trip, canonical ordering, fog retention and destruction/absence separation.

This first slice defines the DTOs, schemas, codec, synthetic canonical fixtures and `MapDocumentV1Factory`. The factory projects already-normalized `SpatialMap`/`SpatialTerrain` data and delegates payload storage; it does not decode SC2 protocol messages or assign map identity. Production SC2 ingestion/metadata wiring, real-map payload storage, match-frame capture, Forge live streaming and ServerSC2 integration remain subsequent work. Event/delta replay is deliberately deferred until full-snapshot reconstruction is proven deterministic.
