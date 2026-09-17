using System.Text.RegularExpressions;
using VidShrink.Tests;

public sealed class BelgeBasliklariTests
{
    [Fact]
    public void IkiReadmeAyniBaslikDuzeniniTasiyor()
    {
        var ingilizce = Basliklar("README.md");
        var turkce = Basliklar("README.tr.md");

        Assert.Equal(Duzen(ingilizce), Duzen(turkce));
    }

    [Fact]
    public void ReadmeBaslikTaramasiKodBloklarinaKanmiyor()
    {
        var ingilizce = Basliklar("README.md");

        Assert.DoesNotContain("Windows", ingilizce.Select(b => b.Metin));
        Assert.DoesNotContain("macOS / Linux", ingilizce.Select(b => b.Metin));
        Assert.Contains("Command Line", ingilizce.Select(b => b.Metin));
        Assert.Contains("Watch Folder", ingilizce.Select(b => b.Metin));
    }

    private static string Duzen(IReadOnlyList<(int Duzey, string Metin)> basliklar) =>
        string.Join("\n", basliklar.Select((b, i) => $"{i}: {new string('#', b.Duzey)}"));

    private static IReadOnlyList<(int Duzey, string Metin)> Basliklar(string ad)
    {
        var basliklar = new List<(int, string)>();
        var cit = false;
        foreach (var satir in File.ReadAllLines(Path.Combine(TipSources.Root, ad)))
        {
            if (satir.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                cit = !cit;
                continue;
            }

            if (cit) continue;
            var eslesme = Regex.Match(satir, "^(#{1,6})[ \t]+(.+)$");
            if (eslesme.Success) basliklar.Add((eslesme.Groups[1].Value.Length, eslesme.Groups[2].Value.Trim()));
        }

        return basliklar;
    }
}
