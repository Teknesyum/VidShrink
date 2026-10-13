# Oynatıcı Sekmesi — Çekirdek Oynatma: Pazar Notları

13 Ekim 2026. Kod okuması `origin/main` 9de48d8b. Eksik tablosu aynı klasörde: `oynatici-cekirdek-eksikler.md`.

**Okuma biçimi.** mpv, VLC, MPC-HC, MPC-BE ve IINA satırlarını resmi depodan `gh` ile kendim okudum; satır numarası verdiklerim o dosyadaki birebir satırdır. PotPlayer, GOM Player, Infuse, Elmedia, Movist Pro, QuickTime Player ile mpv.net, SMPlayer, Celluloid, Haruna, Screenbox, ImPlay, GNOME Showtime, Clapper ve Windows Media Player satırları iki alt ajanın resmi sayfa okumasına dayanır; bunlar iddiadır, Kaynaklar'da "alt ajan" diye işaretli. Hiçbir uygulama çalıştırılmadı.

**Bir kez açıklanan terimler.** libmpv: mpv oynatıcısının başka uygulamaya gömülen kitaplığı. Yazılım çizimi: karenin ekran kartı yerine işlemcide hazırlanması. HDR: parlaklık aralığı geniş video. Ton eşleme: HDR videoyu sıradan (SDR) ekrana sığdırma. Passthrough: sesi çözmeden alıcıya olduğu gibi gönderme. Shader: görüntüyü ekran kartında işleyen küçük program. Deinterlace: taramalı videodaki tarak izini giderme. ASS: biçemli metin altyazı. PGS ve VobSub: resim olarak saklanan disk altyazıları. PiP: resim içinde resim, küçük yüzen pencere.

## Envanter

Yollar `src/` altındadır; `Player/` = `VidShrink.Player/`, `Playback/` = `VidShrink.App/Playback/`.

### Motor

- libmpv, `vo=libmpv`, yazılım çizimi, çıktı 8 bit BGRA: `Player/MpvEngine.cs:153`, `Player/AGENTS.md:4`.
- mpv'nin kendi tuşları, betikleri, ekran üstü yazısı ve çubuğu kapalı; hepsini uygulama çiziyor: `Player/MpvEngine.cs:159–164`.
- Yazılım çiziminde mpv'nin ekran kartına bağlı seçenekleri kareye dokunmuyor; bu yüzden görüntü ve ses ayarı `vf` ve `af` süzgeç halkalarıyla yapılıyor: `Player/MpvEngine.Advanced.cs:25–29`.

### Görüntü Ve Ses Çözme

- Varsayılan yazılım çözme: `Player/IPlaybackEngine.cs:18–22, 46`. Anahtar açılınca `auto-copy` (ekran kartı çözer, kareyi belleğe kopyalar): `Player/MpvEngine.cs:149, 154, 428`; arayüz anahtarı `Playback/PlayerView.Advanced.cs:133`; ayar saklanıyor `Playback/PlayerSettings.cs:55, 107`.
- Ses çıkışı açılamazsa sessiz sürer: `Player/MpvEngine.cs:168`.

### HDR

- Yalnız bilgi: renk uzayı, aktarım eğrisi, bit derinliği ve Dolby Vision profili okunuyor (`Player/MediaDetails.cs:18–21`), etiket "Dolby Vision", "HDR10", "HLG" ya da "SDR" (`Player/MediaDetails.cs:26–38`; `Player/MpvEngine.cs:473`).
- HDR10+ ayrılmıyor, tepe parlaklık yok (`.claude/acik.md:11`, defterde).
- Ton eşleme yalnız kodlama tarafında: `VidShrink.Core/EncodePlan.cs:11`. Oynatıcıda yok.

### Görüntü Ayarı Ve Süzgeçler

- Parlaklık, kontrast, doygunluk, gama, ton, keskinlik, yüzde 20'ye kadar kırpma, deinterlace aç ve kapa: `Player/MpvEngine.Advanced.cs:76–98`; sınırlar `Player/AdvancedSettings.cs:7–32`.
- Döndürme, ayna, en-boy oranı (özgün, 16:9, 4:3, 2.35:1): `Player/IPlaybackEngine.cs:198–213`; `Player/MpvEngine.cs:421, 522–526`; `Playback/PlayerView.Window.cs:25, 125, 133`.

### Ses

- Düzey ve sessiz: `Player/MpvEngine.cs:261–265, 280–284`. Düzey her açılışta 100 ile başlıyor, saklanmıyor: `Playback/PlayerView.axaml.cs:60`; `Playback/PlayerSettings.cs:96–108`.
- 10 bantlı ekolayzer (±12 dB) ve ön ayarlar: `Player/MpvEngine.Advanced.cs:100–113`; `Player/AdvancedSettings.cs:39–63`; `Playback/PlayerView.Advanced.cs:40`.
- Ses düzeyi dengeleme (`dynaudnorm`) ve yüzde 200'e kadar yükseltme: `Player/MpvEngine.Advanced.cs:110–111`; `Playback/PlayerView.Advanced.cs:172, 180`.
- Ses gecikmesi, 0,1 sn adım: `Player/MpvEngine.cs:391`; `Playback/SubtitleOptions.cs:18`; `Playback/PlayerView.Tracks.cs:104–109`.
- Ses izi gezme: `Playback/PlayerView.Tracks.cs:61–73`; `Playback/Keymap.cs:167`.
- Ses çıkışını yeniden açma: `Player/MpvEngine.cs:603`.

### Altyazı

- Birincil ve ikincil iz seçimi: `Player/MpvEngine.cs:353–357`; `Player/IPlaybackEngine.cs:136, 152`. İkincilin ekranda göründüğü gözle doğrulanmadı (`.claude/acik.md:10`).
- Varsayılan kapalı (`sid=no`), mpv'nin otomatik yüklemesi kapalı (`sub-auto=no`): `Player/MpvEngine.cs:165–166`.
- Dış dosya ekleme ve aynı klasördeki yan dosyayı kendiliğinden yükleme (srt, ass, ssa, vtt, sub): `Player/MpvEngine.cs:362, 369`; `Playback/PlayerView.Tracks.cs:52–59`; `Playback/SubtitleOptions.cs:35, 42–57`.
- Gecikme (0,5 sn adım), ölçek (0,5–3), konum (0–150), kod sayfası: `Playback/SubtitleOptions.cs:17–25, 59`; `Player/MpvEngine.cs:389–399`.
- Biçem: yazı tipi (üç genel aile), renk, kenar, gölge, arka plan: `Player/MpvEngine.Advanced.cs:115–122`; `Player/AdvancedSettings.cs:69–79`; `Playback/PlayerView.Advanced.cs:31–33`.
- Çevrimiçi arama (OpenSubtitles): `VidShrink.Core/Subtitles/OpenSubtitlesProvider.cs`; `Playback/PlayerView.Subtitles.cs:71`.
- Gömülü ASS, PGS ve VobSub izlerini libmpv çözer; bizde ayrı kod yok, bu dosyalarla gözle deneme `docs/olcumler/` altında bulunamadı.

### Hız

- 0,25–4 kat, tuşla 0,05, tekerlekle 0,1 adım: `Playback/Keymap.cs:49–51, 56–59`.
- İki hız arasında tek tuşla geçiş (A ve B hızı, saklanıyor): `Playback/Keymap.cs:83`; `Playback/PlayerSettings.cs:44–53`.
- Hız değişince perde düzeltmesi mpv varsayılanıyla açık kalıyor; kodda bu seçeneğe dokunan satır yok (çıkarım, `Player/MpvEngine.cs:261, 280` yalnız hızı yazıyor).

### Gezinme

- Atlama adımları 1, 10, 60 ve 300 sn, değerler sabit: `Playback/Keymap.cs:52–55`. Kullanıcı araması tam konuma gider: `Playback/PlayerView.axaml.cs:808`.
- Kare ileri ve geri: `Player/MpvEngine.cs:289`; `Playback/Keymap.cs:84–85`.
- Bölüm atlama ve zaman çubuğunda bölüm işareti: `Player/MpvEngine.cs:292–298, 430–438`; `Playback/Keymap.cs:92–93, 165–166`; `Playback/PlayerView.Window.cs:572–583`. Bölüm adı okunmuyor.
- A-B tekrar: `Player/MpvEngine.cs:276–278, 303–304`; `Playback/Keymap.cs:86–88`.
- Yer imi ekleme ve yer imleri arasında gezme: `Playback/Keymap.cs:89–91`; `Playback/PlaybackHistory.cs:221`.
- Kaldığı yerden devam: 200 kayıt tutuluyor, devam yalnız en son 5 dosyada çalışıyor: `Playback/PlaybackHistory.cs:14, 18, 104–113`. Kayıtta yalnız konum ve yer imi var: `Playback/PlaybackHistory.cs:209–222`.
- Zaman çubuğunda önizleme küçük resmi, 128×72: `Playback/PlayerView.Tools.cs:24–25, 181, 233, 259`.
- Başa dön: `Playback/Keymap.cs:64, 117`.
- Arama gecikmesi ölçülü: ortanca 32,8 ms (1080p60) ve 66,2 ms (2160p30), `docs/olcumler/oynatici-motor-libmpv.md`.

### Oynatıcının Kendi Kare Yakalaması

- PNG ya da JPG, klasör ve ad kalıbı seçimi: `Playback/PlayerSettings.cs:19–36, 120–150`; `Playback/PlayerView.Window.cs:262–271, 339, 419`.
- Kare her zaman altyazısız kaydediliyor: `Player/MpvEngine.cs:536–538`.
- Kareyi panoya kopyalama: `Playback/Keymap.cs:74, 155`; `Playback/PlayerView.Window.cs:332`.
- Klip ve GIF kesme: `Playback/Keymap.cs:173–174`; `Playback/ToolsOptions.cs:16–24`.

### Yakınlaştırma

- Alt+tekerlek ile yakınlaştırma, sürükleyerek kaydırma, sıfırlama: `Playback/Keymap.cs:67, 96, 109`; `Playback/PlayerView.axaml.cs:28, 108`; `Playback/PlayerView.Fare.cs:80, 103`.

### Klavye Ve Fare Denetimi

- Kısayolları ve fare düğmelerini yeniden atama, sıfırlama: `Playback/Keymap.cs:219`; `Playback/ShortcutBindings.cs:32, 107, 125, 155`; `Playback/ShortcutTable.cs:65, 118`.
- Fare: sol tık oynat ya da duraklat, orta tık kompakt ya da tam ekran, sağ tık menü, tekerlek: `Playback/Keymap.cs:109–112`. Çift tık yalnız başlık çubuğunda çalışıyor: `VidShrink.App/MainWindow.axaml.cs:905–913`.
- Üstte tut: `Playback/Keymap.cs:71, 151–152`; `Playback/PlayerView.Window.cs:120–122`.
- Denetim şeridi kendiliğinden gizleniyor: `Playback/PlayerView.Serit.cs:139–140, 235–236`.

### Mini Oynatıcı

- Mini kip, 480×270 (240–1280 arası): `Playback/Keymap.cs:175`; `Playback/ToolsOptions.cs:25–28, 142–168`; `Playback/PlayerView.Tools.cs:28, 96`.

### Ekran Üstü Bilgi

- Video üstü rozet yalnız hız ve ses değişiminde çıkıyor: `Playback/PlayerView.Gosterge.cs:32, 61, 72`. İz değişimi ve gecikme yalnız izleme günlüğüne yazılıyor: `Playback/PlayerView.Tracks.cs:72, 85, 101, 108`.
- Bilgi paneli ve canlı ölçüm (düşen kare, çözücü, bit hızı, gösterilen kare hızı): `Playback/Keymap.cs:72, 153`; `Playback/PlayerView.Window.cs:140–143, 596–603`; `Player/PlaybackStats.cs:7–12`; `Player/MpvEngine.cs:500–504`.

### Eskiyen Belge Satırları

- `docs/oynatici/mevcut-envanter.md` bayat: ffmpeg tabanlı eski oynatıcıyı anlatıyor; ses, hız, kare adımı ve altyazı için "YOK" diyor. Bugün motor libmpv ve dördü de var (yukarıdaki satırlar). Belge düzeltilmeli; bu turda depoya yazılmadı.
- `docs/oynatici/rakip-envanter.md` (11 Eylül 2026) kaynaklarının çoğu resmi değil; bu notlardaki satırlar resmi depo ve sürüm notuna dayanıyor.

### Önceki Taramanın "Karar Gerektirmeyenler" Kalemleri

Alanıma düşen beş kalemin beşi de yapılmış (`docs/piyasa/tarama-2026-10-05.md` 227–250):

- 1, bölüm atlama: yapıldı (`Playback/Keymap.cs:92–93, 165–166`; `Player/MpvEngine.cs:292–298`).
- 6, kareyi panoya kopyalama ve JPG: yapıldı (`Playback/Keymap.cs:74, 155`; `Playback/PlayerSettings.cs:19–23`).
- 7, donanım çözme anahtarı: yapıldı (`Playback/PlayerSettings.cs:55`; `Player/MpvEngine.cs:428`).
- 8, ikincil altyazı: yapıldı (`Player/MpvEngine.cs:357`); gözle görülmedi, defterde.
- 10, HDR alanı ve canlı istatistik: yapıldı (`Player/MediaDetails.cs:18–38`; `Player/PlaybackStats.cs:7–12`); HDR10+ ve tepe parlaklık eksik, defterde (tabloda OYA-15).

### Bizde Olmadığı Koddan Doğrulananlar

Her biri en az iki adla arandı, eşleşme çıkmadı: ton eşleme (`tone-mapping`, `target-peak`), kare ara değerleme (`interpolation`, `video-sync`), shader (`glsl`, `shader`), ses aygıtı (`audio-device`), passthrough (`spdif`, `passthrough`), perde (`pitch`, `scaletempo`, `rubberband`), kanal (`audio-channels`), dil tercihi (`slang`, `alang`), izleme kaydı (`save-position`, `watch-later`), kenar doldurma (`panscan`, `video-zoom`), altyazı gizleme (`sub-visibility`), video üstünde çift tık (`DoubleTapped`, `ClickCount`), uyku engeli (`SetThreadExecutionState`, `PowerCreateRequest`, `screensaver`, `IOPMAssertion`), imleç gizleme (`StandardCursorType.None`, `HideCursor`), zamana git (`GoTo`, `SeekDialog`, `ZamanaGit`), küçük resim tablosu (`ContactSheet`, `Mosaic`), ses düzeyi kalıcılığı (`volume`, `mute` ayar anahtarı), küçültünce duraklatma (`Minimized`), dokunmatik (`touch`, `pinch`).

### Güçlü Yanlar

Rakiplerin iş takipçisinde hâlâ istenen ve bizde bugün olanlar: zaman çubuğu önizlemesi (SMPlayer 18 oy, Celluloid 17 oy, Screenbox 6 oy), yer imi (IINA 52 oy), klip kesme (IINA 30 oy), mini kip (Celluloid 7 oy), ince atlama adımı (Celluloid 10 oy), altyazı indirme (Clapper 5 oy), görüntü alanını aşan yakınlaştırma (IINA 23 oy). Geri kare adımı VLC'ye ancak 4.0 dalında geliyor [6].

## Rakipler

| Ad | Sürüm | Tarih | Tür | URL |
|---|---|---|---|---|
| mpv | 0.41.0 | 21 Aralık 2025 | açık kaynak, çok platform | https://github.com/mpv-player/mpv/releases/tag/v0.41.0 |
| VLC | 3.0.24 kararlı; 4.0.0-dev yayımlanmadı | 22 Eylül 2026 (etiket) | açık kaynak, çok platform | https://github.com/videolan/vlc/blob/3.0.x/NEWS |
| MPC-HC (clsid2) | 2.8.3 | 8 Ekim 2026 | açık kaynak, Windows | https://github.com/clsid2/mpc-hc/releases/tag/2.8.3 |
| MPC-BE | 1.9.2 | 13 Ekim 2026 | açık kaynak, Windows | https://github.com/Aleksoid1978/MPC-BE/releases/tag/1.9.2 |
| IINA | 1.5.0 | 3 Ekim 2026 | açık kaynak, macOS, mpv tabanlı | https://github.com/iina/iina/releases/tag/v1.5.0 |
| mpv.net | 7.1.2.0 | 9 Ocak 2026 | açık kaynak, Windows, mpv arayüzü | https://github.com/mpvnet-player/mpv.net/releases/tag/v7.1.2.0 |
| SMPlayer | 26.8.29 | 30 Ağustos 2026 | açık kaynak, mpv arayüzü | https://github.com/smplayer-dev/smplayer/releases/tag/v26.8.29 |
| Celluloid | 0.30 | 29 Mart 2026 | açık kaynak, Linux, mpv arayüzü | https://github.com/celluloid-player/celluloid/releases/tag/v0.30 |
| Haruna | 1.8.1 | 19 Mayıs 2026 | açık kaynak, KDE, mpv arayüzü | https://github.com/KDE/haruna/blob/HEAD/data/org.kde.haruna.metainfo.xml |
| Screenbox | 0.23.0 | 4 Ekim 2026 | açık kaynak, Windows, LibVLC tabanlı | https://github.com/huynhsontung/Screenbox/releases/tag/v0.23.0 |
| ImPlay | 1.5.1 | 30 Kasım 2023 (eski; son commit 23 Şubat 2026) | açık kaynak, mpv arayüzü | https://github.com/tsl0922/ImPlay/releases/tag/1.5.1 |
| GNOME Showtime | 50.0 | 14 Mart 2026 | açık kaynak, Linux masaüstünün kendi oynatıcısı | https://github.com/GNOME/showtime/blob/HEAD/data/org.gnome.Showtime.metainfo.xml.in |
| Clapper | 0.10.0 | 22 Aralık 2025 | açık kaynak, GStreamer tabanlı | https://github.com/Rafostar/clapper/releases/tag/0.10.0 |
| uosc | 5.13.0 | 3 Ağustos 2026 | açık kaynak, mpv için arayüz betiği | https://github.com/tomasklaen/uosc/releases |
| PotPlayer | derleme 260819 | 19 Ağustos 2026 (derleme numarasından çıkarım) | ücretsiz, kapalı kaynak, Windows | https://t1.daumcdn.net/potplayer/PotPlayer/v4/Update2/UpdateEng.html |
| GOM Player | 2.3.123 | 13 Ekim 2026 (alt ajan okuması) | ücretsiz ve ücretli, Windows | https://www.gomlab.com/gomplayer-media-player/release-note |
| KMPlayer | doğrulanmadı | doğrulanmadı | ücretsiz, Windows | resmi sayfa 404 döndü |
| Infuse | 8.5.6 | 30 Eylül 2026 | ücretli, Apple aygıtları | https://firecore.com/releases |
| Elmedia | 8.24 | 5 Aralık 2025 | ücretsiz ve ücretli, macOS | https://www.electronic.us/products/elmedia/ |
| Movist Pro | 2.19.0 | doğrulanmadı | ücretli, macOS | https://movistprime.com/changelog.html |
| QuickTime Player | doğrulanmadı | doğrulanmadı | macOS'un kendi oynatıcısı | https://support.apple.com/guide/quicktime-player/welcome/mac |
| Windows Media Player (yeni) | doğrulanmadı | doğrulanmadı | Windows 11'in kendi oynatıcısı | https://blogs.windows.com/windows-insider/2021/11/16/new-media-player-for-windows-11-begins-rolling-out-to-windows-insiders/ |

Görev listesinde olmayıp eklenenler: Movist Pro, ImPlay, GNOME Showtime, Clapper, uosc.

## Özellik Matrisi

Okuma: `var` resmi kaynakta görüldü; `?` doğrulanmadı ("rakipte yok" demek değildir); `K4` bizde kapalı karara düşüyor. Köşeli sayı Kaynaklar'a bakar; mpv sütunundaki çıplak sayı `options.rst` satırıdır [1].

### HDR

| Özellik | Biz | mpv | VLC | MPC-HC | IINA | Diğer |
|---|---|---|---|---|---|---|
| HDR videoyu SDR ekranda ton eşleme | yok | var, 7675 | var, 4.0 dalında [6] | var: MPCVR ya da madVR ile; eski çiziciye HLG ton eşleme 2.8.3'te geldi [11] | var [20][21] | PotPlayer [36], Movist Pro [42], mpv.net [46] |
| Ton eşleme yöntemi ve hedef parlaklık ayarı | yok, K4 | var, 7549–7825 | ? | ? | var [21] | PotPlayer [36] |
| HDR ekranda HDR çıkışı | yok, K4 | var, 7346 | var: D3D11 HDR seçeneği [7] | var: MPCVR [11] | var [21] | ? |
| Dolby Vision | yarım: yalnız etiket | var, 7383 | ? | var: MPCVR [11] | ? | MPC-BE [18], Infuse profil 5 ve 8 [40], Movist Pro [42][43] |
| HDR10+ | yarım: HDR10'dan ayrılmıyor | var: sahne üst verisi [3] | ? | ? | ? | Infuse [40] |
| HLG | etiket var, gösterim ölçülmedi | var | ? | var [11] | ? | Infuse [40] |

### Görüntü İyileştirme

| Özellik | Biz | mpv | VLC | MPC-HC | IINA | Diğer |
|---|---|---|---|---|---|---|
| Kare ara değerleme | yok, K4 | var, 6177 ve 8162 | ? | ? | ? | mpv.net [46] |
| Ekran kartıyla büyütme (RTX Video Super Resolution ve benzeri) | yok, K4 | var: RTX Video HDR, 0.40 [5] | var: AMD Super Resolution, NVIDIA TrueHDR, Moore Threads [6][7] | ? | ? | Screenbox "Super resolution" seçeneği [55], Infuse yapay zekâ büyütme [39] |
| Shader yükleme | yok, K4 | var, 6569 | ? | var [15] | ? | ? |
| Ölçekleyici ve bant giderme | yok, K4 | var, 5942 ve 6911 | var: 4.0 dalında libplacebo ölçekleme [6] | ? | ? | ? |
| ICC renk yönetimi | yok, K4 | var, 7899 | var: 4.0 dalında [6] | ? | var [21] | ? |
| Keskinleştirme | var | var, 6948 | ? | ? | ? | ? |
| Deinterlace | yarım: elle aç ve kapa | var: otomatik seçenekli, 1818 | var: otomatik seçenekli [8] | ? | ? | mpv.net [45], Celluloid [50], ImPlay [56] |
| Renk ayarı | var | var | ? | var: 2.8.0 renk denetimleri | ? | SMPlayer [49], ImPlay [56] |

### Ses

| Özellik | Biz | mpv | VLC | MPC-HC | IINA | Diğer |
|---|---|---|---|---|---|---|
| Çıkış aygıtı seçimi | yok | var, 2075 | var [6][8] | ? | var [21] | Elmedia [41], Movist Pro [42], mpv.net [45], ImPlay [56] |
| Bitstream passthrough | yok | var, 2134 | var [6] | ? | var [21] | Infuse [40], Elmedia [41], Movist Pro [42] |
| Özel kip çıkışı | yok | var, 2106 | var: 4.0 dalında [6] | ? | ? | ? |
| Ekolayzer | var, 10 bant | var: süzgeçle, 2127 | ? | ? | ? | Elmedia [41], Movist Pro [42], ImPlay [56] |
| Ses düzeyi dengeleme | var | var: süzgeçle | var: 4.0 dalında EBU R128 [6] | var [15] | ? | Haruna ReplayGain [53] |
| Perde düzeltme | yarım: açık ama anahtarı yok | var, 2056 ve 256 | var [8] | var: kendi ses çizicisinde [11] | ? | ? |
| Ses gecikmesi | var | var | var [8] | ? | ? | Elmedia [41], Screenbox [54], ImPlay [56] |
| Kanal düzeni | yok | var, 2273 | var [8] | ? | ? | MPC-BE [19], Movist Pro [42] |
| Ses düzeyini hatırlama | yok | dosya başına, 1161 | var [8] | ? | var [21][22] | mpv.net [47] |

### Altyazı

| Özellik | Biz | mpv | VLC | MPC-HC | IINA | Diğer |
|---|---|---|---|---|---|---|
| İkinci altyazı | var, gözle görülmedi | var, 2536 | var: 4.0 dalında [6] | var: 2.8.1 [13] | var [21] | mpv.net [45], SMPlayer [48], ImPlay [56] |
| İkinci altyazının ayrı konum ve gecikmesi | yok | var, 2515 ve 2629 | var [8] | ? | var [21] | ? |
| Biçem ayarı | yarım: üç genel yazı tipi ailesi | var, 3068 | var: metin ölçeği [8] | ? | var [21] | Haruna [53], Elmedia [41], mpv.net [45] |
| ASS biçemini geçersiz kılma | ölçülmedi | var, 2721 | ? | ? | var [21] | mpv.net [45], Celluloid [50] |
| Gecikme | var | var | var [8] | ? | ? | GOM Player [37], Elmedia [41], ImPlay [56] |
| Satıra atlama ve satıra göre eşzamanlama | yok | var [2] | var [8] | var [15] | ? | ? |
| Tek tuşla gizleme | yarım | var [2] | var [8] | ? | ? | Celluloid [50] |
| Çevrimiçi arama | var | ? | ? | var [11] | var [21] | SMPlayer [49], Infuse [40], Elmedia [41] |
| Dil tercihi | yok | var, 7 ve 23 | var [8] | ? | var [21] | Movist Pro [42], mpv.net [45], Haruna [53], Screenbox [54] |
| Alt klasörde arama | yok | var, 3006 | var [8] | ? | var [21] | Haruna [53] |
| Zorunlu altyazıyı kendiliğinden gösterme | yok | var, 173 | var: 4.0 dalında [6] | ? | ? | ? |
| Dış PGS ve VobSub dosyası | yok | ? (varsayılan uzantı listesi belgede yazmıyor, 2910) | ? | ? | ? | Infuse [39] |
| Konuşmadan altyazı üretimi | yok, K5 | ? | ? | ? | ? | PotPlayer [36], GOM Player [37][38] |

### Gezinme Ve Hız

| Özellik | Biz | mpv | VLC | MPC-HC | IINA | Diğer |
|---|---|---|---|---|---|---|
| Önizleme küçük resmi | var | ? (çekirdekte görülmedi) | ? | var [11] | var [21][22] | Infuse [39], Movist Pro [42], Haruna [52], SMPlayer 26.8 [48] |
| Bölüm atlama | var | var [2] | var [8] | ? | ? | Celluloid [50], Screenbox [55], Clapper [58] |
| Bölüm adı ve listesi | yok | var [2][3] | ? | ? | var [20] | Screenbox 0.20.0 [54], Celluloid [51], mpv.net [45], ImPlay [56] |
| A-B tekrar | var | var [2] | var: 4.0 dalında tekrar sayısıyla [6] | var [11] | ? | Elmedia [41], Movist Pro [42], Celluloid [50] |
| Kare ileri ve geri | var | var [2] | ileri var, geri 4.0 dalında [6][8] | var [11][15] | ? | mpv.net [45], ImPlay [56] |
| Tam konuma arama | var | var, 382 | var [8] | ? | var [21] | Haruna [52] |
| Kaldığı yerden devam | yarım: yalnız son 5 dosya | var, 1127 | ? | var [11] | var [21] | Screenbox 0.15.0 [54], SMPlayer [49], Haruna [53], Movist Pro [42] |
| Dosya başına iz, gecikme ve hız hatırlama | yok | var, 1161 | ? | ? | ? | SMPlayer [49], mpv.net [45] |
| Zamana git | yok | ? | var [10] | var [15][16] | ? | ? |
| Atlama adımını ayarlama | yarım: dört sabit adım | var: tuş başına yazılır [2] | ? | ? | var [21] | Screenbox 0.19.2 [54], Haruna [52], Clapper [58] |
| Hız aralığı | 0,25–4 | 0,01–100, 249 | var [8] | var: adım ayarlı [11] | ? | Screenbox özel hız 0.23.0 [54] |
| Basılı tutarak geçici hızlandırma | yok | ? | ? | ? | ? | MPC-BE 1.9.1 [18], PotPlayer [36], Infuse 8.0.5 [39], Screenbox 0.19.0 [54] |
| Ters oynatma | yok | var, 558 | ? | ? | ? | ? |

### Denetim, Pencere Ve Ekran Üstü Bilgi

| Özellik | Biz | mpv | VLC | MPC-HC | IINA | Diğer |
|---|---|---|---|---|---|---|
| Kısayol özelleştirme | var | var [2] | var [8] | var [11] | ? | Haruna [52], mpv.net [47], Movist Pro [42] |
| Fare eylemi atama | var | var [2] | ? | var [11] | var [21] | Haruna [52], Clapper [58] |
| Çift tıkla tam ekran | yok | var [2] | ? | ? | var [21] | PotPlayer: çift tıkta sol ve sağ alana ayrı eylem [36] |
| Dokunmatik hareketler | yok | var, 4503 | var: 4.0 dalında [6] | ? | var: izleme dörtgeni [21] | Screenbox [54], Clapper [58], SMPlayer [49] |
| Mini oynatıcı ya da PiP | var | ? | var: 4.0 dalında [6][9] | ? | var [21][22] | Screenbox [55], Elmedia [41], Movist Pro [42] |
| Her zaman üstte | var | var, 3527 | var [8] | var [15] | var [21] | mpv.net [45], ImPlay [56] |
| Yalnız oynatırken üstte | yok | ? | ? | var [15] | var [21] | ? |
| Uyku ve ekran kararması engeli | yok | var, 3815 | var [8] | ? | var [22] | ? |
| İmleci gizleme | yok | var, 3732 | var [8] | ? | ? | ? |
| Ekran üstü bildirim | yarım: yalnız hız ve ses | var, 4767 ve 4925 | var [8] | ? | var [21] | ? |
| İstatistik katmanı | var, beş ölçüm | var [2][5] | ? | ? | ? | mpv.net [45], ImPlay [56] |
| Küçültünce duraklatma | yok | ? | var [9] | ? | var [21] | ? |

### Kare Yakalama

| Özellik | Biz | mpv | VLC | MPC-HC | IINA | Diğer |
|---|---|---|---|---|---|---|
| Kare kaydı | var: PNG ve JPG | var: png, jpg, webp, jxl, avif, 5037 | var [8] | var [15] | var [21] | Screenbox 0.21.0 [54], Haruna [53], GNOME Showtime [57] |
| Altyazılı ve altyazısız seçenek | yok: her zaman altyazısız | var [2] | ? | ? | var [21] | mpv.net [45], Celluloid [50] |
| Panoya kopyalama | var | ? | ? | ? | var [21] | ? |
| Küçük resim tablosu | yok | ? | ? | var [15] | ? | SMPlayer küçük resim üreteci [49] |
| Art arda kare yakalama | yok | var [2] | ? | ? | ? | Elmedia [41] |

## Kullanıcı Talepleri

Rakiplerin iş takipçisinde en çok beğeni alan açık istekler. Oy, GitHub'da başparmak sayısı, VLC'de GitLab oyu. Sayılar 13 Ekim 2026 okumasıdır.

| İstek | Oy | Kimin Takipçisi | Bizde | URL |
|---|---|---|---|---|
| AirPlay | 527 | IINA | komşu kol, K10 | https://github.com/iina/iina/issues/63 |
| Chromecast | 307 | IINA | komşu kol, K10 | https://github.com/iina/iina/issues/126 |
| DLNA | 110 | IINA | komşu kol, K10 | https://github.com/iina/iina/issues/2173 |
| AppleScript desteği | 69 | IINA | komşu kol | https://github.com/iina/iina/issues/316 |
| Yer imi | 52 | IINA | var | https://github.com/iina/iina/issues/681 |
| Quick Look | 41 | IINA | komşu kol | https://github.com/iina/iina/issues/146 |
| Sessiz bölümleri atlama | 40 | mpv | yok, OYA-37 | https://github.com/mpv-player/mpv/issues/6797 |
| yt-dlp | 40 | IINA | komşu kol, K1 | https://github.com/iina/iina/issues/3502 |
| Klip kırpma | 30 | IINA | var | https://github.com/iina/iina/issues/1989 |
| Dolby Atmos | 24 | IINA | yok, OYA-21 ile ilgili | https://github.com/iina/iina/issues/3548 |
| Görüntü alanını aşan yakınlaştırma | 23 | IINA | var | https://github.com/iina/iina/issues/3208 |
| Sonraki dosyayı önbelleğe alma | 21 | mpv | komşu kol | https://github.com/mpv-player/mpv/issues/6437 |
| Gömme arayüzünde gpu-next çizici | 18 | mpv | K4 ile ilgili | https://github.com/mpv-player/mpv/issues/10810 |
| Zaman çubuğunda önizleme | 18 | SMPlayer | var | https://github.com/smplayer-dev/smplayer/issues/240 |
| SponsorBlock | 17 | mpv | komşu kol | https://github.com/mpv-player/mpv/issues/9630 |
| Kare ikileme | 17 | mpv | yok, OYA-16, K4 | https://github.com/mpv-player/mpv/issues/6137 |
| Önizleme | 17 | Celluloid | var | https://github.com/celluloid-player/celluloid/issues/390 |
| Fade-In ve Fade-Out | 13 | mpv | gövdesi okunmadı | https://github.com/mpv-player/mpv/issues/15581 |
| Eşzamanlı ağ oynatma | 13 | mpv | komşu kol | https://github.com/mpv-player/mpv/issues/1272 |
| 360° video | 13 | mpv | yok, OYA-52 | https://github.com/mpv-player/mpv/issues/8283 |
| Gömme arayüzüne Vulkan | 12 | mpv | K4 ile ilgili | https://github.com/mpv-player/mpv/issues/6575 |
| Ok tuşu atlama adımı fazla büyük | 10 | Celluloid | yarım, OYA-55 | https://github.com/celluloid-player/celluloid/issues/385 |
| Yapay zekâ ile büyütme | 8 | mpv.net | yok, OYA-18, K4 | https://github.com/mpvnet-player/mpv.net/issues/724 |
| PiP | 7 | Celluloid | var (mini kip) | https://github.com/celluloid-player/celluloid/issues/886 |
| Kaldığı yeri hatırlama | 7 | Celluloid | yarım, OYA-36 | https://github.com/celluloid-player/celluloid/issues/822 |
| Zaman çubuğu önizleme | 6 | Screenbox | var | https://github.com/huynhsontung/Screenbox/issues/132 |
| Pencereyi video oranına kilitleme | 6 | Clapper | yok, OYA-48 | https://github.com/Rafostar/clapper/issues/30 |
| Klasörde önceki ve sonraki dosya | 6 | VLC | komşu kol | https://code.videolan.org/videolan/vlc/-/work_items/22382 |
| HDR soluk görünüyor | 5 | Screenbox | ölçülmedi, OYA-11 | https://github.com/huynhsontung/Screenbox/issues/407 |
| Altyazı özelleştirme | 5 | Screenbox | yarım, OYA-34 | https://github.com/huynhsontung/Screenbox/issues/29 |
| Altyazı metnini listeleme ve altyazıya göre gezinme | 5 | VLC | yok, OYA-25 | https://code.videolan.org/videolan/vlc/-/work_items/25003 |
| Altyazı indirme | 5 | Clapper | var | https://github.com/Rafostar/clapper/issues/69 |
| Dokunmatik denetimler | 5 | Celluloid | yok, OYA-53 | https://github.com/celluloid-player/celluloid/issues/408 |
| Sessizliği atlama | 4 | VLC | yok, OYA-37 | https://code.videolan.org/videolan/vlc/-/work_items/20850 |
| Aramada altyazıyı anında gösterme | 4 | VLC | ölçülmedi | https://code.videolan.org/videolan/vlc/-/work_items/18862 |
| Dokunmatikte çift dokunuşla ileri ve geri | 4 | Screenbox | yok, OYA-53 | https://github.com/huynhsontung/Screenbox/issues/227 |
| Daha iyi HDR desteği | 3 | VLC | OYA-11 | https://code.videolan.org/videolan/vlc/-/work_items/18088 |
| Renk ayarı arayüzü | 3 | uosc | var | https://github.com/tomasklaen/uosc/issues/616 |

VLC'nin GitLab takipçisinde oylar düşük kalıyor; en popüler açık istek 6 oyda. Haruna (bugs.kde.org) ve GNOME Showtime (gitlab.gnome.org) takipçileri `gh` ile okunamadı.

## Son İki Yılın Yenilikleri

Ekim 2024 – Ekim 2026. Tarihi yazılmayan sürümün tarihi bu turda okunmadı.

- **mpv 0.40.0 (25 Mart 2025)** [5]: NVIDIA RTX Video HDR, Linux'ta yerel HDR, betiklere pano erişimi, komut menüsü, izleme geçmişi, imleç merkezli yakınlaştırma ve sürükleyerek kaydırma, istatistikte zaman kodu, sağ tıkla en yakın bölüme arama.
- **mpv 0.41.0 (21 Aralık 2025)** [4]: `gpu-next` varsayılan çizici oldu, Vulkan donanım çözme öne alındı, sağ tık bağlam menüsü, HDR çıkışında altyazı parlaklığı, ekran görüntüsü komutuna yeni bayraklar.
- **VLC 3.0.24 (22 Eylül 2026)** [7]: FFmpeg 8.1, dosya adından altyazı dili algılama, yeni ekran kartlarında Super Resolution.
- **VLC 4.0 dalı (yayımlanmadı)** [6]: geri kare adımı, ikili altyazı, zorunlu altyazı dili, A-B tekrar sayısı, Vulkan ve libplacebo çıkışı, ICC profili, PiP, özel kip ses çıkışı, yeni ses süzgeçleri, dokunmatik gezinme.
- **MPC-HC 2.8.1 – 2.8.3 (2.8.3: 8 Ekim 2026)** [11][12][13]: ikincil altyazı, altyazı metnini panoya kopyalama, ReplayGain, eski çiziciye HLG ton eşleme, Windows 11 görünümü. 2.7.4'te MPCVR varsayılan çizici oldu [14].
- **MPC-BE 1.9.1 – 1.9.2 (1.9.2: 13 Ekim 2026)** [17][18]: AV1 içinde Dolby Vision, sol fare tuşuna uzun basarak hızlandırma, resim altyazıda geniş renk uzayı, koyu tema.
- **IINA 1.5.0 (3 Ekim 2026)** [20]: SDR ekranda HDR ton eşleme düzeltmesi, ton eşleme parametresi, zaman çubuğunda bölüm adı, önizleme genişliği ayarı, karedeki yazıyı seçme (Live Text), eklenti mağazası.
- **PotPlayer** [36]: fare tuşu basılıyken hız (15 Ekim 2024), HDR'de ekran parlaklığı ayarı (11 Aralık 2024), ASS için libass (26 Şubat 2025), ton eşleme türü seçimi (25 Haziran 2025), çift tıkta sol ve sağ alana ayrı eylem (19 Ağustos 2026).
- **Infuse** [39]: basılı tutunca geçici 2 kat hız (8.0.5, 3 Aralık 2024), yapay zekâ ile büyütme (8.1.2, 11 Mart 2025), dış resim altyazı dosyası (8.1.4), canlı önizleme küçük resmi (8.1.6), HDR altyazı parlaklığı (8.2).
- **Screenbox** [54]: ses gecikmesi (0.14.0, 23 Ekim 2024), her zaman son konumdan devam (0.15.0, 5 Mart 2025), görüntü büyütme seçenekleri (0.16.0), basılı tutma ve kaydırma hareketleri (0.19.0, 21 Mayıs 2026), atlama adımı ayarı (0.19.2), bölüm listesi (0.20.0, 27 Temmuz 2026), özel hız (0.23.0).
- **SMPlayer 26.8.29 (30 Ağustos 2026)** [48]: zaman çubuğunda önizleme, ikinci altyazı seçeneği.
- **Haruna 1.4.0 – 1.8.1** [53]: alt klasörlerde altyazı arama, fare atamasında değiştirici tuş, ReplayGain, altyazı kenar biçemi.
- **Clapper 0.8.0 – 0.10.0** [58]: ilk Windows sürümü, kare ilerletme.
- **GNOME Showtime 48 – 50** [57]: yeniden tasarım, elle döndürme, ekran görüntüsü klasörü.
- **ImPlay**: Ekim 2024'ten bu yana kararlı sürüm yok.

**Eğilim.** Üç şey öne çıkıyor. Birincisi HDR: mpv, MPC-HC, IINA, PotPlayer ve VLC'nin beşi de bu dönemde ton eşleme ya da HDR çıkışına iş koydu. İkincisi basılı tutarak geçici hızlandırma: dört rakip iki yıl içinde ekledi. Üçüncüsü ekran kartı sürücüsünün görüntü iyileştirmesi (RTX, AMD) oynatıcılara bağlanıyor.

### Kapalı Kararlara Denk Gelen Konularda Pazar

- **K4, oynatıcıda GPU çizim yolu.** Pazar bu dönemde GPU çizimine daha çok yaslandı: mpv yeni çizicisini varsayılan yaptı [4], MPC-HC varsayılan çiziciyi değiştirdi ve eskisine HLG ton eşleme ekledi [11][14], VLC 4.0 dalı Vulkan çıkışı ekledi [6], IINA ve PotPlayer ton eşleme ayarı ekledi [20][36]. Bizde ton eşleme, HDR çıkışı, Dolby Vision rengi, kare ara değerleme, shader, sürücü büyütmesi, bant giderme ve ICC bu karara bağlı (OYA-12, 13, 14 ve 16–20). mpv'nin gömme arayüzünde yeni çizici hâlâ açık istek [24][25]; karar açılsa bile yol hazır değil.
- **K5, konuşmadan altyazı.** PotPlayer Whisper ile altyazı üretimini (11 Aralık 2024) ve gerçek zamanlı üretimi (9 Eylül 2025) ekledi [36]; GOM Player yapay zekâ altyazısı sunuyor [37][38].
- **Donanım çözme varsayılanı (sahibin kararı).** mpv 0.41 Vulkan donanım çözmeyi öne aldı ve çözücü iş parçacığı seçeneği ekledi [4].

## Doğrulanamayanlar

- **HDR dosya bizde bugün nasıl görünüyor: ölçülmedi.** `docs/olcumler/` altında HDR gösterim ölçümü yok; `.claude/acik.md:11` gerçek Dolby Vision ve HLG dosyasının denenmediğini söylüyor. OYA-11'in büyüklüğü bu ölçüme bağlı.
- **GPU'suz ton eşleme yolu çıkarımdır.** libmpv'ye `vf` ile işlemcide çalışan bir ton eşleme süzgeci takılabileceği kod yapısından çıkarıldı; pakette gereken süzgecin olup olmadığı ve 4K'da hızın yetip yetmediği ölçülmedi.
- **Ekranın oynatma sırasında gerçekten karardığı ölçülmedi.** Yalnız kodda engelleme çağrısı bulunamadı (OYA-01). İmleç için de aynı: gizleyen kod yok, ekranda denenmedi (OYA-08).
- **Perde düzeltmesinin açık olduğu çıkarımdır**: mpv varsayılanı açık [1] ve kodda kapatılmıyor; kulakla denenmedi.
- **Biçem ayarının ASS altyazıya uygulanıp uygulanmadığı ölçülmedi** (OYA-27). mpv belgesine göre geçersiz kılma seçeneği olmadan uygulanmaz; bu da çıkarım.
- **Gömülü PGS ve VobSub izlerinin bizde göründüğü ölçülmedi**; libmpv'nin çözdüğü varsayıldı. mpv'nin dış `.sup` ve `.idx` dosyasını kendiliğinden yükleyip yüklemediği belgede yazmıyor [1].
- **KMPlayer**: resmi sayfa 404 döndü; sürüm ve bütün özellikler doğrulanmadı.
- **QuickTime Player**: yalnız kılavuz başlıkları okundu; hemen her kalem doğrulanmadı.
- **Windows Media Player (yeni)**: sürüm, tarih, son iki yılın notları ve mini oynatıcı dışındaki özellikler doğrulanmadı; blog içeriği arama özetinden alındı.
- **PotPlayer**: güncelleme günlüğünün Ekim 2024 öncesi okunmadı; kare ara değerleme, shader, ses aygıtı, passthrough, küçük resim tablosu ve A-B bu yüzden doğrulanmadı. Sürüm tarihi derleme numarasından çıkarıldı.
- **GOM Player**: HDR, hız, kare yakalama ve ses özellikleri doğrulanmadı. Sürüm tarihi alt ajan okumasıdır.
- **Movist Pro**: sürüm tarihi doğrulanmadı.
- **RTX Video Super Resolution** adıyla hiçbir ticari oynatıcıda ve mpv arayüzünde resmi kanıt görülmedi; Screenbox'ta yalnız "Super resolution" seçeneği var, hangi teknoloji olduğu belli değil.
- **MPC-HC**: ses ve altyazı dil tercihi, iz seçimini hatırlama ve ses çıkış aygıtı seçimi için resmi kaynak bulunamadı. Eski sürümlerin (2.7.4, 2.8.0 – 2.8.2) tarihi okunmadı.
- **SMPlayer'ın dosya başına ayar hatırlaması** alt ajanın site okumasına dayanır; deponun README dosyasında bu söz bulunamadı.
- **Haruna**: eylem listesi okunamadı; kare adımı, hız, deinterlace ve gecikme açık. Sürüm KDE'nin GitHub aynasından.
- **mpv.net**: ayar düzenleyici listesi kesik okundu; shader, kare ara değerleme anahtarı ve passthrough kesilen kısımda olabilir.
- **Celluloid**: çoğu özellik yalnız kısayol listesinde görüldü, menüde değil.
- **mpv çekirdeğinde zaman çubuğu önizlemesi** görülmedi; yaygın çözüm üçüncü taraf betik.
- **Sessiz bölümleri atlamayı hazır sunan bir video oynatıcı doğrulanmadı**; yalnız istek var (OYA-37).
- **mpv #15581 (Fade-In ve Fade-Out)**: yalnız başlık ve oy okundu, gövde okunmadı; tabloya alınmadı.
- **VLC 4.0** yayımlanmadı; "4.0 dalında" denen her satır geliştirme dalının değişiklik listesindendir, kullanıcının elindeki 3.0.24'te olmayabilir.
- **"Çift tıkla tam ekran" ve "basılı tutarak hızlandırma"** varsayılan fare davranışını değiştirir; bizde sol tık oynat ya da duraklat. İkisinin nasıl bir arada duracağı sahibin kararı.

## Kaynaklar

- [1] https://github.com/mpv-player/mpv/blob/master/DOCS/man/options.rst — mpv seçenek belgesi; satır numaraları bu dosyadan (okuyucu için karşılığı https://mpv.io/manual/master/)
- [2] https://github.com/mpv-player/mpv/blob/master/etc/input.conf — mpv varsayılan kısayolları: çift tık 32, altyazı satırı 57–60, bölüm 75–76, istatistik 103–105, altyazı gizleme 139, kare kaydı 149–152, kenar doldurma 153–155, pencere ölçeği 131–133, bölüm seçme 191
- [3] https://github.com/mpv-player/mpv/blob/master/DOCS/man/input.rst — mpv özellik belgesi: bölüm üst verisi 2580, HDR üst verisi 2950–2966
- [4] https://github.com/mpv-player/mpv/releases/tag/v0.41.0 — mpv 0.41.0 sürüm notu
- [5] https://github.com/mpv-player/mpv/releases/tag/v0.40.0 — mpv 0.40.0 sürüm notu
- [6] https://github.com/videolan/vlc/blob/master/NEWS — VLC 4.0.0-dev değişiklik listesi; 3.0.21 başlığı 606–630
- [7] https://github.com/videolan/vlc/blob/3.0.x/NEWS — VLC 3.0.x değişiklik listesi, 3.0.24
- [8] https://github.com/videolan/vlc/blob/master/src/libvlc-module.c — VLC seçenek ve kısayol tanımları: `volume-save` 1620, `audio-time-stretch` 1621, `stereo-mode` 1631, `disable-screensaver` 1702, `mouse-hide-timeout` 1720, `snapshot-format` 1726, `deinterlace` 1775–1779, `osd` 1808, `sub-autodetect-path` 1826, `secondary-sub-margin` 1844, `audio-language` 1867, `sub-language` 1870, altyazı eşzamanlama tuşları 2659–2665, `key-audiodevice-cycle` 2677, `key-subtitle-toggle` 2684, kırpma ve yakınlaştırma tuşları 2692–2752
- [9] https://github.com/videolan/vlc/blob/master/modules/gui/qt/qt.cpp — VLC arayüz seçenekleri: `qt-pause-minimized` 370, `qt-pip-mode` 476
- [10] https://github.com/videolan/vlc/tree/master/modules/gui/qt/dialogs/gototime — VLC zamana git penceresi
- [11] https://github.com/clsid2/mpc-hc/releases/tag/2.8.3 — MPC-HC 2.8.3 notu ve özellik özeti
- [12] https://github.com/clsid2/mpc-hc/releases/tag/2.8.2 — MPC-HC 2.8.2: ReplayGain
- [13] https://github.com/clsid2/mpc-hc/releases/tag/2.8.1 — MPC-HC 2.8.1: ikincil altyazı, altyazı metnini panoya kopyalama
- [14] https://github.com/clsid2/mpc-hc/releases/tag/2.7.4 — MPC-HC 2.7.4: MPCVR varsayılan çizici
- [15] https://github.com/clsid2/mpc-hc/blob/develop/src/mpc-hc/resource.h — MPC-HC komut adları: zamana git, altyazıya atlama, shader, küçük resim tablosu, oynatırken üstte, normalleştirme
- [16] https://github.com/clsid2/mpc-hc/blob/develop/src/mpc-hc/GoToDlg.cpp — MPC-HC zamana git penceresi
- [17] https://github.com/Aleksoid1978/MPC-BE/releases/tag/1.9.2 — MPC-BE 1.9.2
- [18] https://github.com/Aleksoid1978/MPC-BE/releases/tag/1.9.1 — MPC-BE 1.9.1: AV1 içinde Dolby Vision, uzun basarak hızlandırma
- [19] https://github.com/Aleksoid1978/MPC-BE/releases/tag/1.8.9 — MPC-BE 1.8.9: 7.1 kanal dağılımı
- [20] https://github.com/iina/iina/releases/tag/v1.5.0 — IINA 1.5.0 sürüm notu
- [21] https://github.com/iina/iina/blob/develop/iina/Preference.swift — IINA tercih anahtarları: `softVolume` 58, `resumeLastPosition` 78, `alwaysFloatOnTop` 83, `pauseWhenMinimized` 86, `screenshotIncludeSubtitle` 101, `enableOSD` 132, `thumbnailWidth` 158, `enableToneMapping` 189, `audioLanguage` 200, passthrough 203–207, `audioDevice` 209, `subAutoLoadSearchPath` 228, `ignoreAssStyles` 229, `subTextFont` 232, `subLang` 249, `relativeSeekAmount` 282, `doubleClickAction` 293, `pinchAction` 296
- [22] https://github.com/iina/iina/tree/develop/iina — IINA kaynak dosyaları: `SleepPreventer.swift`, `ThumbnailPeekView.swift`, `VideoPIPViewController.swift`; `PlayerCore.swift:1300` ses düzeyini tercihe yazar
- [23] https://github.com/mpv-player/mpv/issues/6797 — sessiz bölümleri atlama isteği, 40 oy
- [24] https://github.com/mpv-player/mpv/issues/10810 — gömme arayüzünde yeni çizici isteği, 18 oy
- [25] https://github.com/mpv-player/mpv/issues/6575 — gömme arayüzüne Vulkan isteği, 12 oy
- [26] https://github.com/mpv-player/mpv/issues/8283 — 360° video isteği, 13 oy
- [27] https://github.com/mpv-player/mpv/issues/6137 — kare ikileme isteği, 17 oy
- [28] https://github.com/mpv-player/mpv/issues/15581 — Fade-In ve Fade-Out isteği, 13 oy
- [29] https://github.com/iina/iina/issues/681 — yer imi isteği, 52 oy
- [30] https://github.com/iina/iina/issues/3208 — yakınlaştırma isteği, 23 oy
- [31] https://github.com/iina/iina/issues/3548 — Dolby Atmos isteği, 24 oy
- [32] https://github.com/iina/iina/issues/1989 — klip kırpma isteği, 30 oy
- [33] https://code.videolan.org/videolan/vlc/-/work_items/25003 — altyazıya göre gezinme isteği, 5 oy
- [34] https://code.videolan.org/videolan/vlc/-/work_items/20850 — sessizliği atlama isteği, 4 oy
- [35] https://code.videolan.org/videolan/vlc/-/work_items/18088 — daha iyi HDR desteği isteği, 3 oy
- [36] https://t1.daumcdn.net/potplayer/PotPlayer/v4/Update2/UpdateEng.html — PotPlayer güncelleme günlüğü (alt ajan)
- [37] https://www.gomlab.com/gomplayer-media-player/release-note — GOM Player sürüm notu (alt ajan)
- [38] https://www.gomlab.com/gomplayer-media-player/ — GOM Player ürün sayfası (alt ajan)
- [39] https://firecore.com/releases — Infuse sürüm notları (alt ajan)
- [40] https://firecore.com/infuse — Infuse ürün sayfası (alt ajan)
- [41] https://www.electronic.us/products/elmedia/ — Elmedia ürün sayfası (alt ajan)
- [42] https://movistprime.com/ — Movist Pro ürün sayfası (alt ajan)
- [43] https://movistprime.com/changelog.html — Movist Pro değişiklik listesi (alt ajan)
- [44] https://github.com/mpvnet-player/mpv.net/releases/tag/v7.1.2.0 — mpv.net sürümü
- [45] https://github.com/mpvnet-player/mpv.net/blob/main/src/MpvNet/InputHelp.cs — mpv.net menü ve komut tanımları (alt ajan)
- [46] https://github.com/mpvnet-player/mpv.net/blob/main/src/MpvNet.Windows/Resources/editor_conf.txt — mpv.net ayar düzenleyici (alt ajan)
- [47] https://github.com/mpvnet-player/mpv.net/blob/main/docs/manual.md — mpv.net el kitabı: `--remember-audio-device` 373, `--remember-volume` 377
- [48] https://github.com/smplayer-dev/smplayer/releases/tag/v26.8.29 — SMPlayer 26.8.29 (alt ajan)
- [49] https://www.smplayer.info/en/info — SMPlayer özellik sayfası (alt ajan)
- [50] https://github.com/celluloid-player/celluloid/blob/HEAD/src/celluloid-shortcuts-dialog.c — Celluloid kısayol listesi (alt ajan)
- [51] https://github.com/celluloid-player/celluloid/releases — Celluloid sürüm notları (alt ajan)
- [52] https://haruna.kde.org/ — Haruna sitesi (alt ajan)
- [53] https://github.com/KDE/haruna/tree/HEAD/src/settings — Haruna ayar tanımları ve sürüm dosyası (alt ajan)
- [54] https://github.com/huynhsontung/Screenbox/releases — Screenbox sürüm notları; 0.19.0 gövdesi doğrudan okundu
- [55] https://github.com/huynhsontung/Screenbox/blob/HEAD/Screenbox/Strings/en-US/Resources.resw — Screenbox arayüz dizeleri (alt ajan)
- [56] https://github.com/tsl0922/ImPlay/blob/HEAD/resources/romfs/lang/en-US.json — ImPlay arayüz dizeleri (alt ajan)
- [57] https://github.com/GNOME/showtime/blob/HEAD/data/org.gnome.Showtime.metainfo.xml.in — GNOME Showtime sürüm notları (alt ajan)
- [58] https://github.com/Rafostar/clapper/tree/HEAD/src/bin/clapper-app/ui — Clapper kısayol, hareket ve tercih tanımları (alt ajan)
- [59] https://blogs.windows.com/windows-insider/2021/11/16/new-media-player-for-windows-11-begins-rolling-out-to-windows-insiders/ — yeni Windows Media Player duyurusu (alt ajan, arama özetinden)
- [60] https://github.com/smplayer-dev/smplayer/issues/240 — zaman çubuğu önizleme isteği, 18 oy
- [61] https://github.com/celluloid-player/celluloid/issues/390 — önizleme isteği, 17 oy
- [62] https://github.com/celluloid-player/celluloid/issues/385 — atlama adımı isteği, 10 oy
- [63] https://github.com/celluloid-player/celluloid/issues/822 — kaldığı yeri hatırlama isteği, 7 oy
- [64] https://github.com/huynhsontung/Screenbox/issues/407 — HDR soluk görünüyor şikâyeti, 5 oy
- [65] https://github.com/huynhsontung/Screenbox/issues/29 — altyazı özelleştirme isteği, 5 oy
- [66] https://github.com/mpvnet-player/mpv.net/issues/724 — yapay zekâ ile büyütme isteği, 8 oy
- [67] https://github.com/Rafostar/clapper/issues/30 — pencereyi video oranına kilitleme isteği, 6 oy
- [68] https://support.apple.com/guide/quicktime-player/welcome/mac — QuickTime Player kılavuzu (alt ajan; yalnız başlıklar)
- [69] https://github.com/tomasklaen/uosc/releases — uosc sürümleri

## Komşu Kola Not

Çalma listesi, kitaplık, ağ ve kabuk kollarına düşenler: AirPlay, Chromecast ve DLNA istekleri (IINA 527, 307 ve 110 oy; K10), yt-dlp ve SponsorBlock (K1), klasörde önceki ve sonraki dosya (VLC 6 oy), sonraki dosyayı önbelleğe alma (mpv 21 oy), listeyi kendiliğinden kaydetme (Celluloid 9 oy), ReplayGain ve kesintisiz geçiş (yalnız ses; MPC-HC 2.8.2, Haruna 1.7.0, IINA 1.4.2), .LRC şarkı sözü (Screenbox 8 oy), ses dosyasında kapak ve dalga biçimi (Clapper 6 oy), eklenti mağazası (IINA 1.5.0), oynatma bitince eylemler ve web uzaktan denetimi (MPC-HC), AppleScript ve Quick Look (IINA), oyun kumandası (Screenbox).
