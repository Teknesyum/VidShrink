using System.Globalization;

namespace VidShrink.Ab;

public sealed record AbSettings(
    string SourcePath,
    IReadOnlyList<double> TargetsMb,
    IReadOnlyList<string> Competitors,
    bool ChunkMode,
    string ChunkDirectory,
    string OutputDirectory,
    string LogDirectory,
    string JsonPath,
    double TolerancePercent,
    int EqualizeAttempts)
{
    public static readonly string[] KnownCompetitors = { "handbrake", "vidshrink", "vidshrink-sdr" };
    public static readonly string[] DefaultCompetitors = { "handbrake", "vidshrink" };
    public const int DefaultEqualizeAttempts = 4;

    public static AbSettings Parse(IReadOnlyList<string> args, string workRoot)
    {
        string? source = null;
        var targets = new List<double>();
        var competitors = new List<string>();
        var chunkMode = false;
        string? chunkDirectory = null;
        string? outputDirectory = null;
        string? logDirectory = null;
        string? jsonPath = null;
        var tolerance = SizeParityCheck.DefaultTolerancePercent;
        var equalizeAttempts = DefaultEqualizeAttempts;

        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--kaynak" when i + 1 < args.Count:
                    source = args[++i];
                    break;
                case "--hedef-mb" when i + 1 < args.Count:
                    foreach (var part in args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries))
                        targets.Add(double.Parse(part, CultureInfo.InvariantCulture));
                    break;
                case "--yarismaci" when i + 1 < args.Count:
                    competitors.AddRange(args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim().ToLowerInvariant()));
                    break;
                case "--parca":
                    chunkMode = true;
                    break;
                case "--parca-dizin" when i + 1 < args.Count:
                    chunkDirectory = args[++i];
                    break;
                case "--cikti" when i + 1 < args.Count:
                    outputDirectory = args[++i];
                    break;
                case "--gunluk" when i + 1 < args.Count:
                    logDirectory = args[++i];
                    break;
                case "--json" when i + 1 < args.Count:
                    jsonPath = args[++i];
                    break;
                case "--tolerans" when i + 1 < args.Count:
                    tolerance = double.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--esitleme-denemesi" when i + 1 < args.Count:
                    equalizeAttempts = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    if (equalizeAttempts < 0) throw new ArgumentException("--esitleme-denemesi negatif olamaz.");
                    break;
                default:
                    throw new ArgumentException($"Bilinmeyen seçenek: {args[i]}");
            }
        }

        if (source is null) throw new ArgumentException("--kaynak gerekli.");
        if (targets.Count == 0) throw new ArgumentException("--hedef-mb gerekli.");
        if (competitors.Count == 0) competitors.AddRange(DefaultCompetitors);
        foreach (var competitor in competitors)
            if (!KnownCompetitors.Contains(competitor))
                throw new ArgumentException($"Bilinmeyen yarışmacı: {competitor}. Bilinenler: {string.Join(", ", KnownCompetitors)}");

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return new AbSettings(
            Path.GetFullPath(source),
            targets,
            competitors,
            chunkMode,
            Path.GetFullPath(chunkDirectory ?? Path.GetDirectoryName(Path.GetFullPath(source)) ?? workRoot),
            Path.GetFullPath(outputDirectory ?? Path.Combine(workRoot, "cikti")),
            Path.GetFullPath(logDirectory ?? Path.Combine(workRoot, "gunluk")),
            Path.GetFullPath(jsonPath ?? Path.Combine(workRoot, $"sonuc-{(chunkMode ? "parca" : "tam")}-{stamp}.json")),
            tolerance,
            equalizeAttempts);
    }

    public static string DefaultWorkRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VidShrink.sln")))
            directory = directory.Parent;
        var root = directory?.FullName ?? Directory.GetCurrentDirectory();
        return Path.Combine(root, ".calisma", "ab");
    }
}
