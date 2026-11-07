namespace TheSingularityWorkshop.SingularityBot.Gui.Authoring;

/// <summary>GUI-owned draft of authored intent. Runtime validation remains authoritative.</summary>
public sealed record BotDefinitionDraft
{
    public string SchemaVersion { get; init; } = "forge-draft/0.1";
    public required BotIdentity Bot { get; init; }
    public List<StrategyPhaseDraft> Phases { get; init; } = [];
    public List<GroupDraft> Groups { get; init; } = [];
    public List<UnitProfileDraft> UnitProfiles { get; init; } = [];
    public List<BehaviorDraft> Behaviors { get; init; } = [];
    public List<BehaviorRuleDraft> Rules { get; init; } = [];
    public List<ConstraintDraft> Constraints { get; init; } = [];
}

public sealed record BotIdentity(string Id, string Name, string Race);
public sealed record StrategyPhaseDraft(string Id, string Name, string Objective, int Priority = 0);
public sealed record GroupDraft(string Id, string Name, string MembershipSelector, string SharedObjective, string CoordinationPolicy);
public sealed record UnitProfileDraft(string Id, string UnitId, string Role, string ProfileCatalogId, string CatalogVersion, Dictionary<string, object?> Overrides);
public sealed record BehaviorDraft(string Id, string CatalogId, string CatalogVersion, Dictionary<string, object?> Parameters);
public sealed record BehaviorRuleDraft(string Id, string BehaviorId, string FactId, string Operator, object? Value, string CandidateIntentId, int Priority = 0);
public sealed record ConstraintDraft(string Id, string Kind, bool IsHard, Dictionary<string, object?> Parameters);