using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

    internal static double ListX(double anchorX, double menuLeft, double menuWidth, double listWidth, double screenLeft, double screenRight) =>
        ListX(anchorX, menuLeft, menuWidth, menuLeft, menuLeft + menuWidth, listWidth, screenLeft, screenRight, 0);

    internal static double ListX(double pointerX, double menuWidth, double listWidth, double screenLeft, double screenRight) =>
        ListX(pointerX, MenuLeft(pointerX, menuWidth, screenLeft, screenRight), menuWidth, listWidth, screenLeft, screenRight);

    internal static double ListX(double anchorX, double menuLeft, double menuWidth, double occupiedLeft, double occupiedRight,
        double listWidth, double screenLeft, double screenRight, double shadow)
    {
        var busyLeft = Math.Min(occupiedLeft, menuLeft);
        var busyRight = Math.Max(occupiedRight, menuLeft + menuWidth);
        var low = screenLeft + shadow;
        var high = Math.Max(low, screenRight - shadow - listWidth);
        var left = Math.Min(anchorX, busyLeft) - shadow - listWidth;
        var right = Math.Max(anchorX, busyRight) + shadow;
        var leftFits = left >= low;
        var rightFits = right <= high;

        double x;
        if (menuLeft + menuWidth / 2 >= anchorX)
            x = leftFits || !rightFits && busyLeft - screenLeft >= screenRight - busyRight ? left : right;
        else
            x = rightFits || !leftFits && screenRight - busyRight >= busyLeft - screenLeft ? right : left;

        return Math.Clamp(x, low, high);
    }

    internal static double ListY(double menuTop, double listHeight, double screenTop, double screenBottom, double shadow)
    {
        var low = screenTop + shadow;
        return Math.Max(low, Math.Min(menuTop, screenBottom - shadow - listHeight));
    }

    internal static double AnchorX(double? pointerX, double? focusedLeft, double menuLeft) => pointerX ?? focusedLeft ?? menuLeft;
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
        if (_path is not { } path || _queue is null && !File.Exists(path)) return Array.Empty<string>();
        EnsureSettings();
        return NavigationList(path);
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

    private Control? KeyboardMenuTarget()
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Control focused) return null;
        if (ReferenceEquals(focused, this) || !focused.IsEffectivelyVisible || !this.IsVisualAncestorOf(focused)) return null;
        return focused;
    }

    private void OpenPlaylist(MenuFlyout flyout, PixelPoint? pointer, Control? focused)
    {
        ClosePlaylist();
        if (!ReferenceEquals(_menu, flyout) || !flyout.IsOpen) return;
        if (flyout.Items.Count == 0 || flyout.Items[0] is not Visual first) return;
        if (first.FindAncestorOfType<MenuFlyoutPresenter>() is not { } presenter) return;
        if (TopLevel.GetTopLevel(presenter) is not { } menuRoot) return;
        if (!FillPlaylist()) return;
        if (!PlacePlaylist(flyout, presenter, menuRoot, pointer, focused)) return;

        PlaylistPopup.Open();
        _trace.Add("playlist " + PlaylistList.ItemCount);

        EventHandler relayout = (_, _) =>
        {
            if (PlaylistPopup.IsOpen) PlacePlaylist(flyout, presenter, menuRoot, pointer, focused);
        };
        EventHandler<PixelPointEventArgs> moved = (_, _) => relayout(null, EventArgs.Empty);
        var submenu = MenuItem.IsSubMenuOpenProperty.Changed.AddClassHandler<MenuItem>((_, _) =>
            Dispatcher.UIThread.Post(() => relayout(null, EventArgs.Empty), DispatcherPriority.Loaded));
        presenter.LayoutUpdated += relayout;
        if (menuRoot is WindowBase window) window.PositionChanged += moved;
        _playlistDetach = () =>
        {
            submenu.Dispose();
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

    private bool PlacePlaylist(MenuFlyout flyout, MenuFlyoutPresenter presenter, TopLevel menuRoot, PixelPoint? pointer, Control? focused)
    {
        if (TopLevel.GetTopLevel(this) is not { } top) return false;

        var offset = new Point();
        for (Visual? visual = presenter; visual is not null && !ReferenceEquals(visual, menuRoot); visual = visual.GetVisualParent())
            offset += visual.Bounds.Position;
        var menuOrigin = menuRoot.PointToScreen(offset);
        var menuWidth = presenter.Bounds.Width * menuRoot.RenderScaling;
        double busyLeft = menuOrigin.X, busyRight = menuOrigin.X + menuWidth;
        OpenSubmenuSpan(flyout.Items, ref busyLeft, ref busyRight);

        PlaylistPanel.Measure(Size.Infinity);
        var listWidth = PlaylistPanel.DesiredSize.Width * top.RenderScaling;
        var listHeight = PlaylistPanel.DesiredSize.Height * top.RenderScaling;
        var shadow = (this.TryFindResource("MenuShadowExtent", out var extent) && extent is double dip ? dip : 0) * top.RenderScaling;
        double? focusLeft = focused is { } target && TopLevel.GetTopLevel(target) is not null ? target.PointToScreen(new Point()).X : null;
        var anchorX = PlaylistPlacement.AnchorX(pointer?.X, focusLeft, menuOrigin.X);
        var probe = new PixelPoint((int)Math.Round(anchorX), menuOrigin.Y);
        var work = top.Screens?.ScreenFromPoint(probe)?.WorkingArea
                   ?? new PixelRect(menuOrigin.X - (int)listWidth, menuOrigin.Y, (int)(listWidth * 2 + menuWidth), (int)Math.Ceiling(listHeight + menuOrigin.Y));
        var x = PlaylistPlacement.ListX(anchorX, menuOrigin.X, menuWidth, busyLeft, busyRight, listWidth, work.X, work.Right, shadow);
        var y = PlaylistPlacement.ListY(menuOrigin.Y, listHeight, work.Y, work.Bottom, shadow);

        var local = this.PointToClient(new PixelPoint((int)Math.Round(x), (int)Math.Round(y)));
        PlaylistPopup.PlacementTarget = this;
        if (PlaylistPopup.HorizontalOffset != local.X) PlaylistPopup.HorizontalOffset = local.X;
        if (PlaylistPopup.VerticalOffset != local.Y) PlaylistPopup.VerticalOffset = local.Y;
        return true;
    }

    private static void OpenSubmenuSpan(ItemCollection items, ref double left, ref double right)
    {
        foreach (var item in items)
        {
            if (item is not MenuItem { IsSubMenuOpen: true } open) continue;
            foreach (var child in open.Items)
            {
                if (child is not Visual visual || TopLevel.GetTopLevel(visual) is not PopupRoot root) continue;
                var origin = root.PointToScreen(new Point());
                left = Math.Min(left, origin.X);
                right = Math.Max(right, origin.X + root.ClientSize.Width * root.RenderScaling);
                break;
            }

            OpenSubmenuSpan(open.Items, ref left, ref right);
        }
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
        if (!File.Exists(file) && !IsAddress(file)) return;
        _trace.Add("playlist -> " + Path.GetFileName(file));
        _navigation = OpenQuietlyAsync(file);
    }
}
