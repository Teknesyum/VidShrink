using System.Diagnostics;
using System.Text.Json;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// B4, dinamik HDR (<c>docs/olcumler/b4-hdr-dinamik.md</c>): DV 8.1 yazilim HEVC/AV1'de
/// <c>-dolbyvision 1</c> ile tasinir, MP4 ailesinde <c>dvcC</c> icin <c>-strict unofficial</c>
/// eklenir; tasinamayan DV ve HDR10+ gerekce satirina <see cref="ReasonCode.HdrDynamicMetadataDropped"/>
/// olarak duser. Canli kollar ≤2 sn 320x180 kaynagi kendisi uretir (DV RPU'su elle yazilir),
/// kanit <c>.calisma/b4-hdr-dinamik/</c>, her olcu kendi dosyalarini siler.
/// </summary>
public sealed class HdrDinamikTests
{
    private sealed class Kodlayicilar : IEncoderAvailability, IHdr10EncoderAvailability
    {
        private readonly HashSet<string> _adlar;
        public Kodlayicilar(params string[] adlar) => _adlar = new HashSet<string>(adlar, StringComparer.OrdinalIgnoreCase);
        public bool HasEncoder(string name) => _adlar.Contains(name);
        public bool WorksAsEncoder(string codec) => _adlar.Contains(codec);
        public string? Hdr10PixelFormat(string codec) => _adlar.Contains(codec) && CodecModel.IsHardware(codec) ? "p010le" : null;
    }

    private static readonly Kodlayicilar Hepsi = new("libx264", "libx265", "libsvtav1", "hevc_nvenc");

    private static MediaInfo Hdr10() => new()
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
        IsHdr = true
    };

    private static MediaInfo Dv(int profil, int uyum) => Hdr10() with { DolbyVisionProfile = profil, DolbyVisionCompatibilityId = uyum };

    private static PlanResult Planla(MediaInfo info, string kodlayici, HdrPolicy politika = HdrPolicy.Preserve, VideoFilterOptions? suzgec = null)
        => PlanCalculator.BuildDetailed(info, new PlanOptions
        {
            TargetMb = 40,
            Intent = Intent.Sharing,
            LockedCodec = kodlayici,
            HdrPolicy = politika,
            Filters = suzgec ?? VideoFilterOptions.Default
        }, null, Hepsi);

    private static bool Dustu(PlanResult sonuc) => sonuc.Plan.ReasonCodes.Any(n => n.Code == ReasonCode.HdrDynamicMetadataDropped);

    private static bool Ardisik(IReadOnlyList<string> args, string bayrak, string deger)
        => args.Select((arg, i) => arg == bayrak && i + 1 < args.Count && args[i + 1] == deger).Any(x => x);

    [Theory]
    [InlineData("libx265")]
    [InlineData("libsvtav1")]
    public void Dv81YazilimKodlayicidaTasiniyorVeNotDusmuyor(string kodlayici)
    {
        var sonuc = Planla(Dv(8, 1), kodlayici);
        var args = FfmpegArguments.Build(Dv(8, 1), sonuc.Plan, "cikti.mkv", 0, null);

        Assert.Equal(kodlayici, sonuc.Plan.Codec);
        Assert.True(sonuc.Plan.DolbyVisionCarried);
        Assert.True(Ardisik(args, "-dolbyvision", "1"), string.Join(' ', args));
        Assert.DoesNotContain("-strict", args);
        Assert.False(Dustu(sonuc));
    }

    [Theory]
    [InlineData("cikti.mp4")]
    [InlineData("cikti.mov")]
    public void Mp4AilesindeDvcCIcinStrictUnofficialSonGeciste(string cikti)
    {
        var sonuc = Planla(Dv(8, 1), "libx265");
        var son = FfmpegArguments.Build(Dv(8, 1), sonuc.Plan, cikti, 2, "log");
        var ilk = FfmpegArguments.Build(Dv(8, 1), sonuc.Plan, cikti, 1, "log");

        Assert.True(Ardisik(son, "-strict", "unofficial"), string.Join(' ', son));
        Assert.True(son.ToList().IndexOf("-strict") < son.Count - 1);
        Assert.True(Ardisik(ilk, "-dolbyvision", "1"));
        Assert.DoesNotContain("-strict", ilk);
    }

    [Fact]
    public void DvsizHdr10KaynaktaBayrakYokNotYok()
    {
        var sonuc = Planla(Hdr10(), "libx265");
        var args = FfmpegArguments.Build(Hdr10(), sonuc.Plan, "cikti.mp4", 0, null);

        Assert.False(sonuc.Plan.DolbyVisionCarried);
        Assert.DoesNotContain("-dolbyvision", args);
        Assert.DoesNotContain("-strict", args);
        Assert.False(Dustu(sonuc));
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(7, 6)]
    [InlineData(8, 4)]
    [InlineData(8, 2)]
    public void Profil81DisindakiDvTasinmiyorVeNotDusuyor(int profil, int uyum)
    {
        var sonuc = Planla(Dv(profil, uyum), "libx265");
        var args = FfmpegArguments.Build(Dv(profil, uyum), sonuc.Plan, "cikti.mp4", 0, null);

        Assert.DoesNotContain("-dolbyvision", args);
        Assert.DoesNotContain("-strict", args);
        Assert.True(Dustu(sonuc));
    }

    [Fact]
    public void DonanimKodlayicisiDvTasimiyorVeNotDusuyor()
    {
        var sonuc = Planla(Dv(8, 1), "hevc_nvenc");
        var args = FfmpegArguments.Build(Dv(8, 1), sonuc.Plan, "cikti.mp4", 0, null);

        Assert.Equal("hevc_nvenc", sonuc.Plan.Codec);
        Assert.DoesNotContain("-dolbyvision", args);
        Assert.True(Dustu(sonuc));
    }

    [Fact]
    public void TonEslemedeDvBayragiYokDinamikNotuYok()
    {
        var sonuc = Planla(Dv(8, 1), "libx265", HdrPolicy.TonemapToSdr);
        var args = FfmpegArguments.Build(Dv(8, 1), sonuc.Plan, "cikti.mp4", 0, null);

        Assert.DoesNotContain("-dolbyvision", args);
        Assert.False(Dustu(sonuc));
    }

    [Fact]
    public void RenkMatrisiDonusumuDvyiKapatirVeNotDusurur()
    {
        var matris = new VideoFilterOptions { ColorMatrix = ColorMatrixTarget.Bt601 };
        var sonuc = Planla(Dv(8, 1), "libx265", suzgec: matris);
        var args = FfmpegArguments.Build(Dv(8, 1), sonuc.Plan, "cikti.mp4", 0, null);

        Assert.Contains(args, arg => arg.Contains("zscale=", StringComparison.Ordinal));
        Assert.DoesNotContain("-dolbyvision", args);
        Assert.True(Dustu(sonuc));
    }

    [Fact]
    public void PlandanSonraAcilanRenkMatrisiBayragiDusurur()
    {
        var sonuc = Planla(Dv(8, 1), "libx265");
        Assert.True(sonuc.Plan.DolbyVisionCarried);

        sonuc.Plan.Filters = new VideoFilterOptions { ColorMatrix = ColorMatrixTarget.Bt601 };
        var args = FfmpegArguments.Build(Dv(8, 1), sonuc.Plan, "cikti.mp4", 0, null);

        Assert.Contains(args, arg => arg.Contains("zscale=", StringComparison.Ordinal));
        Assert.DoesNotContain("-dolbyvision", args);
        Assert.DoesNotContain("-strict", args);
    }

    [Fact]
    public void DvBayragiRenkArgumanlarindaDegilKalibrasyonaGirmez()
    {
        var sonuc = Planla(Dv(8, 1), "libx265");

        Assert.DoesNotContain("-dolbyvision", sonuc.Plan.HdrColorArgs);
        Assert.DoesNotContain("-dolbyvision", FfmpegArguments.PsychovisualAndColorArgs("libx265", Array.Empty<string>(), sonuc.Plan.HdrColorArgs));
    }

    [Fact]
    public void Hdr10ArtiNotDusurur()
    {
        var sonuc = Planla(Hdr10() with { HasHdr10Plus = true }, "libx265");

        Assert.True(Dustu(sonuc));
        Assert.Contains("dynamic HDR metadata", sonuc.Plan.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void KaynakHedefinAltindaysaKopyaDinamikNotuDusurmez()
    {
        var info = Dv(8, 1) with { HasHdr10Plus = true, FileSizeBytes = 10L * 1024 * 1024 };
        var sonuc = Planla(info, "libx265");

        Assert.Equal(EncodeMode.PassThrough, sonuc.Plan.ModeEnum);
        Assert.False(Dustu(sonuc));
    }

    [Theory]
    [InlineData("mp4", true)]
    [InlineData("mkv", false)]
    [InlineData("webm", false)]
    public void DonusturmeSvtAv1DvTasiyor(string kap, bool strict)
    {
        var plan = new ConversionPlan { Container = kap, VideoCodec = "libsvtav1", AudioCodec = null };
        var args = ConversionArguments.Build(Dv(8, 1), plan, "cikti." + kap);

        Assert.True(Ardisik(args, "-dolbyvision", "1"), string.Join(' ', args));
        Assert.Equal(strict, Ardisik(args, "-strict", "unofficial"));
    }

    [Fact]
    public void DonusturmeX265VbvsizDvTasimiyor()
    {
        var plan = new ConversionPlan { Container = "mp4", VideoCodec = "libx265", AudioCodec = null };
        var args = ConversionArguments.Build(Dv(8, 1), plan, "cikti.mp4");

        Assert.DoesNotContain("-maxrate", args);
        Assert.DoesNotContain("-dolbyvision", args);
        Assert.DoesNotContain("-strict", args);
    }

    [Fact]
    public void YoklamaDoviKaydiniOkuyor()
    {
        using var akis = JsonDocument.Parse("""
            {"index":0,"side_data_list":[
              {"side_data_type":"Mastering display metadata"},
              {"side_data_type":"DOVI configuration record","dv_version_major":1,"dv_profile":8,"dv_level":1,"dv_bl_signal_compatibility_id":1}
            ]}
            """);
        using var bos = JsonDocument.Parse("""{"index":0,"side_data_list":[{"side_data_type":"Mastering display metadata"}]}""");

        Assert.Equal((8, 1), FfprobeClient.ParseDolbyVision(akis.RootElement));
        Assert.Equal((null, null), FfprobeClient.ParseDolbyVision(bos.RootElement));
    }

    /// <summary>
    /// B4 yan bulgusu: koordinat ve parlaklik kare hizi ayristiricisindan geciyordu, 0,1'in
    /// altindaki <c>blue_y</c> ve <c>min_luminance</c> dusuyor ve satir her kaynakta bos
    /// kaliyordu. CLL'de ffprobe'un alan adi <c>max_average</c>; <c>average_content</c> diye
    /// bir alan yok.
    /// </summary>
    [Fact]
    public void YoklamaStatikHdrVerisiniOkuyor()
    {
        using var akis = JsonDocument.Parse("""
            {"index":0,"side_data_list":[
              {"side_data_type":"Mastering display metadata","red_x":"34000/50000","red_y":"16000/50000","green_x":"13250/50000","green_y":"34500/50000","blue_x":"7500/50000","blue_y":"3000/50000","white_point_x":"15635/50000","white_point_y":"16450/50000","min_luminance":"50/10000","max_luminance":"10000000/10000"},
              {"side_data_type":"Content light level metadata","max_content":1000,"max_average":400}
            ]}
            """);

        Assert.Equal("G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,50)", FfprobeClient.ParseMasteringDisplay(akis.RootElement));
        Assert.Equal("1000,400", FfprobeClient.ParseContentLightLevel(akis.RootElement));
    }

    [Fact]
    public void YoklamaHdr10ArtiKaresiniTaniyor()
    {
        const string var = """{"frames":[{"side_data_list":[{"side_data_type":"Mastering display metadata"},{"side_data_type":"HDR Dynamic Metadata SMPTE2094-40 (HDR10+)"}]}]}""";
        const string yok = """{"frames":[{"side_data_list":[{"side_data_type":"Mastering display metadata"},{"side_data_type":"Content light level metadata"}]}]}""";

        Assert.True(FfprobeClient.FrameCarriesHdr10Plus(var));
        Assert.False(FfprobeClient.FrameCarriesHdr10Plus(yok));
        Assert.False(FfprobeClient.FrameCarriesHdr10Plus("bozuk"));
    }

    private static string Klasor
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "b4-hdr-dinamik");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static void Kapat(params string[] adlar) => KanitKapanisi.Kapat(Klasor, adlar);

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

    private static async Task KosVeyaAtAsync(string exe, IEnumerable<string> args)
    {
        var sonuc = await KosAsync(exe, args);
        if (sonuc.Kod != 0) throw new InvalidOperationException(sonuc.Hata);
    }

    private const string Pq = "format=yuv420p10le,setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc:range=tv";
    private const string X265Az = "pools=2:frame-threads=1:log-level=error";
    private const string Hdr10X265 = "hdr10=1:hdr10-opt=1:master-display=G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,1):max-cll=1000,400:";

    private static async Task Hdr10ArtiKaynagiAsync(string ad, bool dinamik)
    {
        var json = Path.Combine(Klasor, Path.GetFileNameWithoutExtension(ad) + ".json");
        var sahneler = Enumerable.Range(0, 48).Select(i => new Dictionary<string, object>
        {
            ["BezierCurveData"] = new { Anchors = new[] { 102, 205, 307, 410, 512, 614, 717, 819, 922 }, KneePointX = 0, KneePointY = 0 },
            ["LuminanceParameters"] = new
            {
                AverageRGB = 1000 + i,
                LuminanceDistributions = new
                {
                    DistributionIndex = new[] { 1, 5, 10, 25, 50, 75, 90, 95, 99 },
                    DistributionValues = new[] { 0, 10, 50, 200, 500, 1000, 2000, 3000, 4000 }
                },
                MaxScl = new[] { 4000, 3500, 3000 }
            },
            ["NumberOfWindows"] = 1,
            ["TargetedSystemDisplayMaximumLuminance"] = 400,
            ["SceneFrameIndex"] = i,
            ["SceneId"] = 0,
            ["SequenceFrameIndex"] = i
        }).ToList();
        File.WriteAllText(json, JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["JSONInfo"] = new { HDR10plusProfile = "B", Version = "1.0" },
            ["SceneInfo"] = sahneler
        }));

        var x265 = Hdr10X265 + X265Az;
        if (dinamik) x265 += ":dhdr10-info=" + Path.GetFileName(json);
        await KosVeyaAtAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=24:duration=2",
            "-vf", Pq, "-c:v", "libx265", "-b:v", "3M", "-x265-params", x265, ad
        });
    }

    private static async Task<(string Akis, string Kare)> OkuAsync(string ad)
    {
        var akis = await KosAsync(ToolLocator.Ffprobe, new[]
        {
            "-hide_banner", "-v", "error", "-select_streams", "v:0",
            "-show_entries", "stream_side_data=side_data_type,dv_profile,dv_bl_signal_compatibility_id", "-of", "compact", ad
        });
        var kare = await KosAsync(ToolLocator.Ffprobe, new[]
        {
            "-hide_banner", "-v", "error", "-select_streams", "v:0", "-read_intervals", "%+#3",
            "-show_frames", "-show_entries", "frame_side_data=side_data_type", "-of", "compact", ad
        });
        Assert.Equal(0, akis.Kod);
        Assert.Equal(0, kare.Kod);
        return (akis.Cikti, kare.Cikti);
    }

    private static int Say(string metin, string aranan)
    {
        var n = 0;
        for (var i = metin.IndexOf(aranan, StringComparison.Ordinal); i >= 0; i = metin.IndexOf(aranan, i + aranan.Length, StringComparison.Ordinal)) n++;
        return n;
    }

    private static IReadOnlyList<string> Kodlama(MediaInfo info, string kodlayici, string cikti)
    {
        var sonuc = PlanCalculator.BuildDetailed(info, new PlanOptions
        {
            TargetMb = info.FileSizeMb * 0.5,
            Intent = Intent.Sharing,
            LockedCodec = kodlayici,
            HdrPolicy = HdrPolicy.Preserve
        }, null, Hepsi);
        Assert.NotEqual(EncodeMode.PassThrough, sonuc.Plan.ModeEnum);
        sonuc.Plan.ExtraArgs = kodlayici == "libx265"
            ? new List<string> { "-x265-params", X265Az }
            : new List<string> { "-svtav1-params", "lp=2" };
        return FfmpegArguments.Build(info, sonuc.Plan, Path.Combine(Klasor, cikti), 0, null);
    }

    [FfmpegFact]
    public async Task CanliHdr10ArtiYoklanirVeKodlamadaDustuguNotaYaziliyor()
    {
        string[] adlar = ["h10.mkv", "h10.json", "h10-duz.mkv", "h10-duz.json", "h10-cikti.mkv"];
        KanitKapanisi.Onceki(Klasor, adlar);

        await Hdr10ArtiKaynagiAsync("h10.mkv", dinamik: true);
        await Hdr10ArtiKaynagiAsync("h10-duz.mkv", dinamik: false);
        var info = await FfprobeClient.ProbeAsync(Path.Combine(Klasor, "h10.mkv"));
        var duz = await FfprobeClient.ProbeAsync(Path.Combine(Klasor, "h10-duz.mkv"));

        Assert.True(info.HasHdr10Plus);
        Assert.False(duz.HasHdr10Plus);
        Assert.Null(info.DolbyVisionProfile);

        var args = Kodlama(info, "libx265", "h10-cikti.mkv");
        await KosVeyaAtAsync(ToolLocator.Ffmpeg, args);
        var (_, kare) = await OkuAsync("h10-cikti.mkv");

        Assert.Equal(0, Say(kare, "SMPTE2094-40"));
        Assert.Equal(3, Say(kare, "Mastering display"));
        Assert.True(Dustu(PlanCalculator.BuildDetailed(info, new PlanOptions
        {
            TargetMb = info.FileSizeMb * 0.5, Intent = Intent.Sharing, LockedCodec = "libx265", HdrPolicy = HdrPolicy.Preserve
        }, null, Hepsi)));
        Kapat(adlar);
    }

    [FfmpegFact]
    public async Task CanliDv81Mp4VeMkvdeDvcCIleTasiniyor()
    {
        string[] adlar = ["dv-ham.hevc", "dv-rpu.hevc", "dv.mkv", "dv-x265.mp4", "dv-x265-strictsiz.mp4", "dv-av1.mkv"];
        KanitKapanisi.Onceki(Klasor, adlar);

        await KosVeyaAtAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=24:duration=2",
            "-vf", Pq, "-c:v", "libx265", "-b:v", "3M", "-x265-params", Hdr10X265 + X265Az, "-f", "hevc", "dv-ham.hevc"
        });
        File.WriteAllBytes(Path.Combine(Klasor, "dv-rpu.hevc"), DvRpu.Ekle(File.ReadAllBytes(Path.Combine(Klasor, "dv-ham.hevc"))));
        await KosVeyaAtAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-r", "24", "-i", "dv-rpu.hevc", "-c:v", "libx265", "-b:v", "3M", "-maxrate", "3M", "-bufsize", "6M",
            "-x265-params", X265Az, "-pix_fmt", "yuv420p10le", "-dolbyvision", "1", "dv.mkv"
        });

        var info = await FfprobeClient.ProbeAsync(Path.Combine(Klasor, "dv.mkv"));
        Assert.Equal(8, info.DolbyVisionProfile);
        Assert.Equal(1, info.DolbyVisionCompatibilityId);

        var mp4 = Kodlama(info, "libx265", "dv-x265.mp4");
        Assert.True(Ardisik(mp4, "-strict", "unofficial"));
        await KosVeyaAtAsync(ToolLocator.Ffmpeg, mp4);
        var (akis, kare) = await OkuAsync("dv-x265.mp4");
        Assert.Contains("DOVI configuration record", akis);
        Assert.Contains("dv_profile=8", akis);
        Assert.Contains("dv_bl_signal_compatibility_id=1", akis);
        Assert.Equal(3, Say(kare, "Dolby Vision RPU Data"));

        var strictsiz = Kodlama(info, "libx265", "dv-x265-strictsiz.mp4").ToList();
        strictsiz.RemoveRange(strictsiz.IndexOf("-strict"), 2);
        await KosVeyaAtAsync(ToolLocator.Ffmpeg, strictsiz);
        var (strictsizAkis, _) = await OkuAsync("dv-x265-strictsiz.mp4");
        Assert.DoesNotContain("DOVI configuration record", strictsizAkis);

        var av1 = Kodlama(info, "libsvtav1", "dv-av1.mkv");
        await KosVeyaAtAsync(ToolLocator.Ffmpeg, av1);
        var (av1Akis, _) = await OkuAsync("dv-av1.mkv");
        Assert.Contains("dv_profile=10", av1Akis);
        Assert.Contains("dv_bl_signal_compatibility_id=1", av1Akis);
        Kapat(adlar);
    }
}

/// <summary>
/// Sentetik DV profil 8.1 RPU'su: rpu_type 2, format 18, vdr_rpu_profile 1, artik katmansiz,
/// ozdeslik polinomu, DM seviye 0 ve CRC32/MPEG-2. Her VCL NAL'dan sonra NAL 62 olarak
/// eklenir; ffmpeg'in HEVC cozucusu bunu kare yan verisine cevirir ve <c>-dolbyvision 1</c>
/// gercek bir 8.1 akisi yazar. Yontem <c>docs/olcumler/b4-hdr-dinamik.md</c>.
/// </summary>
internal static class DvRpu
{
    internal static byte[] Ekle(byte[] hevc)
    {
        var cikti = new List<byte>();
        var n = 0;
        foreach (var nal in Nallar(hevc))
        {
            cikti.AddRange(new byte[] { 0, 0, 0, 1 });
            cikti.AddRange(nal);
            if (((nal[0] >> 1) & 0x3F) > 31) continue;
            cikti.AddRange(new byte[] { 0, 0, 0, 1 });
            cikti.AddRange(Rpu(n == 0));
            n++;
        }
        return cikti.ToArray();
    }

    private static IEnumerable<byte[]> Nallar(byte[] d)
    {
        var baslar = new List<int>();
        for (var i = 0; i + 3 < d.Length; i++)
            if (d[i] == 0 && d[i + 1] == 0 && d[i + 2] == 1)
            {
                baslar.Add(i + 3);
                i += 2;
            }
        for (var k = 0; k < baslar.Count; k++)
        {
            var s = baslar[k];
            var e = k + 1 < baslar.Count ? baslar[k + 1] - 3 : d.Length;
            while (e > s && d[e - 1] == 0) e--;
            yield return d[s..e];
        }
    }

    private sealed class Bitler
    {
        private readonly List<int> _b = new();
        public void U(long v, int n) { for (var i = n - 1; i >= 0; i--) _b.Add((int)((v >> i) & 1)); }
        public void Ue(long v)
        {
            var x = v + 1;
            var len = 0;
            for (var t = x; t > 1; t >>= 1) len++;
            U(0, len);
            U(x, len + 1);
        }
        public void Se(long v) => Ue(v > 0 ? 2 * v - 1 : -2 * v);
        public void S16(int v) => U(v & 0xFFFF, 16);
        public byte[] Baytlar()
        {
            while (_b.Count % 8 != 0) _b.Add(0);
            var sonuc = new byte[_b.Count / 8];
            for (var i = 0; i < sonuc.Length; i++)
                for (var j = 0; j < 8; j++)
                    sonuc[i] = (byte)((sonuc[i] << 1) | _b[i * 8 + j]);
            return sonuc;
        }
    }

    private static uint Crc32Mpeg2(byte[] veri)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in veri)
        {
            crc ^= (uint)b << 24;
            for (var k = 0; k < 8; k++) crc = (crc & 0x80000000u) != 0 ? (crc << 1) ^ 0x04C11DB7u : crc << 1;
        }
        return crc;
    }

    private static byte[] Rpu(bool sahneBasi)
    {
        var w = new Bitler();
        w.U(2, 6);
        w.U(18, 11);
        w.U(1, 4);
        w.U(0, 4);
        w.U(1, 1);
        w.U(0, 1);
        w.U(0, 2);
        w.Ue(23);
        w.U(1, 2);
        w.U(0, 1);
        w.Ue(2);
        w.Ue(2);
        w.Ue(4);
        w.U(0, 1);
        w.U(0, 3);
        w.U(0, 1);
        w.U(1, 1);
        w.U(1, 1);
        w.U(0, 1);
        w.Ue(0);
        w.Ue(0);
        w.Ue(0);
        for (var c = 0; c < 3; c++) { w.Ue(0); w.U(0, 10); w.U(1023, 10); }
        w.Ue(0);
        w.Ue(0);
        for (var c = 0; c < 3; c++) { w.Ue(0); w.Ue(0); w.U(0, 1); w.Se(0); w.U(0, 23); w.Se(1); w.U(0, 23); }
        w.Ue(0);
        w.Ue(0);
        w.Ue(sahneBasi ? 1 : 0);
        foreach (var v in new[] { 9575, 0, 14742, 9575, -1754, -4383, 9575, 17372, 0 }) w.S16(v);
        foreach (var v in new[] { 67108864L, 536870912L, 536870912L }) w.U(v, 32);
        foreach (var v in new[] { 7222, 8771, 390, 2654, 12430, 1300, 0, 488, 15896 }) w.S16(v);
        w.U(65535, 16);
        w.U(0, 16);
        w.U(0, 16);
        w.U(0, 32);
        w.U(12, 5);
        w.U(0, 2);
        w.U(0, 2);
        w.U(1, 2);
        w.U(62, 12);
        w.U(3079, 12);
        w.U(42, 10);
        w.Ue(0);

        var govde = w.Baytlar();
        var crc = Crc32Mpeg2(govde);
        var ham = new List<byte> { 0x7C, 0x01, 0x19 };
        ham.AddRange(govde);
        ham.AddRange(new[] { (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc, (byte)0x80 });

        var kacisli = new List<byte>();
        var sifir = 0;
        foreach (var b in ham)
        {
            if (sifir >= 2 && b <= 3)
            {
                kacisli.Add(3);
                sifir = 0;
            }
            kacisli.Add(b);
            sifir = b == 0 ? sifir + 1 : 0;
        }
        return kacisli.ToArray();
    }
}
