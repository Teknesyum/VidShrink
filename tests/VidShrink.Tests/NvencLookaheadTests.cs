using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// NVENC lookahead kolu (<c>docs/olcumler/nvenc-kalite-kollari.md</c>, danışma
/// <c>docs/danisma/2026-09-19-fable-nvenc-kalite-kollari.md</c>).
///
/// <para>Sayılar burada elle yazılı değil: kazanan kol ve düşen kollar ölçüm belgesinden
/// okunup kodun ürettiği argümanla karşılaştırılıyor. Belge ile kod ayrışırsa kırmızı olur.</para>
///
/// <para>Kapı <c>-lookahead_level</c>: <c>-rc-lookahead</c> nvenc'te on yıldır var, seviye
/// bayrağı yeni ffmpeg ve yeni sürücü istiyor. Listelenen bayrak kabul edilen bayrak
/// değil — aynı turda <c>-tf_level</c> hevc'te listelendiği halde sürücüden red aldı.</para>
/// </summary>
public sealed class NvencLookaheadTests
{
    private sealed class Kabiliyet(params (string Codec, string Option)[] destekli)
        : IEncoderAvailability, IEncoderOptionAvailability
    {
        public bool HasEncoder(string name) => true;
        public bool WorksAsEncoder(string codec) => true;
        public bool SupportsEncoderOption(string codec, string option, string value)
            => destekli.Contains((codec, option));
    }

    private static string Belge => File.ReadAllText(
        Path.Combine(TipSources.Root, "docs", "olcumler", "nvenc-kalite-kollari.md"));

    private static IReadOnlyList<string> Kol(string codec, bool destekli) =>
        FfmpegArguments.CachedPsychovisualArgs(codec,
            destekli ? new Kabiliyet((codec, "-lookahead_level")) : new Kabiliyet());

    /// <summary>Üç NVENC kodeğinde de kol açılıyor ve ölçülen çifti aynen yazıyor.</summary>
    [Theory]
    [InlineData("h264_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("av1_nvenc")]
    public void UcNvencKodegindeDeLookaheadYaziliyor(string codec)
        => Assert.Equal(new[] { "-rc-lookahead", "20", "-lookahead_level", "3" }, Kol(codec, destekli: true));

    /// <summary>
    /// Olumsuz kontrol: seçenek kabul edilmiyorsa hiçbir şey yazılmıyor. Bu satır olmadan
    /// yukarıdaki ölçü "her zaman yazıyor" diye de yeşil kalırdı.
    /// </summary>
    [Theory]
    [InlineData("h264_nvenc")]
    [InlineData("hevc_nvenc")]
    [InlineData("av1_nvenc")]
    public void KabulEdilmeyenSecenekteKolSessizceDusuyor(string codec)
        => Assert.Empty(Kol(codec, destekli: false));

    /// <summary>
    /// İkinci olumsuz kontrol: kol NVENC'e özel. QSV, AMF ve yazılım kodlayıcıları
    /// seçenek "destekli" görünse bile lookahead almıyor — ölçüm yalnız NVENC'te yapıldı.
    /// </summary>
    [Theory]
    [InlineData("h264_qsv")]
    [InlineData("hevc_amf")]
    [InlineData("libx264")]
    public void NvencDisindaLookaheadYazilmiyor(string codec)
        => Assert.DoesNotContain("-lookahead_level", Kol(codec, destekli: true));

    /// <summary>
    /// Seviye uydurulmadı: belge level 1'i de ölçtü ve 9 hücrenin 9'unda level 3'ün
    /// altında kaldı. Kod ölçülen seviyeyi yazıyor.
    /// </summary>
    [Fact]
    public void YazilanSeviyeOlcumBelgesindekiKazanan()
    {
        var kol = Kol("hevc_nvenc", destekli: true);
        var seviye = kol[kol.ToList().IndexOf("-lookahead_level") + 1];

        Assert.Contains($"`-rc-lookahead 20 -lookahead_level {seviye}` **alındı**", Belge);
        Assert.DoesNotContain($"`-rc-lookahead 20 -lookahead_level {seviye}` **alınmadı**", Belge);
    }

    /// <summary>
    /// Ölçümde kaybeden iki kol üründe yok: <c>-spatial-aq</c> 6/6 kaybetti,
    /// <c>-tf_level</c> hevc'te sürücüden red aldı.
    /// </summary>
    [Theory]
    [InlineData("-spatial-aq")]
    [InlineData("-aq-strength")]
    [InlineData("-tf_level")]
    [InlineData("-highbitdepth")]
    public void OlcumdeDusenKollarUrunYolunaGirmiyor(string bayrak)
    {
        foreach (var codec in new[] { "h264_nvenc", "hevc_nvenc", "av1_nvenc" })
            Assert.DoesNotContain(bayrak, Kol(codec, destekli: true));
    }

    /// <summary>
    /// Kol tam kodlamanın argümanına gerçekten giriyor — <c>Psychovisual</c>'ı doldurmak
    /// tek başına yetmez, <c>Build</c> onu taşımak zorunda. Parça kodlaması da aynı çifti
    /// taşıyor; ayrışırsa ölçüm parçası tam kodlamadan farklı kalite alırdı.
    /// </summary>
    [Fact]
    public void KolTamKodlamaninVeParcaninArgumanlarindaDuruyor()
    {
        var info = new MediaInfo
        {
            FilePath = "kaynak.mp4",
            FileSizeBytes = 40_000_000,
            DurationSeconds = 60,
            Width = 1920,
            Height = 1080,
            Fps = 60,
            VideoCodec = "h264",
            TotalBitrateBps = 5_700_000
        };
        var plan = new EncodePlan
        {
            Codec = "hevc_nvenc",
            Mode = "2pass",
            VideoBitrateK = 1200,
            Width = 1280,
            Height = 720,
            Fps = 30,
            Preset = "p4",
            PixelFormat = "yuv420p",
            AudioCodec = "aac",
            AudioBitrateK = 128
        };
        var kabiliyet = new Kabiliyet(("hevc_nvenc", "-lookahead_level"));

        var tam = FfmpegArguments.Build(info, plan, "out.mp4", 0, null, kabiliyet);
        var parca = FfmpegArguments.BuildSegment(info, plan, 1, 2, "part.mp4", kabiliyet);
        var kapali = FfmpegArguments.Build(info, plan, "out.mp4", 0, null, new Kabiliyet());

        Assert.Equal("20", tam[tam.ToList().IndexOf("-rc-lookahead") + 1]);
        Assert.Equal("3", tam[tam.ToList().IndexOf("-lookahead_level") + 1]);
        Assert.Contains("-lookahead_level", parca);
        Assert.DoesNotContain("-lookahead_level", kapali);
    }
}
