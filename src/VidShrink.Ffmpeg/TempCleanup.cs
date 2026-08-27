using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("VidShrink.Tests")]

namespace VidShrink.Ffmpeg;

/// <summary>
/// Gecici artiklari toplar. Iki soru ayri ayri cevaplanir: bir dosya <b>eski</b> mi, ve
/// bir dosya <b>baskasina ait</b> mi. Tek basina yas esigi ikincisini cozmez, cunku canli
/// bir sikistirma bir saniye once yazilmis dosyanin da sahibidir.
///
/// Her VidShrink sureci actiginda <c>%TEMP%</c> icine kendi kiralama dosyasini birakir:
/// <c>vidshrink_sahip_&lt;pid&gt;_&lt;baslangic&gt;.kilit</c>. Dosya <c>DeleteOnClose</c> ile
/// tutulur, yani surec duserse cekirdek onu kapatir ve kiralama kendiliginden yok olur.
/// Temizlik canli kiralamalarin en erken baslangic anini taban alir; o andan sonra
/// yazilmis hicbir artiga dokunmaz. Boylece ayni anda kosan iki VidShrink birbirinin
/// dosyasini silemez, cokme sonrasi artiklar ise ilk acilista gercekten temizlenir.
/// </summary>
public static class TempCleanup
{
    private const string OwnerPrefix = "vidshrink_sahip_";
    private const string OwnerSuffix = ".kilit";

    /// <summary>Kiralama kurulamazsa geri dusulen yas esigi.</summary>
    private static readonly TimeSpan Fallback = TimeSpan.FromHours(1);

    private static FileStream? _lease;

    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255",
        Justification = "Kiralama ilk artiktan once kurulmali. Bu modulu yukleyen her surec artik yazabilir, " +
            "dolayisiyla sahiplik iddiasi cagriya birakilamaz.")]
    internal static void ClaimOwnership() => ClaimOwnership(Path.GetTempPath());

    internal static void ClaimOwnership(string tempDir)
    {
        if (_lease is not null) return;

        try
        {
            using var self = Process.GetCurrentProcess();
            var path = Path.Combine(tempDir, LeaseName(self.Id, self.StartTime));
            _lease = new FileStream(path, FileMode.Create, FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.DeleteOnClose);
        }
        catch { _lease = null; }
    }

    public static void CleanupStaleArtifacts(string tempDir)
    {
        var floor = LiveOwnerFloor(tempDir);
        DeleteMatching(tempDir, "vidshrink_*", floor);
        DeleteMatching(tempDir, "*.partial", floor);
    }

    /// <summary>
    /// Canli sahiplerin en erken baslangic ani. Bu andan sonra yazilmis artik canli bir
    /// isin olabilir, dokunulmaz. Hic canli sahip bulunamazsa yas esigine geri dusulur.
    /// </summary>
    internal static DateTime LiveOwnerFloor(string tempDir)
    {
        var floor = DateTime.MaxValue;

        foreach (var lease in Leases(tempDir))
        {
            if (IsAlive(lease.Id, lease.Started)) { if (lease.Started < floor) floor = lease.Started; }
            else TryDelete(lease.Path);
        }

        return floor == DateTime.MaxValue ? DateTime.UtcNow - Fallback : floor;
    }

    private static IEnumerable<(string Path, int Id, DateTime Started)> Leases(string tempDir)
    {
        List<string> files;
        try { files = Directory.EnumerateFiles(tempDir, OwnerPrefix + "*" + OwnerSuffix).ToList(); }
        catch { yield break; }

        foreach (var file in files)
        {
            var parts = Path.GetFileNameWithoutExtension(file)[OwnerPrefix.Length..].Split('_');
            if (parts.Length != 2) continue;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) continue;
            if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)) continue;
            yield return (file, id, new DateTime(ticks, DateTimeKind.Utc));
        }
    }

    /// <summary>
    /// Pid'in canli olmasi yetmez: isletim sistemi pid'i geri kullanmis olabilir, o yuzden
    /// baslangic ani da tutmali. Baslangic okunamiyorsa canli sayilir — silmemek yaniltici
    /// silmekten ucuzdur.
    /// </summary>
    private static bool IsAlive(int id, DateTime started)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            if (process.HasExited) return false;
            try { return process.StartTime.ToUniversalTime() == started; }
            catch { return true; }
        }
        catch (ArgumentException) { return false; }
        catch { return true; }
    }

    private static string LeaseName(int id, DateTime started)
        => OwnerPrefix
            + id.ToString(CultureInfo.InvariantCulture) + "_"
            + started.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture)
            + OwnerSuffix;

    private static void DeleteMatching(string tempDir, string pattern, DateTime floor)
    {
        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(tempDir, pattern).ToList(); }
        catch { return; }

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            if (name.StartsWith(OwnerPrefix, StringComparison.Ordinal)) continue;
            if (Touched(file) >= floor) continue;
            TryDelete(file);
        }
    }

    /// <summary>Artigin son hareketi. Yazilmadan once olusturulmus olabilir, ikisinin gecidir.</summary>
    private static DateTime Touched(string file)
    {
        try
        {
            var info = new FileInfo(file);
            var written = info.LastWriteTimeUtc;
            var created = info.CreationTimeUtc;
            return created > written ? created : written;
        }
        catch { return DateTime.UtcNow; }
    }

    private static void TryDelete(string file)
    {
        try { File.Delete(file); } catch { }
    }
}
