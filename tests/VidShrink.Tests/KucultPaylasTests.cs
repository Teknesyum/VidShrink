using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using VidShrink.App.Localization;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Kullanıcının şikâyeti: "Shrink (Küçült) sekmesinde neden paylaş tuşu yok?" — kaynak
/// <c>.calisma/kucult-paylas/tarif.md</c>. Denetim üç olasılık sıraladı: (a) sağ tıkla
/// açılan <see cref="ShrinkJobWindow"/>'da paylaş hiç yok, (b) ana penceredeki düğme iş
/// bittikten sonra görünür ama kırpılmış/tıklanamaz çıkıyor, (c) iş yokken hiç görünmüyor —
/// bu tasarım gereği.
///
/// <para>Bu dosya üçünü de ölçer. <see cref="AnaPencereninPaylasDugmesiIsBittiktenSonraGercektenGorunurVeTiklanabilir"/>
/// başsız pencerede gerçek bir yerleşim geçişi koşturup <c>IsVisible</c>, <c>Bounds</c> ve
/// ata görünürlüğünü ölçer: (b) ve (c) burada çürütülüyor — düğme iş bitince gerçekten
/// görünür, boyutlu ve tıklanabilir çıkıyor. Geriye (a) kalıyor: kabuk menüsünden açılan
/// <see cref="ShrinkJobWindow"/>'da paylaş hiç yoktu, bu dosyanın kalan ölçüleri onu ekliyor.</para>
/// </summary>
public sealed class KucultPaylasTests
{
    private readonly ITestOutputHelper _output;

    public KucultPaylasTests(ITestOutputHelper output) => _output = output;

    private static string Read(params string[] parts)
        => File.ReadAllText(Path.Combine(new[] { TipSources.Root }.Concat(parts).ToArray()));

    private static string JobXaml() => Read("src", "VidShrink.App", "ShrinkJobWindow.axaml");

    private static string JobCode() => Read("src", "VidShrink.App", "ShrinkJobWindow.axaml.cs");

    private static string JobShareCode() => Read("src", "VidShrink.App", "ShrinkJobWindow.Paylas.cs");

    // ---- (a) ShrinkJobWindow'a paylaş eklendi -------------------------------------------

    /// <summary>Kabuk menüsünün ilerleme penceresinde paylaşımın dört öğesi var.</summary>
    [Theory]
    [InlineData("BtnShare")]
    [InlineData("BtnShareCancel")]
    [InlineData("ShareProgress")]
    [InlineData("TxtShareLink")]
    [InlineData("TxtShareStatus")]
    public void KucultIsPenceresindePaylasOgeleriVar(string name)
        => Assert.Contains($"x:Name=\"{name}\"", JobXaml(), System.StringComparison.Ordinal);

    /// <summary>
    /// Bu pencerede diğer düğmeler XAML <c>Click=</c> yerine yapıcıda koddan bağlanıyor
    /// (<c>BtnReveal.Click += OnReveal;</c>); yeni düğmeler de aynı kuralı izliyor.
    /// </summary>
    [Fact]
    public void KucultIsPenceresiPaylasiKoddaBagliyor()
    {
        var code = JobCode();
        Assert.Contains("BtnShare.Click += OnShare;", code, System.StringComparison.Ordinal);
        Assert.Contains("BtnShareCancel.Click += OnShareCancel;", code, System.StringComparison.Ordinal);
        Assert.Contains("BtnShareCopy.Click += OnCopyShareLink;", code, System.StringComparison.Ordinal);
        Assert.DoesNotContain("Click=\"OnShare\"", JobXaml(), System.StringComparison.Ordinal);
    }

    /// <summary>Teslim edilen yol ekrandaki metin kutusundan değil, kendi alanından okunuyor.</summary>
    [Fact]
    public void KucultIsPaylasimiYoluAlandanOkur()
    {
        var code = JobShareCode();
        Assert.Contains("_outputs.Count > 0 ? _outputs[^1] : null", code, System.StringComparison.Ordinal);
        Assert.DoesNotContain("var path = TxtShareLink.Text;", code, System.StringComparison.Ordinal);
        Assert.DoesNotContain("var path = TxtMessage.Text;", code, System.StringComparison.Ordinal);
    }

    /// <summary>Paylaşım ikinci bir yükleyici yazmıyor: iş <c>ShareFlow</c> ve <c>Core/Share</c> katmanından geçiyor.</summary>
    [Fact]
    public void KucultIsPaylasimiAyniKatmandanGeciyor()
    {
        var code = JobShareCode();
        Assert.Contains("new ShareFlow(", code, System.StringComparison.Ordinal);
        Assert.Contains("CoreShare.ShareProviderFactory.Create(", code, System.StringComparison.Ordinal);
        Assert.Contains("flow.ShareAsync(target, path", code, System.StringComparison.Ordinal);
        Assert.DoesNotContain("HttpRequestMessage", code, System.StringComparison.Ordinal);
    }

    /// <summary>Bitmiş her iş için düğme yeniden hazırlanıyor: sıradaki kuyruk öğesi de paylaşabiliyor.</summary>
    [Fact]
    public void KucultIsPaylasimiHerBitenOgedeSifirlaniyor()
    {
        var code = JobCode();
        Assert.Contains("ResetShare(false);", code, System.StringComparison.Ordinal);
        Assert.Contains("ResetShare(true);", code, System.StringComparison.Ordinal);
    }

    /// <summary>Kırk iki dilin hepsinde metin var; hiçbiri yeni anahtar açmıyor, hepsi yeniden kullanım.</summary>
    [Theory]
    [InlineData("main.action.share")]
    [InlineData("main.action.cancel-upload")]
    [InlineData("main.action.copy")]
    [InlineData("settings.share.nothing")]
    [InlineData("settings.share.targets-missing")]
    [InlineData("settings.share.uploading")]
    [InlineData("settings.share.shared-until")]
    [InlineData("settings.share.shared")]
    [InlineData("settings.share.cancelled")]
    [InlineData("settings.share.failed")]
    public void KucultIsPaylasimindaKullanilanAnahtarlarButunDillerdeVar(string key)
    {
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            Assert.True(values.ContainsKey(key), $"{language} dilinde {key} yok.");
            Assert.False(string.IsNullOrWhiteSpace(values[key]), $"{language} dilinde {key} boş.");
        }
    }

    /// <summary>
    /// Düğme gerçekten pencerenin durumuna bağlı: iş bitmeden görünmüyor, bitince görünüyor,
    /// yeni iş başlayınca yeniden kayboluyor. Kaynak metin değil, çalışan pencere ölçülüyor.
    /// </summary>
    [Fact]
    public void KucultIsPenceresindePaylasDugmesiIsDurumunuGercektenTakipEdiyor()
    {
        AppHost.Run(() =>
        {
            var window = new ShrinkJobWindow(new ShellShrinkStartup(null, ShrinkArgumentProblem.NoTarget, null), null);
            try
            {
                Assert.False(window.ShareButtonVisibleForTest, "İş bitmeden düğme görünüyor.");

                window.AddOutputForTest(@"C:\ornek\video_shrunk.mp4");
                window.ResetShareForTest(true);
                Assert.True(window.ShareButtonVisibleForTest, "İş bitince düğme görünmüyor.");

                window.ResetShareForTest(false);
                Assert.False(window.ShareButtonVisibleForTest, "Yeni iş başlayınca eski düğme görünür kalıyor.");
            }
            finally { window.Close(); }
        });
    }

    // ---- (b)/(c) ana penceredeki düğmenin gerçek görünürlüğü ----------------------------

    private static double Token(MainWindow window, string key)
    {
        Assert.True(window.TryFindResource(key, out var value), $"{key} belirteci yok.");
        return (double)value!;
    }

    /// <summary>
    /// Denetimin asıl sorusu: iş bittikten sonra ana penceredeki <c>BtnShare</c> gerçekten
    /// görünür, boyutlu ve tıklanabilir mi — yoksa görünüyormuş gibi durup kırpılıyor mu.
    ///
    /// <para>Başsız pencere gerçek bir yerleşim geçişi koşturur (<see cref="Window.Measure"/>
    /// /<see cref="Window.Arrange"/>/<see cref="TopLevel.UpdateLayout"/>, ardından kökün
    /// yeniden ölçümü — <c>WindowState="Maximized"</c> olduğu için pencerenin kendi sınırı
    /// hep <c>ClientSize</c>'a döner, ölçü kökte alınır). İş bitmeden düğme gizli, iş
    /// bitince üçü de doğrulanıyor: <c>IsVisible</c>, sıfırdan büyük <c>Bounds</c>, atalarının
    /// hiçbiri gizli değil.</para>
    /// </summary>
    [Fact]
    public void AnaPencereninPaylasDugmesiIsBittiktenSonraGercektenGorunurVeTiklanabilir()
    {
        AppHost.Run(() =>
        {
            var window = new MainWindow();
            try
            {
                window.Tabs.SelectedIndex = window.ShrinkTabIndex;

                Assert.False(window.BtnShare.IsVisible, "İş yokken düğme zaten görünüyor — (c) beklenen tasarım değil.");

                window.ResetShareForTest(true);

                var size = new Size(Token(window, "WindowPreferredWidth"), Token(window, "WindowPreferredHeight"));
                window.Width = double.NaN;
                window.Height = double.NaN;
                window.Measure(size);
                window.Arrange(new Rect(size));
                window.UpdateLayout();

                var root = (Layoutable)window.GetVisualChildren().Single();
                foreach (var node in window.GetVisualDescendants().OfType<Layoutable>()) node.InvalidateMeasure();
                root.InvalidateMeasure();
                root.Measure(size);
                root.Arrange(new Rect(size));

                _output.WriteLine($"BtnShare.IsVisible={window.BtnShare.IsVisible} Bounds={window.BtnShare.Bounds} IsShown={window.BtnShare.IsShown()}");

                Assert.True(window.BtnShare.IsVisible, "İş bitince düğme IsVisible=false kalıyor.");
                Assert.True(window.BtnShare.IsShown(), "Bir ata gizli — düğme görsel ağaçta gösterilmiyor.");
                Assert.True(window.BtnShare.Bounds.Width > 0 && window.BtnShare.Bounds.Height > 0,
                    $"Düğme kırpılmış: Bounds={window.BtnShare.Bounds}.");
                Assert.True(window.BtnShare.IsEnabled, "Düğme görünüyor ama tıklanamıyor.");
            }
            finally { window.Close(); }
        });
    }
    /// <summary>
    /// İş penceresinin iki boyut satırı ailenin yazımını ve <b>pencerenin kendi dilinin</b>
    /// kültürünü kullanıyor. Eskiden ikisi de <c>InvariantCulture</c> okuyordu: Türkçe
    /// arayüzde sapma <c>1.23</c>, tavan aşımı <c>12.0</c> diye noktayla yazılıyordu.
    /// Beklenen değer <see cref="Bicim.Boyut"/> yüzeyinden ve pencerenin dilinden
    /// kuruluyor — böylece hem yazım hem kültür kaynağı tek ölçüyle pimleniyor.
    /// </summary>
    [Fact]
    public void IsPenceresininBoyutSatirlariAileninYazimiylaVePencereninDiliyle()
    {
        AppHost.Run(() =>
        {
            var window = new ShrinkJobWindow(new ShellShrinkStartup(null, ShrinkArgumentProblem.NoTarget, null), null);
            try
            {
                var kultur = Strings.CultureOf(window.Language);

                var asan = new EncodeResult(true, @"C:\ornekideo_shrunk.mp4", 17.23, null!, 1, null, OverTarget: true);
                var bitti = window.BittiSatiri(asan, 16, new EncodePlan());
                Assert.Contains(Bicim.Boyut.Sapma(17.23 - 16, kultur), bitti);

                var tavan = new EncodeResult(false, @"C:\ornekideo_shrunk.mp4", 12.0, null!, 3, null, CeilingExceeded: true);
                var hata = window.HataSatiri(tavan, 16, new EncodePlan());
                Assert.Contains(Bicim.Boyut.Mb(12.0, kultur), hata);
            }
            finally { window.Close(); }
        });
    }
}
