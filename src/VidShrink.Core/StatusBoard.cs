namespace VidShrink.Core;

public sealed class StatusBoard
{
    public string Tools { get; private set; } = "";

    public string? Applied { get; private set; }

    public string ReportTools(string text)
    {
        Tools = text;
        return Tools;
    }

    public string ReportApplied(string version)
    {
        Applied = version;
        return Applied;
    }

    public void ClearApplied() => Applied = null;
}
