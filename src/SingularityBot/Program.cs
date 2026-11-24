#nullable enable annotations
using SC2API_CSharp;
using SC2APIProtocol;
using System.Globalization;
using SingularityBot.Behavior;

namespace SingularityBot;

public static class Program
{
    private static readonly BotMatchConfiguration MatchConfiguration = BotMatchConfiguration.Default;

    public static void Run(string[] args, Action<SingularityBot> beforeConnect = null, System.Action afterConnect = null)
    {
        args ??= [];
        if (!args.Contains("--no-diagnostics", StringComparer.Ordinal))
            Sc2Diagnostics.StartSessionLog();

        var passiveBaseline = args.Contains("--passive-baseline", StringComparer.Ordinal);
        var diagnostics = !args.Contains("--no-diagnostics", StringComparer.Ordinal);
        var diagnosticsRequested = args.Contains("--diagnostics", StringComparer.Ordinal);
        var designatedAreaArgument = args.FirstOrDefault(argument =>
            argument.StartsWith("--cc-area=", StringComparison.Ordinal));
        var modeArgument = args.FirstOrDefault(argument =>
            argument.StartsWith("--mode=", StringComparison.Ordinal));
        var raceArgument = args.FirstOrDefault(argument =>
            argument.StartsWith("--race=", StringComparison.Ordinal));
        var definitionArgument = args.FirstOrDefault(argument =>
            argument.StartsWith("--definition=", StringComparison.Ordinal));
        var selectedModeId = modeArgument?["--mode=".Length..];
        Race? explicitRace = null;
        if (raceArgument is not null)
        {
            var raceText = raceArgument["--race=".Length..];
            if (!Enum.TryParse<Race>(raceText, ignoreCase: true, out var parsedRace) ||
                parsedRace is not (Race.Terran or Race.Zerg or Race.Protoss))
                throw new ArgumentException("--race must be one of: terran, zerg, protoss.", nameof(args));
            explicitRace = parsedRace;
            if (string.IsNullOrWhiteSpace(selectedModeId))
                selectedModeId = parsedRace switch
                {
                    Race.Terran => "terran.marine.v1",
                    Race.Zerg => "zerg.zergling.v1",
                    Race.Protoss => ProtossZealotOpener.Id,
                    _ => throw new ArgumentOutOfRangeException(nameof(parsedRace), parsedRace, "Unsupported StarCraft II race.")
                };
        }
        var connectionArgs = args.Where((argument, index) =>
            !argument.StartsWith("--cc-area=", StringComparison.Ordinal) &&
            !argument.StartsWith("--mode=", StringComparison.Ordinal) &&
            !argument.StartsWith("--race=", StringComparison.Ordinal) &&
            !argument.StartsWith("--definition=", StringComparison.Ordinal) &&
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
            throw new ArgumentException("--passive-baseline is a local-run option, not a ladder argument.", nameof(args));
        if (args.Contains("--no-diagnostics", StringComparer.Ordinal) && !localRun)
            throw new ArgumentException("--no-diagnostics is a local-run option, not a ladder argument.", nameof(args));

        // Diagnostic capture is the default for every launch path, including
        // IDE Play. Ladder operators may opt in explicitly; only an explicit
        // --no-diagnostics disables local capture.
        var writeDiagnostics = diagnostics && (localRun || diagnosticsRequested);
        BotDefinition selectedDefinition;
        BotMatchConfiguration selectedConfiguration;
        if (definitionArgument is not null)
        {
            if (!string.IsNullOrWhiteSpace(selectedModeId) || explicitRace is not null)
                throw new ArgumentException("--definition cannot be combined with --mode or --race; edit the definition artifact instead.", nameof(args));
            var definitionPath = definitionArgument["--definition=".Length..];
            if (string.IsNullOrWhiteSpace(definitionPath) || !File.Exists(definitionPath))
                throw new FileNotFoundException("The supplied BotDefinition JSON file does not exist.", definitionPath);
            selectedDefinition = BotDefinition.Parse(File.ReadAllText(definitionPath));
            selectedConfiguration = BotMatchConfiguration.ForMode(selectedDefinition.ModeId);
            if (!Enum.TryParse<Race>(selectedDefinition.Race, ignoreCase: false, out var definitionRace) ||
                definitionRace != selectedConfiguration.RequestedRace)
                throw new InvalidOperationException($"BotDefinition race {selectedDefinition.Race} does not match mode {selectedDefinition.ModeId}.");
        }
        else
        {
            selectedConfiguration = string.IsNullOrWhiteSpace(selectedModeId)
                ? MatchConfiguration
                : BotMatchConfiguration.ForMode(selectedModeId);
            if (explicitRace is Race race && selectedConfiguration.RequestedRace != race)
                throw new ArgumentException($"Configured mode '{selectedModeId}' requires {selectedConfiguration.RequestedRace}, but --race selected {race}.", nameof(args));
            selectedDefinition = BotDefinitionCatalog.CreateForMode(
                selectedModeId ?? BotDefinitionCatalog.DefaultModeId);
        }
        var requestedRace = selectedConfiguration.RequestedRace;
        Console.WriteLine($"[BotStrategy] Using compiled '{selectedDefinition.Id}' ({selectedDefinition.Name}), race={selectedDefinition.Race}, mode={selectedDefinition.ModeId}.");
        var bot = new SingularityBot(
            passiveBaseline: passiveBaseline,
            writeDiagnostics: writeDiagnostics,
            botDefinition: selectedDefinition);
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
                throw new ArgumentException("--cc-area must be supplied as --cc-area=centerX,centerY,radius using finite map coordinates and a positive radius.", nameof(args));

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
