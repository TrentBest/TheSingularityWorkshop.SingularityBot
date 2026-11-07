using TheSingularityWorkshop.SingularityBot.Gui.Contracts;

namespace TheSingularityWorkshop.SingularityBot.Gui.Authoring;

/// <summary>
/// Performs editor-side structural checks only. Catalog resolution and semantic/runtime
/// validation remain owned by the bot runtime.
/// </summary>
public static class ForgeDraftValidator
{
    private static readonly HashSet<string> Operators = new(StringComparer.Ordinal)
    {
        "lessThan", "lessThanOrEqual", "greaterThan", "greaterThanOrEqual", "equals"
    };

    public static IReadOnlyList<ForgeDiagnostic> Validate(BotDefinitionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var diagnostics = new List<ForgeDiagnostic>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        void Required(string? value, string path, string code)
        {
            if (string.IsNullOrWhiteSpace(value))
                diagnostics.Add(new(code, "Error", path, "A value is required.", []));
        }

        void Unique(string? value, string path)
        {
            Required(value, path, "required-id");
            if (!string.IsNullOrWhiteSpace(value) && !ids.Add(value))
                diagnostics.Add(new("duplicate-id", "Error", path, $"The identifier '{value}' is used more than once.", [value]));
        }

        Required(draft.SchemaVersion, "$.schemaVersion", "required-schema-version");
        Required(draft.Bot.Name, "$.bot.name", "required-bot-name");
        Unique(draft.Bot.Id, "$.bot.id");
        if (draft.Bot.Race is not ("Terran" or "Zerg" or "Protoss" or "Random"))
            diagnostics.Add(new("unsupported-race", "Error", "$.bot.race", "Race must be Terran, Zerg, Protoss, or Random.", []));

        for (var i = 0; i < draft.Phases.Count; i++)
        {
            var phase = draft.Phases[i];
            Unique(phase.Id, $"$.phases[{i}].id");
            Required(phase.Name, $"$.phases[{i}].name", "required-phase-name");
            Required(phase.Objective, $"$.phases[{i}].objective", "required-phase-objective");
        }

        for (var i = 0; i < draft.Groups.Count; i++)
        {
            var group = draft.Groups[i];
            Unique(group.Id, $"$.groups[{i}].id");
            Required(group.Name, $"$.groups[{i}].name", "required-group-name");
            Required(group.MembershipSelector, $"$.groups[{i}].membershipSelector", "required-membership-selector");
            Required(group.SharedObjective, $"$.groups[{i}].sharedObjective", "required-group-objective");
            Required(group.CoordinationPolicy, $"$.groups[{i}].coordinationPolicy", "required-coordination-policy");
        }

        for (var i = 0; i < draft.UnitProfiles.Count; i++)
        {
            var profile = draft.UnitProfiles[i];
            Unique(profile.Id, $"$.unitProfiles[{i}].id");
            Required(profile.UnitId, $"$.unitProfiles[{i}].unitId", "required-unit-id");
            Required(profile.Role, $"$.unitProfiles[{i}].role", "required-unit-role");
            Required(profile.ProfileCatalogId, $"$.unitProfiles[{i}].profileCatalogId", "required-profile-catalog-id");
            Required(profile.CatalogVersion, $"$.unitProfiles[{i}].catalogVersion", "required-catalog-version");
            if (profile.RetreatHealthRatio is double ratio && (!double.IsFinite(ratio) || ratio is < 0 or > 1))
                diagnostics.Add(new("invalid-health-ratio", "Error", $"$.unitProfiles[{i}].retreatHealthRatio", "Retreat health ratio must be a finite value from 0 through 1.", [profile.Id]));
        }

        var behaviorIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < draft.Behaviors.Count; i++)
        {
            var behavior = draft.Behaviors[i];
            Unique(behavior.Id, $"$.behaviors[{i}].id");
            behaviorIds.Add(behavior.Id);
            Required(behavior.CatalogId, $"$.behaviors[{i}].catalogId", "required-behavior-catalog-id");
            Required(behavior.CatalogVersion, $"$.behaviors[{i}].catalogVersion", "required-catalog-version");
        }

        for (var i = 0; i < draft.Rules.Count; i++)
        {
            var rule = draft.Rules[i];
            Unique(rule.Id, $"$.rules[{i}].id");
            Required(rule.BehaviorId, $"$.rules[{i}].behaviorId", "required-rule-behavior");
            if (!string.IsNullOrWhiteSpace(rule.BehaviorId) && !behaviorIds.Contains(rule.BehaviorId))
                diagnostics.Add(new("unresolved-behavior-reference", "Error", $"$.rules[{i}].behaviorId", $"Behavior '{rule.BehaviorId}' does not exist in this draft.", [rule.Id, rule.BehaviorId]));
            Required(rule.FactId, $"$.rules[{i}].factId", "required-fact-id");
            Required(rule.CandidateIntentId, $"$.rules[{i}].candidateIntentId", "required-candidate-intent-id");
            if (!Operators.Contains(rule.Operator))
                diagnostics.Add(new("unsupported-rule-operator", "Error", $"$.rules[{i}].operator", $"Operator '{rule.Operator}' is not supported by this editor vocabulary.", [rule.Id]));
            if (rule.Value is null)
                diagnostics.Add(new("missing-rule-value", "Error", $"$.rules[{i}].value", "A rule comparison value is required.", [rule.Id]));
            else if (rule.Value is double number && !double.IsFinite(number))
                diagnostics.Add(new("non-finite-rule-value", "Error", $"$.rules[{i}].value", "Rule values must be finite.", [rule.Id]));
        }

        return diagnostics;
    }
}
