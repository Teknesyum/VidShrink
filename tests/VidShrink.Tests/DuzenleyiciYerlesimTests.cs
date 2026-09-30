using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VidShrink.App.Editing;
using VidShrink.App.Playback;
using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

public sealed class DuzenleyiciYerlesimTests
{
    private const long Sn = EditTime.TicksPerSecond;

    private sealed class Ortam
    {
        internal required Window Pencere { get; init; }
        internal required EditorView Duzenleyici { get; init; }
        internal required PlayerView Oynatici { get; init; }

        internal EditTimeline Model => Duzenleyici.Model!;

        internal EditorTimeline Cizelge => Duzenleyici.TimelineView;

        internal bool Bas(Key tus, KeyModifiers ek = KeyModifiers.None, Control? hedef = null)
        {
            var olay = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = tus, KeyModifiers = ek };
            (hedef ?? Duzenleyici.TimelineView).RaiseEvent(olay);
            Dispatcher.UIThread.RunJobs();
            return olay.Handled;
        }

        internal void Tikla(Button dugme)
        {
            dugme.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        }

        internal bool ZamanKodu(string metin)
        {
            Duzenleyici.TxtTimecode.Focus();
            Duzenleyici.TxtTimecode.Text = metin;
            return Bas(Key.Enter, KeyModifiers.None, Duzenleyici.TxtTimecode);
        }
    }

    private static Ortam Kur()
    {
        var duzenleyici = new EditorView();
        var oynatici = new PlayerView();
        var pencere = new Window { Width = 900, Height = 600, Content = new Grid { Children = { oynatici, duzenleyici } } };
        pencere.Show();
        duzenleyici.ShowTimeline(EditTimeline.FromSource(120 * Sn), 25);
        oynatici.IsVisible = false;
        Dispatcher.UIThread.RunJobs();
        return new Ortam { Pencere = pencere, Duzenleyici = duzenleyici, Oynatici = oynatici };
    }

    private static void Kos(System.Action<Ortam> govde) => AppHost.Run(() =>
    {
        var o = Kur();
        try
        {
            govde(o);
        }
        finally
        {
            o.Pencere.Close();
        }
    });

    [Fact]
    public void ZamanKoduEnterIleOKareyeGider()
    {
        Kos(o =>
        {
            Assert.True(o.ZamanKodu("00:00:10:00"));
            Assert.Equal(10 * Sn, o.Cizelge.Playhead);
            Assert.Equal("00:00:10:00", o.Duzenleyici.TxtTimecode.Text);

            Assert.True(o.ZamanKodu("+10"));
            Assert.Equal(10 * Sn + 10 * Sn / 25, o.Cizelge.Playhead);

            Assert.True(o.ZamanKodu("-10"));
            Assert.Equal(10 * Sn, o.Cizelge.Playhead);
        });
    }

    [Fact]
    public void GecersizZamanKoduOynatmaBasiniDegistirmez()
    {
        Kos(o =>
        {
            o.Cizelge.Playhead = 5 * Sn;
            Assert.True(o.ZamanKodu("xyz"));
            Assert.Equal(5 * Sn, o.Cizelge.Playhead);
            Assert.Equal("00:00:05:00", o.Duzenleyici.TxtTimecode.Text);

            o.Duzenleyici.TxtTimecode.Focus();
            o.Duzenleyici.TxtTimecode.Text = "00:00:40:00";
            Assert.True(o.Bas(Key.Escape, KeyModifiers.None, o.Duzenleyici.TxtTimecode));
            Assert.Equal(5 * Sn, o.Cizelge.Playhead);
            Assert.Equal("00:00:05:00", o.Duzenleyici.TxtTimecode.Text);
        });
    }

    [Fact]
    public void SureToplamUzunluguGosterir()
    {
        Kos(o => Assert.Equal("00:02:00:00", o.Duzenleyici.TxtDuration.Text));
    }

    [Fact]
    public void MonitorDugmeleriDogruKomutuCalistirir()
    {
        Kos(o =>
        {
            var d = o.Duzenleyici;
            o.Cizelge.Playhead = 10 * Sn;
            o.Tikla(d.BtnMarkIn);
            Assert.Equal(10 * Sn, o.Cizelge.MarkIn);
            o.Cizelge.Playhead = 20 * Sn;
            o.Tikla(d.BtnMarkOut);
            Assert.Equal(20 * Sn, o.Cizelge.MarkOut);

            o.Tikla(d.BtnGoIn);
            Assert.Equal(10 * Sn, o.Cizelge.Playhead);
            o.Tikla(d.BtnGoOut);
            Assert.Equal(20 * Sn, o.Cizelge.Playhead);

            o.Tikla(d.BtnFrameForward);
            Assert.Equal(20 * Sn + Sn / 25, o.Cizelge.Playhead);
            o.Tikla(d.BtnFrameBack);
            Assert.Equal(20 * Sn, o.Cizelge.Playhead);

            o.Cizelge.Playhead = 30 * Sn;
            o.Tikla(d.BtnSplit);
            Assert.Equal(2, o.Model.Clips.Count);

            Assert.True(d.BtnExtract.IsEnabled);
            o.Tikla(d.BtnExtract);
            Assert.Equal(110 * Sn, o.Model.Duration);
        });
    }

    [Fact]
    public void GirisCikisYokkenGitDugmeleriPasif()
    {
        Kos(o =>
        {
            Assert.False(o.Duzenleyici.BtnGoIn.IsEnabled);
            Assert.False(o.Duzenleyici.BtnGoOut.IsEnabled);
            Assert.False(o.Duzenleyici.BtnExtract.IsEnabled);
            Assert.True(o.Duzenleyici.BtnMarkIn.IsEnabled);
        });
    }

    [Fact]
    public void VcbAraciDegistirirVePaletIzler()
    {
        Kos(o =>
        {
            var d = o.Duzenleyici;
            Assert.Equal(EditorTool.Selection, o.Cizelge.Tool);
            Assert.True(d.BtnToolSelection.IsChecked);

            Assert.True(o.Bas(Key.C));
            Assert.Equal(EditorTool.Razor, o.Cizelge.Tool);
            Assert.True(d.BtnToolRazor.IsChecked);
            Assert.False(d.BtnToolSelection.IsChecked);
            Assert.False(d.BtnToolRipple.IsChecked);

            Assert.True(o.Bas(Key.B));
            Assert.Equal(EditorTool.Ripple, o.Cizelge.Tool);
            Assert.True(d.BtnToolRipple.IsChecked);
            Assert.False(d.BtnToolRazor.IsChecked);

            Assert.True(o.Bas(Key.V));
            Assert.Equal(EditorTool.Selection, d.Tool);
            Assert.True(d.BtnToolSelection.IsChecked);

            o.Tikla(d.BtnToolRazor);
            Assert.Equal(EditorTool.Razor, o.Cizelge.Tool);
            o.Tikla(d.BtnToolRazor);
            Assert.True(d.BtnToolRazor.IsChecked);
            Assert.False(d.BtnToolSelection.IsChecked);
        });
    }

    [Fact]
    public void YapismaDugmesiSDurumunuIzler()
    {
        Kos(o =>
        {
            var once = o.Cizelge.SnapEnabled;
            Assert.Equal(once, o.Duzenleyici.BtnSnap.IsChecked);
            Assert.True(o.Bas(Key.S));
            Assert.Equal(!once, o.Cizelge.SnapEnabled);
            Assert.Equal(!once, o.Duzenleyici.BtnSnap.IsChecked);
            o.Tikla(o.Duzenleyici.BtnSnap);
            Assert.Equal(once, o.Cizelge.SnapEnabled);
            Assert.Equal(once, o.Duzenleyici.BtnSnap.IsChecked);
        });
    }

    [Fact]
    public void SeritDuzenleyicideGizliOynaticidaGorunur()
    {
        Kos(o =>
        {
            Assert.False(o.Duzenleyici.Player.StripVisible);
            Assert.True(o.Oynatici.StripVisible);
        });
    }

    [Fact]
    public void CtrlMDisaAktarmayiAcar()
    {
        Kos(o =>
        {
            Assert.Equal(EditorCommand.Export, EditorKeymap.For(Key.M, KeyModifiers.Control));
            Assert.Equal(EditorCommand.AddMarker, EditorKeymap.For(Key.M, KeyModifiers.None));
            var once = o.Duzenleyici.ExportRequests;
            Assert.True(o.Bas(Key.M, KeyModifiers.Control));
            Assert.Equal(once + 1, o.Duzenleyici.ExportRequests);
            Assert.Empty(o.Cizelge.Markers);
        });
    }

    [Fact]
    public void TeslimBitinceYakinlastirmaIpucuKorunur()
    {
        Kos(o =>
        {
            var d = o.Duzenleyici;
            var serit = typeof(EditorView).GetMethod("ShowExportBar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            serit.Invoke(d, new object[] { true });
            serit.Invoke(d, new object[] { false });
            Assert.Equal(EditorView.Tip("editor.zoom-in", EditorCommand.ZoomIn), ToolTip.GetTip(d.BtnZoomIn));
            Assert.Equal(EditorView.Tip("editor.zoom-out", EditorCommand.ZoomOut), ToolTip.GetTip(d.BtnZoomOut));
            Assert.Equal(EditorView.Tip("editor.key.zoom-fit", EditorCommand.ZoomFit), ToolTip.GetTip(d.BtnZoomFit));
            Assert.Contains(EditorKeymap.Gesture(EditorCommand.Export), EditorView.Tip("editor.save-as", EditorCommand.Export));
        });
    }

    [Fact]
    public void OynatDugmesiSimgesiDurumaGoreDegisir()
    {
        Kos(o =>
        {
            var d = o.Duzenleyici;
            Assert.Equal(EditorView.Tip("playback.control.play", EditorCommand.PlayPause), ToolTip.GetTip(d.BtnPlay));
            o.Tikla(d.BtnPlay);
            var oynuyor = d.Player.IsPlaying;
            var anahtar = oynuyor ? "playback.control.pause" : "playback.control.play";
            Assert.Equal(EditorView.Tip(anahtar, EditorCommand.PlayPause), ToolTip.GetTip(d.BtnPlay));
        });
    }
}
