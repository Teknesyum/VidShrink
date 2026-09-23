using System.Diagnostics;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// "Kullanıcıya yalan yok" denetiminin iş penceresi bulguları: hedefi aşan dosya sorusuz
/// teslim edilmez (Y3), aşama satırı ve hata satırındaki hedef pencerenin dilinde yazılır (D4),
/// aşım farkı motorun karşılaştırdığı etkin hedefe göre hesaplanır (D3).
/// </summary>
public sealed class IsPenceresiDurustlukTests
{
    private static RetryPrompt Soru(int deneme, int hak, bool altta = false)
        => new(deneme, hak, TargetMb: 10, ActualMb: 12.3, TimeSpan.FromSeconds(42), altta, altta ? 9.1 : 0);

    private static EncodePlan KirpilmisPlan(double etkinMb) => new() { EffectiveTargetMb = etkinMb };

    [Fact]
    public void SorusuzCevapYalnizHedefiAsanTeslimdeYok()
    {
        Assert.Equal(OvershootChoice.Retry, ShrinkJobWindow.UnaskedChoice(Soru(1, 3)));
        Assert.Equal(OvershootChoice.Retry, ShrinkJobWindow.UnaskedChoice(Soru(2, 3, altta: true)));
        Assert.Equal(OvershootChoice.Leave, ShrinkJobWindow.UnaskedChoice(Soru(3, 3, altta: true)));
        Assert.Null(ShrinkJobWindow.UnaskedChoice(Soru(3, 3)));
    }

    [Fact]
    public void IsPenceresiHedefiAsanDosyayiSormadanVermez()
    {
        var (sorulduMu, bekliyorMu, cevap) = AppHost.Run(() =>
        {
            var window = new ShrinkJobWindow(new ShellShrinkStartup(null, ShrinkArgumentProblem.NoTarget, null), null);
            try
            {
                var gorev = window.AskOvershootAsync(Soru(3, 3), CancellationToken.None);
                Dispatcher.UIThread.RunJobs();
                var soruldu = window.OvershootAsked;
                var bekliyor = !gorev.IsCompleted;

                window.AnswerOvershoot(accept: false);
                var saat = Stopwatch.StartNew();
                while (!gorev.IsCompleted && saat.Elapsed < TimeSpan.FromSeconds(5)) Dispatcher.UIThread.RunJobs();
                return (soruldu, bekliyor, gorev.IsCompletedSuccessfully ? gorev.Result : (OvershootChoice?)null);
            }
            finally { window.Close(); }
        });

        Assert.True(sorulduMu, "Son denemede hedefin üzerinde kalan dosya için soru paneli açılmadı.");
        Assert.True(bekliyorMu, "Motor kullanıcı cevap vermeden devam etti.");
        Assert.Equal(OvershootChoice.Leave, cevap);
    }

    [Fact]
    public void AsamaSatiriPencereninDilindeYazilir()
    {
        const string asama = "pass 2/2 (attempt 1)";
        var (yazilan, beklenen) = AppHost.Run(() =>
        {
            var window = new ShrinkJobWindow(new ShellShrinkStartup(null, ShrinkArgumentProblem.NoTarget, null), null);
            try
            {
                window.ShowProgress(new EncodeProgress(0.6, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2), 1.0, asama));
                return (window.StageText, MainWindow.LocalizeStageIn(asama, window.Language));
            }
            finally { window.Close(); }
        });

        Assert.Equal(beklenen, yazilan);
        Assert.NotEqual(asama, yazilan);
        Assert.DoesNotContain("attempt", MainWindow.LocalizeStageIn(asama, "tr"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HataSatirindaHedefAileninYazimiyla()
    {
        var (satir, hedef) = AppHost.Run(() =>
        {
            var window = new ShrinkJobWindow(new ShellShrinkStartup(null, ShrinkArgumentProblem.NoTarget, null), null);
            try
            {
                var tavan = new EncodeResult(false, @"C:\ornek\video_shrunk.mp4", 9.0, null!, 3, null, CeilingExceeded: true);
                return (window.HataSatiri(tavan, 16, KirpilmisPlan(7.8125)), Bicim.Boyut.Hedef(7.8125, Strings.CultureOf(window.Language)));
            }
            finally { window.Close(); }
        });

        Assert.Contains(hedef, satir, StringComparison.Ordinal);
        Assert.DoesNotContain("8125", satir, StringComparison.Ordinal);
    }

    [Fact]
    public void AsimFarkiEtkinHedefeGore()
    {
        Assert.Equal(10, EncodeRunner.EffectiveTargetMb(16, KirpilmisPlan(10)));
        Assert.Equal(16, EncodeRunner.EffectiveTargetMb(16, new EncodePlan()));

        var (isSatiri, fark, eksi) = AppHost.Run(() =>
        {
            var window = new ShrinkJobWindow(new ShellShrinkStartup(null, ShrinkArgumentProblem.NoTarget, null), null);
            try
            {
                var kultur = Strings.CultureOf(window.Language);
                var asan = new EncodeResult(true, @"C:\ornek\video_shrunk.mp4", 12.0, null!, 1, null, OverTarget: true);
                return (window.BittiSatiri(asan, 16, KirpilmisPlan(10)), Bicim.Boyut.Sapma(2.0, kultur), Bicim.Boyut.Sapma(-4.0, kultur));
            }
            finally { window.Close(); }
        });

        Assert.Contains(fark, isSatiri, StringComparison.Ordinal);
        Assert.DoesNotContain(eksi, isSatiri, StringComparison.Ordinal);

        var anaSatir = MainWindow.AcceptedLargerText(12.0, 16, KirpilmisPlan(10));
        Assert.Contains(2.0.ToString("0.00", Strings.Culture), anaSatir, StringComparison.Ordinal);
        Assert.DoesNotContain((-4.0).ToString("0.00", Strings.Culture), anaSatir, StringComparison.Ordinal);
    }
}
