# Platform Eşitliği

13 Ekim 2026. Kod, çalışma ağacında okundu (`eb0a5db0`; `origin/main` 9de48d8b'nin bir sürüm commit'i üstü).

Kapsam: yalnız bir platformda olup ötekinde olmayan ya da yarım kalan şey. Hiçbir platformda
olmayan özellik bu kolda yazılmadı; öteki kolların işi.

Yol kısaltmaları: `Core/` = `src/VidShrink.Core/`, `App/` = `src/VidShrink.App/`,
`Ffmpeg/` = `src/VidShrink.Ffmpeg/`, `Player/` = `src/VidShrink.Player/`. Kurucu betikleri depo kökünde.

## Envanter

Terimler, ilk geçtikleri yerde:

- **gdigrab, ddagrab, avfoundation, x11grab:** ffmpeg'in ekran yakalama girişleri. İlk ikisi Windows, üçüncüsü macOS, dördüncüsü Linux'un eski pencere sistemi X11 içindir.
- **Wayland:** Linux'un yeni pencere sistemi. Uygulamaların ekranı izinsiz okumasına kapalıdır.
- **PipeWire ve portal:** Wayland'da ekranı okumanın resmi yolu. Portal izin penceresini açar, PipeWire görüntüyü taşır.
- **ScreenCaptureKit:** Apple'ın ekran, pencere ve ses yakalama çatısı.
- **NVENC, QSV, AMF:** NVIDIA, Intel ve AMD ekran kartlarının donanım kodlayıcıları. **VideoToolbox** Mac'in, **VAAPI** Linux'un, **Media Foundation** Windows'un kendi kodlayıcı arabirimidir.
- **pulse `.monitor`:** Linux'ta hoparlöre giden sesin kopyasını veren ses kaynağı.

| Özellik | Sekme | Windows | macOS | Linux | Kanıt (dosya:satır) |
|---|---|---|---|---|---|
| Ekran yakalama | Kaydedici | var | yarım: ekran sıra numarasıyla verilir, kameralı Mac'te kamerayı açabilir; ölçülmedi | yarım: yalnız X11; Wayland'da kapı yok, ölçülmedi | Core/RecorderArguments.cs:1422-1486; App/Recorder/RecorderView.Secici.cs:127-138; App/Recorder/RecorderView.Hedef.cs:177 |
| Bölge yakalama | Kaydedici | var | yarım: ekran yakalanır, kırpma filtresiyle kesilir | yarım: yalnız X11 | Core/RecorderArguments.cs:1138, 1436-1448, 1497-1508 |
| Pencere yakalama | Kaydedici | var | yarım: pencere dikdörtgeni kayıt başında ekran kırpmasına çevrilir | yarım: X11'de pencere kimliğiyle; Wayland'da hata satırı | Core/RecorderArguments.cs:1127-1129, 1450-1456; App/Recorder/RecorderView.Secici.cs:45-48, 79; App/Recorder/RecorderWindows.cs:81-110; App/Recorder/RecorderWindowsMac.cs:19-40 |
| Bölge düzenleyicisi | Kaydedici | var | yok | yok | App/Recorder/RecorderView.Duzenleyici.cs:23-27; App/Recorder/RecorderView.Secici.cs:169, 178 |
| Ekran yenileme hızını okuma | Kaydedici | var | yok | yok | App/Recorder/ScreenRefresh.cs:22 |
| Sistem sesi | Kaydedici | var | yarım: yalnız adında "loopback" geçen sanal ses aygıtı kuruluysa | yarım: pulse `.monitor` kaynağı listede çıkar; ölçülmedi | Ffmpeg/CaptureDevices.cs:58-59, 115-119, 132-133, 239-264; Ffmpeg/SystemAudioCapture.cs:36 |
| Mikrofon | Kaydedici | var | var | var | Core/AudioCaptureArguments.cs:333-334; Ffmpeg/CaptureDevices.cs:115-119 |
| Kamera katmanı | Kaydedici | var | yok | yok | App/Recorder/RecorderView.Kamera.cs:53; Core/RecorderArguments.cs:894-895; Ffmpeg/CaptureDevices.cs:252 |
| Tıklama gösterimi | Kaydedici | var | yok | yok | App/Recorder/RecorderView.Girdi.cs:14-20; App/Recorder/RecorderView.axaml:616 |
| Tıklama sesi | Kaydedici | var | yok | yok | App/Recorder/RecorderView.Girdi.cs:14-26; App/Recorder/RecorderView.axaml:620 |
| Tuş gösterimi | Kaydedici | var | yok | yok | App/Recorder/RecorderView.Girdi.cs:20; App/Recorder/RecorderView.axaml:624 |
| Büyüteç | Kaydedici | var | yok | yok | App/Recorder/RecorderView.Girdi.cs:40; App/Recorder/RecorderMagnifier.cs:51, 93; App/Recorder/RecorderView.axaml:628 |
| Genel kısayol | Kaydedici | var | yok | yok | App/Recorder/RecorderView.Kisayol.cs:31-34 |
| Denetim yüzeylerini kayıttan çıkarma | Kaydedici | var | yok | yok | App/Recorder/CaptureAffinity.cs:20-23; App/Recorder/RecorderMini.axaml.cs:71-72; App/Recorder/RecorderView.Mini.cs:96, 122 |
| Kaydedicide donanım kodlayıcı | Kaydedici | var | yok | yarım: NVENC, QSV ve AMF listede, VAAPI yok | App/Recorder/RecorderView.Hedef.cs:23-29; Core/RecorderArguments.cs:368-374 |
| Anlık tekrar, canlı önizleme, tepsi simgesi | Kaydedici | var | var | var | Ffmpeg/ReplayRecorder.cs:45; App/Recorder/RecorderView.Onizleme.cs:25; App/Recorder/RecorderTray.cs:70-110 |
| Dosyayı klasörde seçili gösterme | Oynatıcı, Küçült | var | yarım: oynatıcıda seçili, kuyruk sonunda yalnız klasör | yarım: yalnız klasör açılır | App/Platform.cs:9-26; App/QueueEndActions.cs:33-39 |
| Altyazı hesabı oturumunu saklama | Oynatıcı | var | yok | yok | App/Subtitles/SessionStore.cs:19-21, 32 |
| Videodan pencereyi sürükleme | Oynatıcı | var | yarım: yerel sürükleme kapalı, yedek yol doğrulanamadı | yarım: yerel sürükleme kapalı, yedek yol doğrulanamadı | App/Playback/PlayerView.Fare.cs:35 |
| libmpv ile oynatma ve donanım çözme | Oynatıcı, Düzenleyici | var | var | var | Player/LibMpvLocator.cs:19-58; Player/MpvEngine.cs:146-154, 426-428 |
| Yazı katmanına sistem yazı tipi klasörü | Düzenleyici | var | yarım: klasör verilmez, sonucu doğrulanamadı | yarım: klasör verilmez, sonucu doğrulanamadı | Core/Editing/TextFonts.cs:43-47 |
| Hızlı kipte donanım kodlayıcının kendiliğinden seçilmesi | Küçült, Dönüştür | var | yarım: VideoToolbox yalnız kodek kilidiyle | yarım: NVENC, QSV, AMF kendiliğinden; VAAPI yalnız kodek kilidiyle ve hiç koşmadı | Core/CodecModel.cs:169-175, 197-214; Core/PlanCalculator.cs:1668-1670; Ffmpeg/EncoderCapabilities.cs:256 |
| İş bitti bildirimi | Küçült, Dönüştür | var | yok | yok | App/IsBittiBildirimi.cs:63-67, 95-104 |
| Kuyruk sonunda uyut ya da kapat | Küçült, Dönüştür | var | var | var | App/QueueEndActions.cs:41-53 |
| Yetim ffmpeg sürecini toplama | Tüm sekmeler | var | yok | yok | Ffmpeg/ChildJob.cs:12, 28 |
| Hareketi azalt ayarını okuma | Tüm sekmeler | var | yok | yok | App/MainWindow.axaml.cs:377-384; App/Playback/HoverZone.cs:260-266 |
| Sağ tık menüsü | Kabuk | var | yarım: "Birlikte Aç" kaydı var, kendi eylemi yok | yarım: "birlikte aç" kaydı var, kendi eylemi yok | App/ShellMenu.cs:36; App/MainWindow.KabukMenusu.cs:27-28; macos-app-bundle.sh:82-110; install-vidshrink.sh:77-112 |
| Varsayılan oynatıcı yapma | Kabuk | var | yarım: tür kaydı var, öneri ve "varsayılan yap" yok | yarım: tür kaydı var, öneri ve "varsayılan yap" yok | App/MainWindow.axaml.cs:362-372; App/Integration/DefaultAppSuggestionBar.cs:97-101; App/Integration/DefaultApp.cs:16 |
| Elle güncelleme ("Yükle") | Kabuk | var | yarım: bütün paket takası, yalnız uygun kurulumda | yok | Core/UpdateCheck.cs:146-147, 1509-1516, 1618-1626; docs/kurulum.md:170-176 |
| Otomatik sessiz güncelleme | Kabuk | var | yok | yok | Core/UpdateCheck.cs:433; App/MainWindow.KapanistaGuncelleme.cs:85, 116-120 |
| Grafik kurucu | Dağıtım | var | yok | yok | .github/workflows/release.yml:387-395; docs/kurulum.md:70-81 |
| Kurucunun ffmpeg'i getirmesi | Dağıtım | var | yok | yok | Core/Setup/SetupModel.cs:78-103; Install-VidShrink.ps1:135-148; install-vidshrink.sh:232-262 |
| Kurucunun libmpv'yi getirmesi | Dağıtım | var | var | yok | Core/Setup/SetupModel.cs:29-45; install-vidshrink.sh:264-307 |
| Yayın hedefi (x64 ve arm64) | Dağıtım | var | var | var | .github/workflows/release.yml:347; install-vidshrink.sh:187-220 |

### Tablonun Söylemediği

**Kullanıcı üç ayrı biçimde karşılaşıyor.** Panel gizli (kamera, sağ tık menüsü). Hata satırı
(Wayland'da pencere hedefi). Sessizce hiçbir şey: tıklama, tıklama sesi, tuş ve büyüteç kutuları
macOS ve Linux'ta görünür ama işlemez (`App/Recorder/RecorderView.axaml:616-628`'de görünürlük
özniteliği yok, kodda görünürlük ataması araması boş). Genel kısayol, iş bitti bildirimi ve
otomatik güncelleme de aynı sessiz sınıfta.

**macOS ekran indeksi.** Bizim kod ekranı açılır kutudaki sırasıyla verir
(`App/Recorder/RecorderView.Secici.cs:138`, `Core/RecorderArguments.cs:1461-1467`, girdi
`-i "<sıra>:none"`). ffmpeg kaynağı ekranları kameralardan sonra numaralar: `avfoundation.m`
satır 1124 ekran numarasını "kamera sayısı + i" diye basar, 1163-1167 indeks kamera sayısından
küçükse kamerayı açar, 1219-1224 adla seçimi ("Capture screen N") kabul eder [45]. Sonuç:
kamerası olan Mac'te ilk ekran yerine kamera açılır. Bu kod okumasıdır, gerçek Mac'te ölçülmedi.

**macOS izin akışı.** Depo genelinde `NSMicrophoneUsageDescription`, `NSCameraUsageDescription`,
`NSScreenCapture`, `CGRequestScreenCaptureAccess`, `CGPreflightScreenCaptureAccess` ve
`AVCaptureDevice` araması boş. Paket ad-hoc imzalı (`macos-app-bundle.sh:117-118`), yani Apple
kimliği taşımayan yerel imza; en düşük sistem 14.0 (`:78-79`).

**Ölçüm durumu.** `docs/olcumler/kaydedici-coklu-ekran.md:139` x11grab ve avfoundation kollarının
ölçüm dışında kaldığını, `docs/olcumler/kaydedici-ses-seviyesi.md:53` macOS avfoundation ve Linux
pulse sesinin ölçülmediğini söylüyor. VideoToolbox, CI'daki M1 sanal makinede ölçüldü ve otomatik
sıra kapısından geçemedi (`docs/olcumler/videotoolbox-hizli.md`, `Core/CodecModel.cs:209-214`).
Defter `.claude/acik.md:111` VAAPI ve VideoToolbox'ın gerçek donanımda koşmadığını yazıyor.

**İş bitti bildiriminin Windows kolu** defterde ayrıca açık: Win32 çağrıları hiç koşmamış
(`.claude/acik.md:25, 50`). Bu, macOS ve Linux eksiğinden ayrı bir satır.

**ARM.** win-arm64 kurulum yolu var: ffmpeg BtbN `autobuild-2026-08-31-13-27` pinli, libmpv ve
`vulkan-1.dll` arm64 pinli (`Core/Setup/SetupModel.cs:29-45, 66-67, 78-103`;
`Install-VidShrink.ps1:135-148, 218-238`). Kaydedicinin kodlayıcı listesinde Media Foundation yok.
Windows 11'in ilk sağ tık menüsündeki eklenti yalnız x64 derleniyor (`.github/workflows/release.yml:275, 282`);
ARM64'te yalnız eski menü çalışıyor (`:406`).
linux-arm64 kurucu dalı var (`install-vidshrink.sh:213-216`), ama libmpv yedek dizinlerinde
`aarch64-linux-gnu` yok (`:292`) ve kodda `v4l2m2m` (ARM kartlarının kodlayıcı arabirimi) araması boş.

**Bayat belge.** `docs/kurulum.md:50-51` "Only `win-x64` is published" diyor, `:73-76` ve `:178-180`
linux-arm64'ü anmıyor. Kod altı hedef yayımlıyor (`.github/workflows/release.yml:347`).

**Önceki taramanın "Karar Gerektirmeyenler" listesi** (`docs/piyasa/tarama-2026-10-05.md:227-250`).
Bu kola düşen iki kalem yapılmış ama yalnız Windows'ta: 11 numara "küçültme bitince bildirim"
(`App/IsBittiBildirimi.cs:63-67`) ve 19 numara "mini çubuğu kayıttan gizleme"
(`App/Recorder/CaptureAffinity.cs:20-23`). İkisi de aşağıda eksik satırı olarak duruyor
(PLT-35, PLT-36, PLT-18).

**Kapsam dışı bırakılanlar.** Ortam tuşu tümleşimi hiçbir platformda yok (`App/Playback/Keymap.cs:178`;
öteki kolda OYB-30, OYB-31). Açılışta çalışma ve görev çubuğu ilerlemesi de hiçbir platformda yok.
Kod imzası platform farkı değil: `release.yml`'de `signtool`, `authenticode`, `codesign`, `notar`
araması boş; öteki kolda DAG-05. ddagrab bir özellik değil yakalama yoludur; karşılıkları PLT-02,
PLT-03 ve PLT-05.

## Rakipler

Sürüm ve tarih `gh api repos/<sahip>/<depo>/releases/latest` ile okundu [44]; öyle olmayanlar belirtildi.

| Ad | Sürüm | Tarih | Tür | URL |
|---|---|---|---|---|
| OBS Studio | 32.2.2 | 2026-08-14 | açık kaynak; Windows, macOS, Linux; kayıt ve yayın | https://obsproject.com/ |
| HandBrake | 1.11.2 | 2026-06-07 | açık kaynak; üç platform; dönüştürücü | https://handbrake.fr/ |
| Shutter Encoder | 20.4 | 2026-09-28 | ücretsiz; üç platform; dönüştürücü | https://www.shutterencoder.com/ |
| LosslessCut | 3.69.0 | 2026-06-04 | açık kaynak; üç platform; kayıpsız kesme | https://github.com/mifi/lossless-cut |
| Shotcut | 26.9.27 | 2026-09-28 | açık kaynak; üç platform; düzenleyici | https://www.shotcut.org/ |
| Kdenlive | 26.08.1 | tarih sayfada yok | açık kaynak; üç platform; düzenleyici | https://kdenlive.org/download/ |
| mpv | 0.41.0 | 2025-12-21 | açık kaynak; üç platform; oynatıcı | https://github.com/mpv-player/mpv |
| VLC | bakılmadı | bakılmadı | açık kaynak; üç platform; oynatıcı (yalnız depo araması) | https://github.com/videolan/vlc |
| IINA | 1.5.0 | 2026-10-03 | açık kaynak; yalnız macOS; oynatıcı | https://iina.io/ |
| Celluloid | 0.30 | 2026-03-29 | açık kaynak; Linux; oynatıcı | https://github.com/celluloid-player/celluloid |
| Haruna | bakılmadı | bakılmadı | açık kaynak; Linux; oynatıcı (yalnız depo araması) | https://github.com/KDE/haruna |
| Kooha | 2.3.2 | 2026-06-05 | açık kaynak; Linux; ekran kaydedici | https://github.com/SeaDve/Kooha |
| GPU Screen Recorder | sürüm numarası yok | sayfada 2026-08-29 tarihli düzeltme | açık kaynak; Linux; ekran kaydedici | https://git.dec05eba.com/gpu-screen-recorder/about/ |
| SimpleScreenRecorder | doğrulanmadı (yayın sorgusu 404) | doğrulanmadı | açık kaynak; Linux, X11; ekran kaydedici | https://github.com/MaartenBaert/ssr |
| Kap | 3.6.0 | 2022-10-27 (eski) | açık kaynak; macOS; ekran kaydedici | https://github.com/wulkano/Kap |
| CleanShot X | sayfada görülmedi | sayfada görülmedi | ücretli; macOS; ekran kaydedici | https://cleanshot.com/features |
| Screen Studio | 4.0 | tarih sitede yok | ücretli; macOS; ekran kaydedici | https://screen.studio/ |

Rakip değil, yol: Sparkle 2.10.0 (2026-09-13; macOS güncelleme çatısı) [30], ScreenCast, GlobalShortcuts
ve Notification portalları [2][3][4], MPRIS 2.2 (Linux'ta ortam denetimi arabirimi) [5], ScreenCaptureKit [6].

## Özellik Matrisi

Soru her satırda aynı: bizde Windows'ta olan iş, rakipte macOS ve Linux'ta hangi yoldan sunuluyor.

| Özellik | Bizde (Windows / macOS / Linux) | macOS'ta Rakip Yolu | Linux'ta Rakip Yolu | Kaynak |
|---|---|---|---|---|
| Ekran yakalama | var / yarım / yarım | ScreenCaptureKit: OBS Studio (macOS 12.5 ve üstü), CleanShot X, Screen Studio | PipeWire ve portal: OBS Studio (27.0'dan beri), Kooha, GPU Screen Recorder; X11: SimpleScreenRecorder | [1][2][6][9][10][14][15][41] |
| Pencere yakalama | var / yarım / yarım | ScreenCaptureKit pencere süzgeci: OBS Studio, CleanShot X, Screen Studio | portalın pencere kaynağı: OBS Studio; Kooha'da deneysel; GPU Screen Recorder yalnız X11'de | [2][6][14][15][16][17] |
| Sistem sesi | var / yarım / yarım | OBS Studio 30 ve üstü "macOS Audio Capture" (macOS 13 ve üstü); CleanShot X; Screen Studio | Kooha masaüstü sesi | [8][9][14][16][17] |
| Kamera katmanı | var / yok / yok | CleanShot X, Screen Studio, OBS Studio | OBS Studio (V4L2, Linux'un kamera arabirimi), GPU Screen Recorder | [9][10][15][16][17] |
| Tıklama ve tuş gösterimi | var / yok / yok | CleanShot X, Screen Studio; ffmpeg'de `capture_mouse_clicks` hazır | doğrulanmadı | [1][16][17] |
| Genel kısayol | var / yok / yok | doğrulanmadı | GPU Screen Recorder masaüstü kısayoluna betik bağlatır; OBS Studio'da Wayland kolu açık PR; Kooha'da açık istek | [3][12][13][15][40] |
| Kaydedicide donanım kodlama | var / yok / yarım | OBS Studio: VideoToolbox | OBS Studio: NVENC, AMF, QSV; GPU Screen Recorder: NVENC, VAAPI, Vulkan; Kooha: deneysel | [11][14][15] |
| Dönüştürmede donanım kodlama | var / yarım / yarım | HandBrake: VideoToolbox (H.264, H.265, H.265 10-bit); Shutter Encoder: Apple donanımı | HandBrake: QSV, Flatpak eklentisiyle, VAAPI yok; Shutter Encoder: NVIDIA, AMD, Intel | [19][20][26] |
| İş bitti bildirimi | var / yok / yok | HandBrake (platform ayrıntısı doğrulanmadı) | HandBrake (platform ayrıntısı doğrulanmadı) | [21] |
| Güncelleme | otomatik / elle / yalnız haber | Sparkle: OBS Studio, IINA, HandBrake; Mac App Store: LosslessCut | mağaza kanalı: HandBrake (Flatpak), Shotcut (Flatpak, Snap), Kdenlive (Flatpak), LosslessCut (Snap) | [18][24][27][28][30][31][32][33] |
| Kurulum | grafik kurucu / uçbirim betiği / uçbirim betiği | dmg: HandBrake, Shotcut, Kdenlive, LosslessCut | AppImage, Flatpak ya da Snap: Shotcut, Kdenlive, HandBrake, LosslessCut, Kooha, GPU Screen Recorder; deb ve AppImage: Shutter Encoder | [14][15][18][24][26][27][28] |
| ARM yapıları | win-arm64 ve linux-arm64 var | evrensel ya da çift dmg: HandBrake, Shotcut, Kdenlive, LosslessCut, Shutter Encoder | linux arm64: LosslessCut; Windows ARM64: HandBrake, Shotcut | [18][24][26][27][28] |
| Ortam tuşları (kapsam dışı, hiçbir platformda yok) | yok / yok / yok | mpv ve IINA: Now Playing | Celluloid, Haruna, VLC: MPRIS | [5][32][34][35][36][37] |

## Kullanıcı Talepleri

Oy, sayfadaki beğeni tepkisi sayısıdır; 13 Ekim 2026'da okundu.

| İstek | Oy | Durum | URL |
|---|---|---|---|
| HandBrake: VAAPI ile kodlama | 29 | kapalı | https://github.com/HandBrake/HandBrake/issues/3468 |
| HandBrake: aynı konuda eski istek | 10 | kapalı | https://github.com/HandBrake/HandBrake/issues/1083 |
| OBS Studio: Wayland'da genel kısayol (PR) | 11 | açık | https://github.com/obsproject/obs-studio/pull/13661 |
| OBS Studio: portal ile genel kısayol (PR) | 4 | açık, birleşmedi | https://github.com/obsproject/obs-studio/pull/13680 |
| LosslessCut: Windows ARM64 yapısı | 11 | kapalı | https://github.com/mifi/lossless-cut/issues/1475 |
| Kap: sistem sesini kaydetme | 3 | kapalı | https://github.com/wulkano/Kap/issues/1241 |
| Kooha: genel kısayol | 1 | açık | https://github.com/SeaDve/Kooha/issues/185 |
| IINA: Now Playing tercihi | 1 | açık | https://github.com/iina/iina/issues/3896 |
| LosslessCut: Linux arm64 AppImage | 0 | açık | https://github.com/mifi/lossless-cut/issues/2471 |

Oyla kanıtlı büyük bir talep bu kolda çıkmadı; en yükseği 29. Öncelik 1 verilen üç satır oya değil
"üç yaygın rakipte belgeli ve sekmenin temel işini engelliyor" ölçütüne dayanıyor.

## Son İki Yılın Yenilikleri

Bu kolda sürüm notları satır satır okunmadı; aşağıdakiler bugünkü resmi sayfaların gösterdiği durumdur.

- **Wayland'da genel kısayol hâlâ açık iş.** OBS Studio'da iki PR da birleşmedi [12][13]; Kooha'da istek açık [40]. Portalın kendisi hazır: GlobalShortcuts sürüm 2 [3]. Yani pazar da bu boşluğu yeni kapatıyor.
- **Yakalama portalı olgunlaştı.** ScreenCast sürüm 6; ekran, pencere ve sanal kaynak, imleç kipleri ve izni hatırlayan `restore_token` (her kayıtta yeniden sormamak için) taşıyor [2].
- **Windows ARM64 yapıları yaygınlaştı.** HandBrake 1.11.2 ve Shotcut 26.9.27 ARM64 kurucu veriyor [18][27]; LosslessCut'ta istek kapandı [25].
- **macOS'ta açık kaynak kaydedici durdu.** Kap'ın son sürümü 2022-10-27 [44]; alan ücretli araçlarda (CleanShot X, Screen Studio 4.0) [16][17].
- **Linux kaydedicileri canlı.** Kooha 2.3.2 (2026-06-05), GPU Screen Recorder (2026-08-29 tarihli düzeltme) [14][15][44].
- **Eski ama temel, iki yıldan önce:** OBS Studio 27.0 Wayland ve PipeWire'ı [10], 28.0 ScreenCaptureKit'i [9], 30 "macOS Audio Capture" kaynağını [8] getirdi. Kesin tarihleri bu turda okunmadı.

Önceki kararlarla ilişki: K2 (sistem sesi) yalnız Windows'ta kapandı; PLT-03 onun macOS karşılığıdır.
K3 (pencereyi WGC ile yakalama) rafta; PLT-05 aynı işin macOS yüzüdür ve ayrı karar ister. K12
(VAAPI ve VideoToolbox) kodlandı; PLT-37 ve PLT-38 ondan kalan ölçüm ve otomatik sıra işidir.

## Doğrulanamayanlar

- **macOS ekran indeksinin kayması.** Kod ve ffmpeg kaynağından çıkarım; gerçek Mac'te koşulmadı. PLT-01'in önceliği bu çıkarıma dayanıyor.
- **Wayland'da ekran ve bölge kaydının çıktısı.** Siyah kare mi, hata mı, bilinmiyor; kodda kapı yok.
- **macOS kaydedicinin gerçek makinedeki davranışı.** İzin istemi çıkıyor mu, ad-hoc imzayla izin güncellemeden sonra sıfırlanıyor mu, görülmedi.
- **Linux'ta sistem sesi.** Kod pulse `.monitor` kaynağını sistem sesi sayıyor, README yalnız Windows diyor, ölçüm yok (PLT-43).
- **Linux'ta kaydedicinin NVENC, QSV ve AMF kolu.** Listede sunuluyor; gerçek makinede ölçüldüğü görülmedi.
- **Tepsi simgesinin GNOME'da görünmesi.** Kodda platform kapısı yok; masaüstünde bakılmadı.
- **Windows dışında videodan pencere sürükleme.** `NativeMoveDrag` yalnız Windows; yedek yol kodda bulunamadı.
- **Windows dışında yazı tipi eşleşmesi.** Klasör verilmiyor; ffmpeg'in doğru yazı tipini bulup bulmadığı görülmedi.
- **macOS ve Linux'ta mini şeridin kayda girmesi.** Kayıttan çıkarma yalnız Windows'ta; etkisi ölçülmedi.
- **BtbN winarm64 ffmpeg'in kodlayıcı kümesi.** Hangi donanım kodlayıcıları taşıdığı okunmadı.
- **macOS'ta genel kısayol sunan rakip.** Resmi kaynakla görülmedi (PLT-09).
- **Rakiplerin macOS izin akışı.** Apple belgesi izni şart koşuyor; rakiplerin yönlendirme ekranı okunmadı (PLT-04).
- **HandBrake bildiriminin platform ayrıntısı.** Belge platform ayırmıyor; kod araması oran sınırına takıldı.
- **LosslessCut'ın noterlemesi.** Kod araması oran sınırına takıldı.
- **Rakip dmg'lerinin içinde ffmpeg taşıması.** Tek dosyayla kuruldukları görüldü; içerik ayrıca doğrulanmadı (PLT-24).
- **VLC, Haruna ve mpv.** Belgeleri açılmadı; yalnız depo aramasıyla kanıtlandılar. Sürümleri okunmadı.
- **SimpleScreenRecorder sürümü.** Yayın sorgusu 404 döndü.
- **CleanShot X sürümü; Kdenlive ve Screen Studio tarihleri.** Sayfada görülmedi.
- **Linux'ta tıklama, tuş ve büyüteç sunan rakip.** Görülmedi (PLT-12, PLT-13).

## Kaynaklar

- [1] https://ffmpeg.org/ffmpeg-devices.html — avfoundation seçenekleri (`capture_cursor`, `capture_mouse_clicks`), x11grab, kmsgrab yetki şartı, PipeWire girişinin yokluğu, video4linux2 ve pulse
- [2] https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.ScreenCast.html — ScreenCast portalı, sürüm 6
- [3] https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.GlobalShortcuts.html — GlobalShortcuts portalı, sürüm 2
- [4] https://flatpak.github.io/xdg-desktop-portal/docs/doc-org.freedesktop.portal.Notification.html — Notification portalı, sürüm 2
- [5] https://specifications.freedesktop.org/mpris/latest/ — MPRIS 2.2
- [6] https://developer.apple.com/videos/play/wwdc2022/10156/ — ScreenCaptureKit: ekran, pencere, uygulama, ses, Ekran Kaydı izni, pencere süzgeci
- [7] https://developer.apple.com/developer-id/ — Developer ID ve noterleme
- [8] https://obsproject.com/kb/macos-desktop-audio-capture-guide — OBS Studio'da macOS masaüstü sesi
- [9] https://github.com/obsproject/obs-studio/releases/tag/28.0.0 — ScreenCaptureKit, doğrudan ses, Apple Silicon yapısı, Linux'ta V4L2 kamera
- [10] https://github.com/obsproject/obs-studio/releases/tag/27.0.0 — Wayland ve PipeWire yakalama, macOS kamera kaynağı
- [11] https://obsproject.com/kb/hardware-encoding — platform başına donanım kodlayıcılar
- [12] https://github.com/obsproject/obs-studio/pull/13661 — Wayland'da genel kısayol, 11 beğeni
- [13] https://github.com/obsproject/obs-studio/pull/13680 — portal ile genel kısayol, 4 beğeni
- [14] https://github.com/SeaDve/Kooha — PipeWire ve portal, masaüstü sesi, deneysel pencere ve donanım kodlama, Flathub
- [15] https://git.dec05eba.com/gpu-screen-recorder/about/ — X11 ve Wayland, portal, NVENC ve VAAPI, kamera, kısayol önerisi, Flathub
- [16] https://cleanshot.com/features — pencere, alan, bilgisayar sesi, tıklama ve tuş gösterimi, kamera
- [17] https://screen.studio/ — sistem sesi, kamera, tuş gösterimi, pencere ve alan
- [18] https://handbrake.fr/downloads.php — macOS dmg, Windows x64 ve ARM64, Linux Flatpak
- [19] https://handbrake.fr/docs/en/latest/technical/video-videotoolbox.html — HandBrake'te VideoToolbox
- [20] https://handbrake.fr/docs/en/latest/technical/video-qsv.html — HandBrake'te QSV ve platformları
- [21] https://handbrake.fr/docs/en/latest/introduction/quick-start.html — kodlama bitince bildirim
- [22] https://github.com/HandBrake/HandBrake/issues/3468 — VAAPI isteği, 29 beğeni
- [23] https://github.com/HandBrake/HandBrake/issues/1083 — aynı konuda eski istek, 10 beğeni
- [24] https://github.com/mifi/lossless-cut — mağazalar, macOS dmg, Linux x64 ve arm64
- [25] https://github.com/mifi/lossless-cut/issues/1475 — Windows ARM64 isteği, 11 beğeni
- [26] https://www.shutterencoder.com/ — platform paketleri ve donanım hızlandırma
- [27] https://www.shotcut.org/download/ — Windows x64 ve ARM64, macOS dmg, Linux paketleri
- [28] https://kdenlive.org/download/ — macOS dmg, Linux AppImage ve Flatpak
- [29] https://iina.io/ — IINA 1.5.0 ve sistem gereksinimi
- [30] https://sparkle-project.org/ — Sparkle'ın yaptığı iş
- [31] https://github.com/obsproject/obs-studio — `plugins/mac-capture/mac-sck-common.m`, `plugins/linux-pipewire/`, `frontend/cmake/macos/Info.plist.in`
- [32] https://github.com/iina/iina — `iina/NowPlayingInfoManager.swift`, `iina/Info.plist`
- [33] https://github.com/HandBrake/HandBrake — `macosx/Info.plist.m4`
- [34] https://github.com/mpv-player/mpv — `osdep/mac/remote_command_center.swift`
- [35] https://github.com/celluloid-player/celluloid — `data/celluloid-mpris-gdbus.xml`
- [36] https://github.com/KDE/haruna — `src/mpris/rootadaptor.cpp`
- [37] https://github.com/videolan/vlc — `modules/control/dbus/dbus_root.h`
- [38] https://github.com/wulkano/Kap — Kap'ın kapsamı
- [39] https://github.com/wulkano/Kap/issues/1241 — sistem sesi isteği, 3 beğeni
- [40] https://github.com/SeaDve/Kooha/issues/185 — genel kısayol isteği, 1 beğeni
- [41] https://github.com/MaartenBaert/ssr — X11'e dayandığı
- [42] https://github.com/iina/iina/issues/3896 — Now Playing tercihi isteği, 1 beğeni
- [43] https://github.com/mifi/lossless-cut/issues/2471 — Linux arm64 AppImage isteği
- [44] https://api.github.com/repos/obsproject/obs-studio/releases/latest — sürüm ve tarih sorgusu; öteki depolar aynı kalıpla
- [45] https://github.com/FFmpeg/FFmpeg/blob/master/libavdevice/avfoundation.m — ekranların kameralardan sonra numaralanması ve adla seçim

## Yinelenenler

Öteki kolların tabloları 13 Ekim 2026, 18:30'da okundu. Bu kol satırı platform başına ayırıyor; öteki
kollar aynı işi tek satırda tutuyor.

- PLT-02 = EKA-02
- PLT-03 = EKA-03
- PLT-05 = EKA-04
- PLT-06 = EKA-07
- PLT-06 = EKB-18
- PLT-07 = EKA-07
- PLT-07 = EKB-18
- PLT-08 = EKA-13
- PLT-09 = EKA-13
- PLT-10 = EKB-14
- PLT-11 = EKB-14
- PLT-12 = EKB-14
- PLT-13 = EKB-14
- PLT-15 = EKA-12
- PLT-16 = EKA-12
- PLT-20 = DAG-25
- PLT-22 = DAG-06
- PLT-23 = DAG-04
- PLT-23 = DAG-09
- PLT-24 = DAG-20
- PLT-25 = DAG-20
- PLT-26 = DAG-41
- PLT-27 = DAG-42
- PLT-30 = AYR-15
- PLT-35 = AYR-19
- PLT-35 = SIK-23
- PLT-36 = AYR-19
- PLT-36 = SIK-23
- PLT-44 = DAG-21

Yalnız bu kolda olanlar: PLT-01, PLT-04, PLT-14, PLT-17, PLT-18, PLT-19, PLT-21, PLT-28, PLT-29,
PLT-31, PLT-32, PLT-33, PLT-34, PLT-37, PLT-38, PLT-39, PLT-40, PLT-41, PLT-42, PLT-43.
