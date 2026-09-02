using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class EncoderCapabilitiesTests
{
    private const string EncodersWithoutSvtav1 = """
        Encoders:
         V..... = Video
         A..... = Audio
         S..... = Subtitle
         .F.... = Frame-level multithreading
         ..S... = Slice-level multithreading
         ...X.. = Codec is experimental
         ....B. = Supports draw_horiz_band
         .....D = Supports direct rendering method 1
        -------
         V..... libx264              libx264 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10 (codecs: h264)
         V..... libx265              libx265 H.265 / HEVC (codecs: hevc)
         V..... h264_nvenc           NVIDIA NVENC H.264 encoder (codecs: h264)
         A..... aac                  AAC (Advanced Audio Coding)
        """;

    private const string EncodersWithSvtav1 = """
        Encoders:
         V..... = Video
        -------
         V..... libx264              libx264 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10 (codecs: h264)
         V..... libx265              libx265 H.265 / HEVC (codecs: hevc)
         V..... libsvtav1            SVT-AV1(codecs: av1)
        """;

    private const string Filters = """
        Filters:
          T.. = Timeline support
          C.. = Command support
          |   = Source or sink filter
         ... zscale            V->V       Apply resize, colorspace and bit depth conversion.
         ... tonemap           V->V       Conversion to/from different dynamic ranges.
        """;

    private const string Version = "ffmpeg version 9.0-full_build-www.gyan.dev Copyright (c) 2000-2026\n";

    [Fact]
    public void ParseEncodersIgnoresHeaderAndSeparatorLines()
    {
        var caps = EncoderCapabilities.Parse(EncodersWithoutSvtav1, Filters, Version);

        Assert.True(caps.HasEncoder("libx264"));
        Assert.True(caps.HasEncoder("libx265"));
        Assert.True(caps.HasEncoder("h264_nvenc"));
        Assert.False(caps.HasEncoder("libsvtav1"));
        Assert.True(caps.HasFilter("zscale"));
        Assert.True(caps.HasFilter("tonemap"));
        Assert.Equal("9.0-full_build-www.gyan.dev Copyright (c) 2000-2026", caps.Version);
    }

    [Fact]
    public void MissingLibsvtav1DoesNotCrashPlanGenerationAndFallsBackToLibx265()
    {
        var caps = EncoderCapabilities.Parse(EncodersWithoutSvtav1, Filters, Version);
        var info = new MediaInfo
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
        var options = new PlanOptions { TargetMb = 25, Intent = Intent.Sharing, Codec = CodecPreference.MaxCompression };

        var result = PlanCalculator.BuildDetailed(info, options, null, caps);

        Assert.Equal("libx265", result.Plan.Codec);
        Assert.Contains(result.Plan.ReasonCodes, n => n.Code == ReasonCode.EncoderFallback);
        Assert.Contains("falls back to libx265", result.Plan.Reason);
    }

    [Fact]
    public void PresentLibsvtav1IsSelectedForMaxCompression()
    {
        var caps = EncoderCapabilities.Parse(EncodersWithSvtav1, Filters, Version);
        Assert.True(caps.HasEncoder("libsvtav1"));
    }

    // --- K4: ucuncu durum iki olcuyle tutuluyor ---

    private static EncoderCapabilities Capabilities() =>
        EncoderCapabilities.Parse(EncodersWithoutSvtav1, Filters, Version);

    /// <summary>
    /// K4 birinci olcu: yoklama sonuca varamadiginda sonuc "calismiyor" degil
    /// "olculemedi"dir, karar yazilima duser ve <b>onbellege yazilmaz</b> - bir sonraki
    /// cagri yeniden olcer. Ucuncu durum "calismiyor" kovasina dusurulurse bu olcu kirilir.
    /// </summary>
    [Fact]
    public void AnUnmeasuredProbeIsNeitherWorkingNorCachedAndFallsBackToSoftware()
    {
        var caps = Capabilities();
        var calls = 0;
        caps.EncoderProbeHook = _ => { calls++; return EncoderCapabilities.ProbeOutcome.Unmeasured; };

        var first = caps.Probe("h264_nvenc");
        var second = caps.Probe("h264_nvenc");

        Assert.Equal(EncoderProbeState.Unmeasured, first.State);
        Assert.False(first.Measured);
        Assert.False(first.Succeeded);
        Assert.Equal(EncoderProbeState.Unmeasured, caps.EncoderState("h264_nvenc"));

        // Olculemeyen sonuc onbellege girmedigi icin ikinci cagri yeniden yokluyor.
        Assert.Equal(3, calls);
        Assert.Equal(EncoderProbeState.Unmeasured, second.State);

        var verdict = HardwareVerdict.Decide(first, 12_000, 1920, 1080, 30);
        Assert.False(verdict.EnableFastMode);
        Assert.False(verdict.Measured);
    }

    /// <summary>
    /// K4 ikinci olcu: yoklama kostu ve kodlayici reddettiyse sonuc olculmustur, geri
    /// dusme kalicidir ve tekrar yoklanmaz. "Calismiyor" ucuncu duruma dusurulurse bu
    /// olcu kirilir.
    /// </summary>
    [Fact]
    public void AMeasuredRejectionIsCachedAndStaysDistinctFromUnmeasured()
    {
        var caps = Capabilities();
        var calls = 0;
        caps.EncoderProbeHook = _ => { calls++; return EncoderCapabilities.ProbeOutcome.Rejected; };

        var first = caps.Probe("h264_nvenc");
        var second = caps.Probe("h264_nvenc");

        Assert.Equal(EncoderProbeState.NotWorking, first.State);
        Assert.True(first.Measured);
        Assert.False(first.Succeeded);
        Assert.Equal(EncoderProbeState.NotWorking, caps.EncoderState("h264_nvenc"));

        // Olculmus red onbellege girdi: ikinci cagri surec dogurmuyor.
        Assert.Equal(1, calls);
        Assert.Same(first, second);

        var verdict = HardwareVerdict.Decide(first, 12_000, 1920, 1080, 30);
        Assert.False(verdict.EnableFastMode);
        Assert.True(verdict.Measured);
    }

    /// <summary>ffmpeg kodlayiciyi hic listelemiyorsa bu olculmus bir yokluktur.</summary>
    [Fact]
    public void AnEncoderMissingFromTheListIsMeasuredNotUnmeasured()
    {
        var caps = Capabilities();
        caps.EncoderProbeHook = _ => throw new InvalidOperationException("Listede olmayan kodlayici yoklanmamali.");

        var probe = caps.Probe("libsvtav1");

        Assert.Equal(EncoderProbeState.NotWorking, probe.State);
        Assert.True(probe.Measured);
    }

    // --- K5: olculemeyen secenek yoklamasi onbellege girmiyor ---

    [Fact]
    public void AnUnmeasuredOptionProbeIsNotCached()
    {
        var caps = Capabilities();
        var outcome = EncoderCapabilities.ProbeOutcome.Unmeasured;
        var calls = 0;
        caps.OptionProbeHook = (_, _, _) => { calls++; return outcome; };

        Assert.False(caps.WarmEncoderOption("libx264", "-tune", "film"));
        Assert.False(caps.SupportsEncoderOption("libx264", "-tune", "film"));
        Assert.Equal(1, calls);

        // Ayni anahtar false olarak muhurlenmedi: kaynak serbestleyince olculebiliyor.
        outcome = EncoderCapabilities.ProbeOutcome.Accepted;
        Assert.True(caps.WarmEncoderOption("libx264", "-tune", "film"));
        Assert.True(caps.SupportsEncoderOption("libx264", "-tune", "film"));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void AMeasuredOptionRejectionIsCached()
    {
        var caps = Capabilities();
        var calls = 0;
        caps.OptionProbeHook = (_, _, _) => { calls++; return EncoderCapabilities.ProbeOutcome.Rejected; };

        Assert.False(caps.WarmEncoderOption("libx264", "-tune", "film"));
        Assert.False(caps.WarmEncoderOption("libx264", "-tune", "film"));

        Assert.Equal(1, calls);
    }

    // --- K6: kilit ffmpeg suresince tutulmuyor ---

    private const int SlowProbeMs = 2000;
    private const int ReaderBudgetMs = 750;

    /// <summary>
    /// Yavas bir yoklama surerken baska bir kodlayicinin yoklamasi kilidin arkasinda
    /// beklemiyor. Kilit surec boyunca tutulsaydi okuyan <see cref="SlowProbeMs"/> kadar
    /// beklerdi; butce onun ucte birinden az.
    /// </summary>
    [Fact]
    public async Task ASlowProbeDoesNotBlockAnotherRead()
    {
        var caps = Capabilities();
        using var probeStarted = new ManualResetEventSlim(false);
        caps.EncoderProbeHook = codec =>
        {
            if (!codec.Equals("h264_nvenc", StringComparison.OrdinalIgnoreCase))
                return EncoderCapabilities.ProbeOutcome.Accepted;
            probeStarted.Set();
            Thread.Sleep(SlowProbeMs);
            return EncoderCapabilities.ProbeOutcome.Accepted;
        };

        var slow = Task.Run(() => caps.Probe("h264_nvenc"));
        Assert.True(probeStarted.Wait(TimeSpan.FromSeconds(5)), "Yavas yoklama hic baslamadi.");

        var stopwatch = Stopwatch.StartNew();
        var fast = caps.Probe("libx264");
        stopwatch.Stop();

        Assert.True(fast.Succeeded);
        Assert.True(
            stopwatch.ElapsedMilliseconds < ReaderBudgetMs,
            $"Okuma {stopwatch.ElapsedMilliseconds} ms bekledi; kilit ffmpeg suresince tutuluyor.");

        var slowResult = await slow;
        Assert.True(slowResult.Succeeded);
    }

    /// <summary>Ayni kural secenek onbelleginde: isitma surerken okuma bloke olmuyor.</summary>
    [Fact]
    public async Task ASlowOptionProbeDoesNotBlockCachedOptionReads()
    {
        var caps = Capabilities();
        using var probeStarted = new ManualResetEventSlim(false);
        caps.OptionProbeHook = (_, _, _) =>
        {
            probeStarted.Set();
            Thread.Sleep(SlowProbeMs);
            return EncoderCapabilities.ProbeOutcome.Accepted;
        };

        var warming = Task.Run(() => caps.WarmEncoderOption("libx264", "-tune", "film"));
        Assert.True(probeStarted.Wait(TimeSpan.FromSeconds(5)), "Isitma hic baslamadi.");

        var stopwatch = Stopwatch.StartNew();
        var read = caps.SupportsEncoderOption("libx265", "-tune", "grain");
        stopwatch.Stop();

        Assert.False(read);
        Assert.True(
            stopwatch.ElapsedMilliseconds < ReaderBudgetMs,
            $"Okuma {stopwatch.ElapsedMilliseconds} ms bekledi; kilit ffmpeg suresince tutuluyor.");

        Assert.True(await warming);
    }

    /// <summary>
    /// Kilit daraldi ama sonuc kararsizlasmadi: ayni kodlayiciya ayni anda giren onaltisi
    /// da ayni sonucu goruyor ve onbellekte tek bir deger kaliyor.
    /// </summary>
    [Fact]
    public void ConcurrentProbesAgreeOnOneResult()
    {
        var caps = Capabilities();
        caps.EncoderProbeHook = _ =>
        {
            Thread.Sleep(20);
            return EncoderCapabilities.ProbeOutcome.Accepted;
        };

        var results = new EncoderProbeResult[16];
        Parallel.For(0, results.Length, i => results[i] = caps.Probe("h264_nvenc"));

        Assert.All(results, r => Assert.Equal(EncoderProbeState.Working, r.State));
        var settled = caps.Probe("h264_nvenc");
        Assert.All(results, r => Assert.Same(settled, r));
    }
}

