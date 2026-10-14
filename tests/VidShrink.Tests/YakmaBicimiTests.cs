using System.Text;
using VidShrink.App;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// SIK-P5: yakilan metin altyazinin yazi tipi, boyutu ve arka plan kutusu <c>subtitles</c>
/// suzgecine <c>force_style</c> ile gider. Secim bosken suzgec metni eskisiyle bayt bayt ayni;
/// resim altyazida (PGS, VobSub) stil yazilmaz ve arayuzde secim gizlenir. Canli kol ayni
/// SRT'yi iki boyutla yakar ve parlak piksel sayisinin buyudugunu olcer.
/// </summary>
public sealed class YakmaBicimiTests
{
    private const string EskiMetin = "subtitles=filename='C\\:/kaynak/girdi.mkv':si=1";

    private static readonly CliText Tr = CliText.ForLanguage("tr");
    private static readonly CliText En = CliText.ForLanguage("en");

    private static readonly SourceStream Resim = new(1, StreamKind.Subtitle, "hdmv_pgs_subtitle", "eng", Bytes: 1_000);
    private static readonly SourceStream Metin = new(2, StreamKind.Subtitle, "subrip", "tur", Bytes: 1_000);

    private static MediaInfo Info() => Kaynak(600, Video, Resim, Metin);

    private static string Klasor
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-a03bae204855031c6", "yakma-bicimi");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static CliParseResult Ayristir(params string[] ek)
        => CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB" }.Concat(ek).ToArray());

    [Fact]
    public void BosSecimdeSuzgecMetniDegismez()
    {
        var bos = new VideoFilterOptions { BurnSubtitle = 1 };

        Assert.Equal(EskiMetin, VideoFilterChain.BurnFilter(Info(), bos, 0));
        Assert.Null(VideoFilterChain.BurnStyle(bos));
        Assert.Equal("subtitles=filename='C\\:/alt/a.srt'",
            VideoFilterChain.BurnFilter(Info(), new VideoFilterOptions { BurnFile = "C:\\alt\\a.srt" }, 0));
    }

    [Fact]
    public void SecimForceStyleIleYazilir()
    {
        var tam = new VideoFilterOptions { BurnSubtitle = 1, BurnFont = "Arial", BurnFontSize = 24, BurnBox = true };

        Assert.Equal(EskiMetin + ":force_style='FontName=Arial,FontSize=24,BorderStyle=3'",
            VideoFilterChain.BurnFilter(Info(), tam, 0));
        Assert.Equal(EskiMetin + ":force_style='FontSize=12'",
            VideoFilterChain.BurnFilter(Info(), new VideoFilterOptions { BurnSubtitle = 1, BurnFontSize = 12 }, 0));
        Assert.Equal(EskiMetin + ":force_style='BorderStyle=3'",
            VideoFilterChain.BurnFilter(Info(), new VideoFilterOptions { BurnSubtitle = 1, BurnBox = true }, 0));
        Assert.Equal(EskiMetin + ":force_style='FontName=Times New Roman'",
            VideoFilterChain.BurnFilter(Info(), new VideoFilterOptions { BurnSubtitle = 1, BurnFont = "Times New Roman" }, 0));
        Assert.Equal("setpts=PTS+20/TB," + EskiMetin + ":force_style='FontSize=24',setpts=PTS-20/TB",
            VideoFilterChain.BurnFilter(Info(), new VideoFilterOptions { BurnSubtitle = 1, BurnFontSize = 24 }, 20));
        Assert.Equal("subtitles=filename='C\\:/alt/a.srt':force_style='FontSize=24'",
            VideoFilterChain.BurnFilter(Info(), new VideoFilterOptions { BurnFile = "C:\\alt\\a.srt", BurnFontSize = 24 }, 0));
    }

    [Fact]
    public void ResimIzindeStilYazilmaz()
    {
        var info = Info();
        var resim = new VideoFilterOptions { BurnSubtitle = 0, BurnFont = "Arial", BurnFontSize = 24, BurnBox = true };
        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 50, Filters = resim });
        var args = string.Join(' ', FfmpegArguments.Build(info, plan, "cikti.mp4", 0, null));

        Assert.Null(VideoFilterChain.BurnFilter(info, resim, 0));
        Assert.Contains("overlay=", args);
        Assert.DoesNotContain("force_style", args);
        Assert.DoesNotContain("FontSize", args);
    }

    [Theory]
    [InlineData("Ari,al", 24, 1)]
    [InlineData("Ari'al", 24, 1)]
    [InlineData("Ari:al", 24, 1)]
    [InlineData("Ari=al", 24, 1)]
    [InlineData("Ari\\al", 24, 1)]
    [InlineData(" ", 24, 1)]
    [InlineData("Arial", 0, 1)]
    [InlineData("Arial", 201, 1)]
    [InlineData("Ari,al", 0, 2)]
    [InlineData("Noto Sans CJK-JP_1.0", 6, 0)]
    [InlineData("Arial", 200, 0)]
    public void DogrulamaYasakKarakterVeBoyutuReddeder(string font, int boyut, int beklenen)
    {
        var options = new VideoFilterOptions { BurnSubtitle = 1, BurnFont = font, BurnFontSize = boyut };
        Assert.Equal(beklenen, VideoFilterChain.Validate(Info(), options).Count(p => p.StartsWith("burn style", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("--yak-yazi-tipi", "--yak-boyut", "--yak-kutu")]
    [InlineData("--burn-font", "--burn-size", "--burn-box")]
    public void CliBayraklariPlanaTasinir(string font, string boyut, string kutu)
    {
        var sonuc = Ayristir("--yak", "2", font, "DejaVu Sans", boyut, "28", kutu);
        Assert.Null(sonuc.ErrorKey);
        var filters = sonuc.Request!.ToPlanOptions(25).Filters!;

        Assert.Equal(1, filters.BurnSubtitle);
        Assert.Equal("DejaVu Sans", filters.BurnFont);
        Assert.Equal(28, filters.BurnFontSize);
        Assert.True(filters.BurnBox);

        var bos = Ayristir("--yak", "2").Request!.ToPlanOptions(25).Filters!;
        Assert.Null(bos.BurnFont);
        Assert.Null(bos.BurnFontSize);
        Assert.False(bos.BurnBox);
    }

    [Fact]
    public void CliHatalari()
    {
        Assert.Equal("error.bad-burn-size", Ayristir("--yak", "1", "--yak-boyut", "0").ErrorKey);
        Assert.Equal("error.bad-burn-size", Ayristir("--yak", "1", "--yak-boyut", "abc").ErrorKey);
        Assert.Equal("error.bad-burn-font", Ayristir("--yak", "1", "--yak-yazi-tipi", "A,B").ErrorKey);
        Assert.Equal("error.burn-style-needs-burn", Ayristir("--yak-kutu").ErrorKey);
        Assert.Equal("error.burn-style-needs-burn", Ayristir("--yak-boyut", "20").ErrorKey);
        Assert.Null(Ayristir("--yak-srt", "a.srt", "--yak-kutu").ErrorKey);
        Assert.Null(Ayristir("--altyazi-tara", "--yak-boyut", "20").ErrorKey);
        Assert.Equal("error.missing-value", Ayristir("--yak", "1", "--yak-boyut").ErrorKey);
        foreach (var bayrak in new[] { "--yak-yazi-tipi", "--burn-font", "--yak-boyut", "--burn-size", "--yak-kutu", "--burn-box" })
            Assert.Contains(bayrak, CliParser.NotInWatch);
        Assert.Equal("error.not-in-watch",
            CliParser.Parse(new[] { "izle", "k", "--cikti", "c", "--hedef", "25MB", "--yak-kutu" }).ErrorKey);
    }

    [Fact]
    public void YardimVeHataMetniIkiDilde()
    {
        foreach (var dil in new[] { Tr, En })
        {
            Assert.Contains("--yak-yazi-tipi", dil["help"]);
            Assert.Contains("--yak-boyut", dil["help"]);
            Assert.Contains("--yak-kutu", dil["help"]);
            Assert.Contains("A,B", dil.Format("error.bad-burn-font", "A,B"));
            Assert.Contains("999", dil.Format("error.bad-burn-size", "999"));
            Assert.Contains("--yak", dil["error.burn-style-needs-burn"]);
        }
        Assert.Contains("--burn-font", En["help"]);
        foreach (var anahtar in new[] { "error.bad-burn-font", "error.bad-burn-size", "error.burn-style-needs-burn" })
            Assert.NotEqual(Tr[anahtar], En[anahtar]);
    }

    private static MediaInfo PencereKaynagi() => new()
    {
        FilePath = "C:\\ornek\\film.mkv",
        FileSizeBytes = 50_000_000L,
        DurationSeconds = 60,
        Width = 1280,
        Height = 720,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 6_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p",
        Streams = new[]
        {
            new SourceStream(0, StreamKind.Video, "h264"),
            new SourceStream(1, StreamKind.Audio, "aac", "tur", Channels: 2),
            new SourceStream(2, StreamKind.Subtitle, "hdmv_pgs_subtitle", "eng"),
            new SourceStream(3, StreamKind.Subtitle, "subrip", "tur")
        }
    };

    [Fact]
    public void ArayuzSecimiMetinIzindeGorunurPlanaGecerYeniKaynaktaSifirlanir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var window = new MainWindow();
            try
            {
                window.LoadWithoutProbing("C:\\ornek\\film.mkv", PencereKaynagi());
                var kapali = window.BurnStylePanel.IsVisible;
                window.CmbBurnSubtitle.SelectedIndex = 1;
                var resimde = window.BurnStylePanel.IsVisible;
                window.CmbBurnSubtitle.SelectedIndex = 2;
                var metinde = window.BurnStylePanel.IsVisible;
                var bos = window.PlanOptionsForTest().Filters;
                window.CmbBurnFont.SelectedIndex = MainWindow.BurnFontChoices.ToList().IndexOf("Arial") + 1;
                window.CmbBurnSize.SelectedIndex = MainWindow.BurnSizeCandidates.ToList().IndexOf(32) + 1;
                window.ChkBurnBox.IsChecked = true;
                var dolu = window.PlanOptionsForTest().Filters;
                window.CmbBurnSubtitle.SelectedIndex = 1;
                var resimPlan = window.PlanOptionsForTest();
                window.LoadWithoutProbing("C:\\ornek\\ikinci.mkv", PencereKaynagi() with { FilePath = "C:\\ornek\\ikinci.mkv" });
                var yeni = window.PlanOptionsForTest().Filters;
                return (kapali, resimde, metinde, bos, dolu, resimPlan, yeni, window.BurnStylePanel.IsVisible);
            }
            finally { window.Close(); }
        });

        Assert.False(sonuc.kapali);
        Assert.False(sonuc.resimde);
        Assert.True(sonuc.metinde);
        Assert.Equal(1, sonuc.bos.BurnSubtitle);
        Assert.Null(sonuc.bos.BurnFont);
        Assert.Null(sonuc.bos.BurnFontSize);
        Assert.False(sonuc.bos.BurnBox);
        Assert.Equal("Arial", sonuc.dolu.BurnFont);
        Assert.Equal(32, sonuc.dolu.BurnFontSize);
        Assert.True(sonuc.dolu.BurnBox);
        var info = PencereKaynagi();
        Assert.DoesNotContain("force_style",
            string.Join(' ', FfmpegArguments.Build(info, PlanCalculator.Build(info, sonuc.resimPlan), "c.mp4", 0, null)));
        Assert.Null(sonuc.yeni.BurnSubtitle);
        Assert.Null(sonuc.yeni.BurnFont);
        Assert.Null(sonuc.yeni.BurnFontSize);
        Assert.False(sonuc.yeni.BurnBox);
        Assert.False(sonuc.Item8);
    }

    [Fact]
    public void AnahtarlarButunDillerde()
    {
        var anahtarlar = new[] { "main.subtitles.style.font", "main.subtitles.style.size", "main.subtitles.style.box", "main.subtitles.style.default" };
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            foreach (var key in anahtarlar)
                Assert.True(values.TryGetValue(key, out var metin) && metin.Length > 0, $"{language}: {key}");
            if (language != "en")
                Assert.NotEqual(Locales.Values("en")["main.subtitles.style.box"], values["main.subtitles.style.box"]);
        }
    }

    private static async Task<string> SiyahAsync(string ad)
    {
        var yol = Path.Combine(Klasor, ad);
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y",
            "-f", "lavfi", "-i", "color=c=black:size=320x240:rate=25:duration=2",
            "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=2",
            "-map", "0", "-map", "1", "-threads", "2", "-c:v", "libx264", "-preset", "ultrafast", "-b:v", "3M",
            "-c:a", "aac", yol
        });
        return yol;
    }

    private static async Task YakAsync(string kaynak, string cikti, VideoFilterOptions filters)
    {
        var info = await FfprobeClient.ProbeAsync(kaynak);
        var plan = PlanCalculator.Build(info, new PlanOptions
        {
            TargetMb = 1.5,
            LockedCodec = "libx264",
            LockedMode = EncodeMode.Crf,
            LockedCrf = 30,
            LockedPreset = "ultrafast",
            Filters = filters
        });
        Assert.NotEqual(EncodeMode.PassThrough, plan.ModeEnum);
        var args = FfmpegArguments.Build(info, plan, cikti, 0, null).ToList();
        args.InsertRange(args.Count - 1, new[] { "-threads", "2" });
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, args);
    }

    private static async Task<int> ParlakPikselAsync(string yol)
    {
        var gri = yol + ".gray";
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-ss", "1", "-i", yol, "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "gray", gri
        });
        var baytlar = File.ReadAllBytes(gri);
        File.Delete(gri);
        Assert.NotEmpty(baytlar);
        return baytlar.Count(b => b > 128);
    }

    [FfmpegFact]
    public async Task CanliIkiBoyutFarkliKareVerir()
    {
        var srt = Path.Combine(Klasor, "canli-yazi.srt");
        try
        {
            File.WriteAllText(srt, "1\n00:00:00,000 --> 00:00:02,000\nMERHABA\n\n", new UTF8Encoding(false));
            var kaynak = await SiyahAsync("canli-kaynak.mp4");
            var kucuk = Path.Combine(Klasor, "canli-12.mp4");
            var buyuk = Path.Combine(Klasor, "canli-32.mp4");
            var bos = Path.Combine(Klasor, "canli-bos.mp4");

            await YakAsync(kaynak, kucuk, new VideoFilterOptions { BurnFile = srt, BurnFontSize = 12 });
            await YakAsync(kaynak, buyuk, new VideoFilterOptions { BurnFile = srt, BurnFontSize = 32 });
            await YakAsync(kaynak, bos, new VideoFilterOptions { BurnFile = srt });

            var k = await ParlakPikselAsync(kucuk);
            var b = await ParlakPikselAsync(buyuk);
            var v = await ParlakPikselAsync(bos);
            Assert.True(k > 0, $"12: {k}");
            Assert.True(b > k * 3, $"12: {k}, 32: {b}");
            Assert.True(v > k && v < b, $"12: {k}, varsayilan: {v}, 32: {b}");
        }
        finally
        {
            KanitKapanisi.Kapat(Klasor, "canli-*");
        }
    }
}
