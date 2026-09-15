using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Pencere kabuğunun iki kararı: üst şerit gizli duruyor ve işaretçi üste gidince beliriyor,
/// oynatıcı sekmesindeyken kabuk kenarlığı çizilmiyor. Ölçülen şey çizim değil kaynağın
/// kurduğu düzen: şeridin katman olması, eşiğin uydurulmaması, kenarlık kuralının tek yerde
/// durması.
/// </summary>
public class PencereKabuguTests
{
    private static string Xaml() => File.ReadAllText(TipSources.WindowXamlPath).Replace("\r\n", "\n");

    private static string Code() => File.ReadAllText(TipSources.WindowCodePath);

    private static string Controls() => File.ReadAllText(
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "Themes", "Controls.axaml"));

    /// <summary>Şerit ve içerik aynı gözde: sekme denetimi artık satır paylaştırmıyor.</summary>
    [Fact]
    public void SeritIcerikleAyniGozde()
    {
        var xaml = Xaml();

        Assert.Contains("<TabControl x:Name=\"Tabs\" Theme=", xaml);
        Assert.DoesNotContain("<TabControl x:Name=\"Tabs\" Grid.Row", xaml);
        Assert.Contains("<Border x:Name=\"TitleBar\"\n              VerticalAlignment=\"Top\"", xaml);
    }

    /// <summary>
    /// Şablonda içerik iki satırı da kaplıyor ve sekme şeridi ondan sonra bildiriliyor:
    /// şerit içeriğin üstünde duruyor, göründüğünde içeriği aşağı itmiyor.
    /// </summary>
    [Fact]
    public void SekmeSeridiIcerigiItmiyor()
    {
        var controls = Controls();

        var icerik = controls.IndexOf("Name=\"SelectedContentHost\"", StringComparison.Ordinal);
        var serit = controls.IndexOf("Name=\"PART_ItemsPresenter\"", StringComparison.Ordinal);

        Assert.True(icerik > 0 && serit > 0, "şablonun iki parçası da bulunmalı");
        Assert.True(icerik < serit, "şerit içerikten sonra bildirilmeli ki üstte kalsın");
        Assert.Contains("Grid.RowSpan=\"2\"", controls[icerik..serit]);
    }

    /// <summary>
    /// Başlık çubuğunun düğmeleri sekme denetiminden <b>sonra</b> bildiriliyor. Avalonia'da
    /// aynı gözdeki kardeşlerin sırası z düzenidir: düğmeler önce bildirildiği sürece
    /// oynatıcı sekmesinin tam pencereyi kaplayan donuk sahnesi onların üstüne biniyordu,
    /// şerit geri gelince düğmeler görünmüyor ve tıklama oynatıcıya gidiyordu.
    ///
    /// <para>Bandın zemini <c>TitleBar</c>'da kaldı, içerik <c>TitleBarLayer</c>'a çıktı.
    /// Katmanın kendi zemini yok; ortadaki boş sütun tıklamayı altındaki sekme şeridine
    /// geçiriyor, yalnız iki yandaki düğmeler hedef oluyor.</para>
    /// </summary>
    [Fact]
    public void BaslikDugmeleriIcerigiUstunde()
    {
        var xaml = Xaml();

        var serit = xaml.IndexOf("</TabControl>", StringComparison.Ordinal);
        var katman = xaml.IndexOf("<Border x:Name=\"TitleBarLayer\"", StringComparison.Ordinal);

        Assert.True(serit > 0 && katman > 0, "iki parça da bulunmalı");
        Assert.True(katman > serit, "başlık katmanı sekme denetiminden sonra bildirilmeli");
        Assert.Contains("x:Name=\"TitleBarContent\"", xaml[katman..]);
        Assert.DoesNotContain("Background", xaml[katman..(katman + 160)]);
        Assert.Contains("Selector=\"Window.chrome-hidden Border#TitleBarLayer\"", xaml);
    }

    /// <summary>Gizleme sınıfı hem başlık çubuğunu hem sekme şeridini kapatıyor.</summary>
    [Fact]
    public void GizlemeSinifiIkiParcayiKapatiyor()
    {
        var xaml = Xaml();

        Assert.Contains("Selector=\"Window.chrome-hidden Border#TitleBar\"", xaml);
        Assert.Contains("PART_ItemsPresenter", Regex.Match(
            xaml, @"Selector=""Window\.chrome-hidden TabControl#Tabs[^""]*""").Value);
    }

    /// <summary>
    /// Belirme eşiği uydurulmuyor: işaretçinin y'si başlık çubuğunun kendi yüksekliğiyle
    /// karşılaştırılıyor, kaynağa ikinci bir sayı yazılmıyor.
    ///
    /// <para>Pim önce <c>ShowChrome(false)</c> arıyordu, yani şerit her sekmede kendiliğinden
    /// kayboluyordu. 13 Eylül 2026'da gizlenme yalnız oynatıcıya bağlandı: kaybolma artık
    /// koşulsuz değil, <see cref="MainWindow.ChromeHidesItself"/>'in tersine bakıyor.
    /// Değişen şey eşik değil, eşiğin uygulandığı yer — ölçünün eşik satırı aynı kaldı.</para>
    /// </summary>
    [Fact]
    public void EsikBaslikYuksekligindenGeliyor()
    {
        var code = Code();

        Assert.Contains("ShowChrome(e.GetPosition(this).Y <= TitleBar.Height)", code);
        Assert.Contains("PointerExited += (_, _) => ShowChrome(!ChromeHidesItself);", code);
    }

    /// <summary>
    /// Üst şerit yalnız oynatıcı sekmesinde kendiliğinden küçülüyor. Diğer sekmelerde
    /// işaretçi nereye giderse gitsin şerit yerinde duruyor; gizlenme oynatıcının kendi
    /// ihtiyacı, uygulamanın geneline dayatılan bir davranış değil.
    /// </summary>
    [Fact]
    public void GizlenmeYalnizOynaticidaGecerli()
    {
        var code = Code();

        Assert.Contains("internal bool ChromeHidesItself => Tabs.SelectedIndex == PlayerTabIndex;", code);
        Assert.Contains("private void ApplyChromeMode() => ShowChrome(!ChromeHidesItself);", code);
        Assert.Contains("Tabs.SelectionChanged += (_, _) => ApplyChromeMode();", code);
        Assert.Contains("if (!ChromeHidesItself)", code);
    }

    /// <summary>Kenarlık kuralı tek yerde: oynatıcı sekmesi tam ekranla aynı kolda.</summary>
    [Fact]
    public void OynaticiSekmesindeAnahatYok()
    {
        var code = Code();

        Assert.Contains(
            "WindowShell.BorderThickness = maximized || Tabs.SelectedIndex == PlayerTabIndex",
            code);
        Assert.Contains("Tabs.SelectionChanged += (_, _) => ApplyWindowFrame();", code);
    }

    /// <summary>
    /// Sahnenin kendisinde de anahat yok. Kabuk kenarlığı oynatıcı sekmesinde kalkıyor ama
    /// <c>Stage</c> Panel temasından bir kenarlık daha alıyordu; görüntünün etrafında
    /// çerçeve kalmıyor.
    /// </summary>
    [Fact]
    public void SahneninEtrafindaAnahatYok()
    {
        var xaml = File.ReadAllText(
            Path.Combine(TipSources.Root, "src", "VidShrink.App", "Playback", "PlayerView.axaml"));

        var sahne = xaml[xaml.IndexOf("x:Name=\"Stage\"", StringComparison.Ordinal)..];
        var kapanis = sahne.IndexOf(">", StringComparison.Ordinal);

        Assert.Contains("BorderThickness=\"0\"", sahne[..kapanis]);
        Assert.Contains("CornerRadius=\"0\"", sahne[..kapanis]);
    }
}
