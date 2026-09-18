namespace VidShrink.Tests;

/// <summary>
/// <see cref="MetinPimi.Duz"/>'un kendi olcusu. Normallestirici belge pimlerinin altinda
/// duruyor ama davranisi yalnizca o pimler uzerinden goruluyordu: iddiayi hem beklenen
/// metne hem belgeye uyguladigimiz icin zayiflatma kendi basina gorunmuyor, baska sinifin
/// ham beklentisine bagli kaliyordu. Burada dogrudan pimli.
/// </summary>
public sealed class MetinPimiTests
{
    /// <summary>Satir sarmasi kelimelerin arasindan kalkiyor; sira ve kelimeler duruyor.</summary>
    [Theory]
    [InlineData("iki\nsatir", "iki satir")]
    [InlineData("iki\r\nsatir", "iki satir")]
    [InlineData("cok    bosluk", "cok bosluk")]
    [InlineData("sekme\tarasi", "sekme arasi")]
    [InlineData("bos\n\n  satir", "bos satir")]
    public void BoslukDizisiTekBosluga_Iniyor(string ham, string beklenen)
        => Assert.Equal(beklenen, MetinPimi.Duz(ham));

    /// <summary>Iki uc kirpiliyor.</summary>
    [Fact]
    public void UclarKirpiliyor()
        => Assert.Equal("govde", MetinPimi.Duz("  \n govde \t\n "));

    /// <summary>Kelime ici hicbir sey degismiyor (olumsuz kontrol): noktalama ve buyuk harf duruyor.</summary>
    [Theory]
    [InlineData("`--kes` BAS-SON.")]
    [InlineData("Tek istisna `-o`: bunun tek yazimi var.")]
    public void TekBosluklu_MetinDokunulmadanGeciyor(string metin)
        => Assert.Equal(metin, MetinPimi.Duz(metin));

    /// <summary>
    /// Zayiflatmanin kendisi pimli: kimlik islevi bu olcuyu kirar. Normallestirici sarilmis
    /// bir cumleyi duz cumleye esitlemek zorunda, yoksa belge pimleri sarma konumuna baglanir.
    /// </summary>
    [Fact]
    public void SarilmisCumleDuzCumleyeEsitleniyor()
    {
        const string duz = "Bu cumle belgede iki satira sarilmis olabilir.";
        const string sarili = "Bu cumle belgede\niki satira\n  sarilmis olabilir.";

        Assert.NotEqual(duz, sarili);
        Assert.Equal(duz, MetinPimi.Duz(sarili));
    }
}
