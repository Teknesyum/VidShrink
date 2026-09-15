using VidShrink.Core;

namespace VidShrink.Launcher;

/// <summary>
/// Açılış perdesi. Ölçüm şunu söylüyordu: çift tıktan ilk kareye 1,75 saniye geçiyor ve o
/// sürenin tamamı boş ekran. Perde o boşluğu dolduruyor — uygulama doğduğu anda başlatıcının
/// paneli ekrana geliyor ve uygulamanın "ilk karem ekranda" işaretini bekliyor.
///
/// <para>Panel yeni değil: kurulum ilerlemesini çizen <see cref="SplashWindow"/> burada da
/// kullanılıyor, yalnız eşiği sıfır. Yeni çatı, yeni süreç, yerleşik bekleyen hiçbir şey
/// yok; perde kapanınca başlatıcı da çıkıyor.</para>
///
/// <para>İşaret adlandırılmış bir olay. Adı her açılışta yeniden üretiliyor ve çocuk sürece
/// <see cref="Degisken"/> ile geçiyor: iki VidShrink aynı anda açılırsa biri diğerinin
/// perdesini kaldıramaz. İşaret gelmezse <see cref="Tavan"/> dolunca perde yine kalkıyor.</para>
/// </summary>
internal sealed class AcilisPerdesi : IDisposable
{
    internal const string Degisken = "VIDSHRINK_ACILIS_PERDESI";

    /// <summary>İşaret hiç gelmezse perdenin kendi kendine kalktığı süre.</summary>
    internal static readonly TimeSpan Tavan = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Çubuğun süründüğü tavan. Yüzde yüz yalan olurdu: iş bitmiyor, bekleniyor.
    /// </summary>
    private const double CubukTavani = 92;

    private readonly EventWaitHandle _olay;
    private readonly SplashGate _kapi;
    private int _kapandi;

    private AcilisPerdesi(string ad, EventWaitHandle olay, SplashGate kapi)
    {
        Ad = ad;
        _olay = olay;
        _kapi = kapi;
    }

    /// <summary>Çocuk sürece geçirilecek olay adı.</summary>
    internal string Ad { get; }

    /// <summary>
    /// Perdeyi açar. Panel çizilemezse ya da olay kurulamazsa <c>null</c> döner: açılış
    /// perdesiz sürer, hiçbir yol bu yüzden durmaz.
    /// </summary>
    internal static AcilisPerdesi? Ac()
    {
        try
        {
            var ad = "VidShrinkPerde-" + Guid.NewGuid().ToString("N");
            var olay = new EventWaitHandle(false, EventResetMode.ManualReset, ad);
            var ilerleme = new InstallProgress();
            ilerleme.Step(0, CubukTavani, "VidShrink açılıyor");
            return new AcilisPerdesi(ad, olay, SplashGate.Arm(ilerleme, TimeSpan.Zero));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>İşareti ya da tavanı bekler, sonra perdeyi kaldırır.</summary>
    internal void BekleVeKapat()
    {
        try { _olay.WaitOne(Tavan); }
        catch (Exception) { }
        Dispose();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _kapandi, 1) != 0) return;
        _kapi.Kapat();
        _olay.Dispose();
    }
}
