using System.Text;
using System.Text.Json;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// <c>--altyazi-tara</c> (HandBrake <c>--subtitle scan</c>, Foreign Audio Search): once kaynagin
/// zorunlu bayragi, bayrak yoksa altyazi paket sayimi. Saf kollar karari (<see cref="ForeignAudioSearch"/>),
/// sahte servisli kollar bulunan izin var olan <c>--yak N</c> yoluna beslendigini, canli kol gercek
/// ffprobe sayimini olcer. Olcum ve esigin dayanagi <c>docs/olcumler/yabanci-ses-arama.md</c>.
/// </summary>
public sealed class AltyaziTaramaTests
{
    private static readonly SourceStream Ses = new(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000);

    private static SourceStream Alt(int index, string dil, string kodek = "subrip", bool zorunlu = false)
        => new(index, StreamKind.Subtitle, kodek, dil, IsForced: zorunlu, Bytes: 1_000);

    private static Dictionary<int, int> Sayim(params (int Index, int Paket)[] satirlar)
        => satirlar.ToDictionary(satir => satir.Index, satir => satir.Paket);

    private static string[] Komut(params string[] ekler)
        => new[] { "plan", "a.mp4", "--hedef", "25MB" }.Concat(ekler).ToArray();

    /// <summary>Bayrakli iz sirasi: tercih edilen dil, sonra sesin dili, sonra ilki; sayi altyazi sirasindan.</summary>
    [Fact]
    public void BayrakliIzSirasi()
    {
        var info = Kaynak(600, Video, Ses, Alt(2, "fra"), Alt(3, "fra", zorunlu: true), Alt(4, "eng", zorunlu: true), Alt(5, "tur", zorunlu: true));

        Assert.Equal(4, ForeignAudioSearch.Flagged(info, "tr-TR")!.Number);
        Assert.Equal(3, ForeignAudioSearch.Flagged(info, null)!.Number);
        Assert.Equal(3, ForeignAudioSearch.Flagged(info, "de")!.Number);
        Assert.Equal(ForeignAudioOutcome.Flagged, ForeignAudioSearch.Flagged(info, "de")!.Outcome);
        var sessiz = Kaynak(600, Video, Alt(1, "fra", zorunlu: true), Alt(2, "eng", zorunlu: true));
        Assert.Equal(1, ForeignAudioSearch.Flagged(sessiz, "de")!.Number);
    }

    /// <summary>Olumsuz kontrol: bayraksiz kaynakta ve yakilamayan kodekte bayrakli iz bulunmaz.</summary>
    [Fact]
    public void BayrakYoksaBulunmaz()
    {
        Assert.Null(ForeignAudioSearch.Flagged(Kaynak(600, Video, Ses, Alt(2, "eng"), Alt(3, "tur")), "tr"));
        Assert.Null(ForeignAudioSearch.Flagged(Kaynak(600, Video, Ses, Alt(2, "eng", "eia_608", zorunlu: true)), "tr"));
    }

    /// <summary>Esigin iki yakasi: en dolu izin onda biri gecer, bir fazlasi gecmez.</summary>
    [Theory]
    [InlineData(10, ForeignAudioOutcome.Sparse)]
    [InlineData(1, ForeignAudioOutcome.Sparse)]
    [InlineData(11, ForeignAudioOutcome.NoCandidate)]
    [InlineData(100, ForeignAudioOutcome.NoCandidate)]
    [InlineData(0, ForeignAudioOutcome.NoCandidate)]
    public void EsiginIkiYakasi(int seyrek, ForeignAudioOutcome beklenen)
    {
        var info = Kaynak(600, Video, Ses, Alt(2, "eng"), Alt(3, "eng"));

        var karar = ForeignAudioSearch.Decide(info, Sayim((2, 100), (3, seyrek)), null);

        Assert.Equal(beklenen, karar.Outcome);
        if (beklenen == ForeignAudioOutcome.Sparse)
        {
            Assert.Equal(2, karar.Number);
            Assert.Equal(seyrek, karar.Packets);
            Assert.Equal(100, karar.FullestPackets);
        }
        else Assert.Null(karar.Number);
    }

    /// <summary>Sayimda hic gecmeyen iz bos sayilir ve secilmez; tek iz kiyaslanacak es bulamaz.</summary>
    [Fact]
    public void BosVeTekIzSecilmez()
    {
        var cift = Kaynak(600, Video, Ses, Alt(2, "eng"), Alt(3, "eng"));
        var tek = Kaynak(600, Video, Ses, Alt(2, "eng"));

        Assert.Equal(ForeignAudioOutcome.NoCandidate, ForeignAudioSearch.Decide(cift, Sayim((2, 100)), null).Outcome);
        Assert.Equal(ForeignAudioOutcome.NoCandidate, ForeignAudioSearch.Decide(tek, Sayim((2, 3)), null).Outcome);
    }

    /// <summary>Ayri dildeki ya da ayri turdeki (metin, resim) izler birbiriyle kiyaslanmaz.</summary>
    [Fact]
    public void DilVeTurKarismaz()
    {
        var ayriDil = Kaynak(600, Video, Ses, Alt(2, "eng"), Alt(3, "fra"));
        var ayriTur = Kaynak(600, Video, Ses, Alt(2, "eng"), Alt(3, "eng", "hdmv_pgs_subtitle"));
        var resim = Kaynak(600, Video, Ses, Alt(2, "eng", "hdmv_pgs_subtitle"), Alt(3, "eng", "hdmv_pgs_subtitle"));
        var sayim = Sayim((2, 100), (3, 4));

        Assert.Equal(ForeignAudioOutcome.NoCandidate, ForeignAudioSearch.Decide(ayriDil, sayim, null).Outcome);
        Assert.Equal(ForeignAudioOutcome.NoCandidate, ForeignAudioSearch.Decide(ayriTur, sayim, null).Outcome);
        Assert.Equal(2, ForeignAudioSearch.Decide(resim, sayim, null).Number);
    }

    /// <summary>
    /// Iki dilde aday: tercih edilen dil, sonra sesin dili ayirir; ikisi de ayirmiyorsa ve ayni
    /// dilde iki seyrek iz varsa iz secilmez.
    /// </summary>
    [Fact]
    public void BelirsizdeSecilmez()
    {
        var ikiDil = Kaynak(600, Video, Ses, Alt(2, "fra"), Alt(3, "fra"), Alt(4, "eng"), Alt(5, "eng"));
        var sayim = Sayim((2, 200), (3, 5), (4, 300), (5, 9));

        Assert.Equal(2, ForeignAudioSearch.Decide(ikiDil, sayim, "fr").Number);
        Assert.Equal(4, ForeignAudioSearch.Decide(ikiDil, sayim, "de").Number);
        var yabanci = Kaynak(600, Video, Ses with { Language = "jpn" }, Alt(2, "fra"), Alt(3, "fra"), Alt(4, "eng"), Alt(5, "eng"));
        var karar = ForeignAudioSearch.Decide(yabanci, sayim, "de");
        Assert.Equal(ForeignAudioOutcome.Ambiguous, karar.Outcome);
        Assert.Null(karar.Number);

        var ikiSeyrek = Kaynak(600, Video, Ses, Alt(2, "eng"), Alt(3, "eng"), Alt(4, "eng"));
        Assert.Equal(ForeignAudioOutcome.Ambiguous, ForeignAudioSearch.Decide(ikiSeyrek, Sayim((2, 100), (3, 4), (4, 6)), null).Outcome);
    }

    /// <summary>Sayim yoksa ya da yakilabilir altyazi yoksa iz secilmez ve nedeni ayri soylenir.</summary>
    [Fact]
    public void SayimsizVeAltyazisiz()
    {
        var info = Kaynak(600, Video, Ses, Alt(2, "eng"), Alt(3, "eng"));

        Assert.Equal(ForeignAudioOutcome.NotMeasured, ForeignAudioSearch.Decide(info, null, null).Outcome);
        Assert.Equal(ForeignAudioOutcome.NoSubtitles, ForeignAudioSearch.Decide(Kaynak(600, Video, Ses), Sayim(), null).Outcome);
        Assert.Equal(ForeignAudioOutcome.NoSubtitles, ForeignAudioSearch.Decide(Kaynak(600, Video, Ses, Alt(2, "eng", "eia_608")), Sayim((2, 5)), null).Outcome);
    }

    /// <summary>Alti sonucun adi ayri ve her biri iki CLI dilinde metinli; yardim bayragi anlatiyor.</summary>
    [Fact]
    public void SonuclarIkiDildeMetinli()
    {
        var adlar = Enum.GetValues<ForeignAudioOutcome>().Select(sonuc => new ForeignAudioPick(sonuc).Slug).ToList();

        Assert.Equal(6, adlar.Distinct().Count());
        foreach (var dil in new[] { "tr", "en" })
        {
            var text = CliText.ForLanguage(dil);
            foreach (var ad in adlar) Assert.NotEqual("result.subtitle-scan." + ad, text["result.subtitle-scan." + ad]);
            Assert.NotEqual("progress.subtitle-scan", text["progress.subtitle-scan"]);
            Assert.NotEqual("error.scan-burn-conflict", text["error.scan-burn-conflict"]);
            Assert.Contains("--altyazi-tara", text["help"]);
            Assert.Contains("7", text.Format("result.subtitle-scan.sparse", 7, 8, 9));
            Assert.Contains("9", text.Format("result.subtitle-scan.sparse", 7, 8, 9));
        }
        Assert.Contains("--subtitle-scan", CliText.ForLanguage("en")["help"]);
    }

    /// <summary>Iki yazim ayni istege iner; bayraksiz kosumda tarama kapali (olumsuz kontrol).</summary>
    [Theory]
    [InlineData("--altyazi-tara")]
    [InlineData("--subtitle-scan")]
    public void BayrakAyrisiyor(string bayrak)
    {
        var sonuc = CliParser.Parse(Komut(bayrak));

        Assert.Null(sonuc.ErrorKey);
        Assert.True(sonuc.Request!.SubtitleScan);
        Assert.Null(sonuc.Request.BurnSubtitle);
        Assert.False(CliParser.Parse(Komut()).Request!.SubtitleScan);
    }

    /// <summary>Tarama yakilacak izi kendi secer: elle yakma secenegiyle iki sirada da reddedilir, izle almaz.</summary>
    [Fact]
    public void YakmaylaVeIzleyleReddedilir()
    {
        Assert.Equal("error.scan-burn-conflict", CliParser.Parse(Komut("--altyazi-tara", "--yak", "1")).ErrorKey);
        Assert.Equal("error.scan-burn-conflict", CliParser.Parse(Komut("--yak", "1", "--subtitle-scan")).ErrorKey);
        Assert.Equal("error.scan-burn-conflict", CliParser.Parse(Komut("--altyazi-tara", "--yak-srt", "a.srt")).ErrorKey);
        Assert.Null(CliParser.Parse(Komut("--yak", "1")).ErrorKey);
        foreach (var bayrak in new[] { "--altyazi-tara", "--subtitle-scan" })
            Assert.Equal("error.not-in-watch", CliParser.Parse(new[] { "izle", "giris", "--hedef", "25MB", bayrak }).ErrorKey);
    }

    private sealed record Kosum(int Exit, string Stdout, string Stderr, int Sayim);

    private static async Task<Kosum> Kos(SourceStream[] akislar, IReadOnlyDictionary<int, int>? paketler, params string[] ekler)
    {
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "altyazi-tarama", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        try
        {
            var dosya = Path.Combine(klasor, "kaynak.mkv");
            await File.WriteAllTextAsync(dosya, "x");
            var info = new MediaInfo
            {
                FilePath = dosya,
                FileSizeBytes = 400_000_000L,
                DurationSeconds = 600,
                Width = 1920,
                Height = 1080,
                Fps = 24,
                VideoCodec = "h264",
                TotalBitrateBps = 5_000_000,
                AudioCodec = "aac",
                AudioBitrateBps = 128_000,
                AudioChannels = 2,
                Streams = akislar
            };
            var sayac = 0;
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var args = new List<string> { "plan", dosya, "--hedef", "25MB", "--olcumsuz", "--json" };
            args.AddRange(ekler);

            var exit = await CliApp.RunAsync(args, stdout, stderr, CliText.ForLanguage("tr"),
                new CliServices
                {
                    MissingTool = () => null,
                    Probe = (_, _) => Task.FromResult(info),
                    Availability = () => null,
                    HasFilter = _ => true,
                    CountSubtitlePackets = (_, _) =>
                    {
                        sayac++;
                        return Task.FromResult(paketler);
                    },
                },
                CancellationToken.None);

            return new Kosum(exit, stdout.ToString(), stderr.ToString(), sayac);
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }

    private static string Zincir(string stdout)
    {
        using var belge = JsonDocument.Parse(stdout);
        var argumanlar = belge.RootElement.GetProperty("arguments").EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
        var i = Array.IndexOf(argumanlar, "-vf");
        return i >= 0 && i + 1 < argumanlar.Length ? argumanlar[i + 1] : "";
    }

    /// <summary>Bayrakli iz varken paket sayimi hic kosmaz ve iz <c>--yak N</c> yolundan plana girer.</summary>
    [Fact]
    public async Task BayrakliIzSayimsizYakilir()
    {
        var kosum = await Kos(new[] { Video, Ses, Alt(2, "eng"), Alt(3, "eng", zorunlu: true) }, Sayim((2, 1), (3, 500)), "--altyazi-tara");

        Assert.Equal(ExitCodes.InBand, kosum.Exit);
        Assert.Equal(0, kosum.Sayim);
        Assert.Contains(":si=1", Zincir(kosum.Stdout));
        Assert.Contains(CliText.ForLanguage("tr").Format("result.subtitle-scan.flagged", 2, 0, 0), kosum.Stderr);
    }

    /// <summary>Bayrak yokken sayim bir kez kosar; seyrek iz ayni <c>subtitles=</c> suzgecine iner.</summary>
    [Fact]
    public async Task SeyrekIzSayimlaYakilir()
    {
        var kosum = await Kos(new[] { Video, Ses, Alt(2, "eng"), Alt(3, "eng") }, Sayim((2, 900), (3, 40)), "--subtitle-scan");

        Assert.Equal(ExitCodes.InBand, kosum.Exit);
        Assert.Equal(1, kosum.Sayim);
        Assert.Contains(":si=1", Zincir(kosum.Stdout));
        Assert.Contains(CliText.ForLanguage("tr")["progress.subtitle-scan"], kosum.Stderr);
        Assert.Contains(CliText.ForLanguage("tr").Format("result.subtitle-scan.sparse", 2, 40, 900), kosum.Stderr);
    }

    /// <summary>Aday yoksa ya da sayim dustuyse yakma yok ve neden stderr'de; bayraksiz kosum hic saymaz.</summary>
    [Fact]
    public async Task AdayYoksaYakilmazVeSoylenir()
    {
        var akislar = new[] { Video, Ses, Alt(2, "eng"), Alt(3, "eng") };
        var text = CliText.ForLanguage("tr");

        var dolu = await Kos(akislar, Sayim((2, 900), (3, 800)), "--altyazi-tara");
        Assert.Equal(ExitCodes.InBand, dolu.Exit);
        Assert.DoesNotContain("subtitles=", Zincir(dolu.Stdout));
        Assert.Contains(text["result.subtitle-scan.no-candidate"], dolu.Stderr);

        var olculmedi = await Kos(akislar, null, "--altyazi-tara");
        Assert.DoesNotContain("subtitles=", Zincir(olculmedi.Stdout));
        Assert.Contains(text["result.subtitle-scan.not-measured"], olculmedi.Stderr);

        var altyazisiz = await Kos(new[] { Video, Ses }, Sayim(), "--altyazi-tara");
        Assert.Equal(0, altyazisiz.Sayim);
        Assert.Contains(text["result.subtitle-scan.no-subtitles"], altyazisiz.Stderr);

        var bayraksiz = await Kos(akislar, Sayim((2, 900), (3, 40)));
        Assert.Equal(0, bayraksiz.Sayim);
        Assert.DoesNotContain("subtitles=", Zincir(bayraksiz.Stdout));
        Assert.DoesNotContain("Altyazı taraması", bayraksiz.Stderr);
    }

    private static string Srt(int satir, double aralik)
    {
        var metin = new StringBuilder();
        for (var i = 0; i < satir; i++)
        {
            var bas = TimeSpan.FromSeconds(i * aralik);
            var son = bas + TimeSpan.FromSeconds(aralik * 0.8);
            metin.Append(i + 1).Append('\n')
                .Append(bas.ToString(@"hh\:mm\:ss\,fff")).Append(" --> ").Append(son.ToString(@"hh\:mm\:ss\,fff")).Append('\n')
                .Append("SATIR ").Append(i + 1).Append("\n\n");
        }
        return metin.ToString();
    }

    /// <summary>
    /// Canli kol: 3 sn 320x240 klipte 12 satirlik ve 1 satirlik iki srt izi. Gercek ffprobe
    /// sayimi 12 ve 1 okur, karar ikinci izi secer; ayni sayida satir tasiyan es klipte iz
    /// secilmez (olumsuz kontrol).
    /// </summary>
    [FfmpegFact]
    public async Task CanliPaketSayimi()
    {
        var dolu = Yol("tarama-dolu.srt");
        var seyrek = Yol("tarama-seyrek.srt");
        File.WriteAllText(dolu, Srt(12, 0.25), new UTF8Encoding(false));
        File.WriteAllText(seyrek, Srt(1, 0.25), new UTF8Encoding(false));
        try
        {
            var klip = await IkiIzliAsync("tarama-klip.mkv", dolu, seyrek);
            var es = await IkiIzliAsync("tarama-es.mkv", dolu, dolu);

            var info = await FfprobeClient.ProbeAsync(klip);
            var sayim = await FfprobeClient.CountSubtitlePacketsAsync(klip);
            Assert.NotNull(sayim);
            var altyazilar = info.Streams.Where(stream => stream.Kind == StreamKind.Subtitle).ToList();
            Assert.Equal(2, altyazilar.Count);
            Assert.Equal(12, sayim![altyazilar[0].Index]);
            Assert.Equal(1, sayim[altyazilar[1].Index]);
            Assert.Equal(2, sayim.Count);
            Assert.Null(ForeignAudioSearch.Flagged(info, null));
            var karar = ForeignAudioSearch.Decide(info, sayim, null);
            Assert.Equal(ForeignAudioOutcome.Sparse, karar.Outcome);
            Assert.Equal(2, karar.Number);

            var esInfo = await FfprobeClient.ProbeAsync(es);
            var esKarar = ForeignAudioSearch.Decide(esInfo, await FfprobeClient.CountSubtitlePacketsAsync(es), null);
            Assert.Equal(ForeignAudioOutcome.NoCandidate, esKarar.Outcome);

            Assert.Null(await FfprobeClient.CountSubtitlePacketsAsync(Yol("tarama-olmayan.mkv")));
        }
        finally
        {
            Kapat("tarama-*");
        }
    }

    private static async Task<string> IkiIzliAsync(string ad, string ilk, string ikinci)
    {
        var yol = Yol(ad);
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-threads", "2",
            "-f", "lavfi", "-i", "color=c=black:size=320x240:rate=25:duration=3",
            "-i", ilk, "-i", ikinci,
            "-map", "0", "-map", "1", "-map", "2", "-c:v", "libx264", "-preset", "ultrafast", "-threads", "2",
            "-c:s", "srt", "-metadata:s:s:0", "language=eng", "-metadata:s:s:1", "language=eng", yol
        });
        return yol;
    }
}
