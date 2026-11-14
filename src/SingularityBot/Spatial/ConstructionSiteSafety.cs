using SingularityBot.Observation;

namespace SingularityBot.Spatial;

public enum ConstructionSiteStatus
{
    Unknown,
    Contested,
    Clear
}

public readonly record struct DestroyedConstructionSite(
    ulong DestroyedUnitTag,
    MapPoint Position,
    uint DestroyedAtGameLoop,
    ConstructionSiteStatus Status,
    uint? ClearSinceGameLoop);

/// <summary>
/// Retains confirmed destroyed Barracks footprints and evaluates reuse against
/// remembered enemy contacts plus authoritative, current SC2 visibility.
/// Missing or non-visible evidence always fails closed.
/// </summary>
public sealed class ConstructionSiteSafety
{
    public const float BarracksThreatRadius = 12f;
    public const uint EnemyContactFreshnessGameLoops = 224;
    public const uint RequiredClearGameLoops = 112;
    public const float BarracksFootprintExclusionRadius = 3f;
    public const byte VisibleCellValue = 2;

    private sealed class SiteState
    {
        public required ulong UnitTag { get; init; }
        public required MapPoint Position { get; init; }
        public required uint DestroyedAtGameLoop { get; init; }
        public ConstructionSiteStatus Status { get; set; } = ConstructionSiteStatus.Unknown;
        public uint? ClearSinceGameLoop { get; set; }

        public DestroyedConstructionSite Snapshot => new(
            UnitTag, Position, DestroyedAtGameLoop, Status, ClearSinceGameLoop);
    }

    private readonly Dictionary<ulong, MapPoint> _liveBarracks = new();
    private readonly Dictionary<ulong, SiteState> _destroyedSites = new();

    public IReadOnlyList<DestroyedConstructionSite> Sites =>
        _destroyedSites.Values
            .OrderBy(site => site.DestroyedAtGameLoop)
            .ThenBy(site => site.UnitTag)
            .Select(site => site.Snapshot)
            .ToArray();

    public void Update(
        NormalizedObservation observation,
        SpatialGridSnapshot visibility,
        SpatialIntelligence intelligence)
    {
        ArgumentNullException.ThrowIfNull(observation);

        foreach (var unit in observation.Units
                     .Where(unit => unit.Alliance == UnitAlliance.Self &&
                         unit.IsLiveVisible && unit.HasPosition &&
                         unit.UnitKind.Equals("Barracks", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(unit => unit.Tag))
        {
            _liveBarracks[unit.Tag] = new MapPoint(unit.PositionX, unit.PositionY);
        }

        foreach (var destroyedTag in observation.DeadUnitTags.OrderBy(tag => tag))
        {
            if (!_liveBarracks.Remove(destroyedTag, out var position))
                continue;

            _destroyedSites[destroyedTag] = new SiteState
            {
                UnitTag = destroyedTag,
                Position = position,
                DestroyedAtGameLoop = observation.GameLoop
            };
        }

        foreach (var site in _destroyedSites.Values)
            Evaluate(site, observation.GameLoop, visibility, intelligence);
    }

    public bool IsBlocked(MapPoint candidate, uint gameLoop)
    {
        var radiusSquared = BarracksFootprintExclusionRadius * BarracksFootprintExclusionRadius;
        return _destroyedSites.Values.Any(site =>
            site.Status != ConstructionSiteStatus.Clear &&
            DistanceSquared(candidate, site.Position) <= radiusSquared);
    }

    public void Reset()
    {
        _liveBarracks.Clear();
        _destroyedSites.Clear();
    }

    private static void Evaluate(
        SiteState site,
        uint gameLoop,
        SpatialGridSnapshot visibility,
        SpatialIntelligence intelligence)
    {
        if (visibility is null ||
            !visibility.TryGetValue(site.Position, out var visibilityValue) ||
            visibilityValue != VisibleCellValue ||
            intelligence is null)
        {
            site.ClearSinceGameLoop = null;
            site.Status = ConstructionSiteStatus.Unknown;
            return;
        }

        var threatRadiusSquared = BarracksThreatRadius * BarracksThreatRadius;
        var hasRecentThreat = intelligence.KnownContacts.Any(contact =>
            contact.Alliance == SpatialAlliance.Enemy &&
            contact.LastSeenGameLoop <= gameLoop &&
            gameLoop - contact.LastSeenGameLoop <= EnemyContactFreshnessGameLoops &&
            DistanceSquared(contact.Position, site.Position) <= threatRadiusSquared);

        if (hasRecentThreat)
        {
            site.ClearSinceGameLoop = null;
            site.Status = ConstructionSiteStatus.Contested;
            return;
        }

        site.ClearSinceGameLoop ??= gameLoop;
        site.Status = gameLoop >= site.ClearSinceGameLoop.Value &&
            gameLoop - site.ClearSinceGameLoop.Value >= RequiredClearGameLoops
                ? ConstructionSiteStatus.Clear
                : ConstructionSiteStatus.Unknown;
    }

    private static float DistanceSquared(MapPoint left, MapPoint right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return dx * dx + dy * dy;
    }
}
