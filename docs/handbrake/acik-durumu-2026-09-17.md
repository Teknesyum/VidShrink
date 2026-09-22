# HandBrake Açık Durumu — 17 Eylül 2026

Taban: `acik-analizi.md` (63 satır, 0.8.2 dönemi). Karşılaştırılan: `origin/main` `0bc86188` (çıkık kopya
`.calisma/kurucu-kilit`). Son main CI koşumu 35231811847 yeşil. Bu turda kod yazılmadı, ölçüm koşulmadı.
Satır numaraları aksi yazılmadıkça `src/` altından. Ölçüm belgeleri `docs/olcumler/` altında.

Durum anahtarı: öndeyiz / eşit / gerideyiz / yok / ölçülecek / bilerek yapılmaz (fable kararı 1,
`.calisma/danisma/handbrake-yanit.md`). Gürültü bandı B1'in: VMAF-NEG ±0,3, XPSNR ±0,2 dB, kbps ±%2.

## 1. Satır Satır

### Kalite ve Verim

| # | Alan | Eski → Bugün | Kanıt |
|---|---|---|---|
| 1 | SDR eş bayt | öndeyiz → **öndeyiz** | `handbrake-kiyas-b1-sdr.md`: 8/8 önde, VMAF-NEG +0,35…+12,40, XPSNR +0,35…+21,14 (koşum 35158725446). CLI ürün yoluyla ölçülmedi (`b1:41`) |
| 2 | Ekran kaydı | öndeyiz → **öndeyiz** | B1 ekran 600/2000: VMAF-NEG +12,40 / +4,94, CAMBI −5,18 / −0,84 |
| 3 | Parlak kesit, karanlık PSNR | gerideyiz → **öndeyiz** | B1 parlak Δ karanlık PSNR +1,63 / +1,20; e1 dönemi ölçümü geçersizleşti |
| 4 | Düşük hedef, düşürme rejimi | gerideyiz → **öndeyiz** | `handbrake-kiyas-b7-aciklar.md` Açık 3: otomatik FPS düşürme kaldırıldı (`fb764674`), `urun-otomatik` 6/6 HB önünde (+2,54…+12,94); hareketli 300 harmonik 3,96 → 65,69 (HB 52,49) |
| 5 | HDR10 eş boyut | gerideyiz (bayat) → **öndeyiz** | `handbrake-kiyas-b4-hdr.md`: 3/4 önde; CosmosCaterpillar 3000 XPSNR −0,25 geride. Kaynak sentetik etiketli (gerçek mastering verisi ölçülmedi) |
| 6 | Donanım yolu | ölçülecek → **eşit** (NVENC), QSV/AMF ölçülmedi | `nvenc-2.md` özet: adil h264 +2,32, adil hevc −0,20, adil av1 −0,10 (bant içi). GOP 10 sn (`nvenc-gop10.md`, hevc −0,22) ve tam kare/la20 (`nvenc-tamkare.md`, hevc dörtlü −0,26) kapıları kaldı. Ürün hedefin ort. %4,8 altında teslim ediyor (donanımda açık, `butce-doldur.md`) |
| 7 | VideoToolbox | ölçülecek → **gerideyiz** | B7 Açık 1: VT tek geçiş Main 10'a çevrildi (`b07ed823`); HB VT'ye karşı VMAF-NEG 6/6 önde ama XPSNR(ii) film hücrelerinde 2/6 kapıda, toplam −1,11 dB. B6 CAMBI cümleleri geçersiz ölçerden (B7) |
| 8 | Hız, yazılım | öndeyiz → **öndeyiz** | B1: ürün kodlama süresi HB'nin 0,30–0,86×'i (ekran 2000'de 1,34×). HB'nin kendi SVT-AV1'i ile kıyas ölçülmedi |
| 9 | Turbo ilk geçiş | gerideyiz → **eşit** | B7 Açık 4: `slow-firstpass=0` (`CodecModel.cs:223`); ürün turbo ↔ HB turbo 77,28/77,24, 96,79/96,47, 95,68/95,16. Varsayılan yalnız Hızlı kipte ve karanlık geçişte (`PlanCalculator.cs:789`, `:569`, `:611`); HB'de ön ayarda açık |
| 10 | Hedef isabeti | öndeyiz → **öndeyiz** | HB'de hedef boyut yok. B7: 51 ürün satırının 35'i ilk denemede bantta. Hb-2b: tavan bekçisi (`CeilingGuard.cs`) — hiç sığmayan koşum artık dosyasız bitmiyor, en küçük sonucu `OverTarget` işaretle teslim ediyor (`handbrake-kiyas-hb2b.md`) |
| 11 | Ekranda bant altı teslim | gerideyiz → **gerideyiz** (kendi ölçütümüz) | `handbrake-kiyas-karar10-ekran.md`: 120 sn döngüde %97,6–98,6; 4,33 sn kesitte SVT %82,3, VT %23,9. `Saturation.cs` doygunluk kuralı main'de; `BudgetFill.cs:23` yalnız yazılım. Arayüzde `Saturated` tüketicisi yok (`src/VidShrink.App` taraması: yalnız `MainWindow.axaml.cs:4217` `HasUnderBandFallback`) — fable kararı 10'un "sessizlik" yanı açık |
| 12 | Hedef boyutlu hazır ayar ↔ Social | öndeyiz→ölçülecek → **öndeyiz** | `handbrake-kiyas-b2-dusuk-hedef.md`: 21 satırın 18'i önde. B7 Açık 7: Social SVT p4; tek 1080p60 ön ayarında parlak −0,35/−0,10 (%4,8 az bayt, bant içi), karanlık/hareketli önde. Kalan: ~%96 dolum |
| 13 | 10-bit SDR | eşit → **eşit** | Yeni ölçüm yok. B7 Açık 2 ve hb2b: SVT 10 bit CAMBI(ii)'yi oynatmıyor (9,15–9,38) |

### Kodek, Kap, Ses, Altyazı

| # | Alan | Eski → Bugün | Kanıt |
|---|---|---|---|
| 14 | Yazılım kodlayıcıları | gerideyiz → **gerideyiz** (ProRes/DNxHR/FFV1 bilerek yapılmaz) | `Core/PlanParser.cs:13` libx264/libx265/libsvtav1/libvpx-vp9; VP9 küçültmede kilitli kodekle (CLI `--kodek vp9`, Gelişmiş kodek kilidi), WebM + opus, iki geçiş (`Vp9KucultmeTests`) |
| 15 | Donanım kodlayıcıları | gerideyiz → **gerideyiz** | nvenc/qsv/amf plan yolunda (`PlanParser.cs:13`); VT tanımlı ama plan yolunda yok (`CodecModel.cs:150`, `:159-161`); Media Foundation yok |
| 16 | Donanım çözme | eşit → **eşit** | `-hwaccel auto` yalnız dağıtım kodeklerinde (`FfmpegArguments.cs:392-401`, merge `8c1ef48f`) |
| 17 | Kap, küçültme | gerideyiz → **eşit** | `Core/StreamMapping.cs:134-145` Mp4/Mkv/WebM; otomatik seçim Mp4, "İzleri koru"da Mkv (`:131-132`); faststart yalnız Mp4 (`FfmpegArguments.cs:469-470`). WebM otomatik seçilmiyor |
| 18 | Kap, dönüştürme | öndeyiz → **öndeyiz** | Eski kanıt (`ConversionArguments.cs:117-153`); bu turda yeniden okunmadı |
| 19 | Ses kodlayıcıları | gerideyiz → **gerideyiz** | aac/libopus/libmp3lame/copy (`PlanParser.cs:14`); flac/ac3/eac3 yalnız kopya (`StreamMapping.cs:110-112`) |
| 20 | Ses passthru | gerideyiz → **eşit** | Kap izin listesi, iz ≤ yeniden kodlama, toplam ≤ bütçenin %15'i, tutmazsa yeniden kodlama (`StreamMapping.cs:101`, `:106-113`, `:214-239`) |
| 21 | Çoklu ses, dil | yok → **eşit** | "İzleri koru"da tüm izler, değilse dil tercihi (`StreamMapping.cs:156-163`, `:193-199`, `-map` `:61-64`) |
| 22 | Mixdown, DRC, gain | gerideyiz → **gerideyiz** | >2 kanal `-ac 2` (`StreamMapping.cs:231-235`); loudnorm/volume/DRC yok (tarandı) |
| 23 | Altyazı kopya / içe alma | yok → **gerideyiz** | Mp4 mov_text, resim düşer; Mkv kopya; WebM webvtt (`StreamMapping.cs:254-279`). Harici SRT/ASS kodlamada yok (yalnız oynatıcı `PlayerView.Tracks.cs:30-32`) |
| 24 | Yakma, forced | yok → **yok** | `subtitles=` yok; `IsForced` alanı (`StreamMapping.cs:29`) kararda kullanılmıyor |
| 25 | Bölüm | yok → **eşit** | `-map_chapters 0` (`StreamMapping.cs:85`); isim/CSV içe alma yok |
| 26 | Meta veri, kapak | yok → **gerideyiz** | `-map_metadata 0` (`StreamMapping.cs:85`); kapak (`attached_pic`) çıktıya taşınmıyor (`FfprobeClient.cs:251-253`, `StreamMapping.cs:186`) |
| 27 | HDR10 koruma | eşit → **eşit** | B4: 4/4 çıktıda `smpte2084`, `bt2020`, CLL 1000,400 |
| 28 | HDR10 statik metadata | gerideyiz → **eşit** | B4: libsvtav1 çıktısı mastering değerlerini kaynakla aynen yazdı (`yan_veri_es` evet). Kodda svtav1-params'ta `mastering-display` yok (`HdrResolver.cs:44-47`); ffmpeg yan veriyi kendisi taşıyor (ölçülen davranış, kaynaktan doğrulanmadı) |
| 29 | HDR10+ / DV | yok → **yok** | `hdr10plus|dolby|dovi` taraması boş (yalnız `FfmpegArguments.cs:268` yorum) |
| 30 | Tonemap | eşit → **eşit** | `tonemap=hable` (`HdrResolver.cs:22`); fable kararı 4 "hable kalır" |

### Filtreler

| # | Alan | Eski → Bugün | Kanıt |
|---|---|---|---|
| 31 | Deinterlace | yok → **yok** | `yadif|bwdif|idet` src'de yok |
| 32 | Detelecine | yok → **yok** | `fieldmatch|decimate` yok |
| 33 | Denoise | yok → **yok** | `nlmeans|hqdn3d` yok (fable 7: varsayılan kapalı, seçenek olmalı) |
| 34 | Keskinleştirme | yok → **yok** | `unsharp|cas` kodlamada yok (yalnız oynatıcı `MpvEngine.Advanced.cs:82`) |
| 35 | Deblock | yok → **yok** | `deblock` yok |
| 36 | Otomatik kırpma | yok → **yok** | `cropdetect` yok; fable 8'in "tespit açık, kırpma tek tık" kararı uygulanmadı |
| 37 | Döndürme/çevirme | yok → **yok** | kodlamada `transpose|hflip` yok (yalnız oynatıcı `MpvEngine.cs:434-436`) |
| 38 | Gri, pad | yok → **yok** | `format=gray|pad=` yok |
| 39 | Renk uzayı | yok → **yok** | `zscale` yalnız tonemap zincirinde (`HdrResolver.cs:22`) |
| 40 | Ölçekleme | eşit → **eşit** | `scale=W:H:flags=lanczos` (`FfmpegArguments.cs:408-409`); `setsar` yok, anamorfik koruma ölçülmedi |
| 63 | Deband, BM3D | yok → **yok** (BM3D bilerek yapılmaz) | `deband` yok |
| 41 | Kare hızı | eşit → **eşit** | `fps=` yalnız kaynağın altına (`FfmpegArguments.cs:414-415`); otomatik düşürme yalnız çalışabilir taban altında (B7 Açık 3); pfr yok |

### Ön Ayarlar, Kuyruk, Otomasyon

| # | Alan | Eski → Bugün | Kanıt |
|---|---|---|---|
| 42 | Ön ayar sayısı | gerideyiz → **gerideyiz** ("85 birebir" bilerek yapılmaz) | 8 çip (`MainWindow.axaml:297-355`) |
| 43 | Cihaz ön ayarları | yok → **yok** | cihaz profili taraması boş |
| 44 | Platform hedefleri | öndeyiz → **öndeyiz** | WhatsApp 16/180, 8/25/100/128, Yarı, Arşiv (`MainWindow.axaml:297-355`); Discord/e-posta yalnız ipucu metninde, Telegram yok |
| 45 | Özel ön ayar, JSON | yok → **yok** (HB JSON içe alma bilerek yapılmaz) | kaydet/içe/dışa aktarma yok |
| 46 | Kuyruk | gerideyiz → **gerideyiz** | Sıralı `Queue<ShrinkRequest>` + `_busy` (`ShrinkJobWindow.axaml.cs:77`, `:81`, `:226-238`); düzenleme yok |
| 47 | Toplu tarama | gerideyiz → **gerideyiz** | Klasör bırakma reddediliyor, tek dosya şartı (`MainWindow.axaml.cs:2892-2899`) |
| 48 | Klasör izleme | eşit → **eşit** | HB'de de yok; bizde `FileSystemWatcher|izle` yok. Fable 9'un `izle` alt komutu yapılmadı |
| 49 | CLI | yok → **eşit** | `src/VidShrink.Cli`: `kucult`, `plan`; `--hedef --kalite --kodek --cikti --json --olcumsuz --vmaf --hizli`; çıkış kodları 0/1/2/3/64/130 (`Cli/CliRequest.cs:12-17`, `:73-123`); `release.yml:216` her RID'e yayınlıyor. HB CLI'ın seçenek seti daha geniş (filtre/iz yok) |
| 50 | Kabuk menüsü | öndeyiz → **öndeyiz** | Eski kanıt, değişiklik yok |
| 51 | DVD/Blu-ray | yok → **bilerek yapılmaz** | fable kararı 1 |

### Önizleme, Aralık, Platform, UX

| # | Alan | Eski → Bugün | Kanıt |
|---|---|---|---|
| 52 | Önizleme | öndeyiz → **öndeyiz** | Eski kanıt; önizleme parçası `FfmpegArguments.cs:570-577` |
| 53 | Ürün içi kalite ölçümü | öndeyiz → **öndeyiz** | Eski kanıt (`QualityMeter.cs`) |
| 54 | Küçültmede aralık | gerideyiz → **gerideyiz** | `EncodePlan.cs:96-120` Start/End yok; aralık yalnız Dönüştür (`ConversionPlan.cs:17`, `MainWindow.axaml.cs:4353-4354`) |
| 55 | Arayüz dili | öndeyiz → **öndeyiz** | `App/Locales`: 42 dil × 8 dosya; CLI en+tr |
| 56 | OS/mimari | gerideyiz → **gerideyiz** | `release.yml:202` `[win-x64, osx-arm64, osx-x64, linux-x64]` |
| 57 | macOS alt sürümü | gerideyiz → **gerideyiz** | Değişiklik yok (macOS 15) |
| 58 | Kurulum/güncelleme | öndeyiz → **öndeyiz** | Eski kanıt, bu turda yeniden okunmadı |
| 59 | Bitince eylem/bildirim | yok → **yok** | `Toast|NotifyIcon|shutdown` yok (yalnız `App.axaml.cs:75` ShutdownMode) |
| 60 | Karar sayısı | öndeyiz → **öndeyiz** | Değişiklik yok |
| 61 | Ek araçlar | öndeyiz → **öndeyiz** | Değişiklik yok |
| 62 | Tema | öndeyiz → **öndeyiz** | 26 palet (`PaletteCatalog.cs:17-27`) |

### Sayım

| Durum | Eski | Bugün | Satırlar (bugün) |
|---|---:|---:|---|
| öndeyiz | 15 | **18** | 1, 2, 3, 4, 5, 8, 10, 12, 18, 44, 50, 52, 53, 55, 58, 60, 61, 62 |
| eşit | 7 | **15** | 6, 9, 13, 16, 17, 20, 21, 25, 27, 28, 30, 40, 41, 48, 49 |
| gerideyiz | 18 | **14** | 7, 11, 14, 15, 19, 22, 23, 26, 42, 46, 47, 54, 56, 57 |
| yok | 21 | **15** | 24, 29, 31, 32, 33, 34, 35, 36, 37, 38, 39, 43, 45, 59, 63 |
| ölçülecek | 2 | **0** | — (QSV/AMF satır 6 içinde "ölçülmedi") |
| bilerek yapılmaz | — | **1** | 51 (14, 42, 45, 63'ün birer parçası da) |

**Gerideyiz + yok: 39 → 29.** Kalite satırlarında (1-13) geride kalan yalnız 7 (VT XPSNR) ve 11 (kendi ölçütümüz).
Kalite dışı bulgu: karanlık filmde SVT-AV1 CAMBI açığı (B1 +2,70/+2,79) yalnız Otomatik + Aggressive/Extreme'de
x265 geçişiyle kapandı (`karanlik-x265.md`: CAMBI(ii) 6,74/6,61, HB 6,52/6,56; `DarkContentSwitch.cs:5,15`). Diğer
rejimlerde SVT seçilirse açık sürüyor; hiçbir SVT anahtarı kapıyı geçmedi (`handbrake-kiyas-hb2b.md`).

## 2. Fable'ın On Sorusu

On sorunun tamamı **`.calisma/danisma/handbrake-yanit.md`**'de cevaplandı (`docs/danisma`da değil; depoya girmedi).
`docs/danisma`da sonraki turlar bazı cevapları ölçümle sürdürdü:

| Soru | Cevap yeri | docs/danisma izi | Bugün |
|---|---|---|---|
| 1 Kapsam | handbrake-yanit K1 | — | kapandı (5 satır parçası bilerek yapılmaz) |
| 2 Donanım ölçüm zemini | K2 | `2026-09-17-nvenc2-fable.md`, `nvenc-gop10-fable.md`, `nvenc-hevc-sonraki-fable.md`, `hb2-yanit` Soru 6, `hb2b-yanit` Soru B | kapandı; hevc −0,2 açığı ve %4,8 boş bütçe açık |
| 3 Düşük hedef | K3 | `hb2-yanit` Soru 3-4, `hb2c-soru/yanit`, `hb2b-fable` Bölüm A | kapandı (FPS kuralı, doygunluk, bekçi). K3'ün "iki aday yerleşim ölçümü" kodda aranmadı, doğrulanmadı |
| 4 HDR kıyası | K4 | — | kapandı (B4 ölçtü) |
| 5 Bantlaşma ölçüsü | K5 | `hb2-yanit` Soru 1, `hb2b-yanit` Soru A, `hb2b-fable` Soru 6-9, `karanlik-x265-fable.md` | kapandı (CAMBI(ii) kapı). **Açık:** hangi izleyici için hüküm (dither'lı libmpv mi) ürün kararı (B7 "Ölçer Düzeltmesi") |
| 6 Ses/altyazı | K6 | — | kapandı, 1c uygulandı |
| 7 Deinterlace/denoise | K7 | — | cevaplı, uygulanmadı |
| 8 Otomatik kırpma | K8 | — | cevaplı, uygulanmadı |
| 9 CLI/izleme mimarisi | K9 | — | cevaplı; CLI yapıldı, `izle` yapılmadı |
| 10 Ekran bant altı | K10 | `hb2-yanit` "Doygunluk", `hb2b-fable` Bölüm A | kapandı; arayüz bildirimi uygulanmadı |

Fable'a gidecek yeni açık sorular (belgelerde "açık" diye kalanlar):
- VT: XPSNR(ii) HB kapısı 2/6 (B7) — VT plan yoluna açılsın mı, hangi kapıyla?
- NVENC: hevc adil −0,20 ve donanımda ort. %4,8 boş bütçe (`butce-doldur.md` aday 1 kaldı) — sonraki aday ne?
- Social 1080p60 ~%96 dolum ve 135,6 sn yeniden deneme (B7 Açık 7) — merkez nişan kuralı değişsin mi?
- Karanlık geçiş Balanced/diğer rejimlerde SVT kaldığında CAMBI açığı — geçiş kapsamı genişlesin mi?
- CAMBI hükmünün izleyicisi (8 bit dither'sız mı, libmpv mi).

## 3. On Dalga

| Dalga | Durum | Eksik |
|---|---|---|
| 1 Ölçüm (CI) | **yapıldı** | B1-B6 + karar 10 (35158725446), B7, hb2b, karanlık geçiş. Eksik: CLI ürün yoluyla koşum (`urun_yolu=cli` hazır, hiç koşulmadı), HB SVT-AV1 hız kıyası |
| 2 Akış eşleme | **kısmen** | `StreamMapping.cs` main'de (merge `21fac99a`). Eksik: flac/ac3/eac3 kodlama (19), loudnorm/gain (22), harici SRT/ASS (23), kapak (26), forced önerisi (24) |
| 3 Filtre zinciri + aralık | **başlanmadı** | 31-39, 54, 63 |
| 4 CLI + klasör izleme | **kısmen** | CLI yapıldı; `izle` alt komutu ve GUI düğmesi yok (48) |
| 5 Ön ayar kütüphanesi | **başlanmadı** | 42-45 |
| 6 HDR yan verisi | **kısmen** | Statik metadata ölçümle eşit (B4), tonemap kararı verildi; HDR10+/DV 8.1 passthru yok (29) |
| 7 Arayüz | **kısmen** | "İzleri koru" kutusu 42 dilde, karanlık geçiş gerekçesi 42 dilde. Eksik: kuyruk düzenleme (46), klasör bırakma (47), bitince eylem (59), filtre paneli, `Saturated` bilgisi (11) |
| 8 Kodlayıcı ve platform | **kısmen** | VT argümanları düzeltildi (tek geçiş, Main 10); VT plan yoluna açılmadı (15), VP9 küçültmede yok (14), arm64 RID yok (56) |
| 9 Düşük hedef | **yapıldı** (yazılım) | FPS kuralı, `Saturation`, `CeilingGuard`, `BudgetFill` (yazılım), Social p4. Eksik: donanım bütçe doldurma, rampa 1200 SVT oran denetimi tavan davranışı, arayüz bildirimi |
| 10 Kapsam dışı adaylar | **kısmen** | Kapsam kararı verildi; NVENC yerel (`nvenc-yerel.md`, `nvenc-2.md`) ve VT CI ölçüldü. QSV/AMF ölçülmedi; macOS 15 altı (57) açık |

## 4. Kalan İş, Sıralı

"Ağır kodlama": kullanıcı PC'sinde yasak; "CI" = yalnız GitHub Actions'ta.

| Sıra | İş | Kapattığı satırlar | Dokunacağı dosyalar | Ağır kodlama | Fable |
|---|---|---|---|---|---|
| **Grup A — paralel** | | | | | |
| A1 | Filtre zinciri çekirdeği: koşullu `idet`+`bwdif`, `cropdetect` tespit + tek tık kırpma, denoise/unsharp/deblock/transpose/pad/gray/zscale/deband seçenekleri (varsayılan kapalı) | 31-39, 63 | `Core/VideoFilterChain.cs` (yeni), `Ffmpeg/InterlaceProbe.cs`, `Ffmpeg/CropProbe.cs` (yeni), `Core/FfmpegArguments.cs` (`:408-415` filtre noktası), testleri | CI (fable B9: progressive |ΔVMAF| < 0,1, taramalı negatif) | hayır (K7, K8 verili) |
| A2 | Ön ayar kütüphanesi (Core): cihaz profilleri, Discord 10 MB/Telegram/e-posta, kullanıcı ön ayarı JSON kaydet/içe/dışa | 42, 43, 44 (genişletme), 45 | `Core/PresetLibrary.cs`, `Core/Presets/*.json` (yeni), testleri | hayır | cihaz tavan değerleri için evet (kaynak seçimi) |
| A3 | `izle` alt komutu: kararlılık bekleme, döngü koruması, `.vidshrink-izle.json` | 48 | `Cli/CliRequest.cs`, `Cli/CliApp.cs`, `Core/WatchFolder.cs` (yeni), `Cli/Locales/*.json`, testleri | hayır | hayır (K9) |
| A4 | win-arm64, linux-arm64 RID'leri | 56 | `.github/workflows/release.yml` | hayır | hayır |
| A5 | CLI ürün yoluyla B1 + HB SVT-AV1 hız kolu | 1 (kanıt), 8 (kanıt), 49 | `tools/kalite-paketi-3/hb.ps1`, `docs/olcumler/handbrake-kiyas-*.md` | CI | hayır |
| **Grup B — A1 bittikten sonra** | | | | | |
| B1 | Ses/altyazı eksikleri: flac/ac3/eac3 kodlama, loudnorm/gain, harici SRT/ASS, kapak, forced önerisi, isteğe bağlı yakma | 19, 22, 23, 24, 26 | `Core/StreamMapping.cs`, `Ffmpeg/FfprobeClient.cs`, `Core/PlanCalculator.cs` (ses bütçesi), `Core/FfmpegArguments.cs` (yakma A1'in filtre noktasından) | hayır (test girdisi ≤3 sn) | hayır (K6) |
| B2 | Küçültmeye aralık, bütçe aralık süresinden | 54 | `Core/EncodePlan.cs`, `Core/PlanCalculator.cs`, `Core/FfmpegArguments.cs` | hayır | hayır — B1 ile `PlanCalculator` ortak, **B1'den sonra** |
| B3 | VT plan yoluna + VP9 küçültmeye | 7, 14, 15 | `Core/PlanParser.cs`, `Core/CodecModel.cs`, testleri | CI (macos-15 VT kıyası) | **evet** (VT XPSNR 2/6 ile kapı) — B1/B2 ile paralel |
| B4 | HDR10+/DV 8.1 passthru | 29 | `Core/HdrResolver.cs`, `Ffmpeg/FfprobeClient.cs`, `Core/FfmpegArguments.cs` HDR bölümü | CI | **evet** (kaynak, profil) — B1'den sonra (`FfprobeClient` ortak) |
| **Grup C — A2, A3, B1 sonrası** | | | | | |
| C1 | Arayüz: `Saturated` bilgi satırı, kuyruk düzenleme, klasör bırakma, bitince eylem, filtre/iz/ön ayar panelleri (gelişmiş, ana ekranda değil), 42 dil | 11, 46, 47, 59 + A1/A2/B1 yüzeyi | `MainWindow.axaml(.cs)`, `ShrinkJobWindow.axaml(.cs)`, `App/Locales/*` | hayır | hayır |
| **Açık kararlar — kodlamadan önce** | | | | | |
| D1 | Donanım bütçe doldurma sonraki adayı | 6 (%4,8) | `Core/BudgetFill.cs`, `Ffmpeg/EncodeRunner.cs` | yerel NVENC kısa ölçüm (kullanıcı onayı) | **evet** |
| D2 | Karanlık geçişin diğer rejimlere kapsamı / izleyici kararı | (bantlaşma) | `Core/DarkContentSwitch.cs` | CI | **evet** + kullanıcı |
| D3 | macOS 14 için libmpv derleme | 57 | kurucu, `release.yml` | CI | **evet** (kapsam) |
| D4 | Kapanış ölçümü `handbrake-kapanis.md` (fable B1-B10) | hepsi | `tools/`, `docs/olcumler/` | CI | hayır |

Paralel gruplar dosya kesişmesine göre: A1-A5 birbirine dokunmuyor (A5 yalnız `tools/`/`docs/`). B1→B2 ve B1→B4
sıralı; B3 bağımsız. C1 tek başına, en son.

## 5. 19 Eylül 2026 Denetimi — Yukarıdaki Sıra Nerede

§4'ün tablosu 17 Eylül'ün fotoğrafıydı; iki günde dalgalar girdi ve tablo bayatladı. Aşağıdaki
satırlar tabloyu silmiyor, üstüne bugünkü ölçümü yazıyor. Her satır kodda okundu; "yapılmadı"
diyen satır da bir dosya ve satır numarası gösteriyor.

| İş | Bugün | Kanıt |
|---|---|---|
| A1 filtre zinciri | **girdi** | `VideoFilterChainTests` / `FiltreYoklamaTests`, `docs/olcumler/handbrake-filtre.md` |
| A2 ön ayar kütüphanesi | **girdi** | `Core/PresetLibrary.cs`, `Core/Presets/platformlar.json`, `OnAyarKutuphanesiTests` |
| A3 `izle` | **girdi** | `Core/WatchFolder.cs`, `WatchFolderTests` |
| A5 CLI ürün yolu + HB SVT hız kolu | **girdi** | `hb.ps1:1610` `urun-cli`, `:1832` svt kolu, `docs/olcumler/handbrake-kiyas-cli.md`, koşum 35248903679, birleşme `ef8e6931` |
| B1a ac3/eac3 **kodlama** | **girdi** (19 Eylül, `8f09dc60`) | `Core/PlanParser.cs:14` `AllowedAudioCodecs` artık `ac3, eac3` taşıyor; `LanguageTests` marka adı izinlisi |
| B1a flac **küçültmede** | **dalda** (22 Eylül, `worktree-agent-ac648426fe8f56b4a`, birleşmedi) | `AllowedAudioCodecs` flac taşıyor; bütçeye 16 bit tavanla girer (`StreamMapping.FlacCeilingK`), sığmazsa `FlacFellBack`; CLI `--ses-kodek`; `FlacSesTests` |
| B1b loudnorm / gain | **dalda** (aynı dal) | `StreamMapping.AudioFilter` (`loudnorm=I=-24:LRA=7:TP=-2`, `volume=NdB`), istenince ses kopyalanmaz; CLI `--ses-normal`, `--ses-kazanc`; `SesNormallestirmeTests` |
| B1c harici SRT/ASS | **dalda** (aynı dal) | `ExternalSubtitle` ek `-i` girdisi, MP4 mov_text / MKV kopya / WebM webvtt; CLI `--altyazi`, `--yan-altyazi`; `DisAltyaziTests`. Arayüz yüzü yok |
| B1d kapak resmi | **dalda** (aynı dal), yalnız MP4 | `CoverMap` + `-disposition:v:1 attached_pic`, bayt bütçede; MKV/MOV ölçüldü, taşımıyor; `KapakResmiTests` |
| B1e forced | **dalda** (aynı dal) | `StreamMapping.DefaultForced`: varsayılansız çıktıda forced iz varsayılan olur; `ForcedAltyaziTests`. Foreign Audio Search yok |
| B1f yakma | **dalda**, yalnız metin altyazı | `VideoFilterChain.BurnFilter` (`subtitles=...:si=N`), CLI `--yak N`; PGS overlay ister, reddedilir; `AltyaziYakmaTests` |
| B2 küçültmeye aralık | denetlenmedi | bu turda okunmadı |
| B3 VT plan yolu / VP9 küçültme | VP9 **girdi (23 Eylül)**; VT **ölçüldü, kapıdan kaldı** (23 Eylül denetimi) | VT: K2 2/8, K4 5/8, bağlantı geri alındı, kapı `PlanParserTests.ParserStillRejectsVideoToolboxEncoders` ile kapalı (`docs/olcumler/videotoolbox-hizli.md`). VP9: `-b:v` + `-pass 1/2`, `-deadline good -cpu-used 4 -row-mt 1`, kap WebM, ses opus; WebM'in taşımadığı iz `WebmStreamDropped` notuyla düşer; HandBrake `av_webm`/`VP9` artık gerçek kol (kilit `libvpx-vp9`). cpu-used ve vp9 CRF ölçeği ölçülmedi. `Vp9KucultmeTests`, mutasyonlar `docs/olcumler/vp9-kucultme-mutasyonlar.md` |
| B4 HDR10+/DV | **girdi (23 Eylül)** | DV 8.1 x265/SVT-AV1'de taşınıyor, MP4'te `-strict unofficial`; HDR10+ ve taşınamayan DV gerekçeye düşüyor; `HdrDinamikTests`, ölçüm `docs/olcumler/b4-hdr-dinamik.md` |
| C1-1 `Saturated` satırı | **girdi (22 Eylül)** | `MainWindow.SaturatedSuffix`, `ShrinkJobWindow.BittiSatiri`; `main.run.saturated` 42 dilde; `DoygunTeslimTests` 4/4, iki mutasyon kırmızı |
| C1-2 kuyruk düzenleme | **girdi (22 Eylül)** | bekleyen listesi, yukarı/aşağı/çıkar, sırayı duraklat (`ShrinkJobWindow`, `KuyrukDuzenlemeTests`) |
| C1-3 klasör bırakma | girdi (22 Eylül) | klasör ya da çoklu video kuyruk penceresine, pencerenin seçenekleriyle (`Core/DroppedMedia`, `KlasorBirakmaTests`) |
| C1-4 "bitince" eylemi | **girdi (22 Eylül)** | hiçbir şey / klasörü aç / uyut / kapat; uyut ve kapat 60 sn geri sayımlı, vazgeçilebilir (`QueueEndActions.cs`) |
| C1-5 filtre paneli | **girdi (22 Eylül)** | Gelişmiş'te açılır kutular ve onay kutuları, metin tek kaynak (`VideoFilterChain.Format`, `Parse`'ın tersi); `SuzgecPaneliTests` (7a6286e6) |
| C1-6 iz paneli | **girdi (22 Eylül)** | ses yüksekliği, kazanç, dış altyazı, metin altyazı yakma ana pencerede; yeni kaynakta sıfırlanır, kuyruğa taşınmaz (`MainWindow.Izler.cs`, `IzPaneliTests` 12/12, 7a6286e6) |
| C1-7 ön ayar yüzeyi | **girdi (22 Eylül)** | içe/dışa aktarma pencerede, HandBrake dosyası özetle (0666989b); CLI `--profil-dosyasi` (d17e0e3c) |
| D2 karanlık geçişin kapsamı | genişletilmedi | `Core/DarkContentSwitch.cs:21-27` hâlâ Auto + Aggressive/Extreme + libsvtav1 |
| D3 macOS 14 libmpv | **girdi** | `install-vidshrink.sh:298-305`, `tools/mpvkit-macos/mpvkit-1.0.0.lock`, `.github/workflows/macos-mpvkit.yml:106-159` |

Fable'ın on kararı 17 Eylül'de geldi ve `docs/handbrake/fable-kararlar-2026-09-17.md`'de duruyor;
"fable'da bekliyor" diyen her not o tarihten itibaren yanlıştır.
