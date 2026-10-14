using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Editing;

/// <summary>Cizelgede boyanan taranmis aralik: kaynaktaki sirasi ve kesilecek, tutulacak ya da hizlanacak durumu.</summary>
internal readonly record struct IdlePreview(long Start, long End, IdleAction Action, int Index);

/// <summary>
/// Otomatik sessizlik ve siyah kare kesme paneli. Tarama secilen ses izinde bir kez kosar; en kisa sure,
/// on pay ve arka pay degisince plan taramasiz tazelenir, esik, tur ya da ses izi degisince yeniden tarama
/// istenir. Onerilen araliklar cizelgede boyanir; araliga tiklamak onu kes, tut ve hizlandir arasinda cevirir.
/// Uygulama tek geri alma adimidir: kesilenler silinir, hizlandirilanlar bolunup hizlanir.
/// </summary>
internal partial class EditorView
{
    private SilenceScan? _silenceScan;
    private string? _silenceSource;
    private string? _silenceTrackSource;
    private CancellationTokenSource? _silenceCts;
    private IReadOnlyList<(long Start, long End)> _silenceCuts = Array.Empty<(long Start, long End)>();
    private readonly List<IdleAction> _silenceActions = new();

    internal Func<string, SilenceCutOptions, double, IProgress<double>, CancellationToken, Task<SilenceScan>> SilenceScanner { get; set; } = DefaultSilenceScan;

    internal IReadOnlyList<(long Start, long End)> SilenceCuts => _silenceCuts;

    internal IReadOnlyList<IdleAction> SilenceActions => _silenceActions;

    internal bool SilenceScanning => _silenceCts is not null;

    private void InitSilence()
    {
        TxtSilenceThreshold.Text = SilenceCutOptions.DefaultThresholdDb.ToString("0.#", Strings.Culture);
        TxtSilenceMin.Text = SilenceCutOptions.DefaultMinSeconds.ToString("0.##", Strings.Culture);
        TxtSilencePaddingStart.Text = SilenceCutOptions.DefaultPaddingSeconds.ToString("0.##", Strings.Culture);
        TxtSilencePaddingEnd.Text = SilenceCutOptions.DefaultPaddingSeconds.ToString("0.##", Strings.Culture);

        BtnSilence.Click += (_, _) => ShowSilencePanel(BtnSilence.IsChecked == true);
        BtnSilenceScan.Click += (_, _) => _ = ScanSilenceAsync();
        BtnSilenceCancel.Click += (_, _) => _silenceCts?.Cancel();
        BtnSilenceApply.Click += (_, _) => ApplySilence();
        BtnSilenceSaveList.Click += (_, _) => _ = SaveAuditListAsync();
        BtnSceneSplit.Click += (_, _) => _ = SplitScenesAsync();
        TxtSceneThreshold.Text = SceneMap.DefaultThreshold.ToString("0.###", Strings.Culture);
        CmbAutoSplit.SelectedIndex = 0;
        BtnAutoSplit.Click += (_, _) => AutoSplitClips();
        TxtMarkerName.LostFocus += (_, _) => CommitMarkerName();
        TxtMarkerName.KeyDown += (_, e) => CommitOnEnter(e, KeyModifiers.None, () => CommitMarkerName());
        ChkSilenceAudio.IsCheckedChanged += (_, _) => RefreshSilence();
        ChkSilenceBlack.IsCheckedChanged += (_, _) => RefreshSilence();
        TxtSilenceThreshold.TextChanged += (_, _) => RefreshSilence();
        TxtSilenceMin.TextChanged += (_, _) => RefreshSilence();
        TxtSilencePaddingStart.TextChanged += (_, _) => RefreshSilence();
        TxtSilencePaddingEnd.TextChanged += (_, _) => RefreshSilence();
        CmbSilenceTrack.SelectionChanged += (_, _) => RefreshSilence();
        Timeline.SelectionChanged += () => UpdateSilencePreview();
        Timeline.IdleSpanClicked += CycleSilenceAction;
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
        EnsureSilenceTracks();
        if (!TryNumber(TxtSilenceThreshold.Text, out var threshold)
            || !TryNumber(TxtSilenceMin.Text, out var min)
            || !TryNumber(TxtSilencePaddingStart.Text, out var paddingStart)
            || !TryNumber(TxtSilencePaddingEnd.Text, out var paddingEnd))
            return null;

        var options = new SilenceCutOptions
        {
            Silence = ChkSilenceAudio.IsChecked == true,
            Black = ChkSilenceBlack.IsChecked == true,
            ThresholdDb = threshold,
            MinSeconds = min,
            PaddingStartSeconds = paddingStart,
            PaddingEndSeconds = paddingEnd,
            AudioTrack = Math.Max(0, CmbSilenceTrack.SelectedIndex)
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
        var (cuts, faster) = SilenceCut.Split(_silenceCuts, _silenceActions);
        if (cuts.Count == 0 && faster.Count == 0) return false;
        var cutCount = _model?.SourceToTimeline(cuts).Count ?? 0;
        var fastCount = faster.Count;
        if (!Apply(model => model.ReshapeSource(cuts, faster, SilenceCut.SpeedFactor), -1)) return false;
        TxtSilenceStatus.Text = fastCount == 0
            ? string.Format(Strings.Culture, Strings.Get("editor.silence.cut"), cutCount)
            : string.Format(Strings.Culture, Strings.Get("editor.silence.applied"), cutCount, fastCount);
        return true;
    }

    private void CycleSilenceAction(int index)
    {
        if (index < 0 || index >= _silenceActions.Count) return;
        _silenceActions[index] = SilenceCut.Next(_silenceActions[index]);
        RefreshSilence();
    }

    private void EnsureSilenceTracks()
    {
        var key = _source ?? string.Empty;
        if (_silenceTrackSource == key) return;
        _silenceTrackSource = key;
        var names = new List<string>();
        if (_source is { } source && KnownInfo?.Invoke(source) is { } info)
            foreach (var stream in info.Streams.Where(s => s.Kind == StreamKind.Audio && !s.IsAttachedPicture))
                names.Add(TrackName(names.Count + 1, stream));
        if (names.Count == 0) names.Add(1.ToString(Strings.Culture));
        CmbSilenceTrack.ItemsSource = names;
        CmbSilenceTrack.SelectedIndex = 0;
    }

    private static string TrackName(int number, SourceStream stream)
    {
        var parts = new[] { number.ToString(Strings.Culture), stream.Language, stream.Title }
            .Where(p => !string.IsNullOrWhiteSpace(p) && p != "und");
        return string.Join(" · ", parts);
    }

    private void ForgetSilence()
    {
        _silenceCts?.Cancel();
        _silenceScan = null;
        _silenceSource = null;
        _silenceCuts = Array.Empty<(long Start, long End)>();
        _silenceActions.Clear();
        _sceneCuts = null;
        _sceneSource = null;
        Timeline.CutPreview = Array.Empty<IdlePreview>();
        TxtSilenceStatus.Text = string.Empty;
        BtnSilenceApply.IsEnabled = false;
        BtnSilenceSaveList.IsEnabled = false;
    }

    private void RefreshSilence()
    {
        _silenceCuts = Array.Empty<(long Start, long End)>();
        var options = ReadSilenceOptions();
        BtnSilenceScan.IsEnabled = _model is { SourceDuration: not null } && options is not null && !SilenceScanning;
        BtnSceneSplit.IsEnabled = _model is { SourceDuration: not null } && !SilenceScanning;
        BtnAutoSplit.IsEnabled = BtnSceneSplit.IsEnabled;
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
        if (_silenceActions.Count != _silenceCuts.Count)
        {
            _silenceActions.Clear();
            _silenceActions.AddRange(Enumerable.Repeat(IdleAction.Cut, _silenceCuts.Count));
        }

        var spans = UpdateSilencePreview();
        var cut = spans.Where(s => s.Action == IdleAction.Cut).ToList();
        TxtSilenceStatus.Text = spans.Count == 0
            ? Strings.Get("editor.silence.none")
            : string.Format(Strings.Culture, Strings.Get("editor.silence.found"), spans.Select(s => s.Index).Distinct().Count(),
                Saat.Kesit(TimeSpan.FromSeconds(EditTime.ToSeconds(cut.Sum(s => s.End - s.Start))), Strings.Culture));
    }

    private IReadOnlyList<IdlePreview> UpdateSilencePreview()
    {
        var spans = new List<IdlePreview>();
        if (SilencePanel.IsVisible && _model is { } model)
            for (var i = 0; i < _silenceCuts.Count; i++)
            {
                var action = i < _silenceActions.Count ? _silenceActions[i] : IdleAction.Cut;
                foreach (var (start, end) in model.SourceToTimeline(new[] { _silenceCuts[i] }))
                    spans.Add(new IdlePreview(start, end, action, i));
            }

        Timeline.CutPreview = spans;
        BtnSilenceApply.IsEnabled = spans.Any(s => s.Action != IdleAction.Keep) && !SilenceScanning;
        RefreshAuditSave();
        return spans;
    }

    private void SetSilenceBusy(bool busy)
    {
        SilenceProgress.Value = 0;
        SilenceProgress.IsVisible = busy;
        BtnSilenceCancel.IsVisible = busy;
        BtnSilenceScan.IsEnabled = !busy;
        BtnSceneSplit.IsEnabled = !busy;
        BtnAutoSplit.IsEnabled = !busy;
        BtnSilenceApply.IsEnabled = false;
        BtnSilenceSaveList.IsEnabled = !busy && HasAuditList;
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
