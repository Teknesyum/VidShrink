using System.Globalization;

namespace VidShrink.Core.Setup;

/// <summary>
/// Kurucunun konuştuğu tek yüzey. Uygulama kırk iki dil taşıyor ama kurucu onun çeviri
/// katmanını göremiyor: <c>VidShrink-Setup.exe</c> kırpılmış, tek dosya, kendi kendine
/// yeten bir ikili; <c>VidShrink.App</c>'e referansı yok ve <c>InvariantGlobalization</c>
/// bilerek açık. Üstelik kurucu yayın paketini indirip açmadan <b>önce</b> de konuşuyor —
/// o anda diskte hiç <c>Locales</c> klasörü yok.
///
/// <para>Bu yüzden tablo gömülü ve iki dilli. Kırk iki dil gömmek reddedildi: birkaç
/// saniye görünen bir konsol için iki binden çok cümle, kırpılmış ikiliye ayrıştırıcı
/// yükü ve doğrulanamayan çeviri. Yalnız İngilizce de reddedildi: sağ tık etiketi bile
/// kullanıcının dilinde yazılırken kurucunun kırmızı hatası tek İngilizce yüzey
/// kalırdı.</para>
///
/// <para>Dil seçimi yeni kural istemiyor:
/// <see cref="ShellRegistration.ResolveLanguage(string, Func{string}, string?)"/> klasör
/// verilmediğinde zaten yalnız <c>tr</c> ve <c>en</c> tanıyor.</para>
/// </summary>
public static class SetupText
{
    public const string FallbackLanguage = "en";

    private static readonly object Gate = new();

    private static string current = FallbackLanguage;

    /// <summary>
    /// Anahtar → (Türkçe, İngilizce). Yer tutucular iki dilde aynı olmak zorunda;
    /// <c>KurucuDiliTests</c> bunu sayıyor.
    /// </summary>
    private static readonly Dictionary<string, (string Tr, string En)> Table = new(StringComparer.Ordinal)
    {
        ["setup.preparing"] = (
            "VidShrink kurulumu hazırlanıyor...",
            "Preparing the VidShrink installation..."),
        ["setup.arch.unsupported"] = (
            "Bu mimari için yayın yok: {0}. VidShrink Windows'ta şu an yalnız win-x64 ve win-arm64 için yayımlanıyor.",
            "No release for this architecture: {0}. On Windows, VidShrink currently ships only for win-x64 and win-arm64."),
        ["setup.arch.32bit"] = (
            "Mimari okunamadı ve işletim sistemi 32 bit görünüyor: win-x64 yayını bu makinede çalışmaz. VidShrink Windows'ta şu an yalnız win-x64 ve win-arm64 için yayımlanıyor.",
            "The architecture could not be read and the operating system looks 32-bit: the win-x64 release will not run on this machine. On Windows, VidShrink currently ships only for win-x64 and win-arm64."),
        ["setup.arch.assumed-x64"] = (
            "Mimari okunamadı; işletim sistemi 64 bit olduğu için x64 varsayıldı.",
            "The architecture could not be read; x64 was assumed because the operating system is 64-bit."),
        ["setup.arch.assumed-x86"] = (
            "Mimari okunamadı; işletim sistemi 32 bit olduğu için x86 kabul edildi.",
            "The architecture could not be read; x86 was assumed because the operating system is 32-bit."),
        ["setup.root.outside-programs"] = (
            @"Güvenlik nedeniyle kurulum yolu LocalAppData\Programs altında olmalıdır: {0}",
            @"For safety the install path must be under LocalAppData\Programs: {0}"),
        ["setup.ffmpeg.downloading"] = (
            "FFmpeg ve FFprobe indiriliyor...",
            "Downloading FFmpeg and FFprobe..."),
        ["setup.release.searching"] = (
            "Son yayın aranıyor...",
            "Looking for the latest release..."),
        ["setup.version.installing"] = (
            "Kurulacak sürüm: {0}",
            "Version to install: {0}"),
        ["setup.package.downloading"] = (
            "Yayın paketi indiriliyor...",
            "Downloading the release package..."),
        ["setup.downloads.verified"] = (
            "İndirilenler doğrulandı.",
            "Downloads verified."),
        ["setup.libmpv.ready"] = (
            "libmpv hazır ({0}, sha256 doğrulandı).",
            "libmpv ready ({0}, sha256 verified)."),
        ["setup.libmpv.reused"] = (
            "yeniden kullanıldı",
            "reused"),
        ["setup.libmpv.downloaded"] = (
            "indirildi",
            "downloaded"),
        ["setup.launcher.missing"] = (
            "Kurulan VidShrink.exe bulunamadı.",
            "The installed VidShrink.exe was not found."),
        ["setup.app.missing"] = (
            @"Kurulan app\VidShrink.App.exe bulunamadı.",
            @"The installed app\VidShrink.App.exe was not found."),
        ["setup.menu.modern-and-classic"] = (
            "Windows 11 birincil ve klasik",
            "Windows 11 primary and classic"),
        ["setup.menu.classic"] = (
            "Windows 10 klasik",
            "Windows 10 classic"),
        ["setup.menu.written"] = (
            "Sağ tık menüsü {0} uzantıya, küçültme alt menüsü {1} girdiye yazıldı ({2} menü).",
            "The context menu was written for {0} extensions and the shrink submenu for {1} entries ({2} menu)."),
        ["setup.association.written"] = (
            "Dosya ilişkilendirmesi {0} uzantıya yazıldı.",
            "The file association was written for {0} extensions."),
        ["setup.installed"] = (
            "VidShrink {0} kuruldu: {1}",
            "VidShrink {0} installed: {1}"),
        ["setup.uninstall.menus-removed"] = (
            "Sağ tık menüsü ({0} uzantı, {1} paket) ve dosya ilişkilendirmesi ({2} uzantı) kaldırıldı.",
            "The context menu ({0} extensions, {1} package) and the file association ({2} extensions) were removed."),
        ["setup.uninstalled"] = (
            "VidShrink kaldırıldı: {0}",
            "VidShrink was removed: {0}"),
        ["setup.uninstall.outside-programs"] = (
            @"Kurulum klasörü LocalAppData\Programs altında değil, silinmedi: {0}",
            @"The install folder is not under LocalAppData\Programs, so it was not deleted: {0}"),
        ["setup.shell.no-package"] = (
            "Bu yayın Windows 11 kabuk paketini taşımıyor; klasik menü yazıldı.",
            "This release does not carry the Windows 11 shell package; the classic menu was written."),
        ["setup.shell.package-failed"] = (
            "Windows 11 birincil sağ tık menüsü eklenemedi (imzasız paket için geliştirici modu gerekiyor); klasik menü 'Daha fazla seçenek göster' altında çalışır.",
            "The Windows 11 primary context menu could not be added (an unsigned package needs developer mode); the classic menu works under 'Show more options'."),
        ["setup.lock.waiting"] = (
            "VidShrink kapanmayı bekliyor ({0}); virüs taraması sürüyorsa en çok {1} sn beklenecek...",
            "Waiting for VidShrink to close ({0}); if a virus scan is running this waits at most {1} s..."),
        ["setup.lock.still-open"] = (
            "Kurulum klasörü silinemedi: VidShrink {0} sn sonra hâlâ açık - {1}. Virüs programı dosyayı tarıyorsa taramanın bitmesini bekleyip kurucuyu yeniden çalıştırın. Klasör: {2}",
            "The install folder could not be deleted: VidShrink is still open after {0} s - {1}. If a virus scanner is reading the file, wait for the scan to finish and run the installer again. Folder: {2}"),
        ["setup.lock.retry"] = (
            "Kurulum klasörü kilitli, {0} ms sonra yeniden denenecek ({1}/{2})...",
            "The install folder is locked; retrying in {0} ms ({1}/{2})..."),
        ["setup.restore.aside-left"] = (
            "Kurulum geri alındı ama eski kurulum yerine konamadı; şu an \"{0}\" klasöründe duruyor. \"{1}\" klasörü boşaldığında bu klasörü oraya taşıyabilirsiniz.",
            "The install was rolled back but the previous install could not be put back; it is now in \"{0}\". Once \"{1}\" is free you can move that folder there."),
        ["setup.lock.failed"] = (
            "Kurulum klasörü {0} denemede ve {1} ms beklemede silinemedi: {2}. Bir dosya başka bir süreçte açık - genellikle virüs taraması ya da Gezgin önizlemesi; birkaç saniye sonra kurucuyu yeniden çalıştırın. Son hata: {3}",
            "The install folder could not be deleted in {0} attempts over {1} ms: {2}. A file is open in another process - usually a virus scan or an Explorer preview; run the installer again in a few seconds. Last error: {3}"),
        ["setup.release.tag-unreadable"] = (
            "Son yayının etiketi okunamadı: {0}",
            "The tag of the latest release could not be read: {0}"),
        ["setup.release.not-found"] = (
            "Son yayın bulunamadı (HTTP {0}).",
            "The latest release was not found (HTTP {0})."),
        ["setup.checksum.missing"] = (
            "Sağlama listesinde {0} yok; indirilen dosya doğrulanamıyor.",
            "{0} is missing from the checksum list; the downloaded file cannot be verified."),
        ["setup.checksum.mismatch"] = (
            "{0} sağlaması tutmuyor. Beklenen {1}, bulunan {2}. Kurulum durduruldu.",
            "The checksum of {0} does not match. Expected {1}, found {2}. The installation was stopped."),
        ["setup.download.failed"] = (
            "İndirilemedi (HTTP {0}): {1}",
            "Download failed (HTTP {0}): {1}"),
        ["setup.asset.missing"] = (
            "Yayın {0} bu varlığı taşımıyor: {1}.",
            "Release {0} does not carry this asset: {1}."),
        ["setup.asset.missing-launcher"] = (
            "Yayın {0} bu varlığı taşımıyor: {1}. Başlatıcısız kurulum yapılmaz; başlatıcıyı da içeren bir yayın çıkana kadar bekleyin.",
            "Release {0} does not carry this asset: {1}. There is no install without the launcher; wait for a release that also ships it."),
        ["setup.libmpv.no-tar"] = (
            "libmpv arşivini açmak için {0} gerekli; bu Windows'ta yok.",
            "Extracting the libmpv archive needs {0}; it is missing on this Windows."),
        ["setup.libmpv.source-failed"] = (
            "libmpv indirilemedi, yedek kaynak deneniyor: {0}",
            "libmpv could not be downloaded; trying a fallback source: {0}"),
        ["setup.libmpv.checksum-fallback"] = (
            "libmpv sağlaması tutmadı, yedek kaynak deneniyor: {0}",
            "The libmpv checksum did not match; trying a fallback source: {0}"),
        ["setup.libmpv.all-sources-failed"] = (
            "libmpv indirilemedi: {0}",
            "libmpv could not be downloaded: {0}"),
        ["setup.libmpv.archive-mismatch"] = (
            "libmpv arşivinin sağlaması tutmuyor. Beklenen {0}, bulunan {1}. Kurulum durduruldu.",
            "The checksum of the libmpv archive does not match. Expected {0}, found {1}. The installation was stopped."),
        ["setup.tar.start-failed"] = (
            "tar başlatılamadı.",
            "tar could not be started."),
        ["setup.libmpv.extract-failed"] = (
            "libmpv arşivi açılamadı (tar çıkış kodu {0}).",
            "The libmpv archive could not be extracted (tar exit code {0})."),
        ["setup.libmpv.not-in-archive"] = (
            "libmpv arşivinde {0} yok.",
            "{0} is not in the libmpv archive."),
        ["setup.registry.only-setup-writes"] = (
            "Gerçek kayıt köküne yalnız {0} yazar: {1}",
            "Only {0} writes to the real registry root: {1}"),
        ["setup.console.press-enter"] = (
            "Kapatmak için Enter'a basın.",
            "Press Enter to close."),
        ["setup.arg.value-expected"] = (
            "{0} bir değer bekliyor.",
            "{0} expects a value."),
        ["setup.arg.bad-menu-language"] = (
            "Geçersiz menü dili: {0}",
            "Invalid menu language: {0}"),
        ["setup.arg.unknown-option"] = (
            "Bilinmeyen seçenek: {0}. Yardım için --help.",
            "Unknown option: {0}. Use --help for help."),
        ["setup.shell.package-remove-failed"] = (
            "Windows 11 kabuk paketi kaldırılamadı.",
            "The Windows 11 shell package could not be removed."),
        ["setup.shortcut.failed"] = (
            "Kısayol yazılamadı: {0}",
            "The shortcut could not be written: {0}"),
        ["setup.powershell.start-failed"] = (
            "powershell başlatılamadı.",
            "powershell could not be started."),
        ["shell.menu.open"] = (
            "Bu Videoyu VidShrink ile Aç",
            "Open this video with VidShrink"),
        ["shell.menu.shrink"] = (
            "VidShrink ile Küçült",
            "Shrink with VidShrink"),
        ["setup.help"] = (
            """
            VidShrink-Setup - VidShrink'i kurar, günceller ya da kaldırır.

            Kullanım: VidShrink-Setup.exe [seçenekler]

              --uninstall             Kısayolları, sağ tık menüsünü, ilişkilendirmeyi ve kurulumu kaldırır.
              --install-root <yol>    Kurulum klasörü (varsayılan %LOCALAPPDATA%\Programs\VidShrink).
              --registry-root <kök>   Kayıt kökü (varsayılan HKCU:\Software\Classes).
              --menu-language <dil>   auto, tr ya da en.
              --skip-shortcuts        Kısayol, menü ve ilişkilendirme yazılmaz.
              --no-launch             Kurulumdan sonra VidShrink açılmaz.
              --shortcut-dir <klasör> Kısayollar Masaüstü ve Başlat yerine bu klasöre yazılır.
              --tag <etiket>          Son yayın yerine bu etiket kurulur.
              --asset-source <klasör> Yayın varlıkları bu klasörden okunur.
              --download-ffmpeg       Yüklü FFmpeg olsa da sabitlenmiş FFmpeg indirilir.
              --timings               Adım sürelerini yazar.
            """,
            """
            VidShrink-Setup - installs, updates or removes VidShrink.

            Usage: VidShrink-Setup.exe [options]

              --uninstall             Removes the shortcuts, context menu, association and the installation.
              --install-root <path>   Install folder (default %LOCALAPPDATA%\Programs\VidShrink).
              --registry-root <root>  Registry root (default HKCU:\Software\Classes).
              --menu-language <lang>  auto, tr or en.
              --skip-shortcuts        No shortcut, menu or association is written.
              --no-launch             VidShrink is not started after the installation.
              --shortcut-dir <folder> Shortcuts go to this folder instead of Desktop and Start.
              --tag <tag>             Installs this tag instead of the latest release.
              --asset-source <folder> Release assets are read from this folder.
              --download-ffmpeg       Downloads the pinned FFmpeg even if one is installed.
              --timings               Prints the duration of each step.
            """)
    };

    /// <summary>Tablodaki bütün anahtarlar. Ölçü bunun üstünden sayar.</summary>
    public static IReadOnlyCollection<string> Keys => Table.Keys;

    public static string Language
    {
        get { lock (Gate) return current; }
    }

    /// <summary>
    /// Konuşulacak dili kurar. <c>tr</c> dışındaki her şey <see cref="FallbackLanguage"/>
    /// sayılır; tablo iki dilli, üçüncü bir kol yok.
    /// </summary>
    public static void Use(string? language)
    {
        var wanted = string.Equals(language?.Trim(), "tr", StringComparison.OrdinalIgnoreCase)
            ? "tr"
            : FallbackLanguage;
        lock (Gate) current = wanted;
    }

    public static string Get(string key) => GetIn(Language, key);

    /// <summary>
    /// Kurulu dile bakmadan, verilen dilde okur. Sağ tık etiketi bunu kullanıyor: etiketin
    /// dili <c>--menu-language</c> ile ayrı seçilebiliyor.
    /// </summary>
    public static string GetIn(string language, string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!Table.TryGetValue(key, out var pair)) return key;
        return string.Equals(language?.Trim(), "tr", StringComparison.OrdinalIgnoreCase) ? pair.Tr : pair.En;
    }

    /// <summary>
    /// Biçimleme <see cref="CultureInfo.InvariantCulture"/> ile: kurucu
    /// <c>InvariantGlobalization</c> ile derleniyor, makinenin kültürü burada okunmaz.
    /// </summary>
    public static string Get(string key, params object?[] args) => GetIn(Language, key, args);

    public static string GetIn(string language, string key, params object?[] args)
    {
        var text = GetIn(language, key);
        if (args is null || args.Length == 0) return text;

        try
        {
            return string.Format(CultureInfo.InvariantCulture, text, args);
        }
        catch (FormatException)
        {
            return text;
        }
    }
}
