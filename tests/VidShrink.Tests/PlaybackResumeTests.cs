using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Core.Playback;

namespace VidShrink.Tests;

/// <summary>
/// T161: oynatirken durdur/baslat yapilinca, ayar degismedigi halde onden hazirlik
/// (<see cref="PanelHost.PrepareAheadAsync"/>) bastan basliyordu. K1/K3 bu dosyada.
///
/// Gercek animasyon cercevesi (<c>RequestAnimationFrame</c>) basli olmayan pencerede hic
/// akmadigi icin <see cref="PanelHost.Follow"/>'un tetikledigi cagriyi elle uretiyoruz:
/// <see cref="PanelHost.PrepareAheadAsync"/> bu yuzden internal.
/// </summary>
public sealed class PlaybackResumeTests : IClassFixture<SegmentClips>
{
    private readonly SegmentClips _clips;

    public PlaybackResumeTests(SegmentClips clips) => _clips = clips;

    private static MediaInfo Source(string path, double duration = 12) => new()
    {
        FilePath = path,
        FileSizeBytes = 1_000_000,
        DurationSeconds = duration,
        Width = 640,
        Height = 360,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 2_000_000
    };

    private static EncodePlan TwoPassPlan() => new()
    {
        Codec = "libx264",
        Mode = "2pass",
        VideoBitrateK = 300,
        Width = 320,
        Height = 180,
        Fps = 30,
        Preset = "veryfast",
        PixelFormat = "yuv420p",
        AudioCodec = null
    };

    private sealed class SessizKaynak : IComparisonFrameSource
    {
        public event EventHandler<ComparisonSourceStatus>? StatusChanged;
        public ComparisonSourceStatus Status => new(ComparisonSourceState.Bosta, 0, 0, 0, 0, 0);
        public Task StartAsync(ComparisonFrameRequest request, CancellationToken ct = default)
        {
            StatusChanged?.Invoke(this, Status);
            return Task.CompletedTask;
        }
        public bool TryTake(out PlaybackFrame frame) { frame = null!; return false; }
        public void Return(PlaybackFrame frame) { }
        public void Play() { }
        public void Pause() { }
        public Task SeekAsync(TimeSpan position, CancellationToken ct = default) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public void Dispose() { }
    }

    private string Temp()
    {
        var dir = Path.Combine(_clips.Directory, "resume-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static PanelHost KurHost(ComparisonPanel panel, SegmentEncoder encoder)
        => AppHost.Run(() => new PanelHost(panel, () => new SessizKaynak(), encoder));

    /// <summary>
    /// RestartCore panonun gercek olculerine bakar (<c>Measure()</c>, <c>PanelHost.cs</c>);
    /// pencereye hic baglanmamis bir panelde bu olcu sifirdir ve Restart sessizce hicbir
    /// sey yapmaz. Gercek bir Window acmadan layout gecisini elle tetiklemek K4 olcumu icin
    /// yeterli.
    /// </summary>
    private static readonly Size PanelSize = new(640, 480);

    /// <summary>
    /// Panonun kendisine <see cref="Layoutable.Arrange"/> çağırmak boyutu taşımıyor —
    /// tema kaynakları (<c>StaticResource</c>) ve stil eşleşmesi yalnız gerçek bir
    /// <see cref="Window"/> köküne bağlıyken çözülüyor (bkz. <c>ComparisonPanelTests.LayOutAt</c>,
    /// T66/K4 aynı sorunu <c>MainWindow</c> için çözmüştü). Pencere hiç gösterilmez.
    /// </summary>
    private static void Yerlestir(ComparisonPanel panel)
    {
        var window = new Window { Content = panel };
        window.Measure(PanelSize);
        window.Arrange(new Rect(PanelSize));
        window.UpdateLayout();

        var root = (Layoutable)window.GetVisualChildren().Single();
        root.Measure(PanelSize);
        root.Arrange(new Rect(PanelSize));
    }

    /// <summary>Kosumu hazirlar: dosya, plan, ac, aktif parcayi kodla.</summary>
    private async Task<(ComparisonPanel panel, PanelHost host)> Hazirla(SegmentEncoder encoder, MediaInfo info)
    {
        var panel = AppHost.Run(() => new ComparisonPanel());
        var host = KurHost(panel, encoder);
        AppHost.Run(() =>
        {
            Yerlestir(panel);
            host.SetFiles(_clips.Kaynak, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.SetPlan(info, TwoPassPlan(), null);
            host.Open();
        });
        // LoadClipAsync konak iş parçacığında BAŞLATILIR: Avalonia'nın senkronizasyon
        // bağlamı yalnız o iş parçacığında kurulu, RestartCore'un panel dokunuşları
        // (_panel.Controls.* vb.) başka bir iş parçacığından "Call from invalid thread"
        // ile düşer. Devam eden Teardown/StartAsync bekleyişi Bekle() içindeki
        // Dispatcher.UIThread.RunJobs() ile akıtılır (bkz. ShellIntegrationTests.Drain).
        var clipTask = AppHost.Run(() => host.LoadClipAsync(4));
        await Bekle(() => clipTask.IsCompleted);
        await clipTask;
        Assert.NotNull(host.ActiveClip);
        Assert.True(await Bekle(() => host.SourceStatus is not null), "Restart gercekten kosup kaynagi kurmadi");
        return (panel, host);
    }

    private static async Task<bool> Bekle(Func<bool> kosul, int msTimeout = 5000)
    {
        var son = DateTime.UtcNow.AddMilliseconds(msTimeout);
        while (DateTime.UtcNow < son)
        {
            if (kosul()) return true;
            AppHost.Run(() => Dispatcher.UIThread.RunJobs());
            await Task.Delay(5);
        }
        return kosul();
    }

    /// <summary>
    /// K1 + K3: iki kosum yan yana. A onden hazirlik kosarken durdur/baslat yapiyor,
    /// B hicbir sey yapmadan kosuyor. Olcum SegmentEncoder.StartedEncodes: onden
    /// hazirligin kac kez BASTAN baslatildigi.
    /// </summary>
    [FfmpegFact]
    public async Task Durdur_baslat_onden_hazirligi_bastan_baslatmaz()
    {
        Assert.True(_clips.Ready);
        // 12 sn'lik gercek dosyaya bilerek uzun sure verildi: kisa surede SegmentEncoder.Clamp
        // pencereyi WindowSeconds (5 sn) kadar geri cekip PrepareAheadAsync'in kendi tekillik
        // kontrolunu (ActiveClip.EndSeconds karsilastirmasi) her cagrida bozuyordu.
        var info = Source(_clips.Kaynak, duration: 30);

        // ---- Kosum B (kontrol): durdur/baslat yok ----
        var dirB = Temp();
        using var encoderB = new SegmentEncoder(dirB);
        var (panelB, hostB) = await Hazirla(encoderB, info);
        var oncekiB = encoderB.StartedEncodes;

        // PrepareAheadAsync AppHost.Run icinden CAGRILMAZ: Avalonia'nin senkronizasyon
        // baglami o iş parçacığına yapışır ve gercek ffmpeg beklemesinden sonraki devam,
        // hic pompalanmayan bir kuyruga dusup sonsuza dek asılı kalirdi (bu olcum bunu
        // once boyle yazip canli olarak yasadi).
        await hostB.PrepareAheadAsync();
        await hostB.PrepareAheadAsync();
        var kosumB = encoderB.StartedEncodes - oncekiB;
        AppHost.Run(hostB.Dispose);

        // ---- Kosum A: onden hazirlik kosarken durdur/baslat ----
        var dirA = Temp();
        using var encoderA = new SegmentEncoder(dirA);
        var (panelA, hostA) = await Hazirla(encoderA, info);
        var oncekiA = encoderA.StartedEncodes;

        var turA1 = hostA.PrepareAheadAsync();
        Assert.True(await Bekle(() => encoderA.StartedEncodes > oncekiA), "onden hazirlik baslamadi");

        AppHost.Run(() => panelA.Controls.TogglePlay()); // durdur
        AppHost.Run(() => panelA.Controls.TogglePlay()); // baslat
        await turA1;

        await hostA.PrepareAheadAsync();
        var kosumA = encoderA.StartedEncodes - oncekiA;
        AppHost.Run(hostA.Dispose);

        Record(
            "T161 K1 durdur/baslat izgarasi -- onden hazirligin bastan baslama sayisi\n" +
            $"  kontrol (durdur/baslat YOK): {kosumB}\n" +
            $"  durdur/baslat kosumu       : {kosumA}\n" +
            $"  fark (fazladan cagri)      : {kosumA - kosumB}");

        Assert.Equal(1, kosumB);
        // K3: duzeltme sonrasi durdur/baslat da tek seferde biter, kontrol ile ayni sayim.
        Assert.Equal(kosumB, kosumA);
    }

    /// <summary>
    /// K4: dosya degisimi hala Restart tetikliyor. Olcum: yeni kaynak fabrikasi kac kez
    /// cagrildi (Restart her cagirdiginda bir kere kosar).
    /// </summary>
    [FfmpegFact]
    public void Dosya_degisimi_hala_yeniden_kurar()
    {
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var panel = AppHost.Run(() => new ComparisonPanel());
        var sayac = 0;
        var host = AppHost.Run(() => new PanelHost(panel, () => { Interlocked.Increment(ref sayac); return new SessizKaynak(); }, encoder));

        AppHost.Run(() =>
        {
            Yerlestir(panel);
            host.SetFiles(_clips.Kaynak, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.Open();
        });
        var acilistaki = sayac;
        Assert.True(acilistaki >= 1, "Open() kaynak fabrikasini hic cagirmadi");

        // Ikinci Restart eskiyen kaynagi Task.Run ile kapatir (Teardown); AppHost'un
        // konak iş parçacığı Avalonia'nin senkronizasyon bağlamına yapışık ve bu bağlamı
        // hiç kimse pompalamaz. Bekleme sırasında Dispatcher.UIThread.RunJobs() ile
        // bağlamdaki bekleyen devamı elle akıtıyoruz (bkz. ShellIntegrationTests.Drain).
        AppHost.Run(() =>
        {
            host.SetFiles(_clips.Buyuk, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            var son = DateTime.UtcNow.AddSeconds(10);
            while (sayac <= acilistaki && DateTime.UtcNow < son)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(5);
            }
        });
        var dosyaDegisince = sayac;

        Record($"T161 K4 dosya degisimi -- fabrika cagrisi: acilista {acilistaki}, dosya degisince {dosyaDegisince}");
        Assert.True(dosyaDegisince > acilistaki, "dosya degisince Restart kosmadi");
        AppHost.Run(host.Dispose);
    }

    /// <summary>
    /// K4: plan degisimi (yeni bir pencere kodlamasiyla sonuclanan) hala Restart tetikliyor.
    /// Olcum: LoadClipAsync basarili bitince kaynak fabrikasinin yeniden cagrilmasi.
    /// </summary>
    [FfmpegFact]
    public async Task Plan_degisimi_hala_yeniden_kurar()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var panel = AppHost.Run(() => new ComparisonPanel());
        var sayac = 0;
        var host = AppHost.Run(() => new PanelHost(panel, () => { Interlocked.Increment(ref sayac); return new SessizKaynak(); }, encoder));
        var info = Source(_clips.Kaynak);

        AppHost.Run(() =>
        {
            Yerlestir(panel);
            host.SetFiles(_clips.Kaynak, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.SetPlan(info, TwoPassPlan(), null);
            host.Open();
        });
        var ilkParcaOncesi = sayac;

        // LoadClipAsync konak iş parçacığında başlatılır (bkz. Hazirla) — RestartCore'un
        // panel dokunuşları başka bir iş parçacığından "Call from invalid thread" ile düşer.
        var clipTask = AppHost.Run(() => host.LoadClipAsync(4));
        await Bekle(() => clipTask.IsCompleted);
        await clipTask;
        await Bekle(() => sayac > ilkParcaOncesi);
        var ilkParcaSonrasi = sayac;

        Record($"T161 K4 plan degisimi -- fabrika cagrisi: parca oncesi {ilkParcaOncesi}, parca sonrasi {ilkParcaSonrasi}");
        Assert.True(ilkParcaSonrasi > ilkParcaOncesi, "parca hazir olunca Restart kosmadi");
        AppHost.Run(host.Dispose);
    }

    private static void Record(string line)
    {
        try
        {
            var dir = TestPaths.LiveOut("t161");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "olcum.txt"), line + Environment.NewLine + Environment.NewLine);
        }
        catch { }
    }
}
