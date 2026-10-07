using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Playback;
using VidShrink.App.Themes;
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

    private static (int X, int Ust, int Alt) GovdeSutunu((int W, int H, byte[] Px) simdiki, (int W, int H, byte[] Px) zemin, Rect govde)
    {
        var sutunlar = new List<(int X, int Ilk, int Son)>();
        for (var x = (int)govde.X; x < (int)govde.Right; x++)
        {
            var ilk = -1;
            var son = -1;
            for (var y = (int)govde.Y; y < (int)govde.Bottom; y++)
            {
                if (Sapma(simdiki, zemin, x, y, 0) <= 20) continue;
                if (ilk < 0) ilk = y;
                son = y;
            }

            if (ilk >= 0) sutunlar.Add((x, ilk, son));
        }

        if (sutunlar.Count == 0) return (-1, 0, 0);
        var enBoy = sutunlar.Max(s => s.Son - s.Ilk);
        var secilen = sutunlar.First(s => s.Son - s.Ilk >= enBoy * 0.9);
        var boy = secilen.Son - secilen.Ilk;
        return (secilen.X + 3, secilen.Ilk + (int)(boy * 0.12), secilen.Ilk + (int)(boy * 0.85));
    }

    private static double Parla((int W, int H, byte[] Px) a, int x, int y)
    {
        var i = (y * a.W + x) * 4;
        return (a.Px[i] + a.Px[i + 1] + a.Px[i + 2]) / 3.0;
    }

    private static void Kaydet(Window window, string ad)
    {
        var klasor = Environment.GetEnvironmentVariable("VIDSHRINK_ROZET_PNG");
        if (string.IsNullOrEmpty(klasor)) return;
        Directory.CreateDirectory(klasor);
        var w = (int)window.Bounds.Width;
        var h = (int)window.Bounds.Height;
        using var kare = new RenderTargetBitmap(new PixelSize(w * 2, h * 2), new Vector(192, 192));
        kare.Render(window);
        kare.Save(Path.Combine(klasor, ad + ".png"), PngBitmapEncoderOptions.Default);
    }

    [Fact]
    public void OynatSimgesiMatDolguVeUstteUfakBeyazParlamaTasir() => AppHost.Run(() =>
    {
        var body = new StringBuilder();
        var motor = new YolMotoru();
        var klasor = Klasor("medya");
        var view = Ac(motor, out var window, klasor, false);
        var simge = view.FindControl<Border>("PauseGlyph")!;
        var ikon = view.FindControl<Avalonia.Controls.Shapes.Path>("PauseGlyphIcon")!;
        var parlama = view.FindControl<Avalonia.Controls.Shapes.Path>("PauseGlyphGloss")!;
        var hedef = view.FindResource("PauseGlyphOpacity") is double o ? o : double.NaN;
        var mat = view.FindResource("NeonBlue");
        var firca = view.FindResource("PauseGlyphGloss") as LinearGradientBrush;
        var parlamaRengi = view.FindResource("PauseGlossColor") is Color c ? c : default;
        var ilkDurak = firca?.GradientStops[0].Color;
        var sonDurak = firca?.GradientStops[1];

        (int W, int H, byte[] Px) Bos()
        {
            Dongu(() => !simge.IsVisible, 2);
            return Ciz(window);
        }

        void Goster()
        {
            view.Apply(Keymap.PlayPause.ToCommand());
            Dongu(() => simge.IsVisible && simge.Opacity >= hedef - 0.01, 1);
        }

        (int X, int Ust, int Alt) Olc(Rect govde, (int W, int H, byte[] Px) bos, out double fark, out double altFark)
        {
            (int W, int H, byte[] Px) var = default, yok = default;
            for (var deneme = 0; deneme < 8; deneme++)
            {
                Goster();
                var = Ciz(window);
                parlama.IsVisible = false;
                yok = Ciz(window);
                parlama.IsVisible = true;
                if (simge.IsVisible && simge.Opacity >= hedef - 0.01) break;
            }

            var kol = GovdeSutunu(var, bos, govde);
            fark = kol.X < 0 ? 0 : Parla(var, kol.X, kol.Ust) - Parla(yok, kol.X, kol.Ust);
            altFark = kol.X < 0 ? 0 : Math.Abs(Parla(var, kol.X, kol.Alt) - Parla(yok, kol.X, kol.Alt));
            return kol;
        }

        var zemin = Bos();
        Goster();
        var govde = new Rect(simge.TranslatePoint(new Point(), window)!.Value, simge.Bounds.Size);
        var parlamaGorunur = parlama.IsVisible;
        var parlamaVeri = parlama.Data;
        var ikonVeri = ikon.Data;
        Bos();
        var sutun = Olc(govde, zemin, out var ustFark, out var altFark);
        body.AppendLine($"govde sutunu {sutun.X}, ust y {sutun.Ust}, alt y {sutun.Alt}; parlama ust farki {YolKanit.N(ustFark)}, alt farki {YolKanit.N(altFark)}; parlama rengi {parlamaRengi}");
        Kaydet(window, "sonra-" + PaletteCatalog.Default);

        var oncekiFirca = ikon.Fill;
        var oncekiKenar = ikon.Stroke;
        var oncekiKalinlik = ikon.StrokeThickness;
        ikon.Fill = (IBrush)view.FindResource("NeonBlueActive")!;
        ikon.Stroke = (IBrush)view.FindResource("NeonPink")!;
        ikon.StrokeThickness = view.FindResource("SliderThumbStroke") is double k ? k : 2;
        parlama.IsVisible = false;
        Bos();
        Goster();
        Kaydet(window, "once-" + PaletteCatalog.Default);
        ikon.Fill = oncekiFirca;
        ikon.Stroke = oncekiKenar;
        ikon.StrokeThickness = oncekiKalinlik;
        parlama.IsVisible = true;

        var paletler = new List<string>();
        var baslangic = PaletteCatalog.Use(PaletteCatalog.Default);
        try
        {
            foreach (var ad in new[] { "Kar", "Dracula", "Neon", "Keskin" })
            {
                PaletteCatalog.Use(ad);
                var bos = Bos();
                var palettenSutun = Olc(govde, bos, out var fark, out _);
                body.AppendLine($"{ad}: parlama ust farki {YolKanit.N(fark)}");
                paletler.Add($"{ad}:{(fark > 3 ? "ok" : "yok")}");
                Bos();
                Goster();
                Kaydet(window, "sonra-" + ad);
            }
        }
        finally
        {
            PaletteCatalog.Use(baslangic);
        }

        Kapat(view, window);
        Directory.Delete(klasor, true);
        YolKanit.Write("geri-bildirim-mat-parlama.txt", body.ToString());

        Assert.Same(mat, ikon.Fill);
        Assert.Null(ikon.Stroke);
        Assert.NotNull(firca);
        Assert.Same(firca, parlama.Fill);
        Assert.Equal(parlamaRengi, ilkDurak);
        Assert.Equal(Colors.Transparent.A, sonDurak!.Color.A);
        Assert.True(sonDurak.Offset <= 0.5, "parlama govdenin ust yarisindan asagi inmemeli");
        Assert.True(firca!.Opacity > 0 && firca.Opacity <= 0.7, $"parlama ince olmali: {firca.Opacity}");
        Assert.True(parlamaGorunur);
        Assert.Same(ikonVeri, parlamaVeri);
        Assert.True(sutun.X >= 0, "govdede ust ve alt noktasi birlikte dolu bir sutun bulunamadi");
        Assert.True(ustFark > 3, $"ustte parlama yok: {ustFark}");
        Assert.True(altFark < 1, $"parlama altta da var, ufak degil: {altFark}");
        Assert.All(paletler, p => Assert.EndsWith(":ok", p));
        YolKanit.Kapat("geri-bildirim-mat-parlama.txt");
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

    /// <summary>
    /// Anahattin ekranda kaldigi sure duvar saatiyle olculur; yuklu kosucuda gec gelen kareler onu
    /// uzatir (CI'da 400 ms'lik surede 492,88 ms olculdu, kosum 37571553376). Pay surenin yarisindan
    /// kucuk kalir, iki kat ekranda kalan anahat yine kirmizi olur.
    /// </summary>
    private const double GecKarePayiMs = 180;

    [Fact]
    public void DosyaSonundaAnahatBirKezYanipSonerGoruntuyuKaplamaz() => AppHost.Run(() =>
    {
        var oncekiHareket = HoverZone.MotionReduced;
        HoverZone.MotionReduced = false;
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
        double hedef = 0;
        double yakalanan = double.NaN;
        while (cue.IsVisible && saat.Elapsed.TotalSeconds < 3)
        {
            Dilim();
            ornekler.Add((saat.Elapsed.TotalMilliseconds - basla, cue.Opacity));
            if (cue.GetBaseValue(Visual.OpacityProperty) is { HasValue: true } taban) hedef = Math.Max(hedef, taban.Value);
            if (double.IsNaN(ortaSapma) && cue.Opacity >= tepe / 2)
            {
                yakalanan = cue.Opacity;
                var kare = Ciz(window);
                kenarSapma = Sapma(kare, once, (int)sol.X, (int)sol.Y, 0);
                ortaSapma = Sapma(kare, once, (int)orta.X, (int)orta.Y);
            }
        }

        var ekranda = saat.Elapsed.TotalMilliseconds - basla;
        Dongu(() => false, 1);
        var ilkSayi = view.EndCueCount;
        var enYuksek = ornekler.Count == 0 ? 0 : ornekler.Max(x => x.Op);
        var ara = cue.Transitions?.OfType<DoubleTransition>().Any(x => x.Property == Visual.OpacityProperty && x.Duration > TimeSpan.Zero && x.Duration <= sure) == true;
        body.AppendLine($"sure {sure.TotalMilliseconds} ms, tepe {YolKanit.N(tepe)}; ekranda {YolKanit.N(ekranda)} ms, en yuksek {YolKanit.N(enYuksek)}, hedef {YolKanit.N(hedef)}, yakalanan {YolKanit.N(yakalanan)}, ara saydamlik {ara}, kenar sapma {YolKanit.N(kenarSapma)}, orta sapma {YolKanit.N(ortaSapma)}, sayi {ilkSayi}");
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
        HoverZone.MotionReduced = oncekiHareket;

        Assert.True(sure > TimeSpan.Zero && sure <= TimeSpan.FromMilliseconds(500));
        Assert.InRange(ekranda, sure.TotalMilliseconds - 40, sure.TotalMilliseconds + GecKarePayiMs);
        Assert.InRange(hedef, tepe - 0.001, tepe + 0.001);
        Assert.InRange(enYuksek, tepe / 2, tepe + 0.001);
        Assert.InRange(enYuksek, tepe / 2, tepe + 0.001);
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
