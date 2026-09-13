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
    /// </summary>
    [Fact]
    public void EsikBaslikYuksekligindenGeliyor()
    {
        var code = Code();

        Assert.Contains("ShowChrome(e.GetPosition(this).Y <= TitleBar.Height)", code);
        Assert.Contains("ShowChrome(false);", code);
        Assert.Contains("PointerExited += (_, _) => ShowChrome(false);", code);
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
}
