using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.Core.Editing;
using VidShrink.Player;

namespace VidShrink.App.Playback;

internal sealed class MusicPreview : IDisposable
{
    internal const double DriftSeconds = 0.25;

    private readonly Func<PlaybackOptions, IPlaybackEngine> _create;
    private IPlaybackEngine? _engine;
    private string? _path;
    private string? _failed;
    private Task _opening = Task.CompletedTask;
    private Task _seeking = Task.CompletedTask;
    private double? _level;
    private double _volume = 100;
    private bool _muted;
    private bool _playing;
    private int _seeks;
    private int _generation;
    private bool _disposed;

    internal MusicPreview()
        : this(options => new MpvEngine(options))
    {
    }

    internal MusicPreview(Func<PlaybackOptions, IPlaybackEngine> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        _create = create;
    }

    internal IPlaybackEngine? Engine => Volatile.Read(ref _engine);

    internal string? Path => _path;

    internal Task Opening => _opening;

    internal bool Playing => _playing;

    internal int Seeks => _seeks;

    internal static string LevelFilter(double levelDb)
        => "volume=" + levelDb.ToString("0.0", CultureInfo.InvariantCulture) + "dB";

    internal static double? Offset(EditMusic music, long timeline)
    {
        var local = timeline - music.Start;
        return local >= 0 && local < music.Duration ? EditTime.ToSeconds(local) : null;
    }

    internal void Sync(EditMusic? music, long timeline, bool playing, double rate, double volume, bool muted)
    {
        if (_disposed) return;
        if (music is null)
        {
            Close();
            return;
        }

        if (_path is not null && !string.Equals(music.Path, _path, StringComparison.Ordinal)) Close();

        var target = Offset(music, timeline);
        if (!playing || target is not { } at || !double.IsFinite(rate) || rate <= 0)
        {
            Pause();
            return;
        }

        if (_path is null)
        {
            if (string.Equals(music.Path, _failed, StringComparison.Ordinal)) return;
            _path = music.Path;
            _opening = OpenAsync(music.Path, ++_generation);
            return;
        }

        if (Engine is not { } engine) return;

        if (_level != music.LevelDb)
        {
            _level = music.LevelDb;
            engine.SetEditTimed(null, LevelFilter(music.LevelDb));
        }

        if (Math.Abs(_volume - volume) > 1e-9)
        {
            _volume = volume;
            engine.SetVolume(volume);
        }

        if (_muted != muted)
        {
            _muted = muted;
            engine.SetMuted(muted);
        }

        if (Math.Abs(engine.Speed - rate) > 1e-9) engine.SetSpeed(rate);
        if (!_playing || (_seeking.IsCompleted && Math.Abs(engine.PositionSeconds - at) > DriftSeconds))
        {
            _seeking = engine.SeekAsync(at, SeekPrecision.Exact);
            _seeks++;
        }

        if (!_playing)
        {
            engine.Play();
            _playing = true;
        }
    }

    internal void Pause()
    {
        if (!_playing) return;
        _playing = false;
        try { Engine?.Pause(); } catch (ObjectDisposedException) { }
    }

    internal void Close()
    {
        _generation++;
        var engine = Interlocked.Exchange(ref _engine, null);
        _path = null;
        _level = null;
        _volume = 100;
        _muted = false;
        _playing = false;
        _seeking = Task.CompletedTask;
        try { engine?.Dispose(); } catch (ObjectDisposedException) { }
    }

    private async Task OpenAsync(string path, int generation)
    {
        IPlaybackEngine? engine = null;
        try
        {
            engine = await Task.Run(() => _create(new PlaybackOptions { Video = false })).ConfigureAwait(true);
            await engine.OpenAsync(path).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or PlaybackEngineUnavailableException or UnauthorizedAccessException or TimeoutException or OperationCanceledException)
        {
            engine?.Dispose();
            if (generation == _generation)
            {
                _failed = path;
                _path = null;
            }

            return;
        }

        if (_disposed || generation != _generation || !engine.HasAudio)
        {
            if (generation == _generation && !engine.HasAudio)
            {
                _failed = path;
                _path = null;
            }

            engine.Dispose();
            return;
        }

        Volatile.Write(ref _engine, engine);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
    }
}
