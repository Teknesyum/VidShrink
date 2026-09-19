using System.Globalization;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// <see cref="PromptBuilder"/> metni İngilizce ve makineye göre değişmemeli: model bu
/// satırları okuyor ve iki koşumun istemi karşılaştırılabilir kalmalı. Beş sayı araya
/// biçim konmadan yazılıyordu, yani <see cref="CultureInfo.CurrentCulture"/> okunuyordu;
/// Türkçe bir makinede süre <c>12,34 s</c>, boyut <c>9,5 MB</c>, kare hızı <c>23,98</c>
/// çıkıyordu. Ölçü istemi <c>tr-TR</c> altında kuruyor — hiçbir sayıda virgül olmamalı.
/// </summary>
public sealed class IstemKulturuTests
{
    private static MediaInfo Kaynak() => new()
    {
        FilePath = "kaynak.mp4",
        DurationSeconds = 12.34,
        Width = 1920,
        Height = 1080,
        Fps = 23.976,
        VideoCodec = "h264",
        FileSizeBytes = 10_000_000,
        TotalBitrateBps = 8_000_000
    };

    [Fact]
    public void IstemSayilariMakineninKulturunuOkumaz()
    {
        var onceki = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            var kaynak = Kaynak();
            var secenek = new PlanOptions { TargetMb = 7.25 };
            var plan = PlanCalculator.Build(kaynak, secenek);
            var istem = PromptBuilder.Build(kaynak, secenek, plan);

            Assert.Contains("- duration: 12.34 s", istem);
            Assert.Contains("23.98 fps", istem);
            Assert.Contains("- target size: 7.25 MB", istem);
            Assert.DoesNotContain("12,34", istem);
            Assert.DoesNotContain("23,98", istem);
            Assert.DoesNotContain("7,25", istem);
        }
        finally { CultureInfo.CurrentCulture = onceki; }
    }

    /// <summary>
    /// Taramanın kör olmadığının olumlu kontrolü: aynı sayılar <c>tr-TR</c>'de gerçekten
    /// virgülle yazılabiliyor, yani yukarıdaki <c>DoesNotContain</c>'ler boşa düşmüyor.
    /// </summary>
    [Fact]
    public void OlumluKontrolAyniSayiTurkceVirgulAlabiliyor()
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");

        Assert.Equal("12,34", 12.34.ToString("0.##", tr));
        Assert.Equal("23,98", 23.976.ToString("0.##", tr));
    }
}
