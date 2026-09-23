using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using VidShrink.App;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kısaltılan metin kaybolmaz: üç noktayla kesilen her satırın tam metni ipucunda durur.
/// Küçült penceresi 420 px'e sabit ve boyutu değişmiyor; Almanca başlık (48 karakter) ve
/// uzun dosya adları tek satıra sığmıyordu. Başlık iki satıra kadar iner, hedef satırı
/// sarar, aşama ve güncelleme günlüğü satırları ipucunu taşır.
/// </summary>
public sealed class KirpilmaIpucuTests
{
    private const string Uzun = "Bu_dosya_adi_bilerek_cok_uzun_tutuldu_ki_tek_satira_hicbir_dilde_sigmasin_2026-09-23_kayit.mp4";

    [Fact]
    public void KucultPenceresiUzunBasligiIkiSatiraIndirirVeIpucundaTutar()
    {
        var olcu = AppHost.Run(() =>
        {
            var window = new ShrinkJobWindow(new ShellShrinkStartup(null, ShrinkArgumentProblem.NoTarget, null), null);
            try
            {
                var baslik = window.FindControl<TextBlock>("TxtHeadline")!;
                var hedef = window.FindControl<TextBlock>("TxtTarget")!;
                var asama = window.FindControl<TextBlock>("TxtStage")!;
                baslik.Text = Uzun;
                asama.Text = Uzun;
                hedef.Text = Uzun;
                baslik.Measure(new Size(372, double.PositiveInfinity));
                baslik.Arrange(new Rect(0, 0, 372, baslik.DesiredSize.Height));
                hedef.Measure(new Size(372, double.PositiveInfinity));
                hedef.Arrange(new Rect(0, 0, 372, hedef.DesiredSize.Height));
                return (baslikIpucu: ToolTip.GetTip(baslik) as string, asamaIpucu: ToolTip.GetTip(asama) as string,
                    baslikSatir: baslik.TextLayout.TextLines.Count, hedefSatir: hedef.TextLayout.TextLines.Count,
                    hedefSarar: hedef.TextWrapping);
            }
            finally { window.Close(); }
        });

        Assert.Equal(Uzun, olcu.baslikIpucu);
        Assert.Equal(Uzun, olcu.asamaIpucu);
        Assert.Equal(2, olcu.baslikSatir);
        Assert.Equal(TextWrapping.Wrap, olcu.hedefSarar);
        Assert.True(olcu.hedefSatir > 1, $"hedef satır {olcu.hedefSatir}");
    }

    [Fact]
    public void GuncellemeGunlukSatiriTamMetniIpucundaTasir()
    {
        var ipuclari = AppHost.Run(() =>
        {
            var window = new MainWindow();
            var ilerleme = new InstallProgress();
            window.ShowUpdateProgress(ilerleme);
            ilerleme.Step(0, 10, Uzun);
            ilerleme.Step(10, 20, "ikinci");
            for (var i = 0; i < 90; i++) window.UpdateFrame(TimeSpan.FromMilliseconds(InstallProgress.FrameMilliseconds));
            return window.FindControl<StackPanel>("UpdateLogLines")!.Children.OfType<TextBlock>()
                .Select(s => (s.Text, Ipucu: ToolTip.GetTip(s) as string)).ToList();
        });

        Assert.NotEmpty(ipuclari);
        Assert.Contains(ipuclari, s => s.Text == Uzun);
        Assert.All(ipuclari, s => Assert.Equal(s.Text, s.Ipucu));
    }
}
