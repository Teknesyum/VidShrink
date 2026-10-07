using System.Globalization;
using System.Text.Json;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit.Abstractions;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Resim altyaziyi (PGS, VOBSUB, DVB) goruntuye yakma: <c>--yak N</c> iz resimse <c>subtitles</c>
/// yerine <see cref="VideoFilterChain.OverlayGraph"/> grafigi <c>-filter_complex</c> ile yazilir ve
/// video <c>-map [v]</c> ile alinir. Bindirme kirpma ve dondurmeden sonra, olceklemeden once durur.
/// Canli kol PGS'i elle yazar, VOBSUB ve DVB'yi ondan turetir. Olcum:
/// <c>docs/olcumler/resim-altyazi-yakma.md</c>.
/// </summary>
public sealed class ResimAltyaziYakmaTests(ITestOutputHelper cikti)
{
    private const string Bindirme = "overlay=x=(W-w)/2:y=H-h:eof_action=pass";

    private static readonly CliText Tr = CliText.ForLanguage("tr");
    private static readonly CliText En = CliText.ForLanguage("en");

    private static readonly SourceStream Ses = new(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000);
    private static readonly SourceStream Metin = new(2, StreamKind.Subtitle, "subrip", "eng", Bytes: 1_000);
    private static readonly SourceStream Pgs = new(3, StreamKind.Subtitle, "hdmv_pgs_subtitle", "tur", Bytes: 50_000);
    private static readonly SourceStream Kapak = new(4, StreamKind.Video, "png", IsAttachedPicture: true, Bytes: 600_000);

    private static readonly VideoFilterOptions ResimYak = new() { BurnSubtitle = 1 };

    private static MediaInfo Info() => Kaynak(600, Video, Ses, Metin, Pgs);

    private static string Klasor
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "resim-altyazi-yakma");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static string Graf(IReadOnlyList<string> args)
        => args.ToList().IndexOf("-filter_complex") is var i and >= 0 ? args[i + 1] : "";

    private static List<string> Haritalar(IReadOnlyList<string> args)
        => Enumerable.Range(0, args.Count - 1).Where(i => args[i] == "-map").Select(i => args[i + 1]).ToList();

    private static EncodePlan Plan(MediaInfo info, double hedefMb, VideoFilterOptions filtre, Action<PlanOptions>? ayar = null)
    {
        var options = new PlanOptions { TargetMb = hedefMb, Filters = filtre };
        ayar?.Invoke(options);
        return PlanCalculator.Build(info, options);
    }

    [Fact]
    public void GrafAltyaziyiKareGenisligineOlcekleyipAltaBindiriyor()
    {
        var info = Info();
        var plan = Plan(info, 2, ResimYak);
        var harita = plan.Streams!.VideoMap;
        var sonrasi = VideoFilterChain.Filters(info, plan);

        Assert.Contains(sonrasi, filter => filter.StartsWith("scale=", StringComparison.Ordinal));
        Assert.DoesNotContain(sonrasi, filter => filter.StartsWith("subtitles=", StringComparison.Ordinal));

        var graf = Graf(FfmpegArguments.Build(info, plan, "cikti.mp4", 0, null));
        Assert.Equal($"[0:3]scale=1920:-1[s];[{harita}][s]{Bindirme},{string.Join(',', sonrasi)}[v]", graf);
        Assert.Equal(graf, VideoFilterChain.OverlayGraph(info, plan, harita));
        Assert.DoesNotContain("[b]", graf);
        Assert.Equal("[v]", VideoFilterChain.OverlayOutput);
    }

    [Fact]
    public void KirpmaVeDondurmeBindirmedenOnceOlceklemeSonra()
    {
        var info = Info();
        var filtre = ResimYak with { Crop = new CropRect(1920, 800, 0, 140), Transpose = TransposeMode.Clockwise };
        var plan = Plan(info, 2, filtre);
        var harita = plan.Streams!.VideoMap;

        var graf = Graf(FfmpegArguments.Build(info, plan, "cikti.mp4", 0, null));
        cikti.WriteLine(graf);

        Assert.StartsWith($"[{harita}]crop=1920:800:0:140,transpose=", graf);
        Assert.Contains($"[b];[0:3]scale=800:-1[s];[b][s]{Bindirme}", graf);
        Assert.EndsWith("[v]", graf);
        var bindirme = graf.IndexOf("overlay=", StringComparison.Ordinal);
        Assert.True(graf.IndexOf("crop=", StringComparison.Ordinal) < bindirme);
        Assert.True(graf.IndexOf("transpose=", StringComparison.Ordinal) < bindirme);
        Assert.True(graf.LastIndexOf("scale=", StringComparison.Ordinal) > bindirme, graf);
    }

    [Fact]
    public void OnBitKaynaktaBicimYaziliyorSekizBitteYazilmiyor()
    {
        var sekiz = Info();
        var on = Info() with { BitDepth = 10 };

        var onGraf = Graf(FfmpegArguments.Build(on, Plan(on, 2, ResimYak), "cikti.mp4", 0, null));
        var sekizGraf = Graf(FfmpegArguments.Build(sekiz, Plan(sekiz, 2, ResimYak), "cikti.mp4", 0, null));

        Assert.Contains(Bindirme + ":format=yuv420p10", onGraf);
        Assert.Contains(Bindirme, sekizGraf);
        Assert.DoesNotContain("format=yuv420p10", sekizGraf);
    }

    [Fact]
    public void IkiGecisteAyniGrafVeVideoGraftanEsleniyor()
    {
        var info = Info();
        var plan = Plan(info, 2, ResimYak, o => o.LockedMode = EncodeMode.TwoPass);
        Assert.Equal(EncodeMode.TwoPass, plan.ModeEnum);

        var ilk = FfmpegArguments.Build(info, plan, "cikti.mp4", 1, "gecis");
        var son = FfmpegArguments.Build(info, plan, "cikti.mp4", 2, "gecis");

        Assert.NotEqual("", Graf(ilk));
        Assert.Equal(Graf(ilk), Graf(son));
        foreach (var args in new[] { ilk, son })
        {
            Assert.DoesNotContain("-vf", args);
            Assert.DoesNotContain("-filter:v:0", args);
            Assert.Equal("[v]", Haritalar(args)[0]);
            Assert.DoesNotContain("0:3", Haritalar(args));
            Assert.DoesNotContain(plan.Streams!.VideoMap, Haritalar(args));
        }
        Assert.Contains("0:2", Haritalar(son));
    }

    [Fact]
    public void MetinIzdeGrafYokSuzgecEskiYerinde()
    {
        var info = Info();
        var plan = Plan(info, 2, new VideoFilterOptions { BurnSubtitle = 0 });
        var args = FfmpegArguments.Build(info, plan, "cikti.mp4", 0, null);

        Assert.DoesNotContain("-filter_complex", args);
        Assert.Contains("subtitles=", Sonraki(args, "-vf"));
        Assert.Equal(plan.Streams!.VideoMap, Haritalar(args)[0]);
        Assert.Null(VideoFilterChain.OverlayGraph(info, plan, plan.Streams.VideoMap));
        Assert.Null(VideoFilterChain.ImageBurnStream(info, new VideoFilterOptions { BurnSubtitle = 0 }));
        Assert.Null(VideoFilterChain.ImageBurnStream(info, VideoFilterOptions.Default));
        Assert.Null(VideoFilterChain.ImageBurnStream(info, new VideoFilterOptions { BurnSubtitle = 5 }));
        Assert.Equal(3, VideoFilterChain.ImageBurnStream(info, ResimYak)!.Index);
    }

    [Theory]
    [InlineData("hdmv_pgs_subtitle", true)]
    [InlineData("dvd_subtitle", true)]
    [InlineData("dvb_subtitle", true)]
    [InlineData("subrip", false)]
    public void UcResimKodegiBindirmeyeGidiyor(string kodek, bool resim)
    {
        var info = Kaynak(600, Video, Ses, Metin, Pgs with { Codec = kodek });

        Assert.Equal(resim, VideoFilterChain.ImageBurnStream(info, ResimYak) is not null);
        Assert.Equal(!resim, VideoFilterChain.BurnFilter(info, ResimYak, 0) is not null);
        Assert.Empty(VideoFilterChain.Validate(info, ResimYak));
    }

    [Fact]
    public void NeMetinNeResimOlanIzReddediliyor()
    {
        var info = Kaynak(600, Video, Ses, Metin, Pgs with { Codec = "uydurma_kodek" });

        Assert.Contains("burn: the subtitle is neither text nor image", VideoFilterChain.Validate(info, ResimYak));
        Assert.Null(VideoFilterChain.ImageBurnStream(info, ResimYak));
    }

    [Fact]
    public void KapakliCiktidaVideoGraftanKapakKaynaktan()
    {
        var info = Kaynak(600, Video, Ses, Metin, Pgs, Kapak);
        var plan = Plan(info, 25, ResimYak);
        Assert.NotNull(plan.Streams!.CoverMap);

        var args = FfmpegArguments.Build(info, plan, "cikti.mp4", 2, "log");

        Assert.StartsWith("[0:3]scale=1920:-1[s];", Graf(args));
        Assert.DoesNotContain("-filter:v:0", args);
        Assert.DoesNotContain("-vf", args);
        Assert.Equal("[v]", Haritalar(args)[0]);
        Assert.Contains(plan.Streams.CoverMap, Haritalar(args));
        Assert.Contains("-c:v:0", args);
    }

    [Fact]
    public void KareHiziKipiBindirmeyleBirlikteDuruyor()
    {
        var info = Info();
        var sabit = Plan(info, 2, ResimYak, o => o.FrameRate = FrameRateMode.Constant);
        var tavanli = Plan(info, 2, ResimYak, o => { o.FrameRate = FrameRateMode.Peak; o.MaxFps = 12; });

        var sabitArgs = FfmpegArguments.Build(info, sabit, "cikti.mp4", 0, null);
        var tavanArgs = FfmpegArguments.Build(info, tavanli, "cikti.mp4", 0, null);

        Assert.Contains(Bindirme, Graf(sabitArgs));
        Assert.Equal("cfr", Sonraki(sabitArgs, "-fps_mode"));
        Assert.Contains(Bindirme, Graf(tavanArgs));
        Assert.Equal("vfr", Sonraki(tavanArgs, "-fps_mode"));
        Assert.StartsWith("1/", Sonraki(tavanArgs, "-enc_time_base"));
        Assert.DoesNotContain(",fps=", Graf(tavanArgs));
    }

    [Fact]
    public void YakmaKopyayiKesiyorIzYumusakCikmiyorNotDusmuyor()
    {
        var kucuk = Kaynak(600, Video, Ses, Pgs with { Index = 2 }) with { FileSizeBytes = 5_000_000 };
        var yakan = PlanCalculator.Build(kucuk, new PlanOptions { TargetMb = 25, Filters = new VideoFilterOptions { BurnSubtitle = 0 } });
        var yakmayan = PlanCalculator.Build(kucuk, new PlanOptions { TargetMb = 25 });

        Assert.Equal(EncodeMode.PassThrough, yakmayan.ModeEnum);
        Assert.NotEqual(EncodeMode.PassThrough, yakan.ModeEnum);
        Assert.Equal(0, yakan.Streams!.Request.BurnedSubtitle);
        Assert.Empty(yakan.Streams.Subtitles);
        Assert.DoesNotContain(StreamNote.ImageSubtitleDropped, yakan.Streams.Notes);

        var mp4 = StreamMapping.Decide(kucuk, StreamRequest.Default, OutputContainer.Mp4, 160, null, "aac", true, 100);
        Assert.Contains(StreamNote.ImageSubtitleDropped, mp4.Notes);
        Assert.Empty(mp4.Subtitles);
    }

    [Theory]
    [InlineData("2", false, ExitCodes.InBand, false, false)]
    [InlineData("1", false, ExitCodes.Error, true, true)]
    [InlineData("1", true, ExitCodes.InBand, false, true)]
    public async Task ResimIzdeLibassSorulmuyor(string iz, bool suzgecVar, int beklenen, bool ileti, bool soruldu)
    {
        var kaynak = Path.Combine(Klasor, $"libass-{iz}-{suzgecVar}.mkv");
        File.WriteAllText(kaynak, "x");
        try
        {
            var sorulan = new List<string>();
            var stderr = new StringWriter();

            var kod = await CliApp.RunAsync(new[] { "plan", kaynak, "--hedef", "25MB", "--olcumsuz", "--yak", iz }, new StringWriter(), stderr, Tr,
                new CliServices
                {
                    MissingTool = () => null,
                    Probe = (_, _) => Task.FromResult(Info() with { FilePath = kaynak }),
                    Availability = () => null,
                    HasFilter = ad =>
                    {
                        sorulan.Add(ad);
                        return suzgecVar;
                    }
                },
                CancellationToken.None);

            Assert.Equal(beklenen, kod);
            Assert.Equal(ileti, stderr.ToString().Contains(Tr["error.no-libass"], StringComparison.Ordinal));
            Assert.Equal(soruldu ? new[] { "subtitles" } : Array.Empty<string>(), sorulan);
        }
        finally
        {
            KanitKapanisi.Kapat(Klasor, Path.GetFileName(kaynak));
        }
    }

    [Fact]
    public void CliResimIziKabulEdiyorOlmayanIziReddediyor()
    {
        static CliRequest Istek(string iz) => CliParser.Parse(new[] { "kucult", "a.mkv", "--hedef", "25", "--yak", iz }).Request!;

        Assert.Null(Istek("2").ResolvedSubtitles(Info(), out _, out _));
        Assert.Equal(1, Istek("2").ToPlanOptions(10).Filters.BurnSubtitle);
        Assert.Equal("error.bad-burn", Istek("3").ResolvedSubtitles(Info(), out _, out var arguman));
        Assert.Equal("3", arguman);
    }

    [Fact]
    public void NotVeYardimYakmayiOneriyor()
    {
        foreach (var anahtar in new[] { "plan.stream.image-subtitle-dropped", "plan.stream.image-subtitle-dropped-by-container" })
        {
            Assert.Contains("--yak N", Tr[anahtar]);
            Assert.Contains("--burn N", En[anahtar]);
        }
        foreach (var dil in new[] { Tr, En })
        {
            Assert.Contains("PGS, VOBSUB, DVB", dil["help"]);
            Assert.Contains("PGS, VOBSUB, DVB", dil.Format("error.bad-burn", "7"));
            Assert.Contains("7", dil.Format("error.bad-burn", "7"));
        }
    }

    private static byte[] U16(int value) => new[] { (byte)(value >> 8), (byte)value };

    private static byte[] PgsBaytlari(int tuvalEn, int tuvalBoy, int x, int y, int en, int boy, double bas, double bit)
    {
        var ms = new MemoryStream();
        void Parca(double saniye, byte tur, params byte[][] parcalar)
        {
            var veri = parcalar.SelectMany(parca => parca).ToArray();
            var pts = (uint)Math.Round(saniye * 90000);
            ms.Write(new byte[] { 0x50, 0x47, (byte)(pts >> 24), (byte)(pts >> 16), (byte)(pts >> 8), (byte)pts, 0, 0, 0, 0, tur });
            ms.Write(U16(veri.Length));
            ms.Write(veri);
        }

        var satir = new byte[] { 0, (byte)(0xC0 | (en >> 8)), (byte)en, 1, 0, 0 };
        var rle = Enumerable.Repeat(satir, boy).SelectMany(parca => parca).ToArray();
        var uzunluk = rle.Length + 4;
        var pencere = new[] { new byte[] { 1, 0 }, U16(x), U16(y), U16(en), U16(boy) };

        Parca(bas, 0x16, U16(tuvalEn), U16(tuvalBoy), new byte[] { 0x10, 0, 0, 0x80, 0, 0, 1, 0, 0, 0, 0 }, U16(x), U16(y));
        Parca(bas, 0x17, pencere);
        Parca(bas, 0x14, new byte[] { 0, 0, 1, 235, 128, 128, 255 });
        Parca(bas, 0x15, new byte[] { 0, 0, 0, 0xC0, (byte)(uzunluk >> 16), (byte)(uzunluk >> 8), (byte)uzunluk }, U16(en), U16(boy), rle);
        Parca(bas, 0x80);
        Parca(bit, 0x16, U16(tuvalEn), U16(tuvalBoy), new byte[] { 0x10, 0, 1, 0, 0, 0, 0 });
        Parca(bit, 0x17, pencere);
        Parca(bit, 0x80);
        return ms.ToArray();
    }

    /// <summary>
    /// Siyah 320x240 video ve tek PGS izi. Kutu tuvalin alt ortasinda (320x240 tuvalde x 128-191,
    /// y 200-215), beyaz. <c>.sup</c> ayri girdi olunca damgasi sifira cekiliyor; <c>-itsoffset</c>
    /// ilk olayi yerine koyar.
    /// </summary>
    private static async Task<string> PgsliAsync(string ad, int sure, double bas, double bit, int kat = 1)
    {
        var sup = Path.Combine(Klasor, Path.GetFileNameWithoutExtension(ad) + ".sup");
        File.WriteAllBytes(sup, PgsBaytlari(320 * kat, 240 * kat, 128 * kat, 200 * kat, 64 * kat, 16 * kat, bas, bit));
        var yol = Path.Combine(Klasor, ad);
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y",
            "-f", "lavfi", "-i", $"color=c=black:size=320x240:rate=25:duration={sure}",
            "-itsoffset", bas.ToString("0.###", CultureInfo.InvariantCulture), "-i", sup,
            "-map", "0", "-map", "1", "-threads", "2", "-c:v", "libx264", "-preset", "ultrafast", "-g", "25",
            "-c:s", "copy", yol
        });
        return yol;
    }

    private static async Task<(MediaInfo Info, EncodePlan Plan, List<string> Args)> YakAsync(string kaynak, string hedef, Action<PlanOptions> ayar)
    {
        var info = await FfprobeClient.ProbeAsync(kaynak);
        var options = new PlanOptions
        {
            TargetMb = 1.5,
            LockedCodec = "libx264",
            LockedMode = EncodeMode.Crf,
            LockedCrf = 30,
            LockedPreset = "ultrafast"
        };
        ayar(options);
        var plan = PlanCalculator.Build(info, options);
        Assert.NotEqual(EncodeMode.PassThrough, plan.ModeEnum);
        var args = FfmpegArguments.Build(info, plan, hedef, 0, null).ToList();
        args.InsertRange(args.Count - 1, new[] { "-threads", "2" });
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, args);
        return (info, plan, args);
    }

    private static async Task<byte[]> KareAsync(string yol, double saniye)
    {
        var gri = yol + ".gray";
        await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-ss", saniye.ToString("0.###", CultureInfo.InvariantCulture), "-i", yol,
            "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "gray", gri
        });
        var baytlar = File.ReadAllBytes(gri);
        File.Delete(gri);
        Assert.NotEmpty(baytlar);
        return baytlar;
    }

    private static (int Ust, int Alt) Yarilar(byte[] kare)
        => (kare.Take(kare.Length / 2).Max(), kare.Skip(kare.Length / 2).Max());

    private static (int Sayi, int Sol, int Sag, int Tepe, int Dip) Kutu(byte[] kare, int en)
    {
        var parlak = Enumerable.Range(0, kare.Length).Where(i => kare[i] > 128).ToList();
        if (parlak.Count == 0) return (0, -1, -1, -1, -1);
        return (parlak.Count, parlak.Min(i => i % en), parlak.Max(i => i % en), parlak.Min(i => i / en), parlak.Max(i => i / en));
    }

    private static async Task<(int Paket, double Sure)> PaketlerAsync(string yol)
    {
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffprobe, new[]
        {
            "-v", "error", "-select_streams", "v:0", "-count_packets",
            "-show_entries", "stream=nb_read_packets:format=duration", "-of", "json", yol
        });
        Assert.Equal(0, sonuc.Code);
        using var doc = JsonDocument.Parse(sonuc.Out);
        var paket = int.Parse(doc.RootElement.GetProperty("streams")[0].GetProperty("nb_read_packets").GetString()!, CultureInfo.InvariantCulture);
        var sure = double.Parse(doc.RootElement.GetProperty("format").GetProperty("duration").GetString()!, CultureInfo.InvariantCulture);
        return (paket, sure);
    }

    private static void Yakin(int beklenen, int olculen, string ad)
        => Assert.True(Math.Abs(beklenen - olculen) <= 2, $"{ad}: beklenen {beklenen}, olculen {olculen}");

    private static void Topla(string onEk) => KanitKapanisi.Kapat(Klasor, onEk + "*");

    [FfmpegTheory]
    [InlineData("pgs", "hdmv_pgs_subtitle", null)]
    [InlineData("dvd", "dvd_subtitle", "dvdsub")]
    [InlineData("dvb", "dvb_subtitle", "dvbsub")]
    public async Task CanliUcResimKodegiYakiliyor(string ad, string kodek, string? kodlayici)
    {
        var kaynak = await PgsliAsync($"uc-{ad}-pgs.mkv", 2, 0.2, 1.8);
        if (kodlayici is not null)
        {
            var turev = Path.Combine(Klasor, $"uc-{ad}-kaynak.mkv");
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, new[]
            {
                "-hide_banner", "-y", "-i", kaynak, "-map", "0", "-threads", "2", "-c:v", "copy", "-c:s", kodlayici, turev
            });
            kaynak = turev;
        }
        var yanik = Path.Combine(Klasor, $"uc-{ad}-yanik.mp4");
        var yaniksiz = Path.Combine(Klasor, $"uc-{ad}-yok.mp4");

        var (info, plan, args) = await YakAsync(kaynak, yanik, o => o.Filters = new VideoFilterOptions { BurnSubtitle = 0 });
        var (_, bosPlan, bosArgs) = await YakAsync(kaynak, yaniksiz, _ => { });

        Assert.Equal(kodek, info.Streams.Single(stream => stream.Kind == StreamKind.Subtitle).Codec);
        Assert.Contains(Bindirme, Graf(args));
        Assert.DoesNotContain("-vf", args);
        Assert.DoesNotContain("-filter_complex", bosArgs);
        Assert.Contains(StreamNote.ImageSubtitleDropped, bosPlan.Streams!.Notes);
        Assert.DoesNotContain(StreamNote.ImageSubtitleDropped, plan.Streams!.Notes);

        var kare = await KareAsync(yanik, 1);
        var (ust, alt) = Yarilar(kare);
        var kutu = Kutu(kare, 320);
        var (bosUst, bosAlt) = Yarilar(await KareAsync(yaniksiz, 1));
        var (paket, sure) = await PaketlerAsync(yanik);
        cikti.WriteLine($"{ad}: ust {ust} alt {alt}, kutu {kutu}, yakmasiz {bosUst}/{bosAlt}, paket {paket}, sure {sure}");

        Assert.True(alt > 200, $"alt yari {alt}");
        Assert.True(ust < 60, $"ust yari {ust}");
        Assert.True(bosUst < 60 && bosAlt < 60, $"yakmasiz {bosUst}/{bosAlt}");
        Yakin(128, kutu.Sol, "sol");
        Yakin(191, kutu.Sag, "sag");
        Yakin(200, kutu.Tepe, "tepe");
        Yakin(215, kutu.Dip, "dip");
        Assert.Equal(50, paket);
        Assert.InRange(sure, 1.9, 2.1);
        Assert.DoesNotContain(await AkislarAsync(yanik), stream => Alan(stream, "codec_type") == "subtitle");
        Assert.DoesNotContain(await AkislarAsync(yaniksiz), stream => Alan(stream, "codec_type") == "subtitle");

        Topla($"uc-{ad}-");
    }

    [FfmpegFact]
    public async Task CanliBuyukTuvalKareyeOlcekleniyor()
    {
        var kaynak = await PgsliAsync("tuval-kaynak.mkv", 2, 0.2, 1.8, kat: 2);
        var yanik = Path.Combine(Klasor, "tuval-yanik.mp4");

        var (_, _, args) = await YakAsync(kaynak, yanik, o => o.Filters = new VideoFilterOptions { BurnSubtitle = 0 });

        Assert.Contains("scale=320:-1[s]", Graf(args));
        var kutu = Kutu(await KareAsync(yanik, 1), 320);
        cikti.WriteLine($"640x480 tuval, 320x240 kare: kutu {kutu}");
        Yakin(128, kutu.Sol, "sol");
        Yakin(191, kutu.Sag, "sag");
        Yakin(200, kutu.Tepe, "tepe");
        Yakin(215, kutu.Dip, "dip");

        Topla("tuval-");
    }

    [FfmpegFact]
    public async Task CanliKirpmadaAltyaziAltKenaraOturuyor()
    {
        var kaynak = await PgsliAsync("kirp-kaynak.mkv", 2, 0.2, 1.8);
        var yanik = Path.Combine(Klasor, "kirp-yanik.mp4");

        var (_, plan, args) = await YakAsync(kaynak, yanik, o => o.Filters = new VideoFilterOptions
        {
            BurnSubtitle = 0,
            Crop = new CropRect(320, 160, 0, 40)
        });

        Assert.Contains("crop=320:160:0:40[b];", Graf(args));
        Assert.Equal((320, 160), (plan.Width, plan.Height));
        var kare = await KareAsync(yanik, 1);
        Assert.Equal(320 * 160, kare.Length);
        var kutu = Kutu(kare, 320);
        cikti.WriteLine($"kirpma 320x160: kutu {kutu}");
        Yakin(128, kutu.Sol, "sol");
        Yakin(191, kutu.Sag, "sag");
        Yakin(120, kutu.Tepe, "tepe");
        Yakin(135, kutu.Dip, "dip");

        Topla("kirp-");
    }

    [FfmpegFact]
    public async Task CanliIkiGecisteYakiliyor()
    {
        var kaynak = await PgsliAsync("iki-kaynak.mkv", 2, 0.2, 1.8);
        var yanik = Path.Combine(Klasor, "iki-yanik.mp4");
        var gunluk = Path.Combine(Klasor, "iki-gecis");
        var info = await FfprobeClient.ProbeAsync(kaynak);
        var plan = PlanCalculator.Build(info, new PlanOptions
        {
            TargetMb = 0.05,
            LockedCodec = "libx264",
            LockedMode = EncodeMode.TwoPass,
            LockedPreset = "ultrafast",
            Filters = new VideoFilterOptions { BurnSubtitle = 0 }
        });
        Assert.Equal(EncodeMode.TwoPass, plan.ModeEnum);

        foreach (var gecis in new[] { 1, 2 })
        {
            var args = FfmpegArguments.Build(info, plan, yanik, gecis, gunluk).ToList();
            args.InsertRange(args.Count - 1, new[] { "-threads", "2" });
            Assert.Contains(Bindirme, Graf(args));
            await AkisGirdisi.RunOrThrowAsync(ToolLocator.Ffmpeg, args);
        }

        var (ust, alt) = Yarilar(await KareAsync(yanik, 1));
        var (paket, sure) = await PaketlerAsync(yanik);
        cikti.WriteLine($"iki gecis: ust {ust} alt {alt}, paket {paket}, sure {sure}");
        Assert.True(alt > 200, $"alt yari {alt}");
        Assert.True(ust < 60, $"ust yari {ust}");
        Assert.Equal(50, paket);

        Topla("iki-");
    }

    [FfmpegFact]
    public async Task CanliKesitteAltyaziKaynaginSaatiyleGeliyor()
    {
        var kaynak = await PgsliAsync("kesit-kaynak.mkv", 14, 12, 13.5);
        var yanik = Path.Combine(Klasor, "kesit-yanik.mp4");

        await YakAsync(kaynak, yanik, o =>
        {
            o.Filters = new VideoFilterOptions { BurnSubtitle = 0 };
            o.Trim = TrimWindow.Of(11.5, 14, 14);
        });

        var once = Yarilar(await KareAsync(yanik, 0.2)).Alt;
        var icinde = Yarilar(await KareAsync(yanik, 1.2)).Alt;
        var sonra = Yarilar(await KareAsync(yanik, 2.3)).Alt;
        var (paket, sure) = await PaketlerAsync(yanik);
        cikti.WriteLine($"kesit 11,5-14, altyazi 12-13,5: 0,2 sn {once}, 1,2 sn {icinde}, 2,3 sn {sonra}, paket {paket}, sure {sure}");
        Assert.True(once < 60, $"altyazidan once {once}");
        Assert.True(icinde > 200, $"altyazi icinde {icinde}");
        Assert.True(sonra < 60, $"altyazidan sonra {sonra}");
        Assert.InRange(sure, 2.4, 2.6);

        Topla("kesit-");
    }

    [FfmpegFact]
    public async Task CanliMp4ResimAltyaziyiKopyaylaTasimiyor()
    {
        var kaynak = await PgsliAsync("kap-kaynak.mkv", 2, 0.2, 1.8);
        string[] Kopya(string hedef) => new[] { "-hide_banner", "-y", "-i", kaynak, "-map", "0", "-c", "copy", Path.Combine(Klasor, hedef) };

        var mp4 = await AkisGirdisi.RunAsync(ToolLocator.Ffmpeg, Kopya("kap-kopya.mp4"));
        var mkv = await AkisGirdisi.RunAsync(ToolLocator.Ffmpeg, Kopya("kap-kopya.mkv"));

        cikti.WriteLine($"mp4 kopya kod {mp4.Code}: {mp4.Err.Trim().Split('\n')[^1].Trim()}");
        Assert.NotEqual(0, mp4.Code);
        Assert.Equal(0, mkv.Code);
        Assert.Contains(await AkislarAsync(Path.Combine(Klasor, "kap-kopya.mkv")), stream => Alan(stream, "codec_name") == "hdmv_pgs_subtitle");

        Topla("kap-");
    }
}
