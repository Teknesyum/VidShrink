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
            if (error > worst)
            {
                worst = error;
                worstCase = FormattableString.Invariant(
                    $"{Path.GetFileName(info.FilePath)} {intent} istenen {quality:0.0} -> {result.TargetMb:0.####} MB / {result.PredictedQuality:0.###}");
            }
        }

        report.AppendLine(FormattableString.Invariant($"en kotu sapma {worst:0.###} puan ({worstCase}), {undershoots} eksik kalma"));
        _output.WriteLine(report.ToString());
        Write(report.ToString());

        // The inverse is defined as the smallest target that reaches the request, so it never
        // lands under it - the undershoot count is asserted at zero rather than tolerated.
        Assert.Equal(0, undershoots);

        // The overshoot is not free choice: where the quality curve steps, the first target that
        // clears the request clears it by the whole height of that step. The sweep above walks
        // 0,5-point requests from 20 to 95 on three sources at two intents and measures 2,78
        // points at worst (sample.mp4 Sharing, request 82 -> 58,81 MB / 84,78), so the gate is
        // 3,0 - the measured maximum rounded up, not a guess. A tighter gate could only be met by
        // returning a target that does not reach the requested quality.
        Assert.True(worst <= 3.0, $"En kotu sapma {worst:0.###} puan: {worstCase}");
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
        var worst = 0;
        foreach (var info in Sources())
        foreach (var quality in new[] { 20.0, 30.0, 40.0, 50.0, 60.0, 70.0, 80.0, 90.0 })
        {
            var result = PlanCalculator.TargetMbForQuality(info, new PlanOptions(), quality);
            worst = Math.Max(worst, result.Evaluations);
            _output.WriteLine(FormattableString.Invariant(
                $"{Path.GetFileName(info.FilePath)} istenen {quality:0}: {result.Evaluations} cagri, {result.Bound}"));
        }

        // Measured: 1 call when the request falls outside the reachable range, 5-25 when it is
        // inside. The gate is the measured worst case (25) plus the two coarse-scan steps that a
        // slightly different source shape could add.
        Assert.True(worst <= 27, $"En pahali arama {worst} BuildDetailed cagrisi surdu.");
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
