using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// C1-1: kalite doyduğu için hedefin altında teslim edilen dosya kullanıcıya bir cümleyle
/// anlatılıyor — ana pencerede ve iş penceresinde. Doymamış teslim, hedefi aşan ve kırpılan
/// teslim bu cümleyi almıyor (olumsuz kontroller); onlar kendi cümlelerini taşıyor.
/// </summary>
public sealed class DoygunTeslimTests
{
    private const string Yol = @"C:\ornek\video_shrunk.mp4";

    private static EncodeResult Sonuc(bool saturated = false, bool overTarget = false, TrimPlan? trim = null, bool success = true) =>
        new(success, Yol, 6.4, null!, 2, null, UnderBand: saturated, OverTarget: overTarget, Trim: trim, Saturated: saturated);

    [Fact]
    public void DoygunTeslimAnaPenceredeCumleAliyor()
    {
        var ek = AppHost.Run(() => MainWindow.SaturatedSuffix(Sonuc(saturated: true), 25));

        Assert.StartsWith(" ", ek, StringComparison.Ordinal);
        Assert.Contains(6.4.ToString("0.0", Strings.Culture), ek, StringComparison.Ordinal);
        Assert.Contains(25.0.ToString("0.##", Strings.Culture), ek, StringComparison.Ordinal);
        Assert.DoesNotContain("{", ek, StringComparison.Ordinal);
    }

    [Fact]
    public void DoymamisAsanVeKirpilanTeslimCumleAlmiyor()
    {
        var kirpma = new TrimPlan(TrimSide.End, 0, 50, 60, 1);
        var ekler = AppHost.Run(() => new[]
        {
            MainWindow.SaturatedSuffix(Sonuc(), 25),
            MainWindow.SaturatedSuffix(Sonuc(saturated: true, overTarget: true), 25),
            MainWindow.SaturatedSuffix(Sonuc(saturated: true, trim: kirpma), 25),
            MainWindow.SaturatedSuffix(Sonuc(saturated: true, success: false), 25),
        });

        Assert.All(ekler, ek => Assert.Equal("", ek));
    }

    [Fact]
    public void IsPenceresiDoygunSatiriPencereninDiliyle()
    {
        var (doygun, duz, mb, hedef) = AppHost.Run(() =>
        {
            var window = new ShrinkJobWindow(new ShellShrinkStartup(null, ShrinkArgumentProblem.NoTarget, null), null);
            try
            {
                var kultur = Strings.CultureOf(window.Language);
                return (window.BittiSatiri(Sonuc(saturated: true), 25, new EncodePlan()), window.BittiSatiri(Sonuc(), 25, new EncodePlan()),
                    Bicim.Boyut.Mb(6.4, kultur), Bicim.Boyut.Hedef(25, kultur));
            }
            finally { window.Close(); }
        });

        Assert.StartsWith(Yol + " ", doygun, StringComparison.Ordinal);
        Assert.Contains(mb, doygun, StringComparison.Ordinal);
        Assert.Contains(hedef, doygun, StringComparison.Ordinal);
        Assert.Equal(Yol, duz);
    }

    [Fact]
    public void DoygunAnahtariButunDillerde()
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            Assert.True(values.TryGetValue("main.run.saturated", out var metin), $"{language} dilinde anahtar yok.");
            Assert.Contains("{0}", metin, StringComparison.Ordinal);
            Assert.Contains("{1}", metin, StringComparison.Ordinal);
        }
    }
}
