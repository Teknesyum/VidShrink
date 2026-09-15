using System.Diagnostics;
using System.Globalization;

namespace VidShrink.Launcher;

/// <summary>
/// Baslaticinin kendi izi. Olcunun sifir noktasi bugune kadar app surecinin dogumuydu ve
/// baslaticinin harcadigi sure hic gorunmuyordu; bu iz o payi gorunur yapiyor.
///
/// <para>Sifir noktasi bu surecin <see cref="Process.StartTime"/>'i, yani cift tikin
/// hemen ardi. Deger <see cref="SifirDegiskeni"/> ile cocuk surece gecirilir; app kendi
/// dogumunu degil bunu taban alir, boylece iki surecin satirlari ayni eksende okunur.</para>
///
/// <para>Ortam degiskeni bos oldugunda hicbir sey yapmaz: uretim yolunda dosya acilmaz.</para>
/// </summary>
internal static class AcilisIzi
{
    internal const string Degisken = "VIDSHRINK_ACILIS_IZI";

    /// <summary>Cocuk surece gecirilen sifir noktasi; UTC tick.</summary>
    internal const string SifirDegiskeni = "VIDSHRINK_ACILIS_T0";

    private static readonly DateTime Baslangic = SurecBaslangici();

    private static DateTime SurecBaslangici()
    {
        try { return Process.GetCurrentProcess().StartTime.ToUniversalTime(); }
        catch (Exception) { return DateTime.UtcNow; }
    }

    internal static bool Acik => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Degisken));

    /// <summary>Cocuk surecin tabana alacagi deger.</summary>
    internal static string SifirIsareti => Baslangic.Ticks.ToString(CultureInfo.InvariantCulture);

    internal static void Yaz(string adim)
    {
        var yol = Environment.GetEnvironmentVariable(Degisken);
        if (string.IsNullOrWhiteSpace(yol)) return;

        try
        {
            File.AppendAllText(yol, string.Create(CultureInfo.InvariantCulture,
                $"{adim}\t{(DateTime.UtcNow - Baslangic).TotalMilliseconds:0.0}{Environment.NewLine}"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
