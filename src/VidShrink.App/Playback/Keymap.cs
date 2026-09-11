using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Input;
using VidShrink.App.Localization;

namespace VidShrink.App.Playback;

internal enum PlayerInputKind
{
    Key,
    Wheel,
    Press,
    DoubleClick
}

internal readonly record struct PlayerInput(
    PlayerInputKind Kind,
    Key Key,
    string? Symbol,
    PlayerButton Button,
    KeyModifiers Modifiers)
{
    internal static PlayerInput OnKey(Key key, KeyModifiers modifiers = KeyModifiers.None)
        => new(PlayerInputKind.Key, key, null, PlayerButton.Left, modifiers);

    internal static PlayerInput OnSymbol(string symbol, Key fallback)
        => new(PlayerInputKind.Key, fallback, symbol, PlayerButton.Left, KeyModifiers.None);

    internal static PlayerInput OnWheel(KeyModifiers modifiers = KeyModifiers.None)
        => new(PlayerInputKind.Wheel, Key.None, null, PlayerButton.Left, modifiers);

    internal static PlayerInput OnPress(PlayerButton button)
        => new(PlayerInputKind.Press, Key.None, null, button, KeyModifiers.None);

    internal static PlayerInput OnDoubleClick()
        => new(PlayerInputKind.DoubleClick, Key.None, null, PlayerButton.Left, KeyModifiers.None);
}

internal sealed record PlayerAction(PlayerCommandKind Command, double Amount, string LabelKey, int MenuGroup)
{
    internal bool InMenu => MenuGroup > 0;

    internal PlayerCommand ToCommand(double scale = 1) => new(Command, Amount * scale);
}

internal sealed record KeymapRow(PlayerInput Input, PlayerAction Action);

internal static class Keymap
{
    internal const double VolumeStep = 5;
    internal const double SpeedStep = 0.1;
    internal const double MinimumSpeed = 0.25;
    internal const double MaximumSpeed = 4;
    internal const double SeekSmall = 10;
    internal const double SeekMedium = 60;
    internal const double SeekLarge = 300;
    internal const double SeekFine = 1;

    internal static readonly PlayerAction PlayPause = new(PlayerCommandKind.TogglePlay, 0, "main.player.menu.playpause", 1);
    internal static readonly PlayerAction Fullscreen = new(PlayerCommandKind.ToggleFullscreen, 0, "main.player.menu.fullscreen", 1);
    internal static readonly PlayerAction ResetZoom = new(PlayerCommandKind.ResetZoom, 0, "main.player.menu.reset", 1);
    internal static readonly PlayerAction AspectCycle = new(PlayerCommandKind.AspectCycle, 0, "player.view.aspect", 7);
    internal static readonly PlayerAction Rotate = new(PlayerCommandKind.Rotate, 90, "player.view.rotate", 7);
    internal static readonly PlayerAction Mirror = new(PlayerCommandKind.Mirror, 0, "player.view.mirror", 7);
    internal static readonly PlayerAction Topmost = new(PlayerCommandKind.ToggleTopmost, 0, "player.view.topmost", 7);
    internal static readonly PlayerAction Info = new(PlayerCommandKind.ToggleInfo, 0, "player.view.info", 7);
    internal static readonly PlayerAction Screenshot = new(PlayerCommandKind.Screenshot, 0, "player.view.screenshot", 7);
    internal static readonly PlayerAction PreviousFile = new(PlayerCommandKind.FileStep, -1, "player.list.previous", 8);
    internal static readonly PlayerAction NextFile = new(PlayerCommandKind.FileStep, 1, "player.list.next", 8);
    internal static readonly PlayerAction Shuffle = new(PlayerCommandKind.ToggleShuffle, 0, "player.list.shuffle", 8);
    internal static readonly PlayerAction RepeatCycle = new(PlayerCommandKind.RepeatCycle, 0, "player.list.repeat", 8);
    internal static readonly PlayerAction Mute = new(PlayerCommandKind.ToggleMute, 0, "main.player.menu.mute", 2);
    internal static readonly PlayerAction Faster = new(PlayerCommandKind.Speed, SpeedStep, "main.player.menu.faster", 3);
    internal static readonly PlayerAction Slower = new(PlayerCommandKind.Speed, -SpeedStep, "main.player.menu.slower", 3);
    internal static readonly PlayerAction NormalSpeed = new(PlayerCommandKind.SpeedReset, 0, "main.player.menu.normalspeed", 3);
    internal static readonly PlayerAction NextFrame = new(PlayerCommandKind.FrameStep, 1, "main.player.menu.nextframe", 4);
    internal static readonly PlayerAction PreviousFrame = new(PlayerCommandKind.FrameStep, -1, "main.player.menu.prevframe", 4);
    internal static readonly PlayerAction LoopStart = new(PlayerCommandKind.LoopStart, 0, "main.player.menu.loopstart", 5);
    internal static readonly PlayerAction LoopEnd = new(PlayerCommandKind.LoopEnd, 0, "main.player.menu.loopend", 5);
    internal static readonly PlayerAction LoopClear = new(PlayerCommandKind.LoopClear, 0, "main.player.menu.loopclear", 5);
    internal static readonly PlayerAction BookmarkAdd = new(PlayerCommandKind.BookmarkAdd, 0, "main.player.menu.bookmarkadd", 6);
    internal static readonly PlayerAction BookmarkNext = new(PlayerCommandKind.BookmarkNext, 0, "main.player.menu.bookmarknext", 6);
    internal static readonly PlayerAction OpenMenu = new(PlayerCommandKind.ContextMenu, 0, "main.player.menu.open", 0);
    internal static readonly PlayerAction LeaveFullscreen = new(PlayerCommandKind.LeaveFullscreen, 0, "main.player.menu.leavefullscreen", 0);
    internal static readonly PlayerAction Zoom = new(PlayerCommandKind.Zoom, 1, "main.player.menu.zoom", 0);

    private static PlayerAction Seek(double seconds) => new(PlayerCommandKind.Seek, seconds, "main.player.menu.seek", 0);

    private static PlayerAction Volume(double step) => new(PlayerCommandKind.Volume, step, "main.player.menu.volume", 0);

    internal static readonly IReadOnlyList<KeymapRow> Rows = new KeymapRow[]
    {
        new(PlayerInput.OnWheel(), Volume(VolumeStep)),
        new(PlayerInput.OnWheel(KeyModifiers.Control), Seek(SeekSmall)),
        new(PlayerInput.OnWheel(KeyModifiers.Shift), Seek(SeekMedium)),
        new(PlayerInput.OnWheel(KeyModifiers.Control | KeyModifiers.Shift), Seek(SeekLarge)),
        new(PlayerInput.OnWheel(KeyModifiers.Alt), Zoom),
        new(PlayerInput.OnPress(PlayerButton.Middle), Fullscreen),
        new(PlayerInput.OnDoubleClick(), Fullscreen),
        new(PlayerInput.OnPress(PlayerButton.Right), OpenMenu),
        new(PlayerInput.OnKey(Key.Space), PlayPause),
        new(PlayerInput.OnKey(Key.Enter), Fullscreen),
        new(PlayerInput.OnKey(Key.Escape), LeaveFullscreen),
        new(PlayerInput.OnKey(Key.Apps), OpenMenu),
        new(PlayerInput.OnKey(Key.Right), Seek(SeekSmall)),
        new(PlayerInput.OnKey(Key.Left), Seek(-SeekSmall)),
        new(PlayerInput.OnKey(Key.Right, KeyModifiers.Control), Seek(SeekMedium)),
        new(PlayerInput.OnKey(Key.Left, KeyModifiers.Control), Seek(-SeekMedium)),
        new(PlayerInput.OnKey(Key.Right, KeyModifiers.Shift), Seek(SeekLarge)),
        new(PlayerInput.OnKey(Key.Left, KeyModifiers.Shift), Seek(-SeekLarge)),
        new(PlayerInput.OnKey(Key.Right, KeyModifiers.Alt), Seek(SeekFine)),
        new(PlayerInput.OnKey(Key.Left, KeyModifiers.Alt), Seek(-SeekFine)),
        new(PlayerInput.OnKey(Key.Up), Volume(VolumeStep)),
        new(PlayerInput.OnKey(Key.Down), Volume(-VolumeStep)),
        new(PlayerInput.OnKey(Key.M), Mute),
        new(PlayerInput.OnKey(Key.C), Faster),
        new(PlayerInput.OnKey(Key.X), Slower),
        new(PlayerInput.OnKey(Key.Z), NormalSpeed),
        new(PlayerInput.OnKey(Key.F), NextFrame),
        new(PlayerInput.OnKey(Key.F, KeyModifiers.Shift), PreviousFrame),
        new(PlayerInput.OnSymbol("[", Key.OemOpenBrackets), LoopStart),
        new(PlayerInput.OnSymbol("]", Key.OemCloseBrackets), LoopEnd),
        new(PlayerInput.OnSymbol("/", Key.Oem2), LoopClear),
        new(PlayerInput.OnKey(Key.F5, KeyModifiers.Control), AspectCycle),
        new(PlayerInput.OnKey(Key.S, KeyModifiers.Control | KeyModifiers.Shift), Rotate),
        new(PlayerInput.OnKey(Key.H, KeyModifiers.Control), Mirror),
        new(PlayerInput.OnKey(Key.A, KeyModifiers.Control), Topmost),
        new(PlayerInput.OnKey(Key.F1, KeyModifiers.Control), Info),
        new(PlayerInput.OnKey(Key.E, KeyModifiers.Control), Screenshot),
        new(PlayerInput.OnKey(Key.PageUp), PreviousFile),
        new(PlayerInput.OnKey(Key.PageDown), NextFile),
        new(PlayerInput.OnKey(Key.F, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift), Shuffle),
        new(PlayerInput.OnKey(Key.B, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift), RepeatCycle),
        new(PlayerInput.OnKey(Key.N), BookmarkAdd),
        new(PlayerInput.OnKey(Key.B), BookmarkNext),
        new(PlayerInput.OnKey(Key.A), SubtitleOptions.AudioCycle),
        new(PlayerInput.OnKey(Key.S), SubtitleOptions.SubtitleCycle),
        new(PlayerInput.OnSymbol(">", Key.OemPeriod), SubtitleOptions.SubtitleLater),
        new(PlayerInput.OnSymbol("<", Key.OemComma), SubtitleOptions.SubtitleEarlier),
        new(PlayerInput.OnKey(Key.OemPeriod, KeyModifiers.Control), SubtitleOptions.AudioLater),
        new(PlayerInput.OnKey(Key.OemComma, KeyModifiers.Control), SubtitleOptions.AudioEarlier)
    };

    internal static readonly IReadOnlyList<PlayerAction> MenuActions = new[]
    {
        PlayPause, Fullscreen, ResetZoom,
        Mute,
        Faster, Slower, NormalSpeed,
        NextFrame, PreviousFrame,
        AspectCycle, Rotate, Mirror, Topmost, Info, Screenshot,
        PreviousFile, NextFile, Shuffle, RepeatCycle,
        LoopStart, LoopEnd, LoopClear,
        BookmarkAdd, BookmarkNext
    };

    internal static PlayerCommand ForWheel(double notches, KeyModifiers modifiers)
    {
        var row = Rows.FirstOrDefault(r => r.Input.Kind == PlayerInputKind.Wheel && r.Input.Modifiers == Clean(modifiers));
        return row is null ? PlayerCommand.None : row.Action.ToCommand(notches);
    }

    internal static PlayerCommand ForPress(PlayerButton button, int clicks)
    {
        if (button == PlayerButton.Left && clicks >= 2)
            return Find(r => r.Input.Kind == PlayerInputKind.DoubleClick);
        return Find(r => r.Input.Kind == PlayerInputKind.Press && r.Input.Button == button);
    }

    internal static PlayerCommand ForKey(Key key, KeyModifiers modifiers, string? symbol)
    {
        if (!string.IsNullOrEmpty(symbol))
        {
            var bySymbol = Rows.FirstOrDefault(r => r.Input.Kind == PlayerInputKind.Key && r.Input.Symbol == symbol);
            if (bySymbol is not null) return bySymbol.Action.ToCommand();
        }

        var clean = Clean(modifiers);
        return Find(r => r.Input.Kind == PlayerInputKind.Key && r.Input.Key == key && r.Input.Modifiers == clean);
    }

    internal static KeymapRow? FirstKeyRow(PlayerAction action)
        => Rows.FirstOrDefault(r => r.Input.Kind == PlayerInputKind.Key && ReferenceEquals(r.Action, action));

    internal static string Gesture(PlayerInput input)
    {
        var parts = new List<string>();
        if ((input.Modifiers & KeyModifiers.Control) != 0) parts.Add("Ctrl");
        if ((input.Modifiers & KeyModifiers.Shift) != 0) parts.Add("Shift");
        if ((input.Modifiers & KeyModifiers.Alt) != 0) parts.Add("Alt");
        parts.Add(input.Kind switch
        {
            PlayerInputKind.Wheel => Strings.Get("main.player.input.wheel"),
            PlayerInputKind.DoubleClick => Strings.Get("main.player.input.double"),
            PlayerInputKind.Press when input.Button == PlayerButton.Middle => Strings.Get("main.player.input.middle"),
            PlayerInputKind.Press => Strings.Get("main.player.input.right"),
            _ => KeyName(input)
        });
        return string.Join("+", parts);
    }

    internal static string Label(KeymapRow row)
    {
        var action = row.Action;
        return action.Command switch
        {
            PlayerCommandKind.Seek or PlayerCommandKind.Volume or PlayerCommandKind.SubtitleDelay or PlayerCommandKind.AudioDelay => Strings.Get(action.LabelKey, Signed(action.Amount, row.Input.Kind == PlayerInputKind.Wheel)),
            _ => Strings.Get(action.LabelKey)
        };
    }

    private static string Signed(double amount, bool bothWays)
    {
        var magnitude = Math.Abs(amount).ToString("0.##", CultureInfo.CurrentCulture);
        if (bothWays) return "±" + magnitude;
        return (amount < 0 ? "−" : "+") + magnitude;
    }

    private static string KeyName(PlayerInput input)
    {
        if (input.Symbol is { } symbol) return symbol;
        return input.Key switch
        {
            Key.Space => Strings.Get("main.player.input.space"),
            Key.Enter => Strings.Get("main.player.input.enter"),
            Key.Escape => Strings.Get("main.player.input.esc"),
            Key.Apps => Strings.Get("main.player.input.menu"),
            Key.Left => "←",
            Key.Right => "→",
            Key.Up => "↑",
            Key.Down => "↓",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.PageUp => "PgUp",
            Key.PageDown => "PgDn",
            _ => input.Key.ToString()
        };
    }

    private static KeyModifiers Clean(KeyModifiers modifiers)
        => modifiers & (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt);

    private static PlayerCommand Find(Func<KeymapRow, bool> match)
    {
        var row = Rows.FirstOrDefault(match);
        return row is null ? PlayerCommand.None : row.Action.ToCommand();
    }
}
