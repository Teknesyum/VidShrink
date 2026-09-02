using System.Reflection;
using System.Text.RegularExpressions;
using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class PreviewSegmentTests
{
    private static MediaInfo Source(double durationSeconds = 60, double fps = 60, int width = 1920, int height = 1080) => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 40_000_000,
        DurationSeconds = durationSeconds,
        Width = width,
        Height = height,
        Fps = fps,
        VideoCodec = "h264",
        TotalBitrateBps = 5_700_000
    };

    private static EncodePlan TwoPassPlan(string codec = "libx264", int videoK = 1200) => new()
    {
        Codec = codec,
        Mode = "2pass",
        VideoBitrateK = videoK,
        Width = 1280,
        Height = 720,
        Fps = 30,
        Preset = "slow",
        PixelFormat = "yuv420p",
        AudioCodec = "aac",
        AudioBitrateK = 128
    };

    private static EncodePlan CrfPlan(int crf = 24)
    {
        var plan = TwoPassPlan();
        plan.Mode = "crf";
        plan.Crf = crf;
        return plan;
    }

    [Fact]
    public void Pencere_en_az_bes_saniye_ilerisini_gosterir()
    {
        Assert.True(
            PreviewSegment.WindowSeconds >= 5.0,
            $"Onizleme penceresi {PreviewSegment.WindowSeconds:0.###} sn; en az 5 sn olmali.");

        var segment = PreviewSegment.For(Source(durationSeconds: 60), TwoPassPlan(), 10, "ornek.mp4");

        Assert.True(
            segment.DurationSeconds >= 5.0,
            $"Parca {segment.DurationSeconds:0.###} sn kodlaniyor.");
        Assert.Contains("-t", segment.Arguments);
        Assert.Contains(
            PreviewSegment.WindowSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            segment.Arguments);
    }

    [Fact]
    public void Varsayilan_sure_pencere_tavani_kadar()
    {
        var segment = PreviewSegment.For(Source(), TwoPassPlan(), 10, "ornek.mp4");

        Assert.Equal(PreviewSegment.WindowSeconds, segment.DurationSeconds, 6);
        Assert.False(segment.WasClamped);
    }

    [Fact]
    public void Kaynagin_sonunda_sure_kirpilir_ve_kirpma_gorunur()
    {
        var segment = PreviewSegment.For(Source(durationSeconds: 60), TwoPassPlan(), 59.4, "ornek.mp4");

        Assert.Equal(0.6, segment.DurationSeconds, 6);
        Assert.Equal(PreviewSegment.WindowSeconds, segment.RequestedDurationSeconds, 6);
        Assert.True(segment.WasClamped);
    }

    [Fact]
    public void Negatif_baslangic_reddedilir()
        => Assert.Throws<ArgumentOutOfRangeException>(() => PreviewSegment.For(Source(), TwoPassPlan(), -0.5, "ornek.mp4"));

    [Fact]
    public void Sifir_sure_reddedilir()
        => Assert.Throws<ArgumentOutOfRangeException>(() => PreviewSegment.For(Source(), TwoPassPlan(), 1, "ornek.mp4", durationSeconds: 0));

    [Fact]
    public void Kaynagin_disinda_baslangic_reddedilir()
        => Assert.Throws<ArgumentOutOfRangeException>(() => PreviewSegment.For(Source(durationSeconds: 60), TwoPassPlan(), 60, "ornek.mp4"));

    [Fact]
    public void Iki_gecisli_plan_bitrate_yerine_kalite_degeri_alir()
    {
        var segment = PreviewSegment.For(Source(), TwoPassPlan(), 10, "ornek.mp4");

        Assert.Equal(PreviewQuality.Yaklasik, segment.Quality.Kind);
        Assert.NotNull(segment.Quality.Crf);
        Assert.True(segment.IsApproximate);
        Assert.Equal(EncodeMode.Crf, segment.Plan.ModeEnum);
    }

    [Fact]
    public void Crf_plani_ayni_degerle_kodlanir_ve_kesin_sayilir()
    {
        var segment = PreviewSegment.For(Source(), CrfPlan(24), 10, "ornek.mp4");

        Assert.Equal(PreviewQuality.Kesin, segment.Quality.Kind);
        Assert.Equal(24, segment.Quality.Crf);
        Assert.False(segment.DroppedSecondPass);
        Assert.False(segment.IsApproximate);
    }

    [Fact]
    public void Modellenmemis_kodlayici_acikca_desteklenmiyor_der()
    {
        var plan = TwoPassPlan(codec: "libvpx-vp9");

        var segment = PreviewSegment.For(Source(), plan, 10, "ornek.mp4");

        Assert.Equal(PreviewQuality.Desteklenmiyor, segment.Quality.Kind);
        Assert.Null(segment.Quality.Crf);
        Assert.DoesNotContain("-crf", segment.Arguments);
    }

    /// <summary>
    /// Planlayicinin kabul ettigi kodlayici listesini kaynaktan okur. Liste buraya
    /// kopyalanmaz: kopyalanirsa surukleme de kopyalanir ve olcum hicbir sey korumaz.
    /// </summary>
    private static IReadOnlyList<string> AllowedCodecs()
    {
        var field = typeof(PlanParser).GetField("AllowedCodecs", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        var codecs = (string[])field!.GetValue(null)!;
        Assert.NotEmpty(codecs);
        return codecs;
    }

    [Fact]
    public void Planlayicinin_kabul_ettigi_her_kodlayici_siniflandirilmis()
    {
        var siniflandirilmamis = new List<string>();

        foreach (var codec in AllowedCodecs())
        {
            var choice = PreviewSegment.QualityFor(Source(), TwoPassPlan(codec));
            if (choice.Kind == PreviewQuality.Desteklenmiyor || choice.Crf is null)
            {
                siniflandirilmamis.Add(codec);
                continue;
            }

            var (min, max) = CodecModel.CrfRange(codec);
            Assert.InRange(choice.Crf.Value, min, max);
            Assert.Equal(PreviewQuality.Yaklasik, choice.Kind);
        }

        Assert.True(
            siniflandirilmamis.Count == 0,
            "PlanParser.AllowedCodecs'te olup PreviewSegment'in kalite olcegini bilmedigi kodlayici: "
                + string.Join(", ", siniflandirilmamis));
    }

    [Fact]
    public void Daha_yuksek_bitrate_daha_dusuk_crf_verir()
    {
        var dusuk = PreviewSegment.QualityFor(Source(), TwoPassPlan(videoK: 600)).Crf!.Value;
        var yuksek = PreviewSegment.QualityFor(Source(), TwoPassPlan(videoK: 4800)).Crf!.Value;

        Assert.True(yuksek < dusuk, $"4800k icin {yuksek}, 600k icin {dusuk}");
    }

    [Fact]
    public void Sonuc_kodlayicinin_araligina_kelepcelenir()
    {
        var (min, max) = CodecModel.CrfRange("libx264");

        Assert.Equal(min, PreviewSegment.QualityFor(Source(), TwoPassPlan(videoK: 500_000)).Crf!.Value, 6);
        Assert.Equal(max, PreviewSegment.QualityFor(Source(), TwoPassPlan(videoK: 1)).Crf!.Value, 6);
    }

    [Fact]
    public void Karsilik_projenin_kendi_bagintisindan_gelir()
    {
        var info = Source();
        var plan = TwoPassPlan();
        var profile = ComplexityProfile.FromSourceBitrate(info);

        var bppf = PlanCalculator.BitsPerPixel(plan.VideoBitrateK, plan.Width, plan.Height, plan.Fps);
        var beklenen = profile.CrfForBppf(plan.Codec, bppf, (double)plan.Width / info.Width, plan.Fps, info.Fps);
        var (min, max) = CodecModel.CrfRange(plan.Codec);

        Assert.Equal(Math.Clamp(beklenen, min, max), PreviewSegment.QualityFor(info, plan).Crf!.Value, 6);
    }

    [Fact]
    public void Iki_gecis_dusurulur_ve_sapma_gorunur()
    {
        var segment = PreviewSegment.For(Source(), TwoPassPlan(), 10, "ornek.mp4");

        Assert.True(segment.DroppedSecondPass);
        Assert.DoesNotContain("-pass", segment.Arguments);
        Assert.DoesNotContain("-passlogfile", segment.Arguments);
    }

    [Fact]
    public void Donanim_kodlayicisinda_ikinci_gecis_zaten_yok()
    {
        var segment = PreviewSegment.For(Source(), TwoPassPlan(codec: "av1_nvenc"), 10, "ornek.mp4");

        Assert.False(segment.DroppedSecondPass);
        Assert.True(segment.IsApproximate);
    }

    [Fact]
    public void Cekirdek_dosyalari_surec_ve_dosya_cagrisi_tasimaz()
    {
        var yasak = new Regex(@"\b(Process|File|Directory)\s*\.|using\s+Avalonia");

        foreach (var name in new[] { "PreviewSegment.cs", "PreviewTimeline.cs", "FfmpegArguments.cs" })
        {
            var path = Path.Combine(TipSources.Root, "src", "VidShrink.Core", name);
            var source = File.ReadAllText(path);
            var hit = yasak.Match(source);
            Assert.False(hit.Success, $"{name}: {hit.Value}");
        }
    }

    /// <summary>
    /// Sahne uzunlugu <paramref name="sahneSaniye"/> olan duz bir harita. Turetilen haritada tek
    /// esik olmadigi icin <c>Threshold</c> <c>NaN</c>; kural alanda durur.
    /// </summary>
    private static SceneMap Harita(double sahneSaniye, double sure)
    {
        var sahneler = new List<Scene>();
        for (var i = 0; i * sahneSaniye < sure; i++)
        {
            var bas = i * sahneSaniye;
            var son = Math.Min(bas + sahneSaniye, sure);
            sahneler.Add(new Scene { Index = i, Start = bas, End = son, Bits = 1_000_000, Complexity = 1.0 });
        }
        return new SceneMap
        {
            Threshold = double.NaN,
            Duration = sure,
            Scenes = sahneler,
            Rule = ThresholdRule.Measured
        };
    }

    private static int Aralik(IReadOnlyList<string> arguments)
    {
        var at = arguments.ToList().IndexOf("-g");
        Assert.True(at >= 0, "argumanlarda -g yok");
        return int.Parse(arguments[at + 1], System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public void Onizleme_haritayi_arguman_uretimine_gecirir()
    {
        var info = Source(durationSeconds: 120, fps: 60);
        var plan = TwoPassPlan();
        var harita = Harita(sahneSaniye: 3.0, sure: 120);

        var haritali = PreviewSegment.For(info, plan, 10, "ornek.mp4", scenes: harita);
        var haritasiz = PreviewSegment.For(info, plan, 10, "ornek.mp4");

        Assert.Same(harita, haritali.Scenes);
        Assert.Null(haritasiz.Scenes);
        Assert.Equal((int)Math.Round(plan.Fps * FfmpegArguments.KeyframeCeilingMinSeconds), Aralik(haritali.Arguments));
        Assert.Equal((int)Math.Round(plan.Fps * FfmpegArguments.KeyframeCeilingDefaultSeconds), Aralik(haritasiz.Arguments));
        Assert.NotEqual(Aralik(haritasiz.Arguments), Aralik(haritali.Arguments));
    }

    [Fact]
    public void Onizleme_ve_nihai_kodlama_ayni_anahtar_kare_araligini_verir()
    {
        var info = Source(durationSeconds: 120, fps: 60);
        var plan = TwoPassPlan();

        foreach (var harita in new SceneMap?[] { null, Harita(3.0, 120), Harita(7.5, 120), Harita(40.0, 120) })
        {
            var onizleme = PreviewSegment.For(info, plan, 10, "parca.mp4", scenes: harita);
            var nihai = FfmpegArguments.Build(info, plan, "cikti.mp4", 2, null, null, harita);

            Assert.Equal(Aralik(nihai), Aralik(onizleme.Arguments));
        }
    }

    private sealed class SessizKaynak : VidShrink.Core.Playback.IComparisonFrameSource
    {
        public event EventHandler<VidShrink.Core.Playback.ComparisonSourceStatus>? StatusChanged;
        public VidShrink.Core.Playback.ComparisonSourceStatus Status
            => new(VidShrink.Core.Playback.ComparisonSourceState.Bosta, 0, 0, 0, 0, 0);
        public Task StartAsync(VidShrink.Core.Playback.ComparisonFrameRequest request, CancellationToken ct = default)
        {
            StatusChanged?.Invoke(this, Status);
            return Task.CompletedTask;
        }
        public bool TryTake(out VidShrink.Core.Playback.PlaybackFrame frame) { frame = null!; return false; }
        public void Return(VidShrink.Core.Playback.PlaybackFrame frame) { }
        public void Play() { }
        public void Pause() { }
        public Task SeekAsync(TimeSpan position, CancellationToken ct = default) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public void Dispose() { }
    }

    [Fact]
    public void Arayuz_onizleme_zinciri_nihai_kodlamayla_ayni_tavani_verir()
    {
        var info = Source(durationSeconds: 120, fps: 60);
        var plan = TwoPassPlan();

        foreach (var harita in new SceneMap?[] { null, Harita(3.0, 120), Harita(7.5, 120), Harita(40.0, 120) })
        {
            using var kodlayici = new VidShrink.App.Playback.SegmentEncoder(Path.GetTempPath())
            {
                Scenes = harita
            };

            var onizleme = kodlayici.Describe(info, plan, 10, "parca.mp4", null);
            var nihai = FfmpegArguments.Build(info, plan, "cikti.mp4", 2, null, null, harita);

            Assert.Equal(Aralik(nihai), Aralik(onizleme.Arguments));
        }
    }

    [Fact]
    public void Baglanmamis_onizleme_zinciri_nihai_kodlamadan_ayrisir()
    {
        var info = Source(durationSeconds: 120, fps: 60);
        var plan = TwoPassPlan();
        var harita = Harita(sahneSaniye: 3.0, sure: 120);

        using var baglanmamis = new VidShrink.App.Playback.SegmentEncoder(Path.GetTempPath());

        var onizleme = baglanmamis.Describe(info, plan, 10, "parca.mp4", null);
        var nihai = FfmpegArguments.Build(info, plan, "cikti.mp4", 2, null, null, harita);

        Assert.Equal((int)Math.Round(plan.Fps * FfmpegArguments.KeyframeCeilingDefaultSeconds), Aralik(onizleme.Arguments));
        Assert.Equal((int)Math.Round(plan.Fps * FfmpegArguments.KeyframeCeilingMinSeconds), Aralik(nihai));
        Assert.NotEqual(Aralik(nihai), Aralik(onizleme.Arguments));
    }

    [Fact]
    public void Panel_haritayi_parca_kodlayicisina_iletir()
    {
        var harita = Harita(sahneSaniye: 3.0, sure: 120);
        using var kodlayici = new VidShrink.App.Playback.SegmentEncoder(Path.GetTempPath());
        var panel = AppHost.Run(() => new VidShrink.App.Playback.PanelHost(
            new VidShrink.App.Playback.ComparisonPanel(), () => new SessizKaynak(), kodlayici));

        Assert.Null(kodlayici.Scenes);

        panel.Scenes = harita;
        Assert.Same(harita, kodlayici.Scenes);
        Assert.Same(harita, panel.Scenes);

        panel.Scenes = null;
        Assert.Null(kodlayici.Scenes);
    }

    [Fact]
    public void Pencere_haritayi_onizleme_paneline_gecirir()
    {
        var source = File.ReadAllText(TipSources.WindowCodePath);

        Assert.Contains("if (_preview is not null) _preview.Scenes = _sceneMap?.Map;", source);
    }

    [Fact]
    public void Harita_yoksa_onizleme_on_saniyelik_varsayilana_duser()
    {
        var plan = TwoPassPlan();
        var segment = PreviewSegment.For(Source(durationSeconds: 120), plan, 10, "ornek.mp4", scenes: null);

        Assert.Equal(
            (int)Math.Round(plan.Fps * FfmpegArguments.KeyframeCeilingDefaultSeconds),
            Aralik(segment.Arguments));
    }
}
