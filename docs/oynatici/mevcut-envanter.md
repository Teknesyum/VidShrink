# VidShrink Oynatıcı — Mevcut Envanter

Salt okuma amaçlı mimari/özellik envanteri. Kod yazılmadı, değiştirilmedi.

## 1. Mimari

Kod tabanında birbirinden bağımsız **üç "oynatıcı"** var:

**(a) Tek dosya sekmesi — `PlayerView`**
- `PlayerView.axaml.cs` (461 satır) tek başına oynatıcıyı yönetir: `ZoomGesture _zoom`, `FullscreenSwitch _fullscreen`, `StallWatch _stall`, `SeekCoalescer _seek`, `DecoderPipe? _pipe`, `DecoderPipe.ContinuousPlayback? _play`, `AudioSink? _audio` alanları.
- `StartPlayback`: `_pipe.StartContinuousPlayback(atSeconds)` çağırır, 16ms'lik `DispatcherTimer` (`_render`) her tikte `RenderLatest()` çağırır (`PlayerView.axaml.cs:` — RenderLatest `play.TryCopyLatest(ref _shown, DrawBgra)` ile en son kareyi çeker).
- `OpenAsync`: `DecoderPipe` açar, `pipe.HasAudio` ise `AudioSink` bağlar, 500ms'lik watchdog `DispatcherTimer` (`PollStall`) kurar.
- Girdi haritası **App katmanında** `PlayerInputMap.cs` (281 satır) içinde: `Wheel`/`Press`/`Key` statik fonksiyonları donanım olayını `PlayerCommand`'a çevirir; `PlayerView` bunu yorumlar.
- `SeekCoalescer` (`PlayerInputMap.cs:114-206`) — sadece bu sekmede kullanılır; hızlı ardışık `Nudge` çağrılarını tek pump-loop ile biriktirir (`GoTo`→`_pending=true`→`PumpAsync` tek seferde en son hedefe gider).
- Alt katman: `DecoderPipe` (`src/VidShrink.Ffmpeg/Playback/DecoderPipe.cs`, 653 satır) — `StartContinuousPlayback` ffmpeg'i `-re -fps_mode passthrough` ile rawvideo BGRA çıktısı için başlatır; `ContinuousPlayback` iç sınıfı çift tamponlu (`_buffer`/`_front`, `Pump()` içinde kilit altında takas). `SeekAsync`: 128MB tavanlı keyframe-indeksli LRU önbellek → önbellek isabetinde anında, `MaxForwardCatchupFrames=3` içindeyse "yakalama", değilse `RestartVideo` (yeni ffmpeg süreci, `-ss X -skip_frame nokey`). Ses için **ayrı bir ffmpeg süreci** (`SeekAudio`, PCM s16le çıktısı) — video ve ses iki bağımsız boru.
- Ses çıkışı: `AudioSink.cs` (101 satır) — NAudio `WaveOutEvent`+`BufferedWaveProvider`, 48kHz/16bit/stereo, `DesiredLatency=80ms`.
- **`PlaybackClock.cs`** (82 satır, `src/VidShrink.Ffmpeg/Playback/`) — hız-farkında soyut saat sınıfı (`Start/Pause/Resume/Seek/Rate/PositionSeconds`) var ama **kod tabanında hiçbir yerde örneklenmiyor** (grep ile doğrulandı — kendi dosyası dışında referans yok). Yani A/V senkronu için "resmi" bir saat mekanizması yok; gerçek senkron, video ve ses borularının aynı zaman damgasına ayrı ayrı `-re` ile seek edilmesine dayanıyor, aralarında uzlaştırıcı bir kod bulunamadı.

**(b) Karşılaştırma paneli — `ComparisonPanel` + `PanelHost` + `PipeComparisonFrameSource`**
- `MainWindow.axaml.cs:142`: `_preview = new PanelHost(Preview, () => new PipeComparisonFrameSource());` — panel tek bir hstack ffmpeg sürecinden beslenir.
- `PipeComparisonFrameSource.cs` (492 satır): tek ffmpeg süreci → `FrameRing`(4) + `FramePool`; arka plan `Pump()` iş parçacığı; `Play/Pause` ffmpeg sürecini değil **okuyucu iş parçacığını** `ManualResetEventSlim` ile durdurur; `SeekAsync` sürecin **tamamını yeniden başlatır** (DecoderPipe'daki önbellek/yakalama yok).
- `ComparisonGraph.cs` (170 satır) hstack filtre grafiğini kurar: `[0:v]fps=N,scale[l];[1:v]fps=N,scale[r];[l][r]hstack[v]`.
- `ComparisonSurface.cs` (514 satır): tek `WriteableBitmap`'i **iki farklı kırpma dikdörtgeniyle iki kez çiziyor** (split-view airspace sorununu böyle çözüyor); `TopLevel.RequestAnimationFrame` ile sürülüyor (DispatcherTimer değil).
- `PanelHost.cs` (1055 satır) — panel ile kaynak arasında yapıştırıcı. "Handover" deseni: bir segment bitmeden ~160ms önce (`HandoverLeadSeconds=0.16`) bir sonraki segmentin ffmpeg borusu önceden açılır (`BeginHandover/OpenStandbyAsync`), `SwapAt(duration,fps)=duration-1.5/fps` anında `_source` atomik olarak değiştirilir (`SwapToStandby`) — ffmpeg'in ~105ms süreç-başlatma gecikmesini gizlemek için.
- `Seek`: segment-önizleme modunda yeni bir kodlama planlar (`ScheduleClip`, 400ms debounce — `SegmentEncoder`), tam-çıktı modunda doğrudan `source.SeekAsync` (yine süreç yeniden başlatma).
- `ClipSignature` = ffmpeg argüman listesinin kendisi (manuel alan karşılaştırması değil) — "bir şey değişti mi" testinde kullanılıyor.

**(c) Önizleme sesi — `PreviewAudio.cs`** (146 satır): hstack borusu sadece video taşıdığı için, karşılaştırma paneline ses beslemek üzere **üçüncü, bağımsız** bir `DecoderPipe`+`AudioSink` çifti açılır (`AttachAsync`, aynı yol zaten bağlıysa yeniden açmaz).

**Kablolama (`MainWindow.axaml` / `.axaml.cs`):**
- `MainWindow.axaml:192`: `<playback:PlayerView x:Name="Player"/>`
- `MainWindow.axaml:613`: `<playback:ComparisonPanel Grid.Row="0" x:Name="Preview" .../>`
- `MainWindow.axaml.cs:2531`: `internal PlayerView PlayerTab => Player;`
- `MainWindow.axaml.cs:2536,2558`: açılışta `PlayerView.Echo(...)` iz kayıtları.
- Kabuk entegrasyonu: `VidShrink.Core.ShellIntegration.ResolveStartupPath(argv)` komut satırı dosya yolunu çözer, `MainWindow(path)` kurucusu `LoadStartupFileAsync` ile oynatıcı sekmesine geçip dosyayı açar (`tests/VidShrink.Tests/OynaticiGirdiTests.cs:288-320`, `KabukYolununActigiSekmeOynaticidir`).

Diğer ilgili dosyalar: `ZoomGesture.cs` (302 satır, saf C#, Avalonia bağımsız) — `T` (0..1) parametresiyle hem zoom hem panel "gölgeleme" (Band/Mid/Full `ShelterStage`) aynı histerezis makinesinden sürülüyor (`FullAt=1.00`, `FullDropAt=0.92`). `HoverZone.cs` (250 satır) — kontrol çubuğunun otomatik gösterme/gizlemesi, `IHoverClock` test edilebilir saat soyutlaması, Windows `SPI_GETCLIENTAREAANIMATION` P/Invoke ile "azaltılmış hareket" tespiti. `ControlStrip.axaml.cs` (406 satır) — zaman çizelgesi sürükleme, `SetEncodeProgress` ile final-kodlama ilerleme imleci.

## 2. Mevcut özellik listesi

| Özellik | Durum | Kanıt |
|---|---|---|
| Oynat/Duraklat | **VAR** | `PlayerInputMap.cs:83` (sağ tık→TogglePlay), `:90` (Space→TogglePlay); `ControlStrip.axaml.cs` PlayPauseRequested |
| Arama çubuğu / scrub | **VAR** | `ControlStrip.axaml.cs` `OnTimelinePressed/Moved/Released`; `ControlStrip.axaml:` Timeline Grid |
| Tekerlek ile arama | **VAR** | `PlayerInputMap.cs:73-79` `Wheel()` — adım 1/10/60/300 sn (Ctrl/Shift kombinasyonu) |
| Zoom/Pan (tekerlek+Alt, sürükle) | **VAR** | `ZoomGesture.cs` tam sınıf; `PlayerInputMap.cs:75-76` Alt+tekerlek→Zoom |
| Tam ekran | **VAR** | `FullscreenSwitch` (`PlayerInputMap.cs:216-242`), orta tık→ToggleFullscreen (`:84`) |
| Klavye kısayolları | **KISMİ** | Sadece 3 tuş tanınıyor: Space/Menu/Escape (`PlayerInputMap.cs:24-30`, `PlayerView.axaml.cs` `OnKey`); ok tuşuyla arama/ses YOK (ControlStrip'in Timeline'ında Left/Right/PageUp/PageDown var ama sadece karşılaştırma panelinde, odak Timeline'dayken) |
| Sağ tık bağlam menüsü | **VAR** | `PlayerInputMap.MenuRows` (3 satır: playpause/fullscreen/reset), `PlayerView.BuildMenu()`, test: `OynaticiGirdiTestsMenuSatirlari` |
| Ses seviyesi (volume) | **YOK** | `AudioSink.cs` içinde seviye kontrolü yok; Playback klasöründe "Volume" grep boş |
| Sessize alma (mute) | **YOK** | grep boş |
| Oynatma hızı değişimi | **YOK** | `DecoderPipe` sadece `-re` (gerçek zamanlı) ile çalışır, hız parametresi yok; `PlaybackClock.Rate` tanımlı ama sınıf hiç kullanılmıyor |
| Kare kare ilerleme | **YOK** | Playback klasöründe frame-step komutu yok |
| Altyazı | **YOK** | grep boş, ffmpeg argümanlarında `-vf subtitles` vb. yok |
| Ses parçası seçimi | **YOK** | `SeekAudio` sabit `-ar 48000 -ac 2`, parça seçimi parametresi yok |
| Klip dışa aktarma (kullanıcı kararıyla dosyaya kaydetme) | **YOK/KISMİ** | `SegmentEncoder.cs` sadece geçici, en fazla 2 adet (`KeepClips=2`) otomatik silinen ÖNİZLEME klipleri üretir (`TempPrefix="vidshrink_preview"`); kullanıcının seçtiği bir "dışa aktar" komutu bulunamadı |
| Bilgi paneli | **KISMİ** | `TxtState` sadece konum/oynatma durumu/zoom% gösterir (`main.player.state` = "position {0} s - {1} - zoom {2}%"); çözünürlük/codec/bit hızı gibi tam bilgi paneli yok |
| Sürükle-bırak | **KISMİ** | `MainWindow.axaml.cs:160-163` (`DragDrop.SetAllowDrop`, `OnDrop`) var ama bu ana pencere/sıkıştırma akışı seviyesinde; `_cts is null` koşuluyla sıkıştırma sürerken engelleniyor; doğrudan oynatıcı sekmesine özel bir sürükle-bırak değil |
| Son açılanlar listesi | **YOK** | grep boş |
| Kaldığı yerden devam (resume position) | **YOK** | grep boş; `SeekCoalescer`/`DecoderPipe` konum durumu kalıcı hale getirilmiyor |
| Takılma tespiti (stall watch) | **VAR** | `StallWatch` (`PlayerInputMap.cs:244-281`), `main.player.stalled` lokalizasyon anahtarı |
| Karşılaştırma/split-view oynatıcı | **VAR** | `ComparisonPanel`+`PanelHost`+`PipeComparisonFrameSource`, hstack filtre grafiği |
| Gapless segment geçişi (handover) | **VAR** | `PanelHost.cs` `BeginHandover/SwapToStandby`, `HandoverLeadSeconds=0.16` |
| Ekran görüntüsü alma | **YOK** | grep boş |

## 3. Mimarinin sınırları (yeni özellik eklerken karşılaşılacak engeller)

- **Hız değişimi (`-re` sabiti):** `DecoderPipe.StartContinuousPlayback` ve `PipeComparisonFrameSource` her ikisi de ffmpeg'i sabit `-re` (gerçek zamanlı, kaynağın kendi hızında) bayrağıyla çağırıyor. Oynatma hızını değiştirmek (0.5x/2x gibi) için mevcut mimaride ffmpeg argümanına parametrik bir `-re` alternatifi (örn. `setpts=N/PTS` filtresi + yeniden hesaplanan `-r`/`-fps_mode`) eklenmesi ve her hız değişiminde **sürecin yeniden başlatılması** gerekir — DecoderPipe'ın önbellek/yakalama mantığı sadece "aynı hızda ileri/geri seek" için tasarlı, hız çarpanını hesaba katmıyor.
- **Altyazı render'ı:** İki boru da çıkışı çıplak `rawvideo BGRA` olarak alıyor (`ComparisonGraph.BuildFilter`, `DecoderPipe`'ın rawvideo çıkışı); ffmpeg tarafında `subtitles=` filtresi eklenebilir ama bu, filtre grafiğini (hstack için zaten karmaşık olan `ComparisonGraph.BuildFilter`) değiştirmek ve her platformda libass bağımlılığının var olduğunu doğrulamak anlamına gelir — şu an hiçbir doğrulama/probe yok (yalnızca `HstackWorks` probe'u var, altyazı için karşılığı yok).
- **Ses parçası seçimi:** `SeekAudio` sabit `-ar 48000 -ac 2` ile PCM akışı üretiyor, ffmpeg'e hangi `-map 0:a:N` akışının seçileceği parametrize edilmemiş; ayrıca video ve ses **iki ayrı süreç** olduğu için akış seçimi her iki tarafta senkronize güncellenmeli (video tarafı zaten akış seçmiyor).
- **ffmpeg süreç-başlatma maliyeti:** Hem `DecoderPipe.RestartVideo` hem `PipeComparisonFrameSource.SeekAsync` seek'te **yeni ffmpeg süreci** başlatıyor; `PanelHost`'un "handover" mekanizması bu maliyeti (~105ms, koddaki yorumdan) sadece segment sonunda önceden bilinen geçişler için gizliyor — rastgele/anlık bir "hız değiştir" veya "altyazı aç/kapa" komutu için böyle bir önceden-açma stratejisi yok, her değişiklik muhtemelen görünür bir donma yaratır.
- **`PlaybackClock` ölü kod:** A/V senkronu için soyut bir saat sınıfı var ama hiç kullanılmıyor; hız veya altyazı zamanlaması gibi yeni bir zaman-bağımlı özellik eklenirken bu sınıfı diriltmek cazip görünebilir ama şu an hiçbir çağıran kod yok — sıfırdan entegre edilmesi gerekir, "kullanılıyor" diye güvenilemez.
- **Üç bağımsız oynatıcı yolu:** Tek dosya (`PlayerView`+`DecoderPipe`), karşılaştırma paneli (`PanelHost`+`PipeComparisonFrameSource`) ve önizleme sesi (`PreviewAudio`+ayrı `DecoderPipe`) birbirinden kopya kodla ayrı akışlar; herhangi bir oynatıcı-geneli özellik (örn. hız, altyazı, ses parçası) üç yerde ayrı ayrı uygulanmak zorunda — ortak bir "oynatıcı çekirdeği" abstraksiyonu yok.

## 4. Test düzeni

- Tek gerçek Avalonia çalışma zamanı: `AppHost.cs` (`tests/VidShrink.Tests/AppHost.cs`) süreç başına bir kez, **ayrı bir iş parçacığında** `AppBuilder` kurar — Windows'ta `UseWin32()` (gerçek pencere), diğer platformlarda `UseHeadless(...)` (`AppHost.Backend`). **Mock/sahte Avalonia yok** — gerçek `PlayerView`, `MainWindow`, `ComparisonPanel` nesneleri oluşturuluyor, olaylar `RaiseEvent` ile gerçek `RoutingStrategies` üzerinden gönderiliyor.
- Girdi simülasyonu gerçek olay nesneleriyle yapılıyor: `OynaticiGirdiTests.cs:44-93` `GirdiSurucu` sınıfı — `PointerWheelEventArgs`/`PointerPressedEventArgs`/`KeyEventArgs` inşa edip `view.RaiseEvent(...)` çağırıyor (mock yok, gerçek Avalonia olay yolu).
- Bazı testler **gerçek ffmpeg** kullanıyor (`[FfmpegAvailableFact]`/`[FfmpegFact]` özel attribute'ları — ffmpeg mevcut değilse testi atlıyor): `GirdiKlipFixture` (`OynaticiGirdiTests.cs:361-412`) 20 saniyelik gerçek bir test klibi üretip `DecoderPipe`'ı gerçek dosyaya karşı açıyor (`OynaticiGirdiTestsGercekBoru.OnHizliTikGercekBoruyaKarsiBirikirVeAramalarSinirdaKalir`, satır 449-492) — burada `SeekCoalescer`, gerçek `DecoderPipe.SeekAsync`'e karşı ölçülüyor, gecikme (`LatenciesMs`) 150ms sınırına karşı doğrulanıyor.
- `PanelHostTests.cs`: gerçek ffmpeg gerektirmeyen testler için **elle yazılmış sahte kaynak** var — `SessizKaynak : IComparisonFrameSource` (satır 55) arayüzü uygulayan minimal bir sınıf; `AppHost.Run(() => new PanelHost(new ComparisonPanel(), () => new SessizKaynak(), encoder))` (satır 81) deseniyle gerçek `ComparisonPanel` + sahte kaynak birleştiriliyor. Gerçek boru gerektiren testler `[FfmpegFact]` ile `PipeComparisonFrameSource` kullanıyor (satır 630).
- Hem başarı hem "kanıt dosyası" deseni tekrarlanıyor: her test `AppHost.Run(() => {...})` içinde çalışır, ölçülen değerler bir `StringBuilder`'a yazılır ve `GirdiKanit.Write("kN-ad.txt", ...)` ile `.calisma/T176/` klasörüne insan-okunabilir kanıt olarak kaydedilir (kural: "rapora giren sayı görülebilir olsun").
- Yeni bir özellik testi eklemek için izlenecek örnek desen: `OynaticiGirdiTests.TekerlekAdimlariHaritadakiDortSayidir` (satır 161-173) gibi **saf mantık testi** (mock gerektirmez, `PlayerInputMap` statik fonksiyonlarını doğrudan çağırır) VEYA `MenuDugmesiBaglamMenusunuAcarVeUcSatirTasir` (satır 256-285) gibi **gerçek `PlayerView` + `AppHost.Run`** deseni (gerçek nesne kur, gerçek olay gönder, `view.Trace` ile iç izi doğrula) — ffmpeg'e ihtiyaç yoksa ikinci desen, gerçek boru davranışı ölçülecekse `[FfmpegFact]` + `GirdiKlipFixture` deseni izlenmeli.
- Test dosyaları (yalnızca canonical `tests/VidShrink.Tests/`, `.calisma/T155-denetim/klon/` ve `.claude/worktrees/T191-audit/` altındaki kopyalar göz ardı edildi): `OynaticiGirdiTests.cs`, `OynaticiBoruTests.cs`, `PanelHostTests.cs`, `ComparisonPanelTests.cs`, `ZoomGestureTests.cs`, `SegmentEncoderTests.cs`, `PreviewSegmentTests.cs`, `AdvancedPanelTests.cs`, `CodecLockTests.cs`, `HardwareRateControlTests.cs`, `PlaybackPanelTests.cs`.
- **"SeekCoalescer" araması:** terim yalnızca `PlayerInputMap.cs` (tanım) ve `OynaticiGirdiTests.cs` (kullanım/test) içinde geçiyor — `PanelHost`/`ComparisonPanel` tarafında karşılığı yok; karşılaştırma paneli kendi debounce mekanizmasını (`SegmentEncoder`'ın 400ms'lik `DebounceMilliseconds`) ayrı olarak uyguluyor. İki oynatıcı arasında paylaşılan tek bir "arama biriktirme" bileşeni yok.

## 5. Renk/ölçü kuralı

`Themes/Theme.axaml` ölçü belirteçlerini tanımlıyor, ayrıca `Themes/Palette/*`'teki `*Color` anahtarlarını sarmalayan fırça (`SolidColorBrush`) kaynaklarını da burada topluyor:

- **Ölçü belirteçleri** (`Theme.axaml`): `SectionMargin` (satır 324, `0,16,0,0`), `SpaceMd`/`SpaceSm` (satır 301-302, `12`/`8`), `PanelPadding`, `RadiusPanel`/`RadiusPanelScalar` — oynatıcı XAML'leri (`PlayerView.axaml`, `ComparisonPanel.axaml`, `ControlStrip.axaml`) bunları `Margin`/`Spacing`/`CornerRadius` için kullanıyor.
- **Renk belirteçleri**: `AppBg` (satır 10, `AppBgColor`'a sarılı), `NeonEmber` (satır 22, `NeonEmberColor`'a sarılı) — ikisi de asıl renk değerini `Themes/Palette/` altındaki palet dosyasından alıyor, `Theme.axaml` sadece fırça sarmalayıcısı.
- **Stil/tipografi belirteçleri**: `H2` (başlık teması), `GhostButton` (menü düğmesi teması), `FontMono` (satır 287, Consolas/Cascadia Mono ailesi), `Panel` (Border teması), `MonoValue` (durum metni teması) — `PlayerView.axaml` bunların hepsini kullanıyor (satır 13,18,19,25,30,37,39).
- Oynatıcı dosyalarında elle yazılmış renk/ölçü sabiti bulunamadı; hepsi `StaticResource` üzerinden geliyor — AGENTS.md'deki "renk yalnız Palette, ölçü yalnız Theme.axaml" kuralına uygun.

---
*Bu rapor salt okuma bulgularına dayanır; hiçbir kaynak dosya değiştirilmedi.*
