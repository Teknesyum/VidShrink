using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// E6: iz adı ve altyazı bayrakları (<c>docs/netlestirme/023-altyazi-bayragi-iz-adi.md</c>).
/// Karar "kaynağı aynen taşı": başlık varsa <c>-metadata:s:?:N title=</c>, altyazının
/// varsayılan/zorunlu bayrağı <c>-disposition:s:N</c> ile açık yazılır — mp4 muxer'ı
/// yazılmadığında ilk izi kendiliğinden varsayılan yapıyor
/// (<c>docs/olcumler/e6-altyazi-bayragi-iz-adi.md</c>).
/// </summary>
public sealed class IzAdiBayragiTests
{
    private static readonly SourceStream Video = new(0, StreamKind.Video, "h264");

    private static MediaInfo Kaynak(params SourceStream[] streams) => new()
    {
        FilePath = "girdi.mkv",
        FileSizeBytes = 400L * 1024 * 1024,
        DurationSeconds = 600,
        Width = 1920,
        Height = 1080,
        Fps = 24,
        VideoCodec = "h264",
        TotalBitrateBps = 5_000_000,
        AudioCodec = streams.FirstOrDefault(stream => stream.Kind == StreamKind.Audio)?.Codec,
        AudioBitrateBps = streams.FirstOrDefault(stream => stream.Kind == StreamKind.Audio)?.BitrateBps ?? 0,
        AudioChannels = streams.FirstOrDefault(stream => stream.Kind == StreamKind.Audio)?.Channels ?? 0,
        Streams = streams
    };

    private static SourceStream Ses(string? baslik = null)
        => new(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000, Title: baslik);

    private static SourceStream Altyazi(string? baslik = null, bool varsayilan = false, bool zorunlu = false)
        => new(2, StreamKind.Subtitle, "subrip", "eng", IsDefault: varsayilan, IsForced: zorunlu,
            Bytes: 20_000, Title: baslik);

    private static IReadOnlyList<string> Argumanlar(OutputContainer kap, params SourceStream[] yan)
        => StreamMapping.Decide(Kaynak(new[] { Video }.Concat(yan).ToArray()),
            StreamRequest.Default, kap, 160, null, "aac", true, 100).OutputArguments();

    /// <summary>
    /// Ses izinin adı taşınıyor. Adsız kaynakta anahtar hiç yazılmıyor — uydurma başlık
    /// üretilmediği olumsuz kontrolle pimli.
    /// </summary>
    [Fact]
    public void SesIziAdiTasiniyor()
    {
        var adli = Argumanlar(OutputContainer.Mp4, Ses("Ana Ses"));
        var adsiz = Argumanlar(OutputContainer.Mp4, Ses());

        Assert.Equal("title=Ana Ses", adli[adli.ToList().IndexOf("-metadata:s:a:0") + 1]);
        Assert.DoesNotContain("-metadata:s:a:0", adsiz);
    }

    /// <summary>Altyazı adı da taşınıyor, adsız altyazıda yazılmıyor.</summary>
    [Fact]
    public void AltyaziAdiTasiniyor()
    {
        var adli = Argumanlar(OutputContainer.Mkv, Ses(), Altyazi("Zorunlu TR"));
        var adsiz = Argumanlar(OutputContainer.Mkv, Ses(), Altyazi());

        Assert.Equal("title=Zorunlu TR", adli[adli.ToList().IndexOf("-metadata:s:s:0") + 1]);
        Assert.DoesNotContain("-metadata:s:s:0", adsiz);
    }

    /// <summary>
    /// Bayrak kaynaktan türüyor: dört bileşim de ayrı bir <c>-disposition</c> değeri veriyor.
    /// Bilgi yoksa <c>0</c> — mp4 muxer'ının kendiliğinden varsayılan yapmasını kesen değer budur.
    /// </summary>
    [Theory]
    [InlineData(false, false, "0")]
    [InlineData(true, false, "default")]
    [InlineData(false, true, "default+forced")]
    [InlineData(true, true, "default+forced")]
    public void AltyaziBayragiKaynaktanTuruyor(bool varsayilan, bool zorunlu, string beklenen)
    {
        var args = Argumanlar(OutputContainer.Mp4, Ses(), Altyazi(varsayilan: varsayilan, zorunlu: zorunlu)).ToList();

        Assert.Equal(beklenen, args[args.IndexOf("-disposition:s:0") + 1]);
    }

    /// <summary>
    /// Bayrak her altyazıya ayrı yazılıyor: ikinci izin varsayılanı birincininkini izlemiyor.
    /// </summary>
    [Fact]
    public void HerAltyaziKendiBayraginiAliyor()
    {
        var args = StreamMapping.Decide(
            Kaynak(Video, Ses(),
                new SourceStream(2, StreamKind.Subtitle, "subrip", "eng", Bytes: 20_000),
                new SourceStream(3, StreamKind.Subtitle, "subrip", "tur", IsDefault: true, Bytes: 20_000)),
            StreamRequest.Default with { KeepAllTracks = true }, OutputContainer.Mkv,
            160, null, "aac", true, 100).OutputArguments().ToList();

        Assert.Equal("0", args[args.IndexOf("-disposition:s:0") + 1]);
        Assert.Equal("default", args[args.IndexOf("-disposition:s:1") + 1]);
    }

    /// <summary>Altyazı yoksa hiç bayrak yazılmıyor.</summary>
    [Fact]
    public void AltyazisizCiktidaBayrakYok()
        => Assert.DoesNotContain(Argumanlar(OutputContainer.Mp4, Ses()), a => a.StartsWith("-disposition:s:", StringComparison.Ordinal));

    /// <summary>
    /// Sesin varsayılan bayrağı eski kuralında: tek iz taşındığında <c>default</c>, tüm izler
    /// korunduğunda yazılmıyor. E6 ses tarafına dokunmadı.
    /// </summary>
    [Fact]
    public void SesVarsayilaniEskiKuralinda()
    {
        Assert.Contains("-disposition:a:0", Argumanlar(OutputContainer.Mp4, Ses()));

        var korunan = StreamMapping.Decide(
            Kaynak(Video, Ses(), new SourceStream(2, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000)),
            StreamRequest.Default with { KeepAllTracks = true }, OutputContainer.Mkv,
            160, null, "aac", true, 100).OutputArguments();

        Assert.DoesNotContain("-disposition:a:0", korunan);
    }
}
