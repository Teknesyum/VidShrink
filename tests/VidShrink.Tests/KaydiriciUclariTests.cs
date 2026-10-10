using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VidShrink.App.Themes;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Kaydırıcının uçları: tutamak daire, izin dış ucu hap, dolu ile boş izin birleşimi tutamağın
/// altında. İki tema ölçülür: oynatıcının ses ve hız çubuğu (<c>PlaybackSlider</c>) ve varsayılan
/// <see cref="Slider"/>. Köşe ölçüsü çizilen kareden okunur, biçim adından değil.
/// <c>VIDSHRINK_KAYDIRICI_UCLARI_CIKTI</c> verilirse bir koyu ve bir açık palette yakın çekim yazar.
/// </summary>
public sealed class KaydiriciUclariTests
{
    private const string Oynatici = "PlaybackSlider";
    private const string Varsayilan = "";
    private const double Olcek = 4;

    private readonly ITestOutputHelper _output;

    public KaydiriciUclariTests(ITestOutputHelper output) => _output = output;

    private static Slider Kur(string tema, double deger, double genislik = 192)
    {
        var kaydirici = new Slider
        {
            Width = genislik,
            Minimum = 0,
            Maximum = 100,
            Value = deger,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        if (tema.Length > 0) kaydirici.Theme = (ControlTheme)Application.Current!.FindResource(tema)!;
        return kaydirici;
    }

    private static IBrush Firca(string ad) => (IBrush)Application.Current!.FindResource(ad)!;

    private static Shape Knob(Slider k) => k.GetVisualDescendants().OfType<Shape>().Single(s => s.Name == "Knob");

    private static T Ciz<T>(string tema, double deger, Func<Border, Slider, T> olc) => AppHost.Run(() =>
    {
        var kaydirici = Kur(tema, deger);
        var zemin = new Border
        {
            Background = Firca("AppBg"),
            Padding = new Thickness(16),
            Child = kaydirici,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        var pencere = new Window { Width = 400, Height = 200, Content = zemin };
        pencere.Show();
        pencere.UpdateLayout();
        try { return olc(zemin, kaydirici); }
        finally { pencere.Close(); }
    });

    [Theory]
    [InlineData(Oynatici)]
    [InlineData(Varsayilan)]
    public void TutamakDaire(string tema)
    {
        var (tur, en, boy) = Ciz(tema, 50, (_, k) =>
        {
            var knob = Knob(k);
            return (knob.GetType().Name, knob.Bounds.Width, knob.Bounds.Height);
        });
        _output.WriteLine($"{(tema.Length > 0 ? tema : "Slider")}: {tur} {en}x{boy}");
        Assert.Equal(nameof(Ellipse), tur);
        Assert.Equal(en, boy, 3);
    }

    [Theory]
    [InlineData(Oynatici, 0)]
    [InlineData(Oynatici, 50)]
    [InlineData(Oynatici, 100)]
    [InlineData(Varsayilan, 0)]
    [InlineData(Varsayilan, 50)]
    [InlineData(Varsayilan, 100)]
    public void IzinDisUcuHapBirlesimTutamaginAltinda(string tema, double deger)
    {
        var (dolu, doluKose, bos, bosKose, knob) = Ciz(tema, deger, (_, k) =>
        {
            var doluIz = GorselDenetimTests.Ad<Border>(k, "FilledTrack");
            var bosIz = GorselDenetimTests.Ad<Border>(k, "RemainingTrack");
            return (GorselDenetimTests.Yeri(doluIz, k), doluIz.CornerRadius, GorselDenetimTests.Yeri(bosIz, k), bosIz.CornerRadius,
                    GorselDenetimTests.Yeri(Knob(k), k));
        });
        _output.WriteLine($"dolu {dolu} {doluKose}, bos {bos} {bosKose}, tutamak {knob}");

        Assert.Equal(dolu.Height / 2, doluKose.TopLeft, 3);
        Assert.Equal(dolu.Height / 2, doluKose.BottomLeft, 3);
        Assert.Equal(bos.Height / 2, bosKose.TopRight, 3);
        Assert.Equal(bos.Height / 2, bosKose.BottomRight, 3);
        Assert.Equal(knob.Center.X, dolu.Right, 1);
        Assert.Equal(knob.Center.X, bos.Left, 1);
        Assert.True(dolu.Height < knob.Height && bos.Height < knob.Height, "İz tutamaktan yüksek, ucu tutamağın altında kalmaz.");
        if (deger <= 0)
            Assert.True(dolu.Left >= knob.Left - 0.5, $"Dolu izin ucu tutamağın solunda açıkta: iz {dolu.Left:0.#}, tutamak {knob.Left:0.#}.");
        if (deger >= 100)
            Assert.True(bos.Right <= knob.Right + 0.5, $"Boş izin ucu tutamağın sağında açıkta: iz {bos.Right:0.#}, tutamak {knob.Right:0.#}.");
    }

    [Theory]
    [InlineData(Oynatici, 0)]
    [InlineData(Oynatici, 100)]
    [InlineData(Varsayilan, 0)]
    [InlineData(Varsayilan, 100)]
    public void UctaTutamaginKosesiVeIzinUcuCizilmez(string tema, double deger)
    {
        var (koseler, disi, kenar) = Ciz(tema, deger, (zemin, k) =>
        {
            var knob = GorselDenetimTests.Yeri(Knob(k), zemin);
            using var kare = Cek(zemin);
            var pikseller = Pikseller(kare, out var en);
            var bos = Oku(pikseller, en, 2, 2);
            bool Zemin(double x, double y) => Yakin(Oku(pikseller, en, x, y), bos);

            var kose = new[]
            {
                Zemin(knob.Left + 1, knob.Top + 1), Zemin(knob.Right - 1, knob.Top + 1),
                Zemin(knob.Left + 1, knob.Bottom - 1), Zemin(knob.Right - 1, knob.Bottom - 1),
            };
            var x = deger == 0 ? knob.Left - 1 : knob.Right + 1;
            var temiz = Enumerable.Range(0, (int)zemin.Bounds.Height - 4).All(y => Zemin(x, y + 2));
            return (kose, temiz, !Zemin(knob.Left + 1, knob.Center.Y));
        });

        Assert.True(kenar, "Tutamağın kenar çizgisi okunamadı; ölçü kör.");
        Assert.All(koseler, zemin => Assert.True(zemin, "Tutamağın kutu köşesinde çizim var: tutamak köşeli."));
        Assert.True(disi, "Uçta tutamağın dışına iz taşıyor.");
    }

    private static RenderTargetBitmap Cek(Control kok)
    {
        var kare = new RenderTargetBitmap(
            new PixelSize((int)Math.Ceiling(kok.Bounds.Width * Olcek), (int)Math.Ceiling(kok.Bounds.Height * Olcek)),
            new Vector(96 * Olcek, 96 * Olcek));
        kare.Render(kok);
        return kare;
    }

    private static byte[] Pikseller(RenderTargetBitmap kare, out int en)
    {
        en = kare.PixelSize.Width;
        var pikseller = new byte[en * kare.PixelSize.Height * 4];
        var tutamak = GCHandle.Alloc(pikseller, GCHandleType.Pinned);
        try { kare.CopyPixels(new PixelRect(0, 0, en, kare.PixelSize.Height), tutamak.AddrOfPinnedObject(), pikseller.Length, en * 4); }
        finally { tutamak.Free(); }
        return pikseller;
    }

    private static (byte, byte, byte) Oku(byte[] pikseller, int en, double x, double y)
    {
        var i = ((int)(y * Olcek) * en + (int)(x * Olcek)) * 4;
        return (pikseller[i], pikseller[i + 1], pikseller[i + 2]);
    }

    private static bool Yakin((byte, byte, byte) a, (byte, byte, byte) b)
        => Math.Abs(a.Item1 - b.Item1) <= 2 && Math.Abs(a.Item2 - b.Item2) <= 2 && Math.Abs(a.Item3 - b.Item3) <= 2;

    private static void Zorla(Slider k, string durum)
    {
        foreach (var parca in k.GetVisualDescendants().OfType<Control>()) parca.Transitions = null;
        var sinif = (IPseudoClasses)k.Classes;
        k.IsEnabled = durum != "devre dışı";
        if (durum is "üstünde" or "basılı") sinif.Set(":pointerover", true);
        if (durum == "basılı") sinif.Set(":pressed", true);
        if (durum == "odak") sinif.Set(":focus-visible", true);
    }

    private static RenderTargetBitmap Blok(string palet)
    {
        PaletteCatalog.Use(palet);
        var yazi = Firca("TextBody");
        var govde = new StackPanel { Spacing = 8 };
        govde.Children.Add(new TextBlock { Text = palet, Foreground = yazi, FontSize = 13, FontWeight = FontWeight.SemiBold });
        var zorlanacak = new List<(Slider, string)>();

        foreach (var (tema, ad) in new[] { (Oynatici, "oynatıcı"), (Varsayilan, "varsayılan") })
        {
            var satirlar = new UniformGrid { Columns = 4 };
            foreach (var (etiket, deger, durum) in new[]
                     {
                         ("%0", 0d, ""), ("%50", 50d, ""), ("%100", 100d, ""), ("%3", 3d, ""),
                         ("üstünde", 50d, "üstünde"), ("basılı", 50d, "basılı"), ("odak", 50d, "odak"), ("devre dışı", 50d, "devre dışı"),
                     })
            {
                var kaydirici = Kur(tema, deger, 140);
                if (durum.Length > 0) zorlanacak.Add((kaydirici, durum));
                var hucre = new StackPanel { Margin = new Thickness(0, 0, 24, 8) };
                hucre.Children.Add(new TextBlock { Text = $"{ad} {etiket}", Foreground = yazi, FontSize = 10 });
                hucre.Children.Add(kaydirici);
                satirlar.Children.Add(hucre);
            }
            govde.Children.Add(satirlar);
        }

        var zemin = new Border
        {
            Background = Firca("AppBg"),
            Padding = new Thickness(16),
            Child = govde,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        var pencere = new Window { Width = 1000, Height = 700, Content = zemin };
        pencere.Show();
        pencere.UpdateLayout();
        try
        {
            foreach (var (kaydirici, durum) in zorlanacak) Zorla(kaydirici, durum);
            pencere.UpdateLayout();
            return Cek(zemin);
        }
        finally { pencere.Close(); }
    }

    [Fact]
    public void YakinCekim()
    {
        var klasor = Environment.GetEnvironmentVariable("VIDSHRINK_KAYDIRICI_UCLARI_CIKTI");
        if (string.IsNullOrEmpty(klasor)) return;
        var ad = Environment.GetEnvironmentVariable("VIDSHRINK_KAYDIRICI_UCLARI_AD") ?? "kaydirici-uclari.png";

        var boyut = AppHost.Run(() =>
        {
            try
            {
                var bloklar = new[] { PaletteCatalog.Default, "GithubLight" }.Select(Blok).ToList();
                try
                {
                    var en = bloklar.Max(blok => blok.PixelSize.Width);
                    var boy = bloklar.Sum(blok => blok.PixelSize.Height);
                    using var kare = new WriteableBitmap(new PixelSize(en, boy), new Vector(96, 96),
                        bloklar[0].Format ?? Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
                    using (var kilit = kare.Lock())
                    {
                        var satir = 0;
                        foreach (var blok in bloklar)
                        {
                            var pikseller = Pikseller(blok, out var blokEn);
                            for (var y = 0; y < blok.PixelSize.Height; y++)
                                Marshal.Copy(pikseller, y * blokEn * 4, kilit.Address + (satir + y) * kilit.RowBytes, blokEn * 4);
                            satir += blok.PixelSize.Height;
                        }
                    }
                    Directory.CreateDirectory(klasor);
                    kare.Save(System.IO.Path.Combine(klasor, ad), PngBitmapEncoderOptions.Default);
                    return kare.PixelSize;
                }
                finally
                {
                    foreach (var blok in bloklar) blok.Dispose();
                }
            }
            finally { PaletteCatalog.Use(PaletteCatalog.Default); }
        });

        _output.WriteLine($"{System.IO.Path.Combine(klasor, ad)}: {boyut.Width}x{boyut.Height}");
        Assert.True(boyut.Width > 0 && boyut.Height > 0);
    }
}
