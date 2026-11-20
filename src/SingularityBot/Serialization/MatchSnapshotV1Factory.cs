using SingularityBot.Observation;

namespace SingularityBot.Serialization;

/// <summary>Last live-visible contact state retained by the match observation owner.</summary>
public sealed record LastSeenContactV1(
    ulong Tag,
    uint UnitTypeId,
    string UnitKind,
    int OwnerId,
    UnitAlliance Alliance,
    uint GameLoop,
    double X,
    double Y)
{
    public static LastSeenContactV1 FromVisible(UnitObservationFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (!facts.IsLiveVisible || !facts.HasPosition ||
            (facts.CapturedFields & ObservationFields.Position) == 0)
            throw new ArgumentException("Last-seen history can only be captured from a live-visible unit with position.", nameof(facts));
        return new LastSeenContactV1(
            facts.Tag, facts.UnitTypeId, facts.UnitKind, facts.OwnerId,
            facts.Alliance, facts.GameLoop, facts.PositionX, facts.PositionY);
    }
}

/// <summary>
/// Projects one normalized observation plus retained live-visible history into
/// a complete MatchSnapshot v1. Fog snapshots and observation absence never
/// manufacture destruction or a last-seen timestamp.
/// </summary>
public static class MatchSnapshotV1Factory
{
    public static MatchSnapshotV1 Create(
        string matchId,
        string mapId,
        string mapContentHash,
        ulong sequence,
        string lifecycle,
        NormalizedObservation observation,
        IReadOnlyList<MatchPlayerV1> players,
        IReadOnlyDictionary<ulong, LastSeenContactV1> lastSeenContacts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapId);
        ArgumentException.ThrowIfNullOrWhiteSpace(lifecycle);
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(lastSeenContacts);
        if (!SingularityBotDocumentJson.IsSha256(mapContentHash))
            throw new ArgumentException("A valid map content hash is required.", nameof(mapContentHash));

        var playerFacts = players.ToDictionary(player => player.PlayerId);
        if (!playerFacts.TryGetValue(observation.PlayerId, out var observingPlayer))
            throw new ArgumentException("Player metadata must include the observing player.", nameof(players));

        if ((observation.CapturedFields & ObservationFields.Economy) != 0)
        {
            playerFacts[observation.PlayerId] = observingPlayer with
            {
                Minerals = observation.Minerals,
                Vespene = observation.Vespene,
                FoodUsed = observation.FoodUsed,
                FoodCap = observation.FoodCap
            };
        }

        var destroyedTags = observation.DeadUnitTags
            .Where(tag => tag != 0)
            .OrderBy(tag => tag)
            .ToArray();
        var destroyed = destroyedTags.ToHashSet();
        var currentUnits = observation.Units.ToDictionary(unit => unit.Tag);
        if (currentUnits.ContainsKey(0))
            throw new ArgumentException("Normalized units must have non-zero stable tags.", nameof(observation));

        foreach (var pair in lastSeenContacts)
        {
            var memory = pair.Value;
            if (pair.Key == 0 || memory.Tag != pair.Key ||
                !double.IsFinite(memory.X) || !double.IsFinite(memory.Y) ||
                memory.GameLoop > observation.GameLoop)
                throw new ArgumentException($"Last-seen history for unit {pair.Key} is invalid.", nameof(lastSeenContacts));
        }

        var projected = new SortedDictionary<ulong, MatchUnitV1>();
        foreach (var facts in observation.Units)
        {
            if (destroyed.Contains(facts.Tag))
                continue;

            if (facts.Visibility == UnitVisibility.Visible)
            {
                projected.Add(facts.Tag, ProjectVisibleUnit(facts, observation.GameLoop));
            }
            else if (lastSeenContacts.TryGetValue(facts.Tag, out var memory))
            {
                projected.Add(facts.Tag, ProjectRememberedUnit(memory));
            }
            else
            {
                projected.Add(facts.Tag, ProjectUnknownUnit(facts));
            }
        }

        // A unit omitted from this observation remains a remembered contact
        // when live-visible history exists. Absence alone is never destruction.
        foreach (var pair in lastSeenContacts.OrderBy(pair => pair.Key))
        {
            if (!destroyed.Contains(pair.Key) && !currentUnits.ContainsKey(pair.Key))
                projected.Add(pair.Key, ProjectRememberedUnit(pair.Value));
        }

        return new MatchSnapshotV1
        {
            SchemaVersion = 1,
            MatchId = matchId,
            MapId = mapId,
            MapContentHash = mapContentHash,
            ObserverPlayerId = observation.PlayerId,
            Sequence = sequence,
            GameLoop = observation.GameLoop,
            Lifecycle = lifecycle,
            Players = playerFacts.Values.OrderBy(player => player.PlayerId).ToArray(),
            Units = projected.Values.ToArray(),
            AuthoritativelyDestroyedUnitTags = destroyedTags
        };
    }

    private static MatchUnitV1 ProjectVisibleUnit(UnitObservationFacts facts, uint currentGameLoop)
    {
        if (!facts.HasPosition ||
            (facts.CapturedFields & ObservationFields.Position) == 0)
            throw new InvalidOperationException(
                $"Visible unit {facts.Tag} has no captured position. Include ObservationFields.Position.");

        return new MatchUnitV1
        {
            Tag = facts.Tag,
            UnitTypeId = facts.UnitTypeId,
            UnitKind = facts.UnitKind ?? string.Empty,
            OwnerId = facts.OwnerId,
            Alliance = MapAlliance(facts.Alliance),
            ContactState = "visible",
            LastSeenGameLoop = currentGameLoop,
            X = facts.PositionX,
            Y = facts.PositionY,
            Health = (facts.CapturedFields & ObservationFields.Health) != 0 ? facts.Health : null,
            HealthMax = facts.HealthMax,
            Shield = (facts.CapturedFields & ObservationFields.Shields) != 0 ? facts.Shield : null,
            ShieldMax = (facts.CapturedFields & ObservationFields.Shields) != 0 ? facts.ShieldMax : null,
            Energy = (facts.CapturedFields & ObservationFields.Energy) != 0 ? facts.Energy : null,
            BuildProgress = (facts.CapturedFields & ObservationFields.BuildProgress) != 0 ? facts.BuildProgress : null,
            Orders = ProjectOrders(facts)
        };
    }

    private static MatchUnitV1 ProjectRememberedUnit(LastSeenContactV1 memory) => new()
    {
        Tag = memory.Tag,
        UnitTypeId = memory.UnitTypeId,
        UnitKind = memory.UnitKind ?? string.Empty,
        OwnerId = memory.OwnerId,
        Alliance = MapAlliance(memory.Alliance),
        ContactState = "remembered",
        LastSeenGameLoop = memory.GameLoop,
        X = memory.X,
        Y = memory.Y
    };

    private static MatchUnitV1 ProjectUnknownUnit(UnitObservationFacts facts) => new()
    {
        Tag = facts.Tag,
        UnitTypeId = facts.UnitTypeId,
        UnitKind = facts.UnitKind ?? string.Empty,
        OwnerId = facts.OwnerId,
        Alliance = MapAlliance(facts.Alliance),
        ContactState = "unknown",
        HealthMax = facts.HealthMax
    };

    private static IReadOnlyList<MatchOrderV1> ProjectOrders(UnitObservationFacts facts)
    {
        if ((facts.CapturedFields & ObservationFields.OrderDetails) == 0)
            return Array.Empty<MatchOrderV1>();

        return facts.Orders.Select(order => new MatchOrderV1
        {
            AbilityId = order.AbilityId,
            TargetUnitTag = order.TargetUnitTag,
            TargetX = order.TargetPositionX,
            TargetY = order.TargetPositionY
        }).ToArray();
    }

    private static string MapAlliance(UnitAlliance alliance) => alliance switch
    {
        UnitAlliance.Self => "self",
        UnitAlliance.Ally => "ally",
        UnitAlliance.Neutral => "neutral",
        UnitAlliance.Enemy => "enemy",
        _ => "unknown"
    };
}
