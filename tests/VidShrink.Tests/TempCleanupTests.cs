using System.Diagnostics;
using System.Globalization;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class TempCleanupTests
{
    private static string LeaseName(int id, DateTime started)
        => "vidshrink_sahip_" + id.ToString(CultureInfo.InvariantCulture) + "_"
            + started.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture) + ".kilit";

    private static void Age(string path, DateTime moment)
    {
        File.SetCreationTimeUtc(path, moment);
        File.SetLastWriteTimeUtc(path, moment);
    }

    private static int DeadProcessId()
    {
        for (var candidate = 999_000; candidate < 999_400; candidate += 4)
        {
            try { using var live = Process.GetProcessById(candidate); }
            catch (ArgumentException) { return candidate; }
        }

        throw new InvalidOperationException("olu pid bulunamadi");
    }

    [Fact]
    public void CleanupRemovesVidshrinkPrefixedAndPartialFilesButKeepsOthers()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var stale = DateTime.UtcNow.AddDays(-2);
            var passLog = Path.Combine(dir, "vidshrink_abc123-0.log");
            var palette = Path.Combine(dir, "vidshrink_" + Guid.NewGuid().ToString("N") + ".png");
            var partial = Path.Combine(dir, "output.mp4.partial");
            var unrelated = Path.Combine(dir, "keep.txt");
            File.WriteAllText(passLog, "x");
            File.WriteAllText(palette, "x");
            File.WriteAllText(partial, "x");
            File.WriteAllText(unrelated, "x");
            Age(passLog, stale);
            Age(palette, stale);
            Age(partial, stale);
            Age(unrelated, stale);

            TempCleanup.CleanupStaleArtifacts(dir);

            Assert.False(File.Exists(passLog));
            Assert.False(File.Exists(palette));
            Assert.False(File.Exists(partial));
            Assert.True(File.Exists(unrelated));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>
    /// K7. Sinir yas degil sahiplik. Canli sahibin baslangicindan bir saniye sonra yazilmis
    /// artik duruyor; bir saniye once yazilmis olan gidiyor. Ikisi de saniyeler yasinda, yani
    /// bir yas esigi bu ikisini birbirinden ayiramaz.
    /// </summary>
    [Fact]
    public void CleanupSparesWhatALiveOwnerCouldHaveWrittenAndTakesTheRest()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var self = Process.GetCurrentProcess();
            var started = self.StartTime.ToUniversalTime();
            File.WriteAllText(Path.Combine(dir, LeaseName(self.Id, self.StartTime)), "");

            Assert.Equal(started, TempCleanup.LiveOwnerFloor(dir));

            var live = Path.Combine(dir, "vidshrink_canli-0.log");
            var orphan = Path.Combine(dir, "vidshrink_oksuz-0.log");
            File.WriteAllText(live, "x");
            File.WriteAllText(orphan, "x");
            Age(live, started.AddSeconds(1));
            Age(orphan, started.AddSeconds(-1));

            TempCleanup.CleanupStaleArtifacts(dir);

            Assert.True(File.Exists(live), "Canli sahibin yazmis olabilecegi artik silinemez.");
            Assert.False(File.Exists(orphan), "Hicbir canli sahibe ait olamayacak artik durmamali.");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>
    /// Cokme sonrasi. Olu surecin kiralamasi tabani asagi cekmez ve kendisi de toplanir —
    /// yoksa bir cokme butun temizligi sonsuza kadar askiya alirdi.
    /// </summary>
    [Fact]
    public void ADeadOwnersLeaseIsCollectedAndDoesNotHoldTheFloorDown()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var self = Process.GetCurrentProcess();
            var started = self.StartTime.ToUniversalTime();
            File.WriteAllText(Path.Combine(dir, LeaseName(self.Id, self.StartTime)), "");

            var dead = Path.Combine(dir, LeaseName(DeadProcessId(), DateTime.UtcNow.AddDays(-3)));
            File.WriteAllText(dead, "");

            var leftover = Path.Combine(dir, "vidshrink_cokme-0.log");
            File.WriteAllText(leftover, "x");
            Age(leftover, DateTime.UtcNow.AddDays(-3));

            TempCleanup.CleanupStaleArtifacts(dir);

            Assert.False(File.Exists(dead), "Olu surecin kiralamasi diskte kalmamali.");
            Assert.False(File.Exists(leftover), "Cokmus surecin artigi ilk temizlikte gitmeli.");
            Assert.Equal(started, TempCleanup.LiveOwnerFloor(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>Kiralama dosyasi da <c>vidshrink_*</c> desenine uyar; kendini silmemeli.</summary>
    [Fact]
    public void CleanupNeverDeletesALiveLease()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vidshrink_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var self = Process.GetCurrentProcess();
            var lease = Path.Combine(dir, LeaseName(self.Id, self.StartTime));
            File.WriteAllText(lease, "");
            Age(lease, DateTime.UtcNow.AddDays(-3));

            TempCleanup.CleanupStaleArtifacts(dir);

            Assert.True(File.Exists(lease));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>
    /// K8. Suit kendi dosyasini yiyemez. Arayuz sinifi Avalonia'yi kaldirdiginda
    /// <c>App.OnFrameworkInitializationCompleted</c> gercek <c>%TEMP%</c> uzerinde temizlik
    /// kosturuyor; o kosunun aninda yazilmis bir olcum artigina dokunmadigini burada
    /// pinliyoruz. Kirmiziya donerse suit yine kosumdan kosuma degisir.
    /// </summary>
    [Fact]
    public void TheAppBootSweepLeavesThisRunsOwnArtifactsAlone()
    {
        AppHost.Ensure();

        var temp = Path.GetTempPath();
        var passLog = Path.Combine(temp, "vidshrink_" + Guid.NewGuid().ToString("N") + "-0.log");
        var vmaf = Path.Combine(temp, "vidshrink_vmaf_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(passLog, "x");
            File.WriteAllText(vmaf, "{}");

            TempCleanup.CleanupStaleArtifacts(temp);

            Assert.True(File.Exists(passLog), "Suren bir kodlamanin gecis gunlugu silinemez.");
            Assert.True(File.Exists(vmaf), "Suren bir VMAF olcumunun ciktisi silinemez.");
        }
        finally
        {
            try { File.Delete(passLog); } catch { }
            try { File.Delete(vmaf); } catch { }
        }
    }
}
