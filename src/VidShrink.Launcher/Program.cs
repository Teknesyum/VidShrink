using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using VidShrink.Core;

namespace VidShrink.Launcher;

/// <summary>
/// Kısayolun gösterdiği program. Uygulamayı önce doğurur, bakımı arkasından yapar. Windows
/// çalışan exe ve yüklü dll'ler üzerine yazdırmadığı için app klasörüne yazma
/// <see cref="UygulamaKlasoruKapisi"/>'ndan geçer: klasörden koşan uygulama kalmadan yazılmaz.
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

    private static readonly TimeSpan KapiBeklemesi = TimeSpan.FromMinutes(2);

    [STAThread]
    private static int Main(string[] args)
    {
        var baseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var appDirectory = Path.Combine(baseDirectory, "app");
        var executable = Path.Combine(appDirectory, AppExecutableName);

        if (args.Length > 0 && args[0] == LauncherUpdate.CommitArgument)
            return LauncherUpdate.Commit(baseDirectory, ParentProcessId(args)) ? 0 : 3;

        if (args.Length > 0 && args[0] == LauncherUpdate.InstallOnExitArgument)
            return KapanistaKur(baseDirectory, appDirectory, ParentProcessId(args));

        var updateNow = args.Length > 0 && args[0] == LauncherUpdate.UpdateNowArgument;
        if (updateNow)
        {
            WaitForExit(ParentProcessId(args));
            args = Array.Empty<string>();
        }

        if (!File.Exists(executable))
        {
            Alert($"VidShrink uygulaması bulunamadı:{Environment.NewLine}{executable}{Environment.NewLine}{Environment.NewLine}" +
                  $"Kurulumu yeniden çalıştırın:{Environment.NewLine}{UpdateCheck.UpdateInstruction()}");
            return 1;
        }

        AcilisIzi.Yaz("baslatici");
        var previousVersion = UpdateCheck.ReadVersionMarker(appDirectory);

        if (!updateNow && args.Length > 0 && args[0] == LauncherUpdate.MaintenanceArgument)
        {
            Maintain(baseDirectory, appDirectory, previousVersion, download: true, pendingSwap: false);
            return 0;
        }

        var pending = UpdateStage.HasPending(appDirectory) || InPlaceUpdate.HasPending(appDirectory);
        if (!updateNow && !pending && args.Length > 0 && File.Exists(args[0]))
        {
            UygulamaKlasoruKapisi.Bekle(appDirectory, KapiBeklemesi);
            StartApp(executable, appDirectory, baseDirectory, args);
            AcilisIzi.Yaz("app-dogdu");
            Maintain(baseDirectory, appDirectory, previousVersion, download: true, pendingSwap: false);
            return 0;
        }

        if (pending) ResumePending(appDirectory);

        var swap = false;
        if (updateNow)
        {
            try { swap = Updater.Run(baseDirectory, appDirectory, force: true); }
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

        UygulamaKlasoruKapisi.Bekle(appDirectory, KapiBeklemesi);
        StartApp(executable, appDirectory, baseDirectory, args);
        AcilisIzi.Yaz("app-dogdu");

        Maintain(baseDirectory, appDirectory, previousVersion, download: !updateNow, pendingSwap: swap);
        return 0;
    }

    /// <summary>
    /// Uygulama kapanırken yerinde takas düşünce buraya gelinir. Çağıranın çıkması beklenir,
    /// sahne elle yolun kısa bütçeleriyle kurulur, uygulama yeniden açılmaz: kullanıcı
    /// programı kapattı. Kurulamazsa sahne diskte kalır, bir sonraki kapanış aynı işi dener.
    /// </summary>
    private static int KapanistaKur(string baseDirectory, string appDirectory, int? callerId)
    {
        WaitForExit(callerId);
        var previousVersion = UpdateCheck.ReadVersionMarker(appDirectory);
        var swap = false;
        try { swap = Updater.Run(baseDirectory, appDirectory, force: true); }
        catch (Exception) { }
        Maintain(baseDirectory, appDirectory, previousVersion, download: false, pendingSwap: swap);
        return 0;
    }

    private static void ResumePending(string appDirectory)
    {
        UygulamaKlasoruKapisi.Bekle(appDirectory, KapiBeklemesi);
        if (!UpdateStage.HasPending(appDirectory) && !InPlaceUpdate.HasPending(appDirectory)) return;

        var kapi = UygulamaKlasoruKapisi.BosalincaAl(appDirectory, KapiBeklemesi);
        if (kapi is null)
        {
            UygulamaKlasoruKapisi.HataYaz(appDirectory, "uygulama klasörü boşalmadı");
            return;
        }

        try
        {
            UygulamaKlasoruKapisi.Gecikme();
            InPlaceUpdate.Recover(appDirectory);
            UpdateStage.ResumePending(appDirectory);
            if (!UpdateStage.HasPending(appDirectory) && !InPlaceUpdate.HasPending(appDirectory))
                UygulamaKlasoruKapisi.HatayiSil(appDirectory);
        }
        catch (Exception exception)
        {
            UygulamaKlasoruKapisi.HataYaz(appDirectory, exception.Message);
        }
        finally
        {
            UygulamaKlasoruKapisi.Birak(kapi);
        }
    }

    private static void Maintain(string baseDirectory, string appDirectory, string? previousVersion, bool download, bool pendingSwap)
    {
        UygulamaKlasoruKapisi.Gecikme();
        try { pendingSwap |= LauncherUpdate.Repair(baseDirectory, UpdateCheck.CurrentVersion()); }
        catch (Exception) { }
        try { LauncherUpdate.SeedVersionMarker(baseDirectory, UpdateCheck.CurrentVersion()); }
        catch (Exception) { }
        try { RecordAppliedUpdate(appDirectory, previousVersion); }
        catch (Exception) { }

        if (download)
        {
            try { pendingSwap |= Updater.Run(baseDirectory, appDirectory); }
            catch (Exception) { }
        }

        if (pendingSwap)
        {
            try { StartCommitter(baseDirectory); }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// Uygulamayı doğurur. Tek yer: hızlı tur da normal tur da buradan geçiyor, ikisi
    /// yalnız çağrı sırasında ayrılıyor. İz açıksa başlatıcının doğum anı çocuğa geçer.
    /// </summary>
    private static void StartApp(string executable, string appDirectory, string baseDirectory, string[] args)
    {
        var start = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = appDirectory,
            UseShellExecute = false
        };
        // ffmpeg kurulum kökünde duruyor, app klasöründe değil; uygulama onu PATH'ten bulur.
        start.Environment["PATH"] =
            Path.Combine(baseDirectory, "tools", "ffmpeg") + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        start.Environment[LauncherUpdate.LaunchedVariable] = "1";
        if (AcilisIzi.Acik) start.Environment[AcilisIzi.SifirDegiskeni] = AcilisIzi.SifirIsareti;
        foreach (var argument in args) start.ArgumentList.Add(argument);
        Process.Start(start);
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

    /// <summary>
    /// Düğmeye basan uygulamanın çıkmasını bekler; yüklü dll'ler serbest kalmadan güncelleme
    /// uygulanamaz. Süre dolarsa yine sürülür: kilitli dosya zaten sahnede bekler ve bir
    /// sonraki açılışta yerine geçer.
    /// </summary>
    private static void WaitForExit(int? processId)
    {
        if (processId is null) return;
        try
        {
            using var caller = Process.GetProcessById(processId.Value);
            caller.WaitForExit((int)LauncherUpdate.CommitWindow.TotalMilliseconds);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
        }
    }

    private static int? ParentProcessId(string[] args) =>
        args.Length > 1 && int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;

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
