# Ana Oturumun Kod Okuması

13 Ekim 2026. Ajan raporu bir iddiadır. Aşağıdaki iddiaları ana oturum, ajanın gösterdiği satırı
kendisi açıp okuyarak denetledi. Tablodakilerin hiçbiri çalıştırılmadı; hepsi okumadır. Koşulan tek
komut sondaki "Ölçülen Tek Şey" başlığında.

Seçim ölçüsü: doğrulamadan sonra öncelik 1 kalan satırların tamamı, karar gerektirmeyen dört dalgaya giren
paketlerin dayandığı bütün satırlar (birkaçı aşağıdaki örneklem tablosunda), sahibe sorulacak dokuz sorunun
paketlerinde ve sahibe bağlı öbür paketlerde o güne dek okunmamış satırlar (tablonun 271–349. satırları), öncelik 2 taşıyıp o güne dek okunmamış kalan 3 satır (tablonun son 3 satırı), bir kusura işaret eden iddialar,
platform kolunun "yalnız Windows'ta var" dediği yerlerden bir demet ve doğrulama kollarının "bizde yarım"
dediği yerlerden örnekler. Tabloya girmeyen satırlar yalnız doğrulama kolunun okumasına dayanıyor; bunların
çoğu öncelik 3, kalanı öncelik 2.

"Araması boş" yazan satırlarda kanıt bir yokluktur: aranan adlar ana oturumun seçtiği adlardır, başka adla
yazılmış bir karşılık gözden kaçmış olabilir.

| No | Satır | İddia | Okunan Yer | Sonuç |
|---|---|---|---|---|
| 1 | EKB-11 | Tuş gösterimi yazılan her tuşu kayda yazıyor; parola yazılırken de gösterir. | `src/VidShrink.App/Recorder/RecorderInput.cs`, `src/VidShrink.App/Recorder/RecorderView.Girdi.cs:79` | tuttu |
| 2 | AYR-42 | Okunamayan ayar dosyası sessizce varsayılana düşüyor, yedek yok. | `src/VidShrink.App/AppSettings.cs:163-166` | tuttu |
| 3 | AYR-47 | Elle güncelleme denetimi otomatik güncelleme açıkken erken dönüyor. | `src/VidShrink.App/MainWindow.axaml.cs:2917-2923` | tuttu |
| 4 | EKA-17 | Pencere hedefi pencerenin ekrandaki dikdörtgenini sabit bölge olarak kaydediyor. | `src/VidShrink.Core/RecorderArguments.cs:1014-1026` | tuttu |
| 5 | DAG (ek not) | Kurulum belgesi "yalnız win-x64 yayımlanıyor", README "dört hedef" diyor; yayın altı hedef üretiyor. | `docs/kurulum.md:50`, `:73-76`, `:180`; `README.md:74-75`; `.github/workflows/release.yml:347` | tuttu |
| 6 | DZA-33 | Donuk bölüm taraması `freezedetect` ile zaten var. | `src/VidShrink.Core/IdleTrim.cs:29-41` | tuttu |
| 7 | DZA-39 | Oynatıcı menüsünde kare kaydı ve kareyi kopyalama satırları var. | `src/VidShrink.App/Playback/PlayerView.Menu.cs:29-34` | tuttu |
| 8 | DZA-14 | Komut satırı bölüm seçeneğini tanıyor. | `src/VidShrink.Cli/CliRequest.cs:436` | tuttu |
| 9 | DZA-09 | İş kuyruğu sınıfı türden bağımsız yazılmış, düzenleyici de kullanabilir. | `src/VidShrink.Core/JobPump.cs:8` | tuttu |
| 10 | SIK-02 | Kodlama süreci öncelik atanmadan başlıyor; öncelik yalnız yardımcı süreçlerde. | `src/VidShrink.Ffmpeg/EncodeRunner.cs:815-820` | tuttu |
| 11 | EKA-02 | Linux'ta ekran girdisi her zaman X11; Wayland yolu yok. | `src/VidShrink.Core/RecorderArguments.cs:1469`; `src` içinde pipewire, portal, kmsgrab araması boş | tuttu |
| 12 | OYA-06 | Ses düzeyi oturum içinde taşınıyor ama ayara yazılmıyor. | `src/VidShrink.App/Playback/PlayerView.axaml.cs:739-740` | tuttu |
| 13 | EKA-03 | Sistem sesi yakalama Windows dışında boş dönüyor. | `src/VidShrink.Ffmpeg/SystemAudioCapture.cs:36` | tuttu |
| 14 | OYA-07 | Ekran üstü bildirim yalnız hız ve ses düzeyinde çağrılıyor. | `src/VidShrink.App/Playback/PlayerView.Gosterge.cs:33`, `:64-66`, `:72` | tuttu |
| 15 | OYB-01 | Oynatıcıya bırakma tek dosyayı kabul ediyor, klasörü ve çoklu seçimi geri çeviriyor. | `src/VidShrink.App/Playback/PlayerView.Window.cs:470-476` | tuttu |
| 16 | OYB-20 | Liste uzantısıyla biten yayın adresi motora gitmeden yerel liste okuma yoluna giriyor. | `PlayerView.Tools.cs:490-507`, `PlayerView.Window.cs:246-250`, `PlayerView.axaml.cs:673`, `PlayerView.Kuyruk.cs:25-29`, `src/VidShrink.Core/PlaylistFile.cs:20-27` | tuttu |
| 17 | OYB-20 | Liste dosyasının içindeki liste uzantılı girdi atılıyor. | `src/VidShrink.Core/PlaylistFile.cs:48` | tuttu |
| 18 | DZA-05 | Hızlı kip kesim başını önceki anahtar kareye çekiyor, farkı kullanıcıya söylemiyor. | `src/VidShrink.Core/Editing/EditExport.cs:533`, `:572` | tuttu |
| 19 | DZA-12 | Çizelge anahtar kare listesini elinde tutuyor. | `src/VidShrink.App/Editing/EditorTimeline.Serit.cs:36-46` | tuttu |
| 20 | OYB-22 | Site bağlantısı çözme motorda bilerek kapalı ve adres planında kapsam dışı. | `src/VidShrink.Player/MpvEngine.cs:162`, `docs/plan-url-oynatma.md:58` | tuttu |
| 21 | DAG (ek not) | README imza başvurusunu "sürüyor" diye yazıyor, imza belgesi "hiçbiri gönderilmedi" diyor. | `README.md:409-411`, `docs/imzalama.md:6` | tuttu |
| 22 | DZA-07 | Sessizlik taramasının bulduğu aralıklar topluca kesiliyor, tek tek seçilemiyor. | `src/VidShrink.App/Editing/EditorView.Sessizlik.cs:125-133` | tuttu |
| 23 | DZA-21, DZA-22 | Kenar payı tek sayı; tarama her zaman ilk ses izine bakıyor. | `src/VidShrink.Core/Editing/SilenceCut.cs:29`, `:90`, `:171-172` | tuttu |
| 24 | DZA-04 | Akıllı kip yalnız AAC'yi, koşulla bir kodeki daha kopyalıyor; ötekiler Tam kipe düşüyor. | `src/VidShrink.Core/Editing/EditExport.cs:116-124` | tuttu |
| 25 | SIK-01 | Koşan kodlama askıya alınamıyor: duraklatma yalnız kuyrukta, deneme süresi kesintisiz bir sayaçla ölçülüyor. | `src/VidShrink.Core/JobPump.cs:50`, `src/VidShrink.Ffmpeg/EncodeRunner.cs:433-437`; kaynakta süreç askıya alan çağrı araması boş | tuttu |
| 26 | DON-02, DON-10 | Altyazı izi ve dış altyazı girdisi Küçült'ün akış planında hazır; Dönüştür onu kullanmıyor. | `src/VidShrink.Core/StreamMapping.cs:255`, `:798` | tuttu |
| 27 | DON-03 | Dönüştür kendi süzgeç listesini kuruyor, Küçült'ün zincirini çağırmıyor. | `src/VidShrink.Core/ConversionArguments.cs:229-232` | tuttu |
| 28 | EKB-05 | Fare kancası yalnız tıklama anını iletiyor, imleç hareketini değil. | `src/VidShrink.App/Recorder/RecorderInput.cs:202-212` | tuttu |
| 29 | EKB-06, EKB-17 | Kamera katmanı ölçekleme ve köşeye bindirmeden ibaret; ayna ve biçim maskesi yok. | `src/VidShrink.Core/RecorderArguments.cs:937-945` | tuttu |
| 30 | DON-49 | DVD-Video girdi seçeneklerini üreten kayıt motorda yazılı; karar belgesi "bilerek yapılmaz" diyor. | `src/VidShrink.Core/SourceTitles.cs:24-39`, `src/VidShrink.Core/FfmpegArguments.cs:550`, `:563`; `docs/handbrake/durum-2026-09-23.md:139` | tuttu |
| 31 | EKB (ek not) | Paylaşım, teslim edilen kaydı olduğu gibi yüklüyor; sonuçta yalnız bağlantı var, yüklemeyi silen bir yol yok. | `src/VidShrink.App/Recorder/RecorderView.Paylas.cs:69`, `:94`; `src/VidShrink.Core/Share/ShareResult.cs:100-124`; kaynakta silme adresi araması boş | tuttu |
| 32 | SIK-24 | Hata veren işi yeniden sıraya alan yol yok; eldeki "yeniden dene" paneli hedef boyut tutmadığında çıkıyor. | `src/VidShrink.App/MainWindow.axaml.cs:4616-4633`; kaynakta yeniden sıraya alma araması boş | tuttu |
| 33 | OYA-09 | Bölümlerin yalnız zamanı okunuyor, adı okunmuyor. | `src/VidShrink.Player/MpvEngine.cs:430-444` | tuttu |
| 34 | OYB-07 | Klasör gezgini açılan dosyanın klasörünü tek düzeyde okuyor; alt klasöre inmiyor. | `src/VidShrink.App/Playback/FolderNavigator.cs:20-32` | tuttu |
| 35 | DZA-30 | Çizelge anahtar kare çentiklerini çiziyor; monitörde anlık gösterge yok. | `src/VidShrink.App/Editing/EditorTimeline.Serit.cs:36-46`; `EditorView.Monitor.cs` içinde anahtar kare araması boş | tuttu |
| 36 | DZB-19 | Metin modeli yazı tipi, kalın, italik ve solma taşıyor; panelde yalnız içerik, boyut ve renk var. | `src/VidShrink.Core/Editing/TextLayer.cs:43-55`, `src/VidShrink.App/Editing/EditorView.axaml:95-108` | tuttu |
| 37 | DZB-21 | Metin kenarlığı boyuttan kendiliğinden hesaplanıyor, kullanıcı ayarı yok. | `src/VidShrink.Core/Editing/AssWriter.cs:124` | tuttu |
| 38 | SIK-20 | İş parçacığı sınırı yalnız yardımcı süreçlerde; kodlama komutunda yok. | `src/VidShrink.Core/AudioLevel.cs:71`, `src/VidShrink.Ffmpeg/SceneDetector.cs:97`; `FfmpegArguments.cs` ve `EncodeRunner.cs` içinde arama boş | tuttu |
| 39 | DON-04 | Dönüştür motoru donanım kodlayıcılarını kabul ediyor ve testli; kodek listesinde yoklar. | `src/VidShrink.Core/ConversionArguments.cs:142-145`, `tests/VidShrink.Tests/HardwareEncoderTests.cs:213-226`, `src/VidShrink.App/MainWindow.axaml:1149-1160` | tuttu |
| 40 | ARC-24 | Gürültü bastırma süzgeci yalnız kayıt sırasında bağlanıyor; hazır dosyaya uygulanamıyor. | `src/VidShrink.Core/AudioCaptureArguments.cs:73`, `:135`; kaynakta başka kullanım yok | tuttu |
| 41 | ARC-04 | Ses yüksekliğini sabit hedefe çeken tek geçişli süzgeç var; ölçüm geçişi ve rapor yok. | `src/VidShrink.Core/StreamMapping.cs:354-357`; kaynakta ölçüm süzgeci araması boş | tuttu |
| 42 | PLT-01 | macOS'ta ekran, açılır kutudaki sırasıyla ffmpeg'e veriliyor; kamera sayısı kadar kaydıran bir düzeltme yok. Bölge ve pencere hedefi de aynı girdiden geçiyor (`src/VidShrink.App/Recorder/RecorderView.Secici.cs:79`). | `src/VidShrink.Core/RecorderArguments.cs:1461-1467`, `src/VidShrink.App/Recorder/RecorderView.Hedef.cs:177`, `src/VidShrink.App/Recorder/RecorderView.Secici.cs:138`; testler aynı sayıyı pimliyor (`tests/VidShrink.Tests/KaydediciPencereTests.cs:76`, `:216`) | tuttu |
| 43 | PLT-14 | Tıklama, tıklama sesi, tuş ve büyüteç kutuları her sistemde görünüyor; arkalarındaki parçalar Windows dışında boş sınıfa düşüyor. | `src/VidShrink.App/Recorder/RecorderView.Girdi.cs:14-40`, `src/VidShrink.App/Recorder/RecorderView.axaml:616-631`; gizlenen tek panel kamera (`src/VidShrink.App/Recorder/RecorderView.Kamera.cs:53`) | tuttu |
| 44 | OYA-01 | Oynatma sürerken uykuyu ve ekran kararmasını engelleyen çağrı yok. | kaynakta `SetThreadExecutionState`, `stop-screensaver`, `IOPMAssertion`, `org.freedesktop.ScreenSaver` araması boş; ekranın gerçekten karardığı ölçülmedi | tuttu |
| 45 | OYA-03 | Ses çıkış aygıtı seçilemiyor. | kaynakta `audio-device` araması boş | tuttu |
| 46 | OYA-08 | Oynatıcıda imleci kendiliğinden gizleyen kod yok. | `src/VidShrink.App/Playback` altında imleç yalnız boyutlandırma ve el imleci için değişiyor (`ComparisonPanel.axaml.cs:546`, `:602`, `ControlStrip.axaml:48`) | tuttu |
| 47 | OYA-25 | Altyazı satırına atlama yok. | kaynakta `sub-step`, `sub-seek` araması boş | tuttu |
| 48 | OYB-02 | Liste penceresinde arama kutusu yok. | `src/VidShrink.App/Playback/PlayerView.Playlist.cs` içinde metin kutusu ya da arama geçmiyor | tuttu |
| 49 | OYB-11 | İzleme geçmişi tutuluyor, gösteren pencere yok. | arayüz tarafında geçmiş yalnız yükleniyor (`src/VidShrink.App/Playback/PlayerView.axaml.cs:736`) | tuttu |
| 50 | OYB-13 | Kaldığı yerden devam sormadan ve ayarsız uygulanıyor. | `src/VidShrink.App/Playback/PlayerView.axaml.cs:750-751`; kaynakta devamı kapatan ayar araması boş | tuttu |
| 51 | DZA-06 | Otomatik bölme yok; sınır listesinden bölen yol yalnız sahne bölmede çağrılıyor. | `src/VidShrink.Core/Editing/EditTimeline.cs:235`; tek çağıran `src/VidShrink.App/Editing/EditorView.Sahne.cs:41` | tuttu |
| 52 | DZA-03 | Teslimde iz eşlemesi sabit: ilk görüntü ve ilk ses. | `src/VidShrink.Core/Editing/EditExport.cs:541-542`, `:580-581`, `:656-657` | tuttu |
| 53 | DZA-04 | Akıllı kipte ses kapısı AAC ile koşullu tek kodek dışını geçirmiyor. | `src/VidShrink.Core/Editing/EditExport.cs:116-121` | tuttu |
| 54 | DZA-07 | Bulunan sessizlik aralıkları yalnız topluca kesiliyor. | `src/VidShrink.App/Editing/EditorView.Sessizlik.cs:125-131` | tuttu |
| 55 | DZB-35 | Düzenleyicide renk ayarı yok; parlaklık süzgeci yalnız oynatıcıda. | `src/VidShrink.Player/MpvEngine.Advanced.cs:81`; kaynakta başka `eq=` yok | tuttu |
| 56 | DZB-54 | Tam kip teslimi sabit kodek ve sabit kaliteyle yazıyor. | `src/VidShrink.Core/Editing/EditExport.cs:68-70` | tuttu |
| 57 | SIK-01 | Koşan kodlama askıya alınmıyor. | kaynakta `NtSuspendProcess`, `SuspendThread`, `SIGSTOP` araması boş | tuttu |
| 58 | SIK-03, DON-07 | Dış ses dosyası eklenemiyor; ek girdi yalnız dış altyazı. | `src/VidShrink.Core/FfmpegArguments.cs:545-566`, `src/VidShrink.Core/StreamMapping.cs:219` | tuttu |
| 59 | SIK-04 | Yakılan altyazıda yazı tipi ve boyut seçimi yok. | `src/VidShrink.Core/VideoFilterChain.cs:205-209`; kaynakta `force_style` araması boş, yazı tipi yalnız düzenleyicinin metin katmanında (`src/VidShrink.Core/Editing/AssWriter.cs:127`) | tuttu |
| 60 | DON-02 | Dönüştür altyazı izini açıkça eşlemiyor. | `src/VidShrink.Core/ConversionArguments.cs` içinde iz eşleme ya da altyazı kodeği seçeneği yok; yalnız dil etiketi yazılıyor (`:202-206`) | tuttu |
| 61 | DON-05 | Dönüştür son seçimi sıra numarasıyla hatırlıyor; adlı ön ayar yok. | `src/VidShrink.App/MainWindow.axaml.cs:1410-1414` | tuttu |
| 62 | DON-06 | Dönüştür'ün dosya seçicisi tek dosya alıyor. | `src/VidShrink.App/MainWindow.axaml.cs:3231-3233` | tuttu |
| 63 | DON-08, ARC-09 | Resim dizisi girdi olarak alınmıyor; resim biçimi yalnız çıktı tarafında. | `src/VidShrink.Core/Editing/ThumbnailQueue.cs:254`, `src/VidShrink.Core/RecorderArguments.cs:1248`, `src/VidShrink.Ffmpeg/EncodeRunner.cs:194` | tuttu |
| 64 | EKB-05 | İmleç halkası yalnız tıklama anında ve kısa süre görünüyor. | `src/VidShrink.App/Recorder/RecorderInputOverlay.cs:103-113` | tuttu |
| 65 | EKB-06 | Kamera karesi yalnız dikdörtgen. | `src/VidShrink.Core/RecorderArguments.cs:932-933`; aynı dosyada daire ya da yuvarlak köşe maskesi araması boş | tuttu |
| 66 | EKB-08 | Gizlilik maskesi yok. | kaynakta `boxblur`, `gblur`, `delogo`, `pixelize` araması boş | tuttu |
| 67 | AYR-01 | Ayarların tümünü dışa aktaran yol yok; yalnız ön ayar aktarılıyor. | `src/VidShrink.App/MainWindow.OnAyar.cs:301`; kaynakta ayar yedeği araması boş | tuttu |
| 68 | ARC-19 | Dosya okuyucu görüntüsü olmayan dosyayı reddediyor. | `src/VidShrink.Ffmpeg/FfprobeClient.cs:45-46` | tuttu |
| 69 | ARC-01 | Ayrı bir medya bilgisi penceresi yok. | kaynakta böyle bir pencere ya da rapor sınıfı araması boş; var olan özet panel okunmadı | tuttu |
| 70 | DZA-28 | Komut satırı tek aralık kesiyor; proje ya da parça listesi alan bayrak yok. | `src/VidShrink.Cli/CliRequest.cs:425-428`; `src/VidShrink.Cli` içinde proje ve parça bayrağı araması boş | tuttu |
| 71 | DZA-09 | Düzenleyicide teslim kuyruğu yok. | kaynakta teslim kuyruğu ve toplu teslim adlarıyla arama boş | tuttu |
| 72 | EKB-09 | Kayıt panelinde kırpma önizlemesi yok; Düzenle düğmesi kaydı düzenleyiciye gönderiyor. | `src/VidShrink.App/Recorder/RecorderView.Duzenle.cs:11-14` | tuttu |
| 73 | EKB-25 | Kayıt geçmişi listesi yok. | kaynakta kayıt geçmişi adlarıyla arama boş | tuttu |
| 74 | EKB-04 | Kayıt sırasında ekrana çizim aracı yok. | `src/VidShrink.App/Recorder` altında çizim ve kalem adlarıyla arama boş | tuttu |
| 75 | EKB-15 | Prompter yok. | kaynakta `prompter` araması boş | tuttu |
| 76 | DAG-31 | Komut satırından kayıt başlatan bayrak yok; uygulamanın dışarıdan aldığı bayrak küçültme isteği. | `src/VidShrink.Core/ShellIntegration.cs:109`; kaynakta kayıt bayrağı araması boş | tuttu |
| 77 | ARC-14 | Başlık, sanatçı gibi genel etiketi yazan argüman yok; yalnız iz adı ve dili yazılıyor, üst veri topluca taşınıyor ya da siliniyor. | `src/VidShrink.Core/StreamMapping.cs:286-294`, `:341-349` | tuttu |
| 78 | ARC-16 | Bozuk dizini hoş gören okuma bayrakları yok. | kaynakta `genpts`, `igndts`, `err_detect` araması boş | tuttu |
| 79 | PLT-02 | Linux girdisi yalnız x11grab; Wayland oturumunda pencere hedefi hata satırı verir, ekran ve bölge hedefinde kapı yok. | `src/VidShrink.Core/RecorderArguments.cs:1469-1486`, `src/VidShrink.App/Recorder/RecorderView.Secici.cs:44-48`; kaynakta `pipewire`, `portal`, `kmsgrab` araması boş | tuttu |
| 80 | PLT-03 | Sistem sesinin gerçek kaynağı yalnız Windows; başka yerde adında "loopback" ya da "monitor" geçen aygıt aranır. | `src/VidShrink.Ffmpeg/SystemAudioCapture.cs:36`, `src/VidShrink.Ffmpeg/CaptureDevices.cs:58-59`; kaynakta `ScreenCaptureKit` araması boş | tuttu |
| 81 | PLT-08, PLT-09 | Genel kısayol Windows dışında boş uygulama. | `src/VidShrink.App/Recorder/RecorderView.Kisayol.cs:31-34` | tuttu |
| 82 | PLT-35, PLT-36 | İş bitti bildirimi Windows dışında sessiz yüz. | `src/VidShrink.App/IsBittiBildirimi.cs:63-67`; kaynakta `UNUserNotification`, `notify-send`, `org.freedesktop.Notifications` araması boş | tuttu |
| 83 | PLT-20 | Kendini güncelleme Linux'ta kapalı. | `src/VidShrink.Core/UpdateCheck.cs:146-147` | tuttu |
| 84 | PLT-19 | Ekran yenileme hızı Windows dışında sıfır döner. | `src/VidShrink.App/Recorder/ScreenRefresh.cs:22` | tuttu |
| 85 | PLT-39 | Çocuk süreci toplayan iş nesnesi yalnız Windows; ötekinde hiçbir şey yapılmıyor. | `src/VidShrink.Ffmpeg/ChildJob.cs:12`, `:28`; kaynakta `prctl`, `setpgid`, `killpg` araması boş | tuttu |
| 86 | PLT-04 | macOS izin yoklaması ve izin açıklama anahtarı yok. | depoda `NSMicrophoneUsageDescription`, `NSCameraUsageDescription`, `CGPreflightScreenCaptureAccess` araması boş | tuttu |
| 87 | PLT-05 | macOS pencere hedefi ekran kırpmasına çevriliyor; pencerenin kendi içeriği yakalanmıyor. | `src/VidShrink.Core/RecorderArguments.cs:1497-1508` | tuttu |
| 88 | PLT-07 | Linux kamera listesi hep boş dönüyor. | `src/VidShrink.Ffmpeg/CaptureDevices.cs:252` | tuttu |
| 89 | PLT-15, PLT-16 | Kaydedicinin kodlayıcı listesinde VideoToolbox ve VAAPI yok. | `src/VidShrink.App/Recorder/RecorderView.Hedef.cs:23-29`; kaydedici kaynağında `videotoolbox`, `vaapi` araması boş | tuttu |
| 90 | PLT-17 | Bölge düzenleyicisi yalnız Windows. | `src/VidShrink.App/Recorder/RecorderView.Duzenleyici.cs:23-27` | tuttu |
| 91 | PLT-18 | Denetim yüzeylerini kayıttan çıkarma yalnız Windows. | `src/VidShrink.App/Recorder/CaptureAffinity.cs:20-23` | tuttu |
| 92 | PLT-21 | Otomatik sessiz güncelleme Windows dışında kapalı. | `src/VidShrink.App/MainWindow.KapanistaGuncelleme.cs:85`, `:119` | tuttu |
| 93 | PLT-22, PLT-23 | Yayın iş akışı dmg, AppImage ya da Flatpak üretmiyor. | `.github` ve kurulum betiğinde `create-dmg`, `appimage`, `flatpak` araması boş; tek `hdiutil` satırı rakibin kalıbını açan kıyas iş akışında | tuttu |
| 94 | AYR-03 | Pencere konumu ve boyutu saklanmıyor; pencere ekranı kaplamış açılıyor, yerleşim dosyasında yalnız bir panel ölçüsü var. | `src/VidShrink.App/MainWindow.axaml:13-14`, `src/VidShrink.App/MainWindow.axaml.cs:3123-3125` | tuttu |
| 95 | AYR-05 | Taşınabilir kip yok; ayar yolu yalnız ortam değişkeniyle değişiyor. | `src/VidShrink.App/AppDataReset.cs:10`; kaynakta `portable` araması boş | tuttu |
| 96 | DAG-01, DAG-04 | Depoda winget bildirimi de Flatpak tarifi de yok. | izlenen dosyalarda ve `.github` altında `winget`, `flatpak`, `flathub` araması boş | tuttu |
| 97 | DAG-02 | Yayın iş akışında imza adımı yok; imza belgesi "hiçbiri gönderilmedi" diyor. | `docs/imzalama.md:3`, `:6`; `.github` altında `signtool`, `signpath` araması boş | tuttu |
| 98 | DAG-35 | Dışarıya açık tek kanal tek örnek borusu: yalnız aynı kullanıcıya açık, taşıdığı şey dosya yolu listesi. | `src/VidShrink.Core/SingleInstanceChannel.cs:59-66`, `:82` | tuttu |
| 99 | DON-01 | Ses izi seçimi motorda ya hepsi ya da dile göre tek iz; tek tek iz seçme yok. | `src/VidShrink.Core/StreamMapping.cs:637-639` | tuttu |
| 100 | DON-09 | Yakılan altyazıya biçem yazılmıyor. | `src/VidShrink.Core/VideoFilterChain.cs:233-236`; kaynakta `force_style` araması boş | tuttu |
| 101 | DZA-01 | Birden çok kaynakta teslim her kipte süzgeç grafiğiyle, yani yeniden kodlamayla çıkıyor. | `src/VidShrink.Core/Editing/EditExport.cs:252`, `:305-320` | tuttu |
| 102 | DZA-02 | Teslimde yalnız ilk görüntü ve ilk ses izi eşleniyor. | `src/VidShrink.Core/Editing/EditExport.cs:541-542` | tuttu |
| 103 | DZB-01 | Görüntüsü olmayan dosya kaynak olarak reddediliyor; çizelgede tek ses izi var. | `src/VidShrink.App/Editing/EditorView.Kaynak.cs:89-93`, `src/VidShrink.App/Editing/EditorTimeline.cs:22` | tuttu |
| 104 | DZB-02 | Çizelgede tek video izi var; bindirme süzgeci yalnız kaydedicide. | `src/VidShrink.App/Editing/EditorTimeline.cs:21`, `src/VidShrink.Core/RecorderArguments.cs:944`; düzenleyici kaynağında bindirme süzgeci araması boş | tuttu |
| 105 | DZB-16, DZB-17 | Klip başına yalnız solma var; iki klip arası çapraz geçiş süzgeci yok. | `src/VidShrink.Core/Editing/ClipEffects.cs:33-35`, `src/VidShrink.Core/Editing/ClipFilters.cs:69`; kaynakta `xfade`, `acrossfade` araması boş | tuttu |
| 106 | ARC-12 | GIF çıktı olarak üretiliyor ve Gözat listesinde var; izlenen klasörün uzantı listesinde yok. | `src/VidShrink.Core/ConversionPlan.cs:33`, `src/VidShrink.Core/ShellIntegration.cs:14`, `src/VidShrink.Core/WatchFolder.cs:74-76` | tuttu |
| 107 | ARC-21 | Altyazı gecikmesi yalnız oynatmada ayarlanıyor; dış altyazı dosyası çıktıya iz olarak ekleniyor, tek başına çeviren yol yok. | `src/VidShrink.Player/MpvEngine.cs:389`, `src/VidShrink.Core/StreamMapping.cs:86-95` | tuttu |
| 108 | EKB-01, EKB-03 | Otomatik yakınlaştırma ve imleç yumuşatma yok; imleç yalnız gösterilir ya da gizlenir. | `src/VidShrink.App/Recorder/RecorderSettings.cs:64`, `src/VidShrink.App/Recorder/RecorderMagnifier.cs:29-34`; kaynakta `zoompan` araması boş | tuttu |
| 109 | EKB-02 | Dolgu yalnız düz siyah. | `src/VidShrink.Core/VideoFilterChain.cs:347-349` | tuttu |
| 110 | EKB-07 | Kamera arka planında yalnız sabit arka plan farkı ve yeşil perde silme var. | `src/VidShrink.Core/RecorderArguments.cs:926-935` | tuttu |
| 111 | EKA-01 | Uygulama başına ses yakalama yok. | kaynakta `ProcessLoopback`, `ActivateAudioInterfaceAsync` araması boş | tuttu |
| 112 | EKA-04 | macOS pencere hedefi ekran kırpmasıyla kaydediliyor (PLT-05 ile aynı yer). | `src/VidShrink.Core/RecorderArguments.cs:1497-1508` | tuttu |
| 113 | SIK-06 | Kodlama sürerken uykuyu engelleyen çağrı yok. | kaynakta `SetThreadExecutionState`, `caffeinate`, `systemd-inhibit` araması boş; en yakın kod `src/VidShrink.App/QueueEndActions.cs:9` | tuttu |
| 114 | OYA-02 | Çift tık ayrı hareket değil: sol tık oynat ya da duraklat, orta tık tam ekran. | `src/VidShrink.App/Playback/Keymap.cs:110-111`; oynatıcı görünümünde `DoubleTapped` araması boş | tuttu |
| 115 | OYA-04 | Altyazı kapalı başlıyor; dil tercihi seçeneği motora verilmiyor. | `src/VidShrink.Player/MpvEngine.cs:166`; kaynakta `alang`, `slang` araması boş | tuttu |
| 116 | OYA-11 | Oynatıcıda ton eşleme ayarı yok. | kaynakta `tone-mapping`, `target-trc`, `target-peak` araması boş | tuttu |
| 117 | OYA-29 | Altyazı yalnız videonun klasöründe, adı videoyla başlayan dosyalardan aranıyor. | `src/VidShrink.App/Playback/SubtitleOptions.cs:35-44` | tuttu |
| 118 | OYA-36, OYB-12 | Kaldığı yerden devam yalnız en son beş dosyada çalışıyor. | `src/VidShrink.App/Playback/PlaybackHistory.cs:18`, `:107` | tuttu |
| 119 | OYA-37 | Sessiz bölümleri atlama yok. | oynatıcı kaynağında `silence` araması boş | tuttu |
| 120 | OYA-38 | Basılı tutma diye girdi türü yok; sol tık işini bırakışta yapıyor. | `src/VidShrink.App/Playback/PlayerView.Fare.cs:88-94`, `src/VidShrink.App/Playback/Keymap.cs:83` | tuttu |
| 121 | OYB-29 | Ortam tuşları pencere tuş eşlemesinde; sistem ortam denetimine bağlanan kod yok. | `src/VidShrink.App/Playback/Keymap.cs:178-180`; kaynakta `SystemMediaTransportControls`, `media-controls` araması boş | tuttu |
| 122 | OYA-26 | Altyazıyı tek tuşla gizleyen ayrı komut yok; S tuşu izleri sırayla gezip kapalıya geliyor. | `src/VidShrink.App/Playback/Keymap.cs:168`, `src/VidShrink.App/Playback/PlayerView.Tracks.cs:75-86` | tuttu |
| 123 | OYA-30 | Dış altyazı uzantıları srt, ass, ssa, vtt ve sub. | `src/VidShrink.App/Playback/SubtitleOptions.cs:35` | tuttu |
| 124 | OYA-32 | Altyazı metnini panoya kopyalama yok. | oynatıcı kaynağında `sub-text` araması boş | tuttu |
| 125 | OYA-49 | Üstte tut yalnız açılıp kapanıyor; oynatmaya bağlı kip yok. | `src/VidShrink.App/Playback/Keymap.cs:71`, `:151-152` | tuttu |
| 126 | OYA-50 | Pencere simge durumuna inince duraklatan kod yok. | `src/VidShrink.App/Playback` altında `Minimized` araması boş | tuttu |
| 127 | OYA-21, OYA-22, OYA-23 | Oynatıcı ses geçirme, özel kip ve kanal düzeni seçeneklerini motora vermiyor. | oynatıcı kaynağında `audio-spdif`, `audio-exclusive`, `audio-channels` araması boş | tuttu |
| 128 | OYB-03 | Kuyrukta üç işlem var: sona ekle, sıradakine al, kaldır. Serbest taşıma yok. | `src/VidShrink.App/Playback/PlayerView.Kuyruk.cs:53`, `:55`, `:57` | tuttu |
| 129 | OYB-04 | Klasör yalnız doğal ad sırasıyla geliyor. | `src/VidShrink.App/Playback/FolderNavigator.cs:79` | tuttu |
| 130 | OYB-05 | Liste satırı yalnız adı gösteriyor. | `src/VidShrink.App/Playback/PlayerView.Playlist.cs:106` | tuttu |
| 131 | DZA-18 | Değişkenli ad deseni yalnız Küçült çıktısında; düzenleyici sabit sonek yazıyor. | `src/VidShrink.Core/AdlandirmaDeseni.cs:18`, `src/VidShrink.Core/Editing/EditOutputName.cs:9` | tuttu |
| 132 | DZA-19 | Klip modelinde ad alanı yok. Kolun gösterdiği satır başka bir yöntemi gösteriyor; iddia yine de tutuyor. | `src/VidShrink.Core/Editing/EditClip.cs:26-44` | tuttu |
| 133 | DZA-29 | İşaretçiler bellekte duruyor; proje dosyasına yazılmıyor. | `src/VidShrink.App/Editing/EditorTimeline.cs:43`; `src/VidShrink.Core/Editing/EditProject.cs` içinde işaretçi araması boş | tuttu |
| 134 | DZA-31 | Sahne eşiği varsayılan değerden geliyor; hiçbir çağıran eşik vermiyor. | `src/VidShrink.Ffmpeg/SceneDetector.cs:85`, `src/VidShrink.Core/SceneMap.cs:36` | tuttu |
| 135 | DZA-20 | Sessizlik taramasının sonucu yalnız siliniyor; hızlandırma seçeneği yok. | `src/VidShrink.App/Editing/EditorView.Sessizlik.cs:125-131` (54. satırla aynı yer) | tuttu |
| 136 | DZA-10 | Ses izi ve altyazı seçimi oynatıcının sağ tık menüsünde tanımlı. Menü ekranda görülmedi. | `src/VidShrink.App/Playback/PlayerView.Menu.cs:23-24` | tuttu |
| 137 | DZB-55 | Ön ayar dışa aktarma yalnız Küçült sekmesinde. | `src/VidShrink.App/MainWindow.OnAyar.cs:385-401` | tuttu |
| 138 | DZB-58 | Düzenleyici çıktısı yalnız MP4 ve MKV. | `src/VidShrink.App/Editing/EditorView.Teslim.cs:392` | tuttu |
| 139 | DZB-30 | Düzenleyicide renk süzgeci yok. | `src/VidShrink.Core/Editing` altında `hue=`, `colorchannelmixer`, `curves=`, `lut3d`, `eq=`, `sepia`, `grayscale` araması boş | tuttu |
| 140 | DZB-12 | Çoklu silme var; klipleri gruplama yok. | `src/VidShrink.Core/Editing/EditTimeline.cs:362` | tuttu |
| 141 | DZB-11 | Pano yalnız paylaşım bağlantısı için kullanılıyor; klip kopyalama yok. | `src/VidShrink.App/Editing/EditorView.Teslim.cs:314-319` | tuttu |
| 142 | SIK-41 | Disk alanı yalnız kodlama başlamadan önce, iki yerde denetleniyor. | `src/VidShrink.App/MainWindow.axaml.cs:4514`, `src/VidShrink.Cli/CliApp.cs:519`; `DiskSpaceGuard` başka yerde geçmiyor | tuttu |
| 143 | DON-11 | Gömülü altyazıyı dosyaya çıkaran yol yok. | kaynakta `ExtractSubtitle`, `AltyaziCikar`, `SubtitleExtract`, `-c:s srt` araması boş | tuttu |
| 144 | DON-12 | Ön ayar dışa aktarma Küçült'te; Dönüştür'ün ön ayar türü yok. | `src/VidShrink.App/MainWindow.OnAyar.cs:385`; kaynakta `ConversionPreset` araması boş | tuttu |
| 145 | DON-13 | Dönüştür tek dosyayı ekrandan okunan tek planla çeviriyor; dosya başına ayar yok. | `src/VidShrink.App/MainWindow.axaml.cs:4883-4904` | tuttu |
| 146 | DON-23 | Görüntü dizisi çıktısı yok. | `Conversion*.cs` dosyalarında `image2`, `%0Nd`, `-frames:v` araması boş | tuttu |
| 147 | DON-44 | Kare kaydı yalnız oynatıcıda. | `src/VidShrink.Player/MpvEngine.cs:538` | tuttu |
| 148 | EKB-10 | Tıklama halkası tek pencere, süresi sabit 360 ms; ayarlarda yalnız aç kapa var. | `src/VidShrink.App/Recorder/RecorderInputOverlay.cs:47-52`, `:109`, `src/VidShrink.App/Recorder/RecorderSettings.cs:70` | tuttu |
| 149 | EKB-12 | Spot ışığı yok. | kaynakta `spotlight`, `SpotIsik` araması boş | tuttu |
| 150 | EKB-16 | Kamera yalnız dört köşeye yerleşiyor. | `src/VidShrink.Core/RecorderArguments.cs:283-289` | tuttu |
| 151 | ARC-05 | Sessizlik ve siyah kare taraması düzenleyicide, donuk kare taraması kaydedicide; üçü ayrı yerlerde duruyor. | `src/VidShrink.Core/Editing/SilenceCut.cs:90`, `:94`, `src/VidShrink.Core/IdleTrim.cs:24`, `:38` | tuttu |
| 152 | ARC-28 | Kare ara değerleme yok. | kaynakta `minterpolate` araması boş | tuttu |
| 153 | PLT-10 | macOS'ta tıklama gösterimi ffmpeg'e verilmiyor. | kaynakta `capture_mouse_clicks` araması boş | tuttu |
| 154 | DON-14, DON-16, DON-39, DON-40 | Ses yüksekliği, kanal, örnekleme hızı ve görüntü süzgeçleri Küçült yolunda var; Dönüştür'ün argümanlarında yalnız MXF için sabit örnekleme hızı var. | `src/VidShrink.Core/StreamMapping.cs:144`, `:316-317`, `:357`, `src/VidShrink.Core/VideoFilterChain.cs:63`, `src/VidShrink.Core/ConversionArguments.cs:253`; `src/VidShrink.Core/Conversion*.cs` içinde `loudnorm`, `-ac`, `deinterlace`, `crop`, `transpose`, `pad=` araması boş | tuttu |
| 155 | DON-17 | Dönüştür'ün ses listesinde AC3 ve E-AC3 yok; Küçült planı ikisini kabul ediyor. | `src/VidShrink.App/MainWindow.axaml:1298-1305`, `src/VidShrink.Core/PlanParser.cs:14` | tuttu |
| 156 | DON-18 | Yalnız ses çıktısı dört kapla sınırlı. | `src/VidShrink.Core/ConversionPlan.cs:32` | tuttu |
| 157 | DON-19 | Ses kabı seçilince ses kodeki kaba göre atanıyor; kopya yolu yok. | `src/VidShrink.App/MainWindow.axaml.cs:4760`, `:4782` | tuttu |
| 158 | DON-20 | Dönüştür'ün MP4 kopya tablosunda AC3, E-AC3 ve Opus yok; Küçült'ünkinde var. | `src/VidShrink.Core/ConversionArguments.cs:272`, `src/VidShrink.Core/StreamMapping.cs:430` | tuttu |
| 159 | DON-41, DON-43 | Dönüştür kare hızını yalnız `fps` süzgeciyle yazıyor; ölçekleme iki yolda da sabit lanczos. | `src/VidShrink.Core/ConversionArguments.cs:232-237`, `src/VidShrink.Core/VideoFilterChain.cs:338`, `:379` | tuttu |
| 160 | DON-15 | Dönüştür hız ön ayarını sabit varsayılanla yazıyor. | `src/VidShrink.Core/ConversionArguments.cs:142` | tuttu |
| 161 | DON-25 | Dönüştür planı HDR seçimini Küçült sekmesindeki düğmeden okuyor. | `src/VidShrink.App/MainWindow.axaml:802-803`, `src/VidShrink.App/MainWindow.axaml.cs:2272`, `:4777` | tuttu |
| 162 | DON-26 | Renk dönüşümü yalnız BT.709 ve BT.601 için. | `src/VidShrink.Core/VideoFilterChain.cs:397-406` | tuttu |
| 163 | DON-21, DON-22, DON-34 | FFV1, APNG ve CineForm çıktısı yok; hareketli görsel yalnız WebP ve AVIF. | `src/VidShrink.Core/ConversionPlan.cs:34`; kaynakta `ffv1`, `cfhd`, `cineform`, `apng` araması boş | tuttu |
| 164 | DON-24 | GIF paleti tek dither ile sabit. | `src/VidShrink.Core/ConversionArguments.cs:105` | tuttu |
| 165 | DON-27 | Dönüştür'ün komut kutusu salt okunur. | `src/VidShrink.App/MainWindow.axaml:1371` | tuttu |
| 166 | DON-28, EKA-16, DAG-30 | Komut satırında dönüştürme ve kayıt komutu yok. | `src/VidShrink.Cli/CliRequest.cs:6` | tuttu |
| 167 | OYA-55 | Dört sabit atlama adımı var; değeri değiştirilemiyor. | `src/VidShrink.App/Playback/Keymap.cs:52-55` | tuttu |
| 168 | OYA-10 | Zamana git yok. | kaynakta `GoToTime`, `ZamanaGit`, `JumpToTime`, `SeekToTime` araması boş | tuttu |
| 169 | OYA-05 | Geçmiş kaydı dosya başına konum, izlenen yer ve yer imi tutuyor; iz ve gecikme seçimi tutmuyor. | `src/VidShrink.App/Playback/PlaybackHistory.cs:209-222` | tuttu |
| 170 | OYA-34 | Altyazı yazı tipi üç genel aileyle sınırlı. | `src/VidShrink.App/Playback/PlayerView.Advanced.cs:31` | tuttu |
| 171 | SIK-13 | Ad deseninde altı yer tutucu var. | `src/VidShrink.Core/AdlandirmaDeseni.cs:21-22` | tuttu |
| 172 | SIK-07 | Kuyrukta zamanlanmış başlatma yok. | `src/VidShrink.Core/JobPump.cs` ve `src/VidShrink.App/Queue*.cs` içinde `Schedul`, `Zamanla`, `StartAt` araması boş | tuttu |
| 173 | SIK-10 | İş bitti bildirimi ses çalmıyor. | `src/VidShrink.App/IsBittiBildirimi.cs` içinde `Sound`, `Beep` araması boş | tuttu |
| 174 | EKA-12, EKA-21 | Kaydedicinin kodlayıcı listesinde VideoToolbox ve VAAPI yok; AV1 donanım kodlayıcısı yalnız NVENC. | `src/VidShrink.Core/RecorderArguments.cs:368-374` | tuttu |
| 175 | EKA-22 | Kayıtta ses kodeki AAC olarak sabit. | `src/VidShrink.Core/RecorderArguments.cs:349` | tuttu |
| 176 | EKA-28 | Mikrofon süzgeçleri kazanç, gürültü kapısı ve gürültü bastırma; sıkıştırıcı ve sınırlayıcı yok. | `src/VidShrink.Core/AudioCaptureArguments.cs:75-78` | tuttu |
| 177 | EKA-10 | Süre sınırıyla kendiliğinden durma ve en çok on saniyelik geri sayım var. | `src/VidShrink.Core/RecorderArguments.cs:1217`, `src/VidShrink.App/Recorder/RecorderSettings.cs:46` | tuttu |
| 178 | EKA-15, EKA-24 | Yalnız ses kaydı ve kare hızı kipi seçimi yok. | kaydedici kaynağında `AudioOnly`, `YalnizSes` araması boş; `src/VidShrink.Core/RecorderArguments.cs` içinde `fps_mode`, `vsync` araması boş | tuttu |
| 179 | DZB-42, DZB-43, DZB-45 | Düzenleyicide ses süzgeci yok; gürültü bastırma yalnız kaydedicide. | `src/VidShrink.Core/Editing` altında `afftdn`, `loudnorm`, `acompressor`, `alimiter`, `equalizer` araması boş; `src/VidShrink.Core/AudioCaptureArguments.cs:135` | tuttu |
| 180 | DZA-08, DZA-13 | Düzenleyici bölüm yazmıyor. | `src/VidShrink.Core/Editing/EditExport.cs:387` | tuttu |
| 181 | OYA-43 | Kare her zaman altyazısız kaydediliyor. | `src/VidShrink.Player/MpvEngine.cs:538` | tuttu |
| 182 | OYB-32 | İkinci açılış yalnız ilk yolu aktarıyor. | `src/VidShrink.App/Integration/ForwardedFiles.cs:20-22` | tuttu |
| 183 | OYB-33 | Sağ tık menüsünde yalnız aç ve küçült fiili var. | `src/VidShrink.App/ShellMenu.cs:22-24`; `ShellMenu.cs`, `ShellRegistration.cs` ve `ShellIntegration.cs` içinde `Enqueue`, `SirayaEkle` araması boş | tuttu |
| 184 | OYB-34 | Kabuk kaydı 24 uzantıda: 23 video ve gif. | `src/VidShrink.Core/ShellIntegration.cs:11-15`, `src/VidShrink.Core/Setup/ShellRegistration.cs:257-261` | tuttu |
| 185 | OYB-15 | Yer imleri adsız zaman değerleri. | `src/VidShrink.App/Playback/PlaybackHistory.cs:145-158` | tuttu |
| 186 | OYB-16 | Sık kullanılanlar yok. | `src/VidShrink.App/Playback` içinde `Favori`, `Favorite` araması boş | tuttu |
| 187 | OYB-30 | macOS ortam denetimi alanına bağlanan kod yok. | kaynakta `NowPlaying`, `MPRemoteCommand` araması boş | tuttu |
| 188 | OYA-44, ARC-07 | Küçük resim tablosu yok. | kaynakta `tile=`, `ContactSheet`, `KucukResimTablosu` araması boş | tuttu |
| 189 | OYA-53 | Oynatıcıda dokunmatik hareket yok. | `src/VidShrink.App/Playback` içinde `Gestures.`, `PinchEvent`, `ScrollGesture` araması boş | tuttu |
| 190 | DZA-27 | Akıllı kip dört kodekle sınırlı. | `src/VidShrink.Core/Editing/SmartCutCodec.cs:43` | tuttu |
| 191 | DZB-41 | Düzenleyicide ses düzeyi göstergesi yok. | `src/VidShrink.App/Editing` ve `src/VidShrink.Core/Editing` içinde `Meter`, `Gosterge`, `PeakLevel`, `astats`, `ebur128` araması boş | tuttu |
| 192 | DZB-64 | Kaynak doğrudan çizelgenin sonuna ekleniyor; medya havuzu yok. | `src/VidShrink.App/Editing/EditorView.Kaynak.cs:96-97`, `src/VidShrink.Core/Editing/EditTimeline.cs:59` | tuttu |
| 193 | DZB-08 | Aralık silme var; ekleme ve üstüne yazma kipi yok. | `src/VidShrink.Core/Editing/EditTimeline.cs:148`; aynı dosyada `Overwrite`, `UstuneYaz` araması boş | tuttu |
| 194 | SIK-09 | Kuyruk sonu seçenekleri: hiçbir şey, klasörü aç, uyut, kapat. Uygulamadan çıkış yok. | `src/VidShrink.App/QueueEndActions.cs:9-15` | tuttu |
| 195 | SIK-08 | Bekleyen iş yalnız çıkarılıp taşınabiliyor. | `src/VidShrink.Core/JobPump.cs:60`, `:66`, `:76`, `:88` | tuttu |
| 196 | SIK-11 | Kaynak dosyanın tarihi çıktıya taşınmıyor. | `src/VidShrink.Core`, `src/VidShrink.Ffmpeg` ve `src/VidShrink.App` içinde `SetLastWriteTime`, `SetCreationTime` araması boş | tuttu |
| 197 | SIK-18 | Çıktı kısmi dosyadan asıl ada taşınıyor; ardından çıktıyı yoklayan çağrı yok. | `src/VidShrink.Ffmpeg/EncodeRunner.cs:463`; dosyada `Probe` geçen satırlar girdi içindir (`:235-238`) | tuttu |
| 198 | EKA-11 | Boş alan denetimi yalnız Küçült'te ve komut satırında. | `src/VidShrink.App/MainWindow.axaml.cs:4514`, `src/VidShrink.Cli/CliApp.cs:519`; kaydedici kaynağında `DiskSpace`, `FreeSpace` araması boş | tuttu |
| 199 | EKA-20 | Yarım kalan kaydı açılışta arayan kod yok. | kaydedici kaynağında `Recover`, `Kurtar`, `Orphan`, `YarimKalan` araması boş | tuttu |
| 200 | EKA-07, EKB-18 | Kamera katmanı yalnız Windows'ta. | `src/VidShrink.App/Recorder/RecorderView.Kamera.cs:53`, `src/VidShrink.Core/RecorderArguments.cs:894-895` | tuttu |
| 201 | EKA-13 | Genel kısayollar yalnız Windows'ta. | `src/VidShrink.App/Recorder/RecorderView.Kisayol.cs:29-31` | tuttu |
| 202 | EKA-09 | Bas-konuş yok. | kaynakta `PushToTalk`, `BasKonus` araması boş | tuttu |
| 203 | EKB-26 | Kayıt adı sabit önek ve tarih damgası; çakışmada sıra numarası. | `src/VidShrink.App/Recorder/RecorderSettings.cs:427-436` | tuttu |
| 204 | AYR-02 | Açılış teması koyu olarak sabit. | `src/VidShrink.App/App.axaml:5` | tuttu |
| 205 | AYR-15 | Canlandırma izni Windows dışında hep açık dönüyor. | `src/VidShrink.App/MainWindow.axaml.cs:377-379` | tuttu |
| 206 | AYR-19 | İş bitti bildirimi yalnız Windows'ta. | `src/VidShrink.App/IsBittiBildirimi.cs:63-67` | tuttu |
| 207 | AYR-20 | Uygulamanın çaldığı tek ses kaydedicinin tıklama sesi. | `src/VidShrink.App/Recorder/RecorderInput.cs:151` | tuttu |
| 208 | AYR-21 | Düşük öncelik yalnız yardımcı süreçlerde ve güncelleme başlatıcısında veriliyor. | `src/VidShrink.Core/Editing/ThumbnailQueue.cs:276`, `src/VidShrink.Core/Editing/SilenceCut.cs:215`, `src/VidShrink.Core/Editing/AudioPeaks.cs:157`, `src/VidShrink.Ffmpeg/AudioLevelSource.cs:88`, `src/VidShrink.App/Program.cs:220` | tuttu |
| 209 | AYR-18 | Ana pencere yalnız Esc tuşunu dinliyor. | `src/VidShrink.App/MainWindow.PencereKipi.cs:39-42` | tuttu |
| 210 | AYR-08 | Ayarlarda arama yok. | `src/VidShrink.App/MainWindow.axaml` içinde `Search` araması boş | tuttu |
| 211 | DAG-17 | Yüklü uygulamalar kaydı yazılmıyor. | kaynakta `UninstallString` araması boş | tuttu |
| 212 | DAG-19 | Kaldırma menüleri, ilişkiyi, kısayolları ve kurulum kökünü siliyor; ayar klasörüne dokunmuyor. | `src/VidShrink.Core/Setup/SetupRunner.cs:216-241` | tuttu |
| 213 | DAG-28 | Windows kurucusu komutu arama yoluna yazmıyor. | `src/VidShrink.Core/Setup` içinde `"Path"`, `EnvironmentVariableTarget` araması boş | tuttu |
| 214 | DAG-29 | İlerleme yerelleştirilmiş metin olarak hata akışına yazılıyor. | `src/VidShrink.Cli/CliApp.cs:881-890` | tuttu |
| 215 | DAG-33 | Ön ayarı dışa yazan işlev çekirdekte hazır, komutu yok. | `src/VidShrink.Core/PresetLibrary.cs:228`, `src/VidShrink.Cli/CliRequest.cs:99-106` | tuttu |
| 216 | DAG-34 | Kabuk tamamlama dosyası yok. | depoda adı `completion` ya da `tamamla` geçen dosya yok | tuttu |
| 217 | DAG-37 | Kabuk eklentisinin alt komut kolu boş. | `src/VidShrink.ShellExtension/VidShrink.ShellExtension.cpp:136-141` | tuttu |
| 218 | ARC-08 | Yalnız tek kare kaydı ve kareyi panoya kopyalama var. | `src/VidShrink.App/Playback/Keymap.cs:73-74` | tuttu |
| 219 | OYA-27, OYA-33 | Altyazı biçemi yazı tipi, renk, kenar, gölge ve arka planı yazıyor; biçemli altyazıyı ezme ve işitme engelli notlarını ayıklama ayarı yok. | `src/VidShrink.Player/MpvEngine.Advanced.cs:115-122`; kaynakta `sub-ass-override`, `sub-filter-sdh` araması boş | tuttu |
| 220 | OYA-47 | Elle yakınlaştırma ve yüzde 20'ye kadar kırpma var; bantları kendiliğinden bulan kod yok. | `src/VidShrink.App/Playback/Keymap.cs:109`, `src/VidShrink.Player/AdvancedSettings.cs:15-17`; oynatıcı kaynağında `panscan`, `cropdetect` araması boş | tuttu |
| 221 | SIK-12 | Küçült'te otomatik siyah bant kırpma var; kırpma alanını elle çizen yüz yok. | `src/VidShrink.App/MainWindow.axaml:1067`, `src/VidShrink.App/MainWindow.OtomatikKirpma.cs:20`; düzenleyici ve kaydedici dışında `CropOverlay`, `ManualCrop` araması boş | tuttu |
| 222 | EKB-33 | Bitiş eylemi dört seçenekten biri: hiçbir şey, dosyayı kopyala, küçült ve kopyala, yükle ve bağlantıyı kopyala. | `src/VidShrink.App/Recorder/RecorderView.Bitince.cs:16-22` | tuttu |
| 223 | EKB-22 | Sonuç panelindeki tek kopyala düğmesi bağlantıyı kopyalıyor. | `src/VidShrink.App/Recorder/RecorderView.axaml:282-288` | tuttu |
| 224 | EKB-23 | GIF kabı kayıttan önce seçiliyor; yakalama önce MKV'ye yazılıyor. | `src/VidShrink.Core/RecorderArguments.cs:467-468` | tuttu |
| 225 | EKB-27 | Kaydedicide yükleme hedefini seçtiren kutu yok; tablonun varsayılanı kullanılıyor. | `src/VidShrink.App/Recorder/RecorderView.Paylas.cs:19-21` | tuttu |
| 226 | EKB-19 | En-boy ön ayarı düzenleyicide var, kaydedicide yok. | `src/VidShrink.App/Editing/EditorView.Klip.cs:17`; kaydedici kaynağında `AspectPreset`, `EnBoy` araması boş | tuttu |
| 227 | EKA-06 | Kayıt hedefi ekran, pencere ya da bölge; aygıt kaynağı yok. | `src/VidShrink.Core/RecorderArguments.cs:24-34` | tuttu |
| 228 | AYR-10 | "Günlüğü aç" dosyayı dosya yöneticisinde gösteriyor, içeriğini açmıyor. | `src/VidShrink.App/MainWindow.axaml.cs:1511-1521` | tuttu |
| 229 | AYR-27 | Tek günlük dosyası var; tanılama paketi üreten kod yok. | `src/VidShrink.Core/Gunluk.cs:28`; `src/VidShrink.App` ve `src/VidShrink.Core` içinde `Tanilama`, `ZipFile.CreateFrom` araması boş | tuttu |
| 230 | AYR-43 | Kısayol tablosunda arama yok. | `src/VidShrink.App/Playback/ShortcutTable.cs` içinde `Filter`, `Search`, `Where(` araması boş | tuttu |
| 231 | AYR-16 | Uygulama genelinde 283 erişilebilirlik adı ataması var; deneme kaydı yok. | kaynakta `AutomationProperties.` sayımı 283 (252 arayüz dosyasında, 31 kodda); `docs/olcumler` altında adı ekran okuyucu ya da klavye geçen dosya yok | tuttu |
| 232 | AYR-17 | Klavye gezinme sırası elle verilmemiş. | `TabIndex`, `KeyboardNavigation`, `IsTabStop` adları yalnız sekme numarası tutan alanlarda ve bir yorumda geçiyor (`src/VidShrink.App/Playback/PlayerInputMap.cs:170`, `src/VidShrink.App/Themes/Controls.axaml:1010`); gezinme özelliği olarak hiçbir yerde yok | tuttu |
| 233 | AYR-14 | Canlandırmayı elle kapatan anahtar yok. | kaynakta `ReduceMotion`, `DisableAnimations`, `CanlandirmaKapali` araması boş | tuttu |
| 234 | AYR-06 | Yenilikler ekranı yok. | `src/VidShrink.App` içinde `WhatsNew`, `Yenilikler`, `ReleaseNotes`, `Changelog` araması boş | tuttu |
| 235 | AYR-11, AYR-29 | Lisans ve gizlilik metni yalnız README'de; Hakkında dizgilerinde ikisi de yok. | `README.md:404-405`, `:413-414`; `src/VidShrink.App/Locales/en/main.json` içinde `licen`, `privacy`, `third-party` araması boş | tuttu |
| 236 | AYR-26, AYR-33 | Ana pencerede F1 ve F6 dinlenmiyor; F6 kaydedicide bölüm işareti kısayolu. | `src/VidShrink.App/Recorder/RecorderHotkeys.cs:42`, `src/VidShrink.App/Playback/Keymap.cs:153`; ana pencere için 209. satır | tuttu |
| 237 | AYR-07 | Depoda hata bildirme şablonu yok. | `.github` altında yalnız `workflows` var | tuttu |
| 238 | DAG-16 | Kurucuda adı sessiz olan bayrak yok; panelsiz kurulum ve açmadan bitirme bayrağı var. | `src/VidShrink.Setup/Program.cs:98`, `:104`, `src/VidShrink.Core/Setup/SetupPanel.cs:256`; kaynakta `--silent`, `--quiet`, `--sessiz` araması boş | tuttu |
| 239 | ARC-15 | Bölümler dönüştürmede taşınıyor, kaydedici kendi çıktısına yazıyor; var olan dosyanın bölümlerini düzenleyen yüz yok. | `src/VidShrink.Core/StreamMapping.cs:302`, `src/VidShrink.Core/RecorderChapters.cs:93-116` | tuttu |
| 240 | OYB-21 | Uyarlamalı yayında kalite katmanı elle seçilemiyor; adres motora seçeneksiz gidiyor. | `src/VidShrink.Player/MpvEngine.cs:125-128`; kaynakta `hls-bitrate` araması boş | tuttu |
| 241 | OYB-43 | Kesintisiz çalma yok; kuyruk motora verilmiyor, her dosya ayrı yükleniyor. | `src/VidShrink.App/Playback/PlayerView.Kuyruk.cs:13`; kaynakta `gapless`, `prefetch-playlist` araması boş | tuttu |
| 242 | DZA-15 | Kesim listesi CSV, EDL ya da bölüm metni olarak dışa verilemiyor; en yakın kod mpv önizleme listesi. | `src/VidShrink.Core/Editing/EdlPreview.cs:17`; kaynakta `".csv"`, `".edl"`, `.cue` araması boş | tuttu |
| 243 | DZA-16 | Kesim listesi içe alınamıyor; okunan tek biçim kendi proje dosyamız. | `src/VidShrink.Core/Editing/EditProject.cs:43` | tuttu |
| 244 | DZA-17 | Kurgu programlarının açtığı XML yazılmıyor. | `src/VidShrink.Core/Editing/EditProject.cs:43`; kaynakta `fcpxml`, `xmeml` araması boş | tuttu |
| 245 | DON-29 | Dönüştür'de başlık, sanatçı, tarih yazılamıyor; yalnız "meta veriyi sil" kutusu var. | `src/VidShrink.App/MainWindow.axaml:1357`; kaynakta dosya düzeyinde `"-metadata"` araması boş | tuttu |
| 246 | DON-30 | Bölümler yalnız Küçült yolunda taşınıyor; Dönüştür'de bölüm seçeneği, hiçbir yolda adlandırma yok. | `src/VidShrink.Core/StreamMapping.cs:302`; `-map_chapters` dönüştürme argümanlarında geçmiyor | tuttu |
| 247 | DON-31 | İz dili ve bayrakları yalnız Küçült yolunda kaynaktan taşınıyor; düzenleme yüzü yok, Dönüştür hiç yazmıyor. | `src/VidShrink.Core/StreamMapping.cs:282-294`, `:345-348`; `-metadata:s:` dönüştürme argümanlarında geçmiyor | tuttu |
| 248 | DZA-24 | Düzenleyicide üst veri yalnız topluca silinebiliyor. | `src/VidShrink.App/Editing/EditorView.Teslim.cs:81-85` | tuttu |
| 249 | DZA-11 | İz kaydırma teslimde yok; gecikme yalnız oynatıcıda, çalarken geçerli. | `src/VidShrink.App/Playback/PlayerView.Tracks.cs:104-108`, `src/VidShrink.Core/Editing/EditExport.cs:541-542`; kaynakta `itsoffset` araması boş | tuttu |
| 250 | DZA-25 | Düzenleyicide başka dosyadan iz eklenemiyor; dış altyazı yalnız Küçült yolunda, çok kaynak yalnız uç uca. | `src/VidShrink.Core/StreamMapping.cs:805-808`, `src/VidShrink.Core/Editing/EditTimeline.cs:53` | tuttu |
| 251 | DZA-26 | İzler yeniden kodlamadan ayrı dosyaya çıkarılamıyor; yalnız ses kabı sesi hep yeniden kodluyor. | `src/VidShrink.App/MainWindow.axaml.cs:4782-4788`, `src/VidShrink.Core/ConversionArguments.cs:244` | tuttu |
| 252 | DZA-23 | Döndürme yalnız süzgeçle yapılıyor, yeniden kodluyor; kap bayrağıyla döndürme yok. | `src/VidShrink.Core/Editing/ClipFilters.cs:50-51`; kaynakta `display_rotation` araması boş | tuttu |
| 253 | DZB-20 | Metnin konumunu model tutuyor, arayüzde konum denetimi yok. | `src/VidShrink.Core/Editing/TextLayer.cs:92`, `src/VidShrink.App/Editing/EditorView.axaml:95-109` (içerik, boy, renk) | tuttu |
| 254 | DZB-24 | Konum anahtar karesini model ve yazıcı destekliyor, arayüzden verilemiyor. | `src/VidShrink.Core/Editing/AssWriter.cs:196-197`; arayüz klasöründe `TextKeyframe` araması boş | tuttu |
| 255 | DZB-23 | Metinde hazır hareket yok; yalnız giriş ve çıkış solması var. | `src/VidShrink.Core/Editing/TextLayer.cs:53-55` | tuttu |
| 256 | DZB-62 | Metin izi .ass olarak yazılıyor; altyazı dosyasından içe alma yok. | `src/VidShrink.Core/Editing/AssWriter.cs:113-126`; düzenleyici klasörlerinde `.srt`, `.vtt` araması boş | tuttu |
| 257 | DZB-04 | Resim klibi yok; süresi okunamayan kaynak reddediliyor. | `src/VidShrink.App/Editing/EditorView.Kaynak.cs:89-93` | tuttu |
| 258 | DZB-05 | Düz renk klibi yok. | Kaynakta `ColorClip`, `color=c=` araması boş; `SolidColor` yalnız fırça olarak geçiyor (`src/VidShrink.App/KullaniciRengi.cs:13`) | tuttu |
| 259 | DZB-03 | Klip dönüşümü yarım: kırpma, 90 derece döndürme ve çevirme var, serbest ölçek ve konum yok. | `src/VidShrink.Core/Editing/ClipEffects.cs:15-27` | tuttu |
| 260 | DZB-56 | Proje düzeyinde tuval oranı yok; yalnız klip başına oran kırpması var. | `src/VidShrink.Core/Editing/ClipEffects.cs:96` | tuttu |
| 261 | DZB-49 | Kare dondurma yok. | Kaynakta `FreezeFrame` araması boş; `Freeze` yalnız oynatıcı panelinde ve boşta kırpmada | tuttu |
| 262 | DZB-36 | Eğriler ve renk tekerleri yok. | Kaynakta `curves`, `colorbalance` araması boş | tuttu |
| 263 | DZB-38 | Otomatik renk ve renk eşleme yok. | Kaynakta `colormatch`, `histeq`, `AutoColor` araması boş | tuttu |
| 264 | DZB-28 | Bulanıklık ve bölge gizleme yok. | Kaynakta `gblur`, `boxblur` araması boş | tuttu |
| 265 | DZB-25 | Klip ayarları klip boyunca sabit; anahtar kare yok. | `src/VidShrink.Core/Editing/ClipEffects.cs:15-35` | tuttu |
| 266 | DZB-48 | Hız rampası yok; klip başına sabit hız ve ters oynatma var. | `src/VidShrink.Core/Editing/EditTimeline.cs:402`, `:424`, `:328` | tuttu |
| 267 | DZB-50 | Ara kare üreten ağır çekim yok. | Kaynakta `minterpolate` araması boş | tuttu |
| 268 | DZB-26 | Metin konumu iki nokta arasında yalnız doğrusal; yumuşatma eğrisi yok. | `src/VidShrink.Core/Editing/TextLayer.cs:91-92` | tuttu |
| 269 | ARC-10 | Slayt gösterisi yapılamıyor; bırakma listesi yalnız video uzantıları, düzenleyicide geçiş yok. | `src/VidShrink.Core/WatchFolder.cs:74`, `src/VidShrink.Core/DroppedMedia.cs:24`; düzenleyici çekirdeğinde `xfade`, `Transition` araması boş | tuttu |
| 270 | ARC-20 | Sesten video yok; en yakın kod düzenleyicinin dalga biçimi verisi. | `src/VidShrink.Core/Editing/AudioPeaks.cs:9`; kaynakta `showwaves` araması boş | tuttu |
| 271 | OYA-31 | Altyazı kapalı açılıyor; zorunlu altyazıyı kendiliğinden gösteren ayar yok. | `src/VidShrink.Player/MpvEngine.cs:166`; oynatıcı kodunda `sub-forced`, `slang`, `alang` araması boş | tuttu |
| 272 | OYA-51 | Taramalı görüntü düzeltmesi yalnız elle açılıp kapanıyor. | `src/VidShrink.Player/MpvEngine.Advanced.cs:97` | tuttu |
| 273 | OYB-08 | Oynatıcı kuyruğu bellekte duruyor; liste yalnız elle kaydediliyor. | `src/VidShrink.App/Playback/PlayerView.Kuyruk.cs:17`, `src/VidShrink.App/Playback/PlayerView.ListeKaydet.cs:44` | tuttu |
| 274 | SIK-05 | Küçült kuyruğu yalnız bellekte tutuluyor. | `src/VidShrink.Core/JobPump.cs:60`, `src/VidShrink.App/ShrinkJobWindow.axaml.cs:175`; kaynakta kuyruğu saklayan bir ad araması boş | tuttu |
| 275 | EKA-08 | Motor 10 bit biçimi ve bt2020 etiketini tanıyor; yakalama 8 bit bgra, HDR aktarım etiketi yok. | `src/VidShrink.Core/RecorderArguments.cs:389`, `:403`, `:1118`; aynı dosyada `smpte2084`, `arib`, `color_trc` araması boş | tuttu |
| 276 | EKB-24 | Kayıt sonucu kaydedici sekmesindeki panelde görünüyor; küçük resim ve sürükleyip bırakma yok. | `src/VidShrink.App/Recorder/RecorderView.axaml:193`; kaydedici klasöründe `DoDragDrop`, `Thumbnail` araması boş | tuttu |
| 277 | DZB-18 | Hazır geçiş çeşitleri yok. | kaynakta `xfade` araması boş | tuttu |
| 278 | DZB-29 | Yeşil perde yalnız kaydedicinin kamerasında. | `src/VidShrink.Core/RecorderArguments.cs:928`; `chromakey` başka yerde çıkmadı | tuttu |
| 279 | DZB-51 | Vekil dosya yok; "Proxy" yalnız ProRes kodek adında geçiyor. | `src/VidShrink.Core/IntermediateCodecs.cs:33`, `src/VidShrink.App/MainWindow.axaml:1155` | tuttu |
| 280 | DZB-22 | Hazır başlık şablonu ve alt bant yok. | kaynakta `TitleTemplate`, `LowerThird` araması boş | tuttu |
| 281 | DZB-52 | Proje şablonu yok. | kaynakta `ProjectTemplate` araması boş | tuttu |
| 282 | DZB-53 | Hazır varlık kitaplığı yok. | kaynakta `AssetLibrary`, `Sticker` araması boş | tuttu |
| 283 | DZB-44 | Modelli konuşma temizleme yok. | kaynakta `arnndn` araması boş | tuttu |
| 284 | DZB-69 | Elle oran kırpması var, özne takibi yok. | `src/VidShrink.Core/Editing/ClipEffects.cs:96`; kaynakta `MotionTrack`, `ObjectTrack` araması boş | tuttu |
| 285 | DZB-70 | Arka plan silme ve nesne maskesi yok. | kaynakta `Segmentation`, `rembg` araması boş | tuttu |
| 286 | DZB-71 | Hareket izleme yok. | kaynakta `MotionTrack`, `ObjectTrack` araması boş | tuttu |
| 287 | DZB-72 | Metinden sese yok. | kaynakta `TextToSpeech`, `SpeechSynth` araması boş | tuttu |
| 288 | DON-32 | Modelle büyütme yok. | kaynakta `esrgan`, `waifu`, `onnx`, `ncnn` araması boş | tuttu |
| 289 | EKA-14 | Gürültü için iki süzgeç var (`agate`, `afftdn`), model yok. | `src/VidShrink.Core/AudioCaptureArguments.cs:132`, `:135`; kaynakta `arnndn` araması boş | tuttu |
| 290 | EKB-28 | Yükleme hedefi tablosu önce kullanıcının kendi kopyasından okunuyor; Ayarlar'da hazır hedefler arasından seçim şeridi var. Özel hedef ekleme arayüzünün yokluğu ayrıca aranmadı. | `src/VidShrink.Core/Share/ShareTargets.cs:188-190`, `src/VidShrink.Core/Share/IShareProvider.cs:66`, `src/VidShrink.App/MainWindow.axaml:1781` | tuttu |
| 291 | EKB-29 | Hazır tabloda iki servis var (storage.to, uguu.se); kullanıcının kendi bulut deposu yok. | `src/VidShrink.Core/Share/ShareTargets.cs:101-125`; kaynakta `S3`, `AccessKey`, `amazonaws` araması boş | tuttu |
| 292 | AYR-09 | Sürüm numarasındaki ön sürüm eki ayrıştırılıyor; kanal seçimi yok. | `src/VidShrink.Core/UpdateCheck.cs:286`; kaynakta `UpdateChannel`, `beta`, `nightly` araması boş | tuttu |
| 293 | DAG-24 | Ön sürüm ya da gecelik güncelleme kanalı yok. | `src/VidShrink.Core/UpdateCheck.cs:286`; kaynakta `UpdateChannel`, `beta`, `nightly` araması boş | tuttu |
| 294 | AYR-04 | 42 dil klasörü var; çeviri katkısının yolu yalnız sorun açmak. | `src/VidShrink.App/Localization/Strings.cs:62`, `src/VidShrink.App/Locales` (42 klasör), `README.md:348` | tuttu |
| 295 | AYR-25 | Diller listede eşit duruyor; insan gözünden geçmemiş çeviri işaretlenmiyor. | `src/VidShrink.App/Localization/Strings.cs:62`; aynı klasörde `Reviewed`, `machine`, `verified` araması boş | tuttu |
| 296 | AYR-23 | İlk açılışta yalnız kaydedici sihirbazı ve varsayılan uygulama önerisi var. | `src/VidShrink.App/Recorder/RecorderSettings.cs:271`, `src/VidShrink.App/MainWindow.axaml.cs:368` | tuttu |
| 297 | AYR-24 | Kullanım belgesi iki dilde depoda ve README'den bağlı. | `docs/kullanim.md`, `docs/kullanim.tr.md`, `README.md:373` | tuttu |
| 298 | DAG-08 | Scoop paketi yok. | izlenen dosyalarda `scoop` yalnız bir çeviri dosyasında geçiyor | tuttu |
| 299 | DAG-12 | Chocolatey paketi yok. | izlenen dosyalarda `choco` araması boş | tuttu |
| 300 | DAG-09 | AppImage yok. | izlenen dosyalarda `AppImage` araması boş | tuttu |
| 301 | DAG-05 | macOS paketi yalnız ad-hoc imzalı. | `macos-app-bundle.sh:117-118` | tuttu |
| 302 | DAG-06 | macOS disk görüntüsü üretilmiyor. | `dmg`, `hdiutil` yalnız `.github/workflows/handbrake-kiyas.yml` içinde geçiyor | tuttu |
| 303 | DAG-07 | Homebrew cask yok. | `cask`, `brew` yalnız libmpv iş akışlarında ve kök `AGENTS.md` içinde geçiyor | tuttu |
| 304 | DAG-03 | Microsoft Store paketi yok; `appx` yalnız sağ tık menüsünün yerinde kaydedilen paketi. | `Install-VidShrink.ps1:511`, `.github/workflows/release.yml:283`, `src/VidShrink.ShellExtension/AppxManifest.template.xml` | tuttu |
| 305 | ARC-11 | Resim yalnız çıktı olarak üretiliyor (hareketli WebP, AVIF). | `src/VidShrink.Core/ConversionPlan.cs:34` | tuttu |
| 306 | ARC-02 | Kalite ölçer iki serbest dosya yolu alıyor; iki dosyayı ölçen komut ölçüm aracında. | `src/VidShrink.Ffmpeg/QualityMeter.cs:179`, `tools/VidShrink.Bench/Program.cs:83` | tuttu |
| 307 | ARC-06 | Kare sağlaması yok; en yakın kod altyazı araması için dosya özeti. | `src/VidShrink.Core/Subtitles/MovieHash.cs:13`; kaynakta `framemd5` araması boş | tuttu |
| 308 | ARC-23 | Altyazı çevirisi yok; `Translate` yalnız HandBrake ön ayar aktarımında ve X11 koordinat çağrısında geçiyor. | `src/VidShrink.App/MainWindow.OnAyar.cs:342`, `src/VidShrink.App/Recorder/RecorderWindowsX11.cs:81`, `src/VidShrink.App/MainWindow.axaml.cs:1818` | tuttu |
| 309 | ARC-33 | Logo silme yok; en yakın kod Küçült'ün görüntü süzgeçleri. | `src/VidShrink.Core/VideoFilterChain.cs:65`, `src/VidShrink.App/MainWindow.axaml:1030`; kaynakta `delogo` araması boş | tuttu |
| 310 | ARC-25 | Vokal ayırma yok; en yakın kod sesi tek dosya olarak çıkarma. | `src/VidShrink.Core/ConversionPlan.cs:32`; kaynakta `demucs`, `spleeter` araması boş | tuttu |
| 311 | ARC-27 | Yalnız klasik ölçekleme var, model yok. | `src/VidShrink.Core/ConversionArguments.cs:232`; kaynakta `esrgan`, `onnx`, `ncnn` araması boş | tuttu |
| 312 | ARC-29 | Arka plan silme yok. | `src/VidShrink.Core/VideoFilterChain.cs:59`; kaynakta `Segmentation`, `rembg` araması boş | tuttu |
| 313 | ARC-30 | Yalnız siyah bant algılama var, konu izleme yok. | `src/VidShrink.Ffmpeg/CropProbe.cs:15`; kaynakta `MotionTrack`, `ObjectTrack` araması boş | tuttu |
| 314 | ARC-31 | Yüz bulanıklaştırma yok. | çekirdekte `facedetect`, `boxblur`, `Blur` araması boş | tuttu |
| 315 | ARC-18 | Yinelenen video bulucu yok; en yakın kod dosya özeti. | `src/VidShrink.Core/Subtitles/MovieHash.cs:13`; kaynakta `phash`, `PerceptualHash` araması boş | tuttu |
| 316 | ARC-22 | Düzenleyicide yalnız yazı katmanı var; altyazı dosyası açılıp düzenlenmiyor. | `src/VidShrink.App/Editing/EditorView.Metin.cs:15`; düzenleyici klasöründe `SubRip`, `.srt` araması boş | tuttu |
| 317 | OYB-31 | Linux ortam denetimi arayüzü (MPRIS) yok. | kaynakta `mpris` araması boş | tuttu |
| 318 | OYB-35 | İkinci açılış hep ilk örneğe aktarılıyor; yeni pencere seçeneği yok. | `src/VidShrink.Core/SingleInstanceChannel.cs:10`, `src/VidShrink.App/Program.cs:163` | tuttu |
| 319 | OYB-39 | Uzaktan denetim yok. | kaynakta `HttpListener`, `WebSocket`, `TcpListener` araması boş | tuttu |
| 320 | OYB-40 | Komut satırı yalnız dosya ya da adres açıyor. | `src/VidShrink.App/Program.cs:152` | tuttu |
| 321 | OYB-41 | Betik ve eklenti sistemi yok; `load-scripts=no` yazılmış. | `src/VidShrink.Player/MpvEngine.cs:161`; kaynakta `IPlugin`, `AssemblyLoadContext` araması boş | tuttu |
| 322 | OYB-09 | Liste yalnız m3u8 dosyası olarak dışarı kaydediliyor. | `src/VidShrink.App/Playback/PlayerView.ListeKaydet.cs:18` | tuttu |
| 323 | OYB-17 | Yerel kitaplık görünümü yok. | kaynakta `MediaLibrary`, `LibraryView` araması boş; `Kitaplik` yalnız libmpv yüklemesinin açıklamasında (`src/VidShrink.App/Program.cs:230`) | tuttu |
| 324 | OYB-24 | Ağ paylaşımı gezme yok; adres şemaları altı tane. | `src/VidShrink.Core/MediaAddress.cs:15`; kaynakta `smb:`, `sftp:`, `ftp:`, `webdav` araması boş | tuttu |
| 325 | DZB-10 | Klip sesi kapatılabiliyor ve düzeyi ayarlanıyor; ses ayrı parça olmuyor. | `src/VidShrink.Core/Editing/ClipEffects.cs:29`, `:31` | tuttu |
| 326 | DZB-46 | Otomatik müzik kısma yok. | kaynakta `sidechaincompress`, `Ducking` araması boş | tuttu |
| 327 | DZB-47 | Seslendirme kaydı yok. | kaynakta `Voiceover`, `Seslendirme` araması boş | tuttu |
| 328 | DZB-06 | Çizelgede üç sabit iz var: yazı, video, ses. | `src/VidShrink.App/Editing/EditorTimeline.cs:539-541` | tuttu |
| 329 | DZB-07 | Klipler sıralı liste; taşıma yalnız sırayı değiştiriyor. | `src/VidShrink.Core/Editing/EditTimeline.cs:134` | tuttu |
| 330 | SIK-14 | Arayüzde kalite örnek pencerelerde ölçülüyor; biten çıktının tamamını komut satırı `--vmaf` ile ölçüyor. | `src/VidShrink.App/MainWindow.axaml.cs:3639`, `src/VidShrink.Cli/CliRequest.cs:611` | tuttu |
| 331 | SIK-15 | Hedef kalite, örnek ölçümüyle beslenen tahminden çözülüyor. | `src/VidShrink.Core/PlanCalculator.cs:1170` | tuttu |
| 332 | SIK-16 | İzinli kodek listesinde VVC yok. | `src/VidShrink.Core/PlanParser.cs:13`; kaynakta `vvc`, `libvvenc`, `h266` araması boş | tuttu |
| 333 | SIK-19 | Dolby Vision yalnız libx265 ve libsvtav1 ile taşınıyor. | `src/VidShrink.Core/HdrResolver.cs:46`, `:179` | tuttu |
| 334 | EKA-05 | Oyuna özel yakalama yolu yok; "Oyun" sihirbazın bir seçimi. | `src/VidShrink.Core/RecorderArguments.cs:24`, `src/VidShrink.Core/RecorderWizard.cs:5` | tuttu |
| 335 | EKB-13 | Büyüteç imlecin yanında duran bir mercek ve yalnız Windows'ta. | `src/VidShrink.App/Recorder/RecorderMagnifier.cs:29-32`, `src/VidShrink.App/Recorder/RecorderView.Girdi.cs:38-42` | tuttu |
| 336 | EKB-31 | Hareket bulanıklığı yok. | kaynakta `tblend`, `tmix`, `MotionBlur` araması boş | tuttu |
| 337 | EKB-20 | Kayıtta bildirim susturma yok. | kaynakta `DoNotDisturb`, `QuietHours`, `FocusAssist` araması boş | tuttu |
| 338 | EKB-21 | Masaüstü simgelerini gizleme yok. | kaynakta `DesktopIcons`, `Progman`, `SHELLDLL_DefView` araması boş | tuttu |
| 339 | EKB-14 | Girdi efektleri yalnız Windows'ta; öbür sistemlerde boş dinleyici. | `src/VidShrink.App/Recorder/RecorderView.Girdi.cs:14-22` | tuttu |
| 340 | AYR-22 | Tepsi simgesi yalnız kaydedicide. | `src/VidShrink.App/Recorder/RecorderTray.cs:14`; `TrayIcon` başka dosyada çıkmadı | tuttu |
| 341 | AYR-12 | Yazı boyutları sabit belirteç; pencere en az 1136×720. | `src/VidShrink.App/Themes/Theme.axaml:219`, `src/VidShrink.App/MainWindow.axaml:12` | tuttu |
| 342 | AYR-13 | Palet adları sabit liste; sistem karşıtlık ayarı okunmuyor. | `src/VidShrink.App/Themes/PaletteCatalog.cs:51`; kaynakta `HighContrast`, `PlatformSettings` araması boş | tuttu |
| 343 | DAG-10 | Taşınabilir kip yok; ayar yolu yalnız ölçüm için ortam değişkeniyle değişiyor. | `src/VidShrink.Core/UpdateCheck.cs:476-481`; kaynakta `Portable` araması boş | tuttu |
| 344 | DAG-11 | Kurucu ağdan indiriyor; yerel klasör kaynak olarak verilebiliyor. | `src/VidShrink.Setup/Program.cs:89-92`, `src/VidShrink.Core/Setup/SetupDownloads.cs:97` | tuttu |
| 345 | DAG-20 | macOS ve Linux kurucusu ffmpeg yoksa paket komutunu söyleyip duruyor. | `install-vidshrink.sh:232-236`, `docs/kurulum.md:83-86` | tuttu |
| 346 | DAG-36 | Betik ve eklenti arabirimi yok. | `src/VidShrink.Player/MpvEngine.cs:161`; kaynakta `IPlugin`, `PluginHost`, `ScriptEngine` araması boş | tuttu |
| 347 | PLT-37 | VideoToolbox yalnız açık seçimle geliyor; otomatik hızlı sıra onu adlandırmıyor, kalite ölçeği yok. | `src/VidShrink.Core/CodecModel.cs:197-214`, `src/VidShrink.Core/PlanCalculator.cs:1669` | tuttu |
| 348 | PLT-38 | VAAPI kolu kodda var, yalnız açık seçimle ulaşılıyor ve bu depoda hiç koşmadı. | `src/VidShrink.Core/CodecModel.cs:209-214`, `src/VidShrink.Core/PlanCalculator.cs:1670` | tuttu |
| 349 | PLT-43 | Linux'ta pulse `.monitor` kaynağı sistem sesi diye sınıflanıyor ama ölçülmedi; README yalnız Windows diyor. | `src/VidShrink.Ffmpeg/CaptureDevices.cs:59`, `:239`, `docs/olcumler/kaydedici-ses-seviyesi.md:53`, `README.md:254` | tuttu |
| 350 | PLT-06 | macOS'ta kamera katmanı yok: kamera paneli Windows dışında gizli, istek Windows dışında reddediliyor. | `src/VidShrink.App/Recorder/RecorderView.Kamera.cs:53`, `src/VidShrink.Core/RecorderArguments.cs:894-895` | tuttu |
| 351 | PLT-11 | macOS'ta tuş gösterimi yok: kutu sisteme bakmadan görünüyor, Windows dışında dinleyici boş. | `src/VidShrink.App/Recorder/RecorderView.Girdi.cs:20`, `src/VidShrink.App/Recorder/RecorderView.axaml:624`; `ChkShowKeys` için görünürlük ya da sistem koşulu araması boş | tuttu |
| 352 | PLT-24 | macOS ve Linux kurucusu ffmpeg'i indirmiyor; paket komutunu yazıp kurulumu yeniden başlatmayı söylüyor. | `install-vidshrink.sh:232-262` | tuttu |

## Okumanın Gösterdikleri

- 16. satır bir eksik değil, kusur: adres yapıştırma yolu kuyruk çözümüne giriyor, uzantıya bakan
  sınama adresi yerel liste sayıyor ve dosya okumaya çalışıyor. Adres testlerinde böyle bir örnek yok.
  Çalıştırılıp görülmedi.
- 1. satır bir gizlilik riski: süzgeç yok, yazılan her tuş kayda giriyor.
- 5. ve 21. satır belge bayatlığı: kod doğru, belge eski.
- 42. satırın kod yarısı okundu, ffmpeg yarısı okunmadı: ekranların kameralardan sonra numaralandığı bilgisi
  araştırma kolunun ffmpeg kaynağından çıkarımı. Gerçek bir Mac'te ölçülmedi; ölçülene kadar "olası kusur".
- 30. satır yarım kalmış bir yol: disk girdisi yalnız testte kuruluyor (`tests/VidShrink.Tests/BaslikTaramaTests.cs:189`),
  `src` içinde onu dolduran yer yok. Karar belgesi ile kod aynı şeyi söylemiyor.
- Tabloda satırı olmayan iki platform bulgusu da okundu ve tuttu. Güncelleme paketi yalnız Windows hedeflerinde
  üretiliyor: başlatıcı yalnız `win-` hedeflerine iniyor (`.github/workflows/release.yml:380-384`), paket de
  başlatıcı klasörü varsa kuruluyor (`:492-500`). Kaydedicinin kodlayıcı listesi sisteme göre süzülmüyor
  (89. satırla aynı yer); Mac'te de Windows'a özgü donanım kodlayıcıları seçilebilir görünüyor.
- 132. satırda doğrulama kolunun gösterdiği satır yanlış yeri gösteriyordu; iddia doğru çıktı ama yer tutmadı.
  Eksik araçlar kolunun doğrulayıcısı da kendi turunda beş hücrede aynı türden yer düzeltmesi bildirdi.
- 232. satırda doğrulama kolu üç ad için "0 eşleşme" yazmıştı. Düz arama eşleşme veriyor, ama hepsi sekme
  numarası tutan alanlar ve bir yorum; klavye gezinme özelliği olarak hiçbiri yok. İddianın özü tuttu, sayısı
  tutmadı.
- 231. satırdaki 283 sayısı aynen çıktı: arayüz dosyalarında 252, kodda 31.
- 258. satırda da aynı tür: kol üç ad için "aramaları boş" yazmıştı, `SolidColor` düz aramada çıkıyor ama
  hepsi arayüz fırçası; düz renk klibi yok. Özü tuttu, "boş" sözü tutmadı.
- 247. satırda kol "yalnız dil ve bayraklar taşınıyor" demişti; iz adı da taşınıyor
  (`src/VidShrink.Core/StreamMapping.cs:286`, `:292`). Eksik aynı: düzenleme yüzü yok, Dönüştür hiç yazmıyor.
- 254. satırda kolun gösterdiği satır (`EditorView.Metin.cs:191`) yokluğu göstermiyor, yazı tipi listesini
  kuruyor. Yokluk aramayla doğrulandı.

## Yok Denen Satırlardan Örneklem

Doğrulama turu hiçbir satırı "bizde zaten var" diye düşürmedi. Bu, gevşek bir doğrulamanın da işareti
olabileceği için ana oturum ayrıca denetledi: işi küçük, önceliği 2 ya da 3 olan ve "yok" denen 61 satırdan
14'ü rastgele çekildi (tohum 1310; o sırada doğrulaması bitmemiş iki kol dışarıda) ve her biri `src` içinde arandı.
O iki kolun doğrulaması bitince aynı ölçüye uyan 15 satırından 6'sı aynı tohumla çekilip tabloya eklendi.
Platform kolu örnekleme girmedi; onun satırları yukarıdaki tabloda doğrudan okundu.

| Satır | Aranan | Bulunan | Sonuç |
|---|---|---|---|
| OYB-36 | Görev çubuğunda ilerleme | eşleşen kod yok | yok çıktı |
| EKA-29 | Kayıt durum sesleri | eşleşen kod yok | yok çıktı |
| EKA-24 | Sabit ya da değişken kare hızı seçimi | eşleşen kod yok | yok çıktı |
| OYB-14 | Geçmişi temizleme | eşleşen kod yok | yok çıktı |
| SIK-36 | Film greni sentezi | eşleşen kod yok | yok çıktı |
| SIK-07 | Zamanlanmış başlatma | eşleşen kod yok | yok çıktı |
| DZB-11 | Klip kopyalama ve yapıştırma | yalnız bağlantı kopyalama (`EditorView.Teslim.cs:318-319`) | yok çıktı |
| SIK-24 | Başarısız işi yeniden deneme | yalnız hedef boyut tutmayınca çıkan panel (`MainWindow.axaml.cs:4616-4633`) | yok çıktı |
| DZA-32 | Yalnız anahtar kareleri tutma | eşleşen kod yok | yok çıktı |
| DZA-44 | Kapak resmi ekleme | yalnız kaynaktaki kapağı taşıyan eşleme (`StreamMapping.cs`) | yok çıktı |
| DAG-08 | Scoop paketi | depoda Scoop dosyası yok | yok çıktı |
| DZB-43 | Gürültü azaltma (düzenleyicide) | süzgeç yalnız kaydedicide (`AudioCaptureArguments.cs`) | yok çıktı |
| EKB-36 | Bağlantı kısaltma | eşleşen kod yok | yok çıktı |
| OYA-23 | Stereo indirgeme (oynatıcıda) | indirgeme yalnız kodlama yolunda | yok çıktı |
| ARC-13 | Resimlerden GIF | GIF yolu resim girdisi almıyor (`GifPalette.cs`) | yok çıktı |
| AYR-21 | Kodlama sürecine öncelik ayarı | öncelik yalnız yardımcı süreçlerde veriliyor (`SilenceCut.cs:215`, `SceneDetector.cs:123`) | yok çıktı |
| AYR-43 | Kısayol tablosunda arama | tabloda arama kutusu yok (`ShortcutTable.cs`) | yok çıktı |
| AYR-44 | Tek ayara giden bağlantı | eşleşen kod yok | yok çıktı |
| AYR-40 | Oturum açılınca başlatma | `src` ve `tools` içinde eşleşen kod yok | yok çıktı |
| AYR-26 | Yardım tuşu | F1 yalnız oynatıcıda başka bir işe bağlı (`Keymap.cs:153`) | yok çıktı |

Örneklem bir kanıt değil, bir yoklama: 20 satırın 20'si tuttu. Geri kalan "yok" satırları yalnız
doğrulama kolunun aramasına dayanıyor.

## Danışmanın Gösterdiği Dört Satır

Karar sorularını ayıklayan danışma da bir iddiadır. Cevabında dört yeri satır numarasıyla gösterdi; dördü de açıldı.

- `.github/workflows/ci.yml:198` — "CI libvmaf'ı yokluyor": tuttu. Adımın adı o satırda; hemen üstündeki not adımın derlemeyi düşürmediğini, yalnız bilgi verdiğini söylüyor.
- `src/VidShrink.App/MainWindow.axaml:13` — "pencere hep büyütülmüş açılıyor": tuttu, `WindowState="Maximized"` o satırda.
- `docs/kurulum.md:145` — "FFmpeg sürümle birlikte gelmez": tuttu; bir alt satır aynı şeyi libmpv için söylüyor.
- `src/VidShrink.App/Recorder/RecorderWindows.cs:83` — "uygulama Wayland'ı algılayıp X11 listesine düşüyor": **tutmadı.** Satır tersini yapıyor: Wayland oturumunda pencere listesi boş dönüyor, X11 listesi yalnız Wayland yoksa okunuyor. Pencere hedefi seçilirse kaydedici hata satırı gösteriyor (`src/VidShrink.App/Recorder/RecorderView.Secici.cs:45`). Danışmanın EKA-P14 için önerdiği sıra (önce ölçüm) bundan etkilenmiyor; yalnız "bugün ne oluyor" cümlesi yanlış.

## Ölçülen Tek Şey

Kaydedici kolu şunu yazdı: tek pencereyi yakalayan Windows arabirimi (WGC) artık ffmpeg'in içinde bir
süzgeç olarak geliyor. Pencere yakalama kararı (K3) rafta; bu iddia o kararın zeminini değiştirdiği için koşuldu.

Bu makinedeki ffmpeg, kurucunun pinlediği yapıyla aynı aileden (`src/VidShrink.Core/Setup/SetupModel.cs:81`):

~~~~text
ffmpeg version 9.0-full_build-www.gyan.dev
 .. ddagrab           |->V       Grab Windows Desktop images using Desktop Duplication API
 .. gfxcapture        |->V       Capture graphics/screen content as a video source
gfxcapture AVOptions:
   window_title      <string>     ECMAScript regular expression to match against the window title
   window_class      <string>     as window_title, but against the window class
   window_exe        <string>     as window_title, but against the windows executable name
   monitor_idx       <int>        index of the monitor to capture
~~~~

Komut: `ffmpeg -hide_banner -filters` ve `ffmpeg -hide_banner -h filter=gfxcapture`. Süzgeç var; onunla
bir kayıt alınmadı, kare hızı ve üstü örtülü pencere davranışı ölçülmedi. Kod bugün pencere hedefini
ekrandaki dikdörtgen olarak kaydediyor (4. satır). Kararı yeniden açmak sahibin işidir.
