using System.Diagnostics;
using System.Globalization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

/// <summary>
/// Kare hizi dusurmenin olcum klipleri. Dort kaynak kare hizi: 30, 60, 25 ve 29,97.
/// Icerik <c>testsrc2</c>, her karede hareket var; ses <c>sine</c>, cunku sapmanin
/// gorunur oldugu yer goruntunun sese gore kaymasi.
/// </summary>
public sealed class FpsDropClips : IDisposable
{
    /// <summary>Kaynak kare hizi -> klip yolu.</summary>
    public IReadOnlyDictionary<string, string> Sources { get; }

    public string Directory { get; }
    public bool Ready { get; }

    public const double DurationSeconds = 4.0;

    public FpsDropClips()
    {
        Directory = Path.Combine(Path.GetTempPath(), "vidshrink-t60-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(Directory);

        var sources = new Dictionary<string, string>();
        Sources = sources;
        if (!ToolLocator.IsAvailable(out _)) return;

        var ready = true;
        foreach (var rate in new[] { "30", "60", "25", "30000/1001" })
        {
            var path = Path.Combine(Directory, "kaynak-" + rate.Replace('/', '_') + ".mp4");
            ready &= Encode(rate, path);
            sources[rate] = path;
        }
        Ready = ready;
    }

    private static bool Encode(string rate, string output)
        => SegmentClips.Ffmpeg(new[]
        {
            "-y", "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", $"testsrc2=size=320x240:rate={rate}:duration={DurationSeconds.ToString(CultureInfo.InvariantCulture)}",
            "-f", "lavfi", "-i", $"sine=frequency=440:duration={DurationSeconds.ToString(CultureInfo.InvariantCulture)}",
            "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "128k", "-shortest",
            output
        }) == 0;

    public void Dispose()
    {
        try { System.IO.Directory.Delete(Directory, recursive: true); } catch { }
    }
}

/// <summary>
/// Kare hizi dusurme <c>-r</c> ile degil <c>fps=</c> filtresiyle yapilir. <c>-r</c>
/// cikti zaman damgalarini yeniden orneklerken klibin basindaki birkac kareyi oldugu
/// gibi gecirip sonra oranina kilitleniyor: cikti hedef kare hizinda iki kare uzuyor
/// ve kalan butun goruntu sese gore o kadar geciyor. <c>fps=</c> kareyi kaynak
/// zamanina gore secer, sayi ve sure kaynaginkiyle ortusur.
/// </summary>
public sealed class FpsDropTests : IClassFixture<FpsDropClips>
{
    private readonly FpsDropClips _clips;

    public FpsDropTests(FpsDropClips clips) => _clips = clips;

    private static readonly string MeasurementLog =
        Path.Combine(TipSources.Root, ".calisma", "t60", "olcum.txt");

    private static void Log(string line)
    {
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(MeasurementLog)!);
        lock (MeasurementLog) File.AppendAllText(MeasurementLog, line + Environment.NewLine);
    }

    private static MediaInfo Probe(string path) => FfprobeClient.ProbeAsync(path).GetAwaiter().GetResult();

    private static EncodePlan PlanFor(MediaInfo info, double fps, string codec = "libx264", string mode = "crf") => new()
    {
        Codec = codec,
        Mode = mode,
        VideoBitrateK = 600,
        Crf = mode == "crf" ? 23 : null,
        Width = info.Width,
        Height = info.Height,
        Fps = fps,
        Preset = FfmpegArguments.DefaultPreset(codec),
        PixelFormat = "yuv420p",
        AudioCodec = "aac",
        AudioBitrateK = 128
    };

    private static ProcessStartInfo StartInfo(string tool, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = tool,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        return psi;
    }

    private static string Ffprobe(string path, string stream, string entries)
    {
        var args = new List<string> { "-v", "error", "-select_streams", stream };
        if (entries.Contains("nb_read_frames")) args.Add("-count_frames");
        args.AddRange(new[] { "-show_entries", "stream=" + entries, "-of", "default=nw=1:nk=1", path });

        using var process = new Process { StartInfo = StartInfo(ToolLocator.Ffprobe, args) };
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Trim();
    }

    private static int FrameCount(string path)
        => int.Parse(Ffprobe(path, "v:0", "nb_read_frames"), CultureInfo.InvariantCulture);

    private static double VideoSeconds(string path)
        => double.Parse(Ffprobe(path, "v:0", "duration"), CultureInfo.InvariantCulture);

    private static double AudioSeconds(string path)
        => double.Parse(Ffprobe(path, "a:0", "duration"), CultureInfo.InvariantCulture);

    private static int Run(IReadOnlyList<string> args) => SegmentClips.Ffmpeg(args);

    // --- Arguman bicimi -------------------------------------------------------

    [Fact]
    public void Kare_hizi_dusurme_r_degil_filtre_uretir()
    {
        var info = SourceInfo(60);
        var args = FfmpegArguments.Build(info, PlanFor(info, 30), "cikti.mp4", 0, null);

        Assert.DoesNotContain("-r", args);
        var vf = args.IndexOf("-vf");
        Assert.True(vf >= 0, "-vf uretilmedi");
        Assert.Equal("fps=30", args[vf + 1]);
    }

    [Fact]
    public void Kare_hizi_ayniysa_filtre_eklenmez()
    {
        var info = SourceInfo(30);
        var args = FfmpegArguments.Build(info, PlanFor(info, 30), "cikti.mp4", 0, null);

        Assert.DoesNotContain("-r", args);
        Assert.DoesNotContain("-vf", args);
    }

    [Fact]
    public void Fps_zincirin_sonuna_olcekleme_ve_hdr_den_sonra_girer()
    {
        var info = SourceInfo(60);
        var plan = PlanFor(info, 24);
        plan.Width = info.Width / 2;
        plan.Height = info.Height / 2;
        plan.HdrVideoFilter = "zscale=t=linear,tonemap=hable";

        var args = FfmpegArguments.Build(info, plan, "cikti.mp4", 0, null);
        var vf = args.IndexOf("-vf");

        Assert.Equal($"scale={plan.Width}:{plan.Height}:flags=lanczos,zscale=t=linear,tonemap=hable,fps=24", args[vf + 1]);
    }

    [Fact]
    public void Parca_yolu_da_ayni_filtreyi_tasir()
    {
        var info = SourceInfo(60);
        var args = FfmpegArguments.BuildSegment(info, PlanFor(info, 15), 1.0, 2.0, "cikti.mp4");

        Assert.DoesNotContain("-r", args);
        Assert.Contains("fps=15", args);
    }

    private static MediaInfo SourceInfo(double fps) => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 4_000_000,
        DurationSeconds = FpsDropClips.DurationSeconds,
        Width = 320,
        Height = 240,
        Fps = fps,
        VideoCodec = "h264",
        TotalBitrateBps = 800_000
    };

    // --- K1: sapma gercek dosyada ---------------------------------------------

    [FfmpegFact]
    public void Cikti_kare_sayisi_ve_suresi_kaynakla_ortusur()
    {
        Assert.True(_clips.Ready, "olcum klipleri uretilemedi");
        Log($"# K1 kare sayisi ve sure — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        foreach (var (rate, source) in _clips.Sources)
        {
            var info = Probe(source);
            foreach (var target in new[] { 15.0, 24.0, 30.0 })
            {
                if (target >= info.Fps - 0.01) continue;

                var output = Path.Combine(_clips.Directory, $"k1-{rate.Replace('/', '_')}-{target}.mp4");
                var plan = PlanFor(info, target);
                Assert.Equal(0, Run(FfmpegArguments.Build(info, plan, output, 0, null)));

                var frames = FrameCount(output);
                var seconds = VideoSeconds(output);
                var expected = (int)Math.Round(info.DurationSeconds * target);

                Log($"kaynak {rate} ({info.Fps:0.###} fps, {info.DurationSeconds:0.###} sn) -> hedef {target:0.###}: "
                    + $"kare {frames} (beklenen {expected}), sure {seconds:0.###} sn, sapma {seconds - info.DurationSeconds:+0.###;-0.###;0} sn");

                Assert.InRange(frames, expected - 1, expected + 1);
                Assert.InRange(seconds - info.DurationSeconds, -1.5 / target, 1.5 / target);
            }
        }
    }

    // --- K2: ses eszamani -----------------------------------------------------

    [FfmpegFact]
    public void Goruntu_sese_gore_kaymaz()
    {
        Assert.True(_clips.Ready, "olcum klipleri uretilemedi");
        Log($"# K2 ses eszamani — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        foreach (var (rate, source) in _clips.Sources)
        {
            var info = Probe(source);
            var output = Path.Combine(_clips.Directory, $"k2-{rate.Replace('/', '_')}.mp4");
            var plan = PlanFor(info, 15.0);
            Assert.Equal(0, Run(FfmpegArguments.Build(info, plan, output, 0, null)));

            var video = VideoSeconds(output);
            var audio = AudioSeconds(output);
            Log($"kaynak {rate}: goruntu {video:0.###} sn, ses {audio:0.###} sn, fark {video - audio:+0.###;-0.###;0} sn");

            Assert.InRange(Math.Abs(video - audio), 0, 1.5 / 15.0);
        }
    }

    // --- K2: boyut ve tahmin --------------------------------------------------

    [FfmpegFact]
    public void Iki_gecisli_cikti_boyut_tahminini_asmaz()
    {
        Assert.True(_clips.Ready, "olcum klipleri uretilemedi");
        Log($"# K2 boyut tahmini — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        var source = _clips.Sources["30"];
        var info = Probe(source);
        var plan = PlanFor(info, 15.0, mode: "2pass");
        var output = Path.Combine(_clips.Directory, "k2-boyut.mp4");
        var log = Path.Combine(_clips.Directory, "gecis");

        Assert.Equal(0, Run(FfmpegArguments.Build(info, plan, output, 1, log)));
        Assert.Equal(0, Run(FfmpegArguments.Build(info, plan, output, 2, log)));

        var estimate = PlanCalculator.EstimatedMb(plan, info.DurationSeconds)!.Value;
        var actual = new FileInfo(output).Length / 1024.0 / 1024.0;
        Log($"tahmin {estimate:0.###} MB, gercek {actual:0.###} MB, oran {actual / estimate:0.###}");

        Assert.InRange(actual / estimate, 0.5, 1.15);
    }

    // --- K2: iki gecis ayni kareyi gorur --------------------------------------

    [FfmpegFact]
    public void Ilk_gecis_ile_ikinci_gecis_ayni_kare_sayisini_gorur()
    {
        Assert.True(_clips.Ready, "olcum klipleri uretilemedi");
        Log($"# K2 iki gecis — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        var info = Probe(_clips.Sources["30"]);
        var plan = PlanFor(info, 15.0, mode: "2pass");
        var output = Path.Combine(_clips.Directory, "k2-gecis.mp4");
        var log = Path.Combine(_clips.Directory, "gecis2");

        var first = FramesReported(FfmpegArguments.Build(info, plan, output, 1, log));
        Assert.Equal(0, Run(FfmpegArguments.Build(info, plan, output, 2, log)));
        var second = FrameCount(output);

        Log($"ilk gecis {first} kare, ikinci gecis {second} kare");
        Assert.Equal(first, second);
    }

    /// <summary>Ilk gecis dosyaya yazmaz; kare sayisi ffmpeg'in kendi ozetinden okunur.</summary>
    private static int FramesReported(IReadOnlyList<string> args)
    {
        using var process = new Process { StartInfo = StartInfo(ToolLocator.Ffmpeg, args) };
        process.Start();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);

        var match = System.Text.RegularExpressions.Regex.Match(stderr, @"frame=\s*(\d+)", System.Text.RegularExpressions.RegexOptions.RightToLeft);
        Assert.True(match.Success, "ffmpeg ozetinde kare sayisi yok");
        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    // --- K2: donanim yollari --------------------------------------------------

    [FfmpegFact]
    public void Donanim_kodlayicilari_da_ayni_kare_sayisini_uretir()
    {
        Assert.True(_clips.Ready, "olcum klipleri uretilemedi");
        Log($"# K2 donanim yollari — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        var info = Probe(_clips.Sources["30"]);
        var expected = (int)Math.Round(info.DurationSeconds * 15.0);
        var tried = 0;

        foreach (var codec in new[] { "h264_nvenc", "hevc_nvenc", "av1_nvenc", "h264_qsv", "h264_amf" })
        {
            if (!EncoderCapabilities.Instance.WorksAsEncoder(codec)) { Log($"{codec}: yok, atlandi"); continue; }

            var output = Path.Combine(_clips.Directory, $"k2-{codec}.mp4");
            var plan = PlanFor(info, 15.0, codec, "2pass");
            if (Run(FfmpegArguments.Build(info, plan, output, 0, null)) != 0) { Log($"{codec}: kodlama basarisiz, atlandi"); continue; }

            tried++;
            var frames = FrameCount(output);
            Log($"{codec}: kare {frames} (beklenen {expected})");
            Assert.InRange(frames, expected - 1, expected + 1);
        }

        Log($"denenen donanim kodlayici: {tried}");
    }
}
