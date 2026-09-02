using System.Globalization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class SceneMapTests
{
    [Fact]
    public void ParseScores_OrnekCiktiyiOkur()
    {
        var log = string.Join('\n',
            "[Parsed_metadata_1 @ 000001e48b726f00] frame:0    pts:16      pts_time:0.016",
            "[Parsed_metadata_1 @ 000001e48b726f00] lavfi.scene_score=0.080873",
            "[Parsed_metadata_1 @ 000001e48b726f00] frame:1    pts:4833    pts_time:4.833",
            "[Parsed_metadata_1 @ 000001e48b726f00] lavfi.scene_score=0.057346",
            "frame= 3600 fps=910 q=-0.0 Lsize=N/A");

        var scores = SceneDetector.ParseScores(log);

        Assert.Equal(2, scores.Count);
        Assert.Equal(0.016, scores[0].Time, 6);
        Assert.Equal(0.080873, scores[0].Score, 6);
        Assert.Equal(4.833, scores[1].Time, 6);
        Assert.Equal(0.057346, scores[1].Score, 6);
    }

    [Fact]
    public void CutTimes_EsikFiltreler()
    {
        var candidates = new[]
        {
            new SceneScore(2.0, 0.10),
            new SceneScore(5.0, 0.25),
            new SceneScore(8.0, 0.45),
            new SceneScore(12.0, 0.35)
        };

        Assert.Equal(new[] { 5.0, 8.0, 12.0 }, SceneMap.CutTimes(candidates, 0.2, 20.0));
        Assert.Equal(new[] { 8.0, 12.0 }, SceneMap.CutTimes(candidates, 0.3, 20.0));
        Assert.Equal(new[] { 8.0 }, SceneMap.CutTimes(candidates, 0.4, 20.0));
    }

    [Fact]
    public void CutTimes_MinimumAraligiUygular()
    {
        var candidates = new[]
        {
            new SceneScore(0.3, 0.9),
            new SceneScore(5.0, 0.9),
            new SceneScore(5.4, 0.9),
            new SceneScore(9.6, 0.9)
        };

        Assert.Equal(new[] { 5.0 }, SceneMap.CutTimes(candidates, 0.5, 10.0));
    }

    [Fact]
    public void CutTimes_VarsayilanEsikOlculenBandaKilitli()
    {
        var candidates = new[]
        {
            new SceneScore(2.0, 0.100),
            new SceneScore(4.0, 0.110)
        };

        var cuts = SceneMap.CutTimes(candidates, SceneMap.DefaultThreshold, 20.0);

        Assert.Equal(new[] { 4.0 }, cuts);
    }

    [Fact]
    public void CutTimes_SkorDusenAdayAralikSayacniIlerletmez()
    {
        var candidates = new[]
        {
            new SceneScore(2.0, 0.9),
            new SceneScore(2.5, 0.05),
            new SceneScore(3.2, 0.9)
        };

        Assert.Equal(new[] { 2.0, 3.2 }, SceneMap.CutTimes(candidates, 0.5, 20.0));
    }

    [Fact]
    public void CutTimes_VarsayilanAsgariAralikIkiYonluKiskacta()
    {
        var candidates = new[]
        {
            new SceneScore(2.0, 0.9),
            new SceneScore(2.9, 0.9),
            new SceneScore(3.05, 0.9),
            new SceneScore(4.0, 0.9)
        };

        Assert.Equal(new[] { 2.0, 3.05 }, SceneMap.CutTimes(candidates, 0.5, 20.0));
    }

    [Fact]
    public void CutTimes_VarsayilanAsgariAralikKaynaginSonunaDaUygulanir()
    {
        var candidates = new[]
        {
            new SceneScore(19.0, 0.9),
            new SceneScore(19.5, 0.9)
        };

        Assert.Equal(new[] { 19.0 }, SceneMap.CutTimes(candidates, 0.5, 20.0));
        Assert.Empty(SceneMap.CutTimes(new[] { new SceneScore(19.5, 0.9) }, 0.5, 20.0));
    }

    [Fact]
    public void ScanArgs_TabanEsigiFiltreyeGecer()
    {
        var varsayilan = SceneDetector.ScanArgs("girdi.mp4", "vstats.log");
        var acik = SceneDetector.ScanArgs("girdi.mp4", "vstats.log", 0.123);

        Assert.Equal("0.05", TabanEsigi(varsayilan));
        Assert.Equal("0.123", TabanEsigi(acik));

        Assert.Contains("girdi.mp4", varsayilan);
        Assert.Contains("vstats.log", varsayilan);
        Assert.Contains($"[b]scale={SceneDetector.ProbeWidth}:-2[enc]", FiltreGrafigi(varsayilan));
        Assert.Contains(SceneDetector.ProbePreset, varsayilan);
        Assert.Contains(SceneDetector.ProbeCrf.ToString(CultureInfo.InvariantCulture), varsayilan);
    }

    [Fact]
    public void ScanArgs_TabanEsigiKararElegininAltinda()
    {
        var taban = double.Parse(TabanEsigi(SceneDetector.ScanArgs("g.mp4", "v.log")), CultureInfo.InvariantCulture);

        Assert.True(taban < SceneMap.DefaultThreshold,
            $"gunluge giren taban {taban}, karar elegi {SceneMap.DefaultThreshold}: "
            + "taban karar eleginin ustune cikarsa esigi dusurmek etkisiz kalir.");
    }

    private static string FiltreGrafigi(string[] args)
    {
        var i = Array.IndexOf(args, "-filter_complex");
        Assert.True(i >= 0 && i + 1 < args.Length, "-filter_complex bulunamadi");
        return args[i + 1];
    }

    private static string TabanEsigi(string[] args)
    {
        var m = System.Text.RegularExpressions.Regex.Match(FiltreGrafigi(args), @"gte\(scene,([0-9.]+)\)");
        Assert.True(m.Success, $"gte(scene,X) bulunamadi: {FiltreGrafigi(args)}");
        return m.Groups[1].Value;
    }

    [Fact]
    public void CutTimes_SirasizAdaylariSiralar()
    {
        var candidates = new[]
        {
            new SceneScore(12.0, 0.9),
            new SceneScore(3.0, 0.9),
            new SceneScore(7.0, 0.9)
        };

        Assert.Equal(new[] { 3.0, 7.0, 12.0 }, SceneMap.CutTimes(candidates, 0.5, 20.0));
    }

    [Fact]
    public void ParseVstats_YalnizKodlayiciCiktisiniOkur()
    {
        var vstats = string.Join('\n',
            "out=  0 st=  0 frame=     0 q= -0.0 f_size= 172800 s_size=      168KiB time= 0.500 br= 2764800.0kbits/s avg_br= 2764800.0kbits/s",
            "out=  1 st=  0 frame=     0 q= 39.0 f_size=  49452 s_size=       48KiB time= 0.010 br= 23737.0kbits/s avg_br= 23737.0kbits/s type= I",
            "out=  1 st=  0 frame=     1 q= 30.0 f_size=   1200 s_size=       49KiB time= 0.027 br= 384.0kbits/s avg_br= 12000.0kbits/s type= P",
            "");

        var frames = SceneDetector.ParseVstats(vstats);

        Assert.Equal(2, frames.Count);
        Assert.Equal(0.010, frames[0].Time, 6);
        Assert.Equal(49452, frames[0].Size);
        Assert.Equal(0.027, frames[1].Time, 6);
        Assert.Equal(1200, frames[1].Size);
    }

    [Fact]
    public void Build_KareleriSahnelereBolerVeKarmasikligiHesaplar()
    {
        var candidates = new[] { new SceneScore(4.0, 0.9) };
        var frames = new[]
        {
            new ProbeFrame(0.0, 1000),
            new ProbeFrame(2.0, 3000),
            new ProbeFrame(4.0, 5000),
            new ProbeFrame(9.9, 7000),
            new ProbeFrame(10.0, 9999),
            new ProbeFrame(-1.0, 9999)
        };

        var map = SceneMap.Build(10.0, candidates, 0.5, frames);

        Assert.Equal(2, map.Scenes.Count);
        Assert.Equal(0.0, map.Scenes[0].Start);
        Assert.Equal(4.0, map.Scenes[0].End);
        Assert.Equal(4.0, map.Scenes[1].Start);
        Assert.Equal(10.0, map.Scenes[1].End);
        Assert.Equal(32000, map.Scenes[0].Bits);
        Assert.Equal(96000, map.Scenes[1].Bits);
        Assert.Equal(0.625, map.Scenes[0].Complexity, 9);
        Assert.Equal(1.25, map.Scenes[1].Complexity, 9);
        Assert.Equal(8000.0, map.Scenes[0].BitsPerSecond, 9);
        Assert.Equal(16000.0, map.Scenes[1].BitsPerSecond, 9);
    }

    [Fact]
    public void Build_SahnelerAraliksizVeSirali()
    {
        var candidates = new[]
        {
            new SceneScore(14.0, 0.6),
            new SceneScore(3.5, 0.7),
            new SceneScore(8.25, 0.8)
        };

        var map = SceneMap.Build(20.0, candidates, 0.5, Array.Empty<ProbeFrame>());

        Assert.Equal(4, map.Scenes.Count);
        Assert.Equal(0.0, map.Scenes[0].Start);
        Assert.Equal(20.0, map.Scenes[^1].End);
        for (var i = 1; i < map.Scenes.Count; i++)
        {
            Assert.Equal(map.Scenes[i - 1].End, map.Scenes[i].Start);
            Assert.True(map.Scenes[i].Start > map.Scenes[i - 1].Start);
            Assert.Equal(i, map.Scenes[i].Index);
        }
    }

    [Fact]
    public void Spearman_BilinenDegerler()
    {
        var x = new double[] { 1, 2, 3, 4 };

        Assert.Equal(1.0, SceneMap.Spearman(x, new double[] { 10, 20, 30, 40 }), 9);
        Assert.Equal(-1.0, SceneMap.Spearman(x, new double[] { 40, 30, 20, 10 }), 9);
        Assert.Equal(0.8, SceneMap.Spearman(x, new double[] { 10, 30, 20, 40 }), 9);
    }

    [FfmpegFact]
    public async Task ScanArgs_TabanEsigiAdayHavuzunuDaraltir()
    {
        var outDir = TestPaths.LiveOut("sahne-taban-esigi");
        Directory.CreateDirectory(outDir);
        var clip = Path.Combine(outDir, "kademeli.mp4");

        try
        {
            await MakeGradedClipAsync(clip);

            var genis = await SceneDetector.ScanAsync(clip, 0.01);
            var dar = await SceneDetector.ScanAsync(clip, 0.35);
            var varsayilan = await SceneDetector.ScanAsync(clip);
            Assert.True(genis.Ok, genis.Error);
            Assert.True(dar.Ok, dar.Error);
            Assert.True(varsayilan.Ok, varsayilan.Error);

            Assert.All(genis.Candidates, c => Assert.True(c.Score >= 0.01, $"{c.Score} < 0.01"));
            Assert.All(dar.Candidates, c => Assert.True(c.Score >= 0.35, $"{c.Score} < 0.35"));
            Assert.All(varsayilan.Candidates,
                c => Assert.True(c.Score >= SceneDetector.BaseThreshold, $"{c.Score} < {SceneDetector.BaseThreshold}"));

            Assert.NotEmpty(dar.Candidates);
            Assert.True(genis.Candidates.Count > varsayilan.Candidates.Count,
                $"genis={genis.Candidates.Count} varsayilan={varsayilan.Candidates.Count}");
            Assert.True(varsayilan.Candidates.Count > dar.Candidates.Count,
                $"varsayilan={varsayilan.Candidates.Count} dar={dar.Candidates.Count}");

            Assert.Contains(genis.Candidates, c => c.Score < SceneDetector.BaseThreshold);
            Assert.Contains(varsayilan.Candidates, c => c.Score < 0.12);
        }
        finally
        {
            try { Directory.Delete(outDir, recursive: true); } catch (IOException) { }
        }
    }

    [FfmpegFact]
    public async Task ScanArgs_SondaKolunu640UltrafastCrf23Kodlar()
    {
        var outDir = TestPaths.LiveOut("sahne-sonda-sabitleri");
        Directory.CreateDirectory(outDir);
        var clip = Path.Combine(outDir, "kademeli.mp4");

        try
        {
            await MakeGradedClipAsync(clip);

            var scan = await SceneDetector.ScanAsync(clip);
            Assert.True(scan.Ok, scan.Error);
            Assert.NotEmpty(scan.Frames);
            var sonda = scan.Frames.Sum(f => f.Size);

            var esles = await ReferenceBytesAsync(outDir, clip, "esles", 640, "ultrafast", 23);
            var darKare = await ReferenceBytesAsync(outDir, clip, "genislik", 320, "ultrafast", 23);
            var baskaOnAyar = await ReferenceBytesAsync(outDir, clip, "onayar", 640, "veryfast", 23);
            var baskaCrf = await ReferenceBytesAsync(outDir, clip, "crf", 640, "ultrafast", 30);

            Assert.InRange(Sapma(sonda, esles), 0.0, 0.02);
            Assert.True(Sapma(sonda, darKare) > 0.10, $"genislik ayirt edilemedi: {sonda} vs {darKare}");
            Assert.True(Sapma(sonda, baskaOnAyar) > 0.10, $"on ayar ayirt edilemedi: {sonda} vs {baskaOnAyar}");
            Assert.True(Sapma(sonda, baskaCrf) > 0.10, $"crf ayirt edilemedi: {sonda} vs {baskaCrf}");
        }
        finally
        {
            try { Directory.Delete(outDir, recursive: true); } catch (IOException) { }
        }
    }

    private static double Sapma(long a, long b) => Math.Abs(a - b) / (double)Math.Max(a, b);

    private static async Task<long> ReferenceBytesAsync(
        string outDir, string clip, string name, int width, string preset, int crf)
    {
        var path = Path.Combine(outDir, $"ref-{name}.264");
        var run = await FfmpegRunner.RunAsync(new[]
        {
            "-hide_banner", "-loglevel", "error", "-nostats",
            "-i", clip,
            "-an", "-vf", $"scale={width}:-2",
            "-c:v", "libx264", "-preset", preset, "-crf", crf.ToString(CultureInfo.InvariantCulture),
            "-f", "h264", "-y", path
        });
        Assert.True(run.Ok, run.StandardError);
        return new FileInfo(path).Length;
    }

    [FfmpegFact]
    public async Task ScanAsync_TabanEsigiIkiYonluKiskacta()
    {
        var outDir = TestPaths.LiveOut("sahne-taban-kiskac");
        Directory.CreateDirectory(outDir);
        var clip = Path.Combine(outDir, "kiskac.mp4");

        try
        {
            await MakeBracketClipAsync(clip);

            var olcek = await SceneDetector.ScanAsync(clip, 0.005);
            Assert.True(olcek.Ok, olcek.Error);

            var basamaklar = olcek.Candidates.Select(c => c.Score).OrderBy(s => s).ToArray();
            Assert.Equal(7, basamaklar.Length);
            Assert.All(basamaklar.Take(2), s => Assert.InRange(s, 0.020, 0.030));
            Assert.All(basamaklar.Skip(2).Take(2), s => Assert.InRange(s, 0.031, 0.044));
            Assert.All(basamaklar.Skip(4).Take(2), s => Assert.InRange(s, 0.052, 0.059));
            Assert.InRange(basamaklar[6], 0.085, 0.098);

            var alt = await SceneDetector.ScanAsync(clip, 0.03);
            var varsayilan = await SceneDetector.ScanAsync(clip);
            var ust = await SceneDetector.ScanAsync(clip, 0.06);
            Assert.True(alt.Ok, alt.Error);
            Assert.True(varsayilan.Ok, varsayilan.Error);
            Assert.True(ust.Ok, ust.Error);

            Assert.True(alt.Candidates.Count > varsayilan.Candidates.Count,
                $"taban 0,036'nin altina kaydi: alt={alt.Candidates.Count} varsayilan={varsayilan.Candidates.Count}");
            Assert.True(varsayilan.Candidates.Count > ust.Candidates.Count,
                $"taban 0,057'nin ustune cikti: varsayilan={varsayilan.Candidates.Count} ust={ust.Candidates.Count}");
        }
        finally
        {
            try { Directory.Delete(outDir, recursive: true); } catch (IOException) { }
        }
    }

    private static async Task MakeBracketClipAsync(string clip)
    {
        const string graph =
            "[1:v][2:v]hstack=inputs=2[a8];[4:v][5:v]hstack=inputs=2[a20];"
            + "[7:v][8:v]hstack=inputs=2[a24];[10:v][11:v]hstack=inputs=2[a40];"
            + "[0:v][a8][3:v][a20][6:v][a24][9:v][a40]concat=n=8:v=1:a=0";

        var args = new List<string> { "-hide_banner", "-loglevel", "error" };
        args.AddRange(new[] { "-f", "lavfi", "-i", "smptehdbars=duration=2:size=1280x720:rate=30" });
        args.AddRange(new[] { "-f", "lavfi", "-i", "smptehdbars=duration=2:size=1272x720:rate=30" });
        args.AddRange(new[] { "-f", "lavfi", "-i", "color=c=navy:duration=2:size=8x720:rate=30" });
        args.AddRange(new[] { "-f", "lavfi", "-i", "smptehdbars=duration=2:size=1280x720:rate=30" });
        args.AddRange(new[] { "-f", "lavfi", "-i", "smptehdbars=duration=2:size=1260x720:rate=30" });
        args.AddRange(new[] { "-f", "lavfi", "-i", "color=c=navy:duration=2:size=20x720:rate=30" });
        args.AddRange(new[] { "-f", "lavfi", "-i", "smptehdbars=duration=2:size=1280x720:rate=30" });
        args.AddRange(new[] { "-f", "lavfi", "-i", "smptehdbars=duration=2:size=1256x720:rate=30" });
        args.AddRange(new[] { "-f", "lavfi", "-i", "color=c=navy:duration=2:size=24x720:rate=30" });
        args.AddRange(new[] { "-f", "lavfi", "-i", "smptehdbars=duration=2:size=1280x720:rate=30" });
        args.AddRange(new[] { "-f", "lavfi", "-i", "smptehdbars=duration=2:size=1240x720:rate=30" });
        args.AddRange(new[] { "-f", "lavfi", "-i", "color=c=navy:duration=2:size=40x720:rate=30" });
        args.AddRange(new[]
        {
            "-filter_complex", graph,
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18", "-pix_fmt", "yuv420p",
            "-y", clip
        });

        var run = await FfmpegRunner.RunAsync(args.ToArray());
        Assert.True(run.Ok, run.StandardError);
    }

    private static async Task MakeGradedClipAsync(string clip)
    {
        const string graph =
            "[1:v][2:v]concat=n=2:v=1:a=0[sag];[0:v][sag]hstack=inputs=2[parca];"
            + "[4:v][5:v]hstack=inputs=2[serit];[parca][3:v][serit]concat=n=3:v=1:a=0";

        var run = await FfmpegRunner.RunAsync(new[]
        {
            "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", "testsrc2=duration=4:size=854x720:rate=30",
            "-f", "lavfi", "-i", "smptehdbars=duration=2:size=426x720:rate=30",
            "-f", "lavfi", "-i", "color=c=navy:duration=2:size=426x720:rate=30",
            "-f", "lavfi", "-i", "smptehdbars=duration=2:size=1280x720:rate=30",
            "-f", "lavfi", "-i", "smptehdbars=duration=2:size=1240x720:rate=30",
            "-f", "lavfi", "-i", "color=c=navy:duration=2:size=40x720:rate=30",
            "-filter_complex", graph,
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18", "-pix_fmt", "yuv420p",
            "-y", clip
        });
        Assert.True(run.Ok, run.StandardError);
    }

    [FfmpegFact]
    public async Task ScanAsync_YapayKesimiBulurVeHaritaCikarir()
    {
        var outDir = TestPaths.LiveOut("sahne-haritasi-test");
        Directory.CreateDirectory(outDir);
        var clip = Path.Combine(outDir, "kesimli-klip.mp4");

        var make = await FfmpegRunner.RunAsync(new[]
        {
            "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", "testsrc2=duration=2:size=320x180:rate=30",
            "-f", "lavfi", "-i", "smptehdbars=duration=2:size=320x180:rate=30",
            "-filter_complex", "[0:v][1:v]concat=n=2:v=1:a=0",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p",
            "-y", clip
        });
        Assert.True(make.Ok, make.StandardError);

        try
        {
            var scan = await SceneDetector.ScanAsync(clip);
            Assert.True(scan.Ok, scan.Error);

            var cuts = SceneMap.CutTimes(scan.Candidates, 0.4, 4.0);
            var cut = Assert.Single(cuts);
            Assert.InRange(cut, 1.8, 2.2);

            Assert.NotEmpty(scan.Frames);
            Assert.All(scan.Frames, f => Assert.InRange(f.Time, -0.5, 4.5));

            var (map, elapsed) = await SceneDetector.BuildMapAsync(clip, 4.0, 0.4);
            Assert.Equal(2, map.Scenes.Count);
            Assert.True(map.Scenes[0].Complexity > map.Scenes[1].Complexity);
            Assert.True(elapsed > TimeSpan.Zero);
        }
        finally
        {
            try { Directory.Delete(outDir, recursive: true); } catch (IOException) { }
        }
    }
}
