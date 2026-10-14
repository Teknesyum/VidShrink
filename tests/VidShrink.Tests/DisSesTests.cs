using System.Globalization;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// SIK-P4 dis ses izi: ses dosyasi ffmpeg'e ana girdiden sonraki ilk <c>-i</c> olarak verilir,
/// <c>1:a:0</c> ile ya kaynagin sesinin yanina eklenir ya da onun yerine konur. Iz her zaman
/// yeniden kodlanir ve bit hizi yan butceye girer; ses videodan uzunsa girdi <c>-t</c> ile video
/// suresinde kesilir. Kanit <c>.calisma/b1-kalan/dis-ses-*</c>, son asertten sonra silinir.
/// </summary>
public sealed class DisSesTests
{
    private static readonly SourceStream Ses = new(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000, SampleRate: 48000);

    private static ExternalAudio Dis(bool yerine = false) => new("C:\\dis\\muzik.tr.m4a", "tur", yerine);

    private static StreamPlan Karar(OutputContainer kap, ExternalAudio? dis, MediaInfo? info = null, IReadOnlyList<ExternalSubtitle>? altyazi = null)
        => StreamMapping.Decide(info ?? Kaynak(600, Video, Ses), new StreamRequest(ExternalAudio: dis, ExternalSubtitles: altyazi),
            kap, 128, null, "aac", false, 100);

    /// <summary>Uzanti ve dil dosya adindan; tanimayan uzanti ve olmayan dosya <c>null</c>.</summary>
    [Fact]
    public void DosyadanOkunuyor()
    {
        var yol = Yol("dis-ses-okuma.tr.m4a");
        File.WriteAllBytes(yol, new byte[] { 1, 2, 3 });
        var metin = Yol("dis-ses-okuma.txt");
        File.WriteAllText(metin, "x");

        var ses = ExternalAudio.FromFile(yol, replace: true)!;
        Assert.Equal(Path.GetFullPath(yol), ses.Path);
        Assert.Equal("tur", ses.Language);
        Assert.True(ses.Replace);
        Assert.Null(ExternalAudio.FromFile(metin));
        Assert.Null(ExternalAudio.FromFile(Yol("dis-ses-yok.mp3")));

        Kapat("dis-ses-okuma*");
    }

    /// <summary>Yanina ekleme: kaynagin izi once, dis iz <c>1:a:0</c> ile ikinci; ek girdi ses dosyasi.</summary>
    [Fact]
    public void YaninaEklenir()
    {
        var plan = Karar(OutputContainer.Mp4, Dis());
        var args = plan.OutputArguments().ToList();

        Assert.Equal(new[] { "0:1", "1:a:0" }, plan.Audio.Select(track => track.Map));
        Assert.Equal(Dis().Path, plan.Audio[1].InputPath);
        Assert.Equal(new[] { Dis().Path }, plan.ExtraInputs);
        Assert.Equal(Dis().Path, plan.ExternalAudioInput);
        Assert.Contains("1:a:0", args);
        Assert.Equal("aac", Sonraki(args, "-c:a:1"));
        Assert.Equal("language=tur", Sonraki(args, "-metadata:s:a:1"));
        Assert.Equal(256, plan.SideK, 6);
    }

    /// <summary>Yerine koyma: kaynagin sesi eslenmez, tek iz dis dosyadan; olumsuz kontrol <c>0:1</c> yok.</summary>
    [Fact]
    public void YerineKonur()
    {
        var plan = Karar(OutputContainer.Mkv, Dis(yerine: true));
        var args = plan.OutputArguments().ToList();

        Assert.Equal(new[] { "1:a:0" }, plan.Audio.Select(track => track.Map));
        Assert.DoesNotContain("0:1", args);
        Assert.Equal("libopus", plan.Audio[0].Codec);
        Assert.Equal(128, plan.SideK, 6);
    }

    /// <summary>Dis ses ve dis altyazi birlikte: ses girdisi once (<c>1</c>), altyazi ikinci (<c>2:0</c>).</summary>
    [Fact]
    public void AltyaziIleGirdiSirasi()
    {
        var altyazi = new ExternalSubtitle("C:\\dis\\film.tr.srt", "subrip", "tur", 2_000);
        var plan = Karar(OutputContainer.Mp4, Dis(), altyazi: new[] { altyazi });

        Assert.Equal(new[] { Dis().Path, altyazi.Path }, plan.ExtraInputs);
        Assert.Equal("1:a:0", plan.Audio[1].Map);
        Assert.Equal("2:0", plan.Subtitles.Single().Map);
    }

    /// <summary>Sessiz kaynak: dis ses tek iz olur; dis sessiz plan sessiz kalir (olumsuz kontrol).</summary>
    [Fact]
    public void SessizKaynagaSesGelir()
    {
        var info = Kaynak(600, Video);
        var sesli = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25, ExternalAudio = Dis() });
        var sessiz = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25 });

        Assert.Equal("1:a:0", sesli.Streams!.Audio.Single().Map);
        Assert.True(sesli.AudioBitrateK > 0);
        Assert.NotNull(sesli.AudioCodec);
        Assert.Empty(sessiz.Streams!.Audio);
        Assert.Null(sessiz.AudioCodec);
    }

    /// <summary>
    /// Hedef boyut hesabi: eklenen izin bit hizi yan butceye girer ve video payi o kadar duser.
    /// Kopya yolu da kapanir (kaynak hedefe sigsa bile dis ses ancak kodlamayla girer).
    /// </summary>
    [Fact]
    public void ButceyeGirer()
    {
        var info = Kaynak(600, Video, Ses);
        var dissiz = PlanCalculator.Build(info, new PlanOptions { TargetMb = 50 });
        var disli = PlanCalculator.Build(info, new PlanOptions { TargetMb = 50, ExternalAudio = Dis() });

        var eklenen = disli.Streams!.Audio.Single(track => track.InputPath is not null).BitrateK;
        Assert.Equal(dissiz.Streams!.SideK + eklenen, disli.Streams.SideK, 6);
        Assert.True(disli.VideoBitrateK < dissiz.VideoBitrateK, $"{disli.VideoBitrateK} < {dissiz.VideoBitrateK}");

        var kucuk = info with { FileSizeBytes = 1_000_000, TotalBitrateBps = 13_000 };
        Assert.Equal(EncodeMode.PassThrough, PlanCalculator.Build(kucuk, new PlanOptions { TargetMb = 50 }).ModeEnum);
        Assert.NotEqual(EncodeMode.PassThrough, PlanCalculator.Build(kucuk, new PlanOptions { TargetMb = 50, ExternalAudio = Dis() }).ModeEnum);
    }

    /// <summary>
    /// Ses girdisi kesitsiz komutta <c>-t &lt;video suresi&gt;</c> alir; kesitte cikti <c>-t</c>'si keser,
    /// girdi ana girdiyle ayni <c>-ss</c>'i alir. Ilk gecis ses dosyasini acmaz (olumsuz kontrol).
    /// </summary>
    [Fact]
    public void GirdiVideoSuresindeKesilir()
    {
        var info = Kaynak(600, Video, Ses);
        var plan = PlanCalculator.Build(info, new PlanOptions { TargetMb = 25, ExternalAudio = Dis() });

        var duz = FfmpegArguments.Build(info, plan, "cikti.mp4", 2, "log").ToList();
        var ses = duz.IndexOf(Dis().Path);
        Assert.Equal("-i", duz[ses - 1]);
        Assert.Equal("-t", duz[ses - 3]);
        Assert.Equal("600", duz[ses - 2]);
        Assert.Equal(duz.IndexOf(info.FilePath) + 4, ses);

        Assert.DoesNotContain(Dis().Path, FfmpegArguments.Build(info, plan, "cikti.mp4", 1, "log"));

        plan.Trim = new TrimWindow(30, 60);
        var kesit = FfmpegArguments.Build(info, plan, "cikti.mp4", 2, "log").ToList();
        var dis = kesit.IndexOf(Dis().Path);
        Assert.Equal("-ss", kesit[dis - 3]);
        Assert.Equal(kesit[kesit.IndexOf(info.FilePath) - 2], kesit[dis - 2]);
    }

    /// <summary>CLI: <c>--ses-dosyasi</c> ekler, <c>--ses-degistir</c> yerine koyar; uzanti ve yokluk reddedilir.</summary>
    [Fact]
    public void CliBayraklari()
    {
        var yol = Yol("dis-ses-cli.m4a");
        File.WriteAllBytes(yol, new byte[] { 1 });

        var ekle = CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25", "--audio-file", yol });
        Assert.True(ekle.Ok, ekle.ErrorKey);
        Assert.Null(ekle.Request!.ResolvedSubtitles(Kaynak(600, Video), out var cozulen, out _));
        Assert.False(cozulen.ToPlanOptions(25).ExternalAudio!.Replace);

        var degistir = CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25", "--ses-dosyasi", yol, "--ses-degistir" });
        Assert.Null(degistir.Request!.ResolvedSubtitles(Kaynak(600, Video), out cozulen, out _));
        Assert.True(cozulen.ToPlanOptions(25).ExternalAudio!.Replace);
        Assert.Equal(Path.GetFullPath(yol), cozulen.ToPlanOptions(25).ExternalAudio!.Path);

        Assert.Equal("error.bad-audio-file", CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25", "--ses-dosyasi", "notlar.txt" }).ErrorKey);
        var yok = CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25", "--ses-dosyasi", Yol("dis-ses-olmayan.mp3") });
        Assert.Equal("error.bad-audio-file", yok.Request!.ResolvedSubtitles(Kaynak(600, Video), out _, out var arguman));
        Assert.Equal(Yol("dis-ses-olmayan.mp3"), arguman);
        Assert.Equal("error.replace-audio-needs-file", CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25", "--ses-degistir" }).ErrorKey);

        foreach (var bayrak in new[] { "--ses-dosyasi", "--audio-file", "--ses-degistir", "--replace-audio" })
            Assert.Contains(bayrak, CliParser.NotInWatch);

        Kapat("dis-ses-cli*");
    }

    /// <summary>
    /// Canli kol: 3 sn video (48 kHz ses) ve 6 sn dis ses (44,1 kHz). Eklemede ffprobe iki ses izini
    /// bu sirayla okur, yerine koymada tek iz 44,1 kHz (kaynagin 48 kHz izi yok); dis iz video suresinde (≤3,1 sn) biter.
    /// </summary>
    [Fact]
    public async Task CanliIzSayisiVeSirasi()
    {
        var kaynak = await SesliAsync("dis-ses-kaynak.mkv", 48000, 1);
        var dis = await DisSesAsync("dis-ses-muzik.tr.wav", 6);

        await KodlaAsync(kaynak, Yol("dis-ses-ekle.mp4"), options => { options.EncoderThreads = 2; options.ExternalAudio = ExternalAudio.FromFile(dis); });
        await KodlaAsync(kaynak, Yol("dis-ses-yerine.mp4"), options => { options.EncoderThreads = 2; options.ExternalAudio = ExternalAudio.FromFile(dis, replace: true); });

        var ekle = (await AkislarAsync(Yol("dis-ses-ekle.mp4"))).Where(stream => Alan(stream, "codec_type") == "audio").ToList();
        Assert.Equal(new[] { "48000", "44100" }, ekle.Select(stream => Alan(stream, "sample_rate")));
        Assert.Equal("tur", Etiket(ekle[1], "language"));
        Assert.True(double.Parse(Alan(ekle[1], "duration"), CultureInfo.InvariantCulture) <= 3.1, Alan(ekle[1], "duration"));

        var yerine = (await AkislarAsync(Yol("dis-ses-yerine.mp4"))).Where(stream => Alan(stream, "codec_type") == "audio").ToList();
        Assert.Equal("44100", Alan(yerine.Single(), "sample_rate"));

        Kapat("dis-ses-*");
    }

    /// <summary>
    /// Canli kol, hedef boyut: 6 sn gurultulu video, 2 gecis, 1 MB hedef, dis ses eklenmis. Cikti
    /// hedefi asmaz ve <see cref="BudgetFill.Floor"/>'un altina dusmez; tek kodlama, yeniden deneme yok.
    /// </summary>
    [Fact]
    public async Task CanliHedefBantta()
    {
        var kaynak = Yol("dis-ses-hedef.mkv");
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-threads", "2",
            "-f", "lavfi", "-i", "testsrc=size=640x360:rate=25:duration=6,noise=alls=60:allf=t",
            "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=6",
            "-map", "0", "-map", "1", "-c:v", "libx264", "-preset", "ultrafast", "-b:v", "6M", "-c:a", "aac", kaynak
        });
        var dis = await DisSesAsync("dis-ses-hedef-muzik.wav", 8);
        var info = await FfprobeClient.ProbeAsync(kaynak);
        var plan = PlanCalculator.Build(info, new PlanOptions
        {
            TargetMb = 1, LockedCodec = "libx264", LockedPreset = "ultrafast", EncoderThreads = 2, ExternalAudio = ExternalAudio.FromFile(dis)
        });
        Assert.Equal(EncodeMode.TwoPass, plan.ModeEnum);
        var log = Yol("dis-ses-hedef-log");
        var cikti = Yol("dis-ses-hedef.mp4");
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, FfmpegArguments.Build(info, plan, cikti, 1, log));
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, FfmpegArguments.Build(info, plan, cikti, 2, log));

        var bayt = new FileInfo(cikti).Length;
        var hedef = 1 * Megabayt.Bayt;
        File.WriteAllText(Yol("dis-ses-hedef-olcum.txt"), $"bayt={bayt} hedef={hedef} oran={bayt / (double)hedef:0.000}");
        Assert.Equal(2, (await AkislarAsync(cikti)).Count(stream => Alan(stream, "codec_type") == "audio"));
        Assert.InRange(bayt / (double)hedef, BudgetFill.Floor, 1.0);

        Kapat("dis-ses-*");
    }

    private static async Task<string> DisSesAsync(string ad, int saniye)
    {
        var yol = Yol(ad);
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-threads", "2",
            "-f", "lavfi", "-i", $"sine=frequency=880:sample_rate=44100:duration={saniye}", "-c:a", "pcm_s16le", yol
        });
        return yol;
    }

    private static T Pencerede<T>(Func<VidShrink.App.MainWindow, T> is_)
        => AppHost.Run(() =>
        {
            var window = new VidShrink.App.MainWindow();
            try { return is_(window); }
            finally { window.Close(); }
        });

    /// <summary>
    /// Arayuzde secilen dosya plana gecer, kutu iz yerine koymayi acar; taninmayan dosya onceki secimi
    /// bozmaz (olumsuz kontrol), cikarma plani ve listeyi bosaltir.
    /// </summary>
    [Fact]
    public void ArayuzSecimiPlanaGecer()
    {
        var yol = Yol("dis-ses-arayuz.tr.m4a");
        var metin = Yol("dis-ses-arayuz.txt");
        File.WriteAllBytes(yol, new byte[] { 1, 2, 3 });
        File.WriteAllText(metin, "x");
        try
        {
            var (bos, kabul, yanina, yerine, ret, retSonra, gorunur, cikti, gizli) = Pencerede(window =>
            {
                window.LoadWithoutProbing("C:\\kaynak\\girdi.mkv", Kaynak(60, Video, Ses));
                var ilk = window.PlanOptionsForTest().ExternalAudio;
                var ok = window.SetAudioFile(yol);
                var yan = window.PlanOptionsForTest().ExternalAudio;
                var liste = window.AudioFileList.IsVisible;
                window.ChkAudioReplace.IsChecked = true;
                var yer = window.PlanOptionsForTest().ExternalAudio;
                var red = window.SetAudioFile(metin);
                var sonra = window.PlanOptionsForTest().ExternalAudio;
                window.RemoveAudioFile();
                return (ilk, ok, yan, yer, red, sonra, liste, window.PlanOptionsForTest().ExternalAudio, !window.AudioFileList.IsVisible);
            });

            Assert.Null(bos);
            Assert.True(kabul);
            Assert.Equal(Path.GetFullPath(yol), yanina!.Path);
            Assert.Equal("tur", yanina.Language);
            Assert.False(yanina.Replace);
            Assert.True(gorunur);
            Assert.True(yerine!.Replace);
            Assert.False(ret);
            Assert.Equal(Path.GetFullPath(yol), retSonra!.Path);
            Assert.Null(cikti);
            Assert.True(gizli);
        }
        finally { Kapat("dis-ses-arayuz.tr.m4a", "dis-ses-arayuz.txt"); }
    }

    /// <summary>Ses dosyasi o videoya aittir: yeni kaynak secimi ve kutuyu sifirlar, kuyruk penceresi almaz.</summary>
    [Fact]
    public void ArayuzYeniKaynakVeKuyrukTasimaz()
    {
        var yol = Yol("dis-ses-kuyruk.m4a");
        File.WriteAllBytes(yol, new byte[] { 1, 2, 3 });
        try
        {
            var (once, sonra, kutu, kuyruk) = Pencerede(window =>
            {
                VidShrink.App.ShrinkJobWindow? batch = null;
                try
                {
                    window.LoadWithoutProbing("C:\\kaynak\\girdi.mkv", Kaynak(60, Video, Ses));
                    window.SetAudioFile(yol);
                    window.ChkAudioReplace.IsChecked = true;
                    var ilk = window.PlanOptionsForTest().ExternalAudio;
                    batch = window.OpenBatch(new[] { "a.mp4", "b.mp4" });
                    var kuyrukta = batch.OptionsFor(new ShrinkRequest(0, "a.mp4"), Kaynak(60, Video, Ses)).Options.ExternalAudio;
                    window.LoadWithoutProbing("C:\\kaynak\\ikinci.mkv", Kaynak(60, Video, Ses));
                    return (ilk, window.PlanOptionsForTest().ExternalAudio, window.ChkAudioReplace.IsChecked, kuyrukta);
                }
                finally { batch?.Close(); }
            });

            Assert.NotNull(once);
            Assert.True(once!.Replace);
            Assert.Null(kuyruk);
            Assert.Null(sonra);
            Assert.False(kutu);
        }
        finally { Kapat("dis-ses-kuyruk.m4a"); }
    }

    /// <summary>Bes arayuz anahtari 42 dilde dolu.</summary>
    [Fact]
    public void ArayuzAnahtarlariButunDillerde()
    {
        var anahtarlar = new[]
        {
            "main.audio.external.label", "main.audio.external.add", "main.audio.external.replace",
            "main.audio.external.remove", "main.audio.external.file-type"
        };
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            foreach (var key in anahtarlar)
                Assert.True(values.TryGetValue(key, out var metin) && metin.Length > 0, $"{language}: {key}");
        }
    }
}
