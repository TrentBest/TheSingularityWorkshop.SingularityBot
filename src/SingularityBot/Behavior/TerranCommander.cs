namespace SingularityBot.Behavior;

/// <summary>
/// Terran's subordinate command layer. It owns the configurable policy
/// specialists that previously lived as peer fields on SingularityBot.
/// The Chief assigns direction and reviews their proposals; this class does
/// not submit SC2 protocol actions.
/// </summary>
public sealed class TerranCommander
{
    public EconomyAdvisor Economy { get; } = new();
    public IntelligenceAdvisor Intelligence { get; } = new();
    public ScoutingAdvisor Scouting { get; } = new();
}
