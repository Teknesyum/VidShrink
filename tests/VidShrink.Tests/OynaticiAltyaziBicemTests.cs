using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal static class BicemKanit
{
    internal const string Konusma = "Hello there";

    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "oya-p10");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static void Write(string name, string body)
        => File.WriteAllText(Path.Combine(Folder, name), body, new UTF8Encoding(false));

    internal static void Kapat(params string[] adlar) => KanitKapanisi.Kapat(Folder, adlar);

    internal static string Klip
    {
        get
        {
            const string ad = "gri-320x180-4sn.mp4";
            var path = Path.Combine(Folder, ad);
            if (File.Exists(path) && new FileInfo(path).Length > 0) return path;
            Assert.True(ToolLocator.IsAvailable(out var missing), $"klip uretimi icin {missing} gerekli");
            var partial = Path.Combine(Folder, "part-" + ad);
            var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg,
                "-y", "-hide_banner", "-f", "lavfi", "-i", "color=c=0x404040:size=320x180:rate=10:duration=4",
                "-threads", "2", "-c:v", "libx264", "-preset", "ultrafast", "-g", "5", "-pix_fmt", "yuv420p", partial);
            Assert.True(kod == 0, $"ffmpeg {ad} uretemedi (kod {kod}): {hata[Math.Max(0, hata.Length - 600)..]}");
            File.Move(partial, path, true);
            return path;
        }
    }

    /// <summary>Kirmizi, kalin Arial biçemli ASS; ortada tek satir, kenar ve golge sifir.</summary>
    internal static string Ass
    {
        get
        {
            var path = Path.Combine(Folder, "kirmizi.ass");
            File.WriteAllText(path, string.Join("\r\n",
                "[Script Info]",
                "ScriptType: v4.00+",
                "PlayResX: 320",
                "PlayResY: 180",
                "",
                "[V4+ Styles]",
                "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding",
                "Style: Default,Arial,48,&H000000FF,&H000000FF,&H00000000,&H00000000,-1,0,0,0,100,100,0,0,1,0,0,5,10,10,10,1",
                "",
                "[Events]",
                "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text",
                "Dialogue: 0,0:00:00.00,0:00:04.00,Default,,0,0,0,,HHHH",
                ""), new UTF8Encoding(false));
            return path;
        }
    }

    internal static string SdhSrt
    {
        get
        {
            var path = Path.Combine(Folder, "sdh.srt");
            File.WriteAllText(path, "1\r\n00:00:00,000 --> 00:00:04,000\r\n[door slams]\r\n" + Konusma + "\r\n\r\n", new UTF8Encoding(false));
            return path;
        }
    }

    internal static async Task<MpvEngine> AcAsync(string altyazi)
    {
        var engine = new MpvEngine();
        engine.SetProperty("ao", "null");
        await engine.OpenAsync(Klip);
        engine.Pause();
        Assert.True(engine.AddSubtitle(altyazi), "altyazi eklenemedi: " + altyazi);
        await engine.SeekAsync(1.0, SeekPrecision.Exact);
        return engine;
    }

    internal static async Task<byte[]> KareAsync(MpvEngine engine, string ad, bool altyazili)
    {
        var png = Path.Combine(Folder, ad + ".png");
        Assert.True(await engine.SaveScreenshotAsync(png, altyazili), "kare kaydedilemedi: " + ad);
        var ham = Path.Combine(Folder, ad + ".rgb");
        var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg,
            "-y", "-hide_banner", "-threads", "2", "-i", png, "-f", "rawvideo", "-pix_fmt", "rgb24", ham);
        Assert.True(kod == 0, $"ffmpeg kareyi cozemedi (kod {kod}): {hata[Math.Max(0, hata.Length - 400)..]}");
        var piksel = File.ReadAllBytes(ham);
        File.Delete(ham);
        File.Delete(png);
        return piksel;
    }

    internal static int Kirmizi(byte[] rgb) => Say(rgb, (r, g, b) => r > 180 && g < 90 && b < 90);

    internal static int Sari(byte[] rgb) => Say(rgb, (r, g, b) => r > 180 && g > 180 && b < 90);

    internal static int Fark(byte[] a, byte[] b)
    {
        Assert.Equal(a.Length, b.Length);
        var fark = 0;
        for (var i = 0; i + 2 < a.Length; i += 3)
            if (Math.Abs(a[i] - b[i]) + Math.Abs(a[i + 1] - b[i + 1]) + Math.Abs(a[i + 2] - b[i + 2]) > 60) fark++;
        return fark;
    }

    private static int Say(byte[] rgb, Func<int, int, int, bool> uyar)
    {
        var sayi = 0;
        for (var i = 0; i + 2 < rgb.Length; i += 3)
            if (uyar(rgb[i], rgb[i + 1], rgb[i + 2])) sayi++;
        return sayi;
    }
}

internal sealed class BicemKareMotoru : IPlaybackEngine
{
    internal List<(string Yol, bool Altyazili)> Istenen { get; } = new();

    public string Name => "bicem-kare-motoru";

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

    public Task<bool> SaveScreenshotAsync(string path, CancellationToken ct = default) => SaveScreenshotAsync(path, false, ct);

    public Task<bool> SaveScreenshotAsync(string path, bool withSubtitles, CancellationToken ct = default)
    {
        Istenen.Add((path, withSubtitles));
        using var kare = new WriteableBitmap(new PixelSize(8, 8), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        kare.Save(path, PngBitmapEncoderOptions.Default);
        return Task.FromResult(true);
    }

    public bool TryCopyLatest(ref long seen, FrameCopy copy) => false;

    public void Dispose() => IsOpen = false;
}

/// <summary>
/// OYA-P10: biçemli (ASS) altyaziyi gecersiz kilma, isitme engelli notlarini ayiklama, yazi tipi
/// secimi ve altyazili kare kaydi. Gercek libmpv kolu 4 sn'lik 320x180 gri klipte; piksel
/// sayimi kaydedilen PNG'nin ffmpeg ile cozulmus halinden. Kanit <c>.calisma/oya-p10</c>.
/// </summary>
public sealed class OynaticiAltyaziBicemTests
{
    /// <summary>
    /// <para>2026-10-15 olcum: <c>sub-ass-override</c> varsayilaninda (<c>scale</c>) sari renk ayari
    /// ASS'nin kirmizisini degistirmedi; anahtar <c>force</c> yazinca harfler sariya dondu.</para>
    /// </summary>
    [Fact]
    public async Task AssBicemiYalnizGecersizKilmaAcikkenKullaniciAyariniAlir()
    {
        using var engine = await BicemKanit.AcAsync(BicemKanit.Ass);
        var body = new StringBuilder();

        var varsayilan = engine.SubtitleLook;
        body.AppendLine("varsayilan: " + varsayilan + " | sub-ass-override " + engine.GetProperty("sub-ass-override"));
        Assert.False(varsayilan.OverrideAss);

        var ilk = await BicemKanit.KareAsync(engine, "ass-ilk", true);
        body.AppendLine($"ilk: kirmizi {BicemKanit.Kirmizi(ilk)}, sari {BicemKanit.Sari(ilk)}");
        Assert.True(BicemKanit.Kirmizi(ilk) > 200, "ASS'nin kirmizi harfleri karede yok");

        engine.SetSubtitleStyle(new SubtitleStyle(Color: "#FFFFFF00"));
        await Task.Delay(200);
        var gecersizKilmasiz = await BicemKanit.KareAsync(engine, "ass-renk", true);
        body.AppendLine($"yalniz renk: kirmizi {BicemKanit.Kirmizi(gecersizKilmasiz)}, sari {BicemKanit.Sari(gecersizKilmasiz)}");
        Assert.True(BicemKanit.Kirmizi(gecersizKilmasiz) > 200, "olcum degisti: renk ayari ASS'ye gecersiz kilmadan uygulandi");
        Assert.True(BicemKanit.Sari(gecersizKilmasiz) < 20);

        engine.SetProperty("sub-scale", "4");
        engine.SetSubtitleStyle(new SubtitleStyle(Color: "#FFFFFF00", Outline: 0, OverrideAss: true));
        await Task.Delay(200);
        var yazilan = engine.SubtitleLook;
        var gecersizKilinan = await BicemKanit.KareAsync(engine, "ass-gecersiz", true);
        body.AppendLine("yazilan: " + yazilan + " | sub-ass-override " + engine.GetProperty("sub-ass-override"));
        body.AppendLine($"gecersiz kilma: kirmizi {BicemKanit.Kirmizi(gecersizKilinan)}, sari {BicemKanit.Sari(gecersizKilinan)}");
        Assert.True(yazilan.OverrideAss);
        Assert.Equal("force", engine.GetProperty("sub-ass-override"));
        Assert.True(BicemKanit.Sari(gecersizKilinan) > 200, "gecersiz kilma acikken harfler sariya donmedi");
        Assert.True(BicemKanit.Kirmizi(gecersizKilinan) < 20);

        engine.SetSubtitleStyle(SubtitleStyle.Inherited);
        await Task.Delay(200);
        var sifirlanan = engine.SubtitleLook;
        body.AppendLine("sifirlanan: " + sifirlanan + " | sub-ass-override " + engine.GetProperty("sub-ass-override"));
        Assert.Equal(varsayilan, sifirlanan);

        BicemKanit.Write("ass-gecersiz-kilma.txt", body.ToString());
        BicemKanit.Kapat("ass-gecersiz-kilma.txt", "kirmizi.ass");
    }

    [Fact]
    public async Task AltyaziliKareAltyazisizKaredenFarkliAltyazisizKareVideoyaEsit()
    {
        using var engine = await BicemKanit.AcAsync(BicemKanit.Ass);
        var body = new StringBuilder();

        var altyazisiz = await BicemKanit.KareAsync(engine, "kare-altyazisiz", false);
        var altyazili = await BicemKanit.KareAsync(engine, "kare-altyazili", true);
        var fark = BicemKanit.Fark(altyazisiz, altyazili);
        body.AppendLine($"altyazisiz: kirmizi {BicemKanit.Kirmizi(altyazisiz)}");
        body.AppendLine($"altyazili: kirmizi {BicemKanit.Kirmizi(altyazili)}");
        body.AppendLine($"farkli piksel: {fark} / {altyazili.Length / 3}");

        Assert.Equal(0, BicemKanit.Kirmizi(altyazisiz));
        Assert.True(BicemKanit.Kirmizi(altyazili) > 200, "altyazili karede altyazi yok");
        Assert.True(fark > 200, $"iki kare arasinda fark yok: {fark}");

        BicemKanit.Write("kare-altyazi-farki.txt", body.ToString());
        BicemKanit.Kapat("kare-altyazi-farki.txt", "kirmizi.ass");
    }

    [Fact]
    public async Task IsitmeEngelliNotlariGizlenirVeGeriGelir()
    {
        using var engine = await BicemKanit.AcAsync(BicemKanit.SdhSrt);
        var body = new StringBuilder();

        var ham = await ParcaKanit.AltyaziBekle(engine, "[door slams]\n" + BicemKanit.Konusma);
        body.AppendLine($"ham: '{ham}' | sub-filter-sdh {engine.GetProperty("sub-filter-sdh")}");
        Assert.Contains("[door slams]", ham ?? "", StringComparison.Ordinal);

        engine.SetSubtitleStyle(new SubtitleStyle(HideSdh: true));
        var ayiklanan = await ParcaKanit.AltyaziBekle(engine, BicemKanit.Konusma);
        body.AppendLine($"ayiklanan: '{ayiklanan}' | sub-filter-sdh {engine.GetProperty("sub-filter-sdh")} | look {engine.SubtitleLook}");
        Assert.True(engine.SubtitleLook.HideSdh);
        Assert.Equal(BicemKanit.Konusma, ayiklanan);

        engine.SetSubtitleStyle(SubtitleStyle.Inherited);
        var geri = await ParcaKanit.AltyaziBekle(engine, "[door slams]\n" + BicemKanit.Konusma);
        body.AppendLine($"geri: '{geri}' | sub-filter-sdh {engine.GetProperty("sub-filter-sdh")}");
        Assert.False(engine.SubtitleLook.HideSdh);
        Assert.Contains("[door slams]", geri ?? "", StringComparison.Ordinal);

        BicemKanit.Write("sdh.txt", body.ToString());
        BicemKanit.Kapat("sdh.txt", "sdh.srt");
    }

    [Fact]
    public async Task SistemYaziTipiKalinVeEgikMotordanGeriOkunurVeKareyiDegistirir()
    {
        using var engine = await BicemKanit.AcAsync(BicemKanit.Ass);
        var body = new StringBuilder();

        engine.SetProperty("sub-scale", "4");
        engine.SetSubtitleStyle(new SubtitleStyle(Font: "Arial", OverrideAss: true));
        await Task.Delay(200);
        var arial = await BicemKanit.KareAsync(engine, "yazi-arial", true);

        engine.SetSubtitleStyle(new SubtitleStyle(Font: "Courier New", OverrideAss: true, Bold: true, Italic: true));
        await Task.Delay(200);
        var look = engine.SubtitleLook;
        var courier = await BicemKanit.KareAsync(engine, "yazi-courier", true);
        var fark = BicemKanit.Fark(arial, courier);
        body.AppendLine("look: " + look);
        body.AppendLine($"Arial -> Courier New kalin egik farkli piksel: {fark}");
        Assert.Equal("Courier New", look.Font);
        Assert.True(look.Bold);
        Assert.True(look.Italic);
        Assert.Equal("yes", engine.GetProperty("sub-bold"));
        Assert.Equal("yes", engine.GetProperty("sub-italic"));
        Assert.True(fark > 200, $"yazi tipi kareyi degistirmedi: {fark}");

        engine.SetSubtitleStyle(SubtitleStyle.Inherited);
        await Task.Delay(100);
        Assert.False(engine.SubtitleLook.Bold);
        Assert.False(engine.SubtitleLook.Italic);

        BicemKanit.Write("yazi-tipi.txt", body.ToString());
        BicemKanit.Kapat("yazi-tipi.txt", "kirmizi.ass");
    }

    [Fact]
    public void AltyaziliKareAyariKalicidirVeMotoraGecer()
    {
        AppHost.Run(() =>
        {
            var klasor = Path.Combine(BicemKanit.Folder, "gecmis-kare");
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
            Directory.CreateDirectory(klasor);
            var gecmis = Path.Combine(klasor, "gecmis.json");
            var medya = Path.Combine(klasor, "film.mp4");
            File.WriteAllBytes(medya, new byte[16]);
            var motor = new BicemKareMotoru();
            var view = new PlayerView { EngineFactory = () => motor, HistoryPath = () => gecmis };
            var pencere = new Window { Width = 640, Height = 360, Content = view };
            pencere.Show();
            var acilis = view.OpenAsync(medya);
            DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
            view.Settings.ScreenshotFolder = klasor;

            Assert.False(view.Settings.ScreenshotSubtitles);
            view.Apply(Keymap.Screenshot.ToCommand());
            DenetimSurucu.Pump(view, () => view.LastScreenshot.IsCompleted, 5);

            view.ToggleScreenshotSubtitles();
            Assert.True(view.Settings.ScreenshotSubtitles);
            Assert.Equal("screenshotsubtitles -> True", view.Trace[^1]);
            view.Apply(Keymap.Screenshot.ToCommand());
            DenetimSurucu.Pump(view, () => view.LastScreenshot.IsCompleted, 5);

            Assert.Equal(new[] { false, true }, motor.Istenen.Select(i => i.Altyazili).ToArray());
            Assert.True(PlayerSettings.Load(Path.Combine(klasor, PlayerSettings.FileName)).ScreenshotSubtitles);

            view.Close();
            pencere.Close();
            Dispatcher.UIThread.RunJobs();
            Directory.Delete(klasor, true);
            BicemKanit.Kapat();
            return "";
        });
    }

    [Fact]
    public void AltyaziBicemMenusuYeniAnahtarlariVeSistemYaziTiplerininGosterir()
    {
        AppHost.Run(() =>
        {
            var klasor = Path.Combine(BicemKanit.Folder, "gecmis-menu");
            if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
            Directory.CreateDirectory(klasor);
            Strings.Use("en");
            var view = new PlayerView { HistoryPath = () => Path.Combine(klasor, "gecmis.json") };

            var yaziTipleri = PlayerView.SubtitleFontChoices;
            Assert.Equal(PlayerView.SubtitleFonts, yaziTipleri.Take(3));
            Assert.Equal(yaziTipleri.Count, yaziTipleri.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            if (OperatingSystem.IsWindows()) Assert.Contains("Arial", yaziTipleri);
            Assert.True(yaziTipleri.Count > 3, "sistem yazi tipleri listeye girmedi");

            var ogeler = view.SubtitleStyleItems().OfType<MenuItem>().ToList();
            var yazi = ogeler.Single(o => (string?)o.Header == Strings.Get("player.advanced.font"));
            Assert.Equal(yaziTipleri.Count, yazi.Items.OfType<MenuItem>().Count());

            foreach (var (anahtar, oku) in new (string, Func<SubtitleStyle, bool>)[]
            {
                ("player.advanced.ass-override", s => s.OverrideAss),
                ("player.advanced.hide-sdh", s => s.HideSdh),
                ("player.advanced.bold", s => s.Bold),
                ("player.advanced.italic", s => s.Italic)
            })
            {
                var oge = ogeler.Single(o => (string?)o.Header == Strings.Get(anahtar));
                Assert.False(oge.IsChecked);
                oge.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                Assert.True(oku(view.Advanced.Style), anahtar + " ayari degistirmedi");
            }

            var okunan = PlayerAdvanced.Load(Path.Combine(klasor, PlayerAdvanced.FileName));
            Assert.True(okunan.Style.OverrideAss && okunan.Style.HideSdh && okunan.Style.Bold && okunan.Style.Italic);
            Assert.Contains("subtitle", okunan.State());

            view.ResetSubtitleStyle();
            Assert.True(view.Advanced.Style.IsInherited);
            Directory.Delete(klasor, true);
            BicemKanit.Kapat();
            return "";
        });
    }
}
