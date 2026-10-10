using System.Globalization;
using System.Text;

namespace VidShrink.Core;

public enum SubtitleCharsetKind
{
    Utf8,
    Converted,
    Unknown
}

/// <summary>
/// Yakilacak altyazi dosyasinin okunmus hali. <see cref="SubtitleCharsetKind.Utf8"/> dosyaya
/// dokunulmayacagini, <see cref="SubtitleCharsetKind.Converted"/> <see cref="Text"/>'in UTF-8
/// yazilip onun yakilacagini, <see cref="SubtitleCharsetKind.Unknown"/> kodlamanin
/// cikarilamadigini soyler. <see cref="Name"/> kullaniciya soylenen kodlama adidir.
/// </summary>
public sealed record SubtitleCharsetRead(SubtitleCharsetKind Kind, string? Name, string? Text);

/// <summary>
/// ffmpeg'in <c>subtitles</c> suzgeci dosyayi UTF-8 sayar: Windows-1254 ve UTF-16 SRT'de
/// "Invalid UTF-8 in decoded subtitles text" deyip suzgec grafigini kuramadan cikiyor
/// (ffmpeg 9.0, cikis 183). <c>charenc</c> tek baytli kod sayfasinda dogru yakiyor ama
/// UTF-16'da yaziyi bozuyor; o yuzden dosya burada cozulur ve UTF-8 kopyasi yakilir.
/// Olcum: <c>docs/olcumler/altyazi-yakma-kodlama-yol.md</c>.
/// </summary>
public static class SubtitleCharset
{
    private const int Utf8CodePage = 65001;
    private const int SniffBytes = 4096;

    public static int SystemAnsiCodePage => CultureInfo.CurrentCulture.TextInfo.ANSICodePage;

    public static SubtitleCharsetRead Read(byte[] bytes, int ansiCodePage)
    {
        if (StartsWith(bytes, 0xEF, 0xBB, 0xBF)) return new(SubtitleCharsetKind.Utf8, "UTF-8", null);
        if (StartsWith(bytes, 0xFF, 0xFE, 0x00, 0x00)) return Decoded(new UTF32Encoding(false, false), bytes, 4, "UTF-32LE");
        if (StartsWith(bytes, 0x00, 0x00, 0xFE, 0xFF)) return Decoded(new UTF32Encoding(true, false), bytes, 4, "UTF-32BE");
        if (StartsWith(bytes, 0xFF, 0xFE)) return Decoded(Encoding.Unicode, bytes, 2, "UTF-16LE");
        if (StartsWith(bytes, 0xFE, 0xFF)) return Decoded(Encoding.BigEndianUnicode, bytes, 2, "UTF-16BE");

        if (Array.IndexOf(bytes, (byte)0) >= 0)
            return Bomless16(bytes) switch
            {
                true => Decoded(Encoding.Unicode, bytes, 0, "UTF-16LE"),
                false => Decoded(Encoding.BigEndianUnicode, bytes, 0, "UTF-16BE"),
                null => new(SubtitleCharsetKind.Unknown, null, null)
            };

        if (IsUtf8(bytes)) return new(SubtitleCharsetKind.Utf8, "UTF-8", null);
        if (ansiCodePage <= 0 || ansiCodePage == Utf8CodePage) return new(SubtitleCharsetKind.Unknown, null, null);
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var ansi = Encoding.GetEncoding(ansiCodePage);
            if (!ansi.IsSingleByte) return new(SubtitleCharsetKind.Unknown, null, null);
            return Decoded(ansi, bytes, 0, "Windows-" + ansiCodePage.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return new(SubtitleCharsetKind.Unknown, null, null);
        }
    }

    private static SubtitleCharsetRead Decoded(Encoding encoding, byte[] bytes, int skip, string name)
        => new(SubtitleCharsetKind.Converted, name, encoding.GetString(bytes, skip, bytes.Length - skip));

    private static bool StartsWith(byte[] bytes, params byte[] mark)
        => bytes.Length >= mark.Length && bytes.AsSpan(0, mark.Length).SequenceEqual(mark);

    private static bool IsUtf8(byte[] bytes)
    {
        try
        {
            new UTF8Encoding(false, true).GetCharCount(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool? Bomless16(byte[] bytes)
    {
        int even = 0, odd = 0;
        var end = Math.Min(bytes.Length, SniffBytes) & ~1;
        for (var i = 0; i < end; i += 2)
        {
            if (bytes[i] == 0) even++;
            if (bytes[i + 1] == 0) odd++;
        }
        var pairs = end / 2;
        if (odd * 4 >= pairs && even * 10 <= odd) return true;
        if (even * 4 >= pairs && odd * 10 <= even) return false;
        return null;
    }
}
