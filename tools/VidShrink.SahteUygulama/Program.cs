using System.Diagnostics;
using System.Globalization;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Launcher;

namespace VidShrink.SahteUygulama;

internal static class Program
{
    internal const string IsaretDegiskeni = "VIDSHRINK_SAHTE_ISARET";
    internal const string OmurDegiskeni = "VIDSHRINK_SAHTE_OMUR_MS";
    internal const string KilitDosyasi = "kilit.dll";
    internal const string YukleDegiskeni = "VIDSHRINK_SAHTE_YUKLE";
    internal const string YeniOmurDegiskeni = "VIDSHRINK_SAHTE_YENI_OMUR_MS";

    [STAThread]
    private static int Main(string[] args)
    {
        if (UygulamaKlasoruKapisi.BaslaticiyaDevret(AppContext.BaseDirectory, args))
        {
            Yaz("devretti", "");
            return 0;
        }

        var klasor = AppContext.BaseDirectory;
        Yaz("acildi", Environment.GetEnvironmentVariable(LauncherUpdate.LaunchedVariable) ?? "");

        var kilitYolu = Path.Combine(klasor, KilitDosyasi);
        if (Environment.GetEnvironmentVariable(YukleDegiskeni) == "1")
        {
            if (File.Exists(kilitYolu)) Yaz("kilit", File.ReadAllText(kilitYolu));
            Yukle(klasor.TrimEnd(Path.DirectorySeparatorChar));
            return 0;
        }

        FileStream? kilit = null;
        if (File.Exists(kilitYolu))
        {
            kilit = new FileStream(kilitYolu, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var okuyucu = new StreamReader(kilit, leaveOpen: true);
            Yaz("kilit", okuyucu.ReadToEnd());
        }

        var omur = int.TryParse(Environment.GetEnvironmentVariable(OmurDegiskeni), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) ? ms : 2000;
        Thread.Sleep(omur);
        kilit?.Dispose();
        Yaz("kapandi", "");
        return 0;
    }

    /// <summary>
    /// Uygulamanın "Yükle"sini taklit eder: arka plan başlatıcısı sahneyi indirip kapıda
    /// beklemeye geçene kadar bekler, sonra hızlı yolu dener; olmazsa uygulamanın yaptığı
    /// gibi başlatıcıyı <c>--update-now</c> ile açıp çıkar.
    /// </summary>
    private static void Yukle(string appDirectory)
    {
        var baseDirectory = Path.GetDirectoryName(appDirectory)!;
        var muhur = Path.Combine(baseDirectory, UpdateStaging.StageDirectoryName, StageSeal.FileName);
        var bitis = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < bitis && !(Tutuluyor(KurulumBekleyeni.Ad(appDirectory)) && File.Exists(muhur) && !Tutuluyor(UpdateStaging.MutexName)))
            Thread.Sleep(20);
        Thread.Sleep(300);

        var sahne = UpdateStaging.StageAsync(
            baseDirectory, appDirectory, Environment.GetEnvironmentVariable("VIDSHRINK_UPDATE_SOURCE"),
            UpdateStaging.LauncherLanes, null, "VidShrink-Sahte", null, CancellationToken.None).GetAwaiter().GetResult();
        var yeniOmur = Environment.GetEnvironmentVariable(YeniOmurDegiskeni) ?? "1500";
        var ek = new Dictionary<string, string> { [YukleDegiskeni] = "0", [OmurDegiskeni] = yeniOmur };

        Yaz("yukle-basladi", Tutuluyor(KurulumBekleyeni.Ad(appDirectory)) ? "yuva-dolu" : "yuva-bos");
        if (sahne is not null && YerindeGuncelleme.Uygula(baseDirectory, appDirectory, sahne))
        {
            using (YerindeGuncelleme.YeniSurumuAc(baseDirectory, appDirectory, ek)) { }
            Yaz("yukle", "hizli");
            return;
        }

        var start = new ProcessStartInfo { FileName = Path.Combine(baseDirectory, "VidShrink.exe"), UseShellExecute = false };
        start.ArgumentList.Add(LauncherUpdate.UpdateNowArgument);
        start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        foreach (var (ad, deger) in ek) start.Environment[ad] = deger;
        using (Process.Start(start)) { }
        Yaz("yukle", "eski");
    }

    private static bool Tutuluyor(string ad)
    {
        using var mutex = new Mutex(initiallyOwned: false, ad);
        try
        {
            if (!mutex.WaitOne(TimeSpan.Zero)) return true;
        }
        catch (AbandonedMutexException)
        {
        }
        mutex.ReleaseMutex();
        return false;
    }

    private static void Yaz(string olay, string deger)
    {
        var klasor = Environment.GetEnvironmentVariable(IsaretDegiskeni);
        if (string.IsNullOrEmpty(klasor)) return;
        var ad = string.Create(CultureInfo.InvariantCulture, $"{DateTime.UtcNow.Ticks}-{Environment.ProcessId}-{olay}.txt");
        File.WriteAllText(Path.Combine(klasor, ad), deger);
    }
}
