using System.Xml.Linq;
using VidShrink.App;
using VidShrink.App.Themes;

namespace VidShrink.Tests;

/// <summary>
/// Tema tek bir yerde durur: <c>Themes/Palette/</c>. Bu ölçüler o sözün tutulup
/// tutulmadığına bakıyor — her palet aynı anahtarları taşıyor mu, seçim gerçekten
/// yürürlüğe giriyor mu, ve seçim ayar dosyasında saklanıyor mu.
/// </summary>
public sealed class PaletteTests
{
    private static string Folder =>
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "Themes", "Palette");

    private static IReadOnlyList<string> Files()
        => Directory.GetFiles(Folder, "*.axaml").OrderBy(path => path, StringComparer.Ordinal).ToArray();

    private static SortedSet<string> Keys(string file)
    {
        var ui = "{https://github.com/avaloniaui}";
        var x = "{http://schemas.microsoft.com/winfx/2006/xaml}";

        return new SortedSet<string>(
            XDocument.Load(file).Root!.Elements()
                .Where(element => element.Name == ui + "Color" || element.Name == ui + "BoxShadows")
                .Select(element => element.Attribute(x + "Key")!.Value),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Eksik anahtarlı bir palet açılışta boş bir yüzey demek: fırça anahtarı bulamaz.
    /// Ölçü yirmi paletin de aynı kümeyi taşıdığını söylüyor ve sayıyı ekrana yazıyor.
    /// </summary>
    [Fact]
    public void HerPaletAyniAnahtarKumesiniTasir()
    {
        var files = Files();
        Assert.True(files.Count >= 20, $"Palet sayısı yirmiden az: {files.Count}");

        Assert.Equal(
            files.Select(file => Path.GetFileNameWithoutExtension(file)).OrderBy(n => n, StringComparer.Ordinal),
            PaletteCatalog.Names.OrderBy(n => n, StringComparer.Ordinal));

        var reference = Keys(Path.Combine(Folder, PaletteCatalog.Default + ".axaml"));
        Assert.NotEmpty(reference);

        var complaints = new List<string>();

        foreach (var file in files)
        {
            var keys = Keys(file);
            var name = Path.GetFileNameWithoutExtension(file);

            foreach (var missing in reference.Except(keys, StringComparer.Ordinal))
                complaints.Add($"{name}: '{missing}' eksik.");
            foreach (var extra in keys.Except(reference, StringComparer.Ordinal))
                complaints.Add($"{name}: '{extra}' fazladan.");
        }

        Assert.True(complaints.Count == 0,
            $"{files.Count} palet, {reference.Count} anahtar:\n" + string.Join("\n", complaints));
    }

    /// <summary>
    /// Paletler birbirinin kopyası olmamalı: yirmi seçenek yirmi farklı görünüş demek.
    /// </summary>
    [Fact]
    public void PaletlerBirbirinindenFarkli()
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in Files())
        {
            var body = string.Join("|", XDocument.Load(file).Root!.Elements().Select(e => e.Value.Trim()));
            var name = Path.GetFileNameWithoutExtension(file);

            Assert.False(seen.TryGetValue(body, out var twin), $"{name} ile {twin} aynı renkleri taşıyor.");
            seen[body] = name;
        }
    }

    /// <summary>
    /// Seçim yürürlüğe giriyor mu: aynı anahtar palet değişince başka bir renk vermeli.
    /// Ölçü iki paletin arka plan rengini yan yana koyuyor.
    /// </summary>
    [Fact]
    public void PaletDegisince_AyniAnahtarBaskaRengiVerir()
    {
        var (first, second, back) = AppHost.Run(() =>
        {
            var start = PaletteCatalog.Use(PaletteCatalog.Default);
            var a = Read("NeonBlueColor");

            var other = PaletteCatalog.Names.First(name => name != PaletteCatalog.Default);
            PaletteCatalog.Use(other);
            var b = Read("NeonBlueColor");

            PaletteCatalog.Use(start);
            return (a, b, Read("NeonBlueColor"));
        });

        Assert.NotEqual(first, second);
        Assert.Equal(first, back);
    }

    private static string Read(string key)
    {
        Avalonia.Application.Current!.TryGetResource(key, null, out var value);
        return value?.ToString() ?? "";
    }

    /// <summary>
    /// Seçim ayar dosyasında saklanıyor mu — dil gibi, bir sonraki açılışta hatırlansın.
    /// </summary>
    [Fact]
    public void SecilenTemaAyarDosyasindaSaklanir()
    {
        var other = PaletteCatalog.Names.First(name => name != PaletteCatalog.Default);
        var file = Path.Combine(TestPaths.OutputRoot, "tema", Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        try
        {
            new AppSettings { Theme = other }.Save(file);
            Assert.Equal(other, AppSettings.Load(file).Theme);
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(file)!, recursive: true); } catch (IOException) { }
        }
    }
}
