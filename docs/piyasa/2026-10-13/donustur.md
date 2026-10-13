# Dönüştür Sekmesi Ve Gizli Gelişmiş Sekmesi — Pazar Notları

13 Ekim 2026. Kod okuması `origin/main` 9de48d8b. Eksik tablosu yandaki `donustur-eksikler.md` dosyasında (52 satır, önek `DON`).

**Yöntem.** 28 ürün tarandı. İlk 11'ini kendim okudum (resmi sayfa, sürüm notu, `gh api`). Kalan 17'sini iki alt ajan okudu; onların satırları "alt ajan" diye işaretli ve iddia sayılır. Rakip sayfaları bir özetleyiciden geçti, birebir alıntı değildir.

**Kısa sözlük.** *Kap*: videoyu, sesi ve altyazıyı tek dosyada tutan zarf (MP4, MKV). *Kodek*: görüntüyü ya da sesi sıkıştıran yöntem (H.264, AAC). *Remux*: yeniden kodlamadan yalnız kabı değiştirmek. *CRF*: sabit kalite ayarı. *NVENC, QSV, AMF*: sırasıyla Nvidia, Intel ve AMD ekran kartlarının kodlayıcıları. *VVC (H.266)*: HEVC'nin ardılı yeni kodek. *FFV1*: kayıpsız, açık arşiv kodeki. *CineForm*: GoPro kökenli kurgu ara kodeki. *HAP*: sahne yazılımlarının hızlı çözdüğü kodek. *AC3, E-AC3*: Dolby'nin çok kanallı ses biçimleri. *TrueHD*: Dolby'nin kayıpsız ev sineması sesi. *EBU R128*: ses yüksekliğini LUFS birimiyle ölçüp eşitleyen yayın ölçüsü. *Kanal indirgeme*: 5.1 sesi stereoya indirmek. *APNG*: hareketli PNG. *Ton eşleme*: HDR görüntüyü sıradan ekrana (SDR) uyarlamak. *Dither*: az renkle geçişleri yumuşatan noktalama. *LUT*: hazır renk dönüşüm tablosu. *CLI*: komut satırı.

## Envanter

### Dönüştür Sekmesi Bugün Ne Yapıyor

- **Akış.** Tek dosya seçilir (`src/VidShrink.App/MainWindow.axaml.cs:3233`), ayarlar plana okunur (`MainWindow.axaml.cs:4727`), plan denetlenir (`src/VidShrink.Core/ConversionArguments.cs:10`), ffmpeg komutu kurulur (`ConversionArguments.cs:70`) ve koşulur (`src/VidShrink.Ffmpeg/EncodeRunner.cs:679`).
- **Kaplar (13).** MP4, MKV, WebM, MOV, AVI, GIF, MP3, M4A, WAV, FLAC, WebP, AVIF, MXF (`src/VidShrink.App/MainWindow.axaml:1119`).
- **Video kodekleri.** H.264, H.265, VP9, AV1, kopya, beş ProRes ve beş DNxHR profili (`MainWindow.axaml:1149`, `src/VidShrink.Core/IntermediateCodecs.cs:31`). Listenin hepsi yazılım kodlayıcısı.
- **Kalite.** CRF ya da bit hızı (`MainWindow.axaml:1181`). Hız ön ayarı yazılıyor ama sabit varsayılanla; kullanıcı seçemiyor (`ConversionArguments.cs:142`).
- **Çözünürlük.** Kaynak, hazır yükseklikler ya da özel (`MainWindow.axaml:1220`); ölçekleme hep lanczos (`ConversionArguments.cs:232`).
- **Kare hızı.** Kaynak, 60, 30, 24 ya da özel (`MainWindow.axaml:1260`); `fps` süzgeci kare atar ya da çoğaltır (`ConversionArguments.cs:237`).
- **Ses.** AAC, Opus, MP3, PCM, FLAC, kopya, sesi at (`MainWindow.axaml:1298`); bit hızı (`MainWindow.axaml:1322`).
- **Yalnız ses çıktısı.** MP3, M4A, WAV, FLAC (`src/VidShrink.Core/ConversionPlan.cs:32`). Ses her zaman yeniden kodlanır (`MainWindow.axaml.cs:4782`).
- **Süre kırpma.** Başlangıç ve bitiş (`MainWindow.axaml:1338`).
- **Meta veriyi sil kutusu** (`MainWindow.axaml:1357`, `ConversionArguments.cs:196`). GIF kolunda meta argümanı yazılmaz (`docs/olcumler/meta-sil-donustur-duzenleyici.md:30`).
- **Remux.** Video ve ses "kopya" seçilince yeniden kodlama olmaz; hangi kodeğin hangi kaba kopyalanacağını iki tablo söyler (`ConversionArguments.cs:259`, `ConversionArguments.cs:270`).
- **Hareketli çıktı.** GIF iki adımlı paletle (`EncodeRunner.cs:691`, `ConversionArguments.cs:105`); hareketli WebP ve AVIF (`ConversionArguments.cs:165`).
- **HDR.** Koru ya da SDR'ye ton eşle; ama seçim Dönüştür'de değil, Küçült sekmesindeki düğmeden okunuyor (`MainWindow.axaml:803`, `MainWindow.axaml.cs:4777`).
- **Komut önizlemesi.** Üretilen ffmpeg komutu salt okunur kutuda görünür (`MainWindow.axaml:1371`).
- **Kodlayıcısı olmayan kap gizlenir** (`src/VidShrink.App/MainWindow.DonusturKap.cs:43`).
- **Hatırlama.** Son kap ve kodek sıra numarasıyla saklanır (`MainWindow.axaml.cs:1412`); adlı ön ayar yok.
- **K12 ara kodekleri.** On profil ölçüldü (`docs/olcumler/k12-ara-kodekler.md`); ekranda görülmedi (`.claude/acik.md:111`).

### Görevde Adı Geçen Kodeklerin Koddaki Durumu

| Kodek | Dönüştür'de Durum | Kanıt |
|---|---|---|
| VVC (H.266) | yok | arama: vvc, h266, libvvenc; tek eşleşme ilgisiz (`src/VidShrink.Core/HdrResolver.cs:56`) |
| CineForm | yok | arama: cfhd, cineform; eşleşme yok |
| FFV1 | yok | arama: ffv1; eşleşme yok |
| HAP | yok | arama: hap; eşleşme yok |
| AV1 donanım | yarım | motor adları tanıyor (`ConversionArguments.cs:289`), kodek listesinde yok (`MainWindow.axaml:1149`) |
| Opus | var | ses listesi (`MainWindow.axaml:1298`); yalnız ses kabı olarak yok |
| FLAC | var | ses listesi ve .flac kabı (`ConversionPlan.cs:32`) |
| E-AC3, AC3 | yarım | kodlama yok; MKV'ye kopya serbest (`ConversionArguments.cs:279`), MP4'e kopya reddediliyor (`ConversionArguments.cs:272`) |
| TrueHD geçişi | var, ölçülmedi | MKV'ye her ses kopyalanabilir (`ConversionArguments.cs:279`); `docs/olcumler/` altında ölçümü yok |

### Dönüştür'de Olmayanlar (Kod Araması)

- `ConversionArguments.cs` içinde `-map`, `-sn`, `-c:s`, `-ac`, `-pass`, `fps_mode`, `-map_chapters` geçmiyor. İz seçimini ffmpeg'in varsayılanı yapıyor: tek video, tek ses.
- Planda iz, altyazı, ses yüksekliği, kanal, örnekleme hızı, hız ön ayarı, ek argüman ve süzgeç alanı yok (`ConversionPlan.cs:31`).
- Harici ses: `ExternalAudio`, `harici ses`, `AddAudioFile` aramaları boş. Altyazı çıkarma: `ExtractSubtitle`, `AltyaziCikar` boş; en yakın kod `src/VidShrink.Core/StreamMapping.cs:274`.
- Görüntü dizisi: `image2` yalnız kapak çıkarmada (`EncodeRunner.cs:194`), `image2pipe` küçük resimlerde (`src/VidShrink.Core/Editing/ThumbnailQueue.cs:254`). APNG: `apng`, `animated png` boş.
- Yapay zekâ ile büyütme: `realesrgan`, `upscal`, `büyüt` aramalarında ilgili tek satır yukarı ölçeği yasaklayan denetim (`src/VidShrink.Core/PlanParser.cs:93`).
- Gömme altyazı biçemi: `force_style` boş. Ara kare: `minterpolate` yalnız kaydedicide (`src/VidShrink.Core/RecorderArguments.cs`).
- Toplu iş, komut satırı ve sağ tık: Dönüştür tek dosya alır (`MainWindow.axaml.cs:3233`); CLI'da dönüştürme komutu yok (`src/VidShrink.Cli/CliRequest.cs:395`); kabuk menüsünde yalnız Aç ve Küçült (`MainWindow.axaml:1655`, `MainWindow.axaml:1656`).

### Küçült Yolunda Olup Dönüştür'e Bağlanmamış Olanlar

Eksiklerin çoğu "yok" değil "yarım": motor Küçült için yazılmış, Dönüştür'e bağlanmamış.

- İz eşleme (`StreamMapping.cs:255`), altyazı kopyası (`StreamMapping.cs:274`), bölümler (`StreamMapping.cs:302`), iz bayrakları (`StreamMapping.cs:182`).
- MP4'e AC3, E-AC3 ve Opus kopyası (`StreamMapping.cs:430`); Dönüştür'ün tablosu daha dar (`ConversionArguments.cs:272`).
- Süzgeç zinciri: tarama çözme, gürültü azaltma, keskinleştirme, döndürme, kırpma (`src/VidShrink.Core/VideoFilterChain.cs:63`); altyazı gömme (`VideoFilterChain.cs:83`); kare hızı kipi (`VideoFilterChain.cs:379`).
- Ses yüksekliği normalizasyonu (`src/VidShrink.App/MainWindow.Izler.cs:88`, `CliRequest.cs:119`); kanal seçimi (`MainWindow.axaml.cs:1609`); hız ön ayarı seçimi (`MainWindow.axaml:983`).
- Donanım kodlayıcıları ve AC3, E-AC3 kodlama (`src/VidShrink.Core/PlanParser.cs:13`, `PlanParser.cs:14`).
- Kullanıcı ön ayarı ve dışa aktarma (`src/VidShrink.App/MainWindow.OnAyar.cs:228`, `MainWindow.OnAyar.cs:260`).
- 18 hazır hedef, boyut odaklı (`src/VidShrink.Core/Presets/platformlar.json:5`).
- İzlenen klasör, yalnız Küçült kuyruğu için (`src/VidShrink.App/QueueWatch.cs:13`).

### Gizli Gelişmiş Sekmesi

- Sekme arayüzde kapalı doğar: `TabAdvanced`, `IsVisible="False"` (`MainWindow.axaml:1416`).
- İçinde üç panel var: Küçült'ün ffmpeg komut satırı (`MainWindow.axaml:1420`, `MainWindow.axaml:1431`), yapay zekâ planı yapıştırma kutusu (`MainWindow.axaml:1435`, `MainWindow.axaml:1466`), başarım denetimi (`MainWindow.axaml:1472`).
- Açılışı gizli: Hakkında'daki sistem durumu satırına 2 saniye içinde 7 dokunuş (`src/VidShrink.Core/DeveloperUnlock.cs:11`, `DeveloperUnlock.cs:13`, `MainWindow.axaml.cs:3046`). Kapatma düğmesi `MainWindow.axaml:1425`, yöntemi `MainWindow.axaml.cs:3054`.
- Gizlenme tarihi 13 Eylül 2026, commit `bf3b2156`.
- Ayarlar'daki "Gelişmiş varsayılan açık" kutusu bu sekmeyi değil, Küçült'ün gelişmiş panelini açar (`MainWindow.axaml:1561`).
- **Hüküm.** Bu sekme bir uzman dönüştürme yüzü değil, geliştirici teşhis sekmesi. Dönüştür'le bağı yok: Dönüştür kendi komutunu kendi kutusunda gösterir (`MainWindow.axaml:1371`). Rakiplerdeki "uzman yüz" (özel argüman, ön ayar dosyası) bizde hiçbir sekmede Dönüştür için yok.

### Önceki Taramanın İlk Dalgasından Bu Alana Düşenler

- #5 hareketli WebP ve AVIF: **yapıldı** (commit `e0356eff`, `ConversionArguments.cs:165`, ölçüm `docs/olcumler/hareketli-webp-avif.md`).
- #12 meta veriyi sil: **yapıldı** (commit `fea2a34a`, `MainWindow.axaml:1357`).
- #11 iş bitince bildirim: **yapıldı** (`MainWindow.axaml.cs:4947`).
- #20 çoklu seçim: Dönüştür için **yapılmadı**; çoklu bırakma Küçült kuyruğuna gidiyor (`MainWindow.axaml.cs:3308`). Tabloda DON-06.

## Rakipler

"Okuyan" sütunu: *ben* = sayfayı kendim açtım; *alt ajan* = alt ajan raporu, iddia.

| # | Ad | Sürüm | Tarih | Tür | Okuyan | Kaynak |
|---|---|---|---|---|---|---|
| 1 | HandBrake | 1.11.2 | 7 Haz 2026 | açık kaynak, masaüstü | ben | [1] |
| 2 | Shutter Encoder | 20.4 | 28 Eyl 2026 | ücretsiz (bağışla), masaüstü | ben | [11] |
| 3 | XMedia Recode | 3.6.4.1 | 26 Eyl 2026 | ücretsiz, Windows | ben | [12] |
| 4 | FFmpeg Batch AV Converter | 3.2.9 | 27 May 2026 | açık kaynak, Windows | ben | [14] |
| 5 | VidCoder | 12.23 (beta 13.12) | 19 Tem 2026 (beta 25 Eyl 2026) | açık kaynak, Windows | ben | [18] |
| 6 | Format Factory | 5.22.0.0 | 11 Mar 2026 | ücretsiz, Windows | ben | [19] |
| 7 | File Converter | 2.2 | 13 Şub 2026 | açık kaynak, Windows sağ tık eklentisi | ben | [15] |
| 8 | FastFlix | 6.2.1 | 21 Mar 2026 | açık kaynak, masaüstü | ben | [20] |
| 9 | StaxRip | 2.52.6 | 27 Eyl 2026 | açık kaynak, Windows | ben | [16] |
| 10 | MediaCoder | doğrulanmadı | doğrulanmadı | ücretsiz, Windows | ben | [17] |
| 11 | Subtitle Edit | 5.2.0 | 10 Eyl 2026 | açık kaynak, altyazı aracı | ben | [21] |
| 12 | Permute | 4.0.11 | 11 Eyl 2026 | ücretli (14,99 USD), macOS | alt ajan | [22] |
| 13 | Adapter | Mac 2.1.6, Windows 2.1.2 | doğrulanmadı | ücretsiz, masaüstü | alt ajan | [24] |
| 14 | Any Video Converter | 9.2.5 | 10 Ağu 2026 | ücretsiz ve ücretli katman | alt ajan | [25] |
| 15 | Wondershare UniConverter | 17.6.0 | 31 Ağu 2026 | ücretli, masaüstü | alt ajan | [26] |
| 16 | Movavi Video Converter | 24.5.1 | 1 Ara 2025 | ücretli (74,95 USD) | alt ajan | [29] |
| 17 | VideoProc Converter AI | 8.12 | "20 Eylül", yıl sayfada yok | ücretli, masaüstü | alt ajan | [30] |
| 18 | CloudConvert | sürekli hizmet | — | web, ücretli API | alt ajan | [31] |
| 19 | Convertio | sürekli hizmet | — | web | alt ajan | [32] |
| 20 | FreeConvert | sürekli hizmet | — | web | alt ajan | [33] |
| 21 | MKVToolNix | 102.0 | 14 Eyl 2026 | açık kaynak, kap aracı | alt ajan | [58] |
| 22 | tsMuxer | 2.7.0 | 21 Nis 2025 | açık kaynak, artık geliştirilmiyor | alt ajan | [36] |
| 23 | LosslessCut | 3.69.0 | 4 Haz 2026 | açık kaynak, kayıpsız kesim | alt ajan | [37] |
| 24 | fre:ac | 1.1.7 | 5 Mar 2023 | açık kaynak, ses dönüştürücü | alt ajan | [39] |
| 25 | foobar2000 | 2.26.1 | 9 Eki 2026 | ücretsiz, ses oynatıcı ve dönüştürücü | alt ajan | [40] |
| 26 | gifski | 1.34.0 | 13 Tem 2025 | açık kaynak, GIF kodlayıcı | alt ajan | [41] |
| 27 | ScreenToGif | 2.43.2 | 28 Tem 2026 | açık kaynak, Windows | alt ajan | [42] |
| 28 | ezgif | sürekli hizmet | — | web | alt ajan | [43] |

Listede olmayıp eklediklerim: FastFlix, StaxRip, LosslessCut. İşletim sisteminin kendi araçları taranmadı (Doğrulanamayanlar).

## Özellik Matrisi

"—" o sayfada görülmedi demektir, "rakipte yok" demek değildir. "Biz" sütunu yalnız Dönüştür sekmesini anlatır.

| Özellik | Biz (Dönüştür) | HandBrake | Shutter Encoder | XMedia Recode | FFmpeg Batch AV Converter | Diğerleri |
|---|---|---|---|---|---|---|
| Kap genişliği | 13 kap | MP4, MKV, WebM, MOV [1][5] | geniş, kodeğe göre [9] | MKV, MP4, WebM ve fazlası [12] | ffmpeg ne yazarsa [14] | UniConverter "1000+" [26], VideoProc "420+" [30], Movavi "180+" [29], Permute "100+" [22] |
| Donanım kodlayıcı, AV1 dahil | listede yok | var [1][2] | var [9][10] | var [12] | var [14] | VidCoder [18], FastFlix [20], MediaCoder [17] |
| VVC çıktısı | yok | yalnız QSV çözücü [3] | var [9] | — | — | FastFlix [20] |
| FFV1 | yok | var [5] | var [9] | — | — | UniConverter [27] |
| CineForm, HAP | yok | — | var [9][11] | — | — | UniConverter CineForm [28] |
| ProRes, DNxHR | var | var [5] | var [9] | — | ProRes [14] | UniConverter [27], Permute [22] |
| Ses izi seçimi, çok ses | yok | var [8] | var [9] | — | var [14] | MKVToolNix [35], LosslessCut [37], Permute [23], VidCoder [18] |
| Altyazı izi taşıma | seçenek yok | var [1][6] | var [9] | var [12] | var [14] | VideoProc [30], MKVToolNix [35] |
| Altyazı gömme | yok | var [6][8] | var [9] | — | var [14] | Subtitle Edit [21], Adapter [24], VideoProc [30] |
| Harici altyazı ekleme | yok | var [6][8] | var [9] | var [12] | var [14] | VideoProc [30], MKVToolNix [35] |
| Altyazı çıkarma | yok | — | var [9] | var [12] | toplu ayırma (demux) [14] | LosslessCut [37], VideoProc [30], Permute [22] |
| Remux | var, kopya seçimiyle | ses geçişi [1] | var [9] | — | toplu mux ve demux [14] | MKVToolNix [35], LosslessCut [37], Permute [22], VideoProc [30] |
| Harici ses ekleme | yok | 62 oyluk açık istek [45] | var [9][11] | — | var [14] | MKVToolNix [35], LosslessCut [37], Adapter [24] |
| Meta veri | yalnız silme | koru ya da sil [8] | — | — | — | Permute [23], LosslessCut [37], MKVToolNix [34], UniConverter [26] |
| Bölümler | seçenek yok | var [1][8] | — | — | var [14] | MKVToolNix [34], LosslessCut [37], UniConverter [26] |
| Yalnız ses çıktısı | MP3, M4A, WAV, FLAC | — | WAV, AIFF, FLAC, MP3, AAC, AC3, Opus, Vorbis ve fazlası [9] | AAC, AC3, FLAC [12] | MP3, AAC, AC3, FLAC, WAV, Opus, Vorbis [14] | File Converter [15], fre:ac [39], Any Video Converter [25] |
| Ses yüksekliği normalizasyonu | yok | kazanç ve karışım ayarı [8] | var, LUFS hedefli [9] | ses düzeyi ayarı [12] | — | UniConverter [27], foobar2000 [40] |
| Kanal indirgeme | yok | var [8] | — | — | — | — |
| Örnekleme hızı | yalnız MXF'te sabit | var [8] | var [9] | var [12] | — | — |
| GIF | var | — | var [9] | — | — | File Converter [15], Adapter [24], VideoProc [30], gifski [41], ezgif [44] |
| Hareketli WebP, AVIF | var | — | resim biçimi olarak [9] | — | — | ezgif [43], Permute [22] |
| APNG | yok | — | var [9] | — | — | ScreenToGif [42], ezgif [43], FreeConvert [33] |
| Görüntü dizisi | yok | 47 oyluk açık istek [46] | girdi ve çıktı [9] | — | resimden video [14] | Adapter [24], UniConverter [27], Permute [23] |
| HDR'den SDR'ye | var, düğme Küçült'te | var [8] | var [9][10] | — | — | FastFlix HDR10 ve HDR10+ [20] |
| Kare hızı dönüşümü | kare atma ya da çoğaltma | sabit, değişken, tepe [8] | atma, harmanlama, ara kare [9] | — | — | VideoProc [30], UniConverter [26] |
| Kırpma, döndürme, tarama çözme | yok | var [1][8] | var [9] | döndürme, gürültü azaltma [12] | — | Format Factory [19] |
| Kodlayıcı ince ayarı, iki geçiş | yok | var [8] | var [9] | — | iki geçiş [14] | VidCoder [18] |
| Özel ffmpeg argümanı | yok | kodlayıcı seçenekleri [8] | var [9] | — | var [14] | Permute [23], Subtitle Edit [21] |
| Komut önizlemesi | var, salt okunur | — | — | — | kodlamadan önce deneme [14] | — |
| Ön ayar kaydetme | yok | var [7] | var [9][11] | — | — | File Converter [15], Permute [23], Any Video Converter [25] |
| Ön ayar paylaşma | yok | var [7][8] | var [11] | — | — | Permute [23], MKVToolNix [34] |
| Cihaz ve platform ön ayarı | yok | 88 ön ayar [2] | platform paketleri [11] | 29 profil girdisi [12] | — | UniConverter [28], VideoProc [30], Movavi [29], FreeConvert [33] |
| Toplu dönüştürme | yok | var [1] | var [9] | var [12] | var [14] | VidCoder [18], Movavi [29], Convertio [32] |
| Komut satırı | Dönüştür için yok | var [8] | — | — | — | LosslessCut [37], MKVToolNix [35], gifski [41], fre:ac [39] |
| Yapay zekâ ile büyütme | yok | — | yalnız fotoğraf [10] | — | — | UniConverter [26], Movavi [29], VideoProc [30], Any Video Converter [25] |

**Sekiz soruya kısa yanıt.**

1. *Kap ve kodek.* Kap sayımız (13) HandBrake'in dört kabından geniş. Açık olan yer donanım kodlayıcıları, FFV1 ve AC3, E-AC3 kodlama. VVC'yi yalnız Shutter Encoder ve FastFlix yazıyor.
2. *Cihaz ön ayarı.* HandBrake 88 güncel ön ayar tutuyor ve her büyük sürümde ekliyor. XMedia Recode 29 profil girdisi, Shutter Encoder platform paketleri veriyor. Bizde Dönüştür için sıfır; karar kapalı ("85 ön ayar bilerek yapılmaz").
3. *Yeniden kodlamadan işler.* Remux var. İz ekleme, çıkarma, sıralama ve meta veri düzenleme yok.
4. *Altyazı.* Dönüştür'de dört işin hiçbiri yok. Gömme ve taşıma Küçült'te hazır.
5. *Ses.* Yalnız ses çıktısı dört biçimle var; kopyayla çıkarma, yükseklik normalizasyonu ve kanal indirgeme yok.
6. *Görüntü.* GIF, hareketli WebP ve AVIF var. APNG, görüntü dizisi ve tek kare yok.
7. *Uzman yüz.* Komut önizlemesi var ama salt okunur. Özel argüman ve ön ayar dosyası yok. Gizli Gelişmiş sekmesi bu işi görmüyor.
8. *Son iki yıl.* Aşağıdaki bölümde.

## Kullanıcı Talepleri

Oy sayısı ilgili kaydın başparmak tepkisidir; 13 Ekim 2026'da `gh api` ile okundu. Tabloda "oyla kanıtlı" eşiğini 40 oy aldım.

| İstek | Oy | Ürün | Kaynak | Tabloda |
|---|---|---|---|---|
| Akıllı kesim (yalnız kesim yerini yeniden kodlama) | 119 | LosslessCut | [65] | düzenleyici kolu |
| Harici ses izlerini içe alma | 62 | HandBrake | [45] | DON-07 |
| Yeniden kodlamalı (kayıplı) işlemler | 62 | LosslessCut | [56] | bizde var: Dönüştür |
| Görüntü dizisini girdi olarak açma | 47 | HandBrake | [46] | DON-08 |
| Gömme altyazıda yazı tipi, boyut, arka plan | 40 | HandBrake | [47] | DON-09 |
| AVIF resim çıktısı | 24 | File Converter | [53] | kapsam dışı (durağan resim) |
| Toplu iş akışı | 20 | LosslessCut | [68] | DON-06 |
| Meta veri geçişi | 17 | HandBrake | [48] | DON-29 |
| İzleri kaydırma (ses gecikmesi) | 16 | LosslessCut | [62] | tabloda yok, aşağıda not |
| Aynı anda birden çok kodlama | 15 | HandBrake | [66] | komşu kol; bizde K6 yapıldı |
| Ses izini adlandırma | 12 | HandBrake | [49] | DON-31 |
| LUT desteği | 10 | HandBrake | [51] | DON-50, K8 rafta |
| Opus çıktısı | 10 | File Converter | [52] | DON-18 |
| Dil etiketleri | 9 | HandBrake | [50] | DON-31 |
| Hareketli AVIF | 9 | ScreenToGif | [57] | bizde var |
| Ses ofseti | 8 | HandBrake | [61] | tabloda yok, aşağıda not |
| Harici altyazıyı kendiliğinden içe alma | 7 | HandBrake | [63] | DON-10 |
| GPU ile kodlama | 7 | File Converter | [64] | DON-04 |
| VTT altyazıyı gömme | 6 | HandBrake | [67] | DON-03 |
| M4A çıktısı | 5 | File Converter | [69] | bizde var |
| 30'dan 24'e geçişte yalnız kare atılıyor | 2 | Shutter Encoder | [54] | DON-42 |
| GIF'e boyut hedefi | 2 | Shutter Encoder | [55] | komşu kol |

Ses kaydırma (16 ve 8 oy) tabloya girmedi: hangi rakibin bunu belgelediğini okuduğum sayfalarda görmedim. Kullanıcı isteği olarak burada duruyor.

DON-09 oyla öncelik 1'e yaklaşıyor (40 oy) ama rakip iddiasını tek kaynakta gördüm [21]; kural iki kaynak istediği için 2'de bıraktım.

## Son İki Yılın Yenilikleri

Aralık: Ekim 2024 – Ekim 2026.

- **HandBrake 1.9.0** (1 Ara 2024) [3]: QSV ile VVC çözme, kayıpsız VP9, ALAC kodlayıcı, altyazı gömmede iyileştirme, kuyrukta "sonra başlat".
- **HandBrake 1.10.0** (9 Ağu 2025) [4]: "Social 10MB" ön ayarları, meta veri geçişinde iyileştirme, Dolby Vision ve HDR10+ geçişini kapatma, kodlayıcı renk aralığı seçeneği, SRT'nin MKV'ye çevrilmeden geçmesi, kuyruk bitince özel eylem.
- **HandBrake 1.11.0** (8 Mar 2026) [5]: MOV kabı, ProRes ve DNxHR, FFV1 ve arşiv ön ayarları, AMD'de 10 bit AV1, PCM ses, özel kanal sırası.
- **Shutter Encoder 18.5 – 20.4** (16 Eyl 2024 – 28 Eyl 2026) [10]: Vulkan ve Media Foundation kodlayıcıları, VideoToolbox, QSV ve NVENC AV1; JPEG XL; kuyrukta paralel koşu; FFV1 donanım hızlandırma; ses ekolayzırı; Dolby Digital Plus; ses dökümü (Eki 2025), çeviri, renklendirme, ses ayırma, fotoğraf büyütme, arka plan silme, yüz bulanıklaştırma; elle kodlayıcı parametresi (19.9); çoklu kesim ve iyileşen GPU ton eşleme (20.3); tepe normalizasyonu ve yinelenen kare silme (20.4).
- **Permute 4.0** (26 May 2026) [23]: iz sıralama, iz adı, dil ve bayrak düzenleme; yeni meta veri düzenleyici; ham ffmpeg ön ayarı; ön ayar dosyası içe alma; 4.0.8'de ses dökümü, 4.0.9'da özel kare hızı.
- **Any Video Converter 9.2.x** (2026) [25]: DVD ve CD içe alma, "kaynakla aynı" seçeneği, renk uzayı dahil gelişmiş seçenekler, yapay zekâ ile iyileştirme modelleri.
- **Wondershare UniConverter 16.3.5 – 17.6** [27]: toplu GIF ve görüntü dizisi, ses yüksekliği normalizasyonu (düzenleyicide, Windows), FFV1, yapay zekâ ile büyütme ve ara kare.
- **XMedia Recode** [13]: son 12 sürüm çoğunlukla kitaplık güncellemesi; 2025'te yalnız AV1 ve AMD kodlayıcı seçenekleri eklendi.
- **foobar2000 2.26.1** (9 Eki 2026) [40]: dönüştürücüde iş sonrası ReplayGain taraması.

**Pazar şu an ne ekliyor.**

1. Kurgu ve arşiv kodekleri ana akım dönüştürücüye indi: HandBrake 1.11 ProRes, DNxHR ve FFV1 aldı. Bizde K12 ile ilk ikisi var, FFV1 yok (DON-21).
2. Donanım AV1 her üreticide ve her işletim sisteminde açıldı. Bizde motor hazır, Dönüştür listesine bağlı değil (DON-04).
3. Yapay zekâ işleri ücretli dönüştürücülerin ana satış başlığı oldu; ücretsiz tarafta Shutter Encoder bir yılda yedi tane ekledi (DON-32, DON-46, DON-52).
4. İz ve meta veri düzenleme dönüştürücünün içine girdi (Permute 4.0, HandBrake 1.10) (DON-01, DON-29, DON-31).
5. Kuyruk kolaylıkları ve küçük boyut ön ayarları eklendi; bunlar komşu kolun işi.

**Kapalı kararlara dokunan değişiklikler** (yeniden açmak sahibin işidir).

- K1 URL'den indirme: Shutter Encoder 18.8 indirmeye çerez desteği ekledi; FFmpeg Batch AV Converter'da aynı istek açık [10][14].
- K5 otomatik altyazı: Shutter Encoder Ekim 2025'te, Permute 4.0.8'de ses dökümü ekledi [10][23].
- K8 LUT ve filigran: Shutter Encoder'da ikisi de duruyor ve HDR'den SDR'ye dönüşümü LUT ile yapıyor [9]; HandBrake'te LUT isteği 10 oyda [51].
- DVD ve Blu-ray: Any Video Converter 9.2.0 DVD içe almayı yeni ekledi [25].
- 85 ön ayar: HandBrake'in güncel ön ayarı 88; 1.11'de kurgu ve arşiv grupları eklendi [2][5].

## Doğrulanamayanlar

- **Dönüştür çıktısında altyazıya ne oluyor.** Kodda `-map` ve `-c:s` yok; sonucu ffmpeg'in varsayılanı belirliyor. `docs/olcumler/` altında bunun ölçümü yok. DON-02 bu yüzden "seçenek yok" diyor, "altyazı düşüyor" demiyor.
- **Paketteki ffmpeg'de libvvenc ve hap kodlayıcısı var mı.** Program çalıştırmak yasaktı; ölçülmedi. DON-33 ve DON-35 bu yüzden `sahip`.
- **TrueHD'nin MKV'ye kopyası.** Kod izin veriyor (`ConversionArguments.cs:279`), ölçümü yok.
- **K12 ara kodekleri ekranda görülmedi** (`.claude/acik.md:111`); bu tur da bakılmadı.
- **85 ön ayar kararının kapsamı.** Karar metni "85 birebir" diyor (`docs/handbrake/durum-2026-09-23.md:127`, `docs/handbrake/durum-2026-09-23.md:138`). Dönüştür'e küçük bir hazır hedef takımı aynı karara girer mi, bilemedim; DON-47'yi kapalı yazdım.
- **Kanal indirgeme** yalnız HandBrake'te belgeli görüldü [8]; öbür rakiplerin sayfasında aramadım. DON-39 bu yüzden öncelik 3.
- **XMedia Recode** ana sayfasında VVC, altyazı gömme, bölüm düzenleme, HDR, ses yüksekliği ve komut satırı görülmedi.
- **MediaCoder** sürümü ve tarihi sayfada yok. **StaxRip**'in README dışındaki özellikleri, **MKVToolNix** lisansı ve **Adapter** sürüm tarihleri doğrulanmadı.
- **Shutter Encoder sürüm notunun** son 6959 karakteri okunamadı; bu en eski bölüm.
- **Ticari ve web dönüştürücüler** (Rakipler tablosunda 12–20: Permute, Adapter, Any Video Converter, UniConverter, Movavi, VideoProc, CloudConvert, Convertio, FreeConvert) alt ajanın okumasına dayanıyor. Bu dokuz üründe VVC, HAP, TrueHD, E-AC3, dönüştürme akışında EBU R128, HDR'den SDR'ye, donanım kodlayıcı adları, ön ayar sayısı ve dosya başına ayar doğrulanmadı.
- **Any Video Converter** ikinci kaynak sayfasının tam adresi alt ajan raporunda kısaltılmıştı; yalnız [25] yazıldı.
- **İşletim sisteminin kendi araçları** (Windows'ta Clipchamp, macOS'te Finder dönüştürmesi) ve mobil uygulamalar taranmadı.
- **Alfa kanallı (saydam) video çıktısı** rakip belgelerinde aranmadı.
- **Sayfada yönerge.** Alt ajan, Movavi sayfasında yapay zekâ yardımcılarına yönelik bir tanıtım notu bildirdi (başka bir ürünü ve indirim kodunu önermesini istiyor). Veri sayıldı, uygulanmadı, kod aktarılmadı.

## Kaynaklar

- [1] https://handbrake.fr/features.php — HandBrake özellik listesi: kap, kodek, ses geçişi, altyazı, süzgeç.
- [2] https://handbrake.fr/docs/en/latest/technical/official-presets.html — HandBrake'in 88 resmi ön ayarı ve grupları.
- [3] https://github.com/HandBrake/HandBrake/releases/tag/1.9.0 — 1.9.0 sürüm notu.
- [4] https://github.com/HandBrake/HandBrake/releases/tag/1.10.0 — 1.10.0 sürüm notu.
- [5] https://github.com/HandBrake/HandBrake/releases/tag/1.11.0 — 1.11.0 sürüm notu: MOV, ProRes, DNxHR, FFV1.
- [6] https://handbrake.fr/docs/en/latest/advanced/subtitles.html — altyazı: iz, gömme, harici SRT.
- [7] https://handbrake.fr/docs/en/latest/advanced/custom-presets.html — ön ayar kaydetme, içe ve dışa aktarma.
- [8] https://handbrake.fr/docs/en/latest/cli/command-line-reference.html — komut satırı: iz, kanal, süzgeç, kodlayıcı seçenekleri.
- [9] https://www.shutterencoder.com/documentation/ — Shutter Encoder işlev belgesi.
- [10] https://www.shutterencoder.com/changelog.txt — Shutter Encoder sürüm notları, 18.5 – 20.4.
- [11] https://www.shutterencoder.com/ — Shutter Encoder ana sayfa: sürüm, işlev listesi, ön ayar paylaşımı.
- [12] https://www.xmedia-recode.de/en/ — XMedia Recode: sürüm, kodek, profil, altyazı.
- [13] https://www.xmedia-recode.de/en/version.php — XMedia Recode sürüm geçmişi.
- [14] https://github.com/eibol/ffmpeg_batch — FFmpeg Batch AV Converter: özellik listesi ve sürüm.
- [15] https://github.com/Tichau/FileConverter — File Converter: çıktı biçimleri, ön ayar, sürüm.
- [16] https://github.com/staxrip/staxrip — StaxRip: sürüm ve tanım.
- [17] https://www.mediacoderhq.com/ — MediaCoder: kodek ve donanım listesi.
- [18] https://vidcoder.net/ — VidCoder: kodek, ses geçişi, toplu iş, iz seçimi.
- [19] http://www.pcfreetime.com/formatfactory/index.php?language=en — Format Factory: sürüm ve araç listesi.
- [20] https://github.com/cdgriffith/FastFlix — FastFlix: VVC, HDR10+, donanım kodlayıcıları.
- [21] https://github.com/SubtitleEdit/subtitleedit/blob/main/docs/features/burn-in.md — Subtitle Edit: altyazı gömme ve biçem seçenekleri.
- [22] https://software.charliemonroe.net/permute/ — Permute 4 ürün sayfası (alt ajan).
- [23] https://software.charliemonroe.net/trial/permute/v4/rnotes.html — Permute 4 sürüm notları (alt ajan).
- [24] https://www.macroplant.com/adapter/ — Adapter: ses ekleme, altyazı gömme, GIF, görüntü dizisi (alt ajan).
- [25] https://www.any-video-converter.com/products/for_video_free/ — Any Video Converter: biçimler, katmanlar, sürüm notları (alt ajan).
- [26] https://videoconverter.wondershare.com/ — UniConverter ürün sayfası (alt ajan).
- [27] https://videoconverter.wondershare.com/what-is-new.html — UniConverter sürüm notları (alt ajan).
- [28] https://videoconverter.wondershare.com/tech-spec.html — UniConverter biçim ve cihaz listesi (alt ajan).
- [29] https://www.movavi.com/movavi-video-converter.html — Movavi Video Converter (alt ajan).
- [30] https://www.videoproc.com/ — VideoProc Converter AI (alt ajan).
- [31] https://cloudconvert.com/video-converter — CloudConvert (alt ajan).
- [32] https://convertio.co/video-converter/ — Convertio: toplu iş, dosya başına biçim (alt ajan).
- [33] https://www.freeconvert.com/video-converter — FreeConvert: biçim, cihaz ve APNG sayfaları (alt ajan).
- [34] https://mkvtoolnix.download/doc/mkvtoolnix-gui.html — MKVToolNix arayüzü: bölüm düzenleyici, ayar dosyası (alt ajan).
- [35] https://mkvtoolnix.download/doc/mkvmerge.html — mkvmerge: iz seçimi, sıra, dil, bayrak, ek dosya (alt ajan).
- [36] https://github.com/justdan96/tsMuxer — tsMuxer: TS ve M2TS, desteklenen kodekler (alt ajan).
- [37] https://github.com/mifi/lossless-cut — LosslessCut: remux, iz ve meta veri düzenleme, komut satırı (alt ajan).
- [38] https://github.com/SubtitleEdit/subtitleedit/blob/main/docs/faq.md — Subtitle Edit: desteklenen altyazı biçimleri (alt ajan).
- [39] https://www.freac.org/ — fre:ac: ses biçimleri, komut satırı (alt ajan).
- [40] https://www.foobar2000.org/changelog — foobar2000 sürüm notları (alt ajan).
- [41] https://github.com/ImageOptim/gifski — gifski: seçenekler ve sürüm (alt ajan).
- [42] https://github.com/NickeManarin/ScreenToGif — ScreenToGif: çıktı biçimleri (alt ajan).
- [43] https://ezgif.com/converters — ezgif dönüştürücü listesi (alt ajan).
- [44] https://ezgif.com/video-to-gif — ezgif videodan GIF seçenekleri (alt ajan).
- [45] https://github.com/HandBrake/HandBrake/issues/871 — istek: harici ses izleri, 62 oy.
- [46] https://github.com/HandBrake/HandBrake/issues/2346 — istek: görüntü dizisi girdisi, 47 oy.
- [47] https://github.com/HandBrake/HandBrake/issues/146 — istek: altyazı yazı tipi, boyut, arka plan, 40 oy.
- [48] https://github.com/HandBrake/HandBrake/issues/3588 — istek: meta veri geçişi, 17 oy.
- [49] https://github.com/HandBrake/HandBrake/issues/2593 — istek: ses izini adlandırma, 12 oy.
- [50] https://github.com/HandBrake/HandBrake/issues/2784 — istek: dil etiketleri, 9 oy.
- [51] https://github.com/HandBrake/HandBrake/issues/1348 — istek: LUT, 10 oy.
- [52] https://github.com/Tichau/FileConverter/issues/111 — istek: Opus, 10 oy.
- [53] https://github.com/Tichau/FileConverter/issues/226 — istek: AVIF resim, 24 oy.
- [54] https://github.com/paulpacifico/shutter-encoder/issues/134 — şikâyet: kare hızı dönüşümü yalnız kare atıyor, 2 oy.
- [55] https://github.com/paulpacifico/shutter-encoder/issues/159 — istek: GIF boyut hedefi, 2 oy.
- [56] https://github.com/mifi/lossless-cut/issues/372 — istek: kayıplı işlemler, 62 oy (alt ajan).
- [57] https://github.com/NickeManarin/ScreenToGif/issues/1171 — istek: hareketli AVIF, 9 oy (alt ajan).
- [58] https://mkvtoolnix.download/ — MKVToolNix sürümü (alt ajan).
- [59] https://file-converter.io/ — File Converter ana sayfa: sağ tıkla dönüştürme.
- [60] https://gif.ski/ — gifski tanıtım sayfası (alt ajan).
- [61] https://github.com/HandBrake/HandBrake/issues/2500 — istek: ses ofseti, 8 oy.
- [62] https://github.com/mifi/lossless-cut/issues/216 — istek: izleri kaydırma, 16 oy (alt ajan).
- [63] https://github.com/HandBrake/HandBrake/issues/719 — istek: harici altyazıyı kendiliğinden alma, 7 oy.
- [64] https://github.com/Tichau/FileConverter/issues/392 — istek: GPU ile kodlama, 7 oy.
- [65] https://github.com/mifi/lossless-cut/issues/126 — istek: akıllı kesim, 119 oy (alt ajan).
- [66] https://github.com/HandBrake/HandBrake/issues/1445 — istek: aynı anda birden çok kodlama, 15 oy.
- [67] https://github.com/HandBrake/HandBrake/issues/4379 — istek: VTT altyazı gömme, 6 oy.
- [68] https://github.com/mifi/lossless-cut/issues/89 — istek: toplu iş akışı, 20 oy (alt ajan).
- [69] https://github.com/Tichau/FileConverter/issues/245 — istek: M4A, 5 oy.

## Komşu Kola Not

- **Kuyruk.** Dönüştür'ün kuyruğu yok; toplu dönüştürmenin (DON-06, DON-13) doğal evi Küçült'ün iş penceresi. O pencerede Dönüştür'e dair tek satır yok (`src/VidShrink.App/ShrinkJobWindow.axaml.cs`).
- **İzlenen klasör.** Var ama yalnız Küçült kuyruğu için (`src/VidShrink.App/QueueWatch.cs:13`); Shutter Encoder ve FFmpeg Batch AV Converter bunu dönüştürme için de veriyor [9][14].
- **Kuyruk bitince eylem.** HandBrake 1.10 özel eylem, Shutter Encoder kaynağı silme ve var olanı atlama ekledi [4][10].
- **Hedef boyut.** HandBrake "Social 10MB" ve 25 MB'lık web ön ayarları ekledi [2][4]; GIF'e boyut hedefi ezgif'te var, Shutter Encoder'da istek [43][55].
- **Önce ve sonra.** FFmpeg Batch AV Converter parametreleri kodlamadan önce denetiyor, HandBrake canlı önizleme, VidCoder önizleme klibi veriyor [1][14][18]; Dönüştür'de kısa deneme kodlaması yok.
- **Düzenleyici.** LosslessCut'ın en çok oylanan isteği akıllı kesim (119 oy) [65]; Shutter Encoder 20.3 çoklu kesim ekledi [10].
