using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

public sealed class DuzenleyiciBulaniklikTests
{
    private const string Dal = "yan-bulaniklik";
    private const long Gb = 1_000_000_000;
    private const int W = 160;
    private const int H = 120;
    private static readonly double[] Anahtarlar = { 0, 2, 4, 6, 8 };
    private static readonly (int X, int Y, int W, int H) Bolge = (40, 30, 80, 60);

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

    private static ClipEffects Ortasi => ClipEffects.None.WithBlurRegion(0.75, 0.75, 0.25, 0.25);

    [Fact]
    public void ModelSiddetiVeBolgeyiNormallestirir()
    {
        var bolge = Ortasi;
        Assert.True(bolge.HasBlur);
        Assert.True(bolge.HasBlurRegion);
        Assert.False(bolge.IsNeutral);
        Assert.Equal(ClipEffects.DefaultBlurSigma, bolge.BlurSigma);
        Assert.Equal((0.25, 0.25, 0.5, 0.5), (bolge.BlurX, bolge.BlurY, bolge.BlurWidth, bolge.BlurHeight));
        Assert.Equal(Bolge, bolge.BlurRect(W, H));
        Assert.Equal((0, 0, W, H), (ClipEffects.None with { BlurSigma = 5 }).BlurRect(W, H));

        var kenar = new ClipEffects { BlurSigma = 3, BlurX = 0.9, BlurY = 0.9, BlurWidth = 0.5, BlurHeight = 0.5 }.Normalized();
        Assert.Equal(0.1, kenar.BlurWidth, 6);
        var (kx, ky, kw, kh) = kenar.BlurRect(333, 187);
        Assert.True(kx % 2 == 0 && ky % 2 == 0 && kw % 2 == 0 && kh % 2 == 0, $"{kx},{ky},{kw},{kh}");
        Assert.True(kx + kw <= 333 && ky + kh <= 187, $"{kx},{ky},{kw},{kh}");

        Assert.Equal(ClipEffects.MaxBlurSigma, new ClipEffects { BlurSigma = 900 }.Normalized().BlurSigma);
        Assert.Equal(ClipEffects.None, new ClipEffects { BlurSigma = -4, BlurX = 0.1, BlurY = 0.1, BlurWidth = 0.5, BlurHeight = 0.5 }.Normalized());
        Assert.Equal(ClipEffects.None, new ClipEffects { BlurSigma = double.NaN }.Normalized());
        var ufak = ClipEffects.None.WithBlurRegion(0.5, 0.5, 0.505, 0.9);
        Assert.True(ufak.HasBlur);
        Assert.False(ufak.HasBlurRegion);
        Assert.Equal((0.0, 0.0), (ufak.BlurX, ufak.BlurWidth));
        Assert.Equal(7.5, (new ClipEffects { BlurSigma = 7.5 }).WithBlurRegion(0, 0, 1, 1).BlurSigma);
    }

    [Fact]
    public void DisaAktarimGrafiButunKareVeBolgeYazar()
    {
        var butun = EditTimeline.FromSource(S(10));
        butun.SetEffects(new[] { 0 }, e => e with { BlurSigma = 6 });
        Assert.Contains("setpts=PTS-STARTPTS,gblur=sigma=6[v0]", Cizge(butun));

        var bolge = EditTimeline.FromSource(S(10));
        Assert.True(bolge.Split(S(5)));
        bolge.SetEffects(new[] { 0, 1 }, _ => Ortasi);
        var cizge = Cizge(bolge, Bilgi(W, H));
        Assert.Contains("setpts=PTS-STARTPTS,split[b0m][b0c];[b0c]crop=80:60:40:30,gblur=sigma=12[b0s];[b0m][b0s]overlay=40:30[v0]", cizge);
        Assert.Contains("split[b1m][b1c];[b1c]crop=80:60:40:30,gblur=sigma=12[b1s];[b1m][b1s]overlay=40:30[v1]", cizge);

        var donmus = EditTimeline.FromSource(S(10));
        donmus.SetEffects(new[] { 0 }, _ => Ortasi with { Rotation = 90 });
        Assert.Contains("transpose=1,setsar=1,split[b0m][b0c];[b0c]crop=60:80:30:40,", Cizge(donmus, Bilgi(W, H)));

        var yalin = EditTimeline.FromSource(S(10));
        Assert.DoesNotContain("gblur", Cizge(yalin));
    }

    [Fact]
    public void OnizlemeDisaAktarimlaAyniZinciriKurar()
    {
        var c = EditTimeline.FromSource(S(10));
        c.SetEffects(new[] { 0 }, _ => Ortasi);
        var onizleme = new EdlPreview("k.mp4", c);
        var zincir = new EditLook(onizleme, W, H).Geometry(onizleme.Parts[0]);
        var beklenen = ClipFilters.Blur(Ortasi, W, H, EditLook.PreviewBlurLabel);
        Assert.Equal(beklenen, zincir);
        Assert.Contains(ClipFilters.Blur(Ortasi, W, H, "b0"), Cizge(c, Bilgi(W, H)));
        Assert.Equal(beklenen.Replace(EditLook.PreviewBlurLabel, "b0"), ClipFilters.Blur(Ortasi, W, H, "b0"));

        var donmus = EditTimeline.FromSource(S(10));
        donmus.SetEffects(new[] { 0 }, _ => Ortasi with { Rotation = 90 });
        var donmusOnizleme = new EdlPreview("k.mp4", donmus);
        var donmusZincir = new EditLook(donmusOnizleme, W, H).Geometry(donmusOnizleme.Parts[0])!;
        Assert.EndsWith("setsar=1," + ClipFilters.Blur(Ortasi with { Rotation = 90 }, H, W, EditLook.PreviewBlurLabel), donmusZincir);
        Assert.Contains("crop=60:80:30:40", donmusZincir);
    }

    [Fact]
    public void ProjeDosyasiBulanikligiTasirEskiDosyaBulaniksizAcilir()
    {
        var model = EditTimeline.FromSource(S(60));
        Assert.True(model.Split(S(30)));
        model.SetEffects(new[] { 0 }, _ => Ortasi);
        model.SetEffects(new[] { 1 }, e => e with { BlurSigma = 4, Rotation = 90 });
        var json = EditProject.From(new SourceStamp(@"C:\bulanik.mp4", 16, 1), model).ToJson();
        Assert.Contains("\"blur\"", json);
        var okunan = EditProject.Parse(json);
        Assert.NotNull(okunan);
        Assert.Equal(model.Clips, okunan!.Clips);

        var eski = Regex.Replace(json, ",\\s*\"blur\":\\s*\\{[^}]*\\}", string.Empty);
        Assert.DoesNotContain("blur", eski);
        var eskiOkunan = EditProject.Parse(eski);
        Assert.NotNull(eskiOkunan);
        Assert.All(eskiOkunan!.Clips, k => Assert.False(k.Effects.HasBlur, k.Effects.ToString()));
        Assert.Equal(90, eskiOkunan.Clips[1].Effects.Rotation);

        var yalin = EditProject.From(new SourceStamp(@"C:\yalin.mp4", 16, 1), EditTimeline.FromSource(S(5))).ToJson();
        Assert.DoesNotContain("blur", yalin);
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

    private static string Dama(string klasor)
    {
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        Kos(ToolLocator.Ffmpeg, "-hide_banner", "-nostdin", "-v", "error", "-y", "-f", "lavfi",
            "-i", "color=black:s=" + W + "x" + H + ":r=10:d=2,format=yuv420p,geq=lum='if(mod(floor(X/4)+floor(Y/4),2),235,16)':cb=128:cr=128",
            "-c:v", "libx264", "-preset", "ultrafast", "-qp", "0", "-pix_fmt", "yuv420p", "-g", "5", "-threads", "2", kaynak);
        return kaynak;
    }

    private static byte[] Gri(string video, string kare, string? vf = null)
    {
        var args = new[] { "-hide_banner", "-nostdin", "-v", "error", "-y", "-ss", "1", "-i", video }
            .Concat(vf is null ? Array.Empty<string>() : new[] { "-vf", vf })
            .Concat(new[] { "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "gray", "-threads", "2", kare })
            .ToArray();
        Kos(ToolLocator.Ffmpeg, args);
        return File.ReadAllBytes(kare);
    }

    private static bool Icinde(int x, int y) => x >= Bolge.X && x < Bolge.X + Bolge.W && y >= Bolge.Y && y < Bolge.Y + Bolge.H;

    private static (double Ic, double Dis) Keskinlik(Func<int, int, int> deger, int pay)
    {
        double ic = 0, dis = 0;
        int icSay = 0, disSay = 0;
        for (var y = 0; y < H; y++)
        for (var x = 0; x < W - 1; x++)
        {
            var fark = Math.Abs(deger(x + 1, y) - deger(x, y));
            if (x >= Bolge.X + pay && x + 1 < Bolge.X + Bolge.W - pay && y >= Bolge.Y + pay && y < Bolge.Y + Bolge.H - pay)
            {
                ic += fark;
                icSay++;
            }
            else if (!Icinde(x - pay, y) && !Icinde(x + 1 + pay, y) && !Icinde(x, y - pay) && !Icinde(x, y + pay))
            {
                dis += fark;
                disSay++;
            }
        }

        return (ic / icSay, dis / disSay);
    }

    [FfmpegFact]
    public void OnizlemeZinciriBolgeyiBulaniklastirirDisiniDegistirmez()
    {
        var klasor = Path.Combine(Kanit, "bolge-zincir");
        Directory.CreateDirectory(klasor);
        var adlar = new[] { "kaynak.mp4", "yalin.gray", "bulanik.gray" };
        KanitKapanisi.Onceki(klasor, adlar);
        var kaynak = Dama(klasor);

        var c = EditTimeline.FromSource(S(2));
        c.SetEffects(new[] { 0 }, _ => Ortasi);
        var onizleme = new EdlPreview(kaynak, c);
        var zincir = new EditLook(onizleme, W, H).Geometry(onizleme.Parts[0])!;

        var yalin = Gri(kaynak, Path.Combine(klasor, "yalin.gray"));
        var bulanik = Gri(kaynak, Path.Combine(klasor, "bulanik.gray"), zincir);
        Assert.Equal(W * H, yalin.Length);
        Assert.Equal(W * H, bulanik.Length);

        var disFarki = 0;
        var icFarki = 0;
        for (var y = 0; y < H; y++)
        for (var x = 0; x < W; x++)
        {
            var esit = yalin[y * W + x] == bulanik[y * W + x];
            if (Icinde(x, y)) icFarki += esit ? 0 : 1;
            else disFarki += esit ? 0 : 1;
        }

        Assert.Equal(0, disFarki);
        Assert.True(icFarki > Bolge.W * Bolge.H / 2, $"bolge icinde degisen piksel az: {icFarki}");
        var oncesi = Keskinlik((x, y) => yalin[y * W + x], 0);
        var sonrasi = Keskinlik((x, y) => bulanik[y * W + x], 0);
        Assert.True(oncesi.Ic > 40, $"kaynak dama keskin degil: {oncesi.Ic:0.0}");
        Assert.True(sonrasi.Ic < oncesi.Ic / 8, $"bolge bulaniklasmadi: {sonrasi.Ic:0.0} / {oncesi.Ic:0.0}");

        KanitKapanisi.Kapat(klasor, adlar);
    }

    [FfmpegFact]
    public async Task DisaAktarimBolgeyiBulaniklastirirDisiniKorur()
    {
        var klasor = Path.Combine(Kanit, "bolge-aktar");
        Directory.CreateDirectory(klasor);
        var adlar = new[] { "kaynak.mp4", "cikti.mp4", "cikti.gray" };
        KanitKapanisi.Onceki(klasor, adlar);
        var kaynak = Dama(klasor);

        var c = EditTimeline.FromSource(S(2));
        c.SetEffects(new[] { 0 }, _ => Ortasi);
        var cikti = Path.Combine(klasor, "cikti.mp4");
        var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, c, ExportMode.Smart, cikti, 8 * Gb);
        Assert.Equal(ExportMode.Full, plan.Effective);
        var args = plan.Steps[0].Args.ToList();
        Assert.Contains("crop=80:60:40:30,gblur=sigma=12", args[args.IndexOf("-filter_complex") + 1]);
        args.InsertRange(args.Count - 1, new[] { "-threads", "2" });
        await EditExportRunner.RunAsync(plan with { Steps = new[] { plan.Steps[0] with { Args = args } } }, null);

        var kare = Gri(cikti, Path.Combine(klasor, "cikti.gray"));
        Assert.Equal(W * H, kare.Length);
        var (ic, dis) = Keskinlik((x, y) => kare[y * W + x], 4);
        Assert.True(dis > 30, $"bolge disi bulaniklasti: {dis:0.0}");
        Assert.True(ic < 6, $"bolge ici keskin kaldi: {ic:0.0}");

        KanitKapanisi.Kapat(klasor, adlar);
    }

    [FfmpegFact]
    public async Task LibmpvBolgeyiBulaniklastirir()
    {
        var klasor = Path.Combine(Kanit, "bolge-mpv");
        Directory.CreateDirectory(klasor);
        var adlar = new[] { "kaynak.mp4" };
        KanitKapanisi.Onceki(klasor, adlar);
        var kaynak = Dama(klasor);

        var c = EditTimeline.FromSource(S(2));
        c.SetEffects(new[] { 0 }, _ => Ortasi);
        var onizleme = new EdlPreview(kaynak, c);
        var gorunum = new EditLook(onizleme, W, H);

        using (var motor = new MpvEngine())
        {
            motor.SetProperty("ao", "null");
            var surucu = new EdlPreviewDriver(motor, onizleme, look: gorunum);
            await surucu.OpenAsync();
            motor.Pause();
            await surucu.SeekAsync(S(1), SeekPrecision.Exact);

            var saat = Stopwatch.StartNew();
            long gorulen = 0;
            (double Ic, double Dis)? olcum = null;
            while (saat.Elapsed < TimeSpan.FromSeconds(10) && (olcum is null || olcum.Value.Ic >= 6))
            {
                motor.TryCopyLatest(ref gorulen, (p, w, h, s) =>
                {
                    if (w != W || h != H) return;
                    var buf = new byte[s * h];
                    Marshal.Copy(p, buf, 0, buf.Length);
                    olcum = Keskinlik((x, y) => buf[y * s + x * 4 + 1], 4);
                });
                await Task.Delay(20);
            }

            Assert.True(olcum is not null, "kare gelmedi; son log: " + string.Join(" | ", motor.RecentLog));
            Assert.True(olcum!.Value.Dis > 30, $"libmpv bolge disini bulaniklastirdi: {olcum.Value.Dis:0.0}");
            Assert.True(olcum.Value.Ic < 6, $"libmpv bolgeyi bulaniklastirmadi: {olcum.Value.Ic:0.0}; log: " + string.Join(" | ", motor.RecentLog));
        }

        KanitKapanisi.Kapat(klasor, adlar);
    }
}
