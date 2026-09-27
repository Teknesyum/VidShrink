using System.Text.Json;
using System.Xml.Linq;
using VidShrink.App;
using VidShrink.App.Themes;
using VidShrink.PaletteGen;

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
        => Directory.GetFiles(Folder, "*.axaml", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();

    /// <summary>
    /// Palet adı dosya adında değil klasör adında: her palet kendi klasöründe
    /// <c>Theme.axaml</c> olarak durur, çünkü UI kılavuzu ham rengi yalnız belirteç
    /// dosyası adına muaf tutuyor.
    /// </summary>
    private static string Name(string file) => Path.GetFileName(Path.GetDirectoryName(file))!;

    private static string Address(string name) => Path.Combine(Folder, name, "Theme.axaml");

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
            files.Select(Name).OrderBy(n => n, StringComparer.Ordinal),
            PaletteCatalog.Names.OrderBy(n => n, StringComparer.Ordinal));

        var reference = Keys(Address(PaletteCatalog.Default));
        Assert.NotEmpty(reference);

        var complaints = new List<string>();

        foreach (var file in files)
        {
            var keys = Keys(file);
            var name = Name(file);

            foreach (var missing in reference.Except(keys, StringComparer.Ordinal))
                complaints.Add($"{name}: '{missing}' eksik.");
            foreach (var extra in keys.Except(reference, StringComparer.Ordinal))
                complaints.Add($"{name}: '{extra}' fazladan.");
        }

        Assert.True(complaints.Count == 0,
            $"{files.Count} palet, {reference.Count} anahtar:\n" + string.Join("\n", complaints));
    }

    private static IReadOnlyList<PaletteSeed> Seeds() => PaletteSeed.Load(Path.Combine(Folder, "seeds.json"));

    /// <summary>
    /// Palet dosyaları elle yazılmadı: kaynak <c>seeds.json</c>, her palet standardın rolleriyle.
    /// Ölçü rol → anahtar eşlemesini üreticiden bağımsız okuyor: dosya elle düzenlenip tohumdan
    /// koparsa ya da eşleme kayarsa burada yakalanır. Çalışırken palet değeri değere boyadığı
    /// için bir palette iki anahtar aynı değeri taşıyamaz; çakışan değer mavi kanalda birer birim
    /// kaydırılıyor (<c>PaletteBuilder.Distinct</c>). Burada okunan rollerde kayan iki yer var:
    /// Teknesyum ve Keskin'de zemin yüzeye eşit, yedi palette (Nord, Gruvbox, GruvboxLight,
    /// Solarized, SolarizedLight, Everforest, Synthwave) uyarı renk-2'ye eşit. Ölçü bu kaymayı en
    /// çok iki birime kadar kabul ediyor.
    /// </summary>
    [Fact]
    public void HerPaletKendiCekirdeginiTasir()
    {
        var seeds = Seeds();

        Assert.Equal(PaletteCatalog.Names, seeds.Select(seed => seed.Name));

        foreach (var seed in seeds)
        {
            var body = File.ReadAllText(Address(seed.Name));

            foreach (var (role, key) in new[]
                     {
                         ("surface", "SurfaceToneColor"), ("renk-1", "NeonBlueColor"),
                         ("renk-2", "NeonPinkColor"), ("renk-3", "NeonPurpleColor"),
                         ("text", "TextBodyColor"), ("renk-2-text", "PinkTextColor"),
                         ("disabled", "TextDisabledColor"), ("warning", "EmberBlazeColor"),
                         ("black", "AppBgColor")
                     })
            {
                var wanted = Convert.ToUInt32(seed.Role(role)![1..], 16);
                var match = System.Text.RegularExpressions.Regex.Match(body,
                    $"<Color x:Key=\"{key}\">#FF([0-9A-F]{{6}})</Color>");
                Assert.True(match.Success, $"{seed.Name}: {key} yok.");
                var carried = Convert.ToUInt32(match.Groups[1].Value, 16);
                Assert.True((carried & 0xFFFF00) == (wanted & 0xFFFF00)
                            && Math.Abs((int)(carried & 0xFF) - (int)(wanted & 0xFF)) <= 2,
                    $"{seed.Name}: {role} #{wanted:X6}, {key} #{carried:X6} taşıyor.");
            }
        }
    }

    /// <summary>
    /// Palet dosyaları üreticinin çıktısıyla aynı: tohumdan yeniden üretilen metin diskteki
    /// dosyaya eşit. Satır sonu karşılaştırmaya girmez, git onu makineye göre çeviriyor.
    /// Dosya elle düzenlenirse ya da tohum değişip araç koşulmazsa kırmızı yanar.
    /// </summary>
    [Fact]
    public void PaletDosyalariTohumdanYenidenUretilir()
    {
        var seeds = Seeds();
        Assert.Equal(36, seeds.Count);

        var drift = seeds
            .Where(seed => File.ReadAllText(Address(seed.Name)).Replace("\r\n", "\n", StringComparison.Ordinal)
                           != PaletteBuilder.Build(seed))
            .Select(seed => seed.Name)
            .ToList();

        Assert.True(drift.Count == 0,
            "Tohumdan kopan palet: " + string.Join(", ", drift)
            + ". Komut: dotnet run --project tools/VidShrink.PaletteGen");
    }

    /// <summary>
    /// 36 paletin kaynağı: 26'sı projenin, 9'u standardın tema kalıpları, biri standardın kendi
    /// teması. Hiçbiri silinmedi; kaynak kayarsa ya da bir palet düşerse sayım kırmızı yanar.
    /// </summary>
    [Fact]
    public void ProjeninYirmiAltiPaletiVeStandardinOnuBirArada()
    {
        var bySource = Seeds().GroupBy(seed => seed.Source.StartsWith("teknesyum-ui/temalar/", StringComparison.Ordinal)
                ? "temalar"
                : seed.Source)
            .ToDictionary(group => group.Key, group => group.Select(seed => seed.Name).ToArray());

        Assert.Equal(26, bySource[PaletteSeed.ProjectSource].Length);
        Assert.Equal(new[] { "Gece", "Grafit", "Kadife", "Kor", "Buz", "Kagit", "Kar", "Keskin", "Kirik" },
            bySource["temalar"]);
        Assert.Equal(new[] { PaletteCatalog.Default }, bySource[PaletteSeed.OwnSource]);
        Assert.Equal(3, bySource.Count);
    }

    /// <summary>
    /// Görünen ad tohumdaki <c>baslik</c>tan gelir: klasör adı ASCII, ekranda Türkçe harf
    /// korunur (<c>Kagit</c> → <c>Sıcak Kâğıt</c>). Başlığı olmayan palette ad büyük harften
    /// bölünür.
    /// </summary>
    [Fact]
    public void GorunenAdTohumdakiBasliktanGelir()
    {
        foreach (var seed in Seeds())
        {
            var wanted = seed.Title ?? string.Concat(seed.Name.Select((letter, at) =>
                at > 0 && char.IsUpper(letter) && !char.IsUpper(seed.Name[at - 1]) ? " " + letter : letter.ToString()));
            Assert.Equal(wanted, PaletteCatalog.Label(seed.Name));
        }

        Assert.Equal("Sıcak Kâğıt", PaletteCatalog.Label("Kagit"));
        Assert.Equal("Kırık Beyaz", PaletteCatalog.Label("Kirik"));
        Assert.Equal("Tokyo Night", PaletteCatalog.Label("TokyoNight"));
    }

    /// <summary>
    /// Yeni varsayılan Teknesyum. İşaretsiz dosyadaki <c>Neon</c> eski varsayılandır ve bir kez
    /// Teknesyum'a göçer; işaretle yazılmış Neon kullanıcının seçimidir ve açılır. Ayarı boş
    /// olan, ya da ayar dosyası hiç olmayan, Teknesyum'la açılır. <c>App.axaml</c>'ın açılış
    /// paleti de varsayılanla aynı; ayrışırsa <c>Use</c> ilk seçimde hiç boyamaz.
    /// </summary>
    [Fact]
    public void KayitliSecimKorunurBosAyarVarsayilanaDuser()
    {
        Assert.Equal("Teknesyum", PaletteCatalog.Default);
        Assert.Contains($"Themes/Palette/{PaletteCatalog.Default}/Theme.axaml",
            File.ReadAllText(ThemeSources.AppPath), StringComparison.Ordinal);

        var folder = Path.Combine(TestPaths.OutputRoot, "tema", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            var legacy = Path.Combine(folder, "eski-neon.json");
            File.WriteAllText(legacy, "{\"theme\":\"Neon\"}");
            var saved = Path.Combine(folder, "neon.json");
            File.WriteAllText(saved, "{\"theme\":\"Neon\",\"" + AppSettings.ThemeMigrationMarker + "\":true}");
            var empty = Path.Combine(folder, "bos.json");
            File.WriteAllText(empty, "{}");

            var (fromLegacy, fromSaved, fromEmpty, fromMissing) = AppHost.Run(() =>
            {
                try
                {
                    return (
                        PaletteCatalog.Use(AppSettings.Load(legacy).Theme),
                        PaletteCatalog.Use(AppSettings.Load(saved).Theme),
                        PaletteCatalog.Use(AppSettings.Load(empty).Theme),
                        PaletteCatalog.Use(AppSettings.Load(Path.Combine(folder, "yok.json")).Theme));
                }
                finally { PaletteCatalog.Use(PaletteCatalog.Default); }
            });

            Assert.Equal("Teknesyum", fromLegacy);
            Assert.Equal("Neon", fromSaved);
            Assert.Equal("Teknesyum", fromEmpty);
            Assert.Equal("Teknesyum", fromMissing);
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch (IOException) { }
        }
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
            var name = Name(file);

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
