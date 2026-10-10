using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Duzenleyici penceresini kapatir ve kaynagi okuyan arka plan taramalarinin (ses dalgasi
/// ffmpeg'i, anahtar kare ffprobe'u) bittigini bekler. Tarama sureci kaynak dosyayi acik
/// tutar; beklenmeden silinen klasor "being used by another process" ile duser.
/// </summary>
internal static class DuzenleyiciKapanis
{
    internal static void Kapat(Window pencere, EditorView view, double saniye = 30)
    {
        pencere.Close();
        Assert.True(Birakti(view, saniye), "pencere kapandi ama kaynagi okuyan tarama bitmedi");
    }

    internal static bool Birakti(EditorView view, double saniye)
    {
        var saat = Stopwatch.StartNew();
        while (!(view.PeakLoad.IsCompleted && view.KeyframeLoad.IsCompleted) && saat.Elapsed.TotalSeconds < saniye)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        return view.PeakLoad.IsCompleted && view.KeyframeLoad.IsCompleted;
    }
}

public sealed class DuzenleyiciKapanisTests
{
    private static Task<T> IptaleKadar<T>(CancellationToken ct, TaskCompletionSource basladi)
    {
        var bekleyen = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => bekleyen.TrySetCanceled(ct));
        basladi.TrySetResult();
        return bekleyen.Task;
    }

    [Fact]
    public void PencereKapanincaKaynagiOkuyanTaramalarIptalEdilir()
    {
        var s = AppHost.Run(() =>
        {
            var dalga = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var kare = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var view = new EditorView
            {
                KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1 },
                Projects = null,
                PeakReader = (_, ct) => IptaleKadar<AudioPeaks>(ct, dalga),
                KeyframeReader = (_, ct) => IptaleKadar<IReadOnlyList<double>>(ct, kare),
            };
            view.Player.EngineFactory = () => new KlipMotoru();
            var pencere = new Window { Width = 900, Height = 600, Content = view };
            pencere.Show();
            var kapandi = false;
            try
            {
                var acilis = view.OpenSourceAsync(Path.Combine(Path.GetTempPath(), "vidshrink-olmayan-kaynak.mp4"));
                DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted && dalga.Task.IsCompleted && kare.Task.IsCompleted, 10);
                var basladi = dalga.Task.IsCompleted && kare.Task.IsCompleted;
                var acikken = DuzenleyiciKapanis.Birakti(view, 0.3);
                pencere.Close();
                kapandi = true;
                return (basladi, acikken, Kapaninca: DuzenleyiciKapanis.Birakti(view, 5));
            }
            finally
            {
                if (!kapandi) pencere.Close();
            }
        });

        Assert.True(s.basladi, "taramalar baslamadi");
        Assert.False(s.acikken, "pencere acikken tarama kendiliginden bitti; olcu bir sey olcmuyor");
        Assert.True(s.Kapaninca, "pencere kapandi, tarama iptal edilmedi");
    }
}
