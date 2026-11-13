using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>
/// Compile-time personality boundary consumed by the shared bot engine.
/// Implementations are linked into the bot DLL; they are never discovered or
/// loaded from runtime files or dynamic assemblies.
/// </summary>
public interface ICompiledBotStrategy
{
    /// <summary>Validated, versioned authoring data compiled with this personality.</summary>
    BotDefinition Definition { get; }

    /// <summary>
    /// Creates the initial objective policy. This is executable strategy code,
    /// not a runtime-loaded script; the shared engine still owns execution and
    /// the authoritative SC2 action-validation pipeline.
    /// </summary>
    GoalActionPlanDefinition CreateInitialGoalPlan();

    /// <summary>Applies this compiled personality's goal policy through the shared goal planner.</summary>
    IReadOnlyList<string> ApplyGoalPolicy(
        GoalActionPlanDefinition activePlan,
        UnitBehaviorRuntime runtime,
        SpatialMap map);

    /// <summary>Returns whether this personality should send its assembled Marine squad toward the enemy start.</summary>
    bool ShouldAdvanceToEnemyStart(GoalActionPlanDefinition activePlan);
}

/// <summary>The built-in Terran adaptive-Marine personality.</summary>
public sealed class AdaptiveMarineStrategy : ICompiledBotStrategy
{
    public BotDefinition Definition { get; } = BotDefinitionCatalog.CreateDefault();

    public GoalActionPlanDefinition CreateInitialGoalPlan() => Definition.DefaultGoals;

    public IReadOnlyList<string> ApplyGoalPolicy(
        GoalActionPlanDefinition activePlan, UnitBehaviorRuntime runtime, SpatialMap map) =>
        GoalActionPlanner.Apply(activePlan, runtime, map);

    public bool ShouldAdvanceToEnemyStart(GoalActionPlanDefinition activePlan) =>
        activePlan.DefaultRole == GoalActionRole.Engage;
}

/// <summary>Compatibility adapter for callers supplying a BotDefinition directly.</summary>
internal sealed class DefinitionBackedBotStrategy : ICompiledBotStrategy
{
    public DefinitionBackedBotStrategy(BotDefinition definition) =>
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));

    public BotDefinition Definition { get; }

    public GoalActionPlanDefinition CreateInitialGoalPlan() => Definition.DefaultGoals;

    public IReadOnlyList<string> ApplyGoalPolicy(
        GoalActionPlanDefinition activePlan, UnitBehaviorRuntime runtime, SpatialMap map) =>
        GoalActionPlanner.Apply(activePlan, runtime, map);

    public bool ShouldAdvanceToEnemyStart(GoalActionPlanDefinition activePlan) =>
        activePlan.DefaultRole == GoalActionRole.Engage;
}
