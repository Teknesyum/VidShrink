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
    }

    /// <summary>
    /// Düzey dizisi başlığın adını görmüyor: denetim "### İzlenen klasör" başlığını
    /// "### Bambaska Bir Baslik" yapan mutasyonun sağ kaldığını ölçtü. Metin de pimli.
    /// </summary>
    [Theory]
    [InlineData("README.md", "Command Line", "Watch Folder")]
    [InlineData("README.tr.md", "Komut satırı", "İzlenen klasör")]
    public void KomutSatiriBasliklariAdiylaPimli(string ad, string bolum, string altBolum)
    {
        var basliklar = Basliklar(ad);

        Assert.Contains((2, bolum), basliklar);
        Assert.Contains((3, altBolum), basliklar);
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
