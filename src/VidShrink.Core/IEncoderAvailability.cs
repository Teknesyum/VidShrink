namespace VidShrink.Core;

public interface IEncoderAvailability
{
    bool HasEncoder(string name);

    /// <summary>
    /// "Bu kodlayıcı gerçekten kodluyor mu" sorusunun iki durumlu cevabı. Ölçülemeyen
    /// yoklama false döner; ayrımı isteyen çağıran <see cref="EncoderState"/> okur.
    /// </summary>
    bool WorksAsEncoder(string codec);

    /// <summary>
    /// Aynı sorunun üç durumlu ve <b>süreç doğurmayan</b> cevabı: çalışıyor, çalışmıyor,
    /// henüz ölçülmedi. <see cref="WorksAsEncoder"/> bilmediğini öğrenmek için ffmpeg
    /// çağırır; bu yol yalnızca zaten bilineni okur, bu yüzden arayüz iş parçacığından
    /// çağrılabilir. <see cref="EncoderProbeState.Unmeasured"/> "bu makinede çalışmıyor"
    /// değil "henüz bakmadık" demektir; ölçüm arka planda yaptırılır.
    /// Varsayılan gerçekleştirme, yoklaması olmayan sahteleri kırmadan iki durumu korur;
    /// gerçekten yoklayan taraf (<c>EncoderCapabilities</c>) bunu ezer.
    /// </summary>
    EncoderProbeState EncoderState(string codec) =>
        WorksAsEncoder(codec) ? EncoderProbeState.Working : EncoderProbeState.NotWorking;
}

public interface IEncoderOptionAvailability
{
    bool SupportsEncoderOption(string codec, string option, string value);
}

/// <summary>
/// HDR10 yoklamasının üç durumlu yüzü. <see cref="IHdr10EncoderAvailability"/> zaman
/// aşımını da <c>null</c> ile, yani "bu kodlayıcıda HDR10 yok" ile aynı kovada döndürüyor;
/// ölçülemedi ile yokluğu ayırmak isteyen çağıran bunu okur.
/// </summary>
public interface IHdr10ProbeAvailability
{
    EncoderProbeState Hdr10State(string codec);
}
