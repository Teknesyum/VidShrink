# Küçült Sekmesi, Karşılaştırma Paneli Ve İş Kuyruğu — Doğrulanmış Eksikler

13 Ekim 2026. Kod okuması `origin/main` 9de48d8b. Kaynak numaraları `kucult.md` içindeki Kaynaklar bölümüne bakar. Sıra araştırma kolunun ilk sıralamasıdır (01–03 öncelik 1, 04–19 öncelik 2, 20–46 öncelik 3); doğrulama turunda üç satırın önceliği değişti: dördüncü ve altıncı satır 2'den 1'e çıktı, on yedinci satır 2'den 3'e indi. No'lar korundu, satır çıkarılmadı. Değişen her hücre Düzeltmeler bölümünde kanıtıyla yazılı.

| No | Eksik | Kimde Var | Bizde Durum | Fayda | İş | Öncelik | Karar | Kaynak |
|---|---|---|---|---|---|---|---|---|
| SIK-01 | Koşan kodlamayı duraklatma: süren kodlama askıya alınır, sonra kaldığı yerden sürer. | HandBrake, VidCoder, FFmpeg Batch AV Converter, Shutter Encoder ve 1 rakip daha | yarım: duraklatma yalnız sıradaki işi bekletir, koşan dosya biter (src/VidShrink.App/ShrinkJobWindow.axaml.cs:390); koşan süreç yalnız başlatılıyor ve iptalde öldürülüyor, askıya alınmıyor (src/VidShrink.Ffmpeg/EncodeRunner.cs:820) | Kullanıcı uzun bir kodlamayı iptal etmeden bilgisayarını bir süreliğine geri alır. | orta | 1 | gerekmez | [1][8][10][15][17] |
| SIK-02 | Kodlama süreç önceliği: kodlayıcının işlemci önceliği ayardan düşürülür. | HandBrake, FFmpeg Batch AV Converter, FastFlix | yok; kodlama süreci öncelik atanmadan açılıyor (src/VidShrink.Ffmpeg/EncodeRunner.cs:815), düşük öncelik yalnız yardımcı süreçlerde, örneğin düzenleyicinin küçük resim sürecinde (src/VidShrink.Core/Editing/ThumbnailQueue.cs:276) | Kodlama sürerken bilgisayar başka işlerde takılmaz. | küçük | 1 | gerekmez | [5][7][15][17] |
| SIK-03 | Dış ses izi ekleme: ayrı bir ses dosyası videoya eklenir ya da var olan sesin yerine konur. | Shutter Encoder, FFmpeg Batch AV Converter, LosslessCut | yok; en yakın kod dış altyazı dosyası ekleme (src/VidShrink.App/MainWindow.Izler.cs:177) | Sesi ayrı kaydedilmiş ya da düzeltilmiş videoyu tek adımda küçültür; HandBrake'in en çok oy alan açık isteği (62 oy). | orta | 1 | gerekmez | [6][10][11][18] |
| SIK-04 | Yakılan altyazıda biçim: görüntüye gömülen altyazının yazı tipi, boyutu ve arka planı seçilir. | Shutter Encoder, Subtitle Edit | yarım: altyazı yakma var, yazı tipi ve boyut seçimi yok (src/VidShrink.Core/VideoFilterChain.cs:205); biçimli altyazı belgesi yazan kod yalnız düzenleyicinin metin katmanında (src/VidShrink.Core/Editing/AssWriter.cs:120) | Küçültülmüş videoda altyazı okunur kalır, telefonda küçük görünmez; HandBrake'te 40 oyla açık istek, VidCoder'da da açık. | orta | 1 | gerekmez | [6][9][10] |
| SIK-05 | Kuyruğun saklanması: uygulama kapanınca ya da çökünce bekleyen işler kaybolmaz, açılışta geri gelir. | HandBrake, FastFlix | yok; kuyruk yalnız bellekte tutuluyor (src/VidShrink.Core/JobPump.cs:60) | Elektrik kesintisi ya da çökme sonrası onlarca dosyalık sıra yeniden kurulmaz. | orta | 2 | sahip: açılışta eski kuyruğu geri yüklemek varsayılan davranışı değiştirir | [4][6][15] |
| SIK-06 | Kodlama sürerken uykuyu engelleme: iş bitene dek bilgisayarın uykuya geçmesi önlenir. | HandBrake, Shutter Encoder, FastFlix | yok; en yakın kod bitince uyutma eylemi (src/VidShrink.App/QueueEndActions.cs:9) | Gece bırakılan kuyruk bilgisayar uyuduğu için yarıda kalmaz. | küçük | 1 | sahip: kodlama sürerken makineyi uyanık tutmak varsayılan davranışı değiştirir | [1][10][15][51][52][53][54] |
| SIK-07 | Zamanlanmış başlatma: kuyruk verilen saatte ya da verilen süre sonra kendiliğinden başlar. | HandBrake, Tdarr, FileFlows | yok; en yakın kod kuyruğu elle duraklatma (src/VidShrink.Core/JobPump.cs:50) | Ağır iş gece ya da bilgisayar boşken koşar. | küçük | 2 | gerekmez | [1][6][25][26] |
| SIK-08 | Kuyruktaki işi düzenleme: bekleyen işin hedefi ve ayarı kuyruktan çıkarmadan değiştirilir. | HandBrake, FastFlix | yok; bekleyen iş yalnız çıkarılıp taşınabiliyor (src/VidShrink.Core/JobPump.cs:66) | Yanlış hedefle eklenen dosya silinip yeniden eklenmez. | orta | 2 | gerekmez | [2][15] |
| SIK-09 | Bitince uygulamadan çık: kuyruk bitince uygulama kendini kapatır. | FastFlix, Shutter Encoder | yarım: bitince dört eylem var, uygulamadan çıkış yok (src/VidShrink.App/QueueEndActions.cs:9) | Betikle ya da gece koşan iş bittiğinde açık pencere kalmaz. | küçük | 2 | gerekmez | [11][15] |
| SIK-10 | Bildirim sesi: iş bitince ses çalar. | HandBrake, 8mb.video | yok; en yakın kod görsel iş bitti bildirimi (src/VidShrink.App/IsBittiBildirimi.cs:31) | Ekrana bakmayan kullanıcı işin bittiğini duyar. | küçük | 2 | gerekmez | [1][31] |
| SIK-11 | Kaynak dosyanın tarihini koruma: çıktı dosyası kaynağın oluşturma ve değiştirme tarihini taşır. | FFmpeg Batch AV Converter | yarım: kap üst verisi taşınıyor ya da isteğe bağlı siliniyor, dosya tarihi taşınmıyor (src/VidShrink.Core/StreamMapping.cs:335) | Küçültülen arşiv videoları galeri ve klasörde çekim tarihine göre sıralı kalır; HandBrake'te 17 oyla açık istek. | küçük | 2 | gerekmez | [6][18] |
| SIK-12 | Görsel elle kırpma: kırpma alanı önizleme üstünde fareyle çizilir. | FastFlix, FFmpeg Batch AV Converter | yarım: otomatik siyah bant kırpma ve serbest süzgeç kutusu var (src/VidShrink.App/MainWindow.axaml:1067) | Kullanıcı süzgeç sözdizimi yazmadan istediği alanı keser. | orta | 2 | gerekmez | [15][17] |
| SIK-13 | Adlandırma değişkenleri: çıktı adı deseninde saat, bit hızı, kare hızı, kaynak klasörü ve sayaç gibi ek yer tutucular bulunur. | FastFlix, HandBrake, StaxRip | yarım: altı yer tutucu var (src/VidShrink.Core/AdlandirmaDeseni.cs:21) | Toplu işte dosyalar çakışmadan ve aranabilir adlarla çıkar. | küçük | 2 | gerekmez | [1][13][15] |
| SIK-14 | Çıktının gerçek kalite puanı: biten dosyanın tamamı kaynakla ölçülür, puan gösterilir ve kaydedilir. | Shutter Encoder, ab-av1 | yarım: arayüzde kalite yalnız örnek pencerelerde ölçülüp öngörülüyor (src/VidShrink.App/MainWindow.axaml.cs:3639); komut satırı `--vmaf` ile biten çıktının tamamını ölçüyor (src/VidShrink.Cli/CliApp.cs:533), arayüzde bu ölçüm yok | Kullanıcı tahmini değil ölçülmüş kaliteyi görür ve kayıt olarak saklar. | orta | 2 | sahip: tam ölçüm iş süresini uzatır | [6][10][11][20] |
| SIK-15 | Ölçümle hedef kalite araması: hedef puan tutana dek örnek kodlanıp ölçülür, ayar ona göre seçilir. | ab-av1, Av1an, FileFlows | yarım: hedef kalite örnek ölçümüyle beslenen tahminden çözülüyor, hedefi tutturana dek arama yok (src/VidShrink.Core/PlanCalculator.cs:1170) | Seçilen kalite puanı tahminle değil ölçümle tutturulur, dosya gereğinden büyük ya da kötü çıkmaz. | büyük | 2 | sahip: her arama turu ek kodlama ve ölçüm süresi getirir | [6][19][20][26] |
| SIK-16 | VVC (H.266) kodlama: HEVC'nin ardılı olan yeni kodekle çıktı alınır. | Shutter Encoder, FastFlix, StaxRip, FFmpeg Batch AV Converter | yok; izinli kodek listesinde VVC yok (src/VidShrink.Core/PlanParser.cs:13) | Aynı boyutta daha iyi görüntü; bugün çok az aygıt oynatabiliyor. | orta | 2 | sahip: ffmpeg derlemesinde libvvenc gerekir, oynatma desteği dar | [10][11][13][14][18] |
| SIK-17 | Dolby Vision profil 8.4: iPhone'un çektiği HLG tabanlı Dolby Vision videonun üst verisi çıktıya taşınır. | HandBrake | yarım: yalnız profil 8.1 taşınıyor (src/VidShrink.Core/HdrResolver.cs:179) | Telefonla çekilen HDR video küçültülünce Dolby Vision olarak kalır. | büyük | 3 | sahip: Dolby Vision kapsam hükmünü genişletir | [1][3] |
| SIK-18 | Çıktıyı doğrulama: iş bitince çıktının süresi ve çözülebilirliği denetlenir, bozuksa iş hatalı sayılır. | ab-av1, Tdarr | yok; en yakın kod çıktıyı kısmi dosyadan asıl ada taşıma (src/VidShrink.Ffmpeg/EncodeRunner.cs:463) | Bozuk ya da kısa kalmış çıktı fark edilmeden kaynağın yerine geçmez. | küçük | 2 | gerekmez | [20][25] |
| SIK-19 | Donanım kodlayıcıda Dolby Vision: ekran kartı kodlayıcısıyla yapılan kodlamada Dolby Vision üst verisi korunur. | HandBrake, FastFlix | yarım: Dolby Vision yalnız libx265 ve libsvtav1 ile taşınıyor (src/VidShrink.Core/HdrResolver.cs:46) | Hızlı donanım yolu seçildiğinde HDR bilgisi düşmez. | büyük | 2 | sahip: ffmpeg yolu belirsiz, rakip ayrı kodlayıcı ikilisi kullanıyor | [3][15] |
| SIK-20 | İş parçacığı sınırı: kodlamanın kullanacağı çekirdek sayısı sınırlanır. | Shutter Encoder | yok; -threads yalnız yardımcı süreçlerde (ses ölçümü, sahne tespiti, küçük resim) kullanılıyor, kodlama komutunda yok (src/VidShrink.Core/AudioLevel.cs:71, src/VidShrink.Ffmpeg/SceneDetector.cs:97) | Kodlama sürerken birkaç çekirdek başka işlere kalır. | küçük | 3 | gerekmez | [6][11] |
| SIK-21 | Bitince özel komut: kuyruk bitince kullanıcının yazdığı komut çalışır. | HandBrake, FastFlix, FFmpeg Batch AV Converter | yarım: bitince dört hazır eylem var, komut çalıştırma yok (src/VidShrink.App/QueueEndActions.cs:9) | Çıktıyı yükleme ya da taşıma gibi sonraki adım kendiliğinden başlar. | küçük | 3 | sahip: kullanıcı komutu çalıştırmak güvenlik yüzeyi açar | [1][15][17] |
| SIK-22 | Uzak bildirim: iş bitince e-posta ya da ağ üzerinden uyarı gider. | Shutter Encoder, FileFlows | yok; en yakın kod yerel iş bitti bildirimi (src/VidShrink.App/IsBittiBildirimi.cs:31) | Bilgisayarın başında olmayan kullanıcı işin bittiğini öğrenir. | orta | 3 | sahip: posta sunucusu ayarı ve parola saklama gerekir | [11][26] |
| SIK-23 | macOS ve Linux'ta iş bitti bildirimi: iş bitince sistem bildirimi üç işletim sisteminde de çıkar. | doğrulanmadı (bildirim HandBrake ve vidcord'da belgeli, platform ayrımı sayfada yok) | yarım: bildirim yalnız Windows'ta (src/VidShrink.App/IsBittiBildirimi.cs:64) defterde | macOS ve Linux kullanıcısı da işin bittiğini pencereye bakmadan görür. | orta | 3 | gerekmez | [1][23] |
| SIK-24 | Başarısız işi yeniden deneme: hata veren iş kuyrukta tek tıkla yeniden sıraya girer. | vidcord | yok; en yakın kod hata veren işte görünen uygulamada aç düğmesi (src/VidShrink.App/ShrinkJobWindow.axaml.cs:695) ve işi iptal etme (src/VidShrink.Core/JobPump.cs:88) | Geçici hata yüzünden düşen dosya yeniden bulunup eklenmez. | küçük | 3 | gerekmez | [23] |
| SIK-25 | Toplam kuyruk ilerlemesi: bütün kuyruk için tek yüzde ve kalan süre gösterilir. | vidcord | yarım: çubuk koşan işlerin ortalamasını gösteriyor, kalan süre yalnız tek iş koşarken (src/VidShrink.App/ShrinkJobWindow.axaml.cs:540) | Kullanıcı bütün sıranın ne zaman biteceğini bilir. | küçük | 3 | gerekmez | [23] |
| SIK-26 | Kaynağı işlem sonunda silme: başarılı kodlamadan sonra kaynak dosya silinir. | Shutter Encoder | yok; en yakın kod çıktıyı asıl ada taşıma (src/VidShrink.Ffmpeg/EncodeRunner.cs:463) | Yer açmak için küçültülen arşivde ikinci temizlik turu gerekmez. | küçük | 3 | sahip: geri alınamaz işlem | [11] |
| SIK-27 | İzleme klasöründe alt klasörler: izlenen klasörün alt klasörlerine düşen dosyalar da alınır. | Shutter Encoder | yarım: izleme klasörü tek düzeyi yokluyor (src/VidShrink.Core/WatchFolder.cs:397) | Tarihe ya da projeye göre klasörlenen çekimler kendiliğinden işlenir. | küçük | 3 | gerekmez | [10] |
| SIK-28 | Klasör bırakma: bırakılan klasör alt klasörleriyle kuyruğa girer ve çıktıda aynı klasör yapısı korunur. | Shutter Encoder | yarım: çoklu dosya bırakma var, klasör reddediliyor (src/VidShrink.App/MainWindow.axaml.cs:3382) | Bütün bir arşiv klasörü tek hareketle küçültülür. | orta | 3 | gerekmez | [11] |
| SIK-29 | Yarıda kalan kodlamayı sürdürme: kesilen kodlama baştan değil kaldığı parçadan devam eder. | Av1an | yok; yarım dosya kısmi adla yazılıp iş bitince taşınıyor (src/VidShrink.Ffmpeg/EncodeRunner.cs:463) | Saatler süren kodlama kesintide boşa gitmez. | büyük | 3 | sahip: parçalı kodlama mimarisi gerekir | [16][19] |
| SIK-30 | Sahne bazlı parçalı kodlama: tek dosya sahnelere bölünür, parçalar aynı anda kodlanır. | Av1an, NotEnoughAV1Encodes | yarım: eşzamanlılık yalnız dosyalar arasında (src/VidShrink.Core/ParallelJobs.cs:12) | Tek uzun dosya çok çekirdekli makinede daha hızlı biter. | büyük | 3 | sahip: büyük iş, bellek ve işlemci yükü artar | [19][27] |
| SIK-31 | Karşılaştırmada yan yana kip: kaynak ve çıktı iki ayrı karede yan yana ya da üst üste gösterilir. | video-compare | yarım: yalnız bölme çizgisi ve yakınlaştırma var (src/VidShrink.App/Playback/ComparisonPanel.axaml.cs:286) | Kullanıcı aynı bölgeyi iki görüntüde aynı anda görür. | orta | 3 | gerekmez | [6][28] |
| SIK-32 | Karşılaştırmada fark görünümü: kaynak ile çıktı arasındaki fark tek görüntüde gösterilir. | video-compare | yok; en yakın kod bölme çizgili karşılaştırma (src/VidShrink.App/Playback/ComparisonPanel.axaml.cs:286) | Kaybın görüntünün neresinde olduğu bir bakışta görülür. | orta | 3 | gerekmez | [28] |
| SIK-33 | Dolby Vision profil 7'yi 8.1'e çevirme: Blu-ray kaynaklı çift katmanlı Dolby Vision tek katmana çevrilip taşınır. | HandBrake | yarım: profil 8.1 dışındaki kaynakta Dolby Vision üst verisi düşüyor ve uyarı veriliyor (src/VidShrink.Core/HdrResolver.cs:115) | Film arşivi küçültülürken Dolby Vision kaybolmaz. | büyük | 3 | sahip: Dolby Vision kapsam hükmünü genişletir, yeni araç ikilisi gerekebilir | [3] |
| SIK-34 | Dolby Vision profil 5: HDR10 tabanı olmayan akış profili taşınır. | HandBrake | yarım: profil 5 kaynağında Dolby Vision taşınmıyor (src/VidShrink.Core/HdrResolver.cs:179) | Akış hizmeti kaynaklı Dolby Vision video renkleri bozulmadan küçülür. | büyük | 3 | kapalı: Dolby Vision profil 5 kapsam dışı hükmü | [3][57] |
| SIK-35 | HDR10+ üst verisini AV1'e taşıma: sahne bazlı parlaklık bilgisi AV1 çıktısında korunur. | HandBrake, FastFlix | yarım: HDR10+ yalnız libx265 ile taşınıyor (src/VidShrink.Core/HdrResolver.cs:107) | AV1 seçildiğinde Samsung ve benzeri ekranlarda sahne bazlı HDR kaybolmaz. | büyük | 3 | sahip: ffmpeg yolu taşımıyor (bizim ölçüm 0/3), ayrı araç zinciri gerekir | [3][15][57] |
| SIK-36 | Film greni sentezi: gren kodlamadan önce silinir, oynatırken yeniden üretilir. | FastFlix | yok; en yakın kod x264 ve x265 için grain ayarı (src/VidShrink.Core/FfmpegArguments.cs:454) | Grenli film çok daha küçük boyutta benzer görünür. | küçük | 3 | gerekmez | [15] |
| SIK-37 | Ekran kartında ton eşleme: HDR'den SDR'ye çeviri ekran kartında yapılır. | Shutter Encoder | yarım: ton eşleme işlemcide zscale ve tonemap ile yapılıyor (src/VidShrink.Core/HdrResolver.cs:48) | HDR kaynağı SDR'ye çevirirken kodlama hızlanır. | orta | 3 | sahip: ffmpeg derlemesinde ek süzgeç kitaplığı gerekir | [10] |
| SIK-38 | Ek kalite ölçütleri: SSIMULACRA2 ve Butteraugli gibi algısal ölçütler hesaplanır. | Av1an | yarım: VMAF-NEG, XPSNR ve SSIM ölçülüyor (src/VidShrink.Ffmpeg/QualityMeter.cs:298) | Meraklı kullanıcı sonucu ikinci bir ölçütle sınar. | orta | 3 | sahip: ffmpeg derlemesinde yeni ölçüt kitaplığı gerekir | [19] |
| SIK-39 | SVT-AV1 çatalları: görsel kaliteye ayarlı SVT-AV1 türevleri (SVT-AV1-HDR, SVT-AV1-Essential) kodlayıcı olarak seçilir. | StaxRip | yok; yalnız ana hat libsvtav1 kullanılıyor (src/VidShrink.Core/PlanParser.cs:13) | Aynı boyutta daha iyi görünen AV1 çıktısı. | orta | 3 | sahip: yeni ikili dosya dağıtımı | [13][48] |
| SIK-40 | Kitaplık otomasyonu: bütün bir kitaplık kurallarla taranır, işler zamanlanır ve birden çok makineye dağıtılır. | Tdarr, Unmanic, FileFlows | yarım: tek klasör izleme var (src/VidShrink.App/QueueWatch.cs:13) | Büyük film ve dizi arşivi elle dokunmadan küçülür. | büyük | 3 | sahip: ürün yönü, sunucu kipi yeni bir yüzey | [24][25][26] |
| SIK-41 | Düşük disk alanında duraklatma: kuyruk koşarken disk dolmaya yaklaşınca işler kendiliğinden durur. | HandBrake | yarım: disk alanı yalnız ana pencerede ve komut satırında, kodlama başlamadan önce denetleniyor (src/VidShrink.App/MainWindow.axaml.cs:4514, src/VidShrink.Cli/CliApp.cs:519); kuyruk penceresi hiç denetlemiyor (src/VidShrink.App/ShrinkJobWindow.axaml.cs:614) | Uzun kuyruk disk dolduğu için yarım dosyalarla bitmez. | küçük | 3 | gerekmez | [1] |
| SIK-42 | Pilde duraklatma: dizüstü pile geçince ya da pil azalınca kodlama kendiliğinden durur. | HandBrake, FFmpeg Batch AV Converter | yok; en yakın kod kuyruğu elle duraklatma (src/VidShrink.Core/JobPump.cs:50) | Dizüstü bilgisayar kodlama yüzünden pilsiz kalmaz. | küçük | 3 | gerekmez | [1][18] |
| SIK-43 | Kare arası üretim: kare hızı değiştirilirken ara kareler hesaplanarak üretilir. | Shutter Encoder | yarım: kare hızı yalnız kare atarak düşürülüyor (src/VidShrink.Core/VideoFilterChain.cs:351) | Kare hızı değişen videoda hareket takılmadan akar. | küçük | 3 | gerekmez | [11][12] |
| SIK-44 | Büyük hedef yongaları: 1 GB ve Telegram Premium 4 GB hazır hedef olarak sunulur. | vidcord | yarım: özel hedef yongası eklenebiliyor, hazır 1 GB ve 4 GB yongası yok (src/VidShrink.App/MainWindow.axaml:432) | Büyük dosya gönderen kullanıcı sınırı ezberlemeden seçer. | küçük | 3 | gerekmez | [23][39] |
| SIK-45 | Konuşmadan altyazı üretme: videodaki konuşma yazıya dökülüp altyazı yapılır. | Shutter Encoder | yok; en yakın kod dış altyazı dosyası ekleme (src/VidShrink.App/MainWindow.Izler.cs:177) | Altyazısı olmayan videoya altyazı eklenir. | büyük | 3 | kapalı: K5 | [10] |
| SIK-46 | LUT uygulama: renk dönüşüm tablosu (LUT) kodlama sırasında görüntüye uygulanır. | doğrulanmadı (istek HandBrake'te 10 oy) | yok; en yakın kod serbest süzgeç kutusu (src/VidShrink.App/MainWindow.axaml:1071) | Log çekilmiş video küçültülürken renk düzeltmesi de yapılır. | orta | 3 | kapalı: K8 | [6] |

## Düzeltmeler

Rakip kanıtları birebir alıntı değil, kaynağın söylediğinin Türkçe özetidir. NEWS ve CHANGES, rakibin deposundaki sürüm günlüğü dosyalarıdır.

| No | Ne Değişti | Kanıt |
|---|---|---|
| `SIK-01` | Kimde Var'a beşinci rakip olarak FastFlix eklendi (dört ad sınırı yüzünden "ve 1 rakip daha"), Kaynak'a [15] eklendi. Bizde Durum'a koşan sürecin askıya alınmadığını gösteren ikinci kanıt eklendi. Öncelik 1 kaldı. | HandBrake NEWS dosyası kodlamanın duraklatılıp sürdürülebildiğini yazıyor; vidcoder.net özellik listesi aynı işlevi sayıyor; FFmpeg Batch README'si duraklatma ve sürdürmeyi sayıyor; FastFlix CHANGES dosyası 84 numaralı istekle duraklatma ve sürdürmenin eklendiğini yazıyor. Kodda süreç `src/VidShrink.Ffmpeg/EncodeRunner.cs:820` satırında başlatılıyor, askıya alan çağrı yok. |
| `SIK-02` | Bizde Durum yeniden yazıldı: asıl kanıt kodlama sürecinin öncelik atanmadan açılması; küçük resim süreci yalnız örnek olarak kaldı. Öncelik 1 kaldı. | `src/VidShrink.Ffmpeg/EncodeRunner.cs:815` ile `:820` arasında süreç kurulup başlatılıyor, öncelik ataması yok. Rakip tarafı üç kaynakta görüldü: handbrake.fr sistem gereksinimleri sayfası Gelişmiş sekmesindeki öncelik düzeyi ayarını anıyor; FFmpeg Batch README'si kodlama önceliği ayarını sayıyor; FastFlix CHANGES dosyası 345 numaralı istekle başlatılan araçların varsayılan önceliğinin değiştirilebildiğini yazıyor. |
| `SIK-03` | Kimde Var'a üçüncü bağımsız rakip olarak LosslessCut eklendi. Öncelik 1 kaldı. | LosslessCut README'si (github.com/mifi/lossless-cut) başka dosyalardan iz birleştirmeyi ve videoya müzik eklemeyi ya da sesi değiştirmeyi özellik olarak sayıyor; bu kaynak `kucult.md` listesinde olmadığı için Kaynak sütununa numara eklenmedi. Shutter Encoder'ın ses değiştirme işlevi hem belgesinde hem sürüm notunda (16.5 ve 18.1) görüldü. HandBrake'teki 871 numaralı istek açık ve 62 oylu. |
| `SIK-04` | Öncelik 2'den 1'e çıktı. Kimde Var "doğrulanmadı" yerine Shutter Encoder ve Subtitle Edit oldu; Kaynak'a [10] eklendi, oy bilgisi Fayda'ya taşındı. Bizde Durum'a biçimli altyazı yazan kodun yeri eklendi. | Rakip tarafı iki ayrı araçta görüldü. Shutter Encoder'ın kendi kaynak kodu (github.com/paulpacifico/shutter-encoder, `src/shutterencoder/functions/settings/Overlay.java:374`) yakılan altyazıya yazı tipini açılır listeden, boyutu metin kutusundan alıyor, rengi ve arka plan kutusunu ya da çerçeveyi (`:327`, `:329`) `force_style` ile veriyor; aynı aracın 12.2 ve 14.9 sürüm notları arka plan ve çerçeve seçimini yazıyor. Subtitle Edit sürüm günlüğü (github.com/SubtitleEdit/subtitleedit) altyazı yakma işlevinde harf aralığını ve biçim önizlemesini yazıyor. İki depo `kucult.md` listesinde olmadığı için numara almadı. HandBrake'teki 146 numaralı istek 40 oyla açık ve açık istekler içinde üçüncü sırada. Araştırma kolu satırı yalnız rakip doğrulanmadığı için 2'de tutmuştu (`kucult.md:194`); o engel kalktı ve ölçütün oy kolu 1 diyor. Bizde: `src/VidShrink.Core/Editing/AssWriter.cs:120` yalnız düzenleyicinin metin katmanı için biçimli altyazı yazıyor. `src/VidShrink.Core/VideoFilterChain.cs:205` ile `:221` arasındaki yakma süzgeci biçim seçeneği taşımıyor; `force_style` sözcüğü `src` altında hiç geçmiyor. |
| `SIK-06` | Öncelik 2'den 1'e çıktı. Kimde Var'a FastFlix, Kaynak'a [1][10][15] eklendi. | Üç rakip resmi sürüm günlüğünde belgeli: HandBrake NEWS dosyası kodlama sürerken uyku kipini engellediğini yazıyor; Shutter Encoder 19.6 sürüm notu Ayarlar penceresine uykuyu engelleme kutusu eklendiğini yazıyor; FastFlix CHANGES dosyası dönüştürme sürerken Windows'un uyumamasını eklediğini yazıyor. Notlardaki "Shutter Encoder için yalnız kullanıcı bildirimi var" çekincesi bu sürüm notuyla eskidi. Gece bırakılan kuyruğun yarıda kalması ana işe dokunuyor. |
| `SIK-14` | Bizde Durum yeniden yazıldı: biten çıktının tam ölçümü komut satırında var, arayüzde yok. | `src/VidShrink.Cli/CliApp.cs:533` `--vmaf` seçeneğiyle `QualityMeter.MeasureAsync` çağırıyor; bu çağrı `src` altında başka yerde yok. `src/VidShrink.App/MainWindow.axaml.cs:3639` yalnız kodlama öncesi örnek ölçümü yapıyor. |
| `SIK-16` | Kanıt satırı 12'den 13'e düzeltildi. | `src/VidShrink.Core/PlanParser.cs:13` (`AllowedCodecs` listesi). |
| `SIK-17` | Öncelik 2'den 3'e indi: profil 8.4 yalnız tek rakipte belgeli, oy kanıtı yok. | `kucult.md` özellik matrisinde (satır 149) Dolby Vision 8.4 yalnız HandBrake sütununda yazılı; ölçüt tek rakipte olanı 3 sayıyor. |
| `SIK-20` | Bizde Durum genişletildi: `-threads` yalnız ses ölçümünde değil bütün yardımcı süreçlerde var, kodlama komutunda yok. | `src/VidShrink.Core/AudioLevel.cs:71`, `src/VidShrink.Ffmpeg/SceneDetector.cs:97`. Ek argüman süzgeci de bu seçeneği geçirmiyor (`src/VidShrink.Core/PlanParser.cs:162`). |
| `SIK-21` | Kimde Var'a FastFlix ve FFmpeg Batch AV Converter, Kaynak'a [15][17] eklendi. Öncelik 3 kaldı: üç rakipte var ama kitlesi dar (betik yazan kullanıcı) ve ana işin kenarında. | HandBrake NEWS dosyası kuyruk bitince özel eylem çalıştıran tercihi (4851 numaralı istek) yazıyor; FastFlix CHANGES dosyası dönüştürme sonrası komutu anıyor; FFmpeg Batch README'si kodlama sonrası program çalıştırma seçeneğini sayıyor. |
| `SIK-24` | En yakın kod düzeltildi: iptal değil, hata veren işte çıkan düğme. | `src/VidShrink.App/ShrinkJobWindow.axaml.cs:695` hata sonrası yalnız uygulamada aç düğmesini gösteriyor, yeniden sıraya alan yol yok. |
| `SIK-27` | Kanıt satırı 232'den 397'ye düzeltildi. | `src/VidShrink.Core/WatchFolder.cs:397` (`SearchOption.TopDirectoryOnly`). |
| `SIK-28` | Kanıt satırı 3378'den 3382'ye düzeltildi. | `src/VidShrink.App/MainWindow.axaml.cs:3382` bırakılan öğe klasörse geri çeviriyor; `:3384` aynı denetimi yol üzerinden yineliyor. |
| `SIK-39` | Kanıt satırı 12'den 13'e düzeltildi. | `src/VidShrink.Core/PlanParser.cs:13`. |
| `SIK-41` | Bizde Durum genişletildi: denetim yalnız iki yerde var, kuyruk penceresinde hiç yok. | `DiskSpaceGuard` çağrısı `src` altında yalnız `src/VidShrink.App/MainWindow.axaml.cs:4514` ve `src/VidShrink.Cli/CliApp.cs:519` satırlarında; `src/VidShrink.App/ShrinkJobWindow.axaml.cs:614` ile başlayan iş koşturma yolunda yok. |
| `SIK-42` | Kimde Var'a FFmpeg Batch AV Converter, Kaynak'a [18] eklendi. Öncelik 3 kaldı (dar kitle: pilde kodlama yapan dizüstü kullanıcısı). | FFmpeg Batch 2.8.4 sürüm notu pile geçince kendiliğinden duraklatıp prize takılınca sürdüren ayarı yazıyor; HandBrake NEWS dosyası pile geçince kodlamayı duraklatan tercihi yazıyor. |

## Çıkarılanlar

| No | Eksik | Neden | Kanıt (dosya:satır) |
|---|---|---|---|

Bu turda çıkarılan satır yok. 46 satırın hiçbiri kodda tam olarak bulunmadı; araştırma kolunun özellikle sorulan beş "yok" hükmü (SIK-01, SIK-02, SIK-03, SIK-05, SIK-06) ayrı ayrı arandı ve tuttu.

- Duraklatma: `_pump.Paused` yalnız sıradaki işi bekletiyor (`src/VidShrink.App/ShrinkJobWindow.axaml.cs:389`, `src/VidShrink.Core/JobPump.cs:50`); arayüz metni de bunu söylüyor (`src/VidShrink.App/Locales/tr/main.json:246`).
- Süreç önceliği: `PriorityClass` yedi yerde atanıyor, hepsi kodlama dışı: beş yardımcı ffmpeg süreci (ses ölçümü, sahne tespiti, ses tepeleri, küçük resim, sessizlik kesme) ve iki güncelleme süreci (`src/VidShrink.App/MainWindow.KapanistaGuncelleme.cs:239`, `src/VidShrink.App/Program.cs:220`). Kodlama süreci atama almadan açılıyor (`src/VidShrink.Ffmpeg/EncodeRunner.cs:815`); alt süreçleri toplayan iş nesnesindeki öncelik alanı da doldurulmuyor (`src/VidShrink.Ffmpeg/ChildJob.cs:33`, `:71`).
- Dış ses: ses bölümünde dosya ekleyen denetim yok; ekleme yalnız altyazı için var (`src/VidShrink.App/MainWindow.Izler.cs:173`).
- Kuyruğu saklama: bekleyen işler bellekteki listede (`src/VidShrink.Core/JobPump.cs:60`); diske yazan ya da açılışta okuyan kod yok.
- Uyku engelleme: `SetThreadExecutionState`, `caffeinate` ve `systemd-inhibit` adları `src` altında hiç geçmiyor; uykuyla ilgili tek kod bitince uyutma eylemi (`src/VidShrink.App/QueueEndActions.cs:9`).

## Yinelenenler

Komşu tablo `donustur-eksikler.md` (Dönüştür sekmesi). Satır silinmedi; aşağıdaki çiftler aynı eksiği iki sekmeden anlatıyor.

- SIK-03 = DON-07: dış ses dosyası ekleme. İki tabloda da öncelik 1; motor ortak, arayüz iki sekmede ayrı.
- SIK-04 = DON-09: yakılan altyazıda biçim. Öncelik farklı (burada doğrulama sonrası 1, komşuda 2); iş tahmini de farklı (burada orta, komşuda küçük).
- SIK-16 = DON-33: VVC kodlama. Öncelik farklı (burada 2, komşuda 3).
- SIK-43 = DON-42: kare hızı değişirken ara kare üretimi. İki tabloda da öncelik 3; iş tahmini farklı (burada küçük, komşuda orta).
- SIK-45 = DON-52: konuşmadan altyazı üretme. İkisi de kapalı: K5.
- SIK-46 = DON-50: LUT uygulama. İkisi de kapalı: K8.

## Doğrulanamayanlar

- SIK-10: Windows bildirimi, balonun sistem sesini kapatan `NIIF_NOSOUND` bayrağını taşımıyor (`src/VidShrink.App/IsBittiBildirimi.cs:164`); balon bugün zaten sistem sesiyle çıkıyor olabilir. Program çalıştırmak yasak olduğu için ölçülmedi. Çıkıyorsa durum "yarım: Windows'ta sistem sesi var, seçilebilir ses ve öteki sistemler yok" olur. Satıra dokunulmadı.
- SIK-04: öncelik 1 ölçütün oy koluna dayanıyor (40 oy) ve ölçütte oy için eşik yazılı değil. HandBrake'in açık isteklerinde sıra 62, 47, 40, sonra 19 ve 17 oy; ilk üçü belirgin ayrıştığı için 19 oylu SIK-07 ile 17 oylu SIK-11 2'de bırakıldı. Rakip kolunda yalnız iki ad var ve Subtitle Edit küçültme aracı değil, altyazı düzenleyicisi; sahip rakip kolunu esas alırsa satır 2'ye döner. Subtitle Edit'te yazı tipi ve boyut seçimi sürüm günlüğünde tek tek yazmıyor; görülen, yakma işlevindeki harf aralığı ve biçim önizlemesi.
- SIK-23: `defterde` dayanağı gevşek. Defterdeki satır (`.claude/acik.md:25`, `:50`) Windows bildirim çağrılarının hiç koşmadığını söylüyor, macOS ve Linux'a bildirim eklemeyi değil. İşaret kaldırılmadı.
- SIK-01: Shutter Encoder kanıtı yalnız 17.6 sürüm notundaki duraklat düğmesi hata düzeltmesi; belgesinde elle duraklatma anlatılmıyor. Öteki dört rakip açık olduğundan öncelik etkilenmiyor.
- SIK-03: FFmpeg Batch AV Converter kanıtı dolaylı (çoklama sekmesi ve 3.2.7'de ses kabı desteği); videoya ayrı ses eklediğini açıkça söyleyen cümle görülmedi.
- SIK-07: Tdarr ve FileFlows iddiaları bu turda açılmadı; ikisi de sunucu türü araç. HandBrake tarafı NEWS dosyasında (kuyruk penceresindeki sonra başlat seçeneği) görüldü; 586 numaralı istek 19 oyla açık.
- SIK-16: dört rakipte belgeli ama oynatma desteği dar; komşu tablo aynı işe 3 vermiş. Hangi öncelik geçerli olacak sahibin kararı.
- SIK-17: FastFlix CHANGES dosyası ayrı kodlayıcılarda (NVEncC, QSVEncC, VCEEncC) Dolby Vision profilini kaynaktan kopyaladığını yazıyor; bunun profil 8.4'ü kapsadığı açıkça yazmıyor. Kapsıyorsa iki rakip olur ve öncelik 2'ye döner.
- SIK-21 ve SIK-42: rakip sayısı arttı (üç ve iki) ama dar kitle gerekçesiyle 3'te bırakıldılar. Ölçütün "iki rakipte var" kolu 2'yi de savunur; karar sahibin.
- SIK-44: Telegram Premium'un 4 GB sınırı ve WhatsApp'ın kodda yazılı 16 MB ve 180 MB sınırları bu turda resmi sayfadan açılmadı.
- Genel: öncelik 2 ve 3 satırların rakip iddiaları kural gereği yeniden açılmadı; yalnız öncelik 1 kaynakları açılırken aynı sayfalarda rastlananlar görüldü (SIK-05, SIK-07, SIK-08, SIK-09, SIK-11, SIK-12, SIK-19, SIK-21, SIK-42). Bu satırlarda ölçülen taraf "bizde yok" hükmüdür.

## Ek Notlar

- Kuyruk penceresi işe başlamadan önce disk alanını hiç denetlemiyor (`src/VidShrink.App/ShrinkJobWindow.axaml.cs:614`); ana pencere denetliyor (`src/VidShrink.App/MainWindow.axaml.cs:4514`). SIK-41 yalnız koşarken duraklatmayı anlatıyor, bu boşluk tabloda yok.
- Ana sekmede tek dosya kodlanırken duraklatma düğmesi hiç yok, yalnız başlat ve iptal var (`BtnStart`, `BtnCancel`). SIK-01 iki yüzeyi kapsamalı: kuyruk penceresi ve ana sekme.
- Serbest süzgeç kutusu ham ffmpeg süzgeç zinciri almıyor, kendi dilbilgisini çözüyor (`src/VidShrink.Core/VideoFilterChain.cs:530`). Tablodaki "serbest süzgeç kutusu var" ifadeleri (SIK-12, SIK-46) bu sınırla okunmalı.
- Ek argüman süzgeci yalnız beş ffmpeg seçeneğini geçiriyor (`src/VidShrink.Core/PlanParser.cs:162`); iş parçacığı sınırı için bugün elle yol da yok.
- WhatsApp yongalarının sınırları (16 MB ve 180 MB) hazır ayar dosyasında kaynak durumu `Official` değil `Code` olarak işaretli (`src/VidShrink.Core/Presets/platformlar.json:7`, `:11`). 16 MB satırında bir WhatsApp yardım sayfası adresi var ama denetim tarihi yok; 180 MB satırında adres de yok. Aynı dosyadaki Discord, Telegram ve e-posta sınırları adres ve denetim tarihi taşıyor (`:13` ile `:18`). Araştırma kolunun "resmi kaynağa bağlı değil" notu kodda tuttu; sınırın kendisi ayrıca doğrulanmalı.
- Önceki HandBrake karşılaştırması kuyruğu "eşit" sayıyor ve kanıt olarak kuyruğu duraklatan `SetPaused` yolunu gösteriyor (`docs/handbrake/durum-2026-09-23.md:82`). O duraklatma koşan kodlamayı durdurmuyor; SIK-01 kapanana dek o hüküm eksik okunmalı.

## Sekme Özeti

- Eksikler çekirdek işte (hedef boyuta küçültme, kodek seçimi, HDR10 taşıma, bölmeli karşılaştırma) değil; uzun işin konforunda ve kuyruk yönetiminde toplanıyor.
- 46 satırın 46'sı kaldı. Öncelik 1 beş satır (SIK-01, SIK-02, SIK-03, SIK-04, SIK-06); beşi de en az iki bağımsız kaynakta görüldü.
- En ağır üç eksik: koşan kodlamanın duraklatılamaması (SIK-01, beş rakipte var), dış ses dosyasının eklenememesi (SIK-03, HandBrake'te 62 oyla en çok istenen), kodlama sürerken uykunun engellenmemesi (SIK-06, gece bırakılan kuyruk yarıda kalır).
- En ucuz üç kazanç: kodlama süreç önceliği (SIK-02, tek atama ve bir ayar), bitince uygulamadan çık (SIK-09, var olan eylem listesine bir değer), kaynak dosyanın tarihini koruma (SIK-11, taşımadan sonra iki dosya çağrısı).
- On üç paketin yedisi karar beklemiyor; altısı sahibin kararına bağlı. Bunların içinde yalnız SIK-P2 hem küçük hem öncelik 1, ilk sorulacak karar odur.
- Dönüştür sekmesiyle altı satır ortak; dış ses ve altyazı biçimi motoru bir kez yazılıp iki sekmeye hizmet eder.
- Dolby Vision ve VVC tarafındaki satırlar pazarda var ama hepsi derleme ya da kapsam kararına bağlı; bu tur onları sıranın sonuna koydu.

## Paket Önerisi

Sıra önerilen yapım sırasıdır: önce küçük ve öncelik 1 olan, en sonda karar bekleyen büyük işler.

### SIK-P1 — Kodlama Süreç Önceliği Ayarı
- Satırlar: SIK-02, SIK-20
- İş: küçük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.Ffmpeg/FfmpegRunner.cs, src/VidShrink.Core/FfmpegArguments.cs, src/VidShrink.App/MainWindow.axaml (Ayarlar, `CmbParallelJobs` yanı), src/VidShrink.App/AppSettings.cs, src/VidShrink.Cli/CliRequest.cs, src/VidShrink.App/Locales/
- Plan: Ayarlar'a kodlama önceliği seçimi eklenir (Normal, Normalin Altı, Düşük; varsayılan bugünkü gibi Normal) ve kodlama süreci başlar başlamaz `Process.PriorityClass` ile atanır; aynı atama yardımcı süreçlerde zaten yapılıyor (`src/VidShrink.Core/Editing/ThumbnailQueue.cs:276`). Yanına çekirdek sınırı seçimi konur ve kodlama komutuna `-threads` olarak yazılır. Bu seçenek her kodlayıcıda aynı etkiyi vermeyebilir; işçi önce her kodlayıcıda ölçer, sınırlamayan kodlayıcıda seçimi gizler. Doğrulama: ayar varsayılandayken komutun değişmediğini pimleyen argüman testi ve başlatılan sürecin önceliğini okuyan test.

### SIK-P2 — Kodlama Sürerken Uykuyu Engelleme
- Satırlar: SIK-06
- İş: küçük
- Karar: sahip: Kodlama sürerken bilgisayarın uykuya geçmesi varsayılan olarak mı engellensin, yoksa kapalı gelen bir ayar mı olsun?
- Dokunacağı yer: src/VidShrink.App/ (yeni uyku engelleme yardımcısı), src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/QueueEndActions.cs, src/VidShrink.App/AppSettings.cs, src/VidShrink.App/Locales/
- Plan: İlk iş başlarken sistemden uyumama isteği alınır, son iş bitince ya da iptalde geri verilir: Windows'ta `SetThreadExecutionState` çağrısı, macOS'ta `caffeinate` süreci, Linux'ta `systemd-inhibit`. Ekranın kararmasına dokunulmaz, yalnız uyku engellenir; "bitince uyut" eylemi seçiliyse istek önce bırakılır, sonra makine uyutulur. Doğrulama: isteğin alınıp bırakıldığını sayan sahte sağlayıcılı test ve Windows'ta `powercfg /requests` çıktısına elle bakış.

### SIK-P3 — Koşan Kodlamayı Duraklatma
- Satırlar: SIK-01, SIK-41
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.Ffmpeg/ChildJob.cs, src/VidShrink.Core/JobPump.cs, src/VidShrink.App/ShrinkJobWindow.axaml, src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.Ffmpeg/DiskSpaceGuard.cs, src/VidShrink.App/Locales/
- Plan: Koşan ffmpeg süreci askıya alınıp sürdürülür: Windows'ta `NtSuspendProcess` ve `NtResumeProcess`, macOS ve Linux'ta `SIGSTOP` ve `SIGCONT` sinyalleri. Kuyruk penceresindeki duraklat düğmesi koşan işi de durdurur, ana sekmeye de aynı düğme gelir; askıdayken kalan süre ve deneme süresi sayaçları (`src/VidShrink.Ffmpeg/EncodeRunner.cs:436`) durur, iptal askıdaki süreci de sonlandırır. Aynı duraklatma kuyruk koşarken boş disk alanı eşiğin altına inince kendiliğinden tetiklenir; kuyruk penceresi işe başlamadan önce de alanı denetler. Doğrulama: askıdayken kısmi dosyanın büyümediğini ve sürdürünce işin bittiğini ölçen test, askıda iptal testi, sahte disk okuyucusuyla otomatik duraklatma testi.

### SIK-P4 — Dış Ses İzi Ekleme
- Satırlar: SIK-03
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/MainWindow.Izler.cs, src/VidShrink.App/MainWindow.axaml (ses bölümü), src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/FfmpegArguments.cs, src/VidShrink.Core/PlanCalculator.cs, src/VidShrink.Cli/CliRequest.cs, src/VidShrink.App/Locales/
- Plan: Ses bölümüne, dış altyazı eklemenin eşi olan bir ses dosyası ekle düğmesi gelir; dosya ffmpeg'e ikinci giriş olarak verilir ve `-map` ile ya var olan sesin yanına eklenir ya da onun yerine konur. Hedef boyut hesabı eklenen sesin bit hızını bütçeye katar; ses videodan uzunsa video süresinde kesilir. Doğrulama: çıktıdaki iz sayısını ve sırasını ffprobe ile okuyan test ve hedef boyutun bantta kaldığını ölçen test. Motor Dönüştür sekmesindeki DON-07 ile ortaktır; iki iş aynı işçiye verilirse motor bir kez yazılır.

### SIK-P5 — Yakılan Altyazıda Biçim Seçimi
- Satırlar: SIK-04
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.Izler.cs, src/VidShrink.App/MainWindow.axaml (altyazı bölümü, `CmbBurnSubtitle` yanı), src/VidShrink.Cli/CliRequest.cs, src/VidShrink.App/Locales/
- Plan: Altyazı yakılırken yazı tipi, boyut ve arka plan kutusu seçilir; seçim ffmpeg'in `subtitles` süzgecine `force_style` ile verilir (altyazıyı çizen libass kitaplığının, dosyadaki biçimi geçersiz kılan seçeneği). Seçim boşken süzgeç metni bugünküyle aynı kalır; resim tabanlı altyazıda (PGS, VobSub) biçim uygulanamadığı için seçim gizlenir. Doğrulama: süzgeç metnini pimleyen test ve iki ayrı boyutla yakılan karelerin farklı çıktığını ölçen görüntü testi. Dönüştür sekmesindeki DON-09 aynı motoru kullanır.

### SIK-P6 — Çıktı Tarihi, Adı Ve Doğrulaması
- Satırlar: SIK-11, SIK-13, SIK-18, SIK-24
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.Core/AdlandirmaDeseni.cs, src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.App/MainWindow.axaml (Ayarlar), src/VidShrink.App/AppSettings.cs, src/VidShrink.App/Locales/
- Plan: Kısmi dosya asıl ada taşınmadan önce (`src/VidShrink.Ffmpeg/EncodeRunner.cs:463`) çıktı ffprobe ile okunur; süre kaynaktan belirgin kısaysa ya da dosya çözülemiyorsa iş hatalı sayılır ve hedef dosya ezilmez. Taşımadan sonra, varsayılan kapalı gelen bir ayar açıksa kaynağın oluşturma ve değiştirme tarihi çıktıya yazılır. Adlandırma desenine saat, bit hızı, kare hızı, kaynak klasörü ve sayaç yer tutucuları eklenir (`src/VidShrink.Core/AdlandirmaDeseni.cs:21`); hata veren iş satırına tek tıkla yeniden sıraya alan düğme konur. Doğrulama: kasıtlı kısaltılmış çıktının hata verdiğini, tarihin taşındığını ve her yeni yer tutucunun açıldığını ölçen testler.

### SIK-P7 — Kuyrukta Zamanlama, Düzenleme Ve Bitiş
- Satırlar: SIK-07, SIK-08, SIK-09, SIK-10
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/ShrinkJobWindow.axaml, src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.Core/JobPump.cs, src/VidShrink.App/QueueEndActions.cs, src/VidShrink.App/IsBittiBildirimi.cs, src/VidShrink.App/AppSettings.cs, src/VidShrink.App/Locales/
- Plan: Kuyruk penceresine "şu saatte başlat" ve "şu kadar sonra başlat" seçimi eklenir; pompa o ana dek duraklatılmış bekler ve var olan geri sayım satırı kalan süreyi gösterir. Bekleyen iş satırına düzenle düğmesi gelir: iş kuyruktan çıkmadan hedefi ve ayarı değiştirilir. Bitince eylemlerine uygulamadan çık eklenir ve Ayarlar'a varsayılan kapalı bir bitiş sesi kutusu konur; işçi önce bugünkü Windows balonunun ses çalıp çalmadığını ölçer. Doğrulama: sahte saatle zamanlama testi, düzenlenen işin yeni ayarla koştuğunu ölçen pompa testi, yeni eylemin seçilip saklandığını ölçen test.

### SIK-P8 — Önizlemede Elle Kırpma
- Satırlar: SIK-12
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/MainWindow.axaml (önizleme alanı ve `ChkFltAutoCrop` çevresi), src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/Locales/
- Plan: Önizleme karesinin üstüne tutamaçlı bir dikdörtgen çizilir; kullanıcı fareyle sürükler, sonuç kaynak pikseline çevrilip var olan `crop` süzgeç yoluna yazılır (`src/VidShrink.Core/VideoFilterChain.cs:330`). Elle kırpma seçiliyken otomatik siyah bant kırpma kapanır; genişlik ve yükseklik kodlayıcının istediği çift sayıya yuvarlanır. Doğrulama: ekran koordinatından kaynak pikseline çeviriyi ölçen test ve çıktı çözünürlüğünü ffprobe ile okuyan test.

### SIK-P9 — Kuyruğun Saklanması Ve Geri Yüklenmesi
- Satırlar: SIK-05
- İş: orta
- Karar: sahip: Açılışta yarım kalan kuyruk kendiliğinden mi geri gelsin, yoksa kullanıcıya mı sorulsun?
- Dokunacağı yer: src/VidShrink.Core/JobPump.cs, src/VidShrink.Core/ (yeni kuyruk deposu), src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.App/Locales/
- Plan: Bekleyen ve koşan işler her değişiklikte kullanıcı veri klasöründeki bir JSON dosyasına yazılır; önce geçici dosyaya yazılıp sonra yer değiştirilir ki yarım yazılmış dosya kuyruğu bozmasın. Açılışta dosya okunur, kaynağı artık olmayan işler elenir, koşarken kesilen iş baştan sıraya girer. Doğrulama: yaz, süreci kes, yeniden oku testi ve bozuk dosyada uygulamanın boş kuyrukla açıldığını ölçen test.

### SIK-P10 — Biten Çıktının Kalite Ölçümü
- Satırlar: SIK-14
- İş: orta
- Karar: sahip: Tam kalite ölçümü her işi uzatır; varsayılan kapalı bir seçenek olarak arayüze girsin mi?
- Dokunacağı yer: src/VidShrink.Ffmpeg/QualityMeter.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/ShrinkJobWindow.axaml.cs, src/VidShrink.App/Locales/
- Plan: Komut satırının `--vmaf` ile yaptığı ölçüm (`src/VidShrink.Cli/CliApp.cs:533`) arayüze taşınır: iş bitince çıktının tamamı kaynakla karşılaştırılır, VMAF puanı (görüntünün kaynağa benzerliğini 0 ile 100 arasında puanlayan ölçüt) sonuç satırında gösterilir ve iş kaydına yazılır. Ölçüm ilerleme çubuğunda ayrı bir aşama olarak görünür ve iptal edilebilir. Doğrulama: arayüzün gösterdiği puanın aynı dosyada komut satırının verdiği puanla aynı olduğunu ölçen test.

### SIK-P11 — Ölçümle Hedef Kalite Araması
- Satırlar: SIK-15
- İş: büyük
- Karar: sahip: Arama her turda ek kodlama ve ölçüm süresi getirir; hedef kalite kipine isteğe bağlı bir seçenek olarak eklensin mi?
- Dokunacağı yer: src/VidShrink.Core/PlanCalculator.cs, src/VidShrink.Ffmpeg/QualityMeter.cs, src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.Cli/CliApp.cs, tools/VidShrink.Bench/
- Plan: Hedef kalite bugün örnek ölçümünden çıkan tahminle çözülüyor (`src/VidShrink.Core/PlanCalculator.cs:1170`); bu pakette kısa örnekler aday ayarla kodlanıp ölçülür ve hedef puan tutana dek ayar aralığı ikiye bölünerek aranır. Tur sayısı sınırlanır, her tur ilerlemede görünür, arama kapalıyken bugünkü yol değişmez. Doğrulama: ölçüm aracıyla sabit bir dosya kümesinde tahmin yolu ile arama yolunun hedef puandan sapması ve harcadığı süre yan yana ölçülür.

### SIK-P12 — VVC Kodlama Desteği
- Satırlar: SIK-16
- İş: orta
- Karar: sahip: libvvenc içeren bir ffmpeg derlemesi dağıtılsın mı ve oynatma desteği bu kadar darken VVC kodek listesine girsin mi?
- Dokunacağı yer: src/VidShrink.Core/PlanParser.cs, src/VidShrink.Core/FfmpegArguments.cs, src/VidShrink.Core/PlanCalculator.cs, src/VidShrink.App/MainWindow.axaml (kodek seçimi), src/VidShrink.App/Locales/
- Plan: Önce dağıtılan ffmpeg'de libvvenc (VVC kodlayan açık kaynak kitaplık) var mı diye yoklanır; yoksa iş derleme kararında bekler. Varsa kodek izin listesine eklenir (`src/VidShrink.Core/PlanParser.cs:13`), hız ön ayarları ve boyut verimi ölçülerek yazılır, kodek seçiminde çoğu aygıtta oynamadığı uyarısıyla gösterilir. Doğrulama: argüman testi ve ölçüm aracıyla hedef boyut bandının tuttuğunun ölçülmesi. Dönüştür sekmesindeki DON-33 ile aynı iştir.

### SIK-P13 — Donanım Kodlayıcıda Dolby Vision
- Satırlar: SIK-19
- İş: büyük
- Karar: sahip: Dolby Vision kapsamı donanım kodlayıcılarına genişlesin mi, gerekirse ffmpeg dışında ayrı bir araçla?
- Dokunacağı yer: src/VidShrink.Core/HdrResolver.cs, src/VidShrink.Core/FfmpegArguments.cs, tools/VidShrink.Bench/, docs/olcumler/
- Plan: Önce ölçülür: ekran kartı kodlayıcılarıyla yapılan kodlamada ffmpeg Dolby Vision üst verisini çıktıya yazıyor mu; sonuç `docs/olcumler/` altına girer. Yazıyorsa `src/VidShrink.Core/HdrResolver.cs:46` satırındaki kodlayıcı koşulu genişletilir; yazmıyorsa iş ayrı araç zinciri kararına döner ve paket orada durur. Doğrulama: çıktının Dolby Vision kaydını ffprobe ile okuyan test.

## Pakete Girmeyenler

Hepsi öncelik 3 ya da kapalı. Hiçbiri bir paketin yanında neredeyse bedavaya gelmiyor.

- SIK-17: önceliği 3'e indi ve büyük iş; Dolby Vision kapsam kararı açılırsa SIK-P13 ile birlikte ele alınır (iPhone çekimleri yaygın olduğu için o gün ilk ölçülecek olan budur).
- SIK-21: sahibin güvenlik kararını bekliyor ve kitlesi dar; SIK-P7'nin karar beklemeyen satırlarını durdurmasın diye dışarıda.
- SIK-22: posta sunucusu ayarı ve parola saklama ister; yanına ekleneceği paket yok.
- SIK-23: rakip tarafı doğrulanmadı ve üç işletim sisteminde ayrı bildirim yolu orta iş.
- SIK-25: tek rakipte; SIK-P7 ile aynı pencereye dokunur ama o paket dört satırla dolu.
- SIK-26: geri alınamaz işlem, sahibin kararını bekliyor.
- SIK-27: tek rakipte; izleme klasörü koduna dokunan paket yok.
- SIK-28: tek rakipte ve orta iş; klasör yapısını çıktıda korumak ayrı tasarım ister.
- SIK-29: parçalı kodlama mimarisi gerekir, büyük iş.
- SIK-30: büyük iş ve SIK-29 ile aynı mimari karara bağlı.
- SIK-31: tek rakipte, orta iş; karşılaştırma paneline dokunan paket yok.
- SIK-32: tek rakipte, orta iş; SIK-31 ile birlikte ayrı bir karşılaştırma turunda ele alınır.
- SIK-33: büyük iş, Dolby Vision kapsam kararına bağlı.
- SIK-34: kapalı (Dolby Vision profil 5 kapsam dışı hükmü).
- SIK-35: ffmpeg yolu taşımıyor, ayrı araç zinciri kararı gerekir.
- SIK-36: tek rakipte; gren sentezi boyut hesabını değiştirir ve ölçüm ister.
- SIK-37: ffmpeg derlemesine ek kitaplık kararı bekliyor.
- SIK-38: ffmpeg derlemesine yeni ölçüt kitaplığı kararı bekliyor; SIK-P10 bitmeden yeri yok.
- SIK-39: yeni ikili dosya dağıtımı kararı bekliyor.
- SIK-40: ürün yönü kararı; sunucu kipi yeni bir yüzey.
- SIK-42: üç işletim sisteminde ayrı güç durumu okuması ister; SIK-P3 bittikten sonra küçük bir ek olur.
- SIK-43: tek rakipte; ara kare üretimi kodlamayı yavaşlatır, yanına ekleneceği paket yok.
- SIK-44: tek rakipte; yonga listesi başka hiçbir paketin dosyasına dokunmuyor, küçük bir iş olarak ayrıca alınabilir.
- SIK-45: kapalı (K5).
- SIK-46: kapalı (K8).
