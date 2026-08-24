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

    /// <summary>
    /// K: JSON'a üçüncü bir hedef eklenince arayüzde görünüyor. Arayüz listeyi
    /// <see cref="ShareTargetTable"/>'dan alıyor, bu yüzden ölçüm oradan yapılıyor:
    /// üçüncü hedef okunuyorsa açılır kutuda da belirir.
    /// </summary>
    [Fact]
    public void AThirdTargetAddedToTheFileShowsUpWithoutACodeChange()
    {
        const string json = """
        {
          "version": 1,
          "default": "storage.to",
          "targets": [
            { "id": "storage.to", "displayName": "storage.to", "maxBytes": 26843545600,
              "retentionDays": [1,2,3,4,5,6,7], "defaultRetentionDays": 3,
              "canDelete": true, "playsInBrowser": true,
              "endpoints": { "init": "https://storage.to/api/upload/init" } },
            { "id": "uguu.se", "displayName": "uguu.se", "maxBytes": 134217728,
              "retentionDays": [], "fixedRetentionHours": 3,
              "canDelete": false, "playsInBrowser": true,
              "endpoints": { "upload": "https://uguu.se/upload?output=text" } },
            { "id": "yeni.example", "displayName": "yeni.example", "maxBytes": 1073741824,
              "retentionDays": [1,2], "defaultRetentionDays": 2,
              "canDelete": true, "playsInBrowser": false,
              "endpoints": { "upload": "https://yeni.example/upload" } }
          ]
        }
        """;

        var table = ShareTargetTable.Parse(json);

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
