using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using VidShrink.App;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Bildirim yigini oynatici disindaki sekmelerde icerigi ortmuyor. Yigin icerikle ayni
/// gozde bir katman; "varsayilan program degil" seridi Kucult sekmesinde Kaynak ve Cikti
/// basliklarinin ustunde kaliyordu. Olculen: gorunur bildirimin alt kenari ilk panelin
/// ust kenarinin ustunde mi. Oynaticida katman bilincli olarak goruntunun ustunde.
/// </summary>
public sealed class BildirimItmeTests
{
    [Fact]
    public void BildirimKucultSekmesindePaneliOrtmuyor()
    {
        var (bildirimAlt, panelUst) = AppHost.Run(() =>
        {
            var window = new MainWindow();
            try
            {
                window.Classes.Add("reduced-motion");
                window.Tabs.SelectedIndex = window.Tabs.Items.IndexOf(window.TabShrink);
                window.AppliedNotice.IsVisible = true;
                window.AppliedNotice.Opacity = 1;
                var olcu = new Size(1280, 688);
                for (var tur = 0; tur < 3; tur++) UstSeritTikTests.Yerlestir(window, olcu);

                var bildirim = window.AppliedNotice.TranslatePoint(new Point(0, window.AppliedNotice.Bounds.Height), window)!.Value.Y;
                var panel = window.SourcePanel.TranslatePoint(new Point(0, 0), window)!.Value.Y;
                return (bildirim, panel);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(bildirimAlt > 0, "bildirim yerlesmedi");
        Assert.True(panelUst >= bildirimAlt, $"bildirim alti {bildirimAlt:0} panel ustu {panelUst:0}");
    }

    [Theory]
    [InlineData(true, 120, 0)]
    [InlineData(false, 120, 120)]
    [InlineData(false, 0, 0)]
    public void OynaticiDisindaIcerikYiginKadarIner(bool oynatici, double yigin, double beklenen)
        => Assert.Equal(beklenen, MainWindow.NoticePush(oynatici, yigin));
}
