# VidShrink.Player

Oynatıcı sekmesinin ve karşılaştırma panelinin motoru. `IPlaybackEngine` motordan bağımsız; uygulaması `MpvEngine`
(libmpv, `vo=libmpv`, SW render, BGRA, stride 4*w, 64 bayt hizalı). osx-arm64 kapısı kapanırsa LibVLC aynı arayüze girer.

- `Native.cs` — P/Invoke; imzalar `client.h`/`render.h`'den.
- `LibMpvLocator` — sıra: `VIDSHRINK_LIBMPV`, uygulama klasörü, onun ve bir üstünün `tools/libmpv`'si, macOS'ta
  Homebrew/MacPorts `lib`, sistem yolu; yoksa `PlaybackEngineUnavailableException`. libmpv arşive girmez.
- `MpvEngine` — üç iş parçacığı: çağıran, `mpv-events`, `mpv-render`. Render iş parçacığı yalnız
  `mpv_render_*` çağırır; çağıranın libmpv çağrıları `_handleGate` altında.
- Arama bitişi: aramanın RESTART'ı, komuttan sonra takası biten yeni kare **ve** render'da yarım yeni kare yok.
  Dönüşte `time-pos` iner; `time-pos` özellik olayı yalnız tetik, konum olay işlenirken okunarak yazılır.
- `PlaybackOptions`: `RenderWidth/Height`, `Audio=false` → `aid=no`, `Video=false` → `vid=no`, `Loop` → `loop-file=inf`.
  `TryCopyLatest(.., out frameSeconds)` karenin `time-pos` damgası. Varsayılan `hwdec=no`, `sid=no`.
- Parçalar: `Tracks`, `aid`/`sid`, `sub-add`, gecikme, `sub-scale`/`sub-pos`; `sub-codepage` değişince dış
  altyazılar `sub-reload` ile yeniden okunur. Görüntü: `video-rotate`, `vf @vsmirror:hflip`,
  `video-aspect-override`; ekran görüntüsü `screenshot-to-file .. video`, bilgi `track-list` + `file-size`.
  Arayüze yalnız varsayılan gövdeli üyeler eklenir.
- Gelişmiş (`MpvEngine.Advanced.cs`): renk, ton, keskinlik, kırpma, ekolayzer ve normalleştirme etiketli `vf`/`af`
  halkaları (`@vscolor`…); altyazı biçimi ve `volume-max` özellikten. Geri okumada mpv'nin `%uzunluk%` kaçışı ayıklanır.
Testler `OynaticiMotorTests.cs`, `OynaticiKarsilastirmaTests.cs`, `OynaticiParcaTests.cs`, `OynaticiGorunumTests.cs`.
