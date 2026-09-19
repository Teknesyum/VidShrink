using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using VidShrink.App.Localization;
using VidShrink.App.Share;
using VidShrink.Core.Share;

namespace VidShrink.Tests;

/// <summary>
/// Paylaşım hatalarının dili. Core cümle kurmuyor artık: anahtar ve ham argüman döndürüyor,
/// cümleyi <see cref="ShareMessage"/> arayüzün diliyle yazıyor.
/// </summary>
/// <remarks>
/// Kusur şuydu: <c>ShareErrorClassifier</c> Türkçe cümleyi kendi içinde kuruyor, boyutu ve
/// süreyi <c>CultureInfo.CurrentCulture</c> ile yazıyordu. İngilizce arayüzde kullanıcı
/// Türkçe hata okuyordu ve yükleme adının adı ("yükleme", ama "init") iki dilin karışımıydı.
/// Ölçü dört yeri birden tutar: Core'da cümle kalmadığını, anahtarların 42 dilde var
/// olduğunu, yer tutucuların çeviride korunduğunu ve gösterim yerlerinin ham metin
/// yazmadığını.
/// </remarks>
public sealed class PaylasimHataDiliTests : IDisposable
{
    private readonly string _onceki = Strings.Language;

    public void Dispose() => Strings.Use(_onceki);

    private static readonly string[] Anahtarlar =
    {
        "share.error.too-large-no-target", "share.error.too-large-try",
        "share.error.server-too-large", "share.error.rate-limited",
        "share.error.rate-limited-wait", "share.error.forbidden",
        "share.error.unauthorized", "share.error.gone", "share.error.timeout",
        "share.error.unavailable", "share.error.unavailable-wait",
        "share.error.storage-full", "share.error.server-fault",
        "share.error.bad-request", "share.error.unexpected",
        "share.error.unexpected-detail", "share.error.cancelled",
        "share.error.host-not-found", "share.error.connect-failed",
        "share.error.unreachable", "share.error.file-missing",
        "share.error.file-locked", "share.error.disk-full",
        "share.error.connection-dropped", "share.error.unexpected-exception",
        "share.error.no-delete-token-hours", "share.error.no-delete-token",
        "share.error.token-lost",
        "share.size.unlimited", "share.wait.seconds", "share.wait.minutes",
        "share.step.prepare", "share.step.init", "share.step.upload",
        "share.step.confirm", "share.step.probe", "share.step.delete"
    };

    private static ShareTargetTable Tablo() => ShareTargetTable.Load(ShareTargetTable.Locate());

    // ---- Core'da cümle kalmadı ------------------------------------------------------------

    /// <summary>
    /// Sınıflandırıcının ürettiği her tanı bir anahtar taşır ve anahtar dil dosyasında var.
    /// Cümlenin kendisi Core'da olsaydı bu yüzey boş kalırdı.
    /// </summary>
    [Fact]
    public void UretilenHerAnahtarKatalogdaVar()
    {
        var uretilen = TumTanilar().Select(t => t.Key).Distinct().ToArray();

        Assert.NotEmpty(uretilen);
        foreach (var anahtar in uretilen)
        {
            Assert.StartsWith("share.", anahtar, StringComparison.Ordinal);
            Assert.Contains(anahtar, Anahtarlar);
        }
    }

    /// <summary>
    /// Core'un paylaşım katmanında kullanıcıya gösterilecek Türkçe cümle kalmadı. Tarama
    /// belge satırlarını ve <c>throw</c> gövdelerini atar: atılan istisnanın metni
    /// geliştiriciye bakar, kullanıcıya giden yolda <see cref="ShareErrorClassifier"/>
    /// onu zaten bir anahtara çevirir.
    /// </summary>
    [Fact]
    public void CoreunPaylasimKatmanindaTurkceCumleYok()
    {
        var klasor = Path.Combine(TipSources.Root, "src", "VidShrink.Core", "Share");
        var bulunan = new List<string>();

        foreach (var dosya in Directory.GetFiles(klasor, "*.cs"))
        {
            foreach (var (satir, no) in KodSatirlari(dosya))
            {
                foreach (Match m in Regex.Matches(satir, "\"([^\"\\\\]|\\\\.)*\""))
                {
                    if (TurkceCumle(m.Value)) bulunan.Add($"{Path.GetFileName(dosya)}:{no} {m.Value}");
                }
            }
        }

        Assert.True(bulunan.Count == 0, string.Join(Environment.NewLine, bulunan));
    }

    /// <summary>
    /// Taramanın kör olmadığının pozitif kontrolü: aynı süzgeç, kaldırılan cümlelerden
    /// birine uygulandığında yakalar.
    /// </summary>
    [Fact]
    public void TaramaKorDegil()
    {
        Assert.True(TurkceCumle("\"Dosya çok büyük; diğer hedefi deneyin.\""));
        Assert.False(TurkceCumle("\"share.error.too-large-try\""));
        Assert.False(TurkceCumle("\"canDelete=false\""));

        var okunan = KodSatirlari(
            Path.Combine(TipSources.Root, "src", "VidShrink.Core", "Share", "ShareErrorClassifier.cs")).ToArray();

        Assert.Contains(okunan, s => s.Satir.Contains("share.error.", StringComparison.Ordinal));

        var atisli = KodSatirlari(
            Path.Combine(TipSources.Root, "src", "VidShrink.Core", "Share", "IShareProvider.cs")).ToArray();

        Assert.NotEmpty(atisli);
        Assert.DoesNotContain(atisli, s => s.Satir.Contains("tanınan bir uç nokta", StringComparison.Ordinal));
    }

    // ---- 42 dil ---------------------------------------------------------------------------

    [Fact]
    public void OtuzAltiAnahtarButunDillerdeVar()
    {
        var diller = Directory.GetDirectories(
            Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales"));

        Assert.True(diller.Length >= 42, $"dil sayısı beklenenden az: {diller.Length}");

        foreach (var dil in diller)
        {
            var belge = Katalog(dil);
            foreach (var anahtar in Anahtarlar)
            {
                Assert.True(belge.TryGetProperty(anahtar, out var deger),
                    $"{Path.GetFileName(dil)} dilinde {anahtar} yok");
                Assert.False(string.IsNullOrWhiteSpace(deger.GetString()),
                    $"{Path.GetFileName(dil)} dilinde {anahtar} boş");
            }
        }
    }

    /// <summary>
    /// Çeviri yer tutucuyu düşüremez. Düşerse cümlede boyut ya da hedef adı hiç görünmez;
    /// fazladan yer tutucu ise <see cref="string.Format(string,object?[])"/> atar.
    /// </summary>
    [Fact]
    public void YerTutucularHerDildeKorunuyor()
    {
        var ingilizce = Katalog(Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales", "en"));
        var diller = Directory.GetDirectories(
            Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales"));

        foreach (var dil in diller)
        {
            var belge = Katalog(dil);
            foreach (var anahtar in Anahtarlar)
            {
                var beklenen = YerTutucular(ingilizce.GetProperty(anahtar).GetString()!);
                var olan = YerTutucular(belge.GetProperty(anahtar).GetString()!);
                Assert.Equal(beklenen, olan);
            }
        }
    }

    // ---- Cümle arayüzün diliyle yazılıyor ---------------------------------------------------

    /// <summary>
    /// Aynı tanı iki dilde iki ayrı cümle veriyor, ve boyut ayıracı makinenin değil arayüzün
    /// kültüründen geliyor. Eski kodda ikisi de sabitti.
    /// </summary>
    [Fact]
    public void AyniTaniIkiDildeIkiCumle()
    {
        var tablo = Tablo();
        var tani = ShareErrorClassifier.CheckSize(tablo.Find("uguu.se")!, 200L * 1024 * 1024, tablo)!;

        Strings.Use("tr");
        var turkce = ShareMessage.Of(tani.Key, tani.Args);

        Strings.Use("en");
        var ingilizce = ShareMessage.Of(tani.Key, tani.Args);

        Assert.NotEqual(turkce, ingilizce);
        Assert.Contains("128 MiB", turkce, StringComparison.Ordinal);
        Assert.Contains("128 MiB", ingilizce, StringComparison.Ordinal);
        Assert.Contains("storage.to", turkce, StringComparison.Ordinal);
        Assert.DoesNotContain("{0}", turkce, StringComparison.Ordinal);
        Assert.DoesNotContain("{0}", ingilizce, StringComparison.Ordinal);
    }

    /// <summary>Tavanı <c>0</c> olan hedef "sınırsız" yazılır, "0 B" değil.</summary>
    [Fact]
    public void SinirsizTavanSayiyaDonmuyor()
    {
        Strings.Use("tr");

        var yazilan = ShareMessage.Of("share.error.server-too-large", new object[] { "sınırsız.example", 0L });

        Assert.Equal(Strings.Get("share.error.server-too-large", "sınırsız.example", Strings.Get("share.size.unlimited")),
            yazilan);
        Assert.DoesNotContain(" 0 ", yazilan, StringComparison.Ordinal);
    }

    /// <summary>Bekleme süresi bir dakikanın altında saniyeyle, üstünde dakikayla yazılır.</summary>
    [Theory]
    [InlineData(45, "share.wait.seconds")]
    [InlineData(120, "share.wait.minutes")]
    public void BeklemeSuresiBirimiSinirdanGeliyor(int saniye, string beklenenAnahtar)
    {
        Strings.Use("tr");

        var yazilan = ShareMessage.Of(
            "share.error.rate-limited-wait",
            new object[] { "storage.to", TimeSpan.FromSeconds(saniye) });

        var digeri = beklenenAnahtar == "share.wait.seconds" ? "share.wait.minutes" : "share.wait.seconds";
        Assert.Contains(Kok(beklenenAnahtar), yazilan, StringComparison.Ordinal);
        Assert.DoesNotContain(Kok(digeri), yazilan, StringComparison.Ordinal);
    }

    /// <summary>Adımın adı da dilden geliyor; ham protokol sözcüğü cümleye girmiyor.</summary>
    [Fact]
    public void AdimAdiDildenGeliyor()
    {
        Strings.Use("tr");
        var turkce = ShareMessage.Of("share.error.connection-dropped", new object[] { "storage.to", ShareStep.Init });

        Strings.Use("en");
        var ingilizce = ShareMessage.Of("share.error.connection-dropped", new object[] { "storage.to", ShareStep.Init });

        Assert.Contains(Strings.Get("share.step.init"), ingilizce, StringComparison.Ordinal);
        Assert.NotEqual(turkce, ingilizce);
        Assert.DoesNotContain("Init", turkce, StringComparison.Ordinal);
    }

    /// <summary>Başarılı sonuçta yazılacak cümle yok; boş dizge döner.</summary>
    [Fact]
    public void BasariliSonucCumleUretmiyor()
    {
        var link = new ShareLink("uguu.se", "a", "https://u/a", "a.mp4", DateTimeOffset.UtcNow);
        Assert.Equal(string.Empty, ShareMessage.Of(ShareResult.Success(link)));
    }

    // ---- Gösterim yerleri -------------------------------------------------------------------

    /// <summary>
    /// Dört gösterim yerinin dördü de <see cref="ShareMessage"/>'dan geçiyor. Biri ham alanı
    /// okursa o ekran çeviri katmanını atlar ve kusur sessizce geri gelir.
    /// </summary>
    [Theory]
    [InlineData("src/VidShrink.App/MainWindow.axaml.cs")]
    [InlineData("src/VidShrink.App/Recorder/RecorderView.Paylas.cs")]
    [InlineData("src/VidShrink.App/ShrinkJobWindow.Paylas.cs")]
    public void GosterimYerleriHamAlaniOkumuyor(string yol)
    {
        var metin = File.ReadAllText(Path.Combine(TipSources.Root, yol.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("ShareMessage.Of(", metin, StringComparison.Ordinal);
        Assert.DoesNotContain("result.Message", metin, StringComparison.Ordinal);
    }

    // ---- Yardımcılar -------------------------------------------------------------------------

    private static string Kok(string anahtar)
    {
        var sablon = Strings.Get(anahtar);
        var yer = sablon.IndexOf("{0}", StringComparison.Ordinal);
        return yer < 0 ? sablon : sablon[(yer + 3)..].Trim();
    }

    private static JsonElement Katalog(string dilKlasoru)
    {
        var json = File.ReadAllText(Path.Combine(dilKlasoru, "settings.json"));
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static IReadOnlyList<string> YerTutucular(string sablon) =>
        Regex.Matches(sablon, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    private static IEnumerable<(string Satir, int No)> KodSatirlari(string dosya)
    {
        var no = 0;
        var atisSuruyor = false;

        foreach (var ham in File.ReadLines(dosya))
        {
            no++;
            var satir = ham.TrimStart();

            if (atisSuruyor)
            {
                if (satir.EndsWith(";", StringComparison.Ordinal)) atisSuruyor = false;
                continue;
            }

            if (satir.StartsWith("///", StringComparison.Ordinal)) continue;
            if (satir.StartsWith("//", StringComparison.Ordinal)) continue;

            if (IstisnaKurulumu(satir))
            {
                atisSuruyor = !satir.EndsWith(";", StringComparison.Ordinal);
                continue;
            }

            yield return (ham, no);
        }
    }

    /// <summary>
    /// İstisna kurulan satır. İstisnanın iletisi geliştirici tanısıdır: kullanıcıya giden
    /// yolda <see cref="ShareErrorClassifier.FromException"/> onu bir anahtara çevirir,
    /// ham hâli yalnız <see cref="ShareResult.Detail"/> alanında kalır.
    /// </summary>
    private static bool IstisnaKurulumu(string satir) =>
        satir.Contains("throw new", StringComparison.Ordinal) ||
        (satir.Contains("new ", StringComparison.Ordinal) &&
         satir.Contains("Exception(", StringComparison.Ordinal));

    /// <summary>
    /// Türkçe harf taşıyan ve boşluk içeren dizge, kullanıcıya gösterilmek üzere yazılmış
    /// bir cümledir. Anahtarlar noktalı ve boşluksuz olduğu için elenirler.
    /// </summary>
    private static bool TurkceCumle(string dizge) =>
        dizge.Contains(' ', StringComparison.Ordinal) &&
        dizge.Any(c => "çğıöşüÇĞİÖŞÜ".Contains(c, StringComparison.Ordinal));

    private static IEnumerable<ShareDiagnosis> TumTanilar()
    {
        var tablo = Tablo();
        var uguu = tablo.Find("uguu.se")!;
        var storage = tablo.Find("storage.to")!;

        yield return ShareErrorClassifier.CheckSize(uguu, 200L * 1024 * 1024, tablo)!;
        yield return ShareErrorClassifier.CheckSize(uguu, 200L * 1024 * 1024)!;
        yield return ShareErrorClassifier.DeleteUnsupported(uguu);
        yield return ShareErrorClassifier.DeleteUnsupported(storage);

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
            using var bos = new HttpResponseMessage(durum);
            yield return ShareErrorClassifier.FromResponse(storage, bos, string.Empty, ShareStep.Upload);

            using var dolu = new HttpResponseMessage(durum);
            yield return ShareErrorClassifier.FromResponse(storage, dolu, "sunucu metni", ShareStep.Upload);
        }

        using var bekleyen = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        bekleyen.Headers.TryAddWithoutValidation("Retry-After", "120");
        yield return ShareErrorClassifier.FromResponse(storage, bekleyen, string.Empty, ShareStep.Init);

        using var gecikmeli = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        gecikmeli.Headers.TryAddWithoutValidation("Retry-After", "30");
        yield return ShareErrorClassifier.FromResponse(storage, gecikmeli, string.Empty, ShareStep.Confirm);

        Exception[] istisnalar =
        {
            new OperationCanceledException(),
            new HttpRequestException("dns", new SocketException((int)SocketError.HostNotFound)),
            new HttpRequestException("reddedildi", new SocketException((int)SocketError.ConnectionRefused)),
            new HttpRequestException("ulaşılamadı"),
            new FileNotFoundException("yok"),
            new UnauthorizedAccessException("kilitli"),
            new IOException("dolu", unchecked((int)0x80070070)),
            new IOException("koptu"),
            new InvalidOperationException("beklenmeyen")
        };

        foreach (var istisna in istisnalar)
        {
            yield return ShareErrorClassifier.FromException(storage, istisna, ShareStep.Delete);
        }
    }
}
