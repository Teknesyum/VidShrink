using VidShrink.Cli;
using VidShrink.Core;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// <c>--altyazi-dil</c> ve <c>--ilk-altyazi</c> (HandBrake <c>--subtitle-lang-list</c>,
/// <c>--first-subtitle</c>): kaynak altyazilari dile gore suzulur, istenirse yalniz ilki kalir.
/// </summary>
public sealed class AltyaziDilSecimiTests
{
    private static MediaInfo Ornek() => Kaynak(600, Video,
        new SourceStream(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000),
        new SourceStream(2, StreamKind.Subtitle, "subrip", "eng", Bytes: 1_000),
        new SourceStream(3, StreamKind.Subtitle, "subrip", "tur", Bytes: 1_000),
        new SourceStream(4, StreamKind.Subtitle, "subrip", "fra", Bytes: 1_000),
        new SourceStream(5, StreamKind.Subtitle, "subrip", null, Bytes: 1_000));

    private static string[] Diller(StreamRequest istek, OutputContainer kap = OutputContainer.Mkv)
        => StreamMapping.Decide(Ornek(), istek, kap, 160, null, "aac", true, 100)
            .Subtitles.Select(track => track.Language ?? "-").ToArray();

    private static string[] Eslemler(StreamRequest istek)
        => StreamMapping.Decide(Ornek(), istek, OutputContainer.Mkv, 160, null, "aac", true, 100)
            .Subtitles.Select(track => track.Map).ToArray();

    [Fact]
    public void SuzgecsizIstekHepsiniTasiyor()
        => Assert.Equal(new[] { "eng", "tur", "fra", "-" }, Diller(StreamRequest.Default));

    [Fact]
    public void DilListesiYalnizEslesenleriBirakiyor()
    {
        Assert.Equal(new[] { "tur" }, Diller(StreamRequest.Default with { SubtitleLanguages = new[] { "tr" } }));
        Assert.Equal(new[] { "tur" }, Diller(StreamRequest.Default with { SubtitleLanguages = new[] { "tur" } }));
        Assert.Equal(new[] { "eng", "fra" }, Diller(StreamRequest.Default with { SubtitleLanguages = new[] { "fr", "en" } }));
        Assert.Empty(Diller(StreamRequest.Default with { SubtitleLanguages = new[] { "de" } }));
        Assert.Equal(new[] { "0:3" }, Eslemler(StreamRequest.Default with { SubtitleLanguages = new[] { "tr" } }));
    }

    [Fact]
    public void IlkAltyaziTekIzBirakiyorDilleBirlikteOdildekiIlki()
    {
        Assert.Equal(new[] { "eng" }, Diller(StreamRequest.Default with { FirstSubtitleOnly = true }));
        Assert.Equal(new[] { "tur" },
            Diller(StreamRequest.Default with { FirstSubtitleOnly = true, SubtitleLanguages = new[] { "fr", "tr" } }));
        Assert.Equal(new[] { "tur" }, Diller(StreamRequest.Default with { FirstSubtitleOnly = true, BurnedSubtitle = 0 }));
        Assert.Equal(new[] { "eng" }, Diller(StreamRequest.Default with { FirstSubtitleOnly = true }, OutputContainer.Mp4));
    }

    [Fact]
    public void BayraklarIstektenPlanaIniyor()
    {
        var istek = CliParser.Parse(new[] { "kucult", "a.mkv", "--hedef", "25MB", "--altyazi-dil", "TR, eng", "--ilk-altyazi" }).Request!;
        var ingilizce = CliParser.Parse(new[] { "plan", "a.mkv", "--hedef", "25MB", "--subtitle-lang", "fr", "--first-subtitle" }).Request!;
        var bos = CliParser.Parse(new[] { "kucult", "a.mkv", "--hedef", "25MB" }).Request!;

        Assert.Equal(new[] { "tr", "eng" }, istek.ToPlanOptions(25).SubtitleLanguages);
        Assert.True(istek.ToPlanOptions(25).FirstSubtitleOnly);
        Assert.Equal(new[] { "fr" }, ingilizce.ToPlanOptions(25).SubtitleLanguages);
        Assert.True(ingilizce.ToPlanOptions(25).FirstSubtitleOnly);
        Assert.Empty(bos.ToPlanOptions(25).SubtitleLanguages);
        Assert.False(bos.ToPlanOptions(25).FirstSubtitleOnly);

        foreach (var bozuk in new[] { "t", "turk", "t1", ",", "tr;en" })
            Assert.Equal("error.bad-subtitle-lang",
                CliParser.Parse(new[] { "kucult", "a.mkv", "--hedef", "25MB", "--altyazi-dil", bozuk }).ErrorKey);
        Assert.Equal("error.not-in-watch",
            CliParser.Parse(new[] { "izle", "k", "--cikti", "c", "--hedef", "25MB", "--ilk-altyazi" }).ErrorKey);
    }

    [Fact]
    public void PlanSecimiAkisKararinaTasiyor()
    {
        var plan = PlanCalculator.Build(Ornek(), new PlanOptions
        {
            TargetMb = 25, KeepAllTracks = true, SubtitleLanguages = new[] { "tr" }
        });
        var hepsi = PlanCalculator.Build(Ornek(), new PlanOptions { TargetMb = 25, KeepAllTracks = true });

        Assert.Equal(new[] { "tur" }, plan.Streams!.Subtitles.Select(track => track.Language).ToArray());
        Assert.Equal(4, hepsi.Streams!.Subtitles.Count);
    }
}
