using System.Diagnostics;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

/// <summary>
/// Canli bir kaydin ilerleme okumasi. <b>Kesir tasimaz</b>: ekran kaydinin suresi bastan
/// bilinmiyor, <c>EncodeRunner.RunCommandAsync</c> ise kesri kaynagin suresine boluyor
/// (<c>EncodeRunner.cs</c>, <c>durationSeconds</c>). Boyle bir bolen olmadigi icin burada
/// yuzde uretilmez; kullaniciya gosterilecek olanlar gecen sure, dosyaya yazilan boyut ve
/// ffmpeg'in dusurdugu kare sayisidir.
/// </summary>
/// <param name="Elapsed">Oturumun duvar saati; duraklatma ve devam arasinda da islemeye devam eder.</param>
/// <param name="Captured">Dosyaya giren goruntunun toplam suresi; duraklatilan araliklar buna girmez.</param>
/// <param name="OutputMb">O ana kadar yazilan boyut.</param>
/// <param name="Frames">Yazilan kare sayisi, parcalar boyunca birikir.</param>
/// <param name="DroppedFrames">ffmpeg'in dusurdugu kare sayisi (<c>drop_frames</c>).</param>
public sealed record RecordProgress(
    TimeSpan Elapsed,
    TimeSpan Captured,
    double OutputMb,
    long Frames,
    long DroppedFrames);

/// <summary>Oturumun o andaki hali.</summary>
public enum RecorderState
{
    /// <summary>ffmpeg kosuyor, kareler dosyaya yaziliyor.</summary>
    Running,

    /// <summary>Parca nazikce kapatildi, yenisi acilmadi.</summary>
    Paused,

    /// <summary>Kayit bitti; sonuc uretildi.</summary>
    Stopped
}

/// <summary>
/// Bitmis bir kaydin sonucu.
/// </summary>
/// <param name="Ok">Dosya tamamlandi: butun parcalar 0 ile dondu ve birlestirme basarili.</param>
/// <param name="OutputPath">Teslim edilen dosya. <paramref name="Partial"/> dogruyken de doludur: yarim dosya da diskte durur.</param>
/// <param name="OutputMb">Teslim edilen dosyanin boyutu.</param>
/// <param name="Partial">
/// Surec nazik yoldan degil <b>oldurulerek</b> kapandi. O zaman mux kapanmamis olur:
/// olculen ornekte dosya 48 bayt kaldi ve ffprobe "moov atom not found" ile 1 dondu
/// (<c>.calisma/dalga8a/</c>). Cagiran bu dosyayi oynatilabilir saymaz.
/// </param>
/// <param name="ExitCode">Son ffmpeg surecinin cikis kodu.</param>
/// <param name="StandardError">Son satirlar; sebep hep sonda durur.</param>
/// <param name="Segments">Kac parca kaydedildi. Duraklatma her seferinde yeni bir parca acar.</param>
/// <param name="DroppedOptions">ffmpeg'in kabul etmeyip sessizce dusurdugu ayarlarin tanili satirlari.</param>
/// <param name="Files">
/// Teslim edilen dosyalarin hepsi. Birlestirilen kayitta tek eleman ve
/// <paramref name="OutputPath"/> ile ayni; kendiliginden bolunen kayitta bolum sayisi
/// kadar eleman var ve <paramref name="OutputPath"/> ilki oluyor.
/// </param>
/// <param name="DeliveryError">
/// Parca istenen ada tasinamadi; <paramref name="OutputPath"/> ve <paramref name="Files"/> o zaman
/// dosyanin gercekte durdugu parca yolunu gosterir, bu alan da sebebi tasir.
/// </param>
/// <param name="MissingGif">GIF istendi ama cevrilemedi: istenen GIF yolu. Yakalama dosyasi yerinde korunur.</param>
/// <param name="Playable">
/// Yarim dosya ffprobe ile yoklandi: <c>true</c> en az bir video paketi okundu, <c>false</c> okunamadi,
/// <c>null</c> yoklanmadi ya da ffprobe calismadi.
/// </param>
public sealed record RecordResult(
    bool Ok,
    string OutputPath,
    double OutputMb,
    bool Partial,
    int ExitCode,
    string StandardError,
    int Segments,
    IReadOnlyList<string>? DroppedOptions = null,
    IReadOnlyList<string>? Files = null,
    string? DeliveryError = null,
    string? MissingGif = null,
    bool? Playable = null)
{
    /// <summary>Kayitta verilen ayarlardan en az biri dusurulmus.</summary>
    public bool DroppedAnOption => DroppedOptions is { Count: > 0 };

    /// <summary>Kayit birden fazla dosyaya bolunmus.</summary>
    public bool WasSplit => Files is { Count: > 1 };
}

/// <summary>
/// Suresi bastan bilinmeyen bir ekran kaydini surer. Arguman uretmez — ne uretilecegini
/// <see cref="RecorderArguments"/> bilir, burasi yalnizca kosturur.
/// <para>
/// Durdurma yolu <b>nazik</b>: stdin'e <c>q</c> yazilir, ffmpeg kendi mux'unu kapatir ve 0
/// ile doner (olculen ornek: 5.600 s, 84 kare, ffprobe 0). <see cref="Kill"/> yalnizca
/// <paramref name="stopTimeoutMs"/> dolduğunda calisir ve o dosya
/// <see cref="RecordResult.Partial"/> isaretlenir. Bu yuzden argumanlarda <c>-nostdin</c>
/// <b>yoktur</b>; deponun oteki cagrilarinin hepsinde var.
/// </para>
/// <para>
/// Duraklatmanin ffmpeg'de karsiligi yok: <see cref="PauseAsync"/> o anki parcayi nazikce
/// kapatir, <see cref="ResumeAsync"/> yeni bir parca acar, <see cref="StopAsync"/> parcalari
/// <c>concat</c> demuxer'i ile yeniden kodlamadan birlestirir. Boylece duraklatma da nazik
/// durdurma yolundan geciyor ve duraklatilan aralik dosyada zaman olarak durmuyor.
/// </para>
/// </summary>
public sealed class RecorderSession : IAsyncDisposable
{
    /// <summary>Nazik durdurmaya taninan sure. Dolarsa surec oldurulur ve dosya yarim isaretlenir.</summary>
    public const int StopTimeoutMs = 10_000;

    /// <summary>Ilk ilerleme blogu icin beklenen sure; dolmadan surec olurse baslatma hatasi bildirilir.</summary>
    public const int StartTimeoutMs = 15_000;

    /// <summary>
    /// ffmpeg'in <c>-progress</c> blogunu yazma araligi. Yoklamaya artik bagli degil — her
    /// parca kendi <c>-t</c>/<c>-fs</c> sinirini <see cref="RecorderArguments.ForSegment"/>
    /// uzerinden tasiyor ve ffmpeg parcayi icerik zamaninda kendisi kapatiyor
    /// (<see cref="WatchExitAsync"/> dogal cikisi "parca doldu, sonrakini ac" diye okuyor).
    /// Eskiden bolme, ayri bir gorevin 250 ms'de bir yokladigi <c>SplitDue</c> ile disaridan
    /// kararlastirilip nazik <c>q</c> kapanisiyla uygulaniyordu; o yol CI yukunde nazik
    /// kapanisin gecikmesini parca suresine katiyordu (2 sn'lik bolmede 4,4 sn'lik parca,
    /// <c>docs/olcumler/kayit-bolme-parca-siniri.md</c>). 0,1 sn'lik blok araligi yalnizca
    /// ilerleme raporunun (<c>RecordProgress</c>) tazeligini etkiliyor, parca sinirini degil.
    /// </summary>
    public const double StatsPeriodSeconds = 0.1;

    private readonly RecorderRequest _request;
    private readonly string _outputPath;
    private readonly IProgress<RecordProgress>? _progress;
    private readonly List<string> _segments = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly EncodeRunner.StderrWatch _watch = new();
    private readonly SemaphoreSlim _turn = new(1, 1);

    private Process? _process;
    private Task? _stdoutPump;
    private Task? _stderrPump;
    private long _frames;
    private long _framesBefore;
    private long _dropped;
    private long _droppedBefore;
    private double _outputMb;
    private TimeSpan _capturedBefore;
    private TimeSpan _capturedNow;
    private int _lastExitCode;
    private bool _partial;
    private TimeSpan _segmentCapturedAtStart;
    private double _segmentWrittenAtStart;
    private string? _gifPath;
    private bool _closing;
    private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private RecorderSession(RecorderRequest request, string outputPath, IProgress<RecordProgress>? progress)
    {
        _request = request;
        _outputPath = outputPath;
        _progress = progress;
    }

    /// <summary>Oturumun o andaki hali.</summary>
    public RecorderState State { get; private set; } = RecorderState.Stopped;

    /// <summary>Bugune kadar acilan parca sayisi; duraklatma her seferinde bir tane daha acar.</summary>
    public int SegmentCount => _segments.Count;

    public double WrittenMb
    {
        get
        {
            try { return _segments.ToArray().Sum(SizeMb); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return 0; }
        }
    }

    /// <summary>
    /// Kaydi baslatir ve ilk ilerleme blogunu bekler. Surec o blogu uretmeden olurse
    /// sebep yutulmaz: ffmpeg'in son satirlariyla <see cref="InvalidOperationException"/>
    /// atilir (olculen ornek: olmayan pencere adi).
    /// </summary>
    public static async Task<RecorderSession> StartAsync(
        RecorderRequest request, string outputPath,
        IProgress<RecordProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Output path is required.", nameof(outputPath));

        RecorderSession session;
        if (request.Container == RecorderContainer.Gif)
        {
            var errors = RecorderArguments.Validate(request, outputPath);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            session = new RecorderSession(
                RecorderArguments.CaptureRequest(request),
                RecorderArguments.CapturePath(outputPath, request.Container),
                progress)
            { _gifPath = outputPath };
        }
        else if (RecorderArguments.CapturesInMatroska(request))
        {
            var errors = RecorderArguments.Validate(request, outputPath);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            session = new RecorderSession(
                request with { Container = RecorderContainer.Mkv },
                RecorderArguments.MatroskaCapturePath(outputPath),
                progress)
            { _remuxExtension = Path.GetExtension(outputPath) };
        }
        else
        {
            session = new RecorderSession(request, outputPath, progress);
        }

        await session.StartSegmentAsync(ct);
        return session;
    }

    /// <summary>
    /// Kayit surerken tek karelik bir ekran goruntusu alir. Kaydi kesmiyor: yakalama
    /// girdisinden ikinci, kisa omurlu bir ffmpeg surecine okunuyor. Kare kayit
    /// dosyasindan degil ekrandan geliyor: canli ekranin dosyasi yok, dosyadan kare kesen bir
    /// yol buraya uymaz.
    /// </summary>
    public async Task<bool> SnapshotAsync(string imagePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath)) throw new ArgumentException("Image path is required.", nameof(imagePath));

        var folder = Path.GetDirectoryName(imagePath);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        var run = await FfmpegRunner.RunAsync(RecorderArguments.BuildSnapshot(_request, imagePath), ct);
        return run.Ok && File.Exists(imagePath) && new FileInfo(imagePath).Length > 0;
    }

    /// <summary>
    /// O anki parcayi nazikce kapatir. Dosya kapali ve oynatilabilir kalir; devam etmek
    /// <see cref="ResumeAsync"/> ile yeni bir parca acar.
    /// </summary>
    public async Task PauseAsync(int stopTimeoutMs = StopTimeoutMs, CancellationToken ct = default)
    {
        await _turn.WaitAsync(ct);
        try
        {
            if (State != RecorderState.Running) return;
            await FinishSegmentAsync(stopTimeoutMs, ct);
            State = RecorderState.Paused;
        }
        finally { _turn.Release(); }
    }

    /// <summary>Duraklatilmis kayda yeni bir parca acar.</summary>
    public async Task ResumeAsync(CancellationToken ct = default)
    {
        await _turn.WaitAsync(ct);
        try
        {
            if (State != RecorderState.Paused) return;
            await StartSegmentAsync(ct);
        }
        finally { _turn.Release(); }
    }

    /// <summary>
    /// Kaydi bitirir: son parca nazikce kapatilir, birden fazla parca varsa yeniden
    /// kodlanmadan birlestirilir. Kendiliginden bolme acikken birlestirme <b>yapilmaz</b>,
    /// parcalar numarali dosyalar olarak teslim edilir.
    /// <paramref name="stopTimeoutMs"/> dolarsa surec oldurulur ve sonuc
    /// <see cref="RecordResult.Partial"/> doner.
    /// </summary>
    public async Task<RecordResult> StopAsync(int stopTimeoutMs = StopTimeoutMs, CancellationToken ct = default)
    {
        await _turn.WaitAsync(ct);
        try
        {
            if (_process is not null) await FinishSegmentAsync(stopTimeoutMs, ct);
            State = RecorderState.Stopped;
        }
        finally { _turn.Release(); }

        var result = await AssembleAsync(ct);
        if (_remuxExtension is not null) return await RemuxAsync(result, ct);
        return _gifPath is null ? result : await ConvertToGifAsync(result, ct);
    }

    private string? _remuxExtension;

    private async Task<RecordResult> RemuxAsync(RecordResult capture, CancellationToken ct)
    {
        if (!capture.Ok && !capture.Partial) return capture;
        var files = capture.Files ?? new[] { capture.OutputPath };
        var delivered = new List<string>(files.Count);
        foreach (var file in files)
        {
            if (!File.Exists(file)) continue;
            var target = RecorderArguments.MatroskaDeliveryPath(file, _remuxExtension!);
            var run = await FfmpegRunner.RunAsync(new[]
            {
                "-hide_banner", "-y", "-nostdin", "-i", file, "-map", "0", "-c", "copy", "-movflags", "+faststart", target
            }, ct);
            if (!run.Ok || !File.Exists(target))
                return capture with { Ok = false, ExitCode = run.ExitCode, StandardError = run.StandardError };
            TryDelete(file);
            delivered.Add(target);
        }

        if (delivered.Count == 0) return capture;
        return capture with { OutputPath = delivered[0], OutputMb = delivered.Sum(SizeMb), Files = delivered };
    }

    private Task<RecordResult> ConvertToGifAsync(RecordResult capture, CancellationToken ct)
        => ConvertToGifAsync(capture, _gifPath, _request.Fps, ct);

    /// <summary>
    /// Yakalama dosyasini GIF'e cevirir. Cevrilemezse yakalama dosyasi silinmez ve sonuc
    /// <see cref="RecordResult.MissingGif"/> ile GIF'in uretilmedigini soyler.
    /// </summary>
    internal static async Task<RecordResult> ConvertToGifAsync(RecordResult capture, string? gifPath, int fps, CancellationToken ct)
    {
        if (!capture.Ok || gifPath is null || !File.Exists(capture.OutputPath)) return capture;

        var run = await FfmpegRunner.RunAsync(GifPalette.Build(capture.OutputPath, gifPath, fps), ct);
        if (!run.Ok || !File.Exists(gifPath))
            return capture with { Ok = false, ExitCode = run.ExitCode, StandardError = run.StandardError, MissingGif = gifPath };

        TryDelete(capture.OutputPath);
        return capture with { OutputPath = gifPath, OutputMb = SizeMb(gifPath), Files = new[] { gifPath } };
    }

    /// <summary>Yarida kalan bir oturum makinede ffmpeg birakmaz.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_process is null) return;
        _partial = true;
        await FinishSegmentAsync(0, CancellationToken.None);
        State = RecorderState.Stopped;
    }

    private async Task StartSegmentAsync(CancellationToken ct)
    {
        if (RecorderArguments.ForSegment(_request, _capturedBefore, _segments.Count == 0 ? 0 : WrittenMb) is not { } segment)
        {
            State = RecorderState.Stopped;
            return;
        }

        _segmentCapturedAtStart = _capturedBefore;
        _segmentWrittenAtStart = _segments.Count == 0 ? 0 : WrittenMb;
        var path = SegmentPath(_segments.Count);
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        var args = new List<string>
        {
            "-progress", "pipe:1", "-nostats",
            "-stats_period", StatsPeriodSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
        };
        args.AddRange(RecorderArguments.Build(segment, path));

        var startInfo = ToolLocator.StartInfo(ToolLocator.Ffmpeg, args);
        startInfo.RedirectStandardInput = true;

        var process = new Process { StartInfo = startInfo };
        process.Start();
        _process = process;
        _segments.Add(path);

        var firstBlock = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _stdoutPump = Task.Run(() => PumpProgressAsync(process, firstBlock), CancellationToken.None);
        _stderrPump = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync()) is not null)
                _watch.Line(line);
        }, CancellationToken.None);

        var kazanan = await Task.WhenAny(firstBlock.Task, process.WaitForExitAsync(ct), Task.Delay(StartTimeoutMs, ct));

        if (!ReferenceEquals(kazanan, firstBlock.Task) && process.HasExited)
        {
            _ = await Task.WhenAny(_stdoutPump, Task.Delay(StartTimeoutMs, ct));
        }

        var bittiSayilir = firstBlock.Task.IsCompletedSuccessfully && firstBlock.Task.Result;
        if (!bittiSayilir && process.HasExited && process.ExitCode == 0)
        {
            bittiSayilir = File.Exists(path) && new FileInfo(path).Length > 0;
        }

        if (bittiSayilir)
        {
            State = RecorderState.Running;
            _ = WatchExitAsync(process);
            return;
        }

        await FinishSegmentAsync(0, CancellationToken.None);
        _segments.Remove(path);
        State = RecorderState.Stopped;
        throw new InvalidOperationException(
            $"ffmpeg kaydi baslatamadi ({_lastExitCode}): {FfmpegRunner.Tail(string.Join('\n', _watch.Close(_lastExitCode).Tail))}");
    }

    /// <summary>Ölçü dikişi: ilerleme bloğu hiç gelmemiş gibi davranır; CI'daki belirtinin aynısı.</summary>
    internal static bool IlerlemeyiYut;

    private async Task PumpProgressAsync(Process process, TaskCompletionSource<bool> firstBlock)
    {
        string? line;
        while ((line = await process.StandardOutput.ReadLineAsync()) is not null)
        {
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            var key = line[..separator];
            var value = line[(separator + 1)..].Trim();

            switch (key)
            {
                case "frame" when long.TryParse(value, out var frame):
                    _frames = _framesBefore + frame;
                    break;
                case "drop_frames" when long.TryParse(value, out var drop):
                    _dropped = _droppedBefore + drop;
                    break;
                case "total_size" when long.TryParse(value, out var size):
                    _outputMb = Megabayt.Oku(size);
                    break;
                case "out_time_us" when long.TryParse(value, out var micros):
                    _capturedNow = TimeSpan.FromSeconds(micros / 1_000_000.0);
                    break;
                case "out_time_ms" when long.TryParse(value, out var legacy) && _capturedNow == TimeSpan.Zero:
                    _capturedNow = TimeSpan.FromSeconds(legacy / 1_000_000.0);
                    break;
                case "progress" when !IlerlemeyiYut:
                    firstBlock.TrySetResult(true);
                    _progress?.Report(new RecordProgress(
                        _clock.Elapsed, _capturedBefore + _capturedNow, _outputMb, _frames, _dropped));
                    break;
            }
        }

        firstBlock.TrySetResult(false);
    }

    private async Task FinishSegmentAsync(int stopTimeoutMs, CancellationToken ct)
    {
        var process = _process;
        if (process is null) return;

        _closing = true;
        if (stopTimeoutMs > 0) FfmpegRunner.RequestGracefulStop(process);

        if (!await WaitForExitAsync(process, stopTimeoutMs, ct))
        {
            _partial = true;
            Kill(process);
            try { await process.WaitForExitAsync(CancellationToken.None); } catch { }
        }

        if (_stdoutPump is not null) await _stdoutPump;
        if (_stderrPump is not null) await _stderrPump;

        try { _lastExitCode = process.ExitCode; } catch { _lastExitCode = -1; }
        if (_lastExitCode != 0) _partial = true;

        _capturedBefore += _capturedNow;
        _capturedNow = TimeSpan.Zero;
        _framesBefore = _frames;
        _droppedBefore = _dropped;

        process.Dispose();
        _process = null;
        _stdoutPump = null;
        _stderrPump = null;
        _closing = false;
    }

    public Task Ended => _ended.Task;

    /// <summary>
    /// Surecin kendiliginden cikisi. Her parca kendi <c>-t</c>/<c>-fs</c> sinirini tasidigi
    /// icin dogal cikis iki seyden biri olabilir: bolme sinirina gelindi (daha kayit sürecek)
    /// ya da kaydin toplam siniri tukendi (kayit bitti). Ikisini <c>RecorderArguments.NaturalExitContinues</c>
    /// ayirt ediyor: parca bolme olcutuyle sinirlanmissa "parca doldu, sonrakini ac" —
    /// <c>StartSegmentAsync</c> yeni bir surec acar ve bu gorev kendini yeni surec icin yeniden
    /// kurar; degilse kayit bitmis sayilir. Kalan sureye bakmak yetmiyor: son <c>out_time</c>
    /// <c>-t</c>'nin birkac ms altinda kalinca bolmesiz kayit ikinci bir parca aciyordu. Kullanicinin <see cref="PauseAsync"/>/<see cref="StopAsync"/>/
    /// <see cref="DisposeAsync"/> ile nazikce durdurmasi <c>_closing</c> bayragiyla burasindan
    /// ayrilir, o yuzden kullanicinin durdurmasi eskisi gibi calisir.
    /// </summary>
    private async Task WatchExitAsync(Process process)
    {
        try { await process.WaitForExitAsync(); }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException) { return; }

        if (_closing || !ReferenceEquals(_process, process)) return;
        await _turn.WaitAsync();
        var finished = true;
        try
        {
            if (!ReferenceEquals(_process, process)) return;
            await FinishSegmentAsync(StopTimeoutMs, CancellationToken.None);

            if (!_partial && RecorderArguments.NaturalExitContinues(_request, _segmentCapturedAtStart, _segmentWrittenAtStart, _capturedBefore))
            {
                try
                {
                    await StartSegmentAsync(CancellationToken.None);
                    finished = State != RecorderState.Running;
                }
                catch (InvalidOperationException) { State = RecorderState.Stopped; }
            }
            else
            {
                State = RecorderState.Stopped;
            }
        }
        finally { _turn.Release(); }

        if (finished) _ended.TrySetResult();
    }

    private async Task<RecordResult> AssembleAsync(CancellationToken ct)
    {
        var outcome = _watch.Close(_lastExitCode);
        var tail = FfmpegRunner.Tail(string.Join('\n', outcome.Tail));

        if (_segments.Count == 0)
            return new RecordResult(false, _outputPath, 0, _partial, _lastExitCode, tail, 0, outcome.DroppedOptions);

        if (_segments.Count == 1)
        {
            var (delivered, error) = Deliver(_segments[0], _outputPath);
            return new RecordResult(!_partial, delivered, SizeMb(delivered), _partial, _lastExitCode, tail, 1,
                outcome.DroppedOptions, new[] { delivered }, DeliveryError: error,
                Playable: _partial ? await HasPacketAsync(delivered, ct) : null);
        }

        if (_partial)
            return new RecordResult(false, _segments[^1], SizeMb(_segments[^1]), true, _lastExitCode, tail, _segments.Count,
                outcome.DroppedOptions, new[] { _segments[^1] }, Playable: await HasPacketAsync(_segments[^1], ct));

        if (_request.Split is { IsSet: true })
        {
            var parts = DeliverAll(_segments.Select((segment, index) => (segment, SplitPath(index))).ToList(), out var error);
            return new RecordResult(true, parts[0], parts.Sum(SizeMb), false, _lastExitCode, tail, parts.Count,
                outcome.DroppedOptions, parts, DeliveryError: error);
        }

        var listPath = _outputPath + ".parcalar.txt";
        File.WriteAllLines(listPath, _segments.Select(path => $"file '{path.Replace("'", @"'\''")}'"));

        var join = await FfmpegRunner.RunAsync(new[]
        {
            "-hide_banner", "-y", "-f", "concat", "-safe", "0", "-i", listPath, "-c", "copy", _outputPath
        }, ct);

        if (!join.Ok)
            return new RecordResult(false, _segments[^1], SizeMb(_segments[^1]), _partial, join.ExitCode, join.StandardError, _segments.Count,
                join.DroppedOptions, new[] { _segments[^1] });

        foreach (var segment in _segments) TryDelete(segment);
        TryDelete(listPath);

        return new RecordResult(true, _outputPath, SizeMb(_outputPath), false, join.ExitCode, tail, _segments.Count,
            outcome.DroppedOptions, new[] { _outputPath });
    }

    /// <summary>
    /// Tek parcali kayit dogrudan istenen ada tasinir; yarim dosya da tasinir, gizlenmez.
    /// Tasima basarisizsa donen yol dosyanin gercekte durdugu parca yoludur ve hata sebebi yaninda gelir.
    /// </summary>
    internal static (string Path, string? Error) Deliver(string segment, string outputPath)
    {
        if (string.Equals(segment, outputPath, StringComparison.OrdinalIgnoreCase)) return (outputPath, null);
        if (!File.Exists(segment)) return (outputPath, null);
        try
        {
            File.Move(segment, outputPath, overwrite: true);
            return (outputPath, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (segment, ex.Message);
        }
    }

    /// <summary>Bolunen kaydin bolumlerini tasir; donen liste her bolumun diskteki gercek yolu, ilk hata <paramref name="error"/>'da.</summary>
    internal static IReadOnlyList<string> DeliverAll(IReadOnlyList<(string Segment, string Target)> moves, out string? error)
    {
        error = null;
        var paths = new List<string>(moves.Count);
        foreach (var (segment, target) in moves)
        {
            var (path, failure) = Deliver(segment, target);
            paths.Add(path);
            error ??= failure;
        }

        return paths;
    }

    /// <summary>
    /// Yarim dosyanin oynatilabilir oldugu kap turunden cikarilmaz, dosyadan okunur: ffprobe ilk
    /// video paketini okuyabiliyorsa <c>true</c>, okuyamiyorsa <c>false</c>, ffprobe calismadiysa <c>null</c>.
    /// </summary>
    internal static async Task<bool?> HasPacketAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0) return false;
        try
        {
            using var process = new Process
            {
                StartInfo = ToolLocator.StartInfo(ToolLocator.Ffprobe, new[]
                {
                    "-hide_banner", "-v", "error", "-select_streams", "v:0", "-read_intervals", "%+#1",
                    "-show_entries", "packet=size", "-of", "csv=p=0", path
                })
            };
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync(ct);
            var stderr = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            await stderr;
            return process.ExitCode == 0 && (await stdout).Trim().Length > 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    private string SegmentPath(int index)
    {
        var extension = Path.GetExtension(_outputPath);
        var withoutExtension = Path.Combine(
            Path.GetDirectoryName(_outputPath) ?? string.Empty,
            Path.GetFileNameWithoutExtension(_outputPath));
        return $"{withoutExtension}.kayit{index}{extension}";
    }

    /// <summary>
    /// Kendiliginden bolunen kaydin bolum dosyasi. Ilk bolum de numarali: numarasiz bir
    /// ilk dosya bolunmus kaydi tek dosyali kayittan ayirt edilemez yapardi.
    /// </summary>
    private string SplitPath(int index)
    {
        var extension = Path.GetExtension(_outputPath);
        var withoutExtension = Path.Combine(
            Path.GetDirectoryName(_outputPath) ?? string.Empty,
            Path.GetFileNameWithoutExtension(_outputPath));
        return $"{withoutExtension}.bolum{index + 1}{extension}";
    }

    private static double SizeMb(string path)
        => File.Exists(path) ? Megabayt.Oku(new FileInfo(path).Length) : 0;

    private static async Task<bool> WaitForExitAsync(Process process, int timeoutMs, CancellationToken ct)
    {
        if (timeoutMs <= 0) return process.HasExited;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(timeoutMs);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return process.HasExited;
        }
    }

    private static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
