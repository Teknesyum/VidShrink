using System.Reflection;
using System.Text.RegularExpressions;
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

    private static string CagriArgumanMetni(string kaynak, string cagri)
    {
        var yer = kaynak.IndexOf(cagri, StringComparison.Ordinal);
        Assert.True(yer >= 0, $"cagri yeri bulunamadi: {cagri}");
        var bas = yer + cagri.Length;
        var derinlik = 1;
        var son = bas;
        while (son < kaynak.Length && derinlik > 0)
        {
            if (kaynak[son] == '(') derinlik++;
            else if (kaynak[son] == ')') derinlik--;
            if (derinlik > 0) son++;
        }

        Assert.True(derinlik == 0, $"cagrinin kapanis parantezi bulunamadi: {cagri}");
        return kaynak[bas..son];
    }

    private static string[] UstDuzeyArgumanlar(string metin)
    {
        var parcalar = new List<string>();
        var derinlik = 0;
        var bas = 0;
        for (var i = 0; i < metin.Length; i++)
        {
            var c = metin[i];
            if (c is '(' or '[') derinlik++;
            else if (c is ')' or ']') derinlik--;
            else if (c == ',' && derinlik == 0)
            {
                parcalar.Add(metin[bas..i].Trim());
                bas = i + 1;
            }
        }

        parcalar.Add(metin[bas..].Trim());
        return parcalar.ToArray();
    }

    private static int Adet(string kaynak, string parca)
    {
        var sayi = 0;
        var yer = 0;
        while ((yer = kaynak.IndexOf(parca, yer, StringComparison.Ordinal)) >= 0)
        {
            sayi++;
            yer += parca.Length;
        }

        return sayi;
    }

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

    private static PlanOptions Secenekler(SpeedMode kip, double hedefMb = 25,
        CodecPreference kodek = CodecPreference.MaxCompression) => new()
    {
        TargetMb = hedefMb,
        Codec = kodek,
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
    /// T154 K1. Yukaridaki olcu dikis fonksiyonunu pimliyor, cagri yerini degil: T146
    /// denetcisi <c>CalibrationProbe.RunAsync</c>nin son argumanini <c>(SceneMap?)null</c>
    /// yapip derledi ve 44/44 yesil kaldi. Bu olcu cagri yerinin kendisini okur:
    /// <c>MainWindow.axaml.cs</c> icindeki tek <c>CalibrationProbe.RunAsync(</c> cagrisinin
    /// arguman listesini ayirir, altinci argumanin <c>&lt;kapi&gt;(_sceneMap)</c> seklinde
    /// oldugunu arar, o kapiyi yansimayla dolu bir denemeyle cagirir ve donen haritanin
    /// yerlesimi sabit izgaradan gercekten ayirdigini olcer. Arguman <c>null</c>lanirsa
    /// arguman metni artik bu sekle uymaz ve olcu duser.
    /// </summary>
    [Fact]
    public void KalibrasyonCagriYeriHaritayiYoklamayaGeciriyor()
    {
        var kaynak = File.ReadAllText(TipSources.WindowCodePath);
        Assert.Equal(1, Adet(kaynak, "CalibrationProbe.RunAsync("));

        var parcalar = UstDuzeyArgumanlar(CagriArgumanMetni(kaynak, "CalibrationProbe.RunAsync("));
        for (var i = 0; i < parcalar.Length; i++) _cikti.WriteLine($"arg[{i}] = {parcalar[i]}");
        Assert.Equal(6, parcalar.Length);

        var haritaArgumani = parcalar[5];
        var eslesme = Regex.Match(haritaArgumani, @"^(\w+)\(\s*_sceneMap\s*\)$");
        Assert.True(eslesme.Success,
            $"kalibrasyon cagri yeri _sceneMap'i gecirmiyor; gecirdigi arguman: {haritaArgumani}");

        var kapi = typeof(MainWindow).GetMethod(
            eslesme.Groups[1].Value, BindingFlags.Public | BindingFlags.Static);
        Assert.True(kapi is not null, $"cagri yerindeki kapi bulunamadi: {eslesme.Groups[1].Value}");

        var gecen = kapi!.Invoke(null, new object?[] { Deneme(DegiskenHarita(240, 8)) }) as SceneMap;
        Assert.True(gecen is not null, "cagri yerinin gecirdigi kapi dolu denemede null dondu");

        var kaynakBilgi = Kaynak();
        var haritali = Baslangiclar(CalibrationProbe.Windows(kaynakBilgi, SpeedMode.Quality, gecen));
        var sabit = Baslangiclar(CalibrationProbe.Windows(kaynakBilgi, SpeedMode.Quality, null));

        _cikti.WriteLine($"cagri yerinden gecen harita ile : [{string.Join(" ", haritali)}]");
        _cikti.WriteLine($"harita gecmezse                 : [{string.Join(" ", sabit)}]");

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

        Assert.Equal("libx265", plan.Codec);
        Assert.Equal(EncodeMode.TwoPass, plan.ModeEnum);

        var (ilk, son) = GecisOnAyarlari(kaynak, plan);
        _cikti.WriteLine($"kodek={plan.Codec} kip={plan.Mode} ilk={ilk} son={son}");

        Assert.Equal("slow", son);
        Assert.NotEqual(son, ilk);
    }

    /// <summary>
    /// K4. Turbo libx264'te acilmiyor: ikinci gecis birinci gecisin <c>weightp</c> ayarina
    /// uymak zorundadir, <c>veryfast</c> ile <c>slow</c> farkli deger kosar ve x264 ikinci
    /// gecisi hic acmaz. Olculdu: iki klipte de cikti sifir bayt.
    /// </summary>
    [Fact]
    public void Libx264HizliKiptedeTurboyaAcilmiyor()
    {
        var kaynak = Kaynak();
        var plan = PlanCalculator.Build(kaynak,
            Secenekler(SpeedMode.Fast, kodek: CodecPreference.Compatible), new DonanimsizMakine());

        Assert.Equal("libx264", plan.Codec);
        Assert.Equal(EncodeMode.TwoPass, plan.ModeEnum);

        var (ilk, son) = GecisOnAyarlari(kaynak, plan);
        _cikti.WriteLine($"kodek={plan.Codec} kip={plan.Mode} ilk={ilk} son={son}");

        Assert.Equal(son, ilk);
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
    /// K3. Uretimin kurdugu olcer haritayi gercekten tasiyor ve tasidigi harita en kotu
    /// birimi degistiriyor.
    /// </summary>
    [Fact]
    public void UretimOlceriHaritayiOlcumeTasiyor()
    {
        var olcer = MainWindow.ProbeMeter(MainWindow.QualityScenes(Deneme(KesikliHarita(10.0, 2.5, 5.5))));
        var tasinan = Assert.IsType<QualityMeasurement>(olcer).Scenes;
        Assert.NotNull(tasinan);

        var skorlar = DipliSkorlar();
        var haritali = QualityMeter.AggregateVmaf(skorlar, 60, 0, tasinan);
        var sabit = QualityMeter.AggregateVmaf(skorlar, 60, 0, null);

        _cikti.WriteLine($"olcerin tasidigi harita: {tasinan.Scenes.Count} sahne");
        _cikti.WriteLine($"uretim  : enkotu={haritali.WorstScene} at={haritali.WorstSceneStartSeconds}");
        _cikti.WriteLine($"sabit   : enkotu={sabit.WorstScene} at={sabit.WorstSceneStartSeconds}");

        Assert.NotEqual(sabit.WorstScene, haritali.WorstScene);
    }

    /// <summary>
    /// K5. Harita gelmediginde olcer bugunku yolda kaliyor: <c>ProbeMeter</c> yeni bir
    /// govde kurmuyor, <see cref="QualityMeasurement.Instance"/> tekilini donduruyor.
    /// (T154: govde tek yere indi, tekilin haritasi <c>null</c>.)
    /// </summary>
    [Fact]
    public void HaritaGelmedigindeOlcerBugunkuYoldaKaliyor()
    {
        var yok = new SceneMapAttempt(null, TimeSpan.Zero, SceneMapFallback.NoDuration, "sure yok");

        Assert.Same(QualityMeasurement.Instance, MainWindow.ProbeMeter(MainWindow.QualityScenes(yok)));
    }

    /// <summary>
    /// K3'un onkosulu. Olcere verilecek harita, olcum kosarken hazir olmali; T143 borcu
    /// tam buradan dogmustu: harita kalite olcumunden sonra kuruluyordu.
    /// </summary>
    [Fact]
    public void HaritaKaliteOlcumundenOnceKuruluyor()
    {
        var kaynak = File.ReadAllText(TipSources.WindowCodePath);
        var haritaYeri = kaynak.IndexOf("_sceneMap = await EncodeRunner.TryBuildSceneMapAsync", StringComparison.Ordinal);
        var olcumYeri = kaynak.IndexOf("await ProbeWithMeasuredQualityAsync(info, speed, ProbeMeter(", StringComparison.Ordinal);

        Assert.True(haritaYeri > 0, "harita kurulumu bulunamadi");
        Assert.True(olcumYeri > 0, "kalite olcumu cagrisi bulunamadi");
        Assert.True(haritaYeri < olcumYeri, "harita kalite olcumundan sonra kuruluyor; olcere verilecek harita o noktada yok");
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

    /// <summary>
    /// K5 gerileme tablosu 1: kalibrasyon yerlesimi. Her satirda eski (harita yok) ve yeni
    /// (uretimin verdigi harita) pencere baslangiclari yan yana.
    /// </summary>
    [Fact]
    public void TabloKalibrasyonYerlesimi()
    {
        var satirlar = new (string Ad, MediaInfo Kaynak, SpeedMode Kip, SceneMapAttempt Deneme)[]
        {
            ("240sn kalite / 8 sahne", Kaynak(240), SpeedMode.Quality, Deneme(DegiskenHarita(240, 8))),
            ("240sn hizli  / 8 sahne", Kaynak(240), SpeedMode.Fast, Deneme(DegiskenHarita(240, 8))),
            ("60sn hizli   / 6 sahne", Kaynak(60), SpeedMode.Fast, Deneme(DegiskenHarita(60, 6))),
            ("20sn kalite  / 4 sahne", Kaynak(20), SpeedMode.Quality, Deneme(DegiskenHarita(20, 4))),
            ("240sn kalite / harita yok", Kaynak(240), SpeedMode.Quality,
                new SceneMapAttempt(null, TimeSpan.Zero, SceneMapFallback.ScanFailed, "tarama yok"))
        };

        var degisen = 0;
        foreach (var (ad, kaynak, kip, deneme) in satirlar)
        {
            var eski = Baslangiclar(CalibrationProbe.Windows(kaynak, kip, null));
            var yeni = Baslangiclar(CalibrationProbe.Windows(kaynak, kip, MainWindow.CalibrationScenes(deneme)));
            var ayni = eski.SequenceEqual(yeni);
            if (!ayni) degisen++;
            _cikti.WriteLine($"{ad,-26} | eski [{string.Join(" ", eski)}] | yeni [{string.Join(" ", yeni)}] | {(ayni ? "AYNI" : "DEGISTI")}");
        }

        _cikti.WriteLine($"degisen satir: {degisen}/{satirlar.Length}");
        Assert.True(degisen > 0 && degisen < satirlar.Length);
    }

    /// <summary>
    /// K5 gerileme tablosu 2: kalite olcumunun en kotu birimi. Eski sabit iki saniyelik
    /// izgara ile yeni sahne sinirli birim yan yana.
    /// </summary>
    [Fact]
    public void TabloKaliteEnKotuBirim()
    {
        var duz = Enumerable.Repeat(100.0, 600).ToArray();
        var satirlar = new (string Ad, double[] Skorlar, SceneMapAttempt Deneme)[]
        {
            ("dip 2,5-5,5 / kesik 2,5;5,5", DipliSkorlar(), Deneme(KesikliHarita(10.0, 2.5, 5.5))),
            ("dip 2,5-5,5 / kesik 2;4;6;8", DipliSkorlar(), Deneme(KesikliHarita(10.0, 2.0, 4.0, 6.0, 8.0))),
            ("duz 100     / kesik 2,5;5,5", duz, Deneme(KesikliHarita(10.0, 2.5, 5.5))),
            ("dip 2,5-5,5 / harita yok", DipliSkorlar(),
                new SceneMapAttempt(null, TimeSpan.Zero, SceneMapFallback.NoDuration, "sure yok"))
        };

        var degisen = 0;
        foreach (var (ad, skorlar, deneme) in satirlar)
        {
            var eski = QualityMeter.AggregateVmaf(skorlar, 60, 0, null);
            var yeni = QualityMeter.AggregateVmaf(skorlar, 60, 0, MainWindow.QualityScenes(deneme));
            var ayni = eski.WorstScene == yeni.WorstScene
                       && eski.WorstSceneStartSeconds == yeni.WorstSceneStartSeconds
                       && eski.WorstSceneUnitSeconds == yeni.WorstSceneUnitSeconds;
            if (!ayni) degisen++;
            _cikti.WriteLine($"{ad,-27} | eski {eski.WorstScene}@{eski.WorstSceneStartSeconds}/{eski.WorstSceneUnitSeconds}sn | yeni {yeni.WorstScene}@{yeni.WorstSceneStartSeconds}/{yeni.WorstSceneUnitSeconds}sn | {(ayni ? "AYNI" : "DEGISTI")}");
        }

        _cikti.WriteLine($"degisen satir: {degisen}/{satirlar.Length}");
        Assert.True(degisen > 0 && degisen < satirlar.Length);
    }

    /// <summary>
    /// K5 gerileme tablosu 3: turbo ilk gecis. Eski (bayrak hic kurulmuyordu) ve yeni ilk
    /// gecis on ayari yan yana; son gecis her satirda degismeden kaliyor.
    /// </summary>
    [Fact]
    public void TabloTurboIlkGecis()
    {
        var kaynak = Kaynak();
        var satirlar = new (string Ad, SpeedMode Kip, CodecPreference Tercih, IEncoderAvailability? Makine)[]
        {
            ("hizli  / azami sikistirma", SpeedMode.Fast, CodecPreference.MaxCompression, new DonanimsizMakine()),
            ("hizli  / uyumlu", SpeedMode.Fast, CodecPreference.Compatible, new DonanimsizMakine()),
            ("kalite / azami sikistirma", SpeedMode.Quality, CodecPreference.MaxCompression, new DonanimsizMakine()),
            ("kalite / uyumlu", SpeedMode.Quality, CodecPreference.Compatible, new DonanimsizMakine()),
            ("hizli  / donanim var", SpeedMode.Fast, CodecPreference.Compatible, null)
        };

        var degisen = 0;
        foreach (var (ad, kip, tercih, makine) in satirlar)
        {
            var plan = PlanCalculator.Build(kaynak, Secenekler(kip, kodek: tercih), makine);
            var eski = FfmpegArguments.FirstPassPreset(plan.Codec, plan.Preset, false);
            var yeni = FfmpegArguments.FirstPassPreset(plan.Codec, plan.Preset, plan.TurboFirstPass);
            var ayni = eski == yeni;
            if (!ayni) degisen++;
            _cikti.WriteLine($"{ad,-25} | {plan.Codec,-11} {plan.Mode,-11} son={plan.Preset,-6} | eski ilk={eski,-9} | yeni ilk={yeni,-9} | {(ayni ? "AYNI" : "DEGISTI")}");
        }

        _cikti.WriteLine($"degisen satir: {degisen}/{satirlar.Length}");
        Assert.True(degisen > 0 && degisen < satirlar.Length);
    }
}
