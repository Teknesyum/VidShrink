using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using VidShrink.App.Editing;
using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Dalga 2B çizelge etkileşimi ham işaretçi olaylarıyla: kenar sürükleme kaynağın ve komşunun
/// sınırında durur ve tek Geri Al ile döner, jilet tıkı klibi böler, kenardan uzak basış eskisi
/// gibi seçip taşır, kenarda imleç SizeWestEast olur.
/// </summary>
public sealed class DuzenleyiciKirpmaTests
{
    private const double Genislik = 1000;

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static EditTimeline IkiParca() =>
        new(new[] { new EditClip(S(10), S(20)), new EditClip(S(30), S(40)) }, S(60));

    private static EditTimeline UcKesim()
    {
        var model = EditTimeline.FromSource(S(60));
        model.Split(S(20));
        model.Split(S(40));
        return model;
    }

    private static (EditorTimeline Cizelge, Window Pencere) Kur(EditTimeline model)
    {
        var cizelge = new EditorTimeline();
        var pencere = new Window { Width = Genislik, Height = 400, Content = cizelge };
        pencere.Show();
        cizelge.Fps = 30;
        cizelge.Show(model);
        Dispatcher.UIThread.RunJobs();
        return (cizelge, pencere);
    }

    private static Point Nokta(EditorTimeline cizelge, double x) => new(x, cizelge.VideoTop + 2);

    private static void Bas(EditorTimeline cizelge, IPointer isaretci, Point nokta) =>
        cizelge.RaiseEvent(new PointerPressedEventArgs(cizelge, isaretci, cizelge, nokta, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));

    private static void Kaydir(EditorTimeline cizelge, IPointer isaretci, Point nokta, bool basili) =>
        cizelge.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, cizelge, isaretci, cizelge, nokta, 0,
            new PointerPointProperties(basili ? RawInputModifiers.LeftMouseButton : RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None));

    private static void Birak(EditorTimeline cizelge, IPointer isaretci, Point nokta) =>
        cizelge.RaiseEvent(new PointerReleasedEventArgs(cizelge, isaretci, cizelge, nokta, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));

    private static Pointer Isaretci() => new(Pointer.GetNextFreeId(), PointerType.Mouse, true);

    private static void Surukle(EditorTimeline cizelge, double basX, double bitisX, Action<EditorTimeline>? ortada = null)
    {
        var isaretci = Isaretci();
        Bas(cizelge, isaretci, Nokta(cizelge, basX));
        Kaydir(cizelge, isaretci, Nokta(cizelge, (basX + bitisX) / 2), true);
        Kaydir(cizelge, isaretci, Nokta(cizelge, bitisX), true);
        ortada?.Invoke(cizelge);
        Birak(cizelge, isaretci, Nokta(cizelge, bitisX));
    }

    [Fact]
    public void KenarSuruklemeSinirdaDururVeTekGeriAlIleDoner()
    {
        var olcum = AppHost.Run(() =>
        {
            var model = IkiParca();
            var ilk = model.Clips.ToArray();
            var (cizelge, pencere) = Kur(model);
            string? etiket = null;
            GhostBox? hayalet = null;
            var basX = cizelge.TimeToX(S(10));
            Surukle(cizelge, basX, 0, c => { etiket = c.TrimLabel; hayalet = c.Ghost; });
            var basSonra = model.Clips.ToArray();
            var ilkGeriAl = model.Undo();
            var geriAlinmis = model.Clips.ToArray();
            var ikinciGeriAl = model.Undo();

            cizelge.Refresh();
            var sonX = cizelge.TimeToX(S(10)) - 2;
            Surukle(cizelge, sonX, Genislik - 1);
            var kuyrukSonra = model.Clips.ToArray();
            var kuyrukIlkGeriAl = model.Undo();
            var kuyrukGeriAlinmis = model.Clips.ToArray();
            var kuyrukIkinciGeriAl = model.Undo();
            var surukluyorMu = cizelge.Trimming;
            pencere.Close();
            return (ilk, basSonra, ilkGeriAl, geriAlinmis, ikinciGeriAl, etiket, hayalet, kuyrukSonra, kuyrukIlkGeriAl, kuyrukGeriAlinmis, kuyrukIkinciGeriAl, surukluyorMu);
        });

        Assert.Equal(new EditClip(S(20), S(40)), olcum.basSonra[1]);
        Assert.Equal(olcum.ilk[0], olcum.basSonra[0]);
        Assert.True(olcum.ilkGeriAl);
        Assert.Equal(olcum.ilk, olcum.geriAlinmis);
        Assert.False(olcum.ikinciGeriAl);
        Assert.Equal("-00:00:10:00", olcum.etiket);
        Assert.NotNull(olcum.hayalet);

        Assert.Equal(new EditClip(S(10), S(30)), olcum.kuyrukSonra[0]);
        Assert.Equal(olcum.ilk[1], olcum.kuyrukSonra[1]);
        Assert.True(olcum.kuyrukIlkGeriAl);
        Assert.Equal(olcum.ilk, olcum.kuyrukGeriAlinmis);
        Assert.False(olcum.kuyrukIkinciGeriAl);
        Assert.False(olcum.surukluyorMu);
    }

    [Fact]
    public void TersKlipteBasKenariKaynaginSonunuTasir()
    {
        var olcum = AppHost.Run(() =>
        {
            var model = new EditTimeline(new[] { new EditClip(S(10), S(20), 1m, true) }, S(60));
            var (cizelge, pencere) = Kur(model);
            var bas = cizelge.TimeToX(0);
            Surukle(cizelge, bas, bas + 2 * S(1) * cizelge.PixelsPerTick);
            var sonra = model.Clips[0];
            pencere.Close();
            return sonra;
        });

        Assert.Equal(new EditClip(S(10), S(18), 1m, true), olcum);
    }

    [Fact]
    public void JiletTikiKlibiBoler()
    {
        var olcum = AppHost.Run(() =>
        {
            var model = UcKesim();
            var (cizelge, pencere) = Kur(model);
            cizelge.Tool = EditorTool.Razor;
            var once = model.Clips.Count;
            var isaretci = Isaretci();
            var nokta = Nokta(cizelge, cizelge.TimeToX(S(5)));
            Kaydir(cizelge, isaretci, nokta, false);
            var imlec = cizelge.CursorKind;
            Bas(cizelge, isaretci, nokta);
            Birak(cizelge, isaretci, nokta);
            var sonra = model.Clips.Count;
            var sinir = model.ClipStart(1);
            var tekGeriAl = model.Undo() && model.Clips.Count == once;
            pencere.Close();
            return (once, sonra, sinir, imlec, tekGeriAl);
        });

        Assert.Equal(olcum.once + 1, olcum.sonra);
        Assert.Equal(S(5), olcum.sinir);
        Assert.Equal(StandardCursorType.Cross, olcum.imlec);
        Assert.True(olcum.tekGeriAl);
    }

    [Fact]
    public void KenardanUzakBasisSecerVeTasir()
    {
        var olcum = AppHost.Run(() =>
        {
            var model = UcKesim();
            var ilk = model.Clips.ToArray();
            var (cizelge, pencere) = Kur(model);
            (int From, int To)? istek = null;
            cizelge.MoveRequested += (from, to) => istek = (from, to);
            var secili = -1;
            Surukle(cizelge, cizelge.TimeToX(S(10)), cizelge.TimeToX(S(55)), c => secili = c.SelectedIndex);
            var sonra = model.Clips.ToArray();
            pencere.Close();
            return (ilk, sonra, istek, secili);
        });

        Assert.Equal(0, olcum.secili);
        Assert.Equal((0, 2), olcum.istek);
        Assert.Equal(olcum.ilk, olcum.sonra);
    }

    [Fact]
    public void KenardaImlecSizeWestEastOlur()
    {
        var olcum = AppHost.Run(() =>
        {
            var model = UcKesim();
            var (cizelge, pencere) = Kur(model);
            var isaretci = Isaretci();
            var sonuc = new List<StandardCursorType>();
            foreach (var x in new[] { cizelge.TimeToX(S(20)) + 2, cizelge.TimeToX(S(10)), cizelge.TimeToX(S(40)) - 2, cizelge.TimeToX(S(30)) })
            {
                Kaydir(cizelge, isaretci, Nokta(cizelge, x), false);
                sonuc.Add(cizelge.CursorKind);
            }

            cizelge.Tool = EditorTool.Ripple;
            Kaydir(cizelge, isaretci, Nokta(cizelge, cizelge.TimeToX(S(20)) - 2), false);
            sonuc.Add(cizelge.CursorKind);
            pencere.Close();
            return sonuc;
        });

        Assert.Equal(new[]
        {
            StandardCursorType.SizeWestEast, StandardCursorType.Arrow,
            StandardCursorType.SizeWestEast, StandardCursorType.Arrow,
            StandardCursorType.SizeWestEast
        }, olcum);
    }

    [Fact]
    public void CetvelEtiketleriCakismazVeYakindaKareHanesiGosterir()
    {
        var olcum = AppHost.Run(() =>
        {
            var (cizelge, pencere) = Kur(UcKesim());
            var sonuc = new List<(double Ppt, IReadOnlyList<(double X, string Text, double Width)> Etiketler)>();
            foreach (var ppt in new[] { cizelge.MinPixelsPerTick, Math.Sqrt(cizelge.MinPixelsPerTick * cizelge.MaxPixelsPerTick), cizelge.MaxPixelsPerTick })
            {
                cizelge.PixelsPerTick = ppt;
                sonuc.Add((ppt, cizelge.RulerLabels()));
            }

            pencere.Close();
            return sonuc;
        });

        foreach (var (_, etiketler) in olcum)
        {
            Assert.True(etiketler.Count >= 2);
            for (var i = 1; i < etiketler.Count; i++)
                Assert.True(etiketler[i - 1].X + etiketler[i - 1].Width < etiketler[i].X, $"{etiketler[i - 1].Text} ile {etiketler[i].Text} ust uste");
        }

        Assert.DoesNotContain(olcum[0].Etiketler, e => e.Text.Count(c => c == ':') == 3);
        Assert.All(olcum[2].Etiketler, e => Assert.Matches(@"^\d\d:\d\d:\d\d:\d\d$", e.Text));
        Assert.Contains(olcum[2].Etiketler, e => !e.Text.EndsWith(":00", StringComparison.Ordinal));
    }
}
