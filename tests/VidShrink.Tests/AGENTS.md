# VidShrink.Tests

Tek test projesi. Paralel koşum kapalı (`LanguageTests.cs` özniteliği).

**Ne zaman ne koşulur:** yerelde yalnız dokunulan alanın filtresi (`--filter`), her
teslimde. Tam süit yerelde koşulmaz — 35 dakika sürüyor ve CI'da zaten paralel koşuyor;
itmeden sonra `gh run list` yeşili teslimin şartı. `tools/kosum-kapisi` yalnız majör
sürümden (`x.0.0`) önce koşar.

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
  geçer, uydurma ad reddedilir, süre yazımı ve aynı saniyedeki ikinci kaydın ayrı dosyaya gitmesi. Kaydedici ayarının
  `VIDSHRINK_SETTINGS_PATH` klasörüne gitmesi; "Kare al" düğmesi (sahte kare işiyle yolun görünmesi, başarısız ve atan
  işin hata satırı, üç anahtarın 42 dilde olması). Varsayılan kap Matroska: eski `container: Mp4` varsayılana döner,
  seçim `containerChoice`'a yazılır; sonuç panelinde "MP4 olarak kaydet" yalnız mkv'de görünür, `-c copy` ile
  sahte süreçten geçer. Basit/Gelişmiş kip: Basit'te seçenek paneli gizli, hedef iki sütuna yayılır ve elle kip
  kapalı sayılır; eski `manualMode: true` Gelişmiş açılır; altı anahtar 42 dilde. Geri sayım
  (0/3/5/10): sahte bekleme işiyle 3-2-1 şeritte sayılır, iptal düğmesi, F8 ve F7 keser; mini şerit sayıyı gösterir
  (mini pencere artık `InitializeComponent` ile kuruluyor, adlı alanlar boş kalmıyordu); sayım doğrulamadan sonra,
  oturumdan önce koşar (kaynak pimi). Kayıt çerçevesi: bölgenin dışına çizilir (`Outer`, ölçekte yukarı yuvarlanan
  kenar), ekrana sığmazsa bölgenin içine (`Placement`), WS_EX stili saf fonksiyonla, oturumda ve F9 ile gizlenmemişken
  istenir; sahte `IRecorderFrameHost` ile. KaydediciCerceveTests: üç hedefte bölge (ekran, `WindowRect`, bölge); tam
  ekran çerçevesi affinity 0x11 ile kayda girmez (2x2 gdigrab pikseli, negatif kontrol affinity'siz). Tepsi: üç durum
  (boşta/kaydediyor/duraklatıldı) paletin üç ayrı fırçasından, simge Skia'da çizilip merkez pikseli okunur; ipucu
  süre ve diskteki anlık MB (`RecorderSession.WrittenMb`); sahte `IRecorderTrayHost`, TrayIcon kurulmaz. Genel kısayol: tanım tek yerde
  (`RecorderHotkeys`), sahte `IGlobalHotkeys` çakışmayı ve basışı taklit eder; başsızda `NoGlobalHotkeys`
  seçilir, gerçek RegisterHotKey testte kaydedilmez. Gelişmiş panel: on kol denetimden
  `BuildRequest`'e ve ayara geçer, kodlayıcı değişince profil/tune/piksel listesi yenilenir, otomatik kodlayıcıya
  uymayan kol `FitToCodec` ile düşer. Geçici ayar dosyaları `.calisma/kap-olcu-*`, test siler.
- `KaydediciAyarTests.cs` — paket 2, her kaydedici ayarı uçtan uca: denetimde değişir, Başlat'a basmadan
  `recorder-settings.json`'a yazılır, yeni görünümün `PrepareRecording` argümanına geçer, lavfi çevirisiyle ffprobe'ta
  görünür; değiştirilmeyen görünüm negatif kontrol. Otomatik kip kabı korur, açılışta bir kez ölçer (başsızda ve elle
  kipte ölçmez, sahte `OpenMeasure`). Canlı kol 4,5 sn bölge kaydı. Kanıt `.calisma/paket-2/`.
- `KaydediciHedefTests.cs` — paket 2, hedef ve bitiş: tek hedef kutusu kendi sınırına geçer (yalnız saniye `-t`,
  yalnız MB `-fs`), boyut sınırı parçalara kalanla bölünür, MP4/MOV'da boyut ölçütü Matroska'ya yakalanıp durunca
  `-c copy` ile teslim kabına çevrilir; "bitince klasörü aç" yalnız kutu açıkken, F10 iptal kaydı durdurup dosyayı
  siler. Canlı kollar `[KayitFact]`, 640x480 bölge ≤5 sn. Kanıt `.calisma/paket-2/`.
- `KaydediciSeciciTests.cs` — paket 2, hedef seçiciler: sürükleme çift boyuta iner, oran kilidi ekranda kalır
  (`RegionDraw`); sahte `DrawRegion` ile çizilen bölge kutulara, json'a ve `-offset_x/-video_size`'a geçer, Esc
  vazgeçer; çizim penceresi gerçek açılıp masaüstünü kaplar. Hazır boyut, pencere seçici (sahte `ListWindows`,
  `Pick` eleme negatifleri), ekran seçici (sahte `ScreenSource`, ikinci ekranın ofseti). Kanıt `.calisma/paket-2/`.
- `KaydediciCerceveTests.cs` — paket 2, çerçeve her hedefte: ekran/pencere/bölge dikdörtgeni, ekrana sığmayan çerçeve
  içe, gerçek pencerenin istemci alanı, affinity 0x11 gerçek pencerede (negatif kontrol affinity'siz).
- `KaydediciGirdiTests.cs` — paket 2, tıklama halkası, tıklama sesi, tuş gösterimi: `KeyText`/`KeyTracker`, bellek
  WAV'ı, bindirme yeri; sahte `IInputHooks`/`IInputOverlay`/`IClickSound` ile kutu → json → kanca, kapalı kutu kanca
  kurmaz; gerçek halka ve tuş penceresi açılıp süresinde kapanır. Gerçek düşük seviye kanca testte kurulmaz.
- `KaydediciKameraTests.cs` — paket 2, webcam bindirmesi ve imleç büyüteci: dshow girdisi, `overlay` grafiği ses ve
  ölçekle tek `-filter_complex`, dört köşe, geçersiz aygıt/genişlik/köşe reddi; sahte `CameraSource` ile kutu → json →
  istek. `[KameraFact]` OBS Virtual Camera varsa 3 sn kayıt, köşe parlaklığı karşı köşeyle kıyaslanır (CI'da atlanır).
  Büyüteç: saf yerleşim, gerçek pencere `Follow`, sahte `IMagnifier` ile yalnız kayıtta açılma. Kanıt `.calisma/paket-2/`.
- Paket 2b kaydedici sınıfları, kanıt `.calisma/paket-2b/<konu>/`: `KayitOdakTakibiTests` (T7, "Kaydı izle" açıkken biten kayıt
  küçültme ve oynatıcıya sekme değişmeden yüklenir; kapalıyken yüklenmez, `VIDSHRINK_LIBMPV` ister), `KaydediciAyarGidisDonusTests`
  (yansımayla her `RecorderSettings` özelliği diske gidip aynı döner; kapalı kümeli ayar `Kisitli` tablosunda),
  `KaydediciArkaPlanTests` (kamera arka planı `backgroundkey`/`chromakey`, lavfi bileşik karesinin pikseli),
  `BoslukKirpmaTests` (`freezedetect` + `trim/concat`, 6 sn kayıt 3,8 sn olur, donuksuz kayda dokunulmaz),
  `KaydediciOnizlemeTests` (ikinci çıkış `image2 -update 1` 320 px jpg, boyut sınırıyla red, bozuk kare eski resmi korur),
  `KaydediciTamponTests` (2 sn `segment_wrap` parçaları, 4 sn tampon 9 sn döner, kaydedilen ≤6,5 sn; F11 sahte `IReplayBuffer`),
  `KayitBolmeTests` (`[KayitFact]`, gdigrab 5 sn sınır + 2 sn bölme ≥2 parça toplamı 5 sn, bölmesiz tek parça, 1 ms kapanış `_partial`;
  öldürülen mkv `-flush_packets 1` ile 90 paket, bayraksız 0 bayt, öldürülen mp4 0 paket).
- `SesliKayitTests.cs` — 8d kolu, ses girdisinin motora bağlanması: iki cihazda `amix` **ve** `[aout]` eşlemi,
  tek cihazda filtre kurulmaması, sessiz kayıtta `-map` yazılmaması, bölge kırpmasının ses grafiğiyle birlikte
  durması. Canlı kol gerçek mikrofon ister: `ffprobe` iki akış görür. Kanıt `.calisma/dalga8d/`.
- `KayitFfmpegKoluTests.cs` — 9c kolu, kaydın ffmpeg argümanına eklenen on kol: kap (mkv `+faststart` yazmaz,
  öldürülen kaydı yalnız Matroska taşır), `-vf scale` zinciri (kırpma önce), `-g`/`-profile:v`/`-tune`, hedef bit
  hızı kolu (`-b:v` varken `-crf` yok, satıcının hız kontrolü motordan), `-pix_fmt`/`-colorspace`/`-color_range`,
  `-t` ve bölme ölçütünün argümana girmemesi, ayrı ses izleri (`-c:a:N`), ses filtreleri (karışımdan önce girdi
  başına), tek kare `BuildSnapshot`, çoklu monitörün ofsetli bölgeye çevrilmesi. Her kolun negatif kontrolü var;
  44 ölçünün 24'ü üretilen argüman dizisini okur, 20'si doğrulama hata listesini ya da
  kapalı küme dönüşlerini. Piksel biçimi kodlayıcı başına küme (`PixelFormatsFor`): libx264'ün sessizce çevirdiği beş
  ad reddedilir, Quick Sync/NVENC/AMF'de paketli ad yazılır. Tek süreç `ffmpeg -h encoder=<ad>` (11 kısa çağrı), kümenin
  ffmpeg'in bildirdiği biçimlerde olduğunu okur. Kanıt dosyası bırakmaz; tablo `docs/olcumler/kaydedici-piksel-bicimleri.md`.
  GIF kabı: yakalama `.gif-kayit.mkv`'ye, durunca `GifPalette` (klip dışa aktarımıyla aynı filtre) ile GIF'e; ses, bölme
  ve 50'yi aşan kare hızı reddedilir. Tek kısa lavfi çevirisi (1 sn, 64x48) `GIF89a` başlığını okur, `.calisma/`'yı temizler.
- `KayitOtomatikKipTests.cs` — 9d kolu, kaydedicinin otomatik kipi: aday merdiveni (`RecorderAutoPlan`) ve kazanma
  kuralı (`RecorderAutoProbe`). Kodlayıcı yeğlemesi nvenc/qsv/amf, donanım yokken x264, yeğlenmeyen ve uydurma adın
  negatif kontrolü, kare hızı merdiveni (75 Hz → 60), yarı boyutun çift olması, **her adayın
  `RecorderArguments.Validate`'inden geçmesi**, `Apply`'ın bit hızı kolunu temizlemesi, `Unmeasured` donanımın
  seçilmemesi, tamamlanmayan denemenin kazanmaması. Süreç çalıştırmaz, kanıt dosyası bırakmaz.
- `KayitButceTests.cs` — kaydedicinin hedef boyut bütçesi: OBS'in katsayısıyla 10 MB/30 sn → 2636 kbit/sn,
  hedef verilmeyince kalite kolunun korunması, bozuk/sıfır/negatif hedefin elenmesi, taban sınırının iki yakası
  (0,7 MB elenir — 0,8 MB geçer), `ApplyBudget`'in kalite kolunu tavanlı bit hızına çevirip `Validate`'ten geçmesi.
  Süreç çalıştırmaz, kanıt dosyası bırakmaz.
- `KurulumIlerlemesiTests.cs` — kurulum panelinin tavan kuralı: ilk 24 karelik açılış atağı (yaklaşma 0,2, sürünme 0,08),
  sonra çubuğun yüzdeye fark × 0,08 (en az 0,2) ile yaklaşması, yüzde durunca tavana fark × 0,006 ile sürünüp tavanı geçmemesi, geriye yazan adımın yüzdeyi
  düşürmemesi, 0-100 kırpması, günlüğün ekranda dokuz satırda durup diske tamamının gitmesi, sonucun duyurulması.
  Çizim ölçmez; ölçtüğü şey köprünün kararı.
- `KayitTeslimTests.cs` — kayıt bittikten sonraki teslim: sonuç panelindeki dört kapı (klasör, küçültme, oynatıcı,
  paylaşım), yolun metin kutusundan değil alandan okunması, ana pencerenin iki kapısının sekmeyi değiştirip yükleyiciyi
  çağırması, paylaşımın `ShareFlow`/`Core/Share` üstünden gitmesi, hedef tablosunun yayın paketine girmesi ve iki yeni
  anahtarın 42 dilde bulunması. Kaynak metin okur, pencere açmaz.
- `PencereKabuguTests.cs` — pencere kabuğunun düzeni: üst şerit içerikle aynı gözde katman, başlık düğmeleri içeriğin üstünde,
  gizleme sınıfı iki parçayı kapatıyor. Kaynak metin okur; gizlenme ve anahat davranışı `OynaticiYolHaritasiTests`'te.
- `OynaticiKisayolTests.cs` — tarifteki her kısayol gerçek girdi olayıyla PlayerView'a verilir, etkisi motordan geri okunur;
  döndürme karenin piksellerinden. Kanıt `.calisma/oynatici-kisayol/`.
- `OynaticiOdakYoluTests.cs` — aynı tuşlar MainWindow'un odak yolundan: sekme değişimi, kaydırıcı/açılır kutu odakta, tam ekran.
- `OynaticiYolHaritasiTests.cs` — yol haritası denetiminin oynatıcı maddeleri (P2 merkez mıknatısı, P3 menüde ayarlar, P12, P14 üst bar
  gizlenmesi, P18, P19 duraklatma simgesi süresi, P20, P24 anahat pikselleri, P26 yayılma maskesi, P28 yandaki altyazı).
  Zamanlayıcı bekleyen ölçüler `Dispatcher.UIThread.MainLoop` ile pompalar; `RunJobs` Win32 zamanlayıcısını tetiklemez.
  Kanıt `.calisma/oynatici-yol-haritasi/`, negatif kontrol betiği aynı klasörde.
- `KabukMenusuTests.cs` — sağ tık menüsünün iki tarafı: `ShellMenu.cs` ile `Install-VidShrink.ps1`'in anahtar adları,
  uzantı listesi ve hedef listesi birebir aynı; silme kolu Appx paketini de kaldırıyor; kutu Ayarlar sekmesinde;
  etiket arayüz dilini izliyor; sekiz yeni anahtar 42 dilde. Kayıt defterine yazmaz, kaynak metin okur.
- `BaslaticisizCiftTikTests.cs` — G2/G3: çift tık `app\VidShrink.App.exe`'yi açıyor; `--bakim` kapıları (başlatıcıdan
  doğan uygulama, kurulu düzen dışı, eski başlatıcı), `app\` altından kökteki `tools\ffmpeg`, "Yükle"den sonra rozetin
  ara metin yazmaması. Açma komutunun değeri `KabukEntegrasyonTests`, betik/motor eşitliği `KurucuExeTests`'te.
- `BaslaticiPanelsizTests.cs` — Yol D: `.calisma/yol-d/kurulum-*` sahte kurulumda gerçek başlatıcı ve `tools/VidShrink.SahteUygulama`.
  Yavaş bakım kancasında (`VIDSHRINK_BAKIM_GECIKMESI_MS`) başlatıcının görünür penceresi yok (EnumWindows), uygulama hemen doğar;
  kapı tutulurken doğrudan açılan uygulama başlatıcıya devreder; koşan uygulama kapanmadan kopya başlamaz; `.bakim-hatasi` panelde görünür.
- `KabukMenusuKayitTests.cs` — aynı menünün davranışı, yalnız `ShellMenu.TestRoot` altında: kutunun komutu başlatıcıyı
  (`VidShrink.exe`) gösteriyor; `Relabel` anahtarı silip kurmuyor (komut altındaki işaret kalıyor), yalnız `MUIVerb` yazıyor, aynı etiketle 0 dönüyor. Her test gerçek HKCU komut değerinin değişmediğini sınar.
- `OynaticiKarsilastirmaTests.cs` — iki motor örneği: şerit kodlu klipte kare farkı ≤1; yarı güncel bileşik kare ortağı
  gelmeden yayınlanmaz (elle sürülen sahte motor); eski ffmpeg borusuna ve NAudio'ya canlı başvuru yok.
- `IkonKutusuTests.cs` — `Themes/Icons.axaml`'daki 26 yolun tasarım kutusu: hepsinin başında `M 0,0 M 24,24`
  sabitleyicisi, mürekkebin 2 birimlik kenar payı içinde kalması, merkezin 12/12'ye ±0.55 oturması.
  Tek muafiyet `IconPlay` (üçgen optik olarak sağa kaydırılır, +0.5..+1.5 sınanır). Ölçünün kaynağı
  `docs/arastirma/ikon-estetigi.md`. Yolları `AppHost` üstünden ayrıştırır.
- `GelistiriciSekmesiTests.cs` — gizli Gelişmiş sekmesi: `DeveloperUnlock` eşiği, pencere sınırı, ara açılınca
  sıfırlanma, açıldıktan sonra baştan başlama; biçimlemede sekmenin gizli başladığının ve kapatma düğmesinin pimi.
- `GoruntuCekTests.cs` — kanıt karesi üretir: `.calisma/kesit-ef/` altına güncelleme panelini ve 26 simgelik
  sayfayı PNG olarak yazar. Ölçmez, sınamaz; tarz kararlarının resmi buradan çıkar.
- `StreamMappingTests.cs` — dalga 1c, akış eşleme: ffmpeg'in ürettiği 3 sn'lik mkv (2 ses, srt, elle yazılmış PGS, 2 bölüm,
  başlık/tarih) ve dönüş işaretli mp4. Varsayılan MP4 tek ses + mov_text, İzleri koru MKV tüm izler, platform tek iz;
  çıktılar ffprobe'la okunur. Negatif kontroller: eşlemesiz ffmpeg başka dili seçer ve tarihi düşürür, yan izleri
  saymayan bütçe hedefi aşar. Her `StreamNote` ayrı bir `main.reason.stream.*` anahtarına düşer, anahtar 42 dilde çevrilidir ve pencerenin gerekçe satırında görünür. Kanıt `.calisma/hb-1c-test/`.
- `KurucuExeTests.cs` — `VidShrink-Setup.exe` motoru (`Core/Setup`): betikle aynı kayıt ağacını yazıp siler (test anahtarında), kilitli klasör denemeleri, sahte yayınla çevrimdışı kurulum ve kaldırma, sağlama tutmazsa eski kuruluma dokunulmaması, yarım kurulumda geri koyma, sabitlerin betikle aynılığı. Gerçek kayıt köküne test konağı yazamaz. Çıktı `.calisma/test-ciktilari/kurucu-exe/`.
- `HipersurusHTests.cs` — H dalgası: `--bakim` açılış görüntüsünden önce başlamıyor (yedek bekleme, başlatıcı hatası), sinyal boş açılışta boyaya, dosyayla ilk kareye bağlı; panelin ölçüm aşaması erteleme pimi. Davranışı `PlaybackResumeTests` (ilk parça 1 ms, `Probed` planı ekrandaki parçayı iptal etmiyor), composite pimi `HipersurusTests`. Ölçüm `docs/olcumler/hipersurus-h.md`.
- `TestAyarYoluTests.cs` — modül başlatıcısı `VIDSHRINK_SETTINGS_PATH`'i `.calisma/test-ciktilari/appdata/<pid>`'e alır; ana pencerede açılan dosyanın son dosyalar listesi ve kaydedici ayarı oraya yazılır, gerçek `%APPDATA%\VidShrink` dosyalarının boyut/zaman damgası değişmez (yalnız okunur). Kanıt `.calisma/ayar-yolu/`.
- `OynaticiDalga3GirdiTests.cs` — 3. dalga platform girişinden: ham sağ tık, yeni açılan menü penceresinde ham yukarı ok (satırı kaydırıp görünür kılar) ve ham tık ile ekran görüntüsü klasörü (sahte `IStorageProvider`, iptal kolu), ham Ctrl+E o klasöre yazar; ham Space ile oynayan 1 sn'lik klipte gerçek dosya sonu `RepeatMode.All`'da sonrakini açar, `Off`'ta açmaz; ham `RawDragEvent` dosyayı açar, klasör ve silinen dosyayı açmaz. Kanıt `.calisma/girdi-dalga3/`.
- `BudgetFillTests.cs` — bütçe doldurma: hedefin %97'sinin altındaki teslim bir yukarı deneme ister, %97 ve üstü istemez; tavanı aşan ya da küçülen yukarı deneme teslim edilmez; deneme bütçesi koşu sınırı + 1; tavan üstü örnek isteği aradeğerle sınırlar; donanım ve VideoToolbox'ta plan yok (yazılım negatif kontrol). Süreç çalıştırmaz. Ölçüm `docs/olcumler/butce-doldur.md`.
