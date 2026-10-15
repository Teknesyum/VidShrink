using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Dönüştür'de ses izi seçimi (DON-P4). Varsayılan bugünkü davranıştır: tek ses, ffmpeg'in <c>-map</c>'siz
/// seçeceği iz (<see cref="ConversionArguments.AutoAudio"/>). Seçim <see cref="ConversionPlan.AudioTracks"/>
/// ile ek olarak verilir; kaynak ses sırası, çıktıda kaynak sırasıyla eşlenir.
/// </summary>
public sealed class DonusturSesIziTests
{
    private static readonly SourceStream Mono = new(1, StreamKind.Audio, "aac", "eng", Channels: 1, BitrateBps: 64_000);
    private static readonly SourceStream Stereo = new(2, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000);
    private static readonly SourceStream Opus = new(2, StreamKind.Audio, "opus", "tur", Channels: 2, BitrateBps: 128_000);
    private static readonly SourceStream Srt = new(3, StreamKind.Subtitle, "subrip", "eng");

    private static MediaInfo Ornek(params SourceStream[] streams)
        => Kaynak(62, streams) with { FilePath = @"C:\Kayitlar\gezi.mkv" };

    private static List<string> Kur(MediaInfo info, ConversionPlan plan)
        => ConversionArguments.Build(info, plan, "cikti." + plan.Container).ToList();

    private static List<string> Eslemeler(IReadOnlyList<string> args)
        => args.Select((arg, i) => (arg, i)).Where(x => x.arg == "-map" && x.i + 1 < args.Count).Select(x => args[x.i + 1]).ToList();

    /// <summary>
    /// Varsayılan pimi: seçim verilmeyen planda komut bugünküyle aynıdır. Altyazısız MP4'te eşleme yok
    /// (ffmpeg en çok kanallı stereo izi seçer), altyazılı MKV'de yalnız o iz eşlenir.
    /// </summary>
    [Fact]
    public void VarsayilanTekSesFfmpeginSecimi()
    {
        var info = Ornek(Video, Mono, Stereo);
        var plan = new ConversionPlan { Container = "mp4" };

        Assert.Null(plan.AudioTracks);
        Assert.Empty(Eslemeler(Kur(info, plan)));
        Assert.False(ConversionArguments.MapsStreams(info, plan));
        Assert.Equal(new[] { 2 }, ConversionArguments.SourceAudio(info, plan).Select(stream => stream.Index));

        var altyazili = Ornek(Video, Mono, Stereo, Srt);
        Assert.Equal(new[] { "0:0", "0:2", "0:3" }, Eslemeler(Kur(altyazili, new ConversionPlan { Container = "mkv" })));
    }

    /// <summary>Seçilen izler kaynak sırasıyla eşlenir; tek seçilen iz ffmpeg'in seçmeyeceği iz olabilir.</summary>
    [Fact]
    public void SecilenIzlerEslenir()
    {
        var info = Ornek(Video, Mono, Stereo);

        var iki = Kur(info, new ConversionPlan { Container = "mp4", AudioTracks = new[] { 1, 0 } });
        Assert.Equal(new[] { "0:0", "0:1", "0:2" }, Eslemeler(iki));
        Assert.Equal("aac", Sonraki(iki, "-c:a"));

        Assert.Equal(new[] { "0:0", "0:1" }, Eslemeler(Kur(info, new ConversionPlan { Container = "mp4", AudioTracks = new[] { 0 } })));
        Assert.Equal(new[] { "0:0", "0:1", "0:2" },
            Eslemeler(Kur(info, new ConversionPlan { Container = "avi", VideoCodec = "libx264", AudioCodec = "libmp3lame", AudioTracks = new[] { 0, 1 } })));

        var altyazili = Ornek(Video, Mono, Stereo, Srt);
        Assert.Equal(new[] { "0:0", "0:1", "0:3" }, Eslemeler(Kur(altyazili, new ConversionPlan { Container = "mkv", AudioTracks = new[] { 0 } })));
    }

    /// <summary>Boş seçim sesi düşürür; ses taşımayan kapta seçim okunmaz.</summary>
    [Fact]
    public void BosSecimSesiDusurur()
    {
        var info = Ornek(Video, Mono, Stereo);
        var args = Kur(info, new ConversionPlan { Container = "mp4", AudioTracks = Array.Empty<int>() });

        Assert.Equal(new[] { "0:0" }, Eslemeler(args));
        Assert.Contains("-an", args);
        Assert.DoesNotContain("-c:a", args);

        var gif = ConversionArguments.Build(info, new ConversionPlan { Container = "gif", AudioTracks = new[] { 0, 1 } }, "c.gif", "p.png");
        Assert.DoesNotContain("-map", gif);
    }

    /// <summary>Kopya kipinde her seçilen izin kodeği kapla denetlenir; uyan iz geçer (olumsuz kontrol).</summary>
    [Fact]
    public void KopyadaKabinAlmadigiIzUyarir()
    {
        var info = Ornek(Video, Mono, Opus with { Codec = "vorbis" });
        var hatalar = ConversionArguments.Validate(info, new ConversionPlan { Container = "mp4", AudioCodec = "copy", AudioTracks = new[] { 0, 1 } });
        Assert.Contains("The MP4 container does not support copying the source vorbis audio stream.", hatalar);

        Assert.Empty(ConversionArguments.Validate(info, new ConversionPlan { Container = "mp4", AudioCodec = "copy", AudioTracks = new[] { 0 } }));
        Assert.Empty(ConversionArguments.Validate(info, new ConversionPlan { Container = "mkv", AudioCodec = "copy", AudioTracks = new[] { 0, 1 } }));
    }

    /// <summary>Tek ses taşıyan kapta iki iz reddedilir, tek iz o izi eşler; ses kabında boş seçim reddedilir.</summary>
    [Fact]
    public void TekSesliKapIkiIziReddeder()
    {
        var info = Ornek(Video, Mono, Stereo);
        var mp3 = new ConversionPlan { Container = "mp3", AudioCodec = "libmp3lame", AudioTracks = new[] { 0, 1 } };
        Assert.Contains("The MP3 container carries only one audio track.", ConversionArguments.Validate(info, mp3));

        var tek = Kur(info, new ConversionPlan { Container = "mp3", AudioCodec = "libmp3lame", AudioTracks = new[] { 0 } });
        Assert.Equal(new[] { "0:1" }, Eslemeler(tek));
        Assert.Contains("-vn", tek);

        Assert.Contains("No audio track is selected.",
            ConversionArguments.Validate(info, new ConversionPlan { Container = "wav", AudioCodec = "pcm_s16le", AudioTracks = Array.Empty<int>() }));
        Assert.Empty(ConversionArguments.Validate(info, new ConversionPlan { Container = "m4a", AudioTracks = new[] { 0, 1 } }));
    }

    /// <summary>Meta silmede her eşlenen sesin dili kendi çıktı sırasıyla yazılır.</summary>
    [Fact]
    public void MetaSilmeSecilenDilleriYazar()
    {
        var info = Ornek(Video, Mono, Stereo);
        var args = Kur(info, new ConversionPlan { Container = "mkv", DropMetadata = true, AudioTracks = new[] { 0, 1 } });

        Assert.Equal("language=eng", Sonraki(args, "-metadata:s:a:0"));
        Assert.Equal("language=tur", Sonraki(args, "-metadata:s:a:1"));

        var tek = Kur(info, new ConversionPlan { Container = "mkv", DropMetadata = true, AudioTracks = new[] { 0 } });
        Assert.Equal("language=eng", Sonraki(tek, "-metadata:s:a:0"));
        Assert.DoesNotContain("-metadata:s:a:1", tek);
    }

    /// <summary>Canlı: iki sesli MKV iki seçimle iki ses, varsayılanla yalnız ffmpeg'in seçtiği stereo izi taşır.</summary>
    [FfmpegFact]
    public async Task CanliIkiIzTasinir()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "donustur-ses-izi");
        Directory.CreateDirectory(klasor);
        var kaynak = Path.Combine(klasor, "kaynak.mkv");
        var iki = Path.Combine(klasor, "iki.mkv");
        var varsayilan = Path.Combine(klasor, "varsayilan.mkv");
        try
        {
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
            {
                "-hide_banner", "-y",
                "-f", "lavfi", "-i", "color=c=black:size=160x120:rate=10:duration=1",
                "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=1",
                "-f", "lavfi", "-i", "sine=frequency=660:sample_rate=48000:duration=1",
                "-map", "0", "-map", "1", "-map", "2",
                "-c:v", "libx264", "-preset", "ultrafast", "-c:a", "aac", "-ac:a:0", "1", "-ac:a:1", "2",
                "-metadata:s:a:0", "language=eng", "-metadata:s:a:1", "language=tur",
                "-disposition:a:0", "0", "-disposition:a:1", "0", "-threads", "2", kaynak
            });
            var info = await FfprobeClient.ProbeAsync(kaynak);

            foreach (var (cikti, secim) in new[] { (iki, (IReadOnlyList<int>?)new[] { 0, 1 }), (varsayilan, null) })
            {
                var args = ConversionArguments.Build(info, new ConversionPlan { Container = "mkv", AudioCodec = "copy", AudioTracks = secim }, cikti).ToList();
                args.InsertRange(args.Count - 1, new[] { "-threads", "2", "-preset", "ultrafast" });
                await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, args);
            }

            var ikiSes = (await AkislarAsync(iki)).Where(stream => Alan(stream, "codec_type") == "audio").ToList();
            Assert.Equal(new[] { "eng", "tur" }, ikiSes.Select(stream => Etiket(stream, "language")));
            Assert.Equal(new[] { "1", "2" }, ikiSes.Select(stream => Alan(stream, "channels")));

            var tekSes = (await AkislarAsync(varsayilan)).Where(stream => Alan(stream, "codec_type") == "audio").ToList();
            Assert.Equal(new[] { "tur" }, tekSes.Select(stream => Etiket(stream, "language")));
        }
        finally
        {
            foreach (var dosya in new[] { kaynak, iki, varsayilan })
                if (File.Exists(dosya)) File.Delete(dosya);
        }
    }

    /// <summary>
    /// Seçimsiz kopya kipi ffmpeg'in kopyalayacağı izi denetler, ilk ses izini değil. İlk iz aac (MP4 alır),
    /// varsayılan işaretli altı kanallı ikinci iz opus (MP4 almaz): ffmpeg opus'u seçer, doğrulama reddeder.
    /// Ters dizilimde (ilk iz opus, seçilen aac) hata yok.
    /// </summary>
    [Fact]
    public void SecimsizKopyaFfmpeginSececegiIziDenetler()
    {
        var ilkAac = new SourceStream(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000);
        var seciliOpus = new SourceStream(2, StreamKind.Audio, "vorbis", "tur", IsDefault: true, Channels: 6, BitrateBps: 256_000);
        var ilkOpus = new SourceStream(1, StreamKind.Audio, "vorbis", "eng", Channels: 2, BitrateBps: 128_000);
        var seciliAac = new SourceStream(2, StreamKind.Audio, "aac", "tur", IsDefault: true, Channels: 6, BitrateBps: 256_000);
        var plan = new ConversionPlan { Container = "mp4", AudioCodec = "copy" };
        var hata = "The MP4 container does not support copying the source vorbis audio stream.";

        var info = Ornek(Video, ilkAac, seciliOpus);
        Assert.Equal("aac", info.AudioCodec);
        Assert.Same(seciliOpus, ConversionArguments.AutoAudio(info));
        Assert.Contains(hata, ConversionArguments.Validate(info, plan));

        var ters = Ornek(Video, ilkOpus, seciliAac);
        Assert.Equal("vorbis", ters.AudioCodec);
        Assert.DoesNotContain(ConversionArguments.Validate(ters, plan), e => e.Contains("does not support copying"));
    }
}
