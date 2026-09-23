using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Ffmpeg;

namespace VidShrink.Tests;

public sealed class GorselGozTheoryAttribute : TheoryAttribute
{
    public GorselGozTheoryAttribute()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase))
            Skip = "Görsel göz kareleri yalnız yerelde çekilir.";
        else if (Environment.GetEnvironmentVariable("VIDSHRINK_GORSEL_GOZ") is not { Length: > 0 })
            Skip = "VIDSHRINK_GORSEL_GOZ=<klasör adı> verilince çeker.";
    }
}

/// <summary>
/// Gözle denetim için kare çeker: sayısal denetçinin göremediğini (üst üste binen çerçeve,
/// sağdan sola ters dönen yerleşim, okunmayan kontrast) PNG'de bakarak bulmak için. Ölçmez;
/// yalnız kare sayısını sınar. <c>VIDSHRINK_GORSEL_GOZ</c> kareleri
/// <c>.calisma/t0-gorsel-goz/&lt;değer&gt;/</c> altına yazar.
/// </summary>
public sealed class GorselGozTests
{
    private const string Kaynak = @"C:\Kayitlar\tatil-cekimi-2160p60.mkv";

    private static string Klasor()
    {
        var klasor = Path.Combine(TipSources.Root, ".calisma", "t0-gorsel-goz", Environment.GetEnvironmentVariable("VIDSHRINK_GORSEL_GOZ")!);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    public static TheoryData<string, int, int> AnaKollar()
    {
        var kollar = new TheoryData<string, int, int>();
        foreach (var dil in new[] { "tr", "en", "de", "ar" })
            foreach (var (en, boy) in new[] { (1136, 720), (1560, 1060) })
                kollar.Add(dil, en, boy);
        kollar.Add("tr", 1024, 720);
        return kollar;
    }

    public static TheoryData<string> Diller() => new() { "tr", "en", "de", "ar" };

    private static readonly string[] SekmeAdlari = { "oynatici", "kucult", "donustur", "kaydedici", "gelismis", "ayarlar" };

    [GorselGozTheory]
    [MemberData(nameof(AnaKollar))]
    public void AnaPencere(string dil, int en, int boy)
    {
        var klasor = Klasor();
        var boyut = new Size(en, boy);
        var sayi = 0;
        foreach (var durum in new[] { "bos", "yuklu", "sonuc", "asim" })
            sayi += AppHost.Run(() =>
            {
                Strings.Use(dil);
                var pencere = new MainWindow();
                try
                {
                    pencere.Classes.Add("reduced-motion");
                    typeof(MainWindow).GetField("_motionReduced", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(pencere, true);
                    pencere.RestoreSettingsForTest(new UpdateSettings());
                    var uygulamaAyari = typeof(MainWindow).GetMethod("RestoreAppSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                    uygulamaAyari.Invoke(pencere, new[] { Activator.CreateInstance(uygulamaAyari.GetParameters()[0].ParameterType) });
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    if (durum != "bos") pencere.LoadWithoutProbing(Kaynak, Ornek());
                    pencere.SettleFades();
                    pencere.TabAdvanced.IsVisible = true;
                    if (durum == "sonuc") Sonuc(pencere);
                    if (durum == "asim") Asim(pencere);
                    return Sekmeler(pencere, boyut, klasor, $"{dil}-{en}x{boy}-{durum}", durum is "bos" or "yuklu" ? null : pencere.TabShrink);
                }
                finally
                {
                    pencere.Close();
                    Strings.Use("en");
                }
            });
        Assert.True(sayi >= 8, $"yalnız {sayi} kare");
    }

    private static void Sonuc(MainWindow pencere)
    {
        pencere.TxtOutSize.Text = Strings.Get("main.unit.mb-value").Replace("{0}", "15.2", StringComparison.Ordinal);
        pencere.TxtResult.Text = string.Format(CultureInfo.InvariantCulture, Strings.Get("main.run.done"), 3, "420.0", "15.2", "96.4")
            + " " + string.Format(CultureInfo.InvariantCulture, Strings.Get("main.run.hdr10plus-short"), 11, 12);
        pencere.BtnReveal.IsVisible = true;
        pencere.ResetShareForTest(true);
        pencere.TxtShareLink.Text = "https://example.invalid/d/7f3a9c2e41b8/tatil-cekimi-2160p60-vidshrink.mp4";
        pencere.ShareLinkRow.IsVisible = true;
    }

    private static void Asim(MainWindow pencere)
    {
        var kesimler = new[]
        {
            new TrimPlan(TrimSide.End, 0, 137.2, 187.5, 16_000_000),
            new TrimPlan(TrimSide.Start, 50.3, 187.5, 187.5, 16_000_000),
            new TrimPlan(TrimSide.Both, 25.1, 162.3, 187.5, 16_000_000)
        };
        pencere.ShowEncodeProgressForTest(new EncodeProgress(0.42, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(71), 12.3, "pass 2/2 (attempt 3)"));
        _ = pencere.ShowRetryAskForTest(new RetryPrompt(3, 3, 16, 16.4, TimeSpan.FromSeconds(30), true, 15.2, kesimler));
        pencere.BtnRetryTrim.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static int Sekmeler(MainWindow pencere, Size boyut, string klasor, string onek, TabItem? yalniz)
    {
        var sekmeler = pencere.Tabs;
        var acilan = new HashSet<Button>();
        var sayi = 0;
        Yerlestir(pencere, boyut);
        Duzle(pencere);
        for (var sira = 0; sira < sekmeler.ItemCount; sira++)
        {
            if (sekmeler.ContainerFromIndex(sira) is not TabItem { IsVisible: true } oge) continue;
            if (yalniz is not null && oge != yalniz) continue;
            sekmeler.SelectedIndex = sira;
            Yerlestir(pencere, boyut);
            foreach (var dugme in pencere.GetVisualDescendants().OfType<Button>()
                         .Where(d => d.IsShown() && d.Name is { } ad && ad.EndsWith("Toggle", StringComparison.Ordinal) && acilan.Add(d))
                         .ToList())
                dugme.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Duzle(pencere);
            Yerlestir(pencere, boyut);
            pencere.SettleFades();
            Yerlestir(pencere, boyut);

            var ad = $"{onek}-{sira}{SekmeAdlari[Math.Min(sira, SekmeAdlari.Length - 1)]}";
            var kaydirici = pencere.GetVisualDescendants().OfType<ScrollViewer>()
                .Where(s => s.IsShown() && s.Extent.Height > s.Viewport.Height + 1 && s.Viewport.Height > boyut.Height / 3)
                .OrderByDescending(s => s.Viewport.Height * s.Viewport.Width)
                .FirstOrDefault();
            if (kaydirici is null)
            {
                Kaydet(pencere, boyut, Path.Combine(klasor, ad + ".png"));
                sayi++;
                continue;
            }

            var adim = kaydirici.Viewport.Height * 0.85;
            var parca = 0;
            for (var ofset = 0.0; parca < 5; ofset += adim, parca++)
            {
                var son = ofset >= kaydirici.Extent.Height - kaydirici.Viewport.Height;
                kaydirici.Offset = new Vector(0, Math.Min(ofset, kaydirici.Extent.Height - kaydirici.Viewport.Height));
                Yerlestir(pencere, boyut);
                Kaydet(pencere, boyut, Path.Combine(klasor, $"{ad}-k{parca}.png"));
                sayi++;
                if (son) break;
            }
            kaydirici.Offset = default;
        }
        return sayi;
    }

    private static void Yerlestir(MainWindow pencere, Size boyut)
    {
        BassizYerlesim.PlatformBoyu(pencere, boyut);
        pencere.Width = double.NaN;
        pencere.Height = double.NaN;
        pencere.Measure(boyut);
        pencere.Arrange(new Rect(boyut));
        pencere.UpdateLayout();
        var kok = (Layoutable)pencere.GetVisualChildren().Single();
        for (var gecis = 0; gecis < 3; gecis++)
        {
            foreach (var dugum in pencere.GetVisualDescendants().OfType<Layoutable>()) dugum.InvalidateMeasure();
            kok.InvalidateMeasure();
            kok.Measure(boyut);
            kok.Arrange(new Rect(boyut));
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Duzle(Window pencere)
    {
        foreach (var gecis in pencere.GetVisualDescendants().OfType<TransitioningContentControl>()) gecis.PageTransition = null;
        foreach (var parca in pencere.GetVisualDescendants().OfType<Animatable>()) parca.Transitions = null;
        foreach (var panel in pencere.GetLogicalDescendants().OfType<Control>().ToList())
        {
            if (!panel.Classes.Contains("enter") && !panel.Classes.Contains("enter-flat")) continue;
            panel.Transitions = null;
            panel.Classes.Remove("enter");
            panel.Classes.Remove("enter-flat");
        }
    }

    private static void Kaydet(Window pencere, Size boyut, string yol)
    {
        var kok = (Control)pencere.GetVisualChildren().Single();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)boyut.Width, (int)boyut.Height), new Vector(96, 96));
        bitmap.Render(kok);
        bitmap.Save(yol, PngBitmapEncoderOptions.Default);
    }

    private static void KaydetIcerik(Window pencere, string yol)
    {
        pencere.Measure(new Size(420, double.PositiveInfinity));
        pencere.Arrange(new Rect(pencere.DesiredSize));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var kok = (Layoutable)pencere.GetVisualChildren().Single();
        for (var tur = 0; tur < 3; tur++)
        {
            foreach (var dugum in pencere.GetVisualDescendants().OfType<Layoutable>()) dugum.InvalidateMeasure();
            kok.InvalidateMeasure();
            kok.Measure(new Size(420, double.PositiveInfinity));
            kok.Arrange(new Rect(new Size(420, kok.DesiredSize.Height)));
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        foreach (var dugum in pencere.GetVisualDescendants().OfType<Visual>()) dugum.RenderTransform = null;
        var boy = (int)Math.Ceiling(kok.DesiredSize.Height);
        using var bitmap = new RenderTargetBitmap(new PixelSize(420, boy), new Vector(96, 96));
        bitmap.Render((Visual)kok);
        bitmap.Save(yol, PngBitmapEncoderOptions.Default);
    }

    private sealed class SahteSonEylem : IQueueEndActions
    {
        public void Reveal(string path) { }
        public void Sleep() { }
        public void PowerOff() { }
    }

    [GorselGozTheory]
    [MemberData(nameof(Diller))]
    public void IsPencereleri(string dil)
    {
        var klasor = Klasor();
        var sayi = AppHost.Run(() =>
        {
            var kultur = CultureInfo.CurrentUICulture;
            var ayar = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
            CultureInfo.CurrentUICulture = new CultureInfo(dil);
            Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", Path.Combine(TipSources.Root, ".calisma", "t0-gorsel-goz", "appdata-yok", "settings.json"));
            var pencereler = new List<Window>();
            var n = 0;
            try
            {
                var yollar = new[]
                {
                    @"C:\Videolar\2026-09-22 Yaz tatili — Kapadokya balon turu, gün doğumu (tam uzunluk).mp4",
                    @"C:\Videolar\kisa.mov",
                    @"C:\Videolar\Konferans_kaydi_oturum_3_soru_cevap_bolumu_duzenlenmemis_ham_goruntu.mkv"
                };
                var kuyruk = new ShrinkJobWindow(yollar, new PlanOptions { TargetMb = 25 }, false, null) { Actions = new SahteSonEylem() };
                pencereler.Add(kuyruk);
                kuyruk.Classes.Add("reduced-motion");
                kuyruk.SetPaused(true);
                kuyruk.Begin();
                KaydetIcerik(kuyruk, Path.Combine(klasor, $"{dil}-kuyruk-duraklatildi.png")); n++;
                while (kuyruk.Pending.Count > 0) kuyruk.RemovePending(0);
                kuyruk.SetPaused(false);
                kuyruk.WhenDone = QueueEndChoice.PowerOff;
                kuyruk.QueueDrained();
                KaydetIcerik(kuyruk, Path.Combine(klasor, $"{dil}-kuyruk-geri-sayim.png")); n++;
                kuyruk.CancelCountdown();

                var tek = new ShrinkJobWindow(new ShellShrinkStartup(new[] { new ShrinkRequest(25, yollar[0]) }, null, yollar[0]), null) { Actions = new SahteSonEylem() };
                pencereler.Add(tek);
                tek.Classes.Add("reduced-motion");
                tek.Progress.IsVisible = true;
                tek.RowFacts.IsVisible = true;
                tek.TxtHeadline.Text = Path.GetFileName(yollar[0]);
                tek.TxtTarget.Text = Strings.Get("main.shrink-job.target").Replace("{0}", ShellIntegration.FormatQuickShrinkLabel(25), StringComparison.Ordinal).Replace("{1}", "1", StringComparison.Ordinal).Replace("{2}", "1", StringComparison.Ordinal);
                tek.ShowProgress(new EncodeProgress(0.42, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(71), 12.3, "pass 2/2 (attempt 3)"));
                KaydetIcerik(tek, Path.Combine(klasor, $"{dil}-sagtik-kosuyor.png")); n++;
                tek.Progress.Value = 1;
                tek.TxtMessage.Text = Strings.Get("main.run.cancelled");
                tek.BtnReveal.IsVisible = true;
                tek.BtnOpenInApp.IsVisible = true;
                KaydetIcerik(tek, Path.Combine(klasor, $"{dil}-sagtik-bitti.png")); n++;

                var sorun = new ShrinkJobWindow(new ShellShrinkStartup(null, ShrinkArgumentProblem.TargetNotInQuickList, yollar[1]), null);
                pencereler.Add(sorun);
                sorun.Classes.Add("reduced-motion");
                sorun.Begin();
                KaydetIcerik(sorun, Path.Combine(klasor, $"{dil}-sagtik-sorun.png")); n++;
                return n;
            }
            finally
            {
                foreach (var p in pencereler) p.Close();
                Environment.SetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH", ayar);
                CultureInfo.CurrentUICulture = kultur;
                Strings.Use("en");
            }
        });
        Assert.Equal(5, sayi);
    }

    private static MediaInfo Ornek() => new()
    {
        FilePath = Kaynak,
        FileSizeBytes = 420_000_000L,
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
}
