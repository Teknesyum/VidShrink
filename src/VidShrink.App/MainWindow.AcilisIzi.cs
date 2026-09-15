using System;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace VidShrink.App;

/// <summary>
/// Cift tiktan ilk kareye kadar gecen sureyi olcen iz. Ortam degiskeni bos oldugunda
/// hicbir sey yapmaz: <see cref="Yaz"/> tek bir degisken okumasindan ibarettir, uretim
/// yolunda dosya acilmaz ve bekleme kurulmaz.
///
/// <para>Saat surecin kendi baslangicidir (<see cref="Process.StartTime"/>), boylece
/// olcen taraf ile olculen taraf ayni sifir noktasini kullanir: kabuk surecin baslamasini
/// beklemek zorunda kalmaz.</para>
/// </summary>
internal static class AcilisIzi
{
    internal const string Degisken = "VIDSHRINK_ACILIS_IZI";

    /// <summary>Baslaticinin gecirdigi sifir noktasi; UTC tick.</summary>
    internal const string SifirDegiskeni = "VIDSHRINK_ACILIS_T0";

    private static readonly DateTime Baslangic = SurecBaslangici();

    /// <summary>
    /// Baslatici kendi dogum anini gecirdiyse taban odur: cift tikin harcadigi sure
    /// boylece olcunun icine girer. Gecmediyse (app dogrudan acildi) surecin kendi
    /// baslangici kullanilir.
    /// </summary>
    private static DateTime SurecBaslangici()
    {
        var isaret = Environment.GetEnvironmentVariable(SifirDegiskeni);
        if (long.TryParse(isaret, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tick) &&
            tick > 0 && tick <= DateTime.UtcNow.Ticks)
            return new DateTime(tick, DateTimeKind.Utc);

        try { return Process.GetCurrentProcess().StartTime.ToUniversalTime(); }
        catch (Exception) { return DateTime.UtcNow; }
    }

    /// <summary>Iz aciksa <c>true</c>. Kapaliyken cagiran taraf hicbir is yapmaz.</summary>
    internal static bool Acik => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Degisken));

    /// <summary>Surec baslangicindan bu yana gecen milisaniye.</summary>
    internal static double Gecen => (DateTime.UtcNow - Baslangic).TotalMilliseconds;

    /// <summary>
    /// Bir adimi izin sonuna yazar. Dosya her cagride acilip kapanir: olcum surecin
    /// oldurulmesiyle bitiyor, tamponda kalan satir kaybolurdu.
    /// </summary>
    internal static void Yaz(string adim)
    {
        var yol = Environment.GetEnvironmentVariable(Degisken);
        if (string.IsNullOrWhiteSpace(yol)) return;

        try
        {
            File.AppendAllText(yol, string.Create(CultureInfo.InvariantCulture,
                $"{adim}\t{Gecen:0.0}{Environment.NewLine}"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

public partial class MainWindow
{
    /// <summary>
    /// Ilk karenin ekrana geldigi ani yazar. Kare <c>PlayerView</c>'in 16 ms'lik cizim
    /// saati tarafindan kopyalaniyor; buradan gorunen isaret goruntunun kaynaginin
    /// dolmasi (<c>Frame.Source</c>). Isaret dustukten sonra bir de cizim onceligiyle
    /// kuyruga girilir, boylece yazilan an kaynagin dolmasi degil o kareyi tasiyan cizim
    /// gecisinin sirasi olur.
    ///
    /// <para>Yalnizca <see cref="AcilisIzi.Degisken"/> doluyken kurulur; uretimde ne saat
    /// doner ne bekleme olur.</para>
    /// </summary>
    private void IlkKareyiBekle()
    {
        if (!AcilisIzi.Acik) return;

        var saat = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(1) };
        saat.Tick += (_, _) =>
        {
            if (Player.Frame.Source is null) return;
            saat.Stop();
            AcilisIzi.Yaz("kare-kaynagi");
            Dispatcher.UIThread.Post(() => AcilisIzi.Yaz("ilk-kare"), DispatcherPriority.Render);
        };
        saat.Start();
    }
}
