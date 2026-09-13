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
/// <c>M 0,0 M 24,24</c>), murekkebi 2 birimlik kenar payinin icinde kalmali ve
/// hem yatay hem dikey olarak kutunun ortasinda durmali. Olcunun kaynagi
/// <c>docs/arastirma/ikon-estetigi.md</c>: kaydik merkezler orada tek tek sayildi.
/// </summary>
public sealed class IkonKutusuTests
{
    private const double Margin = 2 - 1e-3;
    private const double Center = 12;
    private const double Tolerance = 0.55;

    /// <summary>
    /// Ucgen bir sekil kutu merkezine oturtulunca sola kaymis gorunur: kutlesi
    /// tabanda toplanir. Sektor bu yuzden oynat ucgenini saga kaydiriyor
    /// (Lucide +0.50, Material +1.50). Bizimki +1.00 ile arada; yatay merkez
    /// olcusu bu ikonda gecerli degil, dikey olcu koşuyor.
    /// </summary>
    private static readonly string[] OptikKaydirilan = ["IconPlay"];

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
            Assert.StartsWith("M 0,0 M 24,24 ", (string)satir[1]);
    }

    [Theory]
    [MemberData(nameof(Ikonlar))]
    public void MurekkepOrtada(string ad, string yol)
    {
        var govde = yol["M 0,0 M 24,24 ".Length..];
        AppHost.Ensure();
        var kutu = AppHost.Run(() => Geometry.Parse(govde).Bounds);
        _cikti.WriteLine($"IKON\t{ad}\tx {kutu.X:0.00}-{kutu.Right:0.00}\ty {kutu.Y:0.00}-{kutu.Bottom:0.00}\tcx {kutu.Center.X:0.00}\tcy {kutu.Center.Y:0.00}");

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
