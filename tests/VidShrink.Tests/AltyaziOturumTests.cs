using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using VidShrink.App.Subtitles;
using VidShrink.Core.Subtitles;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// P28 Yol A — OpenSubtitles kullanıcı oturumu: <c>/login</c>, belirtecin saklanması,
/// <c>Authorization</c> başlığının ne zaman gittiği ve oturuma bağlı hata kolları.
/// </summary>
/// <remarks>
/// Hiçbir ölçü ağa çıkmaz; hepsi <see cref="SahteAg"/> üstünden koşar. Kullanıcının gerçek
/// ayar dosyasına da dokunulmaz: <see cref="SessionStore"/> ayar yolunu parametre olarak alır
/// ve ölçüler onu <c>.calisma</c> altına verir.
/// </remarks>
public class AltyaziOturumTests
{
    private static string Jwt(DateTimeOffset? expires)
    {
        static string Parca(string json)
            => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var govde = expires is { } an
            ? "{\"sub\":\"pim\",\"exp\":" + an.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) + "}"
            : "{\"sub\":\"pim\"}";
        return Parca("{\"typ\":\"JWT\",\"alg\":\"HS256\"}") + "." + Parca(govde) + ".imzayerine";
    }

    private static string GirisGovdesi(string belirtec, string konak)
        => "{\"user\":{\"allowed_downloads\":20,\"level\":\"Sub leecher\"},\"token\":\"" + belirtec
           + "\",\"base_url\":\"" + konak + "\",\"status\":200}";

    private static SubtitleCandidate Aday => new(77, "tr", "s", "d.srt", 0, false);

    // ---------- /login ----------

    /// <summary>
    /// <c>/login</c> 200 dönünce belirteç saklanır ve ömrü JWT'nin <c>exp</c> alanından gelir.
    /// Ömrü koddan sabit veren bir uygulama burada kırmızı olur: beklenen an testin verdiği
    /// <c>exp</c>ten türüyor, sabitten değil.
    /// </summary>
    [Fact]
    public async Task BelirtecinOmruJwtExpindenGelir()
    {
        var simdi = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var biter = simdi.AddHours(7).AddMinutes(13);
        var kutu = new MemorySessionStore();
        var ag = new SahteAg().Sonra(HttpStatusCode.OK, GirisGovdesi(Jwt(biter), SubtitleSession.DefaultHost));

        var sonuc = await new OpenSubtitlesProvider(ag, "ANAHTAR", "https://ornek/api/v1", kutu, () => simdi)
            .LoginAsync("kisi", "gizli", CancellationToken.None);

        AltyaziKanit.Yaz("giris-omur.txt",
            sonuc.Outcome + " " + (sonuc.Session?.Expires.ToUnixTimeSeconds() ?? -1)
            + " beklenen " + biter.ToUnixTimeSeconds());

        Assert.Equal(SubtitleOutcome.Ok, sonuc.Outcome);
        Assert.Equal(biter.ToUnixTimeSeconds(), sonuc.Session!.Expires.ToUnixTimeSeconds());
        Assert.Equal(sonuc.Session.Token, kutu.Read()!.Token);
        Assert.Contains("/login", ag.Istekler[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>exp</c> taşımayan belirteç için ömür girişten on iki saat sonrasıdır. Saat testten
    /// geliyor; beklenen an o saatten türer.
    /// </summary>
    [Fact]
    public async Task ExpsizBelirtecOnIkiSaatYasar()
    {
        var simdi = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var ag = new SahteAg().Sonra(HttpStatusCode.OK, GirisGovdesi(Jwt(null), SubtitleSession.DefaultHost));

        var sonuc = await new OpenSubtitlesProvider(ag, "ANAHTAR", "https://ornek/api/v1", new MemorySessionStore(), () => simdi)
            .LoginAsync("kisi", "gizli", CancellationToken.None);

        Assert.Equal(SubtitleOutcome.Ok, sonuc.Outcome);
        Assert.Equal(simdi.AddHours(12).ToUnixTimeSeconds(), sonuc.Session!.Expires.ToUnixTimeSeconds());
    }

    /// <summary>
    /// Parola isteğin gövdesinde gider ama <b>hiçbir dönüşte</b> geri sızmaz. Sağlayıcı hata
    /// gövdesinde gönderileni yankılasa bile kullanıcıya dönen açıklamada ne parola ne de
    /// belirteç kalır. Maskeyi kaldıran bir uygulama burada kırmızı olur.
    /// </summary>
    [Fact]
    public async Task ParolaVeBelirtecHataIletisineSizmaz()
    {
        const string parola = "Ku$5-gizli-parola";
        var yankilayanBelirtec = Jwt(DateTimeOffset.UtcNow.AddHours(1));
        var govde = "{\"status\":401,\"message\":\"Error login/password: username=kisi password=" + parola
                    + "\",\"token\":\"" + yankilayanBelirtec + "\"}";

        var ag = new SahteAg().Sonra(HttpStatusCode.Unauthorized, govde);
        var sonuc = await new OpenSubtitlesProvider(ag, "ANAHTAR", "https://ornek/api/v1")
            .LoginAsync("kisi", parola, CancellationToken.None);

        AltyaziKanit.Yaz("giris-maske.txt", "kol " + sonuc.Outcome + "\naciklama " + sonuc.Detail);

        Assert.Equal(SubtitleOutcome.BadLogin, sonuc.Outcome);
        Assert.DoesNotContain(parola, sonuc.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(yankilayanBelirtec, sonuc.Detail, StringComparison.Ordinal);
        Assert.Contains(Secrets.Hidden, sonuc.Detail, StringComparison.Ordinal);

        // Maske her seyi silmis olsaydi yukaridaki iki olcu de gecerdi; iletinin durdugunu goster.
        Assert.Contains("Error login/password", sonuc.Detail, StringComparison.Ordinal);
    }

    /// <summary>Parolanın kendisi isteğin gövdesinde gitmek zorunda; giden gövde okunur.</summary>
    [Fact]
    public async Task ParolaYalnizGirisGovdesindeGider()
    {
        const string parola = "acik-parola-42";
        var ag = new SahteAg()
            .Sonra(HttpStatusCode.OK, GirisGovdesi(Jwt(DateTimeOffset.UtcNow.AddHours(3)), SubtitleSession.DefaultHost))
            .Sonra(HttpStatusCode.OK, "{\"link\":\"https://dl.ornek/a\",\"remaining\":5}")
            .SonraBayt(Encoding.UTF8.GetBytes("1\n"));

        var kok = AltyaziKanit.Temiz("giris-govde");
        var film = Path.Combine(kok, "f.mkv");
        File.WriteAllBytes(film, new byte[16]);

        var saglayici = new OpenSubtitlesProvider(ag, "ANAHTAR", "https://ornek/api/v1", new MemorySessionStore());
        await saglayici.LoginAsync("kisi", parola, CancellationToken.None);
        await saglayici.DownloadAsync(Aday, film, CancellationToken.None);

        Assert.Contains(parola, ag.Govdeler[0], StringComparison.Ordinal);
        Assert.All(ag.Govdeler.Skip(1), govde => Assert.DoesNotContain(parola, govde, StringComparison.Ordinal));
        Assert.All(ag.Basliklar, baslik => Assert.DoesNotContain(parola, baslik, StringComparison.Ordinal));
    }

    /// <summary>Kullanıcı adı ya da parola boşken ağa hiç çıkılmaz.</summary>
    [Theory]
    [InlineData("", "gizli")]
    [InlineData("kisi", "")]
    public async Task BosKimlikAgaCikmaz(string kullanici, string parola)
    {
        var ag = new SahteAg();
        var sonuc = await new OpenSubtitlesProvider(ag, "ANAHTAR", "https://ornek/api/v1")
            .LoginAsync(kullanici, parola, CancellationToken.None);

        Assert.Equal(SubtitleOutcome.BadLogin, sonuc.Outcome);
        Assert.Empty(ag.Istekler);
    }

    /// <summary>Anahtar yokken giriş de kapalıdır; kol <c>NoKey</c>.</summary>
    [Fact]
    public async Task AnahtarsizGirisDenenmez()
    {
        var ag = new SahteAg();
        var sonuc = await new OpenSubtitlesProvider(ag, "").LoginAsync("kisi", "gizli", CancellationToken.None);

        Assert.Equal(SubtitleOutcome.NoKey, sonuc.Outcome);
        Assert.Empty(ag.Istekler);
    }

    // ---------- Authorization ----------

    /// <summary>
    /// Şartname <c>/download</c> için iki başlığı birden istiyor; aramada ise belirteç yalnız
    /// <c>base_url</c> VIP konağı gösterdiğinde gider. Varsayılan konakta aramaya belirteç
    /// koyan da, VIP'te koymayan da burada kırmızı olur.
    /// </summary>
    [Theory]
    [InlineData(SubtitleSession.DefaultHost, false)]
    [InlineData(SubtitleSession.VipHost, true)]
    public async Task AramayaBelirtecYalnizVipKonaktaGider(string konak, bool beklenen)
    {
        var belirtec = Jwt(DateTimeOffset.UtcNow.AddHours(4));
        var ag = new SahteAg()
            .Sonra(HttpStatusCode.OK, GirisGovdesi(belirtec, konak))
            .Sonra(HttpStatusCode.OK, "{\"data\":[]}");

        var saglayici = new OpenSubtitlesProvider(ag, "ANAHTAR", "https://ornek/api/v1", new MemorySessionStore());
        await saglayici.LoginAsync("kisi", "gizli", CancellationToken.None);
        await saglayici.SearchAsync(new SubtitleQuery(null, "film", new[] { "tr" }), CancellationToken.None);

        AltyaziKanit.Yaz("belirtec-vip.txt", konak + " => " + ag.Basliklar[1]);

        Assert.Equal(beklenen, ag.Basliklar[1].Contains("Authorization=Bearer " + belirtec, StringComparison.Ordinal));
        Assert.Contains("Api-Key=ANAHTAR", ag.Basliklar[1], StringComparison.Ordinal);
    }

    /// <summary>İndirme isteği varsayılan konakta bile <c>Authorization</c> taşır.</summary>
    [Fact]
    public async Task IndirmeIstegiHerKonaktaBelirtecTasir()
    {
        var kok = AltyaziKanit.Temiz("belirtec-indir");
        var film = Path.Combine(kok, "f.mkv");
        File.WriteAllBytes(film, new byte[16]);

        var belirtec = Jwt(DateTimeOffset.UtcNow.AddHours(4));
        var ag = new SahteAg()
            .Sonra(HttpStatusCode.OK, GirisGovdesi(belirtec, SubtitleSession.DefaultHost))
            .Sonra(HttpStatusCode.OK, "{\"link\":\"https://dl.ornek/a\",\"remaining\":7}")
            .SonraBayt(Encoding.UTF8.GetBytes("1\n"));

        var saglayici = new OpenSubtitlesProvider(ag, "ANAHTAR", "https://ornek/api/v1", new MemorySessionStore());
        await saglayici.LoginAsync("kisi", "gizli", CancellationToken.None);
        var sonuc = await saglayici.DownloadAsync(Aday, film, CancellationToken.None);

        AltyaziKanit.Yaz("belirtec-indir.txt", string.Join("\n", ag.Istekler.Zip(ag.Basliklar, (i, b) => i + " => " + b)));

        Assert.Equal(SubtitleOutcome.Ok, sonuc.Outcome);
        Assert.Contains("Authorization=Bearer " + belirtec, ag.Basliklar[1], StringComparison.Ordinal);

        // Imzali baglanti kendi kendini yetkilendiriyor; oraya ne anahtar ne belirtec gider.
        Assert.DoesNotContain("Authorization", ag.Basliklar[2], StringComparison.Ordinal);
    }

    /// <summary>
    /// Süresi dolmuş belirteç gönderilmez ve kutudan silinir. Yalnız varlığa bakıp ömre
    /// bakmayan bir uygulama burada kırmızı olur.
    /// </summary>
    [Fact]
    public async Task SuresiDolanBelirtecGonderilmezVeSilinir()
    {
        var simdi = new DateTimeOffset(2026, 5, 5, 5, 5, 5, TimeSpan.Zero);
        var kutu = new MemorySessionStore();
        kutu.Write(new SubtitleSession("eski-belirtec", SubtitleSession.DefaultHost, simdi.AddMinutes(-1)));

        var ag = new SahteAg().Sonra(HttpStatusCode.OK, "{\"data\":[]}");
        var saglayici = new OpenSubtitlesProvider(ag, "ANAHTAR", "https://ornek/api/v1", kutu, () => simdi);

        Assert.False(saglayici.HasSession);
        await saglayici.SearchAsync(new SubtitleQuery(null, "film", new[] { "tr" }), CancellationToken.None);

        Assert.DoesNotContain("Authorization", ag.Basliklar[0], StringComparison.Ordinal);
        Assert.Null(kutu.Read());
    }

    /// <summary>
    /// Ömrü dolmamış belirteç kutuda kalır; yukarıdaki ölçünün silme kolu yalnız süre
    /// bittiğinde işliyor.
    /// </summary>
    [Fact]
    public void OmruDolmayanBelirtecSilinmez()
    {
        var simdi = new DateTimeOffset(2026, 5, 5, 5, 5, 5, TimeSpan.Zero);
        var kutu = new MemorySessionStore();
        kutu.Write(new SubtitleSession("taze", SubtitleSession.DefaultHost, simdi.AddMinutes(1)));

        var saglayici = new OpenSubtitlesProvider(new SahteAg(), "ANAHTAR", "https://ornek/api/v1", kutu, () => simdi);

        Assert.True(saglayici.HasSession);
        Assert.NotNull(kutu.Read());
    }

    /// <summary>
    /// Açık adres verilmediğinde taban adres oturumun <c>base_url</c>ünden türer; VIP hesap
    /// ikinci istekten itibaren VIP konağa gider.
    /// </summary>
    [Fact]
    public async Task TabanAdresOturumunKonagindanTurer()
    {
        var kutu = new MemorySessionStore();
        kutu.Write(new SubtitleSession(Jwt(null), SubtitleSession.VipHost, DateTimeOffset.UtcNow.AddHours(2)));

        var ag = new SahteAg().Sonra(HttpStatusCode.OK, "{\"data\":[]}");
        await new OpenSubtitlesProvider(ag, "ANAHTAR", null, kutu)
            .SearchAsync(new SubtitleQuery(null, "film", new[] { "tr" }), CancellationToken.None);

        Assert.StartsWith("https://" + SubtitleSession.VipHost + "/api/v1/subtitles?", ag.Istekler[0], StringComparison.Ordinal);
    }

    // ---------- hata kolları ----------

    /// <summary>
    /// İndirmenin kotasız 401'i oturumu düşürür: belirteç silinir ve kol <c>NeedAccount</c>
    /// olur. Parola saklanmadığı için kendiliğinden yeniden giriş yoktur; kullanıcı Ayarlar'dan
    /// yeniden girer.
    /// </summary>
    [Fact]
    public async Task IndirmeninDortYuzBiriBelirteciSiler()
    {
        var kutu = new MemorySessionStore();
        kutu.Write(new SubtitleSession("yanacak", SubtitleSession.DefaultHost, DateTimeOffset.UtcNow.AddHours(2)));

        var ag = new SahteAg().Sonra(HttpStatusCode.Unauthorized, "{\"message\":\"invalid token\"}");
        var sonuc = await new OpenSubtitlesProvider(ag, "ANAHTAR", "https://ornek/api/v1", kutu)
            .DownloadAsync(Aday, "C:\\film\\a.mkv", CancellationToken.None);

        AltyaziKanit.Yaz("indirme-401.txt", sonuc.Outcome + " kutu " + (kutu.Read() is null ? "bos" : "dolu"));

        Assert.Equal(SubtitleOutcome.NeedAccount, sonuc.Outcome);
        Assert.Null(kutu.Read());
    }

    /// <summary>
    /// Kota gövdeli 401 oturumu düşürmez: belirteç geçerlidir, biten günlük haktır. Ayrımı
    /// yapmayan uygulama kullanıcıyı boşuna yeniden giriş yapmaya iter.
    /// </summary>
    [Fact]
    public async Task KotaGovdeliDortYuzBirBelirteciSilmez()
    {
        var kutu = new MemorySessionStore();
        kutu.Write(new SubtitleSession("kalacak", SubtitleSession.DefaultHost, DateTimeOffset.UtcNow.AddHours(2)));

        var ag = new SahteAg().Sonra(HttpStatusCode.Unauthorized,
            "{\"requests\":20,\"remaining\":0,\"reset_time\":\"01 hours and 10 minutes\"}");
        var sonuc = await new OpenSubtitlesProvider(ag, "ANAHTAR", "https://ornek/api/v1", kutu)
            .DownloadAsync(Aday, "C:\\film\\a.mkv", CancellationToken.None);

        Assert.Equal(SubtitleOutcome.QuotaExceeded, sonuc.Outcome);
        Assert.Equal(0, sonuc.Remaining);
        Assert.NotNull(kutu.Read());
    }

    /// <summary>
    /// İmzalı bağlantı üç saatten uzun yaşamıyor; süresi geçmiş bağlantının 410'u ayrı bir
    /// koldur. Ağ hatasına yıkan uygulama kullanıcıya yanlış şeyi söyler.
    /// </summary>
    [Fact]
    public async Task SuresiGecenBaglantiLinkExpiredOlur()
    {
        var ag = new SahteAg()
            .Sonra(HttpStatusCode.OK, "{\"link\":\"https://dl.ornek/eski\",\"remaining\":4}")
            .Sonra(HttpStatusCode.Gone, "Download link has expired", "text/plain");

        var sonuc = await new OpenSubtitlesProvider(ag, "ANAHTAR", "https://ornek/api/v1")
            .DownloadAsync(Aday, "C:\\film\\a.mkv", CancellationToken.None);

        AltyaziKanit.Yaz("baglanti-410.txt", sonuc.Outcome + " kalan " + sonuc.Remaining);

        Assert.Equal(SubtitleOutcome.LinkExpired, sonuc.Outcome);
        Assert.Equal(4, sonuc.Remaining);
    }

    /// <summary>
    /// 429'da sağlayıcının bildirdiği bekleme süresi kullanıcıya taşınır. Saniye de tarih de
    /// gelebiliyor; ikisi de saniyeye çevrilir. Beklenen sayı testin verdiği başlıktan türer.
    /// </summary>
    [Fact]
    public async Task IstekSiniriRetryAfterSaniyesiniTasir()
    {
        var simdi = new DateTimeOffset(2026, 3, 4, 5, 0, 0, TimeSpan.Zero);
        const int saniye = 37;

        var saniyeli = new SahteAg();
        saniyeli.SonraBasliklayan(HttpStatusCode.TooManyRequests, "{\"message\":\"slow down\"}",
            yanit => yanit.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(saniye)));

        var tarihli = new SahteAg();
        tarihli.SonraBasliklayan(HttpStatusCode.TooManyRequests, "{\"message\":\"slow down\"}",
            yanit => yanit.Headers.RetryAfter = new RetryConditionHeaderValue(simdi.AddSeconds(saniye).UtcDateTime));

        var bassiz = new SahteAg().Sonra(HttpStatusCode.TooManyRequests, "{\"message\":\"slow down\"}");

        var a = await new OpenSubtitlesProvider(saniyeli, "ANAHTAR", "https://ornek/api/v1", null, () => simdi)
            .DownloadAsync(Aday, "C:\\film\\a.mkv", CancellationToken.None);
        var b = await new OpenSubtitlesProvider(tarihli, "ANAHTAR", "https://ornek/api/v1", null, () => simdi)
            .DownloadAsync(Aday, "C:\\film\\a.mkv", CancellationToken.None);
        var c = await new OpenSubtitlesProvider(bassiz, "ANAHTAR", "https://ornek/api/v1", null, () => simdi)
            .DownloadAsync(Aday, "C:\\film\\a.mkv", CancellationToken.None);

        AltyaziKanit.Yaz("retry-after.txt",
            "saniyeli " + a.RetryAfterSeconds + " tarihli " + b.RetryAfterSeconds + " bassiz " + c.RetryAfterSeconds);

        Assert.Equal(SubtitleOutcome.RateLimited, a.Outcome);
        Assert.Equal(saniye, a.RetryAfterSeconds);
        Assert.Equal(saniye, b.RetryAfterSeconds);
        Assert.Equal(0, c.RetryAfterSeconds);
    }

    // ---------- belirtecin saklanması ----------

    /// <summary>
    /// Belirteç ayar dosyasının <b>yanına</b> yazılır, kullanıcının gerçek ayarına değil:
    /// yolu <see cref="SessionStore.PathFor"/> ayar yolundan türetir. Ölçü kendi yolunu
    /// <c>.calisma</c> altına verir.
    /// </summary>
    [Fact]
    public void OturumDosyasiAyarDosyasininYanindadir()
    {
        var kok = AltyaziKanit.Temiz("oturum-yol");
        var ayar = Path.Combine(kok, "settings.json");

        Assert.Equal(Path.Combine(kok, "opensubtitles-session.dat"), SessionStore.PathFor(ayar));
    }

    /// <summary>
    /// <c>VIDSHRINK_SETTINGS_PATH</c> verildiğinde oturum dosyası da oraya taşınır; gerçek
    /// <c>%APPDATA%</c> yoluna hiçbir şey yazılmaz.
    /// </summary>
    [Fact]
    public void OturumYoluAyarDegiskeniniIzler()
    {
        var kok = AltyaziKanit.Temiz("oturum-degisken");
        var ayar = Path.Combine(kok, "settings.json");
        var onceki = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
        try
        {
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", ayar);
            Assert.Equal(Path.Combine(kok, "opensubtitles-session.dat"), SessionStore.PathFor());
        }
        finally
        {
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", onceki);
        }
    }

    /// <summary>
    /// Belirteç diske düz yazılmaz. Windows'ta gövde DPAPI ile sarılır ve dosyada belirtecin
    /// kendisi görünmez; başka sistemde dosya hiç oluşmaz, oturum bellekte kalır. İki kol da
    /// ölçülür, hangisinin koştuğu kanıta yazılır.
    /// </summary>
    [Fact]
    public void BelirtecDiskeDuzYazilmaz()
    {
        var kok = AltyaziKanit.Temiz("oturum-kutu");
        var ayar = Path.Combine(kok, "settings.json");
        var dosya = SessionStore.PathFor(ayar);
        const string belirtec = "eyJhbGciOiJIUzI1NiJ9"
            + ".PIM77-belirtec-govdesi.imza";

        var kutu = new SessionStore(ayar);
        var oturum = new SubtitleSession(belirtec, SubtitleSession.VipHost,
            DateTimeOffset.UtcNow.AddHours(5).AddSeconds(-DateTimeOffset.UtcNow.Second));
        kutu.Write(oturum);

        var diskte = File.Exists(dosya) ? File.ReadAllText(dosya) : "";
        var geri = new SessionStore(ayar).Read();

        AltyaziKanit.Yaz("oturum-kutu.txt",
            "korumali " + SessionStore.Supported + "\ndosya " + File.Exists(dosya)
            + "\nuzunluk " + diskte.Length + "\ngeri " + (geri?.Host ?? "yok"));

        if (SessionStore.Supported)
        {
            Assert.True(File.Exists(dosya));
            Assert.DoesNotContain(belirtec, diskte, StringComparison.Ordinal);
            Assert.NotEmpty(diskte);
            Assert.Equal(belirtec, geri!.Token);
            Assert.Equal(SubtitleSession.VipHost, geri.Host);
            Assert.Equal(oturum.Expires.ToUnixTimeSeconds(), geri.Expires.ToUnixTimeSeconds());

            kutu.Clear();
            Assert.False(File.Exists(dosya));
            Assert.Null(new SessionStore(ayar).Read());
        }
        else
        {
            Assert.False(File.Exists(dosya));
            Assert.Equal(belirtec, kutu.Read()!.Token);
            Assert.Null(geri);
        }
    }

    /// <summary>Bozuk ya da başkasının koruduğu dosya sessizce yok sayılır, çökme olmaz.</summary>
    [Fact]
    public void BozukOturumDosyasiYokSayilir()
    {
        var kok = AltyaziKanit.Temiz("oturum-bozuk");
        var ayar = Path.Combine(kok, "settings.json");
        File.WriteAllText(SessionStore.PathFor(ayar), "bu base64 bile degil {{{");

        Assert.Null(new SessionStore(ayar).Read());
    }

    /// <summary>
    /// <see cref="JwtClaims.Expiry"/> yalnız üç parçalı, base64url gövdeli, <c>exp</c> taşıyan
    /// belirteçten okur; kalan her biçimde <c>null</c> döner ve çağıran kendi ömrünü koyar.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("tek-parca")]
    [InlineData("iki.parca")]
    [InlineData("a.!!!.c")]
    public void BozukBelirtectenOmurOkunmaz(string belirtec)
        => Assert.Null(JwtClaims.Expiry(belirtec));

    /// <summary>Olumlu kontrol: düzgün belirteçten ömür okunuyor.</summary>
    [Fact]
    public void DuzgunBelirtectenOmurOkunur()
    {
        var an = new DateTimeOffset(2027, 7, 7, 7, 7, 7, TimeSpan.Zero);
        Assert.Equal(an.ToUnixTimeSeconds(), JwtClaims.Expiry(Jwt(an))!.Value.ToUnixTimeSeconds());
    }
}
