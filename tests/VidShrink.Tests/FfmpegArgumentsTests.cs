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
        Assert.Contains("-g 60", part);
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
    [InlineData("libx265", "-x265-params", "psy-rd=2:psy-rdoq=1:aq-mode=2")]
    [InlineData("libsvtav1", "-svtav1-params", "tune=0:enable-variance-boost=1:variance-boost-strength=2")]
    public void Yazilim_psy_bayragi_yalniz_olculen_destekte_uretilir(string codec, string option, string value)
    {
        var plan = Plan(codec);
        var supported = new OptionAvailability((codec, option));
        var unsupported = new OptionAvailability();

        var enabled = FfmpegArguments.Build(Source(), plan, "out.mp4", 2, "log", supported);
        var disabled = FfmpegArguments.Build(Source(), plan, "out.mp4", 2, "log", unsupported);

        var index = enabled.IndexOf(option);
        Assert.True(index >= 0);
        Assert.Equal(value, enabled[index + 1]);
        Assert.DoesNotContain(option, disabled);
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

    [Theory]
    [InlineData("av1_nvenc", 64, 64, 1, 39)]
    [InlineData("av1_nvenc", 1280, 720, 60, 4200)]
    [InlineData("hevc_qsv", 3840, 2160, 120, 50000)]
    public void Donanim_tepe_carpani_boyut_guvencesi_tavanini_asmaz(string codec, int width, int height, double fps, int bitrateK)
    {
        var factor = FfmpegArguments.PeakRateFactor(codec, bitrateK, width, height, fps);

        Assert.InRange(factor, FfmpegArguments.TightPeakFactor, FfmpegArguments.HardwarePeakCeiling);
        Assert.True(factor <= 1.10, $"boyut-guvenli tepe asildi: {factor}");
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
}
