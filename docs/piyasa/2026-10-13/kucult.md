# Küçült Sekmesi, Karşılaştırma Paneli Ve İş Kuyruğu — Pazar Notları

13 Ekim 2026. Kod okuması `origin/main` 9de48d8b. Eksik tablosu aynı klasörde `kucult-eksikler.md` (46 satır, önek `SIK`).

Kısa sözlük, ilk geçtikleri yer burası:

- **VMAF:** Netflix'in geliştirdiği, görüntü kalitesini 0–100 arası puanlayan ölçüt. VMAF-NEG onun keskinleştirme hilesine kanmayan türü.
- **CRF:** sabit kalite ayarı; sayı küçüldükçe kalite de dosya da büyür.
- **HDR10+ ve Dolby Vision:** HDR videoda parlaklığı sahne sahne tarif eden ek bilgi (üst veri). Kodlamada taşınmazsa video düz HDR10'a düşer.
- **Ton eşleme:** HDR görüntüyü normal (SDR) ekrana uyacak renklere çevirme.
- **VVC (H.266):** HEVC'nin ardılı olan yeni kodek. **SVT-AV1:** Intel ve Netflix'in açık kaynak AV1 kodlayıcısı; "çatal" onun başkalarınca değiştirilmiş türevi.
- **İzleme klasörü:** içine dosya düşünce kendiliğinden kuyruğa alan klasör.

## Envanter

Her satır `origin/main` 9de48d8b üzerinde okundu.

### Hedef Ve Kalite

- Hedef boyut kaydırıcısı ve hazır yongalar (8 MB, WhatsApp, arşiv, yarı boyut, 25, 100, 134, 180 MB): `src/VidShrink.App/MainWindow.axaml:344`, `:372`, `:379`. Kullanıcı kendi yongasını ekler: `:432`.
- Platform ön ayarları `src/VidShrink.Core/Presets/platformlar.json` içinde: Discord 20 / 50 / 500 MB, Telegram 2000 MB, Gmail 25 MB, Outlook 20 MB, WhatsApp 16 ve 180 MB.
- Hedef boyutu tutturma yöntemi: örnek kodlamalarla iki turluk kalibrasyon (`src/VidShrink.Ffmpeg/ShrinkEngine.cs:9`, `:29`), isteğe bağlı iki geçiş (`:77`, arayüzde `MainWindow.axaml:969`), bütçeyi doldurma seçeneği (`MainWindow.axaml:789`).
- Hedef aşılırsa dört yol: yeniden dene, sondan ya da baştan kırp, kabul et, dur (`MainWindow.axaml:668`).
- Hedef kalite kaydırıcısı (varsayılan 60): `MainWindow.axaml:481`. Çekirdeği `src/VidShrink.Core/PlanCalculator.cs:1170` (`TargetMbForQuality`, ikiye bölerek arar); kaynak tavanı ve taban uyarısı `PlanCalculator.cs:211`.
- Kalite tahmini ölçüme dayanıyor: her dosya yüklenince örnek pencereler kodlanıp VMAF ile ölçülüyor (`src/VidShrink.App/MainWindow.axaml.cs:3455`, `:3639`; `src/VidShrink.Ffmpeg/ShrinkEngine.cs:11`; `src/VidShrink.Ffmpeg/ComplexityProbe.cs:70`). Biten çıktının tamamı ölçülmüyor.
- Kalite ölçütleri: VMAF-NEG, XPSNR, SSIM; dört özet (ortalama, harmonik, en kötü yüzde 10, en düşük): `src/VidShrink.Ffmpeg/QualityMeter.cs:298`, `:236`, `:240`, `:252`.
- Plan paneli ve gerekçeler: `MainWindow.axaml:539`, `:565`. Kalite satırları (ölçülmüş ya da tahmini, hedef, öngörülen puan, kayıp): `MainWindow.axaml.cs:3877`.
- Süre ve boyut tahmini: `MainWindow.axaml:612`, `:632`; süre tahmini yalnız plana uyan ölçülmüş hız varsa verilir (`src/VidShrink.Core/ComplexityProfile.cs:368`), boyut tahmini alt ve üst bantla (`ComplexityProfile.cs:393`).

### Kodek, Donanım Ve HDR

- 21 video kodlayıcı: x264, x265, SVT-AV1, VP9 ve NVENC, QSV, AMF, Media Foundation, VideoToolbox, VAAPI donanım yolları (`src/VidShrink.Core/PlanParser.cs:12`). Ses: AAC, Opus, MP3, AC3, E-AC3, FLAC, kopya (`PlanParser.cs:13`).
- Kodlayıcı gerçekten çalışıyor mu diye yoklama: `src/VidShrink.Ffmpeg/EncoderCapabilities.cs:110`, `:236`. Donanımla çözme: `src/VidShrink.Core/FfmpegArguments.cs:521`.
- Otomatik kodek, hızlı donanım yolu, kodek kilidi: `MainWindow.axaml:781`, `:793`, `:1006`, `:1013`.
- HDR koruma seçeneği: `MainWindow.axaml:802`. HDR10 yazılım kodekleri `src/VidShrink.Core/HdrResolver.cs:46`; ton eşleme işlemcide (`HdrResolver.cs:48`).
- HDR10+ yalnız libx265 ile taşınıyor (`HdrResolver.cs:107`); SVT-AV1'de düşüyor ve işaretleniyor (`HdrResolver.cs:114`, `:115`).
- Dolby Vision yalnız profil 8.1 ve yalnız libx265 ile libsvtav1'de taşınıyor (`HdrResolver.cs:54`, `:179`). Ölçümü `docs/olcumler/b4-hdr-dinamik.md`.

### Süzgeçler, Ses Ve Altyazı

- Çözünürlük ve kare hızı düşürme: `MainWindow.axaml:919`, `:920`; ölçekleme hep lanczos (`src/VidShrink.Core/VideoFilterChain.cs:338`), kare hızı kare atarak (`VideoFilterChain.cs:351`).
- Gürültü azaltma, bant giderme, otomatik siyah bant kırpma, serbest süzgeç kutusu: `MainWindow.axaml:1032`, `:1065`, `:1067`, `:1071`.
- Ses kodeği, ses yüksekliğini eşitleme, bütün izleri koruma, üst veriyi silme: `MainWindow.axaml:842`, `:850`, `:860`, `:861`.
- Dış altyazı ekleme (çoklu seçim) ve altyazı yakma: `MainWindow.axaml:869`, `:874`; `src/VidShrink.App/MainWindow.Izler.cs:177`; `VideoFilterChain.cs:83`, `:205`. Tek altyazı yakılabilir (`VideoFilterChain.cs:214`); yazı tipi ve boyut ayarı yok.

### Önizleme Ve Karşılaştırma

- Karşılaştırma paneli: `MainWindow.axaml:523`. Bölme çizgisi, klavyeyle kaydırma, yakınlaştırma: `src/VidShrink.App/Playback/ComparisonPanel.axaml.cs:286`, `:382`, `:433`.
- Kısa örnek kodlama: seçilen noktadan 5 saniye gerçek planla kodlanır (`src/VidShrink.Core/PreviewSegment.cs:48`, `:139`).
- Yan yana, fark, yanıp sönme kipleri ve ölçüt bindirme yok (aranan adlar: `SideBySide`, `YanYana`, `Flicker`, `Blink`, `Difference`, `blend=`).

### İş Kuyruğu

- Kuyruk çekirdeği: ekle, çıkar, sırasını değiştir, iptal et, hepsini iptal et (`src/VidShrink.Core/JobPump.cs:60`, `:66`, `:76`, `:88`, `:102`). Arayüzde yukarı ve aşağı düğmeleri: `src/VidShrink.App/ShrinkJobWindow.axaml.cs:424`.
- Eşzamanlı kodlama (K6): varsayılan 1, tavan 4, donanım kodlayıcıda 2 yuva (`src/VidShrink.Core/ParallelJobs.cs:9`, `:12`, `:19`).
- Duraklatma yalnız kuyruğu durdurur; koşan dosya biter, sıradaki başlamaz (`ShrinkJobWindow.axaml.cs:390`).
- İlerleme: iş başına çubuk ve kalan süre (`ShrinkJobWindow.axaml.cs:146`, `:529`); genel çubuk koşan işlerin ortalaması, genel kalan süre yalnız tek iş koşarken (`:540`, `:549`).
- Bitince eylem: hiçbir şey, klasörü aç, uyut, kapat (`src/VidShrink.App/QueueEndActions.cs:9`); geri sayım ve vazgeç var (`ShrinkJobWindow.axaml.cs:611`).
- Çıktı önce kısmi adla yazılır, bitince asıl ada taşınır (`src/VidShrink.Ffmpeg/EncodeRunner.cs:463`). Disk alanı kodlamadan önce bir kez denetlenir (`src/VidShrink.Ffmpeg/DiskSpaceGuard.cs:27`, `MainWindow.axaml.cs:4514`).
- Tanı günlüğü her koşumu yazar, Ayarlar'dan açılır (`src/VidShrink.Core/Gunluk.cs:16`; anahtar `settings.log.open`).

### Adlandırma Ve Ön Ayar

- Çıktı adı deseni, varsayılan `{ad}_shrunk`, altı yer tutucu (ad, hedef, kalite, çözünürlük, kodek, tarih): `src/VidShrink.Core/AdlandirmaDeseni.cs:18`, `:21`.
- Ön ayar kaydet, içe ve dışa aktar, HandBrake ön ayarını içe al: `src/VidShrink.Core/PresetLibrary.cs:220`, `:228`, `:241`, `:347`; düğmeler `MainWindow.axaml:447`, `:450`.

### Önceki Taramanın Kalemleri (Bugünkü Durum)

`docs/piyasa/tarama-2026-10-05.md` "Karar Gerektirmeyenler" tablosundan alanıma düşen dört kalem:

- **Kuyrukta izleme klasörü: var.** `src/VidShrink.Core/WatchFolder.cs:62` (2 saniyede bir yoklar, dosya boyutu iki kez aynı kalınca alır, `:72`, `:232`), `src/VidShrink.App/QueueWatch.cs:13`, arayüz `src/VidShrink.App/ShrinkJobWindow.axaml:130`. Alt klasörleri taramıyor (SIK-27).
- **İş bitince bildirim: yarım.** `src/VidShrink.App/IsBittiBildirimi.cs:31`; yalnız Windows (`:64`, `:97`). Defterde "Win32 çağrıları hiç koşmadı" notu duruyor (`.claude/acik.md:25`). macOS ve Linux eksiği SIK-23.
- **Üst veriyi silme: var.** `MainWindow.axaml:861`; ön ayara da yazılıyor (`src/VidShrink.App/MainWindow.OnAyar.cs:176`).
- **Dosya seçicide çoklu seçim: var.** `MainWindow.axaml.cs:3267`, `:3285`; birden çok dosya kuyruk penceresine gider. Defterde "gerçek seçici denenmedi" notu duruyor (`.claude/acik.md:30`). Klasör bırakma reddediliyor (`MainWindow.axaml.cs:3378`, SIK-28).

### Kodda Bulunmayanlar (Aranan Adlar)

Her biri en az iki adla arandı, eşleşme çıkmadı:

- Koşan süreci askıya alma: `NtSuspendProcess`, `SuspendThread`, `SIGSTOP`, `PauseEncode`.
- Kodlama önceliği: `PriorityClass`, `BelowNormal`, `ProcessPriority`, `ionice`, `renice` (`EncodeRunner.cs`, `FfmpegRunner.cs` ve `ShrinkEngine.cs` içinde yok). Yardımcı süreçler düşük öncelikte açılıyor (`ThumbnailQueue.cs:276`, `SceneDetector.cs:123`, `AudioLevelSource.cs:88`, `SilenceCut.cs:215`); asıl kodlama süreci açılmıyor. `ChildJob.cs:71`'deki `PriorityClass` yalnız bir yapı alanı: iş nesnesi tek bayrak kuruyor, o da "kapanınca öldür" (`ChildJob.cs:33`). İş parçacığı sınırı: `-threads` yalnız ses ölçümlerinde.
- Kuyruğu saklama: `SaveQueue`, `RestoreQueue`, `queue.json`, `KuyrukKaydet`. Zamanlama: `StartLater`, `SonraBaslat`, `ScheduledStart`.
- Uykuyu engelleme: `SetThreadExecutionState`, `PowerCreateRequest`, `PreventSleep`, `IOPMAssertion`, `caffeinate`, `systemd-inhibit`, `UyanikTut`, `Inhibit`.
- Özel komut, uzak bildirim, pil: `PostCommand`, `Webhook`, `SmtpClient`, `ntfy`, `PowerLineStatus`, `OnBattery`.
- Yeniden deneme ve iş düzenleme: `RetryFailed`, `Requeue`, `EditPending`, `IsiDuzenle`.
- Kaynağı silme ve tarih koruma: `DeleteSource`, `KaynagiSil`, `RecycleBin`, `SetLastWriteTime`, `TarihiKoru`.
- Çıktıyı doğrulama ve çıktı ölçümü: `VerifyOutput`, `CiktiDogrula`, `MeasureOutput`, `FinalQuality`, `OlculenKalite`.
- Kodek ve HDR: `vvc`, `vvenc`, `h266`; `svt-av1-psy`, `svt-av1-hdr`; `film-grain`, `photon`; `libplacebo`, `tonemap_opencl`.
- Dış ses, görsel kırpma, kare üretimi, ek ölçütler: `ExternalAudio`, `DisSes`, `ReplaceAudio`; `CropOverlay`, `ElleKirp`; `minterpolate`; `ssimulacra`, `butteraugli`, `cambi`.

## Rakipler

26 rakibin en az bir resmi kaynağı okundu. Sürümler `gh api .../releases/latest` ya da resmi sayfadan.

| # | Ad | Sürüm | Tarih | Tür | URL |
|---|---|---|---|---|---|
| 1 | HandBrake | 1.11.2 | 2026-06-07 | açık kaynak, masaüstü | https://github.com/HandBrake/HandBrake |
| 2 | VidCoder | 12.23 (beta 13.12) | 2026-07-19 (beta 2026-09-25) | açık kaynak, Windows | https://vidcoder.net/ |
| 3 | Shutter Encoder | 20.4 | 2026-09-28 | ücretsiz, masaüstü | https://www.shutterencoder.com/ |
| 4 | StaxRip | 2.52.6 | 2026-09-27 | açık kaynak, Windows | https://github.com/staxrip/staxrip |
| 5 | FFmpeg Batch AV Converter | 3.2.9 | 2026-05-27 | açık kaynak, Windows | https://github.com/eibol/ffmpeg_batch |
| 6 | FastFlix | 6.2.1 | 2026-03-21 | açık kaynak, masaüstü | https://github.com/cdgriffith/FastFlix |
| 7 | Av1an | 0.5.2 | 2026-01-04 | açık kaynak, komut satırı | https://github.com/rust-av/Av1an |
| 8 | ab-av1 | 0.11.7 | 2026-08-21 | açık kaynak, komut satırı | https://github.com/alexheretic/ab-av1 |
| 9 | Hybrid | rev 2026.10.09.1 | 2026-10-09 (sürüm adından) | ücretsiz, masaüstü (yalnız sürüm okundu) | https://www.selur.de/downloads |
| 10 | XMedia Recode | 3.6.4.1 | 2026-09-26 | ücretsiz, Windows | https://www.xmedia-recode.de/en/ |
| 11 | Compresto | 1.31 | sayfada yok | ücretli, macOS | https://compresto.app/ |
| 12 | Permute | 3 (yapı 4018); Permute 4 macOS 26 ister | sayfada yok | ücretli, macOS (yalnız lisans bilgisi okundu) | https://software.charliemonroe.net/permute/ |
| 13 | CompressO | 3.0.0 | 2026-04-06 | açık kaynak, masaüstü | https://github.com/codeforreal1/compressO |
| 14 | 8mb.video | sürümsüz | — | web, "Discord sıkıştırıcı" | https://8mb.video/ |
| 15 | vidcord | 7.5 | 2026-09-30 | açık kaynak, "Discord sıkıştırıcı" | https://github.com/cyroz1/vidcord |
| 16 | Constrict | 25.12.1 | 2025-12-27 | açık kaynak, Linux | https://github.com/Wartybix/Constrict |
| 17 | FreeConvert | sürümsüz | — | web | https://www.freeconvert.com/video-compressor |
| 18 | Tdarr | doğrulanmadı | — | ücretsiz, sunucu otomasyonu | https://github.com/HaveAGitGat/Tdarr |
| 19 | Unmanic | 0.4.1 | 2026-08-14 | açık kaynak, sunucu otomasyonu | https://github.com/Unmanic/unmanic |
| 20 | FileFlows | doğrulanmadı | — | ücretsiz katmanlı, sunucu otomasyonu | https://fileflows.com/ |
| 21 | Wondershare UniConverter | doğrulanmadı | — | ücretli, masaüstü | https://videoconverter.wondershare.com/compress/ |
| 22 | Movavi Video Converter | doğrulanmadı | — | ücretli, masaüstü | https://www.movavi.com/support/how-to/video-compression.html |
| 23 | Adobe Media Encoder (listede yoktu) | doğrulanmadı | — | ücretli, masaüstü | https://helpx.adobe.com/media-encoder/using/export-settings-reference.html |
| 24 | NotEnoughAV1Encodes (listede yoktu) | doğrulanmadı | — | açık kaynak, Windows | https://github.com/Alkl58/NotEnoughAV1Encodes |
| 25 | video-compare (listede yoktu) | doğrulanmadı | — | açık kaynak, karşılaştırma aracı | https://github.com/pixop/video-compare |
| 26 | macOS Finder ve QuickTime | işletim sistemiyle | — | işletim sistemi aracı | https://support.apple.com/en-us/116944 |

Taranamayan dört rakip: Clipchamp, CloudConvert, VEED (süre yetmedi) ve Video Compressor Panda (mağaza sayfası boş döndü).

Yardımcı kitaplıklar: VMAF 3.2.1 (2026-09-14), dovi_tool 2.3.4 (2026-09-10), hdr10plus_tool 1.7.2 (2025-12-27), vvenc 1.14.0 (2026-01-21), svt-av1-psy 3.0.2 (2025-04-20, son sürümü bu), svt-av1-psyex 3.0.2-B (2025-09-27), svt-av1-hdr yapısı 2026-09-29.

## Özellik Matrisi

`var` resmi kaynakta görüldü. `?` sayfada bulunamadı, yani doğrulanmadı; "yok" demek değildir. `yok` yalnız rakibin kendi takipçisindeki istekle kanıtlıysa yazıldı.

| Özellik | Biz | HandBrake | VidCoder | Shutter Encoder | FastFlix | FFmpeg Batch | Diğer |
|---|---|---|---|---|---|---|---|
| Hedef boyut (MB) | var | yok (istek #2483 kapatıldı) | var | var | yok (istek #234 açık) | var | Constrict, vidcord, 8mb.video, UniConverter, Movavi, Adobe Media Encoder |
| Ölçümle hedef kalite | yarım | yok (istek #2523 açık) | ? | ? | ? | ? | ab-av1, Av1an, FileFlows |
| Kısa örnek kodlama | var | ? | var | var | ? | var | Movavi |
| Bölme çizgili karşılaştırma | var | ? | ? | ? | ? | ? | CompressO |
| Yan yana ve fark kipi | yok | yok (istek #5023 açık) | ? | ? | ? | ? | video-compare |
| Çıktının VMAF puanı | yarım | yok (istek #2523 açık) | ? | var | ? | ? | ab-av1 |
| Koşan kodlamayı duraklatma | yarım | var | var | var | ? | var | — |
| Süreç önceliği | yok | var | ? | ? | var | var | — |
| Eşzamanlı kodlama | var | ? (istek #1445 açık, 15 oy) | var | var | ? | ? | vidcord, Adobe Media Encoder |
| İzleme klasörü | var | ? (istekler kapatıldı) | var | var | ? | ? | Compresto, Tdarr, Unmanic, FileFlows |
| Kuyruğu saklama | yok | var | ? | ? | var | ? | — |
| Zamanlanmış başlatma | yok | var | ? | ? | ? | ? | Tdarr, FileFlows |
| Bitince eylem | var (dört seçenek) | var (özel komut dahil) | ? | var | var (çıkış dahil) | ? | — |
| Uykuyu engelleme | yok | var | ? | var (kullanıcı bildirimi) | ? | ? | — |
| VVC (H.266) | yok | ? | ? | var | var | var | StaxRip |
| Dolby Vision | yarım (8.1) | var (5, 7→8.1, 8.1, 8.4) | ? | ? | var (ayrı kodlayıcılarla; kendi notunda denenmemiş) | ? | StaxRip |
| HDR10+ | yarım (x265) | var | ? | ? | var | ? | — |
| Dış ses izi | yok | yok (istek #871 açık, 62 oy) | ? | var | ? | var | — |
| Yakılan altyazıda biçim | yok | yok (istek #146 açık, 40 oy) | yok (istek #1212 açık) | ? | ? | ? | — |
| Görsel elle kırpma | yarım (otomatik) | ? | ? | ? | var | var | — |
| Adlandırma şablonu | yarım (6 yer tutucu) | var | ? | var (önek, sonek) | var (23 değişken) | ? | StaxRip |
| Ön ayar kaydetme | var | ? | ? | ? | ? | ? | FreeConvert |

### Hedef Boyut Kimde, Hangi Yöntemle

| Araç | Yöntem | Kaynak |
|---|---|---|
| VidShrink | Örnek kodlamayla iki tur kalibrasyon, isteğe bağlı iki geçiş, aşımda yeniden dene, kırp ya da kabul et | `ShrinkEngine.cs:9`, `MainWindow.axaml:668` |
| VidCoder | Hedef bit hızı, boyut ya da kalite seçilir; iki açık hata hedefin tutmadığını söylüyor (#857, #1090) | [8][9] |
| Shutter Encoder | "File size" alanına MB yazılır; belge boyutun tam tutmayabileceğini söyler | [10][11] |
| FFmpeg Batch AV Converter | "Two pass / Target size" sihirbazı | [17][18] |
| Constrict | Hedefe göre bit hızını, çözünürlüğü, kare hızını ve ses kalitesini kendi seçer | [22] |
| vidcord | 20, 50, 100, 500 MB ve 1 GB hazır hedef | [23] |
| 8mb.video | 8, 10, 20, 25, 50, 100 MB; özel hedef en çok 200 MB | [31] |
| FreeConvert | Hedef boyut yüzde olarak; ücretsiz yükleme en çok 1 GB | [30] |
| UniConverter, Movavi, Adobe Media Encoder | Boyut sınırı alanı ya da kaydırıcısı | [35][36][37] |
| HandBrake, FastFlix | Yok; ikisinde de kullanıcı isteği var | [6][16] |

Hedef boyut bizim en güçlü yanımız: rakiplerin çoğu tek bit hızı hesabı yapıyor, aşımı yönetmiyor.

### Platform Sınırları: Kodumuz Ve Resmi Değer

| Platform | Kodumuzda | Resmi Değer (13 Ekim 2026) | Durum | Kaynak |
|---|---|---|---|---|
| Discord ücretsiz | 20 MB | 20 MB | uyumlu; resmi sayfa açılmadı (403), değer arama özeti ve iki haberden | [42][43][44] |
| Discord Nitro Basic | 50 MB | 50 MB | uyumlu, aynı kaynak notu | [42] |
| Discord Nitro | 500 MB | 500 MB | uyumlu, aynı kaynak notu | [42] |
| Telegram | 2000 MB | 2 GB; Premium 4 GB | uyumlu; Premium için hazır hedef yok (SIK-44) | [39] |
| Gmail | 25 MB | 25 MB | uyumlu | [40] |
| Outlook | 20 MB | 20 MB; Exchange hesabında varsayılan 10 MB | uyumlu; Exchange ayrımı yok | [41] |
| WhatsApp sohbet | 16 MB | çelişkili: bir yardım sayfası 16 MB, masaüstü rehberi hızlı bağlantıda 100 MB ve 720p, yavaşta 64 MB ve 480p | doğrulanamadı | [45][46] |
| WhatsApp Web | 180 MB | resmi sayfada bulunamadı; belge olarak gönderim 2 GB | doğrulanamadı | [47] |

WhatsApp'ın iki değeri de kodda kaynak durumu "Code" olarak işaretli, yani resmi sayfaya değil gözleme dayanıyor.

### Öncelik Kararlarında Kurala Göre Sapmalar

- **VVC (SIK-16)** dört rakipte belgeli, kurala göre 1 olurdu. 2 verdim: bugün çok az aygıt oynatıyor, ana işin kenarında kalıyor.
- **Ölçümle hedef kalite (SIK-15)** üç rakipte var, 2 verdim: üçü de meraklı ya da sunucu aracı, bizde yarısı zaten koşuyor.
- **Sahne bazlı parçalı kodlama (SIK-30)** iki rakipte var, 3 verdim: ikisi de AV1 meraklısı aracı, kitle dar.
- **Yakılan altyazıda biçim (SIK-04)** 40 oyla kanıtlı ama hangi rakipte olduğu doğrulanmadı; iki kaynak şartı tutmadığı için 2.

## Kullanıcı Talepleri

Oy, isteğe verilen başparmak sayısı. 13 Ekim 2026'da `gh api` ile okundu.

| İstek | Araç | Oy | Durum | Bizde | URL |
|---|---|---|---|---|---|
| Dış ses izi ekleme | HandBrake | 62 | açık | yok (SIK-03) | https://github.com/HandBrake/HandBrake/issues/871 |
| Altyazıda yazı tipi, boyut, arka plan | HandBrake | 40 | açık | yok (SIK-04) | https://github.com/HandBrake/HandBrake/issues/146 |
| Kodlamayı gecikmeli başlatma | HandBrake | 19 | açık | yok (SIK-07) | https://github.com/HandBrake/HandBrake/issues/586 |
| Tarih üst verisini taşıma | HandBrake | 17 | açık | yarım (SIK-11) | https://github.com/HandBrake/HandBrake/issues/3588 |
| Arayüzden aynı anda birden çok kodlama | HandBrake | 15 | açık | var (K6) | https://github.com/HandBrake/HandBrake/issues/1445 |
| Dış altyazı kaynağı olarak VobSub | HandBrake | 13 | açık | kodda aranmadı | https://github.com/HandBrake/HandBrake/issues/629 |
| Ses izlerini kendiliğinden adlandırma | HandBrake | 12 | açık | kodda aranmadı | https://github.com/HandBrake/HandBrake/issues/2593 |
| FFmpeg süzgeçlerini açma | HandBrake | 11 | açık | var (serbest süzgeç kutusu) | https://github.com/HandBrake/HandBrake/issues/1993 |
| LUT desteği | HandBrake | 10 | açık | kapalı karar K8 (SIK-46) | https://github.com/HandBrake/HandBrake/issues/1348 |
| Intel QuickSync ile VP9 | HandBrake | 9 | açık | yok, tabloya alınmadı (tek istek) | https://github.com/HandBrake/HandBrake/issues/1743 |
| Ses ve altyazı izine dil etiketi | HandBrake | 9 | açık | kodda aranmadı | https://github.com/HandBrake/HandBrake/issues/2784 |
| Saniye aralığı seçimini iyileştirme | HandBrake | 9 | açık | kodda aranmadı | https://github.com/HandBrake/HandBrake/issues/3373 |
| Ses kaydırma (gecikme) | HandBrake | 8 | açık | kodda aranmadı | https://github.com/HandBrake/HandBrake/issues/2500 |
| Dış altyazıyı kendiliğinden bulup ekleme | HandBrake | 7 | açık | yarım (elle ekleme var) | https://github.com/HandBrake/HandBrake/issues/719 |
| VTT altyazı yakma | HandBrake | 6 | açık | kodda aranmadı | https://github.com/HandBrake/HandBrake/issues/4379 |
| Apple uzamsal video | HandBrake | 6 | açık | defterde (`.claude/acik.md:2`) | https://github.com/HandBrake/HandBrake/issues/5710 |
| Hedef boyutu geri getirme | HandBrake | 6 | kapalı | var, ana işimiz | https://github.com/HandBrake/HandBrake/issues/2483 |
| VMAF | HandBrake | 5 | açık | yarım (SIK-14, SIK-15) | https://github.com/HandBrake/HandBrake/issues/2523 |
| Kanal sayısına göre ses bit hızı | HandBrake | 5 | açık | kodda aranmadı | https://github.com/HandBrake/HandBrake/issues/633 |
| Kuyruğu açılışta hep geri yükleme | HandBrake | 1 | kapalı | yok (SIK-05) | https://github.com/HandBrake/HandBrake/issues/4652 |
| Kullanılan çekirdek sayısını sınırlama | HandBrake | 1 | kapalı | yok (SIK-20) | https://github.com/HandBrake/HandBrake/issues/5465 |
| Kodlayıcıyı düşük öncelikle başlatma | HandBrake | 0 | kapalı | yok (SIK-02) | https://github.com/HandBrake/HandBrake/issues/6540 |
| Büyüteçli yan yana karşılaştırma | HandBrake | 0 | açık | yok (SIK-31) | https://github.com/HandBrake/HandBrake/issues/5023 |
| Kodlama sonrası VMAF puanı | HandBrake | okunmadı | kapalı (2026-07-17) | yarım (SIK-14) | https://github.com/HandBrake/HandBrake/issues/7985 |
| 30'dan 24 kareye yalnız kare atarak geçme | Shutter Encoder | 2 | açık | var | https://github.com/paulpacifico/shutter-encoder/issues/134 |
| Ekranı açık tutmayı kapatma | Shutter Encoder | 1 | açık | ters yönden SIK-06 | https://github.com/paulpacifico/shutter-encoder/issues/302 |
| Üst veriyi silme | Shutter Encoder | 1 | açık | var | https://github.com/paulpacifico/shutter-encoder/issues/313 |
| AV1 QuickSync | Shutter Encoder | 1 | açık | var | https://github.com/paulpacifico/shutter-encoder/issues/138 |
| En büyük dosya boyutu seçeneği | FastFlix | 1 | açık | var | https://github.com/cdgriffith/FastFlix/issues/234 |
| HDR10'dan HLG'ye çevirme | FastFlix | 1 | açık | kodda aranmadı | https://github.com/cdgriffith/FastFlix/issues/347 |
| Sürdürülebilir kodlama için parçalara bölme | FastFlix | 0 | açık | yok (SIK-29) | https://github.com/cdgriffith/FastFlix/issues/243 |
| NVENC ile hedef boyut 30 kare üstünde hep düşüyor | VidCoder | 1 | açık | hata bildirimi; bizde aşım paneli var | https://github.com/RandomEngy/VidCoder/issues/857 |
| Hedef boyut 150 MB aşıldı | VidCoder | 1 | açık | hata bildirimi; bizde aşım paneli var | https://github.com/RandomEngy/VidCoder/issues/1090 |
| HDR ve Dolby Vision'dan SDR'ye | VidCoder | 1 | açık | var (ton eşleme) | https://github.com/RandomEngy/VidCoder/issues/1154 |
| Altyazı yazı boyutu | VidCoder | 1 | açık | yok (SIK-04) | https://github.com/RandomEngy/VidCoder/issues/1212 |

Okunan resim: HandBrake kullanıcısının en çok istediği iki şey ses ve altyazıyla ilgili (62 ve 40 oy). Kuyruk istekleri az oy alıyor çünkü HandBrake çoğunu son iki yılda kendisi ekledi.

## Son İki Yılın Yenilikleri

Ekim 2024 – Ekim 2026. Daha eski satırlar tarihiyle işaretli.

**Kuyruk ve otomasyon.** Pazarın en çok hareket ettiği yer burası.

- HandBrake 1.9.0 (2024-12-01): "Start Later" (gecikmeli başlatma) ve pilde duraklatma ayarının düzeltmesi. Pilde duraklatmanın kendisi 1.8.0 ile geldi (2024-05-20, iki yıldan biraz eski). 1.10.0 (2025-08-09): bitince özel komut, yeni adlandırma değişkenleri. 1.11.0 (2026-03-08): bildirim sesi uygulama öndeyken de çalıyor. 1.11.2 (2026-06-07): disk dolarken kuyruğu duraklatmanın iyileştirilmesi [1].
- Shutter Encoder 18.9 (2025-04-03): kuyrukta paralel koşu, izleme klasöründe alt klasör [10].
- FastFlix 6.0.0 (2026-02-10): kuyruğu güvenli kaydetme, süreç önceliği düzeltmesi. 6.2.0 (2026-03-21): bitince uygulamadan çıkış [15].
- FFmpeg Batch 3.2.4: kaynak tarihini koruma, "Try preset" ile deneme kodlaması [18].

**Kalite ölçümü.**

- Shutter Encoder 19.1 (2025-06-01): VMAF ve ses yüksekliği ölçümünü gösterme ve kaydetme. 18.5 (2024-09-16, iki yıldan biraz eski): çıktı kalitesini önceden görme [10].
- ab-av1 0.11.3: XPSNR ve VMAF birlikte. 0.11.7 (2026-08-21): `--verify` ile çıktıyı doğrulama [20].
- Av1an 0.5 (2025-10-02): hedef kalite için yeni ölçütler, varsayılan kodlayıcı SVT-AV1 [19].
- CompressO 3.0.0 (2026-04-06): önce ve sonra kaydırıcısı, bizdekinin eşi [21].

**Kodek.**

- VVC yayıldı: Shutter Encoder 18.4 (2024-08-12), FFmpeg Batch 3.2.3 (2025-05-13), FastFlix 6.0.0, StaxRip (vvencFFapp 1.14.0) [10][13][15][18].
- AV1 donanım yolları: Shutter Encoder 18.6 VideoToolbox, 19.4 Vulkan [10]. Bizde QuickSync, NVENC, AMF, Media Foundation ve VAAPI AV1 var (`PlanParser.cs:12`). Shutter'ın VideoToolbox ve Vulkan satırları kodlama mı çözme mi, sürüm notundan anlaşılmıyor; doğrulanmadı.
- SVT-AV1 çatalları: svt-av1-psy Nisan 2025'te durdu; yerini svt-av1-psyex ve svt-av1-hdr aldı. StaxRip ikincisini ve SVT-AV1-Essential'ı paketliyor [13][48].

**HDR.**

- FastFlix 6.0.0: ayrı kodlayıcı ikilileriyle (NVEncC, QSVEncC, VCEEncC) Dolby Vision kopyası, AV1'de HDR10+ (FFmpeg 8 ile) [15]. Aynı not Dolby Vision desteğinin denenmediğini de yazıyor.
- Shutter Encoder 20.3 (2026-08-31): ekran kartında ton eşleme [10].
- StaxRip: Dolby Vision kırpma düzeltmeleri, dovi_tool 2.3.4 [13].

**Süzgeç, ses, altyazı.**

- FastFlix 6.1.0 (2026-02-19): görsel kırpma penceresi, film greni, 23 değişkenli adlandırma. 6.0.0: görüntü altyazıyı (PGS) metne çevirme [15].
- Shutter Encoder 19.5 (2025-10-07): ekran kartında gürültü bastırma (Vulkan), konuşmayı yazıya dökme. 19.7: sesi ayırma [10].

**Hedef boyut.** Shutter Encoder 18.7 (2024-12-29): 10 MB altında ondalık hedef [10]. vidcord 7.5 (2026-09-30): 1 GB hedef [23].

**Kapalı kararlara denk gelenler.**

- K5 (konuşmadan altyazı): Shutter Encoder 19.5 bunu Ekim 2025'te ekledi, 19.8'de (2026-01-05) baştan yazdı, 20.2'de (2026-06-28) konuşmacı ayırmayı ekledi; bir yılda altı sürümde dokundu [10].
- K8 (LUT): HandBrake'te 10 oyla açık istek duruyor; son iki yılda rakip sürüm notunda yeni bir şey görmedim.
- Dolby Vision profil 5: HandBrake belgesi destekli sayıyor; FastFlix 6.0.0 Dolby Vision kopyasını Şubat 2026'da ekledi.

## Doğrulanamayanlar

- **WhatsApp'ın güncel video sınırı.** Kendi yardım sayfaları çelişiyor, gövdeleri okunamadı. Kodumuzdaki 16 ve 180 MB resmi kaynağa bağlı değil.
- **Discord sınırları.** Resmi sayfa 403 verdi; 20 / 50 / 500 MB arama özeti ve iki haberden. Artış tarihi kaynaklarda Ağustos ve 8 Eylül 2026 diye ikiye ayrılıyor.
- **Yakılan altyazıda biçim hangi rakipte var.** Shutter Encoder belgesinde bulamadım; XMedia Recode ve Hybrid okunamadı.
- **StaxRip'in karşılaştırma aracı, olay komutları, süreç önceliği.** Belge adresi 404 verdi; yalnız sürüm notları kanıt.
- **En çok oy alan istekler:** Av1an, ab-av1, StaxRip, FFmpeg Batch, CompressO, NotEnoughAV1Encodes için okunamadı (GitHub arama kotası doldu).
- **Sürümü okunamayanlar:** Tdarr, FileFlows, NotEnoughAV1Encodes, video-compare, UniConverter, Movavi, Adobe Media Encoder.
- **Hiç taranamayanlar:** Clipchamp, CloudConvert, VEED, Video Compressor Panda. Permute ve Hybrid'in özellik listesi okunamadı.
- **HandBrake'in duraklatma düğmesi ve bitince eylem listesi** resmi kuyruk belgesinde geçmiyor; kanıt sürüm notlarından dolaylı.
- **Shutter Encoder'ın uykuyu engellemesi** resmi belgede değil, bir kullanıcı bildiriminde [54].
- **HandBrake'te izleme klasörü.** Üç istek kapatılmış; eklendiği için mi reddedildiği için mi, okumadım.
- **Bizim ffmpeg derlememizde libvvenc var mı**, VVC'nin boyut kazancı ne: `docs/olcumler/` altında ölçüm bulamadım.
- **HDR10+'ı FFmpeg'in SVT-AV1'e taşıyıp taşımadığı.** Bizim ölçüm 0/3 (`docs/olcumler/b4-hdr-dinamik.md`); HandBrake kendi hattıyla taşıyor. FFmpeg 8 ile değişmiş olabilir.
- **CAMBI** (bantlanma ölçütü) VMAF kitaplığında var, defterde yazılı; bir rakip uygulamada gösterildiğini doğrulayamadım, tabloya almadım.
- **BM3D, DVD ve Blu-ray, "Max sıkıştırma":** kapalı kararlar; rakipte son iki yılda ne değişti, bu turda bakmadım.
- **Kodda aranmayanlar:** görüntü altyazıyı metne çevirme (OCR), HDR10'dan HLG'ye çevirme, ses kaydırma, iz adlandırma. Rakipte var ama bizde durumunu ölçmedim, tabloya almadım.
- **FastFlix'in donanım kodlayıcıda Dolby Vision kopyası.** 6.0.0 notu özelliği duyuruyor ama denenmediğini de yazıyor [15]; SIK-19'da FastFlix bu çekinceyle sayıldı, sağlam kanıt yalnız HandBrake belgesi [3].
- **Defter eşleşmesi.** SIK-23'teki `defterde` notu yalnız Windows bildiriminin hiç koşmadığını söyleyen satıra dayanıyor; macOS ve Linux eksiği defterde ayrıca yazılı değil.

## Kaynaklar

- [1] https://github.com/HandBrake/HandBrake/releases — HandBrake 1.8.0, 1.9.0, 1.10.0, 1.11.0, 1.11.2 sürüm notları
- [2] https://handbrake.fr/docs/en/latest/advanced/queue.html — HandBrake kuyruk belgesi (işi düzenleme, toplu ekleme)
- [3] https://handbrake.fr/docs/en/latest/technical/hdr.html — HandBrake HDR10+ ve Dolby Vision kodlayıcı ve profil listesi
- [4] https://handbrake.fr/news.php?article=42 — HandBrake 1.2.0: kuyruk arşivi ve kurtarma
- [5] https://handbrake.fr/docs/en/1.7.0/technical/system-requirements.html — HandBrake "Priority Level" ayarı
- [6] https://github.com/HandBrake/HandBrake/issues — HandBrake iş takipçisi; numaralar Kullanıcı Talepleri tablosunda
- [7] https://github.com/HandBrake/HandBrake/discussions/6363 — HandBrake süreç önceliği tartışması
- [8] https://vidcoder.net/ — VidCoder özellik listesi
- [9] https://github.com/RandomEngy/VidCoder/issues — VidCoder iş takipçisi
- [10] https://www.shutterencoder.com/changelog.txt — Shutter Encoder sürüm notları 18.4–20.4
- [11] https://www.shutterencoder.com/documentation/ — Shutter Encoder belgesi
- [12] https://github.com/paulpacifico/shutter-encoder/issues — Shutter Encoder iş takipçisi
- [13] https://github.com/staxrip/staxrip/releases — StaxRip sürüm notları
- [14] https://github.com/cdgriffith/FastFlix — FastFlix tanıtım sayfası
- [15] https://github.com/cdgriffith/FastFlix/releases — FastFlix 6.0.0, 6.1.0, 6.2.0 notları; depodaki `CHANGES` dosyası (kuyruktaki işi geri alıp düzenleme, süreç önceliği)
- [16] https://github.com/cdgriffith/FastFlix/issues — FastFlix iş takipçisi
- [17] https://github.com/eibol/ffmpeg_batch — FFmpeg Batch AV Converter tanıtım sayfası
- [18] https://github.com/eibol/ffmpeg_batch/releases — FFmpeg Batch 3.2.0–3.2.9 notları
- [19] https://github.com/rust-av/Av1an — Av1an tanıtım sayfası ve sürüm notları
- [20] https://github.com/alexheretic/ab-av1 — ab-av1 tanıtım sayfası ve sürüm notları
- [21] https://github.com/codeforreal1/compressO — CompressO tanıtım sayfası ve 3.0.0 notu
- [22] https://github.com/Wartybix/Constrict — Constrict'in hedef boyut yöntemi
- [23] https://github.com/cyroz1/vidcord — vidcord hedefleri, toplu kip, yeniden deneme, toplam ilerleme
- [24] https://github.com/Unmanic/unmanic — Unmanic
- [25] https://github.com/HaveAGitGat/Tdarr — Tdarr (zamanlayıcı, sağlık denetimi)
- [26] https://fileflows.com/ — FileFlows (izleme klasörü, kurallar, VMAF'a göre kodlama, uyarılar)
- [27] https://github.com/Alkl58/NotEnoughAV1Encodes — parçalara bölüp çok işçiyle kodlama
- [28] https://github.com/pixop/video-compare — yan yana, fark ve büyüteç kipleri
- [29] https://compresto.app/ — Compresto (klasör izleme, toplu iş)
- [30] https://www.freeconvert.com/video-compressor — FreeConvert (yüzdeyle hedef boyut, ön ayar kaydetme)
- [31] https://8mb.video/ — 8mb.video hedefleri ve bitince ses
- [32] https://software.charliemonroe.net/permute/ — Permute (yalnız lisans ve yapı bilgisi)
- [33] https://www.xmedia-recode.de/en/ — XMedia Recode sürümü
- [34] https://www.selur.de/downloads — Hybrid sürümü
- [35] https://videoconverter.wondershare.com/compress/ — UniConverter "Prioritize Size"
- [36] https://www.movavi.com/support/how-to/video-compression.html — Movavi boyut kaydırıcısı ve "Convert sample"
- [37] https://helpx.adobe.com/media-encoder/using/export-settings-reference.html — Adobe Media Encoder "Maximum File Size"
- [38] https://support.apple.com/en-us/116944 — macOS Finder ve QuickTime ile kodlama (hedef boyut yok)
- [39] https://telegram.org/faq — Telegram 2 GB, Premium 4 GB
- [40] https://support.google.com/mail/answer/6584 — Gmail 25 MB
- [41] https://support.microsoft.com/en-us/office/reduce-attachment-size-to-send-large-files-with-outlook-8c698842-b462-4a4c-8d53-5c5dd04f77ef — Outlook 20 MB, Exchange 10 MB
- [42] https://support.discord.com/hc/en-us/articles/25444343291031-File-Attachments-FAQ — Discord sınırları (sayfa 403 verdi, arama özeti)
- [43] https://www.gamebusiness.jp/article/2026/08/15/27714.html — Discord ücretsiz sınırının 20 MB olması (haber)
- [44] https://allthings.how/discord-doubles-the-free-file-upload-limit-to-20mb/ — aynı konu (haber)
- [45] https://faq.whatsapp.com/453914586839706/ — WhatsApp masaüstü: 100 MB ve 64 MB (arama özeti)
- [46] https://faq.whatsapp.com/239536730601513/ — WhatsApp: 16 MB (arama özeti)
- [47] https://blog.whatsapp.com/reactions-2gb-file-sharing-512-groups — WhatsApp belge olarak 2 GB
- [48] https://github.com/juliobbv-p/svt-av1-hdr — SVT-AV1 çatalı; ayrıca https://github.com/BlueSwordM/svt-av1-psyex ve https://github.com/psy-ex/svt-av1-psy
- [49] https://github.com/Netflix/vmaf — VMAF sürümü
- [50] https://github.com/fraunhoferhhi/vvenc — VVC kodlayıcısı vvenc sürümü
- [51] https://github.com/HandBrake/HandBrake/blob/master/win/CS/HandBrakeWPF/Properties/Resources.resx — HandBrake ayar metni "Prevent the system from sleeping while encoding"
- [52] https://forum.handbrake.fr/viewtopic.php?p=79897 — HandBrake forumu: uykuyu engelleme isteği "[Implemented]"
- [53] https://fftrac-bg.ffmpeg.org/ticket/10447 — HandBrake'in macOS'ta uykuyu engellediğini anan FFmpeg kaydı
- [54] https://github.com/paulpacifico/shutter-encoder/issues/302 — Shutter Encoder ekranı açık tutuyor (kullanıcı bildirimi, 2025-06-27)
- [55] `docs/olcumler/olculen-kaliteyle-plan.md` — bizim ölçüm: ölçülen kalite kolu arayüzde varsayılan
- [56] `docs/olcumler/ornekte-vmaf-maliyeti.md` — bizim ölçüm: örnekte VMAF'ın süre maliyeti
- [57] `docs/olcumler/b4-hdr-dinamik.md` — bizim ölçüm: HDR10+ ve Dolby Vision taşıma, profil 5 kapsam dışı
- [58] `docs/piyasa/tarama-2026-10-05.md`, `docs/handbrake/durum-2026-10-05.md`, `.claude/acik.md` — önceki tarama ve açık işler defteri

## Komşu Kola Not

- Dönüştür: görüntü dizisini girdi olarak alma HandBrake'in ikinci en çok oy alan isteği (47 oy, https://github.com/HandBrake/HandBrake/issues/2346).
- Dönüştür: GIF'te hedef boyut isteği Shutter Encoder'da açık (#159, 2 oy); CompressO 3.0.0 görüntü ve GIF sıkıştırma ekledi.
- Dönüştür: bölümlere göre ayrı dosya çıkarma HandBrake'te 9 oy (#3094).
- Dağıtım: AppImage, Flatpak ve Chocolatey istekleri üç rakipte de açık (HandBrake #1722 18 oy, FastFlix #462, Shutter Encoder #149).
- Komut satırı: kuyruğa komut satırından iş ekleme VidCoder'da istenmiş (#68).
