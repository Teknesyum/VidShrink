using System.Globalization;
using VidShrink.App;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// Uygulanan kare hızı kipi gerekçede söylenir: sabit kipte çıktının hızı, tavanlı kipte tavan. Sayı
/// argümana yazılanla aynı yerden gelir (<see cref="VideoFilterChain.FrameRateNote"/>); pencerenin satırı
/// <c>main.reason.frame-rate-*</c>, CLI'ınki <c>plan.frame-rate.*</c>. Davranış değişmez, yalnız söylenir.
/// </summary>
public sealed class KareHiziGerekceTests
{
    private static MediaInfo Hizli() => Kaynak(600, Video) with { FilePath = @"C:\Kayitlar\hizli.mp4", Fps = 60 };

    private static EncodePlan Kur(MediaInfo info, FrameRateMode kip, double? tavan = null)
    {
        var plan = PlanCalculator.Build(info, new PlanOptions
        {
            TargetMb = 300, AllowFpsDrop = false, AllowResolutionDrop = false, FrameRate = kip, MaxFps = tavan
        });
        Assert.NotEqual(EncodeMode.PassThrough, plan.ModeEnum);
        return plan;
    }

    private static double Sayi(string metin) => double.Parse(metin, CultureInfo.InvariantCulture);

    /// <summary>
    /// Notun sayısı komuta yazılan sayıdır: sabit kipte <c>-r</c>, tavanlı kipte <c>-enc_time_base</c>'in
    /// paydası. Otomatik kipte not yoktur (olumsuz kontrol); kopyalanan plana elle kip yazılsa da yoktur.
    /// </summary>
    [Fact]
    public void NotunSayisiKomutaYazilanSayi()
    {
        var info = Hizli();
        var sabit = Kur(info, FrameRateMode.Constant);
        var tavanli = Kur(info, FrameRateMode.Peak, 24);
        var otomatik = Kur(info, FrameRateMode.Auto);

        var sabitNot = VideoFilterChain.FrameRateNote(info, sabit);
        var tavanNot = VideoFilterChain.FrameRateNote(info, tavanli);

        Assert.NotNull(sabitNot);
        Assert.NotNull(tavanNot);
        Assert.Equal(FrameRateMode.Constant, sabitNot.Value.Mode);
        Assert.Equal(Sayi(Sonraki(FfmpegArguments.Build(info, sabit, "c.mp4", 2, "gecis"), "-r")), sabitNot.Value.Fps, 3);
        Assert.Equal(60, sabitNot.Value.Fps, 3);
        Assert.Equal(FrameRateMode.Peak, tavanNot.Value.Mode);
        Assert.Equal(Sayi(Sonraki(FfmpegArguments.Build(info, tavanli, "c.mp4", 2, "gecis"), "-enc_time_base").Split('/')[1]), tavanNot.Value.Fps, 3);
        Assert.Equal(24, tavanNot.Value.Fps, 3);
        Assert.Null(VideoFilterChain.FrameRateNote(info, otomatik));

        var kopya = PlanCalculator.Build(info, new PlanOptions { TargetMb = 100_000 });
        Assert.Equal(EncodeMode.PassThrough, kopya.ModeEnum);
        kopya.FrameRate = FrameRateMode.Constant;
        Assert.Null(VideoFilterChain.FrameRateNote(info, kopya));
    }

    /// <summary>
    /// Kaynaktan yüksek tavan hızı indirmez ama zaman tabanına yazılır; satır komuttaki tavanı söyler,
    /// planın hızını değil.
    /// </summary>
    [Fact]
    public void KaynaktanYuksekTavanKomuttakiTavaniSoyluyor()
    {
        var info = Hizli();
        var plan = Kur(info, FrameRateMode.Peak, 120);

        Assert.Equal(60, plan.Fps, 3);
        Assert.Equal("1/120", Sonraki(FfmpegArguments.Build(info, plan, "c.mp4", 2, "gecis"), "-enc_time_base"));
        Assert.Equal(120, VideoFilterChain.FrameRateNote(info, plan)!.Value.Fps, 3);
        Assert.Equal("Timing:    frame rate capped at 120 fps, variable", CliApp.FrameRateText(info, plan, CliText.ForLanguage("en")));
    }

    /// <summary>CLI satırının tam metni iki dilde; otomatik kipte satır yok.</summary>
    [Fact]
    public void CliSatiriIkiDildeTamMetin()
    {
        var info = Hizli();
        var sabit = Kur(info, FrameRateMode.Constant);
        var tavanli = Kur(info, FrameRateMode.Peak, 24);
        var tr = CliText.ForLanguage("tr");
        var en = CliText.ForLanguage("en");

        Assert.Equal("Kare hızı: sabit, 60 fps", CliApp.FrameRateText(info, sabit, tr));
        Assert.Equal("Timing:    constant frame rate, 60 fps", CliApp.FrameRateText(info, sabit, en));
        Assert.Equal("Kare hızı: en çok 24 fps, değişken", CliApp.FrameRateText(info, tavanli, tr));
        Assert.Equal("Timing:    frame rate capped at 24 fps, variable", CliApp.FrameRateText(info, tavanli, en));
        Assert.Null(CliApp.FrameRateText(info, Kur(info, FrameRateMode.Auto), en));
    }

    private static async Task<string> PlanMetni(string klasor, params string[] bayraklar)
    {
        var servisler = new CliServices
        {
            MissingTool = () => null,
            Probe = (_, _) => Task.FromResult(Hizli()),
            Availability = () => null,
            UserPresets = () => Array.Empty<PresetProfile>(),
        };
        var dosya = Path.Combine(klasor, "hizli.mp4");
        await File.WriteAllTextAsync(dosya, "x");
        var stdout = new StringWriter();
        var exit = await CliApp.RunAsync(new[] { "plan", dosya, "--olcumsuz", "--hedef", "300" }.Concat(bayraklar).ToArray(),
            stdout, new StringWriter(), CliText.ForLanguage("en"), servisler, CancellationToken.None);
        Assert.Equal(0, exit);
        return stdout.ToString();
    }

    /// <summary>
    /// <c>plan</c> çıktısı: <c>--cfr</c> ve <c>--pfr --fps N</c> kendi satırını yazar, bayraksız koşumda iki
    /// satır da yoktur (olumsuz kontrol).
    /// </summary>
    [Fact]
    public async Task PlanCiktisiKipiYaziyorBayraksizYazmiyor()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "kare-hizi-gerekce", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        try
        {
            var sabit = await PlanMetni(klasor, "--cfr");
            var tavanli = await PlanMetni(klasor, "--pfr", "--fps", "24");
            var bayraksiz = await PlanMetni(klasor);

            Assert.Contains("Timing:    constant frame rate, ", sabit, StringComparison.Ordinal);
            Assert.DoesNotContain("capped", sabit, StringComparison.Ordinal);
            Assert.Contains("Timing:    frame rate capped at 24 fps, variable", tavanli, StringComparison.Ordinal);
            Assert.DoesNotContain("Timing:", bayraksiz, StringComparison.Ordinal);
        }
        finally { Directory.Delete(klasor, true); }
    }

    /// <summary>
    /// Pencerenin gerekçe satırı: sabit ve tavanlı plan kendi cümlesini sayısıyla yazar (tr ve en), otomatik
    /// planda iki cümle de yoktur. Arayüzde kip denetimi yok; satır plandan okunur.
    /// </summary>
    [Fact]
    public void PencereninGerekceSatiriKipiSoyluyor()
    {
        var info = Hizli();
        var sabit = Kur(info, FrameRateMode.Constant);
        var tavanli = Kur(info, FrameRateMode.Peak, 24);
        var otomatik = Kur(info, FrameRateMode.Auto);
        var dosya = Path.Combine(TestPaths.OutputRoot, "kare-hizi-gerekce", "settings-" + Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(dosya)!);
        foreach (var eski in Directory.GetFiles(Path.GetDirectoryName(dosya)!, "settings-*.json")) File.Delete(eski);
        try
        {
            var (tr, en, bos) = AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = dosya };
                try
                {
                    window.LoadWithoutProbing(info.FilePath, info);
                    var ingilizce = (Sabit: window.ReasonLinesForTest(sabit).ToArray(), Tavan: window.ReasonLinesForTest(tavanli).ToArray());
                    window.UseTurkish();
                    return ((Sabit: window.ReasonLinesForTest(sabit).ToArray(), Tavan: window.ReasonLinesForTest(tavanli).ToArray()),
                        ingilizce, window.ReasonLinesForTest(otomatik).ToArray());
                }
                finally { window.Close(); }
            });

            static void Var(string[] satirlar, string beklenen)
                => Assert.Contains(satirlar, satir => string.Equals(satir, beklenen, StringComparison.OrdinalIgnoreCase));

            Var(tr.Sabit, "sabit kare hızı: 60");
            Var(tr.Tavan, "kare hızı tavanı: 24");
            Var(en.Sabit, "constant frame rate: 60");
            Var(en.Tavan, "frame rate capped at 24");
            Assert.DoesNotContain(tr.Sabit, satir => satir.Contains("kare hızı tavanı", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(tr.Tavan, satir => satir.Contains("sabit kare", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(bos, satir => satir.Contains("sabit kare", StringComparison.OrdinalIgnoreCase)
                || satir.Contains("kare hızı tavanı", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(dosya)) File.Delete(dosya);
        }
    }

    /// <summary>Anahtar tek fonksiyondan çıkar, otomatik kipte yoktur; iki anahtar 42 dilde dolu ve sayının yerini taşır.</summary>
    [Fact]
    public void AnahtarlarButunDillerdeSayiTasiyor()
    {
        Assert.Null(MainWindow.FrameRateNoteKey(FrameRateMode.Auto));
        var anahtarlar = new[] { MainWindow.FrameRateNoteKey(FrameRateMode.Constant)!, MainWindow.FrameRateNoteKey(FrameRateMode.Peak)! };
        Assert.Equal(2, anahtarlar.Distinct().Count());

        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            foreach (var anahtar in anahtarlar)
            {
                Assert.True(values.TryGetValue(anahtar, out var metin) && metin.Length > 0, language + ":" + anahtar);
                Assert.Contains("{0}", metin, StringComparison.Ordinal);
            }

            Assert.NotEqual(values[anahtarlar[0]], values[anahtarlar[1]]);
        }
    }
}
