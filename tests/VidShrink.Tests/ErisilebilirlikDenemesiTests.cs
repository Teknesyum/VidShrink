using System.Diagnostics;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using VidShrink.App;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Later 78 (AYR-16, AYR-17), 2026-10-18: her sekmede ana iş yalnız klavyeyle dolaşılır.
/// Sekme sırası gerçek Tab tuşuyla (ham klavye girdisi, Win32 penceresi) yürütülür; her
/// durakta odaklanan denetimin otomasyon adı, ekran okuyucunun UIA'dan okuduğu ad, alınır.
/// Kayıt <c>docs/olcumler/erisilebilirlik-denemesi.md</c>.
/// </summary>
public sealed class ErisilebilirlikDenemesiTests
{
    private readonly ITestOutputHelper _output;

    public ErisilebilirlikDenemesiTests(ITestOutputHelper output) => _output = output;

    private static void Dongu(Func<bool> bitti, double saniye)
    {
        var saat = Stopwatch.StartNew();
        while (!bitti() && saat.Elapsed.TotalSeconds < saniye)
        {
            using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(2));
            Dispatcher.UIThread.MainLoop(dilim.Token);
        }
    }

    private static IInputElement? Odak(MainWindow w)
    {
        if (!w.IsActive || w.FocusManager!.GetFocusedElement() is null)
        {
            w.Activate();
            Dongu(() => w.IsActive && w.FocusManager!.GetFocusedElement() is not null, 3);
        }
        return w.FocusManager!.GetFocusedElement();
    }

    internal sealed record Durak(string Tur, string Ad, string Okunan, bool Gorunur);

    internal sealed record Yuruyus(List<Durak> Duraklar, bool BasaDondu, string? Takildi);

    internal static Yuruyus Yuru(int sekme, bool dolu, string dil = "tr")
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "erisilebilirlik");
        Directory.CreateDirectory(klasor);
        return AppHost.Run(() =>
        {
            VidShrink.App.Localization.Strings.Use(dil);
            var w = new MainWindow { SettingsPathOverride = Path.Combine(klasor, Guid.NewGuid().ToString("N") + ".json"), Width = 1280, Height = 900 };
            var duraklar = new List<Durak>();
            var basaDondu = false;
            string? takildi = null;
            try
            {
                w.Show();
                Dongu(() => w.IsLoaded, 10);
                if (dolu)
                {
                    w.LoadWithoutProbing(@"C:\Kayitlar\ornek.mkv", GorselDenetimTests.Ornek());
                    w.SettleFades();
                }
                w.Tabs.SelectedIndex = sekme;
                Dongu(() => false, 1);
                var ilk = (InputElement)w.Tabs.ContainerFromIndex(sekme)!;
                w.Activate();
                Dongu(() => w.IsActive, 3);
                ilk.Focus(NavigationMethod.Tab);
                Dongu(() => ReferenceEquals(Odak(w), ilk), 2);
                Assert.Same(ilk, Odak(w));

                var simdiki = (IInputElement)ilk;
                for (var i = 0; i < 300; i++)
                {
                    OynaticiListeTests.Tus(w, Key.Tab);
                    Dongu(() => !ReferenceEquals(w.FocusManager!.GetFocusedElement(), simdiki), 0.5);
                    var sonraki = Odak(w);
                    if (ReferenceEquals(sonraki, ilk)) { basaDondu = true; break; }
                    if (sonraki is null || ReferenceEquals(sonraki, simdiki))
                    {
                        takildi = (sonraki as Control)?.Name ?? sonraki?.GetType().Name ?? "odak yok";
                        break;
                    }
                    var c = (Control)sonraki;
                    var okunan = ControlAutomationPeer.CreatePeerForElement(c).GetName() ?? "";
                    duraklar.Add(new Durak(c.GetType().Name, c.Name ?? "", okunan, c.IsEffectivelyVisible));
                    simdiki = sonraki;
                }
            }
            finally
            {
                w.Close();
                VidShrink.App.Localization.Strings.Use("en");
            }
            return new Yuruyus(duraklar, basaDondu, takildi);
        });
    }

    [Theory]
    [InlineData(0, false, 15)]
    [InlineData(1, false, 15)]
    [InlineData(2, false, 25)]
    [InlineData(2, true, 30)]
    [InlineData(3, false, 35)]
    [InlineData(4, false, 20)]
    [InlineData(6, false, 60)]
    public void SekmeSirasiBasaDonerVeHerDurakAdiylaOkunur(int sekme, bool dolu, int enAzDurak)
    {
        var y = Yuru(sekme, dolu);
        foreach (var d in y.Duraklar) _output.WriteLine($"{d.Tur}\t{d.Ad}\t[{d.Okunan}]");

        Assert.True(y.Takildi is null, $"Tab odağı {y.Takildi} üstünde bıraktı; sekme sırası başa dönmüyor.");
        Assert.True(y.BasaDondu, "Sekme sırası 300 adımda seçili sekmeye dönmedi.");
        Assert.True(y.Duraklar.Count >= enAzDurak, $"Yalnız {y.Duraklar.Count} durak dolaşıldı; ölçü boş koşuyor.");
        Assert.All(y.Duraklar, d => Assert.True(d.Gorunur, $"{d.Tur} {d.Ad} görünmezken odak aldı."));
        Assert.All(y.Duraklar, d => Assert.False(string.IsNullOrWhiteSpace(d.Okunan), $"{d.Tur} {d.Ad} ekran okuyucuya adsız gidiyor."));
        Assert.All(y.Duraklar, d => Assert.DoesNotContain("{0}", d.Okunan, StringComparison.Ordinal));
    }

    /// <summary>
    /// Kapalı bölümlerdeki girdi alanları (Gelişmiş, ön ayar adı, özel çözünürlük) Tab
    /// yürüyüşüne girmiyor; onların adı biçimlemeden okunur. Paylaşım bağlantısı kutusu
    /// dışarıda: ekran okuyucu içindeki adresi değer olarak okuyor.
    /// </summary>
    [Fact]
    public void AnaPenceredeAdsizGirdiAlaniKalmaz()
    {
        var xaml = File.ReadAllText(TipSources.WindowXamlPath);
        var adsiz = System.Text.RegularExpressions.Regex
            .Matches(xaml, "<(TextBox|ComboBox|Slider)\\s([^>]*)>")
            .Select(m => m.Groups[2].Value)
            .Where(a => !a.Contains("AutomationProperties.Name", StringComparison.Ordinal) && !a.Contains("Watermark=", StringComparison.Ordinal))
            .Select(a => System.Text.RegularExpressions.Regex.Match(a, "x:Name=\"(\\w+)\"").Groups[1].Value)
            .ToList();

        Assert.True(xaml.Split("<ComboBox ").Length > 30, "Biçimlemede ComboBox bulunamadı; tarama boş koşuyor.");
        Assert.Equal(["TxtShareLink"], adsiz);
    }
}
