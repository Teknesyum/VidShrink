using System.Text.RegularExpressions;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// Kod borcu 9: "bu iki yol aynı dosya mı" sorusu dört ayrı gövdede yazılıydı ve
/// davranışları ayrışmıştı — <c>CurrentMedia.SamePath</c> bozuk yolda <c>false</c>
/// dönüyor, öbür üçü <see cref="ArgumentException"/> atıyordu. Ölçü hem tek gövdenin
/// davranışını hem de ikinci bir gövdenin doğmadığını okur.
/// </summary>
public sealed class YolEsitligiTests
{
    /// <summary>Aynı dosyanın iki yazımı: göreli yol mutlak yola eşit.</summary>
    [Fact]
    public void GoreliYolMutlakYolaEsit()
    {
        var kok = Path.GetTempPath();
        var mutlak = Path.Combine(kok, "vidshrink-yol-esitligi.mp4");
        var dolambacli = Path.Combine(kok, "alt", "..", "vidshrink-yol-esitligi.mp4");

        Assert.True(PathEquality.Same(mutlak, dolambacli));
        Assert.False(PathEquality.Same(mutlak, Path.Combine(kok, "baska.mp4")));
    }

    /// <summary>
    /// Bozuk yol eşitsizliktir, çakma değil. Üç çağrı yerinin üçü de "aynıysa atla"
    /// kolunda duruyor; oradan atılan bir istisna kullanıcının işini durduruyordu.
    /// </summary>
    [Fact]
    public void BozukYolCakmadanEsitsizDonuyor()
    {
        var bozuk = "C:\\kayit\\dosya\0adi.mp4";

        Assert.False(PathEquality.Same(bozuk, bozuk));
        Assert.False(PathEquality.Same(bozuk, Path.GetTempPath()));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, @"C:\a.mp4")]
    [InlineData(@"C:\a.mp4", null)]
    [InlineData("", @"C:\a.mp4")]
    public void BosYolEslesmiyor(string? sol, string? sag) => Assert.False(PathEquality.Same(sol, sag));

    /// <summary>
    /// Harf duyarlılığı işletim sisteminden geliyor, sabitten değil: karşılaştırma
    /// <see cref="WatchFolder.PathComparison"/> dikişini paylaşıyor. İki kol da burada
    /// koşuyor, çünkü karşılaştırma dışarıdan verilebiliyor.
    /// </summary>
    [Fact]
    public void HarfDuyarliligiIsletimSistemindenGeliyor()
    {
        var kucuk = Path.Combine(Path.GetTempPath(), "kayit.mp4");
        var buyuk = Path.Combine(Path.GetTempPath(), "KAYIT.MP4");

        Assert.True(PathEquality.Same(kucuk, buyuk, StringComparison.OrdinalIgnoreCase));
        Assert.False(PathEquality.Same(kucuk, buyuk, StringComparison.Ordinal));

        Assert.Equal(
            PathEquality.Same(kucuk, buyuk, WatchFolder.PathComparison),
            PathEquality.Same(kucuk, buyuk));
    }

    /// <summary>
    /// İkinci gövde doğmasın: kaynakta <c>GetFullPath</c>'i doğrudan <c>string.Equals</c>
    /// ile karşılaştıran başka bir yer kalmadı. Tarayıcının kör olmadığı, aynı desenin
    /// elle kurulmuş bir örneği üzerinde ölçülüyor.
    /// </summary>
    [Fact]
    public void IkinciGovdeYok()
    {
        var desen = new Regex(@"string\.Equals\(\s*Path\.GetFullPath", RegexOptions.CultureInvariant);

        var ornek = "return string.Equals(\r\n    Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);";
        Assert.True(desen.IsMatch(ornek), "Tarayıcı kör: kurulmuş örneği bile görmüyor.");

        var kacaklar = Directory
            .EnumerateFiles(Path.Combine(TipSources.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(yol => !yol.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                       && !yol.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                       && !yol.EndsWith("PathEquality.cs", StringComparison.Ordinal))
            .Where(yol => desen.IsMatch(File.ReadAllText(yol)))
            .Select(yol => Path.GetFileName(yol))
            .ToList();

        Assert.Empty(kacaklar);
    }
}
