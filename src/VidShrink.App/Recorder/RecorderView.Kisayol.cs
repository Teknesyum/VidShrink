using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    private IGlobalHotkeys? _globalHotkeys;

    internal IGlobalHotkeys GlobalHotkeys
    {
        get => _globalHotkeys ??= OperatingSystem.IsWindows()
            && Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
                ? new Win32GlobalHotkeys()
                : new NoGlobalHotkeys();
        set => _globalHotkeys = value;
    }

    internal IReadOnlyList<HotkeyBinding> HotkeyConflicts { get; private set; } = [];

    internal bool HotkeysActive { get; private set; }

    internal void ActivateHotkeys()
    {
        if (HotkeysActive) return;
        HotkeysActive = true;
        HotkeyConflicts = GlobalHotkeys.Register(RecorderHotkeys.All, action => _ = RunHotkeyAsync(action));
        if (HotkeyConflicts.Count > 0) ShowError(Say("recorder.hotkey.taken", RecorderHotkeys.Names(HotkeyConflicts)));
    }

    internal void DeactivateHotkeys()
    {
        if (!HotkeysActive) return;
        HotkeysActive = false;
        GlobalHotkeys.Unregister();
    }

    private bool CanRun(HotkeyAction action)
        => action == HotkeyAction.ReplaySave
            ? ReplayRunning
            : action is not (HotkeyAction.Stop or HotkeyAction.Discard) || HasSession || CountingDown;

    internal async Task<bool> RunHotkeyAsync(HotkeyAction action)
    {
        if (!CanRun(action)) return false;

        switch (action)
        {
            case HotkeyAction.Toggle: await ToggleAsync(); break;
            case HotkeyAction.Stop: await StopAsync(); break;
            case HotkeyAction.Frame: ToggleFrame(); break;
            case HotkeyAction.Discard: await DiscardAsync(); break;
            case HotkeyAction.ReplaySave: await SaveReplayAsync(); break;
        }

        return true;
    }
}
