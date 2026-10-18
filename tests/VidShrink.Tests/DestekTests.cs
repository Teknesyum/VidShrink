using System.IO.Compression;
using System.Text.RegularExpressions;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// AYR-06, 07, 11, 27, 29: Hakkında ve tanılama düğmelerinin açtığı adresler, hata
/// bildiriminin taşıdığı metin, issue şablonu ve tanılama paketi. Ağa çıkmaz.
/// </summary>
public sealed class DestekTests
{
    private static readonly string Klasor = Path.Combine(TipSources.Root, ".calisma", "test-ciktilari", "destek");
    private static readonly string Sablon = Path.Combine(TipSources.Root, ".github", "ISSUE_TEMPLATE", Destek.HataSablonu);

    [Fact]
    public void SurumNotlariEtiketeGider()
    {
        Assert.Equal("https://github.com/Teknesyum/VidShrink-Player/releases/tag/v1.2.17", Destek.SurumNotlari("1.2.17+abc123"));
        Assert.Equal("https://github.com/Teknesyum/VidShrink-Player/releases/tag/v1.3.0-beta", Destek.SurumNotlari("v1.3.0-beta"));
    }

    [Fact]
    public void LisansVeGizlilikAdresleri()
    {
        Assert.Equal("https://github.com/Teknesyum/VidShrink-Player/blob/main/THIRD-PARTY-NOTICES.md", Destek.LisansAdresi);
        Assert.Equal("https://github.com/Teknesyum/VidShrink-Player#privacy", Destek.GizlilikAdresi);
        Assert.Contains("## Privacy", File.ReadAllText(Path.Combine(TipSources.Root, "README.md")));
        Assert.True(File.Exists(Path.Combine(TipSources.Root, "THIRD-PARTY-NOTICES.md")));
    }

    [Fact]
    public void HataBildirimiSurumIsletimVeGunlukYolunuTasir()
    {
        var adres = Destek.HataBildir("1.2.17+abc", "Microsoft Windows 10.0.22631 (X64)", @"~\AppData\Roaming\VidShrink\gunluk\vidshrink.log");

        Assert.Equal(
            "https://github.com/Teknesyum/VidShrink-Player/issues/new?template=bug_report.yml" +
            "&version=1.2.17" +
            "&os=Microsoft%20Windows%2010.0.22631%20%28X64%29" +
            "&log-path=~%5CAppData%5CRoaming%5CVidShrink%5Cgunluk%5Cvidshrink.log",
            adres);
        Assert.DoesNotContain("mailto", adres, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(@"C:\Users\ayse\AppData\Roaming\VidShrink\gunluk\vidshrink.log", @"C:\Users\ayse", @"~\AppData\Roaming\VidShrink\gunluk\vidshrink.log")]
    [InlineData("/home/ayse/.config/VidShrink/gunluk/vidshrink.log", "/home/ayse/", "~/.config/VidShrink/gunluk/vidshrink.log")]
    [InlineData("/home/ayseler/x.log", "/home/ayse", "/home/ayseler/x.log")]
    [InlineData(@"D:\tasinabilir\gunluk\vidshrink.log", @"C:\Users\ayse", @"D:\tasinabilir\gunluk\vidshrink.log")]
    public void GizliYolKullaniciKlasorunuIndirger(string yol, string profil, string beklenen) =>
        Assert.Equal(beklenen, Destek.GizliYol(yol, profil));

    /// <summary>Gerçek yolda kullanıcı adı adrese girmez.</summary>
    [Fact]
    public void GercekGunlukYoluKullaniciAdiTasimaz()
    {
        var yol = Destek.GunlukYolu();
        var profil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (Gunluk.Dosya.StartsWith(profil, StringComparison.OrdinalIgnoreCase))
            Assert.StartsWith("~", yol);
        Assert.DoesNotContain(profil, Destek.HataBildir("1.0.0", "os", yol), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SistemBilgisiSagligiYolsuzYazar()
    {
        var ayar = Path.Combine(Klasor, Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Klasor);
        var saglik = UpdateHealth.RecordFailure(ayar, DateTimeOffset.UnixEpoch, @"IOException: C:\Users\ayse\x\a.zip kilitli");

        var metin = Destek.SistemBilgisi("1.2.17+abc", "TestOS", "~/log", saglik);

        Assert.Contains("vidshrink: 1.2.17\n", metin.Replace("\r", ""));
        Assert.Contains("os: TestOS", metin);
        Assert.Contains("log: ~/log", metin);
        Assert.Contains("update-failures: 1", metin);
        Assert.Contains("update-last-error: IOException: a.zip kilitli", metin);
        Assert.DoesNotContain("ayse", metin);
    }

    [Fact]
    public void PaketSistemGunlukVeYedegiTasir()
    {
        var kok = Path.Combine(Klasor, Guid.NewGuid().ToString("N"));
        var gunluk = Path.Combine(kok, "gunluk");
        Directory.CreateDirectory(gunluk);
        File.WriteAllText(Path.Combine(gunluk, Gunluk.DosyaAdi), "satir-1");
        File.WriteAllText(Path.Combine(gunluk, Gunluk.YedekAdi), "eski");
        var hedef = Path.Combine(kok, Destek.PaketAdi(new DateTimeOffset(2026, 10, 15, 9, 5, 0, TimeSpan.Zero)));

        using (var acik = new FileStream(Path.Combine(gunluk, Gunluk.DosyaAdi), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            Destek.PaketYaz(hedef, gunluk, "sistem-ozeti");

        Assert.EndsWith("vidshrink-tani-20261015-0905.zip", hedef);
        using var zip = ZipFile.OpenRead(hedef);
        var icerik = zip.Entries.ToDictionary(e => e.FullName, e => new StreamReader(e.Open()).ReadToEnd());
        Assert.Equal(new[] { Destek.SistemDosyasi, Gunluk.DosyaAdi, Gunluk.YedekAdi }, icerik.Keys.ToArray());
        Assert.Equal("sistem-ozeti", icerik[Destek.SistemDosyasi]);
        Assert.Equal("satir-1", icerik[Gunluk.DosyaAdi]);
        Assert.Equal("eski", icerik[Gunluk.YedekAdi]);
    }

    [Fact]
    public void GunlukYoksaPaketYalnizSistemiTasir()
    {
        var kok = Path.Combine(Klasor, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(kok);
        var hedef = Path.Combine(kok, "p.zip");

        Destek.PaketYaz(hedef, Path.Combine(kok, "yok"), "s");

        using var zip = ZipFile.OpenRead(hedef);
        Assert.Equal(new[] { Destek.SistemDosyasi }, zip.Entries.Select(e => e.FullName).ToArray());
    }

    /// <summary>
    /// Şablon, adresin doldurduğu alanları aynı kimlikle taşır; e-posta ya da telefon yolu yok.
    /// </summary>
    [Fact]
    public void IssueSablonuAdresinAlanlariniTasir()
    {
        Assert.True(File.Exists(Sablon), Sablon);
        var metin = File.ReadAllText(Sablon);
        var kimlikler = Regex.Matches(metin, @"^\s*id:\s*(\S+)\s*$", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToArray();
        var adres = Destek.HataBildir("1", "o", "l");
        var alanlar = Regex.Matches(adres, @"[?&]([a-z-]+)=").Select(m => m.Groups[1].Value).Where(a => a != "template").ToArray();

        Assert.Equal(new[] { "version", "os", "log-path" }, alanlar);
        Assert.All(alanlar, a => Assert.Contains(a, kimlikler));
        Assert.DoesNotMatch(@"(?i)mailto|e-?mail|phone|telefon|@[a-z0-9-]+\.[a-z]{2,}", metin);
        Assert.DoesNotMatch(@"\+?\d[\d\s().-]{8,}\d", metin);
    }
}
