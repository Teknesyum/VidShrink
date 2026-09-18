using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using VidShrink.App.Recorder;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;
using static VidShrink.Tests.KaydediciAyarTests;

namespace VidShrink.Tests;

public sealed class KaydediciHedefTests
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

    private static RecorderRequest Istek() => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Region,
        Region = new RecorderRegion(0, 0, 640, 480),
        Fps = 15,
        Preset = "ultrafast"
    };

    [Fact]
    public void BoyutSiniriFsArgumaninaGecerVeSifirReddedilir()
    {
        var sinirli = RecorderArguments.Build(Istek() with { MaxMegabytes = 5 }, "a.mp4");
        var sinirsiz = RecorderArguments.Build(Istek(), "a.mp4");

        Assert.Equal("5242880", Deger(sinirli, "-fs"));
        Assert.Null(Deger(sinirsiz, "-fs"));
        Assert.Contains(RecorderArguments.Validate(Istek() with { MaxMegabytes = 0 }, "a.mp4"), e => e.Contains("size limit"));
        Assert.Contains(RecorderArguments.Validate(Istek() with { MaxMegabytes = 2, Container = RecorderContainer.Gif }, "a.gif"), e => e.Contains("GIF"));
    }

    [Fact]
    public void BoyutSiniriParcalaraKalanlaBolunur()
    {
        var istek = Istek() with { MaxMegabytes = 5 };

        Assert.Same(istek, RecorderArguments.ForSegment(istek, TimeSpan.Zero));
        Assert.Equal(2, RecorderArguments.ForSegment(istek, TimeSpan.FromSeconds(3), 3)!.MaxMegabytes);
        Assert.Null(RecorderArguments.ForSegment(istek, TimeSpan.FromSeconds(3), 5));
        Assert.Null(RecorderArguments.ForSegment(Istek(), TimeSpan.FromSeconds(3), 3)!.MaxMegabytes);
    }

    [Fact]
    public void Mp4BoyutSiniriMatroskaYakalamayaDuser()
    {
        Assert.True(RecorderArguments.SizeNeedsMatroska(Istek() with { MaxMegabytes = 5 }));
        Assert.True(RecorderArguments.SizeNeedsMatroska(Istek() with { Container = RecorderContainer.Mov, Split = new RecorderSplit(null, 5) }));
        Assert.False(RecorderArguments.SizeNeedsMatroska(Istek() with { Container = RecorderContainer.Mkv, MaxMegabytes = 5 }));
        Assert.False(RecorderArguments.SizeNeedsMatroska(Istek()));
        Assert.False(RecorderArguments.SizeNeedsMatroska(Istek() with { Split = new RecorderSplit(TimeSpan.FromSeconds(5), null) }));

        var yakalama = RecorderArguments.SizeCapturePath(Path.Combine("k", "kayit_1.mp4"));
        Assert.Equal(Path.Combine("k", "kayit_1.boyut.mkv"), yakalama);
        Assert.Equal(Path.Combine("k", "kayit_1.mp4"), RecorderArguments.SizeDeliveryPath(yakalama, ".mp4"));
        Assert.Equal(Path.Combine("k", "kayit_1.bolum2.mov"), RecorderArguments.SizeDeliveryPath(Path.Combine("k", "kayit_1.boyut.bolum2.mkv"), ".mov"));
    }

    [Fact]
    public void TekHedefKutusuKendiSinirinaGecer()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            (RecorderRequest Istek, string Not) Kur(string saniye, string mb)
            {
                var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true, AutoChoice = RecorderAutoPlan.Candidates(new RecorderMachine(1920, 1080, 60, 8, []))[0] };
                Yaz(view, "TxtTargetSeconds", saniye);
                Yaz(view, "TxtTargetMegabytes", mb);
                return (view.BuildRequest()!, Bul<TextBlock>(view, "TxtBudgetNote").Text ?? string.Empty);
            }

            return (sure: Kur("12", ""), boyut: Kur("", "7"), bos: Kur("", ""), ikisi: Kur("30", "10"));
        }));

        File.WriteAllLines(Path.Combine(Kanit, "tek-hedef.txt"), new[]
        {
            $"sure: t={olcu.sure.Istek.MaxDuration} fs={olcu.sure.Istek.MaxMegabytes} not={olcu.sure.Not}",
            $"boyut: t={olcu.boyut.Istek.MaxDuration} fs={olcu.boyut.Istek.MaxMegabytes} not={olcu.boyut.Not}",
            $"bos: t={olcu.bos.Istek.MaxDuration} fs={olcu.bos.Istek.MaxMegabytes} not={olcu.bos.Not}",
            $"ikisi: t={olcu.ikisi.Istek.MaxDuration} fs={olcu.ikisi.Istek.MaxMegabytes} b={olcu.ikisi.Istek.BitrateKbps}"
        });

        Assert.Equal((TimeSpan.FromSeconds(12), (double?)null), (olcu.sure.Istek.MaxDuration, olcu.sure.Istek.MaxMegabytes));
        Assert.Equal(((TimeSpan?)null, (double?)7), (olcu.boyut.Istek.MaxDuration, olcu.boyut.Istek.MaxMegabytes));
        Assert.Equal(((TimeSpan?)null, (double?)null, string.Empty), (olcu.bos.Istek.MaxDuration, olcu.bos.Istek.MaxMegabytes, olcu.bos.Not));
        Assert.Contains("12", olcu.sure.Not);
        Assert.Contains("7", olcu.boyut.Not);
        Assert.Equal((TimeSpan.FromSeconds(30), (double?)10), (olcu.ikisi.Istek.MaxDuration, olcu.ikisi.Istek.MaxMegabytes));
        Assert.NotNull(olcu.ikisi.Istek.BitrateKbps);
        Assert.Equal("5242880", Deger(RecorderArguments.Build(olcu.boyut.Istek with { MaxMegabytes = 5 }, "a.mkv"), "-fs"));
    }

    [Fact]
    public void KayitBitinceKlasorYalnizKutuAcikkenAcilir()
    {
        var dosya = Path.Combine(Kanit, "klasor-ac.mkv");
        File.WriteAllBytes(dosya, new byte[] { 1 });
        try
        {
            var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
            {
                var acilan = new List<string>();
                var kapali = new RecorderView(ayarYolu) { RevealFolder = acilan.Add };
                kapali.Deliver(new RecordResult(true, dosya, 1, false, 0, string.Empty, 1));
                var kapaliSayi = acilan.Count;

                var once = new RecorderView(ayarYolu);
                Bul<CheckBox>(once, "ChkOpenFolder").IsChecked = true;
                var dosyada = JsonNode.Parse(File.ReadAllText(ayarYolu))?["openFolderWhenDone"]?.ToJsonString();

                var acik = new RecorderView(ayarYolu) { RevealFolder = acilan.Add };
                acik.Deliver(new RecordResult(true, dosya, 1, false, 0, string.Empty, 1));
                return (kapaliSayi, dosyada, acilan: acilan.ToArray(), kutu: Bul<CheckBox>(acik, "ChkOpenFolder").IsChecked);
            }));

            Assert.Equal(0, olcu.kapaliSayi);
            Assert.Equal("true", olcu.dosyada);
            Assert.True(olcu.kutu);
            Assert.Equal(new[] { dosya }, olcu.acilan);
        }
        finally
        {
            File.Delete(dosya);
        }
    }

    [Fact]
    public void IptalKisayoluOturumYokkenIslemezVeTanimdaVar()
    {
        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() => new RecorderView(ayarYolu).RunHotkeyAsync(HotkeyAction.Discard).GetAwaiter().GetResult()));

        Assert.False(olcu);
        Assert.Equal(HotkeyAction.Discard, RecorderHotkeys.ActionOf(Avalonia.Input.Key.F10, Avalonia.Input.KeyModifiers.None));
        Assert.Equal(5, RecorderHotkeys.All.Select(b => b.VirtualKey).Distinct().Count());
    }

    private static void Pompala(Task gorev, int sinirMs)
    {
        var saat = System.Diagnostics.Stopwatch.StartNew();
        while (!gorev.IsCompleted && saat.ElapsedMilliseconds < sinirMs)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }

        Assert.True(gorev.IsCompleted, "gorev zamaninda bitmedi");
        gorev.GetAwaiter().GetResult();
    }

    [KayitFact]
    public void IptalKaydiDurdururVeDosyayiSiler()
    {
        var klasor = Path.Combine(Kanit, "iptal");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);

        var olcu = AyarDosyasiyla(ayarYolu => AppHost.Run(() =>
        {
            var view = new RecorderView(ayarYolu) { SkipAutoMeasure = true };
            Elle(view);
            Bul<ComboBox>(view, "CmbTarget").SelectedIndex = (int)RecorderTargetKind.Region;
            Yaz(view, "TxtRegionX", "0");
            Yaz(view, "TxtRegionY", "0");
            Yaz(view, "TxtRegionWidth", "320");
            Yaz(view, "TxtRegionHeight", "240");
            Sec(view, "CmbCodec", "libx264");
            Sec(view, "CmbPreset", "ultrafast");
            Yaz(view, "TxtOutputFolder", klasor);

            Pompala(view.StartAsync(), 15000);
            var basladi = view.HasSession;
            var yazilan = Directory.Exists(klasor) ? Directory.GetFiles(klasor).Length : 0;
            Pompala(Task.Delay(1500), 3000);
            var iptal = view.RunHotkeyAsync(HotkeyAction.Discard);
            Pompala(iptal, 15000);
            return (basladi, hata: view.ErrorText, iptal: iptal.Result, oturum: view.HasSession, not: view.NoticeText,
                kalan: Directory.Exists(klasor) ? Directory.GetFiles(klasor, "*", SearchOption.AllDirectories) : Array.Empty<string>());
        }));

        File.WriteAllLines(Path.Combine(Kanit, "iptal.txt"), new[] { $"basladi={olcu.basladi} iptal={olcu.iptal} oturum={olcu.oturum} not={olcu.not} hata={olcu.hata} kalan={olcu.kalan.Length}" });
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);

        Assert.True(olcu.basladi, olcu.hata);
        Assert.True(olcu.iptal);
        Assert.False(olcu.oturum);
        Assert.Empty(olcu.kalan);
        Assert.NotEmpty(olcu.not);
    }

    [KayitFact]
    public async Task BoyutSiniriDolunacaKayitKendiBiter()
    {
        var cikti = Path.Combine(Kanit, "boyut-siniri.mp4");
        var istek = Istek() with { Quality = 0, KeyframeSeconds = 1, MaxMegabytes = 0.05 };

        var oturum = await RecorderSession.StartAsync(istek, cikti);
        var saat = System.Diagnostics.Stopwatch.StartNew();
        var bitti = await Task.WhenAny(oturum.Ended, Task.Delay(8000)) == oturum.Ended;
        var sure = saat.Elapsed.TotalSeconds;
        var sonuc = await oturum.StopAsync();
        var bayt = File.Exists(cikti) ? new FileInfo(cikti).Length : 0;
        var yakalama = RecorderArguments.SizeCapturePath(cikti);
        var (kod, metin) = KayitKanit.Ffprobe(cikti, "boyut-siniri.ffprobe.txt");
        var baslik = File.Exists(cikti) ? System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(cikti), 4, 4) : string.Empty;
        var (paketBayt, sonKume) = Paketler(cikti);
        var sinir = (long)(0.05 * 1024 * 1024);

        File.WriteAllLines(Path.Combine(Kanit, "boyut-siniri.txt"), new[] { $"ended={bitti} sn={sure:0.00} ok={sonuc.Ok} teslim={sonuc.OutputPath} bayt={bayt} paket={paketBayt} sonIkiAnahtarKumesi={sonKume} sinir={sinir} yakalamaKaldi={File.Exists(yakalama)} probe={kod}" });
        if (File.Exists(cikti)) File.Delete(cikti);

        Assert.True(bitti, "0,05 MB siniri dolunca oturum kendiliginden bitmeli");
        Assert.True(sonuc.Ok, sonuc.StandardError);
        Assert.Equal(cikti, sonuc.OutputPath);
        Assert.False(File.Exists(yakalama));
        Assert.Equal(0, kod);
        Assert.Equal("ftyp", baslik);
        Assert.Contains("codec_type=video", metin);
        Assert.InRange(sonKume, 1, paketBayt);
        Assert.InRange(paketBayt - sonKume, 0, sinir);
    }

    private static (long Toplam, long SonKume) Paketler(string dosya)
    {
        if (!File.Exists(dosya)) return (0, 0);
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = ToolLocator.Ffprobe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[] { "-hide_banner", "-v", "error", "-select_streams", "v:0", "-show_entries", "packet=size,flags", "-of", "csv=p=0", dosya })
            psi.ArgumentList.Add(arg);
        using var surec = System.Diagnostics.Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        var satirlar = surec.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        surec.WaitForExit(20_000);
        _ = hata.Result;

        var kumeler = new List<long>();
        foreach (var satir in satirlar)
        {
            var parca = satir.Split(',');
            if (!long.TryParse(parca[0], out var boy)) continue;
            if (kumeler.Count == 0 || (parca.Length > 1 && parca[1].StartsWith('K'))) kumeler.Add(0);
            kumeler[^1] += boy;
        }
        return (kumeler.Sum(), kumeler.TakeLast(2).Sum());
    }
}
