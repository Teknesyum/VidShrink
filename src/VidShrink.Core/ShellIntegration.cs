namespace VidShrink.Core;

/// <summary>
/// Uygulamanın kabukla paylaştığı yüzey: açtığı uzantı listesi ve kendisine argüman
/// olarak verilen yolun çözümü. Liste burada tek kopya durur; dosya seçici de kayıt
/// defteri girdileri de buradan beslenir.
/// </summary>
public static class ShellIntegration
{
    /// <summary>Uygulamanın açtığı medya uzantıları; noktasız, küçük harf.</summary>
    public static IReadOnlyList<string> MediaExtensions { get; } = new[]
    {
        "mp4", "mkv", "mov", "avi", "webm", "wmv", "flv", "m4v", "mpg", "mpeg", "ts", "m2ts",
        "3gp", "ogv", "vob", "asf", "rm", "rmvb", "divx", "mxf", "f4v", "mts", "dav", "gif"
    };

    /// <summary>
    /// Argümanlardan var olan ilk dosya yolunu döndürür, bulamazsa <c>null</c>. Tırnağı
    /// kaybolmuş boşluklu yol birden çok parça olarak geldiği için her başlangıç
    /// noktasından en uzun birleşim önce denenir. Uzantıya bakmaz.
    /// </summary>
    public static string? ResolveStartupPath(IReadOnlyList<string>? args)
    {
        if (args is null) return null;

        for (var start = 0; start < args.Count; start++)
        {
            for (var end = args.Count - 1; end >= start; end--)
            {
                var candidate = Join(args, start, end);
                if (candidate.Length > 0 && Exists(candidate)) return candidate;
            }
        }

        return null;
    }

    private static string Join(IReadOnlyList<string> args, int start, int end)
    {
        var parts = new string[end - start + 1];
        for (var i = 0; i < parts.Length; i++) parts[i] = args[start + i] ?? "";
        return string.Join(' ', parts).Trim().Trim('"');
    }

    private static bool Exists(string path)
    {
        try { return File.Exists(path); }
        catch { return false; }
    }
}
