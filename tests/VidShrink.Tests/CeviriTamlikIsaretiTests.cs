using VidShrink.App;
using VidShrink.App.Localization;

namespace VidShrink.Tests;

/// <summary>
/// AYR-25: insan gözünden geçmiş diller bir listede durur, öteki diller dil seçicide kendi
/// dillerinde "makine çevirisi" işaretiyle görünür. İşaret anahtarı her dilin kendi
/// kataloğunda olmalı; İngilizceye düşen işaret o dili okuyana yabancı kalır.
/// </summary>
public sealed class CeviriTamlikIsaretiTests : IDisposable
{
    private const string NameKey = "main.language.name";
    private const string MarkerKey = "main.language.unreviewed";

    public CeviriTamlikIsaretiTests() => Strings.Reset();

    public void Dispose() => Strings.Reset();

    [Fact]
    public void GozdenGecenDillerKurulumdaVarVeIsaretsizGorunur()
    {
        Assert.NotEmpty(Strings.ReviewedLanguages);
        foreach (var language in Strings.ReviewedLanguages)
        {
            Assert.Contains(language, Strings.Languages, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(Strings.PeekIn(language, NameKey), Strings.LanguageLabel(language));
        }
    }

    [Fact]
    public void GozdenGecmemisHerDilKendiDilindeIsaretlenir()
    {
        var unreviewed = Strings.Languages.Where(l => !Strings.IsReviewedLanguage(l)).ToList();
        Assert.True(unreviewed.Count >= 40, "İşaretlenecek dil sayısı: " + unreviewed.Count);

        foreach (var language in unreviewed)
        {
            var name = Strings.PeekIn(language, NameKey);
            var label = Strings.LanguageLabel(language);
            Assert.Contains(MarkerKey, Strings.KeysOf(language));
            Assert.Contains(name, label, StringComparison.Ordinal);
            Assert.NotEqual(name, label);
            Assert.Equal(string.Format(Strings.CultureOf(language), Strings.PeekIn(language, MarkerKey), name), label);
        }
    }

    [Fact]
    public void AyarlardakiDilSecicisiIsaretliAdlariGosterir()
    {
        var offered = AppHost.Run(() =>
        {
            var window = new MainWindow();
            return window.CmbLanguage.ItemsSource!.Cast<object?>().Select(item => item?.ToString() ?? string.Empty).ToList();
        });

        Assert.Equal(Strings.Languages.Select(Strings.LanguageLabel).ToList(), offered);
        Assert.Contains(offered, label => label != Strings.PeekIn("de", NameKey) && label.Contains(Strings.PeekIn("de", NameKey), StringComparison.Ordinal));
    }
}
