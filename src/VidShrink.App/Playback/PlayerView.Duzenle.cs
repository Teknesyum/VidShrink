using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using VidShrink.Core;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    private Func<string, double, Task>? _editRequested;

    internal Func<string, double, Task>? EditRequested
    {
        get => _editRequested;
        set
        {
            _editRequested = value;
            RefreshEditButton();
        }
    }

    internal string? EditTarget => _path is { } path && File.Exists(path) ? Path.GetFullPath(path) : null;

    private bool ApplyEdit(PlayerCommand command)
    {
        if (command.Kind != PlayerCommandKind.Edit) return false;
        if (EditRequested is not { } edit || EditTarget is not { } path)
        {
            _trace.Add("edit -> none");
            return true;
        }

        var at = CurrentPosition();
        _trace.Add("edit -> " + Path.GetFileName(path) + " @ " + Saat.Tani.Konum(at));
        _ = edit(path, at);
        return true;
    }

    private void AddEditRow(MenuFlyout flyout)
    {
        if (EditRequested is null) return;
        var row = ActionRow(Keymap.Edit);
        row.IsEnabled = EditTarget is not null;
        flyout.Items.Add(row);
    }

    private void RefreshEditButton()
    {
        BtnSeritEdit.IsVisible = EditRequested is not null;
        BtnSeritEdit.IsEnabled = _path is not null;
    }
}
