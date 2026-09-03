using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Uc yetenek yazildi ve olculdu; bu olculer onlarin <b>uretim yolunda</b> kosup kosmadigini
/// sorar. Her olcu bir davranis farki arar: gecirilen haritanin urettigi yerlesim sabit
/// izgaradan farkli mi, gecirilen haritanin urettigi en kotu birim sabit pencereden farkli
/// mi, hizli kipte ilk gecis son gecisten hizli mi. "Harita gecirildi mi" sorusu tek basina
/// bir olcu degildir.
/// </summary>
public sealed class UretimYoluTests
{
    private readonly ITestOutputHelper _cikti;

    public UretimYoluTests(ITestOutputHelper cikti) => _cikti = cikti;

    private static MediaInfo Kaynak(double sureSaniye = 240, long boyut = 400L * 1024 * 1024) => new()
    {
        FilePath = "kaynak.mkv",
        FileSizeBytes = boyut,
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

    private static SceneMap DegiskenHarita(double sure, int sahneSayisi)
    {
        var adim = sure / sahneSayisi;
        var sahneler = new (double, double)[sahneSayisi];
        for (var i = 0; i < sahneSayisi; i++) sahneler[i] = (adim, i % 2 == 0 ? 0.6e6 : 18.0e6);
        return Harita(sure, sahneler);
    }

    private static SceneMapAttempt Deneme(SceneMap harita)
        => new(harita, TimeSpan.FromSeconds(1), SceneMapFallback.None, string.Empty);

    private static SceneMap KesikliHarita(double sure, params double[] kesikler)
        => SceneMap.Build(
            sure,
            kesikler.Select(k => new SceneScore(k, 1.0)).ToArray(),
            SceneMap.DefaultThreshold,
            Array.Empty<ProbeFrame>());

    private static double[] Baslangiclar(IReadOnlyList<SampleWindow> pencereler)
        => pencereler.Select(p => Math.Round(p.Start, 3)).ToArray();

    private static double[] DipliSkorlar()
    {
        var skorlar = Enumerable.Repeat(100.0, 600).ToArray();
        for (var i = 150; i < 330; i++) skorlar[i] = 40.0;
        return skorlar;
    }

    private sealed class DonanimsizMakine : IEncoderAvailability
    {
        public bool HasEncoder(string name) => !CodecModel.IsHardware(name);

        public bool WorksAsEncoder(string codec) => !CodecModel.IsHardware(codec);

        public EncoderProbeState EncoderState(string codec)
            => CodecModel.IsHardware(codec) ? EncoderProbeState.NotWorking : EncoderProbeState.Working;
    }

    private static PlanOptions Secenekler(SpeedMode kip, double hedefMb = 25) => new()
    {
        TargetMb = hedefMb,
        Codec = CodecPreference.Compatible,
        SpeedMode = kip
    };

    private static string OnAyar(IReadOnlyList<string> args)
    {
        var yer = args.IndexOf("-preset");
        return yer < 0 ? "<yok>" : args[yer + 1];
    }

    private static (string Ilk, string Son) GecisOnAyarlari(MediaInfo kaynak, EncodePlan plan)
        => (OnAyar(FfmpegArguments.Build(kaynak, plan, "cikti.mp4", 1, "gunluk")),
            OnAyar(FfmpegArguments.Build(kaynak, plan, "cikti.mp4", 2, "gunluk")));

    /// <summary>
    /// K1(a) / K2. Uretimin kalibrasyon yoklamasina verdigi harita
    /// (<see cref="MainWindow.CalibrationScenes"/>) yerlesimi gercekten degistiriyor mu.
    /// Kiyas iki yerlesim listesi arasindadir, haritanin <c>null</c> olup olmadigi
    /// arasinda degil.
    /// </summary>
    [Fact]
    public void KalibrasyonYerlesimiUretimdeSahneHaritasiniGoruyor()
    {
        var kaynak = Kaynak();
        var uretimHaritasi = MainWindow.CalibrationScenes(Deneme(DegiskenHarita(240, 8)));

        var haritali = Baslangiclar(CalibrationProbe.Windows(kaynak, SpeedMode.Quality, uretimHaritasi));
        var sabit = Baslangiclar(CalibrationProbe.Windows(kaynak, SpeedMode.Quality, null));

        _cikti.WriteLine($"uretim  : [{string.Join(" ", haritali)}]");
        _cikti.WriteLine($"sabit   : [{string.Join(" ", sabit)}]");

        Assert.NotEqual(sabit, haritali);
    }

    /// <summary>
    /// K1(b) / K3. Uretimin kalite olcerine verdigi harita
    /// (<see cref="MainWindow.QualityScenes"/>) en kotu birimi gercekten degistiriyor mu.
    /// Skorlarda 2,5-5,5 sn arasinda bir dip var; sabit iki saniyelik izgara dibi
    /// komsulariyla seyreltiyor, sahne siniri onu yalitiyor.
    /// </summary>
    [Fact]
    public void KaliteOlcumuUretimdeSahneHaritasiniGoruyor()
    {
        var skorlar = DipliSkorlar();
        var uretimHaritasi = MainWindow.QualityScenes(Deneme(KesikliHarita(10.0, 2.5, 5.5)));

        var haritali = QualityMeter.AggregateVmaf(skorlar, 60, 0, uretimHaritasi);
        var sabit = QualityMeter.AggregateVmaf(skorlar, 60, 0, null);

        _cikti.WriteLine($"uretim  : enkotu={haritali.WorstScene} at={haritali.WorstSceneStartSeconds} birim={haritali.WorstSceneUnitSeconds}");
        _cikti.WriteLine($"sabit   : enkotu={sabit.WorstScene} at={sabit.WorstSceneStartSeconds} birim={sabit.WorstSceneUnitSeconds}");

        Assert.NotEqual(sabit.WorstScene, haritali.WorstScene);
    }

    /// <summary>
    /// K1(c) / K4. Hizli kipte uretilen planin ilk gecisi son gecisin on ayarini
    /// kosmamali. Kiyas iki ffmpeg argumani arasindadir; plandaki bayrak okunmuyor.
    /// </summary>
    [Fact]
    public void HizliKipteIlkGecisSonGecistenHizliKosuyor()
    {
        var kaynak = Kaynak();
        var plan = PlanCalculator.Build(kaynak, Secenekler(SpeedMode.Fast), new DonanimsizMakine());

        Assert.Equal("libx264", plan.Codec);
        Assert.Equal(EncodeMode.TwoPass, plan.ModeEnum);

        var (ilk, son) = GecisOnAyarlari(kaynak, plan);
        _cikti.WriteLine($"kodek={plan.Codec} kip={plan.Mode} ilk={ilk} son={son}");

        Assert.Equal("slow", son);
        Assert.NotEqual(son, ilk);
    }

    /// <summary>
    /// K5. Kalite kipi degismedi: ilk gecis son gecisin on ayarini kosmaya devam ediyor.
    /// </summary>
    [Fact]
    public void KaliteKipindeIlkGecisSonGecisinOnAyarindaKaliyor()
    {
        var kaynak = Kaynak();
        var plan = PlanCalculator.Build(kaynak, Secenekler(SpeedMode.Quality), new DonanimsizMakine());

        Assert.Equal(EncodeMode.TwoPass, plan.ModeEnum);

        var (ilk, son) = GecisOnAyarlari(kaynak, plan);
        _cikti.WriteLine($"kodek={plan.Codec} kip={plan.Mode} ilk={ilk} son={son}");

        Assert.Equal(son, ilk);
    }

    /// <summary>
    /// K5. Harita gelmediginde kalibrasyon bugunku esit arali yerlesimde kaliyor: yedek
    /// kol silinmedi.
    /// </summary>
    [Fact]
    public void HaritaGelmedigindeKalibrasyonSabitIzgaradaKaliyor()
    {
        var kaynak = Kaynak();
        var yok = MainWindow.CalibrationScenes(new SceneMapAttempt(null, TimeSpan.Zero, SceneMapFallback.ScanFailed, "tarama yok"));

        Assert.Equal(
            Baslangiclar(CalibrationProbe.Windows(kaynak, SpeedMode.Quality, null)),
            Baslangiclar(CalibrationProbe.Windows(kaynak, SpeedMode.Quality, yok)));
    }
}
