using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using VidShrink.App.Playback;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Görsel denetimin (2026-09-23) canlandırma önerileri. Her kol iki yüzü okur: canlandırma
/// açıkken değer ilk anda hedefinde değil, <c>reduced-motion</c> sınıflı pencerede hedefinde.
/// </summary>
public sealed class GorselDenetimCanlandirmaTests
{
    private readonly ITestOutputHelper _output;

    public GorselDenetimCanlandirmaTests(ITestOutputHelper output) => _output = output;

    private static void KareVar(ComparisonPanel panel) =>
        typeof(ComparisonSurface)
            .GetField("_hasFrame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(panel.Frames, true);

    /// <summary>
    /// Öneri 3: karşılaştırma paneli boş durumdan ilk kareye geçerken yüzey saydamdan
    /// belirir. Boşken yüzey saydam; kare gelince canlandırmalı pencerede ilk anda hâlâ
    /// saydama yakın, hareketi azaltılmış pencerede hemen tam görünür.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KarsilastirmaYuzeyiIlkKaredeBelirir(bool azaltilmis)
    {
        var (bos, dolu) = AppHost.Run(() =>
        {
            var panel = new ComparisonPanel();
            var pencere = new Window { Width = 800, Height = 500, Content = panel };
            if (azaltilmis) pencere.Classes.Add("reduced-motion");
            pencere.Show();
            try
            {
                pencere.Measure(new Size(800, 500));
                pencere.Arrange(new Rect(0, 0, 800, 500));
                panel.RefreshEmptyState();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var yuzey = panel.GetVisualDescendants().OfType<ComparisonSurface>().Single();
                var bosOpaklik = yuzey.Opacity;
                KareVar(panel);
                panel.RefreshEmptyState();
                return (bosOpaklik, yuzey.Opacity);
            }
            finally { pencere.Close(); }
        });

        _output.WriteLine($"bos {bos:0.###}, kare {dolu:0.###}");
        Assert.Equal(0, bos, 3);
        if (azaltilmis) Assert.Equal(1, dolu, 3);
        else Assert.True(dolu < 0.5, $"Yüzey ilk anda {dolu:0.###} opaklıkta; geçiş yok, anında beliriyor.");
    }

    /// <summary>
    /// Öneri 6 ve bulgu 23: seçili sekmenin altında ince bir gösterge çizgisi var, seçili
    /// olmayanda yok. Seçim değişince çizgi canlandırmalı pencerede ilk anda hâlâ saydama
    /// yakın, hareketi azaltılmış pencerede hemen tam görünür.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SekmeGostergesiSecimleBelirir(bool azaltilmis)
    {
        var (secilmemis, secildi, eni, sekmeEni) = AppHost.Run(() =>
        {
            var kaynak = Avalonia.Application.Current!;
            TabItem Sekme(string ad) => new()
            {
                Header = ad,
                Content = new TextBlock { Text = ad },
                Theme = (Avalonia.Styling.ControlTheme)kaynak.FindResource("NeonTabItem")!,
            };
            var bir = Sekme("Bir");
            var iki = Sekme("İki");
            var sekmeler = new TabControl
            {
                Theme = (Avalonia.Styling.ControlTheme)kaynak.FindResource("NeonTabControl")!,
                ItemsSource = new[] { bir, iki },
                SelectedIndex = 0,
            };
            var pencere = new Window { Width = 600, Height = 300, Content = sekmeler };
            if (azaltilmis) pencere.Classes.Add("reduced-motion");
            pencere.Show();
            try
            {
                pencere.Measure(new Size(600, 300));
                pencere.Arrange(new Rect(0, 0, 600, 300));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Border Gosterge(TabItem sekme) => sekme.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "TabIndicator");
                var once = Gosterge(iki).Opacity;
                sekmeler.SelectedIndex = 1;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var gosterge = Gosterge(iki);
                pencere.Measure(new Size(600, 300));
                pencere.Arrange(new Rect(0, 0, 600, 300));
                return (once, gosterge.Opacity, gosterge.Bounds.Width, iki.Bounds.Width);
            }
            finally { pencere.Close(); }
        });

        _output.WriteLine($"secilmemis {secilmemis:0.###}, secildi {secildi:0.###}, en {eni:0.#} / sekme {sekmeEni:0.#}");
        Assert.Equal(0, secilmemis, 3);
        Assert.True(eni > 0 && eni <= sekmeEni + 0.5, $"Gösterge eni {eni:0.#}, sekme {sekmeEni:0.#}.");
        if (azaltilmis) Assert.Equal(1, secildi, 3);
        else Assert.True(secildi < 0.5, $"Gösterge ilk anda {secildi:0.###} opaklıkta; geçiş yok, anında beliriyor.");
    }
}
