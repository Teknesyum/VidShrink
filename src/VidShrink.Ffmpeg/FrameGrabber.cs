using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

/// <summary>Karsilastirma panelinin iki yarisi.</summary>
public enum FrameSide { Kaynak, Cikti }

/// <summary>
/// Teslim edilen tek kare. Servis **ne istendigini degil ne teslim ettigini** bildirir:
/// gerceklesen zaman damgasi, gercek goruntuleme boyutu, dondurme ve HDR bayraklari.
/// </summary>
public sealed record GrabbedFrame(
    FrameSide Side,
    byte[] Bgra,
    int Width,
    int Height,
    double RequestedSeconds,
    double ActualSeconds,
    int SourceWidth,
    int SourceHeight,
    int RequestedWidth,
    bool WidthCappedBySource,
    int AppliedRotation,
    bool SourceIsHdr,
    bool ToneMapped)
{
    public bool IsPortrait => Height > Width;
    public long Bytes => Bgra.LongLength;
}

/// <summary>
/// Bir zaman noktasi icin **iki kare birden**. Ayirici cizgi ikisini ayni anda kirpar; tek
/// kare teslim edip otekini istek uzerine cekmek ayiriciyi kilitler. Bu yuzden ikisi
/// birlikte gelir, birlikte durur, birlikte duser.
/// </summary>
public sealed record FramePair(
    GrabbedFrame? Source,
    GrabbedFrame? Output,
    double AtSeconds,
    bool RotationMismatch,
    bool SourceHdrOutputSdr)
{
    public long Bytes => (Source?.Bytes ?? 0) + (Output?.Bytes ?? 0);
    public bool IsEmpty => Source is null && Output is null;
}

public sealed record FramePairRequest
{
    public required string SourcePath { get; init; }

    /// <summary>Sag yarinin dosyasi. Yoksa panel <see cref="PreviewState.YalnizKaynak"/> durumundadir.</summary>
    public string? OutputPath { get; init; }

    public required double AtSeconds { get; init; }

    /// <summary>Panel genisligi x yakinlastirma. Kaynagin kendi genisligiyle tavanlanir.</summary>
    public required int RequestedWidth { get; init; }

    /// <summary>Kaynagin anahtar kare dizini. Verilirse cekim en yakin anahtar kareye hizalanir.</summary>
    public KeyframeIndex? SourceKeyframes { get; init; }

    public KeyframeIndex? OutputKeyframes { get; init; }

    /// <summary>Anahtar kareye hizala. T32/K1: 1080p'de medyani ~%29, p90'i ~%66 dusuruyor.</summary>
    public bool AlignToKeyframe { get; init; } = true;
}

/// <summary>
/// Panelin kare cekme katmani. Tek ucus: yeni istek oncekini iptal eder. Onbellek **bayt**
/// tavaniyla sinirli, adet tavaniyla degil — T30/O4 4K karesini 31,64 MiB olctu, 128 MB
/// tavan 4K'da iki cift demek.
/// </summary>
public sealed class FrameGrabber : IDisposable
{
    /// <summary>T30/O4'un onerdigi tavan: 128 MB.</summary>
    public const long DefaultCacheByteCeiling = 128L * 1024 * 1024;

    private readonly long _cacheByteCeiling;
    private readonly object _gate = new();
    private readonly LinkedList<string> _order = new();
    private readonly Dictionary<string, (LinkedListNode<string> Node, FramePair Pair)> _cache = new();
    private readonly Dictionary<string, MediaProbe> _probes = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _inFlight;
    private long _cacheBytes;
    private int _processesStarted;
    private bool _disposed;

    public FrameGrabber(long cacheByteCeiling = DefaultCacheByteCeiling)
    {
        if (cacheByteCeiling <= 0) throw new ArgumentOutOfRangeException(nameof(cacheByteCeiling));
        _cacheByteCeiling = cacheByteCeiling;
    }

    public long CacheByteCeiling => _cacheByteCeiling;

    public long CacheBytes { get { lock (_gate) return _cacheBytes; } }

    public int CacheCount { get { lock (_gate) return _cache.Count; } }

    /// <summary>Baslatilan ffmpeg/ffprobe surec sayisi. Onbellek isabetinde artmaz.</summary>
    public int ProcessesStarted => Volatile.Read(ref _processesStarted);

    /// <summary>
    /// Kodlama surerken gosterilecek olan. T30/O2 yavaslamayi %17,8-28,4 olctu, konseyin
    /// %5 kurali asildi; o sirada yeni kare cekilmez, bu cift gosterilir.
    /// </summary>
    public FramePair? LastDelivered { get; private set; }

    /// <summary>
    /// Bir zaman noktasinin iki karesini birden teslim eder. Bozuk dosya ya da sure disi
    /// arama <c>null</c> doner, istisna atmaz.
    /// </summary>
    public async Task<FramePair?> GrabPairAsync(
        FramePairRequest request,
        PreviewState state = PreviewState.GercekCikti,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // K5: kodlama surerken cekim kapali, son onbellekli cift gosterilir.
        if (!PreviewStatus.AllowsFrameGrab(state))
            return LastDelivered;

        if (state == PreviewState.KaynakYok) return null;

        var key = CacheKey(request);
        lock (_gate)
        {
            if (_cache.TryGetValue(key, out var hit))
            {
                _order.Remove(hit.Node);
                _order.AddFirst(hit.Node);
                LastDelivered = hit.Pair;
                return hit.Pair;
            }
        }

        // Tek ucus: onceki istek iptal edilir, surec agaci oldurulur. T30 surec acilisinin
        // 53 ms oldugunu olctu; saganaga izin verilmez.
        CancellationTokenSource linked;
        lock (_gate)
        {
            _inFlight?.Cancel();
            _inFlight?.Dispose();
            linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _inFlight = linked;
        }

        try
        {
            var pair = await BuildPairAsync(request, linked.Token);
            if (pair is null || pair.IsEmpty) return null;

            lock (_gate)
            {
                Store(key, pair);
                LastDelivered = pair;
            }
            return pair;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_inFlight, linked))
                {
                    _inFlight = null;
                    linked.Dispose();
                }
            }
        }
    }

    private async Task<FramePair?> BuildPairAsync(FramePairRequest request, CancellationToken ct)
    {
        var sourceProbe = await ProbeAsync(request.SourcePath, ct);
        if (sourceProbe is null) return null;

        var outputProbe = request.OutputPath is null ? null : await ProbeAsync(request.OutputPath, ct);

        // K4/HDR: kaynak PQ/HLG, cikti SDR ise ayni ton esleme zinciri kaynak yarisina da
        // uygulanir. Uygulanmazsa ham kare soluk gri kalir ve kullanici tonu **sikistirma
        // rengi bozdu** diye okur.
        var toneMapSource = sourceProbe.IsHdr && (outputProbe is null || !outputProbe.IsHdr);

        var source = await GrabOneAsync(
            FrameSide.Kaynak, request.SourcePath, sourceProbe, request.AtSeconds,
            request.RequestedWidth, toneMapSource, request.AlignToKeyframe ? request.SourceKeyframes : null, ct);

        // zscale/tonemap her ffmpeg derlemesinde yok (ornegin bazi WinGet paketlerinde).
        // Zincir kurulamazsa kareyi tamamen kaybetmek yerine ton eslemesiz cekilir ve
        // ToneMapped=false ile bildirilir; panel yine de uyarabilir.
        if (source is null && toneMapSource)
            source = await GrabOneAsync(
                FrameSide.Kaynak, request.SourcePath, sourceProbe, request.AtSeconds,
                request.RequestedWidth, false, request.AlignToKeyframe ? request.SourceKeyframes : null, ct);

        // Cikti yarisina ton esleme uygulanmaz: kodlama plani ne karar verdiyse piksellerde
        // zaten pismistir. Kaynak yarisi ona **uydurulur**, tersi degil.
        GrabbedFrame? output = null;
        if (request.OutputPath is not null && outputProbe is not null)
            output = await GrabOneAsync(
                FrameSide.Cikti, request.OutputPath, outputProbe, request.AtSeconds,
                request.RequestedWidth, false,
                request.AlignToKeyframe ? request.OutputKeyframes : null, ct);

        if (source is null && output is null) return null;

        // K4/dondurme: iki yari ters yonde durursa panel ise yaramaz. Iki taraf da ayni
        // kurali (autorotate) uygular; yine de ayrilirlarsa bu bildirilir.
        var mismatch = source is not null && output is not null && source.IsPortrait != output.IsPortrait;

        return new FramePair(
            source,
            output,
            request.AtSeconds,
            mismatch,
            sourceProbe.IsHdr && outputProbe is not null && !outputProbe.IsHdr);
    }

    private async Task<GrabbedFrame?> GrabOneAsync(
        FrameSide side,
        string path,
        MediaProbe probe,
        double atSeconds,
        int requestedWidth,
        bool toneMap,
        KeyframeIndex? keyframes,
        CancellationToken ct)
    {
        if (atSeconds < 0 || atSeconds > probe.DurationSeconds) return null;

        var seekAt = keyframes is { IsEmpty: false } ? keyframes.Floor(atSeconds) : atSeconds;

        // Yakinlastirma geri beslemesi: istenen genislik kaynagin kendi genisligiyle
        // tavanlanir. Kare panel genisliginde cozulup 4x buyutulurse kullanici bizim
        // olceklememizi sikistirma hatasi sanar (T30/O5: buyuk istemek bedava).
        var capped = requestedWidth > probe.DisplayWidth;
        var askedWidth = Even(Math.Clamp(capped ? probe.DisplayWidth : requestedWidth, 2, probe.DisplayWidth));

        var filters = new List<string>();
        if (toneMap) filters.Add(HdrResolver.TonemapFilter);
        // Yukseklik tahmin edilmez, -2 ile en-boy oranindan turetilir; dondurme uygulanmissa
        // sonuc dikey cikar ve bunu **olculur** hale getirir.
        filters.Add($"scale={askedWidth}:-2:flags=bilinear");
        // Gerceklesen zaman damgasi tahmin edilmez, ffmpeg'e sorulur: showinfo teslim edilen
        // karenin pts_time'ini stderr'e basar. stderr zaten bosaltiliyor.
        filters.Add("showinfo");

        var args = new List<string>
        {
            "-hide_banner", "-nostdin", "-loglevel", "info",
            // Bayrak deger almaz; "-autorotate 1" yazilirsa 1 bir cikis dosyasi sanilir.
            // Iki yari da ayni kurali uygular: gosterim matrisi her zaman uygulanir.
            "-autorotate",
            // -copyts olmadan `-ss` giris damgalarini sifirlar ve showinfo arama noktasina
            // gore rapor verir; kaynagin kendi damgasi isteniyor.
            "-copyts",
            "-ss", seekAt.ToString("0.###", CultureInfo.InvariantCulture),
            "-i", path,
            "-frames:v", "1", "-an", "-sn", "-dn",
            "-vf", string.Join(',', filters),
            "-f", "rawvideo", "-pix_fmt", "bgra", "-"
        };

        var psi = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args);
        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();
            Interlocked.Increment(ref _processesStarted);
            TrySetBelowNormal(process);
            using var cancellationRegistration = ct.Register(() => TryKill(process));

            // stdout ikili veri, stderr ayni anda bosaltilir; iki gorev de beklemeye girmeden
            // once baslatilir. Bosaltilmazsa boru dolar ve ffmpeg asilir (RAPOR.md:27).
            var stdoutTask = ReadAllAsync(process.StandardOutput.BaseStream, ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            var pixels = await stdoutTask;
            var log = await stderrTask;
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0 || pixels.Length == 0) return null;

            // Teslim edilen boyut da ffmpeg'den okunur; bayt sayisi onu dogrulamazsa kare atilir.
            var delivered = DeliveredSize(log);
            if (delivered is not { } size) return null;
            if (pixels.LongLength < (long)size.Width * size.Height * 4) return null;

            return new GrabbedFrame(
                side, pixels, size.Width, size.Height,
                atSeconds, DeliveredPts(log) ?? seekAt,
                probe.DisplayWidth, probe.DisplayHeight,
                requestedWidth, capped,
                probe.Rotation, probe.IsHdr, toneMap);
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    private void Store(string key, FramePair pair)
    {
        if (_cache.ContainsKey(key)) return;

        // Tavani asan tek cift hic saklanmaz; onbellegi bosaltmasinin anlami yok.
        if (pair.Bytes > _cacheByteCeiling) return;

        while (_cacheBytes + pair.Bytes > _cacheByteCeiling && _order.Last is { } oldest)
        {
            var evicted = _cache[oldest.Value];
            _cacheBytes -= evicted.Pair.Bytes;
            _cache.Remove(oldest.Value);
            _order.RemoveLast();
        }

        var node = _order.AddFirst(key);
        _cache[key] = (node, pair);
        _cacheBytes += pair.Bytes;
    }

    public void ClearCache()
    {
        lock (_gate)
        {
            _cache.Clear();
            _order.Clear();
            _cacheBytes = 0;
        }
    }

    private static string CacheKey(FramePairRequest request) => string.Join(
        '|',
        request.SourcePath,
        request.OutputPath ?? "-",
        request.AtSeconds.ToString("0.###", CultureInfo.InvariantCulture),
        request.RequestedWidth.ToString(CultureInfo.InvariantCulture),
        request.AlignToKeyframe ? "k" : "-");

    private static int Even(int value) => value % 2 == 0 ? value : value - 1;

    // showinfo suzgeci kodlayicidan daha cok kare gorur: `-frames:v 1` ciktiya tek kare yazar
    // ama filtre zincirinden birkaci gecer. Teslim edilen kare **ilkidir**, sonuncusu degil.

    /// <summary>showinfo satirindaki <c>s:WxH</c> alani: karenin gercekten teslim edilen boyutu.</summary>
    private static (int Width, int Height)? DeliveredSize(string log)
    {
        var at = log.IndexOf(" s:", StringComparison.Ordinal);
        if (at < 0) return null;

        var start = at + 3;
        var end = start;
        while (end < log.Length && (char.IsAsciiDigit(log[end]) || log[end] == 'x')) end++;
        var parts = log[start..end].Split('x');
        if (parts.Length != 2) return null;
        if (!int.TryParse(parts[0], out var width) || !int.TryParse(parts[1], out var height)) return null;
        return width > 0 && height > 0 ? (width, height) : null;
    }

    /// <summary>showinfo satirindaki <c>pts_time:</c> alani; bulunamazsa <c>null</c>.</summary>
    private static double? DeliveredPts(string log)
    {
        const string marker = "pts_time:";
        var at = log.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0) return null;

        var start = at + marker.Length;
        var end = start;
        while (end < log.Length && (char.IsAsciiDigit(log[end]) || log[end] is '.' or '-')) end++;
        var text = log[start..end];
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var pts) && pts >= 0
            ? pts
            : null;
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, 128 * 1024, ct);
        return buffer.ToArray();
    }

    private static void TrySetBelowNormal(Process process)
    {
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_gate)
        {
            _inFlight?.Cancel();
            _inFlight?.Dispose();
            _inFlight = null;
        }
        ClearCache();
    }

    // --- dosya bilgisi ----------------------------------------------------------------

    private sealed record MediaProbe(
        int DisplayWidth,
        int DisplayHeight,
        int Rotation,
        double DurationSeconds,
        bool IsHdr);

    private async Task<MediaProbe?> ProbeAsync(string path, CancellationToken ct)
    {
        lock (_gate)
            if (_probes.TryGetValue(path, out var cached)) return cached;

        var probe = await RunProbeAsync(path, ct);
        if (probe is null) return null;

        lock (_gate) _probes[path] = probe;
        return probe;
    }

    private async Task<MediaProbe?> RunProbeAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return null;

        var args = new[]
        {
            "-hide_banner", "-v", "error",
            "-select_streams", "v:0",
            "-print_format", "json",
            "-show_streams", "-show_format",
            path
        };

        try
        {
            using var process = new Process { StartInfo = ToolLocator.StartInfo(ToolLocator.Ffprobe, args) };
            process.Start();
            Interlocked.Increment(ref _processesStarted);
            using var cancellationRegistration = ct.Register(() => TryKill(process));

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            var stdout = await stdoutTask;
            _ = await stderrTask;
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0) return null;

            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            if (!root.TryGetProperty("streams", out var streams) || streams.GetArrayLength() == 0) return null;

            var v = streams[0];
            var coded = (Width: Int(v, "width") ?? 0, Height: Int(v, "height") ?? 0);
            if (coded.Width <= 0 || coded.Height <= 0) return null;

            var rotation = Rotation(v);
            var display = Math.Abs(rotation) % 180 == 90
                ? (Width: coded.Height, Height: coded.Width)
                : coded;

            var duration = Seconds(root, v);
            if (duration <= 0) return null;

            var transfer = Str(v, "color_transfer");
            var pixFmt = Str(v, "pix_fmt");
            var isHdr = transfer is "smpte2084" or "arib-std-b67"
                || (Str(v, "color_primaries") is "bt2020" && (pixFmt?.Contains("10le") ?? false));

            return new MediaProbe(display.Width, display.Height, rotation, duration, isHdr);
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    private static double Seconds(JsonElement root, JsonElement stream)
    {
        if (root.TryGetProperty("format", out var format) && Dbl(format, "duration") is { } fromFormat && fromFormat > 0)
            return fromFormat;
        return Dbl(stream, "duration") ?? 0;
    }

    private static int Rotation(JsonElement stream)
    {
        var rotation = 0;
        if (stream.TryGetProperty("tags", out var tags) && Int(tags, "rotate") is { } tagged) rotation = tagged;
        if (stream.TryGetProperty("side_data_list", out var list))
            foreach (var item in list.EnumerateArray())
                if (Int(item, "rotation") is { } fromSideData) rotation = fromSideData;
        return rotation;
    }

    private static string? Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number) return (int)v.GetDouble();
        return int.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var i) ? i : null;
    }

    private static double? Dbl(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
        return double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}
