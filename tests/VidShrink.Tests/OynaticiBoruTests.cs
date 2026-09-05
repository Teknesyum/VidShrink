using VidShrink.Ffmpeg;
using VidShrink.Ffmpeg.Playback;
using Xunit;

namespace VidShrink.Tests;

public sealed class FfmpegAvailableFactAttribute : FactAttribute
{
    public FfmpegAvailableFactAttribute()
    {
        if (!ToolLocator.IsAvailable(out var missing))
            Skip = $"{missing} bulunamadi, T175 boru testleri atlandi.";
    }
}

public sealed class SentetikKlipFixture : IAsyncLifetime
{
    public string? ClipPath { get; private set; }
    public string? SessizClipPath { get; private set; }
    public string? UzunSessizKlipPath { get; private set; }

    private string? _dir;

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "VidShrink.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? AppContext.BaseDirectory;
    }

    public async Task InitializeAsync()
    {
        if (!ToolLocator.IsAvailable(out _)) return;

        var dir = Path.Combine(FindRepoRoot(), ".calisma", "T175", "test-klip");
        Directory.CreateDirectory(dir);
        _dir = dir;

        ClipPath = Path.Combine(dir, "sentetik-ses.mkv");
        SessizClipPath = Path.Combine(dir, "sentetik-sessiz.mkv");
        UzunSessizKlipPath = Path.Combine(dir, "sentetik-uzun-sessiz.mkv");

        await RunFfmpegAsync(new[]
        {
            "-y", "-f", "lavfi", "-i", "testsrc=size=160x90:rate=30:duration=8",
            "-f", "lavfi", "-i", "sine=frequency=1000:duration=8",
            "-force_key_frames", "expr:gte(t,n_forced*1)",
            "-pix_fmt", "yuv420p", "-c:v", "libx264", "-preset", "ultrafast",
            "-c:a", "aac", "-shortest", ClipPath
        });

        await RunFfmpegAsync(new[]
        {
            "-y", "-f", "lavfi", "-i", "testsrc=size=160x90:rate=30:duration=4",
            "-force_key_frames", "expr:gte(t,n_forced*1)",
            "-pix_fmt", "yuv420p", "-c:v", "libx264", "-preset", "ultrafast",
            "-an", SessizClipPath
        });

        // Buyuk cozunurluk + her kare anahtar: skip_frame nokey ile cikan ham veri
        // OS borusunun tampon boyutunu asar, boru surecin canli kalmasini garantiler
        // (Surec_disaridan_oldurulunce testi icin — kucuk klipte surec Kill’den once biter).
        await RunFfmpegAsync(new[]
        {
            "-y", "-f", "lavfi", "-i", "testsrc=size=640x480:rate=30:duration=6",
            "-force_key_frames", "expr:gte(t,n_forced*0.1)",
            "-pix_fmt", "yuv420p", "-c:v", "libx264", "-preset", "ultrafast",
            "-an", UzunSessizKlipPath
        });
    }

    public Task DisposeAsync()
    {
        if (_dir is not null && Directory.Exists(_dir))
        {
            try { Directory.Delete(_dir, recursive: true); } catch { }
        }
        return Task.CompletedTask;
    }

    private static async Task RunFfmpegAsync(string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(ToolLocator.Ffmpeg)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var process = new System.Diagnostics.Process { StartInfo = psi };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await stdoutTask;
        await stderrTask;
        await process.WaitForExitAsync();
    }
}

public sealed class OynaticiBoruTests_PlaybackClock
{
    [Fact]
    public void Baslamamis_saat_baz_saniyesinde_durur()
    {
        var clock = new PlaybackClock();
        Assert.Equal(0, clock.PositionSeconds, 3);
        Assert.False(clock.IsRunning);
    }

    [Fact]
    public void Baslatilinca_zaman_ilerler()
    {
        var clock = new PlaybackClock();
        clock.Start(10);
        Thread.Sleep(50);
        Assert.True(clock.PositionSeconds >= 10.03);
        Assert.True(clock.IsRunning);
    }

    [Fact]
    public void Duraklat_konumu_donduruyor_ilerletmiyor()
    {
        var clock = new PlaybackClock();
        clock.Start(0);
        Thread.Sleep(30);
        clock.Pause();
        var frozen = clock.PositionSeconds;
        Thread.Sleep(30);
        Assert.Equal(frozen, clock.PositionSeconds, 3);
        Assert.False(clock.IsRunning);
    }

    [Fact]
    public void Atlama_calisirken_de_calismazken_de_konumu_degistirir()
    {
        var clock = new PlaybackClock();
        clock.Start(0);
        clock.Seek(42);
        Assert.True(clock.PositionSeconds >= 42);

        clock.Pause();
        clock.Seek(7);
        Assert.Equal(7, clock.PositionSeconds, 3);
    }

    [Fact]
    public void Hiz_carpani_ilerlemeyi_olcekler()
    {
        var clock = new PlaybackClock();
        clock.Start(0);
        clock.Rate = 2.0;
        Thread.Sleep(50);
        Assert.True(clock.PositionSeconds >= 0.09);
    }
}

public sealed class OynaticiBoruTests_AudioSink
{
    [Fact]
    public void Ses_yoksa_butun_cagrilar_sessizce_no_op()
    {
        using var sink = new AudioSink(hasAudio: false);
        Assert.False(sink.HasAudio);

        sink.Write(new byte[128]);
        sink.Play();
        sink.Pause();
        sink.Reset();

        Assert.Equal(0, sink.PositionSeconds, 3);
    }

    [Fact]
    public void Cihaz_acilamasa_bile_cokmez()
    {
        using var sink = new AudioSink(hasAudio: true);
        sink.Write(new byte[256]);
        sink.Dispose();
    }

    [Fact]
    public void BytesToSeconds_format_hizinda_dogru_donusum_yapar()
    {
        var birSaniyelikBayt = (long)AudioSink.Format.AverageBytesPerSecond;

        Assert.Equal(1.0, AudioSink.BytesToSeconds(birSaniyelikBayt), 6);
        Assert.Equal(2.0, AudioSink.BytesToSeconds(birSaniyelikBayt * 2), 6);
        Assert.NotEqual(1.15, AudioSink.BytesToSeconds(birSaniyelikBayt), 2);
    }
}

public sealed class OynaticiBoruTests_DecoderPipe : IClassFixture<SentetikKlipFixture>
{
    private readonly SentetikKlipFixture _fixture;

    public OynaticiBoruTests_DecoderPipe(SentetikKlipFixture fixture) => _fixture = fixture;

    private string SesliKlip => _fixture.ClipPath ?? throw new InvalidOperationException("ffmpeg yok");
    private string SessizKlip => _fixture.SessizClipPath ?? throw new InvalidOperationException("ffmpeg yok");
    private string UzunSessizKlip => _fixture.UzunSessizKlipPath ?? throw new InvalidOperationException("ffmpeg yok");

    [FfmpegAvailableFact]
    public async Task Acilinca_suresi_ve_ses_varligi_dogru_okunur()
    {
        using var pipe = new DecoderPipe();
        await pipe.OpenAsync(SesliKlip);

        Assert.True(pipe.HasAudio);
        Assert.InRange(pipe.DurationSeconds, 7.5, 8.5);
    }

    [FfmpegAvailableFact]
    public async Task Sessiz_kaynakta_HasAudio_false_ve_SeekAudio_cokmez()
    {
        using var pipe = new DecoderPipe();
        await pipe.OpenAsync(SessizKlip);

        Assert.False(pipe.HasAudio);
        pipe.SeekAudio(1.0);
    }

    [FfmpegAvailableFact]
    public async Task Ilk_arama_tek_surec_baslatir_ve_kare_teslim_eder()
    {
        using var pipe = new DecoderPipe();
        await pipe.OpenAsync(SessizKlip);

        var frame = await pipe.SeekAsync(0);

        Assert.NotNull(frame);
        Assert.Equal(160, frame!.Width);
        Assert.Equal(90, frame.Height);
        Assert.Equal(1, pipe.ProcessesStarted);
    }

    [FfmpegAvailableFact]
    public async Task Yakin_ileri_aramalar_sureci_yeniden_baslatmaz()
    {
        using var pipe = new DecoderPipe();
        await pipe.OpenAsync(SesliKlip);

        await pipe.SeekAsync(0);
        var startedAfterFirst = pipe.ProcessesStarted;

        await pipe.SeekAsync(1);
        await pipe.SeekAsync(2);
        await pipe.SeekAsync(3);

        Assert.Equal(startedAfterFirst, pipe.ProcessesStarted);
    }

    [FfmpegAvailableFact]
    public async Task Onbellekteki_hedefe_geri_donus_de_surec_baslatmaz()
    {
        using var pipe = new DecoderPipe();
        await pipe.OpenAsync(SesliKlip);

        await pipe.SeekAsync(0);
        await pipe.SeekAsync(3);
        var started = pipe.ProcessesStarted;

        var back = await pipe.SeekAsync(1);

        Assert.NotNull(back);
        Assert.Equal(started, pipe.ProcessesStarted);
    }

    [FfmpegAvailableFact]
    public async Task Onbellek_disina_dusen_uzak_geri_atlama_sureci_yeniden_baslatir()
    {
        using var pipe = new DecoderPipe(cacheByteCeiling: 160 * 90 * 4 * 2);
        await pipe.OpenAsync(SesliKlip);

        await pipe.SeekAsync(0);
        await pipe.SeekAsync(1);
        await pipe.SeekAsync(2);
        await pipe.SeekAsync(3);
        await pipe.SeekAsync(4);
        var startedAfterForward = pipe.ProcessesStarted;

        var frame = await pipe.SeekAsync(0);

        Assert.NotNull(frame);
        Assert.True(pipe.ProcessesStarted > startedAfterForward);
    }

    [FfmpegAvailableFact]
    public async Task Surec_disaridan_oldurulunce_boru_Faulted_yayar_ve_kendini_kurar()
    {
        using var pipe = new DecoderPipe();
        await pipe.OpenAsync(UzunSessizKlip);

        var faulted = new TaskCompletionSource<PipeFault>();
        pipe.Faulted += (_, f) => faulted.TrySetResult(f);

        await pipe.SeekAsync(0);
        var startedBeforeCrash = pipe.ProcessesStarted;

        var killed = pipe.TestOnly_KillVideoProcess();
        Assert.True(killed);

        var completed = await Task.WhenAny(faulted.Task, Task.Delay(3000));
        Assert.Same(faulted.Task, completed);

        var frame = await pipe.SeekAsync(2);
        Assert.NotNull(frame);
        Assert.True(pipe.ProcessesStarted > startedBeforeCrash);
    }

    [FfmpegAvailableFact]
    public async Task Dispose_iki_kere_cagrilinca_da_cokmez()
    {
        var pipe = new DecoderPipe();
        await pipe.OpenAsync(SessizKlip);
        await pipe.SeekAsync(0);

        pipe.Dispose();
        pipe.Dispose();
    }
}
