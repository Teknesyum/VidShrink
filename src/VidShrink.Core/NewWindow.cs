using System.Globalization;

namespace VidShrink.Core;

/// <summary>
/// OYB-35 "yeni pencerede aç": ikinci, bağımsız bir oynatıcı süreci. Tek örnek borusunu almaz ve
/// yolu sahibe iletmez; denetim kanalı, kabuk kuyruğu, kuyruk defteri, güncelleme ve kayıt defteri
/// sahibin işidir. Yarış olmasın diye ayar klasörünün <c>*.json</c> dosyaları açılışta
/// <c>pencereler/&lt;pid&gt;/</c> altına kopyalanır ve süreç yalnız bu kopyaya yazar; kopya kapanışta
/// silinir, ölmüş süreçten kalan kopya sonraki açılışta süpürülür.
/// </summary>
public static class NewWindow
{
    public const string Flag = "--yeni-pencere";
    public const string EnglishFlag = "--new-window";
    public const string FolderName = "pencereler";
    public const string SettingsVariable = "VIDSHRINK_SETTINGS_PATH";

    public static bool Requested(IReadOnlyList<string>? args)
        => args is not null && args.Any(IsFlag);

    public static bool IsFlag(string? arg)
        => string.Equals(arg, Flag, StringComparison.Ordinal) || string.Equals(arg, EnglishFlag, StringComparison.Ordinal);

    public static string[] Without(IReadOnlyList<string> args) => args.Where(a => !IsFlag(a)).ToArray();

    /// <summary>Yeni pencereyi açan sürecin argümanları: bayrak, sonra varsa yol ya da adres.</summary>
    public static IReadOnlyList<string> Arguments(string? target)
        => string.IsNullOrWhiteSpace(target) ? new[] { Flag } : new[] { Flag, target };

    public static string SnapshotFolder(string settingsPath, int processId)
        => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settingsPath)) ?? "", FolderName,
            processId.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Ayar klasörünün üst düzey <c>*.json</c> dosyalarını kopyaya alır ve kopyadaki ayar yolunu döner.
    /// <paramref name="excluded"/> kopyalanmaz (oynatıcı kuyruğu: ikinci pencere sahibin kuyruğunu açmasın).
    /// Okunamayan dosya atlanır; o dosyanın ayarı ikinci pencerede varsayılandan başlar.
    /// </summary>
    public static string Prepare(string settingsPath, int processId, IEnumerable<string>? excluded = null)
    {
        var source = Path.GetDirectoryName(Path.GetFullPath(settingsPath)) ?? "";
        var target = SnapshotFolder(settingsPath, processId);
        Directory.CreateDirectory(target);
        var skip = new HashSet<string>(excluded ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(source))
            foreach (var file in Directory.EnumerateFiles(source, "*.json", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                if (skip.Contains(name)) continue;
                try { File.Copy(file, Path.Combine(target, name), overwrite: true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        return Path.Combine(target, Path.GetFileName(settingsPath));
    }

    /// <summary>Kendisi dışında, süreci yaşamayan kopyaları siler; silineni sayar.</summary>
    public static int Sweep(string settingsPath, int self, Func<int, bool> alive)
    {
        var root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settingsPath)) ?? "", FolderName);
        if (!Directory.Exists(root)) return 0;
        var removed = 0;
        foreach (var folder in Directory.EnumerateDirectories(root))
        {
            if (!int.TryParse(Path.GetFileName(folder), NumberStyles.None, CultureInfo.InvariantCulture, out var pid)) continue;
            if (pid == self || alive(pid)) continue;
            try
            {
                Directory.Delete(folder, recursive: true);
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return removed;
    }

    public static void Remove(string snapshotSettingsPath)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(snapshotSettingsPath));
        if (string.IsNullOrEmpty(folder) || !string.Equals(Path.GetFileName(Path.GetDirectoryName(folder)), FolderName, StringComparison.Ordinal)) return;
        try { Directory.Delete(folder, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Kopya yolunun ayar klasörüne değil kopyaya işaret ettiği ikinci pencerede gerçek ayar yolu.</summary>
    public static string? OriginalSettings { get; private set; }

    public static bool Active { get; private set; }

    /// <summary>
    /// Süreci ikinci pencere kipine alır: ayar yolu kopyaya çevrilir, eski değer çocuk süreçlere geri verilsin
    /// diye saklanır. Kopya hazırlanamazsa <c>null</c> döner ve süreç yine ikinci pencere kalır, ayar yazmaz.
    /// </summary>
    public static string? Enter(string settingsPath, int processId, IEnumerable<string>? excluded, Func<int, bool> alive)
    {
        Active = true;
        OriginalSettings = Environment.GetEnvironmentVariable(SettingsVariable);
        try
        {
            Sweep(settingsPath, processId, alive);
            var snapshot = Prepare(settingsPath, processId, excluded);
            Environment.SetEnvironmentVariable(SettingsVariable, snapshot);
            return snapshot;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Çocuk süreç (yeni pencere, kabuk küçültmesi) kopyayı değil gerçek ayarı görsün.</summary>
    public static void RestoreFor(System.Diagnostics.ProcessStartInfo start)
    {
        if (!Active) return;
        if (OriginalSettings is null) start.Environment.Remove(SettingsVariable);
        else start.Environment[SettingsVariable] = OriginalSettings;
    }

    internal static void ResetForTests()
    {
        Active = false;
        OriginalSettings = null;
    }
}
