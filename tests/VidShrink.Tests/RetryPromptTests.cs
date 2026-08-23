using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class RetryPromptTests
{
    [Fact]
    public async Task StoppingAtTheQuestionEndsTheRunAfterOneAttemptAndWritesNoFile()
    {
        if (!ToolLocator.IsAvailable(out _)) return;

        var dir = NewDirectory();
        try
        {
            var source = await MakeSourceAsync(dir);
            var outputPath = Path.Combine(dir, "result.mp4");
            var prompts = new List<RetryPrompt>();

            var result = await new EncodeRunner().RunAsync(
                InfoFor(source), Plan(), outputPath, targetMb: 0.001, progress: null, ct: CancellationToken.None,
                fillPolicy: FillPolicy.QualityCeiling, profile: null,
                askBeforeRetry: (prompt, _) => { prompts.Add(prompt); return Task.FromResult(false); });

            Assert.Single(prompts);
            Assert.Equal(1, prompts[0].Attempt);
            Assert.Equal(1, result.Attempts);
            Assert.False(result.Success);
            Assert.True(result.CeilingExceeded);
            Assert.False(File.Exists(outputPath), "Leaving it as is must never hand back a file larger than the target.");
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public async Task TheQuestionCarriesTheOverrunAndHowLongTheAttemptTook()
    {
        if (!ToolLocator.IsAvailable(out _)) return;

        var dir = NewDirectory();
        try
        {
            var source = await MakeSourceAsync(dir);
            var outputPath = Path.Combine(dir, "result.mp4");
            RetryPrompt? seen = null;

            await new EncodeRunner().RunAsync(
                InfoFor(source), Plan(), outputPath, targetMb: 0.001, progress: null, ct: CancellationToken.None,
                fillPolicy: FillPolicy.QualityCeiling, profile: null,
                askBeforeRetry: (prompt, _) => { seen ??= prompt; return Task.FromResult(false); });

            Assert.NotNull(seen);
            Assert.Equal(3, seen!.MaxAttempts);
            Assert.Equal(0.001, seen.TargetMb, 6);
            Assert.True(seen.ActualMb > seen.TargetMb);
            Assert.True(seen.OverMb > 0);
            Assert.True(seen.OverPercent > 0);
            Assert.True(seen.AttemptDuration > TimeSpan.Zero);
            Assert.False(seen.HasUnderBandFallback);
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public async Task SayingTryAgainRunsAnotherAttemptAndTheCeilingStillStopsTheAsking()
    {
        if (!ToolLocator.IsAvailable(out _)) return;

        var dir = NewDirectory();
        try
        {
            var source = await MakeSourceAsync(dir);
            var outputPath = Path.Combine(dir, "result.mp4");
            var asked = new List<int>();

            var result = await new EncodeRunner().RunAsync(
                InfoFor(source), Plan(), outputPath, targetMb: 0.001, progress: null, ct: CancellationToken.None,
                fillPolicy: FillPolicy.QualityCeiling, profile: null,
                askBeforeRetry: (prompt, _) => { asked.Add(prompt.Attempt); return Task.FromResult(true); });

            // Asked after attempt 1 and 2; at the MaxAttempts ceiling the run finishes without asking.
            Assert.Equal(new[] { 1, 2 }, asked);
            Assert.Equal(3, result.Attempts);
            Assert.False(result.Success);
            Assert.True(result.CeilingExceeded);
            Assert.False(File.Exists(outputPath));
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public async Task CancellingWhileTheQuestionIsOpenCancelsTheRunAndLeavesNoFile()
    {
        if (!ToolLocator.IsAvailable(out _)) return;

        var dir = NewDirectory();
        try
        {
            var source = await MakeSourceAsync(dir);
            var outputPath = Path.Combine(dir, "result.mp4");
            using var cts = new CancellationTokenSource();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new EncodeRunner().RunAsync(
                InfoFor(source), Plan(), outputPath, targetMb: 0.001, progress: null, ct: cts.Token,
                fillPolicy: FillPolicy.QualityCeiling, profile: null,
                askBeforeRetry: (_, ct) => { cts.Cancel(); ct.ThrowIfCancellationRequested(); return Task.FromResult(true); }));

            Assert.False(File.Exists(outputPath));
        }
        finally { Cleanup(dir); }
    }

    [Fact]
    public async Task WithoutAQuestionHandlerTheEngineKeepsItsHeadlessBehaviour()
    {
        if (!ToolLocator.IsAvailable(out _)) return;

        var dir = NewDirectory();
        try
        {
            var source = await MakeSourceAsync(dir);
            var outputPath = Path.Combine(dir, "result.mp4");

            var result = await new EncodeRunner().RunAsync(
                InfoFor(source), Plan(), outputPath, targetMb: 0.001, progress: null, ct: CancellationToken.None,
                fillPolicy: FillPolicy.QualityCeiling);

            Assert.Equal(3, result.Attempts);
            Assert.True(result.CeilingExceeded);
            Assert.False(File.Exists(outputPath));
        }
        finally { Cleanup(dir); }
    }

    private static string NewDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_retryprompt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Cleanup(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch { }
    }

    private static async Task<string> MakeSourceAsync(string dir)
    {
        var source = Path.Combine(dir, "source.mp4");
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
        {
            "-y", "-f", "lavfi", "-i", "testsrc=size=320x240:rate=10:duration=2",
            "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", source
        }) psi.ArgumentList.Add(arg);

        using var make = new System.Diagnostics.Process { StartInfo = psi };
        make.Start();
        var drain = Task.WhenAll(make.StandardOutput.ReadToEndAsync(), make.StandardError.ReadToEndAsync());
        await make.WaitForExitAsync();
        await drain;
        Assert.True(File.Exists(source));
        return source;
    }

    private static MediaInfo InfoFor(string source) => new()
    {
        FilePath = source,
        FileSizeBytes = new FileInfo(source).Length,
        DurationSeconds = 2,
        Width = 320,
        Height = 240,
        Fps = 10,
        VideoCodec = "h264",
        TotalBitrateBps = 400_000
    };

    private static EncodePlan Plan() => new()
    {
        Codec = "libx264",
        Mode = "crf",
        Crf = 18,
        VideoBitrateK = 2000,
        AudioCodec = null,
        AudioBitrateK = 0,
        Width = 320,
        Height = 240,
        Fps = 10,
        Preset = "ultrafast"
    };
}
