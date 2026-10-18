using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

public sealed class EskiAdGocuTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly string _work;
    private readonly string _settings;

    public EskiAdGocuTests()
    {
        _work = Path.Combine(TestPaths.OutputRoot, "eski-ad-gocu", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_work);
        _settings = Path.Combine(_work, "ayar", "settings.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, true); } catch (IOException) { }
    }

    [Fact]
    public void VeriKlasorleriEskiAddanYeniAdaEslenir()
    {
        var roaming = Path.Combine(_work, "Roaming");
        var local = Path.Combine(_work, "Local");
        var folders = LegacyMigration.DataFolders(special => special switch
        {
            Environment.SpecialFolder.ApplicationData => roaming,
            Environment.SpecialFolder.LocalApplicationData => local,
            _ => ""
        });

        Assert.Equal(
            new[]
            {
                new LegacyDataFolder(Path.Combine(roaming, AppIdentity.LegacyFolderName), Path.Combine(roaming, AppIdentity.FolderName)),
                new LegacyDataFolder(Path.Combine(local, AppIdentity.LegacyFolderName), Path.Combine(local, AppIdentity.FolderName))
            },
            folders);
    }

    [Fact]
    public void AyniKokIkiKezEslenmez()
    {
        var root = Path.Combine(_work, "Tek");
        var folders = LegacyMigration.DataFolders(_ => root);

        Assert.Single(folders);
    }

    [Fact]
    public void VeriKopyalanirSaglikVeNotDisardaKalirEskiKlasordeIzBirakilir()
    {
        var folder = Pair();
        Write(folder.Old, "settings.json", "{\"a\":1}");
        Write(Path.Combine(folder.Old, "Editor"), "proje.json", "p");
        Write(folder.Old, UpdateHealth.FileName, "h");
        Write(folder.Old, "settings.json." + UpdateHealth.FileName, "h");
        Write(folder.Old, AppliedUpdateNotice.MarkerFileName, "1.0");
        Write(folder.Old, AppIdentity.LegacySetupExecutable, "exe");

        var result = LegacyMigration.CopyData(folder, _settings, Now);

        Assert.True(result);
        Assert.Equal("{\"a\":1}", File.ReadAllText(Path.Combine(folder.New, "settings.json")));
        Assert.Equal("p", File.ReadAllText(Path.Combine(folder.New, "Editor", "proje.json")));
        Assert.False(File.Exists(Path.Combine(folder.New, UpdateHealth.FileName)));
        Assert.False(File.Exists(Path.Combine(folder.New, "settings.json." + UpdateHealth.FileName)));
        Assert.False(File.Exists(Path.Combine(folder.New, AppliedUpdateNotice.MarkerFileName)));
        Assert.False(File.Exists(Path.Combine(folder.New, AppIdentity.LegacySetupExecutable)));
        Assert.False(File.Exists(Path.Combine(folder.New, LegacyMigration.MovedMarkerName)));
        Assert.Equal(folder.New, File.ReadAllText(Path.Combine(folder.Old, LegacyMigration.MovedMarkerName)));
        Assert.True(File.Exists(Path.Combine(folder.Old, "settings.json")));
    }

    [Fact]
    public void YeniKlasordekiDosyaEzilmez()
    {
        var folder = Pair();
        Write(folder.Old, "settings.json", "eski");
        Write(folder.Old, "palet.json", "eski-palet");
        Write(folder.New, "settings.json", "yeni");

        var result = LegacyMigration.CopyData(folder, _settings, Now);

        Assert.True(result);
        Assert.Equal("yeni", File.ReadAllText(Path.Combine(folder.New, "settings.json")));
        Assert.Equal("eski-palet", File.ReadAllText(Path.Combine(folder.New, "palet.json")));
    }

    [Fact]
    public void IkinciAcilistaGocTekrarlanmaz()
    {
        var folder = Pair();
        Write(folder.Old, "settings.json", "ilk");

        Assert.True(LegacyMigration.CopyData(folder, _settings, Now));
        File.Delete(Path.Combine(folder.New, "settings.json"));

        Assert.True(LegacyMigration.CopyData(folder, _settings, Now));
        Assert.False(File.Exists(Path.Combine(folder.New, "settings.json")));
    }

    [Fact]
    public void EskiKlasorYoksaIsYok()
    {
        var folder = Pair();

        Assert.True(LegacyMigration.MigrateData(new[] { folder }, _settings, Now));
        Assert.False(Directory.Exists(folder.New));
        Assert.Equal(0, UpdateHealth.Load(_settings).Failures);
    }

    [Fact]
    public void KopyaDuserseSagligaSayilir()
    {
        var folder = Pair();
        Write(folder.Old, "settings.json", "x");
        Directory.CreateDirectory(Path.GetDirectoryName(folder.New)!);
        File.WriteAllText(folder.New, "klasor yerine dosya");

        var result = LegacyMigration.CopyData(folder, _settings, Now);

        Assert.False(result);
        var health = UpdateHealth.Load(_settings);
        Assert.Equal(1, health.Failures);
        Assert.StartsWith(LegacyMigration.DataCopyFailed, health.LastError);
        Assert.False(File.Exists(Path.Combine(folder.Old, LegacyMigration.MovedMarkerName)));
    }

    [Fact]
    public void BirKlasorDusseDeOtekiTasinirSonucYanlisDoner()
    {
        var broken = Pair();
        Write(broken.Old, "settings.json", "x");
        Directory.CreateDirectory(Path.GetDirectoryName(broken.New)!);
        File.WriteAllText(broken.New, "klasor yerine dosya");
        var fine = new LegacyDataFolder(Path.Combine(_work, "Local", AppIdentity.LegacyFolderName), Path.Combine(_work, "Local", AppIdentity.FolderName));
        Write(fine.Old, "jit.profile", "j");

        Assert.False(LegacyMigration.MigrateData(new[] { broken, fine }, _settings, Now));
        Assert.Equal("j", File.ReadAllText(Path.Combine(fine.New, "jit.profile")));
    }

    [Fact]
    public void EskiKurulumAnahtarYoksaVeKlasorYoksaBulunmaz()
    {
        var local = Path.Combine(_work, "Local");

        Assert.Null(LegacyMigration.FindLegacyInstall(false, null, local, _ => false));
    }

    [Fact]
    public void EskiKurulumKayitliKonumdanOkunur()
    {
        var local = Path.Combine(_work, "Local");
        var registered = Path.Combine(_work, "Baska", "VidShrink");

        var install = LegacyMigration.FindLegacyInstall(true, "\"" + registered + "\"", local, _ => false);

        Assert.NotNull(install);
        Assert.Equal(registered, install!.Root);
        Assert.Equal(Path.Combine(local, AppIdentity.LegacyFolderName, AppIdentity.LegacySetupExecutable), install.Uninstaller);
    }

    [Fact]
    public void AnahtarYoksaVarsayilanKlasorKullanilir()
    {
        var local = Path.Combine(_work, "Local");
        var fallback = Path.Combine(local, "Programs", AppIdentity.LegacyFolderName);

        var install = LegacyMigration.FindLegacyInstall(false, null, local, path => path == fallback);

        Assert.Equal(fallback, install!.Root);
    }

    [Fact]
    public void EskiKoktenKosanUygulamaKendiniKaldirmaz()
    {
        var root = Path.Combine(_work, "Programs", "VidShrink");
        var install = new LegacyInstall(root, Path.Combine(_work, "u.exe"));

        Assert.True(LegacyMigration.RunsFrom(install, Path.Combine(root, "app") + Path.DirectorySeparatorChar));
        Assert.True(LegacyMigration.RunsFrom(install, root));
        Assert.False(LegacyMigration.RunsFrom(install, Path.Combine(_work, "Programs", "VidShrink Player", "app")));
        Assert.False(LegacyMigration.RunsFrom(install, root + "X"));
    }

    [Fact]
    public async Task EskiKaldiriciAyarSilmedenKokuyleCagrilir()
    {
        var install = Install();
        var fake = new SahteEv();

        var result = await LegacyMigration.RemoveLegacyInstallAsync(install, fake.Host(), _settings, () => Now);

        Assert.True(result);
        Assert.Equal(install.Uninstaller, fake.Executable);
        Assert.Equal(new[] { "--uninstall", "--install-root", install.Root }, fake.Arguments);
        Assert.DoesNotContain("--remove-settings", fake.Arguments!);
        Assert.True(fake.LockHeldDuringRun);
        Assert.True(fake.LockReleased);
        Assert.Equal(0, UpdateHealth.Load(_settings).Failures);
    }

    [Fact]
    public async Task EskiSurecKapanincaKaldirmaBaslar()
    {
        var fake = new SahteEv { RunningPolls = 3 };

        var result = await LegacyMigration.RemoveLegacyInstallAsync(Install(), fake.Host(), _settings, () => Now);

        Assert.True(result);
        Assert.Equal(3, fake.Delays);
        Assert.NotNull(fake.Arguments);
    }

    [Fact]
    public async Task EskiSurecKapanmazsaKaldirilmazVeSayilir()
    {
        var fake = new SahteEv { RunningPolls = int.MaxValue };

        var result = await LegacyMigration.RemoveLegacyInstallAsync(Install(), fake.Host(), _settings, () => Now);

        Assert.False(result);
        Assert.Null(fake.Arguments);
        Assert.Equal(LegacyMigration.ProcessesStillRunning, UpdateHealth.Load(_settings).LastError);
    }

    [Fact]
    public async Task KilitAlinamazsaKaldirilmazVeSayilir()
    {
        var fake = new SahteEv { LockFree = false };

        var result = await LegacyMigration.RemoveLegacyInstallAsync(Install(), fake.Host(), _settings, () => Now);

        Assert.False(result);
        Assert.Null(fake.Arguments);
        Assert.Equal(LegacyMigration.UpdateLockBusy, UpdateHealth.Load(_settings).LastError);
    }

    [Fact]
    public async Task KaldiriciHataDondururseSayilir()
    {
        var fake = new SahteEv { ExitCode = 3 };

        var result = await LegacyMigration.RemoveLegacyInstallAsync(Install(), fake.Host(), _settings, () => Now);

        Assert.False(result);
        Assert.Equal(LegacyMigration.UninstallFailed + "=3", UpdateHealth.Load(_settings).LastError);
        Assert.True(fake.LockReleased);
    }

    [Fact]
    public async Task KaldiriciYoksaSayilir()
    {
        var fake = new SahteEv { UninstallerExists = false };

        var result = await LegacyMigration.RemoveLegacyInstallAsync(Install(), fake.Host(), _settings, () => Now);

        Assert.False(result);
        Assert.Null(fake.Arguments);
        Assert.Equal(LegacyMigration.UninstallerMissing, UpdateHealth.Load(_settings).LastError);
    }

    private LegacyDataFolder Pair() =>
        new(Path.Combine(_work, "Roaming", AppIdentity.LegacyFolderName), Path.Combine(_work, "Roaming", AppIdentity.FolderName));

    private LegacyInstall Install() =>
        new(Path.Combine(_work, "Programs", AppIdentity.LegacyFolderName), Path.Combine(_work, AppIdentity.LegacyFolderName, AppIdentity.LegacySetupExecutable));

    private static void Write(string directory, string name, string content)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name), content);
    }

    private sealed class SahteEv
    {
        public int RunningPolls { get; init; }

        public bool LockFree { get; init; } = true;

        public int? ExitCode { get; init; } = 0;

        public bool UninstallerExists { get; init; } = true;

        public int Delays { get; private set; }

        public string? Executable { get; private set; }

        public IReadOnlyList<string>? Arguments { get; private set; }

        public bool LockHeldDuringRun { get; private set; }

        public bool LockReleased { get; private set; }

        private bool _held;
        private int _polls;

        public LegacyCleanupHost Host() => new()
        {
            OldProcessesRunning = () => _polls++ < RunningPolls,
            AcquireUpdateLock = () =>
            {
                if (!LockFree) return null;
                _held = true;
                return new Release(this);
            },
            Run = (executable, arguments, _) =>
            {
                Executable = executable;
                Arguments = arguments;
                LockHeldDuringRun = _held;
                return ExitCode;
            },
            Delay = (_, _) =>
            {
                Delays++;
                return Task.CompletedTask;
            },
            FileExists = _ => UninstallerExists,
            ProcessWait = TimeSpan.FromSeconds(5),
            PollInterval = TimeSpan.FromSeconds(1)
        };

        private sealed class Release(SahteEv owner) : IDisposable
        {
            public void Dispose()
            {
                owner._held = false;
                owner.LockReleased = true;
            }
        }
    }
}
