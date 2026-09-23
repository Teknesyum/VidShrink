using System.Buffers.Binary;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kesit passthrough'a düşünce akış kopyası: veri akışı (<c>tmcd</c>) mp4 muxer'ını başlık
/// yazamadan düşürüyordu, <c>+faststart</c> .mov'a yazılmıyordu. Canlı kollar 2 sn'lik lavfi
/// kaynak üretir, olumsuz kontrol aynı koşumda düzeltmesiz argümanla ölçülür.
/// Kanıt <c>.calisma/test-ciktilari/kesit-kopyasi/</c>, yeşil koşum siler.
/// </summary>
public sealed class KesitKopyasiKapTests
{
    private static string Klasor
    {
        get
        {
            var yol = Path.Combine(TestPaths.OutputRoot, "kesit-kopyasi");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static MediaInfo Kaynak(string yol) => new()
    {
        FilePath = yol,
        FileSizeBytes = 1,
        DurationSeconds = 2,
        Width = 320,
        Height = 240,
        Fps = 25,
        VideoCodec = "h264",
        TotalBitrateBps = 1_000_000
    };

    private static readonly TrimWindow Pencere = new(0.4, 1.6);

    [Theory]
    [InlineData("cikti.mp4", true)]
    [InlineData("cikti.MOV", true)]
    [InlineData("cikti.m4v", true)]
    [InlineData("cikti.mkv", false)]
    [InlineData("cikti.webm", false)]
    public void FaststartMp4SoyununHerUzantisinaYazilir(string cikti, bool bekleniyor)
    {
        var args = FfmpegArguments.BuildTrimCopy(Kaynak("girdi"), Pencere, cikti);

        Assert.Equal(bekleniyor, args.Contains("+faststart"));
    }

    [Fact]
    public void VeriAkislariEslemdenCikarilir()
    {
        var args = FfmpegArguments.BuildTrimCopy(Kaynak("girdi.mp4"), Pencere, "cikti.mp4").ToList();

        var tum = args.IndexOf("0");
        Assert.Equal("-map", args[tum - 1]);
        Assert.Equal("-map", args[tum + 1]);
        Assert.Equal("-0:d", args[tum + 2]);
    }

    private static async Task KaynakUretAsync(string yol, bool zamanKodu)
    {
        var a = new List<string>
        {
            "-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=d=2:s=320x240:r=25",
            "-f", "lavfi", "-i", "sine=d=2", "-threads", "2",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "10", "-c:a", "aac"
        };
        if (zamanKodu) a.AddRange(new[] { "-timecode", "00:00:00:00" });
        a.Add(yol);
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, a);
    }

    private static IEnumerable<string> Duzeltmesiz(IReadOnlyList<string> args, string cikar)
    {
        var liste = args.ToList();
        var i = liste.IndexOf(cikar);
        liste.RemoveRange(i - 1, 2);
        return liste;
    }

    [Fact]
    public async Task ZamanKodluMp4KesitKopyasiBasariliVeriAkissizHaliDuser()
    {
        var kaynak = Path.Combine(Klasor, "tmcd.mp4");
        var cikti = Path.Combine(Klasor, "tmcd-kesit.mp4");
        var olumsuz = Path.Combine(Klasor, "tmcd-kesit-olumsuz.mp4");
        await KaynakUretAsync(kaynak, zamanKodu: true);

        var turler = await AkisGirdisi.RunAsync(ToolLocator.Ffprobe, new[] { "-v", "error", "-show_entries", "stream=codec_type", "-of", "csv=p=0", kaynak });
        Assert.Contains("data", turler.Out, StringComparison.Ordinal);

        var args = FfmpegArguments.BuildTrimCopy(Kaynak(kaynak), Pencere, cikti);
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffmpeg, args);
        var olumsuzSonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffmpeg, Duzeltmesiz(args, "-0:d").Select(x => x == cikti ? olumsuz : x));

        Assert.NotEqual(0, olumsuzSonuc.Code);
        Assert.Contains("Could not find tag for codec", olumsuzSonuc.Err, StringComparison.Ordinal);
        Assert.True(sonuc.Code == 0, sonuc.Err);
        var cikan = await AkisGirdisi.RunAsync(ToolLocator.Ffprobe, new[] { "-v", "error", "-show_entries", "stream=codec_type", "-of", "csv=p=0", cikti });
        Assert.Contains("video", cikan.Out, StringComparison.Ordinal);
        Assert.Contains("audio", cikan.Out, StringComparison.Ordinal);

        Directory.Delete(Klasor, true);
    }

    private static List<string> UstAtomlar(string yol)
    {
        var veri = File.ReadAllBytes(yol);
        var atomlar = new List<string>();
        var i = 0;
        while (i + 8 <= veri.Length)
        {
            var boy = (int)BinaryPrimitives.ReadUInt32BigEndian(veri.AsSpan(i, 4));
            atomlar.Add(System.Text.Encoding.ASCII.GetString(veri, i + 4, 4));
            i += boy >= 8 ? boy : veri.Length;
        }
        return atomlar;
    }

    [Fact]
    public async Task MovKesitKopyasindaMoovBastaFaststartsizSonda()
    {
        var kaynak = Path.Combine(Klasor, "kaynak.mov");
        var cikti = Path.Combine(Klasor, "kesit.mov");
        var olumsuz = Path.Combine(Klasor, "kesit-olumsuz.mov");
        await KaynakUretAsync(kaynak, zamanKodu: false);

        var args = FfmpegArguments.BuildTrimCopy(Kaynak(kaynak), Pencere, cikti);
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffmpeg, args);
        var olumsuzSonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffmpeg, Duzeltmesiz(args, "+faststart").Select(x => x == cikti ? olumsuz : x));
        Assert.True(sonuc.Code == 0, sonuc.Err);
        Assert.True(olumsuzSonuc.Code == 0, olumsuzSonuc.Err);

        var atom = UstAtomlar(cikti);
        var olumsuzAtom = UstAtomlar(olumsuz);
        Assert.True(olumsuzAtom.IndexOf("moov") > olumsuzAtom.IndexOf("mdat"), "olumsuz kontrol: faststart'siz moov sonda olmali, " + string.Join(' ', olumsuzAtom));
        Assert.True(atom.IndexOf("moov") < atom.IndexOf("mdat"), "moov mdat'tan once gelmeli, " + string.Join(' ', atom));

        Directory.Delete(Klasor, true);
    }
}

/// <summary>
/// Dönüştür sekmesinin hız anahtarı küçültmeyle aynı <see cref="FfmpegArguments.SpeedArgs"/>'tan
/// gelir. libvpx-vp9 <c>-preset</c> tanımaz: ffmpeg 9.0 onu "has not been used for any stream"
/// uyarısıyla yutuyor ve hız ayarı hiç gitmiyordu; VideoToolbox da almaz. libx264 olumsuz kontrol.
/// </summary>
public sealed class DonusturHizAnahtariTests
{
    private static readonly MediaInfo Source = new()
    {
        FilePath = "source.mp4",
        FileSizeBytes = 20 * 1024 * 1024,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 1_400_000,
        AudioCodec = "aac",
        AudioChannels = 2
    };

    private static List<string> Build(string container, string codec, ConversionQualityMode mode = ConversionQualityMode.Crf)
        => ConversionArguments.Build(Source, new ConversionPlan { Container = container, VideoCodec = codec, AudioCodec = null, QualityMode = mode }, "out." + container).ToList();

    [Fact]
    public void Vp9PresetYerineCpuUsedAlir()
    {
        var args = Build("webm", "libvpx-vp9");

        Assert.DoesNotContain("-preset", args);
        Assert.Equal("1", args[args.IndexOf("-cpu-used") + 1]);
        Assert.Equal("good", args[args.IndexOf("-deadline") + 1]);
    }

    [Fact]
    public void VideoToolboxHizAnahtariAlmaz()
    {
        var args = Build("mkv", "h264_videotoolbox", ConversionQualityMode.Bitrate);

        Assert.DoesNotContain("-preset", args);
        Assert.DoesNotContain("-cpu-used", args);
    }

    [Theory]
    [InlineData("mp4", "libx264", "slow")]
    [InlineData("mp4", "h264_nvenc", "p4")]
    [InlineData("webm", "libsvtav1", "8")]
    public void PresetAlanKodlayiciOlumsuzKontrol(string container, string codec, string preset)
    {
        var args = Build(container, codec);

        Assert.Equal(preset, args[args.IndexOf("-preset") + 1]);
    }

    [Fact]
    public async Task Vp9KodlayicisiPresetSecenegiBildirmiyor()
    {
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffmpeg, new[] { "-hide_banner", "-h", "encoder=libvpx-vp9" });

        Assert.Equal(0, sonuc.Code);
        Assert.Contains("-cpu-used", sonuc.Out, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"(?m)^\s+-preset\s", sonuc.Out);
    }
}
