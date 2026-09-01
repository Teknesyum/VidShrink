using System.Diagnostics;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// Parca olcumlerinin kaynagi. Tek klip uretilir ve butun olcumler paylasir; icerik
/// <c>testsrc2</c>, yani her karede hareket var — hiza olcumu duran goruntude anlamsiz olurdu.
/// </summary>
public sealed class SegmentClips : IDisposable
{
    public string Directory { get; }
    public bool Ready { get; }

    /// <summary>640x360, 12 sn, 30 fps.</summary>
    public string Kaynak => Path.Combine(Directory, "kaynak.mp4");

    /// <summary>1920x1080, 12 sn, 30 fps. K1'in sure olcumu bunu kullanir.</summary>
    public string Buyuk => Path.Combine(Directory, "buyuk.mp4");

    /// <summary>Video akisi olmayan bozuk dosya.</summary>
    public string Bozuk => Path.Combine(Directory, "bozuk.mp4");

    public SegmentClips()
    {
        Directory = Path.Combine(Path.GetTempPath(), "vidshrink-t48-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllBytes(Bozuk, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

        if (!ToolLocator.IsAvailable(out _)) return;

        Ready =
            Encode("testsrc2=size=640x360:rate=30:duration=12", Kaynak)
            && Encode("testsrc2=size=1920x1080:rate=30:duration=12", Buyuk);
    }

    private static bool Encode(string lavfi, string output)
        => Ffmpeg(new[]
        {
            "-y", "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", lavfi,
            "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p",
            output
        }) == 0;

    internal static int Ffmpeg(IEnumerable<string> args, string? rawOutput = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        stdout.Wait();
        stderr.Wait();
        process.WaitForExit();
        _ = rawOutput;
        return process.ExitCode;
    }

    public void Dispose()
    {
        try { System.IO.Directory.Delete(Directory, recursive: true); } catch { }
    }
}

public sealed class SegmentEncoderTests : IClassFixture<SegmentClips>
{
    private readonly SegmentClips _clips;

    public SegmentEncoderTests(SegmentClips clips) => _clips = clips;

    private static MediaInfo Source(string path, int width = 640, int height = 360, double duration = 12) => new()
    {
        FilePath = path,
        FileSizeBytes = new FileInfo(path).Exists ? new FileInfo(path).Length : 1_000_000,
        DurationSeconds = duration,
        Width = width,
        Height = height,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 2_000_000
    };

    private static EncodePlan Plan(int width = 320, int height = 180, int videoK = 300) => new()
    {
        Codec = "libx264",
        Mode = "2pass",
        VideoBitrateK = videoK,
        Width = width,
        Height = height,
        Fps = 30,
        Preset = "veryfast",
        PixelFormat = "yuv420p",
        AudioCodec = null
    };

    private string Temp()
    {
        var dir = Path.Combine(_clips.Directory, "kodlayici-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Sol_kesit_kayipsiz_ve_pencereye_kirpili()
    {
        var args = SegmentEncoder.BuildSourceClipArguments("kaynak.mp4", 12.5, 2, "sol.mp4").ToList();

        Assert.Equal("-ss", args[args.IndexOf("-i") - 2]);
        Assert.Equal("12.5", args[args.IndexOf("-i") - 1]);
        Assert.Equal("2", args[args.IndexOf("-t") + 1]);
        Assert.Equal("0", args[args.IndexOf("-qp") + 1]);
        Assert.Contains("-an", args);
        Assert.Equal("sol.mp4", args[^1]);
    }

    [Fact]
    public void Gecici_dosyalar_temizleyicinin_tanidigi_kalipta()
        => Assert.StartsWith("vidshrink_", SegmentEncoder.TempPrefix);

    [FfmpegFact]
    public async Task Parca_bir_cift_dosya_birakir()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);

        var clip = await encoder.RequestAsync(Source(_clips.Kaynak), Plan(), 4);

        Assert.NotNull(clip);
        Assert.True(File.Exists(clip!.SourcePath));
        Assert.True(File.Exists(clip.EncodedPath));
        Assert.Equal(4, clip.StartSeconds, 3);
        Assert.Equal(PreviewSegment.WindowSeconds, clip.DurationSeconds, 3);
        Assert.True(clip.IsApproximate);
        Assert.NotNull(clip.Crf);
    }

    /// <summary>
    /// Sozlesmenin iptal yolu: arka arkaya bes istek, tek kodlama kosar. Onceki istekler
    /// iptal edilir ve <c>null</c> doner; hata yazmazlar.
    /// </summary>
    [FfmpegFact]
    public async Task Ard_arda_bes_istek_tek_kodlama_kosturur()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var info = Source(_clips.Kaynak);

        var requests = new List<Task<PreviewClip?>>();
        for (var i = 0; i < 5; i++)
            requests.Add(encoder.RequestAsync(info, Plan(videoK: 300 + i * 50), 4));

        var clips = await Task.WhenAll(requests);

        Assert.Equal(1, encoder.PeakConcurrentEncodes);
        Assert.Equal(1, clips.Count(clip => clip is not null));
        Assert.NotNull(clips[^1]);
    }

    /// <summary>K5: yirmi ayar degisimi sonrasi diskte kalan dosya sayisi.</summary>
    [FfmpegFact]
    public async Task Yirmi_istekten_sonra_gecici_dosya_birikmiyor()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using (var encoder = new SegmentEncoder(dir))
        {
            var info = Source(_clips.Kaynak);
            for (var i = 0; i < 20; i++)
                await encoder.RequestAsync(info, Plan(videoK: 200 + i * 25), 4);

            var live = Directory.GetFiles(dir, SegmentEncoder.TempPrefix + "*");
            Record($"K5 20 istek sonrasi kalan dosya: {live.Length}");
            Assert.True(live.Length <= SegmentEncoder.KeepClips * 2, $"kalan dosya: {live.Length}");
        }

        Assert.Empty(Directory.GetFiles(dir, SegmentEncoder.TempPrefix + "*"));
    }

    /// <summary>
    /// Bozuk kaynakta kodlayan sessiz kalmaz. Olcu eskiden "LastError bos degil" diyordu;
    /// bu iddia LastError ekran iletisi tasirken bir sey olcuyordu, bugun anahtar tasidigi
    /// icin "anahtar bos degil"e inmisti. Olculen sey artik sebebin kendisi: hangi anahtar
    /// geliyor, dil dosyalarinda karsiligi var mi, LastError iletiyi degil anahtari mi
    /// veriyor, ve motorun ham tanisi biciim argumani olarak duruyor mu.
    /// </summary>
    [FfmpegFact]
    public async Task Bozuk_kaynakta_hata_yutulmaz()
    {
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);

        var clip = await encoder.RequestAsync(Source(_clips.Bozuk), Plan(), 0);

        Assert.Null(clip);
        Assert.Empty(Directory.GetFiles(dir, SegmentEncoder.TempPrefix + "*"));

        Assert.True(encoder.LastFailure.HasValue, "kodlayan sebep birakmadi");
        var sebep = encoder.LastFailure!.Value;

        Assert.Equal(sebep.Key, encoder.LastError);

        var ekranMetinleri = Locales.Languages
            .SelectMany(language => Locales.Values(language).Values)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(sebep.Key, ekranMetinleri);

        foreach (var language in Locales.Languages)
        {
            var sozluk = Locales.Values(language);
            Assert.True(sozluk.ContainsKey(sebep.Key), $"{language} sozlugunde karsiligi yok: {sebep.Key}");

            var sablon = sozluk[sebep.Key];
            Assert.Equal(sebep.Detail is not null, sablon.Contains("{0}", StringComparison.Ordinal));
        }

        Assert.NotNull(sebep.Detail);
        Assert.False(int.TryParse(sebep.Detail, out _), $"cikis kodu geldi: \"{sebep.Detail}\"");
        Assert.True(sebep.Detail!.Split(' ').Length >= 3, $"motor tanisi bekleniyordu: \"{sebep.Detail}\"");

        Record($"K6 bozuk kaynak sebebi: {sebep.Key} · tani: {Tekcizgi(sebep.Detail)}");
    }

    private static string Tekcizgi(string text)
    {
        var flat = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return flat.Length <= 120 ? flat : flat[..120];
    }

    /// <summary>
    /// K2 hiza olcumu. Panelin kendi grafigi kosturulur ve birlesik karenin iki yarisi
    /// arasindaki ortalama mutlak fark olculur. Ayni pencereye kesilmis cift ile bugunku
    /// eslestirme (sol tam kaynak, sag parca) yan yana konur.
    /// </summary>
    [FfmpegFact]
    public async Task Iki_yari_ayni_kareyi_gosteriyor()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var info = Source(_clips.Kaynak);

        var clip = await encoder.RequestAsync(info, Plan(), 5);
        Assert.NotNull(clip);

        var hizali = Mad(Hstack(clip!.SourcePath, clip.EncodedPath, 0, dir));
        var hizaliOrta = Mad(Hstack(clip.SourcePath, clip.EncodedPath, 1.0, dir));
        var hizasiz = Mad(Hstack(_clips.Kaynak, clip.EncodedPath, 0, dir));

        Record($"K2 MAD hizali={hizali:0.00} pencere-ortasi={hizaliOrta:0.00} hizasiz={hizasiz:0.00}");
        Assert.True(hizali < 12, $"hizali MAD={hizali:0.00}");
        Assert.True(hizaliOrta < 12, $"pencere ortasi MAD={hizaliOrta:0.00}");
        Assert.True(hizasiz > hizali * 3, $"hizali={hizali:0.00} hizasiz={hizasiz:0.00}");
    }

    /// <summary>
    /// K1 sure olcumu: 1080p kaynakta 5 sn'lik pencerenin iki dosyasi kac ms'te cikiyor.
    /// Sayi rapora ham haliyle giriyor, bu yuzden olcum dosyaya da yazilir.
    /// </summary>
    [FfmpegFact]
    public async Task Bir_pencerenin_kodlanma_suresi_olculur()
    {
        Assert.True(_clips.Ready);
        var dir = Temp();
        using var encoder = new SegmentEncoder(dir);
        var info = Source(_clips.Buyuk, 1920, 1080);

        var runs = new List<double>();
        for (var i = 0; i < 5; i++)
        {
            var clip = await encoder.RequestAsync(info, Plan(1280, 720, 1500 + i * 10), 3 + i);
            Assert.NotNull(clip);
            runs.Add(clip!.Elapsed.TotalMilliseconds);
        }

        Record("K1 1080p 5 sn pencere, ms: " + string.Join(" ", runs.Select(ms => ms.ToString("0"))));
        Assert.All(runs, ms => Assert.True(ms > 0));
    }

    /// <summary>Olcum ciktisi projenin kendi calisma klasorune iner; git'e sizmaz.</summary>
    private static void Record(string line)
    {
        try
        {
            var dir = TestPaths.LiveOut("t48");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "olcum.txt"), line + Environment.NewLine);
        }
        catch { }
    }

    private const int PanelWidth = 160;
    private const int PanelHeight = 90;

    private static byte[] Hstack(string left, string right, double seconds, string dir)
    {
        var output = Path.Combine(dir, $"kare-{Guid.NewGuid():N}.raw");
        var args = new List<string> { "-y", "-hide_banner", "-loglevel", "error" };
        if (seconds > 0) { args.Add("-ss"); args.Add(seconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)); }
        args.AddRange(new[] { "-i", left });
        if (seconds > 0) { args.Add("-ss"); args.Add(seconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)); }
        args.AddRange(new[] { "-i", right });
        args.AddRange(new[]
        {
            "-filter_complex",
            $"[0:v]fps=30,scale={PanelWidth}:{PanelHeight}[l];[1:v]fps=30,scale={PanelWidth}:{PanelHeight}[r];[l][r]hstack=inputs=2[v]",
            "-map", "[v]", "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "bgra", output
        });

        Assert.Equal(0, SegmentClips.Ffmpeg(args));
        var bytes = File.ReadAllBytes(output);
        File.Delete(output);
        Assert.Equal(PanelWidth * 2 * PanelHeight * 4, bytes.Length);
        return bytes;
    }

    /// <summary>Birlesik karenin iki yarisi arasindaki ortalama mutlak fark, 0-255.</summary>
    private static double Mad(byte[] frame)
    {
        var stride = PanelWidth * 2 * 4;
        long total = 0;
        long count = 0;
        for (var y = 0; y < PanelHeight; y++)
        {
            var row = y * stride;
            for (var x = 0; x < PanelWidth; x++)
            {
                var l = row + x * 4;
                var r = row + (PanelWidth + x) * 4;
                for (var c = 0; c < 3; c++)
                {
                    total += Math.Abs(frame[l + c] - frame[r + c]);
                    count++;
                }
            }
        }
        return (double)total / count;
    }
}
