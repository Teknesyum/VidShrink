using Avalonia.Controls;
using Avalonia.Media;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    private IRecorderTrayHost? _trayHost;

    internal IRecorderTrayHost TrayHost
    {
        get => _trayHost ??= new RecorderTrayHost(RestoreWindow);
        set => _trayHost = value;
    }

    internal bool TrayActive { get; private set; }

    internal string TrayTip { get; private set; } = string.Empty;

    internal void ActivateTray()
    {
        TrayActive = true;
        SyncTray();
    }

    internal void DeactivateTray()
    {
        if (!TrayActive) return;
        TrayActive = false;
        TrayHost.Remove();
    }

    private void SyncTray()
    {
        if (!TrayActive) return;

        var phase = RecorderTray.PhaseOf(_session is not null, State);
        TrayTip = phase == TrayPhase.Idle
            ? Say("recorder.tray.idle", Say(RecorderTray.StateKey(phase)))
            : Say("recorder.tray.live", Say(RecorderTray.StateKey(phase)), ElapsedText, RecorderTray.Megabytes(_session?.WrittenMb ?? 0, VidShrink.App.Localization.Strings.Culture));

        var key = RecorderTray.BrushKey(phase);
        var found = this.TryFindResource(key, out var value)
            || (Avalonia.Application.Current?.TryGetResource(key, ActualThemeVariant, out value) ?? false);
        var color = found && value is ISolidColorBrush brush
            ? brush.Color
            : Colors.Transparent;
        TrayHost.Update(phase, color, TrayTip);
    }

    private void RestoreWindow()
    {
        if (MiniOpen) return;
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            owner.Show();
            owner.Activate();
        }
    }
}
