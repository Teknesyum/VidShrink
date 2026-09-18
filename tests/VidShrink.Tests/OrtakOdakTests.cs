using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// K19 D0: ortak odak nesnesi <see cref="CurrentMedia"/>. Sözleşmenin iki kabul ölçütü:
/// oynatıcıdan Küçült'e geçişte ffprobe bir kez çağrılır (sayaçlı sahte yoklayıcı) ve
/// "kaydı izle" ayarı kapalıyken Küçült sekmesinin dosyası kayıt bitince değişmez,
/// açıkken değişir. Her ölçünün negatif kontrolü aynı sınıfta.
/// </summary>
public sealed class OrtakOdakTests
{
    private static readonly string Kanit = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".calisma", "k19-d0"));

    private static void Dongu(Func<bool> bitti, double saniye)
    {
        var saat = Stopwatch.StartNew();
        while (!bitti() && saat.Elapsed.TotalSeconds < saniye)
        {
            using var dilim = new CancellationTokenSource(TimeSpan.FromMilliseconds(5));
            Dispatcher.UIThread.MainLoop(dilim.Token);
        }
    }

    private static string Dosya(string ad, int bayt)
    {
        Directory.CreateDirectory(Kanit);
        var yol = Path.Combine(Kanit, ad);
        File.WriteAllBytes(yol, new byte[bayt]);
        return yol;
    }

    /// <summary>
    /// Son asertten sonra çağrılır: yeşil koşum kendi bıraktığını siler, kırmızı koşum
    /// kanıtını korur çünkü düşen asert buraya hiç gelmez. Klasör boşalınca o da gider.
    /// </summary>
    private static void Kapat(params string[] adlar) => KanitKapanisi.Kapat(Kanit, adlar);

    private static MediaInfo Ornek(string yol) => new()
    {
        FilePath = yol,
        FileSizeBytes = new FileInfo(yol).Length,
        DurationSeconds = 12.5,
        Width = 1280,
        Height = 720,
        Fps = 30,
        VideoCodec = "h264",
        TotalBitrateBps = 2_000_000
    };

    /// <summary>
    /// Sahte yoklayıcı: her çağrıyı sayar, ffprobe'a hiç inmez. Ölçülen sayı bu sayaç.
    /// </summary>
    private sealed class Sayac
    {
        private int _kez;
        private readonly List<string> _yollar = new();

        public int Kez => Volatile.Read(ref _kez);

        public string[] Yollar { get { lock (_yollar) return _yollar.ToArray(); } }

        public Task<MediaInfo> Yokla(string yol, CancellationToken ct)
        {
            Interlocked.Increment(ref _kez);
            lock (_yollar) _yollar.Add(yol);
            return Task.FromResult(Ornek(yol));
        }
    }

    private static (int Kez, string[] Yoklanan, string? Kucultme, double Sure, double Fps) Gecis(string[] yollar)
    {
        return AppHost.Run(() =>
        {
            var window = new MainWindow();
            var sayac = new Sayac();
            try
            {
                window.Prober = sayac.Yokla;
                window.ChkFollowRecording.IsChecked = true;

                foreach (var yol in yollar)
                {
                    window.PlayerOpenedForTest(yol);
                    var gecis = window.OpenInShrinkAsync(yol);
                    Dongu(() => gecis.IsCompleted, 30);
                }

                return (sayac.Kez, sayac.Yollar, window.ShrinkLoadedPath,
                    window.Media.DurationSeconds, window.Media.SourceFps);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void OynaticidanKucultmeyeGecisteYoklamaBirKez()
    {
        var dosya = Dosya("gecis.mkv", 4096);
        var olcu = Gecis(new[] { dosya });

        Directory.CreateDirectory(Kanit);
        File.WriteAllText(Path.Combine(Kanit, "gecis.txt"),
            $"dosya={dosya}\nyoklama={olcu.Kez}\nkucultme={olcu.Kucultme}\n"
            + $"sure={olcu.Sure}\nfps={olcu.Fps}\n");

        Assert.Equal(1, olcu.Kez);
        Assert.Equal(new[] { dosya }, olcu.Yoklanan);
        Assert.Equal(dosya, olcu.Kucultme);
        Assert.Equal(12.5, olcu.Sure);
        Assert.Equal(30, olcu.Fps);

        Kapat("gecis.mkv", "gecis.txt");
    }

    /// <summary>
    /// Negatif kontrol: önbellek her yoklamayı yutmuyor. İki ayrı dosya iki geçiş, iki yoklama.
    /// </summary>
    [Fact]
    public void IkiAyriDosyaIkiYoklama()
    {
        var bir = Dosya("iki-bir.mkv", 4096);
        var iki = Dosya("iki-iki.mkv", 8192);
        var olcu = Gecis(new[] { bir, iki });

        Assert.Equal(2, olcu.Kez);
        Assert.Equal(new[] { bir, iki }, olcu.Yoklanan);
        Assert.Equal(iki, olcu.Kucultme);

        Kapat("iki-bir.mkv", "iki-iki.mkv");
    }

    /// <summary>
    /// Negatif kontrol: aynı yol, diskte değişmiş dosya. Damga düşer, yeniden yoklanır.
    /// </summary>
    [Fact]
    public void DosyaDiskteDegisirseYenidenYoklanir()
    {
        var yol = Dosya("damga.mkv", 4096);
        var olcu = AppHost.Run(() =>
        {
            var window = new MainWindow();
            var sayac = new Sayac();
            try
            {
                window.Prober = sayac.Yokla;
                var bir = window.OpenInShrinkAsync(yol);
                Dongu(() => bir.IsCompleted, 30);
                var once = sayac.Kez;

                var tazeVar = window.Media.InfoFor(yol) is not null;
                File.WriteAllBytes(yol, new byte[12288]);
                File.SetLastWriteTimeUtc(yol, DateTime.UtcNow.AddSeconds(5));
                var tazeYok = window.Media.InfoFor(yol) is null;

                var iki = window.OpenInShrinkAsync(yol);
                Dongu(() => iki.IsCompleted, 30);
                return (once, sonra: sayac.Kez, tazeVar, tazeYok);
            }
            finally { window.Close(); }
        });

        Assert.True(olcu.tazeVar, "Yoklama sonrasi onbellek bos.");
        Assert.True(olcu.tazeYok, "Dosya degistigi halde onbellek taze sayildi.");
        Assert.Equal(1, olcu.once);
        Assert.Equal(2, olcu.sonra);

        Kapat("damga.mkv");
    }

    /// <summary>
    /// İkinci kabul ölçütü: ayar kapalıyken Küçült'ün dosyası kayıt bitince değişmez,
    /// açıkken değişir. İki kol aynı pencerede, aynı kayıt dosyasıyla.
    /// </summary>
    [Fact]
    public void AyarKucultmeninDosyasiniBelirler()
    {
        var onceki = Dosya("ayar-onceki.mkv", 4096);
        var kayit = Dosya("ayar-kayit.mkv", 8192);

        var olcu = AppHost.Run(() =>
        {
            var window = new MainWindow();
            var sayac = new Sayac();
            try
            {
                window.Prober = sayac.Yokla;
                window.FollowPlayerOpener = _ => Task.CompletedTask;
                window.LoadWithoutProbing(onceki, Ornek(onceki));
                var taban = window.ShrinkLoadedPath;

                window.ChkFollowRecording.IsChecked = false;
                var kapali = window.FollowRecordingAsync(kayit);
                Dongu(() => kapali.IsCompleted, 30);
                var kapaliYol = window.ShrinkLoadedPath;
                var kapaliYoklama = sayac.Kez;

                window.ChkFollowRecording.IsChecked = true;
                var acik = window.FollowRecordingAsync(kayit);
                Dongu(() => acik.IsCompleted, 30);
                var acikYol = window.ShrinkLoadedPath;

                return (taban, kapaliYol, kapaliYoklama, acikYol, acikYoklama: sayac.Kez);
            }
            finally { window.Close(); }
        });

        Directory.CreateDirectory(Kanit);
        File.WriteAllText(Path.Combine(Kanit, "ayar.txt"),
            $"taban={olcu.taban}\nkapali={olcu.kapaliYol} yoklama={olcu.kapaliYoklama}\n"
            + $"acik={olcu.acikYol} yoklama={olcu.acikYoklama}\n");

        Assert.Equal(onceki, olcu.taban);
        Assert.Equal(onceki, olcu.kapaliYol);
        Assert.Equal(0, olcu.kapaliYoklama);
        Assert.Equal(kayit, olcu.acikYol);
        Assert.Equal(1, olcu.acikYoklama);

        Kapat("ayar-onceki.mkv", "ayar-kayit.mkv", "ayar.txt");
    }

    /// <summary>
    /// Gerçek açılış sırası: oynatıcı dosyayı açar, takip kolu yüklemeyi başlatır ve
    /// Küçült'e geçiş aynı yolu ffprobe henüz dönmeden ister. Uçuştaki yoklama paylaşılmazsa
    /// sayaç ikiye çıkar; ölçülen tek yoklama budur.
    /// </summary>
    private sealed class GecikmeliSayac
    {
        private int _kez;

        public TaskCompletionSource<bool> Kapi { get; } = new();

        public int Kez => Volatile.Read(ref _kez);

        public async Task<MediaInfo> Yokla(string yol, CancellationToken ct)
        {
            Interlocked.Increment(ref _kez);
            await Kapi.Task.ConfigureAwait(true);
            ct.ThrowIfCancellationRequested();
            return Ornek(yol);
        }
    }

    [Fact]
    public void UstusteBinenIkiYuklemeTekYoklama()
    {
        var dosya = Dosya("ustuste.mkv", 4096);
        var olcu = AppHost.Run(() =>
        {
            var window = new MainWindow();
            var sayac = new GecikmeliSayac();
            try
            {
                window.Prober = sayac.Yokla;
                window.ChkFollowRecording.IsChecked = true;

                window.PlayerOpenedForTest(dosya);
                var gecis = window.OpenInShrinkAsync(dosya);
                var ucustaki = sayac.Kez;

                sayac.Kapi.SetResult(true);
                Dongu(() => gecis.IsCompleted, 30);
                return (ucustaki, sonra: sayac.Kez, yol: window.ShrinkLoadedPath);
            }
            finally { window.Close(); }
        });

        Assert.Equal(1, olcu.ucustaki);
        Assert.Equal(1, olcu.sonra);
        Assert.Equal(dosya, olcu.yol);

        Kapat("ustuste.mkv");
    }

    /// <summary>
    /// Başka dosyaya odaklanınca çözümleme düşüyor, sahip yeni sahibe geçiyor.
    /// </summary>
    [Fact]
    public void BaskaDosyayaOdaklanincaCozumlemeDusuyor()
    {
        var odak = Dosya("konum-odak.mkv", 4096);
        var baska = Dosya("konum-baska.mkv", 4096);
        var media = new CurrentMedia();

        media.Publish(odak, Ornek(odak), MediaFocusOwner.Player);
        Assert.NotNull(media.Info);

        media.Focus(baska, MediaFocusOwner.Shrink);

        Assert.Null(media.Info);
        Assert.Equal(MediaFocusOwner.Shrink, media.Owner);
        Assert.True(media.Holds(baska));

        Kapat("konum-odak.mkv", "konum-baska.mkv");
    }

    /// <summary>
    /// <b>Tek konum deposu.</b> Konum yalnız <c>PlaybackHistory</c>'de tutulur;
    /// <c>CurrentMedia</c> ikinci bir kopya taşımaz. Eskiden <c>LastPositionSeconds</c>
    /// alanı vardı: sekme değişiminde yazılıyor ama üretimde hiçbir yerde okunmuyordu —
    /// iki depo, biri ölü. Yüzey yansımayla pimli, çünkü alanın geri gelmesi derlemeyi
    /// kırmaz, sessizce ikinci depoyu geri getirir. Desenin kör olmadığı, gerçekten
    /// duran üyelerle olumlu kontrollü.
    /// </summary>
    [Fact]
    public void KonumDeposuCurrentMediaDaYok()
    {
        var uyeler = typeof(CurrentMedia)
            .GetMembers(BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance | BindingFlags.Static)
            .Select(u => u.Name)
            .ToArray();

        Assert.DoesNotContain("LastPositionSeconds", uyeler);
        Assert.DoesNotContain("Remember", uyeler);

        Assert.Contains("Publish", uyeler);
        Assert.Contains("Focus", uyeler);
        Assert.Contains("InfoFor", uyeler);
    }

    /// <summary>
    /// Aynı yolda sahip değişince <c>Changed</c> yayılıyor. <c>Focus</c>'un erken dönüş kolu
    /// sahibi sessizce yazıyordu: kaydediciden küçültmeye geçen aynı dosyada abone hiçbir şey
    /// görmüyor, <c>Owner</c>'a bakan kol bayat kalıyordu. Sahip aynıysa olay yayılmaz
    /// (olumsuz kontrol) — yoksa her <c>Focus</c> çağrısı gereksiz bir tur açardı.
    /// </summary>
    [Fact]
    public void AyniYoldaSahipDegisinceOlayYayiliyor()
    {
        var dosya = Dosya("sahip-degisimi.mkv", 4096);
        var media = new CurrentMedia();

        var sahipler = new List<MediaFocusOwner>();
        media.Changed += m => sahipler.Add(m.Owner);

        media.Publish(dosya, Ornek(dosya), MediaFocusOwner.Recorder);
        var yayindan = sahipler.Count;

        media.Focus(dosya, MediaFocusOwner.Shrink);
        var degisince = sahipler.Count;

        media.Focus(dosya, MediaFocusOwner.Shrink);
        var ayniyken = sahipler.Count;

        Assert.Equal(1, yayindan);
        Assert.Equal(2, degisince);
        Assert.Equal(2, ayniyken);
        Assert.Equal(
            new[] { MediaFocusOwner.Recorder, MediaFocusOwner.Shrink },
            sahipler);
        Assert.Equal(MediaFocusOwner.Shrink, media.Owner);

        Kapat("sahip-degisimi.mkv");
    }
}
