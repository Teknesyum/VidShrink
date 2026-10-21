using System;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.App.Localization;
using VidShrink.Ffmpeg;

namespace VidShrink.App;

public partial class MainWindow
{
    internal KaliteOlcer KaliteOlcer { get; set; } = KaliteSatiri.Varsayilan;

    internal Action<string> KaliteGunlugu { get; set; } = KaliteSatiri.GunlugeYaz;

    internal string KaliteYuzdesi => TxtProgressPercent.IsVisible ? TxtProgressPercent.Text ?? "" : "";

    internal async Task<string> KaliteSatiriAsync(string kaynak, string cikti, CancellationToken ct)
    {
        SetRunning(true);
        SetStage(TxtStage, Say("main.output.quality-check"));
        TxtRemaining.Text = "-";
        Progress.IsIndeterminate = false;
        KaliteIlerlemesi(0);
        TxtProgressPercent.IsVisible = true;
        try
        {
            return await KaliteSatiri.OlcAsync(KaliteOlcer, kaynak, cikti, new ArayuzIlerlemesi(KaliteIlerlemesi),
                KaliteMetni, Say("main.run.quality.failed"), KaliteGunlugu, ct);
        }
        finally
        {
            TxtProgressPercent.IsVisible = false;
        }
    }

    private void KaliteIlerlemesi(double kesir)
    {
        Progress.Value = kesir;
        TxtProgressPercent.Text = KaliteSatiri.Yuzde(kesir, Strings.Culture);
    }

    internal static string KaliteMetni(OutputQuality olcum)
        => KaliteSatiri.Metin(olcum, Say, Strings.Culture);
}
