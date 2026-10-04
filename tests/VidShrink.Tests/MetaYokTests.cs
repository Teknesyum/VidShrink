using System.Text.Json;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// <c>--meta-yok</c> (HandBrake <c>--no-metadata</c>): kaynagin kap etiketleri ciktiya gecmez,
/// iz dili acikca yazildigi icin kalir. <c>-map_metadata -1</c> tek basina iz dilini de siliyor
/// (olculdu: <c>und</c>), o yuzden dil ayri yaziliyor.
/// </summary>
public sealed class MetaYokTests
{
    private static MediaInfo Ornek() => Kaynak(600, Video,
        new SourceStream(1, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000),
        new SourceStream(2, StreamKind.Subtitle, "subrip", "eng", Bytes: 1_000));

    private static List<string> Argumanlar(StreamRequest istek)
        => StreamMapping.Decide(Ornek(), istek, OutputContainer.Mkv, 160, null, "aac", true, 100).OutputArguments().ToList();

    [Fact]
    public void BayrakEtiketEsleminiKapatipDiliAcikcaYaziyor()
    {
        var args = Argumanlar(StreamRequest.Default with { KeepAllTracks = true, DropMetadata = true });

        Assert.Equal("-1", Sonraki(args, "-map_metadata"));
        Assert.Equal("language=tur", Sonraki(args, "-metadata:s:a:0"));
        Assert.Equal("language=eng", Sonraki(args, "-metadata:s:s:0"));
    }

    [Fact]
    public void BayraksizKosumEtiketleriTasiyorDilYazmiyor()
    {
        var args = Argumanlar(StreamRequest.Default with { KeepAllTracks = true });

        Assert.Equal("0", Sonraki(args, "-map_metadata"));
        Assert.DoesNotContain("-metadata:s:a:0", args);
        Assert.DoesNotContain("-metadata:s:s:0", args);
    }

    [Fact]
    public void BayrakIstektenPlanaIniyorVeKopyayiKesiyor()
    {
        var acik = CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--meta-yok" }).Request!;
        var ingilizce = CliParser.Parse(new[] { "plan", "a.mp4", "--hedef", "25MB", "--no-metadata" }).Request!;
        var kapali = CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB" }).Request!;

        Assert.True(acik.ToPlanOptions(25).DropMetadata);
        Assert.True(ingilizce.ToPlanOptions(25).DropMetadata);
        Assert.False(kapali.ToPlanOptions(25).DropMetadata);
        Assert.Equal("error.not-in-watch",
            CliParser.Parse(new[] { "izle", "k", "--cikti", "c", "--hedef", "25MB", "--meta-yok" }).ErrorKey);

        var kucuk = Ornek() with { FileSizeBytes = 5_000_000 };
        Assert.Equal(EncodeMode.PassThrough, PlanCalculator.Build(kucuk, new PlanOptions { TargetMb = 25 }).ModeEnum);
        Assert.NotEqual(EncodeMode.PassThrough,
            PlanCalculator.Build(kucuk, new PlanOptions { TargetMb = 25, DropMetadata = true }).ModeEnum);
    }

    private static async Task<(string Baslik, string Dil)> EtiketlerAsync(string yol)
    {
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffprobe,
            new[] { "-v", "error", "-show_entries", "format_tags=title:stream=codec_type:stream_tags=language", "-of", "json", yol });
        Assert.Equal(0, sonuc.Code);
        using var doc = JsonDocument.Parse(sonuc.Out);
        var baslik = doc.RootElement.GetProperty("format").TryGetProperty("tags", out var tags)
            && tags.TryGetProperty("title", out var deger) ? deger.GetString() ?? "" : "";
        var ses = doc.RootElement.GetProperty("streams").EnumerateArray()
            .First(stream => stream.GetProperty("codec_type").GetString() == "audio");
        return (baslik, Etiket(ses, "language"));
    }

    /// <summary>
    /// Canli kol: baslikli ve Turkce sesli kaynak. Bayrakla baslik duser, dil kalir; bayraksiz
    /// es ayni kaynaktan basligi tasir (olumsuz kontrol).
    /// </summary>
    [Fact]
    public async Task CanliBaslikDusuyorDilKaliyor()
    {
        var kaynak = Yol("meta-kaynak.mkv");
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-threads", "2",
            "-f", "lavfi", "-i", "testsrc=size=320x240:rate=25:duration=2,noise=alls=60:allf=t",
            "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=2",
            "-map", "0", "-map", "1", "-c:v", "libx264", "-preset", "ultrafast", "-b:v", "3M", "-c:a", "aac",
            "-metadata", "title=Gizli Baslik", "-metadata:s:a:0", "language=tur", kaynak
        });
        Assert.Equal(("Gizli Baslik", "tur"), await EtiketlerAsync(kaynak));

        var temiz = Yol("meta-temiz.mp4");
        var tasinan = Yol("meta-tasinan.mp4");
        var (_, _, args) = await KodlaAsync(kaynak, temiz, options => { options.TargetMb = 0.4; options.DropMetadata = true; });
        await KodlaAsync(kaynak, tasinan, options => options.TargetMb = 0.4);

        Assert.Equal("-1", Sonraki(args, "-map_metadata"));
        Assert.Equal(("", "tur"), await EtiketlerAsync(temiz));
        Assert.Equal(("Gizli Baslik", "tur"), await EtiketlerAsync(tasinan));

        Kapat("meta-kaynak.mkv", "meta-temiz.mp4", "meta-tasinan.mp4");
    }
}
