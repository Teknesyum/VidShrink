using System;
using System.IO;
using System.Linq;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// K8 borcu 8: onizleme rozetinin sayisiz kalan hali. Modellenmeyen kodlayicida parca nihai
/// ciktidan en cok saptigi halde rozet hic cikmiyordu; olcu sapmanin her turunun ekrana bir
/// karsilik bulmasini pimler.
/// </summary>
public sealed class OnizlemeRozetiTests
{
    private static PreviewClip Parca(bool yaklasik, int? crf, PreviewQuality tur) => new()
    {
        SourcePath = "a.mp4",
        EncodedPath = "b.mp4",
        StartSeconds = 0,
        DurationSeconds = 5,
        IsApproximate = yaklasik,
        Crf = crf,
        QualityKind = tur,
        Elapsed = TimeSpan.Zero
    };

    [Fact]
    public void ModellenmeyenKodlayicidaRozetSayisizCikiyor()
    {
        var metin = PanelHost.RozetMetni(Parca(true, null, PreviewQuality.Desteklenmiyor));

        Assert.Equal(Strings.Get("main.preview.temsili"), metin);
        Assert.False(string.IsNullOrWhiteSpace(metin));
    }

    [Fact]
    public void SayiVarkenRozetSayiyi_yaziyor()
    {
        var metin = PanelHost.RozetMetni(Parca(true, 27, PreviewQuality.Yaklasik));

        Assert.Equal(Strings.Get("main.plan.mode.crf-value", 27), metin);
    }

    /// <summary>Modellenmeyen kodlayici sayi uretse bile sayi kazanir; kelime yedek koldur.</summary>
    [Fact]
    public void SayiKelimeyiEziyor()
    {
        var metin = PanelHost.RozetMetni(Parca(true, 30, PreviewQuality.Desteklenmiyor));

        Assert.Equal(Strings.Get("main.plan.mode.crf-value", 30), metin);
        Assert.NotEqual(Strings.Get("main.preview.temsili"), metin);
    }

    [Theory]
    [InlineData(PreviewQuality.Kesin)]
    [InlineData(PreviewQuality.Kopya)]
    [InlineData(PreviewQuality.Yaklasik)]
    [InlineData(PreviewQuality.Desteklenmiyor)]
    public void SapmayanParcadaRozetYok(PreviewQuality tur)
    {
        Assert.Null(PanelHost.RozetMetni(Parca(false, 27, tur)));
        Assert.Null(PanelHost.RozetMetni(Parca(false, null, tur)));
    }

    [Fact]
    public void ParcaYokkenRozetYok() => Assert.Null(PanelHost.RozetMetni(null));

    /// <summary>
    /// Modellenmeyen kodlayici gercekten <see cref="PreviewQuality.Desteklenmiyor"/> uretiyor;
    /// olumsuz kontrol modellenen kodlayicinin ayni girdide baska bir kola dusmesi.
    /// </summary>
    [Fact]
    public void ModellenmeyenKodekDesteklenmiyorUretiyor()
    {
        var info = Kaynak();

        var disarida = PreviewSegment.QualityFor(info, Plan("h264_videotoolbox"));
        var iceride = PreviewSegment.QualityFor(info, Plan("libx264"));

        Assert.Equal(PreviewQuality.Desteklenmiyor, disarida.Kind);
        Assert.Null(disarida.Crf);
        Assert.NotEqual(PreviewQuality.Desteklenmiyor, iceride.Kind);
        Assert.NotNull(iceride.Crf);
    }

    /// <summary>
    /// Parcadan panele gecen dikis: kalite turu tasiniyor ve modellenmeyen kodlayici ucdan uca
    /// sayisiz rozete cikiyor. Olumsuz kontrol modellenen kodlayicinin sayili rozete cikmasi.
    /// </summary>
    [Fact]
    public void TurParcadanPaneleTasiniyor()
    {
        var info = Kaynak();

        var disarida = SegmentEncoder.Klip(
            PreviewSegment.For(info, Plan("h264_videotoolbox"), 0, "c.mp4"), "a.mp4", "c.mp4", 0, TimeSpan.Zero);
        var iceride = SegmentEncoder.Klip(
            PreviewSegment.For(info, Plan("libx264"), 0, "d.mp4"), "a.mp4", "d.mp4", 0, TimeSpan.Zero);

        Assert.Equal(PreviewQuality.Desteklenmiyor, disarida.QualityKind);
        Assert.Equal(Strings.Get("main.preview.temsili"), PanelHost.RozetMetni(disarida));

        Assert.Equal(PreviewQuality.Yaklasik, iceride.QualityKind);
        Assert.Equal(Strings.Get("main.plan.mode.crf-value", iceride.Crf), PanelHost.RozetMetni(iceride));
    }

    [Fact]
    public void RozetAnahtariKirkIkiDildeVar()
    {
        var kok = Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales");
        var klasorler = Directory.GetDirectories(kok);
        Assert.True(klasorler.Length >= 42, $"Dil klasoru sayisi 42'nin altinda: {klasorler.Length}");

        foreach (var klasor in klasorler)
        {
            var yol = Path.Combine(klasor, "main.json");
            var metin = File.ReadAllText(yol);
            Assert.True(
                metin.Contains("\"main.preview.temsili\"", StringComparison.Ordinal),
                $"{Path.GetFileName(klasor)}/main.json icinde main.preview.temsili yok");
        }
    }

    /// <summary>Ceviriler birbirinden ayri; tek bir dilin metni otuz dile kopyalanmamis.</summary>
    [Fact]
    public void CevirilerTekBirMetneCokmemis()
    {
        var kok = Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales");
        var degerler = Directory.GetDirectories(kok)
            .Select(k => File.ReadAllLines(Path.Combine(k, "main.json"))
                .FirstOrDefault(s => s.Contains("\"main.preview.temsili\"", StringComparison.Ordinal)))
            .Where(s => s is not null)
            .Select(s => s!.Split(':', 2)[1].Trim())
            .ToList();

        Assert.True(degerler.Distinct(StringComparer.Ordinal).Count() >= 20,
            $"Ayri ceviri sayisi cok dusuk: {degerler.Distinct(StringComparer.Ordinal).Count()}");
    }

    private static MediaInfo Kaynak() => new()
    {
        FilePath = "a.mp4",
        FileSizeBytes = 40_000_000,
        VideoCodec = "h264",
        TotalBitrateBps = 5_000_000,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        DurationSeconds = 60
    };

    private static EncodePlan Plan(string codec) => new()
    {
        Codec = codec,
        Mode = "bitrate",
        VideoBitrateK = 2500,
        Width = 1920,
        Height = 1080,
        Fps = 30
    };
}
