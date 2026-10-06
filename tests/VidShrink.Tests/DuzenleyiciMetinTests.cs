using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VidShrink.App.Editing;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Metin katmani Dalga 1: T1 modeli (ekle/tasi/kirp/sil, 10 geri 10 ileri), ASS yazicisinin
/// kurallari (kacis, <c>\move</c>, <c>\fad</c>, PlayRes, santisaniye, BOM, RTL/CJK), metinli
/// cizelgenin Tam'a dusmesi ve <c>ass=</c> yolu, canli ffmpeg yakmasi, libmpv <c>sub-add</c>
/// ve <c>sub-reload</c>, arayuzun katmani ekleyip tazeleyip kaldirmasi. Her kuralin bir
/// negatif kontrolu var. Kanit <c>.calisma/worktree-agent-aa4351fcb97125cf2/</c>, test siler.
/// </summary>
public sealed class DuzenleyiciMetinTests
{
    private const string Dal = "worktree-agent-aa4351fcb97125cf2";
    private const long Gb = 1_000_000_000;
    private const byte Parlak = 200;

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

    private static string StilSatiri(string belge, int sira) => belge.Split('\n').First(s => s.StartsWith("Style: T" + sira + ",", StringComparison.Ordinal));

    private static string OlaySatiri(string belge) => belge.Split('\n').First(s => s.StartsWith("Dialogue: ", StringComparison.Ordinal));

    [Fact]
    public void MetinEklenirTasinirKirpilirSilinir()
    {
        var cizelge = EditTimeline.FromSource(S(60));
        Assert.False(cizelge.HasText);

        var i = cizelge.AddText(new TextLayer("Merhaba", S(5), S(8)));
        Assert.Equal(0, i);
        Assert.True(cizelge.HasText);

        Assert.True(cizelge.MoveText(0, S(10)));
        Assert.Equal((S(10), S(13)), (cizelge.Texts[0].Start, cizelge.Texts[0].End));
        Assert.False(cizelge.MoveText(0, S(10)));

        Assert.True(cizelge.TrimText(0, head: true, S(11)));
        Assert.Equal((S(11), S(13)), (cizelge.Texts[0].Start, cizelge.Texts[0].End));
        Assert.True(cizelge.TrimText(0, head: false, S(15)));
        Assert.Equal((S(11), S(15)), (cizelge.Texts[0].Start, cizelge.Texts[0].End));
        Assert.Throws<ArgumentOutOfRangeException>(() => cizelge.TrimText(0, head: true, S(15)));

        var kayan = new TextLayer("k", 0, S(4))
        {
            Keyframes = new[] { new TextKeyframe(0, 0, 0.5), new TextKeyframe(S(4), 1, 0.5) }
        }.TrimmedTo(S(1), S(4));
        Assert.Equal(0, kayan.Keyframes[0].Offset);
        Assert.Equal(0.25, kayan.Keyframes[0].X, 6);
        Assert.Equal(S(3), kayan.Keyframes[^1].Offset);

        cizelge.DeleteText(0);
        Assert.False(cizelge.HasText);
        Assert.Throws<ArgumentOutOfRangeException>(() => cizelge.DeleteText(0));
    }

    private static (EditClip[] Klipler, TextLayer[] Metinler) Anlik(EditTimeline c) => (c.Clips.ToArray(), c.Texts.ToArray());

    private static bool Esit((EditClip[] Klipler, TextLayer[] Metinler) a, (EditClip[] Klipler, TextLayer[] Metinler) b)
        => a.Klipler.SequenceEqual(b.Klipler) && a.Metinler.SequenceEqual(b.Metinler);

    [Fact]
    public void OnIslemOnGeriOnIleriAnlikEsit()
    {
        var c = EditTimeline.FromSource(S(60));
        var islemler = new Func<bool>[]
        {
            () => c.AddText(new TextLayer("A", S(1), S(4))) >= 0,
            () => c.AddText(new TextLayer("B", S(5), S(8))) >= 0,
            () => c.MoveText(0, S(2)),
            () => c.TrimText(1, head: true, S(6)),
            () => c.TrimText(1, head: false, S(7)),
            () => c.UpdateText(0, c.Texts[0] with { Size = 12 }),
            () => c.UpdateText(1, c.Texts[1] with { Color = 0xFF0000 }),
            () => c.Split(S(30)),
            () => { c.DeleteText(0); return true; },
            () => c.UpdateText(0, c.Texts[0] with { Text = "bir\nİki" })
        };

        var anliklar = new List<(EditClip[], TextLayer[])> { Anlik(c) };
        foreach (var islem in islemler)
        {
            Assert.True(islem());
            var simdi = Anlik(c);
            Assert.False(Esit(anliklar[^1], simdi), $"islem {anliklar.Count} durumu degistirmedi");
            anliklar.Add(simdi);
        }

        for (var k = islemler.Length - 1; k >= 0; k--)
        {
            Assert.True(c.Undo());
            Assert.True(Esit(anliklar[k], Anlik(c)), $"geri {islemler.Length - k} anliga donmedi");
        }

        Assert.False(c.Undo());
        for (var k = 1; k <= islemler.Length; k++)
        {
            Assert.True(c.Redo());
            Assert.True(Esit(anliklar[k], Anlik(c)), $"ileri {k} anliga donmedi");
        }

        Assert.False(c.Redo());
    }

    [Fact]
    public void KacisSusluParantezTersBoluVeSatirSonu()
    {
        Assert.Equal("a\\{b\\}c\\\u2060N", AssWriter.Escape("a{b}c\\N"));
        Assert.Equal("x\\Ny\\Nz\\Nw", AssWriter.Escape("x\r\ny\nz\rw"));
        Assert.Equal("Çağrı şöyle: ığüö", AssWriter.Escape("Çağrı şöyle: ığüö"));

        var belge = AssWriter.Write(new[] { new TextLayer("{\\b1}kalin", 0, S(1)) }, 320, 240);
        var olay = OlaySatiri(belge);
        Assert.EndsWith("}\\{\\\u2060b1\\}kalin", olay);
        Assert.DoesNotContain("{\\b1}", olay);
    }

    [Fact]
    public void HareketMoveTekKareliPosYazar()
    {
        var giden = new TextLayer("m", 0, S(2))
        {
            Keyframes = new[] { new TextKeyframe(0, 0.25, 0.5), new TextKeyframe(S(2), 0.75, 0.5) }
        };
        var olay = OlaySatiri(AssWriter.Write(new[] { giden }, 320, 240));
        Assert.Contains("{\\an5\\move(80,120,240,120)}", olay);
        Assert.DoesNotContain("\\pos(", olay);

        var duran = OlaySatiri(AssWriter.Write(new[] { new TextLayer("m", 0, S(2)) }, 320, 240));
        Assert.Contains("{\\an5\\pos(160,120)}", duran);
        Assert.DoesNotContain("\\move(", duran);
    }

    [Fact]
    public void SolmaFadYazarSolmasizEtiketYok()
    {
        var solan = new TextLayer("f", 0, S(2)) { FadeIn = S(0.5), FadeOut = S(0.25) };
        Assert.Contains("\\fad(500,250)", OlaySatiri(AssWriter.Write(new[] { solan }, 320, 240)));

        var sabit = OlaySatiri(AssWriter.Write(new[] { new TextLayer("f", 0, S(2)) }, 320, 240));
        Assert.DoesNotContain("\\fad", sabit);
    }

    [Fact]
    public void PlayResKaynakBoyutudur()
    {
        var belge = AssWriter.Write(new[] { new TextLayer("p", 0, S(1)) { Size = 10 } }, 1280, 720);
        Assert.Contains("PlayResX: 1280\nPlayResY: 720\n", belge);
        Assert.DoesNotContain("PlayResX: 384", belge);
        Assert.Contains(",72,", StilSatiri(belge, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AssWriter.Write(Array.Empty<TextLayer>(), 0, 720));
    }

    [Fact]
    public void SantisaniyeYuvarlamasiYarimYukari()
    {
        Assert.Equal(2400, AssWriter.TicksPerCentisecond);
        Assert.Equal("0:00:00.00", AssWriter.Timestamp(1199));
        Assert.Equal("0:00:00.01", AssWriter.Timestamp(1200));
        Assert.Equal("0:00:00.01", AssWriter.Timestamp(3599));
        Assert.Equal("0:00:00.02", AssWriter.Timestamp(3600));
        Assert.Equal("1:00:00.00", AssWriter.Timestamp(S(3600)));
        Assert.Equal("0:00:00.00", AssWriter.Timestamp(-5));

        var olay = OlaySatiri(AssWriter.Write(new[] { new TextLayer("t", 1200, S(1)) }, 320, 240));
        Assert.StartsWith("Dialogue: 0,0:00:00.01,0:00:01.00,T0,", olay);
        var kesik = OlaySatiri(AssWriter.Write(new[] { new TextLayer("t", 1199, S(1)) }, 320, 240));
        Assert.StartsWith("Dialogue: 0,0:00:00.00,", kesik);
    }

    [Fact]
    public void DosyaBomluUtf8TurkceRtlCjk()
    {
        var belge = AssWriter.Write(new[]
        {
            new TextLayer("ğüşİ", 0, S(1)),
            new TextLayer("مرحبا بالعالم", 0, S(1)),
            new TextLayer("你好世界", 0, S(1))
        }, 320, 240);
        var bayt = AssWriter.Bytes(belge);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bayt.Take(3).ToArray());
        var govde = bayt.Skip(3).ToArray();
        Assert.Equal(belge, new UTF8Encoding(false).GetString(govde));
        var g = Encoding.UTF8.GetBytes("ğ");
        Assert.Equal(new byte[] { 0xC4, 0x9F }, g);
        Assert.True(IcerirMi(govde, g));
        Assert.False(IcerirMi(Encoding.Latin1.GetBytes("ğ"), g));

        Assert.EndsWith(",1", StilSatiri(belge, 0));
        Assert.EndsWith(",-1", StilSatiri(belge, 1));
        Assert.EndsWith(",1", StilSatiri(belge, 2));
        Assert.Contains("}你好世界", belge);
        Assert.Contains("}مرحبا بالعالم", belge);
    }

    private static bool IcerirMi(byte[] samanlik, byte[] igne)
    {
        for (var i = 0; i + igne.Length <= samanlik.Length; i++)
            if (samanlik.AsSpan(i, igne.Length).SequenceEqual(igne)) return true;
        return false;
    }

    [Theory]
    [InlineData(ExportMode.Fast)]
    [InlineData(ExportMode.Smart)]
    public void MetinVarsaTamaDuserYoksaKipKalir(ExportMode kip)
    {
        var anahtarlar = new double[] { 0, 2, 4, 6, 8 };
        var yalin = EditTimeline.FromSource(S(10));
        var sinirlar = DuzenleyiciAkilliKodekTests.Sinirlar(anahtarlar);
        var yalinPlan = EditExport.Build(yalin, Bilgi(), anahtarlar, 0, kip, "cikti.mp4", "is", 8 * Gb, sinirlar);
        Assert.Equal(kip, yalinPlan.Effective);
        Assert.False(yalinPlan.TextForcedFull);
        Assert.Null(yalinPlan.SubtitlePath);
        Assert.DoesNotContain(yalinPlan.Steps.SelectMany(s => s.Args), a => a.Contains("ass=", StringComparison.Ordinal));

        var metinli = EditTimeline.FromSource(S(10));
        metinli.AddText(new TextLayer("Merhaba", S(1), S(3)));
        var plan = EditExport.Build(metinli, Bilgi(), anahtarlar, 0, kip, "cikti.mp4", "is", 8 * Gb, sinirlar);
        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.True(plan.TextForcedFull);
        Assert.True(plan.FellBackToFull);
        var cizge = Deger(plan.Steps[0].Args, "-filter_complex");
        Assert.Contains("concat=n=1:v=1:a=1[vcat][aout];[vcat]ass=filename='", cizge);
        Assert.EndsWith("[vout]", cizge);
        Assert.Equal(Path.Combine("is", EditExport.SubtitleFileName), plan.SubtitlePath);
        Assert.Contains("PlayResX: 1920\nPlayResY: 1080\n", plan.SubtitleContent);

        var tam = EditExport.Build(metinli, Bilgi(), anahtarlar, 0, ExportMode.Full, "cikti.mp4", "is", 8 * Gb);
        Assert.False(tam.TextForcedFull);
        Assert.False(tam.FellBackToFull);
    }

    [Fact]
    public void AssSuzgeciSurucuYolunuKacirir()
    {
        Assert.Equal("ass=filename='C\\:/a/text.ass':fontsdir='C\\:/a/fonts'", EditExport.AssFilter(@"C:\a\text.ass", @"C:\a\fonts"));
        Assert.Equal("ass=filename='C\\:/a/text.ass'", EditExport.AssFilter(@"C:\a\text.ass", null));
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
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        _ = surec.StandardOutput.ReadToEndAsync();
        if (!surec.WaitForExit(60_000))
        {
            surec.Kill(true);
            Assert.Fail("ffmpeg 60 sn icinde bitmedi");
        }

        surec.WaitForExit();
        Assert.True(surec.ExitCode == 0, hata.Result);
        return hata.Result;
    }

    private static void KaraKlip(string yol)
        => Ffmpeg("-hide_banner", "-nostdin", "-y", "-f", "lavfi", "-i", "color=black:s=320x240:r=30:d=2",
            "-c:v", "libx264", "-preset", "ultrafast", "-threads", "2", "-pix_fmt", "yuv420p", yol);

    private static ExportPlan IkiIsParcacigi(ExportPlan plan)
    {
        var args = plan.Steps[0].Args.ToList();
        args.InsertRange(args.Count - 1, new[] { "-threads", "2" });
        return plan with { Steps = new[] { plan.Steps[0] with { Args = args } } };
    }

    private static int ParlakGri(string video, string kare, int x0, int y0, int x1, int y1)
    {
        Ffmpeg("-hide_banner", "-nostdin", "-v", "error", "-y", "-ss", "1", "-i", video, "-frames:v", "1",
            "-f", "rawvideo", "-pix_fmt", "gray", "-threads", "2", kare);
        var piksel = File.ReadAllBytes(kare);
        Assert.Equal(320 * 240, piksel.Length);
        var sayi = 0;
        for (var y = y0; y < y1; y++)
        for (var x = x0; x < x1; x++)
            if (piksel[y * 320 + x] > Parlak) sayi++;
        return sayi;
    }

    [FfmpegFact]
    public async Task CanliDisaAktarmaMetniKareyeYakar()
    {
        var klasor = Path.Combine(Kanit, "metin-canli");
        Directory.CreateDirectory(klasor);
        var adlar = new[] { "kaynak.mp4", "metinli.mp4", "yalin.mp4", "metinli.gray", "yalin.gray" };
        KanitKapanisi.Onceki(klasor, adlar);
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        KaraKlip(kaynak);

        var metinli = EditTimeline.FromSource(S(2));
        metinli.AddText(new TextLayer("HH", 0, S(2)) { Size = 20 });
        var plan = await EditExportRunner.PrepareAsync(kaynak, metinli, ExportMode.Fast, Path.Combine(klasor, "metinli.mp4"), 8 * Gb);
        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.True(plan.TextForcedFull);
        await EditExportRunner.RunAsync(IkiIsParcacigi(plan), null);

        var yalinPlan = await EditExportRunner.PrepareAsync(kaynak, EditTimeline.FromSource(S(2)), ExportMode.Full, Path.Combine(klasor, "yalin.mp4"), 8 * Gb);
        Assert.Null(yalinPlan.SubtitlePath);
        await EditExportRunner.RunAsync(IkiIsParcacigi(yalinPlan), null);

        var metinliParlak = ParlakGri(Path.Combine(klasor, "metinli.mp4"), Path.Combine(klasor, "metinli.gray"), 100, 90, 220, 150);
        var yalinParlak = ParlakGri(Path.Combine(klasor, "yalin.mp4"), Path.Combine(klasor, "yalin.gray"), 100, 90, 220, 150);
        Assert.True(metinliParlak > 100, $"metin bolgesinde parlak piksel {metinliParlak}");
        Assert.Equal(0, yalinParlak);
        Assert.False(Directory.Exists(plan.WorkDirectory));

        KanitKapanisi.Kapat(klasor, adlar);
    }

    private static async Task<(byte[] Piksel, int Stride, long Gorulen)> YeniKareAsync(MpvEngine motor, long gorulen)
    {
        var saat = Stopwatch.StartNew();
        byte[]? piksel = null;
        var stride = 0;
        while (piksel is null && saat.Elapsed < TimeSpan.FromSeconds(10))
        {
            motor.TryCopyLatest(ref gorulen, (p, w, h, s) =>
            {
                var b = new byte[s * h];
                Marshal.Copy(p, b, 0, b.Length);
                piksel = b;
                stride = s;
            });
            if (piksel is null) await Task.Delay(10);
        }

        Assert.True(piksel is not null, "10 sn icinde kare gelmedi; son log: " + string.Join(" | ", motor.RecentLog));
        return (piksel!, stride, gorulen);
    }

    private static (int Sol, int Sag) ParlakYarilar(byte[] piksel, int stride)
    {
        var sol = 0;
        var sag = 0;
        for (var y = 0; y < 240; y++)
        for (var x = 0; x < 320; x++)
        {
            var i = y * stride + x * 4;
            if (piksel[i] <= Parlak || piksel[i + 1] <= Parlak || piksel[i + 2] <= Parlak) continue;
            if (x < 160) sol++;
            else sag++;
        }

        return (sol, sag);
    }

    private static string Belge(double x) => AssWriter.Write(new[]
    {
        new TextLayer("HH", 0, S(2)) { Size = 20, Keyframes = new[] { new TextKeyframe(0, x, 0.5) } }
    }, 320, 240);

    [FfmpegFact]
    public async Task LibmpvSubAddIziVeKaredeMetinSubReloadYeniKonum()
    {
        var klasor = Path.Combine(Kanit, "metin-mpv");
        Directory.CreateDirectory(klasor);
        var adlar = new[] { "kaynak.mp4", "katman.ass" };
        KanitKapanisi.Onceki(klasor, adlar);
        var kaynak = Path.Combine(klasor, "kaynak.mp4");
        var katman = Path.Combine(klasor, "katman.ass");
        KaraKlip(kaynak);

        using (var motor = new MpvEngine())
        {
            motor.SetProperty("ao", "null");
            await motor.OpenAsync(kaynak);
            motor.Pause();
            await motor.SeekAsync(0.5, SeekPrecision.Exact);
            var (bos, bosStride, gorulen) = await YeniKareAsync(motor, 0);
            Assert.Equal((0, 0), ParlakYarilar(bos, bosStride));

            File.WriteAllBytes(katman, AssWriter.Bytes(Belge(0.8)));
            var iz = motor.AddOverlay(katman);
            Assert.True(iz > 0, "sub-add iz vermedi; son log: " + string.Join(" | ", motor.RecentLog));

            var bulundu = false;
            var sayi = int.Parse(motor.GetProperty("track-list/count") ?? "0");
            for (var i = 0; i < sayi; i++)
            {
                if (motor.GetProperty($"track-list/{i}/type") != "sub") continue;
                if (!string.Equals(Path.GetFileName(motor.GetProperty($"track-list/{i}/external-filename")), "katman.ass", StringComparison.OrdinalIgnoreCase)) continue;
                Assert.Equal("ass", motor.GetProperty($"track-list/{i}/codec"));
                bulundu = true;
            }

            Assert.True(bulundu, "track-list'te ass izi yok");

            await motor.SeekAsync(1.0, SeekPrecision.Exact);
            var (sagda, sagStride, gorulen2) = await YeniKareAsync(motor, gorulen);
            var (sol1, sag1) = ParlakYarilar(sagda, sagStride);
            Assert.True(sag1 > 50, $"sagda parlak {sag1}");
            Assert.Equal(0, sol1);

            File.WriteAllBytes(katman, AssWriter.Bytes(Belge(0.2)));
            Assert.True(motor.ReloadOverlay(iz));
            await motor.SeekAsync(1.5, SeekPrecision.Exact);
            var (solda, solStride, _) = await YeniKareAsync(motor, gorulen2);
            var (sol2, sag2) = ParlakYarilar(solda, solStride);
            Assert.True(sol2 > 50, $"solda parlak {sol2}");
            Assert.Equal(0, sag2);
        }

        KanitKapanisi.Kapat(klasor, adlar);
    }

    private static bool Bas(EditorView v, Key tus, KeyModifiers ek = KeyModifiers.None)
    {
        var olay = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = tus, KeyModifiers = ek };
        v.TimelineView.RaiseEvent(olay);
        return olay.Handled;
    }

    [Fact]
    public void ArayuzKatmaniEklerTazelerKaldirirGeriAlir()
    {
        var klasor = Path.Combine(Kanit, "metin-arayuz");
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, "kaynak.mp4");
        File.WriteAllBytes(dosya, new byte[16]);
        var katman = Path.Combine(klasor, "katman");
        try
        {
            AppHost.Run(() =>
            {
                var motor = new KatmanMotoru();
                var view = new EditorView
                {
                    KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1 }
                };
                view.Player.EngineFactory = () => motor;
                view.OverlayRoot = katman;
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    var acilis = view.OpenSourceAsync(dosya);
                    DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted, 10);
                    acilis.GetAwaiter().GetResult();
                    Assert.NotNull(view.Model);
                    Assert.Equal(0, motor.Eklenen);
                    Assert.Equal(0, view.OverlayWrites);
                    Assert.False(File.Exists(view.OverlayPath));

                    Assert.True(Bas(view, Key.T));
                    Assert.Single(view.Model!.Texts);
                    Assert.Equal(1, motor.Eklenen);
                    Assert.True(view.OverlayId > 0);
                    var bayt = File.ReadAllBytes(view.OverlayPath);
                    Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bayt.Take(3).ToArray());
                    Assert.Contains("PlayResX: 320\nPlayResY: 240\n", Encoding.UTF8.GetString(bayt, 3, bayt.Length - 3));

                    Assert.True(view.UpdateSelectedText(t => t with { Text = "Yeni" }));
                    Assert.Equal(1, view.OverlayReloads);
                    Assert.Equal(1, motor.Tazelenen);
                    Assert.Equal(1, motor.Eklenen);
                    Assert.Contains("}Yeni", File.ReadAllText(view.OverlayPath));

                    Assert.True(Bas(view, Key.Delete));
                    Assert.Empty(view.Model.Texts);
                    Assert.Equal(1, motor.Kaldirilan);
                    Assert.Equal(0, view.OverlayId);

                    Assert.True(Bas(view, Key.Z, KeyModifiers.Control));
                    Assert.Single(view.Model.Texts);
                    Assert.Equal(2, motor.Eklenen);

                    Assert.True(Bas(view, Key.Y, KeyModifiers.Control));
                    Assert.Empty(view.Model.Texts);
                    Assert.Equal(2, motor.Kaldirilan);

                    view.BtnAddText.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Single(view.Model.Texts);
                    Assert.Equal(3, motor.Eklenen);
                }
                finally
                {
                    pencere.Close();
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

/// <summary>Katman cagrilarini sayan sahte motor; <c>sub-add</c> artan iz numarasi verir.</summary>
internal sealed class KatmanMotoru : IPlaybackEngine
{
    private readonly byte[] _piksel = Enumerable.Repeat((byte)128, 4 * 64 * 36).ToArray();
    private long _sonIz;
    private long _etkin;

    internal int Eklenen { get; private set; }

    internal int Tazelenen { get; private set; }

    internal int Kaldirilan { get; private set; }

    internal int YaziKlasoru { get; private set; }

    public string Name => "katman-motoru";

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
        IsOpen = true;
        FramesRendered = 1;
        _etkin = 0;
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

    public long AddOverlay(string path)
    {
        Eklenen++;
        _etkin = ++_sonIz;
        return _etkin;
    }

    public bool ReloadOverlay(long id)
    {
        Tazelenen++;
        return id != 0 && id == _etkin;
    }

    public bool RemoveOverlay(long id)
    {
        Kaldirilan++;
        if (id == 0 || id != _etkin) return false;
        _etkin = 0;
        return true;
    }

    public void SetSubtitleFontsDir(string? directory) => YaziKlasoru++;

    public void Dispose() => IsOpen = false;
}
