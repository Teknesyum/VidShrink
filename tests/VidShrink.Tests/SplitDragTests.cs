using System.Diagnostics;
using System.Reflection;
using Avalonia;
using VidShrink.App.Playback;

namespace VidShrink.Tests;

/// <summary>
/// T53: ayırıcı sürüklenirken video duraksıyordu. Ölçüm (gerçek pencere, ekran dışı,
/// 500 Hz hareket) duraksamanın yerleşim turunda değil <b>çizim</b> katmanında olduğunu
/// gösterdi: her fare hareketi yüzeyi geçersiz kılıyor, her geçersiz kılma sunum
/// döngüsüne bir tur daha açtırıyordu. Boşta 224 tur/sn olan döngü sürüklerken
/// 477 tur/sn'ye çıkıyor ve her tur tam boy yüzeyi baştan boyuyordu (1902x988'de
/// 3,16 ms). Ham sayılar <c>.calisma/t53/olcum.txt</c> içinde.
///
/// Buradaki ölçüler o düzeltmeyi sabitliyor: ayırıcının yazılması tek başına çizim
/// açmaz; kare akarken hiç açmaz, kare gelmezken en fazla ekran aralığında bir kez açar.
/// </summary>
public sealed class SplitDragTests
{
    private static readonly PixelSize Combined = new(64, 32);

    private static readonly MethodInfo RoundMethod = typeof(ComparisonSurface)
        .GetMethod("Round", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly FieldInfo RunningField = typeof(ComparisonSurface)
        .GetField("_running", BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>
    /// Yüzey görsel ağaca bağlanmadan sunum turu koşmaz; ölçümde ağaç yok, bu yüzden
    /// koşma bayrağı doğrudan açılıyor. Tur elle çevriliyor: gerçek uygulamada onu
    /// <c>RequestAnimationFrame</c> çeviriyor.
    /// </summary>
    private static ComparisonSurface Ready() => Ready(new ComparisonSurface());

    private static ComparisonSurface Ready(SahteSaat saat) => Ready(new ComparisonSurface(saat.Now));

    private static ComparisonSurface Ready(ComparisonSurface surface)
    {
        surface.Configure(Combined);
        RunningField.SetValue(surface, true);
        return surface;
    }

    /// <summary>
    /// T127: yüzeyin boşta çizim açma kararı iki süre eşiğine bakıyor. Ölçü kendi
    /// <see cref="Stopwatch"/>unu açtığında iki saat bağımsızdı ve aralarına giren her
    /// duraklama kararı değiştiriyordu — ölçü CI'da üç kez düştü. Süre artık burada
    /// ilerliyor; ölçünün gördüğü tek zaman bu.
    /// </summary>
    private sealed class SahteSaat
    {
        private readonly long _origin = Stopwatch.GetTimestamp();

        public TimeSpan Elapsed { get; private set; }

        public long Now() => _origin + (long)(Elapsed.TotalSeconds * Stopwatch.Frequency);

        public void Advance(TimeSpan span) => Elapsed += span;
    }

    private static void Round(ComparisonSurface surface) => RoundMethod.Invoke(surface, new object?[] { TimeSpan.Zero });

    private static void Feed(ComparisonSurface surface) => surface.Submit(surface.Rent());

    // ---- K3: düzeltmeyi sabitleyen ölçüler -----------------------------------------

    /// <summary>
    /// Kare akarken ayırıcı tek bir fazladan çizim açmaz: sunulan her kare yüzeyi zaten
    /// baştan çiziyor, sınır o çizimde yerine oturuyor. Yirmi tur, tur başına sekiz
    /// hareket — yirmi çizim.
    /// </summary>
    [Fact]
    public void Akan_karede_ayirici_fazladan_cizim_actirmaz()
    {
        var repaints = AppHost.Run(() =>
        {
            var surface = Ready();
            var before = surface.Repaints;

            for (var tur = 0; tur < 20; tur++)
            {
                for (var k = 0; k < 8; k++) surface.Split = 0.2 + 0.6 * (tur * 8 + k) / 160.0;
                Feed(surface);
                Round(surface);
            }

            return (surface.Repaints - before, surface.IdleRounds);
        });

        Assert.Equal(20, repaints.Item1);

        // Süreye bakan tek dal boş turun içinde; her turda kare beslendiği için o dala
        // hiç girilmedi. Ölçünün duvar saatinden bağımsızlığı bu sayıyla kanıtlanıyor.
        Assert.Equal(0, repaints.Item2);
    }

    /// <summary>
    /// Kare akıyorken araya giren boş turlar da çizim açmaz. Akış duraksadı sayılmadan
    /// önce ayırıcı kendi çizimini açarsa sürükleme yine döngüyü fareye bağlar.
    /// </summary>
    [Fact]
    public void Kare_arasindaki_bos_turda_ayirici_cizim_acmaz()
    {
        var saat = new SahteSaat();

        var (repaints, moves) = AppHost.Run(() =>
        {
            var surface = Ready(saat);
            Feed(surface);
            Round(surface);

            var before = surface.Repaints;
            var count = 0;

            // Akış duraksadı sayılmasına 1 ms kala dur: 0,2 ms'lik adımlarla 99 ms.
            while (saat.Elapsed < TimeSpan.FromMilliseconds(99))
            {
                saat.Advance(TimeSpan.FromMilliseconds(0.2));
                surface.Split = 0.2 + 0.6 * (++count % 400) / 400.0;
                Round(surface);
            }

            return (surface.Repaints - before, count);
        });

        Assert.Equal(495, moves);
        Assert.Equal(0, repaints);
    }

    /// <summary>
    /// Kare gelmiyorsa (duraklatılmış, boru tıkalı) ayırıcı takılı kalmasın diye çizim
    /// açılır — ama ekran aralığında bir kez. Ölçüm boşta bile 217 tur/sn dönüyor;
    /// tur başına bir çizim hâlâ saniyede yüzlerce çizim demekti.
    /// </summary>
    [Fact]
    public void Kare_gelmezken_ayirici_cizimi_ekran_araliginda_bir_kez_acar()
    {
        var saat = new SahteSaat();

        var (repaints, moves) = AppHost.Run(() =>
        {
            var surface = Ready(saat);

            // Akışın durduğuna karar verilecek kadar ilerlet, sonra ölçmeye başla.
            saat.Advance(TimeSpan.FromMilliseconds(150));
            Round(surface);

            var before = surface.Repaints;
            var count = 0;
            var son = saat.Elapsed + TimeSpan.FromMilliseconds(250);

            while (saat.Elapsed < son)
            {
                saat.Advance(TimeSpan.FromMilliseconds(0.5));
                surface.Split = 0.2 + 0.6 * (++count % 400) / 400.0;
                Round(surface);
            }

            return (surface.Repaints - before, count);
        });

        Assert.Equal(500, moves);

        // 250 ms'de 60 Hz tavanı: adım 0,5 ms olduğu için çizimler 16,5-17 ms'de bir
        // düşüyor. Sayı artık bant değil, tek bir tam sayı.
        Assert.Equal(15, repaints);
    }

    [Fact]
    public void Ayirici_yazilan_konuma_gider_ve_uclarda_kirpilir()
    {
        var (orta, alt, ust) = AppHost.Run(() =>
        {
            var surface = Ready();
            surface.Split = 0.25;
            var a = surface.Split;
            surface.Split = -1;
            var b = surface.Split;
            surface.Split = 2;
            return (a, b, surface.Split);
        });

        Assert.Equal(0.25, orta, 9);
        Assert.Equal(0, alt, 9);
        Assert.Equal(1, ust, 9);
    }

    /// <summary>
    /// Yarım pikselden az kayan yazış çizim açmaz ama yutulmaz da: birikince sınır yerine
    /// gider. Klavye adımı (yüzde bir) her ölçüde bu eşiğin çok üstünde kalır.
    /// </summary>
    [Fact]
    public void Yarim_pikselden_kucuk_kayma_birikir_klavye_adimi_gecer()
    {
        var (klavye, ufak, birikmis) = AppHost.Run(() =>
        {
            var surface = Ready();
            surface.Measure(new Size(600, 400));
            surface.Arrange(new Rect(0, 0, 600, 400));

            surface.Split = 0.5;
            surface.Split = 0.51;
            var a = surface.Split;

            surface.Split = 0.5100001;
            var b = surface.Split;

            for (var i = 1; i <= 20; i++) surface.Split = 0.51 + i * 0.0001;
            return (a, b, surface.Split);
        });

        Assert.Equal(0.51, klavye, 9);
        Assert.Equal(0.51, ufak, 9);
        Assert.True(birikmis > 0.51 && birikmis <= 0.512, $"Ufak kaymalar birikmedi: {birikmis}");
    }
}
