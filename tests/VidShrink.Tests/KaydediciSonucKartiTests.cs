using System.Diagnostics;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using VidShrink.App.Recorder;
using VidShrink.Core.Editing;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// Kayıt bitince köşede açılan sonuç kartı: varsayılan açık, tam teslimde açılır, küçük resmi
/// sahte kaynaktan gelir (ffmpeg koşmaz), kendiliğinden kapanır, sürükleme verisi teslim edilen
/// dosyanın yolunu taşır, "bir daha gösterme" ayar dosyasına gider ve sonraki teslimde kart açılmaz.
/// Yarım kayıtta kart açılmaz. Gerçek <c>%APPDATA%</c>'ya dokunulmaz.
/// </summary>
public sealed class KaydediciSonucKartiTests
{
    private const int Tavan = 5000;

    private const string BirPiksel = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    private sealed class SahteKucukResim : IThumbnailSource
    {
        public int Cagri { get; private set; }

        public Task<byte[]?> ReadAsync(string source, long sourceTick, int height, CancellationToken ct)
        {
            Cagri++;
            return Task.FromResult<byte[]?>(Convert.FromBase64String(BirPiksel));
        }
    }

    private static string Dosya(string ad)
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "sonuc-karti");
        Directory.CreateDirectory(klasor);
        var yol = Path.Combine(klasor, ad);
        File.WriteAllBytes(yol, new byte[4096]);
        return yol;
    }

    private static RecordResult Tam(string yol) => new(true, yol, 1, false, 0, string.Empty, 1);

    private static void Bekle(int ms)
    {
        var saat = Stopwatch.StartNew();
        while (saat.ElapsedMilliseconds < ms)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
    }

    private static void Tamamla(Task is_)
    {
        var saat = Stopwatch.StartNew();
        while (!is_.IsCompleted && saat.ElapsedMilliseconds < Tavan) Bekle(5);
        Assert.True(is_.IsCompleted, "Kart işi tavan içinde bitmedi.");
    }

    [Fact]
    public void KartTeslimdeAcilirKucukResimGosterirKendiligindenKapanir()
    {
        var dosya = Dosya("kart-acilir.mp4");
        var olcu = KaydediciAyarTests.AyarDosyasiyla(ayar => AppHost.Run(() =>
        {
            var view = new RecorderView(ayar);
            var kaynak = new SahteKucukResim();
            view.ResultThumbnails = kaynak;
            view.ResultCardHold = TimeSpan.FromMilliseconds(300);
            var varsayilan = view.Settings.ShowResultCard;
            view.Deliver(Tam(dosya));
            var kart = view.ResultCard;
            var acik = kart?.IsVisible ?? false;
            Tamamla(view.ResultCardRun);
            var resim = kart?.Thumbnail is not null;
            var ad = kart?.FileName;
            var saat = Stopwatch.StartNew();
            long kapandi = -1;
            while (kart is not null && saat.ElapsedMilliseconds < Tavan && kapandi < 0)
            {
                Bekle(10);
                if (!kart.IsVisible) kapandi = saat.ElapsedMilliseconds;
            }

            return (varsayilan, acik, resim, ad, kapandi, kaynak.Cagri);
        }));

        Assert.True(olcu.varsayilan, "Sonuç kartı varsayılan açık gelmeli.");
        Assert.True(olcu.acik, "Tam teslimde kart açılmadı.");
        Assert.True(olcu.resim, "Kartta küçük resim yok.");
        Assert.Equal(1, olcu.Cagri);
        Assert.Equal(Path.GetFileName(dosya), olcu.ad);
        Assert.True(olcu.kapandi >= 0, $"Kart {Tavan} ms içinde kendiliğinden kapanmadı.");
    }

    [Fact]
    public void YarimKayittaKartAcilmaz()
    {
        var dosya = Dosya("kart-yarim.mp4");
        var kart = KaydediciAyarTests.AyarDosyasiyla(ayar => AppHost.Run(() =>
        {
            var view = new RecorderView(ayar) { ResultThumbnails = new SahteKucukResim() };
            view.Deliver(Tam(dosya) with { Partial = true });
            return view.ResultCard;
        }));

        Assert.Null(kart);
    }

    [Fact]
    public void SuruklemeVerisiTeslimEdilenDosyaninYolunuTasir()
    {
        var dosya = Dosya("kart-surukle.mp4");
        var yol = KaydediciAyarTests.AyarDosyasiyla(ayar => AppHost.Run(() =>
        {
            var view = new RecorderView(ayar) { ResultThumbnails = new SahteKucukResim() };
            view.Deliver(Tam(dosya));
            var kart = view.ResultCard!;
            var veri = kart.DragData();
            Tamamla(veri);
            var oge = veri.Result!.Items.Single();
            var dosyaOgesi = oge.TryGetRaw(DataFormat.File) as IStorageItem;
            kart.Close();
            return dosyaOgesi?.TryGetLocalPath();
        }));

        Assert.Equal(Path.GetFullPath(dosya), yol is null ? null : Path.GetFullPath(yol));
    }

    [Fact]
    public void BirDahaGostermeAyaraGiderKartiKapatirSonrakiTeslimdeAcilmaz()
    {
        var dosya = Dosya("kart-bir-daha.mp4");
        var olcu = KaydediciAyarTests.AyarDosyasiyla(ayar => AppHost.Run(() =>
        {
            var view = new RecorderView(ayar) { ResultThumbnails = new SahteKucukResim() };
            view.Deliver(Tam(dosya));
            var kart = view.ResultCard!;
            kart.BtnNeverAgain.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Bekle(20);
            var kapali = !kart.IsVisible;
            var kutu = KaydediciAyarTests.Bul<CheckBox>(view, "ChkResultCard").IsChecked;
            var dosyada = (bool?)JsonNode.Parse(File.ReadAllText(ayar))!["showResultCard"];

            var sonra = new RecorderView(ayar) { ResultThumbnails = new SahteKucukResim() };
            sonra.Deliver(Tam(dosya));
            var ikinci = sonra.ResultCard;
            return (kapali, kutu, dosyada, ikinci);
        }));

        Assert.True(olcu.kapali, "Bir daha gösterme kartı kapatmadı.");
        Assert.False(olcu.kutu);
        Assert.False(olcu.dosyada);
        Assert.Null(olcu.ikinci);
    }

    [Fact]
    public void AyarKutusuKartiGeriAcar()
    {
        var dosya = Dosya("kart-geri.mp4");
        var olcu = KaydediciAyarTests.AyarDosyasiyla(ayar => AppHost.Run(() =>
        {
            var view = new RecorderView(ayar) { ResultThumbnails = new SahteKucukResim() };
            var kutu = KaydediciAyarTests.Bul<CheckBox>(view, "ChkResultCard");
            var ilk = kutu.IsChecked;
            kutu.IsChecked = false;
            Bekle(20);
            var kapatinca = (bool?)JsonNode.Parse(File.ReadAllText(ayar))!["showResultCard"];
            kutu.IsChecked = true;
            Bekle(20);
            var sonra = new RecorderView(ayar) { ResultThumbnails = new SahteKucukResim() };
            sonra.Deliver(Tam(dosya));
            var kart = sonra.ResultCard;
            kart?.Close();
            return (ilk, kapatinca, kart is not null);
        }));

        Assert.True(olcu.ilk);
        Assert.False(olcu.kapatinca);
        Assert.True(olcu.Item3, "Kutu yeniden işaretlenince kart açılmadı.");
    }
}
