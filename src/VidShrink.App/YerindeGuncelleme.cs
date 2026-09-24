using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using VidShrink.Core;
using VidShrink.Launcher;

namespace VidShrink.App;

/// <summary>
/// "Yükle"nin hızlı yolu: güncelleme uygulamanın kendi sürecinde, yerinde uygulanır
/// (<see cref="InPlaceUpdate"/>), yeni sürüm açılır, bu süreç kapanır. Başlatıcı, çıkış
/// beklemesi ve ikinci doğrulama yok.
///
/// <para>Kilitler yenileri değil, başlatıcının kısa süre tuttuklarıdır: uygulama klasörü
/// kapısı ve güncelleme kilidi, ikisi de beklemeden. Bekleyen yuvası
/// (<see cref="KurulumBekleyeni.Ad"/>) alınmaz: arka plan başlatıcısı indirdiği sahneyi
/// kurmak için bu uygulamanın kapanmasını beklerken yuvayı günlerce tutar, kapıyı ise
/// beklerken bırakır. Güncellemenin olduğu tipik an budur; yuvaya bağlı kalan hızlı yol
/// orada hep düşerdi. Takastan sonra o başlatıcı kapıyı alınca sürüm işaretini yeni
/// görür ve kurmadan çekilir (<see cref="KurulumBekleyeni.Kurulmus(string, StagedUpdate)"/>).
/// Kapı ya da kilit tutuluyorsa (başka bir kopya şu an yazıyor, indirme sürüyor) ya da
/// klasörden koşan başka bir uygulama varsa hızlı yol denenmez, çağıran eski yola düşer.</para>
/// </summary>
internal static class YerindeGuncelleme
{
    /// <summary>
    /// Yeni sürüme, kapanmasını beklemesi gereken önceki sürecin kimliğini taşır. Tek
    /// örnek kanalı eski süreç kapanana kadar onun elinde; beklemeyen yeni süreç isteğini
    /// ona iletip çıkardı.
    /// </summary>
    internal const string OncekiSurecDegiskeni = "VIDSHRINK_ONCEKI_SUREC";

    /// <summary>Önceki sürecin kapanması için tanınan en uzun süre.</summary>
    internal static readonly TimeSpan OncekiSurecBeklemesi = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Sahneyi yerinde uygular. Döndürdüğü değer uygulamanın yeni sürümle açılabileceğidir;
    /// yanlışsa hiçbir dosya değişmemiştir ve çağıran eski yolu sürmelidir.
    /// </summary>
    internal static bool Uygula(string baseDirectory, string appDirectory, StagedUpdate staged, string? kilitAdi = null)
    {
        var kapi = UygulamaKlasoruKapisi.Al(appDirectory, TimeSpan.Zero);
        if (kapi is null) return false;
        try
        {
            using var kilit = new Mutex(initiallyOwned: false, kilitAdi ?? UpdateStaging.MutexName);
            if (!Tut(kilit)) return false;
            try
            {
                if (KosanVar(appDirectory)) return false;
                if (KurulumBekleyeni.Kurulmus(appDirectory, staged)) return false;
                var baslaticiBekliyor = InPlaceUpdate.Apply(baseDirectory, appDirectory, staged);
                UygulamaKlasoruKapisi.HatayiSil(appDirectory);
                if (baslaticiBekliyor) GecisiBaslat(baseDirectory);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally { Birak(kilit); }
        }
        finally { UygulamaKlasoruKapisi.Birak(kapi); }
    }

    /// <summary>
    /// Yeni sürümü başlatıcının açtığı gibi açar: aynı çalışma klasörü, PATH'in başında
    /// kökteki ffmpeg, başlatıcıdan doğmuş işareti. Ek olarak kapanmasını beklesin diye bu
    /// sürecin kimliği geçer.
    /// </summary>
    internal static Process? YeniSurumuAc(string baseDirectory, string appDirectory, IReadOnlyDictionary<string, string>? ek = null)
    {
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(appDirectory, "VidShrink.App.exe"),
            WorkingDirectory = appDirectory,
            UseShellExecute = false
        };
        start.Environment["PATH"] =
            Path.Combine(baseDirectory, "tools", "ffmpeg") + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
        start.Environment[LauncherUpdate.LaunchedVariable] = "1";
        start.Environment[OncekiSurecDegiskeni] = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        if (ek is not null)
        {
            foreach (var (ad, deger) in ek) start.Environment[ad] = deger;
        }
        return Process.Start(start);
    }

    /// <summary>
    /// Açılışta koşar: yeni sürüm hızlı yoldan doğduysa önceki sürecin çıkmasını bekler,
    /// değişkeni siler ki bu sürecin doğuracağı süreçlere geçmesin.
    /// </summary>
    internal static void OncekiSureciBekle()
    {
        var deger = Environment.GetEnvironmentVariable(OncekiSurecDegiskeni);
        if (string.IsNullOrEmpty(deger)) return;
        Environment.SetEnvironmentVariable(OncekiSurecDegiskeni, null);
        if (!int.TryParse(deger, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)) return;
        if (pid == Environment.ProcessId) return;
        try
        {
            using var onceki = Process.GetProcessById(pid);
            onceki.WaitForExit(OncekiSurecBeklemesi);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
        }
    }

    private static bool KosanVar(string appDirectory)
    {
        var kosanlar = UygulamaKlasoruKapisi.KlasordenKosanlar(appDirectory);
        foreach (var surec in kosanlar) surec.Dispose();
        return kosanlar.Count > 0;
    }

    private static void GecisiBaslat(string baseDirectory)
    {
        var incoming = LauncherUpdate.Incoming(baseDirectory, LauncherUpdate.ExecutableName);
        if (!File.Exists(incoming)) return;
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = incoming,
                WorkingDirectory = baseDirectory,
                UseShellExecute = false
            };
            start.ArgumentList.Add(LauncherUpdate.CommitArgument);
            using var surec = Process.Start(start);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception
            or IOException)
        {
        }
    }

    private static bool Tut(Mutex mutex)
    {
        try { return mutex.WaitOne(TimeSpan.Zero); }
        catch (AbandonedMutexException) { return true; }
    }

    private static void Birak(Mutex mutex)
    {
        try { mutex.ReleaseMutex(); }
        catch (ApplicationException) { }
    }
}
