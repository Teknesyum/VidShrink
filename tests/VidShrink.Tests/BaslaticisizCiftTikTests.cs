using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Hipersürüş G2: çift tık <c>app\VidShrink.App.exe</c>'yi doğrudan açıyor. Uygulama bakımı
/// başlatıcıya <c>--bakim</c> ile arkada yaptırıyor; kapılar, ffmpeg'in kökteki yeri ve
/// "Yükle"den sonra rozetin ara metni burada.
/// </summary>
public sealed class BaslaticisizCiftTikTests : IDisposable
{
    private readonly string _root = Path.Combine(TestPaths.OutputRoot, "g2-cift-tik", Guid.NewGuid().ToString("N"));
    private readonly string _app;
    private readonly string _launcher;

    public BaslaticisizCiftTikTests()
    {
        _app = Path.Combine(_root, "kurulum", "app");
        Directory.CreateDirectory(_app);
        _launcher = Path.Combine(_root, "kurulum", "VidShrink.exe");
        File.WriteAllText(_launcher, "baslatici");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    private string? Karar(string appDirectory, string? isaret, string uygulama, string? baslatici)
        => LauncherUpdate.MaintenanceLauncher(appDirectory, isaret, uygulama, _ => baslatici);

    [Fact]
    public void KuruluDuzendeAyniSurumBaslaticiBakimIcinAciliyor()
    {
        Assert.Equal(_launcher, Karar(_app + Path.DirectorySeparatorChar, null, "0.8.5+abc1234", "0.8.5+abc1234"));
        Assert.Equal(_launcher, Karar(_app, "", "0.8.5", "0.9.0"));
    }

    [Fact]
    public void BaslaticininDogurduguUygulamaBakimAcmiyor()
        => Assert.Null(Karar(_app, "1", "0.8.5", "0.8.5"));

    [Fact]
    public void EskiBaslaticiBakimIcinAcilmiyor()
    {
        Assert.Null(Karar(_app, null, "0.8.5+abc1234", "0.8.4+da09b69"));
        Assert.Null(Karar(_app, null, "0.8.5", null));
        Assert.Null(Karar(_app, null, "0.8.5", "bozuk"));
    }

    [Fact]
    public void KuruluDuzenDisindaBakimAcilmiyor()
    {
        var gevsek = Path.Combine(_root, "gevsek");
        Directory.CreateDirectory(gevsek);
        File.WriteAllText(Path.Combine(_root, "VidShrink.exe"), "baslatici");
        Assert.Null(Karar(gevsek, null, "0.8.5", "0.8.5"));

        File.Delete(_launcher);
        Assert.Null(Karar(_app, null, "0.8.5", "0.8.5"));
    }

    /// <summary>
    /// Başlatıcı artık PATH'e <c>tools\ffmpeg</c> eklemeden de açılış oluyor; uygulama kökteki
    /// kopyayı kendi buluyor. Negatif kontrol: klasör <c>app</c> değilse üst dizine bakılmıyor.
    /// </summary>
    [Fact]
    public void AppKlasorundenKoktekiFfmpegBulunuyor()
    {
        var name = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var tools = Path.Combine(_root, "kurulum", "tools", "ffmpeg");
        Directory.CreateDirectory(tools);
        File.WriteAllText(Path.Combine(tools, name), "ffmpeg");

        Assert.Equal(Path.Combine(tools, name), ToolLocator.Locate("ffmpeg", "", _app + Path.DirectorySeparatorChar));

        var baska = Path.Combine(_root, "kurulum", "uygulama");
        Directory.CreateDirectory(baska);
        if (!OperatingSystem.IsMacOS())
            Assert.Throws<FileNotFoundException>(() => ToolLocator.Locate("ffmpeg", "", baska));
    }

    [Fact]
    public void BakimYalnizTekOrnekSahibindeAciliyor()
    {
        var program = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "Program.cs"));
        var runMain = program[program.IndexOf("private static int RunMain(", StringComparison.Ordinal)..];
        runMain = runMain[..runMain.IndexOf("internal static Task WarmPlayback()", StringComparison.Ordinal)];

        var sahip = runMain.IndexOf("instance.StartListening(files.Receive);", StringComparison.Ordinal);
        var bakim = runMain.IndexOf("StartLauncherMaintenance();", StringComparison.Ordinal);
        Assert.True(sahip > 0 && bakim > sahip);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(program, @"StartLauncherMaintenance\(\);"));
        Assert.True(runMain.IndexOf("if (!instance.IsOwner)", StringComparison.Ordinal) < sahip);
        Assert.Contains("start.ArgumentList.Add(LauncherUpdate.MaintenanceArgument);", program, StringComparison.Ordinal);

        var baslatici = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.Launcher", "Program.cs"));
        Assert.Contains("start.Environment[LauncherUpdate.LaunchedVariable] = \"1\";", baslatici, StringComparison.Ordinal);
    }

    /// <summary>
    /// G3: "Yükle"den sonra pencere kapanmadan rozete "Başlatıcı açılıyor…" yazılmıyor.
    /// Negatif kontrol: aynı gövde başlatıcıyı elle yükleme kipinde açıp pencereyi kapatıyor.
    /// </summary>
    [Fact]
    public void YukleRozeteAraMetinYazmiyor()
    {
        var window = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "MainWindow.axaml.cs"));
        var govde = window[window.IndexOf("private void OnInstallUpdate(", StringComparison.Ordinal)..];
        govde = govde[..govde.IndexOf("\n    }", StringComparison.Ordinal)];

        Assert.DoesNotContain("UpdateBadgeState.Installing", govde, StringComparison.Ordinal);
        Assert.DoesNotContain("SetUpdateBadge(", govde, StringComparison.Ordinal);
        Assert.Contains("start.ArgumentList.Add(LauncherUpdate.UpdateNowArgument);", govde, StringComparison.Ordinal);
        Assert.Contains("Close();", govde, StringComparison.Ordinal);
    }
}
