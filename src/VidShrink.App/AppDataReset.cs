using VidShrink.App.Playback;
using VidShrink.App.Recorder;
using VidShrink.Core;

namespace VidShrink.App;

/// <summary>
/// "Tüm verileri sıfırla": VidShrink'in kullanıcı klasörüne yazdığı her ayar ve geçmiş
/// dosyası silinir, yalnız settings.json değil. Klasör <see cref="UpdateSettings.DefaultPath"/>'in
/// klasörüdür; <c>VIDSHRINK_SETTINGS_PATH</c> verilmişse orası, yoksa %APPDATA%\VidShrink.
///
/// Klasördeki başka dosyalara dokunulmaz: güncelleme günlüğü ve yarım kalmış güncelleme
/// kayıtları veri değil, kurulumun kendisidir.
/// </summary>
internal static class AppDataReset
{
    /// <summary>Silinen dosyaların tam listesi. Yeni bir veri dosyası buraya da girer.</summary>
    internal static readonly IReadOnlyList<string> FileNames = new[]
    {
        UpdateSettings.FileName,
        PlayerSettings.FileName,
        PlayerAdvanced.FileName,
        RecentFiles.FileName,
        ToolsOptions.FileName,
        RecorderSettings.FileName,
        MainWindow.PlayerHistoryFileName,
        MainWindow.LayoutFileName,
        MainWindow.DismissedNoticeFileName,
        ShareLedgerFileName,
        Subtitles.SessionStore.FileName,
    };

    internal const string ShareLedgerFileName = "paylasimlar.json";

    /// <summary>Paylaşım defteri bozuk bulunduğunda yanına bırakılan kopyaların öneki.</summary>
    internal const string ShareLedgerBrokenPrefix = "paylasimlar.json.bozuk";

    /// <summary>Varsayılan veri klasörü.</summary>
    internal static string? DefaultFolder => Path.GetDirectoryName(UpdateSettings.DefaultPath);

    /// <summary>
    /// Klasördeki veri dosyalarını ve varsa ayrıca verilen ayar dosyasını siler. Silinen
    /// dosyaların yolları döner. Olmayan dosya hata değildir.
    /// </summary>
    internal static IReadOnlyList<string> Run(string? folder, string? settingsFile = null)
    {
        var removed = new List<string>();
        if (!string.IsNullOrEmpty(settingsFile) && File.Exists(settingsFile))
        {
            File.Delete(settingsFile);
            removed.Add(settingsFile);
        }

        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return removed;

        var targets = FileNames
            .Select(name => Path.Combine(folder, name))
            .Concat(Directory.EnumerateFiles(folder, ShareLedgerBrokenPrefix + "*"));
        foreach (var path in targets.Distinct(StringComparer.OrdinalIgnoreCase).ToList())
        {
            if (!File.Exists(path)) continue;
            File.Delete(path);
            removed.Add(path);
        }

        return removed;
    }
}
