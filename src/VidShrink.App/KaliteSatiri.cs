using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

internal delegate Task<OutputQuality?> KaliteOlcer(string kaynak, string cikti, IProgress<double>? ilerleme, CancellationToken ct);

internal static class KaliteSatiri
{
    internal static readonly KaliteOlcer Varsayilan = (kaynak, cikti, ilerleme, ct)
        => OutputQualityCheck.MeasureAsync(kaynak, cikti, OutputQualityCheck.Available, ilerleme, ct);

    internal static async Task<string> OlcAsync(
        KaliteOlcer olcer, string kaynak, string cikti, IProgress<double>? ilerleme,
        Func<OutputQuality, string> metin, string olculemedi, Action<string> gunluk, CancellationToken ct)
    {
        OutputQuality? olcum;
        try
        {
            olcum = await olcer(kaynak, cikti, ilerleme, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return "";
        }
        catch (Exception)
        {
            olcum = null;
        }
        gunluk(OutputQualityCheck.LogLine(cikti, olcum));
        return olcum is null ? olculemedi : metin(olcum);
    }

    internal static string Metin(OutputQuality olcum, Func<string, object?[], string> say, CultureInfo kultur)
        => say("main.run.quality", new object?[]
        {
            OlcutAdi(olcum.Metric),
            olcum.Score.ToString(olcum.Metric == OutputQualityMetric.Ssim ? "0.000" : "0.0", kultur),
            say(BantAnahtari(olcum.Band), Array.Empty<object?>())
        });

    internal static string Yuzde(double kesir, CultureInfo kultur)
        => (Math.Floor(Math.Clamp(kesir, 0, 1) * 100) / 100).ToString("P0", kultur);

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

    internal static void GunlugeYaz(string satir)
    {
        try
        {
            Gunluk.Yaz(Gunluk.Blok(satir, UpdateCheck.CurrentVersion(), ToolLocator.FfmpegVersion, 0, TimeSpan.Zero, Array.Empty<string>()));
        }
        catch (Exception)
        {
        }
    }
}

internal sealed class ArayuzIlerlemesi : IProgress<double>
{
    private readonly Action<double> _yaz;

    internal ArayuzIlerlemesi(Action<double> yaz) => _yaz = yaz;

    public void Report(double value)
    {
        if (Dispatcher.UIThread.CheckAccess()) _yaz(value);
        else Dispatcher.UIThread.Post(() => _yaz(value));
    }
}
