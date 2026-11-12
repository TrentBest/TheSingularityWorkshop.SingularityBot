using SingularityBot;

if (args.Length == 1 && args[0] is "--help" or "-h")
{
    Console.WriteLine("SingularityBot runner");
    Console.WriteLine("No arguments: launch local single-player mode.");
    Console.WriteLine("--passive-baseline: run a local match with zero bot actions and record economic/worker CSV traces.");
    Console.WriteLine("Local runs capture protocol snapshots, terrain, CSV traces, and binary event timelines by default.");
    Console.WriteLine("--no-diagnostics: disable local diagnostic capture.");
    Console.WriteLine("--cc-area=centerX,centerY,radius: configure the designated Command Center construction zone in map coordinates.");
    Console.WriteLine("Ladder arguments: forwarded to the SC2 ladder connection.");
    return;
}

SingularityBot.Program.Run(args);
