using VidShrink.App;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// later.md #44, 18 Ekim 2026: Bölüm Düzenleyici'nin komut satırı yüzü ve Onar'ın gerçek bozuk dosyada ölçüsü.
/// <c>donustur</c>'un <c>--etiket-*</c>, <c>--kapak</c>, <c>--onar</c>, <c>--bolum-ekle</c> ve <c>--bolumsuz</c>
/// seçenekleri panelin etiket ve bölüm açılırlarıyla aynı ffmpeg komutunu üretmeli; panel başsız açılır ve aynı seçim
/// alanlara yazılır. Canlı kol yarıda kesilmiş, ortası bozulmuş bir MPEG-TS kaydını CLI'dan onarır ve çıktıyı
/// ffmpeg ile baştan sona çözer.
/// </summary>
public sealed class BolumDuzenleyiciCliTests
{
    private static readonly SourceStream Ses = new(1, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000);

    private static MediaInfo Ornek() => Kaynak(120, Video, Ses) with { FilePath = @"C:\Kayitlar\gezi.mkv" };

    private static CliRequest Ayristir(params string[] args)
    {
        var parsed = CliParser.Parse(args);
        Assert.True(parsed.Request is not null, parsed.ErrorKey + " " + parsed.ErrorArgument);
        return parsed.Request!;
    }

    [Fact]
    public void EtiketVeBolumSecenekleriPlanaGecer()
    {
        var plan = CliConvert.Plan(Ayristir("donustur", "a.mkv", "--etiket-baslik", " Gezi ", "--tag-artist", "Ayşe",
            "--etiket-yil", "2026", "--tag-comment", "not", "--bolum-ekle", "1:30=Ana", "--add-chapter", "0=Giriş", "--bolum-ekle", "45"));

        Assert.Equal((" Gezi ", "Ayşe", "2026", "not"), (plan.Title, plan.Artist, plan.Year, plan.Comment));
        Assert.False(plan.Repair);
        Assert.Equal(new[] { (90d, "Ana"), (0d, "Giriş"), (45d, "") }, plan.Chapters!.Select(c => (c.Start.TotalSeconds, c.Title)).ToArray());
        Assert.Null(CliConvert.Plan(Ayristir("donustur", "a.mkv")).Chapters);
        Assert.Empty(CliConvert.Plan(Ayristir("donustur", "a.mkv", "--bolumsuz")).Chapters!);
        Assert.Equal(Path.GetFullPath("kapak.png"), CliConvert.Plan(Ayristir("donustur", "a.mkv", "--kap", "mkv", "--kapak", "kapak.png")).CoverPath);
    }

    [Fact]
    public void OnarVarsayilaniPanelinOnarHazirAyari()
    {
        var yalin = CliConvert.Plan(Ayristir("donustur", "a.ts", "--onar"));
        Assert.Equal(("mkv", "copy", "copy", true), (yalin.Container, yalin.VideoCodec, yalin.AudioCodec, yalin.Repair));

        var secili = CliConvert.Plan(Ayristir("convert", "a.ts", "--repair", "--container", "mp4", "--codec", "hevc", "--audio-codec", "opus"));
        Assert.Equal(("mp4", "libx265", "libopus", true), (secili.Container, secili.VideoCodec, secili.AudioCodec, secili.Repair));

        Assert.Equal("mp4", CliConvert.Plan(Ayristir("donustur", "a.ts", "--onar", "-o", "b.mp4")).Container);
        Assert.Equal("libx264", CliConvert.Plan(Ayristir("donustur", "a.ts")).VideoCodec);
    }

    [Theory]
    [InlineData("error.bad-chapter", new[] { "donustur", "a.mp4", "--bolum-ekle", "yarım=Ad" })]
    [InlineData("error.bad-chapter", new[] { "donustur", "a.mp4", "--bolum-ekle", "1:75=Ad" })]
    [InlineData("error.missing-value", new[] { "donustur", "a.mp4", "--bolum-ekle" })]
    [InlineData("error.missing-value", new[] { "donustur", "a.mp4", "--etiket-baslik" })]
    [InlineData("error.chapters-conflict", new[] { "donustur", "a.mp4", "--bolumsuz", "--bolum-ekle", "0=Ad" })]
    [InlineData("error.chapters-container", new[] { "donustur", "a.mp4", "--kap", "gif", "--bolum-ekle", "0=Ad" })]
    [InlineData("error.chapters-container", new[] { "donustur", "a.mp4", "--kap", "webm", "--bolumsuz" })]
    public void HataliBolumSecimiReddedilir(string anahtar, string[] args)
        => Assert.Equal(anahtar, CliParser.Parse(args).ErrorKey);

    [Fact]
    public void YeniHataAnahtarlariIkiDildeVar()
    {
        foreach (var dil in new[] { "en", "tr" })
        {
            var metin = CliText.ForLanguage(dil);
            foreach (var anahtar in new[] { "error.bad-chapter", "error.chapters-conflict", "error.chapters-container", "error.cover-missing" })
                Assert.NotEqual(anahtar, metin[anahtar]);
            Assert.Contains("--bolum-ekle", metin["help"], StringComparison.Ordinal);
            Assert.Contains("--onar", metin["help"], StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task EksikKapakKodlamaKosmadanDurur()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "bolum-cli", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var girdi = Path.Combine(klasor, "girdi.mkv");
            File.WriteAllBytes(girdi, new byte[16]);
            var stderr = new StringWriter();
            var services = new CliServices
            {
                MissingTool = () => null,
                Probe = (_, _) => Task.FromResult(Ornek() with { FilePath = girdi }),
                Availability = () => null,
                Convert = (_, _, _, _, _) => throw new InvalidOperationException("kodlama kosmamali")
            };
            var exit = await CliApp.RunAsync(new[] { "donustur", girdi, "--kap", "mkv", "--kapak", Path.Combine(klasor, "yok.png") },
                new StringWriter(), stderr, CliText.ForLanguage("en"), services, CancellationToken.None);

            Assert.Equal(ExitCodes.Error, exit);
            Assert.Contains("yok.png", stderr.ToString(), StringComparison.Ordinal);
        }
        finally { Directory.Delete(klasor, recursive: true); }
    }

    public static TheoryData<string[]> Secimler => new()
    {
        new[] { "--onar" },
        new[] { "--kap", "mkv", "--etiket-baslik", "Gezi", "--etiket-sanatci", "Ayşe", "--etiket-yil", "2026", "--etiket-yorum", "deneme" },
        new[] { "--kap", "mp4", "--bolum-ekle", "0=Giriş", "--bolum-ekle", "1:30=Ana" },
        new[] { "--kap", "mkv", "--kes", "20-80", "--bolum-ekle", "0=Giriş", "--bolum-ekle", "30=Orta", "--bolum-ekle", "1:10=Son" },
        new[] { "--kap", "mov", "--bolumsuz", "--etiket-baslik", "Bölümsüz" }
    };

    /// <summary>
    /// Panel kolu: aynı ön ayar <see cref="MainWindow.ApplyConvertPreset"/> ile, Onar <see cref="MainWindow.ApplyConvertRepairPreset"/>
    /// ile, etiketler alanlara, bölümler <see cref="MainWindow.AddConvertChapter"/> satırlarına yazılır; komut iki yüzde aynı olmalı.
    /// </summary>
    [Theory]
    [MemberData(nameof(Secimler))]
    public void DonusturKomutuPanelinEtiketVeBolumKomutuylaAyni(string[] secim)
    {
        var info = Ornek();
        var request = Ayristir(new[] { "donustur", info.FilePath }.Concat(secim).ToArray());
        var cliPlan = CliConvert.Plan(request);
        var sabit = Path.Combine(@"C:\cikti", "out." + cliPlan.Container);
        var cliKomut = FfmpegArguments.ToCommandLine(CliConvert.Arguments(info, cliPlan, sabit, null));

        var (panelPlan, panelKomut) = AppHost.Run(() =>
        {
            var klasor = Path.Combine(TestPaths.OutputRoot, "bolum-cli", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(klasor);
            var window = new MainWindow
            {
                SettingsPathOverride = Path.Combine(klasor, "settings.json"),
                PresetPathOverride = Path.Combine(klasor, "presets.json"),
                ConvertPresetPathOverride = Path.Combine(klasor, "convert-presets.json")
            };
            try
            {
                window.LoadWithoutProbing(info.FilePath, info);
                var etiket = request.ConvertTags;
                if (etiket.Repair) window.ApplyConvertRepairPreset();
                else window.ApplyConvertPreset(request.Conversion!);
                window.TxtConvertTagTitle.Text = etiket.Title;
                window.TxtConvertTagArtist.Text = etiket.Artist;
                window.TxtConvertTagYear.Text = etiket.Year;
                window.TxtConvertTagComment.Text = etiket.Comment;
                if (request.TrimStartSeconds is { } bas) window.TxtTrimStart.Text = Saat.Ekran(TimeSpan.FromSeconds(bas), TimeSpan.FromHours(1));
                if (request.TrimEndSeconds is { } son) window.TxtTrimEnd.Text = Saat.Ekran(TimeSpan.FromSeconds(son), TimeSpan.FromHours(1));
                if (etiket.Chapters is { } bolumler)
                {
                    window.AddConvertChapter();
                    window.RemoveConvertChapter(0);
                    foreach (var bolum in bolumler)
                    {
                        window.AddConvertChapter();
                        var satir = window.ConvertChapterFields[^1];
                        satir.Time.Text = Saat.Ekran(bolum.Start, TimeSpan.FromHours(1));
                        satir.Name.Text = bolum.Title;
                    }
                }
                var plan = window.CurrentConversionPlan();
                return (plan, FfmpegArguments.ToCommandLine(ConversionArguments.Build(info, plan, sabit, plan.Gif ? "palette.png" : null)));
            }
            finally
            {
                window.Close();
                Directory.Delete(klasor, recursive: true);
            }
        });

        Assert.Equal((cliPlan.Container, cliPlan.VideoCodec, cliPlan.AudioCodec, cliPlan.Repair), (panelPlan.Container, panelPlan.VideoCodec, panelPlan.AudioCodec, panelPlan.Repair));
        Assert.Equal(cliPlan.Chapters?.Select(c => (c.Start, c.Title)).ToArray(), panelPlan.Chapters?.Select(c => (c.Start, c.Title)).ToArray());
        Assert.Equal(ConversionChapters.Metadata(info, cliPlan), ConversionChapters.Metadata(info, panelPlan));
        Assert.Equal(panelKomut, cliKomut);
    }

    private static async Task<int> CozmeHatasiAsync(string yol)
    {
        var (_, _, err) = await AkisGirdisi.RunAsync(ToolLocator.Ffmpeg, new[] { "-v", "error", "-threads", "2", "-i", yol, "-f", "null", "-" });
        return err.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    /// <summary>
    /// Onar'ın gerçek bozuk dosyada ölçüsü: 6 sn'lik, 5 sn'den başlayan MPEG-TS kaydının ortasında 20 paket bozulur ve
    /// dosya bir paketin ortasında kesilir (yarıda kalmış kayıt). Düz yeniden sarma (negatif kontrol) bozuk sesi taşır ve
    /// çözülürken hata verir. <c>donustur --onar</c> MKV'ye kopyalar: çıktı hatasız çözülür, 0'dan başlar, video ve ses izi
    /// kalır, süre korunur; aynı koşumda verilen bölümler ve başlık ffprobe'dan okunur.
    /// </summary>
    [FfmpegFact]
    public async Task OnarBozukKaydiHatasizCozulurHaleGetirir()
    {
        var adlar = new[] { "onar-kaynak.ts", "onar-bozuk.ts", "onar-duz.mkv", "onar-cikti.mkv" };
        Kapat(adlar);
        try
        {
            var kaynak = Yol(adlar[0]);
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
            {
                "-hide_banner", "-y", "-threads", "2",
                "-f", "lavfi", "-i", "testsrc=size=160x120:rate=12:duration=6",
                "-f", "lavfi", "-i", "sine=frequency=440:duration=6",
                "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-c:a", "aac", "-output_ts_offset", "5", kaynak
            });
            var bayt = await File.ReadAllBytesAsync(kaynak);
            var bas = (int)(bayt.Length * 0.4);
            for (var i = 0; i < 188 * 20; i++) bayt[bas + i] = (byte)(i * 73 + 11);
            var bozuk = Yol(adlar[1]);
            await File.WriteAllBytesAsync(bozuk, bayt.AsSpan(0, (int)(bayt.Length * 0.9) + 57).ToArray());
            var bozukBilgi = await FfprobeClient.ProbeAsync(bozuk);
            Assert.True(bozukBilgi.StartSeconds > 4, bozukBilgi.StartSeconds.ToString());

            var duz = Yol(adlar[2]);
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[] { "-hide_banner", "-y", "-i", bozuk, "-map", "0", "-c", "copy", duz });
            Assert.True(await CozmeHatasiAsync(duz) > 0);

            var cikti = Yol(adlar[3]);
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var exit = await CliApp.RunAsync(new[]
            {
                "donustur", bozuk, "--onar", "-o", cikti, "--etiket-baslik", "Onarilmis Kayit",
                "--bolum-ekle", "0=Giriş", "--bolum-ekle", "3=Son"
            }, stdout, stderr, CliText.ForLanguage("tr"), new CliServices(), CancellationToken.None);

            Assert.True(exit == ExitCodes.InBand, stderr.ToString());
            Assert.Equal(0, await CozmeHatasiAsync(cikti));
            var onarilan = await FfprobeClient.ProbeAsync(cikti);
            Assert.Equal(0, onarilan.StartSeconds, 2);
            Assert.InRange(onarilan.DurationSeconds, bozukBilgi.DurationSeconds - 0.5, bozukBilgi.DurationSeconds + 0.5);
            Assert.Contains(onarilan.Streams, s => s.Kind == StreamKind.Video);
            Assert.Contains(onarilan.Streams, s => s.Kind == StreamKind.Audio);
            Assert.Equal(new[] { (0d, "Giriş"), (3d, "Son") },
                onarilan.Chapters.Select(c => (Math.Round(c.StartSeconds, 1), c.Title ?? "")).ToArray());
            var (_, baslik, _) = await AkisGirdisi.RunAsync(ToolLocator.Ffprobe, new[] { "-v", "error", "-show_entries", "format_tags=title", "-of", "default=nw=1:nk=1", cikti });
            Assert.Equal("Onarilmis Kayit", baslik.Trim());
            Assert.False(File.Exists(ConversionChapters.MetadataPath(cikti)));
        }
        finally { Kapat(adlar); }
    }
}
