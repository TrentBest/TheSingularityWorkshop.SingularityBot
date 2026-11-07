using System;
using System.Collections.Generic;
using System.Linq;

namespace SingularityBot.Spatial;

public enum SpatialAlliance
{
    Friendly,
    Enemy,
    Neutral
}

public enum ContactVisibility
{
    Visible,
    Remembered
}

/// <summary>
/// A normalized unit or resource contact. It contains no SC2 protocol objects.
/// Remembered contacts retain their last known position and last-seen game loop.
/// </summary>
public readonly record struct SpatialContact(
    ulong UnitTag,
    uint UnitType,
    MapPoint Position,
    SpatialAlliance Alliance,
    ContactVisibility Visibility,
    uint LastSeenGameLoop);

/// <summary>A contact annotated with its cached map classifications.</summary>
public readonly record struct LocatedContact(
    SpatialContact Contact,
    SectorId Sector,
    MapQuadrant Quadrant,
    MapSide Side);

/// <summary>One sector's current intelligence, split by allegiance and visibility.</summary>
public sealed class SectorIntelligence
{
    internal SectorIntelligence(
        SectorId sector,
        IReadOnlyList<LocatedContact> friendly,
        IReadOnlyList<LocatedContact> rememberedFriendly,
        IReadOnlyList<LocatedContact> visibleEnemies,
        IReadOnlyList<LocatedContact> rememberedEnemies,
        IReadOnlyList<LocatedContact> neutral,
        IReadOnlyList<LocatedContact> rememberedNeutral)
    {
        Sector = sector;
        Friendly = friendly;
        RememberedFriendly = rememberedFriendly;
        VisibleEnemies = visibleEnemies;
        RememberedEnemies = rememberedEnemies;
        Neutral = neutral;
        RememberedNeutral = rememberedNeutral;
    }

    public SectorId Sector { get; }
    public IReadOnlyList<LocatedContact> Friendly { get; }
    public IReadOnlyList<LocatedContact> RememberedFriendly { get; }
    public IReadOnlyList<LocatedContact> VisibleEnemies { get; }
    public IReadOnlyList<LocatedContact> RememberedEnemies { get; }
    public IReadOnlyList<LocatedContact> Neutral { get; }
    public IReadOnlyList<LocatedContact> RememberedNeutral { get; }

    public int TotalContacts =>
        Friendly.Count + RememberedFriendly.Count + VisibleEnemies.Count +
        RememberedEnemies.Count + Neutral.Count + RememberedNeutral.Count;
}

/// <summary>
/// Rebuildable per-observation intelligence over immutable map geometry.
/// Enemy absence from the current observation moves a contact to remembered
/// status; only an explicit destroyed tag removes it from memory.
/// </summary>
public sealed class SpatialIntelligence
{
    private readonly SpatialMap _map;
    private readonly Dictionary<ulong, SpatialContact> _known = new();
    private IReadOnlyDictionary<SectorId, SectorIntelligence> _sectors =
        new Dictionary<SectorId, SectorIntelligence>();
    private IReadOnlyDictionary<MapQuadrant, IReadOnlyList<LocatedContact>> _quadrants =
        new Dictionary<MapQuadrant, IReadOnlyList<LocatedContact>>();
    private IReadOnlyDictionary<MapSide, IReadOnlyList<LocatedContact>> _sides =
        new Dictionary<MapSide, IReadOnlyList<LocatedContact>>();

    public SpatialIntelligence(SpatialMap map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        RebuildIndexes();
    }

    public int KnownContactCount => _known.Count;
    public IReadOnlyDictionary<SectorId, SectorIntelligence> Sectors => _sectors;

    /// <summary>
    /// Applies the visible contacts from one game loop, removes confirmed deaths,
    /// preserves unseen contacts as remembered, and rebuilds sector indexes.
    /// The same input sequence produces the same indexed result.
    /// </summary>
    public void Update(
        uint gameLoop,
        IEnumerable<SpatialContact> visibleContacts,
        IEnumerable<ulong> destroyedTags = null)
    {
        ArgumentNullException.ThrowIfNull(visibleContacts);

        if (destroyedTags != null)
            foreach (var tag in destroyedTags)
                _known.Remove(tag);

        var seenThisLoop = new HashSet<ulong>();
        foreach (var contact in visibleContacts.OrderBy(item => item.UnitTag))
        {
            if (contact.UnitTag == 0)
                throw new ArgumentOutOfRangeException(nameof(visibleContacts), "Contact tags must be non-zero.");
            if (contact.Visibility != ContactVisibility.Visible)
                throw new ArgumentException("Update accepts current visible contacts only.", nameof(visibleContacts));
            if (!seenThisLoop.Add(contact.UnitTag))
                throw new ArgumentException($"Duplicate contact tag {contact.UnitTag}.", nameof(visibleContacts));
            if (!_map.Bounds.Contains(contact.Position))
                throw new ArgumentOutOfRangeException(nameof(visibleContacts), $"Contact {contact.UnitTag} is outside playable map bounds.");

            _known[contact.UnitTag] = contact with
            {
                Visibility = ContactVisibility.Visible,
                LastSeenGameLoop = gameLoop
            };
        }

        foreach (var tag in _known.Keys.ToArray())
        {
            if (seenThisLoop.Contains(tag)) continue;

            var previous = _known[tag];
            if (previous.Visibility != ContactVisibility.Remembered)
                _known[tag] = previous with { Visibility = ContactVisibility.Remembered };
        }

        RebuildIndexes();
    }

    public SectorIntelligence GetSector(SectorId id) =>
        _sectors.TryGetValue(id, out var intelligence)
            ? intelligence
            : throw new ArgumentOutOfRangeException(nameof(id));

    public IReadOnlyList<LocatedContact> GetQuadrant(MapQuadrant quadrant) =>
        _quadrants.TryGetValue(quadrant, out var contacts)
            ? contacts
            : Array.Empty<LocatedContact>();

    public IReadOnlyList<LocatedContact> GetSide(MapSide side) =>
        _sides.TryGetValue(side, out var contacts)
            ? contacts
            : Array.Empty<LocatedContact>();

    /// <summary>Returns the unit's own sector first, followed by its neighbors.</summary>
    public IReadOnlyList<LocatedContact> GetSectorAndNeighbors(
        SectorId sector,
        bool includeDiagonals = true)
    {
        var result = new List<LocatedContact>();
        Append(GetSector(sector), result);

        foreach (var neighbor in _map.GetNeighbors(sector, includeDiagonals))
            Append(GetSector(neighbor.Id), result);

        return result.AsReadOnly();
    }

    private void RebuildIndexes()
    {
        var bySector = _map.Sectors.ToDictionary(
            sector => sector.Id,
            _ => new List<LocatedContact>());
        var byQuadrant = Enum.GetValues<MapQuadrant>()
            .ToDictionary(quadrant => quadrant, _ => new List<LocatedContact>());
        var bySide = Enum.GetValues<MapSide>()
            .ToDictionary(side => side, _ => new List<LocatedContact>());

        foreach (var contact in _known.Values.OrderBy(item => item.UnitTag))
        {
            var located = new LocatedContact(
                contact,
                _map.GetSector(contact.Position).Id,
                _map.GetQuadrant(contact.Position).Kind,
                _map.Side.Assess(contact.Position));

            bySector[located.Sector].Add(located);
            byQuadrant[located.Quadrant].Add(located);
            bySide[located.Side].Add(located);
        }

        _sectors = bySector.ToDictionary(
            pair => pair.Key,
            pair => CreateSectorIntelligence(pair.Key, pair.Value));

        _quadrants = byQuadrant.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<LocatedContact>)pair.Value.AsReadOnly());

        _sides = bySide.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<LocatedContact>)pair.Value.AsReadOnly());
    }

    private static SectorIntelligence CreateSectorIntelligence(
        SectorId id,
        List<LocatedContact> contacts)
    {
        static IReadOnlyList<LocatedContact> Select(
            List<LocatedContact> source,
            SpatialAlliance alliance,
            ContactVisibility visibility) =>
            source.Where(item => item.Contact.Alliance == alliance &&
                                 item.Contact.Visibility == visibility).ToArray();

        return new SectorIntelligence(
            id,
            Select(contacts, SpatialAlliance.Friendly, ContactVisibility.Visible),
            Select(contacts, SpatialAlliance.Friendly, ContactVisibility.Remembered),
            Select(contacts, SpatialAlliance.Enemy, ContactVisibility.Visible),
            Select(contacts, SpatialAlliance.Enemy, ContactVisibility.Remembered),
            Select(contacts, SpatialAlliance.Neutral, ContactVisibility.Visible),
            Select(contacts, SpatialAlliance.Neutral, ContactVisibility.Remembered));
    }

    private static void Append(SectorIntelligence sector, List<LocatedContact> target)
    {
        target.AddRange(sector.Friendly);
        target.AddRange(sector.RememberedFriendly);
        target.AddRange(sector.VisibleEnemies);
        target.AddRange(sector.RememberedEnemies);
        target.AddRange(sector.Neutral);
        target.AddRange(sector.RememberedNeutral);
    }
}
