using VidShrink.App;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// later 157: <c>afftdn</c> içermeyen bir ffmpeg derlemesinde Küçült'ün gürültü kutusu gizlenir ve plana geçmez;
/// <c>--ses-temizle</c> açık bir hatayla durur. Yoklama sahte <c>-filters</c> çıktısından okunur, ffmpeg çağrılmaz.
/// Yoklama okunamadıysa kutu görünür kalır: bilinmeyen, yok demek değildir.
/// </summary>
public sealed class GurultuSuzgeciYoklamaTests
{
    private const string Kodlayicilar = "Encoders:\n V..... = Video\n ------\n V....D libx264              libx264 H.264\n A....D aac                  AAC\n";

    private const string AfftdnsizSuzgecler = """
        Filters:
          T.. = Timeline support
          S.. = Slice threading support
          A = Audio input/output
          V = Video input/output
         ... scale             V->V       Scale the input video size and/or convert the image format.
         ... loudnorm          A->A       EBU R128 loudness normalization
         ... afftdnx           A->A       Not the filter.
        """;

    private const string AfftdnliSuzgecler = AfftdnsizSuzgecler + """

         TSC afftdn            A->A       Denoise audio samples using FFT.
        """;

    private static EncoderCapabilities Yoklama(bool afftdn)
        => EncoderCapabilities.Parse(Kodlayicilar, afftdn ? AfftdnliSuzgecler : AfftdnsizSuzgecler, "ffmpeg version test\n");

    private static readonly SourceStream Ses = new(1, StreamKind.Audio, "aac", "eng", Channels: 2, BitrateBps: 128_000, SampleRate: 44100);

    [Fact]
    public void SahteYoklamaSuzgeciTanir()
    {
        Assert.True(Yoklama(false).Loaded);
        Assert.False(Yoklama(false).HasFilter(AudioCaptureArguments.NoiseSuppressionFilterName));
        Assert.True(Yoklama(true).HasFilter(AudioCaptureArguments.NoiseSuppressionFilterName));
    }

    [Fact]
    public void SuzgecYokkenKutuGizlenirVePlanaGecmez()
    {
        var sonuc = AppHost.Run(() =>
        {
            var window = new MainWindow();
            try
            {
                window.LoadWithoutProbing("C:\\ornek\\film.mkv", Kaynak(600, Video, Ses) with { FilePath = "C:\\ornek\\film.mkv" });
                window.ChkAudioDenoise.IsChecked = true;
                window.ApplyHardwareVerdict(null, false, HardwareVerdict.NotProbed);
                var bilinmiyor = (window.ChkAudioDenoise.IsVisible, window.PlanOptionsForTest().AudioDenoise);
                window.ApplyHardwareVerdict(Yoklama(false), false, HardwareVerdict.NotProbed);
                var yok = (window.ChkAudioDenoise.IsVisible, window.PlanOptionsForTest().AudioDenoise);
                window.ApplyHardwareVerdict(Yoklama(true), false, HardwareVerdict.NotProbed);
                var var_ = (window.ChkAudioDenoise.IsVisible, window.PlanOptionsForTest().AudioDenoise);
                return (bilinmiyor, yok, var_);
            }
            finally { window.Close(); }
        });

        Assert.Equal((true, true), sonuc.bilinmiyor);
        Assert.Equal((false, false), sonuc.yok);
        Assert.Equal((true, true), sonuc.var_);
    }

    [Fact]
    public void SuzgecYokkenKaydedicininKutusuDaGizlenir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var window = new MainWindow();
            try
            {
                window.ApplyHardwareVerdict(Yoklama(false), false, HardwareVerdict.NotProbed);
                var kutu = window.RecorderPaneForTest.ChkNoiseSuppression;
                var sonradanAcilan = kutu.IsVisible;
                window.ApplyHardwareVerdict(Yoklama(true), false, HardwareVerdict.NotProbed);
                kutu.IsChecked = true;
                var var_ = (kutu.IsVisible, kutu.IsChecked);
                window.ApplyHardwareVerdict(Yoklama(false), false, HardwareVerdict.NotProbed);
                var yok = (kutu.IsVisible, kutu.IsChecked);
                return (sonradanAcilan, var_, yok);
            }
            finally { window.Close(); }
        });

        Assert.False(sonuc.sonradanAcilan);
        Assert.Equal((true, (bool?)true), sonuc.var_);
        Assert.Equal((false, (bool?)false), sonuc.yok);
    }

    private static string Klasor
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "gurultu-suzgeci-yoklama");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    [Theory]
    [InlineData("--ses-temizle", false, ExitCodes.Error, true)]
    [InlineData("--denoise", false, ExitCodes.Error, true)]
    [InlineData("--ses-temizle", true, ExitCodes.InBand, false)]
    [InlineData(null, false, ExitCodes.InBand, false)]
    public async Task CliBayragiSuzgecYokkenAcikHataVerir(string? bayrak, bool suzgecVar, int beklenen, bool ileti)
    {
        var kaynak = Path.Combine(Klasor, $"afftdn-{suzgecVar}-{bayrak ?? "yok"}.mkv");
        File.WriteAllText(kaynak, "x");
        try
        {
            var args = new List<string> { "plan", kaynak, "--hedef", "25MB", "--olcumsuz" };
            if (bayrak is not null) args.Add(bayrak);
            var sorulan = new List<string>();
            var stderr = new StringWriter();
            var tr = CliText.ForLanguage("tr");

            var kod = await CliApp.RunAsync(args, new StringWriter(), stderr, tr,
                new CliServices
                {
                    MissingTool = () => null,
                    Probe = (_, _) => Task.FromResult(Kaynak(600, Video, Ses) with { FilePath = kaynak }),
                    Availability = () => null,
                    HasFilter = ad =>
                    {
                        sorulan.Add(ad);
                        return suzgecVar;
                    }
                },
                CancellationToken.None);

            Assert.Equal(beklenen, kod);
            Assert.Equal(ileti, stderr.ToString().Contains(tr["error.no-afftdn"], StringComparison.Ordinal));
            Assert.Equal(bayrak is null ? Array.Empty<string>() : new[] { AudioCaptureArguments.NoiseSuppressionFilterName }, sorulan);
        }
        finally
        {
            KanitKapanisi.Kapat(Klasor, Path.GetFileName(kaynak));
        }
    }

    [Fact]
    public void HataMetniIkiDilde()
    {
        var tr = CliText.ForLanguage("tr");
        var en = CliText.ForLanguage("en");
        foreach (var dil in new[] { tr, en })
        {
            Assert.Contains("afftdn", dil["error.no-afftdn"]);
            Assert.Contains("--ses-temizle", dil["error.no-afftdn"]);
        }
        Assert.NotEqual(tr["error.no-afftdn"], en["error.no-afftdn"]);
    }
}
