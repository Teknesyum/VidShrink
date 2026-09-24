using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Cli;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// "Program ne diyorsa onu yapıyor" turu, CLI ve çalıştırıcı payı
/// (<c>.calisma/yalan-yok/envanter.md</c> maddeleri 4, 11, 12, 23, 24, 26, 31, 35, 38, 50, 51).
/// Her ölçü davranışı ya da kullanıcının okuduğu metni ölçer; olumsuz kontrol aynı testte,
/// doğru girdinin hâlâ geçtiğini gösterir.
/// </summary>
public sealed class YalanYokCliTests
{
    private static readonly CliText Tr = CliText.ForLanguage("tr");
    private static readonly CliText En = CliText.ForLanguage("en");

    [Fact]
    public void AciBayragiKaldirildi()
    {
        Assert.Equal("error.unknown-option", CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--aci", "2" }).ErrorKey);
        Assert.Equal("error.unknown-option", CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--angle", "2" }).ErrorKey);
        Assert.Null(CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--baslik", "2" }).ErrorKey);
        Assert.DoesNotContain("--aci", Tr["help"]);
        Assert.DoesNotContain("--angle", En["help"]);
    }

    [Theory]
    [InlineData("cikti.avi")]
    [InlineData("cikti.MKVX")]
    [InlineData("cikti")]
    public void BilinmeyenCiktiUzantisiReddediliyor(string output)
    {
        var parsed = CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--cikti", output });
        Assert.Equal("error.bad-output-extension", parsed.ErrorKey);
        Assert.Equal(output, parsed.ErrorArgument);
    }

    [Theory]
    [InlineData("cikti.mp4")]
    [InlineData("cikti.MKV")]
    [InlineData("cikti.webm")]
    [InlineData("cikti.mov")]
    public void BilinenCiktiUzantisiGeciyor(string output)
    {
        var parsed = CliParser.Parse(new[] { "plan", "a.mp4", "--hedef", "25MB", "--cikti", output });
        Assert.Null(parsed.ErrorKey);
        Assert.Equal(output, parsed.Request!.Output);
    }

    [Fact]
    public void IzleninCiktiKlasoruUzantiDenetimineTakilmiyor()
        => Assert.Null(CliParser.Parse(new[] { "izle", "gelen", "--cikti", "giden", "--hedef", "25MB" }).ErrorKey);

    [Fact]
    public void YardimCiktiUzantisininSabitOlmadiginiSoyluyor()
    {
        Assert.DoesNotContain("_shrunk.mp4", Tr["help"]);
        Assert.DoesNotContain("_shrunk.mp4", En["help"]);
        Assert.Contains(".webm", En["help"]);
        Assert.Contains("keeps its own extension", En["help"]);
        Assert.Contains("uzantısını korur", Tr["help"]);
    }

    [Theory]
    [InlineData("--crf", "20")]
    [InlineData("--profil", "whatsapp")]
    [InlineData("--kes", "0-10")]
    [InlineData("--suzgec", "crop=10:10:0:0")]
    [InlineData("--yak", "1")]
    [InlineData("--ses-normal", null)]
    public void IzleTekDosyaSecenegineGecersizDiyor(string option, string? value)
    {
        var args = new List<string> { "izle", "gelen", "--cikti", "giden", "--hedef", "25MB", option };
        if (value is not null) args.Add(value);
        var parsed = CliParser.Parse(args);
        Assert.Equal("error.not-in-watch", parsed.ErrorKey);
        Assert.Equal(option, parsed.ErrorArgument);
        Assert.Contains("izle", Tr.Format("error.not-in-watch", option));
    }

    [Fact]
    public void IzledeGercektenBilinmeyenSecenekBilinmeyenKaliyor()
    {
        Assert.Equal("error.unknown-option", CliParser.Parse(new[] { "izle", "gelen", "--cikti", "giden", "--hedef", "25MB", "--uydurma" }).ErrorKey);
        Assert.Null(CliParser.Parse(new[] { "kucult", "a.mp4", "--hedef", "25MB", "--crf", "20" }).ErrorKey);
    }

    [Fact]
    public void IzleninKabulEtmedigiSeceneklerParserdaTanimli()
    {
        foreach (var option in CliParser.NotInWatch)
        {
            var args = new List<string> { "kucult", "a.mp4", "--hedef", "25MB", option };
            var parsed = CliParser.Parse(args);
            Assert.NotEqual("error.unknown-option", parsed.ErrorKey);
        }
    }

    private static CliDecision Karar(MediaInfo kaynak, PlanOptions secenek, string cikti)
    {
        var sonuc = PlanCalculator.BuildDetailed(kaynak, secenek, null);
        return new CliDecision(kaynak, secenek, sonuc, sonuc.Profile, null, null, cikti, Array.Empty<string>());
    }

    private static MediaInfo BuyukKaynak() => new()
    {
        FilePath = "kaynak.mp4",
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        FileSizeBytes = 200_000_000,
        TotalBitrateBps = 26_000_000
    };

    private static MediaInfo KucukKaynak() => new()
    {
        FilePath = "kaynak.mov",
        DurationSeconds = 60,
        Width = 1280,
        Height = 720,
        Fps = 30,
        VideoCodec = "h264",
        FileSizeBytes = 5_000_000,
        TotalBitrateBps = 650_000
    };

    [Fact]
    public void PlanKodlayiciYedeginiInsanDiliyleYaziyor()
    {
        var karar = Karar(BuyukKaynak(), new PlanOptions { TargetMb = 25 }, "cikti.mp4");
        karar.Plan.ReasonCodes.Add(new ReasonNote(ReasonCode.EncoderFallback,
            RequestedCodec: "libsvtav1", FallbackCodec: "libx265", FallbackCause: EncoderFallbackCause.NotInBuild));
        var istek = new CliRequest { Command = CliCommand.Plan, Input = "kaynak.mp4" };

        var turkce = CliApp.PlanText(istek, karar, Tr);
        var ingilizce = CliApp.PlanText(istek, karar, En);

        Assert.Contains("libsvtav1 kodlayıcısı bu ffmpeg derlemesinde yok, kodlama libx265 ile yapılıyor", turkce);
        Assert.Contains("the libsvtav1 encoder is not part of this ffmpeg build, so encoding uses libx265", ingilizce);
    }

    [Fact]
    public void YedekYoksaKodlayiciNotuYok()
    {
        var karar = Karar(BuyukKaynak(), new PlanOptions { TargetMb = 25 }, "cikti.mp4");
        karar.Plan.ReasonCodes.RemoveAll(note => note.Code == ReasonCode.EncoderFallback);
        var istek = new CliRequest { Command = CliCommand.Plan, Input = "kaynak.mp4" };

        Assert.DoesNotContain("encoding uses", CliApp.PlanText(istek, karar, En));
    }

    [Fact]
    public void KucultMetniGercekKodlayiciyiVeYedekNedeniniYaziyor()
    {
        var karar = Karar(BuyukKaynak(), new PlanOptions { TargetMb = 25 }, "cikti.mp4");
        karar.Plan.ReasonCodes.Add(new ReasonNote(ReasonCode.EncoderFallback,
            RequestedCodec: "libvpx-vp9", FallbackCodec: "libx264", FallbackCause: EncoderFallbackCause.NotWorking));
        var kullanilan = karar.Plan.Clone();
        kullanilan.Codec = "libx264";
        karar.Plan.Codec = "libvpx-vp9";
        var sonuc = new EncodeResult(true, "cikti.mp4", 24.1, kullanilan, 1, null);

        var metin = CliApp.ShrinkText(karar, sonuc, TimeSpan.FromSeconds(12), null, En);

        Assert.Contains("Encoder:   libx264", metin);
        Assert.DoesNotContain("Encoder:   libvpx-vp9", metin);
        Assert.Contains("the libvpx-vp9 encoder could not be used on this machine, so encoding uses libx264", metin);
    }

    /// <summary>
    /// Koşucu, teslim ettiği dosyanın hangi denemeden geldiğini taşır. İki senaryo tek iş parçacıklı
    /// x264 ile belirlenimci: ilki bant altında kalan 2. denemeyi yedekler, 3. deneme hedefi aşınca
    /// yedeği teslim eder; ikincisi bantta biten 1. denemeden sonra küçük çıkan bütçe doldurmayı atar.
    /// </summary>
    [Theory]
    [InlineData(0.06, 8, "medium", 3, 2)]
    [InlineData(0.2, 800, "ultrafast", 2, 1)]
    public async Task KosucuTeslimEdilenDenemeninNumarasiniTasiyor(double hedefMb, int kbit, string preset, int denemeler, int teslim)
    {
        if (!ToolLocator.IsAvailable(out _)) return;
        var klasor = Path.Combine(TestPaths.OutputRoot, "yalan-yok-teslim", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = Path.Combine(klasor, "kaynak.mp4");
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ToolLocator.Ffmpeg,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in new[] { "-y", "-f", "lavfi", "-i", "testsrc=size=320x240:rate=10:duration=4", "-c:v", "libx264", "-crf", "18", "-g", "10", "-pix_fmt", "yuv420p", kaynak })
                psi.ArgumentList.Add(arg);
            using (var surec = System.Diagnostics.Process.Start(psi)!)
            {
                var bosalt = Task.WhenAll(surec.StandardOutput.ReadToEndAsync(), surec.StandardError.ReadToEndAsync());
                await surec.WaitForExitAsync();
                await bosalt;
            }
            var info = new MediaInfo
            {
                FilePath = kaynak, FileSizeBytes = new FileInfo(kaynak).Length, DurationSeconds = 4,
                Width = 320, Height = 240, Fps = 10, VideoCodec = "h264", TotalBitrateBps = 400_000
            };
            var plan = new EncodePlan
            {
                Codec = "libx264", Mode = "2pass", VideoBitrateK = kbit, AudioCodec = null, AudioBitrateK = 0,
                Width = 320, Height = 240, Fps = 10, Preset = preset, ExtraArgs = new List<string> { "-threads", "1" }
            };
            var cikti = Path.Combine(klasor, "cikti.mp4");

            var sonuc = await new EncodeRunner().RunAsync(info, plan, cikti, targetMb: hedefMb, progress: null, fillPolicy: FillPolicy.FillTarget);

            var iz = string.Join(" | ", sonuc.Trace!.Select(a => $"{a.Number}:{a.Branch}:{a.VideoBitrateK}k:{a.ActualMb:0.#####}"));
            Assert.True(sonuc.Success, iz);
            Assert.True(denemeler == sonuc.Attempts, iz);
            Assert.True(teslim == sonuc.DeliveredAttemptNumber, $"bildirilen {sonuc.DeliveredAttemptNumber}: {iz}");
            var teslimIzi = sonuc.Trace!.First(a => a.Number == teslim);
            Assert.Equal(teslimIzi.ActualMb, Megabayt.Oku(new FileInfo(cikti).Length), 6);
            Assert.Equal(teslimIzi.VideoBitrateK, sonuc.PlanUsed.VideoBitrateK);
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }

    [Fact]
    public void KucultMetniTeslimEdilenDenemeyiAyriYaziyor()
    {
        var karar = Karar(BuyukKaynak(), new PlanOptions { TargetMb = 25 }, "cikti.mp4");
        var yedekten = new EncodeResult(true, "cikti.mp4", 22, karar.Plan, 3, null) { DeliveredAttempt = 2 };
        var sonuncu = new EncodeResult(true, "cikti.mp4", 22, karar.Plan, 3, null);

        Assert.Contains("Attempts:  3 (delivered: attempt 2)", CliApp.ShrinkText(karar, yedekten, TimeSpan.FromSeconds(1), null, En));
        var duz = CliApp.ShrinkText(karar, sonuncu, TimeSpan.FromSeconds(1), null, En);
        Assert.Contains("Attempts:  3" + Environment.NewLine, duz);
        Assert.DoesNotContain("delivered: attempt", duz);
    }

    [Fact]
    public void KopyalamaKaynaginKabindaKaldiginiSoyluyor()
    {
        var karar = Karar(KucukKaynak(), new PlanOptions { TargetMb = 25 }, "cikti.mkv");
        Assert.Equal(EncodeMode.PassThrough, karar.Plan.ModeEnum);
        var istek = new CliRequest { Command = CliCommand.Plan, Input = "kaynak.mov" };

        var plan = CliApp.PlanText(istek, karar, En);
        Assert.Contains("Output:    cikti.mov", plan);
        Assert.Contains("stays in its own container (.mov) and the requested .mkv is not applied", plan);

        var sonuc = new EncodeResult(true, "cikti.mov", 4.8, karar.Plan, 1, null);
        Assert.Contains("kendi kabında (.mov) kalıyor, istenen .mkv uygulanmıyor", CliApp.ShrinkText(karar, sonuc, TimeSpan.FromSeconds(1), null, Tr));
    }

    [Fact]
    public void KopyalamaAyniKaptaysaNotYok()
    {
        var karar = Karar(KucukKaynak(), new PlanOptions { TargetMb = 25 }, "cikti.mov");
        var istek = new CliRequest { Command = CliCommand.Plan, Input = "kaynak.mov" };
        Assert.DoesNotContain("is not applied", CliApp.PlanText(istek, karar, En));

        var kodlanan = Karar(BuyukKaynak(), new PlanOptions { TargetMb = 25 }, "cikti.mkv");
        Assert.NotEqual(EncodeMode.PassThrough, kodlanan.Plan.ModeEnum);
        Assert.DoesNotContain("is not applied", CliApp.PlanText(new CliRequest { Command = CliCommand.Plan, Input = "kaynak.mp4" }, kodlanan, En));
    }

    [Fact]
    public void KopyalamaYoluCalistiricininYazdigiYolla()
    {
        Assert.Equal("cikti.mov", EncodeRunner.PassThroughPath("kaynak.mov", "cikti.mkv"));
        Assert.Equal("cikti.mkv", EncodeRunner.PassThroughPath("kaynak.MKV", "cikti.mkv"));
        Assert.Equal("cikti.mp4", EncodeRunner.PassThroughPath("kaynak", "cikti.mp4"));
    }

    [Fact]
    public void KalanSuresiDenemeyeAitOldugunuSoyluyor()
    {
        Assert.Contains("bu denemede", Tr.Format("progress.encode", 40, "pass 1/2 (attempt 2)", "01:00"));
        Assert.Contains("left in this attempt", En.Format("progress.encode", 40, "pass 1/2 (attempt 2)", "01:00"));
        foreach (var (dil, kelime) in new[] { ("en", "attempt"), ("de", "Versuch"), ("ar", "محاولة") })
            Assert.Contains(kelime, Strings.GetIn(dil, "main.output.remaining"), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Türkçe etiket dar pencerede iki sütunu tutan bütçeye sığmıyor ("Denemede" bile ızgarayı tek
    /// sütuna indiriyor), "Kalan" kalıyor; denemeyi hemen üstteki aşama hücresi söylüyor.
    /// </summary>
    [Fact]
    public void AsamaMetniDenemeyiSoyluyor()
    {
        var asama = new EncodeStage(1, 2, 2).ToString();
        Assert.Contains("attempt 2", asama);
        Assert.Contains("deneme 2", MainWindow.LocalizeStageIn(asama, "tr"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HedefUstuAslaVerilmezDenmiyor()
    {
        foreach (var (dil, yasak) in new[] { ("tr", "asla"), ("en", "never"), ("de", " nie "), ("ar", "أبداً") })
        {
            var metin = Strings.GetIn(dil, "main.run.over-ceiling");
            Assert.DoesNotContain(yasak, metin, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("{1}", metin);
        }
        Assert.Contains("not accepted", Strings.GetIn("en", "main.run.over-ceiling"));
    }

    [Fact]
    public void KopyalamaGerekcesiKabinKorundugunuSoyluyor()
    {
        foreach (var (dil, kelime) in new[] { ("tr", "kendi kabında"), ("en", "own container"), ("de", "eigenen Container"), ("ar", "حاويته") })
            Assert.Contains(kelime, Strings.GetIn(dil, "main.reason.source-under-target"));
    }

    private static string Soyle(string key) => "<" + key + ">";

    [Fact]
    public async Task BaglantiKopyalanincaBasariSoyleniyor()
    {
        string? yazilan = null;
        var durum = await MainWindow.CopyShareLinkAsync(text => { yazilan = text; return Task.CompletedTask; }, "https://ornek/x", Soyle);
        Assert.Equal("https://ornek/x", yazilan);
        Assert.Equal("<settings.share.link-copied>", durum);
    }

    [Fact]
    public async Task PanoYazamazsaHataSoyleniyor()
    {
        var durum = await MainWindow.CopyShareLinkAsync(_ => throw new InvalidOperationException("kilitli"), "https://ornek/x", Soyle);
        Assert.Equal("<main.ai.clipboard-failed>: kilitli", durum);
    }

    [Fact]
    public async Task PanoYoksaSessizKalinmiyor()
    {
        var durum = await MainWindow.CopyShareLinkAsync(null, "https://ornek/x", Soyle);
        Assert.Equal("<main.ai.clipboard-failed>: <main.ai.no-clipboard>", durum);
    }

    [Fact]
    public void BaglantiKopyalandiIletisiHerDilde()
    {
        foreach (var dil in new[] { "tr", "en", "de", "ar" })
            Assert.False(string.IsNullOrWhiteSpace(Strings.GetIn(dil, "settings.share.link-copied")));
        Assert.Equal("The link was copied.", Strings.GetIn("en", "settings.share.link-copied"));
    }
}
