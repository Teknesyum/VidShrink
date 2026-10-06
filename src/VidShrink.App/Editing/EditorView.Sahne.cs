using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.App.Localization;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Editing;

/// <summary>
/// Sahne degisimlerinden bolme. Sessizlik panelinin iptalini, ilerleme cubugunu ve durum
/// satirini paylasir; tarama bitince bulunan sinirlarda parcalar bolunur ve bu tek geri alma
/// adimidir. Sinir yoksa, tarama duserse ya da iptal edilirse cizelge degismez.
/// </summary>
internal partial class EditorView
{
    internal Func<string, double, IProgress<double>, CancellationToken, Task<IReadOnlyList<double>>> SceneScanner { get; set; } = DefaultSceneScan;

    internal async Task<int> SplitScenesAsync()
    {
        if (_model is not { SourceDuration: { } duration } || _source is not { } source) return 0;

        _silenceCts?.Cancel();
        using var cts = new CancellationTokenSource();
        _silenceCts = cts;
        SetSilenceBusy(true);
        TxtSilenceStatus.Text = Strings.Get("editor.silence.scanning");
        var progress = new Progress<double>(value => { if (ReferenceEquals(_silenceCts, cts)) SilenceProgress.Value = value; });

        string? status = null;
        var split = 0;
        try
        {
            var cuts = await SceneScanner(source, EditTime.ToSeconds(duration), progress, cts.Token).ConfigureAwait(true);
            if (!ReferenceEquals(_silenceCts, cts) || !CurrentMedia.SamePath(source, _source)) return 0;

            var boundaries = SceneSplit.ToTicks(cuts);
            Apply(model => (split = model.SplitSource(boundaries, SceneSplit.MinPieceTicks)) > 0, -1);
            status = split > 0
                ? string.Format(Strings.Culture, Strings.Get("editor.scene.done"), split)
                : Strings.Get("editor.scene.none");
            return split;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or Win32Exception or UnauthorizedAccessException)
        {
            status = string.Format(Strings.Culture, Strings.Get("editor.silence.failed"), ex.Message);
            return 0;
        }
        finally
        {
            if (ReferenceEquals(_silenceCts, cts))
            {
                _silenceCts = null;
                SetSilenceBusy(false);
                RefreshSilence();
                if (status is not null) TxtSilenceStatus.Text = status;
            }
        }
    }

    private static Task<IReadOnlyList<double>> DefaultSceneScan(string source, double duration, IProgress<double> progress, CancellationToken ct)
    {
        if (!ToolLocator.IsAvailable(out var missing)) throw new InvalidOperationException(missing);
        return Task.Run(() => SceneDetector.CutsAsync(source, duration, progress, ct), ct);
    }
}
