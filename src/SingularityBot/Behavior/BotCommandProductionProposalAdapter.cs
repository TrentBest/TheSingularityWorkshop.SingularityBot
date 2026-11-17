using System.Globalization;
using SC2APIProtocol;
using TheSingularityWorkshop.CommandAi;

namespace SingularityBot.Behavior;

/// <summary>
/// Host-side bridge from an untrusted CommandAi proposal to the existing
/// production-plan runtime. It validates, authorizes, and reconciles intent;
/// it never submits SC2 actions.
/// </summary>
public sealed class BotCommandProductionProposalAdapter
{
    private const ulong MarineTargetIdentity = 1;
    private const string MarineTargetName = "Marine";
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _appliedProposals = new(StringComparer.Ordinal);

    /// <summary>
    /// Applies one explicitly authorized production proposal to the matching
    /// authored objective. The host supplies a stable idempotency key and the
    /// authorization decision; neither is inferred from the proposal.
    /// </summary>
    public bool TryApply(
        CommandProposal proposal,
        string idempotencyKey,
        bool authorized,
        BotDefinition definition,
        Race actualRace,
        Sc2GameDataCatalog gameData,
        ProductionPlanRuntime runtime,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(gameData);
        ArgumentNullException.ThrowIfNull(runtime);

        if (!authorized)
            return Fail("The host has not authorized this proposal.", out error);
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128)
            return Fail("A stable idempotency key of 1 to 128 characters is required.", out error);
        if (!Enum.TryParse<Race>(definition.Race, ignoreCase: false, out var configuredRace) ||
            configuredRace != actualRace)
            return Fail("The active BotDefinition race does not match the authoritative match race.", out error);
        if (!string.Equals(definition.Opening?.Race, definition.Race, StringComparison.Ordinal))
            return Fail("The active opening plan race does not match the BotDefinition race.", out error);

        if (!Sc2RaceProductionProfileCatalog.CreateDefault().TryResolveGameData(
                definition.ModeId, actualRace, gameData,
                out var profile, out var target, out var producer, out error))
            return false;

        if (!string.Equals(profile.TargetUnit, MarineTargetName, StringComparison.Ordinal) ||
            target is null || producer is null)
            return Fail("The selected production profile is not supported by this proposal adapter.", out error);

        if (!BotCommandProposalValidator.TryValidate(proposal, out var commands, out error))
            return false;
        if (commands.Count != 1 || commands[0].Definition.Id != BotCommandVocabulary.Actions.SetProductionTarget)
            return Fail("A production proposal must contain exactly one SetProductionTarget command.", out error);

        var command = commands[0];
        if (!command.Arguments.TryGetValue(BotCommandVocabulary.Parameters.ProductionTarget, out var targetValue) ||
            targetValue.Identifier != MarineTargetIdentity)
            return Fail("The production target identity is unknown to the host vocabulary.", out error);

        if (!command.Arguments.TryGetValue(BotCommandVocabulary.Parameters.ProductionDesiredCount, out var countValue) ||
            !int.TryParse(countValue.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var desiredCount) ||
            desiredCount <= 0)
            return Fail("The desired production count is invalid.", out error);

        var openingMatches = (definition.Opening.Steps ?? Array.Empty<ProductionStepDefinition>())
            .Where(step => step is not null &&
                string.Equals(step.Target, profile.TargetUnit, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(step.Producer, profile.ProducerUnit, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (openingMatches.Length != 1)
            return Fail("The active BotDefinition must contain exactly one authored objective for this target and producer.", out error);

        var authoredStep = openingMatches[0];
        if (!HasRequiredPrerequisites(authoredStep, definition.Opening.Steps, profile.RequiredCompletedUnits))
            return Fail("The active production objective does not depend on every profile prerequisite.", out error);
        if (!runtime.TryGetStep(authoredStep.Id, out var runtimeStep) ||
            !ReferenceEquals(runtimeStep.Definition, authoredStep) &&
            runtimeStep.Definition != authoredStep)
            return Fail("The authored production objective is not registered in the active runtime.", out error);

        var priority = runtimeStep.EffectivePriority;
        if (command.Arguments.TryGetValue(BotCommandVocabulary.Parameters.ProductionPriority, out var priorityValue) &&
            !int.TryParse(priorityValue.Text, NumberStyles.None, CultureInfo.InvariantCulture, out priority))
            return Fail("The production priority is invalid.", out error);
        if (priority is < 0 or > 100)
            return Fail("The production priority must be between 0 and 100.", out error);

        var fingerprint = CreateFingerprint(proposal);
        lock (_gate)
        {
            if (_appliedProposals.TryGetValue(idempotencyKey, out var previous))
            {
                if (string.Equals(previous, fingerprint, StringComparison.Ordinal))
                {
                    error = string.Empty;
                    return true;
                }
                return Fail("The idempotency key was already used for a different proposal.", out error);
            }

            if (!runtimeStep.TryApplyAuthorizedTarget(desiredCount, priority))
                return Fail("The runtime rejected the production target override.", out error);
            _appliedProposals.Add(idempotencyKey, fingerprint);
        }

        error = string.Empty;
        return true;
    }

    private static bool HasRequiredPrerequisites(
        ProductionStepDefinition step,
        IReadOnlyList<ProductionStepDefinition> steps,
        IReadOnlyList<string> requiredUnits)
    {
        var byId = steps.Where(candidate => candidate is not null)
            .ToDictionary(candidate => candidate.Id, StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var ancestors = new List<ProductionStepDefinition>();

        void Visit(ProductionStepDefinition current)
        {
            foreach (var dependencyId in current.DependsOn ?? Array.Empty<string>())
            {
                if (!visited.Add(dependencyId) || !byId.TryGetValue(dependencyId, out var dependency))
                    continue;
                ancestors.Add(dependency);
                Visit(dependency);
            }
        }

        Visit(step);
        return requiredUnits.All(required => ancestors.Any(ancestor =>
            string.Equals(ancestor.Target, required, StringComparison.OrdinalIgnoreCase)));
    }

    private static string CreateFingerprint(CommandProposal proposal) =>
        proposal.ProtocolVersion + "|" + string.Join(";", proposal.Commands.Select(command =>
            command.ActionId.ToString(CultureInfo.InvariantCulture) + ":" +
            string.Join(",", command.Arguments.OrderBy(argument => argument.ParameterId)
                .Select(argument => argument.ParameterId.ToString(CultureInfo.InvariantCulture) + "=" +
                    argument.Value.Kind + ":" + argument.Value.Text + ":" +
                    argument.Value.Identifier?.ToString(CultureInfo.InvariantCulture)))));

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
