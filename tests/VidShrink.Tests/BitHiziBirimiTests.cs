using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using VidShrink.App.Localization;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Aynı birim tek pencerede beş ayrı yazımla görünüyordu: kaynak bilgisinde <c>128k</c> ile
/// <c>2500 kbps</c>, oynatıcıda <c>kb/s</c>, kaydedicide <c>kbit/sn</c>, plan gerekçesinde
/// bitişik <c>128kbps</c>.
/// </summary>
/// <remarks>
/// Kural: birim yalnız <c>main.unit.kbps-value</c> içinde yazılı. Değer taşıyan cümleler
/// birimi bırakır, çağıran taraf <see cref="Strings.BitHizi(string)"/> ile biçimlendirip
/// geçirir. Etiketler cümle değil, birimi doğrudan taşır — onlar da aynı yazıma bağlı.
/// </remarks>
public class BitHiziBirimiTests
{
    private static string Locales =>
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales");

    private static readonly Regex Jeton = new(
        "kbps|Kbps|kb/s|kbit/sn|kbit/s|Kbit/s|кбит/с|кбіт/с|كيلوبت/ث|كبت/ث|קילוביט/שנייה|کیلوبیت بر ثانیه",
        RegexOptions.Compiled);

    /// <summary>Birimi doğrudan taşımasına izin verilen anahtarlar ve nedenleri.</summary>
    private static readonly Dictionary<string, string> Muaf = new()
    {
        ["main.unit.kbps-value"] = "birimin tek kaynağı",
        ["main.advanced.audio-kbps.label"] = "etiket, cümle değil",
        ["recorder.advanced.bitrate"] = "etiket, cümle değil",
        ["recorder.advanced.max-bitrate"] = "etiket, cümle değil",
        ["main.convert.crf-label"] = "etiket, cümle değil",
        ["main.convert.audio-bitrate"] = "etiket, cümle değil",
        ["main.convert.audio-bitrate.tip"] = "birimi rakamla değil sözcükle anıyor (ar/fa/he)",
        ["main.convert.crf-label.tip"] = "birimi rakamla değil sözcükle anıyor (fa)"
    };

    /// <summary>Etiketler: değer basmazlar ama birim sözcüğü o dilinkiyle aynı olmalı.</summary>
    private static readonly (string Dosya, string Anahtar)[] Etiketler =
    {
        ("main.json", "main.advanced.audio-kbps.label"),
        ("recorder.json", "recorder.advanced.bitrate"),
        ("recorder.json", "recorder.advanced.max-bitrate"),
        ("main.json", "main.convert.crf-label"),
        ("main.json", "main.convert.audio-bitrate")
    };

    private static string[] Diller() =>
        Directory.GetDirectories(Locales).Select(d => Path.GetFileName(d)!).OrderBy(d => d).ToArray();

    private static Dictionary<string, string> Katalog(string dil, string dosya)
    {
        var yol = Path.Combine(Locales, dil, dosya);
        using var belge = JsonDocument.Parse(File.ReadAllText(yol));
        return belge.RootElement.EnumerateObject()
            .Where(p => p.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(p => p.Name, p => p.Value.GetString()!);
    }

    private static string Birim(string dil)
    {
        var sablon = Katalog(dil, "main.json")["main.unit.kbps-value"];
        Assert.StartsWith("{0} ", sablon, StringComparison.Ordinal);
        return sablon["{0} ".Length..];
    }

    [Fact]
    public void HerDildeTekBirimVarVeDolu()
    {
        var diller = Diller();
        Assert.True(diller.Length >= 42, $"dil sayısı {diller.Length}");

        foreach (var dil in diller)
        {
            var birim = Birim(dil);
            Assert.False(string.IsNullOrWhiteSpace(birim), dil);
            Assert.DoesNotContain("{", birim, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <c>main.unit.k-value</c> ("{0}k") kaldırıldı: aynı sayı iki anahtarla iki yazımda
    /// basılıyordu. Hiçbir dilde geri gelmemeli.
    /// </summary>
    [Fact]
    public void IkinciBirimAnahtariYok()
    {
        foreach (var dil in Diller())
            Assert.False(Katalog(dil, "main.json").ContainsKey("main.unit.k-value"), dil);
    }

    /// <summary>
    /// Kapalı küme: 42 dilin bütün sözlüklerinde birim jetonu yalnız muaf anahtarlarda
    /// geçebilir. Yeni bir cümle birimi kendi içine yazarsa bu ölçü kırmızıya döner.
    /// </summary>
    [Fact]
    public void BirimBaskaHicbirCumledeYazmiyor()
    {
        var kacak = new List<string>();

        foreach (var dil in Diller())
        foreach (var yol in Directory.GetFiles(Path.Combine(Locales, dil), "*.json"))
        {
            var dosya = Path.GetFileName(yol);
            foreach (var (anahtar, metin) in Katalog(dil, dosya))
            {
                if (Muaf.ContainsKey(anahtar)) continue;
                if (Jeton.IsMatch(metin)) kacak.Add($"{dil}/{dosya}/{anahtar}: {metin}");
            }
        }

        Assert.Empty(kacak);
    }

    /// <summary>Etiketteki birim, o dilin <c>main.unit.kbps-value</c> birimiyle aynı.</summary>
    [Fact]
    public void EtiketlerAyniBirimiKullaniyor()
    {
        var sapan = new List<string>();

        foreach (var dil in Diller())
        {
            var birim = Birim(dil);
            foreach (var (dosya, anahtar) in Etiketler)
            {
                var metin = Katalog(dil, dosya)[anahtar];
                if (!metin.Contains(birim, StringComparison.Ordinal))
                    sapan.Add($"{dil}/{anahtar}: {metin} (birim {birim})");
            }
        }

        Assert.Empty(sapan);
    }

    /// <summary>
    /// Değer taşıyan cümlelerde yer tutucunun ardından birim kalmamalı; birimi çağıran
    /// taraf ekliyor.
    /// </summary>
    [Theory]
    [InlineData("recorder.json", "recorder.budget.result")]
    [InlineData("recorder.json", "recorder.budget.too-small")]
    [InlineData("playback.json", "player.info.bitrate")]
    [InlineData("main.json", "main.fast-gpu.on-usable")]
    [InlineData("main.json", "main.fast-gpu.bitrate-floor")]
    [InlineData("main.json", "main.reason.manual-audio-bitrate-unmet")]
    [InlineData("main.json", "main.reason.manual-audio-bitrate-superseded")]
    [InlineData("main.json", "main.reason.manual-audio-bitrate-override")]
    public void DegerCumleleriBirimsiz(string dosya, string anahtar)
    {
        foreach (var dil in Diller())
        {
            var metin = Katalog(dil, dosya)[anahtar];
            Assert.False(Jeton.IsMatch(metin), $"{dil}/{anahtar}: {metin}");
            Assert.Contains("{0}", metin, StringComparison.Ordinal);
        }
    }

    /// <summary>Biçimleyicinin kendisi: sayı dilin birimiyle birleşiyor.</summary>
    [Theory]
    [InlineData("tr", "128 kbit/sn")]
    [InlineData("en", "128 kbit/s")]
    [InlineData("ru", "128 кбит/с")]
    public void BiciminCiktisiDilinBirimi(string dil, string beklenen)
    {
        var onceki = Strings.Language;
        try
        {
            Strings.Use(dil);
            Assert.Equal(beklenen, Strings.BitHizi(128));
            Assert.Equal(beklenen, Strings.BitHizi("128"));
        }
        finally
        {
            Strings.Use(onceki);
        }
    }

    /// <summary>
    /// Tek yazıcı: anahtarın adı kaynakta yalnız <c>Strings.cs</c>'te geçiyor. Bir görünüm
    /// sayıyı kendi başına biçimlerse birim yine ikiye ayrılırdı.
    /// </summary>
    [Fact]
    public void AnahtariYalnizBiciminKendisiOkuyor()
    {
        var src = Path.Combine(TipSources.Root, "src");
        var yazanlar = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(y => !y.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(y => !y.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(y => File.ReadAllText(y).Contains("\"main.unit.kbps-value\"", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OrderBy(a => a)
            .ToArray();

        Assert.Equal(new[] { "Strings.cs" }, yazanlar);
    }
}
