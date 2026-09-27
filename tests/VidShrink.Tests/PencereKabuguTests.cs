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
    /// Sekme şeridi içerikten sonra bildiriliyor, üstte kalıyor. Oynatıcı sekmesinde içerik
    /// iki satırı da kaplıyor: kendiliğinden gizlenen şerit göründüğünde görüntüyü aşağı
    /// itmiyor. Diğer sekmelerde şerit sabit durduğu için içerik ikinci satırdan başlıyor;
    /// kayan sayfa ve kaydırma çubuğu şeridin altına girmiyor (<c>YerlesimDenetimiTests</c>).
    /// </summary>
    [Fact]
    public void SekmeSeridiIcerigiItmiyor()
    {
        var controls = Controls();

        var icerik = controls.IndexOf("Name=\"SelectedContentHost\"", StringComparison.Ordinal);
        var serit = controls.IndexOf("Name=\"PART_ItemsPresenter\"", StringComparison.Ordinal);

        Assert.True(icerik > 0 && serit > 0, "şablonun iki parçası da bulunmalı");
        Assert.True(icerik < serit, "şerit içerikten sonra bildirilmeli ki üstte kalsın");
        Assert.Contains("Grid.Row=\"1\"", controls[icerik..serit]);
        var oynatici = controls.IndexOf("^[SelectedIndex=0] /template/ TransitioningContentControl#SelectedContentHost", StringComparison.Ordinal);
        Assert.True(oynatici > serit, "oynatıcı sekmesinin kapsama biçemi bulunmalı");
        var bitis = controls.IndexOf("</Style>", oynatici, StringComparison.Ordinal);
        Assert.Contains("<Setter Property=\"Grid.RowSpan\" Value=\"2\"/>", controls[oynatici..bitis]);
    }

    /// <summary>
    /// Başlık çubuğunun düğmeleri sekme şablonunun tek üst katmanında (<c>TopOverlay</c>):
    /// <c>TitleBarLayer</c> sekme denetiminin <c>UstPanel.Icerik</c> değeri, şablon onu
    /// seçili içerikten ve sekme şeridinden <b>sonra</b> bildiriyor. Avalonia'da aynı gözdeki
    /// kardeşlerin sırası z düzenidir: oynatıcının tam pencereyi kaplayan sahnesi düğmelerin
    /// üstüne binmiyor, sekmeler de düğmelerin altında kalıyor.
    ///
    /// <para>Katmanın kendi zemini yok; ortadaki boş sütun tıklamayı altındaki sekme şeridine
    /// geçiriyor, yalnız iki yandaki düğmeler hedef oluyor.</para>
    /// </summary>
    [Fact]
    public void BaslikDugmeleriIcerigiUstunde()
    {
        var xaml = Xaml();
        var controls = Controls();

        var bas = xaml.IndexOf("<izi:UstPanel.Icerik>", StringComparison.Ordinal);
        var son = xaml.IndexOf("</izi:UstPanel.Icerik>", StringComparison.Ordinal);
        var katman = xaml.IndexOf("<Border x:Name=\"TitleBarLayer\"", StringComparison.Ordinal);
        var sekmeSonu = xaml.IndexOf("</TabControl>", StringComparison.Ordinal);
        Assert.True(bas > 0 && bas < katman && katman < son && son < sekmeSonu, "başlık katmanı UstPanel.Icerik içinde olmalı");
        Assert.Contains("x:Name=\"TitleBarContent\"", xaml[katman..son]);
        Assert.DoesNotContain("Background", xaml[katman..(katman + 160)]);

        var icerik = controls.IndexOf("Name=\"SelectedContentHost\"", StringComparison.Ordinal);
        var ust = controls.IndexOf("<Panel Name=\"TopOverlay\"", StringComparison.Ordinal);
        var serit = controls.IndexOf("Name=\"PART_ItemsPresenter\"", StringComparison.Ordinal);
        var dugmeler = controls.IndexOf("Name=\"TopOverlayContent\"", StringComparison.Ordinal);
        Assert.True(icerik > 0 && icerik < ust && ust < serit && serit < dugmeler, "sıra: içerik, üst katman, şerit, düğmeler");
        Assert.Contains("{TemplateBinding izi:UstPanel.Icerik}", controls[dugmeler..(dugmeler + 200)]);
    }

    /// <summary>Gizleme sınıfı tek bağla tüm üst katmanı kapatıyor: şerit, düğmeler, paravan.</summary>
    [Fact]
    public void GizlemeSinifiIkiParcayiKapatiyor()
    {
        var xaml = Xaml();

        Assert.Contains("Selector=\"Window.chrome-hidden Border#TitleBar\"", xaml);
        Assert.Equal("Selector=\"Window.chrome-hidden TabControl#Tabs /template/ Panel#TopOverlay\"", Regex.Match(
            xaml, @"Selector=""Window\.chrome-hidden TabControl#Tabs[^""]*""").Value);
        Assert.Single(Regex.Matches(xaml, @"Selector=""Window\.chrome-hidden TabControl#Tabs"));
        Assert.DoesNotContain("Window.chrome-hidden Border#TitleBarLayer", xaml);
    }
}
