using System.Text;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Dönüştür'de altyazı izleri (DON-P2): altyazı taşıyan kapta komut açık <c>-map</c> ve <c>-c:s</c>
/// yazar, kural küçültmenin akış planından (<see cref="StreamMapping.SubtitleTracks"/>) gelir. Dış
/// .srt/.ass ek girdi olur; çıkarma ayrı kısa komuttur (<see cref="SubtitleExtraction"/>).
/// </summary>
public sealed class DonusturAltyaziTests
{
    private static readonly SourceStream Tur = new(1, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000);
    private static readonly SourceStream Srt = new(2, StreamKind.Subtitle, "subrip", "eng");
    private static readonly SourceStream Pgs = new(3, StreamKind.Subtitle, "hdmv_pgs_subtitle", "deu");

    private static MediaInfo Ornek(params SourceStream[] streams)
        => Kaynak(62, streams) with { FilePath = @"C:\Kayitlar\gezi.mkv" };

    private static ConversionPlan Plan(string kap) => kap switch
    {
        "webm" => new ConversionPlan { Container = kap, VideoCodec = "libvpx-vp9", AudioCodec = "libopus" },
        "avi" => new ConversionPlan { Container = kap, VideoCodec = "libx264", AudioCodec = "libmp3lame" },
        "mp3" => new ConversionPlan { Container = kap, AudioCodec = "libmp3lame" },
        _ => new ConversionPlan { Container = kap }
    };

    private static List<string> Kur(MediaInfo info, ConversionPlan plan, string cikti = "cikti")
        => ConversionArguments.Build(info, plan, cikti + "." + plan.Container, plan.Gif ? "palet.png" : null).ToList();

    private static List<string> Eslemeler(IReadOnlyList<string> args)
        => args.Select((arg, i) => (arg, i)).Where(x => x.arg == "-map" && x.i + 1 < args.Count).Select(x => args[x.i + 1]).ToList();

    /// <summary>Metin altyazı MP4'te mov_text'e, WebM'de webvtt'ye çevrilir, MKV'de kopyalanır; üçünde de izi açıkça eşlenir.</summary>
    [Theory]
    [InlineData("mp4", "mov_text")]
    [InlineData("mov", "mov_text")]
    [InlineData("mkv", "copy")]
    [InlineData("webm", "webvtt")]
    public void MetinAltyaziKabaGoreKodlaniyor(string kap, string kodek)
    {
        var args = Kur(Ornek(Video, Tur, Srt), Plan(kap));

        Assert.Equal(new[] { "0:0", "0:1", "0:2" }, Eslemeler(args));
        Assert.Equal(kodek, Sonraki(args, "-c:s"));
        Assert.True(args.IndexOf("-map") > args.LastIndexOf("-i"));
    }

    /// <summary>Resim altyazı MP4'te taşınamaz ve notla düşer; MKV'de kopyalanır; WebM'de düşer.</summary>
    [Fact]
    public void ResimAltyaziTasinamayanKaptaNotlaDusuyor()
    {
        var info = Ornek(Video, Tur, Srt, Pgs);

        var mp4Notlar = new List<StreamNote>();
        Assert.Equal(new[] { "0:2" }, ConversionArguments.Subtitles(info, Plan("mp4"), mp4Notlar).Select(track => track.Map));
        Assert.Contains(StreamNote.ImageSubtitleDropped, mp4Notlar);
        Assert.DoesNotContain("0:3", Eslemeler(Kur(info, Plan("mp4"))));

        var mkv = Kur(info, Plan("mkv"));
        Assert.Equal(new[] { "0:0", "0:1", "0:2", "0:3" }, Eslemeler(mkv));
        Assert.Equal("copy", Sonraki(mkv, "-c:s"));

        var webmNotlar = new List<StreamNote>();
        Assert.Equal(new[] { "0:2" }, ConversionArguments.Subtitles(info, Plan("webm"), webmNotlar).Select(track => track.Map));
        Assert.Contains(StreamNote.WebmStreamDropped, webmNotlar);
    }

    /// <summary>
    /// Dış .srt ana girdiden sonra, <c>-t</c>'den önce ek <c>-i</c> olur; kırpmada aynı <c>-ss</c> onun da
    /// önüne yazılır ve <c>1:0</c> olarak eşlenir. Kaba göre kodeği MP4'te mov_text, MKV'de kopyadır.
    /// </summary>
    [Theory]
    [InlineData("mp4", "mov_text")]
    [InlineData("mkv", "copy")]
    [InlineData("webm", "webvtt")]
    public void DisAltyaziEkGirdiOluyor(string kap, string kodek)
    {
        var dis = new ExternalSubtitle(@"C:\Kayitlar\gezi.tr.srt", "subrip", "tur", 900);
        var plan = Plan(kap);
        plan = new ConversionPlan
        {
            Container = plan.Container, VideoCodec = plan.VideoCodec, AudioCodec = plan.AudioCodec,
            Start = TimeSpan.FromSeconds(5), End = TimeSpan.FromSeconds(20), ExternalSubtitles = new[] { dis }
        };
        var args = Kur(Ornek(Video, Tur), plan);

        var disGirdi = args.IndexOf(dis.Path);
        Assert.Equal("-i", args[disGirdi - 1]);
        Assert.Equal("00:00:05.000", args[disGirdi - 2]);
        Assert.Equal("-ss", args[disGirdi - 3]);
        Assert.True(disGirdi > args.IndexOf(@"C:\Kayitlar\gezi.mkv"));
        Assert.True(disGirdi < args.IndexOf("-t"));
        Assert.Equal(new[] { "0:0", "0:1", "1:0" }, Eslemeler(args));
        Assert.Equal(kodek, Sonraki(args, "-c:s"));
        Assert.Equal("language=tur", Sonraki(args, "-metadata:s:s:0"));
    }

    /// <summary>
    /// Açık eşleme yazılınca ses seçimi değişmez: eşlenen ses ffmpeg'in <c>-map</c>'siz seçeceği izdir
    /// (<see cref="ConversionArguments.AutoAudio"/>) — varsayılan işaretli iz, yoksa en çok kanallı.
    /// </summary>
    [Fact]
    public void AcikEslemeSesSeciminiDegistirmiyor()
    {
        var mono = new SourceStream(1, StreamKind.Audio, "aac", "eng", Channels: 1, BitrateBps: 64_000);
        var stereo = new SourceStream(2, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000);
        var sub = Srt with { Index = 3 };

        var kanal = Ornek(Video, mono, stereo, sub);
        Assert.Equal(2, ConversionArguments.AutoAudio(kanal)!.Index);
        Assert.Equal(new[] { "0:0", "0:2", "0:3" }, Eslemeler(Kur(kanal, Plan("mp4"))));

        var varsayilan = Ornek(Video, mono with { IsDefault = true }, stereo, sub);
        Assert.Equal(1, ConversionArguments.AutoAudio(varsayilan)!.Index);
        Assert.Equal(new[] { "0:0", "0:1", "0:3" }, Eslemeler(Kur(varsayilan, Plan("mkv"))));
    }

    /// <summary>İz seçimi: boş liste hiçbir kaynak altyazıyı, sıra listesi yalnız seçilenleri eşler.</summary>
    [Fact]
    public void IzSecimiEslemeyiSuzuyor()
    {
        var ikinci = new SourceStream(3, StreamKind.Subtitle, "ass", "fra");
        var info = Ornek(Video, Tur, Srt, ikinci);

        Assert.Equal(new[] { "0:0", "0:1" }, Eslemeler(Kur(info, new ConversionPlan { Container = "mkv", SubtitleTracks = Array.Empty<int>() })));
        Assert.DoesNotContain("-c:s", Kur(info, new ConversionPlan { Container = "mkv", SubtitleTracks = Array.Empty<int>() }));
        Assert.Equal(new[] { "0:0", "0:1", "0:3" }, Eslemeler(Kur(info, new ConversionPlan { Container = "mkv", SubtitleTracks = new[] { 1 } })));
    }

    /// <summary>Altyazı taşımayan kapta ve altyazısız kaynakta komut eşleme yazmaz; ffmpeg'in seçimi korunur.</summary>
    [Theory]
    [InlineData("avi", true)]
    [InlineData("gif", true)]
    [InlineData("mp3", true)]
    [InlineData("mp4", false)]
    public void AltyaziTasimayanPlandaEslemeYok(string kap, bool altyazili)
    {
        var info = altyazili ? Ornek(Video, Tur, Srt) : Ornek(Video, Tur);
        var args = Kur(info, Plan(kap));

        Assert.DoesNotContain("-map", args);
        Assert.DoesNotContain("-c:s", args);
        Assert.False(ConversionArguments.MapsStreams(info, Plan(kap)));
    }

    /// <summary>Çıkarma tek izi yazar: mov_text srt'ye çevrilir, srt/ass kopyalanır, resim altyazı reddedilir.</summary>
    [Fact]
    public void CikarmaTekIziYaziyor()
    {
        var movText = new SourceStream(4, StreamKind.Subtitle, "mov_text", "eng");
        var info = Ornek(Video, Tur, Srt, Pgs, movText);

        Assert.Equal(new[] { "-hide_banner", "-y", "-i", @"C:\Kayitlar\gezi.mkv", "-map", "0:2", "-c:s", "copy", "a.srt" },
            SubtitleExtraction.Build(info, 0, "a.srt"));
        Assert.Equal(new[] { "-hide_banner", "-y", "-i", @"C:\Kayitlar\gezi.mkv", "-map", "0:4", "-c:s", "srt", "b.srt" },
            SubtitleExtraction.Build(info, 2, "b.srt"));
        Assert.Equal(".srt", SubtitleExtraction.Extension("mov_text"));
        Assert.Equal(".ass", SubtitleExtraction.Extension("ssa"));
        Assert.Null(SubtitleExtraction.Extension("hdmv_pgs_subtitle"));
        Assert.Throws<ArgumentException>(() => SubtitleExtraction.Build(info, 1, "c.srt"));
        Assert.Throws<ArgumentOutOfRangeException>(() => SubtitleExtraction.Build(info, 3, "d.srt"));
    }

    /// <summary>
    /// Canlı: iki sesli (mono eng, stereo tur) ve srt'li kaynak MP4'e dönüşür. Çıktının sesi ffmpeg'in
    /// eşlemesiz seçtiği sesle aynı dili taşır, altyazı mov_text olur; çıkarma komutu srt yazar.
    /// </summary>
    [FfmpegFact]
    public async Task CanliDonusumSesiKorurAltyaziyiTasir()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "donustur-altyazi");
        Directory.CreateDirectory(klasor);
        var srt = Path.Combine(klasor, "kaynak.srt");
        File.WriteAllText(srt, "1\n00:00:00,000 --> 00:00:02,000\nMERHABA\n\n", new UTF8Encoding(false));
        var kaynak = Path.Combine(klasor, "kaynak.mkv");
        var eslemesiz = Path.Combine(klasor, "eslemesiz.mkv");
        var cikti = Path.Combine(klasor, "cikti.mp4");
        var cikan = Path.Combine(klasor, "cikan.srt");
        try
        {
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
            {
                "-hide_banner", "-y",
                "-f", "lavfi", "-i", "color=c=black:size=160x120:rate=10:duration=2",
                "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=2",
                "-f", "lavfi", "-i", "sine=frequency=660:sample_rate=48000:duration=2",
                "-i", srt,
                "-map", "0", "-map", "1", "-map", "2", "-map", "3",
                "-c:v", "libx264", "-preset", "ultrafast", "-c:a", "aac", "-ac:a:0", "1", "-ac:a:1", "2", "-c:s", "srt",
                "-metadata:s:a:0", "language=eng", "-metadata:s:a:1", "language=tur", "-metadata:s:s:0", "language=eng",
                "-disposition:a:0", "0", "-disposition:a:1", "0", "-threads", "2", kaynak
            });
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[] { "-hide_banner", "-y", "-i", kaynak, "-c", "copy", "-sn", "-threads", "2", eslemesiz });

            var info = await FfprobeClient.ProbeAsync(kaynak);
            var args = ConversionArguments.Build(info, new ConversionPlan { Container = "mp4", VideoCodec = "libx264", Crf = 35 }, cikti).ToList();
            args.InsertRange(args.Count - 1, new[] { "-threads", "2", "-preset", "ultrafast" });
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, args);

            var beklenen = (await AkislarAsync(eslemesiz)).Single(stream => Alan(stream, "codec_type") == "audio");
            var akislar = await AkislarAsync(cikti);
            var ses = akislar.Single(stream => Alan(stream, "codec_type") == "audio");
            Assert.Equal(Etiket(beklenen, "language"), Etiket(ses, "language"));
            Assert.Equal("tur", Etiket(ses, "language"));
            Assert.Equal("mov_text", Alan(akislar.Single(stream => Alan(stream, "codec_type") == "subtitle"), "codec_name"));

            var cikar = SubtitleExtraction.Build(info, 0, cikan).ToList();
            cikar.InsertRange(cikar.Count - 1, new[] { "-threads", "2" });
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, cikar);
            Assert.Contains("MERHABA", File.ReadAllText(cikan));
        }
        finally
        {
            foreach (var dosya in new[] { srt, kaynak, eslemesiz, cikti, cikan })
                if (File.Exists(dosya)) File.Delete(dosya);
        }
    }
}
