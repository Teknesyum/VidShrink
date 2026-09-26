using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using VidShrink.App;
using VidShrink.App.Recorder;
using VidShrink.App.Share;
using VidShrink.App.Themes;
using VidShrink.Core;
using VidShrink.Core.Share;
using VidShrink.Ffmpeg;
using Xunit;
using ShareTarget = VidShrink.Core.Share.ShareTarget;
using ZxingFormat = ZXing.BarcodeFormat;
using ZxingHint = ZXing.DecodeHintType;

namespace VidShrink.Tests;

/// <summary>
/// Paylaşım bağlantısının QR kodu (<see cref="ShareQrCode"/>): çizilen piksel bir QR çözücüyle
/// (ZXing.Net, yalnız bu projede) geri çözülür ve aynı adres okunur. Bütün paletlerde QR açık
/// plaka üstünde koyu modülle çizilir ve karşıtlık 7:1'in altına inmez. Kaydedicide QR yalnız
/// bağlantı hazırken görünür; yüklenirken ve hatada görünmez, bağlantı değişince yenilenir.
/// Ağa çıkılmaz: sağlayıcı sahte. Kanıt <c>.calisma/paylasim-qr/</c>.
/// </summary>
public sealed class PaylasimQrTests
{
    private const string Kisa = "https://ornek.test/f1";
    private const string Orta = "https://litterbox.catbox.moe/ab12cd.mp4";
    private static readonly string Uzun = "https://ornek.test/paylas/" + new string('x', 180) + "?sure=72h&ad=kay%C4%B1t.mp4";

    private static string Klasor()
    {
        var yol = Path.Combine(GirdiKanit.Root, ".calisma", "paylasim-qr");
        Directory.CreateDirectory(yol);
        return yol;
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

    private static void Bekle(Func<bool> kosul)
    {
        var saat = System.Diagnostics.Stopwatch.StartNew();
        while (!kosul() && saat.Elapsed.TotalSeconds < 20)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }

    private sealed record Cizim(int Genislik, int Yukseklik, byte[] Bgra)
    {
        public Color Piksel(int x, int y)
        {
            var i = ((y * Genislik) + x) * 4;
            return Color.FromArgb(Bgra[i + 3], Bgra[i + 2], Bgra[i + 1], Bgra[i]);
        }
    }

    private static Cizim Ciz(Control denetim, string? png = null)
    {
        var w = (int)Math.Ceiling(denetim.Bounds.Width);
        var h = (int)Math.Ceiling(denetim.Bounds.Height);
        Assert.True(w > 0 && h > 0, $"denetim boyutsuz: {denetim.Bounds}");
        using var bitmap = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
        bitmap.Render(denetim);
        if (png is not null) bitmap.Save(png, PngBitmapEncoderOptions.Default);
        var stride = w * 4;
        var pixels = new byte[stride * h];
        var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try { bitmap.CopyPixels(new PixelRect(0, 0, w, h), pinned.AddrOfPinnedObject(), pixels.Length, stride); }
        finally { pinned.Free(); }
        return new Cizim(w, h, pixels);
    }

    private static string? Coz(Cizim cizim)
    {
        var kaynak = new ZXing.RGBLuminanceSource(cizim.Bgra, cizim.Genislik, cizim.Yukseklik, ZXing.RGBLuminanceSource.BitmapFormat.BGRA32);
        var ipucu = new Dictionary<ZxingHint, object>
        {
            [ZxingHint.POSSIBLE_FORMATS] = new List<ZxingFormat> { ZxingFormat.QR_CODE },
            [ZxingHint.TRY_HARDER] = true
        };
        var sonuc = new ZXing.MultiFormatReader().decode(new ZXing.BinaryBitmap(new ZXing.Common.HybridBinarizer(kaynak)), ipucu);
        return sonuc?.Text;
    }

    private static Cizim Ters(Cizim cizim)
    {
        var ters = (byte[])cizim.Bgra.Clone();
        for (var i = 0; i < ters.Length; i += 4)
        {
            ters[i] = (byte)(255 - ters[i]);
            ters[i + 1] = (byte)(255 - ters[i + 1]);
            ters[i + 2] = (byte)(255 - ters[i + 2]);
        }

        return cizim with { Bgra = ters };
    }

    private static double Modul(Control kok) =>
        kok.TryFindResource("ShareQrModuleSize", out var v) && v is double d ? d : throw new InvalidOperationException("ShareQrModuleSize yok");

    private static (ShareQrCode qr, Window window) Tek(string link)
    {
        var qr = new ShareQrCode { Link = link, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var window = new Window { Width = 600, Height = 600, Content = new Border { Child = qr } };
        window.Show();
        qr.ModuleSize = Modul(qr);
        window.UpdateLayout();
        return (qr, window);
    }

    /// <summary>
    /// Kenardan içeri <see cref="ShareQr.QuietModules"/> modül boyunca her piksel plaka rengi mi;
    /// değilse ilk sapan pikselin yeri.
    /// </summary>
    private static string? SessizBolgeSapmasi(Cizim cizim, double modul, Color plaka)
    {
        var bant = (int)Math.Floor(ShareQr.QuietModules * modul);
        for (var y = 0; y < cizim.Yukseklik; y++)
            for (var x = 0; x < cizim.Genislik; x++)
            {
                var kenarda = x < bant || y < bant || x >= cizim.Genislik - bant || y >= cizim.Yukseklik - bant;
                if (kenarda && cizim.Piksel(x, y) != plaka) return $"({x},{y}) {cizim.Piksel(x, y)} != {plaka}";
            }

        return null;
    }

    /// <summary>
    /// Kısa, tipik ve 200+ karakterlik üç adres çizilir ve ZXing ile geri çözülür; okunan adres
    /// yazılanla aynı. Kenarda dört modüllük sessiz bölge plaka renginde. Negatif kontrol: aynı
    /// piksellerin tersi (açık modül koyu zemin) aynı çözücüyle okunmuyor, yani ölçü "her şeyi
    /// okuyan" bir çözücüye yaslanmıyor.
    /// </summary>
    [Fact]
    public void CizilenQrAyniBaglantiyaGeriCozulur()
    {
        var sonuc = AppHost.Run(() =>
        {
            PaletteCatalog.Use(PaletteCatalog.Default);
            var satirlar = new List<(string link, string? okunan, string? ters, string? sessiz, int kenar)>();
            foreach (var link in new[] { Kisa, Orta, Uzun })
            {
                var (qr, window) = Tek(link);
                try
                {
                    var cizim = Ciz(qr, Path.Combine(Klasor(), $"qr-{link.Length}.png"));
                    var plaka = cizim.Piksel(0, 0);
                    satirlar.Add((link, Coz(cizim), Coz(Ters(cizim)), SessizBolgeSapmasi(cizim, qr.ModuleSize, plaka), qr.Matrix!.GetLength(0)));
                }
                finally { window.Close(); }
            }

            return satirlar;
        });

        var govde = new StringBuilder();
        foreach (var s in sonuc) govde.AppendLine($"{s.link.Length}\tkenar {s.kenar}\tokunan {s.okunan}\tters {s.ters ?? "-"}\tsessiz {s.sessiz ?? "tamam"}");
        File.WriteAllText(Path.Combine(Klasor(), "cozum.txt"), govde.ToString());

        foreach (var s in sonuc)
        {
            Assert.Equal(s.link, s.okunan);
            Assert.Null(s.ters);
            Assert.Null(s.sessiz);
        }

        KanitKapanisi.Kapat(Klasor(), "cozum.txt", "qr-*.png");
    }

    /// <summary>
    /// Bütün paletler: QR paletin açık belirteci üstünde koyu modülle çizilir, çizilmiş plaka ve
    /// modül pikselinin karşıtlığı ≥ 7:1 ve QR çözülür. Seçim <see cref="ShareQr.Pick"/>'ten, renk
    /// paletin kendi dosyasından; eksik kalan palet varsa adıyla listelenir.
    /// </summary>
    [Fact]
    public void ButunPalettelerdeQrAcikPlakadaKoyuModulVeYediyeBir()
    {
        var satirlar = AppHost.Run(() =>
        {
            var liste = new List<(string ad, Color plaka, Color modul, double oran, string? okunan, bool acikPalet)>();
            var (qr, window) = Tek(Orta);
            try
            {
                foreach (var ad in PaletteCatalog.Names)
                {
                    PaletteCatalog.Use(ad);
                    window.UpdateLayout();
                    var cizim = Ciz(qr);
                    var m = qr.ModuleSize;
                    var bant = (int)(ShareQr.QuietModules * m);
                    var plaka = cizim.Piksel(1, 1);
                    var modul = cizim.Piksel(bant + (int)(m / 2), bant + (int)(m / 2));
                    liste.Add((ad, plaka, modul, ShareQr.Contrast(plaka, modul), Coz(cizim), PaletteCatalog.IsLight(ad)));
                    if (ShareQr.Luminance(plaka) <= ShareQr.Luminance(modul)) break;
                }
            }
            finally
            {
                window.Close();
                PaletteCatalog.Use(PaletteCatalog.Default);
            }

            return liste;
        });

        var govde = new StringBuilder();
        foreach (var s in satirlar)
            govde.AppendLine($"{s.ad}\t{(s.acikPalet ? "acik" : "koyu")}\tplaka {s.plaka}\tmodul {s.modul}\toran {s.oran:0.00}\tokunan {s.okunan}");
        File.WriteAllText(Path.Combine(Klasor(), "paletler.txt"), govde.ToString());

        var eksik = satirlar
            .Where(s => s.plaka.A != 255 || s.modul.A != 255 || ShareQr.Luminance(s.plaka) <= ShareQr.Luminance(s.modul) || s.oran < 7.0 || s.okunan != Orta)
            .Select(s => $"{s.ad}: plaka {s.plaka}, modul {s.modul}, oran {s.oran:0.00}, okunan {s.okunan}")
            .ToList();

        Assert.Equal(PaletteCatalog.Names.Count, satirlar.Count);
        Assert.True(eksik.Count == 0, "eksik palet: " + string.Join("; ", eksik));
        Assert.Contains(satirlar, s => s.acikPalet);
        Assert.Contains(satirlar, s => !s.acikPalet);

        KanitKapanisi.Kapat(Klasor(), "paletler.txt");
    }

    private sealed class SahteSaglayici : IShareProvider
    {
        private readonly Queue<Func<Task<ShareResult>>> _sira;

        public SahteSaglayici(ShareTarget target, Queue<Func<Task<ShareResult>>> sira)
        {
            Target = target;
            _sira = sira;
        }

        public ShareTarget Target { get; }

        public bool CanDelete => false;

        public Task<ShareResult> UploadAsync(string filePath, int? retentionDays = null, IProgress<UploadProgress>? progress = null, CancellationToken cancellationToken = default)
            => _sira.Dequeue()();

        public Task<ShareResult> CheckHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Basari(Target, Kisa));

        public Task<ShareResult> DeleteAsync(ShareLink link, CancellationToken cancellationToken = default)
            => Task.FromResult(ShareResult.Success(link));
    }

    private static ShareResult Basari(ShareTarget target, string url)
        => ShareResult.Success(new ShareLink(target.Id, "f", url, "kayit.mp4", DateTimeOffset.UtcNow));

    private static string Kayit()
    {
        var dosya = Path.Combine(Klasor(), "kayit.mp4");
        File.WriteAllBytes(dosya, new byte[4096]);
        return dosya;
    }

    /// <summary>
    /// Kaydedici, isteğin asıl yeri: paylaşmadan önce QR yok; yükleme sürerken yok; bağlantı
    /// gelince görünür, erişilebilir adı var ve çizilen QR o bağlantıya çözülür; hata gelince
    /// kaybolur; ikinci bağlantı gelince yeniden görünür ve yeni bağlantıya çözülür.
    /// </summary>
    [Fact]
    public void KaydedicideQrYalnizBaglantiHazirkenGorunurVeYenilenir()
    {
        var ledger = Path.Combine(Klasor(), "paylasimlar.json");
        var olcu = AppHost.Run(() =>
        {
            PaletteCatalog.Use(PaletteCatalog.Default);
            var view = new RecorderView { SkipAutoMeasure = true };
            var window = new Window { Width = 1100, Height = 900, Content = view };
            window.Show();
            try
            {
                view.ShowResult(new RecordResult(true, Kayit(), 1, false, 0, string.Empty, 1));
                window.UpdateLayout();
                Bekle(0.3);

                var ilkYukleme = new TaskCompletionSource<ShareResult>();
                var sira = new Queue<Func<Task<ShareResult>>>();
                SahteSaglayici? saglayici = null;
                view.CreateShareFlow = () => new ShareFlow(t => saglayici ??= new SahteSaglayici(t, sira), new ShareLedger(ledger));
                var qr = view.FindControl<ShareQrCode>("QrRecShare") ?? throw new InvalidOperationException("QrRecShare");
                var dugme = view.FindControl<Button>("BtnRecShare") ?? throw new InvalidOperationException("BtnRecShare");

                void Paylas()
                {
                    dugme.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                }

                string? Oku()
                {
                    qr.BringIntoView();
                    window.UpdateLayout();
                    return Coz(Ciz(qr));
                }

                var once = qr.IsEffectivelyVisible;

                sira.Enqueue(() => ilkYukleme.Task);
                Paylas();
                Bekle(() => saglayici is not null);
                var yuklenirken = qr.IsEffectivelyVisible;
                ilkYukleme.SetResult(Basari(saglayici!.Target, Kisa));
                Bekle(() => view.ShareLinkText == Kisa);
                window.UpdateLayout();
                var ilk = (qr.IsEffectivelyVisible, qr.Link, Oku());
                var ad = AutomationProperties.GetName(qr);

                sira.Enqueue(() => Task.FromResult(ShareResult.Failed(new ShareDiagnosis(ShareFailure.ServiceError, "share.error.gone", new object[] { "x" }))));
                Paylas();
                Bekle(() => view.ShareStatusText.Length > 0 && view.ShareLinkText.Length == 0 && dugme.IsEnabled);
                Bekle(0.1);
                var hatada = (qr.IsEffectivelyVisible, qr.Link);

                sira.Enqueue(() => Task.FromResult(Basari(saglayici.Target, Orta)));
                Paylas();
                Bekle(() => view.ShareLinkText == Orta);
                window.UpdateLayout();
                var ikinci = (qr.IsEffectivelyVisible, qr.Link, Oku());

                return (once, yuklenirken, ilk, ad, hatada, ikinci);
            }
            finally { window.Close(); }
        });

        File.WriteAllText(Path.Combine(Klasor(), "kaydedici.txt"),
            $"once {olcu.once}\nyuklenirken {olcu.yuklenirken}\nilk {olcu.ilk}\nad {olcu.ad}\nhatada {olcu.hatada}\nikinci {olcu.ikinci}\n");

        Assert.False(olcu.once, "paylaşmadan önce QR görünüyor");
        Assert.False(olcu.yuklenirken, "yükleme sürerken QR görünüyor");
        Assert.True(olcu.ilk.IsEffectivelyVisible, "bağlantı gelince QR görünmüyor");
        Assert.Equal(Kisa, olcu.ilk.Link);
        Assert.Equal(Kisa, olcu.ilk.Item3);
        Assert.False(string.IsNullOrWhiteSpace(olcu.ad), "QR'ın erişilebilir adı yok");
        Assert.Contains("QR", olcu.ad);
        Assert.False(olcu.hatada.IsEffectivelyVisible, "hata sonrası QR görünüyor");
        Assert.True(string.IsNullOrEmpty(olcu.hatada.Link), "hata sonrası QR eski bağlantıyı taşıyor");
        Assert.True(olcu.ikinci.IsEffectivelyVisible);
        Assert.Equal(Orta, olcu.ikinci.Link);
        Assert.Equal(Orta, olcu.ikinci.Item3);

        KanitKapanisi.Kapat(Klasor(), "kaydedici.txt", "kayit.mp4", "paylasimlar.json");
    }

    /// <summary>
    /// Ana penceredeki küçültme sekmesi ve kabuk menüsünün küçültme penceresi de aynı parçayı
    /// taşıyor: bağlantı kutusuna yazılan adres QR'a geçer ve çizilen QR ona çözülür; yeni iş
    /// başlayınca (<c>ResetShare</c>) QR kaybolur.
    /// </summary>
    [Fact]
    public void KucultYuzeyleriAyniQrParcasiniTasir()
    {
        var olcu = AppHost.Run(() =>
        {
            PaletteCatalog.Use(PaletteCatalog.Default);
            (bool gorunur, string? okunan, string? ad, bool sonra) Olc(Window window, Grid satir, TextBox kutu, ShareQrCode qr, Action sifirla)
            {
                window.Show();
                kutu.Text = Orta;
                satir.IsVisible = true;
                qr.BringIntoView();
                window.UpdateLayout();
                Bekle(0.2);
                window.UpdateLayout();
                var gorunur = qr.IsEffectivelyVisible;
                var okunan = Coz(Ciz(qr));
                var ad = AutomationProperties.GetName(qr);
                sifirla();
                window.UpdateLayout();
                return (gorunur, okunan, ad, qr.IsEffectivelyVisible);
            }

            var ana = new MainWindow();
            try
            {
                ana.Tabs.SelectedIndex = ana.ShrinkTabIndex;
                var a = Olc(ana, ana.ShareLinkRow, ana.TxtShareLink, ana.QrShare, () => ana.ResetShareForTest(true));

                var is_ = new ShrinkJobWindow(new ShellShrinkStartup(null, ShrinkArgumentProblem.NoTarget, null), null);
                try
                {
                    var b = Olc(is_, is_.ShareLinkRow, is_.TxtShareLink, is_.QrShare, () => is_.ResetShareForTest(true));
                    return (a, b);
                }
                finally { is_.Close(); }
            }
            finally { ana.Close(); }
        });

        foreach (var (yuzey, s) in new[] { ("ana", olcu.a), ("is", olcu.b) })
        {
            Assert.True(s.gorunur, $"{yuzey}: bağlantı varken QR görünmüyor");
            Assert.Equal(Orta, s.okunan);
            Assert.False(string.IsNullOrWhiteSpace(s.ad), $"{yuzey}: QR'ın erişilebilir adı yok");
            Assert.False(s.sonra, $"{yuzey}: yeni işte QR görünür kaldı");
        }
    }
}
