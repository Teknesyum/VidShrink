using System.Globalization;

namespace VidShrink.Player;

public sealed partial class MpvEngine
{
    public const string SpdifCodecs = "ac3,eac3,dts,dts-hd,truehd";

    public IReadOnlyList<AudioDevice> AudioDevices
    {
        get
        {
            var count = ReadInt("audio-device-list/count");
            var devices = new List<AudioDevice>();
            for (var i = 0; i < count; i++)
            {
                var prefix = "audio-device-list/" + i.ToString(CultureInfo.InvariantCulture);
                var name = GetProperty(prefix + "/name");
                if (string.IsNullOrEmpty(name)) continue;
                var description = GetProperty(prefix + "/description");
                devices.Add(new AudioDevice(name, string.IsNullOrEmpty(description) ? name : description));
            }

            return devices;
        }
    }

    public string CurrentAudioDevice => GetProperty("audio-device") is { Length: > 0 } name ? name : AudioDevice.Auto;

    public bool SetAudioDevice(string name) => TrySet("audio-device", string.IsNullOrEmpty(name) ? AudioDevice.Auto : name);

    public AudioOutput Output => new(
        !string.IsNullOrEmpty(GetProperty("audio-spdif")),
        GetProperty("audio-exclusive") == "yes",
        GetProperty("audio-channels") == "stereo");

    public void SetAudioOutput(AudioOutput output)
    {
        TrySet("audio-spdif", output.Passthrough ? SpdifCodecs : "");
        TrySet("audio-exclusive", output.Exclusive ? "yes" : "no");
        TrySet("audio-channels", output.Stereo ? "stereo" : "auto-safe");
    }
}
