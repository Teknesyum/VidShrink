using System.Runtime.CompilerServices;
using VidShrink.Core;

namespace VidShrink.Tests;

internal static class TestAyarYolu
{
    internal const string Degisken = "VIDSHRINK_SETTINGS_PATH";

    internal static string Klasor { get; private set; } = "";

    [ModuleInitializer]
    internal static void Bagla()
    {
        var mevcut = Environment.GetEnvironmentVariable(Degisken);
        if (!string.IsNullOrWhiteSpace(mevcut))
        {
            Klasor = Path.GetDirectoryName(Path.GetFullPath(mevcut)) ?? "";
            return;
        }

        Klasor = Path.Combine(TestPaths.OutputRoot, "appdata", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(Klasor);
        Environment.SetEnvironmentVariable(Degisken, Path.Combine(Klasor, UpdateSettings.FileName));
        OluKlasorleriBuda(Path.GetDirectoryName(Klasor)!);
    }

    /// <summary>
    /// Her koşum kendi süreç kimliğiyle bir klasör açıyor; kimse kapatmadığı için birikiyorlar.
    /// Süreci artık yaşamayan kardeş klasörler silinir, yaşayanlara dokunulmaz.
    /// </summary>
    private static void OluKlasorleriBuda(string kok)
    {
        foreach (var klasor in Directory.GetDirectories(kok))
        {
            var ad = Path.GetFileName(klasor);
            if (!int.TryParse(ad, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var pid)) continue;
            if (pid == Environment.ProcessId) continue;
            if (Yasiyor(pid)) continue;
            try { Directory.Delete(klasor, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static bool Yasiyor(int pid)
    {
        try
        {
            using var surec = System.Diagnostics.Process.GetProcessById(pid);
            return !surec.HasExited;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
}
