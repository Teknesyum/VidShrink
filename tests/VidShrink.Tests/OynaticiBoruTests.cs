using System.Linq;
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

public sealed class SesCihaziVarFactAttribute : FactAttribute
{
    public SesCihaziVarFactAttribute()
    {
        if (!ToolLocator.IsAvailable(out var missing))
        {
            Skip = $"{missing} bulunamadi, T175 boru testleri atlandi.";
            return;
        }

        using var probe = new AudioSink(true);
        if (probe.DeviceFailure is { } failure)
            Skip = $"Calisan ses cihazi yok, ses-goruntu kaymasi olculemez: {failure.Tr}";
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

        await RunFfmpegAsync(new[]
        {
            "-y", "-f", "lavfi", "-i", "testsrc=size=1280x720:rate=30:duration=20",
            "-force_key_frames", "expr:gte(t,n_forced*0.05)",
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

    private const double IleriBaslangicSaniye = 1.0;

    private const double GeriHedefSaniye = 0.0;

    [FfmpegAvailableFact]
    public async Task Surec_disaridan_oldurulunce_boru_Faulted_yayar_ve_kendini_kurar()
    {
        using var pipe = new DecoderPipe();
        await pipe.OpenAsync(UzunSessizKlip);

        var faulted = new TaskCompletionSource<PipeFault>();
        pipe.Faulted += (_, f) => faulted.TrySetResult(f);

        await pipe.SeekAsync(IleriBaslangicSaniye);
        var startedBeforeCrash = pipe.ProcessesStarted;

        var killed = pipe.TestOnly_KillVideoProcess();
        Assert.True(killed, "oldurulecegi sirada kod cozucu surec zaten olmustu, olcu anlamsiz");

        var completed = await Task.WhenAny(faulted.Task, Task.Delay(3000));
        Assert.Same(faulted.Task, completed);

        Assert.False(
            pipe.TestOnly_CacheHasStampAt(GeriHedefSaniye),
            $"{GeriHedefSaniye:0.##} sn onbellege girmis; kurtarma olcusu onbellek isabetiyle karisir");

        var frame = await pipe.SeekAsync(GeriHedefSaniye);

        Assert.NotNull(frame);
        Assert.True(
            pipe.ProcessesStarted > startedBeforeCrash,
            $"boru kendini kurmadi: onbellekte olmayan {GeriHedefSaniye:0.##} sn icin yeni surec baslatilmadi");
    }

    [FfmpegAvailableFact]
    public void KillTree_dondugunde_ffmpeg_sureci_gercekten_olmustur()
    {
        var psi = new System.Diagnostics.ProcessStartInfo(ToolLocator.Ffmpeg)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in new[]
        {
            "-hide_banner", "-nostdin", "-loglevel", "error",
            "-f", "lavfi", "-i", "testsrc=size=1920x1080:rate=30",
            "-f", "rawvideo", "-pix_fmt", "bgra", "-"
        }) psi.ArgumentList.Add(a);

        using var process = new System.Diagnostics.Process { StartInfo = psi };
        process.Start();
        var pid = process.Id;
        Assert.False(process.HasExited, "surec baslar baslamaz olmus, olcu anlamsiz");

        var oldu = DecoderPipe.KillTree(process, DecoderPipe.KillWaitMs, DecoderPipe.KillAttempts);

        Assert.True(oldu, "KillTree oldurulemedi dedi");
        Assert.True(
            process.HasExited,
            "KillTree dondu ama surec hala yasiyor: cikisi beklemeden donuyor");
        Assert.DoesNotContain(
            System.Diagnostics.Process.GetProcessesByName("ffmpeg"),
            p => p.Id == pid);
    }

    [Fact]
    public void Oldurulemeyen_surec_icin_KillTree_basarisiz_bildirir()
    {
        if (!OperatingSystem.IsWindows()) return;

        System.Diagnostics.Process korumali;
        try { korumali = System.Diagnostics.Process.GetProcessById(4); }
        catch { return; }

        using (korumali)
        {
            Assert.False(korumali.HasExited, "korumali surec zaten olmus, olcu anlamsiz");

            var oldu = DecoderPipe.KillTree(korumali, DecoderPipe.KillWaitMs, DecoderPipe.KillAttempts);

            Assert.False(
                oldu,
                "oldurulemeyen surec icin KillTree basarili dedi: cagiran taraf sizintiyi bildiremez");
            Assert.False(korumali.HasExited);
        }
    }

    [FfmpegAvailableFact]
    public async Task StopAsync_donunce_kod_cozucu_ffmpeg_sureci_kalmaz()
    {
        using var pipe = new DecoderPipe();
        await pipe.OpenAsync(UzunSessizKlip);
        await pipe.SeekAsync(0);

        var pid = pipe.TestOnly_VideoProcessId();
        Assert.NotNull(pid);

        await pipe.StopAsync();

        Assert.DoesNotContain(
            System.Diagnostics.Process.GetProcessesByName("ffmpeg"),
            p => p.Id == pid!.Value);
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

    [SesCihaziVarFact]
    public async Task Surekli_oynatmada_ses_goruntu_kaymasi_zamanla_buyumez()
    {
        using var pipe = new DecoderPipe();
        await pipe.OpenAsync(SesliKlip);
        using var sink = new AudioSink(pipe.HasAudio);
        Assert.Null(sink.DeviceFailure);
        Assert.True(sink.HasAudio, "ses yolu kapali; olcu video ptsini bos bir saate karsi okur");
        pipe.AttachAudioSink(sink);

        using var playback = pipe.StartContinuousPlayback(0);
        pipe.SeekAudio(0);

        var samples = new List<double>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long lastFrames = -1;
        while (sw.Elapsed.TotalSeconds < 3.0)
        {
            await Task.Delay(20);
            var frames = playback.FramesDecoded;
            if (frames == 0 || frames == lastFrames) continue;
            lastFrames = frames;
            samples.Add((playback.LatestVideoPts - sink.PositionSeconds) * 1000.0);
        }

        Assert.True(samples.Count > 10, $"cok az ornek: {samples.Count}");
        var erken = samples.Take(5).Average();
        var gec = samples.TakeLast(5).Average();
        Assert.True(Math.Abs(gec - erken) < 200.0,
            $"kayma 3 sn'de asiri buyudu: erken={erken:F1}ms gec={gec:F1}ms");
    }
}
