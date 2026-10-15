using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// DZB-P7, muzik izi (<see cref="EditMusic"/>): cizelgeye tek ses dosyasi baglanir, teslimde son girdinin ardina <c>-i</c> olarak
/// girer, <c>volume</c> + <c>adelay</c> + <c>apad</c> + <c>atrim</c> zinciriyle <c>[mus]</c> olur. Kaynak sesi varsa <c>[aout]</c>
/// ile <c>amix</c> edilip <c>[amix]</c> eslenir, yoksa <c>[mus]</c> tek ses izi olur ve ses kodlayicisi eklenir. Muzik Hizli ve
/// Akilli kipi Tam'a dusurur (<see cref="ExportPlan.MusicForcedFull"/>). Proje dosyasi muzigi tasir; alani olmayan eski proje
/// muziksiz acilir, bozuk muzik projeyi okunmaz yapar. Kaydetme anahtari muzikle degisir.
/// <para>
/// Canli kol 3 sn'lik 440 Hz kaynaga 1. saniyede baslayan 3000 Hz muzik ekler; ciktinin ses izi 2 kHz ustunde suzulunce
/// muzikli ciktida duyulur duzeyde, muziksizde sessizdir; muzigin baslamadigi ilk 0,9 sn de sessizdir. Kanit
/// <c>.calisma/worktree-agent-a08ba4829402285fe/muzik/</c>, test siler. Ses calinmaz.
/// </para>
/// </summary>
public sealed class DuzenleyiciMuzikTests
{
    private const long Gb = 1_000_000_000;

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static readonly double[] Kareler = { 0.0, 2, 4, 6, 8 };

    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-a08ba4829402285fe", "muzik");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static MediaInfo Bilgi() => new()
    {
        FilePath = "kaynak.mp4",
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
            new SourceStream(1, StreamKind.Audio, "aac", "tur", IsDefault: true, Channels: 2)
        }
    };

    private static MediaInfo Sessiz() => Bilgi() with
    {
        AudioCodec = null,
        AudioChannels = 0,
        Streams = new[] { new SourceStream(0, StreamKind.Video, "h264") }
    };

    private static EditMusic Muzik(double baslangic = 1, double duzey = -6) => new("muzik.m4a", S(30), S(baslangic), duzey);

    private static EditTimeline IkiKlip(EditMusic? muzik = null)
    {
        var cizelge = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(4), S(6)) });
        if (muzik is not null) Assert.True(cizelge.SetMusic(muzik));
        return cizelge;
    }

    private static ExportPlan Plan(ExportMode kip, EditTimeline cizelge, MediaInfo? bilgi = null, EditExportSettings? ayar = null, string cikti = "c.mp4")
        => EditExport.Build(cizelge, bilgi ?? Bilgi(), Kareler, 0, kip, cikti, "is", 8 * Gb,
            DuzenleyiciAkilliKodekTests.Sinirlar(Kareler), _ => true, settings: ayar);

    private static string Deger(IReadOnlyList<string> args, string anahtar) => args[args.ToList().IndexOf(anahtar) + 1];

    private static IReadOnlyList<string> Args(ExportPlan plan) => Assert.Single(plan.Steps).Args;

    private static string Grafik(ExportPlan plan) => Deger(Args(plan), "-filter_complex");

    private static List<string> Eslenen(IReadOnlyList<string> args)
        => args.Select((a, i) => (a, i)).Where(x => x.i > 0 && args[x.i - 1] == "-map").Select(x => x.a).ToList();

    [Fact]
    public void MuzikYazmaGeriAlinir()
    {
        var cizelge = IkiKlip();
        var muzik = Muzik();

        Assert.False(cizelge.HasMusic);
        Assert.True(cizelge.SetMusic(muzik));
        Assert.Equal(muzik, cizelge.Music);
        Assert.False(cizelge.SetMusic(Muzik()));
        Assert.True(cizelge.Undo());
        Assert.Null(cizelge.Music);
        Assert.True(cizelge.Redo());
        Assert.Equal(muzik, cizelge.Music);
        Assert.True(cizelge.SetMusic(null));
        Assert.False(cizelge.HasMusic);
    }

    [Fact]
    public void GeriYuklemeMuzikleTekAdimdir()
    {
        var cizelge = IkiKlip();
        var muzik = Muzik(2, -3);

        Assert.True(cizelge.Restore(new[] { new EditClip(S(0), S(5)) }, Array.Empty<TextLayer>(), muzik));
        Assert.Single(cizelge.Clips);
        Assert.Equal(muzik, cizelge.Music);
        Assert.True(cizelge.Undo());
        Assert.Equal(2, cizelge.Clips.Count);
        Assert.Null(cizelge.Music);

        Assert.True(cizelge.Restore(cizelge.Clips.ToArray(), Array.Empty<TextLayer>(), muzik));
        Assert.Equal(2, cizelge.Clips.Count);
        Assert.Equal(muzik, cizelge.Music);
        Assert.False(cizelge.Restore(cizelge.Clips.ToArray(), Array.Empty<TextLayer>(), muzik));
    }

    [Fact]
    public void SinirDisiMuzikReddedilir()
    {
        Assert.Throws<ArgumentException>(() => new EditMusic(" ", S(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EditMusic("m.mp3", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EditMusic("m.mp3", S(1), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EditMusic("m.mp3", S(1), 0, -30.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EditMusic("m.mp3", S(1), 0, 6.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EditMusic("m.mp3", S(1), 0, double.NaN));
        Assert.Equal(-30, new EditMusic("m.mp3", S(1), 0, -30).LevelDb);
        Assert.Equal(6, new EditMusic("m.mp3", S(1), 0, 6).LevelDb);
        Assert.Equal(-30, EditMusic.ClampLevel(-99));
        Assert.Equal(6, EditMusic.ClampLevel(99));
        Assert.Equal(-2.3, EditMusic.ClampLevel(-2.34));
        Assert.Equal(0, EditMusic.ClampLevel(double.PositiveInfinity));
    }

    [Fact]
    public void TamKipteKaynakSesiyleKarisir()
    {
        var plan = Plan(ExportMode.Full, IkiKlip(Muzik(1.5, -6)));
        var args = Args(plan);
        var grafik = Grafik(plan);

        Assert.Equal(new[] { "kaynak.mp4", "muzik.m4a" }, args.Select((a, i) => (a, i)).Where(x => x.i > 0 && args[x.i - 1] == "-i").Select(x => x.a));
        Assert.Contains(";[1:a:0]volume=-6.0dB," + EditExport.MergeAudioFormat + ",adelay=1500:all=1,apad,atrim=duration=4,asetpts=PTS-STARTPTS[mus]", grafik);
        Assert.EndsWith(";[aout]" + EditExport.MergeAudioFormat + "[asrc];[asrc][mus]amix=inputs=2:duration=first:dropout_transition=0:normalize=0[amix]", grafik);
        Assert.Equal(new[] { "[vout]", "[amix]" }, Eslenen(args));
        Assert.Single(args, a => a == "-c:a");
        Assert.False(plan.MusicForcedFull);
    }

    [Fact]
    public void SessizKaynaktaMuzikTekSesIzidir()
    {
        var plan = Plan(ExportMode.Full, IkiKlip(Muzik(0, 3)), Sessiz());
        var args = Args(plan);
        var grafik = Grafik(plan);

        Assert.Contains("[1:a:0]volume=3.0dB,", grafik);
        Assert.Contains(",adelay=0:all=1,", grafik);
        Assert.DoesNotContain("amix", grafik);
        Assert.Equal(new[] { "[vout]", "[mus]" }, Eslenen(args));
        Assert.Equal(EditExport.AudioCodec, Deger(args, "-c:a"));
        Assert.Equal(EditExportSettings.Default.AudioBitrate, Deger(args, "-b:a"));
        Assert.Equal("c.mp4", args[^1]);
    }

    [Fact]
    public void MuziksizGrafikDegismez()
    {
        var grafik = Grafik(Plan(ExportMode.Full, IkiKlip()));

        Assert.DoesNotContain("[mus]", grafik);
        Assert.EndsWith("concat=n=2:v=1:a=1[vout][aout]", grafik);
        Assert.Equal(new[] { "[vout]", "[aout]" }, Eslenen(Args(Plan(ExportMode.Full, IkiKlip()))));
    }

    [Theory]
    [InlineData(ExportMode.Fast)]
    [InlineData(ExportMode.Smart)]
    public void MuzikHizliVeAkilliKipiTamaDusurur(ExportMode kip)
    {
        var plan = Plan(kip, IkiKlip(Muzik()));

        Assert.Equal(kip, plan.Requested);
        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.True(plan.MusicForcedFull);
        Assert.Contains("amix=", Grafik(plan));

        var muziksiz = Plan(kip, IkiKlip());
        Assert.Equal(kip, muziksiz.Effective);
        Assert.False(muziksiz.MusicForcedFull);
    }

    [Fact]
    public void EfektDeTamaDusururkenMuzikBayragiKalkmaz()
    {
        var cizelge = new EditTimeline(new[]
        {
            new EditClip(S(1), S(3)) { Effects = ClipEffects.None with { VolumeDb = -6 } },
            new EditClip(S(4), S(6))
        });
        Assert.True(cizelge.SetMusic(Muzik()));
        var plan = Plan(ExportMode.Fast, cizelge);

        Assert.True(plan.EffectsForcedFull);
        Assert.False(plan.MusicForcedFull);
        Assert.Contains("amix=", Grafik(plan));
    }

    [Fact]
    public void YalnizSesTeslimindeDeKarisir()
    {
        var plan = Plan(ExportMode.Full, IkiKlip(Muzik()), ayar: EditExportSettings.Default with { AudioOnly = true }, cikti: "c.m4a");
        var args = Args(plan);

        Assert.Contains("[1:a:0]volume=", Grafik(plan));
        Assert.Contains("[amix]", Grafik(plan));
        Assert.Equal(new[] { "[amix]" }, Eslenen(args));
        Assert.Contains("-vn", args);
    }

    [Fact]
    public void BirlestirmeDeMuzikSonGirdidir()
    {
        var kaynaklar = new Dictionary<int, ExportSource>
        {
            [0] = new(Bilgi() with { FilePath = "birinci.mp4", Streams = Array.Empty<SourceStream>() }, new[] { 0.0 }, 0),
            [1] = new(Bilgi() with { FilePath = "ikinci.mp4", Width = 640, Height = 360, Streams = Array.Empty<SourceStream>() }, new[] { 0.0 }, 0)
        };
        var cizelge = EditTimeline.FromSource(S(10));
        Assert.Equal(1, cizelge.AddSource(S(4)));
        Assert.True(cizelge.SetMusic(Muzik()));

        var plan = EditExport.Build(cizelge, kaynaklar, ExportMode.Full, "cikti.mp4", "is", 8 * Gb);
        var args = Args(plan);

        Assert.Equal("muzik.m4a", args[args.ToList().LastIndexOf("-i") + 1]);
        Assert.Equal(3, args.Count(a => a == "-i"));
        Assert.Contains(";[2:a:0]volume=-6.0dB,", Grafik(plan));
        Assert.Contains(",atrim=duration=14,", Grafik(plan));
        Assert.Contains("[amix]", Eslenen(args));
        Assert.DoesNotContain("[aout]", Eslenen(args));
    }

    [Fact]
    public void ProjeDosyasiMuzigiTasir()
    {
        var damga = new SourceStamp("kaynak.mp4", 10, 20);
        var muzik = new EditMusic(@"C:\muzik\parca.mp3", S(95.5), S(2.25), -7.5);

        var json = EditProject.From(damga, IkiKlip(muzik)).ToJson();
        var geri = EditProject.Parse(json);

        Assert.Contains("\"music\"", json);
        Assert.NotNull(geri);
        Assert.Equal(muzik, geri!.Music);

        var eski = EditProject.From(damga, IkiKlip()).ToJson();
        Assert.DoesNotContain("music", eski);
        Assert.Null(EditProject.Parse(eski)!.Music);

        var bozuk = Regex.Replace(json, "\"level\":\\s*-7\\.5", "\"level\": 99");
        Assert.NotEqual(json, bozuk);
        Assert.Null(EditProject.Parse(bozuk));
    }

    [Fact]
    public void KaydetmeAnahtariMuzikleVeGecisleDegisir()
    {
        var cizelge = IkiKlip();
        var bos = EditorView.Fingerprint(cizelge);

        Assert.True(cizelge.SetMusic(Muzik()));
        var muzikli = EditorView.Fingerprint(cizelge);
        Assert.NotEqual(bos, muzikli);
        Assert.True(cizelge.SetMusic(Muzik(1, -5)));
        Assert.NotEqual(muzikli, EditorView.Fingerprint(cizelge));

        var gecissiz = EditorView.Fingerprint(cizelge);
        Assert.True(cizelge.SetTransition(new[] { 1 }, new ClipTransition(TransitionKind.Dissolve, S(0.5))));
        Assert.NotEqual(gecissiz, EditorView.Fingerprint(cizelge));
    }

    /// <summary>Metnin kenar, golge ve kutu ayari degisince kaydetme anahtari da degisir; yoksa teslim onbellegi bayat kalir.</summary>
    [Fact]
    public void KaydetmeAnahtariMetinKenarGolgeVeKutuylaDegisir()
    {
        var cizelge = IkiKlip();
        var metin = new TextLayer("a", 0, S(1));
        cizelge.AddText(metin);
        var once = EditorView.Fingerprint(cizelge);

        foreach (var degisik in new[] { metin with { Outline = 3 }, metin with { Shadow = 2.5 }, metin with { Box = true } })
        {
            Assert.True(cizelge.UpdateText(0, degisik));
            Assert.NotEqual(once, EditorView.Fingerprint(cizelge));
            Assert.True(cizelge.UpdateText(0, metin));
            Assert.Equal(once, EditorView.Fingerprint(cizelge));
        }
    }

    [Theory]
    [InlineData(ExportMode.Fast, true)]
    [InlineData(ExportMode.Smart, true)]
    [InlineData(ExportMode.Full, false)]
    public void ZorlamaNotuYalnizDusulenKipteYazilir(ExportMode kip, bool zorlandi)
    {
        var eski = Strings.Language;
        try
        {
            Strings.Use("tr");
            var not = Strings.Get("editor.export.music-full");
            Assert.Equal("Müzik izi yeniden kodlama ister; Tam kipine geçildi.", not);
            Assert.Equal(zorlandi, EditorView.Notes(new[] { Plan(kip, IkiKlip(Muzik())) }).Contains(not, StringComparison.Ordinal));
            Assert.DoesNotContain(not, EditorView.Notes(new[] { Plan(kip, IkiKlip()) }));
        }
        finally
        {
            Strings.Use(eski);
        }
    }

    [FfmpegFact]
    public async Task CanliTeslimMuzigiKaynakSesineKatar()
    {
        string[] dosyalar = { "kaynak.mp4", "muzik.m4a", "muzikli.mp4", "muziksiz.mp4", "olcu.txt" };
        KanitKapanisi.Onceki(Kanit, dosyalar);
        var kaynak = Path.Combine(Kanit, "kaynak.mp4");
        var muzikDosyasi = Path.Combine(Kanit, "muzik.m4a");
        Ffmpeg("-hide_banner", "-y",
            "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=3",
            "-f", "lavfi", "-i", "sine=f=440:d=3",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "15", "-threads", "2", "-c:a", "aac", "-shortest", kaynak);
        Ffmpeg("-hide_banner", "-y", "-f", "lavfi", "-i", "sine=f=3000:d=3", "-c:a", "aac", "-threads", "2", muzikDosyasi);
        var satirlar = new List<string>();
        var olcu = new Dictionary<string, (double Ust, double Bas, double Ses)>();

        foreach (var (muzik, ad) in new[] { (new EditMusic(muzikDosyasi, S(3), S(1), -6), "muzikli.mp4"), ((EditMusic?)null, "muziksiz.mp4") })
        {
            var cizelge = new EditTimeline(new[] { new EditClip(S(0), S(3)) });
            if (muzik is not null) Assert.True(cizelge.SetMusic(muzik));
            var cikti = Path.Combine(Kanit, ad);
            var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, cizelge, ExportMode.Full, cikti, 8 * Gb);
            await EditExportRunner.RunAsync(plan, new Bos());
            var sesSuresi = double.Parse(Ffprobe("-select_streams", "a", "-show_entries", "stream=duration", "-of", "csv=p=0", cikti).Trim(),
                CultureInfo.InvariantCulture);
            const string suzgec = "highpass=f=2000,highpass=f=2000,highpass=f=2000";
            var ust = OrtalamaDuzey(cikti, suzgec);
            var bas = OrtalamaDuzey(cikti, "atrim=0:0.9," + suzgec);
            olcu[ad] = (ust, bas, sesSuresi);
            satirlar.Add($"{ad} ust2k={ust:0.0}dB ilk0.9sn_ust2k={bas:0.0}dB ses={sesSuresi:0.###}sn");
            Assert.InRange(sesSuresi, 2.9, 3.1);
        }

        File.WriteAllLines(Path.Combine(Kanit, "olcu.txt"), satirlar);
        Assert.True(olcu["muzikli.mp4"].Ust > -40, string.Join(" | ", satirlar));
        Assert.True(olcu["muziksiz.mp4"].Ust < -60, string.Join(" | ", satirlar));
        Assert.True(olcu["muzikli.mp4"].Bas < -60, string.Join(" | ", satirlar));
        KanitKapanisi.Kapat(Kanit, dosyalar);
    }

    private static double OrtalamaDuzey(string dosya, string suzgec)
    {
        var hata = Ffmpeg("-hide_banner", "-nostdin", "-threads", "2", "-i", dosya, "-vn", "-af", suzgec + ",volumedetect", "-f", "null", "-");
        var eslesme = Regex.Match(hata, @"mean_volume:\s*(-?[\d.]+|-inf) dB");
        Assert.True(eslesme.Success, hata);
        return eslesme.Groups[1].Value == "-inf" ? double.NegativeInfinity : double.Parse(eslesme.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static string Ffmpeg(params string[] args)
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

        var metin = hata.GetAwaiter().GetResult();
        Assert.True(surec.ExitCode == 0, metin);
        return metin;
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
