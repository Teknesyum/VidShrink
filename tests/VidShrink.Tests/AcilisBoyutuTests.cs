using Avalonia;
using VidShrink.App;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Açılış boyutu çalışma alanına sığar. Taban boyut (1136x720) küçük mantıksal ekranda
/// pencerenin altını görev çubuğunun arkasına itiyordu: 1920x1080 %150'de alan 1280x688.
/// </summary>
public sealed class AcilisBoyutuTests
{
    private static readonly Size Tercih = new(1560, 1060);
    private static readonly Size Taban = new(1136, 720);

    [Theory]
    [InlineData(1920, 1032, 1.5)]
    [InlineData(2560, 1392, 2.0)]
    [InlineData(1920, 1032, 1.75)]
    [InlineData(1366, 728, 1.0)]
    public void PencereCalismaAlaninaSigar(double fizikselEn, double fizikselBoy, double olcek)
    {
        var alan = new Size(fizikselEn / olcek, fizikselBoy / olcek);
        var (enAz, boyut) = MainWindow.StartupFit(alan, 0.9, Tercih, Taban);

        Assert.True(boyut.Width <= alan.Width && boyut.Height <= alan.Height, $"{boyut} alana {alan} sığmıyor");
        Assert.True(enAz.Width <= alan.Width && enAz.Height <= alan.Height, $"taban {enAz} alana {alan} sığmıyor");
        Assert.True(boyut.Width >= enAz.Width && boyut.Height >= enAz.Height);
    }

    [Fact]
    public void GenisEkrandaTabanDegismez()
    {
        var (enAz, boyut) = MainWindow.StartupFit(new Size(1920, 1032), 0.9, Tercih, Taban);

        Assert.Equal(Taban, enAz);
        Assert.Equal(new Size(1560, 1032 * 0.9), boyut);
    }

    [Fact]
    public void BuyukEkrandaTercihBoyutuAcilir()
    {
        var (enAz, boyut) = MainWindow.StartupFit(new Size(2560, 1392), 0.9, Tercih, Taban);

        Assert.Equal(Taban, enAz);
        Assert.Equal(Tercih, boyut);
    }

    [Fact]
    public void KucukEkrandaTabanAlanaIner()
    {
        var (enAz, boyut) = MainWindow.StartupFit(new Size(1280, 688), 0.9, Tercih, Taban);

        Assert.Equal(new Size(1136, 688), enAz);
        Assert.Equal(new Size(1152, 688), boyut);
    }
}
