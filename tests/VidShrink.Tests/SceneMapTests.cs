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
    public void CutTimes_SabitEsikOlculenBandaKilitli()
    {
        var candidates = new[]
        {
            new SceneScore(2.0, 0.100),
            new SceneScore(4.0, 0.110)
        };

        var cuts = SceneMap.CutTimes(candidates, SceneMap.FixedThreshold, 20.0);

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

        Assert.Equal("0.012", TabanEsigi(varsayilan));
        Assert.Equal("0.123", TabanEsigi(acik));
        Assert.Equal("0.0002", TabanEsigi(SceneDetector.ScanArgs("girdi.mp4", "vstats.log", 0.0002)));
        Assert.Equal(
            SceneDetector.BaseThreshold,
            double.Parse(TabanEsigi(varsayilan), CultureInfo.InvariantCulture),
            9);

        Assert.Contains("girdi.mp4", varsayilan);
        Assert.Contains("vstats.log", varsayilan);
        Assert.Contains($"[b]scale={SceneDetector.ProbeWidth}:-2[enc]", FiltreGrafigi(varsayilan));
        Assert.Contains(SceneDetector.ProbePreset, varsayilan);
        Assert.Contains(SceneDetector.ProbeCrf.ToString(CultureInfo.InvariantCulture), varsayilan);
    }

    [Fact]
    public void ScanArgs_TabanEsigiTureyenEsiginTabaniniAsmaz()
    {
        var taban = double.Parse(TabanEsigi(SceneDetector.ScanArgs("g.mp4", "v.log")), CultureInfo.InvariantCulture);
        var kural = ThresholdRule.Measured;

        Assert.True(taban <= kural.Floor,
            $"gunluge giren taban {taban}, kuralin alt kiskaci {kural.Floor}: "
            + "taban alt kiskacin ustune cikarsa esigi dusurmek etkisiz kalir.");
        Assert.True(taban < kural.Offset,
            $"taban {taban}, kuralin sabit terimi {kural.Offset}: taban sabit terime ulasirsa durgun icerikte de eleme yapmaz.");
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
    public void TuretilenEsik_KiskacHerIkiUctaBaglar()
    {
        var kural = ThresholdRule.Measured;

        Assert.Equal(0.08, kural.At(0.0), 9);
        Assert.Equal(0.1009, kural.At(0.01), 9);
        Assert.Equal(0.15, kural.At(1.0), 9);
        Assert.Equal(0.15, kural.At(0.05), 9);

        var alcakSabit = kural with { Offset = 0.02 };
        Assert.Equal(0.05, alcakSabit.At(0.0), 9);
        Assert.Equal(0.05, alcakSabit.At(0.01), 9);
        Assert.Equal(0.0409, (alcakSabit with { Floor = 0.0 }).At(0.01), 9);
    }

    [Fact]
    public void TuretilenEsik_KiskacUclariOlculenAralikla()
    {
        var kural = ThresholdRule.Measured;

        Assert.True(kural.Ceiling >= 0.1468,
            $"tavan {kural.Ceiling}, kaynakta olculen en yuksek turemis esik 0,1468: tavan altina inerse kural kendi araligini kirpar.");
        Assert.True(kural.Ceiling < 0.1568,
            $"tavan {kural.Ceiling}: olculen aralik + bir 0,01 basamagi disinda tavani koyan bir olcu yok.");

        Assert.True(kural.Floor <= kural.Offset,
            $"alt kiskac {kural.Floor}, sabit terim {kural.Offset}: alt kiskac sabit terimin ustune cikarsa kuralin durgun ucu susturulur.");
        Assert.True(kural.Floor >= SceneDetector.BaseThreshold,
            $"alt kiskac {kural.Floor}, tarama tabani {SceneDetector.BaseThreshold}: tabanin altindaki aday zaten gunluge girmez.");
    }

    [Fact]
    public void Agitation_YuzdelikBosKareleriSifirSayar()
    {
        var kural = ThresholdRule.Measured;
        var adaylar = Enumerable.Range(0, 100)
            .Select(i => new SceneScore(10.5 + 0.5 * i, 0.001 * (i + 1)))
            .ToArray();

        Assert.Equal(0.021, SceneMap.Agitation(adaylar, 50.0, 100.0, 10.0, kural), 9);
        Assert.Equal(0.0, SceneMap.Agitation(adaylar, 50.0, 100.0, 10.0, kural with { Percentile = 0.50 }), 9);
        Assert.Equal(0.061, SceneMap.Agitation(adaylar, 50.0, 100.0, 10.0, kural with { Percentile = 0.95 }), 9);
    }

    [Fact]
    public void Agitation_KomsulukGenisligiOlcuyuDegistirir()
    {
        var kural = ThresholdRule.Measured;
        var adaylar = Enumerable.Range(0, 100)
            .Select(i => new SceneScore(10.5 + 0.5 * i, 0.001 * (i + 1)))
            .ToArray();

        Assert.Equal(0.021, SceneMap.Agitation(adaylar, 50.0, 100.0, 10.0, kural), 9);
        Assert.Equal(0.061, SceneMap.Agitation(adaylar, 50.0, 100.0, 10.0, kural with { NeighbourhoodSeconds = 20.0 }), 9);
        Assert.Equal(0.001, SceneMap.Agitation(adaylar, 50.0, 100.0, 10.0, kural with { NeighbourhoodSeconds = 60.0 }), 9);
    }

    [Fact]
    public void DerivedCutTimes_AyniSkorDurgunKomsuluktaGecerHareketlideGecmez()
    {
        var kural = ThresholdRule.Measured;
        var aday = new SceneScore(100.0, 0.09);

        var durgun = new[] { aday };
        var hareketli = Enumerable.Range(0, 200)
            .Select(i => new SceneScore(60.5 + 0.2 * i, 0.02))
            .Append(aday)
            .ToArray();

        Assert.Equal(new[] { 100.0 }, SceneMap.DerivedCutTimes(durgun, 200.0, 10.0, kural));
        Assert.Empty(SceneMap.DerivedCutTimes(hareketli, 200.0, 10.0, kural));

        Assert.Equal(0.0, SceneMap.Agitation(durgun, 100.0, 200.0, 10.0, kural), 9);
        Assert.Equal(0.02, SceneMap.Agitation(hareketli, 100.0, 200.0, 10.0, kural), 9);
    }

    [Fact]
    public void DerivedCutTimes_EgimSifirlanirsaAyrimKaybolur()
    {
        var kural = ThresholdRule.Measured with { Slope = 0.0 };
        var aday = new SceneScore(100.0, 0.09);
        var hareketli = Enumerable.Range(0, 200)
            .Select(i => new SceneScore(60.5 + 0.2 * i, 0.02))
            .Append(aday)
            .ToArray();

        Assert.Equal(new[] { 100.0 }, SceneMap.DerivedCutTimes(hareketli, 200.0, 10.0, kural));
    }

    [Fact]
    public void BuildDerived_TekEsikBildirmezSabitYolBildirir()
    {
        var adaylar = new[] { new SceneScore(4.0, 0.9) };
        var kareler = new[] { new ProbeFrame(0.0, 1000), new ProbeFrame(5.0, 1000) };

        var turetilen = SceneMap.BuildDerived(10.0, adaylar, kareler, ThresholdRule.Measured);
        Assert.True(double.IsNaN(turetilen.Threshold));
        Assert.Equal(ThresholdRule.Measured, turetilen.Rule!.Value);
        Assert.Equal(2, turetilen.Scenes.Count);

        var sabit = SceneMap.Build(10.0, adaylar, SceneMap.FixedThreshold, kareler);
        Assert.Equal(SceneMap.FixedThreshold, sabit.Threshold, 9);
        Assert.Null(sabit.Rule);
    }

    [Fact]
    public void BuildDerived_SondaKaresiYoksaKareHiziniUydurmaz()
        => Assert.Throws<ArgumentException>(() => SceneMap.BuildDerived(
            10.0, new[] { new SceneScore(4.0, 0.9) }, Array.Empty<ProbeFrame>(), ThresholdRule.Measured));

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

            var genis = await SceneDetector.ScanAsync(clip, 0.003);
            var dar = await SceneDetector.ScanAsync(clip, 0.35);
            var varsayilan = await SceneDetector.ScanAsync(clip);
            Assert.True(genis.Ok, genis.Error);
            Assert.True(dar.Ok, dar.Error);
            Assert.True(varsayilan.Ok, varsayilan.Error);

            Assert.All(genis.Candidates, c => Assert.True(c.Score >= 0.003, $"{c.Score} < 0.003"));
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

            var olcek = await SceneDetector.ScanAsync(clip, 0.0002);
            Assert.True(olcek.Ok, olcek.Error);

            var basamaklar = olcek.Candidates.Select(c => c.Score).OrderBy(s => s).ToArray();
            Assert.Equal(6, basamaklar.Length);
            Assert.InRange(basamaklar[0], 0.0002, 0.0009);
            Assert.All(basamaklar.Skip(1).Take(2), s => Assert.InRange(s, 0.0095, 0.0105));
            Assert.All(basamaklar.Skip(3).Take(2), s => Assert.InRange(s, 0.0195, 0.0205));
            Assert.InRange(basamaklar[5], 0.0395, 0.0405);

            var alt = await SceneDetector.ScanAsync(clip, 0.0099);
            var varsayilan = await SceneDetector.ScanAsync(clip);
            var ust = await SceneDetector.ScanAsync(clip, 0.0201);
            Assert.True(alt.Ok, alt.Error);
            Assert.True(varsayilan.Ok, varsayilan.Error);
            Assert.True(ust.Ok, ust.Error);

            Assert.True(alt.Candidates.Count > varsayilan.Candidates.Count,
                $"taban 0,0100'un altina kaydi: alt={alt.Candidates.Count} varsayilan={varsayilan.Candidates.Count}");
            Assert.True(varsayilan.Candidates.Count > ust.Candidates.Count,
                $"taban 0,0200'un ustune cikti: varsayilan={varsayilan.Candidates.Count} ust={ust.Candidates.Count}");
        }
        finally
        {
            try { Directory.Delete(outDir, recursive: true); } catch (IOException) { }
        }
    }

    private static async Task MakeBracketClipAsync(string clip)
    {
        const string graph =
            "[1:v]eq=brightness=0.002[b1];[3:v]eq=brightness=0.012[b2];[5:v]eq=brightness=0.03[b3];"
            + "[0:v][b1][2:v][b2][4:v][b3]concat=n=6:v=1:a=0";

        var args = new List<string> { "-hide_banner", "-loglevel", "error" };
        for (var i = 0; i < 6; i++)
            args.AddRange(new[] { "-f", "lavfi", "-i", "smptehdbars=duration=1:size=1280x720:rate=30" });
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

            var (sabitHarita, elapsed) = await SceneDetector.BuildFixedMapAsync(clip, 4.0, 0.4);
            Assert.Equal(2, sabitHarita.Scenes.Count);
            Assert.True(sabitHarita.Scenes[0].Complexity > sabitHarita.Scenes[1].Complexity);
            Assert.True(elapsed > TimeSpan.Zero);

            var (harita, _) = await SceneDetector.BuildMapAsync(clip, 4.0);
            Assert.NotNull(harita.Rule);
            Assert.Equal(ThresholdRule.Measured, harita.Rule!.Value);
            Assert.True(double.IsNaN(harita.Threshold), "turetilen harita tek bir esik bildiriyor");
            Assert.Equal(2, harita.Scenes.Count);
        }
        finally
        {
            try { Directory.Delete(outDir, recursive: true); } catch (IOException) { }
        }
    }
}
