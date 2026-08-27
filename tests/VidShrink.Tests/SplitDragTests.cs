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
    private static ComparisonSurface Ready()
    {
        var surface = new ComparisonSurface();
        surface.Configure(Combined);
        RunningField.SetValue(surface, true);
        return surface;
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

            return surface.Repaints - before;
        });

        Assert.Equal(20, repaints);
    }

    /// <summary>
    /// Kare akıyorken araya giren boş turlar da çizim açmaz. Akış duraksadı sayılmadan
    /// önce ayırıcı kendi çizimini açarsa sürükleme yine döngüyü fareye bağlar.
    /// </summary>
    [Fact]
    public void Kare_arasindaki_bos_turda_ayirici_cizim_acmaz()
    {
        var repaints = AppHost.Run(() =>
        {
            var surface = Ready();
            Feed(surface);
            Round(surface);

            var before = surface.Repaints;
            var clock = Stopwatch.StartNew();
            var moves = 0;
            while (clock.Elapsed < TimeSpan.FromMilliseconds(50))
            {
                surface.Split = 0.2 + 0.6 * (++moves % 400) / 400.0;
                Round(surface);
            }

            Assert.True(moves > 100, $"Ölçüm yeterince hareket üretmedi: {moves}");
            return surface.Repaints - before;
        });

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
        var (repaints, seconds, moves) = AppHost.Run(() =>
        {
            var surface = Ready();

            // Akışın durduğuna karar verilecek kadar bekle, sonra ölçmeye başla.
            var idle = Stopwatch.StartNew();
            while (idle.Elapsed < TimeSpan.FromMilliseconds(150)) Round(surface);

            var before = surface.Repaints;
            var clock = Stopwatch.StartNew();
            var count = 0;
            while (clock.Elapsed < TimeSpan.FromMilliseconds(250))
            {
                surface.Split = 0.2 + 0.6 * (++count % 400) / 400.0;
                Round(surface);
            }

            return (surface.Repaints - before, clock.Elapsed.TotalSeconds, count);
        });

        Assert.True(moves > 500, $"Ölçüm yeterince hareket üretmedi: {moves}");
        Assert.True(repaints >= 1, "Kare gelmiyorken ayırıcı hiç çizim açmadı; sınır takılı kalır.");

        // Ekran aralığı 60 Hz; kenar turu için bir tolerans.
        var ceiling = (int)Math.Ceiling(seconds * 60) + 2;
        Assert.True(repaints <= ceiling, $"Ayırıcı {moves} harekette {repaints} çizim açtı, tavan {ceiling}.");
    }

    // ---- K4: ayırıcının kendisi bozulmuyor ------------------------------------------

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
