using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Playback;
using Xunit;

namespace VidShrink.Tests;

public sealed class OynaticiGeriBildirimTests
{
    private static string Klasor(string ad)
    {
        var path = Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-adb69bc0c379bc481", ad + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static PlayerView Ac(YolMotoru motor, out Window window, string klasor, bool gecmis, Action<PlayerView>? acilirken = null)
    {
        var view = new PlayerView { EngineFactory = () => motor };
        if (gecmis) view.HistoryPath = () => Path.Combine(klasor, "player-history.json");
        window = new Window { Width = 960, Height = 540, Content = view };
        window.Show();
        var open = view.OpenAsync(Path.Combine(klasor, "yok-sahte.mp4"));
        var saat = Stopwatch.StartNew();
        while (!open.IsCompleted && saat.Elapsed.TotalSeconds < 10)
        {
            Dilim();
            acilirken?.Invoke(view);
        }

        open.GetAwaiter().GetResult();
        saat.Restart();
        while (saat.Elapsed.TotalSeconds < 0.6)
        {
            Dilim();
            acilirken?.Invoke(view);
        }

        return view;
    }

    private static void Dilim()
    {
        using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(2));
        Dispatcher.UIThread.MainLoop(dilim.Token);
    }

    private static void Dongu(Func<bool> bitti, double saniye, Action? her = null)
    {
        var saat = Stopwatch.StartNew();
        while (!bitti() && saat.Elapsed.TotalSeconds < saniye)
        {
            Dilim();
            her?.Invoke();
        }
    }

    private static void Kapat(PlayerView view, Window window)
    {
        view.Close();
        window.Close();
    }

    private static (int W, int H, byte[] Px) Ciz(Window window)
    {
        var w = (int)window.Bounds.Width;
        var h = (int)window.Bounds.Height;
        using var kare = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
        kare.Render(window);
        var pixels = new byte[w * h * 4];
        var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            kare.CopyPixels(new PixelRect(0, 0, w, h), pinned.AddrOfPinnedObject(), pixels.Length, w * 4);
        }
        finally
        {
            pinned.Free();
        }

        return (w, h, pixels);
    }

    private static double Sapma((int W, int H, byte[] Px) a, (int W, int H, byte[] Px) b, int x, int y, int yari = 2)
    {
        double toplam = 0;
        var sayi = 0;
        for (var dy = -yari; dy <= yari; dy++)
            for (var dx = -yari; dx <= yari; dx++)
            {
                var i = ((y + dy) * a.W + x + dx) * 4;
                toplam += Math.Abs(a.Px[i] - b.Px[i]) + Math.Abs(a.Px[i + 1] - b.Px[i + 1]) + Math.Abs(a.Px[i + 2] - b.Px[i + 2]);
                sayi++;
            }

        return toplam / sayi;
    }

    [Fact]
    public void OynatSimgesininMaviDolgusuEskisindenHafifCizilir() => AppHost.Run(() =>
    {
        var body = new StringBuilder();
        var motor = new YolMotoru();
        var klasor = Klasor("medya");
        var view = Ac(motor, out var window, klasor, false);
        var simge = view.FindControl<Border>("PauseGlyph")!;
        var ikon = view.FindControl<Avalonia.Controls.Shapes.Path>("PauseGlyphIcon")!;
        var hedef = view.FindResource("PauseGlyphOpacity") is double o ? o : double.NaN;
        var hafif = view.FindResource("NeonBlueActive");
        var eski = view.FindResource("NeonBlue");

        view.Apply(Keymap.PlayPause.ToCommand());
        Dongu(() => !simge.IsVisible, 2);
        var zemin = Ciz(window);
        view.Apply(Keymap.PlayPause.ToCommand());
        Dongu(() => simge.IsVisible && simge.Opacity >= hedef - 0.01, 1);
        var merkez = simge.TranslatePoint(new Point(simge.Bounds.Width / 2, simge.Bounds.Height / 2), window)!.Value;
        var x = (int)merkez.X;
        var y = (int)merkez.Y;
        var simdiki = Ciz(window);
        var simdikiFirca = ikon.Fill;
        ikon.Fill = (IBrush)eski!;
        var eskiKare = Ciz(window);
        ikon.Fill = simdikiFirca;

        var yeniSapma = Sapma(simdiki, zemin, x, y);
        var eskiSapma = Sapma(eskiKare, zemin, x, y);
        body.AppendLine($"merkez {x},{y}; saydamlik {YolKanit.N(simge.Opacity)}; sapma hafif {YolKanit.N(yeniSapma)}, eski NeonBlue {YolKanit.N(eskiSapma)}");
        Kapat(view, window);
        Directory.Delete(klasor, true);
        YolKanit.Write("geri-bildirim-dolgu.txt", body.ToString());

        Assert.Same(hafif, simdikiFirca);
        Assert.True(eskiSapma > 10, "olumsuz kontrol: eski dolgu zeminden ayrilmali, olcu kor");
        Assert.True(yeniSapma > 1, "dolgu hic cizilmiyor");
        Assert.True(yeniSapma < eskiSapma * 0.6, $"dolgu hafiflemedi: {yeniSapma} / {eskiSapma}");
        YolKanit.Kapat("geri-bildirim-dolgu.txt");
        return 0;
    });

    [Fact]
    public void KendiligindenOynarkenGostergeCikmazKullaniciBasincaCikar() => AppHost.Run(() =>
    {
        var motor = new YolMotoru();
        var acilistaGorundu = false;
        var klasor = Klasor("medya");
        var view = Ac(motor, out var window, klasor, false, v => acilistaGorundu |= v.FindControl<Border>("PauseGlyph")!.IsVisible);
        var simge = view.FindControl<Border>("PauseGlyph")!;
        var oynuyor = view.IsPlaying && !motor.IsPaused;

        var sondaGorundu = false;
        motor.EndReached = true;
        Dongu(() => !view.IsPlaying, 2, () => sondaGorundu |= simge.IsVisible);
        Dongu(() => false, 0.6, () => sondaGorundu |= simge.IsVisible);
        var sondaDurdu = !view.IsPlaying;

        view.Apply(Keymap.PlayPause.ToCommand());
        var basinca = simge.IsVisible;
        var basincaOynuyor = view.IsPlaying;
        Dongu(() => !simge.IsVisible, 2);
        view.Apply(Keymap.PlayPause.ToCommand());
        var duraklatinca = simge.IsVisible;
        Kapat(view, window);
        Directory.Delete(klasor, true);

        Assert.True(oynuyor, "dosya acilinca kendiliginden oynamali");
        Assert.False(acilistaGorundu, "acilistaki kendiliginden oynatma gostergeyi gosterdi");
        Assert.True(sondaDurdu);
        Assert.False(sondaGorundu, "dosya sonundaki kendiliginden duraklama gostergeyi gosterdi");
        Assert.True(basincaOynuyor);
        Assert.True(basinca, "kullanicinin oynatmasi gostergeyi gostermeli");
        Assert.True(duraklatinca, "kullanicinin duraklatmasi gostergeyi gostermeli");
        return 0;
    });

    [Fact]
    public void DosyaSonundaAnahatBirKezYanipSonerGoruntuyuKaplamaz() => AppHost.Run(() =>
    {
        var body = new StringBuilder();
        var motor = new YolMotoru();
        var klasor = Klasor("medya");
        var view = Ac(motor, out var window, klasor, false);
        var cue = view.FindControl<Border>("EndCue")!;
        var yuzey = view.FindControl<Panel>("Surface")!;
        var sure = view.TryFindResource("EndCueDuration", out var s) && s is TimeSpan t ? t : TimeSpan.Zero;
        var tepe = view.FindResource("EndCueOpacity") is double o ? o : double.NaN;
        var yol = view.LoadedPath;
        var once = Ciz(window);
        var sol = yuzey.TranslatePoint(new Point(0, yuzey.Bounds.Height / 2), window)!.Value;
        var orta = yuzey.TranslatePoint(new Point(yuzey.Bounds.Width / 2, yuzey.Bounds.Height / 2), window)!.Value;

        motor.EndReached = true;
        var saat = Stopwatch.StartNew();
        Dongu(() => cue.IsVisible, 2);
        var basla = saat.Elapsed.TotalMilliseconds;
        var ornekler = new List<(double Ms, double Op)>();
        double kenarSapma = 0;
        double ortaSapma = double.NaN;
        while (cue.IsVisible && saat.Elapsed.TotalSeconds < 3)
        {
            Dilim();
            ornekler.Add((saat.Elapsed.TotalMilliseconds - basla, cue.Opacity));
            if (double.IsNaN(ortaSapma) && cue.Opacity >= tepe - 0.05)
            {
                var kare = Ciz(window);
                kenarSapma = Sapma(kare, once, (int)sol.X, (int)sol.Y, 0);
                ortaSapma = Sapma(kare, once, (int)orta.X, (int)orta.Y);
            }
        }

        var ekranda = saat.Elapsed.TotalMilliseconds - basla;
        Dongu(() => false, 1);
        var ilkSayi = view.EndCueCount;
        var enYuksek = ornekler.Count == 0 ? 0 : ornekler.Max(x => x.Op);
        var ara = ornekler.Any(x => x.Op > 0.02 && x.Op < tepe - 0.05);
        body.AppendLine($"sure {sure.TotalMilliseconds} ms, tepe {YolKanit.N(tepe)}; ekranda {YolKanit.N(ekranda)} ms, en yuksek {YolKanit.N(enYuksek)}, ara saydamlik {ara}, kenar sapma {YolKanit.N(kenarSapma)}, orta sapma {YolKanit.N(ortaSapma)}, sayi {ilkSayi}");
        body.AppendLine("iz " + string.Join(" ", ornekler.Where((_, i) => i % 6 == 0).Select(x => YolKanit.N(x.Ms) + ":" + YolKanit.N(x.Op))));

        var durdu = !view.IsPlaying;
        var ayniDosya = view.LoadedPath == yol && motor.IsOpen;

        view.Apply(Keymap.PlayPause.ToCommand());
        Dongu(() => view.IsPlaying && !motor.EndReached, 1);
        var yenidenOynadi = view.IsPlaying;
        motor.EndReached = true;
        Dongu(() => view.EndCueCount > ilkSayi, 2);
        var ikinciSayi = view.EndCueCount;

        view.Settings.Repeat = RepeatMode.One;
        view.Apply(Keymap.PlayPause.ToCommand());
        Dongu(() => view.IsPlaying && !motor.EndReached, 1);
        motor.EndReached = true;
        Dongu(() => !view.IsPlaying, 2);
        Dongu(() => false, 0.3);
        var tekrarSayi = view.EndCueCount;

        view.Settings.Repeat = RepeatMode.Off;
        view.Apply(Keymap.PlayPause.ToCommand());
        Dongu(() => view.IsPlaying && !motor.EndReached, 1);
        view.Apply(new PlayerCommand(PlayerCommandKind.LoopStart, 0));
        motor.EndReached = true;
        Dongu(() => !view.IsPlaying, 2);
        Dongu(() => false, 0.3);
        var donguSayi = view.EndCueCount;
        body.AppendLine($"yeniden oynatma {yenidenOynadi} -> sayi {ikinciSayi}; tek dosya tekrari {tekrarSayi}; A-B dongusu {donguSayi}");
        Kapat(view, window);
        Directory.Delete(klasor, true);
        YolKanit.Write("geri-bildirim-son.txt", body.ToString());

        Assert.True(sure > TimeSpan.Zero && sure <= TimeSpan.FromMilliseconds(500));
        Assert.InRange(ekranda, sure.TotalMilliseconds - 40, sure.TotalMilliseconds + 90);
        Assert.InRange(enYuksek, tepe - 0.05, tepe + 0.001);
        Assert.True(ara, "anahat yumusak girip cikmali");
        Assert.True(kenarSapma > 10, "anahat yuzeyin kenarinda gorunmeli");
        Assert.True(ortaSapma < 1, "anahat goruntuyu kaplamamali");
        Assert.False(cue.IsHitTestVisible);
        Assert.Equal(1, ilkSayi);
        Assert.True(durdu && ayniDosya, "dosya sonunda videodan cikilmamali");
        Assert.True(yenidenOynadi);
        Assert.Equal(2, ikinciSayi);
        Assert.Equal(2, tekrarSayi);
        Assert.Equal(2, donguSayi);
        YolKanit.Kapat("geri-bildirim-son.txt");
        return 0;
    });

    [Fact]
    public void HizKipiAyaraYazilirAcilistaOKipinHiziUygulanir() => AppHost.Run(() =>
    {
        var klasor = Klasor("hiz-kipi");
        var dosya = Path.Combine(klasor, PlayerSettings.FileName);

        var motor = new YolMotoru();
        var view = Ac(motor, out var window, klasor, true);
        var ilkKip = view.SpeedModeB;
        GirdiSurucu.Key(view, Key.V);
        DenetimSurucu.Wait(view, 0.05);
        var yazilanB = PlayerSettings.Load(dosya).SpeedModeB;
        Kapat(view, window);

        var motorB = new YolMotoru();
        var viewB = Ac(motorB, out var windowB, klasor, true);
        var dugmeB = viewB.FindControl<ToggleButton>("BtnSeritSpeedAb")!;
        var acilisB = (viewB.SpeedModeB, viewB.SpeedFactor, motorB.Hiz, dugmeB.IsChecked == true);
        GirdiSurucu.Key(viewB, Key.V);
        DenetimSurucu.Wait(viewB, 0.05);
        var yazilanA = PlayerSettings.Load(dosya).SpeedModeB;
        Kapat(viewB, windowB);

        var motorA = new YolMotoru();
        var viewA = Ac(motorA, out var windowA, klasor, true);
        var acilisA = (viewA.SpeedModeB, viewA.SpeedFactor, motorA.Hiz);
        Kapat(viewA, windowA);

        var motorYolsuz = new YolMotoru();
        var yolsuz = Ac(motorYolsuz, out var windowYolsuz, klasor, false);
        GirdiSurucu.Key(yolsuz, Key.V);
        DenetimSurucu.Wait(yolsuz, 0.05);
        Kapat(yolsuz, windowYolsuz);
        var dosyaSayisi = Directory.GetFiles(klasor, PlayerSettings.FileName, SearchOption.AllDirectories).Length;

        Assert.False(ilkKip);
        Assert.True(yazilanB);
        Assert.Equal((true, PlayerSettings.DefaultSpeedB, PlayerSettings.DefaultSpeedB, true), acilisB);
        Assert.False(yazilanA);
        Assert.Equal((false, PlayerSettings.DefaultSpeedA, PlayerSettings.DefaultSpeedA), acilisA);
        Assert.Equal(1, dosyaSayisi);
        Directory.Delete(klasor, true);
        return 0;
    });

    [Fact]
    public void HizKipiDosyadanOkunurBozukDegerAKipineDuser()
    {
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "worktree-agent-adb69bc0c379bc481", "hiz-kipi-dosya-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        var dosya = Path.Combine(klasor, PlayerSettings.FileName);

        new PlayerSettings { SpeedModeB = true, SpeedB = 1.5 }.Save(dosya);
        var metin = File.ReadAllText(dosya);
        var geri = PlayerSettings.Load(dosya);
        File.WriteAllText(dosya, "{\"speedMode\":\"x\"}");
        var bozuk = PlayerSettings.Load(dosya);
        File.WriteAllText(dosya, "{}");
        var eski = PlayerSettings.Load(dosya);
        Directory.Delete(klasor, true);

        Assert.Contains("\"speedMode\": \"B\"", metin, StringComparison.Ordinal);
        Assert.True(geri.SpeedModeB);
        Assert.Equal(1.5, geri.SpeedB, 6);
        Assert.False(bozuk.SpeedModeB);
        Assert.False(eski.SpeedModeB);
    }
}
