using System.Globalization;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// E8: HandBrake'in <c>--align-av</c> ve <c>--ipod-atom</c> yuzeylerinin karsiligi.
/// Sayilar <c>docs/olcumler/e8-kap-uyumlulugu.md</c>'de, gercek ffmpeg 9.0 kosumundan.
/// </summary>
public sealed class KapUyumluluguTests
{
    private static MediaInfo Kaynak(params SourceStream[] streams) => new()
    {
        FilePath = "girdi.mkv",
        FileSizeBytes = 400L * 1024 * 1024,
        DurationSeconds = 600,
        Width = 1920,
        Height = 1080,
        Fps = 24,
        VideoCodec = "h264",
        TotalBitrateBps = 5_000_000,
        AudioCodec = streams.FirstOrDefault(stream => stream.Kind == StreamKind.Audio)?.Codec,
        AudioBitrateBps = streams.FirstOrDefault(stream => stream.Kind == StreamKind.Audio)?.BitrateBps ?? 0,
        AudioChannels = streams.FirstOrDefault(stream => stream.Kind == StreamKind.Audio)?.Channels ?? 0,
        Streams = streams
    };

    private static readonly SourceStream Video = new(0, StreamKind.Video, "h264");

    private static StreamPlan Plan(OutputContainer container, bool keepAll, params SourceStream[] streams)
        => StreamMapping.Decide(Kaynak(streams), new StreamRequest(KeepAllTracks: keepAll), container, 128, null, "aac", false, 100);

    private static string Sonraki(IReadOnlyList<string> args, string bayrak)
    {
        var index = args.ToList().IndexOf(bayrak);
        Assert.True(index >= 0 && index + 1 < args.Count, bayrak + " bayragi yok");
        return args[index + 1];
    }

    [Fact]
    public void YenidenKodlananSesHizalamaSuzgeciAliyor()
    {
        var args = Plan(OutputContainer.Mp4, false, Video,
            new SourceStream(1, StreamKind.Audio, "dts", "tur", Channels: 6, BitrateBps: 1_500_000)).OutputArguments();

        Assert.Equal("aac", Sonraki(args, "-c:a"));
        Assert.Equal(StreamMapping.SesHizalama, Sonraki(args, "-filter:a"));
    }

    [Fact]
    public void KopyalananSesSuzgecAlmaz()
    {
        var plan = StreamMapping.Decide(
            Kaynak(Video, new SourceStream(1, StreamKind.Audio, "ac3", "tur", Channels: 6, BitrateBps: 384_000)),
            new StreamRequest(KeepAllTracks: true), OutputContainer.Mkv, 640, null, "aac", true, 10_000);
        var args = plan.OutputArguments();

        Assert.True(Assert.Single(plan.Audio).Copies);
        Assert.Equal("copy", Sonraki(args, "-c:a"));
        Assert.DoesNotContain("-filter:a", args);
    }

    [Fact]
    public void SessizKaynakSuzgecUretmez()
    {
        var args = Plan(OutputContainer.Mp4, false, Video).OutputArguments();

        Assert.Contains("-an", args);
        Assert.DoesNotContain(args, deger => deger.StartsWith("-filter:a", StringComparison.Ordinal));
    }

    [Fact]
    public void IkiIzdeSuzgecIzBasinaNumaralanir()
    {
        var plan = Plan(OutputContainer.Mkv, true, Video,
            new SourceStream(1, StreamKind.Audio, "dts", "tur", Channels: 6, BitrateBps: 1_500_000),
            new SourceStream(2, StreamKind.Audio, "truehd", "eng", Channels: 8, BitrateBps: 3_000_000));
        var args = plan.OutputArguments();

        Assert.Equal(2, plan.Audio.Count);
        Assert.All(plan.Audio, track => Assert.False(track.Copies));
        Assert.Equal(StreamMapping.SesHizalama, Sonraki(args, "-filter:a:0"));
        Assert.Equal(StreamMapping.SesHizalama, Sonraki(args, "-filter:a:1"));
        Assert.DoesNotContain("-filter:a", args);
    }

    [Fact]
    public void SuzgecKendiIzininKodeginenSonraGelir()
    {
        var args = Plan(OutputContainer.Mp4, false, Video,
            new SourceStream(1, StreamKind.Audio, "dts", "tur", Channels: 6, BitrateBps: 1_500_000)).OutputArguments().ToList();

        Assert.True(args.IndexOf("-c:a") < args.IndexOf("-filter:a"));
        Assert.True(args.IndexOf("-b:a") < args.IndexOf("-filter:a"));
    }

    [Fact]
    public void HizalamaSuzgeciIkiSartiBirdenTasiyor()
    {
        Assert.Contains("async=1", StreamMapping.SesHizalama, StringComparison.Ordinal);
        Assert.Contains("first_pts=0", StreamMapping.SesHizalama, StringComparison.Ordinal);
        Assert.StartsWith("aresample=", StreamMapping.SesHizalama, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cikti.mp4")]
    [InlineData("cikti.mkv")]
    public void KesitKopyasiDamgayiSifirliyor(string cikti)
    {
        var args = FfmpegArguments.BuildTrimCopy(Kaynak(Video), new TrimWindow(40, 70), cikti).ToList();

        Assert.Equal("make_zero", Sonraki(args, "-avoid_negative_ts"));
        Assert.True(args.IndexOf("-i") < args.IndexOf("-avoid_negative_ts"));
        Assert.Equal("copy", Sonraki(args, "-c"));
    }

    [Fact]
    public void ParcaKopyasiDamgayaDokunmuyor()
    {
        var plan = new EncodePlan { Mode = "PassThrough", Codec = "h264", Preset = "copy" };
        var args = FfmpegArguments.BuildSegment(Kaynak(Video), plan, 1.2, 1.5, "parca.mp4");

        Assert.DoesNotContain("-avoid_negative_ts", args);
    }

    /// <summary>
    /// Canli kol: sesi 0,4 sn gec baslayan kaynak gercek ffmpeg 9.0'dan gecirilir ve
    /// ciktinin ses akisi ffprobe ile okunur. Suzgec yokken kaymayi kap icinde tasiyordu
    /// (olumsuz kontrol ayni kosumda olculur).
    /// </summary>
    [Fact]
    public async Task KayikSesHizalamaSuzgeciyleSifirdanBasliyor()
    {
        var klasor = Path.Combine(AkisGirdisi.Folder, "e8");
        Directory.CreateDirectory(klasor);
        var kaynak = Path.Combine(klasor, "kayik.mkv");
        var hizali = Path.Combine(klasor, "hizali.mp4");
        var hizasiz = Path.Combine(klasor, "hizasiz.mp4");

        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y",
            "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=3",
            "-f", "lavfi", "-itsoffset", "0.4", "-i", "sine=f=440:d=3",
            "-map", "0:v", "-map", "1:a",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-c:a", "aac", kaynak
        });

        IEnumerable<string> Kodla(string cikti, bool suzgec)
        {
            var a = new List<string>
            {
                "-hide_banner", "-y", "-i", kaynak, "-map", "0:v", "-map", "0:a",
                "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-c:a", "aac"
            };
            if (suzgec) a.AddRange(new[] { "-filter:a", StreamMapping.SesHizalama });
            a.Add(cikti);
            return a;
        }

        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, Kodla(hizasiz, false));
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, Kodla(hizali, true));

        var kayan = await SesBaslangiciAsync(hizasiz);
        var duzelen = await SesBaslangiciAsync(hizali);

        Assert.True(kayan > 0.3, "olumsuz kontrol: suzgecsiz cikti kaymayi tasimali, olculen " + kayan);
        Assert.True(duzelen < 0.01, "suzgecli cikti sifirdan baslamali, olculen " + duzelen);

        AkisGirdisi.Kapat(Path.Combine("e8", "*"), "e8");
    }

    private static async Task<double> SesBaslangiciAsync(string yol)
    {
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffprobe, new[]
        {
            "-v", "error", "-select_streams", "a:0", "-show_entries", "stream=start_time", "-of", "csv=p=0", yol
        });
        Assert.Equal(0, sonuc.Code);
        return double.Parse(sonuc.Out.Trim(), CultureInfo.InvariantCulture);
    }

    [Fact]
    public void KesitliYenidenKodlamaDamgaEklemiyor()
    {
        var plan = PlanCalculator.Build(Kaynak(Video), new PlanOptions { TargetMb = 2, Trim = new TrimWindow(40, 70) });
        var args = FfmpegArguments.Build(Kaynak(Video), plan, "cikti.mp4", 0, null);

        Assert.NotEqual(EncodeMode.PassThrough, plan.ModeEnum);
        Assert.DoesNotContain("-avoid_negative_ts", args);
    }
}

