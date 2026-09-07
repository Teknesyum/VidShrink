using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace VidShrink.App.Integration;

/// <summary>
/// Bir uzantıyı bugün hangi programın açtığını okur ve kullanıcıyı Windows'un kendi
/// varsayılan uygulama sayfasına götürür.
///
/// <para>Okuma kayıt defterinden değil <c>AssocQueryString</c>'ten yapılır: kullanıcının
/// seçimini taşıyan anahtar imzalı bir karma tutar, işletim sistemi onu kendi çözer ve
/// aynı cevabı bu çağrıdan verir. Yazma tarafı hiç yoktur; bu sınıf seçime dokunmaz.</para>
/// </summary>
[SupportedOSPlatform("windows")]
internal static class DefaultApp
{
    /// <summary>Windows'un varsayılan uygulamalar sayfasının adresi.</summary>
    internal const string SettingsPage = "ms-settings:defaultapps";

    private const int Executable = 2;

    /// <summary>
    /// <paramref name="extension"/> uzantısını bugün açan programın tam yolu; cevap
    /// alınamazsa <c>null</c>. Uzantı noktasız verilir.
    /// </summary>
    internal static string? Handler(string extension)
    {
        var size = 1024u;
        var buffer = new StringBuilder((int)size);
        var result = AssocQueryStringW(0, Executable, "." + extension, null, buffer, ref size);
        return result == 0 && buffer.Length > 0 ? buffer.ToString() : null;
    }

    /// <summary>
    /// Verilen uzantıların tamamını <paramref name="executablePath"/> açıyorsa doğru.
    /// Tek bir uzantı bile başka bir programa gidiyorsa yanlış döner.
    /// </summary>
    internal static bool IsDefault(string executablePath, IReadOnlyList<string> extensions)
    {
        if (extensions.Count == 0) return false;
        foreach (var extension in extensions)
        {
            var handler = Handler(extension);
            if (handler is null) return false;
            if (!string.Equals(handler, executablePath, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    /// <summary>Ayar sayfasını açar. Açılamazsa yanlış döner, hata fırlatmaz.</summary>
    internal static bool OpenSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo(SettingsPage) { UseShellExecute = true });
            return true;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryStringW(int flags, int str, string association, string? extra,
        StringBuilder? output, ref uint size);
}
