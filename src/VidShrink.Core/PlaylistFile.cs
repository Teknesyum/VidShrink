using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace VidShrink.Core;

/// <summary>
/// Çalma listesi dosyasını girdi listesine çevirir: m3u/m3u8 satırları, pls <c>FileN=</c>
/// anahtarları, wpl <c>&lt;media src&gt;</c> ve asx/wax/wvx/wmx <c>&lt;ref href&gt;</c> öğeleri.
/// Göreli yol listenin klasörüne göre çözülür, <c>file://</c> yerel yola döner, uzak adres
/// olduğu gibi kalır. Yerel girdinin diskte olup olmadığına bakmaz; bunu çağıran eler.
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
            entry = uri.LocalPath;
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
