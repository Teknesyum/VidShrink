using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

/// <summary>
/// <c>VidShrink.exe --recorder-selftest</c>: renk kartini (<see cref="RecorderColourCard"/>)
/// ekranin ortasinda bir saniye gosterir, ayni bolgeyi kaydin HDR'den SDR'ye ceviren yolundan ve
/// 8 bit <c>bgra</c> yoldan birer kare yakalar, sonucu <c>recorder-last.log</c>'a yazar ve
/// kapanir. Cikis kodu: 0 gecti, 1 esik asildi, 2 sinama kosamadi. Kullanicidan tek komut ister;
/// ayar dosyasina, kayit klasorune dokunmaz.
/// </summary>
internal static class RecorderSelfTest
{
    internal const string Flag = "--recorder-selftest";

    internal const int PassCode = 0;
    internal const int FailCode = 1;
    internal const int ErrorCode = 2;

    private static readonly TimeSpan ShowFor = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan GrabTimeout = TimeSpan.FromSeconds(10);

    internal static bool Requested(IEnumerable<string> args)
        => args.Any(a => string.Equals(a?.Trim(), Flag, StringComparison.OrdinalIgnoreCase));

    /// <summary>Sinama penceresi; <see cref="App"/> oz sinama kipinde ana pencere olarak bunu acar.</summary>
    internal static Window CardWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", RecorderColourCard.Columns))),
            RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("*", RecorderColourCard.Rows)))
        };
        for (var i = 0; i < RecorderColourCard.Patches.Count; i++)
        {
            var p = RecorderColourCard.Patches[i];
            var cell = new Border { Background = new SolidColorBrush(Color.FromRgb(p.R, p.G, p.B)) };
            Grid.SetColumn(cell, i % RecorderColourCard.Columns);
            Grid.SetRow(cell, i / RecorderColourCard.Columns);
            grid.Children.Add(cell);
        }

        var window = new Window
        {
            Content = grid,
            Width = 640,
            Height = 480,
            WindowDecorations = WindowDecorations.None,
            CanResize = false,
            Topmost = true,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };
        window.Opened += async (_, _) =>
        {
            await Task.Delay(ShowFor);
            var scale = window.RenderScaling;
            var region = new RecorderRegion(window.Position.X, window.Position.Y,
                (int)Math.Round(window.ClientSize.Width * scale), (int)Math.Round(window.ClientSize.Height * scale));
            var (text, code) = await Task.Run(() => MeasureAsync(region, DdaOutputs.Enumerate()));
            KayitTanisi.Yaz(Gunluk.Klasor, text);
            Dispatcher.UIThread.Post(() => desktop.Shutdown(code));
        };
        return window;
    }

    /// <summary>
    /// Olcumun kendisi: <paramref name="card"/> bolgesini iki yoldan yakalar ve rapor metnini
    /// uretir. HDR olmayan ekranda yalniz 8 bit yol kosar.
    /// </summary>
    internal static async Task<(string Text, int Code)> MeasureAsync(
        RecorderRegion card, IReadOnlyList<DdaOutput> outputs,
        Func<IReadOnlyList<string>, CancellationToken, Task<FfmpegRun>>? run = null)
    {
        run ??= (args, ct) => FfmpegRunner.RunAsync(args, ct);
        var inv = CultureInfo.InvariantCulture;
        var b = new StringBuilder();
        b.AppendLine(RecorderColourCard.Header(DateTimeOffset.Now));
        b.Append("app: ").AppendLine(UpdateCheck.CurrentVersion());
        b.AppendLine(string.Format(inv, "card: {0}x{1} at {2},{3}", card.Width, card.Height, card.X, card.Y));

        var request = new RecorderRequest
        {
            Platform = RecorderPlatform.Windows,
            Target = RecorderTargetKind.Region,
            Region = card,
            DdaOutputs = outputs,
            Capture = RecorderCapture.Ddagrab,
            Container = RecorderContainer.Mkv,
            Fps = 10,
            ShowCursor = false,
            HdrToSdr = true,
            CanTonemap = true
        };
        b.Append(KayitTanisi.Metin(request, null, UpdateCheck.CurrentVersion(), DateTimeOffset.Now));

        if (RecorderArguments.DdagrabTarget(request) is not { } target)
        {
            b.AppendLine(RecorderColourCard.OutsideOutput);
            return (b.ToString(), ErrorCode);
        }

        var paths = new List<(string Name, RecorderRequest Request)>();
        if (target.Output.Hdr) paths.Add(("hdr-to-sdr", request));
        paths.Add(("bgra-8bit", request with { DdaOutputs = outputs.Select(o => o with { Hdr = false, Display = null, SdrWhiteNits = null }).ToArray() }));

        var code = PassCode;
        foreach (var (name, path) in paths)
        {
            var raw = Path.Combine(Path.GetTempPath(), "vidshrink-selftest-" + Guid.NewGuid().ToString("N") + ".raw");
            try
            {
                using var limit = new CancellationTokenSource(GrabTimeout);
                var args = RecorderArguments.BuildFrameGrab(path, raw);
                FfmpegRun result;
                try { result = await run(args, limit.Token); }
                catch (OperationCanceledException)
                {
                    b.AppendLine(RecorderColourCard.TimedOut(name));
                    code = ErrorCode;
                    continue;
                }
                if (!result.Ok || !File.Exists(raw))
                {
                    b.AppendLine(RecorderColourCard.FfmpegFailed(name, Gunluk.YoluIndirge(result.StandardError.Trim())));
                    code = ErrorCode;
                    continue;
                }
                var frame = File.ReadAllBytes(raw);
                var results = RecorderColourCard.Compare(RecorderColourCard.Sample(frame, card.Width, card.Height));
                b.Append(RecorderColourCard.Report(name, results));
                if (code == PassCode && !results.All(RecorderColourCard.Passes)) code = FailCode;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                b.AppendLine(RecorderColourCard.PathError(name, Gunluk.YoluIndirge(ex.Message)));
                code = ErrorCode;
            }
            finally
            {
                try { File.Delete(raw); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }

        b.Append("selftest-result: ").AppendLine(code switch { PassCode => "pass", FailCode => "fail", _ => "error" });
        return (b.ToString(), code);
    }
}
