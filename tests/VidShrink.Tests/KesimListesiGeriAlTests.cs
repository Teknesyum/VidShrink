using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

public sealed class KesimListesiGeriAlTests
{
    private const long S = EditTime.TicksPerSecond;

    [Fact]
    public void BosYigindaGeriVeIleriAlmaYapilmaz()
    {
        var timeline = EditTimeline.FromSource(10 * S);

        Assert.False(timeline.Undo());
        Assert.False(timeline.Redo());
        Assert.Equal(new[] { new EditClip(0, 10 * S) }, timeline.Clips);
    }

    [Fact]
    public void YeniIslemlerTekAdimdaGeriVeIleriAlinir()
    {
        var operations = new (string Name, Func<EditTimeline, bool> Run)[]
        {
            ("bas", t => t.RippleTrimHead(14 * S)),
            ("son", t => t.RippleTrimTail(14 * S)),
            ("kenar", t => t.TrimEdge(1, true, 15 * S)),
            ("kenar-geri", t => t.TrimEdge(2, false, 25 * S)),
            ("coklu", t => t.DeleteMany(new[] { 0, 2 })),
            ("hepsi", t => t.DeleteMany(new[] { 0, 1, 2 }))
        };

        foreach (var (name, run) in operations)
        {
            var timeline = new EditTimeline(new[] { new EditClip(0, 10 * S), new EditClip(10 * S, 20 * S), new EditClip(20 * S, 30 * S) }, 30 * S);
            var before = timeline.Clips.ToArray();
            var beforeDuration = timeline.Duration;

            Assert.True(run(timeline), name);
            var after = timeline.Clips.ToArray();
            Assert.NotEqual(before, after);

            Assert.True(timeline.Undo(), name);
            Assert.Equal(before, timeline.Clips);
            Assert.Equal(beforeDuration, timeline.Duration);
            Assert.False(timeline.CanUndo, name);

            Assert.True(timeline.Redo(), name);
            Assert.Equal(after, timeline.Clips);
            Assert.False(timeline.CanRedo, name);
        }
    }

    [Fact]
    public void YeniIslemIleriAlmaYiginiTemizler()
    {
        var timeline = EditTimeline.FromSource(30 * S);
        timeline.Split(10 * S);
        timeline.Undo();
        Assert.True(timeline.CanRedo);

        Assert.True(timeline.RippleTrimTail(20 * S));

        Assert.False(timeline.CanRedo);
    }

    [Fact]
    public void HerIslemZincirdeTekTekGeriVeIleriAlinir()
    {
        var timeline = EditTimeline.FromSource(60 * S);
        var states = new List<EditClip[]> { timeline.Clips.ToArray() };
        var operations = new Action[]
        {
            () => Assert.True(timeline.Split(10 * S)),
            () => Assert.True(timeline.Split(40 * S + 3)),
            () => Assert.True(timeline.SetSpeed(1, -2.5m)),
            () => Assert.True(timeline.Move(2, 0)),
            () => Assert.True(timeline.DeleteRange(5 * S, 25 * S)),
            () => timeline.Delete(0),
            () => Assert.True(timeline.SetSpeed(0, 0.25m)),
            () => Assert.True(timeline.Split(3 * S))
        };

        foreach (var operation in operations)
        {
            operation();
            states.Add(timeline.Clips.ToArray());
            Assert.NotEqual(states[^2], states[^1]);
        }

        for (var i = states.Count - 2; i >= 0; i--)
        {
            Assert.True(timeline.Undo());
            Assert.Equal(states[i], timeline.Clips);
        }

        Assert.False(timeline.CanUndo);
        Assert.False(timeline.Undo());

        for (var i = 1; i < states.Count; i++)
        {
            Assert.True(timeline.Redo());
            Assert.Equal(states[i], timeline.Clips);
        }

        Assert.False(timeline.CanRedo);
        Assert.False(timeline.Redo());
    }

    [Fact]
    public void AralikSilmeTekAdimdaGeriAlinir()
    {
        var timeline = EditTimeline.FromSource(60 * S);
        var before = timeline.Clips.ToArray();

        Assert.True(timeline.DeleteRange(10 * S + 1, 20 * S + 7));
        var after = timeline.Clips.ToArray();

        Assert.True(timeline.Undo());
        Assert.Equal(before, timeline.Clips);
        Assert.False(timeline.CanUndo);

        Assert.True(timeline.Redo());
        Assert.Equal(after, timeline.Clips);
    }

    [Fact]
    public void YeniIslemIleriAlmaYiginiBosaltir()
    {
        var timeline = EditTimeline.FromSource(60 * S);
        Assert.True(timeline.Split(10 * S));
        Assert.True(timeline.Split(20 * S));
        Assert.True(timeline.Undo());
        Assert.True(timeline.CanRedo);

        Assert.True(timeline.Split(30 * S));

        Assert.False(timeline.CanRedo);
        Assert.False(timeline.Redo());
        Assert.Equal(new[] { 0, 10 * S, 30 * S }, Enumerable.Range(0, 3).Select(timeline.ClipStart));
    }

    [Fact]
    public void IslemYapmayanCagriYiginaGirmez()
    {
        var timeline = EditTimeline.FromSource(60 * S);
        Assert.True(timeline.Split(10 * S));
        Assert.True(timeline.Undo());

        Assert.False(timeline.Split(0));
        Assert.False(timeline.Move(0, 0));
        Assert.False(timeline.SetSpeed(0, 1m));
        Assert.False(timeline.DeleteRange(5 * S, 5 * S));

        Assert.False(timeline.CanUndo);
        Assert.True(timeline.CanRedo);
    }

    [Fact]
    public void SonParcaninSilinmesiGeriVeIleriAlinir()
    {
        var timeline = EditTimeline.FromSource(5 * S);

        timeline.Delete(0);
        Assert.True(timeline.Undo());
        Assert.Equal(5 * S, timeline.Duration);

        Assert.True(timeline.Redo());
        Assert.Empty(timeline.Clips);
        Assert.Equal(0, timeline.Duration);
    }

    [Fact]
    public void UzunZincirGeriAlinincaTickKaybiOlmaz()
    {
        var timeline = EditTimeline.FromSource(90 * S + 13);
        var original = timeline.Clips.ToArray();
        var random = new Random(20260927);

        for (var step = 0; step < 400; step++)
        {
            var count = timeline.Clips.Count;
            switch (random.Next(4))
            {
                case 0 when timeline.Duration > 1:
                    timeline.Split(1 + random.NextInt64(timeline.Duration - 1));
                    break;
                case 1 when count > 1:
                    timeline.Move(random.Next(count), random.Next(count));
                    break;
                case 2 when count > 0:
                    var magnitude = random.Next(1, 10_001) / 100m;
                    timeline.SetSpeed(random.Next(count), random.Next(2) == 0 ? magnitude : -magnitude);
                    break;
                case 3 when count > 2 && timeline.Duration > 8:
                    var start = random.NextInt64(timeline.Duration / 2);
                    timeline.DeleteRange(start, start + random.NextInt64(1, timeline.Duration / 4));
                    break;
            }
        }

        while (timeline.Undo()) { }

        Assert.Equal(original, timeline.Clips);
        Assert.Equal(90 * S + 13, timeline.Duration);
    }
}
