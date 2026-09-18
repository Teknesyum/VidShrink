using System;
using System.Globalization;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Mini kipin sayaç genişliği ölçülüyor, göz kararıyla yazılmıyor. Ölçülen şey
/// <c>MonoValue</c> temasının yazı ölçüsüyle <c>00:00:00</c> metninin gerçek genişliği;
/// <c>RecorderMiniReadoutWidth</c> belirteci bu ölçümün üstüne, <c>SpaceMd</c> katına
/// yuvarlanarak yazıldı.
///
/// <para>Kanıt <c>.calisma/mini-olcu/sayac.txt</c>.</para>
/// </summary>
public class MiniKipOlcusuTests
{
    [Fact]
    public void SayacGenisligiBelirtectenBuyukDegil()
    {
        var (olculen, belirtec, adim) = AppHost.Run(() =>
        {
            var yazi = new TextBlock { Text = "00:00:00" };
            var window = new Window { Content = yazi };

            if (Application.Current?.FindResource("MonoValue") is ControlTheme tema)
                yazi.Theme = tema;

            window.Measure(new Size(1000, 200));

            var space = Application.Current?.FindResource("SpaceMd") is double d ? d : 12;
            var mini = Application.Current?.FindResource("RecorderMiniReadoutWidth") is double m ? m : 0;

            return (yazi.DesiredSize.Width, mini, space);
        });

        var klasor = Path.Combine(TipSources.Root, ".calisma", "mini-olcu");
        Directory.CreateDirectory(klasor);
        File.WriteAllText(
            Path.Combine(klasor, "sayac.txt"),
            string.Create(CultureInfo.InvariantCulture,
                $"MonoValue \"00:00:00\" olculen={olculen:0.##}{Environment.NewLine}RecorderMiniReadoutWidth={belirtec:0.##}{Environment.NewLine}SpaceMd={adim:0.##}{Environment.NewLine}"),
            new UTF8Encoding(false));

        Assert.True(belirtec > 0, "RecorderMiniReadoutWidth tanimli degil");
        Assert.True(belirtec >= olculen, $"sayac belirtece sigmiyor: olculen={olculen:0.##} belirtec={belirtec:0.##}");
        Assert.True(belirtec - olculen < adim, $"belirtec olculenden bir SpaceMd'den fazla genis: {belirtec:0.##} vs {olculen:0.##}");

        Kapat(klasor, "sayac.txt");
    }

    /// <summary>
    /// Son asertten sonra çağrılır: yeşil koşum kendi bıraktığını siler, kırmızı koşum
    /// kanıtını korur çünkü düşen asert buraya hiç gelmez. Klasör boşalınca o da gider.
    /// </summary>
    private static void Kapat(string klasor, params string[] adlar)
    {
        if (!Directory.Exists(klasor)) return;
        foreach (var ad in adlar)
        {
            var yol = Path.Combine(klasor, ad);
            if (File.Exists(yol)) File.Delete(yol);
        }

        if (Directory.GetFileSystemEntries(klasor).Length == 0) Directory.Delete(klasor);
    }
}
