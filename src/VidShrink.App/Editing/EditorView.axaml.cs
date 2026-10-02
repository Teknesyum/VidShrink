using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using VidShrink.Player;

namespace VidShrink.App.Editing;

internal partial class EditorView : UserControl
{
    private static readonly decimal[] SpeedPresets = { 0.5m, 1m, 2m };

    private readonly DispatcherTimer _clock;
    private EditTimeline? _model;
    private string? _source;
    private EdlPreviewDriver? _driver;
    private int _reloads;
    private CancellationTokenSource? _peaks;

    public EditorView()
    {
        InitializeComponent();
        Preview.KeyboardEnabled = false;
        _clock = new DispatcherTimer { Interval = EdlPreviewDriver.DefaultInterval };
        _clock.Tick += (_, _) => Follow();

        Preview.FileDropped = path => _ = OpenSourceAsync(path);
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        Timeline.Seek += OnSeek;
        Timeline.MoveRequested += (from, to) => Move(from, to);
        Timeline.SelectionChanged += RefreshToolbar;
        Timeline.Edited += () => Apply(_ => true, Timeline.SelectedIndex, reload: true);

        BtnUndo.Click += (_, _) => Undo();
        BtnRedo.Click += (_, _) => Redo();
        BtnZoomIn.Click += (_, _) => Timeline.ZoomCentered(true);
        BtnZoomOut.Click += (_, _) => Timeline.ZoomCentered(false);
        TxtSpeed.KeyDown += OnSpeedKey;
        InitExport();
        InitMonitor();
        InitText();
        InitClip();
        InitSilence();

        MnuSplit.Click += (_, _) => Split();
        MnuDelete.Click += (_, _) => DeleteSelected();
        MnuMarkIn.Click += (_, _) => MarkIn();
        MnuMarkOut.Click += (_, _) => MarkOut();
        MnuDeleteRange.Click += (_, _) => DeleteRange();
        MnuReverse.Click += (_, _) => Reverse();
        TimelineMenu.Opening += (_, _) => BuildSpeedMenu();

        RefreshToolbar();
        ShowGestures();
    }

    internal EditTimeline? Model => _model;

    internal string? SourcePath => _source;

    internal EdlPreviewDriver? Driver => _driver;

    internal PlayerView Player => Preview;

    internal EditorTimeline TimelineView => Timeline;

    internal int Reloads => _reloads;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsFinite(availableSize.Height))
        {
            var rows = Layout.RowDefinitions;
            var splitter = double.IsFinite(PreviewSplitter.Height) ? PreviewSplitter.Height : 0;
            rows[2].MaxHeight = Math.Max(0, availableSize.Height - rows[0].MinHeight - splitter);
            LowerScroll.MaxHeight = rows[2].MaxHeight;
        }

        return base.MeasureOverride(availableSize);
    }

    internal void Activate(string? path)
    {
        _clock.Start();
        if (path is not null && !CurrentMedia.SamePath(path, _source)) _ = OpenSourceAsync(path);
    }

    internal void Deactivate()
    {
        _clock.Stop();
        _resumeClock = false;
        if (Preview.IsPlaying) Preview.TogglePlay();
        _driver?.Pause();
    }

    internal async Task EditAsync(string path, double startSeconds)
    {
        var start = double.IsFinite(startSeconds) && startSeconds > 0 ? EditTime.FromSeconds(startSeconds) : 0;
        if (!CurrentMedia.SamePath(path, _source))
        {
            await OpenSourceAsync(path, start).ConfigureAwait(true);
            return;
        }

        if (_model is not { } model) return;
        var at = Math.Clamp(start, 0, model.Duration);
        Timeline.Playhead = at;
        if (_driver is { } driver) await driver.SeekAsync(at).ConfigureAwait(true);
    }

    internal async Task OpenSourceAsync(string path, long start = 0)
    {
        CloseDriver();
        ForgetOverlay();
        ForgetSaved();
        ForgetSilence();
        _source = path;
        _model = null;
        Timeline.Show(null);
        RefreshToolbar();
        LoadPeaks(path);

        try
        {
            await Preview.OpenAsync(path).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or PlaybackEngineUnavailableException or UnauthorizedAccessException)
        {
            return;
        }

        if (!CurrentMedia.SamePath(path, _source) || Preview.Engine is not { } engine || !(engine.DurationSeconds > 0)) return;
        ShowTimeline(EditTimeline.FromSource(EditTime.FromSeconds(engine.DurationSeconds)), SourceFps(KnownInfo?.Invoke(path)?.Fps, engine.FramesPerSecond));
        var at = Math.Clamp(start, 0, _model!.Duration);
        Timeline.Playhead = at;
        await ReloadAsync(at).ConfigureAwait(true);
    }

    private void LoadPeaks(string path)
    {
        _peaks?.Cancel();
        _peaks?.Dispose();
        _peaks = new CancellationTokenSource();
        Timeline.Peaks = null;
        _ = LoadPeaksAsync(path, _peaks.Token);
    }

    private async Task LoadPeaksAsync(string path, CancellationToken ct)
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        AudioPeaks peaks;
        try
        {
            var ffmpeg = ToolLocator.Ffmpeg;
            peaks = await Task.Run(() => AudioPeaks.LoadAsync(ffmpeg, path, ct), ct).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return;
        }

        if (ct.IsCancellationRequested || !CurrentMedia.SamePath(path, _source)) return;
        Timeline.Peaks = peaks;
    }

    internal void ShowTimeline(EditTimeline model, double fps)
    {
        _model = model;
        Timeline.Fps = fps;
        Timeline.Show(model);
        RefreshToolbar();
    }

    internal bool Split()
    {
        var at = Timeline.Playhead;
        return Apply(model => at > 0 && at < model.Duration && model.Split(at), -1);
    }

    internal bool DeleteSelected()
    {
        if (Timeline.SelectedText >= 0) return DeleteSelectedText();
        var all = Timeline.AllSelected;
        var selected = Timeline.SelectedIndices;
        if (selected.Count == 0) return false;
        return Apply(model =>
        {
            if (selected.Any(i => i < 0 || i >= model.Clips.Count)) return false;
            if (!all && selected.Count >= model.Clips.Count) return false;
            return model.DeleteMany(selected);
        }, Math.Max(0, selected.Min() - 1));
    }

    internal bool TrimToPlayhead(bool head)
    {
        var at = Timeline.Playhead;
        var index = Timeline.ClipAt(at);
        return Apply(model =>
        {
            if (index < 0 || index >= model.Clips.Count) return false;
            var start = model.ClipStart(index);
            if (!(head ? model.RippleTrimHead(at) : model.RippleTrimTail(at))) return false;
            if (head) Timeline.Playhead = start;
            return true;
        }, -1);
    }

    internal bool Extract() => DeleteRange();

    internal bool GoToMark(bool markIn)
    {
        if ((markIn ? Timeline.MarkIn : Timeline.MarkOut) is not { } at) return false;
        SeekTo(at);
        return true;
    }

    internal bool GoToEdit(bool next)
    {
        if ((next ? Timeline.NextEditPoint() : Timeline.PrevEditPoint()) is not { } at) return false;
        SeekTo(at);
        return true;
    }

    internal bool DeleteRange()
    {
        if (Timeline.MarkIn is not { } start || Timeline.MarkOut is not { } end) return false;
        var done = Apply(model => end > start && end - start < model.Duration && model.DeleteRange(start, end), -1);
        if (!done) return false;
        Timeline.SetMarks(null, null);
        Timeline.Playhead = start;
        return true;
    }

    internal bool SetSpeed(decimal speed)
    {
        var selected = Timeline.SelectedIndex;
        if (Timeline.AllSelected) return Apply(model => ValidSpeed(speed) && model.SetSpeedAll(speed), -1);
        return Apply(model =>
        {
            if (selected < 0 || selected >= model.Clips.Count || speed == 0) return false;
            var magnitude = decimal.Round(Math.Abs(speed), 2, MidpointRounding.AwayFromZero);
            if (magnitude < EditClip.MinSpeed || magnitude > EditClip.MaxSpeed) return false;
            return model.SetSpeed(selected, speed);
        }, selected);
    }

    private static bool ValidSpeed(decimal speed)
    {
        if (speed == 0) return false;
        var magnitude = decimal.Round(Math.Abs(speed), 2, MidpointRounding.AwayFromZero);
        return magnitude >= EditClip.MinSpeed && magnitude <= EditClip.MaxSpeed;
    }

    internal bool Reverse()
    {
        var selected = ActiveIndex;
        if (_model is not { } model || selected < 0 || selected >= model.Clips.Count) return false;
        var clip = model.Clips[selected];
        return SetSpeed(clip.Reversed ? clip.Speed : -clip.Speed);
    }

    internal bool Move(int from, int to) => Apply(model => model.Move(from, to), to);

    internal bool Undo() => Apply(model => model.Undo(), Timeline.SelectedIndex);

    internal bool Redo() => Apply(model => model.Redo(), Timeline.SelectedIndex);

    internal void MarkIn() => Timeline.SetMarks(Timeline.Playhead, Timeline.MarkOut is { } end && end > Timeline.Playhead ? end : null);

    internal void MarkOut() => Timeline.SetMarks(Timeline.MarkIn is { } start && start < Timeline.Playhead ? start : null, Timeline.Playhead);

    private bool Apply(Func<EditTimeline, bool> edit, int select, bool reload = false)
    {
        if (_model is not { } model) return false;
        var before = model.Clips.ToArray();
        if (!edit(model)) return false;

        var at = Math.Min(Timeline.Playhead, model.Duration);
        Timeline.Refresh();
        if (select >= 0) Timeline.SelectedIndex = Math.Min(select, model.Clips.Count - 1);
        Timeline.Playhead = at;
        ShowTextPanel();
        ShowClipPanel();
        RefreshToolbar();
        if (reload || !SameCuts(before, model.Clips)) _ = ReloadAsync(at);
        else
        {
            if (!before.SequenceEqual(model.Clips)) _ = RestyleAsync();
            RefreshOverlay();
        }

        return true;
    }

    private async Task ReloadAsync(long at)
    {
        if (_model is not { } model || _source is not { } source || Preview.Engine is not { } engine) return;
        if (model.Clips.Count == 0)
        {
            if (Preview.IsPlaying) Preview.TogglePlay();
            _driver?.Dispose();
            _driver = null;
            return;
        }

        var playing = Preview.IsPlaying;
        _driver?.Dispose();
        var preview = new EdlPreview(source, model);
        var driver = new EdlPreviewDriver(engine, preview, look: LookFor(preview, engine));
        _driver = driver;
        _reloads++;
        try
        {
            await driver.OpenAsync().ConfigureAwait(true);
            if (!ReferenceEquals(_driver, driver)) return;
            RefreshOverlay(reopened: true);
            Preview.RefreshDuration();
            await driver.SeekAsync(Math.Clamp(at, 0, model.Duration)).ConfigureAwait(true);
            if (playing) driver.Play();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException)
        {
        }
    }

    internal void Follow()
    {
        if (_driver is not { } driver) return;
        SyncDriver();
        if (Timeline.Scrubbing) return;
        Timeline.Playhead = driver.TimelinePosition;
        if (driver.Playing) Timeline.FollowPlayhead();
        RefreshMonitor();
    }

    private void OnSeek(long time, bool final)
    {
        if (_driver is not { } driver) return;
        _ = driver.SeekAsync(time, final ? SeekPrecision.Exact : SeekPrecision.Keyframe);
    }

    private void OnSpeedKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (decimal.TryParse(TxtSpeed.Text, NumberStyles.Number, Strings.Culture, out var speed)
            || decimal.TryParse(TxtSpeed.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out speed))
            SetSpeed(speed);
        RefreshToolbar();
        e.Handled = true;
    }

    private void BuildSpeedMenu()
    {
        MnuSpeed.Items.Clear();
        foreach (var preset in SpeedPresets)
        {
            var value = preset;
            var item = new MenuItem { Header = Bicim.Kat((double)value, Strings.Culture) + "×" };
            item.Click += (_, _) => SetSpeed(Reversed() ? -value : value);
            MnuSpeed.Items.Add(item);
        }

        var hasClip = ActiveIndex >= 0;
        MnuSpeed.IsEnabled = hasClip;
        MnuReverse.IsEnabled = hasClip;
        MnuDelete.IsEnabled = _canDelete;
        MnuSplit.IsEnabled = BtnSplit.IsEnabled;
        MnuDeleteRange.IsEnabled = Timeline.MarkIn is { } a && Timeline.MarkOut is { } b && b > a;
    }

    private int ActiveIndex => Timeline.AllSelected && _model is { Clips.Count: > 0 } ? 0 : Timeline.SelectedIndex;

    private bool Reversed()
    {
        var selected = ActiveIndex;
        return _model is { } model && selected >= 0 && selected < model.Clips.Count && model.Clips[selected].Reversed;
    }

    private void RefreshToolbar()
    {
        var model = _model;
        var selected = ActiveIndex;
        var hasClip = model is not null && selected >= 0 && selected < model.Clips.Count;
        BtnSplit.IsEnabled = model is not null;
        var chosen = Timeline.SelectedIndices.Count;
        _canDelete = model is { Clips.Count: > 0 } && chosen > 0 && (Timeline.AllSelected || chosen < model.Clips.Count);
        BtnUndo.IsEnabled = model?.CanUndo ?? false;
        BtnRedo.IsEnabled = model?.CanRedo ?? false;
        BtnZoomIn.IsEnabled = model is not null;
        BtnZoomOut.IsEnabled = model is not null;
        BtnZoomFit.IsEnabled = model is not null;
        TxtSpeed.IsEnabled = hasClip;
        if (hasClip)
        {
            var clip = model!.Clips[selected];
            var speed = clip.Reversed ? -clip.Speed : clip.Speed;
            TxtSpeed.Text = Bicim.Kat((double)speed, Strings.Culture);
        }
        else
        {
            TxtSpeed.Text = string.Empty;
        }

        RefreshExport();
        RefreshMonitor();
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DroppedFile(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var file = DroppedFile(e);
        e.Handled = true;
        if (file is not null) _ = OpenSourceAsync(file);
    }

    private static string? DroppedFile(DragEventArgs e)
    {
        var items = e.DataTransfer.TryGetFiles()?.ToList();
        if (items is null || items.Count != 1 || items[0] is IStorageFolder) return null;
        var path = items[0].TryGetLocalPath();
        return path is not null && File.Exists(path) ? path : null;
    }

    private void CloseDriver()
    {
        _driver?.Dispose();
        _driver = null;
    }
}
