namespace SingularityBot;

/// <summary>
/// A bounded, immutable event record for the optional local Bot Studio bridge.
/// Messages contain only explicitly selected diagnostic fields, never raw protocol payloads.
/// </summary>
public sealed record BotStudioEvent(
    long Sequence,
    DateTimeOffset CapturedAtUtc,
    string Category,
    string Name,
    string Severity,
    uint GameLoop,
    string Message);

/// <summary>
/// Thread-safe fixed-capacity event journal. Readers receive a stable ordered copy
/// after a sequence cursor; old entries are evicted rather than allowing growth.
/// </summary>
public sealed class BotStudioEventJournal
{
    public const int DefaultCapacity = 256;
    private readonly object _gate = new();
    private readonly Queue<BotStudioEvent> _events = new();
    private readonly int _capacity;
    private long _sequence;

    public BotStudioEventJournal(int capacity = DefaultCapacity)
    {
        if (capacity is < 1 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public BotStudioEvent Publish(string category, string name, string severity, uint gameLoop, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(severity);
        ArgumentNullException.ThrowIfNull(message);

        lock (_gate)
        {
            var item = new BotStudioEvent(
                ++_sequence, DateTimeOffset.UtcNow, category, name, severity,
                gameLoop, message.Length <= 512 ? message : message[..512]);
            while (_events.Count >= _capacity)
                _events.Dequeue();
            _events.Enqueue(item);
            return item;
        }
    }

    public IReadOnlyList<BotStudioEvent> ReadAfter(long afterSequence, int limit = 100)
    {
        if (afterSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(afterSequence));
        if (limit is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(limit));

        lock (_gate)
            return _events.Where(item => item.Sequence > afterSequence)
                .Take(limit).ToArray();
    }

    public long LatestSequence
    {
        get { lock (_gate) return _sequence; }
    }
}
