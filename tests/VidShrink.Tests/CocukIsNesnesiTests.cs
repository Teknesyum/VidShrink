using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class CocukIsNesnesiTests
{
    private static Process Bekleyen()
    {
        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("/d");
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add("pause");
        var surec = Process.Start(psi)!;
        _ = surec.StandardOutput.ReadToEndAsync();
        _ = surec.StandardError.ReadToEndAsync();
        return surec;
    }

    [Fact]
    public void IsNesnesiKapaninicaBagliSurecOlur()
    {
        if (!OperatingSystem.IsWindows()) return;

        using var surec = Bekleyen();
        try
        {
            var is_ = new ChildJob();
            Assert.True(is_.Attach(surec));
            Assert.False(surec.WaitForExit(500));

            is_.Dispose();

            Assert.True(surec.WaitForExit(5000));
        }
        finally
        {
            try { if (!surec.HasExited) surec.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        }
    }

    [Fact]
    public void BaglanmayanSurecIsNesnesiKapaninicaYasar()
    {
        if (!OperatingSystem.IsWindows()) return;

        using var surec = Bekleyen();
        try
        {
            using (new ChildJob()) { }

            Assert.False(surec.WaitForExit(500));
        }
        finally
        {
            try { if (!surec.HasExited) surec.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        }
    }

    [Fact]
    public async Task TestKonagindaGercekYoklamaSurecAcmaz()
    {
        Assert.True(DdagrabProbe.Disabled);

        var cikis = new DdaOutput(0, 0, 0, 1920, 1080);
        var istek = KaydediciDdagrabTests.Bolge(0, 0, 640, 480, new[] { cikis });
        Assert.Equal(DdagrabFallback.None, RecorderArguments.DdagrabBlocker(istek));

        var yoklama = DdagrabProbe.WorksAsync(istek);
        Assert.True(yoklama.IsCompleted);
        Assert.False(await yoklama);
    }
}
