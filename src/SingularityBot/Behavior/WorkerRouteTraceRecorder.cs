using System.Globalization;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Per-observation worker logistics trace. This recorder is observational only:
/// it captures assignments, observed orders, patch contents, and SC2 pathing
/// evidence without changing worker commands or assignment decisions.
/// </summary>
public sealed class WorkerRouteTraceRecorder : IDisposable
{
    private StreamWriter? _writer;
    private StreamWriter? _latestWriter;
    private string? _path;
    private string? _latestPath;
    private string _mapName = string.Empty;
    private string _definitionId = string.Empty;
    private string _modeId = string.Empty;

    public void Start(string directory, string mapName, string definitionId, string modeId)
    {
        try
        {
            Dispose();
            Directory.CreateDirectory(directory);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
            _path = Path.Combine(directory, $"worker-route-{timestamp}.csv");
            _latestPath = Path.Combine(directory, "worker-route-latest.csv");
            _mapName = mapName ?? string.Empty;
            _definitionId = definitionId ?? string.Empty;
            _modeId = modeId ?? string.Empty;
            _writer = new StreamWriter(_path, false);
            _latestWriter = new StreamWriter(_latestPath, false);
            const string header = "mapName,definitionId,modeId,gameLoop,mineralsBank,vespeneBank,workerTag,assignmentGeneration,routePhase,phaseAgeGameLoops,mineralTag,mineralContents,townHallTag,workerX,workerY,workerCargo,orderAbilityId,orderTargetUnitTag,orderTargetX,orderTargetY,workerToMineralPathingDistance,townHallToMineralPathingDistance";
            _writer.WriteLine(header);
            _latestWriter.WriteLine(header);
            _writer.Flush();
            _latestWriter.Flush();
            Console.WriteLine($"[Diagnostics] Worker route trace: {_path}");
            Console.WriteLine($"[Diagnostics] Latest worker route trace: {_latestPath}");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"[Diagnostics] Worker route trace unavailable: {exception.Message}");
            _writer?.Dispose();
            _writer = null;
            _latestWriter?.Dispose();
            _latestWriter = null;
        }
    }

    public void RecordFrame(
        NormalizedObservation observation,
        IReadOnlyCollection<UnitBehaviorContext> contexts,
        WorkerResourceAssignmentPlanner assignments)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(assignments);
        if (_writer is null)
            return;

        var contextsByTag = contexts.ToDictionary(context => context.UnitTag);
        var unitsByTag = observation.Units.ToDictionary(unit => unit.Tag);

        foreach (var worker in observation.Units
                     .Where(unit => unit.Alliance == UnitAlliance.Self &&
                                    unit.IsLiveVisible &&
                                    unit.UnitKind.Equals("SCV", StringComparison.OrdinalIgnoreCase) &&
                                    unit.HasPosition &&
                                    !observation.DeadUnitTags.Contains(unit.Tag))
                     .OrderBy(unit => unit.Tag))
        {
            contextsByTag.TryGetValue(worker.Tag, out var context);
            var mineralTag = context?.AssignedMineralTag;
            var townHallTag = context?.AssignedTownHallTag;
            UnitObservationFacts? mineral = null;
            if (mineralTag is ulong mineralId)
                unitsByTag.TryGetValue(mineralId, out mineral);

            var order = worker.Orders.FirstOrDefault();
            var workerPathingDistance = mineralTag is ulong assignedMineral &&
                assignments.TryGetPathingDistance(worker.Tag, assignedMineral, out var workerDistance)
                    ? F(workerDistance)
                    : string.Empty;
            var townHallPathingDistance = mineralTag is ulong assignedPatch &&
                townHallTag is ulong assignedHall &&
                assignments.TryGetPathingDistance(assignedHall, assignedPatch, out var hallDistance)
                    ? F(hallDistance)
                    : string.Empty;
            var phaseAge = context is null || observation.GameLoop < context.WorkerRoutePhaseChangedAtGameLoop
                ? string.Empty
                : (observation.GameLoop - context.WorkerRoutePhaseChangedAtGameLoop)
                    .ToString(CultureInfo.InvariantCulture);

            var row = string.Join(",", new[]
            {
                Csv(_mapName),
                Csv(_definitionId),
                Csv(_modeId),
                N(observation.GameLoop),
                N(observation.Minerals),
                N(observation.Vespene),
                N(worker.Tag),
                N(context?.WorkerAssignmentGeneration ?? 0),
                Csv(context?.WorkerRoutePhase.ToString() ?? "Unknown"),
                phaseAge,
                mineralTag?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                N(mineral?.MineralContents ?? 0),
                townHallTag?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                F(worker.PositionX),
                F(worker.PositionY),
                B(worker.HasCargo),
                order?.AbilityId.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                order?.TargetUnitTag?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                order?.TargetPositionX is float targetX ? F(targetX) : string.Empty,
                order?.TargetPositionY is float targetY ? F(targetY) : string.Empty,
                workerPathingDistance,
                townHallPathingDistance
            });
            _writer.WriteLine(row);
            _latestWriter?.WriteLine(row);
        }

        _writer.Flush();
        _latestWriter?.Flush();
    }

    public void Dispose()
    {
        _writer?.Flush();
        _writer?.Dispose();
        _writer = null;
        _latestWriter?.Flush();
        _latestWriter?.Dispose();
        _latestWriter = null;
        if (_path is not null)
            Console.WriteLine($"[Diagnostics] Worker route trace saved: {_path}");
        if (_latestPath is not null)
            Console.WriteLine($"[Diagnostics] Latest worker route trace saved: {_latestPath}");
        _path = null;
        _latestPath = null;
    }

    private static string N(uint value) => value.ToString(CultureInfo.InvariantCulture);
    private static string N(ulong value) => value.ToString(CultureInfo.InvariantCulture);
    private static string B(bool value) => value ? "1" : "0";
    private static string F(float value) => value.ToString("F3", CultureInfo.InvariantCulture);
    private static string F(double value) => value.ToString("F3", CultureInfo.InvariantCulture);
    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
