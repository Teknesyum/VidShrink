using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;

namespace VidShrink.Tests;

/// <summary>
/// Hızlı hedef yongalarının satır kırılımı dile bağlı değil (<see cref="YongaSeridi"/>). Düz
/// <c>WrapPanel</c> ile 1600x1000'de 42 dilin beşi 6+3, kalanı 5+4; 1920x1040'ta 18'i 6+3, kalanı
/// 7+2 kırılıyordu. Ölçüm başsız pencerede; ön ayar ve ayar dosyası her ölçümün kendi geçici
/// klasöründe, gerçek <c>%APPDATA%</c> okunmaz da yazılmaz da.
/// </summary>
public sealed class YongaSeridiTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        TipSources.Root, ".calisma", "test-ciktilari", "yonga-seridi", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static void Yerlestir(Layoutable kok, Size boyut)
    {
        kok.Measure(boyut);
        kok.Arrange(new Rect(boyut));
    }

    private static (YongaSeridi Serit, List<Border> Kutular) Serit(int isaretli, params double[] enler)
    {
        var serit = new YongaSeridi();
        var kutular = enler.Select(en => new Border { Width = en, Height = 20 }).ToList();
        if (isaretli >= 0) YongaSeridi.SetSatirBasi(kutular[isaretli], true);
        serit.Children.AddRange(kutular);
        return (serit, kutular);
    }

    /// <summary>Öncekiler sığıyorsa bildirilen çocuk yeni satırı başlatır; bildirimsiz eşi tek satırda kalır.</summary>
    [Fact]
    public void BildirilenCocukYeniSatiriBaslatir()
    {
        var (satirlar, ikinciUst, boy, bildirimsiz) = AppHost.Run(() =>
        {
            var (serit, kutular) = Serit(2, 50, 50, 50, 50);
            Yerlestir(serit, new Size(400, 200));
            var (duz, _) = Serit(-1, 50, 50, 50, 50);
            Yerlestir(duz, new Size(400, 200));
            return (serit.Satirlar.ToList(), kutular[2].Bounds.Y, serit.DesiredSize.Height, duz.Satirlar.ToList());
        });

        Assert.Equal([2, 2], satirlar);
        Assert.Equal(20, ikinciUst);
        Assert.Equal(40, boy);
        Assert.Equal([4], bildirimsiz);
    }

    /// <summary>
    /// Öncekiler sığmayıp şerit zaten sarıldıysa bildirim yok sayılır: satır sayısı bildirimsiz
    /// şeritle aynı kalır, dar pencere fazladan satır ödemez.
    /// </summary>
    [Fact]
    public void SarilanSeritteBildirimYokSayilir()
    {
        var (bildirimli, bildirimsiz) = AppHost.Run(() =>
        {
            var (serit, _) = Serit(3, 50, 50, 50, 50, 50);
            Yerlestir(serit, new Size(120, 400));
            var (duz, _) = Serit(-1, 50, 50, 50, 50, 50);
            Yerlestir(duz, new Size(120, 400));
            return (serit.Satirlar.ToList(), duz.Satirlar.ToList());
        });

        Assert.Equal([2, 2, 1], bildirimsiz);
        Assert.Equal(bildirimsiz, bildirimli);
    }

    /// <summary>Bildirimden sonraki çocuklar sığdıkça dizilir, sığmayan alt satıra iner; gizli çocuk sayılmaz.</summary>
    [Fact]
    public void BildirimdenSonrasiSigdikcaDizilir()
    {
        var satirlar = AppHost.Run(() =>
        {
            var (serit, kutular) = Serit(2, 50, 50, 50, 50, 50, 50, 50);
            kutular[3].IsVisible = false;
            Yerlestir(serit, new Size(160, 400));
            return serit.Satirlar.ToList();
        });

        Assert.Equal([2, 3, 1], satirlar);
    }

    private MainWindow Pencere() => new()
    {
        SettingsPathOverride = Path.Combine(_folder, "settings.json"),
        PresetPathOverride = Path.Combine(_folder, "presets.json")
    };

    private static void Yerlestir(MainWindow pencere, Size boyut)
    {
        pencere.Width = double.NaN;
        pencere.Height = double.NaN;
        pencere.Measure(boyut);
        pencere.Arrange(new Rect(boyut));
        pencere.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var kok = (Layoutable)pencere.GetVisualChildren().Single();
        foreach (var dugum in pencere.GetVisualDescendants().OfType<Layoutable>()) dugum.InvalidateMeasure();
        kok.Measure(boyut);
        kok.Arrange(new Rect(boyut));
    }

    /// <summary>
    /// 42 dilin hepsinde, 1600x1000 ve 1920x1040'ta gömülü yongalar 4+5 kırılır: ikinci satırı
    /// 25 MB yongası başlatır, "+" ikinci satırın sonundadır.
    /// </summary>
    [Fact]
    public void KirilimButunDillerdeAyni()
    {
        Assert.Equal(42, Locales.Languages.Count);

        var sapanlar = AppHost.Run(() =>
        {
            var sapanlar = new List<string>();
            foreach (var dil in Locales.Languages)
            {
                Strings.Use(dil);
                var pencere = Pencere();
                try
                {
                    foreach (var boyut in new[] { new Size(1600, 1000), new Size(1920, 1040) })
                    {
                        Yerlestir(pencere, boyut);
                        var serit = pencere.ChipStrip;
                        var satirlar = string.Join("+", serit.Satirlar);
                        var ayniSatir = pencere.ChipAddPreset.Bounds.Y == pencere.Chip25.Bounds.Y
                            && pencere.Chip25.Bounds.X == 0
                            && pencere.ChipWhatsApp.Bounds.Y < pencere.Chip25.Bounds.Y;
                        var sigiyor = serit.Children.Where(c => c.IsVisible).All(c => c.Bounds.Right <= serit.Bounds.Width + 0.5);
                        if (satirlar != "4+5" || !ayniSatir || !sigiyor)
                            sapanlar.Add($"{dil} {boyut.Width}x{boyut.Height}: {satirlar} (şerit {serit.Bounds.Width:0})");
                    }
                }
                finally
                {
                    pencere.Close();
                    Strings.Use("en");
                }
            }
            return sapanlar;
        });

        Assert.True(sapanlar.Count == 0, "Kırılımı 4+5 olmayan kol:" + Environment.NewLine + string.Join(Environment.NewLine, sapanlar));
    }

    /// <summary>
    /// Kullanıcı yongası ilk satırı değiştirmez: "+"nın önüne, ikinci satıra girer; sığmayan alt
    /// satıra iner ve hiçbiri şeridin dışına çıkmaz. 1920x1040'ta ilk yonga ikinci satıra sığar
    /// (4+7), 1600x1000'de "+" üçüncü satıra iner (4+6+1). Hepsi silinince 4+5 döner.
    /// </summary>
    [Fact]
    public void KullaniciYongasiIlkSatiraDokunmaz()
    {
        var (genis, orta, cok, tasan, sonra) = AppHost.Run(() =>
        {
            var pencere = Pencere();
            try
            {
                var boyut = new Size(1920, 1040);
                Yerlestir(pencere, boyut);
                pencere.InitUserPresets();
                pencere.SavePreset("Bir");
                Yerlestir(pencere, boyut);
                var genis = pencere.ChipStrip.Satirlar.ToList();
                Yerlestir(pencere, new Size(1600, 1000));
                var orta = pencere.ChipStrip.Satirlar.ToList();
                foreach (var ad in new[] { "İki", "Üç", "Dört", "Beş", "Altı", "Yedi", "Sekiz" }) pencere.SavePreset(ad);
                Yerlestir(pencere, boyut);
                var serit = pencere.ChipStrip;
                var cok = serit.Satirlar.ToList();
                var tasan = serit.Children.Where(c => c.IsVisible).Count(c => c.Bounds.Right > serit.Bounds.Width + 0.5);
                foreach (var onAyar in pencere.UserPresets.ToList()) pencere.DeletePreset(onAyar);
                Yerlestir(pencere, boyut);
                return (genis, orta, cok, tasan, pencere.ChipStrip.Satirlar.ToList());
            }
            finally { pencere.Close(); }
        });

        Assert.Equal([4, 7], genis);
        Assert.Equal([4, 6, 1], orta);
        Assert.Equal(4, cok[0]);
        Assert.True(cok.Count >= 3, $"sekiz kullanıcı yongası üçüncü satıra inmedi: {string.Join("+", cok)}");
        Assert.Equal(8 + 1 + 8 + 1, cok.Sum());
        Assert.Equal(0, tasan);
        Assert.Equal([4, 5], sonra);
    }
}
