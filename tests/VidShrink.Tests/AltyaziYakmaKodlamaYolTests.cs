using System.Text;
using System.Text.Json;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit.Abstractions;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// <c>\\localhost\&lt;surucu&gt;$</c> yonetim paylasimi aciksa kosar; kapaliysa atlanir, yesil sayilmaz.
/// Gercek ag paylasimi kullanilmaz.
/// </summary>
public sealed class UncFactAttribute : FactAttribute
{
    public UncFactAttribute()
    {
        if (!ToolLocator.IsAvailable(out var missing)) Skip = $"{missing} bulunamadi.";
        else if (!OperatingSystem.IsWindows()) Skip = "UNC yolu yalniz Windows'ta olculur.";
        else if (!Directory.Exists(AltyaziYakmaKodlamaYolTests.Unc(GirdiKanit.Root)))
            Skip = $"{AltyaziYakmaKodlamaYolTests.Unc(GirdiKanit.Root)} acilmiyor (yonetim paylasimi kapali); UNC canli olculmedi.";
    }
}

/// <summary>
/// Dis altyazi yakmanin uc hali: UTF-8 olmayan dosya, UNC/uzun/ayracli yol, libass'siz ffmpeg.
/// Olcum: <c>docs/olcumler/altyazi-yakma-kodlama-yol.md</c>.
/// </summary>
public sealed class AltyaziYakmaKodlamaYolTests(ITestOutputHelper cikti)
{
    private const string Yazi = "ŞĞÜİÖÇ şğıİ MERHABA";
    private const string Srt = "1\n00:00:00,000 --> 00:00:02,000\n" + Yazi + "\n\n";

    private static readonly CliText Tr = CliText.ForLanguage("tr");
    private static readonly CliText En = CliText.ForLanguage("en");
    private static readonly SourceStream Ses = new(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000);

    static AltyaziYakmaKodlamaYolTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private static string Klasor
    {
        get
        {
            var yol = Path.GetFullPath(Path.Combine(GirdiKanit.Root, ".calisma", "altyazi-kodlama-yol"));
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    internal static string Unc(string yol)
    {
        var tam = Path.GetFullPath(yol);
        return "\\\\localhost\\" + tam[0] + "$" + tam[2..];
    }

    private static byte[] Baytlar(string kodlama) => kodlama switch
    {
        "utf8" => new UTF8Encoding(false).GetBytes(Srt),
        "utf8-bom" => new UTF8Encoding(true).GetPreamble().Concat(new UTF8Encoding(false).GetBytes(Srt)).ToArray(),
        "cp1254" => Encoding.GetEncoding(1254).GetBytes(Srt),
        "utf16le-bom" => Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(Srt)).ToArray(),
        "utf16be-bom" => Encoding.BigEndianUnicode.GetPreamble().Concat(Encoding.BigEndianUnicode.GetBytes(Srt)).ToArray(),
        "utf16le" => Encoding.Unicode.GetBytes(Srt),
        "utf16be" => Encoding.BigEndianUnicode.GetBytes(Srt),
        "utf32le-bom" => new UTF32Encoding(false, true).GetPreamble().Concat(new UTF32Encoding(false, false).GetBytes(Srt)).ToArray(),
        _ => throw new ArgumentOutOfRangeException(nameof(kodlama))
    };

    [Theory]
    [InlineData("utf8", SubtitleCharsetKind.Utf8, "UTF-8")]
    [InlineData("utf8-bom", SubtitleCharsetKind.Utf8, "UTF-8")]
    [InlineData("cp1254", SubtitleCharsetKind.Converted, "Windows-1254")]
    [InlineData("utf16le-bom", SubtitleCharsetKind.Converted, "UTF-16LE")]
    [InlineData("utf16be-bom", SubtitleCharsetKind.Converted, "UTF-16BE")]
    [InlineData("utf16le", SubtitleCharsetKind.Converted, "UTF-16LE")]
    [InlineData("utf16be", SubtitleCharsetKind.Converted, "UTF-16BE")]
    [InlineData("utf32le-bom", SubtitleCharsetKind.Converted, "UTF-32LE")]
    public void KodlamaTaniniyorVeYaziAyniCikiyor(string kodlama, SubtitleCharsetKind tur, string ad)
    {
        var okunan = SubtitleCharset.Read(Baytlar(kodlama), 1254);

        Assert.Equal(tur, okunan.Kind);
        Assert.Equal(ad, okunan.Name);
        Assert.Equal(tur == SubtitleCharsetKind.Converted ? Srt : null, okunan.Text);
    }

    [Fact]
    public void KodSayfasiTahminiSistemdenGelirBilinmeyenReddedilir()
    {
        var bati = SubtitleCharset.Read(Baytlar("cp1254"), 1252);
        Assert.Equal(SubtitleCharsetKind.Converted, bati.Kind);
        Assert.Equal("Windows-1252", bati.Name);
        Assert.Contains("ÞÐÜÝÖÇ þðýÝ", bati.Text);
        Assert.DoesNotContain(Yazi, bati.Text);

        Assert.Equal(SubtitleCharsetKind.Unknown, SubtitleCharset.Read(Baytlar("cp1254"), 65001).Kind);
        Assert.Equal(SubtitleCharsetKind.Unknown, SubtitleCharset.Read(Baytlar("cp1254"), 0).Kind);
        Assert.Equal(SubtitleCharsetKind.Unknown, SubtitleCharset.Read(Baytlar("cp1254"), 932).Kind);
        Assert.Equal(SubtitleCharsetKind.Unknown, SubtitleCharset.Read(Baytlar("cp1254"), 99999).Kind);
        Assert.Equal(SubtitleCharsetKind.Unknown, SubtitleCharset.Read(new byte[] { 0x41, 0x00, 0x00, 0x42, 0x43, 0x44, 0x45, 0x46 }, 1254).Kind);
        Assert.Equal(SubtitleCharsetKind.Utf8, SubtitleCharset.Read(Array.Empty<byte>(), 1254).Kind);
    }

    private static string Yaz(string ad, string kodlama)
    {
        var yol = Path.Combine(Klasor, ad);
        File.WriteAllBytes(yol, Baytlar(kodlama));
        return yol;
    }

    private static string Kopyalar => Path.Combine(Klasor, "kopya");

    private static CliServices Servis(int kodSayfasi) => new()
    {
        MissingTool = () => null,
        Availability = () => null,
        HasFilter = _ => true,
        AnsiCodePage = () => kodSayfasi,
        BurnScratchFolder = () => Kopyalar
    };

    private static string[] KopyaDosyalari()
        => Directory.Exists(Kopyalar) ? Directory.GetFiles(Kopyalar) : Array.Empty<string>();

    private static void Topla(params string[] adlar)
    {
        if (Directory.Exists(Kopyalar)) Directory.Delete(Kopyalar, recursive: true);
        KanitKapanisi.Kapat(Klasor, adlar);
    }

    [Theory]
    [InlineData("cp1254", ".srt", "Windows-1254")]
    [InlineData("utf16le-bom", ".SRT", "UTF-16LE")]
    [InlineData("cp1254", ".ass", "Windows-1254")]
    public void Utf8OlmayanDosyaUtf8KopyayaCevrilir(string kodlama, string uzanti, string ad)
    {
        var dosya = Yaz($"cevir-{kodlama}{uzanti}", kodlama);
        var istek = new CliRequest { BurnFile = dosya };
        var kopyalar = new List<string>();

        Assert.Null(CliApp.PrepareBurnFile(ref istek, Servis(1254), kopyalar, out var okunan));

        Assert.Equal(ad, okunan);
        var kopya = Assert.Single(kopyalar);
        Assert.Equal(kopya, istek.BurnFile);
        Assert.Equal(Kopyalar, Path.GetDirectoryName(kopya));
        Assert.StartsWith("vidshrink_altyazi_", Path.GetFileName(kopya), StringComparison.Ordinal);
        Assert.Equal(uzanti.ToLowerInvariant(), Path.GetExtension(kopya));
        Assert.Equal(new UTF8Encoding(false).GetBytes(Srt), File.ReadAllBytes(kopya));
        Assert.Equal(Baytlar(kodlama), File.ReadAllBytes(dosya));

        Topla(Path.GetFileName(dosya));
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8-bom")]
    public void Utf8DosyayaDokunulmaz(string kodlama)
    {
        var dosya = Yaz($"dokunma-{kodlama}.srt", kodlama);
        var istek = new CliRequest { BurnFile = dosya };
        var kopyalar = new List<string>();

        Assert.Null(CliApp.PrepareBurnFile(ref istek, Servis(1254), kopyalar, out var okunan));

        Assert.Null(okunan);
        Assert.Empty(kopyalar);
        Assert.Equal(dosya, istek.BurnFile);
        Assert.Empty(KopyaDosyalari());

        Topla(Path.GetFileName(dosya));
    }

    private static string Zincir(string stdout)
    {
        using var belge = JsonDocument.Parse(stdout);
        var argumanlar = belge.RootElement.GetProperty("arguments").EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
        var i = Array.IndexOf(argumanlar, "-vf");
        return i >= 0 && i + 1 < argumanlar.Length ? argumanlar[i + 1] : "";
    }

    private static async Task<(int Kod, string Stdout, string Stderr)> PlanAsync(string srt, int kodSayfasi, CliText dil)
    {
        var kaynak = Path.Combine(Klasor, "plan-kaynak.mkv");
        File.WriteAllText(kaynak, "x");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var servis = Servis(kodSayfasi);
        var kod = await CliApp.RunAsync(new[] { "plan", kaynak, "--hedef", "25MB", "--olcumsuz", "--json", "--yak-srt", srt },
            stdout, stderr, dil,
            new CliServices
            {
                MissingTool = servis.MissingTool,
                Availability = servis.Availability,
                HasFilter = servis.HasFilter,
                AnsiCodePage = servis.AnsiCodePage,
                BurnScratchFolder = servis.BurnScratchFolder,
                Probe = (_, _) => Task.FromResult(Kaynak(600, Video, Ses) with { FilePath = kaynak })
            },
            CancellationToken.None);
        return (kod, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public async Task KomutKopyayiYakarKodlamayiSoylerKopyayiSiler()
    {
        var cp = Yaz("komut-cp1254.srt", "cp1254");
        var u8 = Yaz("komut-utf8.srt", "utf8");

        var cevrilen = await PlanAsync(cp, 1254, Tr);
        var dokunulmayan = await PlanAsync(u8, 1254, Tr);

        Assert.Equal(ExitCodes.InBand, cevrilen.Kod);
        Assert.Contains("subtitles=filename='" + VideoFilterChain.FilterPath(Path.Combine(Kopyalar, "vidshrink_altyazi_")), Zincir(cevrilen.Stdout));
        Assert.DoesNotContain("komut-cp1254", Zincir(cevrilen.Stdout));
        Assert.Contains(Tr.Format("result.burn-charset", "Windows-1254"), cevrilen.Stderr);
        Assert.Empty(KopyaDosyalari());

        Assert.Equal(ExitCodes.InBand, dokunulmayan.Kod);
        Assert.Contains("subtitles=filename='" + VideoFilterChain.FilterPath(u8) + "'", Zincir(dokunulmayan.Stdout));
        Assert.DoesNotContain("UTF-8", dokunulmayan.Stderr);

        Topla("komut-cp1254.srt", "komut-utf8.srt", "plan-kaynak.mkv");
    }

    [Fact]
    public async Task KodlamasiCikarilamayanDosyaAcikNedenleReddedilir()
    {
        var cp = Yaz("red-cp1254.srt", "cp1254");

        var sonuc = await PlanAsync(cp, 65001, Tr);

        Assert.Equal(ExitCodes.Usage, sonuc.Kod);
        Assert.Contains(Tr.Format("error.burn-file-encoding", Path.GetFullPath(cp)), sonuc.Stderr);
        Assert.Equal("", sonuc.Stdout);
        Assert.Empty(KopyaDosyalari());
        foreach (var dil in new[] { Tr, En })
        {
            Assert.Contains("UTF-8", dil["error.burn-file-encoding"]);
            Assert.Contains("x.srt", dil.Format("error.burn-file-encoding", "x.srt"));
            Assert.Contains("Windows-1254", dil.Format("result.burn-charset", "Windows-1254"));
        }
        Assert.NotEqual(Tr["error.burn-file-encoding"], En["error.burn-file-encoding"]);
        Assert.NotEqual(Tr["result.burn-charset"], En["result.burn-charset"]);

        Topla("red-cp1254.srt", "plan-kaynak.mkv");
    }

    [Theory]
    [InlineData("\\\\sunucu\\paylasim\\alt klasor\\a.srt", "//sunucu/paylasim/alt klasor/a.srt")]
    [InlineData("\\\\localhost\\C$\\Videolar\\a.srt", "//localhost/C$/Videolar/a.srt")]
    [InlineData("\\\\?\\C:\\Videolar\\a.srt", "//?/C\\:/Videolar/a.srt")]
    [InlineData("\\\\?\\UNC\\sunucu\\paylasim\\a.srt", "//?/UNC/sunucu/paylasim/a.srt")]
    [InlineData("C:\\a[1], b; c=d %e #f\\x.srt", "C\\:/a[1], b; c=d %e #f/x.srt")]
    [InlineData("C:\\it's\\'a'.srt", "C\\:/it'\\\\\\''s/'\\\\\\''a'\\\\\\''.srt")]
    [InlineData("D:\\a:b\\c.ass", "D\\:/a\\:b/c.ass")]
    public void YolSuzgecKacisi(string yol, string beklenen)
    {
        Assert.Equal(beklenen, VideoFilterChain.FilterPath(yol));
        Assert.Equal("subtitles=filename='" + beklenen + "'",
            VideoFilterChain.BurnFilter(Kaynak(600, Video, Ses), new VideoFilterOptions { BurnFile = yol }, 0));
    }

    [Fact]
    public void UzunYolKisaltilmadanKacisli()
    {
        var uzun = "C:\\" + string.Join("\\", Enumerable.Repeat("klasor-0123456789012345678901234567890123456789", 7)) + "\\alt.srt";

        var kacisli = VideoFilterChain.FilterPath(uzun);

        Assert.True(uzun.Length > 260, uzun.Length.ToString());
        Assert.Equal(uzun.Length + 1, kacisli.Length);
        Assert.Equal(uzun.Replace('\\', '/').Replace("C:", "C\\:"), kacisli);
    }

    private const string SahteKodlayicilar = "Encoders:\n V..... = Video\n ------\n V....D libx264              libx264 H.264\n";

    private const string LibasssizSuzgecler = """
        Filters:
          T.. = Timeline support
          S.. = Slice threading support
          A = Audio input/output
          V = Video input/output
         ... scale             V->V       Scale the input video size and/or convert the image format.
         T.. overlay           VV->V      Overlay a video source on top of the input.
         ... subtitlesx        V->V       Not the filter.
        """;

    private const string LibassliSuzgecler = LibasssizSuzgecler + """

         ... ass               V->V       Render ASS subtitles onto input video using the libass library.
         ... subtitles         V->V       Render text subtitles onto input video using the libass library.
        """;

    internal static EncoderCapabilities Yoklama(bool libass)
        => EncoderCapabilities.Parse(SahteKodlayicilar, libass ? LibassliSuzgecler : LibasssizSuzgecler, "ffmpeg version test\n");

    [Fact]
    public void YoklamaLibasssizDerlemeyiSuzgecListesindenTanir()
    {
        Assert.True(Yoklama(false).Loaded);
        Assert.True(Yoklama(false).HasFilter("overlay"));
        Assert.False(Yoklama(false).HasFilter(VideoFilterChain.BurnFilterName));
        Assert.True(Yoklama(true).HasFilter(VideoFilterChain.BurnFilterName));
    }

    private static async Task<(int Kod, byte[] Kare, string Hata)> KareAsync(string suzgec)
    {
        var gri = Path.Combine(Klasor, "kare.gray");
        if (File.Exists(gri)) File.Delete(gri);
        var sonuc = await AkisGirdisi.RunAsync(ToolLocator.Ffmpeg, new[]
        {
            "-hide_banner", "-y", "-loglevel", "warning",
            "-f", "lavfi", "-i", "color=c=black:size=320x240:rate=25:duration=2",
            "-threads", "2", "-vf", suzgec, "-ss", "1", "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "gray", gri
        });
        var kare = File.Exists(gri) ? File.ReadAllBytes(gri) : Array.Empty<byte>();
        if (File.Exists(gri)) File.Delete(gri);
        return (sonuc.Code, kare, sonuc.Err);
    }

    private static string Yak(string dosya) => VideoFilterChain.BurnFilter(Kaynak(2, Video), new VideoFilterOptions { BurnFile = dosya }, 0)!;

    private async Task<byte[]> ReferansAsync()
    {
        var u8 = Yaz("referans-utf8.srt", "utf8");
        var (kod, kare, hata) = await KareAsync(Yak(u8));
        Assert.True(kod == 0, hata);
        var parlak = kare.Skip(kare.Length / 2).Count(b => b > 200);
        cikti.WriteLine($"referans: {kare.Length} bayt, alt yarida parlak piksel {parlak}");
        Assert.True(parlak > 50, $"alt yarida parlak piksel {parlak}");
        return kare;
    }

    [FfmpegFact]
    public async Task CanliUtf8OlmayanSrtUtf8IleAyniYakiliyor()
    {
        var referans = await ReferansAsync();

        foreach (var kodlama in new[] { "cp1254", "utf16le-bom", "utf16be-bom" })
        {
            var dosya = Yaz($"canli-{kodlama}.srt", kodlama);

            var ham = await KareAsync(Yak(dosya));
            cikti.WriteLine($"{kodlama} ham: cikis {ham.Kod}, kare {ham.Kare.Length} bayt, {ham.Hata.Split('\n')[0].Trim()}");
            Assert.True(ham.Kod != 0 || !ham.Kare.SequenceEqual(referans), $"{kodlama} cevrilmeden de dogru yakiliyor; cevirme gereksiz");

            var istek = new CliRequest { BurnFile = dosya };
            Assert.Null(CliApp.PrepareBurnFile(ref istek, Servis(1254), new List<string>(), out var okunan));
            var cevrilen = await KareAsync(Yak(istek.BurnFile!));
            cikti.WriteLine($"{kodlama} cevrilen ({okunan}): cikis {cevrilen.Kod}, referansla ayni {cevrilen.Kare.SequenceEqual(referans)}");
            Assert.True(cevrilen.Kod == 0, cevrilen.Hata);
            Assert.True(cevrilen.Kare.SequenceEqual(referans), $"{kodlama} karesi UTF-8 referansindan farkli");
        }

        Topla("referans-utf8.srt", "canli-cp1254.srt", "canli-utf16le-bom.srt", "canli-utf16be-bom.srt");
    }

    [FfmpegFact]
    public async Task CanliUzunVeAyracliYoldanYakiliyor()
    {
        var referans = await ReferansAsync();
        var kok = Path.Combine(Klasor, "uzun [a], b'c; d=e %f #g");
        var klasor = kok;
        while (klasor.Length < 300) klasor = Path.Combine(klasor, "klasor-0123456789012345678901234567890123456789");
        Directory.CreateDirectory(klasor);
        var uzun = Path.Combine(klasor, "alt yazı.srt");
        File.WriteAllBytes(uzun, Baytlar("utf8"));
        Assert.True(uzun.Length > 260, uzun.Length.ToString());

        var duz = await KareAsync(Yak(uzun));
        var onEkli = await KareAsync(Yak("\\\\?\\" + uzun));
        var kisaOnEkli = await KareAsync(Yak("\\\\?\\" + Path.Combine(Klasor, "referans-utf8.srt")));

        cikti.WriteLine($"uzun yol {uzun.Length} karakter: cikis {duz.Kod}; \\\\?\\ on ekli: {onEkli.Kod}; kisa \\\\?\\: {kisaOnEkli.Kod}");
        Assert.True(duz.Kod == 0, duz.Hata);
        Assert.True(duz.Kare.SequenceEqual(referans), "uzun yol karesi referanstan farkli");
        Assert.True(onEkli.Kod == 0, onEkli.Hata);
        Assert.True(onEkli.Kare.SequenceEqual(referans), "on ekli uzun yol karesi referanstan farkli");
        Assert.True(kisaOnEkli.Kod == 0, kisaOnEkli.Hata);
        Assert.True(kisaOnEkli.Kare.SequenceEqual(referans), "on ekli kisa yol karesi referanstan farkli");

        Directory.Delete(kok, recursive: true);
        Topla("referans-utf8.srt");
    }

    [UncFact]
    public async Task CanliUncYolundanYakiliyor()
    {
        var referans = await ReferansAsync();
        var unc = Unc(Path.Combine(Klasor, "referans-utf8.srt"));

        var sonuc = await KareAsync(Yak(unc));

        cikti.WriteLine($"unc {unc}: cikis {sonuc.Kod}");
        Assert.True(sonuc.Kod == 0, sonuc.Hata);
        Assert.True(sonuc.Kare.SequenceEqual(referans), "UNC karesi referanstan farkli");

        Topla("referans-utf8.srt");
    }
}
