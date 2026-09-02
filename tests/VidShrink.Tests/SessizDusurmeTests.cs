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

    /// <summary>
    /// Olcum K3 — hicbir secenegi dusurulmemis, cikis kodu 0 ile biten <b>gercek yoklama</b>
    /// ciktilari. Sozluk bunlarin hicbirine takilmamali: takilirsa calisan bir kodlayici
    /// "desteklenmiyor" diye elenir, bu da sessiz dusurmeden daha kotudur.
    /// </summary>
    public static TheoryData<string, string> CleanProbes() => new()
    {
        {
            "libx265, motorun gercekten sordugu psy dizgisi",
            """
            Stream mapping:
              Stream #0:0 -> #0:0 (wrapped_avframe (native) -> hevc (libx265))
            Press [q] to stop, [?] for help
            x265 [info]: HEVC encoder version 4.3+2-5ab552e
            x265 [info]: build info [Windows][GCC 16.1.0][64 bit] 8bit+10bit+12bit
            x265 [info]: Main profile, Level-2 (Main tier)
            x265 [warning]: Source height < 720p; disabling lookahead-slices
            x265 [info]: AQ: mode / str / qg-size / cu-tree  : 2 / 1.0 / 32 / 1
            x265 [info]: Rate Control / qCompress            : CRF-28.0 / 0.60
            x265 [info]: tools: rd=3 psy-rd=2.00 early-skip rskip mode=1 signhide tmvp
            x265 [info]: tools: b-intra strong-intra-smoothing deblock sao dhdr10-info
            [out#0/null @ 000001ee16487b40] video:5KiB audio:0KiB subtitle:0KiB other streams:0KiB global headers:0KiB muxing overhead: unknown
            """
        },
        {
            "libsvtav1, temiz variance-boost dizgisi",
            """
            Stream mapping:
              Stream #0:0 -> #0:0 (wrapped_avframe (native) -> av1 (libsvtav1))
            Svt[info]: SVT [version]:	SVT-AV1 Encoder Lib v4.2.0-68-gc1e79b04f
            Svt[info]: SVT [config]: AQ mode / Variance Boost 					: 2 / 1
            Svt[info]: SVT [config]: QP scale compress strength 					: 0
            [out#0/null @ 000001e7418d8dc0] video:3KiB audio:0KiB subtitle:0KiB other streams:0KiB global headers:0KiB muxing overhead: unknown
            """
        },
        {
            "libx264, deprecated pixel format uyarisi",
            """
            Press [q] to stop, [?] for help
            [swscaler @ 0000017c24c5cb40] deprecated pixel format used, make sure you did set range correctly
            [libx264 @ 0000017c24c2dd00] profile High, level 1.2, 4:2:0, 8-bit
            [out#0/null @ 0000017c22dd6880] video:2KiB audio:0KiB subtitle:0KiB other streams:0KiB global headers:0KiB muxing overhead: unknown
            """
        },
        {
            "muxer uyarisi (bu makinede uretilemedi, sozlesmeden alindi)",
            """
            Past duration 0.999992 too large
            frame=    1 fps=0.0 q=28.0 Lsize=N/A time=00:00:00.03 bitrate=N/A speed=9.61x
            """
        }
    };

    [Theory]
    [MemberData(nameof(CleanProbes))]
    public void ACleanProbeIsNeverRejected(string label, string stderr)
    {
        Assert.True(EncoderCapabilities.OptionAccepted(0, stderr),
            $"{label}: calisan yoklama reddedildi — kodlayici sebepsiz elenir.");
    }

    [Fact]
    public void TheWordUnknownOnItsOwnDoesNotRejectAProbe()
    {
        Assert.True(EncoderCapabilities.OptionAccepted(
            0, "[out#0/null @ 0] video:5KiB audio:0KiB muxing overhead: unknown"));
        Assert.True(EncoderCapabilities.OptionAccepted(
            0, "x265 [warning]: Source height < 720p; disabling lookahead-slices"));
        Assert.True(EncoderCapabilities.OptionAccepted(
            0, "x265 [info]: tools: rd=3 psy-rd=2.00 early-skip rskip mode=1 signhide tmvp"));
    }

    /// <summary>Cikis kodu sifirdan farkliysa metin ne derse desin yoklama kabul etmez.</summary>
    [Fact]
    public void ANonZeroExitIsRejectedWhateverTheTextSays()
    {
        Assert.False(EncoderCapabilities.OptionAccepted(8, string.Empty));
        Assert.False(EncoderCapabilities.OptionAccepted(8, "x265 [info]: tools: rd=3 psy-rd=2.00"));
    }
}
