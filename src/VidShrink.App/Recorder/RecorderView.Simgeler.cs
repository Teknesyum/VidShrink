using System;
using Avalonia.Controls;
using VidShrink.App.Localization;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    internal const string DesktopIconsUnavailableKey = "recorder.option.hide-desktop-icons-unavailable";

    private DesktopIconGuard? _desktopIcons;

    internal bool DesktopIconsSupported { get; private set; } = OperatingSystem.IsWindows();

    internal DesktopIconGuard DesktopIcons
    {
        get => _desktopIcons ??= RealDesktop
            ? DesktopIconGuard.ForSettings(_settingsPath)
            : new DesktopIconGuard(new NoDesktopIcons(), null);
        set => _desktopIcons = value;
    }

    internal void ApplyDesktopIconsSupport(bool supported)
    {
        DesktopIconsSupported = supported;
        ChkHideIcons.IsEnabled = supported;
        ChkHideIcons.IsChecked = supported && _settings.HideDesktopIcons;
        if (supported) return;
        ChkHideIcons.Bind(ToolTip.TipProperty, LocalizedText.For(DesktopIconsUnavailableKey).Binding);
    }

    internal bool HideDesktopIconsForRecording()
        => DesktopIconsSupported && _settings.HideDesktopIcons && DesktopIcons.Hide();

    internal bool RestoreDesktopIcons() => DesktopIcons.Restore();
}
