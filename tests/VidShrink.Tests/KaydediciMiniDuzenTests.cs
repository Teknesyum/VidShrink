using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Path = System.IO.Path;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using VidShrink.App.Localization;
using VidShrink.App.Recorder;
using VidShrink.Ffmpeg;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Kaydedicinin mini şeridinin düzeni başsız ölçülür: taşma, kesik metin, dikey hiza, düğme
/// yükseklik tutarlılığı ve boşluk düzeni. Ölçü pencere gösterilmeden yerleşimden okunur.
/// </summary>
public sealed class KaydediciMiniDuzenTests
{
    private readonly ITestOutputHelper _output;

    public KaydediciMiniDuzenTests(ITestOutputHelper output) => _output = output;

    internal enum Durum { Bos, Kayit, KayitBolum, Duraklatildi, GeriSayim }

    internal sealed record Oge(string Ad, Rect Kutu);

    internal sealed record Olcum(string Dil, Durum Durum, Size Serit, IReadOnlyList<Oge> Ogeler, IReadOnlyList<string> Kusurlar);

    private static string Ad(Control c) => string.IsNullOrEmpty(c.Name) ? c.GetType().Name : c.Name!;

    private static Rect Konum(Visual dugum, Visual ata)
    {
        double x = 0, y = 0;
        for (var d = dugum; d is not null && d != ata; d = d.GetVisualParent())
        {
            x += d.Bounds.X;
            y += d.Bounds.Y;
        }

        return new Rect(x, y, dugum.Bounds.Width, dugum.Bounds.Height);
    }

    private static bool Gorunur(Visual c, Visual ata)
    {
        for (var d = c; d is not null && d != ata; d = d.GetVisualParent())
            if (!d.IsVisible) return false;
        return true;
    }

    private static double Dogal(TextBlock blok, string metin) =>
        new Avalonia.Media.TextFormatting.TextLayout(
            metin,
            new Typeface(blok.FontFamily, blok.FontStyle, blok.FontWeight, blok.FontStretch),
            blok.FontSize,
            Brushes.Black).WidthIncludingTrailingWhitespace;

    internal static RecorderMini Kur(string dil, Durum durum)
    {
        Strings.Use(dil);
        var mini = new RecorderMini { ExcludeFromCapture = false };
        mini.Classes.Add("reduced-motion");
        switch (durum)
        {
            case Durum.Bos: mini.Follow(RecorderState.Stopped, "00:00:00"); break;
            case Durum.Kayit: mini.Follow(RecorderState.Running, "01:23:45"); break;
            case Durum.KayitBolum:
                mini.Follow(RecorderState.Running, "01:23:45");
                mini.ShowChapter(string.Format(CultureInfo.InvariantCulture, Strings.Get("recorder.chapter.marked"), 12));
                break;
            case Durum.Duraklatildi: mini.Follow(RecorderState.Paused, "01:23:45"); break;
            case Durum.GeriSayim: mini.Follow(RecorderState.Stopped, "00:00:00", 3); break;
        }

        mini.Topmost = false;
        mini.Measure(Size.Infinity);
        mini.Arrange(new Rect(mini.DesiredSize));
        var kok = (Layoutable)mini.GetVisualChildren().Single();
        for (var tur = 0; tur < 3; tur++)
        {
            foreach (var dugum in mini.GetVisualDescendants().OfType<Layoutable>()) dugum.InvalidateMeasure();
            kok.InvalidateMeasure();
            kok.Measure(Size.Infinity);
            kok.Arrange(new Rect(kok.DesiredSize));
        }

        return mini;
    }

    internal static Olcum Olc(string dil, Durum durum)
    {
        var mini = Kur(dil, durum);
        try { return Olc(mini, dil, durum); }
        finally { mini.Close(); }
    }

    internal static Olcum Olc(RecorderMini mini, string dil, Durum durum)
    {
        var serit = mini.MiniStrip;
        var kusurlar = new List<string>();
        var izgara = (Panel)serit.Child!;
        var ic = Konum(izgara, serit);
        var orta = serit.Bounds.Height / 2;

        var ogeler = new List<Oge>();
        foreach (var cocuk in izgara.Children.OfType<Control>().Where(c => c.IsVisible))
        {
            if (cocuk is Panel yigin and not Grid)
                foreach (var alt in yigin.Children.OfType<Control>().Where(c => c.IsVisible))
                    ogeler.Add(new Oge(Ad(alt), Konum(alt, serit)));
            else
                ogeler.Add(new Oge(Ad(cocuk), Konum(cocuk, serit)));
        }

        ogeler.Sort((a, b) => a.Kutu.X.CompareTo(b.Kutu.X));

        foreach (var oge in ogeler)
        {
            var k = oge.Kutu;
            if (k.X < ic.X - 0.5 || k.Right > ic.Right + 0.5 || k.Y < ic.Y - 0.5 || k.Bottom > ic.Bottom + 0.5)
                kusurlar.Add($"taşma {oge.Ad}: {k} iç alan {ic}");
            var sapma = k.Y + k.Height / 2 - orta;
            if (Math.Abs(sapma) > 0.5)
                kusurlar.Add($"hiza {oge.Ad}: dikey merkez {sapma:+0.#;-0.#} px");
        }

        for (var i = 1; i < ogeler.Count; i++)
            if (ogeler[i].Kutu.X < ogeler[i - 1].Kutu.Right - 0.5)
                kusurlar.Add($"çakışma {ogeler[i - 1].Ad}/{ogeler[i].Ad}");

        foreach (var blok in serit.GetVisualDescendants().OfType<TextBlock>())
        {
            if (!Gorunur(blok, serit) || string.IsNullOrEmpty(blok.Text) || blok.Bounds.Width <= 0) continue;
            var yer = blok.Bounds.Width - blok.Padding.Left - blok.Padding.Right;
            var gereken = Dogal(blok, blok.Text!);
            if (gereken - yer > 0.5) kusurlar.Add($"kesik {Ad(blok)} [{blok.Text}]: gereken {gereken:0.#}, yer {yer:0.#}");
            var k = Konum(blok, serit);
            if (k.X < -0.5 || k.Right > serit.Bounds.Width + 0.5) kusurlar.Add($"kesik {Ad(blok)} [{blok.Text}]: şerit dışında");
        }

        foreach (var dugme in serit.GetVisualDescendants().OfType<Button>().Where(b => Gorunur(b, serit)))
            foreach (var simge in dugme.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>())
            {
                var k = Konum(simge, dugme);
                if (k.Width <= 0 || k.Height <= 0 ||k.X < -0.5 || k.Y < -0.5 || k.Right > dugme.Bounds.Width + 0.5 || k.Bottom > dugme.Bounds.Height + 0.5)
                    kusurlar.Add($"simge {Ad(dugme)}: {k} düğme {dugme.Bounds.Size}");
                else if (Math.Abs(k.X + k.Width / 2 - dugme.Bounds.Width / 2) > 0.5 || Math.Abs(k.Y + k.Height / 2 - dugme.Bounds.Height / 2) > 0.5)
                    kusurlar.Add($"simge-merkez {Ad(dugme)}: {k} düğme {dugme.Bounds.Size}");
            }

        kusurlar.AddRange(SimgeGorunurlugu(serit));
        return new Olcum(dil, durum, serit.Bounds.Size, ogeler, kusurlar);
    }

    internal static IEnumerable<string> SimgeGorunurlugu(Border serit)
    {
        var w = (int)Math.Ceiling(serit.Bounds.Width);
        var h = (int)Math.Ceiling(serit.Bounds.Height);
        using var hedef = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
        hedef.Render(serit);
        var piksel = new byte[w * h * 4];
        var tutamak = System.Runtime.InteropServices.GCHandle.Alloc(piksel, System.Runtime.InteropServices.GCHandleType.Pinned);
        try { hedef.CopyPixels(new PixelRect(0, 0, w, h), tutamak.AddrOfPinnedObject(), piksel.Length, w * 4); }
        finally { tutamak.Free(); }

        int Renk(int x, int y) => BitConverter.ToInt32(piksel, (y * w + x) * 4);
        static int Fark(int a, int b) => Enumerable.Range(0, 3).Max(i => Math.Abs(((a >> (8 * i)) & 0xFF) - ((b >> (8 * i)) & 0xFF)));

        foreach (var dugme in serit.GetVisualDescendants().OfType<Button>().Where(b => Gorunur(b, serit) && b.Content is Avalonia.Controls.Shapes.Path))
        {
            var k = Konum(dugme, serit);
            var zemin = Renk((int)(k.X + 4), (int)(k.Y + k.Height / 2));
            int toplam = 0, farkli = 0;
            for (var y = (int)(k.Y + 8); y < (int)(k.Bottom - 8); y++)
                for (var x = (int)(k.X + 8); x < (int)(k.Right - 8); x++)
                {
                    toplam++;
                    if (Fark(Renk(x, y), zemin) > 48) farkli++;
                }

            if (toplam == 0 || farkli * 10 < toplam)
                yield return $"simge-görünmüyor {Ad(dugme)}: zeminden ayrışan {farkli}/{toplam} piksel";
        }
    }

    internal static string Yaz(Olcum o)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"{o.Dil} {o.Durum}: şerit {o.Serit.Width:0.#}x{o.Serit.Height:0.#}");
        Oge? onceki = null;
        foreach (var oge in o.Ogeler)
        {
            var bosluk = onceki is null ? "" : $" boşluk {oge.Kutu.X - onceki.Kutu.Right:0.#}";
            var x15 = oge.Kutu.X * 1.5;
            var r15 = oge.Kutu.Right * 1.5;
            var kesirli = Math.Abs(x15 - Math.Round(x15)) > 0.01 || Math.Abs(r15 - Math.Round(r15)) > 0.01 ? " 1.5x-kesirli-kenar" : "";
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {oge.Ad,-12} x={oge.Kutu.X:0.#} y={oge.Kutu.Y:0.#} {oge.Kutu.Width:0.#}x{oge.Kutu.Height:0.#}{bosluk}{kesirli}");
            onceki = oge;
        }

        foreach (var k in o.Kusurlar) sb.AppendLine("  KUSUR " + k);
        return sb.ToString();
    }

    private static void Ciz(RecorderMini mini, string yol, double olcek)
    {
        var s = mini.MiniStrip.Bounds.Size;
        using var hedef = new RenderTargetBitmap(
            new PixelSize((int)Math.Ceiling(s.Width * olcek), (int)Math.Ceiling(s.Height * olcek)),
            new Vector(96 * olcek, 96 * olcek));
        hedef.Render(mini.MiniStrip);
        hedef.Save(yol, PngBitmapEncoderOptions.Default);
    }

    private static bool IzgarayaOturur(double x) => Math.Abs(x * 1.5 - Math.Round(x * 1.5)) < 0.01;

    [Fact]
    public void SeritDuzeniButunDillerdeVeHallerdeTutarli()
    {
        var hatalar = AppHost.Run(() =>
        {
            var h = new List<string>();
            var boy = (double)Application.Current!.FindResource("RecorderButtonSize")!;
            var ic = (double)Application.Current!.FindResource("RecorderStripGap")!;
            var dis = (double)Application.Current!.FindResource("RecorderMiniGroupGap")!;
            var sira = new[] { "BtnToggle", "BtnStop|BtnRegion", "TxtElapsed", "BtnOptions", "BtnExpand" };
            Assert.True(Strings.Languages.Count >= 42, "dil sayısı " + Strings.Languages.Count);

            foreach (var dil in Strings.Languages)
            {
                var hal = new Dictionary<Durum, Olcum>();
                foreach (var durum in Enum.GetValues<Durum>())
                {
                    var o = Olc(dil, durum);
                    hal[durum] = o;
                    var ad = $"{dil} {durum}";
                    h.AddRange(o.Kusurlar.Select(k => $"{ad}: {k}"));

                    foreach (var d in o.Ogeler.Where(x => x.Ad.StartsWith("Btn", StringComparison.Ordinal)))
                    {
                        if (Math.Abs(d.Kutu.Height - boy) > 0.01) h.Add($"{ad}: {d.Ad} yüksekliği {d.Kutu.Height}, beklenen {boy}");
                        if (d.Ad is "BtnToggle" or "BtnStop" &&!(IzgarayaOturur(d.Kutu.X) && IzgarayaOturur(d.Kutu.Right)))
                            h.Add($"{ad}: {d.Ad} kenarı 1.5 ölçekte piksele oturmuyor ({d.Kutu.X}..{d.Kutu.Right})");
                    }

                    for (var i = 1; i < o.Ogeler.Count; i++)
                    {
                        var bosluk = o.Ogeler[i].Kutu.X - o.Ogeler[i - 1].Kutu.Right;
                        if (Math.Abs(bosluk - ic) > 0.5 && Math.Abs(bosluk - dis) > 0.5)
                            h.Add($"{ad}: {o.Ogeler[i - 1].Ad}/{o.Ogeler[i].Ad} boşluğu {bosluk:0.#}, beklenen {ic} ya da {dis}");
                    }

                    var dizilis = sira.Select(s => o.Ogeler.Select((x, i) => s.Split('|').Contains(x.Ad) ? i : -1).DefaultIfEmpty(-1).Max()).ToArray();
                    if (dizilis.Any(i => i < 0) || dizilis.Zip(dizilis.Skip(1)).Any(p => p.First >= p.Second))
                        h.Add($"{ad}: sıra {string.Join(",", o.Ogeler.Select(x => x.Ad))}");
                }

                double X(Durum d, string ad) => hal[d].Ogeler.FirstOrDefault(x => x.Ad == ad)?.Kutu.X ?? double.NaN;
                var oynat = Enum.GetValues<Durum>().Select(d => X(d, "BtnToggle")).Distinct().ToArray();
                if (oynat.Length != 1) h.Add($"{dil}: BtnToggle hal değişince kayıyor ({string.Join(", ", oynat)})");
                var durdur = new[] { Durum.Kayit, Durum.KayitBolum, Durum.Duraklatildi, Durum.GeriSayim }.Select(d => X(d, "BtnStop")).Distinct().ToArray();
                if (durdur.Length != 1) h.Add($"{dil}: BtnStop hal değişince kayıyor ({string.Join(", ", durdur)})");
            }

            return h;
        });

        Assert.True(hatalar.Count == 0, hatalar.Count + " kusur:\n" + string.Join("\n", hatalar.Take(40)));
    }

    [Fact]
    public void OlcumuYaz()
    {
        var klasor = Path.Combine(TipSources.Root, ".calisma", "agent", "kaydedici-mini-duzen", Environment.GetEnvironmentVariable("MINI_ETIKET") ?? "olcum");
        Directory.CreateDirectory(klasor);
        var metin = AppHost.Run(() =>
        {
            var sb = new StringBuilder();
            foreach (var dil in new[] { "tr", "en", "lv" })
                foreach (var durum in Enum.GetValues<Durum>())
                {
                    var mini = Kur(dil, durum);
                    try
                    {
                        sb.Append(Yaz(Olc(mini, dil, durum)));
                        foreach (var olcek in new[] { 1.0, 1.5 })
                            Ciz(mini, Path.Combine(klasor, string.Create(CultureInfo.InvariantCulture, $"{dil}-{durum}-{olcek:0.0}.png")), olcek);
                    }
                    finally { mini.Close(); }
                }

            return sb.ToString();
        });
        File.WriteAllText(Path.Combine(klasor, "olcum.txt"), metin, new UTF8Encoding(false));
        _output.WriteLine(metin);
    }
}
