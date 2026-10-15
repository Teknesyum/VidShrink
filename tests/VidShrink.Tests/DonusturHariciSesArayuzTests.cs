using Avalonia.Controls;
using VidShrink.App;
using VidShrink.Core;
using static VidShrink.Tests.B1KalanGirdi;

namespace VidShrink.Tests;

/// <summary>
/// DON-P7'nin arayüz yüzü: Dönüştür'ün ses panelinde dış ses dosyası. Dosya seçici yerine
/// <see cref="MainWindow.SetConvertAudioFile"/> çağrılır. "Yerine koy" ile "ekle" kipi ve "kısa olanda bitir"
/// komuta yansır; yeni kaynak seçimi dosyayı bırakır. Geçici dosyalar <c>.calisma/test-ciktilari/donustur-harici-ses/</c>, test siler.
/// </summary>
public sealed class DonusturHariciSesArayuzTests
{
    private static readonly SourceStream Stereo = new(1, StreamKind.Audio, "aac", "tur", Channels: 2, BitrateBps: 128_000);

    private static List<string> Eslemeler(MainWindow window)
    {
        var parcalar = (window.TxtConvertCommand.Text ?? "").Split(' ').ToList();
        return parcalar.Select((p, i) => (p, i)).Where(x => x.p == "-map" && x.i + 1 < parcalar.Count).Select(x => parcalar[x.i + 1]).ToList();
    }

    [Fact]
    public void KipVeKisaOlandaBitirKomutaYansir()
    {
        var info = Kaynak(30, Video, Stereo);
        var (yok, ekle, yerine, kisa, bosalt) = Pencerede((window, ses) =>
        {
            window.LoadWithoutProbing(info.FilePath, info);
            window.CmbContainer.SelectedItem = Kap(window, "mkv");
            var yokz = (window.ConvertExternalAudio, Eslemeler(window));
            var eklendi = window.SetConvertAudioFile(ses);
            var eklez = (eklendi, window.ConvertExternalAudio?.Replace, Eslemeler(window), window.ChkConvertAudioShortest.IsEnabled);
            window.ChkConvertAudioReplace.IsChecked = true;
            var yerinez = (window.ConvertExternalAudio?.Replace, Eslemeler(window));
            window.ChkConvertAudioShortest.IsChecked = true;
            var kisaz = (window.ChkConvertAudioShortest.IsChecked == true, (window.TxtConvertCommand.Text ?? "").Contains(" -shortest ", StringComparison.Ordinal));
            window.RemoveConvertAudioFile();
            return (yokz, eklez, yerinez, kisaz, (window.ConvertExternalAudio, Eslemeler(window), (window.TxtConvertCommand.Text ?? "").Contains("-shortest", StringComparison.Ordinal)));
        });

        Assert.Null(yok.Item1);
        Assert.Empty(yok.Item2);
        Assert.True(ekle.Item1);
        Assert.False(ekle.Item2);
        Assert.Equal(new[] { "0:0", "0:1", "1:a:0" }, ekle.Item3);
        Assert.True(ekle.Item4);
        Assert.True(yerine.Item1);
        Assert.Equal(new[] { "0:0", "1:a:0" }, yerine.Item2);
        Assert.True(kisa.Item1);
        Assert.True(kisa.Item2);
        Assert.Null(bosalt.Item1);
        Assert.Empty(bosalt.Item2);
        Assert.False(bosalt.Item3);
    }

    [Fact]
    public void YeniKaynakDosyayiBirakirVeGecersizDosyaReddedilir()
    {
        var info = Kaynak(30, Video, Stereo);
        var (gecersiz, sonra, liste) = Pencerede((window, ses) =>
        {
            window.LoadWithoutProbing(info.FilePath, info);
            var red = window.SetConvertAudioFile(Path.ChangeExtension(ses, ".txt"));
            window.SetConvertAudioFile(ses);
            window.ChkConvertAudioReplace.IsChecked = true;
            var diger = Kaynak(20, Video, Stereo);
            window.LoadWithoutProbing(diger.FilePath, diger);
            return (red, window.ConvertExternalAudio, window.ConvertAudioFileList.IsVisible);
        });

        Assert.False(gecersiz);
        Assert.Null(sonra);
        Assert.False(liste);
    }

    [Fact]
    public void SesTasimayanKaptaKapanir()
    {
        var info = Kaynak(30, Video, Stereo);
        var (gif, mp4) = Pencerede((window, ses) =>
        {
            window.LoadWithoutProbing(info.FilePath, info);
            window.SetConvertAudioFile(ses);
            window.CmbContainer.SelectedItem = Kap(window, "gif");
            var g = (window.BtnConvertAudioFile.IsEnabled, window.ChkConvertAudioReplace.IsEnabled, window.ChkConvertAudioShortest.IsEnabled);
            window.CmbContainer.SelectedItem = Kap(window, "mp4");
            return (g, (window.BtnConvertAudioFile.IsEnabled, window.ChkConvertAudioReplace.IsEnabled, window.ChkConvertAudioShortest.IsEnabled));
        });

        Assert.Equal((false, false, false), gif);
        Assert.Equal((true, true, true), mp4);
    }

    [Fact]
    public void AnahtarKirkIkiDildeCevrili()
    {
        var english = Locales.Domain("en", "main");
        Assert.Equal(42, Locales.Languages.Count);
        var eksik = new List<string>();
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Domain(language, "main");
            const string key = "main.convert.audio-external.shortest";
            if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) eksik.Add($"{language}: yok");
            else if (language != "en" && value == english[key]) eksik.Add($"{language}: cevrilmemis");
        }
        Assert.Empty(eksik);
    }

    private static ComboBoxItem Kap(MainWindow window, string etiket)
        => window.CmbContainer.Items.OfType<ComboBoxItem>().First(item => (string?)item.Tag == etiket);

    private static T Pencerede<T>(Func<MainWindow, string, T> govde)
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "donustur-harici-ses", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        var ses = Path.Combine(klasor, "muzik.m4a");
        File.WriteAllBytes(ses, new byte[] { 0 });
        File.WriteAllBytes(Path.ChangeExtension(ses, ".txt"), new byte[] { 0 });
        try
        {
            return AppHost.Run(() =>
            {
                var window = new MainWindow { SettingsPathOverride = Path.Combine(klasor, "settings.json") };
                try { return govde(window, ses); }
                finally { window.Close(); }
            });
        }
        finally
        {
            try { Directory.Delete(klasor, true); }
            catch (IOException) { }
        }
    }
}
