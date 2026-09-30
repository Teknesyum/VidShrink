using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using VidShrink.App.Localization;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    private MenuFlyout? _itemMenu;

    internal bool ItemMenuOpen => _itemMenu?.IsOpen ?? false;

    internal MenuFlyout? ItemMenu => _itemMenu;

    internal Func<TopLevel?, string, Task> PathCopier { get; set; } =
        (top, text) => top?.Clipboard is { } clipboard ? clipboard.SetTextAsync(text) : Task.CompletedTask;

    internal MenuFlyout BuildItemMenu(string file)
    {
        var current = _path is { } playing && QueueEdit.Same(file, playing);
        var flyout = new MenuFlyout();
        flyout.Items.Add(ItemRow("play", "player.list.item.play", true, () => PlayFile(file)));
        flyout.Items.Add(new Separator());
        flyout.Items.Add(ItemRow("enqueue", "player.list.item.enqueue", !current, () => AfterEdit(Enqueue(file))));
        flyout.Items.Add(ItemRow("play-next", "player.list.item.play-next", !current, () => AfterEdit(PlayNext(file))));
        flyout.Items.Add(new Separator());
        flyout.Items.Add(ItemRow("reveal", "player.menu.reveal", File.Exists(file), () => RevealPath(file)));
        flyout.Items.Add(ItemRow("copy-path", "player.list.item.copy-path", true, () => _ = PathCopier(TopLevel.GetTopLevel(this), file)));
        flyout.Items.Add(new Separator());
        flyout.Items.Add(ItemRow("remove", "player.list.item.remove", !current, () => AfterEdit(RemoveFromList(file))));
        return flyout;
    }

    private MenuItem ItemRow(string tag, string key, bool enabled, Action run)
    {
        var item = new MenuItem { Header = Strings.Get(key), Tag = tag, IsEnabled = enabled };
        item.Click += (_, _) =>
        {
            CloseItemMenu();
            run();
        };
        return item;
    }

    private void AfterEdit(bool changed)
    {
        if (changed && PlaylistPopup.IsOpen) FillPlaylist();
    }

    private void OpenItemMenu(ListBoxItem row)
    {
        if (row.Tag is not string file) return;
        CloseItemMenu();
        PlaylistList.SelectedItem = row;
        var flyout = BuildItemMenu(file);
        _itemMenu = flyout;
        try
        {
            flyout.ShowAt(row, true);
            _trace.Add("itemmenu " + Path.GetFileName(file));
        }
        catch (InvalidOperationException)
        {
            _itemMenu = null;
        }
    }

    private void CloseItemMenu()
    {
        var menu = _itemMenu;
        _itemMenu = null;
        menu?.Hide();
    }

    private void CloseMenus()
    {
        CloseItemMenu();
        var menu = _menu;
        _menu = null;
        menu?.Hide();
        ClosePlaylist();
    }

    private ListBoxItem? PlaylistRowAt(object? source)
    {
        if (!PlaylistPopup.IsOpen || source is not Visual visual || !PlaylistList.IsVisualAncestorOf(visual)) return null;
        return visual as ListBoxItem ?? visual.FindAncestorOfType<ListBoxItem>();
    }

    private bool InPlaylist(object? source) => PlaylistPopup.IsOpen && source is Visual visual && PlaylistPanel.IsVisualAncestorOf(visual);

    private bool OwnRoot(object? source)
        => source is Visual visual && ReferenceEquals(TopLevel.GetTopLevel(visual), TopLevel.GetTopLevel(this));

    private bool MenuPress(PointerPressedEventArgs e, PlayerButton button)
    {
        if (ItemMenuOpen && (button == PlayerButton.Right || InPlaylist(e.Source)))
        {
            CloseItemMenu();
            _trace.Add("itemmenu close");
            return true;
        }

        if (button != PlayerButton.Right) return false;

        if (PlaylistRowAt(e.Source) is { } row)
        {
            OpenItemMenu(row);
            return true;
        }

        if (MenuOpen || !OwnRoot(e.Source))
        {
            CloseMenus();
            _trace.Add("menu close");
            return true;
        }

        return false;
    }

    private void PlayFile(string file)
    {
        foreach (var entry in PlaylistList.Items)
        {
            if (entry is ListBoxItem { Tag: string tag } row && QueueEdit.Same(tag, file))
            {
                PlayFromPlaylist(row);
                return;
            }
        }
    }
}
