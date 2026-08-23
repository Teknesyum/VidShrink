using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// Scans a publish folder and writes manifest.json: one entry per file with its
// SHA-256 and size, so an updater can download only the files that changed.
// The format is defined in .github/workflows/manifest.md.

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

try
{
    return args[0] switch
    {
        "write" => Write(args),
        "diff" => Diff(args),
        _ => Unknown(args[0])
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  vidshrink-manifest write <yayinKlasoru> --version <v> --rid <rid>");
    Console.WriteLine("                          [--commit <sha>] [--built <iso8601>] [--out <dosya>]");
    Console.WriteLine("  vidshrink-manifest diff <eski.json> <yeni.json>");
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Bilinmeyen komut: {command}");
    PrintUsage();
    return 1;
}

// Files the manifest never lists. ffmpeg and ffprobe are 424 MB, carry their own GPLv3
// licence and do not ship with the release, so they must not appear as missing files on
// the updater's side. manifest.json itself is excluded because it is written into the
// same folder and would otherwise change its own hash on every run.
static bool IsExcluded(string relativePath) =>
    relativePath.StartsWith("tools/ffmpeg/", StringComparison.OrdinalIgnoreCase) ||
    relativePath.Equals("manifest.json", StringComparison.OrdinalIgnoreCase);

static int Write(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("usage: vidshrink-manifest write <yayinKlasoru> --version <v> --rid <rid>");
        return 1;
    }

    var root = Path.GetFullPath(args[1]);
    if (!Directory.Exists(root))
    {
        Console.Error.WriteLine($"Yayin klasoru yok: {root}");
        return 1;
    }

    var version = Option(args, "--version");
    var rid = Option(args, "--rid");
    if (version is null || rid is null)
    {
        Console.Error.WriteLine("--version ve --rid zorunlu.");
        return 1;
    }

    var commit = Option(args, "--commit") ?? "";
    var built = Option(args, "--built") ?? DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    var output = Option(args, "--out") ?? Path.Combine(root, "manifest.json");

    var files = new JsonArray();
    long total = 0;
    var count = 0;

    var entries = Directory
        .EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Select(full => (Full: full, Relative: Relative(root, full)))
        .Where(e => !IsExcluded(e.Relative))
        // Ordinal ordering on the relative path, so two manifests of the same tree line
        // up row by row and can be compared with a plain text diff.
        .OrderBy(e => e.Relative, StringComparer.Ordinal);

    foreach (var entry in entries)
    {
        var size = new FileInfo(entry.Full).Length;
        files.Add(new JsonObject
        {
            ["path"] = entry.Relative,
            ["sha256"] = Sha256(entry.Full),
            ["size"] = size
        });
        total += size;
        count++;
    }

    var manifest = new JsonObject
    {
        ["version"] = version,
        ["commit"] = commit,
        ["built"] = built,
        ["rid"] = rid,
        ["files"] = files
    };

    var json = manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    var directory = Path.GetDirectoryName(Path.GetFullPath(output));
    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
    // UTF-8 without a BOM and with a trailing newline: the file is hashed into
    // checksums.txt and read back by a plain HTTP client.
    File.WriteAllText(output, json + "\n", new UTF8Encoding(false));

    Console.WriteLine($"{output}: {count} dosya, {Megabytes(total)} MB");
    return 0;
}

static int Diff(string[] args)
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("usage: vidshrink-manifest diff <eski.json> <yeni.json>");
        return 1;
    }

    var old = Read(args[1]);
    var neu = Read(args[2]);

    var added = neu.Keys.Where(k => !old.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
    var removed = old.Keys.Where(k => !neu.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
    var changed = neu.Keys
        .Where(k => old.TryGetValue(k, out var o) && o.Sha256 != neu[k].Sha256)
        .OrderBy(k => k, StringComparer.Ordinal)
        .ToList();

    foreach (var path in added) Console.WriteLine($"+ {path}");
    foreach (var path in removed) Console.WriteLine($"- {path}");
    foreach (var path in changed) Console.WriteLine($"~ {path}");

    var bytes = added.Sum(k => neu[k].Size) + changed.Sum(k => neu[k].Size);
    var moved = added.Count + changed.Count;
    Console.WriteLine($"indirilecek: {moved} dosya, {Megabytes(bytes)} MB "
        + $"(silinecek {removed.Count}, degismeyen {neu.Count - moved})");
    return 0;
}

static Dictionary<string, (string Sha256, long Size)> Read(string path)
{
    var root = JsonNode.Parse(File.ReadAllText(path))
        ?? throw new InvalidOperationException($"Bos manifest: {path}");
    var files = root["files"]?.AsArray()
        ?? throw new InvalidOperationException($"'files' alani yok: {path}");

    var map = new Dictionary<string, (string, long)>(StringComparer.Ordinal);
    foreach (var file in files)
    {
        if (file is null) continue;
        var relative = file["path"]!.GetValue<string>();
        map[relative] = (file["sha256"]!.GetValue<string>(), file["size"]!.GetValue<long>());
    }

    return map;
}

// Always '/', on every platform. A manifest produced on Windows is read by the same code
// on macOS and Linux, and a mixed separator turns into a missed match later.
static string Relative(string root, string full) =>
    Path.GetRelativePath(root, full).Replace(Path.DirectorySeparatorChar, '/').Replace('\\', '/');

static string Sha256(string path)
{
    using var stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static string Megabytes(long bytes) =>
    (bytes / 1024.0 / 1024.0).ToString("0.00", CultureInfo.InvariantCulture);

static string? Option(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
