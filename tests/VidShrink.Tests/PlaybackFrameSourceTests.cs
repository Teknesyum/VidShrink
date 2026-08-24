using VidShrink.Core.Playback;
using VidShrink.Ffmpeg;
using VidShrink.Ffmpeg.Playback;
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

    private static ComparisonFrameRequest Request(string left = "left.mp4", string right = "right.mp4") => new()
    {
        LeftPath = left,
        RightPath = right,
        PanelWidth = 1920,
        PanelHeight = 1080,
        Fps = 60
    };

    // --- ComparisonGraph -----------------------------------------------------------------

    [Fact]
    public void Filter_yan_yana_iki_paneli_tek_cikisa_bagliyor()
    {
        var filter = ComparisonGraph.BuildFilter(1920, 1080, 60);

        Assert.Equal(
            "[0:v]fps=60,scale=1920:1080[l];[1:v]fps=60,scale=1920:1080[r];[l][r]hstack=inputs=2[v]",
            filter);
    }

    [Fact]
    public void Filter_panel_olcusunu_kullanir_kaynak_cozunurlugunu_degil()
    {
        var filter = ComparisonGraph.BuildFilter(640, 360, 30);

        Assert.Contains("scale=640:360", filter);
        Assert.Contains("fps=30", filter);
        Assert.Equal(2, filter.Split("scale=640:360").Length - 1);
    }

    [Fact]
    public void Dosya_yolu_filtre_metnine_girmez_ayri_arguman_olarak_gecer()
    {
        const string awkward = @"C:\Videolar\bir 'iki': uc\klip;dosya[1].mp4";
        var args = ComparisonGraph.BuildArguments(Request(awkward, awkward));

        var filterIndex = args.ToList().IndexOf("-filter_complex");
        Assert.True(filterIndex >= 0);
        var filter = args[filterIndex + 1];

        Assert.DoesNotContain("Videolar", filter);
        Assert.DoesNotContain("klip", filter);

        // Yol tam olarak, bozulmadan, kendi argumaninda duruyor.
        Assert.Equal(2, args.Count(a => a == awkward));
        foreach (var i in Enumerable.Range(0, args.Count).Where(i => args[i] == awkward))
            Assert.Equal("-i", args[i - 1]);
    }

    [Fact]
    public void Kacis_filtre_ozel_karakterlerini_ters_bolu_ile_korur()
    {
        var escaped = ComparisonGraph.EscapeFilterValue(@"a'b:c,d[e]f;g=h\i");

        Assert.Equal(@"a\'b\:c\,d\[e\]f\;g\=h\\i", escaped);
    }

    [Fact]
    public void Kacis_sade_metni_degistirmez()
    {
        Assert.Equal("klip1.mp4", ComparisonGraph.EscapeFilterValue("klip1.mp4"));
    }

    [Fact]
    public void Argumanlar_ham_bgra_boruya_yaziyor()
    {
        var args = ComparisonGraph.BuildArguments(Request());

        Assert.Equal("-", args[^1]);
        Assert.Contains("rawvideo", args);
        Assert.Contains("bgra", args);
        Assert.Contains("-map", args);
        Assert.Contains("[v]", args);
    }

    [Fact]
    public void Atlama_konumu_her_iki_girdiye_de_uygulanir()
    {
        var args = ComparisonGraph.BuildArguments(Request() with { Position = TimeSpan.FromSeconds(12.5) });

        Assert.Equal(2, args.Count(a => a == "-ss"));
        Assert.Equal(2, args.Count(a => a == "12.5"));
    }

    [Fact]
    public void Gercek_zamanli_kapaliyken_re_verilmez()
    {
        var args = ComparisonGraph.BuildArguments(Request() with { Realtime = false });

        Assert.DoesNotContain("-re", args);
    }

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

    // --- Kare tamponunun parca parca doldurulmasi -----------------------------------------

    private sealed class ChunkRecordingStream : Stream
    {
        private readonly byte[] _data;
        private readonly int _maxPerRead;
        private int _position;

        public ChunkRecordingStream(byte[] data, int maxPerRead)
        {
            _data = data;
            _maxPerRead = maxPerRead;
        }

        public List<int> Requested { get; } = new();

        public override int Read(byte[] buffer, int offset, int count)
        {
            Requested.Add(count);
            var n = Math.Min(Math.Min(count, _maxPerRead), _data.Length - _position);
            if (n <= 0) return 0;
            Array.Copy(_data, _position, buffer, offset, n);
            _position += n;
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _data.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public void Kare_tamponu_64_kb_parcalardan_toplanir()
    {
        // T33/P8: kareyi tek okumada istemek 70,9 fps, 64 KB parcalarla toplamak 148,0 fps.
        Assert.Equal(64 * 1024, PipeComparisonFrameSource.ChunkBytes);

        const int frameBytes = 300 * 1024;
        var payload = new byte[frameBytes];
        for (var i = 0; i < frameBytes; i++) payload[i] = (byte)(i % 251);

        var stream = new ChunkRecordingStream(payload, 7000);
        var buffer = new byte[frameBytes];

        var read = PipeComparisonFrameSource.FillFrame(
            stream, buffer, frameBytes, PipeComparisonFrameSource.ChunkBytes);

        Assert.Equal(frameBytes, read);
        Assert.Equal(payload, buffer);
        Assert.True(stream.Requested.Count > 1, "Kare tek okumada istenmemeli.");
        Assert.All(stream.Requested, count => Assert.True(count <= PipeComparisonFrameSource.ChunkBytes));
    }

    [Fact]
    public void Yarim_kalan_kare_okunan_bayt_sayisini_doner()
    {
        var payload = new byte[1000];
        var stream = new ChunkRecordingStream(payload, 256);
        var buffer = new byte[4096];

        var read = PipeComparisonFrameSource.FillFrame(stream, buffer, 4096, 1024);

        Assert.Equal(1000, read);
    }

    // --- Canli ---------------------------------------------------------------------------

    [LivePlaybackFact]
    public async Task Canli_kaynak_iki_paneli_besliyor()
    {
        var path = Environment.GetEnvironmentVariable("VIDSHRINK_LIVE_SOURCE")!;

        using var source = new PipeComparisonFrameSource();
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

        using var source = new PipeComparisonFrameSource();
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
