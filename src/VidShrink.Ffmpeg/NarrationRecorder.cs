using System.Diagnostics;

namespace VidShrink.Ffmpeg;

public sealed class NarrationRecorder : IAsyncDisposable
{
    public const int StopTimeoutMs = 5_000;

    private readonly Process _process;
    private readonly Task _stdout;
    private readonly Task<string> _stderr;

    private NarrationRecorder(Process process, string output)
    {
        _process = process;
        Output = output;
        _stdout = process.StandardOutput.ReadToEndAsync();
        _stderr = process.StandardError.ReadToEndAsync();
    }

    public string Output { get; }

    public bool Running => !_process.HasExited;

    public static NarrationRecorder Start(IReadOnlyList<string> arguments, string output)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var folder = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        var startInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, arguments);
        startInfo.RedirectStandardInput = true;
        var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch
        {
            process.Dispose();
            throw;
        }

        return new NarrationRecorder(process, output);
    }

    public async Task<FfmpegRun> StopAsync(int timeoutMs = StopTimeoutMs)
    {
        var clock = Stopwatch.StartNew();
        FfmpegRunner.RequestGracefulStop(_process);
        using var wait = new CancellationTokenSource(timeoutMs);
        try
        {
            await _process.WaitForExitAsync(wait.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { _process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            await _process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        await _stdout.ConfigureAwait(false);
        var text = await _stderr.ConfigureAwait(false);
        var written = File.Exists(Output) && new FileInfo(Output).Length > 0;
        return new FfmpegRun(_process.ExitCode == 0 && written, _process.ExitCode, FfmpegRunner.Tail(text), clock.Elapsed);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited) await StopAsync().ConfigureAwait(false);
        _process.Dispose();
    }
}
