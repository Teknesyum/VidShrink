using System;
using System.IO;
using System.Linq;
using VidShrink.App;
using Xunit;
using CoreShare = VidShrink.Core.Share;

namespace VidShrink.Tests;

/// <summary>
/// <c>paylasim-hedefleri.json</c>'ı bir tek tür okuyor: <see cref="CoreShare.ShareTargetTable"/>.
/// Eskiden iki tanesi vardı — şeridi kuran App kopyası ve yüklemeyi yapan Core'unki — ve şema
/// T35'te sabitlenmiş olsa da <b>arama sırası</b> sabitlenmemişti.
///
/// <para>Core önce kullanıcının kendi kopyasına bakıyor (<c>%APPDATA%\VidShrink</c>); belgesi
/// bunun sebebini de yazıyor: bir uç nokta ölünce kullanıcı sürüm beklemeden düzeltebilsin.
/// App tarafı oraya hiç bakmıyordu, yalnız uygulamanın yanından yukarı tarıyordu. Kullanıcı
/// kendi kopyasını düzenlediğinde şerit paketteki tavanları gösteriyor, yükleme kullanıcının
/// uç noktasına gidiyordu: görünen sınır ile gidilen adres ayrı dosyalardan. Hata yok, uyarı
/// yok.</para>
///
/// <para>İkizin geri gelmemesi ayrıca pimli: App derlemesinde ikinci bir hedef tablosu
/// türü kalmadı. Tür geri eklense derleme kırılmaz, sessizce iki parser olur.</para>
///
/// <para>Ölçü pencere açmıyor; arama <see cref="CoreShare.ShareTargetTable.LoadOrFallback"/>
/// ile dışarıdan veriliyor, gerçek <c>%APPDATA%</c>'ya dokunulmuyor.</para>
/// </summary>
public sealed class PaylasimAramaSirasiTests
{
    /// <summary>
    /// Kusurun kendisi: şerit ile yükleme tek tablodan okuyor. App derlemesinde kendi
    /// <c>ShareTarget</c>/<c>ShareTargetTable</c> türü kalmadı — ikisi ayrıştıkça görünen
    /// tavan ile gidilen adres ayrı dosyalardan geliyordu.
    /// </summary>
    [Fact]
    public void AppKendiHedefTablosunuTutmuyor()
    {
        var ikiz = typeof(MainWindow).Assembly.GetTypes()
            .Where(tur => tur.Name is "ShareTarget" or "ShareTargetTable")
            .Select(tur => tur.FullName!)
            .ToArray();

        Assert.Equal(Array.Empty<string>(), ikiz);
        Assert.NotNull(typeof(CoreShare.ShareTargetTable).GetMethod("LoadOrFallback"));
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

        Assert.Equal(kullanici, CoreShare.ShareTargetTable.AramaSirasi().First());
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
            var yol = Path.Combine(klasor, CoreShare.ShareTargetTable.FileName);
            File.WriteAllText(yol, """
            { "version": 1, "default": "ozel.example", "targets": [
              { "id": "ozel.example", "displayName": "Kullanicinin Kopyasi", "maxBytes": 7,
                "retentionDays": [], "canDelete": false, "playsInBrowser": false,
                "endpoints": { "upload": "https://ozel.example/upload" } } ] }
            """);

            var tablo = CoreShare.ShareTargetTable.LoadOrFallback(() => yol);

            Assert.Equal(new[] { "ozel.example" }, tablo.Targets.Select(hedef => hedef.Id));
            Assert.Equal(7, tablo.Targets[0].MaxBytes);
            Assert.NotSame(CoreShare.ShareTargetTable.Fallback, tablo);
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
        var tablo = CoreShare.ShareTargetTable.LoadOrFallback(() => null);

        Assert.Same(CoreShare.ShareTargetTable.Fallback, tablo);
        Assert.NotEmpty(tablo.Targets);
    }

    /// <summary>
    /// Olumsuz kontrol: okunamayan dosya da varsayılana düşüyor, pencere açılmaya devam ediyor.
    /// </summary>
    [Fact]
    public void OkunamayanDosyaVarsayilanaDusuyor()
    {
        var yok = Path.Combine(Calisma(), Guid.NewGuid().ToString("N"), "olmayan.json");

        Assert.Same(CoreShare.ShareTargetTable.Fallback, CoreShare.ShareTargetTable.LoadOrFallback(() => yok));
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
