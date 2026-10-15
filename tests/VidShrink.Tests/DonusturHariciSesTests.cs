using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Dönüştür'de harici ses dosyası (DON-P7). Dosya ana girdiden sonraki ilk <c>-i</c>'dir ve <c>1:a:0</c> ile
/// açıkça eşlenir; "yerine koy" kaynağın sesini bırakır, "ekle" kaynağın sesinin yanına koyar. Video
/// kopyalanabiliyorsa kopyalanır; kopya kipinde kabın almadığı dış ses yalnız kendi izinde yeniden kodlanır.
/// </summary>
public sealed class DonusturHariciSesTests
{
    private static readonly SourceStream Mono = new(1, StreamKind.Audio, "aac", "eng", Channels: 1, BitrateBps: 64_000);
    private static readonly SourceStream Stereo = new(2, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000);
    private static readonly SourceStream Opus = new(1, StreamKind.Audio, "opus", "eng", Channels: 2, BitrateBps: 96_000);

    private const string Muzik = @"C:\Kayitlar\muzik.m4a";

    private static MediaInfo Ornek(params SourceStream[] streams)
        => Kaynak(62, streams) with { FilePath = @"C:\Kayitlar\gezi.mkv" };

    private static List<string> Kur(MediaInfo info, ConversionPlan plan)
        => ConversionArguments.Build(info, plan, "cikti." + plan.Container).ToList();

    private static List<string> Eslemeler(IReadOnlyList<string> args)
        => args.Select((arg, i) => (arg, i)).Where(x => x.arg == "-map" && x.i + 1 < args.Count).Select(x => args[x.i + 1]).ToList();

    private static List<string> Girdiler(IReadOnlyList<string> args)
        => args.Select((arg, i) => (arg, i)).Where(x => x.arg == "-i" && x.i + 1 < args.Count).Select(x => args[x.i + 1]).ToList();

    /// <summary>Yerine koy: ikinci <c>-i</c> dış dosya, kaynağın sesi eşlenmez, video kopyası korunur.</summary>
    [Fact]
    public void YerineKoyKaynakSesiniBirakir()
    {
        var info = Ornek(Video, Mono, Stereo);
        var plan = new ConversionPlan { Container = "mp4", VideoCodec = "copy", AudioCodec = "copy", ExternalAudio = new ExternalAudio(Muzik, null, Replace: true) };
        var args = Kur(info, plan);

        Assert.Equal(new[] { @"C:\Kayitlar\gezi.mkv", Muzik }, Girdiler(args));
        Assert.Equal(new[] { "0:0", "1:a:0" }, Eslemeler(args));
        Assert.Equal("copy", Sonraki(args, "-c:v"));
        Assert.Equal("copy", Sonraki(args, "-c:a"));
        Assert.DoesNotContain(args, arg => arg.StartsWith("-c:a:", StringComparison.Ordinal));
        Assert.Empty(ConversionArguments.SourceAudio(info, plan));

        var kodla = Kur(info, new ConversionPlan { Container = "mp4", ExternalAudio = new ExternalAudio(Muzik, null, Replace: true) });
        Assert.Equal(new[] { "0:0", "1:a:0" }, Eslemeler(kodla));
        Assert.Equal("libx264", Sonraki(kodla, "-c:v"));
        Assert.Equal("aac", Sonraki(kodla, "-c:a"));
    }

    /// <summary>Ekle: kaynağın sesi (ya da seçilen izler) önce, dış iz en sonda eşlenir.</summary>
    [Fact]
    public void EkleKaynakSesininYaninaKoyar()
    {
        var info = Ornek(Video, Mono, Stereo);
        var dis = new ExternalAudio(Muzik, "tur");

        var varsayilan = Kur(info, new ConversionPlan { Container = "mp4", ExternalAudio = dis });
        Assert.Equal(new[] { "0:0", "0:2", "1:a:0" }, Eslemeler(varsayilan));
        Assert.Equal("language=tur", Sonraki(varsayilan, "-metadata:s:a:1"));

        var secili = Kur(info, new ConversionPlan { Container = "mkv", AudioTracks = new[] { 0, 1 }, ExternalAudio = dis });
        Assert.Equal(new[] { "0:0", "0:1", "0:2", "1:a:0" }, Eslemeler(secili));
        Assert.Equal("language=tur", Sonraki(secili, "-metadata:s:a:2"));

        var bos = Kur(info, new ConversionPlan { Container = "mp4", AudioTracks = Array.Empty<int>(), ExternalAudio = dis });
        Assert.Equal(new[] { "0:0", "1:a:0" }, Eslemeler(bos));
        Assert.DoesNotContain("-an", bos);
    }

    /// <summary>"Kısa olanda bitir" yalnız dış ses varken <c>-shortest</c> yazar.</summary>
    [Fact]
    public void KisaOlandaBitir()
    {
        var info = Ornek(Video, Stereo);
        var dis = new ExternalAudio(Muzik, null, Replace: true);

        Assert.Contains("-shortest", Kur(info, new ConversionPlan { Container = "mkv", ExternalAudio = dis, EndAtShortest = true }));
        Assert.DoesNotContain("-shortest", Kur(info, new ConversionPlan { Container = "mkv", ExternalAudio = dis }));
        Assert.DoesNotContain("-shortest", Kur(info, new ConversionPlan { Container = "mkv", EndAtShortest = true }));
    }

    /// <summary>
    /// Kopya kipinde kabın almadığı dış ses kendi izinde yeniden kodlanır, kaynağın izi kopya kalır. Kodek
    /// uzantıdan çıkar; kesin olmayan uzantıda (wav) yalnız her şeyi alan MKV kopyalar.
    /// </summary>
    [Fact]
    public void KabAlmayincaYenidenKodlar()
    {
        var webm = Kur(Ornek(Video, Opus), new ConversionPlan
        {
            Container = "webm", VideoCodec = "libvpx-vp9", AudioCodec = "copy", ExternalAudio = new ExternalAudio(@"C:\Kayitlar\muzik.mp3", null)
        });
        Assert.Equal("copy", Sonraki(webm, "-c:a"));
        Assert.Equal("libopus", Sonraki(webm, "-c:a:1"));
        Assert.Equal("128k", Sonraki(webm, "-b:a:1"));
        Assert.True(webm.IndexOf("-c:a") < webm.IndexOf("-c:a:1"));

        var mp4 = Kur(Ornek(Video, Stereo), new ConversionPlan
        {
            Container = "mp4", VideoCodec = "copy", AudioCodec = "copy", ExternalAudio = new ExternalAudio(@"C:\Kayitlar\anlatim.wav", null, Replace: true)
        });
        Assert.Equal("aac", Sonraki(mp4, "-c:a:0"));
        Assert.Equal("copy", Sonraki(mp4, "-c:v"));

        var uyan = Kur(Ornek(Video, Stereo), new ConversionPlan
        {
            Container = "mp4", VideoCodec = "copy", AudioCodec = "copy", ExternalAudio = new ExternalAudio(@"C:\Kayitlar\muzik.mp3", null)
        });
        Assert.DoesNotContain(uyan, arg => arg.StartsWith("-c:a:", StringComparison.Ordinal));

        var mkv = Kur(Ornek(Video, Stereo), new ConversionPlan
        {
            Container = "mkv", VideoCodec = "copy", AudioCodec = "copy", ExternalAudio = new ExternalAudio(@"C:\Kayitlar\anlatim.wav", null)
        });
        Assert.DoesNotContain(mkv, arg => arg.StartsWith("-c:a:", StringComparison.Ordinal));
    }

    /// <summary>Yerine koyulan kaynağın sesi kopya denetimine girmez; sessiz kaynağa dış ses eklenebilir.</summary>
    [Fact]
    public void DogrulamaDisSesiSayar()
    {
        var dis = new ExternalAudio(Muzik, null, Replace: true);
        Assert.Contains("The MP4 container does not support copying the source vorbis audio stream.",
            ConversionArguments.Validate(Ornek(Video, Opus with { Codec = "vorbis" }), new ConversionPlan { Container = "mp4", AudioCodec = "copy" }));
        Assert.Empty(ConversionArguments.Validate(Ornek(Video, Opus with { Codec = "vorbis" }), new ConversionPlan { Container = "mp4", AudioCodec = "copy", ExternalAudio = dis }));

        var sessiz = Ornek(Video);
        Assert.Empty(ConversionArguments.Validate(sessiz, new ConversionPlan { Container = "mp4", AudioCodec = "copy", ExternalAudio = dis with { Replace = false } }));
        Assert.Equal(new[] { "0:0", "1:a:0" }, Eslemeler(Kur(sessiz, new ConversionPlan { Container = "mp4", ExternalAudio = dis with { Replace = false } })));
        Assert.Empty(ConversionArguments.Validate(sessiz, new ConversionPlan { Container = "m4a", ExternalAudio = dis }));
    }

    /// <summary>Tek ses taşıyan kapta "ekle" reddedilir, "yerine koy" yalnız dış izi yazar.</summary>
    [Fact]
    public void TekSesliKap()
    {
        var info = Ornek(Video, Stereo);
        Assert.Contains("The MP3 container carries only one audio track.",
            ConversionArguments.Validate(info, new ConversionPlan { Container = "mp3", AudioCodec = "libmp3lame", ExternalAudio = new ExternalAudio(Muzik, null) }));

        var yerine = Kur(info, new ConversionPlan { Container = "mp3", AudioCodec = "libmp3lame", ExternalAudio = new ExternalAudio(Muzik, null, Replace: true) });
        Assert.Equal(new[] { "1:a:0" }, Eslemeler(yerine));
        Assert.Contains("-vn", yerine);
    }

    /// <summary>Dış altyazı girdileri dış sesten sonra gelir, eşleme sırası bir kayar; kırpmada dış ses aynı yerden başlar.</summary>
    [Fact]
    public void GirdiSirasiVeKirpma()
    {
        var info = Ornek(Video, Stereo);
        var srt = new ExternalSubtitle(@"C:\Kayitlar\gezi.tr.srt", "subrip", "tur", 900);
        var args = Kur(info, new ConversionPlan
        {
            Container = "mkv", ExternalSubtitles = new[] { srt }, ExternalAudio = new ExternalAudio(Muzik, null), Start = TimeSpan.FromSeconds(10)
        });

        Assert.Equal(new[] { @"C:\Kayitlar\gezi.mkv", Muzik, srt.Path }, Girdiler(args));
        Assert.Equal(new[] { "0:0", "0:2", "1:a:0", "2:0" }, Eslemeler(args));
        Assert.Equal("00:00:10.000", args[args.IndexOf(Muzik) - 2]);
        Assert.Equal("-ss", args[args.IndexOf(Muzik) - 3]);
    }

    /// <summary>
    /// Canlı: 1 sn video + 2 sn dış ses. Yerine koyda tek ses, kısa olanda bitirince süre videoyla biter; eklemede
    /// iki ses. Video kopyalanır.
    /// </summary>
    [FfmpegFact]
    public async Task CanliYerineKoyVeEkle()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "donustur-harici-ses", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        var kaynak = Path.Combine(klasor, "kaynak.mkv");
        var ses = Path.Combine(klasor, "anlatim.wav");
        var yerine = Path.Combine(klasor, "yerine.mkv");
        var ekle = Path.Combine(klasor, "ekle.mkv");
        try
        {
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
            {
                "-hide_banner", "-y",
                "-f", "lavfi", "-i", "color=c=black:size=160x120:rate=10:duration=1",
                "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=1",
                "-c:v", "libx264", "-preset", "ultrafast", "-c:a", "aac", "-threads", "2", kaynak
            });
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
            {
                "-hide_banner", "-y", "-f", "lavfi", "-i", "sine=frequency=660:sample_rate=48000:duration=2", "-threads", "2", ses
            });
            var info = await FfprobeClient.ProbeAsync(kaynak);

            foreach (var (cikti, degistir) in new[] { (yerine, true), (ekle, false) })
            {
                var plan = new ConversionPlan
                {
                    Container = "mkv", VideoCodec = "copy", AudioCodec = "copy", EndAtShortest = degistir,
                    ExternalAudio = new ExternalAudio(ses, null, degistir)
                };
                var args = ConversionArguments.Build(info, plan, cikti).ToList();
                args.InsertRange(args.Count - 1, new[] { "-threads", "2" });
                await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, args);
            }

            var yerineSes = (await AkislarAsync(yerine)).Where(stream => Alan(stream, "codec_type") == "audio").ToList();
            Assert.Equal(new[] { "pcm_s16le" }, yerineSes.Select(stream => Alan(stream, "codec_name")));
            Assert.InRange((await FfprobeClient.ProbeAsync(yerine)).DurationSeconds, 0.5, 1.5);

            var ekleSes = (await AkislarAsync(ekle)).Where(stream => Alan(stream, "codec_type") == "audio").ToList();
            Assert.Equal(new[] { "aac", "pcm_s16le" }, ekleSes.Select(stream => Alan(stream, "codec_name")));
            Assert.Equal(new[] { "h264" }, (await AkislarAsync(ekle)).Where(stream => Alan(stream, "codec_type") == "video").Select(stream => Alan(stream, "codec_name")));
        }
        finally
        {
            try { Directory.Delete(klasor, true); }
            catch (IOException) { }
        }
    }
}
