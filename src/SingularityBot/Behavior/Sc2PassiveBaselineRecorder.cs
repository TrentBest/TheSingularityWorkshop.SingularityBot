using System.Diagnostics;
using System.Globalization;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Records a local, zero-command economic baseline. The recorder is opt-in and
/// writes only from local runs; ladder execution does not require filesystem access.
/// </summary>
public sealed class Sc2PassiveBaselineRecorder
{
    private const uint MineralTarget = 300;
    private readonly List<string> _rows = new()
    {
        "gameLoop,elapsedSeconds,minerals,foodUsed,foodCap,scvCount,scvsWithOrders,distinctMineralTargets,scvsWithCargo"
    };
    private readonly List<string> _workerRows = new()
    {
        "gameLoop,elapsedSeconds,workerTag,x,y,hasCargo,orderAbilityId,targetUnitTag,targetX,targetY,distanceToTarget"
    };

    private long _startedAt;
    private uint _lastSampleLoop;
    private uint? _startingMinerals;
    private double? _timeToTargetSeconds;
    private uint? _targetReachedAtLoop;

    public void Start() => _startedAt = Stopwatch.GetTimestamp();

    public void Record(NormalizedObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var elapsed = Stopwatch.GetElapsedTime(_startedAt).TotalSeconds;
        var workers = observation.Units
            .Where(unit => unit.Alliance == UnitAlliance.Self &&
                           unit.IsLiveVisible &&
                           unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var targets = workers
            .Select(worker => worker.Orders.FirstOrDefault()?.TargetUnitTag)
            .Where(tag => tag.HasValue)
            .Select(tag => tag.Value)
            .Distinct()
            .Count();

        var visibleUnits = observation.Units
            .Where(unit => unit.IsLiveVisible && unit.HasPosition)
            .ToDictionary(unit => unit.Tag);
        foreach (var worker in workers)
        {
            var order = worker.Orders.FirstOrDefault();
            UnitObservationFacts target = null;
            var hasTarget = order?.TargetUnitTag is ulong targetTag &&
                            visibleUnits.TryGetValue(targetTag, out target);
            var distance = hasTarget
                ? MathF.Sqrt(
                    MathF.Pow(worker.PositionX - target.PositionX, 2) +
                    MathF.Pow(worker.PositionY - target.PositionY, 2))
                : (float?)null;
            _workerRows.Add(string.Join(",",
                observation.GameLoop.ToString(CultureInfo.InvariantCulture),
                elapsed.ToString("F3", CultureInfo.InvariantCulture),
                worker.Tag.ToString(CultureInfo.InvariantCulture),
                worker.PositionX.ToString("F3", CultureInfo.InvariantCulture),
                worker.PositionY.ToString("F3", CultureInfo.InvariantCulture),
                worker.HasCargo ? "1" : "0",
                order?.AbilityId.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                order?.TargetUnitTag?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                hasTarget ? target.PositionX.ToString("F3", CultureInfo.InvariantCulture) : string.Empty,
                hasTarget ? target.PositionY.ToString("F3", CultureInfo.InvariantCulture) : string.Empty,
                distance?.ToString("F3", CultureInfo.InvariantCulture) ?? string.Empty));
        }

        _startingMinerals ??= observation.Minerals;
        if (observation.Minerals >= MineralTarget && _timeToTargetSeconds is null)
        {
            _timeToTargetSeconds = elapsed;
            _targetReachedAtLoop = observation.GameLoop;
        }

        if (_rows.Count == 1 || observation.GameLoop >= _lastSampleLoop + 22 ||
            observation.Minerals >= MineralTarget && _targetReachedAtLoop == observation.GameLoop)
        {
            _rows.Add(string.Join(",",
                observation.GameLoop.ToString(CultureInfo.InvariantCulture),
                elapsed.ToString("F3", CultureInfo.InvariantCulture),
                observation.Minerals.ToString(CultureInfo.InvariantCulture),
                observation.FoodUsed.ToString(CultureInfo.InvariantCulture),
                observation.FoodCap.ToString(CultureInfo.InvariantCulture),
                workers.Length.ToString(CultureInfo.InvariantCulture),
                workers.Count(worker => worker.HasOrders).ToString(CultureInfo.InvariantCulture),
                targets.ToString(CultureInfo.InvariantCulture),
                workers.Count(worker => worker.HasCargo).ToString(CultureInfo.InvariantCulture)));
            _lastSampleLoop = observation.GameLoop;
        }
    }

    public void Write(string directory)
    {
        Directory.CreateDirectory(directory);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
        var path = Path.Combine(directory, $"passive-baseline-{timestamp}.csv");
        File.WriteAllLines(path, _rows);
        var workerPath = Path.Combine(directory, $"passive-baseline-workers-{timestamp}.csv");
        File.WriteAllLines(workerPath, _workerRows);
        var reached = _timeToTargetSeconds is null
            ? "not reached"
            : $"{_timeToTargetSeconds.Value:F3}s at game loop {_targetReachedAtLoop}";
        Console.WriteLine($"[Baseline] Zero-intervention run: start minerals={_startingMinerals?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}, time to 300 minerals={reached}, samples={_rows.Count - 1}, trace={path}, worker-frame-rows={_workerRows.Count - 1}, worker-trace={workerPath}.");
    }
}
