# VidShrink.Player

Oynatıcı motoru. `IPlaybackEngine` motordan bağımsız; uygulaması `MpvEngine` (libmpv, `vo=libmpv`,
SW render, BGRA, stride 4*w, 64 bayt hizalı). osx-arm64 kapısı kapanırsa LibVLC aynı arayüze girer.

- `Native.cs` — P/Invoke; imzalar `client.h`/`render.h`'den.
- `LibMpvLocator` — sıra: `VIDSHRINK_LIBMPV`, uygulama klasörü, onun ve bir üstünün `tools/libmpv`'si,
  macOS'ta Homebrew/MacPorts `lib`, sistem yolu; yoksa `PlaybackEngineUnavailableException`. libmpv
  arşive girmez: Windows kurucusu sha256'lı indirir, macOS/Linux paket komutunu söyler, CI indirir.
- `MpvEngine` — üç iş parçacığı: çağıran, `mpv-events`, `mpv-render`. Render iş parçacığı yalnız
  `mpv_render_*` çağırır (render.h "Threading"); video ölçüsü olay iş parçacığında okunur. Çağıranın
  libmpv çağrıları `_handleGate` altında; Dispose tutamacı aynı kilitle bırakır.
- Arama bitişi: yanıttan sonra gelen `MPV_EVENT_SEEK`'le silahlanan aramanın RESTART'ı, komuttan sonra
  takası biten yeni kare **ve** render'da yarım yeni kare yok. mpv ilk kareyi RESTART'tan önce render
  eder; olay iş parçacığının saati kareyle kıyaslanmaz. Gecikme ikisinden geç olana; dönüşte `time-pos` iner.
- `time-pos` özellik olayı yalnız tetik: aramada yakalanan değer (hedef) yük altında RESTART'tan sonra
  gelir. Konum olay işlenirken `time-pos` okunarak yazılır.
- Çözme varsayılanı `hwdec=no`, `auto-copy` seçenek. Ses libmpv'nin ao'su, cihaz yoksa null.

Testler `tests/VidShrink.Tests/OynaticiMotorTests.cs`; libmpv yoksa kırmızı olur, atlanmaz.
