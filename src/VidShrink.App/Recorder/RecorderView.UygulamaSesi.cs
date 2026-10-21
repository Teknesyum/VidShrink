using System;
using System.Collections.Generic;
using Avalonia.Controls;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.App.Recorder;

internal partial class RecorderView
{
    internal const string AppAudioRequiresKey = "recorder.audio.app.requires";

    internal static bool AppAudioSupported => AppAudio.Supported(OperatingSystem.IsWindows(), Environment.OSVersion.Version);

    private void InitUygulamaSesi()
    {
        CmbSystemAudio.ContainerPrepared += (_, e) => ExplainAppAudio(CmbSystemAudio, e.Container, e.Index);
    }

    private IReadOnlyList<AudioCaptureDevice> DefaultAudioDevices()
    {
        var supported = AppAudioSupported;
        return AppAudio.Compose(
            CaptureDevices.Instance.Audio,
            supported,
            supported ? AppAudioSessions.List() : Array.Empty<AppAudioSession>(),
            Environment.ProcessId,
            SelectedName(CmbSystemAudio) ?? _settings.SystemAudioName);
    }

    internal static string? AppAudioLabel(string name)
    {
        if (string.Equals(name, AppAudio.PlaceholderName, StringComparison.Ordinal)) return Say("recorder.audio.app.off");
        return AppAudio.ProcessName(name) is { } process ? Say("recorder.audio.app", process) : null;
    }

    private static void ExplainAppAudio(ComboBox box, Control container, int index)
    {
        if (container is not ComboBoxItem item) return;
        var placeholder = box.Tag is IReadOnlyList<string> names
                          && index >= 0 && index < names.Count
                          && string.Equals(names[index], AppAudio.PlaceholderName, StringComparison.Ordinal);
        item.IsEnabled = !placeholder;
        ToolTip.SetTip(item, placeholder ? Say(AppAudioRequiresKey) : null);
        ToolTip.SetShowOnDisabled(item, placeholder);
    }
}
