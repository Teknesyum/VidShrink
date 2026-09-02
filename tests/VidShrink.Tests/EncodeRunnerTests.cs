using System.Diagnostics;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class EncodeRunnerTests
{
    [Fact]
    public void PlanThatFilledTheBandDoesNotClaimADeliberateStop()
    {
        var filled = CrfPlan(ReasonCode.PredictedQualityMeasured, ReasonCode.FillCrfLowered);
        var twoPassFill = CrfPlan(ReasonCode.PredictedQualityMeasured, ReasonCode.FillTwoPassBandCenter);
        var narrowBandFill = CrfPlan(ReasonCode.PredictedQualityMeasured, ReasonCode.FillTwoPassBandTooNarrowForCrf);
        var stopped = CrfPlan(ReasonCode.PredictedQualityMeasured);

        Assert.False(filled.StopsShortOfBandOnPurpose);
        Assert.False(twoPassFill.StopsShortOfBandOnPurpose);
        Assert.False(narrowBandFill.StopsShortOfBandOnPurpose);
        Assert.True(stopped.StopsShortOfBandOnPurpose);

        var corrected = PlanCalculator.Correct(stopped, actualMb: 1.0, targetMb: 5.0, durationSeconds: 2);
        Assert.False(corrected.StopsShortOfBandOnPurpose);

        Assert.False(CrfPlan().StopsShortOfBandOnPurpose);
    }

    [FfmpegFact]
    public async Task PlannedStopAboveTheHardFloorIsDeliveredWithoutARetry()
    {
        await WithClipAsync(async (info, outputPath, naturalMb) =>
        {
            var targetMb = naturalMb / 0.88;
            var band = FillBand.For(targetMb);

            var result = await new EncodeRunner().RunAsync(
                info, CrfPlan(ReasonCode.PredictedQualityMeasured), outputPath, targetMb,
                progress: null, ct: CancellationToken.None, fillPolicy: FillPolicy.FillTarget);

            Assert.True(result.Success);
            Assert.InRange(result.OutputMb, band.HardFloorMb, band.LowerMb);
            Assert.Equal(1, result.Attempts);
            Assert.DoesNotContain(result.Trace ?? Array.Empty<EncodeAttempt>(), a => a.Branch == "under band");
        });
    }

    [FfmpegFact]
    public async Task UnderBandAccidentAboveTheHardFloorStillRetries()
    {
        await WithClipAsync(async (info, outputPath, naturalMb) =>
        {
            var targetMb = naturalMb / 0.88;

            var result = await new EncodeRunner().RunAsync(
                info, CrfPlan(ReasonCode.PredictedQualityMeasured, ReasonCode.FillCrfLowered), outputPath, targetMb,
                progress: null, ct: CancellationToken.None, fillPolicy: FillPolicy.FillTarget);

            Assert.True(result.Success);
            Assert.True(result.Attempts > 1,
                $"doldurmayi hedefleyen plan band altinda kalinca yeniden denemeliydi, {result.Attempts} deneme oldu");
            Assert.Contains(result.Trace ?? Array.Empty<EncodeAttempt>(), a => a.Branch == "under band");
        });
    }

    [FfmpegFact]
    public async Task PlannedStopUnderTheHardFloorStillRetries()
    {
        await WithClipAsync(async (info, outputPath, naturalMb) =>
        {
            var targetMb = naturalMb / 0.5;
            var band = FillBand.For(targetMb);
            Assert.True(naturalMb < band.HardFloorMb);

            var result = await new EncodeRunner().RunAsync(
                info, CrfPlan(ReasonCode.PredictedQualityMeasured), outputPath, targetMb,
                progress: null, ct: CancellationToken.None, fillPolicy: FillPolicy.FillTarget);

            Assert.True(result.Success);
            Assert.True(result.Attempts > 1,
                $"sert tabanin altina dusen bilerek durma yeniden denenmeliydi, {result.Attempts} deneme oldu");
            Assert.Contains(result.Trace ?? Array.Empty<EncodeAttempt>(), a => a.Branch == "under band");
        });
    }

    [FfmpegFact]
    public async Task TheSentenceShownToTheUserAgreesWithWhatTheRunnerDelivers()
    {
        await WithClipAsync(async (info, outputPath, naturalMb) =>
        {
            foreach (var factor in new[] { 0.88, 0.5 })
            {
                var targetMb = naturalMb / factor;
                var plan = StopPlan(naturalMb, targetMb);
                var promisesStop = MainWindow.ShowsMeasuredQualityStop(plan, plan.ReasonCodes[0], FillPolicy.FillTarget);

                var result = await new EncodeRunner().RunAsync(
                    info, plan, outputPath, targetMb,
                    progress: null, ct: CancellationToken.None, fillPolicy: FillPolicy.FillTarget);

                Assert.True(result.Success);
                Assert.True(promisesStop == (result.Attempts == 1),
                    $"hedef {targetMb:0.###} MB: arayuz 'burada durur' dedi={promisesStop}, kosucu tek denemede durdu={result.Attempts == 1}");
            }
        });
    }

    private static EncodePlan StopPlan(double ceilingMb, double targetMb)
    {
        var plan = CrfPlan();
        plan.ReasonCodes = new List<ReasonNote>
        {
            new(ReasonCode.BudgetExceedsCeiling, Crf: 30, Mb: ceilingMb, TargetMb: targetMb)
        };
        return plan;
    }

    private static EncodePlan CrfPlan(params ReasonCode[] codes) => new()
    {
        Codec = "libx264",
        Mode = "crf",
        Crf = 30,
        VideoBitrateK = 200,
        AudioCodec = null,
        AudioBitrateK = 0,
        Width = 320,
        Height = 240,
        Fps = 10,
        Preset = "ultrafast",
        ReasonCodes = codes.Select(code => new ReasonNote(code)).ToList()
    };

    private static async Task WithClipAsync(Func<MediaInfo, string, double, Task> body)
    {
        var dir = Path.Combine(TestPaths.OutputRoot, "encode-runner", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var source = Path.Combine(dir, "source.mp4");
            await RunFfmpegAsync(new[]
            {
                "-y", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=10:duration=4",
                "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", source
            });

            var info = new MediaInfo
            {
                FilePath = source,
                FileSizeBytes = new FileInfo(source).Length,
                DurationSeconds = 4,
                Width = 320,
                Height = 240,
                Fps = 10,
                VideoCodec = "h264",
                TotalBitrateBps = 400_000
            };

            var probePath = Path.Combine(dir, "natural.mp4");
            var natural = await new EncodeRunner().RunAsync(
                info, CrfPlan(), probePath, targetMb: 1000, progress: null,
                ct: CancellationToken.None, fillPolicy: FillPolicy.QualityCeiling);
            Assert.True(natural.Success);
            Assert.True(natural.OutputMb > 0);
            File.Delete(probePath);

            await body(info, Path.Combine(dir, "out.mp4"), natural.OutputMb);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    private static async Task RunFfmpegAsync(IEnumerable<string> args)
    {
        using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args) };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(stdout, stderr);
        Assert.Equal(0, process.ExitCode);
    }

    private sealed class Encoders : IEncoderAvailability
    {
        public bool HasEncoder(string name) => true;
        public bool WorksAsEncoder(string codec) => true;
    }

    /// <summary>
    /// Sahne uzunlugu sabit, duz bir harita. Turetilen haritada tek esik yoktur; <c>Threshold</c>
    /// <c>NaN</c> gelir ve kural <c>Rule</c> alaninda durur.
    /// </summary>
    private static SceneMap SceneMapOf(double sceneSeconds, double durationSeconds)
    {
        var scenes = new List<Scene>();
        for (var i = 0; i * sceneSeconds < durationSeconds; i++)
        {
            var start = i * sceneSeconds;
            var end = Math.Min(start + sceneSeconds, durationSeconds);
            scenes.Add(new Scene { Index = i, Start = start, End = end, Bits = 1_000_000, Complexity = 1.0 });
        }
        return new SceneMap
        {
            Threshold = double.NaN,
            Duration = durationSeconds,
            Scenes = scenes,
            Rule = ThresholdRule.Measured
        };
    }

    private static int KeyframeInterval(IReadOnlyList<string> args)
    {
        var at = args.ToList().IndexOf("-g");
        Assert.True(at >= 0, "arguments carry no -g");
        return int.Parse(args[at + 1], System.Globalization.CultureInfo.InvariantCulture);
    }

    private static MediaInfo Clip(double durationSeconds, double fps, string path = "kaynak.mp4") => new()
    {
        FilePath = path,
        FileSizeBytes = 10_000_000,
        DurationSeconds = durationSeconds,
        Width = 320,
        Height = 240,
        Fps = fps,
        VideoCodec = "h264",
        TotalBitrateBps = 400_000
    };

    [Fact]
    public void EncodeArgumentsCarryTheSceneMapCeiling()
    {
        var info = Clip(120, 10);
        var plan = CrfPlan();
        var mapped = EncodeRunner.EncodeArguments(info, plan, "out.mp4", 0, null, new Encoders(), SceneMapOf(3.0, 120));
        var unmapped = EncodeRunner.EncodeArguments(info, plan, "out.mp4", 0, null, new Encoders());

        Assert.Equal((int)Math.Round(plan.Fps * FfmpegArguments.KeyframeCeilingMinSeconds), KeyframeInterval(mapped));
        Assert.Equal((int)Math.Round(plan.Fps * FfmpegArguments.KeyframeCeilingDefaultSeconds), KeyframeInterval(unmapped));
    }

    [Fact]
    public void DisplayedCommandCarriesTheSameCeilingAsTheEncode()
    {
        var info = Clip(120, 10);
        var plan = CrfPlan();

        foreach (var map in new SceneMap?[] { null, SceneMapOf(3.0, 120), SceneMapOf(7.5, 120), SceneMapOf(40.0, 120) })
        {
            var shown = MainWindow.DisplayedEncodeArguments(info, plan, "out.mp4", new Encoders(), map);
            var run = EncodeRunner.EncodeArguments(info, plan, "out.mp4", 0, null, new Encoders(), map);
            Assert.Equal(KeyframeInterval(run), KeyframeInterval(shown));
        }
    }

    /// <summary>
    /// Arayuzun uc baglantisi ayri ayri pimlenir; biri bagliyken oteki bagsiz kalirsa
    /// kirmiziya donen olcu o baglantiya ait olur. Pencere bassiz kosumda kurulamadigi icin
    /// pim davranisa degil kaynak metnine bakar - projedeki mevcut kalip budur
    /// (FfmpegArgumentsTests.cs:405).
    /// </summary>
    [Fact]
    public void TheWindowFeedsTheMapToTheDisplayedCommand()
    {
        var source = File.ReadAllText(TipSources.WindowCodePath);

        Assert.Contains("DisplayedEncodeArguments(_info, plan,", source);
        Assert.Contains("_encoders, _sceneMap?.Map));", source);
    }

    [Fact]
    public void TheWindowFeedsTheMapToTheEncode()
    {
        var source = File.ReadAllText(TipSources.WindowCodePath);

        Assert.Contains("CurrentOptions().FillPolicy, _profile, AskBeforeRetryAsync, _sceneMap?.Map);", source);
    }

    [Fact]
    public void TheWindowBuildsTheMapWhenTheSourceLoads()
    {
        var source = File.ReadAllText(TipSources.WindowCodePath);

        Assert.Contains("_sceneMap = await EncodeRunner.TryBuildSceneMapAsync(info, ct: cts.Token);", source);
        Assert.Contains("_sceneMap = null;", source);
    }

    [Fact]
    public async Task AFailedScanFallsBackToTheDefaultCeilingAndSaysSo()
    {
        var info = Clip(120, 10);

        var missingFfmpeg = await EncodeRunner.TryBuildSceneMapAsync(info,
            (_, _) => Task.FromResult(new SceneScan(false, Array.Empty<SceneScore>(), Array.Empty<ProbeFrame>(),
                TimeSpan.Zero, "ffmpeg was not found.")));
        var brokenSource = await EncodeRunner.TryBuildSceneMapAsync(info,
            (_, _) => Task.FromResult(new SceneScan(false, Array.Empty<SceneScore>(), Array.Empty<ProbeFrame>(),
                TimeSpan.Zero, "Invalid data found when processing input")));
        var noFrames = await EncodeRunner.TryBuildSceneMapAsync(info,
            (_, _) => Task.FromResult(new SceneScan(true, Array.Empty<SceneScore>(), Array.Empty<ProbeFrame>(),
                TimeSpan.Zero, string.Empty)));
        var noDuration = await EncodeRunner.TryBuildSceneMapAsync(Clip(0, 10),
            (_, _) => throw new InvalidOperationException("the scan must not run without a duration"));

        Assert.Equal(SceneMapFallback.ScanFailed, missingFfmpeg.Fallback);
        Assert.Contains("ffmpeg", missingFfmpeg.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SceneMapFallback.ScanFailed, brokenSource.Fallback);
        Assert.Contains("Invalid data", brokenSource.Detail);
        Assert.Equal(SceneMapFallback.NoProbeFrames, noFrames.Fallback);
        Assert.Equal(SceneMapFallback.NoDuration, noDuration.Fallback);

        var plan = CrfPlan();
        var ceiling = (int)Math.Round(plan.Fps * FfmpegArguments.KeyframeCeilingDefaultSeconds);
        foreach (var attempt in new[] { missingFfmpeg, brokenSource, noFrames, noDuration })
        {
            Assert.False(attempt.Ok);
            Assert.Null(attempt.Map);
            Assert.Equal(ceiling, KeyframeInterval(EncodeRunner.EncodeArguments(info, plan, "out.mp4", 0, null, new Encoders(), attempt.Map)));
        }
    }

    [FfmpegFact]
    public async Task ABrokenSourceFallsBackInsteadOfThrowing()
    {
        var dir = Path.Combine(TestPaths.OutputRoot, "harita-baglantisi", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var broken = Path.Combine(dir, "bozuk.mp4");
            await File.WriteAllBytesAsync(broken, new byte[4096]);

            var attempt = await EncodeRunner.TryBuildSceneMapAsync(Clip(10, 10, broken));

            Assert.False(attempt.Ok);
            Assert.Equal(SceneMapFallback.ScanFailed, attempt.Fallback);
            Assert.NotEqual(string.Empty, attempt.Detail);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    /// <summary>
    /// Harita kosucuya verildiginde ciktinin I-kare sayisi degisir. Arguman degil davranis
    /// olculur: <c>RunOneAsync</c> haritayi dusurse arguman ureten test yesil kalirdi.
    /// </summary>
    [FfmpegFact]
    public async Task TheMapChangesTheIFrameCountOfTheDeliveredFile()
    {
        var dir = Path.Combine(TestPaths.OutputRoot, "harita-baglantisi", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var source = Path.Combine(dir, "kaynak.mp4");
            await RunFfmpegAsync(new[]
            {
                "-y", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=10:duration=30",
                "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-threads", "2", source
            });

            var info = Clip(30, 10, source);
            var map = SceneMapOf(3.0, 30);

            foreach (var mode in new[] { "crf", "2pass" })
            {
                var plan = CrfPlan();
                if (mode == "2pass") { plan.Mode = "2pass"; plan.Crf = null; plan.VideoBitrateK = 300; }

                var mappedPath = Path.Combine(dir, mode + "-haritali.mp4");
                var unmappedPath = Path.Combine(dir, mode + "-haritasiz.mp4");

                var mapped = await new EncodeRunner().RunAsync(info, plan, mappedPath, 1000, null,
                    CancellationToken.None, FillPolicy.QualityCeiling, null, null, map);
                var unmapped = await new EncodeRunner().RunAsync(info, plan, unmappedPath, 1000, null,
                    CancellationToken.None, FillPolicy.QualityCeiling);

                Assert.True(mapped.Success);
                Assert.True(unmapped.Success);

                var withMap = await CountIFramesAsync(mapped.OutputPath);
                var withoutMap = await CountIFramesAsync(unmapped.OutputPath);

                Assert.True(withMap > withoutMap,
                    $"{mode}: haritali {withMap} I-kare, haritasiz {withoutMap}; harita kosucuya ulasmiyor");
                Assert.Equal((int)(30.0 / FfmpegArguments.KeyframeCeilingMinSeconds), withMap);
                Assert.Equal((int)(30.0 / FfmpegArguments.KeyframeCeilingDefaultSeconds), withoutMap);
            }
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    private static async Task<int> CountIFramesAsync(string path)
    {
        using var process = new Process
        {
            StartInfo = ToolLocator.StartInfo(ToolLocator.Ffprobe, new[]
            {
                "-v", "error", "-select_streams", "v:0",
                "-show_entries", "frame=pict_type", "-of", "csv=p=0", path
            })
        };
        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return stdout.Split('\n').Count(line => line.Trim().TrimEnd(',') == "I");
    }
}

/// <summary>
/// Teslim yolunun basari kapisi. ffmpeg taninmayan bir kodlayici anahtarini dusurup
/// <b>cikis kodu 0</b> ile donuyor; motorun sectigi psikogorsel ayar sessizce kayboluyor.
/// Olcumler `docs/olcumler/cikis-kodu-yalan.md` altinda.
/// </summary>
public sealed class EncodeRunnerDroppedOptionTests
{
    [Fact]
    public void ExitZeroWithADroppedOptionDoesNotFailButTheDropIsCarried()
    {
        var watch = new EncodeRunner.StderrWatch();
        foreach (var line in FfmpegRunnerTests.SvtAv1DroppedKey.Split('\n'))
            watch.Line(line.TrimEnd('\r'));

        var outcome = watch.Close(0);

        EncodeRunner.ThrowIfFailed(outcome);
        Assert.NotEmpty(outcome.DroppedOptions);
        Assert.Contains(outcome.DroppedOptions, line => line.Contains("zzznotreal"));
    }

    [Fact]
    public void TheDiagnosticLineDoesNotSurviveTheTailWindow()
    {
        var watch = new EncodeRunner.StderrWatch();
        foreach (var line in FfmpegRunnerTests.SvtAv1DroppedKey.Split('\n'))
            watch.Line(line.TrimEnd('\r'));

        var outcome = watch.Close(0);

        Assert.DoesNotContain(outcome.Tail, line => line.Contains("Error parsing option"));
    }

    [FfmpegFact]
    public async Task ARealEncodeThatDropsAnOptionReportsTheDrop()
    {
        var outcome = await EncodeRunner.RunCommandAsync(
            new[]
            {
                "-hide_banner", "-f", "lavfi", "-i", "testsrc2=size=128x128:rate=30:duration=0.1",
                "-c:v", "libx264", "-x264-params", "zzznotreal=1", "-frames:v", "2",
                "-f", "null", OperatingSystem.IsWindows() ? "NUL" : "/dev/null"
            },
            durationSeconds: 0.1, progress: null, stage: "olcum", spanFrom: 0.0, spanTo: 1.0,
            ct: CancellationToken.None);

        Assert.Equal(0, outcome.ExitCode);
        Assert.Contains(outcome.DroppedOptions, line => line.Contains("zzznotreal"));
    }
}
