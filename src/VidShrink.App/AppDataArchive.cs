using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// Ayarların dışa ve içe aktarılması. Paket, sıfırlamanın saydığı dosya listesinden
/// (<see cref="AppDataReset.FileNames"/>) tek zip olarak üretilir. Gizli anahtar taşıyanlar dışarıda
/// kalır: OpenSubtitles oturum belirteci, silme jetonlarını taşıyan paylaşım defteri ve
/// settings.json içindeki OpenSubtitles API anahtarı. İçe aktarma yalnız listedeki adları yazar;
/// paketteki settings.json JSON nesnesi değilse hiçbir dosyaya dokunulmaz.
/// </summary>
internal static class AppDataArchive
{
    internal static readonly IReadOnlyList<string> SecretFiles = new[]
    {
        Subtitles.SessionStore.FileName,
        AppDataReset.ShareLedgerFileName,
    };

    internal static readonly IReadOnlyList<string> SecretKeys = new[] { "openSubtitlesApiKey" };

    /// <summary>Pakete girebilen dosya adları.</summary>
    internal static IReadOnlyList<string> EntryNames { get; } = AppDataReset.FileNames
        .Where(name => !SecretFiles.Contains(name, StringComparer.OrdinalIgnoreCase))
        .ToList();

    /// <summary>Var olan dosyaları zip'e yazar; pakete giren adlar döner.</summary>
    internal static IReadOnlyList<string> Export(string zipPath, string folder, string settingsFile)
    {
        var written = new List<string>();
        var temp = zipPath + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var name in EntryNames)
                {
                    var source = SourceOf(name, folder, settingsFile);
                    if (!File.Exists(source)) continue;
                    var bytes = File.ReadAllBytes(source);
                    if (IsSettings(name)) bytes = StripSecrets(bytes);
                    var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                    using var output = entry.Open();
                    output.Write(bytes);
                    written.Add(name);
                }
            }
            File.Move(temp, zipPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
        return written;
    }

    /// <summary>
    /// Paketteki tanınan dosyaları yerine yazar; yazılan adlar döner. Tanınmayan girdi atlanır.
    /// Paket okunamazsa ya da settings.json bozuksa <see cref="InvalidDataException"/> atılır.
    /// </summary>
    internal static IReadOnlyList<string> Import(string zipPath, string folder, string settingsFile)
    {
        var files = new List<(string Name, byte[] Bytes)>();
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            foreach (var entry in zip.Entries)
            {
                var name = EntryNames.FirstOrDefault(known => string.Equals(known, entry.FullName, StringComparison.OrdinalIgnoreCase));
                if (name is null) continue;
                using var input = entry.Open();
                using var buffer = new MemoryStream();
                input.CopyTo(buffer);
                files.Add((name, buffer.ToArray()));
            }
        }

        foreach (var (name, bytes) in files)
        {
            if (IsSettings(name) && !SettingsBackup.IsObject(bytes))
                throw new InvalidDataException(name);
        }

        Directory.CreateDirectory(folder);
        var settingsFolder = Path.GetDirectoryName(Path.GetFullPath(settingsFile));
        if (!string.IsNullOrEmpty(settingsFolder)) Directory.CreateDirectory(settingsFolder);

        var written = new List<string>();
        foreach (var (name, bytes) in files)
        {
            var target = SourceOf(name, folder, settingsFile);
            var content = IsSettings(name) ? KeepSecrets(bytes, target) : bytes;
            File.WriteAllBytes(target, content);
            written.Add(name);
        }
        return written;
    }

    private static bool IsSettings(string name) => string.Equals(name, UpdateSettings.FileName, StringComparison.OrdinalIgnoreCase);

    private static string SourceOf(string name, string folder, string settingsFile)
        => IsSettings(name) ? settingsFile : Path.Combine(folder, name);

    private static byte[] StripSecrets(byte[] bytes)
    {
        JsonObject? root;
        try { root = JsonNode.Parse(bytes) as JsonObject; }
        catch (JsonException) { return "{}"u8.ToArray(); }
        if (root is null) return "{}"u8.ToArray();
        foreach (var key in SecretKeys) root.Remove(key);
        return Serialize(root);
    }

    private static byte[] KeepSecrets(byte[] bytes, string target)
    {
        if (JsonNode.Parse(bytes) is not JsonObject root) return bytes;
        foreach (var key in SecretKeys) root.Remove(key);
        try
        {
            if (File.Exists(target) && JsonNode.Parse(File.ReadAllText(target)) is JsonObject current)
            {
                foreach (var key in SecretKeys)
                    if (current[key] is JsonNode value) root[key] = value.DeepClone();
            }
        }
        catch (JsonException)
        {
        }
        return Serialize(root);
    }

    private static byte[] Serialize(JsonObject root)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            root.WriteTo(writer);
        return buffer.ToArray();
    }
}
