using System.Diagnostics;
using System.Globalization;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Canli ekran kaydi olculeri. ffmpeg yoksa, makine Windows degilse ya da bu ffmpeg
/// derlemesinde <c>gdigrab</c> yoksa atlanir: kaydin oteki iki girdisi
/// (<c>avfoundation</c>, <c>x11grab</c>) bu makinede kosturulamiyor, onlarin olcusu
/// arguman uretiminde duruyor.
/// </summary>
public sealed class KayitFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> Gdigrab = new(() =>
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ToolLocator.Ffmpeg,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-devices");
            using var process = Process.Start(psi)!;
            var text = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(5000);
            return text.Contains("gdigrab", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    });

    public KayitFactAttribute()
    {
        if (!ToolLocator.IsAvailable(out var missing))
            Skip = $"{missing} bulunamadi, canli ekran kaydi olculeri kosturulmadi.";
        else if (!OperatingSystem.IsWindows())
            Skip = "canli kayit olcusu gdigrab ile alindi; bu makine Windows degil.";
        else if (!Gdigrab.Value)
            Skip = "bu ffmpeg derlemesinde gdigrab yok, canli ekran kaydi olculeri kosturulmadi.";
    }
}

/// <summary>Kayit olculerinin kaniti; ham ffprobe ciktilari burada kalir.</summary>
internal static class KayitKanit
{
    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "dalga8a");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static string Path_(string name) => Path.Combine(Folder, name);

    /// <summary>ffprobe'u kosturur, ham ciktiyi kanit dosyasina yazar ve geri dondurur.</summary>
    internal static (int ExitCode, string Text) Ffprobe(string file, string evidenceName)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffprobe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
        {
            "-hide_banner", "-v", "error",
            "-show_entries", "format=duration,size",
            "-show_entries", "stream=codec_type,codec_name,width,height,nb_frames",
            "-of", "default=nw=1", file
        }) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var text = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit(20_000);
        File.WriteAllText(Path.Combine(Folder, evidenceName), $"# {file}{Environment.NewLine}# cikis kodu {process.ExitCode}{Environment.NewLine}{text}");
        return (process.ExitCode, text);
    }

    internal static double? Duration(string probeText)
    {
        foreach (var line in probeText.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("duration=", StringComparison.Ordinal)) continue;
            if (double.TryParse(trimmed["duration=".Length..], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return value;
        }
        return null;
    }

    internal static void Log(string name, IEnumerable<string> lines)
        => File.WriteAllLines(Path.Combine(Folder, name), lines);
}

/// <summary>
/// 8a kolu: ekran kaydi argumanlari <c>Core</c>'da uretilir, oturum <c>Ffmpeg</c>'de
/// kosturulur. Olculer iki yere bakiyor — uretilen argumanin bicimi (uc platform, uc hedef,
/// negatif kontroller) ve gercek bir kaydin dosyasi.
/// </summary>
public sealed class KayitMotoruTests
{
    private static RecorderRequest Ekran(RecorderPlatform platform) => new()
    {
        Platform = platform,
        Target = RecorderTargetKind.Screen
    };

    private static string Arg(IReadOnlyList<string> args) => string.Join(" ", args);

    [Fact]
    public void WindowsEkraniGdigrabMasaustunuAlir()
    {
        var args = RecorderArguments.Build(Ekran(RecorderPlatform.Windows) with { Fps = 15 }, @"C:\kayit\a.mp4");

        Assert.Contains("-f gdigrab", Arg(args));
        Assert.Contains("-framerate 15", Arg(args));
        Assert.Contains("-i desktop", Arg(args));
        Assert.Contains("-draw_mouse 1", Arg(args));
        Assert.Equal(@"C:\kayit\a.mp4", args[^1]);
        Assert.Contains("-movflags", args);
    }

    [Fact]
    public void WindowsPenceresiBasliktanGirer()
    {
        var args = RecorderArguments.Build(
            Ekran(RecorderPlatform.Windows) with { Target = RecorderTargetKind.Window, WindowTitle = "Not Defteri" },
            @"C:\kayit\p.mkv");

        Assert.Contains("-i title=Not Defteri", Arg(args));
        Assert.DoesNotContain("-movflags", args);
    }

    [Fact]
    public void WindowsBolgesiOfsetVeBoyutVerir()
    {
        var args = RecorderArguments.Build(
            Ekran(RecorderPlatform.Windows) with
            {
                Target = RecorderTargetKind.Region,
                Region = new RecorderRegion(40, 20, 640, 480)
            },
            @"C:\kayit\b.mp4");

        Assert.Contains("-offset_x 40 -offset_y 20 -video_size 640x480", Arg(args));
        Assert.Contains("-i desktop", Arg(args));
        Assert.DoesNotContain("-vf", args);
    }

    [Fact]
    public void MacEkraniAvfoundationIndeksiVerir()
    {
        var args = RecorderArguments.Build(Ekran(RecorderPlatform.MacOs) with { ScreenIndex = 2 }, "/tmp/a.mov");

        Assert.Contains("-f avfoundation", Arg(args));
        Assert.Contains("-i 2:none", Arg(args));
        Assert.Contains("-capture_cursor 1", Arg(args));
    }

    /// <summary>
    /// macOS'ta bolge girdi secenegi degil: dikdortgen kirpma filtresine dusuyor. Ayni
    /// istek Windows'ta girdiye ofset yaziyordu, burada <c>-vf crop</c> cikiyor.
    /// </summary>
    [Fact]
    public void MacBolgesiKirpmaFiltresineDuser()
    {
        var args = RecorderArguments.Build(
            Ekran(RecorderPlatform.MacOs) with
            {
                Target = RecorderTargetKind.Region,
                Region = new RecorderRegion(10, 30, 320, 240)
            },
            "/tmp/b.mov");

        Assert.Contains("-vf crop=320:240:10:30", Arg(args));
        Assert.DoesNotContain("-offset_x", args);
    }

    /// <summary>Negatif kontrol: avfoundation pencere vermiyor, istek sessizce yutulmuyor.</summary>
    [Fact]
    public void MacPencereSecimiSessizceYutulmaz()
    {
        var istek = Ekran(RecorderPlatform.MacOs) with { Target = RecorderTargetKind.Window, WindowTitle = "Safari" };

        var hata = Assert.Throws<InvalidOperationException>(() => RecorderArguments.Build(istek, "/tmp/p.mov"));
        Assert.Contains("avfoundation", hata.Message);
        Assert.Contains("not single windows", hata.Message);
    }

    [Fact]
    public void LinuxEkraniX11grabAdresiniVerir()
    {
        var args = RecorderArguments.Build(Ekran(RecorderPlatform.Linux), "/tmp/a.mp4");

        Assert.Contains("-f x11grab", Arg(args));
        Assert.Contains("-i :0.0", Arg(args));
    }

    [Fact]
    public void LinuxBolgesiAdresinSonunaKoordinatYazar()
    {
        var args = RecorderArguments.Build(
            Ekran(RecorderPlatform.Linux) with
            {
                Target = RecorderTargetKind.Region,
                Display = ":1.0",
                Region = new RecorderRegion(100, 50, 800, 600)
            },
            "/tmp/b.mp4");

        Assert.Contains("-video_size 800x600", Arg(args));
        Assert.Contains("-i :1.0+100,50", Arg(args));
    }

    [Fact]
    public void LinuxPenceresiWindowIdIleGirer()
    {
        var args = RecorderArguments.Build(
            Ekran(RecorderPlatform.Linux) with { Target = RecorderTargetKind.Window, WindowId = "0x3200003" },
            "/tmp/p.mp4");

        Assert.Contains("-window_id 0x3200003", Arg(args));
        Assert.Contains("-i :0.0", Arg(args));
    }

    /// <summary>
    /// Kolun tasiyici karari: kayit argumanlarinda <c>-nostdin</c> yok, cunku nazik durdurma
    /// yolu tam olarak stdin'e yazilan <c>q</c>. Deponun oteki cagrilari onu veriyor.
    /// </summary>
    [Theory]
    [InlineData(RecorderPlatform.Windows)]
    [InlineData(RecorderPlatform.MacOs)]
    [InlineData(RecorderPlatform.Linux)]
    public void HicbirPlatformNostdinVermez(RecorderPlatform platform)
    {
        var args = RecorderArguments.Build(Ekran(platform), "/tmp/a.mp4");

        Assert.DoesNotContain("-nostdin", args);
    }

    /// <summary>Ilerleme bayraklari ureticinin degil okuyan tarafin isi.</summary>
    [Fact]
    public void IlerlemeBayraklariniUreticiKoymaz()
    {
        var args = RecorderArguments.Build(Ekran(RecorderPlatform.Windows), "/tmp/a.mp4");

        Assert.DoesNotContain("-progress", args);
        Assert.DoesNotContain("-nostats", args);
    }

    [Fact]
    public void SesArgumanlariGirdidenSonraArayaGirer()
    {
        var args = RecorderArguments.Build(
            Ekran(RecorderPlatform.Windows) with
            {
                Audio = new AudioCapturePlan(
                    new[] { "-f", "dshow", "-i", "audio=Mikrofon" },
                    null,
                    new[] { "1:a" },
                    1)
            },
            @"C:\kayit\s.mp4");

        var metin = Arg(args);
        Assert.True(metin.IndexOf("-i desktop", StringComparison.Ordinal) < metin.IndexOf("audio=Mikrofon", StringComparison.Ordinal));
        Assert.Contains("-c:a aac", metin);
        Assert.DoesNotContain("-an", args);
    }

    [Fact]
    public void SessizKayitAnVerir()
    {
        var args = RecorderArguments.Build(Ekran(RecorderPlatform.Windows), @"C:\kayit\s.mp4");

        Assert.Contains("-an", args);
    }

    /// <summary>
    /// Negatif kontroller: uydurma kodek, olcusuz bolge, tek sayili olcu, sifir kare hizi ve
    /// gdigrab'in almadigi ekran indeksi reddedilir.
    /// </summary>
    [Fact]
    public void KabulEdilemezIstekSebepleriyleReddedilir()
    {
        var uydurmaKodek = RecorderArguments.Validate(Ekran(RecorderPlatform.Windows) with { VideoCodec = "libzzznotreal" }, "a.mp4");
        var bolgesizBolge = RecorderArguments.Validate(Ekran(RecorderPlatform.Windows) with { Target = RecorderTargetKind.Region }, "a.mp4");
        var tekSayili = RecorderArguments.Validate(
            Ekran(RecorderPlatform.Windows) with { Target = RecorderTargetKind.Region, Region = new RecorderRegion(0, 0, 641, 480) },
            "a.mp4");
        var sifirHiz = RecorderArguments.Validate(Ekran(RecorderPlatform.Windows) with { Fps = 0 }, "a.mp4");
        var ekranIndeksi = RecorderArguments.Validate(Ekran(RecorderPlatform.Windows) with { ScreenIndex = 1 }, "a.mp4");
        var basliksizPencere = RecorderArguments.Validate(Ekran(RecorderPlatform.Windows) with { Target = RecorderTargetKind.Window }, "a.mp4");
        var kimliksizPencere = RecorderArguments.Validate(Ekran(RecorderPlatform.Linux) with { Target = RecorderTargetKind.Window }, "a.mp4");

        Assert.Contains(uydurmaKodek, satir => satir.Contains("libzzznotreal"));
        Assert.Contains(bolgesizBolge, satir => satir.Contains("rectangle"));
        Assert.Contains(tekSayili, satir => satir.Contains("even"));
        Assert.Contains(sifirHiz, satir => satir.Contains("Frame rate"));
        Assert.Contains(ekranIndeksi, satir => satir.Contains("gdigrab"));
        Assert.Contains(basliksizPencere, satir => satir.Contains("window title"));
        Assert.Contains(kimliksizPencere, satir => satir.Contains("window id"));
        Assert.Empty(RecorderArguments.Validate(Ekran(RecorderPlatform.Windows), "a.mp4"));
    }

    /// <summary>
    /// Canli kayitta sure yok, o yuzden kesir de yok: <see cref="RecordProgress"/> yuzde
    /// tasiyan bir uye tasimaz. <c>EncodeProgress</c> tasiyor, cunku orada kaynagin suresi
    /// bolen olarak var.
    /// </summary>
    [Fact]
    public void KayitIlerlemesiKesirTasimaz()
    {
        var kayit = typeof(RecordProgress).GetProperties().Select(p => p.Name).ToArray();
        var kodlama = typeof(EncodeProgress).GetProperties().Select(p => p.Name).ToArray();

        Assert.Contains("Fraction", kodlama);
        Assert.DoesNotContain("Fraction", kayit);
        Assert.Equal(new[] { "Captured", "DroppedFrames", "Elapsed", "Frames", "OutputMb" }, kayit.OrderBy(n => n, StringComparer.Ordinal));
    }

    /// <summary>
    /// Kabul olcusu: 5 sn'lik gercek kayit. Dosya <c>q</c> ile kapanir, ffprobe suresini ve
    /// akisini okur, ilerleme raporlari gecen sureyi ve kare sayisini tasir.
    /// </summary>
    [KayitFact]
    public async Task BesSaniyelikGercekKayitOkunur()
    {
        var cikti = KayitKanit.Path_("bes-saniye.mp4");
        var raporlar = new List<RecordProgress>();
        var progress = new Progress<RecordProgress>(rapor => { lock (raporlar) raporlar.Add(rapor); });

        var oturum = await RecorderSession.StartAsync(BolgeIstegi(), cikti, progress);

        await Task.Delay(5000);
        var sonuc = await oturum.StopAsync();

        var (kod, metin) = KayitKanit.Ffprobe(cikti, "bes-saniye.ffprobe.txt");
        KayitKanit.Log("bes-saniye.ilerleme.txt", raporlar.Select(r =>
            $"elapsed={r.Elapsed.TotalSeconds:0.000} captured={r.Captured.TotalSeconds:0.000} mb={r.OutputMb:0.000} frames={r.Frames} drop={r.DroppedFrames}"));

        Assert.True(sonuc.Ok, $"kayit 0 ile kapanmali; stderr: {sonuc.StandardError}");
        Assert.False(sonuc.Partial, "nazik durdurma yarim dosya birakmaz");
        Assert.Equal(0, kod);
        Assert.Contains("codec_type=video", metin);
        var sure = KayitKanit.Duration(metin);
        Assert.NotNull(sure);
        Assert.InRange(sure!.Value, 4.0, 8.0);
        Assert.True(sonuc.OutputMb > 0, "dosyaya bayt yazilmali");
        Assert.NotEmpty(raporlar);
        Assert.True(raporlar[^1].Frames > 0, "ilerleme kare sayisi tasimali");
        Assert.True(raporlar[^1].Elapsed > TimeSpan.Zero, "ilerleme gecen sureyi tasimali");
    }

    /// <summary>
    /// Negatif kontrol: nazik durdurmaya sure taninmazsa surec oldurulur, mux kapanmaz ve
    /// dosya ffprobe'ta bozuk cikar. <c>q</c> ile kapanan ayni kayit okunuyordu.
    /// </summary>
    [KayitFact]
    public async Task ZamanAsiminaDusenDurdurmaYarimDosyaBirakir()
    {
        var cikti = KayitKanit.Path_("oldurulen.mp4");
        var oturum = await RecorderSession.StartAsync(BolgeIstegi(), cikti);

        await Task.Delay(3000);
        var sonuc = await oturum.StopAsync(stopTimeoutMs: 0);

        var (kod, metin) = KayitKanit.Ffprobe(cikti, "oldurulen.ffprobe.txt");

        Assert.True(sonuc.Partial, "oldurulen kayit yarim isaretlenmeli");
        Assert.False(sonuc.Ok);
        Assert.NotEqual(0, kod);
        Assert.True(kod != 0 || !metin.Contains("codec_type=video"), "yarim dosya oynatilabilir sayilmaz");
    }

    /// <summary>
    /// Duraklatmanin ffmpeg'de karsiligi yok: her duraklatma parcayi nazikce kapatiyor,
    /// devam yeni parca aciyor, durdurma parcalari yeniden kodlamadan birlestiriyor.
    /// </summary>
    [KayitFact]
    public async Task DuraklatilanKayitIkiParcayiBirlestirir()
    {
        var cikti = KayitKanit.Path_("duraklatilan.mp4");
        var oturum = await RecorderSession.StartAsync(BolgeIstegi(), cikti);

        await Task.Delay(2500);
        await oturum.PauseAsync();
        Assert.Equal(RecorderState.Paused, oturum.State);
        await Task.Delay(1000);
        await oturum.ResumeAsync();
        Assert.Equal(RecorderState.Running, oturum.State);
        await Task.Delay(2500);
        var sonuc = await oturum.StopAsync();

        var (kod, metin) = KayitKanit.Ffprobe(cikti, "duraklatilan.ffprobe.txt");

        Assert.True(sonuc.Ok, $"birlestirme basarili olmali; stderr: {sonuc.StandardError}");
        Assert.Equal(2, sonuc.Segments);
        Assert.Equal(0, kod);
        var sure = KayitKanit.Duration(metin);
        Assert.NotNull(sure);
        Assert.InRange(sure!.Value, 3.0, 8.0);
        Assert.Equal(1, metin.Split('\n').Count(satir => satir.Trim() == "codec_type=video"));
        Assert.Equal(RecorderState.Stopped, oturum.State);
    }

    /// <summary>
    /// Negatif kontrol: olmayan bir pencere adiyla baslatma sessizce bekleyen bir oturum
    /// birakmaz; ffmpeg'in sebebi yukari tasinir.
    /// </summary>
    [KayitFact]
    public async Task OlmayanPencereSebebiyleBaslatmaPatlar()
    {
        var istek = Ekran(RecorderPlatform.Windows) with
        {
            Target = RecorderTargetKind.Window,
            WindowTitle = "VidShrink-8a-olmayan-pencere"
        };

        var hata = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RecorderSession.StartAsync(istek, KayitKanit.Path_("olmayan-pencere.mp4")));

        File.WriteAllText(Path.Combine(KayitKanit.Folder, "olmayan-pencere.stderr.txt"), hata.Message);
        Assert.Contains("ffmpeg kaydi baslatamadi", hata.Message);
    }

    /// <summary>Canli olculerin ortak istegi: kucuk bir bolge, hizli kodlama.</summary>
    private static RecorderRequest BolgeIstegi() => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Region,
        Region = new RecorderRegion(0, 0, 640, 480),
        Fps = 15,
        Preset = "ultrafast"
    };
}
