using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// <c>OnNeon</c> neon dolgunun ustune binen tek yazi rengi: birincil dugme, vurgulu sekme ve
/// isaretli satir onu kullaniyor. 18 Eylul 2026'ya kadar hicbir olcu bu ikilinin okunabilir
/// olup olmadigini sormamisti; 26 paletin 17'si WCAG AA esiginin (4,5:1) altindaydi, en kotusu
/// MaterialOcean'da <b>1,38:1</b>. Bu sinif olcuyu yerine koyuyor.
///
/// <para>Renk uydurulmadi: her palette <c>OnNeonColor</c> yalniz siyah ile beyaz arasinda
/// secildi, hangisi o paletin neon dolgularina daha uzaksa o. Iki palet — CatppuccinLatte ve
/// GruvboxLight — iki secenekle de 4,5'e ulasamiyor; onlarin dolgusu acik, cozum renk degil
/// dolgu degisikligi ister. Kalan borc <see cref="EsiginAltindaKalanlarPimli"/> ile adiyla
/// duruyor, susturulmuyor.</para>
/// </summary>
public sealed class PaletKarsitligiTests
{
    private const double TabanEsik = 3.0;
    private const double AaEsik = 4.5;

    private static readonly string[] Zeminler =
    [
        "NeonBlueColor", "NeonPurpleColor", "NeonPinkColor", "NeonBlueActiveColor"
    ];

    private static readonly string[] AaAltindaKalanlar = ["CatppuccinLatte", "GruvboxLight"];

    private readonly ITestOutputHelper _cikti;

    public PaletKarsitligiTests(ITestOutputHelper cikti) => _cikti = cikti;

    private static string PaletKoku =>
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "Themes", "Palette");

    public static IEnumerable<object[]> Paletler() =>
        Directory.GetDirectories(PaletKoku)
            .Where(d => File.Exists(Path.Combine(d, "Theme.axaml")))
            .Select(d => new object[] { Path.GetFileName(d) })
            .OrderBy(o => (string)o[0], StringComparer.Ordinal);

    private static double Kanal(int bayt)
    {
        var v = bayt / 255.0;
        return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    /// <summary>WCAG 2.1 bagil parlaklik; alfa yok sayiliyor, dolgular tamamen opak.</summary>
    internal static double Parlaklik(string argb)
    {
        var r = Convert.ToInt32(argb.Substring(3, 2), 16);
        var g = Convert.ToInt32(argb.Substring(5, 2), 16);
        var b = Convert.ToInt32(argb.Substring(7, 2), 16);
        return 0.2126 * Kanal(r) + 0.7152 * Kanal(g) + 0.0722 * Kanal(b);
    }

    internal static double Karsitlik(string a, string b)
    {
        var x = Parlaklik(a);
        var y = Parlaklik(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    private static string? Renk(string xaml, string anahtar)
    {
        var m = Regex.Match(xaml, "x:Key=\"" + anahtar + "\"[^>]*>(#[0-9A-Fa-f]{8})<");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static (string On, double EnDusuk, string EnKotuZemin) Olc(string palet)
    {
        var xaml = File.ReadAllText(Path.Combine(PaletKoku, palet, "Theme.axaml"));
        var on = Renk(xaml, "OnNeonColor");
        Assert.True(on is not null, $"{palet}: OnNeonColor yok.");

        var enDusuk = double.MaxValue;
        var enKotu = string.Empty;
        foreach (var anahtar in Zeminler)
        {
            var zemin = Renk(xaml, anahtar);
            if (zemin is null) continue;
            var oran = Karsitlik(on!, zemin);
            if (oran < enDusuk) { enDusuk = oran; enKotu = anahtar; }
        }

        Assert.True(enKotu.Length > 0, $"{palet}: hicbir neon dolgu bulunamadi.");
        return (on!, enDusuk, enKotu);
    }

    /// <summary>
    /// Sert taban: hicbir palette neon dolgu ustundeki yazi 3:1'in altina inmiyor. Bu esik
    /// kalin yazi icin WCAG'in buyuk metin sinirina denk; 4,5 hedefi ayri testte.
    /// </summary>
    [Theory]
    [MemberData(nameof(Paletler))]
    public void NeonDolgununUstundekiYaziTabaniGeciyor(string palet)
    {
        var (on, enDusuk, enKotu) = Olc(palet);
        _cikti.WriteLine($"KARSITLIK\t{palet}\t{on}\t{enDusuk:0.00}\t{enKotu}");
        Assert.True(enDusuk >= TabanEsik,
            $"{palet}: OnNeon {on}, {enKotu} ustunde {enDusuk:0.00}:1 — taban {TabanEsik:0.0}.");
    }

    /// <summary>
    /// AA esigini gecemeyen paletler tek tek yazili. Listeye yeni bir ad dusmesi de, listedeki
    /// bir adin duzelmesi de kirmizi verir — borc ne buyur ne de sessizce kapanir.
    /// </summary>
    [Fact]
    public void EsiginAltindaKalanlarPimli()
    {
        var altta = Paletler()
            .Select(o => (string)o[0])
            .Where(p => Olc(p).EnDusuk < AaEsik)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        _cikti.WriteLine("AA altinda: " + (altta.Length == 0 ? "yok" : string.Join(", ", altta)));
        Assert.Equal(AaAltindaKalanlar.OrderBy(p => p, StringComparer.Ordinal), altta);
    }

    /// <summary>
    /// Olcunun kendi kor olmadiginin pimi: bilinen karsitliklar elle hesaplanip dogrulaniyor.
    /// Beyaz uzerine siyah 21:1, kendi uzerine her renk 1:1, acik mavi neon uzerine beyaz 2,30:1
    /// — ucuncusu olcunun AA esigini gecen bir degeri kazara uretmedigini gosteriyor.
    /// </summary>
    [Fact]
    public void KarsitlikHesabiBilinenDegerleriVeriyor()
    {
        Assert.Equal(21.0, Karsitlik("#FFFFFFFF", "#FF000000"), 2);
        Assert.Equal(1.0, Karsitlik("#FF3B82F6", "#FF3B82F6"), 2);
        Assert.Equal(2.30, Karsitlik("#FFFFFFFF", "#FF82AAFF"), 2);
    }
}
