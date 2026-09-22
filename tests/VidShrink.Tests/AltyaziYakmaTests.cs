using VidShrink.Cli;
using VidShrink.Core;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// B1f metin altyazi yakma: <c>--yak N</c> (1 tabanli) kaynagin N. altyazisini <c>subtitles</c>
/// suzgeciyle goruntuye yakar; suzgec dondurmeden sonra, olceklemeden once durur, yakilan iz
/// ciktiya ayrica eslenmez. Kesitte <c>setpts</c> ile sarilir. Goruntu altyazi (PGS) overlay
/// ister, desteklenmez ve dogrulamada reddedilir.
/// </summary>
public sealed class AltyaziYakmaTests
{
    private static readonly SourceStream Ses = new(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000);
    private static readonly SourceStream Metin = new(2, StreamKind.Subtitle, "subrip", "eng", Bytes: 1_000);
    private static readonly SourceStream Pgs = new(3, StreamKind.Subtitle, "hdmv_pgs_subtitle", "tur", Bytes: 50_000);

    private static MediaInfo Info() => Kaynak(600, Video, Ses, Metin, Pgs);

    /// <summary>Suzgec metni kaynagin yolunu ve iz sirasini tasir; kesitte iki <c>setpts</c> arasina oturur.</summary>
    [Fact]
    public void SuzgecMetni()
    {
        var yak = new VideoFilterOptions { BurnSubtitle = 0 };

        Assert.Equal("subtitles=filename='C\\:/kaynak/girdi.mkv':si=0", VideoFilterChain.BurnFilter(Info(), yak, 0));
        Assert.Equal("setpts=PTS+20/TB,subtitles=filename='C\\:/kaynak/girdi.mkv':si=0,setpts=PTS-20/TB",
            VideoFilterChain.BurnFilter(Info(), yak, 20));
        Assert.Null(VideoFilterChain.BurnFilter(Info(), VideoFilterOptions.Default, 0));
    }

    /// <summary>Yol kacisi: ters bolu duz boluye, iki nokta <c>\:</c>, tek tirnak grafik duzeyinde kapatilip acilir.</summary>
    [Fact]
    public void YolKacisi()
        => Assert.Equal("C\\:/a b/it'\\\\\\''s.mkv", VideoFilterChain.FilterPath("C:\\a b\\it's.mkv"));

    /// <summary>Olumsuz kontrol: PGS ve olmayan iz suzgec kurmaz, dogrulama ikisini ayri sebeple reddeder.</summary>
    [Fact]
    public void GoruntuVeOlmayanIzReddediliyor()
    {
        var pgs = new VideoFilterOptions { BurnSubtitle = 1 };
        var yok = new VideoFilterOptions { BurnSubtitle = 5 };

        Assert.Null(VideoFilterChain.BurnFilter(Info(), pgs, 0));
        Assert.Null(VideoFilterChain.BurnFilter(Info(), yok, 0));
        Assert.Contains("burn: image subtitles need an overlay and are not supported", VideoFilterChain.Validate(Info(), pgs));
        Assert.Contains("burn: the source has no such subtitle", VideoFilterChain.Validate(Info(), yok));
        Assert.Empty(VideoFilterChain.Validate(Info(), new VideoFilterOptions { BurnSubtitle = 0 }));
    }

    /// <summary>Yakilan iz ciktiya eslenmez, digeri eslenir; yakma resmi degistirdigi icin passthrough'u keser.</summary>
    [Fact]
    public void YakilanIzEslenmiyor()
    {
        var plan = StreamMapping.Decide(Info(), new StreamRequest(BurnedSubtitle: 0), OutputContainer.Mkv, 160, null, "aac", true, 100);
        var yakmasiz = StreamMapping.Decide(Info(), StreamRequest.Default, OutputContainer.Mkv, 160, null, "aac", true, 100);

        Assert.Equal(new[] { "0:3" }, plan.Subtitles.Select(track => track.Map));
        Assert.Equal(new[] { "0:2", "0:3" }, yakmasiz.Subtitles.Select(track => track.Map));
        Assert.True(new VideoFilterOptions { BurnSubtitle = 0 }.ChangesPicture);
        Assert.False(VideoFilterOptions.Default.ChangesPicture);
    }

    /// <summary>Zincirde yakma olceklemeden once: altyazi kaynak cozunurlugunde cizilir.</summary>
    [Fact]
    public void YakmaOlceklemedenOnce()
    {
        var info = Info();
        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 2, Filters = new VideoFilterOptions { BurnSubtitle = 0 } });
        var zincir = VideoFilterChain.Filters(info, plan).ToList();

        var yak = zincir.FindIndex(filter => filter.StartsWith("subtitles=", StringComparison.Ordinal));
        var olcek = zincir.FindIndex(filter => filter.StartsWith("scale=", StringComparison.Ordinal));
        Assert.True(yak >= 0 && olcek > yak, string.Join(" | ", zincir));
        Assert.Equal(0, plan.Streams!.Request.BurnedSubtitle);
    }

    /// <summary>CLI: <c>--yak 0</c> ayristirmada, kaynakta olmayan iz cozumde <c>error.bad-burn</c>.</summary>
    [Fact]
    public void CliYakSiniri()
    {
        var sifir = CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25", "--yak", "0" });
        Assert.False(sifir.Ok);
        Assert.Equal("error.bad-burn", sifir.ErrorKey);

        var parsed = CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25", "--burn", "2" });
        Assert.True(parsed.Ok, parsed.ErrorKey);
        Assert.Equal("error.bad-burn", parsed.Request!.ResolvedSubtitles(Info(), out _, out var arguman));
        Assert.Equal("2", arguman);
        Assert.Null(CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25", "--yak", "1" }).Request!.ResolvedSubtitles(Info(), out _, out _));
        Assert.Equal(0, CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25", "--yak", "1" }).Request!.ToPlanOptions(10).Filters.BurnSubtitle);
    }

    /// <summary>
    /// Canli kol: siyah video + beyaz metin altyazi. Yakilinca 1. saniyenin karesinde parlak
    /// piksel var ve cikti altyazi izi tasimiyor; yakmasiz cikti karanlik (olumsuz kontrol).
    /// </summary>
    [Fact]
    public async Task CanliYakma()
    {
        var kaynak = await AltyaziliAsync("yak-kaynak.mkv", zorunlu: false);

        await KodlaAsync(kaynak, Yol("yak-cikti.mp4"), options => options.Filters = new VideoFilterOptions { BurnSubtitle = 0 });
        await KodlaAsync(kaynak, Yol("yak-yok.mp4"), _ => { });

        Assert.True(await EnParlakAsync(Yol("yak-cikti.mp4"), Yol("yak-cikti.gray")) > 200);
        Assert.True(await EnParlakAsync(Yol("yak-yok.mp4"), Yol("yak-yok.gray")) < 60);
        Assert.DoesNotContain(await AkislarAsync(Yol("yak-cikti.mp4")), stream => Alan(stream, "codec_type") == "subtitle");

        Kapat("yak-*");
    }
}
