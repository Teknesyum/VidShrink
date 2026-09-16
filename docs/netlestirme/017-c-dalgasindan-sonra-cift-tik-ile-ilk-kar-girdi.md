[[netlestirme:017]]

# Netleştirme: C dalgasindan sonra cift tik ile ilk kare arasi 4064 ms; bunun 2520 ms'i pencere

İşe başlamadan önce soruyu keskinleştir. Görüş verme, plan yazma, kod yazma.
Yalnız şunu döndür: soruda belirsiz kalan yerler, her biri için tek satırlık bir netleştirme sorusu, en fazla beş. Belirsizlik yoksa "net" yaz.

## Soru

C dalgasindan sonra cift tik ile ilk kare arasi 4064 ms; bunun 2520 ms'i pencerenin kurulmasinda (xaml 1225, yapici kuyrugu 476, Show 413, Opened 407), 555 ms'i Avalonia cercevesinde, 391 ms'i mpv'nin kendi payinda. Hedef 100 ms civari. Bu dort kalemi hangi sirayla ve hangi somut degisikliklerle asagi cekerim; yedi satir ici TabItem'i tembellestirmek 258 x:Name'e bagli 4624 satirlik kod arkasini kirmadan nasil yapilir; mpv tarafinda hwdec/demuxer/cache/vd-lavc anahtarlarindan hangileri ilk kareyi one ceker ve hangisinin gorunur davranis bedeli var?

## Elde olan olgular

# Hipersürüş B dalgası olguları — 16 Eylül 2026

Hepsi bu depoda ölçülmüş ya da kaynaktan okunmuş. Tahmin yok.

## Nerede duruyoruz

C dalgası (0.6.0) kesildi. Eşleşik sıcak ölçüm, 14 tekrar, 720p30 20 sn 6,2 MB klip,
DESKTOP-0J80KVV / Windows 11 22631:

| Saat | Taban | C dalgası | Eşleşik fark ortancası |
| --- | --- | --- | --- |
| kabuk-ilk-kare (dış saat, çift tık → ilk kare) | 6506,3 ms | 4064,0 ms | **−2603,3 ms**, 14/14 çift |
| perde (ekranda bir şey görünene kadar) | yok | 211,4 ms | yeni adım |

Bu makine bu oturumda A dalgası oturumuna göre ~3,6 kat yavaş koşuyor; mutlak
sayılar oturumlar arası kıyaslanmaz, yalnız eşleşik fark kıyaslanır.

## C dalgası sonrası adım tablosu (ortanca, ms, sürecin doğumundan itibaren)

| Adım | Ortanca | Bir öncekinden pay |
| --- | --- | --- |
| baslatici | 128,4 | 128,4 |
| perde | 211,4 | — (ayrı kol) |
| app-dogdu | 164,4 | 36,0 |
| main | 285,8 | 121,4 |
| tek-ornek | 291,2 | 5,4 |
| libmpv-hazir | 333,0 | 41,8 |
| cerceve (Avalonia OnFrameworkInitializationCompleted) | 888,0 | **555,0** |
| ayar-okundu | 900,4 | 12,4 |
| palet | 901,6 | 1,2 |
| pencere-yapici (MainWindow ctor girişi) | 1071,1 | 169,5 |
| xaml (InitializeComponent döndü) | 2296,3 | **1225,2** |
| yapici-bitti | 2771,9 | **475,6** |
| pencere-kuruldu (Show döndü) | 3185,0 | **413,1** |
| pencere-yuklendi (Opened) | 3591,6 | **406,6** |
| ayarlar | 3629,6 | 38,0 |
| giris-canlandirmasi | 3630,6 | 1,0 |
| sekme | 3632,3 | 1,7 |
| kare-kaynagi | 4022,9 | **390,6** |
| ilk-kare | 4027,2 | 4,3 |

İşaretler birikimli. `motor-acildi` 4012,7. Toplamın dörtte üçü pencerenin
kurulmasında: `xaml` + `yapici-bitti` + `pencere-kuruldu` + `pencere-yuklendi`
= 2520,5 ms. Motorun kendi payı 390,6 ms.

## Pencere tarafındaki olgular

- `MainWindow.axaml` **1349 satır**, `MainWindow.axaml.cs` **4624 satır**,
  XAML'de **258 adet `x:Name`**.
- Tek `TabControl`, **yedi `TabItem`'ın içeriği aynı XAML dosyasında satır içi**:
  oynatıcı (:137, sadece 5 satır — `PlayerView` denetimi), küçültme (:142-747),
  dönüştürme (:749-994), hakkında (:996-1020), kaydedici (:1022-1026),
  gelişmiş (:1028-1120, `IsVisible=False`), ayarlar (:1122-1273).
  `InitializeComponent` yedisini birden kuruyor; açılışta görünen bir tanesi
  (`SelectedIndex="1"`, küçültme; kabuktan dosya gelince oynatıcıya geçiyor).
- Yapıcı `InitializeComponent`'ten sonra **~60 `Watch(...)` aboneliği**, dil/tema
  listesi kurulumu, `InitializeAdvancedUi()`, `RefreshOutputAndFfmpegChoiceLists()`,
  `RefreshChipDerivation()`, `RefreshSectionSummaries()` koşuyor — hepsi
  `yapici-bitti`nin 475,6 ms'i içinde.
- `Opened += OnWindowLoaded`; `pencere-yuklendi` ile `sekme` arası yalnız 40 ms.
- 0.6.0'da yayın `PublishReadyToRun` + `TieredPGO` ile çıkıyor. `PublishAot`
  **yasak**: Avalonia XAML ve palet yansıma kullanıyor.

## Motor tarafındaki olgular

- `MpvEngine.BaseOptions` (`src/VidShrink.Player/MpvEngine.cs:141`) bugün şunları
  veriyor: `vo=libmpv`, `hwdec` (`PlaybackOptions.Hardware` `Off` varsayılan →
  `no`), `keep-open=yes`, `idle=yes`, `pause=yes`, `terminal=no`,
  `input-default-bindings=no`, `input-vo-keyboard=no`, `load-scripts=no`,
  `osd-level=0`, `osd-bar=no`, `sub-auto=no`, `sid=no`, `audio-display=no`,
  `audio-fallback-to-null=yes`.
- `demuxer-lavf-probe-info`, `demuxer-max-bytes`, `cache`, `hr-seek`,
  `video-sync`, `vd-lavc-threads`, `vd-lavc-fast` **hiç yazılmıyor**: libmpv
  varsayılanı geçerli.
- `HardwareDecoding` enum'u iki değerli: `Off`, `AutoCopy`. Varsayılan `Off`
  (`IPlaybackEngine.cs:39`).
- `sekme` → `kare-kaynagi` arası 390,6 ms: `mpv_create` + `loadfile` + ilk çözme.
- `Program.WarmPlayback()` libmpv'yi `Task.Run` ile önden yüklüyor; `libmpv-hazir`
  333 ms'te, yani kitaplık ilk karenin çok önünde hazır.
- Oynatıcı motoru zaten `Task.Run(EngineFactory)` ile arayüz ipliğinin dışında
  kuruluyor.

## Kısıtlar

- Renk yalnız `Themes/Palette/<Ad>/Theme.axaml`'dan (26 palet dosyası), ölçü yalnız
  `Themes/Theme.axaml` belirteçlerinden. Yeni belirteç uydurulmaz, sorulur.
- Kurulum iki süreçli: `VidShrink.exe` (başlatıcı) + `app\VidShrink.App.exe`.
- Tek test projesi `tests/VidShrink.Tests`; her karar kaynağı okuyan ya da davranışı
  koşturan bir pimle sabitleniyor. Tam süit yerelde koşulmaz, CI koşar.
- Kullanıcıya görünen davranış değişirse ayrı söylenir; `hwdec` açmak çözme yolunu
  değiştirir ve karşılaştırma paneli (`EngineComparisonFrameSource`) iki motor
  örneğinin karelerini bayt bayt eşliyor (`OynaticiKarsilastirmaTests`).

## Kullanıcının bu turdaki cümlesi

> "dalga dalga iznimi isteme hipersürüş hedefimiz için maximum yakınlığa ulaşalım
> sonra bilgilendirme yap gerekli yerleri danışmayı ihmal etmiyorsun değil mi"

Önceki turdaki hedef cümlesi:

> "100ms cıvarında hedefimiz var daha kısa olursa daha iyi"
