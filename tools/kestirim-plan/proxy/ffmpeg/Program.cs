using System.Diagnostics;

const string SelfName = "ffmpeg";
var counterFile = Environment.GetEnvironmentVariable("KESTIRIM_SAYAC_FFMPEG");
KestirimSayac.Increment(counterFile);

var real = KestirimSayac.FindRealTool(SelfName, AppContext.BaseDirectory);
if (real is null)
{
    Console.Error.WriteLine($"{SelfName} bulunamadi (kestirim-plan sayac vekili).");
    return 127;
}

var psi = new ProcessStartInfo(real) { UseShellExecute = false };
foreach (var a in args) psi.ArgumentList.Add(a);
using var p = Process.Start(psi)!;
p.WaitForExit();
return p.ExitCode;

static class KestirimSayac
{
    private static readonly Mutex Lock = new(false, "Global\\KestirimPlanSayacMutex");

    public static void Increment(string? counterFile)
    {
        if (string.IsNullOrEmpty(counterFile)) return;
        try
        {
            Lock.WaitOne();
            try
            {
                long n = 0;
                if (File.Exists(counterFile)) long.TryParse(File.ReadAllText(counterFile).Trim(), out n);
                File.WriteAllText(counterFile, (n + 1).ToString());
            }
            finally { Lock.ReleaseMutex(); }
        }
        catch { }
    }

    public static string? FindRealTool(string name, string selfDir)
    {
        var exe = name + (OperatingSystem.IsWindows() ? ".exe" : "");
        var selfFull = Path.GetFullPath(selfDir.TrimEnd(Path.DirectorySeparatorChar));
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            string full;
            try { full = Path.Combine(dir.Trim('"'), exe); }
            catch (ArgumentException) { continue; }
            var dirFull = Path.GetFullPath(Path.GetDirectoryName(full) ?? "");
            if (string.Equals(dirFull, selfFull, StringComparison.OrdinalIgnoreCase)) continue;
            if (File.Exists(full)) return full;
        }
        return null;
    }
}
