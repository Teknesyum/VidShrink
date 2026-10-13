# Ayarlar Sekmesi, Hakkında Bölümü Ve Uygulama Kabuğu — Pazar Notları

13 Ekim 2026. Kod okuması `origin/main` 9de48d8b. Eksik tablosu aynı klasörde `ayarlar-kabuk-eksikler.md`.

Bu kol yalnız Ayarlar sekmesine, Hakkında bölümüne ve uygulama kabuğuna (başlık çubuğu, pencere, dil, görünüm, bildirim, tanılama) bakar. Kurulum, güncelleme mekaniği, paket kanalları, imza, komut satırı ve sağ tık menüsünün tekniği komşu kolundur.

## Envanter

Her satır bugün kodda olanı söyler. Yol bir kez tam yazılır, aynı maddede sonraki satır numaraları o dosyaya aittir.

### Ayarlar Sekmesi: On İki Kart

Sekme `src/VidShrink.App/MainWindow.axaml:1504` (`TabSettings`), kart duvarı `:1512` (`SettingsCards`).

- **Genel** `src/VidShrink.App/MainWindow.axaml:1518`: dil `:1536`, tema (palet) `:1547`, varsayılan hedef MB `:1554`, "Gelişmiş açık gelsin" `:1561`, kaydediciyle ilgili `ChkFollowRecording` anahtarı `:1566`, iş bitince bildir `:1571`, ffmpeg yolu otomatik ya da elle `:1585–1590`, yol hatası `:1592`.
- **Tanılama Günlüğü** `:1597`: tek düğme, "Günlüğü aç" `:1601`.
- **Çıktı** `:1609`: klasör kipi `:1616–1621`, ad deseni `:1629`, deseni sıfırla `:1630`, canlı önizleme `:1632`, eşzamanlı iş sayısı `:1645` (K6).
- **Kabuk Menüsü** `:1650`: sağ tık "Aç" ve "Küçült" kutuları `:1655–1656`, "varsayılan uygulama yap" `:1661`.
- **Altyazı** `:1667`: OpenSubtitles anahtarı `:1680`, kullanıcı adı `:1697`, parola `:1702`, giriş ve çıkış `:1707–1708`. Ayar yedeği yapılırsa bu alanlar dışarıda kalmalı.
- **Güncelleme** `:1715`: yalnız otomatik güncelleme anahtarı `:1719` ve etki metni `:1721`.
- **Oynatıcı Gelişmiş Paneli** `:1728`: görüntü, ses ve altyazı biçimi seçenekleri (`src/VidShrink.App/Locales/en/advanced.json:2–31`), donanım çözme anahtarı `:16`.
- **Düzenleyici Kısayolları** `src/VidShrink.App/MainWindow.axaml:1730` ve **Oynatıcı Kısayolları** `:1732`.
- **Hakkında** `:1734`: aç ve kapa `:1736`, gövde `:1745`, sistem durumu `:1762`, platformlar `:1765`. Dizgiler `src/VidShrink.App/Locales/en/main.json:395–415`, sürüm satırı `:412`.
- **Paylaşım** `src/VidShrink.App/MainWindow.axaml:1770` (komşu kolun konusu).
- **Sıfırlama** `:1814`: düğme `:1816`, onay `:1821–1826`.

### Başlık Çubuğu Ve Pencere

- Başlık çubuğu `src/VidShrink.App/MainWindow.axaml:1838`: güncelleme rozeti `:1854`, hızlı dil değiştirici `:1864`, destek `:1866`, GitHub bağlantısı `:1873`, küçült, büyüt ve kapat `:1882–1893`.
- Pencere 1560×1060 açılır, en az 1136×720 `:12`; her açılışta ekran ortası `:14`.
- Pencere konumu ve boyutu saklanmıyor. `layout.json` yalnız önizleme ayırıcısını tutar (`src/VidShrink.App/MainWindow.axaml.cs:58`, `:3123–3125`).
- Sürükle-bırak var `src/VidShrink.App/MainWindow.axaml.cs:216–219`. Tek örnek (ikinci açılışın birinciye devretmesi) var `src/VidShrink.App/Program.cs:158`.
- Ana pencerenin tek tuş işleyicisi `src/VidShrink.App/MainWindow.PencereKipi.cs:25`, yalnız Esc tuşunu dinler `:41`. `KeyBinding`, `KeyGesture`, `HotKey` ana pencerede 0 eşleşme.

### Ayar Dosyaları

- Ana dosya `settings.json`: `src/VidShrink.Core/UpdateCheck.cs:421` (`UpdateSettings`); alanlar `AutoUpdate` `:433`, `FastGpu` `:446`, `Language` `:447`, `TargetMb` `:448`, `ShareTarget` `:473`, `ShareRetention` `:474`. Yol `:477`, ölçüm için `VIDSHRINK_SETTINGS_PATH` ortam değişkeni `:476–481`.
- Okunamayan ayar dosyası sessizce varsayılana düşer; yedek alınmaz, kullanıcıya söylenmez `src/VidShrink.App/AppSettings.cs:163–168`.
- "Tümünü sıfırla" on iki dosyayı siler `src/VidShrink.App/AppDataReset.cs:17–31`; çağrı `src/VidShrink.App/MainWindow.axaml.cs:1529`, `:1535`, `:1545`.
- Yalnız ön ayar dışa ve içe aktarılır `src/VidShrink.App/MainWindow.OnAyar.cs:301` (içe), `:385` (dışa). Ayarın tümü için dışa aktarma yok.

### Kısayollar

- Oynatıcı ve düzenleyici kısayolları düzenlenebilir: `src/VidShrink.App/Playback/ShortcutBindings.cs:20–22`, dosya `shortcut-bindings.json` `:31`. Yalnız varsayılandan ayrılan satır yazılır; çakışmada yeni atama kazanır.
- Tablo `src/VidShrink.App/Playback/ShortcutTable.cs:69` (kurulum), yeniden atanabilir satır `:79`, varsayılana dön `:63–67`. Ayrıca `src/VidShrink.App/Playback/Keymap.cs:219`, `src/VidShrink.App/Editing/EditorShortcutsPanel.axaml.cs:19`.
- Kaydedicinin genel kısayolları (uygulama arkadayken de çalışan tuşlar) yeniden atanabilir: `src/VidShrink.App/Recorder/RecorderHotkeys.cs:33`, `:145`, `:184`; `src/VidShrink.App/Recorder/RecorderView.Kisayol.cs:54`, `:111`, `:129–132`.
- Kabuk düzeyi kısayol (sekme değiştirme, Ayarlar'ı açma) ve bunların tablosu yok.

### Dil

- 42 dil klasörü `src/VidShrink.App/Locales`; liste `src/VidShrink.App/Localization/Strings.cs:62–95`; sağdan sola diller, harf aralığı kümesi ve sistem dilini izleme `:100–123`.
- Sağdan sola dört dil (`ar`, `fa`, `he`, `ur`); düzen çevirme `src/VidShrink.App/MainWindow.axaml.cs:1017–1024`, `src/VidShrink.App/Localization/YonYalitimi.cs:17–22`.
- Çeviri katkı yolu yalnız "Open an issue" `README.md:346–348`; `CONTRIBUTING.md:119–120`. Defter: "40 dilin çevirisi okunmadı" `.claude/acik.md:20`.

### Görünüm

- 36 palet (25 koyu, 11 açık): `src/VidShrink.App/Themes/PaletteCatalog.cs:51–61`, varsayılan `:45`, açık ve koyu ayrımı `:96`.
- Açılış koyu görünüme sabit `src/VidShrink.App/App.axaml:5`. Sistem temasını izleme yok.
- Yazı boyutları sabit belirteç `src/VidShrink.App/Themes/Theme.axaml:219–222`. Arayüz ölçeği ayarı yok.
- Karşıtlık ölçümleri `docs/olcumler/palet-karsitligi.md` (18 Eylül 2026) ve `docs/olcumler/dugme-kenari-karsitlik.md`. Defter: 13 palette düğme kenarı, giriş kutusu ve kaydırıcı izi 3:1 altında `.claude/acik.md:17`, `:23`, `:62`, `:75`.

### Erişilebilirlik

- Erişilebilir ad (ekran okuyucunun, yani ekranı sesli okuyan yardımcı programın bir düğme için söylediği ad): `AutomationProperties.` axaml dosyalarında 252, kod dosyalarında 31 kez. Ayarlar ve başlık çubuğu aralığında 16; ilk `src/VidShrink.App/MainWindow.axaml:1530`, son `:1890`.
- Odak halkası `src/VidShrink.App/Themes/Controls.axaml:13`.
- Azaltılmış hareket yalnız Windows'ta izleniyor `src/VidShrink.App/MainWindow.axaml.cs:374–390`; Windows dışında hep izinli `:379`. Elle anahtar yok.
- 0 eşleşme: `LabeledBy`, `AccessKey`, `AcceleratorKey`, `TabIndex=`, `KeyboardNavigation.`, `IsTabStop`, `HighContrast`, `ForcedColors`, `SPI_GETHIGHCONTRAST`.
- `docs/olcumler` altında ekran okuyucu ya da klavyeyle tam kullanım ölçümü yok. `k8-odak-korumalari-2026-09-19.md` ve `k19-d0-ortak-odak.md` klavye odağını değil "geçerli medya" nesnesini ölçer.
- Uyarı durumu renkle değil simge ve ağırlıkla ayrılır `AGENTS.md:16` (bilinçli karar, eksik değil).

### Bildirim, Kuyruk Sonu Ve Başarım

- İş bitti bildirimi yalnız Windows `src/VidShrink.App/IsBittiBildirimi.cs:64`, `:97`; pencere yanıp sönmesi ve tepsi balonu `:124–126`. Defter `.claude/acik.md:25`, `:50`.
- Kuyruk sonu dört seçenek: hiçbir şey, klasörü aç, uyut, bilgisayarı kapat `src/VidShrink.App/QueueEndActions.cs:9–15`.
- İş başlamadan boş alan denetimi `src/VidShrink.App/MainWindow.axaml.cs:4514`, `src/VidShrink.Ffmpeg/DiskSpaceGuard.cs:5`.
- Süreç önceliği yalnız güncelleme sürecinde düşürülüyor `src/VidShrink.App/MainWindow.KapanistaGuncelleme.cs:239`, `src/VidShrink.App/Program.cs:220`. Kodlama için ayar yok.
- Tepsi simgesi yalnız kaydedicide `src/VidShrink.App/Recorder/RecorderTray.cs:14–29`.
- Kaydedici yalnız ilk grafik bağdaştırıcısının ekranlarını sayar `src/VidShrink.App/Recorder/DdaOutputs.cs:41`, `src/VidShrink.Core/RecorderArguments.cs:1008`.
- Kodlama sürerken uykuyu engelleyen çağrı yok: `SetThreadExecutionState`, `PowerCreateRequest`, `caffeinate`, `systemd-inhibit`, `IOPMAssertion` 0 eşleşme.
- Pil durumu okunmuyor: `battery`, `PowerLineStatus`, `SystemPowerStatus` 0 eşleşme.

### Tanılama, Hakkında Ve Güven

- Günlük `src/VidShrink.Core/Gunluk.cs:7–15`; 1 MiB'de devrilir `:19`, dosya `vidshrink.log` `:28`, tek yedek `:31`. Açma `src/VidShrink.App/MainWindow.axaml.cs:1511`. Uygulama içi görüntüleyici, panoya kopyalama, hata bildirme düğmesi yok.
- Hakkında'da lisans, gizlilik ve sürüm notu satırı yok (`src/VidShrink.App/Locales/en/main.json:395–415`).
- `LICENSE` ve `THIRD-PARTY-NOTICES.md` depoda, README'den bağlı `README.md:405`. Gizlilik beyanı ve ağ isteği listesi `README.md:413–431`; "No telemetry" `README.md:12`.
- Güncelleme rozeti tıkı `src/VidShrink.App/MainWindow.axaml.cs:2899–2915`; otomatik güncelleme açıkken elle denetim koşmaz, rozet gizlenir `:2919–2923`. Durum dizgileri `src/VidShrink.App/Locales/en/main.json:26–39`.
- `.github/` altında yalnız `workflows/`; hata şablonu, `SECURITY`, `SUPPORT`, `PRIVACY` dosyası yok.
- İlk açılış: yalnız kaydedici sihirbazı `src/VidShrink.App/Recorder/RecorderSettings.cs:271–273` ve varsayılan uygulama önerisi `src/VidShrink.App/Integration/DefaultAppSuggestionBar.cs`, `src/VidShrink.App/MainWindow.axaml.cs:366–371`.

### Önceki Taramanın Bu Kola Düşen Kalemleri

`docs/piyasa/tarama-2026-10-05.md:227–250` içinden: 4 (kaydedici kısayolları) yapılmış, 7 (oynatıcıda donanım çözme anahtarı) yapılmış, 11 (iş bitince bildirim) yalnız Windows'ta yapılmış. 20 (dosya seçicide çoklu seçim) bu kolda denetlenmedi.

## Rakipler

Sürümler 13 Ekim 2026'da depo ya da resmi sayfadan okundu.

| Ad | Sürüm | Tarih | Tür | URL |
|---|---|---|---|---|
| HandBrake | 1.11.2 | 2026-06-07 | açık kaynak, dönüştürücü | https://github.com/HandBrake/HandBrake/releases |
| OBS Studio | 32.2.2 | 2026-08-14 | açık kaynak, kayıt ve yayın | https://github.com/obsproject/obs-studio/releases |
| VLC | 3.0.23 | 2026-01-08 | açık kaynak, oynatıcı | https://images.videolan.org/news.html |
| PotPlayer | 1.7.22980 (doğrulanmadı) | 2026-07-06 (ikincil kaynak) | ücretsiz, kapalı kaynak, oynatıcı | resmi kaynak okunamadı |
| ShareX | v21.0.0 | 2026-07-03 | açık kaynak, ekran yakalama | https://github.com/ShareX/ShareX/releases |
| mpv.net | v7.1.2.0 | 2026-01-09 | açık kaynak, oynatıcı (mpv arayüzü) | https://github.com/mpvnet-player/mpv.net |
| mpv | v0.41.0 | 2025-12-21 | açık kaynak, oynatıcı çekirdeği | https://github.com/mpv-player/mpv/releases |
| IINA | v1.5.0 | 2026-10-03 (GitHub), 2026-09-28 (blog) | açık kaynak, macOS oynatıcı | https://github.com/iina/iina/releases |
| Shotcut | v26.9.27 | 2026-09-28 | açık kaynak, kurgu | https://github.com/mltframework/shotcut/releases |
| LosslessCut | v3.69.0 | 2026-06-04 | açık kaynak, kayıpsız kesim | https://github.com/mifi/lossless-cut/releases |
| DaVinci Resolve | 21.0.4 | "Updated August 04, 2026" | ücretsiz ve ücretli sürümlü, kurgu | https://www.blackmagicdesign.com/support/content/readme/f1b3986a11634684b538ff3d1d4b55d6 |
| Shutter Encoder | 20.4 | 2026-09-28 | açık kaynak, dönüştürücü | https://github.com/paulpacifico/shutter-encoder/releases |
| PowerToys (ölçüt) | v0.101.2362.0; önizleme v0.102.2851.0 | 2026-08-25; 2026-10-12 | Microsoft, yardımcı araç takımı | https://github.com/microsoft/PowerToys/releases |
| Windows Terminal (ölçüt) | v1.25.2733.0; önizleme v1.26.2734.0 | 2026-10-02 | Microsoft, terminal | https://github.com/microsoft/terminal/releases |

On dört ürün. PotPlayer ve DaVinci Resolve'un ayar yüzü resmi kaynaktan okunamadı; ikisi de matriste "doğrulanmadı" olarak durur. mpv'nin kendi ayar ekranı yok; ayar yüzü için mpv.net'e bakıldı.

## Özellik Matrisi

### Öncelik Nasıl Verildi

- PowerToys ve Windows Terminal ölçüt ürünlerdir ama sayıma girer; görev onları ayar yüzünün ölçütü olarak verdi.
- "Ana iş" bu kolda: ayarın korunması ve taşınması, uygulamanın dili ve görünümü, sorun çıktığında tanı ve bilgilendirme.
- "Kenar" sayılanlar: tek platforma bakan kalem, özü bizde zaten olan işin üstüne gelen kolaylık (arama, günlük görüntüleyici, bildirim sesi), komşu kolun mekaniğine dayanan ayar yüzü (güncelleme kanalı), belge işi (kılavuz).
- Üç ve daha çok üründe belgeli olduğu halde kenar sayılıp 2 verilenler: AYR-08, AYR-09, AYR-10, AYR-14, AYR-16, AYR-18, AYR-20, AYR-21, AYR-22, AYR-23, AYR-24, AYR-25. Kanıt yeterli; sahip bunları 1'e çekebilir.
- Yönergeye dayanan kalemlerde rakip sayısı yerine yönergenin düzeyi kullanıldı: üç yönergede ortak ve WCAG'nin en temel düzeyinde olan 2 (AYR-17), tek yönergedeki öneri 3 (AYR-32, AYR-33). WCAG, web ve uygulama erişilebilirliğinin uluslararası ölçüt listesidir.
- AYR-31 (eşitleme) oyla kanıtlı ama oylar video dışı iki ölçüt üründen geliyor ve hiçbir üründe uygulanmış hali görülmedi; bu yüzden 3.

### Matris

"Belgeli" sütunu yalnız resmi kaynakta görüleni sayar. Boş kalan ürün "yok" demek değildir, "doğrulanmadı" demektir.

| Özellik | VidShrink | Belgeli Olduğu Ürünler | Not |
|---|---|---|---|
| Arayüz dili seçimi | var, 42 dil | PowerToys 22 dil [1]; HandBrake 1.11.0 [9][73]; ShareX 23 dil [18]; OBS Studio [72]; Windows Terminal [76] | VLC doğrulanmadı |
| Sağdan sola düzen | var, 4 dil | HandBrake [73]; IINA v1.5.0 düzeltmesi [11] | bizde ekran görüntülü deneme yok |
| Çeviri platformu | yok | HandBrake, mpv.net, Shotcut: Transifex [10][7][58]; OBS Studio, IINA: Crowdin [15][12]; LosslessCut: Weblate [14] | VLC doğrulanmadı |
| Sistem temasını izleme | yok | PowerToys [1]; mpv.net [7]; Windows Terminal, varsayılan değer [76] | HandBrake'te tema kipi seçeneği var [73], "sistem" değeri okunamadı; ShareX yalnız görüntü düzenleyicide [18] |
| Yazı boyutu ya da ölçek | yok | OBS Studio "Font Size" ve "Density" [72]; Shotcut ortam değişkeni [30] | ShareX 20.1.0 yalnız yüksek DPI düzeltmesi [18] |
| Yüksek karşıtlık | yok | Windows Terminal [5]; PowerToys [6] | video rakiplerinde doğrulanmadı |
| Renk körlüğü seçeneği | uyarı simgeyle ayrılıyor | OBS Studio "Color Blind Alternative" [72] | |
| Azaltılmış hareketi izleme | yalnız Windows | IINA v1.4.0 [11]; LosslessCut v3.67.0 [13] | |
| Canlandırmaları elle kapatma | yok | LosslessCut [13][66]; Shutter Encoder [22]; Windows Terminal `disableAnimations` [76] | |
| Ekran okuyucu çalışması | adlar var, deneme yok | HandBrake 1.11.1 [9]; LosslessCut v3.67.0 [13]; OBS Studio [35] | OBS Studio'da açık hata [51] |
| Kısayol düzenleme | var: oynatıcı, düzenleyici, kaydedici | VLC [26]; OBS Studio [72]; mpv.net [7]; LosslessCut [13]; PowerToys çakışma algılama [6] | DaVinci Resolve doğrulanmadı |
| Kabuk düzeyi kısayollar | yalnız Esc | VLC [26]; OBS Studio [72]; LosslessCut [13] | |
| Kısayol tablosunda arama | yok | OBS Studio "Filter" ve "Filter by Hotkey" [72] | |
| Ayarlarda arama | yok | PowerToys [1][6]; Windows Terminal v1.25 [5]; IINA v1.3.5, gelişmiş ayarlarda [11]; mpv.net [7] | VLC doğrulanmadı |
| Komut paleti | yok | Windows Terminal [3] | mpv.net'te istek [47] |
| Ayarı dışa ve içe aktarma | yalnız ön ayar | PowerToys [1]; ShareX [18] | ShareX ekranı görülmedi, yalnız değişiklik günlüğü satırı |
| Ayar dosyası yedeği | yok, bozulan dosya sessizce sıfırlanır | PowerToys, güncelleme öncesi yedek [1] | |
| Ayarı sıfırlama | var, 12 dosya | VLC [25] | PowerToys'ta açık istek [69] |
| Taşınabilir kip | yalnız ortam değişkeni | Windows Terminal [4]; mpv.net [7]; ShareX [17][19]; OBS Studio [16]; HandBrake [55] | Shotcut doğrulanmadı |
| Pencere konumunu hatırlama | yok | mpv.net [7]; LosslessCut [46]; Windows Terminal [37][61] | |
| Başlangıçta çalıştırma | yok | PowerToys [1]; ShareX [63] | |
| Tepsiye küçültme | yalnız kaydedici simgesi | OBS Studio [72][62]; HandBrake [73]; PowerToys tepsi simgesi [1] | |
| İş bitti bildirimi | yalnız Windows | HandBrake [74]; LosslessCut v3.62.0 [13][67] | |
| Bildirim sesi | yok | HandBrake [74]; ShareX 17.0.0 [18]; Shutter Encoder [22] | |
| Bitince özel komut | yok | HandBrake [9][74] | |
| E-posta bildirimi | yok | Shutter Encoder [22] | |
| Süreç önceliği | yok | HandBrake [52]; OBS Studio [72]; Shotcut [53] | |
| İş parçacığı sınırı | yok | Shutter Encoder [22] | |
| Grafik bağdaştırıcısı seçimi | yok | Shotcut v26.8.1 [20]; Shutter Encoder [22] | |
| Uykuyu engelleme | yok | HandBrake [52] | |
| Pilde ve disk azalınca duraklatma | yalnız iş öncesi disk denetimi | HandBrake [52][56] | |
| Geçici klasör ayarı | yok | doğrulanmadı | hiçbir üründe resmi kaynaktan okunamadı |
| Günlük görüntüleyici | dosyayı dış programda açar | HandBrake [29]; IINA v1.5.0 [11]; OBS Studio [72] | |
| Günlük düzeyi ve kodlama başına günlük | yok | HandBrake [52][29] | |
| Hata bildirme yolu | yalnız depo bağlantısı | PowerToys: hata paketi ve hazır doldurulmuş bildirim [1]; OBS Studio: günlük ve çökme raporu yükleme [72][59]; LosslessCut: depoda hata şablonu [60] | |
| Yenilikler ekranı | yok | PowerToys [1]; OBS Studio [72][33]; LosslessCut v3.67.0 [13][65] | |
| Lisanslar sayfası | yalnız depoda | OBS Studio [64] | HandBrake, ShareX ve Shutter Encoder içeriği doğrulanmadı |
| Gizlilik beyanı uygulamada | yalnız README'de | PowerToys [1] | Shotcut yalnız web sitesinde [30] |
| Seçmeli tanılama verisi | yok, bilinçli | PowerToys [1] | |
| Güncelleme kanalı | yok | PowerToys [2]; ShareX 16.1.0 [18]; OBS Studio [34][72] | |
| Elle güncelleme denetimi | yalnız otomatik kapalıyken rozetle | OBS Studio "Check For Updates" [72] | HandBrake'te denetim sıklığı seçimi [73] |
| İlk açılış karşılaması | yalnız kaydedici sihirbazı | IINA v1.5.0 [11][12]; OBS Studio [57]; PowerToys [54]; ShareX 20.1.0 [18] | |
| Kılavuz ve yardım girişi | yalnız README | HandBrake [8]; VLC [25]; OBS Studio [16][72]; Shutter Encoder [22] | |

Bizim güçlü olduğumuz yerler: 42 dil ve dört sağdan sola dil, 36 palet, üç yüzde düzenlenebilir kısayol, çıktı adı deseni ve canlı önizlemesi, on iki dosyayı kapsayan sıfırlama.

## Kullanıcı Talepleri

Oy, konunun ilk iletisindeki "+1" sayısıdır; 13 Ekim 2026'da okundu. Kapanış nedeni de aynı okumadan.

| İstek | Ürün Ve Konu | Oy | Durum | URL |
|---|---|---|---|---|
| Pencere düzenini saklama ve geri yükleme | Windows Terminal #766 | 254 | tamamlandı | https://github.com/microsoft/terminal/issues/766 |
| Ayarların cihazlar arası taşınması | Windows Terminal #2933 | 151 | açık | https://github.com/microsoft/terminal/issues/2933 |
| Uygulama içi dil seçimi | PowerToys #7936 | 119 | tamamlandı | https://github.com/microsoft/PowerToys/issues/7936 |
| Ayar eşitleme | PowerToys #3381 | 84 | açık | https://github.com/microsoft/PowerToys/issues/3381 |
| Yedekleme ve geri yükleme | PowerToys #586 | 74 | tamamlandı | https://github.com/microsoft/PowerToys/issues/586 |
| Taşınabilir sürüm | PowerToys #598 | 55 | tamamlandı | https://github.com/microsoft/PowerToys/issues/598 |
| Yüksek DPI desteği | ShareX #5312 | 18 | not edilmedi | https://github.com/ShareX/ShareX/issues/5312 |
| Klavye kısayolları | LosslessCut #254 | 10 | açık | https://github.com/mifi/lossless-cut/issues/254 |
| Ayarları hatırlama | LosslessCut #371 | 9 | açık | https://github.com/mifi/lossless-cut/issues/371 |
| Konusu not edilmedi | mpv.net #75 | 9 | not edilmedi | https://github.com/mpvnet-player/mpv.net/issues/75 |
| Pencere boyutu ve konumu | LosslessCut #44 | 8 | tamamlandı | https://github.com/mifi/lossless-cut/issues/44 |
| Ayarları sıfırlama | PowerToys #3822 | 6 | açık | https://github.com/microsoft/PowerToys/issues/3822 |
| Konusu not edilmedi | ShareX #6951 | 4 | açık | https://github.com/ShareX/ShareX/issues/6951 |
| Komut paleti | mpv.net #701 | 4 | not edilmedi | https://github.com/mpvnet-player/mpv.net/issues/701 |
| Sistem temasına göre otomatik tema | ShareX #6679 | 3 | kapatıldı, yapılmayacak | https://github.com/ShareX/ShareX/issues/6679 |
| Hareketi azaltma ayarı | LosslessCut #2580 | 3 | tamamlandı | https://github.com/mifi/lossless-cut/issues/2580 |
| Taşınabilir kip | HandBrake #267 | 3 | tamamlandı | https://github.com/HandBrake/HandBrake/issues/267 |
| Konusu not edilmedi | LosslessCut #726 | 2 | açık | https://github.com/mifi/lossless-cut/issues/726 |
| Ekran okuyucu hatası | OBS Studio #11402 | 2 | açık | https://github.com/obsproject/obs-studio/issues/11402 |
| İş bitti bildirimi | LosslessCut #1510 | 1 | tamamlandı | https://github.com/mifi/lossless-cut/issues/1510 |
| Uygulama içi dil seçimi | IINA #5841 | 1 | açık | https://github.com/iina/iina/issues/5841 |
| Yenilikler ekranı | LosslessCut #669 | 0 | tamamlandı | https://github.com/mifi/lossless-cut/issues/669 |
| Çeviri isteği | ShareX #8845 | 0 | açık | https://github.com/ShareX/ShareX/issues/8845 |

Okuma: büyük sayılar iki ölçüt üründen geliyor. Video araçlarında ayar konuları az oy topluyor (en çok 18). En çok oy alan üç konu pencere düzenini hatırlama, ayarın taşınması ve dil seçimi; sonuncusu bizde var.

## Son İki Yılın Yenilikleri

Ekim 2024 – Ekim 2026 arası. Tarihi not edilmeyen sürümde yalnız sürüm numarası var.

- **PowerToys** [1][6]: ayarlarda arama (önizlemede yakın eşleşmeli arama), kısayol çakışma algılama, tek ayarı açan bağlantı (v0.101.2712.0, 2026-09-30).
- **Windows Terminal** [5]: v1.25'te yeni ayar araması (kararlı 2026-10-02); v1.26 önizlemede yüksek karşıtlıkta ayar ekranı düzeltmesi (#20098).
- **HandBrake** [9]: 1.10.0 kuyruk bitince özel eylem ve bakımsız çevirilerin kaldırılması (Türkçe dahil); 1.11.0 arayüz dili seçeneği; 1.11.1 ekran okuyucu düzeltmesi. Pilde duraklatma ve bildirimler 1.8.0'dan (2024, iki yıldan eski).
- **IINA** [11][12]: v1.4.0 sistemin "hareketi azalt" ayarına uyum; v1.5.0 (2026-10-03) yeni Ayarlar penceresi, günlük görüntüleyici (#6041), karşılama penceresi (#6410), sağdan sola düzeltmesi. Gelişmiş ayarlarda arama v1.3.5'ten.
- **LosslessCut** [13]: v3.62.0 işletim sistemi bildirimi; v3.63.0 çakışan tuşu kendiliğinden yeniden atama; v3.67.0 erişilebilirlik çalışması (#2561), hareketi azaltma ayarı (#2580), "What's new" (#669).
- **ShareX** [18]: 16.1.0 güncelleme kanalı; 17.0.0 bildirim sesleri ve özel ses; 19.0.1 dışa aktarılan ayar dosyasının adında bilgisayar adı; 20.0.2 görüntü düzenleyicide sistem teması; 20.1.0 yüksek DPI ve ilk kullanımda seçici; 21.0.0 (2026-07-03) 23 dilin tümü yüzde 100.
- **OBS Studio** [72]: 32.2.2 dizgilerinde yazı boyutu ve sıkılık ayarı, renk körlüğü takımı, güncelleme kanalı, güvenli kip, günlük ve çökme raporu yükleme var. Hangi sürümde geldikleri okunmadı.
- **Shotcut** [20]: v26.6.25 güvenli kip; v26.8.1 Ayarlar'da grafik bağdaştırıcısı seçimi.
- **Shutter Encoder** [21]: 20.4 (2026-09-28) Ayarlar'a "Information" bölümü.
- **VLC** [31]: 3.0.23 (2026-01-08) Windows ve Linux'a koyu kip seçeneği.

Eğilim: pazar son iki yılda dört yere yatırım yaptı. Ayar araması (PowerToys, Windows Terminal), erişilebilirlik düzeltmeleri (HandBrake, LosslessCut, IINA), yenilikler ekranı ve karşılama (LosslessCut, IINA), uygulama içi günlük (IINA).

## Doğrulanamayanlar

### Rakip Tarafı

- PotPlayer: ayar yüzü ve sürümün resmi kaynağı okunamadı; sürüm yalnız ikincil kaynaktan.
- DaVinci Resolve: klavye özelleştirme ve tercihler resmi kaynaktan okunamadı; 21.1.0 yayını doğrulanmadı.
- VLC: gelişmiş tercihlerde arama, arayüz dili seçimi ve çeviri platformu doğrulanmadı; 4.0 yayını bulunamadı.
- Shotcut: taşınabilir kip ve dil sayısı doğrulanmadı.
- ShareX: ayarı dışa ve içe aktarma ekranı görülmedi, yalnız değişiklik günlüğü satırı [18]; `getsharex.com/docs` 404 verdi.
- HandBrake: tema kipi seçeneği XAML'de var [73], değerleri arasında "sistem" olup olmadığı okunamadı (denenen dosya yolu 404 verdi). OBS Studio'da sistem temasını izleme de doğrulanmadı.
- Lisans içeriği: HandBrake ve ShareX Hakkında penceresi ile Shutter Encoder "Information" bölümü okunmadı.
- LosslessCut Weblate sayılarının (34 dil, yüzde 61,9) tarihi yok [14].
- OBS Studio'nun çeviri platformu Crowdin olarak not edildi ama hangi sayfada görüldüğü kaydedilmedi; AYR-04'teki [15] atfı kesin değil. Kalem OBS Studio sayılmadan da beş üründe belgeli (HandBrake, IINA, LosslessCut, mpv.net, Shotcut).
- Geçici klasör ayarı hiçbir üründe resmi kaynaktan okunamadı.
- PowerToys'ta en son kararlı sürüm: `releases/latest` v0.101.2362.0 (2026-08-25) döndürdü, sürüm listesinde v0.101.2712.0 (2026-09-30) da geçiyor.
- OBS Studio özelliklerinin geldiği sürümler okunmadı. Başlatma parametreleri sayfası 2021 tarihli [16]. Windows Terminal görünüm sayfası 2025-08-21'de güncellenmiş [76].
- WCAG 2.5.7, 2.5.8, 3.2.6 ve 4.1.2 metinleri okunmadı (sayfa 100000 karakterde kesildi).
- Üç iş kaydının konusu not edilmedi: ShareX #6951, LosslessCut #726, mpv.net #75.
- GitHub arama sınırı 17:26'da doldu; sonraki doğrulamalar ham dosya adresinden yapıldı.

### Bizim Tarafımız

- `ScaleSwitch` defterde geçiyor (`.claude/acik.md:101`), izlenen dosyalarda 0 eşleşme; ne olduğu bilinmiyor.
- Çıktı adı deseninin kabul ettiği belirteçler okunamadı.
- `ChkFollowRecording` anahtarının ne yaptığı okunmadı.
- Sağdan sola düzenin ekran görüntülü denemesi yok.
- 1136×720 en küçük pencerenin yüzde 150 ölçekli 1080p ekrana sığıp sığmadığı denenmedi.
- Gövde metni karşıtlığının 36 palette ölçülüp ölçülmediği belirsiz; `docs/olcumler/palet-karsitligi.md` başlığına göre yalnız neon dolgu üstündeki yazıyı ölçüyor.
- Görev çubuğunda ilerleme göstergesi: `ITaskbarList`, `SetProgressValue`, `TaskbarProgress` 0 eşleşme. Rakip tarafında yalnız HandBrake'in "başlık çubuğunda durum" seçeneği görüldü [73]; kanıt yetmediği için satır açılmadı.
- Tablodaki İş sütunu tahmindir.

## Komşu Kola Not

- Güncelleme kanalının mekaniği (ön sürüm yayını, manifest): AYR-09 yalnız Ayarlar'daki yüzü.
- Taşınabilir zip paketi ve bu kipte güncelleme: AYR-05 yalnız ayar konumu.
- `THIRD-PARTY-NOTICES.md` yalnız README ve iki `docs/` dosyasında anılıyor; paket betiklerinde ve uygulama kodunda geçmiyor. Kurucuda yalnız `VulkanRT-License.txt` var (`Install-VidShrink.ps1:411–446`).
- OBS Studio `--disable-updater` ve "Check File Integrity" (kurulu dosyaları doğrulama) [72].
- Paket kanalı istekleri: HandBrake #1722 AppImage (18 oy), ShareX #6625 Linux desteği (68 oy), IINA #2152 Mac App Store (30 oy).
- Dosya seçicide çoklu seçim (önceki taramanın 20. kalemi) bu kolda denetlenmedi.

## Kaynaklar

- [1] https://learn.microsoft.com/en-us/windows/powertoys/general — PowerToys Genel ayarları: tema, dil, başlangıçta çalıştırma, yedekleme ve geri yükleme, yenilikleri göster, tanılama ve geri bildirim, hata paketi
- [2] https://learn.microsoft.com/en-us/windows/powertoys/update-channel — PowerToys güncelleme kanalı (Stable, Insider)
- [3] https://learn.microsoft.com/en-us/windows/terminal/command-palette — Windows Terminal komut paleti
- [4] https://learn.microsoft.com/en-us/windows/terminal/distributions — Windows Terminal taşınabilir kip (`.portable`)
- [5] https://github.com/microsoft/terminal/releases — Windows Terminal sürümleri: v1.25 ayar araması, v1.26 yüksek karşıtlık düzeltmesi
- [6] https://github.com/microsoft/PowerToys/releases — PowerToys sürümleri: ayar araması, kısayol çakışması, tek ayara bağlantı
- [7] https://github.com/mpvnet-player/mpv.net/blob/main/docs/manual.md — mpv.net kılavuzu: ayar düzenleyici, tuş düzenleyici, `--dark-mode`, `portable_config`, pencere konumu, Transifex
- [8] https://handbrake.fr/docs/en/latest/ — HandBrake belgeleri (kılavuzun varlığı)
- [9] https://github.com/HandBrake/HandBrake/releases — HandBrake sürüm notları 1.8.0–1.11.2
- [10] https://handbrake.fr/docs/en/1.9.0/contributing/contribute.html — HandBrake katkı ve çeviri yolu (Transifex)
- [11] https://github.com/iina/iina/releases — IINA sürüm notları v1.3.5–v1.5.0
- [12] https://iina.io/blog/150-release/ — IINA 1.5.0 duyurusu: yeni Ayarlar penceresi, karşılama, günlük görüntüleyici
- [13] https://github.com/mifi/lossless-cut/releases — LosslessCut sürüm notları v3.62.0–v3.69.0
- [14] https://hosted.weblate.org/engage/losslesscut — LosslessCut çeviri durumu (Weblate)
- [15] https://github.com/obsproject/obs-studio/releases — OBS Studio sürüm notları
- [16] https://obsproject.com/kb/launch-parameters — OBS başlatma parametreleri: `--portable` (sayfa 2021 tarihli)
- [17] https://getsharex.com/docs/command-line-arguments — ShareX `-portable`
- [18] https://getsharex.com/changelog — ShareX değişiklik günlüğü 16.1.0–21.0.0
- [19] https://github.com/ShareX/ShareX/releases — ShareX sürümü ve portable zip
- [20] https://github.com/mltframework/shotcut/releases — Shotcut sürüm notları: grafik bağdaştırıcısı ayarı, güvenli kip
- [21] https://github.com/paulpacifico/shutter-encoder/releases — Shutter Encoder 20.4: Ayarlar'a "Information" bölümü
- [22] https://www.shutterencoder.com/documentation/ — Shutter Encoder ayarları
- [23] https://www.w3.org/TR/WCAG22/ — WCAG 2.2 ölçütleri (1.4.3, 1.4.4, 1.4.11, 2.1.1, 2.1.2, 2.3.3, 2.4.3, 2.4.7, 2.4.11, 2.4.13)
- [24] https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-overview — Microsoft erişilebilirlik genel bakışı
- [25] https://vlc-user-documentation.readthedocs.io/en/latest/settings/preferences.html — VLC tercihleri: basit ve gelişmiş görünüm, sıfırlama
- [26] https://wiki.videolan.org/Documentation:Hotkeys — VLC kısayol düzenleme
- [27] https://learn.microsoft.com/en-us/windows/apps/design/accessibility/keyboard-accessibility — Microsoft klavye erişilebilirliği: F6, erişim tuşları, odak
- [28] https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessible-text-requirements — Microsoft metin karşıtlığı ve metin ölçeği
- [29] https://handbrake.fr/docs/en/latest/help/activity-log.html — HandBrake etkinlik günlüğü
- [30] https://www.shotcut.org/FAQ/ — Shotcut SSS: ayar konumu, ölçek ortam değişkeni, gizlilik cümlesi
- [31] https://images.videolan.org/news.html — VLC 3.0.23 duyurusu
- [32] https://www.blackmagicdesign.com/support/content/readme/f1b3986a11634684b538ff3d1d4b55d6 — DaVinci Resolve 21.0.4 sürüm notu
- [33] https://github.com/obsproject/obs-studio/pull/8470 — OBS "What's New" penceresi
- [34] https://github.com/obsproject/obs-studio/pull/6907 — OBS güncelleme kanalı
- [35] https://github.com/obsproject/obs-studio/pull/4975 — OBS erişilebilirlik çalışması
- [36] https://github.com/microsoft/terminal/issues/2933 — istek: ayarların cihazlar arası taşınması
- [37] https://github.com/microsoft/terminal/issues/766 — istek: pencere düzenini saklama ve geri yükleme
- [38] https://github.com/microsoft/PowerToys/issues/3381 — istek: ayar eşitleme
- [39] https://github.com/microsoft/PowerToys/issues/586 — istek: yedekleme ve geri yükleme
- [40] https://github.com/microsoft/PowerToys/issues/598 — istek: taşınabilir sürüm
- [41] https://github.com/microsoft/PowerToys/issues/7936 — istek: uygulama içi dil seçimi
- [42] https://github.com/ShareX/ShareX/issues/5312 — istek: yüksek DPI desteği
- [43] https://github.com/ShareX/ShareX/issues/6951 — ShareX isteği (oy sayımı)
- [44] https://github.com/mifi/lossless-cut/issues/254 — istek: klavye kısayolları
- [45] https://github.com/mifi/lossless-cut/issues/371 — istek: ayarları hatırlama
- [46] https://github.com/mifi/lossless-cut/issues/44 — istek: pencere boyutu ve konumu
- [47] https://github.com/mpvnet-player/mpv.net/issues/701 — istek: komut paleti
- [48] https://github.com/mpvnet-player/mpv.net/issues/75 — mpv.net isteği (oy sayımı)
- [49] https://github.com/HandBrake/HandBrake/issues/267 — istek: taşınabilir kip
- [50] https://github.com/iina/iina/issues/5841 — istek: uygulama içi dil seçimi
- [51] https://github.com/obsproject/obs-studio/issues/11402 — açık hata: ekran okuyucu
- [52] https://github.com/HandBrake/HandBrake/blob/84a2864637e8759282363d3882e862e5d609eafb/win/CS/HandBrakeWPF/Views/Options/OptionsAdvanced.xaml — HandBrake gelişmiş tercihler: uykuyu engelle, pilde duraklat, disk eşiği, öncelik, günlük düzeyi
- [53] https://github.com/mltframework/shotcut/blob/f360166260fa5dbe93003c5756567cb4b132dbdb/src/settings.h — Shotcut `jobPriority` ayarı
- [54] https://github.com/microsoft/PowerToys/blob/31de3688ec82b6d8df508d096c4704e73647d0a9/src/settings-ui/Settings.UI/SettingsXAML/OobeWindow.xaml — PowerToys ilk açılış penceresi
- [55] https://github.com/HandBrake/HandBrake/blob/84a2864637e8759282363d3882e862e5d609eafb/win/CS/HandBrakeWPF/portable.ini.template — HandBrake taşınabilir kip şablonu
- [56] https://github.com/HandBrake/HandBrake/blob/84a2864637e8759282363d3882e862e5d609eafb/win/CS/HandBrakeWPF/UserSettingConstants.cs — HandBrake ayar anahtarları (disk eşiği)
- [57] https://github.com/obsproject/obs-studio/blob/7d98bebe1115b12608837b622164ea5fe6d9483a/frontend/wizards/AutoConfigStartPage.cpp — OBS otomatik yapılandırma sihirbazı
- [58] https://github.com/mltframework/shotcut/blob/f360166260fa5dbe93003c5756567cb4b132dbdb/.tx/config — Shotcut çevirisi Transifex'te
- [59] https://github.com/obsproject/obs-studio/blob/7d98bebe1115b12608837b622164ea5fe6d9483a/frontend/forms/OBSBasic.ui — OBS Yardım menüsü
- [60] https://github.com/mifi/lossless-cut/blob/70f2663a7a7c995903701acd2f616d057f4fdc2f/.github/ISSUE_TEMPLATE/2-bug.yml — LosslessCut hata şablonu
- [61] https://github.com/microsoft/terminal/blob/e90f9a534cae047fccddacc1ee813a82929662d5/src/cascadia/TerminalSettingsModel/GlobalAppSettings.cpp — Windows Terminal `persistedWindowLayout`
- [62] https://github.com/obsproject/obs-studio/blob/7d98bebe1115b12608837b622164ea5fe6d9483a/frontend/widgets/OBSBasic_SysTray.cpp — OBS tepsi davranışı
- [63] https://github.com/ShareX/ShareX/blob/5a649532c073df4069d9884241b37f0bacbdcf05/ShareX/ApplicationConfig.cs — ShareX `RunAtStartup`
- [64] https://github.com/obsproject/obs-studio/blob/7d98bebe1115b12608837b622164ea5fe6d9483a/frontend/dialogs/OBSAbout.cpp — OBS Hakkında: yazarlar ve lisans bölümü
- [65] https://github.com/mifi/lossless-cut/issues/669 — istek: yenilikler ekranı (tamamlandı)
- [66] https://github.com/mifi/lossless-cut/issues/2580 — istek: hareketi azaltma ayarı (tamamlandı)
- [67] https://github.com/mifi/lossless-cut/issues/1510 — istek: iş bitti bildirimi (tamamlandı)
- [68] https://github.com/ShareX/ShareX/issues/6679 — istek: sistem temasına göre otomatik tema (kapatıldı, yapılmayacak)
- [69] https://github.com/microsoft/PowerToys/issues/3822 — istek: ayarları sıfırlama
- [70] https://github.com/mifi/lossless-cut/issues/726 — LosslessCut isteği (oy sayımı)
- [71] https://github.com/ShareX/ShareX/issues/8845 — ShareX çeviri isteği
- [72] https://raw.githubusercontent.com/obsproject/obs-studio/master/frontend/data/locale/en-US.ini — OBS arayüz dizgileri: Yardım menüsü, Genel, Görünüm, Erişilebilirlik, Gelişmiş, kısayol süzgeci
- [73] https://raw.githubusercontent.com/HandBrake/HandBrake/master/win/CS/HandBrakeWPF/Views/Options/OptionsGeneral.xaml — HandBrake genel tercihler: güncelleme denetimi ve sıklığı, dil, sağdan sola kip, tema kipi, tepsiye küçült
- [74] https://raw.githubusercontent.com/HandBrake/HandBrake/master/win/CS/HandBrakeWPF/Views/Options/OptionsWhenDone.xaml — HandBrake "bitince" tercihleri: bildirim, özel eylem, dosyayı programa gönder, ses
- [75] https://developer.apple.com/tutorials/data/design/human-interface-guidelines/accessibility.json — Apple Human Interface Guidelines erişilebilirlik sayfasının verisi
- [76] https://learn.microsoft.com/en-us/windows/terminal/customize-settings/appearance — Windows Terminal görünüm ayarları: `theme` ("system", varsayılan), `language`, `disableAnimations`
