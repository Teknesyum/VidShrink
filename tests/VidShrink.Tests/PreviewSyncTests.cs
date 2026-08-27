using System.Diagnostics;
using System.Globalization;
using VidShrink.App;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Core.Playback;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// Kaynak: her karesinin parlakligi kendi kare numarasini tasiyan bir rampa. Birlesik
/// karenin bir yarisinin ortalama parlakligi okunup kalibrasyon tablosunda aranince o
/// yarinin <b>hangi kaynak karesini</b> gosterdigi cikar; iki yarinin farki da kaymadir.
/// Olcum boyle sayiya doner, goze degil.
/// </summary>
public sealed class SyncRamp : IDisposable
{
    /// <summary>Rampa 4 sn ve 30 fps: 120 kare, kare basina 2 birim parlaklik.</summary>
    public const int SourceFps = 30;

    public const double SourceDurationSeconds = 4.0;

    /// <summary>Olcum karesinin kenari. Kucuk: sayilan sey duz bir alanin ortalamasi.</summary>
    public const int Edge = 16;

    public string Directory { get; }
    public bool Ready { get; }
    public string Kaynak => Path.Combine(Directory, "rampa.mp4");

    /// <summary>Kalibrasyon: kaynagin her karesinin ortalama parlakligi.</summary>
    public IReadOnlyList<double> Calibration { get; } = Array.Empty<double>();

    public SyncRamp()
    {
        Directory = Path.Combine(Path.GetTempPath(), "vidshrink-t58-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(Directory);
        if (!ToolLocator.IsAvailable(out _)) return;

        var duration = SourceDurationSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        var built = Run(ToolLocator.Ffmpeg, new[]
        {
            "-y", "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", $"color=c=black:s=320x180:r={SourceFps}:d={duration}",
            "-vf", "geq=lum='N*2':cb=128:cr=128",
            "-c:v", "libx264", "-preset", "veryfast", "-crf", "10", "-pix_fmt", "yuv420p",
            Kaynak
        }, out _) == 0;

        if (!built) return;

        var raw = Raw(new[]
        {
            "-hide_banner", "-loglevel", "error", "-i", Kaynak,
            "-vf", $"format=gray,scale={Edge}:{Edge}",
            "-f", "rawvideo", "-pix_fmt", "gray", "-"
        });

        Calibration = Means(raw, 0, Edge, Edge);
        Ready = Calibration.Count >= (int)(SourceDurationSeconds * SourceFps) - 2;
    }

    /// <summary>Verilen ortalama parlakligin karsiligi olan kaynak kare numarasi.</summary>
    public int FrameFor(double mean)
    {
        var best = double.MaxValue;
        var index = -1;
        for (var i = 0; i < Calibration.Count; i++)
        {
            var error = Math.Abs(Calibration[i] - mean);
            if (error >= best) continue;
            best = error;
            index = i;
        }
        return index;
    }

    /// <summary>Ham gri kareleri ortalamaya cevirir. <paramref name="offset"/> sol/sag yarilar icin.</summary>
    public static List<double> Means(byte[] raw, int offset, int width, int stride)
    {
        var frame = stride * Edge;
        var result = new List<double>(raw.Length / Math.Max(1, frame));
        for (var k = 0; k + frame <= raw.Length; k += frame)
        {
            long total = 0;
            for (var y = 0; y < Edge; y++)
            {
                var row = k + y * stride + offset;
                for (var x = 0; x < width; x++) total += raw[row + x];
            }
            result.Add((double)total / (width * Edge));
        }
        return result;
    }

    public static ProcessStartInfo Info(string tool, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = tool,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        return psi;
    }

    public static int Run(string tool, IEnumerable<string> args, out string standardOutput)
    {
        using var process = new Process { StartInfo = Info(tool, args) };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        stdout.Wait();
        stderr.Wait();
        process.WaitForExit();
        standardOutput = stdout.Result;
        return process.ExitCode;
    }

    /// <summary>Borudan ham kare okur; cikti ikili oldugu icin metin olarak okunamaz.</summary>
    public static byte[] Raw(IEnumerable<string> args)
    {
        using var process = new Process { StartInfo = Info(ToolLocator.Ffmpeg, args) };
        process.Start();
        using var buffer = new MemoryStream();
        var stderr = process.StandardError.ReadToEndAsync();
        process.StandardOutput.BaseStream.CopyTo(buffer);
        stderr.Wait();
        process.WaitForExit();
        return buffer.ToArray();
    }

    public void Dispose()
    {
        try { System.IO.Directory.Delete(Directory, recursive: true); } catch { }
    }
}

/// <summary>
/// Karsilastirma panelinin iki yarisinin ayni kaynak anini gosterip gostermedigi. Olcum
/// panelin kendi grafigiyle yapilir; kabul edilen kayma ve kaymanin <b>karakteri</b>
/// (sabit mi, buyuyor mu) burada tutuluyor.
/// </summary>
public sealed class PreviewSyncTests : IClassFixture<SyncRamp>
{
    private readonly SyncRamp _ramp;

    public PreviewSyncTests(SyncRamp ramp) => _ramp = ramp;

    /// <summary>
    /// Kabul edilen kayma. Fiziksel taban <c>1/plan.Fps</c>: sag yarinin elinde iki cikti
    /// karesi arasinda daha yakin bir kare yoktur ve panel o bosluk boyunca ayni kareyi
    /// tutar — 15 fps'lik planda <b>2 kaynak karesi</b>. Olculen sabit kayma ise 3,5 kaynak
    /// karesi; aradaki 1,5 kare <c>FfmpegArguments.Build</c>'in fps dususunu <c>-r</c> ile
    /// yapmasindan geliyor (ayni kodlama <c>-vf fps=</c> ile yapilinca medyan kayma 0).
    /// O dosya bu sozlesmenin <c>owns</c> listesinde degil, bu yuzden tavan bugunku
    /// kodlayicinin tutabildigi yerde: 4 kaynak karesi, 0,133 sn. <c>-r</c> degistigi gun
    /// tavan 2'ye iner ve <see cref="Fps_dusuren_cikti_kaynaktan_uzun"/> o gunu haber verir.
    ///
    /// Asil olcu buyukluk degil <b>karakter</b>: kayma sabit kalmali. Buyuyen kaymanin
    /// tavani yoktur ve kullanicinin gordugu de oydu.
    /// </summary>
    private const int AcceptedDriftFrames = 4;

    private const int PlanFps = 15;

    private static MediaInfo Source(string path) => new()
    {
        FilePath = path,
        FileSizeBytes = new FileInfo(path).Exists ? new FileInfo(path).Length : 1_000_000,
        DurationSeconds = SyncRamp.SourceDurationSeconds,
        Width = 320,
        Height = 180,
        Fps = SyncRamp.SourceFps,
        VideoCodec = "h264",
        TotalBitrateBps = 2_000_000
    };

    private static EncodePlan ReducedFpsPlan() => new()
    {
        Codec = "libx264",
        Mode = "crf",
        Crf = 26,
        VideoBitrateK = 300,
        Width = 160,
        Height = 90,
        Fps = PlanFps,
        Preset = "veryfast",
        PixelFormat = "yuv420p",
        AudioCodec = null
    };

    [Fact]
    public void Ayni_dosya_basa_sarar_ayri_dosya_sarmaz()
    {
        Assert.True(PanelHost.ShouldLoop("kaynak.mp4", "kaynak.mp4"));
        Assert.False(PanelHost.ShouldLoop("kaynak.mp4", "cikti.mp4"));
    }

    private sealed class SessizKaynak : IComparisonFrameSource
    {
        public event EventHandler<ComparisonSourceStatus>? StatusChanged;
        public ComparisonSourceStatus Status => new(ComparisonSourceState.Bosta, 0, 0, 0, 0, 0);
        public Task StartAsync(ComparisonFrameRequest request, CancellationToken ct = default)
        {
            StatusChanged?.Invoke(this, Status);
            return Task.CompletedTask;
        }
        public bool TryTake(out PlaybackFrame frame) { frame = null!; return false; }
        public void Return(PlaybackFrame frame) { }
        public void Play() { }
        public void Pause() { }
        public Task SeekAsync(TimeSpan position, CancellationToken ct = default) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;
        public void Dispose() { }
    }

    /// <summary>
    /// Panelin istegi: perde durumunda iki girdi ayni dosyadir ve basa sarilir; tam cikti
    /// gelince iki girdi ayri dosyadir ve sarilmaz.
    /// </summary>
    [FfmpegFact]
    public void Panel_tam_cikti_borusunu_basa_sarmaz()
    {
        var cikti = Path.Combine(_ramp.Directory, "istek.mp4");
        Assert.Equal(0, SyncRamp.Run(ToolLocator.Ffmpeg,
            FfmpegArguments.Build(Source(_ramp.Kaynak), ReducedFpsPlan(), cikti, 0, null), out _));

        var host = AppHost.Run(() => new PanelHost(new ComparisonPanel(), () => new SessizKaynak()));
        try
        {
            AppHost.Run(() => host.SetFiles(_ramp.Kaynak, null, 16.0 / 9, TimeSpan.FromSeconds(4), 30));
            var perde = AppHost.Run(() => host.BuildRequest(new Avalonia.PixelSize(320, 180)));
            Assert.Equal(perde.LeftPath, perde.RightPath);
            Assert.True(perde.Loop);

            AppHost.Run(() => host.SetFiles(_ramp.Kaynak, cikti, 16.0 / 9, TimeSpan.FromSeconds(4), 30));
            var tam = AppHost.Run(() => host.BuildRequest(new Avalonia.PixelSize(320, 180)));
            Assert.NotEqual(tam.LeftPath, tam.RightPath);
            Assert.False(tam.Loop);
        }
        finally
        {
            AppHost.Run(host.Dispose);
        }
    }

    /// <summary>
    /// K1/K2/K3'un olcumu. Iki yarinin gosterdigi kaynak karesi butun oynatma boyunca
    /// okunur; kaymanin ilk ve son bolumu karsilastirilir. Sabit kayma ikisinde de aynidir,
    /// buyuyen kayma degildir.
    ///
    /// Olcum iki kez kosar: bir kez panelin bugunku kararyla
    /// (<see cref="PanelHost.ShouldLoop"/>), bir kez de basa sarma zorla acikken. Ikincisi
    /// hatanin kendisini gosteriyor ve rapora ham haliyle giriyor.
    /// </summary>
    [FfmpegFact]
    public void Iki_yari_uzun_oynatmada_da_ayni_ani_gosterir()
    {
        Assert.True(_ramp.Ready);
        var info = Source(_ramp.Kaynak);
        var cikti = Path.Combine(_ramp.Directory, "cikti.mp4");
        Assert.Equal(0, SyncRamp.Run(ToolLocator.Ffmpeg,
            FfmpegArguments.Build(info, ReducedFpsPlan(), cikti, 0, null), out _));

        var panelFps = SyncRamp.SourceFps;
        var pencere = (int)(SyncRamp.SourceDurationSeconds * panelFps);

        // Bes tur: buyuyen kayma ancak tekrar edince gorunur.
        var sarmali = Drift(_ramp.Kaynak, cikti, panelFps, pencere * 5, loop: true);
        var sarmaliIlk = MedianBetween(sarmali, panelFps, pencere - 5);
        var sarmaliSon = MedianBetween(sarmali, pencere * 4 + panelFps, pencere * 5 - 5);

        // Panelin kendi karari. Ayni bes turluk kare sayisi istenir: basa sarma kapaliysa
        // boru tek turdan sonra biter ve kuyruk ilk turun icinde kalir; acilirsa kuyruk
        // besinci tura kayar ve buyume oradan gorunur.
        var sarmasiz = Drift(_ramp.Kaynak, cikti, panelFps, pencere * 5,
            loop: PanelHost.ShouldLoop(_ramp.Kaynak, cikti));
        var ilk = MedianBetween(sarmasiz, panelFps, pencere / 2);
        var son = MedianOfLast(sarmasiz, panelFps);

        Record($"K1 basa-sarma-acik   kare={sarmali.Count} ilk-tur-medyan={sarmaliIlk:0.0} " +
               $"son-tur-medyan={sarmaliSon:0.0} buyume={Math.Abs(sarmaliSon - sarmaliIlk):0.0} kaynak karesi");
        Record($"K1 basa-sarma-kapali kare={sarmasiz.Count} bas-medyan={ilk:0.0} " +
               $"son-medyan={son:0.0} buyume={Math.Abs(son - ilk):0.0} kaynak karesi");

        // Hata gercekten var: basa sararken kayma turdan ture buyuyor.
        Assert.True(Math.Abs(sarmaliSon - sarmaliIlk) > AcceptedDriftFrames,
            $"basa sarmada buyume beklenirdi: ilk={sarmaliIlk:0.0} son={sarmaliSon:0.0}");

        // Duzeltmeden sonra kayma sabit ve tavanin altinda.
        Assert.True(Math.Abs(son - ilk) <= 1, $"kayma buyuyor: bas={ilk:0.0} son={son:0.0}");
        Assert.True(Math.Abs(ilk) <= AcceptedDriftFrames && Math.Abs(son) <= AcceptedDriftFrames,
            $"kayma tavani asiyor: bas={ilk:0.0} son={son:0.0} tavan={AcceptedDriftFrames}");
    }

    /// <summary>
    /// Kaymanin sebebi: <c>-r</c> ile fps dusuren kodlama kaynaktan <b>uzun</b> bir dosya
    /// uretiyor ve fark her basa sarmada birikiyor. Sure farkinin sifir olmadigini olcu
    /// olarak tutuyoruz — sifirlandigi gun bu test duser ve panel yeniden basa sarabilir.
    /// </summary>
    [FfmpegFact]
    public void Fps_dusuren_cikti_kaynaktan_uzun()
    {
        Assert.True(_ramp.Ready);
        var cikti = Path.Combine(_ramp.Directory, "uzunluk.mp4");
        Assert.Equal(0, SyncRamp.Run(ToolLocator.Ffmpeg,
            FfmpegArguments.Build(Source(_ramp.Kaynak), ReducedFpsPlan(), cikti, 0, null), out _));

        var kaynakSure = DurationOf(_ramp.Kaynak);
        var ciktiSure = DurationOf(cikti);

        Record($"K2 sure kaynak={kaynakSure:0.000} cikti={ciktiSure:0.000} fark={ciktiSure - kaynakSure:0.000} sn");
        Assert.True(ciktiSure - kaynakSure > 0.05,
            $"sure farki beklenirdi: kaynak={kaynakSure:0.000} cikti={ciktiSure:0.000}");
    }

    private static double DurationOf(string path)
    {
        SyncRamp.Run(ToolLocator.Ffprobe, new[]
        {
            "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=duration",
            "-of", "default=nw=1:nk=1", path
        }, out var text);
        return double.Parse(text.Trim(), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Panelin kendi grafigi kosturulur; her cikti karesi icin sag ve sol yarinin gosterdigi
    /// kaynak karesi ve ikisinin farki.
    /// </summary>
    private List<(int Frame, int Drift)> Drift(string left, string right, int panelFps, int frames, bool loop)
    {
        var args = new List<string> { "-hide_banner", "-loglevel", "error" };
        if (loop) args.AddRange(new[] { "-stream_loop", "-1" });
        args.AddRange(new[] { "-i", left });
        if (loop) args.AddRange(new[] { "-stream_loop", "-1" });
        args.AddRange(new[] { "-i", right });
        args.AddRange(new[]
        {
            "-filter_complex",
            $"[0:v]fps={panelFps},scale={SyncRamp.Edge}:{SyncRamp.Edge}[l];" +
            $"[1:v]fps={panelFps},scale={SyncRamp.Edge}:{SyncRamp.Edge}[r];[l][r]hstack=inputs=2[v]",
            "-map", "[v]", "-frames:v", frames.ToString(CultureInfo.InvariantCulture),
            "-f", "rawvideo", "-pix_fmt", "gray", "-"
        });

        var raw = SyncRamp.Raw(args);
        var stride = SyncRamp.Edge * 2;
        var sol = SyncRamp.Means(raw, 0, SyncRamp.Edge, stride);
        var sag = SyncRamp.Means(raw, SyncRamp.Edge, SyncRamp.Edge, stride);

        var drift = new List<(int, int)>(sol.Count);
        for (var i = 0; i < sol.Count && i < sag.Count; i++)
        {
            var l = _ramp.FrameFor(sol[i]);
            var r = _ramp.FrameFor(sag[i]);
            // Basa sarma sinirindaki kare: sol yari sifira dondu, sag yari donmedi. O tek
            // karenin farki oynatmanin kaymasi degil, sinirin kendisi.
            if (Math.Abs(r - l) > _ramp.Calibration.Count / 2) continue;
            drift.Add((i, r - l));
        }
        return drift;
    }

    /// <summary>Oynatmanin son <paramref name="count"/> karesinin medyani.</summary>
    private static double MedianOfLast(List<(int Frame, int Drift)> values, int count)
        => Median(values.Skip(Math.Max(0, values.Count - count)).Select(v => v.Drift).ToList());

    private static double MedianBetween(List<(int Frame, int Drift)> values, int from, int to)
    {
        var window = values.Where(v => v.Frame >= from && v.Frame < to).Select(v => v.Drift).ToList();
        Assert.NotEmpty(window);
        return Median(window);
    }

    private static double Median(List<int> values)
    {
        Assert.NotEmpty(values);
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    private static void Record(string line)
    {
        try
        {
            var dir = Path.Combine(TipSources.Root, ".calisma", "t58");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "olcum.txt"), line + Environment.NewLine);
        }
        catch { }
    }
}
