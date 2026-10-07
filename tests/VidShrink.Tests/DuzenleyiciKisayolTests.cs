using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using VidShrink.App.Editing;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

public sealed class DuzenleyiciKisayolTests
{
    private const long Sn = EditTime.TicksPerSecond;

    private sealed class Ortam
    {
        internal required Window Pencere { get; init; }
        internal required EditorView Duzenleyici { get; init; }
        internal required PlayerView Oynatici { get; init; }
        internal required TextBox Kutu { get; init; }

        internal EditTimeline Model => Duzenleyici.Model!;

        internal EditorTimeline Cizelge => Duzenleyici.TimelineView;

        internal void Sekme(bool duzenleyici)
        {
            Duzenleyici.IsVisible = duzenleyici;
            Oynatici.IsVisible = !duzenleyici;
            Dispatcher.UIThread.RunJobs();
        }

        internal bool Bas(Key tus, KeyModifiers ek = KeyModifiers.None, Control? hedef = null)
        {
            var olay = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = tus, KeyModifiers = ek };
            (hedef ?? Duzenleyici.TimelineView).RaiseEvent(olay);
            return olay.Handled;
        }
    }

    private static Ortam Kur()
    {
        var duzenleyici = new EditorView();
        var oynatici = new PlayerView();
        var kutu = new TextBox();
        var panel = new DockPanel();
        DockPanel.SetDock(kutu, Dock.Top);
        panel.Children.Add(kutu);
        panel.Children.Add(new Grid { Children = { oynatici, duzenleyici } });
        var pencere = new Window { Width = 900, Height = 600, Content = panel };
        pencere.Show();
        duzenleyici.ShowTimeline(EditTimeline.FromSource(120 * Sn), 25);
        var ortam = new Ortam { Pencere = pencere, Duzenleyici = duzenleyici, Oynatici = oynatici, Kutu = kutu };
        ortam.Sekme(true);
        return ortam;
    }

    [Theory]
    [InlineData(Key.PageUp)]
    [InlineData(Key.PageDown)]
    public void CizelgedeYalnizDegistiricisizSayfaTusuTuketilir(Key tus)
    {
        AppHost.Run(() =>
        {
            var o = Kur();
            try
            {
                var ulasan = new List<KeyModifiers>();
                o.Pencere.AddHandler(InputElement.KeyDownEvent, (_, e) =>
                {
                    if (e.Key == tus && !e.Handled) ulasan.Add(e.KeyModifiers);
                }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
                o.Cizelge.Focus();

                Assert.True(o.Bas(tus));
                Assert.Empty(ulasan);

                var ekler = new[]
                {
                    KeyModifiers.Control, KeyModifiers.Shift, KeyModifiers.Alt, KeyModifiers.Control | KeyModifiers.Shift,
                };
                foreach (var ek in ekler) Assert.False(o.Bas(tus, ek), ek.ToString());
                Assert.Equal(ekler, ulasan);
            }
            finally { o.Pencere.Close(); }
        });
    }

    [Fact]
    public void DuzenleyiciEtkinkenTuslarModeliDegistirir()
    {
        AppHost.Run(() =>
        {
            var o = Kur();
            try
            {
                o.Cizelge.Playhead = 10 * Sn;
                Assert.True(o.Bas(Key.I));
                o.Cizelge.Playhead = 20 * Sn;
                Assert.True(o.Bas(Key.O));
                Assert.Equal(10 * Sn, o.Cizelge.MarkIn);
                Assert.Equal(20 * Sn, o.Cizelge.MarkOut);

                Assert.True(o.Bas(Key.K, KeyModifiers.Control));
                Assert.Equal(2, o.Model.Clips.Count);
                o.Cizelge.Playhead = 30 * Sn;
                Assert.True(o.Bas(Key.K, KeyModifiers.Control));
                Assert.Equal(3, o.Model.Clips.Count);

                o.Cizelge.SelectedIndex = 1;
                Assert.True(o.Bas(Key.Delete));
                Assert.Equal(2, o.Model.Clips.Count);
                Assert.Equal(110 * Sn, o.Model.Duration);

                Assert.True(o.Bas(Key.Z, KeyModifiers.Control));
                Assert.Equal(3, o.Model.Clips.Count);
                Assert.True(o.Bas(Key.Y, KeyModifiers.Control));
                Assert.Equal(2, o.Model.Clips.Count);
                Assert.True(o.Bas(Key.Z, KeyModifiers.Control));
                Assert.True(o.Bas(Key.Z, KeyModifiers.Control | KeyModifiers.Shift));
                Assert.Equal(2, o.Model.Clips.Count);

                o.Cizelge.Playhead = 5 * Sn;
                Assert.True(o.Bas(Key.M));
                Assert.Equal(new[] { 5 * Sn }, o.Cizelge.Markers);

                Assert.True(o.Bas(Key.A, KeyModifiers.Control));
                Assert.True(o.Cizelge.AllSelected);
                Assert.True(o.Duzenleyici.SetSpeed(2m));
                Assert.All(o.Model.Clips, c => Assert.Equal(2m, c.Speed));
                Assert.True(o.Bas(Key.Z, KeyModifiers.Control));
                Assert.All(o.Model.Clips, c => Assert.Equal(1m, c.Speed));

                Assert.True(o.Bas(Key.End));
                Assert.Equal(o.Model.Duration, o.Cizelge.Playhead);
                Assert.True(o.Bas(Key.Home));
                Assert.Equal(0, o.Cizelge.Playhead);
                Assert.True(o.Bas(Key.Right));
                Assert.Equal(Sn / 25, o.Cizelge.Playhead);
                Assert.True(o.Bas(Key.Left));
                Assert.Equal(0, o.Cizelge.Playhead);
            }
            finally
            {
                o.Pencere.Close();
            }
        });
    }

    [Fact]
    public void JklMekigiOnizlemeSurucusundeHizVeYonDegistirir()
    {
        AppHost.Run(() =>
        {
            var o = Kur();
            var motor = new EdlMotoru();
            using var surucu = new EdlPreviewDriver(motor, new EdlPreview("kaynak.mp4", o.Model), TimeSpan.FromHours(1));
            try
            {
                o.Duzenleyici.UseDriver(surucu);

                Assert.True(o.Bas(Key.L));
                Assert.Equal(1, surucu.Shuttle);
                Assert.True(o.Duzenleyici.Player.IsPlaying);
                Assert.False(motor.IsPaused);

                o.Bas(Key.L);
                Assert.Equal(2, surucu.Shuttle);
                Assert.Equal(2, motor.Speed);
                o.Bas(Key.L);
                o.Bas(Key.L);
                o.Bas(Key.L);
                Assert.Equal(EditorKeymap.MaxShuttle, surucu.Shuttle);

                Assert.True(o.Bas(Key.K));
                Assert.Equal(0, surucu.Shuttle);
                Assert.Equal(1, surucu.Rate);
                Assert.False(o.Duzenleyici.Player.IsPlaying);
                Assert.True(motor.IsPaused);

                o.Cizelge.Playhead = 50 * Sn;
                _ = surucu.SeekAsync(50 * Sn);
                Assert.True(o.Bas(Key.J));
                Assert.Equal(-1, surucu.Shuttle);
                Assert.True(surucu.Backward);
                Assert.True(motor.IsPaused);
                o.Bas(Key.J);
                Assert.Equal(-2, surucu.Shuttle);

                o.Bas(Key.L);
                Assert.Equal(1, surucu.Shuttle);
                Assert.False(surucu.Backward);
                Assert.True(o.Duzenleyici.Player.IsPlaying);

                Assert.True(o.Bas(Key.Space));
                Assert.False(o.Duzenleyici.Player.IsPlaying);
                Assert.False(surucu.Playing);
            }
            finally
            {
                o.Pencere.Close();
            }
        });
    }

    [Fact]
    public void OtekiSekmedeAyniTuslarOynaticiHaritasinaGider()
    {
        AppHost.Run(() =>
        {
            var o = Kur();
            try
            {
                o.Cizelge.Playhead = 30 * Sn;
                o.Bas(Key.K, KeyModifiers.Control);
                o.Cizelge.SelectedIndex = 1;
                Assert.Equal(2, o.Model.Clips.Count);

                o.Sekme(false);
                o.Cizelge.Playhead = 60 * Sn;
                foreach (var (tus, ek) in new[] { (Key.S, KeyModifiers.None), (Key.K, KeyModifiers.Control), (Key.Delete, KeyModifiers.None), (Key.Z, KeyModifiers.Control), (Key.I, KeyModifiers.None), (Key.M, KeyModifiers.None) })
                    o.Bas(tus, ek, o.Oynatici);
                Assert.Equal(2, o.Model.Clips.Count);
                Assert.True(o.Model.CanUndo);
                Assert.Null(o.Cizelge.MarkIn);
                Assert.Empty(o.Cizelge.Markers);

                Assert.Equal(PlayerCommandKind.TogglePlay, Keymap.ForKey(Key.Space, KeyModifiers.None, null).Kind);
                Assert.Equal(PlayerCommandKind.ToggleMute, Keymap.ForKey(Key.M, KeyModifiers.None, null).Kind);
                Assert.Equal(PlayerCommandKind.Seek, Keymap.ForKey(Key.Right, KeyModifiers.None, null).Kind);
                var once = o.Oynatici.IsPlaying;
                Assert.True(o.Bas(Key.Space, KeyModifiers.None, o.Oynatici));
                Assert.NotEqual(once, o.Oynatici.IsPlaying);
                Assert.False(o.Duzenleyici.Player.IsPlaying);

                o.Sekme(true);
                var oynaticiOnce = o.Oynatici.IsPlaying;
                Assert.True(o.Bas(Key.Space));
                Assert.Equal(oynaticiOnce, o.Oynatici.IsPlaying);
                Assert.True(o.Duzenleyici.Player.IsPlaying);
            }
            finally
            {
                o.Pencere.Close();
            }
        });
    }

    [Fact]
    public void YaziKutusundaKisayolYutulmaz()
    {
        AppHost.Run(() =>
        {
            var o = Kur();
            try
            {
                o.Cizelge.Playhead = 30 * Sn;
                o.Cizelge.SelectedIndex = 0;
                foreach (var (tus, ek) in new[] { (Key.S, KeyModifiers.None), (Key.Delete, KeyModifiers.None), (Key.Space, KeyModifiers.None), (Key.J, KeyModifiers.None), (Key.Left, KeyModifiers.None), (Key.A, KeyModifiers.Control), (Key.I, KeyModifiers.None) })
                    o.Bas(tus, ek, o.Kutu);

                var hiz = o.Duzenleyici.FindControl<TextBox>("TxtSpeed")!;
                o.Bas(Key.S, KeyModifiers.None, hiz);
                o.Bas(Key.Z, KeyModifiers.Control, hiz);

                Assert.Single(o.Model.Clips);
                Assert.True(o.Cizelge.SnapEnabled);
                Assert.False(o.Model.CanUndo);
                Assert.False(o.Cizelge.AllSelected);
                Assert.Null(o.Cizelge.MarkIn);
                Assert.False(o.Duzenleyici.Player.IsPlaying);
                Assert.Equal(30 * Sn, o.Cizelge.Playhead);
            }
            finally
            {
                o.Pencere.Close();
            }
        });
    }

    [Fact]
    public void PremiereTuslariModeliVeOynatmaBasiniDegistirir()
    {
        AppHost.Run(() =>
        {
            var o = Kur();
            try
            {
                Assert.True(o.Bas(Key.S));
                Assert.False(o.Cizelge.SnapEnabled);
                Assert.True(o.Bas(Key.S));
                Assert.True(o.Cizelge.SnapEnabled);
                Assert.False(o.Model.CanUndo);

                o.Cizelge.Playhead = 30 * Sn;
                o.Bas(Key.K, KeyModifiers.Control);
                o.Cizelge.Playhead = 60 * Sn;
                o.Bas(Key.K, KeyModifiers.Control);
                Assert.Equal(3, o.Model.Clips.Count);

                o.Cizelge.Playhead = 45 * Sn;
                var gezinti = new List<long>();
                foreach (var tus in new[] { Key.Down, Key.Down, Key.Down, Key.Up, Key.Up, Key.Up, Key.Up })
                {
                    Assert.True(o.Bas(tus));
                    gezinti.Add(o.Cizelge.Playhead);
                }
                Assert.Equal(new[] { 60 * Sn, 120 * Sn, 120 * Sn, 60 * Sn, 30 * Sn, 0, 0 }, gezinti);

                Assert.True(o.Bas(Key.Right, KeyModifiers.Shift));
                Assert.Equal(5 * Sn / 25, o.Cizelge.Playhead);
                Assert.True(o.Bas(Key.Right, KeyModifiers.Shift));
                Assert.True(o.Bas(Key.Left, KeyModifiers.Shift));
                Assert.Equal(5 * Sn / 25, o.Cizelge.Playhead);

                o.Cizelge.Playhead = 40 * Sn;
                Assert.True(o.Bas(Key.Q));
                Assert.Equal(new EditClip(40 * Sn, 60 * Sn), o.Model.Clips[1]);
                Assert.Equal(110 * Sn, o.Model.Duration);
                Assert.Equal(30 * Sn, o.Cizelge.Playhead);
                Assert.True(o.Bas(Key.Z, KeyModifiers.Control));
                Assert.Equal(120 * Sn, o.Model.Duration);

                o.Cizelge.Playhead = 40 * Sn;
                Assert.True(o.Bas(Key.W));
                Assert.Equal(new EditClip(30 * Sn, 40 * Sn), o.Model.Clips[1]);
                Assert.Equal(100 * Sn, o.Model.Duration);
                Assert.Equal(40 * Sn, o.Cizelge.Playhead);
                Assert.True(o.Bas(Key.Z, KeyModifiers.Control));
                Assert.Equal(120 * Sn, o.Model.Duration);

                o.Cizelge.Playhead = 10 * Sn;
                o.Bas(Key.I);
                o.Cizelge.Playhead = 20 * Sn;
                o.Bas(Key.O);
                o.Cizelge.Playhead = 90 * Sn;
                Assert.True(o.Bas(Key.I, KeyModifiers.Shift));
                Assert.Equal(10 * Sn, o.Cizelge.Playhead);
                Assert.True(o.Bas(Key.O, KeyModifiers.Shift));
                Assert.Equal(20 * Sn, o.Cizelge.Playhead);
                Assert.Equal(10 * Sn, o.Cizelge.MarkIn);

                Assert.True(o.Bas(Key.OemQuotes));
                Assert.Equal(110 * Sn, o.Model.Duration);
                Assert.Equal(4, o.Model.Clips.Count);
                Assert.True(o.Bas(Key.Z, KeyModifiers.Control));
                Assert.Equal(3, o.Model.Clips.Count);

                o.Cizelge.SelectedIndex = 0;
                o.Cizelge.ToggleSelection(2);
                Assert.True(o.Bas(Key.Delete));
                Assert.Equal(new[] { new EditClip(30 * Sn, 60 * Sn) }, o.Model.Clips);
                Assert.True(o.Bas(Key.Z, KeyModifiers.Control));
                Assert.Equal(3, o.Model.Clips.Count);

                Assert.True(o.Bas(Key.A, KeyModifiers.Control));
                Assert.True(o.Bas(Key.Delete, KeyModifiers.Shift));
                Assert.Empty(o.Model.Clips);
                Assert.True(o.Bas(Key.Z, KeyModifiers.Control));
                Assert.Equal(3, o.Model.Clips.Count);
                Assert.Equal(120 * Sn, o.Model.Duration);

                var enAz = o.Cizelge.MinPixelsPerTick;
                foreach (var (tus, buyur) in new[] { (Key.OemPlus, true), (Key.Add, true), (Key.OemMinus, false), (Key.Subtract, false) })
                {
                    var once = o.Cizelge.PixelsPerTick;
                    Assert.True(o.Bas(tus));
                    if (buyur) Assert.True(o.Cizelge.PixelsPerTick > once, tus.ToString());
                    else Assert.True(o.Cizelge.PixelsPerTick < once, tus.ToString());
                }
                o.Bas(Key.OemPlus);
                o.Cizelge.ViewStart = 50 * Sn;
                Assert.True(o.Cizelge.ViewStart > 0);
                Assert.True(o.Bas(Key.OemPipe));
                Assert.Equal(enAz, o.Cizelge.PixelsPerTick);
                Assert.Equal(0, o.Cizelge.ViewStart);

                var hiz = o.Duzenleyici.FindControl<TextBox>("TxtSpeed")!;
                o.Cizelge.SelectedIndex = 0;
                Dispatcher.UIThread.RunJobs();
                Assert.False(hiz.IsFocused);
                Assert.True(o.Bas(Key.R, KeyModifiers.Control));
                Dispatcher.UIThread.RunJobs();
                Assert.True(hiz.IsFocused);
                var sure = o.Model.Duration;
                o.Bas(Key.Q, KeyModifiers.None, hiz);
                Assert.Equal(sure, o.Model.Duration);
            }
            finally
            {
                o.Pencere.Close();
            }
        });
    }

    [Fact]
    public void OynarkenCizelgeOynatmaBasiniSayfaSayfaIzler()
    {
        AppHost.Run(() =>
        {
            var o = Kur();
            var motor = new EdlMotoru();
            using var surucu = new EdlPreviewDriver(motor, new EdlPreview("kaynak.mp4", o.Model), TimeSpan.FromHours(1));
            try
            {
                o.Duzenleyici.UseDriver(surucu);
                o.Cizelge.PixelsPerTick = o.Cizelge.TrackWidth / (10 * Sn);
                o.Cizelge.ViewStart = 0;

                Assert.True(o.Bas(Key.L));
                motor.PositionSeconds = 50;
                o.Duzenleyici.Follow();
                Assert.Equal(50 * Sn, o.Cizelge.Playhead);
                Assert.Equal(50 * Sn, o.Cizelge.ViewStart);

                Assert.True(o.Bas(Key.K));
                motor.PositionSeconds = 90;
                o.Duzenleyici.Follow();
                Assert.Equal(90 * Sn, o.Cizelge.Playhead);
                Assert.Equal(50 * Sn, o.Cizelge.ViewStart);
            }
            finally
            {
                o.Pencere.Close();
            }
        });
    }

    [Fact]
    public void KareHiziDosyadanGelirBilinmezseOtuz()
    {
        Assert.Equal(23.976, EditorView.SourceFps(23.976, 30));
        Assert.Equal(25, EditorView.SourceFps(null, 25));
        Assert.Equal(50, EditorView.SourceFps(0, 50));
        Assert.True(double.IsNaN(EditorView.SourceFps(null, double.NaN)));

        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "duzenleyici-kisayol", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, "kaynak.mp4");
        File.WriteAllBytes(dosya, new byte[16]);
        try
        {
            var (bilinen, bilinmeyen) = AppHost.Run(() =>
            {
                double Ac(double? fps)
                {
                    var view = new EditorView
                    {
                        KnownInfo = yol => fps is { } f
                            ? new MediaInfo { FilePath = yol, FileSizeBytes = 16, DurationSeconds = 600, Width = 64, Height = 36, Fps = f, VideoCodec = "h264", TotalBitrateBps = 1 }
                            : null
                    };
                    view.Player.EngineFactory = () => new YolMotoru();
                    var acilis = view.OpenSourceAsync(dosya);
                    DenetimSurucu.Pump(view.Player, () => acilis.IsCompleted, 10);
                    acilis.GetAwaiter().GetResult();
                    Assert.NotNull(view.Model);
                    return view.TimelineView.FrameTicks;
                }

                return (Ac(50), Ac(null));
            });

            Assert.Equal(Sn / 50.0, bilinen);
            Assert.Equal(Sn / 30.0, bilinmeyen);
        }
        finally
        {
            OynaticiListeTests.Sil(klasor);
        }
    }

    [Fact]
    public void KisayollarMenudeIpucundaVeListedeGorunur()
    {
        var (menu, ipucu, liste) = AppHost.Run(() =>
        {
            var o = Kur();
            try
            {
                var bol = o.Duzenleyici.FindControl<MenuItem>("MnuSplit")!.InputGesture?.ToString();
                var ip = ToolTip.GetTip(o.Duzenleyici.FindControl<Button>("BtnUndo")!) as string;
                var panel = new EditorShortcutsPanel();
                return (bol, ip, panel.Shown);
            }
            finally
            {
                o.Pencere.Close();
            }
        });

        Assert.Equal("Ctrl+K", menu);
        Assert.Equal("Undo <Ctrl+Z>", ipucu);
        Assert.Equal(EditorKeymap.Rows.Count, liste.Count);
        Assert.Contains(("<J>", "Play backward, press again for faster"), liste);
        Assert.Contains(("<Ctrl+Shift+Z>", "Redo"), liste);
    }

    [Fact]
    public void GeriParcaRozetiSimgeKaynagindanCizilir()
    {
        var (simge, yazi, gorunur) = AppHost.Run(() =>
        {
            var cizelge = new EditorTimeline();
            cizelge.Measure(new Avalonia.Size(900, double.PositiveInfinity));
            cizelge.Show(new EditTimeline(new[] { new EditClip(0, 10 * Sn, 2m, true) }));
            cizelge.Measure(new Avalonia.Size(900, double.PositiveInfinity));
            cizelge.Arrange(new Avalonia.Rect(0, 0, 900, cizelge.DesiredSize.Height));
            var parca = cizelge.Realized[0];
            return (ReferenceEquals(parca.ReverseMark.Data, Avalonia.Application.Current!.FindResource(EditorClip.ReverseIcon)), parca.BadgeText, parca.ReverseMark.IsVisible);
        });

        Assert.True(simge);
        Assert.True(gorunur);
        Assert.DoesNotContain("◀", yazi);
        Assert.EndsWith("×", yazi);
    }
}
