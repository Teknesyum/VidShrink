using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using VidShrink.Core;

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

    /// <summary>Tıklama, tıklama sesi, tuş ve büyüteç Windows kancasıyla çalışıyor; öteki platformda kutular gizli, kayıtlı ayar işe geçmez.</summary>
    private bool HooksAvailable => CapturePlatform == RecorderPlatform.Windows;

    /// <summary>macOS'ta tıklamayı kancasız ffmpeg işaretliyor (<c>-capture_mouse_clicks</c>); kutu yalnız orada da açık.</summary>
    private bool ClicksAvailable => HooksAvailable || CapturePlatform == RecorderPlatform.MacOs;

    private bool ClicksInCapture => CapturePlatform == RecorderPlatform.MacOs && ChkShowClicks.IsChecked == true;

    private bool ShowClicksOn => HooksAvailable && _settings.ShowClicks;

    private bool ClickSoundOn => HooksAvailable && _settings.ClickSound;

    private bool ShowKeysOn => HooksAvailable && _settings.ShowKeys;

    private bool MagnifierOn => HooksAvailable && _settings.ShowMagnifier;

    private bool WantsClicks => ShowClicksOn || ClickSoundOn;

    internal void RefreshInputBoxes()
    {
        ChkShowClicks.IsVisible = ClicksAvailable;
        ChkClickSound.IsVisible = HooksAvailable;
        ChkShowKeys.IsVisible = HooksAvailable;
        ChkMagnifier.IsVisible = HooksAvailable;
        RefreshPointerChoices();
        RefreshMini();
    }

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
        var lens = recording && MagnifierOn;
        if (lens && !Magnifier.Running) Magnifier.Start();
        else if (!lens && Magnifier.Running) Magnifier.Stop();

        var wanted = recording && (WantsClicks || WantsPointer || ShowKeysOn);
        var mode = (WantsClicks, ShowKeys: ShowKeysOn, CurrentPointerStyle);
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
        if (ClickSoundOn) ClickSound.Play();
        if (ShowClicksOn &&(_frameRegion is not { } region || region.Contains(point))) InputOverlay.Ring(point, button);
    }

    internal void OnInputMove(PixelPoint point)
    {
        if (!InputActive || !_inputMode.Style.Highlight && !_inputMode.Style.Spotlight) return;
        InputOverlay.Pointer(point, _frameRegion ?? CaptionFallback());
    }

    internal void OnInputKey(uint virtualKey, bool down)
    {
        if (!InputActive || !ShowKeysOn) return;
        if (_keys.Feed(virtualKey, down) is not { } text) return;
        InputOverlay.Keys(text, _frameRegion ?? CaptionFallback());
    }

    private PixelRect CaptionFallback()
    {
        var screen = Avalonia.Controls.TopLevel.GetTopLevel(this)?.Screens?.Primary;
        return screen?.Bounds ?? new PixelRect(0, 0, 0, 0);
    }
}
