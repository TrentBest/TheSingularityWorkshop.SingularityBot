# Goal action planning

Goal action plans are user-authored strategic intent, separate from production plans. Production determines what is built; a goal plan assigns the resulting combat units jobs. The initial example is `strategies/contain-and-observe.goal.json`.

The initial roles are Rally, Scout, Contain, Engage and Fallback. The planner assigns two visible friendly Marines/Marauders to distinct sectors in stable unit-tag order, excluding the sector containing the known enemy start. Remaining eligible units retain the configured containment role. Sector assignments use the existing 3x3 `SpatialMap`.

FSM_API registers GoalAction, ScoutSector and StutterStep capabilities. GoalAction changes strategic role; ScoutSector emits the assigned-sector scouting intent; StutterStep alternates between attack intent when the weapon is ready and reposition intent while cooldown remains. Stutter-step is a per-unit plan setting and can be disabled. Sector and cooldown data are caller-owned facts.

This first slice tests semantic intents and deterministic role assignment only. These new intents are not yet translated into SC2 raw actions. Weapon cooldown is not yet populated from authoritative weapon data; health-based fallback, enemy-expansion response and runtime JSON loading remain integration gates.
