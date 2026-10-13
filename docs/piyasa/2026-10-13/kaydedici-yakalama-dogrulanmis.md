# Kaydedici Sekmesi — Yakalama: Doğrulanmış Eksikler Tablosu

13 Ekim 2026. Kaynak numaraları `kaydedici-yakalama.md` dosyasındaki Kaynaklar bölümüne bakar.

Yol kısaltması: `Core/` = `src/VidShrink.Core/`, `App/` = `src/VidShrink.App/`, `Ffmpeg/` = `src/VidShrink.Ffmpeg/`. Satır numaraları `origin/main` 9de48d8b içindir. Doğrulama çalışma ağacında (eb0a5db0) yapıldı; aradaki tek commit yalnız `CHANGELOG.md` ve `Directory.Build.props` dosyalarına dokunuyor.

Bu turda kod yazılmadı, hiçbir şey derlenmedi ve çalıştırılmadı. Her satır kodda birkaç adla arandı, bulunan satır açılıp okundu. Öncelik 1 satırların rakip iddiası en az iki ayrı kaynakta yeniden görüldü.

| No | Eksik | Kimde Var | Bizde Durum | Fayda | İş | Öncelik | Karar | Kaynak |
|---|---|---|---|---|---|---|---|---|
| EKA-01 | Uygulama başına ses: yalnız seçilen uygulamanın sesini kaydetme, öbür sesleri dışarıda bırakma. | OBS, Bandicam, GPU Screen Recorder | yok | Bildirim ve müzik sesi kayda girmez, yalnız anlatılan uygulama duyulur. | orta | 1 | sahip: yeni bir Windows ses arabirimi (süreç geri döngüsü) ister ve eski Windows 10 yapılarında çalışmaz; orada ve Windows dışında seçenek gizlensin mi, kapalı mı görünsün seçilmeli | [3][4][12][26] |
| EKA-02 | Wayland'da yakalama: Linux'un yeni pencere sisteminde ekranı PipeWire portalı üzerinden kaydetme. | OBS, Kooha, GPU Screen Recorder, Cap | yarım: Linux'ta yalnız X11 yolu var; Wayland'da pencere seçici hata verir (App/Recorder/RecorderView.Secici.cs:45), ekran ve bölge kaydı uyarısız X11 yoluna gider (Core/RecorderArguments.cs:1469) | Wayland ile açılan Linux masaüstlerinde kaydedici çalışır. | büyük | 1 | sahip: ffmpeg'de PipeWire girişi yok, yeni yardımcı ikili ya da yeni bağımlılık gerekir | [1][9][22][24][26] |
| EKA-03 | macOS'ta sistem sesi: Mac'te bilgisayarın sesini kullanıcıya ayrı sürücü aratmadan kaydetme. | OBS, CleanShot X, macOS aracı | yarım: yalnız adı ipucu listesine uyan sanal ses aygıtı kuruluysa (Ffmpeg/CaptureDevices.cs:59) | Mac kullanıcısı ek sürücü kurmadan sesli kayıt alır. | büyük | 1 | sahip: ffmpeg'de ScreenCaptureKit girişi yok, yerel yardımcı ikili dağıtmak gerekir | [4][14][22][27] |
| EKA-04 | macOS'ta pencere yakalama: seçilen pencerenin kendisini kaynak olarak kaydetme. | OBS, CleanShot X, macOS aracı | yarım: pencere listelenir ama ekrandaki yeri kırpılarak kaydedilir (App/Recorder/RecorderWindows.cs:93, Core/RecorderArguments.cs:1134) | Pencere taşınınca ya da üstüne başka pencere gelince kayıt bozulmaz. | büyük | 1 | sahip: EKA-03 ile aynı yardımcı ikiliyi ister | [2][4][14][27] |
| EKA-05 | Oyun yakalama: oyunu ayrı bir kaynak olarak yakalama ve çalışan oyunu kendiliğinden bulma. | OBS, Bandicam, NVIDIA App | yarım: oyun ekrandaysa ekran kaydına girer; sihirbazdaki "Oyun" seçimi yalnız kare hızını ve kaliteyi ayarlar, oyuna özel yakalama yolu yok (Core/RecorderArguments.cs:24, Core/RecorderWizard.cs:5) | Tam ekran oyun, pencere ya da bölge seçmeden kaydedilir. | büyük | 2 | sahip: oyun sürecine kanca atmak hile koruması riski taşır ve ürün kapsamını genişletir | [2][8][12][15] |
| EKA-06 | Aygıt kaynağı: web kamerasını ya da yakalama kartını ana görüntü olarak kaydetme. | OBS, Bandicam, ScreenToGif, Screenity | yarım: kamera yalnız ekran kaydının üstünde katman olarak var (Core/RecorderArguments.cs:298) | Yalnız kamerayla konuşma videosu ya da konsol görüntüsü kaydedilir. | orta | 2 | gerekmez | [2][11][12][29][30] |
| EKA-07 | Kamera katmanı Windows dışında: macOS ve Linux'ta ekran kaydının üstüne kamera görüntüsü koyma. | OBS, Cap, CleanShot X, vokoscreenNG | yarım: yalnız Windows (App/Recorder/RecorderView.Kamera.cs:53) | Mac ve Linux kullanıcısı da kendini kayda ekler. | orta | 2 | gerekmez | [2][10][25][27] |
| EKA-08 | HDR yakalama: HDR ekranı 10 bit ve HDR bilgisiyle kaydetme. | macOS aracı, GPU Screen Recorder, NVIDIA App | yarım: motor 10 bit piksel biçimini ve bt2020 renk etiketini kabul ediyor (Core/RecorderArguments.cs:389, Core/RecorderArguments.cs:403); yakalama hep 8 bit bgra, HDR aktarım etiketi yok (Core/RecorderArguments.cs:1118) | HDR ekranda alınan kayıt, ekranda görünen parlaklık ve renkle saklanır. | büyük | 2 | sahip: HDR ekranda varsayılanın HDR dosya mı SDR'ye çevirme mi olacağı seçilmeli | [8][14][16][21][26] |
| EKA-09 | Bas-konuş: mikrofonu yalnız bir tuş basılıyken kayda alma. | OBS, NVIDIA App, Screenity | yok | Öksürük ve yan konuşma kayda girmez, kayıt durmadan mikrofon susar. | orta | 2 | gerekmez | [6][15][29] |
| EKA-10 | Zamanlanmış kayıt: belirlenen saatte kaydı başlatıp bitirme, günlük ya da haftalık yineleme. | Bandicam, Icecream Screen Recorder | yarım: süre ya da boyut sınırıyla kendiliğinden durma (Core/RecorderArguments.cs:1217) ve en çok 10 sn geri sayım (App/Recorder/RecorderSettings.cs:46) var; saatli başlatma ve yineleme yok | Yayın ya da toplantı, başında durmadan kaydedilir. | orta | 2 | gerekmez | [12][13][28] |
| EKA-11 | Düşük disk uyarısı: kayıt öncesinde ve sırasında boş alana bakma, azalınca uyarma. | Cap, OBS | yarım: boş alan denetimi yalnız Küçült'te ve komut satırında çağrılıyor (App/MainWindow.axaml.cs:4514) | Disk dolmadan önce haber gelir, kayıt yarım kalmaz. | küçük | 2 | gerekmez | [1][9] |
| EKA-12 | Kaydedicide donanım kodlayıcı Windows dışında: macOS'ta VideoToolbox, Linux'ta VAAPI ile kayıt. | OBS, GPU Screen Recorder, Kooha (deneysel) | yarım: kaydedici listesinde yalnız NVENC, QSV ve AMF var (Core/RecorderArguments.cs:368) | Mac ve Linux'ta kayıt işlemciyi yormadan alınır. | orta | 2 | gerekmez | [1][24][26] |
| EKA-13 | Genel kısayollar Windows dışında: macOS ve Linux'ta uygulama arkadayken kaydı tuşla başlatıp durdurma. | OBS, GNOME Ekran Kaydı | yarım: yalnız Windows (App/Recorder/RecorderView.Kisayol.cs:29) | Mac ve Linux'ta kaydı durdurmak için uygulama penceresine dönmek gerekmez. | orta | 2 | gerekmez | [1][35] |
| EKA-14 | Model tabanlı gürültü bastırma: mikrofon gürültüsünü kademeli ya da öğrenmiş bir modelle temizleme. | OBS, Cap | yarım: afftdn ve agate süzgeçleri var (Core/AudioCaptureArguments.cs:132) | Klavye ve fan sesi konuşmayı boğmadan temizlenir. | orta | 2 | sahip: model dosyası pakete girer | [1][9][11] |
| EKA-15 | Yalnız ses kaydı: görüntü olmadan mikrofonu ya da sistem sesini kaydetme. | Bandicam, ShareX | yok | Toplantı ya da anlatım sesi video dosyası üretmeden alınır. | küçük | 2 | gerekmez | [7][12] |
| EKA-16 | Komut satırından kayıt: kaydı betikten ya da başlatma parametresiyle başlatma. | OBS, ShareX, GPU Screen Recorder | yok | Kayıt betikle ya da sistemin görev zamanlayıcısıyla kendiliğinden başlar. | orta | 2 | gerekmez | [7][26][33] |
| EKA-17 | Windows'ta gerçek pencere yakalama: pencereyi WGC ile, üstü örtülse de kaydetme ve taşınınca izleme. | OBS, Bandicam | yarım: pencerenin ekrandaki yeri sabit bölge olarak kırpılır (Core/RecorderArguments.cs:1019) | Üste gelen pencere kayda girmez, pencere taşınınca görüntü kaymaz. | orta | 3 | kapalı: K3; pazar notu: ffmpeg 8.1 ile gelen gfxcapture süzgeci WGC'yi veriyor ve sarı kenarlık seçeneği varsayılan kapalı | [2][11][12][19][20][23][31] |
| EKA-18 | Tekrar tamponu süresi: son iki dakikadan uzun aralığı tutma. | GPU Screen Recorder | yarım: en çok 120 sn (Core/ReplayBuffer.cs:10) | Birkaç dakika önce olan an da kurtarılır. | küçük | 3 | gerekmez | [26] |
| EKA-19 | Tamponu açılışta başlatma: anlık tekrarı uygulama açılınca kendiliğinden çalıştırma. | OBS | yarım: tampon her oturumda elle başlatılır, paneli yalnız Gelişmiş kipte görünür (App/Recorder/RecorderView.Tampon.cs:88) | Tamponu açmayı unutan kullanıcı da anı kaçırmaz. | küçük | 3 | sahip: açılışta çalışan tampon uygulamayı sürekli kaydeden bir araca çevirir | [33] |
| EKA-20 | Yarım kalan kaydı kurtarma: çökme ya da kesintiden sonra kalan yakalama dosyasını bulup istenen biçime çevirmeyi önerme. | Cap | yarım: yakalama dosyası diskte kalır ama açılışta aranmaz (Core/RecorderArguments.cs:491) | Çöken kayıt kaybolmuş sanılmaz, istenen adla geri alınır. | küçük | 3 | gerekmez | [9] |
| EKA-21 | AV1 için QSV ve AMF kodlayıcıları: Intel ve AMD kartlarda AV1 ile kayıt. | OBS | yarım: AV1 donanım kodlayıcısı yalnız NVENC (Core/RecorderArguments.cs:368) | Intel ve AMD kart sahibi de küçük dosyalı AV1 kayıt alır. | küçük | 3 | gerekmez | [1] |
| EKA-22 | Ses kodeki seçimi: kayıtta AAC dışında Opus gibi bir ses kodeki seçme. | GPU Screen Recorder | yarım: ses kodeki AAC 160 kbit/sn olarak sabit (Core/RecorderArguments.cs:349) | Aynı ses kalitesi daha küçük dosyayla saklanır. | küçük | 3 | gerekmez | [26] |
| EKA-23 | Birden çok mikrofon: aynı kayıtta iki ya da daha çok ses girişini alma. | OBS, vokoscreenNG | yarım: en çok bir mikrofon ve bir sistem sesi (Core/AudioCaptureArguments.cs:36) | İki kişilik anlatım tek kayıtta ayrı mikrofonlarla alınır. | orta | 3 | gerekmez | [2][25] |
| EKA-24 | Sabit ya da değişken kare hızı seçimi: kaydın CFR mi VFR mi yazılacağını seçme. | GPU Screen Recorder | yok | Kurguya gidecek kayıt sabit kare hızıyla yazılır, kurgu programında ses kaymaz. | küçük | 3 | gerekmez | [21][26] |
| EKA-25 | Bütün ekranlar tek kayıtta: birden çok monitörü tek dosyaya kaydetme. | Bandicam, Kooha | yarım: tek kayıt tek ekran, iki ekrana yayılan bölge yavaş yola düşer (Core/RecorderArguments.cs:1039) | Çok monitörlü çalışma tek videoda görünür. | orta | 3 | gerekmez | [12][24] |
| EKA-26 | Aygıt kopunca toparlanma: mikrofon ya da kamera çıkıp takılınca seçili aygıta yeniden bağlanma. | Cap | yarım: sistem sesi çıkış aygıtı değişince yeni aygıt okunuyor, aygıt yoksa sessizlik yazılıyor (Ffmpeg/SystemAudioCapture.cs:11); mikrofon ve kamera için toparlanma yok | Kablo oynayınca ses ya da kamera kayıttan düşmez. | orta | 3 | gerekmez | [9] |
| EKA-27 | Kayıtta bildirimleri susturma: kayıt başlayınca sistemin Rahatsız Etmeyin kipini kendiliğinden açma. | CleanShot X | yok | Kayıt sırasında beliren bildirim görüntüye ve sese girmez. | orta | 3 | sahip: uygulama kullanıcının sistem bildirim ayarını değiştirir | [27] |
| EKA-28 | Ses sıkıştırıcı ve sınırlayıcı: mikrofon sesinin düzeyini dengeleyen ve patlamayı kesen süzgeçler. | OBS | yarım: kazanç, gürültü kapısı ve gürültü bastırma var (Core/AudioCaptureArguments.cs:75) | Fısıltı ile bağırış arasındaki fark azalır, ses patlamaz. | küçük | 3 | gerekmez | [32] |
| EKA-29 | Kayıt durum sesleri: kayıt durunca, duraklayınca ya da iptal olunca kısa bir ses çalma. | ShareX | yok | Ekrana bakmadan kaydın durduğu ya da duraklatıldığı anlaşılır. | küçük | 3 | gerekmez | [7] |

## Düzeltmeler

Araştırma kolu sekiz satır için "kural harfiyle okunursa sıra değişir" diyor. Ölçüt öyle okunmuyor: `1` kolu iki koşulu "ve" ile bağlıyor (en az üç yaygın rakip ve alanın ana işi), `2` kolu da "ana işin kenarında" olanı açıkça 2'ye koyuyor. Yani üç rakip tek başına 1 yapmaz. Sekiz satırın yedisinde ikinci koşul tutmuyor; birinde (EKA-04) tutuyor.

Bu bölümde değişmeyen ama ayrıca sorulan satırlar da var; onlarda "değişmedi" yazıyor.

| No | Ne Değişti | Kanıt |
|---|---|---|
| `EKA-01` | Karar `gerekmez` iken `sahip` oldu. Bizde Durum `yok` doğru çıktı. Öncelik 1 değişmedi; kaynakta doğrulandı. | Bugünkü yol aygıtın tamamını dinliyor: `Ffmpeg/WasapiLoopbackCapture.cs:31` (geri döngü bayrağı), `:91` (aygıt numaralayıcı). Kodda `ActivateAudioInterface` ve `PROCESS_LOOPBACK` araması boş. Tek programın sesi ayrı bir Windows arabirimi ister (süreç geri döngüsü: yalnız seçilen programın çaldığı sesi veren arabirim). Microsoft belgesi en düşük sürüm için "Windows 10 Build 20348" yazıyor: https://learn.microsoft.com/en-us/windows/win32/api/audioclientactivationparams/ns-audioclientactivationparams-audioclient_process_loopback_params ; OBS belgesi [3] Windows 10 2004 diyor. Ayrıca ses çalan programları listeleyen yeni bir arayüz parçası gerekir. Rakip üç ayrı kaynakta görüldü: [3] OBS "Application Audio Capture", [12] Bandicam tek pencereyi kendi sesiyle kaydediyor, [26] GPU Screen Recorder uygulama sesini listeliyor. |
| `EKA-02` | Bizde Durum genişledi: Wayland'da ekran ve bölge kaydı uyarısız X11 yoluna gidiyor. Öncelik 1 değişmedi; kaynakta doğrulandı. | `IsWayland` yalnız iki yerde soruluyor: `App/Recorder/RecorderView.Secici.cs:45` ve `App/Recorder/RecorderWindows.cs:83`. Linux girdisi hep `x11grab`: `Core/RecorderArguments.cs:1469`. Rakip: [26] (`-w portal`, PipeWire gerekir), [24] (PipeWire ve portal şart). FFmpeg ana dalının dosya ağacı `gh` ile okundu: adında `pipewire` geçen dosya `libavdevice` ve `libavfilter` içinde yok. |
| `EKA-03` | Değişmedi. Öncelik 1 kaynakta doğrulandı. | [4] OBS 30 ve macOS 13 ile ek sürücüsüz masaüstü sesi, [27] CleanShot X "Record Computer Audio", [14] Apple aracında sistem sesi seçeneği. FFmpeg ağacında adında `screencapturekit` geçen dosya yok. Bizde Windows dışında kaynak açılmıyor: `Ffmpeg/SystemAudioCapture.cs:36`. |
| `EKA-04` | Öncelik `2` iken `1` oldu. Bizde Durum'a pencerenin listelendiği eklendi. | Üç yaygın rakip üç ayrı kaynakta görüldü: [2] OBS, [27] CleanShot X, [14] macOS aracı. Pencere, kaydedicinin kendi sunduğu üç hedeften biri; macOS'ta listeleniyor (`App/Recorder/RecorderWindows.cs:81`) ama kayda ekran kırpması giriyor (`:93`, `Core/RecorderArguments.cs:1134`). Yani ana işe dokunuyor. "Yalnız macOS" gerekçesi EKA-03'ü 1'den düşürmemişti. Not: aynı kusurun Windows eşi K3 ile rafta; sahibi o kararı macOS'a da yayarsa satır 3'e iner. |
| `EKA-05` | Öncelik 2 değişmedi. Bizde Durum'a sihirbazdaki "Oyun" seçimi eklendi. | Üç rakip var ([2], [12], [15]) ama ikinci koşul tutmuyor: oyun bugün ekran kaydına giriyor, eksik olan ayrı bir yakalama yöntemi. Oy kanıtı zayıf: ShareX #848 2 Ağustos 2015'te açılmış (`gh` ile okundu), yani ShareX'e `ddagrab` (ffmpeg'in ekranı ekran kartından alan hızlı yakalama süzgeci) gelmeden önce. Sihirbaz: `Core/RecorderWizard.cs:5`; yalnız kare hızı ve kalite seçiyor (`:45`, `:49`). |
| `EKA-06` | Öncelik 2 değişmedi. | Üçten çok rakip var ama bu yeni bir kaynak türü; ekranı kaydetme işinin kendisi değil. Oy: Cap #504, 4 oy. Bizde kamera yalnız katman: `Core/RecorderArguments.cs:298`. |
| `EKA-07` | Öncelik 2 değişmedi. | Kamera katmanı ekran kaydının üstüne eklenen parça; ana işin kenarı. Kamera girdisi yalnız DirectShow ile kuruluyor: `Core/RecorderArguments.cs:1203`. Panel yalnız Windows'ta açılıyor: `App/Recorder/RecorderView.Kamera.cs:53`. Kardeş tablo aynı eksiğe de 2 vermiş. |
| `EKA-08` | Öncelik 2 değişmedi. Bizde Durum `yok` iken `yarım` oldu. | Yalnız HDR ekranı olanı ilgilendiriyor. Sağlam görülen rakip iki: [14] macOS aracı, [26] GPU Screen Recorder; NVIDIA tek okumada. Motor 10 bit biçimi ve bt2020 etiketini kabul ediyor (`Core/RecorderArguments.cs:389`, `:403`) ama hızlı yol kareyi hep 8 bit `bgra`ya indiriyor (`:1118`). Aynı dosyada `output_fmt`, `smpte2084` ve `color_trc` araması boş. |
| `EKA-09` | Öncelik 2 değişmedi. | Üç rakip sayılıyor ama OBS kanıtı bir hata kaydı (#10701); resmi belge bulunamamış. Mikrofon kolaylığı, ana işin kenarı. Bizde kısayol yalnız basışı bildiriyor (`App/Recorder/RecorderHotkeys.cs:184`); tuşun bırakılışını görebilecek klavye kancası başka iş için var (`App/Recorder/RecorderInput.cs:227`). |
| `EKA-10` | Bizde Durum `yok` iken `yarım` oldu. | Süre ve boyut sınırıyla durma var: `Core/RecorderArguments.cs:1217`, `:1220`. Geri sayım 0, 3, 5 ya da 10 sn: `App/Recorder/RecorderSettings.cs:46`. Saatli başlatma yok: `src/VidShrink.App/Recorder/` ve `src/VidShrink.Core/Recorder*.cs` içinde `Schedul`, `Zamanl`, `StartAt`, `Daily` ve `Weekly` araması boş. |
| `EKA-11` | Değişmedi; araştırma kolunun iddiası doğru. | `DiskSpaceGuard` yalnız iki yerde çağrılıyor: `App/MainWindow.axaml.cs:4514` (Küçült'ün başlat düğmesi, satır açılıp okundu) ve `src/VidShrink.Cli/CliApp.cs:519`. Kaydedici çağırmıyor. |
| `EKA-12` | Öncelik 2 değişmedi. Kimde Var'da Kooha'ya "deneysel" notu eklendi. | Kooha'da donanım kodlama deneysel ve varsayılan kapalı [24]; sağlam rakip iki (OBS, GPU Screen Recorder [26]). Kayıt işlemci kodlayıcısıyla zaten alınıyor; kazanç başarım. Liste: `Core/RecorderArguments.cs:368`. |
| `EKA-16` | Öncelik 2 değişmedi. | Üç rakip sayılıyor ama ShareX kanıtı 2013 tarihli, GPU Screen Recorder da zaten yalnız komut satırı aracı [26]. Betikle kayıt başlatan kitle dar; ana işin kenarı. `src/VidShrink.Cli/` içinde `Recorder`, `ddagrab`, `gdigrab`, `x11grab` ve `avfoundation` araması boş. |
| `EKA-17` | Karar hücresine pazar notu eklendi. `kapalı: K3` ve öncelik 3 değişmedi. | WGC (Windows Graphics Capture; tek pencereyi üstü örtülse de yakalayan Windows arabirimi) artık ffmpeg'de: değişiklik günlüğü `gfxcapture` süzgecini 8.1 başlığında veriyor [19], kaynak dosyada `display_border` seçeneğinin varsayılanı kapalı [20]. Kurucunun pinlediği ffmpeg 9.0: `Install-VidShrink.ps1:255`. K3'ün gerekçesi sarı kenarlıktı: `docs/piyasa/tarama-2026-10-05.md:258`. Pencere hedefinin bugün kırpma olduğu doğru: `Core/RecorderArguments.cs:1019`. |
| `EKA-20` | Değişmedi; araştırma kolunun iddiası doğru. | Yakalama dosyasının yolu `Core/RecorderArguments.cs:491`; tek kullanan `Ffmpeg/RecorderSession.cs:219`. Açılışta bu adı arayan kod yok. |
| `EKA-23` | Öncelik 3 değişmedi. | İki rakipte var ama ölçütün "dar kitle" kolu geçerli. Bizde seçim en çok bir mikrofon ve bir sistem sesi: `Core/AudioCaptureArguments.cs:36`. |
| `EKA-25` | Öncelik 3 değişmedi. | İki rakipten biri deneysel: Kooha'da çok ekran deneysel [24]. Dar kitle. |
| `EKA-26` | Bizde Durum `yok` iken `yarım` oldu. | Sistem sesinde çıkış aygıtı değişince yeni aygıt okunuyor: `Ffmpeg/SystemAudioCapture.cs:11`, `Ffmpeg/WasapiLoopbackCapture.cs:171`. Mikrofon ve kamera için `src/` içinde `DeviceLost`, `Reconnect`, `DeviceRemoved` ve `DeviceArrival` araması boş. |

## Çıkarılanlar

Çıkarılan satır yok. 29 satırın hiçbiri kodda tam olarak bulunmadı.

| No | Eksik | Neden | Kanıt (dosya:satır) |
|---|---|---|---|

## Yinelenenler

Kardeş tablo `kaydedici-sunum-eksikler.md` 13 Ekim 2026 saat 18:13'te yeniden okundu; iki satır da yerinde. Kardeş kolun doğrulanmış dosyası (`kaydedici-sunum-dogrulanmis.md`) aynı iki çifti yazıyor.

- EKA-07 = EKB-18 — Kamera katmanı macOS ve Linux'ta. İki tabloda da öncelik 2 ve karar `gerekmez`. Kardeş kol bunu `EKB-P13` paketine tek başına koymuş; burada `EKA-P9`. Yalnız biri açılır.
- EKA-27 = EKB-20 — Kayıtta bildirimleri susturma. Bu tabloda tek rakip ve öncelik 3; kardeş tabloda iki rakip (CleanShot X, Zight) ve öncelik 2. Kardeş kol `EKB-P15` paketine koymuş; burada pakete girmedi.

Aynı eksik değil ama yakın olan iki çift var; yineleme sayılmadı.

- EKA-13 (genel kısayollar Windows dışında) ile EKB-14 (girdi efektleri macOS ve Linux'ta): ikisi de Windows dışında genel tuş dinlemeye dayanıyor.
- EKA-06 (kamerayı ana görüntü olarak kaydetme) ile EKB-32 (kayıt içinde kamera ve ekran yerleşimi): ikisi de kamerayı tam kareye taşıyor, ama biri kaynak türü, öbürü düzen.

## Doğrulanamayanlar

- EKA-01: En düşük Windows sürümünde iki kaynak ayrışıyor. Microsoft belgesi 20348 yapısını, OBS belgesi 2004 sürümünü yazıyor. Hangisinin geçerli olduğu ölçülmedi.
- EKA-02: Wayland'da bugünkü X11 yoluyla alınan ekran kaydının ne verdiği ölçülmedi (siyah kare mi, yalnız eski tür pencereler mi).
- EKA-05: Tam ekran bir oyunun `ddagrab` ile kaydı ölçülmedi; `docs/olcumler/kaydedici-ddagrab.md` durağan masaüstünü ölçtü. Ölçüm kusur gösterirse satır öncelik 1'e çıkar.
- EKA-08: HDR ekranda bugünkü kaydın nasıl göründüğü ölçülmedi. NVIDIA App'in HDR kaydı tek okumada.
- EKA-09: OBS'te bas-konuş için resmi belge bu turda da görülmedi; kanıt hata kaydı.
- EKA-11: OBS kanıtı dolaylı; sağlam görülen rakip yalnız Cap. Kaydın kesilmemesi ana işe dokunduğu için satır 2'de bırakıldı.
- EKA-14: Cap "Studio Sound"un model tabanlı olduğu doğrulanmadı. Pinli ffmpeg derlemesinde `arnndn` süzgecinin açık olup olmadığı ölçülmedi.
- EKA-15 ve EKA-16: ShareX kanıtları 2013 ve 2014 girdileri; bugünkü sürümde sürdükleri doğrulanmadı.
- EKA-17: `gfxcapture` süzgecinin pinli ffmpeg derlemesinde açık olup olmadığı ölçülmedi (program çalıştırmak bu turda yasak). Kenarlığın bizim gibi paketsiz bir uygulamada kapanıp kapanmadığı da doğrulanmadı [23].
- EKA-18: GPU Screen Recorder sayfası tampon için üst sınır yazmıyor; "iki dakikadan uzun" iddiası süre serbest olduğu için geçerli sayıldı.
- EKA-24: Hızlı yolda çıktının gerçekten sabit kare hızlı olup olmadığı ölçülmedi. `ddagrab` durağan ekranda kareyi çoğaltıyor (`docs/olcumler/kaydedici-ddagrab.md:22`).
- EKA-27: Kardeş tablo ikinci rakip olarak Zight'ı sayıyor; kardeş kol o sayfayı açamadığını yazmış. Bu tablodaki öncelik 3, tek rakibe göre verildi.

## Ek Notlar

- Wayland'da ekran ve bölge kaydı için hiç uyarı yok; hata yalnız pencere seçicide çıkıyor (`App/Recorder/RecorderView.Secici.cs:45`). Karar beklemeyen küçük bir uyarı işi; tabloda ayrı satırı yok.
- Kurucunun pinlediği ffmpeg 9.0 (`Install-VidShrink.ps1:255`), `gfxcapture` 8.1'de geldi. K3 yeniden açılırsa yeni bağımlılık gerekmiyor.
- Anlık tekrar kayıt sürerken başlamıyor (`App/Recorder/RecorderView.Tampon.cs:102`). OBS'te ikisinin birlikte çalıştığı doğrulanmadığı için tabloya girmemiş.
- Kaydedicinin kapları MP4, MKV, MOV ve GIF (`Core/RecorderArguments.cs:46`). WebP ve APNG yok; ShareX iddiası doğrulanmadığı için tabloya girmemiş.
- VideoToolbox ve VAAPI dönüştürme yolunda var (`src/VidShrink.Core/FfmpegArguments.cs`, `src/VidShrink.Core/CodecModel.cs`); kaydedici listesine girmemiş. Defter bu ikisinin gerçek donanımda koşmadığını yazıyor (`.claude/acik.md:111`).
- EKA-P7 mikrofonu uygulama tarafında yakalamaya taşırsa EKA-26'nın mikrofon yarısı aynı yoldan gelir; sistem sesinde bu yol zaten aygıt değişimini izliyor.
- Defter: tablodaki 29 eksikten hiçbiri `.claude/acik.md` içinde yazılı değil; bu yüzden hiçbir satıra `defterde` eklenmedi.
- Okunan sayfalarda ve dosyalarda ajana yönelik yazılmış bir yönerge görülmedi.

## Sekme Özeti

- Windows'ta çekirdek iş yerinde: ekran ve bölge kaydı, sistem sesi, ayrı ses izleri, anlık tekrar, çökmeye dayanıklı kap ve genel kısayollar var. Açık, çekirdeğin çevresinde ve Windows dışında toplanıyor.
- 29 satırın 29'u kaldı. Öncelik 1 dört satır, öncelik 2 on iki satır, öncelik 3 on üç satır.
- En ağır üç eksik: Wayland'da yakalama (EKA-02), macOS'ta sistem sesi ve pencere (EKA-03, EKA-04), uygulama başına ses (EKA-01). Dördü de sahibin kararını bekliyor; ilk ikisi yeni yardımcı ikili ister.
- En ucuz üç kazanç: düşük disk uyarısı (EKA-11), yarım kalan kaydı kurtarma (EKA-20), yalnız ses kaydı (EKA-15). Üçü de karar beklemiyor.
- On altı paketin dokuzu hemen açılabilir (P1, P3, P4, P5, P6, P7, P8, P9, P10); yedisi sahibin tek cümlelik kararına bağlı (P2, P11, P12, P13, P14, P15, P16).
- macOS ve Linux işleri bu Windows makinesinde koşulamıyor; o paketlerin ölçüsü CI koşucusuna ve gerçek aygıtta elle denemeye kalıyor.
- K3 için pazar değişti: WGC artık ffmpeg'in içinde ve dağıttığımız sürüm onu taşıyacak kadar yeni. Kararı yeniden açmak sahibin işi.

## Paket Önerisi

Sıra önerilen yapım sırasıdır: önce küçük, eşit boyda yüksek öncelikli olan. Sahibin kararını bekleyen paket, beklemeyeni durdurmaz.

Paket sayısı beklenen on ikiyi geçiyor. Önceliği 1 ya da 2 olan on altı satır var ve iki `orta` satırı birleştiren her paket `büyük` oluyor; bölünebilen büyük paket bölündü. Öncelik 3 satırlardan beşi, aynı dosyaya dokunduğu paketin yanına eklendi.

### EKA-P1 — Disk Uyarısı Ve Kayıt Kurtarma
- Satırlar: EKA-11, EKA-20
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Ffmpeg/DiskSpaceGuard.cs, src/VidShrink.Ffmpeg/RecorderSession.cs, src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Yakalama.cs, src/VidShrink.App/Locales/
- Plan: Kayıt başlamadan önce ve kayıt sürerken belirli aralıkla `DiskSpaceGuard` çağrılır; boş alan eşiğin altına inince durum satırında uyarı çıkar. Kaydedici açılırken çıktı klasöründe sahipsiz `.yakalama.mkv` dosyası aranır ve var olan Matroska'dan aktarma yoluyla (`RecorderSession.cs:213`) istenen biçime çevirmek önerilir. İki davranış da sahte disk ve sahte klasörle ölçülür; `tests/VidShrink.Tests/DiskSpaceGuardTests.cs` örnek alınır.

### EKA-P2 — Uygulama Başına Ses
- Satırlar: EKA-01
- İş: orta
- Karar: sahip: Uygulama başına ses yeni bir Windows ses arabirimi ister ve eski Windows 10 yapılarında çalışmaz; orada ve Windows dışında seçenek gizlensin mi, kapalı mı görünsün.
- Dokunacağı yer: src/VidShrink.Ffmpeg/WasapiLoopbackCapture.cs, src/VidShrink.Ffmpeg/SystemAudioCapture.cs, src/VidShrink.Ffmpeg/LoopbackFeed.cs, src/VidShrink.App/Recorder/RecorderView.Ses.cs
- Plan: Bugünkü sistem sesi kaynağının yanına süreç geri döngüsü kaynağı eklenir: `ActivateAudioInterfaceAsync` çağrısı `AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS` ile seçilen programın sesini verir, ham ses aynı adlandırılmış borudan ffmpeg'e gider. Ses panelinde ses çalan programların listesi çıkar; Windows sürümü yetmiyorsa seçenek sahibin kararına göre gizlenir ya da kapalı görünür. Ölçü `ISystemAudioCapture` (`SystemAudioCapture.cs:24`) için sahte kaynakla yazılır; gerçek deneme iki program aynı anda ses çalarken elle yapılır.

### EKA-P3 — Yalnız Ses Ve Ses Seçenekleri
- Satırlar: EKA-15, EKA-22, EKA-28
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Core/AudioCaptureArguments.cs, src/VidShrink.App/Recorder/RecorderView.Ses.cs, src/VidShrink.App/Recorder/RecorderView.Gelismis.cs
- Plan: Hedef listesine "yalnız ses" eklenir; görüntü girdisi kurulmaz ve ses girdilerinin sıra numarası varsayımı (`RecorderArguments.cs:366`) buna göre parametreye çevrilir. Ses kodeki sabiti (`RecorderArguments.cs:349`) seçilebilir olur; varsayılan AAC kalır, Opus yalnız onu taşıyan kaplarda sunulur. Ses süzgeç zincirine (`AudioCaptureArguments.cs:75`) ffmpeg'in `acompressor` (ses düzeyini dengeleyen) ve `alimiter` (patlamayı kesen) süzgeçleri iki yeni seçenek olarak girer. Üçü de argüman ölçüleriyle doğrulanır.

### EKA-P4 — Kodlayıcı Listesi Ve Kare Hızı
- Satırlar: EKA-12, EKA-21, EKA-24
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Core/CodecModel.cs, src/VidShrink.App/Recorder/RecorderView.Gelismis.cs
- Plan: Kaydedicinin kodlayıcı listesine (`RecorderArguments.cs:368`) VideoToolbox, VAAPI, `av1_qsv` ve `av1_amf` eklenir; VideoToolbox ve VAAPI argümanları dönüştürme yolundan (`FfmpegArguments.cs`, K12) alınır. Aynı yere sabit ya da değişken kare hızı seçimi ffmpeg'in `-fps_mode` seçeneğiyle eklenir; varsayılan bugünkü davranış kalır. Argümanlar ölçüyle doğrulanır; macOS ve Linux kodlayıcıları bu makinede koşulamadığı için gerçek deneme o sistemlerde elle yapılır.

### EKA-P5 — Komut Satırından Kayıt
- Satırlar: EKA-16
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Cli/CliApp.cs, src/VidShrink.App/Program.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.Ffmpeg/RecorderSession.cs
- Plan: Kayıtlı kaydedici ayarlarından arayüz açmadan kayıt isteği kuran tek bir giriş yazılır: başlat, süre, çıktı yolu. Giriş uygulamanın başlatma parametresine ya da komut satırı aracına bağlanır; hangisinin daha az kod taşıdığına işin başında bakılır. Kayıt süre sınırı dolunca (`RecorderArguments.cs:1217`) ya da durdurma işaretiyle biter. Ölçü: komut satırı argümanlarının kayıt isteğine çevrilmesi `tests/VidShrink.Tests` içinde sınanır.

### EKA-P6 — Saatli Kayıt
- Satırlar: EKA-10
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.GeriSayim.cs, src/VidShrink.App/Recorder/RecorderView.Yakalama.cs, src/VidShrink.App/Locales/
- Plan: Kaydediciye başlangıç saati ve yineleme (bir kez, günlük, haftalık) seçimi eklenir; uygulama açıkken saat gelince kayıt bugünkü başlatma yolundan başlar, bitiş için var olan süre sınırı (`RecorderArguments.cs:1217`) kullanılır. Uygulama kapalıyken başlatma EKA-P5'in girişine ve sistemin görev zamanlayıcısına bırakılır; bu yüzden EKA-P5'ten sonra yapılır. Ölçü: sahte saatle zamanlama testleri.

### EKA-P7 — Bas Konuş
- Satırlar: EKA-09
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderHotkeys.cs, src/VidShrink.App/Recorder/RecorderView.Kisayol.cs, src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.Core/AudioCaptureArguments.cs, src/VidShrink.Ffmpeg/RecorderSession.cs
- Plan: Kısayol arabirimi tuşun basılışını ve bırakılışını ayrı bildirecek biçimde genişletilir; bugünkü kayıt yalnız basışı veriyor (`RecorderHotkeys.cs:184`), bırakılışı görebilen klavye kancası başka iş için var (`RecorderInput.cs:227`). Tuş bırakılınca mikrofon izine sessizlik yazılır. En sağlam yol mikrofonu sistem sesi gibi uygulama tarafında yakalamaktır; çalışan ffmpeg'e süzgeç komutu göndermek ikinci seçenektir ve önce ölçülmelidir. Ölçü `tests/VidShrink.Tests/KaydediciKisayolAtamaTests.cs` yanına yazılır; gerçek deneme Windows'ta elle yapılır.

### EKA-P8 — Kamera Ana Kaynak Olarak
- Satırlar: EKA-06
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Ffmpeg/CaptureDevices.cs, src/VidShrink.App/Recorder/RecorderView.Hedef.cs, src/VidShrink.App/Recorder/RecorderView.Kamera.cs
- Plan: Hedef türlerine (`RecorderArguments.cs:24`) "aygıt" eklenir: web kamerası ya da yakalama kartı ekran girdisinin yerine ana görüntü olur. Windows'ta kamera girdisi zaten DirectShow ile kuruluyor (`RecorderArguments.cs:1203`); aynı kurucu ana girdi için yeniden kullanılır. Ölçü `tests/VidShrink.Tests/KaydediciHedefTests.cs` ve `KaydediciKameraTests.cs` yanına yazılır; gerçek deneme takılı bir kamerayla Windows'ta elle yapılır.

### EKA-P9 — Kamera Katmanı Windows Dışında
- Satırlar: EKA-07
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Ffmpeg/CaptureDevices.cs, src/VidShrink.App/Recorder/RecorderView.Kamera.cs
- Plan: Kamera girdisi macOS'ta `avfoundation`, Linux'ta `v4l2` ile kurulur (ffmpeg'in o sistemlerdeki aygıt girdileri) ve kamera paneli yalnız Windows koşulundan çıkar (`RecorderView.Kamera.cs:53`). Kardeş tablodaki EKB-18 ve onun paketi EKB-P13 aynı iştir; yalnız biri açılır. Argüman üretimi `tests/VidShrink.Tests/KaydediciKameraTests.cs` yanında her sistem için ölçülür; gerçek aygıt denemesi bu makinede yapılamaz, o sistemlerde elle yapılır.

### EKA-P10 — Genel Kısayollar Windows Dışında
- Satırlar: EKA-13
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderHotkeys.cs, src/VidShrink.App/Recorder/RecorderView.Kisayol.cs
- Plan: Windows dışında bugün boş kısayol sürümü seçiliyor (`RecorderView.Kisayol.cs:29`, `RecorderHotkeys.cs:136`); macOS ve X11 karşılıkları yazılır: macOS'ta izin istemeyen sistem kısayol kaydı, X11'de tuş yakalama çağrısı. Wayland bu paketin dışında kalır; orada genel kısayol portal ister ve EKA-P14 ile birlikte düşünülür. Atama mantığı `tests/VidShrink.Tests/KaydediciKisayolAtamaTests.cs` içinde ölçülür; gerçek deneme o sistemlerde elle yapılır.

### EKA-P11 — Model Tabanlı Gürültü Bastırma
- Satırlar: EKA-14
- İş: orta
- Karar: sahip: Gürültü bastırma modeli (RNNoise dosyası) kurulum paketine girsin mi.
- Dokunacağı yer: src/VidShrink.Core/AudioCaptureArguments.cs, src/VidShrink.App/Recorder/RecorderView.Ses.cs, Install-VidShrink.ps1 ve öbür kurucular
- Plan: ffmpeg'in `arnndn` süzgeci (konuşmayı gürültüden ayırmak için eğitilmiş küçük bir modelle çalışan süzgeç) bugünkü `afftdn` seçeneğinin yanına üçüncü kademe olarak eklenir; model dosyası kurulumla gelir. Önce pinli ffmpeg derlemesinde süzgecin açık olduğu ve model dosyasının lisansı doğrulanır. Ölçü: argüman testi ve gürültülü örnek kayıtta önce ile sonra dinleme.

### EKA-P12 — Mac Sistem Sesi Yardımcısı
- Satırlar: EKA-03
- İş: büyük
- Karar: sahip: macOS için ScreenCaptureKit kullanan yerel bir yardımcı ikili yazılıp pakete eklensin mi.
- Dokunacağı yer: tools/ altında yeni yardımcı proje, src/VidShrink.Ffmpeg/SystemAudioCapture.cs, src/VidShrink.Ffmpeg/CaptureDevices.cs, src/VidShrink.Core/LoopbackAudio.cs, macOS kurucusu
- Plan: ScreenCaptureKit (macOS'un ekranı, tek pencereyi ve sistem sesini veren yakalama arabirimi) ffmpeg'de olmadığı için küçük bir yardımcı ikili yazılır; ham sesi verir, uygulama onu Windows'taki düzenle ffmpeg'e aktarır (`SystemAudioCapture.cs:36` bugün Windows dışında kaynak açmıyor). Sanal ses aygıtı yolu (`CaptureDevices.cs:59`) yedek olarak kalır. Bu makinede koşulamaz: derleme CI'ın macOS koşucusunda, ses denemesi gerçek Mac'te elle yapılır.

### EKA-P13 — Mac Pencere Yakalama
- Satırlar: EKA-04
- İş: büyük
- Karar: sahip: macOS'ta pencere yakalama EKA-P12'deki yardımcı ikiliye bağlansın mı; o karar hayır çıkarsa bu paket düşer.
- Dokunacağı yer: tools/ altındaki yardımcı proje, src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderWindows.cs, src/VidShrink.App/Recorder/RecorderWindowsMac.cs
- Plan: EKA-P12'nin yardımcısı seçilen pencerenin karelerini de verir ve ffmpeg onları ham görüntü girdisi olarak okur; ekran kırpması (`RecorderWindows.cs:93`, `RecorderArguments.cs:1134`) yerini bu kaynağa bırakır. Argüman üretimi `tests/VidShrink.Tests/KaydediciPencereTests.cs` yanında ölçülür. Pencere taşınınca ve üstü örtülünce kaydın bozulmadığı gerçek Mac'te elle denenir; EKA-P12 bitmeden başlamaz.

### EKA-P14 — Wayland Yakalama Yolu
- Satırlar: EKA-02
- İş: büyük
- Karar: sahip: Wayland için PipeWire portalını konuşan bir yardımcı ikili ya da yeni bağımlılık eklensin mi, yoksa yalnız açık bir uyarıyla yetinilsin mi.
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderWindowsX11.cs, src/VidShrink.App/Recorder/RecorderView.Secici.cs, Linux kurucusu
- Plan: Wayland oturumunda (`RecorderWindowsX11.cs:22` bunu zaten anlıyor) ekran, kullanıcının onay verdiği portal üzerinden istenir ve gelen PipeWire akışı bir yardımcıyla ffmpeg'e boru olarak verilir; ffmpeg'in kendi PipeWire girişi yok. Karar "yalnız uyarı" çıkarsa iş küçülür: ekran ve bölge kaydı için de pencere seçicideki gibi açık hata gösterilir. Deneme Wayland ile açılan gerçek bir Linux masaüstünde elle yapılır.

### EKA-P15 — HDR Yakalama
- Satırlar: EKA-08
- İş: büyük
- Karar: sahip: HDR ekranda varsayılan HDR dosya mı olsun, yoksa SDR'ye çevrilmiş kayıt mı.
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/DdaOutputs.cs, src/VidShrink.App/Recorder/RecorderView.Gelismis.cs, docs/olcumler/
- Plan: İlk adım ölçümdür: HDR ekranda bugünkü kaydın nasıl göründüğü `docs/olcumler/` altında yok. Sonra hızlı yolda `ddagrab` süzgecinden 10 bit çıkış istenir (bugün kare hep 8 bit `bgra`ya iniyor, `RecorderArguments.cs:1118`), 10 bit kodlama ve HDR renk etiketleri yazılır. HDR olmayan ekranda davranış değişmez. Ölçü: argüman testi (`tests/VidShrink.Tests/KaydediciDdagrabTests.cs` yanında) ve HDR ekranda alınan dosyanın `ffprobe` çıktısı.

### EKA-P16 — Oyun Yakalama
- Satırlar: EKA-05
- İş: büyük
- Karar: sahip: Oyun sürecine kanca atan ayrı bir yakalama yolu yapılsın mı, yoksa ekran kaydı yolu ölçülüp yeterli mi sayılsın.
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Core/RecorderWizard.cs, src/VidShrink.App/Recorder/RecorderView.Sihirbaz.cs, docs/olcumler/
- Plan: İlk adım ölçümdür: tam ekran bir oyunun bugünkü hızlı yolla kaydı hiç ölçülmedi. Kayıt düzgünse iş küçülür: sihirbazdaki "Oyun" seçimi (`RecorderWizard.cs:5`) oyunun bulunduğu ekranı kendiliğinden hedef alır ve satır kapanır. Kayıt bozuksa kanca yolu (oyunun içine girip çizdiği kareyi oradan alan yöntem) sahibin kararına çıkar; hile koruması olan oyunlarda risk taşır. Ölçüm `tools/VidShrink.Bench` ile alınır ve `docs/olcumler/` altına yazılır.

## Pakete Girmeyenler

- EKA-17: Karar kapalı (K3); kapalı satır pakete girmez.
- EKA-18: Tek rakipte; tampon koduna dokunan paket yok, yanına bedavaya gelmiyor.
- EKA-19: Sahibin kararını bekliyor ve tek rakipte; yanına geleceği bir paket yok.
- EKA-23: Orta iş ve dar kitle; ses grafiğini yeniden kurmayı ister, hiçbir paketin yanında bedava değil.
- EKA-25: Orta iş ve dar kitle; hızlı yol tek ekran çıkışıyla sınırlı, ayrı bir yakalama düzeni ister.
- EKA-26: Orta iş ve tek rakipte; EKA-P7 mikrofonu uygulama tarafına taşırsa yeniden bakılır.
- EKA-27: Kardeş tablodaki EKB-20 ile aynı iş; orada (`EKB-P15`) paketlenmiş.
- EKA-29: Tek rakipte; hiçbir paketin dosyalarına değmiyor.
