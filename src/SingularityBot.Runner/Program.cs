using SingularityBot;

if (args.Length == 1 && args[0] is "--help" or "-h")
{
    Console.WriteLine("SingularityBot runner");
    Console.WriteLine("No arguments: launch local single-player mode.");
    Console.WriteLine("--passive-baseline: run a local match with zero bot actions and record economic/worker CSV traces.");
    Console.WriteLine("Ladder arguments: forwarded to the SC2 ladder connection.");
    return;
}

SingularityBot.Program.Run(args);
