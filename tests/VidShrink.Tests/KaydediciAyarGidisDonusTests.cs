using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using VidShrink.App.Recorder;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Paket 2b borcu: <see cref="RecorderSettings"/>'in yazilabilir her ozelligi varsayilandan farkli
/// bir degerle diske yazilir, yeni bir nesneye okunur ve ozellik ozellik karsilastirilir. Ozellik
/// listesi yansimayla toplaniyor; yeni eklenen bir ayar <c>ToJson</c> ya da <c>Load</c>'da
/// unutulursa bu olcu kirmizi olur.
/// </summary>
public sealed class KaydediciAyarGidisDonusTests
{
    private static readonly Dictionary<string, object> Kisitli = new()
    {
        ["CountdownSeconds"] = 5,
        ["WebcamWidth"] = 480,
        ["RegionAspect"] = "4:3",
        ["PixelFormat"] = "yuv444p10le",
        ["Codec"] = "libx265",
        ["Preset"] = "slow",
        ["ColorRange"] = "jpeg"
    };

    internal static IReadOnlyList<PropertyInfo> Ozellikler() => typeof(RecorderSettings)
        .GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
        .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
        .OrderBy(p => p.Name, StringComparer.Ordinal)
        .ToList();

    internal static object Farkli(PropertyInfo ozellik, object? varsayilan)
    {
        if (Kisitli.TryGetValue(ozellik.Name, out var sabit)) return sabit;
        var tur = Nullable.GetUnderlyingType(ozellik.PropertyType) ?? ozellik.PropertyType;
        if (tur == typeof(string)) return "deger-" + ozellik.Name;
        if (tur == typeof(bool)) return !(bool)(varsayilan ?? false);
        if (tur == typeof(int)) return (int)(varsayilan ?? 0) + 7;
        if (tur == typeof(double)) return (double)(varsayilan ?? 0d) + 7.5;
        if (tur.IsEnum) return Enum.GetValues(tur).Cast<object>().Last(v => !Equals(v, varsayilan));
        throw new InvalidOperationException($"{ozellik.Name} icin deger ureticisi yok: {tur.Name}");
    }

    [Fact]
    public void HerAyarDiskeGidipAyniDonerDegisir()
    {
        var klasor = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".calisma", "paket-2b", "ayar-gidis-donus");
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, $"recorder-settings-{Environment.ProcessId}.json");
        try
        {
            var varsayilan = new RecorderSettings();
            var yazilan = new RecorderSettings();
            var ozellikler = Ozellikler();
            foreach (var ozellik in ozellikler)
                ozellik.SetValue(yazilan, Farkli(ozellik, ozellik.GetValue(varsayilan)));

            yazilan.Save(dosya);
            var okunan = RecorderSettings.Load(dosya);

            var anahtarlar = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(dosya))!.AsObject().Count;
            Assert.Equal(ozellikler.Count, anahtarlar);
            var kayip = ozellikler
                .Where(o => !Equals(o.GetValue(yazilan), o.GetValue(okunan)))
                .Select(o => $"{o.Name}: yazilan {o.GetValue(yazilan)}, okunan {o.GetValue(okunan)}")
                .ToList();
            Assert.Empty(kayip);
            Assert.All(ozellikler, o => Assert.NotEqual(o.GetValue(varsayilan), o.GetValue(okunan)));
        }
        finally
        {
            File.Delete(dosya);
        }
    }

    [Fact]
    public void BozukDosyaVarsayilanaDoner()
    {
        var klasor = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".calisma", "paket-2b", "ayar-gidis-donus");
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, $"bozuk-{Environment.ProcessId}.json");
        try
        {
            File.WriteAllText(dosya, "{ \"fps\": ");
            var okunan = RecorderSettings.Load(dosya);
            var varsayilan = new RecorderSettings();

            Assert.All(Ozellikler(), o => Assert.Equal(o.GetValue(varsayilan), o.GetValue(okunan)));
        }
        finally
        {
            File.Delete(dosya);
        }
    }
}
