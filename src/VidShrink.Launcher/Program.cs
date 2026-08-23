using System.Diagnostics;
using System.Runtime.InteropServices;
using VidShrink.Core;

namespace VidShrink.Launcher;

/// <summary>
/// Kısayolun gösterdiği program. Windows'ta çalışan bir exe ve yüklü dll'ler üzerine
/// yazılamadığı için güncelleme, uygulama yüklenmeden önce burada uygulanır.
/// Başlatıcı kendini asla güncellemez ve sabit bir sözleşmeyi yürütür: manifesti çek,
/// farkı app klasörüne uygula, uygulamayı başlat, çık.
/// </summary>
internal static class Program
{
    private const string AppExecutableName = "VidShrink.App.exe";
    private const string Caption = "VidShrink";

    [STAThread]
    private static int Main(string[] args)
    {
        var baseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var appDirectory = Path.Combine(baseDirectory, "app");
        var executable = Path.Combine(appDirectory, AppExecutableName);

        if (!File.Exists(executable))
        {
            Alert($"VidShrink uygulaması bulunamadı:{Environment.NewLine}{executable}{Environment.NewLine}{Environment.NewLine}" +
                  $"Kurulumu yeniden çalıştırın:{Environment.NewLine}{UpdateCheck.UpdateInstruction()}");
            return 1;
        }

        // Önceki açılışta kopyalama yarım kaldıysa iş burada tamamlanır.
        try { UpdateStage.ResumePending(appDirectory); }
        catch (Exception) { }

        // Ağ yok, manifest bozuk, disk dolu: hepsinde sessizce vazgeçilir.
        try { Updater.Run(baseDirectory, appDirectory); }
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
        return 0;
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
