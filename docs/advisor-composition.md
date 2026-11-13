# Advisor composition and execution model

## Purpose

Advisors are first-class, GUI-composable behavior owners. The GUI authors a versioned match manifest; the bot ships the executable FSM definitions, capability catalogues, schemas, and adapters. A manifest selects and configures those compiled definitions. It is declarative data, not user-supplied code.

The initial advisor families are:

- **Military Advisor** — force composition, squad/task assignments, threat response, engagement and retreat policy.
- **Economic Advisor** — worker/resource allocation, saturation, MULE policy, resource reservations, production and construction budgets.
- **Research Advisor** — technology priorities, prerequisites, research-host selection and research queue policy.
- **Base Layout Advisor** — expansion zones, construction placement, defensive geometry, add-on clearance and base-local response.
- **Intelligence Advisor** — scouting coverage, information confidence, scan reserve and uncertainty policy.

Advisors may own child advisors or subordinate capabilities. The hierarchy is intentionally recursive but bounded by manifest validation and runtime limits.

## Execution ownership

Every enabled advisor owns a distinct FSM_API context and a declared process group. Its state graph is a compiled definition. Advisor contexts expose typed observations, policy settings, child-group ownership, and emitted strategic intents.

The root scheduler advances root advisor groups in stable manifest order. An advisor's active state may advance only the child groups owned by that state. A child group must have exactly one scheduler owner in a frame. The runtime rejects cycles, duplicate group ownership, unknown groups, excessive nesting, and attempts to advance a group twice in one frame.

The scheduler must not recursively call a group that is also registered as a root group. Parent-to-child execution is explicit, ordered, and traceable. State transitions may activate/deactivate child groups; they do not silently change ownership.

All advisor FSMs use FSM_API. No parallel hand-written advisor state machine is permitted. Existing policy classes such as EconomyAdvisorSettings remain typed policy/configuration services consumed by the relevant advisor FSM, not competing execution engines.

## Intent boundary

Advisors reason and publish desired outcomes (for example, desired workers per resource, a technology priority, a squad objective, or a placement request). They do not submit SC2 protocol actions. A central intent coordinator resolves ownership, priority, resource contention, reservations and conflicts. SC2 adapters then validate current visibility, game data, prerequisites, ability IDs, range, placement and resource availability before translating accepted intents.

## Economic mining controls

The Economic Advisor exposes per-resource policy rather than one global magic saturation number:

- desired workers per mineral node;
- desired workers per gas refinery;
- whether custom assignments are enabled;
- whether StarCraft II default worker behavior remains authoritative;
- MULE enablement, energy reserve and call-down policy;
- optional phase/threshold rules that can reduce mineral staffing when MULE coverage or another configured condition is met.

A policy that sets a resource target to zero means the custom planner should stop assigning new workers there; it must not strand workers carrying resources or erase active reservations without a safe handoff.

Mining comparison has three explicit modes:

1. **GameDefault** — observe and record Blizzard's default assignments; do not issue custom mining assignments.
2. **Custom** — custom worker planner owns assignments, subject to safety and action validation.
3. **DualRun** — run custom planning in shadow alongside the default behavior, record both plans and compare outcomes, but submit no custom mining commands. A later explicit authority mode can select the custom plan. DualRun must never issue both plans simultaneously.

Comparison metrics include worker-to-resource assignments, saturation, idle time, travel distance/time, mineral/gas collection where observable, reassignment churn, and missed/invalid commands. Do not claim exact income attribution unless the observation stream supports it.

## Manifest and ladder packaging

The schema is versioned and rejects unknown properties. The build embeds the selected manifest and all required compiled definitions/catalogues as assembly resources. At match startup, the runtime validates the complete manifest against the compiled catalogue before activating any advisor. Unknown IDs, unsupported settings, duplicate IDs/groups, invalid parent-child ownership, invalid thresholds, and unsupported schema versions fail closed.

The precise ladder delivery contract still needs verification against the SC2 ladder runner. Do not assume arbitrary file reads or network access. The embedded manifest is the no-external-I/O baseline; if the ladder protocol supplies an allowed configuration channel, a validated manifest override may be accepted through that channel and merged deterministically over embedded defaults.

## GUI contract

The GUI should render the same compiled catalogue and schema used by the bot. Users compose an advisor tree, choose compiled definitions, enable/disable nodes, configure typed settings, and inspect process-group ownership and active FSM state. The GUI must not author arbitrary C# or introduce new executable FSM code into a ladder match.

For every advisor and subordinate group, expose:
- definition ID and version;
- owning parent and process group;
- enabled/current state;
- current settings and effective merged values;
- intents emitted and their provenance;
- groups stepped this frame and execution order;
- suppressed/invalid intents with reasons;
- shadow-comparison metrics where enabled.

## Implementation sequence

1. Stabilize the advisor contract, typed manifest schema, and default manifest.
2. Implement a root advisor runtime with FSM_API-backed advisor contexts and deterministic hierarchical scheduling.
3. Move existing Economy and Intelligence policies under those advisor contexts without changing their current behavior.
4. Add Research and Base Layout advisors, then Military Advisor.
5. Add mining mode selection and DualRun shadow comparison with tests proving that shadow mode never submits commands.
6. Bind the manifest to embedded resources and verify the actual ladder startup contract.
7. Expose the compiled schema/catalogue and live state/intents to the GUI.
