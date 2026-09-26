using System.Text;
using VidShrink.PaletteGen;

var folder = Path.Combine("src", "VidShrink.App", "Themes", "Palette");
var rest = args.ToList();
if (rest.Count > 1 && rest[0] == "--folder")
{
    folder = rest[1];
    rest.RemoveRange(0, 2);
}

var seedFile = Path.Combine(folder, "seeds.json");
if (!File.Exists(seedFile))
{
    Console.Error.WriteLine($"Tohum dosyası yok: {seedFile}");
    return 1;
}

IReadOnlyList<PaletteSeed> seeds;

if (rest.Count > 0 && rest[0] == "import")
{
    if (rest.Count < 3)
    {
        Console.Error.WriteLine("Kullanım: import <temalar-klasörü> <benim.tokens.json>");
        return 1;
    }

    var incoming = Directory.GetFiles(rest[1], "*.json")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(StandardImport.FromTheme)
        .Append(StandardImport.FromTokens(rest[2]));
    seeds = StandardImport.Merge(PaletteSeed.Load(seedFile), incoming);
    File.WriteAllText(seedFile, PaletteSeed.Serialize(seeds), new UTF8Encoding(false));
    Console.WriteLine($"Standarttan alındı: {seeds.Count} tohum.");
}
else
{
    seeds = PaletteSeed.Load(seedFile);
}

PaletteBuilder.WriteAll(folder, seeds);
foreach (var seed in seeds) Console.WriteLine($"{seed.Name,-16} {seed.Renk1} {seed.Renk2} {seed.Renk3}");
Console.WriteLine($"{seeds.Count} palet yazıldı: {folder}");
Console.WriteLine("Ad listesi PaletteCatalog.Names içinde; ölçü PaletteTests ikisini karşılaştırır.");
return 0;

