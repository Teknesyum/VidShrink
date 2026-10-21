using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App.Recorder;
using VidShrink.Ffmpeg;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

public sealed class KaydediciMiniTasimaVeIpucuTests
{
    private readonly ITestOutputHelper _output;

    public KaydediciMiniTasimaVeIpucuTests(ITestOutputHelper output) => _output = output;

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
        var (belirtec, varsayilan, okunanlar) = AppHost.Run(() =>
        {
            var mini = KaydediciMiniDuzenTests.Kur("tr", KaydediciMiniDuzenTests.Durum.Kayit);
            try
            {
                var b = (int)Application.Current!.FindResource("RecorderMiniTipDelay")!;
                var v = (int)ToolTip.ShowDelayProperty.GetMetadata(typeof(Button)).DefaultValue!;
                var dugmeler = mini.MiniStrip.GetVisualDescendants().OfType<Button>().Where(x => ToolTip.GetTip(x) is not null);
                return (b, v, dugmeler.Select(x => ToolTip.GetShowDelay(x)).ToList());
            }
            finally { mini.Close(); }
        });

        _output.WriteLine($"varsayilan ShowDelay {varsayilan}, belirtec {belirtec}");
        Assert.Equal(varsayilan + 1000, belirtec);
        Assert.True(okunanlar.Count >= 4);
        Assert.All(okunanlar, d => Assert.Equal(belirtec, d));
    }

    [Fact]
    public void IpucuAcikkenSayacIlerleseDeAcikKalir()
    {
        var (once, sonra, metin) = AppHost.Run(() =>
        {
            var mini = new RecorderMini { ExcludeFromCapture = false };
            try
            {
                mini.Follow(RecorderState.Running, "00:00:00");
                mini.Show();
                Dispatcher.UIThread.RunJobs();
                var dugme = mini.BtnToggle;
                ToolTip.SetIsOpen(dugme, true);
                Dispatcher.UIThread.RunJobs();
                var o = ToolTip.GetIsOpen(dugme);
                for (var sn = 1; sn <= 20; sn++)
                {
                    mini.Follow(RecorderState.Running, TimeSpan.FromSeconds(sn).ToString(@"hh\:mm\:ss"));
                    Dispatcher.UIThread.RunJobs();
                    System.Threading.Thread.Sleep(100);
                    Dispatcher.UIThread.RunJobs();
                }

                return (o, ToolTip.GetIsOpen(dugme), mini.TxtElapsed.Text);
            }
            finally { mini.Close(); }
        });

        _output.WriteLine($"acik once {once}, 2 sn sonra {sonra}, sayac {metin}");
        Assert.True(once);
        Assert.True(sonra);
    }
}
