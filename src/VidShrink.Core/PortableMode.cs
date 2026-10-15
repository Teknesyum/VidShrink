using System;
using System.IO;

namespace VidShrink.Core;

/// <summary>
/// AYR-P8 taşınabilir kip: exe'nin yanında <see cref="MarkerFileName"/> varsa ayar, günlük,
/// paylaşım defteri ve JIT profili kullanıcı profiline değil uygulama klasörünün
/// <see cref="DataFolderName"/> altına yazılır. Bu kipte sessiz güncelleme, kısayol ve kayıt
/// defteri yazımı kapalıdır; güncelleme kartı indirme sayfasını açar.
/// <c>VIDSHRINK_SETTINGS_PATH</c> işaretten önce gelir.
/// </summary>
public static class PortableMode
{
    public const string MarkerFileName = "VidShrink.portable";
    public const string DataFolderName = "data";

    public static bool IsActive(string? appDirectory, Func<string, bool>? fileExists = null)
        => !string.IsNullOrWhiteSpace(appDirectory)
           && (fileExists ?? File.Exists)(Path.Combine(appDirectory, MarkerFileName));

    /// <summary>Çalışan uygulamanın klasöründe işaret var mı.</summary>
    public static bool Active => IsActive(AppContext.BaseDirectory);

    /// <summary>
    /// Kullanıcı verisinin klasörü: taşınabilirde <c>&lt;uygulama&gt;\data</c>, değilse verilen özel
    /// klasörün altındaki <c>VidShrink</c>.
    /// </summary>
    public static string UserFolder(Environment.SpecialFolder folder, string? appDirectory)
        => IsActive(appDirectory)
            ? Path.Combine(appDirectory!, DataFolderName)
            : Path.Combine(Environment.GetFolderPath(folder), UpdateSettings.FolderName);

    public static string RoamingFolder => UserFolder(Environment.SpecialFolder.ApplicationData, AppContext.BaseDirectory);

    public static string LocalFolder => UserFolder(Environment.SpecialFolder.LocalApplicationData, AppContext.BaseDirectory);

    public static string SettingsPath(string? overridden, string? appDirectory)
        => !string.IsNullOrWhiteSpace(overridden)
            ? overridden!
            : Path.Combine(UserFolder(Environment.SpecialFolder.ApplicationData, appDirectory), UpdateSettings.FileName);
}
