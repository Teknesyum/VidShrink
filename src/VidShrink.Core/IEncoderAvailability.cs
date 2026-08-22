namespace VidShrink.Core;

public interface IEncoderAvailability
{
    bool HasEncoder(string name);
    bool WorksAsEncoder(string codec);
}
