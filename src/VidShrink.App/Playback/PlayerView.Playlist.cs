using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.Core;

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

internal static class PlaylistFilter
{
    /// <summary>Liste penceresinin süzgeci: görünen adda, büyük-küçük harf ayırmadan, geçerli kültürle. Boş arama listeyi olduğu gibi bırakır.</summary>
    internal static IReadOnlyList<string> Match(IReadOnlyList<string> files, string? search, Func<string, string> name)
    {
        var text = search?.Trim() ?? "";
        if (text.Length == 0) return files;
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        return files.Where(file => compare.IndexOf(name(file), text, CompareOptions.IgnoreCase) >= 0).ToList();
    }
}

internal static class PlaylistRow
{
    /// <summary>Satırın sağındaki yazı: kalınan yer ve süre, ölçeği süreden alır. Geçmişte iz yoksa yazı yok.</summary>
    internal static string? Progress(double position, double duration)
    {
        var known = double.IsFinite(duration) && duration > 0;
        var resumed = double.IsFinite(position) && position >= PlaybackHistory.MinimumResumeSeconds;
        var scale = TimeSpan.FromSeconds(known ? duration : resumed ? position : 0);
        if (resumed && known) return Saat.Ekran(TimeSpan.FromSeconds(position), scale) + " / " + Saat.Ekran(scale);
        if (resumed) return Saat.Ekran(TimeSpan.FromSeconds(position), scale);
        return known ? Saat.Ekran(scale) : null;
    }
}

internal partial class PlayerView
{
    private PixelPoint? _menuPointer;

    private Action? _playlistDetach;

    internal bool PlaylistOpen => PlaylistPopup.IsOpen;

    internal ListBox Playlist => PlaylistList;

    internal Border PlaylistFrame => PlaylistPanel;

    internal TextBox PlaylistSearchBox => PlaylistSearch;

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
        PlaylistSearch.TextChanged += (_, _) =>
        {
            if (PlaylistPopup.IsOpen) FillPlaylist();
        };
    }

    private bool FillPlaylist()
    {
        var files = PlaylistFiles();
        PlaylistList.Items.Clear();
        if (files.Count == 0) return false;

        var shown = PlaylistFilter.Match(files, PlaylistSearch.Text, MediaName);
        foreach (var file in shown)
        {
            var name = MediaName(file);
            var item = new ListBoxItem { Tag = file, Content = PlaylistContent(file, name) };
            ToolTip.SetTip(item, name);
            PlaylistList.Items.Add(item);
        }

        PlaylistList.SelectedIndex = _path is { } current ? FolderNavigator.IndexOf(shown, current) : -1;
        return true;
    }

    private Control PlaylistContent(string file, string name)
    {
        var title = new TextBlock
        {
            Text = name,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };
        if (_history.Progress(file) is not { } progress || PlaylistRow.Progress(progress.Position, progress.Duration) is not { } text) return title;

        var time = new TextBlock { Text = text, TextWrapping = TextWrapping.NoWrap, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        if (this.TryFindResource("FontSizeSm", out var size) && size is double small) time.FontSize = small;
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        if (this.TryFindResource("SpaceSm", out var gap) && gap is double space) row.ColumnSpacing = space;
        Grid.SetColumn(time, 1);
        row.Children.Add(title);
        row.Children.Add(time);
        return row;
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
        PlaylistSearch.Text = "";
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
        var gap = (this.TryFindResource("PlaylistMenuGap", out var space) && space is double gapDip ? gapDip : 0) * top.RenderScaling;
        double? focusLeft = focused is { } target && TopLevel.GetTopLevel(target) is not null ? target.PointToScreen(new Point()).X : null;
        var anchorX = PlaylistPlacement.AnchorX(pointer?.X, focusLeft, menuOrigin.X);
        var probe = new PixelPoint((int)Math.Round(anchorX), menuOrigin.Y);
        var work = top.Screens?.ScreenFromPoint(probe)?.WorkingArea
                   ?? new PixelRect(menuOrigin.X - (int)listWidth, menuOrigin.Y, (int)(listWidth * 2 + menuWidth), (int)Math.Ceiling(listHeight + menuOrigin.Y));
        var x = PlaylistPlacement.ListX(anchorX, menuOrigin.X, menuWidth, busyLeft, busyRight, listWidth, work.X, work.Right, Math.Max(shadow, gap));
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
        CloseItemMenu();
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
        if (e.KeyModifiers == KeyModifiers.Alt && e.Key is Key.Up or Key.Down)
        {
            if (PlaylistList.SelectedItem is ListBoxItem { Tag: string moving })
            {
                if (e.Key == Key.Up ? MoveUp(moving) : MoveDown(moving)) SelectAfterMove(moving);
                e.Handled = true;
            }

            return;
        }

        if (e.Key != Key.Enter) return;
        if (PlaylistList.SelectedItem is not ListBoxItem item) return;
        PlayFromPlaylist(item);
        e.Handled = true;
    }

    private void SelectAfterMove(string file)
    {
        FillPlaylist();
        foreach (var entry in PlaylistList.Items)
        {
            if (entry is not ListBoxItem { Tag: string tag } row || !QueueEdit.Same(tag, file)) continue;
            PlaylistList.SelectedItem = row;
            PlaylistList.ScrollIntoView(row);
            row.Focus();
            return;
        }
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
