using System.Globalization;
using System.Text.RegularExpressions;
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
    /// Seviye uydurulmadı: belge level 1'i de ölçtü ve ortalamada 9 hücrenin 9'unda level 3'ün
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

    /// <summary>
    /// Karar paragrafındaki sayılar tablodan yeniden sayılıyor. Önceki yazımda p10 "9/9",
    /// bayt "7/9", seviye 1'in tabana kaybı "üç" yazıyordu; tablo 8, 8 ve 5 diyordu.
    /// Özet cümle tablodan kayarsa kırmızı olur.
    /// </summary>
    [Fact]
    public void KararCumlesiTablodanSayiliyor()
    {
        var satirlar = new Dictionary<(string Kodek, string Kesit, string Kol), (long Bayt, double Ort, double P10)>();
        string? kodek = null;
        foreach (var satir in Belge.Split('\n').SkipWhile(s => !s.StartsWith("### h264_nvenc")).TakeWhile(s => !s.StartsWith("## Karar")))
        {
            var baslik = Regex.Match(satir, @"^### (\S+)");
            if (baslik.Success) { kodek = baslik.Groups[1].Value; continue; }
            var h = satir.Split('|').Select(x => x.Trim()).ToArray();
            if (kodek is null || h.Length < 9 || h[1] is not ("karanlik" or "parlak" or "hareketli")) continue;
            satirlar[(kodek, h[1], h[2])] = (
                long.Parse(h[5].Split('(')[0].Replace(" ", ""), CultureInfo.InvariantCulture),
                double.Parse(h[6], CultureInfo.InvariantCulture),
                double.Parse(h[7], CultureInfo.InvariantCulture));
        }

        var hucreler = satirlar.Keys.Select(k => (k.Kodek, k.Kesit)).Distinct().ToArray();
        Assert.Equal(9, hucreler.Length);
        var taban = hucreler.Select(c => satirlar[(c.Kodek, c.Kesit, "taban")]).ToArray();
        var bir = hucreler.Select(c => satirlar[(c.Kodek, c.Kesit, "`-lookahead_level 1`")]).ToArray();
        var uc = hucreler.Select(c => satirlar[(c.Kodek, c.Kesit, "`-lookahead_level 3`")]).ToArray();
        int Say(Func<int, bool> kosul) => Enumerable.Range(0, 9).Count(kosul);

        var karar = Regex.Replace(Belge[Belge.IndexOf("## Karar")..], @"\s+", " ");
        Assert.Contains($"ortalamada {Say(i => uc[i].Ort > taban[i].Ort)}/9, p10'da {Say(i => uc[i].P10 > taban[i].P10)}/9 hücrede geçiyor", karar);
        Assert.Contains($"ve {Say(i => uc[i].Bayt < taban[i].Bayt)}/9 hücrede **daha az baytla**", karar);
        Assert.Contains($"ortalamada {Say(i => bir[i].Ort < uc[i].Ort)}/9 hücrede seviye 3'ün altında", karar);
        Assert.Contains($"altında ortalamada {Say(i => bir[i].Ort < taban[i].Ort)}/9, p10'da {Say(i => bir[i].P10 < taban[i].P10)}/9 hücrede", karar);
    }
}
