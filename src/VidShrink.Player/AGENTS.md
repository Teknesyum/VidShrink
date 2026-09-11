# VidShrink.Player

Oynatıcı sekmesinin ve karşılaştırma panelinin motoru. `IPlaybackEngine` motordan bağımsız
arayüz; bugünkü uygulaması `MpvEngine` (libmpv, `vo=libmpv`, `MPV_RENDER_API_TYPE_SW`, BGRA,
stride 4*w, 64 bayt hizalı). osx-arm64 gömme kapısı kapalı kalırsa LibVLC aynı arayüze girer.

- `Native.cs` — P/Invoke; imzalar `client.h`/`render.h`'den.
- `LibMpvLocator` — sıra: `VIDSHRINK_LIBMPV`, uygulama klasörü, onun ve bir üstünün
  `tools/libmpv`'si, macOS'ta Homebrew/MacPorts `lib`, sistem yolu; yoksa
  `PlaybackEngineUnavailableException`. libmpv arşive girmez; kurucular ve CI indirir.
- `MpvEngine` — iş parçacıkları: çağıran, `mpv-events`, `mpv-render`. Render iş parçacığı
  `mpv_render_*` dışında libmpv çağırmaz; çağıranın çağrıları `_handleGate` altında.
- Arama bitişi: `MPV_EVENT_SEEK`'ten sonra render'ı başlayan ilk yeni kare **ve** o aramanın
  `MPV_EVENT_PLAYBACK_RESTART`'ı. Dönüşte `time-pos` iner. Varsayılan `hwdec=no`.
- `PlaybackOptions`: `RenderWidth/Height` sabit render ölçüsü, `Audio=false` → `aid=no`,
  `Video=false` → `vid=no`, `Loop` → `loop-file=inf`. `TryCopyLatest(.., out frameSeconds)`
  karenin `time-pos` damgasını verir. Karşılaştırma paneli iki örnek, önizleme sesi bir
  `vid=no` örnek (`App/Playback/EngineComparisonFrameSource`, `PreviewAudio`).

- Goruntu: `video-rotate`, `vf @vsmirror:hflip`, `video-aspect-override`; ekran goruntusu
  `screenshot-to-file .. video` (kaynak cozunurlugu), bilgi `track-list` + `file-size`.

Testler `OynaticiMotorTests.cs`, `OynaticiKarsilastirmaTests.cs`; libmpv yoksa kırmızı.
