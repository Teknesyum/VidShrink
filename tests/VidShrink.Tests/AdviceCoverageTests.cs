using VidShrink.App;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// T12 K4.6: <see cref="AdviceCode"/> üyelerinin hiçbiri sessizce yutulmayacak.
///
/// Motor bir gerekçe üretip arayüz onu <c>_ => null</c> dalına düşürdüğünde kimse
/// fark etmiyordu: dört kod aylarca üretildi ve hiç görünmedi. Ölçüm enum'un
/// üyelerini yansımayla geziyor, dolayısıyla enum'a yeni üye eklendiğinde ya metin
/// yazılır ya da <see cref="MainWindow.AdviceCodesWithoutText"/> listesine adıyla
/// girer — üçüncü seçenek yok.
/// </summary>
public sealed class AdviceCoverageTests
{
    private static IEnumerable<AdviceCode> All() => Enum.GetValues<AdviceCode>();

    [Fact]
    public void Every_advice_code_is_either_written_or_declared_unused()
    {
        var missing = All()
            .Where(code => !MainWindow.AdviceCodesWithoutText.Contains(code))
            .Where(code => string.IsNullOrWhiteSpace(MainWindow.AdviceLine(code, language: "tr", fastGpu: false)))
            .Select(code => code.ToString())
            .ToList();

        Assert.True(missing.Count == 0,
            $"Bu gerekçe kodları kullanıcıya hiç görünmüyor: {string.Join(", ", missing)}. " +
            "Ya metin yaz ya da MainWindow.AdviceCodesWithoutText listesine ekle.");
    }

    [Fact]
    public void Declared_unused_codes_really_have_no_text()
    {
        var stray = MainWindow.AdviceCodesWithoutText
            .Where(code => !string.IsNullOrWhiteSpace(MainWindow.AdviceLine(code, language: "tr", fastGpu: false)))
            .Select(code => code.ToString())
            .ToList();

        Assert.True(stray.Count == 0,
            $"Bu kodlar hem metni var hem de üretilmez diye listelenmiş: {string.Join(", ", stray)}.");
    }

    [Fact]
    public void Every_written_code_speaks_both_languages()
    {
        var same = All()
            .Where(code => !MainWindow.AdviceCodesWithoutText.Contains(code))
            .Where(code =>
            {
                var turkish = MainWindow.AdviceLine(code, language: "tr", fastGpu: false);
                var english = MainWindow.AdviceLine(code, language: "en", fastGpu: false);
                return string.IsNullOrWhiteSpace(english) || turkish == english;
            })
            .Select(code => code.ToString())
            .ToList();

        Assert.True(same.Count == 0,
            $"Bu kodların iki dilde ayrı karşılığı yok: {string.Join(", ", same)}.");
    }

    /// <summary>
    /// Denetimin adıyla saydığı dört kod. Genel ölçüm zaten kapsıyor; bu ölçüm
    /// hangi dördünün eksik olduğunu bilerek duruyor ki geri düşerse ad görünsün.
    /// </summary>
    [Theory]
    [InlineData(AdviceCode.TargetBelowCodecFloor)]
    [InlineData(AdviceCode.FrameRateCutForFloor)]
    [InlineData(AdviceCode.MotionCutIsCheap)]
    [InlineData(AdviceCode.MotionCutIsExpensive)]
    public void Codes_the_engine_produces_reach_the_user(AdviceCode code)
    {
        Assert.False(string.IsNullOrWhiteSpace(MainWindow.AdviceLine(code, language: "tr", fastGpu: false)));
        Assert.False(string.IsNullOrWhiteSpace(MainWindow.AdviceLine(code, language: "en", fastGpu: false)));
    }
}
