using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class BudgetFillTests
{
    private const double Hedef = 1000 * 10.0 / 8 / 1024;
    private const double Sure = 10;

    private static EncodePlan Teslim(int k = 950, string mode = "2pass", string codec = "libx264") => new()
    {
        Codec = codec,
        Mode = mode,
        VideoBitrateK = k,
        AudioCodec = null,
        AudioBitrateK = 0,
        Width = 1382,
        Height = 588,
        Fps = 24,
        Preset = "slow"
    };

    private static double Mb(double k, double verim) => k * Sure / Megabayt.Kbit / 0.995 * verim;

    [Fact]
    public void AltaDusenTeslimBirYukariDenemeTetikler()
    {
        var teslimMb = 0.92 * Hedef;

        Assert.True(BudgetFill.Wants(teslimMb, Hedef, attemptsUsed: 1, attemptLimit: 3, alreadyUsed: false));
        var plan = BudgetFill.Plan(Teslim(), teslimMb, Array.Empty<SizeSample>(), Hedef, Sure);

        Assert.NotNull(plan);
        Assert.Equal("2pass", plan!.Mode);
        Assert.True(plan.VideoBitrateK > 950, $"{plan.VideoBitrateK}k");
        var verim = teslimMb / Mb(950, 1.0);
        var beklenen = Mb(plan.VideoBitrateK, verim);
        Assert.InRange(beklenen, BudgetFill.Floor * Hedef, Hedef);
    }

    [Theory]
    [InlineData(0.97)]
    [InlineData(0.99)]
    [InlineData(1.0)]
    public void NegatifKontrolYuzdeDoksanYediVeUstuTeslimYukariDenemeTetiklemez(double oran)
    {
        var teslimMb = oran * Hedef;

        Assert.False(BudgetFill.Wants(teslimMb, Hedef, 1, 3, false));
        Assert.Null(BudgetFill.Plan(Teslim(), teslimMb, Array.Empty<SizeSample>(), Hedef, Sure));
    }

    [Theory]
    [InlineData(0.9650, true)]
    [InlineData(0.9699, true)]
    [InlineData(0.9750, false)]
    [InlineData(0.9800, false)]
    public void EsigiAsagiYaDaYukariKaydiranMutasyonTeslimKararindaGoruluyor(double oran, bool bekleniyor)
    {
        var teslimMb = oran * Hedef;

        Assert.Equal(bekleniyor, BudgetFill.Wants(teslimMb, Hedef, 1, 3, false));
        Assert.Equal(bekleniyor, BudgetFill.Plan(Teslim(), teslimMb, Array.Empty<SizeSample>(), Hedef, Sure) is not null);
    }

    [Fact]
    public void TavaniAsanYukariDenemeTeslimEdilmezOncekiSonucKalir()
    {
        Assert.False(BudgetFill.Keeps(previousMb: 0.90 * Hedef, topUpMb: 1.001 * Hedef, targetMb: Hedef));
    }

    [Fact]
    public void NegatifKontrolTavanAltindaDahaDoluYukariDenemeTeslimEdilir()
    {
        Assert.True(BudgetFill.Keeps(previousMb: 0.90 * Hedef, topUpMb: 0.985 * Hedef, targetMb: Hedef));
        Assert.True(BudgetFill.Keeps(previousMb: 0.90 * Hedef, topUpMb: Hedef, targetMb: Hedef));
    }

    [Fact]
    public void DahaKucukCikanYukariDenemeDeTeslimEdilmez()
    {
        Assert.False(BudgetFill.Keeps(previousMb: 0.90 * Hedef, topUpMb: 0.89 * Hedef, targetMb: Hedef));
    }

    [Theory]
    [InlineData(4, 3, false)]
    [InlineData(5, 3, false)]
    [InlineData(2, 1, false)]
    [InlineData(1, 3, true)]
    public void DenemeButcesiAsilmaz(int kullanilan, int sinir, bool kullanildi)
    {
        Assert.False(BudgetFill.Wants(0.90 * Hedef, Hedef, kullanilan, sinir, kullanildi));
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(1, 3)]
    [InlineData(5, 5)]
    public void NegatifKontrolButceIcindeYukariDenemeAcik(int kullanilan, int sinir)
    {
        Assert.True(BudgetFill.Wants(0.90 * Hedef, Hedef, kullanilan, sinir, false));
    }

    [Fact]
    public void ButceSiniriKosuSinirininBirFazlasi()
    {
        Assert.Equal(1, BudgetFill.ExtraAttempts);
        Assert.True(BudgetFill.Wants(0.90 * Hedef, Hedef, 3, 3, false));
        Assert.False(BudgetFill.Wants(0.90 * Hedef, Hedef, 4, 3, false));
    }

    [Fact]
    public void TavanUstuOrnekIstegiAradegerleSinirlar()
    {
        var teslimMb = 0.92 * Hedef;
        var ornekler = new[] { new SizeSample(1000, 1.35, true) };

        var plan = BudgetFill.Plan(Teslim(), teslimMb, ornekler, Hedef, Sure);

        Assert.NotNull(plan);
        var aradeger = 950 + (1000 - 950) * (BudgetFill.Aim * Hedef - teslimMb) / (1.35 - teslimMb);
        Assert.Equal((int)Math.Floor(aradeger), plan!.VideoBitrateK);
        Assert.True(plan.VideoBitrateK < 1000);
    }

    [Fact]
    public void NegatifKontrolOrneksizVeAltIstekliOrnekteOlcekKullanilir()
    {
        var teslimMb = 0.92 * Hedef;
        var olcek = (int)Math.Floor(950 * (BudgetFill.Aim * Hedef) / teslimMb);

        var orneksiz = BudgetFill.Plan(Teslim(), teslimMb, Array.Empty<SizeSample>(), Hedef, Sure);
        var altIstek = BudgetFill.Plan(Teslim(), teslimMb, new[] { new SizeSample(900, 1.35, true), new SizeSample(1000, 1.0, false) }, Hedef, Sure);

        Assert.Equal(olcek, orneksiz!.VideoBitrateK);
        Assert.Equal(olcek, altIstek!.VideoBitrateK);
        Assert.True(olcek > 1000);
    }

    [Fact]
    public void TeslimdenYukariDahaAzIstekKurulamiyorsaPlanYok()
    {
        var ornekler = new[] { new SizeSample(951, 1.35, true) };

        Assert.Null(BudgetFill.Plan(Teslim(), 0.92 * Hedef, ornekler, Hedef, Sure));
    }

    [Fact]
    public void CrfTeslimIkiGecisliPlanaDonerVeIstegiCikanBaytanKurar()
    {
        var teslimMb = 0.90 * Hedef;

        var plan = BudgetFill.Plan(Teslim(0, "crf"), teslimMb, Array.Empty<SizeSample>(), Hedef, Sure);

        Assert.NotNull(plan);
        Assert.Equal("2pass", plan!.Mode);
        Assert.Null(plan.Crf);
        Assert.InRange(Mb(plan.VideoBitrateK, 1.0), BudgetFill.Floor * Hedef, Hedef);
    }

    /// <summary>
    /// NVENC'te yukari deneme 22 Eylul 2026'dan beri kuruluyor. Nisan 0,97 idi
    /// (<c>docs/olcumler/nvenc-4-teslim.md</c>); 0,97 tabanin kendisi oldugu icin yukari deneme
    /// teslimi ancak tabana kadar tasiyordu. 23 Eylul'de yazilim nisanina (0,985) cekildi
    /// (<c>docs/olcumler/d1-donanim-dolum.md</c>): ayni 18 hucrede ortalama teslim 0,9750'den
    /// 0,9780'e, yedi hucre yukari, hic hucre asagi, hedefi asan teslim yok.
    /// </summary>
    [Theory]
    [InlineData("h264_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("av1_nvenc")]
    public void NvencteYukariDenemeYuzdeDoksanSekizBucugaNisanlanir(string codec)
    {
        var teslimMb = 0.90 * Hedef;

        var plan = BudgetFill.Plan(Teslim(codec: codec), teslimMb, Array.Empty<SizeSample>(), Hedef, Sure);

        Assert.NotNull(plan);
        Assert.Equal(1039, plan!.VideoBitrateK);
        Assert.Contains("aims at 0.985 of the target", plan.Reason);
    }

    /// <summary>
    /// Olculen hucre (d1-donanim-dolum.md, karanlik / hevc_nvenc / 2000 kbit): 1945k tavani asti
    /// (2,514 MB), 1860k bantta 2,358 MB. 0,97 nisaniyla yukari deneme 1865k istiyor ve 2,358 MB
    /// cikip atiliyordu; 0,985 ile 1885k istiyor ve 2,367 MB teslim ediliyor. Istek iki olcum
    /// arasindaki aradegerden.
    /// </summary>
    [Fact]
    public void NvencOlculenHucredeYukariDenemeTavanOrnegiyleAradegerlenenYaziliminNisaniniIster()
    {
        var hedef = 2000 * 10.0 / 8 / 1024;
        var teslim = new EncodePlan { Codec = "hevc_nvenc", Mode = "2pass", VideoBitrateK = 1860, AudioCodec = null, AudioBitrateK = 0, Width = 1882, Height = 802, Fps = 24, Preset = "p4" };
        var ornekler = new[] { new SizeSample(1945, 2.514, true), new SizeSample(1860, 2.3577, false) };

        var nvenc = BudgetFill.Plan(teslim, 2.3577, ornekler, hedef, Sure);
        var x265 = teslim.Clone();
        x265.Codec = "libx265";
        var yazilim = BudgetFill.Plan(x265, 2.3577, ornekler, hedef, Sure);

        Assert.Equal(1885, nvenc!.VideoBitrateK);
        Assert.Equal(yazilim!.VideoBitrateK, nvenc.VideoBitrateK);
        Assert.True(BudgetFill.Keeps(2.3577, 2.3674, hedef));
    }

    [Fact]
    public void NvencteTavanUstuOrnekIstegiNvencNisaniylaAradegerler()
    {
        var teslimMb = 0.92 * Hedef;
        var ornekler = new[] { new SizeSample(1000, 1.35, true) };

        var plan = BudgetFill.Plan(Teslim(codec: "av1_nvenc"), teslimMb, ornekler, Hedef, Sure);

        var aradeger = 950 + (1000 - 950) * (0.985 * Hedef - teslimMb) / (1.35 - teslimMb);
        Assert.Equal((int)Math.Floor(aradeger), plan!.VideoBitrateK);
    }
    /// <summary>
    /// NVENC disindaki donanim kolunda yukari deneme kurulmaz: teslim yayilimlari olculmedi.
    /// NVENC'in kapisi <c>docs/olcumler/nvenc-butce-doldurma.md</c>'de kapali birakilmisti,
    /// <c>nvenc-4-teslim.md</c>'de yeniden olculup acildi.
    /// </summary>
    [Theory]
    [InlineData("h264_qsv")]
    [InlineData("hevc_amf")]
    [InlineData("hevc_videotoolbox")]
    public void DonanimKodlayicidaYukariDenemeKurulmaz(string codec)
    {
        Assert.Null(BudgetFill.Plan(Teslim(codec: codec), 0.90 * Hedef, Array.Empty<SizeSample>(), Hedef, Sure));
    }

    [Theory]
    [InlineData("libx264")]
    [InlineData("libx265")]
    [InlineData("libsvtav1")]
    public void NegatifKontrolYazilimKodlayicidaYukariDenemeKurulur(string codec)
    {
        Assert.NotNull(BudgetFill.Plan(Teslim(codec: codec), 0.90 * Hedef, Array.Empty<SizeSample>(), Hedef, Sure));
    }
}