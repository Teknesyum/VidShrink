using System.IO;
using VidShrink.App;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Ayarlardaki "Sabit klasör" kipinin gerçekten çalıştığının ölçüsü. Kip kaydediliyor,
/// geri yükleniyor ve satırı gösterip gizliyordu — ama çıktı yolu yalnız kaynağın
/// klasöründen kuruluyordu. Arayüzdeki ipucu ise <c>her çıktıyı kaynağın yanı yerine hep
/// aynı yere gönderir</c> diyordu; kullanıcıya söylenen ile olan farklıydı.
///
/// <para>Sessiz geri düşüş de ölçünün konusu: klasör kullanılamıyorsa çıktı kaynağın
/// yanına düşer, ama bu kullanıcıya söylenir. Söylenmeyen geri düşüş ayarı yalan yapar.</para>
/// </summary>
public sealed class SabitCiktiKlasoruTests
{
    private static string GeciciKlasor(string ad)
    {
        var yol = Path.Combine(TipSources.Root, ".calisma", "sabit-cikti-klasoru", ad);
        if (Directory.Exists(yol)) Directory.Delete(yol, recursive: true);
        Directory.CreateDirectory(yol);
        return yol;
    }

    [Fact]
    public void SecilenKlasoreYaziliyor()
    {
        var kaynak = GeciciKlasor("kaynak-1");
        var hedef = GeciciKlasor("hedef-1");
        var girdi = Path.Combine(kaynak, "klip.mp4");

        var cikti = ShrinkEngine.UniqueOutputPath(girdi, "shrunk", "mp4", hedef);

        Assert.Equal(hedef, Path.GetDirectoryName(cikti));
        Assert.Equal("klip_shrunk.mp4", Path.GetFileName(cikti));
    }

    /// <summary>Olumsuz kontrol: klasör verilmezse çıktı kaynağın yanında kalır.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void KlasorVerilmezseKaynaginYaninda(string? klasor)
    {
        var kaynak = GeciciKlasor("kaynak-2");
        var girdi = Path.Combine(kaynak, "klip.mp4");

        var cikti = ShrinkEngine.UniqueOutputPath(girdi, "shrunk", "mp4", klasor);

        Assert.Equal(kaynak, Path.GetDirectoryName(cikti));
    }

    /// <summary>
    /// Çakışma sayacı hedef klasörde işler: aynı adlı dosya orada varsa sayı artar,
    /// kaynağın klasöründeki aynı adlı dosya ise sayacı tetiklemez.
    /// </summary>
    [Fact]
    public void CakismaSayaciHedefKlasordeIsliyor()
    {
        var kaynak = GeciciKlasor("kaynak-3");
        var hedef = GeciciKlasor("hedef-3");
        var girdi = Path.Combine(kaynak, "klip.mp4");

        File.WriteAllBytes(Path.Combine(kaynak, "klip_shrunk.mp4"), []);
        var ilk = ShrinkEngine.UniqueOutputPath(girdi, "shrunk", "mp4", hedef);
        Assert.Equal("klip_shrunk.mp4", Path.GetFileName(ilk));

        File.WriteAllBytes(Path.Combine(hedef, "klip_shrunk.mp4"), []);
        var ikinci = ShrinkEngine.UniqueOutputPath(girdi, "shrunk", "mp4", hedef);
        Assert.Equal("klip_shrunk_2.mp4", Path.GetFileName(ikinci));
        Assert.Equal(hedef, Path.GetDirectoryName(ikinci));
    }

    [Fact]
    public void KaynaginYaniKipindeSabitKlasorOkunmuyor()
    {
        var hedef = GeciciKlasor("hedef-4");
        Assert.Null(MainWindow.UsableFixedFolder(0, hedef));
    }

    [Fact]
    public void YazilabilirKlasorKullaniliyor()
    {
        var hedef = GeciciKlasor("hedef-5");
        Assert.Equal(hedef, MainWindow.UsableFixedFolder(1, hedef));
        Assert.Empty(Directory.GetFiles(hedef));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BosKlasorKullanilamaz(string? klasor)
    {
        Assert.Null(MainWindow.UsableFixedFolder(1, klasor));
    }

    [Fact]
    public void OlmayanKlasorKullanilamaz()
    {
        var yok = Path.Combine(GeciciKlasor("hedef-6"), "hic-olmayan-alt-klasor");
        Assert.False(Directory.Exists(yok));
        Assert.Null(MainWindow.UsableFixedFolder(1, yok));
    }

    /// <summary>
    /// Var olmak yetmez: yazılamayan klasöre çıktı gitmez. Yazma hakkı açıkça reddedilmiş
    /// bir klasörle ölçülür — yoklama olmadan bu kol sessizce "kullanılabilir" derdi.
    /// Yalnız Windows: Linux CI konteynerinde kök kullanıcı izni geçersiz kılıyor.
    /// </summary>
    [Fact]
    public void YazilamayanKlasorKullanilamaz()
    {
        if (!OperatingSystem.IsWindows()) return;

        var hedef = GeciciKlasor("hedef-7");
        var kullanici = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
        var bilgi = new DirectoryInfo(hedef);
        var izinler = bilgi.GetAccessControl();
        var red = new System.Security.AccessControl.FileSystemAccessRule(
            kullanici,
            System.Security.AccessControl.FileSystemRights.CreateFiles,
            System.Security.AccessControl.AccessControlType.Deny);

        izinler.AddAccessRule(red);
        bilgi.SetAccessControl(izinler);
        try
        {
            Assert.True(Directory.Exists(hedef));
            Assert.Null(MainWindow.UsableFixedFolder(1, hedef));
        }
        finally
        {
            izinler.RemoveAccessRule(red);
            bilgi.SetAccessControl(izinler);
        }

        Assert.Equal(hedef, MainWindow.UsableFixedFolder(1, hedef));
    }

    /// <summary>
    /// Bağlantının pimi: çıktı yolu sabit klasörü soran koldan kuruluyor ve kullanılamayan
    /// klasör sessizce geçilmiyor, uyarı anahtarı yazılıyor. Davranış ölçüsü statik kolu
    /// tutuyor; bu ölçü onu pencereye bağlayan iki satırı tutuyor.
    /// </summary>
    [Fact]
    public void PencereSabitKlasoruSoruyorVeUyariyor()
    {
        var kaynak = File.ReadAllText(TipSources.WindowCodePath);

        Assert.Contains("UsableFixedFolder(OutputFolderModeIndex, TxtOutputFolder.Text)", kaynak, System.StringComparison.Ordinal);
        Assert.Contains("ShrinkEngine.UniqueOutputPath(inputPath, suffix, extension,", kaynak, System.StringComparison.Ordinal);
        Assert.Contains("settings-tab.output-folder.unusable", kaynak, System.StringComparison.Ordinal);
        Assert.Contains("if (FixedFolderUnusable)", kaynak, System.StringComparison.Ordinal);
    }

    /// <summary>Uyarı metni kırk iki dilin hepsinde var ve yer tutucusunu koruyor.</summary>
    [Fact]
    public void UyariMetniButunDillerde()
    {
        const string anahtar = "settings-tab.output-folder.unusable";
        foreach (var dil in Locales.Languages)
        {
            var degerler = Locales.Values(dil);
            Assert.True(degerler.ContainsKey(anahtar), $"{dil} dilinde {anahtar} yok.");
            var metin = degerler[anahtar];
            Assert.False(string.IsNullOrWhiteSpace(metin), $"{dil} dilinde {anahtar} boş.");
            Assert.Contains("{0}", metin, System.StringComparison.Ordinal);
        }
    }
}
