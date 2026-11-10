using SC2API_CSharp;
using SC2APIProtocol;

namespace SingularityBot;

public static class Program
{
    private static readonly Race BotRace = Race.Terran;
    private static readonly string MapName = "AcolyteLE.SC2Map";
    private static readonly Race OpponentRace = Race.Random;
    private static readonly Difficulty OpponentDifficulty = Difficulty.VeryEasy;

    public static void Run(string[] args)
    {
        var passiveBaseline = args.Contains("--passive-baseline", StringComparer.Ordinal);
        var diagnostics = args.Contains("--diagnostics", StringComparer.Ordinal);
        var connectionArgs = args.Where(argument =>
            !string.Equals(argument, "--passive-baseline", StringComparison.Ordinal) &&
            !string.Equals(argument, "--diagnostics", StringComparison.Ordinal)).ToArray();
        var localRun = connectionArgs.Length == 0;
        if (passiveBaseline && !localRun)
            throw new ArgumentException("--passive-baseline is a local-run option, not a ladder argument.");
        if (diagnostics && !localRun)
            throw new ArgumentException("--diagnostics is a local-run option, not a ladder argument.");

        var bot = new SingularityBot(
            passiveBaseline: passiveBaseline,
            writeDiagnostics: localRun && (diagnostics || passiveBaseline));
        var connection = new GameConnection();
        if (localRun)
            connection.RunSinglePlayer(bot, MapName, BotRace, OpponentRace, OpponentDifficulty).GetAwaiter().GetResult();
        else
            connection.RunLadder(bot, BotRace, connectionArgs).GetAwaiter().GetResult();
    }
}
