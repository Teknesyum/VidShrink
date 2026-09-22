using VidShrink.Cli;
using VidShrink.Core;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// B1b ses normallestirme ve kazanc: <c>--ses-normal</c> (loudnorm I=-24 LRA=7 TP=-2) ve
/// <c>--ses-kazanc</c> (-20..20 dB). Suzgec yalniz yeniden kodlanan ize kurulur; istenince ses
/// kopyalanmaz, kopya zorunluysa not dusulur. Canli kol seviyeyi ffmpeg'in kendi olcerleriyle okur.
/// </summary>
public sealed class SesNormallestirmeTests
{
    private static readonly SourceStream Ses = new(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000, SampleRate: 44100);

    private static StreamPlan Karar(StreamRequest istek, string kodek = "aac")
        => StreamMapping.Decide(Kaynak(600, Video, Ses), istek, OutputContainer.Mp4, 160, null, kodek, true, 100);

    /// <summary>Zincir sirasi: hizalama, kazanc, loudnorm, kaynak hizina donus.</summary>
    [Fact]
    public void ZincirSirasi()
    {
        Assert.Equal("aresample=async=1:first_pts=0,volume=-6dB,loudnorm=I=-24:LRA=7:TP=-2,aresample=44100",
            StreamMapping.AudioFilter(new StreamRequest(AudioLoudnorm: true, AudioGainDb: -6), 44100));
        Assert.Equal("aresample=async=1:first_pts=0,volume=3.5dB",
            StreamMapping.AudioFilter(new StreamRequest(AudioGainDb: 3.5), 0));
        Assert.Equal("aresample=async=1:first_pts=0,loudnorm=I=-24:LRA=7:TP=-2,aresample=48000",
            StreamMapping.AudioFilter(new StreamRequest(AudioLoudnorm: true), 0));
    }

    /// <summary>Olumsuz kontrol: istek yoksa ya da kazanc 0 ise yalniz hizalama kalir.</summary>
    [Fact]
    public void IstekYokkenYalnizHizalama()
    {
        Assert.Equal(StreamMapping.SesHizalama, StreamMapping.AudioFilter(StreamRequest.Default, 44100));
        Assert.Equal(StreamMapping.SesHizalama, StreamMapping.AudioFilter(new StreamRequest(AudioGainDb: 0), 44100));
        Assert.False(new StreamRequest(AudioGainDb: 0).FiltersAudio);
    }

    /// <summary>
    /// Suzgec istenince kopyalanabilir aac kopyalanmaz, kodlanir ve zinciri <c>-filter:a</c>'ya yazar.
    /// Olumsuz kontrol: istek yokken ayni iz kopyalanir.
    /// </summary>
    [Fact]
    public void SuzgecKopyayiEngelliyor()
    {
        var suzgecli = Karar(new StreamRequest(AudioLoudnorm: true));
        var duz = Karar(StreamRequest.Default);

        Assert.False(suzgecli.Audio[0].Copies);
        Assert.Contains("loudnorm", Sonraki(suzgecli.OutputArguments(), "-filter:a"));
        Assert.True(duz.Audio[0].Copies);
        Assert.DoesNotContain("-filter:a", duz.OutputArguments());
    }

    /// <summary>Kopya zorunluysa (<c>copy</c>) suzgec kurulamaz: iz degismeden gecer, not dusulur.</summary>
    [Fact]
    public void KopyaZorunlukenNotDusuyor()
    {
        var plan = Karar(new StreamRequest(AudioGainDb: 4), "copy");

        Assert.True(plan.Audio[0].Copies);
        Assert.Contains(StreamNote.AudioFilterSkippedOnCopy, plan.Notes);
        Assert.DoesNotContain("-filter:a", plan.OutputArguments());
        Assert.DoesNotContain(StreamNote.AudioFilterSkippedOnCopy, Karar(StreamRequest.Default, "copy").Notes);
    }

    /// <summary>Plan istegi tasiyor: kap degisince <see cref="StreamMapping.ForOutput"/> suzgeci kaybetmiyor.</summary>
    [Fact]
    public void KapDegisinceSuzgecKorunuyor()
    {
        var info = Kaynak(600, Video, Ses);
        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 100, AudioLoudnorm = true, AudioGainDb = -3 });

        Assert.True(plan.Streams!.Request.AudioLoudnorm);
        Assert.Equal(-3, plan.Streams.Request.AudioGainDb);
        var mkv = StreamMapping.ForOutput(info, plan, "cikti.mkv");
        Assert.Contains("volume=-3dB", mkv.Audio[0].Filter);
    }

    /// <summary>CLI: kazanc araligin disinda <c>error.bad-gain</c>; sinirin kendisi gecer.</summary>
    [Theory]
    [InlineData("25", false)]
    [InlineData("-21", false)]
    [InlineData("yuksek", false)]
    [InlineData("20", true)]
    [InlineData("-20", true)]
    public void CliKazancSiniri(string deger, bool gecerli)
    {
        var parsed = CliParser.Parse(new[] { "plan", "a.mp4", "--hedef", "25", "--ses-kazanc", deger, "--ses-normal" });

        Assert.Equal(gecerli, parsed.Ok);
        if (gecerli)
        {
            var options = parsed.Request!.ToPlanOptions(10);
            Assert.True(options.AudioLoudnorm);
            Assert.Equal(double.Parse(deger, System.Globalization.CultureInfo.InvariantCulture), options.AudioGainDb);
        }
        else Assert.Equal("error.bad-gain", parsed.ErrorKey);
    }

    /// <summary>
    /// Canli kol: -10 dB kazanc ortalama duzeyi suzgecsiz ciktiya gore ~10 dB dusuruyor;
    /// ayni -10 dB'nin ustune loudnorm tumlesik yuksekligi -24 LUFS'a ±2 geri cekiyor; loudnorm'suz
    /// -10 dB cikti oradan uzak (olumsuz kontrol).
    /// </summary>
    [Fact]
    public async Task CanliSeviye()
    {
        var kaynak = await SesliAsync("ses-kaynak.mkv", 48000, 1);

        await KodlaAsync(kaynak, Yol("ses-duz.mp4"), _ => { });
        await KodlaAsync(kaynak, Yol("ses-kazanc.mp4"), options => options.AudioGainDb = -10);
        await KodlaAsync(kaynak, Yol("ses-normal.mp4"), options =>
        {
            options.AudioGainDb = -10;
            options.AudioLoudnorm = true;
        });

        var duz = await OrtalamaDuzeyAsync(Yol("ses-duz.mp4"));
        var kazanc = await OrtalamaDuzeyAsync(Yol("ses-kazanc.mp4"));
        Assert.InRange(duz - kazanc, 9, 11);

        var normal = await YukseklikAsync(Yol("ses-normal.mp4"));
        var kazancYukseklik = await YukseklikAsync(Yol("ses-kazanc.mp4"));
        Assert.InRange(normal, -26, -22);
        Assert.True(Math.Abs(kazancYukseklik - -24) > 4, $"loudnorm'suz cikti {kazancYukseklik} LUFS");

        Kapat("ses-*");
    }
}
