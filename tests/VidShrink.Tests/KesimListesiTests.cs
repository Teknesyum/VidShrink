using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

public sealed class KesimListesiTests
{
    private const long S = EditTime.TicksPerSecond;

    [Theory]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(30)]
    [InlineData(50)]
    [InlineData(60)]
    [InlineData(48_000)]
    public void KareSinirlariTickteTamSayiyaOturur(int rate)
    {
        var step = S / rate;
        Assert.Equal(S, step * rate);

        for (var frame = 0; frame <= rate * 3; frame++)
            Assert.Equal(frame * step, EditTime.FromSeconds((double)frame / rate));
    }

    [Theory]
    [InlineData(0L, 0L)]
    [InlineData(0L, -5L)]
    [InlineData(-1L, 10L)]
    public void SifirVeTersUzunluktakiParcaKurulmaz(long start, long end)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EditClip(start, end));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.009")]
    [InlineData("100.01")]
    [InlineData("1.234")]
    public void ParcaHiziSinirVeAdimDisindaKurulmaz(string speed)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EditClip(0, S, decimal.Parse(speed, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData("1", 10, 10)]
    [InlineData("2", 10, 5)]
    [InlineData("0.5", 10, 20)]
    [InlineData("3", 10, 4)]
    [InlineData("100", 1, 1)]
    [InlineData("0.01", 3, 300)]
    public void CizelgeUzunluguHizaBolunupYukariYuvarlanir(string speed, long source, long expected)
    {
        var clip = new EditClip(1000, 1000 + source, decimal.Parse(speed, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(expected, clip.TimelineLength);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1.5")]
    [InlineData("2.5")]
    [InlineData("3")]
    [InlineData("7.77")]
    [InlineData("100")]
    public void HizliParcadaCizelgeAniKaynaktanAynenGeriDoner(string speed)
    {
        foreach (var reversed in new[] { false, true })
        {
            var clip = new EditClip(500, 500 + 10_007, decimal.Parse(speed, System.Globalization.CultureInfo.InvariantCulture), reversed);
            for (long t = 0; t < clip.TimelineLength; t++)
            {
                var source = clip.ToSource(t);
                Assert.True(clip.Contains(source));
                Assert.Equal(t, clip.ToOffset(source));
            }
        }
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0.5")]
    [InlineData("0.33")]
    [InlineData("0.01")]
    public void YavasParcadaKaynakTickiAynenGeriDoner(string speed)
    {
        foreach (var reversed in new[] { false, true })
        {
            var clip = new EditClip(500, 500 + 1_003, decimal.Parse(speed, System.Globalization.CultureInfo.InvariantCulture), reversed);
            for (var s = clip.SourceStart; s < clip.SourceEnd; s++)
                Assert.Equal(s, clip.ToSource(clip.ToOffset(s)));
        }
    }

    [Fact]
    public void GeriParcaKaynagiSondanBasaGosterir()
    {
        var clip = new EditClip(10 * S, 20 * S, 1m, reversed: true);

        Assert.Equal(20 * S - 1, clip.ToSource(0));
        Assert.Equal(10 * S, clip.ToSource(clip.TimelineLength - 1));
        Assert.Equal(15 * S - 1, clip.ToSource(5 * S));
    }

    [Fact]
    public void TekParcaliCizelgeKaynagaBireBirDuser()
    {
        var timeline = EditTimeline.FromSource(60 * S);

        Assert.Single(timeline.Clips);
        Assert.Equal(60 * S, timeline.Duration);
        foreach (var t in new[] { 0, 1, 30 * S, 60 * S - 1 })
        {
            Assert.Equal(t, timeline.ToSource(t));
            Assert.Equal(t, timeline.ToTimeline(t));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.ToSource(60 * S));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.ToSource(-1));
        Assert.Null(timeline.ToTimeline(60 * S));
    }

    [Fact]
    public void OrtadanKesmeIkiBitisikParcaVeriIcerikDegismez()
    {
        var timeline = EditTimeline.FromSource(60 * S);
        var before = Sample(timeline);

        Assert.True(timeline.Split(25 * S + 7));

        Assert.Equal(new[] { new EditClip(0, 25 * S + 7), new EditClip(25 * S + 7, 60 * S) }, timeline.Clips);
        Assert.Equal(60 * S, timeline.Duration);
        Assert.Equal(25 * S + 7, timeline.ClipStart(1));
        Assert.Equal(before, Sample(timeline));
    }

    [Fact]
    public void KenardaKesmeIslemDegildir()
    {
        var timeline = EditTimeline.FromSource(60 * S);
        Assert.True(timeline.Split(20 * S));
        var clips = timeline.Clips.ToList();
        Assert.True(timeline.Undo());
        Assert.True(timeline.Redo());

        Assert.False(timeline.Split(0));
        Assert.False(timeline.Split(20 * S));
        Assert.False(timeline.Split(60 * S));

        Assert.Equal(clips, timeline.Clips);
        Assert.True(timeline.Undo());
        Assert.False(timeline.CanUndo);
    }

    [Fact]
    public void CizelgeDisindaKesmeReddedilir()
    {
        var timeline = EditTimeline.FromSource(10 * S);

        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.Split(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.Split(10 * S + 1));
        Assert.False(timeline.CanUndo);
    }

    [Fact]
    public void GeriParcayiKesmeGosterimSirasiniKorur()
    {
        var timeline = new EditTimeline(new[] { new EditClip(10 * S, 20 * S, 1m, reversed: true) });
        var before = Sample(timeline);

        Assert.True(timeline.Split(3 * S));

        Assert.Equal(
            new[] { new EditClip(17 * S, 20 * S, 1m, true), new EditClip(10 * S, 17 * S, 1m, true) },
            timeline.Clips);
        Assert.Equal(before, Sample(timeline));
    }

    [Theory]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("7.77")]
    public void HizliParcadaKesimIstenenAnaOturur(string speed)
    {
        var timeline = new EditTimeline(new[] { new EditClip(0, 30 * S, decimal.Parse(speed, System.Globalization.CultureInfo.InvariantCulture)) });
        var duration = timeline.Duration;
        var cut = duration / 3 + 1;
        var before = Sample(timeline);

        Assert.True(timeline.Split(cut));

        Assert.Equal(cut, timeline.ClipStart(1));
        Assert.InRange(timeline.Duration - duration, 0, 1);
        var after = Sample(timeline, duration);
        for (var i = 0; i < before.Length; i++)
            Assert.InRange(after[i] - before[i], -1, 0);
    }

    [Theory]
    [InlineData("0.5")]
    [InlineData("0.33")]
    [InlineData("0.01")]
    public void YavasParcadaKesimKaynakTickineGeriOturur(string speed)
    {
        var value = decimal.Parse(speed, System.Globalization.CultureInfo.InvariantCulture);
        var timeline = new EditTimeline(new[] { new EditClip(0, 30 * S, value) });
        var duration = timeline.Duration;
        var cut = duration / 3 + 1;
        var boundary = timeline.ToTimeline(timeline.ToSource(cut))!.Value;

        Assert.True(timeline.Split(cut));

        Assert.Equal(boundary, timeline.ClipStart(1));
        Assert.InRange(cut - timeline.ClipStart(1), 0, (long)(1 / value));
        Assert.InRange(timeline.Duration - duration, 0, 1);
    }

    [Fact]
    public void YavasParcadaKaynakTickiIcindeKesmeBolmez()
    {
        var timeline = new EditTimeline(new[] { new EditClip(0, 10, 0.01m) });

        Assert.False(timeline.Split(50));
        Assert.Single(timeline.Clips);
        Assert.False(timeline.CanUndo);

        Assert.True(timeline.Split(100));
        Assert.Equal(new[] { new EditClip(0, 1, 0.01m), new EditClip(1, 10, 0.01m) }, timeline.Clips);
    }

    [Fact]
    public void OrtadakiParcaSilinincePesindekiKayar()
    {
        var timeline = ThreeParts();

        timeline.Delete(1);

        Assert.Equal(new[] { new EditClip(0, 10 * S), new EditClip(20 * S, 30 * S) }, timeline.Clips);
        Assert.Equal(20 * S, timeline.Duration);
        Assert.Equal(20 * S, timeline.ToSource(10 * S));
        Assert.Null(timeline.ToTimeline(15 * S));
    }

    [Fact]
    public void SonParcaSilinebilirCizelgeBosKalir()
    {
        var timeline = EditTimeline.FromSource(5 * S);

        timeline.Delete(0);

        Assert.Empty(timeline.Clips);
        Assert.Equal(0, timeline.Duration);
        Assert.False(timeline.Split(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.ToSource(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.Delete(0));
        Assert.Null(timeline.ToTimeline(0));

        Assert.True(timeline.Undo());
        Assert.Equal(new[] { new EditClip(0, 5 * S) }, timeline.Clips);
    }

    [Fact]
    public void TasimaSirayiDegistirirKaynakYeniYerindeBulunur()
    {
        var timeline = ThreeParts();

        Assert.True(timeline.Move(0, 2));

        Assert.Equal(new[] { new EditClip(10 * S, 20 * S), new EditClip(20 * S, 30 * S), new EditClip(0, 10 * S) }, timeline.Clips);
        Assert.Equal(20 * S + 5, timeline.ToTimeline(5));
        Assert.Equal(10 * S, timeline.ToSource(0));

        Assert.True(timeline.Move(2, 1));
        Assert.Equal(new[] { new EditClip(10 * S, 20 * S), new EditClip(0, 10 * S), new EditClip(20 * S, 30 * S) }, timeline.Clips);

        Assert.False(timeline.Move(1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.Move(0, 3));
    }

    [Fact]
    public void AralikSilmeArkadakiniKaydirir()
    {
        var timeline = EditTimeline.FromSource(60 * S);

        Assert.True(timeline.DeleteRange(10 * S, 20 * S));

        Assert.Equal(new[] { new EditClip(0, 10 * S), new EditClip(20 * S, 60 * S) }, timeline.Clips);
        Assert.Equal(50 * S, timeline.Duration);
        Assert.Equal(20 * S, timeline.ToSource(10 * S));
        Assert.Equal(10 * S - 1, timeline.ToSource(10 * S - 1));
    }

    [Fact]
    public void BirkacParcayiKesenAralikSilinir()
    {
        var timeline = ThreeParts();
        timeline.SetSpeed(1, 2m);

        Assert.True(timeline.DeleteRange(5 * S, 22 * S));

        Assert.Equal(new[] { new EditClip(0, 5 * S), new EditClip(27 * S, 30 * S) }, timeline.Clips);
        Assert.Equal(8 * S, timeline.Duration);
        Assert.Equal(27 * S, timeline.ToSource(5 * S));
    }

    [Fact]
    public void KenardanKenaraAralikSilmeKesmeYapmaz()
    {
        var timeline = ThreeParts();

        Assert.True(timeline.DeleteRange(10 * S, 20 * S));
        Assert.Equal(new[] { new EditClip(0, 10 * S), new EditClip(20 * S, 30 * S) }, timeline.Clips);

        Assert.True(timeline.DeleteRange(0, timeline.Duration));
        Assert.Empty(timeline.Clips);
    }

    [Fact]
    public void BosVeTersAralikSilinmez()
    {
        var timeline = EditTimeline.FromSource(10 * S);

        Assert.False(timeline.DeleteRange(3 * S, 3 * S));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.DeleteRange(4 * S, 3 * S));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.DeleteRange(-1, 3 * S));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.DeleteRange(0, 10 * S + 1));
        Assert.False(timeline.CanUndo);
    }

    [Fact]
    public void KaynakTickindenDarAralikYavasParcadaHicbirSeyBozmaz()
    {
        var timeline = new EditTimeline(new[] { new EditClip(0, 10, 0.01m) });

        Assert.False(timeline.DeleteRange(120, 180));

        Assert.Equal(new[] { new EditClip(0, 10, 0.01m) }, timeline.Clips);
        Assert.False(timeline.CanUndo);
    }

    [Fact]
    public void HizYazmaSureyiVeYonuDegistirir()
    {
        var timeline = EditTimeline.FromSource(10 * S);

        Assert.True(timeline.SetSpeed(0, 2m));
        Assert.Equal(5 * S, timeline.Duration);
        Assert.Equal(2 * S, timeline.ToSource(S));
        Assert.Equal(S, timeline.ToTimeline(2 * S));

        Assert.True(timeline.SetSpeed(0, -2m));
        Assert.Equal(new EditClip(0, 10 * S, 2m, true), timeline.Clips[0]);
        Assert.Equal(10 * S - 1, timeline.ToSource(0));

        Assert.True(timeline.SetSpeed(0, 0.5m));
        Assert.Equal(new EditClip(0, 10 * S, 0.5m, false), timeline.Clips[0]);
        Assert.Equal(20 * S, timeline.Duration);
    }

    [Theory]
    [InlineData("-100", "100", true)]
    [InlineData("100.004", "100", false)]
    [InlineData("1.234", "1.23", false)]
    [InlineData("-0.005", "0.01", true)]
    public void HizMutlakDegerOlarak001eYuvarlanir(string written, string stored, bool reversed)
    {
        var timeline = EditTimeline.FromSource(10 * S);

        Assert.True(timeline.SetSpeed(0, Dec(written)));

        Assert.Equal(Dec(stored), timeline.Clips[0].Speed);
        Assert.Equal(reversed, timeline.Clips[0].Reversed);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.004")]
    [InlineData("-0.004")]
    [InlineData("100.005")]
    [InlineData("-250")]
    public void SifirVeSinirDisiHizReddedilir(string written)
    {
        var timeline = EditTimeline.FromSource(10 * S);

        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.SetSpeed(0, Dec(written)));

        Assert.Equal(new EditClip(0, 10 * S), timeline.Clips[0]);
        Assert.False(timeline.CanUndo);
    }

    [Fact]
    public void AyniHizYazilincaKomutAcilmaz()
    {
        var timeline = EditTimeline.FromSource(10 * S);

        Assert.False(timeline.SetSpeed(0, 1m));
        Assert.False(timeline.SetSpeed(0, 1.001m));
        Assert.False(timeline.CanUndo);
    }

    [Fact]
    public void BinKareSinirindaKesmeSureyeTickKaybettirmez()
    {
        var frame = S / 30;
        var timeline = EditTimeline.FromSource(1000 * frame + 17);
        var before = Sample(timeline);

        for (var i = 999; i >= 1; i--)
            Assert.True(timeline.Split(i * frame));

        Assert.Equal(1000, timeline.Clips.Count);
        Assert.Equal(1000 * frame + 17, timeline.Duration);
        Assert.Equal(1000 * frame + 17, timeline.Clips.Sum(c => c.SourceLength));
        for (var i = 0; i < 1000; i++)
            Assert.Equal(i * frame, timeline.ClipStart(i));
        Assert.Equal(before, Sample(timeline));
    }

    [Fact]
    public void BastanKirpmaParcaninBasindanOynatmaBasinaKadarSilerVeBoslukKapanir()
    {
        var timeline = ThreeParts();

        Assert.True(timeline.RippleTrimHead(14 * S));

        Assert.Equal(26 * S, timeline.Duration);
        Assert.Equal(new[] { new EditClip(0, 10 * S), new EditClip(14 * S, 20 * S), new EditClip(20 * S, 30 * S) }, timeline.Clips);
        Assert.Equal(14 * S, timeline.ToSource(10 * S));
    }

    [Fact]
    public void SondanKirpmaOynatmaBasindanParcaninSonunaKadarSiler()
    {
        var timeline = ThreeParts();

        Assert.True(timeline.RippleTrimTail(14 * S));

        Assert.Equal(24 * S, timeline.Duration);
        Assert.Equal(new[] { new EditClip(0, 10 * S), new EditClip(10 * S, 14 * S), new EditClip(20 * S, 30 * S) }, timeline.Clips);
        Assert.Equal(20 * S, timeline.ToSource(14 * S));
    }

    [Fact]
    public void KirpmaHizliParcadaCizelgeSuresiyleCalisir()
    {
        var timeline = EditTimeline.FromSource(20 * S);
        Assert.True(timeline.SetSpeed(0, 2m));

        Assert.True(timeline.RippleTrimHead(4 * S));

        Assert.Equal(6 * S, timeline.Duration);
        Assert.Equal(8 * S, timeline.Clips[0].SourceStart);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(10L)]
    [InlineData(30L)]
    public void KenardaKirpilacakBirSeyYok(long seconds)
    {
        var timeline = ThreeParts();

        Assert.False(timeline.RippleTrimHead(seconds * S));
        Assert.False(timeline.RippleTrimTail(seconds * S));
        Assert.Equal(30 * S, timeline.Duration);
        Assert.False(timeline.CanUndo);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(30 * S + 1)]
    public void KirpmaCizelgeDisindaReddedilir(long time)
    {
        var timeline = ThreeParts();

        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.RippleTrimHead(time));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.RippleTrimTail(time));
        Assert.Equal(30 * S, timeline.Duration);
    }

    [Fact]
    public void KenarKirpmaKaynagiKisaltirVeUzatir()
    {
        var timeline = new EditTimeline(new[] { new EditClip(10 * S, 20 * S), new EditClip(40 * S, 50 * S) }, 60 * S);

        Assert.True(timeline.TrimEdge(0, true, 5 * S));
        Assert.Equal(new EditClip(5 * S, 20 * S), timeline.Clips[0]);
        Assert.Equal(25 * S, timeline.Duration);

        Assert.True(timeline.TrimEdge(1, false, 45 * S));
        Assert.Equal(new EditClip(40 * S, 45 * S), timeline.Clips[1]);
        Assert.Equal(20 * S, timeline.Duration);

        Assert.False(timeline.TrimEdge(1, false, 45 * S));
    }

    [Fact]
    public void KenarKirpmaKaynakSiniriniGecemez()
    {
        var timeline = EditTimeline.FromSource(30 * S);
        timeline.Split(10 * S);
        timeline.Delete(0);

        Assert.Equal((0L, 30 * S - 1), timeline.TrimEdgeRange(0, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.TrimEdge(0, true, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.TrimEdge(0, false, 30 * S + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.TrimEdge(0, true, 30 * S));
        Assert.Equal(new EditClip(10 * S, 30 * S), timeline.Clips[0]);
    }

    [Fact]
    public void KenarKirpmaKomsuParcayaTasamaz()
    {
        var timeline = EditTimeline.FromSource(30 * S);
        timeline.Split(10 * S);
        timeline.Split(20 * S);
        timeline.Delete(1);

        Assert.Equal(20 * S, timeline.TrimEdgeRange(0, false).Max);
        Assert.Equal(10 * S, timeline.TrimEdgeRange(1, true).Min);
        Assert.Equal(30 * S, timeline.TrimEdgeRange(1, false).Max);
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.TrimEdge(0, false, 20 * S + 1));
        Assert.True(timeline.TrimEdge(0, false, 20 * S));
        Assert.Equal(30 * S, timeline.Duration);
        Assert.Equal(20 * S, timeline.TrimEdgeRange(1, true).Min);
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.TrimEdge(1, true, 20 * S - 1));

        var bitisik = ThreeParts();
        Assert.Throws<ArgumentOutOfRangeException>(() => bitisik.TrimEdge(1, true, 10 * S - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => bitisik.TrimEdge(1, false, 20 * S + 1));
        Assert.True(bitisik.TrimEdge(1, true, 15 * S));
        Assert.Equal(25 * S, bitisik.Duration);
    }

    [Fact]
    public void GeriParcadaBasKenariKaynaginSonunuTasir()
    {
        var timeline = new EditTimeline(new[] { new EditClip(10 * S, 20 * S, 1m, true) }, 30 * S);

        Assert.True(timeline.TrimEdge(0, true, 25 * S));

        Assert.Equal(new EditClip(10 * S, 25 * S, 1m, true), timeline.Clips[0]);
        Assert.Equal(25 * S - 1, timeline.ToSource(0));
    }

    [Fact]
    public void CokluSilmeSecilenParcalariKaldirir()
    {
        var timeline = ThreeParts();

        Assert.True(timeline.DeleteMany(new[] { 2, 0, 2 }));

        Assert.Equal(new[] { new EditClip(10 * S, 20 * S) }, timeline.Clips);
        Assert.Equal(10 * S, timeline.Duration);
        Assert.False(timeline.DeleteMany(Array.Empty<int>()));
    }

    [Fact]
    public void CokluSilmeHepsiniSilebilir()
    {
        var timeline = ThreeParts();

        Assert.True(timeline.DeleteMany(new[] { 0, 1, 2 }));

        Assert.Empty(timeline.Clips);
        Assert.Equal(0, timeline.Duration);
        Assert.Equal(new[] { 0L }, timeline.EditPoints);
    }

    [Fact]
    public void CokluSilmeListeDisiSirayiReddederVeDokunmaz()
    {
        var timeline = ThreeParts();

        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.DeleteMany(new[] { 0, 3 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.DeleteMany(new[] { -1 }));
        Assert.Equal(3, timeline.Clips.Count);
        Assert.False(timeline.CanUndo);
    }

    [Fact]
    public void DuzenlemeNoktalariParcaSinirlaridir()
    {
        var timeline = ThreeParts();
        timeline.SetSpeed(1, 2m);

        Assert.Equal(new[] { 0L, 10 * S, 15 * S, 25 * S }, timeline.EditPoints);
        Assert.Equal(timeline.Duration, timeline.EditPoints[^1]);
        for (var i = 0; i < timeline.Clips.Count; i++)
            Assert.Equal(timeline.ClipStart(i), timeline.EditPoints[i]);
    }

    private static EditTimeline ThreeParts()
        => new(new[] { new EditClip(0, 10 * S), new EditClip(10 * S, 20 * S), new EditClip(20 * S, 30 * S) });

    private static decimal Dec(string text) => decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

    private static long[] Sample(EditTimeline timeline, long? upTo = null)
    {
        var end = upTo ?? timeline.Duration;
        var points = new List<long>();
        for (long t = 0; t < end; t += Math.Max(1, end / 997)) points.Add(t);
        points.Add(end - 1);
        return points.Select(timeline.ToSource).ToArray();
    }
}
