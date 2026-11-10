# Tactical spatial reasoning: spacing and platforms

The spatial foundation now includes two additional reusable services. They return geometric measurements and scores; they do not issue SC2 commands or choose strategic intent.

## Formation spacing against area damage

`FormationSpacing.Evaluate` treats each friendly unit as a possible impact center and reports the worst-case number of friendly units within a supplied splash radius. A formation is single-hit-only when every unit is farther than that radius from every other unit. The radius comes from the weapon/effect data selected by the caller; the service does not hard-code Siege Tank splash values.

`FindMostSeparatedPosition` chooses the candidate location farthest from the nearest other formation member. The caller must first filter candidates for pathability, map bounds, collision, threat, and movement feasibility. A tactical controller can use the score to spread a group, let a struck unit withdraw, and later rejoin when conditions allow.

## Platform assessment

`PlatformProfile` accepts measured:
- number of distinct access routes;
- elevation advantage;
- open-area fraction;
- tightest choke width;
- directness of the enemy approach.

Continuous features are normalized to [0,1]. `PlatformReasoning.Assess` combines them using caller-supplied doctrine weights. The included defensive Terran weights are an initial policy, not a claim that one weighting is universally optimal.

Direct approach is a weighted prior, not a guaranteed opponent behavior. The service does not discover routes, measure terrain, infer elevation, or identify choke points itself. SC2 pathing, placement, and terrain-height grids are now cached as `SpatialTerrain` from `OnStart`, providing the source data for those future measurements. Route connectivity, choke extraction, elevation comparisons, and platform cataloguing are still unimplemented.

## Bridge doctrine and seven-layer composition

A doctrine can use platform assessments to select likely approach routes and defensive platforms. It can then assign observation/fallback roles to small scouts and prepare the main force's engagement geometry. The platform and spacing services remain reusable: neither knows what a bridge, Siege Tank, Marine, Thor, scouting group, or defensive doctrine is.

Damage distribution and target selection are separate combat concerns. Spacing reduces the number of friendly units exposed to one area strike; Siege Tank target assignment should separately avoid redundant overkill and distribute shots across valuable targets. Thor weapon selection and target priority likewise belong in combat evaluation backed by game data.

## Boundaries

These are pure utilities with unit tests. They are not yet wired into live movement FSMs, terrain extraction, pathing, target selection, or SC2 action translation. No in-game behavior is claimed until those integrations are tested in CI and replayed in SC2.
