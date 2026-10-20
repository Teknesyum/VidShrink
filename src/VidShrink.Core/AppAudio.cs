namespace VidShrink.Core;

public sealed record AppAudioSession(int ProcessId, string ProcessName, bool Active);

public static class AppAudio
{
    public const int MinimumBuild = 19041;

    public const string NamePrefix = LoopbackAudio.LoopbackName + " app:";

    public const string PlaceholderName = LoopbackAudio.LoopbackName + " app";

    public const string TokenInfix = "-app-";

    public const int ActivationProcessLoopback = 1;

    public const int IncludeTargetProcessTree = 0;

    public static AudioCaptureDevice Placeholder { get; } =
        new(PlaceholderName, CaptureBackend.WasapiLoopback, AudioSourceRole.SystemAudio, LoopbackAudio.PipeToken);

    public static bool Supported(bool isWindows, Version? version)
        => isWindows && version is { } v && (v.Major > 10 || (v.Major == 10 && v.Build >= MinimumBuild));

    public static bool IsPlaceholder(AudioCaptureDevice? device)
        => device is { Backend: CaptureBackend.WasapiLoopback }
           && string.Equals(device.Name, PlaceholderName, StringComparison.Ordinal);

    public static AudioCaptureDevice For(string processName)
    {
        var name = Clean(processName);
        return new AudioCaptureDevice(NamePrefix + name, CaptureBackend.WasapiLoopback, AudioSourceRole.SystemAudio, Token(name));
    }

    public static string Token(string processName) => LoopbackAudio.PipeToken + TokenInfix + Clean(processName);

    public static string? ProcessName(string? deviceName)
        => deviceName is not null
           && deviceName.Length > NamePrefix.Length
           && deviceName.StartsWith(NamePrefix, StringComparison.Ordinal)
            ? deviceName[NamePrefix.Length..]
            : null;

    public static string? ProcessName(AudioCaptureDevice? device)
        => device is { Backend: CaptureBackend.WasapiLoopback } ? ProcessName(device.Name) : null;

    public static bool TryReadToken(string? argument, out string? processName)
    {
        processName = null;
        if (argument is null || !argument.StartsWith(LoopbackAudio.PipeToken, StringComparison.Ordinal)) return false;
        if (argument.Length == LoopbackAudio.PipeToken.Length) return true;

        var rest = argument[LoopbackAudio.PipeToken.Length..];
        if (!rest.StartsWith(TokenInfix, StringComparison.Ordinal) || rest.Length == TokenInfix.Length) return false;

        processName = rest[TokenInfix.Length..];
        return true;
    }

    public static IReadOnlyList<AudioCaptureDevice> Compose(
        IReadOnlyList<AudioCaptureDevice> devices,
        bool supported,
        IEnumerable<AppAudioSession> sessions,
        int ownProcessId,
        string? remembered)
    {
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(sessions);

        var list = devices.Where(d => !IsPlaceholder(d) && ProcessName(d) is null).ToList();
        if (!supported)
        {
            list.Add(Placeholder);
            return list;
        }

        var names = sessions
            .Where(s => s.ProcessId > 0 && s.ProcessId != ownProcessId && Usable(s.ProcessName))
            .Select(s => s.ProcessName)
            .ToList();
        if (ProcessName(remembered) is { } kept && Usable(kept)) names.Add(kept);

        list.AddRange(names
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(For));
        return list;
    }

    public static int? PickProcess(string processName, IEnumerable<AppAudioSession> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return candidates
            .Where(c => c.ProcessId > 0 && string.Equals(c.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.Active)
            .Select(c => (int?)c.ProcessId)
            .FirstOrDefault();
    }

    public static byte[] ActivationParams(int processId)
    {
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId), processId, "surec kimligi pozitif olmali.");

        var bytes = new byte[12];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), ActivationProcessLoopback);
        BitConverter.TryWriteBytes(bytes.AsSpan(4, 4), processId);
        BitConverter.TryWriteBytes(bytes.AsSpan(8, 4), IncludeTargetProcessTree);
        return bytes;
    }

    private static bool Usable(string? name)
        => !string.IsNullOrWhiteSpace(name) && name.IndexOfAny(['\\', '/', '"']) < 0;

    private static string Clean(string processName)
    {
        if (!Usable(processName))
            throw new ArgumentException("surec adi bos ya da yol karakteri tasiyor.", nameof(processName));
        return processName.Trim();
    }
}
