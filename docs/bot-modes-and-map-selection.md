# Bot modes and map selection

A map/experience selects a bot mode as its **What**. The map stores a stable mode ID; the runtime resolves that ID against the authoritative in-game `RaceActual`. Race is not inferred from the mode, and a mismatched or missing selection fails closed. The same explicit selection path is used even when a race temporarily has only one available mode.

| Race | Mode | Primary unit | Mode ID |
|---|---|---|---|
| Terran | Marine Mode | Marine | `terran.marine.v1` |
| Terran | Marauder Mode | Marauder | `terran.marauder.v1` |
| Protoss | Zealot Mode | Zealot | `protoss.zealot.v1` |
| Protoss | Stalker Mode | Stalker | `protoss.stalker.v1` |
| Zerg | Zergling Mode | Zergling | `zerg.zergling.v1` |
| Zerg | Queen Mode | Queen | `zerg.queen.v1` |

The primary unit names the central force and strategic identity; it is not an exclusive unit whitelist. Supporting units, workers, production structures, upgrades and race-specific mechanics remain available to the strategy when their prerequisites and safety checks permit.

The first implementation milestone is deliberately narrow: Zergling Mode must establish Zerg production and get Zerglings onto the map; Zealot Mode must establish Protoss production and get Zealots onto the map. Queen and Stalker modes exercise the same selection and strategy contracts and can follow as their production/control paths are ready. Marine Mode remains the existing Terran baseline; Marauder Mode is a separate Terran strategy, not a renamed Marine composition.

Mode catalogue registration is not gameplay support. Until a mode has a race-specific production adapter and tested action path, its descriptor is authoring/selection metadata only and the race module must remain fail-closed. Unit IDs, costs, producer requirements and ability IDs must be resolved from the match's authoritative SC2 game data; never guess IDs or silently substitute another mode.

The runtime composition remains local: the selected experience's trusted definition is composed at initialization from registered components. A web repository is not a runtime MicroBundle source.
