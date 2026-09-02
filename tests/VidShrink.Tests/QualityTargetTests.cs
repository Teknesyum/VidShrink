using System.Diagnostics;
using System.Globalization;
using System.Text;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit.Abstractions;

namespace VidShrink.Tests;

public sealed class QualityTargetTests
{
    private readonly ITestOutputHelper _output;

    public QualityTargetTests(ITestOutputHelper output) => _output = output;

    private const double LadderStepThreshold = 1.0;
    private const double OneScanStep = 1.005;

    private static MediaInfo SampleInfo() => new()
    {
        FilePath = "sample.mp4",
        FileSizeBytes = 500L * 1024 * 1024,
        DurationSeconds = 120,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 35_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2
    };

    private static MediaInfo ShortPhoneClip() => new()
    {
        FilePath = "phone.mp4",
        FileSizeBytes = 90L * 1024 * 1024,
        DurationSeconds = 42,
        Width = 1080,
        Height = 1920,
        Fps = 60,
        VideoCodec = "hevc",
        TotalBitrateBps = 18_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2
    };

    private static MediaInfo LongScreenCapture() => new()
    {
        FilePath = "capture.mkv",
        FileSizeBytes = 2200L * 1024 * 1024,
        DurationSeconds = 3600,
        Width = 2560,
        Height = 1440,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 5_100_000,
        AudioCodec = "aac",
        AudioBitrateBps = 96_000,
        AudioChannels = 2
    };

    private static IEnumerable<MediaInfo> Sources()
    {
        yield return SampleInfo();
        yield return ShortPhoneClip();
        yield return LongScreenCapture();
    }

    private static double QualityAt(MediaInfo info, PlanOptions options, double mb, ComplexityProfile? profile = null)
    {
        var probe = new PlanOptions
        {
            TargetMb = mb,
            Intent = options.Intent,
            Codec = options.Codec,
            AllowResolutionDrop = options.AllowResolutionDrop,
            AllowFpsDrop = options.AllowFpsDrop,
            HdrPolicy = options.HdrPolicy,
            FillPolicy = options.FillPolicy,
            SpeedMode = options.SpeedMode
        };
        return PlanCalculator.BuildDetailed(info, probe, profile).PredictedQuality;
    }

    [Fact]
    public void SameSourceAndQualityGiveTheSameTargetTwice()
    {
        var info = SampleInfo();
        var options = new PlanOptions { Intent = Intent.Sharing };

        var first = PlanCalculator.TargetMbForQuality(info, options, 60);
        var second = PlanCalculator.TargetMbForQuality(info, options, 60);

        Assert.Equal(first.TargetMb, second.TargetMb, 10);
        Assert.Equal(first.PredictedQuality, second.PredictedQuality, 10);
        Assert.Equal(first.Evaluations, second.Evaluations);
    }

    [Fact]
    public void TargetFedBackToBuildDetailedReproducesTheRequestedQuality()
    {
        var worst = 0.0;
        var lines = new List<string>();

        foreach (var info in Sources())
        foreach (var intent in new[] { Intent.Sharing, Intent.Archive })
        foreach (var quality in new[] { 30.0, 40.0, 50.0, 60.0, 70.0, 75.0, 80.0 })
        {
            var options = new PlanOptions { Intent = intent };
            var result = PlanCalculator.TargetMbForQuality(info, options, quality);
            if (result.Bound != QualityTargetBound.Matched) continue;

            var round = QualityAt(info, options, result.TargetMb);
            var error = Math.Abs(round - result.PredictedQuality);
            worst = Math.Max(worst, error);
            lines.Add(FormattableString.Invariant(
                $"{Path.GetFileName(info.FilePath)} {intent} istenen {quality:0} -> {result.TargetMb:0.###} MB, geri okunan {round:0.##}, sapma {error:0.###}, {result.Evaluations} arama"));
        }

        foreach (var line in lines) _output.WriteLine(line);

        // Feeding the returned target back into BuildDetailed has to reproduce the quality the
        // search reported for it, exactly - the search reports what BuildDetailed said at that
        // very target, so any difference here would mean the two calls disagree. The distance
        // from the request itself is a separate matter and is gated in
        // SearchLandsWithinTheMeasuredTolerance.
        Assert.True(worst <= 1e-9, $"Geri besleme sapmasi {worst:0.####} puan, beklenen 0.");
    }

    [Fact]
    public void SearchLandsWithinTheMeasuredTolerance()
    {
        var worst = 0.0;
        var worstCase = "";
        var undershoots = 0;
        var unexplained = new List<string>();
        var report = new StringBuilder();
        report.AppendLine("# T57 ters cevirme sapmasi");

        foreach (var info in Sources())
        foreach (var intent in new[] { Intent.Sharing, Intent.Archive })
        for (var quality = 20.0; quality <= 95.0; quality += 0.5)
        {
            var options = new PlanOptions { Intent = intent };
            var result = PlanCalculator.TargetMbForQuality(info, options, quality);
            if (result.Bound != QualityTargetBound.Matched) continue;

            if (result.QualityError < -1e-9) undershoots++;
            var error = Math.Abs(result.QualityError);
            report.AppendLine(FormattableString.Invariant(
                $"{Path.GetFileName(info.FilePath)} {intent} {quality:0.0} -> {result.TargetMb:0.####} MB / {result.PredictedQuality:0.###} (sapma {result.QualityError:+0.###;-0.###;0}) {result.Evaluations} cagri"));

            if (error > LadderStepThreshold)
            {
                var belowMb = result.TargetMb / OneScanStep;
                var below = PlanCalculator.BuildDetailed(info, new PlanOptions { TargetMb = belowMb, Intent = intent }, null).Plan;
                var here = result.Plan!.Plan;
                report.AppendLine(FormattableString.Invariant(
                    $"  basamak: {belowMb:0.####} MB {below.Width}x{below.Height}@{below.Fps:0.##} -> {result.TargetMb:0.####} MB {here.Width}x{here.Height}@{here.Fps:0.##}"));
                if (below.Width == here.Width && below.Height == here.Height && Math.Abs(below.Fps - here.Fps) < 0.01)
                    unexplained.Add(FormattableString.Invariant(
                        $"{Path.GetFileName(info.FilePath)} {intent} istenen {quality:0.0}: sapma {error:0.###} ama yerlesim {here.Width}x{here.Height}@{here.Fps:0.##} degismedi"));
            }

            if (error > worst)
            {
                worst = error;
                worstCase = FormattableString.Invariant(
                    $"{Path.GetFileName(info.FilePath)} {intent} istenen {quality:0.0} -> {result.TargetMb:0.####} MB / {result.PredictedQuality:0.###}");
            }
        }

        report.AppendLine(FormattableString.Invariant($"en kotu sapma {worst:0.###} puan ({worstCase}), {undershoots} eksik kalma, {unexplained.Count} aciklanmayan"));
        _output.WriteLine(report.ToString());
        Write(report.ToString());

        // The inverse is defined as the smallest target that reaches the request, so it never
        // lands under it - the undershoot count is asserted at zero rather than tolerated.
        Assert.Equal(0, undershoots);

        // Every overshoot past the old 1,0 gate has to be a step in the layout ladder, not a
        // search that stopped early: one scan step below the returned target the plan must be a
        // different layout. This is the claim the loosened gate below rests on, and it is
        // asserted rather than assumed.
        Assert.True(unexplained.Count == 0,
            $"{unexplained.Count} istekte 1,0 puani asan sapma yerlesim degisimiyle aciklanmiyor: {string.Join(" ; ", unexplained.Take(5))}");

        // The overshoot is what the curve gives at the first target that clears the request, so it
        // is the height of the layout ladder's step there. Round one gated at 3,0 blaming step
        // height; the 0,5% scan then measured 0,833 (phone.mp4 Sharing, request 80) and the gate
        // went to 1,0. T99 raised DefaultMotionExponent from 0,25 to the measured 0,871, the
        // 30 fps and the 6 fps layouts came within a point of each other, and the ladder grew a
        // taller step: capture.mkv Sharing, request 55,5 lands at 23,2606 MB / 58,875, 3,375
        // points out. Measured cause, sweeping the target in 0,1 MB steps: at 23,2 MB the winner
        // is 306x172@30 (55,49), at 23,3 MB it is 358x202@6 (58,875), and nothing in between
        // scores between the two. Nine requests in this sweep pass 1,0 and all nine are such
        // steps - the assertion above checks that, so this gate is the ladder's height and not a
        // tolerance for a sloppy search. The floor change (av1 0,020 -> 0,0095, hardware factor
        // 1,25 -> 1,52) was reverted on its own and moved this sweep by nothing.
        //
        // A tighter claim was tried and refused by measurement: "a target 0,1% below the returned
        // one must fail the request" is false, because predicted quality is not monotone in the
        // target at sub-percent scale. capture.mkv Sharing near 204,3 MB is the case - the audio
        // ladder steps 84k -> 85k there, video drops 383k -> 382k, and the winner flips back from
        // 818x460@30 (79,915) to 818x460@25 (78,995) for about 0,12 MB before flipping again.
        // That island belongs to PickAudio, not to the floor, and is left to its own contract.
        Assert.True(worst <= 3.5, $"En kotu sapma {worst:0.###} puan: {worstCase}");
    }

    [Fact]
    public void QualityAboveWhatTheSourceCanCarryIsReportedNotClipped()
    {
        var info = SampleInfo();
        var options = new PlanOptions { Intent = Intent.Sharing };

        var result = PlanCalculator.TargetMbForQuality(info, options, 99.5);

        Assert.Equal(QualityTargetBound.AboveSourceCeiling, result.Bound);
        Assert.True(result.PredictedQuality < 99.5);
        Assert.Equal(PlanCalculator.QualityCeilingTargetMb(info), result.TargetMb, 6);
        _output.WriteLine(FormattableString.Invariant(
            $"tavan {result.TargetMb:0.##} MB, oradaki kalite {result.PredictedQuality:0.##}"));
    }

    [Fact]
    public void QualityBelowTheSmallestPlanIsReportedNotClipped()
    {
        var info = LongScreenCapture();
        var options = new PlanOptions { Intent = Intent.Sharing };

        var result = PlanCalculator.TargetMbForQuality(info, options, 1);

        Assert.Equal(QualityTargetBound.BelowFloor, result.Bound);
        Assert.True(result.PredictedQuality > 1);
        Assert.Equal(PlanCalculator.QualityFloorTargetMb(info), result.TargetMb, 6);
        _output.WriteLine(FormattableString.Invariant(
            $"taban {result.TargetMb:0.###} MB, oradaki kalite {result.PredictedQuality:0.##}"));
    }

    [Fact]
    public void SearchCostIsBoundedAndCounted()
    {
        var report = new StringBuilder();
        report.AppendLine("# T57 arama maliyeti - tam supurme, sabit vaka listesi degil");
        var worstOverall = 0;
        var worstOverallCase = "";
        var worstByBound = new Dictionary<QualityTargetBound, int>();

        foreach (var info in Sources())
        foreach (var intent in new[] { Intent.Sharing, Intent.Archive })
        for (var quality = 20.0; quality <= 95.0; quality += 0.5)
        {
            var result = PlanCalculator.TargetMbForQuality(info, new PlanOptions { Intent = intent }, quality);
            worstByBound.TryGetValue(result.Bound, out var soFar);
            worstByBound[result.Bound] = Math.Max(soFar, result.Evaluations);
            if (result.Evaluations > worstOverall)
            {
                worstOverall = result.Evaluations;
                worstOverallCase = FormattableString.Invariant(
                    $"{Path.GetFileName(info.FilePath)} {intent} istenen {quality:0.0} ({result.Bound})");
            }
        }

        foreach (var pair in worstByBound.OrderBy(p => p.Key.ToString(), StringComparer.Ordinal))
            report.AppendLine(FormattableString.Invariant($"{pair.Key}: en pahali {pair.Value} cagri"));
        report.AppendLine(FormattableString.Invariant($"tumunde en pahali {worstOverall} cagri ({worstOverallCase})"));
        _output.WriteLine(report.ToString());
        Write(report.ToString());

        // Measured over the whole 0,5-point request sweep on every source and intent, not over a
        // hand-picked list - that is what let round 1 publish a wrong number. BelowFloor costs one
        // call and AboveSourceCeiling two; a Matched request costs the 0,5% scan walking from the
        // floor to the answer, and the gate below is the measured worst of that sweep: 1315 calls
        // (sample.mp4 Sharing, request 94 - the answer sits just under the ceiling, so the scan
        // walks nearly the whole range), about 240 ms at ~0,18 ms a call.
        Assert.Equal(1, worstByBound[QualityTargetBound.BelowFloor]);
        Assert.Equal(2, worstByBound[QualityTargetBound.AboveSourceCeiling]);
        Assert.True(worstOverall <= 1320, $"En pahali arama {worstOverall} BuildDetailed cagrisi surdu: {worstOverallCase}");
    }

    [Fact]
    public void TargetMbPathIsUnchanged()
    {
        var info = SampleInfo();
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing };

        var direct = PlanCalculator.BuildDetailed(info, options, null);

        Assert.Equal(25, options.TargetMb);
        Assert.True(direct.PredictedQuality > 0);
        Assert.NotEmpty(direct.Plan.ReasonCodes);
    }

    private static double ScanAnswer(MediaInfo info, PlanOptions options, double quality, double step, ComplexityProfile? profile, ref int calls)
    {
        var floor = PlanCalculator.QualityFloorTargetMb(info);
        var ceiling = PlanCalculator.QualityCeilingTargetMb(info);
        var span = ceiling / floor;
        var steps = Math.Max(1, (int)Math.Ceiling(Math.Log(span) / Math.Log(step)));
        var low = floor;
        var hit = ceiling;
        for (var i = 1; i < steps; i++)
        {
            var mb = floor * Math.Pow(span, (double)i / steps);
            calls++;
            if (QualityAt(info, options, mb, profile) >= quality) { hit = mb; break; }
            low = mb;
        }
        for (var i = 0; i < 10; i++)
        {
            var mid = Math.Sqrt(low * hit);
            if (mid <= low * 1.000001 || mid >= hit * 0.999999) break;
            calls++;
            if (QualityAt(info, options, mid, profile) >= quality) hit = mid; else low = mid;
        }
        return hit;
    }

    [Fact]
    public void SearchReturnsTheSmallestTargetThatReachesTheRequest()
    {
        var report = new StringBuilder();
        report.AppendLine("# T57 uretim aramasi vs 0,15% izgara gercegi");
        var worst = 1.0;
        var worstCase = "";

        foreach (var info in Sources())
        foreach (var intent in new[] { Intent.Sharing, Intent.Archive })
        foreach (var quality in new[] { 76.0, 78.0, 80.0, 82.0, 84.0, 86.0, 88.0, 90.0 })
        {
            var options = new PlanOptions { Intent = intent };
            var result = PlanCalculator.TargetMbForQuality(info, options, quality);
            if (result.Bound != QualityTargetBound.Matched) continue;

            var truthCalls = 0;
            var truth = ScanAnswer(info, options, quality, 1.0015, null, ref truthCalls);
            var ratio = result.TargetMb / truth;
            report.AppendLine(FormattableString.Invariant(
                $"{Path.GetFileName(info.FilePath)} {intent} {quality:0}: uretim {result.TargetMb:0.####} MB, gercek {truth:0.####} MB, oran x{ratio:0.0000}, {result.Evaluations} cagri"));
            if (ratio > worst)
            {
                worst = ratio;
                worstCase = FormattableString.Invariant(
                    $"{Path.GetFileName(info.FilePath)} {intent} {quality:0}: {result.TargetMb:0.####} MB vs {truth:0.####} MB");
            }
        }

        report.AppendLine(FormattableString.Invariant($"en kotu fazlalik x{worst:0.0000} ({worstCase})"));
        _output.WriteLine(report.ToString());
        Write(report.ToString());

        // The gate that the round-1 search failed. A 31% grid answered up to 3,83x too large
        // because it stepped over a peak; the 0,5% scan is measured at x1,0000 against a 0,15%
        // grid, so the gate is 1,01 - room for the scan's own step, nothing like a missed peak.
        Assert.True(worst <= 1.01, $"Arama gercegin x{worst:0.0000} kati buyuk bir hedef verdi: {worstCase}");
    }

    [Fact]
    public void ScanResolutionIsChosenByConvergence()
    {
        var report = new StringBuilder();
        report.AppendLine("# T57 tarama cozunurlugu yakinsamasi");
        var steps = new[] { 1.31, 1.10, 1.05, 1.02, 1.01, 1.005 };
        var worst = new Dictionary<double, double>();
        var cost = new Dictionary<double, int>();
        foreach (var st in steps) { worst[st] = 1.0; cost[st] = 0; }

        foreach (var info in Sources())
        foreach (var intent in new[] { Intent.Sharing, Intent.Archive })
        foreach (var quality in new[] { 76.0, 78.0, 80.0, 82.0, 84.0, 86.0, 88.0, 90.0 })
        {
            var options = new PlanOptions { Intent = intent };
            var floor = PlanCalculator.QualityFloorTargetMb(info);
            var ceiling = PlanCalculator.QualityCeilingTargetMb(info);
            if (QualityAt(info, options, floor) >= quality) continue;
            if (QualityAt(info, options, ceiling) < quality) continue;

            var truthCalls = 0;
            var truth = ScanAnswer(info, options, quality, 1.0015, null, ref truthCalls);
            var line = FormattableString.Invariant($"{Path.GetFileName(info.FilePath)} {intent} {quality:0}: gercek {truth:0.####} MB ({truthCalls} cagri)");
            foreach (var st in steps)
            {
                var calls = 0;
                var answer = ScanAnswer(info, options, quality, st, null, ref calls);
                var ratio = answer / truth;
                worst[st] = Math.Max(worst[st], ratio);
                cost[st] = Math.Max(cost[st], calls);
                line += FormattableString.Invariant($" | {st:0.000}: {answer:0.####} x{ratio:0.000} ({calls})");
            }
            report.AppendLine(line);
        }

        foreach (var st in steps)
            report.AppendLine(FormattableString.Invariant($"adim {st:0.000} -> en kotu fazlalik x{worst[st]:0.000}, en pahali {cost[st]} cagri"));
        _output.WriteLine(report.ToString());
        Write(report.ToString());
        Assert.True(worst[1.005] < 2.0);
    }

    // K3: the inversion assumes quality rises with the target. That assumption is measured
    // here rather than believed. The sweep walks the target upwards in log steps and records
    // every place the predicted quality falls back.
    [Fact]
    public void MonotonicityOfQualityAgainstTargetIsMeasuredNotAssumed()
    {
        var report = new StringBuilder();
        report.AppendLine("# T57 tekduzelik olcumu - sentetik kaynaklar, olculmemis profil");
        var worstDrop = 0.0;
        var drops = 0;
        var samples = 0;

        foreach (var info in Sources())
        foreach (var intent in new[] { Intent.Sharing, Intent.Archive })
        {
            var options = new PlanOptions { Intent = intent };
            var lo = PlanCalculator.QualityFloorTargetMb(info);
            var hi = PlanCalculator.QualityCeilingTargetMb(info);
            var previous = double.NaN;
            var previousMb = 0.0;
            report.AppendLine(FormattableString.Invariant(
                $"## {Path.GetFileName(info.FilePath)} {intent} [{lo:0.###} .. {hi:0.##}] MB"));

            for (var i = 0; i <= 200; i++)
            {
                var mb = lo * Math.Pow(hi / lo, i / 200.0);
                var q = QualityAt(info, options, mb);
                samples++;
                if (!double.IsNaN(previous) && q < previous - 1e-9)
                {
                    drops++;
                    var drop = previous - q;
                    worstDrop = Math.Max(worstDrop, drop);
                    report.AppendLine(FormattableString.Invariant(
                        $"  DUSUS {previousMb:0.####} MB {previous:0.###} -> {mb:0.####} MB {q:0.###} (fark {drop:0.###})"));
                }
                previous = q;
                previousMb = mb;
            }
        }

        report.AppendLine(FormattableString.Invariant(
            $"toplam {samples} ornek, {drops} dusus, en buyuk dusus {worstDrop:0.###} puan"));
        _output.WriteLine(report.ToString());
        Write(report.ToString());

        Assert.True(samples > 0);
    }

    [FfmpegFact]
    public async Task MonotonicityOnRealSourcesIsMeasuredNotAssumed()
    {
        var root = Path.Combine(TipSources.Root, ".calisma", "t57");
        Directory.CreateDirectory(root);

        var clips = new (string Name, string Source)[]
        {
            ("hareketli-1080p30-120s.mp4", "testsrc2=size=1920x1080:rate=30:duration=120"),
            ("hareketli-720p60-120s.mp4", "testsrc2=size=1280x720:rate=60:duration=120")
        };

        var report = new StringBuilder();
        report.AppendLine("# T57 tekduzelik olcumu - gercek klipler, olculmus profil");
        var worstDrop = 0.0;
        var drops = 0;
        var samples = 0;

        foreach (var (name, source) in clips)
        {
            var path = Path.Combine(root, name);
            if (!File.Exists(path)) await Encode(source, path);

            var info = await FfprobeClient.ProbeAsync(path);
            var profile = await ComplexityProbe.RunAsync(info, SpeedMode.Quality);
            var options = new PlanOptions { Intent = Intent.Sharing };
            var lo = PlanCalculator.QualityFloorTargetMb(info);
            var hi = PlanCalculator.QualityCeilingTargetMb(info);
            report.AppendLine(FormattableString.Invariant(
                $"## {name} {info.Width}x{info.Height}@{info.Fps:0.##} {info.FileSizeMb:0.##} MB, olculdu={profile.Measured}, bppf {profile.ReferenceBppf:0.0000}, ayrinti ussu {profile.DetailExponent:0.00}"));
            report.AppendLine(FormattableString.Invariant($"   aralik [{lo:0.###} .. {hi:0.##}] MB"));

            var previous = double.NaN;
            var previousMb = 0.0;
            for (var i = 0; i <= 120; i++)
            {
                var mb = lo * Math.Pow(hi / lo, i / 120.0);
                var q = QualityAt(info, options, mb, profile);
                samples++;
                report.AppendLine(FormattableString.Invariant($"   {mb:0.#####}\t{q:0.###}"));
                if (!double.IsNaN(previous) && q < previous - 1e-9)
                {
                    drops++;
                    var drop = previous - q;
                    worstDrop = Math.Max(worstDrop, drop);
                    report.AppendLine(FormattableString.Invariant(
                        $"   DUSUS {previousMb:0.####} MB {previous:0.###} -> {mb:0.####} MB {q:0.###} (fark {drop:0.###})"));
                }
                previous = q;
                previousMb = mb;
            }

            foreach (var quality in new[] { 60.0, 70.0, 75.0, 80.0, 85.0, 90.0 })
            {
                var result = PlanCalculator.TargetMbForQuality(info, options, quality, profile);
                var round = QualityAt(info, options, result.TargetMb, profile);
                report.AppendLine(FormattableString.Invariant(
                    $"   TERS istenen {quality:0} -> {result.TargetMb:0.####} MB, kalite {result.PredictedQuality:0.###}, geri okunan {round:0.###}, sinir {result.Bound}, {result.Evaluations} cagri"));
            }
        }

        report.AppendLine(FormattableString.Invariant(
            $"toplam {samples} ornek, {drops} dusus, en buyuk dusus {worstDrop:0.###} puan"));
        _output.WriteLine(report.ToString());
        Write(report.ToString());

        Assert.True(samples > 0);
    }

    private static void Write(string text)
    {
        var root = Path.Combine(TipSources.Root, ".calisma", "t57");
        Directory.CreateDirectory(root);
        File.AppendAllText(Path.Combine(root, "olcum.txt"),
            $"=== {DateTime.Now.ToString("s", CultureInfo.InvariantCulture)} ==={Environment.NewLine}{text}{Environment.NewLine}");
    }

    private static async Task Encode(string source, string path)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
        {
            "-y", "-f", "lavfi", "-i", source, "-c:v", "libx264", "-preset", "veryfast",
            "-crf", "23", "-pix_fmt", "yuv420p", path
        }) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        await stderr;
        await stdout;
    }


}
