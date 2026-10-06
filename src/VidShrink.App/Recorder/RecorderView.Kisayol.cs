using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using VidShrink.App.Playback;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    private readonly StackPanel _hotkeyRows = new();
    private readonly Dictionary<HotkeyAction, ShortcutKeyButton> _hotkeyBoxes = [];
    private readonly Dictionary<HotkeyAction, TextBlock> _hotkeyErrors = [];
    private readonly Dictionary<HotkeyAction, Button> _hotkeyResets = [];
    private IGlobalHotkeys? _globalHotkeys;
    private IReadOnlyList<HotkeyBinding> _hotkeys = RecorderHotkeys.All;
    private (HotkeyAction Action, Func<string> Text)? _hotkeyError;
    private string _hotkeyTaken = string.Empty;
    private bool _hotkeysSuspended;

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

    /// <summary>Geçerli atamalar; ayardan okunur, bozuk ya da eski ayarda varsayılan tablodur.</summary>
    internal IReadOnlyList<HotkeyBinding> Hotkeys => _hotkeys;

    internal string HotkeyName(HotkeyAction action) => RecorderHotkeys.Gesture(RecorderHotkeys.Of(_hotkeys, action));

    internal ShortcutKeyButton HotkeyBox(HotkeyAction action) => _hotkeyBoxes[action];

    internal Button HotkeyReset(HotkeyAction action) => _hotkeyResets[action];

    internal string HotkeyError(HotkeyAction action)
        => _hotkeyErrors[action].IsVisible ? _hotkeyErrors[action].Text ?? string.Empty : string.Empty;

    private void InitKisayol()
    {
        _hotkeys = RecorderHotkeys.Read(_settings.Hotkeys) ?? RecorderHotkeys.All;
        _hotkeyRows.Spacing = HotkeyToken("SpaceSm");
        var flyout = new Flyout { Content = _hotkeyRows };
        flyout.Opened += (_, _) => SuspendHotkeys();
        flyout.Closed += (_, _) => ResumeHotkeys();
        BtnHotkeys.Flyout = flyout;
        RefreshHotkeyLabels();
    }

    internal void ActivateHotkeys()
    {
        if (HotkeysActive) return;
        HotkeysActive = true;
        HotkeyConflicts = GlobalHotkeys.Register(_hotkeys, PressHotkey);
        ShowHotkeyConflicts();
        RefreshHotkeyRows();
    }

    internal void DeactivateHotkeys()
    {
        if (!HotkeysActive) return;
        HotkeysActive = false;
        if (!_hotkeysSuspended) GlobalHotkeys.Unregister();
        _hotkeysSuspended = false;
    }

    private void PressHotkey(HotkeyAction action) => _ = RunHotkeyAsync(action);

    /// <summary>
    /// Yakalama kutusu açıkken genel kayıt bırakılır: kayıtlı tuş pencereye hiç ulaşmaz ve
    /// kullanıcı var olan bir kısayolu başka bir eyleme veremezdi.
    /// </summary>
    private void SuspendHotkeys()
    {
        if (!HotkeysActive || _hotkeysSuspended) return;
        _hotkeysSuspended = true;
        GlobalHotkeys.Unregister();
    }

    private void ResumeHotkeys()
    {
        _hotkeyError = null;
        if (_hotkeysSuspended)
        {
            _hotkeysSuspended = false;
            if (HotkeysActive)
            {
                HotkeyConflicts = GlobalHotkeys.Register(_hotkeys, PressHotkey);
                ShowHotkeyConflicts();
            }
        }

        RefreshHotkeyRows();
    }

    private void ShowHotkeyConflicts()
    {
        if (HotkeyConflicts.Count > 0)
        {
            _hotkeyTaken = Say("recorder.hotkey.taken", RecorderHotkeys.Names(HotkeyConflicts));
            ShowError(_hotkeyTaken);
            return;
        }

        if (_hotkeyTaken.Length > 0 && TxtError.Text == _hotkeyTaken)
        {
            TxtError.IsVisible = false;
            TxtError.Text = string.Empty;
        }

        _hotkeyTaken = string.Empty;
    }

    /// <summary>
    /// Bir eyleme yeni tuş verir. Atanamayan tuş, başka eylemin tuşu ve sistemin kaydetmediği tuş
    /// reddedilir; eski atama kalır ve neden o eylemin satırında yazar.
    /// </summary>
    internal bool AssignHotkey(HotkeyAction action, Key key, KeyModifiers modifiers)
    {
        if (RecorderHotkeys.Bind(action, key, modifiers) is not { } binding)
            return RejectHotkey(action, () => Say("recorder.hotkeys.unsupported"));

        if (_hotkeys.FirstOrDefault(b => b.Action != action && RecorderHotkeys.Same(b, binding)) is { } owner)
            return RejectHotkey(action, () => Say("recorder.hotkeys.duplicate", RecorderHotkeys.Gesture(binding), HotkeyLabel(owner.Action)));

        IReadOnlyList<HotkeyBinding> next = _hotkeys.Select(b => b.Action == action ? binding : b).ToList();
        var conflicts = HotkeyConflicts.Where(b => b.Action != action).ToList() as IReadOnlyList<HotkeyBinding>;
        if (HotkeysActive)
        {
            conflicts = GlobalHotkeys.Register(next, PressHotkey);
            if (conflicts.Any(b => b.Action == action))
            {
                if (_hotkeysSuspended) GlobalHotkeys.Unregister();
                else HotkeyConflicts = GlobalHotkeys.Register(_hotkeys, PressHotkey);
                return RejectHotkey(action, () => Say("recorder.hotkeys.rejected", RecorderHotkeys.Gesture(binding)));
            }

            if (_hotkeysSuspended) GlobalHotkeys.Unregister();
        }

        _hotkeys = next;
        HotkeyConflicts = conflicts;
        _hotkeyError = null;
        _settings.Hotkeys = RecorderHotkeys.Write(_hotkeys);
        _settings.Save(_settingsPath);
        ShowHotkeyConflicts();
        RefreshHotkeyLabels();
        return true;
    }

    internal bool ResetHotkey(HotkeyAction action)
    {
        var fallback = RecorderHotkeys.Of(RecorderHotkeys.All, action);
        return AssignHotkey(action, fallback.Key, fallback.Modifiers);
    }

    private bool RejectHotkey(HotkeyAction action, Func<string> text)
    {
        _hotkeyError = (action, text);
        RefreshHotkeyRows();
        return false;
    }

    private static string HotkeyLabel(HotkeyAction action) => action switch
    {
        HotkeyAction.Toggle => Say("recorder.strip.start") + " / " + Say("recorder.strip.pause"),
        HotkeyAction.Stop => Say("recorder.strip.stop"),
        HotkeyAction.Frame => Say("recorder.hotkeys.frame"),
        HotkeyAction.Discard => Say("recorder.hotkeys.discard"),
        HotkeyAction.Chapter => Say("recorder.hotkeys.chapter"),
        _ => Say("recorder.hotkeys.replay-save")
    };

    /// <summary>Tuş adını taşıyan her metin atamadan yeniden yazılır: ipuçları, bölge paneli, satırlar.</summary>
    private void RefreshHotkeyLabels()
    {
        var replay = Core.Bicim.Satir.Bagla(Say("recorder.replay.hint", HotkeyName(HotkeyAction.ReplaySave)));
        ToolTip.SetTip(CmbReplaySeconds, replay);
        ToolTip.SetTip(BtnReplay, replay);
        _regionEditor.HideKey = HotkeyName(HotkeyAction.Frame);
        RefreshHotkeyRows();
    }

    private void RefreshHotkeyRows()
    {
        _hotkeyRows.Children.Clear();
        _hotkeyBoxes.Clear();
        _hotkeyErrors.Clear();
        _hotkeyResets.Clear();

        foreach (var fallback in RecorderHotkeys.All)
        {
            var action = fallback.Action;
            var binding = RecorderHotkeys.Of(_hotkeys, action);
            var label = HotkeyLabel(action);

            var name = new TextBlock { Text = label, Theme = HotkeyTheme("Body"), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            var slot = new ShortcutSlot(ShortcutMap.Player, action.ToString(), PlayerInput.OnKey(fallback.Key, fallback.Modifiers), PlayerInput.OnKey(binding.Key, binding.Modifiers), label);
            var box = new ShortcutKeyButton(slot, HotkeyTheme("ShortcutKeyButton"), HotkeyTheme("MonoValue")) { VerticalAlignment = VerticalAlignment.Center };
            box.Captured += (_, input) =>
            {
                if (input.Kind == PlayerInputKind.Key) AssignHotkey(action, input.Key, input.Modifiers);
                else RejectHotkey(action, () => Say("recorder.hotkeys.unsupported"));
            };

            var restore = Say("recorder.hotkeys.reset");
            var reset = new Button { Content = restore, Theme = HotkeyTheme("GhostButton"), IsEnabled = !RecorderHotkeys.Same(binding, fallback), VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(reset, label + " " + restore);
            reset.Click += (_, _) => ResetHotkey(action);

            var message = _hotkeyError is { } failed && failed.Action == action
                ? failed.Text()
                : HotkeyConflicts.Any(b => b.Action == action) ? Say("recorder.hotkeys.rejected", RecorderHotkeys.Gesture(binding)) : string.Empty;
            var error = new TextBlock { Text = message, Theme = HotkeyTheme("StatusError"), TextWrapping = TextWrapping.Wrap, IsVisible = message.Length > 0 };

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                ColumnSpacing = HotkeyToken("SpaceMd"),
                RowSpacing = HotkeyToken("SpaceXs")
            };
            Grid.SetColumn(box, 1);
            Grid.SetColumn(reset, 2);
            Grid.SetRow(error, 1);
            Grid.SetColumnSpan(error, 3);
            row.Children.Add(name);
            row.Children.Add(box);
            row.Children.Add(reset);
            row.Children.Add(error);
            _hotkeyRows.Children.Add(row);

            _hotkeyBoxes[action] = box;
            _hotkeyErrors[action] = error;
            _hotkeyResets[action] = reset;
        }
    }

    private static ControlTheme? HotkeyTheme(string key)
        => Application.Current?.TryFindResource(key, out var value) == true ? value as ControlTheme : null;

    private static double HotkeyToken(string key)
        => Application.Current?.TryFindResource(key, out var value) == true && value is double number ? number : 0;

    private bool CanRun(HotkeyAction action) => action switch
    {
        HotkeyAction.ReplaySave => ReplayRunning,
        HotkeyAction.Chapter => CanMarkChapter,
        _ => action is not (HotkeyAction.Stop or HotkeyAction.Discard) || HasSession || CountingDown
    };

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
            case HotkeyAction.Chapter: MarkChapter(); break;
        }

        return true;
    }
}
