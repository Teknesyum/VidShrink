using System;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.Ffmpeg.Playback;

namespace VidShrink.App.Playback;

/// <summary>
/// Onizlemenin ses yolu. <see cref="VidShrink.Ffmpeg.Playback.DecoderPipe"/> icindeki
/// PCM borusu ile <see cref="AudioSink"/> bastan sona yaziliydi; eksik olan tek sey
/// uygulamanin <see cref="DecoderPipe.AttachAudioSink"/> cagirmasiydi, bu sinif o
/// baglantiyi kuruyor.
///
/// Karsilastirma borusu (<c>PipeComparisonFrameSource</c>) yalniz ham video tasiyor ve
/// iki dosyayi yan yana diziyor; ses oradan cikamaz. Bu yuzden ses <b>kaynak dosyadan</b>
/// ayri bir borudan geliyor ve pencere basina bir kez o pencerenin anina atlaniyor.
///
/// Sessiz klipte <see cref="DecoderPipe.HasAudio"/> yanlis doner, kuyu sessiz kurulur ve
/// <see cref="DecoderPipe.SeekAudio"/> ilk satirinda geri doner: ses cikmaz, hata da olmaz.
/// </summary>
internal sealed class PreviewAudio : IDisposable
{
    private readonly object _gate = new();

    private DecoderPipe? _pipe;
    private AudioSink? _sink;
    private string? _path;
    private int _seeks;
    private bool _disposed;

    /// <summary>Bagli dosyada ses akisi var mi. Ses cihazindan bagimsiz: kaynagin kendi ozelligi.</summary>
    internal bool HasAudio
    {
        get { lock (_gate) return _pipe?.HasAudio == true; }
    }

    /// <summary>Kuyu boruya takildi mi — <see cref="DecoderPipe.AttachAudioSink"/> kostu mu.</summary>
    internal bool Attached
    {
        get { lock (_gate) return _sink is not null; }
    }

    /// <summary>Kac kez ses borusu bir ana atladi. Olcum bunu sayar.</summary>
    internal int Seeks => Volatile.Read(ref _seeks);

    /// <summary>Bagli dosya; hicbir sey bagli degilse <c>null</c>.</summary>
    internal string? Path
    {
        get { lock (_gate) return _path; }
    }

    /// <summary>
    /// Dosyayi baglar ve kuyuyu takar. Ayni dosya ikinci kez verilirse boru yeniden
    /// kurulmaz — her pencere yenilenmesinde ffmpeg acmak bosuna maliyettir.
    /// </summary>
    internal async Task AttachAsync(string path)
    {
        if (_disposed || string.IsNullOrWhiteSpace(path)) return;

        lock (_gate)
        {
            if (_pipe is not null && string.Equals(_path, path, StringComparison.Ordinal)) return;
        }

        Close();

        var pipe = new DecoderPipe();
        try
        {
            await pipe.OpenAsync(path).ConfigureAwait(false);
        }
        catch
        {
            pipe.Dispose();
            return;
        }

        var sink = new AudioSink(pipe.HasAudio);
        pipe.AttachAudioSink(sink);

        lock (_gate)
        {
            if (_disposed)
            {
                sink.Dispose();
                pipe.Dispose();
                return;
            }

            _pipe = pipe;
            _sink = sink;
            _path = path;
        }
    }

    /// <summary>Ses borusunu verilen ana tasir. Sessiz klipte hicbir sey yapmaz.</summary>
    internal void SeekTo(double atSeconds)
    {
        DecoderPipe? pipe;
        lock (_gate) pipe = _pipe;
        if (pipe is null || !pipe.HasAudio) return;

        pipe.SeekAudio(Math.Max(0, atSeconds));
        Interlocked.Increment(ref _seeks);
    }

    internal void Play()
    {
        AudioSink? sink;
        lock (_gate) sink = _sink;
        sink?.Play();
    }

    internal void Pause()
    {
        AudioSink? sink;
        lock (_gate) sink = _sink;
        sink?.Pause();
    }

    /// <summary>Boruyu ve kuyuyu birakir; nesne yeniden baglanabilir kalir.</summary>
    internal void Close()
    {
        DecoderPipe? pipe;
        AudioSink? sink;
        lock (_gate)
        {
            pipe = _pipe;
            sink = _sink;
            _pipe = null;
            _sink = null;
            _path = null;
        }

        try { sink?.Pause(); } catch { }
        try { pipe?.Dispose(); } catch { }
        try { sink?.Dispose(); } catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
    }
}
