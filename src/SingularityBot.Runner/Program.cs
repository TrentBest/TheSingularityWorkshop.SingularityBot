using SingularityBot;
using SingularityBot.Runner;

Console.WriteLine($"[RunnerDiagnostics] Assembly: {typeof(Program).Assembly.Location}");
Console.WriteLine($"[RunnerDiagnostics] Working directory: {Environment.CurrentDirectory}");
Console.WriteLine($"[RunnerDiagnostics] Argument count: {args.Length}");
for (var index = 0; index < args.Length; index++)
    Console.WriteLine($"[RunnerDiagnostics] args[{index}] = <{args[index]}>");

if (args.Length == 1 && args[0] is "--help" or "-h")
{
    Console.WriteLine("SingularityBot runner");
    Console.WriteLine("No arguments: launch local single-player mode.");
    Console.WriteLine("--mode=zerg.zergling.v1: run the experimental Zergling opening (Spawning Pool -> first Zergling pair).");
    Console.WriteLine("--map=<map-name-or-path>: select the map for this fresh local training session; launch a new Runner session after changing maps.");
    Console.WriteLine("--definition=<path>: load a validated BotDefinition JSON artifact saved by Studio.");
    Console.WriteLine("--passive-baseline: run a local match with zero bot actions and record economic/worker CSV traces.");
    Console.WriteLine("Local runs capture protocol snapshots, terrain, CSV traces, and binary event timelines by default.");
    Console.WriteLine("--no-diagnostics: disable local diagnostic capture.");
    Console.WriteLine("--cc-area=centerX,centerY,radius: configure the designated Command Center construction zone in map coordinates.");
    Console.WriteLine("Ladder mode is opt-in: --ladder --GamePort <port> --StartPort <port> --OpponentId <id>.");
    Console.WriteLine("Designated host: add --CreateGame --Map <trusted-map-path> to create the multiplayer game before joining.");
    Console.WriteLine("--studio-bridge: expose read-only Bot Studio telemetry on loopback. A bearer token is printed locally.");
    Console.WriteLine("--studio-bridge-port=43127: choose the loopback bridge port (requires --studio-bridge).");
    return;
}

var bridgeEnabled = args.Contains("--studio-bridge", StringComparer.Ordinal);
var portArgument = args.FirstOrDefault(argument =>
    argument.StartsWith("--studio-bridge-port=", StringComparison.Ordinal));
var port = 43127;
if (portArgument is not null &&
    (!bridgeEnabled || !int.TryParse(portArgument["--studio-bridge-port=".Length..], out port) || port is < 1024 or > 65535))
    throw new ArgumentException("--studio-bridge-port requires --studio-bridge and a port from 1024 through 65535.");

BotStudioBridge bridge = null;
SingularityBot.Program.Run(
    args,
    beforeConnect: bot =>
    {
        if (!bridgeEnabled) return;
        bridge = BotStudioBridge.StartAsync(bot, port).GetAwaiter().GetResult();
        Console.WriteLine($"[BotStudio] Read-only bridge: {bridge.Address}");
        Console.WriteLine($"[BotStudio] Bearer token (keep local): {bridge.Token}");
    },
    afterConnect: () =>
    {
        if (bridge is null) return;
        bridge.DisposeAsync().AsTask().GetAwaiter().GetResult();
        bridge = null;
    });
