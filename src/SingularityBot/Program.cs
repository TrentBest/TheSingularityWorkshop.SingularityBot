using SC2API_CSharp;
using SC2APIProtocol;
using System.Globalization;
using SingularityBot.Behavior;

namespace SingularityBot;

public static class Program
{
    private static readonly BotMatchConfiguration MatchConfiguration = BotMatchConfiguration.Default;

    public static void Run(string[] args)
    {
        var passiveBaseline = args.Contains("--passive-baseline", StringComparer.Ordinal);
        var diagnostics = !args.Contains("--no-diagnostics", StringComparer.Ordinal);
        var designatedAreaArgument = args.FirstOrDefault(argument =>
            argument.StartsWith("--cc-area=", StringComparison.Ordinal));

        var connectionArgs = args.Where((argument, index) =>
            !argument.StartsWith("--cc-area=", StringComparison.Ordinal) &&
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

        var strategy = new AdaptiveMarineStrategy();
        var definition = strategy.Definition;
        Console.WriteLine($"[BotStrategy] Using compiled '{definition.Id}' ({definition.Name}), strategy={definition.StrategyProfile}.");
        Console.WriteLine($"[MatchConfiguration] Requested race={MatchConfiguration.RequestedRace}.");
        var bot = new SingularityBot(
            passiveBaseline: passiveBaseline,
            writeDiagnostics: localRun && diagnostics,
            compiledStrategy: strategy);

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
        if (localRun)
            connection.RunSinglePlayer(bot, MatchConfiguration.MapName, MatchConfiguration.RequestedRace,
                MatchConfiguration.OpponentRace, MatchConfiguration.OpponentDifficulty).GetAwaiter().GetResult();
        else
            connection.RunLadder(bot, MatchConfiguration.RequestedRace, connectionArgs).GetAwaiter().GetResult();
    }
}
