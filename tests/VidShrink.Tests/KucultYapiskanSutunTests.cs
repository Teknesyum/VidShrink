using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.Core;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Görsel denetim Bulgu 2/3: dört bölüm açıkken sol sütun sayfayı birkaç ekran boyu uzatıyor.
/// Orta sütunun önizleme satırı <c>*</c> olduğu için karşılaştırma paneli o boya geriliyor,
/// yer tutucusu ilk ekranın altında kalıyordu; sağ sütunun altında da o kadar boşluk vardı.
/// Ölçülen: önizleme görünüm yüksekliğini aşmıyor, sayfa kaydırıldığında orta ve sağ sütun
/// görünümde kalıyor.
/// </summary>
public sealed class KucultYapiskanSutunTests
{
    private readonly ITestOutputHelper _output;

    public KucultYapiskanSutunTests(ITestOutputHelper output) => _output = output;

    private static readonly string[] Bolumler = { "BtnQualityToggle", "BtnAudioToggle", "BtnFrameToggle", "BtnAdvancedToggle" };

    private static void Yerlestir(MainWindow window, Size size)
    {
        BassizYerlesim.PlatformBoyu(window, size);
        window.Width = double.NaN;
        window.Height = double.NaN;
        window.Measure(size);
        window.Arrange(new Rect(size));
        window.UpdateLayout();

        var root = (Layoutable)window.GetVisualChildren().Single();
        for (var gecis = 0; gecis < 3; gecis++)
        {
            foreach (var node in window.GetVisualDescendants().OfType<Layoutable>()) node.InvalidateMeasure();
            root.InvalidateMeasure();
            root.Measure(size);
            root.Arrange(new Rect(size));
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        foreach (var node in window.GetVisualDescendants().OfType<Visual>()) node.RenderTransform = null;
    }

    private static Rect Yeri(Control c, Visual kok) => new(c.TranslatePoint(new Point(0, 0), kok)!.Value, c.Bounds.Size);

    [Theory]
    [InlineData(1136, 720)]
    [InlineData(1560, 1060)]
    public void OnizlemeGorunumdeKaliyorSolSutunUzasaDa(double en, double boy)
    {
        var olcu = AppHost.Run(() =>
        {
            var window = new MainWindow();
            try
            {
                window.UseTurkish();
                window.LoadWithoutProbing(@"C:\Kayitlar\ornek.mkv", new MediaInfo
                {
                    FilePath = @"C:\Kayitlar\ornek.mkv", FileSizeBytes = 420L * 1024 * 1024, DurationSeconds = 187.5,
                    Width = 3840, Height = 2160, Fps = 59.94, VideoCodec = "hevc", TotalBitrateBps = 18_800_000,
                    AudioCodec = "aac", AudioBitrateBps = 192_000, AudioChannels = 2, PixelFormat = "yuv420p"
                });
                window.SettleFades();
                var size = new Size(en, boy);
                Yerlestir(window, size);

                foreach (var ad in Bolumler)
                    window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == ad)
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Yerlestir(window, size);

                var sayfa = window.GetVisualDescendants().OfType<ScrollViewer>().Single(v => v.Name == "PageShrink");
                var sutunlar = window.GetVisualDescendants().OfType<KucultSutunlari>().Single();
                var orta = sutunlar.Children.OfType<Control>().Single(c => Grid.GetColumn(c) == 1);
                var sag = sutunlar.Children.OfType<Control>().Single(c => Grid.GetColumn(c) == 2);
                var sol = sutunlar.Children.OfType<Control>().Single(c => Grid.GetColumn(c) == 0);
                var onizleme = window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "Preview");

                var gorunum = sayfa.Viewport.Height;
                var solBoy = sol.Bounds.Height;
                var onizlemeBoy = onizleme.Bounds.Height;
                var ortaUst0 = Yeri(orta, sayfa).Top;

                sayfa.Offset = new Vector(0, (sayfa.Extent.Height - gorunum) / 2);
                Yerlestir(window, size);
                var ortaKayik = Yeri(orta, sayfa);
                var sagKayik = Yeri(sag, sayfa);
                return (gorunum, solBoy, onizlemeBoy, ortaUst0, ortaKayik, sagKayik, kayma: sayfa.Offset.Y);
            }
            finally { window.Close(); }
        });

        _output.WriteLine($"gorunum {olcu.gorunum:0.#}, sol {olcu.solBoy:0.#}, onizleme {olcu.onizlemeBoy:0.#}, orta ust {olcu.ortaUst0:0.#}");
        _output.WriteLine($"kayma {olcu.kayma:0.#}: orta {olcu.ortaKayik}, sag {olcu.sagKayik}");

        Assert.True(olcu.solBoy > 1.5 * olcu.gorunum, $"Bölümler açılınca sol sütun uzamadı ({olcu.solBoy:0.#}); ölçü kurgusu bozuk.");
        Assert.True(olcu.kayma > 0, "Sayfa kaydırılamadı; ölçü kurgusu bozuk.");
        Assert.True(olcu.onizlemeBoy < olcu.gorunum,
            $"Önizleme {olcu.onizlemeBoy:0.#} px, görünüm {olcu.gorunum:0.#}: panel sol sütunun boyuna gerildi.");
        Assert.InRange(olcu.ortaUst0, 0, olcu.gorunum / 4);
        Assert.True(Gorunen(olcu.ortaKayik, olcu.gorunum) >= Math.Min(olcu.ortaKayik.Height, olcu.gorunum) - olcu.ortaUst0 - 0.5,
            $"Kaydırılınca orta sütun görünümden çıktı: {olcu.ortaKayik}.");
        Assert.True(Gorunen(olcu.sagKayik, olcu.gorunum) >= Math.Min(olcu.sagKayik.Height, olcu.gorunum) - olcu.ortaUst0 - 0.5,
            $"Kaydırılınca sağ sütun görünümden çıktı: {olcu.sagKayik}.");
    }

    private static double Gorunen(Rect r, double gorunum) => Math.Max(0, Math.Min(r.Bottom, gorunum) - Math.Max(r.Top, 0));

    [Theory]
    [InlineData(300, 2000, 600, 0, 0)]
    [InlineData(300, 2000, 600, 500, 500)]
    [InlineData(300, 2000, 600, 1900, 1700)]
    [InlineData(900, 2000, 600, 100, 0)]
    [InlineData(900, 2000, 600, 500, 200)]
    public void YapiskanUstSatirinIcindeKalir(double boy, double satir, double gorunen, double kayma, double beklenen)
        => Assert.Equal(beklenen, KucultSutunlari.YapiskanUst(boy, satir, gorunen, kayma));
}
