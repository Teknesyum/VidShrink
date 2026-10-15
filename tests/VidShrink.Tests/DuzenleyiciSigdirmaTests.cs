using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Duzenleyicide en-boy on ayarinin "sigdir" kipi: kare <c>scale</c> ile kuculur, kalan alan
/// <c>pad</c> ile secilen duz renkle dolar. Model (uzun kenar korunur, kirpma ile sigdirma
/// birbirini dislar, bozuk oran ve renk normallesir), disa aktarim grafi (<c>scale</c> +
/// <c>pad=...:color=0xRRGGBB</c>), proje dosyasinin gidis donusu ve eski dosyanin sigdirmasiz
/// acilmasi, canli ffmpeg ciktisinin boyutu ve dolgu bandinin pikseli, arayuzde kip dugmesi,
/// dolgu rengi kutusu ve onizleme zinciri. Kanit <c>.calisma/worktree-agent-ab170af9822303fb1/</c>, test siler.
/// </summary>
public sealed class DuzenleyiciSigdirmaTests
{
    private const string Dal = "worktree-agent-ab170af9822303fb1";
    private const long Gb = 1_000_000_000;
    private static readonly double[] Anahtarlar = { 0, 2, 4, 6, 8 };

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", Dal);
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static MediaInfo Bilgi(int w = 1920, int h = 1080) => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 1,
        DurationSeconds = 60,
        Width = w,
        Height = h,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 1,
        AudioCodec = "aac",
        PixelFormat = "yuv420p",
        BitDepth = 8
    };

    private static string Cizge(EditTimeline cizelge, MediaInfo? bilgi = null)
    {
        var args = EditExport.Build(cizelge, bilgi ?? Bilgi(), Anahtarlar, 0, ExportMode.Full, "cikti.mp4", "is", 8 * Gb).Steps[0].Args.ToList();
        return args[args.IndexOf("-filter_complex") + 1];
    }

    [Fact]
    public void SigdirmaUzunKenariKorurKirpmayiDislarNormallesir()
    {
        Assert.Equal((1080, 1920), ClipEffects.None.WithFit(9, 16, "000000").OutputSize(1920, 1080));
        Assert.Equal((1920, 1920), ClipEffects.None.WithFit(1, 1, "000000").OutputSize(1920, 1080));
        Assert.Equal((1920, 1440), ClipEffects.None.WithFit(4, 3, "000000").OutputSize(1920, 1080));
        Assert.Equal((1536, 1920), ClipEffects.None.WithFit(4, 5, "000000").OutputSize(1920, 1080));
        Assert.Equal((1920, 1080), ClipEffects.None.WithFit(16, 9, "000000").OutputSize(1920, 1080));
        Assert.Equal((180, 320), ClipEffects.None.WithFit(9, 16, "000000").OutputSize(320, 240));
        Assert.Equal((1920, 1080), (ClipEffects.None with { Rotation = 90 }).WithFit(16, 9, "000000").OutputSize(1920, 1080));

        foreach (var (num, den) in EditorView.CropPresets.Skip(1))
        foreach (var aci in new[] { 0, 90, 180, 270 })
        {
            var e = (ClipEffects.None with { Rotation = aci }).WithFit(num, den, "000000");
            var (w, h) = e.OutputSize(333, 187);
            Assert.True(w % 2 == 0 && h % 2 == 0, $"{num}:{den} {aci} tek boyut {w}x{h}");
            Assert.InRange((double)w / h, (double)num / den - 0.02, (double)num / den + 0.02);
            Assert.Equal(332, Math.Max(w, h));
        }

        var sigdir = ClipEffects.None.WithAspect(9, 16, 1920, 1080).WithFit(1, 1, "ff8800");
        Assert.True(sigdir.HasFit);
        Assert.False(sigdir.HasCrop);
        Assert.True(sigdir.HasGeometry);
        Assert.False(sigdir.IsNeutral);
        Assert.Equal("FF8800", sigdir.PadColor);
        var kirp = sigdir.WithAspect(9, 16, 1920, 1080);
        Assert.False(kirp.HasFit);
        Assert.True(kirp.HasCrop);
        Assert.Equal(ClipEffects.DefaultPadColor, kirp.PadColor);
        Assert.Equal(ClipEffects.None, ClipEffects.None.WithFit(0, 0, "FFFFFF"));

        Assert.Equal("000000", ClipEffects.DefaultPadColor);
        var bozukRenk = new ClipEffects { FitNum = 1, FitDen = 1, PadColor = "kirmizi" }.Normalized();
        Assert.Equal(ClipEffects.DefaultPadColor, bozukRenk.PadColor);
        Assert.True(bozukRenk.HasFit);
        Assert.Equal("ABCDEF", new ClipEffects { FitNum = 1, FitDen = 1, PadColor = "#abcdef" }.Normalized().PadColor);
        var eksi = new ClipEffects { FitNum = -1, FitDen = 1, PadColor = "FFFFFF" }.Normalized();
        Assert.False(eksi.HasFit);
        Assert.Equal(ClipEffects.None, eksi);
        Assert.False(new ClipEffects { FitNum = 100, FitDen = 1 }.Normalized().HasFit);
        Assert.True(new ClipEffects { FitNum = 21, FitDen = 9 }.Normalized().HasFit);
    }

    [Fact]
    public void DisaAktarimGrafiOlcekVeRenkliDolguYazar()
    {
        var dikey = EditTimeline.FromSource(S(10));
        dikey.SetEffects(new[] { 0 }, e => e.WithFit(9, 16, "FFFFFF"));
        var cizge = Cizge(dikey);
        Assert.Contains("setpts=PTS-STARTPTS,scale=1080:1920:force_original_aspect_ratio=decrease:force_divisible_by=2,"
                        + "pad=1080:1920:(ow-iw)/2:(oh-ih)/2:color=0xFFFFFF,setsar=1[v0]", cizge);
        Assert.DoesNotContain("crop=", cizge);

        var donmus = EditTimeline.FromSource(S(10));
        donmus.SetEffects(new[] { 0 }, e => (e with { Rotation = 90 }).WithFit(16, 9, "000000"));
        Assert.Contains("transpose=1,scale=1920:1080:force_original_aspect_ratio=decrease:force_divisible_by=2,"
                        + "pad=1920:1080:(ow-iw)/2:(oh-ih)/2:color=0x000000,setsar=1[v0]", Cizge(donmus));

        var ayni = EditTimeline.FromSource(S(10));
        ayni.SetEffects(new[] { 0 }, e => e.WithFit(16, 9, "FFFFFF"));
        var ayniCizge = Cizge(ayni);
        Assert.DoesNotContain("pad=", ayniCizge);
        Assert.Contains("setpts=PTS-STARTPTS,setsar=1[v0]", ayniCizge);

        var gorunum = new EditLook(new EdlPreview("k.mp4", dikey), 320, 240);
        Assert.Equal((180, 320), gorunum.Canvas);
        Assert.Equal("scale=180:320:force_original_aspect_ratio=decrease:force_divisible_by=2,pad=180:320:(ow-iw)/2:(oh-ih)/2:color=0xFFFFFF,setsar=1",
            gorunum.Geometry(new EdlPreview("k.mp4", dikey).Parts[0]));
    }

    [Fact]
    public void ProjeDosyasiSigdirmayiTasirEskiDosyaSigdirmasizAcilir()
    {
        var model = EditTimeline.FromSource(S(60));
        Assert.True(model.Split(S(30)));
        model.SetEffects(new[] { 0 }, e => e.WithFit(9, 16, "FF0000"));
        model.SetEffects(new[] { 1 }, e => e with { Rotation = 90 });
        var json = EditProject.From(new SourceStamp(@"C:\sigdir.mp4", 16, 1), model).ToJson();
        var okunan = EditProject.Parse(json);
        Assert.NotNull(okunan);
        Assert.Equal(model.Clips, okunan!.Clips);
        Assert.Equal("FF0000", okunan.Clips[0].Effects.PadColor);

        var eski = Regex.Replace(json, ",\\s*\"(fitNum|fitDen|padColor)\":[^,}]+", string.Empty);
        Assert.DoesNotContain("padColor", eski);
        var eskiOkunan = EditProject.Parse(eski);
        Assert.NotNull(eskiOkunan);
        Assert.All(eskiOkunan!.Clips, k => Assert.False(k.Effects.HasFit, k.Effects.ToString()));
        Assert.Equal(90, eskiOkunan.Clips[1].Effects.Rotation);
    }

    private static string Kos(string arac, params string[] args)
    {
        var psi = new ProcessStartInfo(arac)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var standart = surec.StandardOutput.ReadToEndAsync();
        if (!surec.WaitForExit(60_000))
        {
            surec.Kill(true);
            Assert.Fail(Path.GetFileName(arac) + " 60 sn icinde bitmedi");
        }

        surec.WaitForExit();
        Assert.True(surec.ExitCode == 0, hata.Result);
        return standart.Result;
    }

    private static (int W, int H) Boyut(string video)
    {
        var satir = Kos(ToolLocator.Ffprobe, "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height",
            "-of", "csv=p=0", video).Trim().Split(',');
        return (int.Parse(satir[0], CultureInfo.InvariantCulture), int.Parse(satir[1], CultureInfo.InvariantCulture));
    }

    private static byte[] Kare(string video, string kare)
    {
        Kos(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-v", "error", "-y", "-ss", "1", "-i", video,
            "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", "-threads", "2", kare);
        return File.ReadAllBytes(kare);
    }

    private static (int R, int G, int B) Piksel(byte[] kare, int w, int x, int y)
    {
        var i = (y * w + x) * 3;
        return (kare[i], kare[i + 1], kare[i + 2]);
    }

    [FfmpegFact]
    public async Task CanliSigdirmaBoyutVeDolguPikseliniOlcer()
    {
        var klasor = Path.Combine(Kanit, "sigdir-canli");
        Directory.CreateDirectory(klasor);
        var adlar = new[] { "kaynak.mp4", "kare.mp4", "dikey.mp4", "kare.rgb", "dikey.rgb" };
        KanitKapanisi.Onceki(klasor, adlar);
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        Kos(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "color=white:s=320x240:r=30:d=2",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-threads", "2", kaynak);

        async Task<string> Aktar(string ad, Func<ClipEffects, ClipEffects> ayar)
        {
            var c = EditTimeline.FromSource(S(2));
            c.SetEffects(new[] { 0 }, ayar);
            var cikti = Path.Combine(klasor, ad);
            var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, c, ExportMode.Smart, cikti, 8 * Gb);
            Assert.Equal(ExportMode.Full, plan.Effective);
            var args = plan.Steps[0].Args.ToList();
            args.InsertRange(args.Count - 1, new[] { "-threads", "2" });
            await EditExportRunner.RunAsync(plan with { Steps = new[] { plan.Steps[0] with { Args = args } } }, null);
            return cikti;
        }

        var kare = await Aktar("kare.mp4", e => e.WithFit(1, 1, "FF0000"));
        var dikey = await Aktar("dikey.mp4", e => e.WithFit(9, 16, "0000FF"));

        Assert.Equal((320, 320), Boyut(kare));
        Assert.Equal((180, 320), Boyut(dikey));

        var kp = Kare(kare, Path.Combine(klasor, "kare.rgb"));
        Assert.Equal(320 * 320 * 3, kp.Length);
        var (ur, ug, ub) = Piksel(kp, 320, 160, 10);
        Assert.True(ur > 200 && ug < 60 && ub < 60, $"ust bant kirmizi degil {ur},{ug},{ub}");
        var (ar, ag, ab) = Piksel(kp, 320, 160, 310);
        Assert.True(ar > 200 && ag < 60 && ab < 60, $"alt bant kirmizi degil {ar},{ag},{ab}");
        var (or, og, ob) = Piksel(kp, 320, 160, 160);
        Assert.True(or > 220 && og > 220 && ob > 220, $"orta beyaz degil {or},{og},{ob}");
        var (sr, sg, sb) = Piksel(kp, 320, 5, 160);
        Assert.True(sr > 220 && sg > 220 && sb > 220, "yan kenar dolguya gitti: " + sr + "," + sg + "," + sb);

        var dp = Kare(dikey, Path.Combine(klasor, "dikey.rgb"));
        Assert.Equal(180 * 320 * 3, dp.Length);
        var (dr, dg, db) = Piksel(dp, 180, 90, 20);
        Assert.True(dr < 60 && dg < 60 && db > 200, $"dikey dolgu mavi degil {dr},{dg},{db}");
        var (mr, mg, mb) = Piksel(dp, 180, 90, 160);
        Assert.True(mr > 220 && mg > 220 && mb > 220, $"dikey orta beyaz degil {mr},{mg},{mb}");

        KanitKapanisi.Kapat(klasor, adlar);
    }

    [Fact]
    public void ArayuzSigdirKipiVeDolguRenginiYazar()
    {
        var klasor = Path.Combine(Kanit, "sigdir-arayuz");
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, "kaynak.mp4");
        File.WriteAllBytes(dosya, new byte[16]);
        try
        {
            AppHost.Run(() =>
            {
                var motor = new KlipMotoru();
                var view = new EditorView
                {
                    KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1 }
                };
                view.Player.EngineFactory = () => motor;
                view.OverlayRoot = Path.Combine(klasor, "katman");
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    var acilis = view.OpenSourceAsync(dosya);
                    DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted, 10);
                    acilis.GetAwaiter().GetResult();
                    DenetimSurucu.Pump(view.Player, () => motor.Acilan >= 1, 10);
                    var model = view.Model!;

                    view.TimelineView.SelectedIndex = 0;
                    Assert.True(view.ClipPanel.IsVisible);
                    Assert.Equal(EditorView.PadColors.Length, view.CmbClipPad.ItemCount);
                    Assert.Equal(0, view.CmbClipPad.SelectedIndex);
                    Assert.All(view.CmbClipPad.Items.Cast<ComboBoxItem>(), i => Assert.False(string.IsNullOrWhiteSpace(i.Content as string)));
                    Assert.False(string.IsNullOrWhiteSpace(view.BtnClipFit.Content as string));
                    Assert.False(view.BtnClipFit.IsChecked == true);

                    view.BtnClipFit.IsChecked = true;
                    view.BtnClipFit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.True(model.Clips[0].Effects.IsNeutral);

                    view.CmbClipCrop.SelectedIndex = 3;
                    var e = model.Clips[0].Effects;
                    Assert.True(e.HasFit);
                    Assert.False(e.HasCrop);
                    Assert.Equal((320, 320), e.OutputSize(320, 240));
                    Assert.Equal(ClipEffects.DefaultPadColor, e.PadColor);
                    DenetimSurucu.Pump(view.Player, () => view.Restyles >= 1, 10);

                    view.CmbClipPad.SelectedIndex = 1;
                    Assert.Equal(EditorView.PadColors[1], model.Clips[0].Effects.PadColor);
                    DenetimSurucu.Pump(view.Player, () => view.Restyles >= 2, 10);
                    Assert.Equal("lavfi=[scale=320:320:force_original_aspect_ratio=decrease:force_divisible_by=2,pad=320:320:(ow-iw)/2:(oh-ih)/2:color=0x"
                                 + EditorView.PadColors[1] + ",setsar=1]", motor.Geometri);

                    Assert.True(view.RotateClip());
                    Assert.True(model.Clips[0].Effects.HasFit);
                    Assert.Equal((320, 320), model.Clips[0].Effects.OutputSize(320, 240));

                    view.TimelineView.SelectedIndex = -1;
                    view.TimelineView.SelectedIndex = 0;
                    Assert.Equal(3, view.CmbClipCrop.SelectedIndex);
                    Assert.True(view.BtnClipFit.IsChecked == true);
                    Assert.Equal(1, view.CmbClipPad.SelectedIndex);

                    view.BtnClipFit.IsChecked = false;
                    view.BtnClipFit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.False(model.Clips[0].Effects.HasFit);
                    Assert.True(model.Clips[0].Effects.HasCrop);
                    Assert.Equal(3, view.CmbClipCrop.SelectedIndex);

                    Assert.True(view.Undo());
                    Assert.True(model.Clips[0].Effects.HasFit);
                    Assert.True(view.BtnClipFit.IsChecked == true);
                }
                finally
                {
                    DuzenleyiciKapanis.Kapat(pencere, view);
                }
            });
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
            KanitKapanisi.Kapat(Kanit);
        }
    }
}
