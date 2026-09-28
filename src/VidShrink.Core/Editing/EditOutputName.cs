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
}
