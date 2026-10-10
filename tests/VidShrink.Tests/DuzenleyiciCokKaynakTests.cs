using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Duzenleyicide cok kaynak: <see cref="EditTimeline.AddSource"/> ve geri alma, parca basina yol
/// yazan <see cref="EdlPreview"/>, birden cok kaynagi tek adimda yeniden kodlayan
/// <see cref="EditExport.Build(EditTimeline, IReadOnlyDictionary{int, ExportSource}, ExportMode, string, string, long, Func{string, bool}?, bool)"/>,
/// surum 1 proje dosyasinin acilmasi, surum 2 gidis donusu, arayuzde kaynak ekleme ve kapanista
/// butun kaynaklarin taramasinin iptali. Canli kol 2 sn'lik iki kaynagi tek surecle birlestirir.
/// Kanit <c>.calisma/worktree-agent-a319bef524e550188/cok-kaynak-*</c>, test siler.
/// </summary>
public sealed class DuzenleyiciCokKaynakTests
{
    private const long Gb = 1_000_000_000;

    private static string Kok => Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-a319bef524e550188");

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static string Klasor(string ad)
    {
        var klasor = Path.Combine(Kok, "cok-kaynak-" + ad);
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static void Kapat(string klasor)
    {
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        KanitKapanisi.Kapat(Kok);
    }

    private static MediaInfo Bilgi(string yol, int en, int boy, double fps, bool ses, double sure = 10) => new()
    {
        FilePath = yol,
        FileSizeBytes = 1_000_000,
        DurationSeconds = sure,
        Width = en,
        Height = boy,
        Fps = fps,
        VideoCodec = "h264",
        AudioCodec = ses ? "aac" : null,
        TotalBitrateBps = 1_000_000
    };

    private static ExportSource Kaynak(string yol, int en, int boy, double fps, bool ses, double sure = 10)
        => new(Bilgi(yol, en, boy, fps, ses, sure), new[] { 0.0 }, 0);

    private static EditTimeline IkiKaynak()
    {
        var model = EditTimeline.FromSource(S(10));
        Assert.Equal(1, model.AddSource(S(4)));
        return model;
    }

    private static string Suzgec(ExportPlan plan)
    {
        var args = Assert.Single(plan.Steps).Args;
        var i = args.ToList().IndexOf("-filter_complex");
        Assert.True(i >= 0, string.Join(' ', args));
        return args[i + 1];
    }

    private static string[] Girdiler(ExportPlan plan)
    {
        var args = plan.Steps.SelectMany(s => s.Args).ToArray();
        return args.Select((a, i) => (a, i)).Where(x => x.a == "-i").Select(x => args[x.i + 1]).ToArray();
    }

    [Fact]
    public void EklenenKaynakSonaTekParcaGelirGeriAlKaynagiSilmez()
    {
        var model = EditTimeline.FromSource(S(10));

        Assert.Empty(model.ExtraSources);
        Assert.Equal(1, model.AddSource(S(4)));
        Assert.Single(model.ExtraSources);
        Assert.Equal(2, model.Clips.Count);
        Assert.Equal(new EditClip(0, S(4)) { Source = 1 }, model.Clips[1]);
        Assert.NotEqual(new EditClip(0, S(4)), model.Clips[1]);
        Assert.Equal(S(14), model.Duration);
        Assert.Equal(S(4), model.DurationOf(1));
        Assert.Equal(S(10), model.DurationOf(0));
        Assert.Equal(S(11), model.ToTimeline(S(1), 1));
        Assert.Equal(S(1), model.ToTimeline(S(1)));
        Assert.Equal(S(4), model.TrimEdgeRange(1, false).Max);
        Assert.Equal(S(10), model.TrimEdgeRange(0, false).Max);

        Assert.True(model.Undo());
        Assert.Single(model.Clips);
        Assert.Single(model.ExtraSources);
        Assert.True(model.Redo());
        Assert.Equal(2, model.Clips.Count);
        Assert.Equal(1, model.Clips[1].Source);

        Assert.Equal(2, model.AddSource(S(3)));
        Assert.Equal(S(17), model.Duration);
        Assert.Equal(2, model.Clips[2].Source);
        Assert.ThrowsAny<ArgumentException>(() => model.AddSource(0));
        Assert.Equal(2, model.ExtraSources.Count);
    }

    [Fact]
    public void EdlHerParcayiKendiKaynagininYoluylaYazar()
    {
        var model = IkiKaynak();
        var edl = new EdlPreview(new[] { "birinci.mp4", "ikinci.mp4" }, model);
        var satirlar = edl.Document.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, satirlar.Length);
        Assert.Equal(EdlPreview.Header, satirlar[0]);
        Assert.Contains("birinci.mp4", satirlar[1]);
        Assert.DoesNotContain("ikinci.mp4", satirlar[1]);
        Assert.Contains("ikinci.mp4", satirlar[2]);
        Assert.DoesNotContain("birinci.mp4", satirlar[2]);
        Assert.Equal(S(14), edl.TimelineDuration);

        Assert.ThrowsAny<ArgumentException>(() => new EdlPreview(new[] { "birinci.mp4" }, model));
        Assert.DoesNotContain("ikinci.mp4", new EdlPreview("birinci.mp4", EditTimeline.FromSource(S(10))).Document);
    }

    [Theory]
    [InlineData(ExportMode.Fast, true)]
    [InlineData(ExportMode.Smart, true)]
    [InlineData(ExportMode.Full, false)]
    public void IkiKaynakTekAdimdaIlkKaynaginOlcusuneKodlanir(ExportMode kip, bool zorlandi)
    {
        var kaynaklar = new Dictionary<int, ExportSource>
        {
            [0] = Kaynak("birinci.mp4", 320, 240, 30, true),
            [1] = Kaynak("ikinci.mp4", 160, 120, 25, false, 4)
        };
        var plan = EditExport.Build(IkiKaynak(), kaynaklar, kip, "cikti.mp4", "is", 8 * Gb);
        var args = Assert.Single(plan.Steps).Args;
        var suzgec = Suzgec(plan);

        Assert.Equal(kip, plan.Requested);
        Assert.Equal(ExportMode.Full, plan.Effective);
        Assert.Equal(zorlandi, plan.MergeForcedFull);
        Assert.Equal(new[] { "birinci.mp4", "ikinci.mp4" }, Girdiler(plan));
        Assert.Equal(14.0, plan.Steps[0].DurationSeconds, 3);
        Assert.Equal("cikti.mp4", args[^1]);

        Assert.Contains("[0:v]trim=", suzgec);
        Assert.Contains("[1:v]trim=", suzgec);
        Assert.Contains("[0:a]atrim=", suzgec);
        Assert.DoesNotContain("[1:a]", suzgec);
        Assert.Contains("anullsrc=", suzgec);
        Assert.Contains("concat=n=2:v=1:a=1[vout][aout]", suzgec);
        Assert.Contains("[aout]", args);
        Assert.Contains("-c:a", args);

        var hizlar = Regex.Matches(suzgec, @"fps=([^,\[;]+)").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(2, hizlar.Length);
        Assert.Equal(hizlar[0], hizlar[1]);
        Assert.StartsWith("30", hizlar[0]);
        var ikinciZincir = suzgec[suzgec.IndexOf("[1:v]", StringComparison.Ordinal)..suzgec.IndexOf("[v1]", StringComparison.Ordinal)];
        Assert.Contains("320", ikinciZincir);
        Assert.Contains("240", ikinciZincir);
    }

    [Fact]
    public void SessizKaynaklarBirlesinceSesYoluAcilmaz()
    {
        var kaynaklar = new Dictionary<int, ExportSource>
        {
            [0] = Kaynak("birinci.mp4", 320, 240, 30, false),
            [1] = Kaynak("ikinci.mp4", 320, 240, 30, false, 4)
        };
        var plan = EditExport.Build(IkiKaynak(), kaynaklar, ExportMode.Full, "cikti.mp4", "is", 8 * Gb);
        var args = plan.Steps[0].Args;

        Assert.Contains("concat=n=2:v=1:a=0[vout]", Suzgec(plan));
        Assert.DoesNotContain("anullsrc", Suzgec(plan));
        Assert.DoesNotContain("[aout]", args);
        Assert.DoesNotContain("-c:a", args);
    }

    [Fact]
    public void TekKaynakKullananCizelgeEskiYoluKosar()
    {
        var birinci = Kaynak("birinci.mp4", 320, 240, 30, true);
        var ikinci = Kaynak("ikinci.mp4", 160, 120, 25, false, 4);
        var kaynaklar = new Dictionary<int, ExportSource> { [0] = birinci, [1] = ikinci };

        foreach (var kip in new[] { ExportMode.Fast, ExportMode.Smart, ExportMode.Full })
        {
            var tek = EditTimeline.FromSource(S(10));
            var eski = EditExport.Build(tek, birinci.Info, birinci.Keyframes, 0, kip, "cikti.mp4", "is", 8 * Gb);
            var yeni = EditExport.Build(tek, new Dictionary<int, ExportSource> { [0] = birinci }, kip, "cikti.mp4", "is", 8 * Gb);

            Assert.Equal(eski.Effective, yeni.Effective);
            Assert.False(yeni.MergeForcedFull);
            Assert.Equal(eski.Steps.SelectMany(s => s.Args), yeni.Steps.SelectMany(s => s.Args));
        }

        var geriAlinan = IkiKaynak();
        Assert.True(geriAlinan.Undo());
        Assert.Equal(ExportMode.Fast, EditExport.Build(geriAlinan, kaynaklar, ExportMode.Fast, "cikti.mp4", "is", 8 * Gb).Effective);
        var plan = EditExport.Build(geriAlinan, kaynaklar, ExportMode.Full, "cikti.mp4", "is", 8 * Gb);
        Assert.False(plan.MergeForcedFull);
        Assert.NotEmpty(Girdiler(plan));
        Assert.All(Girdiler(plan), yol => Assert.Equal("birinci.mp4", yol));

        var yalnizIkinci = IkiKaynak();
        yalnizIkinci.Delete(0);
        Assert.Equal(1, Assert.Single(yalnizIkinci.Clips).Source);
        var ikinciPlan = EditExport.Build(yalnizIkinci, kaynaklar, ExportMode.Full, "cikti.mp4", "is", 8 * Gb);
        Assert.False(ikinciPlan.MergeForcedFull);
        Assert.NotEmpty(Girdiler(ikinciPlan));
        Assert.All(Girdiler(ikinciPlan), yol => Assert.Equal("ikinci.mp4", yol));

        Assert.ThrowsAny<ArgumentException>(() => EditExport.Build(IkiKaynak(),
            new Dictionary<int, ExportSource> { [0] = birinci }, ExportMode.Full, "cikti.mp4", "is", 8 * Gb));
    }

    [Fact]
    public void Surum1ProjeDosyasiAcilirTekKaynakAyniBicimdeYazilir()
    {
        const string eski = "{\"version\":1,\"source\":{\"path\":\"a.mp4\",\"size\":1,\"modifiedUtcTicks\":1},\"sourceDuration\":2400000,"
            + "\"clips\":[{\"start\":0,\"end\":1200000,\"speed\":1,\"reversed\":false},{\"start\":1200000,\"end\":2400000,\"speed\":1,\"reversed\":false}]}";
        var proje = EditProject.Parse(eski);

        Assert.NotNull(proje);
        Assert.Empty(proje!.ExtraSources);
        Assert.Equal(2, proje.Clips.Count);
        Assert.All(proje.Clips, c => Assert.Equal(0, c.Source));
        Assert.Equal("a.mp4", proje.Source.Path);

        using var yazilan = JsonDocument.Parse(proje.ToJson());
        Assert.Equal(1, yazilan.RootElement.GetProperty("version").GetInt32());
        Assert.False(yazilan.RootElement.TryGetProperty("sources", out _));
        Assert.All(yazilan.RootElement.GetProperty("clips").EnumerateArray(), c => Assert.False(c.TryGetProperty("source", out _)));

        var tek = EditProject.From(new SourceStamp("a.mp4", 1, 1), EditTimeline.FromSource(S(10)));
        using var tekBelge = JsonDocument.Parse(tek.ToJson());
        Assert.Equal(1, tekBelge.RootElement.GetProperty("version").GetInt32());
        Assert.False(tekBelge.RootElement.TryGetProperty("sources", out _));
    }

    [Fact]
    public void CokKaynakliProjeSurum2YazilirGeriOkunur()
    {
        var model = IkiKaynak();
        var ek = new SourceStamp("b.mp4", 7, 9);
        var proje = EditProject.From(new SourceStamp("a.mp4", 1, 1), model, "Smart", new[] { ek });
        var json = proje.ToJson();

        using (var belge = JsonDocument.Parse(json))
        {
            Assert.Equal(2, EditProject.CurrentVersion);
            Assert.Equal(2, belge.RootElement.GetProperty("version").GetInt32());
            var kaynak = Assert.Single(belge.RootElement.GetProperty("sources").EnumerateArray());
            Assert.Equal("b.mp4", kaynak.GetProperty("path").GetString());
            Assert.Equal(S(4), kaynak.GetProperty("duration").GetInt64());
            var parcalar = belge.RootElement.GetProperty("clips").EnumerateArray().ToArray();
            Assert.False(parcalar[0].TryGetProperty("source", out _));
            Assert.Equal(1, parcalar[1].GetProperty("source").GetInt32());
        }

        var okunan = EditProject.Parse(json);
        Assert.NotNull(okunan);
        Assert.Equal(new ExtraSource(ek, S(4)), Assert.Single(okunan!.ExtraSources));
        Assert.Equal(model.Clips, okunan.Clips);
        Assert.Equal(1, okunan.Clips[1].Source);
        Assert.True(okunan.FitsIn(S(10)));

        Assert.Contains("\"version\": 2", json);
        Assert.Null(EditProject.Parse(json.Replace("\"version\": 2", "\"version\": 3")));
        Assert.Contains("\"source\": 1", json);
        Assert.Null(EditProject.Parse(json.Replace("\"source\": 1", "\"source\": 5")));
        Assert.Null(EditProject.Parse(json.Replace("\"version\": 2", "\"version\": 1")));

        Assert.ThrowsAny<ArgumentException>(() => EditProject.From(new SourceStamp("a.mp4", 1, 1), model));
        Assert.ThrowsAny<ArgumentException>(() => EditProject.From(new SourceStamp("a.mp4", 1, 1), model, null, new[] { ek, ek }));
    }

    private static Task<T> IptaleKadar<T>(CancellationToken ct)
    {
        var bekleyen = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => bekleyen.TrySetCanceled(ct));
        return bekleyen.Task;
    }

    private static EditorView Gorunum(Func<string, MediaInfo?>? bilgi = null)
    {
        var view = new EditorView
        {
            KnownInfo = bilgi ?? (yol => new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1 }),
            Projects = null,
            PeakReader = (_, _) => Task.FromResult(AudioPeaks.Empty),
            KeyframeReader = (_, _) => Task.FromResult<IReadOnlyList<double>>(Array.Empty<double>()),
        };
        view.Player.EngineFactory = () => new KlipMotoru();
        return view;
    }

    private static T Bekle<T>(EditorView view, Task<T> gorev)
    {
        DenetimSurucu.Pump(view.Player, () => gorev.IsCompleted, 10);
        Assert.True(gorev.IsCompleted, "gorev 10 sn icinde bitmedi");
        return gorev.GetAwaiter().GetResult();
    }

    private static void Bekle(EditorView view, Task gorev)
    {
        DenetimSurucu.Pump(view.Player, () => gorev.IsCompleted, 10);
        Assert.True(gorev.IsCompleted, "gorev 10 sn icinde bitmedi");
        gorev.GetAwaiter().GetResult();
    }

    private static Avalonia.Styling.ControlTheme Tema(string ad)
    {
        Assert.True(Avalonia.Application.Current!.TryFindResource(ad, out var tema));
        return Assert.IsType<Avalonia.Styling.ControlTheme>(tema);
    }

    private static string Sahte(string klasor, string ad)
    {
        var dosya = Path.Combine(klasor, ad);
        File.WriteAllBytes(dosya, new byte[16]);
        return dosya;
    }

    [Fact]
    public void KaynakEkleSonaEklerGeriAlinirOkunamayanDosyaCizelgeyiDegistirmez()
    {
        var klasor = Klasor("ekle");
        try
        {
            var a = Sahte(klasor, "a.mp4");
            var b = Sahte(klasor, "b.mp4");
            var bozuk = Sahte(klasor, "bozuk.mp4");

            AppHost.Run(() =>
            {
                var view = Gorunum(yol => new MediaInfo
                {
                    FilePath = yol, FileSizeBytes = 16, DurationSeconds = yol == b ? 30 : 600,
                    Width = yol == bozuk ? 0 : 320, Height = yol == bozuk ? 0 : 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 1
                });
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    Assert.False(Bekle(view, view.AddSourceAsync(b)));
                    Assert.Empty(view.SourcePaths);

                    Bekle(view, view.OpenSourceAsync(a));
                    Assert.NotNull(view.Model);
                    Assert.Equal(new[] { a }, view.SourcePaths);

                    Assert.True(Bekle(view, view.AddSourceAsync(b)));
                    Assert.Equal(new[] { a, b }, view.SourcePaths);
                    Assert.Equal(2, view.Model!.Clips.Count);
                    Assert.Equal(new EditClip(0, S(30)) { Source = 1 }, view.Model.Clips[1]);
                    Assert.Equal(S(630), view.Model.Duration);

                    Assert.True(view.Undo());
                    Assert.Single(view.Model.Clips);
                    Assert.Single(view.Model.ExtraSources);
                    Assert.True(view.Redo());
                    Assert.Equal(2, view.Model.Clips.Count);

                    Assert.False(Bekle(view, view.AddSourceAsync(bozuk)));
                    Assert.Equal(2, view.Model.Clips.Count);
                    Assert.Equal(2, view.SourcePaths.Count);
                    Assert.Single(view.Model.ExtraSources);
                    Assert.Contains("bozuk.mp4", view.ExportStatusText);
                    Assert.Equal(Tema("StatusError"), view.TxtExportStatus.Theme);
                    Assert.True(view.TxtExportStatus.IsEffectivelyVisible);
                }
                finally
                {
                    DuzenleyiciKapanis.Kapat(pencere, view);
                }
            });
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [Fact]
    public void BirakilanDosyalarinIlkiAcilirKalaniSonaEklenir()
    {
        var klasor = Klasor("birak");
        try
        {
            var a = Sahte(klasor, "a.mp4");
            var b = Sahte(klasor, "b.mp4");
            var c = Sahte(klasor, "c.mp4");

            AppHost.Run(() =>
            {
                var view = Gorunum();
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    Bekle(view, view.TakeFilesAsync(new[] { a, b }));
                    Assert.Equal(new[] { a, b }, view.SourcePaths);
                    Assert.Equal(new[] { 0, 1 }, view.Model!.Clips.Select(k => k.Source));

                    Bekle(view, view.TakeFilesAsync(new[] { c }));
                    Assert.Equal(new[] { a, b, c }, view.SourcePaths);
                    Assert.Equal(new[] { 0, 1, 2 }, view.Model.Clips.Select(k => k.Source));
                }
                finally
                {
                    DuzenleyiciKapanis.Kapat(pencere, view);
                }
            });
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [Fact]
    public void KaydedilenCokKaynakliProjeAcilirEkKaynakDegismisseBayatSayilir()
    {
        var klasor = Klasor("proje");
        try
        {
            var a = Sahte(klasor, "a.mp4");
            var b = Sahte(klasor, "b.mp4");
            var dosya = Path.Combine(klasor, "is" + EditProject.Extension);

            AppHost.Run(() =>
            {
                var ilk = Gorunum();
                var pencere = new Window { Width = 900, Height = 600, Content = ilk };
                pencere.Show();
                try
                {
                    Bekle(ilk, ilk.OpenSourceAsync(a));
                    Assert.True(Bekle(ilk, ilk.AddSourceAsync(b)));
                    ilk.TimelineView.Playhead = S(100);
                    Assert.True(ilk.Split());
                    Assert.True(ilk.SaveProjectTo(dosya));
                }
                finally
                {
                    DuzenleyiciKapanis.Kapat(pencere, ilk);
                }

                using (var belge = JsonDocument.Parse(File.ReadAllText(dosya)))
                {
                    Assert.Equal(2, belge.RootElement.GetProperty("version").GetInt32());
                    Assert.Equal(b, Assert.Single(belge.RootElement.GetProperty("sources").EnumerateArray()).GetProperty("path").GetString());
                }

                var ikinci = Gorunum();
                pencere = new Window { Width = 900, Height = 600, Content = ikinci };
                pencere.Show();
                try
                {
                    Assert.True(Bekle(ikinci, ikinci.OpenProjectAsync(dosya)));
                    Assert.Equal(new[] { a, b }, ikinci.SourcePaths);
                    Assert.Equal(new[] { 0, 0, 1 }, ikinci.Model!.Clips.Select(k => k.Source));
                    Assert.Equal(S(100), ikinci.Model.Clips[0].SourceEnd);
                    Assert.Equal(Strings.Get("editor.project.restored"), ikinci.ExportStatusText);
                }
                finally
                {
                    DuzenleyiciKapanis.Kapat(pencere, ikinci);
                }

                File.SetLastWriteTimeUtc(b, File.GetLastWriteTimeUtc(b).AddMinutes(3));
                var bayat = Gorunum();
                pencere = new Window { Width = 900, Height = 600, Content = bayat };
                pencere.Show();
                try
                {
                    Bekle(bayat, bayat.OpenProjectAsync(dosya));
                    Assert.Equal(new[] { a }, bayat.SourcePaths);
                    Assert.Single(bayat.Model!.Clips);
                    Assert.Empty(bayat.Model.ExtraSources);
                    Assert.Equal(Strings.Get("editor.project.stale"), bayat.ExportStatusText);
                    Assert.Equal(Tema("StatusWarning"), bayat.TxtExportStatus.Theme);
                }
                finally
                {
                    DuzenleyiciKapanis.Kapat(pencere, bayat);
                }
            });
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [Fact]
    public void PencereKapanincaButunKaynaklarinTaramasiIptalEdilir()
    {
        var a = Path.Combine(Kok, "cok-kaynak-olmayan-a.mp4");
        var b = Path.Combine(Kok, "cok-kaynak-olmayan-b.mp4");

        var s = AppHost.Run(() =>
        {
            var dalga = new ConcurrentDictionary<string, CancellationToken>();
            var kare = new ConcurrentDictionary<string, CancellationToken>();
            var view = Gorunum();
            view.PeakReader = (yol, ct) =>
            {
                dalga[yol] = ct;
                return IptaleKadar<AudioPeaks>(ct);
            };
            view.KeyframeReader = (yol, ct) =>
            {
                kare[yol] = ct;
                return IptaleKadar<IReadOnlyList<double>>(ct);
            };
            var pencere = new Window { Width = 900, Height = 600, Content = view };
            pencere.Show();
            var kapandi = false;
            try
            {
                Bekle(view, view.OpenSourceAsync(a));
                var eklendi = Bekle(view, view.AddSourceAsync(b));
                DenetimSurucu.Pump(view.Player, () => dalga.Count == 2 && kare.Count == 2, 10);
                var basladi = dalga.ContainsKey(a) && dalga.ContainsKey(b) && kare.ContainsKey(a) && kare.ContainsKey(b);
                var acikken = DuzenleyiciKapanis.Birakti(view, 0.3);
                var acikkenIptal = dalga.Values.Concat(kare.Values).Count(ct => ct.IsCancellationRequested);
                pencere.Close();
                kapandi = true;
                var kapaninca = DuzenleyiciKapanis.Birakti(view, 10);
                var iptal = dalga.Values.Concat(kare.Values).Count(ct => ct.IsCancellationRequested);
                return (eklendi, basladi, acikken, acikkenIptal, kapaninca, iptal);
            }
            finally
            {
                if (!kapandi) pencere.Close();
            }
        });

        Assert.True(s.eklendi);
        Assert.True(s.basladi, "iki kaynagin ses dalgasi ve anahtar kare taramasi baslamadi");
        Assert.False(s.acikken);
        Assert.Equal(0, s.acikkenIptal);
        Assert.True(s.kapaninca, "pencere kapandi ama taramalar bitmedi");
        Assert.Equal(4, s.iptal);
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

    [FfmpegFact]
    public async Task FarkliOlcudeIkiKaynakTekDosyadaBirlesir()
    {
        var klasor = Klasor("canli");
        try
        {
            var a = Path.Combine(klasor, "a.mp4");
            var b = Path.Combine(klasor, "b.mp4");
            Ffmpeg("-hide_banner", "-y",
                "-f", "lavfi", "-i", "testsrc2=s=320x240:r=30:d=2",
                "-f", "lavfi", "-i", "sine=f=440:d=2",
                "-c:v", "libx264", "-preset", "ultrafast", "-g", "30", "-threads", "2", "-c:a", "aac", "-shortest", a);
            Ffmpeg("-hide_banner", "-y",
                "-f", "lavfi", "-i", "testsrc2=s=160x120:r=25:d=2",
                "-c:v", "libx264", "-preset", "ultrafast", "-g", "25", "-threads", "2", b);

            var ilk = await FfprobeClient.ProbeAsync(a);
            var ek = await FfprobeClient.ProbeAsync(b);
            Assert.True(ilk.HasAudio);
            Assert.False(ek.HasAudio);

            var model = EditTimeline.FromSource(EditTime.FromSeconds(ilk.DurationSeconds));
            Assert.Equal(1, model.AddSource(EditTime.FromSeconds(ek.DurationSeconds)));
            var cikti = Path.Combine(klasor, "birlesik.mp4");
            var plan = await EditExportRunner.PrepareAsync(new[] { a, b }, model, ExportMode.Smart, cikti, 8 * Gb);

            Assert.True(plan.MergeForcedFull);
            Assert.Equal(ExportMode.Full, plan.Effective);
            Assert.Equal(new[] { a, b }, Girdiler(plan));

            await EditExportRunner.RunAsync(plan, null);

            var sonuc = await FfprobeClient.ProbeAsync(cikti);
            Assert.InRange(sonuc.DurationSeconds, ilk.DurationSeconds + ek.DurationSeconds - 0.2, ilk.DurationSeconds + ek.DurationSeconds + 0.2);
            Assert.Equal(320, sonuc.Width);
            Assert.Equal(240, sonuc.Height);
            Assert.Equal(30.0, sonuc.Fps, 1);
            Assert.True(sonuc.HasAudio);
            Assert.False(Directory.Exists(plan.WorkDirectory));
            Assert.Empty(Directory.GetFiles(klasor, "vidshrink_partial_*"));
        }
        finally
        {
            Kapat(klasor);
        }
    }

    [Fact]
    public void CokKaynakMetinleri42DildeVar()
    {
        var anahtarlar = new[] { "editor.source.add", "editor.source.failed", "editor.export.merge-full" };

        Assert.Equal(42, Locales.Languages.Count);
        foreach (var dil in Locales.Languages)
        {
            var mevcut = Strings.KeysOf(dil).ToHashSet(StringComparer.Ordinal);
            foreach (var anahtar in anahtarlar)
            {
                Assert.True(mevcut.Contains(anahtar), $"{dil}: {anahtar} yok");
                var metin = Strings.GetIn(dil, anahtar);
                Assert.False(string.IsNullOrWhiteSpace(metin), $"{dil}: {anahtar} bos");
                Assert.Equal(anahtar == "editor.source.failed", metin.Contains("{0}", StringComparison.Ordinal));
            }
        }
    }
}
