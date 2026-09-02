using System.Globalization;
using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class FfmpegArgumentsTests
{
    private sealed class OptionAvailability(params (string Codec, string Option)[] supported) : IEncoderAvailability, IEncoderOptionAvailability
    {
        public bool HasEncoder(string name) => true;
        public bool WorksAsEncoder(string codec) => true;
        public bool SupportsEncoderOption(string codec, string option, string value)
            => supported.Contains((codec, option));
    }
    private static MediaInfo Source() => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 40_000_000,
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 60,
        VideoCodec = "h264",
        TotalBitrateBps = 5_700_000
    };

    private static EncodePlan Plan(string codec = "libx264") => new()
    {
        Codec = codec,
        Mode = "2pass",
        VideoBitrateK = 1200,
        Width = 1280,
        Height = 720,
        Fps = 30,
        Preset = "slow",
        PixelFormat = "yuv420p",
        AudioCodec = "aac",
        AudioBitrateK = 128
    };

    /// <summary>
    /// Argumanlari bayrak-deger ciftlerine ayirir. Iki argumandan yalnizca birinde bulunan
    /// ciftler K4'un fark listesidir.
    /// </summary>
    private static List<string> Pairs(IReadOnlyList<string> args)
    {
        var pairs = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            if (!args[i].StartsWith('-')) { pairs.Add($"<konum> {args[i]}"); continue; }
            if (i + 1 < args.Count && !args[i + 1].StartsWith('-'))
            {
                pairs.Add($"{args[i]} {args[i + 1]}");
                i++;
            }
            else pairs.Add(args[i]);
        }
        return pairs;
    }

    private static string FlagOf(string pair) => pair.Split(' ')[0];

    [Fact]
    public void Ss_girdiden_once_gelir()
    {
        var args = FfmpegArguments.BuildSegment(Source(), Plan(), 12.5, 2.0, "ornek.mp4");

        var ss = args.IndexOf("-ss");
        var input = args.IndexOf("-i");

        Assert.True(ss >= 0, "-ss uretilmedi");
        Assert.True(input >= 0, "-i uretilmedi");
        Assert.True(ss < input, $"-ss {ss}. sirada, -i {input}. sirada");
        Assert.Equal("12.5", args[ss + 1]);
    }

    [Fact]
    public void Sure_girdiden_sonra_gelir()
    {
        var args = FfmpegArguments.BuildSegment(Source(), Plan(), 12.5, 1.75, "ornek.mp4");

        var input = args.IndexOf("-i");
        var t = args.IndexOf("-t");

        Assert.True(t > input, $"-t {t}. sirada, -i {input}. sirada");
        Assert.Equal("1.75", args[t + 1]);
    }

    [Fact]
    public void Parca_argumanlari_tam_kodlamadan_yalniz_uc_baslikta_ayrilir()
    {
        var info = Source();
        var plan = Plan();
        var segment = PreviewSegment.For(info, plan, 12.5, "ornek.mp4");

        var full = Pairs(FfmpegArguments.Build(info, plan, "cikti.mp4", 2, "gunluk"));
        var part = Pairs(segment.Arguments);

        var onlyFull = full.Except(part).ToList();
        var onlyPart = part.Except(full).ToList();

        var izinli = new[] { "-ss", "-t", "-b:v", "-maxrate", "-bufsize", "-crf", "-pass", "-passlogfile", "<konum>" };
        var disarida = onlyFull.Concat(onlyPart).Select(FlagOf).Distinct().Except(izinli).ToList();

        Assert.True(disarida.Count == 0, "izinsiz fark: " + string.Join(", ", disarida));

        var zincir = part.Single(p => FlagOf(p) == "-vf")["-vf ".Length..].Split(',');
        Assert.Contains("scale=1280:720:flags=lanczos", zincir);
        Assert.Equal("fps=30", zincir[^1]);
        Assert.True(Array.IndexOf(zincir, "scale=1280:720:flags=lanczos") < zincir.Length - 1,
            "olcekleme kare dusurmeden sonra geliyor: " + string.Join(',', zincir));
        Assert.DoesNotContain("-r", part.Select(FlagOf));
        Assert.Contains("-preset slow", part);
        Assert.Contains("-pix_fmt yuv420p", part);
        Assert.Contains("-c:a aac", part);
        Assert.Contains("-b:a 128k", part);
        Assert.Contains("-g 300", part);
        Assert.Contains("-keyint_min 30", part);
    }

    [Fact]
    public void Parca_argumanlari_iki_gecis_bayragi_tasimaz()
    {
        var info = Source();
        var segment = PreviewSegment.For(info, Plan(), 12.5, "ornek.mp4");

        Assert.DoesNotContain("-pass", segment.Arguments);
        Assert.DoesNotContain("-passlogfile", segment.Arguments);
        Assert.DoesNotContain("-b:v", segment.Arguments);
        Assert.Contains("-crf", segment.Arguments);
    }

    [Theory]
    [InlineData("libx265", "-x265-params", "psy-rd=2:psy-rdoq=1:aq-mode=2", "keyint=300:min-keyint=30:scenecut=40")]
    [InlineData("libsvtav1", "-svtav1-params", "tune=0:enable-variance-boost=1:variance-boost-strength=2", "keyint=300:scd=1")]
    public void Yazilim_psy_bayragi_yalniz_olculen_destekte_uretilir(string codec, string option, string value, string keyframeParams)
    {
        var plan = Plan(codec);
        var supported = new OptionAvailability((codec, option));
        var unsupported = new OptionAvailability();

        var enabled = FfmpegArguments.Build(Source(), plan, "out.mp4", 2, "log", supported);
        var disabled = FfmpegArguments.Build(Source(), plan, "out.mp4", 2, "log", unsupported);

        var index = enabled.IndexOf(option);
        Assert.True(index >= 0);
        Assert.Equal($"{keyframeParams}:{value}", enabled[index + 1]);
        Assert.Equal(keyframeParams, disabled[disabled.IndexOf(option) + 1]);
    }

    [Fact]
    public void Nvenc_aq_bayraklari_bagimsiz_olculur()
    {
        var plan = Plan("av1_nvenc");
        var onlySpatial = new OptionAvailability(("av1_nvenc", "-spatial-aq"));

        var args = FfmpegArguments.Build(Source(), plan, "out.mp4", 0, null, onlySpatial);

        Assert.Contains("-spatial-aq", args);
        Assert.DoesNotContain("-temporal-aq", args);
    }

    [Fact]
    public void Hdr_x265_psy_ve_renk_parametreleri_tek_dizgide_birlesir()
    {
        var plan = Plan("libx265");
        plan.HdrColorArgs = new List<string>
        {
            "-color_primaries", "bt2020", "-x265-params", "hdr10-opt=1:repeat-headers=1"
        };
        var availability = new OptionAvailability(("libx265", "-x265-params"));

        var args = FfmpegArguments.Build(Source(), plan, "out.mp4", 2, "log", availability);

        Assert.Equal(1, args.Count(a => a == "-x265-params"));
        var value = args[args.IndexOf("-x265-params") + 1];
        Assert.Contains("psy-rd=2:psy-rdoq=1:aq-mode=2", value);
        Assert.Contains("hdr10-opt=1:repeat-headers=1", value);
    }

    [Fact]
    public void Parca_tam_kodlamayla_ayni_psy_kabiliyetini_kullanir()
    {
        var info = Source();
        var plan = Plan("av1_nvenc");
        var availability = new OptionAvailability(("av1_nvenc", "-spatial-aq"), ("av1_nvenc", "-temporal-aq"));

        var full = FfmpegArguments.Build(info, plan, "out.mp4", 0, null, availability);
        var segment = FfmpegArguments.BuildSegment(info, plan, 1, 2, "part.mp4", availability);

        Assert.Contains("-spatial-aq", segment);
        Assert.Contains("-temporal-aq", segment);
        Assert.Equal(full.Contains("-spatial-aq"), segment.Contains("-spatial-aq"));
        Assert.Equal(full.Contains("-temporal-aq"), segment.Contains("-temporal-aq"));
    }

    [Fact]
    public void Arayuzde_gosterilen_komut_kosucunun_argumanlariyla_aynidir()
    {
        var info = Source();
        var plan = Plan("av1_nvenc");
        var availability = new OptionAvailability(("av1_nvenc", "-spatial-aq"), ("av1_nvenc", "-temporal-aq"));

        var displayed = VidShrink.App.MainWindow.DisplayedEncodeArguments(info, plan, "out.mp4", availability);
        var executed = FfmpegArguments.Build(info, plan, "out.mp4", 2, null, availability);

        Assert.Equal(executed, displayed);
        Assert.Contains("-spatial-aq", displayed);
        Assert.Contains("-temporal-aq", displayed);
        var windowSource = File.ReadAllText(TipSources.WindowCodePath);
        Assert.Contains("TxtCommand.Text = FfmpegArguments.ToCommandLine(DisplayedEncodeArguments", windowSource);
    }

    /// <summary>
    /// Tepe carpaninin taban orani basina verdigi deger. Beklenen sayilar kodun sabitlerinden
    /// degil elle yurutulen egriden geliyor: diz altinda 1,02, dizle en genis olculen oranin
    /// ortasinda (8,7x) 1,02 + 0,08/2 = 1,06, en genis olculen oranda ve ustunde 1,10.
    /// Onceki surum burada carpani <c>TightPeakFactor</c> ile <c>HardwarePeakCeiling</c>
    /// arasinda sayan bir aralik iddiasi tutuyordu; <c>PeakRateFactor</c> tam o iki sinira
    /// <c>Clamp</c>lendigi icin iddia tanim geregi dogruydu ve formul tumden bozulsa da
    /// yesil kaliyordu.
    /// </summary>
    [Theory]
    [InlineData("av1_nvenc", 64, 64, 1, 1.0, 1.02)]
    [InlineData("av1_nvenc", 1280, 720, 60, 2.92, 1.02)]
    [InlineData("av1_nvenc", 1280, 720, 60, 6.0, 1.02)]
    [InlineData("av1_nvenc", 1280, 720, 60, 8.7, 1.06)]
    [InlineData("av1_nvenc", 1280, 720, 60, 11.4, 1.10)]
    [InlineData("hevc_qsv", 3840, 2160, 120, 8.7, 1.06)]
    [InlineData("hevc_qsv", 3840, 2160, 120, 30.0, 1.10)]
    public void Donanim_tepe_carpani_taban_oraninda_beklenen_degeri_uretir(
        string codec, int width, int height, double fps, double floorRatio, double expected)
    {
        Assert.Equal(expected, PeakAtFloorRatio(codec, width, height, fps, floorRatio), 3);
    }

    [Fact]
    public void Yazilim_kodlayicisinda_tepe_carpani_genis_kalir()
    {
        Assert.Equal(1.5, FfmpegArguments.PeakRateFactor("libx264", 1200, 1280, 720, 30), 12);
        Assert.Equal(1.5, FfmpegArguments.PeakRateFactor("libx265", 9000, 3840, 2160, 60), 12);
    }

    /// <summary>
    /// Yoklamanin kac kez dogurulduğunu sayar. <c>WarmEncoderOption</c> uretimin surec
    /// doguran yolunun karsiligi; <c>SupportsEncoderOption</c> yalniz isitilmis sonucu okur.
    /// </summary>
    private sealed class WarmingAvailability : IEncoderAvailability, IEncoderOptionAvailability, IEncoderOptionWarmup
    {
        private readonly Dictionary<string, bool> _warmed = new(StringComparer.Ordinal);
        internal List<(string Codec, string Option)> Warmed { get; } = new();
        public bool HasEncoder(string name) => true;
        public bool WorksAsEncoder(string codec) => true;
        public bool SupportsEncoderOption(string codec, string option, string value)
            => _warmed.TryGetValue($"{codec}\0{option}\0{value}", out var cached) && cached;
        public bool WarmEncoderOption(string codec, string option, string value)
        {
            Warmed.Add((codec, option));
            _warmed[$"{codec}\0{option}\0{value}"] = true;
            return true;
        }
    }

    [Fact]
    public void Arguman_uretimi_kodlayici_yoklamasi_dogurmaz()
    {
        var recorder = new WarmingAvailability();
        var plan = Plan("av1_nvenc");

        FfmpegArguments.Build(Source(), plan, "out.mp4", 2, "log", recorder);
        FfmpegArguments.Build(Source(), plan, "out.mp4", 1, "log", recorder);
        FfmpegArguments.BuildSegment(Source(), plan, 1, 2, "part.mp4", recorder);

        Assert.Empty(recorder.Warmed);
    }

    [Fact]
    public void Isitilan_secenek_sonraki_arguman_uretiminde_onbellekten_okunur()
    {
        var recorder = new WarmingAvailability();
        var plan = Plan("av1_nvenc");

        var cold = FfmpegArguments.Build(Source(), plan, "out.mp4", 2, "log", recorder);
        Assert.DoesNotContain("-spatial-aq", cold);

        FfmpegArguments.PsychovisualArgs("av1_nvenc", recorder);
        Assert.Contains(("av1_nvenc", "-spatial-aq"), recorder.Warmed);

        var warmed = FfmpegArguments.Build(Source(), plan, "out.mp4", 2, "log", recorder);
        Assert.Contains("-spatial-aq", warmed);
        Assert.Contains("-temporal-aq", warmed);
    }

    private static MediaInfo PreviewSource() => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 40_000_000,
        DurationSeconds = 30,
        Width = 1920,
        Height = 1080,
        Fps = 60,
        VideoCodec = "h264",
        TotalBitrateBps = 5_700_000
    };

    [Fact]
    public void Onizleme_parcasi_verilen_psy_kabiliyetini_argumana_tasir()
    {
        var plan = Plan("av1_nvenc");
        var availability = new OptionAvailability(("av1_nvenc", "-spatial-aq"), ("av1_nvenc", "-temporal-aq"));

        var withFlags = PreviewSegment.For(PreviewSource(), plan, 1, "part.mp4", 2, availability: availability);
        var without = PreviewSegment.For(PreviewSource(), plan, 1, "part.mp4", 2);

        Assert.Contains("-spatial-aq", withFlags.Arguments);
        Assert.Contains("-temporal-aq", withFlags.Arguments);
        Assert.DoesNotContain("-spatial-aq", without.Arguments);
    }

    [Fact]
    public void Onizleme_kodlayicisi_kendi_kabiliyetini_parcaya_gecirir()
    {
        var plan = Plan("av1_nvenc");
        var availability = new OptionAvailability(("av1_nvenc", "-spatial-aq"), ("av1_nvenc", "-temporal-aq"));
        using var encoder = new VidShrink.App.Playback.SegmentEncoder(Path.GetTempPath())
        {
            Availability = availability
        };

        var segment = encoder.Describe(PreviewSource(), plan, 1, "part.mp4", null);

        Assert.Contains("-spatial-aq", segment.Arguments);
        Assert.Contains("-temporal-aq", segment.Arguments);
        var panelSource = File.ReadAllText(
            Path.Combine(TipSources.Root, "src", "VidShrink.App", "Playback", "PanelHost.cs"));
        Assert.Contains("_segments.Describe(info, plan, Math.Max(0, startSeconds), SignatureOutput, _profile)", panelSource);
    }

    [Fact]
    public void Yoklama_isinmasi_psy_secenegi_olan_her_kodlayiciyi_arka_planda_sorar()
    {
        var recorder = new WarmingAvailability();

        VidShrink.App.MainWindow.WarmPsychovisualProbe(recorder);

        Assert.Contains(("libx265", "-x265-params"), recorder.Warmed);
        Assert.Contains(("libsvtav1", "-svtav1-params"), recorder.Warmed);
        Assert.Contains(("av1_nvenc", "-spatial-aq"), recorder.Warmed);
        Assert.Contains(("av1_nvenc", "-temporal-aq"), recorder.Warmed);
        Assert.Contains(("hevc_nvenc", "-spatial-aq"), recorder.Warmed);
    }

    /// <summary>
    /// Kodlama yolu psy/AQ yoklamasini kendisi isitir. <c>FfmpegArguments.Build</c> saf
    /// oldugu icin isitilmamis bir secenegi desteklenmiyor sayiyor; isitmayan bir cagiran
    /// -- olcum araci <c>tools/VidShrink.Bench</c> <c>EncodeRunner</c> uzerinden geciyor --
    /// bayraklari sessizce kaybederdi. Kabiliyet burada hic isitilmamis veriliyor.
    /// </summary>
    [Theory]
    [InlineData("av1_nvenc", "-spatial-aq")]
    [InlineData("av1_nvenc", "-temporal-aq")]
    [InlineData("libx265", "-x265-params")]
    public void Kosucunun_arguman_uretimi_isitilmamis_kabiliyette_psy_bayragini_dusurmez(string codec, string flag)
    {
        var cold = new WarmingAvailability();

        var args = VidShrink.Ffmpeg.EncodeRunner.EncodeArguments(Source(), Plan(codec), "out.mp4", 2, "log", cold);

        Assert.Contains(flag, args);
        Assert.Contains((codec, flag), cold.Warmed);
    }

    /// <summary>
    /// Saf okuma yolu adiyla ayrildi: <c>CachedPsychovisualArgs</c> hicbir kosulda yoklama
    /// dogurmaz, isitmayi <c>WarmPsychovisual</c> ustlenir. Ayrim varsayilan parametreyle
    /// yapilsaydi yeni bir cagiran hicbir sey yazmadan surec dogururdu.
    /// </summary>
    [Fact]
    public void Saf_psy_yolu_kabiliyeti_isitmaz()
    {
        var cold = new WarmingAvailability();

        Assert.Empty(FfmpegArguments.CachedPsychovisualArgs("av1_nvenc", cold));
        Assert.Empty(FfmpegArguments.CachedPsychovisualArgs("libx265", cold));

        Assert.Empty(cold.Warmed);
    }

    /// <summary>
    /// Yoklamayi doguran tek nokta arka plandaki <c>Task.Run</c> olmali. Arayuzun her plan
    /// tazelemesinde kosturdugu <c>DisplayedEncodeArguments</c> yoklama doguruyorsa ilk cagri
    /// arayuz is parcacigini kodlayici basina yoklama suresi kadar bloklar. Olcu artik
    /// pencerenin kaynak metnine degil sahte kabiliyetin cagri sayacina bakiyor.
    /// </summary>
    [Fact]
    public void Arayuz_yolunda_kodlayici_yoklamasi_dogurulmaz()
    {
        var recorder = new WarmingAvailability();

        VidShrink.App.MainWindow.DisplayedEncodeArguments(Source(), Plan("av1_nvenc"), "out.mp4", recorder);
        VidShrink.App.MainWindow.DisplayedEncodeArguments(Source(), Plan("libx265"), "out.mp4", recorder);

        Assert.Empty(recorder.Warmed);

        var windowSource = File.ReadAllText(TipSources.WindowCodePath);
        Assert.Contains("var capabilities = EncoderCapabilities.Instance;\n                WarmPsychovisualProbe(capabilities);",
            windowSource.Replace("\r\n", "\n"));
        Assert.Contains("BuildUniqueOutputPath(_info.FilePath, \"shrunk\", \"mp4\"), _encoders));", windowSource);
    }

    /// <summary>
    /// ffmpeg <c>-x265-params</c>'ta son yazani kazandiriyor: ayri ayri uretilen psy, HDR renk
    /// ve kullanicinin <c>ExtraArgs</c>'i birbirini sessizce iptal ederdi. Ucu de tek
    /// birlestiriciden geciyor.
    /// </summary>
    [Fact]
    public void Psy_renk_ve_kullanici_x265_parametreleri_tek_dizgide_birlesir()
    {
        var plan = Plan("libx265");
        plan.HdrColorArgs = new List<string> { "-color_primaries", "bt2020", "-x265-params", "hdr10-opt=1" };
        plan.ExtraArgs = new List<string> { "-tag:v", "hvc1", "-x265-params", "keyint=48" };
        var availability = new OptionAvailability(("libx265", "-x265-params"));

        var args = FfmpegArguments.Build(Source(), plan, "out.mp4", 2, "log", availability);

        Assert.Equal(1, args.Count(a => a == "-x265-params"));
        var value = args[args.IndexOf("-x265-params") + 1];
        Assert.Contains("psy-rd=2:psy-rdoq=1:aq-mode=2", value);
        Assert.Contains("hdr10-opt=1", value);
        Assert.Contains("keyint=48", value);
        Assert.Contains("-color_primaries", args);
        Assert.Contains("-tag:v", args);
        Assert.Contains("hvc1", args);
    }

    [Fact]
    public void Ilk_gecis_de_tek_x265_dizgesi_uretir()
    {
        var plan = Plan("libx265");
        plan.HdrColorArgs = new List<string> { "-x265-params", "hdr10-opt=1" };
        plan.ExtraArgs = new List<string> { "-x265-params", "keyint=48" };
        var availability = new OptionAvailability(("libx265", "-x265-params"));

        var args = FfmpegArguments.Build(Source(), plan, "out.mp4", 1, "log", availability);

        Assert.Equal(1, args.Count(a => a == "-x265-params"));
        var value = args[args.IndexOf("-x265-params") + 1];
        Assert.Contains("hdr10-opt=1", value);
        Assert.Contains("keyint=48", value);
    }

    [Fact]
    public void Svtav1_parametreleri_de_tek_dizgide_birlesir()
    {
        var plan = Plan("libsvtav1");
        plan.ExtraArgs = new List<string> { "-svtav1-params", "fast-decode=1" };
        var availability = new OptionAvailability(("libsvtav1", "-svtav1-params"));

        var args = FfmpegArguments.Build(Source(), plan, "out.mp4", 0, null, availability);

        Assert.Equal(1, args.Count(a => a == "-svtav1-params"));
        var value = args[args.IndexOf("-svtav1-params") + 1];
        Assert.Contains("tune=0:enable-variance-boost=1:variance-boost-strength=2", value);
        Assert.Contains("fast-decode=1", value);
    }

    /// <summary>
    /// Verilen taban oranini planin gercekten tasidigi bit hizina cevirir; tepe carpani
    /// mutlak bit hizina degil bu orana bakiyor, olcum tablosu da oranla yazildi.
    /// </summary>
    private static double PeakAtFloorRatio(string codec, int width, int height, double fps, double floorRatio)
    {
        var floorK = CodecModel.MinBitrateK(codec, width, height, fps);
        Assert.True(floorK > 0, $"donanim tabani sifir: {codec} {width}x{height}@{fps}");
        var bitrateK = (int)Math.Round(floorK * floorRatio);
        return FfmpegArguments.PeakRateFactor(codec, bitrateK, width, height, fps);
    }

    public static TheoryData<string, int, int, double> OlculenYerlesimler() => new()
    {
        { "av1_nvenc", 1280, 720, 60 },
        { "av1_nvenc", 1920, 1080, 30 }
    };

    /// <summary>
    /// Tepe carpani taban orani boyunca geri gitmez. Olcum bunu soyluyor: dar tepe
    /// tabanin 2,6-5,6 kati arasinda istenen boyuta oturuyor, 11,4 katinda %2,7 eksik
    /// birakiyor. Eksik teslim yukselen oranla buyudugune gore acilma da tek yonlu.
    /// Iddia <c>Clamp</c> sinirlarina degil ardisik iki cikti arasindaki iliskiye bakiyor.
    /// </summary>
    [Theory]
    [MemberData(nameof(OlculenYerlesimler))]
    public void Tepe_carpani_taban_orani_boyunca_geri_gitmez(string codec, int width, int height, double fps)
    {
        var previous = PeakAtFloorRatio(codec, width, height, fps, 0.5);
        for (var ratio = 0.75; ratio <= 30.0; ratio += 0.25)
        {
            var factor = PeakAtFloorRatio(codec, width, height, fps, ratio);
            Assert.True(factor >= previous,
                $"tepe carpani {ratio:0.00}x tabanda geri gitti: {previous} -> {factor}");
            previous = factor;
        }
    }

    /// <summary>
    /// Egrinin sekli: dizden once duz, diz ile en genis olculen oran arasinda kesin
    /// artan, olculen en yuksek oranin ustunde doymus. Ucu de sabit karsilastirmiyor,
    /// uretimin iki ciktisini birbiriyle karsilastiriyor.
    /// </summary>
    [Theory]
    [MemberData(nameof(OlculenYerlesimler))]
    public void Tepe_egrisi_dizden_once_duz_dizden_sonra_artan_olcum_disinda_doymus(string codec, int width, int height, double fps)
    {
        double At(double ratio) => PeakAtFloorRatio(codec, width, height, fps, ratio);

        Assert.Equal(At(1.0), At(2.92), 12);
        Assert.Equal(At(2.92), At(5.3), 12);

        Assert.True(At(5.3) < At(7.80), $"diz sonrasi artis yok: {At(5.3)} -> {At(7.80)}");
        Assert.True(At(7.80) < At(11.90), $"diz sonrasi artis yok: {At(7.80)} -> {At(11.90)}");

        Assert.Equal(At(11.90), At(12.5), 12);
        Assert.Equal(At(11.90), At(30.0), 12);
    }

    /// <summary>
    /// Boyut guvencesinin olculmus siniri. Sayilar <c>docs/olcumler/tepe-tavani-ve-psy.md</c>
    /// ve <c>FfmpegArguments</c> yorum blogundaki bench kosumlarindan geliyor, koddaki
    /// sabitlerden degil:
    /// 882x496@60 5,3x tabanda 1,02 tepesi 1,007 teslim etti — orada tepe acilamaz;
    /// ayni yerlesimde 11,4x tabanda 1,50 tepesi 1,056 ile hedefi asti, 1,10 ise 1,008'de
    /// kaldi — orada tavan olculen 1,10.
    /// </summary>
    [Theory]
    [MemberData(nameof(OlculenYerlesimler))]
    public void Tepe_carpani_olculen_guvenli_degerlerin_disina_cikmaz(string codec, int width, int height, double fps)
    {
        Assert.True(PeakAtFloorRatio(codec, width, height, fps, 5.3) <= 1.02,
            $"5,3x tabanda tepe acildi: {PeakAtFloorRatio(codec, width, height, fps, 5.3)}");
        Assert.True(PeakAtFloorRatio(codec, width, height, fps, 11.4) <= 1.10,
            $"11,4x tabanda olculen guvenli tepe asildi: {PeakAtFloorRatio(codec, width, height, fps, 11.4)}");

        for (var ratio = 11.4; ratio <= 200.0; ratio += 7.3)
            Assert.True(PeakAtFloorRatio(codec, width, height, fps, ratio) < 1.50,
                $"{ratio:0.0}x tabanda tepe olculen asma degerine ({1.50}) ulasti");
    }

    /// <summary>
    /// Esit uzunlukta <paramref name="sceneCount"/> sahneden olusan bir harita. Esit bolmede
    /// ortalama ile medyan ayni oldugu icin bu yardimci iki kurali ayirt etmez; ayirt etmesi
    /// gereken olculer <see cref="CarpikHarita"/> kullanir.
    /// </summary>
    private static SceneMap Harita(double durationSeconds, int sceneCount)
    {
        var scenes = new List<Scene>(sceneCount);
        var step = durationSeconds / sceneCount;
        for (var i = 0; i < sceneCount; i++)
            scenes.Add(new Scene
            {
                Index = i,
                Start = i * step,
                End = (i + 1) * step,
                Bits = 1_000_000,
                Complexity = 1.0
            });
        return new SceneMap { Threshold = SceneMap.DefaultThreshold, Duration = durationSeconds, Scenes = scenes };
    }

    /// <summary>
    /// Verilen uzunluklardan bir harita kurar. Sagi carpik dagilimlar burada uretiliyor:
    /// T105'in olctugu tam kaynak dagiliminda ortalama 13,46 sn iken medyan 5,62 sn.
    /// </summary>
    private static SceneMap CarpikHarita(params double[] lengths)
    {
        var scenes = new List<Scene>(lengths.Length);
        var t = 0.0;
        for (var i = 0; i < lengths.Length; i++)
        {
            scenes.Add(new Scene
            {
                Index = i,
                Start = t,
                End = t + lengths[i],
                Bits = 1_000_000,
                Complexity = 1.0
            });
            t += lengths[i];
        }
        return new SceneMap { Threshold = SceneMap.DefaultThreshold, Duration = t, Scenes = scenes };
    }

    private static double TavanSaniye(string codec, double fps, SceneMap? scenes)
        => FfmpegArguments.KeyframeInterval(codec, fps, scenes).MaxFrames / fps;

    private static double TabanSaniye(string codec, double fps, SceneMap? scenes)
        => FfmpegArguments.KeyframeInterval(codec, fps, scenes).MinFrames / fps;

    /// <summary>
    /// Aralik tek sayi degil: alt sinir bir saniye, ust sinir alt sinirdan kat kat buyuk
    /// ve ikisi de argumana ayri ayri yaziliyor. Eski sabit <c>-g fps*2</c> yolunda ust
    /// sinir alt sinirin iki kati ve ayri bir alt sinir hic yoktu.
    /// </summary>
    [Theory]
    [InlineData("libx264", 30)]
    [InlineData("libx264", 60)]
    [InlineData("libvpx-vp9", 24)]
    public void Anahtar_kare_araligi_alt_ve_ust_siniri_ayri_yazar(string codec, double fps)
    {
        var args = FfmpegArguments.KeyframeArgs(codec, fps);
        var g = double.Parse(args[args.ToList().IndexOf("-g") + 1], CultureInfo.InvariantCulture);
        var min = double.Parse(args[args.ToList().IndexOf("-keyint_min") + 1], CultureInfo.InvariantCulture);

        Assert.Equal(1.0, min / fps, 2);
        Assert.True(g / fps >= 5.0,
            $"ust sinir olculen en kisa degerin (5 sn) altinda: {g / fps:0.##} sn");
        Assert.True(g >= 4 * min, $"aralik degil tek sayi gibi davraniyor: {min}..{g}");
    }

    /// <summary>
    /// Harita yoksa HandBrake araligina dusuluyor: 1 sn alt sinir, 10 sn ust sinir
    /// (encx264.c:386-391, encx265.c:188-190). Davranis bozulmuyor, sabit 2 sn'ye de
    /// geri donulmuyor.
    /// </summary>
    [Theory]
    [InlineData(24)]
    [InlineData(30)]
    [InlineData(59.94)]
    public void Harita_yokken_HandBrake_araligina_dusulur(double fps)
    {
        Assert.Equal(1.0, TabanSaniye("libx264", fps, null), 2);
        Assert.Equal(10.0, TavanSaniye("libx264", fps, null), 1);
    }

    /// <summary>
    /// Ust sinir haritadan turuyor: kisa sahneli icerikte kisaliyor, uzun sahneli
    /// icerikte uzuyor. Iddia iki sabiti degil uretimin iki ciktisini karsilastiriyor.
    /// </summary>
    [Fact]
    public void Ust_sinir_sahne_uzunluguyla_birlikte_uzar()
    {
        var previous = 0.0;
        foreach (var sceneCount in new[] { 40, 20, 10, 6, 4, 2, 1 })
        {
            var ceiling = TavanSaniye("libx264", 60, Harita(60.0, sceneCount));
            Assert.True(ceiling >= previous,
                $"{sceneCount} sahnede ust sinir geri gitti: {previous:0.###} -> {ceiling:0.###}");
            previous = ceiling;
        }

        Assert.True(TavanSaniye("libx264", 60, Harita(60.0, 20)) < TavanSaniye("libx264", 60, Harita(60.0, 2)),
            "kisa ve uzun sahneli icerik ayni ust siniri aliyor");
    }

    /// <summary>
    /// Ust sinir ortalamayi degil **medyani** okuyor. Iddia sagi carpik bir haritayla
    /// kuruluyor: bes sahnenin dordu 6 sn, biri 60 sn; ortalama 16,8 sn (kiskacin ustune
    /// tasar, tavana yapisir), medyan 6 sn (kiskacin icinde). Ortalama okunsaydi ust sinir
    /// 10 sn cikardi. T105 tam kaynakta ayni carpikligi olctu: ortalama 13,46 sn, medyan
    /// 5,62 sn. Ayni izgarada olculdugunde medyan kurali kalitede geride kalmadi ve %24
    /// daha az atlama bedeli getirdi.
    /// </summary>
    [Fact]
    public void Ust_sinir_ortalamayi_degil_medyani_okur()
    {
        var carpik = CarpikHarita(6.0, 6.0, 6.0, 6.0, 60.0);
        var ortalama = carpik.Duration / carpik.Scenes.Count;

        Assert.True(ortalama > 10.0, $"harita carpik degil, olcu bir sey ayirt etmiyor: {ortalama:0.##} sn");
        Assert.Equal(6.0, TavanSaniye("libx264", 60, carpik), 2);
    }

    /// <summary>
    /// Bolen bir ayar sabiti degil, haritanin **olculen geri cagirmasi**, ve iki sayisi da
    /// ayni birimden: yer gercegi penceresindeki (144,117-333,300] kesim sayilari. Olculen
    /// esikte harita 28 gercek kesimin 28'ini buluyor, yanlis pozitif yok; geri cagirma
    /// 1,000, bolen 1,0 ve duzeltme borcu yok. Eski 0,2 esiginde ayni pencere 28'de 10
    /// veriyordu ve bolen 2,8'di. Bu olcu boleni ciktidan siniyor: sayilardan biri oynarsa
    /// medyani kiskacin icinde olan bir harita baska ust sinir uretir.
    /// </summary>
    [Fact]
    public void Bolen_olculen_geri_cagirmadan_geliyor()
    {
        Assert.Equal(8.0, TavanSaniye("libx264", 60, CarpikHarita(3.0, 8.0, 8.0, 8.0, 40.0)), 2);
    }

    private static int VbvTavani(string codec, int bitrateK, string bayrak)
    {
        var plan = Plan(codec);
        plan.Mode = "crf";
        plan.Crf = 23;
        plan.VideoBitrateK = bitrateK;
        var args = FfmpegArguments.Build(Source(), plan, "cikti.mp4", 0, null).ToList();
        var at = args.IndexOf(bayrak);
        return at < 0 ? -1 : int.Parse(args[at + 1].TrimEnd('k'), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// CRF yolunda yazilim kodlayicisi VBV tavanini tasiyor ve tavan bit hiziyla olcekleniyor.
    /// Bu sabit degil bir kosul: T98'de kaldirildiginda olculen sonuc kalite +0,310 mean /
    /// +0,599 p10 ama ayni CRF'te dosya %3,9 buyuk. CRF bu projede hedefe inen bir kip
    /// oldugu icin tavan kaldi. Iddia iki bit hizinin uretimini birbiriyle karsilastiriyor.
    /// </summary>
    [Theory]
    [InlineData("libx264")]
    [InlineData("libx265")]
    public void Crf_yolunda_VBV_tavani_bit_hiziyla_olcekleniyor(string codec)
    {
        var dusuk = VbvTavani(codec, 2000, "-maxrate");
        var yuksek = VbvTavani(codec, 4000, "-maxrate");

        Assert.True(dusuk > 2000, $"{codec} CRF yolunda VBV tavani yok ya da ortalamanin altinda: {dusuk}");
        Assert.Equal(2 * dusuk, yuksek);
        Assert.Equal(2 * VbvTavani(codec, 2000, "-bufsize"), VbvTavani(codec, 4000, "-bufsize"));
        Assert.True(VbvTavani(codec, 2000, "-bufsize") > dusuk,
            "arabellek tavandan kucuk; VBV penceresi tepeyi tasimaz");
    }

    /// <summary>
    /// SVT-AV1 hiz siniri tasimiyor; kosul kaldirilirsa bu ölçü kizarir.
    /// </summary>
    [Fact]
    public void Crf_yolunda_hiz_siniri_desteklemeyen_kodlayici_VBV_almaz()
    {
        Assert.Equal(-1, VbvTavani("libsvtav1", 4000, "-maxrate"));
        Assert.Equal(-1, VbvTavani("libsvtav1", 4000, "-bufsize"));
    }

    /// <summary>
    /// Geri cagirma tek bir esige aittir. T105 <c>SceneMap.DefaultThreshold</c>'u 0,2'den
    /// 0,105'e tasiyinca bu olcu kizardi ve geri cagirma yeniden olculdu: 28/10 yerine
    /// 28/28. Esik yine oynarsa harita yine baska bolusur ve eski bolen ust siniri ikinci
    /// kez kisaltir; bu olcu tam o an kizarir, yeniden olculmeden gecilemez.
    /// </summary>
    [Fact]
    public void Az_bolme_duzeltmesi_olculdugu_esikte_kalir()
    {
        Assert.Equal(FfmpegArguments.SceneMapThresholdOfRecord, SceneMap.DefaultThreshold);
    }

    /// <summary>
    /// Olculen esikte harita yer gercegiyle ayni bolusu veriyor: 189,183 sn'lik pencerede
    /// 28 gercek kesim, yani 29 gercek cekim; harita da 28 kesim bildiriyor. Bu bolusten
    /// turetilen ust sinir, gercek cekim uzunlugunun kendisi olmali (6,52 sn) - bolen
    /// oynarsa olmaz.
    /// </summary>
    [Fact]
    public void Duzeltme_olculen_pencerede_gercek_cekim_uzunlugunu_uretir()
    {
        const double pencere = 189.183;
        var gercekCekim = pencere / (FfmpegArguments.SceneMapGroundTruthCutsInWindow + 1);

        Assert.Equal(6.52, gercekCekim, 2);
        Assert.Equal(gercekCekim, FfmpegArguments.KeyframeCeilingSeconds(Harita(pencere, 29)), 2);
    }

    /// <summary>
    /// Kiskacin alt ucu olculen 5 saniyede bagliyor. Iddia sabiti kendi modulunden okumuyor:
    /// medyani 2 sn olan bir harita 60 fps'te <c>-g 300</c> uretmeli. Alt uc 1 sn'ye
    /// indirilirse 120 gelir ve bu olcu kizarir. Deger keyfi degil - ust sinir supurmesinde
    /// 2 sn ile 20 sn arasindaki p10 kazancinin %87'si 5 saniyede zaten alinmisti.
    /// </summary>
    [Fact]
    public void Kiskacin_alt_ucu_bes_saniyede_bagliyor()
    {
        var args = FfmpegArguments.KeyframeArgs("libx264", 60, CarpikHarita(2.0, 2.0, 2.0)).ToList();

        Assert.Equal("300", args[args.IndexOf("-g") + 1]);
        Assert.Equal(5.0, FfmpegArguments.KeyframeCeilingMinSeconds);
    }

    /// <summary>
    /// Kiskacin ust ucu olculen 10 saniyede bagliyor: medyani 30 sn olan harita 60 fps'te
    /// <c>-g 600</c> uretmeli, 1800 degil. Deger HandBrake'in <c>keyint = 10*fps</c>'i ile
    /// ayni ve supurmede 10 sn ile 20 sn ayni uc I-kareyi ayni yerlere koymustu - ustunde
    /// ust sinir artik baglamiyor.
    /// </summary>
    [Fact]
    public void Kiskacin_ust_ucu_on_saniyede_bagliyor()
    {
        var args = FfmpegArguments.KeyframeArgs("libx264", 60, CarpikHarita(30.0, 30.0, 30.0)).ToList();

        Assert.Equal("600", args[args.IndexOf("-g") + 1]);
        Assert.Equal(10.0, FfmpegArguments.KeyframeCeilingMaxSeconds);
    }

    /// <summary>
    /// Donanim ust siniri olculen 5 saniyede sabit ve haritadan bagimsiz: uzun sahneli
    /// harita bile 60 fps'te <c>-g 300</c> almali. Donanimda sahne kesimi olmadigi icin
    /// ust sinir gerceklesen araligin kendisi, yani dogrudan atlama butcesi; 2 sn'ye
    /// indirilirse 120 gelir ve bu olcu kizarir.
    /// </summary>
    [Fact]
    public void Donanim_ust_siniri_bes_saniyede_sabit()
    {
        var args = FfmpegArguments.KeyframeArgs("av1_nvenc", 60, CarpikHarita(30.0, 30.0, 30.0)).ToList();

        Assert.Equal("300", args[args.IndexOf("-g") + 1]);
        Assert.Equal(5.0, FfmpegArguments.HardwareKeyframeCeilingSeconds);
    }

    /// <summary>
    /// Donanimda sahne kesimi yok (ffmpeg <c>-h encoder=hevc_nvenc</c>: <c>-no-scenecut</c>
    /// yalniz lookahead acikken is goruyor, bu proje lookahead acmiyor). Orada ust sinir
    /// gerceklesen araligin kendisi oldugu icin harita ust siniri oynatmaz ve deger
    /// yazilim yolundaki varsayilandan kisa kalir.
    /// </summary>
    [Theory]
    [InlineData("av1_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("h264_qsv")]
    public void Donanimda_ust_sinir_haritadan_etkilenmez(string codec)
    {
        var kisa = FfmpegArguments.KeyframeInterval(codec, 60, Harita(60.0, 30));
        var uzun = FfmpegArguments.KeyframeInterval(codec, 60, Harita(60.0, 1));

        Assert.Equal(kisa.MaxFrames, uzun.MaxFrames);
        Assert.False(kisa.FromSceneMap);
        Assert.True(kisa.MaxFrames / 60.0 < TavanSaniye("libx264", 60, null),
            "donanim ust siniri yazilim varsayilanindan kisa degil");
    }

    /// <summary>
    /// Yerlesim karari kodlayiciya birakiliyor: her yazilim kodlayicisinda sahne kesimi
    /// acik yaziliyor ve aralik kodlayicinin kendi diliyle veriliyor.
    /// </summary>
    [Fact]
    public void Sahne_kesimi_kodlayicinin_kendi_diliyle_acik_yazilir()
    {
        var x265 = FfmpegArguments.KeyframeArgs("libx265", 30);
        var x265Params = x265[x265.ToList().IndexOf("-x265-params") + 1];
        Assert.Contains("keyint=300", x265Params);
        Assert.Contains("min-keyint=30", x265Params);
        Assert.Contains("scenecut=40", x265Params);

        var svt = FfmpegArguments.KeyframeArgs("libsvtav1", 30);
        var svtParams = svt[svt.ToList().IndexOf("-svtav1-params") + 1];
        Assert.Contains("keyint=300", svtParams);
        Assert.Contains("scd=1", svtParams);
    }

    /// <summary>
    /// Anahtar kare parametreleri de psy/HDR gibi tek dizgede birlesiyor; yoksa ffmpeg
    /// son yazani alir ve ikisinden biri sessizce duser.
    /// </summary>
    [Fact]
    public void Anahtar_kare_ve_psy_parametreleri_tek_x265_dizgesinde_birlesir()
    {
        var plan = Plan("libx265");
        plan.Fps = 30;
        var args = FfmpegArguments.Build(Source(), plan, "cikti.mp4", 0, null,
            new OptionAvailability(("libx265", "-x265-params")));

        Assert.Single(args, a => a == "-x265-params");
        var value = args[args.ToList().IndexOf("-x265-params") + 1];
        Assert.Contains("keyint=300", value);
        Assert.Contains("min-keyint=30", value);
        Assert.Contains("psy-rd=2", value);
    }

    /// <summary>
    /// Aralik degisikligi hiz denetimine dokunmuyor: iki farkli aralikla uretilen iki
    /// komut yalniz anahtar kare bayraklarinda ayrisiyor, <c>-b:v</c> / <c>-maxrate</c> /
    /// <c>-bufsize</c> ikisinde de ayni. Boyut guvencesinin ilk savunmasi bu.
    /// </summary>
    [Fact]
    public void Aralik_degisikligi_hiz_denetimi_bayraklarina_dokunmaz()
    {
        var plan = Plan();
        var haritasiz = FfmpegArguments.Build(Source(), plan, "cikti.mp4", 2, "log");
        var haritali = FfmpegArguments.Build(Source(), plan, "cikti.mp4", 2, "log", null, Harita(60.0, 20));

        var fark = Pairs(haritasiz).Except(Pairs(haritali))
            .Concat(Pairs(haritali).Except(Pairs(haritasiz)))
            .Select(FlagOf)
            .Distinct()
            .ToList();

        Assert.All(fark, flag => Assert.Contains(flag, new[] { "-g", "-keyint_min" }));
        Assert.Contains("-g", fark);
    }

    private static async Task<int> RunAsync(string exe, params string[] a)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = exe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var x in a) psi.ArgumentList.Add(x);
        using var p = new System.Diagnostics.Process { StartInfo = psi };
        p.Start();
        var so = p.StandardOutput.ReadToEndAsync();
        var se = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        await so;
        await se;
        return p.ExitCode;
    }

    private static async Task<int> AnahtarKareSayisiAsync(string path)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = VidShrink.Ffmpeg.ToolLocator.Ffprobe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var x in new[] { "-v", "error", "-select_streams", "v:0", "-skip_frame", "nokey",
                                  "-show_entries", "frame=pts_time", "-of", "csv=p=0", path })
            psi.ArgumentList.Add(x);
        using var p = new System.Diagnostics.Process { StartInfo = psi };
        p.Start();
        var so = p.StandardOutput.ReadToEndAsync();
        var se = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        await se;
        return (await so).Split('\n').Count(l => l.Trim().Length > 0);
    }

    /// <summary>
    /// Kesimsiz bir kaynakta ust sinir gerceklesen aralik oluyor: uzun ust sinir kesinlikle
    /// daha az I-kare uretiyor. Arama gecikmesinin altindaki mekanizma bu; gecikmenin
    /// kendisi <c>docs/olcumler/tepe-tavani-ve-psy.md</c>'de olculdu.
    /// </summary>
    [FfmpegFact]
    public async Task Uzun_ust_sinir_kesimsiz_kaynakta_daha_az_anahtar_kare_uretir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vidshrink-t98-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var kisa = Path.Combine(dir, "kisa.mp4");
            var uzun = Path.Combine(dir, "uzun.mp4");

            async Task Kodla(string cikti, IReadOnlyList<string> keyframeArgs)
            {
                var a = new List<string> { "-hide_banner", "-loglevel", "error", "-y",
                    "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=30:duration=20",
                    "-c:v", "libx264", "-preset", "fast", "-crf", "28" };
                a.AddRange(keyframeArgs);
                a.AddRange(new[] { "-pix_fmt", "yuv420p", cikti });
                Assert.Equal(0, await RunAsync(VidShrink.Ffmpeg.ToolLocator.Ffmpeg, a.ToArray()));
            }

            await Kodla(kisa, FfmpegArguments.KeyframeArgs("libx264", 30, Harita(20.0, 20)));
            await Kodla(uzun, FfmpegArguments.KeyframeArgs("libx264", 30, null));

            var kisaSayi = await AnahtarKareSayisiAsync(kisa);
            var uzunSayi = await AnahtarKareSayisiAsync(uzun);

            Assert.True(kisaSayi > uzunSayi,
                $"kisa ust sinir daha cok I-kare uretmedi: kisa={kisaSayi} uzun={uzunSayi}");
            Assert.True(uzunSayi <= 3, $"10 sn ust sinirda 20 sn'lik kesimsiz kaynakta {uzunSayi} I-kare var");
        }
        finally
        {
            foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            Directory.Delete(dir);
        }
    }

    /// <summary>
    /// Yerlesim gercekten kodlayicida: 2,5 saniyede bir sert kesim tasiyan 20 sn'lik kaynak,
    /// uretilen aralikla kodlandiginda ust sinirin izin verdiginden fazla I-kare aliyor.
    /// Iddia sabitle degil, ayni argumanlarin <c>scenecut=0</c>'li ikizinden gelen sayiyla
    /// karsilastiriliyor; aralik sahne kesimini kapatirsa iki sayi esitlenir ve olcu kizarir.
    /// </summary>
    [FfmpegFact]
    public async Task Sahne_kesimi_ust_sinirin_izin_verdiginden_cok_I_kare_yerlestirir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vidshrink-t98-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            async Task<int> Kodla(string cikti, string? kapali)
            {
                var a = new List<string> { "-hide_banner", "-loglevel", "error", "-y",
                    "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=30:duration=2.5",
                    "-f", "lavfi", "-i", "smptebars=size=320x240:rate=30:duration=2.5",
                    "-filter_complex", "[0:v][1:v][0:v][1:v][0:v][1:v][0:v][1:v]concat=n=8:v=1[v]",
                    "-map", "[v]", "-c:v", "libx264", "-preset", "fast", "-crf", "28" };
                a.AddRange(FfmpegArguments.KeyframeArgs("libx264", 30));
                if (kapali is not null) a.AddRange(new[] { "-x264-params", kapali });
                a.AddRange(new[] { "-pix_fmt", "yuv420p", Path.Combine(dir, cikti) });
                Assert.Equal(0, await RunAsync(VidShrink.Ffmpeg.ToolLocator.Ffmpeg, a.ToArray()));
                return await AnahtarKareSayisiAsync(Path.Combine(dir, cikti));
            }

            var acik = await Kodla("acik.mp4", null);
            var kapali = await Kodla("kapali.mp4", "scenecut=0");

            Assert.True(acik > kapali,
                $"sahne kesimi acikken fazladan I-kare gelmedi: acik={acik} kapali={kapali}");
            Assert.True(acik > 20.0 / FfmpegArguments.KeyframeCeilingDefaultSeconds + 1,
                $"20 sn'lik 8 kesimli kaynakta yalniz {acik} I-kare var; yerlesim ust sinirdan geliyor");
        }
        finally
        {
            foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            Directory.Delete(dir);
        }
    }
}
