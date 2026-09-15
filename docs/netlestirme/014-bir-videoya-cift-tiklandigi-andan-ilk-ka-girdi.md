[[netlestirme:014]]

# Netleştirme: Bir videoya cift tiklandigi andan ilk karenin ekranda gorunmesine kadar gecen su

İşe başlamadan önce soruyu keskinleştir. Görüş verme, plan yazma, kod yazma.
Yalnız şunu döndür: soruda belirsiz kalan yerler, her biri için tek satırlık bir netleştirme sorusu, en fazla beş. Belirsizlik yoksa "net" yaz.

## Soru

Bir videoya cift tiklandigi andan ilk karenin ekranda gorunmesine kadar gecen sureyi milisaniyeler mertebesine indirecek bir hipersurus tasarisi istiyorum. Bugun bu sure sicak 1857,8 ms / soguk 1586,0 ms. Guncelleme isi gerekirse ertelenecek; bu konu muhim. Elimizdeki olculer ve zincirin tamami asagida. En verimli yol nedir: hangi adimlar kaldirilir, hangileri ertelenir, hangileri paralellesir, hangi mpv secenekleri degisir, ve bunlarin her birinin tahmini kazanci nedir? Sirali, olculebilir bir tasari ver.

## Elde olan olgular

# Olgular — VidShrink soğuk açılış, çift tıktan ilk kareye

Hepsi kaynak okunarak toplandı; ölçümler deponun kendi aracından.

## Ölçülmüş süreler

`tools/acilis-hizi/olcum.ps1` + `src/VidShrink.App/MainWindow.AcilisIzi.cs` (env
`VIDSHRINK_ACILIS_IZI`, sıfır noktası `Process.StartTime`). Sonuçlar
`docs/olcumler/acilis-hizi.md`:

- `ilk-kare` ortancası: **sıcak 1857,8 ms / soğuk 1586,0 ms**
- `sekme` → `ilk-kare` arası ~**330 ms**; ölçüm bunun libmpv yüklemesi değil
  `mpv_create` + ilk çözme işi olduğunu gösteriyor (`acilis-hizi.md:96-108`)
- libmpv ön yüklemesi (arka planda) sıcak 74,0 ms / soğuk 67,1 ms
- Ölçü 20 adımı kaydediyor. `tools/acilis-hizi` `.sln`'e eklenmemiş, CI'da koşmuyor.
- `tests/VidShrink.Tests/` altında bu yolun **süresini ölçen test yok**.

## Zincir

Çift tıkta çalışan komut: `"<KurulumKoku>\VidShrink.exe" "%1"` — yani **başlatıcı**,
app değil (`Install-VidShrink.ps1:445,453,456,722`). İki süreç var; ölçümün saati
app sürecinin `Process.StartTime`'ından başlıyor, **başlatıcının harcadığı süre
ölçüme hiç girmiyor**.

### A. Başlatıcı süreci (`VidShrink.exe`) — hepsi app doğmadan önce

| Yer | İş |
|---|---|
| `Launcher/Program.cs:74` | `UpdateCheck.ReadVersionMarker` — disk okuması |
| `Launcher/Program.cs:80` | `SplashGate.Arm` — 400 ms eşikli sayaç, hemen döner (`Splash.cs:25,58-63`) |
| `Launcher/Program.cs:86` | `LauncherUpdate.Repair` — yarım kalan başlatıcı değişimi |
| `Launcher/Program.cs:92` | `SeedVersionMarker` — **her açılışta disk yazımı** |
| `Launcher/Program.cs:97` | `UpdateStage.ResumePending` — bekleyen güncelleme dosyalarını yerine taşır, **yüzlerce MB kopyalama olabilir** |
| `Launcher/Program.cs:115` | `progress.WriteLog` — `update-log.txt`, her açılışta |
| `Launcher/Program.cs:116` → `Splash.cs:122,125` | `SplashGate.Dispose` — panel çizildiyse **2+2 sn tavanlı join** |
| `Launcher/Program.cs:118` | `RecordAppliedUpdate` |
| `Launcher/Program.cs:121,222-247` | `ToolsPresent` — ffmpeg/ffprobe varlık + **PATH taraması**. Oynatma ffmpeg kullanmıyor |
| `Launcher/Program.cs:129-139` | `Process.Start` — **app burada doğuyor, ölçünün sıfır noktası** |
| `Launcher/Program.cs:145-149` | `Updater.Run` — güncelleme yoklaması/indirmesi, **app başlatıldıktan sonra** (zaten doğru tarafta) |

### B. App süreci — ilk kareye kadar engelleyen işler

| Yer | İş |
|---|---|
| `App/Program.cs:182-188` | `WarmPlayback()` — `Task.Run` ile `LibMpvLocator.EnsureLoaded()`. **Zaten arka planda** |
| `App.axaml.cs:42` | `AvaloniaXamlLoader.Load` — App.axaml + tema sözlükleri |
| `App.axaml.cs:52-56` | `TempCleanup` — `Task.Run`, arka planda (bilinçli, −94 ms) |
| `App.axaml.cs:62,64` | `AppSettings.Load` + `PaletteCatalog.Use` — **senkron**; pencereden önce, bir kare yanlış renk çizilmesin diye (bilinçli ödün) |
| `App.axaml.cs:71` | `RegisterFileTypes` — **senkron kayıt defteri yazımı**, oynatmayla ilgisiz |
| `MainWindow.axaml.cs:149` | `InitializeComponent()` — **dört sekmenin tamamının XAML'i** (küçültme, dönüştürme, kaydedici, ayarlar, gelişmiş paneller, karşılaştırma paneli). Kullanıcı yalnız oynatıcıyı görecek |
| `MainWindow.axaml.cs:155` | `new PanelHost(... EngineComparisonFrameSource)` |
| `MainWindow.axaml.cs:169-265` | Liste/tema/dil kurulumları, **~60 `Watch` bağlaması**, `InitializeAdvancedUi`, `LoadTitleBarLogo` (asset + Bitmap, `:474-487`), `SetupShellMenu`, dört özet tazelemesi |
| `MainWindow.axaml.cs:500-508` | `UpdateSettings.Load` + `AppSettings.Load` + `UseLanguage` + `RestoreSettings` + `InitializeShareUi` + `InitializeUpdateUi` — **senkron, arayüz iş parçacığında** |
| `MainWindow.axaml.cs:510` | `PlayPanelEntrance()` — giriş canlandırması |
| `MainWindow.axaml.cs:2782` | `Tabs.SelectedIndex = PlayerTabIndex` |
| `MainWindow.axaml.cs:2786` → `PlayerView.axaml.cs:523` | `Player.OpenAsync(path)` |
| `PlayerView.axaml.cs:529` | **`new MpvEngine()` — arayüz iş parçacığında senkron** |
| `MpvEngine.cs:82` | `mpv_create()` |
| `MpvEngine.cs:90` | **`mpv_initialize()` — soğuk açılışın en pahalı tek çağrısı** |
| `MpvEngine.cs:96-104` | `mpv_render_context_create` (SW render) |
| `MpvEngine.cs:112-115` | `mpv-events` + `mpv-render` iş parçacıkları |
| `MpvEngine.cs:227` | `loadfile` — buradan sonrası `await`, arayüz serbest |
| `MpvEngine.cs:602,700-704` | `MPV_EVENT_FILE_LOADED` → süre, ses izi sayımı, `ReadVideoSize()` |
| `PlayerView.axaml.cs:549` | `PlaybackHistory.Load` — **senkron dosya okuması** |
| `PlayerView.axaml.cs:550,554` | `ApplyAdvanced` + `ApplyTrackOptions` — 8+ mpv özellik yazımı, `_handleGate` altında |
| `PlayerView.axaml.cs:557-558` | `StartWatchdog()` (500 ms) + `StartRender()` — **16 ms'lik `DispatcherTimer`** |
| `PlayerView.axaml.cs:559-561` | `_history.ResumeFor` + `_seek.GoTo(resume)` — kaldığı yere arama |
| `PlayerView.Window.cs:252-253` | `PlayerSettings.Load` + `RecentFiles.Load` — iki senkron okuma |
| `PlayerView.Window.cs:163` | **`RecentFiles.Save` — senkron disk yazımı, `Play()`'den ÖNCE** |
| `PlayerView.axaml.cs:563` | `TogglePlay()` → `MpvEngine.cs:505` `set pause no` — **asıl oynatma buradan** |
| `MpvEngine.cs:766-775,823` | `RenderLoop` → `mpv_render_context_render`, BGRA arka tampon |
| `PlayerView.axaml.cs:628-648` | `DrawFrame`: `WriteableBitmap` + `FramePixels.CopyRows` + `Frame.Source = _bitmap` (`:647`) — **ilk kare işareti** |

### C. İlk kareden sonra (doğru tarafta)

ffprobe yoklaması (`MainWindow.axaml.cs:2726`), karmaşıklık ölçümü (`:2742`),
varsayılan uygulama önerisi (`:519`), güncelleme yoklaması (`:521`), ffmpeg sürümü
(`:522`), donanım kodlayıcı yoklaması (`:523`), güncelleme indirmesi
(`Launcher/Program.cs:147`), geçici temizlik (`App.axaml.cs:52`). Küçük resim
üretimi açılış yolunda hiç yok.

## mpv seçenekleri (`MpvEngine.cs:140-157`, `mpv_initialize` öncesi)

| Seçenek | Değer | Satır |
|---|---|---|
| `vo` | `libmpv` — **SW render, BGRA**, kareler CPU'da kopyalanıyor | `:142` |
| `hwdec` | **varsayılan `no`**; yalnız `HardwareDecoding.AutoCopy` ise `auto-copy` | `:143` |
| `keep-open` | `yes` | `:144` |
| `idle` | `yes` | `:145` |
| `pause` | **`yes`** — dosya duraklatılmış açılıyor, `Play()` sonradan | `:146` |
| `terminal`, `input-default-bindings`, `input-vo-keyboard`, `load-scripts`, `osd-level`, `osd-bar`, `sub-auto`, `sid`, `audio-display` | kapalı / `no` / `0` | `:147-155` |
| `audio-fallback-to-null` | `yes` | `:156` |
| `aid=no` / `vid=no` / `loop-file=inf` | koşullu | `:134-136` |

**`cache` ve `demuxer*` seçeneklerine hiç dokunulmuyor** — libmpv varsayılanları
geçerli.

## Yığın ve kısıtlar

- .NET 8, Avalonia 12.1.2, tek test projesi `tests/VidShrink.Tests`.
- Oynatma motoru libmpv (`src/VidShrink.Player/MpvEngine.cs`, 981 satır); libmpv
  pakete girmiyor, Windows kurucusu sha256'lı indiriyor.
- İki süreç: `VidShrink.exe` (başlatıcı) + `app/VidShrink.App.exe`.
- Tek örnek kanalı var: `App/Program.cs:152` `SingleInstanceChannel`; ikinci çift tık
  yolu çalışan örneğe iletip çıkıyor (`Program.cs:158-166`).
- Renk yalnız palet dosyasından, ölçü yalnız `Themes/Theme.axaml` belirteçlerinden
  gelir; yeni sayı uydurulmaz.
- `main`e yalnız T0 birleştirir.
