using System.Collections.ObjectModel;

namespace TheSingularityWorkshop.SingularityBot.Gui.Authoring;

/// <summary>Editable GUI draft. It expresses authored intent only; runtime validation remains authoritative.</summary>
public sealed class BotDefinitionDraft
{
    public string SchemaVersion { get; set; } = "forge-draft/0.2";
    public BotIdentity Bot { get; set; } = new("new-bot", "New Bot", "Terran");
    public ObservableCollection<StrategyPhaseDraft> Phases { get; set; } = [];
    public ObservableCollection<GroupDraft> Groups { get; set; } = [];
    public ObservableCollection<UnitProfileDraft> UnitProfiles { get; set; } = [];
    public ObservableCollection<BehaviorDraft> Behaviors { get; set; } = [];
    public ObservableCollection<BehaviorRuleDraft> Rules { get; set; } = [];
    public ObservableCollection<ConstraintDraft> Constraints { get; set; } = [];
}

public sealed class BotIdentity(string id, string name, string race)
{
    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
    public string Race { get; set; } = race;
}

public sealed class StrategyPhaseDraft(string id, string name, string objective, int priority = 0)
{
    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
    public string Objective { get; set; } = objective;
    public int Priority { get; set; } = priority;
}

public sealed class GroupDraft(string id, string name, string membershipSelector, string sharedObjective, string coordinationPolicy)
{
    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
    public string MembershipSelector { get; set; } = membershipSelector;
    public string SharedObjective { get; set; } = sharedObjective;
    public string CoordinationPolicy { get; set; } = coordinationPolicy;
}

public sealed class UnitProfileDraft(string id, string unitId, string role, string profileCatalogId, string catalogVersion)
{
    public string Id { get; set; } = id;
    public string UnitId { get; set; } = unitId;
    public string Role { get; set; } = role;
    public string ProfileCatalogId { get; set; } = profileCatalogId;
    public string CatalogVersion { get; set; } = catalogVersion;
    public double? RetreatHealthRatio { get; set; }
    public Dictionary<string, object?> Overrides { get; set; } = [];
}

public sealed class BehaviorDraft(string id, string catalogId, string catalogVersion)
{
    public string Id { get; set; } = id;
    public string CatalogId { get; set; } = catalogId;
    public string CatalogVersion { get; set; } = catalogVersion;
    public Dictionary<string, object?> Parameters { get; set; } = [];
}

public sealed class BehaviorRuleDraft(string id, string behaviorId, string factId, string @operator, object? value, string candidateIntentId, int priority = 0)
{
    public string Id { get; set; } = id;
    public string BehaviorId { get; set; } = behaviorId;
    public string FactId { get; set; } = factId;
    public string Operator { get; set; } = @operator;
    public object? Value { get; set; } = value;
    public string CandidateIntentId { get; set; } = candidateIntentId;
    public int Priority { get; set; } = priority;
}

public sealed class ConstraintDraft(string id, string kind, bool isHard)
{
    public string Id { get; set; } = id;
    public string Kind { get; set; } = kind;
    public bool IsHard { get; set; } = isHard;
    public Dictionary<string, object?> Parameters { get; set; } = [];
}