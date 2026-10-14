using System;
using System.IO;
using System.Linq;

namespace VidShrink.Core.Editing;

public static class EditOutputName
{
    public const string Suffix = "-duzenlenmis";

    public const string FallbackExtension = ".mkv";

    private static readonly string[] Kept = { ".mp4", ".m4v", ".mov", ".mkv" };

    public static string Extension(string source)
    {
        var ext = Path.GetExtension(source);
        return Kept.Contains(ext.ToLowerInvariant()) ? ext : FallbackExtension;
    }

    public static string For(string source, Func<string, bool>? exists = null)
    {
        exists ??= path => File.Exists(path) || Directory.Exists(path);
        var folder = Path.GetDirectoryName(Path.GetFullPath(source)) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(source) + Suffix;
        var ext = Extension(source);
        var candidate = Path.Combine(folder, stem + ext);
        for (var n = 2; exists(candidate); n++) candidate = Path.Combine(folder, stem + "-" + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + ext);
        return candidate;
    }

    /// <summary>
    /// Parcalari ayri yazan teslimin dosya adlari: <c>&lt;ad&gt;-01&lt;uzanti&gt;</c>, <c>-02</c> ...
    /// Sira numarasi parca sayisina gore sifir dolgulu, en az iki basamak. Adlardan biri varsa
    /// govdeye <c>-2</c>, <c>-3</c> eklenir ve butun kume yeniden denenir; var olanin ustune yazilmaz.
    /// </summary>
    public static IReadOnlyList<string> Segments(string output, int count, Func<string, bool>? exists = null)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "Parca sayisi pozitif olmalidir");
        exists ??= path => File.Exists(path) || Directory.Exists(path);
        var folder = Path.GetDirectoryName(Path.GetFullPath(output)) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(output);
        var ext = Path.GetExtension(output);
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        var width = Math.Max(2, count.ToString(invariant).Length);

        string[] Names(string root)
            => Enumerable.Range(1, count)
                .Select(n => Path.Combine(folder, root + "-" + n.ToString(invariant).PadLeft(width, '0') + ext))
                .ToArray();

        var names = Names(stem);
        for (var n = 2; names.Any(exists); n++) names = Names(stem + "-" + n.ToString(invariant));
        return names;
    }

    /// <summary>Parca adi deseninin varsayilani (<see cref="AdlandirmaDeseni.ParcaVarsayilan"/>).</summary>
    public const string PartPattern = AdlandirmaDeseni.ParcaVarsayilan;

    /// <summary>
    /// Parca adlari desenden (<see cref="AdlandirmaDeseni.ParcaUygula"/>): <c>{ad}</c> cikti adinin
    /// govdesi, <c>{parca}</c> sira, <c>{etiket}</c> klibin etiketi, <c>{zaman}</c> parcanin cizelgedeki
    /// basi. Etiketsiz parcalar ve varsayilan desen eski adlari verir. Bos kalan ad govdeye duser;
    /// kumede ayni ad iki kez cikarsa sira numarasi eklenir. Adlardan biri varsa <c>{ad}</c>
    /// <c>-2</c>, <c>-3</c> ile (desende <c>{ad}</c> yoksa adin sonuna) yeniden denenir.
    /// </summary>
    public static IReadOnlyList<string> Segments(string output, IReadOnlyList<SegmentName> parts, string? pattern, Func<string, bool>? exists = null)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Count < 1) throw new ArgumentOutOfRangeException(nameof(parts), parts.Count, "Parca sayisi pozitif olmalidir");
        var usable = AdlandirmaDeseni.ParcaHata(pattern) is null ? pattern! : PartPattern;
        if (usable == PartPattern && parts.All(p => p.Label is null)) return Segments(output, parts.Count, exists);

        exists ??= path => File.Exists(path) || Directory.Exists(path);
        var folder = Path.GetDirectoryName(Path.GetFullPath(output)) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(output);
        var ext = Path.GetExtension(output);
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        var width = Math.Max(2, parts.Count.ToString(invariant).Length);
        var carriesStem = usable.Contains("{ad}", StringComparison.Ordinal);
        var now = DateTime.Now;

        string Number(int i) => (i + 1).ToString(invariant).PadLeft(width, '0');

        string[] Names(string root, string tail)
        {
            var bare = parts.Select((part, i) =>
            {
                var name = AdlandirmaDeseni.ParcaUygula(usable, new AdBilgisi(root)
                {
                    Parca = Number(i),
                    Etiket = part.Label,
                    Zaman = Stamp(part.Start),
                    Tarih = now,
                });
                return (name.Length == 0 ? root : name) + tail;
            }).ToArray();

            var repeated = bare.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return bare.Select((name, i) => Path.Combine(folder, (repeated.Contains(name) ? name + "-" + Number(i) : name) + ext)).ToArray();
        }

        var names = Names(stem, string.Empty);
        for (var n = 2; names.Any(exists); n++)
        {
            var suffix = "-" + n.ToString(invariant);
            names = carriesStem ? Names(stem + suffix, string.Empty) : Names(stem, suffix);
        }

        return names;
    }

    /// <summary>Cizelge anini parca adina uygun yazar: <c>01h02m05s</c>.</summary>
    public static string Stamp(long ticks)
    {
        var seconds = Math.Max(0, ticks) / EditTime.TicksPerSecond;
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{seconds / 3600:00}h{seconds / 60 % 60:00}m{seconds % 60:00}s");
    }
}

/// <summary>Ayri yazilan parcanin adina giren bilgiler: klibin etiketi ve cizelgedeki basi (tick).</summary>
public sealed record SegmentName(string? Label, long Start);
