using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VidShrink.App.Recorder;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// 8b — kaydedici arayuzunun motorla uyumu.
///
/// <para>Arayuz kodlayici ve on ayar listesini kendi dosyasinda tutuyor, cunku motorun
/// <c>KnownVideoCodecs</c> dizisi <c>private</c>: listeyi oradan okumak mumkun degil.
/// Kopya listenin motordan kaymasi sessiz bir kusur olurdu — kullanici listeden bir ad
/// secer, kayit baslamaz. Bu olcu kaymayi kaymanin oldugu turda yakalar: listedeki her
/// adin <see cref="RecorderArguments.Validate"/>'ten gectigi tek tek sinanir.</para>
///
/// <para>Kabul olcusunun yaninda <b>negatif denetim</b> de duruyor: uydurma bir kodlayici
/// adi gercekten reddediliyor mu. O olmadan "hepsi gecti" bulgusu bir sey soylemez —
/// dogrulama her seyi kabul ediyor da olabilir.</para>
/// </summary>
public sealed class KaydediciArayuzTests
{
    private static string Output => Path.Combine(Path.GetTempPath(), "vidshrink-kaydedici-olcu.mp4");

    private static RecorderRequest Request(string? codec = null, string? preset = null) => new()
    {
        Platform = RecorderPlatform.Windows,
        Target = RecorderTargetKind.Screen,
        VideoCodec = codec ?? RecorderArguments.DefaultVideoCodec,
        Preset = preset ?? RecorderArguments.DefaultPreset
    };

    /// <summary>Listelenen her kodlayici motorca kabul ediliyor.</summary>
    [Fact]
    public void SeritteListelenenHerKodlayiciMotordanGeciyor()
    {
        var sikayet = new List<string>();

        foreach (var codec in RecorderView.Codecs)
        {
            var errors = RecorderArguments.Validate(Request(codec: codec), Output);
            if (errors.Count > 0)
                sikayet.Add($"{codec}: {string.Join(" ", errors)}");
        }

        Assert.Empty(sikayet);
        Assert.Equal(RecorderView.Codecs.Length, RecorderView.Codecs.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Listelenen her on ayar motorca kabul ediliyor.</summary>
    [Fact]
    public void SeritteListelenenHerOnAyarMotordanGeciyor()
    {
        var sikayet = new List<string>();

        foreach (var preset in RecorderView.Presets)
        {
            var errors = RecorderArguments.Validate(Request(preset: preset), Output);
            if (errors.Count > 0)
                sikayet.Add($"{preset}: {string.Join(" ", errors)}");
        }

        Assert.Empty(sikayet);
        Assert.Equal(RecorderView.Presets.Length, RecorderView.Presets.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// Negatif denetim: dogrulama her adi yutmuyor. Bu olcu kirmizi verirse yukaridaki iki
    /// olcunun yesili bir sey kanitlamiyor demektir.
    /// </summary>
    [Fact]
    public void UydurmaKodlayiciAdiReddediliyor()
    {
        var errors = RecorderArguments.Validate(Request(codec: "libhicboyle"), Output);
        Assert.NotEmpty(errors);
    }

    /// <summary>
    /// Arayuzun acilista gosterdigi degerler motorun varsayilanlariyla ayni. Ayri bir sayi
    /// yazilsaydi kullanici arayuzde bir sey, kayitta baskasini gorurdu.
    /// </summary>
    [Fact]
    public void AcilisDegerleriMotorunVarsayilanlariyla()
    {
        var settings = new RecorderSettings();

        Assert.Equal(RecorderArguments.DefaultFps, settings.Fps);
        Assert.Equal(RecorderArguments.DefaultVideoCodec, settings.Codec);
        Assert.Equal(RecorderArguments.DefaultPreset, settings.Preset);
        Assert.Equal(RecorderArguments.DefaultQuality, settings.Quality);
        Assert.Contains(settings.Codec, RecorderView.Codecs);
        Assert.Contains(settings.Preset, RecorderView.Presets);
    }

    /// <summary>
    /// Hedef listesinin sirasi <see cref="RecorderTargetKind"/> ile ayni. Arayuz secimi
    /// indeksten okudugu icin sira kaymasi yanlis hedefi kaydettirirdi.
    /// </summary>
    [Fact]
    public void HedefListesininSirasiNumaralandirmayaBagli()
    {
        Assert.Equal(0, (int)RecorderTargetKind.Screen);
        Assert.Equal(1, (int)RecorderTargetKind.Window);
        Assert.Equal(2, (int)RecorderTargetKind.Region);
    }

    /// <summary>
    /// Gecen surenin yazimi. Bir saatin altinda dakika:saniye, ustunde saat de yaziliyor;
    /// sayi bicimi degismez kulturden geliyor, dil degisince kaymiyor.
    /// </summary>
    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(9, "00:09")]
    [InlineData(61, "01:01")]
    [InlineData(599, "09:59")]
    [InlineData(3600, "01:00:00")]
    [InlineData(3732, "01:02:12")]
    public void GecenSureninYazimi(int saniye, string beklenen)
        => Assert.Equal(beklenen, RecorderView.Clock(TimeSpan.FromSeconds(saniye)));

    /// <summary>
    /// Ayni saniyede iki kayit baslarsa ikincisi birincinin uzerine yazmiyor. Dosya adi
    /// zaman damgasindan geldigi icin bu gercek bir carpisma.
    /// </summary>
    [Fact]
    public void AyniSaniyedekiIkinciKayitUstuneYazmiyor()
    {
        var folder = Path.Combine(Path.GetTempPath(), "vidshrink-kaydedici-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            var settings = new RecorderSettings { OutputFolder = folder };
            var now = new DateTime(2026, 9, 12, 14, 30, 0, DateTimeKind.Local);

            var first = settings.OutputPath(now);
            File.WriteAllText(first, string.Empty);
            var second = settings.OutputPath(now);

            Assert.NotEqual(first, second);
            Assert.False(File.Exists(second));
            Assert.Equal(folder, Path.GetDirectoryName(second));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void KaydediciAyariAnaAyarinKlasorundeDurur()
    {
        var ana = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VidShrink");

        Assert.False(string.IsNullOrWhiteSpace(ana));
        Assert.Equal(Path.GetDirectoryName(ana), RecorderSettings.Folder);
        Assert.NotEqual(appData, RecorderSettings.Folder, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void KareAlinincaYoluSeritteGorunur()
    {
        var (ok, alinan, bildirim, hata, gorunur) = AppHost.Run<(bool, string?, string, string, bool)>(() =>
        {
            var view = new RecorderView();
            string? yol = null;
            var sonuc = view.SnapshotAsync(p => { yol = p; return System.Threading.Tasks.Task.FromResult(true); })
                .GetAwaiter().GetResult();
            return (sonuc, yol, view.NoticeText, view.ErrorText, Avalonia.Controls.ControlExtensions.FindControl<Avalonia.Controls.Button>(view, "BtnSnapshot")!.IsVisible);
        });

        Assert.True(ok);
        Assert.NotNull(alinan);
        Assert.StartsWith("kare_", Path.GetFileName(alinan));
        Assert.EndsWith(".png", alinan);
        Assert.Contains(alinan!, bildirim);
        Assert.Equal(string.Empty, hata);
        Assert.False(gorunur);
    }

    [Fact]
    public void AlinamayanKareHataOlarakGorunur()
    {
        var (basarisiz, atilan, bildirim, hata) = AppHost.Run<(bool, bool, string, string)>(() =>
        {
            var view = new RecorderView();
            var yanlis = view.SnapshotAsync(_ => System.Threading.Tasks.Task.FromResult(false)).GetAwaiter().GetResult();
            var patlayan = view.SnapshotAsync(_ => throw new InvalidOperationException("yok")).GetAwaiter().GetResult();
            return (yanlis, patlayan, view.NoticeText, view.ErrorText);
        });

        Assert.False(basarisiz);
        Assert.False(atilan);
        Assert.Equal(string.Empty, bildirim);
        Assert.NotEqual(string.Empty, hata);
    }

    [Theory]
    [InlineData("recorder.strip.snapshot")]
    [InlineData("recorder.snapshot.saved")]
    [InlineData("recorder.snapshot.failed")]
    public void KareAnahtarlariButunDillerde(string key)
    {
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            Assert.True(values.ContainsKey(key), $"{language} dilinde {key} yok.");
            Assert.False(string.IsNullOrWhiteSpace(values[key]), $"{language} dilinde {key} boş.");
        }
    }

    private static string CalismaKlasoru()
    {
        var kok = new DirectoryInfo(AppContext.BaseDirectory);
        while (kok is not null && !File.Exists(Path.Combine(kok.FullName, "VidShrink.sln"))) kok = kok.Parent;
        var klasor = Path.Combine(kok!.FullName, ".calisma", "kap-olcu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    [Fact]
    public void VarsayilanKapMatroska()
    {
        var settings = new RecorderSettings { OutputFolder = @"C:\kayit" };

        Assert.Equal(RecorderContainer.Mkv, settings.Container);
        Assert.EndsWith(".mkv", settings.OutputPath(new DateTime(2026, 9, 16, 10, 0, 0, DateTimeKind.Local)));
    }

    [Theory]
    [InlineData("{\"container\":\"Mp4\"}", RecorderContainer.Mkv)]
    [InlineData("{\"container\":\"Mov\"}", RecorderContainer.Mov)]
    [InlineData("{\"containerChoice\":\"Mp4\",\"container\":\"Mov\"}", RecorderContainer.Mp4)]
    [InlineData("{}", RecorderContainer.Mkv)]
    public void EskiAyardakiKapOkunur(string json, RecorderContainer beklenen)
    {
        var klasor = CalismaKlasoru();
        try
        {
            var dosya = Path.Combine(klasor, "recorder.json");
            File.WriteAllText(dosya, json);
            Assert.Equal(beklenen, RecorderSettings.Load(dosya).Container);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void SecilenMp4KabiKaydedilipGeriOkunur()
    {
        var klasor = CalismaKlasoru();
        try
        {
            var dosya = Path.Combine(klasor, "recorder.json");
            new RecorderSettings { Container = RecorderContainer.Mp4 }.Save(dosya);

            Assert.Contains("\"containerChoice\": \"Mp4\"", File.ReadAllText(dosya));
            Assert.Equal(RecorderContainer.Mp4, RecorderSettings.Load(dosya).Container);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void MatroskaSonucuMp4OlarakKaydedilir()
    {
        var klasor = CalismaKlasoru();
        try
        {
            var mkv = Path.Combine(klasor, "kayit.mkv");
            var mp4 = Path.Combine(klasor, "kayit.mp4");
            File.WriteAllText(mkv, "x");

            var (gorunur, hedef, argumanlar, bildirim, mp4Gorunur, hata) =
                AppHost.Run<(bool, string?, IReadOnlyList<string>?, string, bool, string)>(() =>
                {
                    var view = new RecorderView();
                    view.ShowResult(new VidShrink.Ffmpeg.RecordResult(true, mkv, 1, false, 0, string.Empty, 1));
                    var ilk = view.Mp4Visible;
                    IReadOnlyList<string>? yazilan = null;
                    var sonuc = view.SaveAsMp4Async(a => { yazilan = a; return System.Threading.Tasks.Task.FromResult(true); })
                        .GetAwaiter().GetResult();
                    var not = view.NoticeText;

                    var baska = new RecorderView();
                    baska.ShowResult(new VidShrink.Ffmpeg.RecordResult(true, mp4, 1, false, 0, string.Empty, 1));
                    var hataView = new RecorderView();
                    hataView.ShowResult(new VidShrink.Ffmpeg.RecordResult(true, mkv, 1, false, 0, string.Empty, 1));
                    hataView.SaveAsMp4Async(_ => System.Threading.Tasks.Task.FromResult(false)).GetAwaiter().GetResult();
                    return (ilk, sonuc, yazilan, not, baska.Mp4Visible, hataView.ErrorText);
                });

            Assert.True(gorunur);
            Assert.Equal(mp4, hedef);
            Assert.NotNull(argumanlar);
            Assert.Equal(mkv, argumanlar![argumanlar.ToList().IndexOf("-i") + 1]);
            Assert.Equal(mp4, argumanlar[^1]);
            Assert.Contains(mp4, bildirim);
            Assert.False(mp4Gorunur);
            Assert.NotEqual(string.Empty, hata);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Theory]
    [InlineData("recorder.output.to-mp4")]
    [InlineData("recorder.output.mp4-saved")]
    [InlineData("recorder.output.mp4-failed")]
    public void Mp4AnahtarlariButunDillerde(string key)
    {
        foreach (var language in Locales.Languages)
            Assert.False(string.IsNullOrWhiteSpace(Locales.Values(language).GetValueOrDefault(key)), $"{language} dilinde {key} yok.");
    }

    [Fact]
    public void BasitVeGelismisKipSecenekPaneliniSurer()
    {
        var dosya = RecorderSettings.FilePath!;
        var onceki = File.Exists(dosya) ? File.ReadAllBytes(dosya) : null;
        try
        {
            var olcu = AppHost.Run<(bool, bool, int, bool, bool, int, bool, bool, bool, bool)>(() =>
            {
                var view = new RecorderView();
                T Bul<T>(string ad) where T : Avalonia.Controls.Control
                    => Avalonia.Controls.ControlExtensions.FindControl<T>(view, ad)!;

                Bul<Avalonia.Controls.RadioButton>("RadSimple").IsChecked = true;
                var basitGelismis = view.AdvancedMode;
                var basitPanel = Bul<Avalonia.Controls.Border>("PanelOptions").IsVisible;
                var basitYayilim = Avalonia.Controls.Grid.GetColumnSpan(Bul<Avalonia.Controls.Border>("PanelTarget"));

                Bul<Avalonia.Controls.RadioButton>("RadAdvanced").IsChecked = true;
                Bul<Avalonia.Controls.RadioButton>("RadManual").IsChecked = true;
                var gelismisPanel = Bul<Avalonia.Controls.Border>("PanelOptions").IsVisible;
                var gelismisYayilim = Avalonia.Controls.Grid.GetColumnSpan(Bul<Avalonia.Controls.Border>("PanelTarget"));
                var gelismisElle = view.ManualMode;
                var elleKaydi = view.Settings.ManualMode && view.Settings.AdvancedMode;

                Bul<Avalonia.Controls.RadioButton>("RadSimple").IsChecked = true;
                return (basitGelismis, basitPanel, basitYayilim, gelismisPanel, gelismisElle, gelismisYayilim,
                    elleKaydi, view.ManualMode, view.AutoMode, view.Settings.ManualMode);
            });

            Assert.False(olcu.Item1);
            Assert.False(olcu.Item2);
            Assert.Equal(2, olcu.Item3);
            Assert.True(olcu.Item4);
            Assert.True(olcu.Item5);
            Assert.Equal(1, olcu.Item6);
            Assert.True(olcu.Item7);
            Assert.False(olcu.Item8);
            Assert.True(olcu.Item9);
            Assert.True(olcu.Item10);
        }
        finally
        {
            if (onceki is null) File.Delete(dosya);
            else File.WriteAllBytes(dosya, onceki);
        }
    }

    [Theory]
    [InlineData("{\"manualMode\":true}", true)]
    [InlineData("{\"manualMode\":false}", false)]
    [InlineData("{\"manualMode\":true,\"advancedMode\":false}", false)]
    [InlineData("{}", false)]
    public void EskiElleAyarGelismisKipteAcilir(string json, bool gelismis)
    {
        var klasor = CalismaKlasoru();
        try
        {
            var dosya = Path.Combine(klasor, "recorder.json");
            File.WriteAllText(dosya, json);
            Assert.Equal(gelismis, RecorderSettings.Load(dosya).AdvancedMode);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Theory]
    [InlineData("recorder.level.simple")]
    [InlineData("recorder.level.simple-hint")]
    [InlineData("recorder.level.advanced")]
    [InlineData("recorder.level.advanced-hint")]
    [InlineData("recorder.auto.automatic")]
    [InlineData("recorder.auto.automatic-hint")]
    public void KipAnahtarlariButunDillerde(string key)
    {
        foreach (var language in Locales.Languages)
            Assert.False(string.IsNullOrWhiteSpace(Locales.Values(language).GetValueOrDefault(key)), $"{language} dilinde {key} yok.");
    }

    [Fact]
    public void GeriSayimSeritteSayilirVeSonundaBaslatir()
    {
        var olcu = AppHost.Run<(bool, List<int>, List<string>, List<bool>, List<bool>, bool, bool, int)>(() =>
        {
            var view = new RecorderView();
            Avalonia.Controls.ControlExtensions.FindControl<Avalonia.Controls.ComboBox>(view, "CmbCountdown")!.SelectedIndex = 1;
            var kalan = new List<int>();
            var durum = new List<string>();
            var baslatGorunur = new List<bool>();
            var iptalGorunur = new List<bool>();
            view.CountdownDelay = (sure, _) =>
            {
                Assert.Equal(TimeSpan.FromSeconds(1), sure);
                kalan.Add(view.CountdownLeft);
                durum.Add(view.StateText);
                baslatGorunur.Add(Avalonia.Controls.ControlExtensions.FindControl<Avalonia.Controls.Button>(view, "BtnStart")!.IsVisible);
                iptalGorunur.Add(Avalonia.Controls.ControlExtensions.FindControl<Avalonia.Controls.Button>(view, "BtnCountdownCancel")!.IsVisible);
                return System.Threading.Tasks.Task.CompletedTask;
            };
            var bitti = view.CountdownAsync().GetAwaiter().GetResult();
            return (bitti, kalan, durum, baslatGorunur, iptalGorunur, view.CountingDown,
                Avalonia.Controls.ControlExtensions.FindControl<Avalonia.Controls.Button>(view, "BtnStart")!.IsVisible, view.SelectedCountdown);
        });

        Assert.True(olcu.Item1);
        Assert.Equal(new[] { 3, 2, 1 }, olcu.Item2);
        Assert.All(olcu.Item3.Zip(olcu.Item2), c => Assert.Contains(c.Second.ToString(System.Globalization.CultureInfo.InvariantCulture), c.First));
        Assert.All(olcu.Item4, b => Assert.False(b));
        Assert.All(olcu.Item5, b => Assert.True(b));
        Assert.False(olcu.Item6);
        Assert.True(olcu.Item7);
        Assert.Equal(3, olcu.Item8);
    }

    [Fact]
    public void GeriSayimIptalDugmesiVeDurdurmaTusuylaKesilir()
    {
        var olcu = AppHost.Run<(bool, int, bool, int, bool, int, bool)>(() =>
        {
            var view = new RecorderView();
            Avalonia.Controls.ControlExtensions.FindControl<Avalonia.Controls.ComboBox>(view, "CmbCountdown")!.SelectedIndex = 3;

            var ilkAdim = 0;
            view.CountdownDelay = (_, ct) =>
            {
                if (++ilkAdim == 2) Avalonia.Controls.ControlExtensions.FindControl<Avalonia.Controls.Button>(view, "BtnCountdownCancel")!
                    .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                return ct.IsCancellationRequested
                    ? System.Threading.Tasks.Task.FromCanceled(ct)
                    : System.Threading.Tasks.Task.CompletedTask;
            };
            var iptal = view.CountdownAsync().GetAwaiter().GetResult();

            var ikinciAdim = 0;
            view.CountdownDelay = (_, ct) =>
            {
                if (++ikinciAdim == 4) view.StopAsync().GetAwaiter().GetResult();
                ct.ThrowIfCancellationRequested();
                return System.Threading.Tasks.Task.CompletedTask;
            };
            var durdur = view.CountdownAsync().GetAwaiter().GetResult();

            var ucuncuAdim = 0;
            view.CountdownDelay = (_, ct) =>
            {
                if (++ucuncuAdim == 1) view.ToggleAsync().GetAwaiter().GetResult();
                return System.Threading.Tasks.Task.CompletedTask;
            };
            var tus = view.CountdownAsync().GetAwaiter().GetResult();

            return (iptal, ilkAdim, durdur, ikinciAdim, tus, ucuncuAdim, view.HasSession || view.CountingDown);
        });

        Assert.False(olcu.Item1);
        Assert.Equal(2, olcu.Item2);
        Assert.False(olcu.Item3);
        Assert.Equal(4, olcu.Item4);
        Assert.False(olcu.Item5);
        Assert.Equal(1, olcu.Item6);
        Assert.False(olcu.Item7);
    }

    [Fact]
    public void GeriSayimYokkenBekletmez()
    {
        var (bitti, cagri) = AppHost.Run<(bool, int)>(() =>
        {
            var view = new RecorderView();
            Avalonia.Controls.ControlExtensions.FindControl<Avalonia.Controls.ComboBox>(view, "CmbCountdown")!.SelectedIndex = 0;
            var sayi = 0;
            view.CountdownDelay = (_, _) => { sayi++; return System.Threading.Tasks.Task.CompletedTask; };
            return (view.CountdownAsync().GetAwaiter().GetResult(), sayi);
        });

        Assert.True(bitti);
        Assert.Equal(0, cagri);
    }

    [Fact]
    public void MiniSeritGeriSayimiGosterirVeIptaleIzinVerir()
    {
        var (metin, durdur, ustte, bosMetin, bosDurdur) = AppHost.Run<(string, bool, bool, string, bool)>(() =>
        {
            var mini = new RecorderMini();
            mini.Follow(VidShrink.Ffmpeg.RecorderState.Stopped, "00:00", 5);
            var sayim = (mini.TxtElapsed.Text ?? string.Empty, mini.BtnStop.IsVisible, mini.Topmost);
            mini.Follow(VidShrink.Ffmpeg.RecorderState.Stopped, "00:00");
            return (sayim.Item1, sayim.Item2, sayim.Item3, mini.TxtElapsed.Text ?? string.Empty, mini.BtnStop.IsVisible);
        });

        Assert.Equal("5", metin);
        Assert.True(durdur);
        Assert.True(ustte);
        Assert.Equal("00:00", bosMetin);
        Assert.False(bosDurdur);
    }

    [Theory]
    [InlineData("{\"countdownSeconds\":5}", 5)]
    [InlineData("{\"countdownSeconds\":10}", 10)]
    [InlineData("{\"countdownSeconds\":7}", 0)]
    [InlineData("{\"countdownSeconds\":-3}", 0)]
    [InlineData("{}", 0)]
    public void GeriSayimAyariYalnizSecenekleriKabulEder(string json, int beklenen)
    {
        var klasor = CalismaKlasoru();
        try
        {
            var dosya = Path.Combine(klasor, "recorder.json");
            File.WriteAllText(dosya, json);
            var okunan = RecorderSettings.Load(dosya);
            Assert.Equal(beklenen, okunan.CountdownSeconds);
            okunan.Save(dosya);
            Assert.Equal(beklenen, RecorderSettings.Load(dosya).CountdownSeconds);
        }
        finally
        {
            Directory.Delete(klasor, true);
        }
    }

    [Fact]
    public void KayitGeriSayimBittiktenSonraBaslar()
    {
        var kok = new DirectoryInfo(AppContext.BaseDirectory);
        while (kok is not null && !File.Exists(Path.Combine(kok.FullName, "VidShrink.sln"))) kok = kok.Parent;
        var serit = File.ReadAllText(Path.Combine(kok!.FullName, "src", "VidShrink.App", "Recorder", "RecorderView.Serit.cs"));

        var sayim = serit.IndexOf("await CountdownAsync()", StringComparison.Ordinal);
        var dogrulama = serit.IndexOf("RecorderArguments.Validate(request, path)", StringComparison.Ordinal);
        var oturum = serit.IndexOf("RecorderSession.StartAsync(", StringComparison.Ordinal);
        Assert.True(dogrulama >= 0 && sayim > dogrulama && oturum > sayim);
    }

    [Theory]
    [InlineData("recorder.countdown.title")]
    [InlineData("recorder.countdown.hint")]
    [InlineData("recorder.countdown.off")]
    [InlineData("recorder.countdown.seconds")]
    [InlineData("recorder.countdown.left")]
    [InlineData("recorder.countdown.cancel")]
    public void GeriSayimAnahtarlariButunDillerde(string key)
    {
        foreach (var language in Locales.Languages)
            Assert.False(string.IsNullOrWhiteSpace(Locales.Values(language).GetValueOrDefault(key)), $"{language} dilinde {key} yok.");
    }

    [Fact]
    public void KareDugmesiOturumunKaresiniAlir()
    {
        var kok = new DirectoryInfo(AppContext.BaseDirectory);
        while (kok is not null && !File.Exists(Path.Combine(kok.FullName, "VidShrink.sln"))) kok = kok.Parent;
        var serit = File.ReadAllText(Path.Combine(kok!.FullName, "src", "VidShrink.App", "Recorder", "RecorderView.Serit.cs"));
        var xaml = File.ReadAllText(Path.Combine(kok.FullName, "src", "VidShrink.App", "Recorder", "RecorderView.axaml"));

        Assert.Contains("session.SnapshotAsync(path)", serit);
        Assert.Contains("BtnSnapshot.IsVisible = running;", serit);
        Assert.Contains("Click=\"OnSnapshot\"", xaml);
    }
}
