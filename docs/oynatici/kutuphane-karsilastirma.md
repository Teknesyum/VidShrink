# Gömülebilir Video Oynatıcı Kütüphaneleri — Karşılaştırma

VidShrink bağlamı: .NET 8 + Avalonia 11, **AGPL-3.0-or-later**, Windows birincil / macOS
ikincil. Bugünkü yol: ffmpeg'i ayrı süreç olarak çağırıp rawvideo BGRA + ayrı ses borusu +
NAudio ile oynatıyor. Hedef: GOM Player benzeri özellik kapsamı, görüntü Avalonia içinde
çizilecek (airspace sorunu kritik), testler başsız (headless) Avalonia ile koşuyor.

Tarih: 2026-09-11. Sayı ve tarihlerin yanına kaynak var; doğrulanamayanlar işaretli.

---

## 1. libmpv + kendi P/Invoke katmanımız

- **Lisans**: mpv çekirdeği **GPLv2-or-later / LGPLv2.1-or-later** karışık kod tabanı;
  derleme zamanında `--disable-gpl` ile tamamen LGPLv2.1+ yapılabilir, aksi halde GPL
  parçaları devreye girer ve bütün ikili GPL sayılır.
  [mpv/Copyright](https://github.com/mpv-player/mpv/blob/master/Copyright) ·
  [LGPL relicensing tartışması #2033](https://github.com/mpv-player/mpv/issues/2033).
  **AGPL ile uyum**: LGPL modunda **Sorunsuz** (dinamik bağlama, ayrı süreç veya P/Invoke
  ile ayrı ikili — AGPL'in "combined work" sınırı zaten aşılıyor); GPL moduna düşerse hâlâ
  **Sorunsuz** çünkü GPLv2-or-later + AGPLv3-or-later birlikte dağıtılabilir (ikisi de
  "or-later"); tek risk build'in sessizce GPL koluna kaymasıdır — bu yüzden build script'te
  `--disable-gpl` doğrulanmalı.
- **İkili boyutu (Windows x64)**: shinchiro/mpv-winbuild-cmake güncel derleme (2026-09-03,
  commit 69e63f425a) — sıkıştırılmış 7z paket `mpv-dev-x86_64` **31.4 MB**,
  `mpv-x86_64` (runtime) **33.8 MB**.
  [Releases](https://github.com/shinchiro/mpv-winbuild-cmake/releases) — bu 7z arşiv boyutu,
  açılmış `libmpv-2.dll` boyutu **doğrulanamadı** (kaynaklarda 10–60 MB arası geniş bir
  aralık dolaşıyor, derleme seçeneklerine bağlı:
  [mpv issue #9259](https://github.com/mpv-player/mpv/issues/9259)).
  macOS: MPVKit üzerinden xcframework, boyut doğrulanamadı.
- **Platformlar**: Windows / macOS / Linux (BSD dahil).
- **Avalonia entegrasyonu**: mpv'nin render API'si OpenGL (veya `sw`) tabanlı; Windows'ta
  Avalonia'nın D3D11/Vulkan interop'u OpenGL context'i doğrudan paylaşamadığından ANGLE
  (D3D üzerinde OpenGL ES) katmanı gerekir — bu HanumanInstitute'un yaptığı şey.
  Alternatif: `render-api=sw` ile kare kare bir bitmap'e çöz, Avalonia `WriteableBitmap`
  üzerine bas (airspace yok ama CPU yükü yüksek, GOM'daki gibi 4K'da zorlanır — **tahmin**).
  OpenGL/ANGLE yoluyla texture paylaşımı yapılırsa airspace sorunu **yok** (native pencere
  gerekmez).
- **Başsız test uygunluğu**: `sw` render modu başsızda çalışır (GPU context gerektirmez).
  OpenGL modu çalışmaz — Avalonia headless platformu şu an OpenGL context oluşturamıyor:
  [Avalonia discussion #15761](https://github.com/AvaloniaUI/Avalonia/discussions/15761).
- **Donanım kod çözme**: mpv'nin `--hwdec` desteği zengin (d3d11va, nvdec, vaapi, videotoolbox).
- **Özellik kapsamı**: mpv çekirdeği zaten hazır getiriyor — hız (perde korumalı, `speed`
  + `audio-pitch-correction`), kare kare (`frame-step`/`frame-back-step`), A-B tekrar
  (`ab-loop-a/b`), altyazı (srt/ass/vtt gömülü ASS render dahil, `libass`), ses parçası
  seçimi (`aid`), ses/altyazı gecikmesi (`audio-delay`/`sub-delay`), ekran görüntüsü
  (`screenshot`), deinterlace (`vf=yadif` veya `d3d11vp`), döndürme/zoom (`video-rotate`,
  `video-zoom`), bilgi paneli için `mpv_get_property` ile codec/bitrate. **Elle yazılacak**:
  ekolayzer (mpv'de yerleşik yok, `af=lavfi=[superequalizer]` filtre zinciri kurulabilir),
  parlaklık/kontrast (`brightness`/`contrast` property var, GUI slider elle bağlanır), tüm
  Avalonia UI ve P/Invoke katmanı.
- **Performans**: OpenGL yolunda 4K donanım kod çözmeyle GPU'ya yaslanır (kaynaklı sayı yok,
  **tahmin**: düşük CPU). SW render modunda her kare CPU'da BGRA'ya kopyalanır — mevcut
  ffmpeg-rawvideo borusuna benzer sınırlamalar, 4K'da tek çekirdek darboğazı **tahmin**.
- **Bakım**: mpv çekirdeği çok aktif — 36.9k yıldız, son push 2026-09-11, son kararlı sürüm
  v0.41.0 (2025-12-21). [mpv-player/mpv](https://github.com/mpv-player/mpv). .NET tarafında
  "kendi P/Invoke katmanı" bizim yazacağımız kod, bakım yükü tamamen bize ait.
- **Riskler**: P/Invoke katmanını sıfırdan yazmak — büyük mühendislik yükü; ANGLE/OpenGL
  paylaşımı Windows'ta ince ayar ister; GPL/LGPL build ayrımı CI'da sürekli doğrulanmalı.

---

## 2. libmpv + HanumanInstitute.LibMpv / LibMpv.Avalonia

- **Lisans**: sarmalayıcı **MIT** —
  [NuGet HanumanInstitute.LibMpv.Avalonia 0.10.1](https://www.nuget.org/packages/HanumanInstitute.LibMpv.Avalonia).
  Gerçek repo adı `mysteryx93/LibMpv-OpenGL`: MIT, 56 yıldız, son push 2026-07-10.
  [github.com/mysteryx93/LibMpv-OpenGL](https://github.com/mysteryx93/LibMpv-OpenGL).
  Native mpv ikilisi ayrıca indirilir, madde 1'deki lisans/uyum aynen geçerli.
  **AGPL ile uyum**: Sorunsuz (sarmalayıcı MIT, native libmpv ayrı ikili).
- **Paket boyutu**: NuGet sayfasında ayrıştırılmış rakam bulunamadı — **doğrulanamadı**;
  native mpv ikilisi madde 1'deki boyutla aynı.
- **Platformlar**: Windows / macOS / Linux — "Avalonia 12 implementation supporting
  Windows, macOS, and Linux" [NuGet sayfası](https://www.nuget.org/packages/HanumanInstitute.LibMpv.Avalonia).
- **Avalonia entegrasyonu**: OpenGL render-api, Windows'ta ANGLE (D3D11 üzerinde GLES)
  üzerinden; SW render de yedek olarak var. Texture tabanlı olduğundan **airspace sorunu
  yaşanmıyor** — Avalonia denetimleri üstüne binebilir (native pencere değil).
- **Başsız test uygunluğu**: OpenGL yolu başsızda çalışmaz (madde 1'deki aynı kısıt); SW
  render moduna geçilebilirse test edilebilir olabilir — **doğrulanamadı** (kütüphane bunu
  ayrıca desteklediğine dair açık dokümantasyon bulunamadı).
- **Donanım kod çözme**: mpv üzerinden aynen kalıtsal.
- **Özellik kapsamı**: madde 1'deki mpv özellikleri, ama artık **.NET tarafında hazır
  P/Invoke ve strongly-typed komut/özellik katmanı var** (MpvContext) — bu, "kendi katmanımızı
  yazma" işini büyük ölçüde ortadan kaldırıyor. Elle yazılacaklar: yine ekolayzer UI'ı,
  parlaklık/kontrast slider bağlama, tüm oynatıcı arayüzü.
- **Performans**: Madde 1 ile aynı (kaynaklı doğrudan ölçüm yok).
- **Bakım**: Küçük, tek geliştiricili proje (56 yıldız); son push 2026-07-10 — aktif ama
  düşük hacim. Sürüm 0.10.x hâlâ ön-sürüm numaralandırmasında.
- **Riskler**: Küçük topluluk → hata/PR yanıt hızı garanti değil; API henüz 1.0 öncesi,
  kırıcı değişiklik riski var; Avalonia 12 hedeflemesi VidShrink'in Avalonia 11'iyle sürüm
  uyumu doğrulanmalı.

---

## 3. LibVLCSharp + LibVLCSharp.Avalonia

- **Lisans**: LibVLCSharp **LGPLv2.1-or-later** (ticari lisans da mevcut ama LibVLC ikili
  buna dahil değil) —
  [LibVLCSharp README](https://github.com/videolan/libvlcsharp/blob/3.x/src/LibVLCSharp.Avalonia/README.md) ·
  gh api: `license: LGPL-2.1`. `VideoLAN.LibVLC.Windows` native paketi ayrıca
  **LGPLv2.1-or-later / GPL** karışık (VLC eklentilerine göre değişir).
  **AGPL ile uyum**: **Sorunsuz** — LGPL dinamik bağlama + "or-later" ibaresi AGPLv3'le
  birleştirmeye izin veriyor; GPL'e düşen eklentiler devre dışı bırakılabilir/ayrı süreçte
  tutulabilir.
- **Native ikili boyutu**: `VideoLAN.LibVLC.Windows` NuGet paketi 3.0.23.1 (2026-04-16),
  paket boyutu **128.06 MB** — [nuget.org](https://www.nuget.org/packages/VideoLAN.LibVLC.Windows).
  Bu, tüm VLC codec/demux eklentilerini içerdiği için mpv'den kat kat büyük.
- **Platformlar**: Windows / macOS / Linux / Android / iOS.
- **Avalonia entegrasyonu**: Resmi `LibVLCSharp.Avalonia.VideoView`, Linux'ta **ayrı
  kayan (floating) bir X11 top-level pencere** açıp Avalonia denetiminin üzerine bindiriyor
  — bu **klasik airspace sorunu**: layout değişince pencere kendi hücresinden taşabiliyor,
  yanlış konumda görünebiliyor, `UserControl` içine konamıyor (yalnız `Window` içinde
  çalışıyor).
  [winkmichael/EmbeddedVideoView.Avalonia](https://github.com/winkmichael/EmbeddedVideoView.Avalonia) ·
  [jpmikkers/LibVLCSharp.Avalonia.Unofficial](https://github.com/jpmikkers/LibVLCSharp.Avalonia.Unofficial)
  bu sorunu `NativeControlHost` ile çözmeye çalışan resmi olmayan alternatifler —
  yani **resmi paket airspace sorununu tam çözmüyor**, community fork'ları gerekiyor.
- **Başsız test uygunluğu**: Native pencere/HWND gerektiği için başsız Avalonia'da
  **muhtemelen çalışmaz** (doğrulanamadı, ama mimari buna işaret ediyor).
- **Donanım kod çözme**: VLC'nin `--avcodec-hw` ile DXVA2/D3D11VA/VAAPI desteği var.
- **Özellik kapsamı**: VLC çekirdeği zengin — hız, altyazı (srt/ass/vtt), ses parçası
  seçimi, ses/altyazı gecikmesi, ekran görüntüsü, deinterlace, döndür/zoom hazır gelir.
  A-B tekrar ve kare kare ileri/geri libvlc API'sinde var ama LibVLCSharp'ta ince sarmalama
  gerekebilir; ekolayzer VLC'de yerleşik var (`libvlc_audio_equalizer_*`). Elle yazılacak:
  Avalonia airspace düzeltmesi (community fork'u değerlendirme/uyarlama), UI.
- **Performans**: Kaynaklı doğrudan ölçüm yok; VLC'nin genel bilinen davranışı 4K'da
  donanım kod çözmeyle akıcı (**tahmin**, kaynak yok).
- **Bakım**: libvlcsharp — 1809 yıldız, son push 2026-09-08, en son sürüm 3.10.1 (gh api ile
  doğrulandı). Çok aktif, VideoLAN resmi projesi.
- **Riskler**: Airspace sorunu resmi pakette çözülmemiş — production'da community fork'a
  bağımlı kalınabilir; native ikili boyutu (128 MB) VidShrink'in dağıtım boyutunu önemli
  büyütür.

---

## 4. FFmpeg.AutoGen / Sdcb.FFmpeg ile süreç içi oynatıcı (NAudio/OpenAL/miniaudio)

- **Lisans**: FFmpeg.AutoGen sarmalayıcı **MIT** (gh api: `license: MIT`, 1608 yıldız, son
  sürüm v8.0.0.1, 2026-03-14) —
  [github.com/Ruslan-B/FFmpeg.AutoGen](https://github.com/Ruslan-B/FFmpeg.AutoGen).
  Sdcb.FFmpeg sarmalayıcı **LGPL-3.0** (gh api doğrulandı, 407 yıldız, son push
  2025-03-24). Her iki durumda da gerçek FFmpeg native ikilisi ayrı indirilir ve onun
  lisansı (GPL veya LGPL, derleme seçeneğine bağlı) geçerli olur.
  **AGPL ile uyum**: Sarmalayıcı katman Sorunsuz (MIT/LGPL); native FFmpeg ikili LGPL
  build'i kullanılırsa Sorunsuz, GPL build (x264/x265 gibi GPL-only codec'lerle) kullanılırsa
  **Koşullu** — VidShrink zaten AGPL-or-later olduğu için GPL FFmpeg ile birlikte
  dağıtım hukuken mümkün (or-later uyumu), ama "sadece LGPL codec'ler" seçilirse risk sıfıra
  iner.
- **İkili boyutu**: `Sdcb.FFmpeg.runtime.windows-x64` NuGet 7.1.0, paket boyutu **53.64 MB**
  (2024-12-16) — [nuget.org](https://www.nuget.org/packages/Sdcb.FFmpeg.runtime.windows-x64).
  FFmpeg.AutoGen kendi native paket sunmuyor, ayrı FFmpeg shared build indirilmeli (Gyan
  build'i zaten proje kullanıyor — VidShrink.Ffmpeg altında).
- **Platformlar**: Windows / Linux / macOS.
- **Avalonia entegrasyonu**: **Yok, tamamen elle yazılır.** Kod çözülen kareler
  `AVFrame`'den `WriteableBitmap`'e (ya da OpenGL texture'a) elle kopyalanır — bugünkü
  rawvideo BGRA borusuyla aynı prensip, ama ayrı süreç yerine aynı süreçte (in-process),
  boru/süreç overhead'i olmadan. Airspace sorunu **yok** çünkü native pencere kullanılmıyor.
- **Başsız test uygunluğu**: Yüksek — `WriteableBitmap`'e kopyalama GPU context gerektirmez,
  başsız Avalonia'da doğrudan test edilebilir (bugünkü mimariyle aynı avantaj).
- **Donanım kod çözme**: FFmpeg'in `hwaccel` API'si (`av_hwdevice_ctx_create`) her ikisinde
  de erişilebilir ama entegrasyonu **elle** yazılır — hazır gelmiyor.
- **Özellik kapsamı**: **Hiçbir oynatıcı özelliği hazır gelmiyor** — hız, kare kare, A-B
  tekrar, altyazı render (libass'ı ayrıca P/Invoke ile bağlamak gerekir), ses parçası
  seçimi, gecikme, ekran görüntüsü, ekolayzer, parlaklık/kontrast, deinterlace, döndür/zoom,
  bilgi paneli — **tamamı elle yazılır**. Bu, listedeki en yüksek mühendislik yükü olan
  seçenek; büyük olasılıkla mevcut ffmpeg-süreç mimarisinin devamı/genişlemesi gibi.
- **Performans**: Süreç-içi olduğu için ayrı süreç + boru overhead'i kalkar (**tahmin**,
  kaynaklı ölçüm yok) — ama SW kare kopyalama sınırı madde 1 ile aynı kalır.
- **Bakım**: FFmpeg.AutoGen aktif (2026-03 sürüm), Sdcb.FFmpeg 2025-03'ten beri sürüm
  çıkarmamış (son push 2025-03-24) — **görece durgun** ama FFmpeg.AutoGen'in forku olduğu
  için temel API stabil.
- **Riskler**: En büyük mühendislik yükü ve en yüksek hata riski (bellek yönetimi, senkron
  ses/video, altyazı render'ı sıfırdan) — GOM Player kapsamına ulaşmak aylar sürebilir
  (**tahmin**).

---

## 5. Bugünkü yol: ffmpeg ayrı süreç + boru (genişletilerek)

- **Lisans**: Zaten projede olan Gyan GPL derlemesi kullanılıyor — AGPL-or-later ile
  **Sorunsuz** (ayrı süreç, IPC ile haberleşme; "or-later" ibaresi birleşimi zaten kapsıyor).
  Değişen bir şey yok.
- **İkili boyutu**: Mevcut Gyan ffmpeg-full build ~250-500 MB aralığında olabilir
  (VidShrink'in kendi `.calisma`/kurulum betiklerinden doğrulanmalı — burada **doğrulanamadı**,
  proje-özel bir ölçüm gerekir, `tools/VidShrink.Bench` üzerinden alınabilir).
- **Platformlar**: Windows / macOS (ffmpeg statik build mevcut, proje zaten ikisini de
  hedefliyor).
- **Avalonia entegrasyonu**: **Airspace sorunu hiç yok** — kare zaten bir bitmap/byte
  dizisi olarak geliyor, Avalonia `WriteableBitmap` içine yazılıyor, native pencere yok.
  Bu, listedeki en temiz Avalonia entegrasyonu.
- **Başsız test uygunluğu**: **En yüksek** — zaten bugün başsız testler bu boru üzerinden
  çalışıyor (AGENTS.md/CLAUDE.md'de belirtilen mevcut mimari).
- **Donanım kod çözme**: ffmpeg CLI `-hwaccel` bayrağıyla var, ama süreç sınırı ötesinde
  kontrolü zor (GPU belleğinden CPU belleğine kopyalama zorunlu, `rawvideo` çıkışı için).
- **Özellik kapsamı**: ffmpeg CLI ile hız (`-ss`/`atempo` filtresi, perde koruması filtre
  zinciriyle elle), kare kare (bir sonraki kareyi okuma), A-B tekrar (elle segment
  yönetimi), altyazı (`ass`/`subtitles` filtresi burn-in olarak — **etkileşimli
  açma/kapama zor**, çünkü filtre grafiği yeniden başlatma gerektirir), ses parçası seçimi
  (`-map` ile süreç yeniden başlatılır), ekran görüntüsü (kolay, zaten kare akışı var),
  ekolayzir (`-af` ile mümkün ama yeniden başlatma), parlaklık/kontrast (`eq` filtresi,
  yeniden başlatma), deinterlace/döndür/zoom (filtre, yeniden başlatma). **Sorun**: ffmpeg
  CLI parametreleri **statik** — çoğu canlı ayar (ses parçası değiştirme, altyazı açma,
  hız) süreç yeniden başlatma ve senkronizasyon kaybı riski taşır; GOM Player'daki gibi
  "aninda" kontrol için CLI mimarisi doğal değil.
- **Performans**: Ayrı süreç + boru overhead'i mevcut (proje zaten bunu yaşıyor); 4K'da
  rawvideo BGRA boru bant genişliği yüksek (**tahmin**, VidShrink.Bench ile ölçülebilir).
- **Bakım**: ffmpeg projesi çok aktif ve stabil; ekstra bağımlılık riski yok (zaten var).
- **Riskler**: Özellik genişledikçe her canlı kontrol için süreci yeniden başlatma
  ihtiyacı — GOM benzeri "aninda" deneyim mimari olarak zorlanır; bu yaklaşımın tavanı
  düşük.

---

## 6. Flyleaf (FlyleafLib)

- **Lisans**: **LGPL-3.0** (gh api doğrulandı) — [SuRGeoNix/Flyleaf](https://github.com/SuRGeoNix/Flyleaf).
  **AGPL ile uyum**: Sorunsuz (LGPL, dinamik/ayrı derleme birimi olarak kullanılabilir).
- **İkili boyutu**: FFmpeg/DirectX tabanlı, NuGet paket boyutu **doğrulanamadı**.
- **Platformlar**: **Yalnız Windows** — WinUI 3 / WPF / WinForms hedefliyor; Linux/macOS
  desteği yok ("Cross Platform Support (Linux)?" issue #216 hâlâ açık soru olarak duruyor,
  kapatılmadı). VidShrink'in macOS ikincil hedefiyle **doğrudan uyumsuz**.
- **Avalonia entegrasyonu**: **Yok** — resmi doküman ve GitHub sayfası Avalonia'dan hiç
  bahsetmiyor, yalnız WinUI3/WPF/WinForms. Avalonia'ya bağlamak için `NativeControlHost` ile
  Flyleaf'in D3D11 Surface'ini elle host etmek gerekir — denenmemiş, doğrulanamadı.
- **Başsız test uygunluğu**: D3D11 Surface donanıma bağımlı — başsız ortamda **muhtemelen
  çalışmaz** (doğrulanamadı, ama mimari GPU context gerektiriyor).
- **Donanım kod çözme**: Var, DirectX tabanlı hızlandırma güçlü noktası.
- **Özellik kapsamı**: Hız kontrolü, ters oynatma, kare kare ileri/geri, altyazı (bitmap
  altyazı + karakter algılama dahil) hazır geliyor. A-B tekrar ve ekolayzer resmi
  dokümantasyonda **görülmedi** (yok sayılabilir, kesin değil).
- **Performans**: 4K/HDR için optimize edildiği iddia ediliyor (proje açıklaması), bağımsız
  ölçüm kaynağı yok — **tahmin/iddia**.
- **Bakım**: 906 yıldız, son push 2026-08-21, son sürüm v3.11.3 (2026-08-21) — aktif.
- **Riskler**: **Windows-only mimari, macOS hedefiyle uyumsuz** ve Avalonia entegrasyonu
  hiç yapılmamış — VidShrink için en büyük platform riskini taşıyan aday.

---

## 7. Windows Media Foundation / Windows.Media.Playback

- **Lisans**: Windows'un parçası, ayrı bir açık kaynak lisansı yok; .NET tarafı için
  **Vortice.MediaFoundation** (MIT, gh api: 1238 yıldız, son push 2026-09-05,
  [amerkoleci/Vortice.Windows](https://github.com/amerkoleci/Vortice.Windows)) kullanılabilir.
  **AGPL ile uyum**: Sorunsuz (işletim sistemi bileşeni, telif hakkı/dağıtım sorunu yok);
  ama **Windows'a kilitler** — macOS'ta hiç çalışmaz.
- **İkili boyutu**: Yok — işletim sistemi bileşeni, ek dağıtım gerekmiyor.
- **Platformlar**: **Yalnız Windows** (8+). `Windows.Media.Playback` WinRT API'si asıl
  olarak UWP için tasarlandı; masaüstü .NET'te Media Foundation'ın düşük seviye COM API'si
  (`IMFMediaEngine` vb.) kullanılmalı — bu doğrudan WinRT `MediaPlayer` sınıfından daha
  zahmetli.
- **Avalonia entegrasyonu**: Resmi entegrasyon yok; VisioForge gibi ticari SDK'lar Avalonia
  `VideoView` sunuyor ama bunlar Media Foundation'ı sarmalayan **ücretli ürünler**
  ([VisioForge](https://www.visioforge.com/help/docs/dotnet/mediaplayer/guides/avalonia-player/)),
  lisans/fiyat bilgisi görülmedi, ayrı araştırma gerekir.
- **Başsız test uygunluğu**: MF, DXGI/D3D swap chain'e bağımlı — başsız ortamda
  **muhtemelen çalışmaz** (doğrulanamadı).
- **Donanım kod çözme**: MF'nin en güçlü noktası — DXVA tabanlı donanım hızlandırma
  yerleşik.
- **Özellik kapsamı**: Temel oynatma, ses parçası seçimi MF API'sinde var; altyazı (SRT/ASS
  render), A-B tekrar, ekolayzer, kare kare gibi GOM-seviyesi özellikler **tamamı elle**
  COM API üzerinden yazılır — çok düşük seviye, yüksek mühendislik yükü.
- **Performans**: Donanım hızlandırmalı olduğundan iyi olması beklenir (**tahmin**, kaynaklı
  ölçüm yok).
- **Bakım**: Microsoft'un işletim sistemi bileşeni, ayrıca bakım riski yok; Vortice.Windows
  aktif bakımda.
- **Riskler**: **macOS'ta çalışmaz** — VidShrink'in ikincil platform hedefini tamamen
  düşürür; düşük seviye COM API karmaşıklığı yüksek.

---

## 8. GStreamer (.NET bağlayıcısı)

- **Lisans**: GStreamer çekirdek kütüphaneleri (gstreamer, gst-plugins-base) **LGPL-2.1+**;
  bazı eklentiler (gst-plugins-ugly, bazı codec'ler) **GPL**. .NET bağlayıcısı
  `gstreamer-sharp` **LGPL-2.1** (gh api doğrulandı) ama **arşivlenmiş/durgun**: son push
  2021-05-25, 33 yıldız — [gstreamer-sharp/gstreamer-sharp](https://github.com/gstreamer-sharp/gstreamer-sharp).
  Daha yeni fork `aligungr/GstSharpBundle`: LGPL-2.1, 11 yıldız, son push 2025-06-05.
  **AGPL ile uyum**: LGPL çekirdek Sorunsuz; GPL eklentiler kullanılırsa Koşullu (or-later
  ile yine mümkün ama eklenti seçimi denetlenmeli).
- **İkili boyutu**: Doğrulanamadı — GStreamer runtime dağıtımı genelde 100+ MB
  (**tahmin**, kaynak yok).
- **Platformlar**: Windows / macOS / Linux.
- **Avalonia entegrasyonu**: Resmi Avalonia entegrasyonu bulunamadı; GStreamer'ın
  `glimagesink`/`d3d11videosink` çıktısını Avalonia'ya bağlamak tamamen elle yapılacak bir
  iş — hazır köprü yok.
- **Başsız test uygunluğu**: Doğrulanamadı; GStreamer'ın kendi test altyapısı var ama
  Avalonia-headless ile birleşimi denenmemiş.
- **Donanım kod çözme**: GStreamer'ın `vaapi`/`d3d11`/`nvcodec` eklentileri var, iyi destek.
- **Özellik kapsamı**: Çekirdek framework zengin filtre/pipeline mimarisi sunuyor ama
  GOM-seviyesi hazır oynatıcı UI'ı yok — **çoğu elle** kurulur (pipeline tasarımı dahil).
- **Performans**: Kaynaklı veri yok — **tahmin**.
- **Bakım**: **En büyük risk** — resmi C# bağlayıcısı 2021'den beri güncellenmiyor, güncel
  forklar da küçük ve düşük hacimli (11-33 yıldız). GStreamer çekirdeği aktif ama .NET
  köprüsü zayıf halka.
- **Riskler**: .NET bağlayıcı bakımsız; Avalonia entegrasyonu sıfırdan; genel olarak
  listedeki en yüksek "bağlayıcı ölür, biz yalnız kalırız" riski.

---

## 9. Avalonia'nın kendi/ticari medya denetimi (Avalonia Accelerate)

- **Bulgu**: Avalonia'nın **Accelerate** paketi altında resmi `MediaPlayerControl` /
  `Avalonia.Controls.MediaPlayer` NuGet paketi **var**
  ([NuGet 12.0.2](https://www.nuget.org/packages/Avalonia.Controls.MediaPlayer/12.0.2) ·
  [dokümantasyon](https://docs.avaloniaui.net/accelerate/components/media-player/mediaplayercontrol)).
- **Lisans**: Avalonia çekirdeği MIT ve ücretsiz kalıyor, ama **MediaPlayerControl
  Accelerate Pro/Enterprise katmanının parçası** — ticari lisans anahtarı gerektiriyor,
  fiyatlandırma "€89/yıldan başlıyor" olarak duyurulmuş
  ([Avalonia blog](https://avaloniaui.net/blog/avalonia-accelerate-phase1-released)).
  **AGPL ile uyum**: **Koşullu/Uyumsuz** açısı var — kapalı/ticari lisanslı bir bileşeni
  AGPL projesine gömmek, AGPL'in kaynak-açma zorunluluğuyla ticari lisansın "yalnız
  ödeyenler kullanabilir" şartı arasında gerilim yaratır; VidShrink AGPL olarak dağıtılırsa
  bu bağımlılığın kullanıcılara nasıl ulaştığı (kaynak dahil mi, ayrı mı) hukuken
  netleştirilmeli — **doğrulanamadı**, avukat/lisans uzmanı görüşü önerilir.
- **Teknik detay**: Backend, platform desteği, render mekanizması resmi dokümanda
  **ayrıntılı yer almıyor** (WebFetch ile sayfa tarandı, "ses seviyesi/oynat/durdur/ses
  parçası" gibi üst seviye API'ler görüldü, alt seviye mekanizma belirtilmemiş) —
  **doğrulanamadı**.
- **Sonuç**: Var, ama lisans modeli VidShrink'in AGPL doğasıyla sürtüşebilir; teknik
  derinlik dokümantasyonda yetersiz, ayrı bir deneme/inceleme gerekir.

---

## 10. SDL2/SDL3 + ffmpeg (ffplay benzeri)

- **Lisans**: SDL2/3 **zlib** lisansı — çok izin verici, AGPL ile **Sorunsuz**. ffmpeg
  tarafı madde 4/5'teki aynı GPL/LGPL ayrımı geçerli.
- **.NET/C# tarafı**: Aktif, olgun bir "SDL + ffmpeg + C# + Avalonia" hazır kütüphanesi
  **bulunamadı**. En yakın örnekler C/C++ düzeyinde (`fosterseth/sdl2_video_player`) veya
  Avalonia'ya özel hazır sarmalayıcılar değil
  ([arama sonuçları](https://github.com/fosterseth/sdl2_video_player)).
- **Avalonia entegrasyonu**: SDL kendi native penceresini açar (`SDL_CreateWindow`) —
  bunu Avalonia'ya gömmek `NativeControlHost` ile HWND paylaşımı gerektirir, **klasik
  airspace sorunu** madde 3'teki VLC durumuna çok benzer, hazır çözüm yok.
- **Başsız test uygunluğu**: SDL bir pencere/render sürücüsü açtığından başsızda
  **muhtemelen çalışmaz** (doğrulanamadı, ama SDL'nin "dummy" video sürücüsü ile teorik
  olarak mümkün olabilir — **tahmin**).
- **Donanım kod çözme**: ffmpeg tarafından sağlanır, SDL sadece render eder.
- **Özellik kapsamı**: **Hiçbir GOM-seviyesi özellik hazır gelmiyor** — madde 4'teki gibi
  tamamı elle (SDL sadece bir kare/ses çıkış katmanı, oynatıcı mantığı yok).
- **Performans**: SDL donanım hızlandırmalı 2D/3D render sunar, ffplay'in akıcı çalıştığı
  bilinir ama VidShrink bağlamında kaynaklı ölçüm yok — **tahmin**.
- **Bakım**: SDL2/3 çok aktif ve olgun; ama bunu C#/Avalonia'ya bağlayan hazır bir proje
  yok — bu adayın en büyük eksiği kütüphane değil, **entegrasyon boşluğu**.
- **Riskler**: Hazır .NET/Avalonia köprüsü yokluğu, sıfırdan native pencere + airspace
  çözümü + oynatıcı mantığı — mühendislik yükü madde 4 ile aynı seviyede ama ek olarak
  pencere yönetimi sorunu da üstüne biniyor. Pratik faydası düşük, öncelik verilmemeli.

---

## Öne Çıkanlar (olgu özeti, öneri değil)

**AGPL ile sorunsuz VE özellik kapsamı en geniş 2-3 aday:**

1. **libmpv (madde 1/2)** — LGPL modunda tam Sorunsuz, mpv çekirdeği zaten hız (perde
   korumalı), kare kare, A-B tekrar, ASS altyazı, ses parçası/gecikme, donanım kod çözme,
   ekran görüntüsü, deinterlace, döndür/zoom'u **hazır property/command** olarak sunuyor —
   listedeki en geniş hazır özellik seti. HanumanInstitute.LibMpv.Avalonia (madde 2) bu
   kapsamı Avalonia'ya OpenGL/ANGLE ile airspace sorunu olmadan taşıyor, ama küçük/genç bir
   proje (56 yıldız) olduğu için bakım riski mpv çekirdeğinden (36.9k yıldız, çok aktif)
   ayrı değerlendirilmeli.

2. **LibVLCSharp (madde 3)** — LGPL2.1+ ile Sorunsuz, VLC çekirdeği de mpv'ye yakın geniş
   özellik seti sunuyor (altyazı, ekolayzer dahil — mpv'de ekolayzer yerleşik değilken
   VLC'de var). Ancak resmi Avalonia entegrasyonu **airspace sorununu çözmüyor**
   (floating pencere, `UserControl` içine giremiyor) — bu, madde 1/2'ye göre teknik
   dezavantajı; community fork'larıyla (`NativeControlHost`) düzeltilebilir ama resmi paket
   değil.

3. **Bugünkü yol — ffmpeg süreç + boru (madde 5)** — AGPL ile Sorunsuz ve airspace sorunu
   hiç yok (zaten native pencere kullanmıyor), başsız testte en sorunsuz seçenek. Ama
   "hazır özellik" anlamında en zayıf: ffmpeg CLI'nin statik parametre yapısı GOM'daki
   anlık kontrol deneyimini (ses parçası değiştirme, canlı ekolayzer) doğal olarak
   desteklemiyor — her canlı değişiklik süreç yeniden başlatma riski taşıyor.

**Değerlendirme dışı bırakılanlar (olgusal gerekçe):** Flyleaf yalnız Windows hedefliyor ve
Avalonia entegrasyonu hiç yapılmamış (madde 6); Windows Media Foundation macOS'ta hiç
çalışmıyor (madde 7); GStreamer'ın .NET bağlayıcısı 2021'den beri bakımsız (madde 8);
Avalonia Accelerate'in MediaPlayerControl'ü ticari lisanslı ve AGPL ile ilişkisi hukuken
netleştirilmemiş (madde 9); SDL2/3 için hazır bir .NET/Avalonia köprüsü bulunamadı (madde 10).
FFmpeg.AutoGen/Sdcb.FFmpeg (madde 4) ve SDL yolu (madde 10) teknik olarak mümkün ama sıfırdan
oynatıcı yazmak gerektiriyor — en yüksek mühendislik maliyeti taşıyorlar.

---

## Doğrulanamayan / tahmin işaretli noktalar (özet liste)

- Açılmış (extracted) `libmpv-2.dll` gerçek dosya boyutu — geniş kaynak aralığı var, tek
  sayı doğrulanamadı.
- MPVKit macOS xcframework boyutu.
- HanumanInstitute.LibMpv.Avalonia NuGet paket boyutu.
- Her adayın 4K CPU/GPU yükü için VidShrink'e özgü ölçüm yok — hepsi **tahmin** işaretli,
  gerçek karşılaştırma için `tools/VidShrink.Bench` ile üç-dört adayın gerçek prototipinin
  ölçülmesi gerekir.
- Avalonia Accelerate MediaPlayerControl'ün alt seviye render mekanizması ve AGPL ile
  hukuki ilişkisi.
- Flyleaf'in Avalonia'ya `NativeControlHost` ile bağlanabilirliği (denenmiş örnek
  bulunamadı).
- SDL "dummy" video sürücüsüyle başsız test uyumluluğu.
