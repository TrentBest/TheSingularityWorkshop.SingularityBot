using System.Security.Cryptography;
using System.Text.Json;

namespace SingularityBot.Behavior;

/// <summary>
/// Copies files emitted by the game/runner after the match has ended into the
/// configured diagnostics destination. This runs after the SC2 connection returns,
/// rather than from Bot.OnEnd, so the game has a chance to finish writing its replay.
/// </summary>
internal static class MatchArtifactCollector
{
    public static void Collect(string sourceDirectory, string destinationDirectory, DateTime runStartedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory))
            return;

        var source = Path.GetFullPath(sourceDirectory);
        var destination = Path.GetFullPath(destinationDirectory);
        if (!Directory.Exists(source))
        {
            Console.WriteLine($"[Diagnostics] Match artifact source does not exist; nothing collected: {source}");
            return;
        }

        if (IsSameOrChild(destination, source))
        {
            Console.WriteLine("[Diagnostics] Match artifact collection skipped: destination is inside the source directory.");
            return;
        }

        var target = Path.Combine(destination, "match-output",
            DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"));
        var copied = new List<object>();
        var skipped = new List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0 ||
                        info.LastWriteTimeUtc < runStartedAtUtc.AddSeconds(-3))
                        continue;

                    var relative = Path.GetRelativePath(source, file);
                    var output = Path.GetFullPath(Path.Combine(target, relative));
                    if (!IsSameOrChild(output, target))
                    {
                        skipped.Add(relative);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    CopyWithRetry(file, output);
                    var outputInfo = new FileInfo(output);
                    using var stream = File.OpenRead(output);
                    var hash = Convert.ToHexString(SHA256.HashData(stream));
                    copied.Add(new
                    {
                        source = relative,
                        destination = Path.GetRelativePath(destination, output),
                        bytes = outputInfo.Length,
                        sha256 = hash,
                        lastWriteTimeUtc = info.LastWriteTimeUtc
                    });
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    skipped.Add($"{Path.GetRelativePath(source, file)}: {exception.Message}");
                }
            }

            if (copied.Count == 0)
            {
                Console.WriteLine($"[Diagnostics] No new match artifacts found in {source}.");
                return;
            }

            Directory.CreateDirectory(target);
            var manifest = new
            {
                schema = "singularitybot.match-artifact-manifest.v1",
                collectedAtUtc = DateTimeOffset.UtcNow,
                runStartedAtUtc,
                sourceDirectory = source,
                files = copied,
                skipped
            };
            File.WriteAllText(Path.Combine(target, "manifest.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"[Diagnostics] Collected {copied.Count} match artifact(s) into {target}.");
            if (skipped.Count > 0)
                Console.WriteLine($"[Diagnostics] {skipped.Count} match artifact(s) could not be copied; see collection log.");
        }
        catch (Exception exception)
        {
            // Artifact collection is best-effort and must not mask the match result.
            Console.WriteLine($"[Diagnostics] Match artifact collection failed: {exception}");
        }
    }

    private static void CopyWithRetry(string source, string destination)
    {
        Exception? last = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                var before = new FileInfo(source);
                var expectedLength = before.Length;
                var expectedWriteTime = before.LastWriteTimeUtc;
                using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    input.CopyTo(output);
                    output.Flush(true);
                }

                var after = new FileInfo(source);
                if (after.Exists && after.Length == expectedLength &&
                    after.LastWriteTimeUtc == expectedWriteTime &&
                    new FileInfo(destination).Length == expectedLength)
                    return;

                File.Delete(destination);
                last = new IOException("The source artifact was still changing during collection.");
            }
            catch (IOException exception)
            {
                last = exception;
                Thread.Sleep(250);
            }
        }
        throw last ?? new IOException("Could not copy match artifact.");
    }

    private static bool IsSameOrChild(string candidate, string parent)
    {
        var relative = Path.GetRelativePath(parent, candidate);
        return relative == "." ||
               (!Path.IsPathRooted(relative) &&
                relative != ".." &&
                !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }
}
