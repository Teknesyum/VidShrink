using VidShrink.App;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// C1-5: Gelişmiş'teki süzgeç paneli. Metin kutusu tek kaynaktır; açılır kutular ve onay
/// kutuları onu yazar, metin geçerliyse denetimler ondan okunur. Önceden kutu hiç
/// izlenmiyordu ve yazılan belirtim plana ancak başka bir seçenek değişince giriyordu.
/// </summary>
public sealed class SuzgecPaneliTests
{
    public static TheoryData<string> Secenekler() => new()
    {
        "deinterlace=off", "deinterlace=on", "detelecine", "denoise=nlmeans:light", "denoise=hqdn3d:strong",
        "sharpen=light", "sharpen=medium", "sharpen=strong", "deblock", "deband", "gray",
        "rotate=clock", "rotate=cclock", "rotate=180", "rotate=hflip", "rotate=vflip",
        "pad=10:20:30:40", "colorspace=bt709", "colorspace=bt601", "crop=640:360:0:60",
        "deinterlace=on, detelecine, denoise=nlmeans:medium, sharpen=strong, deblock, deband, gray, rotate=180, pad=0:0:8:8, colorspace=bt601, crop=1280:536:0:92"
    };

    /// <summary>Her seçenek belirtime yazılıp aynı seçeneğe döner, ve yazım kendi kendine eşittir.</summary>
    [Theory]
    [MemberData(nameof(Secenekler))]
    public void BelirtimGidisDonusu(string belirtim)
    {
        var secenek = VideoFilterChain.Parse(belirtim);
        Assert.NotEqual(VideoFilterOptions.Default, secenek);
        Assert.Equal(belirtim, VideoFilterChain.Format(secenek));
        Assert.Equal(secenek, VideoFilterChain.Parse(VideoFilterChain.Format(secenek)));
    }

    /// <summary>Olumsuz kontrol: varsayılan boş metne yazılır; güç yalnız gürültüyle birlikte yazılır.</summary>
    [Fact]
    public void VarsayilanBosYazilir()
    {
        Assert.Equal("", VideoFilterChain.Format(VideoFilterOptions.Default));
        Assert.Equal("", VideoFilterChain.Format(VideoFilterOptions.Default with { DenoiseStrength = FilterStrength.Strong }));
    }

    private static T Pencerede<T>(Func<MainWindow, T> is_)
        => AppHost.Run(() =>
        {
            var window = new MainWindow();
            try { return is_(window); }
            finally { window.Close(); }
        });

    /// <summary>Denetimler metni yazar, metin plana geçer; boş panel boş metin ve varsayılan süzgeç.</summary>
    [Fact]
    public void DenetimMetniVePlaniYazar()
    {
        var (bos, bosSuzgec, gucAcikBasta, metin, suzgec, gucAcik) = Pencerede(window =>
        {
            var ilk = (window.TxtAdvFilters.Text ?? "", window.PlanOptionsForTest().Filters, window.CmbFltDenoiseStrength.IsEnabled);
            window.CmbFltDenoise.SelectedIndex = 2;
            window.CmbFltDenoiseStrength.SelectedIndex = 2;
            window.CmbFltRotate.SelectedIndex = 1;
            window.ChkFltGray.IsChecked = true;
            return (ilk.Item1, ilk.Filters, ilk.IsEnabled, window.TxtAdvFilters.Text, window.PlanOptionsForTest().Filters, window.CmbFltDenoiseStrength.IsEnabled);
        });

        Assert.Equal("", bos);
        Assert.Equal(VideoFilterOptions.Default, bosSuzgec);
        Assert.False(gucAcikBasta);
        Assert.Equal("denoise=hqdn3d:strong, gray, rotate=clock", metin);
        Assert.Equal(VideoFilterOptions.Default with
        {
            Denoise = DenoiseFilter.Hqdn3d,
            DenoiseStrength = FilterStrength.Strong,
            Grayscale = true,
            Transpose = TransposeMode.Clockwise
        }, suzgec);
        Assert.True(gucAcik);
    }

    /// <summary>Yazılan metin denetimleri eşitler.</summary>
    [Fact]
    public void MetinDenetimleriEsitler()
    {
        var (keskin, bant, renk, taramasiz, gri) = Pencerede(window =>
        {
            window.TxtAdvFilters.Text = "sharpen=light, deband, colorspace=bt601, deinterlace=on";
            return (window.CmbFltSharpen.SelectedIndex, window.ChkFltDeband.IsChecked, window.CmbFltColor.SelectedIndex,
                window.CmbFltDeinterlace.SelectedIndex, window.ChkFltGray.IsChecked);
        });

        Assert.Equal((int)SharpenMode.Light, keskin);
        Assert.True(bant);
        Assert.Equal((int)ColorMatrixTarget.Bt601, renk);
        Assert.Equal((int)DeinterlaceMode.On, taramasiz);
        Assert.False(gri);
    }

    /// <summary>Bozuk metin denetimleri sıfırlamaz, plan varsayılana düşer ve satır hatayı söyler.</summary>
    [Fact]
    public void BozukMetinDenetimleriBozmaz()
    {
        var (keskin, suzgec, satir) = Pencerede(window =>
        {
            window.TxtAdvFilters.Text = "sharpen=medium";
            window.TxtAdvFilters.Text = "sharpen=medium, uydurma";
            return (window.CmbFltSharpen.SelectedIndex, window.PlanOptionsForTest().Filters, window.TxtAdvFiltersNow.Text);
        });

        Assert.Equal((int)SharpenMode.Medium, keskin);
        Assert.Equal(VideoFilterOptions.Default, suzgec);
        Assert.False(string.IsNullOrEmpty(satir));
    }

    /// <summary>Panelde karşılığı olmayan kırpma ve kenar, denetim değişince metinde kalır.</summary>
    [Fact]
    public void KirpmaDenetimDegisinceKorunur()
    {
        var metin = Pencerede(window =>
        {
            window.TxtAdvFilters.Text = "crop=640:360:0:60, pad=0:0:8:8";
            window.ChkFltDeblock.IsChecked = true;
            return window.TxtAdvFilters.Text;
        });

        Assert.Equal("deblock, pad=0:0:8:8, crop=640:360:0:60", metin);
    }

    /// <summary>Yirmi iki anahtar 42 dilde dolu.</summary>
    [Fact]
    public void AnahtarlarButunDillerde()
    {
        var anahtarlar = new[]
        {
            "deinterlace", "denoise", "strength", "sharpen", "rotate", "color", "off", "on", "light", "medium", "strong",
            "rotate.clock", "rotate.cclock", "rotate.180", "rotate.hflip", "rotate.vflip", "color.keep",
            "detelecine", "deblock", "deband", "gray", "spec"
        }.Select(k => "main.advanced.filters." + k).ToArray();
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            foreach (var key in anahtarlar)
                Assert.True(values.TryGetValue(key, out var metin) && metin.Length > 0, $"{language}: {key}");
        }
    }
}
