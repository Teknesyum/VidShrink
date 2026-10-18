# Açık Kaynak Adobe Alternatifleri Ve Düzenleyiciye Katkıları

18 Ekim 2026. Soru: son dönemde çıkan açık kaynak Adobe alternatiflerinden VidShrink düzenleyicisi için ne alınabilir? Yalnız araştırma ve plan; kod yazılmadı. Web bilgisi bu tarihte arandı, kod okuması `t0-birlesme-4` çalışma ağacında (`main` 229156399 üstü) yapıldı. Yol kısaltması: `Core/` = `src/VidShrink.Core/Editing/`, `App/` = `src/VidShrink.App/Editing/`.

İşaretler: **doğrulanmadı** = tek ikincil kaynaktan ya da hiç kaynak bulunmadan yazıldı.

## Kısa Cevap

Evet, ama kod alarak değil, **fikir ve açık standart alarak**. VidShrink AGPL-3.0; GPLv3 ve "GPLv2 ya da sonrası" kodu teknik olarak birleştirilebilir, fakat bu projelerin hepsi C++/Qt/Python/Rust, biz C#/.NET'iz — kopyalanacak satır pratikte yok. Asıl kazanç üç yerden gelir:

1. Rakiplerin 2026'da çıkardığı özellikler bizim ffmpeg süzgeç zincirimize birebir iniyor (LUT, kroma anahtar, bulanıklık, otomatik müzik kısma, stabilizasyon, vekil dosya).
2. Açık değişim standartları (OpenTimelineIO, .cube LUT) bizi Kdenlive, Resolve, Premiere ve yeni FilmCraft'la konuşur kılar.
3. Yerel ffmpeg 9.0 (Gyan full) bu süzgeçlerin hepsini zaten taşıyor; yeni bağımlılık gerekmiyor (aşağıda ölçüldü).

## Projeler

| Proje | Adobe Karşılığı | Lisans | Dil / Çatı | Son Sürüm (Tarih) | Etkinlik | Motor / Kütüphane | Öne Çıkan |
|---|---|---|---|---|---|---|---|
| Kdenlive | Premiere | GPL-3.0-or-later (bazı dosyalar başka GPL sürümü) | C++, Qt, KDE Frameworks | 26.08.2 listede; 26.08.0 31 Ağu 2026, 26.08.1 11 Eyl 2026 (26.08.2 tarihi doğrulanmadı) | çok etkin, KDE Gear ile 4 ayda bir | MLT, frei0r, LADSPA, SoX; OTIO içe/dışa aktarım (sürümü doğrulanmadı) | iz sırası değiştirme, çok klipte hız, açılı dönüşüm çapası, değerlere matematik ifadesi, animasyonlu geçiş önizlemesi (26.04), vekil kurgu |
| Shotcut | Premiere | GPL-3.0 | C++, Qt (QML) | 26.9 (28 Eyl 2026) | çok etkin, aylık | MLT, FFmpeg 9.0, frei0r, LADSPA/LV2, deneysel OpenFX ve VST2 (26.6) | otomatik müzik kısma (26.9), 32-bit float ses, RNNoise gürültü süzgeci, HDR önizleme (26.6), hazır varlık kitaplığı "Elements" (26.7), Vulkan'lı konuşmadan metne (26.4) |
| OpenShot | Premiere (giriş) | GPL-3.0; motor libopenshot LGPL-3.0 | Python + C++ (libopenshot), Qt 6 | 4.0.1 (27 Eyl 2026); 4.0 (30 Ağu 2026) | etkin | libopenshot, FFmpeg, OpenCV | Renk görünümü: tekerler, eğriler, LUT, canlı skoplar (waveform, histogram, RGB parade, vektörskop); yerel yapay zekâ nesne maskesi (YOLO, EfficientSAM, Cutie); ekran/kamera kaydı; yerel Qt çizelgesi |
| Olive | Premiere + compositing | GPL-3.0 | C++, Qt, OpenGL/GLSL | 0.2.0 yalnız gecelik derleme, etiketli sürüm yok | durgun; README "alfa, çok kararsız" diyor | OpenColorIO, düğüm tabanlı bileşim | düğümlü bileşim, uçtan uca OCIO renk yönetimi — fikir için iyi, kullanım için hazır değil |
| Blender VSE ("Video Sequencer") | Premiere + After Effects | GPL-2.0-or-later (dağıtılan ikili fiilen GPL-3) | C/C++, Python | 5.0 (18 Kas 2025); 5.1 çıktı; 5.2 LTS 14 Tem 2026 (tek ticari kaynak, doğrulanmadı) | çok etkin | kendi motoru, FFmpeg, OCIO/ACES | klibe bileşim düğümü değiştiricisi, HDR skoplar, slip aracı, çoklu sahne |
| FilmCraft (ArtCraft) — **yeni** | Premiere | MIT veya Apache-2.0 (README); GitHub kenar çubuğu Apache-2.0 | Rust, wgpu | sürüm etiketi doğrulanmadı; ~Ekim 2026 duyuruldu, eşdeğerlik ölçümü 10 Eki 2026 | çok yeni; depo 18 Eki 2026'da güncel | FFmpeg **yok** (yalnız test kahini), wgpu GPU bileşimi, Symphonia (MPL-2.0) | FCP7 XML, FCPXML 1.9–1.11, OTIO, CMX 3600 EDL, AAF, OMF içe/dışa; üç nokta kurgu, ripple/roll/slip/slide, tamsayı tick zamanı, R128 ölçer, MCP sunucusu. Kendi beyanı: "alfa", Premiere'e göre ~%56 iş hazırlığı |
| EffectCraft (ArtCraft) — **yeni** | After Effects | MIT veya Apache-2.0 | Rust | doğrulanmadı | çok yeni | doğrulanmadı | yalnız duyuru; özellik listesi okunmadı |
| OpenCut | CapCut / Premiere (web) | MIT | TypeScript (web), Rust/wgpu WASM bileşici | v0.3.0 (15 Nis 2026, klasik sürüm); Mayıs 2026'dan beri yeniden yazım | etkin ama yeniden yazım iskelet düzeyinde | tarayıcıda çalışır | maskeler, grafik anahtar kare düzenleyici, çıkartmalar |
| LosslessCut | (kesim aracı) | GPL-2.0 ("or later" ibaresi doğrulanmadı) | TypeScript, Electron | 3.69.0 (4 Haz 2026) | etkin | FFmpeg | kayıpsız kesim, akıllı kesim, kesim listesi içe/dışa, bölüm düzenleme, sahne/siyah/sessizlik bulma — bizim Kes sekmesinin en yakın akrabası |
| Natron | After Effects / Nuke | GPL-2.0 (sürüm ibaresi doğrulanmadı) | C++, Qt, Python | 2.5.0 (26 Kas 2022); 2.5.1 pre2 (yılı doğrulanmadı) | düşük; README bakımcı arıyor | OpenFX, OCIO | düğümlü bileşim, roto, izleme |
| Friction | After Effects (hareketli grafik) | GPL-3.0 (sayfada gösterilmedi, doğrulanmadı) | C++, Qt, Skia | 1.0.0-rc.3 (31 Ara 2025) | etkin ama 10 aydır yeni etiket yok | Skia, FFmpeg | vektör/raster canlandırma, SVG dışa aktarım |
| Glaxnimate | After Effects (vektör/Lottie) | GPL-3.0 (doğrulanmadı) | C++, Qt, KDE | 0.6.0 (3 Mar 2026) | etkin; KDE'ye katıldı, Kdenlive içinde kullanılıyor | — | Lottie/SVG canlandırma, anahtar kare, bezier düzenleme |
| Audacity | Audition | GPL-3.0 (kod çoğunlukla GPL-2.0-or-later, doğrulanmadı) | C++, Qt 6 (4.0'da wxWidgets'tan geçti) | 4.0.1 (30 Eyl 2026); 4.0 (3 Eyl 2026) | etkin | kendi motoru, LV2/VST3 | yeni klip modeli, `.aup4`; 4.0'da bazı eklenti barındırma özellikleri eksik |
| Ardour | Audition (çok izli) | GPL-2.0-or-later | C++, GTK | 9.7 (~18 Haz 2026, forum) | etkin | LV2, VST3, LADSPA | bölge efektleri, otomasyon |
| HandBrake | Media Encoder | GPL-2.0-only | C, C#/WPF (Windows arayüzü) | 1.10.1 (23 Ağu 2025); 2026 sürümü bulunmadı | yavaş | FFmpeg, x264/x265, SVT-AV1 | hazır ayarlar, "Social 10MB" ön ayarı — bizim çekirdek işimizle örtüşüyor |
| MLT (motor) | — | LGPL-2.1-or-later (bazı modüller GPL) | C/C++ | 7.36.1 (31 Ara 2025) | etkin | FFmpeg, frei0r, Movit, LADSPA | CPU'da doğrusal ışıkta işleme ve 10-bit (7.36) |
| OpenTimelineIO (standart) | — | Apache-2.0 | C++ çekirdek, Python bağı; .NET bağı yok | 0.18.1 (Kas 2025, paketleyici tarihi) | ASWF altında etkin | — | `.otio` JSON çizelge; FCPXML/EDL/AAF uyarlayıcıları ayrı eklenti paketinde |
| OpenColorIO (standart) | — | BSD-3-Clause | C++ | doğrulanmadı | ASWF | — | renk yönetimi, LUT pişirme (`ociobakelut`, sayfada doğrulanmadı) |
| frei0r (eklenti seti) | — | GPL-2.0 (sayfada "or later" yok) | C/C++ | doğrulanmadı | düşük | FFmpeg `frei0r` süzgeci yükler | 100+ efekt eklentisi |

"Cavalry benzeri" (prosedürel 2B hareket) açık kaynak bir proje bulunamadı; en yakını Friction ve Glaxnimate. "Son dönemde çıkan" diye duyulan haber büyük olasılıkla **ArtCraft paketi** (FilmCraft, EffectCraft, PhotoCraft…; birkaç gün önce duyuruldu, Rust, MIT/Apache, kodunu bir yapay zekâ modelinin yazdığı belirtiliyor, kendi beyanıyla alfa).

## VidShrink Düzenleyicisinin Bugünkü Durumu (Kodda Doğrulandı)

**Var:**

- Tek video izi V1, klip sesi A1, müzik izi A2 (`Core/EditMusic.cs`, teslim `amix`, `Core/EditExport.cs`). Önizleme müziği çalmıyor (later.md, DZB-P7 durumu).
- Geçişler: erime, silme, kaydırma, iris → `xfade` + `acrossfade` (`Core/ClipTransition.cs`). Önizlemede gösterilmiyor, yalnız Tam kipte kodlanıyor.
- Renk: parlaklık/kontrast/doygunluk (`eq`), renk sıcaklığı (`colortemperature`), eğriler (`curves`), üç bölge renk dengesi (`colorbalance`), tek tık düzeltme (`normalize`) (`Core/ClipGrade.cs`, `Core/ClipEffects.Grade.cs`).
- Ses: gürültü temizleme (`afftdn`), `loudnorm`, `acompressor`, `alimiter`, `equalizer`, giriş/çıkış solması (`Core/ClipFilters.cs`).
- Geometri: kırpma, 90° döndürme, çevirme (`crop`, `transpose`, `scale`, `pad`).
- Hız: hız rampası `setpts`/`atempo`, akıcı ağır çekim `minterpolate` (`Core/SpeedRamp.cs`).
- Resim klibi, düz renk (siyah/beyaz), kare dondurma (`Core/StillClip.cs`).
- Metin katmanı `.ass` ile, konum anahtar karesi modelde (`Core/TextLayer.cs`, `Core/AssWriter.cs`).
- Sessizlik kesme, sahne bölme, akıllı kesim (`Core/SilenceCut.cs`, `Core/SceneSplit.cs`, `Core/SmartCutCodec.cs`).
- Kesim listesi: CSV, CMX EDL, YouTube bölüm metni, FCPXML **yalnız yazma**; okuma CSV, EDL, CUE (`Core/CutList.cs:55-57`).
- Bölümler, işaretler, geri al, mıknatıs.

**Yok (kodda arandı, boş döndü):** `overlay` (ikinci video izi), `chromakey`/`colorkey`, `gblur`/`boxblur` (bulanıklık ve bölge gizleme), `lut3d`/`.cube`, skoplar (waveform/vektörskop), vekil dosya, OpenTimelineIO, stabilizasyon (`vidstab`/`deshake`), otomatik müzik kısma (`sidechaincompress`), klip ayarlarına anahtar kare, harmanlama kipleri, FCPXML okuma, düzenleyicide otomatik altyazı.

**Yerel ffmpeg 9.0 (Gyan full) süzgeç listesinde var:** `lut3d`, `haldclut`, `lut1d`, `chromakey`, `colorkey`, `despill`, `gblur`, `delogo`, `vidstabdetect`, `vidstabtransform`, `deshake`, `arnndn`, `anlmdn`, `sidechaincompress`, `overlay`, `blend`, `maskedmerge`, `zoompan`, `perspective`, `rotate`, `whisper`, `frei0r`, `libplacebo`, `zscale`, `tonemap`. **Yok:** `ocio`. `frei0r` süzgeci var ama eklenti dosyaları (`.dll`) pakette yok. Linux/macOS ffmpeg yapılarında aynı listenin olup olmadığı doğrulanmadı (BtbN gpl yapısında `frei0r` ve `whisper` doğrulanmadı).

## Lisans Uyumu

VidShrink lisansı **AGPL-3.0** (`LICENSE`).

| Kaynak Lisansı | Kod Kopyalamak | Not |
|---|---|---|
| MIT, BSD-3, Apache-2.0 (FilmCraft, OpenCut, OTIO, OCIO) | **uygun** | telif ve lisans bildirimi korunur; Apache-2.0 GPLv3/AGPLv3 ile uyumlu |
| LGPL-2.1+/LGPL-3 (MLT, libopenshot) | **uygun** | AGPL kapsamına alınabilir ya da ayrı kitaplık olarak bağlanabilir |
| GPL-3.0 / GPL-3.0-or-later (Shotcut, Kdenlive, OpenShot, Olive, Friction, Glaxnimate) | **uygun** | AGPLv3 §13 GPLv3 kodla birleştirmeye izin verir; birleşik çalışmanın GPL kısmı GPLv3 kalır, AGPL ağ maddesi bizim kısma uygulanır. Kaynak gösterimi zorunlu |
| GPL-2.0-or-later (Blender, Ardour) | **uygun** | "or later" sayesinde GPLv3 olarak alınır, sonra yukarıdaki gibi |
| GPL-2.0-only (HandBrake, büyük olasılıkla LosslessCut, frei0r, Natron — ibareler doğrulanmadı) | **uygun değil** | GPLv2-only, GPLv3/AGPLv3 ile birleştirilemez. Kod kopyalanmaz; fikir alınabilir. frei0r'u ffmpeg üzerinden ayrı süreçte çalıştırmak "birleştirme" sayılmaz ama eklentileri pakete koymak ayrı bir dağıtım kararıdır |
| MPL-2.0 (Symphonia) | uygun (dosya bazlı) | gerek yok |

Pratik hüküm: **GPL kodu kopyalamak lisans açısından çoğu projede serbest, ama gereksiz** — dil farkı yüzünden satır taşınmaz. Fikirleri, ffmpeg süzgeç adlarını ve açık dosya biçimlerini almak lisans sorunu doğurmaz. Biçimler (OTIO JSON şeması, .cube, FCPXML) telif konusu kod değil, spesifikasyondur; kendi C# okuyucumuzu yazmak her durumda temiz.

Not: LUT dosyaları ve hazır varlıklar (Shotcut "Elements" gibi) ayrı lisanslıdır; pakete hazır LUT konacaksa her birinin lisansı ayrıca okunur.

## Öneriler

### Fikir Olarak Alınacaklar (ffmpeg Zincirimize İner)

| No | Öneri | Kimden | ffmpeg Karşılığı | İş | Öncelik | later.md Bağı |
|---|---|---|---|---|---|---|
| AA-01 | LUT uygulama: `.cube`/`.3dl` dosyasını klibe yükle, yoğunluk sürgüsü | OpenShot 4.0, Kdenlive, Blender | `lut3d=file=…` (+ yoğunluk için `blend` ya da `mix`) | küçük | 1 | DZB-P13 renk yarısının devamı |
| AA-02 | Otomatik müzik kısma: konuşma varken A2 düzeyi iner | Shotcut 26.9 | `sidechaincompress` (A1 yan zincir, A2 hedef) | küçük–orta | 1 | DZB-46 (DZB-P15) |
| AA-03 | Bulanıklık ve bölge gizleme (yüz, plaka) | OpenShot, Kdenlive | `gblur`, `boxblur`, `delogo`; dikdörtgen için crop+blur+overlay | orta | 1 | DZB-28 (DZB-P13'ün açık yarısı) |
| AA-04 | İkinci video izi + kroma anahtar + harmanlama kipleri | hepsi; FilmCraft 26 kip | `overlay`, `chromakey`, `despill`, `blend=all_mode=…` | büyük | 1 | DZB-02, DZB-29 (DZB-P12); önizleme kararı (DZB-P8) sahibin |
| AA-05 | Stabilizasyon (titrek çekim) | Kdenlive, Shotcut, OpenShot | iki geçiş `vidstabdetect` → `vidstabtransform`; hızlı yol `deshake` | orta | 2 | yeni satır |
| AA-06 | Skoplar: waveform, histogram, vektörskop | OpenShot 4.0, Blender 5.0 | önizleme karesi üstünde `waveform`, `histogram`, `vectorscope` süzgeçleri ya da C# hesap | orta | 2 | yeni satır |
| AA-07 | Vekil dosyalarla kurgu | Kdenlive, Shotcut | düşük çözünürlüklü ara kopya üretimi (bildik kodlama yolumuz) | orta | 2 | DZB-51 (DZB-P16) |
| AA-08 | Konuşmadan otomatik altyazı | Shotcut 26.4 (Vulkan GPU) | ffmpeg 9.0 `whisper` süzgeci (model dosyası indirme ister → sahip kararı) | orta | 2 | DZB-P16 model soruları yanında |
| AA-09 | Klip ayarlarına anahtar kare + değere ifade | Kdenlive 26.08, OpenShot 3.4, FilmCraft | süzgeç değerlerine zaman ifadesi (`t`), `sendcmd` | büyük | 2 | DZB-25, DZB-26 (DZB-P14 açık yarısı) |
| AA-10 | Gürültü temizlemede ikinci seçenek (RNNoise) | Shotcut 26.6 | `arnndn` (model dosyası ister) ya da `anlmdn` (modelsiz) | küçük | 3 | ARC-P1 yanında |
| AA-11 | Kurgu araçları: roll, slip, slide, üç nokta kurgu | FilmCraft, Blender 5.0 (slip) | çizelge modeli; süzgeç gerekmez | orta | 3 | DZB-09, DZB-13–15 |

### Doğrudan Kullanılabilecek Standart ve Kütüphaneler

| No | Standart | Lisans / Uyum | Nasıl | İş | Öncelik |
|---|---|---|---|---|---|
| ST-01 | **OpenTimelineIO `.otio`** içe/dışa aktarım | Apache-2.0, uyumlu | .NET bağı yok; `.otio` JSON şemasını C#'ta kendimiz yazıp okuruz (Python/C++ bağımlılığı almayız). Tek iz + geçiş + işaret alt kümesi yeterli. Kdenlive, Resolve ve FilmCraft okuyor | orta | 1 |
| ST-02 | **.cube LUT** (Adobe/Resolve standardı) | spesifikasyon, sorun yok | ffmpeg `lut3d` zaten okuyor; biz yalnız dosya seçimi ve doğrulama yaparız | küçük | 1 (AA-01 ile tek iş) |
| ST-03 | **FCPXML okuma** (bugün yalnız yazıyoruz) | spesifikasyon | `Core/FcpXml.cs` yazıcısının tersi; FilmCraft ve Resolve'dan gelen kurguyu alır | orta | 2 |
| ST-04 | OpenColorIO | BSD-3, uyumlu | ffmpeg yapımızda `ocio` süzgeci yok; doğrudan kullanmak yerel C++ kitaplığı ve P/Invoke ister. Önerim: şimdilik **alma**, ACES ihtiyacını .cube LUT (OCIO'dan pişirilmiş) karşılar | büyük | 3 |
| ST-05 | frei0r eklentileri | GPL-2.0 (only?) — kod birleştirme uygun değil | ffmpeg `frei0r` süzgeci var ama eklentiler pakette yok; dağıtmak ayrı lisans incelemesi ister. Önerim: **alma**, ihtiyaç duyulan efektlerin yerleşik ffmpeg karşılığı var | orta | 3 |
| ST-06 | MLT motoru (bileşim/önizleme için) | LGPL-2.1+, uyumlu | DZB-17'de "ayrı bileşim motoru" seçeneği olarak geçiyor; C API, P/Invoke gerekir, libmpv'nin yanına ikinci motor demek. Önizleme kararı sahibin | büyük | sahip kararı |
| ST-07 | OpenFX eklenti barındırma | BSD-3 başlıklar | Shotcut 26.6'da deneysel; bizim için çok büyük | büyük | yok |

### Önerilen Sıra

1. **AA-01 + ST-02 LUT** — küçük, renk paneline tek satır, en görünür kazanç.
2. **AA-02 otomatik müzik kısma** — A2 izi var, tek süzgeç.
3. **ST-01 OpenTimelineIO dışa/içe** — kurguyu Kdenlive/Resolve/FilmCraft'a taşır, kod bağımlılığı yok.
4. **AA-03 bulanıklık ve bölge gizleme** — DZB-P13'ün açık yarısı, plan hazır.
5. **AA-05 stabilizasyon** ve **AA-06 skoplar** — orta, bağımsız.
6. **AA-04 ikinci video izi + kroma** ve **AA-09 anahtar kare** — büyük; önizleme yolu kararı (DZB-P8) beklenir.

Model indirme isteyen AA-08 ve AA-10 (`arnndn`) DZB-P16'daki karar demetine girer.

## Açık Sorular

- Linux ve macOS'ta dağıttığımız ffmpeg yapısında `lut3d`, `vidstab*`, `whisper` var mı? Yalnız Windows Gyan yapısı ölçüldü.
- LosslessCut, frei0r ve Natron lisanslarının "only" mi "or later" mı olduğu dosyadan okunmadı.
- FilmCraft'ın gerçek olgunluğu yalnız kendi beyanı; bağımsız ölçüm yok.

## Kaynaklar

- Kdenlive 26.08.0: https://kdenlive.org/news/releases/26.08.0/
- Kdenlive sürüm listesi: https://kdenlive.org/news/releases/
- Kdenlive 26.04.0: https://planet.kde.org/kdenlive-2026-04-27-kdenlive-26-04-0-released/
- Kdenlive 26.04.3: https://planet.kde.org/kdenlive-2026-07-06-kdenlive-26-04-3-released/
- Kdenlive (Wikipedia, lisans ve son sürüm): https://en.wikipedia.org/wiki/Kdenlive
- Shotcut 26.9: https://www.phoronix.com/news/Shotcut-26.9
- Shotcut 26.7: https://phoronix.com/news/Shotcut-26.7-Released
- Shotcut 26.6: https://ubuntuhandbook.org/index.php/2026/06/shotcut-26-6-released-openfx-vst2-filters-support-hdr-preview/
- Shotcut 26.4: https://ubuntuhandbook.org/index.php/2026/05/shotcut-26-4-released-with-gpu-acceleration-for-speech-to-text/
- OpenShot blog (4.0.1, 4.0, 3.5): https://www.openshot.org/blog/
- OpenShot 4.0: https://alternativeto.net/news/2026/9/openshot-4-0-adds-color-and-recording-views-local-ai-powered-masks-and-10-new-effects/
- OpenShot 4.0: https://linuxiac.com/openshot-4-0-video-editor-released-with-built-in-screen-recording/
- OpenShot 3.4: https://www.phoronix.com/news/OpenShot-3.4-Released
- Olive: https://github.com/olive-editor/olive
- Blender 5.0: https://itsfoss.com/news/blender-5-0-release/
- Blender 5.0 VSE: https://heise.de/-11084290
- Blender 5.1: https://www.blendernation.com/blender-5-1-released
- Blender 5.2 (tek ticari kaynak): https://tryuncle.com/learn/davinci-resolve/davinci-resolve-vs-blender-video-sequencer-which-to-use.md
- ArtCraft paketi: https://www.cined.com/artcraft-releases-free-open-source-adobe-alternatives-filmcraft-for-premiere-pro-effectcraft-for-after-effects-written-in-rust-with-ai/
- ArtCraft depoları: https://github.com/storytold
- FilmCraft: https://github.com/storytold/filmcraft
- OpenCut: https://mer.vin/2026/07/opencut-explained-open-source-capcut-alternative-classic-editor-vs-ground-up-rewrite/
- OpenCut listeleri: https://openalternative.co/alternatives/premiere-pro , https://open-source.store/alternatives/premiere-pro
- LosslessCut: https://github.com/mifi/lossless-cut , https://en.wikipedia.org/wiki/LosslessCut
- Natron sürümleri: https://github.com/NatronGitHub/Natron/releases
- Friction sürümleri: https://github.com/friction2d/friction/releases
- Friction (genel): https://www.makeuseof.com/open-source-adobe-after-effects-alternative-getting-seriously-good/
- Glaxnimate 0.6.0: https://glaxnimate.org/news/releases/0.6.0/
- Audacity 4.0: https://phoronix.com/news/Audacity-4.0-Released , https://www.audacityteam.org/changelog/
- Ardour 9.0: https://phoronix.com/news/ardour-9.0-daw , https://discourse.ardour.org/c/blog
- HandBrake 1.10.1: https://handbrake.fr/news.php?article=60
- MLT: https://en.wikipedia.org/wiki/Media_Lovin%27_Toolkit , https://mltframework.org/feed.xml
- OpenTimelineIO: https://github.com/AcademySoftwareFoundation/OpenTimelineIO
- OpenTimelineIO 0.18.1 paketi: https://www.archlinux.de/packages/x86_64/opentimelineio
- OpenColorIO: https://github.com/AcademySoftwareFoundation/OpenColorIO
- frei0r: https://github.com/dyne/frei0r
- Bu repoda: `LICENSE`, `docs/later.md` (DZB-P7, P8, P10, P12–P16), `docs/piyasa/2026-10-13/duzenleyici-kurgu-dogrulanmis.md`
