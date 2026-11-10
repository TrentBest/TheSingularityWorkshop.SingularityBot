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

`TryIsPathable`, `TryIsBuildable`, and `TryGetTerrainHeight` distinguish an unavailable sample from a valid zero-valued cell. The byte values are exposed as SC2-provided cell facts; this layer does not yet calculate connected components, route counts, choke widths, height advantage, or safe movement paths.

## Integration boundary
These utilities are additive and protocol-independent except for the narrow `Sc2SpatialTerrainFactory` adapter. Existing sector behavior is untouched. Splash-aware formation spacing and doctrine-weighted platform assessment are documented in [Tactical spatial reasoning](tactical-spatial-reasoning.md). The longer-term model for connected platforms, real boundary contours, persistent place state machines, role-specific tracks and contingency maneuvers is documented in [World model, platform doctrine and contingency planning](world-model-and-maneuver-planning.md).

Terrain images are cached at match start, but connected-component extraction, real platform boundaries, transition discovery, route measurements, place FSMs, vision revisit scheduling and contingency execution are future implementation work. These capabilities can add fog-aware influence maps, path costs, collision, weapon ranges and threat fields without changing the basic spatial vocabulary.
