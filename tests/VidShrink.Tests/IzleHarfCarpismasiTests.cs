using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// Harf farkıyla aynı ada inen iki dosya (<c>docs/netlestirme/021-izle-harf-carpismasi.md</c>).
/// Kusur yalnız harf duyarlı bir dosya sisteminde doğduğu için sahte dosya sistemi burada
/// ordinal: Windows'ta gerçek dosyayla kurulamaz, tarama listesi elle beslenir.
/// </summary>
public sealed class IzleHarfCarpismasiTests
{
    private const string Root = @"C:\izle\gelen";
    private const string Out = @"C:\izle\giden";
    private static readonly DateTime T0 = new(2026, 9, 18, 10, 0, 0, DateTimeKind.Utc);

    private sealed class HarfDuyarliFs : IWatchFileSystem
    {
        public readonly Dictionary<string, WatchFileStamp> Files = new(StringComparer.Ordinal);

        public void Put(string name, long length) => Files[Path.Combine(Root, name)] = new WatchFileStamp(length, T0);

        public bool DirectoryExists(string path) => true;
        public void CreateDirectory(string path) { }
        public IEnumerable<string> EnumerateFiles(string directory)
            => Files.Keys.Where(k => string.Equals(Path.GetDirectoryName(k), directory, StringComparison.Ordinal)).ToList();
        public WatchFileStamp? Stat(string path) => Files.TryGetValue(path, out var f) ? f : null;
        public bool IsLocked(string path) => false;
        public bool FileExists(string path) => Files.ContainsKey(path);
        public string ReadAllText(string path) => "";
        public void WriteAllTextAtomic(string path, string content) { }
        public void Move(string source, string destination) { }
        public void Delete(string path) { }
    }

    private sealed class Saat : IWatchClock
    {
        public DateTime UtcNow { get; set; } = T0;
        public int Delays;

        public Task Delay(TimeSpan delay, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Delays++;
            UtcNow += delay;
            return Task.CompletedTask;
        }
    }

    private static WatchFolder Izleyici(HarfDuyarliFs fs, Saat saat, bool once = true)
        => new(new WatchOptions
        {
            WatchDirectory = Root,
            OutputDirectory = Out,
            StatePath = Path.Combine(Out, "durum.json"),
            PollInterval = TimeSpan.FromSeconds(2),
            StableFor = TimeSpan.FromSeconds(2),
            Once = once
        }, fs, saat);

    private static List<string> UcTarama(WatchFolder izleyici, Saat saat, List<WatchEvent> olaylar)
    {
        var son = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            son = izleyici.Poll(olaylar.Add).ToList();
            saat.UtcNow += TimeSpan.FromSeconds(2);
        }

        return son;
    }

    /// <summary>
    /// Kusurun kendisi: çakışan çiftte hiçbiri kararlı sayılmıyordu. Artık ordinal sırada
    /// küçük olan alınıyor, öbürü atlanıyor.
    /// </summary>
    [Fact]
    public void CakisanCiftteKucukAdAliniyor()
    {
        var fs = new HarfDuyarliFs();
        var saat = new Saat();
        fs.Put("Klip.mp4", 100);
        fs.Put("klip.mp4", 250);

        var olaylar = new List<WatchEvent>();
        var hazir = UcTarama(Izleyici(fs, saat), saat, olaylar);

        Assert.Equal(new[] { Path.Combine(Root, "Klip.mp4") }, hazir);
    }

    /// <summary>Atlama bir kez duyuruluyor ve kaybedeni kazananın adıyla birlikte söylüyor.</summary>
    [Fact]
    public void AtlamaBirKezDuyuruluyor()
    {
        var fs = new HarfDuyarliFs();
        var saat = new Saat();
        fs.Put("Klip.mp4", 100);
        fs.Put("klip.mp4", 250);

        var olaylar = new List<WatchEvent>();
        var izleyici = Izleyici(fs, saat);
        UcTarama(izleyici, saat, olaylar);

        var carpisma = Assert.Single(olaylar, e => e.Kind == WatchEventKind.Collided);
        Assert.Equal(Path.Combine(Root, "klip.mp4"), carpisma.Path);
        Assert.Equal("Klip.mp4", carpisma.Detail);
        Assert.Equal(1, izleyici.CollidedCount);
    }

    /// <summary>Ayrı adlarda çakışma yok: negatif kontrol, kapı her dosyayı atlamıyor.</summary>
    [Fact]
    public void AyriAdlardaCakismaYok()
    {
        var fs = new HarfDuyarliFs();
        var saat = new Saat();
        fs.Put("Klip.mp4", 100);
        fs.Put("baska.mp4", 250);

        var olaylar = new List<WatchEvent>();
        var izleyici = Izleyici(fs, saat);
        var hazir = UcTarama(izleyici, saat, olaylar);

        Assert.Equal(2, hazir.Count);
        Assert.DoesNotContain(olaylar, e => e.Kind == WatchEventKind.Collided);
        Assert.Equal(0, izleyici.CollidedCount);
    }

    /// <summary>
    /// Asıl zarar: <c>--bir-kez</c> hiç çıkmıyordu. Kapıyla koşu bitiyor ve kazanan dosya
    /// işleniyor; kaybeden hiç işlenmiyor.
    /// </summary>
    [Fact]
    public async Task BirKezCakismadaCikiyor()
    {
        var fs = new HarfDuyarliFs();
        var saat = new Saat();
        fs.Put("Klip.mp4", 100);
        fs.Put("klip.mp4", 250);

        var izleyici = Izleyici(fs, saat);
        var islenen = new List<string>();
        using var iptal = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var sonuc = await izleyici.RunAsync((path, _) =>
        {
            islenen.Add(Path.GetFileName(path));
            return Task.FromResult(new WatchOutcome(0, true, Path.Combine(Out, "Klip_kucuk.mp4"), null));
        }, null, iptal.Token);

        Assert.Equal(WatchRunResult.Finished, sonuc);
        Assert.Equal(new[] { "Klip.mp4" }, islenen);
        Assert.Equal(1, izleyici.CollidedCount);
    }

    /// <summary>Kazanan her koşuda aynı: tarama sırası ordinal, ekleme sırası değil.</summary>
    [Fact]
    public void KazananEklemeSirasindanBagimsiz()
    {
        var ters = new HarfDuyarliFs();
        var saat = new Saat();
        ters.Put("klip.mp4", 250);
        ters.Put("Klip.mp4", 100);

        var olaylar = new List<WatchEvent>();
        var hazir = UcTarama(Izleyici(ters, saat), saat, olaylar);

        Assert.Equal(new[] { Path.Combine(Root, "Klip.mp4") }, hazir);
    }
}
