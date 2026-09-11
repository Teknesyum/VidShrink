# VidShrink.Player

Oynatıcı sekmesinin motoru. `IPlaybackEngine` motordan bağımsız arayüz; bugünkü uygulaması
`MpvEngine` (libmpv, `vo=libmpv`, `MPV_RENDER_API_TYPE_SW`, BGRA, stride 4*w, 64 bayt hizalı).
osx-arm64 gömme kapısı kapalı kalırsa LibVLC uygulaması aynı arayüzün arkasına girer.

- `Native.cs` — P/Invoke; imzalar `client.h`/`render.h`'den.
- `LibMpvLocator` — sıra: `VIDSHRINK_LIBMPV` (dosya ya da klasör), uygulama klasörü, onun
  `tools/libmpv`'si, bir üst klasörün `tools/libmpv`'si (Windows kurulumu), macOS'ta
  Homebrew/MacPorts `lib`, en son sistem yolu. Bulamazsa `PlaybackEngineUnavailableException`.
  libmpv sürüm arşivine girmez: Windows kurucusu sha256'lı indirir, macOS/Linux kurucusu
  paket komutunu söyleyip durur, CI kendisi indirir.
- `MpvEngine` — üç iş parçacığı: çağıran, `mpv-events`, `mpv-render`. Render iş parçacığı
  `mpv_render_*` dışında libmpv çağırmaz (render.h "Threading"); video ölçüsü olay
  iş parçacığında okunur. Çağıranın libmpv çağrıları `_handleGate` altında; Dispose
  tutamacı aynı kilitle bırakır.
- Arama bitişi: `MPV_EVENT_SEEK`'ten sonra render'ı başlayan ilk yeni kare **ve** o aramanın
  `MPV_EVENT_PLAYBACK_RESTART`'ı; gecikme ikisinden geç olana kadar. Dönüşte `time-pos` iner.
- Çözme varsayılanı `hwdec=no`, `auto-copy` seçenek. Ses libmpv'nin kendi ao'su, cihaz
  yoksa null.

Testler `tests/VidShrink.Tests/OynaticiMotorTests.cs`; libmpv yoksa kırmızı olur, atlanmaz.
