using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using Google.Protobuf;
using SC2APIProtocol;

namespace SingularityBot.Behavior;

/// <summary>Local destination and writers for captured SC2 diagnostic files.</summary>
internal static class Sc2Diagnostics
{
    private static readonly object SessionLogGate = new();
    private static StreamWriter _sessionLog;
    private static string _sessionLogPath;

    /// <summary>
    /// Mirrors all runner stdout/stderr into a timestamped file so IDE-launched
    /// matches retain the same diagnostics visible in the IDE output window.
    /// </summary>
    public static void StartSessionLog()
    {
        lock (SessionLogGate)
        {
            if (_sessionLog is not null)
                return;

            var originalOut = Console.Out;
            var originalError = Console.Error;
            try
            {
                Directory.CreateDirectory(OutputDirectory);
                _sessionLogPath = Path.Combine(OutputDirectory,
                    $"singularitybot-session-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}.log");
                _sessionLog = new StreamWriter(new FileStream(
                    _sessionLogPath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite))
                {
                    AutoFlush = true
                };
                Console.SetOut(new TeeTextWriter(originalOut, _sessionLog));
                Console.SetError(new TeeTextWriter(originalError, _sessionLog));
                Console.WriteLine($"[Diagnostics] Session log: {_sessionLogPath}");
                Console.WriteLine($"[Diagnostics] Structured capture directory: {OutputDirectory}");
                AppDomain.CurrentDomain.ProcessExit += (_, _) => FlushSessionLog();
            }
            catch (Exception exception)
            {
                _sessionLog?.Dispose();
                _sessionLog = null;
                Console.WriteLine($"[Diagnostics] Could not start session log: {exception}");
            }
        }
    }

    private static void FlushSessionLog()
    {
        lock (SessionLogGate)
        {
            try { _sessionLog?.Flush(); } catch { }
            try { _sessionLog?.Dispose(); } catch { }
            _sessionLog = null;
        }
    }

    private sealed class TeeTextWriter : TextWriter
    {
        private readonly TextWriter _primary;
        private readonly TextWriter _file;
        private readonly object _gate = new();

        public TeeTextWriter(TextWriter primary, TextWriter file)
        {
            _primary = primary;
            _file = file;
        }

        public override Encoding Encoding => _primary.Encoding;

        public override void Write(char value)
        {
            lock (_gate) { _primary.Write(value); _file.Write(value); }
        }

        public override void Write(string value)
        {
            lock (_gate) { _primary.Write(value); _file.Write(value); }
        }

        public override void Write(char[] buffer, int index, int count)
        {
            lock (_gate) { _primary.Write(buffer, index, count); _file.Write(buffer, index, count); }
        }

        public override void WriteLine(string value)
        {
            lock (_gate) { _primary.WriteLine(value); _file.WriteLine(value); }
        }

        public override void Flush()
        {
            lock (_gate) { _primary.Flush(); _file.Flush(); }
        }
    }
    // All local match evidence is written inside the checkout so it can be reviewed,
    // versioned, and shared with the Workshop tools. Hosts may override this with an
    // absolute path; relative overrides are resolved from the repository root.
    public static string OutputDirectory
    {
        get
        {
            var repositoryRoot = FindRepositoryRoot();
            var configured = Environment.GetEnvironmentVariable("SINGULARITYBOT_DIAGNOSTICS_DIR");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                if (Path.IsPathRooted(configured))
                    return Path.GetFullPath(configured);

                if (repositoryRoot is null)
                    throw new InvalidOperationException(
                        "A relative SINGULARITYBOT_DIAGNOSTICS_DIR requires a discoverable SingularityBot repository root. Set an absolute path instead.");

                return Path.GetFullPath(Path.Combine(repositoryRoot, configured));
            }

            if (repositoryRoot is null)
                throw new InvalidOperationException(
                    "Could not locate the SingularityBot checkout. Launch from within the repository or set SINGULARITYBOT_DIAGNOSTICS_DIR to an absolute directory.");

            return Path.Combine(repositoryRoot, "data", "diagnostics");
        }
    }

    private static string FindRepositoryRoot()
    {
        var starts = new[] { Environment.CurrentDirectory, AppContext.BaseDirectory };

        // Prefer the repository's own solution/project markers. The developer may
        // launch from a workspace parent that is itself another Git checkout.
        foreach (var start in starts)
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var root = directory.FullName;
                if (File.Exists(Path.Combine(root, "TheSingularityWorkshop.SingularityBot.sln")) ||
                    File.Exists(Path.Combine(root, "src", "SingularityBot", "SingularityBot.csproj")))
                    return root;

                directory = directory.Parent;
            }
        }

        // Support source checkouts where the solution is not present, including
        // worktrees whose .git entry is a file rather than a directory.
        foreach (var start in starts)
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var gitEntry = Path.Combine(directory.FullName, ".git");
                if (Directory.Exists(gitEntry) || File.Exists(gitEntry))
                    return directory.FullName;

                directory = directory.Parent;
            }
        }

        return null;
    }

    /// <summary>
    /// Writes the unmodified StartRaw terrain-height image and a coordinate-indexed
    /// CSV. The CSV includes a candidate conversion for analysis, never as an
    /// asserted protocol truth.
    /// </summary>
    public static void WriteTerrainCalibration(ResponseGameInfo gameInfo)
    {
        try
        {
            var startRaw = gameInfo?.StartRaw;
            var mapSize = startRaw?.MapSize;
            var image = startRaw?.TerrainHeight;
            if (startRaw == null || mapSize == null || image?.Size == null)
            {
                Console.WriteLine("[Diagnostics] Terrain calibration capture skipped: StartRaw terrain-height metadata unavailable.");
                return;
            }

            Directory.CreateDirectory(OutputDirectory);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
            var stem = Path.Combine(OutputDirectory, $"terrain-calibration-{timestamp}");
            var bytes = image.Data.ToByteArray();
            var width = image.Size.X;
            var height = image.Size.Y;
            var expectedLength = width > 0 && height > 0 ? (long)width * height : -1;
            var validByteGrid = image.BitsPerPixel == 8 &&
                                expectedLength > 0 &&
                                expectedLength <= int.MaxValue &&
                                bytes.LongLength == expectedLength &&
                                mapSize.X > 0 && mapSize.Y > 0;

            var metadata = new JsonObject
            {
                ["capturedAtUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["mapName"] = gameInfo.MapName,
                ["mapWidth"] = mapSize.X,
                ["mapHeight"] = mapSize.Y,
                ["imageWidth"] = width,
                ["imageHeight"] = height,
                ["bitsPerPixel"] = image.BitsPerPixel,
                ["dataLength"] = bytes.Length,
                ["expectedDataLength"] = expectedLength,
                ["validEightBitGrid"] = validByteGrid,
                ["rowOrder"] = "SC2 StartRaw image row-major; raw game/map image origin is bottom-left",
                ["worldCellCenterMapping"] = "worldX=(cellX+0.5)*mapWidth/imageWidth; worldY=(cellY+0.5)*mapHeight/imageHeight",
                ["candidateWorldHeightFormula"] = "-16 + 32 * rawByte / 255 (community-derived candidate; not calibrated)",
                ["rawBytesBase64"] = Convert.ToBase64String(bytes)
            };
            File.WriteAllText(stem + ".json", metadata.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            }));

            if (validByteGrid)
            {
                using var writer = new StreamWriter(stem + ".csv", false);
                writer.WriteLine("cellX,cellY,linearIndex,worldX,worldY,rawHeightByte,candidateWorldHeight");
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var index = y * width + x;
                        var raw = bytes[index];
                        var worldX = (x + 0.5) * mapSize.X / width;
                        var worldY = (y + 0.5) * mapSize.Y / height;
                        var candidate = -16.0 + 32.0 * raw / 255.0;
                        writer.WriteLine(string.Join(",",
                            x.ToString(CultureInfo.InvariantCulture),
                            y.ToString(CultureInfo.InvariantCulture),
                            index.ToString(CultureInfo.InvariantCulture),
                            worldX.ToString("F6", CultureInfo.InvariantCulture),
                            worldY.ToString("F6", CultureInfo.InvariantCulture),
                            raw.ToString(CultureInfo.InvariantCulture),
                            candidate.ToString("F6", CultureInfo.InvariantCulture)));
                    }
                }
            }

            Console.WriteLine($"[Diagnostics] Terrain calibration capture: {stem}.json" +
                              (validByteGrid ? $" and {stem}.csv ({width}x{height} raw samples)." : " (raw image retained; grid metadata or byte length is invalid, so no CSV was emitted)."));
        }
        catch (Exception exception)
        {
            // Diagnostics must never prevent the bot from entering the match.
            Console.WriteLine($"[Diagnostics] Could not write terrain calibration capture: {exception.Message}");
        }
    }
}
