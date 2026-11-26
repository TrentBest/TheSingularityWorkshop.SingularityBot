using SC2APIProtocol;

namespace SingularityBot.Behavior;

/// <summary>Race-owned runtime boundary. Shared bot infrastructure depends only on this contract.</summary>
public interface IRace
{
    Race Race { get; }
    string Id { get; }
}

/// <summary>Zerg-specific runtime capabilities and policies.</summary>
public interface IZerg : IRace
{
}

/// <summary>Terran-specific runtime capabilities and policies.</summary>
public interface ITerran : IRace
{
    EconomyAdvisor Economy { get; }
    IntelligenceAdvisor Intelligence { get; }
    ScoutingAdvisor Scouting { get; }
}

/// <summary>Protoss-specific runtime capabilities and policies.</summary>
public interface IProtoss : IRace
{
}

/// <summary>Creates the race-owned runtime selected by the compiled bot definition.</summary>
public static class RaceRuntimeFactory
{
    public static IRace Create(string race) => race?.Trim().ToLowerInvariant() switch
    {
        "zerg" => new SingularityZerg(),
        "terran" => new SingularityTerran(),
        "protoss" => new SingularityProtoss(),
        _ => throw new ArgumentException($"Unsupported or missing race '{race}'. A race must be selected by the host definition.", nameof(race))
    };
}
