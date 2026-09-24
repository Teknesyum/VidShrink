using VidShrink.App;
using VidShrink.Cli;
using VidShrink.Core;
using Xunit;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Programin kullaniciya soyledigi ile yaptigi ayni: kodek secimi, yedege dusme tavsiyesi,
/// kap degisimi ve iz notlari. Her olcunun yaninda davranisin degismedigi kol durur
/// (olumsuz kontrol).
/// </summary>
public sealed class SoylenenYapilanTests : IDisposable
{
    private readonly string _klasor = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        ".calisma", "test-ciktilari", "soylenen-yapilan", Guid.NewGuid().ToString("N")[..8]);

    public SoylenenYapilanTests() => Directory.CreateDirectory(_klasor);

    public void Dispose()
    {
        try { Directory.Delete(Path.GetFullPath(_klasor), true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private sealed class Makine(params (string Codec, EncoderProbeState State)[] cevaplar) : IEncoderAvailability
    {
        private readonly Dictionary<string, EncoderProbeState> _cevaplar = cevaplar.ToDictionary(c => c.Codec, c => c.State, StringComparer.OrdinalIgnoreCase);

        public bool HasEncoder(string name) => _cevaplar.ContainsKey(name);
        public bool WorksAsEncoder(string codec) => _cevaplar.TryGetValue(codec, out var s) && s == EncoderProbeState.Working;
        public EncoderProbeState EncoderState(string codec) => _cevaplar.TryGetValue(codec, out var s) ? s : EncoderProbeState.NotWorking;
    }

    private static readonly (string, EncoderProbeState)[] HerDonanimCalisiyor =
    {
        ("av1_nvenc", EncoderProbeState.Working), ("hevc_nvenc", EncoderProbeState.Working), ("h264_nvenc", EncoderProbeState.Working),
        ("libx264", EncoderProbeState.Working), ("libx265", EncoderProbeState.Working), ("libsvtav1", EncoderProbeState.Working)
    };

    private static MediaInfo Kaynak10Dk(int sesIzi = 1) => Kaynak(600, new[] { Video }
        .Concat(Enumerable.Range(1, sesIzi).Select(i => new SourceStream(i, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000, SampleRate: 48000)))
        .ToArray());

    private static EncodePlan Plan(PlanOptions options, IEncoderAvailability? makine = null, MediaInfo? info = null)
        => PlanCalculator.BuildDetailed(info ?? Kaynak10Dk(), options, null, makine).Plan;

    private static bool DusmeNotuVar(EncodePlan plan) => plan.ReasonCodes.Any(n => n.Code == ReasonCode.EncoderFallback);

    /// <summary>
    /// 22: hizli modda acikca H.264 isteyen H.264 donanimina gider, yedege dusme notu yok.
    /// Otomatik ayni makinede av1_nvenc'i secer (olumsuz kontrol).
    /// </summary>
    [Fact]
    public void HizliModUyumluSecimiH264DonanimindaKalir()
    {
        var makine = new Makine(HerDonanimCalisiyor);
        var uyumlu = Plan(new PlanOptions { TargetMb = 50, Codec = CodecPreference.Compatible, SpeedMode = SpeedMode.Fast }, makine);
        var otomatik = Plan(new PlanOptions { TargetMb = 50, Codec = CodecPreference.Auto, SpeedMode = SpeedMode.Fast }, makine);

        Assert.Equal("h264_nvenc", uyumlu.Codec);
        Assert.False(DusmeNotuVar(uyumlu), uyumlu.Reason);
        Assert.Equal("av1_nvenc", otomatik.Codec);
    }

    /// <summary>22: H.264 donanimi yoksa uyumlu hizli mod libx264'e duser ve bunu soyler.</summary>
    [Fact]
    public void HizliModUyumluH264DonanimiYoksaLibx264eDuserVeSoyler()
    {
        var makine = new Makine(("av1_nvenc", EncoderProbeState.Working), ("h264_nvenc", EncoderProbeState.NotWorking), ("libx264", EncoderProbeState.Working));
        var plan = Plan(new PlanOptions { TargetMb = 50, Codec = CodecPreference.Compatible, SpeedMode = SpeedMode.Fast }, makine);

        Assert.Equal("libx264", plan.Codec);
        var not = Assert.Single(plan.ReasonCodes, n => n.Code == ReasonCode.EncoderFallback);
        Assert.Equal("h264_nvenc", not.RequestedCodec);
    }

    /// <summary>22: CLI <c>--hizli --kodek h264</c> plan satirinda H.264 donanimi yazar; yalniz <c>--hizli</c> av1_nvenc yazar.</summary>
    [Fact]
    public async Task CliHizliH264PlanindaH264DonanimiYazar()
    {
        var h264 = await PlanMetni("pcm_s16le", "--hizli", "--kodek", "h264");
        var otomatik = await PlanMetni("pcm_s16le", "--hizli");

        Assert.Contains("h264_nvenc", h264, StringComparison.Ordinal);
        Assert.DoesNotContain("av1_nvenc", h264, StringComparison.Ordinal);
        Assert.Contains("av1_nvenc", otomatik, StringComparison.Ordinal);
    }

    /// <summary>
    /// 20: kullanici donanim yolunu elle sabitleyince tek not yol notudur; motorun yazilim
    /// tercihi "kullanilamadi" diye ikinci, yalanci bir dusme notu uretmez.
    /// </summary>
    [Fact]
    public void ElleDonanimYoluYalanciDusmeNotuUretmez()
    {
        var makine = new Makine(HerDonanimCalisiyor);
        var plan = Plan(new PlanOptions { TargetMb = 50, Codec = CodecPreference.Compatible, EncoderPath = EncoderPathOverride.Hardware }, makine);

        Assert.Equal("h264_nvenc", plan.Codec);
        Assert.Contains(plan.ReasonCodes, n => n.Code == ReasonCode.ManualEncoderPathOverride);
        Assert.False(DusmeNotuVar(plan), plan.Reason);
    }

    /// <summary>20: elle yazilim yolu da yalanci dusme notu uretmez; hizli otomatik yol notsuz donanimda kalir (olumsuz kontrol).</summary>
    [Fact]
    public void ElleYazilimYoluYalanciDusmeNotuUretmez()
    {
        var makine = new Makine(HerDonanimCalisiyor);
        var yazilim = Plan(new PlanOptions { TargetMb = 50, Codec = CodecPreference.Auto, SpeedMode = SpeedMode.Fast, EncoderPath = EncoderPathOverride.Software }, makine);
        var otomatik = Plan(new PlanOptions { TargetMb = 50, Codec = CodecPreference.Auto, SpeedMode = SpeedMode.Fast }, makine);

        Assert.False(CodecModel.IsHardware(yazilim.Codec));
        Assert.Contains(yazilim.ReasonCodes, n => n.Code == ReasonCode.ManualEncoderPathOverride);
        Assert.False(DusmeNotuVar(yazilim), yazilim.Reason);
        Assert.Equal("av1_nvenc", otomatik.Codec);
        Assert.False(DusmeNotuVar(otomatik));
    }

    /// <summary>20: donanim yolu istendi ama donanim yok: yol notu karsilanmadi der, dusme notu eklenmez.</summary>
    [Fact]
    public void KarsilanmayanDonanimYoluTekNotBirakir()
    {
        var makine = new Makine(("libx264", EncoderProbeState.Working), ("h264_nvenc", EncoderProbeState.NotWorking));
        var plan = Plan(new PlanOptions { TargetMb = 50, Codec = CodecPreference.Compatible, EncoderPath = EncoderPathOverride.Hardware }, makine);

        Assert.Equal("libx264", plan.Codec);
        Assert.Contains(plan.ReasonCodes, n => n.Code == ReasonCode.ManualEncoderPathUnmet);
        Assert.False(DusmeNotuVar(plan), plan.Reason);
    }

    public static TheoryData<EncoderProbeState, EncoderFallbackCause> DonanimdanDonanima => new()
    {
        { EncoderProbeState.NotWorking, EncoderFallbackCause.NotWorking },
        { EncoderProbeState.Unmeasured, EncoderFallbackCause.NotMeasured },
    };

    /// <summary>
    /// 19/21: av1_nvenc calismiyor ya da olculmedi, hevc_nvenc calisiyor: tavsiye "yazilima
    /// dusuldu" demez, donanimin surdugunu soyler. Hic donanim yoksa yazilim cumlesi kalir
    /// (olumsuz kontrol).
    /// </summary>
    [Theory]
    [MemberData(nameof(DonanimdanDonanima))]
    public void DonanimdanDonanimaDusmeYazilimDemez(EncoderProbeState av1Durumu, EncoderFallbackCause beklenenSebep)
    {
        var makine = new Makine(("av1_nvenc", av1Durumu), ("hevc_nvenc", EncoderProbeState.Working), ("libx264", EncoderProbeState.Working));
        var plan = Plan(new PlanOptions { TargetMb = 50, Codec = CodecPreference.Auto, SpeedMode = SpeedMode.Fast }, makine);

        Assert.Equal("hevc_nvenc", plan.Codec);
        Assert.Equal(beklenenSebep, MainWindow.EncoderFallbackCauseOf(plan));
        Assert.True(MainWindow.EncoderFallbackToHardware(plan));

        foreach (var (dil, yazilim, donanim) in new[] { ("tr", "yazılım", "donanım"), ("en", "software", "hardware") })
        {
            foreach (var gpu in new[] { true, false })
            {
                var tavsiye = MainWindow.AdviceLine(AdviceCode.EncoderFallback, dil, gpu, beklenenSebep, fallbackToHardware: MainWindow.EncoderFallbackToHardware(plan));
                Assert.NotNull(tavsiye);
                Assert.DoesNotContain(yazilim, tavsiye!, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(donanim, tavsiye!, StringComparison.OrdinalIgnoreCase);
            }
        }

        var yok = Plan(new PlanOptions { TargetMb = 50, Codec = CodecPreference.Auto, SpeedMode = SpeedMode.Fast },
            new Makine(("av1_nvenc", EncoderProbeState.NotWorking), ("libx264", EncoderProbeState.Working), ("libsvtav1", EncoderProbeState.Working)));
        Assert.False(CodecModel.IsHardware(yok.Codec));
        Assert.False(MainWindow.EncoderFallbackToHardware(yok));
        var yazilimTavsiyesi = MainWindow.AdviceLine(AdviceCode.EncoderFallback, "tr", true, MainWindow.EncoderFallbackCauseOf(yok), fallbackToHardware: false);
        Assert.Contains("yazılım", yazilimTavsiyesi!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 7: kullanicinin acikca sectigi AAC Matroska'da AAC kalir ve CLI "Ses:" satiri onu yazar;
    /// secim yokken motorun kendi aac'si Opus'a doner (olumsuz kontrol).
    /// </summary>
    [Fact]
    public async Task MkvdeAcikAacKorunurVeCliSesSatiriGercegiYazar()
    {
        var info = Kaynak(600, Video, new SourceStream(1, StreamKind.Audio, "pcm_s16le", "eng", Channels: 2, BitrateBps: 1_536_000, SampleRate: 48000));
        var acik = Plan(new PlanOptions { TargetMb = 50, DeliveredContainer = OutputContainer.Mkv, AudioCodec = AudioCodecChoice.Aac }, info: info);
        var otomatik = Plan(new PlanOptions { TargetMb = 50, DeliveredContainer = OutputContainer.Mkv }, info: info);

        Assert.Equal("aac", Assert.Single(acik.Streams!.Audio).Codec);
        Assert.Equal("aac", acik.AudioCodec);
        Assert.Equal("libopus", Assert.Single(otomatik.Streams!.Audio).Codec);

        var metin = CliText.ForLanguage("tr");
        var cikti = Path.Combine(_klasor, "cikti.mkv");
        var acikMetin = await PlanMetni("pcm_s16le", "--ses-kodek", "aac", "--cikti", cikti);
        var otomatikMetin = await PlanMetni("pcm_s16le", "--cikti", cikti);
        Assert.Contains(SesSatiri(metin, "aac"), acikMetin, StringComparison.Ordinal);
        Assert.Contains(SesSatiri(metin, "libopus"), otomatikMetin, StringComparison.Ordinal);
    }

    private static string SesSatiri(CliText metin, string kodek) => metin.Format("plan.audio", kodek, "X").Split("X")[0];

    /// <summary>
    /// 5: vp9 kilidi calismayinca libx264'e dusulur ve kap WebM'den MP4'e doner; bunu ayri bir
    /// iz notu soyler. vp9 calisinca kap WebM, not yok (olumsuz kontrol).
    /// </summary>
    [Fact]
    public void Vp9DusunceKapDegisimiSoylenir()
    {
        var bozuk = Plan(new PlanOptions { TargetMb = 50, LockedCodec = "libvpx-vp9" },
            new Makine(("libvpx-vp9", EncoderProbeState.NotWorking), ("libx264", EncoderProbeState.Working)));
        var saglam = Plan(new PlanOptions { TargetMb = 50, LockedCodec = "libvpx-vp9" },
            new Makine(("libvpx-vp9", EncoderProbeState.Working), ("libx264", EncoderProbeState.Working)));

        Assert.Equal("libx264", bozuk.Codec);
        Assert.Equal(OutputContainer.Mp4, bozuk.Streams!.Container);
        Assert.Contains(StreamNote.Vp9FellBackToMp4, bozuk.Streams.Notes);
        Assert.Contains("instead of WebM", bozuk.Reason, StringComparison.Ordinal);

        Assert.Equal("libvpx-vp9", saglam.Codec);
        Assert.Equal(OutputContainer.WebM, saglam.Streams!.Container);
        Assert.DoesNotContain(StreamNote.Vp9FellBackToMp4, saglam.Streams.Notes);
    }

    /// <summary>
    /// 6: 'Izleri koru' acik ama kap MP4 ailesinden: not kutuyu acmayi onermez, kabi soyler.
    /// Kutu kapaliyken eski not, kutu acik ve kap serbestken hic not yok (olumsuz kontrol).
    /// </summary>
    [Fact]
    public void IzleriKoruAcikkenNotKabiSoyler()
    {
        var info = Kaynak10Dk(sesIzi: 2);
        var acikMp4 = Plan(new PlanOptions { TargetMb = 50, KeepAllTracks = true, DeliveredContainer = OutputContainer.Mp4 }, info: info);
        var kapaliMp4 = Plan(new PlanOptions { TargetMb = 50, KeepAllTracks = false, DeliveredContainer = OutputContainer.Mp4 }, info: info);
        var acikSerbest = Plan(new PlanOptions { TargetMb = 50, KeepAllTracks = true }, info: info);

        Assert.Contains(StreamNote.ExtraAudioDroppedByContainer, acikMp4.Streams!.Notes);
        Assert.DoesNotContain(StreamNote.ExtraAudioDropped, acikMp4.Streams.Notes);
        Assert.Contains(StreamNote.ExtraAudioDropped, kapaliMp4.Streams!.Notes);
        Assert.DoesNotContain(StreamNote.ExtraAudioDroppedByContainer, kapaliMp4.Streams.Notes);
        Assert.Equal(2, acikSerbest.Streams!.Audio.Count);
        Assert.DoesNotContain(StreamNote.ExtraAudioDroppedByContainer, acikSerbest.Streams.Notes);
    }

    private async Task<string> PlanMetni(string sesKodegi, params string[] ek)
    {
        var info = Kaynak(600, Video, new SourceStream(1, StreamKind.Audio, sesKodegi, "eng", Channels: 2, BitrateBps: 1_536_000, SampleRate: 48000));
        var servisler = new CliServices
        {
            MissingTool = () => null,
            Probe = (_, _) => Task.FromResult(info),
            Availability = () => null,
            UserPresets = () => Array.Empty<PresetProfile>(),
        };
        var dosya = Path.Combine(_klasor, "kaynak.mkv");
        await File.WriteAllTextAsync(dosya, "x");
        var stdout = new StringWriter();
        var exit = await CliApp.RunAsync(new[] { "plan", dosya, "--olcumsuz", "--hedef", "50" }.Concat(ek).ToArray(), stdout, new StringWriter(),
            CliText.ForLanguage("tr"), servisler, CancellationToken.None);
        Assert.Equal(0, exit);
        return stdout.ToString();
    }
}
