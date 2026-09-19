using System;
using System.IO;
using System.Linq;
using VidShrink.App;
using Xunit;
using CoreShare = VidShrink.Core.Share;

namespace VidShrink.Tests;

/// <summary>
/// <c>paylasim-hedefleri.json</c>'ı iki tür okuyor: şeridi kuran <see cref="ShareTargetTable"/>
/// ve yüklemeyi yapan <c>Core.Share.ShareTargetTable</c>. Şema T35'te sabitlendi ve iki taraf
/// da onu okuyor — ama <b>arama sırası</b> sabitlenmemişti.
///
/// <para>Core önce kullanıcının kendi kopyasına bakıyor (<c>%APPDATA%\VidShrink</c>); belgesi
/// bunun sebebini de yazıyor: bir uç nokta ölünce kullanıcı sürüm beklemeden düzeltebilsin.
/// App tarafı oraya hiç bakmıyordu, yalnız uygulamanın yanından yukarı tarıyordu. Kullanıcı
/// kendi kopyasını düzenlediğinde şerit paketteki tavanları gösteriyor, yükleme kullanıcının
/// uç noktasına gidiyordu: görünen sınır ile gidilen adres ayrı dosyalardan. Hata yok, uyarı
/// yok.</para>
///
/// <para>Ölçü pencere açmıyor; arama <see cref="ShareTargetTable.Load(Func{string})"/> ile
/// dışarıdan veriliyor, gerçek <c>%APPDATA%</c>'ya dokunulmuyor.</para>
/// </summary>
public sealed class PaylasimAramaSirasiTests
{
    /// <summary>
    /// Kusurun kendisi: şeridin araması Core'unkiyle aynı olmalı. Ayrı bir arama tutmak
    /// iki dosyanın sessizce ayrışmasına izin verir.
    /// </summary>
    [Fact]
    public void SeritVeYuklemeAyniAramayiKullaniyor()
    {
        var beklenen = CoreShare.ShareTargetTable.AramaSirasi().ToArray();

        Assert.Equal(beklenen, ShareTargetTable.AramaSirasi().ToArray());
    }

    /// <summary>
    /// Kullanıcının kendi kopyası aramada birinci. Core'un belgesi bunu vaat ediyor;
    /// ölçü vaadi aramanın ilk adayından okuyor.
    /// </summary>
    [Fact]
    public void KullanicininKopyasiAramadaBirinci()
    {
        var kullanici = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VidShrink",
            CoreShare.ShareTargetTable.FileName);

        Assert.Equal(kullanici, ShareTargetTable.AramaSirasi().First());
    }

    /// <summary>
    /// Arama ne bulursa şerit onu okuyor: kendi ikinci aramasını yapmıyor. Aday olarak
    /// verilen dosyadaki uydurma hedef şeride geçmeli.
    /// </summary>
    [Fact]
    public void BulunanDosyaOlduguGibiOkunuyor()
    {
        var klasor = Path.Combine(Calisma(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var yol = Path.Combine(klasor, ShareTargetTable.FileName);
            File.WriteAllText(yol, """
            { "version": 1, "default": "ozel.example", "targets": [
              { "id": "ozel.example", "displayName": "Kullanicinin Kopyasi", "maxBytes": 7,
                "retentionDays": [], "canDelete": false, "playsInBrowser": false,
                "endpoints": { "upload": "https://ozel.example/upload" } } ] }
            """);

            var tablo = ShareTargetTable.Load(() => yol);

            Assert.Equal(new[] { "ozel.example" }, tablo.Targets.Select(hedef => hedef.Id));
            Assert.Equal(7, tablo.Targets[0].MaxBytes);
            Assert.NotSame(ShareTargetTable.Fallback, tablo);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    /// <summary>
    /// Olumsuz kontrol: arama boş dönerse ikinci bir arama denenmiyor, varsayılan kalıyor.
    /// Şerit kendi sırasını geri getirirse bu ölçü kırılmaz — onu <see cref="SeritVeYuklemeAyniAramayiKullaniyor"/>
    /// yakalar; buradaki iddia yalnız çökmemek ve uydurmamak.
    /// </summary>
    [Fact]
    public void AramaBosDonerseVarsayilanKaliyor()
    {
        var tablo = ShareTargetTable.Load(() => null);

        Assert.Same(ShareTargetTable.Fallback, tablo);
        Assert.NotEmpty(tablo.Targets);
    }

    /// <summary>
    /// Olumsuz kontrol: okunamayan dosya da varsayılana düşüyor, pencere açılmaya devam ediyor.
    /// </summary>
    [Fact]
    public void OkunamayanDosyaVarsayilanaDusuyor()
    {
        var yok = Path.Combine(Calisma(), Guid.NewGuid().ToString("N"), "olmayan.json");

        Assert.Same(ShareTargetTable.Fallback, ShareTargetTable.Load(() => yok));
    }

    private static string Calisma()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null && !File.Exists(Path.Combine(dizin.FullName, "VidShrink.sln")))
            dizin = dizin.Parent;

        var kok = dizin?.FullName ?? AppContext.BaseDirectory;
        var calisma = Path.Combine(kok, ".calisma", "paylasim-arama");
        Directory.CreateDirectory(calisma);
        return calisma;
    }
}
