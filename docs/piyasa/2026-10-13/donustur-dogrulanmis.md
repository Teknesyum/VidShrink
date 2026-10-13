# Dönüştür Ve Gelişmiş — Doğrulanmış Eksik Tablosu

13 Ekim 2026. Kod okuması `origin/main` 9de48d8b. Kaynak numaraları `donustur.md` içindeki Kaynaklar bölümüne bakar. Girdi `donustur-eksikler.md`, 52 satır. Hiçbir satır çıkarılmadı; 21 satırda düzeltme var, hepsi Düzeltmeler bölümünde. Önceliği değişen tek satır DON-09.

Dört terim. **Kap**, dosyanın dış biçimidir (MP4, MKV gibi). **Kopya kipi** (remux), görüntüyü ve sesi yeniden kodlamadan yalnız kabı değiştirmektir. **Motor**, ffmpeg komutunu üreten çekirdek koddur (`src/VidShrink.Core`). **Küçült yolu**, hedef boyuta sıkıştıran sekmenin kullandığı zengin motordur; Dönüştür ayrı ve daha ince bir yoldan gider. "Motor Küçült yolunda var" demek, işin kodu yazılmış ama Dönüştür'e bağlanmamış demektir.

| No | Eksik | Kimde Var | Bizde Durum | Fayda | İş | Öncelik | Karar | Kaynak |
|---|---|---|---|---|---|---|---|---|
| DON-01 | Ses izi seçimi: Dönüştür çıktısına hangi ses izlerinin gireceğini seçmek ve birden çok sesi taşımak, kopya (remux) kipinde de. | HandBrake, Shutter Encoder, MKVToolNix, LosslessCut ve 3 rakip daha | yarım: motor Küçült yolunda var (src/VidShrink.Core/StreamMapping.cs:255), ama o da yalnız "tüm izleri koru" ve dil tercihi biliyor, tek tek iz seçme alanı yok (src/VidShrink.Core/StreamMapping.cs:140); Dönüştür hiç `-map` yazmıyor, tek sesi ffmpeg seçiyor (src/VidShrink.Core/ConversionArguments.cs:70) | İki dilli videonun ikinci sesi dönüşümde sessizce kaybolmaz, hangi sesin kalacağını kullanıcı seçer. | orta | 1 | sahip: varsayılan değişir, bugün tek ses çıkıyor, tüm sesler mi taşınsın | [8][9][14][18][23][35][37] |
| DON-02 | Altyazı izlerini taşıma: kaynaktaki gömülü altyazıları çıktı kabının anladığı biçimde, açılıp kapanabilir iz olarak geçirmek. | HandBrake, Shutter Encoder, XMedia Recode, FFmpeg Batch AV Converter ve 2 rakip daha | yarım: motor Küçült yolunda var (src/VidShrink.Core/StreamMapping.cs:271), Dönüştür'de altyazı seçeneği yok, sonuç ffmpeg'in varsayılan seçimine kalıyor (src/VidShrink.Core/ConversionArguments.cs:70) | Altyazılı film başka kaba geçerken altyazısını yitirmez. | orta | 1 | gerekmez | [1][6][9][12][14][30][35] |
| DON-03 | Altyazıyı görüntüye gömme: seçilen altyazıyı Dönüştür'de kareye yakmak. | HandBrake, Shutter Encoder, FFmpeg Batch AV Converter, Subtitle Edit ve 2 rakip daha | yarım: motor Küçült yolunda var (src/VidShrink.Core/VideoFilterChain.cs:233), arayüzü Küçült'te (src/VidShrink.App/MainWindow.Izler.cs:92); Dönüştür'ün süzgeç zinciri yalnız ölçek ve kare hızı (src/VidShrink.Core/ConversionArguments.cs:229) | Altyazı izini göstermeyen sitelere ve telefonlara altyazılı video çıkar. | orta | 1 | gerekmez | [6][8][9][14][21][24][30] |
| DON-04 | Donanım kodlayıcıları: Dönüştür'ün kodek listesinde ekran kartı kodlayıcılarını (NVENC, QSV, AMF; AV1 dahil) seçmek. | HandBrake, Shutter Encoder, XMedia Recode, FFmpeg Batch AV Converter ve 3 rakip daha | yarım: Dönüştür motoru bu kodlayıcıları zaten sürüyor ve testli (src/VidShrink.Core/ConversionArguments.cs:144, tests/VidShrink.Tests/HardwareEncoderTests.cs:218), eksik yalnız kodek listesi (src/VidShrink.App/MainWindow.axaml:1149) ve makinede var mı yoklaması (src/VidShrink.App/MainWindow.DonusturKap.cs:33) | Uzun videonun dönüşümü işlemci yerine ekran kartında koşar, bekleme kısalır. | küçük | 1 | gerekmez | [1][2][9][10][12][14][17][18][20] |
| DON-05 | Adlı Dönüştür ön ayarı: kendi ayar takımını adla kaydedip listeden yeniden seçmek. | HandBrake, Shutter Encoder, File Converter, Permute ve 1 rakip daha | yarım: kullanıcı ön ayarı yalnız Küçült'te (src/VidShrink.App/MainWindow.OnAyar.cs:228), Dönüştür son seçimi sıra numarasıyla hatırlıyor (src/VidShrink.App/MainWindow.axaml.cs:1412) | Sık yapılan dönüşüm her seferinde baştan kurulmaz. | orta | 1 | gerekmez | [7][9][11][15][23][25] |
| DON-06 | Toplu dönüştürme: birden çok dosyayı aynı ayarla art arda dönüştürmek. | HandBrake, Shutter Encoder, XMedia Recode, FFmpeg Batch AV Converter ve 3 rakip daha | yarım: çoklu bırakma Küçült kuyruğuna gidiyor (src/VidShrink.App/MainWindow.axaml.cs:3308), Dönüştür tek dosya alıyor (src/VidShrink.App/MainWindow.axaml.cs:3233) | Bir klasör dolusu dosya tek seferde başka biçime geçer. | orta | 1 | gerekmez | [1][9][12][14][18][29][32] |
| DON-07 | Harici ses dosyası ekleme: videoya ayrı bir ses dosyasını iz olarak eklemek ya da sesi onunla değiştirmek. | Shutter Encoder, MKVToolNix, LosslessCut, Adapter | yok | Ayrı kaydedilmiş ses ya da dublaj, görüntü yeniden kodlanmadan dosyaya girer. | orta | 1 | gerekmez | [9][11][35][37][45] |
| DON-08 | Görüntü dizisinden video: numaralı resim karelerini (PNG, JPEG) tek videoya çevirmek. | Shutter Encoder, Apple Compressor, FFmpeg Batch AV Converter | yok | Animasyon ve hızlandırılmış çekim kareleri başka araca gerek kalmadan videoya döner. | orta | 1 | gerekmez | [9][14][46] |
| DON-09 | Gömme altyazıda biçem: yakılan altyazının yazı tipini, boyutunu, rengini ve arka plan kutusunu seçmek. | Subtitle Edit, Shutter Encoder | yarım: gömme var, süzgece biçem yazılmıyor (src/VidShrink.Core/VideoFilterChain.cs:233) | Yakılan altyazı küçük ekranda okunur boyda ve kutulu çıkar. | küçük | 1 | gerekmez | [10][21][47] |
| DON-10 | Harici altyazı dosyası ekleme: .srt ya da .ass dosyasını Dönüştür çıktısına iz olarak eklemek. | HandBrake, Shutter Encoder, XMedia Recode, VideoProc Converter ve 2 rakip daha | yarım: motor Küçült yolunda var (src/VidShrink.Core/StreamMapping.cs:798), Küçült arayüzünde ve komut satırında açık (src/VidShrink.App/MainWindow.Izler.cs:91, src/VidShrink.Cli/CliRequest.cs:128), Dönüştür'de yok | İnternetten bulunan altyazı dosyası videonun içine girer, ayrı dosya taşımak gerekmez. | küçük | 2 | gerekmez | [6][8][9][12][14][30][35] |
| DON-11 | Altyazıyı dosyaya çıkarma: gömülü altyazı izini .srt ya da .ass olarak ayrı kaydetmek. | Shutter Encoder, XMedia Recode, LosslessCut, VideoProc Converter ve 1 rakip daha | yok | Altyazı çeviri ya da düzeltme için ayrı dosya olarak ele geçer. | küçük | 2 | gerekmez | [9][12][22][30][37] |
| DON-12 | Ön ayarı dosyayla paylaşma: Dönüştür ön ayarını dışa aktarıp başka makinede içe almak. | HandBrake, Shutter Encoder, Permute, MKVToolNix | yarım: dışa ve içe aktarma yalnız Küçült ön ayarında (src/VidShrink.App/MainWindow.OnAyar.cs:385, src/VidShrink.App/MainWindow.OnAyar.cs:301) | Aynı ayar elden ele dolaşır, kimse baştan kurmaz. | küçük | 2 | gerekmez | [7][8][11][23][34] |
| DON-13 | Toplu işte dosya başına ayar: sıradaki her dosyaya ayrı kap, kodek ya da ön ayar vermek. | Convertio, Shutter Encoder, FFmpeg Batch AV Converter | yok | Karışık bir dosya yığını tek oturumda, her biri kendi biçimine dönüşür. | orta | 2 | gerekmez | [9][14][32] |
| DON-14 | Dönüştür'de görüntü süzgeçleri: Küçült'teki kırpma, döndürme, tarama çözme (deinterlace) ve gürültü azaltma zincirini Dönüştür'e bağlamak. | HandBrake, Shutter Encoder, XMedia Recode, Format Factory | yarım: motor Küçült yolunda var (src/VidShrink.Core/VideoFilterChain.cs:63), Dönüştür yalnız ölçek ve kare hızı yazıyor (src/VidShrink.Core/ConversionArguments.cs:229) | Yan dönmüş ya da siyah bantlı video biçim değiştirirken aynı adımda düzelir. | orta | 2 | gerekmez | [1][8][9][12][19] |
| DON-15 | Kodlayıcı ince ayarı: hız ön ayarı, tune, profil, seviye ve iki geçişi Dönüştür'de seçmek. | HandBrake, Shutter Encoder, FFmpeg Batch AV Converter, VidCoder | yarım: hız ön ayarı sabit varsayılanla yazılıyor (src/VidShrink.Core/ConversionArguments.cs:142), seçim yalnız Küçült'ün Gelişmiş panelinde (src/VidShrink.App/MainWindow.axaml:983) | Eski cihaza uyumlu profil ya da daha yavaş ama küçük kodlama elle seçilir. | orta | 2 | gerekmez | [8][9][14][18] |
| DON-16 | Ses yüksekliği normalizasyonu: Dönüştür'de sesi EBU R128 hedefine çekmek. | Shutter Encoder, foobar2000, Wondershare UniConverter | yarım: motor Küçült yolunda var (src/VidShrink.Core/StreamMapping.cs:144), arayüzü Küçült'te (src/VidShrink.App/MainWindow.Izler.cs:88), Dönüştür planında alanı yok (src/VidShrink.Core/ConversionPlan.cs:31) | Farklı kaynaklardan gelen videolar aynı ses düzeyinde çıkar. | küçük | 2 | gerekmez | [9][27][40] |
| DON-17 | AC3 ve E-AC3 kodlama: Dönüştür'ün ses listesine Dolby Digital (AC3) ve Dolby Digital Plus (E-AC3) eklemek. | HandBrake, Shutter Encoder, XMedia Recode, FFmpeg Batch AV Converter | yarım: Küçült planı ac3 ve eac3 kabul ediyor (src/VidShrink.Core/PlanParser.cs:14), Dönüştür'ün ses listesinde (src/VidShrink.App/MainWindow.axaml:1298) ve kap uyum tablosunda (src/VidShrink.Core/ConversionArguments.cs:300) yok | Televizyon ve ses alıcılarının çaldığı çok kanallı ses biçimi seçilebilir. | küçük | 2 | gerekmez | [1][9][12][14] |
| DON-18 | Ek ses dosyası biçimleri: yalnız ses çıktısına Opus, OGG, AIFF ve AC3 dosyası eklemek. | Shutter Encoder, File Converter, Any Video Converter, fre:ac | yarım: dört ses kabı var, MP3, M4A, WAV ve FLAC (src/VidShrink.Core/ConversionPlan.cs:32) | Ses küçük bir Opus dosyası ya da kurgu için AIFF olarak alınabilir. | küçük | 2 | gerekmez | [9][15][25][39][52] |
| DON-19 | Sesi yeniden kodlamadan çıkarma: videodaki ses izini olduğu gibi ayrı dosyaya almak. | Shutter Encoder, LosslessCut, Permute | yarım: ses kabı seçilince ses her zaman yeniden kodlanıyor (src/VidShrink.App/MainWindow.axaml.cs:4782) | Ses kalite kaybı olmadan ve çok daha kısa sürede ayrılır. | küçük | 2 | gerekmez | [9][22][37] |
| DON-20 | MP4'e AC3, E-AC3 ve Opus kopyası: Dönüştür'ün kopya tablosu bu sesleri MP4'te reddediyor, Küçült kabul ediyor. | HandBrake, VidCoder | yarım: motor Küçült yolunda var (src/VidShrink.Core/StreamMapping.cs:430), Küçült MP4'e ac3, eac3 ve opus kopyalıyor; Dönüştür yalnız aac, alac, mp3 ve flac (src/VidShrink.Core/ConversionArguments.cs:272) | MKV'den MP4'e geçişte çok kanallı ses yeniden kodlanmadan kalır. | küçük | 2 | gerekmez | [1][18] |
| DON-21 | FFV1: kayıpsız ve açık arşiv kodekiyle yazmak. | HandBrake, Shutter Encoder, Wondershare UniConverter | yok | Arşivlik kopya kalite kaybı olmadan ve açık bir biçimde saklanır. | küçük | 2 | gerekmez | [5][9][27] |
| DON-22 | APNG çıktısı: hareketli PNG (APNG) üretmek. | Shutter Encoder, ScreenToGif, ezgif, FreeConvert | yarım: hareketli görsel çıktısı WebP ve AVIF olarak var (src/VidShrink.Core/ConversionPlan.cs:34), APNG yok | GIF'in 256 renk sınırına takılmayan, saydamlıklı kısa animasyon çıkar. | küçük | 2 | gerekmez | [9][33][42][43] |
| DON-23 | Görüntü dizisi çıktısı: videoyu numaralı PNG ya da JPEG karelerine dökmek. | Shutter Encoder, Wondershare UniConverter, Permute, Adapter ve 1 rakip daha | yok | Kareler tek tek rötuş, animasyon ya da inceleme için ele geçer. | küçük | 2 | gerekmez | [9][23][24][27][30] |
| DON-24 | GIF ince ayarı: döngü sayısını, renk sayısını, dither'ı ve kayıplı sıkıştırmayı seçmek. | gifski, ezgif | yarım: sabit iki adımlı palet ve tek dither (src/VidShrink.Core/ConversionArguments.cs:105) | GIF'in boyutu ve görünümü işe göre ayarlanır. | küçük | 2 | gerekmez | [41][44] |
| DON-25 | Dönüştür'de HDR seçimi: HDR'yi koru ya da SDR'ye ton eşle seçimini Dönüştür'ün kendi içinde göstermek. | Shutter Encoder, HandBrake, FastFlix | yarım: seçim Küçült sekmesindeki düğmeden okunuyor (src/VidShrink.App/MainWindow.axaml:803, src/VidShrink.App/MainWindow.axaml.cs:4777) | Başka sekmedeki bir düğme Dönüştür çıktısının rengini habersizce değiştirmez. | küçük | 2 | gerekmez | [8][9][10][20] |
| DON-26 | Renk uzayı ve aralık dönüşümü: BT.601, BT.709, BT.2020 ile sınırlı ve tam aralık arasında elle seçim. | HandBrake, Shutter Encoder, Any Video Converter | yarım: motor Küçült yolunda var (src/VidShrink.Core/VideoFilterChain.cs:397), yalnız BT.709 ve BT.601 için; BT.2020 ve aralık seçimi hiçbir yolda yok, Dönüştür'de etiketleri HDR çözücü kendisi yazıyor (src/VidShrink.Core/HdrResolver.cs:81) | Soluk ya da fazla koyu çıkan videonun renk aralığı düzeltilir. | orta | 2 | gerekmez | [4][8][9][25] |
| DON-27 | Özel ffmpeg argümanı: Dönüştür komutuna elle ek seçenek yazmak. | Shutter Encoder, FFmpeg Batch AV Converter, Permute, Subtitle Edit ve 1 rakip daha | yarım: komut salt okunur gösteriliyor (src/VidShrink.App/MainWindow.axaml:1371), ek argüman yalnız yapıştırılan yapay zekâ planında (src/VidShrink.Core/EncodePlan.cs:158) | Arayüzde olmayan bir ffmpeg seçeneği için programdan çıkmak gerekmez. | küçük | 2 | gerekmez | [8][9][10][14][21][23] |
| DON-28 | Komut satırından dönüştürme: `vidshrink` komutunda kap, ara kodek ve GIF çıktısı seçmek. | HandBrake, LosslessCut, MKVToolNix, gifski ve 1 rakip daha | yarım: komut satırı yalnız küçültme yolunu sürüyor (src/VidShrink.Cli/CliRequest.cs:395) | Dönüşüm betiklere ve zamanlanmış işlere bağlanır. | orta | 2 | gerekmez | [8][35][37][39][41] |
| DON-29 | Meta veri düzenleme: başlık, sanatçı, tarih gibi etiketleri dönüşümde yazmak ya da düzeltmek. | Permute, LosslessCut, MKVToolNix, Wondershare UniConverter | yarım: yalnız "meta veriyi sil" kutusu var (src/VidShrink.App/MainWindow.axaml:1357) | Dosya kitaplıkta ve oynatıcıda doğru adla görünür. | orta | 2 | gerekmez | [23][26][34][37][48] |
| DON-30 | Bölüm düzenleme: bölüm işaretlerini taşımayı seçmek, adlandırmak ya da dosyadan almak. | HandBrake, MKVToolNix, LosslessCut, Wondershare UniConverter ve 1 rakip daha | yarım: motor Küçült yolunda var (src/VidShrink.Core/StreamMapping.cs:302), yalnız bölümleri taşımak için; adlandırma ve dosyadan alma hiçbir yolda yok, Dönüştür'de bölüm seçeneği yok | Uzun kayıt oynatıcıda bölüm bölüm gezilir. | orta | 2 | gerekmez | [1][8][14][26][34][37] |
| DON-31 | İz etiketleri: ses ve altyazı izinin dilini, adını, varsayılan ve zorunlu bayrağını düzenlemek. | MKVToolNix, LosslessCut, Permute, HandBrake | yarım: motor Küçült yolunda var (src/VidShrink.Core/StreamMapping.cs:282), yalnız kaynağın dilini, varsayılan ve zorunlu bayrağını taşımak için; düzenleme arayüzü hiçbir yolda yok, Dönüştür hiç yazmıyor | Oynatıcı doğru dili kendiliğinden seçer, izler adıyla görünür. | orta | 2 | gerekmez | [8][23][35][37][49] |
| DON-32 | Yapay zekâ ile büyütme: düşük çözünürlüklü videoyu modelle 2 ya da 4 kat büyütmek. | Wondershare UniConverter, Movavi Video Converter, VideoProc Converter, Any Video Converter | yok | Düşük çözünürlüklü eski video büyük ekranda daha net görünür. | büyük | 2 | sahip: yüzlerce MB model indirmesi ve yeni ikili dağıtımı gerekir | [25][26][29][30] |
| DON-33 | VVC (H.266) çıktısı: HEVC'nin ardılı olan kodekle yazmak. | Shutter Encoder, FastFlix | yok | Aynı kalite daha küçük dosyaya sığar, ama bugün çalan cihaz az. | orta | 3 | sahip: paketteki ffmpeg'de libvvenc var mı ölçülmedi, yoksa yeni ikili gerekir | [9][20] |
| DON-34 | CineForm: GoPro kökenli ara kodekle yazmak. | Shutter Encoder, Wondershare UniConverter | yok | CineForm bekleyen kurgu akışına doğrudan dosya verilir. | küçük | 3 | gerekmez | [9][28] |
| DON-35 | HAP: sahne ve canlı görsel yazılımlarının kullandığı hızlı çözülen kodekle yazmak. | Shutter Encoder | yok | Canlı gösteri yazılımları videoyu takılmadan çalar. | küçük | 3 | sahip: paketteki ffmpeg'de hap kodlayıcısı var mı ölçülmedi | [9][11] |
| DON-36 | Eski kodek aileleri: MPEG-2, MPEG-4 Part 2 (Xvid), WMV ve Theora ile yazmak. | HandBrake, Shutter Encoder, Any Video Converter, VidCoder | yarım: AVI kabı var ama içine yalnız H.264 yazılıyor (src/VidShrink.Core/ConversionArguments.cs:295), eski kodekler kodek listesinde yok (src/VidShrink.App/MainWindow.axaml:1149) | Eski DVD oynatıcı, araç teybi ve televizyonların çaldığı dosya üretilir. | küçük | 3 | gerekmez | [1][9][18][25] |
| DON-37 | MPEG-TS kabı: yayın ve kamera dünyasının .ts ve .m2ts kabına yazmak. | tsMuxer, Any Video Converter | yarım: düzenleyici ara parçayı mpegts olarak yazıyor (src/VidShrink.Core/Editing/EditExport.cs:764), Dönüştür'ün kap listesinde yok (src/VidShrink.App/MainWindow.axaml:1119) | Televizyon ve yayın araçlarının beklediği kap elde edilir. | küçük | 3 | gerekmez | [25][36] |
| DON-38 | Altyazı biçimi dönüştürme: altyazı dosyasını SRT, ASS ve VTT arasında çevirmek. | Subtitle Edit, XMedia Recode | yarım: Küçült yolu gömülü altyazıyı kabın biçimine çeviriyor, örneğin MP4 için mov_text (src/VidShrink.Core/StreamMapping.cs:774), dosyadan dosyaya çeviri yok | Eldeki altyazı dosyası oynatıcının ya da sitenin istediği biçime çevrilir. | küçük | 3 | gerekmez | [12][38] |
| DON-39 | Kanal indirgeme: 5.1 sesi stereoya ya da monoya indirmek. | HandBrake | yarım: motor Küçült yolunda var (src/VidShrink.Core/StreamMapping.cs:317), seçim yalnız Küçült'ün Gelişmiş panelinde (src/VidShrink.App/MainWindow.axaml.cs:1609) | Ev sineması sesli film telefonda ve kulaklıkta doğru karışımla çalar. | küçük | 3 | gerekmez | [8] |
| DON-40 | Örnekleme hızı seçimi: ses çıktısının 44,1 ya da 48 kHz olmasını elle seçmek. | HandBrake, XMedia Recode, Shutter Encoder | yarım: motor Küçült yolunda var (src/VidShrink.Core/StreamMapping.cs:316), komut satırında `--ses-hizi` ile açılıyor (src/VidShrink.Cli/CliRequest.cs:530); Dönüştür'de yalnız MXF için sabit 48 kHz (src/VidShrink.Core/ConversionArguments.cs:253) | Cihazın ya da kurgu projesinin istediği örnekleme hızı elle verilir. | küçük | 3 | gerekmez | [8][9][12] |
| DON-41 | Sabit ve değişken kare hızı seçimi: çıktının kare hızı kipini (CFR ya da VFR) Dönüştür'de seçmek. | HandBrake | yarım: motor Küçült yolunda var (src/VidShrink.Core/VideoFilterChain.cs:379), Dönüştür yalnız `fps` süzgecini yazıyor (src/VidShrink.Core/ConversionArguments.cs:237) | Telefon kaydındaki değişken kare hızı, kurgu yazılımının istediği sabit hıza çevrilir. | küçük | 3 | gerekmez | [1][8] |
| DON-42 | Kare hızı dönüşüm kipi: kare atmak yerine harmanlama ya da hareket tahminiyle ara kare üretmek. | Shutter Encoder | yarım: yalnız `fps` süzgeci var, kare atıyor ya da çoğaltıyor (src/VidShrink.Core/ConversionArguments.cs:237) | 30'dan 24'e ya da 25'ten 30'a geçişte takılma azalır. | orta | 3 | gerekmez | [9][54] |
| DON-43 | Ölçekleme algoritması ve dolgu: lanczos dışında algoritma seçmek ve en-boy farkını siyah bantla doldurmak. | Shutter Encoder, HandBrake | yarım: motor Küçült yolunda var (src/VidShrink.Core/VideoFilterChain.cs:347), yalnız dolgu için; ölçekleme algoritması iki yolda da sabit lanczos (src/VidShrink.Core/ConversionArguments.cs:232) | Farklı en-boy oranı esnetilmeden hedef çerçeveye oturur. | küçük | 3 | gerekmez | [8][9] |
| DON-44 | Tek kare kaydetme: Dönüştür'den seçilen anın karesini resim olarak almak. | LosslessCut, VideoProc Converter, FFmpeg Batch AV Converter | yarım: kare kaydı oynatıcıda var (src/VidShrink.Player/MpvEngine.cs:538), Dönüştür'de yok | Videodan kapak resmi almak için sekme değiştirmek gerekmez. | küçük | 3 | gerekmez | [14][30][37] |
| DON-45 | Sağ tıkla dönüştürme: Gezgin'de dosyaya sağ tıklayıp hazır bir biçime çevirmek. | File Converter | yarım: sağ tık menüsünde yalnız Aç ve Küçült var (src/VidShrink.App/MainWindow.axaml:1656) | Sık yapılan dönüşüm program açılmadan başlar. | küçük | 3 | gerekmez | [15][59] |
| DON-46 | Yapay zekâ ile ara kare: modelle ara kare üretip kare hızını yükseltmek. | VideoProc Converter, Wondershare UniConverter | yok | Düşük kare hızlı video daha akıcı görünür. | büyük | 3 | sahip: model indirmesi ve yeni ikili dağıtımı gerekir | [26][30] |
| DON-47 | Cihaz ve platform ön ayarları: telefon, konsol, televizyon ya da site adına göre hazır Dönüştür ayarı seçmek. | HandBrake, XMedia Recode, Wondershare UniConverter, VideoProc Converter ve 3 rakip daha | yarım: 18 hedef yalnız Küçült'te ve boyut odaklı (src/VidShrink.Core/Presets/platformlar.json:5), Dönüştür'de hazır hedef yok | Kullanıcı kodek bilmeden cihazının adını seçip çalışan dosyayı alır. | orta | 3 | kapalı: 85 ön ayar bilerek yapılmaz | [2][11][12][28][30] |
| DON-48 | URL'den indirme: bağlantıdaki videoyu indirip aynı adımda dönüştürmek. | Shutter Encoder, FFmpeg Batch AV Converter, Format Factory | yok | Bağlantıdaki video ayrı bir indirici olmadan dosyaya döner. | orta | 3 | kapalı: K1 | [9][10][14][19] |
| DON-49 | DVD ve Blu-ray: diskten ya da disk klasöründen okuyup dönüştürmek. | Shutter Encoder, Format Factory, Any Video Converter | yarım: DVD-Video başlığını okuyan girdi yolu motorda ve komut satırında var (src/VidShrink.Core/SourceTitles.cs:28, src/VidShrink.Cli/CliRequest.cs:471), Blu-ray yok, Dönüştür'de disk kaynağı yok | Diskteki film dosyaya aktarılır. | büyük | 3 | kapalı: DVD ve Blu-ray bilerek yapılmaz | [9][19][25] |
| DON-50 | LUT uygulama: hazır renk tablosunu (LUT) dönüşümde görüntüye uygulamak. | Shutter Encoder | yok | Log çekilmiş soluk görüntü tek adımda izlenebilir renge döner. | orta | 3 | kapalı: K8 | [9][51] |
| DON-51 | Filigran: dönüşümde videoya logo ya da yazı basmak. | Shutter Encoder | yok | Paylaşılan videoda sahibin işareti durur. | orta | 3 | kapalı: K8 | [9] |
| DON-52 | Otomatik altyazı: konuşmayı yazıya döküp altyazı üretmek. | Shutter Encoder, Permute | yok | Altyazısız videoya elle yazmadan altyazı çıkar. | büyük | 3 | kapalı: K5 | [10][23] |

## Düzeltmeler

Önceliği değişen tek satır `DON-09` (2'den 1'e). Tek İş değişikliği `DON-04`. Geri kalanı Bizde Durum, Kimde Var ve kanıt satırı düzeltmesi.

| No | Ne Değişti | Kanıt |
|---|---|---|
| `DON-01` | Bizde Durum genişledi: motor Küçült yolunda var ama tek tek iz seçmeyi o da bilmiyor; yalnız "tüm izleri koru" ve dil tercihi var. İş `orta` kaldı, çünkü seçim alanı motora da yeni yazılacak. | src/VidShrink.Core/StreamMapping.cs:140, src/VidShrink.Core/StreamMapping.cs:255, src/VidShrink.App/MainWindow.axaml:860, src/VidShrink.App/MainWindow.axaml.cs:2427 |
| `DON-02` | Bizde Durum "motor Küçült yolunda var" kalıbına çekildi; kanıt satırı 274 yerine `-c:s` kararının başladığı 271. | src/VidShrink.Core/StreamMapping.cs:271 |
| `DON-03` | Kanıt, alan tanımı (83) yerine süzgeci yazan işleve (233) çekildi; Küçült arayüzündeki bağ eklendi. | src/VidShrink.Core/VideoFilterChain.cs:233, src/VidShrink.App/MainWindow.Izler.cs:92 |
| `DON-04` | İş `orta` yerine `küçük`. Donanım yolu Küçült'te değil, Dönüştür'ün kendi motorunda zaten bağlı: cihaz argümanı, yükleme süzgeci ve kalite argümanı kodeğe göre üretiliyor, kap uyum tablosu bu adları kabul ediyor, testi var. Eksik yalnız liste ve yoklama. | src/VidShrink.Core/ConversionArguments.cs:78, src/VidShrink.Core/ConversionArguments.cs:134, src/VidShrink.Core/ConversionArguments.cs:144, src/VidShrink.Core/ConversionArguments.cs:291, tests/VidShrink.Tests/HardwareEncoderTests.cs:218 |
| `DON-08` | Kimde Var değişti: Adapter çıktı (resmi sayfasında yalnız videodan resim dizisine çıktı görüldü), Apple Compressor girdi. Kaynak sütunundan [24] düştü. Öncelik `1` kaldı: iki resmi kaynak ve 47 oyluk açık istek var. | https://support.apple.com/guide/compressor/cpsr0f8fb952/mac ("Import an image sequence in Compressor", sürüm 5.4, File menüsünde Add Image Sequence); [9]; HandBrake istek 2346, 47 oy (`gh api`) |
| `DON-09` | Öncelik `2` yerine `1`. İkinci rakip bulundu: Shutter Encoder'ın altyazı ekleme penceresinde çerçeve, arka plan ve konum seçiliyor; Subtitle Edit'te yazı tipi, renk ve kutu da var. İki rakip ve 40 oyluk açık istek, ölçütün "oyla kanıtlı talep" koluna giriyor; komşu tablo da aynı eksiği 1 saydı. Kimde Var'a Shutter Encoder, Kaynak'a [10] eklendi. Kanıt satırı da 83 yerine 233: yakma süzgeci yalnız dosya adı ve iz numarası yazıyor, kodda `force_style` hiç geçmiyor. | [10] sürüm notları 12.2 ve 14.9 ("Add subtitles" penceresi, "outline option"); [21] "Font Settings" bölümü (Font name, Box type, Text color); HandBrake istek 146, 40 oy (`gh api`); src/VidShrink.Core/VideoFilterChain.cs:233 |
| `DON-10` | Bizde Durum "yalnız komut satırında" diyordu; motor ve Küçült arayüzü de var. Eksik yalnız Dönüştür bağı. | src/VidShrink.Core/StreamMapping.cs:798, src/VidShrink.App/MainWindow.Izler.cs:91, src/VidShrink.App/MainWindow.Izler.cs:96, src/VidShrink.Cli/CliRequest.cs:128 |
| `DON-12` | Kanıt satırı yanlıştı: 260, ön ayar silme işlevinin içindeki yazma. Kullanıcıya dönük dışa aktarma 385'te, içe aktarma 301'de; Küçült'te içe aktarma da var. | src/VidShrink.App/MainWindow.OnAyar.cs:260, src/VidShrink.App/MainWindow.OnAyar.cs:385, src/VidShrink.App/MainWindow.OnAyar.cs:301, src/VidShrink.App/MainWindow.axaml:450 |
| `DON-14` | Bizde Durum kalıba çekildi; içerik aynı. | src/VidShrink.Core/VideoFilterChain.cs:63, src/VidShrink.Core/VideoFilterChain.cs:321 |
| `DON-16` | Motor satırı eklendi: ses yüksekliği alanı Küçült'ün akış isteğinde duruyor. | src/VidShrink.Core/StreamMapping.cs:144, src/VidShrink.App/MainWindow.Izler.cs:88 |
| `DON-17` | Eksik iki yerde: ses listesi ve Dönüştür'ün kap uyum tablosu. İkincisi tabloda yazmıyordu. | src/VidShrink.Core/ConversionArguments.cs:300 |
| `DON-20` | Bizde Durum kalıba çekildi; içerik aynı. | src/VidShrink.Core/StreamMapping.cs:430, src/VidShrink.Core/ConversionArguments.cs:272 |
| `DON-22` | `yok` yerine `yarım`: hareketli WebP ve AVIF çıktısı var. Fayda sütunundaki kazancın çoğu bugün bu iki biçimle alınıyor; eksik olan yalnız APNG'nin kendisi. | src/VidShrink.Core/ConversionPlan.cs:34 |
| `DON-26` | Bizde Durum eksikti: Küçült yolunda renk matrisi seçimi var ama yalnız BT.709 ve BT.601. BT.2020 ve sınırlı ya da tam aralık seçimi hiçbir yolda yok. | src/VidShrink.Core/VideoFilterChain.cs:73, src/VidShrink.Core/VideoFilterChain.cs:397 |
| `DON-30` | Bizde Durum kalıba çekildi ve sınırı yazıldı: motor yalnız taşıyor, adlandırma ve dosyadan alma yok. | src/VidShrink.Core/StreamMapping.cs:302 |
| `DON-31` | Bizde Durum kalıba çekildi; kanıt alan tanımı (182) yerine bayrağın yazıldığı satır (282). | src/VidShrink.Core/StreamMapping.cs:282 |
| `DON-39` | Motor satırı eklendi (`-ac`). | src/VidShrink.Core/StreamMapping.cs:317 |
| `DON-40` | Motor satırı eklendi (`-ar`); komut satırı kanıtı bayrak listesinden (350) bayrağın okunduğu satıra (530) çekildi. | src/VidShrink.Core/StreamMapping.cs:316, src/VidShrink.Core/StreamMapping.cs:152, src/VidShrink.Cli/CliRequest.cs:530 |
| `DON-41` | Bizde Durum kalıba çekildi; Dönüştür tarafının kanıtı eklendi. | src/VidShrink.Core/VideoFilterChain.cs:379, src/VidShrink.Core/ConversionArguments.cs:237 |
| `DON-43` | "Dolgu yok" yalnız Dönüştür için doğru: dolgu Küçült yolunda yazılı. Ölçekleme algoritması ise iki yolda da sabit. | src/VidShrink.Core/VideoFilterChain.cs:71, src/VidShrink.Core/VideoFilterChain.cs:347, src/VidShrink.Core/VideoFilterChain.cs:338 |
| `DON-49` | `yok` yerine `yarım`: DVD-Video başlığı için girdi seçenekleri (`-f dvdvideo -title`) motorda yazılı ve komut satırında başlık bayrağı var. Karar `kapalı` kaldı. | src/VidShrink.Core/SourceTitles.cs:28, src/VidShrink.Core/FfmpegArguments.cs:550, src/VidShrink.Cli/CliRequest.cs:471 |

## Çıkarılanlar

| No | Eksik | Neden | Kanıt (dosya:satır) |
|---|---|---|---|

Çıkarılan satır yok. Hiçbir eksik Dönüştür'de tam olarak bulunmadı; en yakını `DON-04`, onun da listesi eksik.

## Yinelenenler

Komşu tablo `kucult-eksikler.md` (46 satır) en sonda okundu. Altı eksik iki tabloda da var; satırlar silinmedi.

- DON-07 = SIK-03: harici ses dosyası ekleme. İki tabloda da öncelik 1, iş `orta`. İkinci girdi ve iz eşlemesi motorda bir kez yazılır, arayüz iki sekmede ayrı; iki paket aynı işçiye ya da art arda verilmeli.
- DON-09 = SIK-04: gömme altyazıda biçem. Doğrulamadan sonra iki tabloda da öncelik 1 (ham tablolarda 2 idi). İş burada `küçük`, komşuda `orta`. Süzgeç ortak (`src/VidShrink.Core/VideoFilterChain.cs:233`), biçem bir kez yazılır; iki paket aynı işçiye ya da art arda verilmeli.
- DON-33 = SIK-16: VVC çıktısı. Öncelik farklı: burada 3 (iki rakip, çalan cihaz az), komşuda 2 (dört rakip). Karar iki tabloda da `sahip`. Kodlayıcı tek olduğu için hüküm de tek olmalı; komşuda iş açılırsa Dönüştür'e eklemek küçük kalır.
- DON-42 = SIK-43: kare hızı değişirken ara kare üretimi. İki tabloda da öncelik 3; iş burada `orta`, komşuda `küçük`.
- DON-50 = SIK-46: LUT uygulama. İkisi de `kapalı: K8`.
- DON-52 = SIK-45: konuşmadan altyazı üretme. İkisi de `kapalı: K5`.

Yakın ama ayrı iki çift: `DON-13` (toplu dönüştürmede dosya başına ayar) ile `SIK-08` (Küçült kuyruğunda bekleyen işi düzenleme) aynı gereksinimin iki sekmedeki hali, kodu ayrı. `DON-46` (modelle ara kare) ile `SIK-43` aynı değil; biri model ister, öbürü ffmpeg süzgeciyle olur.

## Doğrulanamayanlar

- `DON-47` kararsız, satır olduğu gibi bırakıldı. "Kapalı: 85 ön ayar bilerek yapılmaz" yorumu kesin değil. Karar metni HandBrake'in 85 ön ayarını **birebir** eşlemeyi reddediyor (`docs/handbrake/durum-2026-09-23.md:138`), cihaz hedeflerinin tümünü değil. ORTAK.md karar listesinde bu kalem yok. Küçült'teki 18 hedefin dördü zaten cihaz hedefi (`src/VidShrink.Core/Presets/platformlar.json:19`). Sahip "birebir 85 değil, bir avuç cihaz hedefi" derse satır öncelik 2 olur ve `DON-P5`'in yanına girer.
- `DON-13`: üç rakibin hiçbirinde "dosya başına ayrı ayar" görülmedi. Convertio'nun sayfası dosya başına yalnız biçim seçtiriyor; Shutter Encoder belgesinde ve FFmpeg Batch AV Converter'ın sitesinde bu özellik yazmıyor. Öncelik 2 bırakıldı.
- `DON-08`: FFmpeg Batch AV Converter'ın depo sayfası "resimden video" diyor, "numaralı dizi" demiyor; sitesinde hiç geçmiyor. Adapter'da yalnız çıktı yönü görüldü. Sağlam iki kaynak Shutter Encoder ve Apple Compressor.
- `DON-05`: File Converter'ın depo sayfasında adlı ön ayar kaydı görülmedi. Satır HandBrake, Shutter Encoder ve Permute ile ayakta.
- `DON-09`: Shutter Encoder'da yazı tipi ve renk seçimi görülmedi; sürüm notları yalnız çerçeve, arka plan ve konumu yazıyor, işlev belgesi biçemden hiç söz etmiyor. Dört ayarın dördü yalnız Subtitle Edit'te belgeli.
- `DON-21`: FFV1 için açık bir karar bulunamadı. Eski durum belgesi FFV1'i ProRes ve DNxHR ile birlikte "bilerek yapılmaz" sayıyordu (`docs/handbrake/durum-2026-09-23.md:39`); K12 ProRes ve DNxHR'ı açtı, FFV1'den söz etmiyor. ORTAK.md listesinde olmadığı için satır açık bırakıldı.
- `DON-33`, `DON-35`: paketteki ffmpeg'in kodlayıcı listesine bakılamadı, çünkü bu turda program çalıştırmak yasak. Bilinen: Windows x64 için GyanD 9.0 "full build", arm64 için BtbN derlemesi iniyor (`src/VidShrink.Core/Setup/SetupModel.cs:81`, `:91`). İki derleme ayrı kaynaktan geldiği için yoklama ikisinde de yapılmalı.
- `DON-32`, `DON-46`: bütün rakipleri alt ajan raporundan geliyor (notlar satır 248); bu turda o sayfalar açılmadı. Alt ajana dayanan rakiplerden yalnız Permute, Adapter ve Convertio'nun sayfası açıldı.
- `DON-25`: Küçült'teki HDR paneli gizli başlıyor (`src/VidShrink.App/MainWindow.axaml:798`). HDR kaynakta açıldığı ve Dönüştür'ün o seçimi okuduğu kodda görüldü, ekranda görülmedi.

## Ek Notlar

- Dönüştür'ün kodek, kap ve ses seçimi ayarlara sıra numarasıyla yazılıyor (`src/VidShrink.App/MainWindow.axaml.cs:1412`). Listeye öğe ekleyen her paket kayıtlı seçimi kaydırır; `DON-P1` bunu adla saklamaya çevirmeden bitmiş sayılmaz.
- Paketteki ffmpeg iki ayrı kaynaktan geliyor (`src/VidShrink.Core/Setup/SetupModel.cs:81`, `:91`). Yeni kodlayıcı ekleyen her satır, olmayan seçeneği gizleyen yoklamaya girmeli (`src/VidShrink.App/MainWindow.DonusturKap.cs:33`).
- Küçült ön ayarı HandBrake ön ayar dosyasını içe alabiliyor (`src/VidShrink.App/MainWindow.OnAyar.cs:342`). Dönüştür ön ayarı yazılırken bu çeviricinin Dönüştür alanlarını da doldurması tabloda satır olarak yok.
- Karar "DVD ve Blu-ray bilerek yapılmaz" diyor, ama kodda DVD-Video başlığı okuyan bir yol duruyor (`src/VidShrink.Core/SourceTitles.cs:28`). Çelişki değilse karar metni "arayüzde yok, komut satırında var" diye netleşmeli.
- Defterde gömme altyazının ölçülmemiş halleri yazılı (`.claude/acik.md` satır 32, 33, 38, 54) ve K12'nin yeni liste öğeleri ekranda görülmedi notu var (satır 111). `DON-P3` ve listeye öğe ekleyen paketler aynı borcu devralır.
- Paket sayısı on beş; beklenen üst sınır on ikiydi. 32 satır pakete girmek zorunda ve paketler `orta` kalsın diye bölündü. On ikiye indirmek gerekirse üç birleşme temiz: `DON-P3` ile `DON-P10` (aynı süzgeç zinciri), `DON-P5` ile `DON-P6` (ön ayar ve sıra), `DON-P7` ile `DON-P8` (yeni girdi türleri). Üçü de `büyük` olur.

## Sekme Özeti

- Dönüştür bugün tek dosyalık ince bir yol: 13 kap, yazılım kodlayıcıları, kesit, HDR ve meta veri silme. HandBrake ve Shutter Encoder'ın yanında iz, altyazı, toplu iş ve ön ayar tarafı boş.
- İyi haber: açığın büyük kısmı yazılmamış kod değil, bağlanmamış kod. 14 satırın motoru Küçült yolunda hazır; donanım kodlayıcıları ise Dönüştür'ün kendi motorunda bile hazır.
- En ağır eksik 1: iz ve altyazı. Dönüştür `-map` yazmadığı için ikinci ses ve altyazı sessizce kayboluyor (`DON-01`, `DON-02`, `DON-03`).
- En ağır eksik 2: toplu dönüştürme (`DON-06`). Rakiplerin hepsinde var, bizde çoklu dosya Küçült'e gidiyor.
- En ağır eksik 3: adlı ön ayar (`DON-05`). Dönüştür her seferinde baştan kuruluyor.
- En ucuz üç kazanç: donanım kodlayıcılarını listeye eklemek (`DON-04`), MP4 ses kopya tablosunu Küçült'le eşitlemek (`DON-20`), ses yüksekliği ile kanal ve örnekleme hızını bağlamak (`DON-16`, `DON-39`, `DON-40`).
- Öncelik 1'e gelen sekiz satırın sekizi de iki ayrı kaynakta görüldü; `DON-09` dokuzuncu olarak eklendi. Öncelik 3'teki on dört açık satırın altısı bir paketin yanında bedavaya geliyor.

## Paket Önerisi

### DON-P1 — Dönüştür'de Donanım Kodlayıcıları
- Satırlar: DON-04
- İş: küçük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.DonusturKap.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Kodek listesine NVENC, QSV ve AMF kodlayıcıları (Nvidia, Intel ve AMD ekran kartlarının kodlayıcıları; H.264, HEVC, AV1) eklenir. Motor bu adları zaten sürüyor (`ConversionArguments.cs:78`, `:134`, `:144`), yeni argüman yazılmaz; yoklama makinede olmayan kodlayıcıyı gizler. Kayıtlı seçim sıra numarası yerine adla saklanır, yoksa yeni öğe eski seçimi kaydırır. Doğrulama: liste ile kap uyum tablosunu eşleyen test ve ekran kartlı bir makinede elle tek dönüşüm.

### DON-P2 — Dönüştür'de Altyazı İzleri
- Satırlar: DON-02, DON-10, DON-11
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/MainWindow.Izler.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Dönüştür planına altyazı alanı eklenir ve komut, Küçült'ün akış planından (`StreamMapping.cs:255`) açık `-map` ve `-c:s` alır; kabın anlamadığı altyazı oradaki kuralla çevrilir ya da düşer. Dış .srt ve .ass dosyası aynı planın ek girdisi olarak bağlanır (`StreamMapping.cs:798`). Çıkarma, seçilen izi tek başına altyazı dosyasına yazan ayrı ve kısa bir komuttur. Doğrulama: kap başına argüman testleri (en az MP4, MKV ve WebM) ve açık `-map` yazılınca ses seçiminin değişmediğini gösteren test.

### DON-P3 — Dönüştür'de Altyazı Gömme
- Satırlar: DON-03, DON-09
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.Izler.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Dönüştür'ün kendi süzgeç listesi (`ConversionArguments.cs:229`) yerine Küçült'ün zinciri (`VideoFilterChain.cs:321`) çağrılır; yakma süzgeci (`VideoFilterChain.cs:233`) ve resim altyazı bindirmesi böylece hazır gelir. Arayüzde Küçült'teki yakma seçimi (`MainWindow.Izler.cs:48`) Dönüştür'e de konur. Biçem için süzgece `force_style` (yazı tipi, boyut, renk, kutu) yazılır; bu yalnız metin altyazıda çalışır. Doğrulama: süzgeç metni testleri ve libass (altyazıyı çizen kitaplık) yoksa seçeneğin gizlendiğini gösteren test.

### DON-P4 — Dönüştür'de Ses İzi Seçimi
- Satırlar: DON-01
- İş: orta
- Karar: sahip: Dönüştür varsayılan olarak tek ses mi çıkarsın, yoksa kabın taşıyabildiği bütün sesleri mi taşısın.
- Dokunacağı yer: src/VidShrink.Core/StreamMapping.cs, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: DON-P2'nin bağladığı akış planına dayanır. Akış isteğine seçilen ses izlerinin listesi eklenir; bugün yalnız "tüm izleri koru" ve dil tercihi var (`StreamMapping.cs:140`). Arayüzde kaynağın ses izleri işaret kutulu liste olarak gösterilir, kopya kipinde kabın kabul etmediği ses için uyarı verilir. Doğrulama: iki sesli kaynakla argüman testleri; varsayılan, sahibin kararına göre testle sabitlenir.

### DON-P5 — Adlı Dönüştür Ön Ayarı
- Satırlar: DON-05, DON-12
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/PresetLibrary.cs, src/VidShrink.App/MainWindow.OnAyar.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Dönüştür panelindeki seçimler adla kaydedilir ve listeden geri yüklenir. Depolama Küçült ön ayarının kitaplığını örnek alır (`MainWindow.OnAyar.cs:228`) ama ayrı bir tür olarak durur, iki sekmenin ayarı karışmaz. Dışa ve içe aktarma, Küçült'teki düğmelerin eşi olarak eklenir (`MainWindow.OnAyar.cs:385`, `:301`). Doğrulama: kaydet, yükle, dışa aktar, içe al gidiş dönüş testi.

### DON-P6 — Toplu Dönüştürme Ve Dosya Başına Ayar
- Satırlar: DON-06, DON-13
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/ (yeni sıra penceresi), src/VidShrink.Ffmpeg/EncodeRunner.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Dönüştür'e bırakılan ya da seçilen birden çok dosya bir sıraya girer. Sıradaki her öğe kendi dönüşüm planını taşır; böylece "hepsine aynı ayar" ile "bu dosyaya ayrı ayar" aynı yapıdan çıkar. Küçült kuyruğunun penceresi örnek alınır (`MainWindow.axaml.cs:3324`); bir dosyanın hatası sırayı durdurmaz, sonda özet verilir. Doğrulama: sıra mantığı için sahte koşucuyla testler ve üç dosyalık elle deneme.

### DON-P7 — Harici Ses Dosyası Ekleme
- Satırlar: DON-07
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Dönüştür'de ikinci girdi olarak bir ses dosyası seçilir; iki kip sunulur: sesi onunla değiştir ya da ek iz olarak ekle. Komut ikinci `-i` ve açık `-map` yazar; görüntü kopyalanabiliyorsa yeniden kodlanmaz. Süreler tutmuyorsa kısa olanda bitirme seçeneği konur. Doğrulama: iki kip için argüman testleri ve kabın o sesi kabul etmediği durumda yeniden kodlamaya düşüş testi.

### DON-P8 — Görüntü Dizisi Girdisi Ve Çıktısı
- Satırlar: DON-08, DON-23, DON-44
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/MainWindow.DonusturKap.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Girdi yönünde numaralı resim klasörü seçilir, kare hızı sorulur ve ffmpeg'in resim dizisi okuyucusuyla (`image2`) tek videoya çevrilir. Çıktı yönünde kap listesine PNG dizisi ve JPEG dizisi eklenir, kareler bir klasöre numaralı yazılır; tek kare kaydetme aynı yolun tek karelik halidir. Doğrulama: desen ve kare hızı argüman testleri, numarasında boşluk olan dizide davranış testi.

### DON-P9 — Dönüştür'de Ses Seçenekleri
- Satırlar: DON-16, DON-17, DON-18, DON-19, DON-20, DON-39, DON-40
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/MainWindow.DonusturKap.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Önce tablolar: ses listesine AC3 ve E-AC3, ses kaplarına Opus, OGG, AIFF ve AC3 eklenir; MP4 kopya tablosu Küçült'ünkiyle eşitlenir (`StreamMapping.cs:430`); ses kabı seçilince kaynak ses o kaba sığıyorsa kopyalanır. Sonra işleme: ses yüksekliği (`loudnorm`), kanal (`-ac`) ve örnekleme hızı (`-ar`) plana alan olarak girer; karşılıkları Küçült yolunda yazılı (`StreamMapping.cs:316`, `:317`). Bu üçünden biri seçilince ses kopyası kapanır. Doğrulama: kap ve kodek uyum tablosu testleri, her yeni kodlayıcının iki ffmpeg derlemesinde yoklanması.

### DON-P10 — Dönüştür'de Görüntü Süzgeçleri
- Satırlar: DON-14, DON-41, DON-43
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: DON-P3'ün bağladığı zincire dayanır. Dönüştür paneline kırpma, döndürme, tarama çözme ve gürültü azaltma seçimleri konur; değerler Küçült'teki süzgeç seçeneklerine yazılır (`VideoFilterChain.cs:63`). Sabit ya da değişken kare hızı seçimi (`VideoFilterChain.cs:379`) ve siyah bantla dolgu (`VideoFilterChain.cs:347`) aynı zincirden gelir; ölçekleme algoritması zincire yeni bir alan olarak eklenir. Doğrulama: zincir sırası testleri ve kopya kipinde süzgeç seçilince yeniden kodlamaya geçiş testi.

### DON-P11 — Kodlayıcı, HDR Ve Renk Ayarları
- Satırlar: DON-15, DON-25, DON-26
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/HdrResolver.cs, src/VidShrink.Core/VideoFilterChain.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Hız ön ayarı, tune, profil, seviye ve bit hızı kipinde iki geçiş Dönüştür'de seçilebilir olur; bugün sabit varsayılan yazılıyor (`ConversionArguments.cs:142`). HDR seçimi Küçült'teki düğmeden okunmak yerine (`MainWindow.axaml.cs:4777`) Dönüştür'ün kendi panelinde gösterilir. Renk için BT.601, BT.709 ve BT.2020 ile sınırlı ve tam aralık seçimi eklenir; BT.709 ve BT.601 kolu hazır (`VideoFilterChain.cs:397`), BT.2020 ve aralık yeni yazılır. Doğrulama: argüman testleri ve renk kolu için ölçüm aracıyla önce ve sonra kare karşılaştırması.

### DON-P12 — Arşiv Kodeği, APNG Ve GIF Ayarı
- Satırlar: DON-21, DON-22, DON-24, DON-34
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/CodecModel.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.DonusturKap.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Kodek listesine FFV1 ve CineForm, hareketli görsel kaplarına APNG eklenir; her biri kap uyum tablosuna ve kullanılabilirlik yoklamasına girer. GIF için döngü sayısı, renk sayısı ve dither (az renkle geçişleri noktalayarak yumuşatma) seçimi plana alan olarak eklenir; bugün palet ve dither sabit (`ConversionArguments.cs:105`). Kayıplı GIF sıkıştırması ffmpeg'in GIF kodlayıcısında yoksa bu parça dışarıda kalır; ölçülmedi. Doğrulama: argüman testleri ve yeni kodlayıcıların iki ffmpeg derlemesinde yoklanması.

### DON-P13 — Özel Argüman Ve Komut Satırı
- Satırlar: DON-27, DON-28
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Cli/CliRequest.cs, src/VidShrink.Cli/, src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Dönüştür paneline "ek argüman" kutusu konur; yazılan seçenekler komutta çıktı yolundan önce yer alır ve salt okunur komut önizlemesinde görünür (`MainWindow.axaml:1371`). Komut satırına ayrı bir dönüştürme komutu eklenir: kap, kodek, kalite ve kesit bayrakları Dönüştür planına çevrilir; GIF ve ara kodek (ProRes, DNxHR) çıktısı buradan da alınır. Doğrulama: bayrak ayrıştırma testleri ve arayüzle komut satırının aynı plandan aynı argümanı ürettiğini gösteren test.

### DON-P14 — Meta Veri, Bölüm Ve İz Etiketleri
- Satırlar: DON-29, DON-30, DON-31
- İş: büyük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/ConversionArguments.cs, src/VidShrink.Core/ConversionPlan.cs, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Dönüştür'e tek bir "Etiketler" bölümü eklenir; üç parça aynı bölüme ve aynı plan alanlarına yazıldığı için tek pakette durur. Sırayla: başlık, sanatçı ve tarih alanları (`-metadata`); bölümleri taşı ya da at seçimi ve bölüm adlarını düzenleme (`StreamMapping.cs:302`); izlerin dili, adı, varsayılan ve zorunlu bayrağı (`StreamMapping.cs:282`). DON-P2'nin bağladığı akış planına dayanır. Doğrulama: argüman testleri ve çıktının ffprobe ile okunan etiketlerini karşılaştıran test.

### DON-P15 — Yapay Zekâ İle Büyütme
- Satırlar: DON-32
- İş: büyük
- Karar: sahip: Yüzlerce MB'lık model ve yeni bir ikili dosya kullanıcıya indirtilsin mi.
- Dokunacağı yer: src/VidShrink.Core/Setup/SetupModel.cs, src/VidShrink.Core/, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/Locales/, tools/VidShrink.Bench/
- Plan: Karar çıkmadan iş açılmaz. Çıkarsa önce ölçüm: aday modelin bu makinedeki hızı ve çıktı kalitesi ölçüm aracıyla ölçülür, lisansı ve indirme boyu yazılır. Sonra model, kurucunun pinli ve sha256'lı indirme yoluna isteğe bağlı bileşen olarak eklenir (`SetupModel.cs:81`) ve Dönüştür'de 2 kat ile 4 kat seçenekleri açılır.

## Pakete Girmeyenler

- DON-33, DON-35: karar `sahip` ve paketteki ffmpeg'de kodlayıcının olup olmadığı bilinmiyor; öncelik 3.
- DON-36, DON-37: dar kitle; her biri yeni kalite eşlemesi ve kap kuralı ister, bir paketin yanında bedavaya gelmiyor.
- DON-38: altyazı dosyasını girdi olarak almak gerekir; Dönüştür bugün yalnız video ve ses dosyası açıyor.
- DON-42: tek rakipte var ve `orta` iş; hiçbir paketin yanında ucuza gelmiyor.
- DON-45: sağ tık menüsü Ayarlar ve kabuk kolunun alanı; tek rakipte var.
- DON-46: karar `sahip`, iş `büyük`, model indirmesi ister; öncelik 3.
- DON-47: kararı belirsiz, Doğrulanamayanlar'da; sahip açarsa `DON-P5`'in yanına girer.
- DON-48, DON-49, DON-50, DON-51, DON-52: kararı `kapalı`.
