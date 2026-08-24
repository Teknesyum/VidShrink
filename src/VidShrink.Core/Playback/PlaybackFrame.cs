namespace VidShrink.Core.Playback;

/// <summary>
/// Boruda gelen tek birlesik kare. Tampon <see cref="FramePool"/>'dan gelir ve tuketici
/// isini bitirince ayni havuza doner; kare basina ayirma yoktur.
/// </summary>
/// <remarks>
/// T37/O4b: kare basina yeni tampon kopya suresini 0,77 ms'ten 1,47 ms'e cikariyor.
/// Tampon bu yuzden yeniden kullanilir, her karede yenisi ayrilmaz.
/// </remarks>
public sealed class PlaybackFrame
{
    public PlaybackFrame(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        Buffer = buffer;
    }

    /// <summary>BGRA baytlari. Uzunlugu havuzun kare boyudur, dolu kismi <see cref="ByteLength"/>.</summary>
    public byte[] Buffer { get; }

    /// <summary>Birlesik karenin genisligi — iki panelin toplami.</summary>
    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>
    /// Iki panelin genislik ayrimi: sol panel <c>[0, SplitX)</c>, sag panel <c>[SplitX, Width)</c>.
    /// Sunum katmani ayiriciyi bu sayidan hesaplar, kendi bolmez.
    /// </summary>
    public int SplitX { get; private set; }

    /// <summary>Karenin kaynaktaki sunum zamani. Atlama sonrasi atlanan konumdan devam eder.</summary>
    public TimeSpan Presentation { get; private set; }

    /// <summary>Kaynak baslatildigindan beri uretilen kacinci kare. Atlamada sifirlanir.</summary>
    public long Sequence { get; private set; }

    /// <summary>Karenin dolu bayt sayisi.</summary>
    public int ByteLength => Width * Height * 4;

    /// <summary>Tampon degismeden karenin tarifini tazeler. Havuzdan kiralandiktan sonra cagrilir.</summary>
    public void Describe(int width, int height, int splitX, TimeSpan presentation, long sequence)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (splitX <= 0 || splitX >= width) throw new ArgumentOutOfRangeException(nameof(splitX));
        if (checked(width * height * 4) > Buffer.Length)
            throw new ArgumentException("Kare tarifi tampondan buyuk.", nameof(width));

        Width = width;
        Height = height;
        SplitX = splitX;
        Presentation = presentation;
        Sequence = sequence;
    }
}
