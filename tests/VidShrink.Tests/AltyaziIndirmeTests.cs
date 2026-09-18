using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Text;
using Avalonia.Controls;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Core.Share;
using VidShrink.Core.Subtitles;
using Xunit;

namespace VidShrink.Tests;

internal static class AltyaziKanit
{
    private static string Kok => Path.Combine(GirdiKanit.Root, ".calisma", "p28-altyazi");

    internal static string Folder
    {
        get
        {
            var path = Kok;
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static string Temiz(string ad)
    {
        var path = Path.Combine(Folder, ad);
        if (Directory.Exists(path)) Directory.Delete(path, true);
        Directory.CreateDirectory(path);
        return path;
    }

    internal static void Yaz(string ad, string govde)
        => File.WriteAllText(Path.Combine(Folder, ad), govde, new UTF8Encoding(false));

    /// <summary>
    /// Son asertten sonra çağrılır: yeşil koşum kendi bıraktığını siler, kırmızı koşum
    /// kanıtını korur çünkü düşen asert buraya hiç gelmez. Klasör boşalınca o da gider.
    /// <paramref name="adlar"/> dosya adı ya da <see cref="Temiz"/>'in bıraktığı klasör adı olabilir.
    /// </summary>
    internal static void Kapat(params string[] adlar) => KanitKapanisi.Kapat(Kok, adlar);
}

/// <summary>
/// Sahte HTTP: her isteğin adresini ve gövdesini kaydeder, sıradaki hazır cevabı döner.
/// Ağa çıkmaz; çıkmadığı <see cref="Istekler"/> sayısıyla okunur.
/// </summary>
internal sealed class SahteAg : IHttpTransport
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _cevaplar = new();

    internal List<string> Istekler { get; } = new();

    internal List<string> Govdeler { get; } = new();

    internal List<string> Basliklar { get; } = new();

    /// <summary>
    /// Kimlik basligi tel uzerindeki haliyle. <see cref="Basliklar"/> cok degerli basliklari
    /// virgulle birlestiriyor; User-Agent bir urun listesi oldugu icin orada
    /// <c>Ad,vSurum</c> gorunur, giden istekte ise bosluklu.
    /// </summary>
    internal List<string> Kimlikler { get; } = new();

    internal SahteAg Sonra(HttpStatusCode kod, string govde, string tur = "application/json")
    {
        _cevaplar.Enqueue(_ => new HttpResponseMessage(kod) { Content = new StringContent(govde, Encoding.UTF8, tur) });
        return this;
    }

    /// <summary>Cevabi basliklarini duzenleyerek kuyruga koyar (Retry-After gibi).</summary>
    internal SahteAg SonraBasliklayan(HttpStatusCode kod, string govde, Action<HttpResponseMessage> duzen)
    {
        _cevaplar.Enqueue(_ =>
        {
            var yanit = new HttpResponseMessage(kod) { Content = new StringContent(govde, Encoding.UTF8, "application/json") };
            duzen(yanit);
            return yanit;
        });
        return this;
    }

    internal SahteAg SonraBayt(byte[] bayt)
    {
        _cevaplar.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bayt) });
        return this;
    }

    internal SahteAg SonraAtar()
    {
        _cevaplar.Enqueue(_ => throw new HttpRequestException("ag yok"));
        return this;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Istekler.Add(request.RequestUri?.ToString() ?? "");
        Basliklar.Add(string.Join(";", request.Headers.Select(h => h.Key + "=" + string.Join(",", h.Value))));
        Kimlikler.Add(request.Headers.UserAgent.ToString());
        Govdeler.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
        if (_cevaplar.Count == 0) throw new InvalidOperationException("sahte agda hazir cevap kalmadi");
        return _cevaplar.Dequeue()(request);
    }
}

/// <summary>Testin dediğini dönen sağlayıcı; oynatıcı kollarını bununla sürüyoruz.</summary>
internal sealed class SahteSaglayici : ISubtitleProvider
{
    internal SubtitleSearchResult Arama { get; set; } = SubtitleSearchResult.Failed(SubtitleOutcome.NoResult);

    internal SubtitleDownloadResult Indirme { get; set; } = SubtitleDownloadResult.Failed(SubtitleOutcome.NetworkError);

    internal bool Kurulu { get; set; } = true;

    internal List<SubtitleQuery> Sorgular { get; } = new();

    internal List<long> Indirilenler { get; } = new();

    /// <summary>Kurulursa arama burada bekler; arama sürerken ekranda ne yazdığı okunabilir.</summary>
    internal TaskCompletionSource? AramaKapisi { get; set; }

    internal SubtitleSession? Oturum { get; set; }

    /// <summary>Oturum var mi; olculerin cogu "var" varsayar, yoklugu ayri olcude surulur.</summary>
    internal bool OturumVar { get; set; } = true;

    internal List<string> Girisler { get; } = new();

    public bool IsConfigured => Kurulu;

    public bool HasSession => OturumVar;

    public Task<SubtitleLoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken)
    {
        Girisler.Add(username);
        return Task.FromResult(Oturum is null
            ? SubtitleLoginResult.Failed(SubtitleOutcome.BadLogin)
            : new SubtitleLoginResult(SubtitleOutcome.Ok, Oturum));
    }

    public void SignOut() => Oturum = null;

    public Task<SubtitleSearchResult> SearchAsync(SubtitleQuery query, CancellationToken cancellationToken)
    {
        Sorgular.Add(query);
        if (AramaKapisi is { } kapi) return kapi.Task.ContinueWith(_ => Arama, cancellationToken,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return Task.FromResult(Arama);
    }

    public Task<SubtitleDownloadResult> DownloadAsync(SubtitleCandidate candidate, string mediaPath, CancellationToken cancellationToken)
    {
        Indirilenler.Add(candidate.FileId);
        return Task.FromResult(Indirme);
    }
}

/// <summary>
/// P28 — OpenSubtitles'tan altyazı indirme. Hiçbir ölçü gerçek ağa çıkmaz: sağlayıcı
/// <see cref="SahteAg"/> ile, oynatıcı <see cref="SahteSaglayici"/> ile sürülür.
/// </summary>
public class AltyaziIndirmeTests
{
    // ---------- moviehash ----------

    /// <summary>
    /// Beklenti algoritmanın tanımından türetiliyor, koddan değil: bütün sözcükler sıfırsa
    /// iki bloğun toplamı sıfırdır, geriye yalnız dosya boyutu kalır.
    /// </summary>
    [Fact]
    public void SifirDolusuDosyaninHashiBoyutunKendisi()
    {
        var kok = AltyaziKanit.Temiz("hash-sifir");
        var dosya = Path.Combine(kok, "sifir.bin");
        const long boyut = 256 * 1024;
        File.WriteAllBytes(dosya, new byte[boyut]);

        var beklenen = ((ulong)boyut).ToString("x16", CultureInfo.InvariantCulture);
        AltyaziKanit.Yaz("hash-sifir.txt", $"boyut {boyut}; beklenen {beklenen}; okunan {MovieHash.Compute(dosya)}");

        Assert.Equal(beklenen, MovieHash.Compute(dosya));

        AltyaziKanit.Kapat("hash-sifir", "hash-sifir.txt");
    }

    /// <summary>
    /// İki uçtaki bloklar gerçekten toplanıyor mu: ilk 64 KB'ın her sözcüğü 1, son 64 KB'ın
    /// her sözcüğü 2. Beklenen değer elle sayılıyor — boyut + 8192·1 + 8192·2 — dolayısıyla
    /// blokları okumayan ya da yalnız birini okuyan bir kod bu ölçüyü geçemez.
    /// </summary>
    [Fact]
    public void IkiUcBlokIcerigeGoreToplanir()
    {
        var kok = AltyaziKanit.Temiz("hash-iki-uc");
        var dosya = Path.Combine(kok, "iki-uc.bin");
        const long boyut = 256 * 1024;
        const int sozcuk = MovieHash.ChunkBytes / sizeof(ulong);

        var bayt = new byte[boyut];
        for (var i = 0; i < sozcuk; i++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(bayt.AsSpan(i * sizeof(ulong)), 1);
            BinaryPrimitives.WriteUInt64LittleEndian(bayt.AsSpan((int)boyut - MovieHash.ChunkBytes + i * sizeof(ulong)), 2);
        }

        File.WriteAllBytes(dosya, bayt);

        var beklenen = ((ulong)boyut + (ulong)sozcuk * 1 + (ulong)sozcuk * 2).ToString("x16", CultureInfo.InvariantCulture);
        var okunan = MovieHash.Compute(dosya);
        AltyaziKanit.Yaz("hash-iki-uc.txt", $"sozcuk {sozcuk}; beklenen {beklenen}; okunan {okunan}");

        Assert.Equal(beklenen, okunan);

        AltyaziKanit.Kapat("hash-iki-uc", "hash-iki-uc.txt");
    }

    /// <summary>
    /// Negatif kontrol: ortadaki bayt hash'e girmez, uçtaki bayt girer. Dosyanın tamamını
    /// okuyan bir uygulama birinci iddiada, hiç okumayan ikincide düşer.
    /// </summary>
    [Fact]
    public void OrtadakiBaytHashiDegistirmezUctakiDegistirir()
    {
        var kok = AltyaziKanit.Temiz("hash-orta");
        var dosya = Path.Combine(kok, "orta.bin");
        const int boyut = 320 * 1024;
        var bayt = new byte[boyut];
        File.WriteAllBytes(dosya, bayt);
        var once = MovieHash.Compute(dosya);

        bayt[boyut / 2] = 0xAB;
        File.WriteAllBytes(dosya, bayt);
        var orta = MovieHash.Compute(dosya);

        bayt[7] = 0xCD;
        File.WriteAllBytes(dosya, bayt);
        var uc = MovieHash.Compute(dosya);

        AltyaziKanit.Yaz("hash-orta.txt", $"once {once}; orta {orta}; uc {uc}");

        Assert.Equal(once, orta);
        Assert.NotEqual(once, uc);

        AltyaziKanit.Kapat("hash-orta", "hash-orta.txt");
    }

    /// <summary>
    /// Toplama 64 bitte sarılır. İlk sözcük <c>ulong.MaxValue</c> iken toplam
    /// boyut + (2^64 − 1) ≡ boyut − 1 (mod 2^64). Taşmayı yakalayan ya da daha geniş bir
    /// türe kaçan uygulama başka sayı verir.
    /// </summary>
    [Fact]
    public void ToplamaAltmisDortBitteSarilir()
    {
        var kok = AltyaziKanit.Temiz("hash-sarma");
        var dosya = Path.Combine(kok, "sarma.bin");
        const long boyut = 2L * MovieHash.ChunkBytes;
        var bayt = new byte[boyut];
        BinaryPrimitives.WriteUInt64LittleEndian(bayt.AsSpan(0), ulong.MaxValue);
        File.WriteAllBytes(dosya, bayt);

        var beklenen = ((ulong)boyut - 1UL).ToString("x16", CultureInfo.InvariantCulture);
        var okunan = MovieHash.Compute(dosya);
        AltyaziKanit.Yaz("hash-sarma.txt", $"boyut {boyut}; beklenen {beklenen}; okunan {okunan}");

        Assert.Equal(beklenen, okunan);

        AltyaziKanit.Kapat("hash-sarma", "hash-sarma.txt");
    }

    /// <summary>
    /// Blok boyu protokolün dışarıdan verdiği sayıdır: her iki uçtan tam 64 KiB.
    /// Ölçü, kodun kendi sabitini değil bu sayıyı kullanır; yoksa sabit kaydığında
    /// beklenti onunla birlikte kayar ve ölçü kör kalır.
    /// </summary>
    /// <remarks>
    /// 192 KiB'lık dosyada 32768. bayt ilk 64 KiB'ın içinde ama ilk 32 KiB'ın dışında,
    /// 131072. bayt son 64 KiB'ın içinde ama son 32 KiB'ın dışında. İki uçtan 64 KiB
    /// okuyan uygulama ikisini de toplar: 196608 + 1 + 2 = 196611 = 0x30003.
    /// Blok küçülürse ikisi de dışarıda kalır ve toplam yalnız boyut olur.
    /// </remarks>
    [Fact]
    public void BlokUclardanTamAltmisDortKibOkunur()
    {
        var kok = AltyaziKanit.Temiz("hash-blok");
        var dosya = Path.Combine(kok, "blok.bin");
        const int boyut = 3 * 65536;
        var bayt = new byte[boyut];
        BinaryPrimitives.WriteUInt64LittleEndian(bayt.AsSpan(32768), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(bayt.AsSpan(131072), 2);
        File.WriteAllBytes(dosya, bayt);

        var beklenen = ((ulong)boyut + 1UL + 2UL).ToString("x16", CultureInfo.InvariantCulture);
        var yalnizBoyut = ((ulong)boyut).ToString("x16", CultureInfo.InvariantCulture);
        var okunan = MovieHash.Compute(dosya);
        AltyaziKanit.Yaz("hash-blok.txt", $"beklenen {beklenen}; yalniz boyut {yalnizBoyut}; okunan {okunan}");

        Assert.Equal(beklenen, okunan);
        Assert.NotEqual(yalnizBoyut, okunan);

        AltyaziKanit.Kapat("hash-blok", "hash-blok.txt");
    }

    /// <summary>128 KB'ın altında iki blok üst üste binerdi; hash üretilmez.</summary>
    [Fact]
    public void KucukDosyaHashUretmez()
    {
        var kok = AltyaziKanit.Temiz("hash-kucuk");
        var kucuk = Path.Combine(kok, "kucuk.bin");
        var tam = Path.Combine(kok, "tam.bin");
        File.WriteAllBytes(kucuk, new byte[MovieHash.MinimumBytes - 1]);
        File.WriteAllBytes(tam, new byte[MovieHash.MinimumBytes]);

        Assert.Null(MovieHash.Compute(kucuk));
        Assert.NotNull(MovieHash.Compute(tam));

        AltyaziKanit.Kapat("hash-kucuk");
    }

    // ---------- sağlayıcı: arama ----------

    private static string AramaCevabi(params (long Id, string Dil, int Sayi, bool Hash)[] satirlar)
    {
        var govde = new StringBuilder("{\"data\":[");
        govde.Append(string.Join(",", satirlar.Select(s =>
            "{\"attributes\":{\"language\":\"" + s.Dil + "\",\"release\":\"surum-" + s.Id +
            "\",\"download_count\":" + s.Sayi.ToString(CultureInfo.InvariantCulture) +
            ",\"moviehash_match\":" + (s.Hash ? "true" : "false") +
            ",\"files\":[{\"file_id\":" + s.Id.ToString(CultureInfo.InvariantCulture) +
            ",\"file_name\":\"dosya-" + s.Id + ".srt\"}]}}")));
        govde.Append("]}");
        return govde.ToString();
    }

    private static SubtitleQuery Sorgu(string? hash = "abc123def4567890")
        => new(hash, "Film Adi", new[] { "tr", "en" });

    [Fact]
    public async Task AramaIstegiHashVeDiliTasir()
    {
        var ag = new SahteAg().Sonra(HttpStatusCode.OK, AramaCevabi((11, "tr", 5, true)));
        var saglayici = new OpenSubtitlesProvider(ag, "ANAHTAR");

        var sonuc = await saglayici.SearchAsync(Sorgu(), CancellationToken.None);
        AltyaziKanit.Yaz("arama-istegi.txt", string.Join("\n", ag.Istekler) + "\n" + string.Join("\n", ag.Basliklar));

        Assert.Equal(SubtitleOutcome.Ok, sonuc.Outcome);
        Assert.Single(ag.Istekler);
        Assert.Contains("moviehash=abc123def4567890", ag.Istekler[0], StringComparison.Ordinal);
        Assert.Contains("languages=en,tr", ag.Istekler[0], StringComparison.Ordinal);
        Assert.Contains("Api-Key=ANAHTAR", ag.Basliklar[0], StringComparison.Ordinal);

        // Parametreler alfabetik: languages, moviehash. Sirasiz istek yonlendiriliyor.
        Assert.EndsWith("/subtitles?languages=en,tr&moviehash=abc123def4567890", ag.Istekler[0], StringComparison.Ordinal);

        AltyaziKanit.Kapat("arama-istegi.txt");
    }

    /// <summary>
    /// Saglayici kimligi "Uygulama vX.Y.Z" biciminde istiyor; baska bicim 403 ile geri
    /// ceviriliyor. Paylasim katmaninin "ad/surum (+adres)" bicimi burada gecmez, bu yuzden
    /// olcu bicimi dogrudan pimliyor.
    /// </summary>
    [Fact]
    public async Task KimlikBasligiSaglayicininIstedigiBicimde()
    {
        var ag = new SahteAg().Sonra(HttpStatusCode.OK, AramaCevabi((1, "tr", 1, true)));
        await new OpenSubtitlesProvider(ag, "ANAHTAR").SearchAsync(Sorgu(), CancellationToken.None);

        var kimlik = ag.Kimlikler[0];
        AltyaziKanit.Yaz("kimlik.txt", kimlik + "\npaylasim: " + ShareIdentity.UserAgent);

        Assert.Matches(@"^VidShrink v\d+\.\d+\.\d+$", kimlik);
        Assert.DoesNotContain("/", kimlik, StringComparison.Ordinal);
        Assert.NotEqual(ShareIdentity.UserAgent, kimlik);

        AltyaziKanit.Kapat("kimlik.txt");
    }

    /// <summary>Hash tutmazsa ikinci istek ad aramasıdır; iki istek de tek aramada gider.</summary>
    [Fact]
    public async Task HashTutmazsaAdlaAranir()
    {
        var ag = new SahteAg()
            .Sonra(HttpStatusCode.OK, "{\"data\":[]}")
            .Sonra(HttpStatusCode.OK, AramaCevabi((22, "en", 3, false)));
        var saglayici = new OpenSubtitlesProvider(ag, "ANAHTAR");

        var sonuc = await saglayici.SearchAsync(Sorgu(), CancellationToken.None);
        AltyaziKanit.Yaz("arama-ad.txt", string.Join("\n", ag.Istekler));

        Assert.Equal(SubtitleOutcome.Ok, sonuc.Outcome);
        Assert.Equal(2, ag.Istekler.Count);
        Assert.Contains("moviehash=", ag.Istekler[0], StringComparison.Ordinal);
        Assert.DoesNotContain("moviehash=", ag.Istekler[1], StringComparison.Ordinal);
        Assert.Contains("query=film+adi", ag.Istekler[1], StringComparison.Ordinal);
        Assert.Equal(22, sonuc.Candidates[0].FileId);

        AltyaziKanit.Kapat("arama-ad.txt");
    }

    /// <summary>
    /// Sıralama: hash eşleşmesi her şeyin önünde, sonra yeğlenen dil sırası (arayüz dili
    /// İngilizceden önce), sonra indirilme sayısı. Cevaptaki sıra bilerek ters verildi.
    /// </summary>
    [Fact]
    public async Task SiralamaHashiSonraYeglenenDiliSonraIndirmeSayisiniAlir()
    {
        var ag = new SahteAg().Sonra(HttpStatusCode.OK, AramaCevabi(
            (1, "en", 900, false),
            (2, "tr", 10, false),
            (3, "tr", 400, false),
            (4, "en", 1, true)));
        var saglayici = new OpenSubtitlesProvider(ag, "ANAHTAR");

        var sonuc = await saglayici.SearchAsync(Sorgu(), CancellationToken.None);
        var sira = sonuc.Candidates.Select(c => c.FileId).ToList();
        AltyaziKanit.Yaz("siralama.txt", string.Join(" > ", sonuc.Candidates.Select(c => $"{c.FileId}/{c.Language}/{c.DownloadCount}/hash={c.HashMatch}")));

        Assert.Equal(new long[] { 4, 3, 2, 1 }, sira);

        AltyaziKanit.Kapat("siralama.txt");
    }

    // ---------- sağlayıcı: indirme ----------

    [Fact]
    public async Task IndirilenDosyaVideonunYaninaDilEkiyleYazilir()
    {
        var kok = AltyaziKanit.Temiz("indir");
        var film = Path.Combine(kok, "Film Adi.mkv");
        File.WriteAllBytes(film, new byte[16]);
        const string govde = "1\r\n00:00:01,000 --> 00:00:02,000\r\nmerhaba\r\n";

        var ag = new SahteAg()
            .Sonra(HttpStatusCode.OK, "{\"link\":\"https://dl.example/abc\",\"file_name\":\"x.srt\",\"remaining\":4}")
            .SonraBayt(Encoding.UTF8.GetBytes(govde));
        var saglayici = new OpenSubtitlesProvider(ag, "ANAHTAR");

        var sonuc = await saglayici.DownloadAsync(new SubtitleCandidate(77, "tr", "s", "d.srt", 1, true), film, CancellationToken.None);
        AltyaziKanit.Yaz("indir.txt", $"{sonuc.Outcome} {sonuc.Path} kalan {sonuc.Remaining}\n" + string.Join("\n", ag.Govdeler));

        Assert.Equal(SubtitleOutcome.Ok, sonuc.Outcome);
        Assert.Equal(Path.Combine(kok, "Film Adi.tr.srt"), sonuc.Path);
        Assert.Equal(govde, File.ReadAllText(sonuc.Path!));
        Assert.Equal(4, sonuc.Remaining);
        Assert.Contains("\"file_id\":77", ag.Govdeler[0], StringComparison.Ordinal);

        AltyaziKanit.Kapat("indir", "indir.txt");
    }

    /// <summary>Kullanıcının kendi altyazısı ezilmez; ikinci dosya numaralanır.</summary>
    [Fact]
    public async Task VarOlanAltyaziEzilmez()
    {
        var kok = AltyaziKanit.Temiz("indir-ezme");
        var film = Path.Combine(kok, "Film.mkv");
        File.WriteAllBytes(film, new byte[16]);
        File.WriteAllText(Path.Combine(kok, "Film.tr.srt"), "elle yazilmis", new UTF8Encoding(false));

        var ag = new SahteAg()
            .Sonra(HttpStatusCode.OK, "{\"link\":\"https://dl.example/abc\",\"remaining\":1}")
            .SonraBayt(Encoding.UTF8.GetBytes("indirilen"));
        var saglayici = new OpenSubtitlesProvider(ag, "ANAHTAR");

        var sonuc = await saglayici.DownloadAsync(new SubtitleCandidate(5, "tr", "s", "d.srt", 1, true), film, CancellationToken.None);

        Assert.Equal(Path.Combine(kok, "Film.tr.2.srt"), sonuc.Path);
        Assert.Equal("elle yazilmis", File.ReadAllText(Path.Combine(kok, "Film.tr.srt")));
        Assert.Equal("indirilen", File.ReadAllText(sonuc.Path!));

        AltyaziKanit.Kapat("indir-ezme");
    }

    // ---------- hata kolları ----------

    /// <summary>Anahtar yokken özellik kendini kapatır: tek bir istek bile gitmez.</summary>
    [Fact]
    public async Task AnahtarYokkenAgaCikilmaz()
    {
        var ag = new SahteAg();
        var saglayici = new OpenSubtitlesProvider(ag, "   ");

        var arama = await saglayici.SearchAsync(Sorgu(), CancellationToken.None);
        var indirme = await saglayici.DownloadAsync(new SubtitleCandidate(1, "tr", "s", "d", 0, false), "C:\\film\\a.mkv", CancellationToken.None);

        Assert.False(saglayici.IsConfigured);
        Assert.Equal(SubtitleOutcome.NoKey, arama.Outcome);
        Assert.Equal(SubtitleOutcome.NoKey, indirme.Outcome);
        Assert.Empty(ag.Istekler);
    }

    [Fact]
    public async Task SonucYoksaNoResultDoner()
    {
        var ag = new SahteAg()
            .Sonra(HttpStatusCode.OK, "{\"data\":[]}")
            .Sonra(HttpStatusCode.OK, "{\"data\":[]}");
        var sonuc = await new OpenSubtitlesProvider(ag, "ANAHTAR").SearchAsync(Sorgu(), CancellationToken.None);

        Assert.Equal(SubtitleOutcome.NoResult, sonuc.Outcome);
        Assert.Empty(sonuc.Candidates);
    }

    /// <summary>
    /// 406 gunluk kota, 429 saniyedeki istek siniri. Ikisi ayri kol: biri gece yarisi
    /// yenileniyor, oteki birkac saniyede geciyor.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.NotAcceptable, SubtitleOutcome.QuotaExceeded)]
    [InlineData(HttpStatusCode.TooManyRequests, SubtitleOutcome.RateLimited)]
    public async Task SinirKodlariAyriKollaraDuser(HttpStatusCode kod, SubtitleOutcome beklenen)
    {
        var ag = new SahteAg().Sonra(kod, "{\"message\":\"sinir\"}");
        var sonuc = await new OpenSubtitlesProvider(ag, "ANAHTAR").SearchAsync(Sorgu(), CancellationToken.None);

        Assert.Equal(beklenen, sonuc.Outcome);
    }

    /// <summary>
    /// Tuzak: <c>/download</c> kota dolunca 401 de donebiliyor. Govdede kalan hak ve
    /// yenilenme saati varsa bu anahtarin reddi degildir. Ayrimi yapmayan istemci
    /// kullaniciya "anahtarin yanlis" deyip yeniden giris dongusune giriyor.
    /// </summary>
    [Fact]
    public async Task IndirmedeKotaGovdeliDortYuzBirKotaSayilir()
    {
        const string kotaGovdesi = "{\"requests\":20,\"remaining\":0,\"message\":\"You have downloaded your allowed 20 subtitles for 24h\",\"reset_time\":\"00 hours and 27 minutes\"}";
        var kota = new SahteAg().Sonra(HttpStatusCode.Unauthorized, kotaGovdesi);
        var anahtar = new SahteAg().Sonra(HttpStatusCode.Unauthorized, "{\"message\":\"invalid api key\"}");
        var aday = new SubtitleCandidate(1, "tr", "s", "d.srt", 0, false);

        var kotaSonucu = await new OpenSubtitlesProvider(kota, "ANAHTAR").DownloadAsync(aday, "C:\\film\\a.mkv", CancellationToken.None);
        var anahtarSonucu = await new OpenSubtitlesProvider(anahtar, "ANAHTAR").DownloadAsync(aday, "C:\\film\\a.mkv", CancellationToken.None);

        AltyaziKanit.Yaz("dort-yuz-bir.txt", $"kota {kotaSonucu.Outcome} kalan {kotaSonucu.Remaining}; anahtar {anahtarSonucu.Outcome}");

        Assert.Equal(SubtitleOutcome.QuotaExceeded, kotaSonucu.Outcome);
        Assert.Equal(0, kotaSonucu.Remaining);
        Assert.Equal(SubtitleOutcome.NeedAccount, anahtarSonucu.Outcome);

        AltyaziKanit.Kapat("dort-yuz-bir.txt");
    }

    /// <summary>
    /// Indirme baglantisi imzali ve kendi kendini yetkilendiriyor; anahtari o istege
    /// koymuyoruz. Kimlik basligi yine gidiyor.
    /// </summary>
    [Fact]
    public async Task ImzaliBaglantiyaAnahtarGonderilmez()
    {
        var kok = AltyaziKanit.Temiz("indir-baglanti");
        var film = Path.Combine(kok, "f.mkv");
        File.WriteAllBytes(film, new byte[16]);

        var ag = new SahteAg()
            .Sonra(HttpStatusCode.OK, "{\"link\":\"https://dl.example/imzali\",\"remaining\":9}")
            .SonraBayt(Encoding.UTF8.GetBytes("govde"));
        await new OpenSubtitlesProvider(ag, "ANAHTAR").DownloadAsync(new SubtitleCandidate(3, "tr", "s", "d", 0, false), film, CancellationToken.None);

        AltyaziKanit.Yaz("indir-baglanti.txt", string.Join("\n", ag.Istekler.Zip(ag.Basliklar, (i, b) => i + " => " + b)));

        Assert.Equal(2, ag.Istekler.Count);
        Assert.Contains("Api-Key=ANAHTAR", ag.Basliklar[0], StringComparison.Ordinal);
        Assert.DoesNotContain("Api-Key", ag.Basliklar[1], StringComparison.Ordinal);
        Assert.Equal(ag.Kimlikler[0], ag.Kimlikler[1]);
        Assert.NotEmpty(ag.Kimlikler[1]);

        AltyaziKanit.Kapat("indir-baglanti", "indir-baglanti.txt");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ReddedilenAnahtarBadKeyOlur(HttpStatusCode kod)
    {
        var ag = new SahteAg().Sonra(kod, "{\"message\":\"no\"}");
        var sonuc = await new OpenSubtitlesProvider(ag, "YANLIS").SearchAsync(Sorgu(), CancellationToken.None);

        Assert.Equal(SubtitleOutcome.BadKey, sonuc.Outcome);
    }

    [Fact]
    public async Task AgYokkenNetworkErrorDonerIstisnaSizmaz()
    {
        var arama = await new OpenSubtitlesProvider(new SahteAg().SonraAtar(), "ANAHTAR")
            .SearchAsync(Sorgu(), CancellationToken.None);
        var indirme = await new OpenSubtitlesProvider(new SahteAg().SonraAtar(), "ANAHTAR")
            .DownloadAsync(new SubtitleCandidate(1, "tr", "s", "d", 0, false), "C:\\film\\a.mkv", CancellationToken.None);

        Assert.Equal(SubtitleOutcome.NetworkError, arama.Outcome);
        Assert.Equal(SubtitleOutcome.NetworkError, indirme.Outcome);
    }

    // ---------- oynatıcı ----------

    /// <summary>
    /// Oturum yokken arama isteği hiç gönderilmez: indirme zaten <c>Authorization</c>
    /// olmadan düşecek, boşuna bir istek kotayı ve saniyelik sınırı yer. Kullanıcıya
    /// hesap girişi gerektiği söylenir. Oturum varken aynı yol aramaya kadar gider.
    /// </summary>
    [Fact]
    public void OturumsuzIndirmeAgaCikmadanHesapIster()
    {
        var rapor = AppHost.Run(() =>
        {
            var kok = AltyaziKanit.Temiz("oturumsuz");
            var film = Path.Combine(kok, "film.mp4");
            File.WriteAllBytes(film, new byte[16]);

            var saglayici = new SahteSaglayici { OturumVar = false };
            var view = Ac(new YolMotoru(), out var pencere, film);
            view.SubtitleProviderSource = () => saglayici;

            var is_ = view.DownloadSubtitleAsync();
            DenetimSurucu.Pump(view, () => is_.IsCompleted, 10);
            is_.GetAwaiter().GetResult();
            var oturumsuz = (Bildirim: view.TrackNotice, Sorgu: saglayici.Sorgular.Count);

            saglayici.OturumVar = true;
            var is2 = view.DownloadSubtitleAsync();
            DenetimSurucu.Pump(view, () => is2.IsCompleted, 10);
            is2.GetAwaiter().GetResult();
            var oturumlu = (Bildirim: view.TrackNotice, Sorgu: saglayici.Sorgular.Count);

            pencere.Close();
            return (Oturumsuz: oturumsuz, Oturumlu: oturumlu);
        });

        AltyaziKanit.Yaz("oturumsuz.txt",
            "oturumsuz " + rapor.Oturumsuz.Bildirim + " sorgu " + rapor.Oturumsuz.Sorgu
            + "\noturumlu " + rapor.Oturumlu.Bildirim + " sorgu " + rapor.Oturumlu.Sorgu);

        Assert.Equal("player.subtitle.download.needaccount", rapor.Oturumsuz.Bildirim);
        Assert.Equal(0, rapor.Oturumsuz.Sorgu);
        Assert.Equal(1, rapor.Oturumlu.Sorgu);

        AltyaziKanit.Kapat("oturumsuz", "oturumsuz.txt");
    }

    private static PlayerView Ac(YolMotoru motor, out Window pencere, string yol)
    {
        var view = new PlayerView { EngineFactory = () => motor };
        pencere = new Window { Width = 640, Height = 360, Content = view };
        pencere.Show();
        var acilis = view.OpenAsync(yol);
        DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
        acilis.GetAwaiter().GetResult();
        DenetimSurucu.Wait(view, 0.2);
        return view;
    }

    /// <summary>İndirilen dosya motora veriliyor mu; seçim ilk adaya düşüyor mu.</summary>
    [Fact]
    public void IndirilenAltyaziMotoraVerilir()
    {
        var rapor = AppHost.Run(() =>
        {
            var kok = AltyaziKanit.Temiz("oynatici-indir");
            var film = Path.Combine(kok, "film.mp4");
            File.WriteAllBytes(film, new byte[16]);
            var altyazi = Path.Combine(kok, "film.tr.srt");
            File.WriteAllText(altyazi, "1\r\n00:00:00,000 --> 00:00:01,000\r\nx\r\n", new UTF8Encoding(false));

            var saglayici = new SahteSaglayici
            {
                Arama = new SubtitleSearchResult(SubtitleOutcome.Ok, new[]
                {
                    new SubtitleCandidate(91, "tr", "surum", "film.tr.srt", 7, true),
                    new SubtitleCandidate(92, "en", "surum", "film.en.srt", 9, false)
                }),
                Indirme = new SubtitleDownloadResult(SubtitleOutcome.Ok, altyazi, 3)
            };

            var motor = new YolMotoru();
            var view = Ac(motor, out var pencere, film);
            view.SubtitleProviderSource = () => saglayici;
            var is_ = view.DownloadSubtitleAsync();
            DenetimSurucu.Pump(view, () => is_.IsCompleted, 10);
            is_.GetAwaiter().GetResult();

            var sonuc = (
                Eklenen: motor.Eklenen.Select(Path.GetFileName).ToList(),
                Secilen: saglayici.Indirilenler.ToList(),
                Hash: saglayici.Sorgular[0].MovieHash,
                Diller: saglayici.Sorgular[0].Languages.ToList(),
                Iz: view.Trace.Where(t => t.StartsWith("subdl", StringComparison.Ordinal)).ToList(),
                Bildirim: view.TrackNotice);
            pencere.Close();
            return sonuc;
        });

        AltyaziKanit.Yaz("oynatici-indir.txt",
            $"eklenen {string.Join(" | ", rapor.Eklenen)}\nsecilen {string.Join(",", rapor.Secilen)}\n" +
            $"hash {rapor.Hash ?? "yok"}\ndiller {string.Join(",", rapor.Diller)}\niz {string.Join(" | ", rapor.Iz)}\nbildirim {rapor.Bildirim}");

        Assert.Contains("film.tr.srt", rapor.Eklenen);
        Assert.Equal(new long[] { 91 }, rapor.Secilen);
        Assert.Null(rapor.Hash);
        Assert.Equal("player.subtitle.download.done", rapor.Bildirim);

        AltyaziKanit.Kapat("oynatici-indir", "oynatici-indir.txt");
    }

    /// <summary>
    /// Beş hata kolunun her biri kendi bildirim anahtarına düşüyor; ikisi aynı metne
    /// çıkmıyor ve hepsi 42 dilin kataloğunda var.
    /// </summary>
    [Fact]
    public void HerHataKoluAyriBildirimAnahtarinaDuser()
    {
        var kollar = new[]
        {
            SubtitleOutcome.NoKey, SubtitleOutcome.BadKey, SubtitleOutcome.NeedAccount,
            SubtitleOutcome.NoResult, SubtitleOutcome.QuotaExceeded, SubtitleOutcome.RateLimited,
            SubtitleOutcome.NetworkError, SubtitleOutcome.WriteError
        };

        var anahtarlar = kollar.Select(PlayerView.NoticeKeyFor).ToList();
        AltyaziKanit.Yaz("hata-kollari.txt", string.Join("\n", kollar.Zip(anahtarlar, (k, a) => $"{k} -> {a}")));

        Assert.Equal(anahtarlar.Count, anahtarlar.Distinct(StringComparer.Ordinal).Count());
        foreach (var dil in Strings.Languages)
            foreach (var anahtar in anahtarlar)
                Assert.False(string.IsNullOrWhiteSpace(Strings.GetIn(dil, anahtar)), $"{dil}/{anahtar} bos");

        AltyaziKanit.Kapat("hata-kollari.txt");
    }

    /// <summary>
    /// Ölçülen kol sağlayıcıdan gelen her sonucun oynatıcıda ayrı bir satıra döndüğü.
    /// Hepsi tek bir "olmadı"ya düşseydi ölçü kırmızı olurdu.
    /// </summary>
    [Fact]
    public void SaglayiciKollariOynaticidaAyriBildirimUretir()
    {
        var rapor = AppHost.Run(() =>
        {
            var kok = AltyaziKanit.Temiz("oynatici-kollar");
            var film = Path.Combine(kok, "film.mp4");
            File.WriteAllBytes(film, new byte[16]);

            var saglayici = new SahteSaglayici();
            var motor = new YolMotoru();
            var view = Ac(motor, out var pencere, film);
            view.SubtitleProviderSource = () => saglayici;

            var okunan = new List<(SubtitleOutcome Kol, string? Anahtar)>();
            foreach (var kol in new[] { SubtitleOutcome.NoResult, SubtitleOutcome.QuotaExceeded, SubtitleOutcome.BadKey, SubtitleOutcome.NetworkError })
            {
                saglayici.Arama = SubtitleSearchResult.Failed(kol);
                var is_ = view.DownloadSubtitleAsync();
                DenetimSurucu.Pump(view, () => is_.IsCompleted, 10);
                is_.GetAwaiter().GetResult();
                okunan.Add((kol, view.TrackNotice));
            }

            saglayici.Arama = new SubtitleSearchResult(SubtitleOutcome.Ok, new[] { new SubtitleCandidate(1, "tr", "s", "d.srt", 1, true) });
            saglayici.Indirme = SubtitleDownloadResult.Failed(SubtitleOutcome.WriteError);
            var yazma = view.DownloadSubtitleAsync();
            DenetimSurucu.Pump(view, () => yazma.IsCompleted, 10);
            yazma.GetAwaiter().GetResult();
            okunan.Add((SubtitleOutcome.WriteError, view.TrackNotice));

            pencere.Close();
            return okunan;
        });

        AltyaziKanit.Yaz("oynatici-kollar.txt", string.Join("\n", rapor.Select(r => $"{r.Kol} -> {r.Anahtar}")));

        Assert.Equal("player.subtitle.download.noresult", rapor[0].Anahtar);
        Assert.Equal("player.subtitle.download.quota", rapor[1].Anahtar);
        Assert.Equal("player.subtitle.download.badkey", rapor[2].Anahtar);
        Assert.Equal("player.subtitle.download.offline", rapor[3].Anahtar);
        Assert.Equal("player.subtitle.download.writefail", rapor[4].Anahtar);
        Assert.Equal(5, rapor.Select(r => r.Anahtar).Distinct(StringComparer.Ordinal).Count());

        AltyaziKanit.Kapat("oynatici-kollar", "oynatici-kollar.txt");
    }

    /// <summary>
    /// İndirme satırı menüde. Anahtar girilmemişken satır "anahtar nasıl alınır" yüzünü
    /// gösterir, girilmişken indirme yüzünü; iki yüz aynı anda görünmez. Menünün kendisinin
    /// çizildiği, dokunulmamış iki altyazı satırıyla pimli — menü boş dönseydi yalnız
    /// yokluğa bakan bir ölçü de geçerdi.
    /// </summary>
    [Fact]
    public void IndirmeSatiriMenudeGorunur()
    {
        var rapor = AppHost.Run(() =>
        {
            var kok = AltyaziKanit.Temiz("oynatici-menu");
            var film = Path.Combine(kok, "film.mp4");
            File.WriteAllBytes(film, new byte[16]);

            var saglayici = new SahteSaglayici { Kurulu = false };
            var motor = new YolMotoru();
            var view = Ac(motor, out var pencere, film);
            view.SubtitleProviderSource = () => saglayici;

            var kapali = view.BuildSubtitleItems().OfType<MenuItem>().Select(m => m.Header as string).ToList();
            saglayici.Kurulu = true;
            var acik = view.BuildSubtitleItems().OfType<MenuItem>().Select(m => m.Header as string).ToList();
            pencere.Close();
            return (Kapali: kapali, Acik: acik);
        });

        AltyaziKanit.Yaz("oynatici-menu.txt", $"kapali {string.Join(" | ", rapor.Kapali)}\nacik {string.Join(" | ", rapor.Acik)}");

        Assert.Contains(Strings.Get("player.subtitle.download.getkey"), rapor.Kapali);
        Assert.DoesNotContain(Strings.Get("player.subtitle.download"), rapor.Kapali);

        Assert.Contains(Strings.Get("player.subtitle.download"), rapor.Acik);
        Assert.DoesNotContain(Strings.Get("player.subtitle.download.getkey"), rapor.Acik);

        foreach (var liste in new[] { rapor.Kapali, rapor.Acik })
        {
            Assert.Contains(Strings.Get("player.subtitle.off"), liste);
            Assert.Contains(Strings.Get("player.subtitle.load"), liste);
        }

        AltyaziKanit.Kapat("oynatici-menu", "oynatici-menu.txt");
    }

    /// <summary>Arayüz dili önce, İngilizce sonra; İngilizce arayüzde liste tek elemanlı.</summary>
    [Theory]
    [InlineData("tr", "tr,en")]
    [InlineData("en", "en")]
    [InlineData("zh-Hans", "zh,en")]
    [InlineData("", "en")]
    public void AramaDiliArayuzDilindenTurer(string arayuz, string beklenen)
        => Assert.Equal(beklenen, string.Join(",", PlayerView.SubtitleLanguages(arayuz)));

    // ---------- dil dosyası enjeksiyonu ----------

    /// <summary>
    /// İndirme bildiriminin metni koddan değil dil dosyasından geliyor: İngilizce
    /// kataloğun kopyasında biçim değiştirilir, oynatıcının durum satırında o biçim okunur.
    /// Metni koddan yazan bir uygulama bu enjeksiyondan etkilenmez, dolayısıyla kırmızı olur.
    /// </summary>
    [Fact]
    public void IndirmeBildirimiDilDosyasindanGelir()
    {
        var kopya = Kopya("p28-dil", new Dictionary<string, string>
        {
            ["\"player.subtitle.download.done\": \"Subtitle downloaded: {0}\""]
                = "\"player.subtitle.download.done\": \"PIM94 {0} PIM94\""
        });

        string durum;
        var onceki = AppHost.Run(() => Strings.Language);
        AppHost.Run(() =>
        {
            Strings.UseRoot(kopya);
            Strings.Use("en");
            return 0;
        });

        try
        {
            durum = AppHost.Run(() =>
            {
                var kok = AltyaziKanit.Temiz("dil-pim");
                var film = Path.Combine(kok, "film.mp4");
                File.WriteAllBytes(film, new byte[16]);
                var altyazi = Path.Combine(kok, "film.tr.srt");
                File.WriteAllText(altyazi, "1\r\n00:00:00,000 --> 00:00:01,000\r\nx\r\n", new UTF8Encoding(false));

                var saglayici = new SahteSaglayici
                {
                    Arama = new SubtitleSearchResult(SubtitleOutcome.Ok, new[] { new SubtitleCandidate(1, "tr", "s", "film.tr.srt", 1, true) }),
                    Indirme = new SubtitleDownloadResult(SubtitleOutcome.Ok, altyazi, 2)
                };

                var view = Ac(new YolMotoru(), out var pencere, film);
                view.SubtitleProviderSource = () => saglayici;
                var is_ = view.DownloadSubtitleAsync();
                DenetimSurucu.Pump(view, () => is_.IsCompleted, 10);
                is_.GetAwaiter().GetResult();
                var okunan = view.FindControl<TextBlock>("TxtControls")?.Text ?? "";
                pencere.Close();
                return okunan;
            });
        }
        finally
        {
            AppHost.Run(() =>
            {
                Strings.UseRoot(null);
                Strings.Use(onceki);
                return 0;
            });
        }

        AltyaziKanit.Yaz("dil-pim.txt", durum);

        Assert.Contains("PIM94", durum, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("film.tr.srt", durum, StringComparison.OrdinalIgnoreCase);

        AltyaziKanit.Kapat("p28-dil", "dil-pim", "dil-pim.txt");
    }

    /// <summary>
    /// Dil dosyalarının tamamını <c>.calisma</c> altına kopyalar ve İngilizce
    /// <c>tracks.json</c>'da verilen şablonları değiştirir.
    /// </summary>
    private static string Kopya(string ad, Dictionary<string, string> degisim)
    {
        var kaynak = Path.Combine(AppContext.BaseDirectory, "Locales");
        var kopya = Path.Combine(AltyaziKanit.Folder, ad, "Locales");
        foreach (var dosya in Directory.GetFiles(kaynak, "*", SearchOption.AllDirectories))
        {
            var hedef = Path.Combine(kopya, Path.GetRelativePath(kaynak, dosya));
            Directory.CreateDirectory(Path.GetDirectoryName(hedef)!);
            File.Copy(dosya, hedef, overwrite: true);
        }

        var enIzler = Path.Combine(kopya, "en", "tracks.json");
        var metin = File.ReadAllText(enIzler);
        foreach (var (arama, yazi) in degisim)
        {
            Assert.Contains(arama, metin);
            metin = metin.Replace(arama, yazi);
        }

        File.WriteAllText(enIzler, metin, new UTF8Encoding(false));
        return kopya;
    }

    // ---------- ayar ----------

    /// <summary>
    /// Anahtar kullanıcının gerçek ayar dosyasına değil, ölçüm için verilen yola yazılır.
    /// Gidiş-dönüş: kaydedilen anahtar aynen geri okunuyor ve diğer anahtarlar bozulmuyor.
    /// </summary>
    [Fact]
    public void AnahtarAyarDosyasinaYazilipGeriOkunur()
    {
        var kok = AltyaziKanit.Temiz("ayar");
        var dosya = Path.Combine(kok, "settings.json");
        var onceki = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
        Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", dosya);
        try
        {
            new AppSettings { OpenSubtitlesApiKey = "kullanici-anahtari-123", Theme = "Gece" }.Save();
            var geri = AppSettings.Load();
            var ham = File.ReadAllText(dosya);
            AltyaziKanit.Yaz("ayar.txt", ham);

            Assert.True(File.Exists(dosya), "anahtar VIDSHRINK_SETTINGS_PATH'in gosterdigi dosyaya yazilmadi");
            Assert.Equal("kullanici-anahtari-123", geri.OpenSubtitlesApiKey);
            Assert.Equal("Gece", geri.Theme);
            Assert.Contains("openSubtitlesApiKey", ham, StringComparison.Ordinal);

            var bos = AppSettings.Load(Path.Combine(kok, "yok.json"));
            Assert.Equal("", bos.OpenSubtitlesApiKey);

            AltyaziKanit.Kapat("ayar", "ayar.txt");
        }
        finally
        {
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", onceki);
        }
    }

    /// <summary>
    /// Menü satırının "anahtar var mı" sorusu her çizimde diske inmiyor. Ayar dosyası
    /// değişmedikçe okuma bir kez olur; dosya yeniden yazılınca damga düşer ve cevap
    /// hemen değişir — yani önbellek bayat kalmıyor. Ölçülen sayı gerçek okuma sayacı.
    /// </summary>
    [Fact]
    public void MenuCizimiAyarDosyasiniHerSeferOkumaz()
    {
        var kok = AltyaziKanit.Temiz("anahtar-onbellek");
        var dosya = Path.Combine(kok, "settings.json");
        var onceki = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
        Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", dosya);
        try
        {
            var view = AppHost.Run(() => new PlayerView());

            new AppSettings { OpenSubtitlesApiKey = "" }.Save();
            var bastaki = PlayerView.AnahtarOkumaSayisi;
            var bosCevaplar = new List<bool>();
            for (var i = 0; i < 10; i++) bosCevaplar.Add(view.SubtitleDownloadReady);
            var bosOkuma = PlayerView.AnahtarOkumaSayisi - bastaki;

            new AppSettings { OpenSubtitlesApiKey = "kullanici-anahtari-123" }.Save();
            var doluCevaplar = new List<bool>();
            for (var i = 0; i < 10; i++) doluCevaplar.Add(view.SubtitleDownloadReady);
            var doluOkuma = PlayerView.AnahtarOkumaSayisi - bastaki - bosOkuma;

            AltyaziKanit.Yaz("anahtar-onbellek.txt",
                $"bos: cevap={string.Join(",", bosCevaplar)} okuma={bosOkuma}\n"
                + $"dolu: cevap={string.Join(",", doluCevaplar)} okuma={doluOkuma}\n");

            Assert.All(bosCevaplar, cevap => Assert.False(cevap));
            Assert.Equal(1, bosOkuma);
            Assert.All(doluCevaplar, cevap => Assert.True(cevap));
            Assert.Equal(1, doluOkuma);

            AltyaziKanit.Kapat("anahtar-onbellek", "anahtar-onbellek.txt");
        }
        finally
        {
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", onceki);
        }
    }

    /// <summary>
    /// Depoda anahtar yok. Tarama <b>butun depo</b>: kaynak kadar belge, test, ayar ve
    /// arayuz dosyasi da. Yalniz <c>src/**/*.cs</c> tarandigi surumde gercek bir ucuncu
    /// taraf anahtari <c>docs/arastirma/</c> altina dusup 65/65 yesil kalmisti.
    /// </summary>
    [Fact]
    public void DepodaGomuluAnahtarYok()
    {
        string[] uzantilar = [".cs", ".md", ".json", ".axaml", ".xaml", ".yml", ".yaml", ".ps1", ".sh", ".txt"];
        var izlenen = GitinIzledigiDosyalar();
        Assert.True(izlenen.Count > 500, "git ls-files depoyu okumadi, tarama kor kalirdi: " + izlenen.Count);

        var suclular = new List<string>();
        foreach (var bagil in izlenen)
        {
            if (!uzantilar.Contains(Path.GetExtension(bagil), StringComparer.OrdinalIgnoreCase)) continue;
            var dosya = Path.Combine(GirdiKanit.Root, bagil);
            if (!File.Exists(dosya)) continue;
            foreach (var satir in File.ReadAllLines(dosya))
            {
                if (AnahtarKokuyor(satir)) suclular.Add(bagil + ": " + satir.Trim());
            }
        }

        // Pozitif kontrol: tarayici gercek bicimli bir anahtari yakaliyor mu. Yakalamasaydi
        // bos liste "temiz" degil "kor" demek olurdu. Iki bicim de sinanir: tirnakli kod
        // sabiti ve belgelerdeki tirnaksiz tel kaydi satiri. Ikisi de kaynakta iki parcaya
        // bolunmus yazilir, yoksa tarama kendi pozitif kontrolunu suclu sayardi.
        const string gomulu = "        request.Headers.TryAddWithoutValidation(\"Api-Key\", \"9fK2mQ7xTz4LpW8dRb1"
            + "VnH5cJ6yEaG0s\");";
        const string telKaydi = "Api-Key: mij33pjc3kOlup1"
            + "qOKxnWWxvle2kFbMH";
        const string alan = "        root[\"openSubtitlesApiKey\"] = OpenSubtitlesApiKey;";

        AltyaziKanit.Yaz("anahtar-taramasi.txt",
            (suclular.Count == 0 ? "temiz" : string.Join("\n", suclular))
            + "\npozitif kontrol: " + AnahtarKokuyor(gomulu)
            + "\ntel kaydi: " + AnahtarKokuyor(telKaydi)
            + "\nayar alani: " + AnahtarKokuyor(alan));

        Assert.True(AnahtarKokuyor(gomulu), "tarayici gomulu anahtari kaciriyor");
        Assert.True(AnahtarKokuyor(telKaydi), "tarayici tirnaksiz tel kaydi anahtarini kaciriyor");
        Assert.False(AnahtarKokuyor(alan), "tarayici ayar alan adini anahtar saniyor");
        Assert.Empty(suclular);

        AltyaziKanit.Kapat("anahtar-taramasi.txt");
    }

    /// <summary>
    /// Sir satiri uc bicimden biri: (1) JWT — <c>eyJ</c> ile baslayan uc noktali yapi, tetik
    /// sozcuk gerektirmez cunku bicimin kendisi kimlik; (2) sir sozcugu gecen bir satirda en
    /// az 24 karakterlik, hem harf hem rakam tasiyan bir dizi; (3) parola atamasi. Tirnak sart
    /// degil — belgelerdeki tel kaydi (<c>Api-Key: &lt;deger&gt;</c>) tirnaksizdir ve tirnak
    /// arayan surum onu kaciriyordu. Alan adlari ("openSubtitlesApiKey") rakamsizdir, elenir.
    /// Tek anahtar adina bagli surum yalnizca <c>Api-Key</c> ariyordu: belirtec, parola ve
    /// bearer basligi ayni depoya tarama hic bakmadan girebilirdi.
    /// </summary>
    /// <summary>
    /// Taramanin kapsami: <b>git'in izledigi</b> dosyalar. Sizinti ancak commit'lenmisse
    /// sizintidir; gitignore'daki <c>.calisma</c>, <c>trash</c>, <c>bin</c> ve oturum
    /// gunlukleri depoya girmez. Klasor gezen surum bunlari da tarayip yabanci belirtec
    /// bildiriyordu. Liste bosalirsa olcu "temiz" demeden once bunu yakalar.
    /// </summary>
    private static List<string> GitinIzledigiDosyalar()
    {
        var baslangic = new System.Diagnostics.ProcessStartInfo("git", "ls-files")
        {
            WorkingDirectory = GirdiKanit.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var surec = System.Diagnostics.Process.Start(baslangic)!;
        var cikti = surec.StandardOutput.ReadToEnd();
        surec.StandardError.ReadToEnd();
        surec.WaitForExit();
        Assert.Equal(0, surec.ExitCode);
        return cikti.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().Replace('/', Path.DirectorySeparatorChar))
            .ToList();
    }

    private static bool AnahtarKokuyor(string satir)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(
                satir, "eyJ[A-Za-z0-9_-]{10,}\\.[A-Za-z0-9_-]{10,}\\.")) return true;

        string[] tetikler = ["Api-Key", "apiKey", "api_key", "secret", "token", "bearer", "password", "parola"];
        if (!tetikler.Any(t => satir.Contains(t, StringComparison.OrdinalIgnoreCase))) return false;

        foreach (System.Text.RegularExpressions.Match sabit in
                 System.Text.RegularExpressions.Regex.Matches(satir, "[A-Za-z0-9]{24,}"))
        {
            var deger = sabit.Value;
            if (deger.Any(char.IsDigit) && deger.Any(char.IsLetter)) return true;
        }

        return false;
    }

    /// <summary>
    /// Arama sürerken ekranda "aranıyor" satırı duruyor. Bu anahtarın tek okuyucusu bu ölçü;
    /// olmasa 42 dile yazılan metin hiçbir yerde görünmeyen ölü anahtar olurdu.
    /// </summary>
    [Fact]
    public void AramaSurerkenAraniyorSatiriGorunur()
    {
        var okunan = AppHost.Run(() =>
        {
            var kok = AltyaziKanit.Temiz("aramada");
            var film = Path.Combine(kok, "film.mp4");
            File.WriteAllBytes(film, new byte[16]);

            var kapi = new TaskCompletionSource();
            var saglayici = new SahteSaglayici { AramaKapisi = kapi };
            var view = Ac(new YolMotoru(), out var pencere, film);
            view.SubtitleProviderSource = () => saglayici;

            var is_ = view.DownloadSubtitleAsync();
            DenetimSurucu.Pump(view, () => saglayici.Sorgular.Count > 0, 10);
            var sirasinda = view.TrackNotice;

            kapi.SetResult();
            DenetimSurucu.Pump(view, () => is_.IsCompleted, 10);
            is_.GetAwaiter().GetResult();
            var sonra = view.TrackNotice;
            pencere.Close();
            return (Sirasinda: sirasinda, Sonra: sonra);
        });

        AltyaziKanit.Yaz("aramada.txt", $"sirasinda {okunan.Sirasinda}; sonra {okunan.Sonra}");

        Assert.Equal("player.subtitle.download.working", okunan.Sirasinda);
        Assert.NotEqual(okunan.Sirasinda, okunan.Sonra);

        AltyaziKanit.Kapat("aramada", "aramada.txt");
    }

    /// <summary>
    /// P28 dil dosyalarına dil başına yirmi sekiz anahtar ekledi: dalın tabanı
    /// <c>d0aea4d2</c> dil başına 900, dalın tepesi 928, düşen yok. On beşi ilk turda
    /// (menü, ilerleme, hata kolları, ayar alanı), on üçü Yol A ile (oturum alanları ve
    /// oturuma bağlı iki yeni hata kolu) girdi. Bu ölçü <b>hepsini</b> tutuyor: Yol A
    /// indirme satırını menüye geri koyduğu için artık üretimde okunmayan P28 anahtarı yok.
    /// Listenin son iki satırı P28'in eklediği değil, P28'in dokunduğu eski anahtar
    /// (<c>player.subtitle.loadfailed</c>, <c>settings.player-shortcuts.hint</c>); toplam otuz satır.
    /// Her satır üretim kaynağında (kod ya da axaml) geçiyor ve 42 dilin hepsinde boş olmayan,
    /// anahtarın kendisi olmayan bir karşılığı var. Okunmayan anahtar dile girer, ekrana hiç çıkmaz.
    /// </summary>
    [Theory]
    [InlineData("player.subtitle.download")]
    [InlineData("player.subtitle.download.getkey")]
    [InlineData("player.subtitle.download.working")]
    [InlineData("player.subtitle.download.done")]
    [InlineData("player.subtitle.download.nokey")]
    [InlineData("player.subtitle.download.badkey")]
    [InlineData("player.subtitle.download.badlogin")]
    [InlineData("player.subtitle.download.needaccount")]
    [InlineData("player.subtitle.download.noresult")]
    [InlineData("player.subtitle.download.quota")]
    [InlineData("player.subtitle.download.toofast")]
    [InlineData("player.subtitle.download.toofast.wait")]
    [InlineData("player.subtitle.download.expired")]
    [InlineData("player.subtitle.download.offline")]
    [InlineData("player.subtitle.download.writefail")]
    [InlineData("settings-tab.opensubtitles.label")]
    [InlineData("settings-tab.opensubtitles.get")]
    [InlineData("settings-tab.opensubtitles.hint")]
    [InlineData("settings-tab.opensubtitles.account")]
    [InlineData("settings-tab.opensubtitles.user")]
    [InlineData("settings-tab.opensubtitles.password")]
    [InlineData("settings-tab.opensubtitles.signin")]
    [InlineData("settings-tab.opensubtitles.signout")]
    [InlineData("settings-tab.opensubtitles.signingin")]
    [InlineData("settings-tab.opensubtitles.signedin")]
    [InlineData("settings-tab.opensubtitles.signedout")]
    [InlineData("settings-tab.opensubtitles.signinfailed")]
    [InlineData("settings-tab.opensubtitles.accounthint")]
    [InlineData("player.subtitle.loadfailed")]
    [InlineData("settings.player-shortcuts.hint")]
    public void EklenenMetinKaynaktaOkunurVeKirkIkiDildeVar(string anahtar)
    {
        Assert.True(KaynaktaGeciyor(anahtar), anahtar + " uretim kaynaginda hic okunmuyor");

        var klasorler = Directory.GetDirectories(Path.Combine(GirdiKanit.Root, "src", "VidShrink.App", "Locales"))
            .Select(Path.GetFileName)
            .OrderBy(ad => ad, StringComparer.Ordinal)
            .ToArray();

        var eksik = klasorler
            .Where(dil => string.IsNullOrWhiteSpace(Strings.GetIn(dil!, anahtar))
                          || string.Equals(Strings.GetIn(dil!, anahtar), anahtar, StringComparison.Ordinal))
            .ToArray();

        AltyaziKanit.Yaz("diller.txt",
            "Strings.Languages (" + Strings.Languages.Count + "): " + string.Join(",", Strings.Languages)
            + Environment.NewLine
            + "Locales klasorleri (" + klasorler.Length + "): " + string.Join(",", klasorler));

        // Sayi elle yazilmaz: katalogdaki klasor sayisindan turetilir, yoksa dil eklenince
        // olcu sabit kalir ve eklenen dili gormez. Olcunun alani katalogtur; Strings.Languages
        // gomulu kaynak adindan turettigi icin fazladan "zh_Hans" bildiriyor (ayri is).
        Assert.Equal(42, klasorler.Length);
        Assert.Empty(eksik);
        Assert.All(klasorler, dil => Assert.Contains(dil, Strings.Languages, StringComparer.OrdinalIgnoreCase));

        AltyaziKanit.Kapat("diller.txt");
    }

    /// <summary>
    /// Yukarıdaki taramanın kör olmadığının kontrolü: uydurma bir anahtar kaynakta
    /// bulunmuyor ve dil dosyalarında karşılığı yok.
    /// </summary>
    [Fact]
    public void OkunmayanAnahtarTaramasiKorDegil()
    {
        const string uydurma = "player.subtitle.download.boyleBirSeyYok";

        AltyaziKanit.Yaz("olu-anahtar.txt",
            "uydurma kaynakta: " + KaynaktaGeciyor(uydurma)
            + "\ngercek kaynakta: " + KaynaktaGeciyor("player.subtitle.download.toofast"));

        Assert.False(KaynaktaGeciyor(uydurma));
        Assert.True(KaynaktaGeciyor("player.subtitle.download.toofast"));

        AltyaziKanit.Kapat("olu-anahtar.txt");
    }

    private static bool KaynaktaGeciyor(string anahtar)
    {
        var kok = Path.Combine(GirdiKanit.Root, "src", "VidShrink.App");
        var aranan = "\"" + anahtar + "\"";
        var isaret = "{loc:Text " + anahtar + "}";

        foreach (var dosya in Directory.GetFiles(kok, "*.cs", SearchOption.AllDirectories)
                     .Concat(Directory.GetFiles(kok, "*.axaml", SearchOption.AllDirectories)))
        {
            if (dosya.Contains(Path.DirectorySeparatorChar + "Locales" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                continue;

            var metin = File.ReadAllText(dosya);
            if (metin.Contains(aranan, StringComparison.Ordinal) || metin.Contains(isaret, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}

