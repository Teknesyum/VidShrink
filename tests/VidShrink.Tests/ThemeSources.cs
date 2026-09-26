using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace VidShrink.Tests;

/// <summary>
/// Tema belirteçlerinin tek okuma yeri. Belirteçler artık tek dosyada değil: renkler
/// <c>Themes/Palette</c> altındaki palet dosyasında, ölçüler <c>Themes/Theme.axaml</c>
/// içinde duruyor. Hangi paletin yürürlükte olduğu <c>App.axaml</c>'ın kendi
/// bildirimidir; okuyucu o bildirimi izler, palet adını içinde taşımaz. Başka bir palete
/// geçildiğinde ölçümler kendiliğinden yeni paleti okur.
/// </summary>
internal static class ThemeSources
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly Regex Include = new(
        @"<ResourceInclude\s+Source=""avares://VidShrink\.App/(?<path>[^""]+)""", RegexOptions.Compiled);

    internal static readonly string AppPath =
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "App.axaml");

    private static readonly Regex PaletteFile = new(
        @"^Themes/Palette/[^/]+/Theme\.axaml$", RegexOptions.Compiled);

    /// <summary>
    /// <c>App.axaml</c>'dan başlayarak birleştirilen sözlük dosyaları, birleşme sırasıyla.
    /// <paramref name="palette"/> verilirse açılış paletinin yerine o palet okunur: bir paletin
    /// kendi tasarımını anlatan ölçü (Neon'un yeşil atmosferi gibi) varsayılan değişince başka
    /// paleti okumasın diye.
    /// </summary>
    internal static IReadOnlyList<string> Files(string? appAxamlPath = null, string? palette = null)
    {
        var start = Path.GetFullPath(appAxamlPath ?? AppPath);
        var root = Path.GetDirectoryName(start)!;
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Walk(string file)
        {
            if (!seen.Add(file) || !File.Exists(file)) return;
            foreach (Match match in Include.Matches(File.ReadAllText(file)))
            {
                var path = match.Groups["path"].Value;
                if (palette is not null && PaletteFile.IsMatch(path))
                    path = $"Themes/Palette/{palette}/Theme.axaml";
                Walk(Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))));
            }
            ordered.Add(file);
        }

        Walk(start);
        return ordered;
    }

    /// <summary>Birleşen sözlüklerin bütün belirteçleri. <c>App.axaml</c>'ın kendisi sözlük taşımaz.</summary>
    internal static IEnumerable<XElement> Resources(string? palette = null) => Files(palette: palette)
        .Where(file => !string.Equals(Path.GetFileName(file), "App.axaml", StringComparison.OrdinalIgnoreCase))
        .SelectMany(file => XDocument.Load(file).Root!.Elements());

    internal static XElement Resource(string key, string? palette = null) => Resources(palette)
        .Single(element => (string?)element.Attribute(X + "Key") == key);

    internal static string Token(string key, string? palette = null) => Resource(key, palette).Value.Trim();
}
