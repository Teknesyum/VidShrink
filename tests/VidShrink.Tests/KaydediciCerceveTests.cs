using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

public sealed class KaydediciCerceveTests
{
    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "paket-2");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static readonly PixelRect Ekran = new(0, 0, 1024, 768);

    private static RecorderRequest Istek(RecorderTargetKind hedef) => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = hedef,
        Fps = 15,
        Preset = "ultrafast"
    };

    [Fact]
    public void CerceveEkranaSigmazsaBolgeninIcineCizilir()
    {
        var ekranlar = new[] { Ekran, new PixelRect(1024, 0, 1280, 1024) };

        Assert.Equal(new PixelRect(98, 98, 644, 364), RecorderFrame.Placement(new PixelRect(100, 100, 640, 360), 2, ekranlar));
        Assert.Equal(Ekran, RecorderFrame.Placement(Ekran, 2, ekranlar));
        Assert.Equal(new PixelRect(1024, 0, 1280, 1024), RecorderFrame.Placement(new PixelRect(1024, 0, 1280, 1024), 3, ekranlar));
        Assert.Equal(new PixelRect(0, 10, 200, 200), RecorderFrame.Placement(new PixelRect(0, 10, 200, 200), 2, ekranlar));
    }

    [Fact]
    public void CerceveUcHedefteDeBolgeBulur()
    {
        var olcu = AppHost.Run(() =>
        {
            var sorulan = new List<string>();
            var view = new RecorderView
            {
                SkipAutoMeasure = true,
                WindowRect = baslik =>
                {
                    sorulan.Add(baslik);
                    return baslik == "Hesap Makinesi" ? new PixelRect(200, 150, 320, 480) : null;
                }
            };
            var ekranlar = new[] { new ScreenBounds(0, 0, 0, 1024, 768), new ScreenBounds(1, 1024, 0, 1281, 1025) };
            return (
                ekran0: view.RegionOf(Istek(RecorderTargetKind.Screen) with { Screens = ekranlar, ScreenIndex = 0 }),
                ekran1: view.RegionOf(Istek(RecorderTargetKind.Screen) with { Screens = ekranlar, ScreenIndex = 1 }),
                yokEkran: view.RegionOf(Istek(RecorderTargetKind.Screen) with { Screens = ekranlar, ScreenIndex = 5 }),
                pencere: view.RegionOf(Istek(RecorderTargetKind.Window) with { WindowTitle = "Hesap Makinesi" }),
                kapaliPencere: view.RegionOf(Istek(RecorderTargetKind.Window) with { WindowTitle = "Kapali" }),
                bosBaslik: view.RegionOf(Istek(RecorderTargetKind.Window) with { WindowTitle = " " }),
                bolge: view.RegionOf(Istek(RecorderTargetKind.Region) with { Region = new RecorderRegion(10, 20, 320, 240) }),
                sorulan);
        });

        Assert.Equal(new PixelRect(0, 0, 1024, 768), olcu.ekran0);
        Assert.Equal(new PixelRect(1024, 0, 1280, 1024), olcu.ekran1);
        Assert.Null(olcu.yokEkran);
        Assert.Equal(new PixelRect(200, 150, 320, 480), olcu.pencere);
        Assert.Null(olcu.kapaliPencere);
        Assert.Null(olcu.bosBaslik);
        Assert.Equal(new PixelRect(10, 20, 320, 240), olcu.bolge);
        Assert.Equal(new[] { "Hesap Makinesi", "Kapali" }, olcu.sorulan);
    }

    [Fact]
    public void GercekPencereninIstemciAlaniOkunur()
    {
        var olcu = AppHost.Run(() =>
        {
            var pencere = new Window { Title = "VidShrink cerceve olcusu " + Environment.ProcessId, Width = 300, Height = 200, Position = new PixelPoint(120, 90), ShowActivated = false };
            pencere.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var alan = RecorderFrame.WindowBounds(pencere.Title!);
            var olcek = pencere.RenderScaling;
            pencere.Close();
            return (alan, olcek, yok: RecorderFrame.WindowBounds("VidShrink olmayan pencere " + Guid.NewGuid()));
        });

        Assert.NotNull(olcu.alan);
        Assert.Equal((int)Math.Round(300 * olcu.olcek), olcu.alan!.Value.Width);
        Assert.Equal((int)Math.Round(200 * olcu.olcek), olcu.alan.Value.Height);
        Assert.Null(olcu.yok);
    }

    private static byte[] Yakala(int x, int y, int genislik, int yukseklik)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in new[]
                 {
                     "-hide_banner", "-loglevel", "error", "-f", "gdigrab", "-framerate", "5",
                     "-offset_x", x.ToString(), "-offset_y", y.ToString(), "-video_size", $"{genislik}x{yukseklik}",
                     "-i", "desktop", "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", "-"
                 })
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var hata = p.StandardError.ReadToEndAsync();
        using var bellek = new MemoryStream();
        p.StandardOutput.BaseStream.CopyTo(bellek);
        p.WaitForExit(5000);
        hata.Wait(1000);
        return bellek.ToArray();
    }

    private static bool Ayni(byte[] piksel, Color renk)
        => piksel.Length >= 3 && Math.Abs(piksel[0] - renk.R) <= 12 && Math.Abs(piksel[1] - renk.G) <= 12 && Math.Abs(piksel[2] - renk.B) <= 12;

    private static void Bekle(int ms)
    {
        var saat = Stopwatch.StartNew();
        while (saat.ElapsedMilliseconds < ms)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
    }

    [KayitFact]
    public void TamEkranCercevesiKaydaGirmez()
    {
        var olcu = AppHost.Run(() =>
        {
            var ornek = new RecorderFrame();
            var renk = ((ISolidColorBrush)ornek.FindControl<Border>("FrameEdge")!.BorderBrush!).Color;
            var ekran = ornek.Screens.Primary!.Bounds;
            var y = ekran.Height / 2;
            var once = Yakala(0, y, 16, 1);

            string Dene(bool haric, out bool ayarlandi, out byte[] piksel)
            {
                var cerceve = new RecorderFrame { ExcludeFromCapture = haric };
                cerceve.Place(ekran);
                cerceve.Show();
                Bekle(1500);
                piksel = Yakala(0, y, 16, 1);
                ayarlandi = cerceve.CaptureExcluded;
                var yer = $"konum={cerceve.Position} genislik={cerceve.Width}";
                cerceve.Close();
                Bekle(200);
                return yer;
            }

            var haricYer = Dene(true, out var haricAyar, out var haricPiksel);
            var kontrolYer = Dene(false, out var kontrolAyar, out var kontrolPiksel);
            return (renk, ekran, once, haricPiksel, haricAyar, haricYer, kontrolPiksel, kontrolAyar, kontrolYer);
        });

        string Hex(byte[] b) => b.Length >= 3 ? string.Join(",", System.Linq.Enumerable.Range(0, b.Length / 3).Select(i => $"{b[3*i]:X2}{b[3*i+1]:X2}{b[3*i+2]:X2}")) : $"bos({b.Length})";
        File.WriteAllLines(Path.Combine(Kanit, "cerceve-kayit-disi.txt"), new[]
        {
            $"ekran={olcu.ekran} cerceveRengi=#{olcu.renk.R:X2}{olcu.renk.G:X2}{olcu.renk.B:X2} oncesi={Hex(olcu.once)}",
            $"affinity=0x11 ayarlandi={olcu.haricAyar} {olcu.haricYer} yakalanan={Hex(olcu.haricPiksel)}",
            $"negatif kontrol affinity yok ayarlandi={olcu.kontrolAyar} {olcu.kontrolYer} yakalanan={Hex(olcu.kontrolPiksel)}",
            Array.Exists(olcu.once, b => b != 0) ? "piksel karsilastirmasi olculdu" : "masaustu yakalamasi siyah: piksel karsilastirmasi OLCULEMEDI, yalniz affinity cagrisinin sonucu olculdu"
        });

        Assert.True(olcu.haricAyar);
        Assert.False(olcu.kontrolAyar);
        Assert.False(Ayni(olcu.haricPiksel, olcu.renk));
        if (Array.Exists(olcu.once, b => b != 0)) Assert.True(Ayni(olcu.kontrolPiksel, olcu.renk));
    }
}
