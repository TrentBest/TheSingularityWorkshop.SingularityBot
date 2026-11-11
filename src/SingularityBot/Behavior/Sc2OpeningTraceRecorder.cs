using System.Globalization;
using SingularityBot.Observation;
using SingularityBot.Spatial;

namespace SingularityBot.Behavior;

/// <summary>
/// Writes a per-frame trace of the opening build-order gates beside the other
/// local diagnostics. This is intentionally observational: it never changes
/// command selection or timing.
/// </summary>
public sealed class Sc2OpeningTraceRecorder : IDisposable
{
    private StreamWriter _writer;
    private string _path;

    public void Start(string directory)
    {
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, $"opening-trace-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}.csv");
        _writer = new StreamWriter(_path, false);
        _writer.WriteLine("gameLoop,minerals,vespene,foodUsed,foodCap,scvCount,marineCount,commandCenterCount,orbitalCount,refineryCount,engineeringBayCount,barracksCount,depotCount,enemyVisibleCount,enemyMarineCount,enemyRangedCount,activeBuildJobs,builderTag,builderX,builderY,candidateCount,placementQueryPending,placementX,placementY,moveSubmitted,dispatched,buildSubmitted,completed,depotCount,barracksCount,marineCount,builderActionAbility,builderActionX,builderActionY,marineActionSubmitted,spatialMapAvailable");
        _writer.Flush();
        Console.WriteLine($"[Diagnostics] Opening trace: {_path}");
    }

    public void RecordFrame(
        NormalizedObservation observation,
        Sc2OpeningBuilderController builder,
        SC2APIProtocol.Action builderAction,
        bool marineActionSubmitted,
        bool spatialMapAvailable,
        int activeBuildJobs = 0)
    {
        if (_writer is null)
            return;

        var units = observation.Units;
        var scvs = units.Where(unit => Is(unit, "SCV")).ToArray();
        var commandCenters = units.Where(unit => Is(unit, "CommandCenter") || Is(unit, "OrbitalCommand")).ToArray();
        var enemies = units.Where(unit => unit.Alliance == UnitAlliance.Enemy && unit.IsLiveVisible).ToArray();
        var builderUnit = builder.BuilderTag is ulong tag
            ? units.FirstOrDefault(unit => unit.Tag == tag)
            : null;
        var placement = builder.Placement;
        var command = builderAction?.ActionRaw?.UnitCommand;
        var target = command?.TargetWorldSpacePos;
        var row = new[]
        {
            N(observation.GameLoop), N(observation.Minerals), N(observation.Vespene), N(observation.FoodUsed), N(observation.FoodCap),
            N(scvs.Length), N(units.Count(unit => Is(unit, "Marine"))), N(commandCenters.Length),
            N(units.Count(unit => Is(unit, "OrbitalCommand"))), N(units.Count(unit => Is(unit, "Refinery"))),
            N(units.Count(unit => Is(unit, "EngineeringBay"))), N(units.Count(unit => Is(unit, "Barracks"))),
            N(units.Count(unit => Is(unit, "SupplyDepot") || Is(unit, "SupplyDepotLowered"))),
            N(enemies.Length), N(enemies.Count(unit => unit.UnitKind.Equals("Marine", StringComparison.OrdinalIgnoreCase))),
            N(enemies.Count(unit => unit.UnitKind.Contains("Marauder", StringComparison.OrdinalIgnoreCase) ||
                unit.UnitKind.Contains("Stalker", StringComparison.OrdinalIgnoreCase) ||
                unit.UnitKind.Contains("Roach", StringComparison.OrdinalIgnoreCase) ||
                unit.UnitKind.Contains("Hydralisk", StringComparison.OrdinalIgnoreCase))),
            N(activeBuildJobs),
            builder.BuilderTag?.ToString(CultureInfo.InvariantCulture) ?? "",
            builderUnit?.PositionX.ToString("F3", CultureInfo.InvariantCulture) ?? "",
            builderUnit?.PositionY.ToString("F3", CultureInfo.InvariantCulture) ?? "",
            N(builder.CandidateCount), B(builder.IsPlacementQueryPending),
            placement?.X.ToString("F3", CultureInfo.InvariantCulture) ?? "",
            placement?.Y.ToString("F3", CultureInfo.InvariantCulture) ?? "",
            B(builder.IsMoveSubmitted), B(builder.IsDispatched), B(builder.IsBuildSubmitted), B(builder.IsCompleted),
            command?.AbilityId.ToString(CultureInfo.InvariantCulture) ?? "",
            target?.X.ToString("F3", CultureInfo.InvariantCulture) ?? "",
            target?.Y.ToString("F3", CultureInfo.InvariantCulture) ?? "",
            B(marineActionSubmitted), B(spatialMapAvailable)
        };
        _writer.WriteLine(string.Join(",", row));
    }

    public void RecordEvent(string eventName, uint gameLoop, string details)
    {
        if (_writer is null)
            return;
        // Events are written as comment rows so the CSV remains easy to inspect.
        _writer.WriteLine($"#event,{gameLoop},{Escape(eventName)},{Escape(details)}");
        _writer.Flush();
    }

    public void Dispose()
    {
        _writer?.Flush();
        _writer?.Dispose();
        _writer = null;
        if (_path is not null)
            Console.WriteLine($"[Diagnostics] Opening trace saved: {_path}");
    }

    private static bool Is(UnitObservationFacts unit, string kind) =>
        unit.Alliance == UnitAlliance.Self &&
        unit.IsLiveVisible &&
        unit.UnitKind.Equals(kind, StringComparison.OrdinalIgnoreCase);

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string N(uint value) => value.ToString(CultureInfo.InvariantCulture);
    private static string B(bool value) => value ? "1" : "0";
    private static string Escape(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
}
