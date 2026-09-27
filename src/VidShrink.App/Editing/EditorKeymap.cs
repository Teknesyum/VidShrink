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
    FrameForward
}

internal sealed record EditorKeyRow(Key Key, KeyModifiers Modifiers, EditorCommand Command, string LabelKey)
{
    internal string Gesture => Keymap.Gesture(PlayerInput.OnKey(Key, Modifiers));

    internal string Label => Strings.Get(LabelKey);
}

internal static class EditorKeymap
{
    internal const double MaxShuttle = 8;

    internal static readonly IReadOnlyList<EditorKeyRow> Rows = new EditorKeyRow[]
    {
        new(Key.Space, KeyModifiers.None, EditorCommand.PlayPause, "main.player.menu.playpause"),
        new(Key.J, KeyModifiers.None, EditorCommand.ShuttleBack, "editor.key.shuttle-back"),
        new(Key.K, KeyModifiers.None, EditorCommand.ShuttleStop, "main.player.menu.stop"),
        new(Key.L, KeyModifiers.None, EditorCommand.ShuttleForward, "editor.key.shuttle-forward"),
        new(Key.Left, KeyModifiers.None, EditorCommand.FrameBack, "main.player.menu.prevframe"),
        new(Key.Right, KeyModifiers.None, EditorCommand.FrameForward, "main.player.menu.nextframe"),
        new(Key.Home, KeyModifiers.None, EditorCommand.GoToStart, "main.player.menu.tostart"),
        new(Key.End, KeyModifiers.None, EditorCommand.GoToEnd, "editor.to-end"),
        new(Key.I, KeyModifiers.None, EditorCommand.MarkIn, "editor.mark-in"),
        new(Key.O, KeyModifiers.None, EditorCommand.MarkOut, "editor.mark-out"),
        new(Key.M, KeyModifiers.None, EditorCommand.AddMarker, "editor.marker"),
        new(Key.S, KeyModifiers.None, EditorCommand.Split, "editor.split"),
        new(Key.K, KeyModifiers.Control, EditorCommand.Split, "editor.split"),
        new(Key.Delete, KeyModifiers.None, EditorCommand.DeleteSelected, "editor.delete"),
        new(Key.A, KeyModifiers.Control, EditorCommand.SelectAll, "editor.select-all"),
        new(Key.Z, KeyModifiers.Control, EditorCommand.Undo, "editor.undo"),
        new(Key.Y, KeyModifiers.Control, EditorCommand.Redo, "editor.redo"),
        new(Key.Z, KeyModifiers.Control | KeyModifiers.Shift, EditorCommand.Redo, "editor.redo")
    };

    internal static EditorCommand For(Key key, KeyModifiers modifiers)
    {
        var clean = modifiers & (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Meta);
        return Rows.FirstOrDefault(r => r.Key == key && r.Modifiers == clean)?.Command ?? EditorCommand.None;
    }

    internal static EditorKeyRow? First(EditorCommand command) => Rows.FirstOrDefault(r => r.Command == command);

    internal static string Gesture(EditorCommand command) => First(command)?.Gesture ?? string.Empty;
}
