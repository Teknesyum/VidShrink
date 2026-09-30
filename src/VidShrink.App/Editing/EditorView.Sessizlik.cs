using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Editing;

/// <summary>
/// Otomatik sessizlik ve siyah kare kesme paneli. Tarama bir kez kosar; en kisa sure ve kenar
/// payi degisince plan taramasiz tazelenir, esik ya da tur degisince yeniden tarama istenir.
/// Onerilen araliklar cizelgede boyanir, "Araliklari kes" tek geri alma adimidir.
/// </summary>
internal partial class EditorView
{
    private SilenceScan? _silenceScan;
    private string? _silenceSource;
    private CancellationTokenSource? _silenceCts;
    private IReadOnlyList<(long Start, long End)> _silenceCuts = Array.Empty<(long Start, long End)>();

    internal Func<string, SilenceCutOptions, double, IProgress<double>, CancellationToken, Task<SilenceScan>> SilenceScanner { get; set; } = DefaultSilenceScan;

    internal IReadOnlyList<(long Start, long End)> SilenceCuts => _silenceCuts;

    internal bool SilenceScanning => _silenceCts is not null;

    private void InitSilence()
    {
        TxtSilenceThreshold.Text = SilenceCutOptions.DefaultThresholdDb.ToString("0.#", Strings.Culture);
        TxtSilenceMin.Text = SilenceCutOptions.DefaultMinSeconds.ToString("0.##", Strings.Culture);
        TxtSilencePadding.Text = SilenceCutOptions.DefaultPaddingSeconds.ToString("0.##", Strings.Culture);

        BtnSilence.Click += (_, _) => ShowSilencePanel(BtnSilence.IsChecked == true);
        BtnSilenceScan.Click += (_, _) => _ = ScanSilenceAsync();
        BtnSilenceCancel.Click += (_, _) => _silenceCts?.Cancel();
        BtnSilenceApply.Click += (_, _) => ApplySilence();
        ChkSilenceAudio.IsCheckedChanged += (_, _) => RefreshSilence();
        ChkSilenceBlack.IsCheckedChanged += (_, _) => RefreshSilence();
        TxtSilenceThreshold.TextChanged += (_, _) => RefreshSilence();
        TxtSilenceMin.TextChanged += (_, _) => RefreshSilence();
        TxtSilencePadding.TextChanged += (_, _) => RefreshSilence();
        Timeline.SelectionChanged += () => UpdateSilencePreview();
        RefreshSilence();
    }

    internal void ShowSilencePanel(bool open)
    {
        BtnSilence.IsChecked = open;
        SilencePanel.IsVisible = open;
        if (!open) _silenceCts?.Cancel();
        RefreshSilence();
    }

    /// <summary>Paneldeki ayar; gecersiz sayi ya da hic tur secilmemisse <c>null</c>.</summary>
    internal SilenceCutOptions? ReadSilenceOptions()
    {
        if (!TryNumber(TxtSilenceThreshold.Text, out var threshold)
            || !TryNumber(TxtSilenceMin.Text, out var min)
            || !TryNumber(TxtSilencePadding.Text, out var padding))
            return null;

        var options = new SilenceCutOptions
        {
            Silence = ChkSilenceAudio.IsChecked == true,
            Black = ChkSilenceBlack.IsChecked == true,
            ThresholdDb = threshold,
            MinSeconds = min,
            PaddingSeconds = padding
        };
        return options.IsValid ? options : null;
    }

    internal async Task<bool> ScanSilenceAsync()
    {
        if (_model is not { SourceDuration: { } duration } || _source is not { } source || ReadSilenceOptions() is not { } options) return false;

        _silenceCts?.Cancel();
        using var cts = new CancellationTokenSource();
        _silenceCts = cts;
        _silenceScan = null;
        SetSilenceBusy(true);
        TxtSilenceStatus.Text = Strings.Get("editor.silence.scanning");
        var progress = new Progress<double>(value => { if (ReferenceEquals(_silenceCts, cts)) SilenceProgress.Value = value; });

        string? failure = null;
        try
        {
            var scan = await SilenceScanner(source, options, EditTime.ToSeconds(duration), progress, cts.Token).ConfigureAwait(true);
            if (!ReferenceEquals(_silenceCts, cts) || !CurrentMedia.SamePath(source, _source)) return false;
            _silenceScan = scan;
            _silenceSource = source;
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or Win32Exception or UnauthorizedAccessException)
        {
            failure = ex.Message;
            return false;
        }
        finally
        {
            if (ReferenceEquals(_silenceCts, cts))
            {
                _silenceCts = null;
                SetSilenceBusy(false);
                RefreshSilence();
                if (failure is not null)
                    TxtSilenceStatus.Text = string.Format(Strings.Culture, Strings.Get("editor.silence.failed"), failure);
            }
        }
    }

    internal bool ApplySilence()
    {
        var cuts = _silenceCuts;
        if (cuts.Count == 0) return false;
        var count = _model?.SourceToTimeline(cuts).Count ?? 0;
        if (!Apply(model => model.RemoveSource(cuts), -1)) return false;
        TxtSilenceStatus.Text = string.Format(Strings.Culture, Strings.Get("editor.silence.cut"), count);
        return true;
    }

    private void ForgetSilence()
    {
        _silenceCts?.Cancel();
        _silenceScan = null;
        _silenceSource = null;
        _silenceCuts = Array.Empty<(long Start, long End)>();
        Timeline.CutPreview = Array.Empty<(long Start, long End)>();
        TxtSilenceStatus.Text = string.Empty;
        BtnSilenceApply.IsEnabled = false;
    }

    private void RefreshSilence()
    {
        _silenceCuts = Array.Empty<(long Start, long End)>();
        var options = ReadSilenceOptions();
        BtnSilenceScan.IsEnabled = _model is { SourceDuration: not null } && options is not null && !SilenceScanning;
        if (SilenceScanning) return;

        var scan = _silenceScan;
        if (scan is not null && (!CurrentMedia.SamePath(_silenceSource, _source) || options is null || !scan.Options.SameScan(options)))
        {
            UpdateSilencePreview();
            TxtSilenceStatus.Text = Strings.Get("editor.silence.stale");
            return;
        }

        if (scan is null || options is null || _model is not { SourceDuration: { } duration })
        {
            UpdateSilencePreview();
            TxtSilenceStatus.Text = string.Empty;
            return;
        }

        _silenceCuts = SilenceCut.ToTicks(SilenceCut.Plan(scan.All, EditTime.ToSeconds(duration), options));
        var spans = UpdateSilencePreview();
        TxtSilenceStatus.Text = spans.Count == 0
            ? Strings.Get("editor.silence.none")
            : string.Format(Strings.Culture, Strings.Get("editor.silence.found"), spans.Count,
                Saat.Kesit(TimeSpan.FromSeconds(EditTime.ToSeconds(spans.Sum(s => s.End - s.Start))), Strings.Culture));
    }

    private IReadOnlyList<(long Start, long End)> UpdateSilencePreview()
    {
        var spans = SilencePanel.IsVisible && _model is { } model && _silenceCuts.Count > 0
            ? model.SourceToTimeline(_silenceCuts)
            : Array.Empty<(long Start, long End)>();
        Timeline.CutPreview = spans;
        BtnSilenceApply.IsEnabled = spans.Count > 0 && !SilenceScanning;
        return spans;
    }

    private void SetSilenceBusy(bool busy)
    {
        SilenceProgress.Value = 0;
        SilenceProgress.IsVisible = busy;
        BtnSilenceCancel.IsVisible = busy;
        BtnSilenceScan.IsEnabled = !busy;
        BtnSilenceApply.IsEnabled = false;
    }

    private static bool TryNumber(string? text, out double value)
    {
        var trimmed = text?.Trim();
        value = 0;
        return !string.IsNullOrEmpty(trimmed)
               && (double.TryParse(trimmed, NumberStyles.Float, Strings.Culture, out value)
                   || double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
               && double.IsFinite(value);
    }

    private static Task<SilenceScan> DefaultSilenceScan(string source, SilenceCutOptions options, double duration, IProgress<double> progress, CancellationToken ct)
    {
        if (!ToolLocator.IsAvailable(out var missing)) throw new InvalidOperationException(missing);
        var ffmpeg = ToolLocator.Ffmpeg;
        return Task.Run(() => SilenceCut.ScanAsync(ffmpeg, source, options, duration, progress, ct), ct);
    }
}
