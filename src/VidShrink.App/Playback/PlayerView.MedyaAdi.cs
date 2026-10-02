using System;
using System.IO;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    private const string TitleFilled = "dolu";
    private const string TitleMenuClass = "menude";

    private MenuFlyout? _titleMenu;

    internal event Action? MediaTitleMenuChanged;

    internal string? MediaTitleText => MediaTitle.Classes.Contains(TitleFilled) ? TxtMediaTitle.Text : null;

    internal bool MediaTitleShown => MediaTitle.IsEffectivelyVisible;

    internal bool MediaTitleMenuOpen => _titleMenu?.IsOpen ?? false;

    internal MenuFlyout? MediaTitleMenu => _titleMenu;

    private void ShowMediaTitle(string path)
    {
        var name = Path.GetFileName(path);
        TxtMediaTitle.Text = name;
        ToolTip.SetTip(MediaTitle, path);
        AutomationProperties.SetName(MediaTitle, name);
        MediaTitle.Classes.Set(TitleFilled, name.Length > 0);
    }

    private void HideMediaTitle()
    {
        CloseTitleMenu();
        MediaTitle.Classes.Remove(TitleFilled);
        TxtMediaTitle.Text = null;
        ToolTip.SetTip(MediaTitle, null);
        AutomationProperties.SetName(MediaTitle, null);
    }

    internal bool OverMediaTitle(PointerEventArgs e)
        => MediaTitle.IsEffectivelyVisible && new Rect(MediaTitle.Bounds.Size).Contains(e.GetPosition(MediaTitle));

    private bool InMediaTitle(object? source)
        => source is Visual visual && (ReferenceEquals(visual, MediaTitle) || MediaTitle.IsVisualAncestorOf(visual));

    private bool TitlePress(PointerPressedEventArgs e, PlayerButton button)
    {
        if (!InMediaTitle(e.Source)) return false;
        if (button == PlayerButton.Left) return true;
        if (button != PlayerButton.Right) return false;
        OpenTitleMenu();
        return true;
    }

    internal MenuFlyout BuildTitleMenu(string path)
    {
        var flyout = new MenuFlyout();
        flyout.Items.Add(ItemRow("copy-name", "player.title.copy-name", true, () => _ = PathCopier(TopLevel.GetTopLevel(this), Path.GetFileName(path))));
        flyout.Items.Add(ItemRow("copy-path", "player.list.item.copy-path", true, () => _ = PathCopier(TopLevel.GetTopLevel(this), Path.GetFullPath(path))));
        flyout.Items.Add(new Separator());
        flyout.Items.Add(RevealRow());
        flyout.Items.Add(ActionRow(Keymap.Info));
        return flyout;
    }

    internal void OpenTitleMenu()
    {
        if (_path is not { } path || !MediaTitle.IsEffectivelyVisible) return;
        CloseMenus();
        var flyout = BuildTitleMenu(path);
        _titleMenu = flyout;
        _itemMenu = flyout;
        flyout.Opened += (_, _) => TitleMenuState(flyout, true);
        flyout.Closed += (_, _) => TitleMenuState(flyout, false);
        try
        {
            flyout.ShowAt(MediaTitle, true);
            _trace.Add("titlemenu " + Path.GetFileName(path));
        }
        catch (InvalidOperationException)
        {
            _titleMenu = null;
            _itemMenu = null;
        }
    }

    private void TitleMenuState(MenuFlyout flyout, bool open)
    {
        if (!ReferenceEquals(_titleMenu, flyout)) return;
        MediaTitle.Classes.Set(TitleMenuClass, open);
        MediaTitleMenuChanged?.Invoke();
    }

    private void CloseTitleMenu()
    {
        var menu = _titleMenu;
        if (menu is null) return;
        if (ReferenceEquals(_itemMenu, menu)) _itemMenu = null;
        menu.Hide();
        _titleMenu = null;
        MediaTitle.Classes.Remove(TitleMenuClass);
        MediaTitleMenuChanged?.Invoke();
    }
}
