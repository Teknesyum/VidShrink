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
            if ($Eylem -eq 'next') { $r = Bekle ($s.TrySkipNextAsync()) ([bool]) }
            if ($Eylem -eq 'prev') { $r = Bekle ($s.TrySkipPreviousAsync()) ([bool]) }
            $c = $s.GetPlaybackInfo().Controls
            "oturum|$($s.GetPlaybackInfo().PlaybackStatus)|$r|next=$($c.IsNextEnabled)|prev=$($c.IsPreviousEnabled)|artist=$($p.Artist)"
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

    private static string BaslikliKlip(string klasor, string baslik, string ad = "klip.mp4")
    {
        var yol = Path.Combine(klasor, ad);
        var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, "-y", "-hide_banner", "-threads", "2", "-f", "lavfi", "-i", "testsrc2=size=160x90:rate=25",
            "-f", "lavfi", "-i", "anullsrc=r=48000:cl=mono", "-t", "6", "-c:v", "libx264", "-threads", "2", "-pix_fmt", "yuv420p", "-c:a", "aac",
            "-metadata", "title=" + baslik, yol);
        Assert.True(kod == 0, hata[Math.Max(0, hata.Length - 400)..]);
        return yol;
    }

    private static string SesKlibi(string klasor, string baslik, string sanatci)
    {
        var yol = Path.Combine(klasor, "ses.m4a");
        var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, "-y", "-hide_banner", "-threads", "2", "-f", "lavfi", "-i", "anullsrc=r=48000:cl=mono",
            "-t", "6", "-c:a", "aac", "-threads", "2", "-metadata", "title=" + baslik, "-metadata", "artist=" + sanatci, yol);
        Assert.True(kod == 0, hata[Math.Max(0, hata.Length - 400)..]);
        return yol;
    }

    private static async Task<MpvEngine> AcikMotor(string klip)
    {
        var motor = new MpvEngine(new PlaybackOptions { MediaControls = true });
        motor.SetProperty("ao", "null");
        await motor.OpenAsync(klip);
        motor.Play();
        return motor;
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
            Assert.StartsWith("oturum|Playing||", Assert.Single(oturum), StringComparison.Ordinal);
            Assert.Contains("|True|next=", Assert.Single(tus), StringComparison.Ordinal);
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

    [SmtcFact]
    public async Task KapatilanMotorSmtcOturumuBirakmaz()
    {
        var klasor = Klasor();
        var baslik = "VidShrink SMTC " + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var klip = BaslikliKlip(klasor, baslik);
            var acikken = new List<string>();
            var kapaninca = new List<string>();
            for (var i = 0; i < 3; i++)
            {
                var motor = await AcikMotor(klip);
                await Task.Delay(700);
                acikken.Add(string.Join(" ; ", Oturumlar(klasor, baslik, "")));
                motor.Dispose();
                var kalan = Oturumlar(klasor, baslik, "");
                for (var j = 0; j < 6 && kalan.Length > 0; j++)
                {
                    await Task.Delay(500);
                    kalan = Oturumlar(klasor, baslik, "");
                }

                kapaninca.Add(string.Join(" ; ", kalan));
            }

            var dokum = "açıkken [" + string.Join(" | ", acikken) + "] kapanınca [" + string.Join(" | ", kapaninca) + "]";
            Assert.True(acikken.All(s => s.StartsWith("oturum|Playing|", StringComparison.Ordinal)), dokum);
            Assert.True(kapaninca.All(s => s.Length == 0 || !s.Contains(" ; ", StringComparison.Ordinal)), dokum);        }
        finally
        {
            Sil(klasor);
        }
    }

    [SmtcFact]
    public async Task SmtcSonrakiVeOncekiTusuKomsuyuBirKezIster()
    {
        var klasor = Klasor();
        var baslik = "VidShrink SMTC " + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var klip = BaslikliKlip(klasor, baslik);
            var sonraki = BaslikliKlip(klasor, baslik + " B", "b.mp4");
            var onceki = BaslikliKlip(klasor, baslik + " C", "c.mp4");
            foreach (var (eylem, ileri) in new[] { ("next", true), ("prev", false) })
            {
                using var motor = await AcikMotor(klip);
                var istekler = new List<bool>();
                motor.NeighborRequested += (_, yon) => { lock (istekler) istekler.Add(yon); };
                motor.SetNeighbors(onceki, sonraki);
                await Task.Delay(800);
                var oturum = Assert.Single(Oturumlar(klasor, baslik, ""));
                var tus = Assert.Single(Oturumlar(klasor, baslik, eylem));
                await Bekle(() => { lock (istekler) return istekler.Count > 0; });
                await Task.Delay(800);

                Assert.Contains("|next=True|prev=True|", oturum, StringComparison.Ordinal);
                Assert.StartsWith("oturum|Playing|True|", tus, StringComparison.Ordinal);
                lock (istekler) Assert.Equal(new[] { ileri }, istekler);
            }
        }
        finally
        {
            Sil(klasor);
        }
    }

    [SmtcFact]
    public async Task TekDosyadaVeKuyrukSonundaSmtcTusuKapaliKalir()
    {
        var klasor = Klasor();
        var baslik = "VidShrink SMTC " + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var klip = BaslikliKlip(klasor, baslik);
            var sonraki = BaslikliKlip(klasor, baslik + " B", "b.mp4");
            using var motor = await AcikMotor(klip);
            var istekler = new List<bool>();
            motor.NeighborRequested += (_, yon) => { lock (istekler) istekler.Add(yon); };
            motor.SetNeighbors(null, null);
            await Task.Delay(800);
            var tek = Assert.Single(Oturumlar(klasor, baslik, "next"));
            var sayi = motor.GetProperty("playlist-count");
            motor.SetNeighbors(null, sonraki);
            await Task.Delay(800);
            var son = Assert.Single(Oturumlar(klasor, baslik, "prev"));
            await Task.Delay(800);

            Assert.Equal("1", sayi);
            Assert.Contains("|next=False|prev=False|", tek, StringComparison.Ordinal);
            Assert.Contains("|True|next=True|prev=True|", son, StringComparison.Ordinal);
            lock (istekler) Assert.Empty(istekler);
            Assert.Equal("0", motor.GetProperty("playlist-pos"));
        }
        finally
        {
            Sil(klasor);
        }
    }

    [SmtcFact]
    public async Task SesDosyasindaSanatciEtiketiSmtcyeGecer()
    {
        var klasor = Klasor();
        var baslik = "VidShrink SMTC " + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var ses = SesKlibi(klasor, baslik, "Deneme Sanatci");
            using var motor = await AcikMotor(ses);
            await Task.Delay(800);
            var oturum = Assert.Single(Oturumlar(klasor, baslik, ""));

            Assert.EndsWith("|artist=Deneme Sanatci", oturum, StringComparison.Ordinal);
        }
        finally
        {
            Sil(klasor);
        }
    }

    [FfmpegFact]
    public async Task KomsuListesindeKonumDegisimiTekIstekAtar()
    {
        var klasor = Klasor();
        try
        {
            var klip = BaslikliKlip(klasor, "komsu a");
            var sonraki = BaslikliKlip(klasor, "komsu b", "b.mp4");
            var onceki = BaslikliKlip(klasor, "komsu c", "c.mp4");
            using var motor = await AcikMotor(klip);
            var istekler = new List<bool>();
            motor.NeighborRequested += (_, yon) => { lock (istekler) istekler.Add(yon); };
            motor.SetNeighbors(onceki, sonraki);
            var kurulus = (sayi: motor.GetProperty("playlist-count"), konum: motor.GetProperty("playlist-pos"));
            await Task.Delay(300);
            lock (istekler) Assert.Empty(istekler);

            motor.SetProperty("playlist-pos", "2");
            await Bekle(() => { lock (istekler) return istekler.Count > 0; });
            motor.SetProperty("playlist-pos", "0");
            await Task.Delay(500);
            bool[] ilk;
            lock (istekler) ilk = istekler.ToArray();

            await motor.OpenAsync(klip);
            var yeniden = motor.GetProperty("playlist-count");
            motor.SetNeighbors(onceki, sonraki);
            motor.SetProperty("playlist-pos", "0");
            await Bekle(() => { lock (istekler) return istekler.Count > 1; });
            await Task.Delay(300);
            bool[] ikinci;
            lock (istekler) ikinci = istekler.ToArray();

            Assert.Equal(("3", "1"), kurulus);
            Assert.Equal(new[] { true }, ilk);
            Assert.Equal(new[] { true, false }, ikinci);
            Assert.Equal("1", yeniden);
        }
        finally
        {
            Sil(klasor);
        }
    }

    [Fact]
    public void KuyrukKomsulariMotoraGiderVeIstekKuyruguBirAdimIlerletir()
    {
        var klasor = Klasor();
        try
        {
            foreach (var ad in new[] { "a.mp4", "b.mp4", "c.mp4" }) File.WriteAllBytes(Path.Combine(klasor, ad), new byte[16]);
            string Y(string ad) => Path.Combine(klasor, ad);
            var motorlar = new List<KomsuMotoru>();
            var sonuc = AppHost.Run(() =>
            {
                var view = new PlayerView
                {
                    EngineFactory = () => { var m = new KomsuMotoru(); lock (motorlar) motorlar.Add(m); return m; },
                    HistoryPath = () => Path.Combine(klasor, "gecmis.json"),
                };
                var pencere = new Window { Width = 640, Height = 360, Content = view };
                pencere.Show();
                try
                {
                    Assert.True(view.OpenDroppedItems(new[] { Y("a.mp4"), Y("b.mp4"), Y("c.mp4") }));
                    KomsuBekle(view, view.Navigation);
                    var ilk = motorlar[^1].Komsular.ToArray();

                    var eski = motorlar[^1];
                    eski.Iste(true);
                    KomsuBekle(view, view.Navigation);
                    var ikinci = (yol: Path.GetFileName(view.LoadedPath), komsu: motorlar[^1].Komsular.ToArray(), sayi: motorlar.Count);

                    eski.Iste(true);
                    DenetimSurucu.Wait(view, 0.2);
                    var bayat = (yol: Path.GetFileName(view.LoadedPath), sayi: motorlar.Count);

                    Assert.True(view.RemoveFromList(Y("c.mp4")));
                    var silinince = motorlar[^1].Komsular[^1];

                    motorlar[^1].Iste(true);
                    DenetimSurucu.Wait(view, 0.2);
                    var sonda = (yol: Path.GetFileName(view.LoadedPath), sayi: motorlar.Count, iz: view.Trace[^1]);
                    return (ilk, ikinci, bayat, silinince, sonda);
                }
                finally
                {
                    view.Close();
                    pencere.Close();
                }
            });

            Assert.Equal(new (string?, string?)[] { (null, Y("b.mp4")) }, sonuc.ilk);
            Assert.Equal("b.mp4", sonuc.ikinci.yol);
            Assert.Equal(2, sonuc.ikinci.sayi);
            Assert.Equal(new (string?, string?)[] { (Y("a.mp4"), Y("c.mp4")) }, sonuc.ikinci.komsu);
            Assert.Equal(("b.mp4", 2), sonuc.bayat);
            Assert.Equal((Y("a.mp4"), (string?)null), sonuc.silinince);
            Assert.Equal(("b.mp4", 2, "media-key -> no"), sonuc.sonda);
        }
        finally
        {
            Sil(klasor);
        }
    }

    private static void KomsuBekle(PlayerView view, Task gorev)
    {
        DenetimSurucu.Pump(view, () => gorev.IsCompleted, 10);
        gorev.GetAwaiter().GetResult();
        DenetimSurucu.Wait(view, 0.1);
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

internal sealed class KomsuMotoru : IPlaybackEngine
{
    private readonly byte[] _piksel = Enumerable.Repeat((byte)128, 4 * 64 * 36).ToArray();

    internal List<(string? Onceki, string? Sonraki)> Komsular { get; } = new();

    public string Name => "komsu-motoru";

    public bool IsOpen { get; private set; }

    public double DurationSeconds => 600;

    public bool HasAudio => false;

    public bool IsPaused { get; private set; } = true;

    public bool EndReached => false;

    public double PositionSeconds { get; private set; }

    public double AudioVideoOffsetSeconds => 0;

    public long FramesRendered { get; private set; }

    public event EventHandler<PlaybackFault>? Faulted { add { } remove { } }

    public event EventHandler<bool>? NeighborRequested;

    internal void Iste(bool ileri) => NeighborRequested?.Invoke(this, ileri);

    public void SetNeighbors(string? previous, string? next) => Komsular.Add((previous, next));

    public Task OpenAsync(string path, CancellationToken ct = default)
    {
        IsOpen = true;
        FramesRendered = 1;
        return Task.CompletedTask;
    }

    public void Play() => IsPaused = false;

    public void Pause() => IsPaused = true;

    public Task<SeekResult> SeekAsync(double seconds, SeekPrecision precision, CancellationToken ct = default)
    {
        PositionSeconds = seconds;
        FramesRendered++;
        return Task.FromResult(new SeekResult(SeekOutcome.Shown, 1));
    }

    public bool TryCopyLatest(ref long seen, FrameCopy copy)
    {
        if (seen == FramesRendered) return false;
        seen = FramesRendered;
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(_piksel, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            copy(handle.AddrOfPinnedObject(), 64, 36, 64 * 4);
        }
        finally
        {
            handle.Free();
        }

        return true;
    }

    public void Dispose() => IsOpen = false;
}