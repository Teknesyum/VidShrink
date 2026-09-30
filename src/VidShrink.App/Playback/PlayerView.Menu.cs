using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using VidShrink.App.Localization;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    internal Action<ProcessStartInfo> RevealLauncher { get; set; } = start => Process.Start(start)?.Dispose();

    internal MenuFlyout BuildMenu()
    {
        var flyout = new MenuFlyout();
        foreach (var action in Keymap.MenuTop) flyout.Items.Add(ActionRow(action));
        flyout.Items.Add(RevealRow());
        AddEditRow(flyout);
        flyout.Items.Add(new Separator());
        flyout.Items.Add(AudioMenu());
        flyout.Items.Add(SubtitleMenu());
        flyout.Items.Add(ActionMenu("player.menu.view", Keymap.ViewMenu));
        flyout.Items.Add(ActionMenu("player.menu.playback", Keymap.PlaybackMenu));
        flyout.Items.Add(ActionMenu("player.menu.loop", Keymap.LoopMenu));
        flyout.Items.Add(new Separator());
        var shot = ActionRow(Keymap.Screenshot);
        shot.IsEnabled = !AudioOnly;
        flyout.Items.Add(shot);
        flyout.Items.Add(ToolsMenu());
        flyout.Items.Add(RecentMenu());
        flyout.Items.Add(SettingsMenu());
        return flyout;
    }

    private MenuItem SettingsMenu()
    {
        var item = new MenuItem { Header = Strings.Get(Keymap.Settings.LabelKey), Tag = Keymap.Settings };
        foreach (var child in SettingsItems()) item.Items.Add(child);
        return item;
    }

    private MenuItem ActionMenu(string headerKey, IReadOnlyList<PlayerAction?> rows)
        => Submenu(Strings.Get(headerKey), rows.Select(action => action is null ? (Control)new Separator() : ActionRow(action)).ToList());

    private MenuItem ActionRow(PlayerAction action)
    {
        var item = new MenuItem { Header = Strings.Get(action.LabelKey), Tag = action };
        if (Keymap.FirstKeyRow(action) is { } row)
        {
            item.InputGesture = new KeyGesture(row.Input.Key, row.Input.Modifiers);
            ToolTip.SetTip(item, Strings.Get(action.LabelKey) + " (" + Keymap.Gesture(row.Input) + ")");
        }

        if (ToggleState(action) is { } on)
        {
            item.ToggleType = MenuItemToggleType.CheckBox;
            item.IsChecked = on;
        }

        item.Click += OnMenuRow;
        return item;
    }

    private MenuItem RevealRow()
    {
        var item = new MenuItem { Header = Strings.Get("player.menu.reveal"), IsEnabled = RevealTarget() is not null };
        item.Click += (_, _) => RevealFile();
        return item;
    }

    private string? RevealTarget() => _path is { } path && File.Exists(path) ? Path.GetFullPath(path) : null;

    internal bool RevealFile() => RevealTarget() is { } path && RevealPath(path);

    internal bool RevealPath(string path)
    {
        if (!File.Exists(path)) return false;
        path = Path.GetFullPath(path);
        try
        {
            RevealLauncher(RevealStart(path, OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()));
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }
    }

    internal static ProcessStartInfo RevealStart(string path, bool windows, bool mac)
    {
        var start = new ProcessStartInfo { UseShellExecute = false };
        if (windows)
        {
            start.FileName = "explorer.exe";
            start.ArgumentList.Add("/select,");
            start.ArgumentList.Add(path);
            return start;
        }

        start.FileName = mac ? "open" : "xdg-open";
        start.ArgumentList.Add(Path.GetDirectoryName(path) ?? path);
        return start;
    }
}
