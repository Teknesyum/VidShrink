# Kaydedicide Küçük Kip: Mini Denetim Arayüzü Saha Taraması

Tarih: 15 Eylül 2026
Kapsam: 31 araç — ekran kaydı, makro kaydı, ekran görüntüsü ve yayın denetimi yapan,
küçük/kompakt/hep üstte bir yüzey sunan programlar.

Bu belge tek bir soruyu kovalıyor: **kaydedici küçüldüğünde ne kalır, ne gider?**
Kodek, bit hızı, hedef boyut tarafı burada yok; onlar
`docs/arastirma/ekran-kaydedici-ozellik-karsilastirmasi.md` ve
`docs/arastirma/kaydedici-otomatik-ayar-ve-hedef-boyut.md` belgelerinde.
Bölge seçimi ve çerçeve `docs/arastirma/kaydedici-bolge-ve-cerceve.md` belgesinde.

**Doğrulama kuralı.** Her piksel değerinin yanında onu okuduğum dosya ve satır var.
Kaynak kodda sabit bulamadığım ölçü **"bilinmiyor"** yazıyor — ekran görüntüsünden
göz kararı ölçmedim, ürün sayfasının "small toolbar" ifadesini sayıya çevirmedim.
Bölüm 6'daki bağlantılar bu okumaların tamamını taşıyor.

**Ölçü birimi uyarısı.** WPF (ScreenToGif, Captura, akon47) ve Avalonia (ShareX, VidShrink)
sayıları aygıttan bağımsız birimdir; %100 ölçekte piksele eşittir, %150'de 1,5 katına
çıkar. GTK (Peek, Kooha) sabitleri mantıksal piksel, macOS (Cap, recordbuddy) sabitleri
nokta. Tabloda hepsi yazıldığı gibi duruyor, ölçekle çarpılmadı.

---

## 1. Tablo

Yıldız sayıları 15 Eylül 2026'da `gh api repos/<ad>` ile okundu.

| Proje | Yıldız | Kompakt kip ölçüsü | Düğme sayısı |
|---|---|---|---|
| ShareX (ScreenRecordWindow) | 39.582 | 461×42 tam; etiketsiz 277×42; sayaçsız+etiketsiz **165×42** | 4 |
| ScreenToGif (kompakt kip) | 27.651 | başlık 30 + şerit **32**, pencere ≥220 geniş | 7 (+ 2 başlıkta) |
| Kap | 19.354 | çubuk 464×64; kayıt sırasında **çubuk yok**, tepsi simgesi | 1 (tepsi) / 7 (çubuk) |
| Cap (desktop-gpui) | 22.257 | panel 320×150, görünür kart **yükseklik 40** | 4–5 + sürükleme tutamağı |
| Screenity (tarayıcı) | 18.692 | yükseklik **48**, genişlik içerik kadar | ~10 |
| LICEcap | 5.595 | diyalog 551×335 DLU, alt şerit ~17 DLU | 3 |
| Kooha | 3.510 | pencere **220×230**, yeniden boyutlanmaz | 6 ana + 2 kayıtta |
| SimpleScreenRecorder | 2.892 | kompakt kip yok; "Hide window" ile tepsiye iner | 0 (tepsi) |
| Pulover's Macro Creator | 2.021 | bilinmiyor (yalnız "Always On Top" seçeneği belgeli) | bilinmiyor |
| vokoscreenNG | 1.505 | kompakt kip yok; ana pencere 845×580 | — |
| Peek | 10.552 | min 60×60; varsayılan 500×300; **<400 px'de etiketler düşer** | 3–4 |
| Captura | 10.832 | kompakt kip yok; tek pencere `MaxWidth=440`, yükseklik içerik kadar | bilinmiyor |
| Blue Recorder | 594 | GTK headerbar, sabit piksel yok | bilinmiyor |
| Green Recorder (arşiv) | 626 | GTK headerbar, sabit piksel yok | bilinmiyor |
| Fluent Screen Recorder | 565 | CompactOverlay **412×260**; kayıtta **412×88**; min 412×88 | 1 + 2 açılır liste |
| akon47/ScreenRecorder | 547 | ana pencere **yükseklik 72**, ≥240 geniş, hep üstte | bilinmiyor |
| atbswp (TinyTask klonu) | 741 | sabit yok; `main_sizer.Fit()` ile içeriğe oturur | 7 |
| PyMacroRecord | 616 | **350×200**, yeniden boyutlanmaz | 2–3 |
| open-recorder | 86 | HUD genişlik **760**, yükseklik bilinmiyor | bilinmiyor |
| TinyTask-macOS (TinyRecorder) | 38 | kayıt HUD'u **genişlik 320**, yükseklik dinamik (~240) | 1 (HUD'da Stop) |
| alierenaltindag/TinyTask | 13 | **350×650** sabit | 4 |
| recordbuddy | 0 | kurulum 860×62 → aktif kayıt **190×54** | 3–4 |
| OBS Omni-Bar (eklenti) | 0 | "compact" stil var, piksel sabiti bilinmiyor | yapılandırılabilir |
| obs-menu-recorder (RecBar) | 0 | yalnız macOS menü çubuğu, pencere yok | 3 + "•••" |
| PowerToys Video Conference Mute | 138.678 | **kaldırıldı** (v0.88.0); ölçü DPI'ya bağlı | 2 |
| TinyTask (resmî) | kapalı | bilinmiyor (36 KB exe, "compact toolbar") | ~10 |
| Xbox Game Bar | kapalı | bilinmiyor | bilinmiyor |
| Windows 11 Snipping Tool | kapalı | bilinmiyor | bilinmiyor |
| macOS Shift+Cmd+5 / QuickTime | kapalı | bilinmiyor; durdurma menü çubuğunda | 1 (durdur) |
| Screen Studio | kapalı | bilinmiyor | 4 + sayaç |
| CleanShot X | kapalı | bilinmiyor | 4 |

Ölçüsü bilinen 22 araçtan **kompakt kip yüksekliği 32 ile 64 arasına sıkışıyor**:
ScreenToGif 32, ShareX 42, Cap 40, Screenity 48, recordbuddy 54, Kap 64, LICEcap ~17 DLU.
Tek kaçak yukarı doğru Fluent'in 88'i — çünkü onun küçük kipi bir şerit değil, küçültülmüş
bir pencere.

**Kompakt kip taşımayan araçlar da bir bulgu.** Captura, vokoscreenNG, SimpleScreenRecorder,
Blue/Green Recorder ve OBS'in kendisi küçük kip sunmuyor. OBS'te bu bir eksiklik olarak
2023'ten beri forumda açık; resmî cevap "kendi betiğini yaz" (bkz. bölüm 6). Yani küçük kip,
alanın standardı değil; ayırt edici bir özellik.

---

## 2. Küçük Kipte Mutlaka Olan Düğmeler

Sayım tabanı: düğme listesini kaynaktan ya da resmî belgeden okuyabildiğim **18 kayıt
aracı** — ShareX, ScreenToGif, Kap, Cap, Screenity, LICEcap, Kooha, Peek, Fluent,
recordbuddy, atbswp, PyMacroRecord, TinyTask-macOS, alierenaltindag/TinyTask,
obs-menu-recorder, Screen Studio, CleanShot X, macOS Shift+Cmd+5. PowerToys VCM sayıma
girmiyor: o bir kaydedici değil, mikrofon/kamera susturucu.

**Kaydı bitiren düğme — 18/18.** İstisnasız. Adı her yerde "Durdur" değil: makro
kaydedicilerin bir bölümünde (atbswp, alierenaltindag/TinyTask, PyMacroRecord) aynı
düğme kayıt açıkken durdurma işini görüyor. Kap'ta çubuk tamamen kaybolduğunda bile
tepsi simgesine tek tıklama durdurma demek (`tray.ts` → `stopRecording()`).

**Geçen süre sayacı — çoğunluk.** ShareX (`TimerText`, `00:00:00`, Cascadia Mono 14),
Cap (250 ms'de bir tazelenen sayaç), Screenity (58 px saatli / 42 px saatsiz etiket),
Kooha (`recording_time_label`), recordbuddy, ScreenToGif (`DisplayTimer`), Blue Recorder
(`record_time_label`). Sayaç kompakt kipin ikinci direği: durdurma düğmesinin yanında
duran tek okuma.

Not: ShareX'te sayaç **kapatılabilir** (`ShowRecordingTimer`) ve kapatınca çubuk 112 px
daralıyor. Yani sayaç zorunlu değil, ama varsayılan.

**Duraklat/Sürdür — çoğunluk.** ShareX, Cap, ScreenToGif, Screenity, Kooha, recordbuddy,
Kap (Alt+tık), obs-menu-recorder. Tek düğme, duruma göre simge değiştiriyor.

**Kaydediyor göstergesi (kırmızı nokta / canlı simge)** — çoğunluk. ShareX'te 8×8 px
`StatusIndicator`: hazırken Goldenrod, kayıtta Red. Fluent'te bir `Ellipse` + "Recording"
yazısı. Kap'ta menü çubuğu simgesi 20 ms aralıklı kare dizisiyle canlandırılıyor.
macOS Sonoma'da gösterge işletim sisteminin kendisinde, mor nokta olarak.

**Sürükleme tutamağı — çoğunluk.** Ayrı bir düğme değil ama yer kaplıyor, o yüzden
sayıyorum. ShareX'te sayacın kendisi tutamak (`TimerDragHandle`, `Cursor="SizeAll"`,
112 px). Cap'te şeridin sağ ucunda 24 px'lik ayrı tutamak. LICEcap'te pencerenin her yeri.
recordbuddy'de `isMovableByWindowBackground`.

**İptal/At — yarı yarıya.** ShareX (onay diyaloguyla), ScreenToGif (`DiscardCommand`,
varsayılan F9), Screenity, CleanShot X, Screen Studio. Yarısında yok — ve olduğu yerde
neredeyse hep onay isteniyor: ShareX çubuğu iptal düğmesine basınca 461 px'e geri açılıp
"Bu kayıt iptal edilsin mi?" satırını gösteriyor, CleanShot X onay diyaloğu açıyor.

---

## 3. Küçük Kipte Asla Olmayan — Büyük Pencereye Bırakılanlar

Tarama boyunca hiçbir kompakt yüzeyde görmediklerim:

**Kodek, bit hızı, kalite, ön ayar.** Hiçbirinde yok. ScreenToGif'te bunlar
`Options` diyaloğunda; kompakt şeritte yalnız o diyaloğu açan 26 px'lik dişli var.
LICEcap'te aynı: ayarlar `IDD_OPTIONS` diyaloğunda, şeritte yalnız FPS ve boyut.

**Ses aygıtı seçimi.** Mikrofon **açık/kapalı anahtarı** kompakt kipte olabiliyor
(Cap "mic indicator", Screenity mic toggle, PowerToys VCM'in iki düğmesinden biri,
Camtasia), ama **hangi mikrofon** sorusu her zaman büyük pencerede. Aygıt listesi bir
açılır kutu ister, açılır kutu 40 px yükseklik ister.

**Çıktı klasörü ve dosya adı.** Hiçbir kompakt yüzeyde yok. SimpleScreenRecorder dosya
adını gösteren bir etiket taşıyor ama o büyük penceresinde, sekiz okumadan biri olarak
(`Total time`, `Frame rate in/out`, `Size in/out`, `File name`, `Bit rate`).

**Sıcak tuş tanımlama.** Tuşun kendisi kompakt kipte çalışıyor, **tanımı** her zaman
ayarlar penceresinde. akon47/ScreenRecorder'da ayrı bir `ShortcutEditorWindow` var.

**Kayıt sonrası akış — önizleme, düzenleme, paylaşma.** Hiçbirinde yok. Kap kayıt
bitince ayrı bir düzenleyici penceresi açıyor; Fluent `PlayerPage`'e geçiyor; ShareX
sonucu iş akışına devrediyor.

**Yarı yarıya olanlar — sınırdakiler.** Bölge/hedef seçimi ScreenToGif'in kompakt
şeridinde 50 px'lik bir ayrılmış düğme olarak duruyor (Alan/Pencere/Ekran), Kooha'da
kayıt sayfasına geçilince kayboluyor. FPS ScreenToGif'in kompakt şeridinde 45 px'lik
bir sayı kutusu, Fluent'in küçük kipinde bir açılır liste — ama ikisi de bunu kaydın
**öncesinde** gösteriyor.

---

## 4. Üç Yerleşimin Çizimi

Ölçüler kaynak koddaki sabitler. Çizimler ölçekli değil, sayılar gerçek.

### 4.1 ShareX — ScreenRecordWindow (Avalonia, 461×42)

`ScreenRecordWindow.axaml.cs` satır 48-56:
`ToolbarWidth=461`, `ToolbarHeight=42`, `TimerWidth=112`, `ActionButtonWidth=86`,
`CompactActionButtonWidth=40`, `ActionButtonCount=4`, `ToolbarGapPixels=3`.

```
        kayıt alanı (seçilen bölge, 1 px çerçeve)
   +--------------------------------------------------------------+
   |                                                              |
   |                                                              |
   +--------------------------------------------------------------+
                          ↕ 3 px boşluk
   +==============================================================+
   | ● 00:00:00  | ▷ Başlat | ‖ Duraklat | ↺ Yeniden | ✕ İptal   |  42
   +=============+==========+===========+===========+============+
     112 px        86 px      86 px       86 px       86 px
     (sürükleme     (8×8 nokta: bekliyor=Goldenrod, kayıtta=Red)
      tutamağı)
   |<------------------------- 461 px -------------------------->|
```

Etiketler kapatılınca (`ShowRecordingButtonLabels=false`) dört düğme 86 → 40'a iner,
çubuk 461 − 46×4 = **277 px**. Sayaç da kapatılınca (`ShowRecordingTimer=false`)
112 px daha gider:

```
   +=====================+
   | ▷ | ‖ | ↺ | ✕ |      42
   +====+===+===+===+
     40  40  40  40
   |<---- 165 px ------->|
```

Çubuk kayıt alanının **3 px altında**, alanın dışında duruyor (`ConfigureGeometry`,
`Canvas.SetTop(Toolbar, frameHeight + gap)`). `Topmost="True"`, `CanResize="False"`,
`ShowInTaskbar="False"`. İki küçültme ekseni birbirinden bağımsız: sayaç ve etiketler
ayrı ayrı kapanıyor.

### 4.2 ScreenToGif — kompakt kip (WPF, ≥220 geniş, 30+32)

`NewRecorder.xaml`: pencere `MinWidth="220"`, `Topmost="True"`, `WindowStyle="None"`,
`SizeToContent="WidthAndHeight"`. Satır 74-75: başlık satırı 30, alt satır Auto.
Satır 129: normal şerit `MinWidth="250" Height="64"`. Satır 334: kompakt şerit
`Height="32" MinWidth="100"`. İkisinin `Visibility`'si `RecorderCompactMode`
ayarına ters bağlı — biri görünürken diğeri yok.

```
  +====================================================+
  | ScreenToGif   "tıkla veya bas"   [00:00:00 / 12]  – × |  30   ← her zaman
  +====================================================+          görünür
  | ⚙  ◐ [15]fps ▾ | ⬒ Alan ▾ | ✕  ● ‖  ■              |  32   ← kompakt şerit
  +====================================================+
    26   32  45  ~40   50        26  26   26
  |<--------------- en az 220 px --------------------->|
                                                      toplam 62
```

Başlıktaki `DisplayTimer` süreyi ve çekilen kare sayısını taşıyor, **kompakt kipte de
duruyor** — çünkü başlık satırı kompakt kipin parçası değil, penceresinin sabiti.
Kompakt şeritteki 10 sütun: dişli (Options), dairesel frekans göstergesi, FPS sayı
kutusu, frekans değiştirme düğmesi, ayraç, Alan/Pencere/Ekran açılır düğmesi, At,
Anlık-çek/Kaydet/Duraklat (tek sütunu paylaşıyor, duruma göre biri görünüyor), Durdur.

Normal kipte aynı şerit 64 px, düğmeler `MinWidth="65"` ile yazılı. Kompakt kipte
yazılar gidiyor, ikonlar 20×20'ye iniyor.

```
  normal kip (RecorderCompactMode=False)
  +====================================================+
  | ScreenToGif                        [00:00:00] – ×  |  30
  +====================================================+
  | [◉ Kaydet] | ◐ 15 fps  | ⬒ 1280 × 720 | [■ Durdur] |  64
  |            | [frekans▾]| [Alan ▾]      |            |
  +====================================================+
     ≥65          ≥100         ≥110           ≥65
  |<------------- en az 250 px ----------------------->|
                                                      toplam 94
```

Yani geçiş 94 → 62 px, **%34 boy tasarrufu**. Geçiş düğmeyle değil, Ayarlar >
Kaydedici > "Compact mode." onay kutusuyla yapılıyor ve anında uygulanıyor.

### 4.3 Cap — kayıt denetim çubuğu (Rust/GPUI, panel 320×150, kart 40)

`apps/desktop-gpui/src/controls_window.rs` dosya başı yorumu ve satır 108, 359, 452:
panel 320×150, görünür kart `h(px(40.))`, `rounded(px(16.))`; düğmeler 28×32 içinde
20 px ikon; sağ uçta `w(px(24.))` sürükleme tutamağı; sayaç 14 px, 250 ms'de bir
tazeleniyor.

```
  ekranın altı, kayıt alanının dışında, odak çalmayan panel
  +--------------------------------------------------+
  |            (saydam, 320×150)                     |
  |                                                  |
  |   +==========================================+   |
  |   | ■ 00:12  |  🎤  ↺  🗑  |  ⋮⋮ |            40  |  ← görünür kart
  |   +==========================================+   |
  |      28×32     28×32 her biri   24            |
  +--------------------------------------------------+
```

Panelin 320×150 olup görünen kısmın yalnız alttaki 40 px olması bir tasarım kararı:
üstteki boşluk gölge, onay açılır kutusu (`max_h(px(56.))`) ve genişleyen durumlar
için ayrılmış. Pencere `nonactivatingPanel` ve en yüksek pencere seviyesinde —
tıklanınca kaydedilen uygulamadan odağı almıyor.

---

## 5. VidShrink İçin Öneri

### 5.1 Elimizdeki yüzey

VidShrink'in Kaydedici sekmesinde zaten bir denetim şeridi var:
`src/VidShrink.App/Recorder/RecorderView.axaml` satır 20-99, `Strip` adlı `Border`.
Ölçüleri `src/VidShrink.App/Themes/Recorder.axaml` satır 25-29'da:

| Belirteç | Değer |
|---|---|
| `RecorderStripPadding` | 16,8 |
| `RecorderStripGap` | 8 |
| `RecorderButtonSize` | 40 |
| `RecorderDotSize` | 12 |
| `RecorderReadoutMinWidth` | 136 |

Şerit bugün şunu taşıyor: canlı nokta (12), durum yazısı, üç okuma (geçen süre,
kare, düşen kare — her biri en az 136 geniş, iki satırlı etiket+değer), ve dört
düğme (Başlat / Duraklat / Sürdür / Durdur, 40 yüksek, duruma göre `IsVisible`).

Bu şeridin bugünkü yüksekliği 40 + 2×8 = **56 px**, genişliği en az
16 + 12 + 8 + durum + 3×136 + 2×24 + düğmeler + 16 ≈ **700 px**. Yükseklik saha
aralığının (32–64) içinde; **genişlik değil**. Küçültülecek eksen genişlik.

### 5.2 Küçük kipte kalması gereken düğmeler

**1. Durdur.** 22/22. Tartışmasız. Küçük kipte tek başına kalsa bile o kalır.

**2. Duraklat/Sürdür — tek düğme olarak.** Bugün `BtnPause` ve `BtnResume` ayrı iki
düğme, aynı anda yalnız biri görünüyor. Küçük kipte tek sütun, duruma göre simge
değiştiren tek düğme olmalı — ShareX (`PauseButton` metnini değiştiriyor), Cap ve
ScreenToGif (Snap/Record/Pause aynı `Grid.Column="8"`'i paylaşıyor) üçü de böyle
yapıyor. Gerekçe: iki ayrı sütun ayırmak, yarısı hep boş 40 px demek.

**3. Başlat.** Küçük kip kayıt başlamadan da açılabiliyorsa gerekli. ShareX'te
`StartButton` çubuğun kalıcı parçası. Ama Duraklat'la aynı sütunu paylaşabilir —
kayıt başladığında Başlat gider, Duraklat gelir.

**4. Geçen süre.** Çoğunluk kuralı. Ama **tek satır, tek değer** olarak: bugünkü
`PlanFactLabel` + `PlanFactValue` iki satırlı düzen 40 px'e sığmaz. ShareX'in
biçimini alalım: tek hücre, tek eşgenişlikte yazı, `00:00:00`. VidShrink'te
`MonoValue` teması zaten var (`TxtState` onu kullanıyor).

**5. Canlı nokta.** 12 px'lik `LiveDot` zaten var ve NeonEmber ile boyanıyor.
Sahada bunun karşılığı ShareX'in 8×8 `StatusIndicator`'ı. Küçük kipte kalsın —
maliyeti 12 px, kazancı "bu şey şu anda kaydediyor" bilgisinin tek bakışta okunması.

**6. Büyük kipe dön düğmesi.** Fluent'in `OverlayButton`'ı, ScreenToGif'in başlık
satırındaki küçültme düğmesi, Cap'in sürükleme tutamağı. Bizde başlık çubuğu yoksa
bu düğme şeritte olmak zorunda. 26–30 px.

**Toplam:** beş öğe — nokta 12, sayaç ~70, Başlat/Duraklat 40, Durdur 40, geri-dön 28 —
aralarında 4×8 boşluk, 2×16 kenar payı: 16+12+8+70+8+40+8+40+8+28+16 = **254 px genişlik,
56 px yükseklik**. Sayaç genişliği (~70) tek tahmini kalem; gerçek değeri `MonoValue`
temasında `00:00:00` metninin ölçülmesiyle belirlenmeli (bkz. 5.5).

Bu, ShareX'in etiketsiz-sayaçlı 277 px'iyle aynı ailede, Kooha'nın 220 genişliğinin
biraz üstünde, Kap'ın 464'ünün yarısı kadar.

Etiketleri de atıp yalnız ikon bırakırsak (ShareX'in ikinci ekseni) ~200 px'e iner.
Bunu ikinci bir adım olarak öneriyorum, ilk sürümde değil: yazılı düğme okunur,
ikon öğrenilir.

### 5.3 Küçük kipte olmaması gerekenler

**Kare ve düşen kare okumaları.** Bugün şeritte üç okuma var, her biri en az 136 px.
Sahada hiçbir kompakt yüzeyde ikiden fazla okuma görmedim; çoğunda bir tane (süre).
`TxtFrames` ve `TxtDropped` büyük kipte kalsın. Gerekçe sadece yer değil: düşen kare
sayısı kayıt sırasında **eyleme dönüşmeyen** bir sayıdır — kullanıcı onu görüp
yapabileceği tek şey kaydı iptal etmek, o da zaten ayrı bir karar.

**Hedef seçimi (`CmbTarget`, bölge kutuları), kodek, ön ayar, FPS, kalite, ses aygıtı
listeleri, çıktı klasörü, hedef boyut/süre bütçesi, `BtnAutoMeasure`.** Tamamı büyük
pencerede. Bunlar kayıt **öncesi** kararlar; kayıt başladıktan sonra hiçbiri
değişemiyor zaten. ScreenToGif bile kompakt şeridinde yalnız FPS ve hedef tipini
tutuyor, gerisini dişliye gönderiyor.

**Sonuç paneli — `ResultPanel`, `BtnReveal`, `BtnToShrink`, `BtnToPlayer`, paylaşma.**
Kayıt bitince küçük kipten çıkılıp büyük pencereye dönülmeli. Kap, Fluent ve
ShareX'in üçü de bunu yapıyor: kayıt biter, küçük yüzey kapanır, sonuç ekranı açılır.

**Ayrık durum: mikrofon aç/kapa.** Sahada kompakt kipte **kalan** tek ses denetimi bu
(Cap, Screenity, Camtasia, PowerToys VCM). VidShrink'te bugün kayıt sırasında mikrofon
susturma yok; eklenecekse yeri küçük kip, ama bu ayrı bir iş — burada yalnız yerini
işaretliyorum.

### 5.4 Davranış önerileri

**Hep üstte: evet, ama yalnız küçük kipte.** LICEcap'in yaptığı bu — kayıt başlarken
`HWND_TOPMOST`, durunca `HWND_NOTOPMOST`. Sürekli üstte duran bir pencere kayıt
yokken sinir bozucu. Avalonia'da karşılığı `Window.Topmost`.

**Sürüklenebilir: evet.** Sahada istisnasız. En ucuz yol ShareX'inki: sayacın kendisi
tutamak olsun (`Cursor="SizeAll"`, `PointerPressed` → `BeginMoveDrag`). Ayrı bir
tutamak sütunu 24 px yer alır, sayaç zaten orada.

**Kenara yapışma: gerekmiyor.** Yalnız PowerToys VCM'de (köşe seçimi) ve OBS Omni-Bar'da
(dock kenarı) gördüm, ikisi de 0 yıldızlı ya da kaldırılmış. Ana akımda yok.

**Kayıt alanının dışında durma: evet, ve bu kritik.** ShareX çubuğu bölgenin 3 px
altına koyuyor. Cap'in paneli `nonactivatingPanel` — tıklanınca kaydedilen uygulamadan
odağı çalmıyor. Bizde ffmpeg gdigrab ile ekran/bölge yakalandığında şerit kadrajın
içine düşerse kayda karışır. En az iki şey lazım: şerit varsayılan olarak seçili
bölgenin dışına konumlanmalı, ve bölge tam ekransa kullanıcıya bunun kayda gireceği
söylenmeli.

**Geçiş: şeritteki düğmeyle, ayarlardan değil.** ScreenToGif'in zayıf yanı bu —
kompakt kip Ayarlar > Kaydedici altındaki bir onay kutusu, yani kullanıcı onu
bulamıyorsa hiç kullanmıyor. Fluent'in `OverlayButton`'ı doğru model: başlıkta tek
düğme, tıkla-geç, tıkla-dön, ikon ve ipucu yön değiştiriyor.

**Sıcak tuşlar.** Sahadaki varsayılanlar: ScreenToGif F7 başlat/duraklat, F8 durdur,
F9 at. Peek Ctrl+Alt+R. Xbox Game Bar Win+Alt+R. Snipping Tool Win+Shift+R. ShareX
Shift+PrintScreen. TinyTask-macOS F6/F8. atbswp F2–F12 aralığında kullanıcı seçimi.
İki örüntü var: **tek tuş, F-sırasından** (ScreenToGif, TinyTask) ya da
**değiştirici + R** (Peek, Game Bar, Snipping Tool). VidShrink Windows'ta Game Bar'ın
Win+Alt+R'siyle çakışmamalı; F7/F8 daha güvenli. Sıcak tuş küçük kipte zaten çalışır,
tanımı büyük pencerede kalır.

### 5.5 Ölçü belirteçleri

Yeni ölçü uydurmadan, `Themes/Recorder.axaml`'daki mevcut belirteçlerin üstüne:

| Yeni belirteç | Önerilen kaynak | Gerekçe |
|---|---|---|
| `RecorderMiniHeight` | 40 + 2×8 = 56 (bugünkü şerit yüksekliği) | Zaten sahada (32–64); yeni sayı gerekmiyor |
| `RecorderMiniMinWidth` | ~254, `SpaceMd` katına yuvarlanmış | Bölüm 5.2 toplamı |
| `RecorderMiniButtonSize` | `RecorderButtonSize` (40) | Dokunma hedefi küçülmemeli |
| `RecorderMiniReadoutWidth` | `RecorderReadoutMinWidth`'ten küçük yeni bir değer | 136 iki satırlık düzen için; tek satırlık sayaç daha dar |

Son satırdaki değeri **yazmıyorum**: `MonoValue` temasının yazı ölçüsüyle `00:00:00`
metninin gerçek genişliğine bakılıp belirlenmeli, göz kararı bir sayı `teknesyum-ui`
belirteçlerinin dışına düşer.

---

## 6. Kaynaklar

**Kaynak kodundan okunan ölçüler**

- ShareX — [`ScreenRecordWindow.axaml`](https://github.com/ShareX/ShareX/blob/develop/ShareX.ScreenCaptureLib/Presentation/ScreenRecording/ScreenRecordWindow.axaml) (461×42, düğme 86×40, sayaç 112, nokta 8×8), [`ScreenRecordWindow.axaml.cs`](https://github.com/ShareX/ShareX/blob/develop/ShareX.ScreenCaptureLib/Presentation/ScreenRecording/ScreenRecordWindow.axaml.cs) satır 48-56 sabitler, 130-134 `CurrentToolbarWidth`, 603-645 `ConfigureGeometry`
- ScreenToGif — [`NewRecorder.xaml`](https://github.com/NickeManarin/ScreenToGif/blob/master/ScreenToGif/Windows/NewRecorder.xaml) satır 10 (`MinWidth=220`, `Topmost`), 74 (başlık 30), 129 (normal 250×64), 334 (kompakt 32×100), 420-441 (At/Anlık/Kaydet/Duraklat/Durdur); [`NewRecorder.xaml.cs`](https://github.com/NickeManarin/ScreenToGif/blob/master/ScreenToGif/Windows/NewRecorder.xaml.cs) satır 957-968 kompakt kipte `StopCommand`; [`Settings.xaml`](https://github.com/NickeManarin/ScreenToGif/blob/master/ScreenToGif/Resources/Settings.xaml) satır 81 (`RecorderCompactMode=False`), 166-171 (F7/F8/F9); [`RecorderSettings.xaml`](https://github.com/NickeManarin/ScreenToGif/blob/master/ScreenToGif/Views/Settings/RecorderSettings.xaml) satır 61-63 onay kutusu
- Cap — [`controls_window.rs`](https://github.com/CapSoftware/Cap/blob/main/apps/desktop-gpui/src/controls_window.rs) dosya başı yorumu (320×150, 40 px kart), satır 108 (28×32), 359 (`h(px(40.))`), 452 (tutamak 24), 531 (`max_h(px(56.))`)
- Kap — [`renderer/containers/action-bar.js`](https://github.com/wulkano/Kap/blob/main/renderer/containers/action-bar.js) satır 4-5 (`barWidth=464`, `barHeight=64`), 47-49 konum (yatay orta, ekranın %80'i), 114-140 sürükleme; [`renderer/components/action-bar/index.js`](https://github.com/wulkano/Kap/blob/main/renderer/components/action-bar/index.js) satır 84-120 (`.actions` 200×64, basit/gelişmiş çapraz geçiş — çubuk küçülmüyor, içerik değişiyor); [`record-button.js`](https://github.com/wulkano/Kap/blob/main/renderer/components/action-bar/record-button.js) satır 136-170 (64/48/24); [`main/tray.ts`](https://github.com/wulkano/Kap/blob/main/main/tray.ts) satır 60-88 (kayıtta tepsi: tık=durdur, Alt+tık=duraklat, 20 ms canlandırma)
- Peek — [`ui/application-window.ui`](https://github.com/phw/peek/blob/main/ui/application-window.ui) satır 120-121 (min 60×60), 148-230 (Stop/Record/format/menü), 246-300 (kayıt alanı min 20×40, `size_indicator` "0 x 0", `delay_indicator`, `shortcut_label`); [`gschema.xml`](https://github.com/phw/peek/blob/main/data/com.uploadedlobster.peek.gschema.xml) satır 25-26 (Ctrl+Alt+R), 75-76 (varsayılan 500×300), 13-14 (gösterge gecikmesi 1600 ms); [`application-window.vala`](https://github.com/phw/peek/blob/main/src/ui/application-window.vala) satır 92 (`SMALL_WINDOW_SIZE=400`), 442-452 (etiket düşürme), 616-624 (`freeze_window_size`, `set_keep_above`). **Proje arşivlenmiş.**
- Fluent Screen Recorder — [`MainPage.xaml.cs`](https://github.com/MarcAnt01/Fluent-Screen-Recorder/blob/master/FluentScreenRecorder/Views/MainPage.xaml.cs) satır 79 (`SetPreferredMinSize(412, 88)`), 111-113 ve 699-703 (`CompactOverlay`, `CustomSize=412×260`), 289 (kayıtta `SetAppSize(412, 88)`), 671 (kayıt bitince kullanıcı boyutuna dönüş); [`MainPage.xaml`](https://github.com/MarcAnt01/Fluent-Screen-Recorder/blob/master/FluentScreenRecorder/Views/MainPage.xaml) satır 23-30 (başlık 32), 57-77 (`OverlayButton`, 16×16 ikonlar), 95-144 (Record 40, `RecordingMiniOptions`: çözünürlük + kare hızı), 159-175 (`Ellipse` + "Recording")
- Kooha — [`data/resources/ui/window.ui`](https://github.com/SeaDve/Kooha/blob/main/data/resources/ui/window.ui) satır 6-9 (`default-width=220`, `default-height=230`, `resizable=False`)
- LICEcap — [`licecap/licecap.rc`](https://github.com/justinfrankel/licecap/blob/main/licecap/licecap.rc) satır 29 (`IDD_DIALOG1 DIALOG 0,0,551,335`, `IDC_VIEWRECT` 4,4→543×314, Insert/Record/Stop); [`licecap_ui.cpp`](https://github.com/justinfrankel/licecap/blob/main/licecap/licecap_ui.cpp) satır 666 / 1680 (`HWND_NOTOPMOST` ↔ `HWND_TOPMOST`), 768-810 (`IDD_OPTIONS`), 1775-1811 (sürükleme). **Depoda LICENSE dosyası yok.**
- Screenity — [`_Toolbar.scss`](https://github.com/alyssaxuu/screenity/blob/master/src/pages/Content/toolbar/styles/layout/_Toolbar.scss) satır 194-211 (yükseklik 48, `bottom:20px; left:20px`, genişlik içerik kadar, z-index 99999999999999); [`ToolbarWrap.jsx`](https://github.com/alyssaxuu/screenity/blob/master/src/pages/Content/toolbar/layout/ToolbarWrap.jsx) (`react-rnd` ile sürüklenebilir, sayaç 58/42 px)
- recordbuddy — [`RecordingControlBarController.swift`](https://github.com/edemekong/recordbuddy/blob/master/App/RecordingControlBarController.swift) satır 29-60 (kurulum 860×62 → 620×62), 103-104 ve 143 (aktif kayıt **190×54**), 317 (kart 852×54); [issue #102](https://github.com/edemekong/recordbuddy/issues/102)
- akon47/ScreenRecorder — [`MainWindow.xaml`](https://github.com/akon47/ScreenRecorder/blob/master/ScreenRecorder/MainWindow.xaml) satır 17-25 (`Height=72`, `MinWidth=240`, `Topmost=True`, `WindowStyle=None`, `ResizeMode=NoResize`)
- Captura — [`MainWindow.xaml`](https://github.com/MathewSachin/Captura/blob/master/src/Captura/Windows/MainWindow.xaml) satır 1-20 (`MaxWidth=440`, `SizeToContent=Height`, `CaptionHeight=0`). Kompakt kip yok.
- SimpleScreenRecorder — [`PageRecord.cpp`](https://github.com/MaartenBaert/ssr/blob/master/src/GUI/PageRecord.cpp) satır 188-219 (sıcak tuş kullanıcı tanımlı), 257-295 (sekiz okuma), 458 ("Hide window" → tepsi)
- vokoscreenNG — [`formMainWindow.ui`](https://github.com/vkohaupt/vokoscreenNG/blob/master/src/formMainWindow.ui) satır 9-10 (845×580), sekme düğmeleri 110×56
- open-recorder — [`HUDWindowMetricsTests.swift`](https://github.com/imbhargav5/open-recorder/blob/main/apps/macos/Tests/OpenRecorderMacTests/HUDWindowMetricsTests.swift) satır 212-213 (genişlik 760, testle pimli)
- TinyTask-macOS — `RecordingHUD.swift` satır 54-58 ve 214 (genişlik 320, `panel.level = .floating`), `MainWindowController.swift` satır 30-32 (720×460), `HotkeyManager.swift` satır 85-87 (F6/F8) — [depo](https://github.com/Aaru1801/TinyTask-macOS)
- atbswp — `atbswp/gui.py` satır 140-180 (7 düğme), 235-258 (`main_sizer.Fit`), `settings.py` satır 58 (`"Always On Top": True`), `control.py` satır 148-159 (F1-F12) — [depo](https://github.com/RMPR/atbswp)
- PyMacroRecord — `src/windows/window.py` satır 8-13 ve `src/windows/main/main_app.py` satır 47 (350×200, `resizable(False, False)`), satır 51/116 (topmost yalnız açılışta) — [depo](https://github.com/LOUDO56/pymacrorecord)
- alierenaltindag/TinyTask — `src/ui/main_window.py` satır 24 (`setFixedSize(350, 650)`) — [depo](https://github.com/alierenaltindag/TinyTask)
- PowerToys Video Conference Mute — [silinme commit'i `12bb5c2`](https://github.com/microsoft/PowerToys/commit/12bb5c21317474255c5399849de65637b593ba0e) (v0.88.0'da kaldırıldı), `Toolbar.cpp` (boyut sprite/DPI'dan, `BORDER_OFFSET=12`, sağ üst 40, `HWND_TOPMOST`), [issue #17457](https://github.com/microsoft/PowerToys/issues/17457) (4K'da 322×44 — kullanıcı raporu, kaynak sabiti değil)

**Belge ve ürün sayfaları**

- Xbox Game Bar — [Microsoft Support](https://support.microsoft.com/en-us/accessibility/windows/use-a-screen-reader-to-record-your-screen-with-xbox-game-bar) (Win+Alt+R, Win+Alt+M, Win+G)
- Snipping Tool — [Microsoft Support](https://support.microsoft.com/en-us/windows/apps/use-snipping-tool-to-capture-screenshots) (Win+Shift+R)
- macOS ekran kaydı — [Apple Support 102618](https://support.apple.com/en-us/102618) (Shift+Cmd+5, durdurma menü çubuğunda ya da Cmd+Ctrl+Esc); [QuickTime Player kılavuzu](https://support.apple.com/guide/quicktime-player/record-your-screen-qtp97b08e666/mac)
- macOS gizlilik göstergesi — [Apple Developer Forums 769968](https://developer.apple.com/forums/thread/769968) (Sequoia 15.1'de Control Center'a mor nokta)
- OBS'te kompakt kayıt çubuğu talebi — [OBS Forums 167316](https://obsproject.com/forum/threads/floating-control-bar-for-obs-studio-while-recording.167316/) (resmî karşılığı yok, "betik yaz" deniyor)
- OBS Omni-Bar — [depo](https://github.com/Voidscape-Development/Omni-Bar) (GPL-2.0+, dock kenara yapışıyor, "compact" stil)
- obs-menu-recorder / RecBar — [depo](https://github.com/stageerdman/obs-menu-recorder) (yalnız macOS menü çubuğu; pause/resume, stop, discard, "•••")
- Screen Studio — [kayıt sırasında denetim](https://screen.studio/guide/managing-recording-in-progress) (finish/pause/restart/delete + sayaç; sağ tıkla gizlenebilir; macOS 12.3+'ta kayda girmiyor)
- CleanShot X — [changelog](https://cleanshot.com/changelog) (Stop, Pause/Resume, Restart, Discard; Discard onaylı)
- Loom — [denetimleri gizleme](https://support.atlassian.com/loom/docs/hide-the-recording-controls/)
- Camtasia — [Recorder eğitimi](https://www.techsmith.com/learn/tutorials/camtasia/camtasia-recorder/) (ekran/kamera/mikrofon/sistem sesi anahtarları + mikrofon seviye kaydırıcısı)
- Streamlabs — [blog](https://blog.streamlabs.com/how-to-record-a-live-stream-for-free-a315ebf9645c) (sağ altta yüzen düğme, basınca sayaç)
- Zoom yüzen denetim çubuğu — [topluluk forumu](https://community.zoom.com/t5/Zoom-Meetings/Keep-floating-meeting-controls-and-video-panel-from-hiding-part/m-p/37544) (Ctrl+Alt+Shift+H gizler, Esc geri getirir) — resmî sayfa değil
- ShareX bölge yakalama — [getsharex.com/docs/region-capture](https://getsharex.com/docs/region-capture)
- TinyTask (resmî) — [thetinytask.com](https://thetinytask.com/), [kullanım](https://thetinytask.com/how-to-use-tinytask/)
- Pulover's Macro Creator — [belgeler](https://www.macrocreator.com/docs/Main.html) ("Always On Top" View menüsünde)
- Gyazo GIF — [yardım](https://help.gyazo.com/Gyazo%20Video-5de75e1e040e1d0017df4379)

**Doğrulanamayanlar**

- `LazyGreed/tinytask_clone` — depo 404 döndü, arama indeksindeki açıklama doğrulanamadı; taramadan düşürüldü.
- Xbox Game Bar, Snipping Tool, macOS çubuğu, Screen Studio, CleanShot X, Loom, Streamlabs, Camtasia, ScreenRec, Gyazo: **hiçbirinin resmî belgesi piksel ölçüsü vermiyor.** "Small toolbar/widget" ifadesiyle bırakılıyor. Bu belgede sayıya çevrilmediler.
- Jitbit Macro Recorder: yalnız yüksek DPI'da arayüzün küçük göründüğüne dair destek kaydı bulundu ([KB 15132578](https://support.jitbit.com/helpdesk/KB/View/15132578-macro-recorder-s-interface-is-too-small-to-read)); kompakt kip bilgisi yok.
- `dec05eba/gpu-screen-recorder`: GitHub'da kod deposuna erişilemedi; bulunan GUI sarmalayıcıları (`runlevel5/gpu-screen-recorder-adwaita`) tam ayarlar penceresi, kompakt şerit değil.
- Cap'in lisansı GitHub'da `NOASSERTION` görünüyor; depoda LICENSE dosyası var ama SPDX otomatik saptanamamış.
