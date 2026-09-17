using System.Globalization;
using VidShrink.Core;
using VidShrink.Launcher;

namespace VidShrink.SahteUygulama;

internal static class Program
{
    internal const string IsaretDegiskeni = "VIDSHRINK_SAHTE_ISARET";
    internal const string OmurDegiskeni = "VIDSHRINK_SAHTE_OMUR_MS";
    internal const string KilitDosyasi = "kilit.dll";

    [STAThread]
    private static int Main(string[] args)
    {
        if (UygulamaKlasoruKapisi.BaslaticiyaDevret(AppContext.BaseDirectory, args))
        {
            Yaz("devretti", "");
            return 0;
        }

        var klasor = AppContext.BaseDirectory;
        Yaz("acildi", Environment.GetEnvironmentVariable(LauncherUpdate.LaunchedVariable) ?? "");

        var kilitYolu = Path.Combine(klasor, KilitDosyasi);
        FileStream? kilit = null;
        if (File.Exists(kilitYolu))
        {
            kilit = new FileStream(kilitYolu, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var okuyucu = new StreamReader(kilit, leaveOpen: true);
            Yaz("kilit", okuyucu.ReadToEnd());
        }

        var omur = int.TryParse(Environment.GetEnvironmentVariable(OmurDegiskeni), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) ? ms : 2000;
        Thread.Sleep(omur);
        kilit?.Dispose();
        Yaz("kapandi", "");
        return 0;
    }

    private static void Yaz(string olay, string deger)
    {
        var klasor = Environment.GetEnvironmentVariable(IsaretDegiskeni);
        if (string.IsNullOrEmpty(klasor)) return;
        var ad = string.Create(CultureInfo.InvariantCulture, $"{DateTime.UtcNow.Ticks}-{Environment.ProcessId}-{olay}.txt");
        File.WriteAllText(Path.Combine(klasor, ad), deger);
    }
}
