using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using VidShrink.App.Editing;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// DZA-P1: Hizli kipte kesim basinin onceki anahtar kareye cekilmesiyle olusan kayma plana yazilir ve teslim
/// notunda soylenir; onceki/sonraki anahtar kareye giden iki komut ve monitordeki "anahtar karede" gostergesi.
/// Bilinen anahtar kare listesiyle saf hesap ve sahte konakta arayuz kolu; dosya ve surec yok.
/// </summary>
public sealed class DuzenleyiciAnahtarKareTests
{
    private const long Gb = 1_000_000_000;
    private const long Sn = EditTime.TicksPerSecond;

    private static long S(double saniye) => EditTime.FromSeconds(saniye);

    private static readonly double[] Kareler = { 0.0, 2.5, 5.0, 7.5 };

    private static MediaInfo Bilgi() => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 1,
        DurationSeconds = 60,
        Width = 1920,
        Height = 1080,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 1,
        AudioCodec = "aac",
        PixelFormat = "yuv420p",
        BitDepth = 8
    };

    [Fact]
    public void HizliKipKaymayiKlipBasinaPlanaYazar()
    {
        var model = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(6.2), S(8)), new EditClip(S(2.5), S(4)) });
        var plan = EditExport.Build(model, Bilgi(), Kareler, 0, ExportMode.Fast, "c.mp4", "is", 8 * Gb);

        Assert.Equal(ExportMode.Fast, plan.Effective);
        Assert.Equal(3, plan.KeyframeLeads.Count);
        Assert.Equal(1.0, plan.KeyframeLeads[0], 6);
        Assert.Equal(1.2, plan.KeyframeLeads[1], 6);
        Assert.Equal(0.0, plan.KeyframeLeads[2], 6);

        var ozet = EditExport.KeyframeLeadSummary(new[] { plan });
        Assert.Equal(new[] { 0, 1 }, ozet.Clips);
        Assert.Equal(1.2, ozet.Longest, 6);
    }

    [Fact]
    public void ParcaliHizliKipteKodlananKlipKaymaz()
    {
        var model = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(3), S(5), 2m) });
        var plan = EditExport.Build(model, Bilgi(), Kareler, 0, ExportMode.Fast, "c.mp4", "is", 8 * Gb);

        Assert.Equal(ExportMode.Fast, plan.Effective);
        Assert.Equal(new[] { 1 }, plan.MotionClips);
        Assert.Equal(2, plan.KeyframeLeads.Count);
        Assert.Equal(1.0, plan.KeyframeLeads[0], 6);
        Assert.Equal(0.0, plan.KeyframeLeads[1], 6);
    }

    [Fact]
    public void TamVeAkilliKipKaymaYazmaz()
    {
        var model = new EditTimeline(new[] { new EditClip(S(1), S(3)) });
        var tam = EditExport.Build(model, Bilgi(), Kareler, 0, ExportMode.Full, "c.mp4", "is", 8 * Gb);
        var akilli = EditExport.Build(model, Bilgi(), Kareler, 0, ExportMode.Smart, "c.mp4", "is", 8 * Gb,
            DuzenleyiciAkilliKodekTests.Sinirlar(Kareler));

        Assert.Empty(tam.KeyframeLeads);
        Assert.Empty(akilli.KeyframeLeads);
        Assert.Empty(EditExport.KeyframeLeadSummary(new[] { tam, akilli }).Clips);
    }

    [Fact]
    public void AyriParcalardaKaymaCizelgeSirasiylaToplanir()
    {
        var model = new EditTimeline(new[] { new EditClip(S(2.5), S(4)), new EditClip(S(5.5), S(7)) });
        var planlar = EditExport.BuildSegments(model, Bilgi(), Kareler, 0, ExportMode.Fast,
            new[] { "a.mp4", "b.mp4" }, new[] { "is-a", "is-b" }, 8 * Gb);

        var ozet = EditExport.KeyframeLeadSummary(planlar);
        Assert.Equal(new[] { 1 }, ozet.Clips);
        Assert.Equal(0.5, ozet.Longest, 6);
    }

    [Fact]
    public void KareAltiKaymaBildirilmez()
    {
        var model = new EditTimeline(new[] { new EditClip(S(2.501), S(4)) });
        var plan = EditExport.Build(model, Bilgi(), Kareler, 0, ExportMode.Fast, "c.mp4", "is", 8 * Gb);

        Assert.True(plan.KeyframeLeads[0] > 0);
        Assert.Empty(EditExport.KeyframeLeadSummary(new[] { plan }).Clips);
    }

    [Fact]
    public void TeslimNotuKaymayiSoyler()
    {
        var eski = Strings.Language;
        try
        {
            Strings.Use("tr");
            var model = new EditTimeline(new[] { new EditClip(S(1), S(3)), new EditClip(S(6.2), S(8)) });
            var plan = EditExport.Build(model, Bilgi(), Kareler, 0, ExportMode.Fast, "c.mp4", "is", 8 * Gb);
            var not = EditorView.Notes(new[] { plan });
            Assert.Contains(string.Format(Strings.Culture, Strings.Get("editor.export.keyframe-lead"), "1, 2", 1.2.ToString("0.##", Strings.Culture)), not);

            var tam = EditExport.Build(model, Bilgi(), Kareler, 0, ExportMode.Full, "c.mp4", "is", 8 * Gb);
            Assert.Equal(string.Empty, EditorView.Notes(new[] { tam }));
        }
        finally
        {
            Strings.Use(eski);
        }
    }

    [Fact]
    public void KomsuAnahtarKareCizelgeAnindanBulunur()
    {
        var model = new EditTimeline(new[] { new EditClip(S(1), S(6)), new EditClip(S(10), S(14), 2m) });
        var kaynak = new Dictionary<int, IReadOnlyList<long>> { [0] = new[] { S(0), S(2), S(4), S(12) } };
        var anlar = TimelineStrip.AllKeyframes(model, kaynak);

        Assert.Equal(new[] { S(1), S(3), S(6) }, anlar);
        Assert.Equal(S(3), TimelineStrip.NeighborKeyframe(anlar, S(1), true, 10));
        Assert.Equal(S(3), TimelineStrip.NeighborKeyframe(anlar, S(2), true, 10));
        Assert.Equal(S(1), TimelineStrip.NeighborKeyframe(anlar, S(3), false, 10));
        Assert.Null(TimelineStrip.NeighborKeyframe(anlar, S(6), true, 10));
        Assert.Null(TimelineStrip.NeighborKeyframe(anlar, S(1), false, 10));

        Assert.True(TimelineStrip.OnKeyframe(model, kaynak, S(6), 10));
        Assert.True(TimelineStrip.OnKeyframe(model, kaynak, S(3) + 9, 10));
        Assert.False(TimelineStrip.OnKeyframe(model, kaynak, S(3) + 11, 10));
        Assert.False(TimelineStrip.OnKeyframe(model, kaynak, S(2), 10));
    }

    [Fact]
    public void EkKaynagınAnahtarKaresiDeSayilir()
    {
        var model = new EditTimeline(new[] { new EditClip(S(0), S(2)), new EditClip(S(0), S(2)) { Source = 1 } });
        var kaynak = new Dictionary<int, IReadOnlyList<long>> { [0] = new[] { S(0) }, [1] = new[] { S(1) } };

        Assert.Equal(new[] { S(0), S(3) }, TimelineStrip.AllKeyframes(model, kaynak));
        Assert.True(TimelineStrip.OnKeyframe(model, kaynak, S(3), 10));
    }

    private static void Kos(Action<EditorView, Window> govde) => AppHost.Run(() =>
    {
        var duzenleyici = new EditorView();
        var pencere = new Window { Width = 900, Height = 600, Content = duzenleyici };
        pencere.Show();
        duzenleyici.ShowTimeline(EditTimeline.FromSource(20 * Sn), 25);
        Dispatcher.UIThread.RunJobs();
        try
        {
            govde(duzenleyici, pencere);
        }
        finally
        {
            pencere.Close();
        }
    });

    private static bool Bas(EditorView d, Key tus, KeyModifiers ek)
    {
        var olay = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = tus, KeyModifiers = ek };
        d.TimelineView.RaiseEvent(olay);
        Dispatcher.UIThread.RunJobs();
        return olay.Handled;
    }

    [Fact]
    public void KisayolOncekiVeSonrakiAnahtarKareyeGider()
    {
        Kos((d, _) =>
        {
            d.TimelineView.Keyframes = new[] { S(0), S(2.5), S(5), S(7.5) };
            d.TimelineView.Playhead = S(3);

            Assert.True(Bas(d, Key.Right, KeyModifiers.Control));
            Assert.Equal(S(5), d.TimelineView.Playhead);
            Assert.True(Bas(d, Key.Right, KeyModifiers.Control));
            Assert.Equal(S(7.5), d.TimelineView.Playhead);
            Assert.True(Bas(d, Key.Left, KeyModifiers.Control));
            Assert.Equal(S(5), d.TimelineView.Playhead);
            Assert.True(Bas(d, Key.Left, KeyModifiers.Control));
            Assert.Equal(S(2.5), d.TimelineView.Playhead);
        });
    }

    [Fact]
    public void MonitorAnahtarKaredeOldugunuGosterir()
    {
        Kos((d, _) =>
        {
            d.TimelineView.Keyframes = new[] { S(0), S(2.5), S(5) };
            d.TimelineView.Playhead = S(1);
            d.RefreshMonitor();
            Assert.False(d.TxtOnKeyframe.IsVisible);

            d.Run(EditorCommand.NextKeyframe);
            Assert.Equal(S(2.5), d.TimelineView.Playhead);
            Assert.True(d.TxtOnKeyframe.IsVisible);
            Assert.Equal(Strings.Get("editor.on-keyframe"), d.TxtOnKeyframe.Text);

            d.Run(EditorCommand.FrameForward);
            Assert.False(d.TxtOnKeyframe.IsVisible);
        });
    }

    [Fact]
    public void KomutlarYenidenAtanabilirKisayolDuzenindedir()
    {
        var geri = Assert.Single(EditorKeymap.DefaultRows, r => r.Command == EditorCommand.PrevKeyframe);
        var ileri = Assert.Single(EditorKeymap.DefaultRows, r => r.Command == EditorCommand.NextKeyframe);
        Assert.Equal((Key.Left, KeyModifiers.Control), (geri.Key, geri.Modifiers));
        Assert.Equal((Key.Right, KeyModifiers.Control), (ileri.Key, ileri.Modifiers));
        Assert.Contains(EditorKeymap.ShortcutSlots(), s => s.Id == EditorKeymap.SlotId(geri));
        Assert.Contains(EditorKeymap.ShortcutSlots(), s => s.Id == EditorKeymap.SlotId(ileri));
        Assert.Single(EditorKeymap.DefaultRows, r => EditorKeymap.SlotId(r) == EditorKeymap.SlotId(geri));
        Assert.Single(EditorKeymap.DefaultRows, r => EditorKeymap.SlotId(r) == EditorKeymap.SlotId(ileri));
    }

    [Fact]
    public void AnahtarKareMetinleri42DildeVar()
    {
        var anahtarlar = new[] { "editor.key.prev-keyframe", "editor.key.next-keyframe", "editor.on-keyframe", "editor.export.keyframe-lead" };

        Assert.Equal(42, Locales.Languages.Count);
        foreach (var dil in Locales.Languages)
        {
            var mevcut = Strings.KeysOf(dil).ToHashSet(StringComparer.Ordinal);
            foreach (var anahtar in anahtarlar)
            {
                Assert.True(mevcut.Contains(anahtar), $"{dil}: {anahtar} yok");
                var metin = Strings.GetIn(dil, anahtar);
                Assert.False(string.IsNullOrWhiteSpace(metin), $"{dil}: {anahtar} bos");
                var bicimli = anahtar == "editor.export.keyframe-lead";
                Assert.Equal(bicimli, metin.Contains("{0}", StringComparison.Ordinal));
                Assert.Equal(bicimli, metin.Contains("{1}", StringComparison.Ordinal));
            }
        }
    }
}
