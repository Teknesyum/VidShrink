using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// DZA-P7, duzenleyici teslimi varsayilan olarak butun izleri tasir (<see cref="EditAudioTrack.All"/>): her ses izi Tam kipte
/// kendi atrim zinciriyle, Hizli kipte kendi eslemesiyle, Akilli kipte kendi parca dosyasi ve listesiyle gelir; altyazi
/// izleri klip basina ayri parcaya kesilir, sureli bir concat listesiyle son adima ek girdi olarak verilir. MP4 ailesi
/// metin altyazisini <c>mov_text</c>'e cevirir, goruntu altyazisini birakir; hiz ya da ters klipte altyazi tasinmaz.
/// Birlestirme ilk izde kalir. Canli kol iki ses izli, altyazili 3 sn 320x240 kaynagi (<c>-threads 2</c>) uc kipte
/// teslim edip ffprobe ile iz sayisini ve iz surelerini okur. Kanit <c>.calisma/worktree-agent-a08ba4829402285fe/tum-izler/</c>, test siler.
/// </summary>
public sealed class DuzenleyiciTumIzlerTests
{
    private const long Gb = 1_000_000_000;

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static readonly double[] Kareler = { 0.0, 2, 4, 6, 8 };

    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-a08ba4829402285fe", "tum-izler");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static MediaInfo Bilgi(string ikinci = "aac") => new()
    {
        FilePath = "kaynak.mkv",
        FileSizeBytes = 1,
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 1,
        AudioCodec = "aac",
        AudioChannels = 2,
        PixelFormat = "yuv420p",
        BitDepth = 8,
        Streams = new[]
        {
            new SourceStream(0, StreamKind.Video, "h264"),
            new SourceStream(1, StreamKind.Audio, "aac", "tur", IsDefault: true, Channels: 2),
            new SourceStream(2, StreamKind.Audio, ikinci, "eng", Channels: 2),
            new SourceStream(3, StreamKind.Subtitle, "subrip", "tur"),
            new SourceStream(4, StreamKind.Subtitle, "hdmv_pgs_subtitle", "eng")
        }
    };

    private static EditTimeline IkiKlip() => new(new[] { new EditClip(S(1), S(3)), new EditClip(S(4), S(6)) });

    private static ExportPlan Plan(ExportMode kip, EditTimeline? cizelge = null, MediaInfo? bilgi = null, string cikti = "c.mkv", bool meta = false)
        => EditExport.Build(cizelge ?? IkiKlip(), bilgi ?? Bilgi(), Kareler, 0, kip, cikti, "is", 8 * Gb,
            DuzenleyiciAkilliKodekTests.Sinirlar(Kareler), _ => true, meta);

    private static string Deger(IReadOnlyList<string> args, string anahtar) => args[args.ToList().IndexOf(anahtar) + 1];

    private static string[] Esler(ExportStep adim)
        => adim.Args.Select((a, i) => (a, i)).Where(x => x.a == "-map").Select(x => adim.Args[x.i + 1]).ToArray();

    private static ExportStep[] AltyaziParcalari(ExportPlan plan)
        => plan.Steps.Where(s => s.Args.Any(a => a.StartsWith("0:s:", StringComparison.Ordinal))).ToArray();

    [Fact]
    public void VarsayilanSecimButunIzlerdir()
    {
        Assert.Equal(EditAudioTrack.All, EditAudioTrack.Default);
        Assert.Equal(new[] { 0, 1 }, EditAudioTrack.Tracks(Bilgi(), EditAudioTrack.Default));
        Assert.Equal(new[] { 1 }, EditAudioTrack.Tracks(Bilgi(), 1));
        Assert.Empty(EditAudioTrack.Tracks(Bilgi(), EditAudioTrack.None));
        var tek = Bilgi() with { Streams = Bilgi().Streams.Where(s => s.Index != 2).ToArray() };
        Assert.Equal(new[] { 0 }, EditAudioTrack.Tracks(tek, EditAudioTrack.All));
    }

    [Fact]
    public void HizliKipButunSesIzleriniVeAltyaziyiEsler()
    {
        var plan = Plan(ExportMode.Fast);

        Assert.Equal(ExportMode.Fast, plan.Effective);
        var son = plan.Steps[^1];
        Assert.Equal(new[] { "0:v:0", "0:a:0", "0:a:1", "1:s" }, Esler(son));
        Assert.Equal("copy", Deger(son.Args, "-c:s"));
        Assert.Equal(Path.Combine("is", EditExport.SubtitleListName), son.Args[son.Args.ToList().LastIndexOf("-i") + 1]);
        Assert.Contains(son.ExtraLists, l => l.Path == Path.Combine("is", EditExport.SubtitleListName));
    }

    [Fact]
    public void TamKipHerSesIziniAyriZincirleBirlestirir()
    {
        var plan = Plan(ExportMode.Full);

        var graf = Deger(plan.Steps[^1].Args, "-filter_complex");
        Assert.Contains("[0:a]atrim=start=1:end=3", graf);
        Assert.Contains("[0:a:1]atrim=start=1:end=3", graf);
        Assert.Contains("[0:a:1]atrim=start=4:end=6", graf);
        Assert.Contains("[v0][a0][a0_1][v1][a1][a1_1]concat=n=2:v=1:a=2[vout][aout][aout1]", graf);
        Assert.Equal(new[] { "[vout]", "[aout]", "[aout1]", "1:s" }, Esler(plan.Steps[^1]));
    }

    [Fact]
    public void AkilliKipHerSesIzineAyriParcaVeListeYazar()
    {
        var plan = Plan(ExportMode.Smart);

        Assert.Equal(ExportMode.Smart, plan.Effective);
        var sesler = plan.Steps.Where(s => s.Args.Contains("-vn") && Esler(s).Any(m => m.StartsWith("0:a:", StringComparison.Ordinal))).ToArray();
        Assert.Equal(4, sesler.Length);
        Assert.Equal(2, sesler.Count(s => Deger(s.Args, "-map") == "0:a:1"));
        var son = plan.Steps[^1];
        Assert.Equal(new[] { "0:v:0", "1:a:0", "2:a:0", "3:s" }, Esler(son));
        Assert.Equal(Path.Combine("is", EditExport.AudioListName), son.AudioListPath);
        var ikinci = Assert.Single(son.ExtraLists, l => l.Path == Path.Combine("is", "audio1.ffconcat"));
        Assert.Contains("a1_0000.ts", ikinci.Content);
        Assert.Contains("a1_0001.ts", ikinci.Content);
        Assert.DoesNotContain("a1_", son.AudioListContent);
    }

    [Fact]
    public void ParcaliHizliKipParcalardaVeBirlesimdeButunIzler()
    {
        var cizelge = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(4), S(6), 1m, true) });
        var plan = Plan(ExportMode.Fast, cizelge);

        Assert.Equal(ExportMode.Fast, plan.Effective);
        Assert.Equal(3, plan.Steps.Count);
        Assert.All(plan.Steps.Take(2), adim => Assert.Equal(new[] { "0:v:0", "0:a:0", "0:a:1" }, Esler(adim)));
        Assert.Equal(new[] { "0:v:0", "0:a:0", "0:a:1" }, Esler(plan.Steps[^1]));
    }

    [Theory]
    [InlineData(ExportMode.Fast, "3", "4")]
    [InlineData(ExportMode.Full, "2", "1")]
    public void AltyaziKlipAraligindaKesilir(ExportMode kip, string ilkSure, string ilkBas)
    {
        var plan = Plan(kip);

        var parcalar = AltyaziParcalari(plan);
        Assert.Equal(2, parcalar.Length);
        Assert.Equal(ilkSure, Deger(parcalar[0].Args, "-t"));
        if (kip == ExportMode.Full) Assert.Equal(ilkBas, Deger(parcalar[0].Args, "-ss"));
        else Assert.DoesNotContain("-ss", parcalar[0].Args);
        Assert.Equal("4", Deger(parcalar[1].Args, "-ss"));
        Assert.Equal("2", Deger(parcalar[1].Args, "-t"));
        Assert.Equal("0", Deger(parcalar[1].Args, "-copypriorss"));
        var liste = plan.Steps[^1].ExtraLists.Single(l => l.Path == Path.Combine("is", EditExport.SubtitleListName)).Content;
        Assert.Contains("duration " + ilkSure + "\n", liste);
        Assert.Contains("duration 2\n", liste);
        Assert.Contains("s0001.mkv", liste);
    }

    [Fact]
    public void Mp4MetinAltyazisiniMovTexteCevirirGoruntuAltyazisiniBirakir()
    {
        var mp4 = Plan(ExportMode.Fast, cikti: "c.mp4");
        var mkv = Plan(ExportMode.Fast);

        Assert.All(AltyaziParcalari(mp4), s => Assert.Equal(new[] { "0:s:0" }, Esler(s)));
        Assert.Equal("mov_text", Deger(mp4.Steps[^1].Args, "-c:s"));
        Assert.All(AltyaziParcalari(mkv), s => Assert.Equal(new[] { "0:s:0", "0:s:1" }, Esler(s)));
        Assert.Empty(AltyaziParcalari(Plan(ExportMode.Fast, cikti: "c.webm")));
    }

    [Fact]
    public void HizliYaDaTersKliptAltyaziTasinmaz()
    {
        var cizelge = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(4), S(6), 2m) });

        foreach (var kip in new[] { ExportMode.Full, ExportMode.Fast, ExportMode.Smart })
        {
            var plan = Plan(kip, cizelge);
            Assert.Empty(AltyaziParcalari(plan));
            Assert.DoesNotContain("-c:s", plan.Steps[^1].Args);
        }
    }

    [Fact]
    public void TekIzSecilinceAltyaziGitmez()
    {
        var plan = EditExport.Build(IkiKlip(), Bilgi(), Kareler, 0, ExportMode.Fast, "c.mkv", "is", 8 * Gb, audioTrack: 0);

        Assert.Equal(new[] { "0:v:0", "0:a:0" }, Esler(Assert.Single(plan.Steps)));
    }

    [Fact]
    public void MetaSilmeButunIzlerinDiliniYazar()
    {
        var son = Plan(ExportMode.Full, meta: true).Steps[^1].Args;

        Assert.Equal("language=tur", Deger(son, "-metadata:s:a:0"));
        Assert.Equal("language=eng", Deger(son, "-metadata:s:a:1"));
        Assert.Equal("language=tur", Deger(son, "-metadata:s:s:0"));
        Assert.Equal("language=eng", Deger(son, "-metadata:s:s:1"));
    }

    [Fact]
    public void AkilliKapisiHerSesIziniSorar()
    {
        var hepsi = Plan(ExportMode.Smart, bilgi: Bilgi("pcm_s16le"));
        var ilk = EditExport.Build(IkiKlip(), Bilgi("pcm_s16le"), Kareler, 0, ExportMode.Smart, "c.mkv", "is", 8 * Gb,
            DuzenleyiciAkilliKodekTests.Sinirlar(Kareler), _ => true, audioTrack: EditAudioTrack.First);

        Assert.Equal(ExportMode.Full, hepsi.Effective);
        Assert.True(hepsi.AudioForcedFull);
        Assert.Equal(ExportMode.Smart, ilk.Effective);
    }

    [Fact]
    public void ParcaliHizliKapisiHerSesIziniSorar()
    {
        var cizelge = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(4), S(6), 1m, true) });

        Assert.Equal(ExportMode.Full, Plan(ExportMode.Fast, cizelge, Bilgi("ac3")).Effective);
    }

    [Fact]
    public void BirlestirmeIlkIzdeKalir()
    {
        var kaynaklar = new Dictionary<int, ExportSource>
        {
            [0] = new(Bilgi(), Kareler, 0),
            [1] = new(Bilgi() with { FilePath = "ek.mkv" }, Kareler, 0)
        };
        var cizelge = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(0), S(2)) { Source = 1 } }, extraSources: new[] { S(60) });

        var plan = EditExport.Build(cizelge, kaynaklar, ExportMode.Full, "c.mkv", "is", 8 * Gb);

        var graf = Deger(plan.Steps[0].Args, "-filter_complex");
        Assert.Contains("[0:a]atrim=start=1:end=3", graf);
        Assert.Contains("[1:a]atrim=start=0:end=2", graf);
        Assert.DoesNotContain(":a:1]", graf);
        Assert.Contains(":v=1:a=1[", graf);
        Assert.Single(plan.Steps);
    }

    [Fact]
    public void ProjeIlkIziYazarVarsayilaniYazmaz()
    {
        var damga = new SourceStamp("kaynak.mkv", 10, 20);
        var cizelge = new EditTimeline(new[] { new EditClip(S(0), S(2)) }, S(60));

        var varsayilan = EditProject.From(damga, cizelge).ToJson();
        var ilk = EditProject.From(damga, cizelge, audioTrack: EditAudioTrack.First).ToJson();

        Assert.DoesNotContain("audioTrack", varsayilan);
        Assert.Equal(EditAudioTrack.All, EditProject.Parse(varsayilan)!.AudioTrack);
        Assert.Contains("\"audioTrack\": 0", ilk.Replace("\"audioTrack\":0", "\"audioTrack\": 0"));
        Assert.Equal(EditAudioTrack.First, EditProject.Parse(ilk)!.AudioTrack);
    }

    [FfmpegFact]
    public async Task CanliTeslimButunIzleriTasirSureEsit()
    {
        string[] dosyalar = { "kaynak.mkv", "kaynak.srt", "tam.mkv", "hizli.mkv", "akilli.mkv", "olcu.txt" };
        KanitKapanisi.Onceki(Kanit, dosyalar);
        var srt = Path.Combine(Kanit, "kaynak.srt");
        File.WriteAllText(srt, "1\n00:00:00,100 --> 00:00:00,400\nbir\n\n2\n00:00:01,200 --> 00:00:01,800\niki\n\n3\n00:00:02,200 --> 00:00:02,800\nuc\n");
        var kaynak = Path.Combine(Kanit, "kaynak.mkv");
        Ffmpeg("-hide_banner", "-y",
            "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=3",
            "-f", "lavfi", "-i", "sine=f=440:d=3",
            "-f", "lavfi", "-i", "sine=f=880:d=3",
            "-i", srt,
            "-map", "0:v", "-map", "1:a", "-map", "2:a", "-map", "3:s",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "15", "-threads", "2",
            "-c:a", "aac", "-c:s", "srt", "-metadata:s:a:0", "language=tur", "-metadata:s:a:1", "language=eng", kaynak);
        var satirlar = new List<string>();
        var cizelge = new EditTimeline(new[] { new EditClip(S(1), S(1.5)), new EditClip(S(2), S(3)) });

        foreach (var (kip, ad) in new[] { (ExportMode.Full, "tam.mkv"), (ExportMode.Fast, "hizli.mkv"), (ExportMode.Smart, "akilli.mkv") })
        {
            var cikti = Path.Combine(Kanit, ad);
            var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, cizelge, kip, cikti, 8 * Gb);
            await EditExportRunner.RunAsync(plan, new Bos());
            var izler = Ffprobe("-show_entries", "stream=codec_type:stream_tags=DURATION", "-of", "csv=p=0", cikti)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.Split(','))
                .Select(p => (Tur: p[0], Sure: TimeSpan.Parse(p[1][..15], CultureInfo.InvariantCulture).TotalSeconds))
                .ToArray();
            var altyazi = Ffprobe("-select_streams", "s", "-show_entries", "packet=pts_time", "-of", "csv=p=0", cikti)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            satirlar.Add($"{kip} etkin={plan.Effective} izler={string.Join(";", izler.Select(i => i.Tur + "=" + i.Sure.ToString("0.###", CultureInfo.InvariantCulture)))} altyazi={string.Join(",", altyazi)}");

            Assert.Equal(1, izler.Count(i => i.Tur == "video"));
            Assert.Equal(2, izler.Count(i => i.Tur == "audio"));
            Assert.Equal(1, izler.Count(i => i.Tur == "subtitle"));
            var goruntu = izler.Single(i => i.Tur == "video").Sure;
            Assert.All(izler.Where(i => i.Tur == "audio"), i => Assert.InRange(i.Sure, goruntu - 0.1, goruntu + 0.1));
            Assert.Equal(2, altyazi.Length);
            Assert.All(altyazi, t => Assert.InRange(double.Parse(t, CultureInfo.InvariantCulture), 0, goruntu));
        }

        File.WriteAllLines(Path.Combine(Kanit, "olcu.txt"), satirlar);
        KanitKapanisi.Kapat(Kanit, dosyalar);
    }

    private static void Ffmpeg(params string[] args)
    {
        var psi = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        if (!surec.WaitForExit(30000))
        {
            surec.Kill(true);
            Assert.Fail("ffmpeg 30 sn icinde bitmedi.");
        }

        Assert.True(surec.ExitCode == 0, hata.GetAwaiter().GetResult());
    }

    private static string Ffprobe(params string[] args)
    {
        var psi = new ProcessStartInfo("ffprobe") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("error");
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var metin = surec.StandardOutput.ReadToEnd();
        Assert.True(surec.WaitForExit(15000));
        return metin;
    }

    private sealed class Bos : IProgress<EncodeProgress>
    {
        public void Report(EncodeProgress value)
        {
        }
    }
}
