using Avalonia.Controls;
using VidShrink.App;
using VidShrink.App.Playback;

namespace VidShrink.Tests;

/// <summary>
/// K3: jest matematiği saf bir sınıf ve burada sınanıyor. Hiçbir testte pencere açılmıyor,
/// çünkü <see cref="ZoomGesture"/> tek bir Avalonia türü kullanmıyor.
///
/// T52: ölçülen büyüklük panelin boy ölçeğidir. Görüntü panelin içinde sığdırma ölçeğinde
/// durur ve jestle büyümez; jestin büyüttüğü şey panelin kendisidir.
/// </summary>
public class ZoomGestureTests
{
    private static ZoomGesture Fitted(double scaleCeiling = 4.0)
    {
        var gesture = new ZoomGesture(scaleCeiling);
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
        Assert.Equal(1.0, gesture.PanelScale, 9);
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

        while (gesture.T < gesture.MidAt && gesture.Wheel(1, 400, 225)) { }
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
        Assert.True(gesture.T < gesture.MidAt);
        Assert.True(gesture.T >= gesture.MidDropAt);
        Assert.Equal(ShelterStage.Mid, gesture.Shelter);

        gesture.Wheel(-1, 400, 225);
        Assert.True(gesture.T < gesture.MidDropAt);
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

    /// <summary>
    /// T52: çıpanın yeri artık sonucu değiştirmez. Görüntü ölçeklenmediği için imlecin
    /// altındaki nokta zaten sabittir ve pano hep ortalı kalır.
    /// </summary>
    [Fact]
    public void Cipa_nerede_olursa_olsun_sonuc_aynidir()
    {
        var corner = Fitted();
        var centre = Fitted();

        corner.Wheel(2, 0, 0);
        centre.Wheel(2, 400, 225);

        Assert.Equal(corner.T, centre.T, 9);
        Assert.Equal(corner.OffsetX, centre.OffsetX, 6);
        Assert.Equal(corner.OffsetY, centre.OffsetY, 6);
    }

    /// <summary>
    /// T52/K1: jest görüntüyü büyütmez. Panel ölçeği tavana çıksa da görüntü panoya sığmış
    /// hâlde kalır, bu yüzden sürüklenecek bir yer de oluşmaz.
    /// </summary>
    [Fact]
    public void Olcek_buyurken_goruntu_panoya_sigmis_kalir()
    {
        var gesture = Fitted();
        while (gesture.Wheel(1, 400, 225)) { }

        Assert.True(gesture.AtCeiling);
        Assert.Equal(800, gesture.ContentWidth, 6);
        Assert.Equal(450, gesture.ContentHeight, 6);
        Assert.False(gesture.CanPan);
        Assert.False(gesture.Drag(50, 50));
        Assert.Equal(0, gesture.OffsetX, 6);
        Assert.Equal(0, gesture.OffsetY, 6);
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

    /// <summary>T52/K1: tek parametre hem panelin boyunu hem kademesini sürer.</summary>
    [Fact]
    public void Tek_parametre_hem_olcegi_hem_kademeyi_surer()
    {
        var gesture = Fitted(scaleCeiling: 4.0);

        gesture.Wheel(1, 400, 225);
        var first = gesture.PanelScale;
        gesture.Wheel(1, 400, 225);
        var second = gesture.PanelScale;

        // İki ayrı sayaç yok: ölçek t ile doğrusal, terfi de aynı t ile karar veriliyor.
        Assert.True(second > first);
        Assert.Equal(1.0 + gesture.T * 3.0, gesture.PanelScale, 9);
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
    /// T52: dogrudan panel olcegi. Hedef parametre tavandan turer - 4x tavanda 2x,
    /// araligin ucte biridir. Sayi testte de uydurulmuyor, tavandan hesaplaniyor.
    /// </summary>
    [Theory]
    [InlineData(4.0, 2.0)]
    [InlineData(4.0, 3.0)]
    [InlineData(8.0, 2.0)]
    public void ScaleToTavandanTuretir(double ceiling, double wanted)
    {
        var gesture = new ZoomGesture(ceiling);
        Assert.True(gesture.ScaleTo(wanted, 0, 0));
        Assert.Equal(wanted, gesture.PanelScale, 9);
        Assert.Equal((wanted - 1.0) / (ceiling - 1.0), gesture.T, 9);
    }

    /// <summary>Bire donmek tabana donmektir; iki yol da tek parametreye yaziyor.</summary>
    [Fact]
    public void ScaleToBirTabanaDoner()
    {
        var gesture = new ZoomGesture();
        gesture.ScaleTo(2.0, 0, 0);
        Assert.False(gesture.AtFloor);
        Assert.True(gesture.ScaleTo(1.0, 0, 0));
        Assert.True(gesture.AtFloor);
    }

    /// <summary>
    /// T52/K2: fare panele girince panel iki katina cikar. Iki kat panel bandina sigmayan
    /// ilk boy oldugu icin bu ayni zamanda orta kademenin esigidir - panel kok katmana
    /// terfi eder. Tabana donmek kademeyi de bandina indirir.
    /// </summary>
    [Fact]
    public void GirisOlceklemesiPaneliOrtaKademeyeCikarir()
    {
        var gesture = new ZoomGesture();
        for (var i = 0; i < 5; i++)
        {
            gesture.ScaleTo(gesture.PromoteScale, 0, 0);
            Assert.Equal(ShelterStage.Mid, gesture.Shelter);
            Assert.Equal(gesture.PromoteScale, gesture.PanelScale, 9);

            gesture.ScaleTo(1.0, 0, 0);
            Assert.Equal(ShelterStage.Band, gesture.Shelter);
        }

        Assert.True(gesture.AtFloor);
    }

    /// <summary>
    /// T52/K5: orta kademenin esigi uydurulmus bir sayi degil, terfi olceginin parametre
    /// karsiligi. Tavan degisince esik de kendiliginden kayar.
    /// </summary>
    [Theory]
    [InlineData(4.0, 2.0)]
    [InlineData(8.0, 2.0)]
    [InlineData(4.0, 3.0)]
    public void OrtaKademeEsigiTerfiOlcegindenTurer(double ceiling, double promote)
    {
        var gesture = new ZoomGesture(ceiling, promote);

        Assert.Equal((promote - 1.0) / (ceiling - 1.0), gesture.MidAt, 9);
        Assert.True(gesture.MidDropAt > ZoomGesture.Floor);
        Assert.True(gesture.MidDropAt < gesture.MidAt);
        Assert.True(gesture.MidAt < ZoomGesture.FullAt);
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
}

/// <summary>
/// T52/K3: panelin taban boyu. İki belirteç de panel tabanının iki katı olacak; sayı
/// testte de uydurulmuyor, <c>PanelMinHeight</c>'tan çarpılarak türetiliyor.
/// </summary>
public class PlaybackBaseHeightTests
{
    /// <summary>Kullanıcının istediği kat: taban boy iki katına çıktı.</summary>
    private const double BaseFactor = 2.0;

    [Theory]
    [InlineData("PlaybackStageMinHeight")]
    [InlineData("PlaybackIdleMinHeight")]
    public void Taban_boy_panel_tabaninin_iki_kati(string key)
    {
        var (panelBase, playback) = AppHost.Run(() =>
        {
            var window = new MainWindow();
            window.TryFindResource("PanelMinHeight", out var basis);
            window.TryFindResource(key, out var value);
            return ((double)basis!, (double)value!);
        });

        Assert.Equal(panelBase * BaseFactor, playback, 6);
    }

    /// <summary>Önizleme olsun olmasın panel aynı boyda durur.</summary>
    [Fact]
    public void Bos_panel_de_sahne_de_ayni_tabana_oturur()
    {
        var (stage, idle) = AppHost.Run(() =>
        {
            var window = new MainWindow();
            window.TryFindResource("PlaybackStageMinHeight", out var a);
            window.TryFindResource("PlaybackIdleMinHeight", out var b);
            return ((double)a!, (double)b!);
        });

        Assert.Equal(stage, idle, 6);
    }
}
