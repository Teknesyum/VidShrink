using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using VidShrink.App.Localization;
using VidShrink.App.Playback;

namespace VidShrink.App.Editing;

internal enum EditorCommand
{
    None,
    ShuttleBack,
    ShuttleStop,
    ShuttleForward,
    MarkIn,
    MarkOut,
    AddMarker,
    PlayPause,
    Undo,
    Redo,
    SelectAll,
    GoToStart,
    GoToEnd,
    DeleteSelected,
    Split,
    FrameBack,
    FrameForward,
    ToggleSnap,
    TrimHead,
    TrimTail,
    PrevEdit,
    NextEdit,
    Back5,
    Forward5,
    ZoomIn,
    ZoomOut,
    ZoomFit,
    GoToIn,
    GoToOut,
    FocusSpeed,
    Extract,
    ToolSelection,
    ToolRazor,
    ToolRipple,
    Export,
    AddText
}

internal sealed record EditorKeyRow(Key Key, KeyModifiers Modifiers, EditorCommand Command, string LabelKey, string? Symbol = null, PlayerButton? Button = null)
{
    internal PlayerInput Input => Button is { } button
        ? PlayerInput.OnPress(button)
        : Symbol is null ? PlayerInput.OnKey(Key, Modifiers) : PlayerInput.OnSymbol(Symbol, Key, Modifiers);

    internal string Gesture => Keymap.Gesture(Input);

    internal EditorKeyRow Bound(PlayerInput input) => input.Kind == PlayerInputKind.Press
        ? this with { Key = Key.None, Modifiers = KeyModifiers.None, Symbol = null, Button = input.Button }
        : this with { Key = input.Key, Modifiers = input.Modifiers, Symbol = input.Symbol, Button = null };

    internal string Label => Strings.Get(LabelKey);
}

internal static class EditorKeymap
{
    internal const double MaxShuttle = 8;

    private static readonly object Gate = new();
    private static int _version = -1;
    private static IReadOnlyList<EditorKeyRow> _rows = System.Array.Empty<EditorKeyRow>();
    private static IReadOnlyList<EditorKeyRow?> _effective = System.Array.Empty<EditorKeyRow?>();

    internal static IReadOnlyList<EditorKeyRow> Rows
    {
        get
        {
            Refresh();
            return _rows;
        }
    }

    internal static string SlotId(EditorKeyRow row) => ShortcutBindings.Canon(row.Input);

    internal static IReadOnlyList<ShortcutSlot> ShortcutSlots()
    {
        Refresh();
        var slots = new List<ShortcutSlot>(DefaultRows.Count);
        for (var i = 0; i < DefaultRows.Count; i++)
            slots.Add(new ShortcutSlot(ShortcutMap.Editor, SlotId(DefaultRows[i]), DefaultRows[i].Input, _effective[i]?.Input, DefaultRows[i].Label));
        return slots;
    }

    private static void Refresh()
    {
        var version = ShortcutBindings.Version;
        lock (Gate)
        {
            if (version == _version) return;
            var effective = new EditorKeyRow?[DefaultRows.Count];
            for (var i = 0; i < DefaultRows.Count; i++)
            {
                var row = DefaultRows[i];
                if (!ShortcutBindings.TryOverride(ShortcutMap.Editor, SlotId(row), out var input)) effective[i] = row;
                else if (input is { } bound) effective[i] = row.Bound(bound);
            }

            _effective = effective;
            _rows = effective.OfType<EditorKeyRow>().ToArray();
            _version = version;
        }
    }

    internal static readonly IReadOnlyList<EditorKeyRow> DefaultRows = new EditorKeyRow[]
    {
        new(Key.Space, KeyModifiers.None, EditorCommand.PlayPause, "main.player.menu.playpause"),
        new(Key.J, KeyModifiers.None, EditorCommand.ShuttleBack, "editor.key.shuttle-back"),
        new(Key.K, KeyModifiers.None, EditorCommand.ShuttleStop, "main.player.menu.stop"),
        new(Key.L, KeyModifiers.None, EditorCommand.ShuttleForward, "editor.key.shuttle-forward"),
        new(Key.Left, KeyModifiers.None, EditorCommand.FrameBack, "main.player.menu.prevframe"),
        new(Key.Right, KeyModifiers.None, EditorCommand.FrameForward, "main.player.menu.nextframe"),
        new(Key.Left, KeyModifiers.Shift, EditorCommand.Back5, "editor.key.back-5"),
        new(Key.Right, KeyModifiers.Shift, EditorCommand.Forward5, "editor.key.forward-5"),
        new(Key.Up, KeyModifiers.None, EditorCommand.PrevEdit, "editor.key.prev-edit"),
        new(Key.Down, KeyModifiers.None, EditorCommand.NextEdit, "editor.key.next-edit"),
        new(Key.Home, KeyModifiers.None, EditorCommand.GoToStart, "main.player.menu.tostart"),
        new(Key.End, KeyModifiers.None, EditorCommand.GoToEnd, "editor.to-end"),
        new(Key.I, KeyModifiers.None, EditorCommand.MarkIn, "editor.mark-in"),
        new(Key.O, KeyModifiers.None, EditorCommand.MarkOut, "editor.mark-out"),
        new(Key.I, KeyModifiers.Shift, EditorCommand.GoToIn, "editor.key.go-in"),
        new(Key.O, KeyModifiers.Shift, EditorCommand.GoToOut, "editor.key.go-out"),
        new(Key.M, KeyModifiers.None, EditorCommand.AddMarker, "editor.marker"),
        new(Key.K, KeyModifiers.Control, EditorCommand.Split, "editor.split"),
        new(Key.S, KeyModifiers.None, EditorCommand.ToggleSnap, "editor.key.snap"),
        new(Key.Q, KeyModifiers.None, EditorCommand.TrimHead, "editor.key.trim-head"),
        new(Key.W, KeyModifiers.None, EditorCommand.TrimTail, "editor.key.trim-tail"),
        new(Key.Delete, KeyModifiers.None, EditorCommand.DeleteSelected, "editor.delete"),
        new(Key.Delete, KeyModifiers.Shift, EditorCommand.DeleteSelected, "editor.delete"),
        new(Key.OemQuotes, KeyModifiers.None, EditorCommand.Extract, "editor.delete-range", "'"),
        new(Key.OemPlus, KeyModifiers.None, EditorCommand.ZoomIn, "editor.zoom-in", "="),
        new(Key.Add, KeyModifiers.None, EditorCommand.ZoomIn, "editor.zoom-in", "Num +"),
        new(Key.OemMinus, KeyModifiers.None, EditorCommand.ZoomOut, "editor.zoom-out", "-"),
        new(Key.Subtract, KeyModifiers.None, EditorCommand.ZoomOut, "editor.zoom-out", "Num -"),
        new(Key.OemPipe, KeyModifiers.None, EditorCommand.ZoomFit, "editor.key.zoom-fit", "\\"),
        new(Key.V, KeyModifiers.None, EditorCommand.ToolSelection, "editor.tool.selection"),
        new(Key.C, KeyModifiers.None, EditorCommand.ToolRazor, "editor.tool.razor"),
        new(Key.B, KeyModifiers.None, EditorCommand.ToolRipple, "editor.tool.ripple"),
        new(Key.M, KeyModifiers.Control, EditorCommand.Export, "editor.save-as"),
        new(Key.T, KeyModifiers.None, EditorCommand.AddText, "editor.text.add"),
        new(Key.R, KeyModifiers.Control, EditorCommand.FocusSpeed, "editor.speed"),
        new(Key.A, KeyModifiers.Control, EditorCommand.SelectAll, "editor.select-all"),
        new(Key.Z, KeyModifiers.Control, EditorCommand.Undo, "editor.undo"),
        new(Key.Y, KeyModifiers.Control, EditorCommand.Redo, "editor.redo"),
        new(Key.Z, KeyModifiers.Control | KeyModifiers.Shift, EditorCommand.Redo, "editor.redo")
    };

    internal static EditorCommand For(Key key, KeyModifiers modifiers)
    {
        var clean = Clean(modifiers);
        return Rows.FirstOrDefault(r => r.Button is null && r.Key == key && r.Modifiers == clean)?.Command ?? EditorCommand.None;
    }

    internal static KeyModifiers Clean(KeyModifiers modifiers)
        => modifiers & (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Meta);

    internal static EditorCommand ForPress(PlayerButton button)
        => Rows.FirstOrDefault(r => r.Button == button)?.Command ?? EditorCommand.None;

    internal static EditorKeyRow? FirstKey(EditorCommand command) => Rows.FirstOrDefault(r => r.Command == command && r.Button is null);

    internal static EditorKeyRow? First(EditorCommand command) => Rows.FirstOrDefault(r => r.Command == command);

    internal static string Gesture(EditorCommand command) => First(command)?.Gesture ?? string.Empty;
}
