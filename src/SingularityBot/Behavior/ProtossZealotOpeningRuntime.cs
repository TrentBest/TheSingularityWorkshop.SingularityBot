#nullable enable annotations
using SC2APIProtocol;
using SingularityBot.Observation;

namespace SingularityBot.Behavior;

/// <summary>
/// Bounded Protoss first-unit opening: Probe builds Pylon, then Gateway; once
/// the Gateway is observed complete and positively powered, it trains a Zealot.
/// Placement is always approved by SC2 and uncertain action submissions stay latched.
/// </summary>
public sealed class ProtossZealotOpeningRuntime
{
    private enum Stage { NeedPylonPlacement, PylonPlacementReady, PylonSubmitted, AwaitingPylon,
        NeedGatewayPlacement, GatewayPlacementReady, GatewaySubmitted, AwaitingGateway,
        Producing, Complete }

    private readonly Sc2UnitFacts _probe;
    private readonly Sc2UnitFacts _pylon;
    private readonly Sc2UnitFacts _gateway;
    private readonly Sc2UnitFacts _zealot;
    private readonly Sc2AbilityFacts _pylonBuild;
    private readonly Sc2AbilityFacts _gatewayBuild;
    private readonly Sc2RaceProductionProfile _profile;
    private readonly List<Point2D> _candidatePoints = new();
    private readonly HashSet<ulong> _reservedProducers = new();
    private Stage _stage = Stage.NeedPylonPlacement;
    private ulong _builderTag;
    private Point2D _placement;
    private bool _queryPending;
    private bool _queryInFlight;
    private bool _buildPending;
    private bool _productionPending;
    private int _placementAttempts;
    private bool _failureLogged;

    public ProtossZealotOpeningRuntime(
        Sc2UnitFacts probe, Sc2UnitFacts pylon, Sc2UnitFacts gateway,
        Sc2UnitFacts zealot, Sc2AbilityFacts pylonBuild, Sc2AbilityFacts gatewayBuild,
        Sc2RaceProductionProfile profile)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _pylon = pylon ?? throw new ArgumentNullException(nameof(pylon));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _zealot = zealot ?? throw new ArgumentNullException(nameof(zealot));
        _pylonBuild = pylonBuild ?? throw new ArgumentNullException(nameof(pylonBuild));
        _gatewayBuild = gatewayBuild ?? throw new ArgumentNullException(nameof(gatewayBuild));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        if (_pylon.AbilityId == 0 || _gateway.AbilityId == 0 ||
            _pylonBuild.AbilityId != _pylon.AbilityId || _gatewayBuild.AbilityId != _gateway.AbilityId ||
            !_pylonBuild.IsBuilding || !_gatewayBuild.IsBuilding ||
            !_profile.TargetUnit.Equals(_zealot.Name, StringComparison.OrdinalIgnoreCase) ||
            !_profile.ProducerUnit.Equals(_gateway.Name, StringComparison.OrdinalIgnoreCase) ||
            !_profile.RequiresPoweredProducer)
            throw new ArgumentException("Resolved Protoss building/production game data is inconsistent.");
    }

    public string State => _stage.ToString();
    public bool IsComplete => _stage == Stage.Complete;

    public IReadOnlyList<SC2APIProtocol.Action> OnFrame(NormalizedObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var pylonComplete = HasCompleted(observation, _pylon.Name);
        var gatewayComplete = HasCompleted(observation, _gateway.Name);
        if (_stage is Stage.PylonSubmitted or Stage.AwaitingPylon && pylonComplete)
        {
            _stage = Stage.NeedGatewayPlacement;
            _builderTag = 0;
            _placement = null;
            _placementAttempts = 0;
            _candidatePoints.Clear();
            _queryPending = false;
            _buildPending = false;
            Console.WriteLine("[ProtossOpening] Completed Pylon observed; proceeding to Gateway.");
        }
        if (_stage is Stage.GatewaySubmitted or Stage.AwaitingGateway && gatewayComplete)
        {
            _stage = Stage.Producing;
            _builderTag = 0;
            _placement = null;
            _placementAttempts = 0;
            _candidatePoints.Clear();
            _queryPending = false;
            _buildPending = false;
            Console.WriteLine("[ProtossOpening] Completed Gateway observed; checking power before Zealot production.");
        }

        if (_stage is Stage.NeedPylonPlacement or Stage.NeedGatewayPlacement)
            PreparePlacement(observation, _stage == Stage.NeedPylonPlacement ? _pylonBuild : _gatewayBuild);

        if (_stage is Stage.PylonPlacementReady or Stage.GatewayPlacementReady)
        {
            var structure = _stage == Stage.PylonPlacementReady ? _pylon : _gateway;
            var ability = _stage == Stage.PylonPlacementReady ? _pylonBuild : _gatewayBuild;
            if (observation.Minerals < structure.MineralCost || observation.Vespene < structure.VespeneCost)
                return Array.Empty<SC2APIProtocol.Action>();
            var probe = observation.Units.FirstOrDefault(unit => unit.Tag == _builderTag &&
                unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible && unit.HasPosition &&
                !observation.DeadUnitTags.Contains(unit.Tag) &&
                unit.UnitKind.Equals(_probe.Name, StringComparison.OrdinalIgnoreCase) &&
                !unit.HasCargo && !unit.HasOrders);
            if (probe is null)
            {
                ResetPlacement();
                return Array.Empty<SC2APIProtocol.Action>();
            }
            var translated = Sc2RaceBuildingActionTranslator.Translate(
                structure, _probe, ability, observation, probe.Tag,
                _placement.X, _placement.Y, new HashSet<ulong>());
            if (translated.IsBlocked)
            {
                LogOnce(structure.Name + " build blocked: " + translated.BlockReason);
                return Array.Empty<SC2APIProtocol.Action>();
            }
            _buildPending = true;
            _stage = _stage == Stage.PylonPlacementReady ? Stage.PylonSubmitted : Stage.GatewaySubmitted;
            Console.WriteLine($"[ProtossOpening] Building {structure.Name} with Probe {probe.Tag} at ({_placement.X:F1},{_placement.Y:F1}).");
            return translated.Actions;
        }

        if (_stage is Stage.PylonSubmitted or Stage.AwaitingPylon or Stage.GatewaySubmitted or Stage.AwaitingGateway)
            return Array.Empty<SC2APIProtocol.Action>();

        if (_stage == Stage.Producing)
        {
            var completed = observation.Units.Count(unit =>
                unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
                !observation.DeadUnitTags.Contains(unit.Tag) &&
                unit.UnitKind.Equals(_zealot.Name, StringComparison.OrdinalIgnoreCase) &&
                unit.BuildProgress >= 1f);
            if (completed >= 1)
            {
                _stage = Stage.Complete;
                Console.WriteLine("[ProtossOpening] First Zealot observed complete.");
                return Array.Empty<SC2APIProtocol.Action>();
            }
            if (_productionPending)
            {
                // Keep the producer latched until action callback or observed order resolves it.
                if (observation.Units.Any(unit => _reservedProducers.Contains(unit.Tag) &&
                    unit.Alliance == UnitAlliance.Self && unit.Orders.Count > 0))
                    _productionPending = false;
                else
                    return Array.Empty<SC2APIProtocol.Action>();
            }
            var translatedProduction = Sc2RaceProductionActionTranslator.Translate(
                _profile, _zealot, _gateway, observation, 1, _reservedProducers,
                unit => unit.HasPowerEvidence && unit.IsPowered);
            if (translatedProduction.IsBlocked)
                return Array.Empty<SC2APIProtocol.Action>();
            _reservedProducers.UnionWith(translatedProduction.ProducerTags);
            _productionPending = true;
            return new[] { translatedProduction.Actions[0] };
        }
        return Array.Empty<SC2APIProtocol.Action>();
    }

    private bool HasCompleted(NormalizedObservation observation, string name) =>
        observation.Units.Any(unit => unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
            !observation.DeadUnitTags.Contains(unit.Tag) &&
            unit.UnitKind.Equals(name, StringComparison.OrdinalIgnoreCase) && unit.BuildProgress >= 1f);

    private void PreparePlacement(NormalizedObservation observation, Sc2AbilityFacts ability)
    {
        if (_queryPending || _queryInFlight || _placement is not null || _placementAttempts >= 3)
            return;
        var nexus = observation.Units.Where(unit => unit.Alliance == UnitAlliance.Self &&
            unit.IsLiveVisible && unit.HasPosition && !observation.DeadUnitTags.Contains(unit.Tag) &&
            unit.UnitKind.Equals("Nexus", StringComparison.OrdinalIgnoreCase))
            .OrderBy(unit => unit.Tag).FirstOrDefault();
        var probe = observation.Units.Where(unit => unit.Alliance == UnitAlliance.Self &&
            unit.IsLiveVisible && unit.HasPosition && !unit.HasCargo && !unit.HasOrders &&
            !observation.DeadUnitTags.Contains(unit.Tag) &&
            unit.UnitKind.Equals(_probe.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(unit => nexus is null ? 0f :
                DistanceSquared(unit.PositionX, unit.PositionY, nexus.PositionX, nexus.PositionY))
            .ThenBy(unit => unit.Tag).FirstOrDefault();
        if (nexus is null || probe is null) return;
        _builderTag = probe.Tag;
        _candidatePoints.Clear();
        for (var ring = 0; ring < 3; ring++)
        {
            var radius = 4.5f + ring * 1.5f;
            for (var i = 0; i < 16; i++)
            {
                var angle = (float)(Math.PI * 2 * i / 16.0);
                _candidatePoints.Add(new Point2D {
                    X = nexus.PositionX + MathF.Cos(angle) * radius,
                    Y = nexus.PositionY + MathF.Sin(angle) * radius
                });
            }
        }
        _queryPending = true;
    }

    public RequestQuery TakeQueryRequest()
    {
        if (!_queryPending || _builderTag == 0 || _candidatePoints.Count == 0) return null;
        _queryPending = false;
        _queryInFlight = true;
        _placementAttempts++;
        var ability = _stage == Stage.NeedPylonPlacement ? _pylonBuild : _gatewayBuild;
        var query = new RequestQuery { IgnoreResourceRequirements = true };
        foreach (var point in _candidatePoints)
            query.Placements.Add(new RequestQueryBuildingPlacement {
                AbilityId = checked((int)ability.AbilityId), TargetPos = point, PlacingUnitTag = _builderTag
            });
        return query;
    }

    public void OnQueryResult(ResponseQuery response)
    {
        _queryInFlight = false;
        if (_stage is not Stage.NeedPylonPlacement and not Stage.NeedGatewayPlacement || _builderTag == 0)
            return;
        var count = Math.Min(response?.Placements.Count ?? 0, _candidatePoints.Count);
        for (var i = 0; i < count; i++)
        {
            if (response.Placements[i].Result != ActionResult.Success) continue;
            _placement = _candidatePoints[i];
            _stage = _stage == Stage.NeedPylonPlacement ? Stage.PylonPlacementReady : Stage.GatewayPlacementReady;
            return;
        }
        ResetPlacement();
        if (_placementAttempts >= 3) LogOnce("SC2 rejected all Protoss structure placement candidates; opener remains fail-closed.");
    }

    public void OnActionResult(ResponseAction response)
    {
        if (_buildPending)
        {
            if (response is null || response.Result.Count == 0) return;
            _buildPending = false;
            if (response.Result[0] == ActionResult.Success)
                _stage = _stage == Stage.PylonSubmitted ? Stage.AwaitingPylon : Stage.AwaitingGateway;
            else
                ResetPlacement();
            return;
        }
        if (_productionPending)
        {
            if (response is null || response.Result.Count == 0) return;
            if (response.Result[0] != ActionResult.Success)
            {
                _productionPending = false;
                _reservedProducers.Clear();
            }
        }
    }

    private void ResetPlacement()
    {
        _builderTag = 0;
        _placement = null;
        _candidatePoints.Clear();
        _queryPending = false;
        _queryInFlight = false;
        _stage = _stage is Stage.NeedPylonPlacement or Stage.PylonPlacementReady or Stage.PylonSubmitted or Stage.AwaitingPylon
            ? Stage.NeedPylonPlacement : Stage.NeedGatewayPlacement;
    }

    private static float DistanceSquared(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx; var dy = ay - by; return dx * dx + dy * dy;
    }

    private void LogOnce(string message)
    {
        if (_failureLogged) return;
        _failureLogged = true;
        Console.WriteLine("[ProtossOpening] FAIL-CLOSED: " + message);
    }
}
