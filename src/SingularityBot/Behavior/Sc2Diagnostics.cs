using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Protobuf;
using SC2APIProtocol;

namespace SingularityBot.Behavior;

/// <summary>Local destination and writers for captured SC2 diagnostic files.</summary>
internal static class Sc2Diagnostics
{
    // Keep all local-run captures beside the existing extracted session records.
    public const string OutputDirectory = @"C:\Users\TheSi\source\repos\TheSingularityWorkshop.SingularityBot\extracted";

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
