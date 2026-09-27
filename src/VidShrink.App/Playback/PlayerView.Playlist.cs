using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace VidShrink.App.Playback;

internal static class PlaylistPlacement
{
    internal static double MenuLeft(double pointerX, double menuWidth, double screenLeft, double screenRight) =>
        pointerX + menuWidth <= screenRight ? pointerX : Math.Max(screenLeft, pointerX - menuWidth);

    internal static double ListX(double anchorX, double menuLeft, double menuWidth, double listWidth, double screenLeft, double screenRight)
    {
        double x;
        if (menuLeft + menuWidth / 2 >= anchorX)
        {
            x = anchorX - listWidth;
            if (x < screenLeft) x = menuLeft + menuWidth;
        }
        else
        {
            x = anchorX;
            if (x + listWidth > screenRight) x = menuLeft - listWidth;
        }

        return Math.Clamp(x, screenLeft, Math.Max(screenLeft, screenRight - listWidth));
    }

    internal static double ListX(double pointerX, double menuWidth, double listWidth, double screenLeft, double screenRight) =>
        ListX(pointerX, MenuLeft(pointerX, menuWidth, screenLeft, screenRight), menuWidth, listWidth, screenLeft, screenRight);
}

internal partial class PlayerView
{
    private PixelPoint? _menuPointer;

    private Action? _playlistDetach;

    internal bool PlaylistOpen => PlaylistPopup.IsOpen;

    internal ListBox Playlist => PlaylistList;

    internal Border PlaylistFrame => PlaylistPanel;

    internal PixelPoint? MenuPointer => _menuPointer;

    internal PixelRect PlaylistScreenBounds
    {
        get
        {
            if (!PlaylistPopup.IsOpen || TopLevel.GetTopLevel(PlaylistPanel) is not { } root) return default;
            var origin = root.PointToScreen(new Point(0, 0));
            return new PixelRect(origin, PixelSize.FromSize(root.ClientSize, root.RenderScaling));
        }
    }

    internal IReadOnlyList<string> PlaylistFiles()
    {
        if (_path is not { } path || !File.Exists(path)) return Array.Empty<string>();
        EnsureSettings();
        return FolderNavigator.Order(FolderNavigator.Siblings(path), _settings.Shuffle, _shuffleSeed);
    }

    private void InitPlaylist()
    {
        PlaylistPopup.PlacementTarget = this;
        PlaylistList.AddHandler(PointerReleasedEvent, OnPlaylistReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        PlaylistList.AddHandler(KeyDownEvent, OnPlaylistKey, RoutingStrategies.Tunnel);
    }

    private bool FillPlaylist()
    {
        var files = PlaylistFiles();
        PlaylistList.Items.Clear();
        if (files.Count == 0) return false;

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var item = new ListBoxItem
            {
                Tag = file,
                Content = new TextBlock
                {
                    Text = name,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap,
                },
            };
            ToolTip.SetTip(item, name);
            PlaylistList.Items.Add(item);
        }

        PlaylistList.SelectedIndex = _path is { } current ? FolderNavigator.IndexOf(files, current) : -1;
        return true;
    }

    private void OpenPlaylist(MenuFlyout flyout, PixelPoint? pointer)
    {
        ClosePlaylist();
        if (!ReferenceEquals(_menu, flyout) || !flyout.IsOpen) return;
        if (flyout.Items.Count == 0 || flyout.Items[0] is not Visual first) return;
        if (first.FindAncestorOfType<MenuFlyoutPresenter>() is not { } presenter) return;
        if (TopLevel.GetTopLevel(presenter) is not { } menuRoot) return;
        if (!FillPlaylist()) return;
        if (!PlacePlaylist(presenter, menuRoot, pointer)) return;

        PlaylistPopup.Open();
        _trace.Add("playlist " + PlaylistList.ItemCount);

        EventHandler relayout = (_, _) =>
        {
            if (PlaylistPopup.IsOpen) PlacePlaylist(presenter, menuRoot, pointer);
        };
        EventHandler<PixelPointEventArgs> moved = (_, _) => relayout(null, EventArgs.Empty);
        presenter.LayoutUpdated += relayout;
        if (menuRoot is WindowBase window) window.PositionChanged += moved;
        _playlistDetach = () =>
        {
            presenter.LayoutUpdated -= relayout;
            if (menuRoot is WindowBase owner) owner.PositionChanged -= moved;
        };

        var selected = PlaylistList.SelectedIndex;
        if (selected < 0) return;
        PlaylistList.ScrollIntoView(selected);
        Dispatcher.UIThread.Post(() =>
        {
            if (PlaylistPopup.IsOpen && PlaylistList.SelectedIndex == selected) PlaylistList.ScrollIntoView(selected);
        }, DispatcherPriority.Loaded);
    }

    private bool PlacePlaylist(MenuFlyoutPresenter presenter, TopLevel menuRoot, PixelPoint? pointer)
    {
        if (TopLevel.GetTopLevel(this) is not { } top) return false;

        var offset = new Point();
        for (Visual? visual = presenter; visual is not null && !ReferenceEquals(visual, menuRoot); visual = visual.GetVisualParent())
            offset += visual.Bounds.Position;
        var menuOrigin = menuRoot.PointToScreen(offset);
        var menuWidth = presenter.Bounds.Width * menuRoot.RenderScaling;

        PlaylistPanel.Measure(Size.Infinity);
        var listWidth = PlaylistPanel.DesiredSize.Width * top.RenderScaling;
        var anchorX = pointer?.X ?? menuOrigin.X;
        var work = top.Screens?.ScreenFromPoint(new PixelPoint(anchorX, menuOrigin.Y))?.WorkingArea
                   ?? new PixelRect(menuOrigin.X - (int)listWidth, menuOrigin.Y, (int)(listWidth * 2 + menuWidth), 1);
        var x = PlaylistPlacement.ListX(anchorX, menuOrigin.X, menuWidth, listWidth, work.X, work.Right);

        var local = this.PointToClient(new PixelPoint((int)Math.Round(x), menuOrigin.Y));
        PlaylistPopup.PlacementTarget = this;
        if (PlaylistPopup.HorizontalOffset != local.X) PlaylistPopup.HorizontalOffset = local.X;
        if (PlaylistPopup.VerticalOffset != local.Y) PlaylistPopup.VerticalOffset = local.Y;
        return true;
    }

    private void ClosePlaylist()
    {
        _playlistDetach?.Invoke();
        _playlistDetach = null;
        if (PlaylistPopup.IsOpen) PlaylistPopup.Close();
    }

    private void OnPlaylistReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left) return;
        if (e.Source is not Visual source) return;
        var item = source as ListBoxItem ?? source.FindAncestorOfType<ListBoxItem>();
        if (item is null) return;
        PlayFromPlaylist(item);
        e.Handled = true;
    }

    private void OnPlaylistKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (PlaylistList.SelectedItem is not ListBoxItem item) return;
        PlayFromPlaylist(item);
        e.Handled = true;
    }

    private void PlayFromPlaylist(ListBoxItem item)
    {
        if (item.Tag is not string file) return;
        var menu = _menu;
        _menu = null;
        menu?.Hide();
        ClosePlaylist();
        if (!File.Exists(file)) return;
        _trace.Add("playlist -> " + Path.GetFileName(file));
        _navigation = OpenQuietlyAsync(file);
    }
}
