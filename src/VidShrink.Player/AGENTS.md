# VidShrink.Player

Oynatıcı sekmesinin ve karşılaştırma panelinin motoru. `IPlaybackEngine` motordan bağımsız; uygulaması
`MpvEngine` (libmpv, `vo=libmpv`, SW render, BGRA, stride 4*w, 64 bayt hizalı). osx-arm64 kapısı kapanırsa
LibVLC aynı arayüze girer.

- `Native.cs` — P/Invoke; imzalar `client.h`/`render.h`'den.
- `LibMpvLocator` — sıra: `VIDSHRINK_LIBMPV`, uygulama klasörü, onun ve bir üstünün `tools/libmpv`'si,
  macOS'ta Homebrew/MacPorts `lib`, sistem yolu; yoksa `PlaybackEngineUnavailableException`. libmpv
  arşive girmez: Windows kurucusu sha256'lı indirir, macOS/Linux paket komutunu söyler, CI indirir.
- `MpvEngine` — üç iş parçacığı: çağıran, `mpv-events`, `mpv-render`. Render iş parçacığı yalnız
  `mpv_render_*` çağırır; çağıranın libmpv çağrıları `_handleGate` altında.
- Arama bitişi: aramanın RESTART'ı, komuttan sonra takası biten yeni kare **ve** render'da yarım yeni
  kare yok; olay iş parçacığının saati kareyle kıyaslanmaz. Dönüşte `time-pos` iner. `time-pos` özellik
  olayı yalnız tetik; konum olay işlenirken `time-pos` okunarak yazılır.
- `PlaybackOptions`: `RenderWidth/Height` sabit render ölçüsü, `Audio=false` → `aid=no`, `Video=false` →
  `vid=no`, `Loop` → `loop-file=inf`. `TryCopyLatest(.., out frameSeconds)` karenin `time-pos` damgası.
  Karşılaştırma paneli iki örnek, önizleme sesi bir `vid=no` örnek. Varsayılan `hwdec=no`.

Testler `OynaticiMotorTests.cs`, `OynaticiKarsilastirmaTests.cs`; libmpv yoksa kırmızı.
