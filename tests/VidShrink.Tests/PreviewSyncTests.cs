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
    /// Kabul edilen kayma, kare izgarasindan turetildi: <c>1/plan.Fps</c>. Plan fps'i
    /// dusurdugunde sag yarinin elinde iki cikti karesi arasinda daha yakin bir kare yoktur
    /// ve panel o bosluk boyunca ayni kareyi tutar — 15 fps'lik planda <b>2 kaynak
    /// karesi</b>. Tavan bu; olculen sayiya gore ayarlanmadi (bugun olculen 0 ile 0,5 kare
    /// arasinda, yani tavanin epey altinda).
    ///
    /// <b>Bu sayi neyi koruyor:</b> iki yarinin ayni kaynak anini gostermesini.
    /// <b>Bozulursa kullanici ne gorur:</b> hareketli sahnede iki yari arasindaki dikis
    /// kopuk gorunur — sag yari solun gosterdigi anin gerisinde kalir.
    ///
    /// Asil olcu yine de buyukluk degil <b>karakter</b>: kayma sabit kalmali. Buyuyen
    /// kaymanin tavani yoktur ve kullanicinin gordugu de oydu.
    /// </summary>
    private const int AcceptedDriftFrames = 2;

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
    /// Iki yarinin gosterdigi kaynak karesi butun oynatma boyunca okunur; kaymanin basi ile
    /// sonu karsilastirilir. Aranan iki sey var: kayma <b>tavanin altinda</b> ve
    /// <b>sabit</b> — yani izleme uzadikca buyumuyor.
    ///
    /// <b>Ne koruyor:</b> panelin iki yarisinin ayni ani gostermesini, oynatmanin
    /// besinci turunda da. <b>Bozulursa kullanici ne gorur:</b> onizleme ile orijinal
    /// birbirinden ayrilir; sabit bir sapmada dikis hep kopuk durur, buyuyen sapmada
    /// izledikce daha da acilir.
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

        // Bes turluk kare istenir. Panel basa sarmadigi icin boru tek turdan sonra biter ve
        // kuyruk ilk turun icinde kalir; karar basa sarmaya donerse kuyruk besinci tura
        // kayar ve orada birikmis kayma gorunur.
        var olcum = Drift(_ramp.Kaynak, cikti, panelFps, pencere * 5,
            loop: PanelHost.ShouldLoop(_ramp.Kaynak, cikti));
        var bas = MedianBetween(olcum, panelFps, pencere / 2);
        var son = MedianOfLast(olcum, panelFps);

        Record($"K1 panel-karari kare={olcum.Count} bas-medyan={bas:0.0} son-medyan={son:0.0} " +
               $"buyume={Math.Abs(son - bas):0.0} kaynak karesi (tavan={AcceptedDriftFrames})");

        Assert.True(Math.Abs(son - bas) <= 1, $"kayma buyuyor: bas={bas:0.0} son={son:0.0}");
        Assert.True(Math.Abs(bas) <= AcceptedDriftFrames && Math.Abs(son) <= AcceptedDriftFrames,
            $"kayma tavani asiyor: bas={bas:0.0} son={son:0.0} tavan={AcceptedDriftFrames}");
    }

    /// <summary>
    /// <see cref="PanelHost.ShouldLoop"/>'un korudugu tehlike, olculerek. Sag girdi kaynaktan
    /// kisa oldugunda basa sarma kaymayi tur basina buyutuyor; panel de tam bu yuzden ayri
    /// dosyalari sarmiyor.
    ///
    /// Kisa dosya burada <b>bilerek</b> uretiliyor. Onceden bu farki kodlayicinin kendi
    /// kusuru (fps dususunun <c>-r</c> ile yapilmasi) yaratiyordu; T60 onu kapatti ama
    /// tehlike kapanmadi: kirpma, kare izgarasina oturmayan sure, degisken kare hizi ayni
    /// esitsizligi uretebilir.
    ///
    /// <b>Ne koruyor:</b> esit olmayan iki girdinin ayri ayri basa sarmamasini.
    /// <b>Bozulursa kullanici ne gorur:</b> uzun izlemede iki yari giderek acilir.
    /// </summary>
    [FfmpegFact]
    public void Esit_olmayan_girdiler_basa_sarilirsa_kayma_buyur()
    {
        Assert.True(_ramp.Ready);
        var kisa = Path.Combine(_ramp.Directory, "kisa.mp4");
        var kirpik = (SyncRamp.SourceDurationSeconds - 0.2).ToString("0.###", CultureInfo.InvariantCulture);
        Assert.Equal(0, SyncRamp.Run(ToolLocator.Ffmpeg, new[]
        {
            "-y", "-hide_banner", "-loglevel", "error", "-i", _ramp.Kaynak, "-t", kirpik,
            "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18", "-pix_fmt", "yuv420p",
            "-an", kisa
        }, out _));

        var panelFps = SyncRamp.SourceFps;
        var pencere = (int)(SyncRamp.SourceDurationSeconds * panelFps);

        var sarmali = Drift(_ramp.Kaynak, kisa, panelFps, pencere * 5, loop: true);
        var ilkTur = MedianBetween(sarmali, panelFps, pencere - 5);
        var sonTur = MedianBetween(sarmali, pencere * 4 + panelFps, pencere * 5 - 5);

        Record($"K3 esitsiz-girdi kaynak={DurationOf(_ramp.Kaynak):0.000} kisa={DurationOf(kisa):0.000} " +
               $"ilk-tur-medyan={ilkTur:0.0} son-tur-medyan={sonTur:0.0} " +
               $"buyume={Math.Abs(sonTur - ilkTur):0.0} kaynak karesi");

        Assert.False(PanelHost.ShouldLoop(_ramp.Kaynak, kisa));
        Assert.True(Math.Abs(sonTur - ilkTur) > AcceptedDriftFrames,
            $"esitsiz girdide buyume beklenirdi: ilk={ilkTur:0.0} son={sonTur:0.0}");
    }

    /// <summary>
    /// Fps dusuren kodlama kaynakla <b>ayni</b> sureyi tutmali. Bu olcu once kusuru
    /// belgeliyordu — <c>-r</c> ile dusurulen kare hizi cikti sureyi uzatiyordu, 4 sn'lik
    /// kaynak 15 fps'te 62 kare / 4,133 sn cikiyordu. T60 fps dususunu <c>fps=</c> filtresine
    /// tasidi ve fark sifirlandi; olcu de yonunu cevirdi: artik o kusurun geri gelmesini
    /// yakaliyor.
    ///
    /// Tolerans kare izgarasindan: bir cikti karesi, <c>1/plan.Fps</c>. Kare hizi degisimi
    /// son karenin bitisini bir kareden az oynatabilir, daha fazlasini oynatamaz. Olculen
    /// sayiya uydurulmadi — bugun fark tam 0, bilinen gerileme ise 0,133 sn, yani iki cikti
    /// karesi ve toleransin rahatca disinda.
    ///
    /// <b>Ne koruyor:</b> cikti ile kaynagin ayni uzunlukta kalmasini.
    /// <b>Bozulursa kullanici ne gorur:</b> teslim edilen dosya kaynaktan uzun olur ve
    /// karsilastirma panelinde iki yari her basa sarmada biraz daha acilir.
    /// </summary>
    [FfmpegFact]
    public void Fps_dusuren_cikti_kaynakla_ayni_sureyi_tutar()
    {
        Assert.True(_ramp.Ready);
        var cikti = Path.Combine(_ramp.Directory, "uzunluk.mp4");
        Assert.Equal(0, SyncRamp.Run(ToolLocator.Ffmpeg,
            FfmpegArguments.Build(Source(_ramp.Kaynak), ReducedFpsPlan(), cikti, 0, null), out _));

        var kaynakSure = DurationOf(_ramp.Kaynak);
        var ciktiSure = DurationOf(cikti);
        var tolerans = 1.0 / PlanFps;

        Record($"K2 sure kaynak={kaynakSure:0.000} cikti={ciktiSure:0.000} " +
               $"fark={ciktiSure - kaynakSure:0.000} sn (tolerans={tolerans:0.000})");
        Assert.True(Math.Abs(ciktiSure - kaynakSure) < tolerans,
            $"cikti kaynakla ayni sureyi tutmali: kaynak={kaynakSure:0.000} cikti={ciktiSure:0.000} " +
            $"tolerans={tolerans:0.000}");
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
