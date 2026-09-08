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

    /// <summary>Renk çemberi sırasıyla: ayarlardaki liste tondan tona yürüsün.</summary>
    public static IReadOnlyList<string> Names { get; } = new[]
    {
        "Neon", "Lagoon", "Azure", "Cobalt", "Indigo", "Violet", "Orchid", "Magenta",
        "Rose", "Crimson", "Scarlet", "Flare", "Amber", "Gold", "Citron", "Lime",
        "Fern", "Emerald", "Jade", "Teal"
    };

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
