using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Media.Imaging;
using VidShrink.App.Playback;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal static class MotorKanit
{
    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "oynatici-motor");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static void Write(string name, string body)
        => File.WriteAllText(Path.Combine(Folder, name), body, new UTF8Encoding(false));

    internal static string Ms(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);

    internal static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return double.NaN;
        var sorted = values.OrderBy(v => v).ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    internal static double ReadDouble(MpvEngine engine, string name)
        => double.TryParse(engine.GetProperty(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : double.NaN;
}

internal static class MotorKlipleri
{
    private const int UretimTavaniMs = 600_000;

    internal static string Kucuk => Hazirla("kucuk-320x180-25sn.mp4",
        "-f", "lavfi", "-i", "testsrc2=size=320x180:rate=30:duration=25",
        "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=25",
        "-c:v", "libx264", "-preset", "ultrafast", "-g", "60", "-keyint_min", "60", "-sc_threshold", "0",
        "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "96k", "-ac", "1", "-shortest");

    internal static string H264_1080p60 => Hazirla("h264_1080p60.mp4",
        "-f", "lavfi", "-i", "testsrc2=size=1920x1080:rate=60:duration=35",
        "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=35",
        "-c:v", "libx264", "-preset", "fast", "-crf", "20", "-g", "120", "-keyint_min", "120", "-sc_threshold", "0",
        "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "128k", "-ac", "1", "-shortest");

    internal static string H264_2160p30 => Hazirla("h264_2160p30.mp4",
        "-f", "lavfi", "-i", "testsrc2=size=3840x2160:rate=30:duration=35",
        "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=35",
        "-c:v", "libx264", "-preset", "fast", "-crf", "20", "-g", "60", "-keyint_min", "60", "-sc_threshold", "0",
        "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "128k", "-ac", "1", "-shortest");

    internal static string Hevc_1080p60 => Hazirla("hevc_1080p60.mp4",
        "-f", "lavfi", "-i", "testsrc2=size=1920x1080:rate=60:duration=35",
        "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=35",
        "-c:v", "libx265", "-preset", "fast", "-crf", "22",
        "-x265-params", "keyint=120:min-keyint=120:scenecut=0:log-level=error", "-tag:v", "hvc1",
        "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "128k", "-ac", "1", "-shortest");

    private static string Hazirla(string name, params string[] args)
    {
        var path = Path.Combine(MotorKanit.Folder, name);
        if (File.Exists(path) && new FileInfo(path).Length > 0) return path;

        Assert.True(ToolLocator.IsAvailable(out var missing), $"klip uretimi icin {missing} gerekli; bu test ffmpeg olmadan kirmizi kalir");

        var partial = path + ".part.mp4";
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-hide_banner");
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        psi.ArgumentList.Add(partial);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(UretimTavaniMs))
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException($"klip uretimi {UretimTavaniMs} ms icinde bitmedi: {name}");
        }

        stdout.GetAwaiter().GetResult();
        var log = stderr.GetAwaiter().GetResult();
        Assert.True(process.ExitCode == 0, $"ffmpeg {name} uretemedi (kod {process.ExitCode}): {log[Math.Max(0, log.Length - 600)..]}");
        File.Move(partial, path, true);
        return path;
    }
}

internal static class SistemYuku
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    internal static (long Idle, long Total) Anlik()
    {
        if (!OperatingSystem.IsWindows() || !GetSystemTimes(out var idle, out var kernel, out var user)) return (0, 0);
        return (idle, kernel + user);
    }

    internal static double Mesgul((long Idle, long Total) start, (long Idle, long Total) end)
    {
        var total = end.Total - start.Total;
        return total <= 0 ? double.NaN : 100.0 * (1 - (double)(end.Idle - start.Idle) / total);
    }
}

internal sealed record AramaOlcumu(
    string Klip,
    double Sure,
    IReadOnlyList<(double Hedef, SeekResult Sonuc)> Aramalar,
    double MesgulYuzde,
    double IsinmaMs)
{
    public IReadOnlyList<double> Gosterilen => Aramalar.Where(a => a.Sonuc.Outcome == SeekOutcome.Shown).Select(a => a.Sonuc.LatencyMs).ToList();

    public double Medyan => MotorKanit.Median(Gosterilen);

    public string Rapor(string baslik)
    {
        var body = new StringBuilder();
        body.AppendLine(baslik);
        body.AppendLine($"klip: {Klip} sure {Sure.ToString("0.###", CultureInfo.InvariantCulture)} sn");
        body.AppendLine("yontem: 2 sn isinma oynatma, sonra oynarken 20 arama, hedef 1.0 + rng*(sure-4.0), Random(1), aramalar arasi 400 ms, ao=null, hwdec=no, absolute+exact");
        body.AppendLine("gecikme: seek komutu -> MPV_EVENT_SEEK sonrasi renderi baslayan ilk yeni karenin render bitisi");
        body.AppendLine($"sistem mesgul % (arama penceresi, GetSystemTimes): {MotorKanit.Ms(MesgulYuzde)}");
        body.AppendLine($"gosterilen {Gosterilen.Count}/{Aramalar.Count}, medyan {MotorKanit.Ms(Medyan)} ms, min {MotorKanit.Ms(Gosterilen.DefaultIfEmpty(double.NaN).Min())}, max {MotorKanit.Ms(Gosterilen.DefaultIfEmpty(double.NaN).Max())}");
        foreach (var (hedef, sonuc) in Aramalar)
            body.AppendLine($"  {hedef.ToString("0.000", CultureInfo.InvariantCulture)} sn -> {sonuc.Outcome} {MotorKanit.Ms(sonuc.LatencyMs)} ms");
        return body.ToString();
    }

    public static async Task<AramaOlcumu> OlcAsync(string path)
    {
        using var engine = new MpvEngine();
        engine.SetProperty("ao", "null");
        await engine.OpenAsync(path);
        var duration = engine.DurationSeconds;
        Assert.True(duration > 10, $"sure okunamadi: {duration}");

        var isinma = Stopwatch.StartNew();
        engine.Play();
        await Task.Delay(2000);
        isinma.Stop();

        var rng = new Random(1);
        var aramalar = new List<(double, SeekResult)>();
        var start = SistemYuku.Anlik();
        for (var i = 0; i < 20; i++)
        {
            var target = 1.0 + rng.NextDouble() * (duration - 4.0);
            var result = await engine.SeekAsync(target, SeekPrecision.Exact);
            aramalar.Add((target, result));
            await Task.Delay(400);
        }

        var busy = SistemYuku.Mesgul(start, SistemYuku.Anlik());
        return new AramaOlcumu(Path.GetFileName(path), duration, aramalar, busy, isinma.Elapsed.TotalMilliseconds);
    }
}

public sealed class OynaticiMotorTests
{
    private static async Task<(int Width, int Height, int Stride, byte[] Pixels)> KareBekleAsync(MpvEngine engine, long seen, TimeSpan limit)
    {
        var saat = Stopwatch.StartNew();
        (int, int, int, byte[])? frame = null;
        while (frame is null && saat.Elapsed < limit)
        {
            engine.TryCopyLatest(ref seen, (pixels, width, height, stride) =>
            {
                var bytes = new byte[stride * height];
                Marshal.Copy(pixels, bytes, 0, bytes.Length);
                frame = (width, height, stride, bytes);
            });
            if (frame is null) await Task.Delay(10);
        }

        Assert.True(frame is not null, $"{limit.TotalSeconds} sn icinde kare gelmedi; son log: {string.Join(" | ", engine.RecentLog)}");
        return frame!.Value;
    }

    [Fact]
    public async Task BassizOrtamdaKareCozulur()
    {
        var clip = MotorKlipleri.Kucuk;
        using var engine = new MpvEngine();
        engine.SetProperty("ao", "null");
        await engine.OpenAsync(clip);

        var (width, height, stride, pixels) = await KareBekleAsync(engine, 0, TimeSpan.FromSeconds(10));
        var distinct = new HashSet<int>();
        for (var i = 0; i + 3 < pixels.Length; i += 4)
            distinct.Add(pixels[i] | pixels[i + 1] << 8 | pixels[i + 2] << 16);

        MotorKanit.Write("k1-bassiz-kare.txt",
            $"motor: {engine.Name}, libmpv: {LibMpvLocator.LoadedFrom}{Environment.NewLine}"
            + $"klip: {Path.GetFileName(clip)}, sure {engine.DurationSeconds.ToString("0.###", CultureInfo.InvariantCulture)} sn, ses {engine.HasAudio}{Environment.NewLine}"
            + $"kare: {width}x{height}, stride {stride}, farkli renk {distinct.Count}, render edilen {engine.FramesRendered}{Environment.NewLine}");

        Assert.Equal(320, width);
        Assert.Equal(180, height);
        Assert.Equal(4 * width, stride);
        Assert.True(distinct.Count > 16, $"kare tek renk gibi: {distinct.Count} farkli renk");
        Assert.True(engine.HasAudio);
    }

    [Fact]
    public async Task BozukDosyaAcilistaHataVerir()
    {
        var bozuk = Path.Combine(MotorKanit.Folder, "bozuk-4-bayt.mp4");
        File.WriteAllBytes(bozuk, new byte[] { 0, 1, 2, 3 });

        using var engine = new MpvEngine();
        var saat = Stopwatch.StartNew();
        var hata = await Assert.ThrowsAsync<PlaybackOpenException>(() => engine.OpenAsync(bozuk));
        saat.Stop();

        MotorKanit.Write("k2-bozuk-dosya.txt", $"{saat.ElapsedMilliseconds} ms: {hata.Message}{Environment.NewLine}");
        Assert.True(saat.Elapsed < TimeSpan.FromSeconds(10), $"bozuk dosya {saat.ElapsedMilliseconds} ms surdu");
        Assert.False(engine.IsOpen);
    }

    [Fact]
    public async Task TamAramaHedefeKareAramasiAnahtarKareyeIner()
    {
        var clip = MotorKlipleri.Kucuk;
        using var engine = new MpvEngine();
        engine.SetProperty("ao", "null");
        await engine.OpenAsync(clip);
        var first = await KareBekleAsync(engine, 0, TimeSpan.FromSeconds(10));

        var exact = await engine.SeekAsync(5.5, SeekPrecision.Exact);
        var exactPos = MotorKanit.ReadDouble(engine, "time-pos");
        long seen = 0;
        var afterExact = await KareBekleAsync(engine, seen, TimeSpan.FromSeconds(5));

        var keyframe = await engine.SeekAsync(5.5, SeekPrecision.Keyframe);
        var keyPos = MotorKanit.ReadDouble(engine, "time-pos");

        var ayni = first.Pixels.AsSpan().SequenceEqual(afterExact.Pixels);
        MotorKanit.Write("k3-arama-inisi.txt",
            $"klip: {Path.GetFileName(clip)} (GOP 2 sn, 30 kare/sn, duraklatilmis){Environment.NewLine}"
            + $"exact 5.5 -> {exact.Outcome} {MotorKanit.Ms(exact.LatencyMs)} ms, time-pos {exactPos.ToString("0.000", CultureInfo.InvariantCulture)}{Environment.NewLine}"
            + $"keyframes 5.5 -> {keyframe.Outcome} {MotorKanit.Ms(keyframe.LatencyMs)} ms, time-pos {keyPos.ToString("0.000", CultureInfo.InvariantCulture)}{Environment.NewLine}"
            + $"acilis karesi ile arama sonrasi kare ayni mi: {ayni}{Environment.NewLine}");

        Assert.Equal(SeekOutcome.Shown, exact.Outcome);
        Assert.Equal(SeekOutcome.Shown, keyframe.Outcome);
        Assert.True(Math.Abs(exactPos - 5.5) <= 1.0 / 30 + 0.001, $"exact arama 5.5'e inmedi: {exactPos}");
        Assert.True(Math.Abs(keyPos - 5.5) >= 0.4, $"keyframes arama anahtar kareye inmedi: {keyPos}");
        Assert.False(ayni, "arama bitti denildi ama gosterilen kare acilis karesiyle ayni");
    }

    [Fact]
    public void OynaticiGorunumuMotordanKareAlir()
    {
        var clip = MotorKlipleri.Kucuk;
        var rapor = AppHost.Run(() =>
        {
            var view = new PlayerView();
            var open = view.OpenAsync(clip);
            var saat = Stopwatch.StartNew();
            while (!open.IsCompleted && saat.Elapsed < TimeSpan.FromSeconds(20))
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Thread.Sleep(10);
            }

            Assert.True(open.IsCompleted, "PlayerView.OpenAsync 20 sn icinde bitmedi");
            open.GetAwaiter().GetResult();

            var drawn = false;
            saat.Restart();
            while (!drawn && saat.Elapsed < TimeSpan.FromSeconds(10))
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                drawn = view.RenderLatest();
                if (!drawn) Thread.Sleep(10);
            }

            var bitmap = view.Frame.Source as WriteableBitmap;
            var body = $"motor: {view.Engine?.Name}, cizildi {drawn} ({saat.ElapsedMilliseconds} ms), kaynak {view.Frame.Source?.GetType().Name}, boyut {bitmap?.PixelSize}, oynuyor {view.IsPlaying}{Environment.NewLine}";

            Assert.True(drawn, "PlayerView 10 sn icinde kare cizmedi");
            Assert.NotNull(bitmap);
            Assert.Equal(new PixelSize(320, 180), bitmap!.PixelSize);
            Assert.Equal("libmpv", view.Engine?.Name);
            Assert.True(view.IsPlaying);

            view.Close();
            Assert.Null(view.Engine);
            return body;
        });

        MotorKanit.Write("k4-oynatici-gorunumu.txt", rapor);
    }

    [Fact]
    public async Task SesVeGoruntuOnSaniyeSonraKirkMilisaniyeIcinde()
    {
        var clip = MotorKlipleri.Kucuk;
        using var engine = new MpvEngine();
        engine.SetProperty("volume", "0");
        await engine.OpenAsync(clip);
        Assert.True(engine.HasAudio, "klipte ses izi yok");

        engine.Play();
        await Task.Delay(10_000);
        var ao = engine.GetProperty("current-ao");
        var once = await OrnekleAsync(engine);

        engine.SetProperty("audio-delay", "0.2");
        await Task.Delay(1500);
        var gecikmeli = await OrnekleAsync(engine);
        engine.SetProperty("audio-delay", "0");

        var medyan = MotorKanit.Median(once);
        var kaydirilmis = MotorKanit.Median(gecikmeli);
        var kayma = kaydirilmis - medyan;

        MotorKanit.Write("k5-ses-goruntu.txt",
            $"klip: {Path.GetFileName(clip)}, ao {ao}, konum {engine.PositionSeconds.ToString("0.000", CultureInfo.InvariantCulture)} sn{Environment.NewLine}"
            + "olcu: time-pos - audio-pts, cagiran iş parcaciginda, 10 sn oynatmadan sonra 100 ornek, 20 ms arayla" + Environment.NewLine
            + $"ornek {once.Count}, medyan {MotorKanit.Ms(medyan * 1000)} ms, min {MotorKanit.Ms(once.Min() * 1000)}, max {MotorKanit.Ms(once.Max() * 1000)}{Environment.NewLine}"
            + $"negatif kontrol audio-delay=0.2: ornek {gecikmeli.Count}, medyan {MotorKanit.Ms(kaydirilmis * 1000)} ms, kayma {MotorKanit.Ms(kayma * 1000)} ms{Environment.NewLine}");

        Assert.True(once.Count >= 90, $"yalniz {once.Count} gecerli ornek");
        Assert.True(Math.Abs(medyan) <= 0.040, $"A/V farki {MotorKanit.Ms(medyan * 1000)} ms");
        Assert.InRange(Math.Abs(kayma), 0.150, 0.250);
    }

    private static async Task<List<double>> OrnekleAsync(MpvEngine engine)
    {
        var values = new List<double>();
        for (var i = 0; i < 100; i++)
        {
            var offset = engine.AudioVideoOffsetSeconds;
            if (double.IsFinite(offset)) values.Add(offset);
            await Task.Delay(20);
        }

        return values;
    }
}

public sealed class OynaticiMotorTestsGirdi : IClassFixture<GirdiKlipFixture>
{
    private readonly GirdiKlipFixture _klip;

    public OynaticiMotorTestsGirdi(GirdiKlipFixture klip) => _klip = klip;

    [Fact]
    public async Task OnHizliTikMotoraKarsiBirikirVeAramalarSinirdaKalir()
    {
        Assert.True(_klip.ClipPath is not null, "girdi klibi uretilemedi; ffmpeg gerekli");
        var path = _klip.ClipPath!;
        using var engine = new MpvEngine();
        engine.SetProperty("ao", "null");
        await engine.OpenAsync(path);

        var gosterilmeyen = new List<SeekOutcome>();
        var coalescer = new SeekCoalescer(async at =>
        {
            var result = await engine.SeekAsync(at, SeekPrecision.Exact);
            if (result.Outcome != SeekOutcome.Shown) lock (gosterilmeyen) gosterilmeyen.Add(result.Outcome);
        })
        { Duration = engine.DurationSeconds };

        var saat = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++) coalescer.Nudge(PlayerInputMap.WheelStepSeconds);
        await coalescer.Idle;
        saat.Stop();

        var body = new StringBuilder();
        body.AppendLine($"motor: {engine.Name}, klip: {Path.GetFileName(path)} sure {engine.DurationSeconds.ToString("0.###", CultureInfo.InvariantCulture)} sn");
        body.AppendLine($"10 hizli tik (tik basi 1 sn) -> hedef {coalescer.Target} sn, ulasilan konum {coalescer.Position} sn");
        body.AppendLine($"tetiklenen arama sayisi: {coalescer.SeekCalls}");
        body.AppendLine("arama hedefleri: " + string.Join(", ", coalescer.IssuedTargets));
        body.AppendLine("arama gecikmeleri (ms): " + string.Join(", ", coalescer.LatenciesMs.Select(MotorKanit.Ms)));
        body.AppendLine($"toplam sure: {MotorKanit.Ms(saat.Elapsed.TotalMilliseconds)} ms");
        body.AppendLine($"150 ms sinirini asan arama: {coalescer.LatenciesMs.Count(ms => ms > 150)}");
        body.AppendLine("gosterilmeyen arama: " + (gosterilmeyen.Count == 0 ? "yok" : string.Join(", ", gosterilmeyen)));
        body.AppendLine("arama hatalari: " + (coalescer.Failures.Count == 0 ? "yok" : string.Join(" | ", coalescer.Failures)));
        MotorKanit.Write("k6-on-tik-motor.txt", body.ToString());

        Assert.Equal(10, coalescer.Target);
        Assert.Equal(10, coalescer.Position);
        Assert.True(coalescer.SeekCalls < 10, $"birikme yok: {coalescer.SeekCalls} arama");
        Assert.NotEmpty(coalescer.LatenciesMs);
        Assert.Empty(gosterilmeyen);
        var asan = coalescer.LatenciesMs.Where(ms => ms > 150).ToList();
        Assert.True(asan.Count == 0, "150 ms sinirini asan arama: " + string.Join(", ", asan.Select(MotorKanit.Ms)));
    }
}

public sealed class OynaticiMotorAramaOlculeri
{
    [Fact]
    public async Task H264_1080p_TamAramaMedyaniAltmisMilisaniyeAltinda()
    {
        var olcum = await AramaOlcumu.OlcAsync(MotorKlipleri.H264_1080p60);
        MotorKanit.Write("k7-arama-h264-1080p60.txt", olcum.Rapor("H.264 1080p60, GOP 2 sn, esik medyan <= 60 ms"));

        Assert.Equal(20, olcum.Gosterilen.Count);
        Assert.True(olcum.Medyan <= 60, $"1080p medyan {MotorKanit.Ms(olcum.Medyan)} ms > 60 ms");
    }

    [Fact]
    public async Task H264_2160p_TamAramaMedyaniIkiYuzMilisaniyeAltinda()
    {
        var olcum = await AramaOlcumu.OlcAsync(MotorKlipleri.H264_2160p30);
        MotorKanit.Write("k8-arama-h264-2160p30.txt", olcum.Rapor("H.264 2160p30, GOP 2 sn, esik medyan <= 200 ms"));

        Assert.Equal(20, olcum.Gosterilen.Count);
        Assert.True(olcum.Medyan <= 200, $"2160p medyan {MotorKanit.Ms(olcum.Medyan)} ms > 200 ms");
    }

    [Fact]
    public async Task Hevc_1080p_TamAramaOlculurVeHepsiGosterilir()
    {
        var olcum = await AramaOlcumu.OlcAsync(MotorKlipleri.Hevc_1080p60);
        MotorKanit.Write("k9-arama-hevc-1080p60.txt", olcum.Rapor("HEVC 1080p60, GOP 2 sn, esik yok (pilot 2: 61.7 ms), sayi rapora"));

        Assert.Equal(20, olcum.Gosterilen.Count);
        Assert.True(double.IsFinite(olcum.Medyan) && olcum.Medyan > 0, $"HEVC medyan okunamadi: {olcum.Medyan}");
    }
}
