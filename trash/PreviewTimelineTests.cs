using VidShrink.Core;

namespace VidShrink.Tests;

public sealed class PreviewTimelineTests
{
    private static MediaInfo Source(double durationSeconds = 60, double fps = 60, int width = 1920, int height = 1080) => new()
    {
        FilePath = "kaynak.mp4",
        FileSizeBytes = 40_000_000,
        DurationSeconds = durationSeconds,
        Width = width,
        Height = height,
        Fps = fps,
        VideoCodec = "h264",
        TotalBitrateBps = 5_700_000
    };

    private static EncodePlan Plan(double fps = 30) => new() { Fps = fps, Width = 1280, Height = 720 };

    [Fact]
    public void Eksen_ciktinin_kare_izgarasina_kilitlenir()
    {
        var timeline = PreviewTimeline.For(Source(fps: 60), Plan(fps: 30));

        // 30 fps ciktida kare izgarasi 1/30 sn; 10,04 sn diye bir kare yok.
        var point = timeline.FromOutput(10.04);

        Assert.Equal(301, point.OutputFrame);
        Assert.Equal(301 / 30.0, point.OutputSeconds, 6);
    }

    [Fact]
    public void Kaynak_zamani_ciktidan_turetilir_ters_yonde_degil()
    {
        var timeline = PreviewTimeline.For(Source(), Plan(fps: 30), trimStartSeconds: 5);

        var point = timeline.FromOutput(10.0);

        // Cikti 0 sn kaynagin 5. saniyesidir; kirpma eksene eklenir.
        Assert.Equal(15.0, point.SourceSeconds, 6);
    }

    [Fact]
    public void Fps_dususunde_bir_kareden_buyuk_sapma_bildirilir()
    {
        // Kaynak 60 fps, cikti 5 fps: cikti kareleri 0,2 sn arayla. Istenen an iki karenin
        // tam ortasina duserse sapma 0,1 sn, kaynak karesi 1/60 = 0,0167 sn.
        var timeline = PreviewTimeline.For(Source(fps: 60), Plan(fps: 5));

        var point = timeline.FromOutput(10.1);

        Assert.True(point.DriftSeconds > 1.0 / 60.0);
        Assert.True(point.DriftExceedsSourceFrame);
    }

    [Fact]
    public void Izgaraya_tam_oturan_an_sapma_bildirmez()
    {
        var timeline = PreviewTimeline.For(Source(fps: 60), Plan(fps: 30));

        var point = timeline.FromOutput(10.0);

        Assert.Equal(0.0, point.DriftSeconds, 6);
        Assert.False(point.DriftExceedsSourceFrame);
    }

    [Fact]
    public void Kaynak_ekseninden_giris_ayni_izgaraya_oturur()
    {
        var timeline = PreviewTimeline.For(Source(fps: 60), Plan(fps: 30), trimStartSeconds: 5);

        var point = timeline.FromSource(15.04);

        // Kirpma cikarilinca istenen an 10,04 sn. 30 fps izgarada komsu kareler 10,0333 ve
        // 10,0667; en yakini 301. kare. Kaynak karsiligi kirpma geri eklenerek bulunur.
        Assert.Equal(301, point.OutputFrame);
        Assert.Equal(timeline.FromOutput(10.04).OutputFrame, point.OutputFrame);
        Assert.Equal(5 + 301 / 30.0, point.SourceSeconds, 6);
    }

    [Fact]
    public void Izgaraya_oturma_en_yakina_yuvarlar_asagi_kirpmaz()
    {
        var timeline = PreviewTimeline.For(Source(fps: 60), Plan(fps: 30));

        // 10,04 sn 301. kareye daha yakin; asagi kirpsaydik 300 gelirdi.
        Assert.Equal(301, timeline.FromOutput(10.04).OutputFrame);
        // 10,01 sn ise 300. kareye daha yakin.
        Assert.Equal(300, timeline.FromOutput(10.01).OutputFrame);
    }

    [Fact]
    public void Kirpma_ciktinin_suresini_ve_kare_sayisini_belirler()
    {
        var timeline = PreviewTimeline.For(Source(durationSeconds: 60), Plan(fps: 30), trimStartSeconds: 10, trimDurationSeconds: 20);

        Assert.Equal(20.0, timeline.OutputDurationSeconds, 6);
        Assert.Equal(600, timeline.OutputFrameCount);
        Assert.True(timeline.WasTrimmed);
    }

    [Fact]
    public void Eksen_disina_cikan_giris_kirpilir()
    {
        var timeline = PreviewTimeline.For(Source(durationSeconds: 60), Plan(fps: 30));

        // 60 sn @30 fps = 1800 kare, son kare 1799.
        Assert.Equal(1800, timeline.OutputFrameCount);
        Assert.Equal(0, timeline.FromOutput(-5).OutputFrame);
        Assert.Equal(1799, timeline.FromOutput(999).OutputFrame);

        // Kare numarasi da kirpilir — ama yalniz gercekten disaridaysa.
        Assert.Equal(1799, timeline.FromFrame(9999).OutputFrame);
        Assert.Equal(0, timeline.FromFrame(-3).OutputFrame);
        Assert.Equal(999, timeline.FromFrame(999).OutputFrame);
    }

    [Fact]
    public void Fps_dusurulmemisse_dusus_bildirilmez()
    {
        var timeline = PreviewTimeline.For(Source(fps: 30), Plan(fps: 30));

        Assert.False(timeline.FpsWasReduced);
        Assert.False(timeline.WasTrimmed);
    }

    [Theory]
    [InlineData(false, false, false, false, false, PreviewState.KaynakYok)]
    [InlineData(true, false, false, false, false, PreviewState.YalnizKaynak)]
    [InlineData(true, true, true, false, false, PreviewState.TamKodlama)]
    [InlineData(true, true, false, true, false, PreviewState.GercekCikti)]
    [InlineData(true, true, false, true, true, PreviewState.Olculemedi)]
    public void Durum_tek_yerden_turetilir(bool hasSource, bool hasPlan, bool isEncoding, bool hasRealOutput, bool grabFailed, PreviewState expected)
        => Assert.Equal(expected, PreviewStatus.Derive(hasSource, hasPlan, isEncoding, hasRealOutput, grabFailed));

    [Fact]
    public void Ornek_kodlama_baslayinca_durum_ornek_kodlaniyor()
    {
        var state = PreviewStatus.Derive(true, true, false, false, false, sampleEncoding: true);

        Assert.Equal(PreviewState.OrnekKodlaniyor, state);
        Assert.False(PreviewStatus.HasRightHalf(state));
    }

    [Fact]
    public void Ornek_kodlama_bitince_sag_yari_dolar()
    {
        var state = PreviewStatus.Derive(true, true, false, false, false, sampleEncoding: false, hasSample: true);

        Assert.Equal(PreviewState.OrnekKodlama, state);
        Assert.True(PreviewStatus.HasRightHalf(state));
    }

    [Fact]
    public void Plan_yokken_ornek_kodlama_durumu_dogmaz()
        => Assert.Equal(PreviewState.YalnizKaynak, PreviewStatus.Derive(true, false, false, false, false, sampleEncoding: true, hasSample: true));

    [Fact]
    public void Kodlama_surerken_kare_cekimi_kapali()
    {
        // T30/O2: kodlama yavaslamasi %17,8-28,4, konseyin %5 kurali asildi.
        Assert.False(PreviewStatus.AllowsFrameGrab(PreviewState.OrnekKodlama));
        Assert.False(PreviewStatus.AllowsFrameGrab(PreviewState.OrnekKodlaniyor));
        Assert.False(PreviewStatus.AllowsFrameGrab(PreviewState.TamKodlama));
        Assert.True(PreviewStatus.AllowsFrameGrab(PreviewState.GercekCikti));
        Assert.True(PreviewStatus.AllowsFrameGrab(PreviewState.YalnizKaynak));
        Assert.False(PreviewStatus.AllowsFrameGrab(PreviewState.KaynakYok));
        Assert.False(PreviewStatus.AllowsFrameGrab(PreviewState.Olculemedi));
    }

    [Fact]
    public void Sag_yari_yalniz_ornek_ve_gercek_ciktida_dolu()
    {
        Assert.True(PreviewStatus.HasRightHalf(PreviewState.OrnekKodlama));
        Assert.True(PreviewStatus.HasRightHalf(PreviewState.GercekCikti));
        Assert.False(PreviewStatus.HasRightHalf(PreviewState.OrnekKodlaniyor));
        Assert.False(PreviewStatus.HasRightHalf(PreviewState.TamKodlama));
        Assert.False(PreviewStatus.HasRightHalf(PreviewState.YalnizKaynak));
    }
}
