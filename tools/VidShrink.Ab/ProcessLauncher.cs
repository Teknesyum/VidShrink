using System.Diagnostics;

namespace VidShrink.Ab;

public static class ProcessLauncher
{
    public static ProcessStartInfo StartInfo(string fileName, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        return psi;
    }

    public static string CommandLine(string fileName, IEnumerable<string> args)
        => fileName + " " + string.Join(' ', args.Select(a => a.Contains(' ') ? "\"" + a + "\"" : a));

    public static async Task<(int ExitCode, string Stderr)> RunAsync(string fileName, IReadOnlyList<string> args, CancellationToken ct)
    {
        using var process = new Process { StartInfo = StartInfo(fileName, args) };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync(ct);
        return (process.ExitCode, await stdout + Environment.NewLine + await stderr);
    }
}
