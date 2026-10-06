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
}
