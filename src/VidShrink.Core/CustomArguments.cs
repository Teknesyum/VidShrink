using System.Globalization;
using System.Text;

namespace VidShrink.Core;

public enum CustomArgumentProblem { None, UnclosedQuote, Forbidden, StrayValue }

public sealed record CustomArgumentResult(IReadOnlyList<string> Arguments, CustomArgumentProblem Problem, string? Offender)
{
    public bool Ok => Problem == CustomArgumentProblem.None;
}

/// <summary>
/// Dönüştür'ün "ek argüman" kutusu ve <c>donustur --ek-arguman</c>: kullanıcının yazdığı metni
/// ffmpeg argümanlarına böler. Boşluk ayırır; çift ya da tek tırnak boşluğu korur, çift tırnak içinde
/// <c>\"</c> tırnağı kaçırır. Girdiyi, üzerine yazma kararını ya da çıktıyı ezen seçenekler
/// (<see cref="ForbiddenOptions"/>) ve ikinci bir çıktı yolu olacak başıboş değerler reddedilir.
/// </summary>
public static class CustomArguments
{
    public static IReadOnlyList<string> ForbiddenOptions { get; } = new[] { "-i", "-y", "-n", "-f", "-progress", "-", "--" };

    public static CustomArgumentResult Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new CustomArgumentResult(Array.Empty<string>(), CustomArgumentProblem.None, null);
        var tokens = Split(text);
        if (tokens is null) return new CustomArgumentResult(Array.Empty<string>(), CustomArgumentProblem.UnclosedQuote, null);
        return Check(tokens);
    }

    public static CustomArgumentResult Check(IReadOnlyList<string> tokens)
    {
        var previousIsOption = false;
        foreach (var token in tokens)
        {
            if (IsOption(token))
            {
                if (IsForbidden(token)) return new CustomArgumentResult(Array.Empty<string>(), CustomArgumentProblem.Forbidden, token);
                previousIsOption = true;
                continue;
            }
            if (!previousIsOption) return new CustomArgumentResult(Array.Empty<string>(), CustomArgumentProblem.StrayValue, token);
            previousIsOption = false;
        }
        return new CustomArgumentResult(tokens.ToList(), CustomArgumentProblem.None, null);
    }

    public static List<string>? Split(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inToken = false;
        char? quote = null;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quote is { } open)
            {
                if (c == open) quote = null;
                else if (open == '"' && c == '\\' && i + 1 < text.Length && text[i + 1] == '"') current.Append(text[++i]);
                else current.Append(c);
                continue;
            }
            if (char.IsWhiteSpace(c))
            {
                if (inToken) tokens.Add(current.ToString());
                current.Clear();
                inToken = false;
                continue;
            }
            inToken = true;
            if (c is '"' or '\'') quote = c;
            else current.Append(c);
        }
        if (quote is not null) return null;
        if (inToken) tokens.Add(current.ToString());
        return tokens;
    }

    private static bool IsOption(string token)
        => token.StartsWith('-') && !double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static bool IsForbidden(string token)
    {
        if (token is "-" or "--") return true;
        var name = "-" + token.TrimStart('-');
        return ForbiddenOptions.Contains(name, StringComparer.Ordinal);
    }
}
