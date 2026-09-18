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

    [Theory]
    [InlineData(100, 200, 640, 360, 2, 98, 198, 644, 364)]
    [InlineData(0, 0, 1280, 720, 3, -3, -3, 1286, 726)]
    public void CerceveBolgeninDisinaCizilir(int x, int y, int w, int h, int kenar, int ox, int oy, int ow, int oh)
    {
        var bolge = new Avalonia.PixelRect(x, y, w, h);
        var dis = RecorderFrame.Outer(bolge, kenar);

        Assert.Equal(new Avalonia.PixelRect(ox, oy, ow, oh), dis);
        Assert.True(dis.Contains(bolge));
        Assert.Equal(bolge, bolge.Intersect(dis));
        Assert.True(dis.X + kenar <= bolge.X && dis.Right - kenar >= bolge.Right);
    }

    [Theory]
    [InlineData(2.0, 1.0, 2)]
    [InlineData(2.0, 1.5, 3)]
    [InlineData(2.0, 1.25, 3)]
    [InlineData(0.0, 2.0, 1)]
    public void CerceveKenariOlceklemeyleYuvarlanir(double kalinlik, double olcek, int beklenen)
        => Assert.Equal(beklenen, RecorderFrame.EdgePixels(kalinlik, olcek));

    [Fact]
    public void CerceveTiklamayiGecirirVeOdakAlmaz()
    {
        var stil = RecorderFrame.ClickThroughStyle(0x100);

        Assert.Equal(0x100, stil & 0x100);
        Assert.NotEqual(0, stil & 0x20);
        Assert.NotEqual(0, stil & 0x80000);
        Assert.NotEqual(0, stil & 0x80);
        Assert.NotEqual(0, stil & 0x08000000);
        Assert.Equal(0, RecorderFrame.ClickThroughStyle(0) & 0x10);

        var (ustte, odak, vurus, gorev, kalinlik) = AppHost.Run<(bool, bool, bool, bool, double)>(() =>
        {
            var cerceve = new RecorderFrame();
            return (cerceve.Topmost, cerceve.ShowActivated, cerceve.IsHitTestVisible, cerceve.ShowInTaskbar, cerceve.Thickness);
        });

        Assert.True(ustte);
        Assert.False(odak);
        Assert.False(vurus);
        Assert.False(gorev);
        Assert.True(kalinlik > 0);
    }

    [Fact]
    public void CerceveYalnizBolgeKaydindaVeGizlenmemisseIstenir()
    {
        var bolge = new Avalonia.PixelRect(10, 20, 320, 240);

        Assert.Equal(bolge, RecorderFrame.Wanted(true, bolge, false));
        Assert.Null(RecorderFrame.Wanted(false, bolge, false));
        Assert.Null(RecorderFrame.Wanted(true, bolge, true));
        Assert.Null(RecorderFrame.Wanted(true, null, false));
    }

    private sealed class SahteCerceve : IRecorderFrameHost
    {
        public List<string> Cagrilar { get; } = new();

        public void Show(Avalonia.PixelRect region) => Cagrilar.Add("goster");

        public void Hide() => Cagrilar.Add("gizle");
    }

    [Fact]
    public void CerceveKisayoluGizlemeyiCevirir()
    {
        var (ilk, ikinci, cagrilar) = AppHost.Run<(bool, bool, List<string>)>(() =>
        {
            var view = new RecorderView();
            var sahte = new SahteCerceve();
            view.FrameHost = sahte;
            view.RaiseEvent(new Avalonia.Input.KeyEventArgs
            {
                RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
                Key = Avalonia.Input.Key.F9
            });
            var once = view.FrameHiddenByUser;
            view.ToggleFrame();
            return (once, view.FrameHiddenByUser, sahte.Cagrilar);
        });

        Assert.True(ilk);
        Assert.False(ikinci);
        Assert.Empty(cagrilar);
    }

    [Fact]
    public void CerceveKayitBaslayincaKurulurDurunkaKalkar()
    {
        var kok = new DirectoryInfo(AppContext.BaseDirectory);
        while (kok is not null && !File.Exists(Path.Combine(kok.FullName, "VidShrink.sln"))) kok = kok.Parent;
        var serit = File.ReadAllText(Path.Combine(kok!.FullName, "src", "VidShrink.App", "Recorder", "RecorderView.Serit.cs"));

        var oturum = serit.IndexOf("RecorderSession.StartAsync(", StringComparison.Ordinal);
        var bolge = serit.IndexOf("_frameRegion = RegionOf(request);", StringComparison.Ordinal);
        Assert.True(oturum >= 0 && bolge > oturum);
        Assert.Contains("_frameRegion = null;", serit);
        Assert.Contains("SyncFrame();", serit);
    }

    [Theory]
    [InlineData(false, VidShrink.Ffmpeg.RecorderState.Running, 0)]
    [InlineData(false, VidShrink.Ffmpeg.RecorderState.Paused, 0)]
    [InlineData(true, VidShrink.Ffmpeg.RecorderState.Running, 1)]
    [InlineData(true, VidShrink.Ffmpeg.RecorderState.Paused, 2)]
    [InlineData(true, VidShrink.Ffmpeg.RecorderState.Stopped, 0)]
    public void TepsiDurumuOturumdanOkunur(bool oturum, VidShrink.Ffmpeg.RecorderState hal, int beklenen)
        => Assert.Equal(beklenen, (int)RecorderTray.PhaseOf(oturum, hal));

    [Fact]
    public void TepsiUcDurumuPaletinUcAyriRengindenBoyar()
    {
        var anahtarlar = Enum.GetValues<TrayPhase>().Select(RecorderTray.BrushKey).ToList();
        Assert.Equal(3, anahtarlar.Distinct(StringComparer.Ordinal).Count());

        var renkler = AppHost.Run<List<Avalonia.Media.Color?>>(() => anahtarlar
            .Select(k => Avalonia.Application.Current!.TryGetResource(k, null, out var v) && v is Avalonia.Media.ISolidColorBrush b
                ? b.Color
                : (Avalonia.Media.Color?)null)
            .ToList());

        Assert.All(renkler, r => Assert.NotNull(r));
        Assert.Equal(3, renkler.Distinct().Count());
    }

    [Fact]
    public void TepsiSimgesiVerilenRenkteCizilir()
    {
        var (orta, kose) = AppHost.Run<(uint, uint)>(() =>
        {
            var renk = Avalonia.Media.Color.FromRgb(0xF0, 0x71, 0x78);
            var simge = RecorderTray.Render(renk);
            using var akis = new MemoryStream();
            simge.Save(akis);
            akis.Position = 0;
            using var resim = Avalonia.Media.Imaging.WriteableBitmap.Decode(akis);
            using var kilit = resim.Lock();
            uint Oku(int x, int y) => (uint)System.Runtime.InteropServices.Marshal.ReadInt32(kilit.Address + y * kilit.RowBytes + x * 4);
            return (Oku(RecorderTray.IconPixels / 2, RecorderTray.IconPixels / 2), Oku(0, 0));
        });

        Assert.Equal(0xFFu, orta >> 24);
        Assert.Equal(new[] { 0xF0u, 0x71u, 0x78u }.OrderBy(v => v), new[] { (orta >> 16) & 0xFF, (orta >> 8) & 0xFF, orta & 0xFF }.OrderBy(v => v));
        Assert.Equal(0u, kose >> 24);
    }

    private sealed class SahteTepsi : IRecorderTrayHost
    {
        public List<(TrayPhase, Avalonia.Media.Color, string)> Guncellemeler { get; } = new();

        public int Kaldirma { get; private set; }

        public void Update(TrayPhase phase, Avalonia.Media.Color color, string tip) => Guncellemeler.Add((phase, color, tip));

        public void Remove() => Kaldirma++;
    }

    [Fact]
    public void TepsiEtkinlesinceBostaIpucuVeRengiYazilir()
    {
        var (once, guncellemeler, kaldirma, bosta, gri) = AppHost.Run<(int, List<(TrayPhase, Avalonia.Media.Color, string)>, int, string, Avalonia.Media.Color)>(() =>
        {
            var view = new RecorderView();
            var sahte = new SahteTepsi();
            view.TrayHost = sahte;
            var ilk = sahte.Guncellemeler.Count;
            view.ActivateTray();
            view.DeactivateTray();
            view.DeactivateTray();
            Avalonia.Application.Current!.TryGetResource("TextDisabled", null, out var v);
            return (ilk, sahte.Guncellemeler, sahte.Kaldirma,
                VidShrink.App.LanguageCatalog.Display(VidShrink.App.Localization.Strings.Get("recorder.strip.idle")),
                ((Avalonia.Media.ISolidColorBrush)v!).Color);
        });

        Assert.Equal(0, once);
        Assert.Single(guncellemeler);
        Assert.Equal(TrayPhase.Idle, guncellemeler[0].Item1);
        Assert.Equal(gri, guncellemeler[0].Item2);
        Assert.Contains(bosta, guncellemeler[0].Item3);
        Assert.Equal(1, kaldirma);
    }

    [Fact]
    public void TepsiIpucundaAnlikBoyutVeSureVar()
    {
        Assert.Equal("3,4", RecorderTray.Megabytes(3.44, new System.Globalization.CultureInfo("tr-TR")));
        Assert.Equal("12.0", RecorderTray.Megabytes(12, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("0.0", RecorderTray.Megabytes(-1, System.Globalization.CultureInfo.InvariantCulture));

        var kok = new DirectoryInfo(AppContext.BaseDirectory);
        while (kok is not null && !File.Exists(Path.Combine(kok.FullName, "VidShrink.sln"))) kok = kok.Parent;
        var tepsi = File.ReadAllText(Path.Combine(kok!.FullName, "src", "VidShrink.App", "Recorder", "RecorderView.Tepsi.cs"));
        var serit = File.ReadAllText(Path.Combine(kok.FullName, "src", "VidShrink.App", "Recorder", "RecorderView.Serit.cs"));
        var tr = Locales.Values("tr");

        Assert.Contains("_session?.WrittenMb", tepsi);
        Assert.Contains("ElapsedText", tepsi);
        Assert.Contains("{1}", tr["recorder.tray.live"]);
        Assert.Contains("{2} MB", tr["recorder.tray.live"]);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(serit, @"SyncTray\(\);").Count);
        foreach (var language in Locales.Languages)
        {
            Assert.False(string.IsNullOrWhiteSpace(Locales.Values(language).GetValueOrDefault("recorder.tray.idle")));
            Assert.False(string.IsNullOrWhiteSpace(Locales.Values(language).GetValueOrDefault("recorder.tray.live")));
        }
    }

    private sealed class SahteKisayol : IGlobalHotkeys
    {
        public HashSet<Avalonia.Input.Key> Dolu { get; } = new();

        public int Kayit { get; private set; }

        public int Birakma { get; private set; }

        public Action<HotkeyAction>? Basildi { get; private set; }

        public IReadOnlyList<HotkeyBinding> Register(IReadOnlyList<HotkeyBinding> bindings, Action<HotkeyAction> pressed)
        {
            Kayit++;
            Basildi = pressed;
            return bindings.Where(b => Dolu.Contains(b.Key)).ToList();
        }

        public void Unregister() => Birakma++;
    }

    [Fact]
    public void KisayolTanimiTekYerde()
    {
        Assert.Equal(HotkeyAction.Toggle, RecorderHotkeys.ActionOf(Avalonia.Input.Key.F7, Avalonia.Input.KeyModifiers.None));
        Assert.Equal(HotkeyAction.Stop, RecorderHotkeys.ActionOf(Avalonia.Input.Key.F8, Avalonia.Input.KeyModifiers.None));
        Assert.Equal(HotkeyAction.Frame, RecorderHotkeys.ActionOf(Avalonia.Input.Key.F9, Avalonia.Input.KeyModifiers.None));
        Assert.Equal(HotkeyAction.Discard, RecorderHotkeys.ActionOf(Avalonia.Input.Key.F10, Avalonia.Input.KeyModifiers.None));
        Assert.Equal(HotkeyAction.ReplaySave, RecorderHotkeys.ActionOf(Avalonia.Input.Key.F11, Avalonia.Input.KeyModifiers.None));
        Assert.Null(RecorderHotkeys.ActionOf(Avalonia.Input.Key.F7, Avalonia.Input.KeyModifiers.Control));
        Assert.Null(RecorderHotkeys.ActionOf(Avalonia.Input.Key.F6, Avalonia.Input.KeyModifiers.None));
        Assert.Equal(5, RecorderHotkeys.All.Select(b => b.VirtualKey).Distinct().Count());
        Assert.Equal(Enum.GetValues<HotkeyAction>().Length, RecorderHotkeys.All.Select(b => b.Action).Distinct().Count());
    }

    [Fact]
    public void TesteGercekKisayolKaydedilmez()
    {
        var tur = AppHost.Run<string>(() => new RecorderView().GlobalHotkeys.GetType().Name);

        Assert.Equal(nameof(NoGlobalHotkeys), tur);
    }

    [Fact]
    public void GenelKisayolCakismasiKullaniciyaSoylenir()
    {
        var (kayit, birakma, cakisma, hata, bekleyen) = AppHost.Run<(int, int, string, string, string)>(() =>
        {
            var view = new RecorderView();
            var sahte = new SahteKisayol();
            sahte.Dolu.Add(Avalonia.Input.Key.F8);
            view.GlobalHotkeys = sahte;
            view.ActivateHotkeys();
            view.ActivateHotkeys();
            var c = RecorderHotkeys.Names(view.HotkeyConflicts);
            var h = view.ErrorText;
            view.DeactivateHotkeys();
            view.DeactivateHotkeys();
            return (sahte.Kayit, sahte.Birakma, c, h,
                VidShrink.App.LanguageCatalog.Display(VidShrink.App.Localization.Strings.Get("recorder.hotkey.taken")));
        });

        Assert.Equal(1, kayit);
        Assert.Equal(1, birakma);
        Assert.Equal("F8", cakisma);
        Assert.Contains("F8", hata);
        Assert.Contains("{0}", bekleyen);
    }

    [Fact]
    public void GenelKisayolBasilincaEylemCalisir()
    {
        var (gizli, durdu, yenidenGorunur) = AppHost.Run<(bool, bool, bool)>(() =>
        {
            var view = new RecorderView();
            var sahte = new SahteKisayol();
            view.GlobalHotkeys = sahte;
            view.ActivateHotkeys();
            sahte.Basildi!(HotkeyAction.Frame);
            var g = view.FrameHiddenByUser;
            var d = view.RunHotkeyAsync(HotkeyAction.Stop).GetAwaiter().GetResult();
            sahte.Basildi!(HotkeyAction.Frame);
            return (g, d, !view.FrameHiddenByUser);
        });

        Assert.True(gizli);
        Assert.False(durdu);
        Assert.True(yenidenGorunur);
    }

    [Fact]
    public void KisayolAnahtariButunDillerde()
    {
        var kok = new DirectoryInfo(AppContext.BaseDirectory);
        while (kok is not null && !File.Exists(Path.Combine(kok.FullName, "VidShrink.sln"))) kok = kok.Parent;
        var bag = File.ReadAllText(Path.Combine(kok!.FullName, "src", "VidShrink.App", "Recorder", "RecorderView.axaml.cs"));

        Assert.Contains("ActivateHotkeys();", bag);
        Assert.Contains("DeactivateHotkeys();", bag);
        foreach (var language in Locales.Languages)
            Assert.Contains("{0}", Locales.Values(language).GetValueOrDefault("recorder.hotkey.taken") ?? string.Empty);
    }

    /// <summary>
    /// Ölçüm, paylaşılan kaydedici ayar dosyasını yedekleyip <c>finally</c>'de geri yazar.
    /// Yedek her zaman yetmiyor: kutulara değer verildiğinde <c>PersistChoices</c> işi
    /// sargının dışına taşıyor ve dosyada 1280x720 kalıyor. Bu, <c>KaydediciPencereTests</c>
    /// testini CI'da kırdı (koşum 35291760780). Sızıntı orada <c>Scale = null</c> ile
    /// kanalından kapatıldı; kaynağındaki temizlik kaydedici sahibinin işi.
    /// <c>Dispatcher.UIThread.RunJobs()</c> ile boşaltmak <b>denendi ve reddedildi</b>:
    /// arayüz iş parçacığı bütün test sınıflarınca paylaşıldığı için pompa başka
    /// sınıfların bekleyen işlerini de sırasız boşaltıyor ve
    /// <c>OynaticiCiftTikSuresiTests</c> iki CI koşumunda düştü
    /// (<c>docs/olcumler/aot-dalgasi.md</c> bölüm 12).
    /// </summary>
    private static T GelismisOlc<T>(Func<RecorderView, Func<string, Avalonia.Controls.Control>, T> olc)
    {
        var dosya = RecorderSettings.FilePath!;
        var onceki = File.Exists(dosya) ? File.ReadAllBytes(dosya) : null;
        try
        {
            return AppHost.Run<T>(() =>
            {
                var view = new RecorderView();
                Avalonia.Controls.Control Bul(string ad) => Avalonia.Controls.ControlExtensions.FindControl<Avalonia.Controls.Control>(view, ad)!;
                ((Avalonia.Controls.RadioButton)Bul("RadAdvanced")).IsChecked = true;
                ((Avalonia.Controls.RadioButton)Bul("RadManual")).IsChecked = true;
                return olc(view, Bul);
            });
        }
        finally
        {
            if (onceki is null) File.Delete(dosya);
            else File.WriteAllBytes(dosya, onceki);
        }
    }

    private static void Sec(Avalonia.Controls.Control kutu, string oge)
    {
        var combo = (Avalonia.Controls.ComboBox)kutu;
        combo.SelectedIndex = combo.ItemsSource!.Cast<object>().Select(o => o.ToString()).ToList().IndexOf(oge);
    }

    [Fact]
    public void GelismisPanelYalnizGelismisKipteGorunur()
    {
        var (gelismis, basit) = GelismisOlc((view, bul) =>
        {
            var g = bul("PanelAdvanced").IsVisible;
            ((Avalonia.Controls.RadioButton)bul("RadSimple")).IsChecked = true;
            return (g, bul("PanelAdvanced").IsVisible);
        });

        Assert.True(gelismis);
        Assert.False(basit);
    }

    [Fact]
    public void GelismisKollarIstegeVeAyaraGecer()
    {
        var (istek, ayar, hata) = GelismisOlc<(VidShrink.Core.RecorderRequest?, RecorderSettings, string)>((view, bul) =>
        {
            Sec(bul("CmbCodec"), "libx264");
            Sec(bul("CmbContainer"), "MOV");
            ((Avalonia.Controls.TextBox)bul("TxtScaleWidth")).Text = "1280";
            ((Avalonia.Controls.TextBox)bul("TxtScaleHeight")).Text = "720";
            ((Avalonia.Controls.TextBox)bul("TxtKeyframe")).Text = "4";
            Sec(bul("CmbProfile"), "high");
            Sec(bul("CmbTune"), "zerolatency");
            ((Avalonia.Controls.ComboBox)bul("CmbRateControl")).SelectedIndex = (int)VidShrink.Core.RecorderRateControl.Bitrate;
            ((Avalonia.Controls.TextBox)bul("TxtBitrate")).Text = "3000";
            ((Avalonia.Controls.TextBox)bul("TxtMaxBitrate")).Text = "4000";
            ((Avalonia.Controls.TextBox)bul("TxtBuffer")).Text = "8000";
            Sec(bul("CmbPixelFormat"), "yuv444p");
            Sec(bul("CmbColorSpace"), "bt709");
            Sec(bul("CmbColorRange"), "pc");
            ((Avalonia.Controls.TextBox)bul("TxtMaxDuration")).Text = "60";
            ((Avalonia.Controls.TextBox)bul("TxtSplitSeconds")).Text = "30";
            ((Avalonia.Controls.TextBox)bul("TxtSplitMegabytes")).Text = "";
            ((Avalonia.Controls.ComboBox)bul("CmbAudioLayout")).SelectedIndex = (int)VidShrink.Core.AudioTrackLayout.SeparateTracks;
            ((Avalonia.Controls.TextBox)bul("TxtAudioGain")).Text = "-3.5";
            ((Avalonia.Controls.CheckBox)bul("ChkNoiseGate")).IsChecked = true;
            ((Avalonia.Controls.CheckBox)bul("ChkNoiseSuppression")).IsChecked = true;
            return (view.BuildRequest(applyAuto: false), view.Settings, view.ErrorText);
        });

        Assert.Equal(string.Empty, hata);
        Assert.NotNull(istek);
        Assert.Equal(VidShrink.Core.RecorderContainer.Mov, istek!.Container);
        Assert.Equal(new VidShrink.Core.RecorderScale(1280, 720), istek.Scale);
        Assert.Equal(4, istek.KeyframeSeconds);
        Assert.Equal("high", istek.Profile);
        Assert.Equal("zerolatency", istek.Tune);
        Assert.Equal(VidShrink.Core.RecorderRateControl.Bitrate, istek.RateControl);
        Assert.Equal(3000, istek.BitrateKbps);
        Assert.Equal(4000, istek.MaxBitrateKbps);
        Assert.Equal(8000, istek.BufferKbits);
        Assert.Equal("yuv444p", istek.PixelFormat);
        Assert.Equal("bt709", istek.ColorSpace);
        Assert.Equal("pc", istek.ColorRange);
        Assert.Equal(TimeSpan.FromSeconds(60), istek.MaxDuration);
        Assert.Equal(TimeSpan.FromSeconds(30), istek.Split!.Duration);
        Assert.Null(istek.Split.Megabytes);
        Assert.Equal(VidShrink.Core.AudioTrackLayout.SeparateTracks, ayar.AudioLayout);
        Assert.Equal(-3.5, ayar.AudioGainDb);
        Assert.True(ayar.AudioNoiseGate);
        Assert.True(ayar.AudioNoiseSuppression);
    }

    [Fact]
    public void GelismisSayiOkunamazsaIstekKurulmaz()
    {
        var (istek, hata, etiket) = GelismisOlc((view, bul) =>
        {
            ((Avalonia.Controls.TextBox)bul("TxtKeyframe")).Text = "iki";
            return (view.BuildRequest(applyAuto: false), view.ErrorText,
                VidShrink.App.LanguageCatalog.Display(VidShrink.App.Localization.Strings.Get("recorder.advanced.keyframe")));
        });

        Assert.Null(istek);
        Assert.Contains(etiket, hata);
    }

    [Fact]
    public void KodlayiciDegisinceProfilVeBicimListesiYenilenir()
    {
        var (x264, vp9Profil, vp9Etkin, av1Bicimler, secilenBicim) = GelismisOlc((view, bul) =>
        {
            Sec(bul("CmbCodec"), "libx264");
            var profiller = ((Avalonia.Controls.ComboBox)bul("CmbProfile")).ItemsSource!.Cast<object>().Select(o => o.ToString()!).ToList();
            Sec(bul("CmbPixelFormat"), "yuv444p");
            Sec(bul("CmbCodec"), "libvpx-vp9");
            var profil = (Avalonia.Controls.ComboBox)bul("CmbProfile");
            var vp9 = profil.ItemsSource!.Cast<object>().Count();
            var etkin = profil.IsEnabled;
            Sec(bul("CmbCodec"), "libsvtav1");
            var bicim = (Avalonia.Controls.ComboBox)bul("CmbPixelFormat");
            return (profiller, vp9, etkin, bicim.ItemsSource!.Cast<object>().Select(o => o.ToString()!).ToList(), bicim.SelectedItem as string);
        });

        Assert.Contains("high", x264);
        Assert.Equal(VidShrink.Core.RecorderArguments.ProfilesFor("libvpx-vp9").Count + 1, vp9Profil);
        Assert.True(vp9Etkin);
        Assert.Equal(VidShrink.Core.RecorderArguments.PixelFormatsFor("libsvtav1"), av1Bicimler);
        Assert.Equal(VidShrink.Core.RecorderArguments.DefaultPixelFormat, secilenBicim);
    }

    [Fact]
    public void OtomatikKodlayiciyaUymayanKolDusurulur()
    {
        var istek = new VidShrink.Core.RecorderRequest
        {
            Platform = VidShrink.Core.RecorderPlatform.Windows,
            Target = VidShrink.Core.RecorderTargetKind.Screen,
            VideoCodec = "hevc_nvenc",
            Profile = "high",
            Tune = "zerolatency",
            PixelFormat = "yuv422p"
        };

        var uyan = RecorderView.FitToCodec(istek);
        var dokunulmayan = RecorderView.FitToCodec(istek with { VideoCodec = "libx264" });

        Assert.Null(uyan.Profile);
        Assert.Null(uyan.Tune);
        Assert.Equal(VidShrink.Core.RecorderArguments.DefaultPixelFormat, uyan.PixelFormat);
        Assert.Equal("high", dokunulmayan.Profile);
        Assert.Equal("zerolatency", dokunulmayan.Tune);
        Assert.Equal("yuv422p", dokunulmayan.PixelFormat);
    }

    [Fact]
    public void GelismisAnahtarlariButunDillerde()
    {
        var kok = new DirectoryInfo(AppContext.BaseDirectory);
        while (kok is not null && !File.Exists(Path.Combine(kok.FullName, "VidShrink.sln"))) kok = kok.Parent;
        var xaml = File.ReadAllText(Path.Combine(kok!.FullName, "src", "VidShrink.App", "Recorder", "RecorderView.axaml"));
        var kod = File.ReadAllText(Path.Combine(kok.FullName, "src", "VidShrink.App", "Recorder", "RecorderView.Gelismis.cs"));
        var anahtarlar = System.Text.RegularExpressions.Regex.Matches(xaml + kod, @"recorder\.advanced\.[a-z-]+")
            .Select(m => m.Value).Distinct().ToList();

        Assert.True(anahtarlar.Count >= 25);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            foreach (var anahtar in anahtarlar)
                Assert.False(string.IsNullOrWhiteSpace(values.GetValueOrDefault(anahtar)), language + " " + anahtar);
        }
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
