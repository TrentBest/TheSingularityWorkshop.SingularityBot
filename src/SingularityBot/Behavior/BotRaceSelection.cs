#nullable enable annotations
using SC2APIProtocol;

namespace SingularityBot.Behavior;

/// <summary>How a bot definition chooses one concrete race for a match.</summary>
public enum BotRaceSelectionPolicy
{
    Random,
    Fixed,
    Custom
}

/// <summary>
/// Declarative race-selection policy. EligibleRaces is an allow-list, never a
/// promise that every listed race currently has an executable gameplay adapter.
/// </summary>
public sealed record BotRaceSelectionDefinition
{
    public BotRaceSelectionPolicy Policy { get; init; } = BotRaceSelectionPolicy.Fixed;
    public Race FixedRace { get; init; } = Race.Terran;
    public IReadOnlyList<Race> EligibleRaces { get; init; } = new[] { Race.Terran };
    /// <summary>Stable ID of a trusted, compile-time registered selector.</summary>
    public string SelectorId { get; init; } = string.Empty;
}

/// <summary>
/// Bounded, immutable facts available to trusted custom selectors. No live
/// observation, arbitrary object graph, script, or executable payload is exposed.
/// </summary>
public sealed record BotRaceSelectionContext(
    Race OpponentRace,
    Difficulty OpponentDifficulty,
    string MapName);

/// <summary>Trusted selector implementation compiled and registered by the host.</summary>
public interface IBotRaceSelector
{
    string Id { get; }
    Race SelectRace(BotRaceSelectionContext context, IReadOnlyList<Race> eligibleRaces);
}

/// <summary>The selected race and the exact executable candidate set considered.</summary>
public sealed record BotRaceSelectionResult(
    Race SelectedRace,
    IReadOnlyList<Race> EligibleExecutableRaces,
    BotRaceSelectionPolicy Policy,
    string? SelectorId);

/// <summary>
/// Resolves a definition's policy against the intersection of its declared
/// races and the host's currently executable race adapters. All outcomes fail
/// closed when a selector is missing, invalid, or chooses an unavailable race.
/// </summary>
public sealed class BotRaceSelectionResolver
{
    private static readonly HashSet<Race> PlayableRaces =
        new() { Race.Terran, Race.Protoss, Race.Zerg };

    private readonly IReadOnlyDictionary<string, IBotRaceSelector> _selectors;

    public BotRaceSelectionResolver(IEnumerable<IBotRaceSelector>? selectors = null)
    {
        var registered = (selectors ?? Array.Empty<IBotRaceSelector>())
            .ToDictionary(selector => selector.Id, StringComparer.Ordinal);
        if (registered.Values.Any(selector => string.IsNullOrWhiteSpace(selector.Id)))
            throw new ArgumentException("Custom race selectors require stable non-empty IDs.", nameof(selectors));
        _selectors = new System.Collections.ObjectModel.ReadOnlyDictionary<string, IBotRaceSelector>(registered);
    }

    public BotRaceSelectionResult Resolve(
        BotRaceSelectionDefinition definition,
        IEnumerable<Race> executableRaces,
        BotRaceSelectionContext context,
        Func<int, int>? chooseIndex = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(executableRaces);
        ArgumentNullException.ThrowIfNull(context);

        var declared = definition.EligibleRaces ?? Array.Empty<Race>();
        if (declared.Count == 0 || declared.Any(race => !PlayableRaces.Contains(race)) ||
            declared.Distinct().Count() != declared.Count)
            throw new InvalidOperationException("Race selection must declare a non-empty, unique allow-list containing only Terran, Protoss, or Zerg.");

        var executable = executableRaces.Distinct().ToHashSet();
        if (executable.Any(race => !PlayableRaces.Contains(race)))
            throw new InvalidOperationException("The host executable-race catalogue contains a non-playable race value.");

        var eligible = declared.Where(executable.Contains).ToArray();
        if (eligible.Length == 0)
            throw new InvalidOperationException("The definition declares no race with an executable gameplay adapter.");

        Race selected;
        string? selectorId = null;
        switch (definition.Policy)
        {
            case BotRaceSelectionPolicy.Fixed:
                selected = definition.FixedRace;
                if (!eligible.Contains(selected))
                    throw new InvalidOperationException($"Fixed race {selected} is not declared and executable for this bot.");
                break;

            case BotRaceSelectionPolicy.Random:
                var index = chooseIndex is null ? Random.Shared.Next(eligible.Length) : chooseIndex(eligible.Length);
                if ((uint)index >= (uint)eligible.Length)
                    throw new InvalidOperationException($"Random selector returned out-of-range index {index}.");
                selected = eligible[index];
                break;

            case BotRaceSelectionPolicy.Custom:
                selectorId = definition.SelectorId;
                if (string.IsNullOrWhiteSpace(selectorId) || !_selectors.TryGetValue(selectorId, out var selector))
                    throw new InvalidOperationException($"Custom race selector '{selectorId}' is not registered by the trusted host.");
                selected = selector.SelectRace(context, Array.AsReadOnly(eligible));
                if (!Enum.IsDefined(selected) || !eligible.Contains(selected))
                    throw new InvalidOperationException($"Custom race selector '{selectorId}' returned {selected}, which is not in the executable allow-list.");
                break;

            default:
                throw new InvalidOperationException($"Unsupported race-selection policy '{definition.Policy}'.");
        }

        return new BotRaceSelectionResult(selected, Array.AsReadOnly(eligible), definition.Policy, selectorId);
    }
}
