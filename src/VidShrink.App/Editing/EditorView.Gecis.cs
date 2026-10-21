using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Editing;

internal partial class EditorView
{
    private readonly Dictionary<string, string> _blends = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MediaInfo> _blendInfo = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _blendWork;
    private Task _blendTask = Task.CompletedTask;
    private string? _blendFolder;
    private bool _blendSwept;
    private int _blendRenders;
    private string? _blendRoot;

    internal Func<IReadOnlyList<string>, CancellationToken, Task<FfmpegRun>>? BlendRunner { get; set; }

    internal string BlendRoot
    {
        get => _blendRoot ?? TransitionPreview.CacheRoot;
        set => _blendRoot = value;
    }

    internal string BlendFolder => _blendFolder ??= TransitionPreview.ProcessFolder(BlendRoot);

    internal Task BlendWork => _blendTask;

    internal int BlendRenders => _blendRenders;

    internal IReadOnlyCollection<string> BlendFiles => _blends.Values;

    private EdlPreview PreviewFor(EditTimeline model)
    {
        var ready = new Dictionary<int, string>();
        foreach (var index in TransitionPreview.Pending(model))
            if (BlendJob(model, index) is { } job && _blends.TryGetValue(job.Key, out var path) && File.Exists(path)) ready[index] = path;
        return new EdlPreview(PreviewPaths, model, ready.Count == 0 ? null : ready);
    }

    private (string Key, IReadOnlyList<string> Arguments)? BlendJob(EditTimeline model, int index)
    {
        var paths = SourcePaths;
        var outgoing = model.Clips[index - 1].Source;
        var incoming = model.Clips[index].Source;
        if (outgoing >= paths.Count || incoming >= paths.Count) return null;
        if (BlendInfo(paths[0]) is not { } first || BlendInfo(paths[outgoing]) is not { } a || BlendInfo(paths[incoming]) is not { } b) return null;
        var infos = paths.Select((path, s) => s == outgoing ? a : s == incoming ? b : BlendInfo(path) ?? first).ToArray();
        var canvas = ClipFilters.Canvas(model, first)
            ?? (Math.Max(2, ClipFilters.DisplayWidth(first.Width, first.ParNum, first.ParDen) / 2 * 2), Math.Max(2, first.Height / 2 * 2));
        var draft = TransitionPreview.Arguments(model, index, paths, infos, canvas, first.Fps, first.HasAudio, Math.Max(_audioTrack, 0), "-");
        var key = TransitionPreview.Key(draft);
        var output = Path.Combine(BlendFolder, TransitionPreview.FileName(key));
        return (key, draft.Take(draft.Count - 1).Append(output).ToArray());
    }

    private MediaInfo? BlendInfo(string path)
    {
        if (_blendInfo.TryGetValue(path, out var known)) return known;
        if (KnownInfo?.Invoke(path) is not { Width: > 0, Height: > 0 } info) return null;
        _blendInfo[path] = info;
        return info;
    }

    private void QueueBlends(EditTimeline model)
    {
        var missing = TransitionPreview.Pending(model).Any(index => BlendJob(model, index) is not { } job || !_blends.ContainsKey(job.Key));
        if (!missing) return;
        _blendWork?.Cancel();
        var work = new CancellationTokenSource();
        _blendWork = work;
        var previous = _blendTask;
        _blendTask = RenderBlendsAsync(model, previous, work.Token);
    }

    private async Task RenderBlendsAsync(EditTimeline model, Task previous, CancellationToken ct)
    {
        try
        {
            await previous.ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is OperationCanceledException)
        {
        }

        if (ct.IsCancellationRequested) return;
        foreach (var path in SourcePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (BlendInfo(path) is not null) continue;
            if (await ProbeSourceAsync(path).ConfigureAwait(true) is { Width: > 0, Height: > 0 } info) _blendInfo[path] = info;
            if (ct.IsCancellationRequested) return;
        }

        if (!_blendSwept)
        {
            _blendSwept = true;
            TransitionPreview.SweepStale(BlendRoot);
        }

        var runner = BlendRunner ?? FfmpegRunner.RunAsync;
        foreach (var index in TransitionPreview.Pending(model))
        {
            if (ct.IsCancellationRequested || !ReferenceEquals(_model, model)) return;
            if (BlendJob(model, index) is not { } job || _blends.ContainsKey(job.Key)) continue;
            var output = job.Arguments[^1];
            try
            {
                Directory.CreateDirectory(BlendFolder);
                var run = await runner(job.Arguments, ct).ConfigureAwait(true);
                if (!run.Ok || !File.Exists(output))
                {
                    TryDeleteBlend(output);
                    continue;
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                TryDeleteBlend(output);
                return;
            }

            _blendRenders++;
            if (ct.IsCancellationRequested || !ReferenceEquals(_model, model) || _driver is not { } driver)
            {
                _blends[job.Key] = output;
                return;
            }

            _blends[job.Key] = output;
            if (PreviewFor(model).Uri != driver.Preview.Uri) await ReloadAsync(Timeline.Playhead).ConfigureAwait(true);
        }
    }

    private void ForgetBlends()
    {
        _blendWork?.Cancel();
        _blendWork = null;
        _blends.Clear();
        _blendInfo.Clear();
        if (_blendFolder is { } folder) TransitionPreview.Remove(folder);
    }

    private static void TryDeleteBlend(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
