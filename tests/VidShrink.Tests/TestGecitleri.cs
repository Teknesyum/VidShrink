using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>ffmpeg yoksa atlanir. Klipleri testin kendisi <c>testsrc2</c> ile uretir.</summary>
public sealed class FfmpegFactAttribute : FactAttribute
{
    public FfmpegFactAttribute()
    {
        if (!ToolLocator.IsAvailable(out var missing))
            Skip = $"{missing} bulunamadi, kare cekme testleri kosturulmadi.";
    }
}

/// <summary>
/// ffmpeg **ve** ton esleme zinciri gerektiren testler. `zscale`/`tonemap` her derlemede
/// bulunmuyor; yoksa test dusmez, atlanir. Zincirin yoklugu bir kod hatasi degil, ortam
/// bulgusudur — servis o durumda ton eslemesiz kareye duser.
/// </summary>
public sealed class TonemapFactAttribute : FactAttribute
{
    private static readonly Lazy<string> Filters = new(() =>
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ToolLocator.Ffmpeg,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-filters");

            using var process = new Process { StartInfo = psi };
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            stdout.Wait();
            stderr.Wait();
            process.WaitForExit();
            return stdout.Result;
        }
        catch { return ""; }
    });

    public TonemapFactAttribute()
    {
        if (!ToolLocator.IsAvailable(out var missing))
            Skip = $"{missing} bulunamadi, ton esleme testi kosturulmadi.";
        else if (!Filters.Value.Contains(" zscale") || !Filters.Value.Contains(" tonemap"))
            Skip = "Bu ffmpeg derlemesinde zscale/tonemap yok, HDR ton esleme testi kosturulmadi.";
    }
}

/// <summary>
/// ffmpeg **ve** bu makinede gercekten acilan bir donanim kodlayicisi gerektiren olculer.
/// <c>FfmpegFact</c> tek soru soruyordu: ffmpeg var mi. Kodlayicinin adinin
/// <c>ffmpeg -encoders</c> listesinde gecmesi onun bu makinede acilacagi anlamina gelmiyor:
/// surucu yoksa ad listede durur, acilis <c>Cannot load nvcuda.dll</c> ile duser. Ayrim
/// <see cref="EncoderCapabilities"/> icinde zaten var — <c>HasEncoder</c> derlemeyi,
/// <c>Probe</c> bu makineyi yokluyor; geçit ikincisini kullanir. Donanim yoklugu bir kod
/// hatasi degil ortam bulgusudur, o yuzden dusmez, sebebini yazarak atlanir.
/// </summary>
public sealed class HardwareEncoderFactAttribute : FactAttribute
{
    public const string Codec = "h264_nvenc";

    public HardwareEncoderFactAttribute()
    {
        if (!ToolLocator.IsAvailable(out var missing))
        {
            Skip = $"{missing} bulunamadi, donanim kodlayici olculeri kosturulmadi.";
            return;
        }

        if (!EncoderCapabilities.Instance.HasEncoder(Codec))
        {
            Skip = $"{Codec} bu ffmpeg derlemesinde yok, donanim kodlayici olculeri kosturulmadi.";
            return;
        }

        var probe = EncoderCapabilities.Instance.Probe(Codec);
        if (!probe.Succeeded)
            Skip = $"{Codec} derlemede var ama bu makinede acilmadi ({probe.ElapsedMs}ms), " +
                   "donanim kodlayici olculeri kosturulmadi.";
    }
}

/// <summary>Hicbir kodlayicinin bulunmadigi liste; yazilim bacagini yalniz birakir.</summary>
internal sealed class NoEncoders : IEncoderAvailability
{
    public static readonly NoEncoders Instance = new();

    public bool HasEncoder(string name) => false;
    public bool WorksAsEncoder(string codec) => false;
}

/// <summary>
/// ffmpeg **ve** olcum boyunca bos duran bir makine gerektiren olculer. Yuk iddialari
/// ("yuk maliyeti yalniz artirabilir") bir **bos taban** varsayar; makinede baska isler
/// kosuyorsa "bos" diye alinan okuma bos degildir ve iddia gercekte olmayan bir seyi
/// olcer. Bu geçit varsayimi ilan eder: urunun kendi siniflandiricisi
/// (<see cref="PerformanceProbe"/> + <see cref="PerformanceCheck.HeavyLoadCores"/>) bir kez
/// kosturulur, makine <see cref="RecordingImpact.SoftwareLightLoad"/> donmuyorsa iddia
/// kurulmaz ve sebep olculen cekirdek sayisiyla yazilir.
///
/// Butce urunun kendi varsayilani (<see cref="PerformanceProbe.BudgetMs"/>); burada yeni
/// bir esik uydurulmadi. Okuma sureç omru boyunca bir kez alinir.
/// </summary>
public sealed class QuietMachineFactAttribute : FactAttribute
{
    private static readonly Lazy<PerformanceCheckResult?> Reading = new(() =>
    {
        try { return PerformanceProbe.RunAsync(NoEncoders.Instance, PerformanceProbe.BudgetMs).GetAwaiter().GetResult(); }
        catch { return null; }
    });

    public QuietMachineFactAttribute()
    {
        if (!ToolLocator.IsAvailable(out var missing))
        {
            Skip = $"{missing} bulunamadi, bos makine olculeri kosturulmadi.";
            return;
        }

        var reading = Reading.Value;
        if (reading is null || !reading.SoftwareMeasured)
        {
            Skip = "makinenin bos okumasi alinamadi (yazilim bacagi olculemedi), " +
                   "bos makine iddiasi kurulmadi.";
            return;
        }

        if (reading.Impact != RecordingImpact.SoftwareLightLoad)
            Skip = $"makine olcum oncesi bos degil: yazilim bacagi " +
                   $"{reading.SoftwareRealtimeCores.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} " +
                   $"gercek zaman cekirdegi istedi, esik {PerformanceCheck.HeavyLoadCores}. " +
                   "Bos taban yok, yuk iddiasi kurulmadi.";
    }
}
