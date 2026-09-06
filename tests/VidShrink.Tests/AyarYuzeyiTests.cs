using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using VidShrink.App;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// T177 kabul ölçütleri: Küçült sekmesinin ayar yüzeyi.
///
/// <para>Sayımlar gözle değil kaynaktan yapılıyor — biçimleme <see cref="XDocument"/>
/// ile okunup Küçült sekmesinin alt ağacı geziliyor. Satır aralığı yazmak yerine sekme
/// ağaçtan bulunuyor: <c>PageShrink</c> kaydırıcısını içeren <c>TabItem</c>.</para>
///
/// <para>Her sıfır iddiasının yanında bir <b>olumlu denetim</b> var: aynı tarayıcı
/// belgenin tamamında sıfırdan büyük sayı bulmalı. Yoksa süzgeç hiçbir şey eşleştirmiyor
/// olabilir ve sıfır sessizce doğru görünürdü.</para>
/// </summary>
public sealed class AyarYuzeyiTests
{
    private readonly ITestOutputHelper _output;

    public AyarYuzeyiTests(ITestOutputHelper output) => _output = output;

    private static readonly XNamespace Xaml = "https://github.com/avaloniaui";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Ölçüm kullanıcının kendi ayar dosyasına yazmaz; her pencere kendi kumunda.</summary>
    private static string SettingsSandbox() =>
        Path.Combine(TestPaths.OutputRoot, "t177-ayar-yuzeyi", $"{Guid.NewGuid():N}", "settings.json");

    private static XElement Document() => XDocument.Load(TipSources.WindowXamlPath).Root!;

    /// <summary>Küçült sekmesinin alt ağacı; <c>PageShrink</c> kaydırıcısını taşıyan sekme.</summary>
    private static XElement ShrinkTab()
    {
        var tab = Document()
            .Descendants(Xaml + "TabItem")
            .SingleOrDefault(item => item.Descendants()
                .Any(node => (string?)node.Attribute(X + "Name") == "PageShrink"));

        Assert.True(tab is not null, "PageShrink kaydırıcısını taşıyan TabItem bulunamadı.");
        return tab!;
    }

    private static IEnumerable<XElement> Boxes(XElement scope) =>
        scope.Descendants(Xaml + "ComboBox");

    private static string Name(XElement box) =>
        (string?)box.Attribute(X + "Name") ?? "(adsız)";

    /// <summary>
    /// Biçimlemeye yazılmış seçenekleri olan kutular. Arka koddan doldurulan
    /// <c>CmbAdv*</c> kutularının gövdesi boştur; onları "sıfır seçenekli" sayıp
    /// üç eşiğinin altına almak ölçümü yalancı yapardı.
    /// </summary>
    private static IReadOnlyList<XElement> SmallBoxes(XElement scope) =>
        Boxes(scope)
            .Select(box => (Box: box, Items: box.Elements(Xaml + "ComboBoxItem").Count()))
            .Where(entry => entry.Items is > 0 and <= 3)
            .Select(entry => entry.Box)
            .ToList();

    private static IReadOnlyList<XElement> StretchedBoxes(XElement scope) =>
        Boxes(scope)
            .Where(box => (string?)box.Attribute("HorizontalAlignment") == "Stretch")
            .ToList();

    /// <summary>
    /// Üç ve altı seçenekli açılır liste kalmadı: iki seçenek anahtara, üç seçenek şeride
    /// döndü. Kural <c>docs/danisma/007</c>'den geliyor — sonucun adı varsa kullanıcı onu
    /// açmadan görmeli.
    /// </summary>
    [Fact]
    public void KucultSekmesindeUcVeAzSecenekliAcilirListeKalmadi()
    {
        var small = SmallBoxes(ShrinkTab());
        var elsewhere = SmallBoxes(Document()).Count;

        Assert.True(elsewhere > 0, "Tarayıcı belgenin hiçbir yerinde küçük kutu bulamadı; süzgeç ölü.");
        _output.WriteLine($"Belgenin tamamında {elsewhere} küçük kutu var (olumlu denetim).");

        Assert.True(
            small.Count == 0,
            $"Küçült sekmesinde ≤3 seçenekli {small.Count} açılır liste var: "
            + string.Join(", ", small.Select(Name)));
    }

    /// <summary>
    /// Denetim içeriği kadar yer kaplar. Genişliğe yayılan kutu, iki kelimelik bir seçenek
    /// için sütunun tamamını tüketiyordu.
    /// </summary>
    [Fact]
    public void KucultSekmesindeGenisligeYayilanAcilirListeKalmadi()
    {
        var stretched = StretchedBoxes(ShrinkTab());
        var elsewhere = StretchedBoxes(Document()).Count;

        Assert.True(elsewhere > 0, "Tarayıcı belgenin hiçbir yerinde yayılan kutu bulamadı; süzgeç ölü.");
        _output.WriteLine($"Belgenin tamamında {elsewhere} yayılan kutu var (olumlu denetim).");

        Assert.True(
            stretched.Count == 0,
            $"Küçült sekmesinde HorizontalAlignment=\"Stretch\" taşıyan {stretched.Count} açılır liste var: "
            + string.Join(", ", stretched.Select(Name)));
    }

    /// <summary>
    /// Kullanım amacı kutusu kalktı; üç seçeneği de yonga şeridinde zaten vardı. Ne
    /// biçimlemede ne arka kodda adı geçmeli.
    /// </summary>
    [Fact]
    public void KullanimAmaciKutusuKaynaktaHicGecmiyor()
    {
        var markup = File.ReadAllText(TipSources.WindowXamlPath);
        var code = File.ReadAllText(TipSources.WindowCodePath);

        int Count(string text, string name) => Regex.Matches(text, $@"\b{name}\b").Count;

        var alive = Count(markup, "CmbAdvCrf") + Count(code, "CmbAdvCrf");
        Assert.True(alive > 0, "Sayaç var olan bir adı bile bulamadı; düzenek ölü.");
        _output.WriteLine($"CmbAdvCrf {alive} kez geçiyor (olumlu denetim).");

        var gone = Count(markup, "CmbIntent") + Count(code, "CmbIntent");
        Assert.True(gone == 0, $"CmbIntent kaynakta hâlâ {gone} kez geçiyor.");
    }

    /// <summary>
    /// Pencerenin taban yüksekliğinde (<c>MinHeight</c>) varsayılan ayarlarla Küçült
    /// sekmesinin taşması pimli: ölçülen değer büyürse ölçüm kırmızıya düşer.
    ///
    /// <para><b>Neden "hiç taşmıyor" değil:</b> sayfa üç sütunun en uzunu kadar yüksek.
    /// Taban yükseklikte görüş alanı 625 px iken ayar sütununa hiç dokunulmasa bile
    /// ortadaki plan/önizleme sütunu tek başına 868 px istiyor. Yani T177'nin sahası olan
    /// ayar sütunu tamamen boşaltılsa dahi sayfa taşmaya devam ederdi; kalan taşma plan ve
    /// önizleme panellerinin taban yüksekliklerinden geliyor ve onları küçültmek yeni ölçü
    /// uydurmak demek olurdu. Ölçüm bu yüzden taşmayı sıfırlamıyor, <b>pimliyor</b>: sayı
    /// büyürse kırmızıya düşer. Sütun dökümü çıktıya yazılıyor.</para>
    ///
    /// <para>T177 turu 1: pim ölçülen değere çekildi. Bugünkü ölçü içerik 915, görüş alanı
    /// 625, <b>taşma 290</b>; aralık 285-295, yani ±5 piksel. Eskiden 0-300 yazıyordu ve
    /// taşmanın 10 piksel daha büyümesine sessizce izin veriyordu.</para>
    ///
    /// <para><b>720 pikselde sığma T180'e bağlı:</b> taşmayı orta sütunun taban boyu
    /// (<c>PlanPanelMinHeight</c>) tutuyor ve onu küçültmek karşılaştırma alanının yeniden
    /// tasarımı demek.</para>
    /// </summary>
    [Fact]
    public void TabanYukseklikteKucultSekmesininTasmasiBuyumuyor()
    {
        var reading = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = SettingsSandbox() };
            try
            {
                window.UseTurkish();

                var size = new Size(
                    window.TryFindResource("WindowPreferredWidth", out var width) ? (double)width! : window.MinWidth,
                    window.MinHeight);

                window.Width = double.NaN;
                window.Height = double.NaN;
                window.Measure(size);
                window.Arrange(new Rect(size));
                window.UpdateLayout();

                var root = (Layoutable)window.GetVisualChildren().Single();
                root.Measure(size);
                root.Arrange(new Rect(size));

                foreach (var node in window.GetVisualDescendants()) node.RenderTransform = null;

                var page = window.GetVisualDescendants().OfType<ScrollViewer>()
                    .Single(viewer => viewer.Name == "PageShrink");

                var grid = (Grid)page.Content!;
                var columns = grid.Children
                    .OfType<Control>()
                    .Select(child => (Column: Grid.GetColumn(child), Height: child.DesiredSize.Height))
                    .OrderBy(entry => entry.Column)
                    .ToList();

                return (size, page.Extent.Height, page.Viewport.Height, columns);
            }
            finally { window.Close(); }
        });

        _output.WriteLine(
            $"{reading.size.Width:0}x{reading.size.Height:0}: içerik {reading.Item2:0}, "
            + $"görüş alanı {reading.Item3:0}, taşma {reading.Item2 - reading.Item3:0}");

        foreach (var column in reading.columns)
            _output.WriteLine($"sütun {column.Column}: {column.Height:0}");

        Assert.True(reading.Item3 > 0, "Görüş alanı sıfır ölçüldü; düzenek ölü.");
        Assert.True(reading.columns.Count == 3, $"Sayfada 3 sütun bekleniyordu, {reading.columns.Count} bulundu.");

        var withoutSettings = reading.columns.Single(column => column.Column == 1).Height;
        Assert.True(
            withoutSettings > reading.Item3,
            $"Plan sütunu ({withoutSettings:0}) artık görüş alanına ({reading.Item3:0}) sığıyor; "
            + "taşmanın kaynağı ayar sütununa döndü, pim yeniden temellendirilmeli.");

        Assert.InRange(reading.Item2 - reading.Item3, 285d, 295d);
    }

    /// <summary>
    /// Katlanmış bölüm durumu gizlemez: ses bit hızı değişince kapalı başlık yeni değeri
    /// yazar.
    /// </summary>
    [Fact]
    public void KapaliSesBasligiYeniBitHiziniYaziyor()
    {
        var reading = AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = SettingsSandbox() };
            try
            {
                window.UseTurkish();

                Assert.False(window.AudioBody.IsVisible, "Ses bölümü varsayılanda açık geliyor.");

                window.CmbAdvAudioKbps.SelectedIndex = 1;
                var chosen = window.CmbAdvAudioKbps.SelectedItem as string ?? "";

                return (chosen, window.TxtAudioSummary.Text ?? "");
            }
            finally { window.Close(); }
        });

        _output.WriteLine($"Seçilen: {reading.chosen} — başlık: {reading.Item2}");

        Assert.False(string.IsNullOrWhiteSpace(reading.chosen), "Ses bit hızı kutusu boş.");
        Assert.Contains(reading.chosen, reading.Item2, StringComparison.Ordinal);
    }
}
