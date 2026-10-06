using System.Text;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit.Abstractions;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Dis altyazi dosyasini goruntuye yakma: <c>--yak-srt</c> (HandBrake <c>--srt-burn</c>) ve
/// <c>--yak-ass</c> (<c>--ssa-burn</c>). Kaynak izini yakan <see cref="VideoFilterChain.BurnFilter"/>
/// ile ayni <c>subtitles</c> suzgeci, ayni yer: kirpmadan sonra, olceklemeden once. Canli kol
/// Turkce harfli, bosluklu, koseli parantezli, virgullu, noktali virgullu ve tek tirnakli yolla
/// kosar. Olcum: <c>docs/olcumler/altyazi-yakma.md</c>.
/// </summary>
public sealed class DisAltyaziYakmaTests(ITestOutputHelper cikti)
{
    private const string ZorYol = "C:\\Videolar\\Çığ şöğü [1080p], it's; x=1\\alt yazı (türkçe).srt";
    private const string ZorYolKacisli = "C\\:/Videolar/Çığ şöğü [1080p], it'\\\\\\''s; x=1/alt yazı (türkçe).srt";

    private static readonly CliText Tr = CliText.ForLanguage("tr");
    private static readonly CliText En = CliText.ForLanguage("en");

    private static readonly SourceStream Ses = new(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000);
    private static readonly SourceStream Metin = new(2, StreamKind.Subtitle, "subrip", "eng", Bytes: 1_000);

    private static MediaInfo Info() => Kaynak(600, Video, Ses, Metin);

    private static string Klasor
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "dis-altyazi-yakma");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static string Suzgec(IReadOnlyList<string> args)
        => args.ToList().IndexOf("-vf") is var i and >= 0 ? args[i + 1] : "";

    private static CliParseResult Ayristir(params string[] ek)
        => CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB" }.Concat(ek).ToArray());

    [Fact]
    public void SuzgecDosyayiYaziyorIzSecmiyor()
    {
        var yak = new VideoFilterOptions { BurnFile = "C:\\alt\\film.tr.srt" };

        Assert.Equal("subtitles=filename='C\\:/alt/film.tr.srt'", VideoFilterChain.BurnFilter(Info(), yak, 0));
        Assert.Equal("setpts=PTS+20/TB,subtitles=filename='C\\:/alt/film.tr.srt',setpts=PTS-20/TB",
            VideoFilterChain.BurnFilter(Info(), yak, 20));
        Assert.DoesNotContain(":si=", VideoFilterChain.BurnFilter(Info(), yak, 0));
        Assert.Contains(":si=0", VideoFilterChain.BurnFilter(Info(), new VideoFilterOptions { BurnSubtitle = 0 }, 0));
    }

    [Fact]
    public void ZorYolSuzgecteKacisli()
    {
        Assert.Equal(ZorYolKacisli, VideoFilterChain.FilterPath(ZorYol));
        Assert.Equal("subtitles=filename='" + ZorYolKacisli + "'",
            VideoFilterChain.BurnFilter(Info(), new VideoFilterOptions { BurnFile = ZorYol }, 0));
    }

    [Fact]
    public void YakmaKirpmadanSonraOlceklemedenOnceVeIkiGecisteAyni()
    {
        var info = Info();
        var plan = PlanCalculator.Build(info, new PlanOptions
        {
            TargetMb = 2,
            Filters = new VideoFilterOptions { BurnFile = ZorYol, Crop = new CropRect(1920, 800, 0, 140) }
        });
        var zincir = VideoFilterChain.Filters(info, plan).ToList();

        var kirp = zincir.FindIndex(filter => filter.StartsWith("crop=", StringComparison.Ordinal));
        var yak = zincir.FindIndex(filter => filter.StartsWith("subtitles=", StringComparison.Ordinal));
        var olcek = zincir.FindIndex(filter => filter.StartsWith("scale=", StringComparison.Ordinal));
        Assert.True(kirp >= 0 && yak > kirp && olcek > yak, string.Join(" | ", zincir));

        var ilk = Suzgec(FfmpegArguments.Build(info, plan, "cikti.mp4", 1, "gecis"));
        var son = Suzgec(FfmpegArguments.Build(info, plan, "cikti.mp4", 2, "gecis"));
        Assert.Contains("subtitles=filename='" + ZorYolKacisli + "'", ilk);
        Assert.Equal(ilk, son);
    }

    [Fact]
    public void YakmaKopyaYolunuKapatiyorKaynakIziniDusurmuyor()
    {
        var kucuk = Info() with { FileSizeBytes = 5_000_000 };
        var yakan = PlanCalculator.Build(kucuk, new PlanOptions { TargetMb = 25, Filters = new VideoFilterOptions { BurnFile = ZorYol } });

        Assert.Equal(EncodeMode.PassThrough, PlanCalculator.Build(kucuk, new PlanOptions { TargetMb = 25 }).ModeEnum);
        Assert.NotEqual(EncodeMode.PassThrough, yakan.ModeEnum);
        Assert.Null(yakan.Streams!.Request.BurnedSubtitle);
        Assert.Contains("0:2", yakan.Streams.Subtitles.Select(track => track.Map));
        Assert.True(new VideoFilterOptions { BurnFile = ZorYol }.ChangesPicture);
    }

    [Fact]
    public void DogrulamaIkiYakmayiVeYabanciUzantiyiReddediyor()
    {
        Assert.Empty(VideoFilterChain.Validate(Info(), new VideoFilterOptions { BurnFile = "a.srt" }));
        Assert.Empty(VideoFilterChain.Validate(Info(), new VideoFilterOptions { BurnFile = "a.SSA" }));
        Assert.Contains("burn: only one subtitle can be burned",
            VideoFilterChain.Validate(Info(), new VideoFilterOptions { BurnFile = "a.srt", BurnSubtitle = 0 }));
        Assert.Contains("burn: the file is not .srt, .ass or .ssa",
            VideoFilterChain.Validate(Info(), new VideoFilterOptions { BurnFile = "a.vtt" }));
    }

    [Theory]
    [InlineData("--yak-srt", "alt.srt")]
    [InlineData("--srt-burn", "alt.SRT")]
    [InlineData("--yak-ass", "alt.ass")]
    [InlineData("--ssa-burn", "alt.ssa")]
    public void BayrakDosyayiTasiyor(string bayrak, string dosya)
    {
        var sonuc = Ayristir(bayrak, dosya);

        Assert.True(sonuc.Ok, sonuc.ErrorKey);
        Assert.Equal(dosya, sonuc.Request!.BurnFile);
        Assert.Equal(dosya, sonuc.Request.ToPlanOptions(25).Filters!.BurnFile);
        Assert.Null(Ayristir().Request!.ToPlanOptions(25).Filters?.BurnFile);
    }

    [Theory]
    [InlineData("--yak-srt", "alt.ass")]
    [InlineData("--srt-burn", "alt.txt")]
    [InlineData("--yak-ass", "alt.srt")]
    [InlineData("--ssa-burn", "alt.vtt")]
    [InlineData("--yak-srt", "alt")]
    public void YabanciUzantiReddediliyor(string bayrak, string dosya)
    {
        var sonuc = Ayristir(bayrak, dosya);

        Assert.Equal("error.bad-burn-file", sonuc.ErrorKey);
        Assert.Equal(dosya, sonuc.ErrorArgument);
    }

    [Theory]
    [InlineData("--yak-srt", "a.srt", "--yak-ass", "b.ass")]
    [InlineData("--ssa-burn", "b.ass", "--srt-burn", "a.srt")]
    [InlineData("--yak-srt", "a.srt", "--yak-srt", "b.srt")]
    [InlineData("--yak", "1", "--yak-srt", "a.srt")]
    [InlineData("--yak-ass", "a.ass", "--burn", "1")]
    public void IkiYakmaBirlikteReddediliyor(params string[] ek)
        => Assert.Equal("error.burn-conflict", Ayristir(ek).ErrorKey);

    [Theory]
    [InlineData("--yak-srt")]
    [InlineData("--srt-burn")]
    [InlineData("--yak-ass")]
    [InlineData("--ssa-burn")]
    public void DegersizBayrakVeIzleKomutu(string bayrak)
    {
        Assert.Equal("error.missing-value", Ayristir(bayrak).ErrorKey);
        Assert.Equal("error.not-in-watch",
            CliParser.Parse(new[] { "izle", "k", "--cikti", "c", "--hedef", "25MB", bayrak, "a.srt" }).ErrorKey);
        Assert.Contains(bayrak, CliParser.NotInWatch);
    }

    [Fact]
    public void OlmayanDosyaCozumdeReddediliyorVarOlanTamYolaCevriliyor()
    {
        var var = Path.Combine(Klasor, "cozum var.srt");
        File.WriteAllText(var, "1\n00:00:00,000 --> 00:00:01,000\nA\n\n");
        var yok = Path.Combine(Klasor, "cozum yok.srt");
        try
        {
            Assert.Equal("error.burn-file-missing", Ayristir("--yak-srt", yok).Request!.ResolvedSubtitles(Info(), out _, out var arguman));
            Assert.Equal(yok, arguman);

            var goreli = Path.GetRelativePath(Directory.GetCurrentDirectory(), var);
            Assert.Null(Ayristir("--yak-srt", goreli).Request!.ResolvedSubtitles(Info(), out var cozulen, out _));
            Assert.Equal(Path.GetFullPath(var), cozulen.BurnFile);
            Assert.Empty(cozulen.ExternalSubtitles);
        }
        finally
        {
            KanitKapanisi.Kapat(Klasor, "cozum var.srt");
        }
    }

    [Theory]
    [InlineData(false, "--yak-srt", ExitCodes.Error, true)]
    [InlineData(false, "--yak", ExitCodes.Error, true)]
    [InlineData(true, "--yak-srt", ExitCodes.InBand, false)]
    [InlineData(false, null, ExitCodes.InBand, false)]
    public async Task LibassYoksaKomutDuruyor(bool suzgecVar, string? bayrak, int beklenen, bool ileti)
    {
        var srt = Path.Combine(Klasor, $"libass-{suzgecVar}-{bayrak ?? "yok"}.srt");
        var kaynak = Path.Combine(Klasor, $"libass-{suzgecVar}-{bayrak ?? "yok"}.mkv");
        File.WriteAllText(srt, "1\n00:00:00,000 --> 00:00:01,000\nA\n\n");
        File.WriteAllText(kaynak, "x");
        try
        {
            var args = new List<string> { "plan", kaynak, "--hedef", "25MB", "--olcumsuz" };
            if (bayrak is not null) args.AddRange(new[] { bayrak, bayrak == "--yak" ? "1" : srt });
            var sorulan = new List<string>();
            var stderr = new StringWriter();

            var kod = await CliApp.RunAsync(args, new StringWriter(), stderr, Tr,
                new CliServices
                {
                    MissingTool = () => null,
                    Probe = (_, _) => Task.FromResult(Info() with { FilePath = kaynak }),
                    Availability = () => null,
                    HasFilter = ad =>
                    {
                        sorulan.Add(ad);
                        return suzgecVar;
                    }
                },
                CancellationToken.None);

            Assert.Equal(beklenen, kod);
            Assert.Equal(ileti, stderr.ToString().Contains(Tr["error.no-libass"], StringComparison.Ordinal));
            Assert.Equal(bayrak is null ? Array.Empty<string>() : new[] { "subtitles" }, sorulan);
        }
        finally
        {
            KanitKapanisi.Kapat(Klasor, Path.GetFileName(srt), Path.GetFileName(kaynak));
        }
    }

    [Fact]
    public void YardimVeHataMetniIkiDilde()
    {
        foreach (var dil in new[] { Tr, En })
        {
            Assert.Contains("--yak-srt", dil["help"]);
            Assert.Contains("--yak-ass", dil["help"]);
            Assert.Contains("x.srt", dil.Format("error.bad-burn-file", "x.srt"));
            Assert.Contains("x.srt", dil.Format("error.burn-file-missing", "x.srt"));
            Assert.Contains("--yak-srt", dil["error.burn-conflict"]);
            Assert.Contains("--yak-ass", dil["error.burn-conflict"]);
            Assert.Contains("libass", dil["error.no-libass"]);
        }
        Assert.Contains("--srt-burn", En["help"]);
        Assert.Contains("--ssa-burn", En["help"]);
        foreach (var anahtar in new[] { "error.bad-burn-file", "error.burn-file-missing", "error.burn-conflict", "error.no-libass" })
            Assert.NotEqual(Tr[anahtar], En[anahtar]);
    }

    private static string ZorKlasor
    {
        get
        {
            var yol = Path.Combine(Klasor, "Çığ şöğü [1080p], it's; x=1");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static async Task<string> SiyahAsync(string ad, int saniye)
    {
        var yol = Path.Combine(Klasor, ad);
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y",
            "-f", "lavfi", "-i", $"color=c=black:size=320x240:rate=25:duration={saniye}",
            "-f", "lavfi", "-i", $"sine=frequency=440:sample_rate=48000:duration={saniye}",
            "-map", "0", "-map", "1", "-threads", "2", "-c:v", "libx264", "-preset", "ultrafast", "-b:v", "3M",
            "-c:a", "aac", yol
        });
        return yol;
    }

    private static async Task<IReadOnlyList<string>> YakAsync(string kaynak, string cikti, Action<PlanOptions> ayar)
    {
        var info = await FfprobeClient.ProbeAsync(kaynak);
        var options = new PlanOptions
        {
            TargetMb = 1.5,
            LockedCodec = "libx264",
            LockedMode = EncodeMode.Crf,
            LockedCrf = 30,
            LockedPreset = "ultrafast"
        };
        ayar(options);
        var plan = PlanCalculator.Build(info, options);
        Assert.NotEqual(EncodeMode.PassThrough, plan.ModeEnum);
        var args = FfmpegArguments.Build(info, plan, cikti, 0, null).ToList();
        args.InsertRange(args.Count - 1, new[] { "-threads", "2" });
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, args);
        return args;
    }

    private static async Task<(int Ust, int Alt)> ParlaklikAsync(string yol)
    {
        var gri = yol + ".gray";
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-ss", "1", "-i", yol, "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "gray", gri
        });
        var baytlar = File.ReadAllBytes(gri);
        File.Delete(gri);
        Assert.NotEmpty(baytlar);
        var yari = baytlar.Length / 2;
        return (baytlar.Take(yari).Max(), baytlar.Skip(yari).Max());
    }

    private static void Topla()
    {
        if (Directory.Exists(ZorKlasor)) Directory.Delete(ZorKlasor, recursive: true);
        KanitKapanisi.Kapat(Klasor, "canli-*");
    }

    [FfmpegFact]
    public async Task CanliSrtZorYoldanYakiliyor()
    {
        var srt = Path.Combine(ZorKlasor, "alt yazı (türkçe).srt");
        File.WriteAllText(srt, "1\n00:00:00,000 --> 00:00:02,000\nŞĞÜİÖÇ MERHABA DÜNYA\n\n", new UTF8Encoding(false));
        var kaynak = await SiyahAsync("canli-srt-kaynak.mkv", 2);
        var yanik = Path.Combine(ZorKlasor, "canli-srt-çıktı.mp4");
        var yaniksiz = Path.Combine(Klasor, "canli-srt-yok.mp4");

        var args = await YakAsync(kaynak, yanik, options => options.Filters = new VideoFilterOptions { BurnFile = srt });
        await YakAsync(kaynak, yaniksiz, _ => { });

        Assert.Contains("subtitles=filename='" + VideoFilterChain.FilterPath(srt) + "'", Suzgec(args));
        var (ust, alt) = await ParlaklikAsync(yanik);
        var (bosUst, bosAlt) = await ParlaklikAsync(yaniksiz);
        cikti.WriteLine($"srt: yanik ust {ust} alt {alt}, yakmasiz ust {bosUst} alt {bosAlt}");
        Assert.True(alt > 200, $"alt yari {alt}");
        Assert.True(ust < 60, $"ust yari {ust}");
        Assert.True(bosUst < 60 && bosAlt < 60, $"yakmasiz {bosUst}/{bosAlt}");
        Assert.DoesNotContain(await AkislarAsync(yanik), stream => Alan(stream, "codec_type") == "subtitle");

        Topla();
    }

    [FfmpegFact]
    public async Task CanliAssBicemiyleYakiliyor()
    {
        var ass = Path.Combine(ZorKlasor, "üst yazı.ass");
        File.WriteAllText(ass,
            "[Script Info]\nScriptType: v4.00+\nPlayResX: 320\nPlayResY: 240\n\n" +
            "[V4+ Styles]\nFormat: Name, Fontname, Fontsize, PrimaryColour, OutlineColour, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\n" +
            "Style: Ust,Arial,28,&H00FFFFFF,&H00000000,1,0,0,8,10,10,10,1\n\n" +
            "[Events]\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\n" +
            "Dialogue: 0,0:00:00.00,0:00:02.00,Ust,,0,0,0,,ÜST YAZI ŞĞİ\n",
            new UTF8Encoding(false));
        var kaynak = await SiyahAsync("canli-ass-kaynak.mkv", 2);
        var yanik = Path.Combine(Klasor, "canli-ass-cikti.mp4");

        await YakAsync(kaynak, yanik, options => options.Filters = new VideoFilterOptions { BurnFile = ass });

        var (ust, alt) = await ParlaklikAsync(yanik);
        cikti.WriteLine($"ass: ust {ust} alt {alt}");
        Assert.True(ust > 200, $"ust yari {ust}");
        Assert.True(alt < 60, $"alt yari {alt}");

        Topla();
    }

    [FfmpegFact]
    public async Task CanliKesitteAltyaziDosyaninSaatiyleOkunuyor()
    {
        var srt = Path.Combine(ZorKlasor, "geç gelen.srt");
        File.WriteAllText(srt, "1\n00:00:12,000 --> 00:00:14,000\nGEC GELEN YAZI\n\n", new UTF8Encoding(false));
        var kaynak = await SiyahAsync("canli-kesit-kaynak.mkv", 14);
        var yanik = Path.Combine(Klasor, "canli-kesit-cikti.mp4");

        var args = await YakAsync(kaynak, yanik, options =>
        {
            options.Filters = new VideoFilterOptions { BurnFile = srt };
            options.Trim = TrimWindow.Of(11.5, 14, 14);
        });

        Assert.Contains("setpts=PTS+1.5/TB,subtitles=", Suzgec(args));
        var (kesitUst, alt) = await ParlaklikAsync(yanik);
        cikti.WriteLine($"kesit: ust {kesitUst} alt {alt}");
        Assert.True(alt > 200, $"kesitin 1. saniyesi (kaynagin 12,5. saniyesi) alt yari {alt}");

        Topla();
    }
}
