# Spatial language: Side, Quadrant and Sector

## Purpose

The bot needs a shared spatial vocabulary before it can make tactical choices. The vocabulary is layered: each world position can be assessed at strategic, regional and local scales without maintaining three unrelated maps.

- **Side** answers whether a position is on our half, the enemy half, or near the dividing line.
- **Quadrant** answers which broad quarter contains a position.
- **Sector** answers which cell of the 3x3 tactical grid contains a position, and which cells touch it.

These are cached map geometry, not observations of who controls an area. A sector can be friendly-side, enemy-side, contested, empty, or unknown depending on current intelligence.

## Geometry contract

The playable rectangle comes from SC2 game information, not an assumed map origin or hard-coded map size. Its center is the midpoint of the playable bounds.

### Side

Side classification uses the two starting anchors. The perpendicular bisector between those anchors is the dividing line. Points on the friendly side classify as `Friendly`; points on the enemy side classify as `Enemy`; points within a small configurable tolerance of the divider classify as `Neutral`.

The side axis is derived from the actual spawn direction, so the model does not assume every map is divided left/right or bottom/top. `SpatialMap.Center` is the geometric center of the playable rectangle; the side divider origin is the midpoint between the spawn anchors. They are intentionally distinct.

For standard two-player maps, the bot's start anchor is resolved from its own initial units and the remaining start location is the opponent candidate. Maps with more than two plausible opponent starts must retain multiple candidates until scouting supplies evidence; the bot must not invent an enemy base.

### Quadrants

The playable rectangle is split at its geometric center into South-West, South-East, North-West and North-East. Quadrants provide broad context and aggregation. They are not navigation waypoints and do not imply control.

### Sectors

The playable rectangle is split into 3 columns by 3 rows. Columns run west-to-east; rows run south-to-north. Each sector exposes stable `(column,row)` identity, world-space bounds and center, eight ordered perimeter points suitable as patrol-route seeds, and up to eight neighbors (or cardinal neighbors only).

Sector lookup rejects positions outside playable bounds rather than silently assigning them to an arbitrary sector.

## Intelligence built on the geometry

The spatial model is immutable for a match and should be built once from map metadata and resolved spawn anchors. Per-observation intelligence is separate and refreshable:

1. Normalize visible units and known remembered contacts into domain snapshots.
2. Assign each snapshot to its current sector, quadrant and side.
3. Build sector occupancy indexes for friendly units, visible enemies, and remembered-but-not-currently-visible contacts separately.
4. Query the unit's own sector first, then neighboring sectors, then its quadrant, then strategic side-level context.
5. Keep visibility and confidence attached to every enemy fact. Fog of war is not evidence that a remembered enemy has been destroyed.

This ordering makes local decisions cheap while retaining broader context. It is a search/evidence order, not a rule that a local contact always outranks a strategic objective.

## Engagement evaluation

Spatial awareness supplies candidates; it does not decide that a fight is safe by itself. A combat evaluator should compare estimated effective damage over a common time horizon: our expected damage after target armor, shields, range, weapon cooldown, accuracy and target availability; enemy expected damage after our armor, shields, movement, range and exposure; effective health remaining on both sides; reinforcement arrival, focus-fire concentration, retreat paths and terrain.

A conservative first gate can require both (a) our expected effective damage to exceed the enemy force's effective health over the chosen horizon and (b) our effective health to exceed expected incoming damage. This is a heuristic, not a complete combat model: target selection, overkill, splash, healing, upgrades, elevation, cover, kiting and reinforcements must be included before treating it as reliable prediction.

Chokepoint behavior is a separate strategic constraint. If the enemy mass is crossing a choke, the bot can evaluate striking the exposed portion without committing its entire force through the choke. After the engagement, the commander can choose a different approach sector—such as the opposite side of the enemy base—instead of blindly continuing through the same choke.

## Current implementation boundary

The first protocol-independent geometry implementation lives in `SingularityBot.Spatial.SpatialMap`. It provides map bounds, center, side classification, quadrants, 3x3 sectors, sector perimeter points and neighbor queries. It does not yet build unit occupancy indexes, infer map control, plan routes, or issue combat actions. Those belong in the next observation/intelligence layer and must be validated against real SC2 observations.


## Town-Hall-relative base geometry

Strategic map quadrants and the local geometry of a base are different coordinate systems. The former remain fixed to the playable rectangle; the latter are attached to an individual Command Center and rotate with that base.

The primary base frame is anchored at the Town Hall. The Town-Hall-to-map-center vector is the initial facing estimate, not a final claim about the ramp. Resource and terrain geometry refine it. Once the ramp is identified, rotate the local frame so **Quadrant I is the ramp-facing quadrant**. The other three quadrants follow in a consistent clockwise/counter-clockwise convention documented by the frame implementation. Never infer a local quadrant from world north/east alone.

### Mineral axis

1. Collect the mineral-field centers associated with this Town Hall.
2. Sort deterministically by world X, then Y. Use the extreme endpoints to seed the mineral-line axis and midpoint.
3. For each plausible additional mineral patch, construct a candidate line through the midpoint with the proposed length-4 probe. Evaluate nearby tile centers on both sides and fit the line to all patch centers; this avoids assuming that every mineral patch is perfectly collinear.
4. Retain the fitted axis, perpendicular, endpoints, residual/error, and confidence. Degenerate or widely scattered clusters remain unresolved rather than being forced into a line.

The length-4 probe is a geometric seed, not a hard-coded final mineral-line length. The final extents come from the actual patch positions and adjacent-tile checks.

### Gas axis and defensive coverage

When two geysers are associated with the base, construct the gas line through their centers and retain its midpoint and direction. Compare the gas and mineral axes and compare their midpoints in the Town-Hall frame. This classifies the gas line as same-side, opposite-side, or crossing/ambiguous relative to the mineral line.

Bunker placement is then an optimization, not a fixed rule: compare worker travel, Marine coverage, approach lanes, threat exposure, and whether one bunker can protect both gas approaches. A second bunker is justified only when its incremental defensive value exceeds its mineral and build-time opportunity cost. Bunkers may also be evaluated as emergency SCV shelter, but evacuation capacity and unload safety must be included in the score.

### Ramp detection and local quadrants

A ramp candidate is a connected region of cells that are pathable but not buildable, adjacent to a meaningful terrain-height transition, and connected to the base's traversable ground. A single unbuildable/pathable cell is not enough: cliffs, destructibles, map decorations, and narrow pathing artifacts can produce false positives.

Score candidate regions using:
- connected width and length;
- terrain-height delta across the region;
- pathing continuity from the Town Hall's ground region to the outer region;
- adjacent buildable cells suitable for a wall;
- alignment with the Town-Hall-to-map-center estimate;
- whether a ground unit can traverse the proposed Depot/Barracks wall in a local match.

The best-supported ramp direction defines local Quadrant I. If multiple ramps or candidate regions remain plausible, preserve multiple hypotheses and do not place a supposedly sealed wall.

### Expansion frames

Every expansion Town Hall receives its own local frame. Do not copy the primary base's world-space rotation or quadrant labels. Derive the new frame from its own mineral and geyser clusters, local pathing/elevation, and the Town-Hall-to-map-center estimate; then identify its own ramp/approach direction. The primary base frame is a template for the algorithm, not a coordinate transform to reuse blindly.

### Confidence and evidence

Keep source observations, fitted lines, residuals, terrain evidence, candidate scores, and unresolved alternatives. Geometry proposes sites; SC2 placement queries establish build legality; pathing checks and controlled matches establish traversal and defensive value. A legal building placement does not prove a wall is sealed.
