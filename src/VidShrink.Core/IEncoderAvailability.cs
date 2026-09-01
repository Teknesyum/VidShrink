namespace VidShrink.Core;

public interface IEncoderAvailability
{
    bool HasEncoder(string name);
    bool WorksAsEncoder(string codec);
}

public interface IEncoderOptionAvailability
{
    bool SupportsEncoderOption(string codec, string option, string value);
}
