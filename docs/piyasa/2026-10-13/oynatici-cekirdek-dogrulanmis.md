# Oynatıcı Sekmesi — Çekirdek Oynatma: Doğrulanmış Eksikler

13 Ekim 2026. Doğrulama turu çıktısı. Kaynak numaraları `oynatici-cekirdek.md` dosyasındaki Kaynaklar bölümüne bakar. Tablodaki 55 satırın hepsi kodda arandı; öncelik 1 satırlarının kaynakları bu turda doğrudan açıldı (liste Ek Notlar'da). Değişen her hücre Düzeltmeler'de.

| No | Eksik | Kimde Var | Bizde Durum | Fayda | İş | Öncelik | Karar | Kaynak |
|---|---|---|---|---|---|---|---|---|
| OYA-01 | Uyku ve ekran kararması engeli: oynatma sürerken ekranın kapanmasını ve ekran koruyucuyu durdurma. | mpv, VLC, IINA | yok (kodda engelleme çağrısı bulunamadı; en yakın kod MpvEngine.cs:153, pencereyi mpv yönetmiyor; ekranın gerçekten karardığı ölçülmedi) | Uzun videoyu fareye dokunmadan izlerken ekran kapanmaz. | orta | 1 | gerekmez | [1][8][22] |
| OYA-02 | Çift tıkla tam ekran: görüntüye çift tıklayınca tam ekrana geçme ve çıkma. | mpv, IINA, PotPlayer, Haruna | yok (sol tık oynat ya da duraklat, orta tık tam ekran: Keymap.cs:110–111; sol tık işini bırakışta yapıyor ve çift tık bilerek ayrı hareket sayılmıyor: PlayerView.Fare.cs:45, 88–94; çift tık yalnız başlık çubuğunda: MainWindow.axaml.cs:908) | Başka oynatıcıdan gelen kişi alıştığı hareketle tam ekrana geçer. | orta | 1 | sahip: varsayılan fare davranışını değiştirir | [2][21][36][52] |
| OYA-03 | Ses çıkış aygıtı seçimi: sesin hangi hoparlöre ya da kulaklığa gideceğini oynatıcının içinden seçme. | mpv, VLC, IINA, Elmedia ve 3 rakip daha | yok (en yakın: ses çıkışını yeniden açma, MpvEngine.cs:603) | Kulaklık ile hoparlör arasında Windows ayarına gitmeden geçilir. | orta | 1 | gerekmez | [1][6][8][21][41][42][45][56] |
| OYA-04 | Ses ve altyazı dil tercihi: çok dilli dosyada tercih edilen dildeki ses ve altyazı izini kendiliğinden seçme. | mpv, VLC, IINA, Movist Pro ve 3 rakip daha | yok (altyazı her açılışta kapalı başlar: MpvEngine.cs:166; izler elle gezilir: PlayerView.Tracks.cs:61–86) | Her dosyada ses ve altyazı izini elle seçmek gerekmez. | orta | 1 | sahip: altyazının kapalı başlama varsayılanını değiştirir | [1][8][21][42][45][53][54] |
| OYA-05 | Dosya başına seçimleri hatırlama: aynı dosya yeniden açılınca ses izini, altyazı izini, gecikmeyi ve hızı geri getirme. | mpv, SMPlayer, mpv.net | yarım: yalnız konum ve yer imi saklanıyor (PlaybackHistory.cs:209–222); gecikmeler ve ikincil altyazı her açılışta sıfırlanıyor (PlayerView.Tracks.cs:38–46) | Yarım kalan film aynı altyazı ve ses ayarıyla sürer. | orta | 2 | gerekmez | [1][45][49] |
| OYA-06 | Ses düzeyini hatırlama: uygulama kapanıp açılınca son ses düzeyiyle başlama. | VLC, mpv.net, IINA | yarım: oturum içinde dosyadan dosyaya korunuyor (PlayerView.axaml.cs:739–740); uygulama yeniden açılınca 100 ile başlıyor (PlayerView.axaml.cs:60), ayar dosyasında ses alanı yok (PlayerSettings.cs:96–108) | Her açılışta sesi yeniden kısmak gerekmez. | küçük | 1 | gerekmez | [8][21][22][47] |
| OYA-07 | Ekran üstü bildirim kapsamı: atlama konumunu, iz değişimini, gecikmeyi, A-B noktasını ve kare kaydı sonucunu video üstünde kısa süre gösterme. | mpv, VLC, IINA | yarım: video üstü rozet yalnız hız ve seste çıkıyor (PlayerView.Gosterge.cs:32, 61; çağrılar PlayerView.axaml.cs:186–209); iz ve gecikme yalnız izleme günlüğüne yazılıyor (PlayerView.Tracks.cs:72, 85, 101, 108) | Tam ekranda bastığı tuşun ne yaptığını görür. | küçük | 1 | gerekmez | [1][2][8][21] |
| OYA-08 | İmleci kendiliğinden gizleme: fare durunca görüntü üstündeki imleci saklama. | mpv, VLC | yok (imleci gizleyen kod bulunamadı; en yakın: denetim şeridinin gecikmeyle gizlenmesi, PlayerView.Serit.cs:140, 236; ekranda denenmedi) | Tam ekranda imleç görüntünün üstünde kalmaz. | küçük | 2 | gerekmez | [1][8] |
| OYA-09 | Bölüm adları ve bölüm listesi: bölümleri adıyla listeleme ve zaman çubuğunda adını gösterme. | mpv, IINA, Screenbox, Celluloid ve 2 rakip daha | yarım: bölüm işareti ve bölüm atlama var, yalnız bölümün zamanı okunuyor, adı okunmuyor (MpvEngine.cs:430–444; IPlaybackEngine.cs:193; PlayerView.Window.cs:186, 564) | Uzun kayıtta istenen bölüme adından gidilir. | küçük | 1 | gerekmez | [2][3][20][21][45][51][54][56] |
| OYA-10 | Zamana git: saat, dakika ve saniye yazarak o ana atlama. | MPC-HC, VLC | yok (en yakın: başa dön, Keymap.cs:64, 117) | Bilinen bir dakikaya çubuğu sürüklemeden gidilir. | küçük | 2 | gerekmez | [10][15][16] |
| OYA-11 | HDR videoyu SDR ekranda doğru gösterme: parlaklık aralığı geniş (HDR) videoyu sıradan ekrana sığdırma, yani ton eşleme. | mpv, IINA, MPC-HC, PotPlayer ve 3 rakip daha | yok (oynatıcıda ton eşleme yok, yalnız kodlamada var: EncodePlan.cs:11; HDR dosyanın bugün nasıl göründüğü ölçülmedi) | Telefonla çekilmiş HDR video sıradan ekranda soluk ya da patlak görünmez. | orta | 1 | sahip: GPU'suz yol işlemcide çalışır, 4K'da yük getirir ve varsayılan görüntüyü değiştirir; önce ölçüm gerekir | [1][6][11][20][21][35][36][42][46][64] |
| OYA-12 | Ton eşleme ayarları: ton eşleme yöntemini ve hedef tepe parlaklığı seçme. | mpv, IINA, PotPlayer, mpv.net | yok (yazılım çiziminde GPU seçenekleri kareye dokunmuyor: MpvEngine.Advanced.cs:25–29) | HDR görüntü ekranın gücüne göre ince ayarlanır. | büyük | 3 | kapalı: K4 | [1][20][21][36][46] |
| OYA-13 | HDR çıkışı: HDR ekranda videoyu HDR olarak gösterme. | mpv, VLC, MPC-HC, IINA | yok (çıktı 8 bit BGRA: VidShrink.Player/AGENTS.md:4) | HDR ekranı olan kişi videoyu tam parlaklık aralığıyla izler. | büyük | 3 | kapalı: K4 | [1][7][11][21] |
| OYA-14 | Dolby Vision renk dönüşümü: Dolby Vision dosyanın rengini kendi üst verisiyle çözme. | mpv, MPC-HC, MPC-BE, Infuse ve 1 rakip daha | yarım: yalnız etiket ve profil numarası gösteriliyor (MediaDetails.cs:26–38; MpvEngine.cs:473) | Dolby Vision çekim doğru renkle görünür. | büyük | 3 | kapalı: K4 | [1][11][18][40][43] |
| OYA-15 | HDR10+ ayrımı ve tepe parlaklık bilgisi: bilgi panelinde HDR türünü ve nit değerlerini gösterme. | mpv | yarım: HDR10, HLG ve Dolby Vision ayrılıyor, HDR10+ ve tepe parlaklık yok (MediaDetails.cs:26–38); defterde | Dosyanın hangi HDR türünde ve kaç nit olduğu görülür. | küçük | 3 | gerekmez | [3] |
| OYA-16 | Kare ara değerleme ve ekranla eşzamanlama: video kare hızını ekranın yenileme hızına uydurup takılmayı azaltma. | mpv, mpv.net | yok (yazılım çiziminde GPU seçenekleri kareye dokunmuyor: MpvEngine.Advanced.cs:25–29) | 24 karelik film 60 Hz ekranda daha akıcı görünür. | büyük | 3 | kapalı: K4 | [1][27][46] |
| OYA-17 | Shader yükleme: görüntüyü ekran kartında işleyen küçük program dosyalarını oynatıcıya takma. | mpv, MPC-HC | yok (yazılım çiziminde GPU seçenekleri kareye dokunmuyor: MpvEngine.Advanced.cs:25–29) | Meraklı kullanıcı kendi keskinleştirme ya da büyütme süzgecini ekler. | büyük | 3 | kapalı: K4 | [1][15] |
| OYA-18 | Ekran kartıyla büyütme ve SDR'den HDR üretme: RTX Video Super Resolution, AMD ve benzeri sürücü özellikleri. | VLC, mpv, Infuse, Screenbox | yok (yazılım çiziminde GPU seçenekleri kareye dokunmuyor: MpvEngine.Advanced.cs:25–29) | Düşük çözünürlüklü video büyük ekranda daha net görünür. | büyük | 3 | kapalı: K4 | [5][6][7][39][55][66] |
| OYA-19 | Ölçekleyici ve bant giderme seçimi: büyütme süzgecini seçme ve renk basamaklarını yumuşatma. | mpv, VLC | yok (en yakın: işlemcide çalışan keskinlik süzgeci, MpvEngine.Advanced.cs:76–98) | Sıkıştırılmış videoda gökyüzü gibi geçişler basamaksız görünür. | büyük | 3 | kapalı: K4 | [1][6] |
| OYA-20 | Renk yönetimi: görüntüyü ekranın ICC renk profiline göre düzeltme. | mpv, IINA, VLC | yok (çıktı 8 bit BGRA, ekran profili okunmuyor: VidShrink.Player/AGENTS.md:4) | Ayarlı ekranda renkler doğru görünür. | büyük | 3 | kapalı: K4 | [1][6][21] |
| OYA-21 | Bitstream passthrough: Dolby ve DTS sesi çözmeden alıcıya olduğu gibi gönderme. | mpv, VLC, IINA, Infuse ve 2 rakip daha | yok (en yakın: ses çıkışını yeniden açma, MpvEngine.cs:603) | Ev sineması alıcısı çok kanallı sesi kendi çözer. | küçük | 2 | gerekmez | [1][6][21][31][40][41][42] |
| OYA-22 | Özel kip ses çıkışı: ses aygıtını tek başına kullanıp sesi Windows karıştırıcısından geçirmeden verme. | mpv, VLC | yok (en yakın: ses çıkışını yeniden açma, MpvEngine.cs:603) | Ses, başka uygulamaların sesiyle karışmadan aygıta gider. | küçük | 3 | gerekmez | [1][6] |
| OYA-23 | Kanal düzeni ve stereo indirgeme: çok kanallı sesi iki kanala indirirken düzeni ve dengeyi seçme. | mpv, VLC, MPC-BE, Movist Pro | yok (en yakın: ekolayzer ve dengeleme süzgeçleri, MpvEngine.Advanced.cs:100–113) | 5.1 film kulaklıkta diyalog kısık kalmadan dinlenir. | küçük | 2 | gerekmez | [1][8][19][42] |
| OYA-24 | Perde düzeltme anahtarı ve perde kaydırma: hız değişince sesin incelip kalınlaşmasını açıp kapama, perdeyi hızdan ayrı değiştirme. | mpv, VLC, MPC-HC | yarım: düzeltme mpv varsayılanıyla açık kalıyor, anahtarı ve perde ayarı yok (MpvEngine.cs:261, 280 yalnız hızı yazıyor) | Müzik çalışan kişi hızı ve perdeyi ayrı ayrı ayarlar. | küçük | 3 | gerekmez | [1][8][11] |
| OYA-25 | Altyazı satırına atlama ve satıra göre eşzamanlama: önceki ya da sonraki altyazıya gitme, altyazıyı bir satır kaydırma. | mpv, MPC-HC, VLC | yok (en yakın: 0,5 sn adımlı gecikme, SubtitleOptions.cs:17) | Dil çalışan kişi cümleyi tek tuşla yeniden dinler, kayık altyazı tek tuşla oturur. | küçük | 2 | gerekmez | [2][8][15][33] |
| OYA-26 | Altyazıyı tek tuşla gizleme: izi değiştirmeden altyazıyı kapatıp açma. | mpv, VLC, Celluloid | yarım: S tuşu izleri sırayla gezip kapalıya geliyor (Keymap.cs:168; PlayerView.Tracks.cs:75–86) | Bir sahnede altyazı tek tuşla kalkar ve geri gelir. | küçük | 2 | gerekmez | [2][8][50] |
| OYA-27 | ASS biçemini geçersiz kılma: biçemli (ASS) altyazıda dosyanın kendi yazı tipini ve rengini kullanıcının ayarıyla değiştirme. | mpv, IINA, mpv.net, Celluloid | yarım: biçem ayarları yazılıyor (MpvEngine.Advanced.cs:115–122), ASS dosyasına uygulanıp uygulanmadığı ölçülmedi | Okunması zor biçemli altyazı sade görünüme çevrilir. | küçük | 2 | gerekmez | [1][21][45][50] |
| OYA-28 | İkincil altyazı ayarları: ikinci altyazının konumunu, gecikmesini ve görünürlüğünü ayrı ayarlama. | mpv, VLC, IINA | yarım: yalnız iz seçimi var (IPlaybackEngine.cs:136, 152; MpvEngine.cs:357); ikincil altyazı 10 Ekim 2026'da gerçek pencerede görüldü (acik.md:77), eski defter satırı hâlâ açık (acik.md:10); defterde | İki dilde altyazı üst üste binmeden okunur. | küçük | 3 | gerekmez | [1][8][21] |
| OYA-29 | Alt klasörde altyazı arama: videonun yanındaki Subs gibi klasörlerde altyazıyı kendiliğinden bulma. | mpv, VLC, IINA, Haruna | yarım: yalnız videonun klasöründe adı videoyla başlayan dosyalar yükleniyor (SubtitleOptions.cs:35, 42–57) | Ayrı klasördeki altyazı elle açılmadan gelir. | küçük | 1 | gerekmez | [1][8][21][53] |
| OYA-30 | Dış görüntü tabanlı altyazı dosyası: yan dosya olarak gelen PGS (.sup) ve VobSub (.idx) altyazıyı açma. | Infuse | yarım: dış altyazı uzantıları srt, ass, ssa, vtt, sub (SubtitleOptions.cs:35) | Diskten çıkarılmış resim altyazılar da açılır. | küçük | 3 | gerekmez | [39] |
| OYA-31 | Zorunlu altyazıyı kendiliğinden gösterme: yalnız yabancı dildeki konuşmaları çeviren (forced) izi kendiliğinden açma. | mpv, VLC | yok (en yakın: sid=no, MpvEngine.cs:166) | Filmdeki yabancı dil sahneleri altyazısız kalmaz. | küçük | 2 | sahip: altyazının kapalı başlama varsayılanını değiştirir | [1][6] |
| OYA-32 | Altyazı metnini panoya kopyalama: ekrandaki altyazı satırını tek tuşla kopyalama. | MPC-HC | yok (en yakın: kareyi panoya kopyalama, Keymap.cs:74, 155) | Bilinmeyen sözcük sözlüğe ya da çeviriye yapıştırılır. | küçük | 3 | gerekmez | [13][15] |
| OYA-33 | İşitme engelli notlarını ayıklama: altyazıdaki ses betimlemelerini gizleme. | mpv | yok (en yakın: altyazı biçem ayarı, MpvEngine.Advanced.cs:115–122) | Ekranda yalnız konuşma metni kalır. | küçük | 3 | gerekmez | [1] |
| OYA-34 | Altyazı yazı tipi seçimi: sistemdeki herhangi bir yazı tipini, kalın ve eğik seçeneğiyle kullanma. | mpv, IINA, PotPlayer, Elmedia ve 2 rakip daha | yarım: üç genel aile seçilebiliyor (PlayerView.Advanced.cs:31) | Altyazı, kullanıcının rahat okuduğu yazı tipiyle çıkar. | küçük | 2 | gerekmez | [1][21][36][41][45][53][65] |
| OYA-35 | Yapay zekâ ile altyazı üretimi: konuşmadan kendiliğinden altyazı çıkarma. | PotPlayer, GOM Player | yok (en yakın: çevrimiçi altyazı arama, PlayerView.Subtitles.cs:71) | Altyazısı olmayan video altyazıyla izlenir. | büyük | 3 | kapalı: K5 | [36][37][38] |
| OYA-36 | Devam sınırı: kaldığı yerden devamı yalnız son beş dosyayla sınırlamama. | mpv, MPC-HC, IINA, Screenbox ve 2 rakip daha | yarım: devam yalnız en son 5 dosyada çalışıyor (PlaybackHistory.cs:18, 107) | Dizi izlerken altıncı dosya da kaldığı yerden açılır. | küçük | 1 | sahip: sınır bilerek konmuş olabilir, varsayılan davranışı değiştirir | [1][11][21][49][53][54][63] |
| OYA-37 | Sessiz bölümleri atlama: konuşma olmayan yerleri kendiliğinden geçme ya da hızlandırma. | hazır sunan rakip doğrulanmadı; istek mpv ve VLC takipçisinde | yok (en yakın: iki hız arasında geçiş, Keymap.cs:83) | Ders ve toplantı kaydı daha kısa sürede izlenir. | orta | 1 | sahip: yeni oynatma kipi | [23][34] |
| OYA-38 | Basılı tutarak geçici hızlandırma: tuşa ya da fareye basılı tutulduğu sürece hızlı oynatma. | MPC-BE, PotPlayer, Infuse, Screenbox | yok (en yakın: iki hız arasında geçiş, Keymap.cs:83; basılı tutma diye girdi türü yok, sol tık işini bırakışta yapıyor: PlayerView.Fare.cs:45, 88–94) | Sıkıcı kısım parmak kaldırılana dek hızlı geçer. | orta | 1 | sahip: sol tıkın oynat ya da duraklat davranışıyla çakışır | [18][36][39][54] |
| OYA-39 | Hız aralığı: 4 katın üstünde ve 0,25 katın altında oynatma. | mpv | yarım: 0,25 ile 4 kat arası (Keymap.cs:50–51) | Uzun kayıt çok hızlı taranır. | küçük | 3 | gerekmez | [1] |
| OYA-40 | Ters oynatma: videoyu geriye doğru oynatma. | mpv | yok (en yakın: düzenleyici önizlemesinde geri oynatma, EdlPreviewDriver.cs:97, 214) | Bir hareket geriye sarılarak incelenir. | orta | 3 | gerekmez | [1] |
| OYA-41 | A-B tekrar sayısı: işaretli aralığı belirli sayıda tekrarlayıp sürdürme. | mpv, VLC | yarım: A-B aralığı sonsuz dönüyor (MpvEngine.cs:276–278, 303–304) | Bir bölüm üç kez dinlenir, video kendiliğinden devam eder. | küçük | 3 | gerekmez | [1][6] |
| OYA-42 | Önizleme küçük resmi ayarı: zaman çubuğu önizlemesinin boyutunu değiştirme ya da kapatma. | IINA, Haruna | yarım: sabit 128×72 (PlayerView.Tools.cs:24–25) | Büyük ekranda önizleme daha okunaklı olur. | küçük | 3 | gerekmez | [20][21][52] |
| OYA-43 | Altyazılı kare kaydı: kareyi altyazısıyla birlikte kaydetme seçeneği. | mpv, IINA, mpv.net, Celluloid | yarım: kare her zaman altyazısız kaydediliyor (MpvEngine.cs:538) | Altyazılı alıntı karesi tek tuşla alınır. | küçük | 2 | gerekmez | [2][21][45][46][50] |
| OYA-44 | Küçük resim tablosu: videodan eşit aralıklı karelerle tek sayfalık özet resim üretme. | MPC-HC, SMPlayer | yok (en yakın: zaman çubuğu önizlemesi, PlayerView.Tools.cs:233) | Videonun içeriği tek resimde paylaşılır. | orta | 2 | gerekmez | [15][49] |
| OYA-45 | Art arda kare yakalama: her kareyi ya da belirli aralıkla kareleri kendiliğinden kaydetme. | mpv, Elmedia | yok (en yakın: tek kare kaydı, PlayerView.Window.cs:262–271) | Bir hareketin kareleri tek tek toplanır. | küçük | 3 | gerekmez | [2][41] |
| OYA-46 | Kare kaydı biçimleri: WebP, JPEG XL, AVIF ve yüksek bit derinliği. | mpv, VLC | yarım: PNG ve JPG (PlayerSettings.cs:19–23) | Kare daha küçük dosyayla ya da daha çok renk basamağıyla kaydedilir. | küçük | 3 | gerekmez | [1][6] |
| OYA-47 | Kenar bantlarını kırparak doldurma: tek tuşla siyah bantları görüntüyü kırparak kapatma. | mpv, VLC, MPC-HC | yarım: elle yakınlaştırma (Keymap.cs:109) ve yüzde 20'ye kadar kırpma (AdvancedSettings.cs:7–32) var | Bantlı video geniş ekranı doldurur. | küçük | 2 | gerekmez | [1][2][8][11] |
| OYA-48 | Pencereyi video boyutuna getirme: yarım, bire bir ve iki kat boyut kısayolları. | mpv, VLC, Screenbox | yok (en yakın: kompakt ya da tam ekran geçişi, Keymap.cs:66, 111) | Video kendi çözünürlüğünde, piksel piksel izlenir. | küçük | 3 | sahip: sekmeli ana pencerenin boyut davranışını değiştirir | [2][8][55][67] |
| OYA-49 | Yalnız oynatırken üstte tutma: pencere oynatma sürerken üstte kalır, duraklayınca bırakır. | MPC-HC, IINA | yarım: üstte tut açılıp kapanıyor (Keymap.cs:71, 151–152) | Duraklatınca pencere öbür işlerin önünü kapatmaz. | küçük | 3 | gerekmez | [15][21] |
| OYA-50 | Küçültünce duraklatma: pencere simge durumuna inince oynatmayı kendiliğinden durdurma. | VLC, IINA | yok (en yakın: mini kip geçişi, PlayerView.Tools.cs:96) | Pencere indirilince film arkada akıp gitmez. | küçük | 2 | gerekmez | [9][21] |
| OYA-51 | Otomatik deinterlace: taramalı (interlaced) videoyu kendiliğinden tanıyıp düzeltme ve yöntem seçme. | mpv, VLC | yarım: yalnız elle aç ve kapa (MpvEngine.Advanced.cs:97) | Eski kamera ve TV kaydı tarak izi olmadan oynar. | küçük | 2 | sahip: varsayılan davranışı değiştirir | [1][6][8] |
| OYA-52 | 360° video: küresel videoda bakış yönünü fareyle çevirme. | VLC, GOM Player | yok (en yakın: ayna süzgeci, MpvEngine.cs:522–523) | Aksiyon kamerası ve VR çekimleri düzgün izlenir. | büyük | 3 | sahip: yeni görüntüleme kipi, GPU'suz izdüşüm işlemciye yük getirir | [6][8][26][37] |
| OYA-53 | Dokunmatik hareketler: dokunarak atlama, ses ayarı ve iki parmakla yakınlaştırma. | mpv, IINA, VLC, Screenbox ve 1 rakip daha | yok (en yakın: fareyle yakınlaştırma ve kaydırma, PlayerView.Fare.cs:80, 103) | Dokunmatik dizüstünde oynatıcı parmakla yönetilir. | orta | 2 | gerekmez | [1][6][21][54][58] |
| OYA-54 | İstatistik katmanında ek ölçümler: zaman kodu, ses ve görüntü kayması, kare zamanlaması. | mpv | yarım: beş ölçüm gösteriliyor (PlaybackStats.cs:7–12; MpvEngine.cs:500–504) | Takılmanın nedeni panelden okunur. | küçük | 3 | gerekmez | [2][5] |
| OYA-55 | Atlama adımını ayarlama: ok tuşlarının kaç saniye atlayacağını kullanıcının belirlemesi. | IINA, Screenbox, Haruna, Clapper | yarım: dört sabit adım var (1, 10, 60 ve 300 sn), değeri değiştirilemiyor (Keymap.cs:52–55) | Kişi alıştığı 5 ya da 30 saniyelik atlamayı kullanır. | orta | 2 | gerekmez | [21][52][54][58][62] |

## Düzeltmeler

Yedi satırın işi `küçük`ten `orta`ya çıktı, bir satırın önceliği yükseldi, altı satırın Bizde Durum hücresi kanıtla güncellendi.

| No | Ne Değişti | Kanıt |
|---|---|---|
| `OYA-01` | İş `küçük` → `orta` | Pencereyi mpv yönetmediği için (MpvEngine.cs:153) engeli uygulama kendi koymalı; uygulama Windows, macOS ve Linux'ta dağıtılıyor (kök AGENTS.md), üçü ayrı sistem çağrısı ister. Yalnız Windows kolu bir günün altında. |
| `OYA-02` | İş `küçük` → `orta`; Bizde Durum'a kanıt eklendi | Sol tık işini bırakışta yapıyor ve kod çift tıkı bilerek ayrı hareket saymıyor (PlayerView.Fare.cs:45, 88–94); `PlayerInputKind` içinde çift tık türü yok (Keymap.cs). Yeni girdi türü, kısayol tablosu ve sürükleme eşiğiyle uyum gerekir. |
| `OYA-03` | İş `küçük` → `orta` | Aygıt listesini okuma, menü, seçimi saklama ve aygıt kaybolunca geri dönüş birlikte gerekir; bugün yalnız `ao-reload` var (MpvEngine.cs:603), ayar dosyasında ses alanı yok (PlayerSettings.cs:96–108). |
| `OYA-04` | İş `küçük` → `orta` | Tercihi tutacak ayar alanı ve arayüz yok (PlayerSettings.cs:96–108); açılış yolu her dosyada izleri sıfırlıyor (PlayerView.Tracks.cs:38–46); dil eşleme kodu yalnız kodlama tarafında (StreamMapping.cs:143). |
| `OYA-05` | İş `küçük` → `orta`; Bizde Durum'a açılıştaki sıfırlama eklendi | Kayıt şeması değişir (PlaybackHistory.cs:209–222) ve açılış yolu bugün gecikmeleri ve ikincil altyazıyı açık çağrıyla sıfırlıyor (PlayerView.Tracks.cs:38–46); eski kayıt dosyasıyla uyum da gerekir. |
| `OYA-06` | Bizde Durum `yok` → `yarım` | Ses düzeyi ve sessiz durumu oturum içinde yeni dosyaya taşınıyor (PlayerView.axaml.cs:739–740); eksik olan yalnız kapanıp açılınca saklama (PlayerView.axaml.cs:60). |
| `OYA-09` | Bizde Durum'daki kanıt satırları açılıp okunanlarla değiştirildi | Yalnız `chapter-list/N/time` okunuyor (MpvEngine.cs:430–444); arayüz üyesi zaman listesi döndürüyor (IPlaybackEngine.cs:193); işaretler bu listeden kuruluyor (PlayerView.Window.cs:186, 564). |
| `OYA-28` | Bizde Durum'daki "gözle görülmedi" çıkarıldı | Defterin yeni satırı ikincil altyazının 10 Ekim 2026'da gerçek pencerede görüldüğünü söylüyor (acik.md:77); eski satır kapatılmamış (acik.md:10). |
| `OYA-36` | Öncelik `2` → `1` | Ölçüt tutuyor: üç yaygın rakipte belgeli ve ana işe dokunuyor. Bu turda açılan kaynaklar: mpv `--watch-later-options` (options.rst:1161), IINA `resumeLastPosition` (Preference.swift:78), Screenbox v0.15.0 sürüm notu. Kardeş tabloda aynı eksik (OYB-12) zaten öncelik 1. |
| `OYA-38` | İş `küçük` → `orta`; Bizde Durum'a kanıt eklendi | Basılı tutma diye girdi türü yok; sol tık bırakışta oynat ya da duraklat yapıyor (PlayerView.Fare.cs:88–94; Keymap.cs:110). Sol tık testi zaten kararsız (acik.md:107; docs/olcumler/soltik-kararsiz.md). |
| `OYA-55` | İş `küçük` → `orta` | Adımlar derleme sabiti ve durağan tuş satırlarına gömülü (Keymap.cs:52–55, 122–129); şerit düğmeleri aynı sabiti okuyor (PlayerView.Serit.cs:183–184, 322–325). Kısayol ayarı tuşu değiştiriyor, miktarı değiştirmiyor. |

## Çıkarılanlar

Çıkarılan satır yok. Hiçbir eksik kodda tam olarak bulunmadı.

| No | Eksik | Neden | Kanıt (dosya:satır) |
|---|---|---|---|

Araştırma kolunun "küçük ama her rakipte var" dediği sekiz eksik ayrıca arandı. Sonuç: sekizi de gerçekten eksik ya da yarım.

- Uyku engeli (`OYA-01`): `SetThreadExecutionState`, `PowerCreateRequest`, `IOPMAssertion`, `screensaver`, `Inhibit`, `KeepAwake`, `PreventSleep`, `UykuEngel` adlarıyla `src/` altında eşleşme yok.
- Çift tık (`OYA-02`): `ClickCount` ve `DoubleTapped` oynatıcıda yok; `ClickCount` yalnız ana pencerede geçiyor (başlık çubuğu MainWindow.axaml.cs:908, zemin taşıma MainWindow.ZeminTasima.cs:33). Oynatıcıdaki `_leftClicks` yalnız sayaç, davranış bağlı değil (PlayerView.Fare.cs:56; PlayerView.axaml.cs:104).
- Ses aygıtı (`OYA-03`): `audio-device`, `AudioDevice`, `SesAygit`, `SesCikis`, `OutputDevice` oynatıcıda yok; kaydedicideki eşleşme yakalama aygıtı, çıkış değil (RecorderView.Ses.cs:43, 60).
- Dil tercihi (`OYA-04`): `alang`, `slang` yok; `PreferredLanguage` yalnız kodlama tarafında (StreamMapping.cs:143; PlanCalculator.cs:128).
- Ses düzeyi (`OYA-06`): `_volume` ayar dosyasına yazılmıyor (PlayerSettings.cs:96–108); oturum içi taşıma var (PlayerView.axaml.cs:739–740).
- Ekran üstü bildirim (`OYA-07`): `ShowOsd`, `ShowSpeedOsd`, `ShowSoundOsd` yalnız beş yerde çağrılıyor (PlayerView.axaml.cs:186, 192, 199, 203, 209); mpv'nin kendi bildirimi kapalı (MpvEngine.cs:163–164).
- Bölüm adları (`OYA-09`): `chapter-list` yalnız sayı ve zaman için okunuyor (MpvEngine.cs:430–444).
- Alt klasörde altyazı (`OYA-29`): `sub-file-paths` yok, `sub-auto` kapalı (MpvEngine.cs:165); `Sidecars` yalnız videonun klasörünü geziyor (SubtitleOptions.cs:42–57).

Arayüz metni: `src/VidShrink.App/Locales/en` altında oynatıcı dosyalarında (playback, tracks, advanced, tools) uyku, çift tık, ses aygıtı, dil tercihi, bölüm ve imleç için metin yok; çıkan iki eşleşme deinterlace ve yazı tipi (advanced.json:15, 27).

## Yinelenenler

- OYA-36 = OYB-12 (devam sınırı; kardeş tabloda öncelik 1, burada bu turda 2'den 1'e çıktı). Aynı iş iki pakete girdi: burada OYA-P5, kardeş kolun çıktısında OYB-P5 (o dosyaya 18:20'de bakıldı, hâlâ yazılıyor olabilir). İş tek sabit (PlaybackHistory.cs:18) ve karar tek; hangisi önce yapılırsa satır öbür paketten düşer.
- OYA-52 = OYB-46 (360° video; iki tabloda da büyük, öncelik 3, sahip)

Komşu ama aynı olmayanlar: OYB-13 (devamı kapatma ya da sorma ayarı) OYA-36 ile aynı dosyaya dokunur; OYB-06 (listede küçük resim) OYA-44 ile aynı şey değil; OYB-10 (bitince bilgisayarı uyutma) OYA-01'in tersidir.

## Doğrulanamayanlar

- OYA-11: HDR dosyanın bugün oynatıcıda nasıl göründüğü ölçülmedi. `docs/olcumler/` altında HDR geçen dosyalar adlarına göre kodlama tarafında; oynatıcı tarafı ölçüm bulunamadı. İşlemcide ton eşlemenin dayanacağı ffmpeg süzgeçlerinin (`zscale`, `tonemap`) libmpv derlemesinde olup olmadığı bilinmiyor; `orta` tahmini buna bağlı.
- OYA-01: Ekranın oynatma sırasında gerçekten karardığı ölçülmedi; kodda engel çağrısı olmadığı kesin.
- OYA-27: Biçem ayarının ASS altyazıya uygulanıp uygulanmadığı ölçülmedi; uygulanıyorsa satır küçülür.
- OYA-24: Perde düzeltmesinin açık olduğu mpv varsayılanından çıkarım (`--audio-pitch-correction`, options.rst:2056). Perdeyi hızdan ayrı kaydıracak süzgecin derlemede olup olmadığı bilinmiyor.
- OYA-21, OYA-22: Kod küçük, ama deneme için ses alıcısı (Dolby ve DTS çözen ev sineması cihazı) ve özel kipi destekleyen aygıt gerekir; bu makinede ölçülemez.
- OYA-46: libmpv derlemesinde WebP, JPEG XL ve AVIF kodlayıcısı var mı bilinmiyor.
- OYA-15: mpv'nin HDR10+ ve tepe parlaklık için verdiği özellik derlemede denenmedi.
- OYA-37: Hazır sunan oynatıcı bulunamadı; öncelik yalnız kullanıcı oyuna dayanıyor.
- OYA-50: VLC kaynağı [9] açılmadı; IINA `pauseWhenMinimized` görüldü (Preference.swift:86).
- Kimde Var sütununda yalnız alt ajan okumasına dayanan adlar (Haruna, Elmedia, Movist Pro, Celluloid, SMPlayer, Clapper, ImPlay, GOM Player) bu turda açılmadı. Hiçbir satırın önceliği yalnız bu adlara dayanmıyor.
- PotPlayer ve Infuse sürüm günlüklerinin yalnız ilk 100.000 karakteri okundu.

## Ek Notlar

- Kodda bilinçli karar: oynatıcıda çift tık ayrı hareket sayılmıyor, sol tık işini bırakışta yapıyor (PlayerView.Fare.cs:45). OYA-02 ve OYA-38 bu kararı değiştirir; sahibe bu bilgiyle sorulmalı.
- Sol tık testi kararsız (acik.md:107; docs/olcumler/soltik-kararsiz.md). Fare işine giren paket bu testi yeniden kırabilir.
- OYA-09 ile ilgili açık kusur: ffprobe yolu Türkçe bölüm adını bozuk okuyor (acik.md:14). Bölüm adı mpv'den okunursa bu kusura takılmaz.
- OYA-36 kardeş tabloda öncelik 1, burada 2 gelmişti; iki tablo aynı eksiğe farklı öncelik vermişti.
- OYA-P1 üç küçük satırın toplamıdır; bir günü biraz aşabilir, yine de sekmenin en ucuz paketidir. Öbür on bir paket `orta`.
- OYA-P5 ve OYA-P9 aynı dosyaya yazar (`PlaybackHistory.cs`); kardeş kolun OYB-P5 paketi de aynı dosyaya dokunuyor, aynı anda yapılırsa çakışır.
- Kırpma sınırı açılıp okundu: en çok yüzde 20 (src/VidShrink.Player/AdvancedSettings.cs:17, 31); OYA-47 satırındaki yol kısaltılmış yazılmış, dosya `VidShrink.Player` altında.
- Bu turda açılan sayfa ve dosyalarda bana yönelik yazılmış yönerge görülmedi.

Öncelik 1 satırlarının bu turda doğrudan açılan kaynakları (satır numaraları 13 Ekim 2026 ana dal içindir):

- `OYA-01`: mpv `--stop-screensaver` (options.rst:3815); VLC `disable-screensaver` (libvlc-module.c:1702); IINA `SleepPreventer.swift:43`.
- `OYA-02`: mpv çift tık tam ekran bağı (input.conf:32); IINA `doubleClickAction` (Preference.swift:293); PotPlayer güncelleme günlüğü, 260819 kaydı (çift tık eylemi ayarı).
- `OYA-03`: mpv `--audio-device` (options.rst:2075) ve aygıt seçme bağı (input.conf:195); VLC `key-audiodevice-cycle` (libvlc-module.c:2677); IINA `audioDevice` (Preference.swift:209).
- `OYA-04`: mpv `--alang`, `--slang` (options.rst:7, 23); VLC `audio-language`, `sub-language` (libvlc-module.c:1867, 1870); IINA `audioLanguage`, `subLang` (Preference.swift:200, 249).
- `OYA-06`: VLC `volume-save` (libvlc-module.c:1620); mpv.net `--remember-volume` (manual.md:377); IINA `softVolume` (Preference.swift:58).
- `OYA-07`: mpv `--osd-level` (options.rst:4925); VLC `osd` (libvlc-module.c:1808); IINA `enableOSD` (Preference.swift:132).
- `OYA-09`: mpv bölüm seçme bağı (input.conf:191); IINA 1.5.0 sürüm notu (bölüm adı zaman çubuğunda; 3 Ekim 2026); Screenbox v0.20.0 (seçilebilir bölüm listesi).
- `OYA-11`: mpv `--tone-mapping` (options.rst:7675); IINA `enableToneMapping` (Preference.swift:189); MPC-HC 2.8.3 sürüm notu (HLG ton eşleme); PotPlayer 250625 kaydı; Infuse 7.3 sürüm notu.
- `OYA-29`: mpv `--sub-file-paths` (options.rst:3006); VLC `sub-autodetect-path` (libvlc-module.c:1826); IINA `subAutoLoadSearchPath` (Preference.swift:228).
- `OYA-36`: mpv `--watch-later-options` (options.rst:1161); IINA `resumeLastPosition` (Preference.swift:78); Screenbox v0.15.0 sürüm notu.
- `OYA-37`: mpv istek kaydı 6797 açık, 40 artı oy; VLC istek kaydı 20850 açık, 4 oy. Hazır sunan rakip yok.
- `OYA-38`: MPC-BE 1.9.1 sürüm notu (sol fare tuşuna uzun basınca hızlandırma); PotPlayer 241015 kaydı; Infuse 8.0.5 sürüm notu; Screenbox v0.19.0 sürüm notu.

## Sekme Özeti

- Çekirdek oynatma sağlam: iz seçimi, gecikme, A-B tekrarı, kare kare ilerleme, yer imi, bölüm atlama, önizleme küçük resmi, görüntü ve ses süzgeçleri var. Eksiklerin çoğu rakiplerde hazır gelen rahatlık davranışları.
- 55 satırın 9'u önceki kararla kapalı (8 satır K4, 1 satır K5). Kalan 46 satırın 12'si öncelik 1, 17'si öncelik 2.
- En ağır üç eksik: HDR videonun sıradan ekranda doğru görünmesi (OYA-11), ses ve altyazı dil tercihi (OYA-04), uyku ve ekran kararması engeli (OYA-01).
- En ucuz üç kazanç: ses düzeyini hatırlama (OYA-06), ekran üstü bildirimin kapsamı (OYA-07; rozet hazır, yalnız çağrılmıyor), alt klasörde altyazı arama (OYA-29).
- Araştırmanın `küçük` dediği 40 işin 7'si `orta` çıktı; 33'ü küçük kaldı.
- Fareye dokunan iki satır (OYA-02, OYA-38) koddaki bilinçli bir karara ve kararsız bir teste denk geliyor; sahibin kararı olmadan başlanmamalı.
- On iki paketin dördü sahibin kararını bekliyor (P5, P6, P7, P8); sekizi karar beklemeden yapılabilir.

## Paket Önerisi

### OYA-P1 — Bildirim, Ses Düzeyi Ve Bölüm Adları
- Satırlar: OYA-06, OYA-07, OYA-09
- İş: küçük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.axaml.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.App/Playback/PlayerView.Gosterge.cs, src/VidShrink.App/Playback/PlayerSettings.cs, src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.Player/MpvEngine.cs, src/VidShrink.Player/IPlaybackEngine.cs
- Plan: Ses düzeyi ve sessiz durumu `PlayerSettings`'e iki alan olarak yazılır, açılışta okunur. Hazır duran `ShowOsd` rozeti (PlayerView.Gosterge.cs:72) iz değişimi, gecikme, A-B noktası, bölüm atlama ve kare kaydı sonucunda da çağrılır; bugün bu olaylar yalnız izleme günlüğüne düşüyor. Bölüm adı mpv'nin `chapter-list/N/title` özelliğinden okunur, zaman çubuğu işaretinde ve bölüm atlama bildiriminde gösterilir, sağ tık menüsüne bölüm listesi eklenir. Doğrulama: `OynaticiGorunumTests` ve `OynaticiParcaTests` içinde rozet metni, ayar gidiş-dönüşü ve bölüm adı okuma.

### OYA-P2 — Altyazı Bulma Ve Tuşları
- Satırlar: OYA-25, OYA-26, OYA-29, OYA-30, OYA-32
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Playback/SubtitleOptions.cs, src/VidShrink.App/Playback/Keymap.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.Player/MpvEngine.cs, src/VidShrink.Player/IPlaybackEngine.cs
- Plan: `SubtitleOptions.Sidecars` videonun klasörüne ek olarak `Subs`, `Subtitles` gibi alt klasörlere de bakar; uzantı listesine `.sup` ve `.idx` eklenir. Motora üç küçük üye eklenir: `sub-visibility` (izi değiştirmeden gizle), `sub-seek` ile `sub-step` (önceki ya da sonraki satıra git, altyazıyı bir satır kaydır) ve `sub-text` (ekrandaki satırı okuyup panoya yaz). Her biri `Keymap`'e yeni eylem ve tuş olarak girer. Doğrulama: geçici klasörde alt klasör taraması testi; örnek SRT ile satır atlamanın konumu değiştirdiği ve gizlemenin iz numarasını değiştirmediği motor testi.

### OYA-P3 — Uyku Engeli Ve Pencere Davranışı
- Satırlar: OYA-01, OYA-08, OYA-49, OYA-50
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.axaml.cs, src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.App/Playback/PlayerView.Fare.cs, src/VidShrink.App/MainWindow.axaml.cs, src/VidShrink.App/Playback/
- Plan: Oynatma sürerken ekranın kapanmasını engelleyen küçük bir sınıf yazılır: Windows'ta `SetThreadExecutionState`, macOS'ta `IOPMAssertion`, Linux'ta masaüstünün ekran koruyucu engelleme isteği; duraklayınca ve kapanınca engel kalkar. Linux kolu yeni paket isterse yalnız Windows ve macOS teslim edilir, Linux sahibe sorulur. Aynı oynat ya da duraklat olayına üç küçük davranış bağlanır: fare durunca imleci gizleme, pencere simge durumuna inince duraklatma (ayar, kapalı gelir) ve yalnız oynatırken üstte tutma seçeneği. Doğrulama: engelin oynatmada alınıp duraklamada bırakıldığını gösteren sahte sağlayıcılı test, Windows'ta `powercfg /requests` çıktısıyla elle ölçüm.

### OYA-P4 — Ses Aygıtı Ve Çıkış Seçenekleri
- Satırlar: OYA-03, OYA-21, OYA-22, OYA-23
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Player/MpvEngine.cs, src/VidShrink.Player/IPlaybackEngine.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.App/Playback/PlayerSettings.cs, src/VidShrink.App/Playback/PlayerAdvancedPanel.axaml.cs
- Plan: Motor aygıtları mpv'nin `audio-device-list` özelliğinden okur, seçim `audio-device` ile yazılır; sağ tık menüsüne ses çıkışı alt menüsü eklenir, seçim `PlayerSettings`'te saklanır, aygıt yoksa otomatiğe dönülür. Gelişmiş panele üç anahtar eklenir: `audio-spdif` (Dolby ve DTS'i çözmeden alıcıya gönderme), `audio-exclusive` (özel kip) ve `audio-channels` (stereoya indirme); üçü de kapalı gelir, bugünkü ses yolu değişmez. Doğrulama: motor testinde listenin okunması ve seçeneğin geri okunması. Passthrough ve özel kip bu makinede denenemez; alıcısı olan bir kurulumda elle ölçüm gerekir.

### OYA-P5 — Açılış Varsayılanları
- Satırlar: OYA-04, OYA-31, OYA-36, OYA-51
- İş: orta
- Karar: sahip: dosya açılırken dört varsayılan değişsin mi: tercih edilen dilde ses ve altyazı izi seçimi, zorunlu altyazının kendiliğinden açılması, devamdaki son beş dosya sınırının kalkması ve deinterlace'in otomatik olması
- Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.App/Playback/PlayerSettings.cs, src/VidShrink.App/Playback/PlaybackHistory.cs, src/VidShrink.Player/MpvEngine.cs, src/VidShrink.Player/MpvEngine.Advanced.cs
- Plan: Ayarlara ses ve altyazı için dil tercihi eklenir; `ApplyTrackOptions` açılışta iz listesindeki dil alanına bakıp eşleşen izi seçer, zorunlu (forced) izi `track-list/N/forced` ile tanır; tercih boşsa bugünkü davranış kalır. Devam sınırı `PlaybackHistory.ResumeRecent` (5) kaldırılır ya da kayıt tavanına (200) çekilir. Deinterlace (taramalı videodaki tarak izini giderme) anahtarı `auto` değerini de alır. Sahibin onaylamadığı satır paketten düşer; doğrulama çok dilli örnekle iz seçimi, altıncı dosyada `ResumeFor` ve taramalı örnekte `deinterlace` geri okuması testleridir.

### OYA-P6 — Çift Tık Ve Basılı Tutma
- Satırlar: OYA-02, OYA-38
- İş: orta
- Karar: sahip: sol tık üç anlam taşısın mı: tek tık oynat ya da duraklat (bugünkü), çift tık tam ekran, basılı tutunca bırakana dek hızlı oynatma
- Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.Fare.cs, src/VidShrink.App/Playback/Keymap.cs, src/VidShrink.App/Playback/PlayerInputMap.cs, src/VidShrink.App/Playback/PlayerShortcutsPanel.axaml.cs
- Plan: Bugün sol tık işini bırakışta yapıyor ve çift tık bilerek ayrı hareket sayılmıyor (PlayerView.Fare.cs:45, 88–94). `PlayerInputKind`'e çift basış ve basılı tutma türleri eklenir: ikinci tık ilk tıkın etkisini geri alıp tam ekrana geçer; basış bekleme eşiğini aşarsa hız geçici olarak yükselir, bırakınca eski hıza döner ve tık sayılmaz. Eşik süresi tema belirtecinden okunur, sürükleme eşiği korunur. Doğrulama: `FarePress` ve `FareRelease` üstünden zamanlı görünüm testleri; kararsız `SolTikOynatmayiDegistirmez` testi bu paketle birlikte ele alınır.

### OYA-P7 — HDR Ton Eşleme
- Satırlar: OYA-11
- İş: orta
- Karar: sahip: HDR video işlemcide ton eşlemeyle gösterilsin mi; 4K'da kare düşürürse yalnız elle açılan bir seçenek mi olsun
- Dokunacağı yer: tools/VidShrink.Bench/, docs/olcumler/, src/VidShrink.Player/MpvEngine.Advanced.cs, src/VidShrink.App/Playback/PlayerAdvancedPanel.axaml.cs
- Plan: Önce ölçüm: bir HDR10 ve bir HLG örnek bugünkü yolda açılıp kare kaydedilir, soluk ya da patlak görünüp görünmediği `docs/olcumler/`e yazılır. Sonra etiketli bir `vf` halkası (ffmpeg `zscale` ve `tonemap` süzgeçleri) eklenir; halka yalnız aktarım eğrisi PQ ya da HLG iken takılır (MediaDetails.cs:26–38). Süzgeçler libmpv derlemesinde yoksa paket burada durur ve sahibe döner. Doğrulama: aynı karede eşlemeli ve eşlemesiz ortalama parlaklık farkı; 1080p ve 4K'da `Stats` ile düşen kare sayısı.

### OYA-P8 — Sessiz Bölümleri Atlama
- Satırlar: OYA-37
- İş: orta
- Karar: sahip: sessiz yerleri hızlandıran yeni bir oynatma kipi eklensin mi; hazır sunan rakip bulunamadı, istek yalnız oyla kanıtlı
- Dokunacağı yer: src/VidShrink.Player/MpvEngine.Advanced.cs, src/VidShrink.Player/IPlaybackEngine.cs, src/VidShrink.App/Playback/PlayerView.axaml.cs, src/VidShrink.App/Playback/Keymap.cs
- Plan: Etiketli bir `af` halkası olarak ffmpeg `silencedetect` süzgeci takılır; motor süzgecin bildirdiği sessizlik başlangıcında hızı yükseltir, bitişinde eski hıza döner. Atlamak yerine hızlandırmak ses ile görüntünün eşliğini bozmaz. Kip bir anahtarla açılır ve kapalı gelir; eşik ile en kısa sessizlik süresi ayardır. Doğrulama: ortasında bilinen uzunlukta sessizlik olan üretilmiş örnekte oynatma süresinin kısaldığı ve konuşmalı kısımda hızın değişmediği motor testi.

### OYA-P9 — Dosya Başına Hatırlama Ve Gezinme
- Satırlar: OYA-05, OYA-10, OYA-55
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Playback/PlaybackHistory.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.App/Playback/Keymap.cs, src/VidShrink.App/Playback/PlayerSettings.cs, src/VidShrink.App/Playback/PlayerView.Serit.cs
- Plan: `PlaybackHistory.Entry`'ye ses izi, altyazı izi, ikincil altyazı, gecikmeler ve hız eklenir; `ApplyTrackOptions` bugün her açılışta sıfırladığı bu değerleri kayıttan geri yükler. Zamana git için küçük bir giriş kutusu ve tuşu eklenir; yazılan saat `_seek.GoTo` ile uygulanır. Atlama adımları (1, 10, 60 ve 300 sn) sabit olmaktan çıkıp `PlayerSettings`'ten okunur; `Keymap` satırları ve şerit düğmeleri aynı değeri kullanır, varsayılanlar değişmez. Doğrulama: kayıt gidiş-dönüş testi, eski kayıt dosyasının okunabildiği test, ayarlı adımla atlama testi.

### OYA-P10 — Altyazı Biçemi Ve Kare Kaydı
- Satırlar: OYA-27, OYA-33, OYA-34, OYA-43
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Player/MpvEngine.Advanced.cs, src/VidShrink.Player/MpvEngine.cs, src/VidShrink.App/Playback/PlayerView.Advanced.cs, src/VidShrink.App/Playback/PlayerAdvancedPanel.axaml.cs, src/VidShrink.App/Playback/PlayerSettings.cs
- Plan: Önce ölçüm: biçemli (ASS) bir altyazıda bugünkü yazı tipi ve renk ayarının uygulanıp uygulanmadığı görülür; uygulanmıyorsa `sub-ass-override` anahtarı eklenir. Yazı tipi listesi üç genel aileden sistem yazı tiplerine genişler, `sub-filter-sdh` ile işitme engelli notlarını gizleme anahtarı gelir. Kare kaydına altyazıyla kaydetme seçeneği eklenir: `screenshot-to-file` komutunun son değeri `video` yerine `subtitles` olur (MpvEngine.cs:538). Doğrulama: ASS örneğinde biçem geri okuması, altyazılı ve altyazısız karenin piksel farkı.

### OYA-P11 — Doldurma Ve Dokunmatik Hareketler
- Satırlar: OYA-47, OYA-53
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.axaml.cs, src/VidShrink.App/Playback/ZoomGesture.cs, src/VidShrink.App/Playback/PlayerView.Fare.cs, src/VidShrink.App/Playback/Keymap.cs
- Plan: Görüntüyü pencereye sığdıran ölçek (`Resize`, PlayerView.axaml.cs:854–862) için ikinci bir kip eklenir: doldur kipinde ölçek panoyu tümüyle kaplayacak biçimde seçilir, taşan kısım kırpılır; tek tuşla sığdır ile doldur arasında geçilir. Dokunmatik ekran için işaretçi türü ayrılır: iki parmakla yakınlaştırma, yatay kaydırmayla atlama, dikey kaydırmayla ses. Fare davranışı değişmez. Doğrulama: doldur kipinde kare ölçüsünün panoyu kapladığı görünüm testi; üretilmiş dokunma olaylarıyla hareket testi ve gerçek dokunmatik ekranda elle deneme.

### OYA-P12 — Küçük Resim Tablosu
- Satırlar: OYA-44
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/, src/VidShrink.Ffmpeg/, src/VidShrink.App/Playback/PlayerView.Tools.cs
- Plan: Videodan eşit aralıklı kareler alıp tek resimde dizen bir araç eklenir: ffmpeg `select` ve `tile` süzgeçleriyle tek komut, argümanı `Core` üretir. Oynatıcının araçlar menüsüne küçük resim tablosu girer; satır ve sütun sayısı seçilir, çıktı kare kaydı klasörüne yazılır. Doğrulama: argüman üretimi için birim testi, kısa örnekte çıkan resmin beklenen ölçüde olduğu test.

## Pakete Girmeyenler

- OYA-12, OYA-13, OYA-14, OYA-16, OYA-17, OYA-18, OYA-19, OYA-20: K4 ile kapalı (oynatıcıda GPU çizim yolu yapılmayacak).
- OYA-35: K5 ile kapalı (otomatik altyazı yapılmayacak).
- OYA-15, OYA-24, OYA-28, OYA-39, OYA-40, OYA-41, OYA-42, OYA-45, OYA-46, OYA-48, OYA-52, OYA-54: öncelik 3 ve hiçbir paketin yanında bedavaya gelmiyor; her biri ayrı arayüz parçası, ayrı ölçüm ya da sahibin kararını ister.
