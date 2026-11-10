# Spatial reasoning foundation

Spatial reasoning supplies reusable geometric measurements. It does not decide which relationship is strategically desirable. Higher layers can request own/opponent side, a quadrant, a 3x3 cell, front/back/left/right of a unit, or candidate positions that maximize enemies behind.

## Coordinate frames
- World/map coordinates use authoritative SC2 positions and known map bounds.
- Battlefield frame starts at our start location and points toward the opponent start, so side classification is independent of spawn orientation.
- Unit-relative frame uses an explicit facing vector; facing must not be guessed when unavailable.
- The normalized 3x3 grid uses map axes: columns left-to-right, rows bottom-to-top. Out-of-bounds points clamp to edge cells.

## Implemented pure utilities
- SpatialPoint vector operations, normalization, dot product and distance.
- BattlefieldFrame side, forward/lateral distance, and relative bearing.
- MapBounds quadrant and 3x3 classification.
- CountEnemiesBehind and FindPositionWithMostEnemiesBehind. Rear-half-plane membership is a strict negative dot product. Candidate ties prefer the shortest movement, then stable input order.

The enemy-behind score is geometric only. It does not imply line-of-sight, pathability, safety, weapon range, or tactical value. Callers must supply legal/pathable candidate positions and can combine this measurement with threat and path costs.

## Seven-layer composition
This is a shared spatial capability, not an eighth strategic layer. A high-level objective expresses intent; tactical planning asks spatial questions; a movement variant executes the chosen destination; the existing action pipeline validates and submits commands.

## SC2 terrain snapshot
`Sc2SpatialTerrainFactory` decodes the `StartRaw` pathing, placement, and terrain-height image layers supplied in `OnStart`. The resulting immutable `SpatialTerrain` is cached by the bot for the match. Each layer retains its own dimensions and is sampled in world coordinates, so differing image resolutions do not require resampling. Missing or malformed layers remain unavailable; they are never silently interpreted as blocked terrain.

Boolean pathing and placement samples are normalized to 0/1 whether the source image arrives as packed one-bit data or expanded eight-bit values. Terrain-height samples remain their original byte values. The factory maps each image across the full `MapSize` world extent; playable bounds remain a separate concept owned by `SpatialMap`.

### Terrain encoding evidence
The Blizzard raw protocol defines `StartRaw.terrain_height` as a one-byte bitmap, but does not specify its world-unit conversion. Two independent community implementations (`python-sc2` and `sc2ai`) decode raw samples with `height = -16 + (sample * 32 / 255)`, placing the represented terrain range at approximately -16 to +16 world units. This is stronger corroboration than a single example, but it is still implementation evidence rather than an explicit Blizzard raw-protocol guarantee. The separate feature-layer `height_map` is explicitly documented as [-200, 200] encoded to [0, 255]; that range must not be applied to `StartRaw.terrain_height`. Accordingly, the snapshot continues to preserve raw bytes and topology currently compares them only as encoded relative samples. A future calibrated conversion should be checked against live map samples and the in-game `TerrainHeight` query before it becomes a gameplay invariant.

The packed one-bit pathing/placement decoder currently reads bits most-significant-first within each byte. A community raw-image example uses the same ordering; the protocol comment specifies one-bit packing but does not itself state bit order. Keep this as a tested compatibility assumption until checked against a live map or the reference implementation.

## Implemented pathing topology
`SpatialTopology.Build` consumes the cached pathing layer and extracts deterministic four-connected walkable regions. Diagonal-only contact does not connect regions. Each region reports its cell count, optional minimum/maximum raw height sample, and directed grid-edge contours. Contours are expressed in world coordinates, retain closure state, and are oriented with the region interior on the left. With the bottom-left map origin, exterior contours therefore wind counter-clockwise and contours around enclosed blocked areas wind clockwise. The trace retains every grid-edge vertex (rather than simplifying collinear runs), which preserves the source cell-edge geometry for later edge-following and transition analysis. Enclosed blocked areas produce inner contours.

A pathing region is **not yet a platform**. Ramps, bridges and other elevation transitions can connect different surfaces inside one pathing component. Raw height bytes are useful as encoded relative samples, but this implementation does not assign world-unit elevations or invent a platform-height threshold. Platform segmentation, transition discovery, route counts, choke widths, safe movement paths and calibrated elevation advantage remain future work.

`TryIsPathable`, `TryIsBuildable`, and `TryGetTerrainHeight` distinguish an unavailable sample from a valid zero-valued cell. Grid layers are sampled independently, so differing image resolutions do not require resampling.

## Integration boundary
These utilities are additive and protocol-independent except for the narrow `Sc2SpatialTerrainFactory` adapter. Existing sector behavior is untouched. Splash-aware formation spacing and doctrine-weighted platform assessment are documented in [Tactical spatial reasoning](tactical-spatial-reasoning.md). The longer-term model for connected platforms, real boundary contours, persistent place state machines, role-specific tracks and contingency maneuvers is documented in [World model, platform doctrine and contingency planning](world-model-and-maneuver-planning.md).

Terrain snapshots and first-stage pathing topology are cached/derivable at match start. Platform segmentation, transition discovery, route measurements, place FSMs, vision revisit scheduling and contingency execution are not yet implemented. These capabilities can add fog-aware influence maps, path costs, collision, weapon ranges and threat fields without changing the basic spatial vocabulary.
