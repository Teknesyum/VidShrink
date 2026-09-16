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
    }
}
