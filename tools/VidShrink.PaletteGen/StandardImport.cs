using System.Text.Json;

namespace VidShrink.PaletteGen;

/// <summary>
/// Teknesyum UI standardının temalarını tohum dosyasına alır: <c>templates/temalar/*.json</c>
/// (<c>renk</c> nesnesi) ve standardın kendi teması <c>benim.tokens.json</c> (<c>brand</c> ve
/// <c>role</c>). Aynı adlı tohum yerinde güncellenir, yeni ad eklenir; sıra
/// <see cref="Order"/>'dan çıkar.
/// </summary>
public static class StandardImport
{
    public const string OwnName = "Teknesyum";

    private const string OwnNote =
        "Teknesyum UI standardının kendi teması: saf siyah zemin üstünde mavi, pembe ve mor üçlü.";

    public static PaletteSeed FromTheme(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var id = root.GetProperty("ad").GetString()!;
        var colours = root.GetProperty("renk");
        string Colour(string role) => Upper(colours.GetProperty(role).GetString()!);

        return new PaletteSeed(
            char.ToUpperInvariant(id[0]) + id[1..],
            root.GetProperty("baslik").GetString(),
            $"teknesyum-ui/temalar/{id}.json",
            root.GetProperty("esin").GetString()!,
            Colour("renk-1"), Colour("renk-2"), Colour("renk-3"),
            Colour("renk-2-text"), Colour("renk-3-text"),
            Colour("surface"), Colour("black"), Colour("glass-base"),
            Colour("text"), Colour("disabled"), Colour("success"), Colour("warning"));
    }

    public static PaletteSeed FromTokens(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        string Brand(string role) => Upper(root.GetProperty("brand").GetProperty(role).GetProperty("value").GetString()!);
        string Role(string role) => Upper(root.GetProperty("role").GetProperty(role).GetProperty("value").GetString()!);
        string? Danger = root.GetProperty("role").TryGetProperty("danger", out var danger)
                         && danger.TryGetProperty("value", out var value)
            ? Upper(value.GetString()!)
            : null;

        return new PaletteSeed(
            OwnName, OwnName, PaletteSeed.OwnSource, OwnNote,
            Brand("renk-1"), Brand("renk-2"), Brand("renk-3"),
            Brand("renk-2-text"), Brand("renk-3-text"),
            Brand("surface"), Brand("black"), Brand("glass-base"),
            Role("text"), Role("disabled"), Role("success"), Role("warning"),
            Danger);
    }

    /// <summary>Tohumları alır, aynı adı günceller, yenileri ekler ve sıraya dizer.</summary>
    public static IReadOnlyList<PaletteSeed> Merge(IEnumerable<PaletteSeed> existing, IEnumerable<PaletteSeed> incoming)
    {
        var merged = existing.ToList();
        foreach (var seed in incoming)
        {
            var at = merged.FindIndex(old => old.Name == seed.Name);
            if (at >= 0) merged[at] = seed;
            else merged.Add(seed);
        }
        return Order(merged);
    }

    /// <summary>
    /// Liste sırası: standardın kendi teması başta (varsayılan), sonra koyu zeminliler, en sonda
    /// açık zeminliler. Her grupta önce projenin paletleri, sonra standardınkiler; grup içindeki
    /// sıra korunur.
    /// </summary>
    public static IReadOnlyList<PaletteSeed> Order(IReadOnlyList<PaletteSeed> seeds)
        => seeds
            .Select((seed, index) => (seed, index))
            .OrderBy(item => item.seed.Source == PaletteSeed.OwnSource ? 0 : 1)
            .ThenBy(item => PaletteBuilder.Luminance(item.seed.Black) > 0.5 ? 1 : 0)
            .ThenBy(item => item.seed.Source == PaletteSeed.ProjectSource ? 0 : 1)
            .ThenBy(item => item.index)
            .Select(item => item.seed)
            .ToList();

    private static string Upper(string hex) => "#" + hex.TrimStart('#').ToUpperInvariant();
}
