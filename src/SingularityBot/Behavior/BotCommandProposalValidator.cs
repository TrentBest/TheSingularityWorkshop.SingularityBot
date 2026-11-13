using System.Globalization;
using TheSingularityWorkshop.CommandAi;

namespace SingularityBot.Behavior;

/// <summary>
/// Validates untrusted CommandAi proposals against SingularityBot's trusted vocabulary.
/// This layer resolves structure and basic parameter semantics only; runtime policy,
/// catalog resolution, live-state checks, scheduling, and SC2 legality remain downstream.
/// </summary>
public static class BotCommandProposalValidator
{
    public sealed record ValidatedCommand(
        BotCommandVocabulary.ActionDefinition Definition,
        IReadOnlyDictionary<ulong, CommandValue> Arguments);

    public static bool TryValidate(
        CommandProposal proposal,
        out IReadOnlyList<ValidatedCommand> commands,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var validated = new List<ValidatedCommand>(proposal.Commands.Count);

        foreach (var instruction in proposal.Commands)
        {
            if (!BotCommandVocabulary.TryGetAction(instruction.ActionId, out var action))
                return Fail($"Unknown action identity '{instruction.ActionId}'.", out commands, out error);

            var definitions = action.Parameters.ToDictionary(parameter => parameter.Id);
            var arguments = new Dictionary<ulong, CommandValue>();

            foreach (var argument in instruction.Arguments)
            {
                if (!definitions.TryGetValue(argument.ParameterId, out var parameter))
                    return Fail($"Action '{action.Name}' does not declare parameter '{argument.ParameterId}'.", out commands, out error);

                var expectedKind = parameter.Kind == BotCommandVocabulary.ValueKind.Identifier
                    ? CommandValueKind.IntegerIdentifier
                    : CommandValueKind.LiteralText;
                if (argument.Value.Kind != expectedKind)
                    return Fail($"Parameter '{parameter.Name}' has the wrong value kind.", out commands, out error);

                if (argument.Value.Kind == CommandValueKind.IntegerIdentifier &&
                    (!argument.Value.Identifier.HasValue || argument.Value.Identifier.Value == 0))
                    return Fail($"Parameter '{parameter.Name}' requires a positive identifier.", out commands, out error);

                if (parameter.Id is BotCommandVocabulary.Parameters.ProductionDesiredCount
                    or BotCommandVocabulary.Parameters.BuildingDesiredCount)
                {
                    if (!int.TryParse(argument.Value.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count <= 0)
                        return Fail($"Parameter '{parameter.Name}' must be a positive base-10 integer.", out commands, out error);
                }
                else if (parameter.Id == BotCommandVocabulary.Parameters.ProductionPriority)
                {
                    if (!int.TryParse(argument.Value.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var priority) || priority is < 0 or > 100)
                        return Fail($"Parameter '{parameter.Name}' must be an integer from 0 through 100.", out commands, out error);
                }

                arguments.Add(argument.ParameterId, argument.Value);
            }

            foreach (var required in action.Parameters.Where(parameter => parameter.Required))
            {
                if (!arguments.ContainsKey(required.Id))
                    return Fail($"Action '{action.Name}' is missing required parameter '{required.Name}'.", out commands, out error);
            }

            validated.Add(new ValidatedCommand(action, arguments));
        }

        commands = validated.AsReadOnly();
        error = null;
        return true;
    }

    private static bool Fail(string message, out IReadOnlyList<ValidatedCommand> commands, out string error)
    {
        commands = Array.Empty<ValidatedCommand>();
        error = message;
        return false;
    }
}
