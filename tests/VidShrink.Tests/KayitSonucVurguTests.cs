using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using VidShrink.App;
using VidShrink.App.Recorder;
using VidShrink.Core.Share;
using ShareTarget = VidShrink.Core.Share.ShareTarget;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kayıt sonucu panelinin vurgusu ve paylaşım kapısı, çizilmiş pikselden ve ham fare tıkından.
/// Kanıt <c>.calisma/kayit-sonuc-vurgu/</c>.
/// </summary>
public sealed class KayitSonucVurguTests
{
    private const BindingFlags Her = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static readonly object Fare = Yeni(typeof(MouseDevice), new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), PointerType.Mouse, true));

    private static object Yeni(Type tur, params object?[] args) => Activator.CreateInstance(tur, Her, null, args, null)!;

    private static void Fareyle(TopLevel top, RawPointerEventType tur, Point nokta, RawInputModifiers tuslar)
    {
        var impl = typeof(TopLevel).GetProperty("PlatformImpl", Her)!.GetValue(top)!;
        var kok = (IInputRoot)typeof(TopLevel).GetProperty("InputRoot", Her)!.GetValue(top)!;
        var giris = (Action<RawInputEventArgs>)impl.GetType().GetInterfaces()
            .Select(i => i.GetProperty("Input", Her)).First(p => p is not null)!.GetValue(impl)!;
        giris((RawInputEventArgs)Yeni(typeof(RawPointerEventArgs), Fare, (ulong)Environment.TickCount64, kok, tur, nokta, tuslar));
    }

    private static string Klasor()
    {
        var yol = Path.Combine(GirdiKanit.Root, ".calisma", "kayit-sonuc-vurgu");
        Directory.CreateDirectory(yol);
        return yol;
    }

    private static string Kayit()
    {
        var dosya = Path.Combine(Klasor(), "kayit.mkv");
        File.WriteAllBytes(dosya, new byte[4096]);
        return dosya;
    }

    private static T Bul<T>(Control kok, string ad) where T : Control => kok.FindControl<T>(ad) ?? throw new InvalidOperationException(ad);

    private static (RecorderView view, Window window) Ac()
    {
        var view = new RecorderView { SkipAutoMeasure = true };
        var window = new Window { Width = 1100, Height = 900, Content = view };
        window.Show();
        view.ShowResult(new RecordResult(true, Kayit(), 1, false, 0, string.Empty, 1));
        window.UpdateLayout();
        Bekle(0.4);
        return (view, window);
    }

    private static void Bekle(double saniye)
    {
        var saat = System.Diagnostics.Stopwatch.StartNew();
        while (saat.Elapsed.TotalSeconds < saniye)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
    }

    /// <summary>Düğmenin çizilmiş pikselleri içinde palet fırçasına (±6) eşit olanların oranı.</summary>
    private static double FircaOrani(Button dugme, Color firca, string png)
    {
        var w = (int)Math.Ceiling(dugme.Bounds.Width);
        var h = (int)Math.Ceiling(dugme.Bounds.Height);
        using var bitmap = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
        bitmap.Render(dugme);
        bitmap.Save(png, PngBitmapEncoderOptions.Default);
        var stride = w * 4;
        var pixels = new byte[stride * h];
        var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try { bitmap.CopyPixels(new PixelRect(0, 0, w, h), pinned.AddrOfPinnedObject(), pixels.Length, stride); }
        finally { pinned.Free(); }

        var esit = 0;
        for (var i = 0; i < w * h; i++)
        {
            var b = pixels[i * 4];
            var g = pixels[(i * 4) + 1];
            var r = pixels[(i * 4) + 2];
            if (Math.Abs(r - firca.R) <= 6 && Math.Abs(g - firca.G) <= 6 && Math.Abs(b - firca.B) <= 6) esit++;
        }

        return (double)esit / (w * h);
    }

    /// <summary>
    /// R10/R14: "Küçült'e gönder" panelin birinci düğmesi ve paletin <c>NeonBlue</c> fırçasıyla dolu;
    /// "Klasörü göster" ve "Paylaş" ikincil, o fırça gövdelerinde yok. Negatif kontrol: iki düğmeye
    /// aynı tema verilince ölçü farkı göremez.
    /// </summary>
    [Fact]
    public void KucultBirincilKlasorVePaylasIkincilPikseldeAyrisir()
    {
        var (ilk, kucult, klasor, paylas, esitKucult, esitKlasor, firca) = AppHost.Run(() =>
        {
            var (view, window) = Ac();
            try
            {
                var renk = Application.Current!.TryGetResource("NeonBlue", ThemeVariant.Default, out var v) && v is ISolidColorBrush b
                    ? b.Color
                    : throw new InvalidOperationException("NeonBlue fırçası yok");
                var dugmeler = Bul<Button>(view, "BtnToShrink").Parent as Panel ?? throw new InvalidOperationException("panel");
                var birinci = dugmeler.Children.OfType<Button>().First(d => d.IsVisible).Name;
                var klasorde = Klasor();
                var k = FircaOrani(Bul<Button>(view, "BtnToShrink"), renk, Path.Combine(klasorde, "kucult.png"));
                var r = FircaOrani(Bul<Button>(view, "BtnReveal"), renk, Path.Combine(klasorde, "klasor.png"));
                var p = FircaOrani(Bul<Button>(view, "BtnRecShare"), renk, Path.Combine(klasorde, "paylas.png"));

                Bul<Button>(view, "BtnReveal").Theme = Bul<Button>(view, "BtnToShrink").Theme;
                window.UpdateLayout();
                var ek = FircaOrani(Bul<Button>(view, "BtnToShrink"), renk, Path.Combine(klasorde, "esit-kucult.png"));
                var er = FircaOrani(Bul<Button>(view, "BtnReveal"), renk, Path.Combine(klasorde, "esit-klasor.png"));
                return (birinci, k, r, p, ek, er, renk);
            }
            finally { window.Close(); }
        });

        File.WriteAllText(Path.Combine(Klasor(), "olcu.txt"),
            $"firca {firca}\nkucult {kucult:0.000}\nklasor {klasor:0.000}\npaylas {paylas:0.000}\nesit-kucult {esitKucult:0.000}\nesit-klasor {esitKlasor:0.000}\n");

        Assert.Equal("BtnToShrink", ilk);
        Assert.True(kucult > 0.5, $"Küçült düğmesinde fırça oranı {kucult:0.000}");
        Assert.True(klasor < 0.05, $"Klasör düğmesinde fırça oranı {klasor:0.000}");
        Assert.True(paylas < 0.05, $"Paylaş düğmesinde fırça oranı {paylas:0.000}");
        Assert.True(kucult - klasor > 0.45 && kucult - paylas > 0.45);

        Assert.True(Math.Abs(esitKucult - esitKlasor) < 0.1, $"eşit fırçada fark {esitKucult:0.000} / {esitKlasor:0.000}");
    }

    private sealed class SahteSaglayici : IShareProvider
    {
        public SahteSaglayici(ShareTarget target) => Target = target;

        public List<string> Yuklenen { get; } = new();

        public ShareTarget Target { get; }

        public bool CanDelete => false;

        public Task<ShareResult> UploadAsync(string filePath, int? retentionDays = null, IProgress<UploadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            Yuklenen.Add(filePath);
            return Task.FromResult(ShareResult.Success(new ShareLink(Target.Id, "f1", "https://ornek.test/f1", Path.GetFileName(filePath), DateTimeOffset.UtcNow)));
        }

        public Task<ShareResult> CheckHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ShareResult.Success(new ShareLink(Target.Id, "h", "https://ornek.test/h", "h", DateTimeOffset.UtcNow)));

        public Task<ShareResult> DeleteAsync(ShareLink link, CancellationToken cancellationToken = default)
            => Task.FromResult(ShareResult.Success(link));
    }

    /// <summary>
    /// R15: "Paylaş" düğmesine ham fare tıkı teslim edilen kaydı <see cref="ShareFlow"/>'un sağlayıcısına
    /// verir ve dönen bağlantı panelde görünür. Negatif kontrol: düğmenin dışına tıklamak yüklemez.
    /// </summary>
    [Fact]
    public void PaylasDugmesineHamTikKaydiSaglayicidanGecirirVeBaglantiyiGosterir()
    {
        var ledger = Path.Combine(Klasor(), "paylasimlar.json");
        var (disaridaYuklenen, yuklenen, baglanti, beklenen) = AppHost.Run(() =>
        {
            var (view, window) = Ac();
            try
            {
                SahteSaglayici? saglayici = null;
                view.CreateShareFlow = () => new ShareFlow(t => saglayici ??= new SahteSaglayici(t), new ShareLedger(ledger));
                var dugme = Bul<Button>(view, "BtnRecShare");
                dugme.BringIntoView();
                window.UpdateLayout();
                Bekle(0.3);

                var bos = new Point(2, 2);
                Fareyle(window, RawPointerEventType.Move, bos, RawInputModifiers.None);
                Fareyle(window, RawPointerEventType.LeftButtonDown, bos, RawInputModifiers.LeftMouseButton);
                Fareyle(window, RawPointerEventType.LeftButtonUp, bos, RawInputModifiers.None);
                var disarida = saglayici?.Yuklenen.Count ?? 0;

                var merkez = dugme.TranslatePoint(new Point(dugme.Bounds.Width / 2, dugme.Bounds.Height / 2), window)!.Value;
                Fareyle(window, RawPointerEventType.Move, merkez, RawInputModifiers.None);
                Fareyle(window, RawPointerEventType.LeftButtonDown, merkez, RawInputModifiers.LeftMouseButton);
                Fareyle(window, RawPointerEventType.LeftButtonUp, merkez, RawInputModifiers.None);
                var saat = System.Diagnostics.Stopwatch.StartNew();
                while (view.ShareLinkText != "https://ornek.test/f1" && saat.Elapsed.TotalSeconds < 5)
                {
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    Thread.Sleep(10);
                }

                return (disarida, saglayici?.Yuklenen.ToList() ?? new List<string>(), view.ShareLinkText, Path.Combine(Klasor(), "kayit.mkv"));
            }
            finally { window.Close(); }
        });

        var govde = new StringBuilder()
            .AppendLine($"disarida tik yukleme: {disaridaYuklenen}")
            .AppendLine($"yuklenen: {string.Join(", ", yuklenen)}")
            .AppendLine($"baglanti: {baglanti}");
        File.WriteAllText(Path.Combine(Klasor(), "paylas.txt"), govde.ToString());

        Assert.Equal(0, disaridaYuklenen);
        Assert.Equal(new[] { beklenen }, yuklenen);
        Assert.Equal("https://ornek.test/f1", baglanti);
    }
}
