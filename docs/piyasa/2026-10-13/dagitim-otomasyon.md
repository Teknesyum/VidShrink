# Dağıtım, Kurulum, Güncelleme Ve Otomasyon — Pazar Notları

13 Ekim 2026. Kod okuması `origin/main` 9de48d8b. Eksik tablosu: `dagitim-otomasyon-eksikler.md` (42 satır, önek `DAG`). Köşeli sayılar bu dosyanın sonundaki "Kaynaklar" listesine bakar.

Kısa sözlük, ilk geçtikleri yerde aranmasın diye:

- **winget, Scoop, Chocolatey:** Windows'ta programı tek komutla kuran ve güncelleyen paket yöneticileri. **Homebrew cask:** aynı işin macOS karşılığı.
- **Flathub, Snap, AppImage:** Linux'ta dağıtımdan bağımsız paket biçimleri; ilk ikisi mağaza gibi çalışır ve kendi günceller, AppImage tek dosyadır.
- **MSIX:** Windows'un mağaza paket biçimi; imzasız kurulmaz. **MSI:** Windows'un klasik kurulum paketi.
- **SmartScreen ve Akıllı Uygulama Denetimi:** Windows'un imzasız programa uyarı veren ya da onu engelleyen korumaları. **Gatekeeper ve noterleme:** macOS'un karşılığı; noterleme, Apple'ın paketi tarayıp onay damgası vermesidir.
- **Yüklü uygulamalar kaydı:** Windows Ayarlar'daki kaldırma listesini besleyen kayıt defteri anahtarı.
- **NDJSON:** her satırı ayrı bir JSON nesnesi olan çıktı. **IPC:** çalışan bir programa başka programın komut yolladığı yerel kanal.

## Envanter

### Yayın Ve Kurulum

- Güncel sürüm v1.2.15, 10 Ekim 2026 [1]. Altı hedef yayımlanıyor: win-x64 (68,6 MB), win-arm64 (56,5 MB), osx-arm64 (56,1 MB), osx-x64 (58,4 MB), linux-x64 (56,0 MB), linux-arm64 (53,6 MB). Yayın matrisi `.github/workflows/release.yml:347`, başlatıcı matrisi `:313`.
- Her hedef için üç dosya: `vidshrink-<rid>.zip`, `manifest-<rid>.json`, `checksums-<rid>.txt`. Windows'ta ayrıca başlatıcı arşivi, güncelleme paketi (`vidshrink-update-win-x64.zip` 72,0 MB, arm64 59,7 MB) ve tek kurucu `VidShrink-Setup.exe` (11,8 MB) [1].
- Yayında dmg, msi, msix, AppImage, deb, rpm, flatpak yok [1].
- Kurucu tek dosya ve x64; makinenin mimarisini okuyup ARM64 makineye win-arm64 kurar (`release.yml:389-395`). Kurucu bir indiricidir: uygulama arşivini kurulum anında ağdan çeker (`docs/kurulum.md:14-15`).
- Kurucu bayrakları: `--help`, `--uninstall`, `--install-root`, `--registry-root`, `--menu-language`, `--skip-shortcuts`, `--no-launch`, `--shortcut-dir`, `--tag`, `--asset-source`, `--download-ffmpeg`, `--timings`, `--console`, `--panel` (`src/VidShrink.Setup/Program.cs:89-104`). Adı "sessiz" olan bir kip yok.
- Kurulum kökü `%LOCALAPPDATA%\Programs\VidShrink` (`Program.cs:112`), kayıt kökü `HKCU:\Software\Classes` (`Program.cs:81`). Yönetici hakkı ve .NET gerekmez (`docs/kurulum.md:41`, `:99`).
- İkinci Windows yolu tek satır PowerShell (`docs/kurulum.md:35-48`). macOS ve Linux yolu `curl ... | sh` (`docs/kurulum.md:67-90`; hedef seçimi `install-vidshrink.sh:187`, `:196-220`).
- macOS'ta gerçek uygulama paketi kurulur: `~/Applications/VidShrink.app` (`install-vidshrink.sh:7`), Info.plist `macos-app-bundle.sh:62-105`. İmza yalnız ad-hoc, yani Apple kimliği taşımayan yerel imza (`macos-app-bundle.sh:117-118`).
- Linux'ta paket yok: `~/.local/share/vidshrink` altına açılır, `~/.local/bin/vidshrink` bağı ve bir `.desktop` dosyası yazılır (`docs/kurulum.md:74-75`; `install-vidshrink.sh:101-109`).
- ffmpeg: Windows kurucusu yoksa pinli GyanD 9.0 arşivinden yalnız iki exe'yi aralık okumasıyla, sabit SHA-256 ile indirir (`docs/kurulum.md:19-21`). macOS ve Linux kurucusu paket yöneticisi komutunu söyler ve durur (`docs/kurulum.md:83-86`; `install-vidshrink.sh:233-242`).
- libmpv: Windows'ta pinli derleme, macOS'ta MPVKit 1.0.0 dylib, Linux'ta paket komutu (`docs/kurulum.md:86-90`; `install-vidshrink.sh:265-274`).
- Sistem gereksinimi: Windows 10 ya da 11, macOS 14 ve üstü, X11 ya da Wayland Linux (`docs/kurulum.md:94`; karar `docs/YOL-HARITASI.md:72`).
- Kaldırma: `VidShrink-Setup.exe --uninstall` (`docs/kurulum.md:29`). Menüleri, Appx paketini, dosya ilişkilerini, kısayolları ve kurulum kökünü siler; ayar klasörüne dokunmaz (`src/VidShrink.Core/Setup/SetupRunner.cs:211-249`). macOS ve Linux'ta `--uninstall` (`install-vidshrink.sh:135`, `:177`).
- İmza: kurucu ve uygulama imzasız (`docs/imzalama.md:3`). SignPath Foundation başvurusu için adımlar yazılı, hiçbiri gönderilmemiş (`docs/imzalama.md:6`, `:8-25`). README de söylüyor: Akıllı Uygulama Denetimi açık makinede hangi yoldan kurulursa kurulsun engelleniyoruz (`README.md:53-54`).
- İş akışlarında imza, noterleme ya da yapı kanıtı adımı yok. `attest, codesign, signtool, notarytool, sigstore, cosign, gpg` araması yalnız `macos-libmpv-gate.yml:87` (doğrulama) ve `macos-app-bundle.sh:117-118` (ad-hoc imza) buldu.
- Paket kanalı: hiçbir dizinde değiliz. winget-pkgs'te `manifests/t/Teknesyum` yok, Scoop Extras'ta `vidshrink` yok [28][29]. README ayrı bir uygulamayı, "Teknesyum Base"i de kurulum yolu sayıyor (`README.md:39-43`); o da imzasız.

### Güncelleme

- Windows'ta güncelleme varsayılan açık ve sessiz: sahne açılıştan sonra iner, kurulum çıkışta yerinde takasla olur (AGENTS.md güncelleme maddeleri; `src/VidShrink.Core/InPlaceUpdate.cs:14-16`, `:36`, `:106-108`, `:143`).
- İndirme tek paket: `StagePackageAsync` çağrısı `src/VidShrink.Core/UpdateStaging.cs:191`, tanımı `:287`. Yarıda kalan paket kaldığı yerden sürer (`:387`). Paket satırı yoksa eski dosya dosya aralık yoluna düşülür (`:643`, paralel `:176-186`).
- Doğrulama yalnız aynı yayındaki SHA-256 listesiyle. Ayrık imza yok: `minisign, Ed25519, ECDsa, RSA.Create, VerifyData, WinVerifyTrust, Authenticode, X509` araması boş.
- macOS'ta bütün paket takası (`docs/kurulum.md:166-173`). Karantinalı kopyada kapalı: `Translocated` ve `CanSwap` (`src/VidShrink.Core/UpdateCheck.cs:1502-1511`).
- Linux'ta yalnız bildirim (`docs/kurulum.md:175-176`).
- Kanal yok: sürüm eki ayrıştırılıyor ama seçim yok (`UpdateCheck.cs:286-301`); `beta, channel, kanal` araması güncelleme dosyalarında boş.
- Geri alma yalnız düşen takas için. Kullanıcıya dönük "önceki sürüme dön" yok; tek yol kurucunun `--tag` bayrağı.
- Kapatma: Ayarlar'daki anahtar (`docs/kurulum.md:151-153`) ve `VIDSHRINK_UPDATE_DISABLED` ortam değişkeni (`UpdateCheck.cs:1621`; `src/VidShrink.Launcher/Updater.cs:84`).
- Paketçiye yarayan öbür ortam değişkenleri: `VIDSHRINK_SETTINGS_PATH` (`UpdateCheck.cs:481`), `VIDSHRINK_UPDATE_SOURCE` (`UpdateCheck.cs:1629`), `VIDSHRINK_UPDATE_BASE_URL` (`UpdateStaging.cs:539`), `VIDSHRINK_LIBMPV`.
- Ölçümler `docs/olcumler/` altında var: `hizli-guncelleme.md` (yerinde takas ortanca 70 ms, eski yol 158-179 ms, n=10, 25 Eylül 2026), `kapanista-guncelleme.md`, `kurulum-suresi.md` (kurucu 4,5-6,1 sn, FFmpeg indirmeli 11,9 sn, v0.8.2), `macos-kurulum-gercek.md`, `guncelleme-sessiz-dusus.md`.

### Komut Satırı

- İkili: Windows ve Linux'ta `vidshrink`, macOS'ta `vidshrink-cli` (`docs/cli.md:6-7`; `src/VidShrink.Cli/VidShrink.Cli.csproj:8`, `:14`; gerekçe `release.yml:358-361`).
- Komutlar: `kucult`, `plan`, `izle`, günlük ve profil listesi. Kapalı küme: `public enum CliCommand { Help, Version, Shrink, Plan, Watch, Gunluk, Profiller }` (`src/VidShrink.Cli/CliRequest.cs:6`). Kodek kümesi `Auto, H264, Hevc, Av1, Vp9` (`:8`).
- `--json`: sonuç stdout'a JSON, `izle` kipinde NDJSON (`docs/cli.md:18-20`, `:127`). İlerleme stderr'e yerelleştirilmiş metin olarak gider; JSON ilerleme olayı yok (`src/VidShrink.Cli/CliApp.cs:878-890`).
- Çıkış kodları belgeli: `0, 2, 3, 1, 64, 130`; izlemede `0, 4, 1, 64, 130` (`docs/cli.md:112-113`, `:165-167`).
- Ön ayar: `--profil-dosyasi` VidShrink dosyasını doğrudan, HandBrake ön ayar dosyasını çevirerek okur (`CliApp.cs:594-617`). Dışa aktaran komut yok.
- Her uzun seçeneğin İngilizce takma adı var (`docs/cli.md:65-110`).
- Windows'ta kurucu PATH'e yazmıyor; PATH yalnız ffmpeg aramak için okunuyor (`Program.cs:145`; `Install-VidShrink.ps1:33`). Ayrıca başlatıcı `VidShrink.exe` ile komut satırı `vidshrink.exe` aynı adı taşıyor (`src/VidShrink.Launcher/VidShrink.Launcher.csproj:8`); Windows büyük küçük harf ayırmaz.

### Sistem Bütünleşmesi

- Windows 11 sağ tık menüsü: var, ama yarım. Menü, yalnız kimlik veren bir Appx paketiyle (sparse package: içinde dosya olmayan, programa mağaza kimliği kazandıran paket) kaydediliyor: `Add-AppxPackage -Register ... -ExternalLocation` (`Program.cs:210-211`; `SetupRunner.cs:252-271`). Manifest `src/VidShrink.ShellExtension/AppxManifest.template.xml`, x64.
- İlk menüde yalnız açma girdisi var. Altı hedefli hızlı küçültme alt menüsü (8, 16, 20, 25, 50, 100 MB) klasik yoldur ve Windows 11'de "Daha fazla seçenek göster" altında kalır (`src/VidShrink.Core/ShellIntegration.cs:46-58`).
- ARM64'te eklenti yok: başlatıcı arşivinde `shell/` klasörü yalnız x64'te (`release.yml:401-404`).
- macOS'ta dosya türü kaydı var (`macos-app-bundle.sh:82`), sağ tık eylemi yok. Linux'ta `.desktop` dosyasında `MimeType` var, `Actions` satırı yok (`install-vidshrink.sh:101-109`). `docs/kurulum.md:182` ikisine de "no" diyor.
- İzleme klasörü hem komut satırında (`docs/cli.md:115-167`) hem arayüzde (`src/VidShrink.App/QueueWatch.cs:1-40`; `ShrinkJobWindow.Izleme.cs:94`, `:155`).
- Dışarıdan denetim: belgeli arabirim yok. Yalnız tek örnek için iç adlandırılmış boru var (`src/VidShrink.Core/SingleInstanceChannel.cs:65`, `:95-99`; `src/VidShrink.Core/ShrinkRequest.cs:129`, `:147-148`).

### Önde Olduğumuz Yerler

- Windows ARM64 ve Linux arm64 derlemesi var. ShareX, File Converter ve StaxRip yalnız x64 yayımlıyor [12][22][61]; Shutter Encoder'da ARM64 hâlâ açık istek [51].
- .NET çalışma zamanı gerekmez. HandBrake 1.11.0 arayüzü ayrıca .NET Desktop Runtime 10 kurdurur [2].
- Çıkış kodları ve NDJSON belgeli; HandBrake'te "JSON çıktı JSON değil" şikâyeti açık [48].
- İzleme klasörü hem komut satırında hem arayüzde; HandBrake'te bu istek kapatılmış [48].
- HandBrake ön ayar dosyasını okuyoruz.
- Kurulum yönetici hakkı istemiyor; kurulum ve güncelleme süreleri ölçülmüş.

### Eskiyen Belge Satırları

- `docs/kurulum.md:50` "Only `win-x64` is published." Bayat: altı hedef var, kurucu ARM64'e win-arm64 kuruyor.
- `docs/kurulum.md:73-76` yalnız `linux-x64` diyor. Bayat: `linux-arm64` yayımlanıyor.
- `docs/kurulum.md:180` hedef satırı. Bayat (aynı neden).
- `docs/kurulum.md:117-130` akış şeması. Bayat: başlatıcının arka plan indirme turu kalktı (AGENTS.md).
- `docs/kurulum.md:108-109` "1.7 MB of a 519 MB". Büyük olasılıkla bayat: bugünkü yol tek paket (72,0 MB). Tam doğrulanmadı.
- `docs/kurulum.md:184` ffmpeg kaynağı "WinGet" diyor; `:19-21` kurucunun WinGet çağırmadığını söylüyor. İki yol var, tablo yalnız birini anlatıyor.
- `README.md:74-75` "four targets". Bayat: altı.
- `vidshrink-pad.xml:36`, `:93` sürüm 0.8.5 ve o sürümün indirme adresi. Bayat: 1.2.15.
- `docs/piyasa/tarama-2026-10-05.md:48` "izleme klasörü arayüzde yok". Bayat: arayüzde var (`QueueWatch.cs`). O taramanın "Karar Gerektirmeyenler" 2. kalemi yapılmış sayılır.

### Yok Hükümlerinin Arama Kaydı

Her satır: aranan adlar, sonuç, en yakın kod.

- **DAG-01, DAG-08, DAG-12 (winget, Scoop, Chocolatey).** `winget, choco, scoop` adları `release.yml` ve `README.md` içinde arandı. Tek eşleşme ffmpeg kaynağı olarak WinGet (`docs/kurulum.md:45-46`) ve "no PowerShell, no WinGet" (`README.md:44-46`). Dizinlerde paketimiz yok [28][29]. En yakın kod: `release.yml:347`.
- **DAG-03, DAG-15 (mağazalar).** `msix, store, appx` arandı. Tek Appx kullanımı sağ tık menüsünün kimlik paketi (`Program.cs:210-211`). Uygulamanın kendisi paketlenmiyor.
- **DAG-04, DAG-09, DAG-13, DAG-14 (Flathub, AppImage, Snap, deb).** `flatpak, flathub, snap, appimage, .deb, .rpm` arandı, eşleşme yok. Yayın varlıklarında da yok [1]. En yakın kod: `install-vidshrink.sh:187`.
- **DAG-06, DAG-07 (dmg, Homebrew).** `dmg, brew` arandı. `brew` yalnız ffmpeg ve libmpv için söylenen komutta (`install-vidshrink.sh:233-242`, `:265-274`). En yakın kod: `macos-app-bundle.sh:62-105`.
- **DAG-17 (yüklü uygulamalar kaydı).** İki ayrı arama: `CurrentVersion\Uninstall, DisplayVersion, UninstallString, App Paths` ve `QuietUninstall, EstimatedSize, DisplayName, DisplayIcon, Installed apps, Programs and Features`. `src` ve `Install-VidShrink.ps1` içinde ilgili eşleşme yok. En yakın kod: `SetupRunner.cs:211-249`.
- **DAG-22 (32 bit).** Yayın matrisinde x86 yok (`release.yml:347`); kurucu 32 bit makinede durur (`docs/kurulum.md:50-51`).
- **DAG-31 (komutla kayıt).** `kayit, kaydet, record, capture` adları `src/VidShrink.Cli` içinde arandı; yalnız C# `record` türleri ve dil dosyası çıktı. Komut kümesi kapalı: `CliRequest.cs:6`.
- **DAG-32 (kuyruk dosyası).** `kuyruk, queue, export, disa-aktar` araması `src/VidShrink.Cli` içinde boş. En yakın kod: `CliApp.cs:594-617`.
- **DAG-34 (kabuk tamamlama).** `completion` araması boş.
- **DAG-36 (betik ve eklenti).** `IPlugin, PluginHost, NLua, MoonSharp, Jint` araması yalnız dil dosyalarını buldu.
- **DAG-39 (tuş takımı).** `HttpListener, TcpListener, UnixDomainSocket, WebSocket` araması boş. En yakın kod: `SingleInstanceChannel.cs:65`.
- **DAG-41 (macOS sağ tık).** `NSServices, CFBundleURLTypes, Quick Action` araması boş. En yakın kod: `macos-app-bundle.sh:82`.
- Tabloya girmeyen ama aranıp bulunmayanlar: `URL Protocol, vidshrink://, x-scheme-handler` (URL protokolü); `SendTo` ("Gönder" menüsü); `nautilus, servicemenu` (Linux dosya yöneticisi eklentisi); `Dockerfile`; `Software\Policies, GroupPolicy`; `JumpList`.

### Defter Ve Önceki Kararlar

- `.claude/acik.md` içinde bu alanın terimleri (`winget, imza, signpath, arm64, flatpak, store, msix, homebrew, appimage, silent, uninstall, docker, notar, dmg, scoop, choco, SmartScreen`) için eşleşme yok. Bu yüzden hiçbir satır `defterde` almadı.
- K1-K12 kararlarından hiçbiri bu alana düşmüyor. Tek kapalı kalem macOS alt sürümü (DAG-23, `docs/YOL-HARITASI.md:72`).
- Bağlantılı not: rafta duran K3 (pencereyi WGC ile yakalama) için yol haritası, sarı kenarlığı kapatmanın MSIX paketi istediğini yazıyor (`docs/YOL-HARITASI.md:115-116`). DAG-03 o kapıyı da açar.

## Rakipler

Sürümler 13 Ekim 2026'da `gh api repos/<depo>/releases/latest` ile okundu; öyle okunmayanlar ayrıca işaretli.

| Ad | Sürüm | Tarih | Tür | URL |
|---|---|---|---|---|
| HandBrake | 1.11.2 | 2026-06-07 | açık kaynak, kodlayıcı | https://github.com/HandBrake/HandBrake [2][3] |
| OBS Studio | 32.2.2 | 2026-08-14 | açık kaynak, kayıt ve yayın | https://github.com/obsproject/obs-studio [5] |
| VLC | 3.0.24 (winget dizininden) | doğrulanmadı | açık kaynak, oynatıcı | https://wiki.videolan.org/VLC_command-line_help/ [39][28] |
| mpv | v0.41.0 | 2025-12-21 | açık kaynak, oynatıcı | https://github.com/mpv-player/mpv [8] |
| ShareX | v21.0.0 | 2026-07-03 | açık kaynak, ekran kaydı (yalnız Windows) | https://github.com/ShareX/ShareX [12] |
| LosslessCut | v3.69.0 | 2026-06-04 | açık kaynak, kayıpsız kesim | https://github.com/mifi/lossless-cut [14] |
| Shutter Encoder | 20.4 | 2026-09-28 | açık kaynak, dönüştürücü | https://github.com/paulpacifico/shutter-encoder [20] |
| Shotcut | v26.9.27 | 2026-09-28 | açık kaynak, kurgu | https://github.com/mltframework/shotcut [21] |
| Kdenlive | 26.08.1 (winget dizininden) | doğrulanmadı | açık kaynak, kurgu | https://github.com/microsoft/winget-pkgs/tree/master/manifests/k/KDE/Kdenlive [28] |
| File Converter | v2.2 | 2026-02-13 | açık kaynak, sağ tık dönüştürücü (yalnız Windows) | https://github.com/Tichau/FileConverter [22] |
| PowerToys | v0.101.2362.0 | 2026-08-25 | Microsoft, açık kaynak; dağıtım ölçütü | https://github.com/microsoft/PowerToys [23] |
| IINA | v1.5.0 | 2026-10-03 | açık kaynak, macOS oynatıcı | https://github.com/iina/iina [26] |
| ScreenToGif | 2.43.2 | 2026-07-28 | açık kaynak, ekran kaydı (yalnız Windows) | https://github.com/NickeManarin/ScreenToGif [27] |
| StaxRip | v2.52.6 | 2026-09-27 | açık kaynak, kodlayıcı arayüzü (yalnız Windows) | https://github.com/staxrip/staxrip [61] |
| FFmpeg Batch AV Converter | 3.2.9 | 2026-05-27 | ücretsiz, toplu dönüştürücü (yalnız Windows) | https://github.com/eibols/ffmpeg_batch [62] |
| Constrict | 25.12.1 | 2025-12-27 | açık kaynak, hedef boyuta küçültme (Linux) | https://github.com/Wartybix/Constrict [63] |
| Unmanic | 0.4.1 | 2026-08-14 | açık kaynak, kitaplık otomasyonu (sunucu) | https://github.com/Unmanic/unmanic [40] |
| FFmpeg | 9.0 (Gyan derlemesi, winget dizininden) | doğrulanmadı | açık kaynak, komut satırı | https://ffmpeg.org/ffmpeg.html [38][28] |
| Tdarr | doğrulanmadı | son itme 2026-10-10 | sunucu otomasyonu | https://github.com/HaveAGitGat/Tdarr [60] |
| Velopack (çatı) | 1.2.161 | 2026-09-29 | kurulum ve güncelleme çatısı | https://github.com/velopack/velopack [37] |
| Sparkle (çatı) | doğrulanmadı | doğrulanmadı | macOS güncelleme çatısı | https://sparkle-project.org/ [36] |

Sayım: 18 rakip ürün (HandBrake'ten FFmpeg'e), artı resmi belgesi okunamayan Tdarr ve iki güncelleme çatısı. FileFlows'un deposu bulunamadı (404).

Yayın varlıkları, son sürümde:

- HandBrake: Windows arayüz kurucusu ve zip, x86_64 ve arm64; `HandBrakeCLI` ayrı zip, iki mimari; macOS dmg; x86_64 flatpak [2].
- OBS Studio: macOS Apple ve Intel dmg; Ubuntu 24.04 ve 26.04 deb; Windows x64 kurucu ve zip; Windows arm64 zip [5].
- mpv: Windows x86_64, i686, aarch64 zip; macOS 14, 15, 26 arm ve 15 intel zip [8].
- ShareX: `setup-x64.exe`, `portable-x64.zip` [12].
- LosslessCut: Linux x64, arm64, armv7l tar.bz2 ve x86_64 AppImage; macOS arm64 ve x64 dmg; mağazalar için macOS pkg ve Windows appx; Windows x64 ve arm64 7z [14].
- Shotcut: Linux AppImage ve txz; macOS dmg; Windows x64 ve ARM kurucu ile zip [21].
- File Converter: `x64-setup.msi` [22].
- PowerToys: kullanıcı başına ve makine geneli kurucu, x64 ve arm64; `GroupPolicyObjectFiles` zip [23].
- ScreenToGif: taşınabilir zip, msi, msix; x64, x86, Arm64 [27].
- IINA: dmg [26]. StaxRip: x64 7z [61]. FFmpeg Batch AV Converter: 64 bit kurucu ve taşınabilir zip [62].
- Shutter Encoder, Unmanic, Constrict: GitHub yayınında dosya yok; sırasıyla kendi sitesi, Docker (`josh5/unmanic`) ve Flathub [20][40][63].

## Özellik Matrisi

Gösterim: `var` kaynakla görüldü; `?` doğrulanmadı; `—` o platformda anlamsız.

### Dağıtım Kanalları

| Ürün | winget | Scoop | Flathub | Homebrew cask | Mağaza | dmg | AppImage ya da deb | Taşınabilir | Windows ARM64 |
|---|---|---|---|---|---|---|---|---|---|
| VidShrink | yok | yok | yok | yok | yok | yok | yok | yarım (zip) | var |
| HandBrake | var | var | var | var | ? | var | ? (istek açık, 18 oy) | var (zip) | var |
| OBS Studio | var | var | ? (depo arşivli) | var | ? | var | var (deb) | var (`--portable`) | var (zip) |
| VLC | var | var | var | var | ? | ? | ? | ? | ? |
| mpv | var | var | var | var | ? | ? (zip var) | ? | var (zip) | var |
| ShareX | var | var | — | — | ? | — | — | var | ? (yayında yalnız x64) |
| LosslessCut | ? | var | var (resmi değil) | var | var (Microsoft, Mac, Snap) | var | var (AppImage) | yarım (kendi belgesi "taşınabilir değil" diyor) [64] | var |
| Shutter Encoder | var | var | ? (istek açık) | var | ? | ? | ? | var | ? (istek açık) |
| Shotcut | var | var | var | var | ? | var | var (AppImage) | var (zip) | var |
| Kdenlive | var | var | var | var | ? | ? | ? | ? | ? |
| File Converter | var | Extras'ta bulunmadı | — | — | ? | — | — | ? (istek açık) | ? (yayında yalnız x64) |
| PowerToys | var | var | — | — | var | — | — | ? | var |
| ScreenToGif | var | var | — | — | ? (msix var) | — | — | var | var |
| IINA | — | — | — | var | ? (istek açık, 30 oy) | var | — | — | — |
| StaxRip | ? | var | — | — | ? | — | — | var (7z) | ? (yayında yalnız x64) |
| FFmpeg | var | var (Main) | — | var (çekirdek) | ? | — | ? | var | ? |
| Constrict | — | — | var | — | — | — | ? | — | — |

Sayılar: winget'te 13 rakip (14 paket; mpv iki paketle), Scoop'ta 13 (12'si Extras, FFmpeg Main'de), Flathub'da 7, Homebrew cask'ta 9 [28][29][30][31].

Taşınabilirlik notu: LosslessCut kendi kurulum belgesinde "taşınabilir uygulama değil" diyor; yine de Windows'ta exe'nin yanına konan `config.json` dosyasını ve `--config-dir` seçeneğini okuyor [16][64]. Bu yüzden DAG-10'da sayılmadı; oradaki beş rakip HandBrake, ShareX, OBS Studio, ScreenToGif ve Shutter Encoder.

### Otomasyon Ve Bütünleşme

| Ürün | Komut Satırı | Makinece Okunur Çıktı | İzleme Klasörü | Denetim Arabirimi | Betik Ya Da Eklenti | Başsız Ya Da Docker | Sağ Tık Menüsü |
|---|---|---|---|---|---|---|---|
| VidShrink | var: `kucult`, `plan`, `izle` | yarım: sonuç JSON ve NDJSON, ilerleme metin | var (komut satırı ve arayüz) | yok (iç boru) | yok | başsız var, Docker yok | Windows'ta var; Windows 11 ilk menüsünde yalnız açma |
| HandBrake | var: `HandBrakeCLI` [4] | var: `--json` [4] | ? (istek kapatılmış) | ? | ? | topluluk görüntüsü [41] | ? |
| FFmpeg | kendisi | var: `-progress` [38] | — | — | — | ? | — |
| OBS Studio | başlatma parametreleri [6] | — | — | var: WebSocket, 28'den beri yerleşik [7][42] | var: betik ve eklenti [43] | ? | — |
| mpv | var | var: JSON IPC [9] | — | var: `--input-ipc-server` [9] | var: Lua ve JavaScript [10] | var: libmpv [10] | ? |
| VLC | var [39] | ? | ? | var: Lua HTTP, soket, telnet [39] | var: Lua arayüzü [39] | ? | ? |
| LosslessCut | var: `--settings-json`, `--keyboard-action` [16] | ? | ? | var: HTTP API, deneysel [17] | ? | ? | ? |
| ShareX | var: `-workflow`, `-task`, kısayol eylemleri [13] | ? | ? | ? | ? | — | ? (Windows 11 isteği açık) |
| Shutter Encoder | belgede bulunmadı [19] | ? | var [19] | ? | ? | ? | ? |
| Unmanic | — | ? | var: izleyici ve zamanlayıcı [40] | web arayüzü [40] | ? | var: Docker [40] | — |
| File Converter | ? | ? | ? | ? | ? | — | var: ana işi [22] |
| PowerToys (Image Resizer) | var: `PowerToys.ImageResizerCLI.exe` [25] | ? | — | ? | — | — | var [25] |
| IINA | var (istek kaydından) [53] | ? | — | ? (OSA isteği açık, 69 oy) | var: eklenti arabirimi, yapım aşamasında [44] | — | ? |

Okuma notları:

- OBS, kaydı komut satırından başlatıyor: `--startrecording`, `--startreplaybuffer`, `--minimize-to-tray` [6]. ShareX kısayol eylemlerini argüman olarak alıyor, örnek `-VideoConverter` [13].
- mpv dört kabuk için tamamlama dosyası taşıyor: zsh, bash, fish, PowerShell [11].
- LosslessCut HTTP API yalnız `127.0.0.1` dinler, kimlik doğrulaması yoktur; mpv IPC belgesi de "no authentication, no encryption" der [9][17]. DAG-35'in `sahip` olmasının nedeni bu.
- HandBrake komut satırı arayüzün kuyruk dosyasını koşar (`--queue-import-file`) ve ön ayarı dışa verir (`--preset-export`) [4].
- PowerToys kurucusu `/quiet`, `/silent`, `/passive`, `/log` alır; ShareX kurucusu `/SILENT`, `/VERYSILENT`, `/NORUN` [13][24].

### Öncelik 1 Satırlarının İki Kaynağı

- **DAG-01 winget.** Birinci kaynak winget-pkgs dizinleri [28]. İkinci kaynak: PowerToys kurulum belgesindeki `winget install` komutu [24] ve rakiplerin kendi iş takipçileri: HandBrake #6460, ShareX #7964, Shutter Encoder #335 ve #307 winget paketini konu ediyor [48][50][51]. Oy: mpv #7751, 8 [54].
- **DAG-02 Windows imzası.** Mağaza ve msix paketi imzasız kurulmaz. LosslessCut: README'de Microsoft Store [15] ve yayında appx [14]. PowerToys: kurulum belgesinde Microsoft Store [24]. ScreenToGif: yayında msix [27]. Mağaza belgesi imzayı şart koşuyor ve MSIX'i Microsoft'un imzaladığını yazıyor [33]. Rakiplerin düz exe kurucularının imzası ayrıca doğrulanmadı.
- **DAG-04 Flathub.** Birinci kaynak Flathub depoları [31]. İkinci kaynak: HandBrake indirme sayfası [3], LosslessCut README [15]. Oy: Shutter Encoder #98, 7 [51].
- **DAG-35 denetim arabirimi.** mpv: `ipc.rst` [9] ve belge dizini [10]. OBS: uzaktan denetim kılavuzu [7] ve obs-websocket deposu [42]. LosslessCut: `cli.md` [16] ve `api.md` [17]. VLC: komut satırı yardımı [39]. Oy: IINA #316, 69 [53].

## Kullanıcı Talepleri

Oy, konunun ilk iletisindeki +1 sayısıdır. Adres kalıbı `https://github.com/<depo>/issues/<no>`.

| İstek | Depo Ve No | Oy | Durum | Kaynak |
|---|---|---|---|---|
| OSA betik desteği (AppleScript ve JavaScript ile dışarıdan sürme) | iina/iina #316 | 69 | açık | [53] |
| Linux desteği | ShareX/ShareX #6625 | 68 | açık | [50] |
| Mac App Store sürümü | iina/iina #2152 | 30 | açık | [53] |
| Toplu işleme | mifi/lossless-cut #89 | 20 | açık | [52] |
| Başlatma zamanlayıcısı | HandBrake/HandBrake #586 | 19 | açık | [48] |
| AppImage | HandBrake/HandBrake #1722 | 18 | açık, 17 yorum | [48] |
| Windows 11 uyumu (sağ tık menüsü) | Tichau/FileConverter #211 | 17 | açık, 12 yorum | [49] |
| Arayüzde eşzamanlı kodlama (bizde K6 ile yapıldı) | HandBrake/HandBrake #1445 | 15 | açık | [48] |
| Windows için ARM64 derlemesi | mifi/lossless-cut #1475 | 11 | kapalı | [52] |
| Windows 11 sağ tık menüsü desteği | Tichau/FileConverter #287 | 10 | kapalı; içeriği okunmadı | [49] |
| Otomatik güncelleme | mifi/lossless-cut #5 | 9 | kapalı | [52] |
| winget paketi | mpv-player/mpv #7751 | 8 | kapalı | [54] |
| Flatpak çalışma zamanı sorunları | obsproject/obs-studio #10409, #9542 | 8 ve 8 | kapalı | [55] |
| Kitaplık olarak kullanım | NickeManarin/ScreenToGif #33 | 8 | açık | [56] |
| Komut satırı yeni örnek açıyor | iina/iina #3459 | 8 | açık | [53] |
| Flatpak sürümü | paulpacifico/shutter-encoder #98 | 7 | kapalı, 14 yorum | [51] |
| Menüden kaybolma | Tichau/FileConverter #653 | 5 | açık | [49] |
| Windows 11 menüsü | ShareX/ShareX #5872 | 4 | açık, 17 yorum | [50] |
| Taşınabilir sürüm | Tichau/FileConverter #121 | 4 | açık | [49] |
| winget yükseltme hatası | ShareX/ShareX #7964 | 4 | kapalı | [50] |
| Arayüz komut satırını göstersin | HandBrake/HandBrake #408 | 4 | kapalı | [48] |
| Chocolatey paketi | paulpacifico/shutter-encoder #149 | 3 | açık | [51] |
| winget'e ekleme | mifi/lossless-cut #1279 | 3 | kapalı | [52] |
| Windows 11 menü girdisi | Tichau/FileConverter #598 | 3 | açık | [49] |
| Taşınabilir sürüm | HandBrake/HandBrake #267 | 3 | kapalı | [48] |
| "Birlikte aç" adı | mifi/lossless-cut #1014 | 3 | açık | [52] |
| Snap paketi | HandBrake/HandBrake #1702 | 2 | açık | [48] |
| Sağ tıkta dönüştürme | ShareX/ShareX #5233 | 2 | açık | [50] |
| Microsoft Store ya da MSIX | HandBrake/HandBrake #2736 | 1 | kapalı | [48] |
| Windows Arm64 derlemesi | paulpacifico/shutter-encoder #414 | 1 | açık, 2026-06-06 | [51] |
| winget ve otomatik güncelleme | paulpacifico/shutter-encoder #244 | 1 | açık | [51] |
| macOS Kestirmeler | iina/iina #4829 | 1 | açık | [53] |
| Komut satırının JSON çıktısı JSON değil | HandBrake/HandBrake #4377 | 1 | açık | [48] |
| Videoyu GIF'e çevirme | microsoft/PowerToys #42327 | 1 | açık, 2025-10-13 | [57] |
| Dışarıdan denetim, API, komut satırı | mifi/lossless-cut #980 | kaydedilmedi | açık, 6 yorum | [52] |
| İzleme klasörü | HandBrake/HandBrake #4415 | 0 | kapalı | [48] |

Okuma: en çok oyu dışarıdan sürme (IINA 69) ve platform kapsamı (ShareX Linux 68) alıyor. Paket kanalı istekleri az oy topluyor ama her depoda tekrar ediyor: winget beş depoda, Flatpak üç depoda. Chocolatey paketi olan LosslessCut'ta paket sağlaması bozulunca kayıt açılmış (#1012, kapalı) [52]: topluluk paketinin bakım yükü var.

## Son İki Yılın Yenilikleri

- **Homebrew 5.0.0, 12 Kasım 2025.** İmzasız cask kullanımdan kalkıyor; Gatekeeper denetiminden geçmeyen cask'lar Eylül 2026'da kapatılacak; `--no-quarantine` bayrağı kaldırılıyor [32]. Sonuç: Homebrew yolu artık noterleme ister (DAG-07, DAG-05'e bağlı).
- **Microsoft Store, 10 Eylül 2025.** Bireysel geliştiriciye kayıt ücretsiz oldu [34]. Exe ve msi gönderimi imzalı, sessiz kurulabilen ve indirici olmayan kurucu ister; MSIX yolunda imzayı ve barındırmayı Microsoft üstlenir (belge güncellemesi 24 Ağustos 2026) [33]. Sonuç: bugünkü kurucumuz exe yoluna giremez; MSIX yolu sertifikasız imza demektir.
- **Azure Artifact Signing** (eski adı Trusted Signing; bulutta sertifika kiralama). Bireysel geliştiriciye yalnız ABD ve Kanada'da açık, aylık 9,99 dolar; haber 14 Ocak 2026, üçüncü taraf [47]. Türkiye'deki bireysel geliştirici için yol görünmüyor.
- **HandBrake 1.10.0 (9 Ağustos 2025) ile 1.11.2 (7 Haziran 2026) arası.** 1.11.0 arayüzü .NET Desktop Runtime 10 ister; Windows ARM64 kurucusu ve zip'i düzenli yayımlanıyor; 1.10.1 "arm64 build" düzeltmesi [2].
- **OBS Studio 32.x.** Windows arm64 zip ve Ubuntu 26.04 deb; 32.2.x macOS 12 desteğini bıraktı; 32.1.2 (21 Nisan 2026) obs-websocket'e Canvas desteği ekledi [5]. WebSocket'in yerleşik gelmesi OBS 28'den beri; iki yıldan eski bir yenilik [7].
- **LosslessCut.** Windows arm64 7z yayımlanıyor (istek #1475, 11 oy, kapalı) [14][52]. HTTP API hâlâ "deneysel" [17]. v3.66 macOS 11'i bıraktı [18].
- **PowerToys.** Kullanıcı başına ve makine geneli iki kurucu, ARM64, grup ilkesi dosyaları, `winget configure` ile bildirimli kurulum [23][24]. Image Resizer'a komut satırı ikilisi geldi (belge 25 Ağustos 2026) [25].
- **Velopack 1.2.161, 29 Eylül 2026.** Tek komutla kurucu, güncelleme, fark paketi ve kendini güncelleyen taşınabilir paket; Windows, macOS, Linux; Squirrel'den göç [37]. Sparkle: fark güncellemesi ve EdDSA imzası, macOS 12 ve üstü [36].
- **mpv v0.41.0, 21 Aralık 2025.** macOS 14, 15 ve 26 için ayrı zip; Windows aarch64 [8].
- **Diğer.** ShareX v21.0.0 (3 Temmuz 2026) [12]; IINA v1.5.0 (3 Ekim 2026) ve eklenti arabirimi tanımları (son itme 28 Haziran 2026) [26][44]; ScreenToGif üç mimaride msix, msi ve zip [27]; Shutter Encoder'da Flatpak isteği 29 Eylül 2026'da yeniden açıldı [51].

## Doğrulanamayanlar

- Rakiplerin düz Windows kurucularının Authenticode imzası ve macOS noterlemesi. HandBrake için bulunan tek kayıt eski belge: 1.1.0'dan beri Apple geliştirici sertifikasıyla imzalı [45]. SignPath proje listesinde sorulan rakiplerden hiçbiri yok [46].
- OBS Studio'nun Flathub'daki varlığı: `com.obsproject.Studio` deposu arşivli görünüyor.
- LosslessCut ve StaxRip'in winget paketi: tahmin edilen yollar 404 verdi; LosslessCut'ta istek (#1279) kapalı.
- Chocolatey ve AUR dizinlerine bakılmadı. DAG-12'nin dayanağı: PowerToys belgesi Chocolatey'i topluluk yolu sayıyor [24]; LosslessCut'ta Chocolatey paketi için hata kaydı var (#1012) [52].
- Velopack ve Sparkle'da kanal ve geri alma; bu çatıları hangi rakibin kullandığı.
- winget'in sessiz kurulum ve yüklü uygulamalar kaydı şartı: gönderim sayfası söylemiyor [58].
- Tdarr ve FileFlows resmi belgeleri okunamadı (FileFlows deposu 404, Tdarr'ın yayını 404).
- Rakiplerin ffmpeg'i paketin içinde getirdiği (DAG-20): Shutter Encoder ve LosslessCut ffmpeg üstüne kurulu, ama paket içeriği ayrıca doğrulanmadı.
- macOS Finder eylemi ve Linux dosya yöneticisi eylemi sunan rakip (DAG-41, DAG-42): rakip belgesi bulunamadı. İkisi de `doğrulanmadı` olarak tabloda.
- URL protokolü (`vidshrink://` gibi), Windows "Gönder" menüsü ve paylaşım hedefi: bizde yok (arama kaydı yukarıda), ama kullanan rakip de belgelenemedi. Tabloya girmedi.
- Stream Deck'in resmi OBS eklentisi: OBS kılavuzu Stream Deck'i anmıyor, başka tuş takımı araçlarını sayıyor [7].
- VLC'de `--intf`, `--extraintf` ve tek örnek seçeneği: sayfanın yalnız ilk üçte biri okunabildi [39].
- Windows 11 ilk menüsünde alt menü sunan rakip (DAG-37): Microsoft belgesi yolu anlatıyor [35], PowerToys Image Resizer sayfası ilk menüyü ayrıca anmıyor [25]. Kimlik paketinin imzalı olması gerektiği yalnız üçüncü taraf kaynakta [59].
- `StagePackageAsync` her güncellemede bütün paketi mi indiriyor: `UpdateStaging.cs:387` öyle gösteriyor, gövdenin tamamı okunmadı. `docs/kurulum.md:108-109` bununla çelişiyor.
- Homebrew'in Eylül 2026 kapatmasının uygulanıp uygulanmadığı.
- File Converter #287'nin neyi tamamladığı ve HandBrake #4415'in kapanış gerekçesi.
- VLC, Kdenlive ve FFmpeg sürüm tarihleri; IINA'nın URL şeması; macOS'un kendi "video kodla" hizmeti.

## Kaynaklar

- [1] https://github.com/Teknesyum/VidShrink/releases/tag/v1.2.15 — bizim yayın varlıklarımız ve boyutları (`gh release view`).
- [2] https://github.com/HandBrake/HandBrake/releases — HandBrake 1.11.2 varlıkları, 1.10.0-1.11.2 sürüm notları.
- [3] https://handbrake.fr/downloads.php — HandBrake kanalları, Flatpak, zip, OpenPGP imzası, macOS alt sürümü.
- [4] https://handbrake.fr/docs/en/latest/cli/command-line-reference.html — HandBrakeCLI seçenekleri.
- [5] https://github.com/obsproject/obs-studio/releases — OBS 32.2.2 varlıkları ve 32.x sürüm notları.
- [6] https://obsproject.com/kb/launch-parameters — OBS başlatma parametreleri.
- [7] https://obsproject.com/kb/remote-control-guide — OBS WebSocket ve istemci listesi.
- [8] https://github.com/mpv-player/mpv/releases — mpv v0.41.0 varlıkları.
- [9] https://github.com/mpv-player/mpv/blob/master/DOCS/man/ipc.rst — mpv JSON IPC.
- [10] https://github.com/mpv-player/mpv/tree/master/DOCS/man — mpv Lua, JavaScript ve libmpv belgeleri.
- [11] https://github.com/mpv-player/mpv/tree/master/etc — kabuk tamamlama dosyaları, `mpv.desktop`.
- [12] https://github.com/ShareX/ShareX/releases — ShareX v21.0.0 varlıkları.
- [13] https://getsharex.com/docs/command-line-arguments — ShareX argümanları, kurucu bayrakları, kayıt defteri ilkesi.
- [14] https://github.com/mifi/lossless-cut/releases — LosslessCut v3.69.0 varlıkları.
- [15] https://github.com/mifi/lossless-cut#download — LosslessCut kanalları: mağazalar, Snap, gecelik derleme, Flathub notu.
- [16] https://github.com/mifi/lossless-cut/blob/master/docs/cli.md — LosslessCut komut satırı.
- [17] https://github.com/mifi/lossless-cut/blob/master/docs/api.md — LosslessCut HTTP API.
- [18] https://github.com/mifi/lossless-cut/blob/master/docs/requirements.md — LosslessCut sistem gereksinimi.
- [19] https://www.shutterencoder.com/documentation/ — Shutter Encoder izleme klasörü ve güncellemeyi kapatma.
- [20] https://github.com/paulpacifico/shutter-encoder — Shutter Encoder README (kurucu ve taşınabilir sürümler) ve sürüm 20.4.
- [21] https://github.com/mltframework/shotcut/releases — Shotcut v26.9.27 varlıkları.
- [22] https://github.com/Tichau/FileConverter — File Converter README ve v2.2 (`x64-setup.msi`).
- [23] https://github.com/microsoft/PowerToys/releases — PowerToys kurucuları ve grup ilkesi dosyaları.
- [24] https://learn.microsoft.com/en-us/windows/powertoys/install — PowerToys kanalları, sessiz kurulum, temizlik betikleri.
- [25] https://learn.microsoft.com/en-us/windows/powertoys/image-resizer — Image Resizer sağ tık girdisi ve komut satırı.
- [26] https://github.com/iina/iina/releases — IINA v1.5.0.
- [27] https://github.com/NickeManarin/ScreenToGif/releases — ScreenToGif 2.43.2 varlıkları.
- [28] https://github.com/microsoft/winget-pkgs/tree/master/manifests — winget paket dizinleri (rakipler ve bizim yokluğumuz).
- [29] https://github.com/ScoopInstaller/Extras — Scoop Extras (FFmpeg ve handbrake-cli için https://github.com/ScoopInstaller/Main).
- [30] https://github.com/Homebrew/homebrew-cask — Homebrew cask dizini.
- [31] https://github.com/flathub — Flathub uygulama depoları.
- [32] https://brew.sh/2025/11/12/homebrew-5.0.0/ — imzasız cask'ların kapatılması.
- [33] https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msi/app-package-requirements — Microsoft Store exe ve msi şartları.
- [34] https://blogs.windows.com/windowsdeveloper/2025/09/10/free-developer-registration-for-individual-developers-on-microsoft-store/ — ücretsiz bireysel kayıt.
- [35] https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer — Windows 11 menüsüne girmenin yolu.
- [36] https://sparkle-project.org/ — Sparkle özellikleri.
- [37] https://github.com/velopack/velopack — Velopack README ve 1.2.161.
- [38] https://ffmpeg.org/ffmpeg.html — `-progress`, `-stats_period`, `-nostdin`.
- [39] https://wiki.videolan.org/VLC_command-line_help/ — VLC Lua HTTP, soket ve telnet arayüzleri.
- [40] https://github.com/Unmanic/unmanic — Unmanic README ve 0.4.1.
- [41] https://github.com/jlesage/docker-handbrake — HandBrake'in topluluk Docker görüntüsü (resmi değil).
- [42] https://github.com/obsproject/obs-websocket — obs-websocket deposu.
- [43] https://github.com/obsproject/obs-studio/tree/master/docs/sphinx — OBS betik ve eklenti belgeleri.
- [44] https://github.com/iina/iina-plugin-definition — IINA eklenti arabirimi tanımları.
- [45] https://handbrake.fr/docs/en/1.3.0/help/troubleshooting-common-issues.html — HandBrake macOS imzası (eski belge).
- [46] https://signpath.org/projects — SignPath Foundation proje listesi.
- [47] https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options — Windows imza seçenekleri, Azure Artifact Signing.
- [48] https://github.com/HandBrake/HandBrake/issues — HandBrake istekleri (#1722, #586, #1445, #1702, #267, #2736, #4415, #408, #4377, #6460).
- [49] https://github.com/Tichau/FileConverter/issues — File Converter istekleri (#211, #287, #598, #121, #653).
- [50] https://github.com/ShareX/ShareX/issues — ShareX istekleri (#6625, #5872, #5233, #4923, #7964).
- [51] https://github.com/paulpacifico/shutter-encoder/issues — Shutter Encoder istekleri (#98, #149, #414, #244, #335, #307, #461).
- [52] https://github.com/mifi/lossless-cut/issues — LosslessCut istekleri (#1475, #5, #1279, #2202, #240, #645, #980, #1014, #1012, #89).
- [53] https://github.com/iina/iina/issues — IINA istekleri (#316, #2152, #3459, #4829).
- [54] https://github.com/mpv-player/mpv/issues/7751 — mpv winget paketi isteği.
- [55] https://github.com/obsproject/obs-studio/issues — OBS Flatpak kayıtları (#10409, #9542, #11478, #12893, #6004).
- [56] https://github.com/NickeManarin/ScreenToGif/issues/33 — ScreenToGif kitaplık isteği.
- [57] https://github.com/microsoft/PowerToys/issues — PowerToys video istekleri (#42327, #27248).
- [58] https://learn.microsoft.com/en-us/windows/package-manager/package/ — winget'e paket gönderme.
- [59] https://www.advancedinstaller.com/adding-items-to-windows-11-context-menu.html — kimlik paketinin imza şartı (üçüncü taraf).
- [60] https://github.com/HaveAGitGat/Tdarr — Tdarr depo etkinliği.
- [61] https://github.com/staxrip/staxrip — StaxRip v2.52.6.
- [62] https://github.com/eibols/ffmpeg_batch — FFmpeg Batch AV Converter 3.2.9.
- [63] https://github.com/Wartybix/Constrict — Constrict 25.12.1.
- [64] https://github.com/mifi/lossless-cut/blob/master/docs/installation.md — LosslessCut ayar konumu, "taşınabilir uygulama değil" açıklaması, mağaza sürümleri.

## Komşu Kola Not

- Güncelleme kanalı (DAG-24) ve taşınabilir kip (DAG-10) yapılırsa Ayarlar ekranına birer seçenek düşer; bugün orada yalnız otomatik güncelleme anahtarı var (`docs/kurulum.md:151-153`) ve sağ tık menüsünün dili kurulumda `--menu-language` ile sabitleniyor (`src/VidShrink.Setup/Program.cs:89-104`).
