# Video Düzenleme (NLE) Mimarisi Taraması — 2026-09-15

Amaç: VidShrink'e Lightworks/Premiere tarzı bir **video düzenleme sekmesi** eklenip eklenemeyeceğine,
eklenecekse hangi desenle eklenmesi gerektiğine karar verdirmek.

Kapsam: 30 açık kaynak proje. Her satırın kaynağı yanında; kaynağı bulunamayan yere **doğrulanmadı**
yazıldı. Bu raporda **hiçbir ölçüm yapılmadı** — hız, bellek, gecikme sayısı yok. Burada yazan her şey
kaynak kodu ve resmi belge okumasıdır.

VidShrink'in kendi lisansı **AGPL-3.0** (`LICENSE`, repo kökü). Lisans bölümü buna göre yazıldı.

---

## Karşılaştırma Tablosu

| Proje | Dil / Yığın | Çizelge Modeli | Önizleme Yolu | Negatif Hız | Kayıpsız Kesme | Lisans |
|---|---|---|---|---|---|---|
| MLT Framework | C, çerçeve | producer → cut → playlist → multitrack → tractor | pull-tabanlı consumer (sdl2) | **Var** (`timewarp:` producer) | Yok | LGPL-2.1 (çekirdek), modüller GPL |
| Shotcut | C++/Qt/QML + MLT | MLT XML (tractor/playlist) | MLT consumer | Var (MLT'den) | Yok | GPL-3.0-or-later |
| Kdenlive | C++/Qt/QML + MLT | `Mlt::Tractor` + ID-hash C++ model | MLT consumer | Time Remap ile var | Yok (yalnız kayıpsız *codec*) | GPL-3.0-or-later |
| Flowblade | Python/GTK + MLT | Python liste **+** MLT playlist (çift defter) | MLT consumer | Var (MLT'den) | Yok | GPL-3 |
| Pitivi | Python/GTK + GES | `GES.Timeline` sarmalayıcı | GES playsink + **proxy** | Doğrulanmadı | GES smart render'a devir | LGPL-2.1+ |
| GES | C/GObject/GStreamer | Timeline → Layer/Track → Clip → TrackElement | `GESPipeline` (3 mod) | **Doğrulanmadı** (issue #2202 açık) | **Var** (`avoid-reencoding`) | LGPL-2+ |
| libopenshot | C++ | `std::list<Clip*>` + `FrameMapper` | `Timeline::GetFrame` + `CacheMemory` | Yön değişimi destekli | Yok | LGPL-3.0-or-later |
| OpenShot (uygulama) | Python/Qt + libopenshot | `.osp` JSON | libopenshot | libopenshot'a devir | Yok | GPL-3.0 |
| Olive Editor | C++/Qt/OpenGL | Node grafiği (0.2) — **kısmen doğrulanmadı** | GL render + önbellek | Doğrulanmadı | Doğrulanmadı | GPL-3.0 |
| Cinelerra-GG | C++ | EDL → Track → `Edit` → `Asset` | kendi motoru + `Proxy` sınıfı | `Track::reverse_edits()` var | Doğrulanmadı | GPL-2.0-or-later |
| Blender VSE | C/C++/Python | `Strip` (eski `Sequence`) DNA dizisi | disk proxy (`SEQ_proxy`) | `use_reverse_frames` bayrağı | Yok (doğrulanmadı) | GPL-2.0-or-later |
| Natron | C++ | Node grafiği | RAM + `DiskCache` node | Geçersiz (kompozit) | Geçersiz | GPL-2.0 |
| Avidemux | C++/Qt | `_SEGMENT` kesme listesi | kendi decoder'ı (`ADM_Composer`) | Yok | **Var** (intra kontrolü ile) | **GPL-2.0-only** |
| VidCutter | Python/Qt + **libmpv** | `clipTimes[]` düz liste | **libmpv + QOpenGLWidget** | Yok | **Var + SmartCut** | GPL-3.0+ |
| LosslessCut | Electron/React + ffmpeg | segment listesi (`.llc`) | HTML5 `<video>`, kesilmemiş kaynak | Yok | **Var + Smart Cut** | GPL-2.0 |
| auto-editor | **Nim** (eski: Python) | `v3`: `Clip{src,start,dur,offset}` | Yok (toplu iş) | Yok | Kısmi | Unlicense |
| moviepy | Python + ffmpeg | `VideoFileClip` / kompozit ağaç | Yok (yazma odaklı) | **Var** (`TimeMirror`) | Yok | MIT |
| ffmpeg-python | Python | Filtre DAG'ı (timeline değil) | Yok | Yok | Yok (yan etki) | Apache-2.0 |
| OpenTimelineIO | C++ + Python | Timeline → Stack → Track → Clip/Gap | Geçersiz (değişim formatı) | `LinearTimeWarp` — semantik yok | Geçersiz | Apache-2.0 |
| Shotstack | Bulut REST/JSON | `timeline` → tracks → clips | Asenkron bulut render | Doğrulanmadı | Yok | Kapalı hizmet |
| LiVES | C/GTK+ | NLE + gerçek zamanlı VJ | gerçek zamanlı motor | Doğrulanmadı | Doğrulanmadı | GPL-3+ |
| Kino | C/GTK+ (**terk**) | DV odaklı | Doğrulanmadı | Doğrulanmadı | Doğrulanmadı | GPL-2.0-or-later |
| Vidiot | C++/wxWidgets | Doğrulanmadı (kaynağa ulaşılamadı) | Doğrulanmadı | Doğrulanmadı | Doğrulanmadı | GPL-3.0 |
| Open Movie Editor | C++ (**terk, 2009**) | node kompozisyon | Gmerlin AV | Doğrulanmadı | Doğrulanmadı | GPL (sürüm belirsiz) |
| Remotion | TypeScript/React | **Kod = çizelge** (`useCurrentFrame()`) | Tarayıcı + headless Chromium | Geçersiz | Yok (her kare yeniden) | **Özel ticari lisans** |
| Motion Canvas | TypeScript generator | `function*` + `yield` zaman akışı | Canlı editör önizleme | Geçersiz | Geçersiz | MIT (doğrulanmadı) |
| editly | Node + ffmpeg | Bildirimsel JSON5 | Yok | Doğrulanmadı | Yok | MIT (doğrulanmadı) |
| Glaxnimate | C++17/Qt | Model/View, keyframe ağacı | Qt canvas | Geçersiz (vektör) | Geçersiz | GPL-3.0 |
| **Sprocket** | **C# / .NET 10 / Avalonia** | `Project→Timeline→Track[]→Clip`, 240000 tick/sn | SkiaSharp GPU + ses ana saat | **Var** (reverse + hız rampası) | Yıkıcı-olmayan model | **MIT** |
| **FramePFX** | **C# / .NET 8 / Avalonia** | kare-tabanlı klip modeli | FFmpeg.AutoGen + async render | Doğrulanmadı | Yok | GPL-3.0+ |

---

## Küme Değerlendirmeleri

### MLT Ekosistemi — Shotcut, Kdenlive, Flowblade

MLT'nin modeli sektörde en olgun olanı: kaynak bir `mlt_producer`, klip onun `mlt_producer_cut()` ile
alınmış bir dilimi, track bir `mlt_playlist`, proje bir `mlt_tractor`. Aynı dosya onlarca kez, sıfır
kopyayla referanslanır (`src/framework/mlt_playlist.c`, `playlist_entry_s` yapısı).

Önizleme tek bir consumer'ın tractor'dan **çekmesiyle** olur. Kesme noktası consumer'ın umurunda değil —
playlist bir sonraki producer'a kendi geçer. Kesintisizlik mimarinin kendisinden gelir, ayrıca kod yazılmaz.

Negatif hız burada gerçek: `src/modules/core/producer_timewarp.c` içinde `speed < 0.0` dalı var, ses
`mlt_audio_reverse()` ile ters çevriliyor, perde `rbpitch` ile düzeltiliyor ama kodun kendi yorumuna göre
`|speed| < 0.1` altında perde düzeltmesi kapatılıyor.

Dördünün de render'ı **tam yeniden kodlama**. Shotcut'ın `src/jobs/encodejob.cpp` içindeki `real_time=-1`
yeniden deneme mantığı bir smart render değil, paralel render başarısız olunca seri moda düşme.
Kdenlive'ın "Lossless/HQ" profilleri (FFV1, HuffYUV, UtVideo) kayıpsız *codec*'tir, stream-copy değil.

Undo üçünde üç ayrı desen: Shotcut `QUndoStack` + komut sınıfları, Kdenlive `Fun = std::function<bool()>`
lambda çiftleri (Qt'nin yığınını kullanmıyor), Flowblade global `undo_stack = []` + `MAX_UNDOS = 35`
sınırı (`src/edit/undo.py`). Üçü de komut yığını, hiçbiri anlık görüntü değil.

Flowblade'in çift defteri dikkat çekici: Python `track.clips` listesi ve MLT playlist'i **elle senkron**
tutuluyor (`src/edit/edit.py`, `_insert_clip`). Proje dosyası XML değil, Python `pickle` (`.flb`) — mutlak
yol sakladığı için taşınabilirlik sorunu bilinen bir şikâyet.

### GStreamer Kümesi — Pitivi + GES

GES'in ayırdığı şey önemli: `GESClip` bir kap, asıl iş `GESTrackElement`'lerde; asset ile klip GObject
`Extractable` arayüzüyle tamamen ayrık (`ges/ges-clip.c`). Bu, aynı kaynağın farklı track'lerde farklı
efektlerle görünmesini bedava yapıyor.

`GESPipeline` üç modlu: Preview (`playsink`), Render (`encodebin`), **Smart Render**
(`avoid-reencoding=TRUE`) — taranan 30 projede "smart render"ı çerçeve seviyesinde sunan tek motor bu
(`ges/ges-pipeline.c`). Kdenlive ve Shotcut'ta karşılığı yok.

Negatif hız GES'te **doğrulanmadı**. GStreamer çekirdeği `gst_element_seek()` ile negatif rate destekliyor
ama GES/Pitivi'de gerçek zamanlı ters oynatma kanıtlanamadı; ilgili GNOME issue #2202 açık görünüyor.

Undo tamamen Pitivi katmanında: `pitivi/undo/undo.py`, `UndoableAction` / `UndoableActionStack` /
`UndoableActionLog` — klasik komut deseni. GES'in kendi edit log'u yok.

Proxy Pitivi'de birinci sınıf: `pitivi/utils/proxy.py` içinde `GstTranscoder` ile düşük çözünürlüklü
kopya üretilip GES asset önbelleğine sokuluyor. Önizleme kaynağı değil, proxy'yi oynatıyor.

### Bağımsız C++ Motorlar — libopenshot, Olive, Cinelerra-GG

libopenshot en okunabilir model: `Timeline` bir `std::list<Clip*>`, `GetFrame(n)` önce `CacheMemory`'ye
bakıyor, yoksa o karede kesişen klipleri bulup katman katman kompozit ediyor ve sonucu önbellekliyor
(`src/Timeline.cpp`). Proje JSON ve `ApplyJsonDiff()` ile **artımlı** güncelleniyor — tam yeniden yükleme yok.

`FrameMapper` (`src/FrameMapper.cpp`) oynatma yönünü biliyor: `SetDirectionHint(bool increasing)` var ve
yön değişince ses resampler'ı sıfırlanıyor. Yani ters oynatma motorda düşünülmüş, üzerine inşa edilebilir.

Cinelerra-GG klasik EDL: `class Edit : public ListItem<Edit>`, `get_source()` ile bir `Asset`'e işaret
ediyor (`cinelerra-5.1/cinelerra/edit.h`). Hız ayrı bir efekt değil, track otomasyonunda bir keyframe
eğrisi — `AUTOMATION_SPEED` / `FloatAutos` (`track.C:257`). `Track::reverse_edits()` API'de doğrudan var.

Cinelerra'nın undo'su bu taramadaki tek **anlık görüntü** örneği: `MainUndo::update_undo_before/after()`
ikisi de `FileXML` alıyor — komut nesnesi değil, EDL'in XML serileştirmesinin öncesi/sonrası saklanıyor
(`cinelerra/mainundo.h`, `edl.C`).

Olive Editor için node-grafiği iddiası GitHub sayfasından **doğrulanamadı**; repo "alpha software and is
considered highly unstable" diyor, resmi site şu an yalnız "Olive will return" gösteriyor. Bu küme eksik.

### Kesme Araçları — Avidemux, VidCutter, LosslessCut

Bunlar NLE değil; çizelgeleri düz bir in/out çiftleri listesi. VidCutter'da `self.clipTimes = []`, her
eleman `[start, end, thumbnail, path, chapter]`. Avidemux'ta `_SEGMENT` yapıları. LosslessCut'ta segment
listesi + `.llc` proje dosyası. Kesişme, katman, geçiş kavramı yok.

**VidShrink için en yakın mimari örnek VidCutter**: python-mpv kullanmıyor, kendi vendored Cython
sarmalayıcısını (`libs/pympv/mpv.pyx`) `QOpenGLWidget` üzerine libmpv render API'siyle bağlıyor
(`mpvwidget.py`). VidShrink'in `MpvEngine`'i zaten aynı deseni C# tarafında kurmuş durumda.

Kayıpsız kesmenin anahtar kare sorununu ikisi de çözüyor. Avidemux `checkSegmentStartsOnIntra()` ile
kesimin intra karede başlamasını zorluyor (`ADM_edVideoCopy.cpp`). LosslessCut'ın `smartcut.ts`'i daha
akıllı: kesim noktasında anahtar kare varsa hiç yeniden kodlamıyor, yoksa **sonraki** anahtar kareyi
bulup aradaki parçayı yeniden kodluyor.

Smartcut'ın kaynak kodundaki iki somut karar kayda değer: kaynak bit hızı `Math.floor(videoBitrate * 1.2)`
ile %20 artırılıyor ("to account for inaccuracies and quality loss"), ve AV1 girişi `libsvtav1`'e
eşleniyor (`src/renderer/src/smartcut.ts`). Tek video akışından fazlası varsa reddediyor.

Undo bu kümede neredeyse yok. Avidemux istisna: 50 adımlık **tam durum anlık görüntüsü** kuyruğu
(`ADM_edUndoQueue.cpp`, `MAX_UNDO_STEPS 50`). VidCutter'da undo yok.

### Toplu İş / Kütüphane Yolu — moviepy, auto-editor, ffmpeg-python, editly

Bunlarda önizleme yok; çizelge bir veri yapısı, çıktı tek seferde yazılıyor. Kararlar VidShrink'e ders
olarak değil, karşı örnek olarak değerli: önizlemesi olmayan bir kurgu aracı yapmıyoruz.

moviepy geri oynatmayı gerçekten sunuyor: `TimeMirror` efekti, docstring'iyle "The same effect is applied
to the clip's audio and mask if any" diyor ve gövdesi tek satır — `clip[::-1]`
(`moviepy/video/fx/TimeMirror.py`). Yani ters oynatma **yalnız çıkışta**, gerçek zamanlı değil.

auto-editor bu tarama sırasında Python'dan **Nim**'e taşınmış durumda (`src/timeline.nim`, varsayılan dal
`master`). `v3` çizelgesinde klip `Clip{src, start, dur, offset}` — hız ayrı bir alan değil, `clipBounds()`
ile `offset`/`dur`'a katlanıyor. Lisansı Unlicense (`gh api` ile doğrulandı, 5225 yıldız).

ffmpeg-python bir çizelge değil, filtre DAG'ı (`InputNode`/`FilterNode`/`OutputNode`). Stream copy için
özel desteği yok, `c='copy'` genel kwarg mekanizmasının yan etkisi. VidShrink'in `VidShrink.Core`'u zaten
bunu C# tarafında yapıyor.

### Değişim Formatları — OpenTimelineIO, EDL, FCPXML

OTIO'nun EDL'den asıl farkı `available_range` (medyanın diskteki gerçek süresi) ile `source_range`
(klibin kullandığı dilim) ayrımı. EDL'de bu ayrım yok, yalnız zaman kodu aralığı var
(`src/opentimelineio/item.h`, `mediaReference.h`).

Hız OTIO'da `LinearTimeWarp(time_scalar: double)` ile temsil ediliyor, ama resmi belge açıkça
"adjusting the time_scalar of a LinearTimeWarp does not affect the duration of the item" diyor — yani
OTIO **yorumlamıyor**, sadece taşıyor. Negatif `time_scalar`'ın standart semantiği **doğrulanmadı**.

CMX3600 EDL'in sınırları VidShrink'in ihtiyacının altında: 999 olay, 4 ses kanalı, pratikte tek video
track. Geçişler satır içi `D nnn` / `W nnn` kodlarıyla. (İkincil kaynaklardan derlendi, birincil SMPTE
258M metnine erişilmedi — **doğrulanmadı**.)

**OTIO'nun resmi C#/.NET bağlaması yok.** nuget.org arama API'si `q=OpenTimelineIO` için `totalHits: 0`
döndürdü. Resmi bağlamalar: Python, C, Swift, Java. C#'tan kullanmak P/Invoke ile C-bindings sarmalamayı
ya da harici süreç çağırmayı gerektirir.

### C# / .NET Tarafı — Sprocket ve FramePFX

Bu taramanın en doğrudan bulgusu: C# ile yazılmış **iki** açık kaynak NLE var, ikisi de Avalonia.

**Sprocket** (SprocketVideo/Sprocket): .NET 10 + Avalonia 12 + SkiaSharp 3.119.4 + FFmpeg 8 (elle yazılmış
P/Invoke) + Silk.NET.OpenAL, **MIT**. Zaman `double` değil `long`, 240.000 tick/saniye (48 kHz ses ve
yaygın kare hızları için tam bölünür). Ses ana saat. Aynı render grafiği hem önizlemeyi hem çıktıyı
besliyor. Sabit hız, **reverse** ve keyframe'li hız rampası destekliyor. Undo ters-komut yığını.
Proje dosyası versiyonlu JSON + autosave + medya relink.

Sprocket'in uyarısı: **7 yıldız, 296 commit, alpha**. Yukarıdaki özellik listesi projenin kendi
README'sinden; bağımsız doğrulaması yok, çalıştırılıp ölçülmedi. Mimari olarak kopyalanacak, kütüphane
olarak bağlanacak bir şey değil — ama VidShrink'in tam olarak durduğu yerde duran tek örnek.

**FramePFX** (AngryCarrot789/FramePFX): C# 12 + .NET 8 + Avalonia, FFmpeg.AutoGen, **GPL-3.0+**,
274 yıldız, 535 commit. README'si kendi eksiklerini sayıyor: **undo yok**, 4K render yavaş, ses fade
yok. Geliştirici kare-tabanlı konumlandırmadan `TimeSpan`'a geçen bir yeniden yazım üstünde.

nuget.org'da NLE/çizelge kütüphanesi **yok** (`OpenTimelineIO`, `libopenshot`, `mlt` sorguları
`totalHits: 0`). libmpv sarmalayıcısı ise bol: `HanumanInstitute.LibMpv.Avalonia` (0.10.1),
`Mpv.NET` (1.1.1), `MediaPlayer.Avalonia.Mpv` (2.1.2) — ama VidShrink kendi `MpvEngine`'ini yazdı,
bunlara ihtiyacı yok.

### Kod-Tabanlı ve Terk Edilmiş Projeler

Remotion, Motion Canvas ve editly'de "çizelge" bir veri yapısı değil, **kodun kendisi**. Remotion'da
sahne bir React bileşeni, zaman `useCurrentFrame()` ile okunuyor. VidShrink'in hedef kitlesi için
alakasız, ama şunu gösteriyor: bir çizelge illa görsel bir ağaç olmak zorunda değil.

Kino (terk), Open Movie Editor (son sürüm 2009), Jahshaka (son ciddi geliştirme ~2009), Vidiot
(kaynağına ulaşılamadı) — dördü de ölü ya da erişilemez. Vidiot'un iç mimarisi hakkında hiçbir iddiada
bulunulmadı, SourceForge meta sayfası dışında kaynak bulunamadı.

Lightworks **hiçbir zaman açık kaynak olmadı**: 2010'da söz verildi, 2011'de süresiz ertelendi, kod hiç
yayınlanmadı. Görev tanımındaki "Lightworks tarzı" ifadesi bir arayüz benzetmesi olarak alınmalı, bir
kaynak olarak değil.

---

## Bulgu: VidShrink İçin Hangi Desen Uygulanabilir

VidShrink'in mevcut yapısı belirleyici: `IPlaybackEngine` **tek dosya** açıyor (`OpenAsync(string path)`),
`MpvEngine` libmpv'yi `vo=libmpv` + yazılım render + BGRA ile sürüyor, çıktı `VidShrink.Ffmpeg`'den
ffmpeg.exe'ye süreç çağrısı. Aşağıdaki değerlendirme bu üç gerçeğe dayanıyor.

### Uygulanabilir — Yüksek Güven

**1. Kesme listesi + `mlt_playlist` tarzı cut modeli.** Klibi "kaynak dosya + in/out + çizelge konumu"
olarak tutmak dile ve motora bağımsız bir desen; MLT (`playlist_entry_s`), libopenshot (`std::list<Clip*>`),
Cinelerra (`Edit::get_source()`), auto-editor (`Clip{src,start,dur,offset}`) ve Sprocket aynı şeyi yapıyor.
C#'ta `record` ile bire bir kurulur, bağımlılık gerektirmez.

**2. Tam sayı tick tabanlı zaman.** Sprocket'in 240.000 tick/sn kararı doğrudan alınabilir: `long`,
48 kHz sese ve yaygın kare hızlarına tam bölünür, kayan nokta birikimi yok. VidShrink'in mevcut
`double PositionSeconds` arayüzü çizelge tarafında değil, yalnız oynatıcı sınırında kalmalı.

**3. Komut yığını undo.** Taranan 30 projeden yığın kullananlar (Shotcut `QUndoStack`, Pitivi
`UndoableActionLog`, Flowblade `EditAction`, Kdenlive `Fun` lambda çiftleri, Sprocket ters-komut)
anlık görüntü kullananlardan (Cinelerra XML diff, Avidemux 50 adım tam durum, Blender memfile) çok daha
kalabalık. C#'ta `interface IEditCommand { void Do(); void Undo(); }` + `Stack<T>` yeterli; Flowblade'in
`MAX_UNDOS = 35` gibi bir tavanı da alınabilir.

**4. mpv `edl://` ile kesintisiz önizleme.** Bu, raporun en değerli tek bulgusu. mpv'nin EDL protokolü
tam olarak VidShrink'in ihtiyacı olan şeyi yapıyor: birden çok dosyadan kesitleri **tek bir oynatılabilir
akış** olarak açmak.

```
# mpv EDL v0
cap.ts,5,240
OP.mkv,0,90,title=Show Opening
```

Sözdizimi: `<dosya>,<başlangıç sn>,<uzunluk sn>`; başlangıç yoksa 0, uzunluk yoksa dosyanın kalanı.
Virgül/noktalı virgül/satırsonu/`!` içeren değerler için `%<baytsayısı>%<değer>` kaçışı var — Windows
yolları ve Türkçe karakterler için bu kaçış **zorunlu kullanılmalı**.
(`DOCS/edl-mpv.rst`, mpv-player/mpv)

Kesintisizliğin mimari kanıtı `demux/demux_edl.c` içinde: her segment bir `tl_part` (`filename`, `offset`,
`length`, `title`) olarak ayrıştırılıp `timeline_part` (`start`, `end`, `source_start`, `source`, `url`)
haline geliyor ve mpv bunu tek bir zaman çizelgesi olarak demux ediyor. Oynatıcı için bu **tek bir dosya**;
kesme noktasında `loadfile` yok, dolayısıyla duraksama yok.

VidShrink'e uyarlaması küçük: `IPlaybackEngine.OpenAsync` zaten bir string alıyor ve `MpvEngine`'in
`loadfile` hedefi uzak şemaları olduğu gibi geçiriyor (`src/VidShrink.Player/AGENTS.md`). `edl://` de bir
şema; hedef çözümleme mantığına eklenmesi yeterli. Çizelge değişince yeni EDL üretilip yeniden
`loadfile` edilir.

**5. Smart cut / kayıpsız kesme.** LosslessCut'ın `smartcut.ts`'i doğrudan okunabilir bir şablon:
kesim noktasında anahtar kare var mı bak, varsa hiç yeniden kodlama, yoksa sonraki anahtar kareyi bul,
sadece aradaki parçayı kaynak bit hızının 1.2 katıyla yeniden kodla, kalanı `-c copy`. VidShrink'in
`VidShrink.Core`'u zaten ffmpeg argümanı üreten katman — buraya oturur. Avidemux'un
`checkSegmentStartsOnIntra()` kontrolü de aynı yerde yapılabilir.

**6. Proxy.** Pitivi (`GstTranscoder`), Blender (`SEQ_proxy`), Cinelerra (`Proxy` sınıfı) ve
Natron (`DiskCache`) aynı çözümü kullanıyor: düşük çözünürlüklü kopya üret, önizlemede onu oynat.
VidShrink ffmpeg'i zaten süreç olarak çağırıyor; proxy üretimi mevcut iş kuyruğuna bir görev tipi.

### Uygulanamaz ya da Yüksek Riskli

**1. Gerçek zamanlı geri oynatma — libmpv ile pratikte kapalı.** mpv'nin `speed` özelliği
`--speed=<0.01-100>`, yani **negatif değer almıyor** (`DOCS/man/options.rst`). Tek yol
`--play-direction=backward` ve mpv'nin kendi belgesi bunu şöyle tarif ediyor:

> "Backward playback is extremely fragile. It may not always work, is much slower than forward playback,
> and breaks certain other features."

> "Backward playback is not exactly a 1st class feature."

Somut engeller, aynı belgeden: geri kod çözme libavcodec'te yok, ileri kod çözüp kuyruğu ters
döndürerek **taklit ediliyor** ("can require buffering an extreme amount of decoded data, and also
completely breaks pipelining"); donanım kod çözmeyle "will probably exhaust all your GPU memory and then
crash a thing or two"; `--cache=yes` ve büyük `--demuxer-max-bytes` zorunlu, yanlış boyutta
"quadratic runtime behavior"; bazı konteyner ve codec'ler desteklenmiyor ve **oynatıcı bunları tespit
etmiyor**; altyazı geri demux'u yok.

Ayrıca `demux_edl.c`'deki `tl_part`/`timeline_part` yapılarında **parça başına hız/rate alanı yok** —
yani EDL ile "bu klip 0.5x, şu klip -1x" demek mümkün değil. EDL yolu ile geri oynatma yolu birbirini
dışlıyor.

**Sonuç: negatif hız yalnızca çıkışta (ffmpeg `reverse`/`areverse`) sunulmalı, önizlemede değil.**
Bu, moviepy'nin (`TimeMirror` → `clip[::-1]`) ve MLT'nin `timewarp:` producer'ının yaptığı şeyin
VidShrink'teki karşılığı. Arayüzde ters klip için gerçek zamanlı önizleme yerine ön-işlenmiş bir ters
proxy üretmek (kısa kesitler için) tek makul yol — ama bu ölçülmedi, bir öneri.

**2. ffmpeg `reverse` filtresinin bellek davranışı sert bir sınır.** Resmi belge her iki filtre için de
aynı şeyi söylüyor: *"this filter requires memory to buffer the entire clip"* — video ve ses akışının
tamamı belleğe alınıyor, öncesinde `trim`/`atrim` öneriliyor (ffmpeg.org/ffmpeg-filters.html).
Uzun bir klibi tersine çevirme isteği doğrudan kabul edilirse RAM'i tüketir. Kullanıcıya süre sınırı
ya da segment segment işleme dayatılmalı. **Gerçek eşik ölçülmedi.**

**3. MLT, GES, libopenshot'u kütüphane olarak bağlamak.** Üçünün de .NET bağlaması yok (nuget
`totalHits: 0`). P/Invoke ile bağlamak, VidShrink'in `MpvEngine`'de zaten yaptığı işin ikinci bir kopyası
demek — üç kütüphane, üç platform, üç ikili dağıtım sorunu. libmpv için `tools/libmpv`'ye sha256'lı
indirme düzeneği kurulmuştu; ikinci bir yerli bağımlılık bu maliyeti ikiye katlar. **Önerilmiyor.**

**4. OTIO'yu proje dosyası biçimi olarak almak.** Şema iyi ama C#'tan erişim yolu yok ve
`LinearTimeWarp` hız semantiğini **yorumlamıyor** — VidShrink'in hız/ters bilgisi OTIO'ya konsa da
oradan geri okunduğunda anlamı tanımsız. Kendi versiyonlu JSON'umuz (Sprocket ve libopenshot'un yaptığı)
daha doğru. OTIO ileride bir **dışa aktarım** hedefi olabilir, iç biçim değil.

**5. Node grafiği (Olive, Natron).** Güçlü ama VidShrink'in hedefi "hedef boyuta sıkıştırma + basit
kurgu". Node grafiği hem arayüz hem motor tarafında düz bir kesme listesinden kat kat pahalı ve
Olive'in kendi durumu ("alpha software and is considered highly unstable") bunun ne kadar zor olduğunun
göstergesi.

**6. Blender'ın anlık görüntü undo'su.** `undo_system.cc`'deki memfile yaklaşımı Blender'ın tüm veri
modelini tek bellek görüntüsü olarak serileştirebilmesine dayanıyor. C#'ta bunun karşılığı her adımda
tüm çizelgeyi klonlamak — komut yığını varken gereksiz.

---

## Lisans Tuzakları

VidShrink **AGPL-3.0**. Bu, alışılmış "GPL bulaşması" endişesini tersine çeviriyor: AGPL-3.0 zaten
copyleft'in en katı ucu, dolayısıyla GPL-3.0 ve LGPL'li kodu içeri almakta sorun yok. Asıl tuzak
başka yerde.

### Asıl Tuzak: GPL-2.0-only

FSF açık: *"the GNU AGPL is not compatible with GPLv2"* ve *"GPLv2 is, by itself, not compatible with
GPLv3"* (gnu.org/licenses/license-list.html). "or later" ibaresi olmayan GPL-2.0-only kod AGPL-3.0 bir
projeye **alınamaz**.

Bu taramada iki proje bu kapıya takılıyor:

- **Avidemux — GPL-2.0-only** (`COPYING`, "or later" ibaresi yok). Kayıpsız kesme mantığı
  (`checkSegmentStartsOnIntra()`) ilgi çekici ama **kodu kopyalanamaz**. Fikri okuyup kendi
  gerçeklememizi yazmak serbest, satır almak değil.
- **LosslessCut — GPL-2.0** (`LICENSE`, GPLv2 başlığı). `smartcut.ts` bu raporda okundu ve tekniği
  anlatıldı — ama **kod alınamaz**. Algoritma fikri (anahtar kare ara, yoksa sonrakine kadar yeniden
  kodla) korunamaz bir fikir; 1.2 katsayısı gibi somut sayılar da öyle. Yine de satır kopyalanmamalı.

Buna karşılık **GPL-2.0-or-later** olanlar sorunsuz: Cinelerra-GG (`cinelerra-5.1/COPYING` + dosya
başlıkları "version 2 ... or any later version"), Blender (SPDX `GPL-2.0-or-later`), Kino.

### Sorunsuz Olanlar

- **GPL-3.0 / GPL-3.0+**: Shotcut, Kdenlive, Flowblade, OpenShot uygulaması, Olive, VidCutter,
  Glaxnimate, FramePFX. AGPL-3.0 ile birleştirilebilir — GPLv3 ve AGPLv3 §13 karşılıklı olarak bu
  birleşimi açıkça izin veriyor; ayrı modüller ayrı lisanslarını korur.
- **LGPL-2.1 / LGPL-3.0**: MLT çekirdeği, GES (LGPL-2+), Pitivi (LGPL-2.1+), libopenshot
  (SPDX `LGPL-3.0-or-later`). Hepsi GPL/AGPL uyumlu.
- **İzin verici**: OpenTimelineIO (Apache-2.0), moviepy (MIT), ffmpeg-python (Apache-2.0),
  auto-editor (Unlicense), **Sprocket (MIT)**. Apache-2.0 GPLv3/AGPLv3 ile uyumlu, GPLv2 ile değil —
  VidShrink AGPL-3.0 olduğu için bizi etkilemiyor.

### Bileşen Lisansları

**libmpv** varsayılan **GPL-2.0-or-later** (`Copyright`, mpv-player/mpv). `-Dgpl=false` ile LGPL-2.1+'a
düşürülebiliyor ama mpv'nin kendi uyarısı var: bu anahtar *"does not in itself create a LGPLv2.1+ license
grant"*. LGPL yapıda kaybolanlar: Linux X11 video çıkışı, OSS ses, NVIDIA/Linux vdpau, jack, DVD, CDDA,
DVB. **VidShrink AGPL-3.0 olduğu için LGPL yapıya ihtiyaç yok** — varsayılan GPL-2.0-or-later libmpv
zaten uyumlu ("or later" sayesinde). Bu, VidShrink lehine bir sadeleşme.

**FFmpeg** varsayılan **LGPL-2.1-or-later**; `--enable-gpl` ile GPL-2.0-or-later bileşenler (libx264,
libx265) eklenir ve bütün FFmpeg GPL olur. `librubberband` GPL olduğu için `--enable-librubberband` de
`--enable-gpl` gerektirir — ses perdesi düzeltmeli hız değişimi düşünülüyorsa bu not önemli.
`--enable-nonfree` ile üretilen ikili **dağıtılamaz**, asla kullanılmamalı (ffmpeg.org/legal.html).

**Kritik ve VidShrink lehine olan nokta:** VidShrink ffmpeg'i **ayrı süreç** olarak çağırıyor
(`VidShrink.Ffmpeg`), libavcodec'i linklemiyor. FSF'nin GPL FAQ'sine göre boru, soket ve komut satırı
argümanları normalde ayrı programlar arası iletişim mekanizmalarıdır; bu şekilde kullanılan modüller
genelde ayrı programlardır. Yani ffmpeg yapısının GPL olması VidShrink'e bulaşmaz. Buna karşılık
`FFmpeg.AutoGen` gibi bir P/Invoke bağlamasına geçilirse (FramePFX'in yaptığı) bu koruma kalkar.

### AGPL §13 ve Dağıtım

AGPL'in ağ maddesi üç koşul birden gerektirir: program değiştirilmiş olacak, değiştirilmiş sürüm ağ
üzerinden kullanıcılara hizmet verecek, kullanıcılar doğrudan o programla etkileşecek. **VidShrink yerel
çalışan bir masaüstü uygulaması olduğu için §13 tetiklenmiyor** — pratik bir ek yük yok.

Mağaza uyumu ayrı bir konu: GPLv2 §6 dağıtım alıcısına ek kısıtlama koymayı yasaklar ve Apple'ın Kullanım
Kuralları (cihaz sınırı, DRM) bunu ihlal ettiği için GPL/AGPL yazılım **macOS App Store'da resmen
dağıtılamaz** (VLC/FSF vakası). VidShrink doğrudan dağıtım yaptığı sürece sorun yok; App Store'a gidilirse
yeniden lisanslama gerekir. Microsoft Store 2022'de açık kaynağa politikasını gevşetti ama AGPLv3'e dair
2026 itibarıyla güncel resmi tavır bu taramada **doğrulanmadı** — karar öncesi güncel policy sayfasına
bakılmalı.

### Özet Kural

Kod satırı **alınabilecek** projeler: Sprocket (MIT), moviepy (MIT), auto-editor (Unlicense),
OTIO (Apache-2.0), ffmpeg-python (Apache-2.0), ve tüm GPL-3.0 / LGPL / GPL-2.0-or-later projeler.

Kod satırı **alınamayacak** projeler: **Avidemux ve LosslessCut** (GPL-2.0-only, AGPL-3.0 ile uyumsuz) —
yalnız fikirleri okunur. Bir de **Remotion**: MIT değil, 4+ çalışanlı şirketler için ücretli Company
License gerektiren özel bir lisans; türev satmak yasak. Hiçbir şekilde temel alınmamalı.

---

## Doğrulanmayanların Listesi

Bu rapordaki şu noktalar kaynak bulunamadığı için açık bırakıldı:

- Olive Editor'ün node grafiği mimarisi, proje dosyası biçimi, önbellek ve ters oynatma desteği —
  resmi site içeriksiz, repo derinliğine inilemedi.
- GES'te gerçek zamanlı negatif rate seek'in çalışıp çalışmadığı (GStreamer çekirdeği destekliyor).
- Blender VSE'de ters oynatmada sesin ne olduğu; VSE'de smart render olup olmadığı.
- Cinelerra-GG'de ters oynatmanın gerçek zamanlı önizlemede çalışıp çalışmadığı; smart render varlığı.
- Vidiot'un tüm iç mimarisi (kaynak koduna erişilemedi).
- MLT `mlt_multitrack.c` içeriği (yapı ilişkisi `mlt_tractor.c`'den çıkarıldı).
- Shotcut'ın C++ çizelge modeli detayları (DeepWiki ikincil kaynak).
- CMX3600'ün 999 olay / 4 ses kanalı sınırları (ikincil kaynaklardan, SMPTE 258M metnine erişilmedi).
- Motion Canvas ve editly'nin lisansları (birincil LICENSE dosyaları okunmadı).
- Microsoft Store'un 2026 itibarıyla AGPLv3'e tavrı.
- Sprocket'in README'sindeki özellik iddialarının hiçbiri çalıştırılıp doğrulanmadı.

Ve tekrar: **bu raporda hiçbir performans, bellek ya da gecikme ölçümü yapılmadı.**

---

## Kaynaklar

- MLT: `src/framework/mlt_producer.c`, `mlt_playlist.c`, `mlt_tractor.c`, `src/modules/core/producer_timewarp.c`, `COPYING` — github.com/mltframework/mlt
- MLT lisans politikası — mltframework.org/docs/copyrightpolicy/
- Shotcut: `src/jobs/encodejob.cpp`, `COPYING` — github.com/mltframework/shotcut
- Kdenlive: `src/timeline2/model/timelinemodel.hpp`, `timelinemodel.cpp` — github.com/KDE/kdenlive
- Flowblade: `flowblade-trunk/Flowblade/src/sequence.py`, `src/edit/edit.py`, `src/edit/undo.py`, `src/render/render.py` — github.com/jliljebl/flowblade
- Pitivi: `pitivi/undo/undo.py`, `pitivi/utils/proxy.py`, `pitivi/render.py`, `COPYING` — github.com/GNOME/pitivi
- GES: `ges/ges-timeline.c`, `ges-clip.c`, `ges-pipeline.c`, `COPYING` — github.com/GStreamer/gst-editing-services
- GES negatif hız issue — gitlab.gnome.org/GNOME/pitivi/-/issues/2202
- libopenshot: `src/Timeline.cpp`, `src/FrameMapper.cpp` (SPDX `LGPL-3.0-or-later`) — github.com/OpenShot/libopenshot
- Olive Editor — github.com/olive-editor/olive ("alpha software and is considered highly unstable")
- Cinelerra-GG: `cinelerra-5.1/cinelerra/{edl.h,edl.C,edit.h,track.h,track.C,asset.h,proxy.h,mainundo.h,render.h,batchrender.h}`, `cinelerra-5.1/COPYING` — github.com/cinelerra-gg/cinelerra-gg
- Blender: `source/blender/makesdna/DNA_sequence_types.h`, `source/blender/sequencer/SEQ_proxy.hh`, `intern/effects/effect_speed.cc`, `blenkernel/intern/undo_system.cc` — github.com/blender/blender
- Natron `DiskCache` — natron.readthedocs.io
- Avidemux: `ADM_segment.cpp`, `ADM_edVideoCopy.cpp`, `ADM_edUndoQueue.cpp`, `PythonScriptWriter.cpp`, `COPYING` — github.com/mean00/avidemux2
- VidCutter: `videocutter.py`, `videoservice.py`, `libs/pympv/mpv.pyx`, `mpvwidget.py`, `setup.py` — github.com/ozmartian/vidcutter
- LosslessCut: `src/renderer/src/smartcut.ts`, `src/renderer/src/edlFormats.ts`, `LICENSE` (GPLv2) — github.com/mifi/lossless-cut
- auto-editor: `src/timeline.nim` (Nim), lisans Unlicense — github.com/WyattBlue/auto-editor
- moviepy: `moviepy/video/fx/TimeMirror.py`, MIT — github.com/Zulko/moviepy
- ffmpeg-python: `nodes.py`, `_filters.py`, `_run.py`, Apache-2.0 — github.com/kkroening/ffmpeg-python
- OpenTimelineIO: `src/opentimelineio/{timeline.h,clip.h,item.h,mediaReference.h,gap.h,transition.h,linearTimeWarp.h}`, `docs/tutorials/otio-serialized-schema.md`, `LICENSE.txt` (Apache-2.0) — github.com/AcademySoftwareFoundation/OpenTimelineIO
- Remotion lisansı — github.com/remotion-dev/remotion/blob/main/LICENSE.md, remotion.dev/docs/license/faq
- Sprocket — github.com/SprocketVideo/Sprocket (MIT, .NET 10 + Avalonia 12 + SkiaSharp + FFmpeg 8)
- FramePFX — github.com/AngryCarrot789/FramePFX (GPL-3.0+, .NET 8 + Avalonia + FFmpeg.AutoGen)
- Lightworks açık kaynak olmadı — fortintam.com/blog/lightworks-is-not-anywhere-close-to-open-source/
- mpv EDL — github.com/mpv-player/mpv/blob/master/DOCS/edl-mpv.rst, `demux/demux_edl.c`
- mpv geri oynatma ve hız — `DOCS/man/options.rst` (`--play-direction`, `--speed=<0.01-100>`, `--video-reversal-buffer`, `--lavfi-complex`)
- mpv lisansı — github.com/mpv-player/mpv/blob/master/Copyright
- ffmpeg `reverse`/`areverse`/`atempo` — ffmpeg.org/ffmpeg-filters.html
- FFmpeg lisansı — ffmpeg.org/legal.html
- Lisans uyumluluğu — gnu.org/licenses/license-list.html, gnu.org/licenses/gpl-faq.html
- nuget.org arama API'si (`azuresearch-usnc.nuget.org/query`) — OpenTimelineIO / MLT / libopenshot sorguları `totalHits: 0`
- VidShrink kendi kaynağı: `LICENSE` (AGPL-3.0), `src/VidShrink.Player/IPlaybackEngine.cs`, `src/VidShrink.Player/AGENTS.md`
