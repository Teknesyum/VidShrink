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

    /// <summary>
    /// T56: bir kullanıcının makinesinde mimari okuması boş döndü ve boş değer
    /// "desteklenmeyen mimari" sayılıp kurulum reddedildi. Karar artık
    /// <see cref="ArchitectureChoice"/> içinde ve betikten ayrı sınanabiliyor;
    /// betiğin kendisi koşturulmuyor, koştuğu anda buradaki kurulumu değiştirir.
    /// </summary>
    [Theory]
    [InlineData("X64")]
    [InlineData("AMD64")]
    [InlineData("x86_64")]
    public void AKnownX64ReadingIsX64(string reading)
    {
        var decision = ArchitectureChoice.Decide(reading, null, null, is64BitOperatingSystem: true);

        Assert.Equal(ArchitectureOutcome.Read, decision.Outcome);
        Assert.Equal("x64", decision.Architecture);
        Assert.Equal(string.Empty, decision.Note);
    }

    [Theory]
    [InlineData("Arm64")]
    [InlineData("aarch64")]
    public void AKnownArm64ReadingIsArm64(string reading)
    {
        var decision = ArchitectureChoice.Decide(reading, null, null, is64BitOperatingSystem: true);

        Assert.Equal(ArchitectureOutcome.Read, decision.Outcome);
        Assert.Equal("arm64", decision.Architecture);
    }

    [Theory]
    [InlineData("X86")]
    [InlineData("i686")]
    public void AKnownX86ReadingIsX86(string reading)
    {
        var decision = ArchitectureChoice.Decide(reading, null, null, is64BitOperatingSystem: false);

        Assert.Equal(ArchitectureOutcome.Read, decision.Outcome);
        Assert.Equal("x86", decision.Architecture);
    }

    /// <summary>
    /// Sözleşmenin sebebi olan durum: hiçbir kaynak ad vermiyor. Reddedilmiyor, varsayılıyor —
    /// ve varsayıldığı söylenecek bir cümle geri geliyor.
    /// </summary>
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", "", "")]
    [InlineData("   ", null, "")]
    [InlineData("RiscV64", null, "SPARC")]
    public void AnUnreadableArchitectureIsAssumedOn64BitAndSaidOutLoud(
        string? runtime, string? architew6432, string? processor)
    {
        var decision = ArchitectureChoice.Decide(runtime, architew6432, processor, is64BitOperatingSystem: true);

        Assert.Equal(ArchitectureOutcome.Assumed, decision.Outcome);
        Assert.Equal("x64", decision.Architecture);
        Assert.False(string.IsNullOrWhiteSpace(decision.Note));
    }

    [Fact]
    public void AnUnreadableArchitectureOn32BitIsX86()
    {
        var decision = ArchitectureChoice.Decide(null, null, null, is64BitOperatingSystem: false);

        Assert.Equal(ArchitectureOutcome.Assumed, decision.Outcome);
        Assert.Equal("x86", decision.Architecture);
        Assert.False(string.IsNullOrWhiteSpace(decision.Note));
    }

    /// <summary>
    /// WOW64: 64 bit Windows üzerinde koşan 32 bit bir süreçte PROCESSOR_ARCHITECTURE
    /// sürecin mimarisini söyler, işletim sisteminkini PROCESSOR_ARCHITEW6432 söyler.
    /// Sıra tersine dönerse 64 bit makine x86 sanılır ve kurulum yine düşer.
    /// </summary>
    [Fact]
    public void TheOperatingSystemsArchitectureBeatsTheProcessArchitecture()
    {
        var decision = ArchitectureChoice.Decide(null, "AMD64", "x86", is64BitOperatingSystem: true);

        Assert.Equal(ArchitectureOutcome.Read, decision.Outcome);
        Assert.Equal("x64", decision.Architecture);
    }

    /// <summary>Mimari adı hiçbir sonuçta boş değil; boş bir değer iletiye giremez.</summary>
    [Fact]
    public void TheDecisionNeverCarriesAnEmptyArchitecture()
    {
        foreach (var is64Bit in new[] { true, false })
        foreach (var reading in new string?[] { null, "", "   ", "X64", "ARM64", "X86", "BILINMEYEN" })
            Assert.False(string.IsNullOrWhiteSpace(
                ArchitectureChoice.Decide(reading, null, null, is64Bit).Architecture));
    }

    /// <summary>Güncelleyici ile kurucu aynı kuralı okuyor: <c>Rid</c> kararın kendisini taşır.</summary>
    [Fact]
    public void TheUpdaterUsesTheSameDecision()
    {
        Assert.EndsWith("-" + ArchitectureChoice.Decide().Architecture, UpdateCheck.Rid, StringComparison.Ordinal);
    }

    /// <summary>Betik tek okumaya güvenmiyor: dört kaynağın dördü de içinde geçiyor.</summary>
    [Theory]
    [InlineData("RuntimeInformation]::OSArchitecture")]
    [InlineData("$env:PROCESSOR_ARCHITEW6432")]
    [InlineData("$env:PROCESSOR_ARCHITECTURE")]
    [InlineData("[Environment]::Is64BitOperatingSystem")]
    public void TheWindowsInstallerReadsTheArchitectureFromMoreThanOnePlace(string source)
    {
        Assert.Contains(source, WindowsInstaller, StringComparison.Ordinal);
    }

    /// <summary>
    /// Düşen sürümde ileti <c>$architecture</c> değişkenini basıyordu ve o değişken boştu.
    /// Mimari adı artık yalnız tanınan bir addan geliyor.
    /// </summary>
    [Fact]
    public void TheWindowsInstallerNoLongerPrintsARawArchitectureReading()
    {
        Assert.DoesNotContain("$architecture", WindowsInstaller, StringComparison.Ordinal);
        Assert.Contains("$($decision.Architecture)", WindowsInstaller, StringComparison.Ordinal);
    }

    /// <summary>Okunamayan mimari kurulumu durdurmuyor; ne varsayıldığı tek satırla söyleniyor.</summary>
    [Fact]
    public void AnUnreadableArchitectureDoesNotStopTheWindowsInstaller()
    {
        Assert.Contains("Mimari okunamadı; işletim sistemi 64 bit olduğu için x64 varsayıldı.", WindowsInstaller, StringComparison.Ordinal);
        Assert.Contains("if ($decision.Note) { Write-Host $decision.Note", WindowsInstaller, StringComparison.Ordinal);
    }

    /// <summary>
    /// K5: aynı tuzak <c>install-vidshrink.sh</c> içinde de vardı — <c>uname -m</c> boş dönerse
    /// boş değer "desteklenmeyen mimari" olarak basılıyordu. Orada da okunamama ayrı söyleniyor.
    /// </summary>
    [Fact]
    public void TheUnixInstallerSeparatesUnreadableFromUnsupported()
    {
        Assert.Contains("uname -m 2>/dev/null", UnixInstaller, StringComparison.Ordinal);
        Assert.Contains("Mimari okunamadı: uname -m ve uname -p boş döndü.", UnixInstaller, StringComparison.Ordinal);
        Assert.Contains("İşletim sistemi okunamadı: uname -s boş döndü.", UnixInstaller, StringComparison.Ordinal);
    }

    [Fact]
    public void TheParameterSurfaceOfTheWindowsInstallerIsUnchanged()
    {
        foreach (var parameter in new[] { "$InstallRoot", "$NoLaunch", "$SkipShortcuts" })
            Assert.Contains(parameter, WindowsInstaller, StringComparison.Ordinal);
    }
}
