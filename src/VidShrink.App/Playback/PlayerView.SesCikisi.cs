using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using VidShrink.App.Localization;
using VidShrink.Player;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    internal AudioOutput AudioOutput => Settings.AudioOutput;

    internal void SelectAudioDevice(string name)
    {
        var device = string.IsNullOrEmpty(name) ? AudioDevice.Auto : name;
        Settings.AudioDevice = device == AudioDevice.Auto ? null : device;
        SaveSettings();
        _engine?.SetAudioDevice(device);
        _trace.Add("audio-device -> " + device);
    }

    internal void ToggleAudioPassthrough() => UseAudioOutput(AudioOutput with { Passthrough = !AudioOutput.Passthrough });

    internal void ToggleAudioExclusive() => UseAudioOutput(AudioOutput with { Exclusive = !AudioOutput.Exclusive });

    internal void ToggleAudioStereo() => UseAudioOutput(AudioOutput with { Stereo = !AudioOutput.Stereo });

    private void UseAudioOutput(AudioOutput output)
    {
        Settings.AudioOutput = output;
        SaveSettings();
        _engine?.SetAudioOutput(output);
        _trace.Add($"audio-output -> spdif {YesNo(output.Passthrough)}, exclusive {YesNo(output.Exclusive)}, stereo {YesNo(output.Stereo)}");
        AdvancedChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string YesNo(bool value) => value ? "yes" : "no";

    /// <summary>
    /// Duz cikis motora yazilmaz. Kayitli aygit motorun listesinde yoksa (cikarilmis kulaklik)
    /// yazilmaz ve otomatik kalir; ayar silinmez, aygit geri takilinca sonraki acilista yine secilir.
    /// </summary>
    private void ApplyAudioOutput(IPlaybackEngine engine)
    {
        if (!Settings.AudioOutput.IsPlain) engine.SetAudioOutput(Settings.AudioOutput);
        if (Settings.AudioDevice is { } device && engine.AudioDevices.Any(known => known.Name == device))
            engine.SetAudioDevice(device);
    }

    private List<Control> AudioOutputItems()
    {
        var devices = _engine?.AudioDevices.ToList() ?? new List<AudioDevice>();
        if (devices.Count == 0 || devices[0].Name != AudioDevice.Auto) devices.Insert(0, new AudioDevice(AudioDevice.Auto, ""));
        var current = _engine?.CurrentAudioDevice ?? AudioDevice.Auto;
        return devices.Select(device =>
        {
            var name = device.Name;
            var header = name == AudioDevice.Auto ? Strings.Get("player.tracks.output-auto") : device.Description;
            return (Control)Choice(header, name == current, () => SelectAudioDevice(name));
        }).ToList();
    }
}
