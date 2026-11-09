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

## Integration boundary
These utilities are additive and protocol-independent. Existing sector behavior is untouched. Splash-aware formation spacing and doctrine-weighted platform assessment are documented in [Tactical spatial reasoning](tactical-spatial-reasoning.md). Neither the original geometric queries nor these new utilities are yet wired into live movement FSMs or the action translator. Future work can add fog-aware influence maps, terrain/elevation extraction, path costs, collision, weapon ranges and threat fields without changing the basic vocabulary.