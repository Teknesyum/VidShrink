using VidShrink.Cli;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// <c>--kes</c>'in iki yeni birimi (plan <c>docs/plan.md</c>): kare eki (<c>300f</c>) ve
/// kaynagin bolum isaretleri (<c>--bolum</c>). Ikisi de ayristirmada saniyeye cevrilmez —
/// donusum kaynagi ister, o yuzden <see cref="CliRequest.Resolved"/>'da olur. Eski saniye
/// ve saat yazimi hic degismedi; her kolun olumsuz kontrolu var.
/// </summary>
public sealed class KesBolumKareTests
{
    private static CliRequest Ayristir(params string[] args)
    {
        var sonuc = CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB" }.Concat(args).ToArray());
        Assert.True(sonuc.Ok, sonuc.ErrorKey);
        return sonuc.Request!;
    }

    private static string? Hata(params string[] args)
        => CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB" }.Concat(args).ToArray()).ErrorKey;

    private static MediaInfo Kaynak(IReadOnlyList<ChapterMark>? bolumler = null, double fps = 30) => new()
    {
        FilePath = "a.mp4",
        FileSizeBytes = 100_000_000L,
        DurationSeconds = 600,
        Width = 1920,
        Height = 1080,
        Fps = fps,
        VideoCodec = "h264",
        TotalBitrateBps = 5_000_000,
        Chapters = bolumler ?? Array.Empty<ChapterMark>()
    };

    private static IReadOnlyList<ChapterMark> UcBolum() => new[]
    {
        new ChapterMark(1, 0, 120, "Giris"),
        new ChapterMark(2, 120, 300, null),
        new ChapterMark(3, 300, 600, "Kapanis")
    };

    /// <summary>Kare eki ayristirmada saniyeye cevrilmiyor: alan ayri duruyor.</summary>
    [Fact]
    public void KareAyristirmadaCevrilmiyor()
    {
        var istek = Ayristir("--kes", "300f-900f");

        Assert.Equal(300, istek.TrimStartFrame);
        Assert.Equal(900, istek.TrimEndFrame);
        Assert.Null(istek.TrimStartSeconds);
        Assert.Null(istek.TrimEndSeconds);
    }

    /// <summary>Kare hizina bolunuyor; 25 fps'te ayni kare baska saniye veriyor.</summary>
    [Theory]
    [InlineData(30.0, 10.0, 30.0)]
    [InlineData(25.0, 12.0, 36.0)]
    [InlineData(60.0, 5.0, 15.0)]
    public void KareKaynaginHiziylaCevriliyor(double fps, double bas, double son)
    {
        Assert.Null(Ayristir("--kes", "300f-900f").Resolved(Kaynak(fps: fps), out var cozulen));

        Assert.Equal(bas, cozulen.TrimStartSeconds!.Value, 5);
        Assert.Equal(son, cozulen.TrimEndSeconds!.Value, 5);
    }

    /// <summary>Iki uc bagimsiz: bir ucu saat, obur ucu kare olabilir.</summary>
    [Fact]
    public void KarisikYazimGecerli()
    {
        var istek = Ayristir("--kes", "0:10-900f");
        Assert.Equal(10, istek.TrimStartSeconds);
        Assert.Equal(900, istek.TrimEndFrame);

        Assert.Null(istek.Resolved(Kaynak(), out var cozulen));
        Assert.Equal(10, cozulen.TrimStartSeconds!.Value, 5);
        Assert.Equal(30, cozulen.TrimEndSeconds!.Value, 5);
    }

    /// <summary>Eski yazim degismedi (olumsuz kontrol): eksiz sayi hala saniye.</summary>
    [Fact]
    public void EksizSayiHalaSaniye()
    {
        var istek = Ayristir("--kes", "10-40");

        Assert.Equal(10, istek.TrimStartSeconds);
        Assert.Equal(40, istek.TrimEndSeconds);
        Assert.Null(istek.TrimStartFrame);
        Assert.Null(istek.TrimEndFrame);
    }

    /// <summary>Kare yazimi sayi ister: kesirli, bos ya da harfli govde kesit degil.</summary>
    [Theory]
    [InlineData("10.5f-20f")]
    [InlineData("f-20f")]
    [InlineData("abcf-20f")]
    [InlineData("-10f")]
    [InlineData("900f-300f")]
    public void BozukKareReddediliyor(string deger)
        => Assert.Equal("error.bad-range", Hata("--kes", deger));

    /// <summary>Bolum numarasi ayristirmada kaynaga bakmadan duruyor; tek numara iki ucu da kurar.</summary>
    [Theory]
    [InlineData("2", 2, 2)]
    [InlineData("2-3", 2, 3)]
    [InlineData("1-3", 1, 3)]
    public void BolumAyristiriliyor(string deger, int bas, int son)
    {
        var istek = Ayristir("--bolum", deger);

        Assert.Equal(bas, istek.ChapterFrom);
        Assert.Equal(son, istek.ChapterTo);
        Assert.Null(istek.TrimStartSeconds);
    }

    /// <summary>Bolum sinirlari kaynaktan: 2-3 ikinci bolumun basindan ucuncunun sonuna.</summary>
    [Fact]
    public void BolumSinirlariKaynaktanGeliyor()
    {
        Assert.Null(Ayristir("--bolum", "2-3").Resolved(Kaynak(UcBolum()), out var aralik));
        Assert.Equal(120, aralik.TrimStartSeconds!.Value, 5);
        Assert.Equal(600, aralik.TrimEndSeconds!.Value, 5);

        Assert.Null(Ayristir("--bolum", "2").Resolved(Kaynak(UcBolum()), out var tek));
        Assert.Equal(120, tek.TrimStartSeconds!.Value, 5);
        Assert.Equal(300, tek.TrimEndSeconds!.Value, 5);
    }

    /// <summary>Aralik disi bolum ve bolumsuz kaynak ayri hatalar veriyor.</summary>
    [Fact]
    public void BolumKaynaktaYoksaHata()
    {
        Assert.Equal("error.bad-chapter", Ayristir("--bolum", "4").Resolved(Kaynak(UcBolum()), out _));
        Assert.Equal("error.no-chapters", Ayristir("--bolum", "1").Resolved(Kaynak(), out _));
    }

    /// <summary>Bozuk bolum yazimi ayristirmada reddediliyor.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("3-2")]
    [InlineData("a")]
    [InlineData("1-2-3")]
    public void BozukBolumReddediliyor(string deger)
        => Assert.Equal("error.bad-chapter", Hata("--bolum", deger));

    /// <summary>Iki kol da kesit penceresi kurdugu icin birlikte verilemiyor; sira onemli degil.</summary>
    [Fact]
    public void KesIleBolumBirlikteVerilemez()
    {
        Assert.Equal("error.chapter-and-cut", Hata("--kes", "10-40", "--bolum", "2"));
        Assert.Equal("error.chapter-and-cut", Hata("--bolum", "2", "--kes", "10-40"));
    }

    /// <summary>Kol verilmeyen istek cozulmeden gecer (olumsuz kontrol).</summary>
    [Fact]
    public void KolsuzIstekDokunulmadanGecer()
    {
        var istek = Ayristir("--kes", "10-40");
        Assert.Null(istek.Resolved(Kaynak(), out var cozulen));
        Assert.Equal(10, cozulen.TrimStartSeconds);
        Assert.Equal(40, cozulen.TrimEndSeconds);
    }

    /// <summary>Kare hizi okunamazsa kare yazimi uydurulmuyor, hata veriliyor.</summary>
    [Fact]
    public void KareHiziYoksaHata()
        => Assert.Equal("error.no-fps", Ayristir("--kes", "300f-900f").Resolved(Kaynak(fps: 0), out _));

    /// <summary>Cozulen pencere plan secenegine kesit olarak iniyor.</summary>
    [Fact]
    public void CozulenPencerePlanaGeciyor()
    {
        Ayristir("--bolum", "2").Resolved(Kaynak(UcBolum()), out var cozulen);
        var secenek = cozulen.ToPlanOptions(25, 600);

        Assert.Equal(180, secenek.Trim!.DurationSeconds, 3);
    }

    /// <summary>
    /// Cozum hatasi kullaniciya bicimlenmis iniyor: bolum numarasi cumlede gorunuyor,
    /// yer tutucu sizmiyor. Olcu sureç acmiyor — yalniz yoklama sahte.
    /// </summary>
    [Theory]
    [InlineData("4", "4")]
    [InlineData("2-4", "2-4")]
    public async Task AralikDisiBolumCumlesiNumarayiTasiyor(string deger, string beklenen)
    {
        var dosya = Path.Combine(Path.GetTempPath(), $"kes-{Guid.NewGuid():N}.mp4");
        await File.WriteAllTextAsync(dosya, "x");
        var stderr = new StringWriter();
        try
        {
            var exit = await CliApp.RunAsync(
                new[] { "kucult", dosya, "--hedef", "25MB", "--bolum", deger },
                new StringWriter(), stderr, CliText.ForLanguage("tr"),
                new CliServices
                {
                    MissingTool = () => null,
                    Probe = (_, _) => Task.FromResult(Kaynak(UcBolum())),
                    Availability = () => throw new InvalidOperationException("calisma acilmamali")
                },
                CancellationToken.None);

            Assert.Equal(ExitCodes.Usage, exit);
            Assert.Contains(beklenen, stderr.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("{0}", stderr.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(dosya);
        }
    }

    /// <summary>Iki kolun birlikte verilmesi yer tutucusuz bir cumle veriyor.</summary>
    [Theory]
    [InlineData("tr")]
    [InlineData("en")]
    public void BirlikteVerilemezCumlesindeYerTutucuYok(string dil)
    {
        var cumle = CliText.ForLanguage(dil).Format("error.chapter-and-cut", "2");

        Assert.DoesNotContain("{0}", cumle, StringComparison.Ordinal);
        Assert.DoesNotContain(": 2", cumle, StringComparison.Ordinal);
    }

    /// <summary>Dort yeni hata anahtari iki dil dosyasinda da var.</summary>
    [Theory]
    [InlineData("error.bad-chapter")]
    [InlineData("error.chapter-and-cut")]
    [InlineData("error.no-chapters")]
    [InlineData("error.no-fps")]
    public void YeniAnahtarlarIkiDildeDeVar(string anahtar)
    {
        foreach (var dil in CliText.Languages)
            Assert.True(CliText.Load(dil).ContainsKey(anahtar), $"{dil}: {anahtar}");
    }
}
