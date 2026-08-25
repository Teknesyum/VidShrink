---
name: vidshrink-panel-terfi-deseni
description: VidShrink'te bir paneli MainWindow'a dokunmadan program boyu kaplatmak — OverlayLayer'a taşı, bandında yer tutucu bırak
metadata:
  type: project
---

Bir paneli "diğer panellerin üstünde" göstermek gerektiğinde `MainWindow` düzenine
dokunmaya gerek yok. Panel kendi kabuğunu `OverlayLayer.GetOverlayLayer(this)` ile
bulduğu katmana taşır, bandında aynı boyutta bir yer tutucu bırakır.

**Why:** VidShrink arayüz sözleşmelerinin `owns` listesi `MainWindow.axaml`'ı çoğu zaman
vermiyor (bkz. [[vidshrink-owns-listesi-daraltiyor]]). Terfi mantığı panelin kendi
dosyasında durursa sözleşme kendi başına bitirilebiliyor.

**How to apply:**

- `OverlayLayer` bir `Canvas`: çocuğu istediği boyutta yerleşir. Bu yüzden `Canvas.Left`,
  `Canvas.Top`, `Width` ve `Height` **açıkça** yazılır; hizalama işe yaramaz.
- Geçiş bu dört özelliğe `Transitions` ile verilir — kabuğa uygulanmış olur, görüntüye değil.
- Kaplama boyutu için `overlay.SizeChanged`'e abone ol, iniş sırasında aboneliği kaldır.
- İniş bitişini `DispatcherTimer` ile bekle; ancak o zaman kabuğu katmandan alıp kendi
  ızgarasına geri koy ve `Canvas.Left/Top` ile `Width/Height` değerlerini `ClearValue` yap.
- `Esc` için terfi süresince `TopLevel`'a `RoutingStrategies.Tunnel` ile bir `KeyDown`
  dinleyicisi tak, inişte sök.
- Azaltılmış hareket: `MainWindow` bunu `SystemParametersInfoW(0x1042)` ile okuyor;
  panel de aynı çağrıyı yapar ve açıksa `Transitions` hiç kurulmaz.
