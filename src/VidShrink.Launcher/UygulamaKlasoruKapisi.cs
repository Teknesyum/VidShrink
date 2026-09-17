using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using VidShrink.Core;

namespace VidShrink.Launcher;

internal static class UygulamaKlasoruKapisi
{
    internal const string HataIsareti = ".bakim-hatasi";

    internal const string GecikmeDegiskeni = "VIDSHRINK_BAKIM_GECIKMESI_MS";

    internal const string UygulamaSurecAdi = "VidShrink.App";

    internal static string Ad(string appDirectory)
    {
        var yol = Path.GetFullPath(appDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        var ozet = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(yol)));
        return @"Local\Teknesyum.VidShrink.UygulamaKlasoru." + ozet[..16];
    }

    internal static Mutex? Al(string appDirectory, TimeSpan sure)
    {
        var kapi = new Mutex(initiallyOwned: false, Ad(appDirectory));
        try
        {
            if (kapi.WaitOne(sure)) return kapi;
        }
        catch (AbandonedMutexException)
        {
            return kapi;
        }
        kapi.Dispose();
        return null;
    }

    internal static void Birak(Mutex? kapi)
    {
        if (kapi is null) return;
        try { kapi.ReleaseMutex(); }
        catch (ApplicationException) { }
        kapi.Dispose();
    }

    internal static bool Tutuluyor(string appDirectory)
    {
        var kapi = Al(appDirectory, TimeSpan.Zero);
        if (kapi is null) return true;
        Birak(kapi);
        return false;
    }

    internal static void Bekle(string appDirectory, TimeSpan sure) => Birak(Al(appDirectory, sure));

    internal static List<Process> KlasordenKosanlar(string appDirectory)
    {
        var klasor = Path.GetFullPath(appDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var kosanlar = new List<Process>();
        foreach (var surec in Process.GetProcessesByName(UygulamaSurecAdi))
        {
            var bizim = false;
            if (surec.Id != Environment.ProcessId)
            {
                try
                {
                    bizim = KlasordenMi(surec.MainModule?.FileName, klasor);
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                {
                    bizim = false;
                }
            }

            if (bizim) kosanlar.Add(surec);
            else surec.Dispose();
        }
        return kosanlar;
    }

    internal static bool KlasordenMi(string? dosya, string klasor) =>
        dosya is not null && string.Equals(
            Path.GetDirectoryName(dosya),
            klasor.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    internal static Mutex? BosalincaAl(string appDirectory, TimeSpan sure)
    {
        var bitis = DateTime.UtcNow + sure;
        while (true)
        {
            var kalan = bitis - DateTime.UtcNow;
            if (kalan < TimeSpan.Zero) return null;
            var kapi = Al(appDirectory, kalan);
            if (kapi is null) return null;

            var kosanlar = KlasordenKosanlar(appDirectory);
            if (kosanlar.Count == 0) return kapi;

            Birak(kapi);
            foreach (var surec in kosanlar)
            {
                using (surec)
                {
                    var bekleme = bitis - DateTime.UtcNow;
                    if (bekleme <= TimeSpan.Zero) continue;
                    try { surec.WaitForExit(bekleme > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : bekleme); }
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
                }
            }
        }
    }

    internal static bool Devretmeli(string? baslaticidan, bool baslaticiVar, bool kapiTutuluyor, bool bekleyenVar) =>
        string.IsNullOrEmpty(baslaticidan) && baslaticiVar && (kapiTutuluyor || bekleyenVar);

    internal static bool BaslaticiyaDevret(string appDirectory, IReadOnlyList<string> args)
    {
        if (!OperatingSystem.IsWindows()) return false;
        var baslaticidan = Environment.GetEnvironmentVariable(LauncherUpdate.LaunchedVariable);
        if (!string.IsNullOrEmpty(baslaticidan)) return false;

        var klasor = appDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var baslatici = LauncherUpdate.LocateLauncher(klasor);
        if (!Devretmeli(baslaticidan, baslatici is not null, Tutuluyor(klasor), UpdateStage.HasPending(klasor)))
            return false;

        var start = new ProcessStartInfo
        {
            FileName = baslatici!,
            WorkingDirectory = Path.GetDirectoryName(baslatici) ?? "",
            UseShellExecute = false
        };
        foreach (var arguman in args) start.ArgumentList.Add(arguman);
        using var surec = Process.Start(start);
        return surec is not null;
    }

    internal static void Gecikme()
    {
        var deger = Environment.GetEnvironmentVariable(GecikmeDegiskeni);
        if (int.TryParse(deger, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) && ms > 0)
            Thread.Sleep(ms);
    }

    internal static void HataYaz(string appDirectory, string neden)
    {
        try { File.WriteAllText(Path.Combine(appDirectory, HataIsareti), neden); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    internal static void HatayiSil(string appDirectory)
    {
        try { File.Delete(Path.Combine(appDirectory, HataIsareti)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
