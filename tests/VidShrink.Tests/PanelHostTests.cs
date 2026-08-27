using VidShrink.App;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Core.Playback;

namespace VidShrink.Tests;

/// <summary>
/// Panelin sag yarisinin uc durumu ve rozetin kosulu. Pencere acilmiyor: host
/// <see cref="AppHost"/> is parcaciginda kuruluyor, kodlama ise arayuz kuyrugu olmadan
/// bekleniyor. Bu is parcaciginda ileti dongusu yok, dolayisiyla <c>DispatcherTimer</c>
/// atesenmez; gecikmenin arkasindaki is <see cref="PanelHost.LoadClipAsync"/> ile
/// dogrudan kosturuluyor.
/// </summary>
public sealed class PanelHostTests : IClassFixture<SegmentClips>
{
    private readonly SegmentClips _clips;

    public PanelHostTests(SegmentClips clips) => _clips = clips;

    private static MediaInfo Source(string path, double duration = 12) => new()
    {
        FilePath = path,
        FileSizeBytes = 1_000_000,
        DurationSeconds = duration,
        Width = 640,
        Height = 360,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 2_000_000
    };

    private static EncodePlan TwoPassPlan() => new()
    {
        Codec = "libx264",
        Mode = "2pass",
        VideoBitrateK = 300,
        Width = 320,
        Height = 180,
        Fps = 30,
        Preset = "veryfast",
        PixelFormat = "yuv420p",
        AudioCodec = null
    };

    private static EncodePlan CrfPlan()
    {
        var plan = TwoPassPlan();
        plan.Mode = "crf";
        plan.Crf = 26;
        return plan;
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

    private string Temp()
    {
        var dir = Path.Combine(_clips.Directory, "host-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static PanelHost Host(SegmentEncoder encoder)
        => AppHost.Run(() => new PanelHost(new ComparisonPanel(), () => new SessizKaynak(), encoder));

    [Fact]
    public void Tam_cikti_varken_parca_kodlanmaz()
    {
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var host = Host(encoder);

        AppHost.Run(() =>
        {
            host.SetFiles(_clips.Kaynak, _clips.Buyuk, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.SetPlan(Source(_clips.Kaynak), TwoPassPlan(), null);
        });

        Assert.Equal(0, encoder.StartedEncodes);
        Assert.Null(host.ActiveClip);
        Assert.Null(host.ApproximateBadge);
        AppHost.Run(host.Dispose);
    }

    [Fact]
    public void Kaynak_yokken_plan_kodlama_baslatmaz()
    {
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var host = Host(encoder);

        AppHost.Run(() => host.SetPlan(null, null, null));

        Assert.Equal(0, encoder.StartedEncodes);
        Assert.Null(host.ActiveClip);
        AppHost.Run(host.Dispose);
    }

    /// <summary>K2: parca hazir olunca sag yari o parcadan beslenir, perde kalkar.</summary>
    [FfmpegFact]
    public async Task Parca_hazir_olunca_sag_yari_parcayi_gosterir()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var host = Host(encoder);
        AppHost.Run(() =>
        {
            host.SetFiles(_clips.Kaynak, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.SetPlan(Source(_clips.Kaynak), TwoPassPlan(), null);
        });

        await host.LoadClipAsync(4);

        var clip = host.ActiveClip;
        Assert.NotNull(clip);
        Assert.Equal(4, clip!.StartSeconds, 3);
        Assert.NotEqual(clip.SourcePath, clip.EncodedPath);
        AppHost.Run(host.Dispose);
    }

    /// <summary>K4/K7: rozet T47'nin alanindan surulur ve ham ondalik degeri basmaz.</summary>
    [FfmpegFact]
    public async Task Rozet_yaklasik_alanindan_surulur()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var host = Host(encoder);
        AppHost.Run(() =>
        {
            host.SetFiles(_clips.Kaynak, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.SetPlan(Source(_clips.Kaynak), TwoPassPlan(), null);
        });

        await host.LoadClipAsync(4);

        var clip = host.ActiveClip!;
        Assert.True(clip.IsApproximate);
        var badge = host.ApproximateBadge;
        Assert.NotNull(badge);
        Assert.Contains($"CRF {clip.Crf}", badge);
        Assert.DoesNotContain(",", badge);
        Assert.DoesNotContain(".", badge);
        AppHost.Run(host.Dispose);
    }

    /// <summary>Plan zaten kalite tasiyorsa parca kesindir; rozet gorunmez.</summary>
    [FfmpegFact]
    public async Task Kesin_parcada_rozet_yok()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var host = Host(encoder);
        AppHost.Run(() =>
        {
            host.SetFiles(_clips.Kaynak, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.SetPlan(Source(_clips.Kaynak), CrfPlan(), null);
        });

        await host.LoadClipAsync(4);

        Assert.False(host.ActiveClip!.IsApproximate);
        Assert.Null(host.ApproximateBadge);
        AppHost.Run(host.Dispose);
    }

    /// <summary>K6: bozuk kaynak cokme uretmez, hata kaydedilir, sag yari bos kalir.</summary>
    [FfmpegFact]
    public async Task Bozuk_kaynakta_panel_cokmez()
    {
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var host = Host(encoder);
        AppHost.Run(() =>
        {
            host.SetFiles(_clips.Bozuk, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.SetPlan(Source(_clips.Bozuk), TwoPassPlan(), null);
        });

        await host.LoadClipAsync(0);

        Assert.Null(host.ActiveClip);
        Assert.False(string.IsNullOrWhiteSpace(encoder.LastError));
        AppHost.Run(host.Dispose);
    }

    /// <summary>K5: panel kapaninca gecici dosya kalmaz.</summary>
    [FfmpegFact]
    public async Task Panel_kapaninca_gecici_dosya_kalmaz()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        var encoder = new SegmentEncoder(dir);
        var host = Host(encoder);
        AppHost.Run(() =>
        {
            host.SetFiles(_clips.Kaynak, null, 16.0 / 9, TimeSpan.FromSeconds(12), 30);
            host.SetPlan(Source(_clips.Kaynak), TwoPassPlan(), null);
        });

        await host.LoadClipAsync(4);
        Assert.NotEmpty(Directory.GetFiles(dir, SegmentEncoder.TempPrefix + "*"));

        AppHost.Run(host.Dispose);

        Assert.Empty(Directory.GetFiles(dir, SegmentEncoder.TempPrefix + "*"));
    }
}
