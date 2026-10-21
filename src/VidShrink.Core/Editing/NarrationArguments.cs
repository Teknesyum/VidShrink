using System.Globalization;

namespace VidShrink.Core.Editing;

public static class NarrationArguments
{
    public const string FolderName = "seslendirme";

    public const string Extension = ".m4a";

    public const string Bitrate = "192k";

    public static AudioCapturePlan Plan(AudioCaptureDevice microphone, IReadOnlyCollection<AudioCaptureDevice> listed, AudioFilterOptions? filters = null)
    {
        ArgumentNullException.ThrowIfNull(microphone);
        var options = (filters ?? AudioFilterOptions.None) with { PushToTalk = false };
        return AudioCaptureArguments.Build(new AudioCaptureSelection(microphone), listed, 0, AudioTrackLayout.MixedSingleTrack, options);
    }

    public static AudioCaptureDevice? PickMicrophone(IEnumerable<AudioCaptureDevice> listed, string? preferred)
    {
        ArgumentNullException.ThrowIfNull(listed);
        var microphones = listed.Where(device => device.Role == AudioSourceRole.Microphone).ToArray();
        return microphones.FirstOrDefault(device => string.Equals(device.Name, preferred, StringComparison.Ordinal)) ?? microphones.FirstOrDefault();
    }

    public static IReadOnlyList<string> Build(AudioCapturePlan plan, string output)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(output);
        if (plan.InputCount != 1) throw new ArgumentException("Seslendirme tek mikrofonla kaydedilir", nameof(plan));
        var args = new List<string> { "-hide_banner", "-loglevel", "error", "-y" };
        args.AddRange(plan.Inputs);
        if (plan.FilterComplex is { } graph) args.AddRange(new[] { "-filter_complex", graph });
        foreach (var map in plan.Maps) args.AddRange(new[] { "-map", map });
        args.AddRange(new[] { "-vn", "-c:a", "aac", "-b:a", Bitrate, output });
        return args;
    }

    public static string FileName(DateTime local) => "anlatim-" + local.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + Extension;
}
