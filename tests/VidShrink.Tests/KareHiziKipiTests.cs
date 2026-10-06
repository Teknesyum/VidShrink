using System.Globalization;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// <c>--sabit-kare</c> (HandBrake <c>--cfr</c>) ve <c>--tavan-kare</c> (<c>--pfr</c>): sabit kip
/// <c>-r</c> + <c>-fps_mode cfr</c>, tavanli kip <c>-enc_time_base 1/tavan</c> + <c>-fps_mode vfr</c>
/// yazar. <c>-fpsmax</c> ve <c>fps</c> suzgeci tavanli kipi kuramiyor (olculdu:
/// <c>docs/olcumler/kare-hizi-kipi.md</c>).
/// </summary>
public sealed class KareHiziKipiTests
{
    private static readonly CliText Tr = CliText.ForLanguage("tr");
    private static readonly CliText En = CliText.ForLanguage("en");

    private static readonly string[] KipBayraklari = { "-fps_mode", "-r", "-enc_time_base", "-fpsmax" };

    private static MediaInfo Hizli() => Kaynak(600, Video) with { Fps = 60 };

    private static PlanOptions Secenek(Action<PlanOptions>? ayar = null)
    {
        var options = new PlanOptions { TargetMb = 300, AllowFpsDrop = false, AllowResolutionDrop = false };
        ayar?.Invoke(options);
        return options;
    }

    private static (EncodePlan Plan, List<string> Ilk, List<string> Son) Kur(MediaInfo info, PlanOptions options)
    {
        var plan = PlanCalculator.Build(info, options);
        Assert.NotEqual(EncodeMode.PassThrough, plan.ModeEnum);
        return (plan,
            FfmpegArguments.Build(info, plan, "cikti.mp4", 1, "gecis").ToList(),
            FfmpegArguments.Build(info, plan, "cikti.mp4", 2, "gecis").ToList());
    }

    private static string Suzgec(List<string> args)
        => args.IndexOf("-vf") is var i and >= 0 ? args[i + 1] : "";

    private static double Sayi(string metin) => double.Parse(metin, CultureInfo.InvariantCulture);

    [Fact]
    public void SabitKipIkiGecisteDeHiziVeKipiYaziyor()
    {
        var (plan, ilk, son) = Kur(Hizli(), Secenek(o => o.FrameRate = FrameRateMode.Constant));

        foreach (var args in new[] { ilk, son })
        {
            Assert.Equal("cfr", Sonraki(args, "-fps_mode"));
            Assert.Equal(plan.Fps, Sayi(Sonraki(args, "-r")), 3);
            Assert.DoesNotContain("-enc_time_base", args);
            Assert.True(args.IndexOf("-fps_mode") < args.IndexOf("-c:v"), string.Join(' ', args));
        }
    }

    [Fact]
    public void TavanliKipZamanTabaniYaziyorHizSuzgeciYazmiyor()
    {
        var kaynak = Hizli();
        var tavan = kaynak.Fps / 2;
        var (plan, ilk, son) = Kur(kaynak, Secenek(o => { o.FrameRate = FrameRateMode.Peak; o.MaxFps = tavan; }));
        var (_, _, kipsiz) = Kur(kaynak, Secenek(o => o.MaxFps = tavan));

        Assert.Equal(tavan, plan.Fps, 3);
        foreach (var args in new[] { ilk, son })
        {
            Assert.Equal("vfr", Sonraki(args, "-fps_mode"));
            var taban = Sonraki(args, "-enc_time_base").Split('/');
            Assert.Equal("1", taban[0]);
            Assert.Equal(tavan, Sayi(taban[1]), 3);
            Assert.DoesNotContain("-r", args);
            Assert.DoesNotContain("fps=", Suzgec(args));
        }

        Assert.Contains("fps=" + tavan.ToString("0.###", CultureInfo.InvariantCulture), Suzgec(kipsiz));
        Assert.DoesNotContain(kipsiz, arg => KipBayraklari.Contains(arg));
    }

    [Fact]
    public void BayraksizKosumIkiGecisteDeKipYazmiyor()
    {
        var (plan, ilk, son) = Kur(Hizli(), Secenek());

        Assert.Equal(FrameRateMode.Auto, plan.FrameRate);
        Assert.DoesNotContain(ilk, arg => KipBayraklari.Contains(arg));
        Assert.DoesNotContain(son, arg => KipBayraklari.Contains(arg));
        Assert.DoesNotContain("fps=", Suzgec(son));
    }

    [Fact]
    public void TavanPlaninHiziniSinirliyorKaynakYavassaEtkisiz()
    {
        var kaynak = Hizli();
        var tavan = kaynak.Fps / 2;

        var serbest = PlanCalculator.Build(kaynak, Secenek());
        var tavanli = PlanCalculator.Build(kaynak, Secenek(o => o.MaxFps = tavan));
        var yuksekTavan = PlanCalculator.Build(kaynak, Secenek(o => o.MaxFps = kaynak.Fps * 2));

        Assert.True(serbest.Fps > tavan + 0.01, $"serbest plan {serbest.Fps}");
        Assert.True(tavanli.Fps <= tavan + 0.01, $"tavanli plan {tavanli.Fps}");
        Assert.Equal(serbest.Fps, yuksekTavan.Fps, 3);
    }

    [Fact]
    public void IzgaraKullanicininTavaniMotorAltinaInerseplaninHizi()
    {
        var kaynak = Hizli();
        var tavan = kaynak.Fps * 2;
        var (plan, _, son) = Kur(kaynak, Secenek(o => { o.FrameRate = FrameRateMode.Peak; o.MaxFps = tavan; }));

        Assert.Equal(kaynak.Fps, plan.Fps, 3);
        Assert.Equal(tavan, Sayi(Sonraki(son, "-enc_time_base").Split('/')[1]), 3);

        var inen = plan.Clone();
        inen.Fps = kaynak.Fps / 4;
        var inenArgs = FfmpegArguments.Build(kaynak, inen, "cikti.mp4", 2, "gecis").ToList();
        Assert.Equal(inen.Fps, Sayi(Sonraki(inenArgs, "-enc_time_base").Split('/')[1]), 3);
        Assert.DoesNotContain("fps=", Suzgec(inenArgs));
    }

    [Theory]
    [InlineData("--sabit-kare", "--tavan-kare")]
    [InlineData("--tavan-kare", "--sabit-kare")]
    [InlineData("--cfr", "--pfr")]
    [InlineData("--pfr", "--sabit-kare")]
    public void IkiKipBirlikteReddediliyor(string ilk, string ikinci)
    {
        var birlikte = CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", ilk, "--kare-hizi", "30", ikinci });

        Assert.Null(birlikte.Request);
        Assert.Equal("error.cfr-and-pfr", birlikte.ErrorKey);
        Assert.Null(CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", ilk, "--kare-hizi", "30" }).ErrorKey);
        Assert.Null(CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", ikinci, "--kare-hizi", "30" }).ErrorKey);
    }

    [Fact]
    public void TavanliKipTavansizReddediliyor()
    {
        Assert.Equal("error.pfr-needs-fps", CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--tavan-kare" }).ErrorKey);
        Assert.Equal("error.pfr-needs-fps", CliParser.Parse(new[] { "plan", "a.mp4", "--hedef", "25MB", "--pfr" }).ErrorKey);
        Assert.Null(CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--kare-hizi", "30", "--tavan-kare" }).ErrorKey);
        Assert.Null(CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--sabit-kare" }).ErrorKey);
    }

    [Fact]
    public void BayraklarIstektenPlanSecenegineIniyor()
    {
        PlanOptions Cevir(params string[] ek)
            => CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB" }.Concat(ek).ToArray()).Request!.ToPlanOptions(25);

        var sabit = Cevir("--sabit-kare");
        var sabitTavanli = Cevir("--cfr", "--fps", "23.976");
        var tavanli = Cevir("--tavan-kare", "--kare-hizi", "30");
        var ingilizce = Cevir("--pfr", "--fps", "30");
        var yalnizHiz = Cevir("--kare-hizi", "30");
        var bayraksiz = Cevir();

        Assert.Equal((FrameRateMode.Constant, (double?)null), (sabit.FrameRate, sabit.MaxFps));
        Assert.Equal((FrameRateMode.Constant, (double?)23.976), (sabitTavanli.FrameRate, sabitTavanli.MaxFps));
        Assert.Equal((FrameRateMode.Peak, (double?)30), (tavanli.FrameRate, tavanli.MaxFps));
        Assert.Equal((tavanli.FrameRate, tavanli.MaxFps), (ingilizce.FrameRate, ingilizce.MaxFps));
        Assert.Equal((FrameRateMode.Peak, (double?)30), (yalnizHiz.FrameRate, yalnizHiz.MaxFps));
        Assert.Equal((FrameRateMode.Auto, (double?)null), (bayraksiz.FrameRate, bayraksiz.MaxFps));

        var kopya = PlanCalculator.WithTarget(tavanli, 10);
        Assert.Equal((tavanli.FrameRate, tavanli.MaxFps), (kopya.FrameRate, kopya.MaxFps));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("241")]
    [InlineData("hizli")]
    public void GecersizHizReddediliyor(string deger)
    {
        var sonuc = CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--kare-hizi", deger });

        Assert.Equal("error.bad-fps", sonuc.ErrorKey);
        Assert.Equal(deger, sonuc.ErrorArgument);
        Assert.Equal("error.missing-value", CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--fps" }).ErrorKey);
    }

    [Theory]
    [InlineData("--sabit-kare", null)]
    [InlineData("--pfr", null)]
    [InlineData("--kare-hizi", "30")]
    public void IzleKomutuBayraklariKabulEtmiyor(string bayrak, string? deger)
    {
        var args = new List<string> { "izle", "k", "--cikti", "c", "--hedef", "25MB", bayrak };
        if (deger is not null) args.Add(deger);

        Assert.Equal("error.not-in-watch", CliParser.Parse(args).ErrorKey);
    }

    [Fact]
    public void YardimVeHataMetniIkiDildeBayraklariSoyluyor()
    {
        foreach (var dil in new[] { Tr, En })
        {
            foreach (var bayrak in new[] { "--sabit-kare", "--tavan-kare", "--kare-hizi" })
                Assert.Contains(bayrak, dil["help"]);
            Assert.Contains("--sabit-kare", dil["error.cfr-and-pfr"]);
            Assert.Contains("--tavan-kare", dil["error.cfr-and-pfr"]);
            Assert.Contains("--kare-hizi", dil["error.pfr-needs-fps"]);
            Assert.Contains("999", dil.Format("error.bad-fps", "999"));
            Assert.DoesNotContain("{0}", dil.Format("error.bad-fps", "999"));
        }
        foreach (var bayrak in new[] { "--cfr", "--pfr", "--fps" })
        {
            Assert.Contains(bayrak, En["help"]);
            Assert.Contains(bayrak, CliParser.NotInWatch);
        }
        Assert.NotEqual(Tr["error.cfr-and-pfr"], En["error.cfr-and-pfr"]);
    }

    [Fact]
    public void KipDeTavanDaKopyaYolunuKapatiyor()
    {
        var kucuk = Kaynak(600, Video) with { FileSizeBytes = 5_000_000 };

        Assert.Equal(EncodeMode.PassThrough, PlanCalculator.Build(kucuk, new PlanOptions { TargetMb = 25 }).ModeEnum);
        Assert.NotEqual(EncodeMode.PassThrough,
            PlanCalculator.Build(kucuk, new PlanOptions { TargetMb = 25, FrameRate = FrameRateMode.Constant }).ModeEnum);
        Assert.NotEqual(EncodeMode.PassThrough,
            PlanCalculator.Build(kucuk, new PlanOptions { TargetMb = 25, FrameRate = FrameRateMode.Peak, MaxFps = 12 }).ModeEnum);
        Assert.NotEqual(EncodeMode.PassThrough,
            PlanCalculator.Build(kucuk, new PlanOptions { TargetMb = 25, MaxFps = 12 }).ModeEnum);
    }

    private static async Task<List<double>> KareAnlariAsync(string yol)
    {
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffprobe,
            new[] { "-v", "error", "-select_streams", "v:0", "-show_entries", "packet=pts_time", "-of", "csv=p=0", yol });
        Assert.Equal(0, sonuc.Code);
        var anlar = sonuc.Out.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(satir => Sayi(satir.TrimEnd(','))).OrderBy(an => an).ToList();
        Assert.True(anlar.Count > 2, yol + ": " + sonuc.Out);
        return anlar;
    }

    private static List<double> Araliklar(List<double> anlar)
        => anlar.Zip(anlar.Skip(1), (once, sonra) => sonra - once).ToList();

    private static int EnDoluSaniye(List<double> anlar)
        => anlar.Max(bas => anlar.Count(an => an >= bas && an < bas + 1));

    private static async Task<(EncodePlan Plan, List<double> Anlar)> KodlaIkiIplikAsync(string kaynak, string cikti, Action<PlanOptions> ayar)
    {
        var info = await FfprobeClient.ProbeAsync(kaynak);
        var options = new PlanOptions
        {
            TargetMb = 1.5,
            LockedCodec = "libx264",
            LockedMode = EncodeMode.Crf,
            LockedCrf = 35,
            LockedPreset = "ultrafast"
        };
        ayar(options);
        var plan = PlanCalculator.Build(info, options);
        Assert.NotEqual(EncodeMode.PassThrough, plan.ModeEnum);
        plan.ExtraArgs.AddRange(new[] { "-threads", "2" });
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, FfmpegArguments.Build(info, plan, cikti, 0, null));
        return (plan, await KareAnlariAsync(cikti));
    }

    /// <summary>
    /// Canli kol: ilk saniyesi 50, ikinci saniyesi 10 kare olan kaynak. Sabit kipte aralik tek,
    /// tavanli kipte hicbir saniye tavani asmiyor ve seyrek kisim kaliyor; bayraksiz es ayni
    /// kaynaktan degisken araligi ve tavan ustu saniyeyi tasir (olumsuz kontrol).
    /// </summary>
    [FfmpegFact]
    public async Task CanliSabitKipAraligiEsitliyorTavanliKipSikKareyiAtiyor()
    {
        const double Tavan = 25;
        var kaynak = Yol("kare-kaynak.mp4");
        try
        {
            await CanliOlcAsync(kaynak, Tavan);
        }
        finally
        {
            Kapat("kare-kaynak.mp4", "kare-duz.mp4", "kare-sabit.mp4", "kare-tavanli.mp4");
        }
    }

    private static async Task CanliOlcAsync(string kaynak, double Tavan)
    {
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-threads", "2",
            "-f", "lavfi", "-i", "testsrc=size=320x240:rate=50:duration=2,select='lt(t,1)+not(mod(n,5))'",
            "-fps_mode", "vfr", "-c:v", "libx264", "-preset", "ultrafast", "-threads", "2", kaynak
        });
        var kaynakAnlar = await KareAnlariAsync(kaynak);
        var kaynakAralik = Araliklar(kaynakAnlar);
        Assert.True(kaynakAralik.Max() > 3 * kaynakAralik.Min(), "kaynak degisken hizli degil");
        Assert.True(EnDoluSaniye(kaynakAnlar) > Tavan, "kaynak tavani asmiyor");

        var (_, duz) = await KodlaIkiIplikAsync(kaynak, Yol("kare-duz.mp4"), _ => { });
        var (sabitPlan, sabit) = await KodlaIkiIplikAsync(kaynak, Yol("kare-sabit.mp4"), o => o.FrameRate = FrameRateMode.Constant);
        var (_, tavanli) = await KodlaIkiIplikAsync(kaynak, Yol("kare-tavanli.mp4"),
            o => { o.FrameRate = FrameRateMode.Peak; o.MaxFps = Tavan; });

        var duzAralik = Araliklar(duz);
        Assert.True(duzAralik.Max() > 3 * duzAralik.Min(), "bayraksiz es sabit hiza donmus");
        Assert.True(EnDoluSaniye(duz) > Tavan, "bayraksiz es tavanin altinda");

        var sabitAralik = Araliklar(sabit);
        Assert.True(sabitAralik.Max() - sabitAralik.Min() < 0.002, $"sabit kip araligi {sabitAralik.Min()}..{sabitAralik.Max()}");
        Assert.Equal(1 / sabitPlan.Fps, sabitAralik.Average(), 3);

        var tavanliAralik = Araliklar(tavanli);
        var seyrekBaslangic = kaynakAnlar.Last() - 0.8;
        Assert.True(EnDoluSaniye(tavanli) <= Tavan, $"tavanli kipte bir saniyede {EnDoluSaniye(tavanli)} kare");
        Assert.True(tavanliAralik.Min() >= 1 / Tavan - 0.001, $"en kisa aralik {tavanliAralik.Min()}");
        Assert.True((tavanli.Count - 1) / (tavanli.Last() - tavanli.First()) <= Tavan, "ortalama hiz tavani asiyor");
        Assert.True(tavanliAralik.Max() > 2 * tavanliAralik.Min(), "tavanli kip sabit hiza donmus");
        Assert.InRange(tavanli.Count(an => an >= seyrekBaslangic), duz.Count(an => an >= seyrekBaslangic) - 1, duz.Count(an => an >= seyrekBaslangic) + 1);
    }
}
