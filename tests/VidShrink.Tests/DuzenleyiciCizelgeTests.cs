using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using VidShrink.App;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// D3 (028) kabul şartlarının ölçülebilen kısmı: yakalama eşiği üç zoom kademesinde
/// 8±1 px, 200 kesimde yalnız görünür aralık kurulur, işlem/geri al/yinele eşitliği,
/// oynatma başının erişilebilirlik rolü ve düzenleyicide sabit renk/ölçü olmaması.
/// </summary>
public sealed class DuzenleyiciCizelgeTests
{
    private const double Genislik = 1000;

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static EditorTimeline Cizelge(EditTimeline model)
    {
        var cizelge = new EditorTimeline();
        cizelge.Measure(new Size(Genislik, double.PositiveInfinity));
        cizelge.Show(model);
        cizelge.Measure(new Size(Genislik, double.PositiveInfinity));
        cizelge.Arrange(new Rect(0, 0, Genislik, cizelge.DesiredSize.Height));
        return cizelge;
    }

    private static EditTimeline UcKesim()
    {
        var model = EditTimeline.FromSource(S(60));
        model.Split(S(20));
        model.Split(S(40));
        return model;
    }

    [Fact]
    public void YakalamaUcZoomKademesindeSekizPikselCevresinde()
    {
        var olcum = AppHost.Run(() =>
        {
            var cizelge = Cizelge(UcKesim());
            var sinir = S(20);
            var kademeler = new[]
            {
                cizelge.MinPixelsPerTick,
                Math.Sqrt(cizelge.MinPixelsPerTick * cizelge.MaxPixelsPerTick),
                cizelge.MaxPixelsPerTick
            };

            var sonuc = new List<(double Esik, double EsikTick, long? Yedi, long? On)>();
            foreach (var ppt in kademeler)
            {
                cizelge.PixelsPerTick = ppt;
                cizelge.ViewStart = sinir - (long)(cizelge.TrackWidth / 2 / cizelge.PixelsPerTick);
                var p = cizelge.PixelsPerTick;
                sonuc.Add((
                    cizelge.SnapThresholdTicks * p,
                    cizelge.SnapThresholdTicks,
                    cizelge.SnapTarget(sinir + (long)Math.Round(7 / p), playhead: false),
                    cizelge.SnapTarget(sinir + (long)Math.Round(10 / p), playhead: false)));
            }

            return (sinir, sonuc);
        });

        Assert.Equal(3, olcum.sonuc.Count);
        foreach (var kademe in olcum.sonuc)
        {
            Assert.InRange(kademe.Esik, 7, 9);
            Assert.Equal(olcum.sinir, kademe.Yedi);
            Assert.Null(kademe.On);
        }

        Assert.Equal(3, olcum.sonuc.Select(k => k.EsikTick).Distinct().Count());
    }

    [Fact]
    public void IkiYuzKesimdeYalnizGorunurKlipKurulur()
    {
        var (kurulan, cocuk, gorunur) = AppHost.Run(() =>
        {
            var model = new EditTimeline(Enumerable.Range(0, 200).Select(i => new EditClip(S(i), S(i + 1))));
            var cizelge = Cizelge(model);
            cizelge.PixelsPerTick = cizelge.TrackWidth / S(10);
            cizelge.ViewStart = S(50);
            var gorunur = model.Clips.Where((_, i) => i >= 50 && i < 60).Count();
            return (cizelge.Realized.Count, cizelge.Children.OfType<EditorClip>().Count(), gorunur);
        });

        Assert.Equal(10, gorunur);
        Assert.InRange(kurulan, gorunur, 12);
        Assert.InRange(cocuk, gorunur, 12);
    }

    [Fact]
    public void OnIslemOnGeriAlOnYineleEsitlik()
    {
        var (adimlar, geriAdimlar, ileriAdimlar, cizelgedeki) = AppHost.Run(() =>
        {
            var view = new EditorView();
            view.ShowTimeline(EditTimeline.FromSource(S(120)), 30);
            var zar = new Random(20260927);
            var adimlar = new List<List<EditClip>> { view.Model!.Clips.ToList() };
            var yapilan = 0;
            for (var deneme = 0; yapilan < 10 && deneme < 500; deneme++)
            {
                var model = view.Model!;
                var sec = zar.Next(model.Clips.Count);
                view.TimelineView.SelectedIndex = sec;
                var basarili = (yapilan % 5) switch
                {
                    0 => Bol(view, zar),
                    1 => view.SetSpeed(new[] { 0.5m, 1.5m, 2m, 3m }[zar.Next(4)]),
                    2 => view.Reverse(),
                    3 => view.DeleteSelected(),
                    _ => Aralik(view, zar)
                };
                if (!basarili) continue;
                yapilan++;
                adimlar.Add(view.Model!.Clips.ToList());
            }

            Assert.Equal(10, yapilan);
            var geriAdimlar = new List<List<EditClip>>();
            for (var i = 0; i < 10; i++)
            {
                Assert.True(view.Undo());
                geriAdimlar.Add(view.Model!.Clips.ToList());
            }
            Assert.False(view.Undo());
            var ileriAdimlar = new List<List<EditClip>>();
            for (var i = 0; i < 10; i++)
            {
                Assert.True(view.Redo());
                ileriAdimlar.Add(view.Model!.Clips.ToList());
            }
            return (adimlar, geriAdimlar, ileriAdimlar, view.TimelineView.Model!.Clips.ToList());
        });

        Assert.NotEqual(adimlar[0], adimlar[10]);
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(adimlar[9 - i], geriAdimlar[i]);
            Assert.Equal(adimlar[i + 1], ileriAdimlar[i]);
        }
        Assert.Equal(adimlar[10], cizelgedeki);
    }

    private static bool Bol(EditorView view, Random zar)
    {
        view.TimelineView.Playhead = (long)(zar.NextDouble() * view.Model!.Duration);
        return view.Split();
    }

    private static bool Aralik(EditorView view, Random zar)
    {
        var sure = view.Model!.Duration;
        var a = (long)(zar.NextDouble() * sure * 0.8);
        var b = a + (long)(zar.NextDouble() * sure * 0.15) + 1;
        view.TimelineView.Playhead = a;
        view.MarkIn();
        view.TimelineView.Playhead = b;
        view.MarkOut();
        return view.DeleteRange();
    }

    [Fact]
    public void OynatmaBasiKaydiriciRoluyleOkunur()
    {
        var (tur, sinif, ad, en, enAz, enCok, deger, beklenenAd) = AppHost.Run(() =>
        {
            var cizelge = Cizelge(UcKesim());
            cizelge.Playhead = S(30);
            var peer = ControlAutomationPeer.CreatePeerForElement(cizelge.Overlay);
            var aralik = Assert.IsAssignableFrom<IRangeValueProvider>(peer);
            return (peer.GetAutomationControlType(), peer.GetClassName(), peer.GetName(), peer.IsControlElement(),
                aralik.Minimum, aralik.Maximum, aralik.Value,
                Strings.Get("editor.playhead") + " " + cizelge.Overlay.TimeText);
        });

        Assert.Equal(AutomationControlType.Slider, tur);
        Assert.Equal(nameof(EditorPlayhead), sinif);
        Assert.Equal(beklenenAd, ad);
        Assert.True(en);
        Assert.Equal(0, enAz);
        Assert.Equal(60, enCok, 3);
        Assert.Equal(30, deger, 3);
    }

    private static string AppKoku => Path.Combine(TipSources.Root, "src", "VidShrink.App");

    private static IEnumerable<string> DuzenleyiciKaynaklari()
    {
        yield return Path.Combine(AppKoku, "Themes", "Editor.axaml");
        foreach (var dosya in Directory.GetFiles(Path.Combine(AppKoku, "Editing")).OrderBy(d => d, StringComparer.Ordinal))
            yield return dosya;
    }

    private static readonly (string Ad, Regex Desen)[] YasakDesenler =
    {
        ("hex renk", new Regex("#[0-9A-Fa-f]{3,8}\\b")),
        ("Colors.", new Regex("\\bColors\\.")),
        ("Brushes.", new Regex("\\bBrushes\\.")),
        ("Color.Parse/FromRgb", new Regex("\\bColor\\.(Parse|From\\w+)")),
        ("new SolidColorBrush", new Regex("new\\s+SolidColorBrush")),
        ("new Thickness(sayı)", new Regex("new\\s+Thickness\\(\\s*\\d")),
        ("new CornerRadius(sayı)", new Regex("new\\s+CornerRadius\\(\\s*\\d")),
        ("TimeSpan.From", new Regex("TimeSpan\\.From(Milli)?[A-Z]\\w*\\(\\s*\\d")),
        ("xaml ölçü", new Regex("\\b(Width|Height|Margin|Padding|Spacing|CornerRadius|BorderThickness|FontSize|Opacity)=\"-?\\d")),
        ("C# ölçü ataması", new Regex("\\b(Width|Height|Margin|Padding|FontSize|Opacity)\\s*=\\s*-?\\d")),
        ("xaml süre", new Regex("Duration=\"\\d"))
    };

    [Fact]
    public void DuzenleyicideSabitRenkYaDaOlcuYok()
    {
        var ihlaller = new List<string>();
        foreach (var dosya in DuzenleyiciKaynaklari())
        {
            var satirlar = File.ReadAllLines(dosya);
            for (var i = 0; i < satirlar.Length; i++)
                foreach (var (ad, desen) in YasakDesenler)
                    if (desen.IsMatch(satirlar[i]))
                        ihlaller.Add($"{Path.GetFileName(dosya)}:{i + 1} [{ad}] {satirlar[i].Trim()}");
        }

        Assert.True(ihlaller.Count == 0, string.Join(Environment.NewLine, ihlaller));
    }

    [Fact]
    public void DuzenleyiciBelirtecleriYorumdakiEslemeyleAyni()
    {
        var metin = File.ReadAllText(Path.Combine(AppKoku, "Themes", "Editor.axaml"));
        var yorum = Regex.Matches(metin, "^\\s+(Editor\\w+)\\s+<-\\s+(\\w+)", RegexOptions.Multiline)
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        var tanim = Regex.Matches(metin, "<StaticResource x:Key=\"(Editor\\w+)\" ResourceKey=\"(\\w+)\"/>")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);

        Assert.NotEmpty(tanim);
        foreach (var (anahtar, kaynak) in tanim)
        {
            Assert.True(yorum.TryGetValue(anahtar, out var yazilan), anahtar + " için <- satırı yok");
            Assert.Equal(kaynak, yazilan);
        }

        Assert.Empty(Regex.Matches(metin, "<x:(Double|Int32|String|TimeSpan)\\b"));
        Assert.Empty(Regex.Matches(metin, "<(Thickness|CornerRadius|Color|SolidColorBrush)\\b"));
    }

    [Fact]
    public void DuzenleyicininCagirdigiHerAnahtarCozulur()
    {
        var anahtarlar = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var dosya in DuzenleyiciKaynaklari())
        {
            var metin = File.ReadAllText(dosya);
            foreach (Match m in Regex.Matches(metin, "(?:EditorTokens\\.\\w+\\(this,\\s*|Metric\\(|Paint\\()\"(\\w+)\"|\\{StaticResource\\s+(\\w+)\\}|ResourceKey=\"(\\w+)\""))
                anahtarlar.Add(m.Groups.Values.Skip(1).First(g => g.Success).Value);
        }

        var cozulmeyen = AppHost.Run(() => anahtarlar
            .Where(k => !Application.Current!.TryFindResource(k, Application.Current!.ActualThemeVariant, out var v) || v is null)
            .ToList());

        Assert.Contains("EditorSnapThresholdPx", anahtarlar);
        Assert.Contains("NeonPink", anahtarlar);
        Assert.Empty(cozulmeyen);
    }

    [Fact]
    public void YakalamaKapaliykenHedefVeCizgiYok()
    {
        var (acik, kapali, geri) = AppHost.Run(() =>
        {
            var cizelge = Cizelge(UcKesim());
            var sinir = S(20);
            var x = cizelge.TimeToX(sinir) + 3;

            (long? Hedef, long? Cizgi, long Bas) Olc()
            {
                var hedef = cizelge.SnapTarget(cizelge.XToTime(x), playhead: false);
                cizelge.ScrubTo(x, false);
                var sonuc = (hedef, cizelge.SnapLine, cizelge.Playhead);
                cizelge.ScrubTo(x, true);
                return sonuc;
            }

            var acik = Olc();
            cizelge.SnapEnabled = false;
            var kapali = Olc();
            cizelge.SnapEnabled = true;
            return (acik, kapali, Olc());
        });

        Assert.Equal(S(20), acik.Hedef);
        Assert.Equal(S(20), acik.Cizgi);
        Assert.Equal(S(20), acik.Bas);
        Assert.Null(kapali.Hedef);
        Assert.Null(kapali.Cizgi);
        Assert.NotEqual(S(20), kapali.Bas);
        Assert.Equal(acik, geri);
    }

    [Fact]
    public void SigdirmaTumCizelgeyiGosterir()
    {
        var (ppt, enAz, bas, son, sure) = AppHost.Run(() =>
        {
            var cizelge = Cizelge(UcKesim());
            cizelge.PixelsPerTick = cizelge.MaxPixelsPerTick;
            cizelge.ViewStart = S(30);
            Assert.True(cizelge.ViewStart > 0);
            cizelge.ZoomToFit();
            return (cizelge.PixelsPerTick, cizelge.MinPixelsPerTick, cizelge.ViewStart, cizelge.ViewEnd, cizelge.Model!.Duration);
        });

        Assert.Equal(enAz, ppt);
        Assert.Equal(0, bas);
        Assert.True(son >= sure);
    }

    [Fact]
    public void DuzenlemeNoktalariArasindaGezinir()
    {
        var olcum = AppHost.Run(() =>
        {
            var cizelge = Cizelge(UcKesim());
            var sonuc = new List<(long? Onceki, long? Sonraki)>();
            foreach (var bas in new[] { 0, S(10), S(20), S(59), S(60) })
            {
                cizelge.Playhead = bas;
                sonuc.Add((cizelge.PrevEditPoint(), cizelge.NextEditPoint()));
            }
            return sonuc;
        });

        Assert.Equal(new (long?, long?)[] { (null, S(20)), (0, S(20)), (0, S(40)), (S(40), S(60)), (S(40), null) }, olcum);
    }

    [Fact]
    public void OynatmaBasiSayfaSayfaIzlenir()
    {
        var olcum = AppHost.Run(() =>
        {
            var cizelge = Cizelge(UcKesim());
            cizelge.PixelsPerTick = cizelge.TrackWidth / S(10);
            cizelge.ViewStart = 0;
            var sonuc = new List<(bool Kaydi, long Bas, long Son)>();
            foreach (var bas in new[] { S(5), S(12), S(15), S(3) })
            {
                cizelge.Playhead = bas;
                sonuc.Add((cizelge.FollowPlayhead(), cizelge.ViewStart, cizelge.ViewEnd));
            }
            return sonuc;
        });

        Assert.Equal((false, 0L), (olcum[0].Kaydi, olcum[0].Bas));
        Assert.Equal((true, S(12)), (olcum[1].Kaydi, olcum[1].Bas));
        Assert.Equal((false, S(12)), (olcum[2].Kaydi, olcum[2].Bas));
        Assert.True(olcum[3].Kaydi);
        Assert.InRange(S(3), olcum[3].Bas, olcum[3].Son);
    }

    private static void Tikla(EditorTimeline cizelge, long zaman, KeyModifiers ek)
    {
        var nokta = new Point(cizelge.TimeToX(zaman), cizelge.VideoTop + 2);
        var isaretci = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        cizelge.RaiseEvent(new PointerPressedEventArgs(cizelge, isaretci, cizelge, nokta, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), ek));
        cizelge.RaiseEvent(new PointerReleasedEventArgs(cizelge, isaretci, cizelge, nokta, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), ek, MouseButton.Left));
    }

    [Fact]
    public void CtrlVeShiftTikiCokluSecimKurar()
    {
        var olcum = AppHost.Run(() =>
        {
            var cizelge = new EditorTimeline();
            var pencere = new Window { Width = Genislik, Height = 400, Content = cizelge };
            pencere.Show();
            cizelge.Show(UcKesim());
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var sonuc = new List<(int[] Secili, int[] Boyali)>();
            foreach (var (zaman, ek) in new[] { (S(10), KeyModifiers.None), (S(50), KeyModifiers.Shift), (S(30), KeyModifiers.Control), (S(30), KeyModifiers.Control), (S(50), KeyModifiers.Control), (S(30), KeyModifiers.None) })
            {
                Tikla(cizelge, zaman, ek);
                sonuc.Add((cizelge.SelectedIndices.ToArray(), cizelge.Realized.Where(c => c.Classes.Contains("selected")).Select(c => c.Index).OrderBy(i => i).ToArray()));
            }
            var adet = cizelge.Model!.Clips.Count;
            pencere.Close();
            return (sonuc, adet);
        });

        var beklenen = new[] { new[] { 0 }, new[] { 0, 1, 2 }, new[] { 0, 2 }, new[] { 0, 1, 2 }, new[] { 0, 1 }, new[] { 1 } };
        Assert.Equal(beklenen, olcum.sonuc.Select(s => s.Secili));
        Assert.Equal(beklenen, olcum.sonuc.Select(s => s.Boyali));
        Assert.Equal(3, olcum.adet);
    }

    [Fact]
    public void CokluSecimTekAdimdaSilinirHepsiSecilinceCizelgeBosalir()
    {
        var (coklu, cokluSure, cokluGeri, bos, bosSure, bosGeri) = AppHost.Run(() =>
        {
            var view = new EditorView();
            view.ShowTimeline(UcKesim(), 30);
            var t = view.TimelineView;
            t.SelectedIndex = 0;
            t.ToggleSelection(2);
            Assert.True(view.DeleteSelected());
            var coklu = view.Model!.Clips.ToArray();
            var cokluSure = view.Model.Duration;
            Assert.True(view.Undo());
            var cokluGeri = view.Model.Clips.ToArray();

            t.SelectAll();
            Assert.True(view.DeleteSelected());
            var bos = view.Model.Clips.Count;
            var bosSure = view.Model.Duration;
            Assert.True(view.Undo());
            return (coklu, cokluSure, cokluGeri, bos, bosSure, view.Model.Clips.ToArray());
        });

        Assert.Equal(new[] { new EditClip(S(20), S(40)) }, coklu);
        Assert.Equal(S(20), cokluSure);
        Assert.Equal(UcKesim().Clips, cokluGeri);
        Assert.Equal(0, bos);
        Assert.Equal(0, bosSure);
        Assert.Equal(UcKesim().Clips, bosGeri);
    }
}
