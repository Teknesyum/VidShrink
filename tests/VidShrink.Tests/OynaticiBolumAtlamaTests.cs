using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

internal static class BolumKanit
{
    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "oynatici-bolum");
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

    private static readonly string[] Kaynak =
    {
        "-f", "lavfi", "-i", "testsrc2=size=160x90:rate=10:duration=4.5"
    };

    private static readonly string[] Kodlama =
    {
        "-threads", "2", "-c:v", "libx264", "-preset", "ultrafast", "-g", "5", "-pix_fmt", "yuv420p"
    };

    internal static string Bolumsuz => Uret("bolumsuz-160x90.mkv", Kaynak.Concat(Kodlama).ToArray());

    internal static string UcBolum
    {
        get
        {
            var meta = Path.Combine(Folder, "bolumler.txt");
            File.WriteAllText(meta, string.Join("\n",
                ";FFMETADATA1",
                "[CHAPTER]", "TIMEBASE=1/1000", "START=0", "END=1500", "title=a",
                "[CHAPTER]", "TIMEBASE=1/1000", "START=1500", "END=3000", "title=b",
                "[CHAPTER]", "TIMEBASE=1/1000", "START=3000", "END=4500", "title=c") + "\n", new UTF8Encoding(false));
            return Uret("uc-bolum-160x90.mkv", Kaynak
                .Concat(new[] { "-i", meta, "-map", "0:v", "-map_metadata", "1", "-map_chapters", "1" })
                .Concat(Kodlama).ToArray());
        }
    }

    private static string Uret(string ad, string[] args)
    {
        var path = Path.Combine(Folder, ad);
        if (File.Exists(path) && new FileInfo(path).Length > 0) return path;
        Assert.True(ToolLocator.IsAvailable(out var missing), $"klip uretimi icin {missing} gerekli");
        var partial = Path.Combine(Folder, "part-" + ad);
        var (kod, _, hata) = GorunumKanit.Kos(ToolLocator.Ffmpeg, new[] { "-y", "-hide_banner" }.Concat(args).Append(partial).ToArray());
        Assert.True(kod == 0, $"ffmpeg {ad} uretemedi (kod {kod}): {hata[Math.Max(0, hata.Length - 600)..]}");
        File.Move(partial, path, true);
        return path;
    }
}

internal sealed class BolumMotoru : IPlaybackEngine
{
    private readonly byte[] _piksel = Enumerable.Repeat((byte)128, 4 * 64 * 36).ToArray();
    private readonly bool _kabul;

    internal BolumMotoru(IReadOnlyList<double> bolumler, bool kabul = true)
    {
        ChapterTimes = bolumler;
        _kabul = kabul;
    }

    internal List<bool> Adimlar { get; } = new();

    public string Name => "bolum-motoru";

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

    public IReadOnlyList<double> ChapterTimes { get; }

    public bool StepChapter(bool backward)
    {
        Adimlar.Add(backward);
        return _kabul;
    }

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

    public bool TryCopyLatest(ref long seen, FrameCopy copy)
    {
        if (seen == FramesRendered) return false;
        seen = FramesRendered;
        var handle = GCHandle.Alloc(_piksel, GCHandleType.Pinned);
        try
        {
            copy(handle.AddrOfPinnedObject(), 64, 36, 64 * 4);
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
/// Sonraki/onceki bolume atlama: Ctrl+PgDn ve Ctrl+PgUp. Sahte motorla komutun motora gittigi,
/// bolumsuz dosyada gitmedigi ve menu satirinin etkinligi; gercek libmpv ile uc bolumlu klipte
/// atlamadan sonraki konum motordan okunur.
/// </summary>
public sealed class OynaticiBolumAtlamaTests
{
    private static readonly double[] Uc = { 0, 200, 400 };

    private static PlayerView Ac(BolumMotoru motor, string ad, out Window pencere)
    {
        var gecmis = Path.Combine(BolumKanit.Folder, "gecmis-" + ad, "gecmis.json");
        Directory.CreateDirectory(Path.GetDirectoryName(gecmis)!);
        var view = new PlayerView { EngineFactory = () => motor, HistoryPath = () => gecmis };
        pencere = new Window { Width = 640, Height = 360, Content = view };
        pencere.Show();
        var acilis = view.OpenAsync(BolumKanit.Sahte(ad + ".mp4"));
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
        BolumKanit.Kapat(ad + ".mp4", "gecmis-" + ad);
    }

    private static void Ileri(PlayerView view) => GirdiSurucu.Key(view, Key.PageDown, KeyModifiers.Control);

    private static void Geri(PlayerView view) => GirdiSurucu.Key(view, Key.PageUp, KeyModifiers.Control);

    [Fact]
    public void BolumluDosyadaTusKomutuMotoraGider()
    {
        AppHost.Run(() =>
        {
            var motor = new BolumMotoru(Uc);
            var view = Ac(motor, "bolumlu", out var pencere);
            Assert.Equal(Uc, view.Chapters);

            Ileri(view);
            Assert.Equal(new[] { false }, motor.Adimlar);
            Assert.Equal("chapter 1 -> ok", view.Trace[^1]);

            Geri(view);
            Assert.Equal(new[] { false, true }, motor.Adimlar);
            Assert.Equal("chapter -1 -> ok", view.Trace[^1]);

            Kapat(view, pencere, "bolumlu");
            return "";
        });
    }

    [Fact]
    public void BolumsuzDosyadaKomutMotoraGitmez()
    {
        AppHost.Run(() =>
        {
            var motor = new BolumMotoru(Array.Empty<double>());
            var view = Ac(motor, "bolumsuz", out var pencere);
            var konum = view.PositionSeconds;

            Ileri(view);
            Assert.Equal("chapter 1 -> no", view.Trace[^1]);
            Geri(view);
            Assert.Equal("chapter -1 -> no", view.Trace[^1]);

            Assert.Empty(motor.Adimlar);
            Assert.Equal(konum, view.PositionSeconds);

            Kapat(view, pencere, "bolumsuz");
            return "";
        });
    }

    [Fact]
    public void MotorReddederseIzBasariYazmaz()
    {
        AppHost.Run(() =>
        {
            var motor = new BolumMotoru(Uc, kabul: false);
            var view = Ac(motor, "reddeden", out var pencere);

            Ileri(view);
            Assert.Single(motor.Adimlar);
            Assert.Equal("chapter 1 -> no", view.Trace[^1]);

            Kapat(view, pencere, "reddeden");
            return "";
        });
    }

    [Fact]
    public void DosyasizGorunumdeKomutTekIzSatiriBirakir()
    {
        AppHost.Run(() =>
        {
            var view = new PlayerView();
            var once = view.Trace.Count;
            view.Apply(Keymap.NextChapter.ToCommand());
            Assert.Equal(new[] { "chapter 1 -> no" }, view.Trace.Skip(once));
            return "";
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MenuSatirlariYalnizBolumluDosyadaEtkin(bool bolumlu)
    {
        AppHost.Run(() =>
        {
            var ad = bolumlu ? "menu-bolumlu" : "menu-bolumsuz";
            var motor = new BolumMotoru(bolumlu ? Uc : Array.Empty<double>());
            var view = Ac(motor, ad, out var pencere);

            var satirlar = OynaticiGirdiTestsMenuSatirlari.EylemSatirlari(view.BuildMenu()).ToList();
            var ileri = Assert.Single(satirlar, item => ReferenceEquals(item.Tag, Keymap.NextChapter));
            var geri = Assert.Single(satirlar, item => ReferenceEquals(item.Tag, Keymap.PreviousChapter));
            var oynat = Assert.Single(satirlar, item => ReferenceEquals(item.Tag, Keymap.NextFrame));

            Assert.Equal(bolumlu, ileri.IsEnabled);
            Assert.Equal(bolumlu, geri.IsEnabled);
            Assert.True(oynat.IsEnabled);
            Assert.Equal(Strings.Get("main.player.menu.chapternext"), ileri.Header);
            Assert.Equal(Strings.Get("main.player.menu.chapterprev"), geri.Header);
            Assert.Equal(new KeyGesture(Key.PageDown, KeyModifiers.Control), ileri.InputGesture);
            Assert.Equal(new KeyGesture(Key.PageUp, KeyModifiers.Control), geri.InputGesture);
            Assert.Contains("PgDn", ToolTip.GetTip(ileri)?.ToString() ?? "", StringComparison.Ordinal);

            ileri.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent) { Source = ileri });
            Assert.Equal(bolumlu ? new[] { false } : Array.Empty<bool>(), motor.Adimlar);

            Kapat(view, pencere, ad);
            return "";
        });
    }

    [Fact]
    public void TuslarBaskaKomutlaCakismazVeKisayolListesindeDurur()
    {
        var ileri = Keymap.DefaultRows.Where(row => ReferenceEquals(row.Action, Keymap.NextChapter)).ToList();
        var geri = Keymap.DefaultRows.Where(row => ReferenceEquals(row.Action, Keymap.PreviousChapter)).ToList();
        Assert.Equal((Key.PageDown, KeyModifiers.Control), (Assert.Single(ileri).Input.Key, ileri[0].Input.Modifiers));
        Assert.Equal((Key.PageUp, KeyModifiers.Control), (Assert.Single(geri).Input.Key, geri[0].Input.Modifiers));

        foreach (var satir in ileri.Concat(geri))
        {
            Assert.Single(Keymap.DefaultRows, row => Keymap.SlotId(row) == Keymap.SlotId(satir));
            Assert.Single(Keymap.ShortcutSlots(), slot => slot.Id == Keymap.SlotId(satir));
        }

        Assert.Same(Keymap.NextFile, Assert.Single(Keymap.DefaultRows, row => row.Input.Key == Key.PageDown && row.Input.Modifiers == KeyModifiers.None).Action);
        Assert.Same(Keymap.BookmarkNext, Assert.Single(Keymap.DefaultRows, row => row.Input.Key == Key.PageDown && row.Input.Modifiers == KeyModifiers.Shift).Action);
        Assert.Contains(Keymap.NextChapter, Keymap.MenuActions);
        Assert.Contains(Keymap.PreviousChapter, Keymap.MenuActions);
    }

    [Fact]
    public void IkiAnahtarButunDillerde()
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var dil in Locales.Languages)
        {
            var degerler = Locales.Domain(dil, "main");
            Assert.True(degerler.TryGetValue("main.player.menu.chapternext", out var ileri) && !string.IsNullOrWhiteSpace(ileri), dil);
            Assert.True(degerler.TryGetValue("main.player.menu.chapterprev", out var geri) && !string.IsNullOrWhiteSpace(geri), dil);
            Assert.NotEqual(ileri, geri);
            Assert.NotEqual(degerler["main.player.menu.bookmarknext"], ileri);
            Assert.NotEqual(degerler["main.player.menu.bookmarkprev"], geri);
        }
    }

    [Fact]
    public void GercekMotordaAtlamaBolumBasinaIner()
    {
        var rapor = AppHost.Run(() =>
        {
            var o = KisayolOrtam.Ac(BolumKanit.UcBolum, "bolum-atlama");
            Assert.Equal(3, o.View.Chapters.Count);
            o.Git(0.2);

            void Bekle(double hedef, string bolum)
            {
                o.Bekle(() => Math.Abs(o.Sayi("time-pos") - hedef) < 0.25 && o.Oku("chapter") == bolum
                    && Math.Abs(o.View.PositionSeconds - hedef) < 0.25, 5);
                o.Not($"hedef {hedef} -> time-pos {o.Sayi("time-pos"):0.###} chapter {o.Oku("chapter")} gorunum {o.View.PositionSeconds:0.###}");
                Assert.InRange(o.Sayi("time-pos"), hedef - 0.25, hedef + 0.25);
                Assert.Equal(bolum, o.Oku("chapter"));
                Assert.InRange(o.View.PositionSeconds, hedef - 0.25, hedef + 0.25);
            }

            Assert.Equal("0", o.Oku("chapter"));

            o.Bas(Key.PageDown, KeyModifiers.Control);
            Assert.Equal("chapter 1 -> ok", o.View.Trace[^1]);
            Bekle(1.5, "1");

            o.Bas(Key.PageDown, KeyModifiers.Control);
            Bekle(3.0, "2");

            o.Bas(Key.PageDown, KeyModifiers.Control);
            Assert.Equal("chapter 1 -> no", o.View.Trace[^1]);
            o.Bekle(0.4);
            Bekle(3.0, "2");
            Assert.False(o.Motor.EndReached);

            o.Bas(Key.PageUp, KeyModifiers.Control);
            Assert.Equal("chapter -1 -> ok", o.View.Trace[^1]);
            Bekle(1.5, "1");

            o.Bas(Key.PageUp, KeyModifiers.Control);
            Bekle(0, "0");

            var kayit = o.Kayit.ToString();
            o.Kapat();
            return kayit;
        });

        Assert.Contains("chapter 1 -> ok", rapor);
        BolumKanit.Kapat("uc-bolum-160x90.mkv", "bolumler.txt");
        KisayolKanit.Kapat(Path.Combine("gecmis", "bolum-atlama"));
    }

    [Fact]
    public void GercekMotordaBolumsuzKlipteKonumDegismez()
    {
        AppHost.Run(() =>
        {
            var o = KisayolOrtam.Ac(BolumKanit.Bolumsuz, "bolum-yok");
            Assert.Empty(o.View.Chapters);
            o.Git(2);
            var once = o.Sayi("time-pos");
            Assert.InRange(once, 1.75, 2.25);

            o.Bas(Key.PageDown, KeyModifiers.Control);
            Assert.Equal("chapter 1 -> no", o.View.Trace[^1]);
            o.Bas(Key.PageUp, KeyModifiers.Control);
            Assert.Equal("chapter -1 -> no", o.View.Trace[^1]);
            o.Bekle(0.5);

            Assert.Equal(once, o.Sayi("time-pos"));
            Assert.False(o.Motor.StepChapter(false));
            Assert.False(o.Motor.StepChapter(true));
            o.Bekle(0.3);
            Assert.Equal(once, o.Sayi("time-pos"));

            o.Kapat();
            return "";
        });

        BolumKanit.Kapat("bolumsuz-160x90.mkv");
        KisayolKanit.Kapat(Path.Combine("gecmis", "bolum-yok"));
    }
}
