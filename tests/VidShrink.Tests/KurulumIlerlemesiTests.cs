using System;
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
    /// <summary>Çubuk yüzdeye fark × 0,08 ile yaklaşıyor, en az 0,2 adımla.</summary>
    [Fact]
    public void CubukYuzdeyeYaklasiyor()
    {
        var p = new InstallProgress();
        p.Step(50, 60, "İndiriliyor");

        Assert.Equal(50 * InstallProgress.Approach, p.Advance(), 6);

        var ikinci = p.Advance();
        Assert.True(ikinci > 4 && ikinci < 50);
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

    /// <summary>İş bitene kadar durum çalışıyor; bitince yüzde ve çubuk sona çekiliyor.</summary>
    [Fact]
    public void SonucDuyuruluyor()
    {
        var p = new InstallProgress();
        p.Step(30, 80, "Yazılıyor");
        Assert.Equal(InstallState.Running, p.State);

        p.Finish(true, "Güncelleme uygulandı");
        Assert.Equal(InstallState.Done, p.State);
        Assert.Equal(100, p.Percent);
        Assert.Equal(100, p.Bar);
        Assert.Equal("100%", p.PercentText);

        var q = new InstallProgress();
        q.Step(30, 80, "Yazılıyor");
        q.Finish(false, "Güncelleme uygulanamadı");

        Assert.Equal(InstallState.Failed, q.State);
        Assert.Equal(30, q.Percent);
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
    /// Elle yükleme artık kullanıcıya sessiz gelmiyor: güncelleyici panele bağlanıyor ve
    /// her aşama — sürüm listesi, bulunan sürüm, inen her dosya adı, yerine taşıma — bir
    /// cümleyle günlüğe düşüyor. Eskiden bütün indirme tek bir "Güncelleme uygulanıyor"
    /// satırıydı; kullanıcı ne olduğunu göremiyordu.
    /// </summary>
    [Fact]
    public void GuncelleyiciPaneleKonusuyor()
    {
        var kaynak = File.ReadAllText(
            Path.Combine(TipSources.Root, "src", "VidShrink.Launcher", "Updater.cs"));
        var program = File.ReadAllText(
            Path.Combine(TipSources.Root, "src", "VidShrink.Launcher", "Program.cs"));

        Assert.Contains("InstallProgress? progress = null", kaynak);
        Assert.Contains("progress?.Step(", kaynak);
        Assert.Contains("\"Sürüm listesi alınıyor\"", kaynak);
        Assert.Contains("indiriliyor (\" + done + \"/\" + total + \")\"", kaynak);
        Assert.Contains("\"Dosyalar yerine taşınıyor\"", kaynak);
        Assert.Contains("progress: progress", program);
        Assert.Contains("progress.WriteLog(", program);
    }

    /// <summary>
    /// Prova kipi: panel birebir aynı koşuyor, dosyalar iniyor, ama hiçbiri yerine
    /// taşınmıyor ve başlatıcı geçişi kurulmuyor. Panel provasız yayımlanmaz.
    /// </summary>
    [Fact]
    public void ProvaKipiKurmuyor()
    {
        var kaynak = File.ReadAllText(
            Path.Combine(TipSources.Root, "src", "VidShrink.Launcher", "Updater.cs"));

        Assert.Contains("VIDSHRINK_UPDATE_PROVA", kaynak);
        var prova = kaynak.IndexOf("if (Rehearsing)", StringComparison.Ordinal);
        var kurulum = kaynak.IndexOf("LauncherUpdate.Stage(stage", StringComparison.Ordinal);

        Assert.True(prova > 0, "prova kolu yok");
        Assert.True(prova < kurulum, "prova kolu kurulumdan önce dönmeli");
    }
}
