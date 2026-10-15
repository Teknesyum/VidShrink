using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// DZA-P5, duzenleyici teslimindeki ses izi secimi (<see cref="EditAudioTrack"/>, <c>EditExport.Build(audioTrack)</c>):
/// ilk iz (DZA-P7 oncesinin varsayilani) eski argumanlari harfi harfine yazar, secilen iz her kipte <c>0:a:N</c> olur (birlestirme listesi
/// <c>0:a:0</c> kalir), "Ses yok" ses eslemesini kaldirir, akilli kapisi ve meta dili secilen izden okunur, ek kaynakli
/// birlestirmede yalniz ilk kaynagin izi degisir; secim projeye yazilir. Canli kol iki ses izli 2 sn 320x240 kaynagi
/// (<c>-threads 2</c>) teslim edip ffprobe ile iz sayisini ve kanal sayisini okur. Kanit
/// <c>.calisma/worktree-agent-aa6805a95a188d41e/iz-secim/</c>, test siler.
/// </summary>
public sealed class DuzenleyiciIzSecimTests
{
    private const long Gb = 1_000_000_000;

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static readonly double[] Kareler = { 0.0, 2, 4, 6, 8 };

    private static string Kanit
    {
        get
        {
            var yol = Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-aa6805a95a188d41e", "iz-secim");
            Directory.CreateDirectory(yol);
            return yol;
        }
    }

    private static MediaInfo Bilgi(string ikinci = "ac3", string yol = "kaynak.mkv") => new()
    {
        FilePath = yol,
        FileSizeBytes = 1,
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 1,
        AudioCodec = "aac",
        AudioChannels = 2,
        PixelFormat = "yuv420p",
        BitDepth = 8,
        Streams = new[]
        {
            new SourceStream(0, StreamKind.Video, "h264"),
            new SourceStream(1, StreamKind.Audio, "aac", "tur", IsDefault: true, Channels: 2),
            new SourceStream(2, StreamKind.Audio, ikinci, "eng", Channels: 6, BitrateBps: 448_000),
            new SourceStream(3, StreamKind.Subtitle, "subrip", "tur")
        }
    };

    private static string Deger(IReadOnlyList<string> args, string anahtar) => args[args.ToList().IndexOf(anahtar) + 1];

    private static EditTimeline IkiKlip() => new(new[] { new EditClip(S(1), S(3)), new EditClip(S(4), S(6)) });

    private static ExportPlan Plan(ExportMode kip, int iz, EditTimeline? cizelge = null, MediaInfo? bilgi = null, string cikti = "c.mkv",
        bool meta = false, EditExportSettings? ayar = null)
        => EditExport.Build(cizelge ?? IkiKlip(), bilgi ?? Bilgi(), Kareler, 0, kip, cikti, "is", 8 * Gb,
            DuzenleyiciAkilliKodekTests.Sinirlar(Kareler), _ => true, meta, ayar, iz);

    private static ExportPlan VarsayilanPlan(ExportMode kip, EditTimeline? cizelge = null, MediaInfo? bilgi = null, string cikti = "c.mkv",
        bool meta = false, EditExportSettings? ayar = null)
        => EditExport.Build(cizelge ?? IkiKlip(), bilgi ?? Bilgi(), Kareler, 0, kip, cikti, "is", 8 * Gb,
            DuzenleyiciAkilliKodekTests.Sinirlar(Kareler), _ => true, meta, ayar, EditAudioTrack.First);

    private static IEnumerable<string> Hepsi(ExportPlan plan) => plan.Steps.SelectMany(s => s.Args);

    public static TheoryData<ExportMode, bool, bool> Kipler => new()
    {
        { ExportMode.Full, false, false },
        { ExportMode.Fast, false, false },
        { ExportMode.Fast, true, false },
        { ExportMode.Smart, false, false },
        { ExportMode.Full, false, true }
    };

    [Theory]
    [MemberData(nameof(Kipler))]
    public void VarsayilanIzBugunkuArgumanlariYazar(ExportMode kip, bool ters, bool yalnizSes)
    {
        var cizelge = ters ? new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(4), S(6), 1m, true) }) : IkiKlip();
        var ayar = yalnizSes ? EditExportSettings.Default with { AudioOnly = true } : null;

        var eski = VarsayilanPlan(kip, cizelge, ayar: ayar, meta: true);
        var sifir = Plan(kip, EditAudioTrack.First, cizelge, ayar: ayar, meta: true);
        var disari = Plan(kip, 7, cizelge, ayar: ayar, meta: true);

        Assert.Equal(Hepsi(eski), Hepsi(sifir));
        Assert.Equal(Hepsi(eski), Hepsi(disari));
        Assert.DoesNotContain(Hepsi(eski), a => a.Contains(":a:1", StringComparison.Ordinal));
    }

    [Fact]
    public void TamKipSecilenIziFiltreGirdisineVerir()
    {
        var plan = Plan(ExportMode.Full, 1);

        var graf = Deger(plan.Steps[0].Args, "-filter_complex");
        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.Contains("[0:a:1]atrim=start=1:end=3", graf);
        Assert.Contains("[0:a:1]atrim=start=4:end=6", graf);
        Assert.DoesNotContain("[0:a]", graf);
        Assert.Contains("[0:a]atrim", Deger(VarsayilanPlan(ExportMode.Full).Steps[0].Args, "-filter_complex"));
    }

    [Fact]
    public void YalnizSesSecilenIziOkur()
    {
        var plan = Plan(ExportMode.Full, 1, ayar: EditExportSettings.Default with { AudioOnly = true }, cikti: "c.m4a");

        var graf = Deger(plan.Steps[0].Args, "-filter_complex");
        Assert.StartsWith("[0:a:1]atrim", graf);
        Assert.DoesNotContain("[0:a]", graf);
        Assert.Contains("-vn", plan.Steps[0].Args);
    }

    [Fact]
    public void HizliKipSecilenIziEsler()
    {
        var plan = Plan(ExportMode.Fast, 1);

        var adim = Assert.Single(plan.Steps);
        Assert.Equal(ExportMode.Fast, plan.Effective);
        var esler = adim.Args.Select((a, i) => (a, i)).Where(x => x.a == "-map").Select(x => adim.Args[x.i + 1]).ToArray();
        Assert.Equal(new[] { "0:v:0", "0:a:1" }, esler);
    }

    [Fact]
    public void ParcaliHizliKipParcalardaSecilenIziBirlestirmedeIlkIziAlir()
    {
        var cizelge = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(4), S(6), 1m, true) });
        var plan = Plan(ExportMode.Fast, 1, cizelge, Bilgi("aac"));

        Assert.Equal(ExportMode.Fast, plan.Effective);
        Assert.Equal(3, plan.Steps.Count);
        Assert.All(plan.Steps.Take(2), adim => Assert.Contains("0:a:1", adim.Args));
        Assert.Contains("0:a:0", plan.Steps[^1].Args);
        Assert.DoesNotContain("0:a:1", plan.Steps[^1].Args);
    }

    [Fact]
    public void AkilliKipSesParcalariSecilenIzdenGelir()
    {
        var cizelge = new EditTimeline(new[] { new EditClip(S(1), S(7)), new EditClip(S(2), S(3), 2m) });
        var plan = Plan(ExportMode.Smart, 1, cizelge, Bilgi("aac"));

        Assert.Equal(ExportMode.Smart, plan.Effective);
        var sesler = plan.Steps.Where(s => s.Args.Contains("-vn") && !s.Args.Contains("concat")).ToArray();
        Assert.Equal(2, sesler.Length);
        Assert.All(sesler, s => Assert.Equal("0:a:1", Deger(s.Args, "-map")));
        Assert.Contains("1:a:0", plan.Steps[^1].Args);
    }

    [Fact]
    public void AkilliKapisiSecilenIzinKodeginiOkur()
    {
        var ilk = Plan(ExportMode.Smart, 0, bilgi: Bilgi("pcm_s16le"));
        var ikinci = Plan(ExportMode.Smart, 1, bilgi: Bilgi("pcm_s16le"));

        Assert.Equal(ExportMode.Smart, ilk.Effective);
        Assert.False(ilk.AudioForcedFull);
        Assert.Equal(ExportMode.Full, ikinci.Effective);
        Assert.True(ikinci.AudioForcedFull);
    }

    [Theory]
    [InlineData(ExportMode.Full)]
    [InlineData(ExportMode.Fast)]
    [InlineData(ExportMode.Smart)]
    public void SesYokSecimiSesEslemesiYazmaz(ExportMode kip)
    {
        var plan = Plan(kip, EditAudioTrack.None);

        var hepsi = Hepsi(plan).ToArray();
        Assert.DoesNotContain(hepsi, a => a.Contains("0:a", StringComparison.Ordinal) || a.Contains("[a", StringComparison.Ordinal));
        Assert.DoesNotContain("[aout]", hepsi);
        Assert.DoesNotContain("-c:a", hepsi);
        Assert.Contains(Hepsi(VarsayilanPlan(kip)), a => a.Contains("0:a", StringComparison.Ordinal));
    }

    [Fact]
    public void SesYokYalnizSesTeslimindeSessizKaynakHatasiVerir()
        => Assert.Throws<EditExportNoAudioException>(() =>
            Plan(ExportMode.Full, EditAudioTrack.None, ayar: EditExportSettings.Default with { AudioOnly = true }, cikti: "c.m4a"));

    [Fact]
    public void MetaSilmeSecilenIzinDiliniYazar()
    {
        var ikinci = Plan(ExportMode.Full, 1, meta: true).Steps[^1].Args;
        var ilk = Plan(ExportMode.Full, 0, meta: true).Steps[^1].Args;
        var sessiz = Plan(ExportMode.Full, EditAudioTrack.None, meta: true).Steps[^1].Args;

        Assert.Equal("language=eng", Deger(ikinci, "-metadata:s:a:0"));
        Assert.Equal("language=tur", Deger(ilk, "-metadata:s:a:0"));
        Assert.DoesNotContain("-metadata:s:a:0", sessiz);
        Assert.Contains("-map_metadata", sessiz);
    }

    [Fact]
    public void BirlestirmedeYalnizIlkKaynaginIziDegisir()
    {
        var ek = Bilgi(yol: "ek.mkv") with { Streams = new[] { new SourceStream(0, StreamKind.Video, "h264"), new SourceStream(1, StreamKind.Audio, "aac") } };
        var kaynaklar = new Dictionary<int, ExportSource>
        {
            [0] = new(Bilgi(), Kareler, 0),
            [1] = new(ek, Kareler, 0)
        };
        var cizelge = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(0), S(2)) { Source = 1 } }, extraSources: new[] { S(60) });

        var plan = EditExport.Build(cizelge, kaynaklar, ExportMode.Full, "c.mkv", "is", 8 * Gb, audioTrack: 1);
        var eski = EditExport.Build(cizelge, kaynaklar, ExportMode.Full, "c.mkv", "is", 8 * Gb);

        var graf = Deger(plan.Steps[0].Args, "-filter_complex");
        Assert.Contains("[0:a:1]atrim=start=1:end=3", graf);
        Assert.Contains("[1:a]atrim=start=0:end=2", graf);
        Assert.Contains("[0:a]atrim=start=1:end=3", Deger(eski.Steps[0].Args, "-filter_complex"));
    }

    [Fact]
    public void BirlestirmedeIlkKaynakSessizSecilirseEkKaynakSesiKalir()
    {
        var kaynaklar = new Dictionary<int, ExportSource>
        {
            [0] = new(Bilgi(), Kareler, 0),
            [1] = new(Bilgi(yol: "ek.mkv"), Kareler, 0)
        };
        var cizelge = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(0), S(2)) { Source = 1 } }, extraSources: new[] { S(60) });

        var graf = Deger(EditExport.Build(cizelge, kaynaklar, ExportMode.Full, "c.mkv", "is", 8 * Gb, audioTrack: EditAudioTrack.None)
            .Steps[0].Args, "-filter_complex");

        Assert.DoesNotContain("[0:a", graf);
        Assert.Contains("anullsrc", graf);
        Assert.Contains("[1:a]atrim=start=0:end=2", graf);
    }

    [Fact]
    public void YalnizEkKaynakKullanilincaSecimEtkisiz()
    {
        var kaynaklar = new Dictionary<int, ExportSource>
        {
            [0] = new(Bilgi(), Kareler, 0),
            [1] = new(Bilgi(yol: "ek.mkv"), Kareler, 0)
        };
        var cizelge = new EditTimeline(new[] { new EditClip(S(0), S(2)) { Source = 1 } }, extraSources: new[] { S(60) });

        var plan = EditExport.Build(cizelge, kaynaklar, ExportMode.Full, "c.mkv", "is", 8 * Gb, audioTrack: 1);

        var varsayilan = EditExport.Build(cizelge, kaynaklar, ExportMode.Full, "c.mkv", "is", 8 * Gb);

        Assert.Equal(varsayilan.Steps[^1].Args, plan.Steps[^1].Args);
        Assert.Contains("[0:a:1]atrim", Deger(plan.Steps[^1].Args, "-filter_complex"));
        Assert.Contains("[aout1]", plan.Steps[^1].Args);
    }

    [Fact]
    public void AyriParcalarSecilenIziTasir()
    {
        var planlar = EditExport.BuildSegments(IkiKlip(), Bilgi(), Kareler, 0, ExportMode.Full, new[] { "a.mkv", "b.mkv" },
            new[] { "is1", "is2" }, 8 * Gb, audioTrack: 1);

        Assert.Equal(2, planlar.Count);
        Assert.All(planlar, p => Assert.StartsWith("[0:v]trim", Deger(p.Steps[0].Args, "-filter_complex")));
        Assert.All(planlar, p => Assert.Contains("[0:a:1]atrim", Deger(p.Steps[0].Args, "-filter_complex")));
    }

    [Fact]
    public void IzListesiYalnizSesAkislariniSiraylaVerir()
    {
        var izler = EditAudioTrack.Streams(Bilgi());

        Assert.Equal(new[] { "aac", "ac3" }, izler.Select(i => i.Codec));
        Assert.Equal(1, EditAudioTrack.Resolve(Bilgi(), 1));
        Assert.Equal(EditAudioTrack.First, EditAudioTrack.Resolve(Bilgi(), 2));
        Assert.Equal(EditAudioTrack.None, EditAudioTrack.Resolve(Bilgi() with { AudioCodec = null }, 1));
        Assert.Equal("ac3", EditAudioTrack.Select(Bilgi(), 1).AudioCodec);
        Assert.Equal(6, EditAudioTrack.Select(Bilgi(), 1).AudioChannels);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(EditAudioTrack.None)]
    public void ProjeSecilenIziGeriVerir(int iz)
    {
        var damga = new SourceStamp("kaynak.mkv", 10, 20);
        var proje = EditProject.From(damga, new EditTimeline(new[] { new EditClip(S(0), S(2)) }, S(60)), audioTrack: iz);

        var geri = EditProject.Parse(proje.ToJson());

        Assert.NotNull(geri);
        Assert.Equal(iz, geri!.AudioTrack);
    }

    [Fact]
    public void VarsayilanIzProjeDosyasinaYazilmaz()
    {
        var damga = new SourceStamp("kaynak.mkv", 10, 20);
        var cizelge = new EditTimeline(new[] { new EditClip(S(0), S(2)) }, S(60));

        var json = EditProject.From(damga, cizelge).ToJson();

        Assert.DoesNotContain("audioTrack", json);
        Assert.Equal(json, EditProject.From(damga, cizelge, audioTrack: EditAudioTrack.Default).ToJson());
        Assert.Equal(EditAudioTrack.Default, EditProject.Parse(json)!.AudioTrack);
    }

    [FfmpegFact]
    public async Task CanliTeslimSecilenIziTasir()
    {
        KanitKapanisi.Onceki(Kanit, "iki-iz.mkv", "tam.mkv", "hizli.mkv", "varsayilan.mkv", "sessiz.mkv");
        var kaynak = Path.Combine(Kanit, "iki-iz.mkv");
        Ffmpeg("-hide_banner", "-y",
            "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=2",
            "-f", "lavfi", "-i", "sine=f=440:d=2",
            "-f", "lavfi", "-i", "sine=f=880:d=2",
            "-filter_complex", "[2:a]pan=stereo|c0=c0|c1=c0[st]",
            "-map", "0:v", "-map", "1:a", "-map", "[st]",
            "-c:v", "libx264", "-preset", "ultrafast", "-g", "15", "-threads", "2",
            "-c:a", "aac", "-metadata:s:a:0", "language=tur", "-metadata:s:a:1", "language=eng", kaynak);
        var satirlar = new List<string>();

        foreach (var (kip, iz, ad, kanal) in new[]
                 {
                     (ExportMode.Full, 1, "tam.mkv", "2"),
                     (ExportMode.Fast, 1, "hizli.mkv", "2"),
                     (ExportMode.Full, EditAudioTrack.First, "varsayilan.mkv", "1"),
                     (ExportMode.Full, EditAudioTrack.None, "sessiz.mkv", "")
                 })
        {
            var cikti = Path.Combine(Kanit, ad);
            var plan = await EditExportRunner.PrepareAsync(new[] { kaynak }, new EditTimeline(new[] { new EditClip(S(0), S(1.5)) }), kip, cikti,
                8 * Gb, audioTrack: iz);
            await EditExportRunner.RunAsync(plan, new Bos());
            var sesler = Ffprobe("-select_streams", "a", "-show_entries", "stream=channels", "-of", "csv=p=0", cikti)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            satirlar.Add($"{kip} iz={iz} sesler={string.Join(",", sesler)}");
            if (kanal.Length == 0) Assert.Empty(sesler);
            else Assert.Equal(kanal, Assert.Single(sesler));
        }

        File.WriteAllLines(Path.Combine(Kanit, "olcu.txt"), satirlar);
        KanitKapanisi.Kapat(Kanit, "iki-iz.mkv", "tam.mkv", "hizli.mkv", "varsayilan.mkv", "sessiz.mkv", "olcu.txt");
    }

    private static void Ffmpeg(params string[] args)
    {
        var psi = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var hata = surec.StandardError.ReadToEndAsync();
        if (!surec.WaitForExit(30000))
        {
            surec.Kill(true);
            Assert.Fail("ffmpeg 30 sn icinde bitmedi.");
        }

        Assert.True(surec.ExitCode == 0, hata.GetAwaiter().GetResult());
    }

    private static string Ffprobe(params string[] args)
    {
        var psi = new ProcessStartInfo("ffprobe") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("error");
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var surec = Process.Start(psi)!;
        var metin = surec.StandardOutput.ReadToEnd();
        Assert.True(surec.WaitForExit(15000));
        return metin;
    }

    private sealed class Bos : IProgress<EncodeProgress>
    {
        public void Report(EncodeProgress value)
        {
        }
    }
}

/// <summary>
/// DZA-P5 arayuz kolu, sahte <c>IzMotoru</c> ile (iki ses izi, mpv kimlikleri 1 ve 2): teslim kutusu "Tum izler"i, izleri ve
/// "Ses yok"u listeler, monitor dugmesi yalniz birden cok izde gorunur ve oynaticiya sonraki izin kimligini yazar,
/// onizleme yeniden yuklenince (kesim) secim motora yeniden verilir, secim teslim anahtarini degistirir, projeye
/// yazilip yeni gorunumde geri gelir, "Ses yok" motora 0 yazar. Teslimin secimi <c>-map</c>'e indirdigi
/// <c>EditorView.Teslim</c> kaynagindan pimlidir. Kanit <c>.calisma/worktree-agent-aa6805a95a188d41e/iz-arayuz-*</c>, test siler.
/// </summary>
public sealed class DuzenleyiciIzSecimArayuzTests
{
    private const string Dal = "worktree-agent-aa6805a95a188d41e";

    private static string Kanit => Path.Combine(GirdiKanit.Root, ".calisma", Dal);

    private static string Klasor(string ad)
    {
        var klasor = Path.Combine(Kanit, "iz-arayuz-" + ad);
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static void Kapat(string klasor) => KanitKapanisi.Kapat(Kanit, Path.GetFileName(klasor));

    private static string Kaynak(string klasor)
    {
        var dosya = Path.Combine(klasor, "kaynak.mp4");
        File.WriteAllBytes(dosya, new byte[16]);
        return dosya;
    }

    private static string Anahtar(EditorView view)
    {
        var yontem = typeof(EditorView).GetMethod("SaveKey", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)yontem.Invoke(view, new object[] { view.Model! })!;
    }

    [Fact]
    public void IkiIzliKaynaktaListeDugmeVeMotorSecimiIzler()
    {
        var klasor = Klasor("iki");
        var dosya = Kaynak(klasor);
        AppHost.Run(() =>
        {
            using var sahne = new Sahne(klasor, null, 2);
            sahne.Ac(dosya);
            var view = sahne.View;
            var motor = sahne.Motor!;

            Assert.Equal(2, view.AudioTracks.Count);
            Assert.Equal(EditAudioTrack.Default, view.SelectedAudioTrack);
            var ogeler = view.CmbExportAudioTrack.ItemsSource!.Cast<object>().Select(o => o.ToString()).ToArray();
            Assert.Equal(4, ogeler.Length);
            Assert.Equal(Strings.Get("editor.export.audio-track.all"), ogeler[0]);
            Assert.Equal(Strings.Get("editor.export.audio-track.none"), ogeler[3]);
            Assert.Equal(0, view.CmbExportAudioTrack.SelectedIndex);
            Assert.True(view.CmbExportAudioTrack.IsEnabled);
            Assert.True(view.BtnAudioTrack.IsVisible);
            Assert.Equal("1/2", view.TxtAudioTrack.Text);

            var ilkAnahtar = Anahtar(view);
            motor.Yazilan.Clear();
            view.BtnAudioTrack.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(1, view.SelectedAudioTrack);
            Assert.Equal(new long[] { 2 }, motor.Yazilan);
            Assert.Equal(2, view.CmbExportAudioTrack.SelectedIndex);
            Assert.Equal("2/2", view.TxtAudioTrack.Text);
            Assert.NotEqual(ilkAnahtar, Anahtar(view));

            motor.Yazilan.Clear();
            var yukleme = view.Reloads;
            Assert.True(sahne.Kes(100));
            Assert.True(sahne.Bekle(() => view.Reloads > yukleme && motor.Yazilan.Count > 0), "yeniden yukleme secimi motora vermedi");
            Assert.All(motor.Yazilan, id => Assert.Equal(2, id));
            Assert.Equal(1, view.SelectedAudioTrack);

            motor.Yazilan.Clear();
            view.CycleAudioTrack();
            Assert.Equal(0, view.SelectedAudioTrack);
            Assert.Equal(new long[] { 1 }, motor.Yazilan);

            motor.Yazilan.Clear();
            view.CmbExportAudioTrack.SelectedIndex = 3;
            Assert.Equal(EditAudioTrack.None, view.SelectedAudioTrack);
            Assert.Equal(new long[] { 0 }, motor.Yazilan);
            Assert.Equal(string.Empty, view.TxtAudioTrack.Text);
            Assert.Contains("|aud|-1", Anahtar(view));

            view.CycleAudioTrack();
            Assert.Equal(0, view.SelectedAudioTrack);

            motor.Yazilan.Clear();
            view.CmbExportAudioTrack.SelectedIndex = 0;
            Assert.Equal(EditAudioTrack.All, view.SelectedAudioTrack);
            Assert.Equal(new long[] { 1 }, motor.Yazilan);
            Assert.Equal("1/2", view.TxtAudioTrack.Text);
            Assert.Contains("|aud|-2", Anahtar(view));
        });
        Kapat(klasor);
    }

    [Fact]
    public void TekIzliKaynaktaDugmeGizliIzsizdeKutuPasif()
    {
        var klasor = Klasor("tek");
        var dosya = Kaynak(klasor);
        AppHost.Run(() =>
        {
            using (var sahne = new Sahne(klasor, null, 1))
            {
                sahne.Ac(dosya);
                Assert.Single(sahne.View.AudioTracks);
                Assert.False(sahne.View.BtnAudioTrack.IsVisible);
                Assert.True(sahne.View.CmbExportAudioTrack.IsEnabled);
                Assert.Equal(3, sahne.View.CmbExportAudioTrack.ItemCount);
                sahne.View.CycleAudioTrack();
                Assert.Equal(EditAudioTrack.Default, sahne.View.SelectedAudioTrack);
            }

            using (var sahne = new Sahne(klasor, null, 0))
            {
                sahne.Ac(dosya);
                Assert.Empty(sahne.View.AudioTracks);
                Assert.False(sahne.View.BtnAudioTrack.IsVisible);
                Assert.False(sahne.View.CmbExportAudioTrack.IsEnabled);
                Assert.Equal(-1, sahne.View.CmbExportAudioTrack.SelectedIndex);
                Assert.Equal(EditAudioTrack.Default, sahne.View.SelectedAudioTrack);
            }
        });
        Kapat(klasor);
    }

    [Fact]
    public void SecimProjeyeYazilirVeYeniGorunumdeGeriGelir()
    {
        var klasor = Klasor("proje");
        var dosya = Kaynak(klasor);
        var depo = new EditProjectStore(Path.Combine(klasor, "kayit"));
        AppHost.Run(() =>
        {
            using (var ilk = new Sahne(klasor, depo, 2))
            {
                ilk.Ac(dosya);
                ilk.View.CycleAudioTrack();
                Assert.True(ilk.View.FlushProject());
            }

            Assert.Equal(1, EditProject.Read(depo.PathFor(dosya))!.AudioTrack);

            using (var ikinci = new Sahne(klasor, depo, 2))
            {
                ikinci.Ac(dosya);
                Assert.Equal(1, ikinci.View.SelectedAudioTrack);
                Assert.Equal(2, ikinci.View.CmbExportAudioTrack.SelectedIndex);
                Assert.True(ikinci.Bekle(() => ikinci.Motor!.Yazilan.Count > 0 && ikinci.Motor.Yazilan[^1] == 2), "geri gelen secim motora yazilmadi");
            }

            using (var ucuncu = new Sahne(klasor, null, 2))
            {
                ucuncu.Ac(dosya);
                Assert.Equal(EditAudioTrack.Default, ucuncu.View.SelectedAudioTrack);
            }
        });
        Kapat(klasor);
    }

    [Fact]
    public void TeslimSecimiHerIkiHazirlamaCagrisinaVerir()
    {
        var kaynak = File.ReadAllText(Path.Combine(GirdiKanit.Root, "src", "VidShrink.App", "Editing", "EditorView.Teslim.cs"));
        Assert.Equal(2, kaynak.Split("audioTrack: SelectedAudioTrack").Length - 1);
    }

    private sealed class Sahne : IDisposable
    {
        private readonly Window _pencere;

        internal Sahne(string klasor, EditProjectStore? depo, int iz)
        {
            View = new EditorView
            {
                KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1 },
                Projects = depo,
                AutosaveDelay = TimeSpan.FromHours(1),
            };
            View.Player.EngineFactory = () => Motor = new IzMotoru(iz);
            View.OverlayRoot = Path.Combine(klasor, "katman");
            _pencere = new Window { Width = 900, Height = 600, Content = View };
            _pencere.Show();
        }

        internal EditorView View { get; }

        internal IzMotoru? Motor { get; private set; }

        internal void Ac(string dosya)
        {
            var acilis = View.OpenSourceAsync(dosya);
            DenetimSurucu.Pump(View.Player, () => acilis.IsCompleted, 10);
            acilis.GetAwaiter().GetResult();
            Assert.NotNull(View.Model);
        }

        internal bool Bekle(Func<bool> kosul)
        {
            var saat = Stopwatch.StartNew();
            while (!kosul() && saat.Elapsed.TotalSeconds < 30)
            {
                using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(2));
                Dispatcher.UIThread.MainLoop(dilim.Token);
                View.Player.RenderLatest();
            }

            return kosul();
        }

        internal bool Kes(double saniye)
        {
            View.TimelineView.Playhead = EditTime.FromSeconds(saniye);
            return View.Split();
        }

        public void Dispose() => _pencere.Close();
    }
}

/// <summary>Ses izlerini bildiren ve ses izi yazimlarini sayan sahte motor.</summary>
internal sealed class IzMotoru : IPlaybackEngine
{
    private readonly byte[] _piksel = Enumerable.Repeat((byte)128, 4 * 64 * 36).ToArray();
    private readonly int _iz;

    internal IzMotoru(int iz) => _iz = iz;

    internal List<long> Yazilan { get; } = new();

    public string Name => "iz-motoru";

    public bool IsOpen { get; private set; }

    public double DurationSeconds => 600;

    public bool HasAudio => _iz > 0;

    public bool IsPaused { get; private set; } = true;

    public bool EndReached { get; set; }

    public double PositionSeconds { get; private set; }

    public double AudioVideoOffsetSeconds => 0;

    public long FramesRendered { get; private set; }

    public long AudioTrack { get; private set; } = 1;

    public IReadOnlyList<PlaybackTrack> Tracks
        => new[] { new PlaybackTrack(1, PlaybackTrackKind.Video, null, null, false, true) }
            .Concat(Enumerable.Range(1, _iz).Select(i => new PlaybackTrack(i, PlaybackTrackKind.Audio, "Iz " + i, i == 1 ? "tur" : "eng", false, i == AudioTrack)))
            .ToArray();

    public void SetAudioTrack(long id)
    {
        Yazilan.Add(id);
        AudioTrack = id;
    }

    public event EventHandler<PlaybackFault>? Faulted { add { } remove { } }

    public Task OpenAsync(string path, CancellationToken ct = default)
    {
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

    public void SetEditGeometry(string? chain)
    {
    }

    public void SetEditTimed(string? video, string? audio)
    {
    }

    public void Dispose() => IsOpen = false;
}
