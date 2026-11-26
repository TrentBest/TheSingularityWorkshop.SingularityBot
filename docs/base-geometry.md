# Base geometry and resource-line inference

## Purpose

Build a reproducible geometric description of each Terran base before asking the bot to place Depots, Barracks, Bunkers, or a flying Command Center. All positions are derived from observed resources and SC2 map grids; no map-specific offsets or assumed spawn rotation are accepted as truth.

## Coordinate frames

The strategic map frame remains world-aligned and continues to provide playable bounds, map center, strategic side, map quadrants, and sectors.

Each Town Hall also owns an independent local frame:
- origin: the observed Town Hall center;
- initial facing estimate: normalized vector from Town Hall to playable-map center;
- refined facing: local ramp/approach direction inferred from pathing and terrain;
- local axes: forward toward the ramp and perpendicular lateral;
- local quadrants: Quadrant I is always the ramp-facing quadrant.

The Town-Hall-to-map-center vector is useful as a prior and as a deterministic tie-breaker. It is not sufficient to identify the ramp by itself. At an expansion, create a new frame from that Command Center's own terrain and resources; never reuse the main base's rotation.

For a world point p and Town Hall H, with unit forward f and lateral l = (-f.y, f.x), local coordinates are:
- forward = dot(p - H, f)
- lateral = dot(p - H, l)

The implementation must make the handedness and quadrant numbering explicit and test it under all rotations/reflections.

## Mineral-line construction

1. Associate visible mineral fields with the Town Hall using distance, path connectivity, and competing Town Hall/resource clusters. Do not assign a patch merely because it is the nearest one if another base is a plausible owner.
2. Sort associated mineral centers by X and then Y for deterministic ordering.
3. Use the two extreme patches as the initial line endpoints and their midpoint as the first center estimate.
4. Choose additional patches and construct candidate probe lines through the estimated midpoint. The user's length-4 probe is the initial geometric sample; test adjacent tile centers on both sides to estimate the actual occupied range and orientation.
5. Fit/refine the line against all associated patch centers. Retain endpoints, midpoint, unit direction, perpendicular, residual, and confidence.
6. If patches form multiple clusters, a curved/irregular arrangement, or an ambiguous ownership boundary, retain multiple candidates or mark the line unresolved.

The X/Y sort establishes stable endpoint selection, but it must not be mistaken for the geometric axis on maps where the mineral arc is diagonal or irregular. Candidate fitting and residual checks decide whether the line model is trustworthy.

## Gas-line relationship

For two associated geysers, create a line through their centers and retain its midpoint and direction. Express both the gas and mineral lines in the Town Hall's local frame. Classify the gas arrangement as:
- same-side: both geysers lie on the same side of the mineral-line midpoint/axis;
- opposite-side: geysers straddle the mineral axis or occupy opposing lateral regions;
- crossing/ambiguous: line intersection or geometric uncertainty makes a binary classification unsafe.

This is a descriptive feature, not a bunker placement command. Evaluate one-bunker and two-bunker plans using SCV travel distance, Marine coverage, approach lanes, likely enemy routes, bunker construction opportunity cost, and the value of emergency SCV shelter. Prefer one bunker only when it provides adequate coverage to both gas approaches; prefer two only when measured incremental protection justifies the cost.

## Ramp inference

Use the raw SC2 pathing, placement, and terrain-height grids. A ramp hypothesis is a connected set of cells that:
- is pathable;
- is not buildable;
- borders cells with a meaningful elevation change;
- connects traversable regions on both sides of the height transition.

Filter false positives using connected-component width/length, height delta, local path connectivity, and proximity/alignment to the Town Hall. Candidate wall tiles must be adjacent to the ramp region and individually validated by SC2 placement queries. A placement query confirms legal construction, not that the wall blocks enemy ground movement. The final Depot/Barracks wall must be tested with ground-unit traversal in a local match.

If the grid evidence supports multiple ramps or no confident ramp, do not invent Quadrant I or claim the wall is sealed. Keep the candidate set and confidence visible to diagnostics.

## Expansion and Command Center landing

A Command Center expansion gets a new local frame after landing. Candidate sites are scored from mineral/gas value, worker path length, terrain/buildability, landing footprint clearance, path connectivity, enemy exposure, and proximity to defensive support. The home-built Command Center may be lifted and flown only after the destination footprint is revalidated. The SCV crew transfer is a separate transaction: load, verify cargo, fly, revalidate, land, verify landed structure, unload, and assign workers to the new resource cluster. Failure at any stage retains a safe recovery state.

## Diagnostics and validation

Persist for each base:
- Town Hall tag and world position;
- resource ownership candidates and confidence;
- sorted mineral and geyser coordinates;
- mineral/gas line endpoints, midpoint, direction, residual, and relationship;
- initial and refined facing vectors;
- ramp candidate regions, pathing/buildability/elevation evidence, and confidence;
- local quadrant transform;
- bunker alternatives and score components;
- rejected candidate reasons.

Unit tests cover translation invariance, rotation invariance, reflection, stable X/Y ordering, collinear and irregular mineral layouts, missing/one/two geysers, degenerate lines, multiple ramp hypotheses, and ambiguous evidence. Integration tests compare inferred terrain features against SC2's actual grids. Local matches verify that a wall blocks ground units and that bunker plans protect the intended workers. No geometry score alone is evidence of in-game success.


## First-match calibration: guess, observe, correct

For the current target map, do not block the first usable wall attempt on perfect ramp classification. Use the Town-Hall-to-map-center vector as a directional prior, choose a plausible ramp-facing direction, and issue a first candidate layout. The first match is a calibration run.

For every attempted structure, record:
- map identity and playable bounds;
- spawn/Town Hall position and inferred facing vector;
- structure type and footprint dimensions;
- requested placement coordinate and the coordinate reported by the observation after construction;
- whether SC2 accepted the placement and whether the structure completed.

When the observed structure position differs from the requested position, calculate the tile correction:

`dx = observedX - requestedX`

`dy = observedY - requestedY`

Apply the inverse correction to subsequent candidate placements:

`correctedX = candidateX - dx`

`correctedY = candidateY - dy`

This is a measured placement-origin/map-grid correction, not proof that the ramp direction itself was correct. Keep those two error sources separate: first compare the requested and observed footprint centers to calibrate coordinate interpretation; then assess whether the resulting layout is actually aligned with the ramp. Do not silently fold a directional mistake into a global coordinate offset.

Store calibration by map identity, spawn orientation, and structure/footprint convention. A calibration from one spawn or footprint must not be applied blindly to another. Use SC2 placement queries for every corrected candidate. After the first wall is built, inspect its relationship to the ramp and run a ground-unit traversal test; a visually plausible wall is not proof of a sealed wall.

The operator's first-match feedback should be tile-based: report the requested coordinate, observed coordinate, and signed X/Y difference. One correction at a time makes the next run interpretable. If the first placement is accepted and observed at the requested coordinate, the coordinate transform needs no offset correction; any remaining miss is a ramp-direction or layout-geometry error.
