using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using VidShrink.App.Localization;
using VidShrink.Player;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    internal static readonly IReadOnlyList<string> AspectRatios = new[] { "-1", "16:9", "4:3", "2.35:1" };

    private readonly int _shuffleSeed = Environment.TickCount;
    private PlayerSettings _settings = new();
    private RecentFiles _recent = new();
    private string? _settingsFolder;
    private bool _settingsRead;
    private bool _topmost;
    private bool _infoVisible;
    private int _aspectIndex;
    private int _rotation;
    private bool _mirrored;
    private bool _seekDragging;
    private string? _notice;
    private IReadOnlyList<double> _chapters = Array.Empty<double>();
    private IReadOnlyList<SeekMark> _marks = Array.Empty<SeekMark>();
    private string _marksKey = "";
    private Task<string?> _lastShot = Task.FromResult<string?>(null);
    private Task _navigation = Task.CompletedTask;

    internal PlayerSettings Settings
    {
        get
        {
            EnsureSettings();
            return _settings;
        }
    }

    internal RecentFiles Recent
    {
        get
        {
            EnsureSettings();
            return _recent;
        }
    }

    internal bool IsTopmost => _topmost;

    internal bool InfoVisible => _infoVisible;

    internal string AspectRatio => AspectRatios[_aspectIndex];

    internal int RotationDegrees => _rotation;

    internal bool IsMirrored => _mirrored;

    internal IReadOnlyList<double> Chapters => _chapters;

    internal IReadOnlyList<SeekMark> Marks => _marks;

    internal Task<string?> LastScreenshot => _lastShot;

    internal Task Navigation => _navigation;

    internal string InfoText => TxtInfo.Text ?? "";

    internal string ViewStateText => TxtView.Text ?? "";

    internal void SaveSettings() => _settings.Save(SettingsFile());

    private void InitWindow()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnFileDragOver);
        AddHandler(DragDrop.DropEvent, OnFileDrop);
        SeekBar.AddHandler(PointerPressedEvent, OnSeekPressed);
        SeekBar.AddHandler(PointerMovedEvent, OnSeekMoved);
        SeekBar.AddHandler(PointerReleasedEvent, OnSeekReleased);
        SeekBar.SizeChanged += (_, _) =>
        {
            _marksKey = "";
            RefreshSeekBar();
        };
    }

    private bool ApplyWindow(PlayerCommand command)
    {
        switch (command.Kind)
        {
            case PlayerCommandKind.ToggleTopmost:
                _topmost = !_topmost;
                if (TopLevel.GetTopLevel(this) is Window window) window.Topmost = _topmost;
                _trace.Add("topmost -> " + _topmost);
                return true;
            case PlayerCommandKind.AspectCycle:
                _aspectIndex = (_aspectIndex + 1) % AspectRatios.Count;
                _engine?.SetAspectOverride(AspectRatios[_aspectIndex]);
                _trace.Add("aspect -> " + AspectRatios[_aspectIndex]);
                return true;
            case PlayerCommandKind.Rotate:
                _rotation = (((_rotation + (int)command.Amount) % 360) + 360) % 360;
                _engine?.SetRotation(_rotation);
                _trace.Add("rotate -> " + _rotation.ToString(CultureInfo.InvariantCulture));
                return true;
            case PlayerCommandKind.Mirror:
                _mirrored = !_mirrored;
                _engine?.SetMirrored(_mirrored);
                _trace.Add("mirror -> " + _mirrored);
                return true;
            case PlayerCommandKind.ToggleInfo:
                _infoVisible = !_infoVisible;
                InfoPanel.IsVisible = _infoVisible;
                RefreshInfo();
                _trace.Add("info -> " + _infoVisible);
                return true;
            case PlayerCommandKind.Screenshot:
                _trace.Add("screenshot -> " + TakeScreenshot());
                return true;
            case PlayerCommandKind.FileStep:
                _trace.Add("file " + command.Amount.ToString("0", CultureInfo.InvariantCulture) + " -> " + StepFile(command.Amount > 0));
                return true;
            case PlayerCommandKind.ToggleShuffle:
                EnsureSettings();
                _settings.Shuffle = !_settings.Shuffle;
                SaveSettings();
                _trace.Add("shuffle -> " + _settings.Shuffle);
                return true;
            case PlayerCommandKind.RepeatCycle:
                EnsureSettings();
                _settings.Repeat = _settings.Repeat switch
                {
                    RepeatMode.Off => RepeatMode.All,
                    RepeatMode.All => RepeatMode.One,
                    _ => RepeatMode.Off
                };
                _engine?.SetRepeatFile(_settings.Repeat == RepeatMode.One);
                SaveSettings();
                _trace.Add("repeat -> " + _settings.Repeat);
                return true;
            default:
                return false;
        }
    }

    private void AfterOpen(string path, IPlaybackEngine engine)
    {
        EnsureSettings();
        if (!IsAddress(path))
        {
            _recent.Add(path);
            _recent.Save(RecentFile());
        }
        _chapters = engine.ChapterTimes;
        _marksKey = "";
        _aspectIndex = 0;
        _rotation = 0;
        _mirrored = false;
        _notice = null;
        if (_settings.Repeat == RepeatMode.One) engine.SetRepeatFile(true);
        RefreshInfo();
    }

    private void AfterEnd()
    {
        EnsureSettings();
        if (_settings.Repeat != RepeatMode.All && !_settings.Shuffle) return;
        _trace.Add("autonext -> " + StepFile(true));
    }

    private string StepFile(bool forward)
    {
        EnsureSettings();
        if (_path is not { } path) return "no";
        var next = FolderNavigator.Step(path, forward, _settings.Repeat, _settings.Shuffle, _shuffleSeed);
        if (next is null)
        {
            _notice = Strings.Get("player.list.end");
            return "no";
        }

        _notice = null;
        _navigation = OpenQuietlyAsync(next);
        return Path.GetFileName(next);
    }

    internal bool OpenDropped(string file)
    {
        if (!File.Exists(file)) return false;
        _trace.Add("drop -> " + Path.GetFileName(file));
        _navigation = OpenQuietlyAsync(file);
        return true;
    }

    internal async Task OpenQuietlyAsync(string path)
    {
        try
        {
            await OpenAsync(path).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is PlaybackOpenException or PlaybackEngineUnavailableException or IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            TxtStall.IsVisible = true;
            TxtStall.Text = LanguageCatalog.Display(Strings.Get(MpvEngine.FailedKey) + ": " + ex.Message);
        }
    }

    private string TakeScreenshot()
    {
        if (_engine is not { IsOpen: true } engine || _path is not { } media) return "no";
        EnsureSettings();
        var target = _settings.ScreenshotPath(media, CurrentPosition());
        _lastShot = SaveScreenshotAsync(engine, target);
        return target;
    }

    private async Task<string?> SaveScreenshotAsync(IPlaybackEngine engine, string target)
    {
        var saved = false;
        try
        {
            var folder = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            saved = await engine.SaveScreenshotAsync(target).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
        }

        _notice = saved ? Strings.Get("player.view.screenshot-saved", target) : Strings.Get("player.view.screenshot-failed");
        RefreshState();
        return saved ? target : null;
    }

    private void EnsureSettings()
    {
        var folder = SettingsFolder();
        if (_settingsRead && string.Equals(folder, _settingsFolder, StringComparison.Ordinal)) return;
        _settingsRead = true;
        _settingsFolder = folder;
        _settings = PlayerSettings.Load(SettingsFile());
        _recent = RecentFiles.Load(RecentFile());
    }

    private string? SettingsFolder()
    {
        var history = HistoryPath?.Invoke();
        return string.IsNullOrEmpty(history) ? null : Path.GetDirectoryName(Path.GetFullPath(history));
    }

    private string? SettingsFile() => SettingsFolder() is { } folder ? Path.Combine(folder, PlayerSettings.FileName) : null;

    private string? RecentFile() => SettingsFolder() is { } folder ? Path.Combine(folder, RecentFiles.FileName) : null;

    private void AppendWindowMenu(MenuFlyout flyout)
    {
        EnsureSettings();
        foreach (var item in flyout.Items.OfType<MenuItem>())
        {
            if (item.Tag is not PlayerAction action) continue;
            bool? on = action.Command switch
            {
                PlayerCommandKind.ToggleTopmost => _topmost,
                PlayerCommandKind.Mirror => _mirrored,
                PlayerCommandKind.ToggleInfo => _infoVisible,
                PlayerCommandKind.ToggleShuffle => _settings.Shuffle,
                _ => null
            };
            if (on is not { } value) continue;
            item.ToggleType = MenuItemToggleType.CheckBox;
            item.IsChecked = value;
        }

        flyout.Items.Add(new Separator());
        var recent = new MenuItem { Header = Strings.Get("player.list.recent") };
        var files = _recent.Items;
        if (files.Count == 0) recent.Items.Add(new MenuItem { Header = Strings.Get("player.list.recent-empty"), IsEnabled = false });
        foreach (var path in files)
        {
            var entry = new MenuItem { Header = new TextBlock { Text = Path.GetFileName(path) }, Tag = path };
            ToolTip.SetTip(entry, path);
            entry.Click += OnRecentRow;
            recent.Items.Add(entry);
        }

        flyout.Items.Add(recent);
        var folder = new MenuItem { Header = Strings.Get("player.view.screenshot-folder") };
        folder.Click += OnPickScreenshotFolder;
        flyout.Items.Add(folder);
    }

    private void OnRecentRow(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string path }) OpenRecent(path);
    }

    internal bool OpenRecent(string path)
    {
        EnsureSettings();
        if (!File.Exists(path))
        {
            _recent.Remove(path);
            _recent.Save(RecentFile());
            _notice = Strings.Get("player.list.missing");
            RefreshState();
            return false;
        }

        _navigation = OpenQuietlyAsync(path);
        return true;
    }

    private async void OnPickScreenshotFolder(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanPickFolder: true } storage) return;
        try
        {
            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = Strings.Get("player.view.screenshot-folder")
            });
            if (folders.Count == 0 || folders[0].TryGetLocalPath() is not { } local) return;
            EnsureSettings();
            _settings.ScreenshotFolder = local;
            SaveSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
        }
    }

    private void OnFileDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DroppedFile(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFileDrop(object? sender, DragEventArgs e)
    {
        var file = DroppedFile(e);
        e.Handled = true;
        if (file is not null) OpenDropped(file);
    }

    private static string? DroppedFile(DragEventArgs e)
    {
        var items = e.DataTransfer.TryGetFiles()?.ToList();
        if (items is null || items.Count != 1 || items[0] is IStorageFolder) return null;
        var path = items[0].TryGetLocalPath();
        return path is not null && File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Basis denetim seridinden mi geliyor. Serit panonun ustunde duruyor ve panonun
    /// kendi tiklamasi oynatmayi duraklatiyor; seritteki dugmeye basmak o yolu
    /// tetiklemesin diye seridin butun icerigi ayriliyor. Zaman cubugu de seridin
    /// icinde, yine de adiyla ayrica soruluyor: cubuk seritten cikarsa kural kalir.
    /// </summary>
    private bool IsSeekBarSource(object? source)
        => source is Visual visual
           && (ReferenceEquals(visual, SeekBar) || SeekBar.IsVisualAncestorOf(visual)
               || ReferenceEquals(visual, StripBar) || StripBar.IsVisualAncestorOf(visual));

    private void OnSeekPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(SeekBar).Properties.IsLeftButtonPressed) return;
        _seekDragging = true;
        e.Pointer.Capture(SeekBar);
        SeekToPointer(e.GetPosition(SeekBar).X);
        e.Handled = true;
    }

    private void OnSeekMoved(object? sender, PointerEventArgs e)
    {
        if (!_seekDragging) return;
        SeekToPointer(e.GetPosition(SeekBar).X);
        e.Handled = true;
    }

    private void OnSeekReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_seekDragging) return;
        _seekDragging = false;
        e.Pointer.Capture(null);
        HideThumbnail();
        e.Handled = true;
    }

    internal void SeekToPointer(double x)
    {
        var duration = _seek.Duration;
        if (_engine is null || !double.IsFinite(duration) || duration <= 0) return;
        var at = SeekMarks.SecondsAt(x, SeekBar.Bounds.Width, duration);
        _trackPaused = false;
        _seek.GoTo(at);
        _trace.Add("seekbar -> " + at.ToString("0.###", CultureInfo.InvariantCulture));
        RefreshState();
    }

    private void RefreshWindowState()
    {
        if (TxtView is null) return;
        RefreshSeekBar();
        EnsureSettings();

        var parts = new List<string>();
        if (_aspectIndex > 0) parts.Add(Strings.Get("player.view.state-aspect", AspectRatios[_aspectIndex]));
        if (_rotation != 0) parts.Add(Strings.Get("player.view.state-rotate", _rotation));
        if (_mirrored) parts.Add(Strings.Get("player.view.state-mirror"));
        if (_topmost) parts.Add(Strings.Get("player.view.state-topmost"));
        if (_settings.Repeat == RepeatMode.All) parts.Add(Strings.Get("player.list.repeat-all"));
        if (_settings.Repeat == RepeatMode.One) parts.Add(Strings.Get("player.list.repeat-one"));
        if (_settings.Shuffle) parts.Add(Strings.Get("player.list.state-shuffle"));
        if (_notice is not null) parts.Add(_notice);

        TxtView.Text = LanguageCatalog.Display(string.Join(" - ", parts));
        TxtView.IsVisible = parts.Count > 0;
        RefreshSerit();
    }

    private void RefreshSeekBar()
    {
        if (SeekBar is null) return;
        var duration = _engine is null ? 0 : _seek.Duration;
        var known = double.IsFinite(duration) && duration > 0;
        var width = SeekBar.Bounds.Width;
        SeekTrack.Classes.Set("unknown", !known);
        SeekFill.IsVisible = known;
        if (known) SeekFill.Width = SeekMarks.Offset(_seek.Target, duration, width);

        var bookmarks = _path is null ? Array.Empty<double>() : _history.Bookmarks(_path);
        var key = string.Join(",", _chapters) + "|" + string.Join(",", bookmarks) + "|"
            + width.ToString("0.#", CultureInfo.InvariantCulture) + "|" + duration.ToString("0.###", CultureInfo.InvariantCulture);
        if (key == _marksKey) return;
        _marksKey = key;
        _marks = known ? SeekMarks.Build(_chapters, bookmarks, duration) : Array.Empty<SeekMark>();
        LayoutMarks(width, duration);
    }

    private void LayoutMarks(double width, double duration)
    {
        SeekMarkLayer.Children.Clear();
        if (_marks.Count == 0 || width <= 0) return;
        var markWidth = Resource("PlaybackCursorWidth");
        var markHeight = Resource("PlaybackSeekBarHeight");
        var chapterBrush = this.TryFindResource("TextBody", out var body) ? body as IBrush : null;
        var bookmarkBrush = this.TryFindResource("NeonPink", out var pink) ? pink as IBrush : null;

        foreach (var mark in _marks)
        {
            var tick = new Border
            {
                Width = markWidth,
                Height = markHeight,
                Background = mark.Chapter ? chapterBrush : bookmarkBrush,
                IsHitTestVisible = false,
                Tag = mark
            };
            Canvas.SetLeft(tick, SeekMarks.Offset(mark.Seconds, duration, width) - markWidth / 2);
            Canvas.SetTop(tick, 0);
            SeekMarkLayer.Children.Add(tick);
        }
    }

    private double Resource(string key)
        => this.TryFindResource(key, out var value) && value is double number ? number : 0;

    private void RefreshInfo()
    {
        if (TxtInfo is null || !_infoVisible) return;
        TxtInfo.Text = Describe(_engine?.Details);
    }

    internal static string Describe(MediaDetails? details)
    {
        if (details is null) return Strings.Get("player.info.none");
        var unknown = Strings.Get("player.info.unknown");
        var size = details.Width > 0 && details.Height > 0
            ? details.Width.ToString(CultureInfo.InvariantCulture) + "×" + details.Height.ToString(CultureInfo.InvariantCulture)
            : unknown;
        var fps = double.IsFinite(details.FramesPerSecond) && details.FramesPerSecond > 0
            ? details.FramesPerSecond.ToString("0.###", CultureInfo.CurrentCulture)
            : unknown;
        var rate = double.IsFinite(details.BitsPerSecond) && details.BitsPerSecond > 0
            ? (details.BitsPerSecond / 1000).ToString("0", CultureInfo.CurrentCulture)
            : unknown;

        var lines = new List<string>
        {
            Strings.Get("player.info.codec", details.VideoCodec ?? unknown),
            Strings.Get("player.info.resolution", size),
            Strings.Get("player.info.framerate", fps),
            Strings.Get("player.info.bitrate", rate),
            details.AudioCodec is null
                ? Strings.Get("player.info.noaudio")
                : Strings.Get("player.info.audio", details.AudioCodec, details.AudioChannels, details.AudioSampleRate)
        };
        return string.Join(Environment.NewLine, lines);
    }
}
