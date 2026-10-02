using System.Globalization;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Sekil = Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Themes;
using Xunit;

namespace VidShrink.Tests;

public sealed class KontrastTests
{
    private const double YaziEsigi = 7.0;
    private const double SimgeEsigi = 3.0;
    private static readonly double[] Olcekler = { 1.0, 1.25, 1.5 };
    private static readonly string[] Durumlar = { ":pointerover", ":pressed", ":focus-visible", ":selected", ":checked", ":open" };
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private sealed record Olcum(string Ekran, string Durum, string Tur, string Yol, string Metin, string Zemin, string On, double Oran)
    {
        public string Anahtar { get; set; } = "";
    }

    public static TheoryData<string> Paletler()
    {
        var veri = new TheoryData<string>();
        foreach (var ad in PaletteCatalog.Names) veri.Add(ad);
        return veri;
    }

    private static double Esik(string tur) => tur == "simge" ? SimgeEsigi : YaziEsigi;

    [Theory]
    [MemberData(nameof(Paletler))]
    public void HerYaziVeSimgeEsigiGeciyor(string palet)
    {
        var klasor = Environment.GetEnvironmentVariable("UC_KLASOR");
        var asama = Environment.GetEnvironmentVariable("UC_ASAMA") ?? "olcum";
        var cekilecek = (Environment.GetEnvironmentVariable("UC_CEK") ?? PaletteCatalog.Default).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var cek = klasor != null && (cekilecek.Contains(palet) || cekilecek.Contains("*"));
        var olcumler = PaletteIcinde(palet, hepsi =>
        {
            foreach (var (ekran, kur) in Ekranlar())
            {
                var pencere = kur();
                try
                {
                    Assert.Equal(palet, PaletteCatalog.Current);
                    var kok = Yerlestir(pencere);
                    if (cek) Cek(pencere, kok, palet + "-" + ekran, asama, klasor!);
                    Tara(ekran, "dinlenik", pencere, pencere, hepsi);
                    ZorlaGoster(pencere);
                    Yerlestir(pencere);
                    Tara(ekran, "dinlenik", pencere, pencere, hepsi);
                    Durumlari(ekran, pencere, hepsi);
                }
                finally
                {
                    pencere.Close();
                }
            }
        });

        var tekil = olcumler.GroupBy(o => o.Ekran + "|" + o.Durum + "|" + o.Tur + "|" + o.Yol + "|" + o.On + "|" + o.Zemin).Select(g => g.First()).ToList();
        var altinda = tekil.Where(o => o.Durum != "edilgen" && o.Oran < Esik(o.Tur)).ToList();
        var bulgular = altinda;
        if (klasor != null)
        {
            Directory.CreateDirectory(klasor);
            var sb = new StringBuilder("palet\tekran\tdurum\ttur\toran\tzemin\ton\tanahtar\tyol\tmetin\n");
            foreach (var o in tekil.OrderBy(o => o.Oran))
                sb.Append(palet).Append('\t').Append(o.Ekran).Append('\t').Append(o.Durum).Append('\t').Append(o.Tur).Append('\t')
                  .Append(o.Oran.ToString("0.00", Inv)).Append('\t').Append(o.Zemin).Append('\t').Append(o.On).Append('\t').Append(o.Anahtar).Append('\t')
                  .Append(o.Yol).Append('\t').Append(o.Metin).Append('\n');
            File.WriteAllText(Path.Combine(klasor, "kontrast-" + asama + "-" + palet + ".tsv"), sb.ToString());
        }

        Assert.True(bulgular.Count == 0,
            palet + ": " + bulgular.Count + " çift eşiğin altında (" + tekil.Count + " ölçüm)\n" +
            string.Join("\n", bulgular.OrderBy(o => o.Oran).Select(o => o.Ekran + " [" + o.Durum + "] " + o.Tur + " " + o.Anahtar + " " + o.Yol + " \"" + o.Metin + "\": bg " + o.Zemin + " fg " + o.On + " " + o.Oran.ToString("0.00", Inv))));
    }

    [Theory]
    [MemberData(nameof(Paletler))]
    public void MedyaAdiVideoUstundeEsigiGeciyor(string palet)
    {
        var olcumler = PaletteIcinde(palet, hepsi =>
        {
            var (ekran, kur) = Ekranlar().First();
            var pencere = kur();
            try
            {
                Yerlestir(pencere);
                ZorlaGoster(pencere);
                Yerlestir(pencere);
                var basliklar = pencere.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "MediaTitle").ToList();
                Assert.NotEmpty(basliklar);
                foreach (var baslik in basliklar)
                {
                    var sahne = baslik.GetVisualAncestors().OfType<Border>().First(b => b.Name == "Stage");
                    var yazi = baslik.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "TxtMediaTitle");
                    var sozde = (IPseudoClasses)baslik.Classes;
                    foreach (var durum in new[] { "dinlenik", "pointerover", "menude" })
                    {
                        if (durum == "pointerover") sozde.Set(":pointerover", true);
                        if (durum == "menude") baslik.Classes.Add("menude");
                        Dispatcher.UIThread.RunJobs();
                        Olc(ekran + "-medyali", durum, yazi, hepsi, false, sahne);
                        sozde.Set(":pointerover", false);
                        baslik.Classes.Remove("menude");
                    }
                }
            }
            finally
            {
                pencere.Close();
            }
        });

        Assert.NotEmpty(olcumler);
        var altinda = olcumler.Where(o => o.Oran < YaziEsigi).ToList();
        Assert.True(altinda.Count == 0,
            palet + ": medya adı video üstünde eşiğin altında\n" +
            string.Join("\n", altinda.Select(o => "[" + o.Durum + "] " + o.Anahtar + " " + o.Yol + ": bg " + o.Zemin + " fg " + o.On + " " + o.Oran.ToString("0.00", Inv))));
    }

    private static List<Olcum> PaletteIcinde(string palet, Action<List<Olcum>> govde)
    {
        var ayarKlasoru = Path.Combine(TestPaths.OutputRoot, "kontrast", palet);
        return AppHost.Run(() =>
        {
            var hepsi = new List<Olcum>();
            var kultur = CultureInfo.CurrentUICulture;
            var ayar = Environment.GetEnvironmentVariable(TestAyarYolu.Degisken);
            Directory.CreateDirectory(ayarKlasoru);
            File.WriteAllText(Path.Combine(ayarKlasoru, "settings.json"), "{\"theme\":\"" + palet + "\",\"language\":\"tr\"}");
            Environment.SetEnvironmentVariable(TestAyarYolu.Degisken, Path.Combine(ayarKlasoru, "settings.json"));
            CultureInfo.CurrentUICulture = new CultureInfo("tr");
            Strings.Use("tr");
            try
            {
                Assert.Equal(palet, PaletteCatalog.Use(palet));
                var adlar = RenkAdlari();
                govde(hepsi);
                foreach (var o in hepsi) o.Anahtar = adlar.TryGetValue(o.On, out var ad) ? ad : o.On;
            }
            finally
            {
                Environment.SetEnvironmentVariable(TestAyarYolu.Degisken, ayar);
                PaletteCatalog.Use(PaletteCatalog.Default);
                CultureInfo.CurrentUICulture = kultur;
                Strings.Use("en");
                try { Directory.Delete(ayarKlasoru, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return hepsi;
        });
    }

    private static Dictionary<string, string> RenkAdlari()
    {
        var adlar = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Application.Current?.Resources.MergedDictionaries is not { Count: > 0 } birlesik) return adlar;
        var sozluk = birlesik[0] is Avalonia.Markup.Xaml.Styling.ResourceInclude ri ? ri.Loaded : birlesik[0] as IResourceDictionary;
        if (sozluk == null) return adlar;
        foreach (var anahtar in sozluk.Keys)
            if (sozluk.TryGetValue(anahtar, out var deger) && deger is Color c)
                adlar.TryAdd(Hex(c), anahtar.ToString()!.Replace("Color", ""));
        return adlar;
    }

    private static VidShrink.Core.Editing.EditTimeline DuzenleyiciOrnegi()
    {
        var model = VidShrink.Core.Editing.EditTimeline.FromSource(VidShrink.Core.Editing.EditTime.FromSeconds(60));
        model.Split(VidShrink.Core.Editing.EditTime.FromSeconds(20));
        model.Split(VidShrink.Core.Editing.EditTime.FromSeconds(40));
        model.SetSpeed(1, 2m);
        model.SetSpeed(2, -1m);
        return model;
    }

    private static IEnumerable<(string, Func<Window>)> Ekranlar()
    {
        string[] sekmeler = { "oynatici", "duzenleyici", "kucult", "donustur", "kaydedici", "gelismis", "ayarlar" };
        for (var i = 0; i < sekmeler.Length; i++)
        {
            var sira = i;
            yield return ("ana-" + sekmeler[i], () =>
            {
                var w = new MainWindow { Width = double.NaN, Height = double.NaN };
                Yerlestir(w);
                var tabs = w.FindControl<TabControl>("Tabs")!;
                foreach (var host in tabs.GetVisualDescendants().OfType<TransitioningContentControl>()) host.PageTransition = null;
                if (tabs.ContainerFromIndex(sira) is TabItem ti) ti.IsVisible = true;
                var adv = w.FindControl<TabItem>("TabAdvanced");
                if (adv != null) adv.IsVisible = true;
                tabs.SelectedIndex = sira;
                if (sira == w.EditorTabIndex) w.EditorPaneForTest.ShowTimeline(DuzenleyiciOrnegi(), 30);
                w.FindControl<VidShrink.App.Playback.ComparisonPanel>("Preview")?.Controls.SetEncodeProgress(0.22, 1, 2, 2);
                Dispatcher.UIThread.RunJobs();
                return w;
            });
        }

        yield return ("kucultme-isi", () => new ShrinkJobWindow());

        var app = typeof(MainWindow).Assembly;
        foreach (var ad in new[] { "RecorderMini", "RecorderRegionPicker", "RecorderRegionEditor", "RecorderFrame", "RecorderKeyCaption", "RecorderMagnifier", "RecorderClickRing" })
        {
            var tip = app.GetTypes().Single(t => t.Name == ad);
            yield return (ad, () => (Window)Activator.CreateInstance(tip, true)!);
        }

        yield return ("acilir-liste", () =>
        {
            var liste = new StackPanel();
            foreach (var m in new[] { "Örnek seçenek", "Seçili seçenek", "Üstünde gezilen" })
                liste.Children.Add(new ComboBoxItem { Content = m });
            ((ComboBoxItem)liste.Children[1]).IsSelected = true;
            var yuzey = new Border { Child = liste };
            yuzey.Bind(Border.BackgroundProperty, yuzey.GetResourceObservable("Surface"));
            var ipucu = new ToolTip { Content = "İpucu metni" };
            var kutu = new FlyoutPresenter { Content = "Açılır kutu metni" };
            var sagTik = new MenuFlyoutPresenter
            {
                Items =
                {
                    new MenuItem { Header = "Oynat", InputGesture = new KeyGesture(Key.Space) },
                    new MenuItem { Header = "Altyazı", Items = { new MenuItem { Header = "Kapalı" } } },
                    new MenuItem { Header = "Seçilemez", IsEnabled = false, InputGesture = new KeyGesture(Key.E, KeyModifiers.Control) }
                }
            };
            return new Window
            {
                Width = 480,
                Height = 480,
                Content = new StackPanel { Children = { new ComboBox(), yuzey, ipucu, kutu, sagTik } }
            };
        });
    }

    private static Size Boyut(Window w)
    {
        var gen = double.IsNaN(w.Width) || w.Width <= 0 ? 1600 : w.Width;
        var yuk = double.IsNaN(w.Height) || w.Height <= 0 ? 1000 : w.Height;
        return new Size(gen, yuk);
    }

    private static Layoutable Yerlestir(Window w)
    {
        var alan = Boyut(w);
        w.Measure(alan);
        w.Arrange(new Rect(alan));
        w.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Durdur(w);
        foreach (var n in w.GetVisualDescendants().OfType<Layoutable>()) n.InvalidateMeasure();
        var kok = (Layoutable)w.GetVisualChildren().Single();
        kok.Measure(alan);
        kok.Arrange(new Rect(kok.DesiredSize.Height > 0 && w.SizeToContent != SizeToContent.Manual ? new Size(alan.Width, kok.DesiredSize.Height) : alan));
        Dispatcher.UIThread.RunJobs();
        return kok;
    }

    private static void Durdur(Window w)
    {
        foreach (var n in new Visual[] { w }.Concat(w.GetVisualDescendants()))
        {
            if (n is Control c)
            {
                c.Transitions = null;
                c.Classes.Remove("enter");
                c.Classes.Remove("enter-flat");
            }
            if (n.RenderTransform is not TranslateTransform) n.RenderTransform = null;
        }
        var settle = typeof(MainWindow).GetMethod("SettleFades", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (w is MainWindow) settle?.Invoke(w, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Cek(Window w, Layoutable kok, string ekran, string asama, string klasor)
    {
        var b = kok.Bounds;
        if (b.Width <= 0 || b.Height <= 0) return;
        Directory.CreateDirectory(klasor);
        var golgeler = kok.GetVisualDescendants().OfType<Border>().ToList();
        foreach (var s in Olcekler)
        {
            var bastirma = s == 1.0 ? new List<IDisposable>() : golgeler.Select(bd => bd.SetValue(Border.BoxShadowProperty, default(BoxShadows), Avalonia.Data.BindingPriority.Animation)).OfType<IDisposable>().ToList();
            var etiket = ((int)Math.Round(s * 100)).ToString(Inv);
            var boyut = new PixelSize((int)Math.Ceiling(b.Width * s), (int)Math.Ceiling(b.Height * s));
            using var katman = new RenderTargetBitmap(boyut, new Vector(96 * s, 96 * s));
            katman.Render(kok);
            using var akis = new MemoryStream();
            katman.Save(akis, PngBitmapEncoderOptions.Default);
            akis.Position = 0;
            using var duz = new Bitmap(akis);
            using var bmp = new RenderTargetBitmap(boyut, new Vector(96, 96));
            using (var ctx = bmp.CreateDrawingContext())
            {
                var alan = new Rect(0, 0, boyut.Width, boyut.Height);
                if (w.Background != null) ctx.FillRectangle(w.Background, alan);
                ctx.DrawImage(duz, alan);
            }
            bmp.Save(Path.Combine(klasor, ekran + "-" + etiket + "-" + asama + ".png"), PngBitmapEncoderOptions.Default);
            foreach (var d in bastirma) d.Dispose();
        }
    }

    private static void ZorlaGoster(Window w)
    {
        for (var tur = 0; tur < 3; tur++)
        {
            foreach (var n in w.GetVisualDescendants().OfType<Control>().ToList())
            {
                if (n is TabItem || n is Popup) continue;
                if (!n.IsVisible) n.IsVisible = true;
                if (n.Opacity < 1 && n is not Sekil.Shape) n.Opacity = 1;
                if (n is TextBlock tb && tb is not AccessText && string.IsNullOrWhiteSpace(tb.Text) && tb.Inlines is not { Count: > 0 }) tb.Text = "Örnek 0.9";
            }
            Yerlestir(w);
        }
    }

    private static void Durumlari(string ekran, Window w, List<Olcum> hepsi)
    {
        var etkilesimli = w.GetVisualDescendants().OfType<TemplatedControl>()
            .Where(c => c is Button || c is TabItem || c is MenuItem || c is ComboBoxItem || c is ListBoxItem || c is ComboBox || c is TextBox || c is Slider)
            .ToList();
        foreach (var c in etkilesimli)
        {
            var sozde = (IPseudoClasses)c.Classes;
            foreach (var d in Durumlar)
            {
                if (d == ":checked" && c is not ToggleButton) continue;
                if (d == ":selected" && c is not (TabItem or ListBoxItem or MenuItem)) continue;
                if (d == ":open" && c is not MenuItem { ItemCount: > 0 }) continue;
                var vardi = c.Classes.Contains(d);
                var seciliVardi = c.Classes.Contains(":selected");
                sozde.Set(d, true);
                if (d == ":open") sozde.Set(":selected", true);
                Dispatcher.UIThread.RunJobs();
                Tara(ekran, d.TrimStart(':'), c, w, hepsi);
                if (!vardi) sozde.Set(d, false);
                if (d == ":open" && !seciliVardi) sozde.Set(":selected", false);
            }
            if (c.IsEnabled)
            {
                c.IsEnabled = false;
                Dispatcher.UIThread.RunJobs();
                Tara(ekran, "edilgen", c, w, hepsi, edilgen: true);
                c.IsEnabled = true;
                Dispatcher.UIThread.RunJobs();
            }
        }
    }

    private static void Tara(string ekran, string durum, Visual kapsam, Window w, List<Olcum> hepsi, bool edilgen = false)
    {
        foreach (var d in new[] { kapsam }.Concat(kapsam.GetVisualDescendants()))
            Olc(ekran, durum, d, hepsi, edilgen);
    }

    private static void Olc(string ekran, string durum, Visual d, List<Olcum> hepsi, bool edilgen, Visual? video = null)
    {
        if (!Gorunur(d)) return;
        if (!edilgen && d is InputElement ie && !ie.IsEffectivelyEnabled) return;
        IBrush? on = null;
        string? metin = null;
        var tur = "yazi";
        if (d is TextBlock tb)
        {
            on = tb.Foreground;
            metin = tb.Text;
            if (string.IsNullOrWhiteSpace(metin) && tb.Inlines is { Count: > 0 }) metin = string.Concat(tb.Inlines.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text));
        }
        else if (d is ContentPresenter cp && cp.Content is string s && !cp.GetVisualChildren().Any())
        {
            on = cp.Foreground;
            metin = s;
        }
        else if (d is TextPresenter tp)
        {
            on = tp.Foreground;
            metin = tp.Text;
        }
        else if (d is PathIcon pi)
        {
            on = pi.Foreground;
            metin = pi.Name ?? "PathIcon";
            tur = "simge";
        }
        else if (d is Sekil.Path p && p.GetVisualAncestors().Any(a => a is Button || a is TabItem || a is ToggleButton || a is MenuItem))
        {
            on = p.Fill ?? p.Stroke;
            metin = p.Name ?? "Path";
            tur = "simge";
        }
        if (string.IsNullOrWhiteSpace(metin)) return;
        if (on is not ISolidColorBrush fg) return;
        if (Saydamlik(d) < 0.05) return;
        var zincir = new List<Visual>();
        for (var n = d; n != null && n != video; n = n.GetVisualParent()) zincir.Insert(0, n);
        var dolgular = new List<List<double[]>>();
        foreach (var n in zincir)
        {
            var b = Dolgu(n);
            if (b == null) dolgular.Add(new() { new double[] { 0, 0, 0, 0 } });
            else if (b is ISolidColorBrush sb) dolgular.Add(new() { Renk(sb.Color, sb.Opacity) });
            else if (b is IGradientBrush gb) dolgular.Add(gb.GradientStops.Select(st => Renk(st.Color, gb.Opacity)).ToList());
            else dolgular.Add(Ornekle(b, n, d));
        }
        var onRenk = Renk(fg.Color, fg.Opacity);
        var secimler = new List<int[]> { new int[zincir.Count] };
        for (var i = 0; i < zincir.Count; i++)
        {
            if (dolgular[i].Count < 2) continue;
            secimler = secimler.SelectMany(sec => Enumerable.Range(0, dolgular[i].Count).Select(j => { var k = (int[])sec.Clone(); k[i] = j; return k; })).Take(64).ToList();
        }
        double enKotu = double.MaxValue;
        double[] enKotuZemin = { 0, 0, 0, 1 };
        foreach (var taban in new[] { new double[] { 255, 255, 255, 1 }, new double[] { 0, 0, 0, 1 } })
            foreach (var sec in secimler)
            {
                var yazi = Ciz(zincir, dolgular, sec, 0, taban, onRenk);
                var zemin = Ciz(zincir, dolgular, sec, 0, taban, null);
                var oran = Oran(yazi, zemin);
                if (oran < enKotu)
                {
                    enKotu = oran;
                    enKotuZemin = zemin;
                }
            }
        hepsi.Add(new Olcum(ekran, edilgen ? "edilgen" : durum, tur, Yol(d), Kisalt(metin), Hex(enKotuZemin), Hex(fg.Color), Math.Round(enKotu, 2)));
    }

    private static double[] Ciz(List<Visual> zincir, List<List<double[]>> dolgular, int[] sec, int i, double[] alt, double[]? yazi)
    {
        var boyali = Ustune(dolgular[i][sec[i]], alt);
        var ic = i == zincir.Count - 1 ? (yazi == null ? boyali : Ustune(yazi, boyali)) : Ciz(zincir, dolgular, sec, i + 1, boyali, yazi);
        var op = zincir[i].Opacity;
        return new[] { alt[0] * (1 - op) + ic[0] * op, alt[1] * (1 - op) + ic[1] * op, alt[2] * (1 - op) + ic[2] * op, 1.0 };
    }

    private static List<double[]> Ornekle(IBrush b, Visual n, Visual d)
    {
        var alan = n.Bounds.Size;
        var yedek = new List<double[]> { new double[] { 255, 255, 255, b.Opacity }, new double[] { 0, 0, 0, b.Opacity } };
        if (alan.Width <= 0 || alan.Height <= 0) return yedek;
        var k = Math.Min(1.0, 400 / Math.Max(alan.Width, alan.Height));
        var px = new PixelSize(Math.Max(1, (int)(alan.Width * k)), Math.Max(1, (int)(alan.Height * k)));
        using var bmp = new RenderTargetBitmap(px, new Vector(96 * k, 96 * k));
        using (var ctx = bmp.CreateDrawingContext()) ctx.FillRectangle(b, new Rect(alan));
        var adim = px.Width * 4;
        var tampon = new byte[adim * px.Height];
        var ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(tampon.Length);
        try
        {
            bmp.CopyPixels(new PixelRect(px), ptr, tampon.Length, adim);
            System.Runtime.InteropServices.Marshal.Copy(ptr, tampon, 0, tampon.Length);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
        }
        var donusum = d.TransformToVisual(n);
        var kutu = donusum.HasValue ? new Rect(d.Bounds.Size).TransformToAABB(donusum.Value) : new Rect(alan);
        var x0 = Math.Clamp((int)(kutu.X * k), 0, px.Width - 1);
        var y0 = Math.Clamp((int)(kutu.Y * k), 0, px.Height - 1);
        var x1 = Math.Clamp((int)Math.Ceiling(kutu.Right * k), x0 + 1, px.Width);
        var y1 = Math.Clamp((int)Math.Ceiling(kutu.Bottom * k), y0 + 1, px.Height);
        double[]? koyu = null, acik = null;
        double enAz = double.MaxValue, enCok = double.MinValue;
        for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++)
            {
                var i = y * adim + x * 4;
                var a = tampon[i + 3] / 255.0;
                var c = a > 0 ? new double[] { tampon[i + 2] / a, tampon[i + 1] / a, tampon[i] / a, a } : new double[] { 0, 0, 0, 0 };
                var l = Parlaklik(c) * a + (1 - a) * 0.5;
                if (l < enAz) { enAz = l; koyu = c; }
                if (l > enCok) { enCok = l; acik = c; }
            }
        return koyu == null || acik == null ? yedek : new List<double[]> { koyu, acik };
    }

    private static bool Gorunur(Visual d)
    {
        for (var n = d; n != null && n is not Window; n = n.GetVisualParent())
            if (!n.IsVisible) return false;
        return true;
    }

    private static IBrush? Dolgu(Visual n) => n switch
    {
        Panel p => p.Background,
        Border b => b.Background,
        ContentPresenter cp => cp.Background,
        TemplatedControl tc => tc.Background,
        TextBlock t => t.Background,
        _ => null
    };

    private static double Saydamlik(Visual d)
    {
        var toplam = 1.0;
        for (var n = d; n != null; n = n.GetVisualParent()) toplam *= n.Opacity;
        return toplam;
    }

    private static double[] Renk(Color c, double opaklik) => new double[] { c.R, c.G, c.B, c.A / 255.0 * opaklik };

    private static double[] Ustune(double[] ust, double[] alt)
    {
        var a = ust[3] + alt[3] * (1 - ust[3]);
        if (a <= 0) return new double[] { 0, 0, 0, 0 };
        double Karis(int i) => (ust[i] * ust[3] + alt[i] * alt[3] * (1 - ust[3])) / a;
        return new[] { Karis(0), Karis(1), Karis(2), a };
    }

    private static double Kanal(double v)
    {
        var s = v / 255;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }

    private static double Parlaklik(double[] c) => 0.2126 * Kanal(c[0]) + 0.7152 * Kanal(c[1]) + 0.0722 * Kanal(c[2]);

    private static double Oran(double[] x, double[] y)
    {
        var a = Parlaklik(x);
        var b = Parlaklik(y);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static string Hex(double[] c) => "#" + ((int)Math.Round(c[0])).ToString("X2") + ((int)Math.Round(c[1])).ToString("X2") + ((int)Math.Round(c[2])).ToString("X2");

    private static string Hex(Color c) => "#" + (c.A < 255 ? c.A.ToString("X2") : "") + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");

    private static string Kisalt(string s)
    {
        var t = s.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
        return t.Length > 40 ? t.Substring(0, 40) + "…" : t;
    }

    private static string Yol(Visual d)
    {
        var parcalar = new List<string>();
        for (var n = d; n != null && parcalar.Count < 4; n = n.GetVisualParent())
        {
            var ad = n is Control c && !string.IsNullOrEmpty(c.Name) ? "#" + c.Name : "";
            parcalar.Insert(0, n.GetType().Name + ad);
        }
        var sahip = d.GetVisualAncestors().OfType<TemplatedControl>().FirstOrDefault(c => !string.IsNullOrEmpty(c.Name));
        return string.Join(" > ", parcalar) + (sahip != null ? " @" + sahip.GetType().Name + "#" + sahip.Name : "");
    }
}
