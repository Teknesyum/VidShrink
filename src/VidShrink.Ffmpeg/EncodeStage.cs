using System.Globalization;
using System.Text.RegularExpressions;

namespace VidShrink.Ffmpeg;

/// <summary>
/// Motorun aşama satırı: <c>pass 2/2 (attempt 3)</c> ya da tek geçişte <c>encoding (attempt 1)</c>.
/// Biçimi yazan ve okuyan aynı yer; arayüz geçişi ve denemeyi buradan çözer.
/// </summary>
public sealed record EncodeStage(int Pass, int PassCount, int Attempt)
{
    private static readonly Regex MultiPass = new(@"^pass (\d+)/(\d+) \(attempt (\d+)\)$", RegexOptions.CultureInvariant);
    private static readonly Regex SinglePass = new(@"^encoding \(attempt (\d+)\)$", RegexOptions.CultureInvariant);

    public override string ToString() => PassCount > 1
        ? string.Create(CultureInfo.InvariantCulture, $"pass {Pass}/{PassCount} (attempt {Attempt})")
        : string.Create(CultureInfo.InvariantCulture, $"encoding (attempt {Attempt})");

    /// <summary>Motorun kendi biçimi değilse ya da geçiş sayıyı aşıyorsa <c>null</c>.</summary>
    public static EncodeStage? Parse(string stage)
    {
        if (MultiPass.Match(stage) is { Success: true } m)
        {
            var parsed = new EncodeStage(Number(m.Groups[1]), Number(m.Groups[2]), Number(m.Groups[3]));
            return parsed.Pass >= 1 && parsed.Pass <= parsed.PassCount ? parsed : null;
        }
        return SinglePass.Match(stage) is { Success: true } s ? new EncodeStage(1, 1, Number(s.Groups[1])) : null;
    }

    /// <summary>Bütün geçişlere yayılan kesirden bu geçişin içindeki yer (iki geçişte ilki 0–0,5).</summary>
    public double Within(double fraction) => Math.Clamp(fraction * PassCount - (Pass - 1), 0, 1);

    private static int Number(Group group) => int.Parse(group.Value, CultureInfo.InvariantCulture);
}
