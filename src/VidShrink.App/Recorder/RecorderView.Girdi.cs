using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    private IInputHooks? _inputHooks;
    private IClickSound? _clickSound;
    private KeyTracker _keys = new();
    private (bool WantsClicks, bool ShowKeys, PointerStyle Style) _inputMode;

    private static bool RealDesktop
        => OperatingSystem.IsWindows()
           && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime;

    internal IInputHooks InputHooks
    {
        get => _inputHooks ??= RealDesktop ? new Win32InputHooks() : new NoInputHooks();
        set => _inputHooks = value;
    }

    internal IClickSound ClickSound
    {
        get => _clickSound ??= RealDesktop ? new Win32ClickSound() : new NoClickSound();
        set => _clickSound = value;
    }

    internal IInputOverlay InputOverlay { get; set; } = new RecorderInputOverlay();

    internal bool InputActive { get; private set; }

    private bool WantsClicks => _settings.ShowClicks || _settings.ClickSound;

    private bool WantsPointer => _settings.CursorHighlight || _settings.Spotlight;

    private PointerStyle CurrentPointerStyle => new(_settings.CursorHighlight, _settings.Spotlight, _settings.PointerSize, _settings.HighlightColor, _settings.LeftClickColor, _settings.RightClickColor);

    private IMagnifier? _magnifier;

    internal IMagnifier Magnifier
    {
        get => _magnifier ??= RealDesktop ? new RecorderMagnifierHost() : new NoMagnifier();
        set => _magnifier = value;
    }

    internal void SyncInput(bool recording)
    {
        var lens = recording && _settings.ShowMagnifier;
        if (lens && !Magnifier.Running) Magnifier.Start();
        else if (!lens && Magnifier.Running) Magnifier.Stop();

        var wanted = recording && (WantsClicks || WantsPointer || _settings.ShowKeys);
        var mode = (WantsClicks, _settings.ShowKeys, CurrentPointerStyle);
        if (wanted == InputActive && (!wanted || mode == _inputMode)) return;

        if (InputActive)
        {
            InputHooks.Stop();
            InputOverlay.Hide();
            InputActive = false;
        }

        if (!wanted) return;

        _keys = new KeyTracker();
        InputOverlay.Style(mode.CurrentPointerStyle);
        InputHooks.Start(mode.WantsClicks, WantsPointer, mode.ShowKeys, OnInputClick, OnInputMove, OnInputKey);
        _inputMode = mode;
        InputActive = true;
    }

    internal void OnInputClick(PixelPoint point, PointerButton button)
    {
        if (!InputActive) return;
        if (_settings.ClickSound) ClickSound.Play();
        if (_settings.ShowClicks && (_frameRegion is not { } region || region.Contains(point))) InputOverlay.Ring(point, button);
    }

    internal void OnInputMove(PixelPoint point)
    {
        if (!InputActive || !_inputMode.Style.Highlight && !_inputMode.Style.Spotlight) return;
        InputOverlay.Pointer(point, _frameRegion ?? CaptionFallback());
    }

    internal void OnInputKey(uint virtualKey, bool down)
    {
        if (!InputActive || !_settings.ShowKeys) return;
        if (_keys.Feed(virtualKey, down) is not { } text) return;
        InputOverlay.Keys(text, _frameRegion ?? CaptionFallback());
    }

    private PixelRect CaptionFallback()
    {
        var screen = Avalonia.Controls.TopLevel.GetTopLevel(this)?.Screens?.Primary;
        return screen?.Bounds ?? new PixelRect(0, 0, 0, 0);
    }
}
