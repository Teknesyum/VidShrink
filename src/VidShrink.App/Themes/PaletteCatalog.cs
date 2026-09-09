using Avalonia;
using Avalonia.Markup.Xaml.Styling;

namespace VidShrink.App.Themes;

/// <summary>
/// Yürürlükteki palet. Renk programın hiçbir yerinde yazılı değil; hepsi
/// <c>Themes/Palette/&lt;ad&gt;.axaml</c> içinde durur ve burada yürürlükteki sözlük
/// yenisiyle değiştirilir. Ölçüler <c>Theme.axaml</c>'de kaldığı için palet değişince
/// yalnız renk değişir, yerleşim yerinde kalır.
///
/// <para>Liste elle yazılı: derlenmiş XAML kaynakları çalışırken sayılamıyor
/// (<c>AssetLoader.GetAssets</c> yalnız ham kaynakları görüyor, denendi ve boş döndü).
/// Listenin klasörle aynı kalması <c>PaletteTests</c>'e bırakıldı; yeni palet eklenip
/// buraya yazılmazsa ölçü kırmızı yanar.</para>
/// </summary>
public static class PaletteCatalog
{
    private const string Folder = "avares://VidShrink.App/Themes/Palette";

    public const string Default = "Neon";

    /// <summary>Sıra <c>seeds.json</c> ile aynı: varsayılan başta, ötekiler tanınırlık sırasında.</summary>
    public static IReadOnlyList<string> Names { get; } = new[]
    {
        "Neon", "Dracula", "Nord", "Gruvbox",
        "TokyoNight", "Catppuccin", "OneDark", "Monokai",
        "Solarized", "Everforest", "RosePine", "Ayu",
        "NightOwl", "Synthwave", "Cobalt", "MaterialOcean",
        "Github", "Kanagawa", "Horizon", "Moonlight"
    };

    /// <summary>
    /// Listede görünen ad: dosya adı tek kelime, burada büyük harften bölünür —
    /// <c>TokyoNight</c> ekranda <c>Tokyo Night</c> olur.
    /// </summary>
    public static string Label(string name)
        => string.Concat(name.Select((letter, at) =>
            at > 0 && char.IsUpper(letter) && !char.IsUpper(name[at - 1]) ? " " + letter : letter.ToString()));

    /// <summary>
    /// Adı verilen paleti yürürlüğe koyar ve gerçekten uygulanan adı döndürür. Tanınmayan
    /// ad varsayılana düşer: elle düzenlenmiş bir ayar dosyası programı açılışta durdurmaz.
    /// </summary>
    public static string Use(string? name)
    {
        var wanted = Names.FirstOrDefault(
            palette => string.Equals(palette, name, StringComparison.OrdinalIgnoreCase)) ?? Default;

        if (Application.Current?.Resources.MergedDictionaries is not { Count: > 0 } merged) return wanted;

        merged[0] = new ResourceInclude((Uri?)null)
        {
            Source = new Uri($"{Folder}/{wanted}.axaml")
        };

        return wanted;
    }
}
