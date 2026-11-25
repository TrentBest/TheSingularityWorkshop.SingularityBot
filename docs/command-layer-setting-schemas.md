# Command-layer setting schemas

## Purpose

A registered command-layer implementation may expose trusted setting metadata so Studio and ServerSC2 can render the same authoring controls without maintaining separate lists of strategy properties.

`CommandLayerDescriptor` now carries:

- `SettingsSchemaVersion`, a positive version for the setting contract;
- `Settings`, an immutable snapshot of `CommandLayerSettingDescriptor` values.

The descriptor is supplied by the trusted compiled registry. A BotDefinition may select a registered implementation and provide values, but it cannot register a new implementation or supply executable control logic.

## Setting descriptor

| Field | Meaning |
|---|---|
| `Id` | Stable implementation-scoped identifier |
| `DisplayName`, `Description` | Author-facing label and help |
| `ValueKind` | Boolean, Integer, Number, String, Enumeration, StringList, or Object |
| `DefaultJson` | One literal JSON default, never an expression |
| `AllowedValues` | Closed options for enumeration or bounded string-list values |
| `Minimum`, `Maximum` | Finite numeric bounds |
| `Units`, `Precision` | Numeric presentation metadata |
| `Required` | Whether the authored value must be present |
| `MaximumItems` | Explicit collection bound for string lists |
| `Mutability` | DefinitionOnly, PreMatch, or RuntimeSafe |
| `VisibleWhen*`, `EnabledWhen*` | Optional equality condition referencing another setting in the same object |
| `Children` | Nested setting descriptors for Object values |

Object settings can contain nested setting schemas. String lists require an explicit maximum length. Enum values are closed and unique. Conditions are data-only equality comparisons; arbitrary expressions, scripts, type names, and executable callbacks are not accepted.

## Validation and publication

`CommandLayerSettingSchema.Validate` checks:

- positive schema version, stable unique IDs, labels, descriptions, and known enum values;
- JSON default syntax and value-kind agreement;
- finite and ordered numeric bounds, precision, and units;
- default values against declared numeric/enum/list constraints;
- collection bounds and nested object depth/count;
- paired visibility/enabling condition fields and same-object setting references.

`CommandLayerSettingSchema.Snapshot` validates and deep-copies the descriptor tree into read-only collections. `CommandLayerRegistry.Register` performs this snapshot before making a descriptor visible. Mutating a caller-owned list after registration cannot change the published catalogue.

The schema is presentation and authoring metadata, not runtime authority. The compiled implementation still owns semantic interpretation, validation against current match state, and action execution. A value marked RuntimeSafe is not permission to bypass the runtime's own safe-update contract.

## Current status

The metadata contract and registry snapshot boundary are implemented with tests. Existing command-layer registrations do not yet publish complete per-implementation settings, and Studio does not yet render the full catalogue from these descriptors. Those are the next integration steps. This is not a claim that every strategy setting is already editable.
