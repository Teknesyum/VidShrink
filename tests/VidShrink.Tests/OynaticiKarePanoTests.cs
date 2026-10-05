using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal static class KarePanoKanit
{
    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "oynatici-kare-pano");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static void Kapat(params string[] adlar) => KanitKapanisi.Kapat(Folder, adlar);

    internal static string Sahte(string ad)
    {
        var path = Path.Combine(Folder, ad);
        File.WriteAllBytes(path, new byte[16]);
        return path;
    }

    internal static string Klip
    {
        get
        {
            const string ad = "kare-160x90.mkv";
            var path = Path.Combine(Folder, ad);
            if (File.Exists(path) && new FileInfo(path).Length > 0) return path;
            Assert.True(ToolLocator.IsAvailable(out var missing), $"klip uretimi icin {missing} gerekli");
            var partial = Path.Combine(Folder, "part-" + ad);
            var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg,
                "-y", "-hide_banner", "-f", "lavfi", "-i", "testsrc2=size=160x90:rate=10:duration=2",
                "-threads", "2", "-c:v", "libx264", "-preset", "ultrafast", "-g", "5", "-pix_fmt", "yuv420p", partial);
            Assert.True(kod == 0, $"ffmpeg {ad} uretemedi (kod {kod}): {hata[Math.Max(0, hata.Length - 600)..]}");
            File.Move(partial, path, true);
            return path;
        }
    }
}

internal sealed class SahtePano
{
    private readonly bool _kabul;
    private readonly Exception? _hata;

    internal SahtePano(bool kabul = true, Exception? hata = null)
    {
        _kabul = kabul;
        _hata = hata;
    }

    internal List<PixelSize> Gelen { get; } = new();

    internal Task<bool> Al(TopLevel? ust, Bitmap kare)
    {
        Gelen.Add(kare.PixelSize);
        return _hata is null ? Task.FromResult(_kabul) : Task.FromException<bool>(_hata);
    }
}

internal sealed class KareMotoru : IPlaybackEngine
{
    internal const int En = 64;
    internal const int Boy = 36;

    private readonly byte[] _piksel = Enumerable.Repeat((byte)128, 4 * En * Boy).ToArray();
    private readonly bool _kaydeder;

    internal KareMotoru(bool kaydeder = true) => _kaydeder = kaydeder;

    internal List<string> Istenen { get; } = new();

    public string Name => "kare-motoru";

    public bool IsOpen { get; private set; }

    public double DurationSeconds => 600;

    public bool HasAudio => false;

    public bool IsPaused { get; private set; } = true;

    public bool EndReached => false;

    public double PositionSeconds { get; private set; }

    public double AudioVideoOffsetSeconds => 0;

    public long FramesRendered { get; private set; }

    public event EventHandler<PlaybackFault>? Faulted { add { } remove { } }

    public double Speed { get; private set; } = 1;

    public Task OpenAsync(string path, CancellationToken ct = default)
    {
        IsOpen = true;
        FramesRendered = 1;
        return Task.CompletedTask;
    }

    public void Play() => IsPaused = false;

    public void Pause() => IsPaused = true;

    public void SetSpeed(double speed) => Speed = speed;

    public Task<SeekResult> SeekAsync(double seconds, SeekPrecision precision, CancellationToken ct = default)
    {
        PositionSeconds = seconds;
        FramesRendered++;
        return Task.FromResult(new SeekResult(SeekOutcome.Shown, 1));
    }

    public Task<bool> SaveScreenshotAsync(string path, CancellationToken ct = default)
    {
        Istenen.Add(path);
        if (!_kaydeder) return Task.FromResult(false);
        using var kare = new WriteableBitmap(new PixelSize(En, Boy), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        kare.Save(path, PngBitmapEncoderOptions.Default);
        return Task.FromResult(true);
    }

    public bool TryCopyLatest(ref long seen, FrameCopy copy)
    {
        if (seen == FramesRendered) return false;
        seen = FramesRendered;
        var handle = GCHandle.Alloc(_piksel, GCHandleType.Pinned);
        try
        {
            copy(handle.AddrOfPinnedObject(), En, Boy, En * 4);
        }
        finally
        {
            handle.Free();
        }

        return true;
    }

    public void Dispose() => IsOpen = false;
}

/// <summary>
/// Kareyi panoya kopyalama (Ctrl+Shift+E) ve ekran goruntusunun JPG secenegi. Pano sahte
/// <see cref="PlayerView.FrameCopier"/> kancasidir, gercek sistem panosuna yazilmaz; ayar ve ara
/// dosya <c>.calisma/oynatici-kare-pano</c> altinda kalir. Gercek libmpv kolu 2 sn'lik 160x90 klipte:
/// panoya giden karenin boyu kaynagin boyudur, kaydedilen dosyanin bas baytlari bicimi soyler.
/// </summary>
public sealed class OynaticiKarePanoTests
{
    private static string Gecmis(string ad)
    {
        var gecmis = Path.Combine(KarePanoKanit.Folder, "gecmis-" + ad, "gecmis.json");
        Directory.CreateDirectory(Path.GetDirectoryName(gecmis)!);
        return gecmis;
    }

    private static void Temizle(string ad)
    {
        foreach (var klasor in new[] { "gecmis-" + ad, "ara-" + ad, "ara-" + ad + "-cikti" })
        {
            var yol = Path.Combine(KarePanoKanit.Folder, klasor);
            if (Directory.Exists(yol)) Directory.Delete(yol, true);
        }
    }

    private static string Bildirim(string anahtar) => LanguageCatalog.Display(Strings.Get(anahtar));

    private static string Ara(string ad)
    {
        var ara = Path.Combine(KarePanoKanit.Folder, "ara-" + ad);
        Directory.CreateDirectory(ara);
        return ara;
    }

    private static PlayerView Ac(KareMotoru motor, SahtePano pano, string ad, out Window pencere)
    {
        Temizle(ad);
        var gecmis = Gecmis(ad);
        var view = new PlayerView
        {
            EngineFactory = () => motor,
            HistoryPath = () => gecmis,
            FrameCopier = pano.Al,
            FrameScratchFolder = Ara(ad)
        };
        pencere = new Window { Width = 640, Height = 360, Content = view };
        pencere.Show();
        var acilis = view.OpenAsync(KarePanoKanit.Sahte(ad + ".mp4"));
        DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
        acilis.GetAwaiter().GetResult();
        DenetimSurucu.Wait(view, 0.2);
        return view;
    }

    private static void Kapat(PlayerView view, Window pencere, string ad)
    {
        view.Close();
        pencere.Close();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        KarePanoKanit.Kapat(ad + ".mp4", "gecmis-" + ad, "ara-" + ad);
    }

    private static void Kopyala(PlayerView view)
    {
        GirdiSurucu.Key(view, Key.E, KeyModifiers.Control | KeyModifiers.Shift);
        DenetimSurucu.Pump(view, () => view.LastFrameCopy.IsCompleted, 15);
    }

    [Fact]
    public void TusKareyiSahtePanoyaVerirVeAraDosyayiSiler()
    {
        AppHost.Run(() =>
        {
            var motor = new KareMotoru();
            var pano = new SahtePano();
            var view = Ac(motor, pano, "kopya", out var pencere);

            Kopyala(view);

            Assert.Equal("copyframe -> ok", view.Trace[^1]);
            Assert.True(view.LastFrameCopy.Result);
            Assert.Equal(new[] { new PixelSize(KareMotoru.En, KareMotoru.Boy) }, pano.Gelen);
            var istenen = Assert.Single(motor.Istenen);
            Assert.Equal(Ara("kopya"), Path.GetDirectoryName(istenen));
            Assert.EndsWith(".png", istenen, StringComparison.Ordinal);
            Assert.False(File.Exists(istenen));
            Assert.Empty(Directory.GetFileSystemEntries(Ara("kopya")));
            Assert.Contains(Bildirim("player.view.screenshot-copied"), view.ViewStateText, StringComparison.Ordinal);
            Assert.DoesNotContain(Bildirim("player.view.screenshot-copy-failed"), view.ViewStateText, StringComparison.Ordinal);

            Kapat(view, pencere, "kopya");
            return "";
        });
    }

    [Fact]
    public void DosyaAcikDegilkenPanoyaHicbirSeyGitmez()
    {
        AppHost.Run(() =>
        {
            var pano = new SahtePano();
            var view = new PlayerView { FrameCopier = pano.Al, FrameScratchFolder = Ara("dosyasiz") };
            var once = view.Trace.Count;

            view.Apply(Keymap.CopyFrame.ToCommand());

            Assert.Equal(new[] { "copyframe -> no" }, view.Trace.Skip(once));
            Assert.Empty(pano.Gelen);
            Assert.False(view.LastFrameCopy.Result);
            Assert.Empty(Directory.GetFileSystemEntries(Ara("dosyasiz")));
            KarePanoKanit.Kapat("ara-dosyasiz");
            return "";
        });
    }

    [Fact]
    public void MotorKareVeremezsePanoCagrilmazVeHataSoylenir()
    {
        AppHost.Run(() =>
        {
            var motor = new KareMotoru(kaydeder: false);
            var pano = new SahtePano();
            var view = Ac(motor, pano, "karesiz", out var pencere);

            Kopyala(view);

            Assert.Single(motor.Istenen);
            Assert.Empty(pano.Gelen);
            Assert.False(view.LastFrameCopy.Result);
            Assert.Contains(Bildirim("player.view.screenshot-copy-failed"), view.ViewStateText, StringComparison.Ordinal);

            Kapat(view, pencere, "karesiz");
            return "";
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PanoReddederYaDaAtarsaHataSoylenir(bool atar)
    {
        AppHost.Run(() =>
        {
            var ad = atar ? "pano-atan" : "pano-reddeden";
            var motor = new KareMotoru();
            var pano = new SahtePano(kabul: false, hata: atar ? new TimeoutException("pano mesgul") : null);
            var view = Ac(motor, pano, ad, out var pencere);

            Kopyala(view);

            Assert.Single(pano.Gelen);
            Assert.True(view.LastFrameCopy.IsCompletedSuccessfully);
            Assert.False(view.LastFrameCopy.Result);
            Assert.Contains(Bildirim("player.view.screenshot-copy-failed"), view.ViewStateText, StringComparison.Ordinal);
            Assert.DoesNotContain(Bildirim("player.view.screenshot-copied"), view.ViewStateText, StringComparison.Ordinal);
            Assert.Empty(Directory.GetFileSystemEntries(Ara(ad)));

            Kapat(view, pencere, ad);
            return "";
        });
    }

    [Fact]
    public void MenuSatiriEkranGoruntusununHemenAltindaVeKomutuCalistirir()
    {
        AppHost.Run(() =>
        {
            var motor = new KareMotoru();
            var pano = new SahtePano();
            var view = Ac(motor, pano, "menu", out var pencere);

            var ust = view.BuildMenu().Items.ToList();
            var goruntu = ust.FindIndex(item => item is MenuItem satir && ReferenceEquals(satir.Tag, Keymap.Screenshot));
            Assert.True(goruntu >= 0);
            var kopya = Assert.IsType<MenuItem>(ust[goruntu + 1]);
            Assert.Same(Keymap.CopyFrame, kopya.Tag);
            Assert.Equal(Strings.Get("player.view.screenshot-copy"), kopya.Header);
            Assert.Equal(new KeyGesture(Key.E, KeyModifiers.Control | KeyModifiers.Shift), kopya.InputGesture);
            Assert.Contains("Ctrl+Shift+E", ToolTip.GetTip(kopya)?.ToString() ?? "", StringComparison.Ordinal);
            Assert.True(kopya.IsEnabled);

            kopya.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent) { Source = kopya });
            DenetimSurucu.Pump(view, () => view.LastFrameCopy.IsCompleted && pano.Gelen.Count > 0, 15);
            Assert.Single(pano.Gelen);
            Assert.StartsWith(Ara("menu"), Assert.Single(motor.Istenen), StringComparison.Ordinal);

            Kapat(view, pencere, "menu");
            return "";
        });
    }

    [Fact]
    public void TusBaskaKomutlaCakismazVeEskiEkranGoruntusuTusuYerinde()
    {
        var kopya = Assert.Single(Keymap.DefaultRows, row => ReferenceEquals(row.Action, Keymap.CopyFrame));
        Assert.Equal((Key.E, KeyModifiers.Control | KeyModifiers.Shift), (kopya.Input.Key, kopya.Input.Modifiers));
        Assert.Single(Keymap.DefaultRows, row => Keymap.SlotId(row) == Keymap.SlotId(kopya));
        Assert.Single(Keymap.ShortcutSlots(), slot => slot.Id == Keymap.SlotId(kopya));
        Assert.Same(Keymap.Screenshot, Assert.Single(Keymap.DefaultRows, row => row.Input.Key == Key.E && row.Input.Modifiers == KeyModifiers.Control).Action);
        Assert.Same(Keymap.Edit, Assert.Single(Keymap.DefaultRows, row => row.Input.Key == Key.E && row.Input.Modifiers == KeyModifiers.None).Action);
        Assert.Equal(PlayerCommandKind.CopyFrame, Keymap.ForKey(Key.E, KeyModifiers.Control | KeyModifiers.Shift, null).Kind);
        Assert.Equal(PlayerCommandKind.Screenshot, Keymap.ForKey(Key.E, KeyModifiers.Control, null).Kind);
        Assert.Contains(Keymap.CopyFrame, Keymap.MenuActions);
    }

    [Fact]
    public void BicimAyariGidipDonerEskiVeBozukAyarPngAcilir()
    {
        var klasor = Path.Combine(KarePanoKanit.Folder, "ayar");
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, PlayerSettings.FileName);

        Assert.Equal(ScreenshotFormat.Png, new PlayerSettings().ScreenshotFormat);
        Assert.Equal(ScreenshotFormat.Png, PlayerSettings.Load(Path.Combine(klasor, "yok.json")).ScreenshotFormat);

        new PlayerSettings { ScreenshotFormat = ScreenshotFormat.Jpg, ScreenshotFolder = klasor }.Save(dosya);
        var geri = PlayerSettings.Load(dosya);
        Assert.Equal(ScreenshotFormat.Jpg, geri.ScreenshotFormat);
        Assert.Equal(".jpg", Path.GetExtension(geri.ScreenshotPath(Path.Combine(klasor, "film.mkv"), 5)));
        Assert.Contains("\"screenshotFormat\": \"Jpg\"", File.ReadAllText(dosya), StringComparison.Ordinal);

        geri.ScreenshotFormat = ScreenshotFormat.Png;
        geri.Save(dosya);
        var png = PlayerSettings.Load(dosya);
        Assert.Equal(ScreenshotFormat.Png, png.ScreenshotFormat);
        Assert.Equal(".png", Path.GetExtension(png.ScreenshotPath(Path.Combine(klasor, "film.mkv"), 5)));

        File.WriteAllText(dosya, "{ \"screenshotFolder\": null, \"screenshotPattern\": \"{name}_{time}\", \"repeat\": \"All\", \"shuffle\": true }");
        var eski = PlayerSettings.Load(dosya);
        Assert.Equal(ScreenshotFormat.Png, eski.ScreenshotFormat);
        Assert.Equal(RepeatMode.All, eski.Repeat);
        Assert.True(eski.Shuffle);

        foreach (var bozuk in new[] { "\"Webp\"", "\"7\"", "\"\"", "null" })
        {
            File.WriteAllText(dosya, "{ \"screenshotFormat\": " + bozuk + ", \"repeat\": \"One\" }");
            var okunan = PlayerSettings.Load(dosya);
            Assert.Equal(ScreenshotFormat.Png, okunan.ScreenshotFormat);
            Assert.Equal(RepeatMode.One, okunan.Repeat);
        }

        KarePanoKanit.Kapat("ayar");
    }

    [Fact]
    public void BicimSecimiMenudeVePaneldeAyniAyariCevirirVeDiskeYazar()
    {
        AppHost.Run(() =>
        {
            var motor = new KareMotoru();
            var view = Ac(motor, new SahtePano(), "secim", out var pencere);
            var dosya = Path.Combine(Path.GetDirectoryName(Gecmis("secim"))!, PlayerSettings.FileName);
            var baslik = Strings.Get("player.view.screenshot-jpg");

            MenuItem MenuSatiri()
            {
                var ayarlar = view.SettingsItems();
                var klasor = ayarlar.FindIndex(item => item is MenuItem satir && (string?)satir.Header == Strings.Get("player.view.screenshot-folder"));
                Assert.True(klasor >= 0);
                var satir = Assert.IsType<MenuItem>(ayarlar[klasor + 1]);
                Assert.Equal(baslik, satir.Header);
                Assert.Equal(MenuItemToggleType.CheckBox, satir.ToggleType);
                return satir;
            }

            var panel = new PlayerAdvancedPanel { Player = view };
            CheckBox Kutu() => Assert.Single(panel.FindControl<StackPanel>("Switches")!.Children.OfType<CheckBox>(), kutu => (string?)kutu.Content == baslik);

            Assert.False(MenuSatiri().IsChecked);
            Assert.False(Kutu().IsChecked);
            Assert.Equal(ScreenshotFormat.Png, view.Settings.ScreenshotFormat);

            var menu = MenuSatiri();
            menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent) { Source = menu });
            Assert.Equal(ScreenshotFormat.Jpg, view.Settings.ScreenshotFormat);
            Assert.Equal(ScreenshotFormat.Jpg, PlayerSettings.Load(dosya).ScreenshotFormat);
            Assert.True(MenuSatiri().IsChecked);
            Assert.True(Kutu().IsChecked);

            view.Settings.ScreenshotFolder = Ara("secim");
            GirdiSurucu.Key(view, Key.E, KeyModifiers.Control);
            DenetimSurucu.Pump(view, () => view.LastScreenshot.IsCompleted, 15);
            Assert.EndsWith(".jpg", Assert.Single(motor.Istenen), StringComparison.Ordinal);

            var kutu = Kutu();
            kutu.RaiseEvent(new RoutedEventArgs(Button.ClickEvent) { Source = kutu });
            Assert.Equal(ScreenshotFormat.Png, view.Settings.ScreenshotFormat);
            Assert.Equal(ScreenshotFormat.Png, PlayerSettings.Load(dosya).ScreenshotFormat);
            Assert.False(MenuSatiri().IsChecked);
            Assert.False(Kutu().IsChecked);

            GirdiSurucu.Key(view, Key.E, KeyModifiers.Control);
            DenetimSurucu.Pump(view, () => view.LastScreenshot.IsCompleted, 15);
            Assert.EndsWith(".png", motor.Istenen[^1], StringComparison.Ordinal);

            panel.Player = null;
            Kapat(view, pencere, "secim");
            return "";
        });
    }

    [Fact]
    public void DortAnahtarButunDillerde()
    {
        var anahtarlar = new[]
        {
            "player.view.screenshot-copy", "player.view.screenshot-copied",
            "player.view.screenshot-copy-failed", "player.view.screenshot-jpg"
        };
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var dil in Locales.Languages)
        {
            var degerler = Locales.Domain(dil, "playback");
            var metinler = anahtarlar.Select(anahtar =>
            {
                Assert.True(degerler.TryGetValue(anahtar, out var metin) && !string.IsNullOrWhiteSpace(metin), dil + " " + anahtar);
                return metin!;
            }).ToList();
            Assert.Equal(4, metinler.Distinct(StringComparer.Ordinal).Count());
            Assert.DoesNotContain(degerler["player.view.screenshot"], metinler);
            Assert.DoesNotContain(degerler["player.view.screenshot-failed"], metinler);
            Assert.Contains("JPG", metinler[3], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GercekMotordaPanoKaresiKaynakBoyundaVeDosyaBicimiBasBaytlardan()
    {
        var klip = KarePanoKanit.Klip;
        Temizle("gercek");
        var gecmis = Gecmis("gercek");
        var cikti = Ara("gercek-cikti");
        var ara = Ara("gercek");

        var (boyut, png, jpg, sayilar) = AppHost.Run(() =>
        {
            var pano = new SahtePano();
            var view = DenetimSurucu.Ac(klip, out var pencere, gecmis);
            view.FrameCopier = pano.Al;
            view.FrameScratchFolder = ara;
            view.Settings.ScreenshotFolder = cikti;
            DenetimSurucu.Duraklat(view);
            DenetimSurucu.Git(view, 1);

            GirdiSurucu.Key(view, Key.E, KeyModifiers.Control | KeyModifiers.Shift);
            DenetimSurucu.Pump(view, () => view.LastFrameCopy.IsCompleted, 15);
            var kopyalandi = view.LastFrameCopy.IsCompleted && view.LastFrameCopy.Result;
            var araKalan = Directory.GetFileSystemEntries(ara).Length;

            string? Cek()
            {
                GirdiSurucu.Key(view, Key.E, KeyModifiers.Control);
                DenetimSurucu.Pump(view, () => view.LastScreenshot.IsCompleted, 15);
                return view.LastScreenshot.IsCompleted ? view.LastScreenshot.Result : null;
            }

            var pngYol = Cek();
            view.ToggleScreenshotJpg();
            var jpgYol = Cek();
            var gelen = pano.Gelen.ToList();
            view.Close();
            pencere.Close();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            return (gelen, pngYol, jpgYol, (kopyalandi, araKalan));
        });

        Assert.True(sayilar.kopyalandi);
        Assert.Equal(0, sayilar.araKalan);
        Assert.Equal(new[] { new PixelSize(160, 90) }, boyut);

        Assert.NotNull(png);
        Assert.NotNull(jpg);
        Assert.Equal(".png", Path.GetExtension(png));
        Assert.Equal(".jpg", Path.GetExtension(jpg));
        Assert.Equal(Path.GetFileNameWithoutExtension(png), Path.GetFileNameWithoutExtension(jpg));
        Assert.Equal(new byte[] { 0x89, 0x50 }, File.ReadAllBytes(png!).Take(2));
        Assert.Equal(new byte[] { 0xFF, 0xD8 }, File.ReadAllBytes(jpg!).Take(2));
        Assert.Equal((160, 90), GorunumKanit.Boyut(png!));
        Assert.Equal((160, 90), GorunumKanit.Boyut(jpg!));
        Assert.Equal(2, Directory.GetFiles(cikti).Length);

        KarePanoKanit.Kapat("kare-160x90.mkv", "gecmis-gercek", "ara-gercek", "ara-gercek-cikti");
    }
}
