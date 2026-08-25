namespace VidShrink.Core;

public sealed class AppliedUpdateNotice
{
    public const string MarkerFileName = ".update-applied";

    private readonly string _marker;

    public AppliedUpdateNotice(string appDirectory)
        => _marker = Path.Combine(appDirectory, MarkerFileName);

    public string? Version { get; private set; }

    public bool IsPending => Version is not null;

    public static void Write(string appDirectory, string version)
        => File.WriteAllText(Path.Combine(appDirectory, MarkerFileName), version);

    public bool Load()
    {
        try
        {
            if (!File.Exists(_marker)) return false;
            var version = File.ReadAllText(_marker).Trim();
            if (version.Length == 0) return false;
            Version = version;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Shown()
    {
        if (Version is null) return;
        try { File.Delete(_marker); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
