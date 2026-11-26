namespace SingularityBot.Behavior;

/// <summary>
/// Stable, bot-owned action and parameter identities for CommandAi proposals.
/// These are domain identities, never StarCraft II protocol ability or unit IDs.
/// </summary>
public static class BotCommandVocabulary
{
    public const string Version = "SingularityBot.Commands/1";

    public static class Actions
    {
        public const ulong SetProductionTarget = 1001;
        public const ulong ConstructBuilding = 1002;
        public const ulong AssignScout = 1003;
        public const ulong AssignMilitaryOperation = 1004;
        public const ulong TransitionStrategy = 1005;
    }

    public static class Parameters
    {
        public const ulong ProductionTarget = 2001;
        public const ulong ProductionDesiredCount = 2002;
        public const ulong ProductionPriority = 2003;
        public const ulong BuildingTarget = 2011;
        public const ulong BuildingDesiredCount = 2012;
        public const ulong ScoutSector = 2021;
        public const ulong MilitaryOperation = 2031;
        public const ulong StrategyTransition = 2041;
    }

    public enum ValueKind
    {
        Identifier,
        LiteralText
    }

    public sealed record ParameterDefinition(
        ulong Id,
        string Name,
        ValueKind Kind,
        bool Required,
        string Description);

    public sealed record ActionDefinition(
        ulong Id,
        string Name,
        string Description,
        IReadOnlyList<ParameterDefinition> Parameters);

    private static readonly IReadOnlyList<ActionDefinition> Definitions =
    [
        new(Actions.SetProductionTarget, "SetProductionTarget",
            "Request a desired count for a known bot production target.",
            [
                new(Parameters.ProductionTarget, "target", ValueKind.Identifier, true,
                    "Bot-owned ontology identity for the unit or production target."),
                new(Parameters.ProductionDesiredCount, "desiredCount", ValueKind.LiteralText, true,
                    "Positive base-10 integer desired total; completed and in-progress units are reconciled by the host."),
                new(Parameters.ProductionPriority, "priority", ValueKind.LiteralText, false,
                    "Optional integer priority from 0 through 100.")
            ]),
        new(Actions.ConstructBuilding, "ConstructBuilding",
            "Request a construction objective for a known building identity.",
            [
                new(Parameters.BuildingTarget, "building", ValueKind.Identifier, true,
                    "Bot-owned ontology identity for the building."),
                new(Parameters.BuildingDesiredCount, "desiredCount", ValueKind.LiteralText, true,
                    "Positive base-10 integer desired completed total.")
            ]),
        new(Actions.AssignScout, "AssignScout",
            "Request reconnaissance of a known strategic sector.",
            [
                new(Parameters.ScoutSector, "sector", ValueKind.Identifier, true,
                    "Bot-owned identity for a map sector.")
            ]),
        new(Actions.AssignMilitaryOperation, "AssignMilitaryOperation",
            "Request a named, authored military operation.",
            [
                new(Parameters.MilitaryOperation, "operation", ValueKind.Identifier, true,
                    "Identity of an operation already defined by the bot.")
            ]),
        new(Actions.TransitionStrategy, "TransitionStrategy",
            "Propose a transition to a known strategy; runtime guards remain authoritative.",
            [
                new(Parameters.StrategyTransition, "strategy", ValueKind.Identifier, true,
                    "Identity of a strategy transition defined by the selected BotDefinition.")
            ])
    ];

    public static IReadOnlyList<ActionDefinition> All => Definitions;

    public static bool TryGetAction(ulong actionId, out ActionDefinition definition)
    {
        definition = Definitions.FirstOrDefault(item => item.Id == actionId);
        return definition is not null;
    }
}
