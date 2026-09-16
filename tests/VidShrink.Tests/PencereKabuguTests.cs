using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Pencere kabuğunun düzeni: üst şerit içerikle aynı gözde bir katman, başlık düğmeleri
/// içeriğin üstünde, gizleme sınıfı iki parçayı birden kapatıyor. Gizlenme kuralının ve
/// anahattın davranışı <see cref="OynaticiYolHaritasiTests"/>'te gerçek pencereden ölçülür.
/// </summary>
public class PencereKabuguTests
{
    private static string Xaml() => File.ReadAllText(TipSources.WindowXamlPath).Replace("\r\n", "\n");

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
}
