using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;
using Xunit.Abstractions;

namespace VidShrink.Tests;

public sealed class DuzenleyiciGecisTests
{
    private static readonly string Kanit = Path.Combine(TipSources.Root, ".calisma", "duzenleyici-gecis");
    private readonly ITestOutputHelper _output;

    public DuzenleyiciGecisTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void DuzenleyiciyeAcilincaKucultSayfasiAltindaKalmaz()
    {
        Directory.CreateDirectory(Kanit);
        var klip = Path.Combine(Kanit, "sahte.mp4");
        File.WriteAllBytes(klip, new byte[16]);
        try
        {
            var s = AppHost.Run(() =>
            {
                Strings.Use("tr");
                var pencere = new MainWindow { SettingsPathOverride = Path.Combine(Kanit, "settings.json"), Width = 1280, Height = 800, WindowState = WindowState.Normal };
                pencere.Classes.Remove("reduced-motion");
                var oynatici = pencere.PlayerTab;
                oynatici.EngineFactory = () => new YolMotoru();
                var duzenleyici = pencere.EditorPaneForTest;
                duzenleyici.Player.EngineFactory = () => new YolMotoru();
                pencere.Show();
                try
                {
                    pencere.Tabs.SelectedIndex = pencere.ShrinkTabIndex;
                    Dilimle(TimeSpan.FromMilliseconds(400));
                    var ev = pencere.GetVisualDescendants().OfType<TransitioningContentControl>().First(d => d.Name == "SelectedContentHost");
                    List<ContentPresenter> Gorunenler() => ev.GetVisualDescendants().OfType<ContentPresenter>()
                        .Where(p => p.TemplatedParent == ev && p.IsVisible && p.Content is not null && p.Opacity > 0)
                        .ToList();
                    var kucult = pencere.Tabs.SelectedContent;

                    var ac = pencere.OpenInEditorAsync(klip);
                    var enCok = 0;
                    var saat = Stopwatch.StartNew();
                    while (saat.Elapsed.TotalMilliseconds < 5000 && !ac.IsCompleted)
                    {
                        Dilimle(TimeSpan.FromMilliseconds(2));
                        enCok = Math.Max(enCok, Gorunenler().Count);
                    }
                    var bitti = Stopwatch.StartNew();
                    while (bitti.Elapsed.TotalMilliseconds < 400)
                    {
                        Dilimle(TimeSpan.FromMilliseconds(2));
                        enCok = Math.Max(enCok, Gorunenler().Count);
                    }
                    var son = Gorunenler();
                    return (
                        Tamam: ac.IsCompleted,
                        Sekme: pencere.Tabs.SelectedIndex,
                        Hedef: pencere.EditorTabIndex,
                        EnCok: enCok,
                        Son: son.Select(p => p.Content?.GetType().Name + " opak " + p.Opacity.ToString("0.##")).ToList(),
                        KucultGorunur: son.Any(p => ReferenceEquals(p.Content, kucult)),
                        DuzenleyiciGorunur: son.Any(p => ReferenceEquals(p.Content, pencere.Tabs.SelectedContent)));
                }
                finally
                {
                    duzenleyici.Player.Close();
                    oynatici.Close();
                    pencere.Close();
                    Strings.Use("en");
                }
            });

            _output.WriteLine($"en cok {s.EnCok} sunucu; son: {string.Join(", ", s.Son)}");
            Assert.True(s.Tamam);
            Assert.Equal(s.Hedef, s.Sekme);
            Assert.False(s.KucultGorunur, "Küçült sayfası düzenleyicinin altında görünür kaldı: " + string.Join(", ", s.Son));
            Assert.True(s.DuzenleyiciGorunur);
            Assert.Single(s.Son);
        }
        finally
        {
            Directory.Delete(Kanit, true);
        }
    }

    private static void Dilimle(TimeSpan sure)
    {
        var saat = Stopwatch.StartNew();
        while (saat.Elapsed < sure)
        {
            using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(2));
            Dispatcher.UIThread.MainLoop(dilim.Token);
        }
    }
}
