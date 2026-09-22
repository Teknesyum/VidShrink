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

    private static double Mb(double k, double verim) => k * Sure / 8388.608 / 0.995 * verim;

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
    /// NVENC'te yukari deneme 22 Eylul 2026'dan beri kuruluyor, nisani 0,97
    /// (<c>docs/olcumler/nvenc-4-teslim.md</c>): 18 hucrede ortalama teslim 0,9637'den 0,9712'ye,
    /// en kotu hucre 0,8656'dan 0,8847'ye cikti, hedefi asan hucre olmadi. 0,985 ortalamada
    /// biraz daha iyi (0,9736) ama on yukari denemenin dordu bosa gitti ve en kotu hucreyi
    /// kaldiramadi. Nisan yaziliminkinden ayri, istegin kendisiyle pimli.
    /// </summary>
    [Theory]
    [InlineData("h264_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("av1_nvenc")]
    public void NvencteYukariDenemeYuzdeDoksanYediyeNisanlanir(string codec)
    {
        var teslimMb = 0.90 * Hedef;

        var plan = BudgetFill.Plan(Teslim(codec: codec), teslimMb, Array.Empty<SizeSample>(), Hedef, Sure);

        Assert.NotNull(plan);
        Assert.Equal((int)Math.Floor(950 * 0.97 * Hedef / teslimMb), plan!.VideoBitrateK);
        Assert.Contains("aims at 0.970 of the target", plan.Reason);
    }

    [Fact]
    public void NegatifKontrolYazilimNisaniNvencinkindenYuksek()
    {
        var teslimMb = 0.90 * Hedef;

        var yazilim = BudgetFill.Plan(Teslim(codec: "libx265"), teslimMb, Array.Empty<SizeSample>(), Hedef, Sure);
        var nvenc = BudgetFill.Plan(Teslim(codec: "hevc_nvenc"), teslimMb, Array.Empty<SizeSample>(), Hedef, Sure);

        Assert.Equal((int)Math.Floor(950 * 0.985 * Hedef / teslimMb), yazilim!.VideoBitrateK);
        Assert.True(yazilim.VideoBitrateK > nvenc!.VideoBitrateK, $"{yazilim.VideoBitrateK}k / {nvenc.VideoBitrateK}k");
    }

    [Fact]
    public void NvencteTavanUstuOrnekIstegiNvencNisaniylaAradegerler()
    {
        var teslimMb = 0.92 * Hedef;
        var ornekler = new[] { new SizeSample(1000, 1.35, true) };

        var plan = BudgetFill.Plan(Teslim(codec: "av1_nvenc"), teslimMb, ornekler, Hedef, Sure);

        var aradeger = 950 + (1000 - 950) * (0.97 * Hedef - teslimMb) / (1.35 - teslimMb);
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