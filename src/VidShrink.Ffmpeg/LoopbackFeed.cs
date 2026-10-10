using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using VidShrink.Core;

namespace VidShrink.Ffmpeg;

/// <summary>
/// Sistem sesini ffmpeg'e adlandirilmis borudan verir. Kaydedici stdin'i nazik durdurma icin
/// kullandigindan PCM oraya yazilamaz. <see cref="Bind"/> argumandaki
/// <see cref="LoopbackAudio.PipeToken"/>'i benzersiz bir boru adiyla degistirir ve boruyu
/// ffmpeg baslamadan once acar; pompa ffmpeg baglaninca yazmaya baslar.
/// <para>
/// Pompa hicbir kosulda acik boru birakmaz: ffmpeg kapaninca yazma duser ve pompa biter,
/// ffmpeg hic baglanmazsa <see cref="Dispose"/> beklemeyi keser. Kaynak yoksa ya da veri
/// vermiyorsa boruya sessizlik yazilir (<see cref="LoopbackTimeline"/>), boylece ffmpeg ses
/// girdisinde beklemez ve ses izi duvar saatinden kopmaz.
/// </para>
/// </summary>
internal sealed class LoopbackFeed : IDisposable
{
    private const int TickMs = 10;
    private const int PipeBytes = 65536;
    private const int StaleReads = 8;

    private static readonly byte[] Silence = new byte[LoopbackTimeline.MaxSilenceFrames * LoopbackAudio.BytesPerFrame];

    private readonly NamedPipeServerStream _pipe;
    private readonly ISystemAudioCapture? _capture;
    private readonly CancellationTokenSource _cancel = new();
    private long _captureStart;

    private LoopbackFeed(NamedPipeServerStream pipe, ISystemAudioCapture? capture)
    {
        _pipe = pipe;
        _capture = capture;
        Completion = Task.Run(PumpAsync);
    }

    /// <summary>Pompanin bitisi; boru ve kaynak o anda kapanmistir. Atmaz.</summary>
    public Task Completion { get; }

    public SystemAudioState State => _capture?.State ?? SystemAudioState.Unavailable;

    /// <summary>
    /// Argumanlar loopback borusundan okumuyorsa <c>null</c> doner ve hicbir sey acilmaz.
    /// Okuyorsa jeton yerinde degistirilir; donen besleme surecle birlikte kapatilmalidir.
    /// </summary>
    public static LoopbackFeed? Bind(IList<string> arguments, Func<ISystemAudioCapture?> open)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(open);

        var index = arguments.IndexOf(LoopbackAudio.PipeToken);
        if (index < 0) return null;

        var suffix = $"-{Environment.ProcessId}-{Guid.NewGuid():N}";
        var path = LoopbackAudio.PipeToken + suffix;
        var pipe = new NamedPipeServerStream(
            Path.GetFileName(path), PipeDirection.Out, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, 0, PipeBytes);
        arguments[index] = path;

        ISystemAudioCapture? capture = null;
        try { capture = open(); }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or IOException) { }

        return new LoopbackFeed(pipe, capture);
    }

    /// <summary>
    /// ffmpeg'in stderr satirlarini alir; ilk goruntu karesinin damgasini tasiyan ilk satir ses
    /// izini o kareye hizalar (<see cref="LoopbackAudio.Lead"/>). Sonraki satirlar etkisizdir.
    /// </summary>
    public void Anchor(string? line)
    {
        if (LoopbackAudio.CaptureStart(line) is { } start)
            Interlocked.CompareExchange(ref _captureStart, start.Ticks, 0);
    }

    public void Dispose()
    {
        try { _cancel.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private async Task PumpAsync()
    {
        try
        {
            var ct = _cancel.Token;
            await _pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);

            var buffer = new byte[Silence.Length];
            for (var i = 0; i < StaleReads && _capture is not null && _capture.Read(buffer) > 0; i++) { }

            var connected = DateTime.UtcNow;
            var clock = Stopwatch.StartNew();
            var timeline = new LoopbackTimeline();
            while (!ct.IsCancellationRequested)
            {
                var start = Interlocked.Read(ref _captureStart);
                var lead = LoopbackAudio.Lead(start == 0 ? null : new DateTime(start, DateTimeKind.Utc), connected);
                var frames = (_capture?.Read(buffer) ?? 0) / LoopbackAudio.BytesPerFrame;
                var step = timeline.Next(clock.Elapsed + lead, frames);

                if (step.SilenceFrames > 0)
                    await _pipe.WriteAsync(Silence.AsMemory(0, step.SilenceFrames * LoopbackAudio.BytesPerFrame), ct).ConfigureAwait(false);

                var kept = frames - step.SkipFrames;
                if (kept > 0)
                    await _pipe.WriteAsync(
                        buffer.AsMemory(step.SkipFrames * LoopbackAudio.BytesPerFrame, kept * LoopbackAudio.BytesPerFrame), ct).ConfigureAwait(false);

                await Task.Delay(TickMs, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
        finally
        {
            try { _pipe.Dispose(); }
            catch (IOException) { }

            _capture?.Dispose();
        }
    }
}
