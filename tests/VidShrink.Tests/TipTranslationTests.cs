namespace VidShrink.Tests;

/// <summary>
/// T26 K2: ipucu metni değişip sözlük anahtarı eski metinde kalırsa arayüz sessizce
/// İngilizce kalıyordu — ne derleme ne test uyarıyordu. Bu ölçüm o sessizliği kaldırır.
///
/// Metin kaynaklarını <see cref="TipSources"/> okur; T27 aynı okuyucuyu satır genişliği
/// ölçümünde de kullanıyor.
/// </summary>
public sealed class TipTranslationTests
{
    /// <summary>
    /// K2: ekranda gösterilen her ipucunun sözlükte birebir karşılığı olacak. Karşılığı
    /// olmayan çıkarsa bu ölçüm kırılır ve metni değiştiren kişi çeviriyi de günceller.
    /// </summary>
    [Fact]
    public void EveryTipOnScreenHasATurkishEntry()
    {
        var catalogue = TipSources.ReadCatalogue();
        var tips = TipSources.ReadTips();

        Assert.NotEmpty(catalogue);
        Assert.NotEmpty(tips);

        var missing = tips
            .Where(tip => !catalogue.ContainsKey(tip.Text))
            .Select(tip => $"{tip.Source}: {TipSources.FirstLine(tip.Text)}")
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"{missing.Count}/{tips.Count} ipucunun LanguageCatalog içinde karşılığı yok:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, missing));
    }

    /// <summary>
    /// K1: madde biçimi iki dilde birebir aynı olacak. Türkçesi İngilizcesinden az madde
    /// taşırsa bilgi kısaltılmış demektir ve ölçüm kırılır.
    /// </summary>
    [Fact]
    public void EveryTipKeepsItsBulletShapeInBothLanguages()
    {
        var catalogue = TipSources.ReadCatalogue();
        var broken = new List<string>();

        foreach (var tip in TipSources.ReadTips())
        {
            if (!catalogue.TryGetValue(tip.Text, out var turkish)) continue;

            var english = tip.Text.Split('\n');
            var translated = turkish.Split('\n');
            var englishBullets = english.Count(line => TipSources.Bullet.IsMatch(line));
            var translatedBullets = translated.Count(line => TipSources.Bullet.IsMatch(line));

            if (english.Length != translated.Length || englishBullets != translatedBullets)
                broken.Add(
                    $"{tip.Source}: satır {english.Length}/{translated.Length}, "
                    + $"madde {englishBullets}/{translatedBullets} — {TipSources.FirstLine(tip.Text)}");
        }

        Assert.True(
            broken.Count == 0,
            "Madde biçimi iki dilde eşleşmiyor:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, broken));
    }
}
