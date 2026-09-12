# Ekran Kaydedici Özellik Karşılaştırması — VidShrink / OBS Studio / Bandicam

Tarih: 2026-09-12. Amaç: VidShrink Kaydedici sekmesinin bugünkü yeteneklerini iki olgun
ekran kaydediciyle yan yana koymak ve eksikleri uygulama maliyetine göre kümelemek.

Kural: OBS ve Bandicam satırları yalnız iki programın **kendi** kaynaklarından
(obsproject.com, github.com/obsproject/obs-studio, bandicam.com) okundu; blog listeleri ve
karşılaştırma yazıları kullanılmadı. VidShrink satırları depodaki koddan okundu. Okunamayan
şey "belgede doğrulanamadı" diye yazıldı, tahmin edilmedi.

**Bir uyarı:** OBS'in Auto-Configuration Wizard'ının resmî bir belge sayfası yok —
`obsproject.com/kb/quick-start-guide` konuyu üç cümleyle geçiyor. Bu yüzden aşağıdaki
"Otomatik kip" bölümünün ayrıntısı OBS'in kendi deposundaki iki dosyadan çıkarıldı: arayüz
dil dosyası (sihirbazın ekranda gösterdiği metinler) ve sihirbazın kaynak kodu (hangi testin
ne ölçtüğü). İkisi de resmî depoda.

## Okunan kaynaklar

### OBS Studio

| # | Adres | Ne için |
|---|---|---|
| O1 | https://obsproject.com/kb/quick-start-guide | Sihirbazın amacı ve nereden çalıştırıldığı |
| O2 | https://obsproject.com/kb/obs-studio-overview | Canvas/output ayrımı, kısayol başlıkları, Replay Buffer, Studio Mode, ses aygıtı yuvaları |
| O3 | https://obsproject.com/kb/sources-guide | Kaynak türlerinin tam listesi |
| O4 | https://obsproject.com/kb/display-capture-sources | Display Capture seçenekleri, macOS/Linux kırpma alanları |
| O5 | https://obsproject.com/kb/window-capture-sources | Window Capture seçenekleri ve platform kısıtları |
| O6 | https://obsproject.com/kb/game-capture-source | Game Capture kipleri ve seçenekleri |
| O7 | https://obsproject.com/kb/video-capture-sources | Webcam/yakalama kartı seçenekleri |
| O8 | https://obsproject.com/kb/browser-source | Browser Source |
| O9 | https://obsproject.com/kb/filters-guide | Video ve ses filtrelerinin tam listesi, Crop/Pad |
| O10 | https://obsproject.com/kb/noise-suppression-filter | RNNoise / Speex / NVIDIA gürültü bastırma |
| O11 | https://obsproject.com/kb/audio-video-formats-guide | Kap ve kodek değerlendirmesi |
| O12 | https://obsproject.com/kb/hybrid-mp4 | Hybrid MP4/MOV, bölüm işaretleri |
| O13 | https://obsproject.com/kb/advanced-recording-settings-guide | Önerilen kayıt ayarları tablosu |
| O14 | https://obsproject.com/kb/recording-encoder-presets-guide | Kalite ön ayarları, lossless, duraklatma kısıtı |
| O15 | https://obsproject.com/kb/standard-recording-output-guide | Remux, ses izi seçimi |
| O16 | https://obsproject.com/kb/advanced-nvenc-options | NVENC oran denetimi, preset, tuning, AQ, B-frame |
| O17 | https://obsproject.com/kb/hardware-encoding | NVENC / QSV / AMF / VideoToolbox donanım koşulları |
| O18 | https://obsproject.com/kb/audio-mixer-guide | Advanced Audio Properties, izleme |
| O19 | https://obsproject.com/kb/multiple-audio-track-recording-guide | Çoklu ses izi ataması |
| O20 | https://obsproject.com/kb/surround-sound-guide | Kanal düzenleri, örnekleme |
| O21 | https://obsproject.com/kb/application-audio-capture-guide | Uygulama başına ses |
| O22 | https://obsproject.com/kb/keyboard-shortcuts | Öntanımlı klavye kısayolları |
| O23 | https://obsproject.com/kb/scene-collections , https://obsproject.com/kb/profiles | Sahne koleksiyonları ve profiller |
| O24 | https://obsproject.com/kb/aspect-ratio-guide | Kaynak başına ölçekleme süzgeci |
| O25 | https://github.com/obsproject/obs-studio/blob/master/frontend/data/locale/en-US.ini | Arayüz dizgileri: kodlayıcı listesi, kayıt biçimleri, renk değerleri, sihirbaz metinleri, dosya bölme |
| O26 | https://github.com/obsproject/obs-studio/blob/master/frontend/wizards/AutoConfigTestPage.cpp , https://github.com/obsproject/obs-studio/blob/master/frontend/wizards/AutoConfig.cpp | Sihirbazın ölçüm ve karar kodu |
| O27 | https://github.com/obsproject/obs-studio/wiki/High-precision-color-spaces-(including-HDR) | HDR ve renk uzayı notları |

### Bandicam

| # | Adres | Ne için |
|---|---|---|
| B1 | https://www.bandicam.com/how-to/mode/ | Üç kayıt kipi |
| B2 | https://www.bandicam.com/ | Fare efektleri, çizim, zamanlanmış kayıt, ses |
| B3 | https://www.bandicam.com/game-recorder/ | 4K UHD / 480 FPS, donanım hızlandırma |
| B4 | https://www.bandicam.com/guide/settings-video/ | Kodek listesi, VBR/CBR, kalite yüzdesi, FPS tavanı, boyut ölçekleme, ses biçimi |
| B5 | https://www.bandicam.com/guide/record-audio/ | Cihaz seçimi, Two Sound Mixing, "Save audio tracks separately" |
| B6 | https://www.bandicam.com/how-to/mouse-effects/ | Tıklama efekti, imleç vurgulama, tıklama sesi |
| B7 | https://www.bandicam.com/guide/hotkeys/ | Kısayol listesi, çizim araçları, bölge ön ayarları |
| B8 | https://www.bandicam.com/guide/scheduled-recording/ | Zamanlanmış kayıt |
| B9 | https://www.bandicam.com/guide/settings-general/ | Auto Complete Recording, FPS overlay |
| B10 | https://www.bandicam.com/how-to-use-chroma-key/ | Chroma key (Similarity / Blend) |
| B11 | https://www.bandicam.com/guide/overlay-webcam/ | Webcam bindirmesi, logo/filigran, AI arka plan |
| B12 | https://www.bandicam.com/guide/record-window/ | Pencere kaydı ve kısıtları |
| B13 | https://www.bandicam.com/how-to/video-capture/ | Device Recording: webcam, HDMI, konsol, HDCP kısıtı |
| B14 | https://www.bandicam.com/best-codec-for-recording-software/ | Kodek kıyası, kayıpsız seçenekler |
| B15 | https://www.bandicam.com/how-to-use-nvidia-nvenc-encoder/ , https://www.bandicam.com/how-to-use-intel-quick-sync-video-encoder/ , https://www.bandicam.com/how-to-use-amd-vce-encoder/ | Donanım kodlayıcı koşulları ve AV1 |
| B16 | https://www.bandicam.com/guide/screen-capture/ | Ekran görüntüsü |
| B17 | https://www.bandicam.com/downloads/version_history/ | Dosya bölme (FAT32 4 GB), özelliklerin eklendiği sürümler |
| B18 | https://www.bandicam.com/how-to-record-smaller-file-size/ , https://www.bandicam.com/guide/overview/ | Otomatik ayar yardımcısının yokluğu |
| B19 | https://www.bandicam.com/guide/draw-on-screen/ | Gerçek zamanlı çizim araçları |
| B20 | https://www.bandicam.com/how-to-record-computer-sound-and-microphone/ | Two Sound Mixing kurulumu |
| B21 | https://www.bandicam.com/guide/record-selected-area/ | Bölge seçim kipleri |
| B22 | https://www.bandicam.com/how-to-start-recording-automatically/ , https://www.bandicam.com/how-to-use-command-line-parameters/ | Komut satırı, Görev Zamanlayıcı |

**Not:** Bandicam'in `downloads/changelog` adresi 404 veriyor; sürüm geçmişi
`downloads/version_history/` altında.

### VidShrink tarafında okunan dosyalar

- `src/VidShrink.Core/RecorderArguments.cs`
- `src/VidShrink.Core/AudioCaptureArguments.cs`
- `src/VidShrink.Core/CodecModel.cs` (`QualityArgs`)
- `src/VidShrink.Ffmpeg/RecorderSession.cs`
- `src/VidShrink.Ffmpeg/CaptureDevices.cs`
- `src/VidShrink.App/Recorder/RecorderView.axaml`, `.axaml.cs`, `.Hedef.cs`, `.Ses.cs`, `.Serit.cs`
- `src/VidShrink.App/Recorder/RecorderSettings.cs`
- `tests/VidShrink.Tests/KayitMotoruTests.cs`, `SesGirisiTests.cs`, `KaydediciArayuzTests.cs`, `SesliKayitTests.cs`

## Karşılaştırma tablosu

| # | Özellik | VidShrink'te var mı | OBS Studio | Bandicam | Kaynak |
|---|---|---|---|---|---|
| 1 | Tüm ekran yakalama | Var — `gdigrab -i desktop`, `avfoundation -i <indeks>:none`, `x11grab -i :0.0` | Var — Display Capture (macOS 13+ için ayrı "macOS Screen Capture", Method = Display/Application/Window) | Var — Screen Recording / "Fullscreen" | O4, B1 |
| 2 | Tek pencere yakalama | Kısmen — Windows'ta `-i title=...`, Linux'ta `-window_id`; **macOS'ta reddediliyor** | Var — Window Capture; `Capture Method` (Automatic/BitBlt), `Window Match Priority`, `Client Area`. macOS'ta düşük başarımlı, Display Capture öneriliyor | Var, ama Windows 10/11 gerekiyor; pencere küçültülürse kayıt olmuyor, açılır menüler yakalanmıyor, bu kipte çizim yok | O5, B12 |
| 3 | Bölge (dikdörtgen) yakalama | Var — Win/Linux'ta girdi ofseti, macOS'ta `crop=` filtresi; ölçüler `yuv420p` için çift olmak zorunda | Var — iki yoldan: kaynağın kendi Crop alanları (macOS Display Capture, Linux XSHM/Xcomposite) ve her kaynağa uygulanabilen **Crop/Pad** filtresi | Var — "Rectangle on a screen", Ctrl+1…9 ile boyut ön ayarları, ok tuşlarıyla taşıma | O4, O9, B21, B7 |
| 4 | Fareyi izleyen bölge | Yok | Belgede doğrulanamadı | Var — "Around Mouse" | B21 |
| 5 | Oyun / DirectX-OpenGL-Vulkan kancası | Yok | Var — Game Capture (yalnız Windows): "any fullscreen application" / "specific window" / "foreground window with hotkey"; anti-cheat uyumluluk kancası, üçüncü taraf bindirmelerini yakalama | Var — Game Recording mode, DirectX/OpenGL/Vulkan | O6, B1 |
| 6 | Webcam / yakalama kartı kaynağı | Yok (cihaz listesinde video cihazları okunuyor ama girdi olarak kullanılmıyor) | Var — Video Capture Device: Resolution/FPS, Video Format, Color Space, Color Range, Buffering, ayrı ses aygıtı | Var — Device Recording (Webcam, Xbox/PlayStation, telefon, IPTV, capture card, VHS); HDCP korumalı içerik alınmıyor | O7, B13 |
| 7 | Tarayıcı / medya / metin / renk kaynağı | Yok | Var — Browser (CEF, URL veya yerel dosya, Custom CSS, özel FPS), Image, Media (VLC ile çalma listesi), Text, Color | Belgede doğrulanamadı | O3, O8 |
| 8 | Birden çok kaynağı üst üste bindirme (sahne) | Yok — tek girdi + isteğe bağlı ses | Var — sahne ve kaynak katmanları, Studio Mode, geçişler (Fade, Cut, Swipe, Slide, Luma Wipe) | Var — webcam bindirmesi, logo/filigran, tarih-saat, CPU/bellek göstergesi | O2, B11 |
| 9 | Yazılım kodlayıcılar | Var — `libx264`, `libx265`, `libsvtav1`, `libvpx-vp9` | Var — x264 (ayrıca "low CPU usage preset" kolu ve `Custom Encoder Settings` alanı) | Var — Xvid, MPEG-1, Motion JPEG, YV12, RGB24, H.264 Lossless (High 4:4:4); harici VFW kodekleri (Lagarith, MagicYUV) `[- External Codec -]` ile | O25, B4, B14 |
| 10 | NVIDIA NVENC | Var — `h264_nvenc`, `hevc_nvenc`, `av1_nvenc` | Var — H.264 / HEVC / AV1; GeForce 750 Ti veya 900 serisi ve üstü | Var — H.264 (GTX 600+), HEVC (GTX 950+), AV1 (RTX 40+) | O17, O25, B15 |
| 11 | Intel QuickSync | Var — `h264_qsv`, `hevc_qsv` | Var — arayüzde H.264 ve AV1; Core i 2xxx ve üstü, 4xxx önerilir | Var — H.264 (2. nesil Core), HEVC (Skylake+), AV1 (Intel Arc) | O17, O25, B15 |
| 12 | AMD | Var — `h264_amf`, `hevc_amf` | Var — H.264 / HEVC / AV1 | Var — VCE/VCN, HD 7700+ ("AMD APP" adı eski sürümlerde kullanılıyordu) | O17, O25, B15, B17 |
| 13 | Apple VideoToolbox | **Yok** — `CodecModel.QualityArgs` VideoToolbox kolunda ölçek ölçülmediği için `NotSupportedException` atıyor | Var — H.264 / HEVC (arayüzde AV1 yok); Apple Silicon'da ProRes kaydı da var | Windows ürünü; yok | O17, O25 |
| 14 | Oran denetimi kipleri | Kalite tabanlı tek kol: yazılımda `-crf`, NVENC'te `-rc vbr -multipass fullres -cq`, QSV'de `-global_quality`, AMF'te `-rc cqp`. **CBR / hedef bit hızı kolu kayıtta yok** | Var — NVENC'te `Constant QP`, `CBR`, `VBR`, `Variable Bitrate with Target Quality` (31.0); x264'te CRF, AMD'de CQP, QuickSync'te ICQ; ayrıca lossless | Var — VBR/CBR seçimi + kalite yüzdesi (varsayılan 80) + hazır bit hızı aralıkları 3,5–15 Mbps | O13, O16, B4 |
| 15 | Kodlayıcı ön ayarı / tuning | Var — `ultrafast … medium` (gerçek zamanlı olduğu için `medium`de bitiyor) | Var — x264 `ultrafast`/`veryfast`/`fast`; NVENC P1–P7 + tuning `High Quality` / `Ultra High Quality` / düşük gecikme; AMD "Quality"; QSV "TU4" | Belgede doğrulanamadı | O13, O16, O25 |
| 16 | Profil / keyframe aralığı / B-frame | Yok — argüman üretilmiyor | Var — Profile "High", Keyframe Interval 2, B-frame sayısı, `B-Frame as Reference` (HEVC/AV1), Look-ahead, Adaptive Quantization (eski adıyla Psycho Visual Tuning) | Var — H.264 profili (Auto/Main/High), keyframe aralığı, FourCC | O13, O16, B4 |
| 17 | Kare hızı | Var — serbest tamsayı, tavan `MaxFps = 240`, varsayılan 30 | Var — `Common FPS Values`, `Integer FPS Value`, `Fractional FPS Value` (pay/payda) | "up to 4K UHD (3840×2160), 480 FPS" (oyun sayfası) ile "adjustable up to 1,000 FPS" (ayar sayfası) — iki resmî sayfa aynı sayıyı vermiyor. VFR/CFR seçimi de var | O25, B3, B4 |
| 18 | Çözünürlük ölçekleme (canvas → çıktı) | Yok — yakalanan ne ise o yazılıyor | Var — `Base (Canvas) Resolution` ve `Output (Scaled) Resolution` ayrı; `Downscale Filter`: Bilinear, Bicubic (16 örnek), Lanczos (36 örnek), Area. Kaynak başına ayrıca Point/Bilinear/Bicubic/Lanczos/Area | Var — Full Size, Half Size, Fit Width, Fit Height, 320×240 gibi ön ayarlar, Custom | O25, O24, B4 |
| 19 | Renk biçimi / uzayı / aralığı | Yok — sabit `-pix_fmt yuv420p` | Var — Color Format: NV12, I420, I444, P010, I010, P216, P416, BGRA; Color Space: sRGB, Rec.601, Rec.709, Rec.2100 (PQ), Rec.2100 (HLG); Color Range: **Limited** / Full | Kodek seçimiyle dolaylı (YV12 / RGB24 / High 4:4:4); ayrı bir renk uzayı ayarı doğrulanamadı | O25, O27, B14 |
| 20 | Kayıt kabı seçimi | Yok — çıktı hep `.mp4`, `mp4/mov`da `-movflags +faststart` | Var — MKV, Hybrid MP4/MOV, Fragmented MP4/MOV, MP4, MOV, MPEG-TS, HLS, FLV. MKV kesilse de oynanıyor ama kurgu desteği zayıf; MP4/MOV sonlandırma gerektiriyor ve kesilirse kurtarılamıyor | Var — AVI ve MP4 (MP4 2.1.0'dan beri; AV1 için MP4 zorunlu) | O11, O12, B4 |
| 21 | Kayıt sonrası remux | Yok | Var — File → Remux Recordings; ayrıca `Automatically remux to mp4`. Hybrid MP4/MOV ise parçalı yazıp kendi içinde "soft-remux" ediyor | Belgede doğrulanamadı | O12, O15 |
| 22 | Mikrofon + sistem sesi birlikte | Var — iki girdi `amix=inputs=2:duration=longest:dropout_transition=0` ile tek ize karışıyor | Var — `Desktop Audio` + `Desktop Audio 2` + `Mic/Auxiliary Audio 1–4` yuvaları | Var — birincil (Default Output Device) + ikincil (Microphone) ve "Two Sound Mixing" | O2, O25, B5, B20 |
| 23 | Sesleri **ayrı iz** olarak kaydetme | Yok — tek AAC izi (`-c:a aac -b:a 160k`) | Var — 6 kayıt izi (Track 1–6); atama Advanced Audio Properties'teki `Tracks` sütunundan | Var — "Save audio tracks separately": izler ayrıca ayrı `.wav` dosyaları olarak yazılıyor | O19, O25, B5 |
| 24 | Ses izleme (monitoring) | Yok | Var — `Monitor Off`, `Monitor Only (mute output)`, `Monitoring Enabled`; izleme aygıtı Settings → Audio'da | Belgede doğrulanamadı | O18, O25 |
| 25 | Ses filtreleri | Yok | Var — Compressor, Expander, Gain, Invert Polarity, Limiter, Noise Gate, Noise Suppression (RNNoise / Speex / NVIDIA Broadcast), VST 2.x | Var — gürültü bastırma, stereo→mono, ses seviyesini %200'e kadar yükseltme; gate/compressor doğrulanamadı | O9, O10, B5 |
| 26 | Ses kodeği / örnekleme / kanal düzeni | Sabit — `aac`, 160 kbit/s, kanal düzeni seçilmiyor | Var — Sample Rate ve Channels (Mono … 7.1, 8 kanala kadar); AAC, libfdk_aac, Opus, Vorbis, PCM | Var — MP2 / MP3 / PCM, 96–320 kbit/s, Stereo/Mono, 22.050–48.000 Hz | O20, B4 |
| 27 | Cihaz listesini programdan okuma ve yenileme | Var — `CaptureDevices` dshow/avfoundation/pactl listesini okuyor, "Yenile" düğmesi önbelleği boşaltıyor | Var | Var | `CaptureDevices.cs` |
| 28 | Seçilen cihazın doğrulanması (sessiz yutma yok) | Var — listede yoksa ya da rolü uymuyorsa `UnknownCaptureDeviceException`, kayıt sessize düşmüyor | Belgede doğrulanamadı | Belgede doğrulanamadı | `AudioCaptureArguments.Verify` |
| 29 | Uygulama başına ses yakalama | Yok | Var — Application Audio Capture (BETA), Windows 10 2004+ / 11 | Belgede doğrulanamadı | O21 |
| 30 | Fare imleci kayda girsin/girmesin | Var — `-draw_mouse` / `-capture_cursor` | Var — `Show Cursor` / `Capture Cursor`, üç yakalama kaynağında da öntanımlı açık | Var — "Show mouse cursor" | O4, O5, O6, B6 |
| 31 | Tıklama efekti / imleç vurgulama | Yok | **Yok** — hiçbir resmî kaynak sayfasında ve arayüz dil dosyasında böyle bir seçenek yok | Var — sol tık kırmızı, sağ tık mavi daire; imleç çevresinde sarı halka; ayrıca tıklama sesi (`lclick.wav` / `rclick.wav` değiştirilebiliyor) | O25, B6 |
| 32 | Gerçek zamanlı çizim / vurgulama | Yok | **Yok** — filtre listesinde çizim/annotation yok (video filtreleri: LUT, Chroma Key, Color Correction, Color Key, Crop/Pad, Image Mask/Blend, Luma Key, Render Delay, Scaling, Scroll, Sharpen) | Var — kalem, fosforlu kalem, çizgi, ok, kutu, elips, numaralandırma, metin, silgi, beyaz tahta; Ctrl+Alt+1…0 | O9, B19, B7 |
| 33 | Chroma key / yeşil perde | Yok | Var — Chroma Key ve Color Key filtreleri | Var — webcam bindirmesi için; önizlemeden renk seçimi, Similarity ve Blend ayarı | O9, B10 |
| 34 | Kısayol tuşları | Yok | Var — Start/Stop Recording, Pause/Unpause Recording, Split Recording File, sahne geçişi, kaynak göster/gizle, push-to-talk, Replay Buffer kaydet; ayrıca öntanımlı düzenleme kısayolları | Var — F12 başlat/durdur, Shift+F12 duraklat, F11 ekran görüntüsü, Ctrl+Alt+H bölgeyi göster/gizle, Ctrl+Alt+S/M susturma; hepsi değiştirilebilir | O2, O22, O25, B7 |
| 35 | Zamanlanmış kayıt | Yok | Belgede doğrulanamadı | Var — 100 adede kadar zamanlama; Repeat (One time / Daily / Weekly), Start/End Time, Duration, hedef seçimi, bitince Bandicam'i veya bilgisayarı kapatma. Ayrıca `/record` `/stop` komut satırı ve Görev Zamanlayıcı | B8, B22 |
| 36 | Tekrar arabelleği (Replay Buffer) | Yok | Var — `Maximum Replay Time`, `Maximum Memory`; kaydetmek için kısayol şart. Custom FFmpeg çıktısında ve lossless'ta kullanılamıyor, kayıt duraklatılmışken kaydedilemiyor | Belgede doğrulanamadı | O25, O2 |
| 37 | Duraklat / devam et | Var — parça nazikçe kapatılıyor, devam yeni parça açıyor, sonda `concat` ile yeniden kodlamadan birleştiriliyor | Var — kısıt **kap değil kodlayıcı** kaynaklı: kayıt kalitesi "Same as stream" ya da kodlayıcı "(Use stream encoder)" ise duraklatılamıyor | Var — Shift+F12 | O14, O25, B7 |
| 38 | Otomatik dosya bölme | Yok (iç parçalama var ama ölçüte bağlı değil, duraklatmaya bağlı) | Var — `Automatic File Splitting`: `Split by Time` / `Split by Size` / `Only split manually`, artı `Split Recording File` kısayolu | Yalnız FAT32 için 4 GB'ta bölme belgeli; kullanıcı ayarlı eşik doğrulanamadı | O25, B17 |
| 39 | Süre/boyut ölçütüyle otomatik bitirme | Yok | Belgede doğrulanamadı | Var — "Auto Complete Recording": süre sınırı, dosya boyutu sınırı, sessizlik süresi sınırı, bitince sistemi kapatma | B9 |
| 40 | Ekran görüntüsü alma | Yok | Belgede doğrulanamadı | Var — F11; BMP/PNG/JPG, tekrarlı yakalama 0,1–9999 s | B16 |
| 41 | Çoklu monitör | Kısmen — Windows'ta yalnız bölge ofsetiyle (`gdigrab` ekran indeksi almıyor, `ScreenIndex != 0` reddediliyor); macOS/Linux'ta ekran indeksi var | Var — ekran başına Display Capture; bir ekran için tek kaynak, başka sahnede referansla kullanılıyor | Var — "Multi-monitor recording" | O3, O4, B2, `RecorderArguments.Validate` |
| 42 | Canlı ilerleme okuması | Var — geçen süre, kare sayısı, düşen kare (`drop_frames`); **yüzde yok**, çünkü kaydın süresi baştan bilinmiyor | Var | Var — Game kipinde ekran üstü FPS sayısı (oyun açıkken yeşil, kayıtta kırmızı); F9/F10 ile denetim | `RecorderSession.cs`, B7, B9 |
| 43 | Yarım dosya bildirimi | Var — nazik durdurma `q` ile; zaman aşımında süreç öldürülüyor ve sonuç `Partial` işaretleniyor | Kabı seçtirerek çözüyor: MKV ve Fragmented/Hybrid biçimler sonlandırma gerektirmiyor | Belgede doğrulanamadı | `RecorderSession.cs`, O11 |
| 44 | Ayarların kalıcılığı | Var — `%AppData%/VidShrink/recorder-settings.json`, cihazlar indeksle değil **adla** hatırlanıyor | Var — Profiles (çıktı ayarları) ve Scene Collections (sahne/kaynak/global ses) ayrı ayrı | Var | `RecorderSettings.cs`, O23 |
| 45 | Otomatik ayar sihirbazı | **Yok** | Var — Auto-Configuration Wizard (aşağıda ayrı bölüm) | **Yok** — sistemi ölçüp ayar öneren bir yardımcı resmî sayfalarda bulunamadı; dosya boyutu kılavuzu açıkça elle ayara yönlendiriyor. Yakın duran şeyler ayar değil davranış otomasyonu: Smart Zoom, AI transkripsiyon | O1, O26, B18 |

Tablo satır sayısı: **45** (başlık ve ayraç satırları hariç).

## Otomatik kip için model: OBS Auto-Configuration Wizard

Kaynak: O1 (amaç ve nereden çalıştığı), O25 (sihirbazın ekranda gösterdiği metinler),
O26 (ölçüm ve karar kodu).

KB'deki tek açıklama üç cümle: sihirbaz "OBS Studio'yu ihtiyacına göre eniyiliyor" ve
kararında "ne yapmak istediğini, bilgisayarının donanım kaynaklarını ve (yayın yapıyorsan)
ağ koşullarını" hesaba katıyor; Tools menüsünden her zaman yeniden çalıştırılabiliyor.
Aşağıdaki ayrıntı depodan çıkarıldı.

### Sihirbazın sorduğu

1. **Kullanım amacı** (`Usage Information`) — üç kol:
   - `Optimize for streaming, recording is secondary`
   - `Optimize just for recording, I will not be streaming`
   - `I will only be using the virtual camera`
2. **Video ayarları** (`Video Settings`) — temel (canvas) çözünürlük için `Use Current (WxH)`
   ve algılanan her ekran için `Display N (WxH)`; FPS için `Use Current (N)`,
   `Either 60 or 30, but prefer 60 when possible`, `Either 60 or 30, but prefer high resolution`.
   Sayfa ayrıca canvas çözünürlüğünün çıktı çözünürlüğüyle aynı olmak zorunda olmadığını
   söylüyor.
3. **Akış bilgisi** (yalnız yayın kolunda) — servis, sunucu, anahtar;
   `Estimate bitrate with bandwidth test` ve `Prefer hardware encoding` onay kutuları.

### Ölçtüğü

1. **Donanım kodlayıcı envanteri.** Kayıtlı kodlayıcı kimlikleri taranıyor: `ffmpeg_nvenc`,
   `obs_qsv11`, AMD, Apple VideoToolbox. Hiçbiri yoksa "donanımı yeğle" kutusu kapanıyor.
2. **CPU sınıfı.** Fiziksel ve mantıksal çekirdek sayısına göre bir üst veri hızı sınırı
   seçiliyor:
   - mantıksal > 8 **veya** fiziksel > 4 → 1920×1200×60
   - mantıksal > 4 ve fiziksel = 4 → 1920×1080×60
   - fiziksel = 4 → 1920×1080×30
   - altı → 960×540×30
3. **Bant genişliği testi** (yalnız yayın kolu). 128×128, 60 FPS sahte video; x264 + CBR +
   `veryfast` + keyint 2, ses 32 kbit/s ile servisin sunucularına **gerçek** bir akış
   açılıyor. Her sunucu için elde edilen bit hızı ve bağlanma süresi ölçülüyor. Kare
   düşerse ya da ölçülen hız başlangıç hızının %75'inin altına inerse o sunucunun değeri
   ölçülenin **%70'i** sayılıyor. Sunucu seçiminde 400 kbit/s içindeki farklar eşit
   sayılıp düşük gecikmeli olan kazanıyor. Çıkan sayı `idealBitrate`.
4. **Kodlayıcı stres testi.** Çözünürlük/FPS merdiveni **gerçekten kodlanarak** deneniyor:
   2160, 1440, 1080, 720, 480, 360, 240 yükseklikleri; FPS sabitlenmemişse her biri 60 ve
   30 ile (14 deneme, sabit FPS'te 7). Her deneme yaklaşık **5 saniye** koşuyor ve
   **atlanan kare ≤ 10** ise başarı sayılıyor. En çok **3 başarılı sonuç** toplanıyor.
   Üst veri hızı sınırını aşan bileşimler hiç denenmiyor. Yayın kolunda ek bir süzgeç var:
   bir bileşim için kestirilen alt bit hızı `idealBitrate`'i aşıyorsa atlanıyor — kestirim
   formülü kodda açıkça "totally arbitrary equation" diye nitelenmiş: `alan^0.85 × √(fps^1.1)`.

**Ölçmediği iki şey:** GPU yükü ölçülmüyor (stres testi oyun koşarken GPU yükünü sınamıyor)
ve ekranın yenileme hızının ölçüldüğüne dair bir kanıt yok — FPS kullanıcının seçtiği kipten
geliyor.

### Karar verdiği

- `Base (Canvas) Resolution` ve çıktı (`Scaled`) çözünürlüğü
- FPS
- Yayın kodlayıcısı ve kayıt kodlayıcısı. Donanım varsa yeğleme sırası
  **NVENC → QSV → Apple → AMD**, yoksa x264. Linux'ta QSV'den x264'e düşülüyor (CBR
  garantisi sebebiyle).
- Video bit hızı ve (yayın kolunda) sunucu. Bit hızı servisin üst sınırına kırpılıyor,
  çözünürlük servisin desteklediği listeye en yakın değere oturtuluyor.
- Simple kayıt kalitesi: `High Quality, Medium File Size` ya da `Same as stream`.

### İki kol arasındaki fark

- **Yayın kolu:** Starting → BandwidthTest → StreamEncoder → RecordingEncoder → Finished.
  Bant genişliği testi yalnız burada koşuyor (kutu işaretsizse atlanıyor). Çözünürlük/FPS
  kararı bit hızı süzgeci yüzünden ağa da bağlı.
- **Salt kayıt kolu** (`Optimize just for recording`): bant genişliği aşaması tümüyle
  atlanıyor, doğrudan kodlayıcı testine geçiliyor. Bit hızı süzgeci uygulanmıyor —
  çözünürlük/FPS **yalnız makinenin dayanıklılığına** göre seçiliyor. Kayıt her zaman kendi
  ayrı kodlayıcısını ve `High Quality, Medium File Size` kalitesini alıyor.
- **Sanal kamera kolu:** hiç test koşmuyor; çıktı çözünürlüğü canvas ile aynı, FPS 30/1.

### Bizim otomatik kipimiz için buradan çıkan kararlar

- Sihirbaz **tahmin etmiyor, koşturuyor.** Kararın dayanağı ~5 saniyelik gerçek kodlama
  denemeleri ve atlanan kare sayımı. Bu depoda zaten "rapora giren her sayı
  `tools/VidShrink.Bench`ten çıkar" kuralı var; aynı hat.
- Ölçüt tek: **atlanan kare.** Kayıtta kalite değil *yetişebilme* ölçülüyor. Bizde karşılığı
  `RecordProgress.DroppedFrames`, yani zaten okunuyor.
- Aday kümesi kapalı ve küçük: 7 çözünürlük × 2 FPS, en çok 3 kazanan. Süresiz arama yok.
- Donanım yeğleme sırası ölçümle değil **sabit listeyle** belirleniyor; ölçüm yalnız "bu
  düzen yetişiyor mu" sorusunu yanıtlıyor.
- CPU sınıfı testten önce bir **tavan** koyuyor; imkânsız bileşimler hiç denenmiyor.
- Bizi ilgilendiren kol salt kayıt kolu: bant genişliği testinin VidShrink'te karşılığı yok.

## Bizde eksik olanlar, uygulama maliyetine göre üç kümede

### (a) ffmpeg CLI ile bugün yapılabilir

Yeni bağımlılık gerektirmeyen, `RecorderArguments`'a birkaç argüman ekleyip ölçmekle biten
işler.

1. **Kayıt kabı seçimi (mkv / fragmented mp4).** Bugün çıktı hep `.mp4`. OBS'in gerekçesi
   birebir bizim `Partial` sorunumuz: MKV ve fragmented biçimler sonlandırma gerektirmiyor,
   kesilen kayıt oynatılabilir kalıyor. Uzantı zaten `Build`te okunuyor.
2. **Çıktı çözünürlüğü ölçekleme.** `-vf scale=W:H` (macOS'ta var olan `crop` ile
   zincirlenerek); ölçekleme süzgecini seçtirmek `scale=...:flags=lanczos` ile.
3. **Keyframe aralığı, profil, B-frame sayısı.** `-g`, `-profile:v`, `-bf`. OBS'in kayıt
   için önerdiği keyframe 2 saniye, profil High.
4. **Hedef bit hızı / CBR kolu.** `CodecModel.BitrateRateControlArgs` zaten var ama kayıt
   kolunda kullanılmıyor; bugün yalnız kalite tabanlı tek kol var.
5. **Ayrı ses izleri.** `amix` yerine iki ayrı `-map` ile iki AAC izi (mp4 ve mkv ikisi de
   taşır). `AudioCapturePlan` zaten `Maps` listesi tutuyor; mimari değişmiyor.
6. **Ses filtreleri.** Gain (`volume=`), gürültü kapısı (`agate`), gürültü bastırma
   (`afftdn` / `arnndn`), sıkıştırıcı (`acompressor`), limitleyici (`alimiter`),
   stereo→mono (`pan`). Hepsi ffmpeg'in kendi filtreleri, `-filter_complex` zaten kuruluyor.
7. **Ses kodeği, bit hızı, örnekleme ve kanal düzeni seçimi.** Bugün `aac` 160 kbit/s sabit.
8. **Süre/boyut ölçütüyle otomatik dosya bölme.** `-f segment -segment_time` ya da var olan
   parça düzeneğinin zamanlayıcıyla tetiklenmesi; `RecorderSession` zaten çok parçalı.
9. **Kayıt süresi sınırı / otomatik bitirme.** `-t`, ya da oturumu saatten durdurmak.
10. **Ekran görüntüsü.** Depoda `FrameGrabber` zaten var; kaydediciye düğme olarak bağlanması.
11. **Windows'ta çoklu monitör seçimi.** Bugün `ScreenIndex != 0` reddediliyor; monitör
    sınırları okunup bölge ofsetine çevrilirse çözülür (yalnız ekran geometrisi okuma gerekir).
12. **Renk biçimi / uzayı / aralığı seçimi.** `-pix_fmt`, `-colorspace`, `-color_range`
    argüman meselesi; depoda `HdrArgumentsTests` benzeri bir kol zaten var.
13. **Otomatik kip (OBS sihirbazının kayıt kolu).** Kapalı aday kümesini kısa kayıtlarla
    koşturup `drop_frames` okumak. `RecorderSession` düşen kareyi zaten sayıyor,
    `EncoderAvailability` / `HardwareEncoderTests` donanım envanterini zaten biliyor. Kalem
    **ölçüm**, yeni teknoloji değil.

### (b) Ciddi iş ister

Yeni bir yüzey, yeni bir yerel API ya da yeni bir mimari kat gerektirenler.

1. **Webcam bindirmesi (picture-in-picture).** İki girdi + `overlay` filtresi ffmpeg'de
   mümkün; iş, kamerayı seçtiren/önizleten arayüz ve gerçek zamanlıda iki girdinin senkronu.
   `CaptureDevices` video cihazlarını zaten listeliyor.
2. **Kısayol tuşları (global hotkey).** Avalonia'nın pencere dışına çıkan kancası yok;
   Windows'ta `RegisterHotKey`, macOS'ta `NSEvent` global monitor, Linux'ta X/Wayland ayrı —
   üç platformda üç ayrı yerel çağrı. İkisinde de kaydedicinin en çok kullanılan özelliği
   bu; öncelik hak ediyor.
3. **Tekrar arabelleği (Replay Buffer).** Sürekli dönen bellek halkası; ffmpeg tek başına
   vermiyor. Segment döngüsü + son N segmenti birleştirme olarak taklit edilebilir ama disk
   aşınması ve kesin süre garantisi ayrı bir tasarım işi.
4. **Fare tıklama efekti / imleç vurgulama.** İmleç konumunu kayıt boyunca örnekleyip
   çizmek gerekir; yakalama demuxer'ları imleci çiziyor ama tıklama olayını bildirmiyor.
   Yerel kanca + karelere çizim katmanı. **Bu, OBS'te de olmayan ama Bandicam'in öne
   çıkardığı özellik** — eğitim/anlatım videosu çeken kullanıcı için ayırt edici.
5. **Gerçek zamanlı çizim / vurgulama.** Şeffaf, tıklamayı geçiren tam ekran pencere ve
   onun kayda karışması. Avalonia'da yapılabilir ama ayrı bir yüzey ve ayrı bir kompozisyon
   yolu demek. Bu da OBS'te yok, Bandicam'de var.
6. **Bölgeyi fareyle seçtirme.** Bugün dört sayı elle giriliyor. Şeffaf kaplama penceresi +
   DPI ölçeği + çoklu monitör koordinatları; bu depoda başsız ölçümün DPI/dil tuzağına
   düştüğü not zaten var.
7. **Zamanlanmış kayıt.** Programın kendi zamanlayıcısı kolay; Windows açılışında başlatma
   ve kullanıcı oturumuna bağlanma kurulum tarafını da açar. Bandicam'in komut satırı +
   Görev Zamanlayıcı çözümü ucuz bir ara basamak olabilir.
8. **Chroma key.** `chromakey`/`colorkey` filtresi var, ama anlamlı olması için webcam
   bindirmesi (b.1) önce gelmeli.
9. **Apple VideoToolbox kolu.** Engel teknik değil, **ölçüm**: `CodecModel.QualityArgs`
   VideoToolbox'ta ölçek ölçülmediği için bilerek patlıyor. Bir Apple makinesinde ölçek
   ölçülürse kol açılır.
10. **macOS'ta pencere yakalama.** `avfoundation` pencere vermiyor; macOS 13+'in
    ScreenCaptureKit'i veriyor ama bu ffmpeg dışında bir yol demek. OBS de bunu ayrı bir
    kaynak türü olarak çözdü.

### (c) Avalonia/ffmpeg ile makul değil

Farklı bir mimariye — bir kompozisyon motoruna ya da çekirdek düzeyinde kancalara — geçmeyi
gerektirenler.

1. **Oyun yakalama (DirectX/OpenGL/Vulkan kancası).** OBS'in Game Capture'ı ve Bandicam'in
   Game Recording kipi oyunun grafik API'sine kanca takıyor; OBS'inki ayrıca anti-cheat
   uyumluluk kancası taşıyor. ffmpeg'in `gdigrab`'ı böyle bir şey yapmıyor. Bunu yapmak
   ayrı bir yerel kanca kütüphanesi yazmak demek.
2. **Sahne/kaynak sistemi.** Katmanlı kaynaklar, sahne geçişleri, Studio Mode, kaynak başına
   filtre zinciri — bu bir kompozisyon motoru, bir ekran kaydedici değil. VidShrink'in
   kaydedicisi tek girdi + ses kolu üzerine kurulu.
3. **Tarayıcı kaynağı.** Gömülü bir CEF örneğinin karelerini kompozisyona vermek; hem büyük
   bir bağımlılık hem de (2)'deki motoru varsayıyor.
4. **AI tarafı (transkripsiyon, webcam arka planı kaldırma, Smart Zoom).** Bandicam'in
   reklam ettiği bu kalemler model çalıştırmayı gerektiriyor; ffmpeg/Avalonia kapsamı dışında.
5. **Ağ bant genişliği testi.** Anlamı yok: VidShrink yayın yapmıyor. Sihirbazın bizi
   ilgilendiren kısmı kodlayıcı testi, bant genişliği kolu değil.

## Not

Tabloda "belgede doğrulanamadı" yazan hücreler, özelliğin **olmadığını** değil, iki programın
okunan resmî sayfalarında doğrulanamadığını söylüyor. Kesin "yok" denen satırlar: 31 ve 32'nin
OBS hücreleri (filtre listesi ve arayüz dil dosyası tarandı), 13'ün Bandicam hücresi (Windows
ürünü) ve 45'in Bandicam hücresi (genel bakış ve boyut küçültme kılavuzları elle ayara
yönlendiriyor).

Bir de iki adlandırma tuzağı not edildi: OBS'te renk aralığının ikinci değeri "Partial" değil
**"Limited"**, ses izleme kiplerinin üçüncüsü "Monitor and Output" değil
**"Monitoring Enabled"**. Yaygın bilinen "MKV'de duraklatılır, MP4'te duraklatılmaz" kuralının
resmî metinde karşılığı yok: duraklatma kısıtı kaba değil **kodlayıcıya** bağlı.
