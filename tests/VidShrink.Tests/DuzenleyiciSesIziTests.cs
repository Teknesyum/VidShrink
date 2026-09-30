using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using VidShrink.App.Editing;
using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Ses izi görüntü izinin beşte biri: yükseklik belirteçten türer, görüntü izi değişince
/// oran korunur; jilet, kenar ve seçim ince satırda da çalışır; dalga kaynağın en yüksek
/// tepesine göre ölçeklenir ve iz başlığı satıra sığar.
/// </summary>
public sealed class DuzenleyiciSesIziTests
{
    private const double Genislik = 1000;

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

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

    private static Point SesNoktasi(EditorTimeline cizelge, double x) => new(x, cizelge.AudioTop + cizelge.AudioHeight / 2);

    private static Pointer Isaretci() => new(Pointer.GetNextFreeId(), PointerType.Mouse, true);

    private static void Bas(EditorTimeline cizelge, IPointer isaretci, Point nokta) =>
        cizelge.RaiseEvent(new PointerPressedEventArgs(cizelge, isaretci, cizelge, nokta, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));

    private static void Kaydir(EditorTimeline cizelge, IPointer isaretci, Point nokta, bool basili) =>
        cizelge.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, cizelge, isaretci, cizelge, nokta, 0,
            new PointerPointProperties(basili ? RawInputModifiers.LeftMouseButton : RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None));

    private static void Birak(EditorTimeline cizelge, IPointer isaretci, Point nokta) =>
        cizelge.RaiseEvent(new PointerReleasedEventArgs(cizelge, isaretci, cizelge, nokta, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));

    [Fact]
    public void SesIziGoruntuIzininBesteBiriVeBelirtecleBirlikteOlceklenir()
    {
        var olcum = AppHost.Run(() =>
        {
            var (cizelge, pencere) = Kur(UcKesim());
            var video = cizelge.VideoHeight;
            var ses = cizelge.AudioHeight;
            var alt = cizelge.TracksBottom;
            var istenen = cizelge.DesiredSize.Height;

            cizelge.Resources["EditorVideoTrackHeight"] = video * 2.5;
            cizelge.InvalidateMeasure();
            Dispatcher.UIThread.RunJobs();
            var buyukVideo = cizelge.VideoHeight;
            var buyukSes = cizelge.AudioHeight;
            var buyukIstenen = cizelge.DesiredSize.Height;
            var buyukAlt = cizelge.TracksBottom;
            pencere.Close();
            return (video, ses, alt, istenen, buyukVideo, buyukSes, buyukIstenen, buyukAlt);
        });

        Assert.True(olcum.video > 0);
        Assert.Equal(olcum.video / 5, olcum.ses, 6);
        Assert.InRange(olcum.istenen, olcum.alt, olcum.alt + 1);
        Assert.Equal(olcum.video * 2.5, olcum.buyukVideo, 6);
        Assert.Equal(olcum.buyukVideo / 5, olcum.buyukSes, 6);
        Assert.InRange(olcum.buyukIstenen, olcum.buyukAlt, olcum.buyukAlt + 1);
    }

    [Fact]
    public void SesSatirindaJiletKenarVeSecimCalisir()
    {
        var olcum = AppHost.Run(() =>
        {
            var model = UcKesim();
            var (cizelge, pencere) = Kur(model);
            var isaretci = Isaretci();

            var kenarNoktasi = SesNoktasi(cizelge, cizelge.TimeToX(S(20)) + 2);
            var kenar = cizelge.EdgeAt(kenarNoktasi, out var kenarIndeks, out var bas);
            Kaydir(cizelge, isaretci, kenarNoktasi, false);
            var kenarImleci = cizelge.CursorKind;

            var altinda = new Point(cizelge.TimeToX(S(20)) + 2, cizelge.TracksBottom + 1);
            var altindaKenar = cizelge.EdgeAt(altinda, out _, out _);

            var secimNoktasi = SesNoktasi(cizelge, cizelge.TimeToX(S(30)));
            Bas(cizelge, isaretci, secimNoktasi);
            Birak(cizelge, isaretci, secimNoktasi);
            var secili = cizelge.SelectedIndex;

            cizelge.Tool = EditorTool.Razor;
            var once = model.Clips.Count;
            var jiletNoktasi = SesNoktasi(cizelge, cizelge.TimeToX(S(5)));
            Kaydir(cizelge, isaretci, jiletNoktasi, false);
            var jiletImleci = cizelge.CursorKind;
            Bas(cizelge, isaretci, jiletNoktasi);
            Birak(cizelge, isaretci, jiletNoktasi);
            var sonra = model.Clips.Count;
            var sinir = model.ClipStart(1);
            pencere.Close();
            return (kenar, kenarIndeks, bas, kenarImleci, altindaKenar, secili, once, sonra, sinir, jiletImleci);
        });

        Assert.True(olcum.kenar);
        Assert.Equal(1, olcum.kenarIndeks);
        Assert.True(olcum.bas);
        Assert.Equal(StandardCursorType.SizeWestEast, olcum.kenarImleci);
        Assert.False(olcum.altindaKenar);
        Assert.Equal(1, olcum.secili);
        Assert.Equal(StandardCursorType.Cross, olcum.jiletImleci);
        Assert.Equal(olcum.once + 1, olcum.sonra);
        Assert.Equal(S(5), olcum.sinir);
    }

    [Fact]
    public void DalgaEnYuksekTepeyeGoreOlceklenir()
    {
        var sessiz = AudioPeaks.FromPcm(Enumerable.Range(0, AudioPeaks.SamplesPerBucket * 3).Select(i => (short)(i % 2 == 0 ? 800 : -1200)).ToArray());
        var tam = AudioPeaks.FromPcm(new short[] { short.MinValue, short.MaxValue });

        Assert.Equal(1200, sessiz.Loudest);
        Assert.Equal(32768, tam.Loudest);
        Assert.Equal(0, AudioPeaks.Empty.Loudest);

        Assert.Equal(-1, EditorTimeline.Amplitude(-1200, sessiz.Loudest), 6);
        Assert.Equal(800 / 1200.0, EditorTimeline.Amplitude(800, sessiz.Loudest), 6);
        Assert.Equal(1, EditorTimeline.Amplitude(short.MaxValue, 1200), 6);
        Assert.Equal(1200 / (double)short.MaxValue, EditorTimeline.Amplitude(1200, 0), 6);
    }

    [Fact]
    public void IzBasligiInceSatiraSigar()
    {
        Assert.Equal(0.5, EditorTimeline.HeaderLabelScale(20, 10), 6);
        Assert.Equal(1, EditorTimeline.HeaderLabelScale(8, 10), 6);
        Assert.Equal(0, EditorTimeline.HeaderLabelScale(20, 0), 6);
        Assert.Equal(1, EditorTimeline.HeaderLabelScale(0, 10), 6);
    }
}
