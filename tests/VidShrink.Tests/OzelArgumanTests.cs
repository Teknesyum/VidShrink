using VidShrink.App;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// DON-27: Dönüştür'ün "ek argüman" kutusu ve <c>donustur --ek-arguman</c>. Metin
/// <see cref="CustomArguments"/> ile bölünür; argümanlar çıktı yolundan hemen önce yazılır; girdiyi,
/// üzerine yazmayı ya da çıktıyı ezen seçenekler ve ikinci çıktı olacak başıboş değerler reddedilir.
/// Panel başsız açılır; geçici ayar dosyaları <c>.calisma/test-ciktilari/ozel-arguman/</c>, test siler.
/// </summary>
public sealed class OzelArgumanTests
{
    private static readonly SourceStream Ses = new(1, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000);

    private static MediaInfo Ornek() => Kaynak(62, Video, Ses);

    public static TheoryData<string, string[]> Bolmeler => new()
    {
        { "-tune film -g 120", new[] { "-tune", "film", "-g", "120" } },
        { "  -tune   film\t-g 120  ", new[] { "-tune", "film", "-g", "120" } },
        { "-metadata \"title=Benim Klibim\"", new[] { "-metadata", "title=Benim Klibim" } },
        { "-metadata title=\"Benim Klibim\"", new[] { "-metadata", "title=Benim Klibim" } },
        { "-metadata 'comment=a \"b\" c'", new[] { "-metadata", "comment=a \"b\" c" } },
        { "-metadata \"comment=a \\\"b\\\" c\"", new[] { "-metadata", "comment=a \"b\" c" } },
        { "-metadata title=\"\"", new[] { "-metadata", "title=" } },
        { "-vf \"\"", new[] { "-vf", "" } },
        { "-x264-params C:\\yol\\a.txt", new[] { "-x264-params", "C:\\yol\\a.txt" } },
        { "", Array.Empty<string>() },
        { "   ", Array.Empty<string>() }
    };

    [Theory]
    [MemberData(nameof(Bolmeler))]
    public void MetinArgumanlaraBolunur(string metin, string[] beklenen)
    {
        var sonuc = CustomArguments.Parse(metin);
        Assert.True(sonuc.Ok, sonuc.Problem + " " + sonuc.Offender);
        Assert.Equal(beklenen, sonuc.Arguments);
    }

    [Theory]
    [InlineData("-metadata \"title=Benim")]
    [InlineData("-metadata 'title=Benim")]
    [InlineData("-metadata \"a\\\"")]
    public void KapanmamisTirnakReddedilir(string metin)
    {
        var sonuc = CustomArguments.Parse(metin);
        Assert.Equal(CustomArgumentProblem.UnclosedQuote, sonuc.Problem);
        Assert.Empty(sonuc.Arguments);
    }

    [Theory]
    [InlineData("-i baska.mp4", "-i")]
    [InlineData("-tune film -i baska.mp4", "-i")]
    [InlineData("-y", "-y")]
    [InlineData("-n", "-n")]
    [InlineData("--y", "--y")]
    [InlineData("-f null", "-f")]
    [InlineData("-progress pipe:1", "-progress")]
    [InlineData("-c:v libx264 -", "-")]
    [InlineData("-- baska.mp4", "--")]
    public void GirdiyiYaDaCiktiyiEzenReddedilir(string metin, string suclu)
    {
        var sonuc = CustomArguments.Parse(metin);
        Assert.Equal(CustomArgumentProblem.Forbidden, sonuc.Problem);
        Assert.Equal(suclu, sonuc.Offender);
        Assert.Empty(sonuc.Arguments);
    }

    [Theory]
    [InlineData("ikinci.mp4", "ikinci.mp4")]
    [InlineData("-c:v libx264 ikinci.mp4", "ikinci.mp4")]
    [InlineData("-loop -1 ikinci.gif", "ikinci.gif")]
    public void BasibosDegerIkinciCiktiOlurduReddedilir(string metin, string suclu)
    {
        var sonuc = CustomArguments.Parse(metin);
        Assert.Equal(CustomArgumentProblem.StrayValue, sonuc.Problem);
        Assert.Equal(suclu, sonuc.Offender);
    }

    [Theory]
    [InlineData("-an")]
    [InlineData("-loop -1")]
    [InlineData("-itsoffset -0.5")]
    [InlineData("-map_metadata -1 -tune film")]
    public void DegerliVeDegersizSeceneklerGecer(string metin)
        => Assert.True(CustomArguments.Parse(metin).Ok);

    [Theory]
    [InlineData("mp4")]
    [InlineData("mkv")]
    [InlineData("mp3")]
    [InlineData("webp")]
    [InlineData("gif")]
    public void EkArgumanlarCiktiYolundanHemenOnce(string kap)
    {
        var info = Ornek();
        var cikti = Path.Combine(@"C:\cikti", "out." + kap);
        var plan = new ConversionPlan
        {
            Container = kap,
            AudioCodec = kap == "mp3" ? "libmp3lame" : "aac",
            ExtraArgs = new[] { "-metadata", "title=Benim Klibim" }
        };
        var palet = plan.Gif ? "palette.png" : null;

        var args = ConversionArguments.Build(info, plan, cikti, palet);
        var yalin = ConversionArguments.Build(info, plan with { ExtraArgs = Array.Empty<string>() }, cikti, palet);

        Assert.Equal(cikti, args[^1]);
        Assert.Equal(new[] { "-metadata", "title=Benim Klibim" }, args.Skip(args.Count - 3).Take(2));
        Assert.Equal(yalin.Take(yalin.Count - 1), args.Take(args.Count - 3));
    }

    [Fact]
    public void GifPaletGecisineEkArgumanGirmez()
    {
        var plan = new ConversionPlan { Container = "gif", ExtraArgs = new[] { "-metadata", "title=x" } };
        var palet = ConversionArguments.Build(Ornek(), plan, "palette.png");
        Assert.DoesNotContain("title=x", palet);
    }

    [Theory]
    [InlineData("-i", "baska.mp4")]
    [InlineData("-y", null)]
    [InlineData("-f", "null")]
    public void PlanaElleKonanYasakArgumanKomutKurmaz(string secenek, string? deger)
    {
        var ek = deger is null ? new[] { secenek } : new[] { secenek, deger };
        var plan = new ConversionPlan { ExtraArgs = ek };
        Assert.Throws<ArgumentException>(() => ConversionArguments.Build(Ornek(), plan, @"C:\cikti\out.mp4"));
    }

    [Fact]
    public void KomutSatiriEkArgumaniPlanaGecer()
    {
        var parsed = CliParser.Parse(new[] { "donustur", "a.mkv", "--ek-arguman", "-tune film -metadata \"title=Benim Klibim\"" });
        Assert.True(parsed.Request is not null, parsed.ErrorKey + " " + parsed.ErrorArgument);
        var plan = CliConvert.Plan(parsed.Request!);
        Assert.Equal(new[] { "-tune", "film", "-metadata", "title=Benim Klibim" }, plan.ExtraArgs);

        var ingilizce = CliParser.Parse(new[] { "convert", "a.mkv", "--extra-args", "-g 120", "--extra-args", "-bf 2" });
        Assert.Equal(new[] { "-g", "120", "-bf", "2" }, CliConvert.Plan(ingilizce.Request!).ExtraArgs);
    }

    [Theory]
    [InlineData("error.extra-args-forbidden", "-i", "-i b.mp4")]
    [InlineData("error.extra-args-forbidden", "-y", "-y")]
    [InlineData("error.extra-args-stray", "ikinci.mp4", "-c:v libx264 ikinci.mp4")]
    [InlineData("error.extra-args-quote", "-metadata \"title=x", "-metadata \"title=x")]
    public void KomutSatiriYasakEkArgumaniReddeder(string anahtar, string arguman, string metin)
    {
        var parsed = CliParser.Parse(new[] { "donustur", "a.mkv", "--ek-arguman", metin });
        Assert.Null(parsed.Request);
        Assert.Equal(anahtar, parsed.ErrorKey);
        Assert.Equal(arguman, parsed.ErrorArgument);
        Assert.Equal("error.missing-value", CliParser.Parse(new[] { "donustur", "a.mkv", "--ek-arguman" }).ErrorKey);
    }

    [Fact]
    public void KomutSatiriHataMetinleriIkiDildeVar()
    {
        foreach (var dil in new[] { "en", "tr" })
        {
            var text = CliText.ForLanguage(dil);
            foreach (var anahtar in new[] { "error.extra-args-quote", "error.extra-args-forbidden", "error.extra-args-stray" })
                Assert.NotEqual(anahtar, text[anahtar]);
            Assert.Contains("--ek-arguman", text["help"]);
        }
    }

    [Fact]
    public void PanelinKomutuKomutSatiriylaAyni()
    {
        var info = Ornek();
        const string metin = "-tune film -metadata \"title=Benim Klibim\"";
        var request = CliParser.Parse(new[] { "donustur", info.FilePath, "--kodek", "hevc", "--crf", "26", "--ek-arguman", metin }).Request!;
        var cliPlan = CliConvert.Plan(request);
        var cliKomut = FfmpegArguments.ToCommandLine(CliConvert.Arguments(info, cliPlan, CliConvert.OutputPath(request, info, cliPlan), null));

        var (panelPlan, panelKomut) = Pencerede(window =>
        {
            window.LoadWithoutProbing(info.FilePath, info);
            window.ApplyConvertPreset(request.Conversion!);
            window.TxtConvertExtraArgs.Text = metin;
            return (window.CurrentConversionPlan(), window.TxtConvertCommand.Text ?? "");
        });

        Assert.Equal(cliPlan.ExtraArgs, panelPlan.ExtraArgs);
        Assert.Equal(cliKomut, panelKomut);
        Assert.Contains("-metadata \"title=Benim Klibim\" ", panelKomut);
    }

    [Theory]
    [InlineData("-i baska.mp4", "main.convert.extra-args.forbidden", "-i")]
    [InlineData("-c:v libx264 ikinci.mp4", "main.convert.extra-args.stray", "ikinci.mp4")]
    [InlineData("-metadata \"title=x", "main.convert.extra-args.quote", null)]
    public void PanelYasakEkArgumaniKomutaKoymaz(string metin, string anahtar, string? suclu)
    {
        var info = Ornek();
        var (komut, dogrulama, dugme, plan) = Pencerede(window =>
        {
            window.LoadWithoutProbing(info.FilePath, info);
            window.TxtConvertExtraArgs.Text = metin;
            return (window.TxtConvertCommand.Text ?? "", window.TxtConvertValidation.Text ?? "", window.BtnConvert.IsEnabled,
                window.CurrentConversionPlan());
        });

        Assert.Equal("", komut);
        Assert.False(dugme);
        Assert.Empty(plan.ExtraArgs);
        Assert.NotEqual("", dogrulama);
        Assert.DoesNotContain(anahtar, dogrulama);
        if (suclu is not null) Assert.Contains(suclu, dogrulama);
    }

    private static T Pencerede<T>(Func<MainWindow, T> govde)
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "ozel-arguman", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            return AppHost.Run(() =>
            {
                var window = new MainWindow
                {
                    SettingsPathOverride = Path.Combine(klasor, "settings.json"),
                    PresetPathOverride = Path.Combine(klasor, "presets.json"),
                    ConvertPresetPathOverride = Path.Combine(klasor, "convert-presets.json")
                };
                try { return govde(window); }
                finally { window.Close(); }
            });
        }
        finally { Directory.Delete(klasor, recursive: true); }
    }
}
