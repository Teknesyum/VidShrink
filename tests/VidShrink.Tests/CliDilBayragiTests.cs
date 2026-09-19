using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.Cli;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// CLI'nin dil bayrağı (<c>--dil</c> / <c>--lang</c>). Bayraktan önce dil yalnız işletim
/// sisteminin kültüründen geliyordu; Türkçe bir makinede İngilizce çıktı almanın ya da
/// İngilizce bir makinede Türkçe okumanın yolu yoktu.
///
/// <para>Ölçü süreç açmıyor: <see cref="CliApp.RunAsync"/> ulaşılmaz servislerle çağrılıyor,
/// yani ffmpeg aransa ya da yoklama koşsa test kendiliğinden patlar.</para>
/// </summary>
public sealed class CliDilBayragiTests
{
    private static CliServices Ulasilmaz() => new()
    {
        MissingTool = () => throw new InvalidOperationException("araç araması koşmamalı"),
        Probe = (_, _) => throw new InvalidOperationException("yoklama koşmamalı"),
        Availability = () => throw new InvalidOperationException("yetenek sorgusu koşmamalı")
    };

    private static async Task<(int Exit, string Out, string Err)> Kos(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await CliApp.RunAsync(args, stdout, stderr,
            CliText.For(CultureInfo.GetCultureInfo("en-US")), Ulasilmaz(), CancellationToken.None);
        return (exit, stdout.ToString(), stderr.ToString());
    }

    /// <summary>
    /// Seçilen dil sayıların yazımını da belirliyor. <c>CliText.Format</c> her satırı
    /// <see cref="CultureInfo.InvariantCulture"/> ile kuruyordu: aynı programın penceresi
    /// Türkçede <c>12,5 MB</c>, komut satırı <c>12.5 MB</c> diyordu. İngilizce değişmez
    /// kültürde kalıyor — çıktısı makine tarafından da okunuyor. Son iki asert olumlu
    /// kontrol: aynı sayı gerçekten iki türlü yazılabiliyor.
    ///
    /// <para>Son iki asert <c>Format</c>'ın kendi kültürünü okuyor: bugünkü çağrı
    /// yerlerinin hepsi ya dizge ya tamsayı geçiyor, yani <c>Format</c>'ın kültürü
    /// kapanan bir kusur değil, ileride araya girecek ham bir ondalığa karşı kapı.
    /// Bu iki satır olmadan kapı pimsiz kalıyordu (ölçüldü: 0 kırmızı).</para>
    /// </summary>
    [Fact]
    public void SayiYazimiSecilenDildenGeliyor()
    {
        var tr = CliText.ForLanguage("tr");
        var en = CliText.ForLanguage("en");

        Assert.Equal(CultureInfo.GetCultureInfo("tr"), tr.Culture);
        Assert.Equal(CultureInfo.InvariantCulture, en.Culture);

        Assert.Contains("12,5 MB", tr.Format("plan.target", 12.5.ToString("0.##", tr.Culture)));
        Assert.Contains("12.5 MB", en.Format("plan.target", 12.5.ToString("0.##", en.Culture)));
        Assert.DoesNotContain("12.5", tr.Format("plan.target", 12.5.ToString("0.##", tr.Culture)));

        Assert.Contains("12,5", tr.Format("plan.target", 12.5));
        Assert.Contains("12.5", en.Format("plan.target", 12.5));
    }

    /// <summary>
    /// Yazımın çağrı yerinden pimi: <see cref="CliApp.PlanText"/>'in kurduğu plan
    /// dökümünde Türkçe koşumun hiçbir sayısında nokta yok, İngilizce koşumun hiçbir
    /// sayısında virgül yok. Seam pimi tek başına yetmez — <c>Num</c>'un kültürü
    /// dilden alması ayrı bir karar.
    /// </summary>
    [Fact]
    public void PlanDokumununSayilariSecilenDilinAyraciyla()
    {
        var kaynak = new MediaInfo
        {
            FilePath = "kaynak.mp4",
            DurationSeconds = 61.5,
            Width = 1920,
            Height = 1080,
            Fps = 23.976,
            VideoCodec = "h264",
            FileSizeBytes = 41_000_000,
            TotalBitrateBps = 8_000_000
        };
        var secenek = new PlanOptions { TargetMb = 12.5 };
        var sonuc = PlanCalculator.BuildDetailed(kaynak, secenek, null);
        var karar = new CliDecision(kaynak, secenek, sonuc, sonuc.Profile, null, null, "cikti.mp4", Array.Empty<string>());
        var istek = new CliRequest { Command = CliCommand.Plan, Input = "kaynak.mp4" };

        var turkce = CliApp.PlanText(istek, karar, CliText.ForLanguage("tr"));
        var ingilizce = CliApp.PlanText(istek, karar, CliText.ForLanguage("en"));

        Assert.Contains("12,5 MB", turkce);
        Assert.Contains("23,98 fps", turkce);
        Assert.Contains("12.5 MB", ingilizce);
        Assert.Contains("23.98 fps", ingilizce);
        Assert.DoesNotContain("23.98", turkce);
        Assert.DoesNotContain("23,98", ingilizce);
    }

    /// <summary>
    /// Bayrak sistemin kültürünü eziyor. Sistem İngilizce kabul ediliyor; bayrak olmadan
    /// İngilizce kalması da ölçülüyor, yoksa Türkçe çıktı bayrağın değil kültürün eseri olabilirdi.
    /// </summary>
    [Fact]
    public async Task BayrakSistemDiliniEziyor()
    {
        var bayrakli = await Kos("--dil", "tr", "--help");
        var bayraksiz = await Kos("--help");

        Assert.Equal(0, bayrakli.Exit);
        Assert.Contains("Kullanım:", bayrakli.Out, StringComparison.Ordinal);
        Assert.DoesNotContain("Kullanım:", bayraksiz.Out, StringComparison.Ordinal);
        Assert.Contains("Usage:", bayraksiz.Out, StringComparison.Ordinal);
    }

    /// <summary>
    /// Yazımın dört yüzü aynı dile çıkıyor: uzun ad, İngilizce ad, eşittirli yazım ve
    /// bölgeli kod.
    /// </summary>
    [Theory]
    [InlineData("--dil", "tr")]
    [InlineData("--lang", "tr")]
    [InlineData("--dil=tr", null)]
    [InlineData("--lang", "TR-tr")]
    public async Task DortYazimAyniDiliVeriyor(string birinci, string? ikinci)
    {
        var args = ikinci is null ? new[] { birinci, "--help" } : new[] { birinci, ikinci, "--help" };

        var (exit, cikti, _) = await Kos(args);

        Assert.Equal(0, exit);
        Assert.Contains("Kullanım:", cikti, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tanınmayan dil sessizce İngilizce'ye düşmüyor: kullandığı kodu göremeyen kullanıcı
    /// bayrağın işlediğini sanırdı. Bilinen kod aynı yerden 64 almıyor — olumlu kontrol.
    /// </summary>
    [Fact]
    public async Task TaninmayanDilKullanimHatasi()
    {
        var kotu = await Kos("--dil", "de", "--help");
        var iyi = await Kos("--dil", "en", "--help");

        Assert.Equal(64, kotu.Exit);
        Assert.Contains("de", kotu.Err, StringComparison.Ordinal);
        Assert.Contains("en, tr", kotu.Err, StringComparison.Ordinal);
        Assert.Equal(0, iyi.Exit);
    }

    /// <summary>Değersiz bayrak da hata: <c>vidshrink --dil</c> sessizce yutulmuyor.</summary>
    [Fact]
    public async Task DegersizBayrakHata()
    {
        var (exit, _, err) = await Kos("--help", "--dil");

        Assert.Equal(64, exit);
        Assert.Contains("--dil", err, StringComparison.Ordinal);
    }

    /// <summary>
    /// Bayrak komut çözümleyicisine sızmıyor. Sızsaydı <c>error.unknown-option</c> ile 64
    /// dönerdi; burada ölçülen, ayıklanan listenin bayraktan arınmış ve sırasının korunmuş
    /// olması.
    /// </summary>
    [Fact]
    public void BayrakAyiklaniyorSiraBozulmuyor()
    {
        var secim = CliText.SplitLanguage(new[] { "kucult", "a.mp4", "--dil", "tr", "--hedef", "25MB" });

        Assert.Equal(new[] { "kucult", "a.mp4", "--hedef", "25MB" }, secim.Rest);
        Assert.Equal("tr", secim.Language);
        Assert.Null(secim.Invalid);
    }

    /// <summary>
    /// Ayıklayıcı benzeyen seçeneklere dokunmuyor: <c>--dilim</c> diye bir seçenek çıksa
    /// listede kalmalı, yoksa ayıklama kendi kapsamının dışına taşar.
    /// </summary>
    [Fact]
    public void BenzerSecenekAyiklanmiyor()
    {
        var secim = CliText.SplitLanguage(new[] { "--dilim", "3", "--langue=fr" });

        Assert.Equal(new[] { "--dilim", "3", "--langue=fr" }, secim.Rest);
        Assert.Null(secim.Language);
        Assert.Null(secim.Invalid);
    }

    /// <summary>Yardım metni bayrağı her iki dilde de anlatıyor.</summary>
    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    public void YardimBayragiAnlatiyor(string dil)
    {
        var satir = CliText.ForLanguage(dil)["help"]
            .Split('\n')
            .Where(s => s.TrimStart().StartsWith("--dil, --lang", StringComparison.Ordinal))
            .ToList();

        Assert.Single(satir);
    }
}
