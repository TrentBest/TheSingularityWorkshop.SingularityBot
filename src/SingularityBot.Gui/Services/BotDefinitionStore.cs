using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TheSingularityWorkshop.SingularityBot.Gui.Authoring;

namespace TheSingularityWorkshop.SingularityBot.Gui.Services;

/// <summary>Local draft persistence only; this does not imply runtime acceptance or execution.</summary>
public sealed class BotDefinitionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task SaveDraftAsync(BotDefinitionDraft draft, string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = JsonSerializer.Serialize(draft, JsonOptions);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, json, new UTF8Encoding(false), cancellationToken);
    }

    public async Task<BotDefinitionDraft> LoadDraftAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<BotDefinitionDraft>(json)
            ?? throw new InvalidDataException("The selected file did not contain a bot definition draft.");
    }

    public static string ComputeCanonicalContentHash(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        var canonical = JsonSerializer.Serialize(document.RootElement);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}