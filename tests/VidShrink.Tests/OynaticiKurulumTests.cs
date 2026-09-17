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
        var yedek = Sabit(kurulum, "libMpvFallbackUrl");
        var arsiv = Sabit(kurulum, "libMpvArchiveSha256");
        var dll = Sabit(kurulum, "libMpvDllSha256");
        var arsivAdi = url[(url.LastIndexOf('/') + 1)..];
        var surum = Regex.Match(arsivAdi, @"^mpv-dev-x86_64-(\d{8})-").Groups[1].Value;
        Assert.Equal($"https://github.com/Teknesyum/VidShrink/releases/download/deps-libmpv-{surum}/{arsivAdi}", url);
        Assert.StartsWith($"https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/{surum}/", yedek, StringComparison.Ordinal);
        Assert.Equal(arsivAdi, yedek[(yedek.LastIndexOf('/') + 1)..]);
        Assert.Contains("foreach ($source in @($libMpvUrl, $libMpvFallbackUrl))", kurulum, StringComparison.Ordinal);
        Assert.Equal(new[] { url, yedek }, VidShrink.Core.Setup.LibMpvPin.Default.Urls);
        foreach (var (ad, metin) in new[] { ("ci.yml", ci), ("release.yml", release) })
        {
            Assert.True(metin.Contains($"$url = '{url}'", StringComparison.Ordinal), $"{ad} libmpv birincil adresi farkli");
            Assert.True(metin.Contains($"$fallbackUrl = '{yedek}'", StringComparison.Ordinal), $"{ad} libmpv yedek adresi farkli");
            Assert.True(metin.Contains("foreach ($source in @($url, $fallbackUrl))", StringComparison.Ordinal), $"{ad} once kendi surumumuzu denemiyor");
            Assert.True(metin.Contains($"$expectedSha256 = '{arsiv}'", StringComparison.Ordinal), $"{ad} libmpv arsiv sha256 farkli");
            Assert.True(metin.Contains($"$expectedDllSha256 = '{dll}'", StringComparison.Ordinal), $"{ad} libmpv-2.dll sha256 farkli");
        }

        Assert.Contains("Join-Path $stageRoot 'tools\\libmpv'", kurulum, StringComparison.Ordinal);
        Assert.Contains("Install-LibMpv $workRoot $libMpvRoot $installedLibMpv", kurulum, StringComparison.Ordinal);
        Assert.Contains("$actual -ne $libMpvArchiveSha256", kurulum, StringComparison.Ordinal);
        Assert.Contains("$dllActual -ne $libMpvDllSha256", kurulum, StringComparison.Ordinal);
    }

    [Fact]
    public void MacosKurulumuSabitlenmisMpvkitLibmpvyiToolsAltinaKoyar()
    {
        var kurulum = Oku("install-vidshrink.sh");
        var kilit = Oku("tools", "mpvkit-macos", "mpvkit-1.0.0.lock");

        var cikti = Regex.Match(kilit, @"^# output (\S+) ([0-9a-f]{64}) (\S+)[ \t]*\r?$", RegexOptions.Multiline);
        Assert.True(cikti.Success, "kilit dosyasinda # output satiri yok");
        var ad = cikti.Groups[1].Value;
        var sha = cikti.Groups[2].Value;
        var url = cikti.Groups[3].Value;

        Assert.Equal($"https://github.com/Teknesyum/VidShrink/releases/download/deps-libmpv-macos-mpvkit-1.0.0/{ad}", url);
        Assert.Contains($"mac_libmpv_url='{url}'", kurulum, StringComparison.Ordinal);
        Assert.Contains($"mac_libmpv_sha256='{sha}'", kurulum, StringComparison.Ordinal);
        Assert.Contains("\"$stage_root/tools/libmpv/libmpv.2.dylib\"", kurulum, StringComparison.Ordinal);
        Assert.Contains("cp -R \"$payload/.\" \"$bundle/Contents/MacOS/\"", Oku("macos-app-bundle.sh"), StringComparison.Ordinal);
        var macos = Path.Combine(KurulumKoku("macos-paket"), "VidShrink.app", "Contents", "MacOS");
        Assert.Contains(Path.Combine(macos, "tools", "libmpv"), LibMpvLocator.AppDirectories(macos));
    }

    [Fact]
    public void MacosAltSurumuDortBelgedeOnDortDiyor()
    {
        var beklenen = new (string Dosya, string Dize)[]
        {
            ("README.md", "macOS 14 or newer"),
            ("README.tr.md", "macOS 14 ve"),
            (Path.Combine("docs", "kurulum.md"), "macOS 14 or newer"),
            (Path.Combine("docs", "kurulum.tr.md"), "macOS 14 ve üstü"),
            (Path.Combine("docs", "YOL-HARITASI.md"), "macOS alt sürümü: 14"),
        };

        foreach (var (dosya, dize) in beklenen)
        {
            var metin = Oku(dosya);
            Assert.Contains(dize, metin, StringComparison.Ordinal);
            var eski = Regex.Matches(metin, @"macOS 15 (or newer|ve üstü|ve\b)|macOS alt sürümü: 15");
            Assert.True(eski.Count == 0, $"{dosya} hala macOS 15 tabani soyluyor: {string.Join(" | ", eski.Select(m => m.Value))}");
        }

        Assert.Contains("MPVKit yolu denendi ve tuttu", Oku(Path.Combine("docs", "YOL-HARITASI.md")), StringComparison.Ordinal);
    }

    [Fact]
    public void GuncellemeDenetimiYayinListesiNumaralandirmaz()
    {
        var kaynaklar = Directory.GetFiles(Path.Combine(Root, "src"), "*.cs", SearchOption.AllDirectories);
        Assert.True(kaynaklar.Length > 50, $"src altinda {kaynaklar.Length} kaynak bulundu");

        foreach (var yol in kaynaklar)
        {
            var metin = File.ReadAllText(yol);
            var ad = Path.GetRelativePath(Root, yol);
            Assert.False(metin.Contains("per_page", StringComparison.Ordinal), $"{ad} sayfali yayin listesi cagiriyor (per_page)");
            foreach (var m in Regex.Matches(metin, @"/releases(?<son>/latest|/tag/|/download/|)").Cast<Match>())
                Assert.True(m.Groups["son"].Value.Length > 0, $"{ad} yayin listesini numaralandiriyor: {metin.Substring(Math.Max(0, m.Index - 40), Math.Min(80, metin.Length - Math.Max(0, m.Index - 40)))}");
        }

        var denetim = Oku("src", "VidShrink.Core", "UpdateCheck.cs");
        Assert.Contains("releases/latest", denetim, StringComparison.Ordinal);
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
