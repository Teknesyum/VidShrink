using System.Text;
using Avalonia;
using VidShrink.App.Themes;
using VidShrink.Player;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Hipersürüş pinleri: yayın anahtarları, perdesiz açılış ve paletin kısa
/// devresi. Üçü de ölçülmüş bir gecikmeye karşılık geliyor ve üçü de sessizce geri
/// alınabilir — bir satır silinince kimse fark etmez, açılış yine uzar. Ölçümün kendisi
/// <c>docs/olcumler/acilis-hizi.md</c>'de.
/// </summary>
public sealed class HipersurusTests
{
    private readonly ITestOutputHelper _cikti;

    public HipersurusTests(ITestOutputHelper cikti) => _cikti = cikti;

    private static string Oku(params string[] parcalar)
        => File.ReadAllText(Path.Combine(new[] { TipSources.Root }.Concat(parcalar).ToArray()));

    /// <summary>
    /// C1. İki yayın da önceden derlenmiş kodla çıkıyor. Anahtarlar kalkarsa açılışta bütün
    /// IL yeniden JIT'lenir ve ölçülen kazanç geri verilir.
    /// </summary>
    [Fact]
    public void YayinOncedenDerlenmisKodlaCikiyor()
    {
        var uygulama = Oku("src", "VidShrink.App", "VidShrink.App.csproj");
        var baslatici = Oku("src", "VidShrink.Launcher", "VidShrink.Launcher.csproj");

        foreach (var (ad, metin) in new[] { ("uygulama", uygulama), ("baslatici", baslatici) })
        {
            Assert.True(metin.Contains("<PublishReadyToRun>true</PublishReadyToRun>", StringComparison.Ordinal),
                $"{ad}: PublishReadyToRun yok");
            Assert.True(metin.Contains("<TieredPGO>true</TieredPGO>", StringComparison.Ordinal),
                $"{ad}: TieredPGO yok");
        }

        Assert.DoesNotContain("<PublishAot>true</PublishAot>", uygulama, StringComparison.Ordinal);
        Assert.Contains("<PropertyGroup Condition=\"'$(RuntimeIdentifier)' == 'win-x64'\">\r\n    <PublishReadyToRunComposite>true</PublishReadyToRunComposite>", uygulama.ReplaceLineEndings("\r\n"), StringComparison.Ordinal);
    }

    /// <summary>
    /// F1. Olağan açılışta perde yok: başlatıcı uygulamayı bekletmeden doğuruyor, uygulamada
    /// perdeyi kaldıran yol da yok. Bakım sessiz koşuyor: dosyayla açılışta ve <c>--bakim</c>
    /// kipinde panel hiç kurulmuyor. Uygulama doğmadan önceki yolda bekleme yok; tek bekleme
    /// elle güncellemede eski sürecin kapanması.
    ///
    /// <para>G3 (17 Eylül 2026): olağan yolda da eşikli sayaç kuruluyor, koşulsuz. 400 ms'yi
    /// aşmayan bakım panelsiz geçiyor; <c>ResumePending</c>'in yüzlerce MB'lık taşıması artık
    /// boş ekranda geçmiyor.</para>
    /// </summary>
    [Fact]
    public void OlaganAcilistaPerdeYok()
    {
        var baslatici = Oku("src", "VidShrink.Launcher", "Program.cs");
        var uygulama = Oku("src", "VidShrink.App", "MainWindow.axaml.cs");

        Assert.False(File.Exists(Path.Combine(TipSources.Root, "src", "VidShrink.Launcher", "AcilisPerdesi.cs")));
        Assert.False(File.Exists(Path.Combine(TipSources.Root, "src", "VidShrink.App", "AcilisPerdesi.cs")));
        Assert.DoesNotContain("AcilisPerdesi", baslatici, StringComparison.Ordinal);
        Assert.DoesNotContain("BekleVeKapat", baslatici, StringComparison.Ordinal);
        Assert.DoesNotContain("AcilisPerdesi", uygulama, StringComparison.Ordinal);
        Assert.DoesNotContain("PerdeyiIzle", uygulama, StringComparison.Ordinal);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(baslatici, @"StartApp\(executable").Count);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(baslatici, @"SplashGate\.Arm\("));
        Assert.Contains("using (SplashGate.Arm(progress))", baslatici, StringComparison.Ordinal);
        var kapi = baslatici.IndexOf("using (SplashGate.Arm(progress))", StringComparison.Ordinal);
        Assert.True(kapi < baslatici.IndexOf("UpdateStage.ResumePending(appDirectory)", StringComparison.Ordinal));

        foreach (var giris in new[] { "if (!updateNow && args.Length > 0 && File.Exists(args[0]))", "args[0] == LauncherUpdate.MaintenanceArgument" })
        {
            var kol = baslatici[baslatici.IndexOf(giris, StringComparison.Ordinal)..];
            kol = kol[..kol.IndexOf("return 0;", StringComparison.Ordinal)];
            Assert.DoesNotContain("SplashGate", kol, StringComparison.Ordinal);
            Assert.DoesNotContain("InstallProgress", kol, StringComparison.Ordinal);
            Assert.DoesNotContain("ResumePending", kol, StringComparison.Ordinal);
            Assert.Contains("Maintain(baseDirectory, appDirectory, previousVersion);", kol, StringComparison.Ordinal);
            Assert.True(baslatici.IndexOf(giris, StringComparison.Ordinal) < kapi);
        }

        var bakimKolu = baslatici[baslatici.IndexOf("args[0] == LauncherUpdate.MaintenanceArgument", StringComparison.Ordinal)..];
        Assert.DoesNotContain("StartApp(", bakimKolu[..bakimKolu.IndexOf("return 0;", StringComparison.Ordinal)], StringComparison.Ordinal);
        var bakim = baslatici[baslatici.IndexOf("private static void Maintain(", StringComparison.Ordinal)..];
        bakim = bakim[..bakim.IndexOf("private static void StartCommitter(", StringComparison.Ordinal)];
        Assert.Contains("StartCommitter(baseDirectory);", bakim, StringComparison.Ordinal);
        Assert.DoesNotContain("ResumePending", bakim, StringComparison.Ordinal);
        Assert.DoesNotContain("StartApp(", bakim, StringComparison.Ordinal);

        var main = baslatici[baslatici.IndexOf("private static int Main(", StringComparison.Ordinal)..];
        var dogumOncesi = main[..main.LastIndexOf("StartApp(executable", StringComparison.Ordinal)];
        foreach (var bekleme in new[] { "Thread.Sleep", "Task.Delay", "SpinWait", ".Wait(", ".Join(" })
            Assert.DoesNotContain(bekleme, dogumOncesi, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(dogumOncesi, @"WaitForExit\("));
        Assert.True(dogumOncesi.IndexOf("WaitForExit(", StringComparison.Ordinal)
                    < dogumOncesi.IndexOf("if (!File.Exists(executable))", StringComparison.Ordinal));
        Assert.Matches(@"if \(updateNow\)\s*\{\s*WaitForExit\(", dogumOncesi);
    }

    /// <summary>
    /// C3. Yürürlükteki paleti yeniden seçmek hiçbir iş yapmıyor: sözlüğün ilk sırası aynı
    /// nesne kalıyor. Gerçek geçişte değişiyor — negatif kontrol o. Ölçülen payı 194,1 ms ve
    /// açılışta bu hal kuraldı.
    /// </summary>
    [Fact]
    public void AyniPaletiYenidenSecmek_HicbirSeyYapmiyor()
    {
        var (ayni, degisen, dokum) = AppHost.Run(() =>
        {
            PaletteCatalog.Use(PaletteCatalog.Default);
            var once = Application.Current!.Resources.MergedDictionaries[0];

            PaletteCatalog.Use(PaletteCatalog.Default);
            var tekrar = Application.Current!.Resources.MergedDictionaries[0];

            var oteki = PaletteCatalog.Names.First(ad => ad != PaletteCatalog.Default);
            PaletteCatalog.Use(oteki);
            var sonra = Application.Current!.Resources.MergedDictionaries[0];

            PaletteCatalog.Use(PaletteCatalog.Default);

            var yazi = new StringBuilder()
                .AppendLine($"ilk         = {once.GetHashCode()}")
                .AppendLine($"tekrar ayni = {tekrar.GetHashCode()}")
                .AppendLine($"gercek gecis= {sonra.GetHashCode()}")
                .ToString();

            return (ReferenceEquals(once, tekrar), ReferenceEquals(once, sonra), yazi);
        });

        _cikti.WriteLine(dokum);
        Assert.True(ayni, "Ayni palet yeniden secildi ve sozluk yine degistirildi:\n" + dokum);
        Assert.False(degisen, "Gercek palet gecisi sozlugu degistirmiyor:\n" + dokum);
    }

    /// <summary>
    /// Kaydedici sekmesinin icerigi XAML'de degil, ilk secimde kuruluyor. Sonda
    /// <c>InitializeComponent</c>'in 89,4 ms'ini bu agacta olctu; XAML'e geri konursa
    /// olculen pay geri gelir ve bu olcu kirmizi olur.
    /// </summary>
    [Fact]
    public void KaydediciSekmesi_IlkSecimde_Kuruluyor()
    {
        var xaml = Oku("src", "VidShrink.App", "MainWindow.axaml");
        Assert.DoesNotContain("<recorder:RecorderView", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PageRecorder\"", xaml, StringComparison.Ordinal);

        var tembel = Oku("src", "VidShrink.App", "MainWindow.TembelSekme.cs");
        Assert.Contains("_recorderPane = new RecorderView();", tembel, StringComparison.Ordinal);
        Assert.Contains("PageRecorder.Content = _recorderPane;", tembel, StringComparison.Ordinal);
        Assert.Contains("TryFindResource(\"SectionMargin\"", tembel, StringComparison.Ordinal);

        var pencere = Oku("src", "VidShrink.App", "MainWindow.axaml.cs");
        Assert.Contains("KaydediciSekmesiSecildi()", pencere, StringComparison.Ordinal);
        Assert.DoesNotContain("RecorderPane.OpenInShrink = OpenInShrinkAsync;", pencere, StringComparison.Ordinal);
    }

    /// <summary>
    /// Oynatici motoru yazilimsal cozmede kaliyor. hwdec=auto-copy olculdu ve motor
    /// adimini 272,8 ms'den 297,0 ms'ye cikardi; geri alindi. Bu pim geri gelmesini
    /// olcumsuz engelliyor.
    /// </summary>
    [Fact]
    public void OynaticiYazilimsalCozuyor_DonanimOlculdu_GeriAlindi()
    {
        var oynatici = Oku("src", "VidShrink.App", "Playback", "PlayerView.axaml.cs");
        Assert.Contains("EngineFactory { get; set; } = () => new MpvEngine();", oynatici, StringComparison.Ordinal);
        Assert.DoesNotContain("HardwareDecoding.AutoCopy", oynatici, StringComparison.Ordinal);

        var karsilastirma = Oku("src", "VidShrink.App", "Playback", "EngineComparisonFrameSource.cs");
        Assert.DoesNotContain("HardwareDecoding.AutoCopy", karsilastirma, StringComparison.Ordinal);

        var ses = Oku("src", "VidShrink.App", "Playback", "PreviewAudio.cs");
        Assert.DoesNotContain("HardwareDecoding.AutoCopy", ses, StringComparison.Ordinal);
    }

    /// <summary>
    /// Olcerin adim listesi ile XAML'deki iz noktalari birbirini tutuyor. Sonda aracin
    /// kendisi: bir isaret eklenip listeye yazilmazsa sutun sessizce kaybolur.
    /// </summary>
    [Fact]
    public void XamlIzNoktalari_OlcerinListesiyle_Ayni()
    {
        var xaml = Oku("src", "VidShrink.App", "MainWindow.axaml");
        var betik = Oku("tools", "acilis-hizi", "olcum.ps1");

        var isaretler = System.Text.RegularExpressions.Regex.Matches(xaml, @"AcilisIsareti\.Ad=""(?<ad>[^""]+)""")
            .Select(e => e.Groups["ad"].Value).ToArray();

        Assert.NotEmpty(isaretler);
        foreach (var ad in isaretler)
            Assert.Contains("'" + ad + "'", betik, StringComparison.Ordinal);
    }

    /// <summary>
    /// D3. Onden isitilan motor ayni yolu isteyen ilk cagirana bir kez gecer; ikinci
    /// cagiri bos alir ve kendi motorunu kurar. Devralinmayan motor <c>Birak</c> ile
    /// atilir, yoksa libmpv ornegi acilis boyunca asili kalir.
    /// </summary>
    [Fact]
    public async Task AcilisMotoru_BirKezDevrediliyor_KalaniAtiliyor()
    {
        var motor = new SahteAcilisMotoru();
        VidShrink.App.Playback.AcilisMotoru.Isit(@"C:\klip\a.mp4", () => motor);

        var gorev = VidShrink.App.Playback.AcilisMotoru.Devral(@"c:\KLIP\A.MP4");
        Assert.NotNull(gorev);
        Assert.Same(motor, await gorev!);
        Assert.Equal(1, motor.Acilis);
        Assert.Null(VidShrink.App.Playback.AcilisMotoru.Devral(@"C:\klip\a.mp4"));

        var bosta = new SahteAcilisMotoru();
        VidShrink.App.Playback.AcilisMotoru.Isit(@"C:\klip\b.mp4", () => bosta);
        Assert.Null(VidShrink.App.Playback.AcilisMotoru.Devral(@"C:\klip\baska.mp4"));
        VidShrink.App.Playback.AcilisMotoru.Birak();

        for (var i = 0; i < 100 && !bosta.Atildi; i++) await Task.Delay(20);
        Assert.True(bosta.Atildi, "devralinmayan motor atilmadi");
    }

    private sealed class SahteAcilisMotoru : IPlaybackEngine
    {
        public int Acilis { get; private set; }

        public bool Atildi { get; private set; }

        public string Name => "sahte-acilis";

        public bool IsOpen => Acilis > 0;

        public double DurationSeconds => 0;

        public bool HasAudio => false;

        public bool IsPaused => true;

        public bool EndReached => false;

        public double PositionSeconds => 0;

        public double AudioVideoOffsetSeconds => double.NaN;

        public long FramesRendered => 0;

        public event EventHandler<PlaybackFault>? Faulted
        {
            add { }
            remove { }
        }

        public Task OpenAsync(string path, CancellationToken ct = default)
        {
            Acilis++;
            return Task.CompletedTask;
        }

        public void Play() { }

        public void Pause() { }

        public Task<SeekResult> SeekAsync(double seconds, SeekPrecision precision, CancellationToken ct = default)
            => Task.FromResult(new SeekResult(SeekOutcome.Failed, 0));

        public bool TryCopyLatest(ref long seen, FrameCopy copy) => false;

        public void Dispose() => Atildi = true;
    }
}
