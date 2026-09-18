using System.IO;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kanıt kapanışının kendi ölçüsü. Kapanış 38 çağrı yerinin hepsinde sessizce çalışıyor;
/// yanlış çalıştığında hiçbir ölçü kırmızıya dönmez, yalnız artık birikir ya da kardeşin
/// kanıtı kaybolur. Bu yüzden gövdenin kendisi pimleniyor.
/// </summary>
public class KanitKapanisiTests
{
    private static string YeniKlasor(string ad)
    {
        var yol = Path.Combine(TestPaths.OutputRoot, "kanit-kapanisi-olcu", ad);
        if (Directory.Exists(yol)) Directory.Delete(yol, true);
        Directory.CreateDirectory(yol);
        return yol;
    }

    private static void Dosya(string klasor, string ad) => File.WriteAllText(Path.Combine(klasor, ad), ad);

    [Fact]
    public void JokerEslesenleriSilerEslesmeyeniBirakir()
    {
        var klasor = YeniKlasor("joker");
        Dosya(klasor, "donu-1-trimmed.mkv");
        Dosya(klasor, "donu-2-trimmed.mkv");
        Dosya(klasor, "baska.mkv");
        Directory.CreateDirectory(Path.Combine(klasor, "donu-klasor-trimmed"));

        KanitKapanisi.Onceki(klasor, "donu*-trimmed*");

        var kalan = Directory.GetFileSystemEntries(klasor).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "baska.mkv" }, kalan);

        KanitKapanisi.Kapat(klasor, "baska.mkv");
        Assert.False(Directory.Exists(klasor));
    }

    [Fact]
    public void JokersizAdYalnizKendiniSiler()
    {
        var klasor = YeniKlasor("duz-ad");
        Dosya(klasor, "olcu.txt");
        Dosya(klasor, "olcu.txt.eski");

        KanitKapanisi.Onceki(klasor, "olcu.txt");

        Assert.Equal(new[] { "olcu.txt.eski" }, Directory.GetFiles(klasor).Select(Path.GetFileName).ToArray());

        KanitKapanisi.Kapat(klasor, "olcu.txt.eski");
        Assert.False(Directory.Exists(klasor));
    }

    [Fact]
    public void EslesmeyenJokerKlasoruSupurmez()
    {
        var klasor = YeniKlasor("kor-joker");
        Dosya(klasor, "kardesin-kanidi.txt");

        KanitKapanisi.Onceki(klasor, "hicbiri*");
        KanitKapanisi.Kapat(klasor, "hicbiri*");

        Assert.True(File.Exists(Path.Combine(klasor, "kardesin-kanidi.txt")));

        KanitKapanisi.Kapat(klasor, "kardesin-kanidi.txt");
        Assert.False(Directory.Exists(klasor));
    }

    [Fact]
    public void BosUstKlasorlerGiderCalismaKalir()
    {
        var kok = Path.Combine(TipSources.Root, ".calisma");
        var derin = Path.Combine(kok, "kanit-kapanisi-olcu-derin", "a", "b");
        if (Directory.Exists(Path.Combine(kok, "kanit-kapanisi-olcu-derin")))
            Directory.Delete(Path.Combine(kok, "kanit-kapanisi-olcu-derin"), true);
        Directory.CreateDirectory(derin);
        Dosya(derin, "tek.txt");

        KanitKapanisi.Kapat(derin, "tek.txt");

        Assert.False(Directory.Exists(Path.Combine(kok, "kanit-kapanisi-olcu-derin")));
        Assert.True(Directory.Exists(kok));
    }
}
