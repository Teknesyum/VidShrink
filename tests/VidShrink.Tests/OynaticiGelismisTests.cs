using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal static class GelismisKanit
{
    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "dalga4a");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static void Write(string name, string body)
        => File.WriteAllText(Path.Combine(Folder, name), body, new UTF8Encoding(false));

    internal static string Gecici(string ad)
    {
        var path = Path.Combine(Folder, "gecici", ad + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }
}

/// <summary>
/// 4a dalga: gelismis goruntu, ses ve altyazi ayarlari. Her olcum ayari arayuzden yazar,
/// degeri <b>motordan</b> geri okur ve sifirlar; goruntu ayarlarinin gercekten kareye
/// dokundugu bayt karsilastirmasiyla, dokunmadigi hal ise negatif kontrolle gosterilir.
/// </summary>
public sealed class OynaticiGelismisTests
{
    [Fact]
    public void HerGoruntuAyariMotoraYazilirVeSifirlanir()
    {
        var rapor = AppHost.Run(() =>
        {
            var view = DenetimSurucu.Ac(MotorKlipleri.Kucuk, out var window);
            DenetimSurucu.Duraklat(view);
            var motor = DenetimSurucu.Motor(view);
            var body = new StringBuilder();

            foreach (var knob in PlayerView.Knobs)
            {
                view.NudgePicture(knob, 2);
                DenetimSurucu.Wait(view, 0.2);
                var okunan = Deger(motor.Picture, knob);
                body.AppendLine($"{knob}: yazilan 10, motordan okunan {okunan}");
                Assert.Equal(10, okunan);
                Assert.Equal(10, Deger(view.Advanced.Picture, knob));
            }

            body.AppendLine("zincir: " + (motor.GetProperty("vf") ?? ""));

            view.ToggleDeinterlace();
            DenetimSurucu.Wait(view, 0.2);
            body.AppendLine("deinterlace: " + motor.GetProperty("deinterlace"));
            Assert.True(motor.Picture.Deinterlace);

            view.ResetPicture();
            DenetimSurucu.Wait(view, 0.2);
            var sifirlanan = motor.Picture;
            body.AppendLine("sifirlama sonrasi: " + sifirlanan);
            Assert.Equal(PictureAdjust.Neutral, sifirlanan);
            Assert.Equal("", (motor.GetProperty("vf") ?? "").Trim());

            window.Close();
            return body.ToString();
        });

        GelismisKanit.Write("goruntu-ayarlari.txt", rapor);
    }

    [Fact]
    public void SesAyarlariMotordanGeriOkunurVeSifirlanir()
    {
        var rapor = AppHost.Run(() =>
        {
            var view = DenetimSurucu.Ac(MotorKlipleri.Kucuk, out var window);
            DenetimSurucu.Duraklat(view);
            var motor = DenetimSurucu.Motor(view);
            var body = new StringBuilder();

            foreach (var (anahtar, bantlar) in PlayerView.EqualizerPresets.Skip(1))
            {
                view.UseEqualizer(bantlar);
                DenetimSurucu.Wait(view, 0.2);
                var okunan = motor.Sound.Bands;
                body.AppendLine($"{anahtar}: yazilan [{string.Join(",", bantlar)}] okunan [{string.Join(",", okunan)}]");
                Assert.Equal(bantlar, okunan);
            }

            view.ToggleNormalize();
            DenetimSurucu.Wait(view, 0.2);
            Assert.True(motor.Sound.Normalize);
            body.AppendLine("af: " + (motor.GetProperty("af") ?? ""));

            view.ToggleBoost();
            DenetimSurucu.Wait(view, 0.2);
            body.AppendLine("tavan: " + motor.VolumeCeiling.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(SoundAdjust.BoostCeiling, motor.VolumeCeiling);
            motor.SetVolume(150);
            DenetimSurucu.Wait(view, 0.2);
            body.AppendLine("ses: " + motor.Volume.ToString("0", CultureInfo.InvariantCulture));
            Assert.Equal(150, motor.Volume, 0);

            view.ResetSound();
            DenetimSurucu.Wait(view, 0.2);
            body.AppendLine("sifirlama sonrasi tavan: " + motor.VolumeCeiling.ToString(CultureInfo.InvariantCulture));
            Assert.True(motor.Sound.IsNeutral);
            Assert.Equal(SoundAdjust.PlainCeiling, motor.VolumeCeiling);
            Assert.True(motor.Volume <= SoundAdjust.PlainCeiling);
            Assert.Equal("", (motor.GetProperty("af") ?? "").Trim());

            window.Close();
            return body.ToString();
        });

        GelismisKanit.Write("ses-ayarlari.txt", rapor);
    }

    /// <summary>
    /// Sifirlama <c>option-info/&lt;ad&gt;/default-value</c>'ya doner; mpv bu varsayilanlari
    /// somut degerler olarak bildirdigi icin karsilastirma yazmadan once okunan hale yapilir.
    /// </summary>
    [Fact]
    public void AltyaziBicimiMotordanGeriOkunurVeSifirlanir()
    {
        var rapor = AppHost.Run(() =>
        {
            var view = DenetimSurucu.Ac(MotorKlipleri.Kucuk, out var window);
            DenetimSurucu.Duraklat(view);
            var motor = DenetimSurucu.Motor(view);
            var body = new StringBuilder();

            var varsayilan = motor.SubtitleLook;
            body.AppendLine("varsayilan: " + varsayilan);

            view.UseSubtitleFont("serif");
            view.UseSubtitleColor("#FFFFFF00");
            view.NudgeSubtitleOutline(4);
            view.NudgeSubtitleShadow(2);
            view.ToggleSubtitleBackground();
            DenetimSurucu.Wait(view, 0.2);

            var yazilan = motor.SubtitleLook;
            body.AppendLine("yazilan: " + yazilan);
            Assert.Equal("serif", yazilan.Font);
            Assert.Equal("#FFFFFF00", yazilan.Color);
            Assert.Equal(2.0, yazilan.Outline!.Value, 3);
            Assert.Equal(1.0, yazilan.Shadow!.Value, 3);
            Assert.Equal(PlayerView.SubtitleBackgroundOn, yazilan.Background);
            Assert.NotEqual(varsayilan.Font, yazilan.Font);

            view.ResetSubtitleStyle();
            DenetimSurucu.Wait(view, 0.2);
            var sifirlanan = motor.SubtitleLook;
            body.AppendLine("sifirlanan: " + sifirlanan);
            Assert.Equal(varsayilan, sifirlanan);

            window.Close();
            return body.ToString();
        });

        GelismisKanit.Write("altyazi-bicimi.txt", rapor);
    }

    /// <summary>
    /// Negatif kontrol: parlaklik kareyi degistirir, altyazi zemini ayni klipte kareye
    /// dokunmaz. Ayni sahnede iki olcum, tek kaynak — fark suzgecin kendisinden gelir.
    /// </summary>
    [Fact]
    public void GoruntuAyariKareyiDegistirirAltyaziAyariDegistirmez()
    {
        var rapor = AppHost.Run(() =>
        {
            var view = DenetimSurucu.Ac(MotorKlipleri.Kucuk, out var window);
            DenetimSurucu.Duraklat(view);
            DenetimSurucu.Git(view, 4);
            var body = new StringBuilder();

            var once = Kare(view);

            view.ToggleSubtitleBackground();
            DenetimSurucu.Wait(view, 0.4);
            var altyaziSonrasi = Kare(view);
            var altyaziFarki = Fark(once, altyaziSonrasi);
            body.AppendLine("altyazi zemini farki: " + altyaziFarki.ToString("0.###", CultureInfo.InvariantCulture));

            view.NudgePicture(PictureKnob.Brightness, 12);
            DenetimSurucu.Wait(view, 0.4);
            var parlakKare = Kare(view);
            var parlaklikFarki = Fark(once, parlakKare);
            body.AppendLine("parlaklik farki: " + parlaklikFarki.ToString("0.###", CultureInfo.InvariantCulture));

            view.ResetPicture();
            DenetimSurucu.Wait(view, 0.4);
            var geriFarki = Fark(once, Kare(view));
            body.AppendLine("sifirlama farki: " + geriFarki.ToString("0.###", CultureInfo.InvariantCulture));

            Assert.True(parlaklikFarki > 5, $"parlaklik kareyi degistirmedi: {parlaklikFarki}");
            Assert.True(altyaziFarki < 1, $"altyazi zemini kareyi degistirdi: {altyaziFarki}");
            Assert.True(geriFarki < 1, $"sifirlama kareyi geri getirmedi: {geriFarki}");

            window.Close();
            return body.ToString();
        });

        GelismisKanit.Write("kare-negatif-kontrol.txt", rapor);
    }

    [Fact]
    public void AyarlarDosyayaYazilirVeSonrakiAcilistaMotora_Doner()
    {
        var rapor = AppHost.Run(() =>
        {
            var klasor = GelismisKanit.Gecici("kalicilik");
            var gecmis = Path.Combine(klasor, "player-history.json");
            var body = new StringBuilder();

            var view = DenetimSurucu.Ac(MotorKlipleri.Kucuk, out var window, gecmis);
            view.NudgePicture(PictureKnob.Saturation, 3);
            view.UseEqualizer(PlayerView.EqualizerPresets[1].Bands);
            DenetimSurucu.Wait(view, 0.2);
            window.Close();

            var dosya = Path.Combine(klasor, PlayerAdvanced.FileName);
            body.AppendLine("dosya: " + File.ReadAllText(dosya));
            Assert.True(File.Exists(dosya));

            var okunan = PlayerAdvanced.Load(dosya);
            Assert.Equal(15, okunan.Picture.Saturation);
            Assert.Equal(PlayerView.EqualizerPresets[1].Bands, okunan.Sound.Bands);

            var ikinci = DenetimSurucu.Ac(MotorKlipleri.Kucuk, out var ikinciPencere, gecmis);
            DenetimSurucu.Wait(view, 0.3);
            var motor = DenetimSurucu.Motor(ikinci);
            body.AppendLine("ikinci acilis: " + motor.Picture + " | [" + string.Join(",", motor.Sound.Bands) + "]");
            Assert.Equal(15, motor.Picture.Saturation);
            Assert.Equal(PlayerView.EqualizerPresets[1].Bands, motor.Sound.Bands);
            ikinciPencere.Close();

            return body.ToString();
        });

        GelismisKanit.Write("kalicilik.txt", rapor);
    }

    [Fact]
    public void GelismisAltMenusuSondaDurupKisayolSatiriUretmez()
    {
        var rapor = AppHost.Run(() =>
        {
            var view = new PlayerView();
            var window = new Window { Width = 640, Height = 480, Content = view };
            var body = new StringBuilder();

            foreach (var dil in new[] { "en", "tr" })
            {
                Strings.Use(dil);
                Dispatcher.UIThread.RunJobs();
                var ogeler = view.BuildMenu().Items.OfType<MenuItem>().ToList();
                var son = ogeler[^1];
                body.AppendLine($"[{dil}] son satir: {son.Header} | alt oge {son.Items.Count}");
                Assert.Equal(Strings.Get("player.advanced.menu"), son.Header);
                Assert.Null(son.Tag);
                Assert.DoesNotContain(Ilerisi(son), item => item.Tag is PlayerAction);
                Assert.All(Ilerisi(son), item => Assert.False(string.IsNullOrWhiteSpace(item.Header?.ToString())));
            }

            Strings.Use("en");
            window.Close();
            return body.ToString();
        });

        GelismisKanit.Write("gelismis-menu.txt", rapor);
    }

    [Fact]
    public void AyarlarSayfasindakiGelismisKumesiAyniYollariCagirir()
    {
        var rapor = AppHost.Run(() =>
        {
            Strings.Use("en");
            var pencere = new MainWindow();
            var panel = pencere.FindControl<PlayerAdvancedPanel>("PlayerAdvancedPanel");
            Assert.NotNull(panel);
            panel!.Player!.ResetAdvanced();
            Dispatcher.UIThread.RunJobs();

            var body = new StringBuilder();
            body.AppendLine($"satirlar: {string.Join(" | ", panel.Shown)}");
            body.AppendLine("ozet: " + panel.Summary);
            Assert.Equal(PlayerView.Knobs.Count + 3, panel.Shown.Count);
            Assert.False(panel.IsOpen);
            Assert.Equal(Strings.Get("player.advanced.state-off"), panel.Summary);

            panel.Player!.NudgePicture(PictureKnob.Contrast, 1);
            Dispatcher.UIThread.RunJobs();
            body.AppendLine("degisim sonrasi ozet: " + panel.Summary);
            Assert.Equal(Strings.Get("player.advanced.state-picture"), panel.Summary);
            Assert.Contains(Strings.Get("player.advanced.contrast", "+5"), panel.Shown);

            panel.Player.ResetAdvanced();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Strings.Get("player.advanced.state-off"), panel.Summary);

            pencere.Close();
            return body.ToString();
        });

        GelismisKanit.Write("ayarlar-kumesi.txt", rapor);
    }

    private static IEnumerable<MenuItem> Ilerisi(MenuItem kok)
    {
        foreach (var cocuk in kok.Items.OfType<MenuItem>())
        {
            yield return cocuk;
            foreach (var torun in Ilerisi(cocuk)) yield return torun;
        }
    }

    private static int Deger(PictureAdjust picture, PictureKnob knob) => knob switch
    {
        PictureKnob.Brightness => picture.Brightness,
        PictureKnob.Contrast => picture.Contrast,
        PictureKnob.Saturation => picture.Saturation,
        PictureKnob.Gamma => picture.Gamma,
        PictureKnob.Hue => picture.Hue,
        PictureKnob.Sharpness => picture.Sharpness,
        _ => picture.Crop
    };

    private static byte[] Kare(PlayerView view)
    {
        long gorulen = 0;
        byte[]? piksel = null;
        var saat = Stopwatch.StartNew();
        while (piksel is null && saat.Elapsed.TotalSeconds < 10)
        {
            Dispatcher.UIThread.RunJobs();
            view.RenderLatest();
            DenetimSurucu.Motor(view).TryCopyLatest(ref gorulen, (p, _, h, stride) =>
            {
                var tampon = new byte[stride * h];
                Marshal.Copy(p, tampon, 0, tampon.Length);
                piksel = tampon;
            });
            Thread.Sleep(10);
        }

        Assert.NotNull(piksel);
        return piksel!;
    }

    private static double Fark(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return double.MaxValue;
        var toplam = 0L;
        for (var i = 0; i < a.Length; i++) toplam += Math.Abs(a[i] - b[i]);
        return (double)toplam / a.Length;
    }
}
