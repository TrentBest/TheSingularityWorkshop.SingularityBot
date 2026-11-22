using SC2APIProtocol;

namespace SingularityBot.Behavior;

/// <summary>Terran implementation owns Terran economy, intelligence and scouting policy.</summary>
public sealed class SingularityTerran : ITerran
{
    public Race Race => SC2APIProtocol.Race.Terran;
    public string Id => "singularity.race.terran.v1";
    public EconomyAdvisor Economy { get; } = new();
    public IntelligenceAdvisor Intelligence { get; } = new();
    public ScoutingAdvisor Scouting { get; } = new();
}

/// <summary>Zerg race composition. Race-specific behavior is added here, not to the protocol shell.</summary>
public sealed class SingularityZerg : IZerg
{
    public Race Race => SC2APIProtocol.Race.Zerg;
    public string Id => "singularity.race.zerg.v1";
}

/// <summary>Protoss race composition. Race-specific behavior is added here, not to the protocol shell.</summary>
public sealed class SingularityProtoss : IProtoss
{
    public Race Race => SC2APIProtocol.Race.Protoss;
    public string Id => "singularity.race.protoss.v1";
}
