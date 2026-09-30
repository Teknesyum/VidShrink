using VidShrink.Core.Editing;
using Xunit;

namespace VidShrink.Tests;

public sealed class DuzenleyiciDalgaTests
{
    private static short[] Kovalar(params (short Min, short Max)[] kovalar)
    {
        var ornekler = new short[kovalar.Length * AudioPeaks.SamplesPerBucket];
        for (var k = 0; k < kovalar.Length; k++)
            for (var i = 0; i < AudioPeaks.SamplesPerBucket; i++)
                ornekler[k * AudioPeaks.SamplesPerBucket + i] = i % 2 == 0 ? kovalar[k].Min : kovalar[k].Max;
        return ornekler;
    }

    [Fact]
    public void ZamanBirimiCizelgeyleAyni()
    {
        Assert.Equal(EditTime.TicksPerSecond, AudioPeaks.TicksPerBucket * AudioPeaks.BucketsPerSecond);
        Assert.Equal(EditTime.TicksPerSecond, AudioPeaks.TicksPerSample * AudioPeaks.SampleRate);
        var tepe = AudioPeaks.FromPcm(new short[AudioPeaks.SampleRate * 2 + 40]);
        Assert.Equal(EditTime.FromSeconds(2.005), tepe.Duration);
        Assert.Equal(201, tepe.Count);
    }

    [Fact]
    public void KovalarMinVeMaksiTutar()
    {
        var ornekler = new short[250];
        for (var i = 0; i < ornekler.Length; i++) ornekler[i] = (short)(i * 10);
        ornekler[5] = -300;
        ornekler[170] = short.MinValue;
        ornekler[245] = short.MaxValue;

        var tepe = AudioPeaks.FromPcm(ornekler);

        Assert.Equal(4, tepe.Count);
        Assert.Equal(250, tepe.SampleCount);
        Assert.Equal(new PeakBucket(-300, 790), tepe[0]);
        Assert.Equal(new PeakBucket(800, 1590), tepe[1]);
        Assert.Equal(new PeakBucket(short.MinValue, 2390), tepe[2]);
        Assert.Equal(new PeakBucket(2400, short.MaxValue), tepe[3]);
    }

    [Fact]
    public void ParcaliBaytAkisiTekParcayaEsit()
    {
        var rastgele = new Random(7);
        var ornekler = new short[AudioPeaks.SamplesPerBucket * 37 + 13];
        for (var i = 0; i < ornekler.Length; i++) ornekler[i] = (short)rastgele.Next(short.MinValue, short.MaxValue + 1);
        var baytlar = new byte[ornekler.Length * 2];
        Buffer.BlockCopy(ornekler, 0, baytlar, 0, baytlar.Length);

        var kurucu = new PeakBuilder();
        for (var i = 0; i < baytlar.Length; i += 7)
            kurucu.Add(baytlar.AsSpan(i, Math.Min(7, baytlar.Length - i)));
        var parcali = kurucu.Build();
        var tek = AudioPeaks.FromPcm(ornekler);

        Assert.Equal(tek.Count, parcali.Count);
        Assert.Equal(tek.SampleCount, parcali.SampleCount);
        for (var i = 0; i < tek.Count; i++) Assert.Equal(tek[i], parcali[i]);
    }

    [Fact]
    public void BirebirDilimKovalariAynenVerir()
    {
        var tepe = AudioPeaks.FromPcm(Kovalar((-1, 1), (-2, 2), (-3, 3), (-4, 4)));

        var dilim = tepe.Slice(0, 4 * AudioPeaks.TicksPerBucket, false, 4);

        Assert.Equal(new[] { new PeakBucket(-1, 1), new PeakBucket(-2, 2), new PeakBucket(-3, 3), new PeakBucket(-4, 4) }, dilim);
    }

    [Fact]
    public void SeyrekDilimMinMaksiKorur()
    {
        var tepe = AudioPeaks.FromPcm(Kovalar((-10, 5), (-1, 40), (-3, 3), (-70, 2)));

        var dilim = tepe.Slice(0, 4 * AudioPeaks.TicksPerBucket, false, 2);

        Assert.Equal(new[] { new PeakBucket(-10, 40), new PeakBucket(-70, 3) }, dilim);
    }

    [Fact]
    public void SikDilimKapsayanKovayiTekrarlar()
    {
        var tepe = AudioPeaks.FromPcm(Kovalar((-1, 1), (-9, 9)));

        var dilim = tepe.Slice(AudioPeaks.TicksPerBucket, 2 * AudioPeaks.TicksPerBucket, false, 5);

        Assert.All(dilim, kova => Assert.Equal(new PeakBucket(-9, 9), kova));
    }

    [Fact]
    public void SaniyeAraligiDogruKaynagiSecer()
    {
        var ornekler = new short[AudioPeaks.SampleRate * 3];
        for (var i = 0; i < ornekler.Length; i++)
        {
            var genlik = (short)((i / AudioPeaks.SampleRate + 1) * 1000);
            ornekler[i] = i % 2 == 0 ? genlik : (short)-genlik;
        }
        var tepe = AudioPeaks.FromPcm(ornekler);
        const long sn = EditTime.TicksPerSecond;
        var parca = new EditClip(sn, 2 * sn);

        var dilim = tepe.Slice(parca.SourceStart, parca.SourceEnd, parca.Reversed, 1);

        Assert.Equal(new PeakBucket(-2000, 2000), dilim[0]);
    }

    [Fact]
    public void TersDilimSirayiCevirir()
    {
        var tepe = AudioPeaks.FromPcm(Kovalar((-1, 1), (-2, 2), (-3, 3), (-4, 4), (-5, 5), (-6, 6)));
        var bas = AudioPeaks.TicksPerBucket;
        var son = 5 * AudioPeaks.TicksPerBucket;

        var ileri = tepe.Slice(bas, son, false, 4);
        var geri = tepe.Slice(bas, son, true, 4);

        Assert.Equal(new[] { new PeakBucket(-2, 2), new PeakBucket(-3, 3), new PeakBucket(-4, 4), new PeakBucket(-5, 5) }, ileri);
        Assert.Equal(ileri.Reverse(), geri);
    }

    [Fact]
    public void KaynakSonununOtesiSifirdir()
    {
        var tepe = AudioPeaks.FromPcm(Kovalar((-5, 5), (-6, 6)));

        var tasan = tepe.Slice(0, 4 * AudioPeaks.TicksPerBucket, false, 4);
        var disarida = tepe.Slice(10 * AudioPeaks.TicksPerBucket, 12 * AudioPeaks.TicksPerBucket, false, 3);
        var tersTasan = tepe.Slice(0, 4 * AudioPeaks.TicksPerBucket, true, 4);

        Assert.Equal(new[] { new PeakBucket(-5, 5), new PeakBucket(-6, 6), default, default }, tasan);
        Assert.All(disarida, kova => Assert.Equal(default, kova));
        Assert.Equal(new[] { default, default, new PeakBucket(-6, 6), new PeakBucket(-5, 5) }, tersTasan);
    }

    [Fact]
    public void KisaAralikCokKovayaBolunebilir()
    {
        var tepe = AudioPeaks.FromPcm(Kovalar((-5, 5), (-6, 6)));

        var dilim = tepe.Slice(AudioPeaks.TicksPerBucket - 1, AudioPeaks.TicksPerBucket + 1, false, 10);

        Assert.Equal(10, dilim.Length);
        Assert.Contains(new PeakBucket(-5, 5), dilim);
        Assert.Contains(new PeakBucket(-6, 6), dilim);
    }

    [Fact]
    public void GecersizDilimReddedilir()
    {
        var tepe = AudioPeaks.FromPcm(Kovalar((-5, 5)));

        Assert.Throws<ArgumentOutOfRangeException>(() => tepe.Slice(-1, 10, false, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => tepe.Slice(10, 10, false, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => tepe.Slice(0, 10, false, 0));
    }

    [Fact]
    public void BosTepeSifirKovaVerir()
    {
        Assert.True(AudioPeaks.Empty.IsEmpty);
        Assert.Same(AudioPeaks.Empty, AudioPeaks.FromPcm(ReadOnlySpan<short>.Empty));

        var dilim = AudioPeaks.Empty.Slice(0, EditTime.TicksPerSecond, true, 8);

        Assert.Equal(8, dilim.Length);
        Assert.All(dilim, kova => Assert.Equal(default, kova));
    }

    [Fact]
    public void OnDakikaTepesiBirMegabaytinAltinda()
    {
        var ornekler = new short[AudioPeaks.SampleRate * 600];
        for (var i = 0; i < ornekler.Length; i++) ornekler[i] = (short)(i % 2000 - 1000);

        var tepe = AudioPeaks.FromPcm(ornekler);

        Assert.Equal(600 * AudioPeaks.BucketsPerSecond, tepe.Count);
        Assert.Equal(EditTime.FromSeconds(600), tepe.Duration);
        Assert.True(tepe.ByteSize < 1024 * 1024, $"tepe verisi {tepe.ByteSize} bayt");
    }

    [Fact]
    public void SessizlikHatasiTaninir()
    {
        Assert.True(AudioPeaks.ReportsNoAudio("Output file #0 does not contain any stream"));
        Assert.True(AudioPeaks.ReportsNoAudio("Stream map '0:a:0' matches no streams."));
        Assert.False(AudioPeaks.ReportsNoAudio("No such file or directory"));
    }

    [Fact]
    public void ArgumanlarDusukYukVeTekSesAkisiIster()
    {
        var argumanlar = AudioPeaks.Arguments("C:\\a b.mp4");

        Assert.Equal(new[] { "-threads", "1", "-i", "C:\\a b.mp4" }, argumanlar.Skip(4).Take(4));
        Assert.Contains("0:a:0?", argumanlar);
        Assert.Equal(new[] { "-ac", "1", "-ar", "8000" }, argumanlar.SkipWhile(a => a != "-ac").Take(4));
        Assert.Equal(new[] { "-f", "s16le", "pipe:1" }, argumanlar.TakeLast(3));
        Assert.Equal(2, argumanlar.Count(a => a == "-threads"));
    }
}
