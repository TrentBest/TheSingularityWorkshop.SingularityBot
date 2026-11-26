namespace SingularityBot.Behavior;

/// <summary>
/// Deterministic frame-boundary handoff for production FSM intents. It does not
/// schedule or submit actions; existing controllers remain the only action owners.
/// </summary>
public sealed class ProductionIntentBuffer
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public int Count => _entries.Count;
    public int ReadyCount => _entries.Values.Count(entry => !entry.Submitted);

    /// <summary>
    /// Adds an emitted intent once per objective. Re-emission of the same intent
    /// is harmless; conflicting intent data for one objective fails closed.
    /// </summary>
    public bool Enqueue(ProductionIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (string.IsNullOrWhiteSpace(intent.StepId) ||
            string.IsNullOrWhiteSpace(intent.Target) ||
            string.IsNullOrWhiteSpace(intent.Producer))
            return false;

        if (_entries.TryGetValue(intent.StepId, out var existing))
            return existing.Intent == intent;

        _entries.Add(intent.StepId, new Entry(intent));
        return true;
    }

    /// <summary>Returns the highest-priority unsubmitted intent deterministically.</summary>
    public bool TryPeek(out ProductionIntent intent)
    {
        var next = _entries.Values
            .Where(entry => !entry.Submitted)
            .OrderByDescending(entry => entry.Intent.Priority)
            .ThenBy(entry => entry.Intent.StepId, StringComparer.Ordinal)
            .Select(entry => entry.Intent)
            .FirstOrDefault();
        intent = next;
        return next is not null;
    }

    /// <summary>Marks the intent submitted only after its existing controller emitted an action.</summary>
    public bool MarkSubmitted(string stepId)
    {
        if (!_entries.TryGetValue(stepId, out var entry) || entry.Submitted)
            return false;
        entry.Submitted = true;
        return true;
    }

    /// <summary>
    /// Resolves an intent after observed execution/completion or an explicit
    /// action rejection. The owning ProductionStepContext latch is host-managed.
    /// </summary>
    public bool Resolve(string stepId) => _entries.Remove(stepId);

    public bool Contains(string stepId) => _entries.ContainsKey(stepId);

    public void Clear() => _entries.Clear();

    private sealed class Entry(ProductionIntent intent)
    {
        public ProductionIntent Intent { get; } = intent;
        public bool Submitted { get; set; }
    }
}
