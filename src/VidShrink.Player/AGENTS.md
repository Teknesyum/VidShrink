# VidShrink.Player

Oynatıcı sekmesinin motoru. `IPlaybackEngine` motordan bağımsız arayüz; bugünkü uygulaması
`MpvEngine` (libmpv, `vo=libmpv`, `MPV_RENDER_API_TYPE_SW`, BGRA, stride 4*w, 64 bayt hizalı).
osx-arm64 gömme kapısı kapalı kalırsa LibVLC uygulaması aynı arayüzün arkasına girer.

- `Native.cs` — P/Invoke; imzalar `client.h`/`render.h`'den.
- `LibMpvLocator` — sıra: `VIDSHRINK_LIBMPV` (dosya ya da klasör), sonra uygulama klasörü.
  Bulamazsa `PlaybackEngineUnavailableException`. libmpv sürüm arşivine girmez; CI indirir.
- `MpvEngine` — üç iş parçacığı: çağıran, `mpv-events`, `mpv-render`. Render iş parçacığı
  `mpv_render_*` dışında libmpv çağırmaz (render.h "Threading"); video ölçüsü olay
  iş parçacığında okunur.
- Arama bitişi: `MPV_EVENT_SEEK`'ten sonra render'ı başlayan ilk yeni kare (MpvBench ile aynı).
- Çözme varsayılanı `hwdec=no`, `auto-copy` seçenek. Ses libmpv'nin kendi ao'su, cihaz
  yoksa null.

Testler `tests/VidShrink.Tests/OynaticiMotorTests.cs`; libmpv yoksa kırmızı olur, atlanmaz.
