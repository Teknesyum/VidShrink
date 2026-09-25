using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Themes;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Kaydırıcı ailesinin davranışı: aralıktan çıkan klavye adımları, iz yüksekliğinin tamamına
/// yayılan basma alanı, değer değişince başparmağın kayması, durumların ayrı görünmesi ve
/// değerin kaydırıcının yanında eşit genişlikli rakamla gösterilmesi.
/// </summary>
public sealed class KaydiriciTests
{
    private readonly ITestOutputHelper _output;

    public KaydiriciTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(1, 500, 0, 1, 50)]
    [InlineData(1, 100, 0, 1, 10)]
    [InlineData(10, 45, 0, 1, 5)]
    [InlineData(50, 10000, 0, 20, 1000)]
    [InlineData(0, 100, 5, 5, 10)]
    [InlineData(0.25, 4, 0.05, 0.05, 0.5)]
    public void AdimlarAraliktanCikar(double enAz, double enCok, double tik, double kucuk, double buyuk)
    {
        var (k, b) = KaydiriciHareketi.Adimlar(enAz, enCok, tik);
        Assert.Equal(kucuk, k, 6);
        Assert.Equal(buyuk, b, 6);
    }

    private static T Olc<T>(Func<Window, Slider, T> is_, double enAz = 50, double enCok = 10000, double deger = 1000)
        => AppHost.Run(() =>
        {
            var kaydirici = new Slider { Width = 300, Minimum = enAz, Maximum = enCok, Value = deger };
            var pencere = new Window { Width = 400, Height = 120, Content = kaydirici };
            pencere.Show();
            pencere.UpdateLayout();
            try { return is_(pencere, kaydirici); }
            finally { pencere.Close(); }
        });

    private static void Pompala(int ms)
    {
        var saat = System.Diagnostics.Stopwatch.StartNew();
        while (saat.ElapsedMilliseconds < ms)
        {
            using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(5));
            Avalonia.Threading.Dispatcher.UIThread.MainLoop(dilim.Token);
        }
    }

    private static void Tus(Slider kaydirici, Key tus)
        => kaydirici.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = tus, Source = kaydirici });

    [Fact]
    public void KlavyeAdimlariAraligaGore()
    {
        var degerler = Olc((_, k) =>
        {
            k.Focus();
            var sonuc = new List<double>();
            foreach (var t in new[] { Key.Right, Key.Right, Key.PageUp, Key.Left, Key.PageDown, Key.End, Key.Left, Key.Home, Key.Up })
            {
                Tus(k, t);
                sonuc.Add(k.Value);
            }
            return sonuc;
        });
        _output.WriteLine(string.Join(" ", degerler));
        Assert.Equal(new double[] { 1020, 1040, 2040, 2020, 1020, 10000, 9980, 50, 70 }, degerler);
    }

    [Fact]
    public void AralikDegisinceAdimlarYenilenir()
    {
        var (kucuk, buyuk) = Olc((_, k) =>
        {
            k.Minimum = 10;
            k.Maximum = 45;
            return (k.SmallChange, k.LargeChange);
        });
        Assert.Equal((1d, 5d), (kucuk, buyuk));
    }

    private static Visual Iz(Slider k) => k.GetVisualDescendants().OfType<Track>().Single();

    [Theory]
    [InlineData(-11)]
    [InlineData(0)]
    [InlineData(11)]
    public void BasmaAlaniIzinTumYuksekligi(double sapma)
    {
        var (yakalayan, yukseklik) = Olc((p, k) =>
        {
            var iz = (Track)Iz(k);
            var merkez = iz.TranslatePoint(new Point(iz.Bounds.Width * 0.8, iz.Bounds.Height / 2), p)!.Value;
            var nokta = merkez + new Point(0, sapma);
            Pompala(200);
            var dugme = p.GetVisualsAt(nokta, v => v.IsVisible)
                .Where(v => v is not Window && v is IInputElement { IsHitTestVisible: true })
                .SelectMany(v => v.GetSelfAndVisualAncestors()).OfType<RepeatButton>().FirstOrDefault();
            return (dugme?.Name, k.Bounds.Height);
        });
        _output.WriteLine($"{sapma}px: {yakalayan ?? "(yok)"}, kaydırıcı {yukseklik}");
        Assert.Equal("PART_IncreaseButton", yakalayan);
    }

    private static double Oteleme(Visual v) => v.RenderTransform?.Value.M31 ?? 0;

    [Fact]
    public void DegerDegisinceBasparmakEskiYerindenKayar()
    {
        var (basparmak, dolu, bos) = Olc((p, k) =>
        {
            var iz = (Track)Iz(k);
            k.Value = 9000;
            p.UpdateLayout();
            return (Oteleme(iz.Thumb!), iz.DecreaseButton!.RenderTransform?.Value.M11 ?? 1, iz.IncreaseButton!.RenderTransform?.Value.M11 ?? 1);
        });
        _output.WriteLine($"başparmak {basparmak:0.#}px, dolu ölçek {dolu:0.###}, boş ölçek {bos:0.###}");
        Assert.True(basparmak < -100, $"Başparmak eski yerinden başlamıyor: öteleme {basparmak:0.#}px.");
        Assert.True(dolu < 0.5, $"Dolu iz eski boyundan başlamıyor: ölçek {dolu:0.###}.");
        Assert.True(bos > 2, $"Boş iz eski boyundan başlamıyor: ölçek {bos:0.###}.");
    }

    [Fact]
    public void SuruklerkenBasparmakImlecleGider()
    {
        var basparmak = Olc((p, k) =>
        {
            var iz = (Track)Iz(k);
            ((IPseudoClasses)iz.Thumb!.Classes).Set(":pressed", true);
            k.Value = 9000;
            p.UpdateLayout();
            return Oteleme(iz.Thumb);
        });
        Assert.Equal(0, basparmak, 3);
    }

    private static IBrush? Kaynak(Control c, string ad) => c.TryFindResource(ad, out var o) ? o as IBrush : null;

    private static Color Renk(IBrush? b) => b is ISolidColorBrush s ? s.Color : default;

    private sealed record Durum(Color Dolu, Color BosKenar, Color KnobDolgu, Color KnobKenar, double KnobOlcek);

    private static Durum Oku(Slider k)
    {
        var dolu = k.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "FilledTrack");
        var bos = k.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "RemainingTrack");
        var knob = k.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Single(e => e.Name == "Knob");
        return new Durum(Renk(dolu.Background), Renk(bos.BorderBrush), Renk(knob.Fill), Renk(knob.Stroke),
            knob.RenderTransform?.Value.M11 ?? 1);
    }

    private static void Zorla(Slider k, string durum)
    {
        var knob = k.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Single(e => e.Name == "Knob");
        knob.Transitions = null;
        var sinif = (IPseudoClasses)k.Classes;
        foreach (var d in new[] { ":pointerover", ":pressed", ":focus-visible" }) sinif.Set(d, false);
        k.IsEnabled = durum != "edilgen";
        if (durum == "ustunde") sinif.Set(":pointerover", true);
        if (durum == "basili") { sinif.Set(":pointerover", true); sinif.Set(":pressed", true); }
        if (durum == "odak") sinif.Set(":focus-visible", true);
    }

    private static readonly string[] Durumlar = { "dinlenik", "ustunde", "basili", "odak", "edilgen" };

    [Fact]
    public void DurumlarBirbirindenAyrilir()
    {
        var (olcumler, mavi, zemin, soluk) = Olc((p, k) =>
        {
            var o = new Dictionary<string, Durum>();
            foreach (var d in Durumlar)
            {
                Zorla(k, d);
                p.UpdateLayout();
                o[d] = Oku(k);
            }
            return (o, Renk(Kaynak(k, "NeonBlue")), Renk(Kaynak(k, "AppBg")), Renk(Kaynak(k, "TextDisabled")));
        }, 0, 100, 40);
        foreach (var (d, v) in olcumler) _output.WriteLine($"{d}: {v}");

        Assert.Equal(mavi, olcumler["dinlenik"].Dolu);
        Assert.Equal(zemin, olcumler["dinlenik"].KnobDolgu);
        Assert.Equal(mavi, olcumler["dinlenik"].KnobKenar);
        Assert.Equal(1, olcumler["dinlenik"].KnobOlcek, 3);
        Assert.Equal(mavi, olcumler["ustunde"].Dolu);
        Assert.Equal(zemin, olcumler["ustunde"].KnobDolgu);
        Assert.Equal(1.1, olcumler["ustunde"].KnobOlcek, 3);
        Assert.NotEqual(olcumler["dinlenik"].BosKenar, olcumler["ustunde"].BosKenar);
        Assert.Equal(mavi, olcumler["basili"].KnobDolgu);
        Assert.Equal(1.2, olcumler["basili"].KnobOlcek, 3);
        Assert.Equal(1.1, olcumler["odak"].KnobOlcek, 3);
        Assert.Equal(olcumler["ustunde"].BosKenar, olcumler["odak"].BosKenar);
        Assert.NotEqual(olcumler["dinlenik"].BosKenar, olcumler["odak"].BosKenar);
        Assert.Equal(soluk, olcumler["edilgen"].Dolu);
        Assert.Equal(soluk, olcumler["edilgen"].KnobKenar);
        Assert.Equal(1, olcumler["edilgen"].KnobOlcek, 3);
    }

    private static double Kanal(double v)
    {
        v /= 255;
        return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    private static double Parlaklik(Color c) => 0.2126 * Kanal(c.R) + 0.7152 * Kanal(c.G) + 0.0722 * Kanal(c.B);

    private static double Oran(Color a, Color b)
    {
        double x = Parlaklik(a), y = Parlaklik(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    /// <summary>
    /// Vurgu rengi (NeonBlue) zeminden 3:1 ayrılmayan palet. Paletin kendi rengi; kaydırıcıya yeni
    /// renk uydurulmadı. Palet düzelirse liste boşalır ve bu ölçü haber verir.
    /// </summary>
    private static readonly string[] PalettenEksikVurgu = { "AyuLight" };

    [Fact]
    public void BasparmakVeDoluIzHerPalettePlanindanAyrilir()
    {
        var satirlar = AppHost.Run(() =>
        {
            var liste = new List<(string Ad, double Knob, double Dolu)>();
            var k = new Slider();
            var p = new Window { Content = k };
            try
            {
                foreach (var ad in PaletteCatalog.Names)
                {
                    PaletteCatalog.Use(ad);
                    var mavi = Renk(Kaynak(k, "NeonBlue"));
                    var zemin = Renk(Kaynak(k, "AppBg"));
                    var yuzey = Renk(Kaynak(k, "Surface"));
                    liste.Add(((string)ad, Math.Min(Oran(mavi, zemin), Oran(mavi, yuzey)), Oran(mavi, yuzey)));
                }
            }
            finally { PaletteCatalog.Use(PaletteCatalog.Default); }
            return liste;
        });
        foreach (var s in satirlar) _output.WriteLine($"{s.Ad}: başparmak kenarı {s.Knob:0.00}:1, dolu iz {s.Dolu:0.00}:1");
        var dusuk = satirlar.Where(s => s.Knob < 3).Select(s => s.Ad).ToList();
        Assert.Equal(PalettenEksikVurgu, dusuk);
    }

    [Fact]
    public void DegerKutusuKaydiricininYanindaVeRakamlarEsitGenislikte()
    {
        var (kaydirici, kutu, bir, sekiz, metin) = AppHost.Run(() =>
        {
            var w = new MainWindow { Width = double.NaN, Height = double.NaN };
            GorselDenetimTests.Yerlestir(w, GorselDenetimTests.Genis);
            try
            {
                var s = w.FindControl<Slider>("SliderTarget")!;
                var t = w.FindControl<TextBox>("TxtTarget")!;
                s.Focus();
                Tus(s, Key.End);
                w.UpdateLayout();
                var yuz = new Typeface(t.FontFamily, t.FontStyle, t.FontWeight);
                double Gen(string x) => new TextLayout(x, yuz, t.FontSize, Brushes.Black).WidthIncludingTrailingWhitespace;
                return (GorselDenetimTests.Yeri(s, w), GorselDenetimTests.Yeri(t, w), Gen("1111"), Gen("8888"), t.Text);
            }
            finally { w.Close(); }
        });
        _output.WriteLine($"kaydırıcı {kaydirici}, kutu {kutu}, 1111 {bir:0.##}, 8888 {sekiz:0.##}, metin {metin}");
        Assert.Equal("500", metin);
        Assert.True(kutu.Left >= kaydirici.Right, "Değer kutusu kaydırıcının sağında değil.");
        Assert.InRange(kutu.Center.Y, kaydirici.Top, kaydirici.Bottom);
        Assert.Equal(bir, sekiz, 2);
    }

    [Fact]
    public void DurumlarOlcekleriyleCizilir()
    {
        var cikti = Environment.GetEnvironmentVariable("VIDSHRINK_KAYDIRICI_CIKTI");
        var farklar = Olc((p, k) =>
        {
            var sonuc = new Dictionary<string, byte[]>();
            foreach (var olcek in new[] { 1.0, 1.25, 1.5 })
            foreach (var d in Durumlar)
            {
                Zorla(k, d);
                p.UpdateLayout();
                var kok = (Control)p.GetVisualChildren().Single();
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)(kok.Bounds.Width * olcek), (int)(kok.Bounds.Height * olcek)), new Vector(96 * olcek, 96 * olcek));
                bitmap.Render(kok);
                using var bellek = new MemoryStream();
                bitmap.Save(bellek, PngBitmapEncoderOptions.Default);
                sonuc[d + "-" + olcek.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)] = bellek.ToArray();
                if (cikti is not null)
                {
                    Directory.CreateDirectory(cikti);
                    File.WriteAllBytes(Path.Combine(cikti, $"kaydirici-{d}-{(int)(olcek * 100)}.png"), bellek.ToArray());
                }
            }
            return sonuc;
        }, 0, 100, 40);
        foreach (var olcek in new[] { "1.00", "1.25", "1.50" })
        {
            var dinlenik = farklar[$"dinlenik-{olcek}"];
            foreach (var d in Durumlar.Skip(1))
                Assert.False(dinlenik.SequenceEqual(farklar[$"{d}-{olcek}"]), $"{d} ölçek {olcek}: dinlenik durumdan ayırt edilmiyor.");
        }
    }
}
