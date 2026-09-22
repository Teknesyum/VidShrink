using VidShrink.Core;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// B1e forced altyazinin kendiliginden varsayilan olmasi: ciktida varsayilan altyazi yoksa
/// zorunlu bayrakli iz varsayilan yapilir — once tercih edilen dilde, sonra tutulan sesin
/// dilinde, yoksa ilki. HandBrake'in "Foreign Audio Search"u degil; yalniz kaynagin bayragi okunur.
/// </summary>
public sealed class ForcedAltyaziTests
{
    private static SubtitleTrack Iz(string dil, bool zorunlu = false, bool varsayilan = false)
        => new("0:" + dil.Length, "copy", false, dil, 1_000, IsDefault: varsayilan, IsForced: zorunlu);

    private static string? Secilen(List<SubtitleTrack> sonuc) => sonuc.SingleOrDefault(track => track.IsDefault)?.Language;

    /// <summary>Tercih edilen dil, sonra ses dili, sonra ilk zorunlu iz.</summary>
    [Fact]
    public void SecimSirasi()
    {
        var izler = new List<SubtitleTrack> { Iz("eng", zorunlu: true), Iz("tur", zorunlu: true), Iz("fra", zorunlu: true) };

        Assert.Equal("tur", Secilen(StreamMapping.DefaultForced(izler, "tr-TR", "fra")));
        Assert.Equal("fra", Secilen(StreamMapping.DefaultForced(izler, null, "fra")));
        Assert.Equal("eng", Secilen(StreamMapping.DefaultForced(izler, "de", "jpn")));
    }

    /// <summary>Zorunlu olmayan iz tercih edilen dilde olsa da secilmez.</summary>
    [Fact]
    public void YalnizZorunluIzSecilir()
    {
        var izler = new List<SubtitleTrack> { Iz("tur"), Iz("eng", zorunlu: true) };

        Assert.Equal("eng", Secilen(StreamMapping.DefaultForced(izler, "tr", null)));
    }

    /// <summary>Olumsuz kontrol: varsayilan zaten varsa ya da zorunlu iz yoksa liste degismez.</summary>
    [Fact]
    public void DegismeyenListeler()
    {
        var varsayilanli = new List<SubtitleTrack> { Iz("eng", zorunlu: true), Iz("tur", varsayilan: true) };
        var zorunsuz = new List<SubtitleTrack> { Iz("eng"), Iz("tur") };

        Assert.Same(varsayilanli, StreamMapping.DefaultForced(varsayilanli, "en", null));
        Assert.Equal("tur", Secilen(varsayilanli));
        Assert.Same(zorunsuz, StreamMapping.DefaultForced(zorunsuz, "en", null));
        Assert.Null(Secilen(zorunsuz));
    }

    /// <summary>Karar zinciri kurali uyguluyor: zorunlu izin bayragi <c>default+forced</c> yaziliyor.</summary>
    [Fact]
    public void KararBayragiYaziyor()
    {
        var info = Kaynak(600, Video,
            new SourceStream(1, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000),
            new SourceStream(2, StreamKind.Subtitle, "subrip", "eng", IsForced: true, Bytes: 1_000),
            new SourceStream(3, StreamKind.Subtitle, "subrip", "tur", IsForced: true, Bytes: 1_000));
        var args = StreamMapping.Decide(info, StreamRequest.Default with { KeepAllTracks = true }, OutputContainer.Mkv, 160, null, "aac", true, 100)
            .OutputArguments().ToList();

        Assert.Equal("forced", args[args.IndexOf("-disposition:s:0") + 1]);
        Assert.Equal("default+forced", args[args.IndexOf("-disposition:s:1") + 1]);
    }

    /// <summary>
    /// Canli kol: yalniz forced bayrakli altyazili kaynak MKV'ye default=1 forced=1 ile cikiyor;
    /// bayraksiz kaynagin altyazisi default=0 kaliyor (olumsuz kontrol).
    /// </summary>
    [Fact]
    public async Task CanliForcedVarsayilan()
    {
        var zorunlu = await AltyaziliAsync("forced-zorunlu.mkv", zorunlu: true);
        var duz = await AltyaziliAsync("forced-duz.mkv", zorunlu: false);

        await KodlaAsync(zorunlu, Yol("forced-zorunlu-cikti.mkv"), _ => { });
        await KodlaAsync(duz, Yol("forced-duz-cikti.mkv"), _ => { });

        var z = (await AkislarAsync(Yol("forced-zorunlu-cikti.mkv"))).Single(stream => Alan(stream, "codec_type") == "subtitle");
        Assert.Equal(1, Bayrak(z, "default"));
        Assert.Equal(1, Bayrak(z, "forced"));
        var d = (await AkislarAsync(Yol("forced-duz-cikti.mkv"))).Single(stream => Alan(stream, "codec_type") == "subtitle");
        Assert.Equal(0, Bayrak(d, "default"));
        Assert.Equal(0, Bayrak(d, "forced"));

        Kapat("forced-*");
    }
}
