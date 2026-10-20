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
/// <para>
/// 2026-10-15: <c>KeyCaption</c> diske uc anahtarla yazilir (<c>keyCaptionPlace</c>, <c>keyCaptionSize</c>,
/// <c>keyCaptionHold</c>); anahtar sayisi ozellik basina bir yerine <c>CokAnahtarli</c>'daki sayiyla toplanir.
/// </para>
/// <para>
/// 2026-10-15: <c>ScheduleAt</c> saniyeye kadar yazilir; tik kesirli bir uretilmis deger geri donmez, sabit tarih <c>Kisitli</c>'da.
/// </para>
/// <para>
/// 2026-10-17: <c>HdrToSdr</c> yaninda goc isareti <c>hdrToSdrDefaultOn</c> yazilir; iki anahtar sayilir.
/// </para>
/// <para>
/// 2026-10-20: bes <c>Frame*</c> ayari tek <c>frame</c> nesnesine yazilir; bir anahtar sayilir, dolgu ve kose secenekten secilir.
/// </para>
/// </summary>
public sealed class KaydediciAyarGidisDonusTests
{
    private static readonly Dictionary<string, object> Kisitli = new()
    {
        ["CountdownSeconds"] = 5,
        ["ReplaySeconds"] = 120,
        ["WebcamX"] = 0.25,
        ["WebcamY"] = 0.5,
        ["WebcamSize"] = 0.35,
        ["RegionAspect"] = "4:3",
        ["PixelFormat"] = "yuv444p10le",
        ["Codec"] = "libx265",
        ["Preset"] = "slow",
        ["ColorRange"] = "jpeg",
        ["AudioCodec"] = "libopus",
        ["Hotkeys"] = "2:F1,2:F2,2:F3,2:F4,2:F5,2:F6,2:F7,2:F8",
        ["PrompterStep"] = 5,
        ["PrompterLook"] = new PrompterLook(Mirror: true, Line: false, Size: 0),
        ["ScheduleAt"] = new DateTime(2026, 10, 12, 14, 30, 0),
        ["FrameBackdrop"] = 3,
        ["FramePadding"] = 128,
        ["FrameCorner"] = 48
    };

    private static readonly Dictionary<string, int> CokAnahtarli = new()
    {
        ["KeyCaption"] = 3,
        ["PrompterLook"] = 3,
        ["HdrToSdr"] = 2,
        ["FrameBackdrop"] = 1,
        ["FramePadding"] = 0,
        ["FrameCorner"] = 0,
        ["FrameShadow"] = 0,
        ["FrameImage"] = 0
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
        if (tur == typeof(List<RecorderRegion>)) return new List<RecorderRegion> { new(11, 22, 33, 44), new(-5, 6, 70, 80) };
        if (tur == typeof(KeyCaptionStyle)) return new KeyCaptionStyle(KeyCaptionPlace.Top, KeyCaptionSize.Large, KeyCaptionHold.Long);
        throw new InvalidOperationException($"{ozellik.Name} icin deger ureticisi yok: {tur.Name}");
    }

    private static bool Ayni(object? a, object? b)
        => a is List<RecorderRegion> x && b is List<RecorderRegion> y ? x.SequenceEqual(y) : Equals(a, b);

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
            Assert.Equal(ozellikler.Sum(o => CokAnahtarli.GetValueOrDefault(o.Name, 1)), anahtarlar);
            var kayip = ozellikler
                .Where(o => !Ayni(o.GetValue(yazilan), o.GetValue(okunan)))
                .Select(o => $"{o.Name}: yazilan {o.GetValue(yazilan)}, okunan {o.GetValue(okunan)}")
                .ToList();
            Assert.Empty(kayip);
            Assert.All(ozellikler, o => Assert.False(Ayni(o.GetValue(varsayilan), o.GetValue(okunan)), o.Name));
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
