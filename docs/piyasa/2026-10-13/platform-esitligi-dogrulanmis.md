# Platform Eşitliği — Doğrulanmış Eksikler

Kod okuması `eb0a5db0` üzerinde. macOS ve Linux davranışı yalnız koddan okundu; bu makine Windows.

| No | Eksik | Kimde Var | Bizde Durum | Fayda | İş | Öncelik | Karar | Kaynak |
|---|---|---|---|---|---|---|---|---|
| PLT-01 | macOS'ta ekran seçimi: avfoundation'a (ffmpeg'in macOS yakalama girişi) ekranı sıra numarasıyla değil "Capture screen N" adıyla vermek. | OBS Studio, CleanShot X, Screen Studio | yarım: ekran, açılır kutudaki sırasıyla `-i "0:none"` diye veriliyor; kutu monitör listesinden dolar, bölge ve pencere hedefi de aynı girdiden geçer; ffmpeg ekranları kameralardan sonra numaralıyor, kameralı Mac'te 0 kamerayı açar; gerçek Mac'te ölçülmedi (Core/RecorderArguments.cs:1461-1467; App/Recorder/RecorderView.Secici.cs:79, 132, 138; App/Recorder/RecorderView.Hedef.cs:177) | Kameralı Mac'te ekran kaydı kamerayı değil ekranı kaydeder. | küçük | 1 | gerekmez | [1][8][9][16][17][45] |
| PLT-02 | Wayland'da ekran, bölge ve pencere yakalama: ScreenCast portalı (masaüstünün izin penceresi) ve PipeWire (Linux'un ortak görüntü ve ses borusu) ile yakalamak. | OBS Studio, Kooha, GPU Screen Recorder | yarım: yalnız x11grab var; pencere hedefi hata satırı verir, ekran ve bölge hedefinde Wayland kapısı yok (Core/RecorderArguments.cs:1469-1486; App/Recorder/RecorderView.Secici.cs:45-48) | Wayland oturumunda kaydedici çalışır. | büyük | 1 | sahip: ffmpeg'de PipeWire girişi yok, yeni bağımlılık ya da yardımcı süreç gerekir | [1][2][10][14][15][31] |
| PLT-03 | macOS'ta sistem sesi: ScreenCaptureKit (Apple'ın ekran ve ses yakalama çatısı) ile sanal aygıt kurmadan bilgisayarın sesini kaydetmek. | OBS Studio, CleanShot X, Screen Studio | yarım: yalnız adında "loopback" ya da "monitor" geçen sanal ses aygıtı kuruluysa seçilebilir (Ffmpeg/CaptureDevices.cs:58-59; Ffmpeg/SystemAudioCapture.cs:36) | Mac'te ek sürücü kurmadan bilgisayarın sesi kayda girer. | büyük | 1 | sahip: ffmpeg belgesinde avfoundation için sistem sesi yolu yok, yerel yardımcı ikili ve Ekran Kaydı izni gerekir | [1][6][8][9][16][17] |
| PLT-04 | macOS izin akışı: Ekran Kaydı ve Mikrofon iznini kayıttan önce yoklamak, eksikse Sistem Ayarları'na yönlendirmek ve pakete izin açıklamalarını koymak. | doğrulanmadı (Apple belgesi izni şart koşuyor; rakiplerin akışı bu turda görülmedi) | yok: izlenen dosyalarda `UsageDescription`, `CGPreflightScreenCaptureAccess` ve `AVCaptureDevice` araması boş; paket ad-hoc imzalı (macos-app-bundle.sh:82-112, 118) | İlk kayıt siyah ekran ya da sessiz ses yerine açık bir yönlendirmeyle başlar. | orta | 2 | sahip: yeni izin istemi ve paket içeriği değişir | [6][7] |
| PLT-05 | macOS'ta gerçek pencere yakalama: pencereyi ekran kırpmasıyla değil kendi içeriğiyle yakalamak. | OBS Studio, CleanShot X, Screen Studio | yarım: pencere dikdörtgeni kayıt başında ekran kırpmasına çevrilir (Core/RecorderArguments.cs:1497-1508; App/Recorder/RecorderView.Secici.cs:79) | Pencere taşınınca ya da üstüne başka pencere gelince kayıt bozulmaz. | büyük | 2 | sahip: PLT-03 ile aynı yardımcı ikili; Windows'taki karşılığı K3 rafta | [6][8][16][17] |
| PLT-06 | macOS'ta kamera katmanı: kamerayı kaydın üstüne küçük pencere olarak bindirmek. | CleanShot X, Screen Studio, OBS Studio | yok: kamera paneli Windows dışında gizli (App/Recorder/RecorderView.Kamera.cs:53; Core/RecorderArguments.cs:894-895) | Mac'te anlatımlı kayıtta yüz de görünür. | orta | 2 | sahip: Kamera izni istemi eklenir | [1][10][16][17] |
| PLT-07 | Linux'ta kamera katmanı: V4L2 (Linux'un kamera arabirimi) aygıtını kaydın üstüne bindirmek. | OBS Studio, GPU Screen Recorder | yok: kamera paneli gizli, kamera listesi hep boş (App/Recorder/RecorderView.Kamera.cs:53; Ffmpeg/CaptureDevices.cs:252) | Linux'ta anlatımlı kayıtta yüz de görünür. | orta | 2 | gerekmez | [1][9][15] |
| PLT-08 | Linux'ta genel kısayol: uygulama odakta değilken kaydı durduran tuşlar; Wayland'da yol GlobalShortcuts portalı. | GPU Screen Recorder (masaüstü kısayoluna bağlanan betikle); OBS Studio'da Wayland kolu açık PR | yok: Windows dışında boş uygulama (App/Recorder/RecorderView.Kisayol.cs:31-34) | Tam ekran uygulamadan çıkmadan kayıt durdurulur ve anlık tekrar kaydedilir. | orta | 2 | sahip: D-Bus istemcisi bağımlılığı gerekebilir | [3][12][13][15][40] |
| PLT-09 | macOS'ta genel kısayol: uygulama odakta değilken kaydı başlatıp durduran tuşlar. | Screen Studio | yok: Windows dışında boş uygulama (App/Recorder/RecorderView.Kisayol.cs:31-34) | Mac'te kayıt başka uygulamadayken tuşla durdurulur. | orta | 3 | gerekmez | [17]; bkz. EKA-13 |
| PLT-10 | macOS'ta tıklama gösterimi: fare tıklamalarını kayıtta işaretlemek; ffmpeg'in `capture_mouse_clicks` seçeneği hazır yol. | CleanShot X, Screen Studio | yok: kutu görünür ama Windows dışında işlemez; `capture_mouse_clicks` kodda hiç geçmiyor (App/Recorder/RecorderView.Girdi.cs:14-20; App/Recorder/RecorderView.axaml:616) | Mac kaydında tıklanan yer görünür. | küçük | 2 | gerekmez | [1][16][17][45] |
| PLT-11 | macOS'ta tuş gösterimi: basılan tuşları kaydın üstünde göstermek. | CleanShot X, Screen Studio | yok: kutu görünür ama işlemez (App/Recorder/RecorderView.Girdi.cs:20; App/Recorder/RecorderView.axaml:624) | Mac'te eğitim kaydında izleyici basılan tuşu görür. | büyük | 2 | sahip: tuşları okumak için yeni izin istemi gerekebilir | [16][17] |
| PLT-12 | Linux'ta tıklama ve tuş gösterimi: tıklama halkasını ve basılan tuşları kayıtta göstermek. | doğrulanmadı | yok: kutular görünür ama işlemez (App/Recorder/RecorderView.Girdi.cs:14-26) | Linux'ta eğitim kaydında tıklama ve tuş görünür. | büyük | 3 | gerekmez | kaynak yok; bkz. EKB-14 |
| PLT-13 | macOS ve Linux'ta büyüteç: imleç çevresini kayıt sırasında büyütmek. | doğrulanmadı | yok: kutu görünür ama işlemez (App/Recorder/RecorderView.Girdi.cs:40; App/Recorder/RecorderMagnifier.cs:51) | Küçük yazılar kayıtta okunur olur. | büyük | 3 | gerekmez | kaynak yok; bkz. EKB-14 |
| PLT-14 | İşlemeyen kutuları gizleme: tıklama, tıklama sesi, tuş ve büyüteç kutuları macOS ve Linux'ta görünür ama etkisiz; kamera paneli gibi gizlenmeli. | rakip ölçütü yok; ölçüt kendi kamera panelimiz | yarım: yalnız kamera paneli gizleniyor; aynı dört kutu mini şeritte de var (App/Recorder/RecorderView.Kamera.cs:53; App/Recorder/RecorderView.axaml:616-631; App/Recorder/RecorderMini.axaml:79-91) | Kullanıcı işaretleyip sonuç alamadığı seçenek görmez. | küçük | 2 | gerekmez | kod |
| PLT-15 | Kaydedicide VideoToolbox: Mac'in donanım kodlayıcısını kaydedicinin kodlayıcı listesine eklemek. | OBS Studio | yok: liste NVENC, QSV, AMF ve yazılım; dönüştürücüdeki VideoToolbox kaydediciye bağlı değil (App/Recorder/RecorderView.Hedef.cs:23-29; Core/RecorderArguments.cs:368-374) | Mac'te kayıt işlemciyi yormadan kodlanır. | orta | 2 | gerekmez | [11] |
| PLT-16 | Kaydedicide VAAPI: Intel ve AMD ekran kartlarının Linux'taki donanım kodlayıcı arabirimini kaydediciye eklemek. | GPU Screen Recorder, Kooha (deneysel) | yarım: Linux'ta NVENC, QSV ve AMF sunulur, VAAPI listede yok (App/Recorder/RecorderView.Hedef.cs:23-29) | Intel ve AMD kartlı Linux makinede kayıt donanımla kodlanır. | orta | 2 | gerekmez | [14][15] |
| PLT-17 | Bölge düzenleyicisi: çizilen bölgeyi kayıttan önce ekranda tutup tutamaçla düzeltmek. | CleanShot X | yok: Windows dışında kapalı, bölge çizilir çizilmez sabitlenir (App/Recorder/RecorderView.Duzenleyici.cs:23-27) | Mac ve Linux'ta bölge kayda başlamadan düzeltilebilir. | orta | 3 | gerekmez | [16] |
| PLT-18 | macOS'ta denetim yüzeylerini kayıttan çıkarma: çerçeveyi ve mini şeridi yakalamanın dışında tutmak. | doğrulanmadı (Apple çatısı pencere dışlamayı belgeliyor) | yok: yalnız Windows'ta `SetWindowDisplayAffinity` ile (App/Recorder/CaptureAffinity.cs:20-23) | Kendi denetim şeridimiz Mac kaydında görünmez. | orta | 3 | sahip: PLT-03 yardımcısına bağlı | [6] |
| PLT-19 | Ekran yenileme hızını okuma: otomatik kare hızını ekranın gerçek hızına göre seçmek. | rakip ölçütü yok; ölçüt kendi Windows sürümümüz | yok: Windows dışında 0 döner, yedek kare hızına düşülür (App/Recorder/ScreenRefresh.cs:22) | Hızlı ekranlı Mac ya da Linux makinede otomatik kare hızı doğru seçilir. | küçük | 3 | gerekmez | kod |
| PLT-20 | Linux'ta kendini güncelleme: yeni sürümü uygulama içinden kurmak. | LosslessCut, Shotcut, Kdenlive, HandBrake (dördü de mağaza kanalıyla) | yok: Linux'ta yalnız haber verilir (Core/UpdateCheck.cs:146-147; docs/kurulum.md:175-176) | Linux kullanıcısı arşivi elle indirmeden yeni sürüme geçer. | orta | 2 | sahip: Linux'ta güncelleme bugün bilerek kapalı | [18][24][27][28] |
| PLT-21 | macOS'ta otomatik sessiz güncelleme: yeni sürümü arka planda indirip kapanışta kurmak; rakiplerin yolu Sparkle (macOS güncelleme çatısı). | OBS Studio, IINA, HandBrake | yarım: elle "Yükle" bütün paketi takas eder, otomatik yol Windows dışında kapalı (App/MainWindow.KapanistaGuncelleme.cs:85, 119; Core/UpdateCheck.cs:433) | Mac kullanıcısı düşünmeden güncel sürümde kalır. | orta | 2 | sahip: varsayılan davranış değişir | [30][31][32][33] |
| PLT-22 | macOS'ta grafik kurulum: sürükle bırak dmg (disk kalıbı) ile kurmak. | HandBrake, Shotcut, Kdenlive, LosslessCut | yok: kurulum uçbirim betiğiyle (install-vidshrink.sh:187-224; docs/kurulum.md:73-81) | Mac kullanıcısı uçbirim açmadan kurar. | orta | 2 | sahip: yeni ikili dağıtım ve imzalama gideri | [18][24][27][28] |
| PLT-23 | Linux'ta tek dosyalık paket: AppImage ya da Flatpak (bağımlılıklarını içinde taşıyan Linux paketleri) ile kurmak. | Shotcut, Kdenlive, HandBrake, LosslessCut ve 3 rakip daha | yok: arşiv ve uçbirim betiği var, paket yok (docs/kurulum.md:78-81; install-vidshrink.sh:187-224) | Linux kullanıcısı tek adımda kurar ve mağazadan güncellenir. | büyük | 2 | sahip: yeni dağıtım kanalı | [14][15][18][24][26][27][28] |
| PLT-24 | macOS kurucusunun ffmpeg'i getirmesi: Windows'taki gibi sabitlenmiş ve özeti doğrulanmış ffmpeg'i kurulumda indirmek. | HandBrake, Shotcut, Kdenlive, LosslessCut (tek dmg ile kurulur; içindeki ffmpeg ayrıca doğrulanmadı) | yok: betik `brew install ffmpeg` der ve durur (install-vidshrink.sh:232-262) | Mac'te kurulum tek adımda biter. | orta | 2 | sahip: yeni ikili indirme kaynağı seçilmeli | [18][24][27][28] |
| PLT-25 | Linux kurucusunun bağımlılığı kurması: ffmpeg ve libmpv eksikse paket komutunu kullanıcı onayıyla çalıştırmak. | rakip ölçütü yok; ölçüt kendi Windows kurucumuz | yok: betik komutu söyler ve durur (install-vidshrink.sh:246-276; docs/kurulum.md:83-90) | Linux'ta kurulum ikinci kez başlatılmadan biter. | küçük | 3 | gerekmez | kod |
| PLT-26 | macOS Finder sağ tık eylemi: Finder'da dosyaya sağ tıklayıp VidShrink ile küçültmek. | doğrulanmadı | yarım: paket belge türlerini bildirir, yani "Birlikte Aç" kaydı var; kendi eylemi yok, sağ tık paneli Windows dışında gizli (macos-app-bundle.sh:82-110; App/ShellMenu.cs:36; App/MainWindow.KabukMenusu.cs:27-28) | Mac'te dosya Finder'dan tek adımda gönderilir. | orta | 3 | gerekmez | kaynak yok; bkz. DAG-41 |
| PLT-27 | Linux dosya yöneticisi sağ tık eylemi: dosya yöneticisinde VidShrink eylemi sunmak. | doğrulanmadı | yarım: kurucu `.desktop` kaydı ve MIME yazar, eylem yok (install-vidshrink.sh:77-112; App/ShellMenu.cs:36) | Linux'ta dosya, dosya yöneticisinden tek adımda gönderilir. | küçük | 3 | gerekmez | kaynak yok; bkz. DAG-42 |
| PLT-28 | macOS'ta varsayılan oynatıcı yapma: uygulama içinden video türlerini VidShrink'e bağlamak. | doğrulanmadı | yarım: paket belge türlerini bildirir ama öneri şeridi ve "varsayılan yap" yalnız Windows'ta (macos-app-bundle.sh:82-110; App/Integration/DefaultApp.cs:16) | Mac'te çift tıklanan video VidShrink'te açılır. | orta | 3 | gerekmez | kaynak yok |
| PLT-29 | Linux'ta varsayılan oynatıcı yapma: uygulama içinden `xdg-mime` ile video türlerini VidShrink'e bağlamak. | doğrulanmadı | yarım: `.desktop` kaydı MIME türlerini taşır ama "varsayılan yap" yalnız Windows'ta (install-vidshrink.sh:100-110; App/Integration/DefaultApp.cs:16) | Linux'ta çift tıklanan video VidShrink'te açılır. | küçük | 3 | gerekmez | kaynak yok |
| PLT-30 | Hareketi azalt ayarı: sistemin "hareketi azalt" tercihini macOS ve Linux'ta da okumak. | doğrulanmadı | yok: yalnız Windows'ta okunuyor (App/MainWindow.axaml.cs:377-384; App/Playback/HoverZone.cs:260-266) | Canlandırmadan rahatsız olan kullanıcı her sistemde korunur. | küçük | 3 | gerekmez | kaynak yok; bkz. AYR-15 |
| PLT-31 | macOS'ta altyazı oturumunu saklama: OpenSubtitles oturumunu Keychain'de (macOS'un parola kasası) tutmak. | doğrulanmadı | yok: Windows dışında diske yazılmaz, her açılışta yeniden giriş gerekir (App/Subtitles/SessionStore.cs:19-21, 32) | Mac'te altyazı hesabına bir kez girilir. | orta | 3 | sahip: Keychain erişimi ad-hoc imzalı pakette yeni izin istemi çıkarabilir (doğrulanmadı) | kod |
| PLT-32 | Linux'ta altyazı oturumunu saklama: OpenSubtitles oturumunu Secret Service'te (Linux masaüstünün parola kasası) tutmak. | doğrulanmadı | yok: Windows dışında diske yazılmaz, her açılışta yeniden giriş gerekir (App/Subtitles/SessionStore.cs:19-21, 32) | Linux'ta altyazı hesabına bir kez girilir. | orta | 3 | sahip: D-Bus istemcisi bağımlılığı gerekebilir | kod |
| PLT-33 | Linux'ta dosyayı seçili gösterme: "klasörde göster" dosyayı seçili açsın. | rakip ölçütü yok; ölçüt kendi Windows ve macOS kolumuz | yarım: `xdg-open` yalnız klasörü açar (App/Platform.cs:9-26) | Linux'ta çıktı dosyası klasörde aranmaz. | küçük | 3 | gerekmez | kod |
| PLT-34 | macOS'ta kuyruk sonunda dosyayı seçili gösterme: kuyruk bitince açılan Finder penceresinde çıktı seçili gelsin. | rakip ölçütü yok; ölçüt kendi oynatıcı menümüz | yarım: oynatıcı menüsü `open -R` ile seçili gösterir, kuyruk sonu yalnız klasörü açar (App/QueueEndActions.cs:33-39; App/Platform.cs:9-26) | Mac'te kuyruk bitince çıktı hemen bulunur. | küçük | 3 | gerekmez | kod |
| PLT-35 | macOS'ta iş bitti bildirimi: kodlama bitince Bildirim Merkezi'ne haber düşürmek. | HandBrake | yok: Windows dışında sessiz yüz kullanılır (App/IsBittiBildirimi.cs:63-67) | Mac'te uzun iş bitince kullanıcı haber alır. | orta | 2 | sahip: bildirim izni istemi eklenir | [21] |
| PLT-36 | Linux'ta iş bitti bildirimi: kodlama bitince masaüstü bildirimi göndermek; yol Notification portalı. | HandBrake | yok: Windows dışında sessiz yüz kullanılır (App/IsBittiBildirimi.cs:63-67) | Linux'ta uzun iş bitince kullanıcı haber alır. | küçük | 2 | gerekmez | [4][21] |
| PLT-37 | macOS'ta VideoToolbox'ın otomatik sıraya girmesi: "hızlı" seçimde Mac'in donanım kodlayıcısını kendiliğinden kullanmak. | HandBrake, OBS Studio, Shutter Encoder (Apple donanımı, ad verilmeden) | yarım: yalnız kodek kilidi ya da yapıştırılan planla seçilir, kalite ölçeği yok (Core/CodecModel.cs:197-214; Core/PlanCalculator.cs:1669) | Mac'te hızlı kodlama elle kodek seçmeden gelir. | orta | 2 | sahip: ölçüm kapısından geçemedi (docs/olcumler/videotoolbox-hizli.md), varsayılanı değiştirir | [11][19][26] |
| PLT-38 | Linux'ta VAAPI ölçümü: VAAPI kolunu gerçek Linux makinede koşturup otomatik sıraya alınıp alınmayacağını ölçmek. | GPU Screen Recorder; HandBrake'te yok ama isteği 29 beğenili | yarım: kod var, yalnız açık seçimle ulaşılır ve bu depoda hiç koşmadı (Core/CodecModel.cs:209-214; Core/PlanCalculator.cs:1670) defterde | Intel ve AMD kartlı Linux'ta hızlı kodlama güvenle sunulur. | orta | 2 | sahip: ölçülecek Linux donanımı gerekir | [15][22][23] |
| PLT-39 | Yetim ffmpeg sürecini toplama: uygulama çökerse çocuk ffmpeg'i de sonlandırmak. | rakip ölçütü yok; ölçüt kendi Windows sürümümüz | yok: iş nesnesi yalnız Windows'ta, ötekinde hiçbir şey yapılmaz (Ffmpeg/ChildJob.cs:12, 28) | Mac ve Linux'ta çökme sonrası arkada işlemci yiyen süreç kalmaz. | küçük | 3 | gerekmez | kod |
| PLT-40 | win-arm64'te kaydedici donanım kodlayıcısı: Media Foundation (Windows'un kendi kodlayıcı arabirimi) kodlayıcılarını kaydedici listesine eklemek. | HandBrake (Windows ARM64 yapısı var; kodlayıcısı doğrulanmadı) | yarım: `h264_mf` ve `hevc_mf` planda izinli ama kaydedici listesi NVENC, QSV, AMF ve yazılım (Core/PlanCalculator.cs:1668; App/Recorder/RecorderView.Hedef.cs:23-29) | ARM işlemcili Windows'ta kayıt donanımla kodlanır. | orta | 3 | gerekmez | [18] |
| PLT-41 | linux-arm64'te donanım kodlayıcı: ARM kartlarının kodlayıcı arabirimini (V4L2 M2M) tanımak. | doğrulanmadı | yok: kodda `v4l2m2m` araması boş; izinli kodek listesinde yok (Core/PlanCalculator.cs:1668-1670) | ARM Linux'ta kodlama yalnız işlemciye kalmaz. | orta | 3 | sahip: ölçülecek ARM donanımı gerekir | kod |
| PLT-42 | linux-arm64 kurucu yedek dizini: libmpv aramasına `aarch64-linux-gnu` dizinini eklemek. | rakip ölçütü yok; ölçüt kendi x64 kolumuz | yarım: `ldconfig` bulunamazsa bakılan dizinlerde yalnız `x86_64-linux-gnu` var (install-vidshrink.sh:292) | ARM Linux'ta kurulu libmpv yanlışlıkla "yok" sayılmaz. | küçük | 3 | gerekmez | kod |
| PLT-43 | Linux'ta sistem sesi ölçümü: pulse'un `.monitor` kaynağıyla (hoparlöre giden sesin kopyası) kaydı gerçek Linux makinede koşturup doğrulamak. | Kooha | yarım: kaynak listede sistem sesi diye sınıflanır ama hiç ölçülmedi, README yalnız Windows diyor (Ffmpeg/CaptureDevices.cs:239-264; docs/olcumler/kaydedici-ses-seviyesi.md:53; README.md:254) | Linux'ta bilgisayarın sesi güvenle kayda girer. | küçük | 3 | sahip: ölçülecek Linux makinesi gerekir | [1][14] |
| PLT-44 | win-arm64'te Windows 11 sağ tık eklentisi: Windows 11'in ilk sağ tık menüsündeki girdiyi ARM64 kurulumuna da vermek. | doğrulanmadı | yarım: eklenti yalnız x64 derlenir, ARM64'te yalnız eski menü çalışır (.github/workflows/release.yml:275, 282, 406) | ARM işlemcili Windows 11'de sağ tıkın ilk menüsünde VidShrink görünür. | orta | 3 | gerekmez | kod; bkz. DAG-21 |

## Düzeltmeler

Hiçbir satırın önceliği değişmedi. Aşağıdakiler hücre düzeltmeleri ve öncelik 1 satırların kaynak denetimidir.

| No | Ne Değişti | Kanıt |
|---|---|---|
| `PLT-01` | Bizde Durum genişledi: kusur yalnız ekran hedefinde değil; bölge hedefi ekran numarasını 0 verir, pencere hedefi de aynı girdiden geçer. Öncelik 1 korundu, iki bağımsız kaynakta doğrulandı. | Kod: RecorderView.Hedef.cs:177; RecorderView.Secici.cs:79, 132. Kaynak [45] ffmpeg `avfoundation.m`: ekran listesi `ctx->num_video_devices + i` numarasıyla basılır, adla seçim `"Capture screen %d"` ile yapılır; sıra numarası kamera sayısından küçükse kamera açılır. Kaynak [1]: aygıt adla ya da sıra numarasıyla seçilir. Rakipler: [16] CleanShot X ve [17] Screen Studio ekran, pencere ve alan kaydını sunuyor. |
| `PLT-02` | Hücre değişmedi. Öncelik 1 üç kaynakta doğrulandı; Karar gerekçesi ölçüldü. | [10] OBS 27.0.0: Wayland desteği ve yeni PipeWire yakalama kaynağı. [14] Kooha: çalışmak için pipewire ve xdg-desktop-portal ister. [15] GPU Screen Recorder: X11 ve Wayland, `-w portal`. ffmpeg deposunun `libavdevice` dizin listesinde pipewire dosyası yok (`gh api`, 13.10.2026). |
| `PLT-03` | Hücre değişmedi. Öncelik 1 üç kaynakta doğrulandı. | [8] OBS: macOS 13 ve sonrasında sanal aygıtsız masaüstü sesi. [16] CleanShot X: bilgisayar sesini kaydetme. [17] Screen Studio: Mac'in sesini kaydetme. [45]: `avfoundation.m` içinde sistem sesi yakalayan kod yok. |
| `PLT-04` | Bizde Durum kanıtı sıkılaştırıldı: arama `git grep` ile izlenen dosyalarda (docs dışı) yinelendi, üç anahtar da boş. Ad-hoc imza (kimliksiz, yalnız o makinede geçen imza) satırı açıldı. | macos-app-bundle.sh:82-112 belge türleri dışında izin anahtarı taşımıyor; :118 `codesign --force --deep --sign -`. |
| `PLT-09` | Kimde Var `doğrulanmadı` iken `Screen Studio` oldu; Kaynak `[17]` eklendi. Tek rakip olduğu için öncelik 3 kaldı. | [17] Screen Studio: kaydı Mac'in her yerinden tek kısayolla başlatıp durdurma. |
| `PLT-10` | Bizde Durum'a seçeneğin kodda hiç geçmediği eklendi. | `git grep capture_mouse_clicks -- src` boş; [1] seçenek ffmpeg belgesinde var, varsayılanı kapalı. |
| `PLT-14` | Bizde Durum genişledi: aynı dört kutu mini şeritte de var; gizleme iki dosyada yapılmalı. | RecorderMini.axaml:79, 83, 87, 91; RecorderView.axaml:616, 620, 624, 628. |
| `PLT-37` | Hücre değişmedi. Karar gerekçesi ölçüm belgesinde açılıp okundu. | docs/olcumler/videotoolbox-hizli.md:41 "Sonuç — Kapıdan Kaldı"; :60 K2 ölçütü 2/8 ile kaldı; :62 K4 ölçütü 5/8 ile kaldı. |
| `PLT-43` | Bizde Durum'a README satırı eklendi. | README.md:254 "system audio on Windows"; kaydedici-ses-seviyesi.md:53 "Linux pulse ölçülmedi". |

## Çıkarılanlar

| No | Eksik | Neden | Kanıt (dosya:satır) |
|---|---|---|---|

Çıkarılan satır yok. 44 satırın hiçbiri kodda tam karşılanmış çıkmadı; platform kapılarının hepsi açılıp okundu.

## Yinelenenler

- PLT-02 = EKA-02
- PLT-03 = EKA-03
- PLT-05 = EKA-04
- PLT-06 = EKA-07 = EKB-18
- PLT-07 = EKA-07 = EKB-18
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
- PLT-23 = DAG-04 ve DAG-09
- PLT-24 = DAG-20
- PLT-25 = DAG-20
- PLT-26 = DAG-41
- PLT-27 = DAG-42
- PLT-30 = AYR-15
- PLT-35 = AYR-19 = SIK-23
- PLT-36 = AYR-19 = SIK-23
- PLT-44 = DAG-21

24 satır komşu kollarla ortak, 20 satır yalnız bu kolda: PLT-01, 04, 14, 17, 18, 19, 21, 28, 29, 31, 32, 33, 34, 37, 38, 39, 40, 41, 42, 43.

## Doğrulanamayanlar

- **Bütün tablo:** macOS ve Linux davranışı yalnız koddan okundu. Hiçbir satır o sistemlerde koşturulmadı; bu makinede koşturulamaz.
- **PLT-01:** Kameralı Mac'te `0:none` girdisinin kamerayı açtığı ffmpeg kaynağından ve bizim argüman satırımızdan çıkarıldı, gerçek kayıtla ölçülmedi. Çok ekranlı Mac'te bizim ekran sıramızla ffmpeg'in "Capture screen N" sırasının eşleşip eşleşmediği de bilinmiyor.
- **PLT-04:** Mikrofon açıklaması olmayan pakette macOS'un çocuk ffmpeg sürecine mikrofonu verip vermediği ölçülemedi. Vermiyorsa bu satır çekirdek eksiktir ve önceliği 1 olmalıdır; öncelik 2 bırakıldı.
- **PLT-10:** `capture_mouse_clicks` seçeneğinin çizdiği işaretin görünümü ve macOS 14 üstündeki davranışı görülmedi.
- **PLT-31 ve PLT-35:** Ad-hoc imzalı pakette Keychain'in ve bildirim izninin nasıl davrandığı ölçülemedi; Karar hücreleri araştırma kolunun yazdığı gibi `sahip` bırakıldı.
- **PLT-37:** Defterin 111. satırı VideoToolbox'ın gerçek donanımda koşmadığını söylüyor, ölçüm belgesi ise koştuğunu ve kapıdan kaldığını yazıyor. Defter satırı K12 ile eklenen açık seçim kolunu kastediyor olabilir; hangisini kastettiği çözülemedi, satıra `defterde` eklenmedi.
- **Rakip kanıtı olmayan satırlar:** Kimde Var hücresi `doğrulanmadı` olan 13 satır var: PLT-04, 12, 13, 18, 26, 27, 28, 29, 30, 31, 32, 41, 44. PLT-04 dışında hepsi öncelik 3; kaynak aranmadı.
- **Komşu paket planları:** EKA, EKB, DAG ve AYR paketlerinin yalnız başlıkları ve kapsadığı satırlar okundu; plan cümlelerinin tamamı okunmadı.

## Ek Notlar

- **Kodlayıcı listesi platforma göre süzülmüyor.** Kaydedici her sistemde aynı on bir adı gösterir (RecorderView.Hedef.cs:23-29, 53); Mac'te de NVENC, QSV ve AMF seçilebilir görünür. PLT-14 ile aynı tür kusur, tabloda satırı yok. EKA-P4 listeye dokunurken süzgeç de eklenmeli. Seçilince ne olduğu açılmadı.
- **Wayland'da ekran ve bölge hedefi uyarısız.** Wayland denetimi yalnız pencere hedefinde hata satırı veriyor (RecorderView.Secici.cs:45-48). EKA-P14 gelene dek aynı hata satırını ekran ve bölge hedefinde de göstermek küçük ve karar istemeyen bir iş; tabloda ayrı satırı yok.
- **Sanal ses aygıtı adları dar.** Sistem sesi sayılan adlar listesinde `blackhole` yok (CaptureDevices.cs:58-59); adında loopback ya da monitor geçmeyen sanal aygıt Mac'te mikrofon sayılır.
- **Kısayol ayar satırları.** RecorderView.Kisayol.cs içinde platforma bağlı görünürlük ataması aramada çıkmadı. Genel kısayol Windows dışında boş uygulamayla çalışırken ayar satırları görünüyorsa bu da PLT-14 ile aynı kusurdur; açılıp doğrulanmadı.
- **Linux ve macOS için güncelleme paketi üretilmiyor.** `vidshrink-update-<rid>.zip` yalnız başlatıcı dizini olan hedeflerde üretiliyor (release.yml:492-500); başlatıcıyı derleyen işin matrisi yalnız `win-x64` ve `win-arm64` görünüyor (release.yml:305-313; işin tamamı okunmadı). PLT-20 ve PLT-21 bugünkü uygulama arşiviyle ya da yeni bir paketle çalışmak zorunda.
- **Bayat belge.** docs/kurulum.md:73-76 ve :178-180 Linux'ta yalnız `linux-x64` yayımlandığını söylüyor; yayın matrisi ve kurucu `linux-arm64` de taşıyor (release.yml:347; install-vidshrink.sh:187, 216).
- **Paket en az macOS 14 istiyor** (macos-app-bundle.sh:78-79). ScreenCaptureKit ile sistem sesi macOS 13'ten beri var ([8]); PLT-03 yolunda eski sürüm yedeği gerekmez.
- **Komşu tablolarla çelişen hücreler.** Birleştirmede tek değer seçilmeli: PLT-05 öncelik 2, EKA-04 öncelik 1. PLT-23 öncelik 2, DAG-04 öncelik 1. PLT-30 öncelik 3, AYR-15 öncelik 2. PLT-20 öncelik 2 ve sahip, DAG-25 öncelik 3 ve gerekmez. PLT-06 sahip (Kamera izni), EKA-07 ve EKB-18 gerekmez. PLT-35 sahip (bildirim izni), AYR-19 gerekmez. PLT-08 sahip (D-Bus), EKA-13 gerekmez. PLT-10 küçük ve gerekmez, EKB-14 büyük ve sahip.
- **EKA-P9 izin istiyor.** Kamera paketi `gerekmez` diye paketlenmiş; macOS kolu Kamera izni istemi ve pakete kamera açıklaması ister. İzin anahtarları PLT-P4 içinde tek yerde toplanmalı.
- **Defter eşleşmeleri.** acik.md:15 yetim ffmpeg'i Windows'taki ddagrab yoklaması için yazıyor; PLT-39 ayrı bir eksik, `defterde` işaretlenmedi. acik.md:25 ve :50 Windows bildirim çağrılarına dair; PLT-35 ve PLT-36 `defterde` işaretlenmedi (AYR-19 işaretlemiş).
- **PLT-12 tek satırda iki iş taşıyor** (tıklama ve tuş). Yeni satır eklenemediği için bölünmedi.
- **Bu tabloda olmayan platform işleri.** macOS Now Playing (OYB-30) ve Linux MPRIS (OYB-31) oynatıcı kolunda, OYB-P9 ve OYB-P11 içinde duruyor.
- **K3 için ipucu.** ffmpeg deposunda `libavfilter/vsrc_gfxcapture.c` dosyası var (yalnız adı görüldü, içeriği açılmadı). Windows'ta pencere yakalama kararı yeniden açılırsa bakılmalı; bu not kararı önermez.

## Sekme Özeti

- Windows'ta kaydedici ve kabuk bütünleşmesi tam. macOS ve Linux'ta dönüştürme, küçültme ve oynatma çalışıyor; kaydedici ise yarım.
- Rakipler (OBS Studio, CleanShot X, Screen Studio, Kooha, GPU Screen Recorder) bizde eksik olan üç şeyi yerleşik veriyor: Wayland yakalama, Mac'te sürücüsüz sistem sesi, kamera katmanı.
- En ağır üç eksik: PLT-02 Wayland'da kaydedici hiç çalışmıyor; PLT-03 Mac'te sistem sesi yok; PLT-01 kameralı Mac'te ekran yerine kamera kaydediliyor olabilir.
- PLT-01 küçük iş ama ana işi kırıyor: bugün her dizüstü Mac'te kamera var. Gerçek Mac'te ölçülmedi.
- En ucuz üç kazanç: PLT-01 (tek fonksiyon), PLT-14 (dört kutuyu gizlemek), PLT-10 (ffmpeg'in hazır seçeneği). Üçü de karar istemiyor.
- Sahibin kararını bekleyen işler üç kümede toplanıyor: ölçüm için Mac ya da Linux makinesi (PLT-37, 38, 41, 43), macOS izin istemleri (PLT-04, 06, 11, 35), yeni dağıtım kanalı (PLT-22, 23, 24).
- 44 satırın 24'ü komşu kollarla ortak; bu kolun kendi paketi dokuz, kalanını komşu paketler kapsıyor.

## Paket Önerisi

Platforma göre: macOS paketleri PLT-P1, PLT-P3, PLT-P4, PLT-P5, PLT-P7; Linux paketleri PLT-P6, PLT-P8, PLT-P9; ikisine ortak PLT-P2. Sıra önerilen yapım sırasıdır.

### PLT-P1 — macOS Ekran Seçimi Adla
- Satırlar: PLT-01
- İş: küçük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Secici.cs, src/VidShrink.App/Recorder/RecorderView.Hedef.cs, tests/VidShrink.Tests
- Plan: `MacInput` ekranı sıra numarasıyla değil `Capture screen N:none` adıyla verir; ffmpeg kaynağı bu adı tanır ve N kamera sayısından bağımsızdır. Bölge ve pencere hedefi de aynı girdiden geçtiği için üç hedef birden düzelir. Argüman testleri ekran, bölge ve pencere hedefinde girdi metnini pimler. Kameralı ve çok ekranlı Mac'te gerçek kayıt bu makinede ölçülemez; belgeye ölçülmedi diye yazılır.

### PLT-P2 — İşlemeyen Kutuları Gizleme
- Satırlar: PLT-14
- İş: küçük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Recorder/RecorderMini.axaml, src/VidShrink.App/Recorder/RecorderView.Girdi.cs, tests/VidShrink.Tests
- Plan: Tıklama, tıklama sesi, tuş ve büyüteç kutuları kamera panelindeki gibi platform kapısına bağlanır ve Windows dışında gizlenir; hem ana görünümde hem mini şeritte. Kayıtlı ayarda açık gelen seçenek Windows dışında isteğe taşınmaz. Başsız arayüz testi, kapı kapalıyken dört kutunun da görünmediğini ölçer.

### PLT-P3 — macOS Tıklama İşareti
- Satırlar: PLT-10
- İş: küçük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Girdi.cs, src/VidShrink.App/Recorder/RecorderView.axaml, tests/VidShrink.Tests
- Plan: Mac'te tıklamaları göster kutusu ffmpeg'in `-capture_mouse_clicks 1` seçeneğine bağlanır; ek izin ve yardımcı ikili gerekmez. PLT-P1 ile aynı fonksiyona dokunur ve PLT-P2'nin gizlediği kutuyu yalnız Mac'te geri açar, bu yüzden ikisinden sonra gelir. Argüman testiyle doğrulanır; işaretin görünümü gerçek Mac'te görülmedi. EKB-P17 açılırsa bu paket onun macOS tıklama kolunu karşılamış olur.

### PLT-P4 — macOS İzin Akışı
- Satırlar: PLT-04
- İş: orta
- Karar: sahip: pakete izin açıklamaları eklenip kayıttan önce izin yoklaması gösterilsin mi
- Dokunacağı yer: macos-app-bundle.sh, src/VidShrink.App/Recorder/, src/VidShrink.App/Locales/
- Plan: Paketin Info.plist dosyasına (macOS paketinin kimlik ve izin bildirimi) mikrofon açıklaması eklenir; kamera paketi açılırsa kamera açıklaması da buraya gelir. Kayıt başlamadan Ekran Kaydı izni yoklanır, eksikse kayıt başlamaz ve Sistem Ayarları'nın ilgili sayfasına götüren hata satırı çıkar. EKA-P9, EKA-P12 ve EKA-P13'ün izin anahtarları da bu pakette toplanır. Paket içeriği testle, izin akışı yalnız gerçek Mac'te doğrulanır.

### PLT-P5 — macOS Kapanışta Sessiz Güncelleme
- Satırlar: PLT-21
- İş: orta
- Karar: sahip: macOS'ta otomatik güncelleme varsayılan olarak açılsın mı
- Dokunacağı yer: src/VidShrink.App/MainWindow.KapanistaGuncelleme.cs, src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.Core/UpdateHealth.cs
- Plan: Elle Yükle'nin kullandığı paket takası, `MacUpdate.CanSwap` doğruysa kapanış yoluna da bağlanır: sahne açılış bittikten sonra sessiz iner, çıkışta takas edilir. Her düşüş `UpdateHealth` sayacına yazılır; sayılmayan sessiz yol eklenmez. Düz kurulumda ve salt okunur yola taşınmış pakette kapalı kalır; Sparkle eklenmez. Birim testleri ve gerçek Mac'te bir sürüm atlamasıyla doğrulanır.

### PLT-P6 — Linux'ta Kendini Güncelleme
- Satırlar: PLT-20
- İş: orta
- Karar: sahip: Linux'ta bilerek kapalı tutulan uygulama içi güncelleme açılsın mı
- Dokunacağı yer: src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.Core/InPlaceUpdate.cs, src/VidShrink.App/YerindeGuncelleme.cs, .github/workflows/release.yml, docs/kurulum.md
- Plan: Kullanıcıya ait `~/.local/share/vidshrink` kurulumunda yerinde takas açılır; yazılamayan yolda kapalı kalır ve bugünkü gibi yalnız haber verilir. Linux için güncelleme paketi bugün üretilmiyor (release.yml:492-500); ya yayına Linux paketi eklenir ya da uygulama arşivi kullanılır. DAG-25 ile aynı iş, DAG paketlerinde yeri yok; DAG-P8 (Flatpak ya da AppImage) onaylanırsa güncellemeyi mağaza üstlenir ve bu paket yalnız betikle kurulanlara kalır. Takas, geri alma ve düşüş sayımı testle ölçülür.

### PLT-P7 — VideoToolbox Hızlı Kip Kapısı
- Satırlar: PLT-37
- İş: orta
- Karar: sahip: ölçüm için Mac ayrılsın mı ve kapıdan geçerse Hızlı kipin Mac'teki varsayılan kodlayıcısı değişsin mi
- Dokunacağı yer: src/VidShrink.Core/CodecModel.cs, src/VidShrink.Core/PlanCalculator.cs, tools/VidShrink.Bench, docs/olcumler/videotoolbox-hizli.md
- Plan: Önceki ölçüm beş ölçütün ikisinden kaldı (docs/olcumler/videotoolbox-hizli.md:60, 62); kapı değişmeden VideoToolbox'a kalite ölçeği eklenir ve ölçüm gerçek Mac'te yinelenir. Geçerse `hevc_videotoolbox` yalnız macOS'ta ve yalnız Hızlı kipte otomatik sıraya girer; geçmezse satır kapanır. Rapora giren her sayı Bench'ten (ölçüm aracımız) çıkar.

### PLT-P8 — Linux Ölçüm Turu: VAAPI Ve Sistem Sesi
- Satırlar: PLT-38, PLT-43
- İş: orta
- Karar: sahip: ölçüm için Intel ya da AMD ekran kartlı gerçek bir Linux makinesi ayrılsın mı
- Dokunacağı yer: tools/VidShrink.Bench, docs/olcumler/, src/VidShrink.Core/CodecModel.cs, src/VidShrink.Ffmpeg/CaptureDevices.cs, README.md
- Plan: Aynı Linux makinesinde iki ölçüm yapılır: VAAPI kolu açık seçimle koşturulup hız ve kalite Bench'le ölçülür; pulse `.monitor` kaynağından kısa kayıt alınıp ses seviyesi ölçülür. Sonuç, VAAPI'nin otomatik sıraya alınıp alınmayacağını ve README'deki "sistem sesi yalnız Windows'ta" cümlesini belirler. Defterdeki VAAPI satırı bu turla kapanır.

### PLT-P9 — Wayland'da Genel Kısayol
- Satırlar: PLT-08
- İş: orta
- Karar: sahip: GlobalShortcuts portalı için D-Bus istemcisi bağımlılığı eklensin mi
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderView.Kisayol.cs, src/VidShrink.App/Recorder/
- Plan: X11 kolu EKA-P10 içinde; bu paket yalnız Wayland kolunu kapsar. Kısayol GlobalShortcuts portalı üzerinden kaydedilir; portalla D-Bus (Linux masaüstünde programların birbirine mesaj yolladığı hat) üzerinden konuşulur. EKA-P14 gelmeden açılmaz, çünkü Wayland'da kaydedicinin kendisi çalışmıyor. Portalı sunan gerçek bir Wayland masaüstünde doğrulanır.

## Pakete Girmeyenler

Komşu kolun paketi kapsıyor:

- PLT-02: EKA-P14 kapsıyor.
- PLT-03: EKA-P12 kapsıyor.
- PLT-05: EKA-P13 kapsıyor.
- PLT-06: EKA-P9 kapsıyor (EKB-P13 aynı iş, yalnız biri açılır).
- PLT-07: EKA-P9 kapsıyor (EKB-P13 aynı iş, yalnız biri açılır).
- PLT-09: EKA-P10 kapsıyor.
- PLT-11: EKB-P17 kapsıyor.
- PLT-12: EKB-P17 kapsıyor.
- PLT-13: EKB-P17 kapsıyor.
- PLT-15: EKA-P4 kapsıyor.
- PLT-16: EKA-P4 kapsıyor.
- PLT-22: DAG-P10 kapsıyor.
- PLT-23: DAG-P8 kapsıyor.
- PLT-24: DAG-P9 kapsıyor.
- PLT-25: DAG-P9 kapsıyor.
- PLT-30: AYR-P4 kapsıyor.
- PLT-35: AYR-P5 kapsıyor.
- PLT-36: AYR-P5 kapsıyor.

Öncelik 3, hiçbir paketin yanında bedavaya gelmiyor:

- PLT-17: Tek rakipte var; pencere biçimi için sisteme özgü kod ister.
- PLT-18: EKA-P12'nin yardımcı ikilisi gelmeden yapılamaz.
- PLT-19: Ayrı dosya; EKA-P4 kare hızına dokunurken ucuzlar.
- PLT-26: DAG-41 ile aynı; DAG paketlerinde de yeri yok, rakip kanıtı yok.
- PLT-27: DAG-42 ile aynı; DAG paketlerinde de yeri yok, rakip kanıtı yok.
- PLT-28: Rakip kanıtı yok, dar kitle.
- PLT-29: Rakip kanıtı yok, dar kitle.
- PLT-31: Yeni izin istemi kararı ister, dar kitle.
- PLT-32: Yeni bağımlılık kararı ister, dar kitle.
- PLT-33: Küçük iş ama ayrı dosyalar (Platform.cs, QueueEndActions.cs); PLT-34 ile birlikte tek seferde yapılır.
- PLT-34: Küçük iş ama ayrı dosyalar (Platform.cs, QueueEndActions.cs); PLT-33 ile birlikte tek seferde yapılır.
- PLT-39: Ayrı dosya (Ffmpeg/ChildJob.cs), rakip ölçütü yok.
- PLT-40: EKA-P4 aynı kodlayıcı listesine dokunurken ucuzlar.
- PLT-41: Ölçülecek ARM donanımı gerekir.
- PLT-42: Tek satırlık iş; DAG-P9 kurucuya dokunurken eklenmeli.
- PLT-44: DAG-21 ile aynı; DAG-P3'ten sonra ucuzlar.
