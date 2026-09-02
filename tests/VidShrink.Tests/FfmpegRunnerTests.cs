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
}
