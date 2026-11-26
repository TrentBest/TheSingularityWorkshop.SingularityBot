using System.Text.Json;

namespace SingularityBot.Behavior;

/// <summary>
/// Intelligence policy for retaining Orbital energy for tactical scans.
/// This policy is consumed by Economy Advisor's MULE spending gate; it does
/// not issue scan or MULE commands itself.
/// </summary>
public sealed class IntelligenceAdvisorSettings
{
    public int Version { get; init; } = 1;
    public bool PreserveOrbitalScanBeforeMules { get; init; } = true;
    public uint OrbitalScanEnergyReserve { get; init; } = 50;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Version != 1) errors.Add("$.version: only version 1 is supported.");
        if (OrbitalScanEnergyReserve > 200)
            errors.Add("$.orbitalScanEnergyReserve: must be between 0 and 200.");
        return errors;
    }
}

/// <summary>
/// Owns intelligence-side energy preservation settings used by Economy Advisor.
/// </summary>
public sealed class IntelligenceAdvisor : IStrategicAdvisor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AdvisorDomain Domain => AdvisorDomain.Intelligence;
    public GamePhase CurrentPhase { get; set; } = GamePhase.Early;
    public int ComputeWeight => AdvisorWorkScheduler.GetWeights(CurrentPhase)[Domain];
    public IntelligenceAdvisorSettings Settings { get; private set; } = new();

    public IReadOnlyList<string> ConfigureJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new[] { "$: intelligence settings JSON is required." };

        IntelligenceAdvisorSettings candidate;
        try
        {
            candidate = JsonSerializer.Deserialize<IntelligenceAdvisorSettings>(json, JsonOptions)
                ?? throw new JsonException("Intelligence settings JSON was empty.");
        }
        catch (JsonException exception)
        {
            return new[] { "$: invalid intelligence settings JSON: " + exception.Message };
        }

        var errors = candidate.Validate();
        if (errors.Count == 0)
            Settings = candidate;
        return errors;
    }

    /// <summary>
    /// Energy that Economy must leave available before authorizing a MULE.
    /// </summary>
    public uint GetMuleScanEnergyReserve(bool economyAlsoRequestsPreservation = false) =>
        Settings.PreserveOrbitalScanBeforeMules || economyAlsoRequestsPreservation
            ? Settings.OrbitalScanEnergyReserve
            : 0;
}
