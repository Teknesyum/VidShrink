using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.Core;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Görsel denetimin (2026-09-23) ölçülebilir bulguları. Her kol düzeltmenin davranışını
/// okur; düzeltme geri alınınca kırmızıya düşer.
/// </summary>
public sealed class GorselDenetimTests
{
    private readonly ITestOutputHelper _output;

    public GorselDenetimTests(ITestOutputHelper output) => _output = output;

    internal static readonly Size Dar = new(1136, 720);
    internal static readonly Size Genis = new(1560, 1060);

    internal static MediaInfo Ornek() => new()
    {
        FilePath = @"C:\Kayitlar\ornek.mkv", FileSizeBytes = 420L * 1024 * 1024, DurationSeconds = 187.5,
        Width = 3840, Height = 2160, Fps = 59.94, VideoCodec = "hevc", TotalBitrateBps = 18_800_000,
        AudioCodec = "aac", AudioBitrateBps = 192_000, AudioChannels = 2, PixelFormat = "yuv420p"
    };

    internal static void Yerlestir(MainWindow window, Size size)
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

    internal static T Pencere<T>(Size size, Func<MainWindow, T> oku, int sekme = -1, string dil = "tr", bool dolu = true, Action<MainWindow>? hazirla = null) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();
            try
            {
                VidShrink.App.Localization.Strings.Use(dil);
                if (dolu)
                {
                    window.LoadWithoutProbing(@"C:\Kayitlar\ornek.mkv", Ornek());
                    window.SettleFades();
                }
                hazirla?.Invoke(window);
                Yerlestir(window, size);
                if (sekme >= 0)
                {
                    Ad<TabControl>(window, "Tabs").SelectedIndex = sekme;
                    Yerlestir(window, size);
                }
                return oku(window);
            }
            finally
            {
                window.Close();
                VidShrink.App.Localization.Strings.Use("en");
            }
        });

    internal static T Ad<T>(Visual kok, string ad) where T : Control =>
        kok.GetVisualDescendants().OfType<T>().First(c => c.Name == ad);

    internal static Rect Yeri(Control c, Visual kok) => new(c.TranslatePoint(new Point(0, 0), kok)!.Value, c.Bounds.Size);

    private static double Belirtec(string anahtar)
    {
        Assert.True(Application.Current!.TryFindResource(anahtar, out var deger), anahtar);
        return (double)deger!;
    }

    /// <summary>
    /// Bulgu 9: dar pencerede hedef ve kalite kaydırıcıları kendi satırında ve en az
    /// <c>SliderMinWidth</c> geniş; geniş pencerede kutu yine kaydırıcıyla aynı satırda.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void KaydiriciDarSutundaKendiSatirinaIner(bool dar)
    {
        var (enDar, olculer) = Pencere(dar ? Dar : Genis, w =>
        {
            var enDar = Belirtec("SliderMinWidth");
            var satirlar = new[] { ("SliderTarget", "TxtTarget"), ("SliderQualityTarget", "TxtQualityTarget") };
            return (enDar, satirlar.Select(s =>
            {
                var kaydirici = Ad<Slider>(w, s.Item1);
                var kutu = Ad<TextBox>(w, s.Item2);
                return (s.Item1, kaydirici: Yeri(kaydirici, w), kutu: Yeri(kutu, w));
            }).ToArray());
        });

        foreach (var (ad, kaydirici, kutu) in olculer)
        {
            _output.WriteLine($"{ad}: kaydirici {kaydirici}, kutu {kutu}");
            Assert.True(kaydirici.Width >= enDar, $"{ad} {kaydirici.Width:0.#} px; en az {enDar} bekleniyor.");
            if (dar) Assert.True(kutu.Top >= kaydirici.Bottom, $"{ad}: dar pencerede kutu kaydırıcının altına inmedi.");
            else Assert.True(kutu.Top < kaydirici.Bottom && kutu.Bottom > kaydirici.Top, $"{ad}: geniş pencerede kutu ayrı satıra düştü.");
        }
        Assert.Equal(olculer[0].kutu.Left, olculer[1].kutu.Left, 1);
    }

    /// <summary>
    /// Bulgu 21: sayfa kaydırma çubuğu ile sağ sütunun çerçevesi arasında en az <c>SpaceMd</c>.
    /// <c>AllowAutoHide="False"</c> çubuğa kendi sütununu ayırır; çubuğun sol kenarı içerik
    /// sunucusunun sağ kenarıdır. Başsız koşumda çubuğun kendisi yerleşmiyor, sütunu yerleşiyor.
    /// Bir piksellik pay yerleşim yuvarlamasının: üç eşit sütun 346,67 px, sağ sütun 1 px taşıyor.
    /// </summary>
    [Fact]
    public void KaydirmaCubuguIcerigeYapismaz()
    {
        var (bosluk, beklenen) = Pencere(Dar, w =>
        {
            var sayfa = Ad<ScrollViewer>(w, "PageShrink");
            var sunucu = (Control)sayfa.Presenter!;
            Assert.True(sayfa.Extent.Height > sayfa.Viewport.Height, "Sayfa kaymıyor; ölçü kurgusu bozuk.");
            Assert.True(sunucu.Bounds.Width < sayfa.Bounds.Width, "Çubuğun sütunu ayrılmamış; çubuk içeriğin üstüne biniyor.");
            var sag = Ad<Control>(w, "OutputPanel");
            return (Yeri(sunucu, w).Right - Yeri(sag, w).Right, Belirtec("SpaceMd"));
        });

        _output.WriteLine($"bosluk {bosluk:0.#}");
        Assert.True(bosluk >= beklenen - 1, $"Kaydırma çubuğu ile panel arası {bosluk:0.#} px; en az {beklenen} bekleniyor.");
    }

    /// <summary>
    /// Bulgu 8: dolu izin başparmağa bakan ucu yuvarlak ve başparmağın solunda açıkta kalınca
    /// ikinci bir başparmak gibi görünüyordu. Ölçülen: iki izin başparmağa bakan ucu ya köşesiz
    /// ya da başparmağın altında; izler başparmağa bitişik, arada boşluk yok.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(50)]
    [InlineData(98)]
    public void DoluIzBasparmakYanindaIkinciYuvarlakCizmez(double deger)
    {
        var (dolu, doluKose, bos, bosKose, basparmak) = AppHost.Run(() =>
        {
            var kaydirici = new Slider { Width = 240, Minimum = 0, Maximum = 100, Value = deger };
            var pencere = new Window { Width = 400, Height = 200, Content = kaydirici };
            pencere.Show();
            pencere.Measure(new Size(400, 200));
            pencere.Arrange(new Rect(0, 0, 400, 200));
            pencere.UpdateLayout();
            try
            {
                var doluIz = Ad<Border>(kaydirici, "FilledTrack");
                var bosIz = Ad<Border>(kaydirici, "RemainingTrack");
                return (Yeri(doluIz, kaydirici), doluIz.CornerRadius, Yeri(bosIz, kaydirici), bosIz.CornerRadius,
                        Yeri(Ad<Thumb>(kaydirici, "ThumbVisual"), kaydirici));
            }
            finally { pencere.Close(); }
        });

        _output.WriteLine($"dolu {dolu} {doluKose}, bos {bos} {bosKose}, basparmak {basparmak}");
        var merkez = basparmak.Center.X;
        var doluUcKapali = (doluKose.TopRight == 0 && doluKose.BottomRight == 0) || dolu.Right >= merkez;
        var bosUcKapali = (bosKose.TopLeft == 0 && bosKose.BottomLeft == 0) || bos.Left <= merkez;
        Assert.True(doluUcKapali, $"Dolu izin yuvarlak ucu başparmağın solunda açıkta: iz {dolu.Right:0.#}'da bitiyor, başparmak {basparmak.Left:0.#}-{basparmak.Right:0.#}.");
        Assert.True(bosUcKapali, $"Boş izin yuvarlak ucu başparmağın sağında açıkta: iz {bos.Left:0.#}'dan başlıyor.");
        Assert.True(dolu.Right >= basparmak.Left - 0.5 && bos.Left <= basparmak.Right + 0.5, "İz ile başparmak arasında boşluk var.");
    }
}
