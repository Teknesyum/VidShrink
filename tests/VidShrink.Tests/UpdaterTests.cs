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
    public void FirstLaunchChecksAndTheNextOnesWithinADayDoNot()
    {
        var file = Path.Combine(_root, "last-check.json");
        var now = DateTimeOffset.UtcNow;

        Assert.True(UpdateSchedule.DueNow(now, file));

        UpdateSchedule.Record(now, file);
        Assert.False(UpdateSchedule.DueNow(now, file));
        Assert.False(UpdateSchedule.DueNow(now.AddHours(23), file));
        Assert.True(UpdateSchedule.DueNow(now.AddHours(24), file));
        Assert.Equal(now, UpdateSchedule.ReadLastCheck(file)!.Value, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MissingOrBrokenOrFutureDatedRecordMeansCheckNow()
    {
        var file = Path.Combine(_root, "last-check.json");
        var now = DateTimeOffset.UtcNow;

        Assert.True(UpdateSchedule.DueNow(now, file));

        File.WriteAllText(file, "{ bozuk");
        Assert.True(UpdateSchedule.DueNow(now, file));

        // Saat geriye alınmış: kayıt gelecekte kalır, beklemek yerine denetlenir.
        UpdateSchedule.Record(now.AddDays(3), file);
        Assert.True(UpdateSchedule.DueNow(now, file));
    }

    [Fact]
    public void ScheduleFileSitsNextToTheSettingsFile()
    {
        var settings = Path.Combine(_root, "appdata", "settings.json");
        Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", settings);
        try
        {
            Assert.Equal(Path.Combine(_root, "appdata", UpdateSchedule.FileName), UpdateSchedule.DefaultPath);
        }
        finally { Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", null); }
    }

    [Fact]
    public void PendingUpdateIsSeenSoTheDailyLimitCanBeSkipped()
    {
        var app = Folder("app");
        Assert.False(UpdateStage.HasPending(app));

        File.WriteAllText(Path.Combine(app, UpdateStage.JournalName), "{}");
        Assert.True(UpdateStage.HasPending(app));
    }

    [LiveLauncherFact]
    public void LauncherStartsTheAppWithinTheTimeoutWithAndWithoutNetwork()
    {
        var exe = Environment.GetEnvironmentVariable("VIDSHRINK_LAUNCHER_EXE")!;
        var settings = Path.Combine(_root, "settings.json");
        new UpdateSettings { AutoUpdate = true }.Save(settings);

        // 10.255.255.1 yönlendirilemeyen adres: ağ yokmuş gibi davranır.
        var offlineFirst = MeasureLaunch(exe, settings, "http://10.255.255.1/vidshrink");
        // Aynı gün içindeki ikinci açılış: denetim yapılmaz, ağa hiç çıkılmaz.
        var offlineSameDay = MeasureLaunch(exe, settings, "http://10.255.255.1/vidshrink", resetSchedule: false);
        var online = MeasureLaunch(exe, settings, null);

        _output.WriteLine($"ağsız ilk açılış (denetim günü): {offlineFirst.TotalMilliseconds:F0} ms");
        _output.WriteLine($"ağsız aynı gün ikinci açılış: {offlineSameDay.TotalMilliseconds:F0} ms");
        _output.WriteLine($"ağlı açılış (güncelleme yok): {online.TotalMilliseconds:F0} ms");
        Assert.True(offlineFirst < TimeSpan.FromSeconds(3), $"ağsız açılış çok uzun: {offlineFirst}");
        Assert.True(offlineSameDay < offlineFirst, "günlük kısıt ikinci açılışı hızlandırmadı");
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

    private static TimeSpan MeasureLaunch(string exe, string settingsPath, string? baseUrl, bool resetSchedule = true)
    {
        var schedule = Path.Combine(Path.GetDirectoryName(settingsPath)!, UpdateSchedule.FileName);
        if (resetSchedule && File.Exists(schedule)) File.Delete(schedule);

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
