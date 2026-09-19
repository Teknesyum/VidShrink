using Avalonia.Automation;
using Avalonia.Controls;
using System.Net;
using System.Net.Sockets;
using VidShrink.App.Localization;
using VidShrink.App.Share;
using VidShrink.Core.Share;

namespace VidShrink.Tests;

/// <summary>
/// Paylaşım hatasının kullanıcıya ulaşmayan yarısı: yeniden deneme.
/// </summary>
/// <remarks>
/// K8 borcu 5 "<c>ShareFailure</c>'ın 11 üyesinden 8'i okunmuyor" diyordu. Ölçüm bulguyu
/// düzeltti: ayrım kullanıcıya zaten ulaşıyor, enum üzerinden değil
/// <see cref="ShareDiagnosis.Key"/> üzerinden — 11 üyeye karşı 22 anahtar var ve hepsi 42
/// dilde pimli. Ölü olan <see cref="ShareResult.RetryAfter"/> ile
/// <see cref="ShareResult.SuggestedTargetId"/> idi: sınıflandırıcı hesaplıyor, üç gösterim
/// yüzeyi de atıyordu.
///
/// <para>Ölçünün sert kuralı idempotentlik: baytlar aktıktan sonraki bir hatada düğme
/// çıkmaz, yoksa kullanıcı aynı dosyanın ikinci kopyasını yükler.</para>
/// </remarks>
public sealed class PaylasimTekrarDenemeTests : IDisposable
{
    private readonly string _onceki = Strings.Language;

    public void Dispose() => Strings.Use(_onceki);

    private static ShareTargetTable Tablo() => ShareTargetTable.Load(ShareTargetTable.Locate());

    private static ShareResult Sonuc(ShareDiagnosis tani) => ShareResult.Failed(tani);

    // ---- Sınıflandırma kullanıcıya ulaşıyor -------------------------------------------------

    /// <summary>
    /// Borcun sorduğu şey: sınıflandırma fazla mı. Değil — <see cref="ShareFailure"/>'ın
    /// <c>None</c> dışındaki her üyesi sınıflandırıcıdan gerçekten çıkıyor. Küme eşitliği:
    /// üretilmeyen bir üye de, listede olmayan yeni bir üye de kırmızı.
    /// </summary>
    [Fact]
    public void HerHataTuruSiniflandiricidanUretiliyor()
    {
        var beklenen = Enum.GetValues<ShareFailure>()
            .Where(u => u != ShareFailure.None)
            .ToHashSet();

        var uretilen = TumTanilar().Select(t => t.Failure).ToHashSet();

        Assert.Equal(beklenen, uretilen);
    }

    /// <summary>
    /// Ayrımın kullanıcıya ulaştığı yer: her hata türünün kendi dil anahtarı var ve iki
    /// tür anahtar paylaşmıyor. Cümle enum'dan değil anahtardan geliyor.
    /// </summary>
    [Fact]
    public void IkiHataTuruAyniAnahtariPaylasmiyor()
    {
        var sahip = new Dictionary<string, ShareFailure>(StringComparer.Ordinal);

        foreach (var tani in TumTanilar())
        {
            if (sahip.TryGetValue(tani.Key, out var onceki))
            {
                Assert.Equal(onceki, tani.Failure);
                continue;
            }

            sahip[tani.Key] = tani.Failure;
        }

        Assert.True(sahip.Count > Enum.GetValues<ShareFailure>().Length);
    }

    // ---- Yeniden deneme kararı --------------------------------------------------------------

    /// <summary>429 isteği baştan reddeder; hangi adımda gelirse gelsin yeniden denenebilir.</summary>
    [Theory]
    [InlineData(ShareStep.Prepare)]
    [InlineData(ShareStep.Init)]
    [InlineData(ShareStep.Upload)]
    [InlineData(ShareStep.Confirm)]
    public void HizSiniriHerAdimdaYenidenDenenebilir(ShareStep adim) =>
        Assert.True(ShareRetry.IsRetryable(ShareFailure.RateLimited, adim));

    /// <summary>
    /// Ağ hatası ve 5xx yalnız baytlar akmadan önce yeniden denenebilir. Sonrasında
    /// dosyanın karşıya geçip geçmediği bilinmez; düğme orada çıksa kullanıcı ikinci bir
    /// kopya yükler.
    /// </summary>
    [Theory]
    [InlineData(ShareFailure.NetworkFailure, ShareStep.Prepare, true)]
    [InlineData(ShareFailure.NetworkFailure, ShareStep.Init, true)]
    [InlineData(ShareFailure.NetworkFailure, ShareStep.Upload, false)]
    [InlineData(ShareFailure.NetworkFailure, ShareStep.Confirm, false)]
    [InlineData(ShareFailure.ServiceError, ShareStep.Init, true)]
    [InlineData(ShareFailure.ServiceError, ShareStep.Upload, false)]
    public void AgVeSunucuHatasiYalnizBaytlarAkmadanOnce(ShareFailure hata, ShareStep adim, bool beklenen) =>
        Assert.Equal(beklenen, ShareRetry.IsRetryable(hata, adim));

    /// <summary>
    /// Olumsuz kontrol: kalan sekiz üyenin hiçbiri hiçbir adımda yeniden denenmiyor.
    /// Dosya tavanı aşıyorsa, jeton yoksa ya da disk doluysa aynı denemenin sonucu aynı.
    /// </summary>
    [Fact]
    public void KalanUyelerHicbirAdimdaYenidenDenenmiyor()
    {
        ShareFailure[] denenmez =
        {
            ShareFailure.None, ShareFailure.NotAuthorized, ShareFailure.TokenExpired,
            ShareFailure.QuotaExceeded, ShareFailure.FileTooLarge, ShareFailure.LocalDiskFull,
            ShareFailure.FileUnreadable, ShareFailure.Cancelled, ShareFailure.Unknown
        };

        foreach (var hata in denenmez)
        {
            foreach (var adim in Enum.GetValues<ShareStep>())
            {
                Assert.False(ShareRetry.IsRetryable(hata, adim), $"{hata}/{adim}");
            }
        }
    }

    /// <summary>
    /// Gerçek sunucu yanıtından uçtan uca: 429 yükleme adımında düğmeyi açıyor, aynı
    /// adımdaki 503 açmıyor. Adım tanıdan geliyor, elle verilmiyor.
    /// </summary>
    [Fact]
    public void AdimTanidanGeliyor()
    {
        var hedef = Tablo().DefaultTarget!;

        using var cok = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        var hizli = Sonuc(ShareErrorClassifier.FromResponse(hedef, cok, string.Empty, ShareStep.Upload));

        using var yok = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var servis = Sonuc(ShareErrorClassifier.FromResponse(hedef, yok, string.Empty, ShareStep.Upload));

        Assert.Equal(ShareStep.Upload, hizli.Step);
        Assert.True(hizli.IsRetryable());
        Assert.Equal(ShareStep.Upload, servis.Step);
        Assert.False(servis.IsRetryable());
    }

    // ---- Düğmenin hali ----------------------------------------------------------------------

    /// <summary>Sunucu bekleme istediyse düğme geri sayımla kilitli, sayı bitince açılıyor.</summary>
    [Fact]
    public void BeklemeVarkenDugmeKilitli()
    {
        var hedef = Tablo().DefaultTarget!;
        using var yanit = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        yanit.Headers.TryAddWithoutValidation("Retry-After", "120");
        var sonuc = Sonuc(ShareErrorClassifier.FromResponse(hedef, yanit, string.Empty, ShareStep.Init));

        Assert.Equal(120, ShareRetryPrompt.InitialSeconds(sonuc));

        var kilitli = ShareRetryPrompt.For(sonuc, null, 120);
        Assert.True(kilitli.Visible);
        Assert.False(kilitli.Enabled);
        Assert.Equal("settings.share.retry-in", kilitli.Key);

        var acik = ShareRetryPrompt.For(sonuc, null, 0);
        Assert.True(acik.Visible);
        Assert.True(acik.Enabled);
        Assert.Equal("settings.share.retry", acik.Key);
        Assert.Empty(acik.Args);
    }

    /// <summary>
    /// Tavanı aşan dosyada düğme "şu hedefle dene" oluyor ve o hedefin kimliğini taşıyor.
    /// Sınıflandırıcının hesapladığı öneri artık atılmıyor.
    /// </summary>
    [Fact]
    public void OnerilenHedefDugmeyeGeciyor()
    {
        var tablo = Tablo();
        var sonuc = Sonuc(ShareErrorClassifier.CheckSize(tablo.Find("uguu.se")!, 200L * 1024 * 1024, tablo)!);

        var hal = ShareRetryPrompt.For(sonuc, tablo, 0);

        Assert.True(hal.Visible);
        Assert.True(hal.Enabled);
        Assert.Equal("settings.share.retry-with", hal.Key);
        Assert.Equal("storage.to", hal.RetryTargetId);
        Assert.Equal(tablo.Find("storage.to")!.DisplayName, Assert.Single(hal.Args));
    }

    /// <summary>
    /// Olumsuz kontrol: öneriyi çözebilecek tablo yoksa düğme "şu hedefle dene" demiyor.
    /// <see cref="ShareFailure.FileTooLarge"/> kendi başına yeniden denenebilir değil, o
    /// yüzden düğme tümden gizleniyor.
    /// </summary>
    [Fact]
    public void TabloYokkaOnerilenHedefDugmesiCikmiyor()
    {
        var tablo = Tablo();
        var sonuc = Sonuc(ShareErrorClassifier.CheckSize(tablo.Find("uguu.se")!, 200L * 1024 * 1024, tablo)!);

        Assert.False(ShareRetryPrompt.For(sonuc, null, 0).Visible);
    }

    /// <summary>Başarılı sonuçta düğme hiç görünmüyor.</summary>
    [Fact]
    public void BasariliSonucDugmeGostermiyor()
    {
        var basarili = ShareResult.Success(new ShareLink("storage.to", "1", "https://ornek/1", "a.mp4", DateTimeOffset.UtcNow));

        Assert.False(ShareRetryPrompt.For(basarili, Tablo(), 0).Visible);
    }

    /// <summary>İptal yeniden denenmez: kullanıcı zaten kendi durdurdu.</summary>
    [Fact]
    public void IptalYenidenDenenmiyor()
    {
        var hedef = Tablo().DefaultTarget!;
        var sonuc = Sonuc(ShareErrorClassifier.FromException(
            hedef, new OperationCanceledException(), ShareStep.Upload));

        Assert.False(ShareRetryPrompt.For(sonuc, Tablo(), 0).Visible);
    }

    /// <summary>
    /// Düğmenin ekran okuyucuya verdiği ad. Üç paylaşım yüzeyinde de yazı kodda kuruluyor,
    /// XAML'da <c>Content</c> yok; 19 Eylül 2026'da arayüz taraması üçünü birden "adsız
    /// düğme" diye okudu. XAML'a sabit ad eklendi, bağlayıcı da yazıyı tazelerken adı canlı
    /// tutuyor. Ölçü iki hali de pimliyor: görünürken ad yazının kendisi, gizliyken yazı
    /// boşalıyor ama ad sabit karşılığa düşüyor — boş ada düşmüyor.
    /// </summary>
    [Fact]
    public void TekrarDugmesininErisimAdiBosKalmiyor()
    {
        var hedef = Tablo().DefaultTarget!;
        using var yanit = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        var sonuc = Sonuc(ShareErrorClassifier.FromResponse(hedef, yanit, string.Empty, ShareStep.Init));
        using var kapali = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var gizlenen = Sonuc(ShareErrorClassifier.FromResponse(hedef, kapali, string.Empty, ShareStep.Upload));
        Assert.False(gizlenen.IsRetryable());

        var olcu = AppHost.Run(() =>
        {
            var dugme = new Button();
            using var baglayici = new ShareRetryBinder(dugme, Tablo, _ => { });

            baglayici.Show(sonuc);
            var acikYazi = dugme.Content as string;
            var acikAd = AutomationProperties.GetName(dugme);

            baglayici.Show(gizlenen);
            return (acikYazi, acikAd, kapaliYazi: dugme.Content as string, kapaliAd: AutomationProperties.GetName(dugme));
        });

        Assert.Equal(Strings.Get("settings.share.retry"), olcu.acikYazi);
        Assert.Equal(olcu.acikYazi, olcu.acikAd);
        Assert.Equal(string.Empty, olcu.kapaliYazi);
        Assert.Equal(Strings.Get("settings.share.retry"), olcu.kapaliAd);
    }

    // ---- Dil ---------------------------------------------------------------------------------

    /// <summary>Üç yeni anahtar 42 dilde var ve yer tutucuları korunmuş.</summary>
    [Fact]
    public void UcAnahtarKirkIkiDildeVar()
    {
        string[] anahtarlar = { "settings.share.retry", "settings.share.retry-in", "settings.share.retry-with" };
        var klasorler = Directory.GetDirectories(
            Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales"));

        Assert.True(klasorler.Length >= 42, $"dil sayisi beklenenden az: {klasorler.Length}");

        foreach (var klasor in klasorler)
        {
            var dil = Path.GetFileName(klasor);
            foreach (var anahtar in anahtarlar)
            {
                var metin = Strings.GetIn(dil, anahtar);
                Assert.False(string.IsNullOrWhiteSpace(metin), $"{dil}/{anahtar}");
                Assert.NotEqual(anahtar, metin);

                if (anahtar == "settings.share.retry") Assert.DoesNotContain("{0}", metin, StringComparison.Ordinal);
                else Assert.Contains("{0}", metin, StringComparison.Ordinal);
            }
        }
    }

    // ---- Üretilen tüm tanılar ----------------------------------------------------------------

    private static IEnumerable<ShareDiagnosis> TumTanilar()
    {
        var tablo = Tablo();
        var uguu = tablo.Find("uguu.se")!;
        var storage = tablo.Find("storage.to")!;

        yield return ShareErrorClassifier.CheckSize(uguu, 200L * 1024 * 1024, tablo)!;
        yield return ShareErrorClassifier.CheckSize(uguu, 200L * 1024 * 1024)!;
        yield return ShareErrorClassifier.DeleteUnsupported(uguu);

        HttpStatusCode[] durumlar =
        {
            HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.TooManyRequests,
            HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.Gone,
            HttpStatusCode.RequestTimeout, HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.InsufficientStorage, HttpStatusCode.InternalServerError,
            HttpStatusCode.BadRequest, (HttpStatusCode)418
        };

        foreach (var durum in durumlar)
        {
            using var yanit = new HttpResponseMessage(durum);
            yield return ShareErrorClassifier.FromResponse(storage, yanit, string.Empty, ShareStep.Upload);
        }

        Exception[] istisnalar =
        {
            new OperationCanceledException(),
            new HttpRequestException("dns", new SocketException((int)SocketError.HostNotFound)),
            new FileNotFoundException("yok"),
            new IOException("dolu", unchecked((int)0x80070070)),
            new InvalidOperationException("beklenmeyen")
        };

        foreach (var istisna in istisnalar)
        {
            yield return ShareErrorClassifier.FromException(storage, istisna, ShareStep.Init);
        }
    }
}
