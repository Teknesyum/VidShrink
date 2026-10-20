using Avalonia.Controls;
using VidShrink.App.Editing;
using VidShrink.App.Playback;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

public sealed class SmtcFactAttribute : FactAttribute
{
    public SmtcFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "SMTC yalnız Windows'ta var.";
        else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")))
            Skip = "GitHub koşucusunda kullanıcı oturumunun ortam denetimi yöneticisi yok; ölçüm yerelde koşar.";
        else if (!File.Exists(OynaticiOrtamDenetimiTests.WindowsPowerShell))
            Skip = "Windows PowerShell 5.1 yok; SMTC oturumunu okuyan sonda koşmaz.";
        else if (!ToolLocator.IsAvailable(out var eksik))
            Skip = $"{eksik} bulunamadı, başlıklı klip üretilemedi.";
    }
}

public sealed class OynaticiOrtamDenetimiTests
{
    internal static string WindowsPowerShell => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");

    private const string Sonda = """
        param([string]$Baslik, [string]$Eylem)
        Add-Type -AssemblyName System.Runtime.WindowsRuntime
        $asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1
        function Bekle($op, [Type]$tur) { $t = $asTask.MakeGenericMethod($tur).Invoke($null, @($op)); [void]$t.Wait(5000); $t.Result }
        $yonetici = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType = WindowsRuntime]
        $ozellik = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties, Windows.Media.Control, ContentType = WindowsRuntime]
        $mgr = Bekle ($yonetici::RequestAsync()) $yonetici
        foreach ($s in $mgr.GetSessions()) {
          $p = Bekle ($s.TryGetMediaPropertiesAsync()) $ozellik
          if ($p.Title -eq $Baslik) {
            $r = ''
            if ($Eylem -eq 'toggle') { $r = Bekle ($s.TryTogglePlayPauseAsync()) ([bool]) }
            "oturum|$($s.GetPlaybackInfo().PlaybackStatus)|$r"
          }
        }
        "bitti"
        """;

    private static string Klasor()
    {
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "ortam-denetimi", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static void Sil(string klasor)
    {
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        var ust = Path.GetDirectoryName(klasor)!;
        if (Directory.Exists(ust) && !Directory.EnumerateFileSystemEntries(ust).Any()) Directory.Delete(ust);
    }

    private static string BaslikliKlip(string klasor, string baslik)
    {
        var yol = Path.Combine(klasor, "klip.mp4");
        var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, "-y", "-hide_banner", "-threads", "2", "-f", "lavfi", "-i", "testsrc2=size=160x90:rate=25",
            "-f", "lavfi", "-i", "anullsrc=r=48000:cl=mono", "-t", "6", "-c:v", "libx264", "-threads", "2", "-pix_fmt", "yuv420p", "-c:a", "aac",
            "-metadata", "title=" + baslik, yol);
        Assert.True(kod == 0, hata[Math.Max(0, hata.Length - 400)..]);
        return yol;
    }

    private static string[] Oturumlar(string klasor, string baslik, string eylem)
    {
        var betik = Path.Combine(klasor, "sonda.ps1");
        File.WriteAllText(betik, Sonda);
        var (kod, cikti, hata) = GorunumKanit.Kos(WindowsPowerShell, "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", betik, baslik, eylem);
        Assert.True(kod == 0 && cikti.Contains("bitti", StringComparison.Ordinal), $"sonda düştü: {cikti} {hata}");
        return cikti.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(s => s.StartsWith("oturum|", StringComparison.Ordinal)).ToArray();
    }

    private static async Task<bool> Bekle(Func<bool> kosul)
    {
        for (var i = 0; i < 60 && !kosul(); i++) await Task.Delay(100);
        return kosul();
    }

    [SmtcFact]
    public async Task AcikMotorBaslikIleSmtcOturumuAcarVeOrtamTusuDuraklatir()
    {
        var klasor = Klasor();
        var baslik = "VidShrink SMTC " + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var klip = BaslikliKlip(klasor, baslik);
            string[] kapali;
            using (var motor = new MpvEngine(new PlaybackOptions { MediaControls = false }))
            {
                motor.SetProperty("ao", "null");
                await motor.OpenAsync(klip);
                motor.Play();
                await Task.Delay(800);
                kapali = Oturumlar(klasor, baslik, "");
            }

            using var acik = new MpvEngine(new PlaybackOptions { MediaControls = true });
            acik.SetProperty("ao", "null");
            await acik.OpenAsync(klip);
            acik.Play();
            await Task.Delay(800);
            var oturum = Oturumlar(klasor, baslik, "");
            var tus = Oturumlar(klasor, baslik, "toggle");
            var durdu = await Bekle(() => acik.IsPaused);

            Assert.Empty(kapali);
            Assert.Equal("oturum|Playing|", Assert.Single(oturum));
            Assert.EndsWith("|True", Assert.Single(tus), StringComparison.Ordinal);
            Assert.True(durdu, "SMTC oynat/duraklat düğmesi motoru duraklatmadı");
        }
        finally
        {
            Sil(klasor);
        }
    }

    [Fact]
    public void OynaticiMotoruAyarlaAcilirDuzenleyiciOnizlemesiAcmaz()
    {
        var klasor = Klasor();
        try
        {
            var fabrikalar = AppHost.Run(() =>
            {
                var oynatici = new PlayerView { HistoryPath = () => Path.Combine(klasor, "gecmis.json") };
                var varsayilan = oynatici.EngineFactory;
                oynatici.ToggleMediaControls();
                var kapali = oynatici.EngineFactory;
                var yeniden = new PlayerView { HistoryPath = () => Path.Combine(klasor, "gecmis.json") };
                var okunan = yeniden.EngineFactory;
                yeniden.ToggleMediaControls();
                return (varsayilan, kapali, okunan, geri: yeniden.EngineFactory, onizleme: new EditorView().Player.EngineFactory);
            });
            var sonuc = (varsayilan: Secenek(fabrikalar.varsayilan), kapali: Secenek(fabrikalar.kapali), okunan: Secenek(fabrikalar.okunan),
                geri: Secenek(fabrikalar.geri), onizleme: Secenek(fabrikalar.onizleme));

            var beklenen = OperatingSystem.IsWindows() ? "yes" : "no";
            Assert.Equal(beklenen, sonuc.varsayilan);
            Assert.Equal("no", sonuc.kapali);
            Assert.Equal("no", sonuc.okunan);
            Assert.Equal(beklenen, sonuc.geri);
            Assert.Equal("no", sonuc.onizleme);
        }
        finally
        {
            Sil(klasor);
        }
    }

    [WindowsFact]
    public void AcikMotorHemenKapatilincaKilitlenmez()
    {
        var sureler = new List<double>();
        for (var i = 0; i < 12; i++)
        {
            var saat = System.Diagnostics.Stopwatch.StartNew();
            var is_ = Task.Run(() =>
            {
                using var motor = new MpvEngine(new PlaybackOptions { MediaControls = true });
            });
            Assert.True(is_.Wait(TimeSpan.FromSeconds(10)), $"{i}. aç-kapa 10 sn'de bitmedi; libmpv media-controls kurulurken mpv_terminate_destroy kilitlendi");
            sureler.Add(saat.Elapsed.TotalMilliseconds);
        }

        Assert.All(sureler, s => Assert.True(s >= MpvEngine.MediaControlsSettle.TotalMilliseconds - 1, $"süre {s} ms"));
    }

    [Fact]
    public void AyarMenusuAnahtariYalnizOynaticidaVeWindowsta()
    {
        var sayilar = AppHost.Run(() =>
        {
            var baslik = VidShrink.App.Localization.Strings.Get("player.view.media-controls");
            static int Say(IEnumerable<Control> ogeler, string baslik) => ogeler.OfType<MenuItem>().Count(o => Equals(o.Header, baslik));
            return (oynatici: Say(new PlayerView().SettingsItems(), baslik), onizleme: Say(new EditorView().Player.SettingsItems(), baslik));
        });

        Assert.Equal(OperatingSystem.IsWindows() ? 1 : 0, sayilar.oynatici);
        Assert.Equal(0, sayilar.onizleme);
    }

    private static string? Secenek(Func<IPlaybackEngine> fabrika)
    {
        using var motor = (MpvEngine)fabrika();
        return motor.GetProperty("media-controls");
    }
}
