# Dağıtım Ve Otomasyon — Doğrulanmış Eksik Tablosu

13 Ekim 2026. Kod `main` eb0a5db0 (Sürüm 1.2.16; `origin/main` ile aynı commit) üzerinden okundu; yayın varlıkları v1.2.16 (13 Ekim 2026) üzerinden sayıldı. Kaynak numaraları `dagitim-otomasyon.md` dosyasındaki "Kaynaklar" listesine bakar; bu turda açılan ek kaynaklar Düzeltmeler tablosunda adıyla yazılı.

42 satır geldi, 42 satır kaldı. Hiçbir satır kodda tam olarak bulunmadı; dört satırın önceliği, iki satırın kararı değişti.

| No | Eksik | Kimde Var | Bizde Durum | Fayda | İş | Öncelik | Karar | Kaynak |
|---|---|---|---|---|---|---|---|---|
| DAG-01 | winget paketi: `winget install` ile kurulmuyor, `winget upgrade` ile güncellenmiyoruz. | HandBrake, OBS Studio, VLC, ShareX ve 9 rakip daha | yok | Kullanıcı tek komutla kurar ve günceller; toplu kurulum yapan BT ekibi listesine ekler. | orta | 1 | sahip: microsoft/winget-pkgs deposuna PR sahibin hesabından açılır ve Microsoft katkı sözleşmesi (CLA) onaylanır | [28][24][48][50][51][54] |
| DAG-02 | Windows kod imzası: kurucu ve uygulama imzasız; SmartScreen uyarıyor, Akıllı Uygulama Denetimi açık makinede kurulum engelleniyor. | HandBrake, OBS Studio, ShareX, PowerToys | yarım: imza yok, yayın iş akışında imza adımı yok; SignPath Foundation başvuru adımları yazılı ama gönderilmedi (docs/imzalama.md:3, :6) | İndiren kişi kırmızı uyarı görmeden kurar. | orta | 1 | sahip: SignPath Foundation başvurusu (ücretsiz, sahibin hesabı ve onayıyla) ya da ücretli sertifika | [15][24][27][33][46] |
| DAG-03 | Microsoft Store paketi: mağazada yokuz. | LosslessCut, PowerToys, Shotcut | yok | Mağazadan kurulur, paketi Microsoft imzalar, güncellemeyi mağaza yapar. | büyük | 2 | sahip: mağaza geliştirici hesabı ve yeni paket biçimi; ffmpeg ile libmpv paketin içine girmek zorunda | [15][24][33][34] |
| DAG-04 | Flathub paketi: Linux'ta paketimiz yok, kurulum kabuk betiğiyle. | HandBrake, VLC, mpv, LosslessCut ve 3 rakip daha | yok | Linux kullanıcısı yazılım merkezinden kurar; ffmpeg ve libmpv paketle gelir, güncelleme kendiliğinden olur. | büyük | 1 | sahip: Flathub'a yeni uygulama başvurusu sahibin hesabından yapılır; ffmpeg ve libmpv paketin içinde gelir | [31][3][15][51] |
| DAG-05 | macOS geliştirici imzası ve noterleme: paket yalnız ad-hoc imzalı, Apple onayından geçmiyor. | HandBrake, LosslessCut | yarım: ad-hoc imza var, Developer ID ve noterleme yok (macos-app-bundle.sh:117-118) | Tarayıcıdan indirilen kopya uyarısız açılır; dmg ve Homebrew yolu açılır. | orta | 2 | sahip: ücretli Apple geliştirici hesabı | [45][15][32] |
| DAG-06 | macOS disk görüntüsü (dmg): sürükle bırak kurulum dosyası yok, tek yol terminal komutu. | HandBrake, OBS Studio, LosslessCut, Shotcut ve 1 rakip daha | yok | Terminal kullanmayan Mac kullanıcısı da kurar. | orta | 2 | sahip: yeni ikili dağıtımı; DAG-05 olmadan Gatekeeper engeller | [2][3][5][14][21][26] |
| DAG-07 | Homebrew cask: `brew install --cask` ile kurulmuyoruz. | HandBrake, OBS Studio, VLC, LosslessCut ve 5 rakip daha | yok | Mac kullanıcısı alıştığı komutla kurar ve günceller. | küçük | 2 | sahip: kendi tap depomuz (Homebrew'un ana dizin dışındaki paket deposu) açılır; ana dizin en az 75 yıldız, sahibin kendi başvurusunda 225 yıldız arıyor, bizde 12; cask Gatekeeper denetiminden geçmek zorunda, önce DAG-05 gerekir | [30][32] |
| DAG-08 | Scoop paketi: `scoop install` ile kurulmuyoruz. | HandBrake, OBS Studio, VLC, mpv ve 9 rakip daha | yok | Scoop kullanan kişi tek komutla kurar ve günceller. | küçük | 2 | sahip: kendi kova depomuz (Scoop'un paket tarifi deposu) açılır ya da ScoopInstaller/Extras deposuna PR gider | [29] |
| DAG-09 | AppImage: tek dosyalık Linux paketi yok. | LosslessCut, Shotcut | yok | İndirilen tek dosya kurulum yapmadan çalışır. | orta | 2 | sahip: yeni ikili dağıtımı; ffmpeg ve libmpv içine girerse boyut ve lisans kararı | [14][15][21][48] |
| DAG-10 | Taşınabilir sürüm: zip açılıp çalıştırılınca eksik kalıyor; ffmpeg ve libmpv içinde yok, ayarlar kullanıcı klasörüne yazılıyor. | HandBrake, ShareX, OBS Studio, ScreenToGif ve 1 rakip daha | yarım: zip yayımlanıyor, ayar yolu yalnız ölçüm için ortam değişkeniyle değişiyor, taşınabilir kip yok (src/VidShrink.Core/UpdateCheck.cs:476-481) | USB bellekte ya da kurulum izni olmayan makinede çalışır. | orta | 2 | sahip: ffmpeg ve libmpv'nin zip'e girmesi yeni ikili dağıtımıdır; ayarı yanında tutan kip tek başına karar istemez | [3][6][12][13][20][27] |
| DAG-11 | Çevrimdışı kurucu: kurucu her şeyi kurulum anında ağdan indiriyor, ağsız makinede kurulmuyor. | File Converter, ScreenToGif, HandBrake, OBS Studio | yarım: 11,8 MB indirici kurucu var; yerel klasör --asset-source ile kaynak olabiliyor (src/VidShrink.Setup/Program.cs:89-104; src/VidShrink.Core/Setup/SetupDownloads.cs:97) | Kapalı ağdaki makineye kurulur; mağazanın exe yolu da bunu şart koşuyor. | orta | 2 | sahip: yüzlerce MB'lık tam kurucu yeni ikili dağıtımıdır | [22][27][2][5][33] |
| DAG-12 | Chocolatey paketi: `choco install` ile kurulmuyoruz. | VLC, OBS Studio, HandBrake, PowerToys (paketlere topluluk bakıyor) | yok | Chocolatey kullanan BT ekibi listesine ekler. | küçük | 2 | sahip: Chocolatey hesabı ve yayın anahtarı gerekir | [24][51][52] |
| DAG-13 | Snap paketi: Ubuntu mağazasında yokuz. | LosslessCut, Shotcut | yok | Ubuntu'da mağazadan kurulur, kendiliğinden güncellenir. | orta | 3 | sahip: mağaza hesabı ve yeni paket kanalı | [15][48] |
| DAG-14 | deb paketi: Ubuntu ve Debian için kurulum paketi yok. | OBS Studio | yok | Çift tıkla kurulur, paket yöneticisinden kaldırılır. | orta | 3 | sahip: yeni ikili dağıtımı | [5] |
| DAG-15 | Mac App Store: Apple mağazasında yokuz. | LosslessCut | yok | Mac'te mağazadan kurulur ve güncellenir. | büyük | 3 | sahip: ücretli Apple hesabı ve mağaza kuralları | [15][53] |
| DAG-16 | Sessiz kurulum bayrağı: kurucuda adı konmuş, belgeli bir sessiz kip yok. | ShareX, PowerToys | yarım: --console panelsiz kurar ve yardım metninde yazılı, --no-launch var; adı sessiz olan bayrak ve docs/kurulum.md belgesi yok, kendi görünür konsolundaysa hata olunca Enter bekliyor (src/VidShrink.Core/Setup/SetupText.cs:353-354; src/VidShrink.Core/Setup/SetupPanel.cs:249-264; src/VidShrink.Setup/Program.cs:31-34) | BT ekibi ve paket yöneticisi kurulumu penceresiz koşar; winget ve mağaza bunu şart koşuyor. | küçük | 2 | gerekmez | [13][24][33] |
| DAG-17 | Yüklü uygulamalar kaydı: Windows Ayarlar'daki uygulama listesinde görünmüyoruz; kaldırma yalnız kurucu bayrağıyla. | File Converter, ScreenToGif | yok | Program herkesin bildiği yerden kaldırılır; paket yöneticisi kurulu sürümü tanır. | küçük | 2 | gerekmez | [22][27] |
| DAG-18 | Makine geneli kurulum: yalnız oturum açan kullanıcıya kuruluyor. | PowerToys | yarım: --install-root ve --registry-root bayrakları var, makine geneli kip yok (src/VidShrink.Setup/Program.cs:79, :89-104, :111) | Ortak bilgisayarda bir kez kurulur, herkes kullanır. | orta | 3 | sahip: yönetici hakkıyla kurulan kopyaya otomatik güncelleme yazamaz; o kipte güncellemenin nasıl davranacağı varsayılan davranış kararıdır | [24][23] |
| DAG-19 | Kaldırmada ayar temizliği: kaldırma ayar klasörünü bırakıyor, silme seçeneği yok. | PowerToys | yarım: kısayol, menü, ilişki, paket ve kurulum kökü silinir, ayar klasörüne dokunulmaz (src/VidShrink.Core/Setup/SetupRunner.cs:210-249); uygulama içinde "Tüm verileri sıfırla" var (src/VidShrink.App/AppDataReset.cs:7-30) | Tam temizlik isteyen kullanıcı iz bırakmadan kaldırır. | küçük | 3 | gerekmez | [24] |
| DAG-20 | macOS ve Linux'ta ffmpeg kurulumu: ffmpeg yoksa kurucu paket yöneticisi komutunu söyleyip duruyor. | LosslessCut, HandBrake, Shutter Encoder (sonuncusu dolaylı kanıt) | yarım: Windows'ta sha256'lı indirir, macOS ve Linux'ta komut söyleyip çıkar (install-vidshrink.sh:232-262; docs/kurulum.md:83-86) | Kurulum tek adımda biter, kullanıcı ikinci bir araç kurmaz. | orta | 2 | sahip: yeni ikili indirme ve lisans kararı (docs/kurulum.md:207-210) | [15][20][3] |
| DAG-21 | ARM64'te Windows 11 menü eklentisi: eklenti yalnız x64 derleniyor, ARM64 kurulumunda ilk menüde girdi yok. | PowerToys (ARM64 kurucu var; menü mimarisi doğrulanmadı) | yarım: x64'te var; paket bildirimi x64'e sabit, ARM64 arşivine shell klasörü girmiyor (src/VidShrink.ShellExtension/AppxManifest.template.xml:10; .github/workflows/release.yml:403-411) | ARM64 dizüstünde de sağ tıkın ilk menüsünde görünürüz. | orta | 3 | gerekmez | [23][24] |
| DAG-22 | 32 bit Windows derlemesi: yalnız x64 ve ARM64 yayımlıyoruz. | mpv, ScreenToGif | yok | Eski 32 bit Windows makinede de çalışır. | orta | 3 | sahip: yeni ikili dağıtımı | [8][27] |
| DAG-23 | Eski macOS desteği: alt sınır macOS 14. | HandBrake, LosslessCut, OBS Studio | yarım: macOS 14 ve üstü (docs/kurulum.md:94; macos-app-bundle.sh:78-79) | Eski Mac'i olan kullanıcı da kurar. | orta | 3 | kapalı: macOS alt sürümü 14 (docs/YOL-HARITASI.md:72) | [3][18][5] |
| DAG-24 | Güncelleme kanalı: ön sürüm ya da gecelik derleme kanalı yok, herkes aynı sürümü alıyor. | OBS Studio, VLC, LosslessCut, HandBrake | yarım: sürüm ekini ayrıştırıyor, kanal seçimi yok (src/VidShrink.Core/UpdateCheck.cs:286-301); yayındaki üç ön sürüm etiketi bağımlılık deposu, uygulama ön sürümü değil | Gönüllü kullanıcı yeni sürümü önce dener, hata herkese ulaşmadan yakalanır. | orta | 2 | sahip: ön sürüm yayımlamak yeni bir ikili akışı açar ve sürüm kesme düzenini değiştirir | [28][15][3] |
| DAG-25 | Linux'ta kendini güncelleme: uygulama yalnız yeni sürüm olduğunu söylüyor. | rakipte kendini güncelleme doğrulanmadı; LosslessCut, HandBrake, VLC, mpv ve 3 rakipte güncellemeyi mağaza yapar | yarım: yalnız bildirim; kendini kurma yalnız Windows'ta ve macOS paketinde açık (src/VidShrink.Core/UpdateCheck.cs:147; docs/kurulum.md:175-176) | Linux kullanıcısı komut koşmadan güncel kalır. | orta | 3 | gerekmez | [15][31] |
| DAG-26 | İkili fark güncellemesi: dosyanın yalnız değişen baytını indiren yama yok, güncelleme tek paket olarak iniyor. | Sparkle, Velopack (güncelleme çatıları; kullanan rakip doğrulanmadı) | yarım: tek paket (win-x64 72,0 MB) ve dosya düzeyi aralık yolu var (src/VidShrink.Core/UpdateStaging.cs:191, :287, :643) | Yavaş bağlantıda güncelleme çok daha az veri indirir. | büyük | 3 | sahip: yeni bağımlılık ya da yeni paket biçimi | [36][37][1] |
| DAG-27 | Yayın dosyalarında ayrık imza: indirilen dosya yalnız aynı yayındaki sha256 listesiyle doğrulanıyor. | HandBrake, Sparkle (çatı) | yarım: sha256 listesi var, imza doğrulaması yok; yayın iş akışında imza ya da köken kaydı adımı yok (docs/imzalama.md:38-46) | Yayın hesabı ele geçse bile sahte dosya doğrulamadan geçmez. | orta | 3 | sahip: imza anahtarını üretip saklamak sahibin işi | [3][36] |
| DAG-28 | Windows'ta komut PATH'te değil: kurulumdan sonra `vidshrink` yazınca komut satırı aracı bulunmuyor. | FFmpeg, HandBrakeCLI, mpv | yarım: macOS ve Linux'ta ~/.local/bin bağı var (install-vidshrink.sh:476), Windows kurucusu PATH'e yazmıyor; komut satırı aracı uygulamayla aynı klasöre yayımlanıyor ve kökteki başlatıcıyla aynı adı taşıyor (src/VidShrink.Cli/VidShrink.Cli.csproj:8; src/VidShrink.Launcher/VidShrink.Launcher.csproj:8) | Betik yazan kişi tam yolu aramadan komutu çağırır. | küçük | 2 | gerekmez (kurucuda varsayılanı kapalı seçenek olarak; varsayılan açık istenirse sahip kararı) | [28][29] |
| DAG-29 | Makinece okunur ilerleme: `--json` yalnız sonucu veriyor, ilerleme insan diliyle stderr'e yazılıyor. | HandBrakeCLI, FFmpeg | yarım: sonuç JSON, ilerleme yerelleştirilmiş metin (src/VidShrink.Cli/CliApp.cs:877-893) | Başka bir program ilerlemeyi metin ayıklamadan gösterir. | küçük | 2 | gerekmez | [4][38][48] |
| DAG-30 | Komut satırında dönüştürme: Dönüştür sekmesinin komutu yok, kodek listesi dört kodekle sınırlı. | HandBrakeCLI, FFmpeg, ShareX | yarım: kucult --kalite ve --kodek yeniden kodluyor, dönüştür komutu yok (src/VidShrink.Cli/CliRequest.cs:6, :8) | Toplu biçim değiştirme betikle yapılır. | orta | 2 | gerekmez | [4][38][13] |
| DAG-31 | Komut satırında ekran kaydı: kaydı komutla başlatma ve durdurma yok. | OBS Studio, ShareX | yok; en yakın kod: uygulamanın dışarıdan aldığı tek bayrak --kucult (src/VidShrink.Core/ShellIntegration.cs:109) | Zamanlanmış görev ya da tuş aracı kaydı başlatır. | orta | 2 | gerekmez | [6][13] |
| DAG-32 | Kuyruğu komut satırında koşma: arayüzde kuyruk dosyaya kaydedilemiyor, komut satırı da kuyruk dosyası almıyor. | HandBrakeCLI | yok | Arayüzde hazırlanan iş başka makinede ya da gece koşar. | orta | 3 | gerekmez | [4] |
| DAG-33 | Ön ayarı komut satırından dışa aktarma: içe alma var, dışa veren komut yok. | HandBrakeCLI | yarım: --profil-dosyasi içe alır, --profiller listeler; dışa yazan işlev çekirdekte hazır, komutu yok (src/VidShrink.Cli/CliApp.cs:594-617; src/VidShrink.Cli/CliRequest.cs:364; src/VidShrink.Core/PresetLibrary.cs:228) | Ayar başka makineye dosyayla taşınır. | küçük | 3 | gerekmez | [4] |
| DAG-34 | Kabuk tamamlama: Tab tuşuyla seçenek tamamlayan dosya yok. | mpv | yok | Uzun seçenek adları ezberlenmeden yazılır. | küçük | 3 | gerekmez | [11] |
| DAG-35 | Denetim arabirimi: çalışan uygulamayı dışarıdan süren belgeli bir kanal yok (soket, HTTP ya da WebSocket). | mpv, OBS Studio, LosslessCut, VLC | yarım: yalnız tek örnek için iç boru var; yalnız aynı kullanıcıya açık, belgeli değil, taşıdığı tek şey dosya yolu ve küçültme isteği (src/VidShrink.Core/SingleInstanceChannel.cs:65-66, :98-100; src/VidShrink.Core/ShrinkRequest.cs:129) | Uzaktan kumanda, tuş takımı ve betik oynatmayı ya da kaydı yönetir. | büyük | 1 | gerekmez (yerel boru olarak ve varsayılan kapalı; ağdan dinleyen HTTP ya da WebSocket eklenecekse sahip kararı) | [9][10][7][42][16][17][39][53] |
| DAG-36 | Betik ve eklenti arabirimi: kullanıcı betiği ya da eklenti yüklenemiyor. | mpv, OBS Studio, IINA | yok | Eksik özelliği topluluk kendi yazar. | büyük | 2 | sahip: yeni bağımlılık (betik motoru) ve kalıcı arabirim sözü | [10][43][44] |
| DAG-37 | Windows 11 ilk menüsünde hızlı küçültme: altı hedefli alt menü yalnız "Daha fazla seçenek göster" altında. | doğrulanmadı; istek File Converter ve ShareX kullanıcılarından (17, 10 ve 4 oy) | yarım: ilk menüde yalnız açma girdisi var; eklentinin alt komut kolu boş (src/VidShrink.Core/ShellIntegration.cs:46-48; src/VidShrink.ShellExtension/VidShrink.ShellExtension.cpp:129-141) | Sağ tık, hedef boyut, bitti: ikinci menüye inilmez. | orta | 2 | gerekmez | [35][25][49][50] |
| DAG-38 | Docker görüntüsü: başsız kullanım için hazır kapsayıcı yok. | Unmanic (HandBrake görüntüsü topluluk işi, resmi değil) | yarım: Linux paketinde başsız komut satırı var, Dockerfile yok (docs/cli.md:6) | Ev sunucusunda izleme klasörü kapsayıcıda sürekli koşar. | orta | 3 | sahip: yeni paket kanalı; görüntüye ffmpeg girmesi yeni ikili dağıtımıdır | [40][41] |
| DAG-39 | Tuş takımı bağlantısı: Stream Deck benzeri kumandalar için hazır bağlantı yok. | OBS Studio | yok | Tek tuş kaydı başlatır ya da küçültmeyi tetikler. | orta | 3 | sahip: yeni eklenti paketi ve yeni yayın kanalı; önce DAG-35 gerekir | [7] |
| DAG-40 | Kurumsal ilke: güncellemeyi ya da bir özelliği merkezden kapatan ilke anahtarı yok. | PowerToys, ShareX | yarım: VIDSHRINK_UPDATE_DISABLED ortam değişkeni var, ilke anahtarı yok (src/VidShrink.Launcher/Updater.cs:84; src/VidShrink.Core/UpdateCheck.cs:1621) | Okul ya da şirket BT'si güncellemeyi merkezden yönetir. | küçük | 3 | gerekmez | [23][13] |
| DAG-41 | macOS Finder sağ tık eylemi: Windows'taki hızlı küçültme menüsünün Mac karşılığı yok. | doğrulanmadı (rakip belgesi bulunamadı) | yok | Mac'te de dosyaya sağ tıklayıp küçültülür. | orta | 3 | gerekmez | [53] |
| DAG-42 | Linux dosya yöneticisi eylemi: sağ tıkta küçültme girdisi yok, yalnız "birlikte aç" kaydı var. | doğrulanmadı (rakip belgesi bulunamadı) | yarım: .desktop dosyası ve MIME kaydı var, eylem yok (install-vidshrink.sh:100-110) | Linux'ta da dosyaya sağ tıklayıp küçültülür. | küçük | 3 | gerekmez | [11] |

## Düzeltmeler

Kanıt sütunundaki depo yolları `gh api` ile (yalnız okuma), sayfalar WebFetch ile bu turda açıldı.

| No | Ne Değişti | Kanıt |
|---|---|---|
| `DAG-01` | Öncelik 1 iki kaynakta görüldü. Karar gerekçesi netleşti: PR sahibin hesabından, katkı sözleşmesi onayıyla. Ön koşul eklendi: winget etkileşimsiz kurulum ister. | microsoft/winget-pkgs `manifests` altında HandBrake, OBSProject, VideoLAN, ShareX, Meltytech (Shotcut) ve KDE (Kdenlive) dizinleri var, `manifests/t/Teknesyum` 404; PowerToys kurulum belgesi `winget install` komutunu veriyor; winget-pkgs `README.md:29-31` (CLA), `doc/Policies.md:22` (etkileşimsiz kurulum şartı) |
| `DAG-02` | Kimde Var değişti: mağaza paketleri yerine düz exe imzası resmi depoda görülen dört rakip. Araştırmanın "rakiplerin düz exe imzası doğrulanamadı" notu kapandı; öncelik 1 kaldı. Bizde Durum'a yayın iş akışında imza adımı olmadığı eklendi. | HandBrake `.github/workflows/windows.yml:145-180` (imza sertifikası sırrı ve zaman damgası sunucusu); OBS Studio `.github/actions/windows-signing/action.yaml`; ShareX `build.yml:123-143` (azure/artifact-signing-action); PowerToys `.pipelines/ESRPSigning_core.json`; bizde `.github/workflows/release.yml` içinde signtool, codesign, attest, cosign, gpg, minisign 0 eşleşme |
| `DAG-03` | Kimde Var'a Shotcut eklendi. Öncelik 2 kaldı: mağaza, imzalı kurucuyla aynı işi gören ikinci kanal. | shotcut.org/download sayfası Microsoft Store bağlantısı veriyor |
| `DAG-04` | Öncelik 1 iki kaynakta görüldü. Karar gerekçesi netleşti. | flathub altında fr.handbrake.ghb, org.videolan.VLC, io.mpv.Mpv, no.mifi.losslesscut, org.shotcut.Shotcut, org.kde.kdenlive, io.github.wartybix.Constrict depoları arşivsiz; handbrake.fr/downloads.php Linux için Flathub'ı gösteriyor; flathub kuruluşunda adı vidshrink geçen depo araması 0 sonuç |
| `DAG-07` | Karar gerekçesi iki olguyla değişti: ana dizinin tanınırlık eşiği ve Gatekeeper şartı. Notlardaki "`--no-quarantine` kaldırılıyor" ifadesi kaynakta "kullanımdan kalktı" (deprecated) diye geçiyor. | Homebrew/brew `docs/Package-Acceptance-Policy.md:31-32` (30 çatal, 30 izleyen ya da 75 yıldızdan biri; depo sahibinin kendi başvurusunda 90, 90 ya da 225); `docs/Acceptable-Casks.md:35`; depomuz 12 yıldız, 1 çatal, 1 izleyen (`gh api`, 13 Ekim); Homebrew/homebrew-cask `Casks/v/vidshrink.rb` 404 |
| `DAG-08` | Karar gerekçesi netleşti: yeni depo ya da başka depoya PR. Rakip iddiası dört rakip için depoda görüldü. | ScoopInstaller/Extras `README.md:3`; `bucket/handbrake.json`, `bucket/obs-studio.json`, `bucket/vlc.json`, `bucket/mpv.json` var, `bucket/vidshrink.json` 404 |
| `DAG-10` | Satır aralığı düzeltildi (`:481` yerine `:476-481`); kararın yalnız ikili dağıtımına bağlı olduğu yazıldı. | src/VidShrink.Core/UpdateCheck.cs:476-481 (değişken "yalnız ölçüm ve deneme için") |
| `DAG-11` | Bizde Durum'a kod kanıtı eklendi; kurucu boyutu v1.2.16 yayınından yeniden sayıldı, değişmedi. | src/VidShrink.Core/Setup/SetupDownloads.cs:97 (`AssetSource` yerel klasör kolu); `VidShrink-Setup.exe` 11.756.490 bayt (11,8 MB) |
| `DAG-12` | Öncelik 3 → 2. Kimde Var değişti: iki rakip yerine dört. Karar gerekçesi hesap şartına çekildi. | community.chocolatey.org: `vlc` (22,6 milyon indirme), `obs-studio` (804.789), `handbrake` (450.229); PowerToys belgesi `choco install powertoys` komutunu "topluluk bakımında" diye veriyor; community.chocolatey.org/packages/vidshrink 404 |
| `DAG-13` | Kimde Var'a Shotcut eklendi. İki rakip olmasına karşın öncelik 3 kaldı: aynı kitleyi DAG-04 karşılıyor. | shotcut.org/download sayfası Snap Store bağlantısı veriyor |
| `DAG-16` | Bizde Durum netleşti: `--console` yardım metninde yazılı ve panelsiz kuruyor; eksik olan ad, belge ve hatada beklememe. Fayda'ya winget eklendi. | src/VidShrink.Core/Setup/SetupText.cs:353-354; src/VidShrink.Core/Setup/SetupPanel.cs:249-264; src/VidShrink.Setup/Program.cs:31-34 |
| `DAG-18` | Satır numaraları düzeltildi (`:81, :112` yerine `:79, :89-104, :111`). Karar gerekçesi ölçüte bağlandı. | src/VidShrink.Setup/Program.cs:79 (kayıt kökü), :111 (kurulum kökü); src/VidShrink.Core/UpdateCheck.cs:433 (otomatik güncelleme Windows'ta açık gelir) |
| `DAG-19` | Bizde Durum'a uygulama içi tam sıfırlama eklendi; satır aralığı `:210-249`. | src/VidShrink.App/AppDataReset.cs:7-30 |
| `DAG-20` | Öncelik 3 → 2. Kimde Var'daki "paket içeriği doğrulanmadı" notu iki rakip için kapandı. Bizde Durum'daki satır aralığı `:233-242` yerine `:232-262`. | LosslessCut `package.json` (`download-ffmpeg-<platform>` betikleri ve `extraResources` altında ffmpeg); HandBrake deposunda `contrib/ffmpeg` (ffmpeg kaynaktan derlenip içine giriyor); Shutter Encoder `Library/sources.txt` ffmpeg'i sayıyor, ikili görülmedi |
| `DAG-21` | Kanıt satırı düzeltildi (`:401-404` yerine `:403-411`) ve kod kanıtı eklendi. | src/VidShrink.ShellExtension/AppxManifest.template.xml:10 (`ProcessorArchitecture="x64"`) |
| `DAG-23` | Bizde Durum'a kod kanıtı eklendi; karar kapalı kaldı. | macos-app-bundle.sh:78-79 (`LSMinimumSystemVersion` 14.0); docs/YOL-HARITASI.md:72 |
| `DAG-24` | Bizde Durum'a not: yayında üç ön sürüm etiketi var ama hepsi bağımlılık deposu. Karar gerekçesi ölçüte bağlandı. | `gh release list`: `deps-libmpv-macos-mpvkit-1.0.0`, `deps-libmpv-20260903`, `libmpv-mirror` |
| `DAG-25` | Öncelik 2 → 3. Rakiplerde belgeli olan kendini güncelleme değil, mağazanın güncellemesi; o fayda DAG-04 ile gelir. Kod kanıtı eklendi. | Satırın kendi Kimde Var notu; src/VidShrink.Core/UpdateCheck.cs:147 (kendini kurma yalnız Windows ve macOS paketi) |
| `DAG-27` | Bizde Durum'a yayın iş akışında imza ya da köken kaydı adımı olmadığı eklendi. | `.github/workflows/release.yml` içinde attest, cosign, gpg, minisign 0 eşleşme |
| `DAG-28` | Karar `sahip` → `gerekmez`. Varsayılanı kapalı bir kurucu seçeneği dört ölçütten hiçbirine girmiyor. Kanıt belgeden koda çekildi, ad çakışmasının kanıtı eklendi. | install-vidshrink.sh:476 (macOS ve Linux bağı); src/VidShrink.Cli/VidShrink.Cli.csproj:8 (`vidshrink`); src/VidShrink.Launcher/VidShrink.Launcher.csproj:8 (`VidShrink`); .github/workflows/release.yml:585-590 (ikisi aynı klasöre yayımlanır); src/VidShrink.Setup/Program.cs:142-156 PATH'i yalnız okur |
| `DAG-29` | Satır aralığı düzeltildi (`:878-890` yerine `:877-893`). | src/VidShrink.Cli/CliApp.cs:877-893 |
| `DAG-31` | Bizde Durum'a en yakın kod eklendi. | `src/VidShrink.App` altında tırnak içinde geçen tek bayrak `--kucult`; src/VidShrink.Core/ShellIntegration.cs:109 |
| `DAG-32` | Eksik cümlesi düzeltildi: arayüzde kuyruğu dosyaya kaydeden bir yol yok, yani iş iki parça (dosya biçimi ve komut). | `SaveQueue`, `KuyrukKaydet`, `QueueFile`, `queue.json` aramaları ve `Locales/en/main.json` içinde queue ile export, import, save, load birlikte: 0 eşleşme |
| `DAG-33` | Bizde Durum'a çekirdekteki hazır işlev eklendi; iş gerçekten küçük. | src/VidShrink.Core/PresetLibrary.cs:228 (`Export`) |
| `DAG-35` | Öncelik 1 iki kaynakta görüldü. Karar `sahip` → `gerekmez`: yerel boru yeni bağımlılık, ikili, hesap ya da varsayılan değişikliği getirmiyor; var olan boru zaten yalnız aynı kullanıcıya açık. Ağdan dinleyen sürüm ayrı karar. | mpv `DOCS/man/ipc.rst` (`--input-ipc-server`, soket ya da adlandırılmış boru); OBS uzaktan denetim kılavuzu ve obs-websocket deposu; LosslessCut `api.md` (`--http-api`, yalnız 127.0.0.1); src/VidShrink.Core/SingleInstanceChannel.cs:65-66, :98-100 (`CurrentUserOnly`) |
| `DAG-37` | Bizde Durum'a eklentinin bugünkü kolu eklendi. | src/VidShrink.ShellExtension/VidShrink.ShellExtension.cpp:129-141 (`GetFlags` `ECF_DEFAULT`, `EnumSubCommands` `E_NOTIMPL`) |
| `DAG-38` | Öncelik 2 → 3. Resmi kaynakta tek rakip kaldı; ikinci kaynak notlarda "resmi değil" diye yazılı. | Notlar kaynak [41]: jlesage/docker-handbrake, topluluk görüntüsü |

## Çıkarılanlar

| No | Eksik | Neden | Kanıt (dosya:satır) |
|---|---|---|---|

Yok. 42 satırın hiçbiri kodda tam olarak bulunmadı; `yok` hükümleri depo dosya listesinde de arandı (winget, scoop, nuspec, wxs, flatpak, appimage, snapcraft, docker, admx, tamamlama dosyası: 0 dosya).

## Yinelenenler

Komşu tablo `ayarlar-kabuk-eksikler.md` 17:53'te okundu (48 satır). Doğrulanmış sürümü 18:17'de klasördeydi; aşağıdaki iki satır orada özgünle aynı duruyordu ve dosyada bu tabloya atıf yoktu (komşu doğrulama o sırada sürüyordu).

- DAG-10 = AYR-05 — taşınabilir kip. Öncelik farklı: burada 2, komşuda 1. Karar ikisinde de sahip.
- DAG-24 = AYR-09 — güncelleme kanalı. İkisinde de öncelik 2, karar sahip.

Aynı olmayan ama komşu duranlar: AYR-35 (iş bitince özel komut) DAG-36'nın en küçük biçimi sayılabilir. AYR-47 (elle güncelleme denetimi) DAG-24 ile aynı Güncelleme kartına dokunur ama ayrı iştir; ikisi aynı anda yapılırsa aynı dosyada çakışır.

## Doğrulanamayanlar

- `DAG-01` "ve 9 rakip daha": altı rakibin winget dizini görüldü, kalanı açılmadı. LosslessCut `manifests/m/mifi` yolunda yok (404).
- `DAG-02` SignPath proje listesi [46] ve Azure imza belgesi [47] açılmadı. LosslessCut ve ScreenToGif'in doğrudan indirilen exe'sinin imzası görülmedi; bu yüzden Kimde Var'dan çıkarıldılar.
- `DAG-07` Homebrew'un Gatekeeper'dan geçmeyen cask'ları Eylül 2026'da gerçekten kapatıp kapatmadığı görülmedi; duyuru ve bugünkü kabul belgesi görüldü.
- `DAG-12` Chocolatey paketlerinin hiçbiri satıcının kendi paketi değil, topluluk bakımında. LosslessCut'ın Chocolatey sayfası açılmadı.
- `DAG-20` Shutter Encoder'da ffmpeg ikilisi görülmedi; yalnız `Library/sources.txt` içinde adı geçiyor. HandBrake ffmpeg'i ayrı ikili olarak değil, kendi içine derleyerek getiriyor.
- `DAG-21` PowerToys'un ARM64'te ilk menü eklentisi getirip getirmediği görülmedi.
- `DAG-26` Sparkle ve Velopack'i kullanan bir rakip bulunamadı; satır çatı belgesine dayanıyor.
- `DAG-31` Kaydedici motorunun pencere olmadan, komut satırı sürecinin içinde koşup koşamayacağına bakılmadı (`src/VidShrink.App/Recorder/`).
- `DAG-37` İlk menüde hızlı eylem sunan bir rakip belgesi bulunamadı. Oylardan 17 (File Converter #211) ve 4 (ShareX #5872) görüldü, 10 oyluk kayıt açılmadı.
- `DAG-39` OBS uzaktan denetim kılavuzu Stream Deck'i adıyla anmıyor; Macro Deck, Touch Portal, Deckboard ve Bitfocus Companion'ı sayıyor. Stream Deck bağlantısının resmi kaynağı görülmedi.
- `DAG-41`, `DAG-42` Rakip belgesi bulunamadı. DAG-42'de dosya yöneticilerinin sağ tık girdisini hangi dosyadan okuduğu (Dolphin, Nemo, Nautilus ayrı yollar kullanır) bu turda kaynakla doğrulanmadı.
- Lisans: depo AGPL-3.0 (`gh api`). ffmpeg'in GPL derlemesini kendi paketimizde dağıtmanın yükümlülükleri çalışılmadı; `docs/kurulum.md:207-210` bunu açık iş olarak tutuyor.

## Ek Notlar

**Bayat Belge Satırları.** Araştırma kolunun bulduğu satırlar koda karşı okundu.

- `README.md:74-75` "four targets" diyor. Koddan doğrulandı: `.github/workflows/release.yml:347` yayın matrisi altı hedef sayıyor ve v1.2.16 yayınında altı arşiv var.
- `docs/kurulum.md:50` "Only `win-x64` is published", `:73-76` ve `:180` yalnız `linux-x64`. Bayat: `install-vidshrink.sh:215` `linux-arm64` seçiyor, `release.yml:386-401` ARM64 makineye `win-arm64` kuruyor.
- `docs/kurulum.md:184` ve `:193` ffmpeg kaynağını WinGet diye yazıyor; aynı belgenin `:19-21` satırları kurucu exe'nin WinGet çağırmadığını söylüyor.
- `docs/kurulum.md:108-109` ("1.7 MB of a 519 MB") ve `:117-130` akış şeması eski indirme yolunu anlatıyor; bugünkü yol tek paket (`src/VidShrink.Core/UpdateStaging.cs:287`).
- `vidshrink-pad.xml:36` ve `:93-94` sürüm 0.8.5 ve o sürümün indirme adresleri. Güncel sürüm 1.2.16 (`Directory.Build.props:7`). Notlar 1.2.15 diyordu; 13 Ekim'de 1.2.16 çıktı, varlık türleri aynı (23 dosya: zip, sha256 listesi, manifest ve tek kurucu exe). Tablodaki iki boyut yeni yayında da tutuyor: kurucu 11.756.490 bayt (11,8 MB), win-x64 güncelleme paketi 72.010.212 bayt (72,0 MB).
- Yeni: `README.md:409-411` "The application is pending" diyor, `docs/imzalama.md:6` "Nothing below has been submitted" diyor. İkisi aynı anda doğru olamaz.
- Yeni: `docs/YOL-HARITASI.md:68-69` yayının dört hedef ürettiğini söylüyor.
- Yeni: `docs/imzalama.md:14-15` imzalanacak dosyaları yalnız `win-x64` işinden sayıyor; `win-arm64` başlatıcısı ve uygulaması yok.
- `docs/piyasa/tarama-2026-10-05.md:48` "izleme klasörü arayüzde yok": `src/VidShrink.App/QueueWatch.cs` dosyası var; içeriği bu turda okunmadı.

**Sahip Kararlarının Yeniden Tartılması.** 26 satır sahip kararı istiyordu; 24'ü kaldı.

- Para ya da hesap gerektirenler kaldı: DAG-02, DAG-03, DAG-05, DAG-12, DAG-13, DAG-15.
- Başka bir depoda sahibin adına yayın ya da yeni depo gerektirenler kaldı: DAG-01 (CLA onayı), DAG-04, DAG-07, DAG-08.
- Yeni ikili dağıtımı olanlar kaldı: DAG-06, DAG-09, DAG-10, DAG-11, DAG-14, DAG-20, DAG-22, DAG-24, DAG-38, DAG-39.
- Yeni bağımlılık olanlar kaldı: DAG-26, DAG-36. DAG-27 anahtar saklama, DAG-18 güncelleme varsayılanı yüzünden kaldı.
- İkisi `gerekmez` oldu: DAG-28 (varsayılanı kapalı kurucu seçeneği) ve DAG-35 (yerel boru, varsayılan kapalı). İkisinde de koşul hücrede yazılı; koşul bozulursa karar sahibe döner.

**Öncelikte Dokunulmayan Yargı.** On bir satır üç ya da daha çok rakipte belgeli olduğu halde 2 taşıyor: DAG-03, DAG-06, DAG-07, DAG-08, DAG-10, DAG-11, DAG-12, DAG-24, DAG-28, DAG-30, DAG-36. Ölçüt 1 için iki şartı birlikte arıyor: üç rakip ve alanın ana işine dokunmak. Araştırma her işletim sisteminin birinci kurulum yolunu ana iş, ikinci yolları kenar saymış; bu bir yargı ve dokunulmadı. Sahip ikinci yolları da ana iş sayarsa en güçlü aday DAG-10: beş rakipte belgeli ve komşu tabloda (AYR-05) 1 taşıyor.

**Sıra Bağımlılıkları.** DAG-16 ve DAG-17 bitmeden DAG-01 gönderilemez. DAG-05 bitmeden DAG-06 ve DAG-07 işe yaramaz. Mağazanın exe yolu imzalı, sessiz ve çevrimdışı kurucu ister (kaynak [33]); bu yüzden DAG-03 o yoldan DAG-02, DAG-16 ve DAG-11'e bağlıdır. DAG-39 ve DAG-31, DAG-35'in kanalına muhtaçtır.

**Tabloda Olmayan Açık Eksikler.**

- Yayın dosyalarına köken kaydı (GitHub'ın anahtarsız "artifact attestation" adımı: dosyanın hangi iş akışından çıktığını imzalı kayda bağlar) yok; `release.yml` içinde `attest`, `cosign`, `gpg` 0 eşleşme. DAG-27'nin anahtar istemeyen küçük parçası olabilir.
- DAG-35'in ağdan dinleyen sürümü (HTTP ya da WebSocket) `README.md:413-414` gizlilik beyanını ve altındaki ağ isteği listesini değiştirir; sahibin kararıdır.
- Kabuk paketinin yayıncı alanı `CN=Teknesyum` (`AppxManifest.template.xml:10`). Paket bir gün imzalanırsa bu alan sertifikadaki adla eşleşmek zorunda; SignPath sertifikasında ad farklı olur.
- DAG-23 kapalı. Pazar notu: HandBrake indirme sayfası alt sınırı macOS 10.13 veriyor; kararı yeniden açmak sahibin işi.
- MSIX kimliği, rafta duran K3'ün (pencereyi WGC ile yakalama) sarı kenarlık engeliyle de ilgili (`docs/YOL-HARITASI.md:114-115`); karar yeniden açılmadı, yalnız bağ not edildi.

## Sekme Özeti

- Güncelleme tarafında pazarın önündeyiz: yerinde takas, tek paket, sha256 doğrulaması ve düşüş sayacı var. Ulaşma tarafında gerideyiz: iki platformda da imzasızız ve beş paket dizininin (winget, Scoop, Chocolatey, Flathub, Homebrew) hiçbirinde yokuz.
- Yayındaki 23 dosyanın tamamı zip, liste ve tek indirici kurucu; dmg, msi, msix, AppImage, deb yok.
- Komut satırı küçültmede güçlü (`kucult`, `plan`, `izle`, `profiller`, `--json`), ama yalnız küçültüyor: dönüştürme ve kayıt komutu yok, ilerleme makinece okunmuyor.
- En ağır üç eksik: Windows kod imzası (DAG-02), Linux'ta paket (DAG-04), çalışan uygulamayı dışarıdan süren kanal (DAG-35).
- En ucuz üç kazanç: "Yüklü uygulamalar" kaydı (DAG-17), adı konmuş sessiz kurulum (DAG-16), makinece okunur ilerleme (DAG-29). İlk ikisi winget'in ön koşulu.
- winget (DAG-01) öncelik 1 satırların en ucuzu: tek engel sahibin CLA onayı ve DAG-P1.
- macOS zincirinin tamamı (imza, dmg, Homebrew) tek karara bağlı: ücretli Apple geliştirici üyeliği.
- 42 satırın 24'ü sahip kararı bekliyor; karar beklemeyen 17 satırın 11'i dört pakette toplandı.

## Paket Önerisi

Sıra önerilen yapım sırasıdır. İlk dört paket karar beklemez. DAG-P2 ile DAG-P4 aynı komut satırı dosyalarına dokunur; aynı anda değil, sırayla yapılmalı.

### DAG-P1 — Sessiz Kurulum Ve Uygulama Kaydı
- Satırlar: DAG-16, DAG-17, DAG-19, DAG-28
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Setup/Program.cs, src/VidShrink.Core/Setup/SetupRunner.cs, src/VidShrink.Core/Setup/SetupText.cs, Install-VidShrink.ps1, docs/kurulum.md
- Plan: Kurucuya adı belli bir sessiz kip eklenir (`--silent`: pencere açmaz, soru sormaz, hata olunca beklemeden sıfırdan farklı kodla çıkar) ve yardım metni ile docs/kurulum.md'ye yazılır. Kurulum, Windows'un Ayarlar listesini besleyen kullanıcı düzeyi kaldırma kaydını (ad, sürüm, yayıncı, simge, kaldırma komutu) yazar, kaldırma onu siler; kaldırmaya ayar klasörünü de silen bir seçenek gelir. İsteğe bağlı ve varsayılanı kapalı bir seçenek kullanıcı PATH'ine yalnız küçük bir `bin` klasörü ekler; içinde komut satırı aracını çağıran küçük bir sarmalayıcı (örneğin tek satırlık `vidshrink.cmd`) durur, çünkü kökteki başlatıcı aynı adı taşıyor. Doğrulama: kurulum ve kaldırma testleri sahte kayıt köküyle, elle de Ayarlar listesinde görünme, sürümün doğru yazılması ve kaldırmanın iz bırakmaması.

### DAG-P2 — Komut Satırı Çıktısı Ve Dönüştürme
- Satırlar: DAG-29, DAG-30, DAG-33, DAG-34
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Cli/CliRequest.cs, src/VidShrink.Cli/CliApp.cs, src/VidShrink.Core/PresetLibrary.cs, docs/cli.md, docs/cli.tr.md
- Plan: Dört ek aynı iki dosyada olduğu için tek elde yapılır. `--json` ile birlikte ilerleme satır başına bir JSON nesnesi olarak yazılır (NDJSON: her satırı ayrı okunabilen JSON akışı); `profiller` komutuna çekirdekteki hazır `Export` işlevini çağıran dışa aktarma seçeneği, bir de bash, zsh, fish ve PowerShell için Tab tamamlama betiğini basan `tamamlama` komutu gelir. `donustur` komutu Dönüştür sekmesinin argüman üretimini çağırır ve kodek kümesini arayüzdekiyle eşitler; o üretim çekirdekte değilse önce oraya taşınır (bu turda bakılmadı). Doğrulama: ayrıştırma ve çıktı testleri, `donustur` için arayüzle aynı ffmpeg argümanını üretme testi.

### DAG-P3 — Windows 11 İlk Menüde Küçültme
- Satırlar: DAG-37
- İş: orta
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.ShellExtension/VidShrink.ShellExtension.cpp, src/VidShrink.ShellExtension/AppxManifest.template.xml, src/VidShrink.Core/ShellIntegration.cs, src/VidShrink.Core/Setup/SetupRunner.cs, Install-VidShrink.ps1
- Plan: Kabuk eklentisi (Windows 11'in ilk sağ tık menüsüne girdi koyan C++ bileşeni) bugün tek komut döndürüyor ve alt komut kolu boş. Açma girdisinin yanına "VidShrink ile Küçült" üst girdisi eklenir: `GetFlags` `ECF_HASSUBCOMMANDS` döndürür, `EnumSubCommands` altı hedefi `ShellIntegration.QuickShrinkMenu` sırasıyla sayar ve her biri başlatıcıyı `--kucult` ile çağırır. Etiketler var olan `ShellLabels` kayıt anahtarından okunur, böylece menü dili korunur. Doğrulama: listeyi üç kurucuyla karşılaştıran mevcut teste eklenti dizisi eklenir; Windows 11'de ilk menüde alt menünün görünmesi ve bir hedefin küçültmeyi başlatması elle bakılır.

### DAG-P4 — Yerel Denetim Borusu Ve Kayıt Komutu
- Satırlar: DAG-35, DAG-31
- İş: büyük
- Karar: gerekmez
- Dokunacağı yer: src/VidShrink.Core/SingleInstanceChannel.cs, src/VidShrink.Core/ShrinkRequest.cs, src/VidShrink.App/Integration/, src/VidShrink.App/Recorder/, src/VidShrink.Cli/, docs/
- Plan: Tek örnek borusu (ikinci kopyanın ilk kopyaya dosya yolu ilettiği, yalnız aynı kullanıcıya açık adlandırılmış boru) belgeli bir komut kanalına genişletilir: satır başına bir JSON komutu ve yanıtı (oynat, duraklat, konum, dosya aç, kaydı başlat ve durdur, kuyruğa küçültme ekle, durum sor). Yeni komutlar Ayarlar'daki bir anahtar ya da başlatma bayrağı açılmadıkça reddedilir; ağ soketi açılmaz, yeni paket eklenmez. Komut satırına bu kanalın istemcisi olan kayıt başlatma, kayıt durdurma ve durum komutları gelir; protokol docs/ altında belgelenir. Doğrulama: boru üstünden komut ve yanıt testleri, kapalıyken reddetme testi, elle de komutla kayıt başlatıp durdurma.

### DAG-P5 — Winget, Scoop Ve Chocolatey Paketleri
- Satırlar: DAG-01, DAG-08, DAG-12
- İş: orta
- Karar: sahip: Windows paket dizinlerinde yayın yapılsın mı; winget için PR sahibin hesabından açılır ve Microsoft katkı sözleşmesi onaylanır, Scoop için kova deposu açılır, Chocolatey için hesap gerekir.
- Dokunacağı yer: .github/workflows/release.yml, yeni packaging/winget/, packaging/scoop/, packaging/chocolatey/; depo dışında microsoft/winget-pkgs PR'ı ve kova deposu
- Plan: DAG-P1'den sonra yapılır: winget etkileşimsiz kurulum ister ve kurulu sürümü kaldırma kaydından tanır. winget bildirimi (paketin adını, sürümünü, kurucu adresini ve sha256'sını taşıyan YAML dosyaları) `VidShrink-Setup.exe` için yazılır; Scoop için tek JSON, Chocolatey için paket tanımı ve kurulum betiği hazırlanır. `release.yml` her sürümde üç bildirimin sürüm ve özet alanını tazeler. Doğrulama: bildirim doğrulama komutu ve temiz bir Windows'ta kurma, yükseltme ve kaldırma.

### DAG-P6 — Windows Kod İmzası
- Satırlar: DAG-02
- İş: orta
- Karar: sahip: SignPath Foundation'a başvurulsun mu (ücretsiz; başvuruyu ve her imza onayını sahip yapar), yoksa ücretli sertifika mı alınsın.
- Dokunacağı yer: .github/workflows/release.yml, docs/imzalama.md, README.md, src/VidShrink.ShellExtension/AppxManifest.template.xml
- Plan: Başvurunun onayı dışarıda beklediği için karar erken verilmeli; öteki paketler bu sırada yürür. Onaydan sonra `release.yml`'e imza adımı girer: kurucu, başlatıcı, uygulama, komut satırı aracı ve kabuk eklentisi, x64 ve ARM64 için; sha256 listeleri imzadan sonra üretilir. README'deki "başvuru sürüyor" cümlesi gerçek duruma çekilir. Doğrulama: yayın dosyalarında imza denetimi geçerli döner; temiz makinede SmartScreen ve Akıllı Uygulama Denetimi davranışı elle bakılır.

### DAG-P7 — Ön Sürüm Güncelleme Kanalı
- Satırlar: DAG-24
- İş: orta
- Karar: sahip: ön sürüm yayımlansın mı ve Ayarlar'a kanal seçimi eklensin mi; sürüm kesme düzeni değişir.
- Dokunacağı yer: src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.App/MainWindow.axaml, src/VidShrink.App/Locales/, .github/workflows/release.yml
- Plan: `release.yml` ekli etiketleri GitHub'da ön sürüm olarak yayımlar; kararlı kanal bunları görmez. Ayarlar'daki Güncelleme kartına kararlı ve ön sürüm seçimi gelir, varsayılan kararlı kalır; `UpdateCheck` seçili kanala göre en yeni sürümü seçer (ek ayrıştırması hazır). Ön sürümden kararlıya dönüş ve sessiz güncellemenin düşüş sayacı iki kanalda da test edilir. Aynı eksik komşu tabloda AYR-09; tek iş olarak yapılır.

### DAG-P8 — Flathub Ve AppImage Paketleri
- Satırlar: DAG-04, DAG-09
- İş: büyük
- Karar: sahip: Flathub'a uygulama başvurusu yapılsın mı (sahibin hesabından; ffmpeg ve libmpv paketin içinde gelir) ve yayına AppImage eklensin mi.
- Dokunacağı yer: yeni packaging/flatpak/, packaging/appimage/, .github/workflows/release.yml, src/VidShrink.Core/UpdateCheck.cs, install-vidshrink.sh
- Plan: Flatpak bildirimi (uygulamanın nasıl derleneceğini ve hangi izinlerle çalışacağını söyleyen dosya) yazılır; ffmpeg ve libmpv Flathub çalışma ortamından ya da bildirimde kaynaktan derlenerek gelir, uygulama kimliği, tanıtım dosyası ve simge eklenir. Paketin içinde kendini güncelleme kapatılır, güncellemeyi Flathub yapar. AppImage aynı yayın çıktısından `release.yml` içinde üretilir ve sha256 listesine girer. Doğrulama: CI'da paketi kurup küçültme ve oynatma denemesi; AppImage için temiz bir Ubuntu koşucusunda açılış.

### DAG-P9 — Tam Paket Ve Çevrimdışı Kurucu
- Satırlar: DAG-10, DAG-11, DAG-20
- İş: büyük
- Karar: sahip: ffmpeg ve libmpv ikilileri kendi yayın dosyalarımızın içinde dağıtılsın mı (yüzlerce MB ek boyut ve lisans yükümlülüğü).
- Dokunacağı yer: .github/workflows/release.yml, src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.Setup/Program.cs, src/VidShrink.Core/Setup/SetupDownloads.cs, install-vidshrink.sh, docs/kurulum.md
- Plan: `release.yml` her hedef için uygulama, ffmpeg ve libmpv'yi bir arada taşıyan ikinci bir arşiv üretir; taşınabilir kip (komşu tabloda AYR-05), uygulama klasöründe bir işaret dosyası görünce ayarı yanına yazar ve kendini güncellemeyi kapatır. Çevrimdışı kurucu aynı arşivi kurucunun var olan `--asset-source` yoluyla kullanır. macOS ve Linux betiği ffmpeg yoksa durmak yerine sha256'sı sabitlenmiş derlemeyi indirir. Doğrulama: ağ kapalıyken kurulum ve ilk küçültme, taşınabilir kopyada kullanıcı klasörüne dosya yazılmadığının testi.

### DAG-P10 — macOS İmza, Dmg Ve Homebrew
- Satırlar: DAG-05, DAG-06, DAG-07
- İş: büyük
- Karar: sahip: ücretli Apple geliştirici üyeliği alınsın mı; alınırsa dmg yayımlanır ve Homebrew için kendi tap depomuz açılır.
- Dokunacağı yer: macos-app-bundle.sh, .github/workflows/release.yml, install-vidshrink.sh, src/VidShrink.Core/UpdateCheck.cs; depo dışında tap deposu
- Plan: `macos-app-bundle.sh` ad-hoc imza yerine Developer ID ile imzalar ve paketi noterlemeye gönderir (noterleme: Apple'ın paketi tarayıp onay damgası vermesi); libmpv ve öteki kitaplıklar da imzalanır. İmzalı paketten sürükle bırak dmg üretilir ve yayına eklenir. Homebrew ana dizini en az 75 yıldız, sahibin kendi başvurusunda 225 yıldız arıyor ve bizde 12 var; bu yüzden cask (Homebrew'un hazır uygulama tarifi) kendi tap depomuzda (projeye ait Homebrew paket deposu) yayımlanır ve `release.yml` her sürümde sürüm ile sha256'yı tazeler. Doğrulama: CI'da Gatekeeper ve damga denetimi, temiz Mac'te tarayıcıdan indirip çift tıkla açma elle.

### DAG-P11 — Microsoft Store Paketi
- Satırlar: DAG-03
- İş: büyük
- Karar: sahip: Microsoft Store geliştirici hesabı açılsın mı ve uygulama MSIX olarak mağazaya girsin mi.
- Dokunacağı yer: yeni packaging/msix/, .github/workflows/release.yml, src/VidShrink.Core/UpdateCheck.cs, src/VidShrink.Core/Setup/
- Plan: Mağazanın exe yolu imzalı, sessiz ve çevrimdışı kurucu ister; bizim kurucu indirici olduğu için o yol DAG-P6, DAG-P1 ve DAG-P9 bitmeden kapalı. MSIX yolunda paketi Microsoft imzalar: uygulama, ffmpeg ve libmpv tek pakete girer, dosya ilişkisi ve sağ tık menüsü paket bildirimine taşınır, kurulum kökü yazılamaz olduğu için kendini güncelleme kapatılır. Önce DAG-P9 kararı gerekir. Doğrulama: paket sertifika aracı, mağaza ön incelemesi ve yerelde kurup beş sekmenin açılışı.

### DAG-P12 — Betik Ve Eklenti Arabirimi
- Satırlar: DAG-36
- İş: büyük
- Karar: sahip: uygulamaya betik motoru (yeni bağımlılık) eklensin ve sürümler arası korunacak bir eklenti arabirimi sözü verilsin mi.
- Dokunacağı yer: src/VidShrink.Core/, src/VidShrink.App/, yeni docs/ sayfası
- Plan: DAG-P4'ten önce başlanmaz: dış süreçten gelen komut kanalı, eklenti isteklerinin kumanda ve otomasyon kısmını bağımlılık eklemeden karşılar. Evet denirse ilk adım dar tutulur: kullanıcı klasöründeki betikler oynatıcı ve kuyruk olaylarına abone olur ve komut kanalındaki komutları çağırır; arayüze parça ekleyen eklenti sonraya kalır. Betik dili ve motoru kararın parçasıdır. Doğrulama: örnek betikle olay ve komut testi, betik hatasının uygulamayı düşürmediğinin testi.

## Pakete Girmeyenler

- DAG-13 — Snap mağaza hesabı ister; aynı kitleyi DAG-P8 karşılar.
- DAG-14 — tek rakipte belgeli, yeni ikili dağıtımı; DAG-P8 çıkınca gereği azalır.
- DAG-15 — ücretli Apple hesabı ve mağaza kuralları ister; DAG-P10 bitmeden başlanamaz.
- DAG-18 — dar kitle; makine geneli kopyada güncellemenin nasıl davranacağı önce karar ister.
- DAG-21 — DAG-P3 ile aynı bileşen ama ayrı derleme hedefi ve ayrı kurulum kolu; bedavaya gelmiyor, DAG-P3'ten sonra ucuzlar.
- DAG-22 — dar kitle, yeni ikili dağıtımı.
- DAG-23 — kapalı karar (macOS alt sürümü 14).
- DAG-25 — karar istemeyen tek Linux işi, ama hiçbir paketin dosyasına denk gelmiyor; DAG-P8 onaylanmazsa öne alınmalı.
- DAG-26 — yeni bağımlılık ister; bugünkü tek paket yolu çalışıyor.
- DAG-27 — imza anahtarını saklamak sahibin işi; köken kaydı adımı (Ek Notlar) anahtarsız küçük parçası.
- DAG-32 — önce arayüzde kuyruğu dosyaya kaydeden yol gerekir; iş iki parça.
- DAG-38 — resmi kaynakta tek rakip; ffmpeg'li görüntü DAG-P9 kararına bağlı.
- DAG-39 — DAG-P4 bitmeden başlanamaz; hazır bağlantı ağdan dinleyen kanal ve ayrı yayın ister.
- DAG-40 — dar kitle; hiçbir paketin dosyasına denk gelmiyor.
- DAG-41, DAG-42 — rakip belgesi bulunamadı, öncelik 3.
