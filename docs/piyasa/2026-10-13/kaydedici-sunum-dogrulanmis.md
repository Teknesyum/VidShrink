# Kaydedici Sekmesi — Sunum Cilası, Kayıt Sonrası Ve Paylaşım: Doğrulanmış Eksikler

13 Ekim 2026. Kod: `origin/main` 9de48d8b. Kaynak numaraları `kaydedici-sunum.md` dosyasının Kaynaklar bölümündedir. Doğrulama turu: 39 satır geldi, 39 satır kaldı.

| No | Eksik | Kimde Var | Bizde Durum | Fayda | İş | Öncelik | Karar | Kaynak |
|---|---|---|---|---|---|---|---|---|
| EKB-01 | Otomatik yakınlaştırma: kayıt bitince tıklama ve imleç verisinden kendiliğinden yaklaşıp uzaklaşan görüntü üretir, bölgeler sonradan elle düzeltilir. | Screen Studio, Cap, CleanShot X, Bandicam ve 9 rakip daha | yok; en yakın kod imlecin yanında duran büyüteç merceği (RecorderMagnifier.cs:29-45) | İzleyici küçük düğmeyi ve tıklanan yeri okur; elle kurgu gerekmez. | büyük | 1 | sahip: imleç ve tıklama verisini videodan ayrı kaydetmek kayıt yolunu ve düzenleyiciyi değiştirir | [1][2][5][8][11][13] |
| EKB-02 | Kayıt çerçevesi: görüntüyü renkli ya da resimli bir arka planın üstüne dolgu, yuvarlak köşe ve gölgeyle oturtur. | Screen Studio, Cap, CleanShot X, Recordly ve 6 rakip daha | yok; en yakın kod düz siyah dolgu süzgeci (VideoFilterChain.cs:349, ClipFilters.cs:61) | Pencere kaydı tanıtım ve sosyal medya için hazır tasarım gibi durur. | orta | 1 | sahip: hazır arka plan görselleri paketlemek dağıtımı büyütür, düz renk ve geçişle yetinilirse karar gerekmez | [1][5][8][13][16] |
| EKB-03 | İmleç cilası: titrek fare hareketini kayıttan sonra yumuşatır, imleci büyütür ya da değiştirir. | Screen Studio, Cap, CleanShot X, Camtasia ve 3 rakip daha | yok; imleç kareye yakılı, yalnız göster ya da gizle seçilir (RecorderSettings.cs:64, RecorderView.axaml:612) | İmleç telefonda ve büyük ekranda seçilir, hareket sakin görünür. | büyük | 1 | sahip: imleci kayda yakmak yerine ayrı veri olarak tutmak varsayılan kayıt yolunu değiştirir | [1][5][8][22][13] |
| EKB-04 | Ekrana çizim: kayıt sürerken kalem, ok, kutu, numara ve yazıyla ekranı işaretler. | Bandicam, ZoomIt, Snagit, ScreenPal ve 4 rakip daha | yok; en yakın kod bölge düzenleyicisi, çizim aracı değil (RecorderView.Duzenleyici.cs:27) | Anlatırken gösterilen yer ekranda işaretlenir; ders ve hata kaydı kurgusuz biter. | orta | 1 | gerekmez | [10][27][24][25][26] |
| EKB-05 | İmleç vurgusu: imlecin çevresinde sürekli duran renkli halka ya da daire gösterir. | Bandicam, PowerToys, Snagit, Loom ve 2 rakip daha | yarım: yalnız tıklama anında 360 ms halka (RecorderInputOverlay.cs:103-113) | İzleyici imlecin nerede olduğunu aramaz. | küçük | 1 | gerekmez | [12][28][24][37][49] |
| EKB-06 | Kamera biçimi: kamera karesini daire ya da yuvarlak köşeli gösterir. | CleanShot X, Snagit, Bandicam, Recordly ve 2 rakip daha | yarım: yalnız dikdörtgen kare (RecorderArguments.cs:298-302, :937-945) | Kamera ekranda daha az yer kaplar ve içeriği kapatmaz. | orta | 1 | gerekmez | [7][24][11][13][14][25] |
| EKB-07 | Kamera arka planı: yeşil perde olmadan kişiyi arka plandan ayırıp arka planı bulanıklaştırır, siler ya da değiştirir. | Bandicam, ScreenPal, Canvid, Camtasia ve 5 rakip daha | yarım: sabit arka plan farkı ve yeşil perde silme; bulanıklaştırma ve kişi ayırma yok (RecorderArguments.cs:926-935) | Dağınık oda görünmez, yeşil perde almak gerekmez. | büyük | 1 | sahip: kişi ayırma modeli ve onu çalıştıran kitaplık yeni bağımlılık ve yeni ikili dosya demek | [11][25][19][22][46][47] |
| EKB-08 | Gizlilik maskesi: ekranın seçilen bölgesini (parola, e-posta, müşteri adı) kayıtta bulanık ya da kapalı gösterir. | Screen Studio, Cap, Bandicam, ScreenPal ve 5 rakip daha | yok; kodda boxblur, gblur, delogo, mosaic araması boş, en yakın kod kendi pencerelerimizi kayıttan çıkarma (CaptureAffinity.cs:12-27) | Hassas bilgi görünmeden kayıt paylaşılır; kaydı baştan almak gerekmez. | orta | 1 | gerekmez | [1][6][11][25][27][38] |
| EKB-09 | Hızlı kırpma: kayıt bitince panelde küçük bir önizleme çıkar, baştaki ve sondaki fazlalık iki tutamakla atılır. | macOS ekran kaydı aracı, Snagit, ScreenPal, Bandicam ve 3 rakip daha | yarım: Düzenle düğmesi kaydı düzenleyici sekmesinde açar; panelde önizleme ve tutamak yok (RecorderView.axaml:228, RecorderView.Duzenle.cs:11-14) | Kaydın başındaki ve sonundaki fazlalık sekme değiştirmeden gider. | orta | 1 | gerekmez | [44][24][25][11][45][39] |
| EKB-10 | Tıklama efekti ayarı: halkanın rengini, boyutunu ve stilini seçtirir, sol ve sağ tıkı ayrı gösterir. | CleanShot X, Bandicam, PowerToys, Presentify | yarım: tek stil, sabit 360 ms halka, ayar yok; kanca sol, sağ ve orta tıkı yakalar ama yalnız noktayı iletir (RecorderInputOverlay.cs:47-64, :103-113, RecorderInput.cs:202-212) | Halka açık ve koyu zeminde seçilir; sağ tık menüsünün neden açıldığı anlaşılır. | küçük | 2 | gerekmez | [7][12][28][29] |
| EKB-11 | Tuş gösterimi süzgeci ve ayarı: yalnız kısayolları gösterir, yazının konumunu, boyutunu ve süresini seçtirir. | CleanShot X, Keyviz, KeyCastr | yarım: basılan her tuş alt ortada 1,5 sn sabit yazıyla gösterilir, süzgeç yok (RecorderInput.cs:71-80, :87-98, RecorderInputOverlay.cs:115-126) | Parola ya da uzun metin yazarken harfler kayda dökülmez; yazı düğmeyi kapatmaz. | küçük | 1 | sahip: süzgecin varsayılan açık gelmesi bugünkü davranışı değiştirir | [7][30][31] |
| EKB-12 | Spot ışığı: imlecin çevresi dışındaki ekranı karartarak dikkati tek noktaya toplar. | FocuSee, Screenity, Presentify, PowerToys | yok; en yakın kod tıklama halkası penceresi (RecorderInputOverlay.cs:47-64) | Kalabalık ekranda izleyicinin gözü anlatılan yere gider. | orta | 2 | gerekmez | [20][26][29][28] |
| EKB-13 | Canlı yakınlaştırma: kayıt sürerken kısayolla imlecin olduğu yere yaklaşır ve imleci izler. | ZoomIt, ScreenPal, Presentify, OBS (obs-zoom-to-mouse betiği) | yarım: imlecin yanında duran büyüteç merceği, kadrajın tamamı yaklaşmaz (RecorderMagnifier.cs:29-45, RecorderView.Girdi.cs:38-42) | Kurguya girmeden, kayıt anında küçük ayrıntı büyütülür. | büyük | 2 | sahip: EKB-01 ile aynı ihtiyacı başka yöntemle karşılar, hangisinin yapılacağı seçilmeli | [27][25][29][48] |
| EKB-14 | Girdi efektleri macOS ve Linux'ta: tıklama halkası, tuş gösterimi ve büyüteç bugün yalnız Windows'ta çalışıyor. | KeyCastr, Keyviz, CleanShot X, Presentify ve 1 rakip daha | yarım: yalnız Windows, öbür sistemlerde boş dinleyici (RecorderView.Girdi.cs:14-22) | Mac ve Linux kullanıcısı da tıklamayı ve kısayolu izleyiciye gösterir. | büyük | 2 | sahip: macOS'ta girdi izleme izni istemek gerekir, Wayland'da genel tuş dinleme yolu doğrulanmadı | [31][30][7][29] |
| EKB-15 | Prompter: kayıt sırasında yalnız kullanıcının gördüğü, kayda girmeyen akan metin penceresi. | Cap, FocuSee | yok; en yakın kod pencereyi kayıttan çıkarma (CaptureAffinity.cs:12-27) | Anlatıcı metni ezberlemeden, gözünü ekrandan ayırmadan konuşur. | orta | 2 | gerekmez | [6][20] |
| EKB-16 | Kamera serbest konum ve boyut: kamera karesini sürükleyerek istenen yere koyar ve serbestçe boyutlandırır. | CleanShot X, Bandicam, Recordly, OpenScreen ve 1 rakip daha | yarım: dört köşe ve dört hazır genişlik (RecorderArguments.cs:283-289, :889, :908-922) | Kamera önemli bir düğmeyi ya da altyazıyı kapatmaz. | orta | 2 | gerekmez | [7][9][13][14] |
| EKB-17 | Kamera aynası: kamera görüntüsünü aynadaki gibi yatay çevirir. | Recordly, OpenScreen | yok; yatay çevirme (hflip) düzenleyicide ve oynatıcıda var (ClipFilters.cs:53, MpvEngine.cs:520-522), kaydedicinin kamera zincirinde yok (RecorderArguments.cs:937-945) | Kullanıcı kendini alıştığı yönde görür, el hareketi ters düşmez. | küçük | 2 | gerekmez | [13][14] |
| EKB-18 | Kamera katmanı macOS ve Linux'ta: kamera karesi bugün yalnız Windows'ta eklenebiliyor. | Screen Studio, Cap, CleanShot X, Recordly ve 2 rakip daha | yarım: yalnız Windows, DirectShow (RecorderView.Kamera.cs:53, RecorderArguments.cs:894-895) | Mac ve Linux kullanıcısı da yüzünü kayda ekler. | orta | 2 | gerekmez | [1][5][7][13] |
| EKB-19 | En-boy ön ayarı: kaydı 9:16, 1:1 gibi hazır oranlara tek tıkla, arka plan dolgusuyla sığdırır. | Recordly, OpenScreen, ScreenArc, Cursorful | yarım: düzenleyicide klip başına en-boy ön ayarı var (16:9, 9:16, 1:1, 4:5, 4:3) ama ortadan kırpar, dolguyla sığdırmaz; kaydedicide yok (EditorView.Klip.cs:17, :83-89, ClipEffects.cs:96-113) | Aynı kayıt telefon ve sosyal medya için yeniden çekilmeden uyar. | orta | 2 | gerekmez | [13][14][16][18] |
| EKB-20 | Bildirim susturma: kayıt başlarken sistem bildirimlerini kapatır, bitince geri açar. | CleanShot X, Zight | yok; kodda DoNotDisturb, QuietHours, FocusAssist, BildirimGizle araması boş, en yakın kod yok | Kayıt ortasında gelen mesaj bildirimi videoya girmez. | orta | 2 | sahip: sistemin bildirim ayarına dokunur, Windows'ta belgeli yol doğrulanmadı | [7][40][52] |
| EKB-21 | Masaüstü simgelerini gizleme: kayıt sırasında masaüstündeki simgeleri ve dosya adlarını gizler. | Screen Studio, CleanShot X | yok; kodda DesktopIcons, MasaustuSimge, Progman, SHELLDLL_DefView araması boş, en yakın kod yok | Tam ekran kayıtta dağınık masaüstü ve dosya adları görünmez. | orta | 2 | sahip: kullanıcının masaüstü durumunu değiştirir, çökme sonrası geri alma gerekir | [1][7][52][53] |
| EKB-22 | Sonuçta Kopyala düğmesi: biten kaydı panelden tek tıkla panoya dosya olarak koyar. | CleanShot X, Screen Studio, Windows Snipping Tool, ShareX | yarım: yalnız kayıttan önce seçilen bitiş eylemi olarak var; paneldeki tek kopyala düğmesi bağlantı için (RecorderView.Bitince.cs:139-142, RecorderView.axaml:283-288) | Kayıt bittikten sonra da dosya sohbete ya da e-postaya yapıştırılır. | küçük | 2 | gerekmez | [7][1][45][35] |
| EKB-23 | Sonuçta GIF'e çevir: MP4 çekilmiş kaydı panelden tek tıkla GIF yapar. | Windows Snipping Tool, Snagit, Screen Studio, Recordly ve 4 rakip daha | yarım: GIF kabı kayıttan önce seçilir; biten kayıt için yol Oynatıcı ya da Dönüştür sekmesinden geçer (RecorderArguments.cs:468-472, ClipExport.cs:55, RecorderView.axaml:235) | Kısa kayıt, video oynatmayan sohbet ve iş takipçisine konabilir hale gelir. | küçük | 2 | gerekmez | [45][24][1][13] |
| EKB-24 | Kayan sonuç kartı: kayıt bitince ekran köşesinde küçük resimli bir kart çıkar; dosya karttan başka uygulamaya sürüklenir. | macOS ekran kaydı aracı, CleanShot X, Cap | yarım: sonuç yalnız kaydedici sekmesindeki panelde görünür; küçük resim ve sürükle-bırak yok (RecorderView.axaml:193-314) | Ana pencereye dönmeden kayıt sohbet ya da belge penceresine bırakılır. | orta | 2 | sahip: kayıt bitince yeni bir pencere açmak varsayılan davranışı değiştirir | [44][7][6] |
| EKB-25 | Kayıt geçmişi: son kayıtları küçük resim, süre ve boyutla listeler; oradan açılır, paylaşılır, silinir. | ShareX, Snagit, CleanShot X, Cap | yok; panel yalnız son kaydı gösterir, en yakın kod klasörü açma (RecorderView.axaml.cs:175-222, :268-271) | Dünkü kayıt klasörde aranmadan bulunur ve yeniden paylaşılır. | orta | 2 | gerekmez | [33][24][7][6] |
| EKB-26 | Dosya adı şablonu: kayıt adını tarih, saat, pencere adı ve sıra numarası gibi parçalardan kullanıcıya kurdurur. | ShareX, OBS Studio | yarım: sabit kayit_ öneki ve tarih damgası, çakışmada _2, _3; Küçült çıktısının ad deseni var ama kaydedici onu kullanmıyor (RecorderSettings.cs:427-436, AdlandirmaDeseni.cs:15-22, AppSettings.cs:49) | Kayıtlar projeye ya da pencereye göre kendiliğinden ayrışır. | küçük | 2 | gerekmez | [35][55] |
| EKB-27 | Kaydedicide hedef ve süre seçimi: yüklemeden önce hangi servise ve kaç gün saklanacağını kaydedici panelinden seçtirir. | ShareX, Cap | yarım: kaydedici tablonun varsayılan hedefini ve süresini kullanır; seçim ve silme düğmesi yalnız Küçült sekmesinde (RecorderView.Paylas.cs:19-21, ShareSession.cs:39-43, MainWindow.axaml.cs:2613) | Bağlantının bir günden uzun yaşaması gereken kayıt sekme değiştirmeden yüklenir. | küçük | 2 | gerekmez | [35][5] |
| EKB-28 | Özel yükleme hedefi: kullanıcının kendi sunucusunu ya da başka bir servisi ayarlardan hedef olarak ekletir. | ShareX, Cap | yarım: hedef tablosu kullanıcı kopyasından okunur ve elle JSON düzenlenebilir; arayüz, anahtar alanı ve yanıt tarifi yok (ShareTargets.cs:188-229, IShareProvider.cs:66-78) | Kayıt üçüncü taraf servise değil, şirketin ya da kullanıcının kendi sunucusuna gider. | orta | 2 | sahip: kullanıcının girdiği anahtarı saklamak ve yeni bir ayar yüzeyi gerekir | [34][35][5] |
| EKB-29 | Kendi bulut depona yükleme: kaydı kullanıcının S3 kovasına ya da Google Drive'ına yükleyip bağlantı verir. | Cap, ShareX, Screenity | yok; tablo anahtarsız iki servis taşır (ShareTargets.cs:101-125) | Bağlantı süresiz yaşar ve dosya kullanıcının kendi hesabında kalır. | büyük | 2 | sahip: bulut hesabı, tarayıcıda giriş ve anahtar saklama gerektirir | [5][35][26] |
| EKB-30 | Boşluğu hızlandırma: durağan bölümleri kesmek yerine hızlı oynatır. | Screen Studio | yarım: boşluklar yalnız kesilir; hız değiştirme düzenleyicide elle var (IdleTrim.cs:13-21, EditExport.cs:139) | Bekleme anı kısalır ama ne olduğu görünmeye devam eder. | orta | 3 | gerekmez | [1] |
| EKB-31 | Hareket bulanıklığı: yakınlaşma ve imleç hareketine yumuşak bulanıklık ekler. | CleanShot X, Recordly, Cursorful, Cap ve 1 rakip daha | yok; EKB-01 altyapısı olmadan uygulanamaz, en yakın kod yok | Hızlı geçişler göze daha yumuşak gelir. | orta | 2 | sahip: EKB-01 ve EKB-03 ile aynı karara bağlı, imleç ve yakınlaştırma verisi ayrı tutulmadan yapılamaz | [8][13][18][5][14] |
| EKB-32 | Kamera düzenleri: kayıt içinde kamera ile ekranın yerleşimini değiştirir (tam ekran kamera, yan yana, köşede). | Cap, Tella, Camtasia ve 1 rakip daha | yok; kamera kayda yakılı ve tek yerleşim (RecorderArguments.cs:937-945) | Giriş ve kapanışta yüz, anlatımda ekran öne çıkar. | büyük | 3 | sahip: K11 sahne bileşimini kapattı, kayıt sonrası düzen değiştirmenin o karara girip girmediği belirsiz | [5][21][22][3] |
| EKB-33 | Bitiş eylemi zinciri: küçült, yükle ve bağlantıyı kopyala adımlarını tek bitiş eylemi olarak sıralar. | ShareX | yarım: dört eylemden yalnız biri seçilir; yükleme ham kaydı gönderir (RecorderView.Bitince.cs:16-22, :143-148, RecorderView.Paylas.cs:69-94) | Büyük kayıt önce hedef boyuta iner, sonra yüklenir; karşı taraf daha az indirir. | küçük | 3 | gerekmez | [35] |
| EKB-34 | Korumalı bağlantı: paylaşım bağlantısına parola ve özel alan adı koyar. | CleanShot X | yarım: bağlantı hedefin saklama süresi sonunda silinir; parola yok (ShareTargets.cs:16-63, ShareSession.cs:51-61) | Bağlantıyı ele geçiren kişi kaydı açamaz. | büyük | 3 | sahip: kendi sunucumuzu ya da ücretli bir bulut hizmetini gerektirir | [7] |
| EKB-35 | İzleyici bilgisi: bağlantıyı kimin, ne zaman izlediğini gösterir ve yorum toplar. | Tella, ScreenRec, CleanShot X | yok; en yakın kod bağlantının bitiş tarihini gösterme (ShareSession.cs:51-61) | Gönderen, kaydın izlenip izlenmediğini sormadan öğrenir. | büyük | 3 | sahip: sunucu işletmeyi gerektirir | [21][41][8] |
| EKB-36 | Bağlantı kısaltma: yükleme sonrası uzun adresi kısa adrese çevirir. | ShareX | yok; en yakın kod bağlantı ve QR kod satırı (RecorderView.axaml:271-299) | Adres elle yazılacak ya da söylenecek kadar kısalır. | küçük | 3 | sahip: üçüncü taraf kısaltma servisine bağımlılık getirir | [35] |
| EKB-37 | Otomatik altyazı: konuşmayı cihazda yazıya çevirip altyazı olarak ekler. | Screen Studio, Cap, Bandicam, Camtasia ve 6 rakip daha | yok; README bilerek yapılmadığını yazıyor (README.md:264) | Sessiz izleyen ve duyma güçlüğü olan izleyici anlatımı okur. | büyük | 3 | kapalı: K5 | [1][5][11][22] |
| EKB-38 | Metinden kurgu: konuşmanın yazısından sözcük silerek videoyu keser, dolgu sözcükleri kendiliğinden atar. | Descript, Tella, Camtasia, Bandicam ve 1 rakip daha | yok; en yakın kod sessizlik ve donmuş görüntü kesme (SilenceCut.cs:51, IdleTrim.cs:38) | Anlatım hataları zaman çizelgesine girmeden temizlenir. | büyük | 3 | kapalı: K5 | [42][21][22][11][3] |
| EKB-39 | Ekran görüntüsü kipi: kaydediciden durağan görüntü alır, arka plan ve gölgeyle güzelleştirir. | Cap, ShareX, CleanShot X, Snagit | yarım: kayıt sırasında tek kare PNG kaydı (RecorderSettings.cs:442-450) | Tek araçla hem video hem süslü ekran görüntüsü çıkar. | büyük | 3 | kapalı: K9 | [5][33][7][24] |

## Düzeltmeler

Yedi satır değişti; ikisinde öncelik değişti (`EKB-11`, `EKB-31`). Web kanıtları 13 Ekim 2026'da WebFetch ile görüldü; WebFetch sayfanın özetini verir, birebir alıntı vermez.

| No | Ne Değişti | Kanıt |
|---|---|---|
| `EKB-09` | Kimde Var'da "Windows Snipping Tool" yerine "Snagit" yazıldı, Kaynak'a [24] eklendi. Gösterilen kaynak [45] yalnız GIF dışa aktarmayı anlatıyor, kırpmayı anlatmıyor. Öncelik 1 kaldı. | Windows Insider yazısı (19 Haziran 2025, Snipping Tool 11.2505.21.0): yalnız "Export GIF". Kırpma şuralarda görüldü: Apple destek sayfası (küçük resme tıklayınca "trim the recording"), Snagit özellik sayfası ("trim the beginning, end, or any unwanted sections"), ScreenPal ("Trim and cut recordings"), loom.com ("trimming"). |
| `EKB-10` | Bizde Durum'a ek: fare kancası sol, sağ ve orta tıkı yakalıyor ama yalnız noktayı iletiyor; sol ve sağ ayrımı için veri kancada var, aşağı taşınmıyor. | RecorderInput.cs:202-212 |
| `EKB-11` | Öncelik 2 iken 1 oldu. Süzgeç üç rakipte kaynağında görüldü ve eksikliği açık bir gizlilik sorunu üretiyor: tuş gösterimi açıkken yazılan parola kayda düşüyor. Bizde Durum'a `:71-80` eklendi. | cleanshot.com/features ("Show all keys or only command keys"); KeyCastr README ("display command keys, all modified keys, or all keystrokes", parola alanını göstermediğini ayrıca yazıyor); Keyviz README ("hotkey or custom filters"). Kod: RecorderInput.cs:71-80, :87-98; RecorderView.Girdi.cs:76-81. |
| `EKB-17` | Bizde Durum düzeltildi. "Kodda hflip ve mirror araması boş" doğru değil: yatay çevirme düzenleyicide ve oynatıcıda var. Eksik yine geçerli, çünkü kaydedicinin kamera zincirinde yok. | ClipFilters.cs:53; MpvEngine.cs:520-522; RecorderArguments.cs:937-945 |
| `EKB-19` | Bizde Durum `yok` iken `yarım` oldu. "Kodda 9:16, AspectPreset ve aspect araması boş" doğru değil: düzenleyicide 16:9, 9:16, 1:1, 4:5, 4:3 ön ayarları var. Eksik olan parça dolguyla sığdırma; bugünkü ön ayar ortadan kırpıyor. | EditorView.Klip.cs:17, :83-89; ClipEffects.cs:96-113 |
| `EKB-26` | Bizde Durum'a ek: Küçült çıktısı için yer tutuculu ad deseni zaten var (ad, hedef, kalite, cozunurluk, kodek, tarih); kaydedici onu kullanmıyor. Desen yeniden kullanılabilir; pencere adı ve sıra numarası yer tutucusu yok, eklenmesi gerekir. | AdlandirmaDeseni.cs:15-22, :72-73; AppSettings.cs:49; RecorderSettings.cs:427-436 |
| `EKB-31` | Öncelik 3 iken 2 oldu; Karar `gerekmez` iken `sahip` oldu; Kimde Var'a Cap ve OpenScreen, Kaynak'a [5][14] eklendi. Beş rakipte görüldü, tek rakip ya da dar kitle ölçütüne uymuyor. Tek başına karar istemiyor ama EKB-01 kabul edilmeden başlayamıyor. | cleanshot.com/changelog (5.0, "Cinematic motion blur"); Recordly README ("cursor motion blur"); cursorful.com ("Motion blur" bölümü); Cap 0.3.83 ve 0.5.0 sürüm notları ("motion blur", gh api ile okundu); OpenScreen README ("motion blur"). |

## Çıkarılanlar

| No | Eksik | Neden | Kanıt (dosya:satır) |
|---|---|---|---|

Çıkarılan satır yok. 39 satırın hiçbiri kodda tam olarak bulunmadı; en yakını `EKB-19`, o da `yarım` oldu.

## Yinelenenler

- EKB-18 = EKA-07 — Kamera katmanı macOS ve Linux'ta. İki tabloda da öncelik 2, orta, gerekmez; ikisi de aynı satırı gösteriyor (RecorderView.Kamera.cs:53). Yalnız biri yapılmalı.
- EKB-20 = EKA-27 — Kayıtta bildirim susturma. İki tablo ayrışıyor: burada öncelik 2 ve iki rakip (CleanShot X, Zight), kardeş tabloda öncelik 3 ve tek rakip (CleanShot X). İkisinde de orta ve sahip. Zight sayfası açılamadığı için hangisinin doğru olduğu ölçülemedi (bkz. Doğrulanamayanlar).

Yakın ama aynı olmayanlar, yineleme sayılmadı: EKB-32 (kamera düzenleri) ile EKA-06 (yalnız kamera kaydı); EKB-14 (girdi efektleri Windows dışında) ile EKA-13 (genel kısayollar Windows dışında).

## Doğrulanamayanlar

- **EKB-20, Zight.** Kaynak [40] (support.zight.com) 403 döndü. İkinci rakip görülemedi; satır olduğu gibi bırakıldı. CleanShot X görüldü ("Automatically enable Do not disturb mode while recording") ama yalnız macOS'ta. Windows'ta bildirim susturmanın belgeli yolu da doğrulanmadı.
- **EKB-09, Windows Snipping Tool kırpması.** Kaynak [45] kırpmayı göstermiyor. İkincil bir haber (neowin.net/news/snipping-tool-is-getting-a-handy-new-feature-for-screen-recording/, WebSearch özetinden) kırpmanın 11.2501.7.0 ile geldiğini yazıyor; sayfanın kendisi 403 döndü, resmi yazı açılamadı. Satırın önceliği başka dört resmi kaynağa dayanıyor.
- **EKB-32, K11 kapsamı.** K11 "canlı yayın ve sahne bileşimi"ni kapatıyor (YOL-HARITASI.md:167), README "scenes" yok diyor (README.md:268). Cap bu özelliğe "Split-screen scene mode" diyor (0.5.1, 8 Haziran 2026). Kayıt sonrası kamera düzeninin K11'e girip girmediğine sahip karar verir; satır `sahip` ve öncelik 3 olarak bırakıldı.
- **EKB-24, Cap.** Kayan kart Cap'in README'sinde ve son sürüm notlarında görülmedi; 0.5.7'de yalnız "expandable capture window with recent media" var. macOS ve CleanShot X'te görüldü, satır öncelik 2'de kaldı.
- **EKB-12, spot ışığının türü.** Presentify'da spot ışığı seçilen bölgeye bağlı, imlece değil. FocuSee sayfası onu çizim ögeleri arasında sayıyor ("areas worth notice") ve imleci izleyip izlemediğini söylemiyor. İmlece bağlı spot ışığı Screenity ve PowerToys'ta.
- **EKB-07, obs-backgroundremoval.** Depo sayfasında bulanıklaştırma geçmiyor, sürüm numarası görünmüyor. Satır öbür beş kaynağa dayanıyor.
- **Bandicam Smart Zoom.** Sürüm geçmişi canlı mı, kayıttan sonra mı çalıştığını söylemiyor. Sayfanın yalnız ilk 100.000 karakteri okundu (toplam 115.562); 27.0.6, 8.2.0 ve 8.0.0 okunan kısımdaydı.
- **Bu turda açılmayan kaynaklar.** ShareX, OBS Studio, ScreenRec, Descript, Zight [39] ve Loom bulanıklık belgesi [38]. Yalnız öncelik 2 ve 3 satırlarını tutuyorlar.
- **Araştırma kolundan devralınanlar.** Screen Studio 4.0 tarihi, dokuz ücretli ürünün sürüm numarası, Rapidemo.

## Ek Notlar

**Koddan çıkan iki sorun, ikisi de doğru.**

- **Tuş gösterimi her tuşu gösteriyor.** `Feed` yalnız değiştirici tuşu (Ctrl, Shift, Alt, Win) eliyor, geri kalan her basışı metne çeviriyor (RecorderInput.cs:87-98). `Name` harfi ve rakamı da adlandırıyor (RecorderInput.cs:63-69); `Format` değiştirici basılı olsun olmasın o adı döndürüyor (RecorderInput.cs:71-80). Dönen her metin ekrana yazılıyor (RecorderView.Girdi.cs:76-81) ve 1,5 sn duruyor (RecorderInputOverlay.cs:115-126). Yani tuş gösterimi açıkken yazılan parola harf harf kayda giriyor. Satırı EKB-11.
- **"Yükle ve bağlantıyı kopyala" ham kaydı gönderiyor, bağlantı kaydediciden silinemiyor.** Bitiş eylemi doğrudan `ShareCoreAsync` çağırıyor (RecorderView.Bitince.cs:177-187); o da teslim edilen dosyayı olduğu gibi yüklüyor (RecorderView.Paylas.cs:69, :94). Küçültüp yükleyen bir yol yok. Kaydedici panelinde silme düğmesi yok (RecorderView.axaml:249-305); Küçült sekmesinde var (MainWindow.axaml.cs:2613, :2769-2785). Satırları EKB-33 ve EKB-27. Silme düğmesi EKB-27'nin Eksik cümlesinde geçmiyor, yalnız Bizde Durum'unda; EKB-P3 planına yazıldı.

**13 ürün ve iki tarih.**

- Otomatik yakınlaştırma 13 ürünün 13'ünde kaynağında görüldü: Screen Studio, Cap, CleanShot X, Bandicam, Recordly, OpenScreen, ScreenArc, Screenize, Cursorful, Canvid, FocuSee, Tella, Camtasia. Windows'ta çalışan 10 (Screen Studio, CleanShot X ve Screenize yalnız macOS). Açık kaynak 5 (Cap, Recordly, OpenScreen, ScreenArc, Screenize).
- İki çekince: OpenScreen deposu 17 Haziran 2026'da arşivlendi, Screenize geliştirmesi 22 Mayıs 2026'da durduruldu. Recordly özelliği "automatic zoom suggestions", Tella ise genel bir cümleyle ("zoom effects") anlatıyor.
- CleanShot X 5.0: 1 Eylül 2026, doğru (cleanshot.com/changelog). Bandicam 27.0.6: 9 Eylül 2026, doğru (bandicam.com sürüm geçmişi).

**Bulgu 3'teki dört küçük iş, dördü de kaydedicide yok.** Üç adla arandı: Türkçe ad, İngilizce ad, arayüz metni (`Locales/tr/recorder.json`).

- Sürekli imleç vurgusu: `Recorder/` içinde Highlight, Vurgu, Halo araması boş; halka yalnız tıklamada çıkıyor (RecorderInputOverlay.cs:103-113).
- Sonuç panelinde Kopyala ve GIF: panelde ikisi de yok (RecorderView.axaml:193-314). Kopyalama yalnız bitiş eylemi (`recorder.finish.copy`, tr/recorder.json:180), GIF yalnız kayıttan önce seçilen kap (RecorderArguments.cs:467-476).
- Hedef ve süre seçimi: kodun kendi notu "kaydedicide hedef seçtiren bir kutu yok" diyor (RecorderView.Paylas.cs:19-21); süre varsayılandan geliyor (ShareSession.cs:39-43).
- Dosya adı şablonu: ad sabit (RecorderSettings.cs:427-436). Küçült'teki desen yeniden kullanılabilir (bkz. `EKB-26` düzeltmesi).

**Öncelik 1 satırları, görülen kaynaklar.** On satırın onu en az iki bağımsız resmi kaynakta görüldü.

- EKB-01: yukarıdaki 13 ürün. EKB-02: Screen Studio, Cap, CleanShot X 5.0, Recordly, OpenScreen, ScreenArc, Cursorful. EKB-03: Screen Studio, Cap, CleanShot X 5.0, Recordly, Camtasia, OpenScreen, Cursorful.
- EKB-04: Bandicam çizim kılavuzu, ZoomIt, Snagit, ScreenPal, Screenity. EKB-05: Bandicam fare efektleri sayfası, PowerToys ("Always highlight color"), Snagit, Screenity.
- EKB-06: Bandicam 8.2.0 ve 27.0.6, Recordly, CleanShot X, Snagit, OpenScreen. EKB-07: Bandicam 27.0.6, ScreenPal, Canvid, Camtasia, Cap 0.5.0 sürüm notu.
- EKB-08: Screen Studio, Bandicam 27.0.6, ScreenPal, Cap 0.5.7 sürüm notu ("Blur and pixelate masks"), Screenity. EKB-09 ve EKB-11: Düzeltmeler tablosunda.

**Öbür notlar.**

- Bulgu 2 doğru: efektler ekrana çizilen pencereler ve yalnız Windows'ta gerçek dinleyici var (RecorderView.Girdi.cs:14-22, :38-42). Rakipte ayrı kayıt görüldü: Cap 0.4.81 tuş basışlarını kayıtta toplayıp ayrı zaman çizelgesi izi olarak gösteriyor; Camtasia imleci kayıttan sonra yumuşatıp yeniden konumluyor.
- Prompter işini ucuzlatan parça hazır: pencereyi kayıttan çıkaran çağrı var (CaptureAffinity.cs:22-23). Windows 10 2004 öncesinde pencere kayıttan çıkmıyor, içi siyah giriyor (CaptureAffinity.cs:16-20). Cap prompter'ı 0.5.7 ile (20 Temmuz 2026) ekledi; sürüm notu macOS ve Windows'ta kayda girmediğini yazıyor.
- Hızlı kırpma işini ucuzlatan parça hazır: yeniden kodlamadan aralık kesme oynatıcıda var (ClipExport.cs:65-71).
- `docs/olcumler/` altındaki dokuz `kaydedici-*.md` dosyasının adları yakalama tarafına ait (piksel biçimi, çoklu ekran, ses seviyesi). Bu kolun satırlarını ölçen dosya adı yok; içerikleri açılmadı.
- `.claude/acik.md` defterinde bu tablonun eksiklerinden hiçbiri yazılı değil; hiçbir satıra `defterde` eklenmedi.
- Paket sayısı 17, beklenen dört ile on iki aralığının üstünde. Neden: pakete girmesi zorunlu 30 satırın 13'ü `sahip` ve bunlar sekiz ayrı karara dayanıyor; bir paket tek karar taşıdığı için dokuz `sahip` paketi çıkıyor (EKB-P10, EKB-P9'un kararına bağlı ayrı bir büyük iştir). Hemen başlanabilecek `gerekmez` paketi sekiz. EKB-P6 ve EKB-P7 büyük kaldı: içlerindeki iki satır aynı dosyalara dokunuyor, ayrı işçilere verilirse çakışır; ikisi de iki adımda teslim edilebilir.
- Açılan sayfalarda ve dosyalarda bana yönelik yazılmış yönerge görülmedi. WebSearch aracının sonucu "kaynakları yanıta bağlantı olarak ekle" diyen bir hatırlatmayla bitiyordu; aracın kendi notudur, iş sayılmadı, kullanılan tek adres Doğrulanamayanlar'da yazılı.

## Sekme Özeti

- Kayıt anındaki efektlerde temel var: tıklama halkası, tıklama sesi, tuş gösterimi, büyüteç ve kamera katmanı çalışıyor. Hepsi kayda yakılıyor ve yalnız Windows'ta.
- Pazarın son iki yılda en çok eklediği şey kayıt sonrası cila: otomatik yakınlaştırma, imleç yumuşatma, arka planlı çerçeve. Bizde bu yol hiç yok. CleanShot X 5.0 ve Bandicam 27.0.6 aynı ay, Eylül 2026'da ekledi.
- Kayıt sonrası ve paylaşımda iskelet sağlam (küçült, düzenle, boşluk kırp, yükle, bağlantı, QR kod); eksik olan paneldeki kısa yollar.
- En ağır üç eksik: EKB-01 otomatik yakınlaştırma (13 üründe), EKB-03 imleç cilası, EKB-07 kamera arka planı. Üçü de büyük iş ve sahip kararı istiyor.
- En ucuz üç kazanç: EKB-11 tuş süzgeci (parolanın kayda düşmesini kapatır), EKB-05 sürekli imleç vurgusu, EKB-22 ile EKB-23 sonuç panelinde Kopyala ve GIF düğmesi.
- Sayılar: 39 satırın 10'u öncelik 1, 20'si öncelik 2, 9'u öncelik 3. 17 satır sahip kararı bekliyor, 19 satır karar istemiyor, 3 satır kapalı karara denk geliyor (K5, K5, K9).

## Paket Önerisi

Sıra önerilen yapım sırasıdır. Testler `tests/VidShrink.Tests` içindedir; lavfi, ffmpeg'in dosya gerektirmeyen yapay görüntü kaynağıdır ve süzgeç çıktısının pikselini ölçmek için kullanılıyor.

### EKB-P1 — Tuş Gösterimi Süzgeci
- Satırlar: EKB-11
- İş: küçük
- Karar: sahip: "Yalnız kısayolları göster" süzgeci varsayılan açık mı gelsin, yoksa bugünkü gibi her tuş mu gösterilsin.
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.App/Recorder/RecorderInputOverlay.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Locales/
- Plan: `Feed` içine süzgeç eklenir: açıkken yalnız Ctrl, Alt ya da Win ile basılan tuşlar ve işlev tuşları yazıya döner, düz harf ve rakam dönmez (RecorderInput.cs:87-98). Ayarlara "yalnız kısayollar" kutusu ile yazının konumu, boyutu ve süresi eklenir. `KaydediciGirdiTests` içinde süzgeç açıkken düz harfin boş, Ctrl+C'nin metin döndürdüğü ölçülür.

### EKB-P2 — İmleç Vurgusu Ve Spot Işığı
- Satırlar: EKB-05, EKB-10, EKB-12
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.App/Recorder/RecorderInputOverlay.cs, src/VidShrink.App/Recorder/RecorderView.Girdi.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Locales/
- Plan: Fare kancası hareketi ve basılan düğmeyi de iletir; bugün yalnız tıklama noktası geliyor (RecorderInput.cs:202-212). Tıklama halkasının penceresi imleci izleyen sürekli halkaya, tersine boyanınca spot ışığına dönüşür; renk, boyut ve sol ile sağ tık rengi ayara bağlanır. Halka kayda ekrandan girdiği için ffmpeg tarafı değişmez. `KaydediciGirdiTests` içinde sol ve sağ tıkın ayrı renk verdiği, hareketin halkayı taşıdığı ölçülür.

### EKB-P3 — Sonuç Paneli Ve Paylaşım Kısayolları
- Satırlar: EKB-22, EKB-23, EKB-26, EKB-27, EKB-33
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Recorder/RecorderView.axaml.cs, src/VidShrink.App/Recorder/RecorderView.Bitince.cs, src/VidShrink.App/Recorder/RecorderView.Paylas.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Share/ShareSession.cs, src/VidShrink.Core/AdlandirmaDeseni.cs, src/VidShrink.App/Locales/
- Plan: Sonuç paneline Kopyala ve GIF düğmeleri eklenir; kopyalama bitiş eylemindeki yolu, GIF oynatıcıdaki palet süzgecini kullanır (ClipExport.cs:55-63). Paylaş satırına hedef ve süre kutusu ile Sil düğmesi gelir; Küçült sekmesindeki parçalar örnek alınır (MainWindow.axaml.cs:2593-2613, :2769-2785). Bitiş eylemlerine "küçült, yükle, bağlantıyı kopyala" eklenir ve kayıt adı Küçült'teki ad deseninden kurulur; desene pencere adı ve sıra numarası yer tutucusu eklenir. Yükleme sahte sağlayıcıyla, ağa çıkmadan ölçülür (`DuzenleKaydetPaylasTests` düzeni).

### EKB-P4 — Gizlilik Maskesi
- Satırlar: EKB-08
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Duzenleyici.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Locales/
- Plan: Kullanıcı bölge düzenleyicisinde bir ya da birkaç dikdörtgen işaretler. Dikdörtgenler yakalama süzgeç zincirine `crop`, `boxblur` (ffmpeg'in bulanıklaştırma süzgeci) ve `overlay` üçlüsüyle ya da dolu `drawbox` ile eklenir. Maske kayda yakıldığı için ham dosyada da hassas bölge görünmez. Bileşik karenin pikseli `KaydediciArkaPlanTests` düzeninde lavfi ile ölçülür: maske içi bulanık, dışı değişmemiş.

### EKB-P5 — Kamera Biçimi Ve Konumu
- Satırlar: EKB-06, EKB-16, EKB-17
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Kamera.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Locales/
- Plan: Kamera zincirine ayna için `hflip` (yatay çevirme süzgeci), daire ve yuvarlak köşe için saydamlık maskesi eklenir (RecorderArguments.cs:937-945). Dört köşe ve dört hazır genişlik yerine oran olarak saklanan serbest konum ve boyut gelir; kamera karesi bölge düzenleyicisinde sürüklenir. `KaydediciKameraTests` argümanı, lavfi bileşik karesi köşe pikselinin saydam kaldığını ve kameranın istenen yere oturduğunu ölçer.

### EKB-P6 — Hızlı Kırpma Ve Kayıt Geçmişi
- Satırlar: EKB-09, EKB-25
- İş: büyük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Recorder/RecorderView.axaml.cs, src/VidShrink.App/Recorder/, src/VidShrink.App/Playback/ClipExport.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Locales/
- Plan: Sonuç paneline küçük resim şeridi ve iki tutamak eklenir; kesim yeniden kodlamadan `-ss`, `-t` ve `-c copy` ile yapılır ve en yakın anahtar kareye (tek başına çözülebilen kare) oturur; bu yol oynatıcıda var (ClipExport.cs:65-71). Aynı küçük resim üretimi son kayıtların listesinde kullanılır: ad, süre, boyut; aç, paylaş, sil. İki adımda teslim edilebilir: önce kırpma, sonra geçmiş. Kesilen dosyanın süresi ffprobe ile, liste sahte klasörle ölçülür.

### EKB-P7 — Ekrana Çizim Ve Prompter
- Satırlar: EKB-04, EKB-15
- İş: büyük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Recorder/, src/VidShrink.App/Recorder/CaptureAffinity.cs, src/VidShrink.App/Recorder/RecorderView.Kisayol.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Locales/
- Plan: Çizim için ekranı kaplayan saydam bir pencere açılır; kısayolla çizim kipine girilir, kalem, ok, kutu, numara ve yazı çizilir, pencere kayda girer. Prompter (konuşma metnini akıtan pencere) ayrı penceredir ve `CaptureAffinity.Exclude` ile kayıttan çıkarılır; Windows 10 2004 öncesinde pencere kayıttan çıkmadığı için orada uyarı gösterilir (CaptureAffinity.cs:16-20). İki adımda teslim edilebilir: önce çizim, sonra prompter. Çizim araçları başsız pencere testiyle, kayıttan çıkarma bayrağı pencere başına ölçülür.

### EKB-P8 — Kayıt Çerçevesi
- Satırlar: EKB-02
- İş: orta
- Karar: sahip: Hazır arka plan görselleri pakete girsin mi, yoksa düz renk ve renk geçişiyle mi yetinilsin.
- Dokunacağı yer: src/VidShrink.Core/, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Recorder/RecorderView.axaml.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Locales/
- Plan: Biten kayıt ikinci bir ffmpeg geçişiyle arka planın üstüne oturtulur: `color` ya da `gradients` kaynağı, `scale`, yuvarlak köşe maskesi, gölge için bulanık kopya ve `overlay`. Sonuç panelinde "Çerçevele" düğmesi ile dolgu, köşe ve arka plan seçimi olur. Çıktının boyutu ve köşe pikselinin arka plan renginde olduğu lavfi ile ölçülür.

### EKB-P9 — Otomatik Ve Canlı Yakınlaştırma
- Satırlar: EKB-01, EKB-13
- İş: büyük
- Karar: sahip: Yakınlaştırma kayıttan sonra mı üretilsin (imleç ve tıklama verisi videodan ayrı kaydedilir), kayıt sırasında canlı mı; ikisinden biri seçilir.
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.App/Recorder/RecorderMagnifier.cs, src/VidShrink.Core/, src/VidShrink.App/Editing/, src/VidShrink.App/Locales/
- Plan: Kayıt sonrası yol seçilirse fare kancası imleç konumunu ve tıklamaları zaman damgasıyla yan dosyaya yazar; kayıt bitince bu izden yakınlaşma bölgeleri çıkarılır ve `zoompan` ya da zamana bağlı `crop` ile `scale` zinciriyle yeni dosya üretilir, bölgeler düzenleyicide elle düzeltilir. Canlı yol seçilirse büyüteç merceği yerine kadrajın tamamı kısayolla yaklaşır. İz dosyası ve bölge planı saf fonksiyon olarak test edilir, çıktı karesi lavfi ile ölçülür.

### EKB-P10 — İmleç Cilası Ve Hareket Bulanıklığı
- Satırlar: EKB-03, EKB-31
- İş: büyük
- Karar: sahip: İmleç kayda yakılmak yerine ayrı veri olarak tutulsun mu; EKB-P9'da kayıt sonrası yol seçilmeden bu paket başlamaz.
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.Core/, src/VidShrink.App/Editing/, src/VidShrink.App/Locales/
- Plan: Kayıt imleçsiz alınır, imleç izi EKB-P9'un yan dosyasından okunur. İz yumuşatılır, imleç seçilen boyutta yeniden çizilip `overlay` ile bindirilir; hızlı geçişlere kare karıştırma (`tmix`) ile bulanıklık eklenir. Yumuşatma saf fonksiyon olarak, imlecin karedeki yeri lavfi ile ölçülür.

### EKB-P11 — Kamera Arka Planı
- Satırlar: EKB-07
- İş: büyük
- Karar: sahip: Kişiyi arka plandan ayıran model ve onu çalıştıran kitaplık uygulamayla dağıtılsın ya da ilk kullanımda indirilsin mi.
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.App/Recorder/RecorderView.Kamera.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, tools/, src/VidShrink.App/Locales/
- Plan: Kamera karesi kişi ayırma modelinden geçirilir (rakipler ONNX Runtime adlı model çalıştırma kitaplığını kullanıyor), çıkan maske ile arka plan bulanıklaştırılır, silinir ya da değiştirilir. Bugünkü sabit arka plan farkı ve yeşil perde seçenekleri yanında kalır (RecorderArguments.cs:926-935). Model ve kitaplık libmpv gibi sha256 özetiyle indirilir. Önce modelin boyutu ve kare hızı `tools/VidShrink.Bench` ile ölçülür, karar o sayıya göre verilir.

### EKB-P12 — En-Boy Sığdırma Ön Ayarı
- Satırlar: EKB-19
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Locales/
- Plan: Düzenleyicideki en-boy ön ayarları bugün ortadan kırpıyor (EditorView.Klip.cs:17, ClipEffects.cs:96-113). Yanına "sığdır" kipi eklenir: kare `scale` ile küçültülür, kalan alan `pad` ile seçilen düz renkle doldurulur; bu ikili dışa aktarımda zaten var (ClipFilters.cs:60-61). Çıktı boyutu ve dolgu pikseli düzenleyicinin dışa aktarım testlerinde ölçülür.

### EKB-P13 — Kamera Katmanı Mac Ve Linux
- Satırlar: EKB-18
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/RecorderArguments.cs, src/VidShrink.Ffmpeg/, src/VidShrink.App/Recorder/RecorderView.Kamera.cs, src/VidShrink.App/Locales/
- Plan: Kamera girdisi macOS'ta `avfoundation`, Linux'ta `v4l2` ile açılır (ffmpeg'in o sistemlerdeki aygıt girdileri); aygıt listesi aynı yoldan okunur ve panel yalnız Windows koşulundan çıkar (RecorderView.Kamera.cs:53, RecorderArguments.cs:894-895). Kardeş tablodaki EKA-07 ile aynı iştir, yalnız biri yapılır. Argüman üretimi her sistem için `KaydediciKameraTests` içinde ölçülür; gerçek aygıt denemesi o sistemlerde elle yapılır.

### EKB-P14 — Kayan Sonuç Kartı
- Satırlar: EKB-24
- İş: orta
- Karar: sahip: Kayıt bitince ekran köşesinde kendiliğinden yeni bir pencere açılsın mı ve bu varsayılan açık mı gelsin.
- Dokunacağı yer: src/VidShrink.App/Recorder/, src/VidShrink.App/Recorder/RecorderView.axaml.cs, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Locales/
- Plan: Kayıt bitince köşede küçük resimli, birkaç saniye sonra kaybolan bir kart açılır; dosya karttan başka uygulamaya sürüklenir (Avalonia'nın sürükle-bırak yolu, dosya verisiyle). Kart `CaptureAffinity` ile sonraki kayıttan çıkarılır. Kartın açılıp kapanması ve sürüklenen verinin dosya yolunu taşıdığı başsız pencere testiyle ölçülür.

### EKB-P15 — Kayıtta Bildirim Ve Simge Gizleme
- Satırlar: EKB-20, EKB-21
- İş: orta
- Karar: sahip: Uygulama kayıt sırasında sistemin bildirim ayarına ve masaüstü simgelerine dokunsun mu.
- Dokunacağı yer: src/VidShrink.App/Recorder/, src/VidShrink.App/Recorder/RecorderSettings.cs, src/VidShrink.App/Recorder/RecorderView.axaml, src/VidShrink.App/Locales/
- Plan: Kayıt başlarken bildirimler susturulur ve masaüstü simgeleri gizlenir, bitince eski durum geri yüklenir; eski durum diske yazılır ki çökmeden sonra açılışta geri alınsın. Windows'ta bildirim susturmanın belgeli yolu doğrulanmadı; bulunamazsa EKB-20 yapılmaz, yalnız simge gizleme teslim edilir. EKB-20 kardeş tablodaki EKA-27 ile aynı iştir. Geri yükleme sahte sistem katmanıyla, çökme senaryosu dahil ölçülür.

### EKB-P16 — Özel Hedef Ve Kendi Bulutun
- Satırlar: EKB-28, EKB-29
- İş: büyük
- Karar: sahip: Kullanıcının girdiği sunucu adresi ve erişim anahtarı uygulamada saklansın mı, bulut hesabına tarayıcıdan giriş eklensin mi.
- Dokunacağı yer: src/VidShrink.Core/Share/, src/VidShrink.App/Share/, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/
- Plan: Önce hedef tablosuna arayüzden satır ekleme gelir: adres, alan adları, isteğe bağlı anahtar; tablo bugün yalnız elle JSON düzenleyerek genişliyor (ShareTargets.cs:188-229, IShareProvider.cs:60-80). Ardından S3 uyumlu depo (Amazon'un dosya deposu protokolü, başka servisler de konuşur) için yeni sağlayıcı yazılır; Google Drive tarayıcıdan giriş istediği için en sona kalır. Anahtar işletim sisteminin korumalı deposunda tutulur. Sağlayıcılar sahte taşıyıcıyla, ağa çıkmadan ölçülür.

### EKB-P17 — Girdi Efektleri Mac Ve Linux
- Satırlar: EKB-14
- İş: büyük
- Karar: sahip: macOS'ta girdi izleme izni istensin mi, ve Wayland'da genel tuş dinleme yolu yoksa Linux desteği X11 ile mi sınırlansın.
- Dokunacağı yer: src/VidShrink.App/Recorder/RecorderInput.cs, src/VidShrink.App/Recorder/RecorderView.Girdi.cs, src/VidShrink.App/Recorder/RecorderMagnifier.cs, src/VidShrink.App/Locales/
- Plan: Boş dinleyicinin yerine macOS ve X11 için gerçek fare ve klavye dinleyicileri yazılır (RecorderView.Girdi.cs:14-22); Wayland ve X11, Linux'un iki ayrı pencere sistemidir. Büyüteç için ekran kopyası o sistemlerin kendi çağrısıyla alınır. İzin verilmediğinde efekt kapalı kalır ve kullanıcıya söylenir. Dinleyiciler arayüzün arkasında sahte olay akışıyla ölçülür; gerçek deneme o sistemlerde elle yapılır.

## Pakete Girmeyenler

- EKB-30 — Öncelik 3, tek rakipte (Screen Studio); orta iş ve hiçbir paketin yanında bedavaya gelmiyor.
- EKB-32 — Öncelik 3; K11'in bu işi kapsayıp kapsamadığı sahibin kararı, karar gelmeden paket açılmaz.
- EKB-34 — Öncelik 3, tek rakipte (CleanShot X); kendi sunucumuzu ya da ücretli bulut hizmetini gerektirir.
- EKB-35 — Öncelik 3; sunucu işletmeyi gerektirir, bugünkü anahtarsız yükleme hedefleriyle yapılamaz.
- EKB-36 — Öncelik 3, tek rakipte (ShareX); üçüncü taraf kısaltma servisine bağımlılık getirir.
- EKB-37 — Kapalı karar K5 (otomatik altyazı).
- EKB-38 — Kapalı karar K5 (konuşmadan metne dayanıyor).
- EKB-39 — Kapalı karar K9 (ekran görüntüsü aracı).
