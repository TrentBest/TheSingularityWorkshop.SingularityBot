#nullable enable

using SingularityBot.Observation;
using TheSingularityWorkshop.FSM_API;

namespace SingularityBot.Behavior;

/// <summary>Events produced by authoritative building FSM transitions.</summary>
public enum BuildingEventKind
{
    Operational,
    RaiseSupplyDepot,
    LowerSupplyDepot,
    ScanRequested
}

public sealed record BuildingEvent(
    BuildingEventKind Kind,
    ulong BuildingTag,
    string BuildingId,
    ulong? BuilderWorkerTag = null,
    float? TargetX = null,
    float? TargetY = null);

/// <summary>
/// Persistent match-local identity and observed facts for one structure.
/// Every structure owns its lifecycle and activity FSMs; Supply Depots also
/// own an access FSM. Observation facts never become a second lifecycle state.
/// </summary>
public sealed class BuildingBehaviorContext : IStateContext
{
    private readonly Dictionary<string, FSMHandle> _handles = new(StringComparer.Ordinal);
    private readonly Action<BuildingEvent> _emit;
    private bool _operationalEventEmitted;
    private bool _scanPending;
    private uint _scanSubmittedAtGameLoop;
    private float _scanEnergyAtSubmission;

    internal BuildingBehaviorContext(
        ulong tag, string unitKind, int ordinal, Action<BuildingEvent> emit)
    {
        UnitTag = tag;
        UnitKind = unitKind;
        Ordinal = ordinal;
        BuildingId = $"{unitKind} {ordinal}";
        Name = $"Building:{BuildingId}:{tag}";
        _emit = emit;
        IsValid = true;
    }

    public ulong UnitTag { get; }
    public string UnitKind { get; }
    public int Ordinal { get; }
    public string BuildingId { get; }
    public string Name { get; set; }
    public bool IsValid { get; set; }
    public uint LastObservedGameLoop { get; private set; }
    public float BuildProgress { get; private set; }
    public float Energy { get; private set; }
    public bool IsVisible { get; private set; }
    public bool IsLanded { get; private set; }
    public bool IsProducing { get; private set; }
    public bool IsResearching { get; private set; }
    public bool IsRaised { get; private set; }
    public bool IsLowered { get; private set; }
    public bool HasNearbyEnemy { get; private set; }
    public bool PassageRequested { get; private set; }
    public bool HasCompletedBarracks { get; private set; }
    public bool HasCompletedSupplyDepot { get; private set; }
    public bool IsInDesignatedCommandCenterConstructionArea { get; private set; }
    public bool IsOrbitalCommand { get; private set; }
    public bool IsPlanetaryFortress { get; private set; }
    public bool ScanRequested { get; private set; }
    public float ScanTargetX { get; private set; }
    public float ScanTargetY { get; private set; }
    public string OrbitalScanState => _handles.ContainsKey(BuildingBehaviorCatalog.OrbitalScanFsm)
        ? CurrentState(BuildingBehaviorCatalog.OrbitalScanFsm)
        : "NotOrbital";
    public bool HasScvTrainingOrder { get; private set; }
    public uint? OrbitalUpgradeAbilityId { get; private set; }
    public string TownHallUpgradeState => _handles.ContainsKey(BuildingBehaviorCatalog.TownHallUpgradeFsm)
        ? CurrentState(BuildingBehaviorCatalog.TownHallUpgradeFsm)
        : "NotTownHall";
    public string TownHallExpansionState => _handles.ContainsKey(BuildingBehaviorCatalog.TownHallExpansionFsm)
        ? CurrentState(BuildingBehaviorCatalog.TownHallExpansionFsm)
        : "NotTownHall";
    public ulong? BuilderWorkerTag { get; private set; }
    public bool IsOperational => CurrentState(BuildingBehaviorCatalog.LifecycleFsm) == "Operational";
    public string LifecycleState => CurrentState(BuildingBehaviorCatalog.LifecycleFsm);
    public string ActivityState => CurrentState(BuildingBehaviorCatalog.ActivityFsm);
    public string? AccessState => _handles.ContainsKey(BuildingBehaviorCatalog.DepotAccessFsm)
        ? CurrentState(BuildingBehaviorCatalog.DepotAccessFsm)
        : null;

    public IReadOnlyDictionary<string, string> CurrentStates =>
        _handles.ToDictionary(pair => pair.Key, pair => pair.Value.CurrentState, StringComparer.Ordinal);

    internal void Attach(string name, FSMHandle handle) => _handles.Add(name, handle);

    internal void SetBuilder(ulong? workerTag) => BuilderWorkerTag = workerTag;

    public void RequestPassage(bool requested) => PassageRequested = requested;

    internal void Apply(
        UnitObservationFacts facts,
        bool hasNearbyEnemy,
        Func<uint, bool> isResearchAbility,
        bool hasCompletedBarracks,
        bool hasCompletedSupplyDepot,
        uint? scvTrainAbilityId,
        uint? orbitalUpgradeAbilityId,
        bool isInDesignatedCommandCenterConstructionArea)
    {
        LastObservedGameLoop = facts.GameLoop;
        IsVisible = facts.IsLiveVisible;
        BuildProgress = facts.BuildProgress;
        Energy = facts.Energy;
        IsLanded = !facts.IsFlying;
        IsRaised = facts.UnitKind.Equals("SupplyDepot", StringComparison.OrdinalIgnoreCase);
        IsLowered = facts.UnitKind.Equals("SupplyDepotLowered", StringComparison.OrdinalIgnoreCase);
        HasNearbyEnemy = hasNearbyEnemy;
        HasCompletedBarracks = hasCompletedBarracks;
        HasCompletedSupplyDepot = hasCompletedSupplyDepot;
        IsInDesignatedCommandCenterConstructionArea = isInDesignatedCommandCenterConstructionArea;
        IsOrbitalCommand = facts.UnitKind.Equals("OrbitalCommand", StringComparison.OrdinalIgnoreCase);
        IsPlanetaryFortress = facts.UnitKind.Equals("PlanetaryFortress", StringComparison.OrdinalIgnoreCase);
        OrbitalUpgradeAbilityId = orbitalUpgradeAbilityId;
        if (!facts.IsLiveVisible) return;

        if (_scanPending)
        {
            if (Energy <= _scanEnergyAtSubmission - 50f)
            {
                _scanPending = false;
                ScanRequested = false;
            }
            else if (facts.GameLoop > _scanSubmittedAtGameLoop + 8)
            {
                _scanPending = false;
            }
        }

        ObservedOrders = facts.Orders;
        HasScvTrainingOrder = scvTrainAbilityId is > 0 &&
            facts.Orders.Any(order => order.AbilityId == scvTrainAbilityId.Value);
        IsResearching = facts.Orders.Any(order => isResearchAbility(order.AbilityId));
        IsProducing = facts.Orders.Count > 0 && !IsResearching;
    }

    internal void EmitOperational()
    {
        if (_operationalEventEmitted) return;
        _operationalEventEmitted = true;
        _emit(new BuildingEvent(BuildingEventKind.Operational, UnitTag, BuildingId, BuilderWorkerTag));
    }

    internal void EmitDepotAccess(bool raise)
    {
        if (raise == IsRaised || (!raise && IsLowered)) return;
        _emit(new BuildingEvent(
            raise ? BuildingEventKind.RaiseSupplyDepot : BuildingEventKind.LowerSupplyDepot,
            UnitTag, BuildingId));
    }

    public void RequestScan(float x, float y)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y))
            throw new ArgumentOutOfRangeException(nameof(x), "Scan coordinates must be finite.");
        if (!IsOrbitalCommand)
            throw new InvalidOperationException("Only an Orbital Command can request a Scanner Sweep.");
        ScanTargetX = x;
        ScanTargetY = y;
        ScanRequested = true;
    }

    internal void EmitScanRequest()
    {
        if (ScanRequested && !_scanPending && IsOrbitalCommand && Energy >= 50)
            _emit(new BuildingEvent(BuildingEventKind.ScanRequested, UnitTag, BuildingId,
                TargetX: ScanTargetX, TargetY: ScanTargetY));
    }

    internal void MarkScanSubmitted(uint gameLoop)
    {
        if (!ScanRequested || _scanPending) return;
        _scanPending = true;
        _scanSubmittedAtGameLoop = gameLoop;
        _scanEnergyAtSubmission = Energy;
    }

    public bool CanScan => ScanRequested && !_scanPending && IsOrbitalCommand && Energy >= 50;
    public bool IsScanPending => _scanPending;
    public bool HasPendingOrRequestedScan => ScanRequested || _scanPending;
    public bool CanRetryScan => ScanRequested && !_scanPending && IsOrbitalCommand && Energy >= 50;
    public bool IsScanComplete => !ScanRequested && !_scanPending;

    public bool CanStreamCommandCenters =>
        UnitKind == "CommandCenter" && !IsOrbitalCommand && !IsPlanetaryFortress &&
        IsLanded && HasCompletedBarracks && IsInDesignatedCommandCenterConstructionArea;

    public bool CanUpgradeToOrbital =>
        UnitKind == "CommandCenter" && IsLanded && !IsOrbitalCommand && !IsPlanetaryFortress &&
        HasCompletedBarracks && HasCompletedSupplyDepot &&
        !IsInDesignatedCommandCenterConstructionArea && !HasScvTrainingOrder;

    public bool IsOrbitalUpgradeOrderObserved =>
        OrbitalUpgradeAbilityId is > 0 &&
        ObservedOrders.Any(order => order.AbilityId == OrbitalUpgradeAbilityId.Value);

    public IReadOnlyList<UnitOrderObservationFacts> ObservedOrders { get; private set; } =
        Array.Empty<UnitOrderObservationFacts>();

    private string CurrentState(string name) =>
        _handles.TryGetValue(name, out var handle) ? handle.CurrentState : "Unknown";

    internal void DestroyHandles()
    {
        foreach (var handle in _handles.Values)
            FSM_API.Interaction.DestroyInstance(handle);
        _handles.Clear();
        IsValid = false;
    }
}

/// <summary>
/// Owns every building identity and advances their FSM_API definitions.
/// IDs are monotonically assigned per canonical building kind for the match,
/// so a destroyed structure's number is never reused.
/// </summary>
public sealed class BuildingBehaviorRuntime
{
    private readonly Dictionary<ulong, BuildingBehaviorContext> _buildings = new();
    private readonly Dictionary<string, int> _nextOrdinalByKind = new(StringComparer.Ordinal);
    private readonly Queue<BuildingEvent> _events = new();
    private (float X, float Y, float Radius)? _designatedCommandCenterArea;

    public IReadOnlyCollection<BuildingBehaviorContext> Buildings => _buildings.Values.ToArray();
    public IReadOnlySet<ulong> ScanReservedOrbitalTags => _buildings.Values
        .Where(building => building.IsOrbitalCommand && building.HasPendingOrRequestedScan)
        .Select(building => building.UnitTag).ToHashSet();
    public int Count => _buildings.Count;
    public bool HasDesignatedCommandCenterConstructionArea => _designatedCommandCenterArea.HasValue;

    public void ConfigureDesignatedCommandCenterConstructionArea(float centerX, float centerY, float radius)
    {
        if (!float.IsFinite(centerX) || !float.IsFinite(centerY) ||
            !float.IsFinite(radius) || radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), "A finite positive construction-area radius is required.");
        _designatedCommandCenterArea = (centerX, centerY, radius);
    }

    public void ClearDesignatedCommandCenterConstructionArea() =>
        _designatedCommandCenterArea = null;

    public void UpdateLifecycle() =>
        FSM_API.Interaction.Update(UnitBehaviorCatalog.DataSyncProcessingGroup);

    public void UpdateActivityAndAccess() =>
        FSM_API.Interaction.Update(UnitBehaviorCatalog.ProductionProcessingGroup);

    public void Update()
    {
        UpdateLifecycle();
        UpdateActivityAndAccess();
    }

    public static void RegisterDefinitions()
    {
        if (!FSM_API.Interaction.Exists(BuildingBehaviorCatalog.LifecycleFsm, UnitBehaviorCatalog.DataSyncProcessingGroup))
            FSM_API.Create.CreateFiniteStateMachine(BuildingBehaviorCatalog.LifecycleFsm, processRate: -1,
                    processingGroup: UnitBehaviorCatalog.DataSyncProcessingGroup)
                .State("UnderConstruction", null, null, null)
                .State("Operational", context => (context as BuildingBehaviorContext)?.EmitOperational(), null, null)
                .Transition("UnderConstruction", "Operational", IsOperational)
                .WithInitialState("UnderConstruction").BuildDefinition();

        if (!FSM_API.Interaction.Exists(BuildingBehaviorCatalog.ActivityFsm, UnitBehaviorCatalog.ProductionProcessingGroup))
            FSM_API.Create.CreateFiniteStateMachine(BuildingBehaviorCatalog.ActivityFsm, processRate: -1,
                    processingGroup: UnitBehaviorCatalog.ProductionProcessingGroup)
                .State("Idle", null, null, null)
                .State("Producing", null, null, null)
                .State("Researching", null, null, null)
                .Transition("Idle", "Producing", IsProducing)
                .Transition("Idle", "Researching", IsResearching)
                .Transition("Producing", "Idle", IsIdle)
                .Transition("Producing", "Researching", IsResearching)
                .Transition("Researching", "Idle", IsIdle)
                .Transition("Researching", "Producing", IsProducing)
                .WithInitialState("Idle").BuildDefinition();

        if (!FSM_API.Interaction.Exists(BuildingBehaviorCatalog.DepotAccessFsm, UnitBehaviorCatalog.ProductionProcessingGroup))
            FSM_API.Create.CreateFiniteStateMachine(BuildingBehaviorCatalog.DepotAccessFsm, processRate: -1,
                    processingGroup: UnitBehaviorCatalog.ProductionProcessingGroup)
                .State("Assess", null, null, null)
                .State("Lowered", context => (context as BuildingBehaviorContext)?.EmitDepotAccess(false), null, null)
                .State("Raised", context => (context as BuildingBehaviorContext)?.EmitDepotAccess(true), null, null)
                .Transition("Assess", "Raised", IsObservedRaised)
                .Transition("Assess", "Lowered", IsObservedLowered)
                .Transition("Lowered", "Raised", ShouldRaiseDepot)
                .Transition("Raised", "Lowered", ShouldLowerDepot)
                .WithInitialState("Assess").BuildDefinition();

        if (!FSM_API.Interaction.Exists(BuildingBehaviorCatalog.TownHallUpgradeFsm, UnitBehaviorCatalog.ProductionProcessingGroup))
            FSM_API.Create.CreateFiniteStateMachine(BuildingBehaviorCatalog.TownHallUpgradeFsm, processRate: -1,
                    processingGroup: UnitBehaviorCatalog.ProductionProcessingGroup)
                .State("WaitingForPrerequisites", null, null, null)
                .State("UpgradeReady", null, null, null)
                .State("Upgrading", null, null, null)
                .State("Orbital", null, null, null)
                .Transition("WaitingForPrerequisites", "Orbital", IsOrbitalCommand)
                .Transition("WaitingForPrerequisites", "UpgradeReady", CanUpgradeToOrbital)
                .Transition("UpgradeReady", "Orbital", IsOrbitalCommand)
                .Transition("UpgradeReady", "WaitingForPrerequisites", CannotUpgradeToOrbital)
                .Transition("UpgradeReady", "Upgrading", IsOrbitalUpgradeOrderObserved)
                .Transition("Upgrading", "Orbital", IsOrbitalCommand)
                .Transition("Upgrading", "UpgradeReady", UpgradeOrderNoLongerObserved)
                .WithInitialState("WaitingForPrerequisites").BuildDefinition();

        if (!FSM_API.Interaction.Exists(BuildingBehaviorCatalog.OrbitalScanFsm, UnitBehaviorCatalog.ProductionProcessingGroup))
            FSM_API.Create.CreateFiniteStateMachine(BuildingBehaviorCatalog.OrbitalScanFsm, processRate: -1,
                    processingGroup: UnitBehaviorCatalog.ProductionProcessingGroup)
                .State("Idle", null, null, null)
                .State("Ready", null, context => (context as BuildingBehaviorContext)?.EmitScanRequest(), null)
                .State("AwaitingEvidence", null, null, null)
                .Transition("Idle", "Ready", CanScan)
                .Transition("Ready", "AwaitingEvidence", IsScanPending)
                .Transition("AwaitingEvidence", "Idle", IsScanComplete)
                .Transition("AwaitingEvidence", "Ready", CanRetryScan)
                .WithInitialState("Idle").BuildDefinition();

        if (!FSM_API.Interaction.Exists(BuildingBehaviorCatalog.TownHallExpansionFsm, UnitBehaviorCatalog.ProductionProcessingGroup))
            FSM_API.Create.CreateFiniteStateMachine(BuildingBehaviorCatalog.TownHallExpansionFsm, processRate: -1,
                    processingGroup: UnitBehaviorCatalog.ProductionProcessingGroup)
                .State("Waiting", null, null, null)
                .State("StreamingCommandCenters", null, null, null)
                .Transition("Waiting", "StreamingCommandCenters", CanStreamCommandCenters)
                .Transition("StreamingCommandCenters", "Waiting", CannotStreamCommandCenters)
                .WithInitialState("Waiting").BuildDefinition();
    }

    private static bool IsOperational(IStateContext context) =>
        context is BuildingBehaviorContext building && building.BuildProgress >= 1f;

    private static bool IsProducing(IStateContext context) =>
        context is BuildingBehaviorContext building && building.IsProducing;

    private static bool IsResearching(IStateContext context) =>
        context is BuildingBehaviorContext building && building.IsResearching;

    private static bool IsIdle(IStateContext context) =>
        context is BuildingBehaviorContext building && !building.IsProducing && !building.IsResearching;

    private static bool CanStreamCommandCenters(IStateContext context) =>
        context is BuildingBehaviorContext building && building.CanStreamCommandCenters;

    private static bool CannotStreamCommandCenters(IStateContext context) =>
        context is BuildingBehaviorContext building && !building.CanStreamCommandCenters;

    private static bool CanUpgradeToOrbital(IStateContext context) =>
        context is BuildingBehaviorContext building && building.CanUpgradeToOrbital;

    private static bool CannotUpgradeToOrbital(IStateContext context) =>
        context is BuildingBehaviorContext building && !building.CanUpgradeToOrbital;

    private static bool IsOrbitalUpgradeOrderObserved(IStateContext context) =>
        context is BuildingBehaviorContext building && building.IsOrbitalUpgradeOrderObserved;

    private static bool IsOrbitalCommand(IStateContext context) =>
        context is BuildingBehaviorContext building && building.IsOrbitalCommand;

    private static bool UpgradeOrderNoLongerObserved(IStateContext context) =>
        context is BuildingBehaviorContext building && !building.IsOrbitalUpgradeOrderObserved && !building.IsOrbitalCommand;

    private static bool CanScan(IStateContext context) =>
        context is BuildingBehaviorContext building && building.CanScan;

    private static bool IsScanPending(IStateContext context) =>
        context is BuildingBehaviorContext building && building.IsScanPending;

    private static bool IsScanComplete(IStateContext context) =>
        context is BuildingBehaviorContext building && building.IsScanComplete;

    private static bool CanRetryScan(IStateContext context) =>
        context is BuildingBehaviorContext building && building.CanRetryScan;

    private static bool IsObservedRaised(IStateContext context) =>
        context is BuildingBehaviorContext building && building.IsRaised;

    private static bool IsObservedLowered(IStateContext context) =>
        context is BuildingBehaviorContext building && building.IsLowered;

    private static bool ShouldRaiseDepot(IStateContext context) =>
        context is BuildingBehaviorContext building && building.HasNearbyEnemy && !building.PassageRequested;

    private static bool ShouldLowerDepot(IStateContext context) =>
        context is BuildingBehaviorContext building && (building.PassageRequested || !building.HasNearbyEnemy);

    public void Reconcile(
        NormalizedObservation observation,
        IEnumerable<BuildJobContext> buildJobs,
        Func<uint, bool> isResearchAbility,
        uint? scvTrainAbilityId = null,
        uint? orbitalUpgradeAbilityId = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(buildJobs);
        ArgumentNullException.ThrowIfNull(isResearchAbility);

        foreach (var deadTag in observation.DeadUnitTags)
            Remove(deadTag);

        var enemies = observation.Units.Where(unit =>
            unit.Alliance == UnitAlliance.Enemy && unit.IsLiveVisible && unit.HasPosition).ToArray();
        var hasCompletedBarracks = observation.Units.Any(unit =>
            unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
            unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase) && unit.BuildProgress >= 1f);
        var hasCompletedSupplyDepot = observation.Units.Any(unit =>
            unit.Alliance == UnitAlliance.Self && unit.IsLiveVisible &&
            (unit.UnitKind.Equals("SupplyDepot", StringComparison.OrdinalIgnoreCase) ||
             unit.UnitKind.Equals("SupplyDepotLowered", StringComparison.OrdinalIgnoreCase)) && unit.BuildProgress >= 1f);

        foreach (var facts in observation.Units.OrderBy(unit => unit.Tag))
        {
            if (facts.Tag == 0 || facts.Alliance != UnitAlliance.Self ||
                observation.DeadUnitTags.Contains(facts.Tag) || !IsBuildingKind(facts.UnitKind))
                continue;

            if (!_buildings.TryGetValue(facts.Tag, out var building))
            {
                var kind = CanonicalKind(facts.UnitKind);
                var ordinal = _nextOrdinalByKind.TryGetValue(kind, out var current) ? current + 1 : 1;
                _nextOrdinalByKind[kind] = ordinal;
                building = new BuildingBehaviorContext(facts.Tag, kind, ordinal, _events.Enqueue);
                _buildings.Add(facts.Tag, building);
                Attach(building, BuildingBehaviorCatalog.LifecycleFsm, UnitBehaviorCatalog.DataSyncProcessingGroup);
                Attach(building, BuildingBehaviorCatalog.ActivityFsm, UnitBehaviorCatalog.ProductionProcessingGroup);
                if (kind == "SupplyDepot")
                    Attach(building, BuildingBehaviorCatalog.DepotAccessFsm, UnitBehaviorCatalog.ProductionProcessingGroup);
                if (kind == "CommandCenter")
                {
                    Attach(building, BuildingBehaviorCatalog.TownHallUpgradeFsm, UnitBehaviorCatalog.ProductionProcessingGroup);
                    Attach(building, BuildingBehaviorCatalog.OrbitalScanFsm, UnitBehaviorCatalog.ProductionProcessingGroup);
                    Attach(building, BuildingBehaviorCatalog.TownHallExpansionFsm, UnitBehaviorCatalog.ProductionProcessingGroup);
                }
            }

            var nearbyEnemy = facts.HasPosition && enemies.Any(enemy =>
                DistanceSquared(facts.PositionX, facts.PositionY, enemy.PositionX, enemy.PositionY) <= 100f);
            var inDesignatedArea = _designatedCommandCenterArea is { } area &&
                facts.HasPosition &&
                DistanceSquared(facts.PositionX, facts.PositionY, area.X, area.Y) <= area.Radius * area.Radius;
            building.Apply(facts, nearbyEnemy, isResearchAbility,
                hasCompletedBarracks, hasCompletedSupplyDepot,
                scvTrainAbilityId, orbitalUpgradeAbilityId, inDesignatedArea);
        }

        foreach (var job in buildJobs.Where(job => job.ResultUnitTag.HasValue && job.WorkerTag.HasValue))
            if (_buildings.TryGetValue(job.ResultUnitTag!.Value, out var building))
                building.SetBuilder(job.WorkerTag);
    }

    private static void Attach(BuildingBehaviorContext context, string fsmName, string group) =>
        context.Attach(fsmName, FSM_API.Create.CreateInstance(fsmName, context, group));

    public bool RequestOrbitalScan(ulong buildingTag, float targetX, float targetY)
    {
        if (!_buildings.TryGetValue(buildingTag, out var building) || !building.IsOrbitalCommand)
            return false;
        building.RequestScan(targetX, targetY);
        return true;
    }

    public bool MarkScanSubmitted(ulong buildingTag, uint gameLoop)
    {
        if (!_buildings.TryGetValue(buildingTag, out var building) || !building.ScanRequested)
            return false;
        building.MarkScanSubmitted(gameLoop);
        return true;
    }

    public bool RequestDepotPassage(ulong buildingTag, bool requested = true)
    {
        if (!_buildings.TryGetValue(buildingTag, out var building) ||
            building.UnitKind != "SupplyDepot")
            return false;
        building.RequestPassage(requested);
        return true;
    }

    public IReadOnlyCollection<BuildingEvent> DrainEvents()
    {
        var result = new List<BuildingEvent>(_events.Count);
        while (_events.TryDequeue(out var item)) result.Add(item);
        return result;
    }

    public bool TryGet(ulong tag, out BuildingBehaviorContext? building) =>
        _buildings.TryGetValue(tag, out building);

    public bool Remove(ulong tag)
    {
        if (!_buildings.Remove(tag, out var building)) return false;
        building.DestroyHandles();
        return true;
    }

    public void Clear()
    {
        foreach (var building in _buildings.Values) building.DestroyHandles();
        _buildings.Clear();
        _events.Clear();
        _nextOrdinalByKind.Clear();
        _designatedCommandCenterArea = null;
    }

    private static bool IsBuildingKind(string kind) =>
        CanonicalKind(kind) is "CommandCenter" or "SupplyDepot" or "Barracks" or "Refinery" or
            "EngineeringBay" or "Bunker" or "MissileTurret" or "Factory" or "Starport" or
            "Armory" or "GhostAcademy" or "FusionCore" or "SensorTower" or "PlanetaryFortress" or
            "OrbitalCommand" or "TechLab" or "Reactor";

    private static string CanonicalKind(string kind) => kind switch
    {
        "SupplyDepotLowered" => "SupplyDepot",
        "OrbitalCommand" or "PlanetaryFortress" => "CommandCenter",
        _ => kind
    };

    private static float DistanceSquared(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return dx * dx + dy * dy;
    }
}

public static class BuildingBehaviorCatalog
{
    public const string LifecycleFsm = "SingularityBot.BuildingLifecycle";
    public const string ActivityFsm = "SingularityBot.BuildingActivity";
    public const string DepotAccessFsm = "SingularityBot.SupplyDepotAccess";
    public const string TownHallUpgradeFsm = "SingularityBot.TownHallOrbitalUpgrade";
    public const string OrbitalScanFsm = "SingularityBot.OrbitalScan";
    public const string TownHallExpansionFsm = "SingularityBot.TownHallExpansion";
}
