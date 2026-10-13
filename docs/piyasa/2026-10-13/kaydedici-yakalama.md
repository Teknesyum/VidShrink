# Kaydedici Sekmesi — Yakalama: Pazar Notları

13 Ekim 2026. Kod okuması `origin/main` 9de48d8b. Bu tur yalnız araştırma: kod yazılmadı, hiçbir şey derlenmedi, kayıt başlatılmadı.

Eksiklerin tablosu yandaki `kaydedici-yakalama-eksikler.md` dosyasında. Köşeli sayılar sondaki Kaynaklar bölümüne bakar.

Yol kısaltması: `Core/` = `src/VidShrink.Core/`, `App/` = `src/VidShrink.App/`, `Ffmpeg/` = `src/VidShrink.Ffmpeg/`.

## Envanter

### Kaynak Ve Hedef

- Üç hedef türü var: ekran, pencere, bölge (`Core/RecorderArguments.cs:24-34`). Dördüncü tür yok: oyun, harici aygıt, yalnız kamera.
- Tek kayıt tek ekranı alır (`App/Recorder/RecorderSettings.cs:127`). İki ekrana yayılan bölge kabul edilir ama yavaş yola düşer (`Core/RecorderArguments.cs:1039-1057`).
- **Pencere seçilince pencerenin kendisi değil, ekrandaki yeri kaydedilir.** Hızlı yolda pencerenin iç alanı sabit bir bölge kırpması olarak yazılır (`Core/RecorderArguments.cs:1019-1032`, `:1112-1119`). Sonuç: üstüne gelen başka pencere kayda girer, pencere taşınınca kırpma onu izlemez.
- Pencerenin dikdörtgeni okunamazsa ya da pencere iki ekrana yayılmışsa eski yola düşülür ve pencere başlığıyla aranır: `-i title=<başlık>` (`Core/RecorderArguments.cs:1430-1459`, `:1452`).
- macOS'ta pencere hedefi ekran kırpmasıdır (`Core/RecorderArguments.cs:1134-1138`). Linux'ta pencere kimliğiyle yakalanır (`Core/RecorderArguments.cs:1481-1482`).
- Web kamerası yalnız ekran kaydının üstünde katman olarak var, o da yalnız Windows'ta (`Core/RecorderArguments.cs:298`, `:937`; `App/Recorder/RecorderView.Kamera.cs:53`).
- Uygulamanın kendi çerçevesi ve şeridi kayıttan dışlanır (`App/Recorder/CaptureAffinity.cs:8-23`); Windows 10 2004 ve sonrasında.
- İmleci kayda alma ya da almama seçilir (`App/Recorder/RecorderSettings.cs:64`; `Core/RecorderArguments.cs:1434`, `:1465`, `:1471`).
- Kayıt sırasında canlı önizleme (`App/Recorder/RecorderSettings.cs:78`; `Core/RecorderArguments.cs:1242`) ve çıktıyı yeniden ölçekleme (`Core/RecorderArguments.cs:77`, `:1505`) var.
- Süren kayıttan tek kare alınır (`Core/RecorderArguments.cs:1339`; `Ffmpeg/RecorderSession.cs:245`). Bu, K9'un kapattığı ekran görüntüsü aracı değildir.
- Bölge çizilirken kenarlar hedef dikdörtgenlere yapışır (`App/Recorder/RegionSnap.cs:15`).

### Yakalama Yolu

- Windows'ta iki yol var: `ddagrab` ve `gdigrab` (`Core/RecorderArguments.cs:96`). `ddagrab`, ffmpeg'in ekranı ekran kartından doğrudan okuyan kaynağıdır (DXGI Desktop Duplication); `gdigrab` işlemciyle kopyalayan eski kaynaktır.
- Hızlı yol kullanılamayınca nedeni tutulur: kullanılamıyor, çıkış yok, pencere dikdörtgeni yok, tek ekranın dışında (`Core/RecorderArguments.cs:117-128`).
- Ölçüm: 60 fps istekte `ddagrab` 59,8 fps, `gdigrab` 24,2 fps verdi (`docs/olcumler/kaydedici-ddagrab.md`, 30 Eylül 2026). Otomatik plan `gdigrab` kolunda 30 fps tavanı uygular (`Core/RecorderAutoPlan.cs:105`).
- İkinci ölçüm 1280x720 pencereyle yapıldı (`docs/olcumler/kaydedici-gdigrab-kare-tavani.md`, 26 Eylül 2026): `gdigrab` 120 fps istekte 81,3 fps, 60 fps istekte 41,2 fps verdi; `ddagrab` 60 fps istekte 59,3 fps verdi. `ddagrab` yolunda 120 ve 144 fps ölçümü yok.
- macOS: `avfoundation` (Apple'ın kamera ve ekran yakalama çatısı) ile ekran (`Core/RecorderArguments.cs:1461-1467`). ScreenCaptureKit (Apple'ın pencereyi ve uygulama sesini de veren yeni çatısı) kullanılmıyor.
- Linux: `x11grab` ile yalnız X11 (`Core/RecorderArguments.cs:1469-1486`). Wayland, Linux'un yeni pencere sistemidir ve uygulamaların ekranı izinsiz okumasına izin vermez.
- Wayland'da pencere seçici hata verir (`App/Recorder/RecorderView.Secici.cs:45-47`), pencere listesi boş döner (`App/Recorder/RecorderWindows.cs:83`). Ekran ve bölge hedefinde Wayland için ne ayrı yol ne uyarı var.

### Ses

- Mikrofon ve sistem sesi birlikte kaydedilir; en çok bir mikrofon ve bir sistem sesi (`Core/AudioCaptureArguments.cs:36-40`).
- Dört ses yolu var: DirectShow, AVFoundation, PulseAudio ve WASAPI loopback (`Core/AudioCaptureArguments.cs:11`). WASAPI loopback, Windows'ta hoparlöre giden sesi geri okuma yoludur; K2 ile yapıldı (`Ffmpeg/CaptureDevices.cs:132-133`).
- **Mikrofon ve sistem sesi ayrı izlere yazılabiliyor**; tek ize karıştırma da var (`Core/AudioCaptureArguments.cs:48-58`).
- Kazanç, gürültü kapısı ve gürültü bastırma var (`Core/AudioCaptureArguments.cs:75-84`; süzgeçler `volume` `:129`, `agate` `:132`, `afftdn` `:135`).
- Ses düzeyi göstergesi var (`App/Recorder/RecorderView.SesSeviyesi.cs:50`). Gerçek pencerede görülmesi defterde açık.
- Ses kodeki sabit: AAC, 160 kbit/sn (`Core/RecorderArguments.cs:349`, `:352`, `:1409`).
- macOS'ta bir aygıt yalnız adı ipucu listesine uyarsa sistem sesi sayılır (`Ffmpeg/CaptureDevices.cs:59`). Listede sekiz ipucu var (`stereo mix`, `loopback`, `.monitor` gibi); `blackhole` adı yok. Linux'ta PulseAudio'nun `.monitor` aygıtı kullanılır (`Ffmpeg/CaptureDevices.cs:111-126`).

### Kodlayıcı, Kare Hızı Ve Renk

- On bir kodlayıcı var: `libx264`, `libx265`, `libsvtav1`, `libvpx-vp9`, NVENC (H.264, HEVC, AV1), QSV (H.264, HEVC), AMF (H.264, HEVC) (`Core/RecorderArguments.cs:368-374`). NVENC, QSV ve AMF sırasıyla NVIDIA, Intel ve AMD kartlarının donanım kodlayıcısıdır.
- Listede olmayanlar: AV1 için QSV ve AMF, macOS'un VideoToolbox'ı, Linux'un VAAPI'si. K12 bunları Dönüştür tarafına ekledi, kaydediciye değil.
- Kare hızı varsayılanı 30, tavanı 240 (`Core/RecorderArguments.cs:317`, `:358`). Varsayılan kodlayıcı `libx264`, ön ayar `veryfast`, kalite 23 (`:320-326`).
- Hız denetimi ve piksel biçimi seçilebilir (`App/Recorder/RecorderSettings.cs:145`, `:157`).
- HDR (yüksek parlaklık aralığı) yakalama yok: hızlı yol her zaman 8 bit `bgra` ister (`Core/RecorderArguments.cs:1112-1119`). `bt2020` adları renk uzayı listesinde yalnız etiket olarak durur (`Core/RecorderArguments.cs:405`).
- Sabit ya da değişken kare hızı (CFR/VFR) seçeneği kaydedicide yok.

### Kap Ve Güvenilirlik

- Dört kap var: MP4, MKV, MOV, GIF (`Core/RecorderArguments.cs:46-58`).
- MP4 ve MOV istenince kayıt önce Matroska'ya (MKV; yarıda kesilse de okunabilen kap) yazılır, durunca yeniden kodlamadan MP4 ya da MOV'a aktarılır (`Core/RecorderArguments.cs:486`, `:491`, `:440`; `Ffmpeg/RecorderSession.cs:412`). Yakalama dosyası çıktının yanında `<ad>.yakalama.mkv` adını taşır.
- ffmpeg yarıda ölürse dosya "yarım" diye işaretlenir, oynayıp oynamadığı yoklanır ve gizlenmeden teslim edilir (`Ffmpeg/RecorderSession.cs:42`, `:64`, `:718`, `:752`; `App/Recorder/RecorderView.axaml.cs:197`, `:242`).
- Uygulamanın kendisi çökerse ya da elektrik kesilirse kalan `.yakalama.mkv` dosyasını bir sonraki açılışta arayan kod yok.
- **Kaydedici boş disk alanına bakmıyor.** Boş alan denetimi yalnız Küçült'te ve komut satırında çağrılıyor (`App/MainWindow.axaml.cs:4514`, `src/VidShrink.Cli/CliApp.cs:519`).

### Kayıt Denetimi

- Duraklat ve sürdür var; duraklatma parçayı kapatıp yenisini açar (`Ffmpeg/RecorderSession.cs:297`, `:310`).
- Altı genel kısayol var: başlat ya da duraklat, durdur, kare al, at, tekrarı kaydet, bölüm işareti (`App/Recorder/RecorderHotkeys.cs:14-22`). Varsayılanlar F7, F8, F9, F10, F11, F6 (`:35-43`).
- **Kısayol yeniden atama bugün var.** Atama, çakışma denetimi ve varsayılana dönüş `App/Recorder/RecorderView.Kisayol.cs:133-166` içinde. Yalnız Windows'ta çalışır; öbür sistemlerde genel kısayol kaydı boş döner (`App/Recorder/RecorderView.Kisayol.cs:29-36`; `App/Recorder/RecorderHotkeys.cs:136`).
- Geri sayım 0, 3, 5 ya da 10 saniye (`App/Recorder/RecorderSettings.cs:46`; `App/Recorder/RecorderView.GeriSayim.cs:60`).
- Kayıt göstergesi üç yerde: bölge çerçevesi (`App/Recorder/RecorderFrame.axaml.cs:17`), mini şerit (`App/Recorder/RecorderMini.axaml.cs:45`), bildirim alanı simgesi (`App/Recorder/RecorderTray.cs:28`).
- Kayıt sırasında bölüm işareti konur ve dosyaya yazılır (`Ffmpeg/RecorderSession.cs:269`, `:363`); kap bölüm taşımıyorsa bu bildirilir (`:371`).
- Süre ve boyut sınırıyla durma var (`Core/RecorderArguments.cs:1218`, `:1221`; `App/Recorder/RecorderSettings.cs:51`, `:54`).
- Süreye ya da boyuta göre otomatik bölme var (`Core/RecorderArguments.cs:139`, `:1273`).
- GIF kaydı var (`Core/RecorderArguments.cs:867`; `Ffmpeg/RecorderSession.cs:442`).

### Anlık Tekrar

- Anlık tekrar tamponu var: arka planda son 15, 30, 60 ya da 120 saniyeyi tutar, tuşla kaydeder (`Core/ReplayBuffer.cs:10-14`, `:48-51`; `Ffmpeg/ReplayRecorder.cs:44`, `:114`).
- Tampon yalnız kayıt yokken çalışır (`App/Recorder/RecorderView.Tampon.cs:71-85`, `:103-105`). Paneli yalnız Gelişmiş kipte ya da tampon çalışırken görünür (`:88-92`).
- Tamponu açılışta kendiliğinden başlatan bir ayar yok.

### Önceki Taramadan Bu Yana Kapananlar

`docs/piyasa/tarama-2026-10-05.md` şu dört kalemi "yok" saymıştı; bugün kodda varlar:

- Kısayol yeniden atama (o belgede satır 177, 234, 304). Yukarıda, Kayıt Denetimi'nde.
- Ses düzeyi göstergesi (satır 167, 247).
- Kayıtta bölüm işareti (satır 175, 248).
- Gerçek sistem sesi (satır 164; K2).

### Eskiyen Belge Satırları

`docs/envanter/ekran-kaydedici.md` (12 Eylül 2026) iki yerde eskidi: "ekran ve pencere yakalama izi kaynakta sıfır" ve "mikrofon ve sistem sesi yok" diyor. Bugün ikisi de var.

Depoya yazmak bu turun dışında olduğu için belge düzeltilmedi; düzeltme ikinci aşamanın işi.

### Yokluk Aramaları

"Bizde yok" denen her şey kaydedici dosyalarında en az iki adla arandı. Hepsi sıfır eşleşme verdi:

- Uygulama başına ses: `PROCESS_LOOPBACK`, `ActivateAudioInterface`.
- HDR: `output_fmt`, `smpte2084`, `allow_hdr`.
- Zamanlanmış kayıt: `Schedul`, `Zamanla`, `StartAt`.
- Mikrofon tuşu: `PushToTalk`, `BasKonus`, `MuteMic`, `Sustur`.
- Yalnız ses ya da yalnız kamera: `AudioOnly`, `YalnizSes`, `WebcamOnly`, `CameraOnly`.
- Kare hızı kipi: `fps_mode`, `vsync`.
- Yarım dosyayı açılışta bulma: `Orphan`, `Recover`, `kurtar`.
- Disk alanı: `AvailableFreeSpace`, `DiskFull`, `ENOSPC`.
- Aygıt kopması: `DeviceLost`, `Reconnect`, `ACCESS_LOST`.
- Bütün ekranlar: `AllScreens`, `TumEkran`, `VirtualScreen`.
- Açılışta tampon: `AutoStart`, `StartWithWindows`, `RunAtLogin`.
- Fareyi izleyen bölge: `follow_mouse`, `FollowMouse`.
- Wayland ve yeni yakalama yolları: `pipewire`, `xdg-desktop-portal`, `ScreenCaptureKit`, `SCStream`, `gfxcapture`.
- Ses sıkıştırıcı ve sınırlayıcı: `acompressor`, `alimiter`, `Compressor`, `Limiter`, `loudnorm`. En yakın kod `Core/AudioCaptureArguments.cs:75-84`.
- Bildirimleri susturma: `DoNotDisturb`, `FocusAssist`, `RahatsizEtme`, `QuietHours`.
- Kayıt durum sesi: `StartSound`, `StopSound`, `BildirimSesi`, `KayitSesi`. Ses çalan tek kod tıklama sesidir (`App/Recorder/RecorderInput.cs:151`).
- Kayıt profili: `RecorderProfile`, `RecorderPreset`, `OnAyar`, `KayitProfil`.
- WebP ve APNG kabı: `webp`, `apng`.
- Tamponu tuşla başlatma: `ReplayToggle`, `ToggleReplay`, `TamponBaslat`. Tamponu kaydetme kısayolu var, başlatma kısayolu yok (`App/Recorder/RecorderHotkeys.cs:14-22`).
- Komut satırından kayıt: `src/VidShrink.Cli` içinde `Recorder`, `kaydedici`, `ddagrab`, `gdigrab`.

Açık işler defterinde (`.claude/acik.md`) bu eksiklerin hiçbiri yazılı değil. Defterdeki kaydedici satırları gerçek pencere denemeleriyle ilgili (satır 16, 77, 83); bu yüzden tabloda `defterde` işareti yok.

## Rakipler

Sürümler 13 Ekim 2026'da `gh api` ile ya da resmi sayfadan okundu.

| Ad | Sürüm | Tarih | Tür | URL |
|---|---|---|---|---|
| OBS Studio | 32.2.2 (33.0.0-rc1 ön sürüm, 9 Ekim 2026) | 14 Ağustos 2026 | Açık kaynak; Windows, macOS, Linux | https://obsproject.com |
| ShareX | 21.0.0 | 3 Temmuz 2026 | Açık kaynak; Windows | https://getsharex.com |
| Cap | 0.6.0 | 15 Eylül 2026 | Açık kaynak ve bulut; macOS, Windows, 0.6.0'da Linux paketleri | https://cap.so |
| Bandicam | 27.0.6.2686 | 9 Eylül 2026 | Ücretli; Windows | https://www.bandicam.com |
| NVIDIA App | Sürüm okunmadı | — | Ücretsiz; NVIDIA kartlı Windows | https://www.nvidia.com/en-us/software/nvidia-app/ |
| macOS Ekran Kaydı (Screenshot, QuickTime Player) | macOS Tahoe 26 ve macOS 27 | — | İşletim sisteminin kendi aracı | https://support.apple.com/en-us/102618 |
| Snipping Tool (Ekran Alıntısı Aracı) | 11.2507.14.0 (pencere kipi, Insider) | Temmuz 2025 | Windows'un kendi aracı | https://blogs.windows.com/windows-insider/ |
| CleanShot X | Sürüm okunmadı | — | Ücretli; macOS | https://cleanshot.com |
| Icecream Screen Recorder | 7.48 | Sayfada 6/5/2026 | Ücretsiz sınırlı ve ücretli; Windows | https://icecreamapps.com/Screen-Recorder/ |
| Kooha | 2.3.2 | 5 Haziran 2026 | Açık kaynak; Linux | https://github.com/SeaDve/Kooha |
| vokoscreenNG | 4.11.0 | 2 Ekim 2026 | Açık kaynak; Windows, Linux | https://github.com/vkohaupt/vokoscreenNG |
| GPU Screen Recorder | Sürüm sayfada görülmedi | — | Açık kaynak; Linux | https://git.dec05eba.com/gpu-screen-recorder/about/ |
| Screenity | 4.6.12 | 25 Eylül 2026 | Açık kaynak; Chrome eklentisi | https://github.com/alyssaxuu/screenity |
| ScreenToGif | 2.43.2 | 28 Temmuz 2026 | Açık kaynak; Windows | https://github.com/NickeManarin/ScreenToGif |
| GNOME Ekran Kaydı | Sürüm okunmadı (yardım sayfasının kararlı sürümü) | — | Linux masaüstünün kendi aracı | https://help.gnome.org/users/gnome-help/stable/screen-shot-record.html |

Yalnız sürümüne bakılanlar (özellik sayfası okunmadı): SimpleScreenRecorder 0.4.4 (yayın kaydı yok, son commit 14 Ağustos 2026), wl-screenrec 0.3.1 (10 Ağustos 2026), wf-recorder 0.6.0 (17 Ekim 2025), Kap 3.6.0 (27 Ekim 2022; dört yıldır sürüm yok), KDE Spectacle 6.7.5 (8 Eylül 2026; uygulama sayfası yalnız ekran görüntüsünü anlatıyor [36]).

## Özellik Matrisi

`var` resmi kaynakta görüldü demektir. `?` bakılan sayfada geçmedi, yani doğrulanmadı; "rakipte yok" anlamına gelmez.

| Özellik | Bizde | OBS | ShareX | Cap | Bandicam | Başka Kimde Belgeli | Kaynak |
|---|---|---|---|---|---|---|---|
| Ekran ve bölge | var | var | var | var | var | macOS aracı, Kooha, CleanShot X | [2][7][10][12][14][24][27] |
| Gerçek pencere yakalama | yarım: ekran kırpması | var | ? | var; hata kayıtları açık | var | macOS aracı (Tahoe 26), GPU Screen Recorder, CleanShot X | [2][11][12][14][26][27][31] |
| Oyun yakalama | yarım: ekran kaydına girer | var, yalnız Windows | ? | ? | var | NVIDIA App | [2][12][15] |
| Bütün ekranlar tek kayıtta | yarım | ? | ? | ? | var | Kooha (deneysel) | [12][24] |
| Web kamerası katmanı | yarım: yalnız Windows | var | ? | var | var | CleanShot X, vokoscreenNG, Icecream | [2][10][12][25][27][28] |
| Aygıt kaynağı (yakalama kartı, yalnız kamera) | yarım: kamera yalnız katman | var | ? | ? | var | ScreenToGif, Screenity | [2][12][29][30] |
| Sistem sesi | var; macOS'ta yarım | var | var (ek aygıt paketiyle) | var | var | macOS aracı (macOS 27), Kooha, CleanShot X, Icecream | [2][7][9][12][14][24][27][28] |
| Uygulama başına ses | yok | var | ? | ? | var | GPU Screen Recorder | [3][4][12][26] |
| Ayrı ses izleri | var | ? | ? | var | ? | NVIDIA App (mikrofon ayrı ize) | [9][15] |
| Gürültü bastırma | var: `afftdn`, `agate` | var | ? | var | ? | ? | [1][9] |
| Ses sıkıştırıcı ve sınırlayıcı | yok | var | ? | ? | ? | ? | [32] |
| Bas-konuş | yok | var | ? | ? | ? | NVIDIA App, Screenity | [6][15][29] |
| Anlık tekrar | var: 15–120 sn | var | ? | ? | ? | NVIDIA App, GPU Screen Recorder | [15][26][33] |
| Zamanlanmış kayıt | yok | ? | ? | ? | var | Icecream | [12][13][28] |
| Süre ya da boyutla durma | var | ? | ? | ? | var | Screenity | [12][29] |
| Çökmeye dayanıklı kayıt | var: Matroska'ya yakalama | var | ? | var | ? | ? | [1][5][9] |
| Düşük disk uyarısı | yarım: denetim yalnız Küçült'te | var (dolaylı) | ? | var | ? | ? | [1][9] |
| HDR yakalama | yok | ? | ? (istek açık) | ? | ? | NVIDIA App, macOS aracı, GPU Screen Recorder | [8][14][16][26] |
| GIF kaydı | var | ? | var | ? | ? | Snipping Tool, Kooha, CleanShot X, ScreenToGif | [8][17][24][27][30] |
| Bölüm işareti | var | var | ? | ? | ? | ? | [5] |
| Yalnız ses kaydı | yok | ? | var | ? | var | ? | [7][12] |
| Komut satırından kayıt | yok | var | var | ? | ? | GPU Screen Recorder | [7][26][33] |
| Genel kısayol | var: yalnız Windows | var | var | ? | ? | GNOME Ekran Kaydı | [1][7][35] |
| Wayland'da yakalama | yarım: yalnız X11 | var | Windows aracı | var (0.6.0) | Windows aracı | Kooha, GPU Screen Recorder | [1][9][24][26] |
| macOS'ta ek sürücüsüz sistem sesi | yok | var (macOS 13 ve sonrası) | Windows aracı | ? | Windows aracı | macOS aracı (macOS 27), CleanShot X | [4][14][27] |

### Ana Sorulara Yanıt

**(a) Kaynak.** OBS ekran, pencere, oyun, web kamerası ve yakalama kartını ayrı kaynak olarak sunuyor [2]. Bandicam ekran, belirli pencere, oyun ve harici aygıt kipi sunuyor [12]. Bizde ekran, bölge ve "pencere" var; pencere aslında ekran kırpması. Çok ekranı tek dosyaya alma, oyun yakalama ve aygıt kaynağı yok. Kamera yalnız katman ve yalnız Windows.

**(b) Platform.** Kodda macOS için `avfoundation`, Linux için `x11grab` yolu var. OBS macOS 13 ve sonrasında ScreenCaptureKit tabanlı "macOS Screen Capture" kaynağını, Linux'ta PipeWire kaynağını kullanıyor [1][2][4]. PipeWire portalı, Wayland'da ekran yakalamanın kullanıcı onaylı yoludur. ffmpeg'in giriş aygıtları arasında PipeWire ve ScreenCaptureKit yok [22]; yani bu iki açık ffmpeg ile kapanmıyor.

**(c) Ses.** Bizde mikrofon ve sistem sesi ayrı izlere yazılabiliyor. Uygulama başına ses OBS'te (Windows ve macOS), Bandicam'de ve GPU Screen Recorder'da belgeli [3][4][12][26]. Gürültü bastırma OBS'te ve Cap'te var [1][9]; bizde iki basit süzgeç var.

**(d) Güvenilirlik.** Bizde MP4 ve MOV kaydı Matroska'ya yazıldığı için yarıda kesilen kayıt okunur kalıyor. Eksik iki yer: uygulama çökünce kalan dosya açılışta sunulmuyor ve kaydedici boş disk alanına bakmıyor. OBS, 32.0'dan (22 Eylül 2025) beri Hybrid MP4'ü varsayılan yapıyor [1][5]. Cap 0.6.0 kesilen kaydı kurtarıyor ve disk azalınca uyarıyor [9].

**(e) Anlık Tekrar Ve Zamanlanmış Kayıt.** Anlık tekrar ekran kartı araçlarının ve oyun araçlarının ana özelliği [15][26]. Zamanlanmış kayıt Bandicam'de ve Icecream Screen Recorder'da belgeli [13][28]; açık kaynak araçlarda bu turda görülmedi. OBS, kaydı ve tekrar tamponunu açılışta kendiliğinden başlatan iki parametre sunuyor [33].

**(f) Kare Hızı Ve Başarım.** Bizde tavan 240 fps; hızlı yol 60 fps isteği tutuyor (ölçüm yukarıda). Donanım kodlayıcı yalnız Windows'un üç markasında. CFR/VFR seçimi ve HDR yok. `ddagrab` 10 ve 16 bit çıkış verebiliyor [21]; yani HDR'nin yakalama ucu ffmpeg'de hazır, eksik bizim argüman ve kodlama tarafımız. NVIDIA App 4K'da 120 fps'e, 8K'da 60 fps'e kadar AV1 ile kaydediyor [15]. Bizde 120 fps yalnız `gdigrab` yolunda ölçüldü ve 81,3 fps geldi.

**(g) Talepler Ve Windows Araçları.** Aşağıdaki iki bölümde.

### Öncelik Verirken

Ölçü ORTAK.md'deki gibi uygulandı. İki yerde yargı kullanıldı; kural harfiyle okunursa bu satırların sırası değişir.

**Üç ya da daha çok rakipte belgeli olduğu halde öncelik 2 alanlar:** EKA-04, EKA-05, EKA-06, EKA-07, EKA-08, EKA-09, EKA-12, EKA-16. Ortak gerekçe: kaydın ana işinin (ekranı sesiyle, kesilmeden kaydetmek) kenarında duruyorlar.

- EKA-04: pencere bugün ekran kırpmasıyla kaydediliyor; kayıt alınıyor ama kusurlu, ve eksik yalnız macOS'ta.
- EKA-05: oyun bugün ekran kaydına giriyor. Tam ekran oyunun `ddagrab` ile ne kadar yakalandığı ölçülmedi. ShareX'teki 13 oylu isteğin numarası (#848) eski bir kayda işaret ediyor; açılış tarihi okunmadı.
- EKA-06: yeni bir kaynak türü; ekran kaydının kendisi değil.
- EKA-07 ve EKA-12: Windows'ta var olan özelliğin öbür sistemlerdeki karşılığı.
- EKA-08: yalnız HDR ekranı olan kullanıcıyı ilgilendiriyor; NVIDIA iddiası tek kaynakta.
- EKA-09: OBS kanıtı bir hata kaydı; resmi belge sayfası bulunamadı.
- EKA-16: ShareX kanıtı 2013 tarihli; betikle kayıt başlatan kitle dar.

**İki rakipte belgeli olduğu halde öncelik 3 alanlar:** EKA-23 (birden çok mikrofon) ve EKA-25 (bütün ekranlar tek kayıtta). Gerekçe: dar kitle; Kooha'da çok ekran deneysel [24].

**Öncelik 1 alan üç eksikte** rakip iddiası ayrı kaynaklarda görüldü: EKA-01 [3][12][26], EKA-02 [1][24][26], EKA-03 [4][14][27].

## Kullanıcı Talepleri

Oy sayısı, kaydın aldığı başparmak tepkisidir; 13 Ekim 2026'da `gh api search/issues` ile okundu.

| İstek | Oy | URL |
|---|---|---|
| ShareX: Linux desteği | 68 | https://github.com/ShareX/ShareX/issues/6625 |
| ShareX: WebP, AVIF ve JPEG XL olarak kaydetme | 41 | https://github.com/ShareX/ShareX/issues/6090 |
| ShareX: durağan WebP | 32 | https://github.com/ShareX/ShareX/issues/5250 |
| ShareX: yüksek DPI desteği | 18 | https://github.com/ShareX/ShareX/issues/5312 |
| ShareX: oyunlardan yakalama | 13 | https://github.com/ShareX/ShareX/issues/848 |
| ShareX: GIF kaydının dosya boyutu çok büyük | 7 | https://github.com/ShareX/ShareX/issues/5183 |
| ShareX: HDR'yi doğru ele alma | 6 | https://github.com/ShareX/ShareX/issues/7992 |
| Cap: Linux sürümü | 10 | https://github.com/CapSoftware/Cap/issues/609 |
| Cap: kayıtta titreme ve takılma | 10 | https://github.com/CapSoftware/Cap/issues/1557 |
| Cap: yalnız web kamerası kayıt kipi | 4 | https://github.com/CapSoftware/Cap/issues/504 |
| Cap: H.265 dışa aktarma | 4 | https://github.com/CapSoftware/Cap/issues/192 |
| Cap: kayıt profilleri | 2 | https://github.com/CapSoftware/Cap/issues/856 |
| Cap: pencere yakalama pencere içeriği yerine ekran bölgesini kaydediyor | 2 | https://github.com/CapSoftware/Cap/issues/1754 |
| Cap: sesten gürültü giderme | 1 | https://github.com/CapSoftware/Cap/issues/1448 |
| Cap: Windows'ta pencere kaydı hatası | 1 | https://github.com/CapSoftware/Cap/issues/2151 |
| OBS (hata): Wayland'da açık eşzamanlama çöküşü | 20 | https://github.com/obsproject/obs-studio/issues/11022 |
| OBS (hata): bas-konuş doğru çalışmıyor | 8 | https://github.com/obsproject/obs-studio/issues/10701 |
| OBS (hata): macOS ekran yakalama birkaç saat sonra donuyor | 7 | https://github.com/obsproject/obs-studio/issues/9056 |
| OBS (hata): macOS ekran yakalamada başarım düşüşü | 4 | https://github.com/obsproject/obs-studio/issues/10636 |
| OBS (hata): PipeWire pencere yakalamada takılma | 3 | https://github.com/obsproject/obs-studio/issues/10535 |
| OBS (hata): MKV'den MP4'e aktarımdan sonra ses çıtırtısı | 3 | https://github.com/obsproject/obs-studio/issues/13249 |

Okuma notları:

- ShareX'in WebP, AVIF ve JPEG XL istekleri ekran görüntüsü biçimleriyle ilgili. Bu, K9'un kapattığı alan; kayıt eksiği sayılmadı.
- Cap #1754 ve #2151, bizim pencere davranışımızın aynısını hata diye bildiriyor: pencere yerine ekran bölgesi kaydediliyor. Kullanıcı bunu özellik değil kusur olarak görüyor.
- OBS'in GitHub'ı yalnız hata alıyor; özellik istekleri ayrı fikir panosunda. O pano bu turda okunamadı.
- OBS #13249 bizi ilgilendiriyor: biz de kaydı Matroska'dan MP4'e aktarıyoruz.

## Son İki Yılın Yenilikleri

- **FFmpeg 8.1: `gfxcapture`.** Windows Graphics Capture (WGC; tek bir pencereyi üstü örtülse de yakalayan Windows arayüzü) tabanlı pencere ve ekran kaynağı ffmpeg'e girdi [19]. Seçenekleri arasında pencereyi başlık, sınıf ya da uygulama adıyla seçme, imleç, sarı kenarlığı gösterme ve 10 ya da 16 bit çıkış var [20]. ShareX 20.2.0 (22 Mayıs 2026) ffmpeg'ini 8.1'e çıkardı [7].
- **OBS 32.0 (22 Eylül 2025).** Hybrid MP4 ve Hybrid MOV varsayılan kap oldu ve betadan çıktı; NVIDIA ses efektlerine konuşma algılama eklendi [1]. Hybrid MP4, yarıda kesilse de kurtarılabilen ama her yerde açılan MP4 türüdür [5].
- **OBS 31.1.** PipeWire ekran yakalamaya açık eşzamanlama desteği; macOS'ta kısayollar için izin türü değişti [1].
- **OBS 32.2 (21 Temmuz 2026).** SDR'yi HDR içine yerleştiren süzgeç; ekran yakalamanın bazen siyah vermesi düzeltildi [1].
- **Cap 0.6.0 (15 Eylül 2026).** Mikrofon gürültüsünü üç kademede azaltan "Studio Sound", kesilen kaydı kurtarma, düşük disk uyarısı, kopan mikrofon ve kamerayı geri bağlama, Linux paketleri [9].
- **ShareX.** 16.0.0'da (10 Mart 2024) `ddagrab` ve duraklat geldi; 17.0.0'da (8 Ocak 2025) kayıt durdurma, duraklatma ve iptal için bildirim sesleri [7].
- **Snipping Tool.** Kaydı GIF olarak dışa aktarma, Haziran 2025'te Insider kanallarına çıktı; en çok 30 saniye [17]. Pencere kipinde kayıt 11.2507.14.0 sürümünde geldi; bölge pencereye oturuyor ama pencereyi izlemiyor [18].
- **macOS.** Tahoe 26 ile seçili pencereyi kaydetme ve HDR (HEVC) yakalama biçimi geldi; sayfa sistem sesini de macOS 27 ile birlikte anıyor [14].
- **NVIDIA App.** AV1 ile 4K 120 fps ya da 8K 60 fps kayıt, anlık tekrar, bas-konuş tuşu ve mikrofonu ayrı ize yazma [15]. HDR video kaydını yalnız [16] anıyor.
- **K3 için not.** Pazar bu konuda iki şeyi değiştirdi: ffmpeg artık WGC'yi kendi süzgeciyle veriyor [19][20] ve yeni Windows yapılarında kenarlık, erişim istenerek kapatılabiliyor [23]. İki işletim sisteminin kendi aracı da pencere kipini son bir buçuk yılda ekledi [14][18]. OBS belgesi, üstü örtülse de yalnız seçilen pencerenin göründüğünü söylüyor [31]. Kararı yeniden açmak sahibin işi.

## Doğrulanamayanlar

- **Xbox Game Bar.** Resmi destek sayfası bulunamadı; resmi alan adlarında yapılan arama yalnız Microsoft Q&A forum başlıklarını verdi. Arka plan kaydı, yalnız oyun sesi ve mikrofon tuşu ikincil kaynaklarda geçiyor; tabloya rakip olarak yazılmadı.
- **Steam Game Recording ve AMD Adrenalin.** Resmi sayfaların arama özeti okundu, sayfanın kendisi değil: Steam sayfası doğrudan açılınca yalnız gezinme çubuğu geldi [34], AMD sayfası zaman aşımına düştü. Arka plan kaydı (Steam) ile "son 20 dakika" ve "mikrofon ayrı dosya" (AMD) bu özetlerden. İkisi de tabloya rakip olarak yazılmadı. Steam'in arka plan kaydı doğrulanırsa EKA-19'un ikinci rakibi olur.
- **Snipping Tool pencere kipi.** Resmi Insider yazısı bulunamadı; bilgi ikincil kaynaktan [18].
- **macOS 27 ve sistem sesi.** Apple sayfasının özeti "Include System Audio" seçeneğini macOS 27 ile anıyor [14]. İkinci okuma aynı cümleyi verdi: seçenek macOS 27 Golden Gate ve sonrasında. İki okuma da aynı tek sayfadan.
- **OBS'in pencere yakalama yöntemi.** OBS belgesi yöntemleri "Otomatik", "BitBlt" ve "Windows 10" diye adlandırıyor ve üstü örtülse de yalnız seçilen pencerenin göründüğünü söylüyor [31]. Sayfa WGC adını ve sarı kenarlığı anmıyor.
- **OBS'te ayrı ses izleri, bas-konuş ayarı ve tamponun kayıtla birlikte çalışması.** Tekrar tamponunun varlığı başlatma parametresinden doğrulandı [33]. Bas-konuş için yalnız hata kaydı (#10701) ve forum başlıkları görüldü; resmi belge sayfası bulunamadı. Tamponun kayıtla aynı anda çalıştığı doğrulanmadı; bizde tampon yalnız kayıt yokken çalışıyor (`App/Recorder/RecorderView.Tampon.cs:71-85`).
- **OBS'in disk alanı denetimi.** Yalnız dolaylı kanıt var: 30.2 sürüm notundaki bir düzeltme, dosyaya yazmayan çıktıda disk alanına bakılmasını hata sayıyor [1].
- **Cap'in Linux durumu.** README macOS ve Windows diyor [10]; 0.6.0 notu Linux paketlerinden söz ediyor [9]. 10 oylu "Linux sürümü" isteğinin kapanıp kapanmadığına bakılmadı.
- **Tam ekran oyun kaydı bizde.** `docs/olcumler/kaydedici-ddagrab.md` durağan masaüstünü ölçtü. Tam ekran bir oyunun `ddagrab` ile kaydı ölçülmedi.
- **Disk dolunca bizde ne oluyor.** Kodda özel bir kol yok; ffmpeg'in yazamayınca düşeceği ve kaydın "yarım" sayılacağı beklenir, ama ölçülmedi.
- **Matroska'dan MP4'e aktarımda ses bütünlüğü.** OBS'te bu yolda çıtırtı hatası açık (#13249). Bizim aktarımımızda ses ölçülmedi.
- **Kenarlıksız WGC paketsiz uygulamada.** Microsoft belgesi kenarlığı kapatmak için paket bildiriminde bir yetenek istiyor [23]. Bizim gibi paketsiz bir uygulamada işleyip işlemediği doğrulanmadı.
- **Icecream sürüm tarihi.** Sayfa 6/5/2026 yazıyor; 5 Haziran mı 6 Mayıs mı belli değil.
- **NVIDIA App.** Sayfa anlık tekrarın uzunluğunun ayarlandığını söylüyor; üst sınırı ve tamponun oturumlar arasında açık kalıp kalmadığını söylemiyor [15]. HDR video kaydı yalnız [16]'da geçiyor; tek okuma.
- **Tekrar tamponu süresinde öbür rakipler.** AMD'nin "son 20 dakika"sı ve OBS'in en uzun tekrar süresi ayarı yalnız arama özetinden ve forumdan. EKA-18 bu yüzden tek rakiple yazıldı.
- **Bandicam komut satırı.** Denenen sayfa adresi 404 verdi; Bandicam bu yüzden EKA-16'ya yazılmadı.
- **ShareX'te WebP ve APNG kaydı.** Önceki tarama bunu değişiklik günlüğüne dayanarak yazmış. Günlük iki parça halinde okundu, girdi görülmedi. Doğrulanırsa tabloya girer: bizde bu iki kap yok (`Core/RecorderArguments.cs:46-58`).
- **ShareX belge sayfası.** `getsharex.com/docs/screen-recording` 404 verdi; ShareX bilgisi yalnız değişiklik günlüğünden.
- **ShareX'in eski girdileri.** Yalnız ses kaydı 9.3.0 (10 Eylül 2014), kaydedici için komut satırı 8.0.0.463 (12 Ağustos 2013) tarihli girdilerden. Bugünkü 21.0.0 sürümünde sürdükleri ayrıca doğrulanmadı; EKA-15 ve EKA-16'da ShareX bu kayıtla sayıldı.
- **vokoscreenNG "timer".** README bir zamanlayıcı aracını anıyor [25]; bunun saatli başlatma olup olmadığı doğrulanmadı.
- **GNOME ve KDE.** GNOME yardım sayfası ekranın tamamını ya da seçili alanı kaydetmeyi anlatıyor; pencere ve ses konusunda bir şey söylemiyor [35]. KDE Spectacle'ın uygulama sayfası kayıt özelliğini anmıyor [36].
- **OBS fikir panosu.** `ideas.obsproject.com` yalnız başlık döndürdü; oy sayıları okunamadı.
- **Kayıt profilleri.** Cap'te 2 oylu açık istek var (#856). OBS'in profil özelliği bu turda resmi sayfadan okunmadı. Bizde kayıt profili yok.
- **120 ve 144 fps bizde.** `ddagrab` yolunda yalnız 60 fps ölçüldü; 120 fps yalnız `gdigrab` yolunda ölçüldü.
- **HDR ekranda bizim kaydımız.** `docs/olcumler/` altında kaydedici için HDR ölçümü yok; HDR ekranda alınan kaydın nasıl göründüğü bilinmiyor.
- **Cap "Studio Sound".** Sürüm notu üç kademeyi söylüyor, yöntemi söylemiyor [9]; model tabanlı olup olmadığı doğrulanmadı.
- **Açılmayan rakipler.** Loom, ScreenPal, FlashBack, Action! ve ScreenRec bu turda açılmadı.

## Kaynaklar

- [1] https://github.com/obsproject/obs-studio/releases — OBS sürüm listesi ve 30.2.0, 31.0.0, 31.1.0, 32.0.0, 32.1.0, 32.2.0 sürüm notları
- [2] https://obsproject.com/kb/sources-guide — OBS kaynak türleri ve hangi sistemde oldukları
- [3] https://obsproject.com/kb/application-audio-capture-guide — OBS'te Windows uygulama sesi
- [4] https://obsproject.com/kb/macos-desktop-audio-capture-guide — OBS'te macOS masaüstü ve uygulama sesi
- [5] https://obsproject.com/kb/hybrid-mp4 — Hybrid MP4 ve bölüm işareti
- [6] https://github.com/obsproject/obs-studio/issues — OBS açık hata kayıtları, tepki sayısına göre
- [7] https://getsharex.com/changelog — ShareX değişiklik günlüğü, 16.0.0'dan 21.0.0'a
- [8] https://github.com/ShareX/ShareX/issues — ShareX açık istekler, oy sayısına göre
- [9] https://github.com/CapSoftware/Cap/releases — Cap sürümleri ve 0.6.0 notları
- [10] https://github.com/CapSoftware/Cap — Cap README: kayıt kipleri ve sistemler
- [11] https://github.com/CapSoftware/Cap/issues — Cap açık istekler ve hatalar
- [12] https://www.bandicam.com/ — Bandicam sürümü ve kayıt kipleri
- [13] https://www.bandicam.com/support/tips/scheduled-recording/ — Bandicam zamanlanmış kayıt
- [14] https://support.apple.com/en-us/102618 — macOS ekran kaydı seçenekleri
- [15] https://www.nvidia.com/en-us/geforce/news/nvidia-app-download-and-features/ — NVIDIA App kayıt özellikleri
- [16] https://www.nvidia.com/en-gb/geforce/news/nvidia-app-beta-update-av1-performance-tuning — NVIDIA App'te AV1 ve HDR kayıt
- [17] https://blogs.windows.com/windows-insider/2025/06/19/gif-export-in-snipping-tool-begins-rolling-out-to-windows-insiders/ — Snipping Tool GIF dışa aktarma
- [18] https://winaero.com/windows-11-build-27924-canary-adds-window-mode-screen-recording-to-snipping-tool — Snipping Tool pencere kipi (ikincil kaynak)
- [19] https://raw.githubusercontent.com/FFmpeg/FFmpeg/master/Changelog — FFmpeg değişiklik günlüğü: 8.1'de `gfxcapture`, 6.0'da `ddagrab`
- [20] https://github.com/FFmpeg/FFmpeg/blob/master/libavfilter/vsrc_gfxcapture.c — `gfxcapture` seçenekleri
- [21] https://github.com/FFmpeg/FFmpeg/blob/master/libavfilter/vsrc_ddagrab.c — `ddagrab` seçenekleri: 10 ve 16 bit çıkış, kare çoğaltma
- [22] https://github.com/FFmpeg/FFmpeg/tree/master/libavdevice — ffmpeg giriş aygıtları: PipeWire ve ScreenCaptureKit yok
- [23] https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.graphicscapturesession.isborderrequired — WGC'de kenarlığı kapatma koşulları
- [24] https://github.com/SeaDve/Kooha — Kooha README
- [25] https://github.com/vkohaupt/vokoscreenNG — vokoscreenNG README
- [26] https://git.dec05eba.com/gpu-screen-recorder/about/ — GPU Screen Recorder özellikleri
- [27] https://cleanshot.com/features — CleanShot X kayıt özellikleri
- [28] https://icecreamapps.com/Screen-Recorder/ — Icecream Screen Recorder özellikleri
- [29] https://github.com/alyssaxuu/screenity — Screenity README
- [30] https://github.com/NickeManarin/ScreenToGif — ScreenToGif README
- [31] https://obsproject.com/kb/window-capture-sources — OBS pencere yakalama: yöntemler ve örtülen pencere
- [32] https://obsproject.com/kb/filters-guide — OBS ses süzgeçleri: sıkıştırıcı, sınırlayıcı, gürültü kapısı, gürültü bastırma
- [33] https://obsproject.com/kb/launch-parameters — OBS başlatma parametreleri: kaydı ve tekrar tamponunu açılışta başlatma
- [34] https://help.steampowered.com/faqs/view/23B7-49AD-4A28-9590 — Steam Game Recording sıkça sorulanlar; yalnız arama özetinden okundu
- [35] https://help.gnome.org/users/gnome-help/stable/screen-shot-record.html — GNOME ekran kaydı ve kısayolu
- [36] https://apps.kde.org/spectacle/ — KDE Spectacle sürümü; sayfa kaydı anmıyor

## Komşu Kola Not

- Cap 0.6.0 kamera arka planını bulanıklaştırma ve kaldırma, imleç biçimleri, tıklama dalgası ve otomatik 3D kamera hareketi getirdi [9].
- CleanShot X tıklama vurgusu ve tuş vuruşu gösterimini renk, boyut ve süzgeç ayarıyla sunuyor [27].
- Bandicam kayıt sırasında çizim ve fare efektleri sunuyor [12]; Screenity yakınlaştırma ve yapay zekâlı kamera arka planı sunuyor [29].
- vokoscreenNG büyüteç, tıklama gösterimi ve imleç halesi sunuyor [25]; GPU Screen Recorder kayıt bitince betik çalıştırıyor [26].
- Bizde tıklama, tuş ve büyüteç ayar alanları duruyor (`App/Recorder/RecorderSettings.cs:70-76`); bu kol onlara bakmadı.
