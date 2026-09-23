using System.Text.RegularExpressions;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// B1a'nin ikinci yarisi: flac kucultmede degil donusturucude. Kayipsiz ses hedef boyutu
/// anlamsiz kilar, o yuzden kucultme kolu degismedi; burada olculen sey donusturucunun
/// hangi kapa flac yazmaya izin verdigi.
///
/// <para>Izin listesi uydurulmadi. <c>docs/olcumler/b1a-flac-kaplari.md</c> her kabi gercek
/// ffmpeg ile denedi ve kabul edilen dosyayi ffprobe ile geri okudu; wav ile avi "cikis kodu 0"
/// verdigi halde kap flac'i tasimadigi icin listeye alinmadi. Olcu o belgeyi okur: belge ile
/// kod ayrisirsa kirmizi olur.</para>
/// </summary>
public sealed class DonusturucuFlacTests
{
    private static MediaInfo Kaynak(string audioCodec = "aac") => new()
    {
        FilePath = "girdi.mkv",
        FileSizeBytes = 40_000_000L,
        DurationSeconds = 60,
        Width = 1280,
        Height = 720,
        Fps = 30,
        VideoCodec = "h264",
        AudioCodec = audioCodec,
        AudioChannels = 2,
        TotalBitrateBps = 4_000_000
    };

    private static string Belge => File.ReadAllText(Path.Combine(TipSources.Root, "docs", "olcumler", "b1a-flac-kaplari.md"));

    /// <summary>Belgedeki "girer" satirlari ile kodun kabul ettigi kaplar birebir ayni.</summary>
    [Fact]
    public void IzinVerilenKaplarOlcumBelgesindenOkunuyor()
    {
        var girenler = Regex.Matches(Belge, @"^\| (\w+) \| kabul \|[^|]*\|[^|]*girer", RegexOptions.Multiline)
            .Select(esleme => esleme.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(new[] { "flac", "mkv", "mp4" }, girenler.OrderBy(ad => ad, StringComparer.Ordinal).ToArray());

        foreach (var kap in new[] { "mp4", "mkv", "flac" })
            Assert.Empty(Hatalar(kap, "flac"));
    }

    /// <summary>
    /// Olumsuz kontrol: ffmpeg'in reddettigi dort kap ve "kabul edip tasimayan" iki kap
    /// ayni sekilde eleniyor. Bu satir olmadan yukaridaki olcu "her kap geciyor" diye
    /// yesil kalirdi.
    /// </summary>
    [Theory]
    [InlineData("mov")]
    [InlineData("m4a")]
    [InlineData("webm")]
    [InlineData("mp3")]
    [InlineData("wav")]
    [InlineData("avi")]
    public void TasimayanKapFlaciReddediyor(string kap)
        => Assert.NotEmpty(Hatalar(kap, "flac"));

    /// <summary>Kaynagi zaten flac olan izin kopyalanmasi da ayni kapilardan geciyor.</summary>
    [Fact]
    public void FlacKopyalamaAyniKaplardaGecerli()
    {
        foreach (var kap in new[] { "mp4", "mkv", "flac" })
            Assert.Empty(Hatalar(kap, "copy", "flac"));

        foreach (var kap in new[] { "mov", "m4a", "webm", "wav", "avi" })
            Assert.NotEmpty(Hatalar(kap, "copy", "flac"));
    }

    /// <summary>
    /// FLAC yeni bir <b>ses kabi</b>: mp3/m4a/wav gibi yalniz ses cikariyor. Olumsuz kontrol
    /// mkv'nin ses kabi olmamasi — yoksa "hepsi true" donen bir ozellik de yesil verirdi.
    /// </summary>
    [Fact]
    public void FlacSesKabi()
    {
        Assert.True(new ConversionPlan { Container = "flac" }.AudioOnly);
        Assert.False(new ConversionPlan { Container = "mkv" }.AudioOnly);
        Assert.False(new ConversionPlan { Container = "mp4" }.AudioOnly);
    }

    private static IReadOnlyList<string> Hatalar(string kap, string sesKodegi, string kaynakKodegi = "aac")
    {
        var plan = new ConversionPlan
        {
            Container = kap,
            VideoCodec = "libx264",
            AudioCodec = sesKodegi
        };

        return ConversionArguments.Validate(Kaynak(kaynakKodegi), plan);
    }
}
