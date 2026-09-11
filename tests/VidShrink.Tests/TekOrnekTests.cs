using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Integration;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

public sealed class TekOrnekTests : IDisposable
{
    private static readonly TimeSpan Short = TimeSpan.FromSeconds(5);

    private readonly ITestOutputHelper _output;
    private readonly string _work = Path.Combine(TestPaths.OutputRoot, "tek-ornek", Guid.NewGuid().ToString("n"));

    public TekOrnekTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_work);
    }

    public void Dispose()
    {
        try { Directory.Delete(_work, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string NewChannel() => "test-" + Guid.NewGuid().ToString("n");

    private static bool Wait(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            Thread.Sleep(50);
        }
        return condition();
    }

    [Fact]
    public void Ilk_acilan_sahip_oluyor_ikincisi_olmuyor_sahip_kapaninca_kanal_bosaliyor()
    {
        var channel = NewChannel();

        using (var first = new SingleInstanceChannel(channel))
        using (var second = new SingleInstanceChannel(channel))
        {
            Assert.True(first.IsOwner);
            Assert.False(second.IsOwner);
            Assert.False(first.Forward(new[] { "x" }, Short, Short));
        }

        using var third = new SingleInstanceChannel(channel);
        Assert.True(third.IsOwner);
    }

    [Fact]
    public void Iletilen_yollar_sahibin_isleyicisine_aynen_ulasiyor()
    {
        var channel = NewChannel();
        var received = new ConcurrentQueue<IReadOnlyList<string>>();
        using var owner = new SingleInstanceChannel(channel);
        owner.StartListening(paths => { received.Enqueue(paths); return true; });

        var sent = new[] { "C:\\Video Klasörü\\çift tıklanan \"özel\".mp4", "ikinci.mkv" };
        using var client = new SingleInstanceChannel(channel);

        Assert.True(client.Forward(sent, Short, Short));
        Assert.True(client.Forward(Array.Empty<string>(), Short, Short));

        Assert.Equal(2, received.Count);
        Assert.Equal(sent, received.First());
        Assert.Empty(received.Last());
    }

    [Fact]
    public void Isleyici_reddedince_ya_da_patlayinca_iletim_basarisiz_ve_kanal_ayakta_kaliyor()
    {
        var channel = NewChannel();
        var answers = new Queue<Func<bool>>(new Func<bool>[]
        {
            () => false,
            () => throw new InvalidOperationException("pencere meşgul"),
            () => true
        });
        using var owner = new SingleInstanceChannel(channel);
        owner.StartListening(_ => answers.Dequeue()());
        using var client = new SingleInstanceChannel(channel);

        Assert.False(client.Forward(new[] { "a.mp4" }, Short, Short));
        Assert.False(client.Forward(new[] { "a.mp4" }, Short, Short));
        Assert.True(client.Forward(new[] { "a.mp4" }, Short, Short));
    }

    [Fact]
    public void Bozuk_satir_isleyiciye_ulasmadan_HATA_aliyor()
    {
        var channel = NewChannel();
        var calls = 0;
        using var owner = new SingleInstanceChannel(channel);
        owner.StartListening(_ => { Interlocked.Increment(ref calls); return true; });

        using var pipe = new NamedPipeClientStream(".", owner.PipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
        pipe.Connect((int)Short.TotalMilliseconds);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
        writer.WriteLine("bu json değil");

        Assert.Equal(SingleInstanceChannel.Nack, reader.ReadLine());
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Dinlemeyen_sahibe_iletim_zaman_asiminda_basarisiz_donuyor()
    {
        var channel = NewChannel();
        using var owner = new SingleInstanceChannel(channel);
        using var client = new SingleInstanceChannel(channel);

        var clock = Stopwatch.StartNew();
        var forwarded = client.Forward(new[] { "a.mp4" }, TimeSpan.FromMilliseconds(300), Short);
        clock.Stop();

        Assert.False(forwarded);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(4), $"Bekleme {clock.Elapsed.TotalMilliseconds:0} ms sürdü.");
    }

    [Fact]
    public void Varsayilan_kanal_ortam_degiskenini_ad_guvenli_hale_getirip_kullaniyor()
    {
        var previous = Environment.GetEnvironmentVariable(SingleInstanceChannel.ChannelVariable);
        try
        {
            Environment.SetEnvironmentVariable(SingleInstanceChannel.ChannelVariable, " Deneme Kanal/1\\ç ");
            Assert.Equal("Deneme_Kanal_1__", SingleInstanceChannel.DefaultChannel());

            Environment.SetEnvironmentVariable(SingleInstanceChannel.ChannelVariable, null);
            var standard = SingleInstanceChannel.DefaultChannel();
            Assert.StartsWith("ana-", standard);
            Assert.All(standard, character => Assert.True(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
        }
        finally
        {
            Environment.SetEnvironmentVariable(SingleInstanceChannel.ChannelVariable, previous);
        }
    }

    [Fact]
    public void Pencere_acilmadan_gelen_dosyalar_bekletilip_sirayla_teslim_ediliyor()
    {
        var files = new ForwardedFiles();

        Assert.True(files.Receive(new[] { "", "  \"ilk.mp4\"  ", "yok-sayilan.mp4" }));
        Assert.True(files.Receive(Array.Empty<string>()));
        Assert.True(files.Receive(new[] { "ikinci.mkv" }));
        Assert.Equal(3, files.Pending);

        var delivered = new List<string?>();
        files.Attach(path => { delivered.Add(path); return true; });

        Assert.Equal(new string?[] { "ilk.mp4", null, "ikinci.mkv" }, delivered);
        Assert.Equal(0, files.Pending);
    }

    [Fact]
    public void Pencere_baglaninca_teslim_sonucu_gondericiye_donuyor()
    {
        var (accepted, refused) = AppHost.Run(() =>
        {
            var files = new ForwardedFiles();
            var open = true;
            files.Attach(_ => open);
            var first = files.Receive(new[] { "a.mp4" });
            open = false;
            return (first, files.Receive(new[] { "b.mp4" }));
        });

        Assert.True(accepted);
        Assert.False(refused);
    }

    [Fact]
    public void Isletim_sisteminin_actigi_dosyalar_ayni_kuyruga_giriyor()
    {
        var local = Path.Combine(_work, "finder'dan açılan.mov");
        File.WriteAllText(local, "video");
        var files = new ForwardedFiles();

        var (opened, reopened, empty) = AppHost.Run(() =>
        {
            var lookup = new Window().StorageProvider.TryGetFileFromPathAsync(new Uri(local));
            Drain(lookup);
            var item = lookup.Result ?? throw new InvalidOperationException("Yerel dosya depolama öğesine dönmedi.");
            return (App.App.OpenActivatedFiles(files, new FileActivatedEventArgs(new IStorageItem[] { item })),
                App.App.OpenActivatedFiles(files, new ActivatedEventArgs(ActivationKind.Reopen)),
                App.App.OpenActivatedFiles(files, new FileActivatedEventArgs(Array.Empty<IStorageItem>())));
        });

        Assert.True(opened);
        Assert.False(reopened);
        Assert.False(empty);

        var delivered = new List<string?>();
        files.Attach(path => { delivered.Add(path); return true; });
        Assert.Equal(new string?[] { local }, delivered);
    }

    [Fact]
    public void Iletilen_dosya_acik_pencereye_yukleniyor_ve_iz_birakiyor()
    {
        var file = Path.Combine(_work, "iletilen belge.txt");
        File.WriteAllText(file, "iletildi");
        var trace = Path.Combine(_work, "iz.txt");
        var previous = Environment.GetEnvironmentVariable(MainWindow.InstanceTraceVariable);

        try
        {
            Environment.SetEnvironmentVariable(MainWindow.InstanceTraceVariable, trace);

            var (queued, name, loaded) = AppHost.Run(() =>
            {
                var window = new MainWindow(null);
                var files = new ForwardedFiles();
                files.Receive(new[] { file });
                var waiting = files.Pending;
                files.Attach(window.AcceptForwarded);
                var load = window.ForwardedLoad;
                if (load is not null) Drain(load);
                return (waiting, window.FindControl<TextBlock>("TxtFileName")?.Text, load is not null);
            });

            Assert.Equal(1, queued);
            Assert.True(loaded, "Pencere iletilen dosyayı yüklemeye başlamadı.");
            Assert.Equal("iletilen belge.txt", name);
            Assert.Equal($"{file}\tiletilen belge.txt", File.ReadAllLines(trace, Encoding.UTF8).Single());
        }
        finally
        {
            Environment.SetEnvironmentVariable(MainWindow.InstanceTraceVariable, previous);
        }
    }

    [Fact]
    public void MacOS_kodlama_surerken_gelen_dosya_bekletilip_bitince_yukleniyor()
    {
        var file = Path.Combine(_work, "mac'te gelen.mp4");
        File.WriteAllText(file, "mac");
        var previous = MainWindow.IsMacOSPlatformForTest;

        try
        {
            MainWindow.IsMacOSPlatformForTest = () => true;

            var (queuedWhileBusy, statusWhileBusy, visibleWhileBusy, loadedAfter, nameAfter, statusAfter) = AppHost.Run(() =>
            {
                var window = new MainWindow(null);
                window.BeginEncodingForTest();

                var files = new ForwardedFiles();
                files.Receive(new[] { file });
                files.Attach(window.AcceptForwarded);

                var queued = window.ForwardedLoad is null;
                var status = window.SourceStatusText;
                var visible = window.SourceStatusVisible;

                window.EndEncodingForTest();
                var load = window.ForwardedLoad;
                if (load is not null) Drain(load);

                return (queued, status, visible, load is not null,
                    window.FindControl<TextBlock>("TxtFileName")?.Text, window.SourceStatusText);
            });

            Assert.True(queuedWhileBusy, "Kodlama sürerken dosya beklemeden yüklendi.");
            Assert.Contains("mac'te gelen.mp4", statusWhileBusy);
            Assert.True(visibleWhileBusy, "Bekleme durumu görünür olmadı.");
            Assert.True(loadedAfter, "Kodlama bitince bekleyen dosya yüklenmedi.");
            Assert.Equal("mac'te gelen.mp4", nameAfter);
            Assert.NotEqual(statusWhileBusy, statusAfter);
        }
        finally
        {
            MainWindow.IsMacOSPlatformForTest = previous;
        }
    }

    [Fact]
    public void Ikinci_surec_dosyayi_acik_pencereye_iletip_sifirla_cikiyor()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.True(ToolLocator.IsAvailable(out var missing), $"{missing} yok; kabul ölçüsü gerçek video ister.");

        var executable = Path.Combine(AppContext.BaseDirectory, "VidShrink.App.exe");
        Assert.True(File.Exists(executable), executable);

        var video = Path.Combine(_work, "çift tıklanan video.mp4");
        Assert.Equal(0, SegmentClips.Ffmpeg(new[]
        {
            "-y", "-hide_banner", "-loglevel", "error", "-f", "lavfi",
            "-i", "testsrc2=size=320x240:rate=30:duration=1",
            "-c:v", "libx264", "-preset", "veryfast", "-pix_fmt", "yuv420p", video
        }));

        var trace = Path.Combine(_work, "iz.txt");
        var settings = Path.Combine(_work, "settings.json");
        File.WriteAllText(settings, JsonSerializer.Serialize(new Dictionary<string, string>
        {
            [FileAssociationSetup.RegisteredKey] = FileAssociation.LaunchTarget(executable)
        }));
        var temp = Path.Combine(_work, "temp");
        Directory.CreateDirectory(temp);
        var channel = NewChannel();

        ProcessStartInfo Start(params string[] arguments)
        {
            var info = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                WorkingDirectory = _work
            };
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            info.Environment[SingleInstanceChannel.ChannelVariable] = channel;
            info.Environment[MainWindow.InstanceTraceVariable] = trace;
            info.Environment["VIDSHRINK_SETTINGS_PATH"] = settings;
            info.Environment["VIDSHRINK_UPDATE_DISABLED"] = "1";
            info.Environment["TEMP"] = temp;
            info.Environment["TMP"] = temp;
            info.Environment["PATH"] = Path.GetDirectoryName(ToolLocator.Ffmpeg) + Path.PathSeparator + info.Environment["PATH"];
            return info;
        }

        using var first = Process.Start(Start()) ?? throw new InvalidOperationException("İlk süreç başlamadı.");
        Process? second = null;
        try
        {
            var opened = Wait(() =>
            {
                first.Refresh();
                return first.HasExited || first.MainWindowHandle != IntPtr.Zero;
            }, TimeSpan.FromSeconds(60));
            Assert.True(opened && !first.HasExited, "İlk süreç penceresini açmadı.");
            _output.WriteLine($"ilk süreç pid={first.Id} pencere=0x{first.MainWindowHandle.ToInt64():X} başlık=\"{first.MainWindowTitle}\"");
            Assert.False(File.Exists(trace));

            var clock = Stopwatch.StartNew();
            second = Process.Start(Start(video)) ?? throw new InvalidOperationException("İkinci süreç başlamadı.");
            var exited = second.WaitForExit(30_000);
            clock.Stop();
            Assert.True(exited, "İkinci süreç 30 sn içinde çıkmadı.");
            second.Refresh();
            _output.WriteLine($"ikinci süreç pid={second.Id} çıkış kodu={second.ExitCode} süre={clock.ElapsedMilliseconds} ms pencere=0x{second.MainWindowHandle.ToInt64():X}");
            Assert.Equal(0, second.ExitCode);

            Assert.True(Wait(() => File.Exists(trace) && File.ReadAllText(trace, Encoding.UTF8).Contains('\n'), TimeSpan.FromSeconds(30)),
                "İlk pencere iletilen dosyayı yüklediğini yazmadı.");
            var lines = File.ReadAllLines(trace, Encoding.UTF8);
            foreach (var line in lines) _output.WriteLine("ilk pencerenin izi: " + line);

            first.Refresh();
            Assert.False(first.HasExited, "İlk süreç kapandı.");
            Assert.Equal($"{video}\t{Path.GetFileName(video)}", Assert.Single(lines));
        }
        finally
        {
            if (second is { HasExited: false }) second.Kill(true);
            second?.Dispose();
            if (!first.HasExited) first.Kill(true);
            first.WaitForExit(10_000);
        }
    }

    private static void Drain(Task work)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!work.IsCompleted && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        Assert.True(work.IsCompleted, "İletilen dosyanın yüklenmesi süresinde bitmedi.");
        work.GetAwaiter().GetResult();
    }
}
