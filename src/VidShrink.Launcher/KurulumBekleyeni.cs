using System;
using System.IO;
using System.Threading;
using VidShrink.Core;

namespace VidShrink.Launcher;

internal static class KurulumBekleyeni
{
    internal static readonly TimeSpan ArkaPlanBeklemesi = TimeSpan.FromDays(7);

    internal static readonly TimeSpan ElleBekleme = TimeSpan.FromSeconds(10);

    internal static readonly TimeSpan KilitBeklemesi = TimeSpan.FromMinutes(10);

    internal static string Ad(string appDirectory) =>
        UygulamaKlasoruKapisi.Ad(appDirectory).Replace(".UygulamaKlasoru.", ".KurulumBekleyeni.", StringComparison.Ordinal);

    internal static bool Calistir(
        string baseDirectory, string appDirectory, bool elle, string kilitAdi, Func<StagedUpdate?> indir, bool prova)
    {
        using var bekleyen = new Mutex(initiallyOwned: false, Ad(appDirectory));
        if (!elle && !Tut(bekleyen, TimeSpan.Zero)) return false;
        try
        {
            StagedUpdate? staged;
            using (var kilit = new Mutex(initiallyOwned: false, kilitAdi))
            {
                if (!Tut(kilit, elle ? KilitBeklemesi : TimeSpan.Zero)) return false;
                try { staged = indir(); }
                catch (Exception) { return false; }
                finally { Birak(kilit); }
            }

            if (staged is null) return false;
            if (prova) return false;
            return Kur(baseDirectory, appDirectory, staged, kilitAdi, elle ? ElleBekleme : ArkaPlanBeklemesi);
        }
        finally
        {
            if (!elle) Birak(bekleyen);
        }
    }

    internal static bool Kur(string baseDirectory, string appDirectory, StagedUpdate staged, string kilitAdi, TimeSpan bekleme)
    {
        var kapi = UygulamaKlasoruKapisi.BosalincaAl(appDirectory, bekleme);
        if (kapi is null) return false;
        try
        {
            using var kilit = new Mutex(initiallyOwned: false, kilitAdi);
            if (!Tut(kilit, KilitBeklemesi)) return false;
            try
            {
                if (Kurulmus(appDirectory, staged)) return false;
                var stage = Path.Combine(baseDirectory, UpdateStaging.StageDirectoryName);
                LauncherUpdate.Stage(stage, baseDirectory, staged.Launcher);
                var applied = UpdateRollout.Apply(stage, baseDirectory, appDirectory, staged.App, staged.Launcher, staged.Manifest, staged.Shell);
                UygulamaKlasoruKapisi.HatayiSil(appDirectory);
                return applied;
            }
            catch (Exception exception)
            {
                UygulamaKlasoruKapisi.HataYaz(appDirectory, exception.Message);
                return false;
            }
            finally { Birak(kilit); }
        }
        finally
        {
            UygulamaKlasoruKapisi.Birak(kapi);
        }
    }

    internal static bool Kurulmus(string appDirectory, StagedUpdate staged) =>
        string.Equals(UpdateCheck.ReadVersionMarker(appDirectory), staged.Manifest.Version, StringComparison.Ordinal);

    private static bool Tut(Mutex mutex, TimeSpan sure)
    {
        try { return mutex.WaitOne(sure); }
        catch (AbandonedMutexException) { return true; }
    }

    private static void Birak(Mutex mutex)
    {
        try { mutex.ReleaseMutex(); }
        catch (ApplicationException) { }
    }
}
