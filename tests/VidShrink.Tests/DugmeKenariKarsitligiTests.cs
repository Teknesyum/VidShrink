using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using VidShrink.App.Themes;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Dugmenin dinlenme kenari zeminden 3:1 ayrilir (WCAG 1.4.11). 6 Ekim 2026'ya kadar kenarli
/// dugmelerin ortak tabani (<c>GhostButton</c>) kenarini <c>HeaderRestBorder</c> ile ciziyordu:
/// 36 paletin 36'sinda 1,24-1,85. Kenar <c>NeonBlueBorderStrong</c>'a alindi; %50 alfayla 13
/// palet esigin altinda kaliyordu. 10 Ekim 2026'da o 13 palette yalniz bu fircanin alfasi, uc
/// zeminde esigi tutan en kucuk degere cikti (<c>PaletteBuilder.BorderStrongAlpha</c>); renk
/// degismedi. Artik muaf palet yok. Giris kutusunun ve kaydiricinin bos izinin kenari da ayni
/// fircadan cizilir. Olcu sayi tasimaz: temalari, kenar fircasini ve kalinligini tema
/// dosyalarindan, rengi palet dosyasindan okur, yari saydam kenari zemine harmanlar.
/// Sayilar <c>docs/olcumler/dugme-kenari-karsitlik.md</c>'de.
/// </summary>
public sealed class DugmeKenariKarsitligiTests
{
    private const double Esik = 3.0;

    private static readonly string[] TemaDosyalari = ["Controls.axaml", "Playback.axaml", "Editor.axaml"];
    private static readonly string[] DugmeTurleri = ["Button", "ToggleButton"];
    private static readonly string[] Zeminler = ["AppBg", "Surface", "PanelSurface"];

    private const string GucluKenar = "NeonBlueBorderStrong";
    private const string GirisKutusu = "{x:Type TextBox}";
    private const string BosIz = "SliderEmptyTrack";

    private static readonly Regex Basvuru = new(@"^\{(?:StaticResource|DynamicResource)\s+(\w+)\}$", RegexOptions.Compiled);
    private static readonly Regex GirdiSecici = new(@"^\^(:pointerover|:focus|\.ustunde|\.odak) /template/ Border#\w+$", RegexOptions.Compiled);
    private static readonly Regex DurumSecici = new(@"^\^(:pointerover|:pressed|:checked|\.listening) /template/ Border#\w+$", RegexOptions.Compiled);
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly ConcurrentDictionary<string, Dictionary<string, XElement>> Sozlukler = new();

    private readonly ITestOutputHelper _cikti;

    public DugmeKenariKarsitligiTests(ITestOutputHelper cikti) => _cikti = cikti;

    public static IEnumerable<object[]> Paletler() => PaletKarsitligiTests.Paletler();

    private sealed record Tema(string Ad, string Tur, string? Taban, XElement Oge);

    private sealed record Kenar(string Tema, string Tur, string Firca);

    private readonly record struct Rgb(double R, double G, double B)
    {
        internal string Onalti => string.Create(CultureInfo.InvariantCulture,
            $"#FF{(int)Math.Round(R):X2}{(int)Math.Round(G):X2}{(int)Math.Round(B):X2}");
    }

    private static string? Anahtar(string? deger) =>
        deger is not null && Basvuru.Match(deger.Trim()) is { Success: true } m ? m.Groups[1].Value : null;

    private static Dictionary<string, Tema> Temalar()
    {
        var hepsi = new Dictionary<string, Tema>(StringComparer.Ordinal);
        foreach (var dosya in TemaDosyalari)
        {
            var yol = Path.Combine(TipSources.Root, "src", "VidShrink.App", "Themes", dosya);
            foreach (var oge in XDocument.Load(yol).Descendants().Where(e => e.Name.LocalName == "ControlTheme"))
            {
                if ((string?)oge.Attribute(X + "Key") is not { } ad) continue;
                hepsi.Add(ad, new Tema(ad, (string?)oge.Attribute("TargetType") ?? string.Empty,
                    Anahtar((string?)oge.Attribute("BasedOn")), oge));
            }
        }
        return hepsi;
    }

    private static IEnumerable<Tema> Zincir(Dictionary<string, Tema> temalar, Tema tema)
    {
        for (Tema? t = tema; t is not null; t = t.Taban is { } taban ? temalar[taban] : null)
            yield return t;
    }

    private static string? Ayar(Dictionary<string, Tema> temalar, Tema tema, string ozellik) =>
        Zincir(temalar, tema)
            .Select(t => t.Oge.Elements()
                .Where(e => e.Name.LocalName == "Setter" && (string?)e.Attribute("Property") == ozellik)
                .Select(e => (string?)e.Attribute("Value"))
                .FirstOrDefault())
            .FirstOrDefault(deger => deger is not null);

    private static IEnumerable<(string Durum, string Firca)> DurumKenarlari(Dictionary<string, Tema> temalar, Tema tema)
    {
        var gorulen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in Zincir(temalar, tema))
            foreach (var stil in t.Oge.Elements().Where(e => e.Name.LocalName == "Style"))
            {
                var secici = DurumSecici.Match((string?)stil.Attribute("Selector") ?? string.Empty);
                if (!secici.Success) continue;
                var firca = stil.Elements()
                    .Where(e => e.Name.LocalName == "Setter" && (string?)e.Attribute("Property") == "BorderBrush")
                    .Select(e => Anahtar((string?)e.Attribute("Value")))
                    .FirstOrDefault(a => a is not null);
                if (firca is not null && gorulen.Add(secici.Groups[1].Value))
                    yield return (secici.Groups[1].Value, firca);
            }
    }

    private static bool KalinlikVar(string? deger)
    {
        if (deger is null) return false;
        var metin = Anahtar(deger) is { } anahtar ? ThemeSources.Token(anahtar) : deger;
        return metin.Split(',', ' ').Where(p => p.Length > 0)
            .Any(p => double.Parse(p, CultureInfo.InvariantCulture) > 0);
    }

    private static (List<Kenar> Kenarli, List<string> Kenarsiz) Dugmeler()
    {
        var temalar = Temalar();
        var kenarli = new List<Kenar>();
        var kenarsiz = new List<string>();
        foreach (var tema in temalar.Values.Where(t => DugmeTurleri.Contains(t.Tur)).OrderBy(t => t.Ad, StringComparer.Ordinal))
        {
            var firca = Anahtar(Ayar(temalar, tema, "BorderBrush"));
            if (firca is not null && KalinlikVar(Ayar(temalar, tema, "BorderThickness")))
                kenarli.Add(new Kenar(tema.Ad, tema.Tur, firca));
            else
                kenarsiz.Add(tema.Ad);
        }
        return (kenarli, kenarsiz);
    }

    private static Dictionary<string, XElement> Sozluk(string palet) => Sozlukler.GetOrAdd(palet, ad =>
    {
        var sozluk = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var oge in ThemeSources.GeneratedResources().Concat(ThemeSources.Resources(ad)))
            if ((string?)oge.Attribute(X + "Key") is { } anahtar)
                sozluk[anahtar] = oge;
        return sozluk;
    });

    private static XElement Coz(string palet, string anahtar)
    {
        Assert.True(Sozluk(palet).TryGetValue(anahtar, out var oge), $"{palet}: {anahtar} belirteci yok.");
        return oge!.Name.LocalName == "StaticResource" && (string?)oge.Attribute("ResourceKey") is { } hedef
            ? Coz(palet, hedef)
            : oge;
    }

    private static string Metin(string palet, string deger) =>
        Anahtar(deger) is { } anahtar ? Coz(palet, anahtar).Value.Trim() : deger.Trim();

    private static (string Argb, double Saydamlik) Firca(string palet, string firca)
    {
        var oge = Coz(palet, firca);
        Assert.True(oge.Name.LocalName == "SolidColorBrush", $"{firca} duz renk fircasi degil: {oge.Name.LocalName}.");
        var renk = Metin(palet, (string)oge.Attribute("Color")!);
        Assert.True(Regex.IsMatch(renk, "^#[0-9A-Fa-f]{8}$"), $"{palet}: {firca} rengi #AARRGGBB degil: {renk}.");
        var saydamlik = oge.Attribute("Opacity") is { } o
            ? double.Parse(Metin(palet, o.Value), CultureInfo.InvariantCulture)
            : 1.0;
        return (renk, saydamlik);
    }

    private static Rgb Harmanla(string argb, double saydamlik, Rgb alt)
    {
        var alfa = Convert.ToInt32(argb.Substring(1, 2), 16) / 255.0 * saydamlik;
        var r = Convert.ToInt32(argb.Substring(3, 2), 16);
        var g = Convert.ToInt32(argb.Substring(5, 2), 16);
        var b = Convert.ToInt32(argb.Substring(7, 2), 16);
        return new Rgb(r * alfa + alt.R * (1 - alfa), g * alfa + alt.G * (1 - alfa), b * alfa + alt.B * (1 - alfa));
    }

    private static Rgb Zemin(string palet, string zemin)
    {
        var (taban, _) = Firca(palet, "AppBg");
        var opak = Harmanla(taban, 1.0, default);
        if (zemin == "AppBg") return opak;
        var (renk, saydamlik) = Firca(palet, zemin);
        return Harmanla(renk, saydamlik, opak);
    }

    private static Rgb Cizilen(string palet, string firca, Rgb zemin)
    {
        var (renk, saydamlik) = Firca(palet, firca);
        return Harmanla(renk, saydamlik, zemin);
    }

    private static double Oran(string palet, string firca, string zemin)
    {
        var alt = Zemin(palet, zemin);
        return PaletKarsitligiTests.Karsitlik(Cizilen(palet, firca, alt).Onalti, alt.Onalti);
    }

    private static double OranAlfayla(string palet, string firca, int alfa, string zemin)
    {
        var alt = Zemin(palet, zemin);
        var (renk, saydamlik) = Firca(palet, firca);
        var cizilen = Harmanla("#" + alfa.ToString("X2", CultureInfo.InvariantCulture) + renk[3..], saydamlik, alt);
        return PaletKarsitligiTests.Karsitlik(cizilen.Onalti, alt.Onalti);
    }

    private static (string Dinlenme, List<(string Durum, string Firca)> Haller) GirdiKenari(string tema)
    {
        var oge = Temalar()[tema].Oge;
        var dinlenme = oge.Elements()
            .Where(e => e.Name.LocalName == "Setter" && (string?)e.Attribute("Property") == "BorderBrush")
            .Select(e => Anahtar((string?)e.Attribute("Value")))
            .FirstOrDefault(a => a is not null)
            ?? oge.Descendants()
                .Where(e => e.Name.LocalName == "Border" && e.Attribute("BorderBrush") is not null)
                .Select(e => Anahtar((string?)e.Attribute("BorderBrush")))
                .FirstOrDefault(a => a is not null);
        Assert.True(dinlenme is not null, $"{tema}: dinlenme kenari bulunamadi.");

        var haller = new List<(string, string)>();
        foreach (var stil in oge.Elements().Where(e => e.Name.LocalName == "Style"))
        {
            var secici = GirdiSecici.Match((string?)stil.Attribute("Selector") ?? string.Empty);
            if (!secici.Success) continue;
            var firca = stil.Elements()
                .Where(e => e.Name.LocalName == "Setter" && (string?)e.Attribute("Property") == "BorderBrush")
                .Select(e => Anahtar((string?)e.Attribute("Value")))
                .FirstOrDefault(a => a is not null);
            if (firca is not null) haller.Add((secici.Groups[1].Value, firca));
        }
        return (dinlenme!, haller);
    }

    private static string Yaz(double oran) => oran.ToString("0.00", CultureInfo.InvariantCulture);

    private static double EnDusuk(string palet, IEnumerable<string> fircalar) =>
        fircalar.SelectMany(f => Zeminler.Select(z => Oran(palet, f, z))).Min();

    /// <summary>
    /// Kenarli her dugme temasinin dinlenme kenari uc zeminde (pencere, yuzey, panel) 3:1'i gecer.
    /// Muaf palet yok.
    /// </summary>
    [Theory]
    [MemberData(nameof(Paletler))]
    public void DinlenmeKenariZemindenAyriliyor(string palet)
    {
        var (kenarli, _) = Dugmeler();
        Assert.NotEmpty(kenarli);

        var dusuk = new List<string>();
        foreach (var grup in kenarli.GroupBy(k => k.Firca).OrderBy(g => g.Key, StringComparer.Ordinal))
            foreach (var zemin in Zeminler)
            {
                var oran = Oran(palet, grup.Key, zemin);
                _cikti.WriteLine($"KENAR\t{palet}\t{grup.Key}\t{zemin}\t{Yaz(oran)}\t{string.Join(",", grup.Select(k => k.Tema))}");
                if (oran < Esik) dusuk.Add($"{grup.Key}/{zemin} {Yaz(oran)}");
            }

        Assert.True(dusuk.Count == 0, $"{palet}: dugme kenari {Esik:0.0}:1 altinda — " + string.Join(", ", dusuk));
    }

    /// <summary>
    /// Katalogdaki her palet olculur ve hicbiri esigin altinda kalmaz: palet klasorleri katalogla
    /// ayni kumedir, en dusuk oran palet basina dokulur.
    /// </summary>
    [Fact]
    public void KatalogdakiHerPaletEsigiGeciyor()
    {
        var fircalar = Dugmeler().Kenarli.Select(k => k.Firca).Distinct().ToArray();
        Assert.Contains(GucluKenar, fircalar);

        var olculen = Paletler().Select(o => (string)o[0]).ToArray();
        Assert.Equal(PaletteCatalog.Names.OrderBy(ad => ad, StringComparer.Ordinal), olculen.OrderBy(ad => ad, StringComparer.Ordinal));

        var altta = new List<string>();
        foreach (var palet in olculen)
        {
            var oran = EnDusuk(palet, fircalar);
            _cikti.WriteLine($"ENDUSUK\t{palet}\t{oran.ToString("0.000", CultureInfo.InvariantCulture)}");
            if (oran < Esik) altta.Add($"{palet}:{oran.ToString("0.000", CultureInfo.InvariantCulture)}");
        }

        Assert.True(altta.Count == 0, "3:1 altinda: " + string.Join(", ", altta));
    }

    /// <summary>
    /// Guclu kenarin alfasi standardin %50'sinin altina inmez ve gereginden fazla da artmaz: alfasi
    /// yukselen palette bir alt deger en kotu zeminde esigin altindadir, yukselmeyen palet %50'de
    /// esigi zaten tutar. Uretecin panel saydamligi tema belirteciyle aynidir.
    /// </summary>
    [Fact]
    public void GucluKenarAlfasiEsigiTutanEnKucukDeger()
    {
        Assert.Equal(VidShrink.PaletteGen.PaletteBuilder.PanelSurfaceOpacity,
            double.Parse(ThemeSources.Token("PanelSurfaceOpacity"), CultureInfo.InvariantCulture));

        var taban = VidShrink.PaletteGen.PaletteBuilder.BorderStrongBaseAlpha;
        var yukselen = 0;
        var yerinde = 0;
        foreach (var palet in Paletler().Select(o => (string)o[0]))
        {
            var alfa = Convert.ToInt32(Firca(palet, GucluKenar).Argb.Substring(1, 2), 16);
            double EnKotu(int a) => Zeminler.Min(z => OranAlfayla(palet, GucluKenar, a, z));
            _cikti.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"ALFA\t{palet}\t{taban:X2}\t{EnKotu(taban):0.000}\t{alfa:X2}\t{EnKotu(alfa):0.000}"));

            Assert.True(alfa >= taban, $"{palet}: guclu kenar alfasi {alfa:X2}, taban {taban:X2}.");
            Assert.True(EnKotu(alfa) >= Esik, $"{palet}: alfa {alfa:X2} ile en kotu oran {EnKotu(alfa):0.000}.");
            if (alfa == taban) { yerinde++; continue; }

            yukselen++;
            Assert.True(EnKotu(alfa - 1) < Esik,
                $"{palet}: alfa {alfa:X2} gereginden buyuk, {alfa - 1:X2} de {EnKotu(alfa - 1):0.000} veriyor.");
        }

        Assert.True(yukselen > 0, "alfasi yukselen palet yok: olcu en kucuk degeri sinamadi.");
        Assert.True(yerinde > 0, "her paletin alfasi yukselmis: taban degeri sinanmadi.");
    }

    /// <summary>
    /// Giris kutusunun ve kaydiricinin bos izinin dinlenme kenari da uc zeminde 3:1'i gecer;
    /// uzerinde ve odak kenari zeminden 3:1 ayrilir ve dinlenme kenariyla ayni renge cizilmez.
    /// Ince kenar (<c>NeonBlueBorder</c>) ayni hesapta esigin altinda okunur (olumsuz kontrol).
    /// </summary>
    [Theory]
    [MemberData(nameof(Paletler))]
    public void GirdiKenariZemindenAyriliyor(string palet)
    {
        var kusur = new List<string>();
        foreach (var tema in new[] { GirisKutusu, BosIz })
        {
            var (dinlenme, haller) = GirdiKenari(tema);
            Assert.NotEmpty(haller);
            foreach (var zemin in Zeminler)
            {
                var alt = Zemin(palet, zemin);
                var oran = Oran(palet, dinlenme, zemin);
                _cikti.WriteLine($"GIRDI\t{palet}\t{tema}\t{dinlenme}\t{zemin}\t{Yaz(oran)}\t{Yaz(Oran(palet, "NeonBlueBorder", zemin))}");
                if (oran < Esik) kusur.Add($"{tema} {dinlenme}/{zemin} {Yaz(oran)}");

                foreach (var (durum, firca) in haller)
                {
                    var hal = Cizilen(palet, firca, alt).Onalti;
                    var zemine = PaletKarsitligiTests.Karsitlik(hal, alt.Onalti);
                    var dinlenmeye = PaletKarsitligiTests.Karsitlik(hal, Cizilen(palet, dinlenme, alt).Onalti);
                    _cikti.WriteLine($"GIRDIHAL\t{palet}\t{tema}\t{durum}\t{firca}\t{zemin}\t{Yaz(zemine)}\t{Yaz(dinlenmeye)}");
                    if (zemine < Esik) kusur.Add($"{tema}{durum} {firca}/{zemin} zemine {Yaz(zemine)}");
                    if (hal == Cizilen(palet, dinlenme, alt).Onalti) kusur.Add($"{tema}{durum} {firca}/{zemin} dinlenme kenariyla ayni");
                }
            }
        }

        Assert.True(Zeminler.All(z => Oran(palet, "NeonBlueBorder", z) < Esik), $"{palet}: ince kenar esigi tutuyor, olumsuz kontrol kor.");
        Assert.True(kusur.Count == 0, $"{palet}: " + string.Join("; ", kusur));
    }

    /// <summary>
    /// Uzerinde, basili, isaretli ve dinleme hallerinin kenari hem zeminden 3:1 ayrilir hem de
    /// dinlenme kenariyla ayni renge cizilmez. Dinlenme kenari guclenince isaretli hal ayni
    /// fircaya dusmustu; ayrim dolguya kalmasin diye o haller opak vurguya alindi.
    /// </summary>
    [Theory]
    [MemberData(nameof(Paletler))]
    public void EtkilesimKenariDinlenmedenVeZemindenAyriliyor(string palet)
    {
        var temalar = Temalar();
        var (kenarli, _) = Dugmeler();
        var sayilan = 0;
        var kusur = new List<string>();

        foreach (var kenar in kenarli)
            foreach (var (durum, firca) in DurumKenarlari(temalar, temalar[kenar.Tema]))
                foreach (var zemin in Zeminler)
                {
                    sayilan++;
                    var alt = Zemin(palet, zemin);
                    var dinlenme = Cizilen(palet, kenar.Firca, alt).Onalti;
                    var hal = Cizilen(palet, firca, alt).Onalti;
                    var zemine = PaletKarsitligiTests.Karsitlik(hal, alt.Onalti);
                    var dinlenmeye = PaletKarsitligiTests.Karsitlik(hal, dinlenme);
                    _cikti.WriteLine($"HAL\t{palet}\t{kenar.Tema}\t{durum}\t{firca}\t{zemin}\t{Yaz(zemine)}\t{Yaz(dinlenmeye)}");
                    if (zemine < Esik) kusur.Add($"{kenar.Tema}{durum} {firca}/{zemin} zemine {Yaz(zemine)}");
                    if (hal == dinlenme) kusur.Add($"{kenar.Tema}{durum} {firca}/{zemin} dinlenme kenariyla ayni");
                }

        Assert.True(sayilan > 0, "hicbir etkilesim hali bulunamadi.");
        Assert.True(kusur.Count == 0, $"{palet}: " + string.Join("; ", kusur));
    }

    /// <summary>
    /// Olcunun kor olmadiginin pimi. Kenarsiz temalar kenarli sayilmaz, kenarli olan sayilir;
    /// ayni hesap eski dinlenme fircasini (<c>HeaderRestBorder</c>) esigin altinda, opak vurguyu
    /// esigin ustunde okur; harman bilinen degeri verir (%50 beyaz, siyah zeminde 5,32:1).
    /// </summary>
    [Fact]
    public void OlcuKenarsiziAyirirVeDusukOraniGorur()
    {
        var (kenarli, kenarsiz) = Dugmeler();
        Assert.Contains(kenarli, k => k.Tema == "GhostButton");
        Assert.Contains(kenarli, k => k.Tur == "ToggleButton");
        Assert.Contains("HeaderButton", kenarsiz);
        Assert.Contains("PanelHeaderToggle", kenarsiz);
        Assert.Contains("LinkButton", kenarsiz);

        var siyah = new Rgb(0, 0, 0);
        Assert.Equal(5.32, PaletKarsitligiTests.Karsitlik(Harmanla("#80FFFFFF", 1.0, siyah).Onalti, siyah.Onalti), 2);
        Assert.Equal(1.0, PaletKarsitligiTests.Karsitlik(Harmanla("#00FFFFFF", 1.0, siyah).Onalti, siyah.Onalti), 2);
        Assert.Equal(Harmanla("#80FFFFFF", 1.0, siyah).Onalti, Harmanla("#FFFFFFFF", 128 / 255.0, siyah).Onalti);

        var dusukGorulen = 0;
        foreach (var palet in Paletler().Select(o => (string)o[0]))
            foreach (var zemin in Zeminler)
            {
                var eski = Oran(palet, "HeaderRestBorder", zemin);
                var ince = Oran(palet, "NeonBlueBorder", zemin);
                var yeni = Oran(palet, "NeonBlueBorderStrong", zemin);
                var vurgu = Oran(palet, "NeonBlue", zemin);
                var uzerinde = Oran(palet, "NeonPurple", zemin);
                var edilgen = Oran(palet, "TextDisabled", zemin);
                _cikti.WriteLine($"TABLO\t{palet}\t{zemin}\t{Yaz(eski)}\t{Yaz(ince)}\t{Yaz(yeni)}\t{Yaz(vurgu)}\t{Yaz(uzerinde)}\t{Yaz(edilgen)}");
                if (eski < Esik) dusukGorulen++;
                Assert.True(vurgu >= Esik, $"{palet}/{zemin}: opak vurgu {Yaz(vurgu)}.");
            }

        Assert.True(dusukGorulen > 0, "olcu hicbir palette esigin altinda bir kenar gormedi.");
    }

    /// <summary>
    /// Kaynaktan cozulen kenar, calisan uygulamada dugmenin gercekten tasidigi kenardir: her
    /// dugme temasi yururlukteki palette kurulur, stilin verdigi firca ve kalinlik okunur.
    /// </summary>
    [Fact]
    public void KaynaktanCozulenKenarCanliStilleAyni()
    {
        var (kenarli, kenarsiz) = Dugmeler();
        var okunan = AppHost.Run(() =>
        {
            var palet = PaletteCatalog.Current;
            var yigin = new StackPanel();
            var dugmeler = new List<(string Tema, Button Dugme)>();
            foreach (var (tema, tur) in kenarli.Select(k => (k.Tema, k.Tur))
                         .Concat(kenarsiz.Select(k => (k, Temalar()[k].Tur))))
            {
                Button dugme = tur == "ToggleButton" ? new ToggleButton() : new Button();
                dugme.Content = "x";
                dugme.Theme = (ControlTheme)Application.Current!.FindResource(tema)!;
                yigin.Children.Add(dugme);
                dugmeler.Add((tema, dugme));
            }

            var pencere = new Window { Width = 400, Height = 300, Content = new ScrollViewer { Content = yigin } };
            pencere.Show();
            try
            {
                pencere.UpdateLayout();
                return (palet, dugmeler.Select(d =>
                {
                    var firca = d.Dugme.BorderBrush as ISolidColorBrush;
                    var renk = firca is null
                        ? null
                        : string.Create(CultureInfo.InvariantCulture, $"#{firca.Color.A:X2}{firca.Color.R:X2}{firca.Color.G:X2}{firca.Color.B:X2}");
                    var kalinlik = d.Dugme.BorderThickness;
                    return (d.Tema, Renk: renk, Saydamlik: firca?.Opacity ?? 0,
                        Kalin: kalinlik.Left + kalinlik.Top + kalinlik.Right + kalinlik.Bottom > 0);
                }).ToList());
            }
            finally { pencere.Close(); }
        });

        foreach (var kenar in kenarli)
        {
            var canli = okunan.Item2.Single(o => o.Tema == kenar.Tema);
            var (renk, saydamlik) = Firca(okunan.palet, kenar.Firca);
            Assert.True(canli.Kalin, $"{kenar.Tema}: canli stilde kenar kalinligi 0.");
            Assert.True(string.Equals(renk, canli.Renk, StringComparison.OrdinalIgnoreCase),
                $"{kenar.Tema}: kaynak {kenar.Firca} {renk}, canli {canli.Renk}.");
            Assert.Equal(saydamlik, canli.Saydamlik, 3);
        }

        foreach (var tema in kenarsiz)
        {
            var canli = okunan.Item2.Single(o => o.Tema == tema);
            Assert.True(!canli.Kalin || canli.Renk is null || canli.Renk.StartsWith("#00", StringComparison.Ordinal),
                $"{tema}: kenarsiz sayildi ama canli stilde {canli.Renk} kenar ciziyor.");
        }
    }
}
