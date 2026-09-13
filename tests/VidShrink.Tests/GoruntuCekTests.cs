using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using VidShrink.App;
using Xunit;

namespace VidShrink.Tests;

public sealed class GoruntuCekTests
{
    private static readonly string Klasor = Path.Combine(TipSources.Root, ".calisma", "kesit-ef");

    private static void Kaydet(Control kok, int w, int h, double olcek, string ad)
    {
        kok.InvalidateMeasure();
        kok.Measure(new Size(w, double.PositiveInfinity));
        var size = new Size(w, h > 0 ? h : kok.DesiredSize.Height);
        kok.Arrange(new Rect(size));
        h = (int)size.Height;
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)(w * olcek), (int)(h * olcek)), new Vector(96 * olcek, 96 * olcek));
        bitmap.Render(kok);
        bitmap.Save(Path.Combine(Klasor, ad), PngBitmapEncoderOptions.Default);
    }

    [Fact]
    public void KareleriAl()
    {
        Directory.CreateDirectory(Klasor);
        AppHost.Run(() =>
        {
            var window = new MainWindow { Width = double.NaN, Height = double.NaN };
            window.Measure(new Size(1200, 800));
            window.FindControl<Border>("UpdateNotice")!.IsVisible = true;
            window.FindControl<TextBlock>("TxtNoticeVersion")!.Text = "0.4.3";
            window.FindControl<TabControl>("Tabs")!.SelectedIndex = 3;
            var kok = (Control)window.GetVisualChildren().Single();
            Kaydet(kok, 1200, 800, 1, "guncelleme-paneli.png");

            var sayfa = new WrapPanel { Width = 700, Background = Brushes.Black };
            foreach (var (ad, veri) in IkonKutusuTests.Ikonlar().Select(o => ((string)o[0], (string)o[1])))
                sayfa.Children.Add(new StackPanel
                {
                    Width = 132,
                    Margin = new Thickness(3),
                    Children =
                    {
                        new Avalonia.Controls.Shapes.Path
                        {
                            Width = 40, Height = 40, Stretch = Stretch.Uniform,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Data = Geometry.Parse(veri), Stroke = Brushes.White, StrokeThickness = 2 * 40 / 24.0,
                            StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round
                        },
                        new TextBlock { Text = ad, Foreground = Brushes.Gray, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center }
                    }
                });
            Kaydet(sayfa, 700, 480, 2, "ikonlar.png");
            return 0;
        });
        Assert.True(File.Exists(Path.Combine(Klasor, "ikonlar.png")));
    }
}
