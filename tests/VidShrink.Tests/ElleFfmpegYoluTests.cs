using System.ComponentModel;
using VidShrink.App;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Ayarlar'daki elle ffmpeg yolu gerçekten bağlı: <see cref="ToolLocator"/> onu ve yanındaki
/// ffprobe'u döndürür, açılışta kayıtlı ayar uygulanır, pencerede değişince hemen geçer,
/// geçersiz yolda otomatik sıraya düşülür ve hata satırı görünür. Sahte ffmpeg boş bir
/// dosyadır; kanıt <c>.calisma/test-ciktilari/elle-ffmpeg/</c>, yeşil koşum siler.
/// </summary>
public sealed class ElleFfmpegYoluTests
{
    private static string Kok => Path.GetFullPath(Path.Combine(TestPaths.OutputRoot, "elle-ffmpeg"));

    private static string Exe(string ad) => OperatingSystem.IsWindows() ? ad + ".exe" : ad;

    private static string SahteKurulum(bool ffprobeIle = true, string altKlasor = "")
    {
        var klasor = Path.Combine(Kok, Guid.NewGuid().ToString("N"), altKlasor);
        Directory.CreateDirectory(klasor);
        File.WriteAllBytes(Path.Combine(klasor, Exe("ffmpeg")), Array.Empty<byte>());
        if (ffprobeIle) File.WriteAllBytes(Path.Combine(klasor, Exe("ffprobe")), Array.Empty<byte>());
        return Path.Combine(klasor, Exe("ffmpeg"));
    }

    private static string Yaninda(string ffmpeg) => Path.Combine(Path.GetDirectoryName(ffmpeg)!, Exe("ffprobe"));

    /// <summary>
    /// Pencerenin arka plan yoklaması sahte exe'yi kısa süre tutabiliyor; silme birkaç kez denenir.
    /// </summary>
    private static void Kapat()
    {
        for (var deneme = 0; deneme < 50 && Directory.Exists(Kok); deneme++)
        {
            try { Directory.Delete(Kok, true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Thread.Sleep(100); }
        }
    }

    [Fact]
    public void LocateElleYoluVeYanindakiFfprobeuDondurur()
    {
        var elle = SahteKurulum();
        var paketli = SahteKurulum(altKlasor: Path.Combine("tools", "ffmpeg"));
        var paketKoku = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(paketli)!, "..", ".."));
        var ffprobesuz = SahteKurulum(ffprobeIle: false);

        Assert.Equal(elle, ToolLocator.Locate("ffmpeg", "", paketKoku, elle));
        Assert.Equal(Yaninda(elle), ToolLocator.Locate("ffprobe", "", paketKoku, elle));

        Assert.Equal(paketli, ToolLocator.Locate("ffmpeg", "", paketKoku));
        Assert.Equal(Yaninda(paketli), ToolLocator.Locate("ffprobe", "", paketKoku));
        Assert.Equal(paketli, ToolLocator.Locate("ffmpeg", "", paketKoku, ffprobesuz));
        Assert.Equal(paketli, ToolLocator.Locate("ffmpeg", "", paketKoku, Path.Combine(Kok, "yok", Exe("ffmpeg"))));

        Kapat();
    }

    [Fact]
    public void ElleYolSurecCagrisinaGiderOtomatikEskiSirayaDoner()
    {
        var otomatik = ToolLocator.Ffmpeg;
        var otomatikProbe = ToolLocator.Ffprobe;
        var elle = SahteKurulum();
        try
        {
            Assert.True(ToolLocator.UseManual(elle));
            Assert.Equal(elle, ToolLocator.Ffmpeg);
            Assert.Equal(Yaninda(elle), ToolLocator.Ffprobe);
            Assert.Throws<Win32Exception>(() => ToolLocator.GetFfmpegVersion());

            Assert.False(ToolLocator.UseManual(SahteKurulum(ffprobeIle: false)));
            Assert.Null(ToolLocator.Manual);
            Assert.Equal(otomatik, ToolLocator.Ffmpeg);
        }
        finally { ToolLocator.UseAutomatic(); }

        Assert.Equal(otomatik, ToolLocator.Ffmpeg);
        Assert.Equal(otomatikProbe, ToolLocator.Ffprobe);
        Kapat();
    }

    [Fact]
    public void AcilistaKayitliElleYolUygulanirOtomatikKipteUygulanmaz()
    {
        var otomatik = ToolLocator.Ffmpeg;
        var elle = SahteKurulum();
        var ayar = Path.Combine(Kok, "settings-" + Guid.NewGuid().ToString("N") + ".json");
        var onceki = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
        string elleAcilis, otomatikAcilis;
        try
        {
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", ayar);
            new AppSettings { FfmpegPathMode = 1, FfmpegPath = elle }.Save(ayar);
            VidShrink.App.App.LoadStartupSettings();
            elleAcilis = ToolLocator.Ffmpeg;

            new AppSettings { FfmpegPathMode = 0, FfmpegPath = elle }.Save(ayar);
            VidShrink.App.App.LoadStartupSettings();
            otomatikAcilis = ToolLocator.Ffmpeg;
        }
        finally
        {
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", onceki);
            ToolLocator.UseAutomatic();
        }

        Assert.Equal(elle, elleAcilis);
        Assert.Equal(otomatik, otomatikAcilis);
        Kapat();
    }

    [Fact]
    public void PenceredeDegisinceHemenGecerGecersizdeOtomatigeDuserVeSoylenir()
    {
        var otomatik = ToolLocator.Ffmpeg;
        var elle = SahteKurulum();
        var ikinci = SahteKurulum();
        var ayar = Path.Combine(Kok, "settings-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var r = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = ayar };
                try
                {
                    window.RestoreAppSettingsForTest(new AppSettings { FfmpegPathMode = 1, FfmpegPath = elle });
                    var geriYuklenen = ToolLocator.Ffmpeg;
                    var hataYokken = window.TxtFfmpegPathError.IsVisible;

                    window.TxtFfmpegPath.Text = Path.Combine(Kok, "yok", Exe("ffmpeg"));
                    var gecersiz = ToolLocator.Ffmpeg;
                    var hataGorunur = window.TxtFfmpegPathError.IsVisible;
                    var hataMetni = window.TxtFfmpegPathError.Text ?? "";
                    var durum = window.TxtSystemStatus.Text ?? "";

                    window.TxtFfmpegPath.Text = ikinci;
                    var degisen = ToolLocator.Ffmpeg;
                    var kaydedilen = AppSettings.Load(ayar).FfmpegPath;

                    window.RbFfmpegAuto.IsChecked = true;
                    var otomatikKip = ToolLocator.Ffmpeg;
                    return (geriYuklenen, hataYokken, gecersiz, hataGorunur, hataMetni, durum, degisen, kaydedilen, otomatikKip);
                }
                finally
                {
                    window.Close();
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                }
            });

            Assert.Equal(elle, r.geriYuklenen);
            Assert.False(r.hataYokken, "gecerli elle yolda hata satiri gorundu");
            Assert.Equal(otomatik, r.gecersiz);
            Assert.True(r.hataGorunur, "gecersiz yolda hata satiri gorunmedi");
            Assert.NotEqual("", r.hataMetni);
            Assert.Contains(r.hataMetni, r.durum, StringComparison.Ordinal);
            Assert.Contains("FFmpeg: " + otomatik, r.durum, StringComparison.Ordinal);
            Assert.Equal(ikinci, r.degisen);
            Assert.Equal(ikinci, r.kaydedilen);
            Assert.Equal(otomatik, r.otomatikKip);
        }
        finally
        {
            AppHost.Run(() => { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return 0; });
            ToolLocator.UseAutomatic();
        }

        Kapat();
    }
}
