using VidShrink.App.Playback;

namespace VidShrink.Tests;

/// <summary>
/// K3: jest matematiği saf bir sınıf ve burada sınanıyor. Hiçbir testte pencere açılmıyor,
/// çünkü <see cref="ZoomGesture"/> tek bir Avalonia türü kullanmıyor.
/// </summary>
public class ZoomGestureTests
{
    private static ZoomGesture Fitted(double zoomCeiling = 4.0)
    {
        var gesture = new ZoomGesture(zoomCeiling);
        gesture.SetViewport(800, 450);
        gesture.SetSource(1600, 900);
        return gesture;
    }

    [Fact]
    public void Baslangicta_taban_ve_bandinda()
    {
        var gesture = Fitted();

        Assert.Equal(0, gesture.T);
        Assert.Equal(ShelterStage.Band, gesture.Shelter);
        Assert.False(gesture.Promoted);
        Assert.True(gesture.AtFloor);
        Assert.Equal(1.0, gesture.ContentZoom, 9);
    }

    [Fact]
    public void Tabanda_kaynak_panoya_tam_sigar()
    {
        var gesture = Fitted();

        Assert.Equal(800, gesture.ContentWidth, 6);
        Assert.Equal(450, gesture.ContentHeight, 6);
        Assert.Equal(0, gesture.OffsetX, 6);
        Assert.Equal(0, gesture.OffsetY, 6);
        Assert.False(gesture.CanPan);
    }

    [Fact]
    public void Tavana_varinca_tekerlek_durur()
    {
        var gesture = Fitted();

        var moved = 0;
        for (var i = 0; i < 100; i++)
            if (gesture.Wheel(1, 400, 225)) moved++;

        Assert.Equal(ZoomGesture.Ceiling, gesture.T, 9);
        Assert.True(gesture.AtCeiling);
        // Son çentik tavana dayanıp kırpılır; ondan sonrası boşa döner.
        Assert.Equal((int)Math.Ceiling(ZoomGesture.Ceiling / ZoomGesture.NotchStep), moved);
        Assert.False(gesture.Wheel(1, 400, 225));
    }

    [Fact]
    public void Tam_pencere_esigi_bir_nokta_sifirdir()
    {
        var gesture = Fitted();

        while (gesture.Wheel(1, 400, 225)) { }

        Assert.Equal(ZoomGesture.FullAt, gesture.T, 9);
        Assert.Equal(ShelterStage.Full, gesture.Shelter);
        Assert.True(gesture.Promoted);
    }

    [Fact]
    public void Uc_kademe_de_tek_parametreden_cikar()
    {
        var gesture = Fitted();

        Assert.Equal(ShelterStage.Band, gesture.Shelter);

        while (gesture.T < ZoomGesture.MidAt && gesture.Wheel(1, 400, 225)) { }
        Assert.Equal(ShelterStage.Mid, gesture.Shelter);
        Assert.True(gesture.T < ZoomGesture.FullAt);

        while (gesture.Wheel(1, 400, 225)) { }
        Assert.Equal(ShelterStage.Full, gesture.Shelter);
    }

    [Fact]
    public void Orta_kademe_yan_sutunlara_tasarken_pencereyi_kaplamaz()
    {
        var gesture = Fitted();

        // Orta kademeye ilk varılan nokta: eşiğin üstündeki ilk çentik.
        while (gesture.Shelter == ShelterStage.Band) gesture.Wheel(1, 400, 225);

        Assert.Equal(ShelterStage.Mid, gesture.Shelter);
        Assert.True(gesture.Promoted);
        Assert.False(gesture.AtCeiling);
    }

    [Fact]
    public void Hicbir_kademe_tek_centikle_atlanmaz()
    {
        var up = Fitted();
        var previous = (int)up.Shelter;
        while (up.Wheel(1, 400, 225))
        {
            var now = (int)up.Shelter;
            Assert.True(now - previous <= 1, $"t={up.T:0.###} kademesi {previous} -> {now}");
            previous = now;
        }

        var down = Fitted();
        while (down.Wheel(1, 400, 225)) { }
        previous = (int)down.Shelter;
        while (down.Wheel(-1, 400, 225))
        {
            var now = (int)down.Shelter;
            Assert.True(previous - now <= 1, $"t={down.T:0.###} kademesi {previous} -> {now}");
            previous = now;
        }
    }

    [Fact]
    public void Histerezis_tek_centikte_indirmez()
    {
        var gesture = Fitted();
        while (gesture.Wheel(1, 400, 225)) { }

        // Bir çentik geri: t iniş eşiğine değer ama altına inmez, panel tam penceredir.
        gesture.Wheel(-1, 400, 225);

        Assert.Equal(ZoomGesture.FullDropAt, gesture.T, 9);
        Assert.Equal(ShelterStage.Full, gesture.Shelter);
    }

    [Fact]
    public void Histerezis_esigin_altinda_bir_kademe_indirir()
    {
        var gesture = Fitted();
        while (gesture.Wheel(1, 400, 225)) { }

        gesture.Wheel(-2, 400, 225);

        Assert.True(gesture.T < ZoomGesture.FullDropAt);
        Assert.Equal(ShelterStage.Mid, gesture.Shelter);
        Assert.True(gesture.Promoted);
    }

    [Fact]
    public void Orta_kademenin_kendi_histerezisi_var()
    {
        var gesture = Fitted();
        while (gesture.Shelter == ShelterStage.Band) gesture.Wheel(1, 400, 225);

        // Çıkış eşiğinin altına inildi ama iniş eşiğinin üstünde kalındı: kademe durur.
        gesture.Wheel(-1, 400, 225);
        Assert.True(gesture.T < ZoomGesture.MidAt);
        Assert.True(gesture.T >= ZoomGesture.MidDropAt);
        Assert.Equal(ShelterStage.Mid, gesture.Shelter);

        gesture.Wheel(-1, 400, 225);
        Assert.True(gesture.T < ZoomGesture.MidDropAt);
        Assert.Equal(ShelterStage.Band, gesture.Shelter);
    }

    [Fact]
    public void Titrek_tekerlek_kademeler_arasinda_cirpinmaz()
    {
        var full = Fitted();
        while (full.Wheel(1, 400, 225)) { }

        for (var i = 0; i < 20; i++)
        {
            full.Wheel(-1, 400, 225);
            Assert.Equal(ShelterStage.Full, full.Shelter);
            full.Wheel(1, 400, 225);
            Assert.Equal(ShelterStage.Full, full.Shelter);
        }

        var mid = Fitted();
        while (mid.Shelter == ShelterStage.Band) mid.Wheel(1, 400, 225);

        for (var i = 0; i < 20; i++)
        {
            mid.Wheel(-1, 400, 225);
            Assert.Equal(ShelterStage.Mid, mid.Shelter);
            mid.Wheel(1, 400, 225);
            Assert.Equal(ShelterStage.Mid, mid.Shelter);
        }
    }

    [Fact]
    public void Zaman_asimi_parametreyi_de_tabana_indirir()
    {
        var gesture = Fitted();
        while (gesture.Wheel(1, 400, 225)) { }
        Assert.Equal(ShelterStage.Full, gesture.Shelter);

        // İniş sayacının zaman aşımı bu yoldan geçer (ComparisonPanel.Descend).
        gesture.Demote();

        Assert.Equal(0, gesture.T, 9);
        Assert.Equal(ShelterStage.Band, gesture.Shelter);

        // Tuzak 1: parametre tavanda kalsaydı bu tek dokunuş paneli tam pencereye atardı.
        gesture.Wheel(1, 400, 225);
        Assert.Equal(ZoomGesture.NotchStep, gesture.T, 9);
        Assert.Equal(ShelterStage.Band, gesture.Shelter);
    }

    [Fact]
    public void Imlecin_altindaki_nokta_sabit_kalir()
    {
        var gesture = Fitted();
        const double anchorX = 640;
        const double anchorY = 300;

        var before = SourceAt(gesture, anchorX, anchorY);
        gesture.Wheel(3, anchorX, anchorY);
        var after = SourceAt(gesture, anchorX, anchorY);

        Assert.Equal(before.X, after.X, 6);
        Assert.Equal(before.Y, after.Y, 6);
        Assert.True(gesture.T > 0);
    }

    [Fact]
    public void Yakinlasma_panonun_ortasina_degil_imlece_gider()
    {
        var corner = Fitted();
        var centre = Fitted();

        corner.Wheel(2, 0, 0);
        centre.Wheel(2, 400, 225);

        Assert.Equal(corner.T, centre.T, 9);
        Assert.NotEqual(corner.OffsetX, centre.OffsetX, 6);
    }

    [Fact]
    public void Pano_kaynagin_disina_cikamaz()
    {
        var gesture = Fitted();
        while (gesture.Wheel(1, 400, 225)) { }

        gesture.Drag(100000, 100000);
        Assert.Equal(0, gesture.OffsetX, 6);
        Assert.Equal(0, gesture.OffsetY, 6);

        gesture.Drag(-100000, -100000);
        Assert.Equal(800 - gesture.ContentWidth, gesture.OffsetX, 6);
        Assert.Equal(450 - gesture.ContentHeight, gesture.OffsetY, 6);
    }

    [Fact]
    public void Sigiyorken_surukleme_bir_sey_yapmaz()
    {
        var gesture = Fitted();

        Assert.False(gesture.CanPan);
        Assert.False(gesture.Drag(50, 50));
        Assert.Equal(0, gesture.OffsetX, 6);
    }

    [Fact]
    public void Yakinlasinca_suruklenebilir()
    {
        var gesture = Fitted();
        gesture.Wheel(4, 400, 225);

        Assert.True(gesture.CanPan);
        Assert.True(gesture.Drag(-10, 0));
    }

    [Fact]
    public void Esc_inisi_tabana_dondurur_ve_panoyu_ortalar()
    {
        var gesture = Fitted();
        while (gesture.Wheel(1, 0, 0)) { }
        gesture.Drag(-50, -20);

        gesture.Demote();

        Assert.Equal(0, gesture.T, 9);
        Assert.Equal(ShelterStage.Band, gesture.Shelter);
        Assert.Equal(0, gesture.OffsetX, 6);
        Assert.Equal(0, gesture.OffsetY, 6);
    }

    [Fact]
    public void Terfi_dugmesi_parametreyi_tavana_tasir()
    {
        var gesture = Fitted();

        gesture.Promote();

        Assert.True(gesture.AtCeiling);
        Assert.Equal(ShelterStage.Full, gesture.Shelter);
        Assert.False(gesture.Wheel(1, 400, 225));
    }

    [Fact]
    public void Tek_parametre_hem_paneli_hem_goruntuyu_surer()
    {
        var gesture = Fitted(zoomCeiling: 4.0);

        gesture.Wheel(1, 400, 225);
        var quarter = gesture.ContentZoom;
        gesture.Wheel(1, 400, 225);
        var half = gesture.ContentZoom;

        // İki ayrı sayaç yok: yakınlaştırma t ile doğrusal, terfi de aynı t ile karar veriliyor.
        Assert.True(half > quarter);
        Assert.Equal(1.0 + gesture.T * 3.0, gesture.ContentZoom, 9);
    }

    [Fact]
    public void Taban_altina_inilemez()
    {
        var gesture = Fitted();

        Assert.False(gesture.Wheel(-1, 400, 225));
        Assert.Equal(0, gesture.T, 9);
    }

    [Fact]
    public void Pano_olcusu_degisince_sigan_goruntu_ortalanir()
    {
        var gesture = Fitted();

        gesture.SetViewport(1000, 450);

        Assert.Equal((1000 - gesture.ContentWidth) / 2, gesture.OffsetX, 6);
    }


    /// <summary>
    /// T46/K5: dogrudan yakinlastirma. Hedef parametre tavandan turer - 4x tavanda 2x,
    /// araligin ucte biridir. Sayi testte de uydurulmuyor, tavandan hesaplaniyor.
    /// </summary>
    [Theory]
    [InlineData(4.0, 2.0)]
    [InlineData(4.0, 3.0)]
    [InlineData(8.0, 2.0)]
    public void ZoomToTavandanTuretir(double ceiling, double wanted)
    {
        var gesture = new ZoomGesture(ceiling);
        Assert.True(gesture.ZoomTo(wanted, 0, 0));
        Assert.Equal(wanted, gesture.ContentZoom, 9);
        Assert.Equal((wanted - 1.0) / (ceiling - 1.0), gesture.T, 9);
    }

    /// <summary>Bire donmek tabana donmektir; iki yol da tek parametreye yaziyor.</summary>
    [Fact]
    public void ZoomToBirTabanaDoner()
    {
        var gesture = new ZoomGesture();
        gesture.ZoomTo(2.0, 0, 0);
        Assert.False(gesture.AtFloor);
        Assert.True(gesture.ZoomTo(1.0, 0, 0));
        Assert.True(gesture.AtFloor);
    }

    /// <summary>
    /// T46/K5, ikinci tuzak: 2x'in parametresi orta kademe esiginin altinda kalir, yani
    /// giris yakinlastirmasi paneli terfi ettirmez ve inis sayaciyla salinmaz.
    /// </summary>
    [Fact]
    public void GirisYakinlastirmasiKademeyiOynatmaz()
    {
        var gesture = new ZoomGesture();
        for (var i = 0; i < 5; i++)
        {
            gesture.ZoomTo(2.0, 0, 0);
            Assert.Equal(ShelterStage.Band, gesture.Shelter);
            gesture.ZoomTo(1.0, 0, 0);
            Assert.Equal(ShelterStage.Band, gesture.Shelter);
        }

        Assert.True(gesture.AtFloor);
    }

    /// <summary>Wheel centik centik ilerler ve geri alir; dugme yolunun kaniti ComparisonPanelTests'te.</summary>
    [Fact]
    public void WheelCentikCentikIlerlerVeGeriAlir()
    {
        var gesture = new ZoomGesture();
        gesture.Wheel(1, 0, 0);
        Assert.Equal(ZoomGesture.NotchStep, gesture.T, 9);
        gesture.Wheel(1, 0, 0);
        Assert.Equal(2 * ZoomGesture.NotchStep, gesture.T, 9);
        gesture.Wheel(-1, 0, 0);
        Assert.Equal(ZoomGesture.NotchStep, gesture.T, 9);
    }

    private static (double X, double Y) SourceAt(ZoomGesture gesture, double x, double y)
        => ((x - gesture.OffsetX) / gesture.Scale, (y - gesture.OffsetY) / gesture.Scale);
}
