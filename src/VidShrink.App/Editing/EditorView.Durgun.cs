using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Editing;

/// <summary>
/// Resim, duz renk ve kare dondurma klipleri (DZB-04, DZB-05, DZB-49): her biri <see cref="StillClip"/> ile kisa bir ara
/// videoya cevrilir ve ek kaynak olarak cizelgeye girer. Ara videolar <see cref="StillRoot"/> altinda durur; proje
/// deposu varsa onun klasorunde, yoksa gecici klasorde.
/// </summary>
internal partial class EditorView
{
    internal const string ColorBlack = "#000000";

    internal const string ColorWhite = "#FFFFFF";

    private string? _stillRoot;

    /// <summary>Ara videolari kosan dikis; verilmezse ffmpeg kosar.</summary>
    internal Func<IReadOnlyList<string>, CancellationToken, Task<FfmpegRun>>? StillRunner { get; set; }

    internal string StillRoot
    {
        get => _stillRoot ??= Projects is { } store
            ? Path.Combine(store.Folder, StillClip.FolderName)
            : Path.Combine(Path.GetTempPath(), "vidshrink_still_" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        set => _stillRoot = value;
    }

    private void InitStills()
    {
        MnuColorBlack.Click += (_, _) => _ = AddColorClipAsync(ColorBlack);
        MnuColorWhite.Click += (_, _) => _ = AddColorClipAsync(ColorWhite);
        MnuFreeze.Click += (_, _) => _ = FreezeFrameAsync();
        TimelineMenu.Opening += (_, _) =>
        {
            MnuColorClip.IsEnabled = _model is not null && !Exporting;
            MnuFreeze.IsEnabled = _model is { Duration: > 0 } && !Exporting;
        };
    }

    /// <summary>Resmi <see cref="StillClip.ImageSeconds"/> sureli ara videoya cevirip cizelgenin sonuna ekler.</summary>
    internal async Task<bool> AddImageAsync(string path)
    {
        if (_model is not { } model || _source is null || Exporting) return false;
        var (width, height, fps) = await StillFrameAsync().ConfigureAwait(true);
        var stamp = SourceStamp.Of(path);
        var name = StillClip.OutputName("resim", Path.GetFullPath(path), stamp?.Size ?? 0, stamp?.ModifiedUtcTicks ?? 0, width, height, fps, StillClip.ImageSeconds);
        var clip = stamp is null ? null : await MakeStillAsync(name, output => StillClip.FromImage(path, output, width, height, fps, StillClip.ImageSeconds)).ConfigureAwait(true);
        if (!ReferenceEquals(model, _model) || Exporting) return false;
        if (clip is null) return StillFailed(Path.GetFileName(path));
        var index = Remember(clip, SourceStamp.Of(clip));
        return Apply(m => m.AddSource(EditTime.FromSeconds(StillClip.ImageSeconds)) == index, model.Clips.Count, reload: true);
    }

    /// <summary>Duz renk klibini oynatma basina araya ekler.</summary>
    internal async Task<bool> AddColorClipAsync(string color)
    {
        if (_model is not { } model || _source is null || Exporting || !StillClip.TryColor(color, out var value)) return false;
        var (width, height, fps) = await StillFrameAsync().ConfigureAwait(true);
        var name = StillClip.OutputName("renk", value, width, height, fps, StillClip.ImageSeconds);
        var clip = await MakeStillAsync(name, output => StillClip.FromColor(color, output, width, height, fps, StillClip.ImageSeconds)).ConfigureAwait(true);
        if (!ReferenceEquals(model, _model) || Exporting) return false;
        if (clip is null) return StillFailed(color);
        return InsertStill(clip, StillClip.ImageSeconds);
    }

    /// <summary>
    /// Oynatma basinda gorunen kareyi resme cikarir, <see cref="StillClip.FreezeSeconds"/> sureli klip yapar ve o ana
    /// araya ekler; oynatma basindaki klip ikiye bolunur.
    /// </summary>
    internal async Task<bool> FreezeFrameAsync()
    {
        if (_model is not { Duration: > 0 } model || _source is null || Exporting) return false;
        var at = Math.Clamp(Timeline.RoundToFrame(Timeline.Playhead), 0, model.Duration - 1);
        var index = ClipIndexAt(model, at);
        if (index < 0) return false;
        var source = model.Clips[index].Source;
        if (source < 0 || source >= SourcePaths.Count) return false;
        var path = SourcePaths[source];
        var seconds = EditTime.ToSeconds(model.ToSource(at));
        var (width, height, fps) = await StillFrameAsync().ConfigureAwait(true);
        var stamp = SourceStamp.Of(path);
        var name = StillClip.OutputName("kare", Path.GetFullPath(path), stamp?.Size ?? 0, stamp?.ModifiedUtcTicks ?? 0, seconds, width, height, fps, StillClip.FreezeSeconds);
        var frame = Path.Combine(StillRoot, Path.ChangeExtension(name, ".png"));
        var clip = await MakeStillAsync(name, output => StillClip.FromImage(frame, output, width, height, fps, StillClip.FreezeSeconds),
            () => RunStillAsync(StillClip.GrabFrame(path, seconds, frame))).ConfigureAwait(true);
        TryDelete(frame);
        if (!ReferenceEquals(model, _model) || Exporting) return false;
        if (clip is null) return StillFailed(Path.GetFileName(path));
        return InsertStill(clip, StillClip.FreezeSeconds, at);
    }

    private bool InsertStill(string clip, double seconds, long? at = null)
    {
        if (_model is not { } model) return false;
        var start = Math.Clamp(at ?? Timeline.RoundToFrame(Timeline.Playhead), 0, model.Duration);
        Remember(clip, SourceStamp.Of(clip));
        var placed = -1;
        if (!Apply(m => (placed = m.InsertSource(EditTime.FromSeconds(seconds), start)) >= 0, -1, reload: true)) return false;
        Timeline.SelectedIndex = placed;
        return true;
    }

    private static int ClipIndexAt(EditTimeline model, long at)
    {
        long start = 0;
        for (var i = 0; i < model.Clips.Count; i++)
        {
            var length = model.Clips[i].TimelineLength;
            if (at < start + length) return i;
            start += length;
        }

        return -1;
    }

    private async Task<(int Width, int Height, double Fps)> StillFrameAsync()
    {
        var info = _source is { } source ? await ProbeSourceAsync(source).ConfigureAwait(true) : null;
        var (width, height) = StillClip.Frame(info);
        return (width, height, StillClip.Rate(info));
    }

    private async Task<string?> MakeStillAsync(string name, Func<string, IReadOnlyList<string>> arguments, Func<Task<bool>>? before = null)
    {
        var output = Path.Combine(StillRoot, name);
        try
        {
            if (File.Exists(output) && new FileInfo(output).Length > 0) return output;
            Directory.CreateDirectory(StillRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (before is not null && !await before().ConfigureAwait(true)) return null;
        var part = output + ".part";
        var ok = await RunStillAsync(arguments(part)).ConfigureAwait(true);
        try
        {
            if (!ok || !File.Exists(part) || new FileInfo(part).Length == 0)
            {
                TryDelete(part);
                return null;
            }

            File.Move(part, output, overwrite: true);
            return output;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(part);
            return null;
        }
    }

    private async Task<bool> RunStillAsync(IReadOnlyList<string> arguments)
    {
        var runner = StillRunner ?? FfmpegRunner.RunAsync;
        try
        {
            return (await runner(arguments, CancellationToken.None).ConfigureAwait(true)).Ok;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private bool StillFailed(string what)
    {
        ShowProjectStatus("StatusError", string.Format(Strings.Culture, Strings.Get("editor.still.failed"), what));
        return false;
    }

    private static void TryDelete(string path)
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
