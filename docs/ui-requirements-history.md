# VidShrink Arayüz Talep Geçmişi

Bu dosya, VidShrink için kullanıcı tarafından bugüne kadar iletilen bütün arayüz taleplerinin kalıcı kaydıdır. Gelecekte yapılacak arayüz çalışmalarında bu belge tasarım kontrol listesi olarak kullanılmalıdır. Yeni bir talep önceki bir maddeyi açıkça değiştiriyorsa en yeni talep geçerlidir.

Son güncelleme: 17 Ağustos 2026

## 1. Genel Tasarım Dili

- Arayüz `teknesyum-ui` neon standardına uymalıdır.
- Renk ve ölçüler gelişigüzel belirlenmemeli; öncelikle `Themes/Theme.xaml` içindeki ortak tasarım tokenları kullanılmalıdır.
- WPF'in varsayılan gri veya beyaz kontrol görünümü kullanılmamalıdır.
- Genel zemin koyu ve neon temayla uyumlu olmalıdır.
- Neon vurgu renkleri özellikle başlıklarda, kenarlıklarda, seçili durumlarda, kaydırıcılarda ve eylem kontrollerinde kullanılmalıdır.
- Yazılarda parlama veya bulanık gölge efekti kullanılmamalıdır. Metinler düz, keskin ve kolay okunur olmalıdır.
- Nötr metin rengi gri olmamalı; tam beyaz kullanılmalıdır.
- Pembe ve mor tonlar sürekli metin rengi olarak kullanılmamalıdır; bu renkler hover, odak, seçili durum ve ince vurgu ayrıntılarıyla sınırlandırılmalıdır.
- Başlık ve alan etiketlerinde neon mavi, normal içerik ve değerlerde tam beyaz tercih edilmelidir.
- Hedef kaydırıcısının dolu bölümü pembe dolgulu ve mavi anahatlı; sürükleme noktası ters renk düzeniyle mavi dolgulu ve pembe anahatlı olmalıdır.
- Kaydırıcı fareyle üzerine gelindiğinde sürükleme noktasında neon geri bildirim göstermelidir.
- Hedef boyutun ana sayısal değeri neon mavi ve kalın gösterilerek hazır seçimlerden daha baskın olmalıdır; hazır seçim düğmelerinin metni beyaz kalmalıdır.
- Aynı işlev grubunda ikincil değerler ana değerden daha güçlü renkle vurgulanmamalıdır.
- Çıktı panelindeki `Çıktı`, `Aşama`, `Kalan` ve `Güncel Çıktı Boyutu` başlıkları neon mavi olmalıdır.
- `Hedef` ve `Çıktı` panelleri aynı kesin yüksekliğe sahip olmalıdır; içerik ölçümüne bağlı küçük farklar bırakılmamalıdır.
- Çıktı panelinin üst ilerleme çubuğu kalın görünmemeli; yüksekliği `5 DIP` olmalıdır.
- Başlık ile alt içerik arasında görsel hiyerarşi kurulmalıdır: başlık neon renkli, başlığa bağlı açıklama veya değer beyaz olmalıdır.
- Eş panellerde başlık ile ilk içerik arasındaki dikey boşluk aynı olmalı; `Kaynak` ve `AI Ayarları` panelleri `14 DIP` kullanmalıdır.
- Uygulama adı `VIDSHRINK` olarak tamamen büyük yazılmamalı; marka yazımı her yerde `VidShrink` olmalıdır.
- Ana pencerenin özel başlık çubuğundaki `VidShrink` adı yeterlidir; içerik alanında aynı başlık ikinci kez tekrarlanmamalı, yalnızca açıklama gösterilmelidir.
- Ana pencere açıklamasının Türkçe metni `Boyut Hedefli Media Sıkıştırma & Media Converter` olmalıdır.
- Panel başlıkları neon mavi olmalıdır.
- Alan başlıkları ve ikincil bölüm başlıkları okunaklı neon mavi kullanmalıdır; pembe ve mor etkileşim vurgularına ayrılmalıdır.
- Sayısal değerler ve komutlar mono yazı tipiyle gösterilmelidir.
- Görünür Windows yerel başlık çubuğu ve yerel küçültme, büyütme veya kapatma düğmeleri kullanılmamalıdır.
- Pencerenin üst kısmı neon temalı özel, sürüklenebilir bir başlık paneli olmalıdır.
- Özel başlık panelinde neon küçültme, büyütme/geri yükleme ve kapatma düğmeleri bulunmalıdır.
- Pencere kenarından yeniden boyutlandırma ve başlığa çift tıklayarak büyütme davranışı korunmalıdır.
- Alt kısımda FFmpeg yolu gibi teknik durum bilgisi sürekli gösterilmemelidir.
- Uygulama ikonu, camgöbeği sıkıştırma parantezleri ve pembe oynat simgesinden oluşan seçilmiş A prototipi olmalıdır.
- İkonun dış arka planı gerçek alfa şeffaflığına sahip olmalı; siyah veya koyu kare tuval görünmemelidir.
- Aynı ikon EXE, Windows görev çubuğu, pencere kimliği ve özel neon başlık panelinde kullanılmalıdır.

## 2. Tipografi

- Ana arayüz yazı tipinde ciddi veya kurumsal görünümden önce azami okunurluk hedeflenmelidir.
- İlk deneme adayı, özellikle harf ayrımı ve düşük görme okunurluğu için tasarlanan `Atkinson Hyperlegible Next` ailesidir.
- Seçilen font `ç, ğ, ı, İ, ö, ş, ü` dahil bütün Türkçe karakterleri eksiksiz desteklemelidir.
- Font uygulamayla birlikte paketlenmeli; kullanıcının bilgisayarında ayrıca kurulu olmasına güvenilmemelidir.
- Komut ve sayısal değerlerde `Consolas`, yedek olarak `Cascadia Mono` kullanılmalıdır.
- Normal arayüz metinleri 16 DIP altına inmemelidir.
- Etiketler, düğmeler, açılır listeler, giriş alanları, onay kutuları, sekmeler, açıklamalar ve değerler normalde en az 16 DIP olmalıdır.
- Bölüm başlıkları 20 DIP veya daha büyük olabilir.
- Yalnızca gerçekten ikincil yardım ve durum metinlerinde 14 DIP istisnasına izin verilebilir.
- Hiçbir metin 14 DIP değerinin altına düşmemelidir.
- `FFMPEG KOMUTU` gibi küçük alan başlıkları da rahatça okunabilmelidir.
- Başlıklar dahil genel arayüz metinleri TAM BÜYÜK HARFLE yazılmamalıdır.
- Kısa arayüz metinlerinde (başlık, alan etiketi, düğme, seçenek ve kısa durum adı) her kelimenin ilk harfi büyük yazılmalıdır.
- Uzun açıklamalar, yardım metinleri ve paragraflar doğal cümle biçiminde kalmalıdır.
- `CRF`, `FPS`, `HDR`, `AI`, `FFmpeg`, `TR`, `EN`, kodek ve dosya biçimi adları gibi gerçek kısaltmalar kendi doğru yazımını koruyabilir.

## 3. Dil Desteği

- Arayüz Türkçe ve İngilizce çalışmalıdır.
- Programın varsayılan başlangıç dili Türkçe olmalıdır.
- Kullanıcı çalışma sırasında `TR` ve `EN` kontrolleriyle dil değiştirebilmelidir.
- Dil değişimi yalnızca sekme adlarını değil; düğmeleri, alan başlıklarını, seçenekleri, açıklamaları, doğrulama metinlerini, ilerleme aşamalarını ve durum çubuğunu da kapsamalıdır.
- Dil değiştirildiğinde yüklenmiş dosya, seçili değerler ve hazırlanmış dönüştürme planı kaybolmamalıdır.
- Türkçe metinler İngilizce karşılıklarından daha uzun olabileceği için yerleşim Türkçe görünüm esas alınarak doğrulanmalıdır.

## 4. Açılır Listeler

- `Sharing` gibi seçili değerler ilk bakışta net şekilde okunmalıdır.
- Beyaz açılır liste zemini kullanılmamalıdır.
- Açılır listelerin zemini koyu ve neon temayla uyumlu olmalıdır.
- Seçili değerler tam beyaz ve yüksek kontrastlı olmalıdır.
- Normal kenarlık neon mavi olabilir.
- Üzerine gelindiğinde mor veya pembe neon anahat gösterilmesi istenmektedir.
- Açılır liste içindeki vurgulanmış satır okunabilir bir metin–zemin kontrastına sahip olmalıdır.
- Yan yana açılır listeler birbirine yapışmamalıdır.
- Küçült ekranındaki amaç ve kodek seçicileri arasında en az 16 DIP boşluk bulunmalıdır.
- Dönüştür ekranındaki iki sütunlu alanlar arasında belirgin yatay boşluk olmalıdır.
- Açılır menü seçenekleri de 16 DIP yazı boyutunu korumalıdır.

## 5. Alan ve Panel Yerleşimi

- Kontroller ve paneller sıkışık veya birbirine yapışık görünmemelidir.
- Normal arayüz öğelerinde tam dikdörtgen kullanılmamalı; bütün köşeler küçük ve dengeli biçimde yuvarlatılmalıdır.
- Genel köşe yarıçapı `6 DIP` olmalı; geniş, kapsül benzeri veya aşırı yuvarlak köşelerden kaçınılmalıdır.
- Uygulama penceresinin en dışında tüm pencereyi çevreleyen `1 DIP` neon mavi anahat bulunmalıdır.
- Özel üst başlık çubuğunda okunurluğu koruyan koyu yatay gradient kullanılmalıdır: sol tarafta hafif neon mavi, sağ tarafta düşük yoğunluklu mor/pembe ton bulunmalıdır.
- Üst bar yüksekliği kompakt `46 DIP` olmalı ve gradient en az beş yakın renk durağıyla yumuşak geçmelidir.
- Üst barın altındaki açıklama ve dil şeridi de daha düşük yoğunluklu merkezi bir gradient kullanmalıdır.
- Sponsor ve imza bağlantıları Hakkında sekmesinde tekrarlanmamalı; üst başlık çubuğunda pencere düğmelerinin solunda yer almalıdır.
- Sağdan sola sıralama küçült düğmesi, `GitHub / By Teknesyum`, `Buy Me A Coffee` olmalıdır.
- Üst bar bağlantıları normalde beyaz, hover sırasında neon pembe ve altı çizili olmalıdır.
- Dairesel `?` yardım rozeti ve kaydırıcı tutamacı gibi işlevi gereği yuvarlak öğeler bu kuralın istisnasıdır.
- Aynı satırdaki kontroller asimetrik görünmemelidir; bir kontrolün sol köşeleri yuvarlatılmışsa sağ köşeleri de aynı yarıçapla yuvarlatılmalıdır.
- Eş görev seviyesindeki yan yana kontrollerin dış yükseklikleri mümkün olduğunca aynı olmalıdır.
- Bir alan içindeki kaydırıcı ve sayısal değer gibi bağlı kontroller gereksiz yere üst üste konarak komşu alanı daha uzun göstermemeli; uygun olduğunda aynı satırda hizalanmalıdır.
- Dönüştür formundaki iki sütun arasında 16 DIP yatay oluk bulunmalıdır.
- Form satırları arasında yaklaşık 18 DIP dikey ritim bulunmalıdır.
- Alan başlığı ile bağlı kontrol arasında yaklaşık 6 DIP boşluk bulunmalıdır.
- CRF kaydırıcısı ile sayısal giriş alanı birbirinden ayrılmalıdır.
- Sekme içeriği gerektiğinde kaydırılabilir olmalı; ancak normal başlangıç boyutunda kullanıcı mümkün olduğunca aşağı kaydırmak zorunda kalmamalıdır.
- Program başlangıçta daha büyük açılmalıdır.
- Hedef başlangıç boyutu 1440×1000'dır; pencere mevcut Windows çalışma alanını aşmamalıdır.
- Daha küçük ekranlarda içerik kaybolmamalı, sekme içi kaydırma devreye girmelidir.
- Minimum pencere boyutunda bile kontroller üst üste binmemeli veya kırpılmamalıdır.

## 6. Kaydırıcılar ve İlerleme Çubukları

- `0–500 MB` hedef çubuğu varsayılan WPF görünümünde veya fazla sade olmamalıdır.
- Kaydırıcılarda neon tema belirgin biçimde hissedilmelidir.
- Kaydırıcı yolu koyu camgöbeği tonunda olmalıdır.
- Dolu bölüm neon camgöbeği olmalıdır.
- Tutamaç pembe/camgöbeği neon vurgulu olmalıdır.
- Tutamaç ve yol rahatça görülecek büyüklükte olmalıdır.
- Kaydırıcı yolu iki uçta da tamamen görünmeli; tutamaç kontrol sınırlarında kırpılmamalıdır.
- Kaydırıcı tutamacı yolun dikey merkezinde olmalı, üstte veya altta yarım görünmemelidir.
- Hedef boyut değeri ile `MB` birimi ayrı yerleşim sütunlarında tutulmalı ve daralmada birim kırpılmamalıdır.
- Dönüştür ekranındaki CRF/bit hızı kaydırıcısı aynı tasarım sistemini kullanmalıdır.
- İlerleme çubukları da koyu neon yol ve camgöbeği dolgu kullanmalıdır.
- Neon görünüm metin parlamasıyla değil, kontrol yüzeyleri ve kenarlıklarıyla sağlanmalıdır.

## 7. Sekme Yapısı

- Ana pencere üç sekmeli olmalıdır: `KÜÇÜLT / SHRINK`, `DÖNÜŞTÜR / CONVERT`, `HAKKINDA / ABOUT`.
- TabControl ve TabItem görünümleri neon temaya özel tasarlanmalıdır.
- WPF'in varsayılan gri sekmeleri kabul edilmez.
- Aktif sekme belirgin olmalı; üzerine gelme durumu da neon vurgu taşımalıdır.
- Sekme başlıkları en az 16 DIP olmalıdır.

## 8. Küçült Sekmesi

- Kaynak seçimi, hedef boyut, otomatik/AI planı, yapılacak işlem, ffmpeg komutu, ilerleme ve çıktı bilgileri tek iş akışı içinde görünmelidir.
- `Paylaşım / Sharing` ve `Uyumlu - H.264 / Compatible - H.264` metinleri eksiksiz ve yüksek kontrastla okunmalıdır.
- Amaç ve kodek seçicileri yan yana durabilir ancak aralarında açık boşluk olmalıdır.
- Hedef boyut kaydırıcısı neon temalı olmalıdır.
- Hedef boyut değerinin yazıldığı alan beyaz ve okunaklı olmalıdır.
- `Çözünürlük Düşürülebilir` ve `Kare Hızı Düşürülebilir` seçenekleri rahat okunmalıdır.
- `Yapılacak İşlem` başlığı renkli olmalı; plan özeti ve gerekçesi beyaz olmalıdır.
- CRF kullanıldığında arayüz kesin dosya boyutu uydurmamalı; hedef tavanı ve gerekirse düzeltileceğini dürüstçe belirtmelidir.
- AI paneli isteğe bağlı görünmeli; otomatik motorun varsayılan olduğu anlaşılmalıdır.
- İlerleme aşaması, kalan süre ve güncel çıktı boyutu açıkça gösterilmelidir.

## 9. Dönüştür Sekmesi

- Hedef boyut zorunluluğu olmadan format ve kodek dönüşümü sunmalıdır.
- Hedef kapsayıcı seçenekleri: MP4, MKV, WebM, MOV, AVI, GIF, MP3, M4A ve WAV.
- Video kodeği seçenekleri: H.264, H.265, VP9, AV1 ve Kopyala.
- Kalite seçenekleri: CRF veya Sabit Bit Hızı.
- Çözünürlük seçenekleri: Kaynak, 2160, 1440, 1080, 720, 480 ve Özel.
- Kare hızı seçenekleri: Kaynak, 60, 30, 24 ve Özel.
- Ses için kodek, bit hızı, Kopyala ve At seçenekleri bulunmalıdır.
- Başlangıç ve bitiş zamanı ile isteğe bağlı kırpma yapılabilmelidir.
- Çalıştırılacak ffmpeg komutu dönüşüm başlamadan önce görünmelidir.
- İlerleme, iptal ve klasörde gösterme kontrolleri bulunmalıdır.
- Alan başlıkları beyaz ve renksiz bırakılmamalı; neon renkle ayrıştırılmalıdır.
- Alanların altındaki seçili değerler ve girilen değerler tam beyaz olmalıdır.
- Form alanları hem yatay hem dikey yönde birbirinden ayrılmalıdır.
- Normal pencere boyutunda mümkün olduğunca kaydırmadan kullanılabilmelidir.

## 10. Bağlamsal Ayar Yardımı

- Kullanıcının `kodek`, `CRF`, `bit hızı`, `stream copy` veya kapsayıcı gibi teknik terimleri bildiği varsayılmamalıdır.
- Önceki tek ve görünür Hızlı Ayar Rehberi talebi iptal edilmiştir; bütün açıklamalar ayrı bir panelde toplanmamalıdır.
- Açıklama gerektiren her ayarın başlığının yanında yuvarlak içinde `?` simgesi bulunmalıdır.
- `?` rozeti 16×16 DIP boyutunda küçük olmalı ve bağlı yazının sağ üstünde matematikteki üs işareti gibi konumlanmalıdır.
- Fare `?` simgesinin üzerine geldiğinde ilgili ayarın ayrıntılı açıklaması gösterilmelidir.
- Yardım balonu koyu neon zemine, neon kenarlığa, tam beyaz metne ve en az 16 DIP yazı boyutuna sahip olmalıdır.
- Yardım içeriği Türkçe ve İngilizce dil geçişini takip etmelidir.
- H.264 için geniş cihaz, TV, telefon ve internet uyumluluğu açıklanmalıdır.
- H.265 için daha küçük dosya avantajı, daha uzun işlem süresi ve eski cihaz uyumluluğu riski açıklanmalıdır.
- VP9'un WebM ve tarayıcı kullanımı için uygun olduğu açıklanmalıdır.
- AV1'in daha küçük modern dosyalar üretebildiği ancak en yavaş seçenek olduğu açıklanmalıdır.
- Kopyala seçeneğinin yeniden kodlama yapmadan kaliteyi koruduğu, ancak kapsayıcı desteğine bağlı olduğu açıklanmalıdır.
- CRF'nin görsel kaliteyi seçtiği, dosya boyutunun değişebileceği ve düşük sayının daha yüksek kalite/daha büyük dosya anlamına geldiği anlatılmalıdır.
- Sabit Bit Hızının daha öngörülebilir boyut gereken durumlara uygun olduğu anlatılmalıdır.
- Kaynak ve Özel çözünürlük/kare hızı seçenekleri açıklanmalıdır.
- Ses Kopyala ve At seçeneklerinin etkileri açıklanmalıdır.
- Başlangıç/Bitiş alanlarının yalnızca seçilen zaman aralığını dönüştürdüğü açıklanmalıdır.
- Yardım yalnızca ihtiyaç duyulan yerde görünmeli ve ana formu gereksiz yere uzatmamalıdır.

## 11. Hakkında Sekmesi

- Programın ne yaptığını kısa ve anlaşılır biçimde anlatmalıdır.
- Hedef boyut yaklaşımını açıklamalıdır.
- Otomatik karar sırasını açıklamalıdır: ses bütçesi, bit hızı, piksel başına bit tablosu, çözünürlük merdiveni ve boyut düzeltme turu.
- Otomatik karar tablosu görünür olmalıdır.
- AI modunun yalnızca istem oluşturduğu ve yapıştırılan JSON'u doğruladığı belirtilmelidir.
- Gömülü AI bulunmamasının nedenleri açıklanmalıdır: çevrimdışı çalışma, API anahtarı gerektirmeme, yanıt doğrulama ve hatada otomatik motora dönüş.
- Kodeklerin hangi durumda tercih edilebileceği açıklanmalıdır.
- ffmpeg yolu ve sürümü, .NET sürümü ve uygulama sürümü gösterilmelidir.
- GitHub bağlantısı ile imza/destek bloğu yalnızca bir kez bulunmalı ve Hakkında sekmesinin en altında yer almalıdır.

## 12. Erişilebilirlik ve Okunabilirlik Kabul Ölçütleri

- Beyaz zeminli açılır liste kalmamalıdır.
- Gri nötr yazı kalmamalıdır.
- Parlama efektli yazı kalmamalıdır.
- Normal arayüz yazısı 16 DIP altına düşmemelidir.
- İkincil yardım metni 14 DIP altına düşmemelidir.
- Başlıklar ile değer/açıklamalar yalnızca konumla değil renk hiyerarşisiyle de ayrılmalıdır.
- Yerel Windows onay kutusu görünümü kullanılmamalı; neon temalı özel kutu ve tik çizilmelidir.
- Özel onay kutusunun içindeki tik neon mavi olmalıdır; pembe tik kullanılmamalıdır.
- Onay kutusu, tik ve yanındaki yazı aynı dikey eksende ortalanmalıdır.
- Yan yana kontroller birbirine değmemelidir.
- Üst gezinme sekmeleri de bu ayrıklık kuralına uymalı; sekmeler arasında `8 DIP` boşluk bulunmalıdır.
- Sekme ve panel anahatları dört kenarda kesintisiz kapanmalı; alt kenarlar yerleşim alanı tarafından kırpılmamalıdır.
- Pencere genelinde tam piksel yerleşimi ve piksele hizalı kenarlık çizimi kullanılmalıdır.
- Yan yana kontrollerin köşe geometrileri ve yükseklikleri görsel olarak dengeli olmalıdır.
- Türkçe uzun metinler kesilmemeli veya komşu kontrolün üzerine taşmamalıdır.
- Seçili açılır liste değeri kapalı durumda da açık durumda da okunmalıdır.
- Neon tema karanlık zeminde kontrastı azaltmamalıdır.
- Mavi metinlerin arkasında mavimsi koyu yüzey kullanılmamalı; kontrol ve panel zeminleri nötr kömür tonunda olmalıdır.
- Genel yüzey tonu bileşenlerde sabit renk olarak tekrarlanmamalı; `Theme.xaml` içindeki tek `SurfaceToneColor` kaynağına bağlanmalıdır.
- Üst bar gradienti de bileşen içinde tanımlanmamalı; merkezi `TitleBarBackground` tema kaynağından gelmelidir.
- Başlangıç penceresi mümkün olan çalışma alanını kullanmalı ve tipik 1440×1000 çalışma alanında Küçült sekmesi kaydırmasız görünmelidir.

## 13. Doğrulama Beklentileri

- Yalnızca derleme yapmak yeterli değildir; uygulama gerçekten açılmalıdır.
- Üç sekme gerçek uygulama penceresinde gezilerek yerleşim kontrol edilmelidir.
- Türkçe başlangıç görünümü ve İngilizce geçişi ayrı ayrı kontrol edilmelidir.
- Açılır listelerin kapalı, açık, seçili ve üzerine gelinmiş durumları incelenmelidir.
- Küçük ve normal pencere boyutlarında taşma, üst üste binme ve gereksiz kaydırma kontrol edilmelidir.
- Kaydırıcı ve ilerleme çubuklarının neon teması gerçek WPF çiziminde doğrulanmalıdır.
- Kullanıcıya sunulan rapor, gerçekten doğrulananlarla yalnızca kaynak üzerinden kontrol edilenleri birbirinden ayırmalıdır.

## 14. Uygulanan Başlıca Düzeltmelerin Kaydı

- Türkçe varsayılan dil ve çalışma zamanında TR/EN geçişi eklendi.
- Beyaz yerel WPF açılır listeleri koyu neon şablonla değiştirildi.
- Açılır liste yazıları tam beyaz ve 16 DIP yapıldı.
- Küçült amaç/kodek seçicileri ve Dönüştür form alanları birbirinden ayrıldı.
- Metin parlama efektleri kaldırıldı.
- Genel tipografi 16 DIP seviyesine yükseltildi; ikincil metin alt sınırı 14 DIP yapıldı.
- Gri nötr metinler tam beyaza çevrildi.
- Alan ve bölüm başlıkları neon maviye, değerler ve normal içerik tam beyaza taşındı; pembe ve mor etkileşim vurgularına ayrıldı.
- Kaydırıcılar ve ilerleme çubukları neon temayla yeniden çizildi.
- Başlangıç penceresi çalışma alanına bağlı 1440×1000 hedef boyuta yükseltildi.
- Tek Hızlı Ayar Rehberi kaldırıldı; teknik alanların yanına Türkçe/İngilizce ayrıntılı `?` yardım balonları eklendi.
- Kısa başlık, alan etiketi, düğme, seçenek ve durum metinleri her kelimenin ilk harfi büyük olacak biçimde düzenlendi; uzun açıklamalar doğal cümle düzeninde bırakıldı.

## 15. İlgili Dosyalar

- `src/VidShrink.App/Themes/Theme.xaml`
- `src/VidShrink.App/Fonts/`
- `src/VidShrink.App/App.xaml`
- `src/VidShrink.App/MainWindow.xaml`
- `src/VidShrink.App/MainWindow.xaml.cs`
- `src/VidShrink.App/LanguageCatalog.cs`
- `docs/implementation-report.md`

Bu belge yeni arayüz talepleri geldikçe güncellenmelidir. Yeni düzenlemeler mevcut maddelerden sapıyorsa değişikliğin tarihi, önceki kural ve yeni kural açıkça kaydedilmelidir.
