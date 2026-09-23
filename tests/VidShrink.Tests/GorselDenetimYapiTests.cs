using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using VidShrink.App;
using Xunit.Abstractions;
using static VidShrink.Tests.GorselDenetimTests;

namespace VidShrink.Tests;

/// <summary>
/// Görsel denetimin (2026-09-23) yapı ve metin bulguları: hizalar, başlık temaları, sarma.
/// Her kol düzeltmenin davranışını okur; düzeltme geri alınınca kırmızıya düşer.
/// </summary>
public sealed class GorselDenetimYapiTests
{
    private readonly ITestOutputHelper _output;

    public GorselDenetimYapiTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Bulgu 10: oynatıcı kısayol tablosunda kalın mono tuş ile sans açıklamanın ilk satır taban
    /// çizgileri aynı yükseklikte. İkisi satırın tepesine yaslıyken 2-3 px kayıyordu.
    /// </summary>
    [Theory]
    [InlineData("tr")]
    [InlineData("el")]
    public void KisayolTusuVeAciklamaAyniTabanda(string dil)
    {
        var kaymalar = AppHost.Run(() =>
        {
            VidShrink.App.Localization.Strings.Use(dil);
            var panel = new VidShrink.App.Playback.PlayerShortcutsPanel();
            var pencere = new Window { Width = 900, Height = 1400, Content = panel };
            pencere.Show();
            try
            {
                pencere.Measure(new Size(900, 1400));
                pencere.Arrange(new Rect(0, 0, 900, 1400));
                pencere.UpdateLayout();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                pencere.UpdateLayout();
                var satirlar = Ad<Grid>(panel, "Rows").Children.OfType<TextBlock>().ToArray();
                return Enumerable.Range(0, satirlar.Length / 2).Select(i =>
                {
                    var tus = satirlar[2 * i];
                    var aciklama = satirlar[2 * i + 1];
                    return (tus.Text, fark: Yeri(tus, panel).Top + tus.TextLayout.TextLines[0].Baseline
                                            - Yeri(aciklama, panel).Top - aciklama.TextLayout.TextLines[0].Baseline);
                }).ToArray();
            }
            finally
            {
                pencere.Close();
                VidShrink.App.Localization.Strings.Use("en");
            }
        });

        Assert.NotEmpty(kaymalar);
        foreach (var (tus, fark) in kaymalar) _output.WriteLine($"{tus}: {fark:0.##}");
        var enKotu = kaymalar.MaxBy(k => Math.Abs(k.fark));
        Assert.True(Math.Abs(enKotu.fark) < 0.5, $"{enKotu.Text}: tuş ile açıklamanın tabanı {enKotu.fark:0.##} px kayık.");
    }

    /// <summary>
    /// Bulgu 5: seçim düğmesi onay kutusuyla aynı yazıyı taşır ve dar sütunda sarar.
    /// Fluent'in varsayılanı başka boyda yazıyor ve sarmıyordu; Rusça dar pencerede
    /// "Остановиться на Пределе Качества" sütundan taşıyordu.
    /// </summary>
    [Fact]
    public void SecimDugmesiOnayKutusuylaAyniYaziylaSarar()
    {
        var (secimYazi, kutuYazi, secimBoy, kutuBoy, en, sigdi) = AppHost.Run(() =>
        {
            var kaynak = Avalonia.Application.Current!;
            const string metin = "Остановиться на Пределе Качества Остановиться на Пределе Качества";
            var secim = new RadioButton { Content = metin };
            var kutu = new CheckBox { Content = "Kutu", Theme = (Avalonia.Styling.ControlTheme)kaynak.FindResource("CheckStyle")! };
            var yigin = new StackPanel { Width = 260, Children = { secim, kutu } };
            var pencere = new Window { Width = 400, Height = 400, Content = yigin };
            pencere.Show();
            try
            {
                pencere.Measure(new Size(400, 400));
                pencere.Arrange(new Rect(0, 0, 400, 400));
                pencere.UpdateLayout();
                var yazi = secim.GetVisualDescendants().OfType<TextBlock>().First();
                var kutuYazisi = kutu.GetVisualDescendants().OfType<TextBlock>().First();
                return (yazi.FontFamily.Name, kutuYazisi.FontFamily.Name, yazi.FontSize, kutuYazisi.FontSize,
                    secim.Bounds.Width, yazi.TextLayout.WidthIncludingTrailingWhitespace <= Yeri(yazi, secim).Width + 0.5
                        && yazi.TextLayout.TextLines.Count > 1);
            }
            finally { pencere.Close(); }
        });

        _output.WriteLine($"secim {secimYazi} {secimBoy}, kutu {kutuYazi} {kutuBoy}, en {en:0.#}, sardi {sigdi}");
        Assert.Equal(kutuYazi, secimYazi);
        Assert.Equal(kutuBoy, secimBoy);
        Assert.True(sigdi, "Seçim düğmesinin yazısı sütunda sarılmıyor, taşıyor.");
    }

    /// <summary>
    /// Bulgu 11: oynatıcının gelişmiş paneli başlık düğmesinin oku panelin içerik kenarında
    /// başlar, düğmenin kendi dolgusu ve çerçevesi başlığı içeri itmez.
    /// </summary>
    [Fact]
    public void PanelBasligiIcerikKenarindaBaslar()
    {
        var (fark, cerceve) = AppHost.Run(() =>
        {
            var panel = new VidShrink.App.Playback.PlayerAdvancedPanel();
            var pencere = new Window { Width = 700, Height = 500, Content = panel };
            pencere.Show();
            try
            {
                pencere.Measure(new Size(700, 500));
                pencere.Arrange(new Rect(0, 0, 700, 500));
                pencere.UpdateLayout();
                var dugme = Ad<Button>(panel, "BtnToggle");
                var ok = Ad<Avalonia.Controls.Shapes.Path>(panel, "Glyph");
                var yigin = (Control)dugme.GetVisualParent()!;
                return (Yeri(ok, panel).Left - Yeri(yigin, panel).Left, dugme.BorderThickness.Left);
            }
            finally { pencere.Close(); }
        });

        _output.WriteLine($"ok farki {fark:0.##}, cerceve {cerceve}");
        Assert.Equal(0, cerceve);
        Assert.True(Math.Abs(fark) < 0.5, $"Başlık oku içerik kenarından {fark:0.##} px içeride.");
    }

    /// <summary>
    /// Kırpılma taraması 1: kaydedicinin otomatik/elle seçimi yatay yığındaydı; yığın çocuklarına
    /// sınırsız en verdiği için seçim düğmesi sarılamıyor, dar pencerede İtalyanca "automatico"
    /// satırı sütunun dışına taşıyordu. İki seçim de seçenek panelinin içerik kenarında kalır.
    /// </summary>
    [Theory]
    [InlineData("it")]
    [InlineData("el")]
    public void KaydediciKodlamaSecimiSutundaKalir(string dil)
    {
        var tasmalar = Pencere(Dar, w =>
        {
            var panel = Ad<Border>(w, "PanelOptions");
            panel.IsVisible = true;
            Grid.SetColumnSpan(Ad<Border>(w, "PanelTarget"), 1);
            Yerlestir(w, Dar);
            var sag = Yeri(panel, w).Right - panel.Padding.Right - panel.BorderThickness.Right;
            return new[] { "RadAuto", "RadManual" }.Select(ad =>
            {
                var secim = Ad<RadioButton>(w, ad);
                var yazi = secim.GetVisualDescendants().OfType<TextBlock>().First();
                var yaziSonu = Yeri(yazi, w).Left + yazi.TextLayout.WidthIncludingTrailingWhitespace;
                return (ad, tasma: Math.Max(Yeri(secim, w).Right, yaziSonu) - sag);
            }).ToArray();
        }, sekme: 3, dil: dil);

        foreach (var (ad, tasma) in tasmalar) _output.WriteLine($"{ad}: {tasma:0.#}");
        var enKotu = tasmalar.MaxBy(t => t.tasma);
        Assert.True(enKotu.tasma <= 0.5, $"{enKotu.ad} seçenek panelinden {enKotu.tasma:0.#} px taşıyor.");
    }

    /// <summary>
    /// Kırpılma taraması 5: güncelleme bildirimlerinin yazısı Auto sütundaydı; Auto sütun
    /// yazıya sınırsız en verdiği için uzun çeviri sarılmıyor, İndir ve kapat düğmelerini
    /// bildirimin dışına itiyordu. Yazı sarar, düğmeler bildirimin içinde kalır.
    /// </summary>
    [Fact]
    public void GuncellemeBildirimiSararDugmelerIcerideKalir()
    {
        var olculer = Pencere(Dar, w =>
        {
            var uzun = string.Join(" ", Enumerable.Repeat("Νέα έκδοση διαθέσιμη για λήψη", 8));
            Ad<Border>(w, "UpdateNotice").IsVisible = true;
            Ad<Border>(w, "AppliedNotice").IsVisible = true;
            Ad<TextBlock>(w, "TxtNoticeLead").Text = uzun;
            Ad<TextBlock>(w, "TxtNoticeVersion").Text = "v9.9.9";
            Ad<TextBlock>(w, "TxtAppliedLead").Text = uzun;
            Ad<TextBlock>(w, "TxtAppliedVersion").Text = "v9.9.9";
            Yerlestir(w, Dar);
            return new[] { ("UpdateNotice", "BtnNoticeDismiss", "TxtNoticeLead"), ("AppliedNotice", "BtnAppliedDismiss", "TxtAppliedLead") }
                .Select(s =>
                {
                    var bildirim = Ad<Border>(w, s.Item1);
                    var sag = Yeri(bildirim, w).Right - bildirim.Padding.Right - bildirim.BorderThickness.Right;
                    var yazi = Ad<TextBlock>(w, s.Item3);
                    return (s.Item1, tasma: Yeri(Ad<Button>(w, s.Item2), w).Right - sag, satir: yazi.TextLayout.TextLines.Count);
                }).ToArray();
        });

        foreach (var (ad, tasma, satir) in olculer) _output.WriteLine($"{ad}: taşma {tasma:0.#}, satır {satir}");
        foreach (var (ad, tasma, satir) in olculer)
        {
            Assert.True(tasma <= 0.5, $"{ad}: kapat düğmesi bildirimden {tasma:0.#} px taşıyor.");
            Assert.True(satir > 1, $"{ad}: uzun yazı sarılmıyor.");
        }
    }

    /// <summary>
    /// Kırpılma taraması 3: oynatıcı gelişmiş panelinin özeti üç noktayla kırpılıyor; tamamı
    /// balonda okunur ve özet değişince balon da değişir.
    /// </summary>
    [Fact]
    public void GelismisPanelOzetiBalondaTamOkunur()
    {
        var (ilk, ikinci) = AppHost.Run(() =>
        {
            var panel = new VidShrink.App.Playback.PlayerAdvancedPanel();
            var ozet = panel.FindControl<TextBlock>("TxtSummary")!;
            ozet.Text = "Deinterlace · 1.25x";
            var once = ToolTip.GetTip(ozet) as string;
            ozet.Text = "Deband · Sharpen · Denoise";
            return (once, ToolTip.GetTip(ozet) as string);
        });

        Assert.Equal("Deinterlace · 1.25x", ilk);
        Assert.Equal("Deband · Sharpen · Denoise", ikinci);
    }

    /// <summary>
    /// Kırpılma taraması 4: yaklaşık rozetinin eni perdeyle değişmez. Tavan perdenin
    /// genişliğinden türüyordu; ayırıcı sağa kayınca 0'a iniyor, metin tümden kayboluyordu.
    /// </summary>
    [Fact]
    public void YaklasikRozetiPerdeKayincaKaybolmaz()
    {
        var (orta, sagda, gorunur) = AppHost.Run(() =>
        {
            var panel = new VidShrink.App.Playback.ComparisonPanel();
            var pencere = new Window { Width = 800, Height = 500, Content = panel };
            pencere.Show();
            try
            {
                void Diz()
                {
                    pencere.Measure(new Size(800, 500));
                    pencere.Arrange(new Rect(0, 0, 800, 500));
                    pencere.UpdateLayout();
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    pencere.UpdateLayout();
                }
                Diz();
                typeof(VidShrink.App.Playback.ComparisonSurface)
                    .GetField("_hasFrame", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .SetValue(panel.Frames, true);
                panel.RefreshEmptyState();
                panel.SetRightBadge("Approximate preview · CRF 21");
                panel.Split = 0.5;
                Diz();
                var ortadaEn = panel.ApproxBadgeText.Bounds.Width;
                panel.Split = 0.9;
                Diz();
                return (ortadaEn, panel.ApproxBadgeText.Bounds.Width, panel.ApproxBadge.IsVisible);
            }
            finally { pencere.Close(); }
        });

        _output.WriteLine($"orta {orta:0.#}, sağda {sagda:0.#}, görünür {gorunur}");
        Assert.True(gorunur, "Rozet perde 0,9'dayken gizli; ölçü boşa düşerdi.");
        Assert.True(orta > 0, "Rozet ölçülmedi.");
        Assert.Equal(orta, sagda, 1);
    }

    [Theory]
    [InlineData("tr", false)]
    [InlineData("tr", true)]
    [InlineData("de", false)]
    [InlineData("en", false)]
    [InlineData("el", true)]
    public void DonusturFormundaEtiketVeDenetimSatirdaHizali(string dil, bool enDar)
    {
        var boyut = enDar ? YerlesimDenetimiTests.DarBoyut : Dar;
        var farklar = Pencere(boyut, w =>
        {
            var form = Ad<Grid>(w, "ConvertForm");
            return form.Children.OfType<Grid>().Where(c => c.IsVisible)
                .GroupBy(Grid.GetRow)
                .Where(g => g.Count() > 1)
                .Select(g =>
                {
                    var etiketler = g.Select(c => Yeri(c.Children[0], w).Bottom).ToArray();
                    var denetimler = g.Select(c => Yeri(c.Children.Single(d => Grid.GetRow(d) == 1), w).Top).ToArray();
                    var satirlar = g.Max(c => ((Grid)c.Children[0]).Children.OfType<TextBlock>().Single().TextLayout.TextLines.Count);
                    return (satir: g.Key, etiket: etiketler.Max() - etiketler.Min(), denetim: denetimler.Max() - denetimler.Min(), satirlar);
                })
                .ToArray();
        }, sekme: 2, dil: dil);

        foreach (var (satir, etiket, denetim, satirlar) in farklar) _output.WriteLine($"satır {satir}: etiket altı {etiket:0.#}, denetim üstü {denetim:0.#}, en çok {satirlar} satır etiket");
        Assert.Equal(6, farklar.Length);
        var enKotu = farklar.MaxBy(f => Math.Max(f.etiket, f.denetim));
        Assert.True(Math.Max(enKotu.etiket, enKotu.denetim) <= 0.5,
            $"Satır {enKotu.satir}: etiket altları {enKotu.etiket:0.#} px, denetim üstleri {enKotu.denetim:0.#} px ayrı.");
    }

    /// <summary>
    /// Görsel denetim bulgu 13: süzgeç anahtarları sarılan bir satırdaydı; dar pencerede
    /// ikinci satır birinci satırın sütunlarıyla hizalanmıyordu. Anahtarlar sütunlu ızgarada,
    /// alt alta aynı sol kenarda durur ve panelin içinde kalır. İki sütun yalnız etiketler tek
    /// satıra sığarsa kurulur: eşit iki sütunda 1024 px pencerede "Detelecine" 74 px'e sıkışıp
    /// sözcük ortasından bölünüyordu (yerleşim denetimi, 453 bölünme). Sütun sayısı en geniş
    /// dar kolda da, genişte de etiketin doğal genişliğinden aşağı inmez.
    /// </summary>
    [Theory]
    [InlineData("tr", false)]
    [InlineData("el", false)]
    [InlineData("en", true)]
    [InlineData("el", true)]
    public void SuzgecAnahtarlariIkiSutundaHizali(string dil, bool enDar)
    {
        var boyut = enDar ? YerlesimDenetimiTests.DarBoyut : Dar;
        var (solFark, sagFark, tasma, sikisma) = Pencere(boyut, w =>
        {
            Ad<StackPanel>(w, "AdvancedBody").IsVisible = true;
            Yerlestir(w, boyut);
            var kutular = new[] { "ChkFltDetelecine", "ChkFltDeblock", "ChkFltDeband", "ChkFltGray" }.Select(ad => Ad<CheckBox>(w, ad)).ToArray();
            var hepsi = kutular.Select(k => Yeri(k, w)).ToArray();
            var izgara = Ad<Panel>(w, "FltSwitches");
            var sinir = Yeri(izgara, w).Right;
            Assert.All(hepsi, r => Assert.True(r.Width > 0, "Anahtar yerleşmedi."));
            var sikisma = kutular.Zip(hepsi).Max(p =>
            {
                p.First.Measure(Size.Infinity);
                return p.First.DesiredSize.Width - p.First.Margin.Left - p.First.Margin.Right - p.Second.Width;
            });
            return (Math.Abs(hepsi[0].Left - hepsi[2].Left), Math.Abs(hepsi[1].Left - hepsi[3].Left), hepsi.Max(r => r.Right) - sinir, sikisma);
        }, sekme: 1, dil: dil);

        _output.WriteLine($"sol {solFark:0.#}, sağ {sagFark:0.#}, taşma {tasma:0.#}, sıkışma {sikisma:0.#}");
        Assert.True(solFark <= 0.5 && sagFark <= 0.5, $"Sütunlar kaymış: sol {solFark:0.#}, sağ {sagFark:0.#} px.");
        Assert.True(tasma <= 0.5, $"Anahtar ızgaradan {tasma:0.#} px taşıyor.");
        Assert.True(sikisma <= 0.5, $"Anahtar doğal genişliğinden {sikisma:0.#} px dar; etiket sarılıyor.");
    }
    /// <summary>
    /// Canlandırma önerisi 1 ve 7: açılır bölüm oku ve açılır kutu oku açılınca döner.
    /// Azaltılmış harekette dönüş anında biter ve geçiş kurulmaz; öbür durumda geçiş
    /// <c>MotionFast</c> süresinde.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AcilirOklarDonerAzaltilmisHareketteAnindaDoner(bool azalt)
    {
        var (bolumAcik, bolumKapali, kutuAcik, bolumGecis, kutuGecis, beklenen) = AppHost.Run(() =>
        {
            var ok = new Avalonia.Controls.Shapes.Path { Width = 16, Height = 16, Data = Avalonia.Media.Geometry.Parse("M 0,0 L 8,8 L 16,0") };
            ok.Classes.Add("chevron");
            var kutu = new ComboBox { Width = 200, ItemsSource = new[] { "a", "b" } };
            var pencere = new Window { Width = 300, Height = 200, Content = new StackPanel { Children = { ok, kutu } } };
            if (azalt) pencere.Classes.Add("reduced-motion");
            pencere.Show();
            try
            {
                Application.Current!.TryFindResource("MotionFast", out var hizli);
                var kutuOku = Ad<Avalonia.Controls.Shapes.Path>(kutu, "Arrow");
                ok.Classes.Set("open", true);
                kutu.IsDropDownOpen = true;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var bolumAcik = M11(ok);
                var kutuAcik = M11(kutuOku);
                ok.Classes.Set("open", false);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var bolumKapali = M11(ok);
                kutu.IsDropDownOpen = false;
                return (bolumAcik, bolumKapali, kutuAcik, Sure(ok), Sure(kutuOku), (TimeSpan)hizli!);
            }
            finally { pencere.Close(); }
        });

        _output.WriteLine($"bölüm açık {bolumAcik:0.##}, kapalı {bolumKapali:0.##}, kutu açık {kutuAcik:0.##}, geçiş {bolumGecis}/{kutuGecis}");
        if (azalt)
        {
            Assert.Equal(-1, bolumAcik, 2);
            Assert.Equal(-1, kutuAcik, 2);
            Assert.Equal(1, bolumKapali, 2);
            Assert.Null(bolumGecis);
            Assert.Null(kutuGecis);
        }
        else
        {
            Assert.Equal(beklenen, bolumGecis);
            Assert.Equal(beklenen, kutuGecis);
        }

        static double M11(Visual v) => v.RenderTransform?.Value.M11 ?? 1;
        static TimeSpan? Sure(Animatable a) => a.Transitions?.OfType<Avalonia.Animation.TransformOperationsTransition>().FirstOrDefault()?.Duration;
    }
}