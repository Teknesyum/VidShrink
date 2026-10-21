using System.Diagnostics;
using Avalonia.Controls;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

public sealed class DuzenleyiciVekilTests
{
    private static string Klasor(string ad)
    {
        var yol = Path.Combine(GirdiKanit.Root, ".calisma", "vekil", ad);
        if (Directory.Exists(yol)) Directory.Delete(yol, true);
        Directory.CreateDirectory(yol);
        return yol;
    }

    private static MediaInfo Bilgi(int w, int h, string codec = "h264", int bit = 8) => new()
    {
        FilePath = "x.mp4", FileSizeBytes = 1, DurationSeconds = 1, Width = w, Height = h, Fps = 30,
        VideoCodec = codec, TotalBitrateBps = 1, BitDepth = bit,
    };

    [Theory]
    [InlineData(3840, 2160, 960, 540)]
    [InlineData(1920, 1080, 960, 540)]
    [InlineData(1280, 720, 960, 540)]
    [InlineData(1080, 1920, 304, 540)]
    [InlineData(320, 240, 320, 240)]
    [InlineData(321, 241, 320, 240)]
    public void BoyutYuksekligi540eIndirirCiftTutar(int w, int h, int bw, int bh)
    {
        Assert.Equal((bw, bh), ProxyMedia.Size(w, h));
    }

    [Fact]
    public void AgirKaynakTaninir()
    {
        Assert.True(ProxyMedia.IsHeavy(Bilgi(3840, 2160)));
        Assert.True(ProxyMedia.IsHeavy(Bilgi(1920, 1080, "hevc")));
        Assert.True(ProxyMedia.IsHeavy(Bilgi(1920, 1080, bit: 10)));
        Assert.False(ProxyMedia.IsHeavy(Bilgi(1920, 1080)));
        Assert.False(ProxyMedia.IsHeavy(Bilgi(1280, 720)));
    }

    [Fact]
    public void ArgumanlarIkiIsParcacigiKisaGopVeOlcek()
    {
        var a = ProxyMedia.Arguments("kaynak.mov", "vekil.mp4", 3840, 2160);
        Assert.Equal("kaynak.mov", a[a.ToList().IndexOf("-i") + 1]);
        Assert.Equal("vekil.mp4", a[^1]);
        Assert.Equal("scale=960:540", a[a.ToList().IndexOf("-vf") + 1]);
        Assert.Equal("12", a[a.ToList().IndexOf("-g") + 1]);
        Assert.Equal("0", a[a.ToList().IndexOf("-bf") + 1]);
        var threads = a.Select((x, i) => (x, i)).Where(p => p.x == "-threads").Select(p => a[p.i + 1]).ToArray();
        Assert.NotEmpty(threads);
        Assert.All(threads, t => Assert.Equal("2", t));
        Assert.Equal("2", a[a.ToList().IndexOf("-filter_threads") + 1]);
    }

    [Fact]
    public void KaynakDegisinceVekilGecersizSilmeHepsiniGoturur()
    {
        var k = Klasor("damga");
        var kaynak = Path.Combine(k, "kaynak.bin");
        File.WriteAllBytes(kaynak, new byte[10]);
        var klasor = Path.Combine(k, "vekiller");
        Directory.CreateDirectory(klasor);
        var vekil = ProxyMedia.PathFor(klasor, SourceStamp.Of(kaynak)!);
        File.WriteAllBytes(vekil, new byte[1]);

        Assert.Equal(vekil, ProxyMedia.Find(klasor, kaynak));
        Assert.Equal(new[] { vekil }, ProxyMedia.PreviewPaths(klasor, new[] { kaynak }));

        File.SetLastWriteTimeUtc(kaynak, File.GetLastWriteTimeUtc(kaynak).AddMinutes(-5));
        Assert.Null(ProxyMedia.Find(klasor, kaynak));
        Assert.Equal(new[] { kaynak }, ProxyMedia.PreviewPaths(klasor, new[] { kaynak }));

        var yeni = ProxyMedia.PathFor(klasor, SourceStamp.Of(kaynak)!);
        File.WriteAllBytes(yeni, new byte[1]);
        var baska = Path.Combine(k, "baska.bin");
        File.WriteAllBytes(baska, new byte[3]);
        var baskaVekil = ProxyMedia.PathFor(klasor, SourceStamp.Of(baska)!);
        File.WriteAllBytes(baskaVekil, new byte[1]);

        Assert.Equal(1, ProxyMedia.DeleteStale(klasor, kaynak, yeni));
        Assert.False(File.Exists(vekil));
        Assert.True(File.Exists(yeni));
        Assert.Equal(1, ProxyMedia.Delete(klasor, kaynak));
        Assert.False(File.Exists(yeni));
        Assert.True(File.Exists(baskaVekil));
        Directory.Delete(k, true);
    }

    [Fact]
    public void IptalYarimDosyaBirakmaz()
    {
        var k = Klasor("iptal");
        var kaynak = Path.Combine(k, "kaynak.mp4");
        Ffmpeg("-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=1", "-c:v", "libx264", "-preset", "ultrafast", "-threads", "2", kaynak);
        var klasor = Path.Combine(k, "vekiller");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => ProxyRunner.CreateAsync(kaynak, klasor, 1, 320, 240, null, cts.Token).GetAwaiter().GetResult());
        Assert.Empty(Directory.Exists(klasor) ? Directory.GetFiles(klasor) : Array.Empty<string>());
        Directory.Delete(k, true);
    }

    [Fact]
    public void OnizlemeVekildenTeslimAsilDosyadan()
    {
        var k = Klasor("pencere");
        var kaynak = Path.Combine(k, "kaynak.mp4");
        var klasor = Path.Combine(k, "vekiller");
        Ffmpeg("-hide_banner", "-y", "-f", "lavfi", "-i", "testsrc2=s=1280x720:r=30:d=3", "-f", "lavfi", "-i", "sine=f=440:d=3",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "90", "-threads", "2", "-c:a", "aac", "-shortest", kaynak);

        var s = AppHost.Run(() =>
        {
            var view = new EditorView { RevealFolder = _ => { }, ProxyFolder = klasor };
            view.Player.EngineFactory = () => new YolMotoru(sure: 3);
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                var ac = view.OpenSourceAsync(kaynak);
                DenetimSurucu.Pump(view.Player, () => ac.IsCompleted, 10);
                Assert.True(ac.IsCompleted);
                var onceOnizleme = view.Driver!.Preview.SourcePaths.ToArray();

                var yap = view.CreateProxiesAsync();
                DenetimSurucu.Pump(view.Player, () => yap.IsCompleted, 60);
                Assert.True(yap.IsCompleted);
                DenetimSurucu.Pump(view.Player, () => view.Driver?.Preview.SourcePaths[0] != kaynak, 10);
                var vekilOnizleme = view.Driver!.Preview.SourcePaths.ToArray();
                var durum = view.ProxyStatusText;

                var teslim = view.ExportToAsync(Path.Combine(k, "teslim.mp4"));
                DenetimSurucu.Pump(view.Player, () => teslim.IsCompleted, 60);
                Assert.True(teslim.IsCompleted);
                var plan = view.LastPlan;

                File.SetLastWriteTimeUtc(kaynak, File.GetLastWriteTimeUtc(kaynak).AddMinutes(-5));
                var eskiyince = view.PreviewPaths.ToArray();
                var silinen = view.DeleteProxies();
                return (Yap: yap.Result, Once: onceOnizleme, Vekil: vekilOnizleme, Durum: durum, Ok: teslim.Result, Plan: plan,
                    Eski: eskiyince, Silinen: silinen);
            }
            finally
            {
                view.Player.Close();
                window.Close();
            }
        });

        Assert.True(s.Yap);
        Assert.Equal(new[] { kaynak }, s.Once);
        var vekil = Assert.Single(s.Vekil);
        Assert.StartsWith(klasor, vekil);
        Assert.True(File.Exists(Path.Combine(k, "teslim.mp4")));
        Assert.Equal(VidShrink.App.Localization.Strings.Get("editor.proxy.ready"), s.Durum);
        Assert.True(s.Ok);
        var girdiler = s.Plan!.Steps.SelectMany(st => st.Args.Select((a, i) => (a, i)).Where(p => p.a == "-i").Select(p => st.Args[p.i + 1])).ToArray();
        Assert.Contains(kaynak, girdiler);
        Assert.DoesNotContain(girdiler, g => g.StartsWith(klasor, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(new[] { kaynak }, s.Eski);
        Assert.Equal(1, s.Silinen);
        Assert.Empty(Directory.GetFiles(klasor));
        Directory.Delete(k, true);
    }

    private static void Ffmpeg(params string[] args)
    {
        var psi = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        if (!surec.WaitForExit(30000))
        {
            surec.Kill(true);
            Assert.Fail("ffmpeg 30 sn icinde bitmedi.");
        }

        Assert.True(surec.ExitCode == 0, hata.GetAwaiter().GetResult());
    }
}
