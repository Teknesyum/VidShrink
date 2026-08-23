Tema: sıkıştırılan videoyu tek tıkla yükleyip bağlantı paylaşma · soru: Streamable bugün masaüstü istemciye izin veriyor mu

# Streamable — masaüstü istemciden yükleme

Bütün rakamlar ve alıntılar 2026-08-24'te birincil kaynaktan çekildi. Kaynak listesi en altta.
Yöntem: Streamable destek merkezinin Zendesk API'si (makale gövdesi + `updated_at`), canlı uç nokta
yoklaması (`curl`, kimlik doğrulamasız), Wayback anlık görüntüleri.

## Kısa cevap

**Belgelenmiş genel yükleme API'si yok.** Streamable'ın kendi API belgesi (güncelleme 2025-03-19)
şunu yazıyor: *"Streamable provides a limited, read-only API for accessing video metadata in order to
enable video playback."* ve *"video uploading, clipping, or editing is not supported by the Streamable
API at this time. You are required to go to Streamable.com to perform those functions."*

Belgelenen tek iki uç nokta: `GET https://api.streamable.com/oembed.json{?url}` ve
`GET https://api.streamable.com/videos/{shortcode}`. Belgede kimlik doğrulama bölümü yok.

**Kullanım şartlarında otomatik gönderim açıkça yasak.** Şartlar (güncelleme 2026-05-08), yasaklı
davranışlar listesinde: *"Use automated means to submit or edit User Content (except as we otherwise
permit)"*. Aynı listede *"Circumvent or attempt to circumvent any filtering, security measures, rate
limits or other features designed to protect the Service"* de var.

Yani soru "ücretsiz hesap yükleyebiliyor mu" değil; **hiçbir plan için genel bir yükleme API'si
sunulmuyor** ve otomatik gönderim sözleşmeyle yasaklanmış durumda.

---

## 1. API var mı, açık mı?

| | Durum |
|---|---|
| Belgelenen yükleme uç noktası | Yok (2025-03-19 tarihli belge) |
| Belgelenen okuma uç noktaları | `GET /oembed.json`, `GET /videos/{shortcode}` |
| Kimlik doğrulama | Belgede tanımlı değil |
| Belge adresi | `streamable.com/documentation` → 302 → `support.streamable.com/api-documentation` → 301 → Zendesk makalesi |
| API anahtarı / OAuth / geliştirici portalı | Bulunamadı. Destek merkezinde "developer", "partner", "API access" başlıklı bir başvuru yolu yok (40 makalelik tam liste tarandı) |

**Eski durum (arşiv).** 2018-05 ile 2020-03 arası Wayback anlık görüntülerinde `streamable.com/documentation`
şunları belgeliyordu: `POST https://api.streamable.com/upload` (çok parçalı dosya) ve
`GET https://api.streamable.com/import{?url}`; kimlik doğrulama **Basic Auth**, kimlik bilgisi
**kullanıcının e-postası ve hesap parolası**. Belgede ayrıca *"If you're a bot, please provide a
descriptive user agent as well"* yazıyordu. Bu bölüm 2020 içinde kaldırıldı: 2020-12-23 tarihli
`support.streamable.com/api-documentation` anlık görüntüsü bugünküyle aynı "read-only / not supported"
ifadesini taşıyor.

**Uç noktalar hâlâ ayakta.** 2026-08-24 canlı yoklama (kimlik bilgisi gönderilmedi):

- `POST https://api.streamable.com/upload` → **401**
- `GET  https://api.streamable.com/upload` → 405
- `GET  https://api.streamable.com/import` → **401**
- `POST https://api.streamable.com/import` → 405
- `GET  https://api.streamable.com/oembed.json?url=...` → 200

401 dönmesi, uç noktanın var olduğunu ve kimlik doğrulama beklediğini gösterir; **geçerli bir hesapla
gerçekten yükleme kabul edip etmediği test edilmedi — `doğrulanamadı`** (hesap açıp deneme yapılmadı).
Bu, belgelenmemiş ve sözleşmeyle yasaklanmış bir yüzeydir; tek taraflı, habersiz kapatılabilir.

Üçüncü taraf sarmalayıcıların hepsi terk edilmiş: `jernejovc/pystreamable` (MIT, 8 yıldız, son push
2017-10-11), `denizdogan/streamable-js` (MIT, **arşivli**, son push 2016-06-05) — `gh api`, 2026-08-24.
Bu iki depo eski Basic Auth yüklemesinin bir zamanlar gerçekten çalıştığının kanıtı; bugünkü durumun
kanıtı değil.

## 2. Ücretsiz hesap yükleyebiliyor mu?

Web sitesi üzerinden evet, API üzerinden hiçbir plan için hayır (belgeye göre).

Web sitesi sınırları (destek makalesi "Free Plan Limits", güncelleme 2025-10-31):

- **Hesapsız (anonim) yükleme mümkün.** Anonim yüklenen videolar 24 saat sonra arşivleniyor.
- **Ücretsiz hesap:** dosya başına en çok **250 MB** ve **10 dakika**; videolar **90 gün** sonra
  otomatik siliniyor.
- **Pro:** dosya boyutu ve süre sınırı yok; videolar yalnız istek üzerine siliniyor.

**Plan adları ve fiyatlar `doğrulanamadı`.** `streamable.com/pricing` istemci tarafında üretiliyor;
HTML'de yalnız meta açıklama var, plan tablosu yok. Destek makaleleri "Pro" ve "Business" adlarını
ve bir "Legacy Plans" (yeni abonelere kapalı eski planlar, güncelleme 2026-04-01) durumunu doğruluyor
ama fiyat vermiyor. Üçüncü taraf toplayıcılar çelişiyor: TrustRadius "$8.99–$39.99", Tekpon
"$12.99–$49" diyor (arama sonucu özeti, 2026-08-24) — **ikisi de şüpheli, birincil kaynak değil.**

Hiçbir destek makalesinde "API erişimi" bir plan özelliği olarak geçmiyor. Yani API, ücretli plana
bağlanmış bir özellik değil; **kamuya açık olarak hiç sunulmuyor.**

## 3. Üçüncü taraf istemci yasağı var mı?

"Yalnız resmî istemciler" diye bir madde yok, ama işlevsel olarak aynı sonucu veren üç madde var
(Terms of Service, güncelleme 2026-05-08):

1. **Otomatik gönderim yasağı:** *"Use automated means to submit or edit User Content (except as we
   otherwise permit)"*. VidShrink'in "tek tıkla yükle" özelliği tam olarak budur. Parantez içindeki
   "except as we otherwise permit" bir izin yolu bırakıyor ama **belgelenmiş bir başvuru yolu yok**
   (destek merkezinde böyle bir makale bulunamadı).
2. **Sınırlı, geri alınabilir lisans:** hizmete erişim "limited, nonexclusive, non-transferable and
   revocable"; tersine mühendislik, hizmetin herhangi bir parçasını kopyalama/aynalama, bütünlüğüne
   müdahale yasak.
3. **Koruma önlemlerini atlatma yasağı:** hız sınırı dâhil güvenlik önlemlerini aşmaya çalışmak yasak.

Ayrıca API belgesinin kendisi ayrım koyuyor: *"Web applications must use the embedded Streamable player
while **pre-approved** native applications may render videos using signed video URLs."* Yani **oynatma
için bile ön onay** öngörülüyor; onay süreci belgelenmemiş.

## 4. Kota ve sınırlar

| Sınır | Ücretsiz | Pro | Kaynak |
|---|---|---|---|
| Dosya boyutu | 250 MB | sınırsız | Free Plan Limits, 2025-10-31 |
| Süre | 10 dakika | sınırsız | aynı |
| Saklama | 90 gün, sonra otomatik silme | istek üzerine silme | aynı |
| Anonim yükleme saklama | 24 saat sonra arşivleniyor | — | aynı |
| Aylık yükleme adedi | belgelenmemiş — `doğrulanamadı` | belgelenmemiş | — |
| Bant genişliği kotası | belgelenmemiş; üçüncü taraflar "2 TB/ay Pro" diyor — **şüpheli, doğrulanamadı** | | arama sonucu, 2026-08-24 |
| API hız sınırı | belgelenmemiş | | — |

Silinen video geri getirilemiyor: *"Once a video is deleted, it cannot be recovered."* (destek makalesi,
2025-03-11).

## 5. Silme ve gizlilik

**Silme:** API ile silme uç noktası belgelenmemiş; 2018 tarihli eski belgede de silme uç noktası yoktu.
Silme yalnız web panosundan. Ücretsiz hesapta 90 gün sonra zaten otomatik siliniyor.

**Gizlilik** (destek makalesi "Video Privacy", güncelleme 2025-10-31): üç seviye var —
**Link only (varsayılan)**, **Password Protected**, **Only Me**. Ayrıca hesap düzeyinde Domain Privacy
(yalnız belirtilen alan adlarında gömme), "Disallow playback on Streamable.com" ve "Disable video
sharing" anahtarları var.

VidShrink açısından iki önemli sonuç:

- Varsayılan **listelenmemiş değil, "bağlantısı olan herkes izler"**. Bu, paylaşım için yeterli ama
  "gizli" değildir; kullanıcıya böyle anlatılmamalı.
- Bu ayarların hiçbiri API'den yapılamıyor. Uygulama bir video yüklese bile **gizlilik seviyesini
  ayarlayamaz**; kullanıcı web panosuna gitmek zorunda kalır. Yükleme özelliğinin vaadi burada kırılır.

## 6. Sorumluluk kime ait?

Hesap sahibine.

- **Tazminat maddesi** doğrudan hesap sahibini bağlıyor: kullanıcı, kendi davranışından, yüklediği
  içerikten "or the rights of any third party by you **or any person using your Streamable account**"
  doğan taleplere karşı Streamable'ı tazmin etmeyi kabul ediyor. VidShrink kullanıcının kendi hesabıyla
  yüklerse, yükleme uygulama üzerinden yapılmış olsa da hesap sahibi sorumludur.
- **Beyan ve garanti:** kullanıcı içeriğin bütün haklarına sahip olduğunu beyan ediyor.
- **DMCA bildirimi** Streamable'ın belirlenmiş temsilcisine gidiyor: Bending Spoons US Inc.,
  169 Madison Ave STE 11218, New York; `copyright@streamable.com` / `dmca@streamable.com`. Yaptırım
  içeriğin kaldırılması ve tekrarlayan ihlalde **hesabın kapatılması**.
- İstemci yazılımına doğrudan bir bildirim yolu yok. Ama şartların 3. maddesindeki otomatik gönderim
  yasağı ihlal edilirse muhatap yine hesap sahibi olur — **kullanıcının hesabı kapanır, VidShrink'in
  değil.** Riski taşıyan taraf, uygulamayı kuran kişidir. Bu, tek bakımcılı AGPL bir proje için kabul
  edilmesi zor bir devretme.

---

## Yapılabilir mi / yapılmalı mı

**Yapılabilir (teknik olarak):** `POST api.streamable.com/upload` uç noktası bugün 401 dönüyor, yani
duruyor. Kullanıcının e-posta + parolasıyla Basic Auth kurup çok parçalı dosya göndermek muhtemelen
çalışır — **doğrulanamadı, hesapla test edilmedi.**

**Yapılmalı mı: hayır.** Dört ayrı nedenle:

1. **Sözleşme ihlali.** "Use automated means to submit User Content" açık yasak. İzin yolu var ama
   belgelenmemiş; başvurmadan yapılırsa kullanıcının hesabı risk altına girer.
2. **Parola saklama.** Basic Auth demek, kullanıcının **Streamable hesap parolasını** masaüstünde
   saklamak demek. Kapsam yok, iptal edilebilir jeton yok, yetki daraltma yok. Parola sızarsa kaybedilen
   video hosting değil, parolanın tekrar kullanıldığı her yer. Bu tek başına yeter sebep.
3. **Belgelenmemiş yüzey.** Altı yıldır belgeden çıkarılmış bir uç noktaya bağlanmak; habersiz
   kapanabilir, alan yeniden adlandırılabilir. Kırıldığında kullanıcı hatayı VidShrink'e yazar.
4. **Vaat yarım kalır.** Yüklense bile gizlilik ayarı, başlık, silme API'den yapılamıyor; ücretsiz
   hesapta 250 MB / 10 dk tavanı ve 90 günlük ömür var. "Tek tıkla paylaş" özelliği yarısı web
   panosunda tamamlanan bir özelliğe dönüşür.

**Bunun yerine yapılabilecek en ucuz şey:** çıktı klasörünü açan/dosyayı panoya koyan bir "Paylaş"
düğmesi ve varsayılan tarayıcıda Streamable yükleme sayfasını açmak. Sözleşmeye dokunmaz, parola
istemez, sıfır bakım yükü taşır. Gerçek yükleme isteniyorsa aşağıdaki iki yol Streamable'dan daha sağlam.

---

## Alternatif A — Kullanıcının kendi deposu (S3 uyumlu / WebDAV)

1. **API var mı, açık mı?** Var ve açık. S3 API'si fiilî standart; AWS S3, Backblaze B2, Cloudflare R2,
   MinIO, Wasabi aynı imzayı konuşuyor. WebDAV ise RFC 4918; Nextcloud, ownCloud, birçok paylaşımlı
   barındırma destekliyor. Kimlik doğrulama: S3'te erişim anahtarı + gizli anahtar (SigV4), WebDAV'da
   temel kimlik doğrulama ya da uygulama parolası. İkisinde de **kapsamı daraltılmış, iptal edilebilir**
   kimlik bilgisi üretilebilir — Streamable'ın hesap parolasından temel fark budur.
2. **Ücretsiz hesap yükleyebiliyor mu?** Kavram yok; kullanıcı kendi kotasını satın alır. Kendi
   sunucusunda barındıran için maliyet sıfır olabilir.
3. **Üçüncü taraf istemci yasağı?** Yok. Kullanıcının sağlayıcısıyla arasındaki sözleşme geçerli;
   VidShrink taraf değil. Sorumluluk ve şart yorumu kullanıcının kendi sağlayıcısına ait.
4. **Kota ve sınırlar:** kullanıcının satın aldığı kadar. S3'te çok parçalı yükleme büyük dosyayı bölerek
   gönderir. Saklama süresi kullanıcının yaşam döngüsü kuralına bağlı. Sağlayıcıya özgü rakamları burada
   yazmıyorum — sağlayıcıya göre değişir, tek bir rakam `doğrulanamaz`.
5. **Silme ve gizlilik:** silme API'de standart (`DELETE`). Gizlilik en iyi seçenek: **ön imzalı
   (presigned) URL** ile süresi dolan, tahmin edilemez bağlantı üretilebilir; kova özel kalır.
   Streamable'ın veremediği "gerçekten listelenmemiş, süreli bağlantı" burada var.
6. **Sorumluluk:** tamamen kullanıcıda; sağlayıcı bildirimi doğrudan hesap sahibine gider. VidShrink
   hiçbir zincirde görünmez.

**Bedeli:** Oynatıcı yok — tarayıcı ham MP4'ü açar, önizleme/kapak/uyarlanabilir kalite yok. Kullanıcının
kurulum yapması gerekir (kova, anahtar, CORS, bazen public-read politikası). Yani "tek tıkla" değil, "bir
kez kur, sonra tek tıkla". Bağımlılık yüzeyi: bir S3 imzalama kitaplığı ya da elle SigV4 — WebDAV
tarafında yalnız `HttpClient` yeter, en ucuz yol WebDAV'dır.

## Alternatif B — Vimeo

1. **API var mı, açık mı?** Var, belgeli ve kamuya açık: `developer.vimeo.com`, uygulama kaydı,
   **OAuth 2** ile kullanıcı adına yetki. Parola saklanmaz, jeton iptal edilebilir — Streamable'a göre
   temel üstünlük.
2. **Ücretsiz hesap yükleyebiliyor mu?** Evet, ama **onaya bağlı.** Vimeo destek makalesine göre
   ücretli plan sahipleri yükleme erişimi için otomatik onaylı; ücretsiz kullanıcılar ve ücretli plana
   geçmeden uygulama oluşturmuş geliştiriciler **elle inceleme** ile "upload access" istemek zorunda ve
   inceleme *"can take up to five business days"*. Gerekçe olarak API'nin kötüye kullanımını önlemek
   gösteriliyor.
3. **Üçüncü taraf istemci yasağı?** Yok — tam tersi, üçüncü taraf uygulama modeli resmî. Bedeli, VidShrink
   için bir uygulama kaydı ve onay sürecidir; ayrıca uygulama kimliği/sırrı AGPL bir masaüstü ikilisinde
   gizli tutulamaz (public client sorunu — PKCE ile yönetilebilir ama tasarım yükü getirir).
4. **Kota ve sınırlar:** Ücretsiz plan, 2024-06-17'den sonra açılan hesaplar için **hesap ömrü boyunca
   toplam 1 GB** (Vimeo destek makalesi). Daha eski hesaplarda video adedi tabanlı farklı sınır var.
   Aylık video adedi için üçüncü taraf "ayda 2 video, ömür boyu 25" diyor — **şüpheli, doğrulanamadı.**
5. **Silme ve gizlilik:** silme API'den yapılabiliyor. Gizlilik seviyeleri API'den ayarlanabiliyor; ancak
   "listelenmemiş / yalnız bağlantısı olan" gibi bazı gizlilik seçeneklerinin ücretli plan gerektirdiği
   biliniyor — **hangi seviyenin hangi planda olduğu bu taramada doğrulanmadı.**
6. **Sorumluluk:** hesap sahibinde; Vimeo'nun kendi DMCA süreci işler. Ek olarak **uygulama** da Vimeo'ya
   kayıtlıdır ve kötüye kullanımda uygulamanın erişimi kesilebilir — yani Streamable'dan farklı olarak
   burada VidShrink de zincirde görünür. Bu bir dezavantaj değil, bilinçli kabul edilmesi gereken bir
   yük: tek bakımcılı bir proje bir uygulama kaydının sorumluluğunu üstlenir.

**Bedeli:** OAuth akışı (tarayıcı açma + geri çağırma dinleyicisi ya da cihaz kodu), jeton yenileme ve
saklama, uygulama onayı, gizlilik/plan matrisinin kullanıcıya doğru anlatılması. Streamable'ın "parola
gir, gönder" basitliğinin yanında belirgin şekilde daha pahalı — ama tek doğru maliyet budur.

---

## Öneri

1. **Streamable'a otomatik yükleme yapma.** Yerine "sıkıştırılmış dosyayı Streamable'a yükle" düğmesi
   varsayılan tarayıcıda Streamable'ı açsın, dosyayı gösteren klasörü de açsın. Şartlara dokunmaz.
2. Gerçek yükleme özelliği istenirse **önce WebDAV / S3-ön imzalı yolu**: en küçük bağımlılık, en iyi
   gizlilik, sıfır sözleşme riski, sorumluluk zincirinde VidShrink hiç yok.
3. Vimeo yalnız "gömülebilir oynatıcı" gerçekten gerekiyorsa. O zaman da OAuth ve uygulama onayı
   maliyeti baştan planlanmalı; sonradan eklenen bir şey değil.
4. Hangi yol seçilirse seçilsin, arayüzde plan/kota bilgisini **sabit yazma**. Bu sayılar yılda birkaç
   kez değişiyor (Streamable'ın ücretsiz plan makalesi 2025-10-31'de, şartları 2026-05-08'de
   güncellenmiş). Sınırı sunucudan gelen hata mesajıyla göster.

## Şüpheli/riskli yanlar (özet)

- **Lisans/marka.** Streamable tescilli bir hizmet; DMCA temsilcisi Bending Spoons US Inc. üzerinden
  görünüyor. Açık kaynak lisansı yok, API'si sözleşmeye bağlı. VidShrink AGPL-3.0-or-later; adı arayüzde
  kullanmak tanımlayıcı kullanımdır ama onay/işbirliği izlenimi verilmemeli.
- **Belge tazeliği.** API belgesi 2025-03-19'dan beri güncellenmemiş; şartlar 2026-05-08'de güncellenmiş.
  Belge ile canlı uç noktalar tutarsız (belge "yok" diyor, uç nokta 401 dönüyor). Bu tutarsızlık
  güvenilecek bir zemin değil.
- **Doğrulanamayan rakamlar:** plan adları ve fiyatları, aylık yükleme adedi, bant genişliği kotası,
  API hız sınırı, "2 TB/ay" iddiası, Vimeo'nun "ayda 2 video / ömür boyu 25" iddiası.
- **Gizli kurulum maliyeti.** Streamable yolu ilk bakışta en ucuzu (bir HTTP POST); asıl maliyet parola
  saklama, gizlilik ayarının API'den yapılamaması ve belgesiz uç noktanın bakımı. S3/WebDAV'ın maliyeti
  görünür ve tek seferlik; Vimeo'nunki görünür ve sürekli.

## Kaynaklar

Hepsi 2026-08-24'te çekildi.

- Streamable API belgesi (Zendesk makale 35415672400916, güncelleme 2025-03-19) —
  https://streamable-support.zendesk.com/hc/en-us/articles/35415672400916-API-Documentation
- Streamable Terms of Service (makale 44623209368468, güncelleme 2026-05-08) —
  https://streamable-support.zendesk.com/hc/en-us/articles/44623209368468-Terms-of-Service
- Free Plan Limits (makale 35415717293204, güncelleme 2025-10-31)
- Video Privacy (makale 35415573864724, güncelleme 2025-10-31)
- Uploading Basics (makale 35415719714324, güncelleme 2026-04-02)
- Recover deleted videos (makale 35415486233876, güncelleme 2025-03-11)
- Legacy Plans (makale 35415949808916, güncelleme 2026-04-01)
- DMCA (makale 35415294578196) ve Copyright Enforcement (makale 35388987655444), güncelleme 2025-12-31
- Wayback: `streamable.com/documentation` 2018-05-01, 2019-01-01, 2019-06-01, 2019-12-01, 2020-03-01
  anlık görüntüleri (POST /upload + Basic Auth belgeli) ve `support.streamable.com/api-documentation`
  2020-12-23 anlık görüntüsü (read-only ifadesi yerleşmiş)
- Canlı uç nokta yoklaması: `curl` HTTP durum kodları, kimlik bilgisi gönderilmedi
- `gh api repos/jernejovc/pystreamable`, `gh api repos/denizdogan/streamable-js`
- Vimeo: "How to request API upload access" —
  https://help.vimeo.com/hc/en-us/articles/12427803706001-How-to-request-API-upload-access
- Vimeo: "API technical and developer prerequisites" —
  https://help.vimeo.com/hc/en-us/articles/12427702473105-API-technical-and-developer-prerequisites
- Vimeo: "About the Vimeo Free plan" —
  https://help.vimeo.com/hc/en-us/articles/12425432518801-About-the-Vimeo-Free-plan
- Fiyat aralığı iddiaları (şüpheli, birincil değil): TrustRadius ve Tekpon Streamable fiyat sayfaları,
  arama sonucu özeti üzerinden
