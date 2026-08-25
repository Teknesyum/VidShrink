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
    public void ToolStatusArrivingLaterDoesNotOverwriteTheAppliedLine()
    {
        var board = new StatusBoard();
        board.ReportApplied("1.5.0");

        board.ReportTools("FFmpeg: C:\\tools\\ffmpeg.exe\nVersion: reading...");
        board.ReportTools("FFmpeg: C:\\tools\\ffmpeg.exe\nVersion: 7.1");

        Assert.Equal("1.5.0", board.Applied);
        Assert.DoesNotContain("1.5.0", board.Tools);
    }

    [Fact]
    public void AnAppliedLineArrivingLaterDoesNotOverwriteTheToolStatus()
    {
        var board = new StatusBoard();
        board.ReportTools("FFmpeg: C:\\tools\\ffmpeg.exe");

        board.ReportApplied("1.5.0");

        Assert.Equal("FFmpeg: C:\\tools\\ffmpeg.exe", board.Tools);
    }

    [Fact]
    public void DismissingTheAppliedLineLeavesTheToolStatusStanding()
    {
        var board = new StatusBoard();
        board.ReportTools("FFmpeg: C:\\tools\\ffmpeg.exe");
        board.ReportApplied("1.5.0");

        board.ClearApplied();

        Assert.Null(board.Applied);
        Assert.Equal("FFmpeg: C:\\tools\\ffmpeg.exe", board.Tools);
    }

    [Fact]
    public void TheAppliedLineIsTranslated()
    {
        Assert.Equal("Yeni sürüme geçildi", TipSources.ReadCatalogue()["Updated to a new version"]);
    }
}
