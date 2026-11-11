#nullable enable annotations

#nullable enable

using System.Buffers.Binary;
using System.Text;
using TheSingularityWorkshop.FSM_Serialization;

namespace SingularityBot.Behavior;

/// <summary>
/// One versioned, domain-owned event in the SC2 diagnostic timeline.
/// Field order and byte order are part of the local trace format.
/// </summary>
internal sealed class Sc2BinaryEventRecord : IBinarySerializable
{
    private const uint MaximumDetailsBytes = 1_048_576;

    public ulong Sequence { get; set; }
    public long UtcTicks { get; set; }
    public uint GameLoop { get; set; }
    public ushort EventType { get; set; }
    public ulong EntityTag { get; set; }
    public long ValueA { get; set; }
    public long ValueB { get; set; }
    public string Details { get; set; } = string.Empty;

    public void Pack(IBinaryStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var details = Encoding.UTF8.GetBytes(Details ?? string.Empty);
        if (details.Length > MaximumDetailsBytes)
            throw new InvalidDataException("Diagnostic event details exceed the 1 MiB record limit.");

        Span<byte> fixedFields = stackalloc byte[46];
        BinaryPrimitives.WriteUInt64LittleEndian(fixedFields[0..8], Sequence);
        BinaryPrimitives.WriteInt64LittleEndian(fixedFields[8..16], UtcTicks);
        BinaryPrimitives.WriteUInt32LittleEndian(fixedFields[16..20], GameLoop);
        BinaryPrimitives.WriteUInt16LittleEndian(fixedFields[20..22], EventType);
        BinaryPrimitives.WriteUInt64LittleEndian(fixedFields[22..30], EntityTag);
        BinaryPrimitives.WriteInt64LittleEndian(fixedFields[30..38], ValueA);
        BinaryPrimitives.WriteInt64LittleEndian(fixedFields[38..46], ValueB);
        stream.Write(fixedFields);

        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(length, (uint)details.Length);
        stream.Write(length);
        stream.Write(details);
    }

    public void Unpack(IBinaryStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Span<byte> fixedFields = stackalloc byte[46];
        ReadExactly(stream, fixedFields);
        Sequence = BinaryPrimitives.ReadUInt64LittleEndian(fixedFields[0..8]);
        UtcTicks = BinaryPrimitives.ReadInt64LittleEndian(fixedFields[8..16]);
        GameLoop = BinaryPrimitives.ReadUInt32LittleEndian(fixedFields[16..20]);
        EventType = BinaryPrimitives.ReadUInt16LittleEndian(fixedFields[20..22]);
        EntityTag = BinaryPrimitives.ReadUInt64LittleEndian(fixedFields[22..30]);
        ValueA = BinaryPrimitives.ReadInt64LittleEndian(fixedFields[30..38]);
        ValueB = BinaryPrimitives.ReadInt64LittleEndian(fixedFields[38..46]);

        Span<byte> length = stackalloc byte[4];
        ReadExactly(stream, length);
        var byteCount = BinaryPrimitives.ReadUInt32LittleEndian(length);
        if (byteCount > MaximumDetailsBytes)
            throw new InvalidDataException("Diagnostic event details exceed the 1 MiB record limit.");
        var details = new byte[(int)byteCount];
        ReadExactly(stream, details);
        Details = new UTF8Encoding(false, true).GetString(details);
    }
    private static void ReadExactly(IBinaryStream stream, Span<byte> buffer)
    {
        while (!buffer.IsEmpty)
        {
            var bytesRead = stream.Read(buffer);
            if (bytesRead == 0)
                throw new EndOfStreamException("The binary event stream ended before a complete record field was read.");
            if (bytesRead < 0 || bytesRead > buffer.Length)
                throw new IOException($"The binary event stream returned an invalid read count ({bytesRead}).");
            buffer = buffer[bytesRead..];
        }
    }

}

/// <summary>
/// Appends a compact binary event timeline. Diagnostics failures never escape
/// into the SC2 decision loop.
/// </summary>
internal sealed class Sc2BinaryEventRecorder : IDisposable
{
    private const ushort FormatVersion = 1;
    private const ushort FrameSummary = 1;
    private const ushort BuildJobSnapshot = 2;
    private const ushort MarineDecision = 3;
    private const ushort SubmittedAction = 4;
    private const ushort ActionResult = 5;

    private FileStream _file;
    private StreamBinaryStream _stream;
    private ulong _sequence;
    private string _path;

    public void Start(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            _path = Path.Combine(directory, $"sc2-events-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}.bin");
            _file = new FileStream(_path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            _stream = new StreamBinaryStream(_file);
            Span<byte> header = stackalloc byte[8];
            header[0] = (byte)'S'; header[1] = (byte)'C'; header[2] = (byte)'2'; header[3] = (byte)'T';
            BinaryPrimitives.WriteUInt16LittleEndian(header[4..6], FormatVersion);
            BinaryPrimitives.WriteUInt16LittleEndian(header[6..8], 0);
            _stream.Write(header);
            _stream.Flush();
            Console.WriteLine($"[Diagnostics] Binary event timeline: {_path}");
        }
        catch (Exception exception)
        {
            Disable(exception);
        }
    }

    public void RecordFrame(uint gameLoop, uint minerals, uint foodUsed, uint foodCap, int unitCount, int actionCount)
    {
        Write(FrameSummary, 0, minerals, foodUsed,
            $"foodCap={foodCap};units={unitCount};actions={actionCount}", gameLoop);
    }

    public void RecordBuildJob(BuildJobContext job, uint gameLoop)
    {
        var status = job.IsCompleted ? "Completed" : job.IsFailed ? "Failed" :
            job.IsExecuting ? "Executing" : job.IsAssigned ? "Assigned" : "Queued";
        Write(BuildJobSnapshot, job.ResultUnitTag ?? 0, job.Id,
            (long)Math.Round(job.Progress * 1000f),
            $"kind={job.BuildableKind};status={status};worker={job.WorkerTag};producer={job.ProducerTag};created={job.CreatedAtGameLoop};lastObserved={job.LastObservedGameLoop};failure={job.FailureReason}",
            gameLoop);
    }

    public void RecordMarineDecision(uint gameLoop, ulong marineTag, ulong targetTag, string decision)
    {
        Write(MarineDecision, marineTag, unchecked((long)targetTag), 0, decision, gameLoop);
    }

    public void RecordAction(uint gameLoop, SC2APIProtocol.Action action)
    {
        var command = action?.ActionRaw?.UnitCommand;
        if (command is null) return;
        Write(SubmittedAction, command.UnitTags.Count > 0 ? command.UnitTags[0] : 0,
            command.AbilityId, command.UnitTags.Count,
            $"units={string.Join(';', command.UnitTags)};targetTag={command.TargetUnitTag};targetPos={command.TargetWorldSpacePos?.X},{command.TargetWorldSpacePos?.Y}",
            gameLoop);
    }

    public void RecordActionResult(uint gameLoop, int index, uint abilityId, string result)
    {
        Write(ActionResult, 0, index, abilityId, result, gameLoop);
    }

    private void Write(ushort eventType, ulong entityTag, long valueA, long valueB, string details, uint gameLoop)
    {
        if (_stream is null) return;
        try
        {
            var record = new Sc2BinaryEventRecord
            {
                Sequence = ++_sequence,
                UtcTicks = DateTime.UtcNow.Ticks,
                GameLoop = gameLoop,
                EventType = eventType,
                EntityTag = entityTag,
                ValueA = valueA,
                ValueB = valueB,
                Details = details ?? string.Empty
            };
            record.Pack(_stream);
            _stream.Flush();
        }
        catch (Exception exception)
        {
            Disable(exception);
        }
    }

    private void Disable(Exception exception)
    {
        Console.WriteLine($"[Diagnostics] Binary event recorder disabled after error: {exception.Message}");
        try { _stream?.Dispose(); } catch { }
        _stream = null;
        _file = null;
    }

    public void Dispose()
    {
        try { _stream?.Flush(); } catch { }
        try { _stream?.Dispose(); } catch { }
        _stream = null;
        _file = null;
        if (_path is not null)
            Console.WriteLine($"[Diagnostics] Binary event timeline saved: {_path} ({_sequence} records).");
    }
}
