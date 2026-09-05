using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Core.Playback;
using VidShrink.Ffmpeg.Playback;

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

    /// <summary>Imzayi degistiren ikinci plan: ffmpeg'e gecen bit hizi baska.</summary>
    private static EncodePlan BaskaPlan()
    {
        var plan = TwoPassPlan();
        plan.VideoBitrateK = 900;
        return plan;
    }

    /// <summary>
    /// Kare uretmeyen kaynak. Konagin boruya ne yaptigini sayar: <see cref="PlayCount"/>,
    /// <see cref="PauseCount"/>, <see cref="SeekCount"/>. Konum iddiasi (D3) bu sayilarla
    /// ve fabrikanin kac ornek urettigiyle olculuyor; gercek borunun kare damgasi ayri bir
    /// olcude (<c>Duraklatilan_boru_kaldigi_karenin_ardindan_surer</c>).
    /// </summary>
    private sealed class SessizKaynak : IComparisonFrameSource
    {
        public event EventHandler<ComparisonSourceStatus>? StatusChanged;
        public ComparisonSourceStatus Status => new(ComparisonSourceState.Bosta, 0, 0, 0, 0, 0);
        public int PlayCount;
        public int PauseCount;
        public int SeekCount;
        public int StartCount;
        public Task StartAsync(ComparisonFrameRequest request, CancellationToken ct = default)
        {
            Interlocked.Increment(ref StartCount);
            StatusChanged?.Invoke(this, Status);
            return Task.CompletedTask;
        }
        public bool TryTake(out PlaybackFrame frame) { frame = null!; return false; }
        public void Return(PlaybackFrame frame) { }
        public void Play() => Interlocked.Increment(ref PlayCount);
        public void Pause() => Interlocked.Increment(ref PauseCount);
        public Task SeekAsync(TimeSpan position, CancellationToken ct = default)
        {
            Interlocked.Increment(ref SeekCount);
            return Task.CompletedTask;
        }
        public Task StopAsync() => Task.CompletedTask;
        public void Dispose() { }
    }

    private string Temp()
    {
        var dir = Path.Combine(_clips.Directory, "resume-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// RestartCore panonun gercek olculerine bakar (<c>Measure()</c>, <c>PanelHost.cs</c>);
    /// pencereye hic baglanmamis bir panelde bu olcu sifirdir ve Restart sessizce hicbir
    /// sey yapmaz. Gercek bir Window acmadan layout gecisini elle tetiklemek K4 olcumu icin
    /// yeterli.
    /// </summary>
    private static readonly Size PanelSize = new(640, 480);

    /// <summary>Boyut kolunun (K4) ikinci olcusu; farki ResizeTolerance'in (8 px) cok ustunde.</summary>
    private static readonly Size KucukPanelSize = new(320, 240);

    /// <summary>
    /// Panonun kendisine <see cref="Layoutable.Arrange"/> çağırmak boyutu taşımıyor —
    /// tema kaynakları (<c>StaticResource</c>) ve stil eşleşmesi yalnız gerçek bir
    /// <see cref="Window"/> köküne bağlıyken çözülüyor (bkz. <c>ComparisonPanelTests.LayOutAt</c>,
    /// T66/K4 aynı sorunu <c>MainWindow</c> için çözmüştü). Pencere hiç gösterilmez.
    /// </summary>
    private static Window Yerlestir(ComparisonPanel panel)
    {
        var window = new Window { Content = panel };
        YenidenOlc(window, PanelSize);
        return window;
    }

    /// <summary>
    /// Pencereyi verilen olcuyle yeniden yerlestirir. K4'un boyut kolu bunu ikinci kez,
    /// baska bir olcuyle cagirir; pano gercekten kuculur ve <c>Frames.SizeChanged</c>
    /// kendiliginden atesenir.
    /// </summary>
    private static void YenidenOlc(Window window, Size size)
    {
        window.Measure(size);
        window.Arrange(new Rect(size));
        window.UpdateLayout();

        var root = (Layoutable)window.GetVisualChildren().Single();
        root.Measure(size);
        root.Arrange(new Rect(size));
    }

    /// <summary>Kosumu hazirlar: dosya, plan, ac, aktif parcayi kodla.</summary>
    private async Task<(ComparisonPanel panel, PanelHost host, Window pencere)> Hazirla(
        SegmentEncoder encoder, MediaInfo info, Func<IComparisonFrameSource>? fabrika = null)
    {
        var panel = AppHost.Run(() => new ComparisonPanel());
        var host = AppHost.Run(() => new PanelHost(panel, fabrika ?? (() => new SessizKaynak()), encoder));
        Window pencere = null!;
        AppHost.Run(() =>
        {
            pencere = Yerlestir(panel);
            host.SetFiles(_clips.Kaynak, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.SetPlan(info, TwoPassPlan(), null);
            host.Open();
        });
        // LoadClipAsync konak iş parçacığında BAŞLATILIR: Avalonia'nın senkronizasyon
        // bağlamı yalnız o iş parçacığında kurulu, RestartCore'un panel dokunuşları
        // (_panel.Controls.* vb.) başka bir iş parçacığından "Call from invalid thread"
        // ile düşer. Devam eden Teardown/StartAsync bekleyişi Bekle() içindeki
        // Dispatcher.UIThread.RunJobs() ile akıtılır (bkz. ShellIntegrationTests.Drain).
        //
        // Parca 2 sn'den baslar: dosya 12 sn, pencere 5 sn, SegmentEncoder.Clamp'in son
        // baslangici 12 - 5 = 7. [2,7] penceresinin ardili Clamp(12, 7) = 7, yani dosyanin
        // icinde kaliyor ve PrepareAheadAsync'in tekillik kontrolu kirpilmadan gecebiliyor.
        var clipTask = AppHost.Run(() => host.LoadClipAsync(2));
        await Bekle(() => clipTask.IsCompleted);
        await clipTask;
        Assert.NotNull(host.ActiveClip);
        Assert.True(await Bekle(() => host.SourceStatus is not null), "Restart gercekten kosup kaynagi kurmadi");
        return (panel, host, pencere);
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
        // Sure gercek dosyanin suresi: 12 sn. Kirpilma karistiricisi pencereyi 4 yerine
        // 2 sn'den baslatarak kaldirildi (bkz. Hazirla), sureyi buyuterek degil.
        var info = Source(_clips.Kaynak);

        // ---- Kosum B (kontrol): durdur/baslat yok ----
        var dirB = Temp();
        using var encoderB = new SegmentEncoder(dirB);
        var (panelB, hostB, _) = await Hazirla(encoderB, info);
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
        var (panelA, hostA, _) = await Hazirla(encoderA, info);
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
    /// Ayri bir olcu: ilk parca kodlanip hazir olunca <c>LoadClipAsync</c>'in basari
    /// dalindaki <c>Restart</c> kosuyor mu.
    ///
    /// <b>Bu kol plan degisimini olcmez.</b> Tur 2'de <c>Plan_degisimi_hala_yeniden_kurar</c>
    /// adiyla duruyordu ve adi olctugu seyi soylemiyordu (denetim bulgusu D2): <c>SetPlan</c>
    /// bir kez ve <c>Open()</c>'dan once cagriliyor, imza zinciri hic kosmuyordu. Gercek plan
    /// degisimi asagida, <see cref="Plan_degisimi_hala_yeniden_kurar"/> icinde.
    /// </summary>
    [FfmpegFact]
    public async Task Ilk_parca_hazir_olunca_yeniden_kurar()
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

        Record($"T161 K4 ilk parca hazir -- fabrika cagrisi: parca oncesi {ilkParcaOncesi}, parca sonrasi {ilkParcaSonrasi}");
        Assert.True(ilkParcaSonrasi > ilkParcaOncesi, "parca hazir olunca Restart kosmadi");
        AppHost.Run(host.Dispose);
    }

    /// <summary>
    /// K4/plan kolu (denetim bulgusu D2). Ekranda calisan bir pencere varken <b>ikinci bir
    /// plan</b> veriliyor ve imza zinciri gercekten kosuyor: <c>SetPlan</c> -&gt;
    /// <c>ClipSignature</c> karsilastirmasi -&gt; <c>ScheduleClip</c> -&gt; gecikme -&gt;
    /// <c>LoadClipAsync</c> -&gt; yeni kodlama -&gt; <c>Restart</c>.
    ///
    /// Iki yonlu olculuyor: <b>ayni</b> plan yeniden verildiginde hicbir sey siraya
    /// girmemeli (imza esitligi), <b>farkli</b> plan verildiginde girmeli. Tek yonlu bir olcu,
    /// imza karsilastirmasi tumden kaldirilsa bile yesil kalirdi.
    ///
    /// Gecikme sayaci (<c>DispatcherTimer</c>) bu is parcaciginda hic atesenmiyor; tikin isi
    /// <see cref="PanelHost.SegmentDelayElapsed"/> ile suruluyor, sayacin kurulmus oldugu
    /// <see cref="PanelHost.ClipScheduled"/> ile ayrica olculuyor.
    /// </summary>
    [FfmpegFact]
    public async Task Plan_degisimi_hala_yeniden_kurar()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var info = Source(_clips.Kaynak);
        var sayac = 0;
        var (_, host, _) = await Hazirla(encoder, info, () => { Interlocked.Increment(ref sayac); return new SessizKaynak(); });

        Assert.NotNull(host.ActiveClip);
        var oncekiFabrika = sayac;
        var oncekiSira = host.ScheduledEncodes;
        var oncekiKodlama = encoder.StartedEncodes;

        AppHost.Run(() => host.SetPlan(info, TwoPassPlan(), null));
        var ayniPlanSira = host.ScheduledEncodes;
        var ayniPlanGecikme = AppHost.Run(() => host.ClipScheduled);

        AppHost.Run(() => host.SetPlan(info, BaskaPlan(), null));
        var yeniPlanSira = host.ScheduledEncodes;
        var yeniPlanGecikme = AppHost.Run(() => host.ClipScheduled);

        var yukle = AppHost.Run(() => host.SegmentDelayElapsed());
        await Bekle(() => yukle.IsCompleted, 60000);
        await yukle;
        await Bekle(() => sayac > oncekiFabrika);

        Record(
            "T161 K4 plan degisimi -- imza zinciri\n" +
            $"  baslangic         : sira {oncekiSira}, fabrika {oncekiFabrika}, kodlama {oncekiKodlama}\n" +
            $"  ayni plan verildi : sira {ayniPlanSira}, gecikme kurulu {ayniPlanGecikme}\n" +
            $"  yeni plan verildi : sira {yeniPlanSira}, gecikme kurulu {yeniPlanGecikme}\n" +
            $"  gecikme sonrasi   : fabrika {sayac}, kodlama {encoder.StartedEncodes}");

        Assert.Equal(oncekiSira, ayniPlanSira);
        Assert.False(ayniPlanGecikme, "ayni plan bos yere kodlama siraya koydu");
        Assert.True(yeniPlanSira > oncekiSira, "plan degisimi kodlama siraya koymadi");
        Assert.True(yeniPlanGecikme, "plan degisimi gecikme sayacini kurmadi");
        Assert.True(encoder.StartedEncodes > oncekiKodlama, "yeni plan icin kodlama kosmadi");
        Assert.True(sayac > oncekiFabrika, "plan degisince Restart kosmadi");
        AppHost.Run(host.Dispose);
    }

    /// <summary>
    /// K4/boyut kolu (denetim bulgusu D1). Zincir: <c>Frames.SizeChanged</c> -&gt;
    /// <c>OnResized</c> -&gt; <c>_settle.Start()</c> -&gt; tik -&gt;
    /// <see cref="PanelHost.SettleElapsed"/> -&gt; <c>Restart</c>.
    ///
    /// Pano gercekten kuculuyor, <c>SizeChanged</c> olayi elle atilmiyor. Sayacin kurulmasi
    /// <see cref="PanelHost.ResizeSettling"/> ile ayrica olculuyor: yalniz tiki surmek,
    /// <c>OnResized</c>'in kapilari (terfi, MinPanelEdge, ResizeTolerance) tumden kaldirilsa
    /// bile yesil kalirdi.
    /// </summary>
    [FfmpegFact]
    public void Boyut_degisimi_hala_yeniden_kurar()
    {
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var panel = AppHost.Run(() => new ComparisonPanel());
        var sayac = 0;
        var host = AppHost.Run(() => new PanelHost(panel, () => { Interlocked.Increment(ref sayac); return new SessizKaynak(); }, encoder));

        Window pencere = null!;
        AppHost.Run(() =>
        {
            pencere = Yerlestir(panel);
            host.SetFiles(_clips.Kaynak, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.Open();
        });
        var acilistaki = sayac;
        Assert.True(acilistaki >= 1, "Open() kaynak fabrikasini hic cagirmadi");
        var acilistaSayac = AppHost.Run(() => host.ResizeSettling);

        AppHost.Run(() => YenidenOlc(pencere, KucukPanelSize));
        var kuculunceSayac = AppHost.Run(() => host.ResizeSettling);

        AppHost.Run(() =>
        {
            host.SettleElapsed();
            var son = DateTime.UtcNow.AddSeconds(10);
            while (sayac <= acilistaki && DateTime.UtcNow < son)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(5);
            }
        });
        var boyutDegisince = sayac;

        Record(
            "T161 K4 boyut degisimi -- yerlesme sayaci ve fabrika cagrisi\n" +
            $"  acilista       : fabrika {acilistaki}, yerlesme sayaci kurulu {acilistaSayac}\n" +
            $"  pano kuculunce : yerlesme sayaci kurulu {kuculunceSayac}\n" +
            $"  tik sonrasi    : fabrika {boyutDegisince}");

        Assert.False(acilistaSayac, "acilista yerlesme sayaci bos yere kuruldu");
        Assert.True(kuculunceSayac, "boyut degisimi yerlesme sayacini kurmadi");
        Assert.True(boyutDegisince > acilistaki, "boyut degisince Restart kosmadi");
        AppHost.Run(host.Dispose);
    }

    /// <summary>
    /// K3'un ikinci yarisi, konak tarafi (denetim bulgusu D3). "Kaldigi konumdan surer"in
    /// konak icin anlami: durdur/baslat <b>boruyu yeniden kurmaz</b> (Teardown ve yeni
    /// fabrika cagrisi yok, ayakta duran boruya ikinci bir <c>StartAsync</c> gitmez),
    /// <b>geriye sarmaz</b> (<c>SeekAsync</c> yok) ve <b>pencereyi degistirmez</b>
    /// (<c>ActiveClip</c> ayni ornek). Boruya giden tek sey bir Pause ve bir Play.
    ///
    /// Borunun kendi kare damgasinin devam ettigi ayri olculuyor
    /// (<see cref="Duraklatilan_boru_kaldigi_karenin_ardindan_surer"/>); bu kol onu iddia etmez.
    /// </summary>
    [FfmpegFact]
    public async Task Durdur_baslat_boruyu_yeniden_kurmaz()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var info = Source(_clips.Kaynak);
        var kaynaklar = new List<SessizKaynak>();
        var (panel, host, _) = await Hazirla(encoder, info, () =>
        {
            var kaynak = new SessizKaynak();
            lock (kaynaklar) kaynaklar.Add(kaynak);
            return kaynak;
        });

        var pencere = host.ActiveClip;
        Assert.NotNull(pencere);
        int oncekiOrnek;
        SessizKaynak canli;
        lock (kaynaklar) { oncekiOrnek = kaynaklar.Count; canli = kaynaklar[^1]; }
        var oncekiPlay = canli.PlayCount;
        var oncekiPause = canli.PauseCount;
        var oncekiSeek = canli.SeekCount;
        var oncekiStart = canli.StartCount;

        AppHost.Run(() => panel.Controls.TogglePlay());
        AppHost.Run(() => panel.Controls.TogglePlay());
        await Bekle(() => false, 300);

        int sonrakiOrnek;
        lock (kaynaklar) sonrakiOrnek = kaynaklar.Count;
        var sonrakiPencere = host.ActiveClip;

        Record(
            "T161 K3 durdur/baslat -- boru ayakta mi\n" +
            $"  kaynak ornegi (fabrika cagrisi): once {oncekiOrnek}, sonra {sonrakiOrnek}\n" +
            $"  ayakta duran boru StartAsync   : once {oncekiStart}, sonra {canli.StartCount}\n" +
            $"  Pause/Play/Seek                : {canli.PauseCount - oncekiPause}/{canli.PlayCount - oncekiPlay}/{canli.SeekCount - oncekiSeek}\n" +
            $"  pencere baslangici (sn)        : once {pencere!.StartSeconds:0.###}, sonra {sonrakiPencere?.StartSeconds:0.###}");

        Assert.Equal(oncekiOrnek, sonrakiOrnek);
        Assert.Equal(oncekiStart, canli.StartCount);
        Assert.Equal(1, canli.PauseCount - oncekiPause);
        Assert.Equal(1, canli.PlayCount - oncekiPlay);
        Assert.Equal(0, canli.SeekCount - oncekiSeek);
        Assert.Same(pencere, sonrakiPencere);
        AppHost.Run(host.Dispose);
    }

    /// <summary>
    /// K3'un ikinci yarisi, boru tarafi (denetim bulgusu D3). Gercek
    /// <see cref="PipeComparisonFrameSource"/> uzerinde olculuyor, konak hic isin icinde
    /// degil: kareler alinir, <c>Pause()</c> sonrasi halka bosaltilir (bekleyen kare kalmasin,
    /// yoksa "devam etti" sonucu <c>Play()</c> hic calismadan da cikardi), <c>Play()</c>
    /// sonrasi gelen ilk karenin damgasi duraklamadaki son damganin <b>hemen ardinda</b> mi
    /// diye bakilir.
    ///
    /// Iki yonlu esik: geriye dusen damga bastan baslamadir, bir saniyeden fazla ileri
    /// atlayan damga icerik atlamasidir.
    /// </summary>
    [FfmpegFact]
    public async Task Duraklatilan_boru_kaldigi_karenin_ardindan_surer()
    {
        Assert.True(_clips.Ready);
        using var source = new PipeComparisonFrameSource();
        await source.StartAsync(new ComparisonFrameRequest
        {
            LeftPath = _clips.Kaynak,
            RightPath = _clips.Kaynak,
            PanelWidth = 320,
            PanelHeight = 180,
            Fps = 30,
            Realtime = true,
            Loop = false
        });
        source.Play();

        var alinan = 0;
        var sonDamga = TimeSpan.MinValue;
        var son = DateTime.UtcNow.AddSeconds(30);
        while (alinan < 5 && DateTime.UtcNow < son)
        {
            if (source.TryTake(out var kare)) { sonDamga = kare.Presentation; source.Return(kare); alinan++; }
            else await Task.Delay(5);
        }
        Assert.True(alinan >= 5, $"5 kare beklendi, {alinan} alindi");

        source.Pause();
        while (source.TryTake(out var bayat)) { sonDamga = bayat.Presentation; source.Return(bayat); }
        var duraklamada = sonDamga;
        await Task.Delay(500);

        source.Play();
        var devam = TimeSpan.MinValue;
        son = DateTime.UtcNow.AddSeconds(30);
        while (devam == TimeSpan.MinValue && DateTime.UtcNow < son)
        {
            if (source.TryTake(out var kare)) { devam = kare.Presentation; source.Return(kare); }
            else await Task.Delay(5);
        }

        Record(
            "T161 K3 duraklatilan boru -- kare damgasi (sn)\n" +
            $"  duraklamadaki son kare : {duraklamada.TotalSeconds:0.###}\n" +
            $"  devam eden ilk kare    : {devam.TotalSeconds:0.###}\n" +
            $"  fark                   : {(devam - duraklamada).TotalSeconds:0.###}");

        Assert.True(devam > TimeSpan.MinValue, "devam ettikten sonra hic kare gelmedi");
        Assert.True(devam > duraklamada,
            $"devam eden ilk kare durdugu yerin gerisinde: {devam.TotalSeconds:0.###} <= {duraklamada.TotalSeconds:0.###}");
        Assert.True((devam - duraklamada).TotalSeconds < 1.0,
            $"devam ederken ileri atladi: {(devam - duraklamada).TotalSeconds:0.###} sn");
        await source.StopAsync();
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
