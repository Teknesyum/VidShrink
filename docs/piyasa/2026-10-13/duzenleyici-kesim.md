# Düzenleyici Sekmesi — Kayıpsız Kesim, Birleştirme Ve Otomatik Kesim

13 Ekim 2026. Kod okuması `main` 9de48d8b (çalışma ağacında `src/` değişikliği yok). Durum: tamam. Eksik tablosu aynı klasördeki `duzenleyici-kesim-eksikler.md` dosyasında (DZA-01 – DZA-44).

Terimler, ilk geçtikleri yerde:

- **Anahtar kare (keyframe):** kendi başına çözülebilen kare. Öbür kareler ona bakarak çözülür; bu yüzden yeniden kodlamadan yalnız anahtar karede kesilebilir.
- **GOP:** iki anahtar kare arasındaki kare grubu.
- **Akıllı kesim (smart cut):** yalnız kesim noktasındaki GOP'u yeniden kodlayıp gerisini bit bit kopyalamak.
- **Kap (container):** mp4, mkv gibi dosya biçimi; içinde görüntü, ses, altyazı izleri, bölümler ve üst veri durur.
- **Yeniden paketleme (remux):** izleri yeniden kodlamadan başka bir kaba taşımak.

## Envanter

Yollar depo köküne göre. `Core/Editing` = `src/VidShrink.Core/Editing`, `App/Editing` = `src/VidShrink.App/Editing`.

### Kesim Ve Teslim

- Üç teslim kipi var: Hızlı, Akıllı, Tam. `Core/Editing/EditExport.cs:6`; arayüz seçimi `App/Editing/EditorView.Teslim.cs:89`; varsayılan Akıllı (`EditorView.Teslim.cs:91`).
- **Hızlı kip** yeniden kodlamaz: ffmpeg concat listesi ve `-c copy`. Her parçanın başı istenen andan önceki anahtar kareye çekilir (`EditExport.cs:533` `KeyframeAtOrBefore`, `:541-543`). Kayan süre kullanıcıya parça başına gösterilmez.
- **Akıllı kip** dört kodekte çalışır: H.264, HEVC, AV1, VP9 (`Core/Editing/SmartCutCodec.cs:43`). 8 ve 10 bit 4:2:0 (`:40-41`, `:49`); taramalı (interlaced) kaynak dışarıda (`:47`); açık GOP'lu anahtar kare sınır sayılmaz (`:78-101`). Ses: AAC ya da sessiz her zaman, Opus yalnız mkv/mp4 çıktıda ve hız/ters klip yokken (`EditExport.cs:116-124`). Ölçümü `docs/olcumler/akilli-kesme-kodekler.md` (dört ölçüt) ve `akilli-kesme-opus.md`.
- Uymayan kaynakta teslim Tam kipe düşer ve arayüz nedenini tek satırla söyler (`EditorView.Teslim.cs:359-370`: birleştirme, metin, klip ayarı, ses, genel düşüş).
- **Tam kip:** libx264 CRF 18 ve AAC (`EditExport.cs:68-70`).
- **Parçaları ayrı dosyalara yazma** var: `EditExport.cs:263`, `:408` (`BuildSegments`); arayüz kutusu `EditorView.Teslim.cs:69-73`. Adlar `<ad>-01`, `-02` (`Core/Editing/EditOutputName.cs:33-37`).
- Çıktı adı sabit sonekli: `-duzenlenmis` (`EditOutputName.cs:9`); uzantı kaynaktan, tanınmazsa `.mkv` (`:11`, `:15`). "Farklı kaydet"te yalnız MP4 ve MKV (`EditorView.Teslim.cs:389-392`). Ad şablonu yok.
- **Üst veri silme kutusu** var (`EditorView.Teslim.cs:81-85`, `EditExport.cs:199`, `:369`, `:389`). Ölçümü `docs/olcumler/meta-sil-donustur-duzenleyici.md`.
- **İz eşlemesi sabit:** ilk görüntü izi ve ilk ses izi (`EditExport.cs:541-542`, `:580-581`, `:656-657`). İkinci ses izi, altyazı izleri ve veri izleri teslimde düşer; kullanıcıya seçim sunulmaz.
- Düzenleyici bölüm ya da etiket yazmaz (`EditExport.cs:387` yorum satırı). ffprobe bölümleri okuyor (`src/VidShrink.Core/MediaInfo.cs:61`, `src/VidShrink.Ffmpeg/FfprobeClient.cs:19`) ama düzenleyici bu listeyi kullanmıyor.

### Çok Kaynak Ve Birleştirme (K7)

- "Kaynak ekle" düğmesi ve menüsü: `App/Editing/EditorView.Kaynak.cs:38-39`; model `Core/Editing/EditTimeline.cs:53` (`AddSource`). Her ek kaynağın dalgası, anahtar karesi ve küçük resmi ayrı taranır (`EditorView.Kaynak.cs:16-21`).
- **Birden çok kaynak varsa teslim her zaman yeniden kodlar.** `EditExport.cs:252` (`Merge`), `:44-45` (`MergeForcedFull`), arayüz notu `EditorView.Teslim.cs:364`, karar `docs/plan-k7-cok-kaynak.md:25`. Aynı kodekli dosyaları kopyalayarak birleştiren yol yok.

### Çizelge İşlemleri

- Böl (`EditTimeline.cs:115`), taşı (`:134`), aralık sil (`:148`), baş ve son kırp (`:302`, `:313`), kenar kırp (`:346`), çoklu sil (`:362`).
- Geri al ve yinele (`EditTimeline.cs:548`, `:558`); komut yığını `Core/Editing/EditCommands.cs:3`.
- Araçlar: seçim, jilet, kaydırmalı kırpma (`App/Editing/EditorKeymap.cs:42-44`).
- Giriş/çıkış işareti ve işaretçi (`EditorKeymap.cs:127-131`). İşaretçiler yalnız bellekte (`App/Editing/EditorTimeline.cs:43`, `:402`); proje dosyasına yazılmaz (`Core/Editing/EditProject.cs:94-130` içinde işaretçi alanı yok). Parça etiketi yok.
- Kare kare gezinme (`EditorKeymap.cs:119-120`), 5 saniye (`:121-122`), önceki ve sonraki kesim noktası (`:123-124`), J/K/L mekiği (`:116-118`).
- Zaman kodu kutusu: yazarak konuma gitme, göreli giriş (`App/Editing/EditorView.Monitor.cs:39`, `:105`; `Core/Editing/EditTimecode.cs:34-41`).
- Yakalama (snap) hedefleri: çizelge başı ve sonu, klip kenarları, oynatma başı, giriş/çıkış işareti, işaretçiler (`EditorTimeline.cs:459-471`). **Anahtar kareler yakalama hedefi değil;** önceki/sonraki anahtar kareye atlayan komut da yok (`EditorKeymap.cs:9-47` listesi).

### Görsel Yardımcılar

- Dalga biçimi: `Core/Editing/AudioPeaks.cs:9`, çizelgede `EditorTimeline.cs:58`, `:89`.
- Küçük resim şeridi: `Core/Editing/TimelineStrip.cs:10`, `App/Editing/EditorTimeline.Serit.cs:15`, `Core/Editing/ThumbnailQueue.cs:19`.
- Anahtar kare çentikleri çizelgede: `EditorTimeline.Serit.cs:36-44`, `:113-130`.

### Otomatik Kesim

- Sessizlik ve siyah kare: `Core/Editing/SilenceCut.cs:12-43`. Ayarlar: eşik (varsayılan −35 dB), en kısa süre (0,5 sn), kenar payı (0,1 sn). ffmpeg `silencedetect` ve `blackdetect` süzgeçleri (`SilenceCut.cs:50-55`, `:90`).
- Akış: tara, bulunan aralıklar çizelgede boyanır, "Aralıkları kes" tek geri alma adımıdır (`App/Editing/EditorView.Sessizlik.cs:17-21`, `:125-131`). Ölçümü `docs/olcumler/duzenleyici-sessizlik-kesme.md`.
- Kısıtlar: yalnız ilk ses izi taranır (`SilenceCut.cs:90`); yalnız ilk kaynak kesilir (`EditTimeline.cs:192` varsayılan `sourceIndex = 0`); öneriler tek tek açılıp kapatılamaz, hepsi birden uygulanır (`EditorView.Sessizlik.cs:125-131`); sessiz yeri silmek yerine hızlandırma yok.
- Sahne değişiminden bölme: `Core/Editing/SceneSplit.cs:9`, `App/Editing/EditorView.Sahne.cs:13-17`, düğme `EditorView.Sessizlik.cs:45`. Eşik sabit (`src/VidShrink.Ffmpeg/SceneDetector.cs:85`, `src/VidShrink.Core/SceneMap.cs:36`); arayüzde ayarı yok. Defterde açık: eşik gerçek filmde ölçülmedi (`.claude/acik.md:13`).

### Proje

- Proje dosyası `.vsproj.json`, JSON, sürüm 2 (`EditProject.cs:41-43`). İçinde kaynak damgası (yol, boy, değişim zamanı: `:11`), klipler, metinler, teslim kipi (`:94-130`) ve ek kaynaklar (`:30`).
- Otomatik kayıt: her düzenlemeden 1 sn sonra (`EditProject.cs:377`, `App/Editing/EditorView.Proje.cs:20`, `:62-68`); uygulama verisinde `duzenleyici-projeleri` klasörü, en çok 50 proje (`EditProject.cs:364-366`); aynı kaynak yeniden açılınca geri yükleme (`EditorView.Proje.cs:100-128`); pencere kapanırken yazma (`:60`).
- Elle "Projeyi kaydet" ve "Proje aç" (`EditorView.Proje.cs:42-43`, `:187-212`).
- Geri al yığını diske yazılmaz (`EditorView.Proje.cs:13-17`).

### Kısayollar

- 39 satırlık varsayılan harita (`EditorKeymap.cs:113-153`), yeniden atanabilir (`:84-91`, `:103`), kısayol paneli `App/Editing/EditorShortcutsPanel.axaml.cs:12`.

### Komut Satırı

- `src/VidShrink.Cli` küçültme komutunda `--kes` ve `--bolum` taşıyor (`src/VidShrink.Cli/CliRequest.cs:425`, `:436`). Düzenleyici projesini koşturan komut yok.

### Önceki Taramadan Bu Yana Kapananlar

`docs/piyasa/tarama-2026-10-05.md` "Karar Gerektirmeyenler" tablosunda alanıma düşen beş kalemin beşi de bugün kodda:

| Eski Kalem | Bugün | Kanıt |
|---|---|---|
| 3. Proje kaydı ve otomatik kayıt | yapıldı | `EditProject.cs:41-43`, `EditorView.Proje.cs:13-17` |
| 13. Bölümleri ayrı dosyalara yazma | yapıldı | `EditExport.cs:408`, `EditorView.Teslim.cs:69-73` |
| 14. Küçük resim şeridi ve anahtar kare işaretleri | yapıldı | `TimelineStrip.cs:10`, `EditorTimeline.Serit.cs:36-44` |
| 15. Sahne değişimiyle bölme | yapıldı | `SceneSplit.cs:9`, `EditorView.Sahne.cs:13-17` |
| 16. Akıllı kesmeyi diğer kodeklere açma | yapıldı | `SmartCutCodec.cs:43` (av1, vp9); canlı AV1+Opus denemesi defterde açık (`.claude/acik.md:28`) |

Eski taramanın şu satırları da eskidi: "Birden çok kaynağı birleştirme: yok" (K7 ile yapıldı), "Proje kaydetme: kodda bulunamadı" (yapıldı), "Küçük resim şeridi, anahtar kare işaretleri: kodda bulunamadı" (yapıldı).

### Kodda Aranıp Bulunamayanlar

Her biri `Core/Editing`, `App/Editing` ve `src/VidShrink.Ffmpeg/EditExportRunner.cs` içinde iki ayrı adla arandı.

- Bölüm düzenleme ya da yazma: `chapter`, `bolum` → yalnız `EditExport.cs:387` ("bölüm yazmaz").
- Döndürme bayrağı: `rotate`, `displaymatrix`, `display_rotation` → yalnız `transpose` süzgeci (`Core/Editing/ClipFilters.cs:50-51`), yani yeniden kodlama.
- Kesim listesi içe ya da dışa aktarma: `csv`, `youtube`, `.edl`, `cue`, `xmeml`, `fcpxml`, `otio` → eşleşme yok. (`EdlPreview.cs` mpv önizlemesi içindir, dışa aktarım değil.)
- Toplu iş: `Batch`, `Toplu`, `Queue`, `kuyruk` → yalnız küçük resim kuyruğu.
- İz seçimi: `AudioTrack`, `SubtitleTrack`, `TrackPick`, `IzSec` → yok; eşleme sabit `0:a:0`.
- Parça etiketi: `Label`, `Etiket`, `ClipName` → yok.
- Kare ya da yalnız ses dışa aktarma: `Snapshot`, `.png`, `AudioOnly`, `ExtractAudio` → düzenleyicide yok.
- Anahtar kare dışını atma: `decimate`, `nokey`, `skip_frame`, `pict_type`, `timelapse` → eşleşme yok; en yakın kod hız ayarı (`Core/Editing/EditTimeline.cs:402`), o da yeniden kodlar.
- Kapak resmi ekleme: `attached_pic`, `CoverArt`, `cover art`, `kapak res` → `Editing/` klasörlerinde eşleşme yok. Kodlama planı kaynakta zaten duran kapağı taşır (`src/VidShrink.Core/StreamMapping.cs:815-828`: MP4'te iz, Matroska'da ek, olmuyorsa düşürüp not yazar); dış dosyadan ya da bir kareden kapak ekleyen kod bulunamadı.
- Akış adresinden dosyaya alma: `m3u8`, `yt-dlp`, `youtube-dl`, `HlsDownload` → yalnız çalma listesi okuma (`src/VidShrink.Core/PlaylistFile.cs:12`) ve oynatıcının uzak adresi çalması; indirme yok.

## Rakipler

21 rakip tarandı. Açık kaynak olanların sürümü ve tarihi `gh api` ile depodan, öbürlerininki resmi sayfadan okundu. Not sütununda "doğrulandı" yazanları kendim gördüm; "ajan" yazanlar alt ajan raporuna dayanır, yani iddiadır.

| Rakip | Sürüm | Tarih | Tür | Kaynak | Not |
|---|---|---|---|---|---|
| LosslessCut | 3.69.0 | 4 Haz 2026 | açık kaynak; Windows, macOS, Linux | [1][2] | doğrulandı; alanın ölçütü, 44.526 yıldız |
| Avidemux | 2.8.1 | 21 Eyl 2022 | açık kaynak; üç platform | [39] | doğrulandı; kararlı sürüm dört yıllık, depo canlı (son itme 12 Eki 2026), gecelik yapı var |
| VidCutter | 6.0.5.1 | 16 Haz 2021 | açık kaynak | [42] | doğrulandı; bakımsız, bilgi 2021 tarihli |
| Shutter Encoder | 20.4 | 28 Eyl 2026 | açık kaynak, ücretsiz | [44][45] | doğrulandı |
| MKVToolNix GUI | 102.0 | 14 Eyl 2026 | açık kaynak; yalnız Matroska (mkv) yazar | [46][47] | ajan |
| MP4Box (GPAC) | 26.07.0 | 24 Tem 2026 | açık kaynak, komut satırı | [48][49] | doğrulandı |
| smartcut | 1.7 | 20 Ara 2025 | açık kaynak, komut satırı | [36] | doğrulandı; README deponun artık güncellenmediğini, işin kapalı kaynak Smart Media Cutter'a taşındığını yazıyor |
| avcut | 0.8 | 15 Tem 2026 | açık kaynak, komut satırı | [37] | doğrulandı; yalnız H.264, deneysel |
| auto-editor | 31.7.2 | 3 Eki 2026 | açık kaynak, komut satırı | [11][12] | doğrulandı; 30.0.0'dan beri bazı özellikler lisans anahtarı ister (ajan) |
| PySceneDetect | 0.7.2 | 12 Eki 2026 | açık kaynak, komut satırı ve kitaplık | [50] | yalnız sürümü okundu |
| SolveigMM Video Splitter | 9.0.2604.22 | 22 Nis 2026 | ücretli; Windows, ayrıca ayrı bir macOS ürünü | [25][26] | doğrulandı; Free, Home, Business, Broadcast planları |
| TMPGEnc MPEG Smart Renderer 6 | 6.1.2.20 | 30 Eyl 2025 | ücretli (69,95 USD); yalnız Windows | [29][30] | ajan |
| Bandicut | 4.2.5 | 11 Şub 2026 | ücretli; Windows | [33][35] | ajan; 3.8.12 (10 Haz 2026) paralel dal |
| Machete | 5.1 Build 44 | 6 May 2024 | ücretli; yalnız Windows | [31][32] | ajan; yeni sürüm görülmedi |
| VideoReDo | bilinmiyor | son sürüm 2022 | ücretli; Windows | [38][56] | satışta olduğu doğrulanamadı |
| Recut | 4.4.8 | 28 Haz 2026 | ücretli (129 USD tek sefer ya da ayda 15 USD); macOS, Windows | [14][15] | ajan |
| TimeBolt | doğrulanmadı | doğrulanmadı | ücretsiz katman ve ücretli (ayda 17 USD, ömür boyu 347 USD) | [16][17] | ajan; sayfada "7.0.7" geçiyor, tarihi yok |
| Gling | doğrulanmadı | doğrulanmadı | ücretsiz katman (ayda 1 saat, filigranlı) ve ayda 20 USD | [18][19] | ajan |
| QuickTime Player | macOS ile gelir | sürüm okunmadı | sistem aracı | [20][21] | ajan |
| Windows Fotoğraflar | Windows ile gelir | sürüm okunmadı | sistem aracı | [22][23] | ajan; eski Video Editor kalktı, yerine Clipchamp öneriliyor |
| Clipchamp | sürüm numarası yok | sayfa tarihsiz | web ve Windows, ücretsiz katman | [24][54] | sessizlik kaldırma sayfası doğrulandı |

## Özellik Matrisi

Okuma anahtarı: `var` resmi kaynakta görüldü. `kısmi` kısıtlı. `?` resmi kaynakta bulunamadı; bu "yok" demek değildir. `yok` yalnız bizim sütunda ya da rakibin kendi belgesi öyle diyorsa yazıldı. `—` aracın işi değil.

### Kesim Ve Birleştirme

| Özellik | Biz | LosslessCut | Avidemux | SolveigMM | TMPGEnc | Bandicut | Shutter Encoder | MKVToolNix | VidCutter |
|---|---|---|---|---|---|---|---|---|---|
| Yeniden kodlamadan kesme | var (Hızlı kip) | var | var (kopya kipi) | var | var | var | var | var | var |
| Akıllı kesim | var (dört kodek) | kısmi (deneysel) | ? | var | var | var (High-Speed kipi) | ? | ? | kısmi (kırılgan) |
| Anahtar kare dışı kesimde uyarı ya da seçenek | yok | var | var | var | ? | kenarı kendiliğinden kodlar | ? | yalnız anahtar karede böler | ? |
| Anahtar kareye atlama | yok | var | var | ? | ? | ? | ? | — | ? |
| Kayıpsız birleştirme | yok | var | ? | var | var | var | var | var | var |
| Ek ses ve altyazı izlerini taşıma | yok | var | kısmi (ses) | var | var (8 ses akışı) | ? | ? | var | var |
| İz seçimi | yok | var | ? | var | ? | ? | kısmi (ses değiştirme) | var | ? |
| Bölüm yazma ya da düzenleme | yok | var | ? | ? | ? | ? | ? | var | var |
| Üst veri düzenleme | kısmi (yalnız silme) | var | ? | ? | ? | ? | ? | var | ? |
| Döndürme bayrağı | yok | var | ? | ? | ? | yalnız kodlamalı kipte | ? | ? | yok (açık istek #52) |
| Parça etiketi | yok | var | ? | ? | ? | ? | ? | — | var (bölüm adı) |
| Eşit, süreye ya da boyuta göre bölme | yok | var | ? | var | ? | var (tek kaynak) | ? | var | ? |
| Çıktı adı şablonu | yok (sabit sonek) | var | ? | ? | ? | kısmi | var | var | ? |
| Proje dosyası | var (.vsproj.json) | var (.llc) | var (Project Script) | ? | var | ? | ? | var (.mtxcfg) | var (.vcp) |
| Geri al ve yinele | var | var | ? | var | var | ? | ? | ? | ? |
| Kesim listesi içe ya da dışa aktarma | yok | var (ikisi de) | ? | içe (EDL) | ? | ? | dışa (.edl) | ? | içe (EDL) |
| Toplu iş ya da kuyruk | yok | yok (README'si öyle diyor) | ? | var (Business planı) | var (Batch Tool) | kısmi | var | var | ? |
| Dalga biçimi | var | var | ? | var | ? | ? | var | — | yok (açık istek #236) |

### Otomatik Kesim

| Özellik | Biz | LosslessCut | auto-editor | Recut | TimeBolt | Gling | Clipchamp | Shutter Encoder | TMPGEnc | VidCutter |
|---|---|---|---|---|---|---|---|---|---|---|
| Sessizlik algılama | var | var | var | var | var | var | var (3 sn ve üstü) | ? | ? | ? |
| Siyah kare algılama | var | var | ? | ? | ? | ? | ? | var | ? | var |
| Sahne değişimi algılama | var (eşik sabit) | var | ? | ? | ? | ? | ? | var | var | ? |
| Hareketsizlik algılama | yok | ? | var | ? | ? | ? | ? | ? | ? | ? |
| Konuşma metninden kesim | yok (K5) | ? | ? (açık istek) | ? | var | var | kısmi (yazıya dökme ister) | ? | ? | ? |
| Reklam adayı algılama | yok | ? | ? | ? | ? | ? | ? | ? | var | ? |
| Bulunanı tek tek düzeltme | yok (topluca keser) | var | — | ? | var | var | var | ? | ? | ? |
| Sessizi hızlandırma | yok | ? | var | ? | var | ? | ? | ? | ? | ? |
| Baş ve son payı ayrı | yok | ? | var | ? | var | ? | ? | ? | ? | ? |
| Taranacak ses izini seçme | yok | var (önizlemede seçili iz) | var | ? | ? | ? | ? | ? | ? | ? |
| Kurgu programına XML | yok | ? (XML listede, yönü okunmadı) | var | var | var | var | ? | ? | ? | ? |
| Komut satırı | kısmi (tek aralık) | kısmi | var | var (4.4) | ? | ? | — | ? | ? | ? |

### Ana Soruların Yanıtı

**1. Akıllı kesim kimde gerçekten çalışıyor?**

Ücretli tarafta olgun. SolveigMM "Frame Accuracy" kipinde ilk ve son GOP'u yeniden kodlar; H.264, HEVC, MPEG-2 ve Mart 2026'dan beri AV1'de kare hassasiyeti verir, VP9'da yalnız GOP hassasiyeti (ajan) [25][26][28]. Ürün sayfası ses tarafında MPEG Audio, PCM, AC3, EAC3, AAC ve Opus'u sayıyor [25]. TMPGEnc MPEG-1, MPEG-2, H.264, HEVC ve VP9'u işler; 4:2:2 10 bit HEVC, HDR ve değişken kare hızı belgeli; ALAC, Opus ve Vorbis sesi akıllı işleyemez (ajan) [29]. Bandicut bu adı kullanmadan aynı işi High-Speed kipinde yapar: kesim noktası ile en yakın anahtar kare arasını yeniden kodlar [34].

Açık kaynakta hâlâ kırılgan. LosslessCut'ta "deneysel" etiketli ve 119 oyla en çok istenen açık kalem [1][4]. VidCutter'ın SmartCut'ı 2021'de kaldı, açık hataları var [42][43]. smartcut H.264, HEVC, VP9, AV1 ve MPEG-2'yi keser ama depo donduruldu [36]. auto-editor Temmuz 2026'da H.264, HEVC ve VP9 için, ardından AV1 için "tam GOP'ları kopyala" yolunu ekledi [12]. avcut yalnız H.264 ve deneysel [37].

Avidemux, Shutter Encoder, MKVToolNix, MP4Box ve Machete'de akıllı kesim doğrulanmadı; Machete hiçbir işlemde yeniden kodlamadığını söylüyor [31].

Bizde var ve varsayılan kip: H.264, HEVC, AV1, VP9; 8 ve 10 bit 4:2:0; taramalı kaynak dışarıda. Rakiplere göre dar kaldığımız üç yer: ses kapısı (yalnız AAC ve koşullu Opus; DZA-04), MPEG-2 (DZA-27), 4:2:2 ve 4:4:4 (DZA-35). Çok kaynakta akıllı kip hiç çalışmıyor (DZA-01).

**2. Kesim noktası anahtar kareye denk gelmeyince rakip ne yapıyor?**

- LosslessCut: kesim kipi seçtirir (anahtar karede ya da normal), anahtar kareye atlama tuşları verir, 3.66'dan beri oynatma başı anahtar karedeyse gösterir, 3.69'dan beri kesim noktası çevresinde az anahtar kare varsa uyarır [1][2][3][5].
- Avidemux: kopya kipinde kaydetmeden önce kesim noktalarını denetler; anahtar karede değilse uyarı penceresi açıp devam onayı ister [40]. Aynı denetim yapıştırmada da çalışır [57]. Menüde önceki ve sonraki anahtar kareye atlama komutu var, kısayolu ok tuşları [41].
- SolveigMM: "K-frame Accuracy" ile "Frame Accuracy" ayrı seçenek; ilki başlangıcı kaydırabilir, kendi yardım sayfası bunu anlatır [25][27].
- Bandicut: sormaz, kenarı kendiliğinden yeniden kodlar [34].
- MP4Box: dört ayrı bölme seçeneği verir; başlangıç en yakın, önceki ya da sonraki rastgele erişim noktasına (RAP, yani anahtar kare) oturur [48].
- MKVToolNix: yalnız anahtar karede böler [46].

Bizde: çentikler çizelgede görünür, Hızlı kip başlangıcı sessizce önceki anahtar kareye çeker. Atlama komutu (DZA-12), kayma bildirimi (DZA-05) ve anlık gösterge (DZA-30) yok.

**3. Otomatik kesim kimde var, çıktısı nasıl kullanılıyor?**

Dört kullanım biçimi var. (a) Doğrudan kesilmiş video: hepsi. (b) Bulunan aralık düzeltilebilir parça olarak gelir: LosslessCut, TimeBolt, Gling, Clipchamp [1][16][18][24]. (c) Kurgu programına XML: auto-editor, Recut, TimeBolt, Gling [11][14][16][18]. (d) Sessiz yeri silmek yerine hızlandırma: auto-editor, TimeBolt [11][16].

LosslessCut taramadan önce parametre penceresi açar: sessizlikte gürültü eşiği ve süre, sahnede en az değişim oranı, ayrıca sonucu "aralığı sınırla" ya da "aralığın ortasından böl" kipiyle parçaya çevirme seçimi [3][58]. Tarama önizlemede seçili izi kullanır: sessizlik seçili ses izine, siyah kare ve sahne seçili görüntü izine bakar [58]. Clipchamp 3 saniyeden kısa duraklamayı önermez, yazıya dökme ister, tek tek ya da topluca siler [24].

Bizde yalnız (a) var ve topluca uygulanır. Eksikler DZA-07, DZA-17, DZA-20, DZA-21, DZA-22, DZA-31.

**4. Parça işleri.** LosslessCut parçaya etiket ve tag verir, ad şablonunda değişken kullanır, parçaları ayrı dosya ya da tek dosya olarak, isterse bölüm işaretiyle yazar [1][6][7]. MKVToolNix ve MP4Box numaralı ad şablonu verir [46][48]. VidCutter klip başına otomatik bölüm yazar, bölüm adı düzenlenir [42]. Bizde ayrı dosya ve tek dosya var; etiket, şablon ve bölüm yok (DZA-18, DZA-19, DZA-08).

**5. Kap içi işler.** En geniş LosslessCut: iz ekleme ve çıkarma, izleri ayrı dosyaya çıkarma, üst veri ve iz bayrağı düzenleme, döndürme bayrağı, bölüm düzenleyici; 3.69 ile kayıpsız kırpma ve iz kaydırma [1][2]. MKVToolNix aynı işleri mkv için yapar [46][47]. Shutter Encoder ses değiştirme ve çıkarma işlerini "dönüştürmeden" başlığında toplar [44]. Bizde düzenleyici yalnız üst veriyi topluca siler (DZA-02, DZA-03, DZA-11, DZA-23 – DZA-26, DZA-38).

**6. Proje ve geri al.** LosslessCut `.llc` (JSON5), VidCutter `.vcp`, MKVToolNix `.mtxcfg`, Avidemux "Project Script", TMPGEnc ve Recut kendi proje dosyasını yazar [6][42][47][39][29][15]. Geri al ve yinele LosslessCut, SolveigMM ve TMPGEnc'te belgeli [1][26][29]. Bizde ikisi de var (`EditProject.cs:41-43`, `EditTimeline.cs:548`). Eksik kalan: işaretçilerin projeye yazılması (DZA-29) ve dosya türü kaydı (DZA-40).

**7. En çok oy alan açık istekler** aşağıdaki bölümde.

### Öne Geçtiğimiz Yerler

- Akıllı kesim bizde varsayılan kip ve dört kodekte çalışıyor; LosslessCut'ta deneysel ve en çok istenen kalem [4].
- Kesilmiş çizelgeyi teslimden önce gerçek hâliyle oynatıyoruz (`Core/Editing/EdlPreview.cs:17`); LosslessCut'ta bu açık istek (#1887, 4 oy) [10].
- Dalga biçimi, küçük resim şeridi ve anahtar kare çentikleri aynı çizelgede; VidCutter'da dalga ve anahtar kare göstergesi açık istek [43].

## Kullanıcı Talepleri

Oylar 13 Ekim 2026'da `gh api` ile okunan başparmak tepkisi sayılarıdır.

### LosslessCut [10]

| İstek | Oy | No | Bizde |
|---|---|---|---|
| Akıllı kesim | 119 | #126 | var |
| Kayıplı işlemler (yeniden kodlama, süzgeç) | 62 | #372 | var; komşu kol |
| Önizlemede ses, görüntü ve altyazı izleri arasında geçiş | 28 | #256 | yok (DZA-10) |
| Çok dosyada toplu iş akışı | 20 | #89 | yok (DZA-09) |
| Çok dosyalı doğrusal olmayan kurgu | 19 | #976 | K7 ile var; komşu kol |
| Dalga biçimini iyileştirme | 18 | #260 | var |
| İzleri tek tek kaydırma | 16 | #216 | yok (DZA-11); LosslessCut 3.69'da ekledi, istek hâlâ açık |
| Anahtar kare kesim konumu doğruluğu | 13 | #330 | yarım (DZA-05) |
| Kayıpsız kırpma, çevirme, döndürme | 12 | #643 | yok (DZA-23, DZA-38); kırpma 3.69'da geldi |
| Bölüm sessize alma | 11 | #495 | komşu kol |
| Kısayollar ve iş akışı | 10 | #254 | var (kısayol paneli) |
| Daha çok ayarı hatırlama | 9 | #371 | bakılmadı |
| Üst veriyi koruma | 7 | #1027 | bakılmadı (Doğrulanamayanlar 11) |
| Oynatırken oynatma başının altındaki parçayı seçme | 7 | #2596 | bakılmadı |
| Altyazı görme ve düzenleme | 7 | #250 | komşu kol |
| Parçalar arasına duraklama, siyah ya da başlık | 6 | #255 | komşu kol |
| İçe ve dışa aktarma biçimleri | 5 | #1340 | yok (DZA-15, DZA-16) |
| Çizelge iyileştirmeleri | 5 | #483 | bakılmadı |
| İzleri yeniden sıralama | 5 | #1942 | yok (DZA-03 kapsamında) |
| Etiket ve tag iyileştirmeleri | 4 | #427 | yok (DZA-19) |
| Aynı kesimi çok dosyaya uygulama | 4 | #868 | yok (DZA-09 kapsamında) |
| Gerçek kesimleri önizlemede oynatma | 4 | #1887 | var |

### VidCutter [43]

Bakımsız olduğu için istekler 2021 ve öncesine ait.

| İstek | Oy | No | Bizde |
|---|---|---|---|
| Birden çok klibi ayrı ayrı kaydetme | 7 | #196 | var |
| Mevcut klibin kesim noktasını değiştirme | 6 | #240 | var |
| Döndürme | 5 | #52 | süzgeçle var, bayrakla yok (DZA-23) |
| Dalga biçimi | 4 | #236 | var |
| Anahtar kare göstergesi | 3 | #237 | var |
| Kayıpsız düzenleme | 3 | #48 | var |

### auto-editor [13]

Açık issue yok; istekler Tartışmalar bölümünde. Sayı tartışma oyudur ve yalnız son güncellenen 50 tartışma sıralandı (ajan).

| İstek | Oy | No | Bizde |
|---|---|---|---|
| Kdenlive'a aktarma | 6 ve 6 | #463, #539 | yok (DZA-17); auto-editor README'si bugün Kdenlive'ı sayıyor |
| Metin üstünden kurgu | 5 | #590 | yok; kapalı K5 |
| Donanım hızlandırma | 4 | #699 | var |
| Çoklu video | 3 | #956 | K7 ile var |

### Çıkarılamayanlar

- **Avidemux:** GitHub'da issue kapalı (`has_issues=false`); istekler forumda ve oy yok. En çok istenenler çıkarılamadı.
- **Shutter Encoder:** en çok oy alan açık istekler bu alanla ilgisiz (ajan).

### Oylardan Çıkan

Kullanıcının en çok istediği şey akıllı kesim; bizde var. Ondan sonraki dört ilgili kalemin dördü bizde eksik: önizlemede iz değiştirme, toplu iş, iz kaydırma, anahtar kare kayması.

## Son İki Yılın Yenilikleri

Ekim 2024 – Ekim 2026.

- **LosslessCut 3.69.0 (4 Haz 2026)** [2]: bit akışı üst verisiyle kayıpsız kırpma ve en-boy oranı; dosya başına iz kaydırma; iz üst verisini silme; kesim noktası çevresinde az anahtar kare uyarısı; birleştirmede var olan bölümleri koruma; anahtar kare dışını atan araç.
- **LosslessCut 3.68.0 (29 Oca 2026)** [3]: dosya açılırken bölümleri içe alma ayarı (her zaman, hiç, sor); YouTube bölümlerini panoya kopyalama düğmesi.
- **LosslessCut 3.67.0 (8 Kas 2025)** [3]: dalga, küçük resim ve anahtar kare görünümü ayarlarını hatırlama.
- **LosslessCut 3.66.1 (24 Eki 2025)** [3]: oynatma başı anahtar karedeyse gösterme; tüm anahtar kareleri okuma komutu; kes-ve-birleştir kipinde etiket dizisi.
- **LosslessCut 3.65.0 (19 Mar 2025)** [3]: işaretçiler (bitişi olmayan parça işaretçi sayılır, proje dosyasına yazılır); birden çok ses izini aynı anda çalma; işaretçileri resim olarak çıkarma.
- **LosslessCut 3.63.0 (7 Eki 2024)** [3]: tekerlekle anahtar kareye atlama; siyah ve sessiz algılamada iki kip (aralığın ortasından bölme ya da aralığı sınırlama); üst veri ve bölüm korumasını açıp kapatma. Önizlemede görüntü ve ses izi değiştirme 3.61.0 ile gelmişti (14 May 2024, iki yılın biraz dışında).
- **auto-editor** [12]: 31.4.0 (28 Tem 2026) H.264, HEVC ve VP9'da tam GOP'ları yeniden kodlamadan kopyalama; 31.4.2 AV1; 30.3.0 OTIO (OpenTimelineIO, kurgu programları arası çizelge biçimi) dışa aktarma; 29.8.0 Final Cut Pro 7 XML içe aktarma; 31.3.0 çözülme geçişi (komşu kol).
- **SolveigMM** [26][28]: 8.1.2410.31 MP4'te Opus ve VP9; 8.2.2510.10 MXF'te çoklu ses önizleme; 9.0.2603.20 AV1'de kare hassasiyeti; 9.0.2604.22 parçalı MP4 (DASH) (ajan).
- **Bandicut** [35]: 4.0.0 döndürme, çevirme ve hız (yalnız kodlamalı kipte); 4.1.0 Cut kipinde çoklu dosya ve kaynak dosya adını koruma; 4.2.1 High-Speed birleştirmede esnek eşleşme ölçütü (ajan).
- **Shutter Encoder 20.3 (31 Ağu 2026)** [45]: tek dosyada çoklu kesim.
- **Recut** [15]: 4.4 (23 Mar 2026) komut satırı ve MP3; 4.4.7 proje dosyası çift tıkla açılır (ajan).
- **smartcut** [36]: 1.7 (20 Ara 2025) son açık sürüm; geliştirme kapalı kaynağa taşındı.
- **Clipchamp** [24][55]: sessizlik kaldırma Nisan 2024'te duyuruldu (iki yılın biraz dışında); bugün ücretsiz katmanda.

Yön: akıllı kesim açık kaynakta yayılıyor ve AV1'e uzanıyor. LosslessCut kap içi kayıpsız işleri genişletiyor. Otomatik kesim araçları kurgu programına aktarmaya ve komut satırına yöneliyor.

Kapalı kararlara denk gelen iki kalem için pazar notu:

- **K5 (konuşmadan metne):** TimeBolt, Gling ve Clipchamp sessizlik kesimini konuşma metnine bağladı; dolgu sözcüğü ve tekrar çekim ayıklama bu araçların ana satış cümlesi oldu [16][18][24].
- **K1 (URL'den indirme):** LosslessCut README'si akış adresinden indirmeyi sayıyor; son iki yılın sürüm notlarında bu konuda yeni kalem görmedim [1].

## Doğrulanamayanlar

1. **Avidemux'un en çok istenen kalemleri.** Issue kapalı, forumda oy yok.
2. **Avidemux'un özellikleri.** Güncel resmi kılavuz bulunamadı. Akıllı kesim, bölüm, üst veri, bayrakla döndürme, otomatik kesim, kuyruk, dalga biçimi ve komut satırı doğrulanmadı. Gecelik yapıların 2.8.1'e ne eklediği okunmadı.
3. **TimeBolt ve Gling sürümü ve tarihi.**
4. **Recut'ın sessizlik ayarları** (eşik, pay).
5. **QuickTime Player, Fotoğraflar ve Clipchamp** kesimde yeniden kodluyor mu.
6. **TMPGEnc:** AV1 desteği, yeni bir ana sürümün varlığı, kesim noktasında anahtar kare gösterimi.
7. **SolveigMM:** kare hassasiyeti hangi planda ve hangi kapta açık (tablodaki işaretler WebFetch çıktısına geçmedi). Ürün sayfası ses tarafında MPEG Audio, PCM, AC3, EAC3, AAC ve Opus'u sayıyor ve düzenlemede yeniden kodlamadığını söylüyor; DZA-04'teki SolveigMM iddiası buna dayanır. Kare hassasiyeti kipinde sesin de kopyalandığı ayrıca yazılı görülmedi.
8. **Anahtar kareye yakalama (mıknatıs).** Hiçbir rakipte belgeli bulunmadı; tabloya yazılmadı.
9. **Taramalı kaynakta akıllı kesim.** Rakip belgelerinde bakılamadı; tabloya yazılmadı.
10. **Bizde değişken kare hızı.** `Core/Editing` içinde `vfr`, `avg_frame_rate`, `r_frame_rate` eşleşmesi yok. TMPGEnc bunu belgeli destekliyor. Ölçüm gerekir; tabloya yazılmadı.
11. **Bizde Tam kipte kaynak bölümleri ve üst veri çıktıya geçiyor mu.** ffmpeg koşmak yasak olduğundan ölçülmedi. DZA-13'teki "yok" hükmü `EditExport.cs:387` satırındaki açıklamaya dayanır.
12. **Shutter Encoder:** döndürmenin kayıpsız olup olmadığı; kayıpsız kesimde kenar kodlama.
13. **Bandicut'ta eşit parçalara bölme.** Yalnız ürün sayfasında görüldü (ajan); soru-yanıt sayfasında yok.
14. **Machete'nin son sürüm tarihi.** Rapor hem 6 Mayıs 2024'ü hem "Ekim 2024'ten beri sürüm yok" cümlesini taşıyor.
15. **VideoReDo satışta mı.**
16. **LosslessCut #216 ve #643.** 3.69.0 sürüm notu ikisini de karşılanmış sayıyor ama istekler açık; kalan kapsam okunmadı.
17. **auto-editor'ın kayıpsız yolunun kısıtları** (ses yeniden kodlanıyor mu).
18. **Oy eşiği.** "Oyla kanıtlı" için 15 oyu ben seçtim. DZA-10 ve DZA-11'in önceliği yalnız buna dayanır; eşik yükselirse ikisi 2'ye iner.
19. **a115 dalı (Premiere uyumu dalga 1).** Defterde açık (`.claude/acik.md:43`); içeriği okunmadı. `main`de XML dışa aktarma kodu yok, DZA-17 buna dayanır.
20. **Defterdeki komşu kusur.** ffprobe Türkçe bölüm adını bozuk okuyor (`.claude/acik.md:14`); DZA-14 yapılırsa önce bu kapanmalı.
21. **Çok kaynakta otomatik kesim.** Bizde tarama yalnız ilk kaynağı keser (`Core/Editing/EditTimeline.cs:192`). Bunu çok dosyada yapan rakip belgeli bulunamadı; auto-editor'da çoklu video açık istek (#956). İç eksik olarak burada duruyor, tabloya yazılmadı.
22. **LosslessCut'ta taranacak ses izi.** Belgede yazmıyor; kaynak kodundan okundu: tarama önizlemede seçili izi kullanır [58]. Ayrı bir "taranacak iz" seçimi görülmedi. DZA-22'deki LosslessCut iddiası bu okumaya dayanır.
23. **Alıntılar.** WebFetch sayfayı özetleyiciden geçirir. Birebir saydığım metinler `gh api` ile okunanlardır (LosslessCut README ve sürüm notu, auto-editor sürüm notu, Avidemux kaynak kodu, smartcut README, VidCutter CHANGELOG).

## Kaynaklar

- [1] https://github.com/mifi/lossless-cut — README özellik listesi
- [2] https://github.com/mifi/lossless-cut/releases/tag/v3.69.0 — 3.69.0 sürüm notu
- [3] https://github.com/mifi/lossless-cut/releases — 3.63 – 3.68 sürüm notları
- [4] https://github.com/mifi/lossless-cut/issues/126 — akıllı kesim isteği, "deneysel" durumu
- [5] https://github.com/mifi/lossless-cut/blob/master/docs/troubleshooting.md — anahtar kare kesim kipi
- [6] https://github.com/mifi/lossless-cut/blob/master/docs/index.md — proje dosyası, içe ve dışa aktarma, etiket
- [7] https://github.com/mifi/lossless-cut/blob/master/docs/file-name-template.md — ad şablonu değişkenleri
- [8] https://github.com/mifi/lossless-cut/blob/master/docs/batch.md — çok dosyalı iş akışı
- [9] https://github.com/mifi/lossless-cut/issues/1340 — içe ve dışa aktarma biçimleri
- [10] https://github.com/mifi/lossless-cut/issues?q=is%3Aissue+is%3Aopen+sort%3Areactions-%2B1-desc — oya göre açık istekler
- [11] https://github.com/WyattBlue/auto-editor — README: algılama, pay, hız, dışa aktarma hedefleri
- [12] https://github.com/WyattBlue/auto-editor/releases — sürüm notları (31.4.0 kayıpsız yol)
- [13] https://github.com/WyattBlue/auto-editor/discussions — kullanıcı istekleri
- [14] https://getrecut.com — Recut özellikleri ve fiyat
- [15] https://getrecut.com/changelog/ — Recut sürüm geçmişi
- [16] https://www.timebolt.io/features — TimeBolt özellikleri
- [17] https://www.timebolt.io/pricing — TimeBolt fiyat
- [18] https://www.gling.ai — Gling özellikleri
- [19] https://www.gling.ai/pricing — Gling fiyat
- [20] https://support.apple.com/guide/quicktime-player/trim-a-movie-or-clip-qtpf2115f6fd/mac — QuickTime kırpma
- [21] https://support.apple.com/guide/quicktime-player/split-a-movie-into-clips-qtpa2d90df3d/mac — QuickTime bölme
- [22] https://support.microsoft.com/en-us/windows/edit-photos-and-videos-in-windows-a3a6e711-1b70-250a-93fa-ef99048a2c86 — Fotoğraflar'da video kırpma
- [23] https://support.microsoft.com/en-us/clipchamp/windows-photos-app/create-films-with-a-video-editor — eski Video Editor'ın durumu
- [24] https://support.microsoft.com/en-US/Clipchamp/how-to-use-silence-removal — Clipchamp sessizlik kaldırma
- [25] https://www.solveigmm.com/en/products/video-splitter/ — SolveigMM özellik ve plan tablosu
- [26] https://www.solveigmm.com/en/products/video-splitter/history/ — SolveigMM sürüm geçmişi
- [27] https://www.solveigmm.com/en/howto/video-splitter-inaccurate-splitting/ — K-frame hassasiyetinde kayma
- [28] https://www.solveigmm.com/blog/en/av1-smart-rendering-in-solveigmm-video-splitter-the-frame-accurate-editing-without-full-re-encoding/ — AV1 kare hassasiyeti
- [29] https://tmpgenc.pegasys-inc.com/en/product/tmsr6.html — TMPGEnc özellikleri
- [30] https://tmpgenc.pegasys-inc.com/en/download/tmsr6.html — TMPGEnc sürüm ve tarih
- [31] https://www.machetesoft.com/ — Machete özellikleri
- [32] https://www.machetesoft.com/machete-version-history.html — Machete sürüm geçmişi
- [33] https://www.bandicam.com/bandicut-video-cutter/ — Bandicut ürün sayfası
- [34] https://www.bandicam.com/bandicut-video-cutter/support/faq/mode/ — High-Speed kipi ve birleştirme koşulu
- [35] https://www.bandicam.com/bandicut-video-cutter/history/ — Bandicut sürüm geçmişi
- [36] https://github.com/skeskinen/smartcut — README: kodekler, ses aktarımı, deponun durumu
- [37] https://github.com/anyc/avcut — avcut
- [38] https://www.videoredo.com/ — VideoReDo
- [39] https://github.com/mean00/avidemux2/releases/tag/2.8.1 — Avidemux 2.8.1
- [40] https://github.com/mean00/avidemux2/blob/master/avidemux/common/gui_savenew.cpp#L469-L473 — kopya kipinde kayıt öncesi anahtar kare uyarısı
- [41] https://github.com/mean00/avidemux2/blob/master/avidemux/qt4/ADM_commonUI/myOwnMenu.h#L140-L147 — "Previous Intra Frame" ve "Next Intra Frame" menü komutları (yukarı ok), siyah kareye atlama; `gh api` ile okundu. Aynı konu forumda: https://avidemux.org/smif/index.php?msg=81714
- [42] https://github.com/ozmartian/vidcutter/blob/master/CHANGELOG — SmartCut, otomatik bölüm, BLACKDETECT, `.vcp`, EDL
- [43] https://github.com/ozmartian/vidcutter/issues?q=is%3Aissue+is%3Aopen+sort%3Areactions-%2B1-desc — VidCutter açık istekleri
- [44] https://www.shutterencoder.com/documentation/ — Shutter Encoder işlevleri
- [45] https://www.shutterencoder.com/changelog — Shutter Encoder sürüm notları (20.3, 20.4)
- [46] https://mkvtoolnix.download/doc/mkvmerge.html — bölme, ekleme, iz seçimi
- [47] https://mkvtoolnix.download/doc/NEWS.md — MKVToolNix sürüm notları, arayüz işlevleri
- [48] https://github.com/gpac/gpac/blob/master/share/doc/man/mp4box.1 — MP4Box bölme, ekleme, bölüm seçenekleri
- [49] https://github.com/gpac/gpac/releases/tag/v26.07.0 — GPAC 26.07.0
- [50] https://github.com/Breakthrough/PySceneDetect/releases — PySceneDetect sürümü
- [51] https://github.com/skeskinen/smartcut/releases/tag/v1.7 — smartcut 1.7
- [52] https://www.solveigmm.com/en/support/doc/video-splitter/interface.html — SolveigMM arayüz belgesi
- [53] https://tmpgenc.pegasys-inc.com/en/product/tmsr6_spec.html — TMPGEnc biçim tablosu
- [54] https://support.microsoft.com/en-us/clipchamp/how-to-trim-videos-images-or-audio-assets — Clipchamp kırpma
- [55] https://alternativeto.net/news/2024/4/microsoft-upgrades-clipchamp-with-ai-powered-silence-removal-and-more-free-features — yalnız duyuru tarihi için (ikincil kaynak)
- [56] https://www.videohelp.com/software/VideoReDo — yalnız VideoReDo'nun durumu için (ikincil kaynak)
- [57] https://github.com/mean00/avidemux2/blob/master/avidemux/common/gui_main.cpp#L619-L652 — yapıştırmada anahtar kare uyarısı
- [58] https://github.com/mifi/lossless-cut/blob/master/src/renderer/src/ffmpegParameters.ts — sahne eşiği (`minChange`), sessizlik ve siyah kare tarama parametreleri; taramanın önizlemede seçili izi kullanması aynı depoda `src/renderer/src/hooks/useSegments.tsx` satır 323–350 ve `src/main/ffmpeg.ts` satır 322, 436, 472 (13 Eki 2026 okuması)

## Komşu Kola Not

- LosslessCut #372 kayıplı işlemler (62 oy) ve #976 çok dosyalı kurgu (19 oy): kullanıcı kesim aracından kurgu istiyor.
- LosslessCut #495 bölüm sessize alma (11 oy), #255 parçalar arasına başlık ya da siyah (6 oy), #250 altyazı düzenleme (7 oy).
- auto-editor 31.3.0 çözülme geçişi ekledi; Bandicut 4.0.0 hız ve döndürmeyi kodlamalı kipe koydu.
- Recut, TimeBolt ve Gling çok kamera ve çok mikrofon eşlemesi sunuyor.
- Yeniden paketleme (kap değiştirme) Dönüştür koluna düşer; LosslessCut ve Shutter Encoder'da ayrı işlev.
