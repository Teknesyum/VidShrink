using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using VidShrink.App.Themes;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Palet seçiminin <b>çalışırken</b> yürürlüğe girip girmediğini ölçer.
///
/// <para><see cref="PaletteTests.PaletDegisince_AyniAnahtarBaskaRengiVerir"/> yalnız
/// <c>Color</c> anahtarına bakıyordu; o anahtar sözlük değişince gerçekten değişiyor,
/// ama ekranı boyayan şey renk değil fırçadır. Programdaki her başvuru
/// <c>{StaticResource}</c> olduğu için fırçalar bir kez kurulup denetimlerin içinde
/// saklanıyor: sözlüğü değiştirmek yaşayan fırçayı değiştirmiyordu. Buradaki ölçüler
/// fırçanın kendisini okuyor.</para>
///
/// <para>İki ayrı kapsam var ve ikisi de ölçülüyor: <c>Theme.axaml</c>'ın fırçaları
/// <c>Application.Resources</c> altında, <c>Controls.axaml</c>'ın denetim temaları ise
/// <c>Application.Styles</c> altında kendi sözlüğünde duruyor.</para>
/// </summary>
public sealed class PaletteApplyTests
{
    private readonly ITestOutputHelper _cikti;

    public PaletteApplyTests(ITestOutputHelper cikti) => _cikti = cikti;

    private static object? Resource(string key)
    {
        Application.Current!.TryGetResource(key, null, out var value);
        return value;
    }

    /// <summary><c>Theme.axaml</c> kapsamındaki bir fırçanın rengi.</summary>
    private static string BrushColour(string key)
        => Resource(key) is SolidColorBrush brush ? brush.Color.ToString() : $"<fırça değil: {Resource(key)?.GetType().Name ?? "yok"}>";

    /// <summary><c>Controls.axaml</c> kapsamındaki bir denetim temasının fırça rengi.</summary>
    private static string ControlThemeColour(string themeKey, string property)
    {
        if (Resource(themeKey) is not ControlTheme theme) return $"<tema yok: {themeKey}>";

        foreach (var setter in theme.Setters)
        {
            if (setter is not Setter typed || typed.Property?.Name != property) continue;
            return typed.Value is SolidColorBrush brush
                ? brush.Color.ToString()
                : $"<fırça değil: {typed.Value?.GetType().Name ?? "boş"}>";
        }

        return $"<kurucu yok: {themeKey}.{property}>";
    }

    private static IReadOnlyList<(string Etiket, string Deger)> Okuma() => new[]
    {
        ("AppBgColor (anahtar)", Resource("AppBgColor")?.ToString() ?? ""),
        ("NeonBlueColor (anahtar)", Resource("NeonBlueColor")?.ToString() ?? ""),
        ("AppBg (fırça)", BrushColour("AppBg")),
        ("NeonBlue (fırça)", BrushColour("NeonBlue")),
        ("TextBody (fırça)", BrushColour("TextBody")),
        ("Surface (fırça)", BrushColour("Surface")),
        ("Body.Foreground (denetim teması)", ControlThemeColour("Body", "Foreground")),
        ("PinkText (fırça)", BrushColour("PinkText"))
    };

    /// <summary>
    /// Kusurun kendisi: palet değişince ekranı boyayan fırçalar da değişmeli. Negatif
    /// kontrol, ölçünün ilk sürümünde kırmızı yanan şey — önceki ve sonraki değer aynıysa
    /// kullanıcı temayı seçer ve programda hiçbir şey değişmez.
    /// </summary>
    [Fact]
    public void PaletDegisince_YasayanFircalarDaDegisir()
    {
        var (once, sonra, geri) = AppHost.Run(() =>
        {
            var baslangic = PaletteCatalog.Use(PaletteCatalog.Default);
            var a = Okuma();

            var oteki = PaletteCatalog.Names.First(name => name != PaletteCatalog.Default);
            PaletteCatalog.Use(oteki);
            var b = Okuma();

            PaletteCatalog.Use(baslangic);
            return (a, b, Okuma());
        });

        var dokum = new StringBuilder();
        for (var at = 0; at < once.Count; at++)
            dokum.AppendLine($"{once[at].Etiket,-34} önce={once[at].Deger,-12} sonra={sonra[at].Deger,-12} geri={geri[at].Deger}");

        _cikti.WriteLine(dokum.ToString());
        Kanit(dokum.ToString());

        var ayniKalan = once.Where((okuma, at) => okuma.Deger == sonra[at].Deger).Select(okuma => okuma.Etiket).ToList();

        Assert.True(ayniKalan.Count == 0,
            "Palet değişti ama şunlar aynı kaldı — seçim ekrana yansımıyor:\n"
            + string.Join("\n", ayniKalan) + "\n\n" + dokum);

        for (var at = 0; at < once.Count; at++)
            Assert.Equal(once[at].Deger, geri[at].Deger);
    }

    /// <summary>
    /// Zemini açık olan paletler koyu denetim çeşidiyle açılırsa Fluent'in kendi açılır
    /// listeleri ve kaydırma çubukları koyu kalır. Çeşit paletin zemin parlaklığından
    /// hesaplanıyor; elle tutulan bir liste yok.
    /// </summary>
    [Fact]
    public void AcikPaletAcikCesitleAcilir()
    {
        var satirlar = AppHost.Run(() =>
        {
            var okunan = PaletteCatalog.Names
                .Select(name =>
                {
                    PaletteCatalog.Use(name);
                    return (name,
                        variant: Application.Current!.RequestedThemeVariant?.ToString() ?? "",
                        bg: Resource("AppBgColor")?.ToString() ?? "",
                        acik: PaletteCatalog.IsLight(name));
                })
                .ToList();

            PaletteCatalog.Use(PaletteCatalog.Default);
            return okunan;
        });

        var dokum = new StringBuilder();
        foreach (var (name, variant, bg, acik) in satirlar)
            dokum.AppendLine($"{name,-18} {bg,-12} {variant,-6} IsLight={acik}");

        _cikti.WriteLine(dokum.ToString());
        Kanit(dokum.ToString(), "cesit.txt");

        Assert.Contains(satirlar, satir => satir.variant == "Light");
        Assert.Contains(satirlar, satir => satir.variant == "Dark");

        foreach (var (name, variant, _, acik) in satirlar)
            Assert.Equal(acik ? "Light" : "Dark", variant);
    }

    private static void Kanit(string metin, string ad = "fircalar.txt")
    {
        var folder = Path.Combine(TipSources.Root, ".calisma", "tema");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ad), metin);
    }
}
