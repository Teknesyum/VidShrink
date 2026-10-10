using System;
using System.Collections.Generic;
using System.Linq;

namespace VidShrink.Core;

/// <summary>
/// Oynatıcının açtığı ağ adresi. Şema kümesi motorun olduğu gibi geçirdiği kümeyle aynıdır
/// (<c>MpvEngine.RemoteSchemes</c>; eşitlik testle pimli). Kayda ve ekrana giden biçim
/// <see cref="WithoutQuery"/>'dir: sorgu (<c>?…</c>), parça (<c>#…</c>) ve kullanıcı bilgisi
/// (<c>ad:parola@</c>) çoğu zaman erişim anahtarı taşır, diske ve ipucuna yazılmaz.
/// </summary>
public static class MediaAddress
{
    public static IReadOnlyList<string> Schemes { get; } = new[] { "http", "https", "rtsp", "rtmp", "srt", "udp" };

    public static bool IsAddress(string? value) => Parse(value) is not null;

    public static string WithoutQuery(string value)
    {
        if (Parse(value) is null) return value;
        var text = value.Trim();
        var cut = text.IndexOfAny(new[] { '?', '#' });
        if (cut >= 0) text = text[..cut];

        var start = text.IndexOf("://", StringComparison.Ordinal) + 3;
        var slash = text.IndexOf('/', start);
        var end = slash < 0 ? text.Length : slash;
        var at = end > start ? text.LastIndexOf('@', end - 1, end - start) : -1;
        return at < 0 ? text : text[..start] + text[(at + 1)..];
    }

    /// <summary>Başlıkta ve listede görünen ad: yolun son parçası, yoksa sunucu adı.</summary>
    public static string Name(string value)
    {
        if (Parse(value) is not { } uri) return value;
        var path = uri.AbsolutePath.TrimEnd('/');
        var cut = path.LastIndexOf('/');
        var name = Uri.UnescapeDataString(cut >= 0 ? path[(cut + 1)..] : path);
        return name.Length > 0 ? name : uri.Host;
    }

    /// <summary>Panodan ya da sürüklemeden gelen metindeki ilk adres satırı.</summary>
    public static string? FromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (var line in text.Split('\n'))
        {
            var candidate = line.Trim().Trim('"');
            if (IsAddress(candidate)) return candidate;
        }

        return null;
    }

    /// <summary>Komut satırındaki ilk adres. Dosya yolu çözülemediğinde bakılır.</summary>
    public static string? FromArguments(IReadOnlyList<string>? args)
    {
        if (args is null) return null;
        foreach (var arg in args)
        {
            var candidate = (arg ?? "").Trim().Trim('"');
            if (IsAddress(candidate)) return candidate;
        }

        return null;
    }

    private static Uri? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)) return null;
        return Schemes.Any(scheme => string.Equals(scheme, uri.Scheme, StringComparison.OrdinalIgnoreCase)) ? uri : null;
    }
}
