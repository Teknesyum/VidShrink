using Avalonia.Controls;
using Avalonia.Data;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Core.Share;
using VidShrink.App.Localization;

namespace VidShrink.Tests;

/// <summary>
/// AOT dalgasının pimleri. Hipersürüş H4 üç engel yazdı; buradaki ölçümler engellerin
/// kaldırılmasının davranışı bozmadığını, kaldırma biçiminin de geri kaymadığını tutar.
/// </summary>
public sealed class AotDalgasiTests
{
    /// <summary>
    /// <c>loc:Text</c> bağı derlenmiş bağa çevrildi. Ölçü bağın çıktısına bakıyor: hedefe
    /// gerçekten dilin metnini yazıyor mu, dil değişince kendiliğinden yeniliyor mu. Yol
    /// yanlış kurulsaydı hedef boş kalırdı, abonelik kopsaydı ikinci iddia düşerdi.
    /// </summary>
    [Fact]
    public void DerlenmisLocBagiMetniYaziyorVeDilDegisinceYeniliyor()
    {
        var anahtar = Strings.KeysOf("en")
            .OrderBy(k => k, StringComparer.Ordinal)
            .First(k => !string.Equals(Strings.GetIn("en", k), Strings.GetIn("tr", k), StringComparison.Ordinal));

        var (once, sonra, geri) = AppHost.Run(() =>
        {
            var blok = new TextBlock();
            blok.Bind(TextBlock.TextProperty, new TextExtension(anahtar).ProvideValue(null!));

            Strings.Use("en");
            var ingilizce = blok.Text;
            Strings.Use("tr");
            var turkce = blok.Text;
            Strings.Use("en");
            return (ingilizce, turkce, blok.Text);
        });

        Assert.Equal(LanguageCatalog.Display(Strings.GetIn("en", anahtar)), once);
        Assert.Equal(LanguageCatalog.Display(Strings.GetIn("tr", anahtar)), sonra);
        Assert.NotEqual(once, sonra);
        Assert.Equal(once, geri);
    }

    /// <summary>
    /// Aynı anahtar biçimlemede kaç yerde geçerse geçsin tek bağ nesnesi dolaşır: yol
    /// ifadesi bir kez gezilir, 493 <c>loc:Text</c> için değil. Her çağrıda yeni bağ
    /// kurulsaydı bu ölçü düşerdi.
    /// </summary>
    [Fact]
    public void AyniAnahtarinBagiTekNesne()
    {
        var (a, b, farkli) = AppHost.Run(() => (
            new TextExtension("settings").ProvideValue(null!),
            new TextExtension("settings").ProvideValue(null!),
            new TextExtension("language").ProvideValue(null!)));

        Assert.Same(a, b);
        Assert.NotSame(a, farkli);
    }

    /// <summary>
    /// Bağın türü: <see cref="Binding"/> yol dizgesini çalışma anında ayrıştırıp özelliği
    /// yansımayla arar ve AOT çözümlemesinde IL2026/IL3050 verir. Ölçü nesnenin türüne
    /// bakıyor, kaynak metnine değil: bağ yansımalı sınıfa geri döndürülürse düşer.
    /// </summary>
    [Fact]
    public void LocBagiYansimaliDegil()
    {
        var bag = AppHost.Run(() => new TextExtension("settings").ProvideValue(null!));

        Assert.IsType<CompiledBinding>(bag);
        Assert.IsNotType<Binding>(bag);
        Assert.IsNotType<ReflectionBinding>(bag);
    }
}

/// <summary>
/// JSON kaynak üretimine geçen çağrıların pimleri. Seçenekler artık bağlamın
/// niteliklerinde duruyor; nitelik ile yerini aldığı <c>JsonSerializerOptions</c>
/// ayrılırsa ayrıştırma sessizce değişir, bu yüzden ölçüler seçeneklerin etkisini
/// davranıştan okuyor.
/// </summary>
public sealed class AotJsonTests
{
    /// <summary>
    /// Gevşek bağlamın üç seçeneği: yorum atlanır, sondaki virgül bağışlanır, anahtar
    /// harfi önemsizdir. Üçü de artık <c>GevsekJson</c>'un niteliğinden geliyor; biri
    /// nitelikten düşerse bu ölçü kırılır.
    /// </summary>
    [Fact]
    public void GevsekBaglamYorumuSondakiVirguluVeBuyukHarfiKabulEdiyor()
    {
        var tablo = VidShrink.Core.Share.ShareTargetTable.Parse("""
            {
              // sürüm
              "VERSION": 3,
              "Default": "ornek",
            }
            """);

        Assert.Equal(3, tablo.Version);
        Assert.Equal("ornek", tablo.Default);
    }

    /// <summary>
    /// Paylaşım defteri girintili yazılıyor, boş alan hiç yazılmıyor ve yazdığını geri
    /// okuyor. Defteri okuyan yalnız uygulama değil: kullanıcı dosyayı da açıyor.
    /// </summary>
    [Fact]
    public void PaylasimDefteriGirintiliYaziliyorVeBosAlaniAtliyor()
    {
        var klasor = Path.Combine(Path.GetTempPath(), "vidshrink-aot-" + Guid.NewGuid().ToString("N"));
        var yol = Path.Combine(klasor, "share.json");
        try
        {
            Directory.CreateDirectory(klasor);
            new ShareLedger(yol).Add(new ShareLink("hedef", "dosya", "https://ornek/1", "klip.mp4", DateTimeOffset.UnixEpoch));

            var yazilan = File.ReadAllText(yol);
            Assert.Contains(Environment.NewLine, yazilan);
            Assert.DoesNotContain("null", yazilan);
            Assert.DoesNotContain("expiresAt", yazilan);
            Assert.DoesNotContain("ExpiresAt", yazilan);

            var geri = new ShareLedger(yol).Load();
            Assert.Equal("https://ornek/1", Assert.Single(geri).Url);
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    /// <summary>
    /// Tek örnek kanalı: kabuktan gelen yollar kodlanıp çözülünce birebir geri geliyor.
    /// Açılış yolundaki tek serileştirme bu.
    /// </summary>
    [Fact]
    public void TekOrnekKanaliYollariBirebirTasiyor()
    {
        var yollar = new[] { @"C:ir iki.mp4", "ünlü-ç-ş.mkv", "" };
        var geri = SingleInstanceChannel.Decode(SingleInstanceChannel.Encode(yollar));

        Assert.NotNull(geri);
        Assert.Equal(yollar, geri);
        Assert.Null(SingleInstanceChannel.Decode("{bozuk"));
    }
}

/// <summary>
/// NLS kipi (<c>System.Globalization.UseNls</c>) açılışta ICU yüklemesini düşürüyor; ölçüm
/// <c>iz:app-init</c>'te üç bağımsız koşumda da NLS lehine. Anahtarın bedava olmadığı tek
/// yer kültür verisi: bu pim anahtarın korumak zorunda olduğu sözleşmeyi ölçüyor. Süit hem
/// ICU hem <c>DOTNET_SYSTEM_GLOBALIZATION_USENLS=1</c> ile koşuluyor; ikisinde de yeşil
/// olmayan bir anahtar dalda kalmaz.
/// </summary>
public class KulturSozlesmesiTests
{
    [Fact]
    public void TurkceBuyukHarfKuraliKulturdenGeliyor()
    {
        Strings.Use("tr");
        try
        {
            Assert.StartsWith("İ", LanguageCatalog.Display("iptal"));
            Assert.DoesNotContain("Iptal", LanguageCatalog.Display("iptal"));
        }
        finally
        {
            Strings.Use("en");
        }

        Assert.StartsWith("I", LanguageCatalog.Display("import"));
    }

    [Fact]
    public void TurkceOndalikAyiriciVirgul()
    {
        var tr = Strings.CultureOf("tr");
        var en = Strings.CultureOf("en");

        Assert.Equal("15,6", 15.6d.ToString("0.0", tr));
        Assert.Equal("15.6", 15.6d.ToString("0.0", en));
        Assert.Equal("1.234", 1234.ToString("N0", tr));
    }

    [Fact]
    public void DilKoduKulturuBosDegil()
    {
        foreach (var dil in new[] { "tr", "en", "de", "zh-Hans" })
            Assert.False(string.IsNullOrEmpty(Strings.CultureOf(dil).Name), dil);
    }
}
