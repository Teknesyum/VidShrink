using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;

namespace VidShrink.Tests;

/// <summary>
/// Canlandırma ve isabet ölçüleri için gerçek saat yerine Avalonia'nın kendi saatini sayar.
/// <c>TopLevel.RequestAnimationFrame</c> geri çağrısı genel animasyon saatinin her vuruşunda,
/// canlandırma gözlemcilerinden hemen önce koşar; saat vurmadıkça (yük altında bekleyen
/// kompozisyon toplusu, ağır yerleşim) kare sayılmaz, canlandırma da ilerlemez. Böylece
/// "N kare geçti" ölçüsü makinenin hızından bağımsızdır. <see cref="Sinir"/> yalnız kırmızı
/// kolun asılı kalmaması içindir, yeşil kolun süresi değildir.
/// </summary>
internal static class KareSayaci
{
    public static readonly TimeSpan Sinir = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Saat vurdukça sayar ve UI iş parçacığını pompalar; <paramref name="yeter"/> geçen kare
    /// sayısıyla doğru dönene ya da <see cref="Sinir"/> dolana dek sürer. <paramref name="oku"/>
    /// her karede ve her pompa diliminden sonra çağrılır. Geçen kare sayısını döner.
    /// </summary>
    public static int Pompala(TopLevel pencere, Func<int, bool> yeter, Action? oku = null)
    {
        var kare = 0;
        var bitti = false;
        void Iste() => pencere.RequestAnimationFrame(_ =>
        {
            if (bitti) return;
            kare++;
            oku?.Invoke();
            Iste();
        });

        Iste();
        var saat = Stopwatch.StartNew();
        try
        {
            while (!yeter(kare) && saat.Elapsed < Sinir)
            {
                using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(2));
                Dispatcher.UIThread.MainLoop(dilim.Token);
                oku?.Invoke();
            }
        }
        finally { bitti = true; }

        return kare;
    }

    /// <summary>
    /// Pencerenin o anki sahnesi çizim iş parçacığında çizilene dek bekler. <c>InputHitTest</c>
    /// UI ağacına değil, çizim iş parçacığının son tamamladığı karenin geri okumasına bakar;
    /// o kare yazılmadan isabet hiçbir şey döndürmez. İki saat vuruşu yerleşimin ve
    /// çizicinin kendi toplusunun gönderildiğini garanti eder, ardından istenen toplunun
    /// <c>Rendered</c> olayı (toplular sırayla uygulanır) öncekilerin de çizildiğini söyler.
    /// </summary>
    public static bool SahneCizilsin(TopLevel pencere)
    {
        Pompala(pencere, k => k >= 2);
        var gorsel = ElementComposition.GetElementVisual(pencere) ?? throw new InvalidOperationException("Pencerenin kompozisyon görseli yok.");
        var toplu = gorsel.Compositor.RequestCompositionBatchCommitAsync();
        var saat = Stopwatch.StartNew();
        while (!toplu.Rendered.IsCompleted && saat.Elapsed < Sinir)
        {
            using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(2));
            Dispatcher.UIThread.MainLoop(dilim.Token);
        }

        return toplu.Rendered.IsCompleted;
    }
}
