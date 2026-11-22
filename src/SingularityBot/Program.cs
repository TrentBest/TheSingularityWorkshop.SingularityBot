using SC2API_CSharp;
using SC2APIProtocol;
using System.Globalization;
using SingularityBot.Behavior;

namespace SingularityBot;

public static class Program
{
    private static readonly BotMatchConfiguration MatchConfiguration = BotMatchConfiguration.Default;

    public static void Run(string[] args, Action<SingularityBot> beforeConnect = null, Action afterConnect = null)
    {
        var passiveBaseline = args.Contains("--passive-baseline", StringComparer.Ordinal);
        var diagnostics = !args.Contains("--no-diagnostics", StringComparer.Ordinal);
        var diagnosticsRequested = args.Contains("--diagnostics", StringComparer.Ordinal);
        var designatedAreaArgument = args.FirstOrDefault(argument =>
            argument.StartsWith("--cc-area=", StringComparison.Ordinal));
        var modeArgument = args.FirstOrDefault(argument =>
            argument.StartsWith("--mode=", StringComparison.Ordinal));
        var selectedModeId = modeArgument?["--mode=".Length..];
        var connectionArgs = args.Where((argument, index) =>
            !argument.StartsWith("--cc-area=", StringComparison.Ordinal) &&
            !argument.StartsWith("--mode=", StringComparison.Ordinal) &&
            !argument.StartsWith("--studio-bridge-port=", StringComparison.Ordinal) &&
            !string.Equals(argument, "--studio-bridge", StringComparison.Ordinal) &&
            !string.Equals(argument, "--CreateGame", StringComparison.Ordinal) &&
            !string.Equals(argument, "--Map", StringComparison.Ordinal) &&
            !string.Equals(argument, "--passive-baseline", StringComparison.Ordinal) &&
            !string.Equals(argument, "--diagnostics", StringComparison.Ordinal) &&
            !string.Equals(argument, "--no-diagnostics", StringComparison.Ordinal) &&
            !(index > 0 && args[index - 1] == "--Map")).ToArray();
        var localRun = connectionArgs.Length == 0;
        if (passiveBaseline && !localRun)
            throw new ArgumentException("--passive-baseline is a local-run option, not a ladder argument.");
        if (args.Contains("--no-diagnostics", StringComparer.Ordinal) && !localRun)
            throw new ArgumentException("--no-diagnostics is a local-run option, not a ladder argument.");

        // Local matches capture by default. Ladder hosts remain file-silent unless
        // the operator explicitly opts in, since host filesystem persistence varies.
        var writeDiagnostics = diagnostics && (localRun || diagnosticsRequested);
        var selectedConfiguration = string.IsNullOrWhiteSpace(selectedModeId)
            ? MatchConfiguration
            : BotMatchConfiguration.ForMode(selectedModeId);
        var requestedRace = selectedConfiguration.RequestedRace;

        SingularityBot bot;
        if (string.IsNullOrWhiteSpace(selectedModeId) ||
            string.Equals(selectedModeId, "terran.marine.v1", StringComparison.Ordinal))
        {
            var strategy = new AdaptiveMarineStrategy();
            var definition = strategy.Definition;
            Console.WriteLine($"[BotStrategy] Using compiled '{definition.Id}' ({definition.Name}), strategy={definition.StrategyProfile}, mode={definition.ModeId}.");
            bot = new SingularityBot(
                passiveBaseline: passiveBaseline,
                writeDiagnostics: writeDiagnostics,
                compiledStrategy: strategy);
        }
        else if (string.Equals(selectedModeId, "zerg.zergling.v1", StringComparison.Ordinal))
        {
            var definition = BotDefinitionCatalog.CreateZerglingOpening();
            Console.WriteLine($"[BotStrategy] Using experimental '{definition.Id}' ({definition.Name}), mode={definition.ModeId}.");
            bot = new SingularityBot(
                passiveBaseline: passiveBaseline,
                writeDiagnostics: writeDiagnostics,
                botDefinition: definition);
        }
        else
        {
            // ForMode is the single allow-list and normally rejects before this point.
            throw new ArgumentException($"Unsupported runner mode '{selectedModeId}'.");
        }
        Console.WriteLine($"[MatchConfiguration] Requested race={requestedRace}.");

        if (designatedAreaArgument is not null)
        {
            var coordinates = designatedAreaArgument["--cc-area=".Length..]
                .Split(',', StringSplitOptions.TrimEntries);
            if (coordinates.Length != 3 ||
                !float.TryParse(coordinates[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var centerX) ||
                !float.TryParse(coordinates[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var centerY) ||
                !float.TryParse(coordinates[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var radius) ||
                !float.IsFinite(centerX) || !float.IsFinite(centerY) ||
                !float.IsFinite(radius) || radius <= 0)
                throw new ArgumentException("--cc-area must be supplied as --cc-area=centerX,centerY,radius using finite map coordinates and a positive radius.");

            bot.ConfigureDesignatedCommandCenterConstructionArea(centerX, centerY, radius);
        }

        var connection = new GameConnection();
        try
        {
            beforeConnect?.Invoke(bot);
            if (localRun)
                connection.RunSinglePlayer(bot, selectedConfiguration.MapName, requestedRace,
                    selectedConfiguration.OpponentRace, selectedConfiguration.OpponentDifficulty).GetAwaiter().GetResult();
            else
                connection.RunLadder(bot, requestedRace, connectionArgs).GetAwaiter().GetResult();
        }
        finally
        {
            afterConnect?.Invoke();
        }
    }
}
