using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Canli <c>ddagrab</c> olcusu: <see cref="KayitFactAttribute"/>'in sartlarina ek olarak bu
/// makinede birincil cikisin sol ustunde 640x480'lik <c>ddagrab</c> yoklamasi gecmeli.
/// Uzak masaustunde, GPU'suz koşucuda ve <c>ddagrab</c>'siz derlemede sebebiyle atlanir.
/// </summary>
public sealed class DdagrabFactAttribute : FactAttribute
{
    internal static readonly Lazy<RecorderRequest?> Istek = new(() =>
    {
        var cikislar = DdaOutputs.Enumerate();
        var istek = KaydediciDdagrabTests.Bolge(0, 0, 640, 480, cikislar) with { Fps = 60, Preset = "ultrafast" };
        if (RecorderArguments.DdagrabBlocker(istek) != DdagrabFallback.None) return null;
        return DdagrabProbe.WorksAsync(istek).GetAwaiter().GetResult()
            ? istek with { Capture = RecorderCapture.Ddagrab }
            : null;
    });

    public DdagrabFactAttribute()
    {
        var kayit = new KayitFactAttribute();
        if (kayit.Skip is not null) Skip = kayit.Skip;
        else if (Istek.Value is null) Skip = "ddagrab bu makinede acilmadi (yoklama gecmedi), canli ddagrab olcusu kosturulmadi.";
    }
}

/// <summary>
/// Windows'ta <c>ddagrab</c> (Desktop Duplication) yakalama yolu: cikis secimi ve yerel ofset,
/// tam cikista ofsetin yazilmamasi, pencerenin istemci alanina kirpma, <c>hwdownload</c>
/// zinciri; her yerlesim engelinin <c>gdigrab</c>'a dusmesi ve dususun ekranda soylenmesi;
/// yoklama gecince otomatik kipin 30 kare tavaninin kalkmasi. Canli kol 3 sn, 640x480, 60 fps.
/// </summary>
public sealed class KaydediciDdagrabTests
{
    private static readonly DdaOutput Ana = new(0, 0, 0, 2560, 1440);
    private static readonly DdaOutput Yan = new(1, 2560, 0, 1920, 1080);
    private static readonly IReadOnlyList<DdaOutput> IkiCikis = new[] { Ana, Yan };

    internal static RecorderRequest Bolge(int x, int y, int w, int h, IReadOnlyList<DdaOutput> cikislar) => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Region,
        Region = new RecorderRegion(x, y, w, h),
        DdaOutputs = cikislar,
        Fps = 60
    };

    private static string Kaynak(IReadOnlyList<string> args)
    {
        var i = args.ToList().IndexOf("lavfi");
        Assert.True(i >= 1 && args[i - 1] == "-f" && args[i + 1] == "-i", "ddagrab lavfi girdisi olarak girmeli: " + string.Join(" ", args));
        return args[i + 2];
    }

    private static RecorderRequest Dda(RecorderRequest istek) => istek with { Capture = RecorderCapture.Ddagrab };

    [Fact]
    public void IkinciCikistakiBolgeYerelOfsetleGirer()
    {
        var args = RecorderArguments.Build(Dda(Bolge(2600, 100, 640, 480, IkiCikis)), @"C:\kayit\a.mp4");

        Assert.Equal("ddagrab=output_idx=1:framerate=60:draw_mouse=1:offset_x=40:offset_y=100:video_size=640x480,hwdownload,format=bgra", Kaynak(args));
        Assert.DoesNotContain("gdigrab", args);
        Assert.DoesNotContain("-offset_x", args);
        Assert.Empty(RecorderArguments.Validate(Dda(Bolge(2600, 100, 640, 480, IkiCikis)), @"C:\kayit\a.mp4"));
    }

    [Fact]
    public void CikisSirasiKoordinattanOkunur()
    {
        var ters = new[] { new DdaOutput(2, 0, 0, 1920, 1080), new DdaOutput(0, -2560, 0, 2560, 1440) };

        var args = RecorderArguments.Build(Dda(Bolge(0, 0, 640, 480, ters)), @"C:\kayit\a.mp4");

        Assert.StartsWith("ddagrab=output_idx=2:", Kaynak(args));
    }

    [Fact]
    public void TamCikisOfsetYazmazVeImlecKapaliKalir()
    {
        var istek = new RecorderRequest
        {
            Platform = RecorderPlatform.Windows,
            Target = RecorderTargetKind.Screen,
            ScreenIndex = 1,
            Screens = new[] { new ScreenBounds(0, 0, 0, 2560, 1440), new ScreenBounds(1, 2560, 0, 1920, 1080) },
            DdaOutputs = IkiCikis,
            ShowCursor = false,
            Fps = 30
        };

        var kaynak = Kaynak(RecorderArguments.Build(Dda(istek), @"C:\kayit\a.mp4"));

        Assert.Equal("ddagrab=output_idx=1:framerate=30:draw_mouse=0,hwdownload,format=bgra", kaynak);
    }

    [Fact]
    public void MonitorListesiBosBirincilEkranTamCikistir()
    {
        var istek = new RecorderRequest { Platform = RecorderPlatform.Windows, Target = RecorderTargetKind.Screen, DdaOutputs = IkiCikis };

        Assert.Equal(DdagrabFallback.None, RecorderArguments.DdagrabBlocker(istek));
        Assert.StartsWith("ddagrab=output_idx=0:framerate=", Kaynak(RecorderArguments.Build(Dda(istek), @"C:\kayit\a.mp4")));
        Assert.DoesNotContain("offset_x", Kaynak(RecorderArguments.Build(Dda(istek), @"C:\kayit\a.mp4")));
    }

    [Fact]
    public void PencereIstemciAlaninaCiftBoyutlaKirpilir()
    {
        var istek = new RecorderRequest
        {
            Platform = RecorderPlatform.Windows,
            Target = RecorderTargetKind.Window,
            WindowRegion = new RecorderRegion(101, 51, 801, 601),
            DdaOutputs = IkiCikis
        };

        Assert.Empty(RecorderArguments.Validate(Dda(istek), @"C:\kayit\a.mp4"));
        Assert.Contains(":offset_x=101:offset_y=51:video_size=800x600,", Kaynak(RecorderArguments.Build(Dda(istek), @"C:\kayit\a.mp4")));
        Assert.Contains("Window capture on gdigrab needs the window title.", RecorderArguments.Validate(istek, @"C:\kayit\a.mp4"));
    }

    [Fact]
    public void GdigrabKoluDegismez()
    {
        var args = RecorderArguments.Build(Bolge(2600, 100, 640, 480, IkiCikis), @"C:\kayit\a.mp4");

        Assert.Contains("gdigrab", args);
        Assert.DoesNotContain(args, a => a.Contains("ddagrab", StringComparison.Ordinal));
        Assert.Equal("2600", KaydediciAyarTests.Deger(args, "-offset_x"));
    }

    [Fact]
    public void IkiCikisaYayilanBolgeGdigrabaDuser()
    {
        var istek = Bolge(2400, 0, 640, 480, IkiCikis);

        var secilen = RecorderArguments.ChooseCapture(istek, ddagrabWorks: true, out var sebep);

        Assert.Equal(DdagrabFallback.OutsideOneOutput, sebep);
        Assert.Equal(RecorderCapture.Gdigrab, secilen.Capture);
        Assert.Contains("gdigrab", RecorderArguments.Build(secilen, @"C:\kayit\a.mp4"));
        Assert.Contains(RecorderArguments.Validate(Dda(istek), @"C:\kayit\a.mp4"), e => e.Contains("single DXGI output", StringComparison.Ordinal));
    }

    [Fact]
    public void YerlesimEngelleriAyriSebepVerir()
    {
        Assert.Equal(DdagrabFallback.NoOutputs, RecorderArguments.DdagrabBlocker(Bolge(0, 0, 640, 480, Array.Empty<DdaOutput>())));
        Assert.Equal(DdagrabFallback.NoWindowRect, RecorderArguments.DdagrabBlocker(new RecorderRequest
        {
            Platform = RecorderPlatform.Windows,
            Target = RecorderTargetKind.Window,
            WindowTitle = "Adsiz",
            DdaOutputs = IkiCikis
        }));
        Assert.Equal(DdagrabFallback.Unavailable, RecorderArguments.DdagrabBlocker(Bolge(0, 0, 640, 480, IkiCikis) with { Platform = RecorderPlatform.MacOs }));
        Assert.Equal(DdagrabFallback.None, RecorderArguments.DdagrabBlocker(Bolge(0, 0, 640, 480, IkiCikis)));
    }

    [Fact]
    public void YoklamaGecmezseGdigrabaDuserGecerseDdagrab()
    {
        var istek = Bolge(0, 0, 640, 480, IkiCikis);

        var dusen = RecorderArguments.ChooseCapture(istek, ddagrabWorks: false, out var sebep);
        var gecen = RecorderArguments.ChooseCapture(istek, ddagrabWorks: true, out var yok);

        Assert.Equal((RecorderCapture.Gdigrab, DdagrabFallback.Unavailable), (dusen.Capture, sebep));
        Assert.Equal((RecorderCapture.Ddagrab, DdagrabFallback.None), (gecen.Capture, yok));
    }

    [Fact]
    public void WindowsDisindaDdagrabReddedilirVeSecilmez()
    {
        var mac = Bolge(0, 0, 640, 480, IkiCikis) with { Platform = RecorderPlatform.MacOs };

        Assert.Contains("ddagrab (Desktop Duplication) exists only on Windows.", RecorderArguments.Validate(Dda(mac), "/tmp/a.mp4"));
        var secilen = RecorderArguments.ChooseCapture(mac, ddagrabWorks: true, out var sebep);
        Assert.Equal((RecorderCapture.Gdigrab, DdagrabFallback.None), (secilen.Capture, sebep));
    }

    [Fact]
    public void YoklamaTekKareNullCikisVeEngeldeAtar()
    {
        var args = RecorderArguments.BuildDdagrabProbe(Bolge(0, 0, 640, 480, IkiCikis));

        Assert.Equal(new[] { "-frames:v", "1", "-f", "null", "-" }, args.TakeLast(5));
        Assert.StartsWith("ddagrab=output_idx=0:", Kaynak(args));
        Assert.Throws<InvalidOperationException>(() => RecorderArguments.BuildDdagrabProbe(Bolge(2400, 0, 640, 480, IkiCikis)));
    }

    [Fact]
    public void AnlikKareGdigrabdaKalir()
    {
        var args = RecorderArguments.BuildSnapshot(Dda(Bolge(0, 0, 640, 480, IkiCikis)), @"C:\kayit\a.png");

        Assert.Contains("gdigrab", args);
        Assert.DoesNotContain(args, a => a.Contains("ddagrab", StringComparison.Ordinal));
    }

    private static T Gorunumle<T>(Func<RecorderView, T> olc) => KaydediciAyarTests.AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
    {
        var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true };
        return olc(view);
    }));

    private static string Metin(string anahtar)
        => VidShrink.App.LanguageCatalog.Display(VidShrink.App.Localization.Strings.Get(anahtar));

    [Fact]
    public void YoklamaGecmeyinceDususSatiriGorunur()
    {
        var olcu = Gorunumle(view =>
        {
            view.DdaOutputSource = () => IkiCikis;
            view.DdagrabCheck = _ => Task.FromResult(false);
            var secilen = view.ChooseCaptureAsync(Bolge(0, 0, 640, 480, Array.Empty<DdaOutput>())).GetAwaiter().GetResult();
            return (secilen.Capture, view.NoticeText, view.DdagrabWorks, beklenen: Metin("recorder.capture.fallback-unavailable"));
        });

        Assert.Equal(RecorderCapture.Gdigrab, olcu.Capture);
        Assert.False(string.IsNullOrWhiteSpace(olcu.beklenen));
        Assert.Equal(olcu.beklenen, olcu.NoticeText);
        Assert.False(olcu.DdagrabWorks);
    }

    [Fact]
    public void YoklamaGecinceSatirYokDdagrabSecilir()
    {
        var olcu = Gorunumle(view =>
        {
            RecorderRequest? yoklanan = null;
            view.DdaOutputSource = () => IkiCikis;
            view.DdagrabCheck = r => { yoklanan = r; return Task.FromResult(true); };
            var secilen = view.ChooseCaptureAsync(Bolge(0, 0, 640, 480, Array.Empty<DdaOutput>())).GetAwaiter().GetResult();
            return (secilen: secilen.Capture, cikis: secilen.DdaOutputs.Count, satir: view.NoticeText, yoklanan: yoklanan?.Capture);
        });

        Assert.Equal(RecorderCapture.Ddagrab, olcu.secilen);
        Assert.Equal(2, olcu.cikis);
        Assert.Equal(string.Empty, olcu.satir);
        Assert.Equal(RecorderCapture.Ddagrab, olcu.yoklanan);
    }

    [Fact]
    public void YayilanBolgeYoklamadanDuserVeSebebiniSoyler()
    {
        var olcu = Gorunumle(view =>
        {
            var yoklama = 0;
            view.DdaOutputSource = () => IkiCikis;
            view.DdagrabCheck = _ => { yoklama++; return Task.FromResult(true); };
            var secilen = view.ChooseCaptureAsync(Bolge(2400, 0, 640, 480, Array.Empty<DdaOutput>())).GetAwaiter().GetResult();
            return (secilen.Capture, yoklama, view.NoticeText, beklenen: Metin("recorder.capture.fallback-layout"));
        });

        Assert.Equal(RecorderCapture.Gdigrab, olcu.Capture);
        Assert.Equal(0, olcu.yoklama);
        Assert.Equal(olcu.beklenen, olcu.NoticeText);
        Assert.NotEqual(Metin("recorder.capture.fallback-unavailable"), olcu.NoticeText);
    }

    [Fact]
    public void PencereHedefiIstemciAlaniylaYoklanir()
    {
        var olcu = Gorunumle(view =>
        {
            view.DdaOutputSource = () => IkiCikis;
            view.WindowRect = _ => new PixelRect(2700, 40, 801, 601);
            view.DdagrabCheck = _ => Task.FromResult(true);
            var istek = new RecorderRequest { Platform = RecorderPlatform.Windows, Target = RecorderTargetKind.Window, WindowTitle = "Deneme" };
            var secilen = view.ChooseCaptureAsync(istek).GetAwaiter().GetResult();
            return (secilen.Capture, kaynak: Kaynak(RecorderArguments.Build(secilen, @"C:\kayit\a.mp4")));
        });

        Assert.Equal(RecorderCapture.Ddagrab, olcu.Capture);
        Assert.Contains("output_idx=1:", olcu.kaynak);
        Assert.Contains(":offset_x=140:offset_y=40:video_size=800x600,", olcu.kaynak);
    }

    [Fact]
    public void YoklamaGecinceOtomatikKipTavaniKalkar()
    {
        var olcu = Gorunumle(view =>
        {
            var once = view.Machine().MaxCaptureFps;
            view.DdagrabWorks = true;
            var gecen = view.Machine().MaxCaptureFps;
            view.DdagrabWorks = false;
            return (once, gecen, dusen: view.Machine().MaxCaptureFps);
        });

        var tavan = OperatingSystem.IsWindows() ? RecorderAutoPlan.GdigrabMaxFps : 0;
        Assert.Equal(tavan, olcu.once);
        Assert.Equal(0, olcu.gecen);
        Assert.Equal(tavan, olcu.dusen);
    }

    [Theory]
    [InlineData("recorder.capture.fallback-unavailable")]
    [InlineData("recorder.capture.fallback-layout")]
    public void DususMetniButunDillerde(string anahtar)
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var dil in Locales.Languages)
        {
            var degerler = Locales.Domain(dil, "recorder");
            Assert.True(degerler.TryGetValue(anahtar, out var deger) && !string.IsNullOrWhiteSpace(deger), $"{dil} dilinde {anahtar} yok.");
        }
    }

    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "kaydedici-ddagrab");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    /// <summary>
    /// 3 sn'lik gercek <c>ddagrab</c> kaydi, 640x480, 60 fps: oturum ayni argumanla kosar, ffprobe
    /// sayilan paketi ve sureyi okur. <c>gdigrab</c> 60 istenince bu makinede 41 veriyordu
    /// (<c>docs/olcumler/kaydedici-gdigrab-kare-tavani.md</c>); esik 50.
    /// </summary>
    [DdagrabFact]
    public async Task UcSaniyelikDdagrabKaydi60KareOkunur()
    {
        var cikti = Path.Combine(Kanit, "ddagrab-3sn.mp4");
        var istek = DdagrabFactAttribute.Istek.Value!;

        var oturum = await RecorderSession.StartAsync(istek, cikti);
        await Task.Delay(3000);
        var sonuc = await oturum.StopAsync();

        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffprobe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in new[] { "-v", "error", "-count_packets", "-select_streams", "v:0",
                     "-show_entries", "stream=nb_read_packets,width,height", "-show_entries", "format=duration", "-of", "default=nw=1", cikti })
            psi.ArgumentList.Add(a);
        using var probe = Process.Start(psi)!;
        var metin = probe.StandardOutput.ReadToEnd() + probe.StandardError.ReadToEnd();
        probe.WaitForExit(20_000);
        File.WriteAllText(Path.Combine(Kanit, "ddagrab-3sn.ffprobe.txt"), metin);

        string Deger(string ad) => metin.Split('\n').Select(s => s.Trim()).First(s => s.StartsWith(ad + "=", StringComparison.Ordinal))[(ad.Length + 1)..];
        var kare = int.Parse(Deger("nb_read_packets"), CultureInfo.InvariantCulture);
        var sure = double.Parse(Deger("duration"), CultureInfo.InvariantCulture);

        Assert.True(sonuc.Ok, $"kayit 0 ile kapanmali; stderr: {sonuc.StandardError}");
        Assert.Equal("640", Deger("width"));
        Assert.Equal("480", Deger("height"));
        Assert.InRange(sure, 2.0, 5.0);
        Assert.True(kare / sure >= 50, $"ddagrab 60 istenince {kare} kare / {sure:0.00} sn verdi");

        KanitKapanisi.Kapat(Kanit, "ddagrab-3sn.mp4", "ddagrab-3sn.ffprobe.txt");
    }
}
