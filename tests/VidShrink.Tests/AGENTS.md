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
- `KurucuExeTests.cs` — `VidShrink-Setup.exe` motoru (`Core/Setup`): betikle aynı kayıt ağacını yazıp siler (test anahtarında), kilitli klasör denemeleri, sahte yayınla çevrimdışı kurulum ve kaldırma, sağlama tutmazsa eski kuruluma dokunulmaması, yarım kurulumda geri koyma, sabitlerin betikle aynılığı. Gerçek kayıt köküne test konağı yazamaz. Çıktı `.calisma/test-ciktilari/kurucu-exe/`.
