using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using VidShrink.App.Localization;
using VidShrink.App.Recorder;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

/// <summary>
/// Kaydedicinin hedef kutuları makinenin kültürünü okuyup yazıyordu, arayüzün dilini
/// değil. Aynı pencerede iki yazım çarpışıyordu: kutunun altındaki bütçe notu
/// <c>Strings.Culture</c> ile <c>12,5</c> derken kutunun kendisi makinenin ayracıyla
/// doluyordu. İngilizce arayüzü açan Türkçe bir makinede kullanıcı kutuda <c>12,5</c>
/// görüyor ama panelin geri kalanı <c>12.5</c> diyordu; düzeltmek için <c>12.5</c>
/// yazınca kutu bunu hiç okuyamıyor — <see cref="System.Globalization.NumberStyles.Float"/>
/// binlik ayracı kabul etmediği için sayı <em>büyümüyor</em>, geçersiz sayılıyor ve
/// hedef sessizce düşüyordu. Ölçümde görülen hüküm budur.
///
/// <para>Ölçü iki yönü de tutuyor: kullanıcının yazdığının okunması (kutu → ayar
/// dosyası) ve ayardan gelenin yazılması (ayar dosyası → kutu). Tek yön pimlenirse
/// ikisini birden makinenin kültürüne çevirmek yine yeşil kalırdı.</para>
/// </summary>
public sealed class KaydediciHedefYazimiTests
{
    private static double? DosyadakiMb(string ayarYolu)
        => (double?)(JsonNode.Parse(File.ReadAllText(ayarYolu)) is JsonObject kok ? kok["targetMegabytes"] : null);

    private static double? Okunan(string dil, string yazilan) => AyarDosyasiyla(ayarYolu =>
    {
        AppHost.Run(() =>
        {
            var onceki = Strings.Language;
            Strings.Use(dil);
            try
            {
                var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true };
                Yaz(view, "TxtTargetMegabytes", yazilan);
            }
            finally { Strings.Use(onceki); }
        });

        return DosyadakiMb(ayarYolu);
    });

    private static string Yazilan(string dil, double mb) => AyarDosyasiyla(ayarYolu =>
    {
        AppHost.Run(() =>
        {
            var onceki = Strings.Language;
            Strings.Use(dil);
            try
            {
                var kuran = new RecorderView(ayarYolu) { SkipAutoMeasure = true };
                Yaz(kuran, "TxtTargetMegabytes", mb.ToString("0.###", Strings.Culture));
            }
            finally { Strings.Use(onceki); }
        });

        return AppHost.Run(() =>
        {
            var onceki = Strings.Language;
            Strings.Use(dil);
            try
            {
                var acan = new RecorderView(ayarYolu) { SkipAutoMeasure = true };
                return Bul<TextBox>(acan, "TxtTargetMegabytes").Text ?? string.Empty;
            }
            finally { Strings.Use(onceki); }
        });
    });

    [Fact]
    public void HedefKutusuArayuzunAyraciylaOkunuyor()
    {
        Assert.Equal(12.5, Okunan("tr", "12,5"));
        Assert.Equal(12.5, Okunan("en", "12.5"));
    }

    /// <summary>
    /// Olumsuz kontrol: yanlış dilin ayracı aynı sayıya çıkmıyor. Türkçe arayüzde
    /// <c>12.5</c> binlik ayracı sayılıp <c>125</c> oluyor — kutunun kültürü gerçekten
    /// okunuyor, herhangi bir ayraç kabul edilmiyor.
    /// </summary>
    [Fact]
    public void YabanciAyracAyniSayiyaCikmiyor()
    {
        Assert.NotEqual(12.5, Okunan("tr", "12.5"));
        Assert.NotEqual(12.5, Okunan("en", "12,5"));
    }

    [Fact]
    public void AyardanGelenHedefArayuzunAyraciylaYaziliyor()
    {
        Assert.Equal("12,5", Yazilan("tr", 12.5));
        Assert.Equal("12.5", Yazilan("en", 12.5));
    }

    /// <summary>
    /// Kutu ile altındaki bütçe notu aynı sayıyı görmeli. Türkçe arayüzde
    /// <c>12,5</c> ile <c>12.5</c> aynı notu üretirse kutunun kültürü not tarafına
    /// geçmiyor demektir: ikisi on kat farklı bütçe, dolayısıyla farklı bit hızı.
    /// </summary>
    [Fact]
    public void ButceNotuKutununOkudugunuKullaniyor()
    {
        static string Not(string yazilan) => AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var onceki = Strings.Language;
            Strings.Use("tr");
            try
            {
                var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true };
                Yaz(view, "TxtTargetSeconds", "30");
                Yaz(view, "TxtTargetMegabytes", yazilan);
                return Bul<TextBlock>(view, "TxtBudgetNote").Text ?? string.Empty;
            }
            finally { Strings.Use(onceki); }
        }));

        var ondalik = Not("12,5");
        var yuzYirmiBes = Not("125");
        var yabanci = Not("12.5");

        Assert.NotEmpty(ondalik);
        Assert.NotEqual(ondalik, yuzYirmiBes);
        Assert.NotEqual(ondalik, yabanci);
        Assert.NotEqual(yuzYirmiBes, yabanci);
    }
}
