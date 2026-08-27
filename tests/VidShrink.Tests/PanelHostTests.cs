using VidShrink.App;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Core.Playback;
using VidShrink.Ffmpeg.Playback;

namespace VidShrink.Tests;

/// <summary>
/// Panelin sag yarisinin uc durumu ve rozetin kosulu. Pencere acilmiyor: host
/// <see cref="AppHost"/> is parcaciginda kuruluyor, kodlama ise arayuz kuyrugu olmadan
/// bekleniyor. Bu is parcaciginda ileti dongusu yok, dolayisiyla <c>DispatcherTimer</c>
/// atesenmez; gecikmenin arkasindaki is <see cref="PanelHost.LoadClipAsync"/> ile
/// dogrudan kosturuluyor.
/// </summary>
public sealed class PanelHostTests : IClassFixture<SegmentClips>
{
    private readonly SegmentClips _clips;

    public PanelHostTests(SegmentClips clips) => _clips = clips;

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

    private static EncodePlan CrfPlan()
    {
        var plan = TwoPassPlan();
        plan.Mode = "crf";
        plan.Crf = 26;
        return plan;
    }

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
        var dir = Path.Combine(_clips.Directory, "host-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static PanelHost Host(SegmentEncoder encoder)
        => AppHost.Run(() => new PanelHost(new ComparisonPanel(), () => new SessizKaynak(), encoder));

    [Fact]
    public void Tam_cikti_varken_parca_kodlanmaz()
    {
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var host = Host(encoder);

        AppHost.Run(() =>
        {
            host.SetFiles(_clips.Kaynak, _clips.Buyuk, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.SetPlan(Source(_clips.Kaynak), TwoPassPlan(), null);
        });

        Assert.Equal(0, encoder.StartedEncodes);
        Assert.Null(host.ActiveClip);
        Assert.Null(host.ApproximateBadge);
        AppHost.Run(host.Dispose);
    }

    [Fact]
    public void Kaynak_yokken_plan_kodlama_baslatmaz()
    {
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var host = Host(encoder);

        AppHost.Run(() => host.SetPlan(null, null, null));

        Assert.Equal(0, encoder.StartedEncodes);
        Assert.Null(host.ActiveClip);
        AppHost.Run(host.Dispose);
    }

    /// <summary>K2: parca hazir olunca sag yari o parcadan beslenir, perde kalkar.</summary>
    [FfmpegFact]
    public async Task Parca_hazir_olunca_sag_yari_parcayi_gosterir()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var host = Host(encoder);
        AppHost.Run(() =>
        {
            host.SetFiles(_clips.Kaynak, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.SetPlan(Source(_clips.Kaynak), TwoPassPlan(), null);
        });

        await host.LoadClipAsync(4);

        var clip = host.ActiveClip;
        Assert.NotNull(clip);
        Assert.Equal(4, clip!.StartSeconds, 3);
        Assert.NotEqual(clip.SourcePath, clip.EncodedPath);
        AppHost.Run(host.Dispose);
    }

    /// <summary>K4/K7: rozet T47'nin alanindan surulur ve ham ondalik degeri basmaz.</summary>
    [FfmpegFact]
    public async Task Rozet_yaklasik_alanindan_surulur()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var host = Host(encoder);
        AppHost.Run(() =>
        {
            host.SetFiles(_clips.Kaynak, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.SetPlan(Source(_clips.Kaynak), TwoPassPlan(), null);
        });

        await host.LoadClipAsync(4);

        var clip = host.ActiveClip!;
        Assert.True(clip.IsApproximate);
        var badge = host.ApproximateBadge;
        Assert.NotNull(badge);
        Assert.Contains($"CRF {clip.Crf}", badge);
        Assert.DoesNotContain(",", badge);
        Assert.DoesNotContain(".", badge);
        AppHost.Run(host.Dispose);
    }

    /// <summary>Plan zaten kalite tasiyorsa parca kesindir; rozet gorunmez.</summary>
    [FfmpegFact]
    public async Task Kesin_parcada_rozet_yok()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var host = Host(encoder);
        AppHost.Run(() =>
        {
            host.SetFiles(_clips.Kaynak, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.SetPlan(Source(_clips.Kaynak), CrfPlan(), null);
        });

        await host.LoadClipAsync(4);

        Assert.False(host.ActiveClip!.IsApproximate);
        Assert.Null(host.ApproximateBadge);
        AppHost.Run(host.Dispose);
    }

    /// <summary>
    /// T50/K1: rozet uc durumda da panele gercekten geciyor. Parca modunda dolu, tam cikti
    /// modunda ve perde durumunda bos.
    /// </summary>
    [FfmpegFact]
    public async Task Rozet_panele_gecer()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var panel = AppHost.Run(() => new ComparisonPanel());
        var host = AppHost.Run(() => new PanelHost(panel, () => new SessizKaynak(), encoder));

        // Perde: parca da tam cikti da yok.
        AppHost.Run(() =>
        {
            host.SetFiles(_clips.Kaynak, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.SetLanguage(false);
            host.Open();
        });
        Assert.True(string.IsNullOrEmpty(Rozet(panel)));

        // Parca: rozet dolu ve panele gecmis.
        AppHost.Run(() => host.SetPlan(Source(_clips.Kaynak), TwoPassPlan(), null));
        await host.LoadClipAsync(4);
        AppHost.Run(() => host.SetLanguage(false));
        Assert.Equal(host.ApproximateBadge, Rozet(panel));
        Assert.False(string.IsNullOrWhiteSpace(Rozet(panel)));
        var ingilizce = Rozet(panel)!;

        // Dil degisince metin de degisir: birlesik dizgeyi panel kendi ceviremez.
        AppHost.Run(() => host.SetLanguage(true));
        Assert.NotEqual(ingilizce, Rozet(panel));
        Assert.Equal(host.ApproximateBadge, Rozet(panel));

        // Tam cikti: rozet kalkar.
        AppHost.Run(() => host.SetFiles(_clips.Kaynak, _clips.Buyuk, 16.0 / 9, TimeSpan.FromSeconds(12), 30));
        Assert.Null(host.ApproximateBadge);
        Assert.True(string.IsNullOrEmpty(Rozet(panel)));
        AppHost.Run(host.Dispose);
    }

    private static string? Rozet(ComparisonPanel panel) => AppHost.Run(() => panel.ApproxBadgeText.Text);

    /// <summary>
    /// T50/K3: plan ve pencere aynıysa arka arkaya gelen SetPlan cagrilari kodlama
    /// siraya koymaz. Ham sayi: bes cagri sonrasi kac kodlama siraya kondu.
    /// </summary>
    [FfmpegFact]
    public async Task Ayni_planla_yeniden_kodlanmaz()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var host = Host(encoder);
        var info = Source(_clips.Kaynak);
        AppHost.Run(() =>
        {
            host.SetFiles(_clips.Kaynak, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.SetPlan(info, TwoPassPlan(), null);
        });

        await host.LoadClipAsync(4);
        Assert.NotNull(host.ActiveClip);

        var oncesi = host.ScheduledEncodes;
        for (var i = 0; i < 5; i++) AppHost.Run(() => host.SetPlan(info, TwoPassPlan(), null));
        var ayniPlan = host.ScheduledEncodes - oncesi;

        // Plan gercekten degisirse kodlama yine siraya girer.
        var baska = TwoPassPlan();
        baska.VideoBitrateK = 900;
        AppHost.Run(() => host.SetPlan(info, baska, null));
        var degisenPlan = host.ScheduledEncodes - oncesi - ayniPlan;

        Record50($"T50 K3 ayni planla bes SetPlan -> siraya konan kodlama: {ayniPlan}; plan degisince: {degisenPlan}");
        Assert.Equal(0, ayniPlan);
        Assert.Equal(1, degisenPlan);
        AppHost.Run(host.Dispose);
    }

    /// <summary>
    /// T50/K4: perde durumunda sag girdi kaynagin kendisidir ve bu kasitlidir. hstack iki
    /// girdi istiyor; sag girdi bos birakilirsa istek reddedilir ve sol yari da akmaz.
    /// Kullanici o goruntuyu gormez, cunku ayni durumda perde sag yariyi kapatiyor.
    /// </summary>
    [Fact]
    public void Perde_durumunda_sag_girdi_kaynaktir()
    {
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var host = Host(encoder);
        AppHost.Run(() => host.SetFiles(_clips.Kaynak, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30));

        var istek = AppHost.Run(() => host.BuildRequest(new Avalonia.PixelSize(640, 360)));

        Assert.Equal(_clips.Kaynak, istek.LeftPath);
        Assert.Equal(_clips.Kaynak, istek.RightPath);
        istek.Validate();

        // Tam cikti gelince sag girdi gercek ciktidir.
        AppHost.Run(() => host.SetFiles(_clips.Kaynak, _clips.Buyuk, 16.0 / 9, TimeSpan.FromSeconds(12), 30));
        var tam = AppHost.Run(() => host.BuildRequest(new Avalonia.PixelSize(640, 360)));
        Assert.Equal(_clips.Buyuk, tam.RightPath);
        AppHost.Run(host.Dispose);
    }

    /// <summary>K6: bozuk kaynak cokme uretmez, hata kaydedilir, sag yari bos kalir.</summary>
    [FfmpegFact]
    public async Task Bozuk_kaynakta_panel_cokmez()
    {
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var host = Host(encoder);
        AppHost.Run(() =>
        {
            host.SetFiles(_clips.Bozuk, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.SetPlan(Source(_clips.Bozuk), TwoPassPlan(), null);
        });

        await host.LoadClipAsync(0);

        Assert.Null(host.ActiveClip);
        Assert.False(string.IsNullOrWhiteSpace(encoder.LastError));
        AppHost.Run(host.Dispose);
    }

    /// <summary>
    /// K9: pencere sinirindaki oynatma boslugu. Panel bir pencereden otekine gecerken
    /// <c>RestartCore</c> boruyu YENIDEN KURUYOR - eski surec olduruluyor, yenisi aciliyor,
    /// ilk kare bekleniyor. Olcum tam bu ucunu kapsiyor ve ayni sirayla kosuyor; pencere
    /// acilmiyor, kareler borudan dogrudan cekiliyor.
    /// </summary>
    [FfmpegFact]
    public async Task Pencere_sinirindaki_oynatma_boslugu_olculur()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var info = Source(_clips.Kaynak);

        var once = await encoder.RequestAsync(info, TwoPassPlan(), 4);
        var sonra = await encoder.RequestAsync(info, TwoPassPlan(), 6);
        Assert.NotNull(once);
        Assert.NotNull(sonra);
        await Boslugu(once!, sonra!, "640x360 kaynak");

        // 1080p kaynakta sol kesit kayipsiz ve cok daha buyuk; borunun acilisi da oyle olabilir.
        var buyuk = Source(_clips.Buyuk);
        var buyukOnce = await encoder.RequestAsync(buyuk, TwoPassPlan(), 4);
        var buyukSonra = await encoder.RequestAsync(buyuk, TwoPassPlan(), 6);
        Assert.NotNull(buyukOnce);
        Assert.NotNull(buyukSonra);
        await Boslugu(buyukOnce!, buyukSonra!, "1080p kaynak");
    }

    private static async Task Boslugu(PreviewClip once, PreviewClip sonra, string etiket)
    {
        var bosluklar = new List<double>();
        for (var tur = 0; tur < 5; tur++)
        {
            var eski = Pipe(once);
            await eski.StartAsync(Istek(once));
            Assert.True(await IlkKare(eski), "ilk pencere kare vermedi");

            // Panelin sinirda yaptigi is, ayni sirayla: Teardown -> yeni kaynak -> StartAsync
            // -> ilk kare. Sayac kullanicinin gordugu son kareden baslar.
            var saat = System.Diagnostics.Stopwatch.StartNew();
            await Task.Run(eski.Dispose);
            var yeni = Pipe(sonra);
            await yeni.StartAsync(Istek(sonra));
            var geldi = await IlkKare(yeni);
            saat.Stop();
            yeni.Dispose();

            Assert.True(geldi, "ikinci pencere kare vermedi");
            bosluklar.Add(saat.Elapsed.TotalMilliseconds);
        }

        Record($"K9 pencere sinirinda boru yeniden kurulmasi ({etiket}), ms: "
            + string.Join(" ", bosluklar.Select(ms => ms.ToString("0"))));
        Assert.All(bosluklar, ms => Assert.True(ms > 0));
    }

    /// <summary>
    /// T50/K2 hazirlik olcumu: 127 ms'in nereye gittigini ayirir. Eski surecin oldurulmesi,
    /// yeni surecin acilmasi ve ilk karenin gelmesi ayri ayri sayilir; hangisinin
    /// kaldirilabilecegi buna bakilarak secilir.
    /// </summary>
    [FfmpegFact]
    public async Task Sinir_maliyeti_parcalara_ayrilir()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var info = Source(_clips.Kaynak);
        var once = await encoder.RequestAsync(info, TwoPassPlan(), 4);
        var sonra = await encoder.RequestAsync(info, TwoPassPlan(), 6);
        Assert.NotNull(once);
        Assert.NotNull(sonra);

        var olum = new List<double>();
        var acilis = new List<double>();
        var ilk = new List<double>();
        for (var tur = 0; tur < 5; tur++)
        {
            var eski = Pipe(once!);
            await eski.StartAsync(Istek(once!));
            Assert.True(await IlkKare(eski), "ilk pencere kare vermedi");

            var saat = System.Diagnostics.Stopwatch.StartNew();
            await Task.Run(eski.Dispose);
            olum.Add(saat.Elapsed.TotalMilliseconds);

            saat.Restart();
            var yeni = Pipe(sonra!);
            await yeni.StartAsync(Istek(sonra!));
            acilis.Add(saat.Elapsed.TotalMilliseconds);

            saat.Restart();
            Assert.True(await IlkKare(yeni), "ikinci pencere kare vermedi");
            ilk.Add(saat.Elapsed.TotalMilliseconds);
            yeni.Dispose();
        }

        Record50("T50 sinir maliyeti, eski surecin olumu ms: " + Birlestir(olum));
        Record50("T50 sinir maliyeti, yeni surecin acilisi ms: " + Birlestir(acilis));
        Record50("T50 sinir maliyeti, ilk karenin beklenmesi ms: " + Birlestir(ilk));
    }

    /// <summary>
    /// T50/K2 sonrasi olcumu. Devir: yeni boru <b>eski oldurulmeden</b> aciliyor, eski boru
    /// bu sirada kare vermeye devam ediyor, degisim ilk yeni kare hazir olunca yapiliyor.
    /// Olculen sey kullanicinin gordugu sey: ekrana konan son eski kare ile ilk yeni kare
    /// arasindaki sure. Yaninda ilk yeni karenin sunum damgasi da yaziliyor - donma
    /// kapanirken icerik atlanmadigini ancak o sayi gosterir.
    /// </summary>
    [FfmpegFact]
    public async Task Devir_sinirindaki_bosluk_olculur()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var info = Source(_clips.Kaynak);
        var once = await encoder.RequestAsync(info, TwoPassPlan(), 4);
        var sonra = await encoder.RequestAsync(info, TwoPassPlan(), 6);
        Assert.NotNull(once);
        Assert.NotNull(sonra);
        await Devri(once!, sonra!, "640x360 kaynak");

        var buyuk = Source(_clips.Buyuk);
        var buyukOnce = await encoder.RequestAsync(buyuk, TwoPassPlan(), 4);
        var buyukSonra = await encoder.RequestAsync(buyuk, TwoPassPlan(), 6);
        Assert.NotNull(buyukOnce);
        Assert.NotNull(buyukSonra);
        await Devri(buyukOnce!, buyukSonra!, "1080p kaynak");
    }

    private static async Task Devri(PreviewClip once, PreviewClip sonra, string etiket)
    {
        var bosluklar = new List<double>();
        var damgalar = new List<double>();
        for (var tur = 0; tur < 5; tur++)
        {
            var eski = Pipe(once);
            await eski.StartAsync(Istek(once));
            Assert.True(await IlkKare(eski), "ilk pencere kare vermedi");

            // Devir baslar: yeni boru aciliyor, eski hala ayakta ve kare veriyor.
            var yeni = Pipe(sonra);
            var acilis = yeni.StartAsync(Istek(sonra));

            var sonEski = System.Diagnostics.Stopwatch.StartNew();
            var son = DateTime.UtcNow.AddSeconds(10);
            while (yeni.Status.ProducedFrames == 0 && DateTime.UtcNow < son)
            {
                if (eski.TryTake(out var kare)) { eski.Return(kare); sonEski.Restart(); }
                await Task.Delay(1);
            }
            await acilis;

            PlaybackFrame? ilk = null;
            while (ilk is null && DateTime.UtcNow < son)
            {
                if (yeni.TryTake(out var kare)) ilk = kare;
                else await Task.Delay(1);
            }
            sonEski.Stop();
            Assert.NotNull(ilk);
            damgalar.Add(ilk!.Presentation.TotalMilliseconds);
            yeni.Return(ilk);

            bosluklar.Add(sonEski.Elapsed.TotalMilliseconds);
            _ = Task.Run(eski.Dispose);
            yeni.Dispose();
        }

        Record50($"T50 devirle sinir boslugu ({etiket}), ms: " + Birlestir(bosluklar));
        Record50($"T50 devirde ilk yeni karenin damgasi ({etiket}), ms: " + Birlestir(damgalar));
    }

    private static string Birlestir(IEnumerable<double> degerler)
        => string.Join(" ", degerler.Select(ms => ms.ToString("0")));

    /// <summary>T50 olcumleri kendi klasorune iner; T48'in dosyasina dokunulmaz.</summary>
    private static void Record50(string line)
    {
        try
        {
            var dir = TestPaths.LiveOut("t50");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "olcum.txt"), line + Environment.NewLine);
        }
        catch { }
    }

    private const int OlcumPanelWidth = 640;
    private const int OlcumPanelHeight = 360;

    private static PipeComparisonFrameSource Pipe(PreviewClip clip)
    {
        _ = clip;
        return new PipeComparisonFrameSource();
    }

    private static ComparisonFrameRequest Istek(PreviewClip clip) => new()
    {
        LeftPath = clip.SourcePath,
        RightPath = clip.EncodedPath,
        PanelWidth = OlcumPanelWidth,
        PanelHeight = OlcumPanelHeight,
        Fps = 30,
        Realtime = true,
        Loop = false
    };

    /// <summary>Ilk kare gelene kadar bekler; kare iade edilir, havuz kurumaz.</summary>
    private static async Task<bool> IlkKare(IComparisonFrameSource source)
    {
        var son = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < son)
        {
            if (source.TryTake(out var frame)) { source.Return(frame); return true; }
            await Task.Delay(1);
        }
        return false;
    }

    /// <summary>Olcum ciktisi projenin kendi calisma klasorune iner; git'e sizmaz.</summary>
    private static void Record(string line)
    {
        try
        {
            var dir = TestPaths.LiveOut("t48");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "olcum.txt"), line + Environment.NewLine);
        }
        catch { }
    }

    /// <summary>K5: panel kapaninca gecici dosya kalmaz.</summary>
    [FfmpegFact]
    public async Task Panel_kapaninca_gecici_dosya_kalmaz()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        var encoder = new SegmentEncoder(dir);
        var host = Host(encoder);
        AppHost.Run(() =>
        {
            host.SetFiles(_clips.Kaynak, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.SetPlan(Source(_clips.Kaynak), TwoPassPlan(), null);
        });

        await host.LoadClipAsync(4);
        Assert.NotEmpty(Directory.GetFiles(dir, SegmentEncoder.TempPrefix + "*"));

        AppHost.Run(host.Dispose);

        Assert.Empty(Directory.GetFiles(dir, SegmentEncoder.TempPrefix + "*"));
    }
}
