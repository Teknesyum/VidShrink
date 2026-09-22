using VidShrink.Cli;
using VidShrink.Core;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// B1a flac: kayipsiz ses kodegi. Butceye 16 bit PCM tavaniyla (ornekleme × kanal × 16) girer,
/// hedefin %15'ine sigmazsa ya da kap tasimazsa kabin varsayilanina duser ve
/// <see cref="StreamNote.FlacFellBack"/> notu birakir. <c>-b:a</c> yazilmaz, <c>-sample_fmt s16</c> yazilir.
/// Kopyalanabilir kaynak ses ac3/eac3 kolundaki gibi once kopyalanir; karar olculeri o yuzden kopyasiz kurulur.
/// </summary>
public sealed class FlacSesTests
{
    private static SourceStream Ses(int ornekleme, int kanal)
        => new(1, StreamKind.Audio, "aac", "eng", Channels: kanal, BitrateBps: 128_000, SampleRate: ornekleme);

    private static StreamPlan Karar(OutputContainer kap, double hedefMb, int ornekleme = 48000, int kanal = 2)
        => StreamMapping.Decide(Kaynak(600, Video, Ses(ornekleme, kanal)), StreamRequest.Default, kap, 160, null, "flac", false, hedefMb);

    /// <summary>Tavan ornekleme hizindan ve kanaldan turer; bilinmeyen hiz 48 kHz sayilir.</summary>
    [Theory]
    [InlineData(44100, 2, 1412)]
    [InlineData(48000, 2, 1536)]
    [InlineData(0, 2, 1536)]
    [InlineData(16000, 1, 256)]
    public void TavanOrneklemeVeKanaldan(int ornekleme, int kanal, int beklenen)
        => Assert.Equal(beklenen, StreamMapping.FlacCeilingK(ornekleme, kanal));

    /// <summary>Butce yeterken flac kurulur: tavan izin hizi olur, arguman <c>-sample_fmt s16</c>, <c>-b:a</c> yok.</summary>
    [Fact]
    public void ButceYeterkenFlacKuruluyor()
    {
        var plan = Karar(OutputContainer.Mkv, 1000);
        var args = plan.OutputArguments();

        Assert.Equal("flac", plan.Audio[0].Codec);
        Assert.Equal(1536, plan.Audio[0].BitrateK);
        Assert.Equal("s16", Sonraki(args, "-sample_fmt"));
        Assert.DoesNotContain("-b:a", args);
        Assert.DoesNotContain(StreamNote.FlacFellBack, plan.Notes);
        Assert.True(plan.SideK >= 1536);
    }

    /// <summary>
    /// Olumsuz kontrol: 10 dk'da 100 MB'in %15'i ~210k, 1536k'lik tavan sigmaz — aac'ye duser, not
    /// dusulur ve <c>-sample_fmt</c> yazilmaz. WebM flac tasimaz, orada da duser.
    /// </summary>
    [Theory]
    [InlineData(OutputContainer.Mp4, 100, "aac")]
    [InlineData(OutputContainer.WebM, 1000, "libopus")]
    public void SigmayincaVarsayilanaDusuyor(OutputContainer kap, double hedefMb, string beklenen)
    {
        var plan = Karar(kap, hedefMb);

        Assert.Equal(beklenen, plan.Audio[0].Codec);
        Assert.Contains(StreamNote.FlacFellBack, plan.Notes);
        Assert.DoesNotContain("-sample_fmt", plan.OutputArguments());
    }

    /// <summary>CLI <c>--ses-kodek flac</c> plana iner; tanimayan ad <c>error.bad-audio-codec</c>.</summary>
    [Fact]
    public void CliFlacIsteginiPlanaIndiriyor()
    {
        var parsed = CliParser.Parse(new[] { "plan", "a.mp4", "--hedef", "25", "--ses-kodek", "flac" });
        Assert.True(parsed.Ok, parsed.ErrorKey);
        Assert.Equal(AudioCodecChoice.Flac, parsed.Request!.ToPlanOptions(10).AudioCodec);

        var bozuk = CliParser.Parse(new[] { "plan", "a.mp4", "--hedef", "25", "--audio-codec", "opus" });
        Assert.False(bozuk.Ok);
        Assert.Equal("error.bad-audio-codec", bozuk.ErrorKey);
    }

    /// <summary>
    /// Canli kol: 16 kHz mono kaynak urunun zincirinden gecer, ffprobe flac s16 16000 okur.
    /// Olumsuz kontrol: secimsiz koşum aac verir.
    /// </summary>
    [Fact]
    public async Task CanliFlacCiktisi()
    {
        var kaynak = await SesliAsync("flac-kaynak.mkv", 16000, 1);

        var (_, plan, _) = await KodlaAsync(kaynak, Yol("flac-cikti.mp4"), options => options.AudioCodec = AudioCodecChoice.Flac);
        await KodlaAsync(kaynak, Yol("flac-secimsiz.mp4"), _ => { });

        Assert.Equal("flac", plan.AudioCodec);
        var ses = (await AkislarAsync(Yol("flac-cikti.mp4"))).Single(stream => Alan(stream, "codec_type") == "audio");
        Assert.Equal("flac", Alan(ses, "codec_name"));
        Assert.Equal("s16", Alan(ses, "sample_fmt"));
        Assert.Equal("16000", Alan(ses, "sample_rate"));
        var secimsiz = (await AkislarAsync(Yol("flac-secimsiz.mp4"))).Single(stream => Alan(stream, "codec_type") == "audio");
        Assert.Equal("aac", Alan(secimsiz, "codec_name"));

        Kapat("flac-*");
    }
}
