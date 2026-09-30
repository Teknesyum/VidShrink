using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// Bütçe doldurmanın köşelemesi: ilk sonuç hedefin %97'si altında, yukarı deneme hedefin üstünde kaldığında
/// motor iki ölçümün arasından bir deneme daha ister. Kodlayıcı ffmpeg değil, bit hızından boyut yazan sahte
/// kodlayıcıdır; ölçülen SVT-AV1 kolunun biçimi (1,842 MB, sonra 2,007 MB) `docs/olcumler/kodlama-hizi-ve-boyut-sapmasi.md`.
/// </summary>
public sealed class BudgetFillBracketTests : IDisposable
{
    private const double Hedef = 2.0;
    private const double Sure = 4.0;
    private const int IlkK = 3600;
    private const double IlkMb = 1.842;

    private readonly string klasor = Path.Combine(AppContext.BaseDirectory, ".calisma", "koseleme-" + Guid.NewGuid().ToString("N"));

    public BudgetFillBracketTests() => Directory.CreateDirectory(klasor);

    public void Dispose()
    {
        try { Directory.Delete(klasor, recursive: true); } catch (IOException) { }
    }

    private static MediaInfo Kaynak() => new()
    {
        FilePath = "kaynak.mkv",
        FileSizeBytes = 7_881_000,
        DurationSeconds = Sure,
        Width = 1920,
        Height = 818,
        Fps = 24,
        VideoCodec = "hevc",
        TotalBitrateBps = 15_000_000
    };

    private static EncodePlan Plan(string codec) => new()
    {
        Codec = codec,
        Mode = "2pass",
        VideoBitrateK = IlkK,
        AudioCodec = null,
        AudioBitrateK = 0,
        Width = 1920,
        Height = 818,
        Fps = 24,
        Preset = "6"
    };

    private sealed class SahteKodlayici
    {
        private readonly Func<int, double> boyut;
        public readonly List<int> Istekler = new();

        public SahteKodlayici(Func<int, double> boyut) => this.boyut = boyut;

        public async Task Kodla(EncodePlan plan, string yol, int gecis)
        {
            if (gecis == 1) return;
            Istekler.Add(plan.VideoBitrateK);
            await File.WriteAllBytesAsync(yol, new byte[Megabayt.Tavan(boyut(plan.VideoBitrateK))]);
        }
    }

    private async Task<(EncodeResult Sonuc, SahteKodlayici Kodlayici)> Kos(Func<int, double> boyut, string codec = "libsvtav1")
    {
        var kodlayici = new SahteKodlayici(boyut);
        var runner = new EncodeRunner { SahteKodlayici = kodlayici.Kodla };
        var cikti = Path.Combine(klasor, "cikti.mkv");
        var sonuc = await runner.RunAsync(Kaynak(), Plan(codec), cikti, Hedef, progress: null, fillPolicy: FillPolicy.FillTarget);
        return (sonuc, kodlayici);
    }

    private static double Egri(int k, double us) => IlkMb * Math.Pow((double)k / IlkK, us);

    [Fact]
    public async Task YukariDenemeHedefiAsincaAradanUcuncuDenemeHedefinYuzdeUcIcineIner()
    {
        var (sonuc, kodlayici) = await Kos(k => Egri(k, 1.3));

        Assert.True(sonuc.Success);
        Assert.Equal(3, kodlayici.Istekler.Count);
        Assert.True(Egri(kodlayici.Istekler[1], 1.3) > Hedef, $"yukarı deneme {Egri(kodlayici.Istekler[1], 1.3):0.###} MB hedefi aşmalı");
        Assert.InRange(kodlayici.Istekler[2], IlkK + 1, kodlayici.Istekler[1] - 1);
        Assert.InRange(sonuc.OutputMb, BudgetFill.Floor * Hedef, Hedef);
        Assert.Equal(3, sonuc.DeliveredAttemptNumber);
        Assert.Equal(sonuc.OutputMb, Megabayt.Oku(new FileInfo(sonuc.OutputPath).Length));
    }

    [Fact]
    public async Task UcuncuDenemeDeAsarsaOncekiSonucTeslimEdilirTavanGevsemez()
    {
        var (sonuc, kodlayici) = await Kos(k => k > IlkK ? 2.05 : IlkMb);

        Assert.True(sonuc.Success);
        Assert.Equal(3, kodlayici.Istekler.Count);
        Assert.Equal(IlkMb, sonuc.OutputMb, 3);
        Assert.False(sonuc.OverTarget);
        Assert.Equal(1, sonuc.DeliveredAttemptNumber);
        Assert.True(new FileInfo(sonuc.OutputPath).Length <= Megabayt.Tavan(Hedef));
    }

    [Fact]
    public async Task NegatifKontrolYukariDenemeBandaInerseUcuncuDenemeYok()
    {
        var (sonuc, kodlayici) = await Kos(k => Egri(k, 1.0), "libx264");

        Assert.Equal(2, kodlayici.Istekler.Count);
        Assert.InRange(sonuc.OutputMb, BudgetFill.Floor * Hedef, Hedef);
    }

    [Fact]
    public void NegatifKontrolTeslimYuzdeDoksanYediUstundeyseKoselemeYok()
    {
        var teslim = Plan("libsvtav1");
        var asan = teslim.Clone();
        asan.VideoBitrateK = 3900;

        Assert.Null(BudgetFill.Bracket(teslim, 0.975 * Hedef, asan, 2.01, Hedef, Sure));
        Assert.NotNull(BudgetFill.Bracket(teslim, 0.965 * Hedef, asan, 2.01, Hedef, Sure));
        Assert.Null(BudgetFill.Bracket(teslim, 0.965 * Hedef, asan, 1.99, Hedef, Sure));
    }
}
