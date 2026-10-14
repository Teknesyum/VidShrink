using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
/// DZA-P2: otomatik bolme ve parca adlari (<c>Core/Editing/AutoSplit</c>, <c>EditClip.Label</c>,
/// <c>EditMarker</c>, <c>EditProject</c>, <c>EditOutputName.Segments</c>, <c>AdlandirmaDeseni</c>,
/// <c>SceneDetector.Cuts</c>, <c>EditorView.Sahne</c>). Saf kol sinir hesabini (esit sayi, sabit
/// sure, ortalama bit hizindan hedef boyut), etiketin bolmede tasinmasini, etiket ve isaretcinin
/// proje gidis donusunu, parca adinin desenden kurulmasini ve sahne esiginin skoru suzmesini olcer.
/// Arayuz kolu sahte <c>KlipMotoru</c> ve sahte tarayiciyla; ffmpeg kosmaz.
/// Kanit <c>.calisma/worktree-agent-a9dcdb2a6fd5f1317/</c>, test siler.
/// </summary>
public sealed class DuzenleyiciOtomatikBolmeTests
{
    private const string Dal = "worktree-agent-a9dcdb2a6fd5f1317";

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

    private static (long, long)[] Araliklar(IEnumerable<EditClip> klipler) => klipler.Select(k => (k.SourceStart, k.SourceEnd)).ToArray();

    [Fact]
    public void EsitSayiSureyiParcalarinaBoler()
    {
        Assert.Equal(new[] { S(200), S(400) }, AutoSplit.Boundaries(S(600), AutoSplitMode.Count, 3));
        Assert.Equal(new[] { S(150), S(300), S(450) }, AutoSplit.Boundaries(S(600), AutoSplitMode.Count, 4));
        Assert.Empty(AutoSplit.Boundaries(S(600), AutoSplitMode.Count, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => AutoSplit.Boundaries(S(600), AutoSplitMode.Count, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AutoSplit.Boundaries(S(600), AutoSplitMode.Count, 2.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => AutoSplit.Boundaries(S(600), AutoSplitMode.Count, AutoSplit.MaxParts + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => AutoSplit.Boundaries(0, AutoSplitMode.Count, 2));
    }

    [Fact]
    public void SabitSureSonaKadarSinirKoyar()
    {
        Assert.Equal(new[] { S(250), S(500) }, AutoSplit.Boundaries(S(600), AutoSplitMode.Duration, 250));
        Assert.Equal(new[] { S(300) }, AutoSplit.Boundaries(S(600), AutoSplitMode.Duration, 300));
        Assert.Empty(AutoSplit.Boundaries(S(600), AutoSplitMode.Duration, 600));
        Assert.Empty(AutoSplit.Boundaries(S(600), AutoSplitMode.Duration, 900));
        Assert.Throws<ArgumentOutOfRangeException>(() => AutoSplit.Boundaries(S(600), AutoSplitMode.Duration, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AutoSplit.Boundaries(S(600), AutoSplitMode.Duration, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => AutoSplit.Boundaries(S(600), AutoSplitMode.Duration, 0.1));
    }

    [Fact]
    public void HedefBoyutOrtalamaBitHizindanSureyeDoner()
    {
        Assert.Equal(new[] { S(150), S(300), S(450) }, AutoSplit.Boundaries(S(600), AutoSplitMode.Size, 150, 600_000_000));
        Assert.Equal(new[] { S(300) }, AutoSplit.Boundaries(S(600), AutoSplitMode.Size, 600, 1_200_000_000));
        Assert.Empty(AutoSplit.Boundaries(S(600), AutoSplitMode.Size, 700, 600_000_000));
        Assert.Throws<ArgumentOutOfRangeException>(() => AutoSplit.Boundaries(S(600), AutoSplitMode.Size, 150, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AutoSplit.Boundaries(S(600), AutoSplitMode.Size, -1, 600_000_000));
    }

    [Fact]
    public void OtomatikSinirVarOlanBolmeyeGirerVeTekAdimdaGeriAlinir()
    {
        var model = EditTimeline.FromSource(S(600));
        Assert.Equal(2, model.SplitSource(AutoSplit.Boundaries(S(600), AutoSplitMode.Count, 3), SceneSplit.MinPieceTicks));
        Assert.Equal(new[] { (S(0), S(200)), (S(200), S(400)), (S(400), S(600)) }, Araliklar(model.Clips));
        Assert.Equal(S(600), model.Duration);
        Assert.True(model.Undo());
        Assert.Equal(new[] { (S(0), S(600)) }, Araliklar(model.Clips));
        Assert.False(model.Undo());
    }

    [Fact]
    public void EtiketKirpilirBosEtiketYokSayilir()
    {
        Assert.Null(new EditClip(0, 10) { Label = "   " }.Label);
        Assert.Null(new EditClip(0, 10) { Label = null }.Label);
        Assert.Equal("soru 3", new EditClip(0, 10) { Label = "  soru 3 " }.Label);
        Assert.NotEqual(new EditClip(0, 10) { Label = "a" }, new EditClip(0, 10));
    }

    [Fact]
    public void EtiketBolmedeHizdaVeSahneBolmesindeTasinir()
    {
        var model = new EditTimeline(new[] { new EditClip(0, S(60)) { Label = "giris" } }, S(60));
        Assert.True(model.Split(S(20)));
        Assert.All(model.Clips, k => Assert.Equal("giris", k.Label));
        Assert.True(model.SetSpeed(1, 2));
        Assert.Equal("giris", model.Clips[1].Label);
        Assert.Equal(1, model.SplitSource(new[] { S(40) }, SceneSplit.MinPieceTicks));
        Assert.All(model.Clips, k => Assert.Equal("giris", k.Label));
        Assert.True(model.RemoveSource(new[] { (S(5), S(6)) }));
        Assert.All(model.Clips, k => Assert.Equal("giris", k.Label));
    }

    [Fact]
    public void EtiketYazmaTekAdimdaGeriAlinir()
    {
        var model = new EditTimeline(new[] { new EditClip(0, S(10)), new EditClip(S(10), S(20)) }, S(20));
        Assert.True(model.SetLabel(new[] { 0, 1 }, " soru 3 "));
        Assert.All(model.Clips, k => Assert.Equal("soru 3", k.Label));
        Assert.False(model.SetLabel(new[] { 0, 1 }, "soru 3"));
        Assert.True(model.SetLabel(new[] { 1 }, ""));
        Assert.Null(model.Clips[1].Label);
        Assert.True(model.Undo());
        Assert.Equal("soru 3", model.Clips[1].Label);
        Assert.True(model.Undo());
        Assert.All(model.Clips, k => Assert.Null(k.Label));
        Assert.Throws<ArgumentOutOfRangeException>(() => model.SetLabel(new[] { 2 }, "x"));
    }

    [Fact]
    public void ProjeEtiketVeIsaretciGidisDonus()
    {
        var model = new EditTimeline(new[] { new EditClip(0, S(10)) { Label = "giris" }, new EditClip(S(10), S(20)) }, S(20));
        var isaretler = new[] { new EditMarker(S(9), null), new EditMarker(S(5), "soru 3") };
        var proje = EditProject.From(new SourceStamp("C:/k.mp4", 16, 1), model, markers: isaretler);
        var json = proje.ToJson();
        Assert.Contains("\"label\"", json, StringComparison.Ordinal);
        Assert.Contains("\"markers\"", json, StringComparison.Ordinal);

        var geri = EditProject.Parse(json);
        Assert.NotNull(geri);
        Assert.Equal(model.Clips, geri!.Clips);
        Assert.Equal(new[] { new EditMarker(S(5), "soru 3"), new EditMarker(S(9), null) }, geri.Markers);
        Assert.Equal(1, System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("version").GetInt32());
    }

    [Fact]
    public void EtiketsizProjeEskiBaytlariTasir()
    {
        var model = EditTimeline.FromSource(S(20));
        var json = EditProject.From(new SourceStamp("C:/k.mp4", 16, 1), model).ToJson();
        Assert.DoesNotContain("\"label\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"markers\"", json, StringComparison.Ordinal);
        Assert.Empty(EditProject.Parse(json)!.Markers);
    }

    [Fact]
    public void BozukIsaretciProjeyiDusurur()
    {
        var json = EditProject.From(new SourceStamp("C:/k.mp4", 16, 1), EditTimeline.FromSource(S(20)), markers: new[] { new EditMarker(S(1), "a") }).ToJson();
        Assert.NotNull(EditProject.Parse(json));
        Assert.Null(EditProject.Parse(json.Replace("\"time\": " + S(1), "\"time\": -5", StringComparison.Ordinal)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EditMarker(-1, null));
        Assert.Null(new EditMarker(0, "  ").Name);
    }

    [Fact]
    public void VarsayilanParcaDeseniEskiAdlariVerir()
    {
        var cikti = Path.Combine(Kanit, "tatil-duzenlenmis.mp4");
        var parcalar = new[] { new SegmentName(null, 0), new SegmentName(null, S(10)), new SegmentName(null, S(20)) };
        Assert.Equal(EditOutputName.Segments(cikti, 3, _ => false), EditOutputName.Segments(cikti, parcalar, null, _ => false));
        Assert.Equal(EditOutputName.Segments(cikti, 3, _ => false), EditOutputName.Segments(cikti, parcalar, EditOutputName.PartPattern, _ => false));
    }

    [Fact]
    public void EtiketVeZamanParcaAdinaGirer()
    {
        var cikti = Path.Combine(Kanit, "tatil-duzenlenmis.mp4");
        var parcalar = new[] { new SegmentName("giris", 0), new SegmentName(null, S(75)), new SegmentName("soru/3", S(3725)) };
        Assert.Equal(new[] { "tatil-duzenlenmis-01-giris.mp4", "tatil-duzenlenmis-02.mp4", "tatil-duzenlenmis-03-soru3.mp4" },
            EditOutputName.Segments(cikti, parcalar, null, _ => false).Select(Path.GetFileName));
        Assert.Equal(new[] { "giris_00h00m00s.mp4", "00h01m15s.mp4", "soru3_01h02m05s.mp4" },
            EditOutputName.Segments(cikti, parcalar, "{etiket}_{zaman}", _ => false).Select(Path.GetFileName));
        Assert.Equal(new[] { "tatil-duzenlenmis-01-giris.mp4", "tatil-duzenlenmis-02.mp4", "tatil-duzenlenmis-03-soru3.mp4" },
            EditOutputName.Segments(cikti, parcalar, "{hedef}", _ => false).Select(Path.GetFileName));
    }

    [Fact]
    public void AyniAdliParcalarNumaraAlirVarOlanaYazilmaz()
    {
        var cikti = Path.Combine(Kanit, "t.mp4");
        var parcalar = new[] { new SegmentName("a", 0), new SegmentName("a", S(10)) };
        Assert.Equal(new[] { "a-01.mp4", "a-02.mp4" }, EditOutputName.Segments(cikti, parcalar, "{etiket}", _ => false).Select(Path.GetFileName));
        var bos = new[] { new SegmentName(null, 0), new SegmentName(null, S(10)) };
        Assert.Equal(new[] { "t-01.mp4", "t-02.mp4" }, EditOutputName.Segments(cikti, bos, "{etiket}", _ => false).Select(Path.GetFileName));

        var dolu = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.Combine(Kanit, "t-01-a.mp4") };
        var ikinci = EditOutputName.Segments(cikti, new[] { new SegmentName("a", 0), new SegmentName("b", S(10)) }, null, dolu.Contains);
        Assert.Equal(new[] { "t-2-01-a.mp4", "t-2-02-b.mp4" }, ikinci.Select(Path.GetFileName));
        Assert.Throws<ArgumentOutOfRangeException>(() => EditOutputName.Segments(cikti, Array.Empty<SegmentName>(), null, _ => false));
    }

    [Fact]
    public void ParcaDeseniKendiKumesiyleDogrulanir()
    {
        Assert.Null(AdlandirmaDeseni.ParcaHata("{ad}-{parca}-{etiket}"));
        Assert.Null(AdlandirmaDeseni.ParcaHata("{zaman}_{tarih}"));
        Assert.Equal("settings-tab.output-name.bad-placeholder", AdlandirmaDeseni.ParcaHata("{hedef}"));
        Assert.Equal("settings-tab.output-name.bad-char", AdlandirmaDeseni.ParcaHata("a/{parca}"));
        Assert.Equal("settings-tab.output-name.empty", AdlandirmaDeseni.ParcaHata(" "));
        Assert.Equal("settings-tab.output-name.bad-placeholder", AdlandirmaDeseni.Hata("{parca}"));
        Assert.Equal(new[] { "ad", "parca", "etiket", "zaman", "tarih" }, AdlandirmaDeseni.ParcaYerTutuculari.ToArray());
    }

    [Fact]
    public void SahneEsigiSkorlariSuzer()
    {
        var skorlar = new[] { new SceneScore(0, 1), new SceneScore(2, 0.2), new SceneScore(4, 0.5), new SceneScore(1, 0.11) };
        Assert.Equal(new[] { 1d, 2, 4 }, SceneDetector.Cuts(skorlar, SceneMap.DefaultThreshold));
        Assert.Equal(new[] { 4d }, SceneDetector.Cuts(skorlar, 0.3));
        Assert.Empty(SceneDetector.Cuts(skorlar, 0.9));
    }

    [Fact]
    public void ArayuzEsitSayiylaBolerVeTekAdimdaGeriAlir()
    {
        Arayuz("oto-sayi", (view, model) =>
        {
            Assert.True(view.BtnAutoSplit.IsEnabled);
            view.CmbAutoSplit.SelectedIndex = 0;
            view.TxtAutoSplit.Text = "3";
            view.BtnAutoSplit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(new[] { (S(0), S(200)), (S(200), S(400)), (S(400), S(600)) }, Araliklar(model.Clips));
            Assert.Equal(string.Format(Strings.Culture, Strings.Get("editor.split.done"), 2), view.TxtSilenceStatus.Text);
            Assert.True(view.Undo());
            Assert.Equal(new[] { (S(0), S(600)) }, Araliklar(model.Clips));

            view.TxtAutoSplit.Text = "abc";
            Assert.Equal(0, view.AutoSplitClips());
            Assert.Equal(Strings.Get("editor.split.invalid"), view.TxtSilenceStatus.Text);
            Assert.Equal(new[] { (S(0), S(600)) }, Araliklar(model.Clips));
        });
    }

    [Fact]
    public void ArayuzSureVeBoyutKipleriBoler()
    {
        Arayuz("oto-boyut", (view, model) =>
        {
            view.CmbAutoSplit.SelectedIndex = 1;
            view.TxtAutoSplit.Text = "250";
            Assert.Equal(2, view.AutoSplitClips());
            Assert.Equal(new[] { (S(0), S(250)), (S(250), S(500)), (S(500), S(600)) }, Araliklar(model.Clips));
            Assert.True(view.Undo());

            view.CmbAutoSplit.SelectedIndex = 2;
            view.TxtAutoSplit.Text = "200";
            Assert.Equal(2, view.AutoSplitClips());
            Assert.Equal(new[] { (S(0), S(200)), (S(200), S(400)), (S(400), S(600)) }, Araliklar(model.Clips));
        });
    }

    [Fact]
    public void ArayuzSahneEsigiTarayiciyaGider()
    {
        Arayuz("oto-esik", (view, model) =>
        {
            Assert.Equal(SceneMap.DefaultThreshold.ToString("0.###", Strings.Culture), view.TxtSceneThreshold.Text);
            var esik = 0d;
            view.SceneScanner = (_, _, e, _, _) =>
            {
                esik = e;
                return Task.FromResult<IReadOnlyList<double>>(new[] { 300d });
            };

            view.TxtSceneThreshold.Text = 0.3.ToString(Strings.Culture);
            var bolme = view.SplitScenesAsync();
            DenetimSurucu.Pump(view.Player, () => bolme.IsCompleted, 10);
            Assert.Equal(1, bolme.Result);
            Assert.Equal(0.3, esik, 6);

            esik = 0;
            view.TxtSceneThreshold.Text = "2";
            var bozuk = view.SplitScenesAsync();
            DenetimSurucu.Pump(view.Player, () => bozuk.IsCompleted, 10);
            Assert.Equal(0, bozuk.Result);
            Assert.Equal(0, esik);
            Assert.Equal(Strings.Get("editor.split.invalid"), view.TxtSilenceStatus.Text);
        });
    }

    [Fact]
    public void ArayuzEtiketKlibeYazilirVeParcaAdinaGirer()
    {
        Arayuz("oto-etiket", (view, model) =>
        {
            Assert.Equal(2, view.AutoSplitClips(AutoSplitMode.Count, 3));
            view.Timeline.Playhead = 0;
            view.TxtClipLabel.Text = "giris";
            Assert.True(view.CommitClipLabel());
            Assert.Equal("giris", model.Clips[view.ActiveIndex].Label);

            var cikti = Path.Combine(Kanit, "kaynak-duzenlenmis.mp4");
            var adlar = view.PartNames(cikti).Select(Path.GetFileName).ToArray();
            Assert.Equal(3, adlar.Length);
            Assert.Contains("kaynak-duzenlenmis-01-giris.mp4", adlar);

            view.TxtPartName.Text = "{etiket}_{parca}";
            Assert.Equal(string.Empty, view.TxtPartNameError.Text ?? string.Empty);
            Assert.Contains("giris_01.mp4", view.PartNames(cikti).Select(Path.GetFileName));
            view.TxtPartName.Text = "{hedef}";
            Assert.Equal(Strings.Get("editor.export.part-name.hint"), view.TxtPartNameError.Text);

            Assert.True(view.Undo());
            Assert.All(model.Clips, k => Assert.Null(k.Label));
        });
    }

    [Fact]
    public void ArayuzIsaretciAdiProjeyeYazilirVeGeriGelir()
    {
        Arayuz("oto-isaret", (view, model) =>
        {
            view.Timeline.Playhead = S(10);
            view.TxtMarkerName.Text = "soru 3";
            Assert.True(view.CommitMarkerName());
            Assert.Equal(new[] { new EditMarker(S(10), "soru 3") }, view.Timeline.MarkerList);

            var yol = Path.Combine(Kanit, "oto-isaret", "proje" + EditProject.Extension);
            Assert.True(view.SaveProjectTo(yol));
            Assert.Equal(new[] { new EditMarker(S(10), "soru 3") }, EditProject.Read(yol)!.Markers);

            view.Timeline.SetMarkers(Array.Empty<EditMarker>());
            Assert.Empty(view.Timeline.Markers);
            var acilis = view.OpenProjectAsync(yol);
            DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted, 10);
            Assert.True(acilis.Result);
            Assert.Equal(new[] { new EditMarker(S(10), "soru 3") }, view.Timeline.MarkerList);

            view.Timeline.AddMarker(S(10));
            Assert.Empty(view.Timeline.MarkerList);

            view.Timeline.Playhead = S(20);
            view.TxtMarkerName.Text = "  ";
            Assert.False(view.CommitMarkerName());
            Assert.Empty(view.Timeline.MarkerList);
        });
    }

    [Fact]
    public void ArayuzYalnizIsaretciTasiyanKayitAcilistaGeriGelir()
    {
        var isaretler = new[] { new EditMarker(S(10), "soru 3"), new EditMarker(S(30), null) };
        Arayuz("oto-acilis", (view, model) =>
        {
            Assert.Equal(new[] { (S(0), S(600)) }, Araliklar(model.Clips));
            Assert.Equal(isaretler, view.Timeline.MarkerList);
        }, dosya =>
        {
            var depo = new EditProjectStore(Path.Combine(Path.GetDirectoryName(dosya)!, "kayit"));
            Assert.True(depo.Save(EditProject.From(SourceStamp.Of(dosya)!, EditTimeline.FromSource(S(600)), markers: isaretler)));
            return depo;
        });
    }

    private static void Arayuz(string ad, Action<EditorView, EditTimeline> govde, Func<string, EditProjectStore>? depo = null)
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
                    KnownInfo = yol => new MediaInfo { FilePath = yol, FileSizeBytes = 600_000_000, DurationSeconds = 600, Width = 320, Height = 240, Fps = 30, VideoCodec = "h264", TotalBitrateBps = 8_000_000 }
                };
                if (depo is not null) view.Projects = depo(dosya);
                view.Player.EngineFactory = () => motor;
                view.OverlayRoot = Path.Combine(klasor, "katman");
                var pencere = new Window { Width = 900, Height = 600, Content = view };
                pencere.Show();
                try
                {
                    var acilis = view.OpenSourceAsync(dosya);
                    DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted, 10);
                    acilis.GetAwaiter().GetResult();
                    view.BtnSilence.IsChecked = true;
                    view.BtnSilence.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    govde(view, view.Model!);
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
