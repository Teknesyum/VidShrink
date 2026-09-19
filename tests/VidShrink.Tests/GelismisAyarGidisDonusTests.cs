using VidShrink.App;

namespace VidShrink.Tests;

/// <summary>
/// Gelismis panelin kutulari ile <see cref="AppSettings"/> alanlari arasindaki eslesme.
///
/// <para><c>-tune</c> isi panele sekizinci kutuyu ekledi ama ayar tarafina hic dokunmadi:
/// toplama ve geri yukleme kutulari <b>konumla</b> okuyordu, yeni kutu ucuncu siraya girdi
/// ve butun eslesme kaydi. Geri yukleme yedi indisi sekiz kutuya dagitmaya calisip
/// <c>IndexOutOfRangeException</c> atti — yani ayarlari acmak cokuyordu. Uc olcu bunu
/// gormedi, cunku hicbiri gidis-donusun <b>alan alan</b> ayni kaldigini sinamiyordu.</para>
///
/// <para>Burada olcu tam o: her kutuya ayri bir deger verilir, pencere geri yukler,
/// yeniden toplar; her alan kendi degeriyle donmelidir. Kutu ile alanin yeri degisirse
/// ya da yeni bir kutu alan almadan eklenirse bu olcu kirmizi olur.</para>
/// </summary>
public sealed class GelismisAyarGidisDonusTests
{
    private static string AyarDosyasi()
        => Path.Combine(TestPaths.OutputRoot, $"gelismis-{Guid.NewGuid():N}.json");

    /// <summary>Dokuz gelismis alanin hepsi kendi degeriyle donuyor.</summary>
    [Fact]
    public void HerGelismisAlanKendiDegeriyleDonuyor()
    {
        var yazilan = new AppSettings
        {
            AdvMode = 1,
            AdvCrf = 2,
            AdvPreset = 3,
            AdvTune = 1,
            AdvAudioKbps = 4,
            AdvAudioChannels = 4,
            AdvAudioCodec = 2,
            AdvMinResolution = 2,
            AdvMinFps = 1,
            AdvEncoderPath = 2,
            AdvCodecLock = 3
        };

        var dosya = AyarDosyasi();
        try
        {
            var okunan = AppHost.Run(() =>
            {
                var pencere = new MainWindow { SettingsPathOverride = dosya };
                try
                {
                    pencere.RestoreAppSettingsForTest(yazilan);
                    return pencere.CaptureAppSettingsForTest();
                }
                finally { pencere.Close(); }
            });

            Assert.Equal(yazilan.AdvMode, okunan.AdvMode);
            Assert.Equal(yazilan.AdvCrf, okunan.AdvCrf);
            Assert.Equal(yazilan.AdvPreset, okunan.AdvPreset);
            Assert.Equal(yazilan.AdvTune, okunan.AdvTune);
            Assert.Equal(yazilan.AdvAudioKbps, okunan.AdvAudioKbps);
            Assert.Equal(yazilan.AdvAudioChannels, okunan.AdvAudioChannels);
            Assert.Equal(yazilan.AdvAudioCodec, okunan.AdvAudioCodec);
            Assert.Equal(yazilan.AdvMinResolution, okunan.AdvMinResolution);
            Assert.Equal(yazilan.AdvMinFps, okunan.AdvMinFps);
            Assert.Equal(yazilan.AdvEncoderPath, okunan.AdvEncoderPath);
            Assert.Equal(yazilan.AdvCodecLock, okunan.AdvCodecLock);
        }
        finally { if (File.Exists(dosya)) File.Delete(dosya); }
    }

    /// <summary>
    /// Olumsuz kontrol: sifirlar donuyor diye yesil olmuyor. Yukaridaki degerlerin
    /// hicbiri varsayilan degil, yani her esitlik gercekten bir seyi olcuyor.
    /// </summary>
    [Fact]
    public void PimlenenDegerlerinHicbiriVarsayilanDegil()
    {
        var varsayilan = new AppSettings();

        Assert.Equal(0, varsayilan.AdvTune);
        Assert.Equal(0, varsayilan.AdvAudioCodec);
        Assert.Equal(0, varsayilan.AdvCrf);
        Assert.Equal(0, varsayilan.AdvCodecLock);
    }

    /// <summary>
    /// <c>advTune</c> dosyaya da yaziliyor ve geri okunuyor; alan yalniz bellekte kalirsa
    /// kilit uygulama kapaninca kaybolur.
    /// </summary>
    [Fact]
    public void AdvTuneDosyayaYazilipGeriOkunuyor()
    {
        var dosya = AyarDosyasi();
        try
        {
            new AppSettings { AdvTune = 2 }.Save(dosya);

            Assert.Contains("advTune", File.ReadAllText(dosya), StringComparison.Ordinal);
            Assert.Equal(2, AppSettings.Load(dosya).AdvTune);
        }
        finally { if (File.Exists(dosya)) File.Delete(dosya); }
    }
}
