# VidShrink.Tests

Tek test projesi. `dotnet test` tamamı yeşil olmadan teslim yok; paralel koşum kapalı (`LanguageTests.cs` özniteliği).

- Zamanlama ölçen testleri yük altında okuma; yerelde filtreli koş, tam süit CI'da.
- Çıktı ve kanıt dosyaları `.calisma/` altına (`GirdiKanit`, `MotorKanit`).
- `OynaticiMotorTests.cs` — libmpv motoru: başsız kare, bozuk dosya, exact/keyframe inişi, geç işlenen SEEK olayı
  (`BeforeEvent` iç kancası), aramadan kalan geç `time-pos` olayı (`OnTimePosChanged` iç girişi), oynarken aramanın
  karesi (referans kareyle bayt eşitliği), PlayerView karesi, A/V farkı (audio-delay negatif kontrolü), 10 tık birikmesi,
  arama medyanları (1080p ≤60 ms, 2160p ≤200 ms, HEVC 1080p sayı), Dispose sırasında okuma yarışı. libmpv ya da ffmpeg
  yoksa kırmızı olur, atlanmaz. Yerelde `VIDSHRINK_LIBMPV` ister. 1080p/2160p ve 10 tıkın 150 ms eşikleri `[HedefMakineFact]`:
  CI'da (`GITHUB_ACTIONS`) hep atlanır, yerelde yalnız sessiz makinede koşar; 10 tıkın birikme ve gösterim şartı CI'da da koşar. `[QuietMachineFact]` CI'yı ayırmaz; koşucu boş okununca koştu.
- `OynaticiParcaTests.cs` — ffmpeg'in ürettiği 2 ses + 1 gömülü altyazılı mkv: `aid`/`sid`, gecikme, boyut,
  konum geri okunur; cp1254 .srt `sub-text`'te bozulmaz, cp1252 negatif kontrolü bozar. Kısayol, menü ve
  altyazı bırakma PlayerView üstünden. Kanıt `.calisma/dalga2/`.
- `OynaticiKurulumTests.cs` — libmpv konum sırası, kurucu sabitleri CI ile aynı, açılamayan motor atılır.
- `OynaticiGorunumTests.cs` — 3. dalga, `.calisma/dalga3/gecici`; `OynaticiAracTests.cs` — 4b dalga: küçük resim ≤300 ms (medyan/p95, gösterim sahte motorla CI'da da), klip/GIF, mini mod, yerel sunucudan adres; `.calisma/dalga4b/`.
- `OynaticiGelismisTests.cs` — 4a dalga: her gelişmiş ayar motora yazılır, **motordan** okunur, sıfırlanır; negatif kontrol kareden. `.calisma/dalga4a/`.
- `KayitMotoruTests.cs` — 8a dalgası, ekran kaydı motoru: üç platformun yakalama argümanı (gdigrab/avfoundation/x11grab),
  `-nostdin` verilmediğinin pimi, negatif kontroller (uydurma kodek, macOS pencere, tek sayılı bölge). Canlı kol
  `[KayitFact]` ile gdigrab'a bağlı: 5 sn'lik gerçek kayıt, `q` ile kapanan dosya ffprobe'ta okunur, zaman aşımında
  öldürülen dosya bozuk çıkar, duraklatılan kayıt iki parçadan birleşir. Kanıt `.calisma/dalga8a/`.
- `SesGirisiTests.cs` — 8c dalgası, ses girişi: pimli ffmpeg çıktısı üzerinden cihaz listesi ayrıştırması, dshow/
  avfoundation/pulse argümanları, iki girdide `amix`, uydurma cihaz adının negatif kontrolü. İki canlı kol gerçek
  cihaz ister. Kanıt `.calisma/dalga8c/`.
- `KaydediciArayuzTests.cs` — 8b dalgası, Kaydedici sekmesi: kodlayıcı ve ön ayar listeleri motorun doğrulamasından
  geçer, uydurma ad reddedilir, süre yazımı ve aynı saniyedeki ikinci kaydın ayrı dosyaya gitmesi. Kanıt `.calisma/dalga8b/`.
- `SesliKayitTests.cs` — 8d kolu, ses girdisinin motora bağlanması: iki cihazda `amix` **ve** `[aout]` eşlemi,
  tek cihazda filtre kurulmaması, sessiz kayıtta `-map` yazılmaması, bölge kırpmasının ses grafiğiyle birlikte
  durması. Canlı kol gerçek mikrofon ister: `ffprobe` iki akış görür. Kanıt `.calisma/dalga8d/`.
- `KayitFfmpegKoluTests.cs` — 9c kolu, kaydın ffmpeg argümanına eklenen on kol: kap (mkv `+faststart` yazmaz,
  öldürülen kaydı yalnız Matroska taşır), `-vf scale` zinciri (kırpma önce), `-g`/`-profile:v`/`-tune`, hedef bit
  hızı kolu (`-b:v` varken `-crf` yok, satıcının hız kontrolü motordan), `-pix_fmt`/`-colorspace`/`-color_range`,
  `-t` ve bölme ölçütünün argümana girmemesi, ayrı ses izleri (`-c:a:N`), ses filtreleri (karışımdan önce girdi
  başına), tek kare `BuildSnapshot`, çoklu monitörün ofsetli bölgeye çevrilmesi. Her kolun negatif kontrolü var;
  44 ölçünün 24'ü üretilen argüman dizisini okur, 20'si doğrulama hata listesini ya da
  kapalı küme dönüşlerini. Süreç çalıştırmaz, kanıt dosyası bırakmaz.
- `OynaticiKarsilastirmaTests.cs` — iki motor örneği: şerit kodlu klipte kare farkı ≤1; yarı güncel bileşik kare ortağı
  gelmeden yayınlanmaz (elle sürülen sahte motor); eski ffmpeg borusuna ve NAudio'ya canlı başvuru yok.
