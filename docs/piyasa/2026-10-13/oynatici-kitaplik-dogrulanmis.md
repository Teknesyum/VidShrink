# Oynatıcı Sekmesi — Liste, Kitaplık, Ağ Ve Kabuk: Doğrulanmış Eksikler

13 Ekim 2026. Doğrulama turu: `oynatici-kitaplik-eksikler.md` tablosunun 47 satırı koddan ve kaynaklardan yeniden ölçüldü; 47 satır kaldı, çıkarılan yok. Kod okuması `origin/main` 9de48d8b; hiçbir şey derlenmedi ve çalıştırılmadı. Kaynak numaraları ve terim açıklamaları (HLS, SMB, UPnP, SMTC, MPRIS, D-Bus ve ötekiler) `oynatici-kitaplik.md` dosyasındadır.

Bu belgede sık geçen dört terim: HLS, yayını küçük parçalara bölüp adresi çoğu zaman `.m3u8` ile biten bir liste dosyasıyla sunan web yayın biçimidir. libmpv, oynatıcının içine gömülü çalışan mpv motorudur. Kabuk fiili, dosyaya sağ tıklayınca çıkan komuttur ("aç", "küçült"). Başsız test, pencere açmadan koşan arayüz testidir.

| No | Eksik | Kimde Var | Bizde Durum | Fayda | İş | Öncelik | Karar | Kaynak |
|---|---|---|---|---|---|---|---|---|
| OYB-01 | Çoklu dosya ve klasör bırakma: oynatıcıya birden çok dosya ya da bir klasör sürüklenince hepsinin kuyruğa girmesi. | mpv, IINA, Celluloid | yarım: yalnız tek dosya kabul edilir; klasör ve çoklu seçim reddedilir (`src/VidShrink.App/Playback/PlayerView.Window.cs:473`) | Bir dizinin bölümleri ya da bir albüm tek hareketle sıraya girer. | küçük | 1 | gerekmez | [1][19][26] |
| OYB-02 | Liste içinde arama: uzun kuyrukta yazarak süzme. | uosc, MPC-HC, MPC-BE, PotPlayer | yok; en yakın kod aramasız liste penceresi (`src/VidShrink.App/Playback/PlayerView.Playlist.cs:94`) | Yüz dosyalık klasörde aranan bölüm kaydırmadan bulunur. | küçük | 1 | gerekmez | [4][15][18][74] |
| OYB-03 | Listede elle sıralama: kuyruktaki ögeyi sürükleyerek ya da tuşla yukarı aşağı taşıma. | uosc, mpv-playlistmanager, Screenbox, Celluloid | yarım: üç işlem var, serbest taşıma yok: "Sıraya al" sona ekler, "Bundan sonra oynat" sıradakine alır, "Listeden kaldır" çıkarır (`src/VidShrink.App/Playback/PlayerView.Kuyruk.cs:53`, `:55`, `:57`) | İzleme sırası listeyi baştan kurmadan değiştirilir. | orta | 2 | gerekmez | [4][5][24][26] |
| OYB-04 | Listeyi ölçüte göre sıralama: ada ya da tarihe göre dizme ve ters çevirme. | MPC-HC, PotPlayer, mpv-playlistmanager | yok; klasör yalnız doğal ad sırasıyla gelir (`src/VidShrink.App/Playback/FolderNavigator.cs:79`) | Kayıt klasöründe en yeni dosya başa alınır. | küçük | 2 | gerekmez | [5][12][18][54] |
| OYB-05 | Liste satırında süre ve izlendi işareti: her ögenin süresini ve nerede kalındığını satırda göstermek. | IINA, PotPlayer | yarım: satırda yalnız ad var (`src/VidShrink.App/Playback/PlayerView.Playlist.cs:106`); kalınan yer depoda duruyor (`src/VidShrink.App/Playback/PlaybackHistory.cs:14`) | Dizide hangi bölümde kalındığı listeye bakınca anlaşılır. | orta | 2 | gerekmez | [18][19] |
| OYB-06 | Listede küçük resim görünümü: liste ögelerini videodan alınmış küçük resimle göstermek. | PotPlayer | yok; liste satırı yalnız metin (`src/VidShrink.App/Playback/PlayerView.Playlist.cs:106`) | Adı anlamsız dosyalar görüntüsünden tanınır. | orta | 3 | gerekmez | [18] |
| OYB-07 | Klasör açma: bir klasörü alt klasörleriyle birlikte kuyruğa alan komut. | mpv, VLC, Screenbox, uosc | yarım: açılan dosyanın klasörü gezilir (`src/VidShrink.App/Playback/FolderNavigator.cs:20`); klasörün kendisi açılamaz, alt klasöre inilmez | Sezon klasörü tek adımda baştan sona oynar. | küçük | 2 | gerekmez | [1][4][9][24] |
| OYB-08 | Kuyruğun kalıcılığı: uygulama yeniden açılınca son kuyruğun ve kalınan ögenin geri gelmesi. | MPC-HC, PotPlayer | yarım: kuyruk bellekte, kapanınca gider (`src/VidShrink.App/Playback/PlayerView.Kuyruk.cs:17`); elle m3u8 kaydı var (`src/VidShrink.App/Playback/PlayerView.ListeKaydet.cs:44`) | Yarım kalan izleme sırası ertesi gün baştan kurulmaz. | orta | 2 | sahip: açılışta eski kuyruğu getirmek varsayılan davranışı değiştirir | [13][18][52] |
| OYB-09 | Adlı kalıcı listeler: uygulama içinde birden çok adlandırılmış liste tutma ve çalan ögeyi listeye ekleme. | Screenbox, PotPlayer, Windows Media Player, Infuse | yarım: liste yalnız m3u8 dosyası olarak dışarı kaydedilir (`src/VidShrink.App/Playback/PlayerView.ListeKaydet.cs:18`) | "Sonra izle" gibi listeler dosya yöneticisine gitmeden tutulur. | büyük | 2 | sahip: kitaplık yönüne açılan yeni yüzey | [18][24][34][38][51] |
| OYB-10 | Oynatma bitince eylem: liste bitince pencereyi kapatma, uygulamadan çıkma ya da bilgisayarı uyutma seçeneği. | MPC-HC | yok; en yakın kod tekrar kipi (`src/VidShrink.App/Playback/PlayerSettings.cs:12`) | Gece izlenen film bitince bilgisayar açık kalmaz. | küçük | 3 | gerekmez | [13] |
| OYB-11 | İzleme geçmişi penceresi: açılan dosyaları tarih sırasıyla gösteren, aranabilen ve oradan yeniden açılabilen liste. | mpv, IINA, MPC-HC, MPC-BE ve 1 rakip daha | yarım: depo 200 kayıt tutuyor ama gösteren arayüz yok (`src/VidShrink.App/Playback/PlaybackHistory.cs:14`); menüde yalnız son 10 dosya (`src/VidShrink.App/Playback/RecentFiles.cs:11`) | Geçen hafta izlenen dosya on kayıtlık menüden düşse de bulunur. | orta | 1 | gerekmez | [1][2][12][15][19][20] |
| OYB-12 | Devam kapsamı: kaldığı yerden devamın yalnız son beş dosyayla sınırlı olmaması. | mpv, MPC-HC, SMPlayer, PotPlayer | yarım: devam yalnız son 5 dosyada (`src/VidShrink.App/Playback/PlaybackHistory.cs:18`, `:107`); adreste devam yok, kayıt dosyanın boyuna ve tarihine bağlı (`docs/plan-url-oynatma.md:60`) | On bölümlük dizide altıncı bölüme geçince ilk bölümün yeri unutulmaz. | küçük | 1 | sahip: sınırı kaldırmak varsayılan davranışı değiştirir | [1][12][13][18][28][53] |
| OYB-13 | Devam ayarı: kaldığı yerden devamı kapatma ya da her açılışta sorma seçeneği. | VLC, mpv, MPC-HC | yarım: devam sormadan ve ayarsız uygulanır (`src/VidShrink.App/Playback/PlayerView.axaml.cs:750`) | Her açılışta baştan başlamasını isteyen bunu kapatabilir. | küçük | 2 | gerekmez | [1][9][13] |
| OYB-14 | Geçmişi kapatma ve temizleme: son açılanları ve izleme kaydını tutmama ya da tek adımda silme. | VLC, MPC-HC | yok; son açılanlar ve geçmiş diske yazılır, temizleme ya da kapatma komutu görülmedi (`src/VidShrink.App/Playback/RecentFiles.cs:11`, `src/VidShrink.App/Playback/PlaybackHistory.cs:14`) | Ortak bilgisayarda ne izlendiği menüde ve diskte kalmaz. | küçük | 2 | gerekmez | [9][12][13] |
| OYB-15 | Adlı yer imi ve yer imi listesi: yer imine ad verme, hepsini listede görme ve tek tek silme. | PotPlayer, IINA (topluluk eklentisiyle) | yarım: adsız zaman işaretleri; ekle, sonraki, önceki (`src/VidShrink.App/Playback/PlaybackHistory.cs:148`, `:158`) | Uzun kayıtta işaretlenen yerler adıyla bulunur, yanlış konan silinir. | orta | 2 | gerekmez | [17][18][20][43] |
| OYB-16 | Sık kullanılanlar: dosya, klasör ya da adresi kalıcı bir favori listesine ekleme. | MPC-HC, Infuse, GOM | yok; en yakın kod son açılanlar menüsü, adres oraya da girmez (`src/VidShrink.App/Playback/PlayerView.Window.cs:181`, `:381`) | Sık açılan klasör ya da yayın adresi her seferinde yeniden bulunmaz. | orta | 2 | gerekmez | [13][34][65] |
| OYB-17 | Yerel kitaplık görünümü: seçilen klasörleri tarayıp video ve müziği ızgarada, klasöre ve albüme göre gösteren ekran. | Screenbox, Windows Media Player, Kodi, MusicBee ve 4 rakip daha | yok | Dosya yöneticisine gitmeden koleksiyonun içinde gezilir. | büyük | 2 | sahip: yeni sekme ve ayrı ürün sınıfı | [23][24][29][36][38] |
| OYB-18 | Poster ve üst veri çekme: dosya adından film ve diziyi tanıyıp afiş, özet ve bölüm bilgisini internetten getirme. | Kodi, Infuse, Plex, Stremio ve 1 rakip daha | yok | Dosya adları yerine afişli, özetli bir raf görülür. | büyük | 3 | sahip: dış servis hesabı ister, ayrı ürün sınıfı | [29][30][31][33][34] |
| OYB-19 | Cihazlar arası izleme eşitlemesi: kalınan yerin ve izlendi bilgisinin başka cihaza taşınması. | Infuse, Stremio, Plex | yok | Bilgisayarda yarım kalan film başka cihazda aynı yerden sürer. | büyük | 3 | sahip: bulut hesabı ya da sunucu gerekir | [31][33][34] |
| OYB-20 | HLS adresi yerel liste yoluna giriyor: `.m3u8` ile biten yayın adresi motora verilmeden disk listesi gibi okunmaya çalışılıyor. | mpv, MPC-BE, PotPlayer | yarım: http ve https adresi açılır ama liste uzantısıyla (m3u8, m3u, pls ve ötekiler) biten adres motora gitmez: liste denetimi yalnız uzantıya bakar (`src/VidShrink.Core/PlaylistFile.cs:20`), adres disk yolu gibi okunmaya çalışılır (`src/VidShrink.Core/PlaylistFile.cs:25`, çağrı `src/VidShrink.App/Playback/PlayerView.Kuyruk.cs:27`, `src/VidShrink.App/Playback/PlayerView.axaml.cs:673`), hata "açılamadı" iletisine döner (`src/VidShrink.App/Playback/PlayerView.Window.cs:255`); sorgulu adres (`.m3u8?…`) bu yola girmez; kod okuması, koşulmadı, adres planı da HLS'i ölçülmedi sayıyor (`docs/plan-url-oynatma.md:61`) | Web'in en yaygın yayın biçimi adres yapıştırınca açılır. | küçük | 1 | gerekmez | [1][15][18] |
| OYB-21 | Akış kalite seçimi: uyarlamalı yayında çözünürlük ya da bit hızı katmanını elle seçme. | mpv, uosc, PotPlayer | yok; adres seçeneksiz motora gider (`src/VidShrink.Player/MpvEngine.cs:125`) | Yavaş bağlantıda düşük, hızlı bağlantıda en yüksek kalite elle seçilir. | orta | 2 | gerekmez | [1][4][18][57] |
| OYB-22 | Site bağlantısı çözme: video sitesinin sayfa adresini yt-dlp (site adresinden akış çıkaran araç) ile çözüp doğrudan oynatma. | mpv, MPC-HC, MPC-BE, IINA ve 4 rakip daha | yok; `ytdl=no` bilerek yazılmış (`src/VidShrink.Player/MpvEngine.cs:162`), adres planı siteden çözmeyi kapsam dışı bırakmış (`docs/plan-url-oynatma.md:58`) | Sayfa adresi yapıştırılınca video tarayıcı açmadan oynar. | orta | 2 | sahip: K1'e komşu; aynı aracı (yt-dlp) kullanır ve kodda bilerek kapatılmış, K1 ise yalnız indirmeyi sayıyor | [1][12][15][20][45] |
| OYB-23 | Kimlik doğrulamalı akış: adres açarken özel başlık, çerez ya da kullanıcı adı verebilme. | mpv, VLC | yarım: adrese yazılan `ad:parola@` bölümü ve sorgu anahtarı motora olduğu gibi gider (`src/VidShrink.Player/MpvEngine.cs:128`), diske yazılmaz (`src/VidShrink.Core/MediaAddress.cs:19`); özel başlık ve çerez verilemez; kod okuması, koşulmadı | Oturum ya da parola isteyen yayınlar açılır. | orta | 3 | sahip: kimlik bilgisi saklama | [1][9][47] |
| OYB-24 | Ağ paylaşımı gezme: SMB, FTP, SFTP, NFS ve WebDAV sunucularına bağlanıp klasörlerinde gezerek oynatma. | VLC, Infuse, PotPlayer, Screenbox | yok; yalnız http, https, rtsp, rtmp, srt, udp adresleri (`src/VidShrink.Core/MediaAddress.cs:15`) | Ev sunucusundaki dosyalar sürücü harfi bağlamadan açılır. | büyük | 2 | sahip: yeni bağımlılık ve parola saklama | [7][10][18][23][34] |
| OYB-25 | UPnP ve DLNA sunucusundan okuma: ağdaki ortam sunucularını bulup içeriğini listeleme ve oynatma. | VLC, Infuse | yok | Ağdaki ortam sunucusunun içeriği oynatıcıdan seçilir. | büyük | 3 | kapalı: K10 — karar DLNA'yı adıyla sayıyor, okuma yönü ayrıca sorulmadı; pazar: VLC 4 geliştirme dalı UPnP sunucu desteği ekliyor, IINA'da 110 oyluk açık istek var | [7][8][34][41][46] |
| OYB-26 | Chromecast ve AirPlay yayını: çalan videoyu televizyona ya da hoparlöre gönderme. | Screenbox, VLC, SMPlayer, Stremio ve 1 rakip daha | yok | Video bilgisayardan televizyona aktarılır. | büyük | 3 | kapalı: K10 — pazar: MPC-HC 2026'da Chromecast ve DLNA yayınını deneme sürümüyle ekliyor, IINA'nın en çok oy alan iki açık isteği bu (527 ve 307) | [7][14][23][28][33][39][40] |
| OYB-27 | İnternet radyosu ve podcast: kayıtlı istasyonlar ve podcast abonelikleri. | MusicBee, foobar2000 | yarım: yayın adresi elle açılır (`src/VidShrink.App/Playback/PlayerView.Tools.cs:490`); kayıtlı istasyon ya da abonelik yok | Sık dinlenen radyo adres aramadan açılır. | orta | 3 | sahip: ürünün işi dışında yeni yüzey | [35][36] |
| OYB-28 | Birlikte izleme: aynı videoyu uzaktaki kişiyle eşzamanlı oynatma. | Plex | yok | Ayrı evlerdeki iki kişi filmi aynı anda izler. | büyük | 3 | sahip: sunucu ya da eşler arası ağ gerekir | [32][49] |
| OYB-29 | SMTC ve odak dışı ortam tuşları: Windows'un ortam denetimi katmanına (SMTC) bağlanıp ses kutucuğunda ve klavye ortam tuşlarında pencere odakta değilken de çal, duraklat, sonraki ve parça adını sunmak. | mpv, MPC-BE, PotPlayer, Screenbox | yarım: ortam tuşları yalnız pencere odaktayken çalışır (`src/VidShrink.App/Playback/Keymap.cs:178`); mpv'nin kendi `media-controls` ve `input-media-keys` seçenekleri libmpv'de varsayılan kapalı ve motor seçeneklerinde yazılı değil (`src/VidShrink.Player/MpvEngine.cs:151`) | Müzik arkada çalarken klavye tuşuyla ve Windows ses kutucuğundan yönetilir. | orta | 1 | sahip: libmpv'nin kendi anahtarı yetmezse Windows SDK bağı gerekir | [1][2][16][18][25] |
| OYB-30 | macOS Now Playing: macOS'un ortam denetimi alanına ve klavye ortam tuşlarına bağlanma. | IINA, mpv | yok | Mac'te klavye tuşları, kulaklık düğmesi ve Denetim Merkezi oynatıcıyı yönetir. | orta | 2 | gerekmez | [3][19][21] |
| OYB-31 | Linux MPRIS: Linux masaüstünün ortam denetimi arayüzüne (MPRIS) bağlanma. | VLC, mpv (mpv-mpris eklentisiyle) | yok | Linux'ta masaüstü ses kutucuğu ve klavye tuşları oynatıcıyı yönetir. | orta | 2 | sahip: D-Bus kitaplığı gerekebilir | [6][7] |
| OYB-32 | Çoklu seçimle açma: dosya yöneticisinde birden çok dosya seçip açınca hepsinin sırayla kuyruğa girmesi. | MPC-HC, VLC | yarım: ikinci açılış yalnız ilk yolu aktarır (`src/VidShrink.App/Integration/ForwardedFiles.cs:22`, `src/VidShrink.App/Program.cs:166`) ve aktarılan yol tek dosya olarak yüklenir (`src/VidShrink.App/MainWindow.SingleInstance.cs:59`); "aç" fiilinde çoklu seçim modeli yazılı değil, "küçült"te yazılı (`src/VidShrink.Core/Setup/ShellRegistration.cs:159`, `:173`); çoklu seçimi kuyruğa çeviren yol görülmedi, koşulmadı | Seçilen beş dosyanın beşi de sırayla oynar. | orta | 2 | gerekmez | [9][12] |
| OYB-33 | Kabukta sıraya ekle fiili: dosya yöneticisinin sağ tık menüsünden çalan oynatıcının kuyruğuna ekleme. | MPC-HC, VLC | yok; sağ tıkta yalnız "aç" ve "küçült" var (`src/VidShrink.App/ShellMenu.cs:22`) | Film çalarken sıradaki dosya oynatmayı kesmeden eklenir. | orta | 2 | gerekmez | [9][13] |
| OYB-34 | Ses ve liste dosyalarında kabuk kaydı: ses ve çalma listesi uzantılarının "Birlikte aç" ve sağ tık menüsünde yer alması. | MPC-HC | yarım: kurucu kaydı ve sağ tık yalnız 24 uzantıda, 23 video ve gif (`src/VidShrink.Core/Setup/ShellRegistration.cs:257`, `src/VidShrink.App/ShellMenu.cs:30`); ses türleri yalnız "Tümünü Ata" kümesinde, orada da ogg, opus, m3u8, pls yok (`src/VidShrink.Core/ShellIntegration.cs:99`) | Müzik dosyası sağ tıkla ya da çift tıkla oynatıcıda açılır. | küçük | 3 | gerekmez | [13] |
| OYB-35 | Yeni pencere ve çoklu örnek seçeneği: ikinci dosyayı ayrı bir oynatıcı penceresinde açabilme. | MPC-HC, VLC | yok; ikinci açılış hep ilk örneğe aktarılır (`src/VidShrink.Core/SingleInstanceChannel.cs:10`, `src/VidShrink.App/Program.cs:163`) | İki video ayrı pencerelerde yan yana izlenir. | orta | 2 | sahip: tek örnek varsayılanına seçenek ekler | [9][13] |
| OYB-36 | Görev çubuğu ilerleme göstergesi: Windows görev çubuğu simgesinde oynatma ilerlemesini göstermek. | mpv | yok | Pencere arkadayken videonun neresinde olunduğu simgeden görülür. | küçük | 3 | gerekmez | [1][13] |
| OYB-37 | Görev çubuğu küçük resim düğmeleri: görev çubuğu önizlemesinde önceki, oynat ve sonraki düğmeleri. | PotPlayer | yok | Pencere öne alınmadan önizlemeden duraklatılır. | küçük | 3 | gerekmez | [18] |
| OYB-38 | Tarayıcıdan oynatıcıda aç: tarayıcı eklentisiyle sayfadaki bağlantıyı oynatıcıya gönderme. | IINA | yok; adres elle yapıştırılır ya da sürüklenir (`src/VidShrink.App/Playback/PlayerView.Tools.cs:508`) | Tarayıcıdaki video bağlantısı tek tıkla oynatıcıda açılır. | orta | 3 | sahip: tarayıcı eklentisi dağıtımı gerekir | [20][22] |
| OYB-39 | Uzaktan denetim: aynı ağdaki telefondan ya da tarayıcıdan oynatmayı yönetme. | VLC, MPC-HC | yok | Koltuktan kalkmadan telefonla duraklatılır ve sarılır. | büyük | 2 | sahip: ağda dinleyen kapı açar | [7][9][12][13] |
| OYB-40 | Çalışan oynatıcıya komut gönderme: komut satırından ya da başka programdan duraklat, sar, sıraya ekle diyebilme. | mpv, IINA, MPC-HC, VLC | yarım: komut satırı yalnız dosya ya da adres açar (`src/VidShrink.App/Program.cs:152`) | Kısayol araçları ve betikler oynatıcıyı yönetebilir. | orta | 2 | sahip: dışa açık denetim yüzeyi | [1][9][13][20][42] |
| OYB-41 | Betik ve eklenti sistemi: kullanıcının ya da topluluğun yazdığı eklentilerle oynatıcıya özellik ekleme. | mpv, IINA, VLC, PotPlayer ve 4 rakip daha | yok; `load-scripts=no` bilerek yazılmış (`src/VidShrink.Player/MpvEngine.cs:161`–`162`) | Eksik kalan özelliği topluluk eklentisi kapatır. | büyük | 2 | sahip: güvenlik ve bakım yükü | [1][7][18][19][20][29] |
| OYB-42 | Dış görünüm: denetimlerin yerleşimini ve biçimini değiştiren kaplamalar. | VLC, PotPlayer, SMPlayer, MusicBee ve 2 rakip daha | yarım: 36 renk paleti var, yerleşim sabit (`src/VidShrink.App/Themes/Palette/`) | Arayüz kişisel zevke göre baştan biçimlenir. | büyük | 3 | sahip: ölçünün yalnız belirteçlerden geldiği tasarım kuralıyla çelişir | [9][18][28][29][36][37] |
| OYB-43 | Kesintisiz çalma: ardışık parçalar arasında boşluk bırakmadan geçiş ve sıradakini önden yükleme. | mpv, foobar2000, MusicBee, PotPlayer | yok; kuyruk motora verilmez, her dosya ayrı yüklenir (`src/VidShrink.App/Playback/PlayerView.Kuyruk.cs:13`) | Canlı albümler parça arasında susmadan çalar. | büyük | 2 | gerekmez | [1][18][35][36][48] |
| OYB-44 | Ses görselleştirici: ses dosyası çalarken spektrum ya da dalga görüntüsü. | VLC, Kodi, PotPlayer, MusicBee ve 1 rakip daha | yok; ses dosyasında kart ve gömülü kapak var (`src/VidShrink.App/Playback/PlayerView.Ses.cs:43`) | Kapaksız müzik çalarken ekran boş kalmaz. | orta | 3 | gerekmez | [7][9][18][29][36] |
| OYB-45 | Şarkı sözü: gömülü ya da `.lrc` dosyasındaki eşzamanlı sözleri çalarken gösterme. | PotPlayer | yok | Şarkı çalarken sözler satır satır izlenir. | orta | 3 | gerekmez | [18][50] |
| OYB-46 | 360° video: küresel videoda fareyle bakış yönünü değiştirme. | VLC, PotPlayer, GOM | yok | 360° kamera kaydı düz ve bozuk değil, çevrilerek izlenir. | büyük | 3 | sahip: K4'e komşu, yazılım çizimiyle ağır olabilir | [10][18][65] |
| OYB-47 | 3D video: yan yana ya da üst alt dizilmiş 3D videoyu tek göze indirme ya da kırmızı-mavi gözlük görüntüsüne çevirme. | PotPlayer, VLC | yok | 3D dosya normal ekranda çift görüntüsüz izlenir. | orta | 3 | gerekmez | [9][17][18] |

## Düzeltmeler

Sekiz satırın hücresi değişti. Hiçbir satırın önceliği değişmedi, hiçbir satır çıkarılmadı.

| No | Ne Değişti | Kanıt |
|---|---|---|
| `OYB-03` | Bizde Durum genişledi. "Yalnız Sonra çal var" eksik sayımdı: sona ekleme, sıradakine alma ve listeden kaldırma üçü de var. Serbest taşıma yine yok, satır kaldı. | `src/VidShrink.App/Playback/PlayerView.Kuyruk.cs:53`, `:55`, `:57`; menü metinleri `src/VidShrink.App/Locales/tr/playback.json:50`–`52` |
| `OYB-12` | Bizde Durum'a iki gönderme eklendi: sınırın uygulandığı satır ve adreste devamın neden olmadığı. | `src/VidShrink.App/Playback/PlaybackHistory.cs:18` (`ResumeRecent = 5`), `:107`; `docs/plan-url-oynatma.md:60` |
| `OYB-20` | Bizde Durum netleşti. Hata yalnız `.m3u8`'de değil, dokuz liste uzantısının hepsinde; sorgulu adres etkilenmiyor; hatanın ekranda nasıl göründüğü eklendi. Araştırma kolunun iddiası doğru çıktı. | `src/VidShrink.Core/PlaylistFile.cs:20`–`26`, `:175`; `src/VidShrink.Core/ShellIntegration.cs:32`–`35`; `src/VidShrink.App/Playback/PlayerView.Kuyruk.cs:27`–`30`; `src/VidShrink.App/Playback/PlayerView.Tools.cs:504`; `src/VidShrink.App/Playback/PlayerView.Window.cs:255`–`258` |
| `OYB-22` | Bizde Durum'a adres planının "kapsam dışı" satırı eklendi, satır göndermesi tek satıra indi; Karar hücresindeki gerekçe açıldı. | `src/VidShrink.Player/MpvEngine.cs:162`; `docs/plan-url-oynatma.md:58` |
| `OYB-23` | Bizde Durum `yok` iken `yarım` oldu: kullanıcı adı ve parola adresin içinde verilebiliyor; özel başlık ve çerez verilemiyor. | `src/VidShrink.Player/MpvEngine.cs:128`–`134`; `src/VidShrink.Core/MediaAddress.cs:19`–`31`; `docs/plan-url-oynatma.md:26`–`29` |
| `OYB-29` | Bizde Durum'a mpv'nin kendi ortam denetimi seçeneklerinin kodda yazılı olmadığı eklendi. | `src/VidShrink.Player/MpvEngine.cs:151`–`169`; mpv kılavuzu `DOCS/man/options.rst:8342`–`8350` ve `:4664` |
| `OYB-32` | Bizde Durum genişledi: eksik iki yerde. "Aç" fiilinde çoklu seçim modeli yazılı değil ve aktarılan yol kuyruğa değil doğrudan yüklemeye gidiyor. | `src/VidShrink.Core/Setup/ShellRegistration.cs:157`–`164`, `:173`; `src/VidShrink.App/MainWindow.SingleInstance.cs:44`–`61` |
| `OYB-34` | Satır göndermesi düzeltildi: `ShellMenu.cs:22` menü anahtarıydı, uzantı listesi `:30`. "24 video uzantısı" yerine "23 video ve gif" yazıldı. | `src/VidShrink.App/ShellMenu.cs:30`–`34`; `src/VidShrink.Core/ShellIntegration.cs:11`–`15` |

## Çıkarılanlar

Çıkarılan satır yok. 47 satırın hiçbiri kodda tam olarak bulunmadı; en yakın üç aday (`OYB-03`, `OYB-07`, `OYB-23`) yarım çıktı ve tabloda kaldı.

| No | Eksik | Neden | Kanıt (dosya:satır) |
|---|---|---|---|

## Yinelenenler

- OYA-36 = OYB-12 — kaldığı yerden devamın son beş dosyayla sınırlı olması. İki tabloda da öncelik 1 (kardeş kol bu turda 2'den 1'e çıkardı) ve sahibin kararını bekliyor. Aynı iş iki pakete girdi: kardeşte OYA-P5 (Açılış Varsayılanları), burada OYB-P5. İş tek sabit (`src/VidShrink.App/Playback/PlaybackHistory.cs:18`), karar tek; hangisi önce yapılırsa satır öteki paketten düşer.
- OYA-52 = OYB-46 — 360° video. İki tabloda da öncelik 3 ve sahibin kararını bekliyor; burada pakete girmedi.
- Yinelenen sayılmadı ama komşu: OYA-05 (dosya başına seçimleri hatırlama; kardeşte OYA-P9) aynı depoya, `PlaybackHistory.cs` dosyasına yazar. OYB-P4, OYB-P5 ve OYB-P8 ile aynı anda yapılırsa aynı dosyada çakışır.
- Kardeş kolun doğrulanmış dosyasına son kez saat 18:17'de bakıldı: 55 satırı da duruyor, OYA-36 ve OYA-52 tabloda; kardeş dosya da aynı iki eşleşmeyi yazmış.

## Doğrulanamayanlar

- OYB-20: hata koşularak görülmedi; bu turda program çalıştırmak yasak. Kod yolu baştan sona okundu, adres planı da HLS'i "ölçülmedi" sayıyor (`docs/plan-url-oynatma.md:61`). Defterde (`.claude/acik.md:80`, `:86`) yalnız genel "adresten oynatma boşlukları" satırı var, HLS adıyla geçmiyor; bu yüzden `defterde` eklenmedi.
- OYB-22: `kapalı: K1` mi, `sahip` mi, kararsız kalındı. K1 adresten indirmeyi sayıyor ve aynı aracı (yt-dlp) anıyor; bu satır indirmeyi değil oynatmayı istiyor. Olduğu gibi `sahip` bırakıldı. Sahip K1'i oynatmayı da kapsar sayarsa satır `kapalı: K1` ve öncelik 3 olur, OYB-P12'den düşer.
- OYB-24: Windows'ta UNC yolu (`\\sunucu\paylaşım` biçimindeki ağ yolu) yerel dosya gibi açılıyor olabilir; koşulmadı. Satır `yok` olarak kaldı.
- OYB-29: mpv'nin `media-controls` seçeneği gömülü kullanımda açılınca çalışır mı, bilinmiyor; kılavuz yalnız varsayılanın kapalı olduğunu söylüyor. Paketin ilk adımı bunu ölçmek.
- OYB-30: aynı soru macOS'ta `input-media-keys` seçeneği için geçerli; bu makinede ölçülemez.
- OYB-32: Explorer çoklu seçimde "aç" fiilini kaç kez çağırıyor (tek süreç mi, dosya başına süreç mi), koşulmadı.
- OYB-42 ve OYB-44: altı ve beş rakipte var, yine de "dar kitle" gerekçesiyle öncelik 3 bırakıldı. Ölçüt yalnız rakip sayısına bakılırsa 2 de diyebilir; kararsız kalındığı için dokunulmadı.
- PotPlayer ayağı bu turda hiçbir satırda yeniden görülmedi: araştırma kolu sürüm günlüğünün yalnız ilk 100 000 karakterini okumuştu, o sınır aynen duruyor. PotPlayer'ın sayıldığı öncelik 1 satırlarda (OYB-02, OYB-12, OYB-20, OYB-29) iki kaynak PotPlayer dışından sağlandı.
- Öncelik 2 ve 3 satırların rakip iddiaları web'den yeniden açılmadı; kural bunu yalnız öncelik 1 için istiyor. Yol üstünde görülenler Ek Notlar'da.
- Araştırma kolunun kendi açık uçları aynen duruyor: Plex, Infuse, Haruna ve Windows Media Player sürüm ayrıntıları ile kodi.tv'nin 403 dönen sayfası.

## Ek Notlar

- Araştırma kolunun üç bulgusu koddan ölçüldü, üçü de doğru çıktı: liste uzantılı adresin yerel liste yoluna girmesi (OYB-20), işletim sistemi ortam denetimine hiçbir bağın olmaması (OYB-29, OYB-30, OYB-31) ve liste ile geçmiş tarafındaki dört öncelik 1 eksik (OYB-01, OYB-02, OYB-11, OYB-12).
- Tabloda olmayan eksik: liste dosyasının içindeki liste uzantılı girdi atılıyor (`src/VidShrink.Core/PlaylistFile.cs:48`). Yani bir `.m3u` dosyasına yazılmış, `.m3u8` ile biten yayın adresi kuyruğa hiç girmez. OYB-20 ile aynı kök; OYB-P1'de birlikte kapanır.
- Küçült kolunda çoklu bırakma toplayıcısı var (`src/VidShrink.Core/DroppedMedia.cs:9`) ama oynatıcı onu olduğu gibi kullanamaz: yalnız küçültme adayı dosyaları alır, alt klasöre inmez ve düz ad sırası verir. OYB-P2 bunu hesaba katar.
- Adres bilerek son dosyalara girmiyor (`docs/plan-url-oynatma.md:59`). Geçmiş penceresi (OYB-11) ve sık kullanılanlar (OYB-16) tasarlanırken bu karar ya korunur ya da açıkça değiştirilir; sorgunun ve parolanın diske yazılmama kuralı (`docs/plan-url-oynatma.md:26`–`29`) her durumda geçerlidir.
- Öncelik 1 satırların kaynak görmeleri. Hepsi bu turda açılıp okundu; satır numaraları okuma anındaki ham dosyadandır (mpv deposunda `DOCS/man/options.rst`, MPC-BE deposunda `docs/Changelog.txt`).
  - `OYB-01`: mpv kılavuzu sürükle-bırak seçeneğinde listeye ekleme ve sıradakine ekleme değerlerini sayıyor (`options.rst:3519`); Celluloid tanıtım dosyası listeye dosya ve adres bırakmayı anlatıyor; IINA 1.5.0 notunda çoklu bırakma düzeltmesi var.
  - `OYB-02`: uosc tanıtım dosyası menülerinin yazarak arandığını söylüyor; MPC-HC sürüm notlarında listede yazarak arama var; MPC-BE günlüğünde liste süzgeç alanı var (`Changelog.txt:81`, sürüm 1.9.1).
  - `OYB-11`: MPC-HC 2.8.0 notu (geçmişte arama, silme, temizleme); mpv kılavuzu izleme geçmişi seçeneği (`options.rst:1203`); IINA 1.5.0 notu (geçmiş penceresi yenilendi); MPC-BE günlüğü (`Changelog.txt:1183`, `:1262`).
  - `OYB-12`: mpv kılavuzu çıkışta konumu kaydetme ve devam seçenekleri (`options.rst:1127`, `:1141`, `:1150`); MPC-HC notları (son dosyalar ve konumları hatırlanır).
  - `OYB-20`: mpv kılavuzu HLS bit hızı seçeneği (`options.rst:5791`); MPC-BE günlüğü, sürüm 1.8.5 (`Changelog.txt:440`): HLS liste dosyası artık sıradan çalma listesi gibi işlenmiyor, olduğu gibi kaynak süzgecine veriliyor. Bu, bizdeki hatayla aynı sınıftan bir hatanın rakipte düzeltilmiş halidir.
  - `OYB-29`: mpv 0.39.0 notu ve kılavuzu (`options.rst:8342`–`8350`); kılavuzun varsayılan cümlesi "Default: yes (except for libmpv)". MPC-BE'de ayrı bir kaynak dosyası (`MediaControls.cpp`) ve günlük satırı (`Changelog.txt:1452`, sürüm 1.6.3); Screenbox'ta SMTC'ye bağlanan kaynak dosyası.
- Yol üstünde görülenler (öncelik 2 ve 3 satırlar, tek kaynak): mpv kılavuzunda klasör kipi (`options.rst:4467`, OYB-07), sıradakini önden yükleme ve kesintisiz ses (`:4424`, `:2373`, OYB-43), dış komut kanalı (`:4599`, OYB-40), özel başlık ve çerez (`:5714`, `:5707`, OYB-23), site çözme biçimi (`:1047`, OYB-22), görev çubuğu ilerlemesi (`:3510`, OYB-36), ortam tuşları (`:4664`, OYB-30). MPC-HC notlarında tarihe göre sıralama (OYB-04), çoklu seçimle açma (OYB-32) ve web üstünden uzaktan denetim (OYB-39). IINA 1.5.0 notunda eklentiler (OYB-41). Celluloid'de listede sürükleyerek sıralama (OYB-03).
- Kapalı karara denk gelen iki satırda (OYB-25, OYB-26) pazar o yöne hareket ediyor; ayrıntı satırların Karar hücresinde. Karar değişmedi, yalnız sahibin gözüne bir kez daha gelsin diye not edildi.
- Okunan web sayfalarında ve dosyalarda bana yönelik yazılmış bir yönerge görülmedi.

## Sekme Özeti

- Neredeyiz: temel taşlar yerinde. Bellekte kuyruk, klasör gezintisi, dokuz liste biçimini okuma, m3u8 kaydı, son dosyalar, yer imi ve adres açma var.
- Pazarın gerisinde kaldığımız üç yer: listeyi yönetmek, geçmişi göstermek ve işletim sisteminin ortam denetimine bağlanmak.
- En ağır eksik OYB-20: liste uzantısıyla biten yayın adresi açılmıyor. Bu bir eksik değil, hata.
- İkinci ve üçüncü ağır eksik: OYB-29 (pencere arkadayken ortam tuşları ve Windows ses kutucuğu çalışmıyor) ve OYB-11 ile OYB-12 (200 kayıtlık geçmiş tutuluyor ama gösterilmiyor, devam son beş dosyayla sınırlı).
- En ucuz üç kazanç: OYB-20 (tek denetimle kapanır), OYB-12 (tek sabit, yalnız sahibin kararını bekliyor) ve OYB-01 (bırakmadaki tek dosya şartını kaldırmak).
- On iki paket önerildi: sekizi karar beklemeden başlayabilir, dördü sahibin kararını bekliyor.
- Kitaplık, ağ paylaşımı, eklenti ve uzaktan denetim satırları ürünün yönünü değiştirir; iki karar paketinde toplandı (OYB-P11, OYB-P12). Kapalı karara denk gelen iki satır (OYB-25, OYB-26) pakete girmedi.

## Paket Önerisi

On iki paket; sıra önerilen yapım sırasıdır. Karar bekleyen dört paket (OYB-P5, OYB-P6, OYB-P11, OYB-P12) sıradaki yerini korur ama karar gelene kadar başlamaz; ötekiler onları beklemez.

### OYB-P1 — HLS Ve Liste Adresi Düzeltmesi
- Satırlar: OYB-20
- İş: küçük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.Kuyruk.cs, src/VidShrink.Core/PlaylistFile.cs, tests/VidShrink.Tests/OynaticiAdresTests.cs
- Plan: Kuyruk çözümü (`ResolveQueue`) yolu liste saymadan önce uzak adres mi diye bakar; adres ise liste yoluna girmeden motora olduğu gibi verilir. Liste dosyasının içindeki liste uzantılı adres girdisi de artık atılmaz (`PlaylistFile.cs:48`). Önce düşen bir test yazılır (`.m3u8` ve `.pls` ile biten adreste kuyruk kurulmadığı, motora adresin gittiği), sonra yerel bir http sunucusundan gerçek HLS adresi açılarak görülür. HLS olmayan uzak liste (örneğin radyo `.pls` adresi) motorun kendi listesine düşer; arayüzün bunu nasıl gösterdiği aynı pakette ölçülür.

### OYB-P2 — Çoklu Bırakma Ve Klasör Açma
- Satırlar: OYB-01, OYB-07
- İş: küçük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.App/Playback/PlayerView.Kuyruk.cs, src/VidShrink.App/Playback/FolderNavigator.cs, src/VidShrink.App/Locales/
- Plan: Bırakmadaki tek dosya şartı kalkar (`PlayerView.Window.cs:473`); bırakılan dosyalar ve klasörlerin içindeki oynatılabilir dosyalar, alt klasörlerle birlikte, doğal ad sırasıyla kuyruğa yazılır ve ilki çalar. Aynı toplayıcıya menüden bir "Klasör aç" komutu bağlanır. Küçült kolundaki toplayıcı (`src/VidShrink.Core/DroppedMedia.cs:9`) örnek alınır ama olduğu gibi kullanılmaz: yalnız küçültme adaylarını alır ve alt klasöre inmez. Başsız testle kuyruğun sayısı ve sırası ölçülür.

### OYB-P3 — Liste Penceresinde Arama Ve Sıralama
- Satırlar: OYB-02, OYB-03, OYB-04, OYB-05
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Playback/PlayerView.Playlist.cs, src/VidShrink.App/Playback/PlayerView.ListeMenusu.cs, src/VidShrink.App/Playback/PlayerView.Kuyruk.cs, src/VidShrink.App/Playback/FolderNavigator.cs, src/VidShrink.App/Locales/
- Plan: Liste penceresine yazdıkça süzen bir arama kutusu ve ada ya da tarihe göre dizme (ters çevirmeyle) eklenir; ikisi de küçüktür ve önce teslim edilir. Ardından kuyruk düzenleme işlemlerine (`QueueEdit`) yukarı ve aşağı taşıma, satıra da geçmiş deposunda kayıtlı süre ile kalınan yer eklenir. Elle dizilmiş kuyruğun karıştırma kipinden etkilenmeme kuralı (`_queueArranged`) korunur. Başsız testle süzme, sıra ve taşıma ölçülür.

### OYB-P4 — Geçmiş Penceresi Ve Devam Ayarı
- Satırlar: OYB-11, OYB-13, OYB-14
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Playback/PlaybackHistory.cs, src/VidShrink.App/Playback/RecentFiles.cs, src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.App/Playback/PlayerSettings.cs, src/VidShrink.App/Locales/
- Plan: Zaten tutulan 200 kayıtlık geçmiş deposunun üstüne tarih sırasıyla gösteren, yazarak aranan ve seçilen dosyayı açan bir pencere konur. Son dosyalar menüsüne "Geçmiş…" ve "Geçmişi temizle" satırları, ayarlara "geçmişi tutma" ve "kaldığı yerden devam et" anahtarları eklenir; varsayılanlar bugünkü davranışla aynı kalır. Adresin geçmişe girmeme kararı (`docs/plan-url-oynatma.md:59`) korunur. Başsız testle liste, arama, temizleme ve ayar kapalıyken diske yazılmadığı ölçülür.

### OYB-P5 — Devam Kapsamı Ve Kalıcı Kuyruk
- Satırlar: OYB-08, OYB-12
- İş: orta
- Karar: sahip: kaldığı yerden devam son beş dosyayla sınırlı kalsın mı ve uygulama açılınca son kuyruk geri gelsin mi
- Dokunacağı yer: src/VidShrink.App/Playback/PlaybackHistory.cs, src/VidShrink.App/Playback/PlayerView.Kuyruk.cs, src/VidShrink.App/Playback/PlayerSettings.cs
- Plan: Devam sınırı tek sabittir (`PlaybackHistory.cs:18`); karar "kalksın" olursa sabit deponun sığasına (200 kayıt) çekilir ve sınırı ölçen testler birlikte güncellenir. Kuyruk için kapanışta kuyruk ve çalan öge diske yazılır, açılışta geri yüklenir; diskte artık olmayan dosyalar atılır. İki soru birbirinden bağımsızdır: biri "evet" alırsa o yarı tek başına yapılır. Kardeş tablodaki OYA-36 aynı iştir ve orada OYA-P5 paketine girmiştir; iş bir kez yapılır.

### OYB-P6 — Windows Ortam Denetimi Bağlantısı
- Satırlar: OYB-29
- İş: orta
- Karar: sahip: libmpv'nin kendi ortam denetimi anahtarı yetmezse Windows SDK bağı eklensin mi
- Dokunacağı yer: src/VidShrink.Player/MpvEngine.cs, src/VidShrink.App/Playback/Keymap.cs, src/VidShrink.App/Playback/PlayerView.axaml.cs
- Plan: İlk adım karar istemez: motor seçeneklerine `media-controls` yazılır ve gerçek pencerede Windows ses kutucuğunda görünüp görünmediği, ortam tuşlarının pencere arkadayken çalışıp çalışmadığı ölçülür. Yetmezse pencere Windows'un ortam denetimi arayüzüne doğrudan bağlanır (MPC-BE'nin yolu); bu yeni bir Windows SDK bağı demektir ve sahibin kararını ister. İki yolda da sonraki ve önceki tuşu uygulamanın kendi kuyruğuna bağlanmalıdır, çünkü kuyruk motora verilmiyor.

### OYB-P7 — Kabukta Çoklu Açma Ve Sıraya Ekleme
- Satırlar: OYB-32, OYB-33, OYB-34
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Program.cs, src/VidShrink.App/Integration/ForwardedFiles.cs, src/VidShrink.App/MainWindow.SingleInstance.cs, src/VidShrink.Core/Setup/ShellRegistration.cs, src/VidShrink.App/ShellMenu.cs, Install-VidShrink.ps1, tests/VidShrink.Tests/KabukMenusuTests.cs
- Plan: İkinci örnek artık bütün yolları çalışan örneğe aktarır; gelen yollar OYB-P2'nin toplayıcısıyla, yeni açılış yerine kuyruğa eklenir. Sağ tık menüsüne "Sıraya ekle" fiili ve onu taşıyan bir komut satırı bayrağı gelir; bayrak kuyruğa ekleme işlemine (`Enqueue`) bağlanır. Ses ve liste uzantıları yalnız "aç" ve "sıraya ekle" fiillerine eklenir, "küçült" fiiline eklenmez. Kanal ve kabuk kaydı testleriyle, sonra Explorer'da elle çoklu seçimle doğrulanır.

### OYB-P8 — Yer İmi Listesi Ve Sık Kullanılanlar
- Satırlar: OYB-15, OYB-16
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.App/Playback/PlaybackHistory.cs, src/VidShrink.App/Playback/Keymap.cs, src/VidShrink.App/Playback/PlayerView.Window.cs, src/VidShrink.App/Locales/
- Plan: Yer imine isteğe bağlı bir ad alanı eklenir (adsız eski kayıt okunmaya devam eder); bir pencere dosyanın yer imlerini listeler, seçilene gider ve tek tek siler. Ayrıca dosya, klasör ve adres tutan kalıcı bir sık kullanılanlar listesi menüye eklenir. Adres saklanırken sorgunun ve parolanın diske yazılmama kuralı (`docs/plan-url-oynatma.md:26`) uygulanır. Başsız testle ekleme, adlandırma, silme ve yeniden açılışta kalıcılık ölçülür.

### OYB-P9 — Mac Ortam Denetimi Bağlantısı
- Satırlar: OYB-30
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Player/MpvEngine.cs, src/VidShrink.App/Playback/
- Plan: mpv'nin ortam tuşları seçeneği (`input-media-keys`) gömülü kullanımda varsayılan kapalıdır; önce macOS'ta açılır ve klavye ortam tuşlarının, Denetim Merkezi'ndeki çalan ortam alanının oynatıcıyı yönetip yönetmediği ölçülür. Yetmezse macOS'un ortam denetimi arayüzüne ayrı bir bağ yazılır. Sonraki ve önceki tuşu yine uygulamanın kuyruğuna bağlanır. Bu iş Windows makinesinde doğrulanamaz; Mac'te koşan bir işçi ister.

### OYB-P10 — Akış Kalitesi Ve Kesintisiz Çalma
- Satırlar: OYB-21, OYB-43
- İş: büyük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Player/MpvEngine.cs, src/VidShrink.Player/IPlaybackEngine.cs, src/VidShrink.App/Playback/PlayerView.Tracks.cs, src/VidShrink.App/Playback/PlayerView.Kuyruk.cs
- Plan: İki adımda teslim edilir ve adımlar ayrı işçilere verilebilir. Önce akış kalitesi (orta): uyarlamalı yayının katmanlarının motorun iz listesinde nasıl göründüğü ölçülür, sonra iz menüsüne kalite seçimi eklenir (video izi değiştirme ya da `hls-bitrate` seçeneği); OYB-P1 bitmeden başlamaz. Sonra kesintisiz çalma (büyük): sıradaki dosyayı önden yükleme (`prefetch-playlist`, `gapless-audio`) kuyruğun motora verilmemesi kararına dokunur, bu yüzden önce o kararın gerekçesi (`PlayerView.Kuyruk.cs:13`) yeniden ölçülür. İki katmanlı yerel bir HLS yayını ve ardışık iki ses dosyasıyla doğrulanır.

### OYB-P11 — Dış Denetim Ve Eklenti Yüzeyleri
- Satırlar: OYB-31, OYB-35, OYB-39, OYB-40, OYB-41
- İş: büyük
- Karar: sahip: oynatıcı dışarıdan yönetilebilen bir yüzey açsın mı (komut kanalı, Linux ortam denetimi, ağdan uzaktan denetim, eklenti) ve tek örnek kuralına "yeni pencere" seçeneği gelsin mi
- Dokunacağı yer: src/VidShrink.App/Program.cs, src/VidShrink.Core/SingleInstanceChannel.cs, src/VidShrink.App/Integration/, src/VidShrink.Player/MpvEngine.cs
- Plan: Bu bir karar paketidir; karar gelince satır başına ayrı işlere bölünür. En ucuz ilk adım çalışan oynatıcıya komut göndermektir (OYB-40): tek örnek kanalı bugün yalnız yol taşıyor, aynı kanala duraklat, sar ve sıraya ekle komutları eklenir; yeni pencere seçeneği (OYB-35) aynı dosyalara dokunur. Linux ortam denetimi (OYB-31) ve ağdan uzaktan denetim (OYB-39) bu komut katmanının üstüne kurulur. Eklenti sistemi (OYB-41) bilerek kapatılmış `load-scripts=no` seçeneğini geri açtığı için en sona kalır.

### OYB-P12 — Kitaplık Ve Ağ Kaynakları
- Satırlar: OYB-09, OYB-17, OYB-22, OYB-24
- İş: büyük
- Karar: sahip: oynatıcı kitaplık ve ağ kaynağı yönüne büyüsün mü (adlı listeler, klasör tarayan kitaplık, site bağlantısı çözme, ağ paylaşımında gezme)
- Dokunacağı yer: src/VidShrink.App/Playback/, src/VidShrink.Core/PlaylistFile.cs, src/VidShrink.Core/MediaAddress.cs, src/VidShrink.Player/MpvEngine.cs
- Plan: Bu da bir karar paketidir; dört satır dört ayrı iştir ve her biri ayrı "evet" ister. En küçük adım adlı listelerdir (OYB-09): bugünkü m3u8 yazma yolu, uygulamanın kendi klasöründe tutulan adlı listelere genişler. Site bağlantısı çözme (OYB-22) kapalı K1 kararıyla aynı aracı kullanır ve bilerek yazılmış `ytdl=no` seçeneğini geri açar; ağ paylaşımı (OYB-24) parola saklama, kitaplık (OYB-17) yeni bir sekme demektir.

## Pakete Girmeyenler

On yedi satır pakete girmedi.

- OYB-25, OYB-26: kapalı karar K10'a denk geliyor; kapalı satır pakete girmez.
- OYB-18, OYB-19, OYB-28: öncelik 3; dış servis hesabı, bulut ya da sunucu ister ve ayrı bir ürün sınıfıdır.
- OYB-23, OYB-27, OYB-38, OYB-42, OYB-46: öncelik 3 ve sahibin kararını bekliyor; hiçbir paketin yanında bedavaya gelmiyor (OYB-46 kardeş tabloda OYA-52).
- OYB-06, OYB-10, OYB-44, OYB-45, OYB-47: öncelik 3 ve karar istemiyor, ama her biri kendi başına yeni bir arayüz parçası; komşu paketlerin dosyalarına dokunmuyor.
- OYB-36, OYB-37: öncelik 3; doğal komşusu OYB-P6 sahibin kararını beklediği için, karar istemeyen bu iki satır oraya karıştırılmadı.
