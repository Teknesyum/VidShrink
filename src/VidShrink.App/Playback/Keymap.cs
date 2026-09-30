using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Input;
using VidShrink.App.Localization;

using VidShrink.Core;

namespace VidShrink.App.Playback;

internal enum PlayerInputKind
{
    Key,
    Wheel,
    Press
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

    internal static PlayerInput OnSymbol(string symbol, Key fallback, KeyModifiers modifiers = KeyModifiers.None)
        => new(PlayerInputKind.Key, fallback, symbol, PlayerButton.Left, modifiers);

    internal static PlayerInput OnWheel(KeyModifiers modifiers = KeyModifiers.None)
        => new(PlayerInputKind.Wheel, Key.None, null, PlayerButton.Left, modifiers);

    internal static PlayerInput OnPress(PlayerButton button)
        => new(PlayerInputKind.Press, Key.None, null, button, KeyModifiers.None);
}

internal sealed record PlayerAction(PlayerCommandKind Command, double Amount, string LabelKey)
{
    internal PlayerCommand ToCommand(double scale = 1) => new(Command, Amount * scale);
}

internal sealed record KeymapRow(PlayerInput Input, PlayerAction Action);

internal static class Keymap
{
    internal const double VolumeStep = 5;
    internal const double SpeedStep = 0.05;
    internal const double MinimumSpeed = 0.25;
    internal const double MaximumSpeed = 4;
    internal const double SeekSmall = 10;
    internal const double SeekMedium = 60;
    internal const double SeekLarge = 300;
    internal const double SeekFine = 1;
    internal const double WheelSeekStep = 1;
    internal const double WheelVolumeStep = 1;
    internal const double WheelSpeedStep = 0.1;
    internal const double WheelFactor = 10;

    internal static readonly PlayerAction Settings = new(PlayerCommandKind.OpenSettings, 0, "main.player.menu.settings");
    internal static readonly PlayerAction PlayPause = new(PlayerCommandKind.TogglePlay, 0, "main.player.menu.playpause");
    internal static readonly PlayerAction Stop = new(PlayerCommandKind.Stop, 0, "main.player.menu.stop");
    internal static readonly PlayerAction GoToStart = new(PlayerCommandKind.GoToStart, 0, "main.player.menu.tostart");
    internal static readonly PlayerAction Fullscreen = new(PlayerCommandKind.ToggleFullscreen, 0, "main.player.menu.fullscreen");
    internal static readonly PlayerAction CompactOrFullscreen = new(PlayerCommandKind.CompactOrFullscreen, 0, "main.player.menu.fullscreen");
    internal static readonly PlayerAction ResetZoom = new(PlayerCommandKind.ResetZoom, 0, "main.player.menu.reset");
    internal static readonly PlayerAction AspectCycle = new(PlayerCommandKind.AspectCycle, 0, "player.view.aspect");
    internal static readonly PlayerAction Rotate = new(PlayerCommandKind.Rotate, 90, "player.view.rotate");
    internal static readonly PlayerAction Mirror = new(PlayerCommandKind.Mirror, 0, "player.view.mirror");
    internal static readonly PlayerAction Topmost = new(PlayerCommandKind.ToggleTopmost, 0, "player.view.topmost");
    internal static readonly PlayerAction Info = new(PlayerCommandKind.ToggleInfo, 0, "player.view.info");
    internal static readonly PlayerAction Screenshot = new(PlayerCommandKind.Screenshot, 0, "player.view.screenshot");
    internal static readonly PlayerAction PreviousFile = new(PlayerCommandKind.FileStep, -1, "player.list.previous");
    internal static readonly PlayerAction NextFile = new(PlayerCommandKind.FileStep, 1, "player.list.next");
    internal static readonly PlayerAction Shuffle = new(PlayerCommandKind.ToggleShuffle, 0, "player.list.shuffle");
    internal static readonly PlayerAction RepeatCycle = new(PlayerCommandKind.RepeatCycle, 0, "player.list.repeat");
    internal static readonly PlayerAction Mute = new(PlayerCommandKind.ToggleMute, 0, "main.player.menu.mute");
    internal static readonly PlayerAction Faster = new(PlayerCommandKind.Speed, SpeedStep, "main.player.menu.faster");
    internal static readonly PlayerAction Slower = new(PlayerCommandKind.Speed, -SpeedStep, "main.player.menu.slower");
    internal static readonly PlayerAction NormalSpeed = new(PlayerCommandKind.SpeedReset, 0, "main.player.menu.normalspeed");
    internal static readonly PlayerAction SpeedAb = new(PlayerCommandKind.SpeedAb, 0, "main.player.menu.speedab");
    internal static readonly PlayerAction NextFrame = new(PlayerCommandKind.FrameStep, 1, "main.player.menu.nextframe");
    internal static readonly PlayerAction PreviousFrame = new(PlayerCommandKind.FrameStep, -1, "main.player.menu.prevframe");
    internal static readonly PlayerAction LoopStart = new(PlayerCommandKind.LoopStart, 0, "main.player.menu.loopstart");
    internal static readonly PlayerAction LoopEnd = new(PlayerCommandKind.LoopEnd, 0, "main.player.menu.loopend");
    internal static readonly PlayerAction LoopClear = new(PlayerCommandKind.LoopClear, 0, "main.player.menu.loopclear");
    internal static readonly PlayerAction BookmarkAdd = new(PlayerCommandKind.BookmarkAdd, 0, "main.player.menu.bookmarkadd");
    internal static readonly PlayerAction BookmarkNext = new(PlayerCommandKind.BookmarkNext, 1, "main.player.menu.bookmarknext");
    internal static readonly PlayerAction BookmarkPrevious = new(PlayerCommandKind.BookmarkNext, -1, "main.player.menu.bookmarkprev");
    internal static readonly PlayerAction OpenMenu = new(PlayerCommandKind.ContextMenu, 0, "main.player.menu.open");
    internal static readonly PlayerAction LeaveFullscreen = new(PlayerCommandKind.LeaveFullscreen, 0, "main.player.menu.leavefullscreen");
    internal static readonly PlayerAction Zoom = new(PlayerCommandKind.Zoom, 1, "main.player.menu.zoom");
    internal static readonly PlayerAction Edit = new(PlayerCommandKind.Edit, 0, "player.menu.edit");

    private static PlayerAction Seek(double seconds) => new(PlayerCommandKind.Seek, seconds, "main.player.menu.seek");

    private static PlayerAction Volume(double step) => new(PlayerCommandKind.Volume, step, "main.player.menu.volume");

    internal static readonly IReadOnlyList<KeymapRow> Rows = new KeymapRow[]
    {
        new(PlayerInput.OnWheel(), Seek(WheelSeekStep)),
        new(PlayerInput.OnWheel(KeyModifiers.Control), Seek(WheelSeekStep * WheelScale(KeyModifiers.Control))),
        new(PlayerInput.OnWheel(KeyModifiers.Shift), Seek(WheelSeekStep * WheelScale(KeyModifiers.Shift))),
        new(PlayerInput.OnWheel(KeyModifiers.Control | KeyModifiers.Shift), Seek(WheelSeekStep * WheelScale(KeyModifiers.Control | KeyModifiers.Shift))),
        new(PlayerInput.OnWheel(KeyModifiers.Alt), Zoom),
        new(PlayerInput.OnPress(PlayerButton.Left), PlayPause),
        new(PlayerInput.OnPress(PlayerButton.Middle), CompactOrFullscreen),
        new(PlayerInput.OnPress(PlayerButton.Right), OpenMenu),
        new(PlayerInput.OnKey(Key.F), Fullscreen),
        new(PlayerInput.OnKey(Key.Space), PlayPause),
        new(PlayerInput.OnKey(Key.P, KeyModifiers.Control), PlayPause),
        new(PlayerInput.OnKey(Key.Space, KeyModifiers.Control), Stop),
        new(PlayerInput.OnKey(Key.Back), GoToStart),
        new(PlayerInput.OnKey(Key.Enter), Fullscreen),
        new(PlayerInput.OnKey(Key.Enter, KeyModifiers.Alt), Fullscreen),
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
        new(PlayerInput.OnKey(Key.V), SpeedAb),
        new(PlayerInput.OnKey(Key.F, KeyModifiers.Control | KeyModifiers.Shift), Faster),
        new(PlayerInput.OnKey(Key.B, KeyModifiers.Control | KeyModifiers.Shift), Slower),
        new(PlayerInput.OnKey(Key.N, KeyModifiers.Control | KeyModifiers.Shift), NormalSpeed),
        new(PlayerInput.OnSymbol(".", Key.OemPeriod), NextFrame),
        new(PlayerInput.OnSymbol(",", Key.OemComma), PreviousFrame),
        new(PlayerInput.OnKey(Key.F, KeyModifiers.Shift), PreviousFrame),
        new(PlayerInput.OnSymbol(">", Key.OemPeriod, KeyModifiers.Control | KeyModifiers.Shift), NextFrame),
        new(PlayerInput.OnSymbol("<", Key.OemComma, KeyModifiers.Control | KeyModifiers.Shift), PreviousFrame),
        new(PlayerInput.OnSymbol("[", Key.OemOpenBrackets), LoopStart),
        new(PlayerInput.OnSymbol("]", Key.OemCloseBrackets), LoopEnd),
        new(PlayerInput.OnSymbol("/", Key.Oem2), LoopClear),
        new(PlayerInput.OnKey(Key.F5, KeyModifiers.Control), AspectCycle),
        new(PlayerInput.OnKey(Key.S, KeyModifiers.Control | KeyModifiers.Shift), Rotate),
        new(PlayerInput.OnKey(Key.H, KeyModifiers.Control), Mirror),
        new(PlayerInput.OnKey(Key.A, KeyModifiers.Control), Topmost),
        new(PlayerInput.OnKey(Key.T, KeyModifiers.Control), Topmost),
        new(PlayerInput.OnKey(Key.F1, KeyModifiers.Control), Info),
        new(PlayerInput.OnKey(Key.E, KeyModifiers.Control), Screenshot),
        new(PlayerInput.OnKey(Key.E), Edit),
        new(PlayerInput.OnKey(Key.PageUp), PreviousFile),
        new(PlayerInput.OnKey(Key.PageDown), NextFile),
        new(PlayerInput.OnKey(Key.F, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift), Shuffle),
        new(PlayerInput.OnKey(Key.B, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift), RepeatCycle),
        new(PlayerInput.OnKey(Key.N), BookmarkAdd),
        new(PlayerInput.OnKey(Key.B), BookmarkNext),
        new(PlayerInput.OnKey(Key.PageDown, KeyModifiers.Shift), BookmarkNext),
        new(PlayerInput.OnKey(Key.PageUp, KeyModifiers.Shift), BookmarkPrevious),
        new(PlayerInput.OnKey(Key.A), SubtitleOptions.AudioCycle),
        new(PlayerInput.OnKey(Key.S), SubtitleOptions.SubtitleCycle),
        new(PlayerInput.OnSymbol(">", Key.OemPeriod, KeyModifiers.Shift), SubtitleOptions.SubtitleLater),
        new(PlayerInput.OnSymbol("<", Key.OemComma, KeyModifiers.Shift), SubtitleOptions.SubtitleEarlier),
        new(PlayerInput.OnKey(Key.OemPeriod, KeyModifiers.Control), SubtitleOptions.AudioLater),
        new(PlayerInput.OnKey(Key.OemComma, KeyModifiers.Control), SubtitleOptions.AudioEarlier),
        new(PlayerInput.OnKey(Key.K, KeyModifiers.Control), ToolsOptions.Clip),
        new(PlayerInput.OnKey(Key.G, KeyModifiers.Control | KeyModifiers.Shift), ToolsOptions.Gif),
        new(PlayerInput.OnKey(Key.M, KeyModifiers.Control), ToolsOptions.MiniMode),
        new(PlayerInput.OnKey(Key.U, KeyModifiers.Control), ToolsOptions.OpenUrl),
        new(PlayerInput.OnKey(Key.MediaPlayPause), PlayPause),
        new(PlayerInput.OnKey(Key.MediaStop), Stop),
        new(PlayerInput.OnKey(Key.MediaNextTrack), NextFile),
        new(PlayerInput.OnKey(Key.MediaPreviousTrack), PreviousFile)
    };

    internal static readonly IReadOnlyList<PlayerAction> MenuTop = new[] { PlayPause, Fullscreen };

    internal static readonly IReadOnlyList<PlayerAction?> ViewMenu = new[]
    {
        AspectCycle, Rotate, Mirror, ResetZoom, null, Topmost
    };

    internal static readonly IReadOnlyList<PlayerAction?> PlaybackMenu = new[]
    {
        Faster, Slower, NormalSpeed, null,
        NextFrame, PreviousFrame, null,
        PreviousFile, NextFile, Shuffle, RepeatCycle
    };

    internal static readonly IReadOnlyList<PlayerAction?> LoopMenu = new[]
    {
        LoopStart, LoopEnd, LoopClear, null,
        BookmarkAdd, BookmarkNext, BookmarkPrevious
    };

    internal static readonly IReadOnlyList<PlayerAction> MenuActions = MenuTop
        .Concat(ViewMenu).Concat(PlaybackMenu).Concat(LoopMenu)
        .OfType<PlayerAction>()
        .Append(Screenshot).Append(Info).Append(Settings)
        .ToList();

    internal static PlayerCommand ForWheel(double notches, KeyModifiers modifiers)
    {
        var row = Rows.FirstOrDefault(r => r.Input.Kind == PlayerInputKind.Wheel && r.Input.Modifiers == Clean(modifiers));
        return row is null ? PlayerCommand.None : row.Action.ToCommand(notches);
    }

    internal static double WheelScale(KeyModifiers modifiers) => Clean(modifiers) switch
    {
        KeyModifiers.Control => 1 / WheelFactor,
        KeyModifiers.Shift => WheelFactor,
        KeyModifiers.Control | KeyModifiers.Shift => WheelFactor * WheelFactor,
        _ => 1
    };

    internal static PlayerCommand ForVolumeWheel(double notches, KeyModifiers modifiers)
        => new(PlayerCommandKind.Volume, notches * WheelVolumeStep * WheelScale(modifiers));

    internal static PlayerCommand ForSpeedWheel(double notches, KeyModifiers modifiers)
        => new(PlayerCommandKind.Speed, notches * WheelSpeedStep * WheelScale(modifiers));

    internal static PlayerCommand ForPress(PlayerButton button)
        => Find(r => r.Input.Kind == PlayerInputKind.Press && r.Input.Button == button);

    internal static PlayerCommand ForKey(Key key, KeyModifiers modifiers, string? symbol)
    {
        var clean = Clean(modifiers);
        if (!string.IsNullOrEmpty(symbol))
        {
            var control = Commanding(clean);
            var bySymbol = Rows.FirstOrDefault(r => r.Input.Kind == PlayerInputKind.Key && r.Input.Symbol == symbol && Commanding(r.Input.Modifiers) == control);
            if (bySymbol is not null) return bySymbol.Action.ToCommand();
            return Find(r => r.Input.Kind == PlayerInputKind.Key && r.Input.Symbol is null && r.Input.Key == key && r.Input.Modifiers == clean);
        }

        return Find(r => r.Input.Kind == PlayerInputKind.Key && r.Input.Key == key && r.Input.Modifiers == clean);
    }

    internal static bool Commanding(KeyModifiers modifiers)
        => (modifiers & KeyModifiers.Control) != 0 && (modifiers & KeyModifiers.Alt) == 0;

    internal static KeymapRow? FirstKeyRow(PlayerAction action)
        => Rows.FirstOrDefault(r => r.Input.Kind == PlayerInputKind.Key && ReferenceEquals(r.Action, action));

    internal static string Gesture(PlayerInput input)
    {
        var parts = new List<string>();
        if ((input.Modifiers & KeyModifiers.Control) != 0) parts.Add("Ctrl");
        if ((input.Modifiers & KeyModifiers.Shift) != 0 && input.Symbol is null) parts.Add("Shift");
        if ((input.Modifiers & KeyModifiers.Alt) != 0) parts.Add("Alt");
        parts.Add(input.Kind switch
        {
            PlayerInputKind.Wheel => Strings.Get("main.player.input.wheel"),
            PlayerInputKind.Press when input.Button == PlayerButton.Left => Strings.Get("main.player.input.left"),
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
        var magnitude = Saat.Adim(Math.Abs(amount), Strings.Culture);
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
            Key.Back => "Backspace",
            Key.Apps => Strings.Get("main.player.input.menu"),
            Key.Left => "←",
            Key.Right => "→",
            Key.Up => "↑",
            Key.Down => "↓",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.PageUp => "PgUp",
            Key.PageDown => "PgDn",
            Key.MediaPlayPause => "⏯",
            Key.MediaStop => "⏹",
            Key.MediaNextTrack => "⏭",
            Key.MediaPreviousTrack => "⏮",
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
