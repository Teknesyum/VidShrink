using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>ffmpeg yoksa atlanir. Klipleri testin kendisi <c>testsrc2</c> ile uretir.</summary>
public sealed class FfmpegFactAttribute : FactAttribute
{
    public FfmpegFactAttribute()
    {
        if (!ToolLocator.IsAvailable(out var missing))
            Skip = $"{missing} bulunamadi, kare cekme testleri kosturulmadi.";
    }
}

/// <summary>
/// ffmpeg **ve** ton esleme zinciri gerektiren testler. `zscale`/`tonemap` her derlemede
/// bulunmuyor; yoksa test dusmez, atlanir. Zincirin yoklugu bir kod hatasi degil, ortam
/// bulgusudur — servis o durumda ton eslemesiz kareye duser.
/// </summary>
public sealed class TonemapFactAttribute : FactAttribute
{
    private static readonly Lazy<string> Filters = new(() =>
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ToolLocator.Ffmpeg,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-filters");

            using var process = new Process { StartInfo = psi };
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            stdout.Wait();
            stderr.Wait();
            process.WaitForExit();
            return stdout.Result;
        }
        catch { return ""; }
    });

    public TonemapFactAttribute()
    {
        if (!ToolLocator.IsAvailable(out var missing))
            Skip = $"{missing} bulunamadi, ton esleme testi kosturulmadi.";
        else if (!Filters.Value.Contains(" zscale") || !Filters.Value.Contains(" tonemap"))
            Skip = "Bu ffmpeg derlemesinde zscale/tonemap yok, HDR ton esleme testi kosturulmadi.";
    }
}

/// <summary>
/// ffmpeg **ve** bu makinede gercekten acilan bir donanim kodlayicisi gerektiren olculer.
/// <c>FfmpegFact</c> tek soru soruyordu: ffmpeg var mi. Kodlayicinin adinin
/// <c>ffmpeg -encoders</c> listesinde gecmesi onun bu makinede acilacagi anlamina gelmiyor:
/// surucu yoksa ad listede durur, acilis <c>Cannot load nvcuda.dll</c> ile duser. Ayrim
/// <see cref="EncoderCapabilities"/> icinde zaten var — <c>HasEncoder</c> derlemeyi,
/// <c>Probe</c> bu makineyi yokluyor; geçit ikincisini kullanir. Donanim yoklugu bir kod
/// hatasi degil ortam bulgusudur, o yuzden dusmez, sebebini yazarak atlanir.
/// </summary>
public sealed class HardwareEncoderFactAttribute : FactAttribute
{
    public const string Codec = "h264_nvenc";

    public HardwareEncoderFactAttribute()
    {
        if (!ToolLocator.IsAvailable(out var missing))
        {
            Skip = $"{missing} bulunamadi, donanim kodlayici olculeri kosturulmadi.";
            return;
        }

        if (!EncoderCapabilities.Instance.HasEncoder(Codec))
        {
            Skip = $"{Codec} bu ffmpeg derlemesinde yok, donanim kodlayici olculeri kosturulmadi.";
            return;
        }

        var probe = EncoderCapabilities.Instance.Probe(Codec);
        if (!probe.Succeeded)
            Skip = $"{Codec} derlemede var ama bu makinede acilmadi ({probe.ElapsedMs}ms), " +
                   "donanim kodlayici olculeri kosturulmadi.";
    }
}

/// <summary>
/// Kliplar bir kez uretilir ve butun testler paylasir. T30 ayni yolu kullandi:
/// <c>testsrc2</c> ile uretilmis kisa klipler.
/// </summary>
public sealed class FrameClips : IDisposable
{
    public string Directory { get; }
    public bool Ready { get; }

    /// <summary>320x240, 4 sn, 30 fps, her saniyede bir anahtar kare (-g 30).</summary>
    public string Duz => Path.Combine(Directory, "duz.mp4");

    /// <summary>Ayni kare, gosterim matrisinde 90 derece dondurme: goruntuleme boyutu 240x320.</summary>
    public string Dik => Path.Combine(Directory, "dik.mp4");

    /// <summary>PQ (smpte2084) etiketli 10 bit klip.</summary>
    public string Hdr => Path.Combine(Directory, "hdr.mp4");

    /// <summary>Ayni icerigin SDR karsiligi; HDR kaynagin "cikti" yarisi.</summary>
    public string Sdr => Path.Combine(Directory, "sdr.mp4");

    /// <summary>Video akisi olmayan bozuk dosya.</summary>
    public string Bozuk => Path.Combine(Directory, "bozuk.mp4");

    public FrameClips()
    {
        Directory = Path.Combine(Path.GetTempPath(), "vidshrink-t32-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(Directory);

        if (!ToolLocator.IsAvailable(out _)) return;

        Ready =
            Run("-f", "lavfi", "-i", "testsrc2=size=320x240:rate=30:duration=4",
                "-c:v", "libx264", "-preset", "ultrafast", "-g", "30", "-pix_fmt", "yuv420p", Duz)
            && Run("-display_rotation", "90", "-i", Duz, "-c", "copy", Dik)
            // PQ etiketleri `setparams` ile basilir. Cikis secenegi olarak verilen
            // `-color_trc`/`-color_primaries` bu ffmpeg'de libx264/libx265 uzerinden
            // **sessizce dusuyor**: dosya yaziliyor ama color_transfer alani bos kaliyor.
            && Run("-f", "lavfi", "-i", "testsrc2=size=320x240:rate=30:duration=2",
                "-vf", "setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc",
                "-c:v", "libx265", "-preset", "ultrafast", "-pix_fmt", "yuv420p10le",
                "-tag:v", "hvc1", Hdr)
            && Run("-f", "lavfi", "-i", "testsrc2=size=320x240:rate=30:duration=2",
                "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", Sdr);

        File.WriteAllBytes(Bozuk, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
    }

    private static bool Run(params string[] args)
    {
        var all = new List<string> { "-y", "-hide_banner", "-loglevel", "error" };
        all.AddRange(args);

        using var process = new Process { StartInfo = StartInfo(all) };
        process.Start();
        // Iki boru da bosaltilir; bosaltilmazsa ffmpeg asilir (RAPOR.md:27).
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        stdout.Wait();
        stderr.Wait();
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private static ProcessStartInfo StartInfo(IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        return psi;
    }

    public void Dispose()
    {
        try { System.IO.Directory.Delete(Directory, recursive: true); } catch { }
    }
}

public sealed class FrameGrabberTests : IClassFixture<FrameClips>
{
    private readonly FrameClips _clips;

    public FrameGrabberTests(FrameClips clips) => _clips = clips;

    private static FramePairRequest Request(string source, double at, int width = 160, string? output = null) => new()
    {
        SourcePath = source,
        OutputPath = output,
        AtSeconds = at,
        RequestedWidth = width,
        AlignToKeyframe = false
    };

    // --- K1'in urunu: anahtar kare dizini ---------------------------------------------

    [FfmpegFact]
    public async Task Anahtar_kare_dizini_cikarilir_ve_damgalar_sirali()
    {
        Assert.True(_clips.Ready);

        var index = await KeyframeIndex.BuildAsync(_clips.Duz);

        Assert.NotNull(index);
        Assert.False(index!.IsEmpty);
        Assert.Equal(index.Stamps.OrderBy(s => s), index.Stamps);
        // -g 30, 30 fps, 4 sn: saniyede bir anahtar kare beklenir.
        Assert.InRange(index.Stamps.Count, 3, 6);
    }

    [FfmpegFact]
    public async Task Dizin_hizalama_noktasini_istenen_andan_geriye_tasir()
    {
        var index = await KeyframeIndex.BuildAsync(_clips.Duz);
        Assert.NotNull(index);

        var floor = index!.Floor(2.4);

        Assert.True(floor <= 2.4);
        Assert.DoesNotContain(index.Stamps, s => s > floor && s <= 2.4);
    }

    [FfmpegFact]
    public async Task Bozuk_dosyada_dizin_null_doner_istisna_atmaz()
        => Assert.Null(await KeyframeIndex.BuildAsync(_clips.Bozuk));

    // --- K2: kare servisi --------------------------------------------------------------

    [FfmpegFact]
    public async Task Zaman_damgasi_bir_kare_toleransinda_dogru()
    {
        using var grabber = new FrameGrabber();

        var pair = await grabber.GrabPairAsync(Request(_clips.Duz, 2.0));

        Assert.NotNull(pair);
        Assert.NotNull(pair!.Source);
        // 30 fps: bir kare 0,0333 sn. Teslim edilen damga showinfo'dan okunuyor.
        Assert.InRange(pair.Source!.ActualSeconds, 2.0 - 1.0 / 30.0, 2.0 + 1.0 / 30.0);
    }

    [FfmpegFact]
    public async Task Hizalanan_cekim_anahtar_kare_damgasi_teslim_eder()
    {
        var index = await KeyframeIndex.BuildAsync(_clips.Duz);
        using var grabber = new FrameGrabber();

        var request = Request(_clips.Duz, 2.4) with { AlignToKeyframe = true, SourceKeyframes = index };
        var pair = await grabber.GrabPairAsync(request);

        Assert.NotNull(pair?.Source);
        Assert.Equal(index!.Floor(2.4), pair!.Source!.ActualSeconds, 1);
        // Istenen an degismedi; teslim edilen an degisti ve servis ikisini de bildiriyor.
        Assert.Equal(2.4, pair.Source.RequestedSeconds, 3);
    }

    [FfmpegFact]
    public async Task Sure_disi_arama_null_doner()
    {
        using var grabber = new FrameGrabber();
        Assert.Null(await grabber.GrabPairAsync(Request(_clips.Duz, 999)));
        Assert.Null(await grabber.GrabPairAsync(Request(_clips.Duz, -1)));
    }

    [FfmpegFact]
    public async Task Bozuk_dosya_null_doner_istisna_atmaz()
    {
        using var grabber = new FrameGrabber();
        Assert.Null(await grabber.GrabPairAsync(Request(_clips.Bozuk, 1.0)));
    }

    [FfmpegFact]
    public async Task Onbellek_isabetinde_surec_dogmaz()
    {
        using var grabber = new FrameGrabber();

        var first = await grabber.GrabPairAsync(Request(_clips.Duz, 1.0));
        Assert.NotNull(first);
        var afterFirst = grabber.ProcessesStarted;

        var second = await grabber.GrabPairAsync(Request(_clips.Duz, 1.0));

        Assert.Same(first, second);
        Assert.Equal(afterFirst, grabber.ProcessesStarted);
    }

    [FfmpegFact]
    public async Task Onbellek_bayt_tavaniyla_sinirli_adetle_degil()
    {
        // Tavan tek bir cifti alacak kadar; ikinci cift birincisini dusurmeli.
        var probe = new FrameGrabber();
        var single = await probe.GrabPairAsync(Request(_clips.Duz, 0.5));
        Assert.NotNull(single);
        var pairBytes = single!.Bytes;
        probe.Dispose();

        using var grabber = new FrameGrabber(cacheByteCeiling: pairBytes + 16);

        await grabber.GrabPairAsync(Request(_clips.Duz, 0.5));
        Assert.Equal(1, grabber.CacheCount);
        await grabber.GrabPairAsync(Request(_clips.Duz, 1.5));

        Assert.Equal(1, grabber.CacheCount);
        Assert.True(grabber.CacheBytes <= grabber.CacheByteCeiling);
    }

    [FfmpegFact]
    public async Task Tavani_asan_tek_cift_hic_saklanmaz()
    {
        using var grabber = new FrameGrabber(cacheByteCeiling: 1024);

        var pair = await grabber.GrabPairAsync(Request(_clips.Duz, 1.0));

        Assert.NotNull(pair);
        Assert.Equal(0, grabber.CacheCount);
        Assert.Equal(0, grabber.CacheBytes);
    }

    [FfmpegFact]
    public async Task Iptal_null_doner_ve_surec_birakilmaz()
    {
        using var grabber = new FrameGrabber();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var pair = await grabber.GrabPairAsync(Request(_clips.Duz, 1.0), PreviewState.YalnizKaynak, cts.Token);

        Assert.Null(pair);
    }

    [FfmpegFact]
    public async Task Yakinlastirma_kaynak_cozunurluguyle_tavanlanir_ve_bildirilir()
    {
        using var grabber = new FrameGrabber();

        // Kaynak 320 px genis; 4x panel genisligi (1280) istenirse tavan devreye girer.
        var pair = await grabber.GrabPairAsync(Request(_clips.Duz, 1.0, width: 1280));

        Assert.NotNull(pair?.Source);
        Assert.True(pair!.Source!.WidthCappedBySource);
        Assert.Equal(1280, pair.Source.RequestedWidth);
        Assert.Equal(320, pair.Source.Width);
    }

    [FfmpegFact]
    public async Task Tavan_altinda_istenen_genislik_aynen_teslim_edilir()
    {
        using var grabber = new FrameGrabber();

        var pair = await grabber.GrabPairAsync(Request(_clips.Duz, 1.0, width: 160));

        Assert.NotNull(pair?.Source);
        Assert.False(pair!.Source!.WidthCappedBySource);
        Assert.Equal(160, pair.Source.Width);
    }

    // --- K4: dondurme ve HDR -----------------------------------------------------------

    [FfmpegFact]
    public async Task Dik_cekilmis_videoda_dondurme_uygulanmis_gelir()
    {
        using var grabber = new FrameGrabber();

        var pair = await grabber.GrabPairAsync(Request(_clips.Dik, 1.0));

        Assert.NotNull(pair?.Source);
        Assert.Equal(90, pair!.Source!.AppliedRotation);
        // Kodlanmis kare 320x240 yatay; gosterim matrisi uygulaninca dikey olmali.
        Assert.True(pair.Source.IsPortrait, $"kare {pair.Source.Width}x{pair.Source.Height} geldi, dikey bekleniyordu");
    }

    [FfmpegFact]
    public async Task Iki_yari_ters_yonde_durursa_bildirilir()
    {
        using var grabber = new FrameGrabber();

        // Sol dik, sag yatay: panelin ise yaramaz hale geldigi durum.
        var pair = await grabber.GrabPairAsync(Request(_clips.Dik, 1.0, output: _clips.Duz));

        Assert.NotNull(pair?.Source);
        Assert.NotNull(pair!.Output);
        Assert.True(pair.RotationMismatch);
    }

    [FfmpegFact]
    public async Task Iki_yari_ayni_yondeyse_bildirilmez()
    {
        using var grabber = new FrameGrabber();

        var pair = await grabber.GrabPairAsync(Request(_clips.Duz, 1.0, output: _clips.Sdr));

        Assert.NotNull(pair?.Output);
        Assert.False(pair!.RotationMismatch);
    }

    [TonemapFact]
    public async Task Hdr_kaynak_sdr_ciktiyla_eslesince_ton_eslenir_ve_bildirilir()
    {
        using var grabber = new FrameGrabber();

        var pair = await grabber.GrabPairAsync(Request(_clips.Hdr, 1.0, output: _clips.Sdr));

        Assert.NotNull(pair?.Source);
        Assert.True(pair!.Source!.SourceIsHdr);
        Assert.True(pair.Source.ToneMapped);
        Assert.True(pair.SourceHdrOutputSdr);
        // Cikti yarisina ton esleme uygulanmaz; plan ne yaptiysa piksellerde pismistir.
        Assert.NotNull(pair.Output);
        Assert.False(pair.Output!.ToneMapped);
    }

    [FfmpegFact]
    public async Task Sdr_kaynakta_ton_esleme_uygulanmaz()
    {
        using var grabber = new FrameGrabber();

        var pair = await grabber.GrabPairAsync(Request(_clips.Duz, 1.0, output: _clips.Sdr));

        Assert.NotNull(pair?.Source);
        Assert.False(pair!.Source!.SourceIsHdr);
        Assert.False(pair.Source.ToneMapped);
        Assert.False(pair.SourceHdrOutputSdr);
    }

    // --- K5: durum makinesi ------------------------------------------------------------

    [FfmpegFact]
    public async Task Kodlama_surerken_cekilmez_son_onbellekli_cift_gosterilir()
    {
        using var grabber = new FrameGrabber();

        var delivered = await grabber.GrabPairAsync(Request(_clips.Duz, 1.0));
        Assert.NotNull(delivered);
        var afterFirst = grabber.ProcessesStarted;

        var during = await grabber.GrabPairAsync(Request(_clips.Duz, 2.0), PreviewState.OrnekKodlama);

        Assert.Same(delivered, during);
        Assert.Equal(afterFirst, grabber.ProcessesStarted);
    }

    [FfmpegFact]
    public async Task Kaynak_yokken_hicbir_sey_teslim_edilmez()
    {
        using var grabber = new FrameGrabber();

        var pair = await grabber.GrabPairAsync(Request(_clips.Duz, 1.0), PreviewState.KaynakYok);

        Assert.Null(pair);
        Assert.Equal(0, grabber.ProcessesStarted);
    }

    [FfmpegFact]
    public async Task Cikti_yolu_yoksa_yalniz_sol_yari_gelir()
    {
        using var grabber = new FrameGrabber();

        var pair = await grabber.GrabPairAsync(Request(_clips.Duz, 1.0), PreviewState.YalnizKaynak);

        Assert.NotNull(pair?.Source);
        Assert.Null(pair!.Output);
    }

    [FfmpegFact]
    public async Task Iki_kare_birlikte_gelir_ve_birlikte_saklanir()
    {
        using var grabber = new FrameGrabber();

        // Ayirici cizgi ikisini ayni anda kirpar; tek istek iki kareyi de getirmeli.
        var pair = await grabber.GrabPairAsync(Request(_clips.Duz, 1.0, output: _clips.Sdr));

        Assert.NotNull(pair);
        Assert.NotNull(pair!.Source);
        Assert.NotNull(pair.Output);
        Assert.Equal(pair.Source!.Bytes + pair.Output!.Bytes, pair.Bytes);
        Assert.Equal(pair.Bytes, grabber.CacheBytes);
        Assert.Equal(1, grabber.CacheCount);
    }
}
