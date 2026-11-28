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
        var diagnosticsDirectoryArgument = args.FirstOrDefault(argument =>
            argument.StartsWith("--diagnostics-dir=", StringComparison.Ordinal));
        var matchOutputDirectoryArgument = args.FirstOrDefault(argument =>
            argument.StartsWith("--match-output-dir=", StringComparison.Ordinal));
        var matchRunStartedAtUtc = DateTime.UtcNow;
        if (diagnosticsDirectoryArgument is not null)
        {
            var requestedDirectory = diagnosticsDirectoryArgument["--diagnostics-dir=".Length..];
            if (string.IsNullOrWhiteSpace(requestedDirectory))
                throw new ArgumentException("--diagnostics-dir requires a non-empty path.", nameof(args));
            Sc2Diagnostics.ConfigureOutputDirectory(requestedDirectory);
        }
        if (matchOutputDirectoryArgument is not null &&
            string.IsNullOrWhiteSpace(matchOutputDirectoryArgument["--match-output-dir=".Length..]))
            throw new ArgumentException("--match-output-dir requires a non-empty path.", nameof(args));

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
        var mapArgument = args.FirstOrDefault(argument =>
            argument.StartsWith("--map=", StringComparison.Ordinal));
        var requestedMap = mapArgument?["--map=".Length..];
        if (mapArgument is not null && string.IsNullOrWhiteSpace(requestedMap))
            throw new ArgumentException("--map requires a non-empty local map name or path.", nameof(args));
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
            !argument.StartsWith("--map=", StringComparison.Ordinal) &&
            !argument.StartsWith("--diagnostics-dir=", StringComparison.Ordinal) &&
            !argument.StartsWith("--match-output-dir=", StringComparison.Ordinal) &&
            !argument.StartsWith("--studio-bridge-port=", StringComparison.Ordinal) &&
            !string.Equals(argument, "--studio-bridge", StringComparison.Ordinal) &&
            !string.Equals(argument, "--CreateGame", StringComparison.Ordinal) &&
            !string.Equals(argument, "--Map", StringComparison.Ordinal) &&
            !string.Equals(argument, "--passive-baseline", StringComparison.Ordinal) &&
            !string.Equals(argument, "--diagnostics", StringComparison.Ordinal) &&
            !string.Equals(argument, "--no-diagnostics", StringComparison.Ordinal) &&
            !(index > 0 && args[index - 1] == "--Map")).ToArray();
        var localRun = connectionArgs.Length == 0;
        if (mapArgument is not null && !localRun)
            throw new ArgumentException("--map is a local training option and cannot be combined with ladder connection arguments.", nameof(args));
        if ((diagnosticsDirectoryArgument is not null || matchOutputDirectoryArgument is not null) && !localRun)
            throw new ArgumentException("--diagnostics-dir and --match-output-dir are local-run options, not ladder arguments.", nameof(args));
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
            selectedConfiguration = BotMatchConfiguration.Default;
            switch (selectedDefinition.RaceSelection.Policy)
            {
                case BotRaceSelectionPolicy.Fixed:
                    selectedConfiguration = BotMatchConfiguration.ForMode(selectedDefinition.ModeId);
                    if (!Enum.TryParse<Race>(selectedDefinition.Race, ignoreCase: false, out var definitionRace) ||
                        definitionRace != selectedConfiguration.RequestedRace ||
                        selectedDefinition.RaceSelection.FixedRace != definitionRace)
                        throw new InvalidOperationException($"BotDefinition race {selectedDefinition.Race}, fixed race {selectedDefinition.RaceSelection.FixedRace}, and mode {selectedDefinition.ModeId} must agree.");
                    break;

                case BotRaceSelectionPolicy.Random:
                    var selectionContext = new BotRaceSelectionContext(
                        selectedConfiguration.OpponentRace,
                        selectedConfiguration.OpponentDifficulty,
                        selectedConfiguration.MapName);
                    var selection = new BotRaceSelectionResolver().Resolve(
                        selectedDefinition.RaceSelection,
                        RaceModuleCatalog.CreateDefault().RaceSelectionEligibleRaces,
                        selectionContext);
                    selectedDefinition = BotDefinitionCatalog.RebindRace(selectedDefinition, selection.SelectedRace);
                    selectedConfiguration = BotMatchConfiguration.ForMode(selectedDefinition.ModeId);
                    Console.WriteLine($"[RaceSelection] Randomly selected {selection.SelectedRace} from race-selection-eligible modes: {string.Join(", ", selection.EligibleExecutableRaces)}.");
                    break;

                case BotRaceSelectionPolicy.Custom:
                    throw new InvalidOperationException("Custom race selection requires a trusted selector registration in the Runner host; no custom selector is registered.");

                default:
                    throw new InvalidOperationException($"Unsupported race-selection policy {selectedDefinition.RaceSelection.Policy}.");
            }
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
        if (requestedMap is not null)
            selectedConfiguration = selectedConfiguration with { MapName = requestedMap };
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
            try
            {
                if (matchOutputDirectoryArgument is not null && diagnostics)
                {
                    var sourceDirectory = Path.GetFullPath(matchOutputDirectoryArgument["--match-output-dir=".Length..]);
                    MatchArtifactCollector.Collect(sourceDirectory, Sc2Diagnostics.OutputDirectory, matchRunStartedAtUtc);
                }
            }
            finally
            {
                afterConnect?.Invoke();
            }
        }
    }
}
