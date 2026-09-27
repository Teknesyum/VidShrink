using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace VidShrink.PaletteGen;

/// <summary>
/// Bir paletin tek şemadaki tanımı: Teknesyum UI standardının renk rolleri
/// (<c>renk-1</c> … <c>warning</c>) ve VidShrink'in üç ek rolü (<c>danger</c>, <c>flame</c>,
/// <c>atmos</c>). Palet dosyasındaki otuz bir anahtar yalnız bunlardan türer
/// (<see cref="PaletteBuilder"/>). Eşleme ve kararlar <c>docs/netlestirme/027-palet-birlesimi.md</c>.
/// </summary>
public sealed record PaletteSeed(
    string Name,
    string? Title,
    string Source,
    string Note,
    string Renk1,
    string Renk2,
    string Renk3,
    string Renk2Text,
    string Renk3Text,
    string Surface,
    string Black,
    string GlassBase,
    string Text,
    string Disabled,
    string Success,
    string Warning,
    string? Danger = null,
    string? Flame = null,
    string? Atmos = null,
    double? Ground = null)
{
    /// <summary>Tohum dosyasındaki alan sırası; yazım bu sırayla yapılır.</summary>
    public static IReadOnlyList<string> Roles { get; } = new[]
    {
        "renk-1", "renk-2", "renk-3", "renk-2-text", "renk-3-text",
        "surface", "black", "glass-base", "text", "disabled", "success", "warning",
        "danger", "flame", "atmos"
    };

    /// <summary>
    /// Standardın kendi temasının kaynağı; bu kaynaktan gelen palet listede başa oturur. Renkleri
    /// tohumda yazılı değil, her okumada bu dosyadan gelir; dosyayı <c>setup.js --apply</c> günceller.
    /// </summary>
    public const string OwnSource = "teknesyum-ui/theme.tokens.json";

    /// <summary>Projenin kendi 26 paletinin kaynağı.</summary>
    public const string ProjectSource = "vidshrink";

    public string? Role(string role) => role switch
    {
        "renk-1" => Renk1,
        "renk-2" => Renk2,
        "renk-3" => Renk3,
        "renk-2-text" => Renk2Text,
        "renk-3-text" => Renk3Text,
        "surface" => Surface,
        "black" => Black,
        "glass-base" => GlassBase,
        "text" => Text,
        "disabled" => Disabled,
        "success" => Success,
        "warning" => Warning,
        "danger" => Danger,
        "flame" => Flame,
        "atmos" => Atmos,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    public static IReadOnlyList<PaletteSeed> Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.EnumerateArray()
            .Select(element => IsOwn(element) ? Own(path, element) : Read(element))
            .ToList();
    }

    private static bool IsOwn(JsonElement element)
        => element.TryGetProperty("kaynak", out var source) && source.GetString() == OwnSource;

    private static PaletteSeed Own(string seedFile, JsonElement element)
    {
        var seed = StandardImport.FromTokens(OwnTokens(seedFile));
        if (element.TryGetProperty("note", out var note) && note.GetString() is { } text)
            seed = seed with { Note = text };
        if (element.TryGetProperty("atmos", out var atmos) && atmos.GetString() is { } role)
            seed = seed with { Atmos = seed.Role(role) ?? throw new InvalidDataException($"{seed.Name}: atmos rolü '{role}' boş.") };
        if (element.TryGetProperty("zemin", out var ground) && ground.TryGetDouble(out var weight))
            seed = seed with { Ground = weight };
        return seed;
    }

    /// <summary>Tohum dosyasından yukarı yürüyerek projenin <see cref="OwnSource"/> dosyasını bulur.</summary>
    public static string OwnTokens(string seedFile)
    {
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(seedFile))!); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, OwnSource);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException($"{OwnSource} bulunamadı: {seedFile} üstünde yok.");
    }

    private static PaletteSeed Read(JsonElement element)
    {
        string? Optional(string key)
            => element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        string Required(string key)
            => Optional(key) ?? throw new InvalidDataException(
                $"{Optional("name") ?? "?"}: '{key}' alanı yok.");

        return new PaletteSeed(
            Required("name"), Optional("baslik"), Required("kaynak"), Required("note"),
            Required("renk-1"), Required("renk-2"), Required("renk-3"),
            Required("renk-2-text"), Required("renk-3-text"),
            Required("surface"), Required("black"), Required("glass-base"),
            Required("text"), Required("disabled"), Required("success"), Required("warning"),
            Optional("danger"), Optional("flame"), Optional("atmos"));
    }

    /// <summary>Tohum dosyasının biçimi: her palet tek satır, alanlar <see cref="Roles"/> sırasında.</summary>
    public static string Serialize(IEnumerable<PaletteSeed> seeds)
    {
        var options = new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var lines = seeds.Select(seed =>
        {
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer, options))
            {
                writer.WriteStartObject();
                writer.WriteString("name", seed.Name);
                if (seed.Title is not null) writer.WriteString("baslik", seed.Title);
                writer.WriteString("kaynak", seed.Source);
                if (seed.Source != OwnSource)
                    foreach (var role in Roles)
                        if (seed.Role(role) is { } value) writer.WriteString(role, value);
                writer.WriteString("note", seed.Note);
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buffer.ToArray());
        });
        return "[\n" + string.Join(",\n", lines) + "\n]\n";
    }
}
