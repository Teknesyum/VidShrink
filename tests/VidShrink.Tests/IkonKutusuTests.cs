using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Media;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Ikonlarin tasarim kutusu. Her yol 24x24 kutuya sabitlenmis olmali (bastaki
/// <c>F1 M 0,0 M 24,24</c>), murekkebi 2 birimlik kenar payinin icinde kalmali ve
/// hem yatay hem dikey olarak kutunun ortasinda durmali. Olcunun kaynagi
/// <c>docs/arastirma/ikon-estetigi.md</c>: kaydik merkezler orada tek tek sayildi.
///
/// <para>Takim Fluent UI System Icons'in 24 px Filled surumune gecince dil kalemden
/// dolguya dondu: sabitleyicinin onune <c>F1</c> (NonZero) geldi, cunku Avalonia'nin
/// varsayilani EvenOdd ve delikli her simge (cerceve, halka, mercek) yanlis dolardi.
/// Kenar payi toleransi 1e-3'ten 0.01'e acildi: Fluent'in canli alani tam olarak 2-22,
/// ama Bezier duzlestirmesi 1.996 ile 2.003 arasinda oynuyor; eski slop olcuyu
/// geometriye degil duzlestiriciye bagliyordu. Fluent'in kendi cizimindeki uc kayma
/// <see cref="Istisna"/> tablosunda tek tek pimli.</para>
/// </summary>
public sealed class IkonKutusuTests
{
    internal const string Sabitleyici = "F1 M 0,0 M 24,24 ";

    private const double Margin = 2 - 0.01;
    private const double Center = 12;
    private const double Tolerance = 0.55;

    /// <summary>
    /// Ucgen bir sekil kutu merkezine oturtulunca sola kaymis gorunur: kutlesi
    /// tabanda toplanir. Sektor bu yuzden oynat ucgenini saga kaydiriyor
    /// (Lucide +0.50, Material +1.50). Fluent'inki +1.43 ile Material'in (+1.50)
    /// hemen altinda; yatay merkez olcusu bu ikonda gecerli degil, dikey olcu koşuyor.
    /// </summary>
    private static readonly string[] OptikKaydirilan = ["IconPlay"];

    /// <summary>
    /// Genel kurala girmeyen iki Fluent cizimi. Kural gevsetilmiyor: ikisi de kendi
    /// olculen sinir kutusuyla 0.02 icinde pimleniyor, yani kaydirilan ya da baska bir
    /// dosyadan gelen bir gövde bu ikisinde de kirmizi doner.
    /// <list type="bullet">
    /// <item><c>IconSpeed</c> — gosterge kutlesi merkezin ustunde (cy 11.00).</item>
    /// <item><c>IconCoffee</c> — kulp sagda 2 birimlik kenar payini tasiyor (sag kenar 23.00).</item>
    /// </list>
    /// </summary>
    private static readonly Dictionary<string, (double Left, double Top, double Right, double Bottom)> Istisna = new()
    {
        ["IconSpeed"] = (2.00, 2.00, 22.00, 20.00),
        ["IconCoffee"] = (3.00, 2.00, 23.00, 22.00)
    };

    private const double IstisnaSlop = 0.02;

    private readonly ITestOutputHelper _cikti;

    public IkonKutusuTests(ITestOutputHelper cikti) => _cikti = cikti;

    private static readonly string IconPath =
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "Themes", "Icons.axaml");

    private static readonly Regex Entry = new(
        """<StreamGeometry x:Key="(?<ad>\w+)">(?<yol>[^<]+)</StreamGeometry>""",
        RegexOptions.Compiled);

    internal static IEnumerable<object[]> IkonlarIc() => Ikonlar();

    public static IEnumerable<object[]> Ikonlar() =>
        Entry.Matches(File.ReadAllText(IconPath)).Select(m => new object[] { m.Groups["ad"].Value, m.Groups["yol"].Value });

    [Fact]
    public void ButunIkonlarKutuyaSabitli()
    {
        var kayit = Ikonlar().ToList();
        AppHost.Ensure();
        Assert.NotEmpty(kayit);
        foreach (var satir in kayit)
            Assert.StartsWith(Sabitleyici, (string)satir[1]);
    }

    /// <summary>
    /// Teknesyum bağlantısının simgesi <c>&lt;/&gt;</c>: uygulamanın yüklediği kaynaktan
    /// okunan geometri Fluent'in <c>code</c> çizimidir — iki köşeli ayraç ve aralarındaki
    /// eğik çizgi. Dil kalemden dolguya döndüğü için ölçü <c>FillContains</c> okuyor.
    ///
    /// <para>Ölçülen parmak izi: iki ayracın ucu ve karşılıklı iki kolu dolu, eğik çizginin
    /// ortası dolu, buna karşılık kutunun üst ve alt ortası <b>boş</b>. Dolu bir leke
    /// (ya da atom simgesi gibi çekirdeği olan bir şekil) son iki koşulu geçemez.</para>
    /// </summary>
    [Fact]
    public void TeknesyumSimgesiKoseliAyrac()
    {
        AppHost.Ensure();
        var (dolu, bos, kutu) = AppHost.Run(() =>
        {
            var geo = Avalonia.Application.Current!.TryGetResource("IconCode", null, out var kaynak) ? (Geometry)kaynak! : throw new InvalidOperationException("IconCode yok");
            var doluNoktalar = new[]
            {
                new Avalonia.Point(2.5, 12.5),
                new Avalonia.Point(21.5, 12.5),
                new Avalonia.Point(5.5, 8.5),
                new Avalonia.Point(16.5, 15.5),
                new Avalonia.Point(12.5, 12.5)
            };
            var bosNoktalar = new[] { new Avalonia.Point(12.5, 4.5), new Avalonia.Point(12.5, 19.5) };
            return (doluNoktalar.All(geo.FillContains), bosNoktalar.All(n => !geo.FillContains(n)), geo.Bounds);
        });
        _cikti.WriteLine($"IconCode dolu {dolu} bos {bos} kutu {kutu}");
        Assert.True(dolu, "Ayracların uçları/kolları ya da eğik çizgi dolu değil.");
        Assert.True(bos, "Kutunun üst ve alt ortası dolu; simge ayraç değil leke.");
    }

    [Theory]
    [MemberData(nameof(Ikonlar))]
    public void MurekkepOrtada(string ad, string yol)
    {
        var govde = yol[Sabitleyici.Length..];
        AppHost.Ensure();
        var kutu = AppHost.Run(() => Geometry.Parse(govde).Bounds);
        _cikti.WriteLine($"IKON\t{ad}\tx {kutu.X:0.00}-{kutu.Right:0.00}\ty {kutu.Y:0.00}-{kutu.Bottom:0.00}\tcx {kutu.Center.X:0.00}\tcy {kutu.Center.Y:0.00}");

        if (Istisna.TryGetValue(ad, out var pim))
        {
            Assert.Equal(pim.Left, kutu.X, IstisnaSlop);
            Assert.Equal(pim.Top, kutu.Y, IstisnaSlop);
            Assert.Equal(pim.Right, kutu.Right, IstisnaSlop);
            Assert.Equal(pim.Bottom, kutu.Bottom, IstisnaSlop);
            return;
        }

        Assert.InRange(kutu.X, Margin, 24 - Margin);
        Assert.InRange(kutu.Y, Margin, 24 - Margin);
        Assert.InRange(kutu.Right, Margin, 24 - Margin);
        Assert.InRange(kutu.Bottom, Margin, 24 - Margin);
        if (!OptikKaydirilan.Contains(ad))
            Assert.InRange(kutu.Center.X, Center - Tolerance, Center + Tolerance);
        else
            Assert.InRange(kutu.Center.X, Center + 0.5, Center + 1.5);
        Assert.InRange(kutu.Center.Y, Center - Tolerance, Center + Tolerance);
    }
}
