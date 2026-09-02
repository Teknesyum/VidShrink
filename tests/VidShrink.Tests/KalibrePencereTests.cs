using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class KalibrePencereTests
{
    private static MediaInfo Kaynak(double sureSaniye) => new()
    {
        FilePath = "kaynak.mkv",
        FileSizeBytes = 400L * 1024 * 1024,
        DurationSeconds = sureSaniye,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 8_000_000
    };

    private static SceneMap Harita(double sure, params (double Uzunluk, double BitHizi)[] sahneler)
    {
        var liste = new List<Scene>(sahneler.Length);
        var imlec = 0.0;
        for (var i = 0; i < sahneler.Length; i++)
        {
            var (uzunluk, bitHizi) = sahneler[i];
            var son = i == sahneler.Length - 1 ? sure : imlec + uzunluk;
            liste.Add(new Scene
            {
                Index = i,
                Start = imlec,
                End = son,
                Bits = (long)(bitHizi * (son - imlec)),
                Complexity = 1.0
            });
            imlec = son;
        }
        return new SceneMap { Threshold = SceneMap.DefaultThreshold, Duration = sure, Scenes = liste };
    }

    private static SceneMap DuzHarita(double sure, int sahneSayisi)
    {
        var adim = sure / sahneSayisi;
        var sahneler = new (double, double)[sahneSayisi];
        for (var i = 0; i < sahneSayisi; i++) sahneler[i] = (adim, 4.0e6);
        return Harita(sure, sahneler);
    }

    private static SceneMap DegiskenHarita(double sure, int sahneSayisi)
    {
        var adim = sure / sahneSayisi;
        var sahneler = new (double, double)[sahneSayisi];
        for (var i = 0; i < sahneSayisi; i++) sahneler[i] = (adim, i % 2 == 0 ? 0.6e6 : 18.0e6);
        return Harita(sure, sahneler);
    }

    private static double[] Baslangiclar(IReadOnlyList<SampleWindow> pencereler)
        => pencereler.Select(p => Math.Round(p.Start, 3)).ToArray();

    [Fact]
    public void AyniSureFarkliHeterojenlikFarkliPencereSayisiVermeli()
    {
        var kaynak = Kaynak(600);
        var duz = CalibrationProbe.Windows(kaynak, SpeedMode.Quality, DuzHarita(600, 10));
        var degisken = CalibrationProbe.Windows(kaynak, SpeedMode.Quality, DegiskenHarita(600, 10));

        Assert.NotEqual(duz.Count, degisken.Count);
    }

    [Fact]
    public void SahneKesigiPencereMerkezleriniDegistirmeli()
    {
        var kaynak = Kaynak(240);
        var erken = Harita(240, (20, 1.0e6), (20, 16.0e6), (20, 2.0e6), (180, 9.0e6));
        var gec = Harita(240, (150, 1.0e6), (30, 16.0e6), (30, 2.0e6), (30, 9.0e6));

        var erkenPencereler = CalibrationProbe.Windows(kaynak, SpeedMode.Quality, erken);
        var gecPencereler = CalibrationProbe.Windows(kaynak, SpeedMode.Quality, gec);

        Assert.NotEqual(Baslangiclar(erkenPencereler), Baslangiclar(gecPencereler));
    }

    [Fact]
    public void UzunVeCokDegiskenKaynakUcPencereninUstuneCikmali()
    {
        var pencereler = CalibrationProbe.Windows(Kaynak(600), SpeedMode.Quality, DegiskenHarita(600, 20));

        Assert.True(pencereler.Count > 3, $"pencere sayisi {pencereler.Count}, tavan hala uc");
    }

    [Theory]
    [InlineData(2.0, SpeedMode.Quality)]
    [InlineData(8.0, SpeedMode.Quality)]
    [InlineData(60.0, SpeedMode.Quality)]
    [InlineData(600.0, SpeedMode.Quality)]
    [InlineData(600.0, SpeedMode.Fast)]
    public void SahneHaritasiYokkenYerlesimBugunkuYolunAynisi(double sure, SpeedMode hiz)
    {
        var pencereler = CalibrationProbe.Windows(Kaynak(sure), hiz);

        Assert.Equal(BugunkuBaslangiclar(sure, hiz), Baslangiclar(pencereler));
    }

    private static double[] BugunkuBaslangiclar(double sure, SpeedMode hiz)
    {
        const double pencereSaniye = 2.0;
        if (sure <= pencereSaniye * 1.5) return new[] { 0.0 };
        var kullanilabilir = Math.Max(0.0, sure - pencereSaniye);
        var sayi = sure < pencereSaniye * 6 || hiz == SpeedMode.Fast ? 2 : 3;
        return Enumerable.Range(0, sayi)
            .Select(i => Math.Round(kullanilabilir * (i + 0.5) / sayi, 3))
            .ToArray();
    }
}
