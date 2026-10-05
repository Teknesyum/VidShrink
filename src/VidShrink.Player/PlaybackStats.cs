namespace VidShrink.Player;

/// <summary>
/// Oynatmanin o anki olcumu; her okuma motora yeniden sorar. <see cref="Decoder"/> motorun <c>hwdec-current</c> degeri:
/// <c>null</c> bilinmiyor, <c>no</c> yazilim, baska her ad donanim cozucusu. Hizlar bilinmiyorsa sonlu degil.
/// </summary>
public sealed record PlaybackStats(
    long DroppedFrames,
    long DecoderDroppedFrames,
    string? Decoder,
    double VideoBitsPerSecond,
    double DisplayedFramesPerSecond)
{
    public bool DecoderKnown => !string.IsNullOrWhiteSpace(Decoder);

    public bool HardwareDecoded => DecoderKnown && Decoder != "no";
}
