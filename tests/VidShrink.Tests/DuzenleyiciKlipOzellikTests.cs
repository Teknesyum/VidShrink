using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VidShrink.App.Editing;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Duzenleyicide klip basina ayarlar: kirpma, dondurme ve cevirme, ses kazanci ve sessize alma,
/// giris/cikis solmasi. Model (<c>ClipEffects</c>, 10 geri 10 ileri, bolmede solmanin kenarda
/// kalmasi), disa aktarim grafi (<c>transpose</c>, <c>crop</c>, tuval <c>pad</c>, <c>fade</c>,
/// <c>afade</c>, <c>volume</c>; etkisiz cizelgede graf degismez), Hizli/Akilli kipin Tam'a
/// dusmesi, onizlemenin EDL zamanli zincirleri, canli ffmpeg ciktisinin ffprobe olculeri,
/// libmpv'de geometri ve solmanin EDL zamaninda uygulanmasi, arayuzun paneli ve EDL'yi yeniden
/// acmadan tazelemesi. Temel renk (<c>eq</c>, <c>colortemperature</c>) ve hazir gorunumler:
/// normallestirme, grafta geometriden sonra solmadan once, onizlemede parca penceresinde,
/// proje dosyasinda eski dosyanin notr acilmasi, canli karede siyah beyaz/parlaklik/sicaklik ve
/// zaman penceresi, libmpv'de EDL zamaninda renk, arayuzde kutular ve hazir gorunum secimi.
/// Kanit <c>.calisma/worktree-agent-a9a2ac977d9da6c2b/</c>, test siler.
/// </summary>
public sealed class DuzenleyiciKlipOzellikTests
{
    private const string Dal = "worktree-agent-a9a2ac977d9da6c2b";
    private const long Gb = 1_000_000_000;

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

    private static string Deger(IReadOnlyList<string> args, string anahtar) => args[args.ToList().IndexOf(anahtar) + 1];

    private static readonly double[] Anahtarlar = { 0, 2, 4, 6, 8 };

    private static string Cizge(EditTimeline cizelge, ExportMode kip = ExportMode.Full, MediaInfo? bilgi = null)
        => Deger(EditExport.Build(cizelge, bilgi ?? Bilgi(), Anahtarlar, 0, kip, "cikti.mp4", "is", 8 * Gb).Steps[0].Args, "-filter_complex");

    [Fact]
    public void AyarlarNormallesirKirpmaCiftBoyutOranKorunur()
    {
        var bozuk = new ClipEffects { CropLeft = 0.7, CropRight = 0.7, CropTop = -1, Rotation = 450, VolumeDb = 99, FadeIn = -5 }.Normalized();
        Assert.Equal(0.7, bozuk.CropLeft, 9);
        Assert.Equal(0.25, bozuk.CropRight, 9);
        Assert.Equal(0, bozuk.CropTop);
        Assert.Equal(90, bozuk.Rotation);
        Assert.Equal(ClipEffects.MaxVolumeDb, bozuk.VolumeDb);
        Assert.Equal(0, bozuk.FadeIn);
        Assert.Equal(270, new ClipEffects { Rotation = -90 }.Normalized().Rotation);
        Assert.True(ClipEffects.None.Normalized().IsNeutral);

        var dikey = ClipEffects.None.WithAspect(9, 16, 320, 240);
        Assert.Equal((92, 0, 136, 240), dikey.CropRect(320, 240));
        Assert.Equal((136, 240), dikey.OutputSize(320, 240));

        var donmus = (ClipEffects.None with { Rotation = 90 }).WithAspect(16, 9, 1920, 1080);
        var (w, h) = donmus.OutputSize(1920, 1080);
        Assert.Equal((1080, 608), (w, h));
        Assert.Equal(0, w % 2);
        Assert.Equal(0, h % 2);
        Assert.InRange((double)w / h, 16.0 / 9 - 0.01, 16.0 / 9 + 0.01);

        foreach (var (num, den) in EditorView.CropPresets.Skip(1))
        foreach (var aci in new[] { 0, 90, 180, 270 })
        {
            var e = (ClipEffects.None with { Rotation = aci }).WithAspect(num, den, 333, 187);
            var r = e.CropRect(333, 187);
            Assert.True(r.Width % 2 == 0 && r.Height % 2 == 0 && r.X % 2 == 0 && r.Y % 2 == 0, $"{num}:{den} {aci} tek boyut {r}");
            Assert.True(r.X + r.Width <= 333 && r.Y + r.Height <= 187, $"{num}:{den} {aci} kareden tasti {r}");
        }

        Assert.False(ClipEffects.None.WithAspect(16, 9, 1920, 1080).HasCrop);
        Assert.False((ClipEffects.None with { Rotation = 90 }).WithAspect(9, 16, 1920, 1080).HasCrop);
        Assert.Equal((1920, 1080), ClipEffects.None.OutputSize(1920, 1080));
        Assert.Equal((S(3), S(1)), new ClipEffects { FadeIn = S(3), FadeOut = S(3) }.Fades(S(4)));
    }

    private static EditClip[] Anlik(EditTimeline c) => c.Clips.ToArray();

    [Fact]
    public void OnIslemOnGeriOnIleriAnlikEsit()
    {
        var c = EditTimeline.FromSource(S(60));
        var islemler = new Func<bool>[]
        {
            () => c.SetEffects(new[] { 0 }, e => e with { Rotation = 90 }),
            () => c.SetEffects(new[] { 0 }, e => e with { FlipH = true }),
            () => c.SetEffects(new[] { 0 }, e => e.WithAspect(1, 1, 1920, 1080)),
            () => c.SetEffects(new[] { 0 }, e => e with { VolumeDb = -6 }),
            () => c.SetEffects(new[] { 0 }, e => e with { FadeIn = S(1) }),
            () => c.Split(S(30)),
            () => c.SetEffects(new[] { 1 }, e => e with { Muted = true }),
            () => c.SetSpeed(1, -2m),
            () => c.SetEffects(new[] { 0, 1 }, e => e with { FadeOut = S(2), FlipV = true }),
            () => c.SetEffects(new[] { 0, 1 }, _ => ClipEffects.None)
        };

        var anliklar = new List<EditClip[]> { Anlik(c) };
        foreach (var islem in islemler)
        {
            Assert.True(islem(), $"islem {anliklar.Count} yapilamadi");
            var simdi = Anlik(c);
            Assert.False(anliklar[^1].SequenceEqual(simdi), $"islem {anliklar.Count} durumu degistirmedi");
            anliklar.Add(simdi);
        }

        Assert.False(c.SetEffects(new[] { 0, 1 }, e => e with { VolumeDb = 0.01 }));
        Assert.False(c.SetEffects(Array.Empty<int>(), e => e with { Rotation = 90 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => c.SetEffects(new[] { 5 }, e => e));

        for (var k = islemler.Length - 1; k >= 0; k--)
        {
            Assert.True(c.Undo());
            Assert.True(anliklar[k].SequenceEqual(Anlik(c)), $"geri {islemler.Length - k} anliga donmedi");
        }

        Assert.False(c.Undo());
        for (var k = 1; k <= islemler.Length; k++)
        {
            Assert.True(c.Redo());
            Assert.True(anliklar[k].SequenceEqual(Anlik(c)), $"ileri {k} anliga donmedi");
        }

        Assert.False(c.Redo());
    }

    [Fact]
    public void BolmeSolmayiKenardaTutarKirpmaVeHizAyariTasir()
    {
        var ayar = new ClipEffects { Rotation = 180, VolumeDb = -3, FadeIn = S(1), FadeOut = S(2) };
        var c = EditTimeline.FromSource(S(10));
        Assert.True(c.SetEffects(new[] { 0 }, _ => ayar));
        Assert.True(c.Split(S(4)));
        Assert.Equal(ayar with { FadeOut = 0 }, c.Clips[0].Effects);
        Assert.Equal(ayar with { FadeIn = 0 }, c.Clips[1].Effects);

        var ters = EditTimeline.FromSource(S(10));
        ters.SetEffects(new[] { 0 }, _ => ayar);
        Assert.True(ters.SetSpeed(0, -1m));
        Assert.Equal(ayar, ters.Clips[0].Effects);
        Assert.True(ters.Split(S(4)));
        Assert.True(ters.Clips[0].Reversed);
        Assert.Equal(ayar with { FadeOut = 0 }, ters.Clips[0].Effects);
        Assert.Equal(ayar with { FadeIn = 0 }, ters.Clips[1].Effects);

        var kirp = EditTimeline.FromSource(S(10));
        kirp.SetEffects(new[] { 0 }, _ => ayar);
        Assert.True(kirp.TrimEdge(0, head: true, S(2)));
        Assert.Equal(ayar, kirp.Clips[0].Effects);
        Assert.True(kirp.SetSpeedAll(2m));
        Assert.Equal(ayar, kirp.Clips[0].Effects);

        var yalin = EditTimeline.FromSource(S(10));
        Assert.True(yalin.Split(S(4)));
        Assert.All(yalin.Clips, k => Assert.True(k.Effects.IsNeutral));
    }

    [Fact]
    public void DisaAktarimGrafiGeometriSolmaVeSesYazar()
    {
        var yalin = EditTimeline.FromSource(S(10));
        var yalinCizge = Cizge(yalin);
        foreach (var yok in new[] { "setsar", "transpose", "crop=", "hflip", "vflip", "fade=", "volume=", "pad=" })
            Assert.DoesNotContain(yok, yalinCizge);

        var sadeceSes = EditTimeline.FromSource(S(10));
        sadeceSes.SetEffects(new[] { 0 }, e => e with { VolumeDb = -10 });
        var sesCizge = Cizge(sadeceSes);
        Assert.Contains("asetpts=PTS-STARTPTS,volume=-10dB[a0]", sesCizge);
        Assert.DoesNotContain("setsar", sesCizge);
        Assert.Equal(yalinCizge.Replace("asetpts=PTS-STARTPTS[a0]", "asetpts=PTS-STARTPTS,volume=-10dB[a0]"), sesCizge);

        var c = EditTimeline.FromSource(S(10));
        c.SetEffects(new[] { 0 }, _ => new ClipEffects { Rotation = 90, FadeIn = S(0.5), FadeOut = S(1), Muted = true });
        var cizge = Cizge(c);
        Assert.Contains("setpts=PTS-STARTPTS,transpose=1,setsar=1,fade=t=in:st=0:d=0.5,fade=t=out:st=9:d=1[v0]", cizge);
        Assert.Contains("asetpts=PTS-STARTPTS,volume=0,afade=t=in:st=0:d=0.5,afade=t=out:st=9:d=1[a0]", cizge);

        var sol = EditTimeline.FromSource(S(10));
        sol.SetEffects(new[] { 0 }, _ => new ClipEffects { Rotation = 270, FlipH = true });
        Assert.Contains("transpose=2,hflip,setsar=1[v0]", Cizge(sol));
        var yarim = EditTimeline.FromSource(S(10));
        yarim.SetEffects(new[] { 0 }, _ => new ClipEffects { Rotation = 180, FlipH = true });
        Assert.Contains("setpts=PTS-STARTPTS,vflip,setsar=1[v0]", Cizge(yarim));

        var dikey = EditTimeline.FromSource(S(10));
        dikey.SetEffects(new[] { 0 }, e => e.WithAspect(9, 16, 1920, 1080));
        Assert.Contains("crop=608:1080:656:0,setsar=1[v0]", Cizge(dikey));
        Assert.DoesNotContain("pad=", Cizge(dikey));

        var karma = EditTimeline.FromSource(S(10));
        karma.Split(S(5));
        karma.SetEffects(new[] { 1 }, e => e with { Rotation = 90 });
        var karmaCizge = Cizge(karma);
        Assert.Contains("setpts=PTS-STARTPTS,setsar=1[v0]", karmaCizge);
        Assert.Contains("transpose=1,scale=1920:1080:force_original_aspect_ratio=decrease:force_divisible_by=2,pad=1920:1080:(ow-iw)/2:(oh-ih)/2,setsar=1[v1]", karmaCizge);

        var anamorfik = Bilgi(1440, 1080) with { ParNum = 4, ParDen = 3 };
        var ana = EditTimeline.FromSource(S(10));
        ana.SetEffects(new[] { 0 }, e => e with { FlipV = true });
        Assert.Contains("setpts=PTS-STARTPTS,scale=1920:1080,vflip,setsar=1[v0]", Cizge(ana, bilgi: anamorfik));
    }

    [Theory]
    [InlineData(ExportMode.Fast)]
    [InlineData(ExportMode.Smart)]
    public void AyarVarsaTamaDuserYoksaKipKalir(ExportMode kip)
    {
        var sinirlar = DuzenleyiciAkilliKodekTests.Sinirlar(Anahtarlar);
        var yalin = EditExport.Build(EditTimeline.FromSource(S(10)), Bilgi(), Anahtarlar, 0, kip, "cikti.mp4", "is", 8 * Gb, sinirlar);
        Assert.Equal(kip, yalin.Effective);
        Assert.False(yalin.EffectsForcedFull);

        var c = EditTimeline.FromSource(S(10));
        c.SetEffects(new[] { 0 }, e => e with { FadeIn = S(1) });
        var plan = EditExport.Build(c, Bilgi(), Anahtarlar, 0, kip, "cikti.mp4", "is", 8 * Gb, sinirlar);
        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.True(plan.EffectsForcedFull);
        Assert.Contains("fade=t=in:st=0:d=1", Deger(plan.Steps[0].Args, "-filter_complex"));

        var tam = EditExport.Build(c, Bilgi(), Anahtarlar, 0, ExportMode.Full, "cikti.mp4", "is", 8 * Gb);
        Assert.False(tam.EffectsForcedFull);

        c.SetEffects(new[] { 0 }, _ => ClipEffects.None);
        Assert.False(c.HasEffects);
        Assert.Equal(kip, EditExport.Build(c, Bilgi(), Anahtarlar, 0, kip, "cikti.mp4", "is", 8 * Gb, sinirlar).Effective);
    }

    [Fact]
    public void OnizlemeZincirleriParcaPenceresindeKalir()
    {
        var c = EditTimeline.FromSource(S(10));
        Assert.True(c.DeleteRange(S(2), S(4)));
        Assert.Equal((null, null), ClipFilters.Timed(new EdlPreview("k.mp4", c)));

        c.SetEffects(new[] { 1 }, e => e with { FadeIn = S(1), FadeOut = S(0.5), VolumeDb = -6 });
        var (video, ses) = ClipFilters.Timed(new EdlPreview("k.mp4", c));
        Assert.Equal("fade=t=in:st=2:d=1:enable='gte(t,2)*lt(t,3)',fade=t=out:st=7.5:d=0.5:enable='gte(t,7.5)*lt(t,8)'", video);
        Assert.Equal("volume=volume=-6dB:enable='gte(t,2)*lt(t,8)',afade=t=in:st=2:d=1:enable='gte(t,2)*lt(t,3)',afade=t=out:st=7.5:d=0.5:enable='gte(t,7.5)*lt(t,8)'", ses);

        Assert.True(c.SetSpeed(1, -2m));
        var (tersVideo, _) = ClipFilters.Timed(new EdlPreview("k.mp4", c));
        Assert.Equal("fade=t=in:st=2:d=1:enable='gte(t,2)*lt(t,3)',fade=t=out:st=6:d=2:enable='gte(t,6)*lt(t,8)'", tersVideo);

        c.SetEffects(new[] { 0 }, e => e with { Muted = true });
        var (_, sessiz) = ClipFilters.Timed(new EdlPreview("k.mp4", c));
        Assert.StartsWith("volume=volume=0:enable='gte(t,0)*lt(t,2)',", sessiz);

        var gorunum = new EditLook(new EdlPreview("k.mp4", c), 320, 240);
        Assert.Null(gorunum.Canvas);
        Assert.All(new EdlPreview("k.mp4", c).Parts, p => Assert.Null(gorunum.Geometry(p)));

        c.SetEffects(new[] { 1 }, e => e with { Rotation = 90 });
        var onizleme = new EdlPreview("k.mp4", c);
        var donmus = new EditLook(onizleme, 320, 240);
        Assert.Equal((320, 240), donmus.Canvas);
        Assert.Equal("setsar=1", donmus.Geometry(onizleme.Parts[0]));
        Assert.StartsWith("transpose=1,scale=320:240:", donmus.Geometry(onizleme.Parts[1]));
    }

    private static string Ffmpeg(params string[] args)
    {
        var psi = new ProcessStartInfo(ToolLocator.Ffmpeg)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        return Kos(psi);
    }

    private static string Ffprobe(params string[] args)
    {
        var psi = new ProcessStartInfo(ToolLocator.Ffprobe)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        return Kos(psi, cikti: true);
    }

    private static string Kos(ProcessStartInfo psi, bool cikti = false)
    {
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var standart = surec.StandardOutput.ReadToEndAsync();
        if (!surec.WaitForExit(60_000))
        {
            surec.Kill(true);
            Assert.Fail(Path.GetFileName(psi.FileName) + " 60 sn icinde bitmedi");
        }

        surec.WaitForExit();
        Assert.True(surec.ExitCode == 0, hata.Result);
        return cikti ? standart.Result : hata.Result;
    }

    private static ExportPlan IkiIsParcacigi(ExportPlan plan)
    {
        var args = plan.Steps[0].Args.ToList();
        args.InsertRange(args.Count - 1, new[] { "-threads", "2" });
        return plan with { Steps = new[] { plan.Steps[0] with { Args = args } } };
    }

    private static (int W, int H, double Sure) Boyut(string video)
    {
        var satirlar = Ffprobe("-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height:format=duration",
            "-of", "default=noprint_wrappers=1", video).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string Al(string ad) => satirlar.First(s => s.StartsWith(ad + "=", StringComparison.Ordinal))[(ad.Length + 1)..];
        return (int.Parse(Al("width"), CultureInfo.InvariantCulture), int.Parse(Al("height"), CultureInfo.InvariantCulture),
            double.Parse(Al("duration"), CultureInfo.InvariantCulture));
    }

    private static double SesOrtalamasi(string video)
    {
        var dokum = Ffmpeg("-hide_banner", "-nostdin", "-ss", "1", "-i", video, "-map", "0:a", "-af", "volumedetect", "-threads", "2", "-f", "null", "-");
        var eslesme = Regex.Match(dokum, @"mean_volume: (-?[\d.]+|-inf) dB");
        Assert.True(eslesme.Success, dokum);
        return eslesme.Groups[1].Value == "-inf" ? double.NegativeInfinity : double.Parse(eslesme.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static double OrtaParlaklik(string video, string kare, double saniye, int w, int h)
    {
        Ffmpeg("-hide_banner", "-nostdin", "-v", "error", "-y", "-ss", saniye.ToString(CultureInfo.InvariantCulture), "-i", video,
            "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "gray", "-threads", "2", kare);
        var piksel = File.ReadAllBytes(kare);
        Assert.Equal(w * h, piksel.Length);
        return piksel.Average(b => (double)b);
    }

    [FfmpegFact]
    public async Task CanliDisaAktarmaDondururKirparSesiVeSolmayiUygular()
    {
        var klasor = Path.Combine(Kanit, "klip-canli");
        Directory.CreateDirectory(klasor);
        var adlar = new[] { "kaynak.mp4", "donmus.mp4", "dikey.mp4", "yalin.mp4", "ilk.gray", "orta.gray" };
        KanitKapanisi.Onceki(klasor, adlar);
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        Ffmpeg("-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "color=white:s=320x240:r=30:d=2",
            "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=2",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", "-threads", "2", kaynak);

        async Task<string> Aktar(string ad, Func<ClipEffects, ClipEffects> ayar, ExportMode kip)
        {
            var c = EditTimeline.FromSource(S(2));
            c.SetEffects(new[] { 0 }, ayar);
            var cikti = Path.Combine(klasor, ad);
            var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, c, kip, cikti, 8 * Gb);
            Assert.Equal(ExportMode.Full, plan.Effective);
            await EditExportRunner.RunAsync(IkiIsParcacigi(plan), null);
            return cikti;
        }

        var donmus = await Aktar("donmus.mp4", e => e with { Rotation = 90, VolumeDb = -10, FadeIn = S(0.5) }, ExportMode.Fast);
        var dikey = await Aktar("dikey.mp4", e => e.WithAspect(9, 16, 320, 240) with { Muted = true }, ExportMode.Smart);
        var yalin = await Aktar("yalin.mp4", e => e, ExportMode.Full);

        var (dw, dh, dsure) = Boyut(donmus);
        Assert.Equal((240, 320), (dw, dh));
        Assert.InRange(dsure, 1.9, 2.15);
        var (kw, kh, ksure) = Boyut(dikey);
        Assert.Equal((136, 240), (kw, kh));
        Assert.InRange(ksure, 1.9, 2.15);
        Assert.Equal((320, 240), (Boyut(yalin).W, Boyut(yalin).H));

        var ortalama = SesOrtalamasi(yalin);
        var kisik = SesOrtalamasi(donmus);
        Assert.True(ortalama > -30, $"yalin ortalama {ortalama}");
        Assert.InRange(kisik - ortalama, -10.5, -9.5);
        Assert.True(SesOrtalamasi(dikey) < -80, "sessiz klipte ses var");

        var ilk = OrtaParlaklik(donmus, Path.Combine(klasor, "ilk.gray"), 0, 240, 320);
        var orta = OrtaParlaklik(donmus, Path.Combine(klasor, "orta.gray"), 1, 240, 320);
        Assert.True(ilk < 40, $"solmali ilk kare parlak {ilk}");
        Assert.True(orta > 200, $"solma sonrasi kare karanlik {orta}");
        Assert.True(OrtaParlaklik(yalin, Path.Combine(klasor, "ilk.gray"), 0, 320, 240) > 200, "solmasiz ilk kare karanlik");

        KanitKapanisi.Kapat(klasor, adlar);
    }

    private static async Task<(int W, int H, int Parlak, long Gorulen)> KareBekleAsync(MpvEngine motor, long gorulen, Func<(int W, int H, int Parlak), bool> kosul)
    {
        var saat = Stopwatch.StartNew();
        var kare = await KareAsync(motor, gorulen);
        while (!kosul((kare.W, kare.H, kare.Parlak)) && saat.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(20);
            kare = await KareAsync(motor, -1);
        }
        return kare;
    }

    private static async Task<(int W, int H, int Parlak, long Gorulen)> KareAsync(MpvEngine motor, long gorulen)
    {
        var saat = Stopwatch.StartNew();
        (int W, int H, int Parlak)? sonuc = null;
        while (sonuc is null && saat.Elapsed < TimeSpan.FromSeconds(10))
        {
            motor.TryCopyLatest(ref gorulen, (p, w, h, s) =>
            {
                var satir = new byte[s];
                Marshal.Copy(p + (h / 2) * s, satir, 0, s);
                var i = (w / 2) * 4;
                sonuc = (w, h, Math.Min(satir[i], Math.Min(satir[i + 1], satir[i + 2])));
            });
            if (sonuc is null) await Task.Delay(10);
        }

        Assert.True(sonuc is not null, "10 sn icinde kare gelmedi; son log: " + string.Join(" | ", motor.RecentLog));
        return (sonuc!.Value.W, sonuc.Value.H, sonuc.Value.Parlak, gorulen);
    }

    [FfmpegFact]
    public async Task LibmpvGeometriVeSolmaEdlZamanindaUygulanir()
    {
        var klasor = Path.Combine(Kanit, "klip-mpv");
        Directory.CreateDirectory(klasor);
        var adlar = new[] { "kaynak.mp4" };
        KanitKapanisi.Onceki(klasor, adlar);
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        Ffmpeg("-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "color=white:s=320x240:r=30:d=3",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "15", "-threads", "2", "-pix_fmt", "yuv420p", kaynak);

        var c = EditTimeline.FromSource(S(3));
        Assert.True(c.DeleteRange(S(0.5), S(1.5)));
        c.SetEffects(new[] { 0, 1 }, e => e with { Rotation = 90 });
        c.SetEffects(new[] { 1 }, e => e with { FadeIn = S(1) });
        var onizleme = new EdlPreview(kaynak, c);
        var gorunum = new EditLook(onizleme, 320, 240);
        Assert.Contains("st=0.5:d=1:enable='gte(t,0.5)*lt(t,1.5)'", gorunum.TimedVideo);

        using (var motor = new MpvEngine())
        {
            motor.SetProperty("ao", "null");
            var surucu = new EdlPreviewDriver(motor, onizleme, look: gorunum);
            await surucu.OpenAsync();
            motor.Pause();

            await surucu.SeekAsync(S(0.3), SeekPrecision.Exact);
            var once = await KareBekleAsync(motor, 0, k => (k.W, k.H) == (240, 320) && k.Parlak > 200);
            Assert.Equal((240, 320), (once.W, once.H));
            Assert.True(once.Parlak > 200, $"birinci parca {once.Parlak}");

            await surucu.SeekAsync(S(0.6), SeekPrecision.Exact);
            var solan = await KareBekleAsync(motor, once.Gorulen, k => k.Parlak < 80);
            Assert.True(solan.Parlak < 80, $"EDL 0.6 sn'de solma yok (kaynak 1.6 sn zamani olurdu): {solan.Parlak}");

            await surucu.SeekAsync(S(1.8), SeekPrecision.Exact);
            var sonra = await KareBekleAsync(motor, solan.Gorulen, k => k.Parlak > 200);
            Assert.True(sonra.Parlak > 200, $"solma penceresi disinda {sonra.Parlak}");

            var yalin = EditTimeline.FromSource(S(3));
            Assert.True(yalin.DeleteRange(S(0.5), S(1.5)));
            await surucu.RestyleAsync(new EdlPreview(kaynak, yalin), null);
            await surucu.SeekAsync(S(0.6), SeekPrecision.Exact);
            var yalinKare = await KareBekleAsync(motor, sonra.Gorulen, k => (k.W, k.H) == (320, 240) && k.Parlak > 200);
            Assert.Equal((320, 240), (yalinKare.W, yalinKare.H));
            Assert.True(yalinKare.Parlak > 200, $"ayarsiz onizlemede solma kaldi {yalinKare.Parlak}");
        }

        KanitKapanisi.Kapat(klasor, adlar);
    }

    [Fact]
    public void ArayuzPaneliAyariYazarEdlyiYenidenAcmadanTazeler()
    {
        var klasor = Path.Combine(Kanit, "klip-arayuz");
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
                    var acilan = motor.Acilan;

                    view.TimelineView.SelectedIndex = -1;
                    Assert.False(view.ClipPanel.IsVisible);
                    Assert.False(view.RotateClip());

                    view.TimelineView.SelectedIndex = 0;
                    Assert.True(view.ClipPanel.IsVisible);
                    Assert.Equal(0, view.CmbClipCrop.SelectedIndex);

                    Assert.True(view.RotateClip());
                    Assert.Equal(90, model.Clips[0].Effects.Rotation);
                    DenetimSurucu.Pump(view.Player, () => view.Restyles >= 1, 10);
                    Assert.Equal(1, view.Restyles);
                    Assert.Equal(acilan, motor.Acilan);
                    Assert.Equal("lavfi=[transpose=1,setsar=1]", motor.Geometri);

                    view.CmbClipCrop.SelectedIndex = 2;
                    Assert.Equal((180, 320), model.Clips[0].Effects.OutputSize(320, 240));
                    DenetimSurucu.Pump(view.Player, () => view.Restyles >= 2, 10);

                    view.TxtClipFadeIn.Text = "0.5";
                    view.TxtClipFadeIn.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
                    Assert.Equal(S(0.5), model.Clips[0].Effects.FadeIn);
                    DenetimSurucu.Pump(view.Player, () => view.Restyles >= 3, 10);
                    Assert.Contains("fade=t=in:st=0:d=0.5", motor.ZamanliVideo);
                    Assert.Contains("afade=t=in:st=0:d=0.5", motor.ZamanliSes);

                    view.BtnClipMute.IsChecked = true;
                    view.BtnClipMute.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.True(model.Clips[0].Effects.Muted);

                    Assert.True(view.Undo());
                    Assert.False(model.Clips[0].Effects.Muted);
                    Assert.True(view.Undo());
                    Assert.Equal(0, model.Clips[0].Effects.FadeIn);
                    Assert.True(view.Undo());
                    Assert.False(model.Clips[0].Effects.HasCrop);
                    Assert.True(view.Undo());
                    Assert.True(model.Clips[0].Effects.IsNeutral);
                    Assert.False(view.Undo());
                    DenetimSurucu.Pump(view.Player, () => view.Restyles >= 8, 10);
                    Assert.Null(motor.Geometri);
                    Assert.Equal(acilan, motor.Acilan);

                    Assert.True(view.Redo());
                    view.BtnClipReset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.True(model.Clips[0].Effects.IsNeutral);

                    view.TimelineView.Playhead = S(5);
                    Assert.True(view.Split());
                    DenetimSurucu.Pump(view.Player, () => motor.Acilan > acilan, 10);
                    Assert.True(motor.Acilan > acilan, "kesim degisince EDL yeniden acilmadi");
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

    [Fact]
    public void RenkAyarlariNormallesirHazirGorunumlerKayitliDegerdir()
    {
        Assert.False(ClipEffects.None.HasColor);
        Assert.Equal(0, ClipEffects.None.Brightness);
        Assert.Equal(1, ClipEffects.None.Contrast);
        Assert.Equal(1, ClipEffects.None.Saturation);
        Assert.Equal(ClipEffects.NeutralTemperature, ClipEffects.None.Temperature);
        Assert.Equal(6500, ClipEffects.NeutralTemperature);

        foreach (var tek in new[]
                 {
                     ClipEffects.None with { Brightness = 0.1 }, ClipEffects.None with { Contrast = 1.2 },
                     ClipEffects.None with { Saturation = 0 }, ClipEffects.None with { Temperature = 4000 },
                 })
        {
            Assert.True(tek.HasColor, tek.ToString());
            Assert.False(tek.IsNeutral, tek.ToString());
            Assert.False(tek.HasGeometry);
            Assert.False(tek.HasAudio);
        }

        var bozuk = new ClipEffects { Brightness = 5, Contrast = -3, Saturation = 9, Temperature = 100 }.Normalized();
        Assert.Equal((1.0, 0.0, 3.0, 1000), (bozuk.Brightness, bozuk.Contrast, bozuk.Saturation, bozuk.Temperature));
        var tavan = new ClipEffects { Brightness = -5, Contrast = 7, Temperature = 99999 }.Normalized();
        Assert.Equal((-1.0, 2.0, 40000), (tavan.Brightness, tavan.Contrast, tavan.Temperature));
        var sonsuz = new ClipEffects { Brightness = double.NaN, Contrast = double.PositiveInfinity, Saturation = double.NaN }.Normalized();
        Assert.False(sonsuz.HasColor, sonsuz.ToString());
        var yuvarlak = new ClipEffects { Brightness = 0.123, Contrast = 1.456, Saturation = 0.789 }.Normalized();
        Assert.Equal((0.12, 1.46, 0.79), (yuvarlak.Brightness, yuvarlak.Contrast, yuvarlak.Saturation));

        Assert.Equal(0, ClipEffects.BlackWhite.Saturation);
        Assert.Equal(ClipEffects.BlackWhite, ClipEffects.BlackWhite.Normalized());
        Assert.False(ClipEffects.BlackWhite.HasGeometry || ClipEffects.BlackWhite.HasAudio);
        Assert.Equal(ClipEffects.OldFilm, ClipEffects.OldFilm.Normalized());
        Assert.True(ClipEffects.OldFilm.Saturation is > 0 and < 1, "eski film renksiz ya da doygun");
        Assert.True(ClipEffects.OldFilm.Temperature < ClipEffects.NeutralTemperature, "eski film sicak degil");
        Assert.NotEqual(ClipEffects.BlackWhite, ClipEffects.OldFilm);

        var klip = new ClipEffects { Rotation = 90, VolumeDb = -6, FadeIn = S(1), Brightness = 0.3 };
        var giydirilmis = klip.WithColor(ClipEffects.OldFilm);
        Assert.Equal(klip with
        {
            Brightness = ClipEffects.OldFilm.Brightness, Contrast = ClipEffects.OldFilm.Contrast,
            Saturation = ClipEffects.OldFilm.Saturation, Temperature = ClipEffects.OldFilm.Temperature,
        }, giydirilmis);
        Assert.Equal(klip with { Brightness = 0 }, klip.WithColor(ClipEffects.None));
    }

    [Fact]
    public void DisaAktarimGrafiRenkSuzgeciniGeometridenSonraSolmadanOnceYazar()
    {
        var yalin = Cizge(EditTimeline.FromSource(S(10)));
        Assert.DoesNotContain("eq=", yalin);
        Assert.DoesNotContain("colortemperature", yalin);

        var c = EditTimeline.FromSource(S(10));
        c.SetEffects(new[] { 0 }, _ => new ClipEffects { Rotation = 90, FadeIn = S(0.5), Brightness = 0.1, Contrast = 1.2, Saturation = 0.5, Temperature = 4000 });
        Assert.Contains("setpts=PTS-STARTPTS,transpose=1,setsar=1,eq=brightness=0.1:contrast=1.2:saturation=0.5,colortemperature=temperature=4000,fade=t=in:st=0:d=0.5[v0]", Cizge(c));

        var bw = EditTimeline.FromSource(S(10));
        bw.SetEffects(new[] { 0 }, e => e.WithColor(ClipEffects.BlackWhite));
        Assert.Contains("setpts=PTS-STARTPTS,eq=saturation=0[v0]", Cizge(bw));

        var sicak = EditTimeline.FromSource(S(10));
        sicak.SetEffects(new[] { 0 }, e => e with { Temperature = 9000 });
        var sicakCizge = Cizge(sicak);
        Assert.Contains("setpts=PTS-STARTPTS,colortemperature=temperature=9000[v0]", sicakCizge);
        Assert.DoesNotContain("eq=", sicakCizge);

        Assert.Equal(string.Empty, ClipFilters.Color(ClipEffects.None));
        Assert.Equal("eq=brightness=-0.25", ClipFilters.Color(ClipEffects.None with { Brightness = -0.25 }));
    }

    [Theory]
    [InlineData(ExportMode.Fast)]
    [InlineData(ExportMode.Smart)]
    public void RenkVarsaTamaDuser(ExportMode kip)
    {
        var sinirlar = DuzenleyiciAkilliKodekTests.Sinirlar(Anahtarlar);
        var c = EditTimeline.FromSource(S(10));
        Assert.True(c.SetEffects(new[] { 0 }, e => e with { Saturation = 0 }));
        Assert.True(c.HasEffects);
        var plan = EditExport.Build(c, Bilgi(), Anahtarlar, 0, kip, "cikti.mp4", "is", 8 * Gb, sinirlar);
        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.True(plan.EffectsForcedFull);
        Assert.Contains("eq=saturation=0", Deger(plan.Steps[0].Args, "-filter_complex"));
    }

    [Fact]
    public void OnizlemeRenkZinciriParcaPenceresindeKalir()
    {
        var c = EditTimeline.FromSource(S(10));
        Assert.True(c.DeleteRange(S(2), S(4)));
        c.SetEffects(new[] { 1 }, e => e.WithColor(ClipEffects.BlackWhite) with { FadeIn = S(1), Temperature = 3000 });
        var onizleme = new EdlPreview("k.mp4", c);
        var (video, ses) = ClipFilters.Timed(onizleme);
        Assert.Equal("eq=saturation=0:enable='gte(t,2)*lt(t,8)',colortemperature=temperature=3000:enable='gte(t,2)*lt(t,8)',"
                     + "fade=t=in:st=2:d=1:enable='gte(t,2)*lt(t,3)'", video);
        Assert.Equal("afade=t=in:st=2:d=1:enable='gte(t,2)*lt(t,3)'", ses);

        c.SetEffects(new[] { 1 }, e => e with { FadeIn = 0 });
        var (yalnizRenk, yalnizSes) = ClipFilters.Timed(new EdlPreview("k.mp4", c));
        Assert.Equal("eq=saturation=0:enable='gte(t,2)*lt(t,8)',colortemperature=temperature=3000:enable='gte(t,2)*lt(t,8)'", yalnizRenk);
        Assert.Null(yalnizSes);

        var gorunum = new EditLook(new EdlPreview("k.mp4", c), 320, 240);
        Assert.Null(gorunum.Canvas);
        Assert.Equal(yalnizRenk, gorunum.TimedVideo);
    }

    [Fact]
    public void ProjeDosyasiRenkAlanlariniTasirEskiDosyaNotrAcilir()
    {
        var model = EditTimeline.FromSource(S(60));
        Assert.True(model.Split(S(30)));
        model.SetEffects(new[] { 0 }, e => e.WithColor(ClipEffects.OldFilm));
        model.SetEffects(new[] { 1 }, e => e with { Rotation = 90, Brightness = -0.2, Temperature = 9000 });
        var damga = new SourceStamp(@"C:\renk.mp4", 16, 1);
        var json = EditProject.From(damga, model).ToJson();
        var okunan = EditProject.Parse(json);
        Assert.NotNull(okunan);
        Assert.Equal(model.Clips, okunan!.Clips);

        var eski = Regex.Replace(json, "\"(brightness|contrast|saturation|temperature)\":[^,}]+,?", string.Empty);
        Assert.DoesNotContain("saturation", eski);
        var eskiOkunan = EditProject.Parse(eski);
        Assert.NotNull(eskiOkunan);
        Assert.Equal(90, eskiOkunan!.Clips[1].Effects.Rotation);
        Assert.All(eskiOkunan.Clips, k => Assert.False(k.Effects.HasColor, k.Effects.ToString()));
        Assert.True(eskiOkunan.Clips[0].Effects.IsNeutral);
    }

    private static (double R, double G, double B) OrtaRenk(string video, string kare, double saniye, int w, int h)
    {
        Ffmpeg("-hide_banner", "-nostdin", "-v", "error", "-y", "-ss", saniye.ToString(CultureInfo.InvariantCulture), "-i", video,
            "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", "-threads", "2", kare);
        var piksel = File.ReadAllBytes(kare);
        Assert.Equal(w * h * 3, piksel.Length);
        double r = 0, g = 0, b = 0;
        for (var i = 0; i < piksel.Length; i += 3)
        {
            r += piksel[i];
            g += piksel[i + 1];
            b += piksel[i + 2];
        }

        var n = w * h;
        return (r / n, g / n, b / n);
    }

    [FfmpegFact]
    public async Task CanliRenkKaresiSiyahBeyazParlaklikVeZamanPenceresi()
    {
        var klasor = Path.Combine(Kanit, "renk-canli");
        Directory.CreateDirectory(klasor);
        var adlar = new[] { "kaynak.mp4", "yalin.mp4", "siyahbeyaz.mp4", "parlak.mp4", "sicak.mp4", "pencere.mp4", "kare.rgb" };
        KanitKapanisi.Onceki(klasor, adlar);
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        Ffmpeg("-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "color=0xC03030:s=320x240:r=30:d=2",
            "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=2",
            "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", "-threads", "2", kaynak);
        var kare = Path.Combine(klasor, "kare.rgb");

        async Task<(double R, double G, double B)> Aktar(string ad, Func<ClipEffects, ClipEffects> ayar)
        {
            var c = EditTimeline.FromSource(S(2));
            c.SetEffects(new[] { 0 }, ayar);
            var cikti = Path.Combine(klasor, ad);
            var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, c, ExportMode.Fast, cikti, 8 * Gb);
            Assert.Equal(c.Clips[0].Effects.IsNeutral ? ExportMode.Fast : ExportMode.Full, plan.Effective);
            await EditExportRunner.RunAsync(IkiIsParcacigi(plan), null);
            return OrtaRenk(cikti, kare, 1, 320, 240);
        }

        var yalin = await Aktar("yalin.mp4", e => e);
        Assert.True(yalin.R - yalin.G > 80, $"kaynak kirmizi degil {yalin}");

        var bw = await Aktar("siyahbeyaz.mp4", e => e.WithColor(ClipEffects.BlackWhite));
        Assert.True(Math.Abs(bw.R - bw.G) < 6 && Math.Abs(bw.R - bw.B) < 6, $"siyah beyaz karede renk kaldi {bw}");

        var parlak = await Aktar("parlak.mp4", e => e with { Brightness = 0.3 });
        var gri = (Func<(double R, double G, double B), double>)(p => 0.299 * p.R + 0.587 * p.G + 0.114 * p.B);
        Assert.True(gri(parlak) - gri(yalin) > 40, $"parlaklik artmadi {yalin} -> {parlak}");

        var sicak = await Aktar("sicak.mp4", e => e with { Temperature = 2000 });
        Assert.True(sicak.B < yalin.B - 5, $"sicaklik maviyi dusurmedi {yalin} -> {sicak}");

        var pencere = Path.Combine(klasor, "pencere.mp4");
        var cizelge = EditTimeline.FromSource(S(2));
        Assert.True(cizelge.Split(S(1)));
        cizelge.SetEffects(new[] { 1 }, e => e.WithColor(ClipEffects.BlackWhite));
        var (zincir, _) = ClipFilters.Timed(new EdlPreview(kaynak, cizelge));
        Assert.Equal("eq=saturation=0:enable='gte(t,1)*lt(t,2)'", zincir);
        Ffmpeg("-hide_banner", "-nostdin", "-y", "-i", kaynak, "-vf", zincir!, "-an", "-c:v", "libx264", "-preset", "ultrafast",
            "-pix_fmt", "yuv420p", "-threads", "2", pencere);
        var once = OrtaRenk(pencere, kare, 0.5, 320, 240);
        var sonra = OrtaRenk(pencere, kare, 1.5, 320, 240);
        Assert.True(once.R - once.G > 80, $"pencere disinda renk gitti {once}");
        Assert.True(Math.Abs(sonra.R - sonra.G) < 6, $"pencere icinde renk kaldi {sonra}");

        KanitKapanisi.Kapat(klasor, adlar);
    }

    [FfmpegFact]
    public async Task LibmpvRenkEdlZamanindaUygulanir()
    {
        var klasor = Path.Combine(Kanit, "renk-mpv");
        Directory.CreateDirectory(klasor);
        var adlar = new[] { "kaynak.mp4" };
        KanitKapanisi.Onceki(klasor, adlar);
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        Ffmpeg("-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "color=red:s=320x240:r=30:d=3",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "15", "-threads", "2", "-pix_fmt", "yuv420p", kaynak);

        var c = EditTimeline.FromSource(S(3));
        Assert.True(c.Split(S(1.5)));
        c.SetEffects(new[] { 1 }, e => e.WithColor(ClipEffects.BlackWhite));
        var onizleme = new EdlPreview(kaynak, c);
        var gorunum = new EditLook(onizleme, 320, 240);
        Assert.Equal("eq=saturation=0:enable='gte(t,1.5)*lt(t,3)'", gorunum.TimedVideo);

        using (var motor = new MpvEngine())
        {
            motor.SetProperty("ao", "null");
            var surucu = new EdlPreviewDriver(motor, onizleme, look: gorunum);
            await surucu.OpenAsync();
            motor.Pause();

            await surucu.SeekAsync(S(0.5), SeekPrecision.Exact);
            var renkli = await KareBekleAsync(motor, 0, k => k.Parlak < 30);
            Assert.True(renkli.Parlak < 30, $"birinci parca renksiz {renkli.Parlak}");

            await surucu.SeekAsync(S(2), SeekPrecision.Exact);
            var gri = await KareBekleAsync(motor, renkli.Gorulen, k => k.Parlak > 50);
            Assert.True(gri.Parlak > 50, $"EDL 2 sn'de siyah beyaz yok {gri.Parlak}");

            var duz = EditTimeline.FromSource(S(3));
            Assert.True(duz.Split(S(1.5)));
            await surucu.RestyleAsync(new EdlPreview(kaynak, duz), null);
            await surucu.SeekAsync(S(2), SeekPrecision.Exact);
            var yalin = await KareBekleAsync(motor, gri.Gorulen, k => k.Parlak < 30);
            Assert.True(yalin.Parlak < 30, $"ayarsiz onizlemede renk geri gelmedi {yalin.Parlak}");
        }

        KanitKapanisi.Kapat(klasor, adlar);
    }

    [Fact]
    public void ArayuzRenkKutulariVeHazirGorunumModeleVeOnizlemeyeGider()
    {
        var klasor = Path.Combine(Kanit, "renk-arayuz");
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
                    var acilan = motor.Acilan;

                    view.TimelineView.SelectedIndex = 0;
                    Assert.True(view.ClipPanel.IsVisible);
                    Assert.Equal(EditorView.LookPresets.Length, view.CmbClipLook.ItemCount);
                    Assert.Equal(0, view.CmbClipLook.SelectedIndex);

                    void Yaz(TextBox kutu, string metin)
                    {
                        kutu.Text = metin;
                        kutu.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
                    }

                    Yaz(view.TxtClipSaturation, "0");
                    Assert.Equal(0, model.Clips[0].Effects.Saturation);
                    Assert.Equal(1, view.CmbClipLook.SelectedIndex);
                    DenetimSurucu.Pump(view.Player, () => view.Restyles >= 1, 10);
                    Assert.Equal("eq=saturation=0:enable='gte(t,0)*lt(t,600)'", motor.ZamanliVideo);
                    Assert.Null(motor.Geometri);
                    Assert.Equal(acilan, motor.Acilan);

                    view.CmbClipLook.SelectedIndex = 2;
                    var eski = model.Clips[0].Effects;
                    Assert.Equal(ClipEffects.None.WithColor(ClipEffects.OldFilm), eski);
                    Assert.Equal(ClipEffects.OldFilm.Temperature.ToString(CultureInfo.InvariantCulture), view.TxtClipTemperature.Text);
                    DenetimSurucu.Pump(view.Player, () => view.Restyles >= 2, 10);
                    Assert.Contains("colortemperature=temperature=" + ClipEffects.OldFilm.Temperature.ToString(CultureInfo.InvariantCulture), motor.ZamanliVideo);

                    Yaz(view.TxtClipBrightness, "0.2");
                    Assert.Equal(0.2, model.Clips[0].Effects.Brightness, 9);
                    Assert.Equal(-1, view.CmbClipLook.SelectedIndex);

                    Yaz(view.TxtClipContrast, "1.5");
                    Assert.Equal(1.5, model.Clips[0].Effects.Contrast, 9);
                    Yaz(view.TxtClipTemperature, "abc");
                    Assert.Equal(ClipEffects.OldFilm.Temperature, model.Clips[0].Effects.Temperature);
                    Assert.Equal(ClipEffects.OldFilm.Temperature.ToString(CultureInfo.InvariantCulture), view.TxtClipTemperature.Text);

                    Assert.True(view.Undo());
                    Assert.Equal(eski with { Brightness = 0.2 }, model.Clips[0].Effects);

                    view.CmbClipLook.SelectedIndex = 0;
                    Assert.False(model.Clips[0].Effects.HasColor);
                    Assert.True(model.Clips[0].Effects.IsNeutral);
                    DenetimSurucu.Pump(view.Player, () => motor.ZamanliVideo is null, 10);
                    Assert.Null(motor.ZamanliVideo);
                    Assert.Equal(acilan, motor.Acilan);
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

/// <summary>Acilis ve klip ayari cagrilarini sayan sahte motor.</summary>
internal sealed class KlipMotoru : IPlaybackEngine
{
    private readonly byte[] _piksel = Enumerable.Repeat((byte)128, 4 * 64 * 36).ToArray();

    internal int Acilan { get; private set; }

    internal string? Geometri { get; private set; }

    internal string? ZamanliVideo { get; private set; }

    internal string? ZamanliSes { get; private set; }

    public string Name => "klip-motoru";

    public bool IsOpen { get; private set; }

    public double DurationSeconds => 600;

    public bool HasAudio => false;

    public bool IsPaused { get; private set; } = true;

    public bool EndReached { get; set; }

    public double PositionSeconds { get; private set; }

    public double AudioVideoOffsetSeconds => 0;

    public long FramesRendered { get; private set; }

    public event EventHandler<PlaybackFault>? Faulted { add { } remove { } }

    public Task OpenAsync(string path, CancellationToken ct = default)
    {
        Acilan++;
        IsOpen = true;
        FramesRendered++;
        return Task.CompletedTask;
    }

    public void Play() => IsPaused = false;

    public void Pause() => IsPaused = true;

    public Task<SeekResult> SeekAsync(double seconds, SeekPrecision precision, CancellationToken ct = default)
    {
        PositionSeconds = seconds;
        EndReached = false;
        FramesRendered++;
        return Task.FromResult(new SeekResult(SeekOutcome.Shown, 1));
    }

    public bool TryCopyLatest(ref long seen, FrameCopy copy)
    {
        if (seen == FramesRendered) return false;
        seen = FramesRendered;
        var handle = GCHandle.Alloc(_piksel, GCHandleType.Pinned);
        try
        {
            copy(handle.AddrOfPinnedObject(), 64, 36, 64 * 4);
        }
        finally
        {
            handle.Free();
        }

        return true;
    }

    public void SetEditGeometry(string? chain) => Geometri = chain is null ? null : "lavfi=[" + chain + "]";

    public void SetEditTimed(string? video, string? audio)
    {
        ZamanliVideo = video;
        ZamanliSes = audio;
    }

    public void Dispose() => IsOpen = false;
}
