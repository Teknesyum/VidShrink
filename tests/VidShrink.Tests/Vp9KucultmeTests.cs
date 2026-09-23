using System.Text.Json;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// B3 kalani: libvpx-vp9 kucultme yolunda. Kilitli vp9 plani WebM kabina iner, ses opus olur,
/// hedef boyut iki gecisli VBR ile tutulur (<c>-b:v</c>, <c>-pass 1/2</c>); hiz <c>-deadline good
/// -cpu-used N -row-mt 1</c> ile yazilir, <c>-preset</c> yazilmaz. WebM'in tasiyamadigi iz
/// (resim altyazi, kapak, ek) plandan duser ve <see cref="StreamNote.WebmStreamDropped"/> notu birakir.
/// Canli kol ≤3 sn 320x240 lavfi, <c>-threads 2</c>; kanit <c>.calisma/b1-kalan/</c>, test siler.
/// </summary>
public sealed class Vp9KucultmeTests
{
    private const string Vp9 = "libvpx-vp9";

    private static MediaInfo Kaynak10Dk(params SourceStream[] ek)
    {
        var streams = new List<SourceStream> { Video, new(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000, SampleRate: 48000) };
        streams.AddRange(ek);
        return Kaynak(600, streams.ToArray());
    }

    private static EncodePlan Plan(MediaInfo info, Action<PlanOptions>? ayar = null)
    {
        var options = new PlanOptions { TargetMb = 50, LockedCodec = Vp9 };
        ayar?.Invoke(options);
        return PlanCalculator.Build(info, options);
    }

    private static StreamPlan Karar(MediaInfo info, string? sesKodegi = null)
        => StreamMapping.Decide(info, StreamRequest.Default, OutputContainer.WebM, 128, null, sesKodegi, true, 50);

    /// <summary>Plan ayristiricisi vp9'u kabul eder; tanimayan ad (vp8) hala reddedilir.</summary>
    [Theory]
    [InlineData(Vp9, true)]
    [InlineData("libvpx", false)]
    public void PlanAyristiricisiVp9uKabulEder(string kodek, bool kabul)
    {
        var json = JsonSerializer.Serialize(new
        {
            codec = kodek, mode = "2pass", videoBitrateK = 1200, crf = (int?)null, audioCodec = "libopus",
            audioBitrateK = 96, width = 1920, height = 1080, fps = 24.0, preset = "4", extraArgs = Array.Empty<string>(), reason = "test"
        });
        var sonuc = PlanParser.Parse(json, Kaynak10Dk(), new PlanOptions());
        Assert.Equal(kabul, sonuc.Ok);
    }

    /// <summary>Kilitli vp9 plani: WebM kabi, libopus ses, iki gecis; x264 kilidi mp4'te kalir (olumsuz kontrol).</summary>
    [Fact]
    public void KilitliVp9WebmVeIkiGecisVerir()
    {
        var plan = Plan(Kaynak10Dk());

        Assert.Equal(Vp9, plan.Codec);
        Assert.Equal(EncodeMode.TwoPass, plan.ModeEnum);
        Assert.Null(plan.Crf);
        Assert.Equal(OutputContainer.WebM, plan.Streams!.Container);
        Assert.Equal("webm", plan.Streams.Extension.TrimStart('.'));
        Assert.Equal("libopus", Assert.Single(plan.Streams.Audio).Codec);
        Assert.Contains(StreamNote.WebmAudioOpus, plan.Streams.Notes);

        var x264 = Plan(Kaynak10Dk(), options => options.LockedCodec = "libx264");
        Assert.Equal(OutputContainer.Mp4, x264.Streams!.Container);
        Assert.DoesNotContain(StreamNote.WebmAudioOpus, x264.Streams.Notes);
    }

    /// <summary>
    /// Iki gecisin argumanlari: ikisinde de <c>-b:v</c>, <c>-pass N</c>, <c>-deadline good</c>,
    /// <c>-cpu-used</c>, <c>-row-mt 1</c>; <c>-preset</c> yok. Birinci gecis <c>-f null</c>'a,
    /// ikinci gecis <c>-c:a libopus</c> ile .webm'e yazar, <c>+faststart</c> yazmaz.
    /// </summary>
    [Fact]
    public void IkiGecisArgumanlari()
    {
        var info = Kaynak10Dk();
        var plan = Plan(info);
        var bir = FfmpegArguments.Build(info, plan, "cikti.webm", 1, "log");
        var iki = FfmpegArguments.Build(info, plan, "cikti.webm", 2, "log");

        foreach (var (args, gecis) in new[] { (bir, "1"), (iki, "2") })
        {
            Assert.Equal(Vp9, Sonraki(args, "-c:v"));
            Assert.Equal($"{plan.VideoBitrateK}k", Sonraki(args, "-b:v"));
            Assert.Equal(gecis, Sonraki(args, "-pass"));
            Assert.Equal("log", Sonraki(args, "-passlogfile"));
            Assert.Equal("good", Sonraki(args, "-deadline"));
            Assert.Equal("1", Sonraki(args, "-row-mt"));
            Assert.True(int.TryParse(Sonraki(args, "-cpu-used"), out var hiz) && hiz is >= 0 and <= 8);
            Assert.DoesNotContain("-preset", args);
            Assert.DoesNotContain("-crf", args);
        }

        Assert.Equal("null", Sonraki(bir, "-f"));
        Assert.DoesNotContain("-c:a", bir);
        Assert.Equal("libopus", Sonraki(iki, "-c:a"));
        Assert.DoesNotContain("+faststart", iki);
        Assert.Equal("cikti.webm", iki[^1]);
    }

    /// <summary>Kalite kilidiyle vp9 CRF kolunda: <c>-crf N -b:v 0</c>, tavan yazilmaz. x264 hala <c>-preset</c> alir.</summary>
    [Fact]
    public void KaliteKilidiVeX264OlumsuzKontrol()
    {
        var info = Kaynak10Dk();
        var crf = Plan(info, options => { options.LockedMode = EncodeMode.Crf; options.LockedCrf = 33; });
        var args = FfmpegArguments.Build(info, crf, "cikti.webm", 0, null);
        Assert.Equal(EncodeMode.Crf, crf.ModeEnum);
        Assert.Equal("33", Sonraki(args, "-crf"));
        Assert.Equal("0", Sonraki(args, "-b:v"));
        Assert.DoesNotContain("-maxrate", args);
        Assert.DoesNotContain("-preset", args);

        var x264 = Plan(info, options => options.LockedCodec = "libx264");
        var x264Args = FfmpegArguments.Build(info, x264, "cikti.mp4", 2, "log");
        Assert.Contains("-preset", x264Args);
        Assert.DoesNotContain("-deadline", x264Args);
        Assert.DoesNotContain("-row-mt", x264Args);
    }

    /// <summary>
    /// Bol hedefte motor CRF'e gider; vp9'da CRF olceklenmedigi icin iki gecise cevrilir ve bu
    /// gerekce satiri yalniz Ingilizce metin olarak kalmaz, <see cref="ReasonCode.Vp9CrfUnmeasuredTwoPass"/>
    /// kodunu da birakir, anahtari 42 dilde. Kilitli CRF ve x264 ayni hedefte kodu birakmaz (olumsuz kontrol).
    /// </summary>
    [Fact]
    public void CrfdenIkiGeciseDonusGerekceKoduBirakir()
    {
        var info = Kaynak(60, Video, new SourceStream(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000, SampleRate: 48000));
        static bool Birakti(EncodePlan p) => p.ReasonCodes.Any(n => n.Code == ReasonCode.Vp9CrfUnmeasuredTwoPass);

        var bol = Plan(info, options => { options.TargetMb = 300; options.FillPolicy = FillPolicy.QualityCeiling; });
        Assert.Equal(EncodeMode.TwoPass, bol.ModeEnum);
        Assert.True(Birakti(bol), bol.Reason);

        var x264 = Plan(info, options => { options.TargetMb = 300; options.FillPolicy = FillPolicy.QualityCeiling; options.LockedCodec = "libx264"; });
        Assert.Equal(EncodeMode.Crf, x264.ModeEnum);
        Assert.False(Birakti(x264));

        var kilitli = Plan(info, options => { options.TargetMb = 300; options.FillPolicy = FillPolicy.QualityCeiling; options.LockedMode = EncodeMode.Crf; options.LockedCrf = 33; });
        Assert.Equal(EncodeMode.Crf, kilitli.ModeEnum);
        Assert.False(Birakti(kilitli));

        Assert.Equal(42, Locales.Languages.Count());
        foreach (var dil in Locales.Languages)
            Assert.False(string.IsNullOrWhiteSpace(Locales.Values(dil).FirstOrDefault(p => p.Key == "main.reason.vp9-crf-unmeasured").Value), dil);
    }

    /// <summary>
    /// cpu-used: varsayilan (Quality) 1, Hizli 4 — <c>docs/olcumler/vp9-cpu-used-crf.md</c>
    /// (kosum 35795367223). Ikisi de <c>FfmpegArguments.DefaultPreset("libvpx-vp9")</c>'un
    /// (1) degil, cunku Hizli olcumde 4'ten hicbir hucrede hizli bulunmadi, 4'te kaldi.
    /// </summary>
    [Fact]
    public void CpuUsedVarsayilanBirHizliDort()
    {
        var quality = Plan(Kaynak10Dk());
        var fast = Plan(Kaynak10Dk(), options => options.SpeedMode = SpeedMode.Fast);

        Assert.Equal("1", quality.Preset);
        Assert.Equal("1", FfmpegArguments.DefaultPreset(Vp9));
        Assert.Equal("4", fast.Preset);
    }

    /// <summary>WebM'de aac kopyalanamaz, opus'a kodlanir ve not duser; opus kopyasi kopya kalir, not dusmez.</summary>
    [Fact]
    public void WebmSesiOpusOluyor()
    {
        var aac = Karar(Kaynak10Dk(), "copy");
        Assert.Equal("libopus", Assert.Single(aac.Audio).Codec);
        Assert.Contains(StreamNote.WebmAudioOpus, aac.Notes);

        var opusKaynak = Kaynak(600, Video, new SourceStream(1, StreamKind.Audio, "opus", "eng", Channels: 2, BitrateBps: 96_000, SampleRate: 48000));
        var opus = Karar(opusKaynak, "copy");
        Assert.Equal("copy", Assert.Single(opus.Audio).Codec);
        Assert.DoesNotContain(StreamNote.WebmAudioOpus, opus.Notes);

        var istenen = Karar(Kaynak10Dk(), "aac");
        Assert.Equal("libopus", Assert.Single(istenen.Audio).Codec);
        Assert.Contains(StreamNote.WebmAudioOpus, istenen.Notes);
    }

    /// <summary>
    /// WebM resim altyazi, kapak ve ek tasimaz: iz plandan duser, <see cref="StreamNote.WebmStreamDropped"/>
    /// notu kalir. Metin altyazi webvtt olarak gecer ve not birakmaz (olumsuz kontrol).
    /// </summary>
    [Fact]
    public void WebmTasimadigiIzNotBirakir()
    {
        var pgs = Karar(Kaynak10Dk(new SourceStream(2, StreamKind.Subtitle, "hdmv_pgs_subtitle", "eng")));
        Assert.Empty(pgs.Subtitles);
        Assert.Contains(StreamNote.WebmStreamDropped, pgs.Notes);

        var kapak = Karar(Kaynak10Dk(new SourceStream(2, StreamKind.Video, "png", IsAttachedPicture: true, Bytes: 4000)));
        Assert.Null(kapak.CoverMap);
        Assert.Contains(StreamNote.WebmStreamDropped, kapak.Notes);

        var ek = Karar(Kaynak10Dk(new SourceStream(2, StreamKind.Attachment, "ttf", Bytes: 4000)));
        Assert.Empty(ek.Attachments);
        Assert.Contains(StreamNote.WebmStreamDropped, ek.Notes);

        var metin = Karar(Kaynak10Dk(new SourceStream(2, StreamKind.Subtitle, "subrip", "eng")));
        Assert.Equal("webvtt", Assert.Single(metin.Subtitles).Codec);
        Assert.DoesNotContain(StreamNote.WebmStreamDropped, metin.Notes);

        Assert.Equal("webm-audio-opus", StreamNotes.Slug(StreamNote.WebmAudioOpus));
        Assert.Equal("webm-stream-dropped", StreamNotes.Slug(StreamNote.WebmStreamDropped));
    }

    /// <summary>CLI <c>--kodek vp9</c> kilidi plana indirir; kodeksiz koşumda kilit yok (olumsuz kontrol).</summary>
    [Theory]
    [InlineData("vp9", Vp9)]
    [InlineData("libvpx-vp9", Vp9)]
    [InlineData("auto", null)]
    public void CliVp9KilidiPlanaIniyor(string kodek, string? beklenen)
    {
        var parsed = CliParser.Parse(new[] { "plan", "a.mp4", "--hedef", "25", "--kodek", kodek });
        Assert.True(parsed.Ok, parsed.ErrorKey);
        Assert.Equal(beklenen, parsed.Request!.ToPlanOptions(10).LockedCodec);
    }

    /// <summary>Ön ayar dosyasi yalniz kilitlenebilir kodegi kabul eder; <c>libvpx</c> (vp8) reddedilir.</summary>
    [Fact]
    public void OnAyarKilitliKodegiDogrular()
    {
        var dosya = PresetLibrary.Serialize(new[]
        {
            new PresetProfile { Id = "v", Name = "v", Kind = PresetKind.User, SizeCapped = true, TargetMb = 8, LockedCodec = Vp9 }
        });

        Assert.Equal(Vp9, Assert.Single(PresetLibrary.Parse(dosya)).LockedCodec);
        var hata = Assert.Throws<PresetFileException>(() => PresetLibrary.Parse(dosya.Replace(Vp9, "libvpx", StringComparison.Ordinal)));
        Assert.Equal(PresetFileError.InvalidValue, hata.Error);
    }

    /// <summary>
    /// Canli kol: 3 sn 320x240 kaynak urunun plan ve arguman zincirinden iki gecisle gecer
    /// (<c>-threads 2</c>), ffprobe vp9 + opus okur ve kap WebM'dir. ffmpeg yoksa atlanir.
    /// </summary>
    [FfmpegAvailableFact]
    public async Task CanliIkiGecisWebm()
    {
        var kaynak = await SesliAsync("vp9-kaynak.mkv", 48000, 2);
        var info = await FfprobeClient.ProbeAsync(kaynak);
        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 0.4, LockedCodec = Vp9 });
        Assert.Equal(EncodeMode.TwoPass, plan.ModeEnum);
        var cikti = Yol("vp9-cikti." + plan.Streams!.Extension.TrimStart('.'));
        var log = Yol("vp9-pass");

        foreach (var gecis in new[] { 1, 2 })
        {
            var args = FfmpegArguments.Build(info, plan, cikti, gecis, log).ToList();
            args.InsertRange(args.IndexOf("-c:v") + 2, new[] { "-threads", "2" });
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, args);
        }

        var akislar = await AkislarAsync(cikti);
        Assert.Equal("vp9", Alan(akislar.Single(s => Alan(s, "codec_type") == "video"), "codec_name"));
        Assert.Equal("opus", Alan(akislar.Single(s => Alan(s, "codec_type") == "audio"), "codec_name"));
        var bicim = await AkisGirdisi.RunAsync(ToolLocator.Ffprobe, new[] { "-v", "error", "-show_entries", "format=format_name", "-of", "csv=p=0", cikti });
        Assert.Contains("webm", bicim.Out);
        Assert.True(new FileInfo(cikti).Length > 0);

        Kapat("vp9-*");
    }
}
