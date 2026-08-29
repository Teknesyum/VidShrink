using System.Diagnostics;
using System.IO.Compression;
using System.Net;
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
        _root = Path.Combine(Path.GetTempPath(), "vidshrink_update_" + Guid.NewGuid().ToString("N"));
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
        var ceiling = UpdateCheck.ManifestTimeout + TimeSpan.FromMilliseconds(500);

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
    public void TheRunningBinaryIsRenamedAsideInsteadOfOverwritten()
    {
        var root = Folder("running");
        var stage = Path.Combine(root, "update-stage");
        var target = Path.Combine(root, LauncherUpdate.ExecutableName);
        File.WriteAllText(target, OldLauncher);

        var staged = LauncherUpdate.StagePath(stage, LauncherUpdate.ExecutableName);
        Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
        File.WriteAllText(staged, NewLauncher);
        var incoming = new ManifestFile(
            LauncherUpdate.ExecutableName, UpdateCheck.HashFile(staged), new FileInfo(staged).Length);

        // Çalışan bir görüntü dosyasının paylaşım kipi: okunabilir ve adı değiştirilebilir,
        // üstüne yazılamaz. Değişimin bu kipte de yürümesi gereken tek koşul bu.
        using (new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        {
            if (OperatingSystem.IsWindows())
                Assert.ThrowsAny<IOException>(() => File.Copy(staged, target, overwrite: true));

            LauncherUpdate.Stage(stage, root, new[] { incoming });
            Assert.Equal(NewLauncher, File.ReadAllText(target + LauncherUpdate.IncomingSuffix));

            LauncherUpdate.Apply(root, new[] { incoming }, "0.2.2+abc1234");
        }

        Assert.Equal(NewLauncher, File.ReadAllText(target));
        Assert.Equal("0.2.2", LauncherUpdate.ReadVersionMarker(root));
        Assert.False(File.Exists(Path.Combine(root, LauncherUpdate.JournalName)));
    }

    [Fact]
    public void TheRetiredNameIsDeletedOnTheNextLaunch()
    {
        var root = Folder("retired");
        var target = Path.Combine(root, LauncherUpdate.ExecutableName);
        File.WriteAllText(target, NewLauncher);
        File.WriteAllText(target + LauncherUpdate.RetiredSuffix, OldLauncher);

        LauncherUpdate.Repair(root);

        Assert.False(File.Exists(target + LauncherUpdate.RetiredSuffix));
        Assert.Equal(NewLauncher, File.ReadAllText(target));
    }

    [Theory]
    [InlineData(1, OldLauncher)]
    [InlineData(2, NewLauncher)]
    [InlineData(3, NewLauncher)]
    [InlineData(4, NewLauncher)]
    [InlineData(5, NewLauncher)]
    public void AnInterruptionAtAnyStepLeavesARunnableLauncher(int completedSteps, string expected)
    {
        var (root, _) = HalfDoneSwap("kesinti" + completedSteps, completedSteps);
        var target = Path.Combine(root, LauncherUpdate.ExecutableName);

        LauncherUpdate.Repair(root);

        Assert.True(File.Exists(target), $"{completedSteps}. adımdan sonra başlatıcı ortada yok");
        Assert.Equal(expected, File.ReadAllText(target));
        Assert.False(File.Exists(Path.Combine(root, LauncherUpdate.JournalName)));
        Assert.False(File.Exists(target + LauncherUpdate.RetiredSuffix));
        Assert.False(File.Exists(target + LauncherUpdate.IncomingSuffix));
        _output.WriteLine($"{completedSteps} adım tamamlandı, kalan başlatıcı: {File.ReadAllText(target)}");
    }

    [Fact]
    public void TheOneStepThatEmptiesTheTargetIsRolledBackWhenTheNewBinaryIsGone()
    {
        // Hedefin boş kaldığı tek an: eski ad alındı, yeni ikili henüz geçmedi. Yeni ikili
        // de elde yoksa eski ad geri alınır; kurulum açılabilir kalır, sürüm işareti
        // ilerlemez ve bir sonraki tur aynı güncellemeyi yeniden dener.
        var (root, _) = HalfDoneSwap("gerial", completedSteps: 3, loseIncoming: true);
        var target = Path.Combine(root, LauncherUpdate.ExecutableName);

        Assert.False(File.Exists(target));

        Assert.False(LauncherUpdate.Repair(root));

        Assert.Equal(OldLauncher, File.ReadAllText(target));
        Assert.Null(LauncherUpdate.ReadVersionMarker(root));
        Assert.False(File.Exists(Path.Combine(root, LauncherUpdate.JournalName)));
    }

    [Fact]
    public void TheVersionMarkerIsOnlyMovedForwardWhenTheSwapReallyLanded()
    {
        var (settled, _) = HalfDoneSwap("isaret", completedSteps: 3);
        Assert.True(LauncherUpdate.Repair(settled));
        Assert.Equal("0.2.2", LauncherUpdate.ReadVersionMarker(settled));
    }

    [Fact]
    public void TheVersionGateSeesALauncherLeftBehindByAnAppUpdate()
    {
        var root = Folder("kapi");
        var app = Folder(Path.Combine("kapi", "app"));
        Write(app, "VidShrink.App.dll", "0.2.1 uygulaması");

        UpdateCheck.WriteVersionMarker(app, "0.2.1");
        LauncherUpdate.WriteVersionMarker(root, "0.2.0");

        var skew = LauncherUpdate.Inspect(root, app);
        Assert.True(skew.Mismatched);
        Assert.Equal("0.2.0", skew.Launcher);
        Assert.Equal("0.2.1", skew.App);

        var manifest = new ReleaseManifest("0.2.1", "abc", DateTimeOffset.UtcNow, "win-x64",
            new[] { Describe(app, "VidShrink.App.dll") })
        {
            Launcher = new[] { new ManifestFile(LauncherUpdate.ExecutableName, new string('0', 64), 10) }
        };

        // Bugüne kadarki kapı yalnız app işaretine bakıyordu ve bu farkı hiç görmüyordu.
        Assert.True(UpdateCheck.AlreadyCurrent(app, manifest));
        Assert.False(UpdateCheck.AlreadyCurrent(root, app, manifest));

        LauncherUpdate.WriteVersionMarker(root, "0.2.1");
        Assert.False(LauncherUpdate.Inspect(root, app).Mismatched);
        Assert.True(UpdateCheck.AlreadyCurrent(root, app, manifest));
    }

    [Fact]
    public void TheMarkerIsSeededFromTheRunningBinaryAndThenLeftAlone()
    {
        var root = Folder("tohum");

        LauncherUpdate.SeedVersionMarker(root, "0.2.0+abc1234");
        Assert.Equal("0.2.0", LauncherUpdate.ReadVersionMarker(root));

        LauncherUpdate.SeedVersionMarker(root, "0.1.0");
        Assert.Equal("0.2.0", LauncherUpdate.ReadVersionMarker(root));
    }

    [Fact]
    public void TheLauncherIsSwappedAfterTheApplicationFilesAreInPlace()
    {
        var code = File.ReadAllText(
            Path.Combine(TipSources.Root, "src", "VidShrink.Launcher", "Updater.cs"));

        var appApply = code.IndexOf("UpdateStage.Apply", StringComparison.Ordinal);
        var launcherApply = code.IndexOf("LauncherUpdate.Apply", StringComparison.Ordinal);

        Assert.True(appApply >= 0 && launcherApply > appApply,
            "başlatıcı uygulama dosyalarından önce değişiyor");

        var program = File.ReadAllText(
            Path.Combine(TipSources.Root, "src", "VidShrink.Launcher", "Program.cs"));
        Assert.Contains("LauncherUpdate.Repair(baseDirectory)", program, StringComparison.Ordinal);
        Assert.True(
            program.IndexOf("LauncherUpdate.Repair", StringComparison.Ordinal)
                < program.IndexOf("Updater.Run", StringComparison.Ordinal),
            "yarım kalan değişim güncellemeden sonra toplanıyor");
    }

    /// <summary>
    /// Değişimin beş adımının ilk <paramref name="completedSteps"/> tanesini uygulayıp
    /// süreç orada ölmüş gibi bırakır. Adımlar sırayla: yan dosyaya iniş, günlük,
    /// eski adın alınması, yeni ikilinin geçmesi, günlüğün silinmesi.
    /// </summary>
    private (string Root, ManifestFile File) HalfDoneSwap(string name, int completedSteps, bool loseIncoming = false)
    {
        var root = Folder(name);
        var target = Path.Combine(root, LauncherUpdate.ExecutableName);
        File.WriteAllText(target, OldLauncher);

        var side = Folder(Path.Combine(name, "yan"));
        var sideFile = Path.Combine(side, LauncherUpdate.ExecutableName);
        File.WriteAllText(sideFile, NewLauncher);
        var file = new ManifestFile(
            LauncherUpdate.ExecutableName, UpdateCheck.HashFile(sideFile), new FileInfo(sideFile).Length);

        var incoming = target + LauncherUpdate.IncomingSuffix;
        var retired = target + LauncherUpdate.RetiredSuffix;

        if (completedSteps >= 1) File.Move(sideFile, incoming);
        if (completedSteps >= 2) WriteLauncherJournal(root, file, "0.2.2");
        if (completedSteps >= 3) File.Move(target, retired);
        if (completedSteps >= 4) File.Move(incoming, target);
        if (completedSteps >= 5) File.Delete(Path.Combine(root, LauncherUpdate.JournalName));
        if (loseIncoming && File.Exists(incoming)) File.Delete(incoming);

        return (root, file);
    }

    private static void WriteLauncherJournal(string baseDirectory, ManifestFile file, string version) =>
        File.WriteAllText(
            Path.Combine(baseDirectory, LauncherUpdate.JournalName),
            $"{{\"version\":\"{version}\",\"files\":[{{\"path\":\"{file.Path}\"," +
            $"\"sha256\":\"{file.Sha256}\",\"size\":{file.Size}}}]}}");

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
