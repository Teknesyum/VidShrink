using VidShrink.App.Playback;
using VidShrink.Core.Editing;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal sealed class YavasAramaMotoru : IPlaybackEngine
{
    private readonly Queue<TaskCompletionSource> _bekleyen = new();

    internal List<double> Aramalar { get; } = new();

    public string Name => "yavas-arama";

    public bool IsOpen => true;

    public double DurationSeconds => 0;

    public bool HasAudio => false;

    public bool IsPaused { get; private set; } = true;

    public bool EndReached => false;

    public double PositionSeconds { get; private set; }

    public double AudioVideoOffsetSeconds => 0;

    public long FramesRendered => 0;

    public double Speed { get; private set; } = 1;

    public event EventHandler<PlaybackFault>? Faulted { add { } remove { } }

    public Task OpenAsync(string path, CancellationToken ct = default) => Task.CompletedTask;

    public void Play() => IsPaused = false;

    public void Pause() => IsPaused = true;

    public void SetSpeed(double speed) => Speed = speed;

    public async Task<SeekResult> SeekAsync(double seconds, SeekPrecision precision, CancellationToken ct = default)
    {
        var kapi = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_bekleyen)
        {
            Aramalar.Add(seconds);
            _bekleyen.Enqueue(kapi);
        }

        await kapi.Task.ConfigureAwait(false);
        PositionSeconds = seconds;
        return new SeekResult(SeekOutcome.Shown, 0);
    }

    internal int Bekleyen
    {
        get
        {
            lock (_bekleyen) return _bekleyen.Count;
        }
    }

    internal void Bitir()
    {
        TaskCompletionSource kapi;
        lock (_bekleyen) kapi = _bekleyen.Dequeue();
        kapi.SetResult();
    }

    public bool TryCopyLatest(ref long seen, FrameCopy copy) => false;

    public void Dispose()
    {
    }
}

public sealed class DuzenleyiciSurtmeTests
{
    private const long Sn = EditTime.TicksPerSecond;

    private static async Task Bekle(Func<bool> kosul)
    {
        var sure = System.Diagnostics.Stopwatch.StartNew();
        while (!kosul())
        {
            Assert.True(sure.Elapsed < TimeSpan.FromSeconds(5), "zaman asimi");
            await Task.Delay(5);
        }
    }

    [Fact]
    public async Task SolaSurtmedeBirikenAramalarAtlanirVeImlecGeriKaymaz()
    {
        var onizleme = new EdlPreview(@"C:\v\a.mp4", EditTimeline.FromSource(10 * Sn));
        var motor = new YavasAramaMotoru();
        using var surucu = new EdlPreviewDriver(motor, onizleme, TimeSpan.FromHours(1));

        var gorevler = new List<Task> { surucu.SeekAsync(9 * Sn, SeekPrecision.Keyframe) };
        await Bekle(() => motor.Bekleyen == 1);
        for (var s = 8; s >= 1; s--) gorevler.Add(surucu.SeekAsync(s * Sn, SeekPrecision.Keyframe));
        gorevler.Add(surucu.SeekAsync(Sn, SeekPrecision.Exact));

        Assert.Equal(Sn, surucu.TimelinePosition);

        var konumlar = new List<long>();
        while (!gorevler.TrueForAll(g => g.IsCompleted))
        {
            if (motor.Bekleyen > 0) motor.Bitir();
            konumlar.Add(surucu.TimelinePosition);
            await Task.Delay(5);
        }

        await Task.WhenAll(gorevler);

        Assert.Equal(new[] { 9.0, 1.0 }, motor.Aramalar);
        Assert.All(konumlar, k => Assert.Equal(Sn, k));
        Assert.Equal(Sn, surucu.TimelinePosition);
    }
}
