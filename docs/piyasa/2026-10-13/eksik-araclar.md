# Eksik Araçlar — Beş İşin Dışında Kalanlar

13 Ekim 2026. Kod okuması `eb0a5db0` ("Surum 1.2.16") üzerinde yapıldı; bu, ORTAK.md'de yazan `9de48d8b`'den bir commit sonradır.

Eksik tablosu `eksik-araclar-eksikler.md` dosyasında: 44 satır, önek `ARC`. Oradaki `[n]` numaraları bu dosyanın sonundaki Kaynaklar listesine bakar.

Kapsam: bugünkü beş işin (oynatıcı, düzenleyici, hedef boyuta küçültme, dönüştürme, ekran kaydı) hiçbirine girmeyen araçlar. Bir sekmenin doğal parçası olan özellikler burada yok; onları sekmelerin kendi kolları yazıyor.

## Envanter

Kısa sözlük: **ffprobe**, ffmpeg'in dosya bilgisini okuyan yardımcı programıdır. **Süzgeç** (filter), ffmpeg'in görüntü ya da ses üstünde çalıştırdığı tek bir işlemdir. **Akış kopyası**, görüntüyü yeniden kodlamadan dosyayı yeniden yazmaktır.

### Bugün Kodda Olanlar

- **Araçlar sekmesi yok.** Sekmeler: Oynatıcı `src/VidShrink.App/MainWindow.axaml:214`, Düzenleyici `src/VidShrink.App/MainWindow.axaml:218`, Küçült `src/VidShrink.App/MainWindow.axaml:223`, Dönüştür `src/VidShrink.App/MainWindow.axaml:1089`, Kaydedici `src/VidShrink.App/MainWindow.axaml:1411`, Gelişmiş (gizli) `src/VidShrink.App/MainWindow.axaml:1416`, Ayarlar `src/VidShrink.App/MainWindow.axaml:1504`.
- **Oynatıcının araçlar menüsü.** GIF dışa aktarma `src/VidShrink.App/Playback/ToolsOptions.cs:14`, bağlantı açma `src/VidShrink.App/Playback/ToolsOptions.cs:33`, zaman çubuğunda küçük resim `src/VidShrink.App/Playback/PlayerView.Tools.cs:181` ve `:233`, dışa aktarma `src/VidShrink.App/Playback/PlayerView.Tools.cs:416`, bağlantı penceresi `src/VidShrink.App/Playback/PlayerView.Tools.cs:564`.
- **Medya bilgisi (özet).** Kayıt `src/VidShrink.Core/MediaInfo.cs:3` (boyut, süre, çözünürlük, kare hızı, kodekler, bit hızları, HDR alanları, akışlar, bölümler); oynatıcı tarafı `src/VidShrink.Player/MediaDetails.cs:9`; okuyan kod `src/VidShrink.Ffmpeg/FfprobeClient.cs:40`; Küçült sekmesindeki bilgi paneli `src/VidShrink.App/MainWindow.axaml:292`, `:316`, `:320`; oynatıcıda bilgi katmanı kısayolu `src/VidShrink.App/Playback/Keymap.cs:72`. Akış akış tam döküm ve dışa aktarma yok.
- **Video akışı olmayan dosya reddediliyor.** `src/VidShrink.Ffmpeg/FfprobeClient.cs:46`. Bu yüzden salt ses dosyası dönüştürücüye girdi olamıyor.
- **Kalite ölçümü.** Sınıf `src/VidShrink.Ffmpeg/QualityMeter.cs:114`, kaynak ve çıktı yolunu alan ölçüm `src/VidShrink.Ffmpeg/QualityMeter.cs:179`; komut satırı `src/VidShrink.Cli/CliApp.cs:531`; görsel karşılaştırma paneli `src/VidShrink.App/Playback/ComparisonPanel.axaml.cs` (satır okunmadı). Yalnız kendi çıktımızı puanlıyor.
- **GIF ve hareketli resim, yalnız çıktı.** `src/VidShrink.Core/ConversionPlan.cs:33` (GIF), `src/VidShrink.Core/ConversionPlan.cs:34` (hareketli WebP ve AVIF), palet `src/VidShrink.Core/GifPalette.cs:5`, kaydedicide GIF `src/VidShrink.Core/RecorderArguments.cs:436`.
- **Ses, yalnız çıktı.** Videodan ses çıkarma `src/VidShrink.Core/ConversionPlan.cs:32`. Girdi adayları yalnız video uzantıları: `src/VidShrink.Core/WatchFolder.cs:74`, `src/VidShrink.Core/DroppedMedia.cs:24`. Ses uzantıları yalnız oynatıcı için tanımlı: `src/VidShrink.Core/ShellIntegration.cs:21`.
- **Üst veri (etiketler).** Topluca silme `src/VidShrink.Core/ConversionPlan.cs:31` ve `src/VidShrink.Core/ConversionArguments.cs:198`; taşıma ve iz adları `src/VidShrink.Core/StreamMapping.cs:82`, `:251`, `:280`, `:286`, `:292`; kapak resmini çıkarma `src/VidShrink.Ffmpeg/EncodeRunner.cs:190`. Kullanıcının etiketi elle yazdığı bir yer yok.
- **Ses yüksekliği.** Sabit hedefe çeken süzgeç `src/VidShrink.Core/StreamMapping.cs:357`, arayüz kutusu `src/VidShrink.App/MainWindow.Izler.cs:88`, komut satırı `src/VidShrink.Cli/CliRequest.cs:119`. Ölçüm raporu yok.
- **Gürültü bastırma, yalnız kayıt sırasında.** `src/VidShrink.Core/AudioCaptureArguments.cs:135` ve `:272`, ayar `src/VidShrink.App/Recorder/RecorderSettings.cs:183`.
- **Altyazı.** Oynatıcıda iz seçme ve gecikme `src/VidShrink.Player/MpvEngine.cs:343`, `:357`, `:389`; OpenSubtitles'tan indirme `src/VidShrink.App/MainWindow.axaml.cs:1818`; düzenleyicide yazı katmanı `src/VidShrink.App/Editing/EditorView.Metin.cs:15`; görüntüye gömme `src/VidShrink.Core/VideoFilterChain.cs:209`; komut satırı `src/VidShrink.Cli/CliRequest.cs:566` ve `:573`; karakter kodlaması `src/VidShrink.Core/SubtitleCharset.cs` (satır okunmadı). Altyazı dosyasını açıp düzenleyen ya da dönüştüren bir yer yok.
- **Kare kaydı.** Tek kare `src/VidShrink.Player/MpvEngine.cs:530`; kısayollar `src/VidShrink.App/Playback/Keymap.cs:73` (kaydet) ve `:74` (panoya kopyala); ayar `src/VidShrink.App/Playback/PlayerSettings.cs:19`; düzenleyicinin küçük resim kuyruğu `src/VidShrink.Core/Editing/ThumbnailQueue.cs:253`.
- **Ölçekleme.** Yalnız klasik yöntem (lanczos): `src/VidShrink.Core/ConversionArguments.cs:232`. Kaynaktan büyüğe çıkmama kuralı `src/VidShrink.Core/PlanParser.cs:93`.
- **Algılamalar, yalnız iç kullanım.** Sahne `src/VidShrink.Ffmpeg/SceneDetector.cs:15`; donuk kare `src/VidShrink.Core/IdleTrim.cs:38`; sessizlik `src/VidShrink.Core/Editing/SilenceCut.cs:51`; siyah kare `src/VidShrink.Core/Editing/SilenceCut.cs:94`; siyah bant `src/VidShrink.Ffmpeg/CropProbe.cs:15`. Hiçbiri rapor olarak dışa verilmiyor.
- **Kopyalayarak yeniden yazma parçaları.** Kayıpsız kesim `src/VidShrink.Core/FfmpegArguments.cs:505`; kaydedicinin kendi çıktısını yeniden yazması `src/VidShrink.Ffmpeg/RecorderSession.cs:423` ve `:703`. `src/VidShrink.App/Playback/OynatmaKurtarici.cs:24` oynatmayı kurtarır, dosyayı onarmaz.
- **Diğer yakın parçalar.** Çıktı adı deseni `src/VidShrink.Core/AdlandirmaDeseni.cs:15`; altyazı araması için dosya özeti `src/VidShrink.Core/Subtitles/MovieHash.cs:13`; izlenen klasör `src/VidShrink.Core/WatchFolder.cs:69`; düzenleyicinin dalga biçimi verisi `src/VidShrink.Core/Editing/AudioPeaks.cs:9`; DVD başlık taraması `src/VidShrink.Core/SourceTitles.cs:25` ve `:35`; paylaşım bağlantısı `src/VidShrink.Core/Share/UploadContent.cs:71`.

### Önceki Taramanın "Karar Gerektirmeyenler" Kalemleri

`docs/piyasa/tarama-2026-10-05.md` satır 227–250 arasından bu kola düşen dört kalem, dördü de yapılmış:

- 5 numara, hareketli WebP ve AVIF: yapıldı (`src/VidShrink.Core/ConversionPlan.cs:34`).
- 6 numara, kareyi panoya ya da JPG olarak alma: yapıldı (`src/VidShrink.App/Playback/Keymap.cs:73`, `:74`; `src/VidShrink.App/Playback/PlayerSettings.cs:29`).
- 10 numara, bilgi panelinde HDR ve canlı istatistik: yapıldı (`src/VidShrink.Core/MediaInfo.cs:3`, `src/VidShrink.App/Playback/Keymap.cs:72`).
- 12 numara, üst veriyi silme: yapıldı (`src/VidShrink.Core/ConversionPlan.cs:31`, `src/VidShrink.Core/ConversionArguments.cs:198`).

### Kodda Aranıp Bulunmayan Adlar

`src` altında `*.cs` dosyalarında arandı, hiçbiri çıkmadı. Arayüz dosyaları (`.axaml`) ve dil dosyaları (`.json`) bu aramaya girmedi.

- Süzgeç ve seçenek adları: `tile=`, `minterpolate`, `ebur128`, `arnndn`, `libplacebo`, `framemd5`, `delogo`, `showwaves`, `signature=`, `itsoffset`, `adelay`, `vidstab`, `deshake`, `lut3d`, `stereo3d`.
- Model ve araç adları: `esrgan`, `realcugan`, `rife`, `demucs`, `whisper`, `deoldify`, `rembg`.
- Türkçe ve İngilizce iş adları: `SpeechSynth`, `text-to-speech`, `TopluAdlandir`, `BatchRename`, `ContactSheet`, `KucukResimTablosu`, `DuplicateFinder`, `Yinelenen`, `SetLastWriteTime`, `SetCreationTime`, `ImageExtensions`, `IsImage(`, `.pdf`, `mtp`.

`afftdn` yalnız kaydedicide çıktı (`src/VidShrink.Core/AudioCaptureArguments.cs:135`).

`.claude/acik.md` defterinde bu koldaki araçların hiçbiri yazılı değil; tabloda `defterde` eki bu yüzden yok.

## Rakipler

45 rakip tarandı: 13 çok araçlı paket, 32 tek işlik araç. "Yıldız", GitHub'da depoyu beğenenlerin sayısıdır; yaygınlık için kaba bir ölçü olarak yazıldı.

### Çok Araçlı Paketler

| Sıra | Ad | Sürüm | Tarih | Tür | URL |
|---|---|---|---|---|---|
| 1 | Shutter Encoder | 20.4 | 28 Eyl 2026 | açık kaynak (GPL-3.0), ücretsiz, masaüstü; 2.815 yıldız; Windows kurucusu 158 MB | https://www.shutterencoder.com/documentation/ |
| 2 | Wondershare UniConverter | 17.6.0 | 31 Ağu 2026 | ücretli, masaüstü; yapay zekâ kredisi var | https://videoconverter.wondershare.com/what-is-new.html |
| 3 | VideoProc Converter AI | doğrulanmadı | doğrulanmadı | ücretli, masaüstü | https://www.videoproc.com/video-converting-software/ |
| 4 | Movavi Video Suite | 2026 (26.1.2) | 18 Ağu 2026 | ücretli, masaüstü; sayfadaki fiyat 29,95 USD | https://www.movavi.com/suite/ |
| 5 | Any Video Converter | 9.2.5 | 10 Ağu 2026 | ücretsiz, Pro ve Ultimate katmanları; masaüstü | https://www.any-video-converter.com/en8/for_video_free/ |
| 6 | Format Factory | 5.22.0.0 | 11 Mar 2026 | ücretsiz, masaüstü (Windows) | https://www.pcfreetime.com/formatfactory/index.php?language=en |
| 7 | HitPaw Univd | doğrulanmadı | doğrulanmadı | ücretli, masaüstü | https://www.hitpaw.com/univd-tools/ |
| 8 | Aiseesoft Video Converter Ultimate | 11.0 | doğrulanmadı | ücretli, masaüstü | https://www.aiseesoft.com/video-converter-ultimate/ |
| 9 | Clipchamp | sürüm numarası yok | — | Microsoft'un web ve Windows aracı | https://clipchamp.com/en/features/ |
| 10 | CapCut | bakılmadı | — | web, masaüstü ve mobil | https://www.capcut.com/tools |
| 11 | Kapwing | sürüm numarası yok | — | web; 200'den çok araç | https://www.kapwing.com/tools |
| 12 | VEED | sürüm numarası yok | — | web | https://www.veed.io/tools |
| 13 | Topaz Video | doğrulanmadı | doğrulanmadı | yalnız abonelik (yılda 299 USD kişisel, 699 USD Pro); masaüstü ve bulut modelleri | https://www.topazlabs.com/topaz-video |

### Tek İşlik Araçlar

| Sıra | Ad | Sürüm | Tarih | Tür | URL |
|---|---|---|---|---|---|
| 14 | MediaInfo | 26.10 | 6 Eki 2026 | açık kaynak (BSD-2-Clause); 2.030 yıldız; medya bilgisi | https://mediaarea.net/en/MediaInfo |
| 15 | Subtitle Edit | 5.2.0 | 10 Eyl 2026 | açık kaynak (MIT); 14.537 yıldız; altyazı düzenleyici; paketler 40–123 MB | https://github.com/SubtitleEdit/subtitleedit |
| 16 | Aegisub | 3.5.0 | 5 Eki 2026 | açık kaynak; 2.010 yıldız; altyazı düzenleyici | https://github.com/TypesettingTools/Aegisub |
| 17 | MKVToolNix | 102.0 | 14 Eyl 2026 | açık kaynak; MKV başlık ve bölüm düzenleyici | https://mkvtoolnix.download/doc/NEWS.md |
| 18 | LosslessCut | 3.69.0 | 4 Haz 2026 | açık kaynak; 44.526 yıldız; kayıpsız kesim ve iz araçları | https://github.com/mifi/lossless-cut |
| 19 | Czkawka | 12.0.2 | 9 Eyl 2026 | açık kaynak; 34.224 yıldız; yinelenen dosya ve benzer video bulucu | https://github.com/qarmin/czkawka |
| 20 | Video Duplicate Finder | 4.1.1 | 25 Eyl 2026 | açık kaynak (AGPL-3.0); 3.783 yıldız; yinelenen video bulucu | https://github.com/0x90d/videoduplicatefinder |
| 21 | FFMetrics | 1.7.0 | 6 May 2026 | ücretsiz, lisans dosyası yok; 1.036 yıldız; kalite ölçer; 17,2 MB | https://github.com/fifonik/FFMetrics |
| 22 | FFBitrateViewer | doğrulanmadı | son itme 23 Ağu 2026 | açık kaynak (MIT); 174 yıldız; bit hızı grafiği | https://github.com/fifonik/FFBitrateViewer |
| 23 | Netflix vmaf | 3.2.1 | 14 Eyl 2026 | açık kaynak; 5.528 yıldız; kalite ölçüm kitaplığı | https://github.com/Netflix/vmaf |
| 24 | video-compare | 20261004 | 4 Eki 2026 | açık kaynak (GPL-2.0); 1.794 yıldız; iki videoyu yan yana karşılaştırma | https://github.com/pixop/video-compare |
| 25 | untrunc | sürüm numarası yok | son itme 21 Ağu 2026 | açık kaynak (GPL-2.0); 3.482 yıldız; kesik MP4 kurtarma | https://github.com/anthwlock/untrunc |
| 26 | Video2X | 6.4.0 | 24 Oca 2025 | açık kaynak (AGPL-3.0); 22.212 yıldız; büyütme ve kare ara değerleme; 188–220 MB | https://github.com/k4yt3x/video2x |
| 27 | REAL Video Enhancer | 2.4.1 | 2 Oca 2026 | açık kaynak (AGPL-3.0); 2.321 yıldız; büyütme ve kare ara değerleme; 67–376 MB | https://github.com/TNTwise/REAL-Video-Enhancer |
| 28 | rife-ncnn-vulkan | 20221029 | 29 Eki 2022 | açık kaynak (MIT); 1.109 yıldız; kare ara değerleme motoru; 411–416 MB | https://github.com/nihui/rife-ncnn-vulkan |
| 29 | Flowframes | 1.36.0 (ücretsiz sürüm) | doğrulanmadı | yeni sürümler yalnız Patreon'da; Windows; kare ara değerleme | https://nmkd.itch.io/flowframes |
| 30 | Upscayl | 2.15.0 | 25 Ara 2024 | açık kaynak; 50.520 yıldız; resim büyütme; 298 MB | https://github.com/upscayl/upscayl |
| 31 | Real-ESRGAN | 0.3.0 | 2022 | açık kaynak; 37.079 yıldız; büyütme modeli | https://github.com/xinntao/Real-ESRGAN |
| 32 | Ultimate Vocal Remover | 5.6 | doğrulanmadı | açık kaynak (MIT); 26.592 yıldız; vokal ayırma; Windows kurucusu 1.793,7 MB | https://github.com/Anjok07/ultimatevocalremovergui |
| 33 | Demucs | depo arşivli | — | açık kaynak; 10.372 yıldız; ses ayırma modeli | https://github.com/facebookresearch/demucs |
| 34 | backgroundremover | 0.4.5 | 10 Tem 2026 | açık kaynak (MIT); 8.091 yıldız; arka plan silme | https://github.com/nadermx/backgroundremover |
| 35 | DeOldify | depo arşivli | — | açık kaynak; 18.465 yıldız; renklendirme modeli | https://github.com/jantic/DeOldify |
| 36 | DeepFilterNet | 0.5.6 | 2023 | açık kaynak; 4.818 yıldız; gürültü bastırma | https://github.com/Rikorose/DeepFilterNet |
| 37 | gifski | 1.34.0 | 13 Tem 2025 | açık kaynak; 5.671 yıldız; GIF kodlayıcı | https://github.com/ImageOptim/gifski |
| 38 | ScreenToGif | 2.43.2 | 28 Tem 2026 | açık kaynak; 27.778 yıldız; GIF kaydedici ve düzenleyici | https://github.com/NickeManarin/ScreenToGif |
| 39 | PySceneDetect | 0.7.2 | 12 Eki 2026 | açık kaynak; 5.234 yıldız; sahne algılama | https://github.com/Breakthrough/PySceneDetect |
| 40 | vcsi | 7.0.17 | 22 Mar 2026 | açık kaynak (MIT); 637 yıldız; küçük resim tablosu | https://github.com/amietn/vcsi |
| 41 | mtn | 3.5.0 | 21 Şub 2024 (ikincil kaynak) | açık kaynak; küçük resim tablosu | https://gitlab.com/movie_thumbnailer/mtn |
| 42 | FileConverter | 2.2 | 13 Şub 2026 | açık kaynak (GPL-3.0); 15.375 yıldız; sağ tık menüsünden dönüştürücü | https://github.com/Tichau/FileConverter |
| 43 | auto-editor | 31.7.2 | 3 Eki 2026 | açık kaynak; 5.496 yıldız; sessizlik kesici | https://github.com/WyattBlue/auto-editor |
| 44 | FileBot | 5.3.0 | doğrulanmadı | ücretli (yılda 8 USD, ömür boyu 80 USD); kitaplık düzenleyici | https://www.filebot.net/ |
| 45 | tinyMediaManager | 5.3.4 | doğrulanmadı | kitaplık düzenleyici | https://www.tinymediamanager.org/ |

## Özellik Matrisi

"Paket" burada 12 çok araçlı paket demek: Shutter Encoder, UniConverter, VideoProc Converter AI, Movavi Video Suite, Any Video Converter, Format Factory, HitPaw Univd, Aiseesoft, Clipchamp, CapCut, Kapwing, VEED. Topaz Video yalnız görüntü iyileştirdiği için ayrı anıldı.

"Beklenen", en az üç pakette bulunan araçtır. Bir pakette görülmeyen araç için "yok" denmiyor; yalnız okunan sayfalarda görülmedi.

### Hangi Araç Kaç Pakette

| Araç | Sunan Paketler | Paket Sayısı | Beklenen | Tablo Satırı |
|---|---|---|---|---|
| GIF yapıcı (videodan) | Shutter Encoder, UniConverter, VideoProc, Any Video Converter, HitPaw, Aiseesoft, Clipchamp, Kapwing, VEED | 9 | evet | bizde var, satır yok |
| Dosyada gürültü temizleme | UniConverter, VideoProc, HitPaw, Aiseesoft, Movavi, Clipchamp, Kapwing, VEED, CapCut | 9 | evet | ARC-24 |
| Konuşmadan metne | Shutter Encoder, UniConverter, Movavi, Any Video Converter, HitPaw, CapCut, Kapwing, VEED, Clipchamp | 9 | evet | ARC-41 (kapalı: K5) |
| Vokal ve enstrüman ayırma | Shutter Encoder, UniConverter, VideoProc, Any Video Converter, HitPaw, Aiseesoft, Kapwing | 7 | evet | ARC-25 |
| Yapay zekâ ile büyütme | Shutter Encoder, UniConverter, VideoProc, Any Video Converter, HitPaw, Aiseesoft, Kapwing; ayrıca Topaz Video | 7 | evet | ARC-27 |
| Görüntü dönüştürücü | UniConverter, HitPaw, Aiseesoft, Format Factory, Shutter Encoder, Kapwing | 6 | evet | ARC-11 |
| Arka plan silme (video) | UniConverter, Movavi, CapCut, Kapwing, VEED, Clipchamp | 6 | evet | ARC-29 |
| Disk yazma ve kopyalama | Shutter Encoder, UniConverter, VideoProc, Movavi, Any Video Converter, Format Factory | 6 | evet | ARC-44 (kapalı) |
| URL'den indirme | Shutter Encoder, VideoProc, Any Video Converter, Format Factory | 4 | evet | ARC-40 (kapalı: K1) |
| Metinden sese | UniConverter, CapCut, Kapwing, VEED, Clipchamp | 5 | evet | ARC-26 |
| Ses dönüştürücü | UniConverter, HitPaw, Format Factory, Kapwing | 4 | evet | ARC-19 |
| Kare ara değerleme | Shutter Encoder, UniConverter, VideoProc, Aiseesoft; ayrıca Topaz Video | 4 | evet | ARC-28 |
| Kendiliğinden yeniden kadraj | UniConverter, HitPaw, Movavi, CapCut | 4 | evet | ARC-30 |
| Altyazı düzenleyici | Shutter Encoder, UniConverter, Kapwing, VEED | 4 | evet | ARC-22 |
| Altyazı çevirisi | Shutter Encoder, UniConverter, Kapwing, VEED | 4 | evet | ARC-23 |
| Stabilizasyon | VideoProc, Any Video Converter, HitPaw, Kapwing; ayrıca Topaz Video | 4 | evet | ARC-42 (kapalı: K8) |
| Filigran ekleme | UniConverter, Any Video Converter, Aiseesoft, HitPaw | 4 | evet | ARC-43 (kapalı: K8) |
| Yüz bulanıklaştırma | Shutter Encoder, UniConverter, HitPaw | 3 | evet | ARC-31 |
| Logo ve filigran silme | UniConverter, Format Factory, Aiseesoft | 3 | evet | ARC-33 |
| 2B'den 3B'ye ve VR | UniConverter, HitPaw, Aiseesoft | 3 | evet | ARC-34 |
| Toplu kare dışa aktarma | Shutter Encoder, HitPaw, VideoProc | 3 | evet | ARC-08 |
| GIF girdisi (GIF küçültme, GIF'ten videoya) | HitPaw, Kapwing, UniConverter | 3 | evet | ARC-12 |
| Üretken yapay zekâ | CapCut, VEED, Any Video Converter | 3 | evet | ARC-39 |
| Etiket düzenleyici | UniConverter, Aiseesoft | 2 | hayır | ARC-14 |
| Altyazı dosyası dönüştürme | Kapwing, VEED | 2 | hayır | ARC-21 |
| Renklendirme (video) | Shutter Encoder, Aiseesoft | 2 | hayır | ARC-32 |
| Slayt gösterisi | Kapwing, VEED | 2 | hayır | ARC-10 |
| Sesten video | Kapwing, Clipchamp | 2 | hayır | ARC-20 |
| Medya bilgisi | Shutter Encoder | 1 | hayır | ARC-01 |
| İki dosya kalite ölçümü | Shutter Encoder | 1 | hayır | ARC-02 |
| Ses yüksekliği raporu | Shutter Encoder | 1 | hayır | ARC-04 |
| Denetim algılamaları | Shutter Encoder | 1 | hayır | ARC-05 |
| Kare sağlaması | Shutter Encoder | 1 | hayır | ARC-06 |
| Resim dizisi girdisi | Shutter Encoder | 1 | hayır | ARC-09 |
| Toplu yeniden adlandırma | Shutter Encoder | 1 | hayır | ARC-35 |
| Onarma | Format Factory; Aiseesoft'ta ayrı ürün | 1 | hayır | ARC-16 |
| Aygıta aktarma | UniConverter | 1 | hayır | ARC-37 |
| Belge araçları | Format Factory | 1 | hayır | ARC-38 |
| Bit hızı grafiği, küçük resim tablosu, bölüm düzenleyici, kesik MP4 kurtarma, yinelenen bulucu, kitaplık düzenleyici | hiçbir pakette görülmedi; yalnız tek işlik araçlarda | 0 | hayır | ARC-03, ARC-07, ARC-15, ARC-17, ARC-18, ARC-36 |

Okuma: paketlerde en yaygın gruplar yapay zekâ araçları ve "her şeyi çevir" araçları. İnceleme ve ölçüm araçları ise neredeyse yalnız Shutter Encoder'da ve tek işlik açık kaynak araçlarda duruyor.

### Tek İşlik En Bilinen Ürün Ve Bize Ne Gerekir

Kısa sözlük: **ncnn**, modeli ekran kartında koşturan küçük bir kitaplıktır. **Vulkan**, ekran kartına erişim arayüzüdür. **PyTorch**, yapay zekâ modellerini koşturan büyük bir Python kitaplığıdır. **RIFE** ara kare üreten, **Real-ESRGAN** görüntü büyüten modeldir.

| Araç | En Bilinen Ürün | En Küçük Özellik Seti | Bize Ne Gerekir |
|---|---|---|---|
| Medya bilgisi | MediaInfo 26.10 | metin, tablo, ağaç ve HTML görünümü; metin, CSV, HTML dışa aktarma | yalnız ffprobe; yeni bağımlılık yok |
| Kalite ölçer | FFMetrics 1.7.0 | PSNR, SSIM, VMAF, XPSNR grafikleri; 24 dosyaya kadar karşılaştırma | yalnız ffmpeg; motor bizde hazır |
| Bit hızı grafiği | FFBitrateViewer | kare, saniye ve GOP başına grafik | yalnız ffprobe |
| Ses yüksekliği raporu | Shutter Encoder | ölçme, gösterme, kaydetme | yalnız ffmpeg (`ebur128`) |
| Denetim raporu | Shutter Encoder, PySceneDetect 0.7.2 | siyah kare, kesme noktası, çevrimdışı medya; listeyi kaydetme | yalnız ffmpeg (`blackdetect`, `silencedetect`, `freezedetect`, `scdet`); algılamalar bizde koşuyor |
| Küçük resim tablosu | vcsi 7.0.17, mtn 3.5.0 | ızgara boyutu, zaman damgası, başlıkta dosya bilgisi | yalnız ffmpeg (`tile`, `thumbnail`) |
| Etiket ve bölüm düzenleyici | MKVToolNix 102.0, LosslessCut 3.69.0 | ad, dil, varsayılan ve zorunlu bayrakları, ekler; bölüm düzenleme | yalnız ffmpeg (akış kopyası) |
| Altyazı düzenleyici | Subtitle Edit 5.2.0, Aegisub 3.5.0 | düzenleme, biçim dönüştürme, video ile önizleme; çeviri, konuşmadan metne ve OCR isteğe bağlı çevrimiçi hizmet | ffmpeg ve libmpv yeter; iş büyük. Subtitle Edit paketleri 40–123 MB |
| Kesik MP4 kurtarma | untrunc | sağlam örnek dosyaya bakarak kurtarma | ffmpeg yalnız yeniden sarar; dizinsiz dosya için ayrı ikili (GPL-2.0), boyutu doğrulanmadı |
| Yinelenen video bulucu | Czkawka 12.0.2, Video Duplicate Finder 4.1.1 | benzer videoları gruplama; kısmi klip algılama; ikisi de ffmpeg ister | yalnız ffmpeg (kare özeti; `signature` süzgeci ana dalda var) |
| GIF | gifski 1.34.0, ScreenToGif 2.43.2 | iyi palet, kare düzenleme | bizde var (`palettegen`) |
| Görüntü dönüştürücü | FileConverter 2.2 | sağ tık menüsünden toplu dönüştürme | yalnız ffmpeg; HEIC ve RAW doğrulanmadı |
| Kare ara değerleme | Flowframes, rife-ncnn-vulkan, REAL Video Enhancer 2.4.1 | 2 ve 4 kat kare hızı, RIFE modeli | `minterpolate` ile bağımlılıksız; RIFE için ncnn ve Vulkan, paket 411–416 MB (rife-ncnn-vulkan) ya da 67–376 MB (REAL Video Enhancer) |
| Yapay zekâ ile büyütme | Video2X 6.4.0, Topaz Video | Real-ESRGAN, Real-CUGAN, Anime4K; 2 ve 4 kat | ncnn ve Vulkan; Video2X 188–220 MB; REAL Video Enhancer en az 4 GB ekran kartı belleği ister |
| Vokal ayırma | Ultimate Vocal Remover 5.6, Demucs | vokal ve çalgı ayrımı | PyTorch ve model; UVR Windows kurucusu 1.793,7 MB, macOS 461–561 MB |
| Arka plan silme | backgroundremover 0.4.5 | resimde ve videoda arka plan silme | PyTorch ister; boyut doğrulanmadı |
| Renklendirme | DeOldify (depo arşivli) | üç model: sanatsal, dengeli, video | PyTorch ve model; boyut doğrulanmadı |
| Gürültü temizleme | DeepFilterNet 0.5.6 | konuşma dışı gürültüyü bastırma | `afftdn` ile bağımlılıksız; `arnndn` model dosyası ister; DeepFilterNet ayrı ikili |
| Sessizlik kesme | auto-editor 31.7.2 | sessiz yerleri kendiliğinden kesme | bizde düzenleyicide var (`src/VidShrink.Core/Editing/SilenceCut.cs:51`); satır yok |
| Kitaplık düzenleyici | FileBot 5.3.0, tinyMediaManager 5.3.4 | çevrimiçi veritabanından adlandırma, afiş, bilgi dosyası | çevrimiçi hizmet gerekir |

Özet: 44 satırın çoğu yalnız ffmpeg ile yapılabilir görünüyor. Yeni çalışma zamanı ve model isteyenler: ARC-25, ARC-27, ARC-29, ARC-30, ARC-31, ARC-32, ARC-34. Çevrimiçi hizmet isteyenler: ARC-23, ARC-26, ARC-36, ARC-39. Ayrı ikili isteyen: ARC-17.

### Öncelik Nasıl Verildi

Bu kolun "ana işi" şöyle okundu: kullanıcının elindeki dosyayı incelemek, ölçmek, yeniden kodlamadan düzeltmek ya da başka biçime çevirmek.

- Bu tanıma giren ve en az üç rakipte belgeli olan ya da oyla kanıtlı araçlar 1 aldı. "Rakip" sayısına tek işlik araçlar da girer; örnek: ARC-01'de MediaInfo, Shutter Encoder ve LosslessCut.
- Yapay zekâ ile görüntü ya da ses iyileştiren araçlar ana işin kenarı sayıldı ve çok pakette olsalar da 2 aldı. Tek istisna dosyada gürültü temizleme (ARC-24): model olmadan yapılabiliyor ve süzgeci kaydedicide zaten var.
- Beş işle bağı olmayan araçlar üç ya da daha çok pakette olsa bile 3 aldı: ARC-26 (5 paket), ARC-34 (3 paket), ARC-39 (3 paket). Bu, ORTAK.md'deki kuralın harfinden bilinçli bir sapmadır; sahibi isterse bu üç satır 2'ye çekilir.
- ARC-09 önceliğini rakip sayısından değil oydan alıyor (47 oy).

### Diğer Kollarla Örtüşen Satırlar

- ARC-07 ile OYA-44: küçük resim tablosu. Oynatıcı kolu 3 vermiş, burada 2.
- ARC-14 ile DZA-24: kap üst verisini düzenleme.
- ARC-15 ile DZA-08, DZA-13, DZA-14: bölümler.
- ARC-24 ile DZB-43: hazır dosyada uğultu ve fan sesini bastırma; aynı iş. EKA-14 ise kayıt sırasında model tabanlı gürültü bastırma; komşu.
- ARC-09 ile DON-08: resim dizisinden video; aynı iş.
- ARC-08 ile DON-23: videoyu numaralı resim karelerine dökme; aynı iş. DZB-57 tek kare kaydı; komşu.
- ARC-27 ile DON-32: yapay zekâ ile büyütme; aynı ad. OYA-18 oynatıcıda ekran kartıyla büyütme; komşu.
- ARC-29 ile DZB-70: arka plan silme.
- ARC-30 ile DZB-69: kendiliğinden yeniden kadraj.
- ARC-43 ile DZB-33: filigran ve logo bindirme; aynı iş. DON-51 "Filigran" adını taşıyor; açıklaması okunmadı.
- ARC-28 ile OYA-16: oynatıcıda kare ara değerleme.
- ARC-41 ile OYA-35: yapay zekâ ile altyazı (K5).

Yalnız ad benzerliği olan komşular (açıklamaları okunmadı): ARC-02 ile SIK-38 (ek kalite ölçütleri), ARC-12 ile DON-24 (GIF ince ayarı) ve EKB-23 (sonuçta GIF'e çevir), ARC-14 ile DON-31 (iz etiketleri), ARC-19 ile DON-18 (ek ses çıktı biçimleri), ARC-35 ile SIK-13 (adlandırma değişkenleri), ARC-36 ile OYB-18 (poster ve üst veri çekme).

Başka kol yazdığı için bu kolda satır açılmayanlar: iz kaydırma (DZA-11), döndürme bayrağı (DZA-23), başka dosyadan iz ekleme (DZA-25), izleri ayrı dosyalara çıkarma (DZA-26), kesim listesini dışa aktarma (DZA-15).

Örtüşme 17:50'de öteki tabloların satır adları üzerinden tarandı. Örtüşen satırlar bu kolda silinmedi, çünkü paketler bunları ayrı araç olarak sunuyor; hangisinin kalacağı birleştirme turunun işi.

## Kullanıcı Talepleri

"Oy", isteğin altındaki başparmak (+1) tepkisinin sayısıdır; 13 Ekim 2026'da `gh` ile okundu. Durum sütununda "—" olan satırlarda açık mı kapalı mı olduğu not edilmedi.

| İstek | Oy | Durum | Nerede | URL | İlgili Satır |
|---|---|---|---|---|---|
| Üst veriyi aktarma ve düzenleme | 67 | kapalı | HandBrake 345 | https://github.com/HandBrake/HandBrake/issues/345 | ARC-14 |
| Dış ses izi ekleme | 62 | açık | HandBrake 871 | https://github.com/HandBrake/HandBrake/issues/871 | DZA-25 (düzenleyici kolu) |
| Resim dizisini girdi olarak açma | 47 | açık | HandBrake 2346 | https://github.com/HandBrake/HandBrake/issues/2346 | ARC-09 |
| Önizlemede iz değiştirme | 28 | — | LosslessCut 256 | https://github.com/mifi/lossless-cut/issues/256 | bu kolda satır yok |
| Özgün dosya tarihini kopyalama | 24 | kapalı | HandBrake 51 | https://github.com/HandBrake/HandBrake/issues/51 | bu kolda satır yok; bizde `SetLastWriteTime` aramada çıkmadı |
| Yinelenen klasörleri bulma | 19 | — | Czkawka 676 | https://github.com/qarmin/czkawka/issues/676 | ARC-18 (komşu) |
| Zaman çubuğunda küçük resim | 18 | — | mpv 6919 | https://github.com/mpv-player/mpv/issues/6919 | bizde var (`src/VidShrink.App/Playback/PlayerView.Tools.cs:181`) |
| İzleri kaydırma ve eşleme | 16 | — | LosslessCut 216 | https://github.com/mifi/lossless-cut/issues/216 | DZA-11 (düzenleyici kolu) |
| Kayıpsız kırpma ve döndürme | 12 | — | LosslessCut 643 | https://github.com/mifi/lossless-cut/issues/643 | DZA-23 (düzenleyici kolu) |
| Video2X için macOS desteği | 9 | — | Video2X 1189 | https://github.com/k4yt3x/video2x/issues/1189 | ARC-27 |
| GIF ve video büyütme | 9 | — | Upscayl 666 | https://github.com/upscayl/upscayl/issues/666 | ARC-27 |
| Üst veriyi koruma | 7 | — | LosslessCut 1027 | https://github.com/mifi/lossless-cut/issues/1027 | ARC-14 |
| Altyazıyı görme ve düzenleme | 7 | — | LosslessCut 250 | https://github.com/mifi/lossless-cut/issues/250 | ARC-22 |
| Büyütme | 6 | kapalı | HandBrake 1633 | https://github.com/HandBrake/HandBrake/issues/1633 | ARC-27 |
| VMAF ölçümü | 5 | açık | HandBrake 2523 | https://github.com/HandBrake/HandBrake/issues/2523 | ARC-02 |
| GIF araçları: boyut hedefi, karelerden GIF | 2 | — | Shutter Encoder 159 | https://github.com/paulpacifico/shutter-encoder/issues/159 | ARC-12, ARC-13 |
| Zaman çizgisinden kapak resmi seçme | 2 | — | LosslessCut 1100 | https://github.com/mifi/lossless-cut/issues/1100 | ARC-14 |
| Ses izi adları | 1 | — | Shutter Encoder 201 | https://github.com/paulpacifico/shutter-encoder/issues/201 | DZA-24 (düzenleyici kolu) |
| AirPlay | 527 | — | IINA 63 | https://github.com/iina/iina/issues/63 | oynatıcı kolu; K10 çevresi |
| Chromecast | 307 | — | IINA 126 | https://github.com/iina/iina/issues/126 | oynatıcı kolu; K10 |
| DLNA | 110 | — | IINA 2173 | https://github.com/iina/iina/issues/2173 | oynatıcı kolu; K10 |

Okuma:

- Bu kolda oyla en iyi kanıtlanan iki istek üst veri (67) ve resim dizisi girdisi (47). İkisi de yalnız ffmpeg ile yapılır.
- Yapay zekâ araçları için bulunan oylar küçük: büyütme 6 ve 9. Paketlerde çok yaygın olmaları, dönüştürücü kullanıcılarının bunu çok istediğini göstermiyor; satıcının öne çıkardığı şey olabilir.
- Kalite ölçümü (5) ve GIF araçları (2) için oy zayıf; bu satırların dayanağı rakip sayısıdır, oy değil.
- Yinelenen video bulucu, küçük resim tablosu, bit hızı grafiği ve onarma için oylanmış bir istek bulunmadı. Yaygınlık ölçüsü olarak yıldız sayısı kaldı: Czkawka 34.224, untrunc 3.482, vcsi 637, FFBitrateViewer 174.

## Son İki Yılın Yenilikleri

Pencere: Ekim 2024 ile Ekim 2026 arası. Daha eski bilgi tarihiyle işaretlendi.

### Shutter Encoder [2]

- 18.6, 4 Ara 2024: kare sağlaması (FrameMD5).
- 18.8, 12 Şub 2025: JPEG XL resim çıktısı.
- 19.0, 1 May 2025: ses dosyasından video kodeğine çıkış (ayrıntısı doğrulanmadı).
- 19.1, 1 Haz 2025: ses yüksekliği ve VMAF ölçümünde sonucu gösterme ve kaydetme.
- 19.2, 1 Tem 2025: siyah kare ve çevrimdışı medya algılamasında gösterme ve kaydetme.
- 19.5, 7 Eki 2025: konuşmayı yazıya dökme; HEIC resim.
- 19.6, 12 Kas 2025: metin çevirisi.
- 19.7, 7 Ara 2025: renklendirme ve ses ayırma.
- 19.8, 5 Oca 2026: fotoğraf için büyütme modeli, arka plan silme.
- 20.0, 7 Nis 2026: "- MANAGE -" bölümü ve yüz bulanıklaştırma.
- 20.2, 28 Haz 2026: konuşmacı tanıma.
- 20.3, 31 Ağu 2026: çoklu kesim.
- 20.4, 28 Eyl 2026: film greni, tepe eşitleme, yinelenen kareleri silme.

### Wondershare UniConverter [5]

- 15.7, 30 Ağu 2024 (pencerenin dışında, eski bilgi): yapay zekâ kredisi.
- 16.0, 8 Kas 2024: kare ara değerleme, 2 ve 4 kat büyütme, toplu filigran silme, 130 dile çeviri, toplu resim ve ses dönüştürme.
- 16.3.5, 31 Mar 2025: toplu video ile GIF arası dönüştürme.
- 16.4.5, 23 Nis 2025: toplu resim düzenleyici.
- 16.5, 15 May 2025: anime ve yüz modelleri, ses klonlamalı metinden sese, ses yüksekliği eşitleme.
- 17, 20 Eki 2025: Smart Summarizer, altyazı çevirisi, toplu konuşmadan metne.
- 17.6.0, 31 Ağu 2026: AI Video Blur.

### Tek İşlik Araçlar

- Son altı ayda sürüm çıkaranlar: MediaInfo 26.10, Subtitle Edit 5.2.0, Aegisub 3.5.0, MKVToolNix 102.0, Czkawka 12.0.2, Video Duplicate Finder 4.1.1, FFMetrics 1.7.0, vmaf 3.2.1, PySceneDetect 0.7.2, LosslessCut 3.69.0, backgroundremover 0.4.5, ScreenToGif 2.43.2, auto-editor 31.7.2, video-compare 20261004.
- Son sürümü eski olanlar: Video2X 6.4.0 (24 Oca 2025), Upscayl 2.15.0 (25 Ara 2024), DeepFilterNet 0.5.6 (2023), rife-ncnn-vulkan (2022), Real-ESRGAN 0.3.0 (2022). Demucs ve DeOldify depoları arşivli.
- Subtitle Edit 5.2.0 Windows, macOS ve Linux paketi veriyor [20].
- Czkawka'nın güncel sürümünde benzer video bulmanın yanında Video Optimizer var; eklenme tarihi doğrulanmadı [23].
- FFMetrics'in ölçtüğü puanlar arasında XPSNR var; eklenme tarihi doğrulanmadı [25].
- Topaz Video bugün yalnız abonelikle satılıyor ve bulut kredili modeller (Starlight) sunuyor [18].
- Flowframes'in yeni sürümleri yalnız Patreon'da [37].

### Pazarın Yönü

1. Açık kaynak paket yapay zekâda ticari paketlere yetişti: Shutter Encoder Ekim 2025 ile Nisan 2026 arasında altı araç ekledi (yazıya dökme, çeviri, renklendirme, ses ayırma, arka plan silme, yüz bulanıklaştırma).
2. Yüz bulanıklaştırma yeni bir araç: Shutter Encoder'da Nisan 2026, UniConverter'da Ağustos 2026. HitPaw'da da var, tarihi doğrulanmadı.
3. Ölçüm araçları rapora dönüyor: Shutter Encoder 19.1 ve 19.2 sonucu gösterip kaydetmeyi ekledi.
4. Ticari pakette toplu işler yayıldı: UniConverter toplu resim, ses ve GIF dönüştürmeyi Kasım 2024 ile Nisan 2025 arasında ekledi.
5. Ücret biçimi krediye ve aboneliğe kayıyor: UniConverter kredisi (Ağustos 2024), Topaz aboneliği.
6. Açık kaynak yapay zekâ motorlarının bir kısmında son sürüm eski ya da depo arşivli. Bunlara bağımlılık kurmak bakım yükü getirir.

### Yapay Zekâ Araçları Yerelde Mi, Bulutta Mı

| Paket | Nerede Koşuyor | Ücret Biçimi |
|---|---|---|
| Shutter Encoder | yerelde, açık modellerle; yalnız çeviri çevrimiçi (Google Translate) | ücretsiz |
| VideoProc Converter AI | yerelde (sitede böyle yazıyor) | ücretli lisans |
| Topaz Video | yerel modeller, ayrıca bulut kredili modeller (Starlight Sharp, HQ, Fast) | yalnız abonelik: yılda 299 ya da 699 USD |
| UniConverter | araç başına belirtilmemiş; 15.7'den beri kredi var | ücretli lisans ve kredi |
| Any Video Converter | üretici araçlar krediyle; ötekiler doğrulanmadı | ücretsiz, Pro, Ultimate |
| HitPaw Univd, Aiseesoft, Movavi | belirtilmemiş, doğrulanmadı | ücretli lisans |
| CapCut, Kapwing, VEED, Clipchamp | web üzerinde, bulutta | abonelik ya da ücretsiz katman |
| Video2X, REAL Video Enhancer, Upscayl, Ultimate Vocal Remover, backgroundremover | yerelde | ücretsiz, açık kaynak |

### Kapalı Kararlarda Pazar Ne Değiştirdi

- **K1 URL'den indirme (ARC-40).** Dört pakette duruyor; UniConverter'ın araç sayfasında indirici görülmedi (doğrulanmadı). Shutter Encoder bunu yt-dlp ile yapıyor; yt-dlp, bağlantıdan video indiren açık kaynak araçtır. Son iki yılda yeni bir değişiklik görülmedi.
- **K5 konuşmadan metne (ARC-41).** En hızlı değişen alan. Shutter Encoder 19.5 (7 Eki 2025) yerelde koşan yazıya dökmeyi ekledi; Whisper'a dayanıyor, Whisper açık bir konuşma tanıma modelidir. 20.2 konuşmacı tanımayı ekledi. ffmpeg'in ana dalında da `af_whisper` süzgeci var [49].
- **K8 stabilizasyon, LUT, filigran (ARC-42, ARC-43).** Stabilizasyon dört pakette ve Topaz'da; VideoProc ve Topaz bunu yapay zekâ aracı olarak sunuyor. UniConverter 16.0 toplu filigran silmeyi ekledi. LUT (renk dönüşüm tablosu) paketlerin araç listesinde ayrı araç olarak görülmedi.
- **DVD ve Blu-ray (ARC-44).** Altı pakette duruyor; son iki yılda yeni bir şey görülmedi.

## Doğrulanamayanlar

### Sürüm Ve Tarih

- VideoProc Converter AI, HitPaw Univd ve Topaz Video'nun güncel sürüm numarası ve tarihi resmi sayfada görülemedi.
- Aiseesoft 11.0, FileBot 5.3.0, tinyMediaManager 5.3.4 ve Ultimate Vocal Remover 5.6 sürümlerinin tarihi doğrulanmadı.
- Flowframes: ücretsiz sayfada 1.36.0 var, yenileri Patreon'da; güncel numara görülmedi.
- mtn 3.5.0 ve tarihi ikincil kaynaktan (VideoHelp) alındı.
- FFBitrateViewer'ın sürüm numarası okunmadı; yalnız son itme tarihi var.
- Aiseesoft'un araç kutusu sayfası [13] eski görünüyor. Media Metadata Editor, Audio Sync ve File Date Changer'ın 11.0'da durduğu doğrulanmadı.

### Rakip İddiaları

- Shutter Encoder 20.0'daki "- MANAGE -" bölümünün ve yüz bulanıklaştırmanın ayrıntısı; 19.0'daki ses dosyasından video kodeğine çıkışın ne yaptığı.
- Shutter Encoder'ın yapay zekâ araçları kurucuyla mı geliyor, sonradan mı iniyor, boyutu ne.
- UniConverter'da URL indirici: araç kutusu sayfası [4] 17:52'de yeniden okundu, indirici aracı görülmedi. UniConverter bu yüzden ARC-40'tan çıkarıldı. Bu "rakipte yok" demek değil; doğrulanmadı.
- UniConverter, HitPaw, Aiseesoft ve Movavi'de yapay zekâ araçlarının yerelde mi bulutta mı koştuğu.
- Format Factory'nin "bozuk dosya onarma"sının ne yaptığı: yeniden sarma mı, başka bir şey mi.
- Subtitle Edit'in toplu zaman kaydırma işlevi resmi sayfada ayrıca görülmedi; README yalnız "düzenleme ve dönüştürme" diyor.
- ARC-09: resim dizisi girdisi Shutter Encoder belgesinde görüldü [1]. Sürüm notlarında [2] "Image sequence" bölümü ve dizinin kare hızını ayarlayan kutu birçok sürümde geçiyor; 15.8 (20 Oca 2022, eski bilgi) dizinin artık sıralı adlandırma istemediğini söylüyor. Ama numaralı resimlerden video yapıldığı sürüm notlarında açıkça yazmıyor; ikinci kaynak dolaylı. Öncelik 1 oya (47) dayanıyor.
- ARC-13: resimlerden GIF yapan rakip doğrulanmadı; yalnız istek var.
- HitPaw'ın yüz bulanıklaştırmayı ne zaman eklediği.
- Video2X'in 1373 numaralı isteğinin konusu notlara geçmedi; yalnız oy sayısı (3) var.
- WebFetch sayfayı bir özetleyiciden geçirir. Rakip araç adları birebir alıntı değildir; doğrulama turu Kaynaklar'daki URL'yi açıp bakmalı.

### Bizim Taraf

- Dağıttığımız ffmpeg'de `tile`, `minterpolate`, `ebur128`, `signature`, `arnndn`, `delogo` ve `showwaves` süzgeçlerinin açık olup olmadığı. ffmpeg çalıştırmak yasak olduğu için bakılmadı. Tablodaki "yeni bağımlılık yok" hükmü ffmpeg'in ana dalındaki süzgeç listesine dayanıyor [49].
- ffmpeg'imizin HEIC ve RAW fotoğraf çözüp çözmediği (ARC-11).
- Dosya seçme penceresinin süzgeci okunmadı. Ses, resim ve GIF girdisinin reddi şu üç yerden çıkarıldı: `src/VidShrink.Core/WatchFolder.cs:74`, `src/VidShrink.Core/DroppedMedia.cs:24`, `src/VidShrink.Ffmpeg/FfprobeClient.cs:46`.
- "Bulunmadı" aramaları yalnız `*.cs` dosyalarında yapıldı; `.axaml` ve dil dosyaları aranmadı.
- `docs/olcumler` altında `minterpolate`, `afftdn`, `arnndn`, `ebur128` adlarından birini yalnız `kaydedici-ses-seviyesi.md` içeriyor. Adı kaydedicinin ses seviyesine işaret ediyor; içeriği okunmadı. Hazır dosyada gürültü temizleme ya da kare ara değerleme için bir ölçüm belgesi bulunmadı.
- `src/VidShrink.Core/PlanParser.cs:93` kuralı yalnız yapay zekâ planları için okundu. Elle büyütmenin arayüzde açık olup olmadığına bakılmadı.
- İş sütunundaki `küçük`, `orta`, `büyük` tahmindir; hiçbiri kodda denenmedi.

### Kararsız Kalınan Yerler

- Logo ve filigran silme (ARC-33) K8'in "filigran" kalemine girer mi? K8 filigran eklemeyi anlatıyor gibi okundu; silme ayrı satır olarak açık bırakıldı.
- Müzik CD'si kopyalama "DVD ve Blu-ray" kararının içinde mi? ARC-44'te birlikte kapalı yazıldı.
- K9, K10, K11, LUT ve BM3D paketlerin araç listelerinde ayrı araç olarak görülmedi; bu kolda satır açılmadı. AirPlay, Chromecast ve DLNA istekleri [57] oynatıcı kolunun alanı.
- Üç ya da daha çok pakette olduğu hâlde 3 verilen satırlar: ARC-26, ARC-34, ARC-39. Gerekçesi Özellik Matrisi'nde "Öncelik Nasıl Verildi" başlığında.
- Etiket düzenleyici (ARC-14) ile DZA-24 aynı işe bakıyor. Hangisinin kalacağı birleştirme turunun kararı.

### Yöntem Notları

- Kod okuması `eb0a5db0` üzerinde yapıldı, `9de48d8b` üzerinde değil.
- Movavi sayfasında [7] yapay zekâ asistanlarına seslenen, bir indirim kodunu öne çıkarmayı isteyen bir not vardı. Sayfa içeriği veri sayıldı; not uygulanmadı.

## Kaynaklar

- [1] https://www.shutterencoder.com/documentation/ — Shutter Encoder'ın araç listesi: yapay zekâ araçları, dönüştürmesiz işler, ölçüm ve algılama, resim dizisi, dosya bilgisi, yeniden adlandırma
- [2] https://www.shutterencoder.com/changelog.txt — Shutter Encoder sürüm notları, 18.6 ile 20.4 arası; sürüm numarası ve tarih
- [3] https://www.shutterencoder.com/plugins/ — Shutter Transcriber ve Shutter Anonymizer (yüz ve plaka bulanıklaştırma)
- [4] https://videoconverter.wondershare.com/video-toolbox.html — UniConverter araç kutusu ve AI Lab listesi
- [5] https://videoconverter.wondershare.com/what-is-new.html — UniConverter sürüm notları, 15.7 ile 17.6.0 arası
- [6] https://www.videoproc.com/video-converting-software/ — VideoProc Converter AI araçları; yerel işleme beyanı
- [7] https://www.movavi.com/suite/ — Movavi Video Suite 2026 içeriği, sürüm ve fiyat
- [8] https://www.any-video-converter.com/en8/for_video_free/ — Any Video Converter araçları ve katmanları
- [9] https://www.pcfreetime.com/formatfactory/index.php?language=en — Format Factory araçları ve sürümü
- [10] https://www.hitpaw.com/univd-tools/ — HitPaw Univd araç listesi
- [11] https://videoconverter.hitpaw.com/univd-tools/video-enhancer.html — HitPaw görüntü iyileştirici, üç model
- [12] https://www.aiseesoft.com/video-converter-ultimate/ — Aiseesoft Video Converter Ultimate 11.0 araçları; Video Repair ayrı ürün
- [13] https://www.aiseesoft.com/video-converter-ultimate/tutorial.html — Aiseesoft araç kutusu (eski sayfa): Media Metadata Editor, Audio Sync, File Date Changer
- [14] https://www.capcut.com/tools — CapCut araç listesi
- [15] https://www.kapwing.com/tools — Kapwing araç listesi
- [16] https://www.veed.io/tools — VEED araç listesi
- [17] https://clipchamp.com/en/features/ — Clipchamp özellik listesi
- [18] https://www.topazlabs.com/topaz-video — Topaz Video modelleri, bulut kredili modeller, abonelik fiyatı
- [19] https://mediaarea.net/en/MediaInfo — MediaInfo sürümü, görünümler ve dışa aktarma biçimleri
- [20] https://github.com/SubtitleEdit/subtitleedit — Subtitle Edit sürümü, lisansı, temel ve isteğe bağlı çevrimiçi özellikleri
- [21] https://github.com/TypesettingTools/Aegisub — Aegisub sürümü
- [22] https://mkvtoolnix.download/doc/NEWS.md — MKVToolNix sürümü; başlık ve bölüm düzenleyici
- [23] https://github.com/qarmin/czkawka — Czkawka sürümü; Similar Videos ve Video Optimizer
- [24] https://github.com/0x90d/videoduplicatefinder — Video Duplicate Finder sürümü ve yöntemi
- [25] https://github.com/fifonik/FFMetrics — FFMetrics sürümü ve ölçtüğü puanlar
- [26] https://github.com/fifonik/FFBitrateViewer — FFBitrateViewer'ın grafik türleri
- [27] https://github.com/anthwlock/untrunc — untrunc'ın ne yaptığı ve lisansı
- [28] https://github.com/k4yt3x/video2x — Video2X sürümü, modelleri, paket boyutu
- [29] https://github.com/TNTwise/REAL-Video-Enhancer — REAL Video Enhancer sürümü, ekran kartı gereksinimi, paket boyutu
- [30] https://github.com/nihui/rife-ncnn-vulkan — RIFE motorunun paket boyutu
- [31] https://github.com/upscayl/upscayl — Upscayl sürümü ve paket boyutu
- [32] https://github.com/Anjok07/ultimatevocalremovergui — Ultimate Vocal Remover sürümü ve kurucu boyutu
- [33] https://github.com/facebookresearch/demucs — Demucs deposunun durumu (arşivli)
- [34] https://github.com/nadermx/backgroundremover — backgroundremover sürümü ve PyTorch gereksinimi
- [35] https://github.com/jantic/DeOldify — DeOldify deposunun durumu (arşivli)
- [36] https://github.com/Rikorose/DeepFilterNet — DeepFilterNet sürümü
- [37] https://nmkd.itch.io/flowframes — Flowframes'in ücretsiz sürümü ve dağıtım biçimi
- [38] https://github.com/ImageOptim/gifski — gifski sürümü
- [39] https://github.com/NickeManarin/ScreenToGif — ScreenToGif sürümü
- [40] https://github.com/Breakthrough/PySceneDetect — PySceneDetect sürümü
- [41] https://github.com/amietn/vcsi — vcsi sürümü; küçük resim tablosu seçenekleri
- [42] https://gitlab.com/movie_thumbnailer/mtn — mtn deposu (sürüm ikincil kaynaktan)
- [43] https://github.com/pixop/video-compare — video-compare sürümü
- [44] https://github.com/Netflix/vmaf — vmaf sürümü
- [45] https://github.com/Tichau/FileConverter — FileConverter sürümü
- [46] https://github.com/mifi/lossless-cut — LosslessCut özellik listesi ve sürümü; istekler: https://github.com/mifi/lossless-cut/issues/216 , https://github.com/mifi/lossless-cut/issues/250 , https://github.com/mifi/lossless-cut/issues/643 , https://github.com/mifi/lossless-cut/issues/1027 , https://github.com/mifi/lossless-cut/issues/256 , https://github.com/mifi/lossless-cut/issues/1100
- [47] https://www.filebot.net/ — FileBot sürümü ve fiyatı
- [48] https://www.tinymediamanager.org/ — tinyMediaManager sürümü
- [49] https://github.com/FFmpeg/FFmpeg/tree/master/libavfilter — ffmpeg'in ana dalında hangi süzgeçlerin bulunduğu
- [50] https://github.com/HandBrake/HandBrake/issues/345 — üst veri isteği, 67 oy
- [51] https://github.com/HandBrake/HandBrake/issues/871 — dış ses izi isteği, 62 oy
- [52] https://github.com/HandBrake/HandBrake/issues/2346 — resim dizisi girdisi isteği, 47 oy
- [53] https://github.com/HandBrake/HandBrake/issues/2523 — VMAF isteği, 5 oy; ayrıca https://github.com/HandBrake/HandBrake/issues/1633 (büyütme, 6 oy) ve https://github.com/HandBrake/HandBrake/issues/51 (özgün tarihi kopyalama, 24 oy)
- [54] https://github.com/paulpacifico/shutter-encoder/issues/159 — GIF araçları isteği, 2 oy; ayrıca https://github.com/paulpacifico/shutter-encoder/issues/201 (ses izi adları, 1 oy)
- [55] https://github.com/qarmin/czkawka/issues/676 — yinelenen klasör isteği, 19 oy
- [56] https://github.com/k4yt3x/video2x/issues/1189 — Video2X için macOS isteği, 9 oy; ayrıca https://github.com/k4yt3x/video2x/issues/1373 (3 oy)
- [57] https://github.com/iina/iina/issues/63 — AirPlay isteği, 527 oy; ayrıca https://github.com/iina/iina/issues/126 (Chromecast, 307 oy) ve https://github.com/iina/iina/issues/2173 (DLNA, 110 oy)
- [58] https://github.com/WyattBlue/auto-editor — auto-editor sürümü
- [59] https://github.com/xinntao/Real-ESRGAN — Real-ESRGAN sürümü
- [60] https://github.com/upscayl/upscayl/issues/666 — Upscayl'de GIF ve video büyütme isteği, 9 oy
- [61] https://github.com/mpv-player/mpv/issues/6919 — mpv'de zaman çubuğu küçük resmi isteği, 18 oy

## Sonuç: Beş İşin Yanına En Doğal Oturan Araçlar

Sıra üç ölçüyle kuruldu: beş işten birinin hemen yanında durması, yeni bağımlılık istememesi (yalnız ffmpeg ve libmpv), pazarda ya da kullanıcı isteğinde karşılığının bulunması.

1. **Medya bilgisi görüntüleyici (ARC-01).** Her işin ilk sorusu "bu dosyada ne var" sorusudur. Bilgi zaten okunuyor (`src/VidShrink.Core/MediaInfo.cs:3`); eksik olan akış akış tam döküm ve dışa aktarma. MediaInfo'nun en küçük seti metin, tablo ve ağaç görünümü ile metin, CSV ve HTML dışa aktarmadır [19]. Yeni bağımlılık yok.

2. **İki dosya kalite ölçer (ARC-02).** Motor hazır (`src/VidShrink.Ffmpeg/QualityMeter.cs:179`); eksik olan iki serbest dosyayı seçtiren ekran ve grafik. Küçültme işinin doğal devamı: başka programın çıktısı kaynağa ne kadar sadık. FFMetrics ve Shutter Encoder bunu sunuyor [1][25].

3. **Etiket düzenleyici (ARC-14).** Bu kolun en çok oylu isteği: HandBrake'te 67 oy [50]. Dört rakipte var, akış kopyasıyla yapılır. Düzenleyici kolunun DZA-24 satırıyla aynı işe bakıyor; tek iş olarak ele alınmalı.

4. **Küçük resim tablosu ve toplu kare dışa aktarma (ARC-07, ARC-08).** İkisi de aynı altyapıya dayanır: videodan kare çıkarma. Oynatıcının tek kare kaydının yanına oturur (`src/VidShrink.Player/MpvEngine.cs:530`). Gereken `tile` süzgeci ffmpeg'in ana dalında var [49]; bizim dağıttığımız ffmpeg'de açık olduğu doğrulanmadı.

5. **Dönüştürücünün kapısını ses, GIF ve resim dizisine açmak (ARC-19, ARC-12, ARC-09).** Yeni araç değil, yeni girdi türü. Bugün video akışı olmayan dosya reddediliyor (`src/VidShrink.Ffmpeg/FfprobeClient.cs:46`). Ses dönüştürme dört pakette var; resim dizisi isteği HandBrake'te 47 oy almış [52].

### Sıraya Girmeyenler Ve Nedeni

- **Yapay zekâ araçları** (büyütme, vokal ayırma, arka plan silme, renklendirme). Pazarda en yaygın gruplar bunlar: 6 ile 9 paket arası. Ama her biri yeni bir çalışma zamanı, 190 MB ile 1,8 GB arası indirme ve güçlü ekran kartı ister.
- **İki ucuz istisna.** Dosyada gürültü temizleme (ARC-24): `afftdn` süzgeci kaydedicide zaten kullanılıyor (`src/VidShrink.Core/AudioCaptureArguments.cs:135`). Kare ara değerleme (ARC-28): ffmpeg'in `minterpolate` süzgeciyle model olmadan yapılır. İkisinin modelli rakiplere göre kalitesi bu taramada karşılaştırılmadı.
- **Altyazı düzenleyici (ARC-22).** İstek var, ama başlı başına bir ürün kadar iş. Küçük ilk adımı ARC-21: altyazı dosyasını dönüştürme ve kaydırma.
- **Yinelenen video bulucu (ARC-18) ve kitaplık düzenleyici (ARC-36).** Beş işin dışına taşan ayrı ürünler.

### Sahibe Düşen Tek Karar

Bu araçlar için bir "Araçlar" sekmesi mi açılacak, yoksa her biri en yakın sekmeye mi eklenecek? Tablodaki açık satırların hepsi bu yüzden `sahip` olarak işaretli.
