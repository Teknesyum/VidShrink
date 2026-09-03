using VidShrink.App.Playback;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// Karari veren yoklama yolu. ffmpeg taninmayan bir kodlayici anahtarini dusurup
/// <b>cikis kodu 0</b> ile donuyor; yoklama bunu "destekleniyor" diye kaydediyor ve motor
/// dusurulen ayari uretmeye devam ediyor. Buradaki metinler uydurma degil:
/// <c>docs/olcumler/sessiz-dusurme-sondada.md</c> altindaki olcumlerin ham stderr ciktisi,
/// bu makinede bugun olculdu.
/// </summary>
public sealed class SessizDusurmeTests
{
    /// <summary>
    /// Olcum A — sondanin kendi arguman sekliyle (<c>-loglevel info</c>, 256x256,
    /// <c>-frames:v 1</c>) libx265, <c>-x265-params zzznotreal=1</c>. Cikis kodu 0.
    /// </summary>
    internal const string X265DroppedKeyAtProbeShape = """
        Input #0, lavfi, from 'testsrc2=size=256x256:rate=30:duration=0.1':
          Duration: N/A, start: 0.000000, bitrate: N/A
          Stream #0:0: Video: wrapped_avframe, yuv420p, 256x256 [SAR 1:1 DAR 1:1], 30 fps, 30 tbr, 30 tbn
        Stream mapping:
          Stream #0:0 -> #0:0 (wrapped_avframe (native) -> hevc (libx265))
        Press [q] to stop, [?] for help
        [libx265 @ 000001eec9dac280] Unknown option: zzznotreal.
        x265 [info]: HEVC encoder version 4.3+2-5ab552e
        x265 [info]: build info [Windows][GCC 16.1.0][64 bit] 8bit+10bit+12bit
        x265 [warning]: Too few rows/columns, --wpp disabled
        x265 [info]: Main profile, Level-2.1 (Main tier)
        x265 [info]: tools: rd=3 psy-rd=2.00 early-skip rskip mode=1 signhide tmvp
        [out#0/null @ 000001eec81e2740] video:2KiB audio:0KiB subtitle:0KiB other streams:0KiB global headers:0KiB muxing overhead: unknown
        frame=    1 fps=0.0 q=31.7 Lsize=N/A time=00:00:00.03 bitrate=N/A speed=0.63x
        """;

    /// <summary>
    /// Olcum D — onizlemenin kodlanmis parca sekliyle (<c>-loglevel</c> verilmeden,
    /// <c>FfmpegArguments.BuildSegment</c>'in koydugu varsayilan seviyede) ayni dusurme.
    /// Tani satiri 39 satirin 7'sinde, yani <see cref="FfmpegRunner.ErrorTailLines"/>
    /// (8) kuyrugunun disinda kalir. Bu makinede bugun olculdu.
    /// </summary>
    internal const string X265DroppedKeyAtEncodedSegmentShape = """
        Input #0, lavfi, from 'testsrc2=size=256x256:rate=30:duration=0.1':
          Duration: N/A, start: 0.000000, bitrate: N/A
          Stream #0:0: Video: wrapped_avframe, yuv420p, 256x256 [SAR 1:1 DAR 1:1], 30 fps, 30 tbr, 30 tbn
        Stream mapping:
          Stream #0:0 -> #0:0 (wrapped_avframe (native) -> hevc (libx265))
        Press [q] to stop, [?] for help
        [libx265 @ 000001207e661280] Unknown option: zzznotreal.
        x265 [info]: HEVC encoder version 4.3+2-5ab552e
        x265 [info]: build info [Windows][GCC 16.1.0][64 bit] 8bit+10bit+12bit
        x265 [info]: using cpu capabilities: MMX2 SSE2Fast LZCNT SSSE3 SSE4.2 AVX FMA3 BMI2 AVX2
        x265 [info]: Main profile, Level-2 (Main tier)
        x265 [info]: Thread pool created using 16 threads
        x265 [warning]: Source height < 720p; disabling lookahead-slices
        x265 [info]: frame threads / pool features       : 4 / wpp(4 rows)
        x265 [info]: Slices                              : 1
        x265 [info]: Coding QT: max CU size, min CU size : 64 / 8
        x265 [info]: Residual QT: max TU size, max depth : 32 / 1 inter / 1 intra
        x265 [info]: ME / range / subpel / merge         : hex / 57 / 2 / 3
        x265 [info]: Keyframe min / max / scenecut / bias  : 25 / 250 / 40 / 5.00
        x265 [info]: Lookahead / bframes / badapt        : 20 / 4 / 2
        x265 [info]: b-pyramid / weightp / weightb       : 1 / 1 / 0
        x265 [info]: References / ref-limit  cu / depth  : 3 / off / on
        x265 [info]: AQ: mode / str / qg-size / cu-tree  : 2 / 1.0 / 32 / 1
        x265 [info]: Rate Control / qCompress            : CRF-28.0 / 0.60
        x265 [info]: tools: rd=3 psy-rd=2.00 early-skip rskip mode=1 signhide tmvp
        x265 [info]: tools: b-intra strong-intra-smoothing deblock sao dhdr10-info
        Output #0, null, to 'NUL':
          Metadata:
            encoder         : Lavf63.1.100
          Stream #0:0: Video: hevc, yuv420p(tv, progressive), 256x256 [SAR 1:1 DAR 1:1], q=2-31, 30 fps, 30 tbn
            Metadata:
              encoder         : Lavc63.1.100 libx265
            Side data:
              CPB properties: bitrate max/min/avg: 0/0/0 buffer size: 0 vbv_delay: N/A
        [out#0/null @ 000001207e61b240] video:5KiB audio:0KiB subtitle:0KiB other streams:0KiB global headers:0KiB muxing overhead: unknown
        frame=    1 fps=0.0 q=32.7 Lsize=N/A time=00:00:00.03 bitrate=N/A speed=1.47x elapsed=0:00:00.02
        x265 [info]: frame I:      1, Avg QP:32.71  kb/s: 625.44

        encoded 1 frames in 0.01s (90.91 fps), 625.44 kb/s, Avg QP:32.71
        """;

    /// <summary>
    /// Olcum C — sifirdan farkli cikis koduyla gelen ret (libx264, taninmayan ust duzey
    /// secenek <c>-zzznotreal</c>). Bu metin sozluktekilerden hicbirini tasimaz; kapiyi
    /// asan sey metin degil <c>cikis kodu 8</c>. Bu makinede bugun olculdu.
    /// </summary>
    internal const string NonZeroExitUnrelatedDiagnostic = """
        Unrecognized option 'zzznotreal'.
        Error splitting the argument list: Option not found
        """;

    /// <summary>Temiz libx265 kosumu — hicbir ayar dusmedi, cikis 0. Bu makinede bugun olculdu.</summary>
    internal const string CleanX265Run = """
        Input #0, lavfi, from 'testsrc2=size=256x256:rate=30:duration=0.1':
          Duration: N/A, start: 0.000000, bitrate: N/A
          Stream #0:0: Video: wrapped_avframe, yuv420p, 256x256 [SAR 1:1 DAR 1:1], 30 fps, 30 tbr, 30 tbn
        Stream mapping:
          Stream #0:0 -> #0:0 (wrapped_avframe (native) -> hevc (libx265))
        Press [q] to stop, [?] for help
        x265 [info]: HEVC encoder version 4.3+2-5ab552e
        x265 [warning]: Source height < 720p; disabling lookahead-slices
        x265 [info]: AQ: mode / str / qg-size / cu-tree  : 2 / 1.0 / 32 / 1
        x265 [info]: tools: rd=3 psy-rd=2.00 early-skip rskip mode=1 signhide tmvp
        Output #0, null, to 'NUL':
        [out#0/null @ 00000216e6f4bec0] video:5KiB audio:0KiB subtitle:0KiB other streams:0KiB global headers:0KiB muxing overhead: unknown
        frame=    1 fps=0.0 q=32.7 Lsize=N/A time=00:00:00.03 bitrate=N/A speed=1.23x elapsed=0:00:00.02
        x265 [info]: frame I:      1, Avg QP:32.71  kb/s: 625.44
        """;

    /// <summary>Temiz libx264 kosumu — hicbir ayar dusmedi, cikis 0. Bu makinede bugun olculdu.</summary>
    internal const string CleanX264Run = """
        Input #0, lavfi, from 'testsrc2=size=256x256:rate=30:duration=0.1':
          Duration: N/A, start: 0.000000, bitrate: N/A
          Stream #0:0: Video: wrapped_avframe, yuv420p, 256x256 [SAR 1:1 DAR 1:1], 30 fps, 30 tbr, 30 tbn
        Stream mapping:
          Stream #0:0 -> #0:0 (wrapped_avframe (native) -> h264 (libx264))
        Press [q] to stop, [?] for help
        [libx264 @ 0000018134722c40] using SAR=1/1
        [libx264 @ 0000018134722c40] profile High, level 1.3, 4:2:0, 8-bit
        Output #0, null, to 'NUL':
        [out#0/null @ 00000181346fc440] video:4KiB audio:0KiB subtitle:0KiB other streams:0KiB global headers:0KiB muxing overhead: unknown
        frame=    1 fps=0.0 q=29.0 Lsize=N/A time=00:00:00.03 bitrate=N/A speed=9.46x elapsed=0:00:00.00
        [libx264 @ 0000018134722c40] frame I:1     Avg QP:25.25  size:  3972
        [libx264 @ 0000018134722c40] kb/s:953.28
        """;

    /// <summary>Temiz libsvtav1 kosumu — banner uzun, hicbir ayar dusmedi. Bu makinede bugun olculdu.</summary>
    internal const string CleanSvtAv1Run = """
        Input #0, lavfi, from 'testsrc2=size=256x256:rate=30:duration=0.1':
        Stream mapping:
          Stream #0:0 -> #0:0 (wrapped_avframe (native) -> av1 (libsvtav1))
        Press [q] to stop, [?] for help
        Svt[info]: -------------------------------------------
        Svt[info]: SVT [version]:	SVT-AV1 Encoder Lib v4.2.0-68-gc1e79b04f
        Svt[info]: SVT [build]  :	GCC 16.1.0	 64 bit
        Svt[info]: SVT [config]: main profile	tier (auto)	level (auto)
        Svt[info]: SVT [config]: BRC mode / rate factor 					: CRF / 35.00
        Svt[info]: -------------------------------------------
        Output #0, null, to 'NUL':
        [out#0/null @ 000002be9ca287c0] video:2KiB audio:0KiB subtitle:0KiB other streams:0KiB global headers:0KiB muxing overhead: unknown
        frame=    1 fps=0.0 q=29.0 Lsize=N/A time=00:00:00.00 bitrate=N/A speed=   0x elapsed=0:00:00.03
        """;

    [Fact]
    public void TheProbeMustNotCallADroppedOptionSupported()
    {
        var accepted = EncoderCapabilities.OptionAccepted(0, X265DroppedKeyAtProbeShape);

        Assert.False(accepted,
            "ffmpeg 'Unknown option: zzznotreal.' yazip 0 ile dondu; yoklama bunu destekleniyor saymamali.");
    }

    [Fact]
    public void ANonZeroExitIsRejectedRegardlessOfDiagnosticText()
    {
        var accepted = EncoderCapabilities.OptionAccepted(8, NonZeroExitUnrelatedDiagnostic);

        Assert.False(accepted,
            "cikis kodu 8 iken metin sozlukteki hicbir deseni tasimasa da sonuc kabul olmamali.");
    }

    [Theory]
    [MemberData(nameof(CleanRuns))]
    public void ACleanRunIsNeverReadAsADroppedOption(string label, string stderr)
    {
        var accepted = EncoderCapabilities.OptionAccepted(0, stderr);

        Assert.True(accepted, $"{label}: hicbir ayar dusmedi, sozluk yine de reddetti (yanlis pozitif).");
    }

    public static IEnumerable<object[]> CleanRuns()
    {
        yield return new object[] { "temiz libx265", CleanX265Run };
        yield return new object[] { "temiz libx264", CleanX264Run };
        yield return new object[] { "temiz libsvtav1", CleanSvtAv1Run };
    }

    /// <summary>
    /// K4'un tuttugu karar: onizlemenin kodlanmis-parca kosumu <c>-loglevel</c> vermiyor,
    /// tani satiri varsayilan seviyede zaten basiliyor (Olcum D, satir 7). Bu satir
    /// <see cref="FfmpegRunner.ErrorTailLines"/> (8) kuyrugunun disinda kalsa da
    /// <see cref="FfmpegDiagnostics.DroppedOptionLines"/> tam metinden okudugu icin
    /// yakaliyor — kuyruk penceresi bu yolu kesmiyor.
    /// </summary>
    [Fact]
    public void ADroppedOptionOutsideTheTailIsStillCaught()
    {
        var lines = FfmpegDiagnostics.DroppedOptionLines(X265DroppedKeyAtEncodedSegmentShape);

        Assert.Contains(lines, line => line.Contains("Unknown option: zzznotreal.", StringComparison.Ordinal));

        var tail = FfmpegRunner.Tail(X265DroppedKeyAtEncodedSegmentShape);
        Assert.DoesNotContain("Unknown option:", tail, StringComparison.Ordinal);
    }

    /// <summary>
    /// K4'un ikinci yarisi: kaynak kesidin argumanlari (<c>-loglevel error</c>) sabit ve
    /// psy/AQ tasiyan hicbir kodlayici parametre dizgisi icermiyor, yani bu koşumda
    /// dusecek bir ayar yok — <c>-loglevel error</c>'un burada zarari olmuyor.
    /// </summary>
    [Fact]
    public void TheSourceClipArgumentsCarryNoDroppableEncoderOptions()
    {
        var args = SegmentEncoder.BuildSourceClipArguments("kaynak.mp4", 0, 2, "cikti.mp4");

        Assert.Contains("-loglevel", args);
        Assert.Contains("error", args);
        Assert.DoesNotContain(args, a => a.Contains("-params", StringComparison.Ordinal));
    }
}
