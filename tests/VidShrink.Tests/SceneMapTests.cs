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
