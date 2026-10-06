using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Küçült sekmesinin "Gözat" seçicisi çoklu seçime açık. Tek dosya eskisi gibi süzülmeden pencereye
/// yüklenir; birden çok dosya bırakmanın süzgecinden geçip bırakmayla aynı kuyruk koluna gider. Seçici
/// testte açılmaz: yollar <see cref="MainWindow.SourcePicker"/> dikişinden, kuyruğa giden liste
/// <see cref="MainWindow.BatchOpener"/> dikişinden, yüklenen dosya sahte yoklayıcıdan okunur. Kuyruk
/// penceresi açılmaz, ffprobe ve ffmpeg koşmaz.
/// </summary>
public sealed class GozatCokluSecimTests : IDisposable
{
    private readonly string _klasor = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        ".calisma", "test-ciktilari", "gozat-coklu", Guid.NewGuid().ToString("N")[..8]));

    public GozatCokluSecimTests() => Directory.CreateDirectory(_klasor);

    public void Dispose()
    {
        try { Directory.Delete(_klasor, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Dosya(string ad)
    {
        var yol = Path.Combine(_klasor, ad);
        File.WriteAllText(yol, "x");
        return yol;
    }

    private static void Dongu(Func<bool> bitti, double saniye)
    {
        var saat = Stopwatch.StartNew();
        while (!bitti() && saat.Elapsed.TotalSeconds < saniye)
        {
            using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(5));
            Dispatcher.UIThread.MainLoop(dilim.Token);
        }
    }

    private sealed record Olcu(int SeciciAcildi, string[] Yuklenen, string[][] Kuyruklar, bool[] Coklu);

    /// <summary>
    /// Pencereyi üç dikişle kurar, <paramref name="eylem"/>i koşturur. Yoklayıcı yolu yazıp düşer:
    /// yükleme kolunun çağrıldığı görülür, karmaşıklık ölçümü (ffmpeg) hiç başlamaz.
    /// </summary>
    private Olcu Kos(IReadOnlyList<string> secilen, Func<MainWindow, Task?> eylem, bool isKosuyor = false)
    {
        var ayar = Path.Combine(_klasor, "ayar", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(ayar)!);
        return AppHost.Run(() =>
        {
            var window = new MainWindow { SettingsPathOverride = ayar };
            var acildi = 0;
            var yuklenen = new List<string>();
            var kuyruklar = new List<string[]>();
            var kipler = new List<bool>();
            try
            {
                window.SourcePicker = coklu => { acildi++; kipler.Add(coklu); return Task.FromResult(secilen); };
                window.BatchOpener = yollar => kuyruklar.Add(yollar.ToArray());
                window.Prober = (yol, _) => { yuklenen.Add(yol); return Task.FromException<MediaInfo>(new IOException("sahte yoklama")); };
                if (isKosuyor) window.BeginEncodingForTest();

                var is_ = eylem(window);
                if (is_ is not null) Dongu(() => is_.IsCompleted, 30);
                else Dongu(() => false, 0.2);
                if (is_ is { IsFaulted: true }) is_.GetAwaiter().GetResult();
                return new Olcu(acildi, yuklenen.ToArray(), kuyruklar.ToArray(), kipler.ToArray());
            }
            finally
            {
                if (isKosuyor) window.EndEncodingForTest();
                window.Close();
            }
        });
    }

    private static Task? Birak(MainWindow window, params string[] yollar)
    {
        var aktarim = new DataTransfer();
        foreach (var yol in yollar) aktarim.Add(DataTransferItem.CreateFile(ParcaKanit.Dosya(yol)));
        window.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, aktarim, window, new Point(1, 1), KeyModifiers.None));
        return null;
    }

    /// <summary>Tek seçim bugünkü gibi: uzantısına bakılmadan bu pencereye yüklenir, kuyruk açılmaz.</summary>
    [Fact]
    public void TekSecimSuzulmedenBuPencereyeYuklenir()
    {
        var metin = Dosya("tek.txt");
        var olcu = Kos(new[] { metin }, window => window.BrowseAsync(true));

        Assert.Equal(1, olcu.SeciciAcildi);
        Assert.Equal(new[] { metin }, olcu.Yuklenen);
        Assert.Empty(olcu.Kuyruklar);
    }

    /// <summary>Birden çok seçim kuyruğa gider: yalnız videolar, ad sırasında; hiçbiri bu pencereye yüklenmez.</summary>
    [Fact]
    public void CokluSecimSuzulupKuyrugaGider()
    {
        var b = Dosya("b.mkv");
        var metin = Dosya("notlar.txt");
        var a = Dosya("a.mp4");
        var olcu = Kos(new[] { b, metin, a }, window => window.BrowseAsync(true));

        Assert.Equal(new[] { a, b }, Assert.Single(olcu.Kuyruklar));
        Assert.Empty(olcu.Yuklenen);
    }

    /// <summary>
    /// Bırakmayla aynı sınır: süzgeçten tek video kalan ya da hiç kalmayan çoklu seçim ne kuyruk açar
    /// ne yükler. Olumlu kontrol aynı ölçünün iki videolu kolu, <see cref="CokluSecimSuzulupKuyrugaGider"/>.
    /// </summary>
    [Fact]
    public void SuzgectenTekVideoKalanCokluSecimHicbirSeyAcmaz()
    {
        var a = Dosya("a.mp4");
        var metin = Dosya("notlar.txt");
        var belge = Dosya("belge.pdf");

        var tek = Kos(new[] { a, metin }, window => window.BrowseAsync(true));
        var hic = Kos(new[] { metin, belge }, window => window.BrowseAsync(true));
        var bos = Kos(Array.Empty<string>(), window => window.BrowseAsync(true));

        foreach (var olcu in new[] { tek, hic, bos })
        {
            Assert.Equal(1, olcu.SeciciAcildi);
            Assert.Empty(olcu.Kuyruklar);
            Assert.Empty(olcu.Yuklenen);
        }
    }

    /// <summary>İş koşarken Gözat hiçbir şey yapmaz: seçici bile açılmaz. Olumlu kontrol iş yokken aynı seçim.</summary>
    [Fact]
    public void IsKosarkenSeciciAcilmaz()
    {
        var a = Dosya("a.mp4");
        var b = Dosya("b.mp4");

        var kosarken = Kos(new[] { a, b }, window => window.BrowseAsync(true), isKosuyor: true);
        var bosta = Kos(new[] { a, b }, window => window.BrowseAsync(true));

        Assert.Equal(0, kosarken.SeciciAcildi);
        Assert.Empty(kosarken.Kuyruklar);
        Assert.Empty(kosarken.Yuklenen);
        Assert.Equal(1, bosta.SeciciAcildi);
        Assert.Single(bosta.Kuyruklar);
    }

    /// <summary>
    /// Küçült sekmesinin Gözat düğmesi seçiciyi çoklu seçimle açar ve aynı yoldan geçer. Dönüştür
    /// sekmesinin düğmesi tek seçimli kalır (orada kuyruk yok) ve tek dosyayı eskisi gibi yükler.
    /// </summary>
    [Fact]
    public void KucultDugmesiCokluDonusturDugmesiTekSecimle()
    {
        var a = Dosya("a.mp4");
        var b = Dosya("b.mp4");
        var kucult = Kos(new[] { a, b }, window =>
        {
            window.BtnBrowseEmpty.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return null;
        });
        var donustur = Kos(new[] { a }, window =>
        {
            window.BtnBrowseConvert.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return null;
        });

        Assert.Equal(new[] { true }, kucult.Coklu);
        Assert.Equal(new[] { a, b }, Assert.Single(kucult.Kuyruklar));
        Assert.Equal(new[] { false }, donustur.Coklu);
        Assert.Equal(new[] { a }, donustur.Yuklenen);
        Assert.Empty(donustur.Kuyruklar);
    }

    /// <summary>
    /// Bırakma ile Gözat aynı koldan geçer: aynı dosyalar bırakılınca kuyruğa giden liste Gözat'ınkiyle
    /// aynı, tek dosya bırakılınca o da bu pencereye yüklenir. Seçici bırakmada açılmaz.
    /// </summary>
    [Fact]
    public void BirakmaIleGozatAyniKuyruguKurar()
    {
        var b = Dosya("b.mkv");
        var metin = Dosya("notlar.txt");
        var a = Dosya("a.mp4");

        var gozat = Kos(new[] { b, metin, a }, window => window.BrowseAsync(true));
        var birakma = Kos(Array.Empty<string>(), window => Birak(window, b, metin, a));
        var tekBirakma = Kos(Array.Empty<string>(), window => Birak(window, a));

        Assert.Equal(0, birakma.SeciciAcildi);
        Assert.Equal(Assert.Single(gozat.Kuyruklar), Assert.Single(birakma.Kuyruklar));
        Assert.Empty(birakma.Yuklenen);
        Assert.Equal(new[] { a }, tekBirakma.Yuklenen);
        Assert.Empty(tekBirakma.Kuyruklar);
    }

    /// <summary>
    /// Seçici testte açılamadığı için çoklu seçim bayrağı kaynaktan okunur; Gözat'ın ve bırakmanın
    /// kuyruğu ortak koldan açtığı da: <c>OpenBatch</c> ana pencerede yalnız o kolda çağrılır.
    /// </summary>
    [Fact]
    public void SeciciCokluSecimeAcikVeKuyrukTekKoldan()
    {
        var kaynak = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "MainWindow.axaml.cs"));

        var secici = Regex.Match(kaynak, @"Task<IReadOnlyList<string>> PickSourcesAsync\(bool multiple\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline);
        Assert.True(secici.Success);
        Assert.Contains("AllowMultiple = multiple", secici.Groups["body"].Value, StringComparison.Ordinal);

        var birakma = Regex.Match(kaynak, @"void OnDrop\(object\? sender, DragEventArgs e\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline);
        Assert.True(birakma.Success);
        Assert.Contains("OpenSourcesAsync(file, batch)", birakma.Groups["body"].Value, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(kaynak, @"OpenBatch\(\w+\)\.Show\(\)"));
    }
}
