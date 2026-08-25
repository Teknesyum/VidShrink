using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using VidShrink.App;

namespace VidShrink.Tests;

/// <summary>
/// T36 İş 1: ayarlar sekmesi. İki şey ölçülüyor — güncelleme ayarının taşındığı (About'ta
/// iz kalmadığı) ve paylaşım hedefi listesinin XAML'e sabit yazılmadığı.
///
/// Sekmenin yerleşimi kaynak metinden okunuyor, çünkü test projesi pencereyi açamıyor:
/// <c>Avalonia.Headless</c> paketi bağlı değil ve <c>MainWindow</c> gerçek bir pencere
/// platformu istiyor. Görünürlük kuralının kendisi bu yüzden <see cref="ShareTargetTable"/>
/// üstünden, veri katmanında ölçülüyor.
/// </summary>
public sealed class SettingsTabTests
{
    private static string WindowXaml() => File.ReadAllText(TipSources.WindowXamlPath);

    private static string WindowCode() => File.ReadAllText(TipSources.WindowCodePath);

    /// <summary>Adı verilen sekmenin gövdesini döndürür.</summary>
    private static string Tab(string header)
    {
        var xaml = WindowXaml();
        var start = xaml.IndexOf($"<TabItem Header=\"{header}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{header} sekmesi MainWindow.axaml içinde yok.");

        var next = xaml.IndexOf("<TabItem Header=\"", start + 1, StringComparison.Ordinal);
        return next < 0 ? xaml[start..] : xaml[start..next];
    }

    [Fact]
    public void TheSettingsTabExists()
    {
        Assert.Contains("Header=\"Settings\"", WindowXaml(), StringComparison.Ordinal);
        Assert.Equal("Ayarlar", LanguageCatalog.EnglishToTurkish[LanguageCatalog.Title("Settings", false)]);
    }

    /// <summary>
    /// K: güncelleme ayarı About'ta **yok**, Settings'te **var**. Kopyalanmadığını görmek
    /// için iki sekme de ayrı ayrı aranıyor.
    /// </summary>
    [Theory]
    [InlineData("ChkAutoUpdate")]
    [InlineData("AutoUpdateRow")]
    [InlineData("TxtAutoUpdateEffect")]
    public void TheUpdateSettingMovedOutOfAboutIntoSettings(string name)
    {
        Assert.Contains($"x:Name=\"{name}\"", Tab("Settings"), StringComparison.Ordinal);
        Assert.DoesNotContain(name, Tab("About"), StringComparison.Ordinal);
    }

    /// <summary>
    /// K: hedef listesi XAML'e sabit yazılmayacak. Açılır kutunun içinde tek bir
    /// <c>ComboBoxItem</c> bulunursa liste dosyadan gelmiyor demektir.
    /// </summary>
    [Fact]
    public void TheTargetListIsNotWrittenIntoTheMarkup()
    {
        var settings = Tab("Settings");

        Assert.Contains("x:Name=\"CmbShareTarget\"", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("ComboBoxItem", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("storage.to", settings.Replace(
            "• storage.to carries up to 25 GiB and lets VidShrink delete the file again, so a link can be closed early.",
            "",
            StringComparison.Ordinal),
            StringComparison.Ordinal);
        Assert.Contains("CmbShareTarget.ItemsSource", WindowCode(), StringComparison.Ordinal);
    }

    /// <summary>Depo kökündeki gerçek <c>paylasim-hedefleri.json</c>.</summary>
    private static string RealFilePath => Path.Combine(TipSources.Root, ShareTargetTable.FileName);

    /// <summary>
    /// K: hedef listesi gerçekten dosyadan geliyor. T35 dosyayı yazdı; ölçüm şema
    /// varsayılanlarını değil o dosyayı okuyor ve <see cref="ShareTargetTable.Fallback"/>
    /// ile aynı nesne olmadığını görüyor.
    /// </summary>
    [Fact]
    public void TheRealFileIsTheOneThatFillsTheList()
    {
        Assert.True(File.Exists(RealFilePath), $"{RealFilePath} yok.");
        Assert.Equal(RealFilePath, ShareTargetTable.Locate(TipSources.Root));

        var table = ShareTargetTable.Parse(File.ReadAllText(RealFilePath));

        Assert.NotSame(ShareTargetTable.Fallback, table);
        Assert.Equal("storage.to", table.Default.Id);
        Assert.Equal(new[] { "storage.to", "uguu.se" }, table.Targets.Select(target => target.Id));

        var storage = table.Targets[0];
        Assert.Equal(26_843_545_600L, storage.MaxBytes);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, storage.RetentionDays);
        Assert.Equal(3, storage.DefaultRetentionDays);
        Assert.True(storage.CanDelete);

        var uguu = table.Targets[1];
        Assert.Equal(134_217_728L, uguu.MaxBytes);
        Assert.Empty(uguu.RetentionDays);
        Assert.Equal(3, uguu.FixedRetentionHours);
        Assert.False(uguu.CanDelete);

        var loaded = ShareTargetTable.Load();
        Assert.NotSame(ShareTargetTable.Fallback, loaded);
        Assert.Equal(table.Targets.Select(target => target.Id), loaded.Targets.Select(target => target.Id));
    }

    /// <summary>
    /// K: JSON'a üçüncü bir hedef eklenince arayüzde görünüyor. Ölçüm uydurma bir şemayla
    /// değil, depodaki gerçek dosyanın kendisiyle koşuyor: dosya okunuyor, üçüncü hedef
    /// ekleniyor, sonuç geçici bir dizine yazılıp <see cref="ShareTargetTable.Load"/>'un
    /// kullandığı yol — <c>Locate</c> ve <c>Parse</c> — üstünden geri okunuyor.
    /// </summary>
    [Fact]
    public void AThirdTargetAddedToTheFileShowsUpWithoutACodeChange()
    {
        var document = JsonNode.Parse(File.ReadAllText(RealFilePath))!.AsObject();
        document["targets"]!.AsArray().Add(JsonNode.Parse("""
        { "id": "yeni.example", "displayName": "yeni.example", "maxBytes": 1073741824,
          "retentionDays": [1,2], "defaultRetentionDays": 2,
          "canDelete": true, "playsInBrowser": false,
          "endpoints": { "upload": "https://yeni.example/upload" } }
        """));

        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, ShareTargetTable.FileName), document.ToJsonString());

            var found = ShareTargetTable.Locate(directory);
            Assert.NotNull(found);

            var table = ShareTargetTable.Parse(File.ReadAllText(found!));

            Assert.Equal(3, table.Targets.Count);
            Assert.Equal(new[] { "storage.to", "uguu.se", "yeni.example" }, table.Targets.Select(target => target.Id));

            var third = table.Targets[2];
            Assert.Equal("yeni.example", third.DisplayName);
            Assert.Equal(1_073_741_824, third.MaxBytes);
            Assert.Equal(new[] { 1, 2 }, third.RetentionDays);
            Assert.Equal(2, third.DefaultRetentionDays);
            Assert.True(third.CanDelete);
            Assert.False(third.PlaysInBrowser);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// K: <c>canDelete: false</c> olan hedefte silme düğmesi görünmüyor. Görünürlük XAML'de
    /// sabitlenmiyor, koddan bu bayrağa göre veriliyor.
    /// </summary>
    [Fact]
    public void ATargetWithoutADeleteTokenHidesTheDeleteButton()
    {
        var uguu = ShareTargetTable.Fallback.Targets.Single(target => target.Id == "uguu.se");
        var storage = ShareTargetTable.Fallback.Targets.Single(target => target.Id == "storage.to");

        Assert.False(uguu.CanDelete);
        Assert.True(storage.CanDelete);

        var button = Regex.Match(Tab("Settings"), """<Button x:Name="BtnShareDelete".*?/>""", RegexOptions.Singleline);
        Assert.True(button.Success, "BtnShareDelete ayarlar sekmesinde yok.");
        Assert.DoesNotContain("IsVisible", button.Value, StringComparison.Ordinal);
        Assert.Contains("BtnShareDelete.IsVisible = target.CanDelete;", WindowCode(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Silme jetonu olmayan hedefte sebebin yazıldığını ölçer. Metin gizlenmiyor: kullanıcı
    /// farkı seçim anında görmek zorunda.
    /// </summary>
    [Fact]
    public void TheMissingDeleteTokenIsExplainedInBothLanguages()
    {
        var code = WindowCode();

        Assert.Contains("x:Name=\"TxtShareDeleteNote\"", Tab("Settings"), StringComparison.Ordinal);
        Assert.Contains("gönderene silme jetonu vermiyor", code, StringComparison.Ordinal);
        Assert.Contains("hands out no delete token", code, StringComparison.Ordinal);
        Assert.Contains("saatlik kendiliğinden silme geçer", code, StringComparison.Ordinal);
        Assert.Contains("stands in for that", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// Dosya yokken ya da bozukken arayüz açılmaya devam eder ve şemadaki varsayılanları
    /// gösterir. Dosyayı T35 yazıyor; T36 onu oluşturmuyor.
    /// </summary>
    [Fact]
    public void AMissingOrBrokenFileFallsBackToTheSchemaDefaults()
    {
        Assert.Null(ShareTargetTable.Locate(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        Assert.Equal(2, ShareTargetTable.Fallback.Targets.Count);
        Assert.Equal("storage.to", ShareTargetTable.Fallback.Default.Id);
        Assert.Same(ShareTargetTable.Fallback, ShareTargetTable.Parse("""{"version":1,"targets":[]}"""));
    }

    /// <summary>
    /// Tavanlar ölçülmüş sayılar: uguu.se ana sayfası 128 MiB diyor, storage.to'nunki tam
    /// 25 GiB. İkisi de ikilik kat olduğu için ikilik adla yazılır, ondalığa yuvarlanmaz.
    /// </summary>
    [Theory]
    [InlineData(134_217_728L, "128 MiB")]
    [InlineData(26_843_545_600L, "25 GiB")]
    [InlineData(1_073_741_824L, "1 GiB")]
    public void TheCeilingIsWrittenInTheUnitTheByteCountActuallyIs(long bytes, string expected)
        => Assert.Equal(expected, MainWindow.DescribeBytes(bytes));
}

/// <summary>
/// Marka yazımı. Büyük harf geçidi her sözcüğü büyütüyor ve "Buy Me A Coffee" yazıyordu;
/// markanın kendi yazımı "Buy Me a Coffee". Yazım <see cref="LanguageCatalog.Brands"/>'te
/// sabit, çeviri girdisi değil: Türkçede de aynı kalır.
/// </summary>
public sealed class BrandSpellingTests
{
    private const string Sponsor = "Buy Me a Coffee";

    [Theory]
    [InlineData("Buy me a coffee")]
    [InlineData("Buy Me a Coffee")]
    [InlineData("BUY ME A COFFEE")]
    public void TheSponsorBrandKeepsItsOwnSpelling(string written)
    {
        Assert.Equal(Sponsor, LanguageCatalog.Title(written, false));
        Assert.Equal(Sponsor, LanguageCatalog.Title(written, true));
        Assert.Equal(Sponsor, LanguageCatalog.Localize(written, true));
    }

    /// <summary>Görünen metin ile erişilebilir ad aynı dizge olacak.</summary>
    [Fact]
    public void TheButtonAndItsAccessibleNameCarryTheSameString()
    {
        var xaml = File.ReadAllText(TipSources.WindowXamlPath);

        Assert.Contains($"""auto:AutomationProperties.Name="{Sponsor}" """.TrimEnd(), xaml, StringComparison.Ordinal);
        Assert.Contains($"""<TextBlock x:Name="TxtSponsor" Text="{Sponsor}" """.TrimEnd(), xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Buy me a coffee", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Buy Me A Coffee", xaml, StringComparison.Ordinal);
    }

    /// <summary>Marka çevrilmez: sözlükte girdisi olmayacak.</summary>
    [Fact]
    public void TheBrandIsNotATranslationEntry()
        => Assert.False(LanguageCatalog.EnglishToTurkish.ContainsKey(Sponsor));
}
