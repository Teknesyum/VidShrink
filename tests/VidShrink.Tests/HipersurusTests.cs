using System.Text;
using Avalonia;
using VidShrink.App.Themes;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Hipersürüş C dalgasının pimleri: yayın anahtarları, açılış perdesi ve paletin kısa
/// devresi. Üçü de ölçülmüş bir gecikmeye karşılık geliyor ve üçü de sessizce geri
/// alınabilir — bir satır silinince kimse fark etmez, açılış yine uzar. Ölçümün kendisi
/// <c>docs/olcumler/acilis-hizi.md</c>'de.
/// </summary>
public sealed class HipersurusTests
{
    private readonly ITestOutputHelper _cikti;

    public HipersurusTests(ITestOutputHelper cikti) => _cikti = cikti;

    private static string Oku(params string[] parcalar)
        => File.ReadAllText(Path.Combine(new[] { TipSources.Root }.Concat(parcalar).ToArray()));

    /// <summary>
    /// C1. İki yayın da önceden derlenmiş kodla çıkıyor. Anahtarlar kalkarsa açılışta bütün
    /// IL yeniden JIT'lenir ve ölçülen kazanç geri verilir.
    /// </summary>
    [Fact]
    public void YayinOncedenDerlenmisKodlaCikiyor()
    {
        var uygulama = Oku("src", "VidShrink.App", "VidShrink.App.csproj");
        var baslatici = Oku("src", "VidShrink.Launcher", "VidShrink.Launcher.csproj");

        foreach (var (ad, metin) in new[] { ("uygulama", uygulama), ("baslatici", baslatici) })
        {
            Assert.True(metin.Contains("<PublishReadyToRun>true</PublishReadyToRun>", StringComparison.Ordinal),
                $"{ad}: PublishReadyToRun yok");
            Assert.True(metin.Contains("<TieredPGO>true</TieredPGO>", StringComparison.Ordinal),
                $"{ad}: TieredPGO yok");
        }

        Assert.DoesNotContain("<PublishAot>true</PublishAot>", uygulama, StringComparison.Ordinal);
    }

    /// <summary>
    /// C2. Perde iki kolda da kuruluyor, adı çocuk sürece geçiyor ve <b>uygulama doğduktan
    /// sonra</b> bekleniyor: önce beklenirse perde uygulamanın açılmasını geciktirir.
    /// </summary>
    [Fact]
    public void PerdeIkiKoldaDaUygulamadanSonraBekleniyor()
    {
        var kod = Oku("src", "VidShrink.Launcher", "Program.cs");

        var acmalar = System.Text.RegularExpressions.Regex.Matches(kod, @"AcilisPerdesi\.Ac\(\)");
        var dogumlar = System.Text.RegularExpressions.Regex.Matches(kod, @"StartApp\(executable");
        var beklemeler = System.Text.RegularExpressions.Regex.Matches(kod, @"BekleVeKapat\(\)");

        Assert.Equal(2, acmalar.Count);
        Assert.Equal(2, dogumlar.Count);
        Assert.Equal(2, beklemeler.Count);

        for (var at = 0; at < 2; at++)
        {
            Assert.True(acmalar[at].Index < dogumlar[at].Index, "perde uygulamadan sonra aciliyor");
            Assert.True(dogumlar[at].Index < beklemeler[at].Index, "perde uygulama dogmadan bekleniyor");
        }

        Assert.Contains("start.Environment[AcilisPerdesi.Degisken] = perdeAdi;", kod, StringComparison.Ordinal);
    }

    /// <summary>
    /// C2. Perdenin iki yakası aynı adı kullanıyor. Değişken adı bir tarafta değişirse perde
    /// hiç kalkmaz ve kullanıcı tavan dolana kadar panele bakar.
    /// </summary>
    [Fact]
    public void PerdeninIkiYakasiAyniDegiskeni_Kullaniyor()
    {
        const string degisken = "\"VIDSHRINK_ACILIS_PERDESI\"";
        Assert.Contains(degisken, Oku("src", "VidShrink.Launcher", "AcilisPerdesi.cs"), StringComparison.Ordinal);
        Assert.Contains(degisken, Oku("src", "VidShrink.App", "AcilisPerdesi.cs"), StringComparison.Ordinal);
    }

    /// <summary>
    /// C2. Perde sıfır eşikle açılıyor ve kapanırken çubuğun dolmasını beklemiyor. Kurulum
    /// paneli eski davranışında kalıyor: eşiği dolmadan çizilmiyor.
    /// </summary>
    [Fact]
    public void PerdeSifirEsikle_AcilipBeklemedenKapaniyor()
    {
        var perde = Oku("src", "VidShrink.Launcher", "AcilisPerdesi.cs");
        Assert.Contains("SplashGate.Arm(ilerleme, TimeSpan.Zero)", perde, StringComparison.Ordinal);
        Assert.Contains("_kapi.Kapat()", perde, StringComparison.Ordinal);

        var splash = Oku("src", "VidShrink.Launcher", "Splash.cs");
        Assert.Contains("public static SplashGate Arm(InstallProgress progress) => Arm(progress, Threshold);", splash, StringComparison.Ordinal);
        Assert.Contains("AcilisIzi.Yaz(\"perde\")", splash, StringComparison.Ordinal);

        var kapat = splash.IndexOf("public void Kapat()", StringComparison.Ordinal);
        var dispose = splash.IndexOf("public void Dispose()", StringComparison.Ordinal);
        Assert.True(kapat > 0 && dispose > kapat, "Kapat, Dispose'dan once tanimli degil");
        Assert.DoesNotContain("_settling = true;", splash[kapat..dispose], StringComparison.Ordinal);
    }

    /// <summary>
    /// C2. Uygulama perdeyi ilk karede kaldırıyor; dosya hiç açılamazsa açılışın sonu onu
    /// yine kaldırıyor. İkinci yol olmadan çökmüş bir açılış perdeyi ekranda bırakır.
    /// </summary>
    [Fact]
    public void UygulamaPerdeyiIlkKaredeVeAcilisinSonundaKaldiriyor()
    {
        var kod = Oku("src", "VidShrink.App", "MainWindow.axaml.cs");
        Assert.Contains("PerdeyiIzle();", kod, StringComparison.Ordinal);
        Assert.Contains("if (Player.Frame.Source is null) return;", kod, StringComparison.Ordinal);
        Assert.Contains("AcilisPerdesi.Kapat();", kod, StringComparison.Ordinal);

        var izle = kod.IndexOf("private void PerdeyiIzle()", StringComparison.Ordinal);
        Assert.True(izle > 0, "PerdeyiIzle yok");
    }

    /// <summary>
    /// C3. Yürürlükteki paleti yeniden seçmek hiçbir iş yapmıyor: sözlüğün ilk sırası aynı
    /// nesne kalıyor. Gerçek geçişte değişiyor — negatif kontrol o. Ölçülen payı 194,1 ms ve
    /// açılışta bu hal kuraldı.
    /// </summary>
    [Fact]
    public void AyniPaletiYenidenSecmek_HicbirSeyYapmiyor()
    {
        var (ayni, degisen, dokum) = AppHost.Run(() =>
        {
            PaletteCatalog.Use(PaletteCatalog.Default);
            var once = Application.Current!.Resources.MergedDictionaries[0];

            PaletteCatalog.Use(PaletteCatalog.Default);
            var tekrar = Application.Current!.Resources.MergedDictionaries[0];

            var oteki = PaletteCatalog.Names.First(ad => ad != PaletteCatalog.Default);
            PaletteCatalog.Use(oteki);
            var sonra = Application.Current!.Resources.MergedDictionaries[0];

            PaletteCatalog.Use(PaletteCatalog.Default);

            var yazi = new StringBuilder()
                .AppendLine($"ilk         = {once.GetHashCode()}")
                .AppendLine($"tekrar ayni = {tekrar.GetHashCode()}")
                .AppendLine($"gercek gecis= {sonra.GetHashCode()}")
                .ToString();

            return (ReferenceEquals(once, tekrar), ReferenceEquals(once, sonra), yazi);
        });

        _cikti.WriteLine(dokum);
        Assert.True(ayni, "Ayni palet yeniden secildi ve sozluk yine degistirildi:\n" + dokum);
        Assert.False(degisen, "Gercek palet gecisi sozlugu degistirmiyor:\n" + dokum);
    }
}
