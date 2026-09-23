using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class WatchFolderTests
{
    private const string Root = @"C:\izle\gelen";
    private const string Out = @"C:\izle\giden";
    private const string Settings = @"C:\izle\ayar";
    private static readonly DateTime T0 = new(2026, 9, 17, 10, 0, 0, DateTimeKind.Utc);

    private sealed class FakeFile
    {
        public long Length;
        public DateTime LastWrite;
        public bool Locked;
        public string Text = "";
    }

    private sealed class FakeFs : IWatchFileSystem
    {
        public readonly Dictionary<string, FakeFile> Files = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> ReadOnly = new(StringComparer.OrdinalIgnoreCase);

        public void Put(string name, long length, DateTime? lastWrite = null, bool locked = false)
            => Files[Path.Combine(Root, name)] = new FakeFile { Length = length, LastWrite = lastWrite ?? T0, Locked = locked };

        public FakeFile Get(string name) => Files[Path.Combine(Root, name)];

        private void Guard(string path)
        {
            if (ReadOnly.Contains(Path.GetDirectoryName(path)!)) throw new UnauthorizedAccessException("salt okunur: " + path);
        }

        public bool DirectoryExists(string path) => true;
        public void CreateDirectory(string path) { }
        public IEnumerable<string> EnumerateFiles(string directory)
            => Files.Keys.Where(k => string.Equals(Path.GetDirectoryName(k), directory, StringComparison.OrdinalIgnoreCase)).ToList();
        public WatchFileStamp? Stat(string path) => Files.TryGetValue(path, out var f) ? new WatchFileStamp(f.Length, f.LastWrite) : null;
        public bool IsLocked(string path) => Files[path].Locked;
        public bool FileExists(string path) => Files.ContainsKey(path);
        public string ReadAllText(string path) => Files[path].Text;
        public void WriteAllTextAtomic(string path, string content)
        {
            Guard(path);
            Files[path] = new FakeFile { Text = content, Length = content.Length, LastWrite = T0 };
        }
        public void Move(string source, string destination)
        {
            Guard(source);
            Guard(destination);
            Files[destination] = Files[source];
            Files.Remove(source);
        }
        public void Delete(string path)
        {
            Guard(path);
            Files.Remove(path);
        }
    }

    private sealed class FakeClock : IWatchClock
    {
        public DateTime UtcNow { get; set; } = T0;
        public Action? OnDelay;
        public int Delays;

        public Task Delay(TimeSpan delay, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Delays++;
            UtcNow += delay;
            OnDelay?.Invoke();
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private static WatchFolder Watcher(FakeFs fs, FakeClock clock, bool once = true, WatchState? state = null, string? statePath = null)
        => new(new WatchOptions
        {
            WatchDirectory = Root,
            OutputDirectory = Out,
            StatePath = statePath,
            PollInterval = TimeSpan.FromSeconds(2),
            StableFor = TimeSpan.FromSeconds(2),
            Once = once
        }, fs, clock, state);

    private static Func<string, CancellationToken, Task<WatchOutcome>> Succeed(List<string> calls)
        => (path, _) =>
        {
            calls.Add(Path.GetFileName(path));
            return Task.FromResult(new WatchOutcome(0, true, Path.Combine(Out, Path.GetFileNameWithoutExtension(path) + "_shrunk.mp4"), null));
        };

    [Fact]
    public void YazimSurerkenAlinmiyorBoyutIkiArdisikAraliktaSabitleninceAliniyor()
    {
        var fs = new FakeFs();
        var clock = new FakeClock();
        var watcher = Watcher(fs, clock);

        fs.Put("a.mp4", 100);
        Assert.Empty(watcher.Poll());
        clock.UtcNow += TimeSpan.FromSeconds(2);
        fs.Get("a.mp4").Length = 250;
        Assert.Empty(watcher.Poll());
        clock.UtcNow += TimeSpan.FromSeconds(2);
        Assert.Empty(watcher.Poll());
        clock.UtcNow += TimeSpan.FromSeconds(2);
        Assert.Equal(new[] { Path.Combine(Root, "a.mp4") }, watcher.Poll());
    }

    [Fact]
    public async Task IslenenZamaniVeZamanDamgasiDurumDosyasindaGorunuyor()
    {
        var fs = new FakeFs();
        var clock = new FakeClock();
        var watcher = Watcher(fs, clock);

        fs.Put("a.mp4", 100, T0);
        Assert.Empty(watcher.Poll());

        var damga = fs.Stat(Path.Combine(Root, "a.mp4"));
        Assert.NotNull(damga);
        Assert.Equal(T0, damga!.Value.LastWriteUtc);
        Assert.Equal(100, damga.Value.Length);

        clock.UtcNow += TimeSpan.FromSeconds(2);
        Assert.Empty(watcher.Poll());
        clock.UtcNow += TimeSpan.FromSeconds(2);
        Assert.Single(watcher.Poll());

        var calls = new List<string>();
        await watcher.RunAsync(Succeed(calls), null, CancellationToken.None);
        var kayit = Assert.Single(watcher.State.Processed);
        Assert.Equal("a.mp4", kayit.Name);
        Assert.Equal(clock.UtcNow, kayit.ProcessedUtc);
        Assert.NotEqual(default, kayit.ProcessedUtc);
    }

    [Fact]
    public void ZamanDamgasiDegisirseBoyutAyniOlsaDaBekleniyor()
    {
        var fs = new FakeFs();
        var clock = new FakeClock();
        var watcher = Watcher(fs, clock);

        fs.Put("a.mp4", 100);
        Assert.Empty(watcher.Poll());
        clock.UtcNow += TimeSpan.FromSeconds(2);
        Assert.Empty(watcher.Poll());
        fs.Get("a.mp4").LastWrite = T0.AddSeconds(2);
        clock.UtcNow += TimeSpan.FromSeconds(2);
        Assert.Empty(watcher.Poll());
        clock.UtcNow += TimeSpan.FromSeconds(2);
        Assert.Empty(watcher.Poll());
        clock.UtcNow += TimeSpan.FromSeconds(2);
        Assert.Single(watcher.Poll());
    }

    [Fact]
    public void OturmaSuresiDolmadanAlinmiyor()
    {
        var fs = new FakeFs();
        var clock = new FakeClock();
        var watcher = Watcher(fs, clock);

        fs.Put("a.mp4", 100);
        Assert.Empty(watcher.Poll());
        clock.UtcNow += TimeSpan.FromSeconds(1);
        Assert.Empty(watcher.Poll());
        clock.UtcNow += TimeSpan.FromSeconds(0.9);
        Assert.Empty(watcher.Poll());
        clock.UtcNow += TimeSpan.FromSeconds(0.1);
        Assert.Single(watcher.Poll());
    }

    [Fact]
    public void KilitliDosyaKilitKalkanaDekAlinmiyor()
    {
        var fs = new FakeFs();
        var clock = new FakeClock();
        var watcher = Watcher(fs, clock);

        fs.Put("a.mp4", 100, locked: true);
        Assert.Empty(watcher.Poll());
        clock.UtcNow += TimeSpan.FromSeconds(2);
        Assert.Empty(watcher.Poll());
        clock.UtcNow += TimeSpan.FromSeconds(2);
        Assert.Empty(watcher.Poll());
        fs.Get("a.mp4").Locked = false;
        Assert.Single(watcher.Poll());
    }

    [Fact]
    public void GercekDosyaSistemindeYazmaIcinAcikDosyaKilitliSayiliyor()
    {
        var dir = Path.Combine(TestPaths.OutputRoot, "hb-a3-izle", $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "yaziliyor.mp4");
        try
        {
            using (var writer = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                writer.WriteByte(1);
                writer.Flush();
                Assert.True(PhysicalWatchFileSystem.Instance.IsLocked(path));
            }
            Assert.False(PhysicalWatchFileSystem.Instance.IsLocked(path));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void YalnizBuIzleminCiktisiAtlaniyorAtlananGunlugeBirKezYaziliyor()
    {
        var fs = new FakeFs();
        var clock = new FakeClock();
        var state = new WatchState { Processed = { new WatchEntry { Name = "a.mp4", Length = 50, Output = "a_shrunk.mp4" } } };
        var watcher = Watcher(fs, clock, state: state);
        foreach (var name in new[] { "a_shrunk.mp4", "x_shrunk.mp4", WatchFolder.StateFileName, WatchFolder.StateFileName + ".tmp", "not.txt", "b.mp4" })
            fs.Put(name, 100);
        var events = new List<WatchEvent>();

        watcher.Poll(events.Add);
        clock.UtcNow += TimeSpan.FromSeconds(2);
        watcher.Poll(events.Add);
        clock.UtcNow += TimeSpan.FromSeconds(2);

        Assert.Equal(new[] { Path.Combine(Root, "b.mp4"), Path.Combine(Root, "x_shrunk.mp4") }, watcher.Poll(events.Add));
        var skipped = Assert.Single(events, e => e.Kind == WatchEventKind.Skipped);
        Assert.Equal(Path.Combine(Root, "a_shrunk.mp4"), skipped.Path);
        Assert.False(WatchFolder.IsCandidate(@"C:\x\.gizli.mp4"));
        Assert.True(WatchFolder.IsCandidate(@"C:\x\a_shrunk.mp4"));
    }

    [Fact]
    public async Task IslenenCiktiKlasorundenIzlenenKlasoreGeriGelmiyor()
    {
        var fs = new FakeFs();
        var clock = new FakeClock();
        var calls = new List<string>();
        fs.Put("a.mp4", 100);
        var watcher = Watcher(fs, clock);

        var result = await watcher.RunAsync((path, ct) =>
        {
            fs.Put("a_shrunk.mp4", 40);
            return Succeed(calls)(path, ct);
        }, null, CancellationToken.None);

        Assert.Equal(WatchRunResult.Finished, result);
        Assert.Equal(new[] { "a.mp4" }, calls);
    }

    [Theory]
    [InlineData(@"C:\izle\gelen", @"C:\izle\gelen", true)]
    [InlineData(@"C:\izle\gelen", @"C:\izle\gelen\", true)]
    [InlineData(@"C:\izle\gelen", @"c:\IZLE\gelen", true)]
    [InlineData(@"C:\izle\gelen", @"C:\izle\gelen\..\gelen", true)]
    [InlineData(@"C:\izle\gelen", @"C:\izle\giden", false)]
    public void CiktiKlasoruIzlenenleAyniOlamaz(string watch, string output, bool rejected)
    {
        Assert.Equal(rejected ? "error.watch-same-output" : null, WatchFolder.ValidateFolders(watch, output));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void HarfFarkliKlasorWindowsVeMacOstaAyniSayiliyorLinuxtaSayilmiyor(bool windows, bool mac, bool rejected)
    {
        var comparison = WatchFolder.ComparisonFor(windows, mac);
        Assert.Equal(rejected ? "error.watch-same-output" : null,
            WatchFolder.ValidateFolders(@"C:\izle\Gelen", @"C:\izle\gelen", comparison));
        Assert.Null(WatchFolder.ValidateFolders(@"C:\izle\Gelen", @"C:\izle\giden", comparison));
    }

    [Fact]
    public void VarsayilanKiyasBuIsletimSistemininKuraliniKullaniyor()
    {
        Assert.Equal(OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase, WatchFolder.PathComparison);
        Assert.Equal(WatchFolder.ComparisonFor(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()), WatchFolder.PathComparison);
        Assert.Equal(OperatingSystem.IsLinux() ? null : "error.watch-same-output",
            WatchFolder.ValidateFolders(@"C:\izle\Gelen", @"C:\izle\gelen"));
    }

    /// <summary>
    /// Kural cümlesinin pimi. <c>PathComparison</c> yalnız iki yeri yönetiyor (klasör
    /// kıyası ve kendi çıktısının adı); dosya adının geri kalan kullanımları her platformda
    /// koşulsuz <c>OrdinalIgnoreCase</c>. Belge bunu söylemek zorunda, yoksa Linux okuruna
    /// "sende bu sorun yok" demiş oluyor.
    /// </summary>
    [Fact]
    public void HarfDuyarsizAdTablosuIkiBelgedeDeYaziyor()
    {
        var ingilizce = Belge("README.md");
        Assert.Contains("Exactly two comparisons follow the rule of the running system", ingilizce, StringComparison.Ordinal);
        Assert.Contains("`Ordinal` on Linux and `OrdinalIgnoreCase` on Windows and macOS.", ingilizce, StringComparison.Ordinal);
        Assert.Contains("default APFS volume, which is case-insensitive but case-preserving", ingilizce, StringComparison.Ordinal);
        Assert.Contains("Every other use of a file's name ignores case **on every platform, Linux included**", ingilizce, StringComparison.Ordinal);
        Assert.Contains("`Klip.mp4` and `klip.mp4` in one watched folder collide even where the filesystem keeps them apart as two separate files.", ingilizce, StringComparison.Ordinal);
        Assert.Contains("the ordinally smallest wins and is shrunk as usual", ingilizce, StringComparison.Ordinal);
        Assert.Contains("written once per file and not repeated on later scans", ingilizce, StringComparison.Ordinal);
        Assert.Contains("Rename the skipped file and the watcher picks it up as a new file.", ingilizce, StringComparison.Ordinal);
        Assert.Contains("or was skipped for a name collision", ingilizce, StringComparison.Ordinal);

        var turkce = Belge("README.tr.md");
        Assert.Contains("Koşan sistemin kuralına uyan tam iki kıyas var", turkce, StringComparison.Ordinal);
        Assert.Contains("Linux'ta `Ordinal`, Windows ile macOS'ta `OrdinalIgnoreCase`.", turkce, StringComparison.Ordinal);
        Assert.Contains("o bölüm harf duyarsız ama harf koruyordur", turkce, StringComparison.Ordinal);
        Assert.Contains("**her platformda, Linux dahil** harfi yok sayıyor", turkce, StringComparison.Ordinal);
        Assert.Contains("dosya sistemi ikisini iki ayrı dosya olarak tutsa bile çakışıyor.", turkce, StringComparison.Ordinal);
        Assert.Contains("sıralı (ordinal) küçük olan kazanıyor", turkce, StringComparison.Ordinal);
        Assert.Contains("Satır dosya başına bir kez yazılıyor, sonraki taramalarda tekrarlanmıyor.", turkce, StringComparison.Ordinal);
        Assert.Contains("izleyici onu yeni dosya olarak görüp küçültüyor.", turkce, StringComparison.Ordinal);
        Assert.Contains("ad çakışması yüzünden atlandığında `4`", turkce, StringComparison.Ordinal);
    }

    /// <summary>
    /// Belgedeki durum dosyası adı ve çıkış kodları sabitlerden pimleniyor: sabit değişince
    /// belge de değişmek zorunda. Denetim bu üçünün pimsiz olduğunu ölçtü.
    /// </summary>
    [Fact]
    public void IzleBelgesiDurumDosyasiVeCikisKodlariniSabitlerdenPimliyor()
    {
        foreach (var ad in new[] { "README.md", "README.tr.md" })
        {
            var belge = Belge(ad);
            Assert.Contains($"`{WatchFolder.StateFileName}`", belge, StringComparison.Ordinal);
            Assert.Contains($"`.vidshrink-izle-", belge, StringComparison.Ordinal);
        }

        var ingilizce = Belge("README.md");
        Assert.Contains($"`{ExitCodes.InBand}` in band, `{ExitCodes.UnderBand}` under the band", ingilizce, StringComparison.Ordinal);
        Assert.Contains($"`{ExitCodes.CeilingExceeded}` size ceiling exceeded", ingilizce, StringComparison.Ordinal);
        Assert.Contains($"`{ExitCodes.Error}` error, `{ExitCodes.Usage}` wrong usage, `{ExitCodes.Cancelled}` cancelled.", ingilizce, StringComparison.Ordinal);
        Assert.Contains($"Exit codes: `{ExitCodes.InBand}` finished, `{ExitCodes.WatchFailures}` `--bir-kez` finished but at least one file failed or was skipped for a name collision, `{ExitCodes.Error}` error, `{ExitCodes.Usage}` wrong usage, `{ExitCodes.Cancelled}` stopped with Ctrl+C.", ingilizce, StringComparison.Ordinal);

        var turkce = Belge("README.tr.md");
        Assert.Contains($"bantta `{ExitCodes.InBand}`, bandın altında `{ExitCodes.UnderBand}`", turkce, StringComparison.Ordinal);
        Assert.Contains($"boy tavanı aşıldığında `{ExitCodes.CeilingExceeded}`", turkce, StringComparison.Ordinal);
        Assert.Contains($"hatada `{ExitCodes.Error}`, yanlış kullanımda `{ExitCodes.Usage}`, iptalde `{ExitCodes.Cancelled}`.", turkce, StringComparison.Ordinal);
        Assert.Contains($"Çıkış kodları: bittiğinde `{ExitCodes.InBand}`, `--bir-kez` bitip en az bir dosya başarısız olduğunda ya da ad çakışması yüzünden atlandığında `{ExitCodes.WatchFailures}`, hatada `{ExitCodes.Error}`, yanlış kullanımda `{ExitCodes.Usage}`, Ctrl+C ile durdurulduğunda `{ExitCodes.Cancelled}`.", turkce, StringComparison.Ordinal);
    }

    /// <summary>
    /// <para>A3 denetim borcu: durum dosyasinin adindaki <c>&lt;hash&gt;</c> de isletim
    /// sisteminin harf kuralina bagliydi ama hicbir belgede yazmiyordu — kullanici ayni
    /// klasorun neden bazen tek bazen iki durum dosyasi actigini okuyacagi bir yer yoktu.
    /// Kural artik iki READMEde yazili ve <b>burada kuralin kendisine</b> bagli: belge
    /// uzunlugu sabitten okur, ucu de <see cref="WatchFolder.StateKey(string, StringComparison)"/>
    /// ile uc isletim sistemi kolunda olculur.</para>
    /// <para>Ozet kolu <c>StateKey</c>'in <b>kendi</b> asiri yuklemesinden alinir, boylece
    /// olcu kosan makineye bagli kalmaz: Windows ve macOS kollari harfi yok sayar, Linux
    /// kolu ayirir.</para>
    /// </summary>
    [Fact]
    public void IzleBelgesiOzetAnahtariniKuraldanPimliyor()
    {
        var duyarsiz = WatchFolder.ComparisonFor(windows: true, mac: false);
        var mac = WatchFolder.ComparisonFor(windows: false, mac: true);
        var duyarli = WatchFolder.ComparisonFor(windows: false, mac: false);

        var kucuk = Path.Combine(Path.GetTempPath(), "gelen");
        var buyuk = Path.Combine(Path.GetTempPath(), "Gelen");
        var baska = Path.Combine(Path.GetTempPath(), "giden");

        Assert.Equal(WatchFolder.StateKey(kucuk, duyarsiz), WatchFolder.StateKey(buyuk, duyarsiz));
        Assert.Equal(WatchFolder.StateKey(kucuk, mac), WatchFolder.StateKey(buyuk, mac));
        Assert.NotEqual(WatchFolder.StateKey(kucuk, duyarli), WatchFolder.StateKey(buyuk, duyarli));
        Assert.NotEqual(WatchFolder.StateKey(kucuk, duyarsiz), WatchFolder.StateKey(baska, duyarsiz));
        Assert.Equal(WatchFolder.StateKey(kucuk, duyarli), WatchFolder.StateKey(kucuk, duyarli));

        var ozet = WatchFolder.StateKey(kucuk, duyarli);
        Assert.Equal(WatchFolder.StateKeyHexLength, ozet.Length);
        Assert.All(ozet, harf => Assert.Contains(harf, "0123456789abcdef"));
        Assert.Equal(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(kucuk)))
                [..WatchFolder.StateKeyHexLength].ToLowerInvariant(),
            ozet);

        var ingilizce = Belge("README.md");
        Assert.Contains($"`<hash>` is the first {WatchFolder.StateKeyHexLength} hex characters, lowercase, of the SHA-256", ingilizce, StringComparison.Ordinal);
        Assert.Contains("upper-cased first where the running system ignores case (Windows and macOS), taken as it stands on Linux.", ingilizce, StringComparison.Ordinal);
        Assert.Contains("one shared state file on Windows and macOS and two separate ones on Linux", ingilizce, StringComparison.Ordinal);
        Assert.Contains("`izle-<hash>.json`", ingilizce, StringComparison.Ordinal);

        var turkce = Belge("README.tr.md");
        Assert.Contains($"SHA-256'sının ilk {WatchFolder.StateKeyHexLength} onaltılık karakteri, küçük harfle.", turkce, StringComparison.Ordinal);
        Assert.Contains("(Windows ve macOS) önce büyük harfe çevriliyor, Linux'ta olduğu gibi alınıyor.", turkce, StringComparison.Ordinal);
        Assert.Contains("Windows ve macOS'ta tek bir durum dosyasını paylaşıyor, Linux'ta iki ayrı dosya", turkce, StringComparison.Ordinal);
        Assert.Contains("`izle-<ozet>.json`", turkce, StringComparison.Ordinal);
    }

    /// <summary>
    /// <para>Denetim borcu: <see cref="WatchFolder.StateKeyHexLength"/> yalniz README cumlesiyle
    /// pimliydi. Yukaridaki testin uzunluk ve SHA-256 iddialarinin <b>ikisi de</b> sabitin
    /// kendisinden turuyor, dolayisiyla <c>16 -> 12</c> mutasyonunu kiran tek sey README metni:
    /// kod ile belge birlikte degisirse pim geciyordu.</para>
    /// <para>Bu kol sabite hic bakmaz. Kullaniciya gorunen yuzey olan <b>durum dosyasinin adini</b>
    /// olcer: <c>StateCandidates</c>'in urettigi iki adayin ozet parcasi tam 16 onaltilik karakter.
    /// Sayi burada duz yaziliyor, cunku pimlenen sey sabitin degeri degil dosya adinin sozlesmesi.
    /// Negatif kontrol: baska bir izlenen klasor baska bir ad uretir, yani ad sabit degil ozetten.</para>
    /// </summary>
    [Fact]
    public void DurumDosyasiAdindakiOzetOnaltiOnaltilikKarakter()
    {
        var kok = Path.Combine(TestPaths.OutputRoot, "hb-a3-izle", $"{Guid.NewGuid():N}");
        var izlenen = Path.Combine(kok, "gelen");
        var baska = Path.Combine(kok, "giden");
        var cikti = Path.Combine(kok, "cikti");
        var yedek = Path.Combine(kok, "ayar");

        var adaylar = WatchFolder.StateCandidates(izlenen, cikti, yedek);
        var ciktidaki = Path.GetFileName(adaylar[1]);
        var yedektki = Path.GetFileName(adaylar[^1]);

        Assert.Matches(@"^\.vidshrink-izle-[0-9a-f]{16}\.json$", ciktidaki);
        Assert.Matches(@"^izle-[0-9a-f]{16}\.json$", yedektki);

        var oteki = Path.GetFileName(WatchFolder.StateCandidates(baska, cikti, yedek)[^1]);
        Assert.Matches(@"^izle-[0-9a-f]{16}\.json$", oteki);
        Assert.NotEqual(yedektki, oteki);
    }

    /// <summary>
    /// README metni <see cref="MetinPimi.Duz"/>'den gecirilerek okunur: iddianin kelimeleri ve
    /// sirasi pimli kalir, satir sarma konumu pimin parcasi olmaz. Paragraf yeniden sarilinca
    /// davranis degismeden kirmizi veren pimler bu yuzden bosluk normallestirmesine gecti.
    /// </summary>
    private static string Belge(string ad) =>
        MetinPimi.Duz(File.ReadAllText(Path.Combine(TipSources.Root, ad)));

    [Fact]
    public void DurumDosyasiGecicidenAtomikTasinmaylaDegisiyorGeciciGerideKalmiyor()
    {
        var dir = Path.Combine(TestPaths.OutputRoot, "hb-a3-izle", $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, WatchFolder.StateFileName);
        try
        {
            PhysicalWatchFileSystem.Instance.WriteAllTextAtomic(path, "eski");
            PhysicalWatchFileSystem.Instance.WriteAllTextAtomic(path, "yeni");

            Assert.Equal("yeni", File.ReadAllText(path));
            Assert.Equal(new[] { path }, Directory.GetFiles(dir));
            Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task DurumDosyasiYenidenBaslatincaTekrarIslemeyiEngelliyor()
    {
        var fs = new FakeFs();
        var calls = new List<string>();
        fs.Put("a.mp4", 100);
        fs.Put("b.mp4", 200);

        await Watcher(fs, new FakeClock()).RunAsync(Succeed(calls), null, CancellationToken.None);
        Assert.Equal(new[] { "a.mp4", "b.mp4" }, calls);

        var statePath = Path.Combine(Root, WatchFolder.StateFileName);
        using (var json = JsonDocument.Parse(fs.ReadAllText(statePath)))
        {
            var names = json.RootElement.GetProperty("processed").EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();
            Assert.Equal(new[] { "a.mp4", "b.mp4" }, names);
            Assert.False(json.RootElement.GetProperty("processed")[0].GetProperty("failed").GetBoolean());
        }

        calls.Clear();
        var clock = new FakeClock { UtcNow = T0.AddHours(1) };
        var load = WatchFolder.LoadState(fs, statePath, clock.UtcNow);
        Assert.Null(load.CorruptBackup);
        await Watcher(fs, clock, state: load.State).RunAsync(Succeed(calls), null, CancellationToken.None);
        Assert.Empty(calls);

        fs.Get("b.mp4").Length = 300;
        load = WatchFolder.LoadState(fs, statePath, clock.UtcNow);
        await Watcher(fs, clock, state: load.State).RunAsync(Succeed(calls), null, CancellationToken.None);
        Assert.Equal(new[] { "b.mp4" }, calls);
    }

    [Theory]
    [InlineData("{ bozuk")]
    [InlineData("{\"version\":1,\"processed\":[{\"name\":\"\"}]}")]
    [InlineData("{\"version\":1,\"processed\":null}")]
    [InlineData("[]")]
    public async Task BozukDurumDosyasiYedeklenipBosListeyleBasliyor(string content)
    {
        var fs = new FakeFs();
        var clock = new FakeClock();
        var statePath = Path.Combine(Root, WatchFolder.StateFileName);
        fs.Files[statePath] = new FakeFile { Text = content, Length = content.Length };
        fs.Put("a.mp4", 100);

        var load = WatchFolder.LoadState(fs, statePath, clock.UtcNow);

        Assert.Equal(statePath + ".bozuk-20260917-100000", load.CorruptBackup);
        Assert.Equal(content, fs.ReadAllText(load.CorruptBackup!));
        Assert.Empty(load.State.Processed);
        var calls = new List<string>();
        var result = await Watcher(fs, clock, state: load.State).RunAsync(Succeed(calls), null, CancellationToken.None);
        Assert.Equal(WatchRunResult.Finished, result);
        Assert.Equal(new[] { "a.mp4" }, calls);
        Assert.Null(WatchFolder.LoadState(fs, statePath, clock.UtcNow).CorruptBackup);
    }

    [Fact]
    public async Task HataVerenDosyaSureciOldurmuyorSonrakiBaslatmadaBirKezYenidenDeneniyor()
    {
        var fs = new FakeFs();
        var calls = new List<string>();
        var attempts = new List<string>();
        fs.Put("a.mp4", 100);
        fs.Put("b.mp4", 100);
        fs.Put("c.mp4", 100);
        var events = new List<WatchEvent>();
        Func<string, CancellationToken, Task<WatchOutcome>> failing = (path, ct) =>
        {
            var name = Path.GetFileName(path);
            attempts.Add(name);
            if (name == "a.mp4") throw new InvalidDataException("moov atom not found");
            if (name == "b.mp4") return Task.FromResult(new WatchOutcome(1, false, null, "ffmpeg failed"));
            return Succeed(calls)(path, ct);
        };
        var statePath = Path.Combine(Root, WatchFolder.StateFileName);

        var clock = new FakeClock();
        var first = Watcher(fs, clock, once: false);
        using (var cts = new CancellationTokenSource())
        {
            clock.OnDelay = () => { if (clock.Delays == 6) cts.Cancel(); };
            var result = await first.RunAsync(failing, events.Add, cts.Token);
            Assert.Equal(WatchRunResult.Cancelled, result);
        }

        Assert.Equal(new[] { "a.mp4", "b.mp4", "c.mp4" }, attempts);
        Assert.Equal(new[] { "c.mp4" }, calls);
        Assert.Equal(2, first.FailedCount);
        var load = WatchFolder.LoadState(fs, statePath, T0);
        var a = load.State.Processed.Single(e => e.Name == "a.mp4");
        Assert.True(a.Failed);
        Assert.False(a.Retried);
        Assert.Equal("moov atom not found", a.Error);
        Assert.True(load.State.Processed.Single(e => e.Name == "b.mp4").Failed);
        Assert.False(load.State.Processed.Single(e => e.Name == "c.mp4").Failed);
        Assert.Equal(2, events.Count(e => e.Kind == WatchEventKind.Failed));

        attempts.Clear();
        await Watcher(fs, new FakeClock(), state: load.State).RunAsync(failing, null, CancellationToken.None);
        Assert.Equal(new[] { "a.mp4", "b.mp4" }, attempts);
        load = WatchFolder.LoadState(fs, statePath, T0);
        Assert.All(load.State.Processed.Where(e => e.Failed), e => Assert.True(e.Retried));

        attempts.Clear();
        await Watcher(fs, new FakeClock(), state: load.State).RunAsync(failing, null, CancellationToken.None);
        Assert.Empty(attempts);
    }

    [Fact]
    public async Task KodlamaSirasindaBuyuyenDosyaIslendiSayilmiyorCiktiSilinipYenidenDeneniyor()
    {
        var fs = new FakeFs();
        var clock = new FakeClock();
        fs.Put("a.mp4", 100);
        var events = new List<WatchEvent>();
        var outputs = new List<string>();
        var lengths = new List<long>();

        var result = await Watcher(fs, clock).RunAsync((path, _) =>
        {
            lengths.Add(fs.Stat(path)!.Value.Length);
            var output = Path.Combine(Out, $"a_shrunk_{lengths.Count}.mp4");
            fs.Files[output] = new FakeFile { Length = 10, LastWrite = T0 };
            outputs.Add(output);
            if (lengths.Count == 1)
            {
                var file = fs.Get("a.mp4");
                file.Length = 180;
                file.LastWrite = T0.AddSeconds(1);
                Assert.Empty(WatchFolder.LoadState(fs, Path.Combine(Root, WatchFolder.StateFileName), T0).State.Processed);
            }
            return Task.FromResult(new WatchOutcome(0, true, output, null));
        }, events.Add, CancellationToken.None);

        Assert.Equal(WatchRunResult.Finished, result);
        Assert.Equal(new long[] { 100, 180 }, lengths);
        Assert.False(fs.FileExists(outputs[0]));
        Assert.True(fs.FileExists(outputs[1]));
        Assert.Single(events, e => e.Kind == WatchEventKind.Changed);
        var entry = Assert.Single(WatchFolder.LoadState(fs, Path.Combine(Root, WatchFolder.StateFileName), T0).State.Processed);
        Assert.Equal((180L, "a_shrunk_2.mp4"), (entry.Length, entry.Output));
    }

    [Fact]
    public async Task SaltOkunurKlasordeDurumCiktiKlasorundeTutuluyorIkinciKosuYenidenKodlamiyor()
    {
        var fs = new FakeFs();
        fs.ReadOnly.Add(Root);
        fs.Put("a.mp4", 100);
        var calls = new List<string>();

        var location = WatchFolder.OpenState(fs, Root, Out, Settings, T0);
        Assert.True(location.Writable);
        Assert.Equal(Path.Combine(Out, $".vidshrink-izle-{WatchFolder.StateKey(Root)}.json"), location.Path);
        var events = new List<WatchEvent>();
        await Watcher(fs, new FakeClock(), state: location.Load.State, statePath: location.Path).RunAsync(Succeed(calls), events.Add, CancellationToken.None);
        Assert.Equal(new[] { "a.mp4" }, calls);
        Assert.True(fs.FileExists(location.Path));
        Assert.DoesNotContain(events, e => e.Kind == WatchEventKind.StateNotSaved);
        Assert.False(fs.FileExists(Path.Combine(Root, WatchFolder.StateFileName)));

        calls.Clear();
        var again = WatchFolder.OpenState(fs, Root, Out, Settings, T0.AddHours(1));
        Assert.Equal(location.Path, again.Path);
        await Watcher(fs, new FakeClock(), state: again.Load.State, statePath: again.Path).RunAsync(Succeed(calls), null, CancellationToken.None);
        Assert.Empty(calls);

        fs.ReadOnly.Add(Out);
        var third = WatchFolder.OpenState(fs, Root, Out, Settings, T0);
        Assert.Equal(Path.Combine(Settings, $"izle-{WatchFolder.StateKey(Root)}.json"), third.Path);
        Assert.Single(third.Load.State.Processed);
    }

    [Fact]
    public async Task DurumHicYazilamazsaIzlemeDurmuyor()
    {
        var fs = new FakeFs();
        fs.ReadOnly.UnionWith(new[] { Root, Out, Settings });
        fs.Put("a.mp4", 100);
        fs.Put("b.mp4", 100);
        var calls = new List<string>();
        var events = new List<WatchEvent>();

        var location = WatchFolder.OpenState(fs, Root, Out, Settings, T0);
        Assert.False(location.Writable);
        var result = await Watcher(fs, new FakeClock(), state: location.Load.State, statePath: location.Path)
            .RunAsync(Succeed(calls), events.Add, CancellationToken.None);

        Assert.Equal(WatchRunResult.Finished, result);
        Assert.Equal(new[] { "a.mp4", "b.mp4" }, calls);
        Assert.Equal(2, events.Count(e => e.Kind == WatchEventKind.StateNotSaved));
        Assert.Equal(2, events.Count(e => e.Kind == WatchEventKind.Done));
    }

    [Fact]
    public async Task CtrlCKodlamaSirasindaTemizKapaniyorYarimDosyaNotEdilmiyor()
    {
        var fs = new FakeFs();
        var calls = new List<string>();
        fs.Put("a.mp4", 100);
        fs.Put("b.mp4", 100);
        using var cts = new CancellationTokenSource();
        var events = new List<WatchEvent>();

        var result = await Watcher(fs, new FakeClock(), once: false).RunAsync(async (path, ct) =>
        {
            if (Path.GetFileName(path) == "b.mp4")
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
            }
            return await Succeed(calls)(path, ct);
        }, events.Add, cts.Token);

        Assert.Equal(WatchRunResult.Cancelled, result);
        Assert.Equal(WatchEventKind.Stopped, events[^1].Kind);
        var load = WatchFolder.LoadState(fs, Path.Combine(Root, WatchFolder.StateFileName), T0);
        Assert.Null(load.CorruptBackup);
        Assert.Equal(new[] { "a.mp4" }, load.State.Processed.Select(e => e.Name));
    }

    [Fact]
    public async Task CtrlCBasarisizDonenIptalliKodlamayiNotEtmiyor()
    {
        var fs = new FakeFs();
        fs.Put("a.mp4", 100);
        using var cts = new CancellationTokenSource();

        var result = await Watcher(fs, new FakeClock(), once: false).RunAsync((_, _) =>
        {
            cts.Cancel();
            return Task.FromResult(new WatchOutcome(1, false, null, "killed"));
        }, null, cts.Token);

        Assert.Equal(WatchRunResult.Cancelled, result);
        Assert.False(fs.FileExists(Path.Combine(Root, WatchFolder.StateFileName)));
    }

    [Fact]
    public async Task CtrlCBeklemedeIkenDonguyuBitiriyor()
    {
        var fs = new FakeFs();
        var clock = new FakeClock();
        using var cts = new CancellationTokenSource();
        clock.OnDelay = () => { if (clock.Delays == 3) cts.Cancel(); };

        var result = await Watcher(fs, clock, once: false).RunAsync((_, _) => throw new InvalidOperationException(), null, cts.Token);

        Assert.Equal(WatchRunResult.Cancelled, result);
        Assert.Equal(3, clock.Delays);
    }

    [Fact]
    public async Task BirKezKipiYazimiSurenDosyayiBekleyipBitiriyor()
    {
        var fs = new FakeFs();
        var clock = new FakeClock();
        var calls = new List<string>();
        fs.Put("a.mp4", 10);
        clock.OnDelay = () =>
        {
            if (clock.Delays <= 2) fs.Get("a.mp4").Length += 10;
        };

        var result = await Watcher(fs, clock).RunAsync(Succeed(calls), null, CancellationToken.None);

        Assert.Equal(WatchRunResult.Finished, result);
        Assert.Equal(new[] { "a.mp4" }, calls);
        Assert.Equal(4, clock.Delays);
        Assert.Equal(30, WatchFolder.LoadState(fs, Path.Combine(Root, WatchFolder.StateFileName), T0).State.Processed.Single().Length);
    }

    [Theory]
    [InlineData(new[] { "izle", "gelen", "--hedef", "25" }, "error.watch-no-output")]
    [InlineData(new[] { "izle", "--cikti", "giden", "--hedef", "25" }, "error.watch-no-folder")]
    [InlineData(new[] { "izle", "gelen", "--cikti", "giden", "--hedef", "25", "--aralik", "0" }, "error.bad-interval")]
    [InlineData(new[] { "kucult", "a.mp4", "--hedef", "25", "--bir-kez" }, "error.unknown-option")]
    [InlineData(new[] { "kucult", "a.mp4", "--hedef", "25", "--aralik", "2" }, "error.unknown-option")]
    public void IzleArgumanHatalari(string[] args, string key)
    {
        var parsed = CliParser.Parse(args);
        Assert.False(parsed.Ok);
        Assert.Equal(key, parsed.ErrorKey);
        Assert.NotEqual(key, CliText.ForLanguage("tr")[key]);
        Assert.NotEqual(key, CliText.ForLanguage("en")[key]);
    }

    [Fact]
    public void IzleArgumanlariOkunuyor()
    {
        var parsed = CliParser.Parse(new[] { "watch", "gelen", "--output", "giden", "--target", "25", "--interval", "0,5", "--once", "--olcumsuz" });
        Assert.True(parsed.Ok);
        var request = parsed.Request!;
        Assert.Equal(CliCommand.Watch, request.Command);
        Assert.Equal(("gelen", "giden", 0.5, true, true), (request.Input, request.Output, request.PollSeconds, request.Once, request.SkipMeasurement));
    }

    [Fact]
    public void IzleAnahtarlariHerCliDilindeVar()
    {
        foreach (var language in CliText.Languages)
        {
            var strings = CliText.Load(language);
            foreach (var key in new[] { "watch.started", "watch.corrupt-state", "watch.waiting", "watch.processing", "watch.done", "watch.failed",
                         "watch.stopped", "watch.skipped", "watch.changed", "watch.state-elsewhere", "watch.state-not-saved",
                         "error.watch-missing", "error.watch-same-output", "error.watch-no-output", "error.watch-no-folder", "error.bad-interval" })
                Assert.True(strings.ContainsKey(key), $"{language}: {key}");
            Assert.Contains("izle", strings["help"], StringComparison.Ordinal);
            Assert.Contains("--bir-kez", strings["help"], StringComparison.Ordinal);
            Assert.Contains("NDJSON", strings["help"], StringComparison.Ordinal);
            Assert.Equal(4, ExitCodes.WatchFailures);
            Assert.Contains($"  {ExitCodes.WatchFailures}   izle --bir-kez", strings["help"], StringComparison.Ordinal);
            Assert.Contains(WatchFolder.StateFileName, strings["help"], StringComparison.Ordinal);
            var belge = File.ReadAllText(Path.Combine(TipSources.Root, "README.md"));
            Assert.Contains($"`{ExitCodes.WatchFailures}` `--bir-kez` finished but at least one file failed", belge, StringComparison.Ordinal);
        }
    }

    private static CliServices FakeServices(FakeFs fs, FakeClock clock) => new()
    {
        MissingTool = () => null,
        Probe = (_, _) => throw new InvalidDataException("bozuk kaynak"),
        Availability = () => null,
        WatchFileSystem = fs,
        WatchClock = clock,
        WatchStateFallbackDirectory = () => Settings
    };

    [Fact]
    public async Task CliCtrlCIle130Donuyor()
    {
        var fs = new FakeFs();
        var clock = new FakeClock();
        using var cts = new CancellationTokenSource();
        clock.OnDelay = () => { if (clock.Delays == 2) cts.Cancel(); };
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(new[] { "izle", Root, "--cikti", Out, "--hedef", "1" },
            new StringWriter(), stderr, CliText.ForLanguage("en"), FakeServices(fs, clock), cts.Token);

        Assert.Equal(ExitCodes.Cancelled, exit);
        Assert.Equal(2, clock.Delays);
        Assert.Contains(CliText.ForLanguage("en")["watch.stopped"], stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CliBirKezHatasizBosKlasordeSifirDoner()
    {
        var exit = await CliApp.RunAsync(new[] { "izle", Root, "--cikti", Out, "--hedef", "1", "--bir-kez" },
            new StringWriter(), new StringWriter(), CliText.ForLanguage("en"), FakeServices(new FakeFs(), new FakeClock()), CancellationToken.None);

        Assert.Equal(ExitCodes.InBand, exit);
    }

    [Fact]
    public async Task CliSaltOkunurKlasordeDurumuCiktiKlasorundeTutuyorHatadaDortDoner()
    {
        var fs = new FakeFs();
        fs.ReadOnly.Add(Root);
        fs.Put("yok.mp4", 100);
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(new[] { "izle", Root, "--cikti", Out, "--hedef", "1", "--olcumsuz", "--bir-kez" },
            new StringWriter(), stderr, CliText.ForLanguage("en"), FakeServices(fs, new FakeClock()), CancellationToken.None);

        Assert.Equal(4, exit);
        var statePath = Path.Combine(Out, $".vidshrink-izle-{WatchFolder.StateKey(Root)}.json");
        Assert.Contains(CliText.ForLanguage("en").Format("watch.state-elsewhere", statePath), stderr.ToString(), StringComparison.Ordinal);
        var entry = Assert.Single(WatchFolder.LoadState(fs, statePath, T0).State.Processed);
        Assert.True(entry.Failed);
    }

    [Fact]
    public async Task CliAyniKlasordeKodlamadanReddediyor()
    {
        var dir = Path.Combine(TestPaths.OutputRoot, "hb-a3-izle", $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "a.mp4"), new byte[16]);
            var services = new CliServices
            {
                MissingTool = () => null,
                Probe = (_, _) => throw new InvalidOperationException("probe must not run"),
                Availability = () => null
            };
            var stderr = new StringWriter();

            var exit = await CliApp.RunAsync(new[] { "izle", dir, "--cikti", dir + Path.DirectorySeparatorChar, "--hedef", "1", "--bir-kez" },
                new StringWriter(), stderr, CliText.ForLanguage("tr"), services, CancellationToken.None);

            Assert.Equal(ExitCodes.Usage, exit);
            Assert.Contains(CliText.ForLanguage("tr")["error.watch-same-output"], stderr.ToString(), StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(dir, WatchFolder.StateFileName)));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task CliProbeHatasindaDosyayiAtlayipDigerineGeciyorBirKezDortDoner()
    {
        var dir = Path.Combine(TestPaths.OutputRoot, "hb-a3-izle", $"{Guid.NewGuid():N}");
        var watch = Path.Combine(dir, "gelen");
        var output = Path.Combine(dir, "giden");
        Directory.CreateDirectory(watch);
        try
        {
            File.WriteAllBytes(Path.Combine(watch, "a.mp4"), new byte[16]);
            File.WriteAllBytes(Path.Combine(watch, "b.mp4"), new byte[16]);
            var probed = new List<string>();
            var services = new CliServices
            {
                MissingTool = () => null,
                Probe = (path, _) =>
                {
                    probed.Add(Path.GetFileName(path));
                    throw new InvalidDataException("bozuk kaynak");
                },
                Availability = () => null,
                WatchClock = new FakeClock()
            };
            var stderr = new StringWriter();

            var exit = await CliApp.RunAsync(new[] { "izle", watch, "--cikti", output, "--hedef", "1", "--olcumsuz", "--bir-kez" },
                new StringWriter(), stderr, CliText.ForLanguage("en"), services, CancellationToken.None);

            Assert.Equal(4, exit);
            Assert.Equal(new[] { "a.mp4", "b.mp4" }, probed);
            Assert.True(Directory.Exists(output));
            var load = WatchFolder.LoadState(PhysicalWatchFileSystem.Instance, Path.Combine(watch, WatchFolder.StateFileName), T0);
            Assert.All(load.State.Processed, e => Assert.True(e.Failed));
            Assert.Equal(2, load.State.Processed.Count);
            Assert.Contains("Failed and noted: a.mp4: bozuk kaynak", stderr.ToString(), StringComparison.Ordinal);
        }
        finally { Directory.Delete(dir, true); }
    }

    [FfmpegFact]
    public async Task IzleSureciKlibiCiktiKlasorundeKucultupDuruyor()
    {
        var dir = Path.Combine(TestPaths.OutputRoot, "hb-a3-izle", $"{Guid.NewGuid():N}");
        var watch = Path.Combine(dir, "gelen");
        var output = Path.Combine(dir, "giden");
        Directory.CreateDirectory(watch);
        try
        {
            var clip = Path.Combine(watch, "klip.mp4");
            var log = await RunAsync(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "testsrc2=size=320x240:rate=15:duration=2",
                "-c:v", "libx264", "-preset", "ultrafast", "-crf", "8", "-threads", "2", clip);
            Assert.True(log.Exit == 0, log.Stderr);
            var target = (Megabayt.Oku(new FileInfo(clip).Length) / 2).ToString("0.###", CultureInfo.InvariantCulture) + "MB";

            var cli = Path.Combine(AppContext.BaseDirectory, "vidshrink.dll");
            Assert.True(File.Exists(cli), cli);
            var run = await RunAsync("dotnet", cli, "izle", watch, "--cikti", output, "--hedef", target, "--kodek", "h264", "--olcumsuz", "--bir-kez", "--aralik", "0.5", "--json");

            Assert.True(run.Exit == 0, run.Stderr);
            var lines = run.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var line = Assert.Single(lines);
            using (var json = JsonDocument.Parse(line))
                Assert.Equal("kucult", json.RootElement.GetProperty("command").GetString());
            var produced = Path.Combine(output, "klip_shrunk.mp4");
            Assert.True(File.Exists(produced), run.Stderr);
            Assert.Empty(Directory.GetFiles(watch, "*_shrunk*"));
            var probe = await RunAsync(ToolLocator.Ffprobe, "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=codec_name,width,height",
                "-of", "csv=p=0", produced);
            Assert.True(probe.Exit == 0, probe.Stderr);
            var fields = probe.Stdout.Trim().Split(',');
            Assert.Equal("h264", fields[0]);
            Assert.True(int.Parse(fields[1], CultureInfo.InvariantCulture) > 0);
            var state = WatchFolder.LoadState(PhysicalWatchFileSystem.Instance, Path.Combine(watch, WatchFolder.StateFileName), DateTime.UtcNow);
            var entry = Assert.Single(state.State.Processed);
            Assert.Equal(("klip.mp4", false, "klip_shrunk.mp4"), (entry.Name, entry.Failed, entry.Output));

            var again = await RunAsync("dotnet", cli, "izle", watch, "--cikti", output, "--hedef", target, "--olcumsuz", "--bir-kez", "--aralik", "0.5");
            Assert.True(again.Exit == 0, again.Stderr);
            Assert.Single(Directory.GetFiles(output));
            Assert.DoesNotContain("klip.mp4", again.Stderr, StringComparison.Ordinal);
        }
        finally { Directory.Delete(dir, true); }
    }

    private static async Task<(int Exit, string Stdout, string Stderr)> RunAsync(string file, params string[] args)
    {
        var start = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(120));
        }
        catch (TimeoutException)
        {
            process.Kill(true);
            throw;
        }
        return (process.ExitCode, await stdout, await stderr);
    }
}
