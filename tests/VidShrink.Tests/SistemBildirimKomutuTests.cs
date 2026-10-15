using Avalonia.Controls;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// AYR-19 ve AYR-20: macOS ve Linux'ta iş bitti haberi ve bitiş sesi sistemin kendi aracına
/// verilen komutla çıkar; ses dosyası seçilebilir. Komutu kuran katman saftır, başlatıcı hep
/// sahtedir: gerçek bildirim gösterilmez, ses çalınmaz, hiçbir süreç başlamaz.
/// </summary>
public sealed class SistemBildirimKomutuTests
{
    private sealed class SahteBaslatici : ISurecBaslatici
    {
        public List<SistemKomutu> Gelen { get; } = new();
        public void Baslat(SistemKomutu komut) => Gelen.Add(komut);
    }

    private sealed class SahteSes : IBitisSesi
    {
        public List<(bool Hata, string? Dosya)> Calinan { get; } = new();
        public void Cal(bool hata) => Calinan.Add((hata, "<dosyasiz>"));
        public void Cal(bool hata, string? dosya) => Calinan.Add((hata, dosya));
    }

    private static string Klasor(string ad)
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "sistem-bildirim-komutu", ad + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    [Fact]
    public void MacOsHaberiOsascriptIleVerirVeTirnagiKacirir()
    {
        var komut = SistemKomutlari.Bildirim(Isletim.MacOS, new IsBildirimi("VidShrink", "a \"b\" \\c.mp4 hazır.", false));

        Assert.NotNull(komut);
        Assert.Equal("osascript", komut!.Program);
        Assert.Equal(new[] { "-e", "display notification \"a \\\"b\\\" \\\\c.mp4 hazır.\" with title \"VidShrink\"" }, komut.Argumanlar);
    }

    [Theory]
    [InlineData(false, "normal")]
    [InlineData(true, "critical")]
    public void LinuxHaberiNotifySendIleVerir(bool hata, string aciliyet)
    {
        var komut = SistemKomutlari.Bildirim(Isletim.Linux, new IsBildirimi("VidShrink", "-x.mp4 hazır.", hata));

        Assert.NotNull(komut);
        Assert.Equal("notify-send", komut!.Program);
        Assert.Equal(new[] { "--app-name=VidShrink", "--urgency=" + aciliyet, "--", "VidShrink", "-x.mp4 hazır." }, komut.Argumanlar);
    }

    [Theory]
    [InlineData(Isletim.Windows)]
    [InlineData(Isletim.Diger)]
    public void WindowsVeTanimsizSistemdeKomutYok(Isletim isletim)
    {
        Assert.Null(SistemKomutlari.Bildirim(isletim, new IsBildirimi("VidShrink", "x", false)));
        Assert.Null(SistemKomutlari.Ses(isletim, null, false));
        Assert.Null(SistemKomutlari.Ses(isletim, "a.wav", true));
    }

    [Fact]
    public void SesKomutuDosyayaVeHataYaGoreAyrilir()
    {
        Assert.Equal(new SistemKomutu("afplay", new[] { "/System/Library/Sounds/Glass.aiff" }), SistemKomutlari.Ses(Isletim.MacOS, null, false), KomutEsit.Instance);
        Assert.Equal(new SistemKomutu("afplay", new[] { "/System/Library/Sounds/Basso.aiff" }), SistemKomutlari.Ses(Isletim.MacOS, null, true), KomutEsit.Instance);
        Assert.Equal(new SistemKomutu("afplay", new[] { "/ses/zil.wav" }), SistemKomutlari.Ses(Isletim.MacOS, "/ses/zil.wav", true), KomutEsit.Instance);
        Assert.Equal(new SistemKomutu("canberra-gtk-play", new[] { "-i", "complete" }), SistemKomutlari.Ses(Isletim.Linux, null, false), KomutEsit.Instance);
        Assert.Equal(new SistemKomutu("canberra-gtk-play", new[] { "-i", "dialog-error" }), SistemKomutlari.Ses(Isletim.Linux, null, true), KomutEsit.Instance);
        Assert.Equal(new SistemKomutu("paplay", new[] { "/ses/zil.wav" }), SistemKomutlari.Ses(Isletim.Linux, "/ses/zil.wav", false), KomutEsit.Instance);
    }

    [Fact]
    public void OlmayanSesDosyasiSistemSesineDuser()
    {
        var klasor = Klasor("dosya");
        try
        {
            var var = Path.Combine(klasor, "zil.wav");
            File.WriteAllBytes(var, new byte[] { 1 });
            Assert.Equal(var, SistemKomutlari.SesDosyasi(var));
            Assert.Null(SistemKomutlari.SesDosyasi(Path.Combine(klasor, "yok.wav")));
            Assert.Null(SistemKomutlari.SesDosyasi(""));
            Assert.Null(SistemKomutlari.SesDosyasi(null));
        }
        finally { Directory.Delete(klasor, true); }
    }

    [Theory]
    [InlineData(Isletim.MacOS)]
    [InlineData(Isletim.Linux)]
    public void MasaustundeKomutYuzuSecilirVeSahteBaslaticiyaBirKezGider(Isletim isletim)
    {
        var baslatici = new SahteBaslatici();
        var yuz = IsBittiBildirimi.YuzSec(isletim, true, baslatici);
        var ses = IsBittiBildirimi.SesSec(isletim, true, baslatici);

        Assert.IsType<KomutIsBildirimYuzu>(yuz);
        Assert.IsType<KomutBitisSesi>(ses);

        var bildirim = new IsBildirimi("VidShrink", "a.mp4 hazır.", false);
        yuz.Bildir(null!, bildirim);
        ses.Cal(true, null);

        Assert.Equal(2, baslatici.Gelen.Count);
        Assert.Equal(SistemKomutlari.Bildirim(isletim, bildirim), baslatici.Gelen[0], KomutEsit.Instance);
        Assert.Equal(SistemKomutlari.Ses(isletim, null, true), baslatici.Gelen[1], KomutEsit.Instance);
    }

    [Theory]
    [InlineData(Isletim.Windows, false)]
    [InlineData(Isletim.MacOS, false)]
    [InlineData(Isletim.Linux, false)]
    [InlineData(Isletim.Diger, true)]
    public void MasaustuDisindaVeTanimsizSistemdeSessiz(Isletim isletim, bool masaustu)
    {
        var baslatici = new SahteBaslatici();
        Assert.IsType<SessizIsBildirimYuzu>(IsBittiBildirimi.YuzSec(isletim, masaustu, baslatici));
        Assert.IsType<SessizBitisSesi>(IsBittiBildirimi.SesSec(isletim, masaustu, baslatici));
        Assert.Empty(baslatici.Gelen);
    }

    [Fact]
    public void WindowsMasaustundeApiYuzuKalir()
    {
        Assert.IsType<SistemIsBildirimYuzu>(IsBittiBildirimi.YuzSec(Isletim.Windows, true, new SahteBaslatici()));
        Assert.IsType<SistemBitisSesi>(IsBittiBildirimi.SesSec(Isletim.Windows, true, new SahteBaslatici()));
    }

    [Fact]
    public void SesDosyasiAyardaGidipDonerEskiDosyaBos()
    {
        var klasor = Klasor("ayar");
        try
        {
            var dolu = Path.Combine(klasor, "dolu.json");
            var eski = Path.Combine(klasor, "eski.json");
            new AppSettings { FinishSoundFile = "C:/ses/zil.wav" }.Save(dolu);
            File.WriteAllText(eski, "{\"finishSound\": true}");

            Assert.Equal("C:/ses/zil.wav", AppSettings.Load(dolu).FinishSoundFile);
            Assert.Contains("\"finishSoundFile\"", File.ReadAllText(dolu), StringComparison.Ordinal);
            Assert.Equal("", AppSettings.Load(eski).FinishSoundFile);
            Assert.Equal("", new AppSettings().FinishSoundFile);
        }
        finally { Directory.Delete(klasor, true); }
    }

    [Fact]
    public void AnaPencereSecilenDosyayiSeseVerir()
    {
        var klasor = Klasor("ana");
        try
        {
            var ayar = Path.Combine(klasor, "settings.json");
            var zil = Path.Combine(klasor, "zil.wav");
            var sonuc = AppHost.Run(() =>
            {
                var ses = new SahteSes();
                var pencere = new MainWindow { SettingsPathOverride = ayar, Bildirim = SessizIsBildirimYuzu.Instance, Ses = ses };
                try
                {
                    var sifirla = pencere.FindControl<MenuItem>("BtnFinishSoundReset")!;
                    var sec = pencere.FindControl<MenuItem>("BtnFinishSoundFile")!;
                    var bastaGorunur = sifirla.IsVisible;
                    var bastaMetin = sec.Header as string;
                    pencere.FindControl<CheckBox>("ChkFinishSound")!.IsChecked = true;

                    pencere.SetFinishSoundFile(zil);
                    pencere.IsBittiHaberi(new BitenIs("a.mp4", IsSonucu.Basarili));
                    var seciliGorunur = sifirla.IsVisible;
                    var toplanan = pencere.CaptureAppSettingsForTest().FinishSoundFile;

                    pencere.SetFinishSoundFile(null);
                    pencere.IsBittiHaberi(new BitenIs("a.mp4", IsSonucu.Hatali));
                    var sifirGorunur = sifirla.IsVisible;
                    var sifirToplanan = pencere.CaptureAppSettingsForTest().FinishSoundFile;

                    pencere.RestoreAppSettingsForTest(new AppSettings { FinishSound = true, FinishSoundFile = zil });
                    pencere.IsBittiHaberi(new BitenIs("a.mp4", IsSonucu.Basarili));
                    var geriGorunur = sifirla.IsVisible;

                    return (bastaGorunur, bastaMetin, seciliGorunur, toplanan, sifirGorunur, sifirToplanan, geriGorunur, calinan: ses.Calinan.ToArray());
                }
                finally { pencere.Close(); }
            });

            Assert.False(sonuc.bastaGorunur);
            Assert.False(string.IsNullOrWhiteSpace(sonuc.bastaMetin));
            Assert.True(sonuc.seciliGorunur);
            Assert.Equal(zil, sonuc.toplanan);
            Assert.False(sonuc.sifirGorunur);
            Assert.Equal("", sonuc.sifirToplanan);
            Assert.True(sonuc.geriGorunur);
            Assert.Equal(new (bool, string?)[] { (false, zil), (true, null), (false, zil) }, sonuc.calinan);
        }
        finally { Directory.Delete(klasor, true); }
    }

    [Fact]
    public void KuyrukPenceresiAyardakiDosyayiSeseVerir()
    {
        var klasor = Klasor("kuyruk");
        try
        {
            var ayar = Path.Combine(klasor, "settings.json");
            var zil = Path.Combine(klasor, "zil.wav");
            new AppSettings { FinishSound = true, FinishSoundFile = zil }.Save(ayar);
            var calinan = AppHost.Run(() =>
            {
                var onceki = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
                Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", ayar);
                ShrinkJobWindow pencere;
                try { pencere = new ShrinkJobWindow(new[] { "a.mp4" }, new PlanOptions { TargetMb = 25 }, false, null); }
                finally { Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", onceki); }
                try
                {
                    var ses = new SahteSes();
                    pencere.Ses = ses;
                    pencere.Bildirim = SessizIsBildirimYuzu.Instance;
                    pencere.IsBitti("a.mp4", IsSonucu.Hatali);
                    pencere.HaberVer();
                    return ses.Calinan.ToArray();
                }
                finally { pencere.Close(); }
            });

            Assert.Equal(new (bool, string?)[] { (true, zil) }, calinan);
        }
        finally { Directory.Delete(klasor, true); }
    }

    [Fact]
    public void AnahtarlarButunDillerde()
    {
        var anahtarlar = new[]
        {
            "settings-tab.finish-sound-file.choose", "settings-tab.finish-sound-file.hint",
            "settings-tab.finish-sound-file.reset", "settings-tab.encode-priority.above-normal"
        };
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            foreach (var key in anahtarlar)
                Assert.True(values.TryGetValue(key, out var metin) && metin.Length > 0, $"{language}: {key}");
            Assert.StartsWith("• ", values["settings-tab.finish-sound-file.hint"], StringComparison.Ordinal);
        }
    }

    private sealed class KomutEsit : IEqualityComparer<SistemKomutu?>
    {
        internal static KomutEsit Instance { get; } = new();

        public bool Equals(SistemKomutu? x, SistemKomutu? y)
            => x is not null && y is not null && x.Program == y.Program && x.Argumanlar.SequenceEqual(y.Argumanlar);

        public int GetHashCode(SistemKomutu? obj) => obj?.Program.GetHashCode(StringComparison.Ordinal) ?? 0;
    }
}
