using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;

using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// K9 ölçüsü: simge takımı Fluent UI System Icons'ın 24 px <b>Filled</b> sürümünden gelir.
///
/// <para><b>Sayı elle yazılmıyor.</b> Sözleşme "26" diyordu, o 13 Eylül ölçümünün sayısı;
/// aradan <c>IconRestore</c> eklendi, kullanılmayan iki geometri düştü. Bu yüzden ölçü sayıyı iki
/// <b>bağımsız</b> kaynaktan okuyor ve karşılaştırıyor: <c>Themes/Icons.axaml</c> ile
/// <c>docs/tasarim/fluent-simge-eslemesi.md</c> eşleme tablosu. Bir geometri silinirse
/// ya da tabloya girmeyen bir anahtar eklenirse iki küme ayrışır ve ölçü kırmızı döner;
/// hiçbir yerde sabit bir liste tekrarlanmıyor.</para>
///
/// <para><b>Kırpılma başsız ekranda ölçülüyor.</b> Her simge %100, %150 ve %200 ölçekte
/// <c>IconSizeSm</c> kutusuna çizilip <see cref="RenderTargetBitmap"/>'e alınıyor, sonra
/// boyanmış piksellerin sınır kutusu okunuyor. Beklenen sınır kutusu geometriden
/// hesaplanıyor (<c>Bounds</c> × kutu/24 × ölçek). Kırpılma tam olarak bu ikisinin
/// ayrışmasıdır: kutunun dışına taşan mürekkep boyanamaz, dolayısıyla boyanmış kutu
/// beklenenden <b>küçük</b> çıkar. Sabit bir sayıyla karşılaştırma yok; ölçü geometrinin
/// kendisinden türüyor, bu yüzden kaydırılan bir gövde de kırmızı verir.</para>
/// </summary>
public sealed class IconsTests
{
    private const double Kutu = 16;
    private const double Slop = 1.5;

    private static readonly double[] Olcekler = [1.0, 1.5, 2.0];

    private static readonly string EslemeYolu =
        System.IO.Path.Combine(TipSources.Root, "docs", "tasarim", "fluent-simge-eslemesi.md");

    private static readonly Regex EslemeSatiri = new(
        @"^\|\s*\d+\s*\|\s*(?<ad>Icon\w+)\s*\|\s*`(?<klasor>[^`]+)`\s*\|\s*`ic_fluent_(?<dosya>\w+)_(?<boy>\d+)_filled\.svg`\s*\|\s*(?<surum>[^|]+?)\s*\|",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private readonly ITestOutputHelper _cikti;

    public IconsTests(ITestOutputHelper cikti) => _cikti = cikti;

    private static List<(string Ad, string Yol)> Takim() =>
        IkonKutusuTests.Ikonlar().Select(o => ((string)o[0], (string)o[1])).ToList();

    private static List<Match> Esleme() => EslemeSatiri.Matches(File.ReadAllText(EslemeYolu)).ToList();

    /// <summary>
    /// Geometri sayısı, eşleme tablosunun satır sayısı kadar; iki kümenin anahtarları da
    /// birebir aynı. Sayı hiçbir yerde sabit yazılı değil, iki kaynaktan okunuyor.
    /// </summary>
    [Fact]
    public void GeometriSayisiEslemeTablosuylaAyni()
    {
        var takim = Takim();
        var esleme = Esleme();

        _cikti.WriteLine($"Icons.axaml {takim.Count} geometri, eşleme tablosu {esleme.Count} satır");

        Assert.NotEmpty(takim);
        Assert.NotEmpty(esleme);
        Assert.Equal(esleme.Count, takim.Count);

        var takimAdlari = takim.Select(t => t.Ad).ToHashSet(StringComparer.Ordinal);
        var eslemeAdlari = esleme.Select(m => m.Groups["ad"].Value).ToHashSet(StringComparer.Ordinal);

        Assert.True(takimAdlari.SetEquals(eslemeAdlari),
            "Tabloda olmayan: " + string.Join(", ", takimAdlari.Except(eslemeAdlari))
            + " | Takımda olmayan: " + string.Join(", ", eslemeAdlari.Except(takimAdlari)));

        Assert.Equal(takim.Count, takimAdlari.Count);
    }

    /// <summary>Tablonun her satırı Filled temanın 24 px çizimini gösteriyor.</summary>
    [Fact]
    public void HerSatirYirmiDortPikselFilled()
    {
        var esleme = Esleme();
        Assert.NotEmpty(esleme);
        foreach (var satir in esleme)
        {
            Assert.Equal("24", satir.Groups["boy"].Value);
            Assert.Contains("Filled", satir.Groups["surum"].Value, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Hiçbir geometri boş değil: sabitleyici çıkarıldıktan sonra gövde ayrıştırılıyor,
    /// sınır kutusunun eni ve boyu pozitif, ve kutunun merkezine en yakın dolu nokta
    /// bulunabiliyor — yani dolgu gerçekten mürekkep taşıyor.
    /// </summary>
    [Theory]
    [MemberData(nameof(IkonKutusuTests.Ikonlar), MemberType = typeof(IkonKutusuTests))]
    public void HicbirGeometriBosDegil(string ad, string yol)
    {
        Assert.StartsWith(IkonKutusuTests.Sabitleyici, yol, StringComparison.Ordinal);
        var govde = yol[IkonKutusuTests.Sabitleyici.Length..].Trim();
        Assert.NotEqual(string.Empty, govde);

        AppHost.Ensure();
        var (kutu, dolu) = AppHost.Run(() =>
        {
            var geo = Geometry.Parse(govde);
            var sayac = 0;
            for (var y = 0; y < 24; y++)
                for (var x = 0; x < 24; x++)
                    if (geo.FillContains(new Point(x + 0.5, y + 0.5))) sayac++;
            return (geo.Bounds, sayac);
        });

        _cikti.WriteLine($"BOS?\t{ad}\tkutu {kutu.Width:0.00}x{kutu.Height:0.00}\tdolu nokta {dolu}");
        Assert.True(kutu.Width > 0, $"{ad}: sınır kutusunun eni sıfır.");
        Assert.True(kutu.Height > 0, $"{ad}: sınır kutusunun boyu sıfır.");
        Assert.True(dolu > 0, $"{ad}: 24x24 ızgarada tek bir dolu nokta yok, geometri boş.");
    }

    /// <summary>
    /// %100 / %150 / %200: boyanan mürekkebin sınır kutusu, geometriden hesaplanan
    /// beklenen kutuyla 1,5 piksel içinde eşleşiyor. Kırpılan bir simgede boyanan kutu
    /// beklenenden küçük kalır.
    /// </summary>
    [Theory]
    [MemberData(nameof(IkonKutusuTests.Ikonlar), MemberType = typeof(IkonKutusuTests))]
    public void UcOlcektenBirindeKirpilmiyor(string ad, string yol)
    {
        var govde = yol[IkonKutusuTests.Sabitleyici.Length..].Trim();
        AppHost.Ensure();

        var satirlar = new List<string>();
        var suclu = new List<string>();

        AppHost.Run(() =>
        {
            var geo = Geometry.Parse(govde);
            var kaynak = geo.Bounds;

            foreach (var olcek in Olcekler)
            {
                var kenar = (int)Math.Round(Kutu * olcek);
                var yol2 = new Avalonia.Controls.Shapes.Path
                {
                    Width = Kutu,
                    Height = Kutu,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Data = Geometry.Parse(yol),
                    Fill = Brushes.White
                };
                var kok = new Border { Width = Kutu, Height = Kutu, Background = Brushes.Black, Child = yol2 };
                kok.Measure(new Size(Kutu, Kutu));
                kok.Arrange(new Rect(0, 0, Kutu, Kutu));
                kok.UpdateLayout();

                using var kare = new RenderTargetBitmap(new PixelSize(kenar, kenar), new Vector(96 * olcek, 96 * olcek));
                kare.Render(kok);

                var (l, t, r, b, sayi) = Boyanan(kare, kenar);
                var oran = Kutu / 24.0 * olcek;
                var bl = kaynak.X * oran;
                var bt = kaynak.Y * oran;
                var br = kaynak.Right * oran;
                var bb = kaynak.Bottom * oran;

                satirlar.Add(string.Format(CultureInfo.InvariantCulture,
                    "OLCEK\t{0}\t%{1:0}\tboyanan {2}..{3} x {4}..{5} ({6} px)\tbeklenen {7:0.00}..{8:0.00} x {9:0.00}..{10:0.00}",
                    ad, olcek * 100, l, r, t, b, sayi, bl, br, bt, bb));

                if (sayi == 0) { suclu.Add($"%{olcek * 100:0}: hiç mürekkep boyanmadı"); continue; }
                if (l > bl + Slop) suclu.Add($"%{olcek * 100:0}: sol kenar kırpıldı ({l} > {bl:0.00})");
                if (t > bt + Slop) suclu.Add($"%{olcek * 100:0}: üst kenar kırpıldı ({t} > {bt:0.00})");
                if (r < br - Slop) suclu.Add($"%{olcek * 100:0}: sağ kenar kırpıldı ({r} < {br:0.00})");
                if (b < bb - Slop) suclu.Add($"%{olcek * 100:0}: alt kenar kırpıldı ({b} < {bb:0.00})");
            }
            return 0;
        });

        foreach (var satir in satirlar) _cikti.WriteLine(satir);
        Assert.True(suclu.Count == 0, ad + ": " + string.Join(" | ", suclu));
    }

    private static (int Sol, int Ust, int Sag, int Alt, int Sayi) Boyanan(RenderTargetBitmap kare, int kenar)
    {
        var tampon = new byte[kenar * kenar * 4];
        var tutamak = GCHandle.Alloc(tampon, GCHandleType.Pinned);
        try { kare.CopyPixels(new PixelRect(0, 0, kenar, kenar), tutamak.AddrOfPinnedObject(), tampon.Length, kenar * 4); }
        finally { tutamak.Free(); }

        int sol = int.MaxValue, ust = int.MaxValue, sag = int.MinValue, alt = int.MinValue, sayi = 0;
        for (var y = 0; y < kenar; y++)
            for (var x = 0; x < kenar; x++)
            {
                var i = (y * kenar + x) * 4;
                if (tampon[i] < 96 && tampon[i + 1] < 96 && tampon[i + 2] < 96) continue;
                sayi++;
                if (x < sol) sol = x;
                if (x > sag) sag = x;
                if (y < ust) ust = y;
                if (y > alt) alt = y;
            }

        return sayi == 0 ? (0, 0, 0, 0, 0) : (sol, ust, sag + 1, alt + 1, sayi);
    }
}
