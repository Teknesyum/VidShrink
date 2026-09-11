using VidShrink.Core.Playback;
using VidShrink.Ffmpeg;
using VidShrink.App.Playback;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>ffmpeg gerektiren oynatma olcumleri. Kapi kapaliyken <c>Skipped</c> doner.</summary>
public sealed class LivePlaybackFactAttribute : FactAttribute
{
    public LivePlaybackFactAttribute()
    {
        var source = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_SOURCE");
        if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
            Skip = "VIDSHRINK_LIVE_SOURCE does not point at an existing file, so the live comparison source was not run.";
        else if (!ToolLocator.IsAvailable(out _))
            Skip = "ffmpeg was not found, so the live comparison source was not run.";
    }
}

public sealed class PlaybackFrameSourceTests
{
    private readonly ITestOutputHelper _output;

    public PlaybackFrameSourceTests(ITestOutputHelper output) => _output = output;

    // --- FramePool -----------------------------------------------------------------------

    [Fact]
    public void Havuz_iade_edilen_tamponu_geri_verir()
    {
        var pool = new FramePool(3, 1024);

        Assert.True(pool.TryRent(out var first));
        pool.Return(first);
        Assert.True(pool.TryRent(out var again));

        Assert.Same(first, again);
        Assert.Same(first.Buffer, again.Buffer);
    }

    [Fact]
    public void Havuz_bosken_ayirmaz()
    {
        var pool = new FramePool(3, 1024);
        var held = new List<PlaybackFrame>();

        for (var i = 0; i < 3; i++)
        {
            Assert.True(pool.TryRent(out var frame));
            held.Add(frame);
        }

        Assert.False(pool.TryRent(out _));
        Assert.Equal(3, pool.Allocations);

        // Iade edip yeniden kiralamak da yeni ayirma yapmaz.
        foreach (var frame in held) pool.Return(frame);
        for (var i = 0; i < 3; i++) Assert.True(pool.TryRent(out _));
        Assert.Equal(3, pool.Allocations);
    }

    [Fact]
    public void Havuz_yabanci_boydaki_tamponu_kabul_etmez()
    {
        var pool = new FramePool(2, 1024);

        Assert.Throws<ArgumentException>(() => pool.Return(new PlaybackFrame(new byte[512])));
    }

    // --- FrameRing -----------------------------------------------------------------------

    [Fact]
    public void Halka_en_az_uc_gozlu_olmali()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameRing(2));
        Assert.Equal(3, FrameRing.MinimumCapacity);
    }

    [Fact]
    public void Halka_doldugunda_en_eski_kare_duser()
    {
        var pool = new FramePool(8, 16);
        var ring = new FrameRing(3, pool);
        var frames = new List<PlaybackFrame>();

        for (var i = 0; i < 4; i++)
        {
            Assert.True(pool.TryRent(out var frame));
            frame.Describe(2, 2, 1, TimeSpan.FromSeconds(i), i);
            frames.Add(frame);
            ring.Publish(frame);
        }

        Assert.Equal(1, ring.Dropped);
        Assert.Equal(3, ring.Count);

        // Dusen kare (sequence 0) havuza dondu, cope gitmedi.
        Assert.True(pool.TryRent(out var recycled));
        Assert.Same(frames[0], recycled);
    }

    [Fact]
    public void Halka_bekleyen_kareyi_sirayla_verir()
    {
        var pool = new FramePool(8, 16);
        var ring = new FrameRing(3, pool);

        for (var i = 0; i < 3; i++)
        {
            Assert.True(pool.TryRent(out var frame));
            frame.Describe(2, 2, 1, TimeSpan.FromSeconds(i), i);
            ring.Publish(frame);
        }

        for (var i = 0; i < 3; i++)
        {
            Assert.True(ring.TryTake(out var taken));
            Assert.Equal(i, taken.Sequence);
            pool.Return(taken);
        }

        Assert.Equal(0, ring.Dropped);
        Assert.Equal(0, ring.Count);
        Assert.False(ring.TryTake(out _));
    }

    [Fact]
    public void Halka_dolunca_en_eskiyi_dusurur_gecikme_tavanli_kalir()
    {
        var pool = new FramePool(8, 16);
        var ring = new FrameRing(3, pool);

        for (var i = 0; i < 5; i++)
        {
            Assert.True(pool.TryRent(out var frame));
            frame.Describe(2, 2, 1, TimeSpan.FromSeconds(i), i);
            ring.Publish(frame);
        }

        Assert.Equal(2, ring.Dropped);
        Assert.Equal(3, ring.Count);
        Assert.True(ring.TryTake(out var oldest));
        Assert.Equal(2, oldest.Sequence);
    }

    [Fact]
    public void Halka_uretici_yetisirse_kare_dusurmez()
    {
        var pool = new FramePool(8, 16);
        var ring = new FrameRing(3, pool);

        for (var i = 0; i < 10; i++)
        {
            Assert.True(pool.TryRent(out var frame));
            frame.Describe(2, 2, 1, TimeSpan.FromSeconds(i), i);
            ring.Publish(frame);
            Assert.True(ring.TryTake(out var taken));
            Assert.Equal(i, taken.Sequence);
            pool.Return(taken);
        }

        Assert.Equal(0, ring.Dropped);
    }

    [Fact]
    public void Halka_en_eskiyi_uretici_icin_geri_kazandirir()
    {
        var pool = new FramePool(8, 16);
        var ring = new FrameRing(3, pool);

        for (var i = 0; i < 2; i++)
        {
            Assert.True(pool.TryRent(out var frame));
            frame.Describe(2, 2, 1, TimeSpan.FromSeconds(i), i);
            ring.Publish(frame);
        }

        Assert.True(ring.TryEvictOldest(out var oldest));
        Assert.Equal(0, oldest.Sequence);
        Assert.Equal(1, ring.Dropped);
        Assert.Equal(1, ring.Count);
    }

    // --- Canli ---------------------------------------------------------------------------

    [LivePlaybackFact]
    public async Task Canli_kaynak_iki_paneli_besliyor()
    {
        var path = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_SOURCE")!;

        using var source = new EngineComparisonFrameSource();
        await source.StartAsync(new ComparisonFrameRequest
        {
            LeftPath = path,
            RightPath = path,
            PanelWidth = 640,
            PanelHeight = 360,
            Fps = 30,
            Realtime = false
        });

        Assert.NotEqual(ComparisonSourceState.Kullanilamiyor, source.Status.State);

        var taken = 0;
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (taken < 10 && DateTime.UtcNow < deadline)
        {
            if (source.TryTake(out var frame))
            {
                Assert.Equal(1280, frame.Width);
                Assert.Equal(360, frame.Height);
                Assert.Equal(640, frame.SplitX);
                Assert.Equal(1280 * 360 * 4, frame.ByteLength);
                source.Return(frame);
                taken++;
            }
            else
            {
                await Task.Delay(5);
            }
        }

        var status = source.Status;
        _output.WriteLine($"uretilen={status.ProducedFrames} dusen={status.DroppedFrames} fps={status.FeedFps:0.0} havuz={status.PoolAllocations}");

        Assert.True(taken >= 10, $"10 kare beklendi, {taken} alindi.");
        Assert.Equal(0, status.ReadErrors);
        await source.StopAsync();
    }

    [LivePlaybackFact]
    public async Task Duraklatma_sureci_oldurmez()
    {
        var path = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_SOURCE")!;

        using var source = new EngineComparisonFrameSource();
        await source.StartAsync(new ComparisonFrameRequest
        {
            LeftPath = path,
            RightPath = path,
            PanelWidth = 320,
            PanelHeight = 180,
            Fps = 30,
            Realtime = true,
            Loop = true
        });

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (source.Status.ProducedFrames == 0 && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.True(source.Status.ProducedFrames > 0);

        source.Pause();
        Assert.Equal(ComparisonSourceState.Duraklatildi, source.Status.State);
        while (source.TryTake(out var stale)) source.Return(stale);

        var afterPause = source.Status.ProducedFrames;
        await Task.Delay(700);
        Assert.Equal(afterPause, source.Status.ProducedFrames);

        source.Play();
        deadline = DateTime.UtcNow.AddSeconds(10);
        while (source.Status.ProducedFrames == afterPause && DateTime.UtcNow < deadline) await Task.Delay(10);

        Assert.True(source.Status.ProducedFrames > afterPause, "Duraklatma surecin borusunu kapatmis.");
        await source.StopAsync();
    }
}
