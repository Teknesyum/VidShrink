using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace VidShrink.Core;

/// <summary>Yazılacak liste girdisi: yol ya da adres, biliniyorsa süre (sn) ve başlık.</summary>
public sealed record PlaylistEntry(string Path, double? Seconds = null, string? Title = null);

/// <summary>
/// Çalma listesi dosyasını girdi listesine çevirir: m3u/m3u8 satırları, pls <c>FileN=</c>
/// anahtarları, wpl <c>&lt;media src&gt;</c> ve asx/wax/wvx/wmx <c>&lt;ref href&gt;</c> öğeleri.
/// Göreli yol listenin klasörüne göre çözülür, <c>file://</c> yerel yola döner, uzak adres
/// olduğu gibi kalır. Yerel girdinin diskte olup olmadığına bakmaz; bunu çağıran eler.
/// Yazma yalnız m3u8 biçimindedir (<see cref="Write"/>).
/// </summary>
public static partial class PlaylistFile
{
    public static bool IsPlaylist(string path)
        => ShellIntegration.PlaylistExtensions.Contains(Extension(path), StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Read(string path)
    {
        var full = Path.GetFullPath(path);
        var text = Decode(File.ReadAllBytes(full));
        return Parse(text, Extension(full), Path.GetDirectoryName(full) ?? "");
    }

    /// <summary><see cref="Read"/>, diskte olmayan yerel girdiler atılarak; uzak adresler kalır.</summary>
    public static IReadOnlyList<string> ReadPlayable(string path)
        => Read(path).Where(entry => !Path.IsPathFullyQualified(entry) || File.Exists(entry)).ToList();

    public static IReadOnlyList<string> Parse(string text, string extension, string folder)
    {
        var raw = extension.ToLowerInvariant() switch
        {
            "pls" => Pls(text),
            "wpl" or "zpl" => Attributes(MediaSrc(), text),
            "asx" or "wax" or "wvx" or "wmx" => Attributes(RefHref(), text),
            _ => M3u(text),
        };

        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var entries = new List<string>();
        foreach (var entry in raw)
        {
            if (Resolve(entry, folder) is { } resolved && !IsPlaylist(resolved) && seen.Add(resolved)) entries.Add(resolved);
        }

        return entries;
    }

    /// <summary>
    /// Listeyi m3u8 olarak yazar: BOM'suz UTF-8, <c>#EXTM3U</c>, bilinen her girdiye
    /// <c>#EXTINF</c>. Önce aynı klasörde geçici dosyaya yazılır, sonra hedefin yerine taşınır;
    /// yarıda kalan yazma eski listeyi bozmaz. Hata çağırana çıkar, geçici dosya kalmaz.
    /// </summary>
    public static void Write(string path, IReadOnlyList<PlaylistEntry> entries)
    {
        var full = Path.GetFullPath(path);
        var folder = Path.GetDirectoryName(full) ?? "";
        var bytes = new UTF8Encoding(false).GetBytes(Format(entries, folder));
        var temp = full + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, full, overwrite: true);
        }
        catch
        {
            try { File.Delete(temp); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    /// <summary>
    /// <see cref="Parse"/>'ın geri okuyacağı m3u8 metni. Listenin klasöründeki ya da altındaki
    /// dosya göreli ve <c>/</c> ile yazılır (klasör taşınınca liste çalışır kalır), dışındaki
    /// mutlak, uzak adres olduğu gibi. Okuyucunun yorum ya da adres sanacağı göreli ad
    /// <c>./</c> ile başlar.
    /// </summary>
    public static string Format(IReadOnlyList<PlaylistEntry> entries, string folder)
    {
        var text = new StringBuilder("#EXTM3U\n");
        foreach (var entry in entries)
        {
            var remote = IsRemote(entry.Path);
            var title = OneLine(entry.Title ?? (remote ? "" : Path.GetFileNameWithoutExtension(entry.Path)));
            if (title.Length > 0 || entry.Seconds is > 0)
            {
                var seconds = entry.Seconds is > 0 and var known ? (long)Math.Round(known) : -1;
                text.Append("#EXTINF:").Append(seconds.ToString(CultureInfo.InvariantCulture)).Append(',').Append(title).Append('\n');
            }

            text.Append(remote ? entry.Path : Local(entry.Path, folder)).Append('\n');
        }

        return text.ToString();
    }

    private static string Local(string path, string folder)
    {
        if (folder.Length == 0 || !Path.IsPathFullyQualified(path)) return path;
        var relative = Path.GetRelativePath(folder, path);
        if (Path.IsPathRooted(relative) || relative == "." || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return path;
        relative = relative.Replace(Path.DirectorySeparatorChar, '/');
        return relative.StartsWith('#') || char.IsWhiteSpace(relative[0]) || HasScheme(relative) ? "./" + relative : relative;
    }

    private static bool IsRemote(string entry) => HasScheme(entry) && !new Uri(entry).IsFile;

    private static bool HasScheme(string entry) => Uri.TryCreate(entry, UriKind.Absolute, out var uri) && uri.Scheme.Length > 1;

    private static string OneLine(string text) => text.Replace('\r', ' ').Replace('\n', ' ').Trim();

    internal static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage).GetString(bytes);
        }
    }

    private static IEnumerable<string> M3u(string text)
        => Lines(text).Where(line => !line.StartsWith('#'));

    private static IEnumerable<string> Pls(string text)
    {
        var files = new SortedDictionary<int, string>();
        foreach (var line in Lines(text))
        {
            var match = PlsFile().Match(line);
            if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                files[index] = match.Groups[2].Value.Trim();
        }

        return files.Values;
    }

    private static IEnumerable<string> Attributes(Regex pattern, string text)
        => pattern.Matches(text).Select(match => WebUtility.HtmlDecode(match.Groups["v"].Value).Trim());

    private static IEnumerable<string> Lines(string text)
        => text.Split('\n').Select(line => line.Trim().TrimStart('﻿')).Where(line => line.Length > 0);

    private static string? Resolve(string entry, string folder)
    {
        if (entry.Length == 0) return null;
        if (Uri.TryCreate(entry, UriKind.Absolute, out var uri) && uri.Scheme.Length > 1)
        {
            if (!uri.IsFile) return entry;
            if (entry.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) entry = uri.LocalPath;
        }

        if (!OperatingSystem.IsWindows()) entry = entry.Replace('\\', '/');
        try
        {
            return Path.GetFullPath(Path.IsPathRooted(entry) ? entry : Path.Combine(folder, entry));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static string Extension(string path) => Path.GetExtension(path).TrimStart('.');

    [GeneratedRegex(@"^File(\d+)\s*=(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex PlsFile();

    [GeneratedRegex(@"<media\b[^>]*?\bsrc\s*=\s*(?:""(?<v>[^""]*)""|'(?<v>[^']*)')", RegexOptions.IgnoreCase)]
    private static partial Regex MediaSrc();

    [GeneratedRegex(@"<ref\b[^>]*?\bhref\s*=\s*(?:""(?<v>[^""]*)""|'(?<v>[^']*)')", RegexOptions.IgnoreCase)]
    private static partial Regex RefHref();
}
