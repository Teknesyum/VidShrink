using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// Karari veren yoklama yolu. ffmpeg taninmayan bir kodlayici anahtarini dusurup
/// <b>cikis kodu 0</b> ile donuyor; yoklama bunu "destekleniyor" diye kaydediyor ve motor
/// dusurulen ayari uretmeye devam ediyor. Buradaki metinler uydurma degil:
/// <c>docs/olcumler/sessiz-dusurme-sondada.md</c> altindaki olcumlerin ham stderr ciktisi.
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

    [Fact]
    public void TheProbeMustNotCallADroppedOptionSupported()
    {
        var accepted = EncoderCapabilities.OptionAccepted(0, X265DroppedKeyAtProbeShape);

        Assert.False(accepted,
            "ffmpeg 'Unknown option: zzznotreal.' yazip 0 ile dondu; yoklama bunu destekleniyor saymamali.");
    }
}
