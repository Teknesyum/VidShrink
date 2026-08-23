Tema: sıkıştırılan videoyu tek tıkla yükleyip bağlantı paylaşma · soru: büyük şirketin belgelenmiş yükleme API'si hangisi

# Yükleme platformları — YouTube, Drive, Dropbox, OneDrive, Vimeo, Cloudflare Stream, Bunny Stream

Devam belgesi: `docs/taramalar/streamable.md` (Streamable elendi — belgelenmiş yükleme API'si yok,
kullanım şartları otomatik göndermeyi açıkça yasaklıyor).

Bütün rakamlar 2026-08-24'te birincil kaynaktan çekildi; her sayının yanında belgenin kendi
"last updated" tarihi var. Kaynak listesi en altta. Doğrulayamadığım her şey `doğrulanamadı`
etiketli.

Ölçüt (kullanıcının koyduğu): **büyük ve köklü şirket**, **gerçek ve belgelenmiş yükleme API'si**,
**tek tıkla yükle ve bağlantı al**.

## Kısa cevap

**Google Drive (`drive.file` kapsamı).** Tek eleyici olmayan aday bu. Gerekçe §Öneri'de.

Eleyiciler:

- **YouTube** — kota tek başına eleyici. `videos.insert` **proje başına günde 100 çağrı**. Bu kota
  VidShrink'in tek OAuth istemcisine ait, kullanıcı başına değil: bütün kullanıcılar aynı 100'ü
  paylaşır. Üstüne, denetimden geçmemiş projelerden yüklenen her video **zorla gizli** kalıyor —
  yani bağlantı zaten paylaşılamaz.
- **Vimeo** — ücretsiz hesap için yükleme erişimi **elle inceleme**, "up to five business days".
  Ücretsiz plan **hesap ömrü boyunca toplam 1 GB**.
- **Bunny Stream** — teknik olarak en temiz API'lerden biri ama şirket ölçütünü karşılamıyor
  (BunnyWay d.o.o.); ücretsiz katman yok.
- **Cloudflare Stream** — büyük şirket, gerçek oynatıcı, ama **ücretsiz katman yok** ve kullanıcının
  hesap kimliği + API jetonu yapıştırması gerekiyor. "Tek tık" değil.

---

## 1. YouTube (Data API v3)

**1. Yükleme API'si var mı, belgeli mi?**
Var. `POST https://www.googleapis.com/upload/youtube/v3/videos`.
Belge: `developers.google.com/youtube/v3/docs/videos/insert`, **son güncelleme 2026-07-08 UTC**.
Azami dosya boyutu 256 GB, kabul edilen MIME `video/*`.

**2. Kimlik doğrulama nasıl?**
OAuth 2. Kapsam `https://www.googleapis.com/auth/youtube.upload`. Masaüstü için loopback yönlendirme
(`http://127.0.0.1:port`) + PKCE. Parola saklanmaz, jeton iptal edilebilir. Bu tarafı sorunsuz.

**3. Uygulama kaydı / onay?**
Kayıt zorunlu (Google Cloud projesi). İki ayrı onay katmanı var ve ikisi de bağlayıcı:

- `youtube.upload` **sensitive** kapsam. Sensitive kapsam isteyen, yayımlanmış ama doğrulanmamış
  uygulamalarda "unverified app warnings (Danger UI)" gösteriliyor ve **"a hard cap of 100 total
  users applies"** (OAuth app state overview, 2026-05-22).
- Ayrıca API projesinin ayrı bir **compliance audit**'i var: *"All videos uploaded via the
  videos.insert endpoint from unverified API projects created after 28 July 2020 will be restricted
  to private viewing mode. To lift this restriction, each API project must undergo an audit"*
  (videos/insert, 2026-07-08). Denetim süresi belgelenmemiş — `doğrulanamadı`.

**4. Ücretsiz katmandan yüklenebiliyor mu? — kota hesabı**
Depolama tarafı ücretsiz ve sınırsız sayılır, ama API kotası eleyici.

Belge (`determine_quota_cost`, **2026-06-01 UTC**): *"Projects that enable the YouTube Data API have
a default quota allocation of 100 search.list calls, 100 videos.insert calls, and 10,000 units per
day combined for all other endpoints."* ve *"The search.list and videos.insert methods have their
own quota buckets. Each of these methods has a default daily limit of 100 per day."*

Hesap — kota **proje başına**, kullanıcı başına değil:

| VidShrink kullanıcı sayısı | Kişi başı günlük yükleme hakkı |
|---|---|
| 10 | 10 |
| 50 | 2 |
| 100 | 1 |
| 500 | 0,2 (beş günde bir) |

Yani uygulama 100 aktif kullanıcıya ulaştığında **kişi başına günde bir video** kalıyor; 101. yükleme
`quotaExceeded` alıyor ve hata hangi kullanıcıya denk gelirse ona düşüyor. Kota artışı ancak
compliance audit ile isteniyor (Developer Policies, 2026-06-24: *"If your API Client reaches the
quota limit for a service, you can apply for a quota extension by completing an API Compliance
Audit."*). **Tek bakımcılı bir projede bu, tek başına eleyici.**

Not: aynı kota sayfasının üstündeki otomatik özet kutusu hâlâ *"videos.insert have the highest cost
of 1600 points"* diyor, gövde metni ve tablo ise "100/gün, çağrı başına 1" diyor. Belge kendi içinde
tutarsız. Ben gövde metnini esas aldım; **eski 1600 birim modeli mi hâlâ bazı projelerde geçerli,
`doğrulanamadı`.** Her iki modelde de sonuç aynı yönde: eski modelde 10.000 ÷ 1600 = **günde 6
yükleme**, yenisinde günde 100 — ikisi de proje geneli.

**5. Paylaşılabilir bağlantı nasıl alınıyor?**
`videos.insert` yanıtı video kimliğini döndürüyor; bağlantı `youtube.com/watch?v=<id>`. Gerçek
oynatıcı, uyarlanabilir kalite, gömme — bu tarafı adaylar arasında en iyisi.

**6. Gizlilik**
`status.privacyStatus` = `public` | `unlisted` | `private`. `unlisted` arama sonuçlarına düşmüyor.
**Ama:** denetimden geçmemiş projede `unlisted` istesen de video `private`'a zorlanıyor (§3). Yani
denetim tamamlanana kadar özellik hiç çalışmıyor — "yapılabilir ama şu an yapılmıyor" değil,
"yapılamıyor".

**7. Silme ve süre**
`videos.delete` var (50 birim, genel kovadan). Süreli bağlantı yok; kullanıcı yayını kapatmak için
gizliliği `private`'a çekmeli ya da silmeli. İkisi de API'den yapılabiliyor.

**8. Şartlar üçüncü taraf istemciye ne diyor?**
Yasak değil, koşullu izinli. Developer Policies (2026-06-24): *"Users must have final control over
the data that will be published to YouTube"*, yüklemeler *"must be clearly initiated by the user"*,
ve *"You must not automate or trigger views, uploads, comments, likes, dislikes, or other actions
without the user's prior specific and express consent."* VidShrink'in "kullanıcı düğmeye basar,
video gider" modeli bu maddeye uyar. Sorun şartlarda değil, kotada ve denetimde.

**Sonuç: elendi.** Kota proje geneli, denetim öncesi bağlantı paylaşılamaz.

---

## 2. Google Drive (API v3)

**1. Yükleme API'si var mı, belgeli mi?**
Var. Üç yükleme tipi belgeli: `uploadType=media` (≤5 MB), `multipart` (≤5 MB, meta veriyle),
`resumable` (>5 MB, kesintide devam). Uç nokta
`POST https://www.googleapis.com/upload/drive/v3/files?uploadType=resumable`.
Belge: `developers.google.com/drive/api/guides/manage-uploads`, **son güncelleme 2026-08-19 UTC**.
Dosya başına tavan: *"All other files: Up to 5 TB"* (Drive yardım, dosya boyutu sayfası).

**2. Kimlik doğrulama nasıl?**
OAuth 2, kapsam `https://www.googleapis.com/auth/drive.file`. Loopback yönlendirme + PKCE;
`client_secret` alanı **installed app akışında "Optional"** (native-app belgesi, 2026-08-07). Bu,
AGPL bir kaynak kodda gizli tutulamayacak bir sırrı hiç taşımamak demek — masaüstü açık kaynak proje
için önemli bir avantaj. Jeton iptal edilebilir; parola hiç görülmüyor.

`drive.file` kapsamı önemli: *"Create new Drive files, or modify existing files, that you open with
an app"* — uygulama kullanıcının **diğer** dosyalarını göremiyor. Kullanıcıya anlatması kolay,
yetki daraltması gerçek.

**3. Uygulama kaydı / onay?**
Kayıt gerekli (Google Cloud projesi, OAuth istemcisi). **Elle inceleme kuyruğu yok.**
`drive.file` **non-sensitive** olarak sınıflandırılmış (`api-specific-auth`, 2026-07-22):
*"Non-sensitive: These scopes provide the smallest scope of authorization and only require basic
OAuth App Verification."*

Yayımlanmış-doğrulanmamış durumun sonucu (OAuth app state overview, 2026-05-22): *"Any Google user
can access... Because the app has not completed brand verification, the app's name and logo are not
displayed on the consent screen. Additionally, **for apps requesting sensitive or restricted
scopes**, unverified app warnings (Danger UI) will be displayed to users, and a hard cap of 100 total
users applies."*

Kritik ayrım: 100 kullanıcı tavanı ve tehlike ekranı **yalnız sensitive/restricted kapsamlar için**.
`drive.file` non-sensitive olduğu için VidShrink kullanıcı tavanına ve tehlike ekranına takılmaz;
tek bedel, onay ekranında uygulama adının ve simgesinin görünmemesi. Marka doğrulaması istenirse
sensitive kapsam incelemesi *"typically takes 3-5 business days"* (sensitive scope verification,
2026-08-19) — ama bu yol zorunlu değil.

Dikkat: geliştirme sırasında "Testing" durumunda kalınırsa tavan **100 test kullanıcısı** ve
yenileme jetonları **7 günde** doluyor. Yayına alma adımı unutulursa özellik bir hafta sonra herkeste
kırılır. (Kaynak: OAuth app state overview tablosu + Google destek dizisi özetleri; 7 gün rakamı
arama sonucu özetinden geldi, tam metin çekilemedi — `kısmen doğrulandı`.)

**4. Ücretsiz katmandan yüklenebiliyor mu?**
Evet. Her Google hesabı **15 GB**, Gmail + Drive + Photos arasında **paylaşımlı**. Yani kullanıcının
gerçekte ne kadarı boşta olduğu bilinemez — arayüzde "15 GB'ın var" yazmak yanlış olur, kotayı
`about.get` ile sunucudan sorup göstermek gerekir. API tarafında ayrı bir günlük yükleme adedi
sınırı belgelenmemiş; genel Drive API kotası çok yüksek (`doğrulanamadı`, birim rakamı çekilmedi).

**5. Paylaşılabilir bağlantı nasıl alınıyor?**
İki çağrı:
`permissions.create` → `{type: "anyone", role: "reader"}`; sonra dosyanın `webViewLink` alanı.
Bağlantı Drive'ın kendi **tarayıcı içi oynatıcısını** açıyor — indirme değil, izleme. Uyarlanabilir
kalite/HLS yok, tek dosya oynatılıyor.

**6. Gizlilik**
`type: "anyone"` = "bağlantısı olan herkes". Bu **listelenmemiş** anlamına gelir: Drive'da dizin yok,
arama motoruna girmesi için bağlantının bir yerde yayımlanması gerekir. Google'ın böyle dosyaları
indekslemediğine dair birincil bir taahhüt bulamadım — **`doğrulanamadı`**; kullanıcıya "gizli"
değil "bağlantısı olan herkes izler" diye anlatılmalı (Streamable raporundaki aynı uyarı).

**7. Silme ve süre**
- Silme: `files.delete` var. Yayını kapatma: `permissions.delete` ile "anyone" iznini kaldırmak
  yeterli, dosya kullanıcının Drive'ında kalır. **"Yayını kapat" düğmesi tam olarak bu.**
- Süreli bağlantı: **yok.** Permission belgesi net: `expirationTime` *"can only be set on user and
  group permissions. The time must be in the future. The time cannot be more than one year in the
  future."* Yani `type: anyone` bağlantısına süre konamıyor. Süre isteniyorsa uygulamanın kendisi
  zamanlayıp izni geri alması gerekir — yerel bir zamanlayıcı, uygulama kapalıyken çalışmaz.
  Bu Drive'ın en belirgin eksiği.

**8. Şartlar üçüncü taraf istemciye ne diyor?**
Drive API belgelerinde ve kapsam sayfasında otomatik yüklemeyi yasaklayan bir madde bulamadım;
üçüncü taraf uygulama modeli resmî ve teşvik ediliyor. Bağlayıcı metin Google API Services User Data
Policy: *"All apps that integrate with Google APIs are required to comply with Google's API Services
User Data Policy regardless of whether they have been verified."* **Şartların tam metnini madde madde
taramadım — `kısmen incelendi`.**

---

## 3. Dropbox (API v2)

**1. Yükleme API'si var mı, belgeli mi?**
Var. `/files/upload` ve büyük dosya için `/files/upload_session/{start,append_v2,finish}`.
Belge sayfası (`dropbox.com/developers/documentation/http/documentation`) tamamen istemci tarafında
üretiliyor — `curl` ile boş geliyor, otomatik doğrulanamıyor. Doğrulanabilir birincil kaynak:
**`dropbox/dropbox-api-spec`** deposu (MIT, son push 2026-08-21, 45 yıldız, 6 açık issue — `gh api`,
2026-08-24). Performans kılavuzu (`developers.dropbox.com/dbx-performance-guide`) net:
*"The /files/upload endpoint is designed to work with files that are under 150 MBs."*,
*"Files over 150 MBs in size should be uploaded in chunks"*, *"Consider uploading chunks in multiples
of 4 MBs."*

API üzerinden azami dosya boyutu 350 GB iddiası üçüncü taraftan geldi (cloudwards.net özeti) —
**şüpheli, `doğrulanamadı`.**

**2. Kimlik doğrulama nasıl?**
OAuth 2, kısa ömürlü erişim jetonu + yenileme jetonu. Parola saklanmıyor. PKCE destekli. Sorunsuz.

**3. Uygulama kaydı / onay?**
Kayıt gerekli ve **burada gerçek bir tavan var.** Geliştirici kılavuzu: *"once your app links 50
Dropbox users, you will have two weeks to apply for and receive production status approval before
your app's ability to link additional Dropbox users will be frozen"*; geliştirme durumunda toplam
tavan 500 kullanıcı. Başvuru 50 kullanıcıya ulaşmadan **incelenmiyor** — yani onay öne alınamıyor.
İnceleme süresi belgelenmemiş — `doğrulanamadı`.

VidShrink için anlamı: özellik 50 kullanıcıya kadar çalışır, sonra iki haftalık bir sayaç başlar ve
inceleme geçilmezse **yeni kullanıcılar bağlanamaz**. Yayınlanmamış bir projede bu, sorunun
kullanıcı tabanı büyüdüğü anda patlaması demek.

**4. Ücretsiz katmandan yüklenebiliyor mu?**
Evet. Basic (ücretsiz) depolama 2 GB — **`doğrulanamadı`, birincil sayfadan çekilmedi.**
Asıl sınır bant genişliği: Basic hesapta paylaşılan bağlantılar için **günde 20 GB ve 100.000
indirme**, ücretli planlarda 1 TB/gün (help.dropbox.com "Why has my sharing activity been
interrupted?" sayfasının arama özeti — **tam metin çekilemedi, `kısmen doğrulandı`**). Aşılırsa
paylaşım etkinliği 24 saate kadar askıya alınıyor: yani popüler olan bir video kullanıcının bütün
paylaşım yeteneğini kilitliyor.

**5. Paylaşılabilir bağlantı nasıl alınıyor?**
`sharing/create_shared_link_with_settings` tek çağrıda bağlantı üretiyor. Bağlantı bir **önizleme
sayfası** açıyor; Dropbox'ın video oynatıcısı orada. `?raw=1` / `?dl=1` değiştiricileriyle doğrudan
içerik/indirme alınabildiği biliniyor ama birincil belgeden doğrulayamadım — **`doğrulanamadı`.**

**6. Gizlilik**
`SharedLinkSettings` (resmî spec dosyası `sharing.stone`): `audience`, `access`, `require_password`,
`link_password`, `allow_download`, `expires`. Bağlantı tahmin edilemez; dizin yok. Parola korumasını
**API'den kurabilmek** Drive ve OneDrive'a göre üstünlük.

**7. Silme ve süre**
- Silme: `files/delete_v2`; bağlantıyı kapatma `sharing/revoke_shared_link`.
- Süre: `expires` alanı spec'te var — **ama bu özellik plan bağımlı.** Yardım merkezi: süre koyma
  Professional, Essentials, Standard, Advanced, Business, Business Plus ve Enterprise planlarında;
  **ücretsiz Basic'te yok** (arama özeti, `kısmen doğrulandı`). Yani ücretsiz kullanıcıda alan
  gönderilirse hata döner — arayüzün bunu plan sorup değil, **hatayı yakalayıp** göstermesi gerekir.

**8. Şartlar üçüncü taraf istemciye ne diyor?**
Yasak yok; Dropbox'ın iş modelinin bir parçası üçüncü taraf entegrasyonlar. Koşullar: geliştirici
marka kılavuzuna uymak, Dropbox onayı ima etmemek, *"use the least privileged permission it can"*
ve yalnız API v2 uç noktaları kullanmak.

---

## 4. Microsoft OneDrive (Microsoft Graph)

**1. Yükleme API'si var mı, belgeli mi?**
Var. `POST /me/drive/items/{parentItemId}:/{fileName}:/createUploadSession`, ardından `uploadUrl`'e
sıralı `PUT` parçaları. Belge: `learn.microsoft.com/graph/api/driveitem-createuploadsession`,
**güncelleme 2026-08-11**. Parça kuralları belgeli ve katı: her istek < 60 MiB, parça boyutu
**320 KiB'nin katı olmalı**, parçalar sırayla gitmeli, önerilen parça 5–10 MiB. Kesinti sonrası
`nextExpectedRanges` ile devam ediliyor. Bu, adaylar arasında **en ayrıntılı belgelenmiş devam
edebilir yükleme protokolü**.

Kişisel hesapta dosya başına tavan 250 GB iddiası — **`doğrulanamadı`.**

**2. Kimlik doğrulama nasıl?**
OAuth 2, delegated `Files.ReadWrite` (kişisel Microsoft hesabı için de en az yetkili izin olarak
listeli). Jeton iptal edilebilir, parola saklanmıyor.

Belgede işe yarar bir ayrıntı: yükleme `PUT`'larında `Authorization` başlığı **gönderilmemeli** —
*"If you include the Authorization header when issuing the PUT call, it might result in an HTTP 401
Unauthorized response."* `uploadUrl` ön yetkilendirilmiş. Bu, `HttpClient` varsayılanlarıyla kolayca
yapılan bir hata.

**3. Uygulama kaydı / onay?**
Kayıt gerekli (Entra uygulama kaydı, "personal Microsoft accounts" desteği açık). **Elle inceleme
kuyruğu belgelenmemiş — kişisel hesaplar için onay süreci yok.** Bu, Dropbox'ın 50 kullanıcı
tavanına ve Vimeo'nun 5 iş gününe göre belirgin avantaj.

Publisher verification isteğe bağlı ve **VidShrink için pratikte erişilemez**: gereksinimler arasında
doğrulanmış bir Microsoft AI Cloud Partner Program hesabı ve *"The app that's to be publisher
verified must be registered by using a Microsoft Entra work or school account. Apps that are
registered by using a Microsoft account can't be publisher verified."* (2026-06-15) Tek bakımcılı bir
projenin kurumsal kiracısı yoksa rozet alınamaz.

Buna bağlı bir risk: 2020-11'den sonra kayıtlı, doğrulanmamış çok kiracılı uygulamalarda
risk-based step-up consent açıksa kullanıcılar onay veremeyebiliyor — **ama bu politika kurumsal
kiracılar için**; kişisel Microsoft hesabında böyle bir engel belgelenmemiş (`kısmen doğrulandı`).
VidShrink kişisel hesapları hedefliyorsa etkilenmez; kurumsal hesaplı kullanıcılar takılabilir.

**4. Ücretsiz katmandan yüklenebiliyor mu?**
Evet, ama en dar alan burada: kişisel hesapta **5 GB** ücretsiz (microsoft.com ürün sayfası özeti;
**tam metin çekilemedi, `kısmen doğrulandı`**). Sıkıştırılmış video için 5 GB birkaç dosya demek.
`createUploadSession` çağrısında `fileSize` verilirse kota yetmediğinde oturum hiç açılmıyor ve
**507 Insufficient Storage** dönüyor — hata hâli temiz ve önceden yakalanabilir.

**5. Paylaşılabilir bağlantı nasıl alınıyor?**
Tek çağrı: `POST /me/drive/items/{itemId}/createLink` → `{"type": "view", "scope": "anonymous"}`,
yanıtta `link.webUrl`. Bağlantı OneDrive'ın web önizlemesini açıyor.

Ayrıca `type: "embed"` **yalnız OneDrive personal'da** çalışıyor ve yanıtta doğrudan
`webHtml` = hazır `<iframe>` dönüyor. Adaylar arasında **gömülebilir oynatıcıyı tek API çağrısıyla
veren tek dosya-depolama seçeneği** bu.

**6. Gizlilik**
`scope: anonymous` = "Anyone with the link has access, without needing to sign in." Dizin yok.
`password` alanı **OneDrive Personal'a özel** ve API'den kurulabiliyor. Bu, ücretsiz katmanda parola
koruması demek — Dropbox'ta parola ücretli plana bağlı.

**7. Silme ve süre**
- Silme: `DELETE /me/drive/items/{itemId}`. Bağlantı kapatma: `permissions` üzerinden ilgili izni
  silmek. Belge: *"Links are visible in the sharing permissions for the item and can be removed by
  an owner of the item."*
- Süre: `expirationDateTime` istek gövdesinde belgeli. **Ama aynı sayfanın Remarks bölümü
  çelişiyor:** *"Links created using this action don't expire unless a default expiration policy is
  enforced for the organization."* İki cümle aynı belgede. Kişisel hesapta `expirationDateTime`'ın
  gerçekten uygulanıp uygulanmadığı **`doğrulanamadı`** — hesapla denenmeli. Süreye bel bağlanacaksa
  ilk iş bu.

**8. Şartlar üçüncü taraf istemciye ne diyor?**
Otomatik yükleme yasağı bulamadım; delegated izinle kullanıcı adına dosya yazmak Graph'ın ana
kullanım senaryosu. Microsoft identity platform Terms of Use'un tam metnini taramadım —
`kısmen incelendi`.

---

## 5. Vimeo

Streamable raporunda başlanmıştı; burada tamamlanıyor.

**1. Yükleme API'si var mı, belgeli mi?** Var: `POST /me/videos`, `upload.approach` = `tus` | `pull`
| `post`. **Belge sayfaları (`developer.vimeo.com/api/...`) tamamen JavaScript ile üretiliyor;
`curl` ile 18 KB'lık boş iskelet dönüyor.** Yani API belgesi otomatik doğrulanamıyor — tarayıcısız
bir ortamda birincil kaynak okunamıyor. Bu tek başına bir bakım riski işareti. tus/PATCH ayrıntıları
arama sonucu özetlerinden geldi — **`doğrulanamadı`.**

**2. Kimlik doğrulama nasıl?** OAuth 2, `upload` ve `edit` kapsamları. Parola saklanmıyor.

**3. Uygulama kaydı / onay?** Kayıt gerekli, üstüne **ayrı bir yükleme erişimi başvurusu** var.
Yardım merkezi (server-rendered, doğrulandı): *"Developers who have not purchased a Vimeo paid plan,
including free users, who want to upload videos using the Vimeo API must request upload access
permission."*, *"This is not required for paid plans, as those are automatically approved for upload
access."*, *"Our developer support team manually reviews each upload access request, which can take
up to five business days to process."*

Kullanıcının ölçütü "çok kolay olmalı". Ücretsiz kullanıcıyı beş iş günü bekleten bir yol bu ölçütü
karşılamıyor.

**4. Ücretsiz katmandan yüklenebiliyor mu?** Onay alınırsa evet, ama alan çok dar:
*"When you create a Free account, you may upload or create up to 1 GB of content for the account's
lifetime, unless you upgrade."* — **hesap ömrü boyunca 1 GB**. Video sıkıştırıcı için bu, birkaç
dosya sonra biten bir kota.

**5. Paylaşılabilir bağlantı nasıl alınıyor?** `link` alanı; gerçek Vimeo oynatıcısı, uyarlanabilir
kalite, gömme. Bu tarafı YouTube ve Cloudflare ile aynı ligde.

**6. Gizlilik** `privacy.view` API'den ayarlanabiliyor. Hangi gizlilik seviyesinin hangi planda
açık olduğu (özellikle "unlisted"/parola korumalı) **bu taramada da doğrulanamadı** — Vimeo belge
sayfaları okunamadığı için. Streamable raporundaki aynı boşluk duruyor.

**7. Silme ve süre** Silme API'den yapılabiliyor (`DELETE /videos/{id}`). Süreli bağlantı
belgelenmemiş — `doğrulanamadı`.

**8. Şartlar** Üçüncü taraf istemci modeli resmî, yasak yok. Bedeli, uygulamanın Vimeo'ya kayıtlı
olması ve kötüye kullanımda **uygulamanın** erişiminin kesilebilmesi — sorumluluk zincirinde
VidShrink de görünür.

Ek: resmî `vimeo/vimeo.php` istemcisi PHP (Apache-2.0, son sürüm 4.0.1 / 2025-10-23, 72 açık issue —
`gh api`, 2026-08-24). **.NET için resmî istemci yok**; her şey elle yazılır.

---

## 6. Cloudflare Stream

**1. Yükleme API'si var mı, belgeli mi?** Var ve iyi belgeli. Doğrudan yükleme, tek seferlik yükleme
URL'i (direct creator upload) ve büyük dosyalar için **tus**. Belge:
`developers.cloudflare.com/stream/uploading-videos/direct-creator-uploads/`. Eşik net:
*"If your end user's video is under 200 MB and their connection is reliable, we recommend using this
method."*, büyüğü için *"You must use the tus protocol."*

**2. Kimlik doğrulama nasıl?** `Authorization: Bearer <API_TOKEN>` — Cloudflare API jetonu. OAuth
akışı **yok**: kullanıcı Cloudflare panosuna gidip jeton üretip yapıştırmak zorunda. Artısı: jeton
kapsamı daraltılabiliyor (Stream:Edit) ve tek tıkla iptal edilebiliyor. Eksisi: kullanıcı arayüzünde
"hesap kimliği" + "API jetonu" isteyen iki alan demek. **Tek tık değil.**

**3. Uygulama kaydı / onay?** Yok. Uygulama kaydı, onay kuyruğu, inceleme — hiçbiri yok. Bu, adaylar
arasında en düşük idari yük.

**4. Ücretsiz katmandan yüklenebiliyor mu? Hayır.** Fiyat sayfası (**son güncelleme 2026-04-21**):
depolama *"prepaid pricing dimension purchased in increments of $5 per 1,000 minutes stored,
regardless of file size"*; teslim *"$1 per 1,000 minutes delivered"*. Yükleme ve kodlama ücretsiz,
bant genişliği teslim ücretine dâhil. **Stream için ücretsiz katman yok.** Depolama biterse
*"...cannot upload new videos or start new live streams until you purchase more storage or delete
videos."*

**5. Paylaşılabilir bağlantı nasıl alınıyor?** Yükleme yanıtındaki `preview` alanı doğrudan izlenebilir
bağlantı: `https://customer-<CODE>.cloudflarestream.com/<VIDEO_UID>/watch`. Gömme için `/iframe`.
Gerçek oynatıcı, HLS/DASH. Video tarafında Drive/OneDrive/Dropbox'tan sınıf olarak üstün.

**6. Gizlilik** Varsayılan **açık**: *"videos on Stream can be viewed by anyone with just a video
id."* `requireSignedURLs` açılırsa *"it can no longer be accessed publicly with only the video id.
Instead, the user will need a signed url token."* Ayrıca Allowed Origins ile gömme alanı
kısıtlanabiliyor.

**7. Silme ve süre** Silme API'den. Süreli bağlantı **var ve gerçek**: imzalı jetonun `exp` alanı —
*"A unix epoch timestamp after which the token will stop working. Cannot be greater than 24 hours in
the future from when the token is signed."* Yani en fazla 24 saatlik bağlantı; daha uzunu için
uygulamanın jetonu yenilemesi gerekir, bu da masaüstü istemcide çalışmaz. Uzun ömürlü paylaşım için
`requireSignedURLs` kapalı bırakılır — o zaman süre yok.

**8. Şartlar** Kendi hesabına kendi jetonunla yüklemek; üçüncü taraf istemci yasağı yok. Cloudflare
şartlarının tam metnini taramadım — `kısmen incelendi`.

---

## 7. Bunny Stream

**1. Yükleme API'si var mı, belgeli mi?** Var, sade ve okunabilir:
`PUT https://video.bunnycdn.com/library/{libraryId}/videos/{videoId}`, gövde `application/octet-stream`.
Önce `POST .../videos` ile kayıt oluşturuluyor, sonra bayt gönderiliyor. Yanıt kodları belgeli
(400 "Video already uploaded", 401/403, 404, 500). Dosya boyutu tavanı belgede yok — `doğrulanamadı`.
Belge alanı 2026-08-24'te `docs.bunny.net` → `bunny.net/docs` 302 yönlendirmesiyle taşınmış durumda.

**2. Kimlik doğrulama nasıl?** `AccessKey` başlığı — statik, uzun ömürlü anahtar. OAuth yok. Anahtar
kütüphane (library) düzeyinde üretilip döndürülebiliyor, yani kapsamı dar ve iptal edilebilir; parola
değil. Kabul edilebilir ama OAuth'un gerisinde.

**3. Uygulama kaydı / onay?** Yok.

**4. Ücretsiz katmandan yüklenebiliyor mu?** Kalıcı ücretsiz katman yok; 14 günlük deneme var
(kredi kartısız). Fiyat: depolama *"From $0.01/GB"*, akış *"From $0.005/GB"*; kodlama, güvenlik ve
oynatıcı ücrete dâhil. **Fiyatlar ürün sayfasından, "from" ifadesiyle — gerçek fatura kademeye göre
değişir, `kısmen doğrulandı`.**

**5. Paylaşılabilir bağlantı nasıl alınıyor?** Direct Play URL ve gömülebilir iframe oynatıcı var.
Tam biçimi belgeden doğrulamadım — `doğrulanamadı`.

**6-7. Gizlilik, silme, süre** Belgelenmiş jetonlu kimlik doğrulama ve süreli bağlantı özellikleri
olduğu biliniyor; bu taramada birincil kaynaktan doğrulanmadı — `doğrulanamadı`.

**8. Şartlar** Yasak yok.

**Eleyici olan şey teknik değil:** işletmeci **BunnyWay d.o.o.** (Slovenya). Kullanıcının ölçütü
*"yeterince büyük bir şirket kolay kolay politika değiştirmez ve böyle küçük projelere uğraşmaz"*.
Bunny bu ölçütün karşıladığı tarafta değil — ürün iyi, şirket küçük. Streamable'da yaşanan riskin
(API'nin sessizce kapatılması) tekrarına açık.

---

## Karşılaştırma

| | YouTube | **Google Drive** | Dropbox | OneDrive | Vimeo | CF Stream | Bunny |
|---|---|---|---|---|---|---|---|
| Şirket ölçeği | çok büyük | çok büyük | büyük | çok büyük | orta-büyük | çok büyük | **küçük** |
| Belgelenmiş yükleme API'si | var | var | var | var | var (belge JS, okunamadı) | var | var |
| Kimlik doğrulama | OAuth 2 | **OAuth 2 + PKCE, secret opsiyonel** | OAuth 2 | OAuth 2 | OAuth 2 | statik jeton | statik AccessKey |
| Elle onay kuyruğu | **denetim (audit)** | **yok** | **50 kullanıcıdan sonra var** | yok | **≤5 iş günü** | yok | yok |
| Kullanıcı tavanı (doğrulanmamış) | 100 (sensitive kapsam) | **yok** (non-sensitive) | 50 → 500 | yok | — | — | — |
| Ücretsiz katman | var | **15 GB (paylaşımlı)** | ~2 GB `doğrulanamadı` | 5 GB | 1 GB ömür boyu | **yok** | **yok** |
| Günlük yükleme sınırı | **100/gün, proje geneli** | belgelenmemiş | belgelenmemiş | belgelenmemiş | — | — | — |
| Tarayıcıda oynatıcı | **en iyi** | var (Drive oynatıcı) | var (önizleme) | var + `embed` iframe | **en iyi** | **en iyi** | var |
| Listelenmemiş bağlantı | evet (`unlisted`) | evet (`anyone`) | evet | evet (`anonymous`) | evet | evet | evet |
| API'den parola koruması | hayır | hayır | **evet** (ücretli plan) | **evet** (personal) | `doğrulanamadı` | imzalı URL | `doğrulanamadı` |
| API'den silme / yayını kapatma | evet | **evet** (`permissions.delete`) | evet | evet | evet | evet | evet |
| Süreli bağlantı | hayır | **hayır** | evet (ücretli plan) | belirsiz (belge çelişkili) | `doğrulanamadı` | evet (≤24 saat) | `doğrulanamadı` |
| .NET resmî istemcisi | Apache-2.0, aktif | **Apache-2.0, aktif** | MIT, aktif | aktif (NOASSERTION) | **yok** | yok | yok |
| Otomatik yükleme şartlarda | izinli, koşullu | yasak bulunamadı | izinli | yasak bulunamadı | izinli | izinli | izinli |

---

## Öneri

**Google Drive, `drive.file` kapsamıyla. İkinci bağlayıcı olarak OneDrive.**

Kullanıcının üç ölçütüne göre gerekçe:

**Büyük şirket.** Google ve Microsoft, listedeki en büyük ikisi. Ama asıl mesele büyüklük değil,
**politikanın ne kadar bağlayıcı belgelendiği**: Drive'ın kapsam sınıflandırması, doğrulama kuralları
ve kota davranışı tarih damgalı belgelerde yazılı ve sürüm geçmişi tutuluyor. Streamable'da eksik
olan tam olarak buydu — belge ile canlı davranış tutmuyordu.

**Kolay.** Ayırt edici bulgu bu: `drive.file` **non-sensitive**. Sonucu, doğrulanmamış yayımlanmış
uygulamada **kullanıcı tavanı ve tehlike ekranı yok** — bunlar yalnız sensitive/restricted kapsamlara
uygulanıyor. Bu, adaylar arasında **elle inceleme kuyruğu olmayan tek büyük şirket + ücretsiz katman
birleşimi**:

- Dropbox: 50 kullanıcıdan sonra iki hafta içinde onay alınmazsa yeni bağlantı donuyor.
- Vimeo: ücretsiz kullanıcı için beş iş günü.
- YouTube: denetimden geçmeden video zaten gizli kalıyor.
- OneDrive'ın da kuyruğu yok — ikinci sıraya bu yüzden geliyor; onu geride bırakan tek şey 5 GB'a
  karşı 15 GB.

Ayrıca `client_secret` installed-app akışında "Optional": AGPL bir kaynak kodda saklanamayacak bir
sır taşınmıyor. Tek bakımcılı açık kaynak bir masaüstü uygulaması için bu, düşünüldüğünden büyük bir
kolaylık.

**Tek tık.** Dürüst ifade: **bir kez izin ver, sonra tek tık.** İlk çalıştırmada tarayıcı açılır,
kullanıcı Google hesabıyla onay verir (loopback + PKCE), jeton saklanır. Sonrasında akış üç HTTP
çağrısı: resumable yükleme → `permissions.create` → `webViewLink`. Kullanıcı bir düğmeye basar,
panosunda izlenebilir bir bağlantı bulur. Cloudflare Stream'de kullanıcı hesap kimliği ve API jetonu
yapıştırmak zorunda; bu ölçütü karşılamıyor.

**Bu öneriyle birlikte kabul edilen üç bedel:**

1. **Süreli bağlantı yok.** `expirationTime` yalnız `user`/`group` izinlerinde. "Yayını kapat"
   `permissions.delete` ile anında çalışıyor, ama "3 gün sonra kendiliğinden kapansın" çalışmıyor.
   Süre şartsa OneDrive'a bakılmalı — orada da belge kendi içinde çelişkili, önce hesapla denenmeli.
2. **15 GB paylaşımlı.** Gmail ve Photos aynı havuzdan yiyor. Arayüzde asla sabit rakam yazılmamalı;
   kota `about.get` ile sorulup gösterilmeli, dolduğunda sunucudan gelen hata mesajı aynen
   aktarılmalı.
3. **Video hosting değil, dosya hosting.** Drive'ın oynatıcısı var ama uyarlanabilir kalite yok;
   büyük dosyada izleyici beklemeye başlar. Gerçek oynatıcı bir gereksinim hâline gelirse doğru
   cevap Cloudflare Stream, ve o zaman ücretli olduğunu baştan söylemek gerekir.

**Yapılabilir ama şu an yapılmamalı olanlar:** YouTube (şartlar izin veriyor, kota vermiyor),
Vimeo (şartlar izin veriyor, onay süresi ve 1 GB vermiyor), Cloudflare Stream (her şey izin veriyor,
"tek tık" ölçütü vermiyor).

---

## Şüpheli/riskli yanlar

- **YouTube kota belgesi kendi içinde tutarsız.** Aynı sayfanın özet kutusu 1600 birim, gövdesi ve
  tablosu "günde 100 çağrı, çağrı başına 1 birim" diyor. Hangisinin canlı sistemde geçerli olduğu
  test edilmedi — `doğrulanamadı`. Karar bundan etkilenmiyor (iki modelde de kota proje geneli), ama
  belgeye tek kaynak olarak güvenilemeyeceğini gösteriyor.
- **OneDrive `createLink` belgesi kendi içinde çelişkili.** İstek gövdesinde `expirationDateTime`
  belgeli, aynı sayfanın Remarks'ı *"Links created using this action don't expire..."* diyor. Süreye
  bel bağlanacaksa hesapla denenmeden koda girmemeli.
- **Vimeo'nun API belgesi tarayıcısız okunamıyor.** `curl` ile üç ayrı sayfa 18 KB'lık aynı boş
  iskeleti döndürdü. Bu, belgeye programatik olarak bakılamaması demek; sürüm değişikliklerini
  izlemek elle olur.
- **Dropbox'ın belgesi de istemci tarafında üretiliyor;** doğrulanabilir birincil kaynak MIT lisanslı
  `dropbox/dropbox-api-spec` deposu (son push 2026-08-21). Şema doğrulanabiliyor, plan bağımlı
  davranış (örneğin `expires` alanının ücretsiz hesapta reddedilmesi) şemada görünmüyor.
- **Doğrulanamayan rakamlar:** Dropbox ücretsiz depolama (2 GB) ve API azami dosya boyutu (350 GB);
  Dropbox bant genişliği sınırları (20 GB/gün, 100.000 indirme — yardım sayfası özeti); OneDrive
  ücretsiz 5 GB ve 250 GB dosya tavanı; Google'ın "Testing" durumunda 7 günlük yenileme jetonu ömrü;
  Drive'ın forumlarda bildirilen "download quota exceeded" davranışı (yalnız topluluk konuları
  bulundu, birincil belge yok); Bunny'nin "from $0.01/GB" fiyatı ve süreli bağlantı yetenekleri;
  Vimeo'nun gizlilik seviyesi/plan matrisi ve tus ayrıntıları.
- **Lisans ve marka.** Yedi platformun hiçbiri açık kaynak hizmet değil; hepsi tescilli marka ve
  sözleşmeye bağlı API. VidShrink AGPL-3.0-or-later — arayüzde "Google Drive'a yükle" yazmak
  tanımlayıcı kullanımdır, ama logo kullanımı ve "onaylı/ortak" izlenimi verilmemeli. Google için
  ek olarak marka doğrulaması yapılmadığı sürece onay ekranında **uygulama adı ve simgesi hiç
  görünmez** — kullanıcı "hangi uygulamaya izin veriyorum" sorusuyla karşılaşır; bu ilk çalıştırma
  metninde açıklanmalı.
- **Gizli kurulum maliyeti (Drive yolu için).** OAuth loopback dinleyicisi + PKCE, jeton saklama
  (Windows DPAPI / macOS Keychain / Linux Secret Service — üç platform, üç ayrı yol), jeton yenileme,
  devam edebilir yükleme durumu, iptal, kota ve ağ hatalarının kullanıcıya anlaşılır çevrilmesi.
  `Google.Apis.Drive.v3` (Apache-2.0, `googleapis/google-api-dotnet-client`, son push 2026-08-20,
  1514 yıldız, 10 açık issue, son etiketli sürüm v1.75.0 / 2026-06-04) bunun çoğunu getiriyor ama
  bağımlılık yüzeyini de büyütüyor. Elle `HttpClient` ile yazmak da mümkün; o zaman jeton yenileme
  ve resumable oturum mantığı bize kalır.
- **Bakım yükü, tek seferlik değil sürekli.** Kota, plan ve kapsam sınıflandırmaları değişiyor:
  taradığım yedi belgeden beşi son 60 gün içinde güncellenmiş. Arayüze hiçbir sayı sabit
  yazılmamalı.

## Kaynaklar

Hepsi 2026-08-24'te çekildi; parantez içindeki tarih belgenin kendi "last updated" değeri.

**YouTube**
- Videos: insert — https://developers.google.com/youtube/v3/docs/videos/insert (2026-07-08 UTC)
- Quota Calculator / determine_quota_cost —
  https://developers.google.com/youtube/v3/determine_quota_cost (2026-06-01 UTC)
- YouTube API Services Developer Policies —
  https://developers.google.com/youtube/terms/developer-policies (2026-06-24 UTC)
- Installed apps OAuth — https://developers.google.com/youtube/v3/guides/auth/installed-apps
  (2026-08-07 UTC)

**Google (OAuth / Drive)**
- OAuth app state overview —
  https://developers.google.com/identity/protocols/oauth2/production-readiness/overview
  (2026-05-22 UTC) — kullanıcı tavanı ve doğrulanmamış uygulama tablosu
- Sensitive scope verification —
  https://developers.google.com/identity/protocols/oauth2/production-readiness/sensitive-scope-verification
  (2026-08-19 UTC) — "3-5 business days"
- OAuth for installed/native apps — https://developers.google.com/identity/protocols/oauth2/native-app
  (2026-08-07 UTC) — PKCE, loopback, `client_secret` "Optional"
- Drive API scopes — https://developers.google.com/drive/api/guides/api-specific-auth
  (2026-07-22 UTC) — `drive.file` non-sensitive
- Drive uploads — https://developers.google.com/drive/api/guides/manage-uploads (2026-08-19 UTC)
- Permissions kaynağı — https://developers.google.com/drive/api/reference/rest/v3/permissions —
  `expirationTime` yalnız user/group
- permissions.create — https://developers.google.com/drive/api/reference/rest/v3/permissions/create
  (2026-02-24 UTC)
- Drive dosya boyutu — https://support.google.com/drive/answer/37603 — "All other files: Up to 5 TB"
- 15 GB paylaşımlı depolama — https://support.google.com/drive/answer/9312312 (arama özeti)

**Dropbox**
- DBX Performance Guide — https://developers.dropbox.com/dbx-performance-guide — 150 MB eşiği
- Developer Guide — https://www.dropbox.com/developers/reference/developer-guide — 50/500 kullanıcı,
  production onayı
- Resmî API şeması — https://github.com/dropbox/dropbox-api-spec (`sharing.stone`, `files.stone`);
  MIT, son push 2026-08-21, 45 yıldız, 6 açık issue (`gh api`)
- Bağlantı süresi planları ve bant genişliği: help.dropbox.com sayfalarının arama özetleri
  (`share/set-link-permissions`, `share/banned-links`) — **tam metin çekilemedi**

**Microsoft**
- driveItem: createUploadSession — https://learn.microsoft.com/en-us/graph/api/driveitem-createuploadsession
  (güncelleme 2026-08-11)
- driveItem: createLink — https://learn.microsoft.com/en-us/graph/api/driveitem-createlink
  (güncelleme 2026-08-04)
- Publisher verification overview —
  https://learn.microsoft.com/en-us/entra/identity-platform/publisher-verification-overview
  (güncelleme 2026-06-15)
- Graph throttling limits — https://learn.microsoft.com/en-us/graph/throttling-limits — Files için
  sayısal sınır vermiyor, SharePoint belgesine yönlendiriyor
- OneDrive ücretsiz 5 GB: microsoft.com ürün sayfaları (arama özeti, **tam metin çekilemedi**)

**Vimeo**
- How to request API upload access —
  https://help.vimeo.com/hc/en-us/articles/12427803706001-How-to-request-API-upload-access
- About the Vimeo Free plan —
  https://help.vimeo.com/hc/en-us/articles/12425432518801-About-the-Vimeo-Free-plan
- `developer.vimeo.com/api/upload/videos`, `/api/guides/start`, `/api/authentication` — üçü de
  `curl` ile boş iskelet döndü (18 KB), içerik okunamadı
- `gh api repos/vimeo/vimeo.php` — Apache-2.0, son sürüm 4.0.1 (2025-10-23), 72 açık issue

**Cloudflare Stream**
- Direct creator uploads —
  https://developers.cloudflare.com/stream/uploading-videos/direct-creator-uploads/
- Pricing — https://developers.cloudflare.com/stream/pricing/ (Last updated Apr 21, 2026)
- Securing your stream —
  https://developers.cloudflare.com/stream/viewing-videos/securing-your-stream/
- Get started — https://developers.cloudflare.com/stream/get-started/

**Bunny**
- Upload video referansı — https://bunny.net/docs/reference/video_uploadvideo
  (`docs.bunny.net` → `bunny.net/docs`, 302)
- Stream ürün/fiyat sayfası — https://bunny.net/stream/ — işletmeci BunnyWay d.o.o.

**.NET istemcileri** (`gh api`, 2026-08-24)
- `googleapis/google-api-dotnet-client` — Apache-2.0, push 2026-08-20, 1514 yıldız, 10 açık issue,
  v1.75.0 (2026-06-04)
- `microsoftgraph/msgraph-sdk-dotnet` — lisans NOASSERTION, push 2026-08-21, 788 yıldız,
  218 açık issue, 6.5.0 (2026-08-06)
- `dropbox/dropbox-sdk-dotnet` — MIT, push 2026-08-21, 347 yıldız, 29 açık issue, v7.2.0 (2026-07-14)
