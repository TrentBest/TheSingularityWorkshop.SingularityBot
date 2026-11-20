# Forge — browser bot authoring

Forge is the browser-facing authoring surface for the existing, versioned `BotDefinition` artifact.

## Open

When GitHub Pages is published, open the `/forge/` route from the documentation site. The Pages workflow remains manual-only; a development commit does not deploy the site.

## Current slice

- Loads the checked-in Terran adaptive Marine example.
- Edits bot identity, strategy profile, default goal posture and bounded AI consultation enablement.
- Exposes the complete definition as JSON for advanced authoring.
- Imports and exports JSON files without sending them to a server.
- Performs browser-side parsing and core contract checks.
- Loads a versioned static MapDocument fixture and overlays two versioned MatchSnapshot fixtures.
- Scrubs deterministic frames using the map document's full-map world-coordinate transform, preserving remembered contacts and displaying only authoritative destruction tags.

The browser checks are intentionally not represented as the authoritative runtime validator. SingularityBot's `BotDefinition.Parse` / `BotDefinition.Validate` and compiled capability resolution remain authoritative.

## Match-host boundary

The Test Arena panel is a truthful host-integration placeholder. It does not simulate a StarCraft II match and does not submit a definition to a guessed endpoint. The ServerSC2 integration still needs its production three-process participant/observer topology, host-owned lifecycle, authoritative terminal result and replay validation proven before Forge can enable Launch Match.

The next integration step is to freeze a versioned Forge-to-ServerSC2 submission/status contract, then bind the UI to that contract. Do not move process supervision, match authority, replay adjudication or bot behavior into browser code.
