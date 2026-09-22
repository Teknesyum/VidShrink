using System.Text.Json;
using VidShrink.Cli;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// C1-8: <c>--profil-dosyasi</c> VidShrink ya da HandBrake ön ayar dosyasını okuyor.
/// Dosyadaki profiller kütüphaneden önce aranıyor, kullanıcının kitaplığına yazılmıyor.
/// HandBrake özeti stderr'de, <c>--json</c> çıktısı temiz kalıyor.
/// </summary>
public sealed class CliOnAyarDosyasiTests : IDisposable
{
    private readonly string _klasor = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        ".calisma", "test-ciktilari", "c1-8-profil-dosyasi", Guid.NewGuid().ToString("N")[..8]);

    public CliOnAyarDosyasiTests() => Directory.CreateDirectory(_klasor);

    public void Dispose()
    {
        try { Directory.Delete(Path.GetFullPath(_klasor), true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static MediaInfo Kaynak() => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 400L * 1024 * 1024,
        DurationSeconds = 600,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 5_000_000,
        AudioCodec = "aac",
        AudioBitrateBps = 128_000,
        AudioChannels = 2,
        Streams = new[]
        {
            new SourceStream(0, StreamKind.Video, "h264"),
            new SourceStream(1, StreamKind.Audio, "aac", Channels: 2),
        },
    };

    private sealed record Kosum(int Exit, string Stdout, string Stderr);

    private async Task<Kosum> Plan(IReadOnlyList<PresetProfile> kullanici, params string[] ekler)
    {
        var servisler = new CliServices
        {
            MissingTool = () => null,
            Probe = (_, _) => Task.FromResult(Kaynak()),
            Availability = () => null,
            UserPresets = () => kullanici,
        };
        var dosya = Path.Combine(_klasor, "kaynak.mp4");
        await File.WriteAllTextAsync(dosya, "x");
        var args = new List<string> { "plan", dosya, "--olcumsuz", "--json" };
        args.AddRange(ekler);
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await CliApp.RunAsync(args, stdout, stderr, CliText.ForLanguage("tr"), servisler, CancellationToken.None);
        return new Kosum(exit, stdout.ToString(), stderr.ToString());
    }

    private static double HedefMb(string stdout)
    {
        using var belge = JsonDocument.Parse(stdout);
        return belge.RootElement.GetProperty("targetMb").GetDouble();
    }

    private string VidShrinkDosyasi(params PresetProfile[] profiller)
    {
        var path = Path.Combine(_klasor, "onayar.json");
        PresetLibrary.Export(profiller, path);
        return path;
    }

    private static PresetProfile Profil(string id, double mb) =>
        new() { Id = id, Name = id + " adı", TargetMb = mb, Kind = PresetKind.User };

    /// <summary>Tek profilli VidShrink dosyası <c>--profil</c> olmadan uygulanıyor.</summary>
    [Fact]
    public async Task TekProfilliDosyaKimliksizUygulaniyor()
    {
        var kosum = await Plan(Array.Empty<PresetProfile>(), "--profil-dosyasi", VidShrinkDosyasi(Profil("tek", 17)));

        Assert.Equal(0, kosum.Exit);
        Assert.Equal(17, HedefMb(kosum.Stdout));
    }

    /// <summary>
    /// Dosyadaki profil kullanıcı kitaplığındaki aynı kimliğin önüne geçiyor; dosyasız koşumda
    /// kitaplıktaki seçiliyor (olumsuz kontrol).
    /// </summary>
    [Fact]
    public async Task DosyaKitapliginOnundeAraniyor()
    {
        var kitaplik = new[] { Profil("ortak", 30) };
        var dosyali = await Plan(kitaplik, "--profil-dosyasi", VidShrinkDosyasi(Profil("ortak", 12)), "--profil", "ortak");
        var dosyasiz = await Plan(kitaplik, "--profil", "ortak");

        Assert.Equal(12, HedefMb(dosyali.Stdout));
        Assert.Equal(30, HedefMb(dosyasiz.Stdout));
    }

    /// <summary>Çok profilli dosyada seçim zorunlu: 64 ve adlar cümlede. Ad ile seçim çalışıyor.</summary>
    [Fact]
    public async Task CokProfildeSecimZorunluAdlaSecilebiliyor()
    {
        var dosya = VidShrinkDosyasi(Profil("bir", 8), Profil("iki", 16));

        var secimsiz = await Plan(Array.Empty<PresetProfile>(), "--profil-dosyasi", dosya);
        var adla = await Plan(Array.Empty<PresetProfile>(), "--profil-dosyasi", dosya, "--profil", "IKI adı");

        Assert.Equal(ExitCodes.Usage, secimsiz.Exit);
        Assert.Contains("bir adı", secimsiz.Stderr, StringComparison.Ordinal);
        Assert.Contains("iki adı", secimsiz.Stderr, StringComparison.Ordinal);
        Assert.Equal(0, adla.Exit);
        Assert.Equal(16, HedefMb(adla.Stdout));
    }

    /// <summary>
    /// HandBrake dosyası çevriliyor: hedef plana iniyor, özet stderr'de, stdout yalnız JSON.
    /// WebM kabı yaklaşık alanlar arasında adıyla yazılıyor; mp4'te yazılmıyor (olumsuz kontrol).
    /// </summary>
    [Theory]
    [InlineData("av_webm", true)]
    [InlineData("av_mp4", false)]
    public async Task HandBrakeDosyasiCevriliyorOzetStderrde(string fileFormat, bool yaklasikKap)
    {
        var dosya = Path.Combine(_klasor, "hb.json");
        File.WriteAllText(dosya, File.ReadAllText(HandBrakeOnAyarCeviriTests.FixturePath)
            .Replace("\"av_mp4\"", $"\"{fileFormat}\"", StringComparison.Ordinal));
        var ceviri = Assert.Single(HandBrakePresetImport.TranslateFile(dosya));
        var alanlar = ceviri.Notes.Where(n => n.Reason != PresetNoteReason.Structural).ToList();

        var kosum = await Plan(Array.Empty<PresetProfile>(), "--profil-dosyasi", dosya);

        Assert.Equal(0, kosum.Exit);
        Assert.Equal(ceviri.Profile.TargetMb, HedefMb(kosum.Stdout));
        var ozet = CliText.ForLanguage("tr").Format("preset-file.handbrake", ceviri.PresetName,
            alanlar.Count(n => n.Outcome == PresetNoteOutcome.Carried),
            alanlar.Count(n => n.Outcome == PresetNoteOutcome.Approximated),
            alanlar.Count(n => n.Outcome == PresetNoteOutcome.Dropped));
        Assert.Contains(ozet, kosum.Stderr, StringComparison.Ordinal);
        Assert.DoesNotContain(ceviri.PresetName, kosum.Stdout, StringComparison.Ordinal);
        var yaklasikSatir = kosum.Stderr.Split('\n').Single(s => s.Contains("Yaklaşık:", StringComparison.Ordinal));
        Assert.Equal(yaklasikKap, yaklasikSatir.Contains("FileFormat", StringComparison.Ordinal));
    }

    /// <summary>Bozuk ve olmayan dosya 1 veriyor, dosya yolu ve sebep cümlede.</summary>
    [Theory]
    [InlineData("{ bu json değil", "JSON değil")]
    [InlineData("{\"baska\":1}", "ön ayarı yok")]
    [InlineData(null, "onayar.json")]
    public async Task OkunamayanDosyaGirdiHatasi(string? icerik, string sebep)
    {
        var dosya = Path.Combine(_klasor, "onayar.json");
        if (icerik is not null) File.WriteAllText(dosya, icerik);

        var kosum = await Plan(Array.Empty<PresetProfile>(), "--profil-dosyasi", dosya);

        Assert.Equal(ExitCodes.Error, kosum.Exit);
        Assert.Contains("onayar.json", kosum.Stderr, StringComparison.Ordinal);
        Assert.Contains(sebep, kosum.Stderr, StringComparison.Ordinal);
    }

    /// <summary>Dosya verilince <c>--hedef</c> zorunlu değil; ayrıştırma aşamasında reddedilmiyor.</summary>
    [Fact]
    public void DosyaVerilinceHedefZorunluDegil()
    {
        Assert.True(CliParser.Parse(new[] { "plan", "a.mp4", "--profil-dosyasi", "x.json" }).Ok);
        Assert.False(CliParser.Parse(new[] { "plan", "a.mp4" }).Ok);
    }

    /// <summary>Yeni metin anahtarları iki CLI dilinde de var; yardım bayrağı anlatıyor.</summary>
    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    public void MetinlerIkiDildeVar(string dil)
    {
        var metin = CliText.ForLanguage(dil);
        foreach (var key in new[]
                 {
                     "error.preset-file", "error.preset-file-choose", "preset-file.handbrake", "preset-file.approximated",
                     "preset-file.reason.not-json", "preset-file.reason.too-new", "preset-file.reason.not-preset",
                     "preset-file.reason.invalid"
                 })
            Assert.False(string.IsNullOrWhiteSpace(metin[key]) || metin[key] == key, $"{dil}: {key}");
        Assert.Contains("--profil-dosyasi", metin["help"], StringComparison.Ordinal);
    }
}
