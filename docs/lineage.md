# Lineage and licensing

## Upstream foundation

The historical starting point identified for this project is [SimonPrins/ExampleBot](https://github.com/SimonPrins/ExampleBot), authored by **Simon Prins**. Its purpose is a minimal C# StarCraft II bot and example of connecting to the SC2 client protocol.

The upstream repository is the source of the original adapter pattern and portions of the SC2 API client code that later appeared in the Workshop's FSM_Bot work.

## Workshop evolution

[TrentBest/FSM_Bot](https://github.com/TrentBest/FSM_Bot) is the Workshop's later FSM-oriented evolution. Its history is not a Git ancestor of SimonPrins/ExampleBot, so it must not be represented as though Git preserved the original upstream commit chain. The code comparison indicates copied/evolved source, not a preserved fork history.

[TrentBest/FSM_Bot_Editor](https://github.com/TrentBest/FSM_Bot_Editor) is a separate editor prototype containing map, unit metadata and build-order UI experiments. It is a source of concepts and candidate components, not a finished editor contract.

## Migration policy

- Preserve upstream attribution and license notices for every retained upstream-derived file.
- Preserve the upstream `LICENSE` and `PROTOCOL_LICENSE` notices where applicable; review protocol-specific terms separately before redistributing generated or copied protocol sources.
- Mark substantial copied or adapted files with source attribution and change notes where practical.
- Record the exact upstream commit SHA and the exact FSM_Bot commit/file origins in the migration ledger before importing code.
- Do not claim the new repository has Simon Prins's Git ancestry unless the original Git history is actually imported.
- Keep upstream-derived adapter code isolated from Workshop strategy/runtime code so it can be updated or replaced.

## Known references

- Upstream repository: https://github.com/SimonPrins/ExampleBot
- Workshop evolution: https://github.com/TrentBest/FSM_Bot
- Strategy editor prototype: https://github.com/TrentBest/FSM_Bot_Editor

The exact source commits and file-level mapping will be added to the migration ledger during the code-port phase.
