using System;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

public partial class MainWindow
{
    internal Func<string, string, CancellationToken, Task<OutputQuality?>> KaliteOlcer { get; set; }
        = (kaynak, cikti, ct) => OutputQualityCheck.MeasureAsync(kaynak, cikti, ct);

    internal async Task<string> KaliteSatiriAsync(string kaynak, string cikti, CancellationToken ct)
    {
        SetRunning(true);
        SetStage(TxtStage, Say("main.output.quality-check"));
        TxtRemaining.Text = "-";
        Progress.IsIndeterminate = true;
        try
        {
            var olcum = await KaliteOlcer(kaynak, cikti, ct);
            return olcum is null ? Say("main.run.quality.failed") : KaliteMetni(olcum);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return "";
        }
        catch (Exception)
        {
            return Say("main.run.quality.failed");
        }
        finally
        {
            Progress.IsIndeterminate = false;
        }
    }

    internal static string KaliteMetni(OutputQuality olcum)
        => Say("main.run.quality", OlcutAdi(olcum.Metric),
            Num(olcum.Score, olcum.Metric == OutputQualityMetric.Ssim ? "0.000" : "0.0"),
            Say(BantAnahtari(olcum.Band)));

    internal static string OlcutAdi(OutputQualityMetric olcut) => olcut switch
    {
        OutputQualityMetric.Vmaf => "VMAF",
        OutputQualityMetric.Ssim => "SSIM",
        _ => "PSNR"
    };

    internal static string BantAnahtari(OutputQualityBand bant) => bant switch
    {
        OutputQualityBand.Transparent => "main.run.quality.transparent",
        OutputQualityBand.Good => "main.run.quality.good",
        OutputQualityBand.Visible => "main.run.quality.visible",
        _ => "main.run.quality.poor"
    };
}
