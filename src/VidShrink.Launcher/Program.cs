using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using VidShrink.Core;

namespace VidShrink.Launcher;

/// <summary>
/// Kısayolun gösterdiği program. Windows'ta çalışan bir exe ve yüklü dll'ler üzerine
/// yazılamadığı için güncelleme, uygulama yüklenmeden önce burada uygulanır. Sabit bir
/// sözleşme yürütülür: yarım kalan başlatıcı değişimini topla, manifesti çek, farkı app
/// klasörüne uygula, gerekiyorsa başlatıcının kendisini değiştir, uygulamayı başlat, çık.
///
/// Başlatıcı kendini de günceller ama kendi dosyasını kendisi değiştirmez: çalışırken o adı
/// tutan süreç odur. Yeni ikili yan ada iner, günlük yazılır, uygulama başlatılır ve geçişi
/// yapması için yeni ikili <see cref="LauncherUpdate.CommitArgument"/> ile açılır. O süreç
/// başlatıcının çıkmasını bekler, sonra tek bir üstüne-yazan yeniden adlandırma yapar; hedef
/// ad hiçbir an boşalmaz. Adımlar ve yarım kalma halleri <see cref="LauncherUpdate"/> içinde.
/// </summary>
internal static class Program
{
    private const string AppExecutableName = "VidShrink.App.exe";
    private const string Caption = "VidShrink";

    /// <summary>İndirilenlerin toplandığı klasör; Updater ile aynı ad.</summary>
    private const string StageDirectoryName = "update-stage";

    [STAThread]
    private static int Main(string[] args)
    {
        var baseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var appDirectory = Path.Combine(baseDirectory, "app");
        var executable = Path.Combine(appDirectory, AppExecutableName);

        // Geçiş kipi: bu süreç yerine geçecek ikilinin kendisidir, uygulamayı açmaz.
        if (args.Length > 0 && args[0] == LauncherUpdate.CommitArgument)
            return LauncherUpdate.Commit(baseDirectory, ParentProcessId(args)) ? 0 : 3;

        if (!File.Exists(executable))
        {
            Alert($"VidShrink uygulaması bulunamadı:{Environment.NewLine}{executable}{Environment.NewLine}{Environment.NewLine}" +
                  $"Kurulumu yeniden çalıştırın:{Environment.NewLine}{UpdateCheck.UpdateInstruction()}");
            return 1;
        }

        var previousVersion = UpdateCheck.ReadVersionMarker(appDirectory);

        // Panel ancak eşik dolarsa çizilir; hızlı turda hiç oluşturulmaz. Bloktan çıkış
        // tek yol: iş bitse de yarıda kalsa da panel kapanır ve uygulama açılır.
        var pendingSwap = false;
        using (SplashGate.Arm(() => Status(baseDirectory)))
        {
            // Başlatıcının kendi değişimi yarım kaldıysa önce o okunur: ayar kapalı olsa
            // bile, çünkü burada eksik kalan şey kısayolun gösterdiği dosyanın kendisi.
            // Bu çağrı hedef dosyaya dokunmaz, artıkları siler ve bekleyeni bildirir.
            try { pendingSwap = LauncherUpdate.Repair(baseDirectory, UpdateCheck.CurrentVersion()); }
            catch (Exception) { }

            // Kurulumdan sonraki ilk açılış: kurulu başlatıcının sürümü çalışan ikiliden
            // okunur, sonraki turlarda işareti değişimin kendisi yazar.
            try { LauncherUpdate.SeedVersionMarker(baseDirectory, UpdateCheck.CurrentVersion()); }
            catch (Exception) { }

            // Önceki açılışta kopyalama yarım kaldıysa iş burada tamamlanır.
            try { UpdateStage.ResumePending(appDirectory); }
            catch (Exception) { }

            // Ağ yok, manifest bozuk, disk dolu: hepsinde sessizce vazgeçilir.
            try { pendingSwap |= Updater.Run(baseDirectory, appDirectory); }
            catch (Exception) { }
        }

        try { RecordAppliedUpdate(appDirectory, previousVersion); }
        catch (Exception) { }

        if (!ToolsPresent(baseDirectory, out var missing))
        {
            Alert($"{missing} bulunamadı. VidShrink dönüştürme için FFmpeg'e ihtiyaç duyar." +
                  $"{Environment.NewLine}{Environment.NewLine}Şu komutu çalıştırın:{Environment.NewLine}{UpdateCheck.UpdateInstruction()}" +
                  $"{Environment.NewLine}{Environment.NewLine}veya dosyayı şuraya koyun:{Environment.NewLine}{Path.Combine(baseDirectory, "tools", "ffmpeg")}");
            return 2;
        }

        var start = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = appDirectory,
            UseShellExecute = false
        };
        // ffmpeg kurulum kökünde duruyor, app klasöründe değil; uygulama onu PATH'ten bulur.
        start.Environment["PATH"] =
            Path.Combine(baseDirectory, "tools", "ffmpeg") + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        foreach (var argument in args) start.ArgumentList.Add(argument);
        Process.Start(start);

        // Geçiş en sonda kurulur, çünkü bu sürecin çıkmasını bekliyor.
        if (pendingSwap)
        {
            try { StartCommitter(baseDirectory); }
            catch (Exception) { }
        }

        return 0;
    }

    /// <summary>
    /// Bekleyen başlatıcı geçişini yapacak süreci açar. Açılan dosya geçirilecek ikilinin
    /// kendisidir: kendi adını değiştirmek Windows'ta serbesttir, hedefi silmek ise ancak
    /// bu süreç çıktıktan sonra mümkün. Kurulamazsa hiçbir şey bozulmaz; hedefte eski ikili
    /// durur ve bir sonraki açılış aynı geçişi yeniden kurar.
    /// </summary>
    private static void StartCommitter(string baseDirectory)
    {
        var incoming = LauncherUpdate.Incoming(baseDirectory, LauncherUpdate.ExecutableName);
        if (!File.Exists(incoming)) return;

        var start = new ProcessStartInfo
        {
            FileName = incoming,
            WorkingDirectory = baseDirectory,
            UseShellExecute = false
        };
        start.ArgumentList.Add(LauncherUpdate.CommitArgument);
        start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        Process.Start(start);
    }

    private static int? ParentProcessId(string[] args) =>
        args.Length > 1 && int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;

    /// <summary>
    /// Panelin iki durumu. İndirilenlerin toplandığı klasör görünmüşse iş artık ağ
    /// denetimi değil, uygulamadır. Güncelleyiciye haber kancası eklemeye gerek yok:
    /// klasörün varlığı zaten aynı bilgiyi taşıyor.
    /// </summary>
    private static string Status(string baseDirectory) =>
        Directory.Exists(Path.Combine(baseDirectory, StageDirectoryName))
            ? "Güncelleme uygulanıyor"
            : "Güncelleme kontrol ediliyor";

    /// <summary>
    /// Bir güncelleme uygulandıysa geçilen sürümü uygulamanın okuyacağı yere bırakır.
    /// Panel hiç görünmemiş olsa bile (hızlı tur) bu bilgi kalır; kullanıcı ne olduğunu
    /// sonradan da öğrenebilmeli. İlk kurulumda işaret yazılmaz: geçilmiş bir sürüm yok.
    /// </summary>
    private static void RecordAppliedUpdate(string appDirectory, string? previousVersion)
    {
        var current = UpdateCheck.ReadVersionMarker(appDirectory);
        if (previousVersion is null || current is null || current == previousVersion) return;
        AppliedUpdateNotice.Write(appDirectory, current);
    }

    /// <summary>
    /// ffmpeg sürümle gelmiyor ve güncellenmiyor, ama varlığı doğrulanır. Kullanıcı o
    /// dosyaları silerse sebep görünmez olur.
    /// </summary>
    private static bool ToolsPresent(string baseDirectory, out string missing)
    {
        foreach (var name in new[] { "ffmpeg.exe", "ffprobe.exe" })
        {
            if (File.Exists(Path.Combine(baseDirectory, "tools", "ffmpeg", name))) continue;
            if (FoundOnPath(name)) continue;
            missing = name;
            return false;
        }
        missing = "";
        return true;
    }

    private static bool FoundOnPath(string exe)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(directory.Trim('"'), exe))) return true;
            }
            catch (ArgumentException) { }
        }
        return false;
    }

    private static void Alert(string message) => MessageBoxW(IntPtr.Zero, message, Caption, 0x00000010);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr window, string text, string caption, uint type);
}
