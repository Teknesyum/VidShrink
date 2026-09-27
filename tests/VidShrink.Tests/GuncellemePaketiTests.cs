using System.IO.Compression;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// Tek güncelleme paketi: <c>vidshrink-update-&lt;rid&gt;.zip</c> tek dosya olarak iner,
/// <c>checksums-&lt;rid&gt;.txt</c> satırıyla doğrulanır, yerelde açılır. Satır yoksa eski
/// aralık yoluna düşülür. Ağa çıkmaz: kaynak <c>.calisma</c> altında bir klasördür.
/// </summary>
public sealed class GuncellemePaketiTests : IDisposable
{
    private readonly string _root = Path.Combine(TestPaths.OutputRoot, "guncelleme-paketi", Environment.ProcessId.ToString(), Guid.NewGuid().ToString("N"));

    public GuncellemePaketiTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    private static readonly string[] AppNames = { "VidShrink.App.dll", "a.dll", "runtimes/win-x64/native/b.dll" };
    private const string LauncherName = "VidShrink.exe";
    private const string ShellName = "shell/kabuk.dll";

    private string Folder(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteRandom(string directory, string relativePath, int size)
    {
        var full = UpdateCheck.LocalPath(directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var bytes = new byte[size];
        Random.Shared.NextBytes(bytes);
        File.WriteAllBytes(full, bytes);
    }

    private static string Entry(string directory, string relativePath)
    {
        var full = UpdateCheck.LocalPath(directory, relativePath);
        return $$"""{ "path": "{{relativePath}}", "sha256": "{{UpdateCheck.HashFile(full)}}", "size": {{new FileInfo(full).Length}} }""";
    }

    private sealed record Yayin(string Source, string Publish, string Launcher, string Package, string Checksums);

    private Yayin Hazirla(bool paket = true, bool aralik = false, bool satir = true)
    {
        var publish = Folder("publish");
        foreach (var name in AppNames) WriteRandom(publish, name, 200_000);
        var launcher = Folder("publish-launcher");
        WriteRandom(launcher, LauncherName, 50_000);
        WriteRandom(launcher, ShellName, 30_000);

        var source = Folder("source");
        File.WriteAllText(Path.Combine(source, UpdateCheck.ManifestAssetName(UpdateCheck.Rid)),
            $$"""
            { "version": "9.9.9", "commit": "abc", "built": "2026-09-27T00:00:00Z", "rid": "{{UpdateCheck.Rid}}",
              "files": [{{string.Join(",", AppNames.Select(n => Entry(publish, n)))}}],
              "launcher": [{{Entry(launcher, LauncherName)}}],
              "shell": [{{Entry(launcher, ShellName)}}] }
            """);

        var package = Path.Combine(source, UpdateCheck.PackageAssetName(UpdateCheck.Rid));
        var checksums = Path.Combine(source, UpdateCheck.ChecksumsAssetName(UpdateCheck.Rid));
        if (paket)
        {
            var pkg = Folder("pkg");
            CopyTree(publish, Path.Combine(pkg, UpdateStaging.PackageAppFolder));
            CopyTree(launcher, Path.Combine(pkg, UpdateStaging.PackageLauncherFolder));
            ZipFile.CreateFromDirectory(pkg, package);
            if (satir) File.WriteAllText(checksums, UpdateCheck.HashFile(package).ToLowerInvariant() + "  " + Path.GetFileName(package) + "\n");
        }
        if (aralik)
        {
            ZipFile.CreateFromDirectory(publish, Path.Combine(source, UpdateCheck.ArchiveAssetName(UpdateCheck.Rid)));
            ZipFile.CreateFromDirectory(launcher, Path.Combine(source, UpdateCheck.LauncherArchiveAssetName(UpdateCheck.Rid)));
        }
        return new Yayin(source, publish, launcher, package, checksums);
    }

    private static void CopyTree(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private (string baseDirectory, string app, string stage) Kurulum()
    {
        var baseDirectory = Folder("install");
        var app = Folder(Path.Combine("install", "app"));
        foreach (var name in AppNames)
        {
            var full = UpdateCheck.LocalPath(app, name);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, "eski");
        }
        return (baseDirectory, app, Path.Combine(baseDirectory, UpdateStaging.StageDirectoryName));
    }

    private static Task<StagedUpdate?> Sahnele(string baseDirectory, string app, string source, List<UpdateStageReport> reports) =>
        UpdateStaging.StageAsync(baseDirectory, app, source, 1, null, "test", reports.Add, CancellationToken.None);

    private static void HepsiSahnede(Yayin yayin, string stage)
    {
        foreach (var name in AppNames)
            Assert.Equal(UpdateCheck.HashFile(UpdateCheck.LocalPath(yayin.Publish, name)), UpdateCheck.HashFile(UpdateCheck.LocalPath(stage, name)));
        Assert.Equal(UpdateCheck.HashFile(UpdateCheck.LocalPath(yayin.Launcher, LauncherName)), UpdateCheck.HashFile(LauncherUpdate.StagePath(stage, LauncherName)));
        Assert.Equal(UpdateCheck.HashFile(UpdateCheck.LocalPath(yayin.Launcher, ShellName)), UpdateCheck.HashFile(ShellUpdate.StagePath(stage, ShellName)));
    }

    [Fact]
    public async Task TekPaketIndirilirAcilirVeHepsiniSahneler()
    {
        var yayin = Hazirla(paket: true, aralik: false);
        var (baseDirectory, app, stage) = Kurulum();
        var reports = new List<UpdateStageReport>();

        var staged = await Sahnele(baseDirectory, app, yayin.Source, reports);

        Assert.NotNull(staged);
        HepsiSahnede(yayin, stage);
        Assert.False(File.Exists(UpdateStaging.PackagePath(stage, UpdateCheck.Rid)));
        var receiving = reports.Where(r => r.Phase == UpdateStagePhase.Receiving).ToList();
        Assert.NotEmpty(receiving);
        Assert.All(receiving, r => Assert.Equal(UpdateCheck.PackageAssetName(UpdateCheck.Rid), r.File));
        Assert.Equal(receiving.Select(r => r.Part).OrderBy(p => p), receiving.Select(r => r.Part));
        Assert.Equal(0.80, receiving[^1].Part, 3);
        Assert.Equal(UpdateStagePhase.Staged, reports[^1].Phase);
    }

    [Fact]
    public async Task OzetSatiriYoksaAralikYolunaDuser()
    {
        var yayin = Hazirla(paket: true, aralik: true, satir: false);
        var (baseDirectory, app, stage) = Kurulum();
        var reports = new List<UpdateStageReport>();

        var staged = await Sahnele(baseDirectory, app, yayin.Source, reports);

        Assert.NotNull(staged);
        HepsiSahnede(yayin, stage);
        Assert.DoesNotContain(reports, r => r.Phase == UpdateStagePhase.Receiving);
    }

    [Fact]
    public async Task AralikArsiviYokkenOzetSatiriOlmadanSahnelenemez()
    {
        var yayin = Hazirla(paket: true, aralik: false, satir: false);
        var (baseDirectory, app, _) = Kurulum();

        await Assert.ThrowsAnyAsync<Exception>(() => Sahnele(baseDirectory, app, yayin.Source, new List<UpdateStageReport>()));
    }

    [Fact]
    public async Task PaketinOzetiTutmazsaSilinirVeHicbirSeySahnelenmez()
    {
        var yayin = Hazirla(paket: true, aralik: true);
        File.WriteAllText(yayin.Checksums, new string('0', 64) + "  " + Path.GetFileName(yayin.Package) + "\n");
        var (baseDirectory, app, stage) = Kurulum();

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Sahnele(baseDirectory, app, yayin.Source, new List<UpdateStageReport>()));

        Assert.Contains(UpdateCheck.PackageAssetName(UpdateCheck.Rid), error.Message);
        Assert.False(File.Exists(UpdateStaging.PackagePath(stage, UpdateCheck.Rid)));
        Assert.All(AppNames, name => Assert.False(File.Exists(UpdateCheck.LocalPath(stage, name))));
    }

    [Fact]
    public async Task PaketGirdisiManifesteTutmazsaReddedilir()
    {
        var yayin = Hazirla(paket: false);
        var pkg = Folder("pkg");
        CopyTree(yayin.Publish, Path.Combine(pkg, UpdateStaging.PackageAppFolder));
        CopyTree(yayin.Launcher, Path.Combine(pkg, UpdateStaging.PackageLauncherFolder));
        File.WriteAllText(UpdateCheck.LocalPath(Path.Combine(pkg, UpdateStaging.PackageAppFolder), AppNames[1]), "kurcalanmış");
        ZipFile.CreateFromDirectory(pkg, yayin.Package);
        File.WriteAllText(yayin.Checksums, UpdateCheck.HashFile(yayin.Package).ToLowerInvariant() + "  " + Path.GetFileName(yayin.Package) + "\n");
        var (baseDirectory, app, stage) = Kurulum();

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Sahnele(baseDirectory, app, yayin.Source, new List<UpdateStageReport>()));

        Assert.Contains(AppNames[1], error.Message);
        Assert.False(File.Exists(UpdateCheck.LocalPath(stage, AppNames[1])));
        Assert.Empty(Directory.EnumerateFiles(stage, "*" + UpdateStaging.PartialSuffix, SearchOption.AllDirectories));
    }

    [Fact]
    public async Task YalnizDegisenDosyalarAcilir()
    {
        var yayin = Hazirla(paket: true);
        var (baseDirectory, app, stage) = Kurulum();
        File.Copy(UpdateCheck.LocalPath(yayin.Publish, AppNames[0]), UpdateCheck.LocalPath(app, AppNames[0]), overwrite: true);
        var reports = new List<UpdateStageReport>();

        var staged = await Sahnele(baseDirectory, app, yayin.Source, reports);

        Assert.NotNull(staged);
        Assert.DoesNotContain(staged!.App, f => f.Path == AppNames[0]);
        Assert.False(File.Exists(UpdateCheck.LocalPath(stage, AppNames[0])));
        Assert.True(File.Exists(UpdateCheck.LocalPath(stage, AppNames[1])));
        Assert.Equal(AppNames.Length - 1 + 2, reports.Count(r => r.Phase == UpdateStagePhase.Downloaded));
    }

    [Fact]
    public async Task InmisDogruPaketYenidenIndirilmez()
    {
        var yayin = Hazirla(paket: true);
        var (baseDirectory, app, stage) = Kurulum();
        Directory.CreateDirectory(stage);
        File.WriteAllText(Path.Combine(stage, UpdateStaging.StageVersionName), "9.9.9");
        File.Move(yayin.Package, UpdateStaging.PackagePath(stage, UpdateCheck.Rid));
        var reports = new List<UpdateStageReport>();

        var staged = await Sahnele(baseDirectory, app, yayin.Source, reports);

        Assert.NotNull(staged);
        HepsiSahnede(yayin, stage);
        Assert.DoesNotContain(reports, r => r.Phase == UpdateStagePhase.Receiving);
    }
}
