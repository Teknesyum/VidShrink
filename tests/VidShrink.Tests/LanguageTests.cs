using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Performance;

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace VidShrink.Tests;

/// <summary>
/// Kaynaktaki dil dosyalarının okuyucusu. Ölçümler çalışan uygulamanın kopyasına değil
/// depodaki dosyaya bakar: çıktıya kopyalanmayan bir düzeltme yeşil vermesin.
/// </summary>
internal static class Locales
{
    internal static readonly string Folder =
        Path.Combine(TipSources.Root, "src", "VidShrink.App", "Locales");

    internal static readonly string[] Domains = { "main", "playback", "performance", "settings" };

    internal static IReadOnlyList<string> Languages =>
        Directory.GetDirectories(Folder).Select(path => Path.GetFileName(path)!).OrderBy(name => name).ToList();

    internal static IReadOnlyDictionary<string, string> Domain(string language, string domain)
        => JsonSerializer.Deserialize<Dictionary<string, string>>(
               File.ReadAllText(Path.Combine(Folder, language, domain + ".json")))
           ?? new Dictionary<string, string>();

    /// <summary>Bir dilin bütün alanları tek sözlükte; hangi anahtarın hangi dosyadan geldiğiyle.</summary>
    internal static IReadOnlyDictionary<string, (string Domain, string Value)> Read(string language)
    {
        var all = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        foreach (var domain in Domains)
            foreach (var (key, value) in Domain(language, domain))
                all[key] = (domain, value);

        return all;
    }

    internal static IReadOnlyDictionary<string, string> Values(string language)
        => Read(language).ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal);

    /// <summary>
    /// İngilizce metnin anahtarı üzerinden Türkçesi. Sözlük artık İngilizceyle anahtarlanmıyor,
    /// bu yüzden eşleştirme değerden yapılıyor; iki anahtar aynı İngilizceyi taşıyorsa ölçüm
    /// bunu söyler, sessizce birini seçmez.
    /// </summary>
    internal static string TurkishFor(string english)
    {
        var keys = Values("en")
            .Where(pair => string.Equals(pair.Value, english, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToList();

        if (keys.Count != 1)
            throw new InvalidOperationException($"\"{english}\" için {keys.Count} anahtar bulundu.");

        return Values("tr")[keys[0]];
    }
}

/// <summary>
/// T83: ekrandaki her metin anahtardan gelir. Ölçümler üç soruyu ayrı ayrı soruyor —
/// kaynakta gömülü metin kaldı mı, Türkçe eksik mi, ve dil düğmesi gerçekten her şeyi
/// çeviriyor mu.
/// </summary>
public sealed class LanguageTests : IDisposable
{
    public LanguageTests() => Strings.Reset();

    public void Dispose() => Strings.Reset();

    // ---- K2: gömülü metin kalmıyor -------------------------------------------------

    /// <summary>
    /// Biçimlemede kullanıcıya çıkan bir öznitelik ya bir bağdır (<c>{...}</c> ile başlar)
    /// ya da harf içermez. Kaçınılmaz olanlar tek tek adıyla yazılı: marka parçaları,
    /// birim ve kısaltma adları. Örüntüyle toptan muafiyet yok — listeye bir cümle
    /// eklendiği anda gözle görünür.
    /// </summary>
    private static readonly string[] XamlNamesThatStayAsWritten =
    {
        "Vid", "Shrink", "Buy Me a Coffee", "Teknesyum",
        "MB", "CRF", "1280x720",
        "MP4", "MKV", "WebM", "MOV", "AVI", "GIF", "MP3", "M4A", "WAV",
        "H.264", "H.265", "VP9", "AV1", "AAC", "Opus", "PCM"
    };

    private static readonly Regex ScreenAttribute = new(
        "(?:^|\\s)((?:\\w+:)?(?:Text|Content|Header|ToolTip\\.Tip|Watermark|AutomationProperties\\.Name|Bullets\\.Text))=\"([^\"]*)\"",
        RegexOptions.Compiled);

    private static readonly Regex AnyLetter = new(@"\p{L}", RegexOptions.Compiled);

    [Fact]
    public void BicimlemedeKullaniciyaGorunenDuzMetinKalmadi()
    {
        var xaml = File.ReadAllText(TipSources.WindowXamlPath);
        var stray = new List<string>();

        foreach (Match match in ScreenAttribute.Matches(xaml))
        {
            var value = TipSources.DecodeXml(match.Groups[2].Value);
            if (value.StartsWith('{')) continue;
            if (!AnyLetter.IsMatch(value)) continue;
            if (XamlNamesThatStayAsWritten.Contains(value, StringComparer.Ordinal)) continue;

            stray.Add($"{match.Groups[1].Value}=\"{value}\"");
        }

        Assert.True(stray.Count == 0,
            "MainWindow.axaml içinde anahtardan gelmeyen metin var:\n" + string.Join("\n", stray));
    }

    /// <summary>
    /// Arka kodda ekrana çıkan her metin bir sözlük çağrısından gelir. Geriye kalan
    /// İngilizce dizgeler ekran metni değil <b>motorun kimlikleri</b>: ffmpeg'in hata
    /// satırında aranan iğneler ve ilerleme satırındaki aşama sözcükleri. Hepsi burada
    /// adıyla sayılı.
    /// </summary>
    private static readonly string[] CodeNamesThatAreEngineTokens =
    {
        "GIF palette", "GIF encode",
        "no space left", "not enough space", "disk full", "insufficient disk space",
        "unknown encoder", "encoder not found", "does not support", "could not write header",
        "error initializing output stream", "automatic encoder selection failed",
        "incorrect codec parameters", "invalid argument", "muxer does not support",
        "invalid data found", "moov atom not found", "could not find codec parameters",
        "decoder not found", "no such file or directory", "end of file", "unknown format"
    };

    private static readonly Regex CsLiteral = new("(?<!\\$)\"((?:[^\"\\\\\n]|\\\\.)*)\"", RegexOptions.Compiled);
    private static readonly Regex CsWord = new(@"\p{L}{3,}", RegexOptions.Compiled);

    public static TheoryData<string> ScannedCode()
    {
        var app = Path.Combine(TipSources.Root, "src", "VidShrink.App");
        var data = new TheoryData<string> { Path.Combine(app, "MainWindow.axaml.cs") };
        foreach (var file in Directory.GetFiles(Path.Combine(app, "Performance"), "*.cs")) data.Add(file);
        return data;
    }

    [Theory]
    [MemberData(nameof(ScannedCode))]
    public void ArkaKoddaCumleKalmadi(string path)
    {
        var relative = Path.GetFileName(path);
        var source = Strip(File.ReadAllText(path));
        var stray = new List<string>();

        foreach (Match match in CsLiteral.Matches(source))
        {
            var value = TipSources.Unescape(match.Groups[1].Value);
            if (!value.Contains(' ')) continue;
            if (CsWord.Matches(value).Count < 2) continue;
            if (CodeNamesThatAreEngineTokens.Contains(value, StringComparer.Ordinal)) continue;

            stray.Add(value);
        }

        Assert.True(stray.Count == 0,
            $"{relative} içinde anahtardan gelmeyen cümle var:\n" + string.Join("\n", stray.Distinct()));
    }

    /// <summary>
    /// Taramadan düşenler: yorumlar ve <c>throw</c> ifadeleri. İkincisi ekrana çıkmaz —
    /// geliştiriciye bir kodun karşılıksız kaldığını söyleyen kesme iletisidir.
    /// </summary>
    private static string Strip(string source)
    {
        source = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        source = Regex.Replace(source, @"//[^\n]*", string.Empty);
        return Regex.Replace(source, @"throw new [^;]*;", string.Empty, RegexOptions.Singleline);
    }

    // ---- K3: Türkçe eksiksiz -------------------------------------------------------

    /// <summary>
    /// Gerçekten iki dilde aynı olan girdiler. Marka ve ürün adları çevrilmez; listeye
    /// yeni bir ad eklemek tek tek karar vermeyi gerektirir, örüntü yok.
    /// </summary>
    private static readonly string[] SameInEveryLanguage =
    {
        "VidShrink", "FFmpeg", ".NET",
        "{0} ms"
    };

    [Fact]
    public void HerAnahtarinTurkcesiIngilizcesindenFarkli()
    {
        var english = Locales.Read("en");
        var turkish = Locales.Values("tr");
        var complaints = new StringBuilder();

        foreach (var (key, (domain, value)) in english)
        {
            Assert.True(turkish.TryGetValue(key, out var written), $"'{key}' Türkçede yok.");
            if (!string.Equals(value, written, StringComparison.Ordinal)) continue;
            if (SameInEveryLanguage.Contains(value.Trim(), StringComparer.Ordinal)) continue;

            complaints.AppendLine($"{domain}: '{key}' iki dilde aynı — \"{value}\"");
        }

        Assert.True(complaints.Length == 0, "Türkçesi yazılmamış anahtarlar:\n" + complaints);
    }

    // ---- K7: üçüncü dil kod değişmeden ---------------------------------------------

    private readonly record struct Reading(IReadOnlyList<string> Offered, string Chosen);

    [Fact]
    public void UcuncuDilKlasoruKopyalayinca_UygulamaOnuListelerVeSecebilir()
    {
        var sandbox = Path.Combine(TestPaths.OutputRoot, "dil", Guid.NewGuid().ToString("N"));

        foreach (var language in new[] { "en", "tr" })
        {
            Directory.CreateDirectory(Path.Combine(sandbox, language));
            foreach (var domain in Locales.Domains)
                File.Copy(
                    Path.Combine(Locales.Folder, language, domain + ".json"),
                    Path.Combine(sandbox, language, domain + ".json"));
        }

        // Üçüncü dil: İngilizcenin kopyası, her değerin başına bir işaret. Kodda hiçbir
        // yerde "zz" yazmıyor — klasörün varlığı yeterli olmalı.
        Directory.CreateDirectory(Path.Combine(sandbox, "zz"));
        foreach (var domain in Locales.Domains)
        {
            var texts = Locales.Domain("en", domain)
                .ToDictionary(pair => pair.Key, pair => "zz " + pair.Value, StringComparer.Ordinal);
            texts["main.language.name"] = "Zzyzx";
            File.WriteAllText(
                Path.Combine(sandbox, "zz", domain + ".json"),
                JsonSerializer.Serialize(texts, new JsonSerializerOptions { WriteIndented = true }));
        }

        try
        {
            Strings.UseRoot(sandbox);
            Assert.Contains("zz", Strings.Languages, StringComparer.OrdinalIgnoreCase);

            var read = AppHost.Run(() =>
            {
                var window = new MainWindow();
                Relayout(window, new Size(1400, 1000));

                var buttons = window.GetVisualDescendants().OfType<Button>()
                    .Where(button => button.Tag is string tag && Strings.Languages.Contains(tag, StringComparer.Ordinal))
                    .Select(button => button.Content?.ToString() ?? string.Empty)
                    .ToList();

                var third = window.GetVisualDescendants().OfType<Button>()
                    .Single(button => (button.Tag as string) == "zz");
                third.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

                return new Reading(buttons, window.TxtDropTitle.Text ?? string.Empty);
            });

            var (offered, chosen) = (read.Offered, read.Chosen);

            Assert.Contains("Zzyzx", offered);
            Assert.Equal("zz", Strings.Language);
            Assert.StartsWith("Zz ", chosen, StringComparison.Ordinal);
        }
        finally
        {
            Strings.Reset();
            try { Directory.Delete(sandbox, recursive: true); } catch (IOException) { }
        }
    }

    // ---- K4: kullanıcının saydığı dört cümle ---------------------------------------

    /// <summary>
    /// Kalite bölümündeki kaynak-yok uyarısı. Ölçü sözlüğe değil <b>ekrana</b> bakıyor:
    /// pencere Türkçeyken bloğun kendi metni okunuyor.
    /// </summary>
    [Fact]
    public void KaliteUyarisiEkrandaTurkce()
    {
        var shown = OnScreen(window => window.TxtQualityTargetNotice.Text ?? string.Empty);

        Assert.Equal(Cased("tr", "main.quality.no-source"), shown);
        Assert.DoesNotContain("Load A Video", shown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ManşetOlculmediginde_EkrandaTurkce()
    {
        var shown = PerformanceLines();

        Assert.Contains(Cased("tr", "performance.headline.not-enough"), shown);
        Assert.DoesNotContain(shown, line => line.Contains("enough measured here", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HicOlculmediSatiri_EkrandaTurkce()
    {
        var shown = PerformanceLines();

        Assert.Contains(Cased("tr", "performance.line.not-measured"), shown);
        Assert.DoesNotContain(shown, line => line.Contains("on this machine yet", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SinirCumlesi_EkrandaTurkce()
    {
        var shown = PerformanceLines();

        Assert.Contains(Cased("tr", "performance.boundary"), shown);
        Assert.DoesNotContain(shown, line => line.Contains("does not capture video", StringComparison.OrdinalIgnoreCase));
    }

    // ---- K5: dil düğmesi her şeyi çeviriyor ----------------------------------------

    /// <summary>
    /// Pencere açılır, tr → en → tr yapılır ve her turda <b>bütün</b> sekmelerin görünen
    /// metin düğümleri toplanır. Ölçüt: hiçbir düğüm öteki dilin metnini taşımıyor.
    /// Ağaç gezen çeviri kalktığı için bu ancak bağlar gerçekten tazelenirse yeşil verir.
    /// </summary>
    [Fact]
    public void DilDegisinceEkrandaOtekiDilinMetniKalmiyor()
    {
        var (first, second, third) = AppHost.Run(() =>
        {
            var window = new MainWindow();
            window.UseTurkish();

            var a = Sweep(window);
            Strings.Use("en");
            var b = Sweep(window);
            Strings.Use("tr");
            var c = Sweep(window);

            return (a, b, c);
        });

        AssertNoForeignText("tr", first);
        AssertNoForeignText("en", second);
        AssertNoForeignText("tr", third);
    }

    private static void AssertNoForeignText(string language, IReadOnlyCollection<string> shown)
    {
        var other = string.Equals(language, "tr", StringComparison.Ordinal) ? "en" : "tr";
        var mine = Locales.Values(language);
        var theirs = Locales.Values(other);

        var foreign = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, value) in theirs)
        {
            // Dil düğmesi kasten öteki dilin adını taşıyor: her dil kendi adını yazar.
            if (key == "main.language.name") continue;
            if (mine.TryGetValue(key, out var same) && string.Equals(same, value, StringComparison.Ordinal)) continue;
            foreign.Add(LanguageCatalog.Title(value, !string.Equals(language, "tr", StringComparison.Ordinal)));
        }

        var caught = shown.Where(foreign.Contains).Distinct().ToList();

        Assert.True(caught.Count == 0,
            $"Dil '{language}' iken ekranda öteki dilin metni duruyor:\n" + string.Join("\n", caught));
    }

    /// <summary>Bütün sekmeleri sırayla açıp görünen metin düğümlerini toplar.</summary>
    private static IReadOnlyList<string> Sweep(MainWindow window)
    {
        var size = new Size(1400, 1000);
        var texts = new List<string>();
        Relayout(window, size);
        var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();

        for (var index = 0; index < tabs.ItemCount; index++)
        {
            tabs.SelectedIndex = index;
            Relayout(window, size);

            texts.AddRange(window.GetVisualDescendants().OfType<TextBlock>()
                .Where(block => block.IsEffectivelyVisible)
                .Select(block => block.Text ?? string.Empty));

            texts.AddRange(window.GetVisualDescendants().OfType<ContentControl>()
                .Select(control => control.Content as string ?? string.Empty));
        }

        tabs.SelectedIndex = 0;
        Relayout(window, size);

        return texts.Where(text => text.Length > 0).ToList();
    }

    // ---- ortak düzenek --------------------------------------------------------------

    private static string Cased(string language, string key)
        => LanguageCatalog.Title(
            Locales.Values(language)[key],
            string.Equals(language, "tr", StringComparison.Ordinal));

    private static T OnScreen<T>(Func<MainWindow, T> read) =>
        AppHost.Run(() =>
        {
            var window = new MainWindow();
            window.UseTurkish();
            Relayout(window, new Size(1400, 1000));
            return read(window);
        });

    /// <summary>
    /// Başarım paneli Gelişmiş sekmesinde. Sekme adıyla değil <b>içeriğiyle</b> bulunuyor:
    /// ad artık dile göre değişiyor ve ölçüm o adı bilmek zorunda değil.
    /// </summary>
    private static IReadOnlyList<string> PerformanceLines() =>
        OnScreen(window =>
        {
            var size = new Size(1400, 1000);
            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();

            for (var index = 0; index < tabs.ItemCount; index++)
            {
                tabs.SelectedIndex = index;
                Relayout(window, size);

                var panel = window.GetVisualDescendants().OfType<Border>()
                    .FirstOrDefault(border => border.Name == "PerformancePanel");

                if (panel is not null)
                    return (IReadOnlyList<string>)panel.GetVisualDescendants().OfType<TextBlock>()
                        .Select(block => block.Text ?? string.Empty)
                        .ToList();
            }

            throw new InvalidOperationException("PerformancePanel hiçbir sekmede bulunamadı.");
        });

    /// <summary>
    /// Başsız ölçümde giriş canlandırması <see cref="Visual.TranslatePoint"/> sonucunu
    /// yalancı yapıyor. Ölçüm onu ortamdan devralmıyor: geçişten önce her panelin
    /// dönüşümü burada sıfırlanıyor.
    /// </summary>
    private static void Relayout(MainWindow window, Size size)
    {
        window.Width = double.NaN;
        window.Height = double.NaN;

        window.Measure(size);
        window.Arrange(new Rect(size));
        window.UpdateLayout();
        window.SettleFades();

        foreach (var node in window.GetVisualDescendants().OfType<Layoutable>()) node.InvalidateMeasure();

        var root = (Layoutable)window.GetVisualChildren().Single();
        root.InvalidateMeasure();
        root.Measure(size);
        root.Arrange(new Rect(size));

        foreach (var node in window.GetVisualDescendants().OfType<Visual>())
            node.RenderTransform = null;
    }
}
