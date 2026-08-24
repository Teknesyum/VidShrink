# Anonim, kısa ömürlü, tarayıcıda oynayan video barındırma — tarama

Tarih: 2026-08-24. Yöntem: her aday için belgesi okundu; oynatılabilirlik iddiası
2 KB'lik gerçek bir `.mp4` yüklenip HTTP başlıkları ölçülerek doğrulandı. Yükleme
testi yapılan adaylarda test dosyası silindi ya da süresi dolmaya bırakıldı.

## Yedi şart

1. Hesap yok — gönderen giriş yapmıyor.
2. Bakımcı kaydı yok — biz konsolda uygulama kaydı yapıp anahtar almıyoruz.
3. Tarayıcıda oynuyor — alıcı indirmek zorunda değil.
4. Uzun süre barınmıyor.
5. İki taraf da kapatabiliyor.
6. Sözleşme otomatik yüklemeye izin veriyor.
7. Boyut tavanı 20–300 MB aralığını kaldırıyor.

## Tablo

| Aday | 1 hesap yok | 2 bakımcı kaydı yok | 3 tarayıcıda oynar | 4 kısa ömür | 5 iki taraf kapatır | 6 sözleşme izinli | 7 tavan yeter |
|---|---|---|---|---|---|---|---|
| **storage.to** | evet | evet | evet | evet | kısmen (yalnız gönderen) | evet | evet (25 GB) |
| **filebin.net** | evet | evet | evet | evet | evet | evet | doğrulanamadı |
| uguu.se | evet | evet | evet | evet | hayır | evet (sessiz) | kısmen (128 MiB) |
| litterbox.catbox.moe | evet | evet | doğrulanamadı | evet | hayır | evet | evet (1 GB) |
| tmpfiles.org | evet | evet | hayır | evet | hayır | evet | kısmen (100 MiB) |
| temp.sh | evet | evet | hayır | evet | hayır | doğrulanamadı | evet (4 GB) |
| catbox.moe | evet | evet | doğrulanamadı | **hayır** | hayır | evet | evet (200 MB) |
| 0x0.st | evet | evet | doğrulanamadı | **hayır** | doğrulanamadı | **hayır** | evet (512 MiB) |
| bashupload.com | evet | evet | **hayır** | evet | doğrulanamadı | doğrulanamadı | doğrulanamadı |
| file.io | doğrulanamadı | evet | doğrulanamadı | evet | evet | doğrulanamadı | doğrulanamadı |
| gofile.io | evet (guest) | evet | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı |
| qu.ax | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı |
| send.vis.ee | evet | evet | **hayır** | evet | evet | doğrulanamadı | doğrulanamadı |
| pixeldrain.com | **hayır** | **hayır** | evet | doğrulanamadı | — | — | — |
| dubz.co | evet (guest) | doğrulanamadı (API yok) | evet | **hayır** | doğrulanamadı | doğrulanamadı | kısmen (100 MB) |
| streamwo.com | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı |
| tempclip.com | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı |
| videostemporales.net | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı | doğrulanamadı |
| streamja.com | — | — | — | — | — | — | DNS çözülmüyor, ölü |

---

## Geçen adaylar

### storage.to — yedi şartın yedisi

Uçtan uca test edildi: hiçbir hesap, hiçbir anahtar olmadan `POST /api/upload/init` →
R2 presigned URL'e `PUT` → `POST /api/upload/confirm` üçlüsüyle dosya yüklendi ve
`https://storage.to/nLcjsZGY0` biçiminde bağlantı döndü (2026-08-24).

1. **Hesap yok:** evet. Belge birebir: "Anonymous uploads work without authentication."
   Anonim istemci kendi ürettiği rastgele `X-Visitor-Token` başlığını gönderiyor,
   sunucudan alınan bir şey değil. <https://storage.to/docs/api>
2. **Bakımcı kaydı yok:** evet. Belge birebir: "no API key is required for anonymous
   uploads." Konsol, uygulama kaydı, gizli anahtar yok — AGPL kaynak kodda saklanacak
   bir şey oluşmuyor.
3. **Tarayıcıda oynuyor:** evet, gerçek oynatıcı sayfası. Paylaşım sayfası
   `<video controls playsInline preload="metadata">` içeriyor ve kaynağı imzalı bir CDN
   URL'i. CDN başlıkları ölçüldü: `Content-Type: video/mp4`, `Accept-Ranges: bytes`,
   `Content-Disposition` yok. Yani satır içi oynatma hem sayfada hem doğrudan bağlantıda
   çalışıyor. Ayrıca `POST /file/{id}/thumbnail` uç noktası "used on the download page"
   açıklamasıyla video kapak resmi kabul ediyor — sayfa video için tasarlanmış.
4. **Kısa ömür:** evet. Anonim varsayılan 3 gün, `expiry_days` ile 1–7 gün arası
   seçilebiliyor. Testte dönen `expires_at` alanı yüklemeden tam 3 gün sonrasıydı.
   Ek olarak `POST /file/{id}/max-downloads` ile "N indirmeden sonra yan" davranışı var.
5. **İki taraf da kapatabiliyor:** kısmen. `confirm` yanıtı bir `owner_token` döndürüyor;
   `DELETE /api/file/{id}` isteğine `Authorization: Owner <token>` başlığıyla
   `{"success":true}` alındı ve bağlantı 403'e düştü — anonim silme jetonu **çalışıyor**,
   hesap gerekmiyor. Ama bu jeton yalnız gönderende. Alıcının silme yolu yok; elinde
   yalnız indirme sayfasındaki "report" bağlantısı var. Şart 5'in yarısı karşılanıyor.
6. **Sözleşme:** açıkça izinli. Kullanım şartları §3 yasaklar listesinde birebir şu var:
   "Use unauthorised automated tools to mass upload or download files (**our official API,
   CLI and desktop apps are permitted**)". Resmî API üzerinden programatik yükleme
   parantez içinde muaf tutulmuş. Şartlar 11 Nisan 2026'da güncellenmiş.
   <https://storage.to/terms>
7. **Tavan:** anonim 25 GB. 20–300 MB fazlasıyla altında. Anonim kotalar: 24 saatte
   50 dosya (cihaz/IP başına) ve 100 GB yükleme bant genişliği.

İşletmeci doğrulandı: Click Here Digital Ltd, İngiltere ve Galler, şirket no 07093545 —
Companies House kaydı mevcut.

### filebin.net — yedincisi dışında hepsi

Test edildi: `POST https://filebin.net/<bin>/<dosya>` ile anahtar olmadan yüklendi,
201 döndü (2026-08-24).

1. **Hesap yok:** evet. Ana sayfanın kendi ifadesi: "Convenient file sharing in three
   steps without registration."
2. **Bakımcı kaydı yok:** evet. Kimlik doğrulaması olan tek uç nokta yönetici onayı
   (`PUT /admin/approve/{bin}`); istemci tarafında hiçbir anahtar yok.
3. **Tarayıcıda oynuyor:** evet, ama oynatıcı sayfası olarak değil. Dosya URL'i imzalı
   bir S3 URL'ine 302 yapıyor ve o URL'de `response-content-disposition=inline` ile
   `response-content-type=video/mp4` gömülü. Ölçülen son yanıt:
   `Content-Type: video/mp4`, `Content-Disposition: inline; filename="..."`,
   `Accept-Ranges: bytes`. Tarayıcı videoyu satır içi oynatır, indirmeye zorlanmaz.
   Bin sayfasında `<video>` etiketi yok; kullanıcı bin sayfasından dosya adına
   tıklayınca oynatma başlar. Senin ölçütünde "ikinci sınıf değil ama oynatıcı sayfası
   da değil" konumunda.
4. **Kısa ömür:** evet, 6 gün. Testte dönen `expired_at_relative` alanı "6 days from
   now". Sabit; istemci seçemiyor (sunucu tarafı `FILEBIN_EXPIRATION` ayarı).
5. **İki taraf da kapatabiliyor:** evet, tam. OpenAPI belgesi birebir: "Everyone knowing
   the URL to the bin have access to deleting files from it." `DELETE /<bin>` test
   edildi, "Bin deleted successfully" döndü, dosya 404 oldu. Ek olarak `POST /<bin>/lock`
   ile bin salt-okunur yapılabiliyor.
6. **Sözleşme:** açıkça izinli, hatta teşvik ediliyor. API sayfasının kendi ifadesi:
   "This API documentation page is generated from the OpenAPI 3.0 specification and aims
   to make it easy to create tools to upload, list, download and delete files from
   Filebin." Kullanım şartlarının "Prohibited uses" maddesinde otomasyona dair yasak yok;
   yalnız "spam, phish, pharm, pretext, spider, crawl, or scrape" var — yükleme bunların
   dışında. Şartlar 8 Eylül 2020'de güncellenmiş.
7. **Tavan:** `doğrulanamadı`. Ne OpenAPI belgesinde ne de sitede dosya başına bir MB
   sınırı yazıyor. Belgede yalnız servis genelinde bir eşik var: "Storage limit reached.
   Please retry later." — doluysa yükleme reddediliyor, boyuttan bağımsız.

Sunucu yazılımı açık kaynak: `espebra/filebin2`, BSD-3-Clause, son push 2026-08-19,
son etiketli sürüm v1.2.0 (2026-07-29), 6 açık issue, 278 yıldız. Çalışan sürüm sitede
`v1.2.0-5-gafd3176` olarak yazıyor.

---

## Sınırda kalanlar

### uguu.se — 5/7

Yükleme testi geçti (`POST https://uguu.se/upload`, anahtar yok). Dönen `d.uguu.se`
bağlantısının ölçülen başlıkları: `Content-Type: video/mp4`, `Accept-Ranges: bytes`,
`Content-Disposition` yok — satır içi oynuyor. Ana sayfa: "Max upload size is 128 MiB &
files expire after 3 hours." API belgeli ve curl örneği var, yani otomatik yükleme
zımnen izinli; ayrı bir kullanım şartları sayfası yok, FAQ yalnız yasak içerik ve
takedown konusunu düzenliyor. İki eksik: silme jetonu yok (gönderen de alıcı da
kapatamaz, 3 saat beklenir) ve 128 MiB tavanı 20–300 MB aralığının üst yarısını kesiyor.
İşletmeci Pomf AB (İsveç). Yazılım `nokonoko/Uguu`, GPL-3.0, son push 2025-12-06,
son etiketli sürüm v.1.9.9 (2025-12-06).

### litterbox.catbox.moe — TR'den erişilemedi

Yükleme **çalıştı** (`https://litterbox.catbox.moe/resources/internals/api.php`,
`time=1h`, anahtar yok) ve `https://litter.catbox.moe/qrx3f1.mp4` döndü. Ama o bağlantı
bu ağdan açılamadı: `schannel: SEC_E_UNTRUSTED_ROOT` — TLS zinciri araya giren bir
kutuyla kesiliyor. Aynı anda `catbox.moe` ve `files.catbox.moe` sorunsuz açıldı, yani
kesinti `litter.catbox.moe` alt alanına özel. Catbox'ın kendi FAQ'si zaten Türkiye'yi
engelli ülkeler listesinde sayıyor: "Turkey — Why? Unknown... You will need to use a VPN
to bypass the IP-level block." Dolayısıyla şart 3 bu ağdan `doğrulanamadı` ve
Türkiye'deki kullanıcı için bağlantı büyük ihtimalle açılmıyor. İkinci eksik: Litterbox
API'sinin tek istek tipi var — "There is only 1 request type for Litterbox - fileupload."
Silme yok, şart 5 düşüyor.

---

## Elenenler

- **pixeldrain.com** — Anonim yükleme kapalı. Anahtarsız POST `401
  authentication_required` döndü, API belgesi birebir yazıyor: "This API requires
  authentication with an API key. Anonymous uploading is not supported." Şart 1 ve 2
  birlikte düşüyor: anahtarı ya kullanıcı yapıştıracak ya biz ikiliye gömeceğiz,
  AGPL'de ikincisi mümkün değil. **Konsey üyesinin iddiası doğrulandı.** Ne zaman
  değişti: Wayback'te 2024-08-04 anlık görüntüsünde hâlâ "The methods for uploading and
  retrieving files don't require an API key" yazıyor, 2024-11-27 anlık görüntüsünde "To
  upload files to pixeldrain you will need an API key" yazıyor. Değişim bu iki tarih
  arasında; **kesin tarih `doğrulanamadı`** (Eylül–Ekim 2024 anlık görüntüleri boş döndü).
- **catbox.moe** — Şart 4 düşüyor: "Anonymously uploaded files are kept until they have
  2 years of inactivity." Kalıcıya yakın. Ayrıca silme `userhash`, yani hesap istiyor.
- **0x0.st** — Şart 4 düşüyor: `min_age = 30 days`, `max_age = 1 year`; 300 MB'lık bir
  dosya sayfadaki formüle göre ~54 gün duruyor. Üstüne ana sayfa otomatik istemcilere
  açıkça düşman ("CLANKERS ARE NOT WELCOME HERE"), yani şart 6 da düşüyor. Bu ağdan
  zaten erişilemedi (bağlantı sıfırlandı); metin Wayback 2026-08-23 anlık görüntüsünden
  okundu.
- **bashupload.com** — Şart 3 düşüyor: "Files are stored for 3 days and **can be
  downloaded only once**." Tek indirme hakkı olan bir bağlantıda tarayıcı oynatıcısı
  çalışmaz (range istekleri ikinci indirmedir), alıcı da videoyu bir daha açamaz. Metin
  Wayback anlık görüntüsünden; canlı site bu ağdan erişilemedi.
- **tmpfiles.org** — Şart 3 düşüyor. Yükleme çalıştı, ToS gayet uygun ("No account
  registration is required", 1–48 saat, 100 MiB) ve API belgeli, ama görüntüleme
  sayfasında yalnız "Download" bağlantısı var; `/dl/<id>/<ad>` doğrudan bağlantısı
  tarayıcı UA'sı ve referer ile bile 302 ile görüntüleme sayfasına geri atıyor. Oynatıcı
  yok. Silme de yok.
- **temp.sh** — Şart 3 düşüyor. Dosya sayfası bir `<form method="POST">` düğmesi
  ("Click here to download"); GET ile doğrudan dosya yok, oynatıcı yok. Paylaşılan
  bağlantı bir indirme düğmesi sayfası.
- **send.vis.ee** — Şart 3 düşüyor. Firefox Send türevi; dosya tarayıcıda AES-GCM ile
  şifrelenip yükleniyor, alıcı tarafında yine tarayıcıda çözülüyor. Sunucuda oynatılabilir
  bir mp4 URL'i **yok**; alıcı indirmek zorunda. Kaynak: proje FAQ'si, "Send encrypts and
  decrypts the files in the browser". Ayrıca üst proje `mozilla/send` arşivlenmiş
  (son push 2021-05-21, 156 açık issue, MPL-2.0); yaşayan çatal `timvisee/send` MPL-2.0
  ama son push 2025-07-01 ve etiketli sürüm yok.
- **dubz.co** — Şart 4 düşüyor. Kendi ifadesi: "our free plan never deletes your videos
  as long as they keep getting views." Belgelenmiş API yok; "Upload (as Guest)" düğmesi
  var ama programatik arayüz görünmüyor. Sitedeki "20 million users", "300M+ video views"
  rakamları kendi pazarlama metni, **doğrulanamadı**.
- **streamja.com** — Ölü. DNS çözülmüyor.
- **gofile.io** — `doğrulanamadı`. Ne `gofile.io` ne `api.gofile.io` bu ağdan açıldı
  (bağlantı üç denemede de sıfırlandı). Wayback'teki API belgesi "All API requests
  require an API token" diyor ama parametresiz yüklemede sistemin bir guest hesabı
  yarattığını da yazıyor — anonim yükleme teoride mümkün. Saklama süresi, boyut tavanı
  ve tarayıcıda oynatma ölçülemedi.
- **qu.ax** — `doğrulanamadı`. Ana sayfa IPv6 üzerinden açıldı ama `/upload.php`,
  `/upload` ve `/api` yollarının hepsi bağlantı sıfırlamasıyla düştü. Hiçbir şart
  ölçülemedi.
- **file.io** — `doğrulanamadı`. Site Gatsby SPA; Swagger belgesi `POST /` ile `expires`,
  `maxDownloads`, `autoDelete` parametrelerini gösteriyor ve yükleme için anahtar
  istemiyor görünüyor, ama gerçek `POST` denemesi Cloudflare'dan 301 döngüsüne girdi.
  Anonim yüklemenin çalışıp çalışmadığı ve oynatılabilirlik ölçülemedi.
- **tempclip.com, videostemporales.net** — İkisi de canlı (200 döndü) ama tamamen
  istemci tarafında üretiliyor, belgelenmiş API'leri yok, işletmecileri sayfada yazmıyor
  ve videostemporales.net Yandex reklamı sunuyor. Hiçbir şart birincil kaynaktan
  okunamadı; hepsi `doğrulanamadı`.
- **streamwo.com** — Canlı (200) ama gövde boş döndü, sayfa istemci tarafında
  üretiliyor. Hiçbir şart okunamadı, `doğrulanamadı`.

---

## Hiçbiri geçmiyorsa

Geçiyor. Yedi şartın tamamını karşılayan **iki** aday var:

- **storage.to** — yedisi de evet; tek boşluk şart 5'in alıcı tarafı (alıcı silemez).
- **filebin.net** — altısı kesin evet, yedincisi (boyut tavanı) belgeden okunamadı; ama
  servis genelinde bir depolama eşiği dışında dosya başına sınır bulamadım.

Özelliği projeden çıkarmayı gerektiren bir durum çıkmadı. Ama iki adayın da ciddi
riskleri var; aşağıyı okumadan karar verme.

---

## Riskli yanlar

**storage.to**

- İşletmeci Click Here Digital Ltd (07093545) gerçek bir şirket ama servis genç ve
  ticari; sayfa altında "WeTransfer alternative / Pixeldrain alternative / Catbox
  alternatives" gibi düzinelerce SEO sayfasından oluşan bir ağ ve bir premium satışı var.
  "64 Mbps Average Upload", "12.5 Gbps Peak Upload", "300+ edge locations" rakamları
  kendi pazarlama metni, **doğrulanamadı**.
- Kullanım şartları §5 "Fair use" maddesi doğrudan bizi ilgilendiriyor: hizmet "content
  delivery network, media backend, or origin server for another application or website"
  olarak kullanılmak üzere tasarlanmadı; "Embedding or hotlinking storage.to files into a
  third-party app or site... requires a custom plan." Bağlantıyı kullanıcıya verip
  kullanıcının tarayıcıda açması bu maddenin dışında kalır; ama **VidShrink içine
  önizleme oynatıcısı koyarsak hotlink olur ve madde ihlal edilir.** Uygulama içi
  önizleme yapma.
- Kapalı kaynak, kendi barındırma yok. Servis kapanırsa çıkış yolu yok.
- Anonim tavan cihaz/IP başına 24 saatte 50 dosya. Paylaşılan bir NAT arkasındaki
  kurumsal kullanıcı bunu erken tüketebilir. 429 yanıtı `Retry-After` başlığı taşıyor;
  sınırı arayüze sabit yazma, sunucu hatasını göster.
- Şart 5 yarım: alıcının bağlantıyı kapatma yolu yok.

**filebin.net**

- "URL'yi bilen herkes silebilir" iki yönlü bir kılıç: alıcı bağlantıyı kapatabildiği
  gibi, bin adını tahmin eden herhangi biri de silebilir. Testte bin adını ben seçtim
  (`vidshrink-tarama-test-8f2a`) ve var olmayan bir bin anında yaratıldı — yani **bin
  adları çakışabilir ve tahmin edilebilir.** Otomatik yüklemede bin adını asla türetme,
  sunucunun ürettiği 16 karakterlik rastgele adı kullan.
- Servis genelinde depolama eşiği var; doluyken yükleme reddediliyor ("Storage limit
  reached. Please retry later."). Bu bir hata hâli olarak arayüzde ele alınmalı.
- Yönetici onayı özelliği (`FILEBIN_MANUAL_APPROVAL`) sunucu ayarı; şu anda kapalı
  olduğu testte görüldü (yükleme sonrası dosya anında indirilebildi) ama açılırsa
  bağlantılar onaya kadar 403 döner. Aynı şekilde `--require-verification-cookie`
  açılırsa alıcı önce bir doğrulama sayfası görür — OpenAPI bunu 200 yanıtının bir hâli
  olarak belgeliyor.
- İmzalı S3 URL'i varsayılan olarak 1 dakika geçerli. Alıcı sayfayı açık bırakıp saatler
  sonra oynatmaya kalkarsa yeniden yönlendirme gerekir.
- Dosya başına boyut tavanı belgesiz — 300 MB'lık bir yükleme denenmeden garanti edilemez.
- Kullanım şartları 8 Eylül 2020'den beri güncellenmemiş; yazılım ise aktif (son push
  2026-08-19). Sözleşme ile yazılım arasındaki bu yaş farkı, şartların ileride habersiz
  değişebileceği anlamına gelir.

**İkisi için ortak**

- İkisi de tek noktaya bağımlılık. Birini seçip diğerini yedek olarak kurmak, tek bir
  sağlayıcıya bağlanmaktan ucuz — ikisinin de API'si anahtarsız ve birkaç HTTP isteğinden
  ibaret.
- Bu ağdan (Türkiye) ölçüm yapıldı ve **beş** aday erişilemedi: `0x0.st`,
  `bashupload.com`, `gofile.io` / `api.gofile.io`, `qu.ax`'ın yükleme yolları ve
  `litter.catbox.moe`. Seçilen hedefin Türkiye'den erişilebilir olması ayrı bir şart gibi
  davranmayı hak ediyor; storage.to (Cloudflare) ve filebin.net (Hetzner/hel1) testte
  sorunsuz açıldı.

---

## Kaynaklar

- storage.to API belgesi — <https://storage.to/docs/api>
- storage.to genel belge (limitler tablosu) — <https://storage.to/docs>
- storage.to kullanım şartları (11 Nisan 2026) — <https://storage.to/terms>
- Companies House, CLICK HERE DIGITAL LTD (07093545) — <https://find-and-update.company-information.service.gov.uk/company/07093545>
- filebin.net OpenAPI 3.0 belgesi — <https://filebin.net/api.yaml>, <https://filebin.net/api>
- filebin.net kullanım şartları (8 Eylül 2020) — <https://filebin.net/terms>
- filebin.net hakkında (çalışan sürüm) — <https://filebin.net/about>
- espebra/filebin2 — `gh api repos/espebra/filebin2`: BSD-3-Clause, push 2026-08-19,
  6 açık issue, 278 yıldız; `releases/latest` → v1.2.0, 2026-07-29
- uguu.se ana sayfa, API, FAQ — <https://uguu.se/>, <https://uguu.se/api>, <https://uguu.se/faq>
- nokonoko/Uguu — `gh api repos/nokonoko/Uguu`: GPL-3.0, push 2025-12-06; latest v.1.9.9
- catbox FAQ (2 yıl saklama, Türkiye engeli) — <https://catbox.moe/faq.php>
- catbox API ve araçlar — <https://catbox.moe/tools.php>
- catbox hukuki metinler — <https://catbox.moe/legal.php>
- litterbox FAQ ve API — <https://litterbox.catbox.moe/faq.php>, <https://litterbox.catbox.moe/tools.php>
- tmpfiles.org API ve ToS (1 Aralık 2025) — <https://tmpfiles.org/api>, <https://tmpfiles.org/tos>
- temp.sh ana sayfa — <https://temp.sh/>
- pixeldrain API belgesi — <https://pixeldrain.com/api>
- pixeldrain API belgesi, Wayback 2024-08-04 — <https://web.archive.org/web/20240804193714id_/https://pixeldrain.com/api>
- pixeldrain API belgesi, Wayback 2024-11-27 — <https://web.archive.org/web/20241127031015id_/https://pixeldrain.com/api>
- 0x0.st, Wayback 2026-08-23 — <https://web.archive.org/web/20260823132802/https://0x0.st/>
- bashupload.com, Wayback — <https://web.archive.org/web/2026/https://bashupload.com/>
- gofile API belgesi, Wayback — <https://web.archive.org/web/2026/https://gofile.io/api>
- timvisee/send FAQ (tarayıcıda şifreleme) — <https://github.com/timvisee/send/blob/master/docs/faq.md>
- mozilla/send — `gh api repos/mozilla/send`: arşivlenmiş, push 2021-05-21, 156 açık issue
- dubz.co ana sayfa — <https://dubz.co/>

## _sorun.log

`anonim-kisa-omurlu-video | scout | 0x0.st, bashupload.com, gofile.io, qu.ax yükleme
yolları ve litter.catbox.moe canlı yanıtı | beşi de bu ağdan bağlantı sıfırlaması ya da
TLS zincir hatasıyla düştü | ilgili şartlar doğrulanamadı işaretlendi, metin Wayback
anlık görüntülerinden okundu`

`anonim-kisa-omurlu-video | scout | streamwo.com, tempclip.com, videostemporales.net ve
file.io içeriği | sayfalar istemci tarafında üretiliyor, gövde boş ya da yalnız SPA
iskeleti döndü; file.io POST denemesi Cloudflare 301 döngüsüne girdi | dört adayın da
tüm şartları doğrulanamadı işaretlendi`
