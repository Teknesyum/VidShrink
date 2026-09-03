using System.Globalization;
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
        var kabaKesik = Harita(240, (80, 1.0e6), (80, 9.0e6), (80, 3.0e6));
        var sikKesik = Harita(240, (40, 1.0e6), (40, 1.0e6), (80, 9.0e6), (80, 3.0e6));

        Assert.Equal(SaniyeProfili(kabaKesik, 240), SaniyeProfili(sikKesik, 240));

        var kabaPencereler = CalibrationProbe.Windows(kaynak, SpeedMode.Quality, kabaKesik);
        var sikPencereler = CalibrationProbe.Windows(kaynak, SpeedMode.Quality, sikKesik);

        Assert.NotEqual(Baslangiclar(kabaPencereler), Baslangiclar(sikPencereler));
    }

    private static double[] SaniyeProfili(SceneMap harita, double sure)
    {
        var saniyeler = new double[(int)Math.Floor(sure)];
        foreach (var sahne in harita.Scenes)
        {
            var ilk = Math.Max(0, (int)Math.Floor(sahne.Start));
            var son = Math.Min(saniyeler.Length - 1, (int)Math.Ceiling(Math.Min(sure, sahne.End)) - 1);
            for (var i = ilk; i <= son; i++) saniyeler[i] = sahne.BitsPerSecond;
        }
        return saniyeler;
    }

    [Fact]
    public void KesimArgumaniAyriUzunlugunuTasiyabiliyor()
    {
        Assert.Equal(
            new[] { "-ss", "12.5", "-t", "3.25" },
            CalibrationProbe.TrimArgs(new SampleWindow(12.5, 3.25, 1.0)).ToArray());
    }

    [Fact]
    public void KesimArgumaniPencereninKendiUzunlugunuTasiyor()
    {
        var pencereler = CalibrationProbe.Windows(Kaynak(600), SpeedMode.Quality, DegiskenHarita(600, 20));

        foreach (var pencere in pencereler)
        {
            var argumanlar = CalibrationProbe.TrimArgs(pencere);
            Assert.Equal("-ss", argumanlar[0]);
            Assert.Equal(pencere.Start.ToString("0.###", CultureInfo.InvariantCulture), argumanlar[1]);
            Assert.Equal("-t", argumanlar[2]);
            Assert.Equal(pencere.Length.ToString("0.###", CultureInfo.InvariantCulture), argumanlar[3]);
        }
    }

    [Theory]
    [InlineData(8.0)]
    [InlineData(60.0)]
    [InlineData(600.0)]
    [InlineData(3600.0)]
    public void PencereUzunluklariBugunHepsiAyni(double sure)
    {
        var pencereler = CalibrationProbe.Windows(Kaynak(sure), SpeedMode.Quality, DegiskenHarita(sure, 20));
        var uzunluklar = pencereler.Select(p => p.Length).Distinct().ToArray();

        Assert.Single(uzunluklar);
    }

    [Theory]
    [InlineData(8.0, SpeedMode.Quality)]
    [InlineData(30.0, SpeedMode.Quality)]
    [InlineData(60.0, SpeedMode.Quality)]
    [InlineData(120.0, SpeedMode.Quality)]
    [InlineData(600.0, SpeedMode.Quality)]
    [InlineData(3600.0, SpeedMode.Quality)]
    [InlineData(600.0, SpeedMode.Fast)]
    [InlineData(3600.0, SpeedMode.Fast)]
    public void SahneHaritasiHicbirGirdideOrnekSayisiniDusurmuyor(double sure, SpeedMode hiz)
    {
        var eski = BugunkuBaslangiclar(sure, hiz).Length;
        var duz = CalibrationProbe.Windows(Kaynak(sure), hiz, DuzHarita(sure, 10)).Count;
        var degisken = CalibrationProbe.Windows(Kaynak(sure), hiz, DegiskenHarita(sure, 20)).Count;

        Assert.True(degisken >= eski, $"degisken kaynak {eski} yerine {degisken} ornek aliyor");
        Assert.True(duz >= ComplexityProbe.MinWindows, $"duz kaynak {duz} ornege dustu");
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
