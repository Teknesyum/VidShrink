# Düzenleyici Sekmesi — Kurgu (NLE) Yönü: Doğrulanmış Eksikler

13 Ekim 2026. Araştırma kolunun 77 satırlık tablosu kodla ve kaynakla karşılaştırıldı; kod okuması `main` (eb0a5db0) çalışma ağacında yapıldı. NLE, klipleri bir çizelgeye serbestçe dizip üst üste koyan kurgu programı demek. Yol kısaltması: `Core/` = `src/VidShrink.Core/Editing/`, `App/` = `src/VidShrink.App/Editing/`. Kaynak numaraları `duzenleyici-kurgu.md` dosyasının Kaynaklar bölümüne bakar.

| No | Eksik | Kimde Var | Bizde Durum | Fayda | İş | Öncelik | Karar | Kaynak |
|---|---|---|---|---|---|---|---|---|
| DZB-01 | Müzik izi: videonun altına ayrı bir ses dosyası (müzik, anlatım) koyup birlikte teslim etmek. | Shotcut, OpenShot, Clipchamp, CapCut ve 2 rakip daha | yok; görüntüsü olmayan dosya kaynak olarak reddediliyor (App/EditorView.Kaynak.cs:89-93), tek ses izi A1 (App/EditorTimeline.cs:22) | Videoya fon müziği ya da sonradan kaydedilmiş anlatım eklenir. | orta | 1 | sahip: yazılı kapsam tek şerit, ikinci iz kapsamı genişletir; teslime ffmpeg amix, önizlemeye libmpv ek ses dosyası yeter | [1][6][9][19][23] |
| DZB-02 | İkinci video izi: bir klibin üstüne başka bir videoyu ya da resmi bindirmek (resim içinde resim). | Shotcut, OpenShot, Clipchamp, CapCut ve 2 rakip daha | yok; tek video izi V1 (App/EditorTimeline.cs:21), overlay süzgeci yalnız kaydedicide (RecorderArguments.cs:944) | Tepki videosu ve köşede kamera görüntüsü yapılabilir. | büyük | 1 | sahip: kapsam tek şeritten çıkar; teslime ffmpeg overlay yeter ama canlı önizleme yolu seçilmeli (libmpv lavfi-complex, kısa ön-işleme ya da ayrı bileşim motoru) | [1][2][6][9][19][24][26] |
| DZB-03 | Klip dönüşümü: klibi serbestçe ölçeklemek, konumlamak ve istenen açıyla döndürmek. | Shotcut, Kdenlive, OpenShot, DaVinci Resolve | yarım: kırpma, 90 derece döndürme ve çevirme var (Core/ClipEffects.cs:15-35) | Görüntü yakınlaştırılır, kaydırılır ya da eğik çekim düzeltilir. | orta | 2 | gerekmez | [6][9][14][26] |
| DZB-04 | Resim klibi: fotoğrafı süre vererek çizelgeye koymak. | Shotcut, OpenShot, Clipchamp | yok; süresi okunamayan kaynak reddediliyor (App/EditorView.Kaynak.cs:89-93), resimle denenmedi | Fotoğraflardan slayt yapılır, araya kapak görseli konur. | orta | 2 | gerekmez | [6][9][20] |
| DZB-05 | Düz renk klibi: başlığın arkasına ya da iki klibin arasına renkli zemin koymak. | Shotcut, Clipchamp | yok; `ColorClip`, `SolidColor`, `color=c=` aramaları boş | Siyah ya da renkli zemin üstünde başlık kartı yapılır. | küçük | 2 | gerekmez | [1][6][67] |
| DZB-06 | İz denetimleri: iz eklemek ve silmek, izi kilitlemek, susturmak, gizlemek, tek dinlemek (solo). | Shotcut, Kdenlive | yok; üç sabit iz (App/EditorTimeline.cs:539-541) | Bir izde çalışırken ötekiler yanlışlıkla bozulmaz ya da duyulmaz. | orta | 2 | sahip: çok iz kararına bağlı (DZB-01, DZB-02) | [6][14][42] |
| DZB-07 | Boşluklu çizelge: klipleri arada boşluk bırakarak istenen ana yerleştirmek. | Shotcut, Kdenlive | yok; klipler sıralı liste, hep art arda (Core/EditTimeline.cs:134) | Klip müzikteki belli bir ana denk getirilir, araya siyah boşluk bırakılır. | büyük | 2 | sahip: çizelge modeli sıralı listeden konumlu modele döner | [6][12] |
| DZB-08 | Ekleme ve üstüne yazma kipleri: seçilen aralığı çizelgeye ya araya sokarak ya da var olanın üstüne yazarak koymak. | Shotcut, Kdenlive, DaVinci Resolve | yarım: giriş ve çıkış işaretiyle aralık silme var (Core/EditTimeline.cs:148) | Yeni çekim, kurguyu elle kaydırmadan istenen yere girer. | orta | 2 | gerekmez | [6][12][26] |
| DZB-09 | Kaynak monitörü ve üç nokta kurgu: kaynağı ayrı izleyip giriş ve çıkış işaretiyle yalnız seçilen bölümü çizelgeye göndermek. | Shotcut, DaVinci Resolve | yok; kaynak bütün olarak çizelgenin sonuna eklenir (App/EditorView.Kaynak.cs:96-97) | Uzun çekimin yalnız işe yarar kısmı çizelgeye girer. | büyük | 3 | gerekmez | [6][26] |
| DZB-10 | Sesi videodan ayırma: klibin sesini bağımsız parça yapıp ayrı kırpmak ya da taşımak. | Shotcut, OpenShot | yarım: klip sesi kapatılabiliyor ve düzeyi ayarlanıyor (Core/ClipEffects.cs:15-35) | Ses görüntüden önce başlar ya da sonra biter (J ve L kesimi). | orta | 2 | sahip: ikinci ses izi kararına bağlı (DZB-01) | [6][9] |
| DZB-11 | Klip kopyala, kes, yapıştır ve çoğalt: seçili klibi panoya alıp başka yere koymak. | Shotcut; ötekilerde doğrulanmadı | yok; `Copy`, `Paste`, `Kopyala`, `Cogalt` aramaları yalnız bağlantı kopyalamayı buldu (App/EditorView.Teslim.cs:314-319) | Aynı bölüm kurguda ikinci kez kullanılır. | küçük | 3 | gerekmez | [6][14] |
| DZB-12 | Klipleri gruplama: birkaç klibi birbirine bağlayıp tek parça gibi taşımak. | Shotcut, Clipchamp | yarım: çoklu silme var (Core/EditTimeline.cs:362), gruplama yok | Birbirine ait klipler taşınırken dağılmaz. | küçük | 2 | gerekmez | [6][21] |
| DZB-13 | Roll kırpma: iki klibin birleştiği noktayı, toplam süre değişmeden sağa sola kaydırmak. | Shotcut, DaVinci Resolve | yok; araçlar Seçim, Jilet ve Kaydırmalı Kırpma (App/EditorTool.cs:3-8) | Kesim anı, sonraki kliplerin yeri bozulmadan inceltilir. | küçük | 3 | gerekmez | [6][7][26] |
| DZB-14 | Slip kırpma: klibin çizelgedeki yerini ve süresini sabit tutup içinde görünen bölümü kaydırmak. | Kdenlive, DaVinci Resolve | yok; araç listesinde yok (App/EditorTool.cs:3-8) | Klibin yeri bozulmadan içinden başka bir an seçilir. | küçük | 3 | gerekmez | [12][26] |
| DZB-15 | Slide kırpma: klibi komşularının arasında kaydırmak; komşular uzayıp kısalır. | DaVinci Resolve | yok; araç listesinde yok (App/EditorTool.cs:3-8) | Klip, toplam süre değişmeden öne ya da arkaya alınır. | küçük | 3 | gerekmez | [8][26] |
| DZB-16 | Çapraz geçiş teslimi: iki klibin ucunu üst üste bindirip görüntüyü ve sesi birbirine eritmek. | Shotcut, OpenShot, Clipchamp, CapCut ve 2 rakip daha | yarım: yalnız klip başına siyaha ve sessizliğe solma (Core/ClipEffects.cs:33-35, Core/ClipFilters.cs:69) | Kesimler sert sıçrama yerine yumuşak geçer. | orta | 1 | sahip: yazılı kapsam geçişsiz, kapsam genişler; teslime ffmpeg xfade ve acrossfade yeter | [1][6][9][19][23][26] |
| DZB-17 | Geçiş önizlemesi: geçişi dışa aktarmadan önce monitörde görmek. | Shotcut, OpenShot, Clipchamp, CapCut ve 2 rakip daha | yok; önizleme mpv edl:// ile, parçalar arası geçiş alanı yok (Core/EdlPreview.cs:17-19) | Geçişin süresi ve görünümü teslim beklenmeden ayarlanır. | büyük | 1 | sahip: önizleme yolu seçilmeli (libmpv lavfi-complex, geçiş bölgesini kısa ön-işleme ya da ayrı bileşim motoru MLT) | [2][3][6][9][19][23] |
| DZB-18 | Geçiş çeşitleri: silme (wipe), kaydırma ve iris gibi hazır geçiş desenleri; DZB-16 üstüne gelir. | Shotcut, OpenShot, CapCut, DaVinci Resolve | yok; `xfade` araması boş | Geçişler tek tipten çıkar. | küçük | 2 | sahip: geçiş kararına bağlı (DZB-16) | [1][6][9][23][26] |
| DZB-19 | Metin biçim denetimleri: yazı tipini, kalın ve italiği, solma süresini panelden seçmek. | Shotcut, OpenShot, Clipchamp, CapCut ve 2 rakip daha | yarım: model destekliyor (Core/TextLayer.cs:43-57), panelde yalnız içerik, boyut ve renk (App/EditorView.axaml:98, :102, :106) | Başlık istenen yazı tipi ve vurguyla yazılır. | küçük | 1 | gerekmez | [6][9][19][23] |
| DZB-20 | Metin konumu: metnin ekrandaki yerini panelden ya da monitörde sürükleyerek belirlemek. | Shotcut, Kdenlive | yarım: model konum tutuyor (Core/TextLayer.cs:92), arayüzde konum denetimi yok (App/EditorView.axaml:95-109) | Metin alt banda, köşeye ya da ortaya konur. | orta | 2 | gerekmez | [6][14] |
| DZB-21 | Metin kenarlığı, gölgesi ve arka plan kutusu: yazıyı her zeminde okunur kılan süslemeler. | Shotcut, OpenShot, CapCut | yarım: kenarlık boyuta göre kendiliğinden hesaplanıyor (Core/AssWriter.cs:108-128), kullanıcı ayarı yok | Açık renkli görüntü üstünde de yazı okunur. | küçük | 2 | gerekmez | [7][11][23] |
| DZB-22 | Hazır başlık şablonları ve alt bant: düzeni hazır başlık, isim bandı ve kapanış yazıları. | OpenShot, Kdenlive, Clipchamp, CapCut ve 2 rakip daha | yok; `Template` araması yalnız arayüz kalıbı buldu | Tasarım bilmeden düzgün görünen başlık konur. | orta | 2 | sahip: hazır içerik paketi dağıtımı | [7][9][16][20][23][26][47] |
| DZB-23 | Metin canlandırmaları: daktilo, kayarak gelme gibi hazır giriş ve çıkış hareketleri. | Shotcut, Clipchamp, CapCut | yarım: yalnız giriş ve çıkış solması (Core/TextLayer.cs:43-57) | Başlık ekrana hareketle girer ve çıkar. | orta | 2 | gerekmez | [7][20][23] |
| DZB-24 | Metin hareketi arayüzü: metnin konumunu zaman içinde değiştiren anahtar kareleri arayüzden vermek. | Shotcut, OpenShot, CapCut, DaVinci Resolve | yarım: model ve .ass yazıcı konum anahtar karesini destekliyor (Core/TextLayer.cs:92, Core/AssWriter.cs:196-197), arayüzde yok (App/EditorView.Metin.cs:191) | Metin ekranda bir noktadan ötekine kayar. | orta | 2 | gerekmez | [6][9][22][26] |
| DZB-25 | Klip ayarlarına anahtar kare: ses düzeyi, kırpma, konum gibi değerleri klip içinde zamanla değiştirmek. | Shotcut, OpenShot, CapCut, DaVinci Resolve ve 5 rakip daha | yok; klip ayarları klip boyunca sabit (Core/ClipEffects.cs:15-35) | Ses yavaşça kısılır, görüntü yavaşça yakınlaşır. | büyük | 2 | gerekmez | [6][9][22][26][41] |
| DZB-26 | Yumuşatma eğrileri: anahtar kareler arasındaki hareketin hızlanıp yavaşlamasını seçmek. | Shotcut, Kdenlive, OpenShot, CapCut | yarım: metin konumu iki nokta arasında yalnız doğrusal (Core/TextLayer.cs:92) | Hareket mekanik değil doğal görünür. | küçük | 3 | gerekmez | [6][15][22][61] |
| DZB-27 | Fotoğrafta yavaş yakınlaşma: durağan görüntüde yavaş kaydırma ve yakınlaştırma (Ken Burns). | DaVinci Resolve | yok; `zoompan`, `KenBurns` aramaları boş | Fotoğraf slaytı canlı görünür. | orta | 3 | gerekmez | [1][26][51] |
| DZB-28 | Bulanıklık ve bölge gizleme: görüntünün tamamını ya da seçilen bölgeyi bulanıklaştırmak. | Shotcut, OpenShot, Kdenlive | yok; `gblur`, `boxblur` aramaları boş | Yüz, plaka ya da parola gizlenir. | orta | 2 | gerekmez | [1][6][9][14][61] |
| DZB-29 | Yeşil perde: tek renkli arka planı saydam yapıp alttaki görüntüyü göstermek. | Shotcut, OpenShot, Clipchamp, CapCut | yok; `chromakey` yalnız kaydedicinin web kamerasında (RecorderArguments.cs:928) | Kişi başka bir arka planın önüne konur. | orta | 2 | sahip: ikinci video izine bağlı (DZB-02) | [1][6][9][19][23] |
| DZB-30 | Hazır görünüm süzgeçleri: tek tıkla siyah beyaz, eski film gibi görünüm vermek. | Clipchamp, CapCut, OpenShot | yok; düzenleyicide renk süzgeci yok (Core/ClipFilters.cs:38) | Renk bilgisi olmadan hızlıca görünüm verilir. | küçük | 2 | gerekmez | [11][19][23] |
| DZB-31 | Stabilizasyon: titrek çekimi yazılımla sabitlemek. | Shotcut, DaVinci Resolve | yok | Elde çekilmiş görüntü sarsıntısız izlenir. | orta | 3 | kapalı: K8 | [6][26][55] |
| DZB-32 | LUT: hazır renk tablosu dosyasıyla görünüm uygulamak. | OpenShot, Shotcut | yok | Kameranın düz çekimi tek dosyayla renklendirilir. | küçük | 3 | kapalı: K8 | [7][11] |
| DZB-33 | Filigran ve logo: görüntünün köşesine sabit resim ya da yazı bindirmek. | OpenShot, Clipchamp | yok; overlay süzgeci yalnız kaydedicide (RecorderArguments.cs:944) | Videoya kanal logosu konur. | orta | 3 | kapalı: K8 | [9][19] |
| DZB-34 | Eklenti desteği: OpenFX görüntü eklentileri ile VST ve LV2 ses eklentilerini yüklemek. | Shotcut | yok | Hazır üçüncü taraf efektler kullanılır. | büyük | 3 | sahip: üçüncü taraf ikili dosya yükleme | [7][15] |
| DZB-35 | Temel renk ayarı: klip başına parlaklık, kontrast, doygunluk ve renk sıcaklığı. | Shotcut, OpenShot, Clipchamp, CapCut ve 2 rakip daha | yok; `eq=` yalnız oynatıcıda (MpvEngine.Advanced.cs:81), düzenleyicide renk ayarı yok | Karanlık ya da soluk çekim düzeltilir. | orta | 1 | gerekmez | [1][6][9][19][23] |
| DZB-36 | Eğriler ve renk tekerleri: gölgeleri, orta tonları ve parlak bölgeleri ayrı ayrı ayarlamak. | Shotcut, OpenShot, Kdenlive, CapCut ve 1 rakip daha | yok; `curves`, `colorbalance` aramaları boş | Renk, temel ayarın yetmediği yerde ince ayarlanır. | orta | 2 | gerekmez | [1][6][11][14][22] |
| DZB-37 | Renk skopları: histogram, dalga biçimi ve vektörskop gibi ölçüm göstergeleri. | Shotcut, Kdenlive, OpenShot | yok | Renk göz kararı değil ölçüyle ayarlanır. | orta | 3 | gerekmez | [6][11][17] |
| DZB-38 | Otomatik renk ve renk eşleme: tek tıkla düzeltmek ya da iki çekimin rengini birbirine benzetmek. | CapCut, Final Cut Pro | yok | Farklı kameraların görüntüsü yan yana uyumlu durur. | orta | 2 | gerekmez | [22][28] |
| DZB-39 | HDR ve 10 bit kurgu: geniş parlaklık aralıklı kaynağı bozmadan önizlemek ve dışa aktarmak. | Shotcut | yok; Tam kip her şeyi 8 bit yuv420p yapıyor (Core/EditExport.cs:334, :366) | Telefonun HDR çekimi kurgudan soluk çıkmaz. | orta | 3 | gerekmez | [6][7][15] |
| DZB-40 | Ses karıştırıcı: izlerin düzeyini ve sağ-sol dengesini tek panelden ayarlamak. | Shotcut, Kdenlive, DaVinci Resolve | yok; yalnız klip başına kazanç (Core/ClipEffects.cs:15-35) | Müzik ve konuşma dengesi tek yerden kurulur. | orta | 3 | sahip: çok iz kararına bağlı (DZB-01) | [6][7][16][26] |
| DZB-41 | Ses düzeyi göstergesi: oynarken tepe düzeyini gösteren çubuk. | Shotcut, DaVinci Resolve | yok; `Meter`, `Gosterge` aramaları boş | Sesin patladığı yer dışa aktarmadan görülür. | küçük | 2 | gerekmez | [6][7][26] |
| DZB-42 | Ses efektleri: ekolayzır, kompresör ve sınırlayıcı. | Shotcut, Kdenlive, OpenShot | yok; ekolayzır yalnız oynatıcıda (src/VidShrink.Player/MpvEngine.Advanced.cs:107) | Boğuk ya da dengesiz ses toparlanır. | orta | 2 | gerekmez | [1][6][17][50][61] |
| DZB-43 | Gürültü azaltma: sabit uğultuyu ve fan sesini bastırmak. | Shotcut, Clipchamp, CapCut, Movavi | yok; `afftdn` yalnız kaydedicide (AudioCaptureArguments.cs:135) | Konuşma arkasındaki uğultu azalır. | küçük | 2 | gerekmez | [1][6][19][24][35] |
| DZB-44 | Modelli konuşma temizleme: küçük bir sinir ağıyla (RNNoise) konuşmayı gürültüden ayırmak. | Shotcut, Clipchamp, Filmora | yok; `arnndn` araması boş | Değişken gürültüde klasik süzgeçten temiz sonuç alınır. | orta | 2 | sahip: model dosyası dağıtımı | [1][6][19][33] |
| DZB-45 | Ses yüksekliğini eşitleme: klipleri aynı algılanan yüksekliğe getirmek. | Shotcut, Filmora | yok; düzenleyicide `loudnorm` yok, hazır süzgeç sabiti yalnız Küçült ve Dönüştür yolunda (src/VidShrink.Core/StreamMapping.cs:357) | Klipten klibe ses sıçraması olmaz. | küçük | 2 | gerekmez | [1][6][33] |
| DZB-46 | Otomatik müzik kısma: konuşma başlayınca müziği kendiliğinden kısmak (ducking). | Shotcut, Filmora | yok; `sidechaincompress`, `Ducking` aramaları boş | Müzik konuşmayı bastırmaz, elle düzey çizmek gerekmez. | orta | 2 | sahip: müzik izine bağlı (DZB-01) | [1][6][33][45] |
| DZB-47 | Seslendirme kaydı: çizelge oynarken mikrofondan anlatım kaydedip ize koymak. | Shotcut, OpenShot | yok; `Voiceover`, `Seslendirme` aramaları boş | Anlatım görüntüye bakarak kaydedilir. | orta | 2 | sahip: ikinci ses izine bağlı (DZB-01) | [6][11] |
| DZB-48 | Hız rampası: klibin içinde hızı kademeli değiştirmek (yavaşla, hızlan). | Shotcut, OpenShot, CapCut, DaVinci Resolve ve 1 rakip daha | yarım: klip başına sabit hız ve ters oynatma (Core/EditTimeline.cs:402, :424) | Hareketli an yavaşlatılıp sonra hızlandırılır. | büyük | 2 | gerekmez | [6][9][23][26][46] |
| DZB-49 | Kare dondurma: seçilen kareyi belli bir süre durağan göstermek. | Shotcut; ötekilerde doğrulanmadı | yok; `FreezeFrame` araması boş | Önemli an durdurulup üstüne yazı konur. | orta | 3 | gerekmez | [1][6][7][48] |
| DZB-50 | Akıcı ağır çekim: ara kare üreterek yavaşlatılmış görüntüyü pürüzsüz yapmak. | CapCut, DaVinci Resolve | yok; `minterpolate` araması boş | Normal hızda çekilmiş görüntü takılmadan yavaşlar. | orta | 2 | gerekmez | [1][24][27] |
| DZB-51 | Vekil dosyalarla kurgu: ağır kaynağın küçük bir kopyasıyla akıcı çalışıp teslimde aslını kullanmak (proxy). | Shotcut, Kdenlive, OpenShot, CapCut ve 1 rakip daha | yok; `Proxy` yalnız ProRes kodek adında (IntermediateCodecs.cs:33) | 4K ve uzun kayıtlar zayıf bilgisayarda da takılmadan kurgulanır. | büyük | 2 | sahip: diskte ek kopya üretir; kendiliğinden mi istekle mi | [6][11][18][24][28][49] |
| DZB-52 | Proje şablonları: hazır kurgu iskeletini kendi klibinle doldurmak. | Clipchamp, CapCut | yok | Sıfırdan kurmadan hazır düzenle video çıkar. | büyük | 2 | sahip: şablon içeriği üretimi ve dağıtımı | [15][19][21][22] |
| DZB-53 | Hazır varlık kitaplığı: telifsiz müzik, ses efekti, çıkartma ve grafik. | Shotcut, Clipchamp, CapCut | yok | Müzik ve görsel aramak için uygulamadan çıkılmaz. | orta | 2 | sahip: varlık lisansı ve paket boyutu | [7][19][23] |
| DZB-54 | Dışa aktarım ayarları: çözünürlük, kare hızı, kodek ve kaliteyi teslimde seçmek. | OpenShot, CapCut, Shotcut, Clipchamp ve 1 rakip daha | yarım: Tam kip sabit libx264 CRF 18 ve AAC 192k (Core/EditExport.cs:68-70), kap yalnız MP4 ve MKV (App/EditorView.Teslim.cs:392) | Çıktı, ayrıca Küçült ya da Dönüştür sekmesine götürülmeden istenen boyut ve kodekle alınır. | orta | 1 | gerekmez | [6][10][13][23][66] |
| DZB-55 | Dışa aktarım ön ayarları: YouTube, dikey video gibi hazır hedefler ve kullanıcının kendi ön ayarı. | Shotcut, Kdenlive, OpenShot | yarım: ön ayar yalnız Küçült sekmesinde (MainWindow.OnAyar.cs:401) | Doğru ayar tek seçimle gelir. | küçük | 2 | gerekmez | [6][17][61] |
| DZB-56 | Proje oranı ve tuval: 9:16, 1:1, 16:9 gibi çıktı çerçevesini proje düzeyinde seçmek. | Kdenlive, DaVinci Resolve, OpenShot | yarım: yalnız klip başına oran kırpması (Core/ClipEffects.cs:96) | Aynı kurgudan dikey ve yatay sürüm çıkar. | orta | 2 | gerekmez | [10][15][27] |
| DZB-57 | Kare dışa aktarma: oynatma başındaki kareyi resim olarak kaydetmek. | Shotcut | yarım: kare kaydetme oynatıcı görünümünde var (src/VidShrink.App/Playback/Keymap.cs:73, src/VidShrink.App/Playback/PlayerView.Window.cs:146-147), düzenleyici komut listesinde yok (App/EditorKeymap.cs:9-47) ve monitörün klavyesi kapalı (App/EditorView.axaml.cs:42) | Kapak görseli kurgudan çıkarılır. | küçük | 3 | gerekmez | [6] |
| DZB-58 | Yalnız ses dışa aktarma: kurgunun sesini ayrı dosya olarak almak. | Clipchamp | yok; çıktı yalnız MP4 ve MKV (App/EditorView.Teslim.cs:392) | Podcast ya da ses kaydı kurgudan doğrudan çıkar. | küçük | 3 | gerekmez | [67] |
| DZB-59 | Proje alışverişi: kurguyu EDL, Final Cut XML ya da OpenTimelineIO dosyasıyla başka programa taşımak. | OpenShot, Kdenlive, Filmora | yok; proje yalnız .vsproj.json (Core/EditProject.cs:39-43) | Kaba kurgu burada yapılıp başka programda bitirilir. | orta | 3 | gerekmez | [8][11][15][33] |
| DZB-60 | Çoklu kamera kurgusu: aynı anı çeken birkaç kaydı eşleyip oynarken açı seçmek. | Kdenlive, DaVinci Resolve, Final Cut Pro, Filmora ve 3 rakip daha | yok; `Multicam` araması boş | Röportaj ve konser çekimi tek geçişte kurgulanır. | büyük | 3 | sahip: çok iz ve bileşim kararına bağlı (DZB-02) | [12][26][28][33][58] |
| DZB-61 | Sese göre eşleme: ayrı kaydedilmiş sesi ve görüntüyü dalga biçiminden hizalamak. | Camtasia, CapCut | yok | Harici mikrofon kaydı görüntüye elle kaydırmadan oturur. | orta | 3 | gerekmez | [39][58] |
| DZB-62 | Altyazı içe ve dışa aktarma: SRT, VTT ya da ASS dosyasını metin izine almak ve geri yazmak. | Shotcut, Kdenlive, OpenShot, Camtasia | yarım: T1 metin izi ve .ass yazıcı var (Core/AssWriter.cs:113-126), dosyadan içe aktarma yok | Hazır altyazı elle yeniden yazılmadan kurguya girer. | orta | 2 | gerekmez | [6][17][39][61] |
| DZB-63 | Otomatik altyazı: konuşmayı yazıya çevirip altyazı üretmek. | Shotcut, Kdenlive, Clipchamp, CapCut ve 5 rakip daha | yok; `Whisper`, `Transcript` aramaları boş | Altyazı elle yazılmaz. | büyük | 3 | kapalı: K5 | [6][7][28][37][57][60] |
| DZB-64 | Medya havuzu: projeye alınan dosyaların listesi; oradan çizelgeye sürüklemek. | Shotcut, Kdenlive, DaVinci Resolve, OpenShot ve 2 rakip daha | yok; kaynak doğrudan çizelgenin sonuna eklenir (App/EditorView.Kaynak.cs:96-97) | Dosyalar önce toplanır, sonra istenen sırayla kullanılır. | orta | 2 | gerekmez | [6][13][26][61] |
| DZB-65 | Havuzda klasör ve arama: dosyaları kutulara ayırmak ve adla bulmak; DZB-64 üstüne gelir. | Shotcut, Kdenlive | yok; `MediaBin`, `MediaPool` aramaları boş | Çok dosyalı projede aranan klip hemen bulunur. | küçük | 3 | gerekmez | [7][17] |
| DZB-66 | İç içe dizi: bir kurguyu başka bir kurgunun içinde tek klip gibi kullanmak. | Kdenlive | yok; `Nested` araması boş | Uzun proje bölümlere ayrılıp sonra birleştirilir. | büyük | 3 | gerekmez | [8][14][15] |
| DZB-67 | Ayar klibi: altındaki bütün kliplere aynı efekti uygulayan boş klip. | DaVinci Resolve, Final Cut Pro, Shotcut | yok | Renk ya da efekt tek yerden bütün kurguya verilir. | orta | 3 | sahip: çok ize bağlı (DZB-02) | [7][26][28] |
| DZB-68 | Metin üzerinden kurgu: konuşmanın dökümünde sözcük silince videonun da kesilmesi. | Descript, Clipchamp, VEGAS, CapCut ve 3 rakip daha | yok | Konuşma videosu yazı düzeltir gibi kurgulanır. | büyük | 3 | kapalı: K5 | [21][26][37][38][59] |
| DZB-69 | Otomatik yeniden kadraj: yatay videoyu, özneyi kadrajda tutarak dikeye çevirmek. | CapCut, DaVinci Resolve, Movavi | yarım: elle oran kırpması var (Core/ClipEffects.cs:96), özne takibi yok | Yatay çekimden dikey paylaşım videosu çıkar. | büyük | 2 | sahip: özne algılama modeli indirme | [22][27][35] |
| DZB-70 | Arka plan silme ve nesne maskesi: yeşil perde olmadan kişiyi ya da nesneyi arka plandan ayırmak. | CapCut, OpenShot, Kdenlive, Premiere Pro ve 3 rakip daha | yok; `Segmentation`, `rembg` aramaları boş | Kişi, özel çekim gerekmeden arka plandan ayrılır. | büyük | 2 | sahip: model indirme ve çalışma zamanı | [11][16][23][30] |
| DZB-71 | Hareket izleme: metni ya da bulanıklığı hareket eden nesneye yapıştırmak. | Filmora, Movavi, OpenShot | yok | Yazı kişiyi izler, hareket eden yüz gizlenir. | büyük | 2 | sahip: izleme algoritması ya da model bağımlılığı | [11][33][35] |
| DZB-72 | Metinden sese: yazılan metni yapay sesle seslendirip ize koymak. | Shotcut, Clipchamp, VEGAS, DaVinci Resolve | yok | Mikrofon olmadan anlatım eklenir. | orta | 2 | sahip: ses modeli indirme ya da bulut hizmeti | [6][7][19][27][37] |
| DZB-73 | Üretken yapay zekâ: klibi uzatmak, metinden görüntü ya da video üretmek. | Premiere Pro, Filmora, CapCut, VSDC ve 2 rakip daha | yok | Eksik kalan birkaç saniye ya da ara görüntü üretilir. | büyük | 3 | sahip: bulut hizmeti, hesap ve kredi ister | [22][29][33][34] |
| DZB-74 | Sohbetle kurgu yardımcısı: yazıyla komut verip kurguyu programa yaptırmak. | Descript, PowerDirector, Filmora, DaVinci Resolve | yok | Kurgu bilmeyen kullanıcı isteğini yazarak yaptırır. | büyük | 3 | sahip: bulut modeli ve hesap ister | [33][36][38][62] |
| DZB-75 | Akıllı arama: görüntüde ne olduğunu ya da ne söylendiğini yazarak klip bulmak. | Premiere Pro, Final Cut Pro, DaVinci Resolve | yok | Saatlerce çekimin içinden aranan an bulunur. | büyük | 3 | sahip: model indirme; medya havuzuna bağlı (DZB-64); söze göre arama yarısı K5'e denk gelir | [27][28][29] |
| DZB-76 | Vuruşa göre kesim: müziğin vuruşlarını bulup çizelgede işaretlemek. | Final Cut Pro | yok | Kesimler müziğin ritmine oturur. | orta | 3 | sahip: müzik izine bağlı (DZB-01) | [28] |
| DZB-77 | Otomatik kısa klip: uzun videodan öne çıkan anları seçip kısa videolar çıkarmak. | Filmora | yok | Uzun yayından paylaşımlık parçalar hazır gelir. | büyük | 3 | sahip: model ya da bulut hizmeti | [33] |

## Düzeltmeler

| No | Ne Değişti | Kanıt |
|---|---|---|
| `DZB-02` | Fayda hücresinden "üstte logo" çıkarıldı. Logo ve filigran K8 ile rafta; o iş DZB-33'te kapalı duruyor, bu satırın faydası sayılamaz. | ORTAK.md karar listesi (K8); docs/YOL-HARITASI.md:150-167 |
| `DZB-09` | Öncelik 2 → 3. Dar kitle: kaynak monitörü ve üç nokta kurgu yalnız profesyonel akışta kullanılır, iş de büyük. | Kimde Var hücresi yalnız Shotcut ve DaVinci Resolve; ORTAK.md öncelik ölçütü |
| `DZB-11` | Öncelik 2 → 3. Tek rakipte belgeli; ölçüte göre 2 olamaz. | Kimde Var hücresi: "Shotcut; ötekilerde doğrulanmadı" |
| `DZB-13` | Öncelik 2 → 3. Dar kitle: roll kırpma bir kurgucu tekniği; belgelendiği iki program da profesyonel kurguya dönük. | Kimde Var hücresi yalnız Shotcut ve DaVinci Resolve |
| `DZB-14` | Öncelik 2 → 3. Dar kitle: slip kırpma için aynı gerekçe. | Kimde Var hücresi yalnız Kdenlive ve DaVinci Resolve |
| `DZB-18` | Karar `gerekmez` → `sahip: geçiş kararına bağlı (DZB-16)`. Satırın kendisi "DZB-16 üstüne gelir" diyor; DZB-16 sahibin kapsam kararını bekliyor, ona bağlı iş kararsız yapılamaz. | Tablodaki DZB-16 Karar hücresi; `xfade` kodda yok |
| `DZB-24` | Kanıt satırı düzeltildi: `Core/AssWriter.cs:240-253` solma etiketini yazıyor, konum ve hareket etiketi `:196-197` satırlarında. | src/VidShrink.Core/Editing/AssWriter.cs:196-197 (`\pos`, `\move`), :240-253 (`\fad`, `\fade`) |
| `DZB-40` | Öncelik 2 → 3. Dar kitle: ayrı karıştırıcı paneli profesyonel akış; müzik ile konuşma dengesi iz başına düzeyle DZB-01'de zaten gelir. | Kimde Var hücresi yalnız Shotcut, Kdenlive ve DaVinci Resolve |
| `DZB-42` | Kanıta dosya yolu ve satır eklendi. | src/VidShrink.Player/MpvEngine.Advanced.cs:107 (`equalizer=f=`) |
| `DZB-45` | Bizde Durum'a en yakın kod eklendi: süzgeç sabiti hazır ama yalnız Küçült ve Dönüştür yolunda kullanılıyor. | src/VidShrink.Core/StreamMapping.cs:357, :534 |
| `DZB-49` | Öncelik 2 → 3. Tek rakipte belgeli; ölçüte göre 2 olamaz. | Kimde Var hücresi: "Shotcut; ötekilerde doğrulanmadı" |
| `DZB-57` | Bizde Durum `yok` → `yarım`. Kare kaydetme oynatıcı görünümünde var; düzenleyicinin komut listesinde yok ve monitörün klavyesi kapalı; kod okumasına göre oynatıcının kısayolu orada işlemez (koşulmadı). | src/VidShrink.App/Playback/Keymap.cs:73; src/VidShrink.App/Playback/PlayerView.Window.cs:146-147; src/VidShrink.App/Editing/EditorKeymap.cs:9-47; src/VidShrink.App/Editing/EditorView.axaml.cs:42 |
| `DZB-75` | Karar hücresine not eklendi: "ne söylendiğini" arama yarısı konuşmayı yazıya dökmeyi ister, o da K5 ile kapalı. Öncelik zaten 3. | ORTAK.md karar listesi (K5) |

**Öncelik 1 Satırlarının Kaynak Doğrulaması.** Yedi satırın yedisi de iki bağımsız kaynakta görüldü; hiçbirinin önceliği düşmedi. Açılan sayfalar:

- (a) https://shotcut.org/features/
- (b) https://www.openshot.org/features/
- (c) https://clipchamp.com/en/video-editor/
- (d) https://www.capcut.com/resource/how-to-use-capcut
- (e) https://www.blackmagicdesign.com/products/davinciresolve/edit
- (f) https://support.microsoft.com/en-us/Clipchamp/how-to-add-transitions-and-fades-to-videos
- (g) https://www.openshot.org/blog/2009/06/19/transitions-transitions-transitions/
- (h) https://docs.kdenlive.org/en/titles_and_graphics/titles/title_text.html
- (i) https://www.openshot.org/static/files/user-guide/export.html

| No | Sayfada Görülen | Sayfa |
|---|---|---|
| `DZB-01` | Shotcut bütün izlerde ses karıştırıyor; OpenShot sınırsız iz sunuyor; Clipchamp ve CapCut videoya fon müziği eklemeyi anlatıyor. | (a), (b), (c), (d) |
| `DZB-02` | Shotcut video izleri arasında bileşim yapıyor; OpenShot iz tabanlı bileşim ve resim bindirme sunuyor; DaVinci Resolve resim içinde resim efektini anlatıyor; CapCut klibi üstteki ize koymayı anlatıyor. | (a), (b), (e), (d) |
| `DZB-16` | Shotcut aynı izde klipleri bindirerek ses ve görüntü geçişi kuruyor; OpenShot 400'den, DaVinci Resolve 100'den çok geçiş sayıyor; Clipchamp'te varsayılan geçiş çapraz solma. | (a), (b), (e), (f) |
| `DZB-17` | Clipchamp'te geçiş seçeneğinin üstüne gelince önizleme oynuyor; OpenShot'ın resmi günlüğü geçişlerin gerçek zamanlı önizlendiğini yazıyor. | (f), (g) |
| `DZB-19` | OpenShot başlıkta metni, yazı tipini ve rengi değiştiriyor; Kdenlive'ın başlık aracında yazı tipi, ağırlık, italik, kenarlık ve gölge var; Shotcut'ta zengin ve yalın metin süzgeci var. | (b), (h), (a) |
| `DZB-35` | OpenShot parlaklık, kontrast ve renk ayarı efektlerini sayıyor; CapCut parlaklık, kontrast ve doygunluk ayarını anlatıyor; Shotcut renk tekerleriyle düzeltme sunuyor; Clipchamp renk düzeltmeyi sayıyor. | (b), (d), (a), (c) |
| `DZB-54` | OpenShot dışa aktarımda genişlik, yükseklik, kare hızı, kodek ve kalite seçtiriyor; CapCut kare hızı, çözünürlük, kap, kodek ve bit hızı seçtiriyor; Shotcut dışa aktarım ön ayarları sunuyor. | (i), (d), (a) |

## Çıkarılanlar

| No | Eksik | Neden | Kanıt (dosya:satır) |
|---|---|---|---|

Çıkarılan satır yok. "Yok" ya da "yarım" denen hiçbir özellik kodda tam olarak bulunmadı; tablo 77 satırla kaldı.

## Yinelenenler

- `DZA-39 = DZB-57` — düzenleyiciden kare kaydetme. İki tablo da aynı kodu gösteriyor (oynatıcıdaki kare kaydetme).
- `DZA-17 = DZB-59` — kurguyu başka kurgu programının açtığı dosyayla (XML) dışarı verme. DZA-15'in EDL dışa aktarması da DZB-59'un EDL yarısıyla örtüşüyor.
- `DZA-42 = DZB-68` — konuşmanın yazıya dökülmüş metni üstünden kesim. İkisi de `kapalı: K5`.

Yakın ama ayrı işler, yinelenen sayılmadı: DZA-25 ile DZB-01 (biri sesi yeniden kodlamadan dosyaya ekler, öteki iki sesi karıştırır), DZA-26 ile DZB-58 (biri izi olduğu gibi çıkarır, öteki kurgunun sesini teslim eder), DZA-36 ile DZB-39 (biri Akıllı kipte HDR üst verisi, öteki Tam kipte 10 bit).

## Doğrulanamayanlar

- `DZB-01` — Karar hücresi "önizlemeye libmpv ek ses dosyası yeter" diyor. Bildiğim kadarıyla mpv aynı anda tek ses izi çalar; klibin sesiyle müziği birlikte duymak için `lavfi-complex` (mpv'nin ffmpeg süzgeç grafiğini önizlemede çalıştıran seçeneği) ile karıştırma gerekir. Bunun `edl://` (mpv'nin parçaları art arda oynatan liste biçimi) ile birlikte çalışıp çalışmadığı ölçülmedi; "orta" tahmini buna bağlı.
- `DZB-04` — Resim dosyası bugün kaynak olarak verilince ne olduğu ölçülemedi (program çalıştırmak yasak). Satırın kendi notu da "resimle denenmedi" diyor; Bizde Durum `yok` olarak kaldı.
- `DZB-17` — Shotcut'ın özellik sayfası ve OpenShot'ın geçiş kılavuzu canlı önizlemeyi açıkça yazmıyor. Satır iki başka kaynakla doğrulandı; Kimde Var hücresindeki "ve 2 rakip daha" sayısı ölçülmedi.
- `DZB-38` — İki çekimin rengini birbirine eşleyen hazır bir ffmpeg süzgeci bilmiyorum; "orta" tahmini yalnız tek tıkla düzeltme yarısı için geçerli olabilir.
- `DZB-41` — Oynarken ses düzeyini mpv'den okuyan bir yol kodda yok; "küçük" tahmini ölçülmedi.
- `DZB-57` — Düzenleyici monitöründe sağ tıkın oynatıcının menüsünü açıp açmadığı ölçülemedi. Fare yolu klavye kapısından geçmiyor (src/VidShrink.App/Playback/PlayerView.axaml.cs:385-401) ve menüde kare kaydetme satırı var (src/VidShrink.App/Playback/PlayerView.Menu.cs:29); açılıyorsa özellik gizli de olsa erişilebilir demektir.
- Öncelik 2 satırlarının rakip sayıları — Bu turda öncelik 2 satırları için web'e çıkılmadı; Kimde Var hücreleri araştırma kolunun yazdığı gibi kaldı.

## Ek Notlar

- Açılan on sayfada (Düzeltmeler'deki dokuzu ve OpenShot'ın geçiş kılavuzu) ve okunan dosyalarda bana yönelik yazılmış yönerge görülmedi.
- "Üçü yarım, altısı yok" hükmü kodda tutuyor: geçiş, metin, altyazı, müzik izi, bindirme, renk ayarı, dışa aktarım ayarı ve medya paneli üçer adla arandı; `src/VidShrink.App/Locales/tr/editor.json` içinde bunlara ait anahtar yok.
- Adı yanıltan iki test dosyası var: `DuzenleyiciGecisTests.cs` sekmeler arası sayfa geçişini, `DuzenleyiciSesIziTests.cs` A1 ses satırının çizimini ve fare davranışını ölçüyor; kurgu geçişi ya da ikinci ses izi değiller.
- 46 öncelik 2 satırından 6'sı 3'e indi (DZB-09, DZB-11, DZB-13, DZB-14, DZB-40, DZB-49). Dar kitle ölçüsü şuydu: yalnız profesyonel kurgu programlarında belgeli ve ekrana yeni bir sonuç getirmeyen akış inceliği. Kalan 40 satır en az iki rakipte belgeli; ölçüt daha çok indirime dayanak vermiyor.
- Aynı ölçüye yakın düşüp 2'de bırakılan dört satır: DZB-06 ve DZB-07 (çok iz kararının yapı taşı), DZB-08 (üç rakipte var; klibi istenen yere koymak çok kaynaklı kurgunun gündelik işi), DZB-41 (küçük iş, kurgu bilgisi istemeyen gösterge). Sahip bunları da dar kitle sayarsa öncelik 2 sayısı 36'ya iner.
- Öncelik 3 satırları için toplu kod araması yapıldı (eklenti, renk skopu, çoklu kamera, proje alışverişi, iç içe dizi, ayar klibi, vuruş bulma, kırpma araçları ve havuz adları): `src/` altında eşleşme yok.
- K8'e ya da K5'e tam denk gelip `kapalı` yazılmamış satır yok. İki kısmi örtüşme düzeltildi (DZB-02, DZB-75). DZB-30 LUT dosyasıyla değil süzgeç ön ayarıyla yapılmalı, yoksa K8'e girer.
- DZB-57, K9'a (ekran görüntüsü aracı) denk gelmiyor: K9 masaüstünü yakalayan araçtır, bu satır videonun karesini kaydetmek ve aynı iş oynatıcıda zaten var.
- `.claude/acik.md` defterinde kurgu eksiklerinden hiçbiri geçmiyor; hiçbir satıra `defterde` eklenmedi.
- Tabloda olmayan eksik: yazı tipi listesi yalnız Windows'ta doluyor (src/VidShrink.Core/Editing/TextFonts.cs:47, :78). DZB-19 yazı tipi seçicisi macOS ve Linux'ta boş kalır; o iki platform için ayrı keşif yolu gerekir.
- DZB-22, kodda tanımlı birkaç metin ön ayarıyla sınırlanırsa içerik dağıtımı gerektirmez; sahip öyle derse DZB-P1'e katılır.
- On altı paket sınırı yüzünden altı paket (DZB-P9, DZB-P10, DZB-P13, DZB-P14, DZB-P15, DZB-P16) bölünebilir halde bırakıldı; bölme yeri her birinin planında yazılı.
- En ağır eksikleri üç kapsam kararı tutuyor (DZB-P6, DZB-P7, DZB-P8; DZB-P12 bunlara bağlı). Kararlar ilk gün sorulursa karar beklemeyen ilk beş paket yapılırken yanıt gelmiş olur.

## Sekme Özeti

- Kesme ve birleştirme tarafı sağlam: çok kaynak, üç teslim kipi, klip başına kırpma, hız, solma ve bir metin izi var. Kurgu programlarının ortak asgari kümesindeki 11 kalemin 2'si tam, 3'ü yarım, 6'sı yok.
- Tablo 77 satırla kaldı: 7'si öncelik 1, 40'ı öncelik 2, 30'u öncelik 3. Beş satır önceki kararla kapalı (K8 ve K5).
- En ağır üç eksik: ikinci video izi (DZB-02), geçişin canlı önizlemesi (DZB-17), müzik izi (DZB-01). Üçü de "tek şerit" kapsamından çıkma kararı ister.
- En ucuz üç kazanç: metin biçim denetimleri (DZB-19; model hazır, yalnız panel eksik), dışa aktarım ayarları (DZB-54; sabitler seçeneğe döner), temel renk ayarı (DZB-35; tek süzgeç, önizleme yolu hazır).
- Paketlenen 51 satırın 20'si sahibin kararını bekliyor: 11'i çok iz ve geçiş kapsamına, 9'u disk yükü, hazır içerik ve modele bağlı.
- Öneri: önce karar beklemeyen ilk beş paket (DZB-P1 ile DZB-P5) yapılsın, aynı gün üç kapsam kararı sorulsun.

## Paket Önerisi

### DZB-P1 — Metin Biçim Ve Süsleme
- Satırlar: DZB-19, DZB-21
- İş: küçük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Editing/EditorView.Metin.cs, src/VidShrink.Core/Editing/TextLayer.cs, src/VidShrink.Core/Editing/AssWriter.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciMetinTests.cs
- Plan: Metin paneline yazı tipi, kalın, italik ve solma süresi denetimleri eklenir; model bu alanları zaten tutuyor (TextLayer.cs:43-57), yalnız arayüz bağlanır. Kenarlık kalınlığı, gölge ve arka plan kutusu için modele üç alan eklenir ve AssWriter'ın stil satırına yazılır (ASS, metnin biçimini de taşıyan altyazı dosyasıdır). Önizleme ve teslim aynı .ass dosyasını okuduğu için ikinci bir yol gerekmez. Doğrulama: DuzenleyiciMetinTests'e stil satırı ölçüsü ve aynı andaki önizleme karesi ile teslim karesinin karşılaştırması.

### DZB-P2 — Dışa Aktarım Ayarları Ve Ön Ayarlar
- Satırlar: DZB-54, DZB-55, DZB-58
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.App/Editing/EditorView.Teslim.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/MainWindow.OnAyar.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Tam kipin sabitleri (EditExport.cs:68-70, :366-367) seçeneğe döner: çözünürlük, kare hızı, kodek, kalite ve ses bit hızı; bugünkü değerler varsayılan kalır, böylece davranış değişmez. Üstüne birkaç hazır hedef (1080p, 720p, dikey) ve kullanıcının kendi ön ayarı gelir; Küçült sekmesinin ön ayar deposu yeniden kullanılır. Yalnız ses çıktısı aynı pencerede tek seçenektir (görüntüsüz çıktı ve ses kabı). Doğrulama: argüman testleri ve çıktı dosyasında ölçülen çözünürlük, kare hızı ve kodek.

### DZB-P3 — Temel Renk Ve Hazır Görünüm
- Satırlar: DZB-35, DZB-30
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciKlipOzellikTests.cs
- Plan: Klip ayarlarına parlaklık, kontrast, doygunluk ve renk sıcaklığı eklenir; teslimde ffmpeg `eq` ve `colortemperature` süzgeçleriyle, önizlemede klip ayarlarının bugün geçtiği EditLook zinciriyle uygulanır. Hazır görünümler (siyah beyaz, eski film) aynı alanların kayıtlı değerleridir; LUT dosyası kullanılmaz, o yol K8 ile rafta. İlk iş bu süzgeçlerin zaman aralığına bağlanabildiğini ölçmektir, çünkü önizleme bütün çizelgeye tek zincir kuruyor. Doğrulama: DuzenleyiciKlipOzellikTests'e argüman ölçüsü ve bir kare karşılaştırması.

### DZB-P4 — Klip Gruplama Ve Pano
- Satırlar: DZB-12, DZB-11
- İş: küçük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorKeymap.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: Kliplere grup kimliği eklenir; taşıma ve silme grubun bütününe uygulanır (çoklu silme hazır: EditTimeline.cs:362). Kopyala, kes, yapıştır ve çoğalt seçili klipleri bir tampona alıp oynatma başına koyar; komutlar EditorKeymap'e ve çizelgenin sağ tık menüsüne girer. Doğrulama: EditTimeline birim testleri ve her işlemin tek adımda geri alınması.

### DZB-P5 — Ses Temizleme Ve Düzey
- Satırlar: DZB-43, DZB-45, DZB-42, DZB-41
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/StreamMapping.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.Player/MpvEngine.cs, src/VidShrink.App/Locales/
- Plan: Klip paneline gürültü azaltma (`afftdn`), yükseklik eşitleme (`loudnorm`; sabiti StreamMapping.cs:357'de hazır), ekolayzır, kompresör ve sınırlayıcı (`equalizer`, `acompressor`, `alimiter`) eklenir ve ClipFilters'ın ses zincirine (ClipFilters.cs:79) bağlanır. Düzey göstergesi ayrı parçadır: oynarken tepe düzeyi mpv'den okunur; okuma yolu kodda yok, önce o ölçülür. Doğrulama: argüman testleri ve teslim edilen dosyada ölçülen yükseklik değeri.

### DZB-P6 — Çapraz Geçiş Teslimi
- Satırlar: DZB-16, DZB-18
- İş: orta
- Karar: sahip: Düzenleyicinin yazılı kapsamı "geçişsiz"den çıkıp iki klip arasında çapraz geçişi kapsasın mı?
- Dokunacağı yer: src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/
- Plan: İki klibin sınırına geçiş süresi ve türü yazılır; Tam kipte görüntü ffmpeg `xfade`, ses `acrossfade` süzgeciyle birleştirilir. Tür listesi `xfade`'in hazır desenlerinden gelir (erime, silme, kaydırma, iris), ek içerik gerekmez. Geçişli kurgu Hızlı ve Akıllı kipte teslim edilemez; bugün efektlerde olduğu gibi Tam kipe düşer. Bu pakette canlı önizleme yok (DZB-P8); doğrulama argüman testi ve toplam sürenin geçiş süresi kadar kısaldığının ölçümü.

### DZB-P7 — Müzik İzi
- Satırlar: DZB-01
- İş: orta
- Karar: sahip: Düzenleyici tek şeritten çıkıp videonun altına ikinci bir ses izi (müzik, anlatım) alsın mı?
- Dokunacağı yer: src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EdlPreview.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorView.Kaynak.cs, src/VidShrink.Player/MpvEngine.cs, tests/VidShrink.Tests/
- Plan: Çizelgeye A2 izi eklenir; görüntüsü olmayan dosya artık reddedilmez (EditorView.Kaynak.cs:89-93), bu ize başlangıç anı ve düzeyiyle konur. Teslimde iki ses ffmpeg `amix` süzgeciyle karıştırılır. Önizlemede iki sesi birlikte duymanın yolu ölçülmedi: mpv tek ses izi çalar, karıştırma `lavfi-complex` ister ve bunun `edl://` ile çalışması ilk iş denenir. Doğrulama: teslim dosyasında iki sesin de duyulduğunu gösteren düzey ölçümü ve proje dosyasını yazıp geri okuma testi.

### DZB-P8 — Geçiş Önizleme Yolu
- Satırlar: DZB-17
- İş: büyük
- Karar: sahip: Geçişin ve bindirmenin canlı önizlemesi hangi yoldan yapılsın: libmpv süzgeç grafiği, geçiş bölgesini kısa ön-işleme, yoksa ayrı bir bileşim motoru (MLT)?
- Dokunacağı yer: src/VidShrink.Core/Editing/EdlPreview.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.Player/MpvEngine.cs, tools/VidShrink.Bench
- Plan: Önerilen yol kısa ön-işleme: iki klibin bindiği birkaç saniye arka planda küçük bir ara dosyaya işlenir ve önizleme listesine ayrı parça olarak girer; var olan `edl://` önizlemesi bozulmaz, yeni bağımlılık gelmez. Karardan önce iki ölçüm yapılır: 1080p'de bir saniyelik geçişin hazırlanma süresi ve `lavfi-complex` ile `edl://` birlikteliği. MLT (Shotcut ve Kdenlive'ın kullandığı açık kaynak bileşim motoru) yeni ikili dosya dağıtımı demektir, o yüzden son seçenek. Doğrulama: aynı andaki önizleme karesi ile teslim karesinin karşılaştırması.

### DZB-P9 — Metin Konumu, Hareketi Ve Altyazı
- Satırlar: DZB-20, DZB-24, DZB-23, DZB-62
- İş: büyük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Editing/EditorView.Metin.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.Core/Editing/TextLayer.cs, src/VidShrink.Core/Editing/AssWriter.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciMetinTests.cs
- Plan: Metnin yeri panelden ve monitörde sürükleyerek verilir, hazır giriş ve çıkış hareketleri (kayarak gelme, daktilo) eklenir; model konumu ve anahtar kareyi (bir değerin zaman içinde değiştiği işaret noktası) zaten tutuyor (TextLayer.cs:92, AssWriter.cs:196-197), eksik olan arayüz. Altyazı dosyası (SRT, VTT, ASS) metin izine alınır ve geri yazılır; ffmpeg bu biçimleri .ass'e çevirebildiği için tek ayrıştırıcı yeter. Paket ikiye bölünebilir: konum ve hareket (DZB-20, DZB-24, DZB-23) ile altyazı alışverişi (DZB-62). Doğrulama: DuzenleyiciMetinTests'e konum ve hareket ölçüleri, altyazıda içe alıp geri yazma testi.

### DZB-P10 — Resim Klibi, Tuval Ve Dönüşüm
- Satırlar: DZB-04, DZB-05, DZB-03, DZB-56, DZB-49
- İş: büyük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Editing/EditorView.Kaynak.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, tests/VidShrink.Tests/
- Plan: Resim ve düz renk, eklenirken kısa bir ara videoya çevrilir ve bildik kaynak gibi çizelgeye girer; önizleme yolu değişmez. Kare dondurma aynı yoldan gelir: oynatma başındaki kare resme çıkarılıp klip yapılır. Klibe serbest ölçek, konum ve açı, projeye de çıktı çerçevesi (9:16, 1:1, 16:9) eklenir; ikisi de ClipFilters'ın geometri zincirinden (ClipFilters.cs:38) geçer. Paket ikiye bölünebilir: resim, renk ve dondurma (DZB-04, DZB-05, DZB-49) ile dönüşüm ve tuval (DZB-03, DZB-56); doğrulama argüman testleri ve kare karşılaştırması.

### DZB-P11 — Medya Havuzu Ve Araya Ekleme
- Satırlar: DZB-64, DZB-08
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Editing/EditorView.Kaynak.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciCokKaynakTests.cs
- Plan: Projeye alınan kaynaklar bir listede görünür (proje dosyası ek kaynakları zaten tutuyor: EditProject.cs:68); listeden çizelgeye sürüklenen klip bırakıldığı yere girer. Araya ekleme sonraki klipleri iter, üstüne yazma aynı süreyi silip yerine koyar (aralık silme hazır: EditTimeline.cs:148). Dosya ekleyince çizelgenin sonuna gitme davranışı varsayılan kalır, o yüzden karar gerekmez. Doğrulama: DuzenleyiciCokKaynakTests'e iki kipin süre ve sıra ölçüleri.

### DZB-P12 — İkinci Video İzi Ve Yeşil Perde
- Satırlar: DZB-02, DZB-29
- İş: büyük
- Karar: sahip: Düzenleyici tek şeritten çıkıp üstte ikinci bir video izi (resim içinde resim) alsın mı?
- Dokunacağı yer: src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EdlPreview.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, tests/VidShrink.Tests/
- Plan: Çizelgeye V2 izi eklenir; üstteki klip başlangıç anı, boyutu ve konumuyla tutulur, teslimde ffmpeg `overlay` süzgeciyle bindirilir. Yeşil perde aynı klibe tek seçenektir: bindirmeden önce `chromakey` uygulanır (kaydedicideki hazır sabit: src/VidShrink.Core/RecorderArguments.cs:928). Canlı önizleme DZB-P8'de seçilen yoldan gelir; o karar verilmeden bu paket açılmaz. Doğrulama: argüman testi ve bindirilen bölgenin teslim karesinde ölçülmesi.

### DZB-P13 — İleri Renk Ve Bulanıklık
- Satırlar: DZB-36, DZB-38, DZB-28
- İş: büyük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Editing/EditorView.Monitor.cs, src/VidShrink.App/Editing/EditorView.axaml, src/VidShrink.App/Locales/, tests/VidShrink.Tests/DuzenleyiciKlipOzellikTests.cs
- Plan: DZB-P3'ün üstüne gelir: eğriler ve üç bölgeli renk dengesi ffmpeg `curves` ve `colorbalance` süzgeçleriyle, tek tıkla düzeltme `normalize` ile yapılır. Bulanıklık `gblur` ile bütün kareye ya da monitörde çizilen dikdörtgene uygulanır (kırp, bulanıklaştır, geri bindir). İki çekimin rengini eşlemek için hazır süzgeç bilinmiyor; o yarı ayrı ölçüm ister. Paket ikiye bölünebilir: renk (DZB-36, DZB-38) ile bulanıklık (DZB-28); doğrulama argüman testleri ve kare karşılaştırması.

### DZB-P14 — Anahtar Kare Ve Hız Rampası
- Satırlar: DZB-25, DZB-48, DZB-50, DZB-26
- İş: büyük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/Editing/ClipEffects.cs, src/VidShrink.Core/Editing/ClipFilters.cs, src/VidShrink.Core/Editing/EditLook.cs, src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.App/Editing/EditorView.Klip.cs, src/VidShrink.App/Editing/EditorTimeline.cs, tests/VidShrink.Tests/
- Plan: Klip ayarları (ses düzeyi, kırpma, konum) klip içinde zamanla değişebilsin diye değerlere anahtar kare eklenir; süzgeç zinciri sabit sayı yerine zamana bağlı ifade yazar, yumuşatma eğrisi iki kare arasındaki geçişin biçimidir. Hız rampası klibi hız dilimlerine böler (`setpts`), akıcı ağır çekim ara kareyi `minterpolate` süzgeciyle üretir. Önizleme zinciri bugün sabit değer taşıyor; zamana bağlı ifadeyi taşıyıp taşımadığı ilk iş ölçülür. Paket ikiye bölünebilir: anahtar kare (DZB-25, DZB-26) ile hız (DZB-48, DZB-50); doğrulama argüman testleri ve çıktı süresinin ölçümü.

### DZB-P15 — Çok İz Üstüne Gelenler
- Satırlar: DZB-10, DZB-46, DZB-47, DZB-06, DZB-07
- İş: büyük
- Karar: sahip: Çok iz açıldıktan sonra üstüne gelen işler (sesi ayırma, otomatik müzik kısma, seslendirme kaydı, iz denetimleri, boşluklu çizelge) kapsama girsin mi?
- Dokunacağı yer: src/VidShrink.Core/Editing/EditTimeline.cs, src/VidShrink.Core/Editing/EditExport.cs, src/VidShrink.Core/Editing/EditProject.cs, src/VidShrink.Core/AudioCaptureArguments.cs, src/VidShrink.App/Editing/EditorTimeline.cs, src/VidShrink.App/Editing/EditorView.axaml, tests/VidShrink.Tests/
- Plan: DZB-P7 ve DZB-P12 bitmeden açılmaz ve üç parçaya bölünebilir. Ses ekleri (DZB-10, DZB-46, DZB-47): klibin sesi ikinci ize ayrı parça olarak iner, müzik konuşma başlayınca `sidechaincompress` süzgeciyle kısılır, anlatım kaydedicinin hazır mikrofon yoluyla kaydedilip ize konur. İz denetimleri (DZB-06) izi susturma, gizleme ve kilitlemedir; boşluklu çizelge (DZB-07) sıralı listeyi konumlu modele çevirdiği için en ağır parçadır. Doğrulama: her parça için EditTimeline birim testleri ve teslim dosyasında ses düzeyi ölçümü.

### DZB-P16 — Vekil, Hazır İçerik Ve Model
- Satırlar: DZB-51, DZB-22, DZB-52, DZB-53, DZB-44, DZB-69, DZB-70, DZB-71, DZB-72
- İş: büyük
- Karar: sahip: Uygulama kullanıcının diskine büyük ek dosya (vekil kopya, hazır içerik paketi, yapay zekâ modeli) üretsin ya da indirsin mi; hangisi istekle, hangisi hiç?
- Dokunacağı yer: src/VidShrink.Core/Editing/, src/VidShrink.Core/IntermediateCodecs.cs, src/VidShrink.App/Editing/
- Plan: Bu paket bir karar demetidir, tek seferde teslim edilmez: sahip hangi satırı açarsa o satır kendi paketi olur. Vekil dosya (ağır kaynağın kurgu sırasında kullanılan küçük kopyası; DZB-51) indirme istemez, yalnız disk yeri ister ve en bağımsız parçadır. Hazır içerik (DZB-22, DZB-52, DZB-53) lisans ve paket boyutu, modelli işler (DZB-44, DZB-69, DZB-70, DZB-71, DZB-72) indirme ve çalışma zamanı sorusudur. Doğrulama her satır için ayrı kurulur.

## Pakete Girmeyenler

- DZB-31, DZB-32, DZB-33, DZB-63, DZB-68 — önceki kararla kapalı (K8 ve K5); karar geri açılmadan iş açılmaz.
- DZB-13, DZB-14, DZB-15 — dar kitleye bakan üç kırpma aracı; her biri ayrı sürükleme davranışı istediği için bir paketin yanında bedavaya gelmiyor, istenirse üçü tek küçük paket olur.
- DZB-09, DZB-60, DZB-66 — profesyonel kurgu akışına bakan büyük işler; pazarda tek ya da dar kitlede.
- DZB-40, DZB-67, DZB-76 — çok ize bağlı; DZB-P7 ve DZB-P12 bitmeden anlamı yok.
- DZB-27, DZB-37, DZB-39, DZB-59, DZB-61, DZB-65 — tek rakipte ya da dar kitlede ve hiçbir paketin dosyalarına bedavaya oturmuyor.
- DZB-57 — kardeş tablodaki DZA-39 ile aynı iş; iki kez paketlenmesin diye kesim tablosuna bırakıldı.
- DZB-34, DZB-73, DZB-74, DZB-75, DZB-77 — üçüncü taraf ikili dosya, bulut hizmeti ya da hesap ister; öncelik 3 ve sahip kararı olmadan başlamaz.
