# HandBrake Açık Analizi — Her Alanda Karşılaştırma

Tarih: 17 Eylül 2026. Bu tur kod yazılmadı, ölçüm koşulmadı; mevcut ölçümler ve kaynak okundu.

## Önce Bilinmesi Gereken Üç Şey

- **Rakibin sürümü 1.11.2, 1.9/1.10 değil.** Ölçümlerimizin hepsi HandBrakeCLI 1.11.2 ile koştu
  (`docs/olcumler/handbrake-kesit-turu.md`, `docs/taramalar/handbrake.md`). 1.11 ProRes, DNxHR, AMD AV1 10-bit,
  PCM ekledi; 1.10 "Social" hedef boyutlu Web ön ayarlarını (10 MB ya da 25 MB, iki kaynak çelişiyor) getirdi, 1.9 Discord/Discord Nitro ön ayarlarını. Kaynak: `NEWS.markdown`.
- **Çalışma ağacı (`t0/birlesim-084`, 0.8.2) `origin/main`in 53 commit gerisinde.** Güncel kalite ölçümleri
  (`av1-esbayt.md`, `av1-e0-hedef-bant.md`, `handbrake-kesit-turu.md`, `handbrake-acigi-yazilim.md`) ve
  `.github/workflows/kalite-olcumu.yml` yalnız `origin/main`de. SVT-AV1 `tune=1:enable-variance-boost=0`
  değişikliği de orada (`origin/main:src/VidShrink.Core/FfmpegArguments.cs:532`). Aşağıdaki satır numaraları
  aksi yazılmadıkça çalışma ağacından.
- **YOL-HARITASI'ndaki 8,79 VMAF-NEG açığı bayat.** Eski `av1_nvenc` + tonemap çıktısından, kilitsiz ölçerle
  ölçülmüştü (`handbrake-acigi.md:3-22`). Yazılım yolunda, variance boost kapalıyken SVT-AV1 eş baytta 18/18 hücrede
  önde (`av1-esbayt.md`). Açık kalan kalite alanları: düşük hedef (çözünürlük düşürme rejimi), HDR, donanım yolu.

Durum anahtarı: **öndeyiz / eşit / gerideyiz / yok** (VidShrink'te yok). Kanıtı olmayan satırda sütun
"ölçülecek" ya da "doğrulanacak" der. Boyut: **S** ≤ 1 ajan-günü, **M** 1-3, **L** 3+.

## 1. Kalite ve Verim

| # | Alan | HandBrake | VidShrink (kanıt) | Durum | Kapatmak için iş | Boyut | Fable |
|---|---|---|---|---|---|---|---|
| 1 | SDR film, eş bayt, 600/2000 kbit | x265 slow, 2 geçiş, turbo | SVT-AV1 e0 18/18 hücre önde, VMAF-NEG +0,62…+12,40 (`origin/main:docs/olcumler/av1-esbayt.md`) | öndeyiz | Ölçüm ham ffmpeg kollarıyla yapıldı; **ürün yolu** (`bench shrink`) e0 ile `handbrake` işini yeniden koşmak — ölçülecek | S | hayır |
| 2 | Ekran kaydı içeriği | x265 slow | +12,58 / +5,22 VMAF-NEG, XPSNR +13…+21 dB (`handbrake-kesit-turu.md`) | öndeyiz | — | — | hayır |
| 3 | Parlak kesit, karanlık PSNR | x265 slow | e1 döneminde −0,91…−1,40 dB geride (`handbrake-acigi-yazilim.md`); e0 ile `parlak` ölçülmedi | gerideyiz (son ölçüm) | `parlak` kesiti e0 ürün yoluyla — ölçülecek | S | hayır |
| 4 | Düşük hedef, çözünürlük/FPS düşürme rejimi | Çözünürlüğü korur | Eş boyutta harmonik 9,04 / 9,13 geride, düşürülen 5 çiftin 5'inde kayıp (`docs/olcumler/ab-duzenegi.md:375-470`, eski motor `381e8ab`/`af7a0fe`); FPS cezası modelde −17,8 puan sapıyor (`origin/main:docs/olcumler/ceza-kalibrasyonu.md`) | gerideyiz | Bugünkü motorla yeniden ölç; sonra `PlanCalculator` ceza sabitleri/yerleşim kararı | M-L | **evet** |
| 5 | HDR10 kaynak, eş boyut | x265 10-bit PQ korur | Tek ölçüm eski `av1_nvenc`+tonemap: 8,79 geride (`handbrake-acigi.md:146-158`); HDR koruma sonra düzeldi (`:39-51`), güncel motorla HandBrake'e karşı ölçülmedi | gerideyiz (bayat) → ölçülecek | Açık lisanslı HDR10 kaynakla CI kıyası | M | **evet** (kaynak, ölçü uzayı) |
| 6 | Donanım yolu kalitesi (NVENC/QSV/VCN) | H.265 NVENC/QSV/VCN/MF ön ayarları | `av1_nvenc` x265 slow'a 0,8 VMAF yakın (`docs/gpu-kodlama-bulgusu.md`); HandBrake NVENC ile kıyas yok; QSV/AMF donanımı yok (`kodek-secimi-kapanisi.md`) | ölçülecek | GPU'lu koşucu gerekiyor; CI'da GPU yok, kullanıcı PC'si yasak | M | **evet** |
| 7 | VideoToolbox kalitesi | H.265 VideoToolbox ön ayarı | `hevc_videotoolbox` x265'in 6-20 VMAF altında (`docs/olcumler/videotoolbox.md:221-240`); HandBrake VT ile kıyas yok | ölçülecek | macOS koşucusunda HB VT ↔ ürün | S | hayır |
| 8 | Hız, yazılım | x265 slow 60-145 sn / 10 sn kesit | SVT-AV1 p6 21-26 sn aynı kesitte (`av1-e0-hedef-bant.md`, oran tablosu); eski ölçüde av1_nvenc 1-3 sn ↔ HB 13-23 sn (`handbrake-acigi.md:73-86`, süre sütunu geçerli) | öndeyiz | HB'nin kendi SVT-AV1'i ile hız+kalite — ölçülecek | S | hayır |
| 9 | Turbo ilk geçiş | x264/x265'te açık | Kod var, varsayılan kapalı, hiç ölçülmedi (`docs/olcumler/turbo-ilk-gecis.md`) | gerideyiz | CI'da süre+kalite ölç, sonra varsayılanı karar ver | S | hayır |
| 10 | Hedef boyut isabeti | Hedef boyut yok (NEWS 0.9.6); ABR sapıyor: istenen 1894 kbps → 1498 (`handbrake-kesit-turu.md`) | 36/36 aşım yok, 31/36 bantta (`bench-2026-09-13.md:151-158`); e0 film 6/6 ilk denemede bantta, 18 hücrede taşma 0 (`av1-e0-hedef-bant.md`) | öndeyiz | — | — | hayır |
| 11 | Ekran içeriğinde bant altı teslim | (hedef yok) | 50 MB hedefe 38,7 MB, 100 MB'a 44,3 MB (`av1-e0-hedef-bant.md`, ekran satırları) | gerideyiz (kendi ölçütümüze göre) | İkinci denemenin kbit ölçeği; doygun içerikte "hedefe ulaşılamaz" bildirimi | M | **evet** |
| 12 | Hedef boyutlu hazır ayar | "Social" 30 sn/1 dk/2 dk/5 dk (1.10), Discord/Nitro (1.9) — süreye göre sabit çözünürlük, boyut garantisi yok | 8 çip, süre bağımsız gerçek hedef (`MainWindow.axaml.cs:1545-1554`) | öndeyiz → ölçülecek | HB Social ön ayarlarına karşı boyut isabeti + kalite | S | hayır |
| 13 | 10-bit SDR kodlama | x265 10-bit ön ayarları | SDR `yuv420p`; 10-bit VMAF-NEG'de ≈0, süre +%7-11 (`av1-esbayt.md`) | eşit | — | — | hayır |

## 2. Kodek, Kap, Ses, Altyazı

| # | Alan | HandBrake | VidShrink (kanıt) | Durum | Kapatmak için iş | Boyut | Fable |
|---|---|---|---|---|---|---|---|
| 14 | Yazılım video kodlayıcıları | x264, x265 8/10/12, SVT-AV1, VP8, VP9, MPEG-2/4, Theora, FFV1, ProRes, DNxHR | Otomatik planda libx264, libx265, libsvtav1 (`PlanParser.cs:13`); VP9 yalnız elle kodek kilidinde (`MainWindow.axaml.cs:1465`, `FfmpegArguments.cs:29`) ve dönüştürücüde | gerideyiz | VP9 küçültmeye; FFV1/ProRes/DNxHR dönüştürücüye (kapsam kararı) | M | **evet** (kapsam) |
| 15 | Donanım kodlayıcıları | NVENC, QSV, VCN (AV1 10-bit), Media Foundation, VideoToolbox | nvenc/qsv/amf (`PlanParser.cs:13`); VideoToolbox sınıflandırılıyor ama izinli listede yok (`CodecModel.cs:133`, `:221-223`); MF yok | gerideyiz | VideoToolbox'ı plan yoluna aç; Windows ARM için MF | M | hayır |
| 16 | Donanım çözme | nvdec/qsv açılıp kapanır | `-hwaccel auto` sabit (`FfmpegArguments.cs:387`) | eşit | — | — | hayır |
| 17 | Kap, küçültme kolu | MP4, MKV, WebM | Yalnız MP4; faststart koşulsuz (`FfmpegArguments.cs:449`) | gerideyiz | MKV/WebM çıkışı (çoklu iz ve altyazı için şart) | S | hayır |
| 18 | Kap, dönüştürme kolu | MP4, MKV, WebM | mp4, mov, webm, mkv, gif, m4a (`ConversionArguments.cs:117-153`) | öndeyiz | — | — | hayır |
| 19 | Ses kodlayıcıları | AAC, AC3, E-AC3, TrueHD, DTS, MP3, Opus, Vorbis, FLAC, ALAC, PCM | Ayrıştırıcı aac, libopus, libmp3lame, copy kabul eder (`PlanParser.cs:14`), otomatik plan hep aac seçer (`PlanCalculator.cs:1505`) | gerideyiz | FLAC/AC3/E-AC3 ekle (bütçe kuralıyla) | S | hayır |
| 20 | Ses passthru | Kodek maskesi + fallback, auto passthru | Tek bayrak `copy` (`FfmpegArguments.cs:441-442`), maske/fallback yok | gerideyiz | `--audio-copy-mask` eşdeğeri + bütçeden düşme | M | **evet** |
| 21 | Çoklu ses izi, dil listesi | `--all-audio`, `--audio-lang-list`, native dub | `-map` yok; ffmpeg varsayılanı tek ses (`FfmpegArguments.cs:387-449`) | yok | Akış eşleme katmanı | M | **evet** |
| 22 | Mixdown, DRC, gain, normalize | 3.0/4.0/5.1/7.1, Pro Logic II, DRC, gain | Yalnız ≤56 kbit stereoda mono indirme (`PlanCalculator.cs:1275-1279`, `FfmpegArguments.cs:446`) | gerideyiz | Ses filtre zinciri (`pan`/`loudnorm`/`volume`) | S | hayır |
| 23 | Altyazı: soft kopya, SRT/SSA içe alma | Var, dil/offset/kodlama | Kodlama yolunda yok (oynatıcıda `Playback/SubtitleOptions.cs` var) | yok | mkv'de kopya, mp4'te mov_text, harici SRT/ASS ekleme | M | **evet** |
| 24 | Altyazı yakma, forced / foreign audio scan | Var | Yok | yok | `subtitles=`/`overlay` yakma; forced bayrağı | M | hayır |
| 25 | Bölüm işaretleri | Aç/kapa, isim, CSV içe alma | Açık eşleme yok, ffmpeg varsayılanına kalıyor; doğrulanmadı | yok | `-map_chapters`; kırpmada bölüm kaydırma | S | hayır |
| 26 | Meta veri (başlık, kapak, tarih, konum) | 1.8'den beri passthru | Açık eşleme yok; doğrulanmadı | yok | `-map_metadata`, kapak eki (`attached_pic`) | S | hayır |
| 27 | HDR10 koruma | x265/SVT-AV1 10-bit | Canlı 10-bit yoklamayla korunuyor (`HdrResolver`, `handbrake-acigi.md:39-51`) | eşit | — | — | hayır |
| 28 | HDR10 statik metadata (mastering display, MaxCLL) | Aktarılıyor | x265 için `master-display`/`max-cll` var (`HdrResolver.cs:44-58`); SVT-AV1 kolunda doğrulanmadı | gerideyiz | SVT-AV1 `mastering-display`/`content-light` | S | hayır |
| 29 | HDR10+ / Dolby Vision passthru | x265 ve SVT-AV1 ile (1.8+), DV profil 5 etiketi (1.11) | Yok | yok | Yan veri aktarımı, profil eşlemesi | M | **evet** |
| 30 | HDR→SDR tonemap | Hable/Reinhard (ikincil kaynak, resmi belgede doğrulanamadı) | Hable, yalnız 10-bit kodlayıcı yoksa, görünür neden kodu (`HdrResolver.cs:22`, `PlanCalculator.cs:339`) | eşit | Tonemap algoritması seçimi (hable/bt.2390) | S | **evet** |

## 3. Filtreler

| # | Alan | HandBrake | VidShrink (kanıt) | Durum | Kapatmak için iş | Boyut | Fable |
|---|---|---|---|---|---|---|---|
| 31 | Deinterlace: decomb/yadif/bwdif, comb detect | Var, varsayılan koşullu | Yok (`src/` taramasında `yadif`/`bwdif` yok) | yok | `idet` yoklaması + koşullu `bwdif` | M | **evet** |
| 32 | Detelecine | Var | Yok | yok | `fieldmatch,decimate` | S | hayır |
| 33 | Gürültü giderme NLMeans / hqdn3d | Var, içerik ayarlı | Yok | yok | `nlmeans`/`hqdn3d`; düşük bit hızında kazancı ölçülecek | M | **evet** |
| 34 | Keskinleştirme unsharp/lapsharp, chroma smooth | Var | Yok | yok | `unsharp`/`cas` | S | hayır |
| 35 | Deblock | Var | Yok | yok | `deblock` filtresi | S | hayır |
| 36 | Otomatik kırpma | Varsayılan açık (auto/conservative) | Küçültmede yok; kaydedicide elle `crop` (`RecorderArguments.cs:783`); kalite kazancı ölçüldü: 0,083 p10, "değmez" (`docs/olcumler/siyah-kenar.md`) | yok | İsteğe bağlı kırpma (varsayılan kapalı), ölçülmüş mod birleşimi | S | **evet** |
| 37 | Döndürme/çevirme | 0/90/180/270, flip | Yok (küçültme/dönüştürme) | yok | `transpose`/`hflip` + döndürme metadata'sı | S | hayır |
| 38 | Gri tonlama, pad | Var | Yok | yok | `format=gray`, `pad` | S | hayır |
| 39 | Renk uzayı dönüşümü (bt601/709/2020, aralık) | Colorspace filtresi | Yalnız tonemap zinciri | yok | `zscale` ile açık dönüşüm | S | hayır |
| 40 | Ölçekleme | Elle, sınır, anamorfik | İçeriğe göre otomatik yerleşim (`ab-duzenegi.md` yerleşim tablosu) + özel çözünürlük (`MainWindow.axaml.cs:229`); anamorfik yok | eşit | Anamorfik (SAR) koruma | S | hayır |
| 63 | Deband, BM3D | 1.11 ile geldi | Yok | yok | `deband` filtresi; BM3D çok yavaş, kapsam dışı olabilir | S | hayır |
| 41 | Kare hızı | Sabit/tepe (pfr), framerate shaper | Otomatik FPS düşürme + özel FPS; `pfr` açık madde (`docs/inceleme/handbrake-motoru.md:611`) | eşit | — | — | hayır |

## 4. Ön Ayarlar, Kuyruk, Otomasyon

| # | Alan | HandBrake | VidShrink (kanıt) | Durum | Kapatmak için iş | Boyut | Fable |
|---|---|---|---|---|---|---|---|
| 42 | Resmi ön ayar sayısı | ~85: General 24, Web 8, Devices 23, Matroska 15, Hardware 11, Production 4+ (resmi belge) | 8 çip (`MainWindow.axaml.cs:1545-1554`) | gerideyiz | Hedef tabanlı kütüphane (platform + cihaz) | M | hayır |
| 43 | Cihaz ön ayarları (Apple, Android, Chromecast, Roku, PS, Xbox, Fire) | Var | Yok | yok | Uyumluluk profili: kodek/seviye/çözünürlük tavanı | M | hayır |
| 44 | Platform hedef boyutları (WhatsApp, Discord…) | Yalnız Social 25 MB | WhatsApp 16, 8/25/100/128/180 MB, yarı boy, arşiv (aynı satırlar) | öndeyiz | Discord 10 MB, Telegram, e-posta ekle | S | hayır |
| 45 | Özel ön ayar kaydet, JSON içe/dışa aktar | Var | Yalnız son ayarların kalıcılığı (`MainWindow.axaml.cs:1101-1160`) | yok | Ön ayar kütüphanesi + **HandBrake JSON içe alma** | M | hayır |
| 46 | Kuyruk | Düzenlenebilir kuyruk, içe/dışa aktarma, eşzamanlı kodlama, iş bitince eylem | Sıralı `ShrinkRequestQueue` (`ShrinkRequest.cs:63`, `ShrinkJobWindow.axaml.cs:77`); düzenleme/eşzamanlılık yok | gerideyiz | Kuyruk görünümü, sıralama, duraklatma, bitince eylem | M | hayır |
| 47 | Toplu tarama (klasör) | Var | Kabuktan çoklu dosya (`ShellIntegration.cs:25`) | gerideyiz | Klasör sürükle/bırak, özyinelemeli tarama | S | hayır |
| 48 | Klasör izleme | Resmi araç yok; istek #4415, üçüncü taraf HandBrake-daemon | Yok | eşit | `FileSystemWatcher` + kararlılık bekleme (öne geçme fırsatı) | M | **evet** |
| 49 | CLI | HandBrakeCLI, tam seçenek seti | Yalnız `--kucult` kabuk bayrağı (`ShellIntegration.cs:25`); `tools/VidShrink.Bench` ürün değil | yok | Başsız `vidshrink` CLI (hedef MB, çıkış, JSON rapor) | M | hayır |
| 50 | Kabuk sağ tık menüsü | Yok | Var (`ShellMenu.cs`, `ShellIntegration.cs`) | öndeyiz | — | — | hayır |
| 51 | DVD / Blu-ray / ISO / VIDEO_TS kaynak | Var (şifresiz) | Yok | yok | ffmpeg `dvdvideo` demuxer / libbluray | L | **evet** (kapsam) |

## 5. Önizleme, Aralık, Platform, UX

| # | Alan | HandBrake | VidShrink (kanıt) | Durum | Kapatmak için iş | Boyut | Fable |
|---|---|---|---|---|---|---|---|
| 52 | Önizleme | Canlı önizleme: konum + süre seçip kısa test kodlaması | Kodlanmış parça önizlemesi (`PreviewSegment.cs`), libmpv oynatıcı, karşılaştırma paneli (`Playback/ComparisonPanel.axaml.cs`) | öndeyiz | — | — | hayır |
| 53 | Ürün içi kalite ölçümü | Yok | VMAF-NEG/XPSNR/SSIM (`src/VidShrink.Ffmpeg/QualityMeter.cs:201`); kalibrasyonu besliyor (`MainWindow.axaml.cs:2932-2962`), kullanıcıya skor gösterilmiyor | öndeyiz | — | — | hayır |
| 54 | Aralık seçimi | Bölüm / saniye / kare | Başlangıç-bitiş yalnız Dönüştür sekmesinde (`ConversionPlan.cs:17-18`, `ConversionArguments.cs:42-46`); küçültme planında yok (`EncodePlan.cs`) | gerideyiz | Küçültmeye aralık; hedef bütçe aralık süresinden | S-M | hayır |
| 55 | Arayüz dili | Dil sayısı doğrulanamadı (Linux GUI Transifex ile çok dilli) | 42 yerel dosya (`src/VidShrink.App/Locales`) | öndeyiz | — | — | hayır |
| 56 | İşletim sistemi / mimari | Win x64+arm64, macOS universal, Linux flatpak x64+arm64 | win-x64, osx-x64, osx-arm64, linux-x64 (`docs/YOL-HARITASI.md`, "Tek sürüm numarası") | gerideyiz | win-arm64, linux-arm64, flatpak | M | hayır |
| 57 | macOS alt sürümü | macOS 14 Sonoma (1.5+) ve üstü, universal ikili | macOS 15 (`docs/YOL-HARITASI.md`, "macOS alt sürümü: 15") | gerideyiz | libmpv'yi kendimiz derlemek (`.claude/sonra.md`) | L | hayır |
| 58 | Kurulum ve güncelleme | Kurucu; Windows'ta .NET Desktop Runtime şart (10 mu 8 mi, kaynaklar çelişiyor); güncelleme yalnız bildirim | `VidShrink-Setup.exe` (0.8.4), iki adımlı otomatik güncelleme (0.8.0) | öndeyiz | — | — | hayır |
| 59 | İş bitince bildirim / uyku / kapatma | Windows'ta özel "When Done" eylemleri (1.10) | OS bildirimi ve bitince eylem yok (`TrayIcon`/`Toast` sıfır eşleşme) | yok | Bitince eylem seçeneği | S | hayır |
| 60 | Kullanıcı karar sayısı | Ön ayar + sekmeler | Tek karar: hedef MB (`docs/olcumler/auto-mod.md`, K2) | öndeyiz | — | — | hayır |
| 61 | Ek araçlar | Yok | Ekran kaydedici, paylaşım yüklemesi (`Core/Share/`), klip/GIF (`Playback/ClipExport.cs`) | öndeyiz | — | — | hayır |
| 62 | Tema | Açık/koyu | 26 palet (`Themes/PaletteCatalog.cs:47-52`) | öndeyiz | — | — | hayır |

## Sayım

Satır başına Durum sütunu (63 satır):

| Durum | Sayı | Satırlar |
|---|---:|---|
| öndeyiz | 15 | 1, 2, 8, 10, 12, 18, 44, 50, 52, 53, 55, 58, 60, 61, 62 |
| eşit | 7 | 13, 16, 27, 30, 40, 41, 48 |
| gerideyiz | 18 | 3, 4, 5, 9, 11, 14, 15, 17, 19, 20, 22, 28, 42, 46, 47, 54, 56, 57 |
| yok | 21 | 21, 23, 24, 25, 26, 29, 31, 32, 33, 34, 35, 36, 37, 38, 39, 43, 45, 49, 51, 59, 63 |
| ölçülecek (hüküm yok) | 2 | 6, 7 |

**Gerideyiz + yok = 39.** 3 ve 5 bayat ölçüme dayanıyor, 1. dalga hükmü değiştirebilir.

## Uygulama Dalgaları

Kural: her dalga tek ajanın işi; aynı anda koşan dalgaların dosya kümeleri kesişmez. `FfmpegArguments.cs`
2, 3 ve 6. dalgalarda ortak; bu üçü **sırayla** koşar, 2. dalga filtre ve eşleme için tek ekleme noktasını açar.

| Dalga | İş | Kapattığı satırlar | Sahip olduğu dosyalar | Paralel koşabilir |
|---|---|---|---|---|
| **1 — Ölçüm (CI)** | `kalite-olcumu`na üç iş: (a) ürün yolu e0 ↔ HB, `parlak`+`karanlik`+`hareketli`+`ekran`; (b) HB "Social" ve Discord ön ayarları ↔ ürün 25 MB, düşük hedefte çözünürlük düşürme rejimi; (c) HB turbo ↔ ürün turbo süre+kalite. Kullanıcı PC'sinde kodlama yok | 1, 3, 4 (ölçüm), 9, 12 | `.github/workflows/kalite-olcumu.yml`, `tools/kalite-paketi-3/*`, `docs/olcumler/handbrake-*.md` | 2 ve 4 ile |
| **2 — Akış eşleme** | Yeni `Core/StreamMapping.cs`: tüm ses izleri, dil listesi, passthru maskesi + fallback, altyazı kopya (mkv) / mov_text (mp4), `-map_chapters`, `-map_metadata`, kapak; küçültmede MKV/WebM kabı; ses/altyazı baytının bütçeden düşümü; FLAC/AC3/E-AC3 | 17, 19, 20, 21, 22, 23, 25, 26 | `Core/StreamMapping.cs` (yeni), `Core/MediaInfo.cs`, `Core/ProbeResult.cs`, `Ffmpeg/FfprobeClient.cs`, `Core/PlanParser.cs`, `Core/FfmpegArguments.cs` (ses bölümü + ekleme noktası), `PlanCalculator.cs` ses bütçesi, testleri | 1 ve 4 ile |
| **3 — Filtre zinciri** | Yeni `Core/VideoFilterChain.cs` (bwdif, detelecine, nlmeans/hqdn3d, unsharp, deblock, transpose, crop, pad, gray, zscale renk uzayı) ve `Ffmpeg/InterlaceProbe.cs` (`idet`), `Ffmpeg/CropProbe.cs` (`siyah-kenar` mod birleşimi); deband; küçültmeye aralık (`EncodePlan` Start/End); varsayılanlar kapalı, deinterlace koşullu | 31-39, 54, 63 | yeni üç dosya, `Core/EncodePlan.cs`, `Core/FfmpegArguments.cs` (2'nin ekleme noktası), testleri | 4 ile (2 bittikten sonra) |
| 4 — Başsız CLI + klasör izleme | `src/VidShrink.Cli` (hedef MB, çıkış, JSON rapor, çıkış kodu), `Core/WatchFolder.cs` | 48, 49 | yeni proje, `VidShrink.sln`, `release.yml` CLI paketi | 1, 2, 3 ile |
| 5 — Ön ayar kütüphanesi (Core) | `Core/PresetLibrary.cs`: JSON içe/dışa, cihaz profilleri, Discord/Telegram/e-posta, HandBrake ön ayar JSON'unu okuyup hedef tabanlı plana çevirme | 42, 43, 44, 45 | `Core/PresetLibrary.cs`, `Core/Presets/*.json`, testleri | 1-4 ile |
| 6 — HDR yan verisi | Statik HDR10 metadata, HDR10+/DV passthru, tonemap algoritması kararı | 28, 29 | `Core/HdrResolver.cs`, `Core/FfmpegArguments.cs` HDR bölümü, `Ffmpeg/FfprobeClient.cs` yan veri | 3 bittikten sonra |
| 7 — Arayüz | İz/altyazı/filtre paneli, kuyruk düzenleme ve bitince eylem, ön ayar seçici; 42 dil | 22, 24, 46, 47, 59 + 2/3/5'in yüzeyi | `MainWindow.axaml(.cs)`, `ShrinkJobWindow.*`, `Locales/*` | 2, 3, 5 bittikten sonra |
| 8 — Kodlayıcı ve platform kapsamı | VideoToolbox plan yoluna, VP9 küçültmeye, win-arm64 / linux-arm64 RID'leri | 14 (kısmi), 15, 56 | `Core/PlanParser.cs`, `Core/CodecModel.cs`, `release.yml` | 4 ile değil (`release.yml`) |
| 9 — Düşük hedef kalitesi | 1. dalganın ölçümüne göre ceza sabitleri ve yerleşim kararı; ekran içeriğinde bant altı | 4, 11 | `Core/PlanCalculator.cs`, `Core/CodecModel.cs` | 8 ile değil |
| 10 — Kapsam dışı adaylar | DVD/Blu-ray, ProRes/DNxHR/FFV1, macOS 15 altı, donanım yolu ölçümü | 6, 7, 51, 57 | fable kararına bağlı | — |

**İlk üç dalga:** 1 (ölçüm, CI) ve 2 (akış eşleme) paralel; 2 bitince 3 (filtre zinciri). 4 bu üçüyle paralel
koşabilir. 2 ve 3, 39 geride/yok satırının 19'unu kapatıyor.

## Fable'a Gidecek Sorular

1. **Kapsam.** "Her alanda geçmek" DVD/Blu-ray okumayı, ProRes/DNxHR/FFV1 gibi üretim kodeklerini ve 88 ön ayarlık
   bir kütüphaneyi de kapsıyor mu? Hedef boyut aracının kimliğini bozmadan hangi satırlar bilerek "yapılmaz"
   diye yazılmalı? Yapılmayan satır "geçildi" sayımına nasıl girer?
2. **Donanım yolunun ölçüm zemini.** CI koşucusunda GPU yok, kullanıcı PC'sinde ağır kodlama yasak. HandBrake
   NVENC/QSV/VCN ile ürünün donanım yolu nerede kıyaslanır: GPU'lu bulut koşucusu mu, kısa kesitle hafif bir
   istisna mı, yoksa donanım yolu "ölçülmedi" diye kalıp kalite iddiası yalnız yazılım yoluna mı dayanır?
3. **Düşük hedef rejimi.** Eş boyutta çözünürlüğü düşürdüğümüz 5 çiftin 5'inde kaybettik (eski motor), HandBrake
   çözünürlüğü koruyor. Ceza modeli FPS'te −17,8 puan sapıyor. Doğru hamle ceza sabitlerini kalibre etmek mi,
   düşürme eşiğini yukarı çekmek mi, yoksa aday yerleşimleri kısa kesitte ölçüp seçmek mi (süre bedeli)?
4. **HDR kıyası.** Açık lisanslı HDR10 kaynak hangisi olmalı? HandBrake PQ korurken ürün de koruyorsa ölçü uzayı
   PQ'da doğrudan mı, yoksa ikisini aynı tonemap referansına çekerek mi? Tonemap gerektiğinde hable mı bt.2390 mı?
5. **Ölçü çelişkisi.** Variance boost kapanınca VMAF-NEG ve karanlık PSNR yükseldi, ama boost'un hedefi karanlık düz
   alanda bantlaşma ve bu VMAF-NEG'e yansımayabilir. Öznel bantlaşma için hangi nesnel vekil (ör. karanlık ton sayısı
   oranı, `KaranlikOlcu.cs`) kapı olmalı?
6. **Ses/altyazı izleri ve bütçe.** Passthru TrueHD/DTS izi hedef bütçenin büyük kısmını yiyebilir. Varsayılan ne olmalı:
   ilk iz yeniden kodlanır, diğerleri düşürülür mü; passthru yalnız bütçe payı eşiğin altındaysa mı? Altyazı için
   mp4'te mov_text'e dönüşüm mü, otomatik mkv'ye geçiş mi (paylaşım platformu uyumu)?
7. **Deinterlace ve denoise varsayılanları.** HandBrake comb detect + decomb'u varsayılan açıyor. `idet` yoklamasının
   yanlış pozitif maliyeti ve düşük bit hızında `nlmeans`in VMAF-NEG'i yapay yükseltme riski varken varsayılan açık
   mı, koşullu mu, kapalı mı olmalı; kazanç nasıl ölçülmeli?
8. **Otomatik kırpma.** Kalite kazancı ölçüldü ve "değmez" çıktı (p10 +0,083), ama HandBrake bunu varsayılan açıyor ve
   kullanıcı siyah bantsız çıktı bekleyebilir. Karar kalite ölçüsüne mi, izleyici beklentisine mi dayanmalı?
9. **Klasör izleme ve CLI mimarisi.** Tek örnek kanalı (`SingleInstanceChannel`) varken izleme uygulama içinde mi,
   ayrı başsız süreçte mi yaşamalı? CLI aynı motoru paylaşırken güncelleme ve ffmpeg yolu nasıl çözülür?
10. **Ekran içeriğinde bant altı.** Doygun içerikte 100 MB hedefe 44 MB teslim ediliyor. Bu kusur mu (kbit ölçeği
    düzeltilmeli), yoksa "daha büyük dosya kaliteyi artırmaz" diye kullanıcıya söylenecek doğru davranış mı?

## Kaynaklar

- Ölçümler: `docs/olcumler/handbrake-acigi.md`, `ab-duzenegi.md`, `auto-mod-yeni-taban.md`, `siyah-kenar.md`,
  `turbo-ilk-gecis.md`, `videotoolbox.md`, `bench-2026-09-13.md`, `kodek-secimi-kapanisi.md`; `origin/main` üzerinde
  `handbrake-kesit-turu.md`, `handbrake-acigi-yazilim.md`, `av1-esbayt.md`, `av1-e0-hedef-bant.md`,
  `ceza-kalibrasyonu.md`, `whatsapp-karanlik.md`; `docs/gpu-kodlama-bulgusu.md`; `docs/inceleme/handbrake-motoru.md`.
- HandBrake: `https://raw.githubusercontent.com/HandBrake/HandBrake/master/NEWS.markdown`,
  `https://handbrake.fr/docs/en/latest/technical/official-presets.html`,
  `https://handbrake.fr/docs/en/latest/cli/command-line-reference.html`.
- Envanter ajanları: HandBrake tarafı WebFetch özetinden (ham NEWS birebir okunmadı), VidShrink tarafı 0.8.2 çalışma ağacından.
- Doğrulanamayanlar: HandBrake tonemap filtresinin resmi sürümü, Social ön ayar boyutu (10/25 MB), arayüz dil sayısı, Windows .NET sürümü, kuyruk içe/dışa aktarma, kapak resmi geçişi.
