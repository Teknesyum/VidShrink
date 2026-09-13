using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kaydedicinin hedef boyut bütçesi. Ölçülen şey hesabın kendisi: hedef verilmediğinde
/// kalite kolunun korunması, verildiğinde OBS'in katsayısıyla çıkan bit hızı ve tabanın
/// altına düşen hedefin sessizce uygulanmaması.
///
/// <para>Kayıt gerçek zamanlı olduğu için iki geçiş yok; bütçe tavanlı bit hızına
/// çevriliyor ve <see cref="RecorderArguments.Validate"/>'ten geçmek zorunda.</para>
/// </summary>
public class KayitButceTests
{
    private static RecorderRequest Temel() => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Screen
    };

    /// <summary>Hedef verilmemişse bütçe istenmemiştir; istek olduğu gibi kalır.</summary>
    [Fact]
    public void HedefYokkenButceIstenmemis()
    {
        Assert.Equal(RecorderBudgetVerdict.NotRequested, RecorderBudget.From(null, null, 1).Verdict);
        Assert.Equal(RecorderBudgetVerdict.NotRequested, RecorderBudget.From(10, null, 1).Verdict);
        Assert.Equal(RecorderBudgetVerdict.NotRequested, RecorderBudget.From(null, 30, 1).Verdict);

        var temel = Temel() with { RateControl = RecorderRateControl.Quality, Quality = 23 };
        var sonuc = RecorderAutoPlan.ApplyBudget(temel, RecorderBudget.From(null, null, 1));

        Assert.Equal(RecorderRateControl.Quality, sonuc.RateControl);
        Assert.Null(sonuc.BitrateKbps);
    }

    /// <summary>
    /// 10 MB / 30 sn, tek ses izi. Katsayı OBS'in tampon hesabının yazımı:
    /// 10 × 8 × 1024 × 1024 / 1000 / 30 = 2796,2 kbit/sn toplam, eksi 160 ses = 2636 video.
    /// Yaygın <c>×8192</c> yazımı 2730 verirdi; aradaki %2,4 bilerek OBS'in tarafında.
    /// </summary>
    [Fact]
    public void OnMegabaytOtuzSaniye()
    {
        var butce = RecorderBudget.From(10, 30, 1);

        Assert.Equal(RecorderBudgetVerdict.Usable, butce.Verdict);
        Assert.Equal(2636, butce.VideoKbps);
        Assert.Equal(2796, RecorderBudget.From(10, 30, 0).VideoKbps);
        Assert.Equal(2476, RecorderBudget.From(10, 30, 2).VideoKbps);
    }

    /// <summary>Sıfır, negatif ve sayı olmayan hedefler hesaba girmiyor.</summary>
    [Fact]
    public void BozukHedefHesaplanmiyor()
    {
        Assert.Equal(RecorderBudgetVerdict.Invalid, RecorderBudget.From(0, 30, 1).Verdict);
        Assert.Equal(RecorderBudgetVerdict.Invalid, RecorderBudget.From(10, 0, 1).Verdict);
        Assert.Equal(RecorderBudgetVerdict.Invalid, RecorderBudget.From(-10, 30, 1).Verdict);
        Assert.Equal(RecorderBudgetVerdict.Invalid, RecorderBudget.From(10, -30, 1).Verdict);
        Assert.Equal(RecorderBudgetVerdict.Invalid, RecorderBudget.From(double.NaN, 30, 1).Verdict);
        Assert.Equal(RecorderBudgetVerdict.Invalid, RecorderBudget.From(double.PositiveInfinity, 30, 1).Verdict);
        Assert.Equal(RecorderBudgetVerdict.Invalid, RecorderBudget.From(10, 30, -1).Verdict);
    }

    /// <summary>
    /// Tabanın altına düşen hedef uygulanmıyor: bozuk bir kayıt sessizce üretilmektense
    /// hüküm <see cref="RecorderBudgetVerdict.TooSmall"/> dönüyor ve arayüz bunu söylüyor.
    /// </summary>
    [Fact]
    public void TabanAltiHedefUygulanmiyor()
    {
        var butce = RecorderBudget.From(1, 60, 1);

        Assert.Equal(RecorderBudgetVerdict.TooSmall, butce.Verdict);
        Assert.Equal(0, butce.VideoKbps);

        var temel = Temel() with { RateControl = RecorderRateControl.Quality, Quality = 23 };
        Assert.Equal(RecorderRateControl.Quality, RecorderAutoPlan.ApplyBudget(temel, butce).RateControl);
    }

    /// <summary>Taban sınırının iki yakası: bir altı elenirken bir üstü hesaba giriyor.</summary>
    [Fact]
    public void TabanSinirininIkiYakasi()
    {
        Assert.Equal(195, (int)(0.7 * 8 * 1024 * 1024 / 1000 / 30));
        Assert.Equal(RecorderBudgetVerdict.TooSmall, RecorderBudget.From(0.7, 30, 0).Verdict);

        Assert.Equal(223, (int)(0.8 * 8 * 1024 * 1024 / 1000 / 30));
        Assert.Equal(RecorderBudgetVerdict.Usable, RecorderBudget.From(0.8, 30, 0).Verdict);
        Assert.Equal(223, RecorderBudget.From(0.8, 30, 0).VideoKbps);
    }

    /// <summary>
    /// Otomatik kip kalite kolunu yazıyor, bütçe onu bit hızı koluna çeviriyor: tavan bit
    /// hızına eşit, tampon iki katı, ve sonuç doğrulamadan geçiyor.
    /// </summary>
    [Fact]
    public void ButceKaliteKolunuBitHiziKolunaCeviriyor()
    {
        var aday = RecorderAutoPlan.Candidates(new RecorderMachine(1920, 1080, 60, 8, new[] { "h264_nvenc" }))[0];
        var otomatik = RecorderAutoPlan.Apply(Temel(), aday);
        Assert.Equal(RecorderRateControl.Quality, otomatik.RateControl);

        var butce = RecorderBudget.From(10, 30, 1);
        var sonuc = RecorderAutoPlan.ApplyBudget(otomatik, butce);

        Assert.Equal(RecorderRateControl.Bitrate, sonuc.RateControl);
        Assert.Equal(butce.VideoKbps, sonuc.BitrateKbps);
        Assert.Equal(butce.VideoKbps, sonuc.MaxBitrateKbps);
        Assert.Equal(butce.VideoKbps * 2, sonuc.BufferKbits);

        var errors = RecorderArguments.Validate(sonuc, "kayit." + RecorderArguments.Extension(sonuc.Container));
        Assert.True(errors.Count == 0, string.Join(" ", errors));
    }

    /// <summary>Bütçe kodlayıcıyı, kare hızını ve bölgeyi değiştirmiyor.</summary>
    [Fact]
    public void ButceKodlamaKolununGerisineDokunmuyor()
    {
        var temel = Temel() with
        {
            Target = RecorderTargetKind.Region,
            Region = new RecorderRegion(10, 20, 640, 480),
            VideoCodec = "h264_nvenc",
            Preset = "p4",
            Fps = 60,
            ShowCursor = false
        };

        var sonuc = RecorderAutoPlan.ApplyBudget(temel, RecorderBudget.From(10, 30, 1));

        Assert.Equal("h264_nvenc", sonuc.VideoCodec);
        Assert.Equal("p4", sonuc.Preset);
        Assert.Equal(60, sonuc.Fps);
        Assert.Equal(temel.Region, sonuc.Region);
        Assert.False(sonuc.ShowCursor);
    }

    /// <summary>Ses bit hızı sabiti kaydedicinin gerçekten yazdığı sayıyla aynı.</summary>
    [Fact]
    public void SesSabitiArgumanlaAyni()
        => Assert.Equal(RecorderArguments.AudioBitrate, RecorderBudget.AudioKbps.ToString() + "k");
}
