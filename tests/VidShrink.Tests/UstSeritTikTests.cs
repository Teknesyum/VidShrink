using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Ust seridin tik tutmasi. <c>InputHitTest</c> penceresiz kosuda olculemiyor
/// (bkz. <see cref="ComparisonPanelTests"/>), bu yuzden olculen sey geometri:
/// baslik katmanindaki tik tutan her parcanin dikdortgeni sekme seridinin
/// dikdortgenleriyle kesisiyor mu. Katman en sonra bildirildigi icin kesisen her
/// piksel sekmeyi yutar.
/// </summary>
public sealed class UstSeritTikTests
{
    

    private static string Olc(int sekme, Size WindowSize, string dil = "en", UpdateBadgeState? rozet = null)
        => AppHost.Run(() =>
        {
            Strings.Use(dil);
            var window = new MainWindow();
            try
            {
            window.TabAdvanced.IsVisible = true;
            if (rozet is { } durum) window.SetUpdateBadge(durum);
            Yerlestir(window, WindowSize);

            var tabs = window.GetVisualDescendants().OfType<TabControl>().First(t => t.Name == "Tabs");
            tabs.SelectedIndex = sekme;
            foreach (var node in window.GetVisualDescendants().OfType<Layoutable>()) node.InvalidateMeasure();
            Yerlestir(window, WindowSize);
            Yerlestir(window, WindowSize);
            Yerlestir(window, WindowSize);

            var katman = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "TitleBarLayer");

            if (!katman.IsVisible || window.Classes.Contains("chrome-hidden"))
                return $"GIZLI en={WindowSize.Width:0} sekme={sekme}  baslik katmani kapali" + Environment.NewLine;

            var tutan = katman.GetVisualDescendants().OfType<Control>()
                .Where(c => c is Button or TextBlock or Image)
                .Where(c => c.IsVisible && c.IsHitTestVisible && c.Bounds is { Width: > 0, Height: > 0 })
                .Where(c => c.GetVisualAncestors().OfType<Control>().All(a => a.IsHitTestVisible))
                .Select(c => (Ad: Adi(c), Yer: Yeri(c, window)))
                .Where(x => x.Yer is not null)
                .ToArray();

            var rapor = new StringBuilder();
            var marka = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "TitleBrand");
            rapor.AppendLine($"OLCU  dil={dil} rozet={rozet} en={WindowSize.Width:0}  sekme={sekme}  TitleBrand={Y(Yeri(marka, window)!.Value)}  TabsPadding={tabs.Padding}");

            var sag = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "WindowButtons")
                .GetVisualAncestors().OfType<StackPanel>().First();
            rapor.AppendLine($"SAG   en={WindowSize.Width:0} sekme={sekme}  {Y(Yeri(sag, window)!.Value)}");

            foreach (var item in window.GetVisualDescendants().OfType<TabItem>())
            {
                if (!item.IsVisible || item.Bounds.Width <= 0) continue;
                var yer = Yeri(item, window);
                if (yer is null) continue;

                var engel = tutan.Where(t => t.Yer!.Value.Intersects(yer.Value)).Select(t => t.Ad).ToArray();
                var ad = item.Name ?? item.Header?.ToString() ?? "TabItem";
                rapor.AppendLine(engel.Length == 0
                    ? $"TAMAM  en={WindowSize.Width:0} sekme={sekme}  {ad}  {Y(yer.Value)}"
                    : $"ENGEL  en={WindowSize.Width:0} sekme={sekme}  {ad}  {Y(yer.Value)}  ustunde={string.Join(", ", engel)}");
            }

            return rapor.ToString();
            }
            finally
            {
                window.Close();
                Strings.Use("en");
            }
        });

    internal static void Yerlestir(MainWindow window, Size olcu)
    {
        BassizYerlesim.PlatformBoyu(window, olcu);
        window.Width = double.NaN;
        window.Height = double.NaN;
        window.Measure(olcu);
        window.Arrange(new Rect(olcu));
        window.UpdateLayout();

        var kok = (Layoutable)window.GetVisualChildren().Single();
        kok.InvalidateMeasure();
        kok.Measure(olcu);
        kok.Arrange(new Rect(olcu));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        typeof(MainWindow).GetMethod("AlignTabsToTitle",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(window, null);

        kok.InvalidateMeasure();
        kok.Measure(olcu);
        kok.Arrange(new Rect(olcu));
        BassizYerlesim.IkinciGecis(window, kok, olcu);
    }

    private static string Adi(Control c) => c.Name is { } n ? $"{c.GetType().Name}#{n}" : c.GetType().Name;

    private static string Y(Rect r) => $"[{r.X:0},{r.Y:0} {r.Width:0}x{r.Height:0}]";

    private static Rect? Yeri(Control c, Visual kok)
    {
        var sol = c.TranslatePoint(new Point(0, 0), kok);
        return sol is null ? null : new Rect(sol.Value, c.Bounds.Size);
    }

    /// <summary>Hicbir sekme baslik katmanindaki bir parcanin altinda kalmiyor.</summary>
    [Fact]
    public void SeritSekmeleriYutmuyor()
    {
        var rapor = new StringBuilder();
        foreach (var en in new double[] { 1136, 1280, 1418, 1560 })
            for (var i = 0; i < 5; i++) rapor.Append(Olc(i, new Size(en, 1060)));

        var klasor = Path.Combine(TipSources.Root, ".calisma", "serit-tik");
        Directory.CreateDirectory(klasor);
        File.WriteAllText(Path.Combine(klasor, "isabet.txt"), rapor.ToString(), new UTF8Encoding(false));

        var engeller = rapor.ToString().Split('\n').Where(s => s.StartsWith("ENGEL")).ToArray();
        Assert.True(engeller.Length == 0, string.Join("\n", engeller));

        Kapat(klasor, "isabet.txt");
    }

    /// <summary>
    /// Guncelleme rozeti acilistan sonra beliriyor ve dil seridi degistiriyor; ikisi de
    /// sag grubu genisletip sekmelerin ustune bindiriyordu (1280'de "Denetleniyor" ve dil
    /// dugmeleri Gelismis ile Ayarlar'in uzerindeydi). Her dilde, en uzun rozet yazisiyla,
    /// en dar pencereden genise kadar hicbir sekme ortulmuyor.
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    public void RozetVeDilSekmeleriOrtmuyor(string dil)
    {
        var rapor = new StringBuilder();
        foreach (var en in new double[] { 1136, 1280, 1418 })
            foreach (var durum in new[] { UpdateBadgeState.Checking, UpdateBadgeState.UpToDate, UpdateBadgeState.NewVersion })
                for (var i = 0; i < 6; i++) rapor.Append(Olc(i, new Size(en, 1060), dil, durum));

        var klasor = Path.Combine(TipSources.Root, ".calisma", "serit-tik");
        Directory.CreateDirectory(klasor);
        var ad = $"rozet-{dil}.txt";
        File.WriteAllText(Path.Combine(klasor, ad), rapor.ToString(), new UTF8Encoding(false));

        var engeller = rapor.ToString().Split('\n').Where(s => s.StartsWith("ENGEL")).ToArray();
        Assert.True(engeller.Length == 0, string.Join("\n", engeller));

        Kapat(klasor, ad);
    }

    /// <summary>
    /// Görsel denetim bulgu 22: son sekme ile sağ grup arasında en az <c>SpaceLg</c> kalır.
    /// Kademe hesabı yalnız örtüşmeyi önlüyordu; el 1136'da "Ρυθμίσεις" pencere düğmelerine
    /// yaklaşık 10 px yaklaşıyordu. Boşluk her dilde, darından genişine her boyda ölçülür.
    /// Kırpılma taraması: dil düğmeleri de çekildiğinde el 1136'da Gelişmiş açıkken son sekme
    /// pencere düğmelerinin 9 px altına giriyordu; dördüncü kademe sekmeleri simgesiz dizer.
    /// Yerleşim <see cref="BassizYerlesim"/> ile iki geçişli; tek geçişte gizlenen parçalar
    /// eski yerinde kalıyor ve sağ grup 580 px genişlikte okunuyordu. Genişlik 8 px adımla taranır:
    /// altı noktalı taramada <c>SpaceLg</c> payını kaldıran mutasyon yeşil kalıyordu, sık taramada tr 1184'te 13 px.
    /// </summary>
    [Theory]
    [InlineData("el")]
    [InlineData("tr")]
    [InlineData("de")]
    public void SonSekmeSagGrubaYaslanmaz(string dil)
    {
        var sonuclar = new List<(double En, double Bosluk, double EnAz, string Ayrinti)>();
        foreach (var en in Enumerable.Range(0, 54).Select(i => 1136 + i * 8.0))
            sonuclar.Add(AppHost.Run(() =>
            {
                Strings.Use(dil);
                var window = new MainWindow();
                try
                {
                    window.TabAdvanced.IsVisible = true;
                    var boyut = new Size(en, 1060);
                    Yerlestir(window, boyut);
                    Yerlestir(window, boyut);
                    var sekmeSonu = window.GetVisualDescendants().OfType<TabItem>()
                        .Where(t => t.IsVisible && t.Bounds.Width > 0 && t.FindAncestorOfType<TabControl>()?.Name == "Tabs")
                        .Max(t => Yeri(t, window)!.Value.Right);
                    var sag = window.GetVisualDescendants().OfType<StackPanel>().First(c => c.Name == "TitleBarRight");
                    var sagBasi = sag.GetVisualChildren().OfType<Control>()
                        .Where(c => c.IsVisible && c.Bounds.Width > 0)
                        .Min(c => Yeri(c, window)!.Value.Left);
                    window.TryFindResource("SpaceLg", out var enAz);
                    var katman = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "TitleBarLayer");
                    return (en, sagBasi - sekmeSonu, (double)enAz!, $"sekme sonu {sekmeSonu:0}, sag grup {sagBasi:0}, katman {katman.Bounds.Width:0}");
                }
                finally
                {
                    window.Close();
                    Strings.Use("en");
                }
            }));

        var dar = sonuclar.Where(s => s.Bosluk < s.EnAz - 0.5).ToArray();
        Assert.True(dar.Length == 0, string.Join("; ", dar.Select(s => $"{s.En:0}: {s.Bosluk:0.#} px (en az {s.EnAz:0}; {s.Ayrinti})")));
    }

    /// <summary>
    /// Son asertten sonra çağrılır: yeşil koşum kendi bıraktığını siler, kırmızı koşum
    /// kanıtını korur çünkü düşen asert buraya hiç gelmez. Klasör boşalınca o da gider.
    /// </summary>
    private static void Kapat(string klasor, params string[] adlar) => KanitKapanisi.Kapat(klasor, adlar);
}
