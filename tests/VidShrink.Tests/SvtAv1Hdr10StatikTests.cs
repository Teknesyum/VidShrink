using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// HB durum §3 satir 28: HDR10 statik verisi libsvtav1'e <c>-svtav1-params</c> icinde
/// <c>mastering-display</c> ve <c>content-light</c> ile acikca yazilir
/// (<c>docs/olcumler/hb28-svtav1-hdr10-statik.md</c>). Canli kollar 160x96, 5 kare,
/// <c>-threads 2</c>, <c>lp=2</c>; kanit <c>.calisma/hb28-svtav1-hdr10/</c>, yesil kosum siler.
/// </summary>
public sealed class SvtAv1Hdr10StatikTests
{
    private readonly ITestOutputHelper _cikti;

    public SvtAv1Hdr10StatikTests(ITestOutputHelper cikti) => _cikti = cikti;

    private const string KaynakMastering = "G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,50)";
    private const string SvtMastering = "G(0.265,0.69)B(0.15,0.06)R(0.68,0.32)WP(0.3127,0.329)L(1000,0.005)";

    private sealed class Kodlayicilar : IEncoderAvailability, IHdr10EncoderAvailability
    {
        private readonly HashSet<string> _adlar;
        public Kodlayicilar(params string[] adlar) => _adlar = new HashSet<string>(adlar, StringComparer.OrdinalIgnoreCase);
        public bool HasEncoder(string name) => _adlar.Contains(name);
        public bool WorksAsEncoder(string codec) => _adlar.Contains(codec);
        public string? Hdr10PixelFormat(string codec) => null;
    }

    private static readonly Kodlayicilar Hepsi = new("libx264", "libx265", "libsvtav1");

    private static MediaInfo Hdr10(string? mastering = KaynakMastering, string? cll = "1000,400") => new()
    {
        FilePath = "kaynak.mkv",
        FileSizeBytes = 500_000_000L,
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
        MasteringDisplayMetadata = mastering,
        ContentLightLevel = cll
    };

    private static EncodePlan Planla(MediaInfo info, string kodek = "libsvtav1", HdrPolicy politika = HdrPolicy.Preserve, double hedefMb = 40)
        => PlanCalculator.BuildDetailed(info, new PlanOptions
        {
            TargetMb = hedefMb,
            Intent = Intent.Sharing,
            LockedCodec = kodek,
            HdrPolicy = politika
        }, null, Hepsi).Plan;

    private static List<string> Degerler(IReadOnlyList<string> args, string bayrak)
        => args.Select((a, i) => (a, i)).Where(t => t.a == bayrak && t.i + 1 < args.Count).Select(t => args[t.i + 1]).ToList();

    [Fact]
    public void X265BicimiSvtAv1KesirliBicimineCevrilir()
    {
        Assert.Equal(SvtMastering, HdrResolver.SvtAv1MasteringDisplay(KaynakMastering));
        Assert.Equal("G(0.17,0.797)B(0.131,0.046)R(0.708,0.292)WP(0.3127,0.329)L(4000,0.0001)",
            HdrResolver.SvtAv1MasteringDisplay("G(8500,39850)B(6550,2300)R(35400,14600)WP(15635,16450)L(40000000,1)"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("G(0.265,0.69)B(0.15,0.06)R(0.68,0.32)WP(0.3127,0.329)L(1000,0.005)")]
    [InlineData("G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)")]
    [InlineData("G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,50):x")]
    public void TanimsizBicimCevrilmez(string girdi)
        => Assert.Null(HdrResolver.SvtAv1MasteringDisplay(girdi));

    [Fact]
    public void SvtAv1KorunanHdrdeIkiAnahtarYazilir()
    {
        var hdr = HdrResolver.Resolve(Hdr10(), HdrPolicy.Preserve, "libsvtav1", Hepsi);

        Assert.Equal(new[] { $"mastering-display={SvtMastering}:content-light=1000,400" }, Degerler(hdr.ColorArgs, "-svtav1-params"));
        Assert.Empty(Degerler(hdr.ColorArgs, "-x265-params"));
    }

    [Fact]
    public void KaynaktaVeriYoksaAnahtarYazilmaz()
    {
        var hdr = HdrResolver.Resolve(Hdr10(null, null), HdrPolicy.Preserve, "libsvtav1", Hepsi);

        Assert.Empty(Degerler(hdr.ColorArgs, "-svtav1-params"));
        Assert.Contains("-color_primaries", hdr.ColorArgs);
    }

    [Fact]
    public void YalnizBiriVarsaYalnizO()
    {
        Assert.Equal(new[] { $"mastering-display={SvtMastering}" },
            Degerler(HdrResolver.Resolve(Hdr10(cll: null), HdrPolicy.Preserve, "libsvtav1", Hepsi).ColorArgs, "-svtav1-params"));
        Assert.Equal(new[] { "content-light=1000,400" },
            Degerler(HdrResolver.Resolve(Hdr10(mastering: null), HdrPolicy.Preserve, "libsvtav1", Hepsi).ColorArgs, "-svtav1-params"));
    }

    [Fact]
    public void TonEslemedeVeX265teSvtAnahtariYok()
    {
        var ton = HdrResolver.Resolve(Hdr10(), HdrPolicy.TonemapToSdr, "libsvtav1", Hepsi);
        var x265 = HdrResolver.Resolve(Hdr10(), HdrPolicy.Preserve, "libx265", Hepsi);

        Assert.Empty(Degerler(ton.ColorArgs, "-svtav1-params"));
        Assert.Empty(Degerler(x265.ColorArgs, "-svtav1-params"));
        Assert.Contains($"master-display={KaynakMastering}", Degerler(x265.ColorArgs, "-x265-params").Single());
    }

    [Fact]
    public void TamArgumandaTekSvtav1ParamsTuneVeLpIleBirlesir()
    {
        var info = Hdr10();
        var plan = Planla(info);
        plan.Tune = "0";
        plan.ExtraArgs = new List<string> { "-svtav1-params", "lp=2" };

        var args = FfmpegArguments.Build(info, plan, "out.mkv", 0, null);
        var deger = Assert.Single(Degerler(args, "-svtav1-params"));
        var anahtarlar = deger.Split(':');

        Assert.Equal("libsvtav1", plan.Codec);
        Assert.Contains("tune=0", anahtarlar);
        Assert.Contains("lp=2", anahtarlar);
        Assert.Contains($"mastering-display={SvtMastering}", anahtarlar);
        Assert.Contains("content-light=1000,400", anahtarlar);
        Assert.Single(anahtarlar, a => a.StartsWith("mastering-display=", StringComparison.Ordinal));
    }

    [Fact]
    public void Hdr10ArtiSvtAv1deStatikKatmanYineYazilir()
    {
        var info = Hdr10() with { HasHdr10Plus = true };
        var plan = Planla(info);

        var args = FfmpegArguments.Build(info, plan, "out.mkv", 0, null);

        Assert.Equal("libsvtav1", plan.Codec);
        Assert.Contains($"mastering-display={SvtMastering}", Assert.Single(Degerler(args, "-svtav1-params")));
        Assert.DoesNotContain(args, a => a.Contains("dhdr10-info", StringComparison.Ordinal));
    }

    private static string Klasor
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "hb28-svtav1-hdr10");
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

    /// <summary>
    /// x265'in dogrudan yazdigi mkv'de HDR10 verisi yalniz SEI'de durur, akis duzeyinde yoktur
    /// ve yoklama onu gormez (olculdu). Gercek dosyalardaki gibi akis duzeyinde tasimak icin
    /// ikinci bir kodlama (ffv1) cozulen yan veriyi akisa yazar.
    /// </summary>
    private static async Task KaynakUretAsync(string ad, string? x265Hdr)
    {
        var x265 = "pools=2:frame-threads=1:log-level=error:colorprim=bt2020:transfer=smpte2084:colormatrix=bt2020nc"
                   + (x265Hdr is null ? "" : ":hdr10=1:hdr10-opt=1:" + x265Hdr);
        var ilk = x265Hdr is null ? ad : "ara-" + ad;
        var sonuc = await KosAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=160x96:rate=10:duration=0.5",
            "-vf", "format=yuv420p10le,setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc:range=tv",
            "-threads", "2", "-c:v", "libx265", "-crf", "30", "-x265-params", x265, ilk
        });
        Assert.True(sonuc.Kod == 0, sonuc.Hata);
        if (x265Hdr is null) return;

        sonuc = await KosAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-v", "error", "-y", "-i", ilk, "-threads", "2", "-c:v", "ffv1", ad
        });
        Assert.True(sonuc.Kod == 0, sonuc.Hata);
    }

    private async Task<Dictionary<string, double>> YanVeriAsync(string dosya)
    {
        var sonuc = await KosAsync(ToolLocator.Ffprobe, new[]
        {
            "-v", "error", "-select_streams", "v:0", "-read_intervals", "%+#1",
            "-show_frames", "-show_entries", "frame_side_data", "-of", "json", dosya
        });
        Assert.True(sonuc.Kod == 0, sonuc.Hata);
        File.AppendAllText(Path.Combine(Klasor, "kanit.txt"), $"== {Path.GetFileName(dosya)}\n{sonuc.Cikti}\n");
        var alanlar = new Dictionary<string, double>();
        using var doc = JsonDocument.Parse(sonuc.Cikti);
        foreach (var kare in doc.RootElement.GetProperty("frames").EnumerateArray().Take(1))
        {
            if (!kare.TryGetProperty("side_data_list", out var liste)) continue;
            foreach (var oge in liste.EnumerateArray())
            foreach (var alan in oge.EnumerateObject())
            {
                if (alan.Value.ValueKind == JsonValueKind.Number) alanlar[alan.Name] = alan.Value.GetDouble();
                else if (alan.Value.ValueKind == JsonValueKind.String && Oran(alan.Value.GetString()!) is { } oran) alanlar[alan.Name] = oran;
            }
        }
        _cikti.WriteLine($"{Path.GetFileName(dosya)}: " + string.Join(" ", alanlar.Select(a => $"{a.Key}={a.Value.ToString(CultureInfo.InvariantCulture)}")));
        return alanlar;
    }

    private static double? Oran(string metin)
    {
        var parca = metin.Split('/');
        if (parca.Length != 2
            || !double.TryParse(parca[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var pay)
            || !double.TryParse(parca[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var payda)
            || payda == 0) return null;
        return pay / payda;
    }

    private async Task<Dictionary<string, double>> KodlaAsync(MediaInfo info, string cikti, Func<string, string>? degistir = null)
    {
        var plan = Planla(info, hedefMb: info.FileSizeMb * 0.6);
        Assert.True(plan.Codec == "libsvtav1", plan.Codec + " | " + plan.Mode + " | " + plan.Reason);
        plan.Mode = "crf";
        plan.Crf = 50;
        plan.ExtraArgs = new List<string> { "-threads", "2", "-svtav1-params", "lp=2" };
        var args = FfmpegArguments.Build(info, plan, cikti, 0, null).Select(a => degistir is null ? a : degistir(a)).ToList();
        File.AppendAllText(Path.Combine(Klasor, "kanit.txt"), $"== {Path.GetFileName(cikti)} argumanlari\n{string.Join(' ', args)}\n");
        var sonuc = await KosAsync(ToolLocator.Ffmpeg, args);
        Assert.True(sonuc.Kod == 0, sonuc.Hata);
        return await YanVeriAsync(cikti);
    }

    /// <summary>
    /// Anahtarin bit akisina yazdigi: yan verisiz kaynak (ffmpeg 9'un kendi aktarimi burada bos
    /// kalir) yoklamanin bulmus gibi davrandigi bilgiyle kodlanir, deger ciktida okunur. Uydurma
    /// anahtar (SVT-AV1 sessizce yutar) ve anahtarsiz es olumsuz kontrol: ikisinde de yan veri yok.
    /// </summary>
    [FfmpegAvailableFact]
    public async Task CanliAnahtarBitAkisinaYazilirUydurmaAnahtarYazmaz()
    {
        string[] adlar = ["kanit.txt", "yanverisiz.mkv", "anahtarli.mkv", "uydurma.mkv", "anahtarsiz.mkv"];
        KanitKapanisi.Onceki(Klasor, adlar);
        await KaynakUretAsync("yanverisiz.mkv", null);
        var yoklanan = await FfprobeClient.ProbeAsync(Path.Combine(Klasor, "yanverisiz.mkv"));
        Assert.Null(yoklanan.MasteringDisplayMetadata);
        Assert.Null(yoklanan.ContentLightLevel);
        Assert.True(yoklanan.IsHdr);

        var enjekte = yoklanan with
        {
            MasteringDisplayMetadata = "G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(12345000,123)",
            ContentLightLevel = "987,321"
        };
        var anahtarli = await KodlaAsync(enjekte, Path.Combine(Klasor, "anahtarli.mkv"));
        var uydurma = await KodlaAsync(enjekte, Path.Combine(Klasor, "uydurma.mkv"),
            a => a.Replace("mastering-display=", "mastering-displayz=").Replace("content-light=", "content-lightz="));
        var anahtarsiz = await KodlaAsync(yoklanan, Path.Combine(Klasor, "anahtarsiz.mkv"));

        Assert.Equal(1234.5, anahtarli["max_luminance"], 2);
        Assert.Equal(0.0123, anahtarli["min_luminance"], 3);
        Assert.Equal(0.265, anahtarli["green_x"], 3);
        Assert.Equal(0.3127, anahtarli["white_point_x"], 3);
        Assert.Equal(987, anahtarli["max_content"]);
        Assert.Equal(321, anahtarli["max_average"]);
        Assert.False(uydurma.ContainsKey("max_luminance"));
        Assert.False(uydurma.ContainsKey("max_content"));
        Assert.False(anahtarsiz.ContainsKey("max_luminance"));
        Assert.False(anahtarsiz.ContainsKey("max_content"));

        KanitKapanisi.Kapat(Klasor, adlar);
    }

    /// <summary>
    /// Gercek HDR10 kaynagi yoklamadan plana, argumandan ciktiya: acik anahtar ffmpeg 9'un kendi
    /// aktarimini ezer (olculdu), bu yuzden cevirinin her alani kaynagin degerine esit cikmali.
    /// </summary>
    [FfmpegAvailableFact]
    public async Task CanliHdr10KaynagiDegeriCevrilerekAynenCikar()
    {
        string[] adlar = ["kanit.txt", "ara-hdr10.mkv", "hdr10.mkv", "hdr10-av1.mkv"];
        KanitKapanisi.Onceki(Klasor, adlar);
        await KaynakUretAsync("hdr10.mkv",
            "master-display=G(8500,39850)B(6550,2300)R(35400,14600)WP(15635,16450)L(40000000,1):max-cll=1234,567");
        var info = await FfprobeClient.ProbeAsync(Path.Combine(Klasor, "hdr10.mkv"));
        Assert.Equal("G(8500,39850)B(6550,2300)R(35400,14600)WP(15635,16450)L(40000000,1)", info.MasteringDisplayMetadata);
        Assert.Equal("1234,567", info.ContentLightLevel);

        var kaynak = await YanVeriAsync(Path.Combine(Klasor, "hdr10.mkv"));
        var cikti = await KodlaAsync(info, Path.Combine(Klasor, "hdr10-av1.mkv"));

        foreach (var alan in new[] { "red_x", "red_y", "green_x", "green_y", "blue_x", "blue_y", "white_point_x", "white_point_y" })
            Assert.True(Math.Abs(kaynak[alan] - cikti[alan]) < 1e-4, $"{alan}: kaynak {kaynak[alan]} cikti {cikti[alan]}");
        Assert.Equal(4000, cikti["max_luminance"], 2);
        Assert.Equal(0.0001, cikti["min_luminance"], 4);
        Assert.Equal(1234, cikti["max_content"]);
        Assert.Equal(567, cikti["max_average"]);

        KanitKapanisi.Kapat(Klasor, adlar);
    }
}
