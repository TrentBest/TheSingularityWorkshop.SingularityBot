# SingularityBot

SingularityBot is a StarCraft II strategy system built as a Workshop experience: a game adapter feeds observations into an FSM-driven runtime; declarative strategy data expresses intent; the runtime turns that intent into legal game actions.

## Current state

This repository is in its foundation phase. The architecture and strategy contract are being established before importing or rewriting the legacy bot implementation. Nothing here should yet be interpreted as a ladder-ready bot.

## Explore

- [Architecture](architecture.md)
- [Strategy format](strategy-format.md)
- [Example strategy](../strategies/terran-opening.example.json)
- [Lineage and licensing](lineage.md)
- [Source migration audit](migration-audit.md)
- [Roadmap](roadmap.md)

## Design rule

**Strategies are data; execution is code.** A strategy should be inspectable, diffable, testable, and editable without recompiling the engine. The engine validates and compiles strategy data into FSM-backed runtime behavior.
