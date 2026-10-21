using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

public sealed class DonusumKaresiTests
{
    private const string Dal = "worktree-agent-later-88-89";
    private const long Gb = 1_000_000_000;
    private const int W = 160;
    private const int H = 120;

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

    private static KeyframeTrack<TransformKey> Iz => new(new[]
    {
        new TransformKey(0, 1, 0, 0, 0, KeyframeEase.EaseIn),
        new TransformKey(S(3), 2, 0.2, -0.1, 30, KeyframeEase.EaseOut),
        new TransformKey(S(7), 0.5, -0.3, 0.2, -45, KeyframeEase.EaseInOut),
        new TransformKey(S(10), 1.5, 0.1, 0, 90),
    });

    private static void Ayni(TransformKey a, TransformKey b, string an)
    {
        Assert.True(Math.Abs(a.Scale - b.Scale) < 1e-3, an + " olcek " + a.Scale + " / " + b.Scale);
        Assert.True(Math.Abs(a.OffsetX - b.OffsetX) < 1e-3, an + " x " + a.OffsetX + " / " + b.OffsetX);
        Assert.True(Math.Abs(a.OffsetY - b.OffsetY) < 1e-3, an + " y " + a.OffsetY + " / " + b.OffsetY);
        Assert.True(Math.Abs(a.Angle - b.Angle) < 1e-2, an + " aci " + a.Angle + " / " + b.Angle);
    }

    [Fact]
    public void AynalananIzTersZamandaAyniDegeriVerir()
    {
        var iz = Iz;
        var uzunluk = S(10);
        var ayna = iz.Mirrored(uzunluk);
        Assert.Equal(new[] { 0L, S(3), S(7), S(10) }, ayna.Select(k => k.Offset).ToArray());
        for (var i = 0; i <= 100; i++)
        {
            var t = uzunluk * i / 100;
            Ayni(iz.ValueAt(t), ayna.ValueAt(uzunluk - t), "t=" + i);
        }

        Assert.Equal(iz, ayna.Mirrored(uzunluk));
        Assert.Equal(KeyframeEase.EaseOut, KeyframeTrack<TransformKey>.Mirror(KeyframeEase.EaseIn));
        Assert.Equal(KeyframeEase.EaseIn, KeyframeTrack<TransformKey>.Mirror(KeyframeEase.EaseOut));
        Assert.Equal(KeyframeEase.EaseInOut, KeyframeTrack<TransformKey>.Mirror(KeyframeEase.EaseInOut));
    }

    [Fact]
    public void TersCevrilenKlipteAnahtarAyniKaynakKaresindeKalir()
    {
        var model = EditTimeline.FromSource(S(10));
        var hacim = new KeyframeTrack<VolumeKey>(new[] { new VolumeKey(0, -20, KeyframeEase.EaseIn), new VolumeKey(S(4), 0), new VolumeKey(S(10), 6) });
        model.SetEffects(new[] { 0 }, e => e with { TransformKeys = Iz, VolumeKeys = hacim });
        var once = model.Clips[0];

        foreach (var hiz in new[] { -1m, -2m, 0.5m, -0.5m })
        {
            model.SetSpeed(0, hiz);
            var sonra = model.Clips[0];
            foreach (var kaynak in Enumerable.Range(1, 19).Select(i => S(i * 0.5)))
            {
                var eski = once.ToOffset(kaynak);
                var yeni = sonra.ToOffset(kaynak);
                Ayni(once.Effects.TransformKeys.ValueAt(eski), sonra.Effects.TransformKeys.ValueAt(yeni), "hiz " + hiz + " kaynak " + kaynak);
                Assert.True(Math.Abs(once.Effects.VolumeKeys.ValueAt(eski).Db - sonra.Effects.VolumeKeys.ValueAt(yeni).Db) < 0.05, "ses hiz " + hiz);
            }
        }

        model.SetSpeed(0, 1m);
        Assert.Equal(once.Effects.TransformKeys.Select(k => k.Ease), model.Clips[0].Effects.TransformKeys.Select(k => k.Ease));
    }

    [Fact]
    public void NormallestirmeSinirlariKirpar()
    {
        var e = (ClipEffects.None with
        {
            TransformKeys = new KeyframeTrack<TransformKey>(new[]
            {
                new TransformKey(0, 9, 3, -3, 1000),
                new TransformKey(S(1), double.NaN, 0.12345, 0, double.PositiveInfinity),
            }),
        }).Normalized();
        Assert.Equal(ClipEffects.MaxScale, e.TransformKeys[0].Scale);
        Assert.Equal(ClipEffects.MaxKeyAngle, e.TransformKeys[0].Angle);
        Assert.True(Math.Abs(e.TransformKeys[0].OffsetX) <= 1 && Math.Abs(e.TransformKeys[0].OffsetY) <= 1, e.TransformKeys[0].ToString());
        Assert.Equal(1, e.TransformKeys[1].Scale);
        Assert.Equal(0, e.TransformKeys[1].Angle);
        Assert.True(e.HasMovingTransform);
        Assert.True(e.HasTransform);
        Assert.True(e.HasKeyframes);
    }

    [Fact]
    public void ProjeDosyasiDonusumAnahtariniTasirEskiDosyaAnahtarsizAcilir()
    {
        var model = EditTimeline.FromSource(S(10));
        model.SetEffects(new[] { 0 }, e => e with { TransformKeys = Iz });
        var json = EditProject.From(new SourceStamp(@"C:\donusum.mp4", 16, 1), model).ToJson();
        Assert.Contains("\"transformKeys\"", json);
        var okunan = EditProject.Parse(json);
        Assert.NotNull(okunan);
        Assert.Equal(model.Clips, okunan!.Clips);

        var eski = Regex.Replace(json, ",\\s*\"transformKeys\":\\s*\\[[^\\]]*\\]", string.Empty);
        Assert.DoesNotContain("transformKeys", eski);
        var eskiOkunan = EditProject.Parse(eski);
        Assert.NotNull(eskiOkunan);
        Assert.Empty(eskiOkunan!.Clips[0].Effects.TransformKeys);
        Assert.DoesNotContain("transformKeys", EditProject.From(new SourceStamp(@"C:\yalin.mp4", 16, 1), EditTimeline.FromSource(S(5))).ToJson());
    }

    [Fact]
    public void TekAnahtarDuraganZincirIkiAnahtarZamanIfadesiKurar()
    {
        var tek = ClipEffects.None with { TransformKeys = new KeyframeTrack<TransformKey>(new[] { new TransformKey(0, 1.5, 0.1, 0, 0) }) };
        var tekZincir = ClipFilters.Geometry(tek, W, H, 1, 1, (W, H));
        Assert.Equal(ClipFilters.Geometry(ClipEffects.None with { Scale = 1.5, OffsetX = 0.1 }, W, H, 1, 1, (W, H)), tekZincir);
        Assert.DoesNotContain("eval=frame", tekZincir);

        var iki = ClipEffects.None with { TransformKeys = Iz };
        var zincir = ClipFilters.Geometry(iki, W, H, 1, 1, (W, H), "T");
        Assert.Contains("eval=frame", zincir);
        Assert.Contains("rotate=a=", zincir);
        Assert.Contains("if(lt((T),", zincir);
        Assert.DoesNotContain("(t)", zincir);
        Assert.Contains(",crop=w=" + W + ":h=" + H + ":x='", zincir);
    }

    private static string Kos(params string[] args)
    {
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg)
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
            Assert.Fail("ffmpeg 60 sn icinde bitmedi");
        }

        surec.WaitForExit();
        Assert.True(surec.ExitCode == 0, hata.Result);
        return standart.Result;
    }

    private static string Kare(string klasor)
    {
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        Kos("-hide_banner", "-nostdin", "-v", "error", "-y", "-f", "lavfi",
            "-i", "color=black:s=" + W + "x" + H + ":r=10:d=2,format=yuv420p,drawbox=x=60:y=40:w=40:h=40:color=white:t=fill",
            "-c:v", "libx264", "-preset", "ultrafast", "-qp", "0", "-pix_fmt", "yuv420p", "-g", "5", "-threads", "2", kaynak);
        return kaynak;
    }

    private static (double X, double Y, int Alan) Beyaz(string video, string kare, double saniye, string? vf = null)
    {
        var args = new[] { "-hide_banner", "-nostdin", "-v", "error", "-y", "-i", video }
            .Concat(vf is null ? Array.Empty<string>() : new[] { "-vf", vf })
            .Concat(new[] { "-ss", saniye.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "gray", "-threads", "2", kare })
            .ToArray();
        Kos(args);
        var b = File.ReadAllBytes(kare);
        Assert.Equal(W * H, b.Length);
        double sx = 0, sy = 0;
        var alan = 0;
        for (var y = 0; y < H; y++)
        for (var x = 0; x < W; x++)
        {
            if (b[y * W + x] < 128) continue;
            sx += x;
            sy += y;
            alan++;
        }

        return alan == 0 ? (double.NaN, double.NaN, 0) : (sx / alan + 0.5, sy / alan + 0.5, alan);
    }

    private static readonly KeyframeTrack<TransformKey> Kayan = new(new[]
    {
        new TransformKey(0, 1, 0, 0, 0),
        new TransformKey(EditTime.FromSeconds(2), 0.5, 0.25, -0.25, 0),
    });

    [FfmpegFact]
    public void HareketliDonusumKaredeKonumuVeOlcegiAnahtardanAlir()
    {
        var klasor = Path.Combine(Kanit, "donusum-kare");
        Directory.CreateDirectory(klasor);
        var adlar = new[] { "kaynak.mp4", "a.gray" };
        KanitKapanisi.Onceki(klasor, adlar);
        var kaynak = Kare(klasor);
        var etki = ClipEffects.None with { TransformKeys = Kayan };
        var zincir = ClipFilters.Geometry(etki, W, H, 1, 1, (W, H));

        foreach (var t in new[] { 0.0, 0.5, 1.0, 1.5 })
        {
            var anahtar = Kayan.ValueAt(S(t));
            var olcum = Beyaz(kaynak, Path.Combine(klasor, "a.gray"), t, zincir);
            var beklenenX = W / 2.0 + anahtar.OffsetX * W;
            var beklenenY = H / 2.0 + anahtar.OffsetY * H;
            var beklenenAlan = 1600 * anahtar.Scale * anahtar.Scale;
            Assert.True(Math.Abs(olcum.X - beklenenX) < 1.5, $"t={t} x {olcum.X:0.0} beklenen {beklenenX:0.0}");
            Assert.True(Math.Abs(olcum.Y - beklenenY) < 1.5, $"t={t} y {olcum.Y:0.0} beklenen {beklenenY:0.0}");
            Assert.True(Math.Abs(olcum.Alan - beklenenAlan) < beklenenAlan * 0.12, $"t={t} alan {olcum.Alan} beklenen {beklenenAlan:0}");

            var durgun = ClipFilters.Geometry((ClipEffects.None with { Scale = anahtar.Scale, OffsetX = anahtar.OffsetX, OffsetY = anahtar.OffsetY }).Normalized(), W, H, 1, 1, (W, H));
            var esi = Beyaz(kaynak, Path.Combine(klasor, "a.gray"), t, durgun);
            Assert.True(Math.Abs(olcum.X - esi.X) < 1.5 && Math.Abs(olcum.Y - esi.Y) < 1.5, $"t={t} duragan zincir {esi.X:0.0},{esi.Y:0.0} hareketli {olcum.X:0.0},{olcum.Y:0.0}");
        }

        KanitKapanisi.Kapat(klasor, adlar);
    }

    [FfmpegFact]
    public async Task TersKlibinDisaAktarimiAnahtariAynalar()
    {
        var klasor = Path.Combine(Kanit, "donusum-ters");
        Directory.CreateDirectory(klasor);
        var adlar = new[] { "kaynak.mp4", "cikti.mp4", "b.gray" };
        KanitKapanisi.Onceki(klasor, adlar);
        var kaynak = Kare(klasor);

        var c = EditTimeline.FromSource(S(2));
        c.SetEffects(new[] { 0 }, e => e with { TransformKeys = Kayan });
        c.SetSpeed(0, -1m);
        var cikti = Path.Combine(klasor, "cikti.mp4");
        var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, c, ExportMode.Smart, cikti, 8 * Gb);
        var args = plan.Steps[0].Args.ToList();
        args.InsertRange(args.Count - 1, new[] { "-threads", "2" });
        await EditExportRunner.RunAsync(plan with { Steps = new[] { plan.Steps[0] with { Args = args } } }, null);

        foreach (var t in new[] { 0.5, 1.0, 1.5 })
        {
            var anahtar = Kayan.ValueAt(S(2 - t));
            var olcum = Beyaz(cikti, Path.Combine(klasor, "b.gray"), t);
            var beklenenX = W / 2.0 + anahtar.OffsetX * W;
            Assert.True(Math.Abs(olcum.X - beklenenX) < 2.5, $"ters t={t} x {olcum.X:0.0} beklenen {beklenenX:0.0}");
            Assert.True(Math.Abs(olcum.Alan - 1600 * anahtar.Scale * anahtar.Scale) < 1600 * anahtar.Scale * anahtar.Scale * 0.15, $"ters t={t} alan {olcum.Alan}");
        }

        KanitKapanisi.Kapat(klasor, adlar);
    }
}
