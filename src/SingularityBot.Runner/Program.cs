using SingularityBot;

if (args.Length == 1 && args[0] is "--help" or "-h")
{
    Console.WriteLine("SingularityBot runner");
    Console.WriteLine("No arguments: launch local single-player mode.");
    Console.WriteLine("--passive-baseline: run a local match with zero bot actions and record economic/worker CSV traces.");
    Console.WriteLine("--diagnostics: opt in to local protocol snapshots, terrain captures, and per-frame CSV traces.");
    Console.WriteLine("Diagnostics are off by default for normal local runs.");
    Console.WriteLine("Ladder arguments: forwarded to the SC2 ladder connection.");
    return;
}

SingularityBot.Program.Run(args);
