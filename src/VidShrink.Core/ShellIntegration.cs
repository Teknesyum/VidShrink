namespace VidShrink.Core;

/// <summary>
/// Uygulamanın kabukla paylaştığı yüzey: açtığı uzantı listesi, sağ tık menüsündeki
/// hızlı küçültme hedefleri ve kendisine argüman olarak verilen yolun çözümü. Listeler
/// burada tek kopya durur; dosya seçici de kayıt defteri girdileri de buradan beslenir.
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
    /// "VidShrink ile Küçült" alt menüsünün hızlı hedefleri, megabayt cinsinden. Kurulum
    /// betiği kendi dizisinden yazar; iki liste ölçüde karşılaştırılır.
    /// </summary>
    public static IReadOnlyList<int> QuickShrinkTargetsMegabytes { get; } =
        new[] { 100, 250, 500, 1000, 2000 };

    /// <summary>
    /// Ondalık MB'a geçmeden önceki hızlı listenin ikili hedefleri (1024, 2048). Kayıt
    /// defterine eski kurulumda yazılmış <c>--kucult 1024</c> gibi çağrılar hâlâ gelir;
    /// menüde artık gösterilmezler ama <see cref="AcceptedShrinkTargetsMegabytes"/>
    /// üzerinden kabul edilmeye devam ederler.
    /// </summary>
    public static IReadOnlyList<int> LegacyShrinkTargetsMegabytes { get; } =
        new[] { 1024, 2048 };

    /// <summary>
    /// <c>--kucult</c> argümanının kabul ettiği hedefler: güncel hızlı liste ve eski
    /// kurulumlardan kalan ikili hedefler bir arada.
    /// </summary>
    public static IReadOnlyList<int> AcceptedShrinkTargetsMegabytes { get; } =
        QuickShrinkTargetsMegabytes.Concat(LegacyShrinkTargetsMegabytes).ToArray();

    /// <summary>Hızlı küçültme isteğini uygulamaya taşıyan komut satırı bayrağı.</summary>
    public const string ShrinkFlag = "--kucult";

    /// <summary>Hedef boyutun menü etiketi: 1000'in tam katları GB, diğerleri MB.</summary>
    public static string FormatQuickShrinkLabel(int megabytes) => Bicim.HedefEtiketi(megabytes);

    /// <summary>Kurulum kökündeki başlatıcının açtığı uygulama, köke göre.</summary>
    public const string AppExecutableRelativePath = @"app\VidShrink.App.exe";

    /// <summary>
    /// Dosya açma komutunun çalıştıracağı ikili. Başlatıcı verilirse yanındaki
    /// <c>app\VidShrink.App.exe</c> döner: çift tık başlatıcının doğum turunu atlar, bakımı
    /// uygulama arkada başlatıcıya yaptırır. Başka bir ikili verilirse olduğu gibi döner.
    /// Diske bakmaz; kurucu betik, kurucu motoru ve uygulama aynı değeri yazsın diye saf.
    /// Simge, uygulama anahtarının adı ve menüler başlatıcıda kalır.
    /// </summary>
    public static string OpenCommandTarget(string executable)
    {
        if (!string.Equals(Path.GetFileName(executable), "VidShrink.exe", StringComparison.OrdinalIgnoreCase)) return executable;
        return Path.Combine(Path.GetDirectoryName(executable) ?? "", AppExecutableRelativePath);
    }

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
