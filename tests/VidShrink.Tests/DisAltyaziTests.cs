using System.Text;
using VidShrink.Cli;
using VidShrink.Core;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// B1c dis altyazi: <c>--altyazi dosya</c> ve <c>--yan-altyazi</c> (girdinin yanindaki
/// <c>film*.srt|ass|ssa|vtt</c>). Dosya ek bir <c>-i</c> girdisi olur, esleme <c>N:0</c>; dil adindan
/// okunur. Kap kodegi secer: MP4 ailesi mov_text, MKV kopya, WebM webvtt; platform teslimi atar.
/// </summary>
public sealed class DisAltyaziTests
{
    private static readonly ExternalSubtitle Tr = new("C:\\dis\\film.tr.srt", "subrip", "tur", 2_000);

    private static StreamPlan Karar(OutputContainer kap, bool platform = false)
        => StreamMapping.Decide(Kaynak(600, Video), new StreamRequest(PlatformDelivery: platform, ExternalSubtitles: new[] { Tr }),
            kap, 160, null, "aac", true, 100);

    /// <summary>Dosyadan kodek ve dil; tanimayan uzanti ve olmayan dosya <c>null</c>. Yan dosyalar govdeyle suzulur.</summary>
    [Fact]
    public void DosyadanVeYandanOkunuyor()
    {
        var klasor = Yol("dis-yan");
        Directory.CreateDirectory(klasor);
        foreach (var ad in new[] { "film.mkv", "film.tr.srt", "film.en.ass", "film.txt", "baska.srt", "filmler.srt" })
            File.WriteAllText(Path.Combine(klasor, ad), "1\n00:00:00,000 --> 00:00:01,000\nx\n\n", new UTF8Encoding(false));

        var tr = ExternalSubtitle.FromFile(Path.Combine(klasor, "film.tr.srt"))!;
        Assert.Equal("subrip", tr.Codec);
        Assert.Equal("tur", tr.Language);
        Assert.Equal("ass", ExternalSubtitle.FromFile(Path.Combine(klasor, "film.en.ass"))!.Codec);
        Assert.Null(ExternalSubtitle.FromFile(Path.Combine(klasor, "film.txt")));
        Assert.Null(ExternalSubtitle.FromFile(Path.Combine(klasor, "yok.srt")));

        var yan = ExternalSubtitle.Sidecars(Path.Combine(klasor, "film.mkv")).Select(sub => Path.GetFileName(sub.Path)).ToList();
        Assert.Equal(new[] { "film.en.ass", "film.tr.srt" }, yan);

        Kapat("dis-yan");
    }

    /// <summary>Kap kodegi secer; esleme ek girdiden (<c>1:0</c>), dil metaveriye yazilir.</summary>
    [Theory]
    [InlineData(OutputContainer.Mp4, "mov_text")]
    [InlineData(OutputContainer.Mov, "mov_text")]
    [InlineData(OutputContainer.Mkv, "copy")]
    [InlineData(OutputContainer.WebM, "webvtt")]
    public void KapKodegiSeciyor(OutputContainer kap, string kodek)
    {
        var plan = Karar(kap);
        var args = plan.OutputArguments().ToList();

        Assert.Equal(kodek, plan.Subtitles.Single().Codec);
        Assert.Equal("1:0", plan.Subtitles[0].Map);
        Assert.Equal(new[] { Tr.Path }, plan.ExtraInputs);
        Assert.Contains("1:0", args);
        Assert.Equal("language=tur", args[args.LastIndexOf("-metadata:s:s:0") + 1]);
        Assert.Equal(2_000 * 8.0 / 1000 / 600, plan.SideK, 6);
    }

    /// <summary>Olumsuz kontrol: platform teslimi dis altyaziyi da atar ve not dusurur.</summary>
    [Fact]
    public void PlatformDisAltyaziyiAtiyor()
    {
        var plan = Karar(OutputContainer.Mp4, platform: true);

        Assert.Empty(plan.Subtitles);
        Assert.Empty(plan.ExtraInputs);
        Assert.Contains(StreamNote.SubtitleDroppedForPlatform, plan.Notes);
    }

    /// <summary>
    /// Ek girdi ana girdiden hemen sonra; kesitte kendi <c>-ss</c>'ini ana girdiyle ayni degerle alir.
    /// Ilk gecis altyazi yazmaz, ek girdiyi de acmaz (olumsuz kontrol).
    /// </summary>
    [Fact]
    public void GirdiSirasiVeKesit()
    {
        var info = Kaynak(600, Video);
        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25, ExternalSubtitles = new[] { Tr } });

        var duz = FfmpegArguments.Build(info, plan, "cikti.mp4", 2, "log").ToList();
        Assert.Equal(duz.IndexOf(info.FilePath) + 2, duz.IndexOf(Tr.Path));
        Assert.Equal("-i", duz[duz.IndexOf(Tr.Path) - 1]);

        var ilk = FfmpegArguments.Build(info, plan, "cikti.mp4", 1, "log");
        Assert.DoesNotContain(Tr.Path, ilk);

        plan.Trim = new TrimWindow(30, 60);
        var kesit = FfmpegArguments.Build(info, plan, "cikti.mp4", 2, "log").ToList();
        var ana = kesit.IndexOf(info.FilePath);
        var dis = kesit.IndexOf(Tr.Path);
        Assert.Equal("-ss", kesit[ana - 3]);
        Assert.Equal("-ss", kesit[dis - 3]);
        Assert.Equal(kesit[ana - 2], kesit[dis - 2]);
        Assert.Equal(ana + 4, dis);
    }

    /// <summary>CLI: desteklenmeyen uzanti ayristirmada, olmayan dosya cozumde <c>error.bad-subtitle</c>.</summary>
    [Fact]
    public void CliBozukDosyaReddediliyor()
    {
        var uzanti = CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25", "--altyazi", "notlar.txt" });
        Assert.False(uzanti.Ok);
        Assert.Equal("error.bad-subtitle", uzanti.ErrorKey);

        var yok = CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25", "--altyazi", Yol("olmayan.srt") });
        Assert.True(yok.Ok, yok.ErrorKey);
        Assert.Equal("error.bad-subtitle", yok.Request!.ResolvedSubtitles(Kaynak(600, Video), out _, out var arguman));
        Assert.Equal(Yol("olmayan.srt"), arguman);

        Kapat();
    }

    /// <summary>Canli kol: dis srt MP4'te mov_text + tur, MKV'de subrip olarak cikiyor; dissiz cikti altyazisiz.</summary>
    [Fact]
    public async Task CanliDisAltyazi()
    {
        var kaynak = await SesliAsync("dis-kaynak.mkv", 48000, 1);
        var srt = Yol("dis-kaynak.tr.srt");
        File.WriteAllText(srt, "1\n00:00:00,000 --> 00:00:02,500\nDIS ALTYAZI\n\n", new UTF8Encoding(false));
        var dis = new[] { ExternalSubtitle.FromFile(srt)! };

        await KodlaAsync(kaynak, Yol("dis-cikti.mp4"), options => options.ExternalSubtitles = dis);
        await KodlaAsync(kaynak, Yol("dis-cikti.mkv"), options => options.ExternalSubtitles = dis);
        await KodlaAsync(kaynak, Yol("dis-yok.mp4"), _ => { });

        var mp4 = (await AkislarAsync(Yol("dis-cikti.mp4"))).Single(stream => Alan(stream, "codec_type") == "subtitle");
        Assert.Equal("mov_text", Alan(mp4, "codec_name"));
        Assert.Equal("tur", Etiket(mp4, "language"));
        var mkv = (await AkislarAsync(Yol("dis-cikti.mkv"))).Single(stream => Alan(stream, "codec_type") == "subtitle");
        Assert.Equal("subrip", Alan(mkv, "codec_name"));
        Assert.DoesNotContain(await AkislarAsync(Yol("dis-yok.mp4")), stream => Alan(stream, "codec_type") == "subtitle");

        Kapat("dis-*");
    }
}
