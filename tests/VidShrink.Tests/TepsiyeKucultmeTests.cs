using System.Globalization;
using Avalonia.Controls;
using VidShrink.App;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// AYR-22: "Tepsiye küçült" açıkken simge durumuna küçültülen ana pencere gizlenir ve tepsi simgesi
/// görünür; simgeye tıklanınca pencere küçültmeden önceki haline döner, simge kalkar. Seçenek kapalıyken
/// küçültme bugünkü gibidir. Tepsi hep sahtedir: test sürecinde gerçek tepsi simgesi açılmaz.
/// </summary>
public sealed class TepsiyeKucultmeTests
{
    private sealed class SahteTepsi : IMainWindowTray
    {
        public List<string> Olaylar { get; } = new();
        public void Show(string tip) => Olaylar.Add("show:" + tip);
        public void Remove() => Olaylar.Add("remove");
    }

    private static string Say(string key, object?[] args) => key + "=" + string.Join(",", args);

    [Theory]
    [InlineData(true, WindowState.Minimized, true)]
    [InlineData(false, WindowState.Minimized, false)]
    [InlineData(true, WindowState.Normal, false)]
    [InlineData(true, WindowState.Maximized, false)]
    [InlineData(true, WindowState.FullScreen, false)]
    public void YalnizSecenekAcikkenKucultmeTepsiyeGider(bool secenek, WindowState durum, bool beklenen)
        => Assert.Equal(beklenen, TepsiKarari.Gizle(secenek, durum));

    [Fact]
    public void IpucuIsVeKuyrukIlerlemesiniSirayalaYazar()
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        Assert.Equal(VidShrink.Core.AppIdentity.Name, TepsiKarari.Ipucu(null, Array.Empty<double>(), Say, tr));
        Assert.Equal(VidShrink.Core.AppIdentity.Name + " · main.tray.tip.job=%42,5", TepsiKarari.Ipucu(0.425, Array.Empty<double>(), Say, tr));
        Assert.Equal(VidShrink.Core.AppIdentity.Name + " · main.tray.tip.job=%100 · main.tray.tip.queue=%20 · main.tray.tip.queue=%0",
            TepsiKarari.Ipucu(1.4, new[] { 0.2, -0.1 }, Say, tr));
    }

    [Fact]
    public void AcikSecenekleKucultmeGizlerTiklamaGeriGetirir()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "tepsiye-kucultme", "acik");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        var tepsi = new SahteTepsi();
        try
        {
            var sonuc = AppHost.Run(() =>
            {
                var pencere = new MainWindow { SettingsPathOverride = Path.Combine(klasor, "settings.json"), Tepsi = tepsi };
                try
                {
                    var varsayilan = pencere.FindControl<CheckBox>("ChkMinimizeToTray")!.IsChecked == true;
                    pencere.FindControl<CheckBox>("ChkMinimizeToTray")!.IsChecked = true;
                    pencere.Show();
                    pencere.WindowState = WindowState.Normal;
                    pencere.WindowState = WindowState.Minimized;
                    var gizli = (pencere.TepsideGizli, pencere.IsVisible, Olaylar: tepsi.Olaylar.ToArray());

                    pencere.ShowEncodeProgressForTest(new EncodeProgress(0.5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), 1, "pass 1/1 (attempt 1)"));
                    pencere.TepsiyiTazeleForTest();
                    var ipucu = pencere.TepsiIpucu;
                    pencere.EndEncodeForTest();

                    pencere.TepsidenGeriGetir();
                    var geri = (pencere.TepsideGizli, pencere.IsVisible, pencere.WindowState, Son: tepsi.Olaylar[^1]);
                    var toplanan = pencere.CaptureAppSettingsForTest().MinimizeToTray;
                    return (varsayilan, gizli, ipucu, geri, toplanan);
                }
                finally { pencere.Close(); }
            });

            Assert.False(sonuc.varsayilan);
            Assert.True(sonuc.gizli.TepsideGizli);
            Assert.False(sonuc.gizli.IsVisible);
            Assert.Equal(new[] { "show:" + VidShrink.Core.AppIdentity.Name }, sonuc.gizli.Olaylar);
            Assert.StartsWith(VidShrink.Core.AppIdentity.Name + " · ", sonuc.ipucu, StringComparison.Ordinal);
            Assert.False(sonuc.geri.TepsideGizli);
            Assert.True(sonuc.geri.IsVisible);
            Assert.Equal(WindowState.Normal, sonuc.geri.WindowState);
            Assert.Equal("remove", sonuc.geri.Son);
            Assert.True(sonuc.toplanan);
        }
        finally { Directory.Delete(klasor, true); }
    }

    [Fact]
    public void KapaliSecenekleKucultmeTepsiyeGitmez()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "tepsiye-kucultme", "kapali");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        var tepsi = new SahteTepsi();
        try
        {
            var sonuc = AppHost.Run(() =>
            {
                var pencere = new MainWindow { SettingsPathOverride = Path.Combine(klasor, "settings.json"), Tepsi = tepsi };
                try
                {
                    pencere.Show();
                    pencere.WindowState = WindowState.Minimized;
                    return (pencere.TepsideGizli, pencere.IsVisible);
                }
                finally { pencere.Close(); }
            });

            Assert.False(sonuc.TepsideGizli);
            Assert.True(sonuc.IsVisible);
            Assert.Empty(tepsi.Olaylar);
        }
        finally { Directory.Delete(klasor, true); }
    }

    [Fact]
    public void AyarGidisDonusVeEskiDosyaKapali()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "tepsiye-kucultme", "ayar");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        try
        {
            var acik = Path.Combine(klasor, "acik.json");
            var eski = Path.Combine(klasor, "eski.json");
            new AppSettings { MinimizeToTray = true }.Save(acik);
            File.WriteAllText(eski, "{\"followRecording\": true}");

            Assert.True(AppSettings.Load(acik).MinimizeToTray);
            Assert.Contains("\"minimizeToTray\": true", File.ReadAllText(acik), StringComparison.Ordinal);
            Assert.False(AppSettings.Load(eski).MinimizeToTray);
            Assert.False(new AppSettings().MinimizeToTray);
        }
        finally { Directory.Delete(klasor, true); }
    }
}
