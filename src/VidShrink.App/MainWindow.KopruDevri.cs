using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using VidShrink.Core;

namespace VidShrink.App;

public partial class MainWindow
{
    private static int _kopruBasladi;

    internal async Task KopruDevriniBaslatAsync()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS()) return;
        if (Interlocked.Exchange(ref _kopruBasladi, 1) == 1) return;
        var uygun = KopruDevri.Uygun(
            Tasinabilir,
            SettingsPathOverride,
            Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH"),
            Environment.GetEnvironmentVariable("VIDSHRINK_UPDATE_DISABLED"),
            UpdateCheck.AutoUpdateEnabled(UpdateSettings.Load(SettingsPathOverride)));
        if (!uygun) return;

        await AcilisiBekle();
        KopruSonucu sonuc;
        if (OperatingSystem.IsWindows())
        {
            var appDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            if (LauncherUpdate.LocateLauncher(appDirectory) is null) return;
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var calisma = Path.Combine(PortableMode.LocalFolder, "kopru");
            sonuc = await LowPriorityWork.Run(nameof(KopruDevriniBaslatAsync),
                () => KopruDevri.WindowsAsync(KopruOrtami.Gercek, calisma, localAppData, null, CancellationToken.None));
        }
        else
        {
            if (!MacUpdate.CanSwap(Environment.ProcessPath)) return;
            var paket = MacUpdate.BundleOf(Environment.ProcessPath);
            if (paket is null) return;
            var (url, ozet, ad) = KopruDevri.MacArsivi(RuntimeInformation.ProcessArchitecture);
            sonuc = await Task.Run(() => KopruDevri.MacAsync(KopruOrtami.Gercek, paket, url, ozet, ad, null, CancellationToken.None));
        }

        if (sonuc != KopruSonucu.Tamam) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime masaustu)
                masaustu.Shutdown();
        });
    }
}
