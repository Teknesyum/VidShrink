using System.Text.Json;
using VidShrink.Cli;
using VidShrink.Core;
using Xunit;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Yalan yok turunun kalan uclari: tavan ustu metni 42 dilde "asla teslim edilmez" demiyor ve
/// yer tutuculari dogru birime bagli; izleri koru acikken goruntu altyazi notu kutuyu acmayi
/// onermiyor; <c>--cikti x.webm</c> plan satirlari yapilan kodlamayla ayni. Her olcunun yaninda
/// davranisin degismedigi kol durur. Kanit <c>docs/olcumler/yalan-yok-kalan-uclar.md</c>.
/// </summary>
public sealed class YalanYokKalanUclarTests
{
    private sealed class Makine(params (string Codec, EncoderProbeState State)[] cevaplar) : IEncoderAvailability
    {
        private readonly Dictionary<string, EncoderProbeState> _cevaplar = cevaplar.ToDictionary(c => c.Codec, c => c.State, StringComparer.OrdinalIgnoreCase);

        public bool HasEncoder(string name) => _cevaplar.ContainsKey(name);
        public bool WorksAsEncoder(string codec) => _cevaplar.TryGetValue(codec, out var s) && s == EncoderProbeState.Working;
        public EncoderProbeState EncoderState(string codec) => _cevaplar.TryGetValue(codec, out var s) ? s : EncoderProbeState.NotWorking;
    }

    private static Makine Vp9(bool calisiyor) => new(("libx264", EncoderProbeState.Working), ("libx265", EncoderProbeState.Working),
        ("libsvtav1", EncoderProbeState.Working), ("libvpx-vp9", calisiyor ? EncoderProbeState.Working : EncoderProbeState.NotWorking));

    private static readonly SourceStream Aac320 = new(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 320_000, SampleRate: 48000);

    private static string? Deger(IReadOnlyList<string> args, string anahtar)
    {
        for (var i = 0; i < args.Count - 1; i++)
            if (args[i] == anahtar || args[i] == anahtar + ":0") return args[i + 1];
        return null;
    }

    private static CliDecision Karar(string uzanti, CliCodec kodek, bool vp9Calisiyor)
    {
        var info = Kaynak(600, Video, Aac320);
        return CliApp.Decide(new CliRequest
        {
            Command = CliCommand.Plan, Input = info.FilePath, TargetMb = 50, Codec = kodek, SkipMeasurement = true,
            PreferredLanguage = "en", Output = Path.Combine(Path.GetTempPath(), "yalan-yok", "x." + uzanti)
        }, info, null, null, Vp9(vp9Calisiyor));
    }

    /// <summary>Her dilde "asla/never" anlamini tasiyan sozcuk; tavan ustu metninde gecmemeli.</summary>
    private static readonly IReadOnlyDictionary<string, string[]> Asla = new Dictionary<string, string[]>
    {
        ["ar"] = ["أبدا", "أبدًا"], ["bg"] = ["никога"], ["bn"] = ["কখনো"], ["cs"] = ["nikdy"], ["da"] = ["aldrig"],
        ["de"] = ["nie", "niemals"], ["el"] = ["ποτέ"], ["en"] = ["never"], ["es"] = ["nunca"], ["et"] = ["kunagi"],
        ["fa"] = ["هرگز"], ["fi"] = ["koskaan"], ["fr"] = ["jamais"], ["he"] = ["לעולם"], ["hi"] = ["कभी"],
        ["hr"] = ["nikad"], ["hu"] = ["soha"], ["id"] = ["tidak pernah"], ["it"] = ["mai"], ["ja"] = ["決して"],
        ["ko"] = ["절대"], ["lt"] = ["niekada"], ["lv"] = ["nekad"], ["ms"] = ["tidak pernah"], ["nb"] = ["aldri"],
        ["nl"] = ["nooit"], ["pl"] = ["nigdy"], ["pt"] = ["nunca"], ["ro"] = ["niciodată"], ["ru"] = ["никогда"],
        ["sk"] = ["nikdy"], ["sl"] = ["nikoli"], ["sr"] = ["nikad"], ["sv"] = ["aldrig"], ["sw"] = ["kamwe"],
        ["ta"] = ["ஒருபோதும்"], ["th"] = ["เลย"], ["tr"] = ["asla", "hiçbir zaman"], ["uk"] = ["ніколи"], ["ur"] = ["کبھی"],
        ["vi"] = ["không bao giờ"], ["zh-Hans"] = ["绝不", "永远不"],
    };

    private static readonly HashSet<string> BosluksuzYazim = ["ja", "zh-Hans", "th"];

    private static string? AslaSozcugu(string dil, string metin)
        => Asla[dil].FirstOrDefault(s => BosluksuzYazim.Contains(dil)
            ? metin.Contains(s, StringComparison.Ordinal)
            : System.Text.RegularExpressions.Regex.IsMatch(metin, @"(?<![\p{L}\p{M}])" + System.Text.RegularExpressions.Regex.Escape(s) + @"(?![\p{L}\p{M}])",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase));

    private static IEnumerable<(string Dil, string Metin)> Metinler(string dosya, string anahtar)
    {
        foreach (var klasor in Directory.GetDirectories(Path.Combine(GirdiKanit.Root, "src", "VidShrink.App", "Locales")).OrderBy(k => k, StringComparer.Ordinal))
        {
            using var belge = JsonDocument.Parse(File.ReadAllText(Path.Combine(klasor, dosya)));
            yield return (Path.GetFileName(klasor), belge.RootElement.GetProperty(anahtar).GetString()!);
        }
    }

    /// <summary>
    /// Madde 1: tavan ustu metni 42 dilde "hedeften buyuk dosya asla teslim edilmez" demiyor; soylenen
    /// "buyuk sonuc kabul edilmedi". Olumsuz kontrol: eski lt metni kara listeye takiliyor.
    /// </summary>
    [Fact]
    public void TavanUstuMetniAslaDemez()
    {
        var metinler = Metinler("main.json", "main.run.over-ceiling").ToList();
        Assert.Equal(42, metinler.Count);
        Assert.Equal(42, Asla.Count);
        foreach (var (dil, metin) in metinler)
            Assert.True(AslaSozcugu(dil, metin) is null, $"{dil}: {metin}");

        Assert.Equal("niekada", AslaSozcugu("lt", EskiLt));
        Assert.Equal("never", AslaSozcugu("en", "a file larger than the target is never delivered"));
    }

    private const string EskiLt = "Po {0} bandymo(-ų) nepavyko nusileisti žemiau {1} MB tikslo (paskutinis rezultatas {2} MB). Failas neįrašytas, nes už tikslą didesnis failas niekada negrąžinamas.";

    private static readonly string[] Birimler = ["MB", "Mo", "МБ", "م.ب", "مگابایت", "メガ"];

    private static bool BirimeBagli(string metin, string yerTutucu)
    {
        var i = metin.IndexOf(yerTutucu, StringComparison.Ordinal);
        if (i < 0) return false;
        var sonra = metin[(i + yerTutucu.Length)..].TrimStart(' ', ' ', ' ');
        var once = metin[..i].TrimEnd(' ', ' ', ' ');
        return Birimler.Any(b => sonra.StartsWith(b, StringComparison.Ordinal) || once.EndsWith(b, StringComparison.Ordinal));
    }

    /// <summary>
    /// Madde 1 yan bulgusu: lt, nl ve sk {0} (hedef MB) ile {1} (deneme sayisi) yer degistirmisti,
    /// "3 MB hedefini 50 denemede" diyordu. {0} ve {2} birimin yaninda, {1} degil. Olumsuz kontrol:
    /// eski lt metni bu olcuye takiliyor.
    /// </summary>
    [Fact]
    public void TavanUstuYerTutuculariDogruBirimde()
    {
        foreach (var (dil, metin) in Metinler("main.json", "main.run.over-ceiling"))
        {
            Assert.True(BirimeBagli(metin, "{0}"), $"{dil} {{0}}: {metin}");
            Assert.True(BirimeBagli(metin, "{2}"), $"{dil} {{2}}: {metin}");
            Assert.False(BirimeBagli(metin, "{1}"), $"{dil} {{1}}: {metin}");
        }

        Assert.False(BirimeBagli(EskiLt, "{0}"));
        Assert.True(BirimeBagli(EskiLt, "{1}"));
    }

    /// <summary>
    /// Madde 2: izleri koru acik, kap MP4: goruntu altyazi notu kutuyu acmayi onermez, kabi soyler.
    /// Olumsuz kontrol: kutu kapaliyken eski not; kutu acik ve kap serbestken (MKV) not yok.
    /// </summary>
    [Fact]
    public void IzleriKoruAcikkenGoruntuAltyaziNotuKabiSoyler()
    {
        var info = Kaynak(600, Video, Aac320, new SourceStream(2, StreamKind.Subtitle, "hdmv_pgs_subtitle", "eng"));
        EncodePlan Plan(bool koru, OutputContainer? kap)
            => PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = 50, KeepAllTracks = koru, DeliveredContainer = kap }, null, null).Plan;

        var acikMp4 = Plan(true, OutputContainer.Mp4);
        var kapaliMp4 = Plan(false, OutputContainer.Mp4);
        var acikSerbest = Plan(true, null);

        Assert.Contains(StreamNote.ImageSubtitleDroppedByContainer, acikMp4.Streams!.Notes);
        Assert.DoesNotContain(StreamNote.ImageSubtitleDropped, acikMp4.Streams.Notes);
        Assert.DoesNotContain("turn on keep tracks", acikMp4.Reason, StringComparison.Ordinal);
        Assert.Contains(StreamNote.ImageSubtitleDropped, kapaliMp4.Streams!.Notes);
        Assert.DoesNotContain(StreamNote.ImageSubtitleDroppedByContainer, kapaliMp4.Streams.Notes);
        Assert.Equal(OutputContainer.Mkv, acikSerbest.Streams!.Container);
        Assert.DoesNotContain(StreamNote.ImageSubtitleDroppedByContainer, acikSerbest.Streams.Notes);
        Assert.DoesNotContain(StreamNote.ImageSubtitleDropped, acikSerbest.Streams.Notes);

        var slug = StreamNotes.Slug(StreamNote.ImageSubtitleDroppedByContainer);
        foreach (var (dil, metin) in Metinler("main.json", "main.reason.stream." + slug))
            Assert.False(string.IsNullOrWhiteSpace(metin), dil);
        Assert.DoesNotContain("--", CliText.ForLanguage("tr")["plan.stream." + slug], StringComparison.Ordinal);
    }

    /// <summary>
    /// Madde 3: <c>--cikti x.webm</c> otomatik kodekte plan MP4 icin kuruluyor, "Ses:" aac/copy
    /// diyordu, kodlama libopus yapiyordu. Plan artik WebM kabinda: satir ile <c>-c:a</c> ayni.
    /// Olumsuz kontrol: <c>x.mkv</c> zaten tutarliydi, tutarli kaliyor.
    /// </summary>
    [Theory]
    [InlineData("webm")]
    [InlineData("mkv")]
    [InlineData("mp4")]
    public void CiktiUzantisindaSesSatiriGercekKodek(string uzanti)
    {
        var karar = Karar(uzanti, CliCodec.Auto, vp9Calisiyor: true);

        Assert.Equal(Deger(karar.Arguments, "-c:a"), karar.Plan.AudioCodec);
        Assert.Equal(Deger(karar.Arguments, "-c:v"), karar.Plan.Codec);
        Assert.Equal("." + uzanti, Path.GetExtension(karar.OutputPath));
        Assert.Equal(StreamMapping.ContainerOf(karar.OutputPath), karar.Plan.Streams!.Container);
    }

    [Fact]
    public void WebmOtomatikKodekteOpusuPlanSoyler()
    {
        var karar = Karar("webm", CliCodec.Auto, vp9Calisiyor: true);

        Assert.Equal("libopus", karar.Plan.AudioCodec);
        Assert.Contains(StreamNote.WebmAudioOpus, karar.Plan.Streams!.Notes);
        Assert.True(CodecModel.FitsWebM(karar.Plan.Codec));
    }

    /// <summary>
    /// Madde 4: <c>--kodek vp9 --cikti x.webm</c>, vp9 calismiyor: libx264 webm'e yazilmaz, dosya
    /// .mp4 olur, not ("MP4") dogru olur ve ses satiri gercegi soyler. Olumsuz kontrol: vp9
    /// calisirken dosya .webm ve libvpx-vp9 kalir.
    /// </summary>
    [Fact]
    public void Vp9DusunceWebmAdliMp4Yazilmaz()
    {
        var bozuk = Karar("webm", CliCodec.Vp9, vp9Calisiyor: false);
        var saglam = Karar("webm", CliCodec.Vp9, vp9Calisiyor: true);

        Assert.Equal("libx264", Deger(bozuk.Arguments, "-c:v"));
        Assert.Equal(".mp4", Path.GetExtension(bozuk.OutputPath));
        Assert.Equal(OutputContainer.Mp4, bozuk.Plan.Streams!.Container);
        Assert.Contains(StreamNote.Vp9FellBackToMp4, bozuk.Plan.Streams.Notes);
        Assert.Equal(Deger(bozuk.Arguments, "-c:a"), bozuk.Plan.AudioCodec);
        Assert.Equal(bozuk.OutputPath, bozuk.Arguments[^1]);

        Assert.Equal("libvpx-vp9", Deger(saglam.Arguments, "-c:v"));
        Assert.Equal(".webm", Path.GetExtension(saglam.OutputPath));
        Assert.DoesNotContain(StreamNote.Vp9FellBackToMp4, saglam.Plan.Streams!.Notes);
    }

    /// <summary>
    /// Madde 4 yan bulgusu: <c>--cikti x.mkv</c> / <c>x.mov</c> ile vp9 dusunce not "cikti MP4"
    /// diyordu; cikti MKV/MOV. Artik kaptan bagimsiz not dusmeyi soyler. Olumsuz kontrol: webm
    /// isteginde (cikti gercekten MP4) eski not kaliyor.
    /// </summary>
    [Theory]
    [InlineData("mkv", OutputContainer.Mkv)]
    [InlineData("mov", OutputContainer.Mov)]
    public void Vp9DusunceSecilenKapMp4Denmez(string uzanti, OutputContainer kap)
    {
        var karar = Karar(uzanti, CliCodec.Vp9, vp9Calisiyor: false);

        Assert.Equal(kap, karar.Plan.Streams!.Container);
        Assert.Equal("." + uzanti, Path.GetExtension(karar.OutputPath));
        Assert.Contains(StreamNote.Vp9FellBack, karar.Plan.Streams.Notes);
        Assert.DoesNotContain(StreamNote.Vp9FellBackToMp4, karar.Plan.Streams.Notes);
        Assert.DoesNotContain("MP4", CliText.ForLanguage("en")["plan.stream." + StreamNotes.Slug(StreamNote.Vp9FellBack)], StringComparison.Ordinal);
    }

    /// <summary>
    /// Madde 4: uzanti degisince CLI bunu stderr'e yazar; uzanti degismezse satir yok.
    /// </summary>
    [Fact]
    public async Task WebmUzantisiDegisinceCliSoyler()
    {
        var metin = CliText.ForLanguage("tr");
        var bozuk = await Calistir(vp9Calisiyor: false);
        var saglam = await Calistir(vp9Calisiyor: true);

        Assert.Contains(metin.Format("output.not-webm", "libx264", ".mp4"), bozuk, StringComparison.Ordinal);
        Assert.DoesNotContain(metin.Format("output.not-webm", "libx264", ".mp4").Split("libx264")[0], saglam, StringComparison.Ordinal);
    }

    private static async Task<string> Calistir(bool vp9Calisiyor)
    {
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "test-ciktilari", "yalan-yok-kalan", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        try
        {
            var dosya = Path.Combine(klasor, "kaynak.mkv");
            await File.WriteAllTextAsync(dosya, "x");
            var info = Kaynak(600, Video, Aac320) with { FilePath = dosya };
            var servisler = new CliServices
            {
                MissingTool = () => null,
                Probe = (_, _) => Task.FromResult(info),
                Availability = () => Vp9(vp9Calisiyor),
                UserPresets = () => Array.Empty<PresetProfile>(),
            };
            var stderr = new StringWriter();
            var exit = await CliApp.RunAsync(["plan", dosya, "--olcumsuz", "--hedef", "50", "--kodek", "vp9", "--cikti", Path.Combine(klasor, "x.webm")],
                new StringWriter(), stderr, CliText.ForLanguage("tr"), servisler, CancellationToken.None);
            Assert.Equal(0, exit);
            return stderr.ToString();
        }
        finally
        {
            try { Directory.Delete(klasor, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
