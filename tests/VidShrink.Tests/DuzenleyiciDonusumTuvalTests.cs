using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

public sealed class DuzenleyiciDonusumTuvalTests
{
    private const string Dal = "yan-donusum-tuval";
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

    private static MediaInfo Bilgi(int w, int h) => new()
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

    private static ExportPlan Plan(EditTimeline cizelge, MediaInfo bilgi, ExportMode kip = ExportMode.Full)
        => EditExport.Build(cizelge, bilgi, Anahtarlar, 0, kip, "cikti.mp4", "is", 8 * Gb);

    private static string Cizge(EditTimeline cizelge, MediaInfo bilgi)
    {
        var args = Plan(cizelge, bilgi).Steps[0].Args.ToList();
        return args[args.IndexOf("-filter_complex") + 1];
    }

    private static string Zincir(Func<ClipEffects, ClipEffects> ayar, int w = 320, int h = 240, EditFrame cerceve = default)
    {
        var cizelge = new EditTimeline(new[] { new EditClip(0, S(10)) }, S(10), frame: cerceve);
        cizelge.SetEffects(new[] { 0 }, ayar);
        var cizge = Cizge(cizelge, Bilgi(w, h));
        var bas = cizge.IndexOf("setpts=PTS-STARTPTS,", StringComparison.Ordinal) + "setpts=PTS-STARTPTS,".Length;
        var son = cizge.IndexOf("setsar=1", bas, StringComparison.Ordinal) + "setsar=1".Length;
        var disa = cizge[bas..son];

        var onizleme = new EdlPreview("k.mp4", cizelge);
        var gorunum = new EditLook(onizleme, w, h);
        Assert.Equal(disa, gorunum.Geometry(onizleme.Parts[0]));
        return disa;
    }

    [Fact]
    public void DonusumAlanlariNormallesirVeGeometriSayilir()
    {
        Assert.False(ClipEffects.None.HasTransform);
        Assert.True(ClipEffects.None.IsNeutral);

        var olcek = new ClipEffects { Scale = 0.5 };
        Assert.True(olcek.HasTransform);
        Assert.True(olcek.HasGeometry);
        Assert.False(olcek.IsNeutral);

        var bozuk = new ClipEffects { Scale = 10, OffsetX = 2, OffsetY = -3, Angle = 270 }.Normalized();
        Assert.Equal(ClipEffects.MaxScale, bozuk.Scale);
        Assert.Equal(0.5, bozuk.OffsetX);
        Assert.Equal(-0.5, bozuk.OffsetY);
        Assert.Equal(-90, bozuk.Angle);

        Assert.Equal(ClipEffects.MinScale, new ClipEffects { Scale = 0 }.Normalized().Scale);
        Assert.Equal(180, new ClipEffects { Angle = -180 }.Normalized().Angle);
        Assert.Equal(0, new ClipEffects { Angle = 720 }.Normalized().Angle);
        Assert.Equal(ClipEffects.None, new ClipEffects { Scale = double.NaN, OffsetX = double.PositiveInfinity, Angle = double.NaN }.Normalized());
    }

    [Fact]
    public void ProjeCercevesiBoyutuKisaKenardanCikar()
    {
        Assert.Equal((1080, 1920), new EditFrame(9, 16).Size(1920, 1080));
        Assert.Equal((1080, 1080), new EditFrame(1, 1).Size(1920, 1080));
        Assert.Equal((1920, 1080), new EditFrame(16, 9).Size(1080, 1920));
        Assert.Equal((240, 426), new EditFrame(9, 16).Size(320, 240));
        Assert.True(EditFrame.Source.IsSource);
        Assert.Equal(EditFrame.Source, new EditFrame(100, 1).Normalized());
        Assert.Equal(EditFrame.Source, new EditFrame(-1, 1).Normalized());
        Assert.Equal(new EditFrame(9, 16), new EditFrame(9, 16).Normalized());
        Assert.Equal(new[] { EditFrame.Source, new EditFrame(16, 9), new EditFrame(9, 16), new EditFrame(1, 1) }, EditorView.FramePresets);
    }

    [Fact]
    public void DonusumZinciriOnizlemeVeDisaAktarimdaAynidir()
    {
        Assert.Equal("scale=160:120,pad=320:240:80:60,setsar=1", Zincir(e => e with { Scale = 0.5 }));
        Assert.Equal("scale=160:120,pad=320:240:160:60,setsar=1", Zincir(e => e with { Scale = 0.5, OffsetX = 0.25 }));
        Assert.Equal("scale=160:120,pad=320:240:80:0,setsar=1", Zincir(e => e with { Scale = 0.5, OffsetY = -0.25 }));
        Assert.Equal("scale=640:480,crop=320:240:160:120,setsar=1", Zincir(e => e with { Scale = 2 }));
        Assert.Equal("scale=320:240,rotate=1.570796:ow=240:oh=320:c=black,crop=240:240:0:40,pad=320:240:40:0,setsar=1",
            Zincir(e => e with { Angle = 90 }));
        Assert.Equal("scale=320:240,crop=160:240:0:0,pad=320:240:160:0,setsar=1", Zincir(e => e with { OffsetX = 0.5 }));
        Assert.Equal("hflip,scale=160:120,pad=320:240:80:60,setsar=1", Zincir(e => e with { FlipH = true, Scale = 0.5 }));
    }

    [Fact]
    public void ProjeCercevesiTuvaliBelirlerVeYenidenKodlamayaZorlar()
    {
        Assert.Equal("scale=240:426:force_original_aspect_ratio=decrease:force_divisible_by=2,pad=240:426:(ow-iw)/2:(oh-ih)/2,setsar=1",
            Zincir(e => e, cerceve: new EditFrame(9, 16)));
        Assert.Equal("scale=240:240:force_original_aspect_ratio=decrease:force_divisible_by=2,pad=240:240:(ow-iw)/2:(oh-ih)/2,setsar=1",
            Zincir(e => e, cerceve: new EditFrame(1, 1)));
        Assert.Equal("scale=120:90,pad=240:426:60:168,setsar=1", Zincir(e => e with { Scale = 0.5 }, cerceve: new EditFrame(9, 16)));
        Assert.Equal("scale=480:360,crop=240:360:120:0,pad=240:426:0:34,setsar=1", Zincir(e => e with { Scale = 2 }, cerceve: new EditFrame(9, 16)));

        var sinirlar = DuzenleyiciAkilliKodekTests.Sinirlar(Anahtarlar);
        var kaynak = EditTimeline.FromSource(S(10));
        Assert.False(kaynak.HasEffects);
        var yalin = EditExport.Build(kaynak, Bilgi(320, 240), Anahtarlar, 0, ExportMode.Smart, "cikti.mp4", "is", 8 * Gb, sinirlar);
        Assert.Equal(ExportMode.Smart, yalin.Effective);
        Assert.False(yalin.EffectsForcedFull);

        var cizelge = EditTimeline.FromSource(S(10));
        Assert.True(cizelge.SetFrame(new EditFrame(9, 16)));
        Assert.True(cizelge.HasEffects);
        var akilli = EditExport.Build(cizelge, Bilgi(320, 240), Anahtarlar, 0, ExportMode.Smart, "cikti.mp4", "is", 8 * Gb, sinirlar);
        Assert.Equal(ExportMode.Full, akilli.Effective);
        Assert.True(akilli.EffectsForcedFull);
        Assert.Null(ClipFilters.Canvas(kaynak, Bilgi(320, 240)));
        Assert.Equal((240, 426), ClipFilters.Canvas(cizelge, Bilgi(320, 240)));
    }

    [Fact]
    public void CerceveDegisimiTekAdimdaGeriAlinir()
    {
        var cizelge = EditTimeline.FromSource(S(10));
        Assert.True(cizelge.SetFrame(new EditFrame(1, 1)));
        Assert.False(cizelge.SetFrame(new EditFrame(1, 1)));
        Assert.False(cizelge.SetFrame(new EditFrame(50, 1)) && cizelge.Frame != EditFrame.Source);
        Assert.Equal(EditFrame.Source, cizelge.Frame);
        Assert.True(cizelge.Undo());
        Assert.Equal(new EditFrame(1, 1), cizelge.Frame);
        Assert.True(cizelge.Undo());
        Assert.Equal(EditFrame.Source, cizelge.Frame);
        Assert.True(cizelge.Redo());
        Assert.Equal(new EditFrame(1, 1), cizelge.Frame);

        var yukle = EditTimeline.FromSource(S(10));
        var parcalar = new[] { new EditClip(0, S(5)) { Effects = new ClipEffects { Scale = 0.5 } } };
        Assert.True(yukle.Restore(parcalar, Array.Empty<TextLayer>(), null, new EditFrame(9, 16)));
        Assert.Equal(new EditFrame(9, 16), yukle.Frame);
        Assert.Equal(parcalar, yukle.Clips);
        Assert.True(yukle.Undo());
        Assert.Equal(EditFrame.Source, yukle.Frame);
        Assert.Single(yukle.Clips);
        Assert.Equal(S(10), yukle.Clips[0].SourceEnd);
        Assert.False(yukle.CanUndo);

        var parca = new EditTimeline(new[] { new EditClip(0, S(4)) }, frame: new EditFrame(9, 16));
        Assert.Equal(new EditFrame(9, 16), new EdlPreview("k.mp4", parca).Frame);
    }

    [Fact]
    public void ProjeDosyasiDonusumVeCerceveyiTasirEskiDosyaAcilir()
    {
        var model = EditTimeline.FromSource(S(60));
        Assert.True(model.Split(S(30)));
        model.SetEffects(new[] { 0 }, e => e with { Scale = 0.75, OffsetX = -0.2, OffsetY = 0.1, Angle = 12.5 });
        model.SetEffects(new[] { 1 }, e => e with { Rotation = 90 });
        model.SetFrame(new EditFrame(9, 16));
        var json = EditProject.From(new SourceStamp(@"C:\donusum.mp4", 16, 1), model).ToJson();
        Assert.Contains("\"frame\"", json);
        Assert.Contains("\"version\": 1", json);
        var okunan = EditProject.Parse(json);
        Assert.NotNull(okunan);
        Assert.Equal(model.Clips, okunan!.Clips);
        Assert.Equal(new EditFrame(9, 16), okunan.Frame);
        var is_ = okunan.ToJob(yol => new SourceStamp(yol, 16, 1), out _);
        Assert.NotNull(is_);
        Assert.Equal(new EditFrame(9, 16), is_!.Timeline.Frame);

        var eski = Regex.Replace(json, ",\\s*\"(scale|offsetX|offsetY|angle)\":[^,}]+", string.Empty);
        eski = Regex.Replace(eski, ",\\s*\"frame\":\\s*\\{[^}]*\\}", string.Empty);
        Assert.DoesNotContain("offsetX", eski);
        Assert.DoesNotContain("frame", eski);
        var eskiOkunan = EditProject.Parse(eski);
        Assert.NotNull(eskiOkunan);
        Assert.Equal(EditFrame.Source, eskiOkunan!.Frame);
        Assert.All(eskiOkunan.Clips, k => Assert.False(k.Effects.HasTransform, k.Effects.ToString()));
        Assert.Equal(90, eskiOkunan.Clips[1].Effects.Rotation);

        var notr = EditProject.From(new SourceStamp(@"C:\notr.mp4", 16, 1), EditTimeline.FromSource(S(10))).ToJson();
        Assert.DoesNotContain("frame", notr);
        Assert.DoesNotContain("scale", notr);
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

    private static byte[] Kare(string video, string kare, string? suzgec = null)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-v", "error", "-y", "-ss", "1", "-i", video };
        if (suzgec is not null) args.AddRange(new[] { "-vf", suzgec });
        args.AddRange(new[] { "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", "-threads", "2", kare });
        Kos(ToolLocator.Ffmpeg, args.ToArray());
        return File.ReadAllBytes(kare);
    }

    private static (int R, int G, int B) Piksel(byte[] kare, int w, int x, int y)
    {
        var i = (y * w + x) * 3;
        return (kare[i], kare[i + 1], kare[i + 2]);
    }

    private static bool Beyaz((int R, int G, int B) p) => p.R > 200 && p.G > 200 && p.B > 200;

    private static bool Siyah((int R, int G, int B) p) => p.R < 50 && p.G < 50 && p.B < 50;

    [FfmpegFact]
    public async Task CanliKareDonusumVeCerceveyiOnizlemeyleAyniCizer()
    {
        var klasor = Path.Combine(Kanit, "donusum-canli");
        Directory.CreateDirectory(klasor);
        var adlar = new[] { "beyaz.mp4", "desen.mp4", "kayik.mp4", "dikey.mp4", "aci.mp4", "desenli.mp4", "kayik.rgb", "dikey.rgb", "aci.rgb", "desenli.rgb", "onizleme.rgb" };
        KanitKapanisi.Onceki(klasor, adlar);
        var beyaz = Path.Combine(klasor, "beyaz.mp4");
        Kos(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "color=white:s=160x120:r=10:d=2",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-threads", "2", beyaz);
        var desen = Path.Combine(klasor, "desen.mp4");
        Kos(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "testsrc=s=160x120:r=10:d=2",
            "-c:v", "libx264", "-preset", "ultrafast", "-qp", "0", "-pix_fmt", "yuv420p", "-threads", "2", desen);

        async Task<string> Aktar(string kaynak, string ad, Func<ClipEffects, ClipEffects> ayar, EditFrame cerceve = default)
        {
            var c = new EditTimeline(new[] { new EditClip(0, S(2)) }, S(2), frame: cerceve);
            c.SetEffects(new[] { 0 }, ayar);
            var cikti = Path.Combine(klasor, ad);
            var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, c, ExportMode.Smart, cikti, 8 * Gb);
            Assert.Equal(ExportMode.Full, plan.Effective);
            var args = plan.Steps[0].Args.ToList();
            args.InsertRange(args.Count - 1, new[] { "-threads", "2", "-qp", "0" });
            await EditExportRunner.RunAsync(plan with { Steps = new[] { plan.Steps[0] with { Args = args } } }, null);
            return cikti;
        }

        var kayik = await Aktar(beyaz, "kayik.mp4", e => e with { Scale = 0.5, OffsetX = 0.25 });
        Assert.Equal((160, 120), Boyut(kayik));
        var kp = Kare(kayik, Path.Combine(klasor, "kayik.rgb"));
        Assert.True(Beyaz(Piksel(kp, 160, 120, 60)), "kaydirilan klibin ortasi beyaz degil " + Piksel(kp, 160, 120, 60));
        Assert.True(Siyah(Piksel(kp, 160, 40, 60)), "klibin eski yeri bos degil " + Piksel(kp, 160, 40, 60));
        Assert.True(Siyah(Piksel(kp, 160, 120, 10)), "ust bant bos degil " + Piksel(kp, 160, 120, 10));

        var dikey = await Aktar(beyaz, "dikey.mp4", e => e, new EditFrame(9, 16));
        Assert.Equal((120, 214), Boyut(dikey));
        var dp = Kare(dikey, Path.Combine(klasor, "dikey.rgb"));
        Assert.True(Siyah(Piksel(dp, 120, 60, 10)), "dikey cercevede ust bant bos degil " + Piksel(dp, 120, 60, 10));
        Assert.True(Beyaz(Piksel(dp, 120, 60, 107)), "dikey cercevede orta beyaz degil " + Piksel(dp, 120, 60, 107));

        var aci = await Aktar(beyaz, "aci.mp4", e => e with { Angle = 45 });
        var ap = Kare(aci, Path.Combine(klasor, "aci.rgb"));
        Assert.True(Beyaz(Piksel(ap, 160, 80, 60)), "donen klibin ortasi beyaz degil " + Piksel(ap, 160, 80, 60));
        Assert.True(Siyah(Piksel(ap, 160, 3, 3)), "donen klibin kosesi bos degil " + Piksel(ap, 160, 3, 3));

        Func<ClipEffects, ClipEffects> karma = e => e with { Scale = 0.8, OffsetX = -0.1, OffsetY = 0.15, Angle = 20 };
        var desenli = await Aktar(desen, "desenli.mp4", karma, new EditFrame(1, 1));
        Assert.Equal((120, 120), Boyut(desenli));
        var disa = Kare(desenli, Path.Combine(klasor, "desenli.rgb"));
        var c = new EditTimeline(new[] { new EditClip(0, S(2)) }, S(2), frame: new EditFrame(1, 1));
        c.SetEffects(new[] { 0 }, karma);
        var onizleme = new EdlPreview(desen, c);
        var zincir = new EditLook(onizleme, 160, 120).Geometry(onizleme.Parts[0]);
        Assert.NotNull(zincir);
        var gorunen = Kare(desen, Path.Combine(klasor, "onizleme.rgb"), zincir);
        Assert.Equal(disa.Length, gorunen.Length);
        var fark = disa.Zip(gorunen, (a, b) => Math.Abs(a - b)).Average();
        File.WriteAllText(Path.Combine(klasor, "fark.txt"), fark.ToString("0.###", CultureInfo.InvariantCulture) + " " + zincir);
        Assert.True(fark < 8, $"onizleme ile disa aktarim farkli: ortalama {fark:0.##} ({zincir})");
        Assert.True(Siyah(Piksel(disa, 120, 2, 2)), "kare cercevede kose bos degil " + Piksel(disa, 120, 2, 2));

        KanitKapanisi.Kapat(klasor, adlar.Append("fark.txt").ToArray());
    }

    [Fact]
    public void ArayuzDonusumKutulariVeCerceveSecicisiYazar()
    {
        var klasor = Path.Combine(Kanit, "donusum-arayuz");
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

                    Assert.Equal(EditorView.FramePresets.Length, view.CmbProjectFrame.ItemCount);
                    Assert.Equal(0, view.CmbProjectFrame.SelectedIndex);
                    Assert.All(view.CmbProjectFrame.Items.Cast<ComboBoxItem>(), i => Assert.False(string.IsNullOrWhiteSpace(i.Content as string)));

                    view.TimelineView.SelectedIndex = 0;
                    Assert.True(view.ClipPanel.IsVisible);
                    Assert.Equal("100", view.TxtClipScale.Text);
                    Assert.Equal("0", view.TxtClipAngle.Text);

                    foreach (var (kutu, deger) in new (TextBox, string)[] { (view.TxtClipScale, "50"), (view.TxtClipOffsetX, "25") })
                    {
                        kutu.Text = deger;
                        kutu.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
                    }

                    Assert.Equal(0.5, model.Clips[0].Effects.Scale);
                    Assert.Equal(0.25, model.Clips[0].Effects.OffsetX);
                    DenetimSurucu.Pump(view.Player, () => motor.Geometri == "lavfi=[scale=160:120,pad=320:240:160:60,setsar=1]", 10);
                    Assert.Equal("lavfi=[scale=160:120,pad=320:240:160:60,setsar=1]", motor.Geometri);

                    view.TxtClipAngle.Text = "400";
                    view.TxtClipAngle.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
                    Assert.Equal(40, model.Clips[0].Effects.Angle);
                    view.TxtClipAngle.Text = "x";
                    view.TxtClipAngle.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
                    Assert.Equal(40, model.Clips[0].Effects.Angle);
                    Assert.Equal("40", view.TxtClipAngle.Text);
                    Assert.True(view.Undo());
                    Assert.Equal(0, model.Clips[0].Effects.Angle);

                    view.CmbProjectFrame.SelectedIndex = 2;
                    Assert.Equal(new EditFrame(9, 16), model.Frame);
                    DenetimSurucu.Pump(view.Player, () => motor.Geometri?.Contains("pad=240:426:", StringComparison.Ordinal) == true, 10);
                    Assert.Equal("lavfi=[scale=120:90,pad=240:426:120:168,setsar=1]", motor.Geometri);

                    Assert.True(view.Undo());
                    Assert.Equal(EditFrame.Source, model.Frame);
                    Assert.Equal(0, view.CmbProjectFrame.SelectedIndex);
                    DenetimSurucu.Pump(view.Player, () => motor.Geometri == "lavfi=[scale=160:120,pad=320:240:160:60,setsar=1]", 10);
                    Assert.True(view.Redo());
                    Assert.Equal(2, view.CmbProjectFrame.SelectedIndex);
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
