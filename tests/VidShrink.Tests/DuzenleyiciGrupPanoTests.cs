using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

public sealed class DuzenleyiciGrupPanoTests
{
    private const long Sn = EditTime.TicksPerSecond;

    private static EditClip[] Dort() => new[]
    {
        new EditClip(0, 10 * Sn), new EditClip(10 * Sn, 20 * Sn), new EditClip(20 * Sn, 30 * Sn), new EditClip(30 * Sn, 40 * Sn),
    };

    private static EditTimeline Model() => new(Dort(), 40 * Sn);

    private static long[] Baslar(EditTimeline model) => model.Clips.Select(c => c.SourceStart / Sn).ToArray();

    [Fact]
    public void GruplananKliplerAyniKimligiAlirVeTekAdimdaGeriAlinir()
    {
        var model = Model();
        var once = model.Clips.ToArray();

        Assert.True(model.Group(new[] { 0, 2 }));
        var kimlik = model.Clips[0].Group;
        Assert.NotNull(kimlik);
        Assert.Equal(kimlik, model.Clips[2].Group);
        Assert.Null(model.Clips[1].Group);
        Assert.False(model.Group(new[] { 0, 2 }));

        Assert.True(model.Undo());
        Assert.Equal(once, model.Clips);
        Assert.False(model.CanUndo);
        Assert.True(model.Redo());
        Assert.Equal(kimlik, model.Clips[2].Group);

        Assert.True(model.Ungroup(new[] { 2 }));
        Assert.All(model.Clips, c => Assert.Null(c.Group));
        Assert.False(model.Ungroup(new[] { 0 }));
        Assert.True(model.Undo());
        Assert.Equal(kimlik, model.Clips[0].Group);
    }

    [Fact]
    public void TekKlipGruplanmaz()
    {
        var model = Model();
        Assert.False(model.Group(new[] { 1 }));
        Assert.False(model.Group(new[] { 1, 1 }));
        Assert.False(model.CanUndo);
    }

    [Fact]
    public void YeniGrupBaskaGrubunKimliginiAlmaz()
    {
        var model = Model();
        Assert.True(model.Group(new[] { 0, 1 }));
        Assert.True(model.Group(new[] { 2, 3 }));
        Assert.NotEqual(model.Clips[0].Group, model.Clips[2].Group);
        Assert.Equal(new[] { 0, 1 }, model.WithGroups(new[] { 1 }));
        Assert.Equal(new[] { 0, 1, 2, 3 }, model.WithGroups(new[] { 3, 0 }));
    }

    [Fact]
    public void GrupBlokOlarakTasinirVeTekAdimdaGeriAlinir()
    {
        var model = Model();
        Assert.True(model.Group(new[] { 0, 1 }));
        var once = model.Clips.ToArray();

        Assert.False(model.Move(1, 0));
        Assert.True(model.Move(0, 3));
        Assert.Equal(new long[] { 20, 30, 0, 10 }, Baslar(model));
        Assert.True(model.Move(3, 0));
        Assert.Equal(new long[] { 0, 10, 20, 30 }, Baslar(model));

        Assert.True(model.Undo());
        Assert.Equal(new long[] { 20, 30, 0, 10 }, Baslar(model));
        Assert.True(model.Undo());
        Assert.Equal(once, model.Clips);
    }

    [Fact]
    public void GrupsuzKlipEskiGibiTasinir()
    {
        var model = Model();
        Assert.True(model.Group(new[] { 2, 3 }));
        Assert.True(model.Move(0, 1));
        Assert.Equal(new long[] { 10, 0, 20, 30 }, Baslar(model));
    }

    [Fact]
    public void GrupBirlikteSilinirVeTekAdimdaGeriGelir()
    {
        var model = Model();
        Assert.True(model.Group(new[] { 1, 3 }));
        var once = model.Clips.ToArray();

        Assert.True(model.DeleteMany(new[] { 1 }));
        Assert.Equal(new long[] { 0, 20 }, Baslar(model));
        Assert.True(model.Undo());
        Assert.Equal(once, model.Clips);

        model.Delete(3);
        Assert.Equal(new long[] { 0, 20 }, Baslar(model));
        Assert.True(model.Undo());
        Assert.Equal(once, model.Clips);
    }

    [Fact]
    public void YapistirmaOynatmaBasindaBolerVeTekAdimdaGeriAlinir()
    {
        var model = Model();
        var once = model.Clips.ToArray();

        var tampon = model.Copy(new[] { 1 });
        Assert.False(model.CanUndo);
        Assert.Equal(new[] { once[1] }, tampon);

        Assert.Equal(1, model.Paste(tampon, 5 * Sn));
        Assert.Equal(new long[] { 0, 10, 5, 10, 20, 30 }, Baslar(model));
        Assert.Equal(5 * Sn, model.Clips[0].SourceEnd);
        Assert.Equal(50 * Sn, model.Duration);

        Assert.True(model.Undo());
        Assert.Equal(once, model.Clips);
        Assert.False(model.CanUndo);
        Assert.True(model.Redo());
        Assert.Equal(6, model.Clips.Count);
    }

    [Fact]
    public void SinirdaYapistirmaBolmedenEkler()
    {
        var model = Model();
        var tampon = model.Copy(new[] { 0, 3 });

        Assert.Equal(2, model.Paste(tampon, 20 * Sn));
        Assert.Equal(new long[] { 0, 10, 0, 30, 20, 30 }, Baslar(model));

        Assert.Equal(6, model.Paste(tampon, model.Duration));
        Assert.Equal(8, model.Clips.Count);
        Assert.True(model.Undo());
        Assert.Equal(6, model.Clips.Count);
    }

    [Fact]
    public void YapistirilanGrupYeniKimlikAlir()
    {
        var model = Model();
        Assert.True(model.Group(new[] { 0, 1 }));
        var eski = model.Clips[0].Group;
        var tampon = model.Copy(new[] { 0 });
        Assert.Equal(2, tampon.Count);

        Assert.Equal(4, model.Paste(tampon, 40 * Sn));
        var yeni = model.Clips[4].Group;
        Assert.NotNull(yeni);
        Assert.NotEqual(eski, yeni);
        Assert.Equal(yeni, model.Clips[5].Group);
        Assert.Equal(new[] { 0, 1 }, model.WithGroups(new[] { 0 }));
        Assert.Equal(new[] { 4, 5 }, model.WithGroups(new[] { 5 }));
    }

    [Fact]
    public void KesmeTekAdimdirVeTamponuDoner()
    {
        var model = Model();
        var once = model.Clips.ToArray();

        var tampon = model.Cut(new[] { 2, 0 });
        Assert.Equal(new[] { once[0], once[2] }, tampon);
        Assert.Equal(new long[] { 10, 30 }, Baslar(model));

        Assert.True(model.Undo());
        Assert.Equal(once, model.Clips);
        Assert.False(model.CanUndo);
    }

    [Fact]
    public void GecersizKaynakYapistirilmaz()
    {
        var model = Model();
        Assert.Equal(-1, model.Paste(new[] { new EditClip(0, 5 * Sn) { Source = 3 } }, 5 * Sn));
        Assert.Equal(-1, model.Paste(new[] { new EditClip(0, 50 * Sn) }, 5 * Sn));
        Assert.Equal(-1, model.Paste(Array.Empty<EditClip>(), 5 * Sn));
        Assert.Equal(4, model.Clips.Count);
        Assert.False(model.CanUndo);
        Assert.Throws<ArgumentOutOfRangeException>(() => model.Paste(model.Copy(new[] { 0 }), 41 * Sn));
    }

    [Fact]
    public void BolmeVeHizGrubuKorur()
    {
        var model = Model();
        Assert.True(model.Group(new[] { 0, 1 }));
        var kimlik = model.Clips[0].Group;
        Assert.True(model.Split(5 * Sn));
        Assert.Equal(kimlik, model.Clips[0].Group);
        Assert.Equal(kimlik, model.Clips[1].Group);
        Assert.True(model.SetSpeed(0, 2m));
        Assert.Equal(kimlik, model.Clips[0].Group);
    }

    [Fact]
    public void ProjeGrubuTasir()
    {
        var model = Model();
        Assert.True(model.Group(new[] { 1, 2 }));
        var json = EditProject.From(new SourceStamp(@"C:\a.mp4", 16, 1), model).ToJson();
        var geri = EditProject.Parse(json);

        Assert.NotNull(geri);
        Assert.Equal(model.Clips, geri!.Clips);
        Assert.Null(geri.Clips[0].Group);
        Assert.NotNull(geri.Clips[1].Group);
        Assert.DoesNotContain("\"group\"", EditProject.From(new SourceStamp(@"C:\a.mp4", 16, 1), Model()).ToJson());
    }

    private sealed class Ortam
    {
        internal required Window Pencere { get; init; }
        internal required EditorView Duzenleyici { get; init; }

        internal EditTimeline Model => Duzenleyici.Model!;

        internal EditorTimeline Cizelge => Duzenleyici.TimelineView;

        internal bool Bas(Key tus, KeyModifiers ek)
        {
            var olay = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = tus, KeyModifiers = ek };
            Cizelge.RaiseEvent(olay);
            Dispatcher.UIThread.RunJobs();
            return olay.Handled;
        }
    }

    private static Ortam Kur()
    {
        var duzenleyici = new EditorView();
        var pencere = new Window { Width = 900, Height = 600, Content = duzenleyici };
        pencere.Show();
        duzenleyici.ShowTimeline(Model(), 25);
        Dispatcher.UIThread.RunJobs();
        return new Ortam { Pencere = pencere, Duzenleyici = duzenleyici };
    }

    [Fact]
    public void KopyalaYapistirTuslariTekAdimdaGeriAlinir()
    {
        AppHost.Run(() =>
        {
            var o = Kur();
            try
            {
                var once = o.Model.Clips.ToArray();
                o.Cizelge.SelectedIndex = 1;
                o.Cizelge.Playhead = 5 * Sn;

                Assert.True(o.Bas(Key.C, KeyModifiers.Control));
                Assert.Equal(once, o.Model.Clips);
                Assert.False(o.Model.CanUndo);
                Assert.Equal(new[] { once[1] }, o.Duzenleyici.Clipboard);

                Assert.True(o.Bas(Key.V, KeyModifiers.Control));
                Assert.Equal(new long[] { 0, 10, 5, 10, 20, 30 }, Baslar(o.Model));

                Assert.True(o.Bas(Key.Z, KeyModifiers.Control));
                Assert.Equal(once, o.Model.Clips);
                Assert.False(o.Model.CanUndo);

                o.Cizelge.SelectedIndex = 0;
                o.Cizelge.Playhead = 40 * Sn;
                Assert.True(o.Bas(Key.D, KeyModifiers.Control));
                Assert.Equal(new long[] { 0, 10, 20, 30, 0 }, Baslar(o.Model));
                Assert.True(o.Bas(Key.Z, KeyModifiers.Control));
                Assert.Equal(once, o.Model.Clips);
            }
            finally
            {
                o.Pencere.Close();
            }
        });
    }

    [Fact]
    public void KesTusuTekAdimdaGeriAlinir()
    {
        AppHost.Run(() =>
        {
            var o = Kur();
            try
            {
                var once = o.Model.Clips.ToArray();
                o.Cizelge.SelectedIndex = 2;

                Assert.True(o.Bas(Key.X, KeyModifiers.Control));
                Assert.Equal(new long[] { 0, 10, 30 }, Baslar(o.Model));
                Assert.Equal(new[] { once[2] }, o.Duzenleyici.Clipboard);

                Assert.True(o.Bas(Key.Z, KeyModifiers.Control));
                Assert.Equal(once, o.Model.Clips);
                Assert.False(o.Model.CanUndo);

                o.Cizelge.SelectAll();
                Assert.True(o.Bas(Key.X, KeyModifiers.Control));
                Assert.Empty(o.Model.Clips);
            }
            finally
            {
                o.Pencere.Close();
            }
        });
    }

    [Fact]
    public void GrupTuslariTekAdimdaGeriAlinirVeSilmeGrubuKapsar()
    {
        AppHost.Run(() =>
        {
            var o = Kur();
            try
            {
                o.Cizelge.SelectedIndex = 0;
                o.Cizelge.ToggleSelection(1);
                Assert.True(o.Bas(Key.G, KeyModifiers.Control));
                Assert.NotNull(o.Model.Clips[0].Group);
                Assert.Equal(o.Model.Clips[0].Group, o.Model.Clips[1].Group);

                Assert.True(o.Bas(Key.Z, KeyModifiers.Control));
                Assert.Null(o.Model.Clips[0].Group);
                Assert.False(o.Model.CanUndo);
                Assert.True(o.Bas(Key.Y, KeyModifiers.Control));

                o.Cizelge.SelectedIndex = 1;
                Assert.True(o.Bas(Key.Delete, KeyModifiers.None));
                Assert.Equal(new long[] { 20, 30 }, Baslar(o.Model));
                Assert.True(o.Bas(Key.Z, KeyModifiers.Control));

                o.Cizelge.SelectedIndex = 0;
                Assert.True(o.Bas(Key.G, KeyModifiers.Control | KeyModifiers.Shift));
                Assert.All(o.Model.Clips, c => Assert.Null(c.Group));
            }
            finally
            {
                o.Pencere.Close();
            }
        });
    }

    [Fact]
    public void BosTampondaYapistirmaModeliDegistirmez()
    {
        AppHost.Run(() =>
        {
            var o = Kur();
            try
            {
                Assert.Empty(o.Duzenleyici.Clipboard);
                o.Duzenleyici.Run(EditorCommand.Paste);
                Assert.Equal(4, o.Model.Clips.Count);
                Assert.False(o.Model.CanUndo);
            }
            finally
            {
                o.Pencere.Close();
            }
        });
    }

    [Fact]
    public void MenudeSatirlarVeTuslarVar()
    {
        AppHost.Run(() =>
        {
            var o = Kur();
            try
            {
                var d = o.Duzenleyici;
                var satirlar = new (MenuItem Satir, Key Tus, KeyModifiers Ek)[]
                {
                    (d.MnuCopy, Key.C, KeyModifiers.Control),
                    (d.MnuCut, Key.X, KeyModifiers.Control),
                    (d.MnuPaste, Key.V, KeyModifiers.Control),
                    (d.MnuDuplicate, Key.D, KeyModifiers.Control),
                    (d.MnuGroup, Key.G, KeyModifiers.Control),
                    (d.MnuUngroup, Key.G, KeyModifiers.Control | KeyModifiers.Shift),
                };
                foreach (var (satir, tus, ek) in satirlar)
                {
                    Assert.Contains(satir, d.TimelineMenu.Items.OfType<MenuItem>());
                    Assert.Equal(new KeyGesture(tus, ek), satir.InputGesture);
                }

                o.Cizelge.SelectedIndex = 1;
                d.MnuCopy.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Single(d.Clipboard);
                o.Cizelge.Playhead = 0;
                d.MnuPaste.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Equal(new long[] { 10, 0, 10, 20, 30 }, Baslar(o.Model));
            }
            finally
            {
                o.Pencere.Close();
            }
        });
    }

    [Fact]
    public void YeniKisayollarCakismaz()
    {
        var yeni = new[]
        {
            (EditorCommand.Copy, Key.C, KeyModifiers.Control),
            (EditorCommand.Cut, Key.X, KeyModifiers.Control),
            (EditorCommand.Paste, Key.V, KeyModifiers.Control),
            (EditorCommand.Duplicate, Key.D, KeyModifiers.Control),
            (EditorCommand.GroupClips, Key.G, KeyModifiers.Control),
            (EditorCommand.UngroupClips, Key.G, KeyModifiers.Control | KeyModifiers.Shift),
        };
        foreach (var (komut, tus, ek) in yeni)
        {
            var satir = Assert.Single(EditorKeymap.DefaultRows, r => r.Command == komut);
            Assert.Equal((tus, ek), (satir.Key, satir.Modifiers));
            Assert.Single(EditorKeymap.DefaultRows, r => EditorKeymap.SlotId(r) == EditorKeymap.SlotId(satir));
            Assert.Contains(EditorKeymap.ShortcutSlots(), s => s.Id == EditorKeymap.SlotId(satir));
        }

        Assert.Equal(EditorKeymap.DefaultRows.Count, EditorKeymap.DefaultRows.Select(EditorKeymap.SlotId).Distinct().Count());
    }

    [Fact]
    public void GrupPanoMetinleri42DildeVar()
    {
        var anahtarlar = new[] { "editor.copy", "editor.cut", "editor.paste", "editor.duplicate", "editor.group", "editor.ungroup" };

        Assert.Equal(42, Locales.Languages.Count);
        foreach (var dil in Locales.Languages)
        {
            var mevcut = Strings.KeysOf(dil).ToHashSet(StringComparer.Ordinal);
            foreach (var anahtar in anahtarlar)
            {
                Assert.True(mevcut.Contains(anahtar), $"{dil}: {anahtar} yok");
                Assert.False(string.IsNullOrWhiteSpace(Strings.GetIn(dil, anahtar)), $"{dil}: {anahtar} bos");
            }
        }
    }
}
