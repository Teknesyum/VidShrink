# Video Düzenleme Çizelgesi Arayüzü — Saha Taraması

Tarih: 2026-09-15. Kapsam: VidShrink'e Lightworks/Premiere tarzı bir düzenleme sekmesi
eklenecek; bu rapor yalnız **arayüz tarafını** — zaman çizelgesi denetiminin nasıl
kurulduğunu ve .NET/Avalonia'da neyin hazır olduğunu — tarar.

Kesme motoru, ffmpeg argümanları ve proje dosyası biçimi kapsam dışı.

## Okuma Uyarısı

Her iddianın yanında kaynağı var: repo + dosya yolu ya da URL. Dosya yolunu `gh api` ile
gerçekten okuduğum yerlerde alıntı satırı verdim; okuyamadıklarımı **doğrulanmadı** diye
işaretledim. Üçüncü taraf kısayol listelerini resmi belge yerine kullandığım yerleri tabloda
ayrıca belirttim.

**Renk ve ölçü uydurulmadı.** Raporda geçen piksel sayıları başka projelerin kendi kaynak
kodundan alıntıdır, VidShrink önerisi değildir. VidShrink'e girecek her ölçü
`Themes/Theme.axaml` belirteci, her renk `Themes/Palette/<Ad>/Theme.axaml` girdisi olarak
tanımlanmalıdır. Rapor sayı önermez; gerektiği yerde "belirteç gerekir" der.

## Taranan Projeler

| # | Proje | Yığın | Çizelge kodu doğrulama düzeyi |
|---|---|---|---|
| 1 | FramePFX | Avalonia 11.2 + .NET 8 | Satır seviyesinde |
| 2 | Olive Editor | C++ / Qt | Kısmi (önbellek evet, çizim hayır) |
| 3 | Kdenlive | C++ / QML | Kısmi (snap modeli, Clip.qml, kısayollar) |
| 4 | Shotcut | C++ / QML | Satır seviyesinde |
| 5 | Pitivi | Python / GTK / Cairo | Satır seviyesinde |
| 6 | Blender VSE | C++ | Yok — PR başlığı (gövde 403) |
| 7 | Godot AnimationTrackEditor | C++ | Kısmi |
| 8 | Cap | SolidJS + Rust | PR dosya listesi + gövde |
| 9 | wavesurfer.js | TypeScript / Canvas | Kaynaktan |
| 10 | peaks.js (BBC) | JS / Konva | Kaynaktan |
| 11 | OpenCut | React | Yok — PR metni |
| 12 | Remotion Timeline | React | Yok — ücretli ayrı paket |
| 13 | etro | TypeScript | Yok — bulgu çıkmadı |
| 14 | Editly | Node CLI | Evet: çizelge arayüzü **yok** |
| 15 | react-timeline-editor | React | Kısmi — README |
| 16 | Vidstack / Media Chrome / Video.js | Web | VTT storyboard deseni |
| 17 | AvaloniaEdit | Avalonia | Satır seviyesinde |
| 18 | ScottPlot 5 | .NET / SkiaSharp | Satır seviyesinde |
| 19 | OxyPlot Avalonia | .NET | Sürüm/bakım durumu |
| 20 | PanAndZoom / NodeEditor / Nodify | Avalonia, WPF | Kısmi — dosya doğrulanmadı |

## 1. Çizim Başarımı

### Asıl Sorun Ölçek Değil, Görünürlük

Cap'in `Optimize editor performance for large videos` PR'ı tanıyı açıkça yazıyor:
düzenleyici 10 dakikadan uzun videolarda "görünürlükten bağımsız olarak tüm çizelge
öğelerini ve kareleri çizdiği için" takılıyordu. Çözüm dört parça: klip ve zoom şeritlerini
sanallaştırıp yalnız görüntü alanındakini çizmek, kare çiziminde throttle'ı düşürmek,
`requestAnimationFrame` ile toplu işlemek ve kod çözücü önbelleğini büyütmek
([CapSoftware/Cap PR #1417](https://github.com/CapSoftware/Cap/pull/1417); değişen dosyalar
`apps/desktop/src/routes/editor/Timeline/ClipTrack.tsx`, `ZoomTrack.tsx`,
`crates/rendering/src/decoder/mod.rs`).

PR'ın kendi cümlesi hedefi de veriyor: "hesaplama maliyeti sabit kalır, video uzunluğundan
bağımsız olarak akıcı bir deneyim sağlanır." Bu, çizelge denetiminin tek tasarım kuralı
olmalı — **maliyet görünür alanla orantılı, medya uzunluğuyla değil.**

### Cetvel: 1-2-5-10 Ondalık Kademesi

FramePFX'in cetveli, bir çizelgede akla gelen en ucuz çözünürlük kademesini kullanıyor:
ölçek ondalık kademelere yuvarlanıyor ve yalnız kaydırma penceresine düşen aralık çiziliyor
([FramePFX.Avalonia/Editing/Timelines/TimelineRuler.cs](https://github.com/AngryCarrot789/FramePFX/blob/master/FramePFX.Avalonia/Editing/Timelines/TimelineRuler.cs),
`Render` gövdesi):

```csharp
int[] Steps = [1, 2, 5, 10];
...
double minPixel = MinPixelSize * SubStepNumber / timelineWidth;
double minStep = minPixel * this.targetTimelineModel.MaxDuration;
double minStepMagPow = Math.Pow(10, Math.Floor(Math.Log10(minStep)));
double normMinStep = minStep / minStepMagPow;
int finalStep = Steps.FirstOrDefault(step => step > normMinStep);
...
int i = (int) Math.Floor(scrollH / pixelSize);
int j = (int) Math.Ceiling((scrollH + rulerWidth + pixelSize) / pixelSize);
using (dc.PushRenderOptions(new RenderOptions() { EdgeMode = EdgeMode.Aliased }))
```

Üç ders var. Bir: adım `MinPixelSize` (kodda `5`) altına düşmüyor, etiketler asla üst üste
binmiyor. İki: döngü `scrollH`'den başlayıp `scrollH + rulerWidth`'de bitiyor — cetvel
sanallaştırılmış. Üç: `EdgeMode.Aliased` ile kılcal çizgiler bulanmıyor; bir cetvelde kenar
yumuşatma hem yavaş hem çirkin.

Kodda duran `// TODO: optimise smaller/minor lines, maybe using skia?` yorumu da bir uyarı:
`DrawingContext.DrawLine` ile alt bölme çizgilerini tek tek çizmek yoğunlaştıkça maliyetli
oluyor. Bölüm 5'teki Skia yolu tam bu yüzden var.

### Klip Arama: Kova (Chunk) Dizini

FramePFX klipleri 128 karelik kovalara bölüyor
([FramePFX/Editing/Timelines/Tracks/ClipRangeCache.cs](https://github.com/AngryCarrot789/FramePFX/blob/master/FramePFX/Editing/Timelines/Tracks/ClipRangeCache.cs)):

> "A class that stores clips in chunks of 128 frames (0-127, 128-255, 256-383, etc.) to
> efficiently locate clips at a particular frame, rather than having to scan the entire
> track's clip list"

`SortedList<long, ClipList>` üstünde tutuluyor; `SmallestActiveFrame` / `LargestActiveFrame`
ile şeridin gerçek sınırı biliniyor, `FrameDataChanged` olayı klip eklenince/silinince/
konumu değişince tetikleniyor. "Bu aralıkta hangi klipler var" sorusunun sabit zamana yakın
cevaplanması gerekir; doğrusal tarama uzun projede çöker.

### Küçük Resim ve Dalga Formu: Disk Önbelleği + Arka Plan Üretimi

Pitivi bu işi en açık kuran proje
([pitivi/timeline/previewers.py](https://github.com/GNOME/pitivi/blob/master/pitivi/timeline/previewers.py)):

- Küçük resimler her medya için ayrı bir **SQLite** veritabanına yazılıyor
  (`Thumbs(Time INTEGER PRIMARY KEY, Jpeg BLOB NOT NULL)`), `xdg_cache_home("thumbs/v1")`
  altında hash'li dosya adlarıyla. Yazmalar `_schedule_commit()` ile geciktirilip
  toplanıyor, disk I/O kısılıyor.
- Dalga formu GStreamer `level` elemanından RMS tepe değerleri olarak toplanıp `.npy`
  dosyalarına (`waves/v2`) yazılıyor. Yani **ses bir kez taranıyor, bir daha taranmıyor.**
- `PreviewGeneratorManager` LIFO bekleme kuyruğu tutuyor; üretim `GLib.idle_add()` ile düşük
  öncelikte başlıyor, `GLib.timeout_add()` aralığı CPU yüküne göre uyarlanıyor.
- Sanallaştırma: `VideoPreviewer` yalnız görünür aralık için üretim istiyor, aralık
  `thumb_interval(thumb_width)` ile zoom'a göre değişiyor — **çözünürlük kademesi zoom'dan
  türüyor.** `AudioPreviewer` görünür alana `WAVEFORM_SURFACE_EXTRA_PX = 500` kadar tampon
  ekleyerek kaydırmada sürekli yeniden çizmekten kaçınıyor.

(Dosya ve sınıf adları doğrulandı; bu sabitlerin satır numaraları doğrulanmadı.)

Olive aynı deseni playhead merkezli bir pencereyle kuruyor: `PreviewAutoCacher::SetPlayhead()`
`DiskCacheBehind` / `DiskCacheAhead` parametreleriyle önbellek penceresi hesaplıyor,
`TryRender()` en fazla dört eşzamanlı görev çalıştırıyor, `pause_renders_` /
`pause_thumbnails_` ile üretim duraklatılabiliyor
([app/render/previewautocacher.cpp](https://github.com/olive-editor/olive/blob/master/app/render/previewautocacher.cpp)).

Blender VSE aynı sorunu 2024-25'te iki ayrı PR'la yeniden çözmüş: "VSE: Faster and more
consistent thumbnail cache" ([#126405](https://projects.blender.org/blender/blender/pulls/126405))
ve "VSE: Faster timeline thumbnail drawing" ([#126972](https://projects.blender.org/blender/blender/pulls/126972)).
İkincisinin yoğun çizelgede yeniden çizimi "birkaç kat" hızlandırdığı ve zoom sırasında
küçük resmin "yüzmesi" hatasını kırpma değerlerini float'a çevirerek giderdiği belirtiliyor.
**PR gövdeleri 403 nedeniyle okunamadı — doğrulanmadı, başlık ve arama özeti düzeyinde.**

Kdenlive QML tarafında asenkron yükleyici kullanıyor: `Clip.qml` içinde
`Loader { asynchronous: true }` ile `ClipThumbs` / `ClipAudioThumbs` bileşenleri yükleniyor,
`videothumbnails` / `audiothumbnails` ayarları görünürlüğü açıp kapatıyor, genişlik
`Math.round(clipDuration * timeScale)` ile zoom'dan türüyor
([src/timeline2/view/qml/Clip.qml](https://invent.kde.org/multimedia/kdenlive/-/blob/master/src/timeline2/view/qml/Clip.qml)).
Ayrı bir mipmap/LOD katmanı görülmedi — **doğrulanmadı.**

### Web Tarafı: Döşeme (Tile) Planı ve Bellek Tavanı

wavesurfer.js v7 tam olarak istenen döşeme mimarisini kuruyor
([src/renderer.ts](https://raw.githubusercontent.com/katspaugh/wavesurfer.js/main/src/renderer.ts)):
`computeCanvasPlan()` genişliği sabit boy dilimlere bölüyor, `drawnIndexes` hangi dilimin
çizildiğini tutuyor, `getLazyRenderRange({scrollLeft, clientWidth, singleCanvasWidth, numCanvases})`
yalnız görünür dilimleri çiziyor. Piksel oranı `utils.getPixelRatio(window.devicePixelRatio)`
ile alınıp canvas `Math.round(width * pixelRatio)` boyutlanıyor.

En öğretici ayrıntı bellek tarafında: `shouldClearCanvases(Object.keys(drawnIndexes).length)`
eşiği aşılınca tüm döşeme kabı boşaltılıp `drawnIndexes` sıfırlanıyor. **Döşeme önbelleği
sınırsız büyümüyor; bir tavan var ve tavanda toptan atılıyor.** Uzun bir çizelgede saatlerce
kaydırınca bellek sızdırmamanın tek yolu bu.

peaks.js çözünürlük kademesini formülle veriyor
([src/waveform-zoomview.js](https://raw.githubusercontent.com/bbc/peaks.js/master/src/waveform-zoomview.js)):
`_getScale(duration) = Math.floor(duration * sample_rate / width)` — piksel başına örnek
sayısı süreden, örnekleme oranından ve görünüm genişliğinden hesaplanıyor. Tepe değerleri
`audiowaveform` aracıyla önceden üretilip ikili `.dat` dosyası olarak okunuyor (JSON'a göre
sıkıştırmada belirgin küçük), `waveform-data.js` çözüyor
([bbc/peaks.js README](https://github.com/bbc/peaks.js/blob/master/README.md)). Konva
katmanları ayrık: `_playheadLayer`, `_segmentsLayer`, `_axisLayer`, `_pointsLayer` — oynatma
kafası kıpırdadığında dalga formu katmanı yeniden çizilmiyor. **Katman ayrımı, scrub
akıcılığının yarısı.**

Ayrıca peaks.js iki görünüm tutuyor: sabit genişlikli, tüm dosyayı gösteren `overview` ve
kaydırılıp yakınlaştırılan `zoomview`. Bu ikili, uzun bir videoda "neredeyim" sorusunu
çizelgeyi yormadan çözer.

Web oynatıcılarında scrub önizlemesi için ortak bir standart var: `kind="metadata"` bir
WebVTT parçası, her cue bir zaman aralığını bir görsele eşliyor; görsel bir sprite sheet ise
`#xywh=x,y,w,h` fragment'iyle kırpılıyor (`storyboard.jpg#xywh=0,0,256,160`). Vidstack'te
`SeekSlider`'a `thumbnails` prop'u geçiliyor
([Vidstack Thumbnail](https://vidstack.io/docs/player/components/display/thumbnail/),
[Video.js thumbnail previews](https://videojs.org/docs/framework/react/how-to/show-timeline-thumbnail-previews)).
VidShrink masaüstü olduğu için VTT'ye gerek yok, ama **tek büyük atlas dosyası + kırpma
dikdörtgeni** fikri doğrudan geçerli: binlerce ayrı küçük bitmap yerine birkaç atlas,
GPU'da çok daha ucuz.

### VidShrink'te Zaten Ne Var

Motorun bir bölümü hazır:

- `src/VidShrink.Ffmpeg/FrameGrabber.cs` — `FramePairRequest` ile verilen saniyeden BGRA
  kare çıkaran, `DefaultCacheByteCeiling = 128L * 1024 * 1024` tavanlı bellek önbelleği olan
  çıkarıcı. `AlignToKeyframe` bayrağı ve `SourceKeyframes` alanı var; `ProcessesStarted`,
  `CacheBytes`, `CacheCount` sayaçları ölçüme açık.
- `src/VidShrink.Ffmpeg/KeyframeIndex.cs` — `ffprobe -show_entries packet=pts_time,flags`
  ile anahtar kare damgalarını çıkarıp `Floor(atSeconds)` ve `Nearest(atSeconds)` veriyor,
  `AverageGapSeconds` ile de anahtar kare sıklığını bildiriyor.

"Hangi kareyi ucuza çekebilirim" sorusunun cevabı motorun elinde ve bellek tavanı da zaten
wavesurfer'ın uyguladığı ilkeyle aynı. **Eksik olan iki şey:** kalıcı disk önbelleği
(Pitivi'nin SQLite'ı ya da klasör içi JPEG/atlas deposu) ve ses tepe değeri üretimi.
`AGENTS.md`'nin geçici dosya kuralı gereği bunlar `.calisma/` değil, kullanıcı önbellek
klasörü işidir.

## 2. Etkileşim

### Kenar Tutamağı ve Sürükleme Durumu

FramePFX'in klip denetimi, bir NLE klibinin ihtiyacı olan minimum durum makinesini veriyor
([FramePFX.Avalonia/Editing/Timelines/TimelineClipControl.cs](https://github.com/AngryCarrot789/FramePFX/blob/master/FramePFX.Avalonia/Editing/Timelines/TimelineClipControl.cs)):

```csharp
private const double EdgeGripSize = 8d;
...
if (mPos.X <= EdgeGripSize) { /* LeftGrip */ }
else if (mPos.X >= (this.Bounds.Width - EdgeGripSize)) { /* RightGrip */ }
```

`ClipPart` üçe ayrılıyor (`Header`, `LeftGrip`, `RightGrip`), `DragState` ise
`None → Initiated → DragHeader | DragLeftGrip | DragRightGrip` diye ilerliyor. İmleç duruma
göre `StandardCursorType.SizeAll` ya da `SizeWestEast` oluyor. Kritik ayrıntı: `Initiated`
ara durumu var — tıklama hemen sürükleme sayılmıyor, `hasMovedX` olunca gerçek sürüklemeye
geçiliyor. Tıklayıp seçmekle sürüklemek ayrılmazsa her seçim klibi oynatır.

`EdgeGripSize = 8d` FramePFX'in sayısıdır. **VidShrink'te bu bir tema belirteci olmalı**
(`Theme.axaml` içinde, mevcut `IconSizeSm` / `BorderThin` gibi belirteçlerin yanında);
rapor sayı önermez, belirteç gerekir.

### Yapışma (Snap): Eşik Pikselde, Saklanan Zamanda

Pitivi'de eşik kullanıcı ayarı ve varsayılanı kaynaktan doğrulandı
([pitivi/timeline/timeline.py](https://github.com/GNOME/pitivi/blob/master/pitivi/timeline/timeline.py)):

```python
GlobalSettings.add_config_option('edgeSnapDeadband',
                                 section="user-interface",
                                 key="edge-snap-deadband",
                                 default=5,
                                 notify=True)
```

ve eşik her kullanımda piksel → nanosaniye çevriliyor:

```python
def update_snapping_distance(self):
    self.ges_timeline.set_snapping_distance(
        Zoomable.pixel_to_ns(self.app.settings.edgeSnapDeadband))
```

Buradaki karar raporun en taşınabilir parçası: **yapışma eşiği ekran pikselinde tanımlanır,
zamanda değil.** Zoom değişince eşik zamansal olarak büyür/küçülür ama kullanıcının
parmağına göre hep aynı hisseder. Eşiği kare ya da saniye cinsinden sabitlerseniz
yakınlaştırınca yapışma yapışkanlaşır, uzaklaştırınca hiç tutmaz. Ayarın etiketi de
kullanıcıya açık: "Threshold (in pixels) at which two clips will snap together when dragging
or trimming."

Kdenlive aynı modeli ayrı bir sınıfa ayırmış: `SnapModel` yapışma noktalarını `m_snaps`
haritasında (aynı konumdaki noktalar için sayaçlı) tutuyor; `addPoint()`, `removePoint()`,
`getClosestPoint()`, `proposeSize()` ve — önemli — `ignore()` / `unIgnore()` sunuyor,
sürüklenen klibin kendi kenarları yapışma hedefi olmaktan çıkarılıyor
([src/timeline2/model/snapmodel.cpp](https://invent.kde.org/multimedia/kdenlive/-/blob/master/src/timeline2/model/snapmodel.cpp),
eşik `maxSnapDist` parametresi; **sayısal varsayılanı doğrulanmadı**).

Pitivi yapışma olduğunda görsel geri bildirim de veriyor: `__snapping_started_cb()` /
`__snapping_ended_cb()` ve `_draw_snap_indicator()` dikey bir çubuk çiziyor (`SNAPBAR_COLOR`,
`SNAPBAR_WIDTH`, `pitivi/utils/ui.py`). VidShrink'te bu çubuğun rengi palet dosyasından,
kalınlığı tema belirtecinden gelmelidir.

### Yapışmanın Tuzağı: Tıklarken Kayan Klip

Shotcut'ın kaynağında bu tuzak yorumuyla birlikte duruyor
([src/qml/views/timeline/Clip.qml](https://github.com/mltframework/shotcut/blob/master/src/qml/views/timeline/Clip.qml)):

```qml
// Ignore horizontal movement below the drag threshold. Otherwise magnet
// snap can nudge clip.x during a click-to-select, and at default zoom a
// sub-pixel nudge is already 1–2 frames — committing that creates a tiny
// transition.
if (!drag.active)
    return;
```

ve bırakma anında:

```qml
// At low zoom, magnet snap may move by <1px while changing the
// frame. Commit when the frame changes, not only when abs(delta)>=1.
var startFrame = Math.round(startX / multitrack.scaleFactor);
var frame = Math.round(parent.x / multitrack.scaleFactor);
if (trackIndex !== originalTrackIndex || (dragActivated && (Math.abs(delta) >= 1 || frame !== startFrame)))
```

İki yönlü bir ders. Sürükleme eşiğinin altındaki hareket **yok sayılmalı**, yoksa seçmek
klibi oynatır. Ama işlemi tamamlarken ölçü piksel değil **kare** olmalı, yoksa düşük zoom'da
bir karelik kayma sessizce yutulur. İkisi birbirinin zıddı görünür ve yalnız gerçek
kullanımda ortaya çıkar — bu yüzden kaynakta yorumla korunmuş.

### Kırpma Kipleri: Trim, Roll, Ripple

Pitivi kip seçimini değiştiricilere bağlıyor: düz sürükleme `GES.EditMode.EDIT_TRIM`,
tutamak + Ctrl `EDIT_ROLL`, Shift `EDIT_RIPPLE`, gövde `EDIT_NORMAL`
(`__get_editing_mode()`, `pitivi/timeline/timeline.py`; **satır numarası doğrulanmadı**).

Olive daha keskin bir hüküm veriyor: `SetTrimIsARollEdit()` ile "pointer trim aslında bitişik
boşlukla yapılan bir roll edit'tir" diyor
([app/widget/timelinewidget/tool/pointer.cpp](https://github.com/olive-editor/olive/blob/master/app/widget/timelinewidget/tool/pointer.cpp);
akış `InitiateDrag()` → `ProcessDrag()` → `FinishDrag()`). Yani trim ve roll iki ayrı kod
yolu değil, aynı yolun iki kipi.

Shotcut ripple'ı model seviyesinde çözüyor: `trimClipIn()` / `trimClipOut()` ripple kipinde
aşağı akıştaki klipleri boşluğu kapatmak için kaydırıyor, `consolidateBlanks()` ardışık
boşlukları birleştiriyor, `splitClip()` ve `removeRegion()` de aynı `MultitrackModel`
içinde ([src/models/multitrackmodel.cpp](https://github.com/mltframework/shotcut/blob/master/src/models/multitrackmodel.cpp)).
`MultitrackModel` bir `QAbstractItemModel` — üst seviye şeritler, alt seviye klipler.
**Kural: ripple mantığı görünümde değil modelde yaşamalı**, yoksa geri alma tutarsızlaşır.

### Çoklu Seçim

FramePFX seçimi ayrı bir katmana almış:
`FramePFX.Avalonia/Editing/Timelines/Selection/` altında `ClipSelectionManager`,
`TimelineClipSelectionManager`, `TrackSelectionManager` var. Klip denetimi aralık seçimi için
çapa tutuyor — `timeline.RangedSelectionAnchor = new TrackPoint(this.ClipModel, GetCursorFrame(this, e))`
(`TimelineClipControl.cs`). Shift ile aralık, Ctrl ile tek tek ekleme bu çapadan türer.

Nodify (WPF) üç kipli seçim modelini net kurmuş: Shift = ekle (Append), Ctrl = tersine çevir
(Invert, `IsSelected` toggle), değiştirici yok = değiştir (Replace, öncekini temizle); seçim
dikdörtgeni `ItemContainer`'ın `DesiredSizeForSelection` ya da `RenderSize` sınırıyla
kesişime bakıyor ([miroiu/nodify](https://github.com/miroiu/nodify) —
**dosya yolu doğrulanmadı, belge/arama düzeyinde**). Bu üçlü doğrudan klip seçimine taşınır.

OpenCut tarafında Shift ile yapışmayı geçici kapatma, Shift/Ctrl ile seçime ekleme ve
`TimelineZoomControl` bileşeni PR başlıklarında geçiyor
([PR #388](https://github.com/OpenCut-app/OpenCut/pull/388),
[PR #409](https://github.com/OpenCut-app/OpenCut/pull/409)) ama **kod satırı görülmedi —
doğrulanmadı.** "Shift basılıyken yapışma kapanır" kalıbı yine de Pitivi ve Kdenlive'de de
var, NLE'lerin ortak beklentisi sayılabilir.

### Kaydırma ve Yakınlaştırma Jestleri

Pitivi `do_scroll_event()` içinde üçe ayırıyor: Ctrl+tekerlek yakınlaştırma, Shift+tekerlek
yatay kaydırma, düz tekerlek dikey. Adım sabit değil, `page_size ** (2/3)` ile sayfa
boyutundan türüyor (`pitivi/timeline/timeline.py`). **Sabit adım yanlış**: uzaklaşmış bir
çizelgede sabit piksel adımı hiçbir şey yapmaz, yakınlaşmışta fırlatır.

Godot'un çizelgesi zoom'u imleç altında sabitliyor: `AnimationTimelineEdit::_zoom_changed()`
bir "zoom pivot" mantığıyla yeniden ölçekliyor, `_get_zoom_scale()` zoom'u piksel ölçeğine
çeviriyor, `_pan_callback()` / `_zoom_callback()` jestleri ayırıyor
([editor/animation_track_editor.cpp](https://github.com/godotengine/godot/blob/master/editor/animation_track_editor.cpp)).
PR #85142 "Improve usability of zooming" tam da zoom'un fare imleci konumunda yapılmasını
getirmiş ([godotengine/godot#85142](https://github.com/godotengine/godot/pull/85142)).
**İmleç altında zoom, çizelge denetiminin pazarlık edilemez davranışıdır.**

Olive'de zoom değiştiricisinin Ctrl'den Shift'e taşınması ayrı bir PR konusu olmuş
([olive#558](https://github.com/olive-editor/olive/pull/558/files)) — değiştirici seçimi bile
tartışmalı, ayarlanabilir olmalı.

Avalonia tarafında hazır bir jest paketi var: `wieslawsoltes/PanAndZoom`
(`Avalonia.Controls.PanAndZoom`, NuGet'te 11.3.0 görüldü) `ZoomBorder` denetimiyle tekerlekle
noktaya yakınlaştırma, basılı sürüklemeyle pan ve pinch sunuyor; Shift+tekerlek yatay pan.
**Dosya seviyesinde doğrulanmadı — paket/belge düzeyinde.** Bir çizelgede yatay ve dikey
eksenlerin bağımsız davranması gerektiği için (zaman ekseni zoom'lanır, şerit yükseklikleri
zoom'lanmaz) genel amaçlı bir `ZoomBorder` muhtemelen doğrudan oturmaz; jest eşlemesi için
kaynak olarak okunması daha doğru.

### Godot, Kdenlive ve Blender'da Doğrulanamayanlar

Godot'ta `snap_time()`'ın tam gövdesi `animation_track_editor.cpp` içinde bulunamadı —
**doğrulanmadı.** Trackpad ile zoom yapılamaması ayrı bir sorun olarak açık
([godot#38237](https://github.com/godotengine/godot/issues/38237)). Kdenlive'ın QML çizelge
dosyası sürümden sürüme `timeline.qml` / `Timeline.qml` diye ad değiştirdiği için tek bir
kalıcı yol verilemedi — **doğrulanmadı.** Blender'ın strip çizim yolu 403 nedeniyle hiç
okunamadı.

## 3. Oynatma Kafası ile mpv Zamanının Eşitlenmesi

### mpv'nin Sunduğu Ham Malzeme

`seek` komutunun bayrakları mpv kılavuzundan birebir
([mpv DOCS/man/input.rst](https://raw.githubusercontent.com/mpv-player/mpv/master/DOCS/man/input.rst)):

- `relative` (varsayılan): "Seek relative to current position (a negative value seeks backwards)."
- `absolute`: "Seek to a given time (a negative value starts from the end of the file)."
- `absolute-percent`, `relative-percent`.
- `keyframes`: "Always restart playback at keyframe boundaries (fast)."
- `exact`: "Always do exact/hr/precise seeks (slow)."

Varsayılan: göreli aramalarda `keyframes`, mutlak aramalarda `exact`.

`--hr-seek` seçeneğinin dört değeri ([mpv kılavuzu](https://mpv.io/manual/master/)):
`no` — "Never use precise seeks."; `absolute` — "Use precise seeks if the seek is to an
absolute position in the file, such as a chapter seek, but not for relative seeks";
`default` — "Like absolute, but enable hr-seeks in audio-only cases."; `yes` — "Use precise
seeks whenever possible."

`--hr-seek-framedrop` (varsayılan `yes`): "Allow the video decoder to drop frames during
seek, if these frames are before the seek target. If this is enabled, precise seeking can be
faster, but if you're using video filters which modify timestamps or add new frames, it can
lead to precise seeking skipping the target frame." VidShrink'in gelişmiş filtre halkaları
(`@vscolor`, `@vsmirror` vb., `MpvEngine.Advanced.cs`) düşünülürse bu uyarı somut: filtre
etkinken kare kare doğruluk isteniyorsa bu seçenek gözden geçirilmeli.

Kare adımı: `frame-step [<frames>] [<flags>]`, bayraklar `play` (varsayılan), `seek`, `mute`.
Kılavuzun uyarısı: "The default frameskip mode, play, is more accurate but can be slow
depending on how many frames you are skipping." `frame-back-step` ise "Calls frame-step with
a value of -1 and the seek flag" — yani **geri adım tanımı gereği bir aramadır ve pahalıdır.**
İkisi de sesli-yalnız oynatmada çalışmıyor.

`time-pos`, `playback-time` ve `percent-pos` özelliklerinin birebir tanımlarını kılavuzdan
çekemedim — **doğrulanmadı.** mpv'de hazır bir "ses scrub" (kaset sesi) kipi olup olmadığı
da bu turda **doğrulanmadı.**

### VidShrink Bu İşi Zaten Çözmüş

`src/VidShrink.Player/MpvEngine.cs` scrub yığılması sorununu kuşak (generation) sayacıyla
çözüyor — çizelgenin ihtiyacı olan tam mekanizma:

```csharp
var flags = precision == SeekPrecision.Exact ? "absolute+exact" : "absolute+keyframes";
var rc = CommandRc(SeekTag + (ulong)gen, "seek", target, flags);
```

`_seekGen` her aramada artıyor, eski `_seekDone` görevi düşürülüyor;
`IPlaybackEngine.cs` içindeki `SeekOutcome` zaten `Superseded` ve `Canceled` durumlarını
tanımlamış. **Kullanıcı kafayı sürüklerken her piksel için bir arama kuyruğa girmiyor; en
son istek kazanıyor.** `SeekPrecision { Exact, Keyframe }` ve
`SeekResult(SeekOutcome Outcome, double LatencyMs)` bu kararı ve ölçüsünü arayüze taşımış.

Motorun kendi `AGENTS.md`'si aramanın ne zaman bittiğini de tanımlıyor: "aramanın RESTART'ı,
komuttan sonra takası biten yeni kare **ve** render'da yarım yeni kare yok. Dönüşte
`time-pos` iner; `time-pos` özellik olayı yalnız tetik, konum olay işlenirken okunarak
yazılır" (`src/VidShrink.Player/AGENTS.md`). Kafanın konumu bu olaydan sürülmeli; ayrı bir
zamanlayıcıyla `time-pos` yoklamak kafayı titretir.

Ayrıca `TryCopyLatest(.., out frameSeconds)` her karenin kendi `time-pos` damgasını veriyor —
yani çizelgedeki kafa, ekranda gerçekten duran karenin zamanına oturtulabilir, tahmini bir
saate değil. Bu, kare kare düzenlemede doğru kareyi kesmenin ön şartı.

### Scrub İçin Önerilen Akış

Kaynaklardan çıkan ve VidShrink'in mevcut motoruna oturan desen:

1. Kullanıcı kafayı tutup sürüklerken `SeekPrecision.Keyframe` ile ara. mpv'nin kendi
   tanımıyla keyframe araması "fast"; sürüklerken doğruluk değil tepki önemli. Kuşak sayacı
   yığılmayı zaten kesiyor.
2. Kullanıcı bıraktığında tek bir `SeekPrecision.Exact` araması. Nihai kare doğru olmalı.
3. Sürükleme sırasında kafanın kendisi aramayı beklememeli — kafa ekranda hemen hareket
   etsin, görüntü arkadan yetişsin. peaks.js'in ayrı `_playheadLayer`'ı ve Pitivi'nin ayrı
   `_draw_playhead()` yolu aynı şeyi söylüyor: **kafa kendi katmanında çizilir, klip ve dalga
   formu katmanı yeniden çizilmez.**
4. Kaba önizleme için `KeyframeIndex.Nearest()` zaten elde — sürüklerken çizelgenin küçük
   resim şeridinden en yakın kareyi göstermek, motoru hiç rahatsız etmeden anlık geri bildirim
   verir. Web tarafının sprite-sheet/VTT önizlemesinin masaüstü karşılığı budur.
5. Kafa otomatik takip ederken kaydırma eşiği gerekir: peaks.js'te
   `pixelIndex >= frameOffset + width - autoScrollOffset` olunca görünüm kayıyor
   (`waveform-zoomview.js`); Pitivi'de `playhead_locked=True` iken görünüm ortalanıyor.
   Eşiksiz takip her karede kaydırma demektir.
6. Küçük resim üretimini kafaya bağlayın. Olive'in `DiskCacheBehind` / `DiskCacheAhead`
   penceresi bunun örneği: kullanıcı nereye bakıyorsa üretim oradan başlar.

## 4. Klavye Kısayolları

### Ortak Küme

Her hücrenin doğrulama durumu son sütunda. Adobe'un resmi kısayol sayfası 403, Kdenlive'ın
belge sitesi 404 verdi; **Premiere ve Resolve sütunları üçüncü taraf listelerden**, Kdenlive
sütunu ise **kaynak koddan** (`src/mainwindow.cpp`) geliyor.

| İşlev | Yaygın | Premiere | Resolve | Final Cut | Shotcut | Kdenlive | Doğrulama |
|---|---|---|---|---|---|---|---|
| Oynat / duraklat | Space | Space | Space | Space | Space | Space | FCP ve Shotcut resmi belgeden |
| Geri sar (shuttle) | **J** | J | J | J | J | doğrulanmadı | Shotcut, Resolve, FCP |
| Dur (shuttle) | **K** | K | K | K | K | doğrulanmadı | aynı |
| İleri sar (shuttle) | **L** | L | L | L | L | doğrulanmadı | aynı |
| Giriş işareti | **I** | I | I | I | I | `mark_in` (tuş doğrulanmadı) | Shotcut, Resolve, FCP |
| Çıkış işareti | **O** | O | O | O | O | `mark_out` (tuş doğrulanmadı) | aynı |
| Böl / kes | değişken | Ctrl+K | Ctrl+B | Cmd+B | **S** | **Shift+R** | Shotcut, FCP, Kdenlive kaynaktan |
| Tüm şeritlerde böl | — | doğrulanmadı | Shift+Cmd+B | **Shift+Cmd+B** | **Shift+S** | doğrulanmadı | Shotcut, FCP |
| Kare ileri / geri | Sağ / Sol | Sağ / Sol | doğrulanmadı | doğrulanmadı | **Sağ / Sol** (ya da K+L, K+J) | doğrulanmadı | Shotcut resmi belgeden |
| Sonraki / önceki kesme | Alt+Ok | doğrulanmadı | doğrulanmadı | doğrulanmadı | **Alt+Sağ / Alt+Sol** | Alt+Sağ (`monitor_seek_*`) | Shotcut ve Kdenlive kaynaktan |
| Başa / sona git | Home / End | Home / End | doğrulanmadı | doğrulanmadı | **Home / End** | **Home / End** | Shotcut ve Kdenlive kaynaktan |
| Ekle (insert / append) | — | doğrulanmadı | doğrulanmadı | **W** (insert), **E** (append) | **A** (append) | `insert_mode` (tuş doğrulanmadı) | FCP, Shotcut |
| Üzerine yaz (overwrite) | — | doğrulanmadı | doğrulanmadı | doğrulanmadı | **B** | **B** (`overwrite_to_in_point`) | Shotcut ve Kdenlive kaynaktan |
| Bağla (connect) | — | — | — | **Q** | — | — | FCP resmi belgeden |
| Sil | Del | Del | Del | Del | Del | **Del** (`delete_timeline_clip`) | Kdenlive kaynaktan |
| Ripple sil / çıkar | Shift+Del | **Shift+Del** | doğrulanmadı | **Opt+Cmd+Del** | **X** / Shift+Del / Shift+Backspace | **Shift+X** (extract) | Shotcut, FCP, Kdenlive |
| Kaldır (lift) | — | doğrulanmadı | doğrulanmadı | doğrulanmadı | doğrulanmadı | **Z** | Kdenlive kaynaktan |
| Yakınlaştır / uzaklaştır | = / − | doğrulanmadı | doğrulanmadı | doğrulanmadı | **= / −** | doğrulanmadı | Shotcut resmi belgeden |
| Sığdır (zoom fit) | — | doğrulanmadı | doğrulanmadı | doğrulanmadı | **0** | doğrulanmadı | Shotcut resmi belgeden |
| Bölgeyi seçime ayarla | — | — | — | — | doğrulanmadı | **Shift+Z** | Kdenlive kaynaktan |
| Seçim aracı | V ya da A | V | A | A | — | **S** (`select_tool`) | Kdenlive kaynaktan |
| Jilet / razor aracı | değişken | C | **B** | B | — | **X** (`razor_tool`) | Kdenlive, Resolve |
| Aralık / boşluk aracı | — | doğrulanmadı | doğrulanmadı | R | — | **M** (`spacer_tool`) | Kdenlive kaynaktan |
| Geri al | Ctrl+Z | Ctrl+Z | Ctrl+Z | Cmd+Z | **Ctrl+Z** | Ctrl+Z | Shotcut, FCP |
| Tümünü seç | Ctrl+A | Ctrl+A | Ctrl+A | Cmd+A | **Ctrl+A** | doğrulanmadı | Shotcut resmi belgeden |
| İşaretçi ekle | **M** | M | M | M | **M** (Alt+M: seçili klip çevresine) | doğrulanmadı | Shotcut resmi belgeden |
| Klip ekle (bin'e) | — | doğrulanmadı | doğrulanmadı | doğrulanmadı | doğrulanmadı | **Ctrl+I** | Kdenlive kaynaktan |

Kaynaklar: [Shotcut Keyboard Shortcuts](https://shotcut.org/howtos/keyboard-shortcuts/) (resmi);
[KDE/kdenlive `src/mainwindow.cpp`](https://github.com/KDE/kdenlive/blob/master/src/mainwindow.cpp)
(`addAction(QStringLiteral("select_tool"), m_buttonSelectTool, Qt::Key_S, …)`,
`addAction(QStringLiteral("cut_timeline_clip"), …, Qt::SHIFT | Qt::Key_R)` vb. satırları okundu);
[Final Cut Pro Keyboard Shortcuts](https://support.apple.com/guide/final-cut-pro/keyboard-shortcuts-ver90ba5929/mac) (resmi);
Resolve ve Premiere satırları üçüncü taraf listelerden
([Academy Class](https://academyclass.com/blog/davinci-resolve-keyboard-shortcuts-cheat-sheet/),
[Storyblocks](https://www.storyblocks.com/resources/blog/keyboard-shortcuts-adobe-premiere-pro)) —
**bu iki sütun ikinci eldir.**

### Okunması Gereken Sonuç

Kaynaklar üzerinde gerçekten **evrensel** olan çekirdek küçük: **J / K / L**, **I / O**,
**M**, **Space**, **Ctrl+Z**, **Ctrl+A**, **Home / End**, **Del**. Bunun ötesi projeden
projeye değişiyor — özellikle böl (`S` / `Ctrl+K` / `Ctrl+B` / `Cmd+B` / `Shift+R`) ve araç
tuşları: Kdenlive `S`'yi seçim aracına, Shotcut `S`'yi bölmeye veriyor; Kdenlive `X`'i jilet
aracına, Shotcut `X`'i ripple silmeye veriyor. **Yani "NLE standardı" diye tek bir tam küme
yok; çekirdek sekiz tuş var, gerisi seçimdir ve yeniden atanabilir olmalıdır.**

Shotcut'ın belgesi J/K/L'yi "reserved" (özelleştirilemez) diye işaretliyor, ama
`src/docks/timelinedock.cpp` içinde J/K/L **bulunamadı** — muhtemelen ana pencere olay
filtresinde işleniyor, **doğrulanmadı.**

### VidShrink'e Özgü Çakışma Uyarısı

`src/VidShrink.App/Playback/Keymap.cs` içindeki mevcut oynatıcı kısayolları NLE çekirdeğiyle
doğrudan çakışıyor. Kaynaktan okunan çakışmalar:

| Tuş | VidShrink'te bugün | NLE'de beklenen |
|---|---|---|
| `S` | Altyazı döngüsü (`SubtitleCycle`) | Böl (Shotcut) / seçim aracı (Kdenlive) |
| `X` | Yavaşlat (`Slower`) | Jilet aracı (Kdenlive) / ripple sil (Shotcut) |
| `Z` | Normal hız (`NormalSpeed`) | Kaldır / lift (Kdenlive) |
| `A` | Ses parçası döngüsü (`AudioCycle`) | Ekle (Shotcut) / seçim aracı (Resolve, FCP) |
| `B` | Sonraki yer imi (`BookmarkNext`) | Üzerine yaz (Shotcut, Kdenlive) / jilet (Resolve) |
| `M` | Sessize al (`Mute`) | İşaretçi ekle (her yerde) |
| `C` | Hızlandır (`Faster`) | Jilet (Premiere) |
| `N` | Yer imi ekle (`BookmarkAdd`) | — |
| `F` | Sonraki kare (`NextFrame`) | — |

Bu bir hata değil; VidShrink bir oynatıcı olarak tasarlandı ve `Keymap.cs` bilinçli bir
oynatıcı kümesi (MPC-HC/mpv geleneğine yakın). Ama **düzenleme sekmesi kendi kısayol
kapsamını taşımalı.** Oynatıcı kümesini düzenleme sekmesinde de geçerli sayarsanız `S` hem
altyazı değiştirir hem klip böler. `Keymap.cs`'in `PlayerInput` / `PlayerAction` satır yapısı
ikinci bir tabloya zaten uygun görünüyor; hangi kümenin hangi sekmede geçerli olacağı
kullanıcının hükmü.

## 5. Avalonia 12'de Neyin Hazır Olduğu

### Avalonia 12'nin Kendi İddiaları

Avalonia 12'nin iki teması "Performans ve Kararlılık" olarak duyurulmuş
([Avalonia 12 — Ready for What's Next](https://avaloniaui.net/blog/avalonia-12)). Çizelge
açısından üç iddia doğrudan ilgili:

- Derleyici (compositor) baştan elden geçirilmiş; 350.000 benzersiz öğeli karmaşık sahnede
  "up to a 1,867% increase in FPS" bildiriliyor. Bir çizelgede binlerce klip/küçük resim
  öğesi tam bu sınıfa girer.
- `ListBox` ve `ItemsControl` sanallaştırmasında "virtualisation edge cases" düzeltilmiş.
- Odak yönetimi elden geçmiş: odak değişimini önceden iptal edebilme ve yeni bir odak
  gezinme API'si. Klavyeyle gezilen bir çizelgede bu önemlidir.

Avalonia 12.1 ise render yığınında somut kazançlar sayıyor
([What's new in Avalonia 12.1](https://avaloniaui.net/blog/release-12-1)):
"Creating bitmaps directly from pixels is now several times faster" (#21675) — **VidShrink
için doğrudan alakalı**, çünkü hem mpv motoru hem `FrameGrabber` ham BGRA veriyor ve bir
küçük resim şeridi bu dönüşümü binlerce kez yapar. Ayrıca aşırı durumlarda "several
hundredfold" hızlanan isabet testi (#21310), derleyici serileştirmesinde daha az ayırma
(#21366), X11 ve Windows'ta yenileme hızı algılama (60 FPS tavanı kalkıyor), varsayılan açık
stencil tamponu ve ayrık GPU'larda varsayılan kapalı kirli-dikdörtgen kırpması.

Uyarı: 12.1.0'da Windows GPU'da vektör yolu kenar yumuşatmasında bir gerileme raporlanmış
([Avalonia#21760](https://github.com/AvaloniaUI/Avalonia/issues/21760)). Cetvel gibi ince
çizgi çizen bir denetimde bu görülebilir; FramePFX'in `EdgeMode.Aliased` kullanması zaten
bu sınıf sorunları atlatıyor.

### Dört Çizim Yolu ve Ne Zaman Hangisi

Avalonia'nın kendi belgesi seçenekleri ve ödünleşmelerini sayıyor
([Custom rendering | Avalonia Docs](https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering)):

| Yol | Hangi iş parçacığı | Ödünleşme (belgeden) |
|---|---|---|
| `Render(DrawingContext)` override | UI | "Keep drawing operations fast and avoid allocations" |
| `context.Custom(...)` + `ICustomDrawOperation` | Render | "bypasses Avalonia's scene graph caching. Use it only when you need SkiaSharp-level control" |
| `CompositionCustomVisualHandler` | Render | UI iş parçacığını bloklamadan kare başına geri çağrı; "cannot directly access UI-thread state" |
| `RenderTargetBitmap` | — | "requires the target control to be attached to a visible window" |
| `TopLevel.RequestAnimationFrame` | UI | Basit animasyonlar, özellik döngüleri |

SkiaSharp'a inme kalıbı sabit: `context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) as
ISkiaSharpApiLeaseFeature`, sonra `using var lease = leaseFeature?.Lease();` ve
`lease.SkCanvas`. Kiralama her render geçişinde alınır ve `using` ile bırakılmalıdır. Örnek
derlemesi: [wieslawsoltes/CustomDrawingAvaloniaExamples](https://github.com/wieslawsoltes/CustomDrawingAvaloniaExamples).

### Gerçek Bir Avalonia Çizelgesi: FramePFX Ne Yapmış

FramePFX "açık kaynak, doğrusal olmayan bir video düzenleyici, Avalonia ile C#'ta yazıldı"
(Avalonia 11.2.2 + .NET 8) ve seçtiği yol açık: **hibrit.**

Sürüklenen, seçilen, kırpılan şeyler gerçek `Control` — `TimelineClipControl`,
`TimelineTrackControl`, `TrackStoragePanel`, `ClipStoragePanel`, `TimelineScrollableContentGrid`,
`TrackSurfaces/*`. Buna karşılık yoğun ve etkileşimsiz çizim özel `Render(DrawingContext)` —
`TimelineRuler : Control` (Bölüm 1'deki `Render` gövdesi). Temalar ayrı `.axaml` dosyalarında
(`TimelineThemes.axaml`, `TimelineRulerThemes.axaml`, `TrackControlSurfaceThemes.axaml`), yani
renk/ölçü koda gömülmemiş — VidShrink'in palet kuralıyla uyumlu bir düzen.

Yani "her şeyi tuvale çiz" ya da "her şey denetim olsun" ikilisinden hiçbiri değil.
**Etkileşenler denetim, yoğunlar çizim.**

### AvaloniaEdit: Sanallaştırmanın Taşınabilir Deseni

`TextView`, `Control, ITextEditorComponent, ILogicalScrollable` uyguluyor
([src/AvaloniaEdit/Rendering/TextView.cs](https://github.com/AvaloniaUI/AvaloniaEdit/blob/master/src/AvaloniaEdit/Rendering/TextView.cs)).
`CreateAndMeasureVisualLines(Size availableSize)` görünür aralığı şöyle buluyor:
`_heightTree.GetLineByVisualPosition(_scrollOffset.Y)` ile ilk satır, `_clippedPixelsOnTop`
ile üstten kırpılan pay, sonra `while (yPos < availableSize.Height && nextLine != null)`
döngüsü. `HeightTree` (`src/AvaloniaEdit/Rendering/HeightTree.cs`) kırmızı-siyah ağaç olarak
katlanmış bölgeleri de hesaba katan bir yükseklik dizini tutuyor.

Geri dönüşüm modeli sade ve doğrudan taşınabilir: `_allVisualLines` ve `_newVisualLines` iki
liste; her satır için önce `GetVisualLine(lineNumber)` ile eskisi aranıyor, yoksa
`BuildVisualLine(...)`; döngü bitince eskide olup yenide olmayanlar `DisposeVisualLine` ile
atılıyor. Nesne havuzu değil, **"hâlâ görünürse koru, değilse at"**.

İki ayrıntı daha çizelgeye birebir uyuyor. Bir: `IBackgroundRenderer` arayüzü sadece
`KnownLayer Layer { get; }` ve `void Draw(TextView, DrawingContext)` — etkileşimsiz katmanlar
(şerit arka planı, işaretçiler, bölge gölgeleri) için hazır bir sözleşme. İki: metin glifleri
`Render` içinde değil, `TextLayer.SetVisualLines` ile ayrı `VisualLineDrawingVisual`
nesnelerinde tutuluyor ve `RenderTransform = TranslateTransform` ile kaydırılıyor — **kaydırma
yeniden çizim değil, taşıma.** Bir çizelgede yatay kaydırma tam olarak bu şekilde bedavaya
yakın hale gelir.

### ScottPlot 5 ve OxyPlot

ScottPlot'un Avalonia denetimi
(`src/ScottPlot5/ScottPlot5 Controls/ScottPlot.Avalonia/AvaPlot.cs`, yol doğrulandı) bitmap'e
çizip göstermiyor: `Render(DrawingContext)` içinde bir `CustomDrawOp : ICustomDrawOperation`
yaratıyor, o da `context.TryGetFeature<ISkiaSharpApiLeaseFeature>()` ile kiralama alıp
`Multiplot.Render(lease.SkCanvas, rect)` çağırıyor. **Yani ara bitmap kopyası yok, doğrudan
Avalonia'nın kendi SkCanvas'ına çiziyor.**

Büyük veri tarafında `src/ScottPlot5/ScottPlot5/DataSources/MinMaxCache.cs` doğrulandı:
`cachePeriod` (varsayılan 1000) aralıklarla `Parallel.For` içinde min/max önbelleği kuruyor.
`SegmentedTree.cs` ve `FastSignalSourceDouble.cs` de aynı klasörde — çok seviyeli binning
var (iç mantığı okunmadı, **doğrulanmadı**).

Değerlendirme: algoritma doğru, paket ağır. Bir ses dalga formu şeridi eksen, lejant,
etkileşim ve çoklu grafik altyapısına ihtiyaç duymaz; piksel başına min/max mantığını
kendi çiziminizde uygulamak daha hafiftir. **ScottPlot'u bağımlılık olarak değil, kaynak
olarak kullanın.**

OxyPlot Avalonia'ya gelince: resmi deponun son sürümü `2.1.0-Avalonia11` (11 Eylül 2023),
stabil `2.1.0` Aralık 2022; upstream master hâlâ Avalonia 11.0.0'da, Avalonia 12 için resmi
sürüm yok (topluluk fork'u var). **Bakım düzeyi düşük — VidShrink için önerilmez.**

### Diğer Avalonia Seçenekleri

`AvaloniaUI/Avalonia.Labs` içeriği doğrulandı: `AnimatedImage`, `CommandManager`, `Controls`,
`ExpressionBuilder`, `Gif`, `Lottie`, `Notifications`, `Panels`, `Qr`. **Pan/zoom ya da
tuval/düğüm modülü yok**; PanAndZoom, NodeEditor ve Nodify üçü de Labs dışı üçüncü taraf.
`wieslawsoltes/NodeEditor` (NuGet paketi `NodeEditorAvalonia`) tuval + düğüm + bağlayıcı +
seçim mimarisi sunuyor ama **seçim kodu dosya seviyesinde doğrulanmadı.**

### VidShrink İçin Öneri

Kanıtın işaret ettiği yol, FramePFX'in yolunun VidShrink'e uyarlanmış hali:

1. **Şerit gövdesi ve klipler: `Control` + `Render(DrawingContext)`, sanallaştırılmış.**
   Klipler tek tek `Control` olabilir (FramePFX böyle yapıyor, sürükleme/imleç/odak bedava
   gelir) ama yalnız görünür aralıktakiler yaratılmalı; AvaloniaEdit'in "koru ya da at"
   döngüsü bunun hazır reçetesi. Klip aramada FramePFX'in kova dizini.
2. **Cetvel ve küçük resim/dalga formu şeritleri: tek bir özel `Control`**, 1-2-5-10 ondalık
   kademesi, `EdgeMode.Aliased`, yalnız kaydırma penceresi. Burada binlerce ayrı denetim
   yaratmak yanlış olur.
3. **Küçük resim şeridi büyürse Skia'ya inin:** `ICustomDrawOperation` +
   `ISkiaSharpApiLeaseFeature`, atlas bitmap'lerden `DrawBitmap` ile kırpılmış dikdörtgenler.
   FramePFX'in kendi TODO'su (`maybe using skia?`) bu eşiğin gerçek olduğunu söylüyor.
   Avalonia 12.1'in "piksellerden bitmap oluşturma birkaç kat daha hızlı" kazancı da bu
   katmanda toplanır.
4. **`CompositionCustomVisualHandler`'ı yalnız oynatma kafası için düşünün.** Kafa oynatma
   sırasında kare başına hareket eden tek şey ve UI iş parçacığından bağımsız çizilebilirse
   en akıcı olan o olur. Ama belgenin uyarısı ciddi: "cannot directly access UI-thread state" —
   yani kafanın konumu mesajla gönderilmeli (`SendHandlerMessage`). Karmaşıklık maliyeti var;
   **önce basit yolu ölçün, gerekmedikçe girmeyin.**
5. **`RenderTargetBitmap`'ten uzak durun.** "requires the target control to be attached to a
   visible window" kısıtı bir çizelge için sakıncalı; önbellek üretimi zaten arka planda ve
   ffmpeg tarafında yapılmalı, Avalonia'ya değil.
6. **OxyPlot yok, ScottPlot bağımlılık olarak değil kaynak olarak.**

Ölçü ve renk: yukarıdaki hiçbir madde sayı önermiyor. Klip tutamağı genişliği, cetvel çizgi
uzunlukları, şerit yükseklikleri, yapışma eşiği ve küçük resim şeridi yüksekliği için
`Themes/Theme.axaml` içinde yeni belirteçler gerekir; yapışma çubuğu, klip gövdesi, seçim
çerçevesi ve oynatma kafası renkleri için `Themes/Palette/<Ad>/Theme.axaml` içinde yeni
girdiler gerekir. Palet 26 tema taşıyor, yani eklenen her renk 26 yerde tanımlanacak — bu
tasarım kararı verilmeden çizelge çizilmemeli.

## 6. Erişilebilirlik ve Yerelleştirme

### Avalonia'nın Otomasyon Modeli

Ekli özelliklerin tam listesi kaynaktan doğrulandı
(`src/Avalonia.Controls/Automation/AutomationProperties.cs`): `AcceleratorKey`,
`AccessibilityView`, `AccessKey`, `AutomationId`, `ControlTypeOverride`, `ClassNameOverride`,
`IsControlElementOverride`, `HelpText`, `LandmarkType`, `HeadingLevel`, `IsColumnHeader`,
`IsRequiredForForm`, `IsRowHeader`, `IsOffscreenBehavior`, `ItemStatus`, `ItemType`,
`LabeledBy`, `LiveSetting`, `Name`, `PositionInSet`, `SizeOfSet`.

`AutomationControlType` enum'unun tam listesi de doğrulandı
(`src/Avalonia.Controls/Automation/Peers/AutomationPeer.cs`): `None, Button, Calendar,
CheckBox, ComboBox, ComboBoxItem, Edit, Hyperlink, Image, ListItem, List, Menu, MenuBar,
MenuItem, ProgressBar, RadioButton, ScrollBar, Slider, Spinner, StatusBar, Tab, TabItem,
Text, ToolBar, ToolTip, Tree, TreeItem, Custom, Group, Thumb, DataGrid, DataItem, Document,
SplitButton, Window, Pane, Header, HeaderItem, Table, TitleBar, Separator, Expander,
ScrollViewer`.

Özel bir denetime rol vermek için `Control.cs` içindeki
`protected virtual AutomationPeer OnCreateAutomationPeer()` (satır 442) override edilir.

### Oynatma Kafası İçin Hazır Kalıp

Kaynakta tam oturan bir kalıp var: `RangeBaseAutomationPeer : ControlAutomationPeer,
IRangeValueProvider` — `Maximum`, `Minimum`, `Value`, `SmallChange`, `LargeChange`
özelliklerini sahibinden okuyor, `SetValue(double)` ile yazıyor, `OwnerPropertyChanged`
üzerinden `RaisePropertyChangedEvent` tetikliyor. `SliderAutomationPeer` bunu somutlaştırıp
`GetAutomationControlTypeCore() => AutomationControlType.Slider` döndürüyor.

Yani oynatma kafası için doğrudan bir `PlayheadAutomationPeer : RangeBaseAutomationPeer`
yazılabilir: `Minimum` = 0, `Maximum` = video süresi, `Value` = geçerli zaman,
`SmallChange` = bir kare, `LargeChange` = bir saniye ya da bir sayfa. Ekran okuyucu bunu
tanıdık bir kaydırıcı gibi okur.

Çizelgenin kendisi için en yakın rol `Custom`; şeritler için `Group`, klip listesi için
`List` / `ListItem` düşünülebilir. Klip çoklu seçiminde `ISelectionProvider` desteğinin
Avalonia'da olup olmadığı **doğrulanmadı** — `SelectingItemsControlAutomationPeer` üzerinden
bakılmalı.

### Web Standardı Aynı Şeyi Söylüyor

WAI-ARIA tarafında bir scrubber/timeline için önerilen rol **`slider`**; öznitelikler
`aria-valuemin`, `aria-valuemax`, `aria-valuenow` ve — sayının kendisi anlamlı olmadığı için —
`aria-valuetext` ile insan okunur karşılık ("1 dakika 23 saniye"). `aria-orientation`
varsayılanı yatay, bir çizelge kafası için doğru varsayılan. `application` rolü burada
**önerilmez**; o rol, ekran okuyucunun standart gezinme davranışını tamamen devre dışı
bırakan widget'lar içindir.

Yani Avalonia'nın `IRangeValueProvider` deseni ile ARIA'nın `slider` + `aria-valuetext`
deseni birebir örtüşüyor. **Aynı modeli iki platformda da kurabilirsiniz; `aria-valuetext`'in
karşılığı `Value` yanında sunulan biçimli zaman metnidir.**

### Platform Durumu

- **Windows:** UI Automation üzerinden, en olgun destek.
- **Linux:** Avalonia 12.0 duyurusu "the first .NET UI framework to ship a native Linux
  accessibility backend" diyor, AT-SPI2 arka ucuyla
  ([Avalonia 12 duyurusu](https://avaloniaui.net/blog/avalonia-12)); `src/Avalonia.FreeDesktop.AtSpi`
  dizini ve `Handlers/AtSpiCollectionHandler.cs` gibi dosyalar doğrulandı. Ama
  [Avalonia#14275](https://github.com/AvaloniaUI/Avalonia/issues/14275) hâlâ
  "Accessibility support for linux needs to be implemented" başlığını taşıyor — **"tamamen
  bitti" demek yanlış olur.**
- **macOS:** `src/Avalonia.Native/` altında `AvnAutomationPeer.cs` ve `avn.idl` dosyaları var
  (NSAccessibility köprüsü), ama **derinlemesine doğrulanmadı.**

### VidShrink Zaten Doğru Yerde Duruyor

Uygulamanın mevcut düzeni bu bölümün gereğini büyük ölçüde karşılıyor:

- `MainWindow.axaml` içinde `auto:AutomationProperties.Name="{loc:Text main.section.quality}"`
  gibi satırlar var — yani **otomasyon adı doğrudan yerelleştirme anahtarından besleniyor.**
  Çizelge de aynı kalıbı izlemeli; sabit İngilizce ad yazmak bu düzeni bozar.
- `src/VidShrink.App/Locales/` 42 dil klasörü taşıyor (`ar` … `zh-Hans`), sözlükler JSON,
  yükleyici `src/VidShrink.App/Localization/Strings.cs`; `AssertOnMissingKey` eksik anahtarı
  geliştirmede yakalıyor. Çizelgeye eklenecek her ad, ipucu ve kısayol açıklaması bu
  sözlüklere girmeli.

Somut olarak çizelgenin ihtiyaç duyacağı otomasyon adları: çizelge gövdesi, her şerit
(numarası ve türüyle), her klip (dosya adı ve süresiyle — `ItemType` ve `PositionInSet` /
`SizeOfSet` burada işe yarar), oynatma kafası (biçimli zaman metniyle), giriş/çıkış işaretleri
ve yapışma göstergesi. `HelpText` ile de kısayol ipucu verilebilir.

Bir uyarı: bellekteki `Başsız ölçüm İngilizce pencereyi görüyor` kaydı, pinli düzen
ölçümlerinin dil bağımlı olduğunu gösteriyor. Çizelge denetiminin ölçülerine test yazılacaksa
dilin hangi durumda olduğu testte açıkça sabitlenmeli; aksi halde metin genişliğine bağlı her
sayı dile göre kayar.

## Kaynak Listesi

Kod seviyesinde okunanlar:

- [AngryCarrot789/FramePFX](https://github.com/AngryCarrot789/FramePFX) —
  `FramePFX.Avalonia/Editing/Timelines/TimelineRuler.cs`, `TimelineClipControl.cs`,
  `TimelineControl.cs`, `Selection/*`, `TrackSurfaces/*`;
  `FramePFX/Editing/Timelines/Tracks/ClipRangeCache.cs`
- [mltframework/shotcut](https://github.com/mltframework/shotcut) —
  `src/qml/views/timeline/Clip.qml`, `src/models/multitrackmodel.cpp`, `src/docks/timelinedock.cpp`
- [GNOME/pitivi](https://github.com/GNOME/pitivi) — `pitivi/timeline/timeline.py`,
  `pitivi/timeline/previewers.py`
- [KDE/kdenlive](https://github.com/KDE/kdenlive) — `src/mainwindow.cpp`;
  [invent.kde.org](https://invent.kde.org/multimedia/kdenlive) — `src/timeline2/model/snapmodel.cpp`,
  `src/timeline2/view/qml/Clip.qml`
- [olive-editor/olive](https://github.com/olive-editor/olive) —
  `app/render/previewautocacher.cpp`, `app/widget/timelinewidget/tool/pointer.cpp`
- [godotengine/godot](https://github.com/godotengine/godot) — `editor/animation_track_editor.cpp`
- [CapSoftware/Cap PR #1417](https://github.com/CapSoftware/Cap/pull/1417)
- [katspaugh/wavesurfer.js](https://github.com/katspaugh/wavesurfer.js) — `src/renderer.ts`
- [bbc/peaks.js](https://github.com/bbc/peaks.js) — `src/waveform-zoomview.js`
- [AvaloniaUI/AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) —
  `src/AvaloniaEdit/Rendering/TextView.cs`, `HeightTree.cs`, `IBackgroundRenderer.cs`
- [AvaloniaUI/Avalonia](https://github.com/AvaloniaUI/Avalonia) —
  `src/Avalonia.Controls/Automation/AutomationProperties.cs`,
  `src/Avalonia.Controls/Automation/Peers/AutomationPeer.cs`, `RangeBaseAutomationPeer`,
  `src/Avalonia.FreeDesktop.AtSpi/`, `src/Avalonia.Native/AvnAutomationPeer.cs`
- [ScottPlot/ScottPlot](https://github.com/ScottPlot/ScottPlot) —
  `src/ScottPlot5/ScottPlot5 Controls/ScottPlot.Avalonia/AvaPlot.cs`,
  `src/ScottPlot5/ScottPlot5/DataSources/MinMaxCache.cs`
- [mpv-player/mpv](https://github.com/mpv-player/mpv) — `DOCS/man/input.rst`
- VidShrink kendi ağacı — `src/VidShrink.Player/MpvEngine.cs`, `IPlaybackEngine.cs`,
  `AGENTS.md`; `src/VidShrink.Ffmpeg/FrameGrabber.cs`, `KeyframeIndex.cs`;
  `src/VidShrink.App/Playback/Keymap.cs`; `src/VidShrink.App/Localization/Strings.cs`;
  `src/VidShrink.App/Themes/Theme.axaml`

Belge/duyuru seviyesinde:

- [Custom rendering | Avalonia Docs](https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering)
- [Avalonia 12 — Ready for What's Next](https://avaloniaui.net/blog/avalonia-12),
  [What's new in Avalonia 12.1](https://avaloniaui.net/blog/release-12-1),
  [Avalonia#21760](https://github.com/AvaloniaUI/Avalonia/issues/21760),
  [Avalonia#14275](https://github.com/AvaloniaUI/Avalonia/issues/14275)
- [mpv kılavuzu](https://mpv.io/manual/master/)
- [Shotcut Keyboard Shortcuts](https://shotcut.org/howtos/keyboard-shortcuts/),
  [Final Cut Pro Keyboard Shortcuts](https://support.apple.com/guide/final-cut-pro/keyboard-shortcuts-ver90ba5929/mac)
- [Vidstack Thumbnail](https://vidstack.io/docs/player/components/display/thumbnail/),
  [Video.js thumbnail previews](https://videojs.org/docs/framework/react/how-to/show-timeline-thumbnail-previews)
- [Blender #126405](https://projects.blender.org/blender/blender/pulls/126405),
  [#126972](https://projects.blender.org/blender/blender/pulls/126972) — **gövdeler 403**
- [mifi/editly](https://github.com/mifi/editly) — çizelge arayüzü yok,
  [Remotion Timeline](https://www.remotion.dev/docs/timeline) — ayrı ücretli paket,
  [xzdarcy/react-timeline-editor](https://github.com/xzdarcy/react-timeline-editor) — README düzeyinde
- [wieslawsoltes/CustomDrawingAvaloniaExamples](https://github.com/wieslawsoltes/CustomDrawingAvaloniaExamples),
  [wieslawsoltes/PanAndZoom](https://github.com/wieslawsoltes/PanAndZoom),
  [miroiu/nodify](https://github.com/miroiu/nodify) — **dosya yolları doğrulanmadı**

## Doğrulanmayanların Listesi

Karar almadan önce kapatılması gerekenler:

- Blender VSE'nin küçük resim önbelleği ve çizim yolu — PR gövdeleri hiç okunmadı.
- Kdenlive'ın `maxSnapDist` sayısal varsayılanı ve QML çizelge dosyasının kalıcı yolu.
- Shotcut'ın J/K/L tuşlarının kaynak koddaki işlenme yeri.
- OpenCut, Remotion Timeline, etro ve `react-timeline-editor`'ün gerçek kaynak kodu —
  hepsi README/PR özeti düzeyinde kaldı; bunlardan biri karar dayanağı olacaksa `git clone`
  ile yerelde okunmalı.
- mpv'de `time-pos` / `playback-time` / `percent-pos` birebir tanımları; mpv'de hazır ses
  scrub kipi olup olmadığı.
- Avalonia'da `ISelectionProvider` desteği; macOS erişilebilirlik köprüsünün olgunluğu.
- PanAndZoom, NodeEditor ve Nodify'ın jest ve seçim kodu — dosya seviyesinde okunmadı.
