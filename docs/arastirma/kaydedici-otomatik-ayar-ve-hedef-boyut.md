# Kaydedici — Otomatik En İyi Ayar, Hedef Boyut ve Lisans

Tarih: 13 Eylül 2026. Alt ajan raporu, ham hâliyle.

Ajanın kendi çıktı dosyası boş kaldı; metin oturum dökümündeki görev bildiriminden
birebir çıkarıldı, yalnız HTML kaçışları düz karşılıklarına çevrildi.
Ajana verilen istemin birebir metni dökümde bulunamadı — konu başlıkları şunlardı:
otomatik en iyi ayar (OBS sihirbazı, Bandicam kalite ölçeği, ShareX varsayılanları,
Game Bar / ShadowPlay / Camtasia / Snagit / Action! / Screenpresso / Loom / ScreenRec),
hedef dosya boyutu ve hedef süre, lisans ve patent yükü.

## Ajanın dönüşü

Araştırma tamam. Üç paralel alt ajan + OBS kaynak kodunun doğrudan okunması ile toplandı.

---

# 1) OTOMATİK EN İYİ AYAR

## OBS Auto-Configuration Wizard — kaynak kodundan birebir

Kaynak: `frontend/wizards/AutoConfig.cpp` ve `AutoConfigTestPage.cpp`
https://github.com/obsproject/obs-studio/blob/master/frontend/wizards/AutoConfigTestPage.cpp

**Donanım kodlayıcı yoklaması** — sihir yok, kayıtlı kodlayıcı türleri sayılıyor (`obs_enum_encoder_types`) ve kimlik dizgileri eşleştiriliyor:

- `ffmpeg_nvenc` → NVENC var
- `obs_qsv11` → Intel QSV var
- `h264_texture_amf` → AMD AMF/VCE var
- `com.apple.videotoolbox.videoencoder.ave.avc` → Apple VideoToolbox (yalnız macOS 13.0+)

**Seçim sırası sabit ve kalitatif değil, hiyerarşik**: NVENC → QSV → Apple → AMD; hiçbiri yoksa x264. Aynı sıra hem yayın hem kayıt kolunda tekrarlanıyor.

**Yazılım/donanım tercihi**: `preferHardware = nvencAvailable || appleAvailable || os_get_physical_cores() <= 4`. Yani 4 çekirdek ve altında koşulsuz donanıma gidiyor.

**Bit hızı tahmin formülü** (kodda "tamamen keyfi bir denklem" diye yazılı):

```
areaVal = (cx * cy)^0.85
EstimateBitrateVal = areaVal * sqrt(fps^1.1)
alt sınır  = değer / (Val(1920,1080,60) / 5800)
üst sınır  = değer / (Val(1280,720,30) / 3000)
```

Bu iki çapa (1080p60 → 5800 kbps alt, 720p30 → 3000 kbps üst) formülün içine gömülü. Formülü koşturdum, VidShrink'in varsayılan tablosu için doğrudan kullanılabilir:

| Çözünürlük | fps | alt (kbps) | üst (kbps) |
|---|---|---|---|
| 3840×2160 | 30 | 12871 | 19419 |
| 3840×2160 | 60 | 18844 | 28431 |
| 2560×1440 | 30 | 6460 | 9747 |
| 2560×1440 | 60 | 9459 | 14270 |
| 1920×1080 | 30 | 3962 | 5977 |
| 1920×1080 | 60 | 5800 | 8751 |
| 1280×720 | 30 | 1988 | 3000 |
| 1280×720 | 60 | 2911 | 4392 |
| 854×480 | 30 | 999 | 1507 |
| 854×480 | 60 | 1462 | 2206 |
| 640×360 | 30 | 612 | 923 |
| 640×360 | 60 | 896 | 1352 |

**Donanım kodlayıcı cezası**: NVENC dışındaki donanım kollarında minimum bit hızı tahmini `×114/100` ile şişiriliyor — koddaki yorum: "çoğu donanım kodlayıcının kalite/bit hızı oranı iyi değil; NVENC son nesillerdeki iyileşme yüzünden istisna."

**CPU yoklaması — çözünürlük/fps tavanı** (yazılım kodlama kolunda, `pcores`/`lcores`):

- `lcores > 8 || pcores > 4` → tavan `1920×1200×60`
- `lcores > 4 && pcores == 4` → tavan `1920×1080×60`
- `pcores == 4` → tavan `1920×1080×30`
- diğer ("toaster") → tavan `960×540×30`

Donanım kolunda tavan daha kaba: `pcores >= 4` → `1920×1200×60`, aksi halde `1280×720×30`.

**Gerçek test döngüsü**: 2160/1440/1080/720/480/360/240 × {60,30} sırasıyla her biri **5 saniye** boyunca `null_output`'a kodlanıyor; **10'dan fazla kare atlarsa** o kademe eleniyor, en fazla **3 başarılı sonuç** toplanıyor. Yani "bant genişliği testi" değil, gerçek kodlama denemesi.

**Bant genişliği testi** (yalnız yayın kolu): başlangıç bit hızı `startingBitrate = 2500` kbps; sunucuya gerçek veri gönderilip `bitrate = toplam_bayt × 8 / süre` ölçülüyor. Kare düşerse veya ölçülen değer başlangıcın **%75'inin altında** kalırsa, ölçülenin **%70'i** kabul ediliyor. Sunucu seçiminde iki aday arası fark **400 kbps'ten azsa** gecikmesi düşük olan kazanıyor.

**Kayıt kolunda bant genişliği hiç ölçülmüyor** — CRF 20, profile high, preset veryfast ile test ediliyor; sonuçta `recordingQuality = High` sabitleniyor.

## OBS'in varsayılan sayıları (kaynak kodu)

- Basit çıktı varsayılan video bit hızı **6000 kbps**, ses **160 kbps**, preset **veryfast**, kayıt kalitesi **"Stream"** (`frontend/widgets/OBSBasic.cpp:752-757`)
- Varsayılan kare hızı **30** (`FPSCommon = "30"`)
- x264 eklentisi varsayılanları: bitrate **6000**, **CRF 23**, rate_control **CBR**, preset **veryfast** (`plugins/obs-x264/obs-x264.c:100-116`)
- NVENC eklentisi varsayılanları: bitrate **10000**, **CQP 20**, target_quality **20**, preset **p5**, tune **hq**, multipass **qres**, B-kare **2**, rate_control **cbr** (`plugins/obs-nvenc/nvenc-properties.c:37-61`)

## OBS kayıt kalite kademeleri — somut CQP/CRF

`frontend/utility/SimpleOutput.cpp:427-600`:

```
crf = CalcCRF(ultra_hq ? 16 : 23)

CalcCRF:  crossDist = sqrt(cx² + cy²)
          azalt = (1 - min(2000, crossDist)/2000) * 10
          sonuç = crf - (int)azalt        // lowCPUx264 ise ayrıca -2
```

- **"Yüksek Kalite" (HQ)** → temel CRF/CQP **16**
- **"Ayırt edilemez" değil, normal kol** → temel **23**
- Bu tek sayı x264'te `crf`, NVENC'te `cqp`, AMD'de `cqp` (+preset "quality"), QSV'de `cqp` (AV1'de `icq_quality`) olarak **aynen** kullanılıyor.
- Apple VideoToolbox'ta ise 0-100 ölçeğine geçiliyor: kodda "magic numbers" yorumuyla **ultra_hq ? 70 : 50**
- **Kayıpsız** kol: ffmpeg output, AVI konteyner, **utvideo** + **pcm_s16le**. OBS kılavuzu bunun için **dakikada ~7 GB** diyor: https://obsproject.com/kb/recording-encoder-presets-guide
- Çözünürlük düzeltmesi dikkat çekici: düşük çözünürlükte CRF **düşürülüyor** (kalite artırılıyor), çünkü küçük karede aynı CRF daha kötü görünüyor. 1080p'de (crossDist ≈ 2203 > 2000) düzeltme sıfır, 720p'de (crossDist ≈ 1469) yaklaşık **-2,6 → -2**.

NVENC ileri seçenekleri: CQP aralığı **0-51**, I:P:B ayrı QP, VBR+Target Quality kipi, 4K+ hızlı preset'lerde otomatik bölünmüş kodlama: https://obsproject.com/kb/advanced-nvenc-options

## ShareX — kaynak kodundan kesin varsayılanlar

`ShareX.ScreenCaptureLib/ScreenRecording/FFmpegOptions.cs`
https://github.com/ShareX/ShareX/blob/master/ShareX.ScreenCaptureLib/ScreenRecording/FFmpegOptions.cs

- Varsayılan video kodeki **libx264**, ses **libvoaacenc**
- x264: **CRF 28**, preset **ultrafast**, bit hızı kipi kapalı; açılırsa **3000 kbps**
- NVENC: preset **p4**, tune **ll** (low latency), **3000 kbps** — dikkat: kayıtta NVENC'i **CQ ile değil `-b:v` ile** sürüyor, `-cq` hiç üretilmiyor
- QSV: preset **fast**, 3000 kbps · AMF: usage **lowlatency**, quality **speed**, 3000 kbps · XviD: **qscale 10**
- Ses: AAC **128 kbps**, Opus **128 kbps**, Vorbis qscale 3, MP3 qscale 4
- Kare hızı **30 FPS**, GIF **15 FPS**, iki geçişli kodlama **kapalı** (`ShareX/TaskSettings.cs`)
- Üretilen argüman: `-preset <p> -tune zerolatency -crf <n> -pix_fmt yuv420p -movflags +faststart` (`ScreenRecordingOptions.cs`)
- Kayıt sonrası **Video Converter** aracının varsayılanı ayrı: **x264 CRF 23, preset medium** — aynı üründe iki farklı varsayılan: https://github.com/ShareX/ShareX/blob/master/ShareX.Tools/Tools/VideoConverter/VideoConverterOptions.cs

## Bandicam — 0-100 kalite ölçeği

- Varsayılan **kalite 80**, **30 FPS**; ölçek **10-100**: https://www.bandicam.com/support/settings/video/ ve https://www.bandicam.com/how-to-record-video-better-quality/
- Ölçeğin CRF/QP karşılığı hiçbir resmi sayfada yok — **kaynak bulunamadı**. Ama dolaylı ölçüm var (1920×1080, H.264, 30 FPS, 1 dakika): **kalite 40 → 8,9 MB**, **60 → 19,5 MB**, **80 → 34,1 MB**. Doğrusal değil, üstel: https://www.bandicam.com/how-to-record-smaller-file-size/
- Aynı sayfada kodek karşılaştırması (1080p30, kalite 80, 1 dk): **AV1 15-18 MB**, **HEVC 29-36 MB**, **H.264 31-40 MB**
- VBR/CBR: VBR kalite tabanlı varsayılan; CBR'a geçilince referans olarak **3,5 Mbit/s (SD)**, **9,8 Mbit/s (DVD)**, **8-15 Mbit/s (HDTV)** öneriliyor. 1080p60 için resmi varsayılan Mbps — **kaynak bulunamadı**
- Anahtar kare aralığı 1-300 kare: https://www.bandicam.com/support/tips/h264-fourcc/
- Ses: Normal **128 kbps**, Very High **320 kbps**: https://www.bandicam.com/mac/guide/video-settings/
- **Donanım yoklaması**: uygun GPU/sürücü yoksa NVENC seçeneği menüde **hiç gösterilmiyor**. Sürüm eşikleri: H.264 NVENC Bandicam 2.0.0+ (GTX 600+), HEVC 2.4.0+ (GTX 950+), AV1 7.0.1+ (RTX 4060+, yalnız MP4): https://www.bandicam.com/how-to-use-nvidia-nvenc-encoder/

## Xbox Game Bar / Windows Game DVR

- Arayüzde yalnız iki kademe: **Standart** ve **Yüksek**, sayısal bit hızı seçici yok: https://learn.microsoft.com/en-us/answers/questions/3769065/set-video-recording-quality-xbox-game-bar
- API karşılığı `AppCaptureVideoEncodingBitrateMode`: **Custom = 0, High = 1, Standard = 2**: https://learn.microsoft.com/en-us/uwp/api/windows.media.capture.appcapturevideoencodingbitratemode
- **Standart/Yüksek'in Mbps karşılığı Microsoft tarafından yayımlanmıyor — kaynak bulunamadı.**
- Registry (`HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR`): `CustomVideoEncodingBitrate` varsayılan **4.000.000 (4 Mbps)**, tavan **30.000.000 (30 Mbps)**; `CustomVideoEncodingWidth` **1280**/tavan 1920, `Height` **720**/tavan 1080; `AudioEncodingBitrate` **192.000**: https://github.com/FunkyFr3sh/GameDVR_Config/blob/master/GameDVR_ConfigForm.cs
- Bu makinede (Windows 11 Pro 22631) doğruladım: `AppCaptureEnabled = 0` ve özel değerler **hiç yazılmamış** — yani varsayılanlar kullanıcı değiştirene kadar registry'de görünmüyor, koddan okumak yanıltıcı olur.
- Kare hızı yalnız **30/60**: https://www.elevenforum.com/t/change-video-frame-rate-for-game-recording-in-windows-11.18224/
- Kodek kullanıcıya sorulmuyor, cihaz ayarından okunuyor: `XAppCaptureVideoCaptureSettings` → `encoding` alanı **H264 / HEVC**, `colorFormat` SDR/HDR. Belge, ayarların her an değişebileceğini, **kaydı başlatmadan hemen önce yeniden okunması** gerektiğini söylüyor: https://learn.microsoft.com/en-us/gaming/gdk/docs/reference/system/xappcapture/functions/xappcapturegetvideocapturesettings

## NVIDIA ShadowPlay / NVIDIA App

- **Varsayılan bit hızı elle değil, hesaplanıyor**: NVIDIA'nın kendi destek kaydı, varsayılanın "oyun çözünürlüğü, kare hızı ve kullanılan kodeke göre en iyi kalite için önceden hesaplandığını" ve kaydırağın aralığı/adımının **video yapılandırmasına göre uyarlandığını** söylüyor: https://nvidia.custhelp.com/app/answers/detail/a_id/5713/
- **İstenen "çözünürlüğe göre otomatik bit hızı tablosu" NVIDIA tarafından yayımlanmıyor — kaynak bulunamadı.** Dolaşımdaki 30-50 Mbps (1080p/1440p) ve 80-130 Mbps (4K60) değerleri yalnız üçüncü taraf rehberlerden.
- Üst sınır: NVENC ile **4K'ya kadar 130 Mbit/s**: https://en.wikipedia.org/wiki/Nvidia_ShadowPlay
- AV1 NVIDIA App ile geldi, H.264'e göre **%40'a varan verim**; sürüm notunda "AV1 seçildiğinde varsayılan bit hızı ayarındaki sorun giderildi" kaydı var — otomatik hesabın kodeke bağlı olduğunun dolaylı kanıtı: https://www.nvidia.com/en-us/geforce/release-notes/NVAPP/10_0_1/Web/nvapp-v10_0_1-web-release-highlights/
- Kullanıcı kaydı: 1440p'de AV1'e geçince eski 50 Mbps varsayılanın düşmesi: https://www.nvidia.com/en-us/geforce/forums/instant-replay-recording/15/556541/

## Diğer ürünlerin otomatik kolu

- **Camtasia**: varsayılan kayıt kodeki **TSC2** (ekran-optimize, salt CPU); H.264 elle seçiliyor ve donanım hızlandırma **yalnız H.264 kolunda** var. Otomatik geçiş yok: https://support.techsmith.com/hc/en-us/articles/360042592752
- Camtasia Editor varsayılan **30 fps**, özel üretim 1-60 fps; gerçek 60 FPS yakalama için kodlayıcının H.264'e alınması şart. **Değişken kare hızı** yakalıyor — ekran değişmiyorsa kare yazmıyor: https://support.techsmith.com/hc/en-us/articles/360040788091
- **Snagit**: beş kalite kademesi doğrudan FPS tavanı demek — **Very Low 5, Low 10, Med 15, High 30, Very High 60 FPS**. Bit hızı kolu kullanıcıya hiç açılmıyor. VFR yakalıyor: https://support.techsmith.com/hc/en-us/articles/217552078
- Snagit'te hangi kademenin varsayılan olduğu — **kaynak bulunamadı**
- **Action!**: dört donanım kolu (NVENC, NVENC HEVC, QSV, AMF), birden çok NVIDIA kartında hangisinin kullanılacağı seçilebiliyor. İki ayrı ayar: **VIDEO QUALITY** ve **BITRATE (MP4)**; sayısal varsayılanları kılavuzda **verilmemiş**. VFR/CFR seçimi yalnız MP4'te; CFR'de bile kodlayıcı yetişemezse **VFR'ye düşüyor**: https://mirillis.com/pdf/user-manual/Action!-Game-and-screen-recorder-manual.pdf
- **Screenpresso**: varsayılan MP4 (H.264+AAC); HEVC eklendi ama **varsayılan olarak kapalı**: https://www.screenpresso.com/releases/screenpresso-2-2-8/
- **Loom**: "cihazınıza ve bağlantınıza en uygun çözünürlüğe varsayılan olarak ayarlanır" — açıkça otomatik seçim, ama sayı vermiyor. Starter **720p**, uzantı ücretli **1080p**, **4K yalnız masaüstü**: https://support.atlassian.com/loom/docs/manage-your-video-recording-quality/
- **ScreenRec**: ücretsiz **720p** tavan, 60 fps'e kadar, MP4. Bit hızı/kalite kademesi belgelenmemiş — **kaynak bulunamadı**: https://screenrec.com/pricing/ · https://screenrec.com/screen-recorder/
- **FlashBack**: FBR ara biçimi, MP4 ilk dışa aktarımda **Cisco openh264 indiriliyor**; Express 720p, Pro 4K: https://www.flashbackrecorder.com/express — resmi yardım sayfası (`/help/exportandupload/`) **404**, kalite sayıları **kaynak bulunamadı**

## Dış referans tabloları (varsayılan seçmek için sağlam çapa)

YouTube resmi yükleme önerileri (SDR, standart/yüksek kare hızı): 8K **80-160 / 120-240 Mbps**, 2160p **35-45 / 53-68**, 1440p **16 / 24**, 1080p **8 / 12**, 720p **5 / 7,5**, 480p **2,5 / 4**, 360p **1 / 1,5**. HDR'de 1080p **10 / 15**. Ses: mono 128, stereo **384 kbps**, 5.1 512 kbps. Konteyner MP4 + **faststart**, H.264 High profile, 2 ardışık B-kare, kapalı GOP, CABAC, 4:2:0: https://support.google.com/youtube/answer/1722171

Twitch'in resmi tablosu çekilemedi (help.twitch.tv ve stream.twitch.tv erişilemedi) — **doğrulanmış Twitch tablosu: kaynak bulunamadı**.

---

# 2) HEDEF DOSYA BOYUTU / HEDEF SÜRE

## Temel bulgu

**İncelenen on iki kaydediciden hiçbirinde "hedef MB" alanı yok.** Ne OBS, ne Bandicam, ne Camtasia, ne ShareX, ne Snagit, ne Screenpresso, ne Action!, ne FlashBack, ne ShadowPlay, ne Game Bar, ne Loom, ne ScreenRec. Bu pazarda gerçekten boş bir yer.

En yakın muadili **Action!**'ın export penceresindeki tahmini çıkış boyutu göstergesi: "dışa aktarılacak dosyanın tahmini boyutunu gösterir; **yeşil** giriş dosyasından küçük, **kırmızı** büyük olacağını bildirir" — kullanıcı bit hızını kaydırırken boyut canlı güncelleniyor. Aynı PDF.

## Formül — OBS kaynak kodunda birebir var

Tampon bellek tahmininde, yani hedef-boyut hesabının tersi:

```
memMB = saniye × (video_kbps + ses_kbps) × 1000 / 8 / 1024 / 1024
```

`frontend/settings/OBSBasicSettings.cpp:5213`. Çok izli kayıtta ses bit hızı işaretli iz sayısıyla çarpılıyor. Ters çevrilmiş hali istenen formül:

```
video_kbps = (hedef_MB × 8 × 1024 × 1024 / 1000) / süre_sn − ses_kbps
```

Yaygın pratik yazımı (Discord kolu): `(hedef_MB × 8192) / süre_sn − ses_kbps` — 10 MB / 30 sn için ≈ 2730 kbps toplam, eksi ~128 kbps ses: https://zzzachzzz.github.io/blog/hitting-a-target-file-size-with-ffmpeg-perfect-for-discord-clips

Dikkat: 8192 katsayısı MiB→kibibit varsayar, OBS'in `×1000/8/1024/1024` yazımı MB→Mbit karışımıdır; ikisi arasında **%2,4 fark** var. VidShrink'te hangisinin kullanıldığı belgeye yazılmalı yoksa "hedefi 3 MB aştı" hatası buradan gelir.

## HandBrake'in tavrı — dikkate değer karşı görüş

HandBrake **hedef boyut alanını 0.9.6'da kaldırdı ve geri getirmiyor**. Resmi belge: "Bir hedef dosya boyutunu tutturmanız gerçekten gerekmedikçe (ki bunu **tavsiye etmiyoruz**), Constant Quality kullanmanız şiddetle önerilir." Gerekçe: verilen bir kalite düzeyine ulaşmak için gereken bit hızı içerikten içeriğe değişir, çıktı boyutu öngörülemez. Önerilen RF: **SD 20±1, HD 22±1**: https://handbrake.fr/docs/en/latest/technical/video-cq-vs-abr.html

Bu, VidShrink'in tam olarak reddettiği tez — ama hedef boyut kolunun neden iki geçişli kodlama ya da CRF arama gerektirdiğini açıklıyor.

## Kayıt sonrası yeniden kodlama — kimde var

- **Screenpresso**: kayıt sonrası yeniden kodlama **varsayılan akışın parçası**. "Direct MP4 recording" işaretli değilse kendi ekran-optimize codec'iyle AVI kaydediyor, sonra MP4'e çeviriyor. Çıkış biçimi (MP4, MKV/AV1, GIF, APNG, WebP, WMV, OGV, WEBM), önceden tanımlı boyutlar, yeniden boyutlandırma kipi (Fit Max / Max / Exact) ve sıkıştırma kalitesi ayrı ayrı veriliyor. Gizli ayar `ConvertVideoQuality` (varsayılan sayı **verilmemiş**). En önemli tasarım kararı: **"orijinal video yakalaması her zaman korunur; böylece kaliteyi düşürmeden istediğiniz kadar çok kez dönüştürebilirsiniz."** https://www.screenpresso.com/docs/ScreenpressoHelp.pdf
- **Action!**: AVI (kendi **FICV** codec'i) → Export penceresi → H.264 veya H.265 + AAC MP4. Çıkış profili, boyut+kare hızı, video bit hızı alanları; upscale yok. Donanım hızlandırma export'ta da ayrı seçenek. Aynı PDF.
- **Bandicam**: kendisinde yok, ayrı ürün **Bandicut**. Kalite **0-100** ya da doğrudan bit hızı, çözünürlük, FPS; **varsayılan kalite 80**: https://www.bandicam.com/bandicut-video-cutter/support/faq/converting-options/
- **ShareX**: ayrı **Video Converter** aracı, x264 **CRF 23 / preset medium**, NVENC kolunda `-preset p4 -tune hq -profile:v high -b:v`, AV1 için `libsvtav1`: https://github.com/ShareX/ShareX/blob/master/ShareX.Tools/Tools/VideoConverter/VideoConverterOptions.cs
- **FlashBack**: FBR → MP4/AVI/WMV dışa aktarma zorunlu adım: https://www.flashbackrecorder.com/express
- **Camtasia**: Export > Local File > Encodings; hazır ayarlar 480p/720p/1080p, tavan **1920×1080**: https://www.techsmith.com/learn/tutorials/camtasia/export-share/
- **Snagit, OBS, ShadowPlay, Game Bar, Loom, ScreenRec**: kayıt sonrası sıkıştırma yok — **kaynak bulunamadı** / özellik mevcut değil

## OBS'in boyut/süre kolları

- **Rescale Output**: kodlayıcı düzeyinde ölçekleme, tuvalin `Downscale Filter`'ından ayrı ve **sonra** çalışıyor — yayın 720p, kayıt 1080p gibi ayrışmayı sağlıyor: https://obsproject.com/kb/standard-recording-output-guide · forum açıklaması https://obsproject.com/forum/threads/difference-between-rescale-output-and-output-resolution.65138/
- **Automatic File Splitting** (Advanced çıktı kipi): süreye göre, **MB boyutuna göre** veya kısayolla elle bölme. Kaynak kodda `max_size_mb` ve `max_time_sec` alanları: https://github.com/obsproject/obs-studio/blob/master/plugins/obs-ffmpeg/obs-ffmpeg-mux.c#L400 · forum https://obsproject.com/forum/threads/seeking-advice-on-automatically-splitting-obs-recordings-into-smaller-parts-for-easier-uploading.175581/
- **Replay Buffer**: varsayılan **20 saniye / 512 MB** (`RecRBTime = 20`, `RecRBSize = 512`, hem Simple hem AdvOut). Arayüz tahmini bellek kullanımını yukarıdaki formülle canlı gösteriyor ve **sistem belleğinin %75'i** ile tavanlıyor.
- OBS'te **hedef dosya boyutu alanı yok** — kaynak kodda böyle bir hesap geçmiyor.

## Süre/boyut kısıtı olan ürünler

- **Game Bar**: maksimum kayıt süresi 30 dk / 1 sa / **2 sa (varsayılan)** / 4 sa; registry'de 100 ns biriminde QWORD (`MaximumRecordLength`: 2 sa = **72000000000**): https://www.elevenforum.com/t/change-max-recording-length-for-gaming-captures-in-windows-11.17924/ · https://www.top-password.com/blog/change-max-recording-length-for-game-bar-in-windows-11/
- Game Bar geçmiş tamponu `HistoricalBufferLength` varsayılan **15** (arayüz varsayılanı 30 saniye)
- **ShadowPlay Instant Replay**: **15 saniye – 20 dakika** aralığı; SHIELD'in resmi belgesinde varsayılan "son 5 dakika", tavan "20 dakikaya kadar": https://www.nvidia.com/en-us/shield/support/shield-tv-pro/nvidia-share · aralık kaydı https://www.nvidia.com/en-us/geforce/forums/geforce-experience/14/366873/
- Tamponun boyutu doğrudan süre × bit hızı: 20 dk @ 130 Mbps ≈ **19,5 GB** geçici alan. Disk dolarsa tamponlama anında duruyor: https://www.bgr.com/2222483/5-solutions-nvidia-instant-replay-issues/
- **Loom**: ücretsiz planda **5 dakika**, sayaç 5:00'ten geri sayıyor ve 0:00'da kayıt kendiliğinden bitiyor. Business+ sınırsız (uzantı 2 saatte bir soruyor). Ayrıca Starter'da kitaplık **25 video** ile sınırlı: https://support.atlassian.com/loom/docs/how-long-can-i-record/ · https://support.loom.com/hc/en-us/articles/360002212357
- **ScreenRec**: süre sınırı yok, ama ücretsiz bulut deposu **2 GB**; kayıt sırasında eşzamanlı yükleme yapıldığı için bit hızı bant genişliğiyle de kısıtlı: https://screenrec.com/pricing/

Örüntü net: **boyut kısıtı hiçbir üründe doğrudan verilmiyor, hep sürenin türevi olarak ortaya çıkıyor.**

## Hedef boyuta ulaşma teknikleri

- **İki geçişli kodlama**: ffmpeg `-pass 1/2` resmi belgede: https://ffmpeg.org/ffmpeg-all.html
- **`-fs limit_size`**: ffmpeg çıktı dosya boyutu sınırı — ama bu **kesip atıyor**, bit hızını ayarlamıyor: https://ffmpeg.org/ffmpeg.html
- **Capped CRF** (CRF + `-maxrate` + `-bufsize`): kalite hedefini tutuyor, yalnız tavanı aşacağı yerde bit hızını kısıyor. `-maxrate` tek başına hiçbir şey yapmıyor, pencereyi `-bufsize` tanımlıyor; genel öneri bufsize = 2 × maxrate. Örnek: `-crf 22 -maxrate 6M -bufsize 12M`: https://slhck.info/video/2017/03/01/rate-control.html
- Discord sınırları (hedef-boyut aracının en yaygın müşterisi): ücretsiz **10 MB**, Nitro **500 MB** — ikincil kaynak: https://filesize.org/guides/compress-video-for-discord/ ve https://compresto.app/blog/discord-file-compressor . Discord'un **resmi sınır sayfası çekilmedi — kaynak bulunamadı**
- **SVT-AV1 ekran içeriği kipi**: `scm` parametresi — **2 varsayılan** (preset'e göre otomatik algılama), **1** tamamen açık (ekran içeriği araçlarının algılanmasını ve kullanımını büyük ölçüde artırıyor), **0** kapalı. Gerçek zamanlı kayıt için preset **8-12** aralığı öneriliyor: https://wiki.x266.mov/docs/encoders/SVT-AV1 · https://gitlab.com/AOMediaCodec/SVT-AV1

---

# 3) LİSANS VE PATENT

## H.264 / AVC — Via LA (eski MPEG LA)

- Program: https://www.via-la.com/licensing-programs/avc-h-264/ · özet PDF: https://www.via-la.com/wp-content/uploads/avcweb.pdf
- **Yılda ilk 1-100.000 birim → $0.00.** Eşik her yıl sıfırlanıyor ve bağlı şirketler grubunda tek tüzel kişiye tanınıyor. VidShrink ölçeğinde pratik sonuç: **yılda 100 bin kurulumun altında AVC birim ücreti yok.**
- 100.001-5.000.000 birim → **$0,20/birim**; 5.000.001+ → **$0,10/birim**
- Yıllık kurumsal tavan: 2017+ **$9.750.000/yıl**
- **Son kullanıcıya ücretsiz internet videosu** için lisans ömrü boyunca telif alınmayacağı 26 Ağustos 2010'da kalıcılaştırıldı. Birincil via-la.com URL'i bulunamadı; ikincil: https://www.streamingmedia.com/Articles/ReadArticle.aspx?ArticleID=65731
- **2026 değişikliği (yayıncılar için, bizi ilgilendirmiyor ama bilinmeli)**: akış tarafındaki eski $100.000 tavan katmanlı yapıyla değişti, Tier 1 yıllık **$4.500.000**. Yalnız 2026+ yeni lisans alanlara: https://www.tomshardware.com/service-providers/streaming/h264-streaming-license-fees-jump-from-100000-to-4-5-million
- **Access Advance'in AVC ile ilgisi yok** — program listesinde AVC havuzu bulunmuyor: https://accessadvance.com/licensing-programs/

## x264 ve FFmpeg zinciri — asıl risk burada

- x264 **GPLv2**, ayrıca ticari lisansı var: https://www.videolan.org/developers/x264.html · ticari kol: https://x264.org/licensing/ (ilk duyuruda hedef **birim başına $1, 10.000 birim taban**: https://mailman.videolan.org/pipermail/x264-devel/2010-July/007508.html ; güncel liste **kaynak bulunamadı**)
- FFmpeg varsayılan **LGPL v2.1+**; libx264/libx265 devreye girince **tüm ikili GPL** oluyor. LGPL kalmak için `--enable-gpl` **olmadan** derlemek gerekiyor: https://ffmpeg.org/legal.html
- **`ffmpeg.exe`'yi ayrı süreç olarak çağırmak** (VidShrink'in mevcut mimarisi): FSF'nin GPL SSS'i boru/CLI argümanlarını "iki ayrı program" iletişimi sayıyor, ama iletişim "yeterince içli dışlı"ysa tek program sayılabileceğini ekliyor: https://www.gnu.org/licenses/gpl-faq.html#GPLInProprietarySystem · https://www.gnu.org/licenses/gpl-faq.html#MereAggregation
- Yaygın kabul gören güvenli desen, ama **bağlayıcı içtihat kaynak bulunamadı.** Dağıtımda GPL ffmpeg ikilisinin kaynak kodu/teklifi de verilmeli.

## HEVC — neden ağır

- **15 Aralık 2025**: Access Advance, Via LA'nın HEVC/VVC programını satın aldı; artık **Video Codec Licensing LLC (VCL Advance)** yönetiyor: https://accessadvance.com/2025/12/15/access-advance-and-via-licensing-alliance-announce-hevc-vvc-program-acquisition/
- Eski Via LA HEVC oranları: yılda ilk **100.000 birim ücretsiz**, sonrası **Region 1 $0,30 / Region 2 $0,20**, yıllık tavan **$30.000.000**: https://www.via-la.com/licensing-programs/hevc-vvc/
- Access Advance oran tablosu **kamuya açık değil**, istek üzerine veriliyor: https://accessadvance.com/licensing-programs/hevc-advance/ · yıllık **$25.000 royalty credit**: https://accessadvance.com/topic-where-and-when-is-a-royalty-due/
- **1 Ocak 2026'dan itibaren HEVC oranları %25 zamlı** (31 Aralık 2025 sonrası lisans alanlara): https://accessadvance.com/2025/07/21/access-advance-announces-hevc-advance-and-vvc-advance-pricing-through-2030/
- **Velos Media havuzu kapandı** (2022-23), üçüncü taraf patentleri sahiplerine iade edildi: https://www.iam-media.com/article/end-of-velos-joint-licensing-programme-leaves-two-pool-licensing-options-hevc-standard
- Yazılım dağıtımı için ağır olmasının nedeni: AVC'deki gibi geniş muafiyet yok, birden çok lisans vereni ayrı ayrı bulmak gerekiyor, oran tabloları kapalı, SEP dava riski artıyor.
- **Pratik kaçamak**: HEVC'i yalnız kullanıcının donanım kodlayıcısına (NVENC/QSV/AMF) bırakmak, x265 göndermemek. Bunu onaylayan resmi havuz beyanı — **kaynak bulunamadı.**

## AV1 / SVT-AV1 / VP9 — en temiz yol

- **AOMedia Patent License 1.0**: "no-charge, royalty-free, irrevocable patent license"; savunmacı fesih maddesi (dava açarsan hakkın biter): https://aomedia.org/license/patent-license/
- SVT-AV1 kaynak lisansı **BSD 3-Clause Clear** + yanında AOMedia PL 1.0: https://gitlab.com/AOMediaCodec/SVT-AV1/-/blob/master/LICENSE.md · https://gitlab.com/AOMediaCodec/SVT-AV1/-/raw/master/PATENTS.md — **not: patent tarafı Apache-2.0 değil, AOMedia PL 1.0**
- **Sisvel AV1 havuzu iddiası gerçek**: ~2000 patent, lisanslanan **tüketici son ürünleri** (çipset/modül kapsam dışı). Consumer Display Device **€0,32** (uyumlu €0,24), Non-Display **€0,11** (uyumlu €0,08). **Kodlanmış içerik için telif talep edilmiyor**: https://www.sisvel.com/licensing-programmes/audio-and-video-coding-decoding/video-coding-platform-av1/
- Sisvel'in **salt yazılım ürünlerini kapsam dışı bıraktığına dair açık beyan — kaynak bulunamadı** (kapsam "consumer end-products" tanımından çıkarıldı)
- VP9: Google WebM patent grant telifsiz + savunmacı fesih: https://www.webmproject.org/license/additional/ · libvpx **BSD 3-Clause**: https://www.webmproject.org/license/software/ — Sisvel'in VP9 havuzu da var (~1000 patent, aynı platform)

## NVENC SDK — bir tuzak var

- Lisans: https://developer.nvidia.com/nvidia-video-codec-sdk-license-agreement · https://docs.nvidia.com/video-technologies/video-codec-sdk/13.1/license/index.html
- **Ücretsiz**: "royalty-free, fully paid-up" — kurma, kullanma, değiştirme ve **yalnız object code olarak** kendi ürününe gömülü dağıtma hakkı
- **Tuzak**: kaynak kodu ifşasını veya ücretsiz yeniden dağıtımı zorunlu kılan açık kaynak lisansları altında kullanmak **yasak**. Yani **NVENC SDK'yı GPL'li bir ağaca gömmek EULA ihlali.** Bu, "ffmpeg.exe'yi GPL derleyip NVENC ile kullanmak" kombinasyonunu dikkatle ele almayı gerektiriyor.
- **Eşzamanlı NVENC oturum sınırı**: "non-qualified GPU'larda sistem başına **8**": https://docs.nvidia.com/video-technologies/video-codec-sdk/13.0/nvenc-application-note/index.html . Tarihçe: 2 → 3 (2020) → 5 (Mart 2023) → **8** (Ocak/Şubat 2024). SDK 12.2, 8 oturum için **Windows 551.76+ / Linux 550.54.14+** istiyor: https://docs.nvidia.com/video-technologies/video-codec-sdk/12.2/release-notes/index.html
- NVENC'in ücretsiz olması **H.264/HEVC patent yükünü kaldırmıyor** — EULA yalnız NVIDIA yazılımını kapsıyor.

## Intel QSV ve AMD AMF — sorunsuz

- **Intel VPL / oneVPL: MIT** — https://github.com/intel/libvpl/blob/main/LICENSE (eski Media SDK arşivlendi, o da MIT: https://github.com/Intel-Media-SDK/MediaSDK)
- **AMD AMF SDK: MIT** — https://github.com/GPUOpen-LibrariesAndSDKs/AMF/blob/master/LICENSE.txt
- İkisinde de NVENC'teki "açık kaynak lisansı altında kullanma" yasağı yok; GPL/LGPL ağaçla uyumlular.

## ffmpeg kodlayıcı kimlikleri (yoklama için)

`h264_nvenc`, `hevc_nvenc`, `h264_amf`, `hevc_amf`, `hevc_videotoolbox` resmi belgede doğrulandı: https://ffmpeg.org/ffmpeg-all.html · `h264_qsv`, `av1_qsv`, `libsvtav1`, `libvpx-vp9`: https://ffmpeg.org/ffmpeg-codecs.html

---

# VidShrink için üç somut çıkarım

1. **Otomatik ayar için hazır formül OBS'in kaynak kodunda var** ve yukarıdaki tabloyu doğrudan üretiyor. Donanım yoklaması da basit: kodlayıcı kimliklerini sayıp hiyerarşik seç (NVENC → QSV → VideoToolbox → AMF → x264), NVENC dışı donanımda minimum bit hızını ×1,14 şişir. Kalite kolu tek sayı: CRF/CQP **16 (yüksek) / 23 (normal)**, çözünürlüğe göre `CalcCRF` düzeltmesiyle.

2. **Hedef boyut alanı pazarda gerçekten yok.** En yakını Action!'ın yeşil/kırmızı tahmini boyut göstergesi. Formül tartışmasız (`hedef_bit / süre − ses`), asıl karar MB→bit katsayısı (8192 mi, ×1000/8/1024/1024 mü) ve iki geçiş mi capped-CRF mi. HandBrake'in "hedef boyut kötü fikir" tezi belgelenmiş bir karşı görüş; VidShrink bunu bilerek reddettiğini yazmalı.

3. **Lisans tarafında tek gerçek riskli düğüm NVENC SDK'nın "GPL ağaca gömme yasağı"**. AVC patenti 100 bin birim altında ücretsiz, HEVC'ten yazılım kodlayıcı olarak uzak durulmalı, AV1/SVT-AV1 + VP9 lisans açısından temiz, QSV ve AMF MIT.

Yok
