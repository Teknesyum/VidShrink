using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using VidShrink.App;

namespace VidShrink.Tests;

/// <summary>
/// Açılır bölümler görünür olunca yerine süzülüyor. Süre <c>MotionBase</c> belirtecinden,
/// hareketi azaltma açıkken seçici eşleşmiyor. On bir bölümün her biri sınıfı taşıyor;
/// biri düşerse o bölüm sessizce sıçrayarak açılırdı.
/// </summary>
public sealed class AcilirBolumCanlandirmaTests
{
    private static readonly string[] Bolumler =
        {
            "QualitySectionBody", "AudioBody", "FrameBody", "AdvancedBody", "RetryTrimPanel", "AboutBody",
            "HdrPolicyPanel", "PlanReasons", "AiDetails", "PerformanceDetails", "ResetSettingsConfirm"
        };

    [Fact]
    public void HerBolumCanlaniyorSureBelirtectenHareketAzaltmaKapatir()
    {
        var (siniflar, secici, sure, belirtec) = AppHost.Run(() =>
        {
            var window = new MainWindow();
            var siniflar = Bolumler.Select(ad => window.FindControl<StackPanel>(ad)!.Classes.Contains("disclose")).ToArray();
            var stil = window.Styles.OfType<Style>().Single(s => s.Animations.Count > 0 && s.Selector!.ToString().Contains(".disclose"));
            var animasyon = (Animation)stil.Animations[0];
            return (siniflar, stil.Selector!.ToString(), animasyon.Duration, (TimeSpan)window.FindResource("MotionBase")!);
        });

        Assert.All(siniflar, Assert.True);
        Assert.Contains(":not(.reduced-motion)", secici);
        Assert.Contains("[IsVisible=True]", secici);
        Assert.Equal(belirtec, sure);
    }

    private static void Dongu(Func<bool> bitti, double saniye)
    {
        var saat = Stopwatch.StartNew();
        while (!bitti() && saat.Elapsed.TotalSeconds < saniye)
        {
            using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(2));
            Dispatcher.UIThread.MainLoop(dilim.Token);
        }
    }

    /// <summary>
    /// Canlı pencerede: açılan bölüm ilk karelerde saydam başlıyor ve sonunda tam görünür,
    /// yerinde duruyor. Hareketi azaltma açıkken aynı açılış hiç saydamlaşmıyor (olumsuz kontrol).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcilanBolumSaydamdanTamaSuzuluyor(bool hareketAzaltma)
    {
        var klasor = Path.Combine(Path.GetTempPath(), $"vidshrink-acilir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(klasor);
        try
        {
            var (enAz, son, kayma) = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = Path.Combine(klasor, "settings.json"), Width = 1280, Height = 900 };
                try
                {
                    window.Show();
                    Dongu(() => window.IsLoaded, 10);
                    Dongu(() => false, 0.5);
                    window.Classes.Set("reduced-motion", hareketAzaltma);
                    var bolum = window.AdvancedBody;
                    bolum.IsVisible = true;
                    var enAz = 1.0;
                    var saat = Stopwatch.StartNew();
                    while (saat.Elapsed.TotalSeconds < 1)
                    {
                        Dongu(() => false, 0.01);
                        enAz = Math.Min(enAz, bolum.Opacity);
                    }
                    var kayma = bolum.RenderTransform is TranslateTransform t ? t.Y
                        : bolum.RenderTransform is TransformGroup g ? g.Children.OfType<TranslateTransform>().Sum(x => x.Y) : 0;
                    return (enAz, bolum.Opacity, kayma);
                }
                finally { window.Close(); }
            });

            if (hareketAzaltma) Assert.Equal(1.0, enAz);
            else Assert.True(enAz < 0.9, $"en az saydamlık {enAz}");
            Assert.Equal(1.0, son);
            Assert.Equal(0.0, kayma);
        }
        finally
        {
            try { Directory.Delete(klasor, true); } catch (IOException) { }
        }
    }
}
