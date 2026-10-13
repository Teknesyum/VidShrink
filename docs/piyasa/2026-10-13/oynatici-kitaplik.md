# Oynatıcı Sekmesi — Liste, Kitaplık, Ağ Ve Kabuk

13 Ekim 2026. Kod okuması `origin/main` 9de48d8b. Hiçbir şey derlenmedi, koşulmadı, indirilmedi.
Eksik tablosu `oynatici-kitaplik-eksikler.md` dosyasında; bu dosya onun kanıtıdır.

**Terimler.** *libmpv*: oynatıcımızın motoru, mpv oynatıcısının kitaplık hâli. *Kuyruk*: sırayla çalınacak dosyaların listesi.
*m3u8, PLS*: çalma listesi dosya biçimleri. *HLS, DASH*: web'de videoyu küçük parçalar hâlinde yayınlayan iki biçim; HLS adresi `.m3u8` ile biter.
*SMB, NFS, FTP, SFTP, WebDAV*: ağdaki başka bir makinenin dosyalarına erişme protokolleri. *NAS*: evdeki ağ depolama kutusu.
*UNC yolu*: Windows'un `\\sunucu\paylaşım` biçimli ağ yolu. *UPnP, DLNA*: ev ağındaki ortam sunucusunu bulup içeriğini okuma standardı.
*SMTC*: Windows'un ortam denetimi katmanı; ses kutucuğundaki parça adı ve düğmeler buradan gelir. *MPRIS*: aynı işin Linux'taki arayüzü.
*Now Playing*: aynı işin macOS'taki alanı. *ProgID*: Windows'ta bir programın dosya türüne kendini tanıttığı kayıt; "Birlikte aç" listesi buradan dolar.
*Atlama listesi*: görev çubuğu simgesine sağ tıklayınca çıkan son dosyalar listesi. *IPC*: çalışan programa dışarıdan komut gönderme kanalı.
*yt-dlp*: video sitesinin sayfa adresinden akış adresini çıkaran açık kaynak araç. *Kesintisiz çalma*: parçalar arasında boşluk bırakmadan geçiş.
*D-Bus*: Linux'ta programların birbiriyle konuştuğu kanal; MPRIS bunun üstünde çalışır. *ReplayGain*: parçaların ses düzeyini eşitleyen etiket. *`.lrc`*: satırları zaman damgalı şarkı sözü dosyası. *TMDB*: film ve dizi bilgisi veren açık veritabanı.

## Envanter

Her satır kod okumasıdır; hiçbir şey koşulmadı. Yollar depo köküne göredir.

**Eski belge düzeltmesi.** `docs/oynatici/mevcut-envanter.md` eski ffmpeg boru hattını anlatıyor.
"Son açılanlar yok" ve "kaldığı yerden devam yok" satırları bugün yanlış; aşağıdaki liste onların yerine geçer.
Önceki taramanın 9 numaralı kalemi (çalma listesini kaydetme) yapılmış, envantere girdi.

### Çalma Listesi Ve Kuyruk

- Liste dosyası okuma: m3u, m3u8, pls, wpl, zpl, asx, wax, wvx, wmx — `src/VidShrink.Core/ShellIntegration.cs:32`,
  ayrıştırma `src/VidShrink.Core/PlaylistFile.cs:34`. Göreli yol, `file://` ve adres girdisi okunur; iç içe liste atlanır (`PlaylistFile.cs:48`).
- Liste kaydetme: yalnız m3u8, süre ve başlıkla, göreli yol — `PlaylistFile.cs:59`, `:84`;
  arayüz `src/VidShrink.App/Playback/PlayerView.ListeKaydet.cs:18`, `:44`. PLS ve XSPF yazılmaz.
- Kuyruk bellekte durur; "Sıraya ekle", "Sonra çal", "Listeden çıkar" — `src/VidShrink.App/Playback/PlayerView.Kuyruk.cs:53`.
  Öge menüsü: Oynat, Sıraya ekle, Sonra çal, Klasörde göster, Yolu kopyala, Çıkar, Listeyi kaydet — `PlayerView.ListeMenusu.cs:24`.
- Kuyruk motora verilmez, her dosya ayrı yüklenir (`PlayerView.Kuyruk.cs:13`). Uygulama kapanınca kuyruk gider.
- Liste görünümü açılır bir penceredir (`PlaylistPopup`); tıkla ya da Enter ile çalar — `PlayerView.Playlist.cs:128`, `:224`, `:234`.
  Satırda yalnız dosya adı var. Süre, arama kutusu, sürükle-bırak sıralama ve sıralama komutu yok.
- Klasörde sıradaki dosya: aynı türdeki (video ya da ses) kardeş dosyalar, doğal sıralama —
  `src/VidShrink.App/Playback/FolderNavigator.cs:20`, `:79`. Karıştırma `:40`, tekrar kipi `PlayerSettings.cs:12`, adım `FolderNavigator.cs:66`.
  Kısayollar `Keymap.cs:77` çevresinde (karıştır, tekrar, PageUp/PageDown).
- Bırakma: oynatıcı yalnız tek dosya kabul eder; klasör ve çoklu dosya reddedilir — `PlayerView.Window.cs:473`.
  Metin olarak bırakılan adres açılır (`PlayerView.Window.cs:461`).
- "Klasör aç" komutu yok; menüde yalnız "Klasörde göster" var (`PlayerView.Menu.cs:19`–`37`).
  Oynatıcıdaki tek klasör seçici ekran görüntüsü klasörü içindir (`PlayerView.Window.cs:419`–`424`).
- Liste bitince yapılacak eylem (pencereyi kapat, çık, uyut) yok: `after-playback`, `EndAction`, `play-and-exit`, `bitince` aramaları boş.
  Motor son karede bekler (`keep-open=yes`, `src/VidShrink.Player/MpvEngine.cs:155`), sırayı tekrar kipi belirler.
- Son açılanları ya da geçmişi temizleme ve "geçmiş tutma" anahtarı görülmedi (`ClearRecent`, `ClearHistory`, `temizle`, `KeepHistory` aramaları boş).

### Son Açılanlar, Devam, Yer İmi, Geçmiş

- Son açılanlar: 10 kayıt, `player-recent.json` — `src/VidShrink.App/Playback/RecentFiles.cs:11`; menü `PlayerView.Window.cs:381`.
  Adres son açılanlara girmez (`PlayerView.Window.cs:181`); bu `docs/plan-url-oynatma.md` kararıdır.
- Kaldığı yerden devam: yalnız son 5 dosyada (`PlaybackHistory.cs:18`), aynı dosya denetimiyle (boyut, değişme zamanı, süre; `:126`).
  Uygulanış `PlayerView.axaml.cs:750`. Sormadan devam eder; aç/kapa ayarı görülmedi. Adreste devam yok.
- Geçmiş deposu 200 kayıt tutar (`PlaybackHistory.cs:14`) ama geçmişi gösteren bir pencere ya da "izlendi" işareti yok.
- Yer imi: dosya başına adsız zaman listesi; ekle, sonraki, önceki — `PlaybackHistory.cs:148`, `:158`, `PlayerView.axaml.cs:331`, `:351`.
  Çubukta işaret `PlayerView.Window.cs:559`. Ad verme, liste, tek tek silme, dışa aktarma yok. Favori dosya ya da klasör yok.

### Ağ

- "Adres aç" (Ctrl+U), panodan yapıştır (Ctrl+V), tarayıcıdan sürüklenen adres — `PlayerView.Tools.cs:490`, `:508`, `:539`.
  Şemalar: http, https, rtsp, rtmp, srt, udp — `src/VidShrink.Core/MediaAddress.cs:15`, `src/VidShrink.Player/MpvEngine.cs:125`.
  Sorgu ve kullanıcı bilgisi diske yazılmaz (`MediaAddress.cs:19`). Komut satırından adres `src/VidShrink.App/Program.cs:152`.
- Site bağlantısı çözülmez, betik yüklenmez: `ytdl=no`, `load-scripts=no` — `MpvEngine.cs:161`–`162`.
- SMB, FTP, SFTP, NFS, WebDAV, UPnP: kodda iz yok (`smb://`, `webdav`, `upnp`, `dlna`, `ftp://`, `sftp`, `nfs://` aramaları boş).
- Olası kusur (kod okuması, koşulmadı): `.m3u8` ile biten adres yerel liste yoluna giriyor. Adres açma (`PlayerView.Tools.cs:490`)
  tek çağrı noktasından `ResolveQueue`'ya düşer (`PlayerView.axaml.cs:673`); orada adres ayrımı yok (`PlayerView.Kuyruk.cs:27`).
  `PlaylistFile.IsPlaylist` yalnız uzantıya bakar (`PlaylistFile.cs:20`, `:175`), `Read` adresi disk yolu gibi okur (`:23`–`:27`).
  Aynı denetim liste dosyasının içindeki `.m3u8` adres girdilerini "iç içe liste" sayıp atar (`PlaylistFile.cs:48`).
  Açılış ısıtması da aynı yola girer (`AcilisMotoru.cs:24`). Sorgu dizgili adres (`.m3u8?...`) uzantı eşleşmediği için etkilenmez.

### Kabuk Ve Sistem

- Windows "Birlikte aç" kaydı: ProgID, `OpenWithProgids`, `Capabilities`, `RegisteredApplications` —
  `src/VidShrink.Core/Setup/ShellRegistration.cs:261`, `:325`; `src/VidShrink.App/Integration/FileAssociation.cs:17`.
  Kurucunun kaydı yalnız 24 video uzantısını kapsar: bütün döngüler `ShellIntegration.MediaExtensions` üstünde (`ShellRegistration.cs:140`, `:157`, `:257`),
  o küme de yalnız video ve gif (`src/VidShrink.Core/ShellIntegration.cs:11`). Ses ve liste uzantıları yalnız "Tümünü Ata" düğmesinin
  kümesinde (`ShellIntegration.cs:99`); orada ogg, oga, opus, m4b, ac3, ape, wv, weba, m3u8, pls, zpl yok.
- Varsayılan uygulama kayıt defterine yazılmaz; kullanıcı Windows ayar sayfasına götürülür — `Integration/DefaultApp.cs:17`, `:87`.
- Sağ tık: "VidShrink ile aç" ve altı hedefli "Küçült" alt menüsü; Windows 11 üst düzey menüsü dahil —
  `src/VidShrink.App/ShellMenu.cs:22`, `:109`. Yalnız video uzantılarında; "sıraya ekle" fiili yok.
- macOS: `CFBundleDocumentTypes` — `macos-app-bundle.sh:82`. Linux: `.desktop` ve `MimeType=` — `install-vidshrink.sh:30`, `:109`.
- Ortam tuşları yalnız pencere odaktayken çalışır (Avalonia tuş olayı) — `Keymap.cs:178`–`181`.
- Yok (iki ayrı adla arandı, iz yok): SMTC, MPRIS, macOS Now Playing, atlama listesi, görev çubuğu küçük resim düğmeleri,
  görev çubuğu ilerleme göstergesi, Explorer küçük resim sağlayıcısı.
- Tek örnek: ikinci açılış yolunu ilk örneğe aktarıp çıkar — `src/VidShrink.Core/SingleInstanceChannel.cs:10`, `Program.cs:158`.
  Yalnız ilk yol aktarılır (`Integration/ForwardedFiles.cs:22`, `:50`; `Program.cs:166`). Yeni pencere ya da çoklu örnek seçeneği yok.

### Uzaktan Denetim, Betik, Görünüm

- Uzaktan denetim yok: `HttpListener`, `input-ipc-server`, `WebSocket`, `Kestrel` aramaları boş.
- Betik ve eklenti yok (`MpvEngine.cs:161`).
- Görünüm: 36 renk paleti (`src/VidShrink.App/Themes/Palette/`). Yerleşimi değiştiren dış görünüm yok.

### Yalnız Ses, 360° Ve 3D

- 24 ses uzantısı açılır — `ShellIntegration.cs:21`–`25`. Ses kartı: başlık, sanatçı, albüm — `PlayerView.Ses.cs:43`;
  etiket alanları `src/VidShrink.Player/IPlaybackEngine.cs:33`. Gömülü kapak tek kare çizilir — `MpvEngine.cs:167`.
- Yok (iki adla arandı): görselleştirici, şarkı sözü, kesintisiz çalma, ReplayGain.
- Yok: 360° ve 3D (`v360`, `equirect`, `stereo3d`, `spherical`, `anaglyph` aramaları boş).

### Kitaplık

- Klasör tarayan, poster ya da üst veri gösteren kod yok (`MediaLibrary`, `poster`, `tmdb`, `tvdb`, `LibraryScan`, `kitapl` aramaları boş).

## Rakipler

Sürüm ve tarih 13 Ekim 2026'da `gh api` (GitHub'ın komut satırı aracı, yalnız okuma) ve resmi sitelerden okundu.
"doğrulanmadı" yazan hücrede sayı resmi kaynakta görülemedi.

| No | Ad | Sürüm | Tarih | Tür | URL |
|---|---|---|---|---|---|
| 1 | mpv | 0.41.0 | 21 Aralık 2025 | açık kaynak; Windows, macOS, Linux | https://github.com/mpv-player/mpv |
| 2 | uosc (mpv üstüne arayüz) | 5.13.0 | 3 Ağustos 2026 | açık kaynak eklenti | https://github.com/tomasklaen/uosc |
| 3 | VLC | 3.0.24 | 22 Eylül 2026 | açık kaynak; tüm platformlar | https://www.videolan.org/vlc/ |
| 4 | MPC-HC | 2.8.3 | 8 Ekim 2026 | açık kaynak; Windows | https://github.com/clsid2/mpc-hc |
| 5 | MPC-BE | 1.9.2 | 13 Ekim 2026 | açık kaynak; Windows | https://github.com/Aleksoid1978/MPC-BE |
| 6 | PotPlayer | 260819 | 19 Ağustos 2026 | ücretsiz, kapalı kaynak; Windows | https://potplayer.tv/ |
| 7 | IINA | 1.5.0 | 3 Ekim 2026 | açık kaynak; macOS | https://iina.io/ |
| 8 | Screenbox | 0.23.0 | 4 Ekim 2026 | açık kaynak; Windows (Microsoft Store) | https://github.com/huynhsontung/Screenbox |
| 9 | mpv.net | 7.1.2.0 | 9 Ocak 2026 | açık kaynak; Windows | https://github.com/mpvnet-player/mpv.net |
| 10 | SMPlayer | 26.8.29 | 30 Ağustos 2026 | açık kaynak; Windows, Linux, macOS | https://www.smplayer.info/ |
| 11 | Celluloid | 0.30 | 29 Mart 2026 | açık kaynak; Linux (GNOME) | https://github.com/celluloid-player/celluloid |
| 12 | Haruna | doğrulanmadı | doğrulanmadı | açık kaynak; Linux (KDE) | https://github.com/KDE/haruna |
| 13 | ImPlay | 1.5.1 | 30 Kasım 2023 (eski) | açık kaynak; çok platform | https://github.com/tsl0922/ImPlay |
| 14 | Kodi | 21.3 "Omega" | 31 Ekim 2025 | açık kaynak; medya merkezi | https://github.com/xbmc/xbmc |
| 15 | Jellyfin Desktop | 1.12.0 | 20 Mart 2025 | açık kaynak; sunucu istemcisi | https://github.com/jellyfin/jellyfin-desktop |
| 16 | Plex (masaüstü ve HTPC) | doğrulanmadı | doğrulanmadı | ücretsiz ve abonelik; sunucu istemcisi | https://support.plex.tv/ |
| 17 | Stremio | 5.0.26 | 22 Eylül 2026 | ücretsiz; medya merkezi | https://www.stremio.com/ |
| 18 | Infuse | 8 (alt sürüm doğrulanmadı) | doğrulanmadı | ücretli (Pro: 1,99 $/ay, 16,99 $/yıl, 99,99 $ ömür boyu); Apple platformları | https://firecore.com/infuse |
| 19 | foobar2000 | 2.26 | 1 Ekim 2026 | ücretsiz; ses oynatıcı | https://www.foobar2000.org/ |
| 20 | MusicBee | 3.6.9668 | 20 Haziran 2026 | ücretsiz; ses oynatıcı, Windows | https://getmusicbee.com/ |
| 21 | AIMP | 5.40.2727 | 8 Eylül 2026 | ücretsiz; ses oynatıcı | https://www.aimp.ru/ |
| 22 | Windows Media Player (Windows 11) | doğrulanmadı | kaynak 16 Kasım 2021 (eski) | işletim sisteminin kendi aracı | https://blogs.windows.com/windows-insider/2021/11/16/new-media-player-for-windows-11-begins-rolling-out-to-windows-insiders/ |

Tabloya girmeyen yardımcılar: mpv-playlistmanager (sürüm yok, son itme 16 Mart 2026) ve mpv-mpris 1.3 (17 Eylül 2026), ikisi de mpv eklentisi.
GOM Player bu turda yeniden taranmadı; satırlardaki GOM bilgisi iç belgeden gelir (`docs/oynatici/rakip-envanter.md`, 11 Eylül 2026).

VLC 4.0 hâlâ yayımlanmadı: geliştirme dalının sürüm dosyası `4.0.0-dev` yazıyor [8]. O dalda görülen kitaplık, yeni liste ve
yer imi özellikleri bu yüzden "VLC'de var" sayılmadı.

## Özellik Matrisi

Okuma anahtarı: **var** resmi kaynakta görüldü. **ek** eklenti ya da betikle geliyor. **dm** doğrulanmadı: resmi sayfada görülmedi,
"rakipte yok" demek değildir. **—** o platformla ilgisiz. **yok** yalnız bizim sütunumuzda ve rakibin kendi kaynağı öyle dediğinde kullanıldı.
**deneme** yalnız deneme sürümünde var. **kaldırıldı** son sürümde çıkarıldı.

Her satır eksik tablosundaki tek bir satırın kanıtıdır; numarası ayraç içinde. Tablodaki "ve N rakip daha" sayısı yalnız **var** hücrelerini sayar.

| Özellik | Biz | mpv Ve uosc | VLC 3.0 | MPC-HC | MPC-BE | PotPlayer | IINA | Screenbox | Başkaları |
|---|---|---|---|---|---|---|---|---|---|
| Çoklu dosya ve klasör bırakma (OYB-01) | yarım | var | dm | dm | dm | dm | var | dm | Celluloid |
| Liste içinde arama (OYB-02) | yok | var (uosc) | dm | var | var | var | dm | dm | |
| Listede elle sıralama (OYB-03) | yarım | var (uosc, betik) | dm | dm | dm | dm | dm | var | Celluloid |
| Ölçüte göre sıralama (OYB-04) | yok | ek | dm | var | dm | var | dm | dm (sıralama yalnız kitaplık görünümünde görüldü) | |
| Satırda süre ve izlendi işareti (OYB-05) | yarım | dm | dm | dm | dm | var | var | dm | |
| Listede küçük resim görünümü (OYB-06) | yok | dm | dm | dm | dm | var | dm | dm | |
| Klasör açma, alt klasörlerle (OYB-07) | yarım | var | var | dm | dm | dm | dm | var | |
| Kuyruğun kalıcılığı (OYB-08) | yarım | dm | dm | var | dm | var | dm | dm | |
| Adlı kalıcı listeler (OYB-09) | yarım | dm | dm | dm | dm | var | dm | var | Windows Media Player, Infuse |
| Bitince eylem (OYB-10) | yok | dm | dm | var | dm | dm | dm | dm | |
| İzleme geçmişi penceresi (OYB-11) | yarım | var | dm | var | var | var | var | dm | |
| Devam kapsamı, beş dosyayla sınırlı değil (OYB-12) | yarım (son 5) | var | dm | var | dm | var | dm | dm | SMPlayer |
| Devam ayarı, kapat ya da sor (OYB-13) | yarım (ayarsız) | var | var | var | dm | dm | dm | dm | |
| Geçmişi kapatma, temizleme (OYB-14) | yok | dm | var | var | dm | dm | dm | dm | |
| Adlı yer imi (OYB-15) | yarım | dm | dm | dm | dm | var | ek | dm | |
| Sık kullanılanlar (OYB-16) | yok | dm | dm | var | dm | dm | dm | dm | Infuse, GOM |
| Yerel kitaplık (OYB-17) | yok | dm | dm | dm | dm | dm | dm | var | Windows Media Player, Kodi, MusicBee, Infuse, Stremio, Plex, Jellyfin |
| Poster ve üst veri (OYB-18) | yok | dm | dm | dm | dm | dm | dm | dm | Kodi, Infuse, Plex, Stremio, Jellyfin |
| Cihazlar arası izleme eşitlemesi (OYB-19) | yok | dm | dm | dm | dm | dm | dm | dm | Infuse, Stremio, Plex |
| HLS adresi (OYB-20) | yarım (koşulmadı) | var | dm | dm | var | var | dm | dm | |
| Akış kalite seçimi (OYB-21) | yok | var | dm | dm | dm | var | dm | dm | |
| Site bağlantısı çözme (OYB-22) | yok (bilerek) | var | kaldırıldı (3.0.24) | var | var | var | var | dm | SMPlayer, Celluloid, Haruna |
| Kimlik doğrulamalı akış (OYB-23) | yok | var | var | dm | dm | dm | dm | dm | |
| Ağ paylaşımı gezme (OYB-24) | yok | dm | var | dm | dm | var | dm | var | Infuse |
| UPnP ve DLNA okuma (OYB-25) | yok | dm | var | dm | dm | dm | dm | dm | Infuse |
| Chromecast, AirPlay yayını (OYB-26) | yok (K10) | dm | var | deneme | dm | dm | ek | var | SMPlayer, Stremio, Infuse |
| İnternet radyosu ve podcast (OYB-27) | yarım | dm | dm | dm | dm | dm | dm | dm | MusicBee, foobar2000 |
| Birlikte izleme (OYB-28) | yok | dm | dm | dm | dm | dm | dm | dm | Plex (geri çekiliyor) |
| SMTC (OYB-29) | yarım | var | dm | dm | var | var | — | var | |
| macOS Now Playing (OYB-30) | yok | var | dm | — | — | — | var | — | |
| Linux MPRIS (OYB-31) | yok | ek | var | — | — | — | — | — | |
| Çoklu seçimle açma (OYB-32) | yarım | dm | var | var | dm | dm | dm | dm | |
| Kabukta sıraya ekle (OYB-33) | yok | dm | var | var | dm | dm | dm | dm | |
| Ses ve liste dosyalarında kabuk kaydı (OYB-34) | yarım | dm | dm | var | dm | dm | dm | dm | |
| Yeni pencere, çoklu örnek (OYB-35) | yok | dm | var | var | dm | dm | dm | dm | |
| Görev çubuğu ilerleme (OYB-36) | yok | var | dm | dm | dm | dm | — | dm | |
| Küçük resim düğmeleri (OYB-37) | yok | dm | dm | dm | dm | var | — | dm | |
| Tarayıcıdan oynatıcıda aç (OYB-38) | yok | dm | dm | dm | dm | dm | var | dm | |
| Uzaktan denetim (OYB-39) | yok | dm | var | var | dm | dm | dm | dm | |
| Komut gönderme (OYB-40) | yarım | var | var | var | dm | dm | var | dm | |
| Betik ve eklenti (OYB-41) | yok (bilerek) | var | var | dm | dm | var | var | dm | Kodi, foobar2000, MusicBee, AIMP |
| Dış görünüm (OYB-42) | yarım (36 palet) | ek (uosc) | var | dm | dm | var | dm | dm | SMPlayer, MusicBee, AIMP, Kodi |
| Kesintisiz çalma (OYB-43) | yok | var | dm | dm | dm | var | dm | dm | foobar2000, MusicBee |
| Ses görselleştirici (OYB-44) | yok | dm | var | dm | dm | var | dm | dm | Kodi, MusicBee, AIMP |
| Şarkı sözü (OYB-45) | yok | dm | dm | dm | dm | var | dm | yok (açık istek) | |
| 360° video (OYB-46) | yok | dm | var | dm | dm | var | dm | dm | GOM |
| 3D video (OYB-47) | yok | dm | var | dm | dm | var | dm | dm | |

### Kitaplık Ayrı Bir Ürün Sınıfı Mı

Evet. Poster ve üst veri çeken kitaplık "oynatıcı" değil "medya merkezi" sınıfında duruyor. Kodi kendini "entertainment hub" diye
tanıtıyor [29], Stremio "media center" diyor [33]. Plex ve Jellyfin bir sunucunun istemcisi; Plex'te sunucusuz yerel dosya yolu yok [30][31].
Infuse da bu sınıfta ve ücretli [34].

Oynatıcı sınıfında yalnız iki ürün basit bir yerel kitaplık sunuyor: Screenbox ve Windows Media Player [23][24][38]. İkisi de
afiş çekmiyor, klasörü tarayıp ızgarada gösteriyor. VLC bunu 4.0'a yazıyor ama 4.0 çıkmadı [8].

mpv, MPC-HC, MPC-BE, IINA ve PotPlayer'da kitaplık görülmedi (doğrulanmadı). Bu beşi geçmiş ve liste yönetimine yatırım yapıyor.
Sonuç: kitaplık bizim için ayrı bir karar (OYB-17, OYB-18); geçmiş penceresi ve liste yönetimi ise sınıfın olağan işi (OYB-02, OYB-11).

### Öncelik 1 Satırlarının İki Kaynağı

- OYB-01 çoklu bırakma: mpv `--drag-and-drop` (ekle, sıraya koy değerleriyle) [1]; IINA 1.5.0 çoklu ortam bırakma düzeltmesi [19]; Celluloid README [26].
- OYB-02 liste arama: uosc "All menus are instantly searchable" [4]; MPC-HC README [74]; MPC-BE 1.9.1 süzgeç alanları [15]; PotPlayer 260819 [18].
- OYB-11 geçmiş penceresi: mpv `--save-watch-history` [1] ve 0.40.0 notları [2]; MPC-HC 2.8.0 "Show History" [12]; MPC-BE 1.9.1 [15]; IINA 1.5.0 [19].
- OYB-12 devam kapsamı: mpv `--save-position-on-quit`, `--watch-later-dir` [1]; MPC-HC "Remember File position" [13] ve 365 günlük budama [12]; SMPlayer [28]; PotPlayer [18].
- OYB-20 HLS adresi: mpv `--hls-bitrate` [1]; MPC-BE 1.8.5 aynı kusuru düzeltti [15]; PotPlayer 260401 [18].
- OYB-29 SMTC: mpv `--media-controls` [1] ve 0.39.0 notları [2]; MPC-BE kaynak dosyası [16]; PotPlayer 240618 [18]; Screenbox kaynak dosyası [25].

Elle sıralama (OYB-03) bilerek 2'de kaldı: kanıt uosc, bir mpv betiği, Screenbox ve Celluloid'de var; VLC, MPC-HC ve PotPlayer'da bu turda doğrulanmadı.
Çoklu seçimle açma (OYB-32) da 2'de: iki rakipte belgeli. Ama bizdeki hâli olası bir kusur, ucuz bir denemeyle netleşir.

## Kullanıcı Talepleri

Oy, GitHub konusundaki "+1" tepkisi sayısıdır. Hepsi açık konu; durum ve oy sayısı 13 Ekim 2026 17:42'de `gh api` ile yeniden okundu, yirmi satırın yirmisi de tuttu.

| Ürün | İstek | Oy | URL |
|---|---|---|---|
| IINA | AirPlay ile yayın | 527 | https://github.com/iina/iina/issues/63 |
| IINA | Chromecast ile yayın | 307 | https://github.com/iina/iina/issues/126 |
| IINA | UPnP ve DLNA sunucusundan oynatma | 110 | https://github.com/iina/iina/issues/2173 |
| IINA | AppleScript ile dışarıdan denetim | 69 | https://github.com/iina/iina/issues/316 |
| IINA | Yer imi | 52 | https://github.com/iina/iina/issues/681 |
| IINA | Quick Look (Finder'da boşluk tuşuyla önizleme) | 41 | https://github.com/iina/iina/issues/146 |
| IINA | Site çözmede yt-dlp'ye geçiş | 40 | https://github.com/iina/iina/issues/3502 |
| IINA | DLNA | 31 | https://github.com/iina/iina/issues/906 |
| IINA | HomePod ve AirPlay 2 | 30 | https://github.com/iina/iina/issues/1853 |
| mpv | Akışta kimlik doğrulama seçenekleri | 24 | https://github.com/mpv-player/mpv/issues/2689 |
| mpv | Listedeki sonraki videoyu önden yükleme | 21 | https://github.com/mpv-player/mpv/issues/6437 |
| mpv | Ağ üzerinden eşzamanlı oynatma | 13 | https://github.com/mpv-player/mpv/issues/1272 |
| Screenbox | `.lrc` şarkı sözü | 8 | https://github.com/huynhsontung/Screenbox/issues/285 |
| Screenbox | Müzik için çalma listesi | 3 | https://github.com/huynhsontung/Screenbox/issues/199 |
| Celluloid | Listeyi kendiliğinden kaydetme | 9 | https://github.com/celluloid-player/celluloid/issues/594 |
| Celluloid | Kalınan yeri hatırlama | 7 | https://github.com/celluloid-player/celluloid/issues/822 |
| uosc | Listeyi ters çevirme | 6 | https://github.com/tomasklaen/uosc/issues/987 |
| uosc | Listede adres yerine video başlığı | 5 | https://github.com/tomasklaen/uosc/issues/472 |
| SMPlayer | UNC yolunda çökme | 5 | https://github.com/smplayer-dev/smplayer/issues/1404 |
| mpv.net | Site videosunda çözünürlük seçimi | 5 | https://github.com/mpvnet-player/mpv.net/issues/180 |

Okuma: en yüksek oylar yayın ve ağ tarafında (AirPlay, Chromecast, DLNA); bunların hepsi K10'un alanı. K10 dışında kalan en güçlü
istekler dışarıdan denetim (69), yer imi (52) ve site çözme (40). Liste ve geçmiş istekleri az oy alıyor, çünkü rakipler bunları zaten yapıyor.

MPC-HC yeni özellik isteği kabul etmiyor [74], PotPlayer'ın açık bir iş takipçisi görülmedi; ikisinde oy verisi yok.

## Son İki Yılın Yenilikleri

Ekim 2024 ile Ekim 2026 arası. Daha eski olan tarihiyle işaretli.

**Sistem ortam denetimi.** mpv 0.39.0 (23 Eylül 2024, iki yılın hemen dışında) Windows'ta ortam denetimini ekledi [2].
PotPlayer bunu 18 Haziran 2024'te eklemişti [18]. Bugün mpv, MPC-BE, PotPlayer ve Screenbox'ın dördünde de var.

**Geçmiş.** mpv 0.40.0 (25 Mart 2025) izleme geçmişini ve onu seçen menüyü getirdi [2]. MPC-HC 2.8.0 (10 Ağustos 2026) aranabilen
"Show History" penceresi, geçmişe girmeyecek yollar için süzgeç ve 365 günde budama ekledi [12]. MPC-BE 1.9.1 (8 Ağustos 2026) geçmişe ve
listeye süzgeç alanı koydu [15]. IINA 1.5.0 (3 Ekim 2026) geçmiş penceresini yeniledi [19].

**Liste.** Screenbox Temmuz–Ekim 2026 arasında beş sürümde sürükleyerek sıralama, klasörü kuyruğa ekleme, çoklu seçim,
kalıcı sıralama tercihi ve oynatıcı menüsünden listeye eklemeyi getirdi [24]. PotPlayer 260819 liste aramasını süzgece çevirdi [18].
MPC-HC 2.8.0 çoklu seçimi listeye alfabetik sırayla alıyor ve tarihe göre sıralıyor [12]. IINA 1.5.0 karıştırmayı geri alma ve
liste ilerleme göstergesi ekledi [19].

**Ağ.** MPC-BE 1.8.5 (17 Haziran 2025) HLS listelerini çalma listesi gibi işlemeyi bıraktı [15]; bizdeki olası kusurun aynısı.
PotPlayer 260401 HLS ve DASH'i yerleşik modüle aldı [18]. Site çözmede herkes yt-dlp'ye çekiliyor: MPC-BE 1.9.0 (27 Haziran 2026)
kendi YouTube çözücüsünü kaldırdı [15], VLC 3.0.24 (22 Eylül 2026) bozuk `youtube.lua` eklentisini sildi [7].
MPC-HC Chromecast ve DLNA yayınını deneme sürümüyle açtı [14].

**Uzaktan denetim.** MPC-HC 2.8.1 (22 Ağustos 2026) web arayüzüne telefon için "remote" sayfası ekledi [12].

**Eklenti.** IINA 1.5.0 ayarların içine eklenti mağazası koydu [19]. mpv 0.41.0 (21 Aralık 2025) sağ tık menüsünü yerleşik betik yaptı [2].

**Kitaplık.** Yalnız Screenbox ilerliyor: 0.20.0 (27 Temmuz 2026) veritabanına geçti, 0.21.0 (18 Ağustos 2026) kitaplık taramasını yeniledi [24].

**Geri çekilenler.** Plex birlikte izlemeyi yeni arayüzde bırakıyor (25 Şubat 2025 notu) [32]. mpv sıradakini önden yüklemeyi 0.40'ta
varsayılan yaptı, 0.41'de yeniden kapattı [2]; kesintisiz geçiş hâlâ oturmamış.

Yön tek cümleyle: pazar parayı ortam denetimine, geçmişe ve liste yönetimine yatırdı; site çözmede yt-dlp'ye teslim oldu;
kitaplık oynatıcı sınıfında büyümüyor.

## Doğrulanamayanlar

**Bizim Tarafta (Koşulmadı)**

- **HLS adresi (OYB-20).** Kod yolu Envanter'in Ağ bölümünde. `.m3u8` ile biten adres açılınca ne görülüyor (hata iletisi mi, sessiz düşüş mü)
  bilinmiyor. Test yalnız adresin ayara yazıldığını ölçüyor (`tests/VidShrink.Tests/OynaticiAracTests.cs:812`). Doğrulama: tek bir açık HLS adresiyle deneme.
- **SMTC'nin işi (OYB-29).** mpv belgesi anahtarın libmpv'de varsayılan kapalı olduğunu söylüyor [1]. Gömülü kullanımda açılınca çalışıyor mu,
  elimizdeki libmpv 0.39 ve üstü mü bilinmiyor. Çalışmazsa Windows SDK bağı gerekir; Karar hücresi bu yüzden `sahip`.
- **Çoklu seçimle açma (OYB-32).** Explorer her dosya için ayrı süreç mi açıyor, hangisi oynuyor; koşulmadı.
- **Ses dosyalarında "Birlikte aç" (OYB-34).** "Tümünü Ata" sonrası ses türlerinin listede görünüp görünmediği koşulmadı.
- **UNC yolu ve ağ sürücüsü.** `\\sunucu\paylaşım\film.mkv` oynuyor mu; kodda engel görülmedi, denenmedi.
- **Klasördeki kapak dosyası.** mpv'nin `--cover-art-auto` varsayılanı bizde de geçerli olabilir (`MpvEngine.cs:167` yalnız `audio-display` yazıyor).
  Bu yüzden "klasörden kapak" satırı açılmadı.
- **K10'un kapsamı (OYB-25).** Karar metni "Chromecast ve DLNA" (`docs/YOL-HARITASI.md:166`). Sunucudan okuma bu karara mı girer, komşusu mu;
  tabloya `kapalı: K10` yazıldı, hüküm sahibin.
- **İş tahminleri.** Hepsi kod okumasından; hiçbiri denenmedi.

**Rakip Tarafında (Sayfada Görülmedi; "Yok" Demek Değil)**

- Atlama listesi ve Explorer küçük resim sağlayıcısı: hiçbir rakipte resmi kaynakla görülmedi. İkisi de tabloya girmedi.
- PLS ve XSPF biçiminde liste yazma: VLC'nin kaydetme biçimleri okunmadı. Satır açılmadı.
- MPC-HC: SMTC, atlama listesi ve küçük resim düğmeleri ayrı ayrı. Arayüz metninde yalnız "Global Media Keys" ve "Use enhanced taskbar features" var [13].
- VLC: liste arama, sürükleyerek sıralama, macOS Now Playing.
- mpv: görev çubuğu küçük resim düğmeleri; komut satırında çoklu dosyanın listeye dönüşmesi bu turda kaynakta okunmadı.
- PotPlayer: güncelleme günlüğünün yalnız ilk 100 000 karakteri okunabildi. Sık kullanılanlar, elle sıralama ve çoklu örnek seçeneği görülmedi.
- IINA: çoklu pencere. Screenbox: liste içinde arama.
- Kodi: web arayüzü, uzaktan uygulamalar ve 3D (kodi.tv ve kodi.wiki 403 döndü).
- Celluloid ve Haruna: MPRIS. foobar2000, MusicBee, AIMP: SMTC, şarkı sözü, görev çubuğu.
- Windows Media Player: bugünkü özellikler; tek resmi kaynak 16 Kasım 2021 tarihli [38].
- Sürüm numaraları: Plex, Infuse alt sürümü, Haruna, Windows Media Player.
- Oy verisi: Jellyfin Desktop'ın açık istekleri okunamadı (depo yeniden adlandırılmış). VLC'nin kendi iş takipçisi taranmadı.
- Adresin geçmişe yazılması ve adreste devam: bizde bilerek yok (`docs/plan-url-oynatma.md`). Rakiplerde ayrıca doğrulanmadı, satır açılmadı.

## Komşu Kola Not

- Bölüm atlama (önceki taramanın 1 numaralı kalemi) kodda yapılmış görünüyor: `Keymap.cs`, Ctrl+PageDown ve Ctrl+PageUp.
- İkincil altyazı kodda var (`secondary-sid`, `src/VidShrink.Player/AGENTS.md`); MPC-HC 2.8.1 bunu ve altyazıyı panoya kopyalamayı yeni ekledi [12].
- ReplayGain: mpv `--replaygain`, VLC `--audio-replay-gain-mode`, foobar2000 [1][9][35]; bizde iz yok.
- Sessiz yerleri atlama: mpv'de 40 oyluk açık istek [59].
- Zaman çubuğu önizlemesi istekleri: Screenbox 6, Celluloid 17, SMPlayer 18 oy [60][61][64].
- Müzik kipi ve mini oynatıcı: IINA Music Mode [20], Celluloid'de 7 oyluk istek [62]; resim içinde resim Screenbox'ta var [23], Celluloid'de 7 oy [63].
- mpv 0.40.0 imleç merkezli yakınlaştırma getirdi [2].
- Altyazı arama ve indirme: uosc ve IINA eklentiyle yapıyor [4][20]; K5 (konuşmadan metne) değil.

## Kaynaklar

- [1] https://github.com/mpv-player/mpv/blob/master/DOCS/man/options.rst — mpv seçenekleri: liste, bırakma, devam, geçmiş, ağ, ortam denetimi, kapak, kesintisiz çalma
- [2] https://github.com/mpv-player/mpv/releases — mpv 0.39.0, 0.40.0, 0.41.0 sürüm notları ve tarihleri
- [3] https://github.com/mpv-player/mpv/blob/master/osdep/mac/remote_command_center.swift — mpv'nin macOS Now Playing kodu
- [4] https://github.com/tomasklaen/uosc — uosc README ve sürüm
- [5] https://github.com/jonniek/mpv-playlistmanager — liste yönetimi betiği, README
- [6] https://github.com/hoyon/mpv-mpris — mpv için MPRIS eklentisi
- [7] https://github.com/videolan/vlc/blob/3.0.x/NEWS — VLC 3.0.x değişiklik günlüğü
- [8] https://github.com/videolan/vlc/blob/master/NEWS — VLC 4.0.0-dev (yayımlanmadı)
- [9] https://wiki.videolan.org/VLC_command-line_help/ — VLC seçenekleri
- [10] https://www.videolan.org/vlc/releases/3.0.0.html — VLC 3.0.0: 360°, ağ gezme (Şubat 2018, eski)
- [11] https://www.videolan.org/news.html — VLC 3.0.24 haberi ve tarihi
- [12] https://github.com/clsid2/mpc-hc/releases — MPC-HC 2.8.0, 2.8.1, 2.8.3 sürüm notları
- [13] https://github.com/clsid2/mpc-hc/tree/develop/src/mpc-hc/mpcresources/PO — MPC-HC arayüz metinleri (çeviri şablonu)
- [14] https://github.com/clsid2/mpc-hc/issues/4133 — MPC-HC Chromecast ve DLNA yayını denemesi
- [15] https://github.com/Aleksoid1978/MPC-BE/blob/master/docs/Changelog.txt — MPC-BE değişiklik günlüğü
- [16] https://github.com/Aleksoid1978/MPC-BE/blob/master/src/apps/mplayerc/MediaControls.cpp — MPC-BE SMTC kodu
- [17] https://potplayer.tv/ — PotPlayer: 3D, yer imi
- [18] https://t1.daumcdn.net/potplayer/PotPlayer/v4/Update2/UpdateEng.html — PotPlayer güncelleme günlüğü (ilk 100 000 karakter)
- [19] https://github.com/iina/iina/releases — IINA 1.5.0 sürüm notları
- [20] https://github.com/iina/iina — IINA README, resmi ve topluluk eklentileri
- [21] https://github.com/iina/iina/blob/develop/iina/NowPlayingInfoManager.swift — IINA Now Playing kodu
- [22] https://iina.io/ — IINA sitesi: eklenti sistemi, tarayıcı eklentisi
- [23] https://github.com/huynhsontung/Screenbox — Screenbox README
- [24] https://github.com/huynhsontung/Screenbox/releases — Screenbox 0.19.2 ile 0.23.0 arası sürüm notları
- [25] https://github.com/huynhsontung/Screenbox/blob/main/Screenbox.Core/Services/SystemMediaTransportControlsService.cs — Screenbox SMTC kodu
- [26] https://github.com/celluloid-player/celluloid — Celluloid README
- [27] https://github.com/KDE/haruna — Haruna README
- [28] https://www.smplayer.info/ — SMPlayer özellikleri
- [29] https://github.com/xbmc/xbmc — Kodi README
- [30] https://github.com/jellyfin/jellyfin-desktop — Jellyfin Desktop README
- [31] https://support.plex.tv/articles/windows-and-macos-intro-installation/ — Plex masaüstü: sunucu istemcisi
- [32] https://support.plex.tv/articles/watch-together/ — Plex birlikte izleme (25 Şubat 2025 notu)
- [33] https://www.stremio.com/ — Stremio
- [34] https://firecore.com/infuse — Infuse özellikleri ve fiyatı
- [35] https://www.foobar2000.org/ — foobar2000
- [36] https://getmusicbee.com/ — MusicBee
- [37] https://www.aimp.ru/ — AIMP
- [38] https://blogs.windows.com/windows-insider/2021/11/16/new-media-player-for-windows-11-begins-rolling-out-to-windows-insiders/ — Windows 11 Media Player (16 Kasım 2021, eski)
- [39] https://github.com/iina/iina/issues/63 — AirPlay isteği, 527 oy
- [40] https://github.com/iina/iina/issues/126 — Chromecast isteği, 307 oy
- [41] https://github.com/iina/iina/issues/2173 — UPnP ve DLNA isteği, 110 oy
- [42] https://github.com/iina/iina/issues/316 — AppleScript isteği, 69 oy
- [43] https://github.com/iina/iina/issues/681 — yer imi isteği, 52 oy
- [44] https://github.com/iina/iina/issues/146 — Quick Look isteği, 41 oy
- [45] https://github.com/iina/iina/issues/3502 — yt-dlp isteği, 40 oy
- [46] https://github.com/iina/iina/issues/906 — DLNA isteği, 31 oy
- [47] https://github.com/mpv-player/mpv/issues/2689 — kimlik doğrulama seçenekleri, 24 oy
- [48] https://github.com/mpv-player/mpv/issues/6437 — sonraki videoyu önden yükleme, 21 oy
- [49] https://github.com/mpv-player/mpv/issues/1272 — eşzamanlı oynatma, 13 oy
- [50] https://github.com/huynhsontung/Screenbox/issues/285 — `.lrc` isteği, 8 oy
- [51] https://github.com/huynhsontung/Screenbox/issues/199 — müzik listesi isteği, 3 oy
- [52] https://github.com/celluloid-player/celluloid/issues/594 — listeyi kendiliğinden kaydetme, 9 oy
- [53] https://github.com/celluloid-player/celluloid/issues/822 — kalınan yeri hatırlama, 7 oy
- [54] https://github.com/tomasklaen/uosc/issues/987 — listeyi ters çevirme, 6 oy
- [55] https://github.com/tomasklaen/uosc/issues/472 — listede video başlığı, 5 oy
- [56] https://github.com/smplayer-dev/smplayer/issues/1404 — UNC yolunda çökme, 5 oy
- [57] https://github.com/mpvnet-player/mpv.net/issues/180 — çözünürlük seçimi, 5 oy
- [58] https://github.com/iina/iina/issues/1853 — HomePod ve AirPlay 2, 30 oy
- [59] https://github.com/mpv-player/mpv/issues/6797 — sessiz yerleri atlama, 40 oy (komşu kol)
- [60] https://github.com/huynhsontung/Screenbox/issues/132 — zaman çubuğu önizlemesi, 6 oy (komşu kol)
- [61] https://github.com/celluloid-player/celluloid/issues/390 — önizleme, 17 oy (komşu kol)
- [62] https://github.com/celluloid-player/celluloid/issues/439 — müzik kipi, 7 oy (komşu kol)
- [63] https://github.com/celluloid-player/celluloid/issues/886 — resim içinde resim, 7 oy (komşu kol)
- [64] https://github.com/smplayer-dev/smplayer/issues/240 — önizleme, 18 oy (komşu kol)
- [65] `docs/oynatici/rakip-envanter.md` — iç belge: GOM Player satırları (11 Eylül 2026)
- [66] `docs/piyasa/tarama-2026-10-05.md` — iç belge: önceki tarama, oynatıcı tablosu satır 82–107
- [67] `docs/plan-url-oynatma.md` — iç belge: adres oynatma kararları
- [68] https://github.com/mpvnet-player/mpv.net/releases — mpv.net 7.1.2.0
- [69] https://github.com/smplayer-dev/smplayer/releases — SMPlayer 26.8.29
- [70] https://github.com/xbmc/xbmc/releases — Kodi 21.3
- [71] https://github.com/Stremio/stremio-shell-ng/releases — Stremio 5.0.26
- [72] https://github.com/celluloid-player/celluloid/releases — Celluloid 0.30
- [73] https://github.com/tsl0922/ImPlay/releases — ImPlay 1.5.1
- [74] https://github.com/clsid2/mpc-hc — MPC-HC README: özellik listesi, yeni istek kabul edilmediği notu
