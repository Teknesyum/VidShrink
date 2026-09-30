using System;
using System.IO;
using System.Runtime;
using System.Threading.Tasks;
using VidShrink.Core;

namespace VidShrink.App;

internal static class JitProfili
{
    internal const string DosyaAdi = "acilis.jitprofile";

    internal static readonly TimeSpan YedekBekleme = TimeSpan.FromSeconds(10);

    internal static readonly TimeSpan YazmaGecikmesi = TimeSpan.FromSeconds(3);

    internal static string? Klasor(string? ayarYolu, string yerelKlasor)
    {
        if (!string.IsNullOrWhiteSpace(ayarYolu)) return Path.GetDirectoryName(ayarYolu);
        return string.IsNullOrEmpty(yerelKlasor) ? null : Path.Combine(yerelKlasor, UpdateSettings.FolderName);
    }

    internal static void Baslat(Task acilis)
    {
        try
        {
            var klasor = Klasor(
                Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH"),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            if (klasor is null) return;
            Directory.CreateDirectory(klasor);
            ProfileOptimization.SetProfileRoot(klasor);
            ProfileOptimization.StartProfile(DosyaAdi);
            _ = DurdurAsync(acilis, YedekBekleme, YazmaGecikmesi, () => ProfileOptimization.StartProfile(null));
        }
        catch (Exception)
        {
        }
    }

    internal static async Task<bool> DurdurAsync(Task acilis, TimeSpan yedek, TimeSpan gecikme, Action durdur)
    {
        await Task.WhenAny(acilis, Task.Delay(yedek)).ConfigureAwait(false);
        await Task.Delay(gecikme).ConfigureAwait(false);
        try
        {
            durdur();
            AcilisIzi.Yaz("jit-profili-yazildi");
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
