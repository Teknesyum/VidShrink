using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VidShrink.Core;

namespace VidShrink.App.Integration;

/// <summary>
/// VidShrink'in "Birlikte aç" listesinde görünmesini sağlayan kullanıcı kapsamlı kayıt.
/// İki parça yazılır: bir ProgID (uygulamayı çalıştıran komut) ve her medya uzantısının
/// <c>OpenWithProgids</c> listesine o ProgID'nin adı.
///
/// <para>Bir uzantının <b>varsayılanı</b> burada ayarlanmaz ve ayarlanamaz: Windows 10 ve
/// 11'de varsayılanı taşıyan anahtar imzalı bir karma tutar, elle yazılan değer işletim
/// sistemi tarafından reddedilir ve kullanıcının kendi seçimi bozulur. Bu sınıf yalnız
/// listeye girer; seçimi kullanıcı Windows'un kendi sayfasında yapar.</para>
/// </summary>
internal static class FileAssociation
{
    /// <summary>Kaydın adı. Yayıncı ve ürün adını taşır; başka bir programla çakışmaz.</summary>
    internal const string ProgId = "Teknesyum.VidShrink.Video";

    /// <summary>Kaydın yazıldığı kök; <c>HKEY_CURRENT_USER</c> altında görecelidir.</summary>
    internal const string ClassesRoot = @"Software\Classes";

    private const string DisplayName = "VidShrink";

    private static readonly IntPtr CurrentUser = new(unchecked((int)0x80000001));

    private const int KeyWrite = 0x20006;
    private const int RegSz = 1;
    private const int RegNone = 0;

    /// <summary>
    /// Yazılacak anahtar/değer üçlüleri. <c>Value</c> <c>null</c> ise değer boş yazılır —
    /// <c>OpenWithProgids</c> listesinde ad taşıyan ama içeriği olmayan satır budur.
    /// Uygulama koşmadan da sınanabilsin diye yazma işi ayrı durur.
    /// </summary>
    internal static IReadOnlyList<(string Key, string Name, string? Value)> Plan(string executablePath, string classesRoot = ClassesRoot)
    {
        var entries = new List<(string, string, string?)>
        {
            ($@"{classesRoot}\{ProgId}", "", DisplayName),
            ($@"{classesRoot}\{ProgId}", "FriendlyTypeName", DisplayName),
            ($@"{classesRoot}\{ProgId}\DefaultIcon", "", $"{executablePath},0"),
            ($@"{classesRoot}\{ProgId}\shell\open\command", "", $"\"{executablePath}\" \"%1\""),
            ($@"{classesRoot}\Applications\{Path.GetFileName(executablePath)}\shell\open\command", "", $"\"{executablePath}\" \"%1\"")
        };

        foreach (var extension in ShellIntegration.MediaExtensions)
        {
            entries.Add(($@"{classesRoot}\.{extension}\OpenWithProgids", ProgId, null));
        }

        return entries;
    }

    internal const string LauncherName = "VidShrink.exe";

    internal static string LaunchTarget(string processPath)
    {
        var directory = Path.GetDirectoryName(processPath);
        if (string.IsNullOrEmpty(directory)) return processPath;
        if (!string.Equals(Path.GetFileName(directory), "app", StringComparison.OrdinalIgnoreCase)) return processPath;

        var root = Path.GetDirectoryName(directory);
        if (string.IsNullOrEmpty(root)) return processPath;

        var launcher = Path.Combine(root, LauncherName);
        return File.Exists(launcher) ? launcher : processPath;
    }

    /// <summary>
    /// <see cref="Plan"/>'ı <c>HKEY_CURRENT_USER</c> altına yazar ve kabuğa haber verir.
    /// Yönetici hakkı istemez. Yazılamayan satırın anahtarı döner; hepsi yazıldıysa liste boştur.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static IReadOnlyList<string> Register(string executablePath, string classesRoot = ClassesRoot)
    {
        var failed = new List<string>();
        foreach (var (key, name, value) in Plan(executablePath, classesRoot))
        {
            if (!Write(key, name, value)) failed.Add($"{key}|{name}");
        }

        if (failed.Count == 0) SHChangeNotify(0x08000000, 0x1000, IntPtr.Zero, IntPtr.Zero);
        return failed;
    }

    [SupportedOSPlatform("windows")]
    private static bool Write(string key, string name, string? value)
    {
        if (RegCreateKeyExW(CurrentUser, key, 0, null, 0, KeyWrite, IntPtr.Zero, out var handle, out _) != 0)
        {
            return false;
        }

        try
        {
            if (value is null) return RegSetValueExW(handle, name, 0, RegNone, Array.Empty<byte>(), 0) == 0;
            var bytes = System.Text.Encoding.Unicode.GetBytes(value + "\0");
            return RegSetValueExW(handle, name, 0, RegSz, bytes, bytes.Length) == 0;
        }
        finally
        {
            RegCloseKey(handle);
        }
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegCreateKeyExW(IntPtr key, string subKey, int reserved, string? className,
        int options, int access, IntPtr security, out IntPtr result, out int disposition);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegSetValueExW(IntPtr key, string name, int reserved, int type, byte[] data, int size);

    [DllImport("advapi32.dll")]
    private static extern int RegCloseKey(IntPtr key);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr first, IntPtr second);
}
