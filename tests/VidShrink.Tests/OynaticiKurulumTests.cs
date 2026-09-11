using System.Diagnostics;
using System.Text.RegularExpressions;
using VidShrink.App.Playback;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

public sealed class OynaticiKurulumTests
{
    private static readonly string Root = TipSources.Root;

    private static string Oku(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string Sabit(string text, string name)
    {
        var match = Regex.Match(text, "\\$" + name + " = '([^']+)'");
        Assert.True(match.Success, $"{name} Install-VidShrink.ps1 icinde yok");
        return match.Groups[1].Value;
    }

    private static string KurulumKoku(string name)
    {
        var path = Path.Combine(MotorKanit.Folder, "konum-" + name);
        if (Directory.Exists(path)) Directory.Delete(path, true);
        Directory.CreateDirectory(Path.Combine(path, "app"));
        return path;
    }

    [Fact]
    public void KurulanDuzendeLibmpvKokunToolsKlasorundenBulunur()
    {
        var kok = KurulumKoku("kurulu");
        var dll = Path.Combine(kok, "tools", "libmpv", LibMpvLocator.FileNames[0]);
        Directory.CreateDirectory(Path.GetDirectoryName(dll)!);
        File.WriteAllBytes(dll, new byte[] { 0 });

        var ilk = LibMpvLocator.Candidates(null, Path.Combine(kok, "app"), macOs: false).First(File.Exists);

        Assert.Equal(Path.GetFullPath(dll), Path.GetFullPath(ilk));
    }

    [Fact]
    public void YayinKlasorundekiLibmpvKokunToolsKlasorundenOnceGelir()
    {
        var kok = KurulumKoku("yayin");
        var yanindaki = Path.Combine(kok, "app", LibMpvLocator.FileNames[0]);
        var tools = Path.Combine(kok, "tools", "libmpv", LibMpvLocator.FileNames[0]);
        Directory.CreateDirectory(Path.GetDirectoryName(tools)!);
        File.WriteAllBytes(yanindaki, new byte[] { 0 });
        File.WriteAllBytes(tools, new byte[] { 0 });

        var ilk = LibMpvLocator.Candidates(null, Path.Combine(kok, "app"), macOs: false).First(File.Exists);

        Assert.Equal(Path.GetFullPath(yanindaki), Path.GetFullPath(ilk));
    }

    [Fact]
    public void OrtamDegiskeniKurulanDuzendenOnceGelir()
    {
        var kok = KurulumKoku("ortam");
        var tools = Path.Combine(kok, "tools", "libmpv", LibMpvLocator.FileNames[0]);
        var ozel = Path.Combine(kok, "ozel", LibMpvLocator.FileNames[0]);
        Directory.CreateDirectory(Path.GetDirectoryName(tools)!);
        Directory.CreateDirectory(Path.GetDirectoryName(ozel)!);
        File.WriteAllBytes(tools, new byte[] { 0 });
        File.WriteAllBytes(ozel, new byte[] { 0 });

        var ilk = LibMpvLocator.Candidates(Path.GetDirectoryName(ozel), Path.Combine(kok, "app"), macOs: false).First(File.Exists);

        Assert.Equal(Path.GetFullPath(ozel), Path.GetFullPath(ilk));
    }

    [Fact]
    public void MacosHomebrewKlasorleriUygulamaKlasorlerindenSonraDenenir()
    {
        var kok = KurulumKoku("mac");
        var adaylar = LibMpvLocator.Candidates(null, Path.Combine(kok, "app"), macOs: true);
        var ortamli = LibMpvLocator.Candidates(Path.Combine(kok, "yok.dylib"), Path.Combine(kok, "app"), macOs: true);
        var windows = LibMpvLocator.Candidates(null, Path.Combine(kok, "app"), macOs: false);

        var homebrew = adaylar.ToList().FindIndex(a => a.StartsWith("/opt/homebrew/lib", StringComparison.Ordinal));
        var sonUygulama = adaylar.ToList().FindLastIndex(a => a.StartsWith(kok, StringComparison.Ordinal));
        Assert.True(homebrew > sonUygulama, $"homebrew {homebrew}, son uygulama adayi {sonUygulama}");
        Assert.Contains(adaylar, a => a.StartsWith("/usr/local/lib", StringComparison.Ordinal));
        Assert.DoesNotContain(ortamli, a => a.StartsWith("/opt/homebrew/lib", StringComparison.Ordinal));
        Assert.DoesNotContain(windows, a => a.StartsWith("/opt/homebrew/lib", StringComparison.Ordinal));
    }

    [Fact]
    public void WindowsKurulumuCiIleAyniSabitLibmpvyiDogrulayipToolsAltinaKoyar()
    {
        var kurulum = Oku("Install-VidShrink.ps1");
        var ci = Oku(".github", "workflows", "ci.yml");
        var release = Oku(".github", "workflows", "release.yml");

        var url = Sabit(kurulum, "libMpvUrl");
        var arsiv = Sabit(kurulum, "libMpvArchiveSha256");
        var dll = Sabit(kurulum, "libMpvDllSha256");
        foreach (var (ad, metin) in new[] { ("ci.yml", ci), ("release.yml", release) })
        {
            Assert.True(metin.Contains($"$url = '{url}'", StringComparison.Ordinal), $"{ad} libmpv adresi farkli");
            Assert.True(metin.Contains($"$expectedSha256 = '{arsiv}'", StringComparison.Ordinal), $"{ad} libmpv arsiv sha256 farkli");
            Assert.True(metin.Contains($"$expectedDllSha256 = '{dll}'", StringComparison.Ordinal), $"{ad} libmpv-2.dll sha256 farkli");
        }

        Assert.Contains("Join-Path $stageRoot 'tools\\libmpv'", kurulum, StringComparison.Ordinal);
        Assert.Contains("Install-LibMpv $workRoot $libMpvRoot $installedLibMpv", kurulum, StringComparison.Ordinal);
        Assert.Contains("$actual -ne $libMpvArchiveSha256", kurulum, StringComparison.Ordinal);
        Assert.Contains("$dllActual -ne $libMpvDllSha256", kurulum, StringComparison.Ordinal);
    }

    [Fact]
    public void UnixKurulumuLibmpvYoksaKomutuSoyleyipDurur()
    {
        var kurulum = Oku("install-vidshrink.sh");

        var ffmpeg = kurulum.IndexOf("\nrequire_ffmpeg\n", StringComparison.Ordinal);
        var libmpv = kurulum.IndexOf("\nrequire_libmpv\n", StringComparison.Ordinal);
        var indirme = kurulum.IndexOf("\nwork_root=", StringComparison.Ordinal);
        Assert.True(ffmpeg > 0 && libmpv > ffmpeg && indirme > libmpv, $"sira: ffmpeg {ffmpeg}, libmpv {libmpv}, indirme {indirme}");
        Assert.Contains("brew install mpv", kurulum, StringComparison.Ordinal);
        Assert.Contains("sudo apt install libmpv2", kurulum, StringComparison.Ordinal);
        Assert.Contains("libmpv\\.so\\.2", kurulum, StringComparison.Ordinal);
        foreach (var dir in LibMpvLocator.MacLibraryDirectories)
            Assert.Contains(dir, kurulum, StringComparison.Ordinal);
    }
}

public sealed class OynaticiGorunumuFabrikaTests
{
    [Fact]
    public void AcilamayanMotorAtilirVeGorunumBosKalir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var view = new PlayerView();
            var sahte = new AcilmayanMotor();
            view.EngineFactory = () => sahte;
            var open = view.OpenAsync("yok.mp4");
            var saat = Stopwatch.StartNew();
            while (!open.IsCompleted && saat.Elapsed < TimeSpan.FromSeconds(5))
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Thread.Sleep(5);
            }

            return (open, sahte, view.Engine);
        });

        Assert.True(sonuc.open.IsFaulted, "OpenAsync hatayi yutmamali");
        Assert.IsType<PlaybackOpenException>(sonuc.open.Exception!.InnerException);
        Assert.Equal(1, sonuc.sahte.Acilis);
        Assert.True(sonuc.sahte.Atildi, "acilamayan motor Dispose edilmedi");
        Assert.Equal(0, sonuc.sahte.FaultedAboneleri);
        Assert.Null(sonuc.Item3);
    }

    private sealed class AcilmayanMotor : IPlaybackEngine
    {
        private EventHandler<PlaybackFault>? _faulted;

        public int Acilis { get; private set; }

        public bool Atildi { get; private set; }

        public int FaultedAboneleri => _faulted?.GetInvocationList().Length ?? 0;

        public string Name => "sahte";

        public bool IsOpen => false;

        public double DurationSeconds => 0;

        public bool HasAudio => false;

        public bool IsPaused => true;

        public bool EndReached => false;

        public double PositionSeconds => 0;

        public double AudioVideoOffsetSeconds => double.NaN;

        public long FramesRendered => 0;

        public event EventHandler<PlaybackFault>? Faulted
        {
            add => _faulted += value;
            remove => _faulted -= value;
        }

        public Task OpenAsync(string path, CancellationToken ct = default)
        {
            Acilis++;
            return Task.FromException(new PlaybackOpenException("sahte motor acilamadi"));
        }

        public void Play() { }

        public void Pause() { }

        public Task<SeekResult> SeekAsync(double seconds, SeekPrecision precision, CancellationToken ct = default)
            => Task.FromResult(new SeekResult(SeekOutcome.Failed, 0));

        public bool TryCopyLatest(ref long seen, FrameCopy copy) => false;

        public void Dispose() => Atildi = true;
    }
}
