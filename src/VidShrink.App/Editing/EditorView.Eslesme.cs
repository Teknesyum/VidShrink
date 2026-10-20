using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.App.Localization;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    private ColorStats? _matchReference;
    private int _matchReferenceNumber;
    private CancellationTokenSource? _matchCts;

    internal Func<string, double, double, string, CancellationToken, Task<ColorStats?>> ColorProber { get; set; } = ColorProbe.RunAsync;

    internal ColorStats? MatchReference => _matchReference;

    private void InitClipMatch()
    {
        BtnClipMatchReference.Click += async (_, _) => await SetMatchReferenceAsync().ConfigureAwait(true);
        BtnClipMatchApply.Click += async (_, _) => await MatchClipColorsAsync().ConfigureAwait(true);
        BtnClipMatchClear.Click += (_, _) => { if (_showingClip) SetClipEffects(e => e.WithoutMatch()); };
    }

    private void ShowClipMatch(ClipEffects effects)
    {
        BtnClipMatchApply.IsEnabled = _matchReference is not null && _matchCts is null;
        BtnClipMatchClear.IsEnabled = effects.HasMatch;
    }

    internal async Task<bool> SetMatchReferenceAsync()
    {
        var index = ActiveIndex;
        if (_model is not { } model || index < 0 || index >= model.Clips.Count) return false;
        var clip = model.Clips[index];
        var stats = await ProbeClipAsync(clip, ClipFilters.Color(clip.Effects)).ConfigureAwait(true);
        if (stats is null) return false;
        _matchReference = stats;
        _matchReferenceNumber = index + 1;
        TxtClipMatch.Text = string.Format(Strings.Culture, Strings.Get("editor.clip.match.ready"), _matchReferenceNumber);
        BtnClipMatchApply.IsEnabled = true;
        return true;
    }

    internal async Task<int> MatchClipColorsAsync()
    {
        if (_model is not { } model || _matchReference is not { } reference) return 0;
        var found = new List<(EditClip Clip, ColorStats Stats)>();
        foreach (var index in ClipTargets())
        {
            var clip = model.Clips[index];
            var stats = await ProbeClipAsync(clip, string.Empty).ConfigureAwait(true);
            if (stats is null) return 0;
            found.Add((clip, stats));
        }

        if (found.Count == 0 || _model is null) return 0;
        var matched = 0;
        Apply(timeline =>
        {
            foreach (var (clip, stats) in found)
            {
                var at = IndexOfClip(timeline, clip);
                if (at >= 0 && timeline.SetEffects(new[] { at }, e => ClipMatch.Match(e, reference, stats))) matched++;
            }

            return matched > 0;
        }, -1);
        TxtClipMatch.Text = string.Format(Strings.Culture, Strings.Get("editor.clip.match.done"), matched, _matchReferenceNumber);
        return matched;
    }

    private static int IndexOfClip(EditTimeline timeline, EditClip clip)
    {
        for (var i = 0; i < timeline.Clips.Count; i++)
            if (ReferenceEquals(timeline.Clips[i], clip)) return i;
        return -1;
    }

    private async Task<ColorStats?> ProbeClipAsync(EditClip clip, string colorChain)
    {
        var paths = SourcePaths;
        if (clip.Source < 0 || clip.Source >= paths.Count) return null;
        _matchCts?.Cancel();
        using var cts = new CancellationTokenSource();
        _matchCts = cts;
        SetMatchBusy(true);
        ColorStats? stats = null;
        try
        {
            stats = await ColorProber(paths[clip.Source], EditTime.ToSeconds(clip.SourceStart), EditTime.ToSeconds(clip.SourceLength), colorChain, cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            if (ReferenceEquals(_matchCts, cts))
            {
                _matchCts = null;
                SetMatchBusy(false);
            }
        }

        if (stats is null) TxtClipMatch.Text = Strings.Get("editor.clip.match.failed");
        return stats;
    }

    private void SetMatchBusy(bool busy)
    {
        BtnClipMatchReference.IsEnabled = !busy;
        BtnClipMatchApply.IsEnabled = !busy && _matchReference is not null;
    }
}
