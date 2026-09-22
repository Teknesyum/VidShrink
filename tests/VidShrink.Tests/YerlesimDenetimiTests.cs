using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Başsız yerleşim denetçisi. Pencere dil × boyut kolunda açılır, her sekme (gizli Gelişmiş
/// dahil) seçilir, katlanır bölümler açılır ve ağacın tamamı dört soruya karşı taranır:
///
/// <list type="bullet">
/// <item><b>kesik</b> — tek satırlık metnin doğal genişliği kendi yerinden büyük, çok satırlı
/// metnin yüksekliği kutusuna sığmıyor, ya da metin <c>ClipToBounds</c> açık bir atanın
/// dışına düşüyor. Üç noktayla kısalıp tam halini balonda taşıyan metin T194 kararı gereği
/// kesik sayılmaz, ayrı "balonlu" satırına yazılır.</item>
/// <item><b>çakışma</b> — aynı panelin görünür iki kardeşi 1 pikselden fazla kesişiyor.</item>
/// <item><b>taşma</b> — çocuk ebeveyninin sınırından dışarı çıkıyor.</item>
/// <item><b>şerit</b> — sayfa üst şeridin altına giriyor; kaydırılan içerik saydam sekme
/// düğmelerinin arasından görünüyor.</item>
/// <item><b>başlık</b> — yalnız tr ve en: başlık, etiket ve düğme yazısında küçük harfle
/// başlayan içerik sözcüğü (Title Case kuralı; diğer dillerin kendi büyük harf kuralı var).</item>
/// </list>
///
/// <para>Dikdörtgenler <see cref="Visual.TranslatePoint"/> ile değil <see cref="Visual.Bounds"/>
/// zinciriyle bulunur: giriş canlandırmasının <c>translateY</c>'si başsız koşumda hiç geri
/// alınmıyor ve <c>TranslatePoint</c> onu hesaba kattığı için yalan söylüyor. Kardeşler aynı
/// ebeveynin uzayında durduğundan <c>Bounds</c> kesişimi doğrudan karşılaştırılabilir.
/// Pencereye ayrıca <c>reduced-motion</c> sınıfı verilir ve dönüşümler silinir.</para>
///
/// <para>Dil açıkça <see cref="Strings.Use"/> ile kurulur ve sekme başlığından geri okunur;
/// başsız pencere <c>OnWindowLoaded</c> görmediği için kendiliğinden İngilizce kalıyordu.</para>
/// </summary>
public sealed class YerlesimDenetimiTests
{
    private readonly ITestOutputHelper _output;

    public YerlesimDenetimiTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Türkçe, İngilizce ve dil dosyalarındaki metinlerin karakter toplamına göre en uzun üç
    /// dil (it 49981, el 49937, fr 49436; en 42610, tr 40806 — 2026-09-22 sayımı).
    /// </summary>
    internal static readonly string[] Diller = { "tr", "en", "it", "el", "fr" };

    private const string SamplePath = @"C:\Kayitlar\tatil-cekimi-2160p60.mkv";

    /// <summary>
    /// Bilerek üst üste duran katmanlar. Ad → gerekçe. Ad, çakışan çiftin ebeveyn panelinin
    /// adıdır; kural adsız <see cref="Panel"/> türünü zaten katman sayar.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> KatmanIstisnalari =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PART_ItemsPresenter"] = "sekme şeridi TabControl şablonunda içerikle aynı gözde katman (Kesit B)",
        };

    /// <summary>
    /// Başlık kuralının dışında bilerek kalan dil anahtarları. Anahtar → gerekçe.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> BaslikIstisnalari =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["shell.menu.open"] = "Windows sağ tık menüsünün etiketi; kurucu ve ShellMenu aynı metni yazar, kabuk menüsü cümle düzeninde",
        };

    private sealed record Kusur(string Tur, string Sekme, string Yer, string Ayrinti)
    {
        public override string ToString() => $"{Tur} · {Sekme} · {Yer} · {Ayrinti}";
    }

    private sealed class Denetim
    {
        public List<Kusur> Kusurlar { get; } = new();
        public List<string> Balonlu { get; } = new();
        public HashSet<string> Gorulen { get; } = new(StringComparer.Ordinal);
        public int Metin { get; set; }
        public int Panel { get; set; }
        public int Denetim_ { get; set; }
        public int Baslik { get; set; }
        public int Sekme { get; set; }
        public string Dil { get; set; } = "";
        public HashSet<Visual> Disarida { get; } = new();

        /// <summary>
        /// Görünür ve seçili sekmenin ağacında. Geçiş denetimi önceki sayfayı canlandırma
        /// bitene dek ağaçta tutuyor; başka sekmenin sayfası bu sekmenin kusuru sayılmaz.
        /// </summary>
        public bool Kapsamda(Visual gorsel)
        {
            if (!gorsel.IsShown()) return false;
            for (Visual? d = gorsel; d is not null; d = d.GetVisualParent())
                if (Disarida.Contains(d)) return false;
            return true;
        }

        public void Ekle(Kusur kusur)
        {
            if (Gorulen.Add(kusur.Tur + "|" + kusur.Yer + "|" + kusur.Ayrinti)) Kusurlar.Add(kusur);
        }
    }

    public static TheoryData<string, bool> Kollar()
    {
        var kollar = new TheoryData<string, bool>();
        foreach (var dil in Diller)
            foreach (var dar in new[] { true, false })
                kollar.Add(dil, dar);
        return kollar;
    }

    [Theory]
    [MemberData(nameof(Kollar))]
    public void SekmelerdeKesikCakismaTasmaYok(string dil, bool dar)
    {
        var (denetim, boyut) = Ac(dil, dar, null);
        var klasor = Path.Combine(TipSources.Root, ".calisma", "yerlesim-denetimi");
        var ad = $"{dil}-{(dar ? "taban" : "varsayilan")}.txt";
        Directory.CreateDirectory(klasor);

        var dokum = new StringBuilder()
            .AppendLine($"{dil} {boyut.Width:0}x{boyut.Height:0}: sekme {denetim.Sekme}, metin {denetim.Metin}, panel {denetim.Panel}, denetim {denetim.Denetim_}, baslik {denetim.Baslik}")
            .AppendLine($"kusur {denetim.Kusurlar.Count}")
            .AppendJoin(Environment.NewLine, denetim.Kusurlar).AppendLine()
            .AppendLine($"balonlu {denetim.Balonlu.Count}")
            .AppendJoin(Environment.NewLine, denetim.Balonlu).AppendLine()
            .ToString();
        File.WriteAllText(Path.Combine(klasor, ad), dokum);
        _output.WriteLine(dokum);

        Assert.True(denetim.Sekme >= 6, $"yalnız {denetim.Sekme} sekme dolaşıldı");
        Assert.True(denetim.Metin > 300, $"yalnız {denetim.Metin} metin ölçüldü");
        Assert.True(denetim.Panel > 100, $"yalnız {denetim.Panel} panel ölçüldü");
        Assert.True(denetim.Kusurlar.Count == 0, dokum);

        KanitKapanisi.Kapat(klasor, ad);
    }

    /// <summary>
    /// Olumlu kontrol: denetçi kör değil. Pencere tabanın altındaki bir genişlikte (600 px)
    /// açılır; sarmasız bir etiket bilerek dar bir kutuya konur, bir yığın kardeşi eksi kenar
    /// payıyla öncekinin üstüne itilir ve bir çocuk ebeveyninden geniş yapılır. Üç tür de
    /// adıyla yakalanmalı.
    /// </summary>
    [Fact]
    public void DenetciBilinenKusurlariYakalar()
    {
        var (denetim, _) = Ac("tr", true, pencere =>
        {
            var dil = pencere.GetVisualDescendants().OfType<TextBlock>()
                .First(b => b.Text == LanguageCatalog.Display(Strings.Get("settings-tab.general.title")) || b.Text == Strings.Get("settings-tab.general.title"));
            dil.Tag = "OlumluKesik";
            dil.Width = 12;

            var yigin = pencere.FindControl<StackPanel>("AboutStatus")!;
            var ikinci = (Control)yigin.Children[1];
            ikinci.Tag = "OlumluCakisma";
            ikinci.Margin = new Thickness(0, -30, 0, 0);

            var genis = (Control)yigin.Children[2];
            genis.Tag = "OlumluTasma";
            genis.Width = 5000;
        }, new Size(600, 720));

        var metin = string.Join(Environment.NewLine, denetim.Kusurlar);
        _output.WriteLine(metin);

        Assert.Contains(denetim.Kusurlar, k => k.Tur == "kesik" && k.Yer.Contains("OlumluKesik", StringComparison.Ordinal));
        Assert.Contains(denetim.Kusurlar, k => k.Tur == "çakışma" && k.Yer.Contains("OlumluCakisma", StringComparison.Ordinal));
        Assert.Contains(denetim.Kusurlar, k => k.Tur == "taşma" && k.Yer.Contains("OlumluTasma", StringComparison.Ordinal));
    }

    /// <summary>
    /// Başlık denetiminin olumlu kontrolü: küçük harfle yazılmış içerik sözcüğü yakalanır,
    /// bağlaç ve birim yakalanmaz.
    /// </summary>
    [Theory]
    [InlineData("tr", "Ne çıkacak", true)]
    [InlineData("tr", "Kalite ve Uyumluluk", false)]
    [InlineData("tr", "OpenSubtitles Hesabı (İndirme İçin Gerekli)", false)]
    [InlineData("tr", "Ses Hedefi (kbps)", false)]
    [InlineData("en", "OpenSubtitles account (needed for downloads)", true)]
    [InlineData("en", "OpenSubtitles Account (Needed for Downloads)", false)]
    [InlineData("tr", "Arayüz dilini değiştirir.", false)]
    public void BaslikKuraliKucukSozcuguYakalar(string dil, string metin, bool kusurlu)
        => Assert.Equal(kusurlu, KucukSozcuk(metin, dil) is not null);

    private static (Denetim, Size) Ac(string dil, bool dar, Action<MainWindow>? boz, Size? zorla = null) =>
        AppHost.Run(() =>
        {
            Strings.Use(dil);
            var pencere = new MainWindow();
            try
            {
                pencere.Classes.Add("reduced-motion");
                pencere.LoadWithoutProbing(SamplePath, Sample());
                pencere.SettleFades();
                pencere.TabAdvanced.IsVisible = true;

                var boyut = zorla ?? (dar
                    ? new Size(pencere.MinWidth, pencere.MinHeight)
                    : new Size(Belirtec(pencere, "WindowPreferredWidth"), Belirtec(pencere, "WindowPreferredHeight")));

                var denetim = new Denetim { Dil = dil };
                Tara(pencere, boyut, denetim, boz);
                return (denetim, boyut);
            }
            finally
            {
                pencere.Close();
                Strings.Use("en");
            }
        });

    private static double Belirtec(MainWindow pencere, string anahtar)
    {
        Assert.True(pencere.TryFindResource(anahtar, out var deger), $"{anahtar} belirteci yok");
        return (double)deger!;
    }

    private static MediaInfo Sample() => new()
    {
        FilePath = SamplePath,
        FileSizeBytes = 420L * 1024 * 1024,
        DurationSeconds = 187.5,
        Width = 3840,
        Height = 2160,
        Fps = 59.94,
        VideoCodec = "hevc",
        TotalBitrateBps = 18_800_000,
        AudioCodec = "aac",
        AudioBitrateBps = 192_000,
        AudioChannels = 2,
        PixelFormat = "yuv420p"
    };

    /// <summary>
    /// Ağaç iki kez tümden geçersizlenip ölçülür. Tek geçişte bir <see cref="ScrollViewer"/>'ın
    /// dikey çubuğu ölçü sırasında görünür olur; ebeveyn Grid o an ölçmekte olduğu için
    /// <c>Auto</c> sütun 0 genişlikte kalır ve çubuk içerikle çakışır (it 1136x720 Dönüştür,
    /// 5x666). Gerçek pencerede düzen yöneticisi bir sonraki geçişte bunu düzeltir; ikinci
    /// geçiş aynı şeyi yapar. Geçersizleme kaldırılınca 10/18 kol kırmızı döner (ölçüldü).
    /// </summary>
    private static void Yerlestir(MainWindow pencere, Size boyut)
    {
        pencere.Width = double.NaN;
        pencere.Height = double.NaN;
        pencere.Measure(boyut);
        pencere.Arrange(new Rect(boyut));
        pencere.UpdateLayout();
        Dispatcher();
        pencere.SettleFades();

        var kok = (Layoutable)pencere.GetVisualChildren().Single();
        for (var tur = 0; tur < 2; tur++)
        {
            foreach (var dugum in pencere.GetVisualDescendants().OfType<Layoutable>()) dugum.InvalidateMeasure();
            kok.InvalidateMeasure();
            kok.Measure(boyut);
            kok.Arrange(new Rect(boyut));
        }

        foreach (var dugum in pencere.GetVisualDescendants().OfType<Visual>()) dugum.RenderTransform = null;
    }

    private static void Dispatcher() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static void Tara(MainWindow pencere, Size boyut, Denetim denetim, Action<MainWindow>? boz)
    {
        Yerlestir(pencere, boyut);
        var sekmeler = pencere.Tabs;
        var acilan = new HashSet<Button>();

        for (var sira = 0; sira < sekmeler.ItemCount; sira++)
        {
            if (sekmeler.ContainerFromIndex(sira) is not TabItem { IsVisible: true }) continue;
            sekmeler.SelectedIndex = sira;
            Yerlestir(pencere, boyut);

            foreach (var dugme in pencere.GetVisualDescendants().OfType<Button>()
                         .Where(d => d.IsShown() && d.Name is { } ad && ad.EndsWith("Toggle", StringComparison.Ordinal) && acilan.Add(d))
                         .ToList())
                dugme.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Yerlestir(pencere, boyut);
        }

        for (var sira = 0; sira < sekmeler.ItemCount; sira++)
        {
            if (sekmeler.ContainerFromIndex(sira) is not TabItem { IsVisible: true } oge) continue;
            sekmeler.SelectedIndex = sira;
            Yerlestir(pencere, boyut);

            if (boz is not null && oge == pencere.TabSettings) boz(pencere);
            Yerlestir(pencere, boyut);
            Yerlestir(pencere, boyut);

            var baslik = MainWindow.TabHeaderText(oge);
            if (sira == 1 || oge == pencere.TabSettings)
                Assert.Equal(LanguageCatalog.Display(Strings.Get(oge == pencere.TabSettings ? "main.tab.settings" : "main.tab.shrink")), baslik);

            denetim.Sekme++;
            denetim.Disarida.Clear();
            foreach (var diger in sekmeler.Items.OfType<TabItem>())
                if (diger != oge && diger.Content is Visual sayfa) denetim.Disarida.Add(sayfa);
            Metinler(pencere, baslik, denetim);
            Kardesler(pencere, baslik, denetim);
            Tasmalar(pencere, baslik, denetim);
            Serit(pencere, oge, baslik, denetim);
            if (denetim.Dil is "tr" or "en") Basliklar(pencere, baslik, denetim);
        }
    }

    private static string Ad(Control denetim)
    {
        var kendi = string.IsNullOrEmpty(denetim.Name) ? denetim.GetType().Name : denetim.GetType().Name + "#" + denetim.Name;
        if (denetim.Tag is string etiket && etiket.StartsWith("Olumlu", StringComparison.Ordinal)) kendi += "@" + etiket;
        var ata = denetim.GetVisualAncestors().OfType<Control>().FirstOrDefault(c => !string.IsNullOrEmpty(c.Name));
        return ata is null ? kendi : $"{kendi} < {ata.Name}";
    }

    private static string Kisalt(string? metin)
    {
        var tek = (metin ?? string.Empty).Replace('\n', '⏎');
        return tek.Length > 60 ? tek[..60] + "…" : tek;
    }

    private static double GerekenGenislik(TextBlock blok, string metin) =>
        new Avalonia.Media.TextFormatting.TextLayout(
            metin,
            new Typeface(blok.FontFamily, blok.FontStyle, blok.FontWeight, blok.FontStretch),
            blok.FontSize,
            Brushes.Black,
            lineHeight: blok.LineHeight,
            letterSpacing: blok.LetterSpacing).WidthIncludingTrailingWhitespace;

    /// <summary>
    /// <see cref="TextBlock"/> kendi sınırını kırpar (<c>ClipToBounds</c> varsayılanı açık).
    /// Satır kutusu alttaki iniş payını da taşıdığı için yalnız yüksekliğe bakmak "?" gibi
    /// inişsiz tek bir işaretin sığdığı rozeti kesik sayıyordu. İki ölçü: son satırın taban
    /// çizgisi kutunun altına düşüyorsa harfin gövdesi kesilir; satır kutusu
    /// <see cref="DikeyPay"/>'dan fazla taşıyorsa iniş harfleri de kesilir.
    /// </summary>
    private const double DikeyPay = 3;

    private static (double Yukseklik, double Taban) GerekenYukseklik(TextBlock blok, string metin, double genislik)
    {
        var yerlesim = new Avalonia.Media.TextFormatting.TextLayout(
            metin,
            new Typeface(blok.FontFamily, blok.FontStyle, blok.FontWeight, blok.FontStretch),
            blok.FontSize,
            Brushes.Black,
            textWrapping: blok.TextWrapping,
            maxWidth: blok.TextWrapping == TextWrapping.NoWrap ? double.PositiveInfinity : genislik,
            lineHeight: blok.LineHeight,
            letterSpacing: blok.LetterSpacing);
        var satirlar = yerlesim.TextLines;
        var ust = satirlar.Take(satirlar.Count - 1).Sum(satir => satir.Height);
        return (yerlesim.Height, ust + satirlar[^1].Baseline);
    }

    /// <summary>Bir görselin <paramref name="ata"/> uzayındaki konumu, yalnız <see cref="Visual.Bounds"/> toplanarak.</summary>
    private static Point Konum(Visual dugum, Visual ata)
    {
        double x = 0, y = 0;
        for (var d = dugum; d is not null && d != ata; d = d.GetVisualParent())
        {
            x += d.Bounds.X;
            y += d.Bounds.Y;
        }

        return new Point(x, y);
    }

    private static void Metinler(MainWindow pencere, string sekme, Denetim denetim)
    {
        foreach (var blok in pencere.GetVisualDescendants().OfType<TextBlock>())
        {
            if (!denetim.Kapsamda(blok) || blok.Bounds.Width <= 0 || string.IsNullOrWhiteSpace(blok.Text)) continue;
            if (blok.FindAncestorOfType<TextBox>() is not null) continue;
            if (blok.Opacity <= 0) continue;
            denetim.Metin++;

            var metin = blok.Text!;
            var yer = blok.Bounds.Width - blok.Padding.Left - blok.Padding.Right;
            var yukseklikYeri = blok.Bounds.Height - blok.Padding.Top - blok.Padding.Bottom;
            var gereken = blok.TextWrapping == TextWrapping.NoWrap ? GerekenGenislik(blok, metin) : Math.Min(yer, GerekenGenislik(blok, metin));
            var balonda = blok.TextTrimming != TextTrimming.None && ToolTip.GetTip(blok) as string == metin;

            var cizilen = blok.TextLayout.Width;
            if (cizilen - yer > 0.5)
            {
                denetim.Ekle(new Kusur("kesik", sekme, $"{Ad(blok)} [{Kisalt(metin)}]", $"çizilen {cizilen:0.#} kutudan geniş, yer {yer:0.#}"));
                continue;
            }

            if (blok.TextWrapping == TextWrapping.NoWrap && gereken - yer > 0.5)
            {
                if (balonda) denetim.Balonlu.Add($"{sekme} · {Ad(blok)} [{Kisalt(metin)}] gereken {gereken:0.#}, yer {yer:0.#}");
                else denetim.Ekle(new Kusur("kesik", sekme, $"{Ad(blok)} [{Kisalt(metin)}]", $"genişlik gereken {gereken:0.#}, yer {yer:0.#}"));
                continue;
            }

            var (dikey, taban) = GerekenYukseklik(blok, metin, yer);
            if (taban - yukseklikYeri > 0.5 || dikey - yukseklikYeri > DikeyPay)
            {
                denetim.Ekle(new Kusur("kesik", sekme, $"{Ad(blok)} [{Kisalt(metin)}]", $"yükseklik gereken {dikey:0.#} (taban çizgisi {taban:0.#}), yer {yukseklikYeri:0.#}"));
                continue;
            }

            var ust = blok.Padding.Top;
            var alt = blok.Padding.Top + Math.Max(yukseklikYeri, taban);
            var sol = blok.Padding.Left;
            var sag = blok.Padding.Left + Math.Max(yer, gereken);
            foreach (var ata in blok.GetVisualAncestors().OfType<Control>())
            {
                if (ata is TopLevel) break;
                if (!ata.ClipToBounds)
                {
                    if (ata is ScrollViewer) break;
                    continue;
                }

                var konum = Konum(blok, ata);
                var kaydirma = ata is ScrollContentPresenter or ScrollViewer;
                var solTasma = -(konum.X + sol);
                var sagTasma = konum.X + sag - ata.Bounds.Width;
                var ustTasma = kaydirma ? 0 : -(konum.Y + ust);
                var altTasma = kaydirma ? 0 : konum.Y + alt - ata.Bounds.Height;
                var tasma = Math.Max(Math.Max(solTasma, sagTasma), Math.Max(ustTasma, altTasma));
                if (tasma > 1)
                {
                    var eksen = Math.Max(ustTasma, altTasma) >= Math.Max(solTasma, sagTasma) ? "dikey" : "yatay";
                    denetim.Ekle(new Kusur("kesik", sekme, $"{Ad(blok)} [{Kisalt(metin)}]", $"{Ad(ata)} kırpıyor, {eksen} taşma {tasma:0.#} (metin {dikey:0.#} px, kutu {yukseklikYeri:0.#} px)"));
                    break;
                }

                if (ata is ScrollViewer) break;
            }
        }
    }

    private static bool AyniGoz(Control a, Control b)
    {
        static (int, int, int, int) Goz(Control c) =>
            (Grid.GetRow(c), Grid.GetRow(c) + Math.Max(1, Grid.GetRowSpan(c)), Grid.GetColumn(c), Grid.GetColumn(c) + Math.Max(1, Grid.GetColumnSpan(c)));

        var (r1, r2, c1, c2) = Goz(a);
        var (s1, s2, d1, d2) = Goz(b);
        return r1 < s2 && s1 < r2 && c1 < d2 && d1 < c2;
    }

    private static void Kardesler(MainWindow pencere, string sekme, Denetim denetim)
    {
        foreach (var panel in pencere.GetVisualDescendants().OfType<Panel>())
        {
            if (!denetim.Kapsamda(panel)) continue;
            if (panel.GetType() == typeof(Panel) || panel is Canvas) continue;
            if (panel.Name is { } ad && KatmanIstisnalari.ContainsKey(ad)) continue;
            denetim.Panel++;

            var cocuklar = panel.Children.Where(c => c.IsVisible && c.Opacity > 0 && c.Bounds.Width > 0 && c.Bounds.Height > 0).ToList();
            for (var i = 0; i < cocuklar.Count; i++)
                for (var j = i + 1; j < cocuklar.Count; j++)
                {
                    var a = cocuklar[i];
                    var b = cocuklar[j];
                    if (panel is Grid && AyniGoz(a, b)) continue;
                    var kesisim = a.Bounds.Intersect(b.Bounds);
                    if (kesisim.Width <= 1 || kesisim.Height <= 1) continue;
                    denetim.Ekle(new Kusur("çakışma", sekme, $"{Ad(a)} ∩ {Ad(b)}",
                        $"{panel.GetType().Name}{(string.IsNullOrEmpty(panel.Name) ? "" : "#" + panel.Name)} içinde {kesisim.Width:0.#}x{kesisim.Height:0.#}"));
                }
        }
    }

    private static void Tasmalar(MainWindow pencere, string sekme, Denetim denetim)
    {
        foreach (var cocuk in pencere.GetVisualDescendants().OfType<Control>())
        {
            if (!denetim.Kapsamda(cocuk) || cocuk.Bounds.Width <= 0 || cocuk.Bounds.Height <= 0) continue;
            if (cocuk.GetVisualParent() is not Control ebeveyn || ebeveyn is TopLevel) continue;
            if (ebeveyn is Canvas || cocuk is Popup) continue;
            denetim.Denetim_++;

            var b = cocuk.Bounds;
            var yatay = Math.Max(-b.X, b.Right - ebeveyn.Bounds.Width);
            var dikey = Math.Max(-b.Y, b.Bottom - ebeveyn.Bounds.Height);

            if (ebeveyn is ScrollContentPresenter sunucu)
            {
                if (sunucu.FindAncestorOfType<ScrollViewer>() is { } kaydirici
                    && kaydirici.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled) continue;
                dikey = 0;
            }

            var tasma = Math.Max(yatay, dikey);
            if (tasma <= 1) continue;
            denetim.Ekle(new Kusur("taşma", sekme, Ad(cocuk),
                $"{ebeveyn.GetType().Name}{(string.IsNullOrEmpty(ebeveyn.Name) ? "" : "#" + ebeveyn.Name)} [{ebeveyn.Bounds.Width:0.#}x{ebeveyn.Bounds.Height:0.#}] dışına {(yatay >= dikey ? "yatay" : "dikey")} {tasma:0.#}"));
        }
    }

    /// <summary>
    /// Oynatıcı sayfası tüm alanı kaplar (T185) ve şerit onun üstünde kendini gizler; bu yüzden
    /// ölçülmez. Öteki her sayfanın kaydırıcısı şeridin altında başlamalı.
    /// </summary>
    private static void Serit(MainWindow pencere, TabItem oge, string sekme, Denetim denetim)
    {
        if (oge == pencere.TabPlayer) return;
        if (oge.Content is not Control sayfa || !sayfa.IsShown()) return;
        var ust = Konum(sayfa, pencere).Y;
        var seritAlti = Konum(pencere.TitleBar, pencere).Y + pencere.TitleBar.Bounds.Height;
        if (ust + 0.5 < seritAlti)
            denetim.Ekle(new Kusur("şerit", sekme, Ad(sayfa), $"sayfa {ust:0.#}'de başlıyor, şerit {seritAlti:0.#}'de bitiyor"));
    }

    private static readonly IReadOnlyDictionary<string, HashSet<string>> KucukSozcukler =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["tr"] = new(StringComparer.Ordinal) { "ve", "veya", "ile", "ki", "da", "de", "ya", "mi", "mı", "mu", "mü" },
            ["en"] = new(StringComparer.Ordinal) { "a", "an", "the", "and", "or", "but", "nor", "of", "to", "in", "on", "at", "by", "for", "from", "with", "as", "vs", "per", "via" },
        };

    private static readonly HashSet<string> BaslikTemalari = new(StringComparer.Ordinal) { "H1", "H2", "H3", "Label", "PlanFactLabel" };

    /// <summary>Başlık kuralını bozan ilk sözcük; yoksa <c>null</c>. Cümle işaretli metin gövdedir, sorulmaz.</summary>
    internal static string? KucukSozcuk(string metin, string dil)
    {
        if (LanguageCatalog.Brands.ContainsKey(metin)) return null;
        for (var i = 0; i < metin.Length; i++)
            if (metin[i] is '.' or ';' or '!' or '?' && (i + 1 == metin.Length || char.IsWhiteSpace(metin[i + 1])))
                return null;

        var kucuk = KucukSozcukler[dil];
        var ilk = true;
        foreach (var parca in metin.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var bas = 0;
            while (bas < parca.Length && !char.IsLetter(parca[bas])) bas++;
            if (bas == parca.Length) continue;
            var sozcuk = parca[bas..].TrimEnd(')', ',', ':', '…');
            var yalin = sozcuk.ToLowerInvariant();
            var digeri = !ilk && kucuk.Contains(yalin);
            ilk = false;
            if (digeri || !char.IsLower(sozcuk[0])) continue;
            if (LanguageCatalog.Title(sozcuk, dil) == sozcuk) continue;
            return sozcuk;
        }

        return null;
    }

    private static void Basliklar(MainWindow pencere, string sekme, Denetim denetim)
    {
        foreach (var blok in pencere.GetVisualDescendants().OfType<TextBlock>())
        {
            if (!denetim.Kapsamda(blok) || blok.Bounds.Width <= 0 || string.IsNullOrWhiteSpace(blok.Text)) continue;
            if (blok.FindAncestorOfType<TextBox>() is not null || blok.FindAncestorOfType<ComboBox>() is not null) continue;

            var tema = blok.Theme is { } t ? BaslikTemasi(pencere, t) : null;
            var dugmede = blok.Theme is null && blok.FindAncestorOfType<Button>() is not null;
            if (tema is null && !dugmede) continue;
            denetim.Baslik++;

            if (BaslikIstisnalari.Keys.Any(anahtar => LanguageCatalog.Display(Strings.Get(anahtar)) == blok.Text)) continue;
            if (KucukSozcuk(blok.Text!, denetim.Dil) is { } sozcuk)
                denetim.Ekle(new Kusur("başlık", sekme, $"{Ad(blok)} [{Kisalt(blok.Text)}]", $"'{sozcuk}' küçük harfle ({tema ?? "düğme"})"));
        }
    }

    private static string? BaslikTemasi(MainWindow pencere, ControlTheme tema)
    {
        foreach (var ad in BaslikTemalari)
            if (pencere.TryFindResource(ad, out var deger) && ReferenceEquals(deger, tema))
                return ad;
        return null;
    }
}
