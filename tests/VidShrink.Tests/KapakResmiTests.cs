using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// B1d kapak resmi: kaynagin <c>attached_pic</c> izi (mjpeg/png/bmp) MP4'te kopyalanip
/// <c>-disposition:v:1 attached_pic</c> ile yazilir, bayti butceye girer. Kapak varken video
/// bayraklari iz basina (<c>-c:v:0</c>...) yazilir, yoksa ffmpeg kapagi da kodlar. MKV ve MOV
/// tasimaz (olculdu, <c>docs/olcumler/b1-kalan-mutasyonlar.md</c>).
/// </summary>
public sealed class KapakResmiTests
{
    private static readonly SourceStream Ses = new(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000);
    private static readonly SourceStream Kapak = new(2, StreamKind.Video, "png", IsAttachedPicture: true, Bytes: 600_000);

    private static StreamPlan Karar(OutputContainer kap, SourceStream? kapak, bool platform = false)
        => StreamMapping.Decide(Kaynak(600, kapak is null ? new[] { Video, Ses } : new[] { Video, Ses, kapak }),
            new StreamRequest(PlatformDelivery: platform), kap, 160, null, "aac", true, 100);

    /// <summary>MP4'te kapak eslenir, kopyalanir, bayragi yazilir ve bayti yan butceye eklenir.</summary>
    [Fact]
    public void Mp4KapagiTasiyor()
    {
        var kapakli = Karar(OutputContainer.Mp4, Kapak);
        var kapaksiz = Karar(OutputContainer.Mp4, null);
        var args = kapakli.OutputArguments();
        var liste = args.ToList();

        Assert.Equal("0:2", kapakli.CoverMap);
        Assert.Equal("0:2", liste[liste.IndexOf("0:0") + 2]);
        Assert.Equal("copy", Sonraki(args, "-c:v:1"));
        Assert.Equal("attached_pic", Sonraki(args, "-disposition:v:1"));
        Assert.Equal(600_000 * 8.0 / 1000 / 600, kapakli.SideK - kapaksiz.SideK, 3);
        Assert.Equal("0:0", kapakli.VideoMap);
    }

    /// <summary>Olumsuz kontrol: MKV, MOV, platform teslimi ve resim olmayan kodek kapak eslemez.</summary>
    [Fact]
    public void KapakTasinmayanYerler()
    {
        Assert.Null(Karar(OutputContainer.Mkv, Kapak).CoverMap);
        Assert.Null(Karar(OutputContainer.Mov, Kapak).CoverMap);
        Assert.Null(Karar(OutputContainer.WebM, Kapak).CoverMap);
        Assert.Null(Karar(OutputContainer.Mp4, Kapak, platform: true).CoverMap);
        Assert.Null(Karar(OutputContainer.Mp4, Kapak with { Codec = "h264" }).CoverMap);
        Assert.DoesNotContain("-disposition:v:1", Karar(OutputContainer.Mkv, Kapak).OutputArguments());
    }

    /// <summary>Kapak varken video bayraklari iz basina; yokken genel yazim (olumsuz kontrol).</summary>
    [Fact]
    public void KapakVarkenBayraklarIzBasina()
    {
        var info = Kaynak(600, Video, Ses, Kapak);
        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25 });

        var kapakli = FfmpegArguments.Build(info, plan, "cikti.mp4", 2, "log");
        Assert.Contains("-c:v:0", kapakli);
        Assert.Contains("-pix_fmt:v:0", kapakli);
        Assert.DoesNotContain("-c:v", kapakli);
        Assert.DoesNotContain("-pix_fmt", kapakli);

        var mkv = FfmpegArguments.Build(info, plan, "cikti.mkv", 2, "log");
        Assert.Contains("-c:v", mkv);
        Assert.DoesNotContain("-c:v:0", mkv);

        var ilk = FfmpegArguments.Build(info, plan, "cikti.mp4", 1, "log");
        Assert.Contains("-c:v", ilk);
    }

    /// <summary>
    /// Canli kol: yoklama kapagin baytini png dosyasinin boyuna esit okuyor; MP4 cikti png
    /// <c>attached_pic=1</c> tasiyor, MKV cikti tek video izi (olumsuz kontrol).
    /// </summary>
    [Fact]
    public async Task CanliKapak()
    {
        var (kaynak, bayt) = await KapakliAsync("kapak-kaynak.mp4");

        var info = await FfprobeClient.ProbeAsync(kaynak);
        Assert.Equal(bayt, info.Streams.Single(stream => stream.IsAttachedPicture).Bytes);

        await KodlaAsync(kaynak, Yol("kapak-cikti.mp4"), _ => { });
        await KodlaAsync(kaynak, Yol("kapak-cikti.mkv"), _ => { });

        var mp4 = (await AkislarAsync(Yol("kapak-cikti.mp4"))).Where(stream => Alan(stream, "codec_type") == "video").ToList();
        Assert.Equal(2, mp4.Count);
        var resim = mp4.Single(stream => Bayrak(stream, "attached_pic") == 1);
        Assert.Equal("png", Alan(resim, "codec_name"));
        Assert.Equal("h264", Alan(mp4.Single(stream => Bayrak(stream, "attached_pic") == 0), "codec_name"));

        var mkv = (await AkislarAsync(Yol("kapak-cikti.mkv"))).Where(stream => Alan(stream, "codec_type") == "video").ToList();
        Assert.Single(mkv);

        Kapat("kapak-*");
    }
}
