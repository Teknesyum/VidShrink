using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using VidShrink.App;
using Xunit;

namespace VidShrink.Tests;

public sealed class ZeminTasimaTests
{
    [Fact]
    public void DuzZeminTasinirDenetimlerTasinmaz()
    {
        var klasor = Path.Combine(Path.GetTempPath(), $"vidshrink-zemin-{Guid.NewGuid():N}");
        Directory.CreateDirectory(klasor);
        try
        {
            var sonuc = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = Path.Combine(klasor, "settings.json"), Width = 1280, Height = 800, WindowState = WindowState.Normal };
                try
                {
                    window.Show();
                    DenetimSurucu.Wait(window.Player, 0.3);

                    var duzBorder = window.TargetPanel;
                    var duzPanel = window.PlanBody;
                    var dugme = window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && !b.GetVisualAncestors().Contains(window.TitleBar));
                    var dugmeIci = dugme.GetVisualDescendants().OfType<Visual>().First();
                    var metinKutusu = window.GetVisualDescendants().OfType<TextBox>().First();
                    var metinKutusuIci = metinKutusu.GetVisualDescendants().OfType<Visual>().First();

                    var olculer = new List<(string ad, Visual? kaynak, bool beklenen)>
                    {
                        ("duz Border", duzBorder, true),
                        ("duz Panel", duzPanel, true),
                        ("Button", dugme, false),
                        ("Button ici", dugmeIci, false),
                        ("TextBox", metinKutusu, false),
                        ("TextBox ici", metinKutusuIci, false),
                        ("PlayerView", window.Player, false),
                        ("TitleBar", window.TitleBar, false),
                        ("kaynak yok", null, false)
                    };

                    var satirlar = new List<string>();
                    foreach (var (ad, kaynak, beklenen) in olculer)
                    {
                        var olcu = window.ZemindenTasinir(kaynak);
                        if (olcu != beklenen) satirlar.Add($"{ad} ({kaynak?.GetType().Name}): {olcu}, beklenen {beklenen}");
                    }

                    window.WindowState = WindowState.FullScreen;
                    DenetimSurucu.Wait(window.Player, 0.2);
                    if (window.ZemindenTasinir(duzBorder)) satirlar.Add("tam ekranda duz zemin tasinir dondu");
                    return satirlar;
                }
                finally
                {
                    window.Close();
                }
            });

            Assert.True(sonuc.Count == 0, string.Join(Environment.NewLine, sonuc));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }
}
