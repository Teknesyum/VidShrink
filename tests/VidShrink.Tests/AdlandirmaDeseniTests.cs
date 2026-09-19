using System;
using System.IO;
using System.Linq;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// E3, çıktı adının deseni. Ad bugüne kadar <c>{ad}_shrunk</c> olarak gömülüydü: kullanıcı
/// klasörü seçebiliyor ama adı seçemiyordu.
///
/// <para>Kural iki yerde yazılıydı — <c>ShrinkEngine.UniqueOutputPath</c> ve
/// <c>ShrinkJobWindow.UniqueOutputPath</c>. İkinci kopya sabit çıktı klasörünü de görmüyordu,
/// yani kuyruktan geçen iş kullanıcının iki ayarını birden yok sayıyordu. Kopya silindi;
/// geri gelirse bu ölçü değil, kaynak piminin kendisi yakalar.</para>
/// </summary>
public sealed class AdlandirmaDeseniTests
{
    private static AdBilgisi Ornek() => new("klip")
    {
        HedefMb = 25,
        Crf = 26,
        VideoBitrateK = 2500,
        Yukseklik = 720,
        Kodek = "libx264",
        Tarih = new DateTime(2026, 9, 19)
    };

    [Theory]
    [InlineData("{ad}_shrunk", "klip_shrunk")]
    [InlineData("{ad}", "klip")]
    [InlineData("{ad}_{hedef}", "klip_25mb")]
    [InlineData("{ad}_{cozunurluk}", "klip_720p")]
    [InlineData("{ad}_{kodek}", "klip_h264")]
    [InlineData("{ad}_{tarih}", "klip_2026-09-19")]
    [InlineData("{ad}_{kalite}", "klip_crf26")]
    [InlineData("{ad}_{hedef}_{cozunurluk}_{kodek}", "klip_25mb_720p_h264")]
    public void YerTutucularDegereDonusuyor(string desen, string beklenen)
    {
        Assert.Equal(beklenen, AdlandirmaDeseni.Uygula(desen, Ornek()));
    }

    /// <summary>
    /// <c>{kalite}</c> kodlayıcının kalite kolunu gösterir, hedef boyutu değil: CRF yoksa
    /// bit hızına iner. Olumsuz kontrol yukarıdaki CRF'li satırda.
    /// </summary>
    [Fact]
    public void KaliteCrfYokkenBitHizinaIniyor()
    {
        var bilgi = new AdBilgisi("klip") { VideoBitrateK = 2500 };

        Assert.Equal("klip_2500k", AdlandirmaDeseni.Uygula("{ad}_{kalite}", bilgi));
    }

    /// <summary>Değeri olmayan yer tutucu boşa düşer, ardındaki ayırıcı yığılmaz.</summary>
    [Theory]
    [InlineData("{ad}_{hedef}", "klip")]
    [InlineData("{ad}_{hedef}_{cozunurluk}", "klip")]
    [InlineData("{hedef}_{ad}", "klip")]
    [InlineData("{ad}-{kodek}-son", "klip-son")]
    public void DegersizYerTutucuBosaDusuyor(string desen, string beklenen)
    {
        Assert.Equal(beklenen, AdlandirmaDeseni.Uygula(desen, new AdBilgisi("klip")));
    }

    /// <summary>Ondalık ayırıcı dosya adında nokta bırakmıyor; uzantıyla karışırdı.</summary>
    [Fact]
    public void OndalikHedefNoktaBirakmiyor()
    {
        var bilgi = new AdBilgisi("klip") { HedefMb = 7.5, Crf = 23.5 };

        Assert.Equal("klip_7_5mb_crf23_5", AdlandirmaDeseni.Uygula("{ad}_{hedef}_{kalite}", bilgi));
    }

    /// <summary>Kodlayıcı adı değil kodek adı yazılıyor: iki kodlayıcı aynı ada iniyor.</summary>
    [Theory]
    [InlineData("libx265", "klip_hevc")]
    [InlineData("hevc_nvenc", "klip_hevc")]
    [InlineData("libx264", "klip_h264")]
    [InlineData("h264_qsv", "klip_h264")]
    [InlineData("libsvtav1", "klip_av1")]
    [InlineData("av1_nvenc", "klip_av1")]
    [InlineData("libvpx-vp9", "klip_vp9")]
    public void KodlayiciAdiKodegeIniyor(string kodlayici, string beklenen)
    {
        var bilgi = new AdBilgisi("klip") { Kodek = kodlayici };

        Assert.Equal(beklenen, AdlandirmaDeseni.Uygula("{ad}_{kodek}", bilgi));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{hedef}")]
    public void BosaDusenDesenReddediliyor(string desen)
    {
        Assert.Equal("settings-tab.output-name.empty", AdlandirmaDeseni.Hata(desen));
    }

    [Theory]
    [InlineData("{ad}/alt")]
    [InlineData("{ad}\\alt")]
    [InlineData("{ad}:1")]
    [InlineData("{ad}?")]
    [InlineData("{ad}*")]
    [InlineData("{ad}|x")]
    [InlineData("{ad}\"x")]
    public void YolIsaretiReddediliyor(string desen)
    {
        Assert.Equal("settings-tab.output-name.bad-char", AdlandirmaDeseni.Hata(desen));
    }

    [Theory]
    [InlineData("{bilinmeyen}")]
    [InlineData("{ad")]
    [InlineData("ad}")]
    [InlineData("{{ad}}")]
    [InlineData("{AD}")]
    public void BozukYerTutucuReddediliyor(string desen)
    {
        Assert.Equal("settings-tab.output-name.bad-placeholder", AdlandirmaDeseni.Hata(desen));
    }

    /// <summary>Olumsuz kontrol: geçerli desen hata döndürmüyor.</summary>
    [Theory]
    [InlineData("{ad}_shrunk")]
    [InlineData("{ad} {hedef} {kodek}")]
    [InlineData("sabit-ad")]
    public void GecerliDesenHataVermiyor(string desen)
    {
        Assert.Null(AdlandirmaDeseni.Hata(desen));
        Assert.True(AdlandirmaDeseni.Gecerli(desen));
    }

    /// <summary>Bozuk desen işi durdurmuyor: ad kurulamadı diye kodlama iptal edilmez.</summary>
    [Fact]
    public void BozukDesenVarsayilanaDonuyor()
    {
        Assert.Equal("klip_shrunk", AdlandirmaDeseni.Uygula("{bilinmeyen}", Ornek()));
        Assert.Equal("klip_shrunk", AdlandirmaDeseni.Uygula(null, Ornek()));
    }

    /// <summary>Varsayılan desen bugünkü davranışı birebir veriyor.</summary>
    [Fact]
    public void VarsayilanBugunkuDavranis()
    {
        Assert.Equal("{ad}_shrunk", AdlandirmaDeseni.Varsayilan);
        Assert.Equal("klip_shrunk", AdlandirmaDeseni.Uygula(AdlandirmaDeseni.Varsayilan, Ornek()));
    }

    /// <summary>Küme kapalı; tanınan altı ad dışında yer tutucu yok.</summary>
    [Fact]
    public void KumeTamOlarakAltiAd()
    {
        Assert.Equal(
            new[] { "ad", "hedef", "kalite", "cozunurluk", "kodek", "tarih" },
            AdlandirmaDeseni.YerTutucular.ToArray());
    }

    /// <summary><c>{ad}</c> kaynağın adını verir, eski <c>_shrunk</c> ekini tekrarlamaz.</summary>
    [Theory]
    [InlineData("klip.mp4", "klip")]
    [InlineData("klip_shrunk.mp4", "klip")]
    [InlineData("klip_SHRUNK.mkv", "klip")]
    [InlineData("shrunk.mp4", "shrunk")]
    public void KaynakAdiEkiTekrarlamiyor(string dosya, string beklenen)
    {
        Assert.Equal(beklenen, ShrinkEngine.KaynakAdi(Path.Combine("C:", "x", dosya)));
    }

    private static string GeciciKlasor(string ad)
    {
        var yol = Path.Combine(TipSources.Root, ".calisma", "adlandirma-deseni", ad);
        if (Directory.Exists(yol)) Directory.Delete(yol, recursive: true);
        Directory.CreateDirectory(yol);
        return yol;
    }

    /// <summary>Desen motorun yoluna giriyor: verilen ad gövdenin tamamını değiştiriyor.</summary>
    [Fact]
    public void MotorVerilenAdiKullaniyor()
    {
        var kaynak = GeciciKlasor("kaynak-1");
        var girdi = Path.Combine(kaynak, "klip.mp4");

        var cikti = ShrinkEngine.UniqueOutputPath(girdi, "shrunk", "mp4", null, "klip_25mb_720p");

        Assert.Equal("klip_25mb_720p.mp4", Path.GetFileName(cikti));
    }

    /// <summary>Olumsuz kontrol: ad verilmezse eski gövde kuruluyor.</summary>
    [Fact]
    public void AdVerilmezseEskiGovde()
    {
        var kaynak = GeciciKlasor("kaynak-2");
        var girdi = Path.Combine(kaynak, "klip.mp4");

        Assert.Equal("klip_shrunk.mp4", Path.GetFileName(
            ShrinkEngine.UniqueOutputPath(girdi, "shrunk", "mp4", null, null)));
    }

    /// <summary>
    /// Çakışma sayacı <b>desenden sonra</b> işliyor: sayı desenin ürettiği adın sonuna
    /// ekleniyor, desenin içine karışmıyor.
    /// </summary>
    [Fact]
    public void CakismaSayaciDesendenSonra()
    {
        var kaynak = GeciciKlasor("kaynak-3");
        var hedef = GeciciKlasor("hedef-3");
        var girdi = Path.Combine(kaynak, "klip.mp4");

        File.WriteAllBytes(Path.Combine(hedef, "klip_25mb.mp4"), []);
        var cikti = ShrinkEngine.UniqueOutputPath(girdi, "shrunk", "mp4", hedef, "klip_25mb");

        Assert.Equal("klip_25mb_2.mp4", Path.GetFileName(cikti));
        Assert.Equal(hedef, Path.GetDirectoryName(cikti));
    }

    /// <summary>
    /// Kuyruk penceresinin kendi kopyası silindi. Kopya sabit klasörü de görmüyordu; iki
    /// ayar birden yok sayılıyordu. Kaynak pimi: ikinci gövde geri gelirse kırmızı olur.
    /// </summary>
    [Fact]
    public void KuyrukKendiKopyasiniTasimiyor()
    {
        var kaynak = File.ReadAllText(Path.Combine(
            TipSources.Root, "src", "VidShrink.App", "ShrinkJobWindow.axaml.cs"));

        Assert.DoesNotContain("Path.GetFileNameWithoutExtension(inputPath)", kaynak, StringComparison.Ordinal);
        Assert.Contains("ShrinkEngine.UniqueOutputPath(", kaynak, StringComparison.Ordinal);
        Assert.Contains("MainWindow.UsableFixedFolder(settings.OutputFolderMode, settings.OutputFolder)", kaynak, StringComparison.Ordinal);
        Assert.Contains("AdlandirmaDeseni.Uygula(settings.OutputNamePattern", kaynak, StringComparison.Ordinal);
    }

    /// <summary>Ana pencerede desen çıktı yolunu kuran kolun kaynak pimi.</summary>
    [Fact]
    public void PencereDeseniCiktiYolunaVeriyor()
    {
        var kaynak = File.ReadAllText(TipSources.WindowCodePath);

        Assert.Contains("AdlandirmaDeseni.Uygula(TxtOutputName.Text", kaynak, StringComparison.Ordinal);
        Assert.Contains("RefreshOutputNamePreview()", kaynak, StringComparison.Ordinal);
        Assert.Contains("OutputNamePattern = TxtOutputName.Text", kaynak, StringComparison.Ordinal);
    }

    /// <summary>Yedi anahtar kırk iki dilde var; önizleme yer tutucusunu koruyor.</summary>
    [Fact]
    public void AnahtarlarButunDillerde()
    {
        string[] anahtarlar =
        [
            "settings-tab.output-name.label",
            "settings-tab.output-name.reset",
            "settings-tab.output-name.hint",
            "settings-tab.output-name.preview",
            "settings-tab.output-name.empty",
            "settings-tab.output-name.bad-char",
            "settings-tab.output-name.bad-placeholder"
        ];

        foreach (var dil in Locales.Languages)
        {
            var degerler = Locales.Values(dil);
            foreach (var anahtar in anahtarlar)
            {
                Assert.True(degerler.ContainsKey(anahtar), $"{dil} dilinde {anahtar} yok.");
                Assert.False(string.IsNullOrWhiteSpace(degerler[anahtar]), $"{dil} dilinde {anahtar} boş.");
            }

            Assert.Contains("{0}", degerler["settings-tab.output-name.preview"], StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Yer tutucu adları kod belirteci: çeviride bozulurlarsa ipucu kullanıcıya olmayan bir
    /// ad öğretir. Altı ad her dilde aynen geçiyor.
    /// </summary>
    [Fact]
    public void YerTutucuAdlariCeviriyleBozulmuyor()
    {
        foreach (var dil in Locales.Languages)
        {
            var degerler = Locales.Values(dil);
            foreach (var anahtar in new[] { "settings-tab.output-name.hint", "settings-tab.output-name.bad-placeholder" })
            {
                var metin = degerler[anahtar];
                foreach (var ad in AdlandirmaDeseni.YerTutucular)
                    Assert.True(metin.Contains("{" + ad + "}", StringComparison.Ordinal),
                        $"{dil} dilinde {anahtar} içinde {{{ad}}} yok.");
            }
        }
    }
}
