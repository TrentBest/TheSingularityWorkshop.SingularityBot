namespace SingularityBot.Behavior;

/// <summary>
/// Trusted opener registry. Selection is explicit by mode ID; there is no
/// cross-race or generic fallback when an opener is not registered.
/// </summary>
public sealed class OpeningCatalog
{
    private readonly IReadOnlyDictionary<string, Func<IOpener>> _factories;

    public OpeningCatalog(IEnumerable<KeyValuePair<string, Func<IOpener>>> factories)
    {
        ArgumentNullException.ThrowIfNull(factories);
        var indexed = factories.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        if (indexed.Count == 0 || indexed.Any(item =>
            string.IsNullOrWhiteSpace(item.Key) || item.Value is null))
            throw new ArgumentException("At least one valid opener factory is required.", nameof(factories));
        _factories = new System.Collections.ObjectModel.ReadOnlyDictionary<string, Func<IOpener>>(indexed);
    }

    public bool TryCreate(string modeId, out IOpener opener)
    {
        opener = null;
        if (!_factories.TryGetValue(modeId ?? string.Empty, out var factory))
            return false;
        opener = factory();
        return opener is not null;
    }

    public static OpeningCatalog CreateDefault() => new(new[]
    {
        new KeyValuePair<string, Func<IOpener>>(
            ZerglingOpener.Id, static () => new ZerglingOpener())
    });
}
