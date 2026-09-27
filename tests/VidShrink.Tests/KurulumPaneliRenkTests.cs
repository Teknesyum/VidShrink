using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kurulum paneli Avalonia değil, yerel Win32 çizimi; düzenin teması ona ulaşmaz. Renkleri
/// <c>Panel.cs</c>'te sabit, değerleri <c>teknesyum-ui/theme.tokens.json</c>'dan gelir.
/// Belirteç değişip sabit kalırsa bu ölçü kırılır.
/// </summary>
public sealed class KurulumPaneliRenkTests
{
    private static readonly (string Sabit, string Belirtec, double Alfa)[] Esleme =
    {
        ("Background", "brand.black", 1),
        ("Glass", "brand.glass-base", 0.9),
        ("Text", "role.text", 1),
        ("Cyan", "brand.renk-1", 1),
        ("Purple", "brand.renk-3", 1),
        ("Pink", "brand.renk-2", 1),
        ("PinkText", "brand.renk-2-text", 1),
        ("Success", "role.success", 1),
        ("Muted", "role.disabled", 1),
        ("Edge", "brand.renk-1", 0.7),
        ("Hairline", "brand.renk-1", 0.2),
    };

    [Fact]
    public void SabitlerBelirtecDegeriniTasir()
    {
        var kaynak = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.Setup", "Panel.cs"));
        using var belge = JsonDocument.Parse(File.ReadAllText(Path.Combine(TipSources.Root, "teknesyum-ui", "theme.tokens.json")));
        var farklar = new List<string>();

        foreach (var (sabit, belirtec, alfa) in Esleme)
        {
            var eslesme = Regex.Match(kaynak, @"const uint " + sabit + @" = 0x([0-9A-Fa-f]{8});");
            Assert.True(eslesme.Success, sabit + " sabiti bulunamadı");
            var parca = belirtec.Split('.');
            var hex = belge.RootElement.GetProperty(parca[0]).GetProperty(parca[1]).GetProperty("value").GetString()!.TrimStart('#');
            var beklenen = ((int)Math.Round((decimal)alfa * 255, MidpointRounding.AwayFromZero)).ToString("X2", CultureInfo.InvariantCulture) + hex.ToUpperInvariant();
            if (!string.Equals(eslesme.Groups[1].Value, beklenen, StringComparison.OrdinalIgnoreCase))
                farklar.Add(sabit + ": 0x" + eslesme.Groups[1].Value + " != 0x" + beklenen + " (" + belirtec + ")");
        }

        Assert.Empty(farklar);
    }

    [Fact]
    public void EskiNeonDegeriKalmadi()
    {
        var kaynak = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.Setup", "Panel.cs"));
        Assert.DoesNotMatch(new Regex("00F3FF|B026FF|FF00EA|FF54EB|34D399|71717A", RegexOptions.IgnoreCase), kaynak);
    }
}
