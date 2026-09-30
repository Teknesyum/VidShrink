using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using VidShrink.App;
using Xunit.Abstractions;

namespace VidShrink.Tests;

public sealed class KaydediciDuzenTests
{
    private readonly ITestOutputHelper _output;

    public KaydediciDuzenTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(1560, 1060, false, "tr")]
    [InlineData(1560, 1060, true, "tr")]
    [InlineData(1024, 1060, false, "tr")]
    [InlineData(1024, 1060, true, "tr")]
    [InlineData(1600, 1000, true, "tr")]
    [InlineData(1600, 1000, true, "en")]
    [InlineData(1560, 1060, true, "en")]
    public void SayfaKaydirmadanSigarVeTamponSeritteDegil(int en, int boy, bool gelismis, string dil)
    {
        var boyut = new Size(en, boy);
        var olcu = KaydirmasizSekmeTests.AyarlariKorurken(() => GorselDenetimTests.Pencere(boyut, w =>
        {
            var gorunum = w.RecorderPaneForTest;
            GorselDenetimTests.Ad<RadioButton>(gorunum, gelismis ? "RadAdvanced" : "RadSimple").IsChecked = true;
            foreach (var parca in w.GetVisualDescendants().OfType<Animatable>()) parca.Transitions = null;
            GorselDenetimTests.Yerlestir(w, boyut);

            var sayfa = GorselDenetimTests.Ad<ScrollViewer>(w, "PageRecorder");
            var serit = GorselDenetimTests.Ad<Border>(gorunum, "Strip");
            var tampon = GorselDenetimTests.Ad<Button>(gorunum, "BtnReplay");
            var tamponKarti = GorselDenetimTests.Ad<Border>(gorunum, "PanelReplay");
            var sesIzleri = GorselDenetimTests.Ad<ComboBox>(gorunum, "CmbAudioLayout");
            var kodlama = GorselDenetimTests.Ad<Border>(gorunum, "PanelAdvanced").GetVisualDescendants().OfType<SutunIzgara>().First();

            var tasmalar = new List<string>();
            for (var secim = 0; secim < Math.Max(1, sesIzleri.ItemCount); secim++)
            {
                if (sesIzleri.ItemCount > 0)
                {
                    sesIzleri.SelectedIndex = secim;
                    GorselDenetimTests.Yerlestir(w, boyut);
                }
                var satir = $"{sesIzleri.SelectionBoxItem}: extent {sayfa.Extent} viewport {sayfa.Viewport} kodlama {kodlama.Columns} sutun, " +
                    $"{kodlama.Bounds.Width:0.#} px, iki sutun {kodlama.WidthFor(2):0.#} px, tampon {tamponKarti.Bounds}";
                _output.WriteLine(satir);
                if (sayfa.Extent.Height > sayfa.Viewport.Height + 0.5) tasmalar.Add(satir);
            }
            Cek(w, en, boy, gelismis);

            return (
                Tasmalar: tasmalar,
                TamponSeritte: tampon.GetVisualAncestors().Contains(serit),
                TamponKartta: tampon.GetVisualAncestors().Contains(tamponKarti),
                TamponGorunur: tamponKarti.IsVisible);
        }, sekme: 4, dil: dil, dolu: false, hazirla: HareketsizAc));

        Assert.True(olcu.Tasmalar.Count == 0, "sayfa kaydırıyor: " + string.Join("; ", olcu.Tasmalar));
        Assert.False(olcu.TamponSeritte);
        Assert.True(olcu.TamponKartta);
        Assert.Equal(gelismis, olcu.TamponGorunur);
    }

    internal static void HareketsizAc(MainWindow pencere)
    {
        pencere.Classes.Add("reduced-motion");
        typeof(MainWindow).GetField("_motionReduced", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(pencere, true);
    }

    private static void Cek(Window w, int en, int boy, bool gelismis)
    {
        if (Environment.GetEnvironmentVariable("KAYDEDICI_CEK") is not { Length: > 0 } ad) return;
        var klasor = Path.Combine(TipSources.Root, ".calisma", "worktree-agent-a71dee06131f99af4", ad);
        Directory.CreateDirectory(klasor);
        var kok = (Control)w.GetVisualChildren().Single();
        using var bitmap = new RenderTargetBitmap(new PixelSize(en, boy), new Vector(96, 96));
        bitmap.Render(kok);
        bitmap.Save(Path.Combine(klasor, $"tr-{en}x{boy}-{(gelismis ? "gelismis" : "basit")}.png"), PngBitmapEncoderOptions.Default);
    }
}
