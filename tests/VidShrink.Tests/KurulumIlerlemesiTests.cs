using System;
using System.Threading;
using System.IO;
using System.Linq;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kurulum panelinin tavan kuralı. Ölçülen şey çizim değil, çubuğun kararı: yüzdeye
/// yaklaşma, yüzde durunca tavana sürünme, tavanı ve yüzdeyi geçmeme, yüzdenin geri
/// gitmemesi ve günlüğün son dokuz satırda durması.
///
/// <para>Katsayılar <c>private/tercihler/guncelleme-paneli.md</c>'den geliyor; burada
/// uydurulmuyor, pimleniyor.</para>
/// </summary>
public class KurulumIlerlemesiTests
{
    /// <summary>
    /// Çubuk yüzdeye yaklaşıyor: açılış atağında fark × 0,2, atak bitince fark × 0,08,
    /// en az 0,2 adımla.
    /// </summary>
    [Fact]
    public void CubukYuzdeyeYaklasiyor()
    {
        var p = new InstallProgress();
        p.Step(50, 60, "İndiriliyor");

        Assert.Equal(50 * InstallProgress.BurstApproach, p.Advance(), 6);

        var ikinci = p.Advance();
        Assert.True(ikinci > 4 && ikinci < 50);
    }

    /// <summary>
    /// Açılış atağı: ilk <see cref="InstallProgress.BurstFrames"/> kare hızlı, sonra olağan
    /// yasa. Atak da tavanı ve yüzdeyi geçmiyor, çubuk hiçbir karede geri gitmiyor. Sınır
    /// kare içinde düşünce kare bölünüyor: yarım karelerle varılan yer aynı.
    /// </summary>
    [Fact]
    public void AcilisAtagiKisaVeTavanliKosuyor()
    {
        var p = new InstallProgress();
        p.Step(0, 30, "Sürüm listesi alınıyor");

        var onceki = 0.0;
        for (var i = 0; i < InstallProgress.BurstFrames; i++)
        {
            var bar = p.Advance();
            Assert.True(bar >= onceki, "atak çubuğu geri götürdü");
            Assert.True(bar <= 30, $"atak tavanı geçti: {bar}");
            onceki = bar;
        }
        Assert.Equal(30 * (1 - Math.Pow(1 - InstallProgress.BurstCreep, InstallProgress.BurstFrames)), onceki, 6);

        var q = new InstallProgress();
        for (var i = 0; i < InstallProgress.BurstFrames; i++) q.Advance();
        q.Step(100, 100, "x");
        Assert.Equal(100 * InstallProgress.Approach, q.Advance(), 6);

        var tek = new InstallProgress();
        var yarim = new InstallProgress();
        tek.Step(100, 100, "x");
        yarim.Step(100, 100, "x");
        var kare = TimeSpan.FromMilliseconds(InstallProgress.FrameMilliseconds);
        for (var i = 0; i < 3; i++) tek.Advance(kare * 10);
        for (var i = 0; i < 60; i++) yarim.Advance(kare / 2);
        Assert.Equal(tek.Bar, yarim.Bar, 6);

        var yavas = new InstallProgress();
        yavas.Step(100, 100, "x");
        for (var i = 0; i < InstallProgress.BurstFrames; i++) yavas.Advance();
        var olagan = 100 * (1 - Math.Pow(1 - InstallProgress.Approach, InstallProgress.BurstFrames));
        Assert.True(yavas.Bar > olagan, "atak olağan yasadan hızlı koşmalı");
    }

    /// <summary>Fark küçülünce adım tabana oturuyor: çubuk hiçbir karede donmuyor.</summary>
    [Fact]
    public void EnKucukAdimTabani()
    {
        var p = new InstallProgress();
        p.Step(1, 90, "Hazırlanıyor");

        Assert.Equal(InstallProgress.MinimumStep, p.Advance(), 6);
        Assert.Equal(2 * InstallProgress.MinimumStep, p.Advance(), 6);

        for (var i = 0; i < 3; i++) p.Advance();
        Assert.Equal(1, p.Bar, 6);
    }

    /// <summary>
    /// Yüzde durduğunda çubuk tavana fark × 0,006 ile sürünüyor ve tavanı geçmiyor:
    /// uzayan adım sonraki adımın alanını yemiyor.
    /// </summary>
    [Fact]
    public void YuzdeDuruncaTavanaSuruyor()
    {
        var p = new InstallProgress();
        p.Step(0, 40, "Manifest çekiliyor");
        p.Step(20, 40, "Dosyalar yazılıyor");

        for (var i = 0; i < 500; i++) p.Advance();
        Assert.True(p.Bar > 20, $"çubuk yüzdede kaldı: {p.Bar}");
        Assert.True(p.Bar < 40, $"çubuk tavanı geçti: {p.Bar}");

        for (var i = 0; i < 20000; i++) p.Advance();
        Assert.True(p.Bar <= 40);
    }

    /// <summary>Yüzde asla geri gitmiyor; geriye yazan adım görülen en büyükte tutuluyor.</summary>
    [Fact]
    public void YuzdeGeriGitmiyor()
    {
        var p = new InstallProgress();
        p.Step(60, 70, "Kopyalanıyor");
        p.Step(30, 70, "Geriye yazan adım");

        Assert.Equal(60, p.Percent);
        Assert.Equal(70, p.Ceiling);
    }

    /// <summary>Tavan yüzdenin altına inemiyor; 0–100 dışına çıkan sayı kırpılıyor.</summary>
    [Fact]
    public void TavanVeSinirlarKirpiliyor()
    {
        var p = new InstallProgress();
        p.Step(80, 10, "Tavan yüzdenin altında");
        Assert.Equal(80, p.Ceiling);

        var q = new InstallProgress();
        q.Step(500, 900, "Taşan sayı");
        Assert.Equal(100, q.Percent);

        var r = new InstallProgress();
        r.Step(-20, 50, "Negatif");
        Assert.Equal(0, r.Percent);
        Assert.Equal(50, r.Ceiling);
    }

    /// <summary>Ekranda son dokuz satır duruyor, günlüğün tamamı diske gidiyor.</summary>
    [Fact]
    public void GunlukSonDokuzSatir()
    {
        var p = new InstallProgress();
        for (var i = 1; i <= 14; i++) p.Step(i, 100, "Adım " + i);

        Assert.Equal(InstallProgress.LogLines, p.Log.Count);
        Assert.Equal("Adım 6", p.Log.First());
        Assert.Equal("Adım 14", p.Log.Last());
        Assert.Equal(14, p.History.Count);
        Assert.Equal("Adım 14", p.Sentence);
    }

    /// <summary>
    /// Is bitene kadar durum calisiyor; bitince yuzde sona tasiniyor. 15 Eylul 2026'da
    /// cubugun sicramasi kaldirildi: Finish artik _bar'a dokunmuyor, cubuk ayni
    /// yaklasma yasasiyla sona kosuyor ve panel ancak dolduktan sonra kapaniyor.
    /// </summary>
    [Fact]
    public void SonucDuyuruluyor()
    {
        var p = new InstallProgress();
        p.Step(30, 80, "Yazilir");
        Assert.Equal(InstallState.Running, p.State);

        p.Finish(true, "Guncelleme uygulandi");
        Assert.Equal(InstallState.Done, p.State);
        Assert.Equal(100, p.Percent);

        Assert.True(p.Bar < 100, "bitis cubugu sicratmiyor");
        for (var i = 0; i < 400; i++) p.Advance();
        Assert.Equal(100, p.Bar);

        var q = new InstallProgress();
        q.Step(30, 80, "Yazilir");
        q.Finish(false, "Guncelleme uygulanamadi");

        Assert.Equal(InstallState.Failed, q.State);
        Assert.Equal(30, q.Percent);
        for (var i = 0; i < 400; i++) q.Advance();
        Assert.Equal(30, q.Bar);
    }

    /// <summary>
    /// Günlüğün tamamı diske de yazılıyor: ekranda dokuz satır duruyor, kullanıcı iş
    /// bittikten sonra ne olduğuna dosyadan bakabiliyor.
    /// </summary>
    [Fact]
    public void GunlukDiskeYaziliyor()
    {
        var p = new InstallProgress();
        for (var i = 1; i <= 14; i++) p.Step(i, 100, "Adım " + i);
        p.Finish(true, "Bitti");

        var yol = Path.Combine(Path.GetTempPath(), "vidshrink-gunluk-" + Guid.NewGuid().ToString("N"), "update-log.txt");
        try
        {
            Assert.True(p.WriteLog(yol));
            var satirlar = File.ReadAllLines(yol);

            Assert.Equal(15, satirlar.Length);
            Assert.Equal("Adım 1", satirlar[0]);
            Assert.Equal("Bitti", satirlar[^1]);
        }
        finally
        {
            var klasor = Path.GetDirectoryName(yol);
            if (klasor is not null && Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    /// <summary>
    /// Yol D: başlatıcının paneli kalktı, güncelleyici panele konuşmuyor. Yerine taşıma
    /// uygulama klasörü boşalınca başlıyor ve hatası uygulamanın paneline işaret bırakıyor.
    /// Yarışın davranışı <c>BaslaticiPanelsizTests</c>'te gerçek süreçle. Taşıma adımı
    /// <c>KurulumBekleyeni.cs</c>'e geçti; güncelleyici oraya devrediyor.
    /// </summary>
    [Fact]
    public void GuncelleyiciKlasorBosalincaKurar()
    {
        var launcher = Path.Combine(TipSources.Root, "src", "VidShrink.Launcher");
        var guncelleyici = File.ReadAllText(Path.Combine(launcher, "Updater.cs"));
        var kaynak = File.ReadAllText(Path.Combine(launcher, "KurulumBekleyeni.cs"));

        Assert.DoesNotContain("InstallProgress", guncelleyici);
        Assert.Contains("KurulumBekleyeni.Calistir(", guncelleyici);
        Assert.DoesNotContain("InstallProgress", kaynak);
        var bosalinca = kaynak.IndexOf("UygulamaKlasoruKapisi.BosalincaAl(", StringComparison.Ordinal);
        var tasima = kaynak.IndexOf("UpdateRollout.Apply(", StringComparison.Ordinal);
        Assert.True(bosalinca > 0 && bosalinca < tasima, "taşıma klasör boşalmadan başlıyor");
        Assert.Contains("UygulamaKlasoruKapisi.HataYaz(", kaynak);
    }

    /// <summary>
    /// Prova kipi: panel birebir aynı koşuyor, dosyalar iniyor, ama hiçbiri yerine
    /// taşınmıyor ve başlatıcı geçişi kurulmuyor. Panel provasız yayımlanmaz.
    /// </summary>
    [Fact]
    public void ProvaKipiKurmuyor()
    {
        var launcher = Path.Combine(TipSources.Root, "src", "VidShrink.Launcher");
        var guncelleyici = File.ReadAllText(Path.Combine(launcher, "Updater.cs"));
        var kaynak = File.ReadAllText(Path.Combine(launcher, "KurulumBekleyeni.cs"));

        Assert.Contains("VIDSHRINK_UPDATE_PROVA", guncelleyici);
        Assert.Contains("}, Rehearsing);", guncelleyici);
        var prova = kaynak.IndexOf("if (prova) return false;", StringComparison.Ordinal);
        var kurulum = kaynak.IndexOf("LauncherUpdate.Stage(stage", StringComparison.Ordinal);

        Assert.True(prova > 0, "prova kolu yok");
        Assert.True(prova < kurulum, "prova kolu kurulumdan önce dönmeli");
    }

    /// <summary>
    /// Bir karelik sure gectiginde zamana bagli adim, kareye bagli adimla birebir ayni:
    /// katsayilar degismedi, yalnizca neyle carpildigi degisti.
    /// </summary>
    [Fact]
    public void BirKarelikSureEskiAdimlaAyni()
    {
        var a = new InstallProgress();
        var b = new InstallProgress();
        a.Step(50, 60, "x");
        b.Step(50, 60, "x");

        for (var i = 0; i < 40; i++)
        {
            a.Advance();
            b.Advance(TimeSpan.FromMilliseconds(InstallProgress.FrameMilliseconds));
        }

        Assert.Equal(a.Bar, b.Bar, 9);
    }

    /// <summary>
    /// Kare gec dustugunde cubuk ayni yere variyor: iki yarim kare bir tam kare ediyor.
    /// Cubugun hizi panelin cizim yukune degil gecen sureye bagli olan sey budur.
    /// </summary>
    [Fact]
    public void GecikenKareyiSureTelafiEdiyor()
    {
        var tek = new InstallProgress();
        var yarim = new InstallProgress();
        tek.Step(50, 60, "x");
        yarim.Step(50, 60, "x");

        var kare = TimeSpan.FromMilliseconds(InstallProgress.FrameMilliseconds);
        for (var i = 0; i < 20; i++)
        {
            tek.Advance(kare);
            yarim.Advance(kare / 2);
            yarim.Advance(kare / 2);
        }

        Assert.Equal(tek.Bar, yarim.Bar, 6);
    }

    /// <summary>
    /// Donmus bir kareden sonra cubuk sicramiyor. Yakalama yasasinin kendi zaman sabiti
    /// tavan: on saniyelik bir duraklama tek karede hedefe atlamiyor.
    /// </summary>
    [Fact]
    public void UzunDurakCubugaSicratmiyor()
    {
        var p = new InstallProgress();
        p.Step(100, 100, "x");

        p.Advance(TimeSpan.FromSeconds(10));

        Assert.True(p.Bar < 100, "tek karede hedefe varmamali");
        Assert.True(p.Bar > InstallProgress.MinimumStep, "yine de gorunur ilerlemeli");
    }
}
