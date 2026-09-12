using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using VidShrink.App.Localization;
using VidShrink.Player;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    private readonly SubtitleOptions _subtitles = new();
    private string? _trackNotice;

    internal SubtitleOptions Subtitles => _subtitles;

    internal double SubtitleDelay => _subtitles.SubtitleDelay;

    internal double AudioDelay => _subtitles.AudioDelay;

    internal string? TrackNotice => _trackNotice;

    private void InitTracks()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnSubtitleDragOver);
        AddHandler(DragDrop.DropEvent, OnSubtitleDrop);
    }

    private void ApplyTrackOptions(IPlaybackEngine engine)
    {
        _subtitles.ResetDelays();
        _trackNotice = null;
        _subtitles.ApplyTo(engine);
    }

    private void CycleAudio()
    {
        var ids = TrackIds(PlaybackTrackKind.Audio);
        if (_engine is not { IsOpen: true } engine || ids.Count == 0)
        {
            _trace.Add("audio -> no");
            return;
        }

        var next = SubtitleOptions.Next(ids, engine.AudioTrack, false);
        engine.SetAudioTrack(next);
        _trace.Add("audio -> " + next.ToString(CultureInfo.InvariantCulture));
    }

    private void CycleSubtitle()
    {
        if (_engine is not { IsOpen: true } engine)
        {
            _trace.Add("subtitle -> no");
            return;
        }

        var next = SubtitleOptions.Next(TrackIds(PlaybackTrackKind.Subtitle), engine.SubtitleTrack, true);
        engine.SetSubtitleTrack(next);
        _trace.Add("subtitle -> " + SubtitleName(next));
    }

    private void ShiftSubtitleDelay(double seconds)
    {
        _subtitles.ShiftSubtitleDelay(seconds);
        _engine?.SetSubtitleDelay(_subtitles.SubtitleDelay);
        _trace.Add("subdelay " + seconds.ToString("0.###") + " -> " + _subtitles.SubtitleDelay.ToString("0.###"));
    }

    private void ShiftAudioDelay(double seconds)
    {
        _subtitles.ShiftAudioDelay(seconds);
        _engine?.SetAudioDelay(_subtitles.AudioDelay);
        _trace.Add("audiodelay " + seconds.ToString("0.###") + " -> " + _subtitles.AudioDelay.ToString("0.###"));
    }

    internal void SelectAudio(long id)
    {
        _engine?.SetAudioTrack(id);
        _trace.Add("audio -> " + id.ToString(CultureInfo.InvariantCulture));
        RefreshState();
    }

    internal void SelectSubtitle(long id)
    {
        _engine?.SetSubtitleTrack(id);
        _trace.Add("subtitle -> " + SubtitleName(id));
        RefreshState();
    }

    internal void ResizeSubtitle(double steps)
    {
        _subtitles.Grow(steps);
        _engine?.SetSubtitleScale(_subtitles.Scale);
        _trace.Add("subscale -> " + _subtitles.Scale.ToString("0.##", CultureInfo.InvariantCulture));
        RefreshState();
    }

    internal void MoveSubtitle(double steps)
    {
        _subtitles.Move(steps);
        _engine?.SetSubtitlePosition(_subtitles.Position);
        _trace.Add("subpos -> " + _subtitles.Position.ToString("0", CultureInfo.InvariantCulture));
        RefreshState();
    }

    internal void ResetSubtitleLook()
    {
        _subtitles.ResetLook();
        _engine?.SetSubtitleScale(_subtitles.Scale);
        _engine?.SetSubtitlePosition(_subtitles.Position);
        _trace.Add("sublook -> " + _subtitles.Scale.ToString("0.##", CultureInfo.InvariantCulture));
        RefreshState();
    }

    internal void UseCodepage(string value)
    {
        _subtitles.UseCodepage(value);
        _engine?.SetSubtitleCodepage(_subtitles.Codepage);
        _trace.Add("subcodepage -> " + _subtitles.Codepage);
        RefreshState();
    }

    internal void ResetSubtitleDelay()
    {
        _subtitles.ResetSubtitleDelay();
        _engine?.SetSubtitleDelay(0);
        _trace.Add("subdelay -> 0");
        RefreshState();
    }

    internal void ResetAudioDelay()
    {
        _subtitles.ResetAudioDelay();
        _engine?.SetAudioDelay(0);
        _trace.Add("audiodelay -> 0");
        RefreshState();
    }

    internal bool LoadSubtitle(string path)
    {
        if (_engine is not { IsOpen: true } engine)
        {
            _trackNotice = "player.subtitle.novideo";
            _trace.Add("subadd -> no");
            RefreshState();
            return false;
        }

        var added = engine.AddSubtitle(path);
        _trackNotice = added ? null : "player.subtitle.loadfailed";
        _trace.Add("subadd -> " + (added ? SubtitleName(engine.SubtitleTrack) : "no"));
        RefreshState();
        return added;
    }

    internal async Task PickSubtitleAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.Get("player.subtitle.load"),
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(Strings.Get("player.subtitle.files"))
                {
                    Patterns = SubtitleOptions.Extensions.Select(extension => "*." + extension).ToArray()
                },
                FilePickerFileTypes.All
            }
        });

        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (path is not null) LoadSubtitle(path);
    }

    private List<long> TrackIds(PlaybackTrackKind kind)
        => _engine?.Tracks.Where(track => track.Kind == kind).Select(track => track.Id).ToList() ?? new List<long>();

    private static string SubtitleName(long id) => id > 0 ? id.ToString(CultureInfo.InvariantCulture) : "off";

    private static string? DroppedSubtitle(DragEventArgs e)
    {
        var items = e.DataTransfer.TryGetFiles()?.ToList();
        if (items is null || items.Count != 1 || items[0] is IStorageFolder) return null;
        var path = items[0].TryGetLocalPath();
        return SubtitleOptions.IsSubtitleFile(path) && System.IO.File.Exists(path) ? path : null;
    }

    private void OnSubtitleDragOver(object? sender, DragEventArgs e)
    {
        if (DroppedSubtitle(e) is null) return;
        e.DragEffects = _engine is { IsOpen: true } ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnSubtitleDrop(object? sender, DragEventArgs e)
    {
        if (DroppedSubtitle(e) is not { } path) return;
        e.Handled = true;
        LoadSubtitle(path);
    }

    private void AppendTrackState(List<string> parts)
    {
        if (_subtitles.SubtitleDelay != 0) parts.Add(Strings.Get("player.subtitle.delay", SubtitleOptions.Signed(_subtitles.SubtitleDelay)));
        if (_subtitles.AudioDelay != 0) parts.Add(Strings.Get("player.tracks.delay", SubtitleOptions.Signed(_subtitles.AudioDelay)));
        if (_trackNotice is { } notice) parts.Add(Strings.Get(notice));
    }

    private void AddTrackMenus(MenuFlyout flyout)
    {
        var at = flyout.Items.Count;
        for (var i = 0; i < flyout.Items.Count; i++)
        {
            if (flyout.Items[i] is MenuItem { Tag: PlayerAction action } && ReferenceEquals(action, Keymap.Mute))
            {
                at = i + 1;
                break;
            }
        }

        flyout.Items.Insert(at, new Separator());
        flyout.Items.Insert(at + 1, Submenu(Strings.Get("player.tracks.audio"), BuildAudioItems()));
        flyout.Items.Insert(at + 2, Submenu(Strings.Get("player.subtitle.menu"), BuildSubtitleItems()));
    }

    internal List<Control> BuildAudioItems()
    {
        var items = new List<Control>();
        var tracks = _engine?.Tracks.Where(track => track.Kind == PlaybackTrackKind.Audio).ToList() ?? new List<PlaybackTrack>();
        var current = _engine?.AudioTrack ?? 0;
        if (tracks.Count == 0) items.Add(new MenuItem { Header = Strings.Get("player.tracks.none"), IsEnabled = false });
        foreach (var track in tracks)
        {
            var id = track.Id;
            items.Add(Choice(TrackName(track), id == current, () => SelectAudio(id)));
        }

        items.Add(new Separator());
        items.Add(Bound(SubtitleOptions.AudioCycle));
        items.Add(Bound(SubtitleOptions.AudioLater));
        items.Add(Bound(SubtitleOptions.AudioEarlier));
        items.Add(Plain(Strings.Get("player.tracks.delayreset"), ResetAudioDelay));
        return items;
    }

    internal List<Control> BuildSubtitleItems()
    {
        var items = new List<Control>();
        var tracks = _engine?.Tracks.Where(track => track.Kind == PlaybackTrackKind.Subtitle).ToList() ?? new List<PlaybackTrack>();
        var current = _engine?.SubtitleTrack ?? 0;
        items.Add(Choice(Strings.Get("player.subtitle.off"), current == 0, () => SelectSubtitle(0)));
        foreach (var track in tracks)
        {
            var id = track.Id;
            items.Add(Choice(TrackName(track), id == current, () => SelectSubtitle(id)));
        }

        items.Add(new Separator());
        items.Add(Plain(Strings.Get("player.subtitle.load"), () => _ = PickSubtitleAsync()));
        items.Add(Bound(SubtitleOptions.SubtitleCycle));
        items.Add(new Separator());
        items.Add(Bound(SubtitleOptions.SubtitleLater));
        items.Add(Bound(SubtitleOptions.SubtitleEarlier));
        items.Add(Plain(Strings.Get("player.subtitle.delayreset"), ResetSubtitleDelay));
        items.Add(new Separator());
        items.Add(Submenu(Strings.Get("player.subtitle.size", (_subtitles.Scale * 100).ToString("0", CultureInfo.CurrentCulture)), new List<Control>
        {
            Plain(Strings.Get("player.subtitle.larger"), () => ResizeSubtitle(1)),
            Plain(Strings.Get("player.subtitle.smaller"), () => ResizeSubtitle(-1))
        }));
        items.Add(Submenu(Strings.Get("player.subtitle.position", _subtitles.Position.ToString("0", CultureInfo.CurrentCulture)), new List<Control>
        {
            Plain(Strings.Get("player.subtitle.up"), () => MoveSubtitle(-1)),
            Plain(Strings.Get("player.subtitle.down"), () => MoveSubtitle(1))
        }));
        items.Add(Plain(Strings.Get("player.subtitle.reset"), ResetSubtitleLook));
        items.Add(Submenu(Strings.Get("player.subtitle.encoding"), SubtitleOptions.Codepages
            .Select(codepage => (Control)Choice(CodepageName(codepage), codepage.Value == _subtitles.Codepage, () => UseCodepage(codepage.Value)))
            .ToList()));
        return items;
    }

    internal static string TrackName(PlaybackTrack track)
    {
        var name = Strings.Get("player.tracks.item", track.Id);
        if (!string.IsNullOrWhiteSpace(track.Title)) name += " · " + track.Title;
        if (!string.IsNullOrWhiteSpace(track.Language)) name += " [" + track.Language + "]";
        if (track.External) name += Strings.Get("player.subtitle.external");
        return name;
    }

    internal static string CodepageName(SubtitleCodepage codepage)
        => codepage.Name.Length == 0 ? Strings.Get(codepage.LabelKey) : Strings.Get(codepage.LabelKey) + " (" + codepage.Name + ")";

    private static MenuItem Submenu(string header, List<Control> children)
    {
        var menu = new MenuItem { Header = header };
        foreach (var child in children) menu.Items.Add(child);
        return menu;
    }

    private static MenuItem Plain(string header, Action act)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => act();
        return item;
    }

    private static MenuItem Choice(string header, bool selected, Action act)
    {
        var item = new MenuItem { Header = header, ToggleType = MenuItemToggleType.Radio, IsChecked = selected };
        item.Click += (_, _) => act();
        return item;
    }

    private MenuItem Bound(PlayerAction action)
    {
        var row = Keymap.Rows.First(r => ReferenceEquals(r.Action, action));
        var item = new MenuItem { Header = Keymap.Label(row), Tag = action };
        if (row.Input.Symbol is null) item.InputGesture = new KeyGesture(row.Input.Key, row.Input.Modifiers);
        item.Click += OnTrackRow;
        return item;
    }

    private void OnTrackRow(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: PlayerAction action })
            Apply(action.ToCommand());
    }
}
