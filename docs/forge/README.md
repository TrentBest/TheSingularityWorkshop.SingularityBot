# Forge — browser bot authoring

Forge is the browser-facing authoring surface for the existing, versioned `BotDefinition` artifact.

## Open

When GitHub Pages is published, open the `/forge/` route from the documentation site. The Pages workflow remains manual-only; a development commit does not deploy the site.

## Current slice

- Loads the checked-in adaptive Marine example and exposes Terran, Zerg and Protoss as first-class race choices.
- A race change updates `BotDefinition.race`, `modeId`, `strategyProfile`, the runtime race declaration and the opening plan; the export is no longer just a visual preview.
- Fixed race selection pins a definition to one race. Random race selection is resolved by the Runner at launch across the three currently executable race adapters, then rebinds the definition to that race's registered mode and opening.
- Zerg and Protoss selections map to the currently registered bounded Zergling and Zealot openers. They are experimental openings, not claims of complete ladder-ready economies.
- Provides an interactive three-tier command-authority preview with Zerg (Overmind/Cerebrates), Terran (Admiral/command staff), and Protoss (Enclave/conclaves) editorial profiles. Coordinator groupings can be edited in the browser.
- The command-authority preview is not yet part of BotDefinition v1 and is not included in exported JSON; runtime hierarchy serialization and dispatch remain planned.
- Edits bot identity, strategy profile, default goal posture and bounded AI consultation enablement.
- Exposes the complete definition as JSON for advanced authoring.
- Imports and exports JSON files without sending them to a server. The exported artifact can be consumed by the Runner with `--definition=path-to-file.json`.
- Performs browser-side parsing and core contract checks.
- Loads a versioned static MapDocument fixture and overlays two versioned MatchSnapshot fixtures.
- Scrubs deterministic frames using the map document's full-map world-coordinate transform, preserving remembered contacts and displaying only authoritative destruction tags.
- Displays a source-derived diagram of the current Terran Marine scout and engagement path; it is explanatory, not live telemetry or a match simulation.

The browser checks are intentionally not represented as the authoritative runtime validator. SingularityBot's `BotDefinition.Parse` / `BotDefinition.Validate` and compiled capability resolution remain authoritative.

## Match-host boundary

The Test Arena panel is a truthful host-integration placeholder. It does not simulate a StarCraft II match and does not submit a definition to a guessed endpoint. The ServerSC2 integration still needs its production three-process participant/observer topology, host-owned lifecycle, authoritative terminal result and replay validation proven before Forge can enable Launch Match.

The next integration step is to freeze a versioned Forge-to-ServerSC2 submission/status contract, then bind the UI to that contract. Do not move process supervision, match authority, replay adjudication or bot behavior into browser code.
