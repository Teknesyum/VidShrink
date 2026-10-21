using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using VidShrink.App.Recorder;
using Xunit;

namespace VidShrink.Tests;

public sealed class KaydediciMiniTasimaVeIpucuTests
{
    [Fact]
    public void SurukleBasladigiYerDugmeDegilse()
    {
        var sonuc = AppHost.Run(() =>
        {
            var mini = KaydediciMiniDuzenTests.Kur("tr", KaydediciMiniDuzenTests.Durum.Kayit);
            try
            {
                var dugmeler = mini.MiniStrip.GetVisualDescendants().OfType<Button>().ToList();
                var dugmeIci = dugmeler.SelectMany(d => d.GetVisualDescendants().Prepend(d)).ToHashSet();
                var disarisi = new Visual[] { mini.MiniStrip, mini.TxtElapsed, mini.LiveDot }
                    .Concat(mini.MiniStrip.GetVisualDescendants().Where(v => !dugmeIci.Contains(v)))
                    .ToList();
                return (
                    dugmeIci.Count(v => RecorderMini.StartsDrag(v)),
                    disarisi.Count(v => !RecorderMini.StartsDrag(v)),
                    dugmeler.Count,
                    disarisi.Count);
            }
            finally { mini.Close(); }
        });

        Assert.True(sonuc.Item3 >= 4);
        Assert.True(sonuc.Item4 >= 3);
        Assert.Equal(0, sonuc.Item1);
        Assert.Equal(0, sonuc.Item2);
    }

    [Fact]
    public void IpucuBeklemesiBelirtecKadarVeVarsayilandanBirSaniyeUzun()
    {
        var (belirtec, okunanlar) = AppHost.Run(() =>
        {
            var mini = KaydediciMiniDuzenTests.Kur("tr", KaydediciMiniDuzenTests.Durum.Kayit);
            try
            {
                var b = (int)Application.Current!.FindResource("RecorderMiniTipDelay")!;
                var dugmeler = mini.MiniStrip.GetVisualDescendants().OfType<Button>().Where(x => ToolTip.GetTip(x) is not null);
                return (b, dugmeler.Select(x => ToolTip.GetShowDelay(x)).ToList());
            }
            finally { mini.Close(); }
        });

        Assert.Equal((int)ToolTip.ShowDelayProperty.GetMetadata(typeof(Button)).DefaultValue! + 1000, belirtec);
        Assert.True(okunanlar.Count >= 4);
        Assert.All(okunanlar, d => Assert.Equal(belirtec, d));
    }
}
