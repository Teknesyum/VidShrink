using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// HDR10+ koprusu (<c>docs/handbrake/fable-karar-hdr10plus-2026-09-23.md</c>): kaynagin
/// SMPTE 2094-40 verisi ffprobe ile cozulur, x265'in <c>dhdr10-info</c> JSON'una cevrilir ve
/// iki gecise de verilir. Canli kol 12 karelik 320x180 kaynagi kendisi uretir, <c>-threads 2</c>
/// ile kodlar; kanit <c>.calisma/hdr10plus-kopru/</c>, olcu kendi dosyalarini siler.
/// </summary>
public sealed class Hdr10ArtiKopruTests : IDisposable
{
    private readonly ITestOutputHelper _cikti;

    public Hdr10ArtiKopruTests(ITestOutputHelper cikti)
    {
        _cikti = cikti;
        Strings.Reset();
    }

    public void Dispose() => Strings.Reset();

    private sealed class Kodlayicilar : IEncoderAvailability, IHdr10EncoderAvailability
    {
        private readonly HashSet<string> _adlar;
        public Kodlayicilar(params string[] adlar) => _adlar = new HashSet<string>(adlar, StringComparer.OrdinalIgnoreCase);
        public bool HasEncoder(string name) => _adlar.Contains(name);
        public bool WorksAsEncoder(string codec) => _adlar.Contains(codec);
        public string? Hdr10PixelFormat(string codec) => _adlar.Contains(codec) && CodecModel.IsHardware(codec) ? "p010le" : null;
    }

    private static readonly Kodlayicilar Hepsi = new("libx264", "libx265", "libsvtav1", "hevc_nvenc");

    private static MediaInfo Hdr10Arti() => new()
    {
        FilePath = "kaynak.mkv",
        FileSizeBytes = 500L * 1024 * 1024,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 24,
        VideoCodec = "hevc",
        TotalBitrateBps = 35_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p10le",
        BitDepth = 10,
        ColorPrimaries = "bt2020",
        ColorTransfer = "smpte2084",
        ColorSpace = "bt2020nc",
        IsHdr = true,
        HasHdr10Plus = true
    };

    private static PlanResult Planla(MediaInfo info, string? kilit = null, HdrPolicy politika = HdrPolicy.Preserve,
        TrimWindow? kesit = null, VideoFilterOptions? suzgec = null, IEncoderAvailability? kodlayicilar = null,
        CodecPreference tercih = CodecPreference.Auto, SpeedMode hiz = SpeedMode.Quality)
        => PlanCalculator.BuildDetailed(info, new PlanOptions
        {
            TargetMb = 40,
            Codec = tercih,
            SpeedMode = hiz,
            Intent = Intent.Sharing,
            LockedCodec = kilit,
            HdrPolicy = politika,
            Trim = kesit,
            Filters = suzgec ?? VideoFilterOptions.Default
        }, null, kodlayicilar ?? Hepsi);

    private static bool Var(PlanResult sonuc, ReasonCode kod) => sonuc.Plan.ReasonCodes.Any(n => n.Code == kod);

    /// <summary>ffprobe 9'un <c>json=compact=1</c> kare satiri; alan sirasi ve yinelenen anahtarlar olculdugu gibi.</summary>
    private static string Kare(int pts, int i, bool hdr10Arti = true, string ek = "")
    {
        var sb = new StringBuilder();
        sb.Append("        { \"key_frame\": ").Append(i == 0 ? 1 : 0).Append(", \"pts\": ").Append(pts).Append(",\n");
        sb.Append("            \"side_data_list\": [\n");
        sb.Append("                { \"side_data_type\": \"H.26[45] User Data Unregistered SEI message\" },\n");
        sb.Append("                { \"side_data_type\": \"Mastering display metadata\", \"red_x\": \"34000/50000\", \"min_luminance\": \"1/10000\", \"max_luminance\": \"10000000/10000\" },\n");
        if (!hdr10Arti)
        {
            sb.Append("                { \"side_data_type\": \"Content light level metadata\", \"max_content\": 1000, \"max_average\": 400 }\n");
            sb.Append("            ] }");
            return sb.ToString();
        }
        sb.Append("                { \"side_data_type\": \"Content light level metadata\", \"max_content\": 1000, \"max_average\": 400 },\n");
        sb.Append("                { \"side_data_type\": \"HDR Dynamic Metadata SMPTE2094-40 (HDR10+)\", \"application version\": 1, \"num_windows\": 1, ");
        sb.Append($"\"targeted_system_display_maximum_luminance\": \"{400 + i}/1\", ");
        sb.Append($"\"maxscl\": \"{4000 + i}/100000\", \"maxscl\": \"3500/100000\", \"maxscl\": \"3000/100000\", ");
        sb.Append($"\"average_maxrgb\": \"{1000 + i * 7}/100000\", \"num_distribution_maxrgb_percentiles\": 9, ");
        int[] yuzde = [1, 5, 10, 25, 50, 75, 90, 95, 99];
        int[] deger = [0, 10, 50, 200, 500, 1000, 2000, 3000, 4000 + i];
        for (var k = 0; k < 9; k++)
            sb.Append($"\"distribution_maxrgb_percentage\": {yuzde[k]}, \"distribution_maxrgb_percentile\": \"{deger[k]}/100000\", ");
        sb.Append("\"fraction_bright_pixels\": \"0/1000\", \"knee_point_x\": \"0/4095\", \"knee_point_y\": \"0/4095\", \"num_bezier_curve_anchors\": 9, ");
        sb.Append(string.Join(", ", new[] { 102, 205, 307, 410, 512, 614, 717, 819, 922 }.Select(a => $"\"bezier_curve_anchors\": \"{a}/1023\"")));
        sb.Append(ek).Append(" }\n");
        sb.Append("            ] }");
        return sb.ToString();
    }

    private static string Dokum(params string[] kareler)
        => "{\n    \"frames\": [\n" + string.Join(",\n", kareler) + "\n    ]\n}\n";

    [Fact]
    public void DonusturucuAlanDegerleriniKareKareTasir()
    {
        var sonuc = Hdr10PlusJson.FromFfprobe(Dokum(Kare(0, 0), Kare(42, 1), Kare(83, 2)));

        Assert.Equal(3, sonuc.Frames);
        Assert.Equal(3, sonuc.Hdr10PlusFrames);
        Assert.NotNull(sonuc.Json);
        using var doc = JsonDocument.Parse(sonuc.Json!);
        Assert.Equal("B", doc.RootElement.GetProperty("JSONInfo").GetProperty("HDR10plusProfile").GetString());
        var sahneler = doc.RootElement.GetProperty("SceneInfo").EnumerateArray().ToList();
        Assert.Equal(3, sahneler.Count);
        for (var i = 0; i < 3; i++)
        {
            var s = sahneler[i];
            var parlaklik = s.GetProperty("LuminanceParameters");
            Assert.Equal(1000 + i * 7, parlaklik.GetProperty("AverageRGB").GetInt32());
            Assert.Equal(new[] { 4000 + i, 3500, 3000 }, parlaklik.GetProperty("MaxScl").EnumerateArray().Select(e => e.GetInt32()));
            var dagilim = parlaklik.GetProperty("LuminanceDistributions");
            Assert.Equal(new[] { 1, 5, 10, 25, 50, 75, 90, 95, 99 }, dagilim.GetProperty("DistributionIndex").EnumerateArray().Select(e => e.GetInt32()));
            Assert.Equal(new[] { 0, 10, 50, 200, 500, 1000, 2000, 3000, 4000 + i }, dagilim.GetProperty("DistributionValues").EnumerateArray().Select(e => e.GetInt32()));
            Assert.Equal(400 + i, s.GetProperty("TargetedSystemDisplayMaximumLuminance").GetInt32());
            Assert.Equal(1, s.GetProperty("NumberOfWindows").GetInt32());
            Assert.Equal(i, s.GetProperty("SceneFrameIndex").GetInt32());
            Assert.Equal(i, s.GetProperty("SequenceFrameIndex").GetInt32());
            var egri = s.GetProperty("BezierCurveData");
            Assert.Equal(new[] { 102, 205, 307, 410, 512, 614, 717, 819, 922 }, egri.GetProperty("Anchors").EnumerateArray().Select(e => e.GetInt32()));
            Assert.Equal(0, egri.GetProperty("KneePointX").GetInt32());
            Assert.Equal(0, egri.GetProperty("KneePointY").GetInt32());
        }
    }

    [Fact]
    public void DonusturucuPaydasiFarkliKesriAlaninPaydasinaOlcekler()
    {
        var kare = Kare(0, 0).Replace("\"average_maxrgb\": \"1000/100000\"", "\"average_maxrgb\": \"1/100\"", StringComparison.Ordinal);
        var sonuc = Hdr10PlusJson.FromFfprobe(Dokum(kare));

        using var doc = JsonDocument.Parse(sonuc.Json!);
        Assert.Equal(1000, doc.RootElement.GetProperty("SceneInfo")[0].GetProperty("LuminanceParameters").GetProperty("AverageRGB").GetInt32());
    }

    [Fact]
    public void HdrOnArtiTasimayanKareVarsaDonusumReddedilirSayimKalir()
    {
        var sonuc = Hdr10PlusJson.FromFfprobe(Dokum(Kare(0, 0), Kare(42, 1, hdr10Arti: false), Kare(83, 2)));

        Assert.Null(sonuc.Json);
        Assert.Equal(3, sonuc.Frames);
        Assert.Equal(2, sonuc.Hdr10PlusFrames);
    }

    [Theory]
    [InlineData(", \"targeted_system_display_actual_peak_luminance_flag\": 1")]
    [InlineData(", \"fraction_bright_pixels\": \"5/1000\"")]
    public void TasiyamadigimizAlanVarsaDonusumReddedilir(string ek)
    {
        var sonuc = Hdr10PlusJson.FromFfprobe(Dokum(Kare(0, 0, ek: ek)));

        Assert.Null(sonuc.Json);
        Assert.Equal(1, sonuc.Hdr10PlusFrames);
    }

    [Fact]
    public void AyniKaredeIkiHdrOnArtiReddedilir()
    {
        var kare = Kare(0, 0);
        var satir = kare.Split('\n').Single(s => s.Contains(Hdr10PlusJson.SideDataType, StringComparison.Ordinal));
        var ikili = kare.Replace(satir, satir + ",\n" + satir, StringComparison.Ordinal);
        var sonuc = Hdr10PlusJson.FromFfprobe(Dokum(ikili, Kare(42, 1)));

        Assert.Null(sonuc.Json);
        Assert.Equal(2, sonuc.Frames);
        Assert.Equal(2, sonuc.Hdr10PlusFrames);
    }

    [Fact]
    public void IkiPencereliKareReddedilir()
    {
        var sonuc = Hdr10PlusJson.FromFfprobe(Dokum(Kare(0, 0).Replace("\"num_windows\": 1", "\"num_windows\": 2", StringComparison.Ordinal)));

        Assert.Null(sonuc.Json);
    }

    [Theory]
    [InlineData(@"C:\Users\a b\Temp\vidshrink_1_hdr10plus.json", @"C\:/Users/a b/Temp/vidshrink_1_hdr10plus.json")]
    [InlineData("/tmp/vidshrink_1_hdr10plus.json", "/tmp/vidshrink_1_hdr10plus.json")]
    [InlineData("/tmp/a=b'c:d.json", @"/tmp/a\=b\'c\:d.json")]
    public void WindowsYoluIkiNoktaKacisiylaX265ParametresineGirer(string yol, string beklenen)
        => Assert.Equal(beklenen, Hdr10PlusJson.EscapeForX265Params(yol));

    private static EncodePlan KopruPlani(string kodlayici)
    {
        var plan = Planla(Hdr10Arti()).Plan;
        plan.Codec = kodlayici;
        plan.Mode = "2pass";
        plan.Crf = null;
        plan.Hdr10PlusMetadataPath = @"C:\Temp\vidshrink_1_hdr10plus.json";
        return plan;
    }

    private static string? X265Params(IReadOnlyList<string> args)
    {
        var i = args.ToList().IndexOf("-x265-params");
        return i >= 0 && i + 1 < args.Count ? args[i + 1] : null;
    }

    [Fact]
    public void IkiGecisinIkisineDeAyniDhdr10InfoGider()
    {
        var plan = KopruPlani("libx265");
        var bir = FfmpegArguments.Build(Hdr10Arti(), plan, "cikti.mkv", 1, "vidshrink_1");
        var iki = FfmpegArguments.Build(Hdr10Arti(), plan, "cikti.mkv", 2, "vidshrink_1");

        const string beklenen = @"dhdr10-info=C\:/Temp/vidshrink_1_hdr10plus.json";
        Assert.Contains(beklenen, X265Params(bir) ?? "", StringComparison.Ordinal);
        Assert.Contains(beklenen, X265Params(iki) ?? "", StringComparison.Ordinal);
        Assert.Contains("hdr10-opt=1", X265Params(iki) ?? "", StringComparison.Ordinal);
        Assert.Single(iki, a => a == "-x265-params");
    }

    [Fact]
    public void YolYoksaDhdr10InfoYok()
    {
        var plan = KopruPlani("libx265");
        plan.Hdr10PlusMetadataPath = null;

        Assert.DoesNotContain("dhdr10-info", X265Params(FfmpegArguments.Build(Hdr10Arti(), plan, "cikti.mkv", 2, "vidshrink_1")) ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void Hdr10ArtiKaynakOtomatikteX265eYonlenirVeKopruAcilir()
    {
        var sonuc = Planla(Hdr10Arti(), kodlayicilar: new Kodlayicilar("libx264", "libx265", "libsvtav1"));

        Assert.Equal("libx265", sonuc.Plan.Codec);
        Assert.True(sonuc.Plan.Hdr10PlusBridge);
        var not = Assert.Single(sonuc.Plan.ReasonCodes, n => n.Code == ReasonCode.Hdr10PlusRoutedToX265);
        Assert.Equal("libx265", not.FallbackCodec);
        Assert.NotEqual("libx265", not.RequestedCodec);
        Assert.False(Var(sonuc, ReasonCode.HdrDynamicMetadataDropped));
        Assert.False(Var(sonuc, ReasonCode.EncoderFallback));
    }

    [Theory]
    [InlineData(CodecPreference.Compatible, SpeedMode.Quality)]
    [InlineData(CodecPreference.MaxCompression, SpeedMode.Quality)]
    [InlineData(CodecPreference.Auto, SpeedMode.Fast)]
    public void AcikTercihteYonlendirmeYok(CodecPreference tercih, SpeedMode hiz)
    {
        var sonuc = Planla(Hdr10Arti(), kodlayicilar: new Kodlayicilar("libx264", "libx265", "libsvtav1", "h264_nvenc", "hevc_nvenc"), tercih: tercih, hiz: hiz);

        Assert.NotEqual("libx265", sonuc.Plan.Codec);
        Assert.False(sonuc.Plan.Hdr10PlusBridge);
        Assert.False(Var(sonuc, ReasonCode.Hdr10PlusRoutedToX265));
        Assert.True(Var(sonuc, ReasonCode.HdrDynamicMetadataDropped) || Var(sonuc, ReasonCode.Hdr10PlusNotCarriedOnSvtAv1) || Var(sonuc, ReasonCode.HdrTonemapped));
    }

    [Fact]
    public void KilitliX265teYonlendirmeNotuYokKopruAcik()
    {
        var sonuc = Planla(Hdr10Arti(), "libx265");

        Assert.True(sonuc.Plan.Hdr10PlusBridge);
        Assert.False(Var(sonuc, ReasonCode.Hdr10PlusRoutedToX265));
        Assert.False(Var(sonuc, ReasonCode.HdrDynamicMetadataDropped));
    }

    [Fact]
    public void Hdr10KaynakBuKoddanGecmez()
    {
        var sonuc = Planla(Hdr10Arti() with { HasHdr10Plus = false }, kodlayicilar: new Kodlayicilar("libx264", "libx265", "libsvtav1"));

        Assert.NotEqual("libx265", sonuc.Plan.Codec);
        Assert.False(sonuc.Plan.Hdr10PlusBridge);
        Assert.DoesNotContain(sonuc.Plan.ReasonCodes, n => n.Code is ReasonCode.Hdr10PlusRoutedToX265 or ReasonCode.Hdr10PlusDroppedInCut or ReasonCode.Hdr10PlusNotCarriedOnSvtAv1);
    }

    [Fact]
    public void TonEslemedeYonlendirmeYok()
    {
        var sonuc = Planla(Hdr10Arti(), politika: HdrPolicy.TonemapToSdr, kodlayicilar: new Kodlayicilar("libx264", "libx265", "libsvtav1"));

        Assert.NotEqual("libx265", sonuc.Plan.Codec);
        Assert.False(sonuc.Plan.Hdr10PlusBridge);
    }

    [Fact]
    public void X265YoksaYonlendirmeYok()
    {
        var sonuc = Planla(Hdr10Arti(), kodlayicilar: new Kodlayicilar("libx264", "libsvtav1"));

        Assert.NotEqual("libx265", sonuc.Plan.Codec);
        Assert.False(sonuc.Plan.Hdr10PlusBridge);
        Assert.False(Var(sonuc, ReasonCode.Hdr10PlusRoutedToX265));
    }

    [Fact]
    public void KilitliSvtAv1deKopruYokKendiGerekcesiVar()
    {
        var sonuc = Planla(Hdr10Arti(), "libsvtav1");

        Assert.Equal("libsvtav1", sonuc.Plan.Codec);
        Assert.False(sonuc.Plan.Hdr10PlusBridge);
        Assert.True(Var(sonuc, ReasonCode.Hdr10PlusNotCarriedOnSvtAv1));
        Assert.False(Var(sonuc, ReasonCode.HdrDynamicMetadataDropped));
    }

    [Fact]
    public void KilitliDonanimdaGenelDinamikNotuDuser()
    {
        var sonuc = Planla(Hdr10Arti(), "hevc_nvenc");

        Assert.False(sonuc.Plan.Hdr10PlusBridge);
        Assert.True(Var(sonuc, ReasonCode.HdrDynamicMetadataDropped));
        Assert.False(Var(sonuc, ReasonCode.Hdr10PlusRoutedToX265));
    }

    [Fact]
    public void KesitteKopruKapanirVeGerekcesiDuser()
    {
        var kesit = new TrimWindow(10, 30);
        var kilitli = Planla(Hdr10Arti(), "libx265", kesit: kesit);
        var otomatik = Planla(Hdr10Arti(), kesit: kesit, kodlayicilar: new Kodlayicilar("libx264", "libx265", "libsvtav1"));

        Assert.False(kilitli.Plan.Hdr10PlusBridge);
        Assert.True(Var(kilitli, ReasonCode.Hdr10PlusDroppedInCut));
        Assert.False(Var(kilitli, ReasonCode.HdrDynamicMetadataDropped));
        Assert.False(otomatik.Plan.Hdr10PlusBridge);
        Assert.False(Var(otomatik, ReasonCode.Hdr10PlusRoutedToX265));
        Assert.NotEqual("libx265", otomatik.Plan.Codec);
    }

    [Fact]
    public void DetelecinedeKopruKapanir()
    {
        var sonuc = Planla(Hdr10Arti(), "libx265", suzgec: VideoFilterOptions.Default with { Detelecine = true });

        Assert.False(sonuc.Plan.Hdr10PlusBridge);
        Assert.True(Var(sonuc, ReasonCode.Hdr10PlusDroppedInCut));
    }

    [Fact]
    public void KareHiziDusunceKopruKapanir()
    {
        var info = Hdr10Arti() with { Fps = 60, DurationSeconds = 600, FileSizeBytes = 4000L * 1024 * 1024 };
        PlanResult Kur(double hedef) => PlanCalculator.BuildDetailed(info, new PlanOptions
        {
            TargetMb = hedef,
            Intent = Intent.Sharing,
            LockedCodec = "libx265",
            HdrPolicy = HdrPolicy.Preserve,
            AllowResolutionDrop = false
        }, null, Hepsi);

        var genis = Kur(2000);
        Assert.Equal(info.Fps, genis.Plan.Fps);
        Assert.True(genis.Plan.Hdr10PlusBridge);
        var sonuc = new[] { 8.0, 5.0, 3.0, 2.0, 1.5, 1.0, 0.6, 0.4 }.Select(Kur).FirstOrDefault(s => s.Plan.Fps < info.Fps - 0.01);
        Assert.NotNull(sonuc);
        Assert.False(sonuc.Plan.Hdr10PlusBridge);
        var not = Assert.Single(sonuc.Plan.ReasonCodes, n => n.Code == ReasonCode.Hdr10PlusDroppedInCut);
        Assert.Equal(sonuc.Plan.Fps, not.Fps);
    }

    [Fact]
    public void SayimTutmazsaSonucUyariOlur()
    {
        var plan = new EncodePlan();
        var tuttu = new EncodeResult(true, "c.mkv", 1, plan, 1, null, Hdr10Plus: new Hdr10PlusCount(12, 12));
        var tutmadi = new EncodeResult(true, "c.mkv", 1, plan, 1, null, Hdr10Plus: new Hdr10PlusCount(12, 0));
        var okunamadi = new EncodeResult(true, "c.mkv", 1, plan, 1, null, Hdr10Plus: new Hdr10PlusCount(0, 0));
        var koprusuz = new EncodeResult(true, "c.mkv", 1, plan, 1, null);

        Assert.Null(MainWindow.Hdr10PlusMissed(tuttu));
        Assert.Equal(new Hdr10PlusCount(12, 0), MainWindow.Hdr10PlusMissed(tutmadi));
        Assert.NotNull(MainWindow.Hdr10PlusMissed(okunamadi));
        Assert.Null(MainWindow.Hdr10PlusMissed(koprusuz));
    }

    [Fact]
    public void CozmeAsamasiEkrandaDilinde()
    {
        Strings.Use("tr");
        var method = typeof(MainWindow).GetMethod("LocalizeStage",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var ekran = (string)method!.Invoke(null, new object[] { EncodeRunner.Hdr10PlusStage })!;

        Assert.Equal(LanguageCatalog.Display(Strings.Get("main.stage.hdr10plus-metadata")), ekran);
        Assert.DoesNotContain("metadata", ekran, StringComparison.OrdinalIgnoreCase);
    }

    private static string Klasor
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "hdr10plus-kopru");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static async Task<(int Kod, string Cikti, string Hata)> KosAsync(string exe, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Klasor
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var surec = new Process { StartInfo = psi };
        surec.Start();
        var cikti = surec.StandardOutput.ReadToEndAsync();
        var hata = surec.StandardError.ReadToEndAsync();
        await surec.WaitForExitAsync();
        return (surec.ExitCode, await cikti, await hata);
    }

    private const string X265Az = "pools=2:frame-threads=1:log-level=error";

    /// <summary>12 kare, her karede farkli deger: alan karsilastirmasi kare kaymasini da yakalar.</summary>
    private static async Task KaynakUretAsync(string ad, string json)
    {
        var sahneler = Enumerable.Range(0, 12).Select(i => new Dictionary<string, object>
        {
            ["BezierCurveData"] = new { Anchors = new[] { 102, 205, 307, 410, 512, 614, 717, 819, 922 }, KneePointX = 0, KneePointY = 0 },
            ["LuminanceParameters"] = new
            {
                AverageRGB = 1000 + i * 7,
                LuminanceDistributions = new
                {
                    DistributionIndex = new[] { 1, 5, 10, 25, 50, 75, 90, 95, 99 },
                    DistributionValues = new[] { 0, 10, 50, 200, 500, 1000, 2000, 3000, 4000 + i }
                },
                MaxScl = new[] { 4000 + i, 3500, 3000 }
            },
            ["NumberOfWindows"] = 1,
            ["TargetedSystemDisplayMaximumLuminance"] = 400 + i,
            ["SceneFrameIndex"] = i,
            ["SceneId"] = 0,
            ["SequenceFrameIndex"] = i
        }).ToList();
        File.WriteAllText(Path.Combine(Klasor, json), JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["JSONInfo"] = new { HDR10plusProfile = "B", Version = "1.0" },
            ["SceneInfo"] = sahneler
        }));

        var sonuc = await KosAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=24:duration=0.5",
            "-vf", "format=yuv420p10le,setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc:range=tv",
            "-threads", "2", "-c:v", "libx265", "-b:v", "3M",
            "-x265-params", "hdr10=1:hdr10-opt=1:master-display=G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,1):max-cll=1000,400:"
                + X265Az + ":dhdr10-info=" + json,
            ad
        });
        Assert.True(sonuc.Kod == 0, sonuc.Hata);
    }

    /// <summary>
    /// Uctan uca: yoklama, plan (Otomatik x265'e yonlenir), kosucunun cozme gecisi, iki gecisli
    /// kodlama ve cikis sayimi. Alan karsilastirmasi kaynagin ve cikisin ffprobe dokumunu ayni
    /// donusturucuden gecirip sahne sahne esitler; yalniz kare sayisi degil. JSON'un yolu
    /// <c>%TEMP%</c> altinda tam Windows yoludur, <c>:</c> kacisi burada gercekten koşar.
    /// </summary>
    [FfmpegFact]
    public async Task CanliKopruIkiGecisteHerKareninAlanlariniTasir()
    {
        string[] adlar = ["kopru-kaynak.mkv", "kopru-kaynak.json", "kopru-cikti.mkv"];
        KanitKapanisi.Onceki(Klasor, adlar);
        await KaynakUretAsync("kopru-kaynak.mkv", "kopru-kaynak.json");
        var kaynak = Path.Combine(Klasor, "kopru-kaynak.mkv");
        var cikti = Path.Combine(Klasor, "kopru-cikti.mkv");

        var info = await FfprobeClient.ProbeAsync(kaynak);
        Assert.True(info.HasHdr10Plus);
        var plan = PlanCalculator.BuildDetailed(info, new PlanOptions
        {
            TargetMb = info.FileSizeMb * 0.6,
            Codec = CodecPreference.Auto,
            Intent = Intent.Sharing,
            HdrPolicy = HdrPolicy.Preserve
        }, null, new Kodlayicilar("libx264", "libx265", "libsvtav1")).Plan;
        Assert.True(plan.Codec == "libx265", plan.Codec + " | " + plan.Reason);
        Assert.True(plan.Hdr10PlusBridge);
        plan.Mode = "2pass";
        plan.Crf = null;
        plan.VideoBitrateK = 800;
        plan.EffectiveTargetMb = 1;
        plan.TurboFirstPass = false;
        plan.ExtraArgs = new List<string> { "-threads", "2", "-x265-params", X265Az };

        var onceki = Directory.EnumerateFiles(Path.GetTempPath(), "vidshrink_*_hdr10plus.json").ToHashSet();
        var asamalar = new List<string>();
        var ilerleme = new AnindaIlerleme(p => { lock (asamalar) asamalar.Add(p.Stage); });
        var sonuc = await new EncodeRunner().RunAsync(info, plan, cikti, 1, ilerleme);

        Assert.True(sonuc.Success, sonuc.Error);
        Assert.Contains(EncodeRunner.Hdr10PlusStage, asamalar);
        Assert.Contains(new EncodeStage(1, 2, 1).ToString(), asamalar);
        Assert.Contains(new EncodeStage(2, 2, 1).ToString(), asamalar);
        Assert.True(asamalar.IndexOf(EncodeRunner.Hdr10PlusStage) < asamalar.IndexOf(new EncodeStage(1, 2, 1).ToString()));
        Assert.DoesNotContain(Directory.EnumerateFiles(Path.GetTempPath(), "vidshrink_*_hdr10plus.json"), f => !onceki.Contains(f));

        var kaynakDokum = Hdr10PlusJson.FromFfprobe((await KosAsync(ToolLocator.Ffprobe, Hdr10PlusJson.FfprobeArguments(kaynak))).Cikti);
        var ciktiDokum = Hdr10PlusJson.FromFfprobe((await KosAsync(ToolLocator.Ffprobe, Hdr10PlusJson.FfprobeArguments(cikti))).Cikti);
        _cikti.WriteLine($"kaynak kare={kaynakDokum.Frames} hdr10+={kaynakDokum.Hdr10PlusFrames}");
        _cikti.WriteLine($"cikti kare={ciktiDokum.Frames} hdr10+={ciktiDokum.Hdr10PlusFrames}");
        _cikti.WriteLine($"kosucu sayimi kaynak={sonuc.Hdr10Plus?.SourceFrames} cikti={sonuc.Hdr10Plus?.OutputFrames}");

        Assert.Equal(new Hdr10PlusCount(12, 12), sonuc.Hdr10Plus);
        Assert.Equal(12, kaynakDokum.Hdr10PlusFrames);
        Assert.Equal(12, ciktiDokum.Hdr10PlusFrames);
        Assert.NotNull(kaynakDokum.Json);
        Assert.NotNull(ciktiDokum.Json);

        var kaynakSahne = JsonDocument.Parse(kaynakDokum.Json!).RootElement.GetProperty("SceneInfo").EnumerateArray().Select(e => e.GetRawText()).ToList();
        var ciktiSahne = JsonDocument.Parse(ciktiDokum.Json!).RootElement.GetProperty("SceneInfo").EnumerateArray().Select(e => e.GetRawText()).ToList();
        var esit = kaynakSahne.Zip(ciktiSahne).Count(c => c.First == c.Second);
        _cikti.WriteLine($"alan alan esit sahne={esit}/{kaynakSahne.Count}");
        _cikti.WriteLine("kare 0: " + ciktiSahne[0]);
        _cikti.WriteLine("kare 11: " + ciktiSahne[11]);
        Assert.Equal(kaynakSahne, ciktiSahne);
        using var ilk = JsonDocument.Parse(ciktiSahne[11]);
        Assert.Equal(1000 + 11 * 7, ilk.RootElement.GetProperty("LuminanceParameters").GetProperty("AverageRGB").GetInt32());

        KanitKapanisi.Kapat(Klasor, adlar);
    }

    /// <summary><see cref="Progress{T}"/> bildirimi baglama gore geciktirir; olcu sirayi aninda okumali.</summary>
    private sealed class AnindaIlerleme(Action<EncodeProgress> al) : IProgress<EncodeProgress>
    {
        public void Report(EncodeProgress value) => al(value);
    }
}
