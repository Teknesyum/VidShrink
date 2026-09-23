using System.Text.Json;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class HizalamaRaporuTests
{
    private static MediaInfo Kaynak() => new()
    {
        FilePath = @"C:\Kayitlar\hizalama.mp4",
        FileSizeBytes = 90_000_000L,
        DurationSeconds = 62.0,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 11_600_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p"
    };

    private static CliRequest Istek(bool json) => new()
    {
        Command = CliCommand.Shrink,
        Input = @"C:\Kayitlar\hizalama.mp4",
        TargetMb = 25,
        Codec = CliCodec.Auto,
        SkipMeasurement = true,
        Json = json,
        PreferredLanguage = "en"
    };

    private static CliDecision Karar(bool json)
        => CliApp.Decide(Istek(json), Kaynak(), null, null, null);

    private static EncodeResult Sonuc(CliDecision karar)
        => new(true, karar.OutputPath, 24.6, karar.Plan, 1, null);

    private static QualityScore Skor(TimestampAlignment? hizalama)
        => new(92.5, null, null, null, null, null, Alignment: hizalama);

    private static TimestampAlignment Kayma(double saniye)
        => new(saniye, 0, 1.0 / 30);

    [Fact]
    public void KaymaVarkenMetinRaporuHizalamaSatiriniTasiyor()
    {
        var karar = Karar(false);
        var metin = CliApp.ShrinkText(karar, Sonuc(karar), TimeSpan.FromSeconds(12),
            Skor(Kayma(0.02)), CliText.ForLanguage("en"));

        Assert.Contains("Alignment:", metin);
        Assert.Contains("20 ms", metin);
        Assert.Contains("0.6 frames", metin);
    }

    [Fact]
    public void KaymaYokkenMetinRaporuSessizKaliyor()
    {
        var karar = Karar(false);
        var metin = CliApp.ShrinkText(karar, Sonuc(karar), TimeSpan.FromSeconds(12),
            Skor(Kayma(0)), CliText.ForLanguage("en"));

        Assert.DoesNotContain("Alignment:", metin);
    }

    [Fact]
    public void OlcumYokkenMetinRaporuSessizKaliyor()
    {
        var karar = Karar(false);
        var metin = CliApp.ShrinkText(karar, Sonuc(karar), TimeSpan.FromSeconds(12), null, CliText.ForLanguage("en"));

        Assert.DoesNotContain("Alignment:", metin);
    }

    [Fact]
    public void KaymaVarkenJsonRaporuMilisaniyeVeKareTasiyor()
    {
        var karar = Karar(true);
        var belge = JsonDocument.Parse(CliApp.ShrinkJson(Istek(true), karar, Sonuc(karar),
            TimeSpan.FromSeconds(12), Skor(Kayma(0.02)), 0));

        var hizalama = belge.RootElement.GetProperty("result").GetProperty("alignment");
        Assert.Equal(20, hizalama.GetProperty("shiftMs").GetDouble(), 3);
        Assert.Equal(0.6, hizalama.GetProperty("shiftFrames").GetDouble(), 3);
    }

    [Fact]
    public void KaymaYokkenJsonRaporuBosGeciyor()
    {
        var karar = Karar(true);
        var belge = JsonDocument.Parse(CliApp.ShrinkJson(Istek(true), karar, Sonuc(karar),
            TimeSpan.FromSeconds(12), Skor(Kayma(0)), 0));

        Assert.Equal(JsonValueKind.Null, belge.RootElement.GetProperty("result").GetProperty("alignment").ValueKind);
    }

    [Fact]
    public void IkiDilDeHizalamaAnahtariniTasiyor()
    {
        foreach (var dil in new[] { "tr", "en" })
        {
            var kalip = CliText.ForLanguage(dil)["result.alignment"];
            Assert.Contains("{0}", kalip);
            Assert.Contains("{1}", kalip);
        }
    }
}
