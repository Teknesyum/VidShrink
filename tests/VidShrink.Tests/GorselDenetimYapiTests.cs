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

    private sealed class SahteSonEylem : IQueueEndActions
    {
        public void Reveal(string path) { }
        public void Sleep() { }
        public void PowerOff() { }
    }

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
                var yaziSonu = Yeri(yazi, w).Left + yazi.TextLayout.Width;
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

    [Theory]
    [InlineData("tr", false)]
    [InlineData("tr", true)]
    [InlineData("de", true)]
    [InlineData("en", true)]
    [InlineData("ar", true)]
    [InlineData("ar", false)]
    public void CiktiOlgulariAyniSatirdaAyniYukseklikte(string dil, bool genis)
    {
        var boyut = genis ? Genis : Dar;
        var satirlar = Pencere(boyut, w =>
            new[] { "TxtStage", "TxtOutSize", "TxtRemaining", "TxtDurationValue" }
                .Select(ad => (ad, yer: Yeri(Ad<TextBlock>(w, ad), w)))
                .Select(d => (d.ad, ust: d.yer.Top, etiketUst: Yeri(((Panel)Ad<TextBlock>(w, d.ad).GetVisualParent()!).Children[0], w).Top))
                .ToArray(), sekme: 1, dil: dil);

        foreach (var (ad, ust, etiketUst) in satirlar) _output.WriteLine($"{ad}: değer üstü {ust:0.#}, etiket üstü {etiketUst:0.#}");
        var kaymalar = satirlar.GroupBy(s => Math.Round(s.etiketUst))
            .Where(g => g.Count() > 1)
            .Select(g => (satir: string.Join(" / ", g.Select(s => s.ad)), fark: g.Max(s => s.ust) - g.Min(s => s.ust)))
            .ToArray();
        if (kaymalar.Length == 0)
        {
            Assert.All(satirlar, s => Assert.True(s.ust > s.etiketUst + 0.5, $"{s.ad}: satır paylaşmayan değer etiketinin altında değil."));
            return;
        }
        var enKotu = kaymalar.MaxBy(k => k.fark);
        Assert.True(enKotu.fark <= 0.5, $"{enKotu.satir}: aynı satırdaki değerler {enKotu.fark:0.#} px kayık.");
    }

    public static TheoryData<string> TumDiller()
    {
        var veri = new TheoryData<string>();
        foreach (var dil in VidShrink.App.Localization.Strings.Languages) veri.Add(dil);
        return veri;
    }

    [Theory]
    [MemberData(nameof(TumDiller))]
    public void PlanPaneliKatliykenHerDildeKaymaz(string dil)
    {
        var (panel, gorus, icerik) = Pencere(Dar, w =>
        {
            var kaydirici = Ad<ScrollViewer>(w, "PlanScroll");
            return (Ad<Border>(w, "PlanPanel").Bounds.Height, kaydirici.Viewport.Height, kaydirici.Extent.Height);
        }, sekme: 1, dil: dil);

        _output.WriteLine($"panel {panel:0.#}, görüş {gorus:0.#}, içerik {icerik:0.#}");
        Assert.True(icerik > 0, "Plan içeriği ölçülmedi.");
        Assert.True(icerik <= gorus + 0.5, $"Katlı plan {icerik:0.#} px istiyor, görüş alanı {gorus:0.#} px; son satır kırpılıyor.");
    }

    [Theory]
    [InlineData("ar", false)]
    [InlineData("fa", false)]
    [InlineData("ur", false)]
    [InlineData("he", false)]
    [InlineData("hi", false)]
    [InlineData("en", true)]
    [InlineData("tr", true)]
    public void BagliYazidaHarfAraligiSifir(string dil, bool aralikli)
    {
        var (sayi, enGenis, ad) = Pencere(Dar, w =>
        {
            var metinler = w.GetVisualDescendants().OfType<TextBlock>().ToArray();
            var enAralikli = metinler.MaxBy(t => t.LetterSpacing)!;
            return (metinler.Length, enAralikli.LetterSpacing, enAralikli.Text ?? enAralikli.Name ?? "");
        }, sekme: 1, dil: dil);

        _output.WriteLine($"{sayi} metin, en geniş aralık {enGenis:0.##} ('{ad}')");
        Assert.True(sayi > 50, "Pencere kurulmadı.");
        if (aralikli) Assert.True(enGenis > 0, "Harf aralıklı başlık stili hiç uygulanmadı; ölçü kör.");
        else Assert.True(enGenis == 0, $"'{ad}' {enGenis:0.##} px harf aralığıyla çiziliyor; bağlı yazıda harfler kopuyor.");
    }

    private static bool Aynali(Visual v)
    {
        var sayi = 0;
        for (Visual? d = v; d is not null; d = d.GetVisualParent()) if (d.HasMirrorTransform) sayi++;
        return sayi % 2 == 1;
    }

    [Theory]
    [InlineData("ar", true)]
    [InlineData("he", true)]
    [InlineData("en", false)]
    public void MedyaSimgeleriAynalanmaz(string dil, bool sagdanSola)
    {
        (string ad, bool aynali) Oku(MainWindow w, string ad) => (ad, Aynali(Ad<Avalonia.Controls.Shapes.Path>(w, ad)));
        var (oynatici, sekme) = Pencere(Dar, w =>
            (Oku(w, "GlyphSeritPlay"),
             w.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Where(p => p.Name == "TabIcon")
                .Select(p => (ad: "TabIcon", aynali: Aynali(p))).ToArray()), sekme: 0, dil: dil);
        var sayfaOku = Pencere(Dar, w => Oku(w, "GlyphPlanReasons").Item2, sekme: 1, dil: dil);
        var karsilastirma = AppHost.Run(() =>
        {
            var serit = new VidShrink.App.Playback.ControlStrip();
            var pencere = new Window
            {
                Width = 600, Height = 120, Content = serit,
                FlowDirection = sagdanSola ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight
            };
            pencere.Show();
            try
            {
                pencere.UpdateLayout();
                return ("GlyphPlayPause", Aynali(Ad<Avalonia.Controls.Shapes.Path>(serit, "GlyphPlayPause")));
            }
            finally { pencere.Close(); }
        });
        (string ad, bool aynali)[] medya = { oynatici, karsilastirma };

        foreach (var (ad, aynali) in medya.Concat(sekme)) _output.WriteLine($"{ad}: {(aynali ? "aynalı" : "düz")}");
        _output.WriteLine($"plan oku: {(sayfaOku ? "aynalı" : "düz")}");
        Assert.Equal(sagdanSola, sayfaOku);
        Assert.True(sekme.Length >= 5, $"{sekme.Length} sekme simgesi bulundu.");
        var aynalilar = medya.Concat(sekme).Where(m => m.aynali).Select(m => m.ad).ToArray();
        Assert.True(aynalilar.Length == 0, $"Aynalanan medya/sekme simgesi: {string.Join(", ", aynalilar)}.");
    }

    [Theory]
    [InlineData("ar", true)]
    [InlineData("en", false)]
    public void SayiVeKomutSagdanSolaYazidaSoldanSagaOkunur(string dil, bool sagdanSola)
    {
        var girdi = sagdanSola ? "الدقة 1228×690 px" : "Resolution 1228×690 px";
        var (yazi, ilk, ikinci) = AppHost.Run(() =>
        {
            var metin = new TextBlock { Text = girdi };
            var pencere = new Window
            {
                Width = 600, Height = 120, Content = metin,
                FlowDirection = sagdanSola ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight
            };
            pencere.Show();
            try
            {
                pencere.UpdateLayout();
                var t = metin.Text!;
                return (t, metin.TextLayout.HitTestTextPosition(t.IndexOf("1228", StringComparison.Ordinal)).X,
                    metin.TextLayout.HitTestTextPosition(t.IndexOf("690", StringComparison.Ordinal)).X);
            }
            finally { pencere.Close(); }
        });
        var kutular = new[] { ("TxtConvertCommand", 2), ("TxtCommand", 4), ("TxtOutputName", 5) }
            .Select(k => (ad: k.Item1, yon: Pencere(Dar, w => Ad<TextBox>(w, k.Item1).FlowDirection, sekme: k.Item2, dil: dil)))
            .ToArray();
        var pencereYonu = Pencere(Dar, w => w.FlowDirection, sekme: 5, dil: dil);
        Assert.Equal(sagdanSola, pencereYonu == Avalonia.Media.FlowDirection.RightToLeft);

        _output.WriteLine($"{dil}: '1228' x={ilk:0.#}, '690' x={ikinci:0.#}, yalıtım={yazi.Contains('\u200E')}");
        foreach (var (ad, yon) in kutular) _output.WriteLine($"{ad}: {yon}");
        if (!sagdanSola) Assert.Equal(girdi, yazi);
        Assert.True(ilk < ikinci, $"'1228' ({ilk:0.#}) '690'un ({ikinci:0.#}) sağında; çözünürlük ters okunuyor.");
        Assert.All(kutular, k => Assert.Equal(Avalonia.Media.FlowDirection.LeftToRight, k.yon));
    }

    [Theory]
    [InlineData("de", "15,2", "MB")]
    [InlineData("ar", "15.2", "م.ب")]
    public void SonucMetnindeSayiBirimindenAyrilmaz(string dil, string sayi, string birim)
    {
        var (sonuc, tanik, ikinci) = Pencere(Dar, w =>
        {
            var yazi = string.Format(VidShrink.App.Localization.Strings.Get("main.run.done"), 3, "420,0", sayi, "96,4");
            var sonucMetni = Ad<TextBlock>(w, "TxtResult");
            var tanikMetni = Ad<TextBlock>(w, "TxtEstimateNote");
            sonucMetni.Text = yazi;
            tanikMetni.Text = yazi;
            var ilk = sonucMetni.Text!;
            sonucMetni.Text = ilk;
            return (ilk, tanikMetni.Text!, sonucMetni.Text!);
        }, sekme: 1, dil: dil);

        _output.WriteLine($"{dil}: {sonuc.Replace("\u00A0", "[nbsp]").Replace("\u200E", "[lrm]")}");
        Assert.Contains(sayi + " " + birim, tanik.Replace("\u200E", ""), StringComparison.Ordinal);
        Assert.Contains(sayi + "\u00A0" + birim, sonuc.Replace("\u200E", ""), StringComparison.Ordinal);
        Assert.DoesNotContain(sayi + " " + birim, sonuc.Replace("\u200E", ""), StringComparison.Ordinal);
        Assert.Equal(sonuc, ikinci);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("tr")]
    public void SesSeviyesiKutusuKazancKutusuylaOrtali(string dil)
    {
        var (fark, ayniSatir) = Pencere(Genis, w =>
        {
            Ad<StackPanel>(w, "AudioBody").IsVisible = true;
            Yerlestir(w, Genis);
            var kutu = Ad<CheckBox>(w, "ChkAudioLoudnorm");
            var kombo = Ad<ComboBox>(w, "CmbAudioGain");
            var yazi = kutu.GetVisualDescendants().OfType<Border>().First(b => b.Name == "CheckOutline");
            var k = Yeri(kombo, w);
            var y = Yeri(yazi, w);
            return (y.Center.Y - k.Center.Y, y.Top < k.Bottom && y.Bottom > k.Top);
        }, sekme: 1, dil: dil);

        _output.WriteLine($"{dil}: fark {fark:0.##}, aynı satır {ayniSatir}");
        Assert.True(ayniSatir, $"{dil}: kutu ile kazanç aynı satırda değil, ölçü bir şey görmüyor.");
        Assert.True(Math.Abs(fark) < 1, $"{dil}: ses normalleştirme kutusu kazanç kutusundan {fark:0.##} px kayık.");
    }

    [Theory]
    [InlineData("tr")]
    [InlineData("de")]
    public void PaylasimEtiketleriDenetimleOrtali(string dil)
    {
        var farklar = Pencere(Dar, w =>
        {
            var kombo = Ad<ComboBox>(w, "CmbShareRetention");
            var dugme = Ad<Button>(w, "BtnShareDelete");
            var izgara = (Grid)dugme.GetVisualParent()!;
            TextBlock Etiket(int satir) => izgara.Children.OfType<TextBlock>().Single(t => Grid.GetRow(t) == satir && Grid.GetColumn(t) == 0);
            double Orta(Control c) => Yeri(c, w).Center.Y;
            return new[] { ("Ömür", Orta(Etiket(1)) - Orta(kombo)), ("Silme", Orta(Etiket(2)) - Orta(dugme)) };
        }, sekme: 5, dil: dil);

        foreach (var (ad, fark) in farklar) _output.WriteLine($"{dil} {ad}: {fark:0.##}");
        Assert.All(farklar, f => Assert.True(Math.Abs(f.Item2) < 1, $"{f.Item1} etiketi denetimden {f.Item2:0.##} px kayık."));
    }

    [Theory]
    [InlineData("tr")]
    [InlineData("de")]
    public void SayfaKenariKaydirmaCubugunaDayanmaz(string dil)
    {
        var bosluklar = new[] { (1, "PageShrink"), (3, "PageRecorder"), (5, "PageSettings") }
            .Select(s => (ad: s.Item2, bosluk: Pencere(Dar, w =>
            {
                var kaydirici = Ad<ScrollViewer>(w, s.Item2);
                var icerik = (Control)kaydirici.Content!;
                var cubuk = kaydirici.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>()
                    .Single(c => c.Orientation == Orientation.Vertical && ReferenceEquals(c.TemplatedParent, kaydirici));
                Assert.True(cubuk.IsVisible, $"{s.Item2} kaydırma çubuğu görünmüyor; ölçü boş koşar.");
                return Yeri(cubuk, w).Left - Yeri(icerik, w).Right;
            }, sekme: s.Item1, dil: dil)))
            .ToArray();

        foreach (var (ad, bosluk) in bosluklar) _output.WriteLine($"{dil} {ad}: {bosluk:0.##}");
        var kucult = bosluklar[0].bosluk;
        Assert.True(kucult >= 1, $"Küçült'te bile içerik çubuğa dayanıyor ({kucult:0.##} px).");
        Assert.All(bosluklar, b => Assert.True(Math.Abs(b.bosluk - kucult) < 0.5, $"{b.ad} çubuktan {b.bosluk:0.##} px uzak, Küçült {kucult:0.##} px."));
    }

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    [InlineData("ar")]
    public void KuyrukDugmeSimgeleriOrtadaVeCumleBuyutulmez(string dil)
    {
        var (sapmalar, duraklatildi) = AppHost.Run(() =>
        {
            var kultur = System.Globalization.CultureInfo.CurrentUICulture;
            System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(dil);
            VidShrink.App.Localization.Strings.Use(dil);
            var ayar = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", Path.Combine(TipSources.Root, ".calisma", "yerlesim-denetimi", "yok", "settings.json"));
            var yollar = new[] { @"C:\Videolar\a.mp4", @"C:\Videolar\b.mov", @"C:\Videolar\c.mkv" };
            ShrinkJobWindow pencere;
            try { pencere = new ShrinkJobWindow(yollar, new VidShrink.Core.PlanOptions { TargetMb = 25 }, false, null) { Actions = new SahteSonEylem() }; }
            finally { Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", ayar); }
            try
            {
                pencere.Classes.Add("reduced-motion");
                pencere.SetPaused(true);
                pencere.Begin();
                pencere.Measure(new Size(420, double.PositiveInfinity));
                pencere.Arrange(new Rect(pencere.DesiredSize));
                pencere.UpdateLayout();
                DonusumleriSil(pencere);
                var liste = Ad<StackPanel>(pencere, "PendingList");
                var olcu = liste.GetVisualDescendants().OfType<Button>()
                    .Select(d => (ad: Avalonia.Automation.AutomationProperties.GetName(d), sapma: MurekkepSapmasi(d)))
                    .ToArray();
                return (olcu, Ad<TextBlock>(pencere, "TxtPaused").Text ?? "");
            }
            finally
            {
                pencere.Close();
                System.Globalization.CultureInfo.CurrentUICulture = kultur;
                VidShrink.App.Localization.Strings.Use("en");
            }
        });

        foreach (var (ad, sapma) in sapmalar) _output.WriteLine($"{ad}: {sapma:0.##}");
        _output.WriteLine(duraklatildi);
        Assert.Equal(9, sapmalar.Length);
        var enKotu = sapmalar.MaxBy(s => s.sapma);
        Assert.True(enKotu.sapma <= 1, $"{enKotu.ad}: simgenin mürekkebi düğmenin ortasından {enKotu.sapma:0.##} px kayık.");
        Assert.Equal(VidShrink.App.Localization.Strings.GetIn(dil, "main.shrink-job.paused"), duraklatildi.Replace("\u00A0", " ", StringComparison.Ordinal).Replace("\u2060", "", StringComparison.Ordinal));
    }

    private static double MurekkepSapmasi(Button dugme)
    {
        var w = (int)Math.Ceiling(dugme.Bounds.Width);
        var h = (int)Math.Ceiling(dugme.Bounds.Height);
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
        bitmap.Render(dugme);
        var pikseller = new byte[w * h * 4];
        var tutamak = System.Runtime.InteropServices.GCHandle.Alloc(pikseller, System.Runtime.InteropServices.GCHandleType.Pinned);
        try { bitmap.CopyPixels(new PixelRect(0, 0, w, h), tutamak.AddrOfPinnedObject(), pikseller.Length, w * 4); }
        finally { tutamak.Free(); }
        int Parlaklik(int x, int y) => pikseller[(y * w + x) * 4 + 2] + pikseller[(y * w + x) * 4 + 1] + pikseller[(y * w + x) * 4];
        var zemin = Parlaklik(3, h / 2);
        int sol = w, sag = -1, ust = h, alt = -1;
        for (var y = 2; y < h - 2; y++)
            for (var x = 2; x < w - 2; x++)
            {
                if (Math.Abs(Parlaklik(x, y) - zemin) < 150) continue;
                sol = Math.Min(sol, x); sag = Math.Max(sag, x); ust = Math.Min(ust, y); alt = Math.Max(alt, y);
            }
        if (sag < 0) return double.PositiveInfinity;
        return Math.Max(Math.Abs((sol + sag + 1) / 2.0 - w / 2.0), Math.Abs((ust + alt + 1) / 2.0 - h / 2.0));
    }

    [Theory]
    [InlineData("tr", 0)]
    [InlineData("tr", 1)]
    [InlineData("tr", 2)]
    [InlineData("de", 0)]
    [InlineData("en", 1)]
    [InlineData("ar", 0)]
    public void BirakmaBasligiTekSozcukleBitmez(string dil, int boyutNo)
    {
        var boyut = new[] { Dar, YerlesimDenetimiTests.DarBoyut, Genis }[boyutNo];
        var (metin, sonSatir, satir) = Pencere(boyut, w =>
        {
            var baslik = Ad<TextBlock>(w, "TxtDropTitle");
            var satirlar = baslik.TextLayout.TextLines;
            var son = satirlar[^1];
            var yazi = baslik.Text ?? "";
            return (yazi, yazi.Substring(son.FirstTextSourceIndex, Math.Min(son.Length, yazi.Length - son.FirstTextSourceIndex)).Trim(), satirlar.Count);
        }, sekme: 1, dil: dil, dolu: false);

        _output.WriteLine($"{satir} satır, '{metin}', son: '{sonSatir}'");
        Assert.False(string.IsNullOrEmpty(metin));
        Assert.True(satir == 1 || sonSatir.Split(new[] { ' ', ' ' }, StringSplitOptions.RemoveEmptyEntries).Length > 1,
            $"Bırakma başlığının son satırı tek sözcük: '{sonSatir}'.");
    }

    [Theory]
    [InlineData("tr", false)]
    [InlineData("tr", true)]
    [InlineData("de", false)]
    [InlineData("ar", false)]
    public void SimdiSatirlariDenetimSirasiylaHizali(string dil, bool enDar)
    {
        var boyut = enDar ? YerlesimDenetimiTests.DarBoyut : Dar;
        var sonuc = Pencere(boyut, w =>
        {
            w.RecalculateForTest();
            Ad<StackPanel>(w, "AdvancedBody").IsVisible = true;
            Yerlestir(w, boyut);
            return new[] { "TxtAdvModeNow", "TxtAdvCrfNow", "TxtAdvPresetNow", "TxtAdvTuneNow", "TxtAdvEncoderPathNow", "TxtAdvCodecLockNow" }
                .Select(ad => Ad<TextBlock>(w, ad))
                .Where(t => t.Bounds.Width > 0 && !string.IsNullOrEmpty(t.Text))
                .Select(t =>
                {
                    var yazi = Yeri(t, w);
                    var orta = yazi.Top + yazi.Height / 2;
                    var denetimler = t.FindAncestorOfType<Grid>()!.GetVisualDescendants()
                        .Where(d => d is RadioButton or ComboBox).Cast<Control>().Select(d => Yeri(d, w)).ToArray();
                    var fark = denetimler.Min(d => Math.Abs(d.Top + d.Height / 2 - orta));
                    var arada = orta < denetimler.Max(d => d.Bottom);
                    return (ad: t.Name!, fark: arada ? fark : 0, satir: denetimler.Select(d => Math.Round(d.Top)).Distinct().Count());
                })
                .ToArray();
        }, sekme: 1, dil: dil);

        foreach (var (ad, fark, satir) in sonuc) _output.WriteLine($"{ad}: denetim satırları arasında kalan kayma {fark:0.#} px, denetim satırı {satir}");
        Assert.Contains(sonuc, s => s.ad == "TxtAdvEncoderPathNow");
        var enKotu = sonuc.MaxBy(s => s.fark);
        Assert.True(enKotu.fark <= 1, $"{enKotu.ad} denetim satırlarının arasında, hiçbirinin hizasında değil: {enKotu.fark:0.#} px.");
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
