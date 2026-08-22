# Ön araştırma raporu — birleştirilmiş

## 1. Özet

17 tarama dosyasında toplam 50 depo incelendi (sapmalarla birlikte künyede 54 satır var). Temalar:
hedef boyuta kodlayan masaüstü araçları, ffmpeg sarmalayıcıları, donanım kodlayıcı tespiti ve sarmalayıcıları,
AV1/parçalı hedef-kalite kodlama, kalite ölçümü, sahne ve içerik karmaşıklığı ölçümü, medya üstverisi okuma,
frameserver'lar, düzenleyici ve kalite arayüzleri, mobil paylaşım sıkıştırıcıları, psikogörsel kodlayıcı ayarı,
transcode sunucuları, ffmpeg yapıları ve Windows dağıtımı. Bütün künye rakamları 2026-08-22'de `gh api` ile çekildi.

## 2. Kesişen bulgular

- **Hedefi bit hızı yerine kalite ekseninde gütmek.** rigaya'nın üç sarmalayıcısı da varsayılanı kalite tabanlı seçmiş (NVEnc QVBR, QSVEnc ICQ, VCEEnc CQP), bit hızı yalnız tavan. Av1an'ın `--target-quality`'si aynı eksende. HandBrake hedef boyutu kaldırıp RF'e geçmiş. — `rigaya-nvenc.md`, `rigaya-qsvenc.md`, `rigaya-vceenc.md`, `av1an.md`, `svt-av1.md`, `rav1e.md`, `handbrake.md`, `webm-py.md`, `staxrip.md`
- **Ham ffmpeg stderr'ini kullanıcı diline çevirmek.** moviepy kalıp tablosu tutuyor, lossless-cut sınıflandırıp eşleşme yoksa denenecek maddeler listesi veriyor, OpenShot hata sınıflarını eşliyor, FastFlix sabit İngilizce ifade arıyor. Dördü de eşleşmeyi stderr metninde arıyor ve kırılganlığı ya kaynakta itiraf edilmiş ya taramada saptanmış — alınacak olan tablo fikri, arama yöntemi değil. — `ffmpeg-python.md`, `moviepy.md`, `ffmpeg-wasm.md`, `lossless-cut.md`, `fastflix.md`, `aviator.md`, `ffmetrics.md`, `shotcut.md`, `kdenlive.md`, `openshot.md`, `ffmpegcore.md`, `ffmpeg-net.md`, `ffmpeg-autogen.md`
- **İptal, hatadan ayrı bir son durum olmalı.** ffmpeg.wasm `terminate()` ile ayrı işaret üretiyor, lossless-cut iptali ayrı tür olarak yakalayıp hata kutusu göstermiyor, aviator bunu yapmadığı için iptali "bitti" gibi raporluyor (olumsuz örnek). — `ffmpeg-python.md`, `moviepy.md`, `ffmpeg-wasm.md`, `lossless-cut.md`, `fastflix.md`, `aviator.md`, `ffmetrics.md`
- **Gerek yoksa kodlama; hedefe zaten yakınsa kopyala.** Transcoder Validator + `PASS_THROUGH` hiçbir iz işlenmeyecekse işi başlatmadan bitiriyor, ffmpeg-normalize `--threshold` ile "atlandı" diyor, ff4d girdi zaten küçükse uyarıyor, react-native-compressor `minimumFileSizeForCompress` altında dokunmuyor. — `lightcompressor.md`, `deepmedia-transcoder.md`, `react-native-compressor.md`, `pyscenedetect.md`, `auto-editor.md`, `ffmpeg-normalize.md`, `vidcord.md`, `ffmpeg4discord.md`
- **Çıktı bit hızını kaynağınkiyle sınırlamak.** vidcord hedefi kaynak bit hızıyla `min`'liyor, react-native-compressor `min(tavan, kaynak × 0.95)` tavanı koyuyor, LightCompressor 2 Mbps altı girdiyi reddediyor. — `vidcord.md`, `ffmpeg4discord.md`, `lightcompressor.md`, `deepmedia-transcoder.md`, `react-native-compressor.md`
- **Ölçüm sonucunu diske önbelleklemek; anahtara girdiyi ve sürümü yazmak.** PySceneDetect StatsManager CSV'si, auto-editor'ün yol + mtime + argüman anahtarlı LRU önbelleği, rigaya depolarının `GPUFeatures/` veri dosyaları, Jellyfin ve HandBrake'in süreç ömrü boyunca tuttuğu sentinel. — `pyscenedetect.md`, `auto-editor.md`, `ffmpeg-normalize.md`, `rigaya-nvenc.md`, `rigaya-qsvenc.md`, `rigaya-vceenc.md`, `jellyfin.md`, `jellyfin-ffmpeg.md`, `tdarr-handbrake.md`
- **Yetenek sorgusu ffmpeg listesine değil gerçek denemeye dayanmalı.** Jellyfin `-encoders` listesine güveniyor; taramada bunun derleme zamanı yeteneği olduğu saptandı. HandBrake sürücü SDK'sına `MFXVideoENCODE_Query` ile soruyor. Psikogörsel taramada yerelde doğrulandı: tanınmayan `-svtav1-params` anahtarında ffmpeg hata satırı basıyor ama çıkış kodu 0 kalıyor. — `jellyfin.md`, `jellyfin-ffmpeg.md`, `tdarr-handbrake.md`, `svt-av1-psy.md`, `mpv.md`, `lav-filters.md`, `rigaya-nvenc.md`, `rigaya-qsvenc.md`, `rigaya-vceenc.md`
- **Analiz geçişi final geçişten ucuz olabilir ama yapı olarak aynı kalmalı.** HandBrake turbo ilk geçişi (yalnız x264/x265'te destekli), webm.py `speed = max(4, speed)`, SVT-AV1 birinci geçişi daha hızlı preset'te ama aynı mini-GoP yapısında koşuyor. — `handbrake.md`, `webm-py.md`, `staxrip.md`, `av1an.md`, `svt-av1.md`, `rav1e.md`
- **Konteyner süresine güvenmemek, ikincil kaynaklardan doğrulamak.** MediaInfoLib süreyi `FrameCount/FrameRate`'ten yeniden hesaplıyor ve <4 sn dosyada ölçülen bit hızını reddediyor, moviepy `decode_file` ile gerçek süreyi buluyor, yt-dlp `_duration_mismatch` ile beyanı 2 sn toleransla karşılaştırıyor, ffms2 süreyi `FirstTime`/`LastTime`'dan çıkarıyor. — `mediainfolib.md`, `ffms2.md`, `yt-dlp.md`, `ffmpeg-python.md`, `moviepy.md`, `ffmpeg-wasm.md`
- **Ölçüleni tahminden ayrı alanda tutmak, her kırpmayı adıyla loglamak.** ffmpeg-normalize `_constrain(değer, min, max, ad)` ile kırpmayı log'a düşürüyor ve ikinci geçişi de ölçüp hedef–sonuç farkını saklıyor; yt-dlp tahmini boyutu ayrı `filesize_approx` alanında tutuyor; MediaInfoLib tahmin edilen bit hızını ayırıyor. — `pyscenedetect.md`, `auto-editor.md`, `ffmpeg-normalize.md`, `mediainfolib.md`, `ffms2.md`, `yt-dlp.md`
- **Kullanıcıya codec değil varış yeri seçtirmek.** OpenShot hazır ayarları `youtube_shorts`/`instagram_reels`/`tiktok` diye adlandırıyor, Shotcut gelişmiş alanları düğme arkasına katlıyor, Kdenlive CRF'i 0–100 normalize yüzdeye eşleyip ham sayıyı hiç göstermiyor, aviator kasıtlı olarak tek ekranda kalıyor. — `shotcut.md`, `kdenlive.md`, `openshot.md`, `lossless-cut.md`, `fastflix.md`, `aviator.md`, `ffmetrics.md`
- **Kalan süre yüzde üzerinden ve doğrulanabilir yan bilgiyle.** Shotcut geçen süre/yüzde oranı kullanıyor, Kdenlive yanına anlık fps koyuyor, FastFlix hesap tutmazsa uydurma sayı yerine "N/A" basıyor. — `shotcut.md`, `kdenlive.md`, `openshot.md`, `lossless-cut.md`, `fastflix.md`, `aviator.md`, `ffmetrics.md`
- **İş başlamadan çalışan önkoşul kapısı.** media-autobuild_suite hiçbir şey indirmeden ortamı reddediyor ve her mesaj "ne yap" ile bitiyor; webm.py hedefe sığmayan durumu kodlamadan önce üç somut nedenle reddediyor. — `mpc-hc.md`, `media-autobuild-suite.md`, `mpv-winbuild-cmake.md`, `handbrake.md`, `webm-py.md`, `staxrip.md`
- **Ölçümü tüm videoya değil kısa dilimlere yaymak.** CrypticSignal Overview Mode (`--interval` + `--clip-length`), staxrip CompCheck (videonun %5'i, 2 sn bloklar), vidcord'un 1 sn örneği, VapourSynth'in Trim+Splice ile dağınık pencereleri tek klip yapması. Prob çözünürlüğü de sabit küçük genişliğe indirilmeli: PySceneDetect `auto_downscale` 256 px, auto-editor `scale=400:-1,format=gray,gblur`. — `netflix-vmaf.md`, `ffmpeg-quality-metrics.md`, `video-quality-metrics.md`, `handbrake.md`, `webm-py.md`, `staxrip.md`, `vidcord.md`, `ffmpeg4discord.md`, `ffmpeg.md`, `vapoursynth.md`, `avisynthplus.md`, `pyscenedetect.md`, `auto-editor.md`, `ffmpeg-normalize.md`
- **Prob süreçlerinde stdout ve stderr'i eşzamanlı boşaltmak.** Jellyfin bunu boru tıkanması yaşadıktan sonra ekledi (#17429); FFMpegCore ve FFmpeg.NET aynı sorunu farklı yollardan çözüyor. — `jellyfin.md`, `jellyfin-ffmpeg.md`, `tdarr-handbrake.md`, `ffmpegcore.md`, `ffmpeg-net.md`, `ffmpeg-autogen.md`, `ffmpeg-python.md`, `moviepy.md`, `ffmpeg-wasm.md`
- **Karmaşıklığı çözünürlük ve fps'ten bağımsız normalize etmek.** staxrip bit/kare/piksel (`Compressibility`), react-native-compressor bit hızını piksel ve kare oranına bağlıyor, LightCompressor uzun kenara göre ölçek merdiveni tutuyor. — `handbrake.md`, `webm-py.md`, `staxrip.md`, `lightcompressor.md`, `deepmedia-transcoder.md`, `react-native-compressor.md`
- **Çıktının kendisi de doğrulanmalı.** LAV Filters CHANGELOG'u AV1 `av1C` extradata eksikse donanım kod çözmenin devreye girmediğini gösteriyor; MediaInfoLib HDR alanlarını ayrı ayrı tutuyor; ffms2 her üstveri bloğunu kendi "var mı" bayrağıyla veriyor. — `svt-av1-psy.md`, `mpv.md`, `lav-filters.md`, `mediainfolib.md`, `ffms2.md`, `yt-dlp.md`
- **Bozuk/eksik girdide kademeli düşme, istisna değil.** ffms2 `STOP_TRACK` varsayılanı, yt-dlp `fatal=False` ile `None` dönüp işi atlaması, QSVEnc `--fallback-rc`, HandBrake `hb_is_hardware_disabled()` global anahtarı. — `mediainfolib.md`, `ffms2.md`, `yt-dlp.md`, `rigaya-nvenc.md`, `rigaya-qsvenc.md`, `rigaya-vceenc.md`, `jellyfin.md`, `jellyfin-ffmpeg.md`, `tdarr-handbrake.md`

## 3. Alınacak fikirler

| Fikir | Kaynak depo | VidShrink'te hangi dosya | Kaç taramada |
|---|---|---|---|
| Donanımda hedefi kalite ekseninde güt, bit hızını tavan yap | NVEnc/QSVEnc/VCEEnc, Av1an | `ConversionArguments.cs`, `FfmpegArguments.cs`, `PlanCalculator.cs`, `CodecModel.cs` | 3 |
| stderr → eyleme dönüşebilir mesaj tablosu | moviepy, lossless-cut, OpenShot | `EncodeRunner.cs`, `LanguageCatalog.cs` | 4 |
| Gerek yoksa kodlama: pass-through / "atlandı" durumu | deepmedia/Transcoder, ffmpeg-normalize, ff4d | `CompressionStrategy.cs`, `PlanCalculator.cs`, `EncodeRunner.cs` | 3 |
| Ölçüm önbelleği: anahtar = yol + mtime + argüman + sürüm, LRU tahliye | auto-editor, PySceneDetect, rigaya | yeni probe önbelleği, `ComplexityProbe.cs`, `TempCleanup.cs`, `EncoderCapabilities.cs` | 3 |
| Yetenek sınamasını gerçek denemeye dayandır; stderr'de `Error parsing option` ara | HandBrake, SVT-AV1 (yerel ölçüm), Jellyfin | `EncoderCapabilities.cs`, `CodecModel.cs` | 3 |
| Ölçümü kısa dilimlerde yap; pencere içinde eşit dilim, uçlara pay | CrypticSignal, staxrip, PySceneDetect | `ComplexityProbe.cs`, `QualityMeter.cs` | 3 |
| Hedef bit hızını kaynakla sınırla (`min(tavan, kaynak×0.95)`) | vidcord, react-native-compressor, LightCompressor | `PlanCalculator.cs`, `MainWindow.xaml.cs` | 2 |
| İptali ayrı son durum yap, motor durumunu sıfırla | ffmpeg.wasm, lossless-cut | `EncodeRunner.cs`, `TempCleanup.cs`, `MainWindow.xaml.cs` | 2 |
| Süre doğrulama zinciri: `nb_frames/fps`, `boyut×8/bitrate`, <4 sn'de ölçüme güvenme | MediaInfoLib, moviepy, yt-dlp, ffms2 | `FfprobeClient.cs`, `MediaInfo.cs` | 2 |
| Karmaşıklığı bit/kare/piksel olarak normalize et | staxrip, react-native-compressor | `ComplexityProfile.cs`, `ComplexityProbe.cs` | 2 |
| Ucuz ilk geçiş (turbo) + hangi kodlayıcıda geçerli olduğunun tabloda durması | HandBrake, webm.py, SVT-AV1 | `EncoderCapabilities.cs`, `ConversionArguments.cs`, `FfmpegArguments.cs` | 2 |
| Bozuk dosyada kademeli düşme: okunanla devam, eksik alanı işaretle | ffms2, yt-dlp | `FfprobeClient.cs`, `MainWindow.xaml.cs` | 2 |
| Fizibilite ön kontrolü: kodlamadan önce üç somut nedenle reddet | webm.py, media-autobuild_suite | `PlanCalculator.cs`, `Install-VidShrink.ps1`, `MainWindow.xaml.cs` | 2 |
| Hazır ayarları platform adıyla adlandır (`youtube_shorts`, `tiktok`) | OpenShot, Shotcut | `PlanCalculator.cs`, `EncodePlan.cs` | 2 |
| Ham sayı değil bağlamıyla açıklanmış sayı; normalize yüzde kaydıraç | FastFlix, Kdenlive | `MainWindow.xaml`, `MainWindow.xaml.cs` | 2 |
| Gelişmiş alanları katla, varsayılan görünüm tek hedef | Shotcut, aviator | `MainWindow.xaml` | 2 |
| İptalde önce stdin'e `q` yaz, temiz kapanış bekle, timeout'ta `Kill()` | FFMpegCore, FFmpeg.NET | `EncodeRunner.cs` | 2 |
| ffmpeg'i sabitle; sürümü tespit edip hata metnine yaz | jellyfin-ffmpeg, FFmpeg.AutoGen | `ToolLocator.cs`, `EncodeRunner.cs` | 2 |
| Etiketli yayın + son N sürümü tutan budama; sabit URL + SHA-256 | shinchiro/mpv-winbuild-cmake, BtbN | yeni `.github/workflows/release.yml`, `Install-VidShrink.ps1`, `CHANGELOG.md` | 2 |
| Her koşum için ayrı stderr / komut günlüğü dosyası | FastFlix, FFMetrics | `EncodeRunner.cs` | 2 |
| Enterpolasyonlu arama + tüm denemeler arasından aday seçimi | Av1an | `PlanCalculator.cs`, `EncodePlan.cs`, `EncodeRunner.cs` | 1 |
| Ölçülen aşımdan oran düzeltmesi, tur kapaklı, sonda en küçük aşanı bildir | vidcord | `EncodeRunner.cs`, `PlanCalculator.cs`, `EncoderCapabilities.cs` | 1 |
| Kare başına konteyner overhead modeli (sabit çarpan yerine) | staxrip | `PlanCalculator.cs` (`ContainerOverhead`) | 1 |
| Kalibrasyon probu final plandan bayrak mirası alsın (preset hariç) | SVT-AV1 | `CalibrationProbe.cs` | 1 |
| `IsHdr` bool yerine sıralı `DynamicRange`; VFR tespiti ve gösterimi | yt-dlp, MediaInfoLib | `MediaInfo.cs`, `HdrResolver.cs`, `FfprobeClient.cs` | 1 |
| Kodlayıcı başına kırılmış yetenek kaydı + global donanım kapatma anahtarı | HandBrake | `EncoderCapabilities.cs`, `IEncoderAvailability.cs`, `CodecModel.cs` | 1 |
| Belirsizlik bandına göre prob penceresini uzat | vidcord | `CalibrationProbe.cs` (`MinWindows`/`MaxWindows`) | 1 |
| Yerel komşuluğa göre normalize et (global bias düzeltmesi yerine) | PySceneDetect | `ComplexityProbe.cs` (`ComputeScanBias`) | 1 |
| İkinci geçişi de ölç, hedef–sonuç farkını sakla; her kırpmayı adıyla logla | ffmpeg-normalize | `EncodeRunner.cs`, `ComplexityProfile.cs`, `PlanCalculator.cs` | 1 |
| `libvmaf` hizalaması (`shortest=1:repeatlast=0`, `setpts=PTS-STARTPTS`), model yolu Windows kaçışı ve libvmaf sürüm sınaması | Netflix/vmaf, slhck | `QualityMeter.cs`, `EncoderCapabilities.cs`, `QualityMeterTests.cs` | 1 |
| Psikogörsel üçlü (`enable-variance-boost` + `ac-bias` + `qp-scale-compress-strength`) ve preset tablosunu ffmpeg'den doğrulama (libsvtav1'de 12/13 sahte) | SVT-AV1 mainline, yerel ölçüm | `FfmpegArguments.cs`, `CodecModel.cs` (`RelativeBitrateNeed`) | 1 |
| Kodlama sonrası `ffprobe` ile konteyner başlığı doğrulama + uyumluluk sınıfı | LAV Filters | yeni doğrulama adımı, `CodecModel.cs` | 1 |
| Argümanı kova kova üret; geçersiz filtre grafiğini önden reddet | ffmpeg-python | `FfmpegArguments.cs`, `ConversionArguments.cs` | 1 |
| Prob süreçlerinde stdout ve stderr'i eşzamanlı boşalt | Jellyfin (#17429) | `EncoderCapabilities.cs` | 1 |
| Canlı boyut tahmini (hesap tutmazsa "N/A") ve kalan süre yüzde üzerinden + anlık fps | FastFlix, Shotcut, Kdenlive | `PlanCalculator.cs`, `EncodeRunner.cs`, `MainWindow.xaml.cs`, `LanguageCatalog.cs` | 1 |
| Ölçek merdiveni başlangıç tahmini olarak; düşük bit hızlı girdide "tekrar kodlamak bozar" uyarısı | LightCompressor | `PlanCalculator.cs`, `MainWindow.xaml.cs`, `LanguageCatalog.cs` | 1 |
| Prob'u tek süreçte topla: `select=n=K` ile tek çözme, `trim`+`concat` ile tek sanal klip, boyutu `-stats_enc_post`'tan oku, thread bütçesini pencere sayısına göre dağıt | FFmpeg/FFmpeg, VapourSynth, AviSynth+ | `ComplexityProbe.cs`, `CalibrationProbe.cs`, `FfmpegArguments.cs`, `WindowSamplingTests.cs` | 1 |
| GIF paletini geçici dosyasız üret; geçici kaynağı üreten argüman temizlesin | FFMpegCore | `FfmpegArguments.cs`, `EncodeRunner.cs`, `TempCleanup.cs` | 1 |
| Varsayılanların gerekçesi belgede dursun | aviator | `docs/` karar notu | 1 |
| İmza yapılandırmasını depo dışı tek dosyaya bağla, yokluğunda imzasız devam | clsid2/mpc-hc | `Install-VidShrink.ps1`, ileride release workflow | 1 |

## 4. Alınmayacaklar

- **Parçalı paralel kodlama, sahne başına ayrı CRF** (Av1an) — tek dosya + tek ffmpeg akışında kazanç yok, donanımda GOP sınırında kalite sıçraması riski var.
- **Algısal metrik hedefleme, VMAF v1 modelleri, CAMBI/CIEDE/VIF** — libvmaf + model + VapourSynth eklenti yüzeyi; sözleşmemiz boyut.
- **rav1e'nin ibpp tablosu** — katsayılar kendi quantizer ölçeğine bağlı, taşınırsa sessizce yanlış çalışır. **SVT-AV1'in kare içi yeniden kodlama döngüsü** — ffmpeg CLI'den erişilemez. **FFmpeg.AutoGen (libav* P/Invoke)** — in-process çalışmak ffmpeg çökmesini uygulama çökmesine çevirir; README'nin kendisi desteğin sınırlı olduğunu söylüyor.
- **rigaya ikililerini bağımlılık yapmak, `--dynamic-rc`, VCEEnc CQP varsayılanı, QSV'nin eski `--la*` modları** — üç SDK, üç bayrak sözlüğü; ffmpeg karşılıkları yok ya da eskimiş.
- **`Instances` soyutlaması, `BeginErrorReadLine` olay modeli, elle `WaitForExitAsync`, ilerlemeyi stderr `time=` regex'iyle okumak** — mevcut `-progress pipe:1` yolundan geri adım. **Tam DAG filtre makinesi** (ffmpeg-python), **kareleri süreç dışına boruyla taşımak** (moviepy), **wasm/worker mimarisi** — araya bellek kopyası koymanın karşılığı yok.
- **stderr'de substring/regex ile hata sınıflandırma** (lossless-cut, FastFlix, OpenShot) — hata sınıfı kendi katmanımızdan üretilmeli.
- **Hedef boyutu tamamen terk etmek** (HandBrake) — cevabımız düzeltme turları, özelliği silmek değil. **vidcord'un sabit %10 emniyet payı ve tek geçiş ABR'si** — `CrfFitMargin`/`TwoPassUncertainty` ölçüme bağlı, geri adım olur.
- **Kuru bölme + tek atış** (webm.py) ve **ff4d'nin tur kapaksız döngüsü** — biri sistematik olarak bandın bir yanına düşer, diğerinde süre öngörülemez.
- **staxrip'in `GetAutoSize` lineer taraması, 720 px alt sınırı, belgelenmemiş %50 eşiği** — kapalı formülle çözülür, sabitler kendi ölçümümüzle konmalı.
- **Sabit kalite çarpanları ve iki sabit ön ayar** (LightCompressor, Transcoder), **`maxSize=640` varsayılanı** (react-native-compressor) — hedef boyutu olan masaüstü üründe yanlış eksen.
- **VapourSynth / AviSynth+ / MediaInfoLib / FFMS2 / LAV Filters / CrypticSignal'ı bağımlılık yapmak** — Python çalışma zamanı, DLL kaydı, DirectShow ve lisans yüzeyi; alınan davranış, kod değil.
- **Kendi konteyner ayrıştırıcını yazmak** (MediaInfoLib), **kodlamadan önce tam indeks çıkarmak** (ffms2), **codec dizesinden HDR çıkarımı** (yt-dlp) — yerelde `color_transfer` ve `side_data_list` var. **`-encoders` listesine güvenmek ve zaman aşımsız `WaitForExit`** (Jellyfin) — liste derleme zamanı yeteneği; asılı ffmpeg açılışı kilitler.
- **FFmpeg'i çatallayıp yama taşımak** (jellyfin-ffmpeg), **SDK'ya doğrudan P/Invoke** (HandBrake) — üç satıcının ABI'sini üstlenmek olur.
- **`--psy-rd`, `--spy-rd`, `--noise-norm-strength` ve arşivlenmiş svt-av1-psy çatalına bağımlılık** — mainline'da yok, ffmpeg reddediyor.
- **`tune=5` (VMAF)** — unsharp mask uygulayıp ölçüyü kandırıyor. **`--film-grain` varsayılan açık** — oynatma uyumluluğu riski. **CrypticSignal'ın `^|` cmd kaçışı** — `Process`'e argüman dizisi geçiyoruz, kaçış filtreyi bozar.
- **Tüm kareyi/zaman çizgisini tarama** (PySceneDetect, auto-editor) — ölçümüz gerçek kodlama çıktısı, piksel farkı değil. **Kullanıcının makinesinde kaynaktan derlemek** (media-autobuild_suite) — `Install-VidShrink.ps1`'in main.zip + yerel publish zinciri aynı hataya yakın; doğru yön hazır ikili.
- **`{pf}` + admin kurulum varsayılanı ve Inno Setup'a taşınmak** (mpc-hc) — `LOCALAPPDATA\Programs` kazanılmışını geri verir.
- **rdp/ffmpeg-windows-build-helpers'ı bağımlılık yapmak** — etiket yok, Linux/WSL gerektiriyor, README'de ücretli hizmet daveti var.
- **Kdenlive'ın opsiyonel `qualityGroup` kutusu, Shotcut'ın dört sekmeli gelişmiş paneli** — iki kaynaklı durum, doğrulanamaz kombinasyon.
- **`nonfree` ffmpeg varyantı** — hiçbir koşulda; dağıtılamaz.

## 5. Lisans ve dağıtım uyarıları

- **VidShrink bugün MIT lisanslı ama GPLv3 ffmpeg kuruyor.** `Install-VidShrink.ps1:98` WinGet `Gyan.FFmpeg` kuruyor; winget-pkgs manifesti `ffmpeg-9.0-full_build.zip` gösteriyor → GPLv3 full build. Ayrı süreç çağrısı olduğu için kod birleşmesi yok, ama ikiliyi `tools/ffmpeg` altında yeniden dağıtmak GPLv3'ün kaynak sunma ve lisans iletme yükümlülüğünü getirir. (`ffmpeg-builds.md`, `codexffmpeg.md`, `ffmpeg-windows-build-helpers.md`)
- **BtbN varyant matrisi:** `lgpl` = nvenc + qsv + amf + libsvtav1, x264/x265 yok → MIT dağıtımı temizler. `gpl` = hepsi, ürün GPLv3 olur. `nonfree` = fdk-aac, **dağıtılamaz, hiçbir koşulda**. Deponun MIT lisansı **betikleri** kapsar, ikilinin lisansı varyanttan gelir — "MIT depo, ikili de MIT" tuzağı. (`ffmpeg-builds.md`, `codexffmpeg.md`, `ffmpeg-windows-build-helpers.md`)
- **BtbN saklama penceresi dar:** son 14 günlük yapı + her ayın son yapısı 2 yıl. Günlük etikete sabitlersen 14 gün sonra 404; `n9.0-latest` gibi sabit isim + SHA-256 kullan. (`ffmpeg-builds.md`, `codexffmpeg.md`, `ffmpeg-windows-build-helpers.md`)
- **GyanD/codexffmpeg deposunda lisans dosyası yok** (`license: null`), tüm yapılar GPLv3; `essentials`'a kaçmak çözmez, üstüne libsvtav1 içermez. Depoda makine-okunur manifest yok, otomatik doğrulama kurulamaz. (`ffmpeg-builds.md`, `codexffmpeg.md`, `ffmpeg-windows-build-helpers.md`)
- **shinchiro/mpv-winbuild-cmake lisanssız yayın yapıyor** (`license: null`, LICENSE yok). GPL bileşenlerden üretilen ikilileri lisans beyanı olmadan dağıtmak taşınamaz bir risk; yayın varlığının yanına lisans metni konmalı. (`mpc-hc.md`, `media-autobuild-suite.md`, `mpv-winbuild-cmake.md`)
- **İmza:** mpc-hc 2.8.0 x64 kurucusu yerelde `Get-AuthenticodeSignature` ile denetlendi → `NotSigned`. Üç depo da imzasız dağıtıyor, ikisinde imza altyapısı bile yok. Bu nişte SmartScreen "bilinmeyen yayıncı" uyarısı varsayılan. Karşı hamle sertifika değil: tek sabit indirme adresi, varlığın yanında hash, ve uyarının geleceğini README'de önceden söylemek. (`mpc-hc.md`, `media-autobuild-suite.md`, `mpv-winbuild-cmake.md`)
- **Alınabilir imza deseni:** mpc-hc `SignTool` satırını `#ifexist "..\signinfo.txt"` ile sarıyor, sertifika argümanları depo dışında, imzasız derleme sessizce çalışıyor. (`mpc-hc.md`, `media-autobuild-suite.md`, `mpv-winbuild-cmake.md`)
- **Lisans alanı yanıltıcı olan depolar:** ffms2 kaynak MIT ama ikili GPL; AviSynth+ GPL-2+ **artı bağlama istisnası**; NVEnc API `NOASSERTION` ama `NVEnc_license.txt` MIT; ffmpeg-normalize `NOASSERTION` ama `LICENSE.md` düz MIT metni (uzantı yüzünden tanınmıyor); Netflix/vmaf `NOASSERTION` ama README BSD+Patent; jellyfin-ffmpeg kökünde 4 ayrı COPYING; OpenShot `NOASSERTION` ama kaynak başlıkları GPL; HandBrake `NOASSERTION`, LICENSE "çoğu dosya GPLv2" diyor. (`mediainfolib.md`, `ffms2.md`, `yt-dlp.md`, `ffmpeg.md`, `vapoursynth.md`, `avisynthplus.md`, `rigaya-nvenc.md`, `rigaya-qsvenc.md`, `rigaya-vceenc.md`, `pyscenedetect.md`, `auto-editor.md`, `ffmpeg-normalize.md`, `netflix-vmaf.md`, `ffmpeg-quality-metrics.md`, `video-quality-metrics.md`, `jellyfin.md`, `jellyfin-ffmpeg.md`, `tdarr-handbrake.md`, `shotcut.md`, `kdenlive.md`, `openshot.md`, `handbrake.md`, `webm-py.md`, `staxrip.md`)
- **Tdarr `LICENSE.md` OSI onaylı değil — tescilli EULA**, kaynak kod deposunda yok. (`jellyfin.md`, `jellyfin-ffmpeg.md`, `tdarr-handbrake.md`)
- **fifonik/FFMetrics kaynaksız ve lisanssız** — bağımlılık olamaz. (`lossless-cut.md`, `fastflix.md`, `aviator.md`, `ffmetrics.md`)
- **LAV Filters GPL-2.0**, VidShrink'in lisansıyla çakışır. (`svt-av1-psy.md`, `mpv.md`, `lav-filters.md`)
- **cmxl/FFmpeg.NET deposunda 64,6 MB ffmpeg.exe işlenmiş** — klon maliyeti ve tedarik zinciri riski. (`ffmpegcore.md`, `ffmpeg-net.md`, `ffmpeg-autogen.md`)

## 6. Doğrulanmamışlar

- ffmpeg kalite bayrağı adları `-cq` (nvenc), `-global_quality` (qsv), `-qvbr_quality_level` (amf) — `ffmpeg -h encoder=...` ile teyit edilmeli — `rigaya-nvenc.md`, `rigaya-qsvenc.md`, `rigaya-vceenc.md`
- `--multipass 2pass-quarter`'ın ffmpeg karşılığının adı ve değerleri — `rigaya-nvenc.md`, `rigaya-qsvenc.md`, `rigaya-vceenc.md`
- rav1e'nin ibpp tablosunun türetiliş açıklaması (`src/rate.rs` içi yorum) — sayısal doğrulama yapılmadı — `av1an.md`, `svt-av1.md`, `rav1e.md`
- HandBrake marka/isim politikası — depoda yok — `handbrake.md`, `webm-py.md`, `staxrip.md`
- Kagami/webm.py PyPI sürüm tarihi — `handbrake.md`, `webm-py.md`, `staxrip.md`
- rdp/ffmpeg-windows-build-helpers'ın "yaklaşık 2 saat" derleme süresi (README iddiası) — `ffmpeg-builds.md`, `codexffmpeg.md`, `ffmpeg-windows-build-helpers.md`
- "6 puan = JND", "hedef VMAF 93" gibi eşikler — Netflix blog kaynaklı, depo belgelerinde geçmiyor — `netflix-vmaf.md`, `ffmpeg-quality-metrics.md`, `video-quality-metrics.md`
- react-native-compressor'ın "WhatsApp gibi sıkıştırır" iddiası — tek Google Sheets bağlantısı, yöntem/örneklem/tarih yok — `lightcompressor.md`, `deepmedia-transcoder.md`, `react-native-compressor.md`
- react-native-compressor'ın "APK'ya 50 KB, FFmpeg ~9 MB ekler" iddiası — depoda ölçüm yok — `lightcompressor.md`, `deepmedia-transcoder.md`, `react-native-compressor.md`
- WhatsApp/Discord'un yükleme sonrası yeniden kodlamasına karşı öneri — üç deponun hiçbirinde yok; H.264+AAC+faststart çıkarımı ortak varsayım, ölçüm değil — `lightcompressor.md`, `deepmedia-transcoder.md`, `react-native-compressor.md`
- Tdarr çalışma zamanı GPU tespiti (kaynak kapalı) ve "1.000.000 dosyalık kütüphanede test edildi" beyanı — `jellyfin.md`, `jellyfin-ffmpeg.md`, `tdarr-handbrake.md`
- Üç .NET sarmalayıcısında ölçülmüş performans iddiası yok; karşılaştırılabilir tek sayı yıldız — `ffmpegcore.md`, `ffmpeg-net.md`, `ffmpeg-autogen.md`

## 7. Depo künyesi


1. rust-av/Av1an · GPL-3.0 · v0.5.2 · 2026-01-04
2. AOMediaCodec/SVT-AV1 · BSD-3-Clause-Clear · v4.2.0 (yalnız tag)
3. xiph/rav1e · BSD-2-Clause · v0.8.1 · 2025-06-16
4. rigaya/NVEnc · MIT (API `NOASSERTION`) · 9.32 · 2026-08-15
5. rigaya/QSVEnc · MIT · 8.27 · 2026-08-15
6. rigaya/VCEEnc · MIT · 9.13 · 2026-08-22
7. rosenbjerg/FFMpegCore · MIT · NuGet 5.4.0 · 2025-10-27 (GitHub tag v1.0.11 · 2019)
8. cmxl/FFmpeg.NET · MIT · v7.4.0 · 2026-03-28
9. Ruslan-B/FFmpeg.AutoGen · MIT (eskiden LGPL) · v8.0.0.1 · 2026-03-14
10. rosenbjerg/Instances · MIT · —
11. mltframework/shotcut · GPL-3.0 · v26.8.1 · 2026-08-01
12. KDE/kdenlive · GPL-3.0 · tag v26.08.0 (GitHub'da yayın yok)
13. OpenShot/openshot-qt · NOASSERTION (kaynak başlıkları GPL) · v3.5.1 · 2026-04-08
14. cyroz1/vidcord · MIT · v7.3 · 2026-08-14
15. zfleeman/ffmpeg4discord · GPL-3.0 · v0.2.3 · 2026-08-19
16. BtbN/FFmpeg-Builds · MIT (betikler) · `latest`, `autobuild-2026-08-22-12-58`
17. GyanD/codexffmpeg · lisans dosyası yok · 9.0.1 · 2026-08-12
18. rdp/ffmpeg-windows-build-helpers · GPL-3.0 · release/tag yok
19. kkroening/ffmpeg-python · Apache-2.0 · v0.1.9 · 2017-11-20 (fiilen bakımsız)
20. Zulko/moviepy · MIT · v2.2.1 · 2025-05-21
21. ffmpegwasm/ffmpeg.wasm · MIT · v12.15 · 2025-01-07
22. FFmpeg/FFmpeg · LGPL-2.1+ (`--enable-gpl` ile GPL-2+) · GitHub'da release yok
23. vapoursynth/vapoursynth · LGPL-2.1 · R79 · 2026-08-07
24. AviSynth/AviSynthPlus · GPL-2+ artı bağlama istisnası · v3.7.5 · 2025-04-21
25. HandBrake/HandBrake · GPLv2 ("çoğu dosya"), API NOASSERTION · 1.11.2 · 2026-06-07
26. Kagami/webm.py · CC0-1.0 · etiketli sürüm yok (son push 2020-08-02)
27. staxrip/staxrip · MIT · v2.52.5 · 2026-08-08
28. mifi/lossless-cut · GPL-2.0 · v3.69.0 · 2026-06-04
29. cdgriffith/FastFlix · MIT · 6.2.1 · 2026-03-21
30. gianni-rosato/aviator · GPL-3.0 · 0.6.0 · 2024-03-12
31. fifonik/FFMetrics · belirtilmemiş (kaynak yok) · v1.7.0 · 2026-05-06
32. Netflix/vmaf · BSD+Patent (API NOASSERTION) · v3.2.0 · 2026-06-20
33. slhck/ffmpeg-quality-metrics · MIT · v3.12.1 · 2026-07-29
34. CrypticSignal/video-quality-metrics · MIT · sürüm yok (`releases/latest` 404)
35. MediaArea/MediaInfoLib · BSD-2-Clause · v26.05 · 2026-05-12
36. MediaArea/MediaInfo · BSD-2-Clause · v26.05 · 2026-05-12
37. FFMS/ffms2 · kaynak MIT, ikili GPL (API NOASSERTION) · 5.0 · 2024-05-28
38. yt-dlp/yt-dlp · Unlicense · 2026.08.19
39. AbedElazizShe/LightCompressor · Apache-2.0 (arşivlenmiş) · 1.3.3 · 2024-08-17
40. natario1/Transcoder · lisanssız (boş kabuk) · sürüm yok
41. deepmedia/Transcoder · Apache-2.0 · v0.11.2 · 2024-11-05
42. numandev1/react-native-compressor · MIT · v2.0.3 · 2026-07-25
43. psy-ex/svt-av1-psy · BSD-3-Clause-Clear (arşivlenmiş) · v3.0.2 · 2025-04-20
44. mpv-player/mpv · NOASSERTION (LGPL/GPL karışık) · v0.41.0 · 2025-12-21
45. Nevcairiel/LAVFilters · GPL-2.0 · 0.83 · 2026-08-17
46. Breakthrough/PySceneDetect · BSD-3-Clause · v0.7.1 · 2026-07-22
47. WyattBlue/auto-editor · Unlicense · 31.5.0 · 2026-08-13
48. slhck/ffmpeg-normalize · NOASSERTION (`LICENSE.md` düz MIT) · v1.41.1 · 2026-07-10
49. jellyfin/jellyfin · GPL-2.0 · v10.11.11 · 2026-06-06
50. jellyfin/jellyfin-ffmpeg · NOASSERTION (LGPL/GPL karışık) · v7.1.4-3 · 2026-06-06
51. HaveAGitGat/Tdarr · tescilli EULA (OSI onaylı değil) · sürüm yok (son commit 2026-08-05)
52. clsid2/mpc-hc · GPL-3.0 · 2.8.0 · 2026-08-10
53. m-ab-s/media-autobuild_suite · GPL-3.0 · release/tag yok
54. shinchiro/mpv-winbuild-cmake · lisans dosyası yok · 20260814

## 8. Sapmalar

- **HaveAGitGat/Tdarr → HandBrake/HandBrake** — Tdarr kaynak kapalı, `LICENSE.md` tescilli EULA; tespit incelenemez
- **natario1/Transcoder → deepmedia/Transcoder** — Boş kabuk: 2 yıldız, lisanssız, yalnız README + docs, sürüm yok
- **fifonik/FFMetrics → cdgriffith/FastFlix** — Depoda kaynak kod yok, lisans boş, konusu boyut hedefi değil kalite ölçümü
- **master-of-zen/Av1an → rust-av/Av1an** — GitHub API kalıcı yönlendirme; aynı depo, yeni sahip adı
- **gianni-rosato/svt-av1-psy → psy-ex/svt-av1-psy** — Yönlendirme; depo arşivlenmiş, README projeyi bitmiş ilan ediyor
- **MediaArea/MediaInfo → MediaArea/MediaInfoLib** — MediaInfo kabuk, mantık lib'de; ikisinin de künyesi alındı
- **shotcut/shotcut → mltframework/shotcut** — `shotcut/shotcut` 404
- **(genel) → AOMediaCodec/SVT-AV1 (GitHub)** — Kanonik depo GitLab; GitHub aynasının 3 issue / 69 yıldızı yanıltıcı
- **(genel) → KDE/kdenlive (GitHub)** — GitHub aynası, issue kapalı; geliştirme invent.kde.org'da
- **(genel) → FFmpeg/FFmpeg (GitHub)** — Ayna; asıl issue izleyici Trac, `v0.6.1` etiketi yanıltıcı
- **(genel) → rdp/ffmpeg-windows-build-helpers** — İncelendi ama bağımlılık olarak alınmadı: tag yok, Linux/WSL gerektiriyor
