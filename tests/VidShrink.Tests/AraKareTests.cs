using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// ARC-P4, resim dizisi bırakma ve ara kare: Dönüştür sekmesine bırakılan numaralı resimler ya da klasör
/// <see cref="DroppedMedia.Sequence(IEnumerable{string})"/> ile tek <c>image2</c> kaynağı olur (Küçült'te olmaz);
/// "ara kare üret" kutusu kare hızı değişiminde <c>fps=</c> yerine <c>minterpolate=fps=N</c> yazar, ffmpeg yapısında
/// süzgeç yoksa gizlenir. Canlı kol 64x48 10 karelik klibi <c>-threads 2</c> ile 20 kareye çıkarır.
/// Klasör <c>.calisma/ara-kare-*</c>, test siler.
/// </summary>
public sealed class AraKareTests
{
    private static readonly MediaInfo Video = new()
    {
        FilePath = @"C:\media\kaynak.mp4",
        FileSizeBytes = 5_000_000L,
        DurationSeconds = 10,
        Width = 1280,
        Height = 720,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 4_000_000,
        AudioCodec = "aac",
        AudioChannels = 2
    };

    private static string Komut(ConversionPlan plan) => string.Join(' ', ConversionArguments.Build(Video, plan, @"C:\o\cikti." + plan.Container));

    [Fact]
    public void AraKareAcikkenFpsYerineMinterpolateYazilir()
    {
        var acik = Komut(new ConversionPlan { Container = "mp4", Fps = 60, Interpolate = true });
        var kapali = Komut(new ConversionPlan { Container = "mp4", Fps = 60 });

        Assert.Contains("minterpolate=fps=60", acik, StringComparison.Ordinal);
        Assert.DoesNotContain("fps=60,", acik.Replace("minterpolate=fps=60", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.DoesNotContain("minterpolate", kapali, StringComparison.Ordinal);
        Assert.Contains("fps=60", kapali, StringComparison.Ordinal);
    }

    [Fact]
    public void KaynakHizindaVeTavanKipindeAraKareYazilmaz()
    {
        Assert.DoesNotContain("minterpolate", Komut(new ConversionPlan { Container = "mp4", Fps = 30, Interpolate = true }), StringComparison.Ordinal);
        Assert.DoesNotContain("minterpolate", Komut(new ConversionPlan { Container = "mp4", Interpolate = true }), StringComparison.Ordinal);
        Assert.DoesNotContain("minterpolate",
            Komut(new ConversionPlan { Container = "mp4", Fps = 60, Interpolate = true, FrameRate = FrameRateMode.Peak }), StringComparison.Ordinal);
    }

    [Fact]
    public void GifYoluAraKareyiTasir()
    {
        var plan = new ConversionPlan { Container = "gif", Fps = 15, Interpolate = true };
        var palet = string.Join(' ', ConversionArguments.Build(Video, plan, @"C:\o\palet.png"));
        var gif = string.Join(' ', ConversionArguments.Build(Video, plan, @"C:\o\cikti.gif", @"C:\o\palet.png"));

        Assert.Contains("minterpolate=fps=15", palet, StringComparison.Ordinal);
        Assert.Contains("minterpolate=fps=15", gif, StringComparison.Ordinal);
    }

    [Fact]
    public void BirakilanResimlerVeKlasorDiziOlur()
    {
        var klasor = YeniKlasor();
        try
        {
            var kareler = Path.Combine(klasor, "kareler");
            Directory.CreateDirectory(kareler);
            foreach (var n in new[] { "0002", "0001", "0003" }) File.WriteAllBytes(Path.Combine(kareler, "kare_" + n + ".png"), Array.Empty<byte>());
            File.WriteAllBytes(Path.Combine(kareler, "notlar.txt"), Array.Empty<byte>());

            var dosyadan = DroppedMedia.Sequence(Directory.GetFiles(kareler));
            var klasorden = DroppedMedia.Sequence(new[] { kareler });

            Assert.NotNull(dosyadan);
            Assert.Equal(3, dosyadan!.Count);
            Assert.EndsWith("kare_0001.png", dosyadan.Files[0], StringComparison.Ordinal);
            Assert.NotNull(klasorden);
            Assert.Equal(dosyadan.Pattern, klasorden!.Pattern);
            Assert.Null(DroppedMedia.Sequence(new[] { Path.Combine(kareler, "kare_0001.png") }));
            Assert.Null(DroppedMedia.Sequence(new[] { Path.Combine(klasor, "yok") }));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void DonusturSekmesineBirakilanDiziYuklenirKucultteYuklenmez()
    {
        var (donustur, kucult) = Pencerede(kareler =>
        {
            var dosyalar = Directory.GetFiles(kareler).OrderBy(a => a, StringComparer.Ordinal).ToArray();
            return AppHost.Run(() =>
            {
                var sonuc = new List<int?>();
                foreach (var donusturSekmesi in new[] { true, false })
                {
                    var window = YeniPencere(kareler);
                    try
                    {
                        if (donusturSekmesi) window.TabConvert.IsSelected = true;
                        Birak(window, dosyalar);
                        sonuc.Add(window.LoadedSequence?.Count);
                    }
                    finally { window.Close(); }
                }
                return (sonuc[0], sonuc[1]);
            });
        });

        Assert.Equal(4, donustur);
        Assert.Null(kucult);
    }

    [Fact]
    public void DonusturSekmesineBirakilanKlasorDiziOlur()
    {
        var yuklu = Pencerede(kareler => AppHost.Run(() =>
        {
            var window = YeniPencere(kareler);
            try
            {
                window.TabConvert.IsSelected = true;
                Birak(window, kareler);
                return window.LoadedSequence?.Count;
            }
            finally { window.Close(); }
        }));

        Assert.Equal(4, yuklu);
    }

    [Fact]
    public void YoklamadaSuzgecYoksaKutuGizliVarsaGorunur()
    {
        var (yoklamasiz, suzgecsiz, suzgecsizPlan, suzgecli, plan) = Pencerede(kareler => AppHost.Run(() =>
        {
            var window = YeniPencere(kareler);
            try
            {
                window.LoadWithoutProbing(Video.FilePath, Video);
                var ilk = window.ChkConvertInterpolate.IsVisible;
                window.CmbConvertFps.SelectedIndex = 1;
                window.ChkConvertInterpolate.IsChecked = true;
                window.ApplyHardwareVerdict(Yoklama(false), false, HardwareVerdict.NotProbed);
                var gizli = window.ChkConvertInterpolate.IsVisible;
                var gizliPlan = window.CurrentConversionPlan().Interpolate;
                window.ApplyHardwareVerdict(Yoklama(true), false, HardwareVerdict.NotProbed);
                var gorunur = window.ChkConvertInterpolate.IsVisible;
                window.ChkConvertInterpolate.IsChecked = true;
                return (ilk, gizli, gizliPlan, gorunur, window.CurrentConversionPlan());
            }
            finally { window.Close(); }
        }));

        Assert.True(yoklamasiz);
        Assert.False(suzgecsiz);
        Assert.False(suzgecsizPlan);
        Assert.True(suzgecli);
        Assert.True(plan.Interpolate);
        Assert.Equal(60, plan.Fps);
    }

    [Fact]
    public void AraKareAnahtarlariKirkIkiDildeCevrili()
    {
        var anahtarlar = new[] { "main.convert.interpolate", "main.convert.interpolate.tip" };
        Assert.Equal(42, Locales.Languages.Count);
        var eksik = new List<string>();
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Domain(language, "main");
            foreach (var anahtar in anahtarlar)
                if (!values.TryGetValue(anahtar, out var value) || string.IsNullOrWhiteSpace(value)) eksik.Add($"{language}: {anahtar} yok");
            if (values.TryGetValue("main.convert.interpolate.tip", out var tip) && !tip.TrimStart().StartsWith('•'))
                eksik.Add($"{language}: ipucu madde biçimli değil");
        }

        Assert.True(eksik.Count == 0, string.Join(Environment.NewLine, eksik));
    }

    [FfmpegFact]
    public async Task AraKareCiktisiHedefHizaUyar()
    {
        var klasor = YeniKlasor();
        try
        {
            var kaynak = Path.Combine(klasor, "kaynak.mp4");
            var uret = await FfmpegRunner.RunAsync(new[]
            {
                "-hide_banner", "-y", "-nostdin", "-f", "lavfi", "-i", "testsrc2=size=64x48:rate=10",
                "-frames:v", "10", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-threads", "2", kaynak
            });
            Assert.True(uret.Ok, uret.StandardError);
            var info = await FfprobeClient.ProbeAsync(kaynak);

            var cikti = Path.Combine(klasor, "ara.mp4");
            var args = ConversionArguments.Build(info, new ConversionPlan { Container = "mp4", Fps = 20, Interpolate = true }, cikti).ToList();
            Assert.Contains(args, a => a.Contains("minterpolate=fps=20", StringComparison.Ordinal));
            args.InsertRange(args.Count - 1, new[] { "-threads", "2", "-filter_threads", "2" });
            args.Insert(2, "-nostdin");
            var sonuc = await FfmpegRunner.RunAsync(args);
            Assert.True(sonuc.Ok, sonuc.StandardError);

            var (paket, sure, hiz) = PaketVeSure(cikti);
            Assert.Equal("20/1", hiz);
            Assert.True(paket > 10, "ara kare uretilmedi: " + paket);
            Assert.Equal((int)Math.Round(sure * 20), paket);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    private const string SahteKodlayicilar = "Encoders:\n V..... = Video\n ------\n V....D libx264              libx264 H.264\n";

    private const string Suzgecler = """
        Filters:
          T.. = Timeline support
          S.. = Slice threading support
          A = Audio input/output
          V = Video input/output
         ... scale             V->V       Scale the input video size and/or convert the image format.
         ... fps               V->V       Force constant framerate.
        """;

    private const string AraKareliSuzgecler = Suzgecler + """

         ... minterpolate      V->V       Frame rate conversion using Motion Interpolation.
        """;

    private static EncoderCapabilities Yoklama(bool araKare)
        => EncoderCapabilities.Parse(SahteKodlayicilar, araKare ? AraKareliSuzgecler : Suzgecler, "ffmpeg version test\n");

    private static MainWindow YeniPencere(string kareler) => new()
    {
        SettingsPathOverride = Path.Combine(Path.GetDirectoryName(kareler)!, "settings.json"),
        SequenceProber = (_, _) => Task.FromResult<ImageSequenceFrame?>(new ImageSequenceFrame(64, 48, "png", "rgb24"))
    };

    private static void Birak(MainWindow window, params string[] yollar)
    {
        var aktarim = new DataTransfer();
        foreach (var yol in yollar) aktarim.Add(DataTransferItem.CreateFile(Directory.Exists(yol) ? Klasor(yol) : ParcaKanit.Dosya(yol)));
        window.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, aktarim, window, new Point(1, 1), KeyModifiers.None));
    }

    private static IStorageItem Klasor(string path)
    {
        var lookup = new Window().StorageProvider.TryGetFolderFromPathAsync(new Uri(Path.GetFullPath(path)));
        var saat = Stopwatch.StartNew();
        while (!lookup.IsCompleted && saat.Elapsed.TotalSeconds < 5)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        return lookup.GetAwaiter().GetResult() ?? throw new InvalidOperationException("yerel klasor depolama ogesine donmedi: " + path);
    }

    private static T Pencerede<T>(Func<string, T> govde)
    {
        var klasor = YeniKlasor();
        var kareler = Path.Combine(klasor, "kareler");
        Directory.CreateDirectory(kareler);
        foreach (var n in new[] { "001", "002", "003", "004" }) File.WriteAllBytes(Path.Combine(kareler, "kare_" + n + ".png"), Array.Empty<byte>());
        try { return govde(kareler); }
        finally
        {
            try { Directory.Delete(klasor, true); }
            catch (IOException) { }
        }
    }

    private static string YeniKlasor()
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null && !File.Exists(Path.Combine(dizin.FullName, "VidShrink.sln"))) dizin = dizin.Parent;
        var klasor = Path.Combine(dizin!.FullName, ".calisma", "ara-kare-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static (int Paket, double Sure, string Hiz) PaketVeSure(string dosya)
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
                     "-hide_banner", "-v", "error", "-count_packets", "-select_streams", "v:0",
                     "-show_entries", "stream=nb_read_packets,avg_frame_rate:format=duration", "-of", "default=nw=1", dosya
                 })
            psi.ArgumentList.Add(arg);

        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var metin = surec.StandardOutput.ReadToEnd();
        surec.WaitForExit(10_000);
        _ = hata.Result;

        int paket = 0;
        double sure = 0;
        var hiz = "";
        foreach (var satir in metin.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (satir.StartsWith("nb_read_packets=", StringComparison.Ordinal)) paket = int.Parse(satir[16..], CultureInfo.InvariantCulture);
            if (satir.StartsWith("avg_frame_rate=", StringComparison.Ordinal)) hiz = satir[15..];
            if (satir.StartsWith("duration=", StringComparison.Ordinal)) sure = double.Parse(satir[9..], CultureInfo.InvariantCulture);
        }
        return (paket, sure, hiz);
    }
}
