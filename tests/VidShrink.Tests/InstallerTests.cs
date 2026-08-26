using System.Text;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// T45: kurulum betikleri kaynaktan derlemeyi bıraktı, yayınlanmış arşivi indiriyor.
/// Betikler bu makinede koşturulmaz — koştukları anda buradaki kurulumu silip yeniden
/// kurarlar. Ölçüm metin üzerinden: indirilen varlık adları güncelleyicinin beklediği
/// adlarla aynı mı, kurulan düzen başlatıcının beklediği düzen mi, SDK indirme izi
/// kaldı mı.
/// </summary>
public sealed class InstallerTests
{
    private static readonly string Root = TipSources.Root;

    private static string WindowsInstaller => File.ReadAllText(Path.Combine(Root, "Install-VidShrink.ps1"));
    private static string UnixInstaller => File.ReadAllText(Path.Combine(Root, "install-vidshrink.sh"));
    private static string ReleaseWorkflow => File.ReadAllText(Path.Combine(Root, ".github", "workflows", "release.yml"));
    private static string LauncherProgram => File.ReadAllText(Path.Combine(Root, "src", "VidShrink.Launcher", "Program.cs"));

    /// <summary>
    /// Varlık adı tek bir yerde tanımlı: <see cref="UpdateCheck"/>. Betikteki ad kabuk
    /// değişkeniyle kuruluyor, o yüzden karşılaştırma sabit parçalar üzerinden yapılıyor —
    /// <c>UpdateCheck</c> tarafındaki bir ad değişikliği bu ölçümü düşürür.
    /// </summary>
    private static (string Prefix, string Suffix) Split(string template)
    {
        var index = template.IndexOf("RID", StringComparison.Ordinal);
        return (template[..index], template[(index + 3)..]);
    }

    [Fact]
    public void TheArchiveNameTheScriptsFetchIsTheNameTheUpdaterExpects()
    {
        var (prefix, suffix) = Split(UpdateCheck.ArchiveAssetName("RID"));

        Assert.Contains($"\"{prefix}$runtimeIdentifier{suffix}\"", WindowsInstaller, StringComparison.Ordinal);
        Assert.Contains($"\"{prefix}$runtime{suffix}\"", UnixInstaller, StringComparison.Ordinal);
        Assert.Contains($"{prefix}${{{{ matrix.rid }}}}{suffix}", ReleaseWorkflow, StringComparison.Ordinal);
    }

    [Fact]
    public void TheManifestNameTheWorkflowWritesIsTheNameTheUpdaterExpects()
    {
        var (prefix, suffix) = Split(UpdateCheck.ManifestAssetName("RID"));

        Assert.Contains($"{prefix}${{{{ matrix.rid }}}}{suffix}", ReleaseWorkflow, StringComparison.Ordinal);
    }

    [Fact]
    public void TheReleaseCarriesTheLauncherForWindows()
    {
        Assert.Contains("src/VidShrink.Launcher/VidShrink.Launcher.csproj", ReleaseWorkflow, StringComparison.Ordinal);
        Assert.Contains("vidshrink-launcher-${{ matrix.rid }}.zip", ReleaseWorkflow, StringComparison.Ordinal);
        Assert.Contains("checksummed=\"$checksummed vidshrink-launcher-${{ matrix.rid }}.zip\"", ReleaseWorkflow, StringComparison.Ordinal);
        Assert.Contains("vidshrink-launcher-$runtimeIdentifier.zip", WindowsInstaller, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWindowsInstallerLaysOutWhatTheLauncherLooksFor()
    {
        // Başlatıcı kendi klasörünün altında app\ arar ve ffmpeg'i tools\ffmpeg'de bekler.
        Assert.Contains("\"app\"", LauncherProgram, StringComparison.Ordinal);
        Assert.Contains("\"tools\", \"ffmpeg\"", LauncherProgram, StringComparison.Ordinal);

        Assert.Contains("Join-Path $stageRoot 'app'", WindowsInstaller, StringComparison.Ordinal);
        Assert.Contains("Join-Path $stageRoot 'tools\\ffmpeg'", WindowsInstaller, StringComparison.Ordinal);
        Assert.Contains("Join-Path $resolvedInstallRoot 'VidShrink.exe'", WindowsInstaller, StringComparison.Ordinal);
        Assert.Contains("app\\VidShrink.App.exe", WindowsInstaller, StringComparison.Ordinal);
    }

    [Fact]
    public void TheShortcutsPointAtTheLauncherAndNotAtTheApplication()
    {
        foreach (var line in WindowsInstaller.Split('\n'))
        {
            if (!line.Contains(".TargetPath", StringComparison.Ordinal)) continue;
            Assert.Contains("$installedExe", line, StringComparison.Ordinal);
        }

        Assert.Contains("$installedExe = Join-Path $resolvedInstallRoot 'VidShrink.exe'", WindowsInstaller, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("dotnet publish")]
    [InlineData("dotnet-install")]
    [InlineData("--list-sdks")]
    [InlineData("archive/refs/heads/main")]
    public void NeitherScriptBuildsFromSourceAnyMore(string trace)
    {
        Assert.DoesNotContain(trace, WindowsInstaller, StringComparison.Ordinal);
        Assert.DoesNotContain(trace, UnixInstaller, StringComparison.Ordinal);
    }

    [Fact]
    public void BothScriptsVerifyWhatTheyDownloaded()
    {
        Assert.Contains("checksums-$runtimeIdentifier.txt", WindowsInstaller, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", WindowsInstaller, StringComparison.Ordinal);
        Assert.Contains("checksums-$runtime.txt", UnixInstaller, StringComparison.Ordinal);
        Assert.Contains("sha256_of", UnixInstaller, StringComparison.Ordinal);
    }

    [Fact]
    public void BothScriptsWriteTheVersionMarker()
    {
        Assert.Contains(UpdateCheck.VersionMarkerName, WindowsInstaller, StringComparison.Ordinal);
        Assert.Contains(UpdateCheck.VersionMarkerName, UnixInstaller, StringComparison.Ordinal);
    }

    /// <summary>
    /// Windows PowerShell 5.1'in <c>-Encoding UTF8</c> anahtarı bayt sırası işareti yazar.
    /// İşareti okuyan taraf <see cref="UpdateCheck.ReadVersionMarker"/>; işaretli dosyayı da
    /// temiz okuduğu burada ölçülüyor, yoksa kurulan sürüm hiçbir sürümle eşleşmez.
    /// </summary>
    [Fact]
    public void AVersionMarkerWrittenWithAByteOrderMarkStillReadsClean()
    {
        var directory = Path.Combine(Path.GetTempPath(), "vidshrink-marker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(
                Path.Combine(directory, UpdateCheck.VersionMarkerName),
                "0.1.0",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            Assert.Equal("0.1.0", UpdateCheck.ReadVersionMarker(directory));
        }
        finally
        {
            try { Directory.Delete(directory, true); }
            catch (IOException) { }
        }
    }

    /// <summary>
    /// Yayında yalnız dört hedef var. Betikler desteklenmeyen mimaride yanlış arşivi
    /// kurmak yerine duruyor; durmazlarsa kurulum çalışır ama güncelleme hiç bulunmaz.
    /// </summary>
    [Fact]
    public void AnUnsupportedArchitectureStopsTheInstallation()
    {
        Assert.Contains("Bu mimari için yayın yok", WindowsInstaller, StringComparison.Ordinal);
        Assert.Contains("Bu mimari için yayın yok", UnixInstaller, StringComparison.Ordinal);
        Assert.DoesNotContain("win-arm64", WindowsInstaller, StringComparison.Ordinal);
        Assert.DoesNotContain("linux-arm", UnixInstaller, StringComparison.Ordinal);
    }

    /// <summary>
    /// v0.1.0 başlatıcı taşımıyor. Betik varlığı bulamazsa yarım kurulum bırakmıyor.
    /// </summary>
    [Fact]
    public void AMissingAssetStopsTheInstallationInsteadOfLeavingItHalfDone()
    {
        Assert.Contains("Yayın $tag bu varlığı taşımıyor", WindowsInstaller, StringComparison.Ordinal);
        Assert.Contains("Yayın $tag bu varlığı taşımıyor", UnixInstaller, StringComparison.Ordinal);
    }

    [Fact]
    public void TheParameterSurfaceOfTheWindowsInstallerIsUnchanged()
    {
        foreach (var parameter in new[] { "$InstallRoot", "$NoLaunch", "$SkipShortcuts" })
            Assert.Contains(parameter, WindowsInstaller, StringComparison.Ordinal);
    }
}
