using System.Threading;

namespace VidShrink.App;

/// <summary>
/// Başlatıcının açılışta tuttuğu perdeyi kaldıran işaret. Perde başlatıcının çıplak Win32
/// paneli; uygulama doğar doğmaz ekranda duruyor ve arkasında gerçek açılış koşuyor.
///
/// <para>Perde bir örtü değil: gerçek kare gelir gelmez kalkıyor. İşaret adlandırılmış bir
/// olay ve adı <see cref="Degisken"/> ile bu sürece geçiyor; değişken boşsa perde yoktur ve
/// bu sınıf hiçbir şey yapmaz. İşaret bir kez kurulur, ikinci çağrı sessizce döner.</para>
///
/// <para>İşaret hiç gelmezse perde kendi tavanıyla kapanıyor; uygulama çökse bile ekranda
/// asılı bir panel kalmıyor. Perde yalnız Windows başlatıcısında var; başka yerde bu
/// sınıf hiç çalışmaz.</para>
/// </summary>
internal static class AcilisPerdesi
{
    internal const string Degisken = "VIDSHRINK_ACILIS_PERDESI";

    private static int _kuruldu;

    internal static void Kapat()
    {
        if (!OperatingSystem.IsWindows()) return;
        if (Interlocked.Exchange(ref _kuruldu, 1) != 0) return;

        var ad = Environment.GetEnvironmentVariable(Degisken);
        if (string.IsNullOrWhiteSpace(ad)) return;

        try
        {
            using var olay = EventWaitHandle.OpenExisting(ad);
            olay.Set();
        }
        catch (Exception)
        {
        }
    }
}
