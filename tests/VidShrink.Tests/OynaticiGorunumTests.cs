using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Ffmpeg;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// 7b: bekleme süresini duvar saati olmadan süren sahte saat. Bu iş parçacığında gerçek
/// zamanlayıcı zaten tik atmıyor (ileti döngüsü yok); ölçüm süreyi kendi ilerletiyor, yani
/// hiçbir ölçüm <c>Thread.Sleep</c> beklemiyor.
/// </summary>
internal sealed class SeritSaati : IHoverClock
{
    private Action? _tik;

    /// <summary>Sayaca sorulan süre. Belirtecin koda ulaştığını bu gösterir.</summary>
    internal TimeSpan Sure { get; private set; }

    /// <summary>Bekleyen bir tik var mı. Yoksa karar beklemeden verilmiştir.</summary>
    internal bool Bekliyor => _tik is not null;

    public void Start(TimeSpan delay, Action fire)
    {
        Sure = delay;
        _tik = fire;
    }

    public void Stop() => _tik = null;

    internal void Ilerlet()
    {
        var tik = _tik;
        _tik = null;
        tik?.Invoke();
    }
}

internal static class GorunumKanit
{
    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "dalga3");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static void Write(string name, string body)
        => File.WriteAllText(Path.Combine(Folder, name), body, new UTF8Encoding(false));

    internal static string Gecici(string ad)
    {
        var path = Path.Combine(Folder, "gecici", ad + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }

    internal static string Uret(string name, params string[] args)
    {
        var path = Path.Combine(Folder, name);
        if (File.Exists(path) && new FileInfo(path).Length > 0) return path;
        Assert.True(ToolLocator.IsAvailable(out var missing), $"klip uretimi icin {missing} gerekli");

        var partial = path + ".part" + Path.GetExtension(path);
        var (kod, _, hata) = Kos(ToolLocator.Ffmpeg, new[] { "-y", "-hide_banner" }.Concat(args).Append(partial).ToArray());
        Assert.True(kod == 0, $"ffmpeg {name} uretemedi (kod {kod}): {hata[Math.Max(0, hata.Length - 600)..]}");
        File.Move(partial, path, true);
        return path;
    }

    internal static (int Kod, string Cikti, string Hata) Kos(string tool, params string[] args)
    {
        var psi = new ProcessStartInfo(tool)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi)!;
        var cikti = process.StandardOutput.ReadToEndAsync();
        var hata = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException(tool + " 120 sn icinde bitmedi");
        }

        return (process.ExitCode, cikti.GetAwaiter().GetResult().Trim(), hata.GetAwaiter().GetResult());
    }

    internal static string Probe(string path, string stream, string entries)
    {
        var args = new List<string> { "-v", "error" };
        if (stream.Length > 0) args.AddRange(new[] { "-select_streams", stream, "-show_entries", "stream=" + entries });
        else args.AddRange(new[] { "-show_entries", "format=" + entries });
        args.AddRange(new[] { "-of", "csv=p=0", path });
        var (kod, cikti, hata) = Kos(ToolLocator.Ffprobe, args.ToArray());
        Assert.True(kod == 0, $"ffprobe {path}: {hata}");
        return cikti;
    }

    internal static (int Genislik, int Yukseklik) Boyut(string path)
    {
        var parts = Probe(path, "v:0", "width,height").Split(',');
        return (int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    internal static bool KaynakBoyutunda(string png, string kaynak) => Boyut(png) == Boyut(kaynak);

    internal static string BolumluKlip
    {
        get
        {
            var meta = Path.Combine(Folder, "bolumler.txt");
            File.WriteAllText(meta, string.Join("\n",
                ";FFMETADATA1",
                "[CHAPTER]", "TIMEBASE=1/1000", "START=0", "END=5000", "title=a",
                "[CHAPTER]", "TIMEBASE=1/1000", "START=5000", "END=12000", "title=b",
                "[CHAPTER]", "TIMEBASE=1/1000", "START=12000", "END=25000", "title=c") + "\n", new UTF8Encoding(false));
            return Uret("bolumlu-320x180.mp4", "-i", MotorKlipleri.Kucuk, "-i", meta,
                "-map", "0", "-map_metadata", "1", "-map_chapters", "1", "-c", "copy");
        }
    }

    internal static void Bekle(PlayerView view, Func<Task> task, double seconds)
        => DenetimSurucu.Pump(view, () => task().IsCompleted, seconds);
}

/// <summary>
/// 7b dalgasinin kaniti. Yerlesim olcumu <see cref="WindowLayoutTests"/> ile ayni yolu
/// izler: pencere gosterilmez, kok gorsel cocuk istenen gorus alaninda olculup
/// yerlestirilir. Sekme secildikten sonra agacin tamami gecersizlenir, yoksa ilk kez
/// olculen sekme sifir sinirla temiz isaretlenip olcumden kacar.
/// </summary>
internal static class SeritKanit
{
    /// <summary>Olcumun gorus alani: <c>WindowPreferredWidth</c> x <c>WindowPreferredHeight</c>.</summary>
    internal static readonly Size Pencere = new(1560, 1060);

    internal static string Folder
    {
        get
        {
            var path = Path.Combine(GirdiKanit.Root, ".calisma", "dalga7b");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal static void Write(string name, string body)
        => File.WriteAllText(Path.Combine(Folder, name), body, new UTF8Encoding(false));

    internal static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    internal static void LayOutAt(Window window, Size size)
    {
        window.Width = double.NaN;
        window.Height = double.NaN;
        window.Measure(size);
        window.Arrange(new Rect(size));
        window.UpdateLayout();

        var root = (Layoutable)window.GetVisualChildren().Single();
        root.Measure(size);
        root.Arrange(new Rect(size));

        foreach (var node in window.GetVisualDescendants().OfType<Visual>()) node.RenderTransform = null;
    }

    internal static void RelayoutAt(Window window, Size size)
    {
        foreach (var node in window.GetVisualDescendants().OfType<Layoutable>()) node.InvalidateMeasure();

        var root = (Layoutable)window.GetVisualChildren().Single();
        root.InvalidateMeasure();
        root.Measure(size);
        root.Arrange(new Rect(size));

        foreach (var node in window.GetVisualDescendants().OfType<Visual>()) node.RenderTransform = null;
    }

    /// <summary>Oynatici sekmesi secili pencerede istenen denetimin olculmus siniri.</summary>
    internal static T OynaticidaOku<T>(Func<PlayerView, T> oku)
        => AppHost.Run(() =>
        {
            var window = new MainWindow();
            LayOutAt(window, Pencere);
            window.FindControl<TabControl>("Tabs")!.SelectedIndex = window.PlayerTabIndex;
            RelayoutAt(window, Pencere);
            var sonuc = oku(window.PlayerTab);
            window.Close();
            return sonuc;
        });
}

public sealed class OynaticiGorunumTests
{
    private static string Ayar(string ad) => Path.Combine(GorunumKanit.Gecici(ad), "history.json");

    /// <summary>
    /// 7b/K3 — dort duz metin satiri kalkinca video dikeyde ne kadar buyuyor. Olcu
    /// <c>Surface</c> panosunun piksel alani; pencere 1560x1060 gorus alaninda basssiz
    /// yerlestiriliyor. Sayi kanit dosyasina yazilir, pim alttaki taban degerdir.
    /// </summary>
    /// <summary>
    /// 7b/K1: alt şerit gösterirken beklemez, gizlerken belirtecinin söylediği 360 ms'yi
    /// bekler. İki sayı da <c>Themes/Playback.axaml</c>'den geliyor; ölçüm sayıyı
    /// kodlamıyor, sayacın ne kadar beklemek istediğini okuyor.
    /// </summary>
    [Fact]
    public void SeritGostermedeBeklemezGizlemedeBekler()
    {
        var olcu = SeritKanit.OynaticidaOku(view =>
        {
            var bolge = view.SeritZone;
            bolge.Reset(false);

            var saat = new SeritSaati();
            bolge.Clock = saat;

            bolge.PointerWithin(true);
            var acildi = view.SeritRevealed;
            var gostermedeSayac = saat.Bekliyor;

            bolge.PointerWithin(false);
            var halaAcik = view.SeritRevealed;
            var gizlemeSuresi = saat.Sure;

            saat.Ilerlet();
            return (acildi, gostermedeSayac, halaAcik, gizlemeSuresi, view.SeritRevealed);
        });

        var body = new StringBuilder();
        body.AppendLine($"gosterme -> gorunur {olcu.Item1}, sayac kuruldu {olcu.Item2}");
        body.AppendLine($"gosterme beklemesi: 0 ms");
        body.AppendLine($"fare cikti -> hala gorunur {olcu.Item3}");
        body.AppendLine($"gizleme beklemesi: {SeritKanit.N(olcu.Item4.TotalMilliseconds)} ms");
        body.AppendLine($"bekleme dolunca -> gorunur {olcu.Item5}");
        SeritKanit.Write("serit-zamanlama.txt", body.ToString());

        Assert.True(olcu.Item1, "serit gosterme icin bekledi");
        Assert.False(olcu.Item2, "gosterme sayac kurdu");
        Assert.True(olcu.Item3, "serit beklemeden gizlendi");
        Assert.Equal(TimeSpan.FromMilliseconds(360), olcu.Item4);
        Assert.False(olcu.Item5, "serit bekleme dolunca gizlenmedi");
    }

    /// <summary>
    /// 7b/K2: süre etiketi geçen ve kalan süreyi birlikte gösteriyor. Sayılar değişmez
    /// biçimde yazıldığı için iki dilde de birebir aynı; pimlenen şey budur. Süre
    /// bilinmiyorken etiket sıfır değil belirsiz gösteriyor.
    /// </summary>
    [Fact]
    public void SureEtiketiGecenVeKalaniIkiDildeAyniGosterir()
    {
        var okumalar = AppHost.Run(() =>
        {
            var satirlar = new List<(string dil, string kisa, string uzun, string bilinmeyen)>();
            foreach (var dil in new[] { "en", "tr" })
            {
                Strings.Use(dil);
                satirlar.Add((
                    dil,
                    PlayerView.ClockPair(12, 90),
                    PlayerView.ClockPair(12, 3732),
                    PlayerView.ClockPair(0, 0)));
            }

            Strings.Use("en");
            return satirlar;
        });

        var body = new StringBuilder();
        body.AppendLine("bicim: gecen / -kalan");
        foreach (var satir in okumalar)
        {
            body.AppendLine($"{satir.dil}: 12 sn / 90 sn  -> {satir.kisa}");
            body.AppendLine($"{satir.dil}: 12 sn / 3732 sn -> {satir.uzun}");
            body.AppendLine($"{satir.dil}: sure bilinmiyor -> {satir.bilinmeyen}");
        }

        SeritKanit.Write("sure-etiketi.txt", body.ToString());

        Assert.Equal("00:12 / -01:18", okumalar[0].kisa);
        Assert.Equal("00:00:12 / -01:02:00", okumalar[0].uzun);
        Assert.Equal("--:-- / --:--", okumalar[0].bilinmeyen);
        Assert.Equal(okumalar[0].kisa, okumalar[1].kisa);
        Assert.Equal(okumalar[0].uzun, okumalar[1].uzun);
        Assert.Equal(okumalar[0].bilinmeyen, okumalar[1].bilinmeyen);
    }

    [Fact]
    public void OynaticiYuzeyininPikselAlaniOlculur()
    {
        var olcu = SeritKanit.OynaticidaOku(view =>
        {
            var surface = view.FindControl<Panel>("Surface")!;
            var stage = view.FindControl<Border>("Stage")!;
            return (surface.Bounds.Width, surface.Bounds.Height, stage.Bounds.Height, view.Bounds.Height);
        });

        var alan = olcu.Item1 * olcu.Item2;
        var body = new StringBuilder();
        body.AppendLine($"gorus alani: {SeritKanit.N(SeritKanit.Pencere.Width)}x{SeritKanit.N(SeritKanit.Pencere.Height)}");
        body.AppendLine($"PlayerView yuksekligi: {SeritKanit.N(olcu.Item4)}");
        body.AppendLine($"Stage yuksekligi: {SeritKanit.N(olcu.Item3)}");
        body.AppendLine($"Surface: {SeritKanit.N(olcu.Item1)} x {SeritKanit.N(olcu.Item2)}");
        body.AppendLine($"Surface piksel alani: {SeritKanit.N(alan)}");
        SeritKanit.Write("yuzey-alani.txt", body.ToString());

        Assert.True(olcu.Item1 > 0 && olcu.Item2 > 0, body.ToString());
    }

    [Fact]
    public async Task EkranGoruntusuKaynakCozunurlugundeKaydedilirPencereBoyutuKontroluKirilir()
    {
        var clip = MotorKlipleri.Kucuk;
        var klasor = GorunumKanit.Gecici("goruntu");
        var history = Ayar("goruntu-ayar");

        var rapor = AppHost.Run(() =>
        {
            var view = DenetimSurucu.Ac(clip, out var window, history);
            view.Settings.ScreenshotFolder = klasor;
            DenetimSurucu.Git(view, 3);
            GirdiSurucu.Key(view, Key.E, KeyModifiers.Control);
            GorunumKanit.Bekle(view, () => view.LastScreenshot, 15);
            var yol = view.LastScreenshot.IsCompleted ? view.LastScreenshot.Result : null;
            var iz = view.Trace[^1];
            var durum = view.ViewStateText;
            view.Close();
            window.Close();
            return (yol, iz, durum);
        });

        var pencere = new MpvEngine(new PlaybackOptions { RenderWidth = 640, RenderHeight = 360, Audio = false });
        byte[]? piksel = null;
        var (w, h, stride) = (0, 0, 0);
        try
        {
            await pencere.OpenAsync(clip);
            long seen = 0;
            var saat = Stopwatch.StartNew();
            while (piksel is null && saat.Elapsed.TotalSeconds < 10)
            {
                pencere.TryCopyLatest(ref seen, (p, pw, ph, ps) =>
                {
                    piksel = new byte[ps * ph];
                    Marshal.Copy(p, piksel, 0, piksel.Length);
                    (w, h, stride) = (pw, ph, ps);
                });
                Thread.Sleep(10);
            }
        }
        finally
        {
            pencere.Dispose();
        }

        Assert.NotNull(piksel);
        var kontrol = Path.Combine(klasor, "pencere-boyutu.png");
        AppHost.Run(() =>
        {
            using var bitmap = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            using (var buffer = bitmap.Lock())
                for (var row = 0; row < h; row++)
                    Marshal.Copy(piksel!, row * stride, buffer.Address + row * buffer.RowBytes, w * 4);
            bitmap.Save(kontrol, PngBitmapEncoderOptions.Default);
            return 0;
        });

        var kaynak = GorunumKanit.Boyut(clip);
        var body = new StringBuilder();
        body.AppendLine($"kaynak (ffprobe): {kaynak.Genislik}x{kaynak.Yukseklik}");
        body.AppendLine($"iz: {rapor.iz}");
        body.AppendLine($"durum satiri: {rapor.durum}");
        body.AppendLine($"kaydedilen: {rapor.yol ?? "YOK"}");
        var goruntu = rapor.yol is null ? (0, 0) : GorunumKanit.Boyut(rapor.yol);
        body.AppendLine($"kaydedilen (ffprobe): {goruntu.Item1}x{goruntu.Item2}, kaynak boyutunda: {rapor.yol is not null && GorunumKanit.KaynakBoyutunda(rapor.yol, clip)}");
        var kontrolBoyut = GorunumKanit.Boyut(kontrol);
        var kontrolGecti = GorunumKanit.KaynakBoyutunda(kontrol, clip);
        body.AppendLine($"negatif kontrol, pencere boyutunda yakalama (ffprobe): {kontrolBoyut.Genislik}x{kontrolBoyut.Yukseklik}, kaynak boyutunda: {kontrolGecti}");
        GorunumKanit.Write("ekran-goruntusu.txt", body.ToString());

        Assert.NotNull(rapor.yol);
        Assert.StartsWith(Path.GetFullPath(klasor), rapor.yol!, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(Path.GetFileNameWithoutExtension(clip) + "_00-00-0", Path.GetFileName(rapor.yol!), StringComparison.Ordinal);
        Assert.True(GorunumKanit.KaynakBoyutunda(rapor.yol!, clip), body.ToString());
        Assert.False(kontrolGecti, body.ToString());
    }

    [Fact]
    public void BilgiPaneliMotordanDortAlaniGosterir()
    {
        var clip = MotorKlipleri.Kucuk;
        var history = Ayar("bilgi");
        var codec = GorunumKanit.Probe(clip, "v:0", "codec_name");
        var boyut = GorunumKanit.Boyut(clip);
        var oranParca = GorunumKanit.Probe(clip, "v:0", "r_frame_rate").Split('/');
        var fps = double.Parse(oranParca[0], CultureInfo.InvariantCulture) / double.Parse(oranParca[1], CultureInfo.InvariantCulture);
        var bitHizi = double.Parse(GorunumKanit.Probe(clip, "", "bit_rate"), CultureInfo.InvariantCulture);
        var ses = GorunumKanit.Probe(clip, "a:0", "codec_name,sample_rate,channels").Split(',');

        var rapor = AppHost.Run(() =>
        {
            var view = DenetimSurucu.Ac(clip, out var window, history);
            var gizliyken = view.InfoVisible;
            GirdiSurucu.Key(view, Key.F1, KeyModifiers.Control);
            var metin = view.InfoText;
            var details = view.Engine!.Details;
            var bos = PlayerView.Describe(null);
            view.Close();
            window.Close();
            return (gizliyken, view.InfoVisible, metin, details, bos);
        });

        var beklenen = new[]
        {
            Strings.Get("player.info.codec", codec),
            Strings.Get("player.info.resolution", boyut.Genislik.ToString(CultureInfo.InvariantCulture) + "×" + boyut.Yukseklik.ToString(CultureInfo.InvariantCulture)),
            Strings.Get("player.info.framerate", fps.ToString("0.###", CultureInfo.CurrentCulture)),
            Strings.Get("player.info.audio", ses[0], int.Parse(ses[2], CultureInfo.InvariantCulture), int.Parse(ses[1], CultureInfo.InvariantCulture))
        };
        var sapma = rapor.details is null ? double.NaN : Math.Abs(rapor.details.BitsPerSecond - bitHizi) / bitHizi;

        var body = new StringBuilder();
        body.AppendLine($"ffprobe: {codec} {boyut.Genislik}x{boyut.Yukseklik} {fps:0.###} fps, {bitHizi:0} b/s, ses {string.Join(" ", ses)}");
        body.AppendLine($"motor: {rapor.details}");
        body.AppendLine($"bit hizi sapmasi: {sapma:0.####} (esik 0.02)");
        body.AppendLine("panel:");
        body.AppendLine(rapor.metin);
        body.AppendLine($"motor yokken: {rapor.bos}");
        GorunumKanit.Write("bilgi-paneli.txt", body.ToString());

        Assert.False(rapor.gizliyken);
        Assert.True(rapor.Item2);
        foreach (var satir in beklenen) Assert.Contains(satir, rapor.metin);
        Assert.Contains(Strings.Get("player.info.bitrate", (rapor.details!.BitsPerSecond / 1000).ToString("0", CultureInfo.CurrentCulture)), rapor.metin);
        Assert.True(sapma <= 0.02, body.ToString());
        Assert.DoesNotContain(codec, rapor.bos);
    }

    [Fact]
    public void SonrakiDosyaAdSirasindaAcarTekrarKapaliykenSondaDurur()
    {
        var kaynak = MotorKlipleri.Kucuk;
        var klasor = GorunumKanit.Gecici("klasor");
        foreach (var ad in new[] { "klip 1.mp4", "klip 10.mp4", "klip 2.mp4" })
            File.Copy(kaynak, Path.Combine(klasor, ad));
        File.WriteAllText(Path.Combine(klasor, "notlar.txt"), "x");
        var history = Ayar("klasor-ayar");

        var rapor = AppHost.Run(() =>
        {
            var body = new StringBuilder();
            var acilan = new List<string>();
            var view = DenetimSurucu.Ac(Path.Combine(klasor, "klip 1.mp4"), out var window, history);
            body.AppendLine($"tekrar: {view.Settings.Repeat}, karistir: {view.Settings.Shuffle}");

            void Bas(Key key, KeyModifiers mods = KeyModifiers.None)
            {
                GirdiSurucu.Key(view, key, mods);
                GorunumKanit.Bekle(view, () => view.Navigation, 20);
                var ad = view.LoadedPath is null ? "-" : Path.GetFileName(view.LoadedPath);
                acilan.Add(ad);
                body.AppendLine($"{key} -> iz '{view.Trace[^1]}', acik: {ad}");
            }

            Bas(Key.PageDown);
            Bas(Key.PageDown);
            Bas(Key.PageDown);
            var sonNotu = view.ViewStateText;
            Bas(Key.PageUp);
            Bas(Key.PageDown);
            GirdiSurucu.Key(view, Key.B, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift);
            body.AppendLine($"tekrar kipi: {view.Settings.Repeat}");
            Bas(Key.PageDown);
            var ayarDosyasi = File.ReadAllText(Path.Combine(Path.GetDirectoryName(history)!, PlayerSettings.FileName));
            var bitisNotu = LanguageCatalog.Display(Strings.Get("player.list.end"));
            body.AppendLine($"son not: {sonNotu}");
            body.AppendLine($"ayar dosyasi: {ayarDosyasi.Replace(Environment.NewLine, " ")}");
            view.Close();
            window.Close();
            return (body.ToString(), acilan, sonNotu, ayarDosyasi, bitisNotu);
        });

        GorunumKanit.Write("klasor-gezinme.txt", rapor.Item1);
        Assert.Equal(new[] { "klip 2.mp4", "klip 10.mp4", "klip 10.mp4", "klip 2.mp4", "klip 10.mp4", "klip 1.mp4" }, rapor.acilan);
        Assert.Contains(rapor.bitisNotu, rapor.sonNotu);
        Assert.Contains("\"All\"", rapor.ayarDosyasi);
    }

    [Fact]
    public void KlasorSirasiDogalSiralamaVeKaristirmaTohumlaKararli()
    {
        var klasor = GorunumKanit.Gecici("sira");
        foreach (var ad in new[] { "b.mkv", "a10.mp4", "a2.mp4", "a1.mp4", "belge.txt", "C.webm" })
            File.WriteAllText(Path.Combine(klasor, ad), "");

        var sira = FolderNavigator.Siblings(Path.Combine(klasor, "a1.mp4")).Select(Path.GetFileName).ToList();
        var dosyalar = Enumerable.Range(1, 10).Select(i => "f" + i).ToList();
        var bir = FolderNavigator.Order(dosyalar, true, 7);
        var iki = FolderNavigator.Order(dosyalar, true, 7);
        var baska = FolderNavigator.Order(dosyalar, true, 8);
        var duz = FolderNavigator.Order(dosyalar, false, 7);
        var sonda = FolderNavigator.Step(Path.Combine(klasor, "C.webm"), true, RepeatMode.Off, false, 1);
        var basa = FolderNavigator.Step(Path.Combine(klasor, "C.webm"), true, RepeatMode.All, false, 1);
        var once = FolderNavigator.Step(Path.Combine(klasor, "a1.mp4"), false, RepeatMode.Off, false, 1);

        GorunumKanit.Write("klasor-sirasi.txt",
            $"sira: {string.Join(", ", sira)}\nkaristir 7: {string.Join(",", bir)}\nkaristir 8: {string.Join(",", baska)}\nsonda (kapali): {sonda ?? "null"}\nsonda (hepsi): {basa}\nbasta geri: {once ?? "null"}\n");
        Assert.Equal(new[] { "a1.mp4", "a2.mp4", "a10.mp4", "b.mkv", "C.webm" }, sira);
        Assert.Equal(bir, iki);
        Assert.Equal(dosyalar.OrderBy(x => x), bir.OrderBy(x => x));
        Assert.NotEqual(dosyalar, bir);
        Assert.NotEqual(bir, baska);
        Assert.Equal(dosyalar, duz);
        Assert.Null(sonda);
        Assert.Null(once);
        Assert.Equal("a1.mp4", Path.GetFileName(basa));
    }

    [Fact]
    public void SonDosyalarOnlaSinirlanirVeYenidenAcilistaKalir()
    {
        var klasor = GorunumKanit.Gecici("son");
        var dosya = Path.Combine(klasor, RecentFiles.FileName);
        var recent = new RecentFiles();
        for (var i = 1; i <= 12; i++) recent.Add(Path.Combine(klasor, $"v{i}.mp4"));
        recent.Add(Path.Combine(klasor, "v5.mp4"));
        recent.Save(dosya);
        var geri = RecentFiles.Load(dosya).Items.Select(Path.GetFileName).ToList();

        var clip = MotorKlipleri.Kucuk;
        var history = Ayar("son-ayar");
        var rapor = AppHost.Run(() =>
        {
            var view = DenetimSurucu.Ac(clip, out var window, history);
            view.Close();
            window.Close();

            var yeni = GirdiSurucu.Kur(out var ikinci);
            yeni.HistoryPath = () => history;
            var ilk = yeni.Recent.Items.FirstOrDefault();
            var menu = yeni.BuildMenu().Items.OfType<MenuItem>()
                .FirstOrDefault(item => Equals(item.Header, Strings.Get("player.list.recent")));
            var satirlar = menu?.Items.OfType<MenuItem>().Select(item => item.Tag as string).ToList() ?? new List<string?>();
            ikinci.Close();
            return (ilk, satirlar);
        });

        GorunumKanit.Write("son-dosyalar.txt",
            $"12 ekleme + v5 tekrar, kaydedip okununca ({geri.Count}): {string.Join(", ", geri)}\nyeniden acilista ilk: {rapor.ilk}\nmenu: {string.Join(" | ", rapor.satirlar)}\n");
        Assert.Equal(RecentFiles.Capacity, geri.Count);
        Assert.Equal(new[] { "v5.mp4", "v12.mp4", "v11.mp4", "v10.mp4", "v9.mp4", "v8.mp4", "v7.mp4", "v6.mp4", "v4.mp4", "v3.mp4" }, geri);
        Assert.Equal(Path.GetFullPath(clip), rapor.ilk);
        Assert.Contains(Path.GetFullPath(clip), rapor.satirlar);
    }

    [Fact]
    public void DondurmeAynalamaVeOranMotorOzelligineYazilirVeGeriOkunur()
    {
        var clip = MotorKlipleri.Kucuk;
        var rapor = AppHost.Run(() =>
        {
            var body = new StringBuilder();
            var view = DenetimSurucu.Ac(clip, out var window);
            var motor = DenetimSurucu.Motor(view);
            var frame = view.FindControl<Image>("Frame")!;
            string Kare() => frame.Source is Bitmap b ? $"{b.PixelSize.Width}x{b.PixelSize.Height}" : "-";

            var vfOnce = motor.GetProperty("vf") ?? "";
            body.AppendLine($"once: video-rotate {motor.GetProperty("video-rotate")}, vf '{vfOnce}', kare {Kare()}");

            GirdiSurucu.Key(view, Key.S, KeyModifiers.Control | KeyModifiers.Shift);
            DenetimSurucu.Pump(view, () => Kare() == "180x320", 5);
            var donme = (motor.GetProperty("video-rotate"), motor.Rotation, Kare());
            body.AppendLine($"Ctrl+Shift+S: video-rotate {donme.Item1}, Rotation {donme.Rotation}, kare {donme.Item3}");

            GirdiSurucu.Key(view, Key.H, KeyModifiers.Control);
            var ayna = (motor.GetProperty("vf") ?? "", motor.Mirrored);
            body.AppendLine($"Ctrl+H: vf '{ayna.Item1}', Mirrored {ayna.Mirrored}");
            GirdiSurucu.Key(view, Key.H, KeyModifiers.Control);
            var aynaKalkti = (motor.GetProperty("vf") ?? "", motor.Mirrored);
            body.AppendLine($"Ctrl+H tekrar: vf '{aynaKalkti.Item1}', Mirrored {aynaKalkti.Mirrored}");

            GirdiSurucu.Key(view, Key.F5, KeyModifiers.Control);
            var oran = (motor.GetProperty("video-aspect-override"), motor.AspectOverride);
            body.AppendLine($"Ctrl+F5: video-aspect-override {oran.Item1}, AspectOverride {oran.AspectOverride:0.####}");
            var durum = view.ViewStateText;
            body.AppendLine($"durum: {durum}");
            view.Close();
            window.Close();
            return (body.ToString(), vfOnce, donme, ayna, aynaKalkti, oran);
        });

        GorunumKanit.Write("dondur-aynala.txt", rapor.Item1);
        Assert.DoesNotContain("vsmirror", rapor.vfOnce);
        Assert.Equal("90", rapor.donme.Item1);
        Assert.Equal(90, rapor.donme.Rotation);
        Assert.Equal("180x320", rapor.donme.Item3);
        Assert.Contains("vsmirror", rapor.ayna.Item1);
        Assert.True(rapor.ayna.Mirrored);
        Assert.DoesNotContain("vsmirror", rapor.aynaKalkti.Item1);
        Assert.False(rapor.aynaKalkti.Mirrored);
        Assert.InRange(rapor.oran.AspectOverride, 16.0 / 9 - 0.001, 16.0 / 9 + 0.001);
    }

    [Fact]
    public void BolumVeYerImleriZamanCubugundaIsaretlenir()
    {
        var clip = GorunumKanit.BolumluKlip;
        var history = Ayar("bolum");
        var rapor = AppHost.Run(() =>
        {
            var body = new StringBuilder();
            var view = DenetimSurucu.Ac(clip, out var window, history);
            window.Show();
            DenetimSurucu.Wait(view, 0.2);
            DenetimSurucu.Duraklat(view);
            DenetimSurucu.Git(view, 8);
            GirdiSurucu.Key(view, Key.N);
            view.RefreshState();

            var bolumler = view.Chapters.ToList();
            var isaretler = view.Marks.ToList();
            var cubuk = view.FindControl<Panel>("SeekBar")!;
            var katman = view.FindControl<Canvas>("SeekMarkLayer")!;
            body.AppendLine($"bolumler: {string.Join(", ", bolumler.Select(DenetimKanit.N))}");
            body.AppendLine($"isaretler: {string.Join(", ", isaretler.Select(m => DenetimKanit.N(m.Seconds) + (m.Chapter ? " bolum" : " imi")))}");
            body.AppendLine($"cubuk genisligi {DenetimKanit.N(cubuk.Bounds.Width)}, cizilen isaret {katman.Children.Count}");

            var genislik = cubuk.Bounds.Width;
            view.SeekToPointer(genislik / 2);
            DenetimSurucu.Bekle(view);
            var hedef = view.Seek.Target;
            body.AppendLine($"cubugun ortasina tik: hedef {DenetimKanit.N(hedef)}, sure {DenetimKanit.N(view.Seek.Duration)}, iz {view.Trace[^1]}");
            var sure = view.Seek.Duration;
            view.Close();
            window.Close();
            return (body.ToString(), bolumler, isaretler, katman.Children.Count, genislik, hedef, sure);
        });

        GorunumKanit.Write("zaman-cubugu-isaretleri.txt", rapor.Item1);
        Assert.Equal(3, rapor.bolumler.Count);
        Assert.Equal(new[] { (5.0, true), (8.0, false), (12.0, true) },
            rapor.isaretler.Select(m => (Math.Round(m.Seconds), m.Chapter)).ToArray());
        Assert.Equal(3, rapor.Item4);
        Assert.True(rapor.genislik > 0);
        Assert.InRange(rapor.hedef, rapor.sure / 2 - 0.5, rapor.sure / 2 + 0.5);
    }

    [Fact]
    public void HerZamanUstteVeSurukleBirakPencereyeUlasir()
    {
        var clip = MotorKlipleri.Kucuk;
        var ikinci = GorunumKanit.BolumluKlip;
        var history = Ayar("ustte");
        var rapor = AppHost.Run(() =>
        {
            var view = DenetimSurucu.Ac(clip, out var window, history);
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var once = window.Topmost;
            GirdiSurucu.Key(view, Key.A, KeyModifiers.Control);
            var acik = window.Topmost;
            GirdiSurucu.Key(view, Key.A, KeyModifiers.Control);
            var kapali = window.Topmost;

            var yok = view.OpenDropped(Path.Combine(GorunumKanit.Folder, "olmayan.mp4"));
            var birakilan = view.OpenDropped(ikinci);
            GorunumKanit.Bekle(view, () => view.Navigation, 20);
            var acilan = view.LoadedPath;
            var izinli = DragDrop.GetAllowDrop(view);
            view.Close();
            window.Close();
            return (once, acik, kapali, yok, birakilan, acilan, izinli);
        });

        GorunumKanit.Write("ustte-birak.txt", $"Topmost once {rapor.once}, Ctrl+A {rapor.acik}, tekrar {rapor.kapali}\nAllowDrop {rapor.izinli}\nolmayan dosya birakildi: {rapor.yok}\nklip birakildi: {rapor.birakilan}, acilan {rapor.acilan}\n");
        Assert.False(rapor.once);
        Assert.True(rapor.acik);
        Assert.False(rapor.kapali);
        Assert.True(rapor.izinli);
        Assert.False(rapor.yok);
        Assert.True(rapor.birakilan);
        Assert.Equal(ikinci, rapor.acilan);
    }

    [Fact]
    public void OynaticiAyarlariGeciciDizindeYazilirVeOkunur()
    {
        var klasor = GorunumKanit.Gecici("ayar");
        var dosya = Path.Combine(klasor, PlayerSettings.FileName);
        var ayar = new PlayerSettings { ScreenshotFolder = klasor, ScreenshotPattern = "{name}:{time}", Repeat = RepeatMode.One, Shuffle = true };
        ayar.Save(dosya);
        var geri = PlayerSettings.Load(dosya);
        var ilk = geri.ScreenshotPath(Path.Combine(klasor, "film.mkv"), 3725.5);
        File.WriteAllText(ilk, "");
        var ikinci = geri.ScreenshotPath(Path.Combine(klasor, "film.mkv"), 3725.5);
        var bozuk = Path.Combine(klasor, "bozuk.json");
        File.WriteAllText(bozuk, "{");

        GorunumKanit.Write("oynatici-ayarlari.txt", File.ReadAllText(dosya) + $"\nilk: {Path.GetFileName(ilk)}\nikinci: {Path.GetFileName(ikinci)}\n");
        Assert.Equal(klasor, geri.ScreenshotFolder);
        Assert.Equal(RepeatMode.One, geri.Repeat);
        Assert.True(geri.Shuffle);
        Assert.Equal("film_01-02-05-500.png", Path.GetFileName(ilk));
        Assert.Equal("film_01-02-05-500_2.png", Path.GetFileName(ikinci));
        Assert.Equal(RepeatMode.Off, PlayerSettings.Load(bozuk).Repeat);
    }
}
