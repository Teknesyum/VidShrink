using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Duzenleyicide sahne degisimlerinden bolme (<c>Core/Editing/SceneSplit</c>,
/// <c>EditTimeline.SplitSource</c>, <c>SceneDetector.CutsAsync</c>, <c>EditorView.Sahne</c>).
/// Saf hesap parca disi, kenara yakin ve birbirine yakin siniri olumsuz kontroluyle olcer;
/// model kolu parcanin silinmedigini, surenin degismedigini ve tek geri alma adimini; arayuz
/// kolu sahte tarayiciyla bolmeyi, bos sonucu, iptali ve hatayi olcer. Canli kol 2 sn kirmizi
/// + 2 sn mavi 320x240 lavfi kaynagi <c>-threads 2</c> ile tarar, sinir 2 sn'nin 0,1 sn
/// icinde. Kanit <c>.calisma/worktree-agent-aa2ae8c406440094f/</c>, test siler.
/// </summary>
public sealed class DuzenleyiciSahneBolmeTests
{
    private const string Dal = "worktree-agent-aa2ae8c406440094f";
    private const double Pay = 0.1;

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static long Min => SceneSplit.MinPieceTicks;

    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", Dal);
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static (long, long)[] Araliklar(IEnumerable<EditClip> klipler) => klipler.Select(k => (k.SourceStart, k.SourceEnd)).ToArray();

    [Fact]
    public void SaniyeListesiSiraliTekrarsizTickeCevrilir()
    {
        Assert.Equal(new[] { S(1), S(2), S(3) }, SceneSplit.ToTicks(new[] { 3, 1, 2, 1, 0, -4, double.NaN, double.PositiveInfinity }));
        Assert.Empty(SceneSplit.ToTicks(Array.Empty<double>()));
        Assert.Equal(S(0.5), SceneSplit.MinPieceTicks);
    }

    [Fact]
    public void ParcaDisindakiVeKenardakiSinirYokSayilir()
    {
        var klip = new EditClip(S(2), S(8));

        Assert.Equal(new[] { S(4) }, SceneSplit.Accepted(klip, new[] { S(1), S(4), S(9) }, Min));
        Assert.Empty(SceneSplit.Accepted(klip, new[] { S(0.5), S(2), S(8), S(8.5), S(20) }, Min));

        var ayni = SceneSplit.Pieces(klip, new[] { S(1), S(2), S(8), S(9) }, Min);
        Assert.Same(klip, Assert.Single(ayni));
        Assert.Same(klip, Assert.Single(SceneSplit.Pieces(klip, Array.Empty<long>(), Min)));
    }

    [Fact]
    public void KenaraYaDaOncekiSiniraYakinSinirAtilir()
    {
        var klip = new EditClip(S(2), S(8));

        Assert.Empty(SceneSplit.Accepted(klip, new[] { S(2.4), S(7.6) }, Min));
        Assert.Equal(new[] { S(2.5), S(7.5) }, SceneSplit.Accepted(klip, new[] { S(2.5), S(7.5) }, Min));

        Assert.Equal(new[] { S(4), S(4.5) }, SceneSplit.Accepted(klip, new[] { S(4), S(4.2), S(4.4), S(4.5) }, Min));
        Assert.Equal(new[] { S(4), S(5) }, SceneSplit.Accepted(klip, new[] { S(5), S(4.3), S(4), S(4) }, Min));

        Assert.Equal(new[] { S(4), S(4.2) }, SceneSplit.Accepted(klip, new[] { S(4), S(4.2) }, S(0.2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneSplit.Accepted(klip, new[] { S(4) }, 0));
    }

    [Fact]
    public void ParcalarKaynagiEksiksizOrterVeAyarlariTasir()
    {
        var klip = new EditClip(S(2), S(8), 2m) { Effects = ClipEffects.None with { FadeIn = S(0.3), FadeOut = S(0.4), VolumeDb = -6 } };

        var parcalar = SceneSplit.Pieces(klip, new[] { S(6), S(4) }, Min);
        Assert.Equal(new[] { (S(2), S(4)), (S(4), S(6)), (S(6), S(8)) }, Araliklar(parcalar));
        Assert.All(parcalar, p => Assert.True(p.Speed == 2m && !p.Reversed && p.Effects.VolumeDb == -6));
        Assert.Equal(new[] { S(0.3), 0, 0 }, parcalar.Select(p => p.Effects.FadeIn));
        Assert.Equal(new[] { 0, 0, S(0.4) }, parcalar.Select(p => p.Effects.FadeOut));
        Assert.Equal(klip.SourceLength, parcalar.Sum(p => p.SourceLength));

        var ters = new EditClip(S(2), S(8), 1m, reversed: true) { Effects = ClipEffects.None with { FadeIn = S(0.3), FadeOut = S(0.4) } };
        var tersParcalar = SceneSplit.Pieces(ters, new[] { S(4), S(6) }, Min);
        Assert.Equal(new[] { (S(6), S(8)), (S(4), S(6)), (S(2), S(4)) }, Araliklar(tersParcalar));
        Assert.All(tersParcalar, p => Assert.True(p.Reversed));
        Assert.Equal(new[] { S(0.3), 0, 0 }, tersParcalar.Select(p => p.Effects.FadeIn));
        Assert.Equal(new[] { 0, 0, S(0.4) }, tersParcalar.Select(p => p.Effects.FadeOut));
    }

    [Fact]
    public void PlanYalnizSinirDusenParcayiBoler()
    {
        var a = new EditClip(S(0), S(3));
        var b = new EditClip(S(5), S(9));
        var c = new EditClip(S(9), S(12));

        var plan = SceneSplit.Plan(new[] { a, b, c }, new[] { S(4), S(7), S(9) }, Min);
        Assert.Equal(new[] { (S(0), S(3)), (S(5), S(7)), (S(7), S(9)), (S(9), S(12)) }, Araliklar(plan));
        Assert.Same(a, plan[0]);
        Assert.Same(c, plan[3]);
    }

    [Fact]
    public void CizelgeParcaSilmedenBolunurVeTekAdimdaGeriAlinir()
    {
        var c = EditTimeline.FromSource(S(10));
        Assert.True(c.RemoveSource(new[] { (S(4), S(5)) }));
        var once = c.Clips.ToArray();
        var sure = c.Duration;

        Assert.Equal(3, c.SplitSource(new[] { S(2), S(4.5), S(7), S(8) }, Min));
        Assert.Equal(new[] { (S(0), S(2)), (S(2), S(4)), (S(5), S(7)), (S(7), S(8)), (S(8), S(10)) }, Araliklar(c.Clips));
        Assert.Equal(sure, c.Duration);
        var sonra = c.Clips.ToArray();

        Assert.True(c.Undo());
        Assert.Equal(once, c.Clips);
        Assert.True(c.Undo());
        Assert.Equal(new[] { (S(0), S(10)) }, Araliklar(c.Clips));
        Assert.False(c.CanUndo);
        Assert.True(c.Redo());
        Assert.True(c.Redo());
        Assert.Equal(sonra, c.Clips);
        Assert.False(c.CanRedo);
    }

    [Fact]
    public void SinirYoksaCizelgeDegismezVeGecmiseAdimYazilmaz()
    {
        var c = EditTimeline.FromSource(S(10));
        var once = c.Clips.ToArray();

        Assert.Equal(0, c.SplitSource(Array.Empty<long>(), Min));
        Assert.Equal(0, c.SplitSource(new[] { S(0.2), S(9.8), S(10), S(15) }, Min));
        Assert.Same(once[0], Assert.Single(c.Clips));
        Assert.False(c.CanUndo);

        Assert.Equal(1, c.SplitSource(new[] { S(5) }, Min));
        Assert.Equal(0, c.SplitSource(new[] { S(5) }, Min));
        Assert.Equal(2, c.Clips.Count);
        Assert.True(c.Undo());
        Assert.False(c.CanUndo);
    }

    [Fact]
    public void TaramaArgumaniIkiIsParcacigiVeVarsayilanEsikTasir()
    {
        var args = SceneDetector.CutArgs("kaynak.mp4");

        Assert.Equal(2, SceneDetector.CutThreads);
        Assert.Equal("2", args[Array.IndexOf(args, "-threads") + 1]);
        Assert.Equal("2", args[Array.IndexOf(args, "-filter_complex_threads") + 1]);
        Assert.True(Array.IndexOf(args, "-threads") < Array.IndexOf(args, "-i"));
        Assert.Equal("kaynak.mp4", args[Array.IndexOf(args, "-i") + 1]);
        Assert.Equal(1, args.Count(a => a == "-i"));
        Assert.Contains("-nostdin", args);
        Assert.Equal("pipe:1", args[Array.IndexOf(args, "-progress") + 1]);
        var suzgec = args[Array.IndexOf(args, "-filter_complex") + 1];
        Assert.Contains("select='gte(scene,0.105)+eq(n,0)'", suzgec);
        Assert.Contains("metadata=print", suzgec);
        Assert.DoesNotContain(args, a => a is "-c:v" or "-c:a" or "-y");
        Assert.Equal(2, args.Count(a => a == "null"));

        Assert.Contains("gte(scene,0.3)", SceneDetector.CutArgs("k.mp4", 0.3)[Array.IndexOf(args, "-filter_complex") + 1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneDetector.CutArgs("k.mp4", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneDetector.CutArgs("k.mp4", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneDetector.CutArgs("k.mp4", double.NaN));
    }

    [Fact]
    public void ArayuzSinirlardaBolerVeTekAdimdaGeriAlir()
    {
        Arayuz("sahne-arayuz", (view, model) =>
        {
            Assert.True(view.BtnSceneSplit.IsEnabled);
            string? kaynak = null;
            var sure = 0d;
            view.SceneScanner = (yol, saniye, ilerleme, _) =>
            {
                kaynak = yol;
                sure = saniye;
                ilerleme.Report(1);
                return Task.FromResult<IReadOnlyList<double>>(new[] { 300, 100, 100.2, 599.8, 700 });
            };

            view.BtnSceneSplit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            DenetimSurucu.Pump(view.Player, () => !view.SilenceScanning, 10);

            Assert.NotNull(kaynak);
            Assert.Equal(600, sure);
            Assert.Equal(new[] { (S(0), S(100)), (S(100), S(300)), (S(300), S(600)) }, Araliklar(model.Clips));
            Assert.Equal(S(600), model.Duration);
            Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.scene.done"), 2), view.TxtSilenceStatus.Text);
            Assert.True(view.BtnSceneSplit.IsEnabled);
            Assert.False(view.BtnSilenceCancel.IsVisible);

            Assert.True(view.Undo());
            Assert.Equal(new[] { (S(0), S(600)) }, Araliklar(model.Clips));
            Assert.False(view.Undo());
        });
    }

    [Fact]
    public void ArayuzSinirYoksaDegismezVeBulunamadiDer()
    {
        Arayuz("sahne-bos", (view, model) =>
        {
            var once = model.Clips.ToArray();
            view.SceneScanner = (_, _, _, _) => Task.FromResult<IReadOnlyList<double>>(Array.Empty<double>());
            var bos = view.SplitScenesAsync();
            DenetimSurucu.Pump(view.Player, () => bos.IsCompleted, 10);
            Assert.Equal(0, bos.Result);
            Assert.Equal(Strings.Get("editor.scene.none"), view.TxtSilenceStatus.Text);
            Assert.Equal(once, model.Clips);

            view.SceneScanner = (_, _, _, _) => Task.FromResult<IReadOnlyList<double>>(new[] { 0.2, 599.9, 900 });
            var yakin = view.SplitScenesAsync();
            DenetimSurucu.Pump(view.Player, () => yakin.IsCompleted, 10);
            Assert.Equal(0, yakin.Result);
            Assert.Equal(Strings.Get("editor.scene.none"), view.TxtSilenceStatus.Text);
            Assert.Equal(once, model.Clips);
            Assert.False(view.Undo());
        });
    }

    [Fact]
    public void ArayuzIptaldeVeHatadaDegismez()
    {
        Arayuz("sahne-iptal", (view, model) =>
        {
            var once = model.Clips.ToArray();
            view.SceneScanner = async (_, _, _, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new[] { 300d };
            };
            var bekleyen = view.SplitScenesAsync();
            Assert.True(view.SilenceScanning);
            Assert.True(view.BtnSilenceCancel.IsVisible);
            Assert.False(view.BtnSceneSplit.IsEnabled);
            Assert.False(view.BtnSilenceScan.IsEnabled);
            Assert.Equal(Strings.Get("editor.silence.scanning"), view.TxtSilenceStatus.Text);
            view.BtnSilenceCancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            DenetimSurucu.Pump(view.Player, () => bekleyen.IsCompleted, 10);
            Assert.Equal(0, bekleyen.Result);
            Assert.False(view.SilenceScanning);
            Assert.False(view.BtnSilenceCancel.IsVisible);
            Assert.True(view.BtnSceneSplit.IsEnabled);
            Assert.Equal(once, model.Clips);
            Assert.False(view.Undo());

            view.SceneScanner = (_, _, _, _) => Task.FromException<IReadOnlyList<double>>(new InvalidOperationException("bozuk"));
            var bozuk = view.SplitScenesAsync();
            DenetimSurucu.Pump(view.Player, () => bozuk.IsCompleted, 10);
            Assert.Equal(0, bozuk.Result);
            Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.silence.failed"), "bozuk"), view.TxtSilenceStatus.Text);
            Assert.Equal(once, model.Clips);
            Assert.False(view.Undo());
            Assert.True(view.BtnSceneSplit.IsEnabled);
        });
    }

    private static void Arayuz(string ad, Action<EditorView, EditTimeline> govde)
    {
        var klasor = Path.Combine(Kanit, ad);
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
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
                    Assert.False(view.BtnSceneSplit.IsEnabled);
                    var acilis = view.OpenSourceAsync(dosya);
                    DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted, 10);
                    acilis.GetAwaiter().GetResult();
                    view.BtnSilence.IsChecked = true;
                    view.BtnSilence.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.True(view.BtnSceneSplit.IsVisible);
                    govde(view, view.Model!);
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

    /// <summary>320x240, 25 fps: 2 sn kirmizi, ardindan 2 sn mavi; <paramref name="tekRenk"/> ise 4 sn kirmizi.</summary>
    private static string Ornek(string klasor, bool tekRenk = false)
    {
        var yol = Path.Combine(klasor, tekRenk ? "tek-renk.mp4" : "kirmizi-mavi.mp4");
        var args = new List<string> { "-hide_banner", "-nostdin", "-y", "-v", "error" };
        if (tekRenk)
        {
            args.AddRange(new[] { "-f", "lavfi", "-i", "color=c=red:s=320x240:r=25:d=4" });
        }
        else
        {
            args.AddRange(new[]
            {
                "-f", "lavfi", "-i", "color=c=red:s=320x240:r=25:d=2",
                "-f", "lavfi", "-i", "color=c=blue:s=320x240:r=25:d=2",
                "-filter_complex", "[0:v][1:v]concat=n=2:v=1:a=0[v]", "-map", "[v]"
            });
        }

        args.AddRange(new[] { "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-threads", "2", yol });
        Ffmpeg(args.ToArray());
        return yol;
    }

    [FfmpegFact]
    public async Task CanliTaramaRenkDegisimindeBoler()
    {
        var klasor = Path.Combine(Kanit, "sahne-canli");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = Ornek(klasor);
            var ilerleme = new List<double>();
            ProcessPriorityClass? oncelik = null;
            var saat = Stopwatch.StartNew();
            var sinirlar = await SceneDetector.CutsAsync(kaynak, 4, new AnindaIlerleme(ilerleme), CancellationToken.None,
                p => { if (OperatingSystem.IsWindows()) oncelik = p.PriorityClass; });
            saat.Stop();

            var sinir = Assert.Single(sinirlar);
            Assert.InRange(sinir, 2 - Pay, 2 + Pay);
            Assert.Equal(1, ilerleme[^1]);
            Assert.True(ilerleme.Zip(ilerleme.Skip(1)).All(p => p.Second >= p.First), string.Join(" ", ilerleme));
            if (OperatingSystem.IsWindows()) Assert.Equal(ProcessPriorityClass.BelowNormal, oncelik);

            var cizelge = EditTimeline.FromSource(S(4));
            Assert.Equal(1, cizelge.SplitSource(SceneSplit.ToTicks(sinirlar), SceneSplit.MinPieceTicks));
            Assert.Equal(2, cizelge.Clips.Count);
            Assert.Equal(0, cizelge.Clips[0].SourceStart);
            Assert.InRange(EditTime.ToSeconds(cizelge.Clips[0].SourceEnd), 2 - Pay, 2 + Pay);
            Assert.Equal(cizelge.Clips[0].SourceEnd, cizelge.Clips[1].SourceStart);
            Assert.Equal(S(4), cizelge.Clips[1].SourceEnd);
            Assert.Equal(S(4), cizelge.Duration);
            Assert.True(cizelge.Undo());
            Assert.Single(cizelge.Clips);

            var tekRenk = await SceneDetector.CutsAsync(Ornek(klasor, tekRenk: true), 4);
            Assert.Empty(tekRenk);
            var duz = EditTimeline.FromSource(S(4));
            Assert.Equal(0, duz.SplitSource(SceneSplit.ToTicks(tekRenk), SceneSplit.MinPieceTicks));
            Assert.False(duz.CanUndo);

            var bozuk = Path.Combine(klasor, "bozuk.mp4");
            File.WriteAllBytes(bozuk, new byte[64]);
            var hata = await Assert.ThrowsAsync<InvalidOperationException>(() => SceneDetector.CutsAsync(bozuk, 4));
            Assert.Contains("ffmpeg", hata.Message);

            File.WriteAllText(Path.Combine(Kanit, "sahne-canli.txt"),
                FormattableString.Invariant($"sinir {sinir:0.000}\nparca 2\ntarama {saat.ElapsedMilliseconds} ms\nilerleme {ilerleme.Count}\n"));
            KanitKapanisi.Kapat(Kanit, "sahne-canli.txt");
        }
        finally
        {
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        }
    }

    [FfmpegFact]
    public async Task CanliTaramaIptalSureciOldurur()
    {
        var klasor = Path.Combine(Kanit, "sahne-canli-iptal");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = Ornek(klasor);
            using var cts = new CancellationTokenSource();
            Process? surec = null;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SceneDetector.CutsAsync(kaynak, 4, null, cts.Token, p => { surec = p; cts.Cancel(); }));
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
