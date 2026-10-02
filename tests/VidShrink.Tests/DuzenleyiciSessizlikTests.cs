using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Duzenleyicide otomatik sessizlik ve siyah kare kesme (<c>Core/Editing/SilenceCut</c>,
/// <c>EditTimeline.RemoveSource</c>, <c>EditorView.Sessizlik</c>). Ayristirici gercek ffmpeg
/// ciktisindan pimlenmis metinle, plan (birlestirme, en kisa sure, kenar payi) saf fonksiyon
/// olarak, her kol olumsuz kontroluyle olculur. Model kolu hizli, yavas ve ters klipte bolmeyi,
/// solmanin kenarda kalmasini ve tek geri alma adimini; arayuz kolu sahte tarayiciyla onizleme,
/// yeniden taramasiz plan tazelemesi, bayat tarama ve iptali olcer. Canli kol 6 sn 320x240
/// lavfi kaynagi <c>-threads 2</c> ile tarar, araliklar beklenenle 0,1 sn icinde. Kanit
/// <c>.calisma/worktree-agent-a776ee630e59c9b35/</c>, test siler.
/// </summary>
public sealed class DuzenleyiciSessizlikTests
{
    private const string Dal = "worktree-agent-a776ee630e59c9b35";
    private const double Pay = 0.1;

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

    /// <summary>ffmpeg 7'nin 6 sn ornek kaynakta yazdigi satirlar, arada gurultu satirlariyla.</summary>
    private const string PimliCikti = """
        Input #0, mov,mp4,m4a,3gp,3g2,mj2, from 'ornek.mp4':
          Duration: 00:00:06.02, start: 0.000000, bitrate: 108 kb/s
        [Parsed_silencedetect_0 @ 00000206645fcec0] silence_start: 1.514667
        [Parsed_silencedetect_0 @ 00000206645fcec0] silence_end: 3.008021 | silence_duration: 1.493354
        [Parsed_blackdetect_0 @ 00000206646626c0] black_start:3 black_end:4.52 black_duration:1.52
        [out#0/null @ 0000020664580a40] video:3kB audio:1kB subtitle:0kB other streams:0kB
        """;

    [Fact]
    public void AyristiriciPimliCiktidanAraliklariOkur()
    {
        var sessiz = SilenceCut.ParseSilence(PimliCikti, 6.02);
        var siyah = SilenceCut.ParseBlack(PimliCikti);

        Assert.Equal(new[] { new IdleSpan(1.514667, 3.008021) }, sessiz);
        Assert.Equal(new[] { new IdleSpan(3, 4.52) }, siyah);

        Assert.Empty(SilenceCut.ParseSilence("frame=  150 fps=0.0 q=-0.0 size=N/A time=00:00:06.00", 6));
        Assert.Empty(SilenceCut.ParseBlack("[Parsed_blackdetect_0 @ 0] black_start:4 black_end:4 black_duration:0"));
        Assert.Empty(SilenceCut.ParseSilence("[silencedetect @ 0] silence_end: 2.5 | silence_duration: 1", 6));

        var acikKalan = SilenceCut.ParseSilence("[silencedetect @ 0] silence_start: 4.25", 6);
        Assert.Equal(new[] { new IdleSpan(4.25, 6) }, acikKalan);
        Assert.Empty(SilenceCut.ParseSilence("[silencedetect @ 0] silence_start: 6.5", 6));

        var eksi = SilenceCut.ParseSilence("[silencedetect @ 0] silence_start: -0.0213\n[silencedetect @ 0] silence_end: 0.8 | silence_duration: 0.82", 6);
        Assert.Equal(new[] { new IdleSpan(0, 0.8) }, eksi);
    }

    [Fact]
    public void IlerlemeSatiriIslenenSureyiVerir()
    {
        Assert.Equal(1.5, SilenceCut.ParseProgress("out_time_us=1500000"));
        Assert.Equal(6.0, SilenceCut.ParseProgress("out_time_ms=6000000"));
        Assert.Null(SilenceCut.ParseProgress("out_time=00:00:01.500000"));
        Assert.Null(SilenceCut.ParseProgress("progress=end"));
        Assert.Null(SilenceCut.ParseProgress("total_size=N/A"));
    }

    [Fact]
    public void PlanBirlestirirEnKisayiAtarKenarPayiBirakir()
    {
        var ayar = new SilenceCutOptions { MinSeconds = 0.5, PaddingSeconds = 0.1 };

        var birlesen = SilenceCut.Plan(new[] { new IdleSpan(1, 2), new IdleSpan(1.9, 3) }, 10, ayar);
        Assert.Equal(new[] { new IdleSpan(1.1, 2.9) }, Yuvarla(birlesen));
        var ayri = SilenceCut.Plan(new[] { new IdleSpan(1, 2), new IdleSpan(2.05, 3) }, 10, ayar);
        Assert.Equal(new[] { new IdleSpan(1.1, 1.9), new IdleSpan(2.15, 2.9) }, Yuvarla(ayri));

        Assert.Empty(SilenceCut.Plan(new[] { new IdleSpan(1, 1.4) }, 10, ayar));
        Assert.Single(SilenceCut.Plan(new[] { new IdleSpan(1, 1.5) }, 10, ayar));

        Assert.Equal(new[] { new IdleSpan(0, 0.9) }, Yuvarla(SilenceCut.Plan(new[] { new IdleSpan(-0.02, 1) }, 10, ayar)));
        Assert.Equal(new[] { new IdleSpan(9.1, 10) }, Yuvarla(SilenceCut.Plan(new[] { new IdleSpan(9, 12) }, 10, ayar)));

        var genisPay = ayar with { MinSeconds = 0.1, PaddingSeconds = 0.1 };
        Assert.Empty(SilenceCut.Plan(new[] { new IdleSpan(1, 1.15) }, 10, genisPay));

        var paysiz = ayar with { PaddingSeconds = 0 };
        Assert.Equal(new[] { new IdleSpan(1, 2) }, Yuvarla(SilenceCut.Plan(new[] { new IdleSpan(1, 2) }, 10, paysiz)));

        var tersSira = SilenceCut.Plan(new[] { new IdleSpan(5, 6), new IdleSpan(1, 2) }, 10, ayar);
        Assert.Equal(new[] { 1.1, 5.1 }, tersSira.Select(s => Math.Round(s.Start, 6)));
        Assert.Empty(SilenceCut.Plan(new[] { new IdleSpan(1, 2) }, 0, ayar));
    }

    private static IdleSpan[] Yuvarla(IEnumerable<IdleSpan> araliklar)
        => araliklar.Select(s => new IdleSpan(Math.Round(s.Start, 6), Math.Round(s.End, 6))).ToArray();

    [Fact]
    public void ArgumanYalnizGerekenAkisiVeIkiIsParcacigiIster()
    {
        var onceki = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            var ses = SilenceCut.Arguments("k.mp4", new SilenceCutOptions { ThresholdDb = -35.5 }).ToList();
            Assert.Equal("silencedetect=noise=-35.5dB:d=0.1", ses[ses.IndexOf("-af") + 1]);
            Assert.Contains("-vn", ses);
            Assert.DoesNotContain("-vf", ses);
            Assert.DoesNotContain("-an", ses);
            Assert.Equal(2, ses.Count(a => a == "-threads"));
            Assert.All(ses.Select((a, i) => (a, i)).Where(p => p.a == "-threads"), p => Assert.Equal("2", ses[p.i + 1]));
            Assert.Equal("pipe:1", ses[ses.IndexOf("-progress") + 1]);
            Assert.Equal(new[] { "-f", "null", "-" }, ses.TakeLast(3));

            var siyah = SilenceCut.Arguments("k.mp4", new SilenceCutOptions { Silence = false, Black = true }).ToList();
            Assert.Equal("blackdetect=d=0.1:pic_th=0.98:pix_th=0.10", siyah[siyah.IndexOf("-vf") + 1]);
            Assert.Contains("-an", siyah);
            Assert.DoesNotContain("-af", siyah);
            Assert.DoesNotContain("-vn", siyah);

            var ikisi = SilenceCut.Arguments("k.mp4", new SilenceCutOptions { Black = true }).ToList();
            Assert.Contains("-af", ikisi);
            Assert.Contains("-vf", ikisi);
            Assert.DoesNotContain("-an", ikisi);
            Assert.DoesNotContain("-vn", ikisi);

            Assert.Throws<ArgumentOutOfRangeException>(() => SilenceCut.Arguments("k.mp4", new SilenceCutOptions { Silence = false }));
        }
        finally
        {
            CultureInfo.CurrentCulture = onceki;
        }
    }

    [Fact]
    public void AyarGecerliligiVeAyniTarama()
    {
        Assert.True(SilenceCutOptions.Default.IsValid);
        Assert.False((SilenceCutOptions.Default with { Silence = false }).IsValid);
        Assert.False((SilenceCutOptions.Default with { ThresholdDb = 0 }).IsValid);
        Assert.False((SilenceCutOptions.Default with { ThresholdDb = -120 }).IsValid);
        Assert.False((SilenceCutOptions.Default with { MinSeconds = 0 }).IsValid);
        Assert.False((SilenceCutOptions.Default with { MinSeconds = double.NaN }).IsValid);
        Assert.False((SilenceCutOptions.Default with { PaddingSeconds = -0.1 }).IsValid);
        Assert.True((SilenceCutOptions.Default with { PaddingSeconds = 0 }).IsValid);

        var temel = SilenceCutOptions.Default;
        Assert.True(temel.SameScan(temel with { MinSeconds = 2, PaddingSeconds = 0.3 }));
        Assert.False(temel.SameScan(temel with { ThresholdDb = -40 }));
        Assert.False(temel.SameScan(temel with { Black = true }));
    }

    [Fact]
    public void KaynakAraligiTekAdimdaBolunupSilinir()
    {
        var c = EditTimeline.FromSource(S(10));
        var once = c.Clips.ToArray();

        Assert.True(c.RemoveSource(new[] { (S(2), S(3)), (S(5), S(6)) }));
        Assert.Equal(new[] { (S(0), S(2)), (S(3), S(5)), (S(6), S(10)) }, c.Clips.Select(k => (k.SourceStart, k.SourceEnd)));
        Assert.Equal(S(8), c.Duration);
        var sonra = c.Clips.ToArray();

        Assert.True(c.Undo());
        Assert.Equal(once, c.Clips);
        Assert.False(c.CanUndo);
        Assert.True(c.Redo());
        Assert.Equal(sonra, c.Clips);

        Assert.False(c.RemoveSource(Array.Empty<(long, long)>()));
        Assert.False(c.RemoveSource(new[] { (S(2.2), S(2.8)) }));
        Assert.False(c.RemoveSource(new[] { (S(0), S(10)) }));
        Assert.Equal(sonra, c.Clips);

        Assert.True(c.RemoveSource(new[] { (S(2.5), S(5.5)) }));
        Assert.Equal(new[] { (S(0), S(2)), (S(6), S(10)) }, c.Clips.Select(k => (k.SourceStart, k.SourceEnd)));
        Assert.Throws<ArgumentOutOfRangeException>(() => c.RemoveSource(new[] { (S(3), S(2)) }));
    }

    [Fact]
    public void TersVeHizliKliptekiYerDogruVeSolmaKenardaKalir()
    {
        var klip = new EditClip(0, S(10), 2m, reversed: true) { Effects = ClipEffects.None with { FadeIn = S(0.5), FadeOut = S(0.5), VolumeDb = -6 } };
        var c = new EditTimeline(new[] { klip }, S(10));

        Assert.Equal(new[] { (S(3.5), S(4)) }, c.SourceToTimeline(new[] { (S(2), S(3)) }));
        Assert.Empty(c.SourceToTimeline(new[] { (S(10), S(12)) }));

        Assert.True(c.RemoveSource(new[] { (S(2), S(3)), (S(6), S(7)) }));
        Assert.Equal(new[] { (S(7), S(10)), (S(3), S(6)), (S(0), S(2)) }, c.Clips.Select(k => (k.SourceStart, k.SourceEnd)));
        Assert.All(c.Clips, k => Assert.True(k.Reversed && k.Speed == 2m && k.Effects.VolumeDb == -6));
        Assert.Equal(new[] { S(0.5), 0, 0 }, c.Clips.Select(k => k.Effects.FadeIn));
        Assert.Equal(new[] { 0, 0, S(0.5) }, c.Clips.Select(k => k.Effects.FadeOut));
        Assert.Equal(S(4), c.Duration);

        Assert.Empty(c.SourceToTimeline(new[] { (S(2), S(3)) }));
        Assert.Equal(new[] { (S(2.5), S(3.5)) }, c.SourceToTimeline(new[] { (S(1), S(4)) }));
    }

    [Fact]
    public void ArayuzOnizlerTaramasizTazelerVeTekAdimdaKeser()
    {
        var klasor = Path.Combine(Kanit, "sessizlik-arayuz");
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
                var taranan = 0;
                SilenceCutOptions? son = null;
                view.SilenceScanner = (_, ayar, sure, ilerleme, _) =>
                {
                    taranan++;
                    son = ayar;
                    ilerleme.Report(1);
                    return Task.FromResult(new SilenceScan(new[] { new IdleSpan(10, 20), new IdleSpan(100, 100.3) }, Array.Empty<IdleSpan>(), ayar));
                };
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    var acilis = view.OpenSourceAsync(dosya);
                    DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted, 10);
                    acilis.GetAwaiter().GetResult();
                    var model = view.Model!;

                    Assert.False(view.ApplySilence());
                    view.BtnSilence.IsChecked = true;
                    view.BtnSilence.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.True(view.SilencePanel.IsVisible);
                    Assert.False(view.BtnSilenceApply.IsEnabled);

                    var tarama = view.ScanSilenceAsync();
                    DenetimSurucu.Pump(view.Player, () => tarama.IsCompleted, 10);
                    Assert.True(tarama.Result);
                    Assert.Equal(1, taranan);
                    Assert.Equal(SilenceCutOptions.DefaultThresholdDb, son!.ThresholdDb);
                    Assert.Equal(new[] { (S(10.1), S(19.9)) }, view.TimelineView.CutPreview);
                    Assert.True(view.BtnSilenceApply.IsEnabled);
                    Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.silence.found"), 1, Saat.Kesit(TimeSpan.FromSeconds(9.8), Strings.Culture)), view.TxtSilenceStatus.Text);

                    Yaz(view.TxtSilenceMin, "0.2");
                    Assert.Equal(2, view.TimelineView.CutPreview.Count);
                    Assert.Equal(1, taranan);
                    Yaz(view.TxtSilenceMin, "0.5");
                    Assert.Single(view.TimelineView.CutPreview);

                    Yaz(view.TxtSilenceThreshold, "-40");
                    Assert.Empty(view.TimelineView.CutPreview);
                    Assert.False(view.BtnSilenceApply.IsEnabled);
                    Assert.Equal(Strings.Get("editor.silence.stale"), view.TxtSilenceStatus.Text);
                    Yaz(view.TxtSilenceThreshold, "-35");
                    Assert.Single(view.TimelineView.CutPreview);

                    Yaz(view.TxtSilencePadding, "abc");
                    Assert.Empty(view.TimelineView.CutPreview);
                    Assert.False(view.BtnSilenceScan.IsEnabled);
                    Yaz(view.TxtSilencePadding, "0.1");

                    view.BtnSilenceApply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(new[] { (S(0), S(10.1)), (S(19.9), S(600)) }, model.Clips.Select(k => (k.SourceStart, k.SourceEnd)));
                    Assert.Empty(view.TimelineView.CutPreview);
                    Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.silence.cut"), 1), view.TxtSilenceStatus.Text);

                    Assert.True(view.Undo());
                    Assert.Single(model.Clips);
                    Assert.Single(view.TimelineView.CutPreview);
                    Assert.False(view.Undo());

                    view.ShowSilencePanel(false);
                    Assert.Empty(view.TimelineView.CutPreview);

                    view.SilenceScanner = async (_, _, _, _, ct) =>
                    {
                        await Task.Delay(Timeout.Infinite, ct);
                        throw new InvalidOperationException();
                    };
                    view.ShowSilencePanel(true);
                    var bekleyen = view.ScanSilenceAsync();
                    Assert.True(view.SilenceScanning);
                    Assert.True(view.BtnSilenceCancel.IsVisible);
                    view.BtnSilenceCancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    DenetimSurucu.Pump(view.Player, () => bekleyen.IsCompleted, 10);
                    Assert.False(bekleyen.Result);
                    Assert.False(view.SilenceScanning);
                    Assert.False(view.BtnSilenceCancel.IsVisible);
                    Assert.Empty(view.TimelineView.CutPreview);

                    view.SilenceScanner = (_, _, _, _, _) => Task.FromException<SilenceScan>(new InvalidOperationException("bozuk"));
                    var bozuk = view.ScanSilenceAsync();
                    DenetimSurucu.Pump(view.Player, () => bozuk.IsCompleted, 10);
                    Assert.False(bozuk.Result);
                    Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.silence.failed"), "bozuk"), view.TxtSilenceStatus.Text);
                }
                finally
                {
                    pencere.Close();
                }
            });
        }
        finally
        {
            try { Directory.Delete(klasor, true); } catch (IOException) { }
            KanitKapanisi.Kapat(Kanit);
        }
    }

    private static void Yaz(TextBox kutu, string metin)
    {
        kutu.Text = metin;
        Dispatcher.UIThread.RunJobs();
    }

    private static void Ffmpeg(params string[] args)
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
    }

    /// <summary>6 sn 320x240: 1,5-3 sn ses sifir, 3-4,5 sn goruntu siyah; geri kalan gri ve 440 Hz.</summary>
    private static string Ornek(string klasor, bool sesli = true)
    {
        var yol = Path.Combine(klasor, sesli ? "ornek.mp4" : "sessiz-iz.mp4");
        var args = new List<string>
        {
            "-hide_banner", "-nostdin", "-y", "-v", "error",
            "-f", "lavfi", "-i", "color=c=gray:s=320x240:r=25:d=6,drawbox=x=0:y=0:w=iw:h=ih:c=black:t=fill:enable='between(t,3,4.5)'"
        };
        if (sesli) args.AddRange(new[] { "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=6,volume=volume=0:enable='between(t,1.5,3)'", "-c:a", "aac" });
        args.AddRange(new[] { "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-threads", "2", "-shortest", yol });
        Ffmpeg(args.ToArray());
        return yol;
    }

    [FfmpegFact]
    public async Task CanliTaramaBeklenenAraliklariBulur()
    {
        var klasor = Path.Combine(Kanit, "sessizlik-canli");
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = Ornek(klasor);
            var ilerleme = new List<double>();
            ProcessPriorityClass? oncelik = null;
            var saat = Stopwatch.StartNew();
            var tarama = await SilenceCut.ScanAsync(ToolLocator.Ffmpeg, kaynak, new SilenceCutOptions { Black = true }, 6,
                new AnindaIlerleme(ilerleme), CancellationToken.None,
                p => { if (OperatingSystem.IsWindows()) oncelik = p.PriorityClass; });
            saat.Stop();

            var sessiz = Assert.Single(tarama.Silence);
            Assert.InRange(sessiz.Start, 1.5 - Pay, 1.5 + Pay);
            Assert.InRange(sessiz.End, 3.0 - Pay, 3.0 + Pay);
            var siyah = Assert.Single(tarama.Black);
            Assert.InRange(siyah.Start, 3.0 - Pay, 3.0 + Pay);
            Assert.InRange(siyah.End, 4.5 - Pay, 4.5 + Pay);
            Assert.Equal(1, ilerleme[^1]);
            Assert.True(ilerleme.Zip(ilerleme.Skip(1)).All(p => p.Second >= p.First), string.Join(" ", ilerleme));
            if (OperatingSystem.IsWindows()) Assert.Equal(ProcessPriorityClass.BelowNormal, oncelik);

            var plan = SilenceCut.Plan(tarama.All, 6, new SilenceCutOptions { Black = true });
            var kesim = Assert.Single(plan);
            Assert.InRange(kesim.Start, 1.6 - Pay, 1.6 + Pay);
            Assert.InRange(kesim.End, 4.4 - Pay, 4.4 + Pay);
            var cizelge = EditTimeline.FromSource(S(6));
            Assert.True(cizelge.RemoveSource(SilenceCut.ToTicks(plan)));
            Assert.Equal(2, cizelge.Clips.Count);
            Assert.InRange(EditTime.ToSeconds(cizelge.Duration), 3.2 - Pay, 3.2 + Pay);

            var yalnizSes = await SilenceCut.ScanAsync(ToolLocator.Ffmpeg, kaynak, SilenceCutOptions.Default, 6);
            Assert.Single(yalnizSes.Silence);
            Assert.Empty(yalnizSes.Black);

            var yalnizSiyah = await SilenceCut.ScanAsync(ToolLocator.Ffmpeg, kaynak, new SilenceCutOptions { Silence = false, Black = true }, 6);
            Assert.Empty(yalnizSiyah.Silence);
            Assert.Single(yalnizSiyah.Black);

            var sessizIz = Ornek(klasor, sesli: false);
            var izsiz = await SilenceCut.ScanAsync(ToolLocator.Ffmpeg, sessizIz, SilenceCutOptions.Default, 6);
            Assert.Empty(izsiz.Silence);
            Assert.Empty(izsiz.Black);

            File.WriteAllText(Path.Combine(Kanit, "sessizlik-canli.txt"),
                FormattableString.Invariant($"sessiz {sessiz.Start:0.000}-{sessiz.End:0.000}\nsiyah {siyah.Start:0.000}-{siyah.End:0.000}\nkesim {kesim.Start:0.000}-{kesim.End:0.000}\nsure {EditTime.ToSeconds(cizelge.Duration):0.000}\ntarama {saat.ElapsedMilliseconds} ms\nilerleme {ilerleme.Count}\n"));
            KanitKapanisi.Kapat(Kanit, "sessizlik-canli.txt");
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    [FfmpegFact]
    public async Task CanliTaramaIptalSureciOldurur()
    {
        var klasor = Path.Combine(Kanit, "sessizlik-iptal");
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = Ornek(klasor);
            using var cts = new CancellationTokenSource();
            Process? surec = null;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SilenceCut.ScanAsync(ToolLocator.Ffmpeg, kaynak, new SilenceCutOptions { Black = true }, 6,
                null, cts.Token, p => { surec = p; cts.Cancel(); }));
            Assert.NotNull(surec);
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
            KanitKapanisi.Kapat(Kanit);
        }
    }

    private sealed class AnindaIlerleme : IProgress<double>
    {
        private readonly List<double> _degerler;

        public AnindaIlerleme(List<double> degerler) => _degerler = degerler;

        public void Report(double value)
        {
            lock (_degerler) _degerler.Add(value);
        }
    }
}
