using System.Text.RegularExpressions;
using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class AppliedUpdateTests : IDisposable
{
    private readonly string _appDirectory =
        Path.Combine(Path.GetTempPath(), "vidshrink-applied-" + Guid.NewGuid().ToString("N"));

    public AppliedUpdateTests() => Directory.CreateDirectory(_appDirectory);

    public void Dispose()
    {
        try { Directory.Delete(_appDirectory, true); }
        catch (IOException) { }
    }

    private string Marker => Path.Combine(_appDirectory, AppliedUpdateNotice.MarkerFileName);

    [Fact]
    public void Load_ReadsTheVersionAndLeavesTheMarkerInPlace()
    {
        AppliedUpdateNotice.Write(_appDirectory, "1.5.0");

        var notice = new AppliedUpdateNotice(_appDirectory);

        Assert.True(notice.Load());
        Assert.Equal("1.5.0", notice.Version);
        Assert.True(File.Exists(Marker));
    }

    [Fact]
    public void AnUnseenNoticeIsReportedAgainOnTheNextLaunch()
    {
        AppliedUpdateNotice.Write(_appDirectory, "1.5.0");

        var crashed = new AppliedUpdateNotice(_appDirectory);
        crashed.Load();

        var nextLaunch = new AppliedUpdateNotice(_appDirectory);

        Assert.True(nextLaunch.Load());
        Assert.Equal("1.5.0", nextLaunch.Version);
    }

    [Fact]
    public void Shown_RemovesTheMarkerSoTheLineDoesNotRepeat()
    {
        AppliedUpdateNotice.Write(_appDirectory, "1.5.0");
        var notice = new AppliedUpdateNotice(_appDirectory);
        notice.Load();

        notice.Shown();

        Assert.False(File.Exists(Marker));
        Assert.Equal("1.5.0", notice.Version);
        Assert.False(new AppliedUpdateNotice(_appDirectory).Load());
    }

    [Fact]
    public void Shown_WithoutAnythingToShow_DoesNotTouchTheMarker()
    {
        AppliedUpdateNotice.Write(_appDirectory, "1.5.0");

        new AppliedUpdateNotice(_appDirectory).Shown();

        Assert.True(File.Exists(Marker));
    }

    [Fact]
    public void Load_IgnoresAnEmptyMarker()
    {
        AppliedUpdateNotice.Write(_appDirectory, "   ");

        var notice = new AppliedUpdateNotice(_appDirectory);

        Assert.False(notice.Load());
        Assert.Null(notice.Version);
        Assert.False(notice.IsPending);
    }

    [Fact]
    public void TheToolStatusAndTheAppliedLineAreTwoSeparateControls()
    {
        var xaml = File.ReadAllText(TipSources.WindowXamlPath);

        Assert.Contains("x:Name=\"TxtSystemStatus\"", xaml);
        Assert.Contains("x:Name=\"TxtAppliedVersion\"", xaml);
        Assert.Single(Regex.Matches(xaml, "x:Name=\"TxtSystemStatus\""));
        Assert.Single(Regex.Matches(xaml, "x:Name=\"TxtAppliedVersion\""));
    }

    [Fact]
    public void NeitherLineIsWrittenFromTheOthersSource()
    {
        var code = File.ReadAllText(TipSources.WindowCodePath);

        foreach (var line in Writes(code, "TxtSystemStatus"))
            Assert.DoesNotContain("_appliedNotice", line);

        var applied = Writes(code, "TxtAppliedVersion").ToList();
        Assert.NotEmpty(applied);
        Assert.All(applied, line => Assert.Contains("_appliedNotice", line));
    }

    private static IEnumerable<string> Writes(string code, string control) =>
        code.ReplaceLineEndings("\n").Split('\n').Where(l => l.Contains(control + ".Text ="));

    [Fact]
    public void TheAppliedLineIsTranslated()
    {
        Assert.Equal("Yeni sürüme geçildi", Locales.TurkishFor("Updated to a new version"));
    }
}
