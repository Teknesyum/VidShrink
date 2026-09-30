using System.Runtime.Versioning;
using Microsoft.Win32;

namespace VidShrink.Core.Editing;

/// <summary>
/// Metin katmaninin yazi tipi klasoru. Onizleme (<c>sub-fonts-dir</c>) ve disa aktarma
/// (<c>fontsdir</c>) ayni klasoru alir; klasore yalniz secilen ailelerin dosyalari kopyalanir.
/// Sistem yazi tipi klasorunun tamami verilmez: libass verilen klasordeki her dosyayi bellege
/// okur ve Windows'ta bu 400 MB'yi asiyor. Bulunamayan aile icin libass'in sistem saglayicisi kalir.
/// </summary>
public static class TextFonts
{
    public const string FolderName = "fonts";

    /// <summary>
    /// Kurulu yazi tipi kayitlarindan (<c>"Arial Bold (TrueType)" -> arialbd.ttf</c>) istenen ailelere
    /// ait dosyalar. Aile adi kaydin tamami ya da kalin/italik gibi bir ekin onu olarak eslesir.
    /// </summary>
    public static IReadOnlyList<string> FilesFor(IEnumerable<string> families, IEnumerable<KeyValuePair<string, string>> installed)
    {
        ArgumentNullException.ThrowIfNull(families);
        ArgumentNullException.ThrowIfNull(installed);
        var wanted = families.Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var files = new List<string>();
        foreach (var (name, file) in installed)
        {
            var bare = name;
            var paren = bare.LastIndexOf(" (", StringComparison.Ordinal);
            if (paren > 0 && bare.EndsWith(')')) bare = bare[..paren];
            var faces = bare.Split(" & ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (!faces.Any(face => wanted.Any(w => Matches(face, w)))) continue;
            if (!files.Contains(file, StringComparer.OrdinalIgnoreCase)) files.Add(file);
        }

        return files;
    }

    /// <summary>
    /// <paramref name="root"/> altinda <see cref="FolderName"/> klasorunu kurar ve ailelerin dosyalarini
    /// kopyalar. Hicbir dosya bulunamazsa <c>null</c> doner; o zaman yazi tipi klasoru verilmez.
    /// </summary>
    public static string? Prepare(string root, IEnumerable<string> families)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        var system = SystemFolder();
        if (system is null || !OperatingSystem.IsWindows()) return null;

        var files = FilesFor(families, Installed())
            .Select(f => Path.IsPathRooted(f) ? f : Path.Combine(system, f))
            .Where(File.Exists)
            .ToArray();
        if (files.Length == 0) return null;

        var folder = Path.Combine(root, FolderName);
        Directory.CreateDirectory(folder);
        foreach (var file in files)
        {
            var target = Path.Combine(folder, Path.GetFileName(file));
            var source = new FileInfo(file);
            if (File.Exists(target) && new FileInfo(target).Length == source.Length) continue;
            File.Copy(file, target, true);
        }

        return folder;
    }

    public static string? SystemFolder()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        return string.IsNullOrEmpty(folder) || !Directory.Exists(folder) ? null : folder;
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<KeyValuePair<string, string>> Installed()
    {
        const string key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var fonts = hive.OpenSubKey(key);
            if (fonts is null) continue;
            foreach (var name in fonts.GetValueNames())
                if (fonts.GetValue(name) is string file && !string.IsNullOrWhiteSpace(file))
                    yield return new KeyValuePair<string, string>(name, file);
        }
    }

    private static bool Matches(string face, string family)
        => face.Equals(family, StringComparison.OrdinalIgnoreCase)
           || (face.StartsWith(family + " ", StringComparison.OrdinalIgnoreCase)
               && Styles.Any(s => face.EndsWith(" " + s, StringComparison.OrdinalIgnoreCase) && face.Length == family.Length + 1 + s.Length));

    private static readonly string[] Styles = { "Bold", "Italic", "Bold Italic", "Regular", "Kalın", "İtalik", "Kalın İtalik" };
}
