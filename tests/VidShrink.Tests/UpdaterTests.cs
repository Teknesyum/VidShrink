using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using VidShrink.Core;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Kurulu bir başlatıcı gösterilmediğinde açılış süresi ölçümleri atlanır.
/// VIDSHRINK_LAUNCHER_EXE kurulu VidShrink.exe dosyasını göstermelidir.
/// </summary>
public sealed class LiveLauncherFactAttribute : FactAttribute
{
    public LiveLauncherFactAttribute()
    {
        var exe = Environment.GetEnvironmentVariable("VIDSHRINK_LAUNCHER_EXE");
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            Skip = "VIDSHRINK_LAUNCHER_EXE does not point at an existing file, so launcher timing was not measured.";
    }
}

public sealed class UpdaterTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _root;

    public UpdaterTests(ITestOutputHelper output)
    {
        _output = output;
        _root = Path.Combine(TestPaths.OutputRoot, "updater", Environment.ProcessId.ToString(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    private string Folder(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Write(string directory, string relativePath, string content)
    {
        var full = UpdateCheck.LocalPath(directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private static string WriteRandom(string directory, string relativePath, int size)
    {
        var full = UpdateCheck.LocalPath(directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var bytes = new byte[size];
        Random.Shared.NextBytes(bytes);
        File.WriteAllBytes(full, bytes);
        return full;
    }

    private static ManifestFile Describe(string directory, string relativePath)
    {
        var full = UpdateCheck.LocalPath(directory, relativePath);
        return new ManifestFile(relativePath, UpdateCheck.HashFile(full), new FileInfo(full).Length);
    }

    [Fact]
    public void ParseManifestReadsThePublishedSchema()
    {
        const string json = """
        {
          "version": "1.2.0",
          "commit": "abc1234",
          "built": "2026-08-23T18:00:00Z",
          "rid": "win-x64",
          "files": [
            { "path": "runtimes/win-x64/native/av_libglesv2.dll", "sha256": "AA11", "size": 42 },
            { "path": "VidShrink.App.dll", "sha256": "e3b0c442", "size": 123456 }
          ]
        }
        """;

        var manifest = UpdateCheck.ParseManifest(json);

        Assert.Equal("1.2.0", manifest.Version);
        Assert.Equal("abc1234", manifest.Commit);
        Assert.Equal("win-x64", manifest.Rid);
        Assert.Equal(2, manifest.Files.Count);
        Assert.Equal("runtimes/win-x64/native/av_libglesv2.dll", manifest.Files[0].Path);
        Assert.Equal("aa11", manifest.Files[0].Sha256);
        Assert.Equal(123456, manifest.Files[1].Size);
    }

    [Fact]
    public void BrokenManifestIsRejectedInsteadOfHalfRead()
    {
        Assert.Throws<InvalidDataException>(() => UpdateCheck.ParseManifest("{ \"version\": "));
        Assert.Throws<InvalidDataException>(() => UpdateCheck.ParseManifest("{ \"files\": [] }"));
    }

    [Theory]
    [InlineData("1.2.1", "1.2.0", true)]
    [InlineData("v1.3.0", "1.2.9", true)]
    [InlineData("1.2.0", "1.2.0", false)]
    [InlineData("1.1.9", "1.2.0", false)]
    [InlineData("1.2.0", "1.2.0-rc.1", true)]
    [InlineData("1.2.0-rc.1", "1.2.0", false)]
    [InlineData("1.2.0+abc", "1.2.0", false)]
    [InlineData("bozuk", "1.2.0", false)]
    public void NewerVersionIsRecognised(string candidate, string current, bool expected) =>
        Assert.Equal(expected, UpdateCheck.IsNewer(candidate, current));

    [Fact]
    public void DiffPicksOnlyMissingAndChangedFiles()
    {
        var app = Folder("app");
        Write(app, "same.dll", "aynı");
        Write(app, "changed.dll", "eski");
        Write(app, "resized.dll", "kısa");

        var manifest = new ReleaseManifest("1.1.0", "abc", DateTimeOffset.UtcNow, "win-x64", new[]
        {
            Describe(app, "same.dll"),
            Describe(app, "changed.dll") with { Sha256 = new string('0', 64) },
            Describe(app, "resized.dll") with { Size = 9999 },
            new ManifestFile("subdir/missing.dll", new string('1', 64), 10)
        });

        var changed = UpdateCheck.Diff(app, manifest);

        Assert.Equal(
            new[] { "changed.dll", "resized.dll", "subdir/missing.dll" },
            changed.Select(file => file.Path).ToArray());
    }

    [Fact]
    public void HashCacheSkipsRehashingUnchangedFiles()
    {
        var app = Folder("app");
        for (var i = 0; i < 20; i++) Write(app, $"file{i}.dll", new string('x', 4096));
        var manifest = new ReleaseManifest("1.1.0", "abc", DateTimeOffset.UtcNow, "win-x64",
            Enumerable.Range(0, 20).Select(i => Describe(app, $"file{i}.dll")).ToArray());
        var cachePath = Path.Combine(_root, "hashes.json");

        var cold = new HashCache(cachePath);
        Assert.Empty(UpdateCheck.Diff(app, manifest, cold));
        cold.Save();

        var warm = new HashCache(cachePath);
        Assert.Empty(UpdateCheck.Diff(app, manifest, warm));

        Assert.Equal(20, cold.Misses);
        Assert.Equal(20, warm.Hits);
        Assert.Equal(0, warm.Misses);
    }

    [Fact]
    public void CorruptedDownloadIsRejectedAndAppDirectoryStaysUntouched()
    {
        var app = Folder("app");
        var stage = Folder("stage");
        Write(app, "VidShrink.App.dll", "eski sürüm");
        Write(app, "VidShrink.Core.dll", "eski çekirdek");

        Write(stage, "VidShrink.App.dll", "yeni sürüm");
        Write(stage, "VidShrink.Core.dll", "yeni çekirdek");
        var files = new[] { Describe(stage, "VidShrink.App.dll"), Describe(stage, "VidShrink.Core.dll") };

        // Yarım inen dosyayı taklit et: içerik değişti, manifestteki özet duruyor.
        Write(stage, "VidShrink.Core.dll", "yeni çekir");

        Assert.Throws<InvalidDataException>(() => UpdateStage.Apply(stage, app, files));

        Assert.Equal("eski sürüm", File.ReadAllText(Path.Combine(app, "VidShrink.App.dll")));
        Assert.Equal("eski çekirdek", File.ReadAllText(Path.Combine(app, "VidShrink.Core.dll")));
        Assert.False(Directory.Exists(stage));
        Assert.False(File.Exists(Path.Combine(app, UpdateStage.JournalName)));
    }

    [Fact]
    public void VerifiedUpdateIsAppliedAndStageIsCleared()
    {
        var app = Folder("app");
        var stage = Folder("stage");
        Write(app, "VidShrink.App.dll", "eski");
        Write(stage, "VidShrink.App.dll", "yeni");
        Write(stage, "runtimes/win-x64/native/new.dll", "yepyeni");
        var files = new[] { Describe(stage, "VidShrink.App.dll"), Describe(stage, "runtimes/win-x64/native/new.dll") };

        UpdateStage.Apply(stage, app, files);

        Assert.Equal("yeni", File.ReadAllText(Path.Combine(app, "VidShrink.App.dll")));
        Assert.Equal("yepyeni", File.ReadAllText(UpdateCheck.LocalPath(app, "runtimes/win-x64/native/new.dll")));
        Assert.False(Directory.Exists(stage));
        Assert.False(File.Exists(Path.Combine(app, UpdateStage.JournalName)));
    }

    [Fact]
    public void InterruptedCopyIsFinishedOnTheNextLaunch()
    {
        var app = Folder("app");
        var stage = Folder("stage");
        Write(app, "one.dll", "eski bir");
        Write(app, "two.dll", "eski iki");
        Write(stage, "one.dll", "yeni bir");
        Write(stage, "two.dll", "yeni iki");
        var files = new[] { Describe(stage, "one.dll"), Describe(stage, "two.dll") };

        // Süreç kopyalamanın ortasında ölmüş gibi: günlük yazılı, ilk dosya geçmiş,
        // ikincisi hâlâ eski.
        WriteJournalLikeApply(app, stage, files);
        File.Copy(Path.Combine(stage, "one.dll"), Path.Combine(app, "one.dll"), overwrite: true);

        Assert.Equal("eski iki", File.ReadAllText(Path.Combine(app, "two.dll")));

        Assert.True(UpdateStage.ResumePending(app));

        Assert.Equal("yeni bir", File.ReadAllText(Path.Combine(app, "one.dll")));
        Assert.Equal("yeni iki", File.ReadAllText(Path.Combine(app, "two.dll")));
        Assert.False(File.Exists(Path.Combine(app, UpdateStage.JournalName)));
        Assert.False(Directory.Exists(stage));
    }

    [Fact]
    public void ResumeWithoutJournalDoesNothing()
    {
        var app = Folder("app");
        Write(app, "one.dll", "duruyor");
        Assert.False(UpdateStage.ResumePending(app));
        Assert.Equal("duruyor", File.ReadAllText(Path.Combine(app, "one.dll")));
    }

    [Fact]
    public async Task OnlyTheRequestedFileIsPulledOutOfTheArchive()
    {
        var publish = Folder("publish");
        // Sıkışmayan içerik: arşiv boyutu gerçek yayına benzesin, ölçüm anlamlı olsun.
        WriteRandom(publish, "VidShrink.App.dll", 200_000);
        WriteRandom(publish, "big-runtime.dll", 4_000_000);
        var wanted = WriteRandom(publish, "runtimes/win-x64/native/native.dll", 500_000);
        var archive = Path.Combine(_root, "vidshrink-win-x64.zip");
        ZipFile.CreateFromDirectory(publish, archive);

        var counting = new CountingRangeSource(new FileRangeSource(archive));
        var zip = await RemoteZip.OpenAsync(counting, CancellationToken.None);

        var entry = zip.Resolve("runtimes/win-x64/native/native.dll");
        Assert.NotNull(entry);
        var bytes = await zip.ExtractAsync(entry!, CancellationToken.None);

        Assert.Equal(500_000, bytes.Length);
        Assert.Equal(UpdateCheck.HashFile(wanted), UpdateCheck.HashBytes(bytes));

        var archiveSize = new FileInfo(archive).Length;
        _output.WriteLine($"arşiv {archiveSize} bayt, çekilen {counting.BytesRead} bayt");
        Assert.True(counting.BytesRead < archiveSize / 2,
            $"arşivin tamamı indi: {counting.BytesRead}/{archiveSize}");
    }

    [Fact]
    public void AutoUpdateIsOnUntilTheUserTurnsItOff()
    {
        var file = Path.Combine(_root, "settings.json");

        Assert.True(UpdateSettings.Load(file).AutoUpdate);

        new UpdateSettings { AutoUpdate = false }.Save(file);
        Assert.False(UpdateSettings.Load(file).AutoUpdate);

        new UpdateSettings { AutoUpdate = true }.Save(file);
        Assert.True(UpdateSettings.Load(file).AutoUpdate);
    }

    [Fact]
    public void BrokenSettingsFileFallsBackToTheDefault()
    {
        var file = Path.Combine(_root, "settings.json");
        File.WriteAllText(file, "{ bozuk");
        Assert.True(UpdateSettings.Load(file).AutoUpdate);
    }

    [Fact]
    public void SwitchedOffMeansOnlyNotifyOnEveryPlatform()
    {
        var off = new UpdateSettings { AutoUpdate = false };
        Assert.False(UpdateCheck.AutoUpdateEnabled(off));

        var on = new UpdateSettings { AutoUpdate = true };
        Assert.Equal(OperatingSystem.IsWindows(), UpdateCheck.AutoUpdateEnabled(on));
    }

    [Fact]
    public async Task AnUnreachableSourceIsGivenUpWithinTheManifestTimeout()
    {
        const string unreachable = "http://10.255.255.1/vidshrink/manifest-win-x64.json";
        var ceiling = UpdateCheck.ManifestTimeout + TimeSpan.FromMilliseconds(250);

        await UpdateCheck.FetchManifestAsync(unreachable, CancellationToken.None);

        var stopwatch = Stopwatch.StartNew();
        var json = await UpdateCheck.FetchManifestAsync(unreachable, CancellationToken.None);
        stopwatch.Stop();

        _output.WriteLine(
            $"ağsız manifest denemesi: {stopwatch.Elapsed.TotalMilliseconds:F0} ms " +
            $"(zaman aşımı {UpdateCheck.ManifestTimeout.TotalMilliseconds:F0} ms, " +
            $"tavan {ceiling.TotalMilliseconds:F0} ms)");

        Assert.Null(json);
        Assert.True(stopwatch.Elapsed < ceiling,
            $"ağsız açılış zaman aşımını aştı: {stopwatch.Elapsed.TotalMilliseconds:F0} ms > {ceiling.TotalMilliseconds:F0} ms");
    }

    [Fact]
    public void TheSameVersionIsNotDownloadedASecondTime()
    {
        var app = Folder("app");
        Write(app, "VidShrink.App.dll", "kurulu");
        var manifest = new ReleaseManifest("1.4.0", "abc", DateTimeOffset.UtcNow, "win-x64", new[]
        {
            Describe(app, "VidShrink.App.dll") with { Sha256 = new string('0', 64) }
        });

        Assert.NotEmpty(UpdateCheck.Diff(app, manifest));
        Assert.False(UpdateCheck.AlreadyCurrent(app, manifest));

        UpdateCheck.WriteVersionMarker(app, "1.4.0");
        Assert.True(UpdateCheck.AlreadyCurrent(app, manifest));

        UpdateCheck.WriteVersionMarker(app, "1.3.0");
        Assert.False(UpdateCheck.AlreadyCurrent(app, manifest));
    }

    [Fact]
    public void TheVersionGuardComesBeforeAnythingIsDownloaded()
    {
        var code = File.ReadAllText(
            Path.Combine(TipSources.Root, "src", "VidShrink.Launcher", "Updater.cs"));

        var guard = code.IndexOf("UpdateCheck.AlreadyCurrent", StringComparison.Ordinal);
        var download = code.IndexOf("RemoteZip.OpenAsync", StringComparison.Ordinal);

        Assert.True(guard >= 0, "sürüm kapısı Updater.cs içinde yok");
        Assert.True(download > guard, "arşiv sürüm kapısından önce açılıyor");
        Assert.DoesNotContain("UpdateSchedule", code);
    }

    [Fact]
    public void PendingUpdateIsSeenOnTheNextLaunch()
    {
        var app = Folder("app");
        Assert.False(UpdateStage.HasPending(app));

        File.WriteAllText(Path.Combine(app, UpdateStage.JournalName), "{}");
        Assert.True(UpdateStage.HasPending(app));
    }

    private const string OldLauncher = "eski başlatıcı";
    private const string NewLauncher = "yeni başlatıcı, daha uzun";

    [Fact]
    public void TheManifestCountsTheLauncherInItsOwnField()
    {
        const string json = """
        {
          "version": "0.2.2",
          "commit": "abc1234",
          "built": "2026-08-23T18:00:00Z",
          "rid": "win-x64",
          "files": [
            { "path": "VidShrink.App.dll", "sha256": "e3b0c442", "size": 123456 }
          ],
          "launcher": [
            { "path": "VidShrink.exe", "sha256": "AB12", "size": 7788 }
          ]
        }
        """;

        var manifest = UpdateCheck.ParseManifest(json);

        Assert.Single(manifest.Launcher);
        Assert.Equal(LauncherUpdate.ExecutableName, manifest.Launcher[0].Path);
        Assert.Equal("ab12", manifest.Launcher[0].Sha256);
        Assert.Equal(7788, manifest.Launcher[0].Size);

        // Ayrı alan olmasının sebebi: files listesine önekli bir satır girseydi kurulu her
        // eski başlatıcı o satırı uygulama arşivinde arar, bulamaz ve güncellemenin
        // tamamından vazgeçerdi. Bilinmeyen bir üst alan ise sessizce görünmez.
        Assert.Single(manifest.Files);
        Assert.DoesNotContain(manifest.Files, file => file.Path.Contains("VidShrink.exe", StringComparison.Ordinal));
    }

    [Fact]
    public void AManifestWithoutALauncherFieldIsStillRead()
    {
        var manifest = UpdateCheck.ParseManifest("""
        { "version": "0.2.0", "rid": "win-x64", "files": [ { "path": "a.dll", "sha256": "aa", "size": 1 } ] }
        """);

        Assert.Empty(manifest.Launcher);
        Assert.Single(manifest.Files);
    }

    [Fact]
    public void TheWorkflowFoldsTheLauncherIntoTheManifest()
    {
        var workflow = File.ReadAllText(
            Path.Combine(TipSources.Root, ".github", "workflows", "release.yml"));

        // Başlatıcının kendi yayın klasörü ayrıca özetleniyor ve aynı manifestin
        // launcher alanına katlanıyor.
        Assert.Contains("write publish-launcher", workflow, StringComparison.Ordinal);
        Assert.Contains(".launcher = (", workflow, StringComparison.Ordinal);
        Assert.Contains($"select(.path == \"{LauncherUpdate.ExecutableName}\")", workflow, StringComparison.Ordinal);

        // Katlama arşivlemeden önce olmalı: manifest hem arşivin içine hem yanına gidiyor.
        var merge = workflow.IndexOf("merged-manifest.json", StringComparison.Ordinal);
        var archive = workflow.IndexOf("Archive, copy the manifest out", StringComparison.Ordinal);
        Assert.True(merge >= 0, "manifest katlama adımı release.yml'de yok");
        Assert.True(archive > merge, "manifest arşivlendikten sonra katlanıyor");
    }

    [Fact]
    public void TheLauncherComesOutOfTheArchiveTheReleaseAlreadyCarries()
    {
        Assert.Equal("vidshrink-launcher-win-x64.zip", UpdateCheck.LauncherArchiveAssetName("win-x64"));

        var workflow = File.ReadAllText(
            Path.Combine(TipSources.Root, ".github", "workflows", "release.yml"));
        Assert.Contains("vidshrink-launcher-${{ matrix.rid }}.zip", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRunningBinaryIsReplacedOnlyAfterItStopsRunning()
    {
        var root = Folder("running");
        var target = Path.Combine(root, LauncherUpdate.ExecutableName);
        File.WriteAllText(target, OldLauncher);
        var file = StageIncoming(root);

        // Çalışan bir görüntü dosyasının paylaşım kipi: okunabilir ve adı değiştirilebilir,
        // üstüne yazılamaz. Başlatıcı bu kipteyken değişimin yalnız kurulması beklenir.
        using (new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        {
            if (OperatingSystem.IsWindows())
                Assert.ThrowsAny<IOException>(() => File.Copy(LauncherUpdate.Incoming(root, file.Path), target, overwrite: true));

            Assert.True(LauncherUpdate.Arm(root, new[] { file }, "0.2.2+abc1234"));

            // Kurulum adımı hedefe dokunmuyor: kısayolun gösterdiği dosya hâlâ eski ikili.
            Assert.Equal(OldLauncher, File.ReadAllText(target));
            Assert.True(File.Exists(Path.Combine(root, LauncherUpdate.JournalName)));
        }

        Assert.True(LauncherUpdate.Commit(root));

        Assert.Equal(NewLauncher, File.ReadAllText(target));
        Assert.Equal("0.2.2", LauncherUpdate.ReadVersionMarker(root));
        Assert.False(File.Exists(Path.Combine(root, LauncherUpdate.JournalName)));
        Assert.False(File.Exists(LauncherUpdate.Incoming(root, file.Path)));
    }

    [Fact]
    public void TheIncomingNameStaysAnExecutable()
    {
        // Geçişi yapan süreç bu dosyanın kendisi olduğu için adın .exe kalması gerekiyor.
        var root = Folder("adlar");
        Assert.Equal(Path.Combine(root, "VidShrink.new.exe"), LauncherUpdate.Incoming(root, LauncherUpdate.ExecutableName));
        Assert.Equal(Path.Combine(root, "VidShrink.old.exe"), LauncherUpdate.Retired(root, LauncherUpdate.ExecutableName));
    }

    [Fact]
    public void ALeftoverRetiredNameIsSweptOnTheNextLaunch()
    {
        var root = Folder("retired");
        var target = Path.Combine(root, LauncherUpdate.ExecutableName);
        File.WriteAllText(target, NewLauncher);
        File.WriteAllText(LauncherUpdate.Retired(root, LauncherUpdate.ExecutableName), OldLauncher);

        Assert.False(LauncherUpdate.Repair(root));

        Assert.False(File.Exists(LauncherUpdate.Retired(root, LauncherUpdate.ExecutableName)));
        Assert.Equal(NewLauncher, File.ReadAllText(target));
    }

    /// <summary>
    /// Değişimin adımları, üretimde çağrıldıkları hâlleriyle. Ölçü ilk
    /// <paramref name="completedSteps"/> tanesini koşturur ve her adımdan sonra kurulum
    /// kökünde açılabilir bir <c>VidShrink.exe</c> kaldığını doğrular.
    ///
    /// <b>Burada kurtarma çağrılmıyor.</b> Bir önceki tur bu ölçüyü <c>Repair</c> çağırarak
    /// yapıyordu; üretimde <c>Repair</c>'i çağıracak tek şey <c>VidShrink.exe</c>'nin kendisi
    /// olduğu için, adın boşaldığı bir adımda çağıracak kimse kalmıyordu. Adın hiç boşalmaması
    /// ancak kurtarma olmadan gösterilirse gösterilmiş sayılır.
    /// </summary>
    [Theory]
    [InlineData(0, OldLauncher)]
    [InlineData(1, OldLauncher)]
    [InlineData(2, OldLauncher)]
    [InlineData(3, NewLauncher)]
    public void TheTargetNameNeverEmptiesAtAnyStepOfTheSwap(int completedSteps, string expected)
    {
        var root = Folder("kesinti" + completedSteps);
        var target = Path.Combine(root, LauncherUpdate.ExecutableName);
        File.WriteAllText(target, OldLauncher);

        var stage = Path.Combine(root, "update-stage");
        var file = Describe(WriteStagedLauncher(stage));

        var steps = new (string Name, Action Run)[]
        {
            ("yeni ikili yan ada indi", () => LauncherUpdate.Stage(stage, root, new[] { file })),
            ("günlük yazıldı", () => Assert.True(LauncherUpdate.Arm(root, new[] { file }, "0.2.2"))),
            ("geçiş yapıldı", () => Assert.True(LauncherUpdate.Commit(root)))
        };

        AssertLauncherIsRunnable(root, 0);
        for (var step = 0; step < completedSteps; step++)
        {
            steps[step].Run();
            AssertLauncherIsRunnable(root, step + 1);
            _output.WriteLine($"{step + 1}. adım ({steps[step].Name}) sonrası kökte: {RootListing(root)}");
        }

        Assert.Equal(expected, File.ReadAllText(target));
    }

    /// <summary>
    /// Adımlar arasında değil, geçişin <em>içinde</em> yoklar: geçiş koşarken kurulum kökü
    /// aralıksız taranır. Ad bir kez bile yok görünürse ölçü düşer.
    /// </summary>
    [Fact]
    public void TheNameIsNeverAbsentWhileTheSwapItselfRuns()
    {
        var atomic = Hammer("atomik", root =>
        {
            ArmedSwap(root);
            return () => Assert.True(LauncherUpdate.Commit(root));
        }, MinimumRounds, untilCaught: false);

        _output.WriteLine($"geçiş: {atomic.Rounds} tur, {atomic.Samples} yoklama, {atomic.Misses} kez ad yok");
        Assert.True(atomic.Samples > atomic.Rounds, "yoklama gerçekten koşmadı");
        Assert.Equal(0, atomic.Misses);
    }

    /// <summary>
    /// Yukarıdaki yoklamanın dişi var mı. Aynı yoklamaya, adı iki adımda değiştiren eski
    /// yordam sokulur: önce eski ad yana alınır, sonra yeni ikili geçer. Bu hâlde adın
    /// kaybolduğu görülmüyorsa yukarıdaki ölçü hiçbir şey kanıtlamıyor demektir.
    /// </summary>
    [Fact]
    public void TheProbeCatchesATwoStepSwapSoTheMeasureHasTeeth()
    {
        var twoStep = Hammer("ikiadim", root =>
        {
            var target = Path.Combine(root, LauncherUpdate.ExecutableName);
            File.WriteAllText(target, OldLauncher);
            var incoming = LauncherUpdate.Incoming(root, LauncherUpdate.ExecutableName);
            File.WriteAllText(incoming, NewLauncher);
            var retired = LauncherUpdate.Retired(root, LauncherUpdate.ExecutableName);
            return () =>
            {
                MoveOverTransientLock(target, retired);
                MoveOverTransientLock(incoming, target);
            };
        }, MinimumRounds, untilCaught: true);

        _output.WriteLine($"iki adımlı yordam: {twoStep.Rounds} tur, {twoStep.Samples} yoklama, {twoStep.Misses} kez ad yok");
        Assert.True(twoStep.Misses > 0,
            $"{twoStep.Rounds} turda iki adımlı yordamın açtığı boşluk hiç görülmedi; yoklamanın dişi yok");
    }

    [Fact]
    public void AHalfDoneSwapWithoutANewBinaryLeavesTheOldLauncherInPlace()
    {
        // Elde geçirilecek sağlam bir ikili kalmamış. Hedefe dokunulmaz, günlük kapatılır,
        // sürüm işareti ilerlemez ve bir sonraki tur aynı güncellemeyi yeniden dener.
        var root = Folder("gerial");
        var target = Path.Combine(root, LauncherUpdate.ExecutableName);
        File.WriteAllText(target, OldLauncher);
        var file = ArmedSwap(root);
        File.Delete(LauncherUpdate.Incoming(root, file.Path));

        Assert.False(LauncherUpdate.Repair(root));

        Assert.Equal(OldLauncher, File.ReadAllText(target));
        Assert.Null(LauncherUpdate.ReadVersionMarker(root));
        Assert.False(File.Exists(Path.Combine(root, LauncherUpdate.JournalName)));
    }

    [Fact]
    public void APendingSwapIsReportedInsteadOfAppliedWhileTheLauncherRuns()
    {
        // Açılışta koşan kurtarma hedefe dokunmaz; yalnız bekleyen geçişi bildirir ki
        // çıkışta yerine geçecek ikili çağrılsın.
        var root = Folder("bekleyen");
        var target = Path.Combine(root, LauncherUpdate.ExecutableName);
        File.WriteAllText(target, OldLauncher);
        ArmedSwap(root);

        Assert.True(LauncherUpdate.Repair(root));

        Assert.Equal(OldLauncher, File.ReadAllText(target));
        Assert.True(File.Exists(Path.Combine(root, LauncherUpdate.JournalName)));
        Assert.Null(LauncherUpdate.ReadVersionMarker(root));
    }

    [Fact]
    public void APendingSwapIsDroppedWhenTheInstallerAlreadyMovedTheLauncherForward()
    {
        // Kurulum betiği aradan geçip başlatıcıyı elle yenilemiş olabilir. O hâlde bekleyen
        // kayıt bir güncelleme değil, geri alma olurdu.
        var root = Folder("kurulumgecti");
        File.WriteAllText(Path.Combine(root, LauncherUpdate.ExecutableName), OldLauncher);
        ArmedSwap(root);

        Assert.False(LauncherUpdate.Repair(root, "0.2.3+abc1234"));
        Assert.False(File.Exists(Path.Combine(root, LauncherUpdate.JournalName)));
        Assert.Equal(OldLauncher, File.ReadAllText(Path.Combine(root, LauncherUpdate.ExecutableName)));

        // Geride kalmış bir başlatıcıda aynı kayıt duruyor.
        ArmedSwap(root);
        Assert.True(LauncherUpdate.Repair(root, "0.2.1"));
    }

    [Fact]
    public void TheVersionMarkerIsOnlyMovedForwardWhenTheSwapReallyLanded()
    {
        var root = Folder("isaret");
        var target = Path.Combine(root, LauncherUpdate.ExecutableName);
        File.WriteAllText(target, OldLauncher);
        ArmedSwap(root);

        Assert.Null(LauncherUpdate.ReadVersionMarker(root));
        Assert.True(LauncherUpdate.Commit(root));
        Assert.Equal("0.2.2", LauncherUpdate.ReadVersionMarker(root));
    }

    [Fact]
    public void AnUnverifiedLauncherVersionIsNeverClaimed()
    {
        // Manifest başlatıcıyı hiç saymıyorsa kurulu başlatıcının o sürüm olduğu bilinemez.
        var root = Folder("iddia");
        var app = Folder(Path.Combine("iddia", "app"));
        Write(app, "VidShrink.App.dll", "uygulama");

        var withoutLauncher = new ReleaseManifest("0.2.2", "abc", DateTimeOffset.UtcNow, "win-x64",
            new[] { Describe(app, "VidShrink.App.dll") });

        Assert.False(LauncherUpdate.MarkVerified(root, withoutLauncher));
        Assert.Null(LauncherUpdate.ReadVersionMarker(root));

        var withLauncher = withoutLauncher with
        {
            Launcher = new[] { new ManifestFile(LauncherUpdate.ExecutableName, new string('0', 64), 10) }
        };

        Assert.True(LauncherUpdate.MarkVerified(root, withLauncher));
        Assert.Equal("0.2.2", LauncherUpdate.ReadVersionMarker(root));
    }

    [Fact]
    public void TheLauncherIsArmedOnlyAfterTheApplicationFilesAreInPlace()
    {
        var (root, app, stage, appFiles, launcherFiles, manifest) = Rollout("sira");

        Assert.True(UpdateRollout.Apply(stage, root, app, appFiles, launcherFiles, manifest));

        Assert.Equal("yeni uygulama", File.ReadAllText(Path.Combine(app, "VidShrink.App.dll")));
        Assert.True(File.Exists(Path.Combine(root, LauncherUpdate.JournalName)));
        Assert.Equal("0.2.2", UpdateCheck.ReadVersionMarker(app));
    }

    [Fact]
    public void TheLauncherIsNotArmedWhenTheApplicationFilesFailToLand()
    {
        // Sıranın davranıştaki karşılığı: uygulama adımı düşerse başlatıcı hiç kurulmaz.
        // Ters sırada olsaydı günlük yazılmış olur ve yeni başlatıcı eski uygulamayı açardı.
        var (root, app, stage, appFiles, launcherFiles, manifest) = Rollout("sirabozuk");
        File.WriteAllText(UpdateCheck.LocalPath(stage, "VidShrink.App.dll"), "bozuk");

        Assert.Throws<InvalidDataException>(
            () => UpdateRollout.Apply(stage, root, app, appFiles, launcherFiles, manifest));

        Assert.False(File.Exists(Path.Combine(root, LauncherUpdate.JournalName)));
        Assert.Null(LauncherUpdate.ReadVersionMarker(root));
        Assert.Equal("eski uygulama", File.ReadAllText(Path.Combine(app, "VidShrink.App.dll")));
    }

    private (string Root, string App, string Stage, IReadOnlyList<ManifestFile> AppFiles,
        IReadOnlyList<ManifestFile> LauncherFiles, ReleaseManifest Manifest) Rollout(string name)
    {
        var root = Folder(name);
        var app = Folder(Path.Combine(name, "app"));
        var stage = Path.Combine(root, "update-stage");
        Write(app, "VidShrink.App.dll", "eski uygulama");
        Write(stage, "VidShrink.App.dll", "yeni uygulama");

        var appFile = Describe(stage, "VidShrink.App.dll");
        var launcherFile = Describe(WriteStagedLauncher(stage));
        LauncherUpdate.Stage(stage, root, new[] { launcherFile });

        var manifest = new ReleaseManifest("0.2.2", "abc", DateTimeOffset.UtcNow, "win-x64", new[] { appFile })
        {
            Launcher = new[] { launcherFile }
        };
        return (root, app, stage, new[] { appFile }, new[] { launcherFile }, manifest);
    }

    /// <summary>İnen başlatıcıyı yan klasöre yazar ve yolunu döndürür.</summary>
    private static string WriteStagedLauncher(string stageDirectory)
    {
        var staged = LauncherUpdate.StagePath(stageDirectory, LauncherUpdate.ExecutableName);
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        File.WriteAllText(staged, NewLauncher);
        return staged;
    }

    private static ManifestFile Describe(string fullPath) =>
        new(LauncherUpdate.ExecutableName, UpdateCheck.HashFile(fullPath), new FileInfo(fullPath).Length);

    /// <summary>İnen ikiliyi yan ada taşır ve günlüğü yazar; üretimdeki iki adımın aynısı.</summary>
    private static ManifestFile StageIncoming(string root)
    {
        var stage = Path.Combine(root, "update-stage");
        var file = Describe(WriteStagedLauncher(stage));
        LauncherUpdate.Stage(stage, root, new[] { file });
        return file;
    }

    private static ManifestFile ArmedSwap(string root)
    {
        var target = Path.Combine(root, LauncherUpdate.ExecutableName);
        if (!File.Exists(target)) File.WriteAllText(target, OldLauncher);
        var file = StageIncoming(root);
        LauncherUpdate.Arm(root, new[] { file }, "0.2.2");
        return file;
    }

    private void AssertLauncherIsRunnable(string root, int step)
    {
        var names = Directory.GetFiles(root).Select(Path.GetFileName).ToArray();
        Assert.True(
            names.Contains(LauncherUpdate.ExecutableName),
            $"{step}. adımdan sonra kurulum kökünde {LauncherUpdate.ExecutableName} yok; kökte duranlar: {string.Join(", ", names)}");

        var content = File.ReadAllText(Path.Combine(root, LauncherUpdate.ExecutableName));
        Assert.True(
            content == OldLauncher || content == NewLauncher,
            $"{step}. adımdan sonra kökteki başlatıcı çalışabilir bir ikili değil");
    }

    private static string RootListing(string root) =>
        string.Join(", ", Directory.GetFiles(root).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));

    private const int MinimumRounds = 120;

    /// <summary>
    /// Windows tazeyken yazılan dosyayı kısa süre tutabiliyor (tarayıcı, dizinleyici).
    /// Ölçünün iddiası adın boşaldığının görülmesi; taşımanın kendisi o iddianın parçası
    /// değil, kurulumu. <see cref="Hammer"/>'ın temizlik adımındaki sınırlı yeniden deneme
    /// burada da geçerli.
    /// </summary>
    private static void MoveOverTransientLock(string from, string to)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(from, to);
                return;
            }
            catch (IOException) when (attempt < 20)
            {
                Thread.Sleep(25);
            }
        }
    }

    /// <summary>
    /// Aynı değişimi çok kez koşturur ve her turda kurulum kökünü aralıksız yoklayan bir
    /// iplik tutar. <paramref name="untilCaught"/> açıkken ad bir kez yok görülene kadar
    /// (süre sınırıyla) devam eder; bu, yoklamanın dişini ölçmek içindir.
    /// </summary>
    private (int Rounds, long Samples, long Misses) Hammer(string name, Func<string, Action> arrange, int minimumRounds, bool untilCaught)
    {
        long samples = 0;
        long misses = 0;
        var rounds = 0;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (rounds < minimumRounds || (untilCaught && misses == 0 && DateTime.UtcNow < deadline))
        {
            var root = Folder(name + rounds);
            var target = Path.Combine(root, LauncherUpdate.ExecutableName);
            var run = arrange(root);

            var probe = new NameProbe(target);
            try { run(); }
            finally { probe.Stop(); }

            samples += probe.Samples;
            misses += probe.Misses;
            rounds++;

            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                    break;
                }
                catch (IOException) when (attempt < 20)
                {
                    Thread.Sleep(25);
                }
            }
        }

        return (rounds, samples, misses);
    }

    /// <summary>Bir adı, durdurulana kadar aralıksız yoklayan iplik.</summary>
    private sealed class NameProbe
    {
        private readonly string _path;
        private readonly Thread _thread;
        private int _stop;
        private long _samples;
        private long _misses;

        public NameProbe(string path)
        {
            _path = path;
            _thread = new Thread(Loop) { IsBackground = true };
            _thread.Start();
            while (Interlocked.Read(ref _samples) == 0) Thread.SpinWait(20);
        }

        public long Samples => Interlocked.Read(ref _samples);

        public long Misses => Interlocked.Read(ref _misses);

        public void Stop()
        {
            Volatile.Write(ref _stop, 1);
            _thread.Join();
        }

        private void Loop()
        {
            while (Volatile.Read(ref _stop) == 0)
            {
                var present = File.Exists(_path);
                if (!present) Interlocked.Increment(ref _misses);
                Interlocked.Increment(ref _samples);
            }
        }
    }

    /// <summary>
    /// Geçiş sürecine verilen duvar saati tavanı. Süreç tek bir yeniden adlandırma yapıp
    /// çıkıyor; ölçülen değer tek parçalı kendi kendine açılan ikilinin açılış payıdır.
    ///
    /// Eski tavan 60 sn idi ve neye dayandığı yazılı değildi. Ölçüldü: yayımlanmış
    /// başlatıcıyla, on dört ajanın koştuğu meşgul bir makinede beş koşum
    /// 101/81/84/117/82 ms verdi. Tavan en yüksek okumanın kırk katına indirildi; tek
    /// parçalı ikilinin ilk açılışta ödediği açma payı da bu aralığın içinde kalır.
    /// Ölçüm <c>docs/olcumler/kalan-alti-bant.md</c> içinde.
    /// </summary>
    private const int GecisTavaniMs = 5_000;

    /// <summary>
    /// Geçişin üretimdeki hâli, gerçek bir süreçle. Yerine geçecek ikili kendi süreci içinde
    /// açılır ve kendi adını hedefin üstüne alır; ölçü kurulu bir başlatıcı gösterildiğinde
    /// koşar. Yukarıdaki ölçüler adın hiç boşalmadığını süreç içinde gösteriyor, bu ölçü de
    /// o geçişi yapacak sürecin gerçekten açılabildiğini gösteriyor.
    /// </summary>
    [LiveLauncherFact]
    public void TheIncomingBinaryRenamesItselfOntoTheTargetName()
    {
        var exe = Environment.GetEnvironmentVariable("VIDSHRINK_LAUNCHER_EXE")!;
        var root = Folder("canli");
        var target = Path.Combine(root, LauncherUpdate.ExecutableName);
        File.WriteAllText(target, OldLauncher);

        var incoming = LauncherUpdate.Incoming(root, LauncherUpdate.ExecutableName);
        File.Copy(exe, incoming);
        var file = new ManifestFile(
            LauncherUpdate.ExecutableName, UpdateCheck.HashFile(incoming), new FileInfo(incoming).Length);
        Assert.True(LauncherUpdate.Arm(root, new[] { file }, "0.2.2"));

        var start = new ProcessStartInfo { FileName = incoming, WorkingDirectory = root, UseShellExecute = false };
        start.ArgumentList.Add(LauncherUpdate.CommitArgument);
        using var process = Process.Start(start)!;
        var stopwatch = Stopwatch.StartNew();
        var exited = process.WaitForExit(GecisTavaniMs);
        stopwatch.Stop();
        Assert.True(exited, $"geçiş süreci {GecisTavaniMs} ms içinde çıkmadı");

        _output.WriteLine(
            $"geçiş süresi: {stopwatch.Elapsed.TotalMilliseconds:F0} ms, tavan {GecisTavaniMs} ms");
        _output.WriteLine($"çıkış kodu: {process.ExitCode}, kökte: {RootListing(root)}");
        Assert.Equal(0, process.ExitCode);
        Assert.True(LauncherUpdate.Matches(target, file), "hedef, inen ikilinin özetine oturmadı");
        Assert.False(File.Exists(incoming));
        Assert.Equal("0.2.2", LauncherUpdate.ReadVersionMarker(root));
        Assert.False(File.Exists(Path.Combine(root, LauncherUpdate.JournalName)));
    }

    [LiveLauncherFact]
    public void EveryLaunchChecksAndStaysWithinTheTimeout()
    {
        var exe = Environment.GetEnvironmentVariable("VIDSHRINK_LAUNCHER_EXE")!;
        var settings = Path.Combine(_root, "settings.json");
        new UpdateSettings { AutoUpdate = true }.Save(settings);

        // 10.255.255.1 yönlendirilemeyen adres: ağ yokmuş gibi davranır.
        var offlineFirst = MeasureLaunch(exe, settings, "http://10.255.255.1/vidshrink");
        var offlineSecond = MeasureLaunch(exe, settings, "http://10.255.255.1/vidshrink");
        var online = MeasureLaunch(exe, settings, null);

        _output.WriteLine($"ağsız ilk açılış: {offlineFirst.TotalMilliseconds:F0} ms");
        _output.WriteLine($"ağsız ikinci açılış: {offlineSecond.TotalMilliseconds:F0} ms");
        _output.WriteLine($"ağlı açılış (güncelleme yok): {online.TotalMilliseconds:F0} ms");
        Assert.True(offlineFirst < TimeSpan.FromSeconds(3), $"ağsız açılış çok uzun: {offlineFirst}");
        Assert.True(offlineSecond < TimeSpan.FromSeconds(3), $"ağsız ikinci açılış çok uzun: {offlineSecond}");
    }

    [LiveLauncherFact]
    public void SwitchedOffLauncherMakesNoNetworkRequestAtAll()
    {
        var exe = Environment.GetEnvironmentVariable("VIDSHRINK_LAUNCHER_EXE")!;
        var settings = Path.Combine(_root, "settings.json");

        using var server = new CountingHttpServer();

        new UpdateSettings { AutoUpdate = false }.Save(settings);
        var withoutUpdates = MeasureLaunch(exe, settings, server.BaseUrl);
        var requestsWhileOff = server.Requests;

        new UpdateSettings { AutoUpdate = true }.Save(settings);
        var withUpdates = MeasureLaunch(exe, settings, server.BaseUrl);
        var requestsWhileOn = server.Requests - requestsWhileOff;

        _output.WriteLine($"ayar kapalı: {requestsWhileOff} istek, {withoutUpdates.TotalMilliseconds:F0} ms");
        _output.WriteLine($"ayar açık: {requestsWhileOn} istek, {withUpdates.TotalMilliseconds:F0} ms");

        Assert.Equal(0, requestsWhileOff);
        Assert.True(requestsWhileOn > 0, "ayar açıkken manifest hiç istenmedi");
    }

    private static TimeSpan MeasureLaunch(string exe, string settingsPath, string? baseUrl)
    {
        var start = new ProcessStartInfo { FileName = exe, UseShellExecute = false };
        start.Environment["VIDSHRINK_SETTINGS_PATH"] = settingsPath;
        if (baseUrl is not null) start.Environment["VIDSHRINK_UPDATE_BASE_URL"] = baseUrl;
        var stopwatch = Stopwatch.StartNew();
        using var process = Process.Start(start)!;
        process.WaitForExit();
        stopwatch.Stop();
        foreach (var running in Process.GetProcessesByName("VidShrink.App"))
        {
            try { running.Kill(); } catch (InvalidOperationException) { }
            running.Dispose();
        }
        return stopwatch.Elapsed;
    }

    /// <summary>Başlatıcının ağa hiç çıkmadığını göstermek için istekleri sayan yerel sunucu.</summary>
    private sealed class CountingHttpServer : IDisposable
    {
        private readonly HttpListener _listener = new();

        public CountingHttpServer()
        {
            var port = 18000 + Random.Shared.Next(1000);
            BaseUrl = $"http://localhost:{port}";
            _listener.Prefixes.Add(BaseUrl + "/");
            _listener.Start();
            Task.Run(async () =>
            {
                while (_listener.IsListening)
                {
                    HttpListenerContext context;
                    try { context = await _listener.GetContextAsync(); }
                    catch (Exception) { return; }
                    Interlocked.Increment(ref _requests);
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            });
        }

        private int _requests;

        public string BaseUrl { get; }

        public int Requests => Volatile.Read(ref _requests);

        public void Dispose() => _listener.Close();
    }

    /// <summary>
    /// T77-1: kurulum betiğinin silme yordamı, betiğin kendi metninden çıkarılıp koşturulur.
    /// Kilit gerçek: dosya <c>FileShare.None</c> ile açık tutulur, tıpkı iki kurulumu düşüren
    /// <c>app\Avalonia.Base.dll</c> hâlindeki gibi. Betiğin tamamı koşturulmuyor — koştuğu anda
    /// bu makinedeki kurulumu siler.
    /// </summary>
    private const string RemovalProbe = @"param([string]$Root, [string]$Out)
$ErrorActionPreference = 'Stop'

function Write-Host {{
    param([Parameter(Position = 0, ValueFromPipeline = $true)]$Object, [string]$ForegroundColor)
    $line = [string]$Object
    $akis = [System.IO.File]::Open(($Out + '.akis'), 'Append', 'Write', 'Read')
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($line + ""`n"")
    $akis.Write($bytes, 0, $bytes.Length)
    $akis.Flush()
    $akis.Dispose()
    if ($ForegroundColor) {{ Microsoft.PowerShell.Utility\Write-Host $line -ForegroundColor $ForegroundColor }}
    else {{ Microsoft.PowerShell.Utility\Write-Host $line }}
}}

{0}

$log = @()
$code = 0
try {{
    $notes = Remove-InstallRoot $Root 6>&1 | ForEach-Object {{ [string]$_ }}
    $log += 'BITTI'
    if ($notes) {{ $log += $notes }}
}}
catch {{
    $log += 'HATA'
    $log += [string]$_.Exception.Message
    $code = 3
}}
Set-Content -LiteralPath $Out -Value ($log -join ""`n"") -Encoding UTF8
exit $code
";

    private static string InstallerBlock(string script, string header)
    {
        var start = script.IndexOf(header, StringComparison.Ordinal);
        Assert.True(start >= 0, $"kurulum betiğinde yok: {header}");

        var depth = 0;
        for (var index = script.IndexOf('{', start); index >= 0 && index < script.Length; index++)
        {
            if (script[index] == '{') depth++;
            else if (script[index] == '}' && --depth == 0) return script[start..(index + 1)];
        }

        throw new InvalidDataException($"kurulum betiğinde kapanmıyor: {header}");
    }

    private string WriteRemovalProbe(string folder)
    {
        var script = File.ReadAllText(Path.Combine(TipSources.Root, "Install-VidShrink.ps1"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        var knobs = script.Split('\n')
            .Where(line => line.StartsWith("$script:Remove", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, knobs.Length);

        var routine = string.Join("\n", knobs) + "\n\n"
            + InstallerBlock(script, "function Get-InstallRootHolder") + "\n\n"
            + InstallerBlock(script, "function Remove-InstallRoot");

        var path = Path.Combine(folder, "silme.ps1");
        File.WriteAllText(
            path,
            string.Format(CultureInfo.InvariantCulture, RemovalProbe, routine),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    private static string SafeRead(string path)
    {
        try
        {
            using var handle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(handle, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return reader.ReadToEnd();
        }
        catch (IOException) { return string.Empty; }
        catch (UnauthorizedAccessException) { return string.Empty; }
    }

    private static bool NoticeSeen(string path, string needle) =>
        SafeRead(path).Contains(needle, StringComparison.Ordinal);

    private static (int Code, string Log) RunRemovalProbe(string probe, string installRoot, string logPath)
    {
        var info = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", probe, "-Root", installRoot, "-Out", logPath })
            info.ArgumentList.Add(argument);

        using var process = Process.Start(info)!;
        var noise = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        var log = File.Exists(logPath) ? File.ReadAllText(logPath) : noise;
        return (process.ExitCode, log);
    }

    private (string InstallRoot, string Locked, string Probe, string Log) LockedInstall(string name)
    {
        var work = Folder(name);
        var installRoot = Path.Combine(work, "kurulum");
        var locked = UpdateCheck.LocalPath(installRoot, "app/Avalonia.Base.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(locked)!);
        File.WriteAllText(locked, "kilitli dosya");
        File.WriteAllText(Path.Combine(installRoot, LauncherUpdate.ExecutableName), OldLauncher);
        return (installRoot, locked, WriteRemovalProbe(work), Path.Combine(work, "gunluk.txt"));
    }

    [Fact]
    public void TheDeletionStepWaitsOutATransientLock()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (installRoot, locked, probe, logPath) = LockedInstall("silme-gecici");
        var notices = logPath + ".akis";
        var stream = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);
        var releasedOnNotice = false;
        var release = new Thread(() =>
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                if (NoticeSeen(notices, "yeniden denenecek")) { releasedOnNotice = true; break; }
                if (File.Exists(logPath)) break;
                Thread.Sleep(5);
            }

            stream.Dispose();
        }) { IsBackground = true };
        release.Start();

        var stopwatch = Stopwatch.StartNew();
        var (code, log) = RunRemovalProbe(probe, installRoot, logPath);
        stopwatch.Stop();
        Assert.True(release.Join(TimeSpan.FromSeconds(10)), "kilidi bırakan iş parçacığı bitmedi");

        _output.WriteLine($"geçici kilit: çıkış {code}, {stopwatch.Elapsed.TotalMilliseconds:F0} ms");
        _output.WriteLine(log.Trim());

        Assert.True(releasedOnNotice, $"kilit yeniden deneme duyurusu görülmeden bırakıldı: {SafeRead(notices)}");
        Assert.Equal(0, code);
        Assert.Contains("BITTI", log, StringComparison.Ordinal);
        Assert.Contains("yeniden denenecek", log, StringComparison.Ordinal);
        Assert.False(Directory.Exists(installRoot), "kurulum kökü silinmedi");
    }

    [Fact]
    public void TheDeletionStepGivesUpWithAMessageThatSaysWhatHappened()
    {
        if (!OperatingSystem.IsWindows()) return;

        var (installRoot, locked, probe, logPath) = LockedInstall("silme-tukendi");
        using var stream = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);

        var stopwatch = Stopwatch.StartNew();
        var (code, log) = RunRemovalProbe(probe, installRoot, logPath);
        stopwatch.Stop();

        _output.WriteLine($"kalıcı kilit: çıkış {code}, {stopwatch.Elapsed.TotalMilliseconds:F0} ms");
        _output.WriteLine(log.Trim());

        Assert.Equal(3, code);
        Assert.Contains("HATA", log, StringComparison.Ordinal);
        Assert.Contains("başka bir süreçte açık", log, StringComparison.Ordinal);
        Assert.Contains(installRoot, log, StringComparison.Ordinal);
        Assert.True(File.Exists(locked), "kilitli dosya silinmiş görünüyor");
        Assert.True(stopwatch.Elapsed > TimeSpan.FromSeconds(5), $"geri çekilme adımları koşmadı: {stopwatch.Elapsed}");
    }

    /// <summary>
    /// T77-2: <c>Program.cs</c>'te bekleyen geçişi başlatan çağrıyı tutan ölçü. Çağrı
    /// silinirse burası kırmızıya döner; kablo yalnız kaynak metinde tutulabiliyor, çünkü
    /// <c>Main</c> ölçüden çağrılamaz.
    /// </summary>
    [Fact]
    public void TheLauncherStartsTheCommitterOnTheWayOut()
    {
        var code = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.Launcher", "Program.cs"));

        var launch = code.IndexOf("Process.Start(start);", StringComparison.Ordinal);
        var gate = code.IndexOf("if (pendingSwap)", StringComparison.Ordinal);
        var call = code.IndexOf("StartCommitter(baseDirectory);", StringComparison.Ordinal);

        Assert.True(call >= 0, "Program.cs bekleyen geçişi başlatan çağrıyı taşımıyor");
        Assert.True(gate >= 0 && gate < call, "geçiş çağrısı bekleyen geçiş kapısının içinde değil");
        Assert.True(launch >= 0 && launch < call, "geçiş, uygulama başlatılmadan önce kuruluyor");
        Assert.Contains("private static void StartCommitter(string baseDirectory)", code, StringComparison.Ordinal);
        Assert.Contains("LauncherUpdate.Commit(baseDirectory, ParentProcessId(args))", code, StringComparison.Ordinal);
    }

    /// <summary>T77-3: <c>Repair</c>'deki sürüm kapısının <c>Commit</c>'teki eşi.</summary>
    [Fact]
    public void TheSwapIsNotCommittedOntoANewerInstalledLauncher()
    {
        var root = Folder("surumkapisi");
        var target = Path.Combine(root, LauncherUpdate.ExecutableName);
        File.WriteAllText(target, OldLauncher);
        ArmedSwap(root);

        LauncherUpdate.WriteVersionMarker(root, "0.2.3");

        Assert.False(LauncherUpdate.Commit(root));
        Assert.Equal(OldLauncher, File.ReadAllText(target));
        Assert.False(File.Exists(Path.Combine(root, LauncherUpdate.JournalName)));
        Assert.False(File.Exists(LauncherUpdate.Incoming(root, LauncherUpdate.ExecutableName)));
        Assert.Equal("0.2.3", LauncherUpdate.ReadVersionMarker(root));
    }

    [Fact]
    public void TheSwapIsStillCommittedOverAnOlderInstalledLauncher()
    {
        var root = Folder("surumkapisi-eski");
        var target = Path.Combine(root, LauncherUpdate.ExecutableName);
        File.WriteAllText(target, OldLauncher);
        ArmedSwap(root);

        LauncherUpdate.WriteVersionMarker(root, "0.2.1");

        Assert.True(LauncherUpdate.Commit(root));
        Assert.Equal(NewLauncher, File.ReadAllText(target));
        Assert.Equal("0.2.2", LauncherUpdate.ReadVersionMarker(root));
    }

    /// <summary>T77-4: inen ikili diskte dururken aynı arşiv bir daha istenmez.</summary>
    [Fact]
    public void TheLauncherArchiveIsNotFetchedAgainWhileTheSwapWaits()
    {
        var root = Folder("bosainen");
        var app = Folder(Path.Combine("bosainen", "app"));
        Write(app, "VidShrink.App.dll", "uygulama");
        File.WriteAllText(Path.Combine(root, LauncherUpdate.ExecutableName), OldLauncher);

        var launcherFile = ArmedSwap(root);
        UpdateCheck.WriteVersionMarker(app, "0.2.2");

        var manifest = new ReleaseManifest("0.2.2", "abc", DateTimeOffset.UtcNow, "win-x64",
            new[] { Describe(app, "VidShrink.App.dll") })
        {
            Launcher = new[] { launcherFile }
        };

        Assert.Null(LauncherUpdate.ReadVersionMarker(root));
        Assert.True(LauncherUpdate.Armed(root, manifest));
        Assert.True(UpdateCheck.AlreadyCurrent(root, app, manifest), "bekleyen geçiş varken arşiv yeniden isteniyor");

        LauncherUpdate.Sweep(root, manifest.Launcher);
        Assert.False(LauncherUpdate.Armed(root, manifest));
        Assert.False(UpdateCheck.AlreadyCurrent(root, app, manifest), "inen ikili yokken kapı hâlâ kapalı");
    }

    /// <summary>T77-5: başlatıcı adımının düşmesi uygulama adımını iptal etmez.</summary>
    [Fact]
    public void AStuckIncomingLauncherDoesNotCancelTheApplicationUpdate()
    {
        if (!OperatingSystem.IsWindows()) return;

        var root = Folder("takili");
        var app = Folder(Path.Combine("takili", "app"));
        var stage = Path.Combine(root, "update-stage");
        Write(app, "VidShrink.App.dll", "eski uygulama");
        Write(stage, "VidShrink.App.dll", "yeni uygulama");
        File.WriteAllText(Path.Combine(root, LauncherUpdate.ExecutableName), OldLauncher);

        var appFile = Describe(stage, "VidShrink.App.dll");
        var launcherFile = Describe(WriteStagedLauncher(stage));

        var incoming = LauncherUpdate.Incoming(root, LauncherUpdate.ExecutableName);
        File.WriteAllText(incoming, "önceki turdan takılı kalmış");
        using var stuck = new FileStream(incoming, FileMode.Open, FileAccess.Read, FileShare.None);

        var manifest = new ReleaseManifest("0.2.2", "abc", DateTimeOffset.UtcNow, "win-x64", new[] { appFile })
        {
            Launcher = new[] { launcherFile }
        };

        Assert.Empty(LauncherUpdate.Stage(stage, root, new[] { launcherFile }));
        Assert.False(UpdateRollout.Apply(stage, root, app, new[] { appFile }, new[] { launcherFile }, manifest));

        Assert.Equal("yeni uygulama", File.ReadAllText(UpdateCheck.LocalPath(app, "VidShrink.App.dll")));
        Assert.Equal("0.2.2", UpdateCheck.ReadVersionMarker(app));
        Assert.False(File.Exists(Path.Combine(root, LauncherUpdate.JournalName)));
        Assert.Equal(OldLauncher, File.ReadAllText(Path.Combine(root, LauncherUpdate.ExecutableName)));
    }

    private static void WriteJournalLikeApply(string appDirectory, string stageDirectory, IReadOnlyList<ManifestFile> files)
    {
        var entries = string.Join(",", files.Select(file =>
            $"{{\"path\":\"{file.Path}\",\"sha256\":\"{file.Sha256}\",\"size\":{file.Size}}}"));
        File.WriteAllText(
            Path.Combine(appDirectory, UpdateStage.JournalName),
            $"{{\"stage\":{System.Text.Json.JsonSerializer.Serialize(stageDirectory)},\"files\":[{entries}]}}");
    }

    private sealed class CountingRangeSource : IRangeSource
    {
        private readonly IRangeSource _inner;

        public CountingRangeSource(IRangeSource inner) => _inner = inner;

        public long BytesRead { get; private set; }

        public Task<long> LengthAsync(CancellationToken cancellationToken) => _inner.LengthAsync(cancellationToken);

        public async Task<byte[]> ReadAsync(long offset, int length, CancellationToken cancellationToken)
        {
            var bytes = await _inner.ReadAsync(offset, length, cancellationToken);
            BytesRead += bytes.Length;
            return bytes;
        }
    }

}
