using SC2API_CSharp;
using SC2APIProtocol;

namespace SingularityBot;

public static class Program
{
    private static readonly SC2API_CSharp.Bot Bot = new SingularityBot();
    private static readonly Race BotRace = Race.Terran;
    private static readonly string MapName = "InterloperLE.SC2Map";
    private static readonly Race OpponentRace = Race.Random;
    private static readonly Difficulty OpponentDifficulty = Difficulty.VeryEasy;

    public static void Run(string[] args)
    {
        var connection = new GameConnection();
        if (args.Length == 0)
            connection.RunSinglePlayer(Bot, MapName, BotRace, OpponentRace, OpponentDifficulty).GetAwaiter().GetResult();
        else
            connection.RunLadder(Bot, BotRace, args).GetAwaiter().GetResult();
    }
}
