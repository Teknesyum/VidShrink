using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Later 106, kayıt çerçevesi (<see cref="RecordingFrame"/>, <c>RecorderView.Cercevele.cs</c>): biten kayıt
/// ikinci bir ffmpeg geçişiyle düz renk, <c>gradients</c> geçişi ya da kullanıcının görseli üstüne oturur;
/// dolgu, yuvarlak köşe ve gölge seçilir. Saf kol argümanı ve maskeyi, canlı kol 160x90 lavfi klibini
/// gerçek ffmpeg ile (<c>-threads 2</c>) çerçeveleyip boyutu ve köşe/merkez piksellerini rawvideo ile okur.
/// Arayüz kolu sahte yoklama ve koşucuyla. Klasör <c>.calisma/worktree-agent-later-105-106/</c>, test siler.
/// </summary>
public sealed class KayitCercevesiTests
{
    private const int Tolerans = 40;

    private readonly ITestOutputHelper _output;

    public KayitCercevesiTests(ITestOutputHelper output) => _output = output;

    private static string Klasor()
    {
        var yol = Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-later-105-106", "cerceve-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(yol);
        return yol;
    }

    [Theory]
    [InlineData(160, 90, 32, 224, 154)]
    [InlineData(161, 91, 32, 226, 156)]
    [InlineData(1920, 1080, 0, 1920, 1080)]
    public void CiktiOlcusuDolguyuIkiYanaEklerCiftOlur(int w, int h, int p, int ew, int eh)
        => Assert.Equal((ew, eh), RecordingFrame.OutputSize(w, h, p));

    [Fact]
    public void GecersizOlcuVeDolguReddedilir()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RecordingFrame.OutputSize(0, 90, 32));
        Assert.Throws<ArgumentOutOfRangeException>(() => RecordingFrame.OutputSize(160, 90, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => RecordingFrame.OutputSize(160, 90, RecordingFrame.MaxPadding + 1));
    }

    [Theory]
    [InlineData("#FF65C6FF", "0x65C6FF")]
    [InlineData("#65c6ff", "0x65C6FF")]
    [InlineData("0x112233", "0x112233")]
    [InlineData(" 102030 ", "0x102030")]
    public void RenkFfmpegBicimineCevrilir(string girdi, string beklenen)
        => Assert.Equal(beklenen, RecordingFrame.Color(girdi));

    [Theory]
    [InlineData("#12345")]
    [InlineData("kirmizi")]
    [InlineData("#GG0000")]
    public void BozukRenkReddedilir(string girdi)
        => Assert.Throws<ArgumentException>(() => RecordingFrame.Color(girdi));

    [Fact]
    public void KoseYaricapiKisaKenarinYarisiniGecmez()
    {
        Assert.Equal(45, RecordingFrame.EffectiveCorner(160, 90, 400));
        Assert.Equal(0, RecordingFrame.EffectiveCorner(160, 90, -5));
        Assert.Equal(24, RecordingFrame.EffectiveCorner(160, 90, 24));
        Assert.Equal("if(between(X,32,191)*between(Y,32,121),0,255)", RecordingFrame.HoleAlpha(160, 90, 32, 0));
        Assert.Contains("576)", RecordingFrame.HoleAlpha(160, 90, 32, 24));
    }

    [Fact]
    public void ArgumanArkaPlanKaynaginiVeIsParcasiniTasir()
    {
        var duz = RecordingFrame.Arguments("k.mp4", "c.mp4", 160, 90, new FrameOptions(32, 24, false, FrameBackdrop.Solid("#FF102030")), 2).ToList();
        var gecis = RecordingFrame.Arguments("k.mp4", "c.webm", 160, 90, new FrameOptions(32, 24, true, FrameBackdrop.Gradient("#FF0000", "#0000FF"))).ToList();
        var gorsel = RecordingFrame.Arguments("k.mp4", "c.mkv", 160, 90, new FrameOptions(64, 0, false, FrameBackdrop.Image("arka.png"))).ToList();

        Assert.Contains("color=c=0x102030:s=224x154:r=1:d=1", duz);
        Assert.Equal("2", duz[duz.IndexOf("-threads") + 1]);
        Assert.Equal("2", duz[duz.IndexOf("-filter_complex_threads") + 1]);
        Assert.Equal("c.mp4", duz[^1]);
        Assert.Contains("+faststart", duz);
        Assert.DoesNotContain("[sh]", duz[duz.IndexOf("-filter_complex") + 1]);

        Assert.Contains(gecis, a => a.StartsWith("gradients=s=224x154:c0=0xFF0000:c1=0x0000FF:", StringComparison.Ordinal) && a.Contains(":x1=223:y1=153:"));
        Assert.Contains("[sh]", gecis[gecis.IndexOf("-filter_complex") + 1]);
        Assert.Equal("libvpx-vp9", gecis[gecis.IndexOf("-c:v") + 1]);
        Assert.DoesNotContain("-threads", gecis);

        Assert.Equal("arka.png", gorsel[gorsel.IndexOf("k.mp4") + 2]);
        Assert.Contains("scale=288:218:force_original_aspect_ratio=increase,crop=288:218", gorsel[gorsel.IndexOf("-filter_complex") + 1]);
        Assert.Equal("0:a?", gorsel[gorsel.LastIndexOf("-map") + 1]);
        Assert.Throws<ArgumentException>(() => RecordingFrame.BackdropInput(new FrameBackdrop(FrameBackdropKind.Image, "0x000000"), 10, 10));
    }

    [Fact]
    public void HedefKaynaginYanindaCakismasizAdAlir()
    {
        var kaynak = Path.Combine("d", "kayit.mp4");
        var dolu = new HashSet<string> { Path.Combine("d", "kayit-cerceveli.mp4") };

        Assert.Equal(Path.Combine("d", "kayit-cerceveli.mp4"), RecordingFrame.TargetFor(kaynak, _ => false));
        Assert.Equal(Path.Combine("d", "kayit-cerceveli-2.mp4"), RecordingFrame.TargetFor(kaynak, dolu.Contains));
    }

    [FfmpegFact]
    public async Task GercekKlipCercevelenirKoseArkaPlanMerkezVideoOlur()
    {
        var klasor = Klasor();
        try
        {
            var kaynak = Path.Combine(klasor, "kayit.mp4");
            await Uret(kaynak, "testsrc2=s=160x90:r=10:d=1");
            var kaynakKare = await Kare(kaynak, 160, 90);
            var arka = (R: 0x20, G: 0x40, B: 0xC0);
            var koseKaynak = Piksel(kaynakKare, 160, 4, 4);
            _output.WriteLine($"kaynak (4,4) = {koseKaynak}");
            Assert.True(Uzak(koseKaynak, arka), "kaynak köşesi arka planla aynı renk, ölçü ayırt edemez");

            var yuvarlak = Path.Combine(klasor, "yuvarlak.mp4");
            await Calistir(RecordingFrame.Arguments(kaynak, yuvarlak, 160, 90, new FrameOptions(32, 24, false, FrameBackdrop.Solid("#2040C0")), 2));
            var duz = Path.Combine(klasor, "duz.mp4");
            await Calistir(RecordingFrame.Arguments(kaynak, duz, 160, 90, new FrameOptions(32, 0, false, FrameBackdrop.Solid("#2040C0")), 2));

            var bilgi = await FfprobeClient.ProbeAsync(yuvarlak);
            Assert.Equal((224, 154), (bilgi.Width, bilgi.Height));
            Assert.InRange(bilgi.DurationSeconds, 0.8, 1.2);

            var y = await Kare(yuvarlak, 224, 154);
            var d = await Kare(duz, 224, 154);
            _output.WriteLine($"yuvarlak (0,0)={Piksel(y, 224, 0, 0)} (36,36)={Piksel(y, 224, 36, 36)} merkez={Piksel(y, 224, 112, 77)}; duz (36,36)={Piksel(d, 224, 36, 36)}");

            Assert.False(Uzak(Piksel(y, 224, 0, 0), arka));
            Assert.False(Uzak(Piksel(y, 224, 223, 153), arka));
            Assert.False(Uzak(Piksel(y, 224, 36, 36), arka));
            Assert.False(Uzak(Piksel(y, 224, 112, 77), Piksel(kaynakKare, 160, 80, 45)));
            Assert.False(Uzak(Piksel(d, 224, 36, 36), koseKaynak));
            Assert.False(Uzak(Piksel(d, 224, 0, 0), arka));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [FfmpegFact]
    public async Task GecisVeGorselArkaPlanUclardaVerilenRengiTasir()
    {
        var klasor = Klasor();
        try
        {
            var kaynak = Path.Combine(klasor, "kayit.mp4");
            await Uret(kaynak, "testsrc2=s=160x90:r=10:d=1");

            var gecis = Path.Combine(klasor, "gecis.mp4");
            await Calistir(RecordingFrame.Arguments(kaynak, gecis, 160, 90, new FrameOptions(32, 24, true, FrameBackdrop.Gradient("#FF0000", "#0000FF")), 2));
            var g = await Kare(gecis, 224, 154);
            _output.WriteLine($"gecis (0,0)={Piksel(g, 224, 0, 0)} son={Piksel(g, 224, 223, 153)}");
            Assert.False(Uzak(Piksel(g, 224, 0, 0), (0xFF, 0, 0)));
            Assert.False(Uzak(Piksel(g, 224, 223, 153), (0, 0, 0xFF)));

            var resim = Path.Combine(klasor, "arka.png");
            var uret = await FfmpegRunner.RunAsync(["-hide_banner", "-y", "-nostdin", "-f", "lavfi", "-i", "color=c=0x00C000:s=50x30:d=1", "-frames:v", "1", "-threads", "2", resim]);
            Assert.True(uret.Ok, uret.StandardError);
            var gorsel = Path.Combine(klasor, "gorsel.mp4");
            await Calistir(RecordingFrame.Arguments(kaynak, gorsel, 160, 90, new FrameOptions(32, 0, false, FrameBackdrop.Image(resim)), 2));
            var bilgi = await FfprobeClient.ProbeAsync(gorsel);
            var r = await Kare(gorsel, 224, 154);
            _output.WriteLine($"gorsel (0,0)={Piksel(r, 224, 0, 0)}");
            Assert.Equal((224, 154), (bilgi.Width, bilgi.Height));
            Assert.False(Uzak(Piksel(r, 224, 0, 0), (0, 0xC0, 0)));
            Assert.False(Uzak(Piksel(r, 224, 223, 153), (0, 0xC0, 0)));
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void AyarlarGidipGelirBozukDegerVarsayilanaDuser()
    {
        var klasor = Klasor();
        try
        {
            var yol = Path.Combine(klasor, "ayar.json");
            var ayar = new RecorderSettings { FrameBackdrop = 3, FramePadding = 96, FrameCorner = 0, FrameShadow = false, FrameImage = "arka.png" };
            File.WriteAllBytes(yol, ayar.ToJson());
            var okunan = RecorderSettings.Load(yol);

            File.WriteAllText(yol, "{\"frame\":{\"backdrop\":99,\"padding\":33,\"corner\":\"yok\",\"shadow\":false}}");
            var bozuk = RecorderSettings.Load(yol);

            Assert.Equal((3, 96, 0, false, "arka.png"), (okunan.FrameBackdrop, okunan.FramePadding, okunan.FrameCorner, okunan.FrameShadow, okunan.FrameImage));
            Assert.Equal(1, bozuk.FrameBackdrop);
            Assert.Equal(RecorderSettings.DefaultFramePadding, bozuk.FramePadding);
            Assert.Equal(RecorderSettings.DefaultFrameCorner, bozuk.FrameCorner);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void SonucPanelindeCerceveleDugmesiPaletRengiyleKosar()
    {
        var klasor = Klasor();
        try
        {
            var dosya = Path.Combine(klasor, "kayit.mp4");
            File.WriteAllBytes(dosya, [1]);
            var gif = Path.Combine(klasor, "kayit.gif");
            File.WriteAllBytes(gif, [1]);
            var resim = Path.Combine(klasor, "arka.png");
            File.WriteAllBytes(resim, [1]);

            using var ayar = new KaydediciAyarTests.OzelAyar();
            var olcu = AppHost.Run(() =>
            {
                var verilen = new List<IReadOnlyList<string>>();
                var view = new RecorderView(ayar.Yol)
                {
                    RevealFolder = _ => { },
                    FrameProbe = (_, _) => Task.FromResult((160, 90)),
                    FrameRun = (a, _) => { verilen.Add(a); return Task.FromResult(new FfmpegRun(true, 0, string.Empty, TimeSpan.Zero)); }
                };
                var pencere = new Window { Width = 1536, Height = 832, ShowActivated = false, Content = view };
                pencere.Show();
                Dispatcher.UIThread.RunJobs();
                try
                {
                    view.ShowResult(new RecordResult(true, gif, 1, false, 0, string.Empty, 1));
                    var gifte = view.FrameVisible;
                    view.ShowResult(new RecordResult(true, dosya, 1, true, 0, string.Empty, 1));
                    var yarimda = view.FrameVisible;
                    view.ShowResult(new RecordResult(true, dosya, 1, false, 0, string.Empty, 1));
                    var tamda = view.FrameVisible;
                    var komsu = view.FrameButton?.Parent is Panel satir && satir.Children.IndexOf(view.FrameButton) == satir.Children.IndexOf(view.BtnQuickTrim) + 1;

                    var mavi = Hex(view, "NeonBlueColor");
                    var mor = Hex(view, "NeonPurpleColor");
                    view.FrameBackdropIndex = 1;
                    var hedef = view.FrameRecordingAsync();
                    var bildirim = view.NoticeText;

                    view.FrameBackdropIndex = RecorderView.FrameImageIndex;
                    view.FrameImage = null;
                    var gorselsizAcik = view.FrameApplyEnabled;
                    var seciciGorunur = view.FrameImagePickVisible;
                    view.FrameImage = resim;
                    var gorselliAcik = view.FrameApplyEnabled;
                    _ = view.FrameRecordingAsync();

                    view.FrameRun = (_, _) => Task.FromResult(new FfmpegRun(false, 1, "ilk satir\nbozuk filtre\n", TimeSpan.Zero));
                    _ = view.FrameRecordingAsync();
                    var hata = view.ErrorText;
                    var kayitliArka = RecorderSettings.Load(ayar.Yol).FrameBackdrop;
                    return (gifte, yarimda, tamda, komsu, mavi, mor, hedef.IsCompleted ? hedef.Result : null, bildirim, gorselsizAcik, seciciGorunur, gorselliAcik, verilen, hata, kayitliArka);
                }
                finally
                {
                    pencere.Content = null;
                    pencere.Close();
                    Dispatcher.UIThread.RunJobs();
                }
            });

            Assert.False(olcu.gifte);
            Assert.False(olcu.yarimda);
            Assert.True(olcu.tamda);
            Assert.True(olcu.komsu);
            Assert.Equal(Path.Combine(klasor, "kayit-cerceveli.mp4"), olcu.Item7);
            Assert.Contains("kayit-cerceveli.mp4", olcu.bildirim);
            Assert.Contains(olcu.verilen[0], a => a.StartsWith("gradients=", StringComparison.Ordinal)
                                                   && a.Contains("c0=" + RecordingFrame.Color(olcu.mavi))
                                                   && a.Contains("c1=" + RecordingFrame.Color(olcu.mor)));
            Assert.Equal("2", olcu.verilen[0][olcu.verilen[0].IndexOf("-threads") + 1]);
            Assert.False(olcu.gorselsizAcik);
            Assert.True(olcu.seciciGorunur);
            Assert.True(olcu.gorselliAcik);
            Assert.Equal(2, olcu.verilen.Count);
            Assert.Equal(resim, olcu.verilen[1][olcu.verilen[1].IndexOf(dosya) + 2]);
            Assert.Contains("bozuk filtre", olcu.hata);
            Assert.DoesNotContain("ilk satir", olcu.hata);
            Assert.Equal(RecorderView.FrameImageIndex, olcu.kayitliArka);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void CerceveMetinleriButunDillerdeDolu()
    {
        var anahtarlar = RecorderView.FrameBackdropKeys().Concat(
        [
            "recorder.output.frame", "recorder.output.frame.tip", "recorder.output.frame-backdrop", "recorder.output.frame-padding",
            "recorder.output.frame-corner", "recorder.output.frame-shadow", "recorder.output.frame-image-pick", "recorder.output.frame-image-none",
            "recorder.output.frame-apply", "recorder.output.frame-running", "recorder.output.frame-done", "recorder.output.frame-failed"
        ]).ToArray();

        Assert.Equal(RecorderView.FrameBackdropCount, RecorderView.FrameBackdropKeys().Count);
        Assert.Equal(RecorderView.FrameBackdropCount - 2, RecorderView.FrameGradients.Length);
        foreach (var dil in Locales.Languages)
        {
            var metin = Locales.Values(dil);
            foreach (var anahtar in anahtarlar)
                Assert.False(string.IsNullOrWhiteSpace(metin.GetValueOrDefault(anahtar)), dil + " " + anahtar);
            Assert.Contains("{0}", metin["recorder.output.frame-done"]);
            Assert.Contains("{0}", metin["recorder.output.frame-failed"]);
            Assert.Equal(RecorderView.FrameBackdropCount, RecorderView.FrameBackdropKeys().Select(k => metin[k]).Distinct().Count());
        }
    }

    private static string Hex(Control host, string anahtar)
        => VidShrink.App.Editing.EditorTokens.Find(host, anahtar) is Avalonia.Media.Color c
            ? string.Create(CultureInfo.InvariantCulture, $"#{c.R:X2}{c.G:X2}{c.B:X2}")
            : throw new InvalidOperationException(anahtar + " paletten okunamadı");

    private static async Task Uret(string hedef, string kaynak)
    {
        var uret = await FfmpegRunner.RunAsync(["-hide_banner", "-y", "-nostdin", "-f", "lavfi", "-i", kaynak, "-c:v", "libx264", "-preset", "ultrafast", "-crf", "10", "-pix_fmt", "yuv420p", "-threads", "2", hedef]);
        Assert.True(uret.Ok, uret.StandardError);
    }

    private static async Task Calistir(IReadOnlyList<string> arguman)
    {
        var kosum = await FfmpegRunner.RunAsync(arguman.Prepend("-nostdin").ToList());
        Assert.True(kosum.Ok, kosum.StandardError);
    }

    private static async Task<byte[]> Kare(string dosya, int w, int h)
    {
        var cikti = dosya + ".rgb";
        var kosum = await FfmpegRunner.RunAsync(["-hide_banner", "-y", "-nostdin", "-i", dosya, "-vf", "select=eq(n\\,5)", "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", "-threads", "2", cikti]);
        Assert.True(kosum.Ok, kosum.StandardError);
        var bayt = await File.ReadAllBytesAsync(cikti);
        Assert.Equal(w * h * 3, bayt.Length);
        return bayt;
    }

    private static (int R, int G, int B) Piksel(byte[] kare, int w, int x, int y)
    {
        var i = (y * w + x) * 3;
        return (kare[i], kare[i + 1], kare[i + 2]);
    }

    private static bool Uzak((int R, int G, int B) a, (int R, int G, int B) b)
        => Math.Abs(a.R - b.R) > Tolerans || Math.Abs(a.G - b.G) > Tolerans || Math.Abs(a.B - b.B) > Tolerans;
}
