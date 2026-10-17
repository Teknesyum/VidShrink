using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using VidShrink.App.Localization;

namespace VidShrink.App.Playback;

internal enum ShellCommand
{
    None,
    TabPlayer,
    TabEditor,
    TabShrink,
    TabConvert,
    TabRecorder,
    TabSettings,
    OpenFile,
    NextRegion,
    PreviousRegion,
    Help
}

internal sealed record ShellKeyRow(PlayerInput Input, ShellCommand Command)
{
    internal string Label => ShellKeymap.Label(Command);
}

internal static class ShellKeymap
{
    private static readonly object Gate = new();
    private static int _version = -1;
    private static IReadOnlyList<ShellKeyRow> _rows = Array.Empty<ShellKeyRow>();
    private static IReadOnlyList<ShellKeyRow?> _effective = Array.Empty<ShellKeyRow?>();

    internal static readonly IReadOnlyList<ShellKeyRow> DefaultRows = new ShellKeyRow[]
    {
        new(PlayerInput.OnKey(Key.D1, KeyModifiers.Control), ShellCommand.TabPlayer),
        new(PlayerInput.OnKey(Key.D2, KeyModifiers.Control), ShellCommand.TabEditor),
        new(PlayerInput.OnKey(Key.D3, KeyModifiers.Control), ShellCommand.TabShrink),
        new(PlayerInput.OnKey(Key.D4, KeyModifiers.Control), ShellCommand.TabConvert),
        new(PlayerInput.OnKey(Key.D5, KeyModifiers.Control), ShellCommand.TabRecorder),
        new(PlayerInput.OnKey(Key.D6, KeyModifiers.Control), ShellCommand.TabSettings),
        new(PlayerInput.OnKey(Key.O, KeyModifiers.Control), ShellCommand.OpenFile),
        new(PlayerInput.OnKey(Key.F6, KeyModifiers.Control), ShellCommand.NextRegion),
        new(PlayerInput.OnKey(Key.F6, KeyModifiers.Control | KeyModifiers.Shift), ShellCommand.PreviousRegion),
        new(PlayerInput.OnKey(Key.F1), ShellCommand.Help)
    };

    internal static IReadOnlyList<ShellKeyRow> Rows
    {
        get
        {
            Refresh();
            return _rows;
        }
    }

    internal static string SlotId(ShellKeyRow row) => ShortcutBindings.Canon(row.Input);

    internal static IReadOnlyList<ShortcutSlot> ShortcutSlots()
    {
        Refresh();
        var slots = new List<ShortcutSlot>(DefaultRows.Count);
        for (var i = 0; i < DefaultRows.Count; i++)
            slots.Add(new ShortcutSlot(ShortcutMap.Shell, SlotId(DefaultRows[i]), DefaultRows[i].Input, _effective[i]?.Input, DefaultRows[i].Label));
        return slots;
    }

    internal static ShellCommand For(Key key, KeyModifiers modifiers)
    {
        var clean = Keymap.Clean(modifiers);
        return Rows.FirstOrDefault(r => r.Input.Kind == PlayerInputKind.Key && r.Input.Key == key && r.Input.Modifiers == clean)?.Command ?? ShellCommand.None;
    }

    internal static ShellCommand ForPress(PlayerButton button)
        => Rows.FirstOrDefault(r => r.Input.Kind == PlayerInputKind.Press && r.Input.Button == button)?.Command ?? ShellCommand.None;

    internal static string Label(ShellCommand command) => command switch
    {
        ShellCommand.TabPlayer => Tab("main.tab.player"),
        ShellCommand.TabEditor => Tab("main.tab.editor"),
        ShellCommand.TabShrink => Tab("main.tab.shrink"),
        ShellCommand.TabConvert => Tab("main.tab.convert"),
        ShellCommand.TabRecorder => Tab("main.tab.recorder"),
        ShellCommand.TabSettings => Tab("main.tab.settings"),
        ShellCommand.OpenFile => Strings.Get("shortcuts.shell.open"),
        ShellCommand.NextRegion => Strings.Get("shortcuts.shell.region-next"),
        ShellCommand.PreviousRegion => Strings.Get("shortcuts.shell.region-prev"),
        ShellCommand.Help => Strings.Get("shortcuts.shell.help"),
        _ => ""
    };

    private static string Tab(string key) => Strings.Get("shortcuts.shell.tab", Strings.Get(key));

    private static void Refresh()
    {
        var version = ShortcutBindings.Version;
        lock (Gate)
        {
            if (version == _version) return;
            var effective = new ShellKeyRow?[DefaultRows.Count];
            for (var i = 0; i < DefaultRows.Count; i++)
            {
                var row = DefaultRows[i];
                if (!ShortcutBindings.TryOverride(ShortcutMap.Shell, SlotId(row), out var input)) effective[i] = row;
                else if (input is { } bound) effective[i] = row with { Input = bound };
            }

            _effective = effective;
            _rows = effective.OfType<ShellKeyRow>().ToArray();
            _version = version;
        }
    }
}
