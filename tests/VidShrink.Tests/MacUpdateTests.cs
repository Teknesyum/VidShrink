using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// macOS'ta kendini güncelleme yolu: kapının hangi koşula baktığı ve takasın imzayı
/// takastan önce doğrulaması. Paket üreten ve takas eden ölçüler yalnız macOS'un kendi
/// araçlarıyla çalıştığı için macOS dışında erken dönüyor — <c>Skip</c> değil, ki
/// Windows'ta atlanan ölçü sayısı değişmesin.
/// </summary>
public sealed class MacUpdateTests
{
    private static string Sandbox()
    {
        var directory = Path.Combine(Path.GetTempPath(), "vidshrink-macupdate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>Verilen yükten imzalı bir paket üretir; paketleyici yayının kendi betiği.</summary>
    private static void Bundle(string payload, string version, string bundle)
    {
        Directory.CreateDirectory(payload);
        File.WriteAllText(Path.Combine(payload, MacUpdate.HostName), "#!/bin/sh\nexit 0\n");
        File.WriteAllText(Path.Combine(payload, UpdateCheck.VersionMarkerName), version);

        var script = Path.Combine(TipSources.Root, MacUpdate.BundleScriptName);
        Assert.Equal(0, Shell(script, payload, MacUpdate.HostName, version, bundle));
    }

    private static int Shell(params string[] arguments)
    {
        var start = new ProcessStartInfo("/bin/sh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = Process.Start(start)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static string Marker(string bundle) =>
        File.ReadAllText(Path.Combine(bundle, "Contents", "MacOS", UpdateCheck.VersionMarkerName));

    /// <summary>
    /// Kapının ölçüsü: yalnız <c>.app/Contents/MacOS/</c> altındaki bir ikili paket sayılıyor.
    /// Düz kurulumun yolu — <c>~/.local/share/vidshrink/VidShrink</c> — sayılmıyor, orada
    /// kendini güncelleme kapalı kalıp kullanıcı haberi görmeyi sürdürüyor.
    /// </summary>
    [Fact]
    public void OnlyABinaryInsideAnAppBundleCounts()
    {
        Assert.Equal(
            "/Users/x/Applications/VidShrink.app",
            MacUpdate.BundleOf("/Users/x/Applications/VidShrink.app/Contents/MacOS/VidShrink"));

        Assert.Null(MacUpdate.BundleOf("/Users/x/.local/share/vidshrink/VidShrink"));
        Assert.Null(MacUpdate.BundleOf("/Users/x/Applications/VidShrink.app/Contents/Resources/VidShrink"));
        Assert.Null(MacUpdate.BundleOf("/Users/x/Applications/VidShrink/Contents/MacOS/VidShrink"));
        Assert.Null(MacUpdate.BundleOf(""));
        Assert.Null(MacUpdate.BundleOf(null));
    }

    /// <summary>
    /// Taşınmış bir kopya kendi paketine yazamaz; kapı orada da kapalı olmalı, yoksa uygulama
    /// her açılışta indirip hiç takas edemez ve kullanıcı haberi de göremez.
    /// </summary>
    [Fact]
    public void ATranslocatedBundleIsNotSwappable()
    {
        const string translocated =
            "/private/var/folders/ab/xyz/d/AppTranslocation/1234-5678/d/VidShrink.app";

        Assert.True(MacUpdate.Translocated(translocated));
        Assert.False(MacUpdate.Translocated("/Users/x/Applications/VidShrink.app"));
        Assert.False(MacUpdate.CanSwap(translocated + "/Contents/MacOS/VidShrink"));
    }

    /// <summary>
    /// Windows dalı değişmiyor: orada cevap yalnız <c>OperatingSystem.IsWindows()</c>'tan
    /// geliyor ve paket kapısı hep kapalı, yani hiçbir macOS koşulu Windows'a sızmıyor.
    /// macOS'ta cevabın tamamı paket kapısı, Linux'ta kapalı.
    /// </summary>
    [Fact]
    public void TheWindowsBranchDoesNotDependOnTheBundleGate()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.True(UpdateCheck.CanSelfUpdate);
            Assert.False(MacUpdate.CanSwap(Environment.ProcessPath));
            Assert.False(MacUpdate.CanSwap("C:\\Users\\x\\VidShrink.app\\Contents\\MacOS\\VidShrink"));
            return;
        }

        Assert.Equal(MacUpdate.CanSwap(Environment.ProcessPath), UpdateCheck.CanSelfUpdate);
        if (!OperatingSystem.IsMacOS()) Assert.False(UpdateCheck.CanSelfUpdate);
    }

    /// <summary>
    /// İmzası bozuk bir paket kurulu paketin yerine geçmiyor. Mühürden sonra içeriden bir
    /// dosya değişince <c>codesign --verify</c> düşer; takas o noktada durmalı ve kurulu
    /// paket el değmemiş kalmalı. Doğrulama adımı kaldırılırsa bu ölçü kırmızıya döner.
    /// </summary>
    [Fact]
    public void ABrokenSignatureStopsTheSwap()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var sandbox = Sandbox();
        try
        {
            var installed = Path.Combine(sandbox, "Applications", "VidShrink.app");
            Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
            Bundle(Path.Combine(sandbox, "old"), "0.2.5", installed);
            Bundle(Path.Combine(sandbox, "new"), "0.2.6", MacUpdate.StagedBundle(installed));

            var host = Path.Combine(MacUpdate.StagedBundle(installed), "Contents", "MacOS", MacUpdate.HostName);
            File.AppendAllText(host, "\n# mühürden sonra değişti\n");
            Assert.False(MacUpdate.SignatureValid(MacUpdate.StagedBundle(installed)));

            Assert.False(MacUpdate.Commit(installed));

            Assert.Equal("0.2.5", Marker(installed));
            Assert.True(MacUpdate.SignatureValid(installed));
            Assert.False(Directory.Exists(MacUpdate.StageRoot(installed)));
        }
        finally
        {
            Directory.Delete(sandbox, true);
        }
    }

    /// <summary>
    /// Doğrulanmış paket tek çağrıda yer değiştiriyor: kurulu yolda yeni sürüm, hazırlama
    /// dizininde eski paket duruyor ve yeni paketin imzası geçerli. Eski paket burada
    /// silinmiyor — onu bir sonraki açılışın koşulsuz <see cref="MacUpdate.Discard"/>'ı alıyor.
    /// </summary>
    [Fact]
    public void AVerifiedBundleSwapsIntoPlace()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var sandbox = Sandbox();
        try
        {
            var installed = Path.Combine(sandbox, "Applications", "VidShrink.app");
            Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
            Bundle(Path.Combine(sandbox, "old"), "0.2.5", installed);
            Bundle(Path.Combine(sandbox, "new"), "0.2.6", MacUpdate.StagedBundle(installed));

            Assert.True(MacUpdate.Commit(installed));

            Assert.Equal("0.2.6", Marker(installed));
            Assert.True(MacUpdate.SignatureValid(installed));
            Assert.Equal("0.2.5", Marker(MacUpdate.StagedBundle(installed)));

            MacUpdate.Discard(installed);
            Assert.False(Directory.Exists(MacUpdate.StageRoot(installed)));
        }
        finally
        {
            Directory.Delete(sandbox, true);
        }
    }

    /// <summary>
    /// Hazırlama uçtan uca: yerel bir yayından arşiv açılıyor, her dosyanın özeti manifestle
    /// karşılaştırılıyor, yayının kendi betiği paketi kuruyor ve imza doğrulanıyor. Kurulu
    /// pakete bu adımların hiçbirinde dokunulmuyor.
    /// </summary>
    [Fact]
    public async Task PreparingFromAReleaseLeavesASignedBundleBesideTheInstalledOne()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var sandbox = Sandbox();
        try
        {
            var installed = Path.Combine(sandbox, "Applications", "VidShrink.app");
            Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
            Bundle(Path.Combine(sandbox, "old"), "0.2.5", installed);

            var source = Release(Path.Combine(sandbox, "release"), "0.2.6");
            Assert.True(await MacUpdate.PrepareAsync(installed, "0.2.5", source, CancellationToken.None));

            var staged = MacUpdate.StagedBundle(installed);
            Assert.True(MacUpdate.SignatureValid(staged));
            Assert.Equal("0.2.6", Marker(staged));
            Assert.Equal("0.2.5", Marker(installed));

            Assert.False(await MacUpdate.PrepareAsync(installed, "0.2.6", source, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(sandbox, true);
        }
    }

    /// <summary>
    /// Yayının biçimindeki yerel bir kaynak: manifest ile arşiv, adları
    /// <see cref="UpdateCheck.Rid"/>'e göre. Arşivin içinde paketleme betiği de var —
    /// güncelleme onu yayından alıyor, depodan değil.
    /// </summary>
    private static string Release(string directory, string version)
    {
        var payload = Path.Combine(directory, "payload");
        Directory.CreateDirectory(payload);
        File.WriteAllText(Path.Combine(payload, MacUpdate.HostName), "#!/bin/sh\nexit 0\n");
        File.Copy(
            Path.Combine(TipSources.Root, MacUpdate.BundleScriptName),
            Path.Combine(payload, MacUpdate.BundleScriptName));

        var files = new List<ManifestFile>();
        foreach (var path in Directory.GetFiles(payload, "*", SearchOption.AllDirectories))
        {
            files.Add(new ManifestFile(
                Path.GetRelativePath(payload, path).Replace('\\', '/'),
                UpdateCheck.HashFile(path),
                new FileInfo(path).Length));
        }

        var rid = UpdateCheck.Rid;
        ZipFile.CreateFromDirectory(payload, Path.Combine(directory, UpdateCheck.ArchiveAssetName(rid)));

        using (var stream = File.Create(Path.Combine(directory, UpdateCheck.ManifestAssetName(rid))))
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("version", version);
            writer.WriteString("commit", "0000000");
            writer.WriteString("built", "2026-08-31T00:00:00Z");
            writer.WriteString("rid", rid);
            writer.WriteStartArray("files");
            foreach (var file in files)
            {
                writer.WriteStartObject();
                writer.WriteString("path", file.Path);
                writer.WriteString("sha256", file.Sha256);
                writer.WriteNumber("size", file.Size);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return directory;
    }
}
