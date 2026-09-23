using VidShrink.App;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// VP9'un kendi CRF ölçeği (<c>docs/olcumler/vp9-crf-olcek.md</c>, koşum 35833205185): libvpx-vp9
/// 0-63 alır ve x264 23'ün kalitesi VP9'da ~33. Önceden h264 dalına düşüp 10-45'e kırpılıyordu.
/// Dönüştür sekmesinde VP9 seçiliyken 55 yazılabilir; x264'te aynı sayı 45'e iner (olumsuz kontrol).
/// Saydamlık tavanı da ölçekle kayar: VP9 tavanı x264 tavanından yukarıda.
/// </summary>
public sealed class Vp9CrfOlcekTests
{
    private static (double Max, double Deger) Donustur(string kodek) => AppHost.Run(() =>
    {
        var window = new MainWindow();
        window.CmbConvertCodec.SelectedItem = window.CmbConvertCodec.Items.OfType<Avalonia.Controls.ComboBoxItem>()
            .First(i => (string?)i.Tag == kodek);
        window.CmbQualityMode.SelectedIndex = 0;
        window.TxtQuality.Text = "55";
        return (window.SliderQuality.Maximum, window.SliderQuality.Value);
    });

    [Fact]
    public void Vp9SeciliykenCrf55Kirpilmaz()
    {
        var (max, deger) = Donustur("libvpx-vp9");
        Assert.Equal(63, max);
        Assert.Equal(55, deger);
    }

    [Fact]
    public void X264te55KirpilirOlumsuzKontrol()
    {
        var (max, deger) = Donustur("libx264");
        Assert.Equal(45, max);
        Assert.Equal(45, deger);
    }

    /// <summary>
    /// Saydamlık tavanı kodeğin kendi ölçeğinden (<c>PlanCalculator.TransparencyCrf</c>, yansımayla):
    /// VP9 tavanı x264 tavanından beş CRF'ten fazla yukarıda ve 63'ü aşmıyor. Önceki ölçekte ikisi
    /// aynı sayıydı (Paylaşım amacında 20).
    /// </summary>
    [Fact]
    public void Vp9SaydamlikTavaniX264tenYuksek()
    {
        var yontem = typeof(PlanCalculator).GetMethod("TransparencyCrf", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(yontem);
        double Tavan(string kodek) => (double)yontem!.Invoke(null, [kodek, Intent.Sharing])!;

        var vp9 = Tavan("libvpx-vp9");
        var x264 = Tavan("libx264");
        Assert.True(vp9 > x264 + 5, $"vp9 {vp9}, x264 {x264}");
        Assert.True(vp9 <= 63, $"vp9 {vp9}");
    }
}
