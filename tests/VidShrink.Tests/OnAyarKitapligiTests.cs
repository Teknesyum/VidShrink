using System.Text.Json;
using VidShrink.Cli;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// K8 borcu 6 (<c>docs/plan.md</c>). Gömülü kütüphanede on sekiz profil var, arayüz şeridi
/// yalnız <c>chip</c> alanı olan sekizini çiziyordu; kalan onu hiçbir kullanıcı hiçbir
/// yoldan seçemiyordu. <c>profiller</c> komutu ve <c>--profil</c> bayrağı o onu açıyor.
///
/// <para>Listeleme <see cref="PresetKind"/>'in dört üyesini de okuyor — pimlerin ölü
/// saydığı şey buydu. Kullanıcı bölümü servisten geliyor, gerçek ayar dosyası okunmuyor.</para>
/// </summary>
public sealed class OnAyarKitapligiTests
{
    private static MediaInfo Kaynak() => new()
    {
        FilePath = Path.Combine(Path.GetTempPath(), "kitaplik-kaynak.mp4"),
        FileSizeBytes = 400_000_000L,
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

    private static CliServices Servisler(params PresetProfile[] kullanici) => new()
    {
        MissingTool = () => null,
        Probe = (_, _) => Task.FromResult(Kaynak()),
        Availability = () => null,
        UserPresets = () => kullanici,
    };

    private sealed record Kosum(int Exit, string Stdout, string Stderr);

    private static async Task<Kosum> Kos(CliServices servisler, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await CliApp.RunAsync(args, stdout, stderr, CliText.ForLanguage("tr"), servisler, CancellationToken.None);
        return new Kosum(exit, stdout.ToString(), stderr.ToString());
    }

    private static async Task<Kosum> Plan(CliServices servisler, params string[] ekler)
    {
        var klasor = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            ".calisma", "test-ciktilari", "k8-kitaplik", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        try
        {
            var dosya = Path.Combine(klasor, "kaynak.mp4");
            await File.WriteAllTextAsync(dosya, "x");
            var args = new List<string> { "plan", dosya, "--olcumsuz", "--json" };
            args.AddRange(ekler);
            return await Kos(servisler, args.ToArray());
        }
        finally
        {
            Directory.Delete(Path.GetFullPath(klasor), recursive: true);
        }
    }

    private static JsonElement PlanDugumu(string stdout)
    {
        using var belge = JsonDocument.Parse(stdout);
        return belge.RootElement.Clone();
    }

    /// <summary>
    /// Dört bölüm başlığı da listede: <see cref="PresetKind"/>'in dört üyesi de okunuyor.
    /// Kullanıcı bölümü yalnız servis profil verince çıkıyor.
    /// </summary>
    [Fact]
    public async Task ListeDortTuruDeBolumluyor()
    {
        var kendi = new PresetProfile { Id = "kendi-profilim", Name = "Kendi Profilim", TargetMb = 12, Kind = PresetKind.User };
        var kosum = await Kos(Servisler(kendi), "profiller");

        Assert.Equal(0, kosum.Exit);
        var metin = CliText.ForLanguage("tr");
        Assert.Contains(metin["presets.kind.general"], kosum.Stdout, StringComparison.Ordinal);
        Assert.Contains(metin["presets.kind.platform"], kosum.Stdout, StringComparison.Ordinal);
        Assert.Contains(metin["presets.kind.device"], kosum.Stdout, StringComparison.Ordinal);
        Assert.Contains(metin["presets.kind.user"], kosum.Stdout, StringComparison.Ordinal);
        Assert.Contains("kendi-profilim", kosum.Stdout, StringComparison.Ordinal);
    }

    /// <summary>Kullanıcı profili yokken o bölüm hiç yazılmıyor — olumsuz kontrol.</summary>
    [Fact]
    public async Task KullaniciProfiliYokkenBolumCikmiyor()
    {
        var kosum = await Kos(Servisler(), "profiller");

        Assert.Equal(0, kosum.Exit);
        Assert.DoesNotContain(CliText.ForLanguage("tr")["presets.kind.user"], kosum.Stdout, StringComparison.Ordinal);
        Assert.Contains(CliText.ForLanguage("tr")["presets.kind.device"], kosum.Stdout, StringComparison.Ordinal);
    }

    /// <summary>
    /// Yongası olmayan on profilin kimliği listede. Liste testte tekrarlanmıyor: kütüphaneden
    /// <c>Chip is null</c> süzgeciyle çıkarılıyor, yani yeni bir yongasız profil de kapsanır.
    /// </summary>
    [Fact]
    public async Task YongasizProfillerinHepsiListede()
    {
        var yongasiz = PresetLibrary.BuiltIn.Profiles.Where(p => p.Chip is null).Select(p => p.Id).ToList();
        Assert.NotEmpty(yongasiz);

        var kosum = await Kos(Servisler(), "profiller");

        foreach (var id in yongasiz)
            Assert.Contains(id, kosum.Stdout, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>--profil</c> profilin alanlarını plana indiriyor: hedef boyut, niyet, doldurma
    /// siyaseti ve kısa kenar sınırı.
    /// </summary>
    [Fact]
    public async Task ProfilAlanlariPlanaIniyor()
    {
        var kosum = await Plan(Servisler(), "--profil", "device-chromecast-gen1-2", "--hedef", "40MB");

        Assert.Equal(0, kosum.Exit);
        var istek = CliParser.Parse(new[] { "plan", "a.mp4", "--hedef", "40", "--profil", "device-chromecast-gen1-2" }).Request!;
        var cozulen = istek with { Profile = PresetLibrary.BuiltIn.Find("device-chromecast-gen1-2") };
        var secenek = cozulen.ToPlanOptions(40);

        Assert.Equal(720, secenek.FixedResolution);
        Assert.Equal(FillPolicy.QualityCeiling, secenek.FillPolicy);
        Assert.Equal(Intent.Sharing, secenek.Intent);
    }

    /// <summary>Profilsiz koşumda hiçbir kilit kalmıyor — olumsuz kontrol.</summary>
    [Fact]
    public void ProfilsizKosumdaKilitYok()
    {
        var secenek = CliParser.Parse(new[] { "plan", "a.mp4", "--hedef", "40" }).Request!.ToPlanOptions(40);

        Assert.Null(secenek.FixedResolution);
        Assert.Equal(FillPolicy.FillTarget, secenek.FillPolicy);
    }

    /// <summary>
    /// Profil hedefi taşıyorsa <c>--hedef</c> verilmeden koşulabiliyor; elle verilen hedef
    /// profilinkini eziyor.
    /// </summary>
    [Fact]
    public async Task ElleVerilenHedefProfiliEziyor()
    {
        var profilden = await Plan(Servisler(), "--profil", "discord-free");
        Assert.Equal(0, profilden.Exit);
        Assert.Equal(20, PlanDugumu(profilden.Stdout).GetProperty("targetMb").GetDouble());

        var elle = await Plan(Servisler(), "--profil", "discord-free", "--hedef", "100MB");
        Assert.Equal(0, elle.Exit);
        Assert.Equal(100, PlanDugumu(elle.Stdout).GetProperty("targetMb").GetDouble());
    }

    /// <summary>
    /// Profilin kabı çıktı uzantısını seçiyor (fable kararı B, <c>docs/danisma/010-fable-onayar-kap.md</c>).
    /// WebM'in kodlama kolu yok, planın kabına düşüyor; kapsız profil de öyle (olumsuz kontrol).
    /// </summary>
    [Theory]
    [InlineData(OutputContainer.Mov, ".mov")]
    [InlineData(OutputContainer.Mkv, ".mkv")]
    [InlineData(OutputContainer.WebM, ".mp4")]
    [InlineData(null, ".mp4")]
    public async Task ProfilKabiCiktiUzantisiniSeciyor(OutputContainer? kap, string uzanti)
    {
        var profil = new PresetProfile { Id = "kapli", Name = "Kapli", TargetMb = 12, Kind = PresetKind.User, Container = kap };
        var kosum = await Plan(Servisler(profil), "--profil", "kapli");

        Assert.Equal(0, kosum.Exit);
        Assert.Equal(uzanti, Path.GetExtension(PlanDugumu(kosum.Stdout).GetProperty("output").GetString()));
    }

    /// <summary>Elle verilen <c>--cikti</c> profilin kabını eziyor.</summary>
    [Fact]
    public async Task ElleVerilenCiktiProfilKabiniEziyor()
    {
        var profil = new PresetProfile { Id = "kapli", Name = "Kapli", TargetMb = 12, Kind = PresetKind.User, Container = OutputContainer.Mov };
        var kosum = await Plan(Servisler(profil), "--profil", "kapli", "--cikti", "elle.mp4");

        Assert.Equal(0, kosum.Exit);
        Assert.Equal(".mp4", Path.GetExtension(PlanDugumu(kosum.Stdout).GetProperty("output").GetString()));
    }

    /// <summary>Tanınmayan kimlik 64 veriyor ve kimliği cümlede yazıyor.</summary>
    [Fact]
    public async Task TaninmayanKimlikReddediliyor()
    {
        var kosum = await Plan(Servisler(), "--profil", "uydurma-profil", "--hedef", "25MB");

        Assert.Equal(ExitCodes.Usage, kosum.Exit);
        Assert.Contains("uydurma-profil", kosum.Stderr, StringComparison.Ordinal);
    }

    /// <summary>Hedefsiz profil hedefsiz koşulamıyor; ayrı hata kolu.</summary>
    [Fact]
    public async Task HedefsizProfilHedefIstiyor()
    {
        var kosum = await Plan(Servisler(), "--profil", "device-nest-hub");

        Assert.Equal(ExitCodes.Usage, kosum.Exit);
        Assert.Contains("device-nest-hub", kosum.Stderr, StringComparison.Ordinal);
    }

    /// <summary>Kullanıcının kendi profili de <c>--profil</c> ile seçilebiliyor.</summary>
    [Fact]
    public async Task KullaniciProfiliDeSecilebiliyor()
    {
        var kendi = new PresetProfile { Id = "kendi-profilim", Name = "Kendi Profilim", TargetMb = 12, Kind = PresetKind.User };
        var kosum = await Plan(Servisler(kendi), "--profil", "kendi-profilim");

        Assert.Equal(0, kosum.Exit);
        Assert.Equal(12, PlanDugumu(kosum.Stdout).GetProperty("targetMb").GetDouble());
    }

    /// <summary>Yeni anahtarların hepsi iki dilde.</summary>
    [Fact]
    public void IkiDilDeKitaplikAnahtarlariniTasiyor()
    {
        foreach (var dil in new[] { "tr", "en" })
        {
            var metin = CliText.ForLanguage(dil);
            foreach (var anahtar in new[]
                     {
                         "presets.kind.general", "presets.kind.platform", "presets.kind.device",
                         "presets.kind.user", "presets.no-target", "presets.hint"
                     })
                Assert.False(string.IsNullOrWhiteSpace(metin[anahtar]), $"{dil}/{anahtar}");

            Assert.Contains("{0}", metin["presets.target"], StringComparison.Ordinal);
            Assert.Contains("{0}", metin["presets.short-edge"], StringComparison.Ordinal);
            Assert.Contains("{0}", metin["error.bad-profile"], StringComparison.Ordinal);
            Assert.Contains("{0}", metin["error.profile-no-target"], StringComparison.Ordinal);
        }
    }
}
