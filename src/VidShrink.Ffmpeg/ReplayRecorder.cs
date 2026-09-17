using System.Diagnostics;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

public sealed record ReplaySaveResult(bool Ok, string? Target, int Parts, string Error);

public interface IReplayBuffer
{
    int Seconds { get; }

    Task<ReplaySaveResult> SaveAsync(string target, CancellationToken ct = default);

    Task StopAsync();
}

public sealed class ReplayRecorder : IReplayBuffer
{
    public const int StartTimeoutMs = 15_000;

    public const int StopTimeoutMs = 10_000;

    private readonly Process _process;
    private readonly string _folder;
    private readonly Task<string> _stderr;
    private bool _stopped;

    private ReplayRecorder(Process process, string folder, int seconds, Task<string> stderr)
    {
        _process = process;
        _folder = folder;
        Seconds = seconds;
        _stderr = stderr;
    }

    public int Seconds { get; }

    public string Folder => _folder;

    public bool Running => !_stopped && !_process.HasExited;

    public static Task<ReplayRecorder> StartAsync(RecorderRequest request, string folder, int seconds, CancellationToken ct = default)
        => StartAsync(ReplayBuffer.BuildCapture(request, folder, seconds), folder, seconds, ct);

    public static async Task<ReplayRecorder> StartAsync(IReadOnlyList<string> args, string folder, int seconds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);

        var list = args.ToList();
        var info = ToolLocator.StartInfo(ToolLocator.Ffmpeg, list);
        info.RedirectStandardInput = true;
        var process = new Process { StartInfo = info };
        process.Start();
        _ = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        var recorder = new ReplayRecorder(process, folder, seconds, stderr);

        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < StartTimeoutMs)
        {
            if (process.HasExited)
            {
                var error = FfmpegRunner.Tail(await stderr);
                TryDelete(folder);
                throw new InvalidOperationException($"ffmpeg kayit tamponunu baslatamadi ({process.ExitCode}): {error}");
            }

            if (Parts(folder).Any(p => p.Length > 0)) return recorder;
            try { await Task.Delay(100, ct); }
            catch (OperationCanceledException)
            {
                await recorder.StopAsync();
                throw;
            }
        }

        await recorder.StopAsync();
        throw new InvalidOperationException("ffmpeg kayit tamponu ilk parcayi yazmadi.");
    }

    public static IReadOnlyList<ReplayPart> Parts(string folder)
    {
        if (!Directory.Exists(folder)) return Array.Empty<ReplayPart>();
        var parts = new List<ReplayPart>();
        foreach (var file in Directory.GetFiles(folder, ReplayBuffer.PartPrefix + "*.mkv"))
        {
            try
            {
                var info = new FileInfo(file);
                parts.Add(new ReplayPart(file, info.LastWriteTimeUtc, info.Length));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        return parts;
    }

    public async Task<ReplaySaveResult> SaveAsync(string target, CancellationToken ct = default)
    {
        var picked = ReplayBuffer.Pick(Parts(_folder), Seconds);
        if (picked.Count == 0) return new ReplaySaveResult(false, null, 0, "no parts");

        var copies = Path.Combine(_folder, "kaydet");
        if (Directory.Exists(copies)) Directory.Delete(copies, true);
        Directory.CreateDirectory(copies);
        var local = new List<string>(picked.Count);
        foreach (var part in picked)
        {
            var copy = Path.Combine(copies, Path.GetFileName(part));
            try
            {
                using var source = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var destination = File.Create(copy);
                await source.CopyToAsync(destination, ct);
                local.Add(copy);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        var listPath = Path.Combine(copies, ReplayBuffer.ListName);
        await File.WriteAllTextAsync(listPath, ReplayBuffer.ConcatList(local), ct);
        var folder = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        var run = await FfmpegRunner.RunAsync(ReplayBuffer.BuildSave(listPath, target), ct);
        TryDelete(copies);
        return run.Ok && File.Exists(target)
            ? new ReplaySaveResult(true, target, local.Count, string.Empty)
            : new ReplaySaveResult(false, null, local.Count, run.StandardError);
    }

    public async Task StopAsync()
    {
        if (_stopped) return;
        _stopped = true;
        try
        {
            if (!_process.HasExited)
            {
                try
                {
                    await _process.StandardInput.WriteAsync('q');
                    await _process.StandardInput.FlushAsync();
                }
                catch (IOException) { }

                using var wait = new CancellationTokenSource(StopTimeoutMs);
                try { await _process.WaitForExitAsync(wait.Token); }
                catch (OperationCanceledException)
                {
                    try { _process.Kill(true); } catch (InvalidOperationException) { }
                    await _process.WaitForExitAsync(CancellationToken.None);
                }
            }

            try { await _stderr; } catch (IOException) { }
        }
        finally
        {
            _process.Dispose();
            TryDelete(_folder);
        }
    }

    private static void TryDelete(string folder)
    {
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
