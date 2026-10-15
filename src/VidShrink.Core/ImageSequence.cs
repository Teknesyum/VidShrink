using System.Globalization;
using System.Text.RegularExpressions;

namespace VidShrink.Core;

/// <summary>
/// Numaralı resimlerden oluşan dizi: ffmpeg'in <c>image2</c> girdisiyle tek bir video kaynağı sayılır.
/// <c>image2</c> numaradaki ilk boşlukta durur; bu yüzden <see cref="Count"/> başlangıçtan kesintisiz giden
/// kare sayısıdır, boşluktan sonraki dosyalar <see cref="Skipped"/>'e sayılır.
/// </summary>
public sealed record ImageSequence
{
    public const double DefaultFps = 25;

    public static readonly IReadOnlyList<string> Extensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".webp" };

    private static readonly Regex Numbered = new(@"^(.*?)(\d+)(\D*)$", RegexOptions.CultureInvariant);

    public required string Folder { get; init; }
    public required string Prefix { get; init; }
    public required string Suffix { get; init; }
    public required int Digits { get; init; }
    public required bool Padded { get; init; }
    public required long StartNumber { get; init; }
    public required int Count { get; init; }
    public required IReadOnlyList<string> Files { get; init; }
    public int Skipped { get; init; }

    /// <summary>
    /// <c>image2</c> deseni: eşit uzunlukta numarada <c>%0Nd</c>, değilse <c>%d</c>. Addaki <c>%</c> <c>%%</c> olur.
    /// </summary>
    public string Pattern => Path.Combine(Folder,
        Escape(Prefix) + (Padded ? "%0" + Digits.ToString(CultureInfo.InvariantCulture) + "d" : "%d") + Escape(Suffix));

    public string DisplayName
    {
        get
        {
            var name = Prefix.TrimEnd('_', '-', '.', ' ');
            return name.Length > 0 ? name : Path.GetFileName(Path.TrimEndingDirectorySeparator(Folder));
        }
    }

    public IReadOnlyList<string> InputArgs(double fps) => new[]
    {
        "-f", "image2",
        "-framerate", fps.ToString("0.###", CultureInfo.InvariantCulture),
        "-start_number", StartNumber.ToString(CultureInfo.InvariantCulture),
        "-i", Pattern
    };

    public static bool IsImage(string path)
        => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static string Escape(string text) => text.Replace("%", "%%", StringComparison.Ordinal);

    /// <summary>
    /// Verilen dosyalardan en büyük numaralı grubu dizi olarak seçer; aynı klasör, önek ve sonek bir gruptur.
    /// En az iki kare yoksa <c>null</c>. Sıra ada göre değil numaraya göredir.
    /// </summary>
    public static ImageSequence? Detect(IEnumerable<string> files)
    {
        var groups = new Dictionary<(string Folder, string Prefix, string Suffix), List<(long Number, string Digits, string File)>>();
        foreach (var file in files)
        {
            if (!IsImage(file)) continue;
            var name = Path.GetFileName(file);
            var match = Numbered.Match(name);
            if (!match.Success) continue;
            var digits = match.Groups[2].Value;
            if (digits.Length > 18) continue;
            var key = (Path.GetDirectoryName(file) ?? "", match.Groups[1].Value, match.Groups[3].Value);
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new();
            list.Add((long.Parse(digits, CultureInfo.InvariantCulture), digits, file));
        }

        ImageSequence? best = null;
        foreach (var (key, entries) in groups.OrderBy(g => g.Key.Folder, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(g => g.Key.Prefix, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = entries.GroupBy(e => e.Number).Select(g => g.First()).OrderBy(e => e.Number).ToList();
            if (ordered.Count < 2) continue;
            var width = ordered[0].Digits.Length;
            var padded = ordered[0].Digits != ordered[0].Number.ToString(CultureInfo.InvariantCulture);
            string Format(long number) => padded
                ? number.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0')
                : number.ToString(CultureInfo.InvariantCulture);
            var run = 1;
            while (run < ordered.Count && ordered[run].Number == ordered[run - 1].Number + 1
                   && ordered[run].Digits == Format(ordered[run].Number)) run++;
            if (run < 2) continue;
            var candidate = new ImageSequence
            {
                Folder = key.Folder,
                Prefix = key.Prefix,
                Suffix = key.Suffix,
                Digits = ordered[0].Digits.Length,
                Padded = padded,
                StartNumber = ordered[0].Number,
                Count = run,
                Files = ordered.Take(run).Select(e => e.File).ToList(),
                Skipped = ordered.Count - run
            };
            if (best is null || candidate.Count > best.Count) best = candidate;
        }
        return best;
    }

    /// <summary>Dizinin bir video kaynağı gibi okunan bilgisi; süre kare sayısı bölü kare hızıdır.</summary>
    public MediaInfo ToMediaInfo(ImageSequenceFrame frame, double fps)
    {
        long size = 0;
        foreach (var file in Files)
        {
            try { size += new FileInfo(file).Length; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        var duration = Count / fps;
        return new MediaInfo
        {
            FilePath = Path.Combine(Folder, DisplayName),
            FileSizeBytes = size,
            DurationSeconds = duration,
            Width = frame.Width,
            Height = frame.Height,
            Fps = fps,
            VideoCodec = frame.Codec,
            PixelFormat = frame.PixelFormat,
            TotalBitrateBps = duration > 0 ? (long)(size * 8 / duration) : 0,
            Sequence = this
        };
    }
}

/// <summary>Dizinin ilk karesinin yoklanan ölçüsü.</summary>
public sealed record ImageSequenceFrame(int Width, int Height, string Codec, string? PixelFormat);
