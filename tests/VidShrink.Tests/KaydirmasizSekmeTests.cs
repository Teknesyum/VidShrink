using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using Xunit.Abstractions;

namespace VidShrink.Tests;

/// <summary>
/// Tam ekran 1080p pencerede (görev çubuğu düşülmüş çalışma alanı 1920x1040) her sekmenin
/// sayfası kaydırmasız sığar. 28 Eylül 2026 arayüz turunun ilk ölçüsü: Ayarlar tek sütunlu
/// yığında 4926 px, görünen 988 px (4,99 ekran); kart duvarından sonra 988 px.
/// <para>Her sekme için sayfa kaydırıcısının içerik boyu, görünür boyu ve oranı
/// <c>.calisma/kaydirmasiz/</c> altına yazılır; ayrıca <c>Panel</c> temalı her kutunun
/// dolu oranı (içeriğin istediği boy / kutunun boyu) dökülür, yarısı boş kutular listelenir.
/// Karşılaştırma panelinin sahnesi sayılmaz: içeriği görüntünün kendisi, başsızda kare yok.</para>
/// <para>Varsayılan uygulama önerisi açık ölçülür: bildirim yığını içeriği iter ve ilk açılışta
/// kullanıcı onu görür. Gerçek pencerede öneri şeridiyle Ayarlar'ın dördüncü sütunu kesildi,
/// başsız ölçü şeritsiz olduğu için 988/988 diyordu.</para>
/// <para>Paylaşım hedefi kartı ve varsayılan ayarlar elle yüklenir: ikisi de yalnız <c>Loaded</c>'da
/// koşuyor. Gösterilmeyen başsız pencerede tavan, ömür listesi ve silme notu boş kalıyor, boş çıktı adı
/// da ilk açılışta olmayan "Ad boş kalamaz" satırını gösteriyordu.</para>
/// <para>%125 ve %150 satırları aynı ekranı mantıksal boyutla ölçer (1536x832, 1280x693): Win32'de
/// <c>AVALONIA_GLOBAL_SCALE_FACTOR</c> işlemiyor, ölçek ancak pencerenin mantıksal boyuna
/// yansıyarak sınanabiliyor. Kapı %100'de: ölçekli satırlarda kaydırma oranı dökülür, boş kutu yine kusurdur.</para>
/// </summary>
public sealed class KaydirmasizSekmeTests
{
    private readonly ITestOutputHelper _output;

    public KaydirmasizSekmeTests(ITestOutputHelper output) => _output = output;

    internal static readonly Size TamEkran = new(1920, 1040);

    private const string SamplePath = @"C:\Kayitlar\tatil-cekimi-2160p60.mkv";

    private sealed record Olcu(string Sekme, double Icerik, double Gorunen, List<string> BosKutular, List<string> Kartlar)
    {
        public bool Tasiyor => Icerik > Gorunen + 1;
    }

    /// <summary>
    /// Ölçü Kaydedici'yi Gelişmiş'e alıyor ve ana pencere kendi ayar dosyalarına yazıyor. Ertelenmiş
    /// yazma boşaltılıp iki dosya eski baytlarına döner; yoksa sonraki sınıfın kaydedicisi Gelişmiş
    /// açılıyordu (tur10'da YerlesimDenetimi 129 kırmızı).
    /// </summary>
    internal static T AyarlariKorurken<T>(Func<T> olc)
    {
        var yollar = new[] { UpdateSettings.DefaultPath, VidShrink.App.Recorder.RecorderSettings.FilePath }
            .OfType<string>().ToArray();
        var onceki = yollar.ToDictionary(y => y, y => File.Exists(y) ? File.ReadAllBytes(y) : null);
        try
        {
            if (VidShrink.App.Recorder.RecorderSettings.FilePath is { } kaydedici && File.Exists(kaydedici)) File.Delete(kaydedici);
            return olc();
        }
        finally
        {
            AppHost.Run(() =>
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                return 0;
            });
            foreach (var (yol, bayt) in onceki)
            {
                if (bayt is not null) File.WriteAllBytes(yol, bayt);
                else if (File.Exists(yol)) File.Delete(yol);
            }
        }
    }

    [Theory]
    [InlineData("tr", false, 100)]
    [InlineData("tr", true, 100)]
    [InlineData("en", false, 100)]
    [InlineData("en", true, 100)]
    [InlineData("tr", true, 125)]
    [InlineData("en", true, 125)]
    [InlineData("tr", true, 150)]
    public void TamEkrandaSekmelerKaydirmasizSigar(string dil, bool gelismis, int olcek)
    {
        var boyut = new Size(Math.Floor(TamEkran.Width * 100 / olcek), Math.Floor(TamEkran.Height * 100 / olcek));
        var olculer = AyarlariKorurken(() => AppHost.Run(() =>
        {
            Strings.Use(dil);
            var pencere = new MainWindow();
            try
            {
                pencere.Classes.Add("reduced-motion");
                pencere.LoadWithoutProbing(SamplePath, Sample());
                typeof(MainWindow).GetMethod("InitializeShareUi", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(pencere, null);
                pencere.RestoreAppSettingsForTest(new AppSettings());
                var oneriAyari = Path.Combine(TestPaths.OutputRoot, "kaydirmasiz-oneri", "settings.json");
                ((Panel)pencere.AppliedNotice.Parent!).Children.Add(new VidShrink.App.Integration.DefaultAppSuggestionBar(oneriAyari));
                pencere.SettleFades();
                var kaydedici = pencere.RecorderPaneForTest;
                ((RadioButton)kaydedici.FindControl<RadioButton>(gelismis ? "RadAdvanced" : "RadSimple")!).IsChecked = true;
                return Olc(pencere, boyut, $"{dil}-{(gelismis ? "gelismis" : "basit")}-{olcek}", olcek / 100.0);
            }
            finally
            {
                pencere.Close();
                Strings.Use("en");
            }
        }));

        var klasor = Path.Combine(TipSources.Root, ".calisma", "kaydirmasiz");
        var ad = $"{dil}-{(gelismis ? "gelismis" : "basit")}-{olcek}.txt";
        Directory.CreateDirectory(klasor);
        var dokum = new StringBuilder().AppendLine($"{dil} {boyut.Width:0}x{boyut.Height:0} (%{olcek}) kaydedici {(gelismis ? "gelismis" : "basit")}");
        foreach (var o in olculer)
        {
            dokum.AppendLine($"{o.Sekme}\ticerik {o.Icerik:0}\tgorunen {o.Gorunen:0}\toran {o.Icerik / Math.Max(1, o.Gorunen):0.00}{(o.Tasiyor ? "\tKAYIYOR" : "")}");
            foreach (var bos in o.BosKutular) dokum.AppendLine("  bos\t" + bos);
            foreach (var kart in o.Kartlar) dokum.AppendLine("  " + kart);
        }
        var metin = dokum.ToString();
        File.WriteAllText(Path.Combine(klasor, ad), metin);
        _output.WriteLine(metin);

        Assert.True(olculer.Count >= 4, $"yalnız {olculer.Count} sayfa ölçüldü");
        if (olcek == 100) Assert.DoesNotContain(olculer, o => o.Tasiyor);
        Assert.DoesNotContain(olculer, o => o.BosKutular.Count > 0);
    }

    private static List<Olcu> Olc(MainWindow pencere, Size boyut, string etiket, double olcek)
    {
        var olculer = new List<Olcu>();
        var sekmeler = pencere.Tabs;
        Yerlestir(pencere, boyut);
        var ogeler = sekmeler.Items.OfType<TabItem>().ToList();
        for (var sira = 0; sira < ogeler.Count; sira++)
        {
            var oge = ogeler[sira];
            if (!oge.IsVisible) continue;
            sekmeler.SelectedIndex = sira;
            Yerlestir(pencere, boyut);
            if (oge.Content is not ScrollViewer sayfa) continue;
            if (Environment.GetEnvironmentVariable("KAYDIRMASIZ_CEK") == "1") Cek(pencere, boyut, olcek, $"{etiket}-{sira}");
            var bos = new List<string>();
            foreach (var kutu in sayfa.GetVisualDescendants().OfType<Border>()
                         .Where(b => b.IsShown() && b.Theme is { } t && ReferenceEquals(t, pencere.FindResource("Panel")) && b.Child is Layoutable)
                         .Where(b => b.FindAncestorOfType<VidShrink.App.Playback.ComparisonPanel>() is null))
            {
                var ic = (Layoutable)kutu.Child!;
                var dolu = ic.DesiredSize.Height + kutu.Padding.Top + kutu.Padding.Bottom;
                var boy = kutu.Bounds.Height;
                if (boy > 160 && dolu / boy < 0.5)
                    bos.Add($"{kutu.Name ?? Ad(kutu)} boy {boy:0} dolu {dolu:0} oran {dolu / boy:0.00}");
            }
            var kartlar = new List<string>();
            if (sayfa.Content is Panel { } kok)
                foreach (var kart in kok.Children.OfType<Control>().Where(c => c.IsVisible))
                    kartlar.Add($"kart {kart.Name ?? kart.GetType().Name} {kart.Bounds.Width:0}x{kart.Bounds.Height:0} istenen {kart.DesiredSize.Height:0}");
            olculer.Add(new Olcu(Convert.ToString(oge.Header) ?? sira.ToString(), sayfa.Extent.Height, sayfa.Viewport.Height, bos, kartlar));
        }
        return olculer;
    }

    /// <summary>
    /// Sekmenin görüntüsü, ölçek DPI olarak verilir: %125'te 1536x832 mantıksal pencere
    /// 1920x1040 piksele çizilir. Yalnız <c>KAYDIRMASIZ_CEK=1</c> ile; gözle inceleme içindir.
    /// Ölçekli çekimde gölge bastırılır: DPI ≠ 96'da gölge katmanı çocukları kırpıyor (27 Eylül denetimi).
    /// Pencerenin kendisi değil kök görseli çizilir, zemin sonra altına serilir: pencereyi çizen
    /// ilk sürümün 28 karesinin her pikseli saydamdı. Giriş canlandırmaları durdurulur: başsızda
    /// saat ilerlemiyor, Küçült'ün yan sütunları opaklık 0'da kalıp boş sayfa gibi çiziliyordu.
    /// Başlık şeridinin kademesi <see cref="Yerlestir"/>'de elle koşturulur: başsız pencere
    /// <c>SizeChanged</c> atmıyor, %150 karesinde dil düğmeleri Ayarlar sekmesinin üstüne çiziliyordu.
    /// </summary>
    private static void Cek(MainWindow pencere, Size boyut, double olcek, string ad)
    {
        var klasor = Path.Combine(TipSources.Root, ".calisma", "kaydirmasiz", "cekim");
        Directory.CreateDirectory(klasor);
        foreach (var n in new Visual[] { pencere }.Concat(pencere.GetVisualDescendants()))
        {
            if (n is Control c)
            {
                c.Transitions = null;
                c.Classes.Remove("enter");
                c.Classes.Remove("enter-flat");
            }
            if (n.RenderTransform is not Avalonia.Media.TranslateTransform) n.RenderTransform = null;
        }
        pencere.SettleFades();
        Yerlestir(pencere, boyut);
        var bastirma = olcek == 1.0 ? new List<IDisposable>() : pencere.GetVisualDescendants().OfType<Border>()
            .Select(bd => bd.SetValue(Border.BoxShadowProperty, default(Avalonia.Media.BoxShadows), Avalonia.Data.BindingPriority.Animation))
            .OfType<IDisposable>().ToList();
        var px = new PixelSize((int)Math.Round(boyut.Width * olcek), (int)Math.Round(boyut.Height * olcek));
        using var katman = new Avalonia.Media.Imaging.RenderTargetBitmap(px, new Vector(96 * olcek, 96 * olcek));
        katman.Render((Visual)pencere.GetVisualChildren().Single());
        using var akis = new MemoryStream();
        katman.Save(akis, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        akis.Position = 0;
        using var duz = new Avalonia.Media.Imaging.Bitmap(akis);
        using var bmp = new Avalonia.Media.Imaging.RenderTargetBitmap(px, new Vector(96, 96));
        using (var ctx = bmp.CreateDrawingContext())
        {
            var alan = new Rect(0, 0, px.Width, px.Height);
            if (pencere.Background is { } zemin) ctx.FillRectangle(zemin, alan);
            ctx.DrawImage(duz, alan);
        }
        bmp.Save(Path.Combine(klasor, ad + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        foreach (var d in bastirma) d.Dispose();
    }

    private static string Ad(Visual v)
    {
        for (var d = v.GetVisualParent(); d is not null; d = d.GetVisualParent())
            if (d is Control { Name: { Length: > 0 } ad }) return "(" + ad + " içinde)";
        return v.GetType().Name;
    }

    private static void Yerlestir(MainWindow pencere, Size boyut)
    {
        pencere.Width = double.NaN;
        pencere.Height = double.NaN;
        pencere.Measure(boyut);
        pencere.Arrange(new Rect(boyut));
        pencere.UpdateLayout();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        pencere.SettleFades();
        var kok = (Layoutable)pencere.GetVisualChildren().Single();
        for (var tur = 0; tur < 3; tur++)
        {
            foreach (var dugum in pencere.GetVisualDescendants().OfType<Layoutable>()) dugum.InvalidateMeasure();
            kok.InvalidateMeasure();
            kok.Measure(boyut);
            kok.Arrange(new Rect(boyut));
            typeof(MainWindow).GetMethod("AlignTabsToTitle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(pencere, null);
        }
        kok.Measure(boyut);
        kok.Arrange(new Rect(boyut));
    }

    private static MediaInfo Sample() => new()
    {
        FilePath = SamplePath,
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
