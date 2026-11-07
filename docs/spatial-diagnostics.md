# Spatial diagnostics

The standalone diagnostics tool turns a checked-in map/observation fixture into:

- `spatial-report.json`: map geometry, side/quadrant/sector classifications, adjacency, and visible/remembered contact facts.
- `spatial-overlay.svg`: a dependency-light map drawing of the same data.

## Run

From the repository root:

```bash
dotnet run --project tools/SingularityBot.SpatialDiagnostics -- \
  tools/SingularityBot.SpatialDiagnostics/Fixtures/translated-nonsquare.json \
  artifacts/spatial
```

The tool generates each artifact twice in memory and compares the exact output before writing files. A mismatch fails the command. Output belongs in a local ignored/generated directory and should not be committed as build output.

## Visual language

- Blue: friendly contacts and friendly start.
- Pink: enemy contacts and enemy start.
- Gray: neutral contacts.
- Solid, brighter markers: currently visible.
- Faded, dashed markers: remembered from an earlier observation.
- Gold line: the Side divider.
- Dashed center lines: Quadrant boundaries.
- Fine grid: the 3x3 Sector layout.

Sectors and quadrants are geometric partitions only. They are not pathing regions, visibility regions, influence maps, or guarantees that a unit can travel between adjacent cells. The overlay visualizes the current spatial contract; it does not infer terrain connectivity or strategic intent.
