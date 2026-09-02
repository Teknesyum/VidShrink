using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// ffmpeg taninmayan bir kodlayici anahtarini <b>dusurup cikis kodu 0 ile</b> donuyor.
/// Buradaki metinler uydurma degil: `docs/olcumler/cikis-kodu-yalan.md` altindaki
/// olcumlerin ham stderr ciktisidir.
/// </summary>
public sealed class FfmpegRunnerTests
{
    /// <summary>Olcum A — libsvtav1, `-svtav1-params zzznotreal=1`, cikis kodu 0.</summary>
    internal const string SvtAv1DroppedKey = """
        Input #0, lavfi, from 'testsrc2=size=128x128:rate=30:duration=0.1':
          Duration: N/A, start: 0.000000, bitrate: N/A
          Stream #0:0: Video: wrapped_avframe, yuv420p, 128x128 [SAR 1:1 DAR 1:1], 30 fps, 30 tbr, 30 tbn
        Stream mapping:
          Stream #0:0 -> #0:0 (wrapped_avframe (native) -> av1 (libsvtav1))
        Press [q] to stop, [?] for help
        Svt[info]: -------------------------------------------
        Svt[info]: SVT [version]:	SVT-AV1 Encoder Lib v4.2.0-68-gc1e79b04f
        Svt[info]: SVT [build]  :	GCC 16.1.0	 64 bit
        Svt[info]: LIB Build date: Aug  2 2026 11:16:16
        Svt[info]: -------------------------------------------
        [libsvtav1 @ 00000138e021a4c0] Error parsing option zzznotreal: 1.
        Svt[info]: Level of Parallelism: 5
        Svt[info]: Number of PPCS 140
        Svt[info]: [asm level on system : up to avx512icl]
        Svt[info]: [asm level selected : up to avx512icl]
        Svt[info]: -------------------------------------------
        Svt[info]: SVT [config]: main profile	tier (auto)	level (auto)
        Svt[info]: SVT [config]: width / height / fps numerator / fps denominator 		: 128 / 128 / 30 / 1
        Svt[info]: SVT [config]: bit-depth / color format 					: 8 / YUV420
        Svt[info]: SVT [config]: preset / tune / pred struct 					: 8 / PSNR / random access
        Svt[info]: SVT [config]: gop size / mini-gop size / key-frame type 			: 161 / 32 / key frame
        Svt[info]: SVT [config]: BRC mode / rate factor 					: CRF / 35.00
        Svt[info]: SVT [config]: AQ mode / Variance Boost 					: 2 / 0
        Svt[info]: SVT [config]: sharpness / luminance-based QP bias 				: 0 / 0
        Svt[info]: SVT [config]: QP scale compress strength 					: 0
        Svt[info]: -------------------------------------------
        Output #0, null, to 'NUL':
          Metadata:
            encoder         : Lavf63.1.100
          Stream #0:0: Video: av1, yuv420p(tv, progressive), 128x128 [SAR 1:1 DAR 1:1], q=2-31, 30 fps, 30 tbn
            Metadata:
              encoder         : Lavc63.1.100 libsvtav1
        [out#0/null @ 00000138de6ea800] video:2KiB audio:0KiB subtitle:0KiB other streams:0KiB global headers:0KiB muxing overhead: unknown
        frame=    2 fps=0.0 q=35.0 Lsize=N/A time=00:00:00.03 bitrate=N/A speed=0.388x elapsed=0:00:00.08
        """;

    [Fact]
    public void ExitZeroWithADroppedOptionIsStillOkButTheDropIsCarried()
    {
        var run = FfmpegRunner.Decide(0, SvtAv1DroppedKey, TimeSpan.FromSeconds(1));

        Assert.True(run.Ok, "Calisan bir kodlama dusurulmus ayar yuzunden oldurulmez.");
        Assert.Equal(0, run.ExitCode);
        Assert.True(run.DroppedAnOption,
            "ffmpeg 'Error parsing option zzznotreal: 1.' yazip 0 ile dondu; dusurulen ayar sonuca tasinmali.");
        Assert.Contains(run.DroppedOptions!, line => line.Contains("zzznotreal"));
    }

    /// <summary>Olcum D — libx265, `-x265-params zzznotreal=1`, cikis kodu 0.</summary>
    internal const string X265DroppedKey = """
        Stream mapping:
          Stream #0:0 -> #0:0 (wrapped_avframe (native) -> hevc (libx265))
        Press [q] to stop, [?] for help
        [libx265 @ 000001b98a87d040] Unknown option: zzznotreal.
        x265 [info]: HEVC encoder version 4.3+2-5ab552e
        x265 [info]: build info [Windows][GCC 16.1.0][64 bit] 8bit+10bit+12bit
        x265 [warning]: Too few rows/columns, --wpp disabled
        x265 [warning]: Source height < 720p; disabling lookahead-slices
        x265 [info]: tools: rd=3 psy-rd=2.00 early-skip rskip mode=1 signhide tmvp
        [out#0/null @ 000001b988a289c0] video:4KiB audio:0KiB subtitle:0KiB other streams:0KiB global headers:0KiB muxing overhead: unknown
        """;

    /// <summary>Olcum D — libx264, `-x264-params zzznotreal=1`, cikis kodu 0.</summary>
    internal const string X264DroppedKey = """
        Stream mapping:
          Stream #0:0 -> #0:0 (wrapped_avframe (native) -> h264 (libx264))
        Press [q] to stop, [?] for help
        [libx264 @ 00000184d929db80] Error parsing option 'zzznotreal = 1'.
        [libx264 @ 00000184d929db80] using SAR=1/1
        [libx264 @ 00000184d929db80] profile High, level 1.1, 4:2:0, 8-bit
        [out#0/null @ 00000184d779c4c0] video:3KiB audio:0KiB subtitle:0KiB other streams:0KiB global headers:0KiB muxing overhead: unknown
        """;

    [Theory]
    [InlineData(nameof(SvtAv1DroppedKey))]
    [InlineData(nameof(X265DroppedKey))]
    [InlineData(nameof(X264DroppedKey))]
    public void EveryEncoderMeasuredAtExitZeroReportsItsDroppedKey(string fixture)
    {
        var stderr = fixture switch
        {
            nameof(SvtAv1DroppedKey) => SvtAv1DroppedKey,
            nameof(X265DroppedKey) => X265DroppedKey,
            _ => X264DroppedKey
        };

        var dropped = FfmpegDiagnostics.DroppedOptionLines(stderr);

        Assert.Contains(dropped, line => line.Contains("zzznotreal"));
    }

    [Fact]
    public void BothRunnersReadTheSameDictionary()
    {
        var viaFfmpegRunner = FfmpegRunner.Decide(0, X265DroppedKey, TimeSpan.Zero).DroppedOptions;

        var watch = new EncodeRunner.StderrWatch();
        foreach (var line in X265DroppedKey.Split('\n'))
            watch.Line(line.TrimEnd('\r'));
        var viaEncodeRunner = watch.Close(0).DroppedOptions;

        Assert.Equal(viaFfmpegRunner, viaEncodeRunner);
        Assert.NotEmpty(viaEncodeRunner);
    }

    /// <summary>
    /// Olcum K3 — <b>hicbir ayari dusurulmemis</b>, cikis kodu 0 ile biten gercek kosumlarin
    /// ciktisi. Sozluk bunlarin hicbirine takilmamali: takilirsa calisan bir kodlamayi
    /// dusurulmus sayariz, bu da sessiz kalite kaybindan daha kotudur.
    /// </summary>
    public static TheoryData<string, string> CleanRuns() => new()
    {
        {
            "libx265, iki uyari + psy-rd iceren tools satiri",
            """
            Stream mapping:
              Stream #0:0 -> #0:0 (wrapped_avframe (native) -> hevc (libx265))
            Press [q] to stop, [?] for help
            x265 [info]: HEVC encoder version 4.3+2-5ab552e
            x265 [info]: build info [Windows][GCC 16.1.0][64 bit] 8bit+10bit+12bit
            x265 [info]: using cpu capabilities: MMX2 SSE2Fast LZCNT SSSE3 SSE4.2 AVX FMA3 BMI2 AVX2
            x265 [info]: Main profile, Level-1 (Main tier)
            x265 [warning]: Too few rows/columns, --wpp disabled
            x265 [warning]: Source height < 720p; disabling lookahead-slices
            x265 [info]: AQ: mode / str / qg-size / cu-tree  : 2 / 1.0 / 32 / 1
            x265 [info]: Rate Control / qCompress            : CRF-28.0 / 0.60
            x265 [info]: tools: rd=3 psy-rd=2.00 early-skip rskip mode=1 signhide tmvp
            x265 [info]: tools: b-intra strong-intra-smoothing deblock sao dhdr10-info
            [out#0/null @ 000001f0d4ec52c0] video:4KiB audio:0KiB subtitle:0KiB other streams:0KiB global headers:0KiB muxing overhead: unknown
            encoded 2 frames in 0.02s (95.24 fps), 247.92 kb/s, Avg QP:32.73
            """
        },
        {
            "libx264, deprecated pixel format uyarisi",
            """
            Press [q] to stop, [?] for help
            [swscaler @ 000002cebc857980] deprecated pixel format used, make sure you did set range correctly
            [libx264 @ 000002cebc82dd00] using SAR=1/1
            [libx264 @ 000002cebc82dd00] profile High, level 1.1, 4:2:0, 8-bit
            [out#0/null @ 000002cebadd6880] video:3KiB audio:0KiB subtitle:0KiB other streams:0KiB global headers:0KiB muxing overhead: unknown
            [libx264 @ 000002cebc82dd00] Weighted P-Frames: Y:0.0% UV:0.0%
            """
        },
        {
            "ffmpeg surum banner'i ve kodlayici banner'lari",
            """
            ffmpeg version 9.0-full_build-www.gyan.dev Copyright (c) 2000-2026 the FFmpeg developers
              built with gcc 16.1.0 (Rev2, Built by MSYS2 project)
              configuration: --enable-gpl --enable-libsvtav1 --enable-libx264 --enable-libx265 --enable-libaom
              libavcodec     63.  1.100 / 63.  1.100
            Svt[info]: SVT [version]:	SVT-AV1 Encoder Lib v4.2.0-68-gc1e79b04f
            Svt[info]: SVT [config]: preset / tune / pred struct 					: 8 / PSNR / random access
            Svt[info]: SVT [config]: AQ mode / Variance Boost 					: 2 / 0
            Svt[info]: SVT [config]: QP scale compress strength 					: 0
            frame=    2 fps=0.0 q=35.0 Lsize=N/A time=00:00:00.03 bitrate=N/A speed=0.388x elapsed=0:00:00.08
            """
        },
        {
            "muxer uyarisi (bu makinede uretilemedi, sozlesmeden alindi)",
            """
            Past duration 0.999992 too large
            frame=  100 fps=0.0 q=28.0 Lsize=      42KiB time=00:00:03.30 bitrate=104.2kbits/s speed=9.61x
            """
        }
    };

    [Theory]
    [MemberData(nameof(CleanRuns))]
    public void CleanRunsAreNeverReadAsADroppedOption(string label, string stderr)
    {
        var dropped = FfmpegDiagnostics.DroppedOptionLines(stderr);

        Assert.True(dropped.Count == 0,
            $"{label}: calisan kodlama dusurulmus sayildi — {string.Join(" | ", dropped)}");
        Assert.False(FfmpegRunner.Decide(0, stderr, TimeSpan.Zero).DroppedAnOption, label);
    }

    [Fact]
    public void TheWordUnknownOnItsOwnIsNotEnough()
    {
        Assert.False(FfmpegDiagnostics.ReportsADroppedOption(
            "[out#0/null @ 000001b988a289c0] video:4KiB audio:0KiB muxing overhead: unknown"));
        Assert.False(FfmpegDiagnostics.ReportsADroppedOption(
            "x265 [warning]: Too few rows/columns, --wpp disabled"));
        Assert.False(FfmpegDiagnostics.ReportsADroppedOption(
            "[swscaler @ 0] deprecated pixel format used, make sure you did set range correctly"));
    }
}
