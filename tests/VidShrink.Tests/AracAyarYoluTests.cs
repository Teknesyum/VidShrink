using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Depo araçlarının hiçbiri makinede kurulu uygulamanın kendi ayarını yazmaz.
///
/// <para><c>MainWindow</c> yol verilmezse <c>%APPDATA%\VidShrink\settings.json</c>'i açar;
/// kardeş dosyalar (oynatıcı geçmişi, son açılanlar, kaydedici ayarı) da aynı klasörden
/// okunur. 13 Eylül 2026'da T191 kareleri çekilirken <c>VidShrink.Shot</c> pencereyi
/// İngilizce'ye çevirdi ve kapanırken kullanıcının dosyasına <c>language: en</c> ile
/// <c>autoUpdate: false</c> yazdı; masaüstündeki kurulumun otomatik güncellemesi o anda
/// kapandı ve 0.5.2 yayınlandığında kendiliğinden gelmedi. Deneme kliplerinin yolları da
/// kullanıcının "son açılanlar" listesine düştü.</para>
///
/// <para>Ölçü kaynağı okuyor çünkü kırılma başsız çekimde, testin göremediği bir süreçte
/// oluyor: <c>MainWindow</c> kuran her araç <c>SettingsPathOverride</c> yazmalı. Kural
/// tersinden de tutuyor — pencereyi kuran satır sayısı kadar geçersiz kılma satırı aranıyor,
/// ikinci bir pencere eklenip yolu verilmezse ölçü kırmızıya döner.</para>
/// </summary>
public class AracAyarYoluTests
{
    [Theory]
    [InlineData("tools/VidShrink.Shot/Program.cs")]
    public void AracKullanicininAyarDosyasiniYazmaz(string goreli)
    {
        var yol = Path.Combine(FindRoot(), goreli.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(yol), $"Kaynak yok: {yol}");

        var kaynak = File.ReadAllText(yol);
        var pencere = Say(kaynak, "new MainWindow");
        var gecersizKilma = Say(kaynak, "SettingsPathOverride");

        Assert.True(pencere > 0, "Araç MainWindow kurmuyor; ölçü yanlış dosyaya bakıyor.");
        Assert.True(
            gecersizKilma >= pencere,
            $"{goreli}: {pencere} pencere kuruluyor, {gecersizKilma} kez ayar yolu veriliyor.");
        Assert.Contains(".calisma", kaynak);
    }

    private static int Say(string kaynak, string parca)
    {
        var toplam = 0;
        var yer = kaynak.IndexOf(parca, StringComparison.Ordinal);
        while (yer >= 0)
        {
            toplam++;
            yer = kaynak.IndexOf(parca, yer + parca.Length, StringComparison.Ordinal);
        }

        return toplam;
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VidShrink.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
