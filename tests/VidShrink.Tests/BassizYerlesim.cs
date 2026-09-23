using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace VidShrink.Tests;

/// <summary>
/// Gösterilmeyen pencerede yerleşimi gerçek pencereye sadık kurmanın iki eksiği.
///
/// <para>Platform penceresi açılışta ekrana göre boyutlanıyor (<c>MainWindow.ApplyStartupSize</c>) ve
/// pencere düzeyindeki sınırlar onun boyunda kalıyor: CI koşucusunun 1024 genişlikli ekranında kök
/// 752 px, içindeki sayfa 1510 px ölçülüyordu. <c>KucultSutunlari</c> sol sütun kararını o iki sınırın
/// farkından veriyor; karar düştü, bilgi ızgarası iki sütuna indi ve dolu sayfa 108 px uzadı. Yalnız
/// CI'da kırmızıydı. <see cref="PlatformBoyu"/> platform penceresini ölçülen boya getirir.</para>
///
/// <para>Yerleşim yöneticisi gösterilmeyen pencerede geçiş koşmuyor; gerçek pencerede bir denetimin
/// yerleştirme sırasında istediği yeniden ölçüm aynı döngüde karşılanır. <see cref="IkinciGecis"/>
/// bekleyen işleri koşturur ve ölçümü geçersiz kalan her düğümün atalarını geçersizleyip kökü yeniden
/// ölçer.</para>
/// </summary>
internal static class BassizYerlesim
{
    internal static void PlatformBoyu(Window window, Size size)
    {
        var impl = typeof(TopLevel).GetProperty("PlatformImpl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)!.GetValue(window)!;
        var resize = impl.GetType().GetMethods().First(m => m.Name == "Resize" && m.GetParameters().Length == 2);
        resize.Invoke(impl, [size, Enum.ToObject(resize.GetParameters()[1].ParameterType, 0)]);
    }

    internal static void IkinciGecis(Window window, Layoutable root, Size size)
    {
        Dispatcher.UIThread.RunJobs();
        var gecersiz = window.GetVisualDescendants().OfType<Layoutable>().Where(node => !node.IsMeasureValid).ToList();
        if (gecersiz.Count == 0) return;
        foreach (var node in gecersiz)
            foreach (var ata in node.GetVisualAncestors().OfType<Layoutable>())
                ata.InvalidateMeasure();
        root.Measure(size);
        root.Arrange(new Rect(size));
    }
}
