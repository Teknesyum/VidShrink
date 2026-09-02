using System.Diagnostics;
using System.Globalization;

namespace VidShrink.SahneButcesi;

public sealed record KabukSonuc(int Code, string StdErr, TimeSpan Elapsed, bool TimedOut);

public static class Kabuk
{
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(40);

    public static KabukSonuc Kos(string exe, IEnumerable<string> args, TimeSpan? timeout = null)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        var clock = Stopwatch.StartNew();
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"baslatilamadi: {exe}");
        var errTask = p.StandardError.ReadToEndAsync();
        var outTask = p.StandardOutput.ReadToEndAsync();
        if (!p.WaitForExit((int)(timeout ?? Timeout).TotalMilliseconds))
        {
            try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            clock.Stop();
            return new KabukSonuc(-1, "zaman asimi", clock.Elapsed, true);
        }
        clock.Stop();
        var err = errTask.GetAwaiter().GetResult();
        _ = outTask.GetAwaiter().GetResult();
        return new KabukSonuc(p.ExitCode, Tail(err), clock.Elapsed, false);
    }

    public static (int Code, string Out) Yakala(string exe, IEnumerable<string> args, TimeSpan? timeout = null)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"baslatilamadi: {exe}");
        var outTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit((int)(timeout ?? Timeout).TotalMilliseconds))
        {
            try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            return (-1, string.Empty);
        }
        var text = outTask.GetAwaiter().GetResult();
        _ = errTask.GetAwaiter().GetResult();
        return (p.ExitCode, text);
    }

    private static string Tail(string text)
    {
        var lines = text.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('\n', lines.TakeLast(6));
    }

    public static string Inv(double value, string format = "0.####")
        => value.ToString(format, CultureInfo.InvariantCulture);
}
