using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

public sealed class DuzenleyiciMuzikKismaTests
{
    private const long Gb = 1_000_000_000;

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static readonly double[] Kareler = { 0.0, 2, 4, 6, 8 };

    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "yan-muzik-kisma", "kisma");
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

    private static EditMusic Muzik(bool kisma = true, double miktar = 12, int tepki = 400)
        => new EditMusic("muzik.m4a", S(30), S(1), -6) { Duck = kisma, DuckDb = miktar, DuckReleaseMs = tepki };

    private static EditTimeline IkiKlip(EditMusic? muzik)
    {
        var cizelge = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(4), S(6)) });
        if (muzik is not null) Assert.True(cizelge.SetMusic(muzik));
        return cizelge;
    }

    private static ExportPlan Plan(EditTimeline cizelge, MediaInfo? bilgi = null)
        => EditExport.Build(cizelge, bilgi ?? Bilgi(), Kareler, 0, ExportMode.Full, "c.mp4", "is", 8 * Gb,
            DuzenleyiciAkilliKodekTests.Sinirlar(Kareler), _ => true);

    private static IReadOnlyList<string> Args(ExportPlan plan) => Assert.Single(plan.Steps).Args;

    private static string Grafik(ExportPlan plan) => Args(plan)[Args(plan).ToList().IndexOf("-filter_complex") + 1];

    private static List<string> Eslenen(IReadOnlyList<string> args)
        => args.Select((a, i) => (a, i)).Where(x => x.i > 0 && args[x.i - 1] == "-map").Select(x => x.a).ToList();

    [Fact]
    public void KismaAcikkenMuzikKonusmayaGoreSikistirilir()
    {
        var plan = Plan(IkiKlip(Muzik(tepki: 250)));
        var grafik = Grafik(plan);

        Assert.EndsWith(";[aout]" + EditExport.MergeAudioFormat + "[asrcin];[asrcin]asplit=2[asrc][duckkey];[mus][duckkey]"
            + "sidechaincompress=threshold=0.01:ratio=20:attack=20:release=250:level_sc=4:mix=0.749[musduck];"
            + "[asrc][musduck]amix=inputs=2:duration=first:dropout_transition=0:normalize=0[amix]", grafik);
        Assert.Equal(new[] { "[vout]", "[amix]" }, Eslenen(Args(plan)));
    }

    [Fact]
    public void KismaKapaliykenGrafikEskisiyleAyni()
    {
        var grafik = Grafik(Plan(IkiKlip(Muzik(kisma: false, miktar: 24, tepki: 1000))));

        Assert.DoesNotContain("sidechaincompress", grafik);
        Assert.DoesNotContain("asplit", grafik);
        Assert.EndsWith(";[aout]" + EditExport.MergeAudioFormat + "[asrc];[asrc][mus]amix=inputs=2:duration=first:dropout_transition=0:normalize=0[amix]", grafik);
    }

    [Fact]
    public void SessizKaynaktaKismaKurulmaz()
    {
        var plan = Plan(IkiKlip(Muzik()), Sessiz());

        Assert.DoesNotContain("sidechaincompress", Grafik(plan));
        Assert.Equal(new[] { "[vout]", "[mus]" }, Eslenen(Args(plan)));
    }

    [Theory]
    [InlineData(6, "0.499")]
    [InlineData(12, "0.749")]
    [InlineData(18, "0.874")]
    [InlineData(24, "0.937")]
    public void KismaMiktariKarisimOranina(double miktar, string mix)
        => Assert.EndsWith(":mix=" + mix, EditExport.DuckFilter(Muzik(miktar: miktar)));

    [Fact]
    public void VarsayilanlarVeSinirlar()
    {
        var muzik = new EditMusic("m.mp3", S(1));
        Assert.False(muzik.Duck);
        Assert.Equal(12, muzik.DuckDb);
        Assert.Equal(400, muzik.DuckReleaseMs);
        Assert.False(muzik.HasDuckSettings);
        Assert.True((muzik with { Duck = true }).HasDuckSettings);

        Assert.Throws<ArgumentOutOfRangeException>(() => muzik with { DuckDb = 2.9 });
        Assert.Throws<ArgumentOutOfRangeException>(() => muzik with { DuckDb = 30.1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => muzik with { DuckDb = double.NaN });
        Assert.Throws<ArgumentOutOfRangeException>(() => muzik with { DuckReleaseMs = 49 });
        Assert.Throws<ArgumentOutOfRangeException>(() => muzik with { DuckReleaseMs = 3001 });
        Assert.Equal(3, EditMusic.ClampDuck(0));
        Assert.Equal(30, EditMusic.ClampDuck(99));
        Assert.Equal(12, EditMusic.ClampDuck(double.NaN));
        Assert.Equal(50, EditMusic.ClampRelease(1));
        Assert.Equal(3000, EditMusic.ClampRelease(9999));

        var yerlesik = Muzik(miktar: 18, tepki: 150).Placed(S(5), -3);
        Assert.Equal((true, 18.0, 150, S(5), -3.0), (yerlesik.Duck, yerlesik.DuckDb, yerlesik.DuckReleaseMs, yerlesik.Start, yerlesik.LevelDb));
    }

    [Fact]
    public void ProjeDosyasiKismayiTasir()
    {
        var damga = new SourceStamp("kaynak.mp4", 10, 20);
        var muzik = Muzik(miktar: 18, tepki: 1000);

        var json = EditProject.From(damga, IkiKlip(muzik)).ToJson();
        Assert.Contains("\"duck\"", json);
        Assert.Equal(muzik, EditProject.Parse(json)!.Music);

        var kismasiz = EditProject.From(damga, IkiKlip(new EditMusic("muzik.m4a", S(30), S(1), -6))).ToJson();
        Assert.DoesNotContain("duck", kismasiz);
        var eski = EditProject.Parse(kismasiz)!.Music!;
        Assert.False(eski.Duck);
        Assert.Equal(EditMusic.DefaultDuckDb, eski.DuckDb);

        var bozuk = Regex.Replace(json, "\"duckDb\":\\s*18", "\"duckDb\": 99");
        Assert.NotEqual(json, bozuk);
        Assert.Null(EditProject.Parse(bozuk));
    }

    [Fact]
    public void KaydetmeAnahtariKismaylaDegisir()
    {
        var cizelge = IkiKlip(Muzik(kisma: false));
        var once = EditorView.Fingerprint(cizelge);

        Assert.True(cizelge.SetMusic(Muzik()));
        Assert.NotEqual(once, EditorView.Fingerprint(cizelge));
        var acik = EditorView.Fingerprint(cizelge);
        Assert.True(cizelge.SetMusic(Muzik(miktar: 6)));
        Assert.NotEqual(acik, EditorView.Fingerprint(cizelge));
        Assert.True(cizelge.Undo());
        Assert.Equal(acik, EditorView.Fingerprint(cizelge));
    }

    [FfmpegFact]
    public async Task CanliTeslimdeMuzikYalnizKonusmadaAlcalir()
    {
        string[] dosyalar = { "kaynak.mp4", "muzik.m4a", "kismali.mp4", "kismasiz.mp4", "olcu.txt" };
        KanitKapanisi.Onceki(Kanit, dosyalar);
        var kaynak = Path.Combine(Kanit, "kaynak.mp4");
        var muzikDosyasi = Path.Combine(Kanit, "muzik.m4a");
        Ffmpeg("-hide_banner", "-y",
            "-f", "lavfi", "-i", "testsrc2=s=160x120:r=15:d=4",
            "-f", "lavfi", "-i", "sine=f=300:d=4,volume='between(t,1,2)+between(t,3,4)':eval=frame",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "15", "-threads", "2", "-c:a", "aac", "-shortest", kaynak);
        Ffmpeg("-hide_banner", "-y", "-f", "lavfi", "-i", "sine=f=3000:d=4", "-c:a", "aac", "-threads", "2", muzikDosyasi);

        var satirlar = new List<string>();
        var olcu = new Dictionary<string, (double Konusma, double Sessiz1, double Sessiz2)>();
        foreach (var (kisma, ad) in new[] { (true, "kismali.mp4"), (false, "kismasiz.mp4") })
        {
            var cizelge = new EditTimeline(new[] { new EditClip(S(0), S(4)) });
            Assert.True(cizelge.SetMusic(new EditMusic(muzikDosyasi, S(4), 0, -6) { Duck = kisma, DuckDb = 12, DuckReleaseMs = 150 }));
            var cikti = Path.Combine(Kanit, ad);
            var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, cizelge, ExportMode.Full, cikti, 8 * Gb);
            Assert.Equal(kisma, Grafik(plan).Contains("sidechaincompress", StringComparison.Ordinal));
            await EditExportRunner.RunAsync(plan, new Bos());
            var deger = (Bant(cikti, 1.2, 1.9), Bant(cikti, 0.3, 0.9), Bant(cikti, 2.4, 2.9));
            olcu[ad] = deger;
            satirlar.Add(FormattableString.Invariant($"{ad} konusma={deger.Item1:0.0}dB sessiz1={deger.Item2:0.0}dB sessiz2={deger.Item3:0.0}dB"));
        }

        var dusus = olcu["kismasiz.mp4"].Konusma - olcu["kismali.mp4"].Konusma;
        satirlar.Add(FormattableString.Invariant($"konusmada_dusus={dusus:0.0}dB"));
        File.WriteAllLines(Path.Combine(Kanit, "olcu.txt"), satirlar);
        var ozet = string.Join(" | ", satirlar);
        Assert.True(olcu["kismasiz.mp4"].Konusma > -40, ozet);
        Assert.True(dusus >= 8 && dusus <= 14, ozet);
        Assert.True(Math.Abs(olcu["kismasiz.mp4"].Sessiz1 - olcu["kismali.mp4"].Sessiz1) < 1.5, ozet);
        Assert.True(Math.Abs(olcu["kismasiz.mp4"].Sessiz2 - olcu["kismali.mp4"].Sessiz2) < 1.5, ozet);
        KanitKapanisi.Kapat(Kanit, dosyalar);
    }

    private static double Bant(string dosya, double bas, double son)
    {
        var suzgec = FormattableString.Invariant($"atrim={bas}:{son},highpass=f=2000,highpass=f=2000,highpass=f=2000,volumedetect");
        var hata = Ffmpeg("-hide_banner", "-nostdin", "-threads", "2", "-i", dosya, "-vn", "-af", suzgec, "-f", "null", "-");
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

    private sealed class Bos : IProgress<EncodeProgress>
    {
        public void Report(EncodeProgress value)
        {
        }
    }
}
