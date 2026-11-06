<!-- The Singularity Workshop moniker -->
<p align="center">
  <a href="https://github.com/TrentBest">
    <img src="https://raw.githubusercontent.com/TrentBest/TheSingularityWorkshop.Visualize/development/docs/assets/moniker.svg" alt="The Singularity Workshop — animated workshop moniker" width="720" />
  </a>
</p>

# ✳️ SingularityBot

[![Sith Policy](https://raw.githubusercontent.com/TrentBest/TheSingularityWorkshop.SingularityBot/master/assets/sith-policy.svg)](https://github.com/TrentBest/TheSingularityWorkshop.SingularityBot/actions/workflows/sith-policy.yml)

**A StarCraft II strategy system built on the Singularity Workshop.**

SingularityBot separates the StarCraft II client protocol from game ontology, FSM-driven execution, strategy authoring, spatial reasoning, memory and evaluation. Strategies are intended to be declarative data compiled into runtime state machines—not hard-coded bot personalities.

> **Status:** foundation in progress. The repository currently contains architecture, strategy-format documentation and an illustrative strategy file. It is not yet a working or ladder-ready bot.

## Start here

- [Documentation site](https://trentbest.github.io/TheSingularityWorkshop.SingularityBot/)
- [Architecture](docs/architecture.md)
- [Strategy format](docs/strategy-format.md)
- [FSM-native behavior model and first unit expertise set](docs/behavior-model.md)
- [Opening economy and resource logistics](docs/opening-economy-logistics.md)
- [Strategy JSON Schema](schemas/strategy.schema.json)
- [Example strategy](strategies/terran-opening.example.json)
- [Lineage and licensing](docs/lineage.md)
- [Source migration audit](docs/migration-audit.md)
- [GitHub Pages publishing](docs/pages.md)
- [Roadmap](docs/roadmap.md)
- [Local game sessions and human-vs-bot architecture](docs/local-game-sessions.md)

## Workshop composition

| Concern | Workshop system |
|---|---|
| Match, player and unit lifecycles | FSM_API |
| Hierarchical worker, squad and unit behavior | FSM_API behavior definitions + per-entity contexts |
| Game vocabulary and protocol concepts | GrammarAi / ProtocolAi |
| Goals and command decomposition | CommandAi |
| Spatial reasoning | Physics |
| Durable match memory | DataWarehouse |
| Strategy authoring and visualization | GUI / Renderer / Visualize |
| Build, replay regression and evaluation | CiAi / HeadlessAi |

The SC2 adapter remains a replaceable boundary. FSM_API must remain independent of SC2-specific types, and the headless runtime must not require the editor or visualization stack.

## Lineage

The historical upstream starting point is [SimonPrins/ExampleBot](https://github.com/SimonPrins/ExampleBot) by **Simon Prins**. The Workshop's later [FSM_Bot](https://github.com/TrentBest/FSM_Bot) contains an FSM-oriented evolution, but its Git history does not preserve ExampleBot's original ancestry. See [lineage and licensing](docs/lineage.md) before importing or redistributing upstream-derived files.

## Development

- `development` is the active integration branch.
- `master` is the stable/released state.
- The repository is governed by the Workshop's Sith policy: exactly these two branches.
- No NuGet publication, ladder deployment or other external release occurs without explicit authorization.

---

<p align="center">
  <a href="https://github.com/TrentBest/TheSingularityWorkshop" aria-label="The Singularity Workshop living avatar">
    <img src="https://raw.githubusercontent.com/TrentBest/WebPage/development/TheSingularityWorkshop/wwwroot/Images/TheSingularityWorkshopLivingAvatar.svg" alt="The Singularity Workshop living avatar" width="112" />
  </a><br />
  <strong>The Singularity Workshop</strong><br />
  <em>If a picture is worth a thousand words, what is a living picture worth?</em><br />
  <a href="https://github.com/TrentBest">GitHub</a> · <a href="https://www.patreon.com/TheSingularityWorkshop">Support the Workshop</a>
</p>
