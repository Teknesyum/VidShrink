using System.Diagnostics;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// SIK-P6, SIK-18 ve SIK-11: kodlanan dosya asıl ada taşınmadan önce okunur; kısa ya da çözülemeyen
/// çıktı hedefteki eski dosyayı ezmez. Ayar açıksa kaynağın tarihleri çıktıya geçer. ffmpeg'li kollar
/// 3 sn'lik 160x120 klip ve <c>-threads 2</c> ile koşar.
/// </summary>
public sealed class CiktiDogrulamaTests : IDisposable
{
    private const string EskiIcerik = "eski teslim";

    private readonly string klasor = Path.Combine(AppContext.BaseDirectory, ".calisma", "cikti-dogrulama-" + Guid.NewGuid().ToString("N"));

    public CiktiDogrulamaTests() => Directory.CreateDirectory(klasor);

    public void Dispose()
    {
        try { Directory.Delete(klasor, recursive: true); } catch (IOException) { }
    }

    private static MediaInfo Kaynak(string yol, double sure) => new()
    {
        FilePath = yol,
        FileSizeBytes = 2_000_000,
        DurationSeconds = sure,
        Width = 160,
        Height = 120,
        Fps = 10,
        VideoCodec = "h264",
        TotalBitrateBps = 500_000
    };

    private static EncodePlan Plan() => new()
    {
        Codec = "libx264",
        Mode = "crf",
        Crf = 30,
        VideoBitrateK = 200,
        AudioCodec = null,
        AudioBitrateK = 0,
        Width = 160,
        Height = 120,
        Fps = 10,
        Preset = "ultrafast"
    };

    private string EskiHedef()
    {
        var hedef = Path.Combine(klasor, "cikti.mp4");
        File.WriteAllText(hedef, EskiIcerik);
        return hedef;
    }

    private string[] Kismiler() => Directory.GetFiles(klasor, "vidshrink_partial_*");

    [Theory]
    [InlineData(10.0, 10.0, false, false)]
    [InlineData(10.0, 9.2, false, false)]
    [InlineData(10.0, 8.5, true, false)]
    [InlineData(100.0, 96.0, false, false)]
    [InlineData(100.0, 94.0, true, false)]
    [InlineData(10.0, null, true, true)]
    [InlineData(10.0, 0.0, true, true)]
    [InlineData(0.0, 3.0, false, false)]
    [InlineData(0.0, null, true, true)]
    public void KisaYaDaCozulemeyenCiktiKusurlu(double beklenen, double? okunan, bool kusurlu, bool cozulemedi)
    {
        var kusur = CiktiDogrulama.Denetle(beklenen, okunan);

        Assert.Equal(kusurlu, kusur is not null);
        if (kusur is not null) Assert.Equal(cozulemedi, kusur.Cozulemedi);
    }

    /// <summary>Okuyucu kısa süre bildirince iş düşer, eski hedef aynen kalır, kısmi dosya kalmaz.</summary>
    [Fact]
    public async Task KisaCiktiHedefiEzmez()
    {
        var hedef = EskiHedef();
        var runner = new EncodeRunner
        {
            SahteKodlayici = (_, yol, _) => File.WriteAllBytesAsync(yol, new byte[50_000]),
            SureOkuyucu = (_, _) => Task.FromResult<double?>(1.0)
        };

        var sonuc = await runner.RunAsync(Kaynak("kaynak.mp4", 3), Plan(), hedef, 5, null);

        Assert.False(sonuc.Success);
        Assert.NotNull(sonuc.Kusur);
        Assert.Equal(1.0, sonuc.Kusur!.OkunanSaniye);
        Assert.Equal(hedef, sonuc.OutputPath);
        Assert.Equal(EskiIcerik, File.ReadAllText(hedef));
        Assert.Empty(Kismiler());
    }

    /// <summary>Olumsuz kontrol: okuyucu tam süre bildirince iş teslim edilir ve eski hedef yenisiyle değişir.</summary>
    [Fact]
    public async Task TamCiktiTeslimEdilir()
    {
        var hedef = EskiHedef();
        var runner = new EncodeRunner
        {
            SahteKodlayici = (_, yol, _) => File.WriteAllBytesAsync(yol, new byte[50_000]),
            SureOkuyucu = (_, _) => Task.FromResult<double?>(3.0)
        };

        var sonuc = await runner.RunAsync(Kaynak("kaynak.mp4", 3), Plan(), hedef, 5, null);

        Assert.True(sonuc.Success);
        Assert.Null(sonuc.Kusur);
        Assert.Equal(50_000, new FileInfo(hedef).Length);
        Assert.Empty(Kismiler());
    }

    private static async Task Ffmpeg(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var surec = new Process { StartInfo = psi };
        surec.Start();
        var bosalt = Task.WhenAll(surec.StandardOutput.ReadToEndAsync(), surec.StandardError.ReadToEndAsync());
        await surec.WaitForExitAsync();
        await bosalt;
        Assert.Equal(0, surec.ExitCode);
    }

    private static Task Klip(string yol, double sure)
        => Ffmpeg("-hide_banner", "-y", "-f", "lavfi", "-i", $"testsrc=size=160x120:rate=10:duration={sure.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            "-c:v", "libx264", "-preset", "ultrafast", "-threads", "2", "-pix_fmt", "yuv420p", yol);

    private async Task<EncodeResult> GercekOkumaylaKos(Func<string, Task> kodla, string hedef)
    {
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        await Klip(kaynak, 3);
        var runner = new EncodeRunner
        {
            SahteKodlayici = (_, yol, _) => kodla(yol),
            SureOkuyucu = EncodeRunner.CiktiSuresiAsync
        };
        return await runner.RunAsync(Kaynak(kaynak, 3), Plan(), hedef, 5, null);
    }

    /// <summary>Kasıtlı kısaltılmış gerçek çıktı: ffprobe 1 sn okur, 3 sn beklenir; iş hatalı, hedef ezilmez.</summary>
    [Fact]
    public async Task KasitliKisaltilmisGercekCiktiHataVerir()
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        var hedef = EskiHedef();

        var sonuc = await GercekOkumaylaKos(yol => Klip(yol, 1), hedef);

        Assert.False(sonuc.Success);
        Assert.False(sonuc.Kusur!.Cozulemedi);
        Assert.InRange(sonuc.Kusur.OkunanSaniye!.Value, 0.5, 1.5);
        Assert.Equal(EskiIcerik, File.ReadAllText(hedef));
        Assert.Empty(Kismiler());
    }

    /// <summary>Çözülemeyen çıktı: rastgele baytlar video değildir; iş hatalı, hedef ezilmez.</summary>
    [Fact]
    public async Task CozulemeyenGercekCiktiHataVerir()
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        var hedef = EskiHedef();

        var sonuc = await GercekOkumaylaKos(yol => File.WriteAllBytesAsync(yol, Enumerable.Range(0, 40_000).Select(i => (byte)(i * 31)).ToArray()), hedef);

        Assert.False(sonuc.Success);
        Assert.True(sonuc.Kusur!.Cozulemedi);
        Assert.Equal(EskiIcerik, File.ReadAllText(hedef));
    }

    /// <summary>Olumsuz kontrol: tam süreli gerçek çıktı okunur ve teslim edilir.</summary>
    [Fact]
    public async Task TamGercekCiktiTeslimEdilir()
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        var hedef = EskiHedef();

        var sonuc = await GercekOkumaylaKos(yol => Klip(yol, 3), hedef);

        Assert.True(sonuc.Success, sonuc.Error);
        Assert.NotEqual(EskiIcerik.Length, new FileInfo(hedef).Length);
        Assert.Empty(Kismiler());
    }

    private async Task<(DateTime Olusturma, DateTime Degistirme)> TarihliKos(bool koru)
    {
        var kaynak = Path.Combine(klasor, "tarihli.mp4");
        File.WriteAllBytes(kaynak, new byte[10]);
        var eskiOlusturma = new DateTime(2019, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var eskiDegistirme = new DateTime(2020, 8, 9, 10, 11, 12, DateTimeKind.Utc);
        File.SetCreationTimeUtc(kaynak, eskiOlusturma);
        File.SetLastWriteTimeUtc(kaynak, eskiDegistirme);
        var hedef = Path.Combine(klasor, "tarihli_cikti.mp4");
        var runner = new EncodeRunner
        {
            SahteKodlayici = (_, yol, _) => File.WriteAllBytesAsync(yol, new byte[50_000]),
            KaynakTarihleriniKoru = koru
        };

        var sonuc = await runner.RunAsync(Kaynak(kaynak, 3), Plan(), hedef, 5, null);

        Assert.True(sonuc.Success);
        return (File.GetCreationTimeUtc(hedef), File.GetLastWriteTimeUtc(hedef));
    }

    /// <summary>SIK-11: ayar açıkken çıktının değiştirme (Windows'ta oluşturma da) tarihi kaynağınkidir.</summary>
    [Fact]
    public async Task AyarAcikkenKaynakTarihiTasinir()
    {
        var (olusturma, degistirme) = await TarihliKos(koru: true);

        Assert.Equal(new DateTime(2020, 8, 9, 10, 11, 12, DateTimeKind.Utc), degistirme);
        if (OperatingSystem.IsWindows()) Assert.Equal(new DateTime(2019, 3, 4, 5, 6, 7, DateTimeKind.Utc), olusturma);
    }

    /// <summary>Olumsuz kontrol: ayar kapalıyken (varsayılan) çıktı kendi tarihini taşır.</summary>
    [Fact]
    public async Task AyarKapalikkenTarihTasinmaz()
    {
        var (_, degistirme) = await TarihliKos(koru: false);

        Assert.True(degistirme.Year >= 2026, $"değiştirme tarihi {degistirme:O}");
        Assert.False(new EncodeRunner().KaynakTarihleriniKoru);
    }
}
