using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Durum değişince beliren yüzeyler (bildirim şeritleri, hata ve durum satırları, sonuç
/// paneli ve düğmeleri) saydamdan süzülerek gelir; ilerleme çubuğu değerine kayarak varır.
/// Ölçü gerçek saate değil animasyon saatinin vuruşlarına bağlı (<see cref="KareSayaci"/>):
/// canlandırmalı kol ara değer görülüp yüzey tam görünür olana dek pompalar, hareketi
/// azaltılmış kol <see cref="OlumsuzKare"/> vuruş bekler. Eski 400 ms pencere CI yükünde
/// saatin ilk vuruşundan önce kapanıyordu; canlandırma hiç yayınlanmadığı için olay
/// kaydı da ara değeri göremiyordu. Saydamlığın en düşüğü <c>OpacityProperty</c>
/// değişim olayından tutulur. <c>reduced-motion</c> sınıflı pencerede ara değer görülmez.
/// </summary>
public sealed class BelirisCanlandirmaTests
{
    private readonly ITestOutputHelper _output;

    public BelirisCanlandirmaTests(ITestOutputHelper output) => _output = output;

    public static TheoryData<string, string, bool> Yuzeyler()
    {
        var kollar = new TheoryData<string, string, bool>();
        foreach (var (ad, sinif) in new[]
                 {
                     ("UpdateNotice", "reveal-panel"), ("AppliedNotice", "reveal-panel"), ("PresetUndoBar", "reveal-panel"),
                     ("TxtSourceStatus", "reveal"), ("TxtAdvancedError", "reveal"), ("TxtFfmpegPathError", "reveal"),
                     ("TxtShellMenuStatus", "reveal"), ("BtnReveal", "reveal"), ("BtnShare", "reveal"), ("BtnConvertReveal", "reveal"),
                     ("ResultPanel", "reveal-panel"), ("WarningRow", "reveal"), ("TxtError", "reveal"), ("TxtNotice", "reveal"),
                 })
        {
            kollar.Add(ad, sinif, false);
            kollar.Add(ad, sinif, true);
        }

        return kollar;
    }

    /// <summary>
    /// Hareketi azaltılmış kolda beklenen saat vuruşu. Canlandırma ilk vuruşunda ilerleme 0'ı
    /// yayınlar; bu kadar vuruşta ara değer görülmediyse canlandırma kurulmamıştır.
    /// </summary>
    internal const int OlumsuzKare = 5;

    private static Control Bul(MainWindow pencere, string ad)
    {
        var kaydedici = pencere.RecorderPaneForTest;
        return pencere.FindControl<Control>(ad) ?? kaydedici.FindControl<Control>(ad) ?? throw new InvalidOperationException(ad);
    }

    private static void Hazirla(MainWindow pencere, bool azalt)
    {
        if (azalt) pencere.Classes.Add("reduced-motion");
        else pencere.Classes.Remove("reduced-motion");
    }

    /// <summary>
    /// Yüzey görünür olunca canlandırmalı pencerede en düşük saydamlık 1'in altına iner ve
    /// sonunda tam görünür; hareketi azaltılmış pencerede hiç 1'in altına inmez.
    /// </summary>
    [Theory]
    [MemberData(nameof(Yuzeyler))]
    public void BelirenYuzeySaydamdanGelir(string ad, string sinif, bool azalt)
    {
        var (sinifli, enAz, son, kare) = AppHost.Run(() =>
        {
            Strings.Use("en");
            var pencere = new MainWindow();
            Hazirla(pencere, azalt);
            pencere.Show();
            try
            {
                var yuzey = Bul(pencere, ad);
                if (yuzey.FindLogicalAncestorOfType<TabItem>() is { } sekme)
                {
                    sekme.IsVisible = true;
                    pencere.Tabs.SelectedItem = sekme;
                }

                for (var ata = yuzey.Parent as Control; ata is not null && ata is not TabItem; ata = ata.Parent as Control) ata.IsVisible = true;
                yuzey.IsVisible = false;
                Dispatcher.UIThread.RunJobs();
                KareSayaci.Pompala(pencere, k => k >= 2);
                var enAz = 1.0;
                yuzey.PropertyChanged += (_, e) =>
                {
                    if (e.Property == Avalonia.Visual.OpacityProperty) enAz = Math.Min(enAz, yuzey.Opacity);
                };
                yuzey.IsVisible = true;
                enAz = Math.Min(enAz, yuzey.Opacity);
                void Oku() => enAz = Math.Min(enAz, yuzey.Opacity);
                var kare = azalt
                    ? KareSayaci.Pompala(pencere, k => k >= OlumsuzKare, Oku)
                    : KareSayaci.Pompala(pencere, _ => enAz < 0.99 && yuzey.Opacity >= 0.9995, Oku);
                return (yuzey.Classes.Contains(sinif), enAz, yuzey.Opacity, kare);
            }
            finally { pencere.Close(); }
        });

        _output.WriteLine($"{ad} azalt={azalt} en az {enAz:0.###}, son {son:0.###}, {kare} kare");
        Assert.True(sinifli, $"{ad} '{sinif}' sınıfını taşımıyor.");
        Assert.Equal(1, son, 3);
        if (azalt) Assert.Equal(1, enAz, 3);
        else Assert.True(enAz < 0.99, $"{ad} en az {enAz:0.###} saydamlıkta ({kare} kare); geçiş yok, anında beliriyor.");
    }

    /// <summary>
    /// Seçiciler hareketi azaltma kapısını taşır; süreler <c>MotionFast</c> ve <c>MotionBase</c>
    /// belirteçlerinden gelir.
    /// </summary>
    [Fact]
    public void SecicilerKapiliSureBelirtecten()
    {
        var (satirlar, hizli, temel) = AppHost.Run(() =>
        {
            var uygulama = Avalonia.Application.Current!;
            var satirlar = uygulama.Styles.OfType<Style>()
                .Where(s => s.Animations.Count > 0 && s.Selector!.ToString().Contains(".reveal"))
                .Select(s => (s.Selector!.ToString(), ((Animation)s.Animations[0]).Duration))
                .ToList();
            return (satirlar, (TimeSpan)uygulama.FindResource("MotionFast")!, (TimeSpan)uygulama.FindResource("MotionBase")!);
        });

        Assert.Equal(2, satirlar.Count);
        Assert.All(satirlar, s => Assert.Contains(":not(.reduced-motion)", s.Item1));
        Assert.All(satirlar, s => Assert.Contains("[IsVisible=True]", s.Item1));
        Assert.Equal(temel, satirlar.Single(s => s.Item1.Contains(".reveal-panel")).Duration);
        Assert.Equal(hizli, satirlar.Single(s => !s.Item1.Contains(".reveal-panel")).Duration);
    }

    /// <summary>
    /// İlerleme çubuğu yeni değerine kayarak varır: canlandırmalı pencerede 0 ile 1 arasında
    /// ara değer okunur, hareketi azaltılmış pencerede değer tek adımda hedefte.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IlerlemeCubuguDegereKayar(bool azalt)
    {
        var (ara, son) = AppHost.Run(() =>
        {
            Strings.Use("en");
            var pencere = new MainWindow();
            Hazirla(pencere, azalt);
            pencere.Show();
            try
            {
                var cubuk = pencere.FindControl<ProgressBar>("Progress")!;
                cubuk.Value = 0;
                Dispatcher.UIThread.RunJobs();
                KareSayaci.Pompala(pencere, k => k >= 2);
                cubuk.Value = 1;
                var ara = 0;
                void Oku() { if (cubuk.Value > 0.01 && cubuk.Value < 0.99) ara++; }
                if (azalt) KareSayaci.Pompala(pencere, k => k >= OlumsuzKare, Oku);
                else KareSayaci.Pompala(pencere, _ => ara > 0 && cubuk.Value >= 0.9995, Oku);
                return (ara, cubuk.Value);
            }
            finally { pencere.Close(); }
        });

        _output.WriteLine($"azalt={azalt} ara örnek {ara}, son {son:0.###}");
        Assert.Equal(1, son, 3);
        if (azalt) Assert.Equal(0, ara);
        else Assert.True(ara > 0, "Çubuk ara değer göstermeden hedefe atladı.");
    }
}
