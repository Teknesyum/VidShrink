using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Recorder;
using VidShrink.Core.Share;
using VidShrink.Ffmpeg;
using Xunit;
using ShareTarget = VidShrink.Core.Share.ShareTarget;

namespace VidShrink.Tests;

/// <summary>
/// Kaydedicinin "Kayıt bitince" seçimi: her seçim kendi eylemini çağırır (sahte pano, sahte
/// küçültme kapısı, sahte paylaşım sağlayıcısı), yarım ya da başarısız kayıtta hiçbir şey
/// çağrılmaz, hata <c>StatusError</c> satırında görünür, seçim ayar dosyasına gider.
/// Gerçek panoya, ağa ve gerçek <c>%APPDATA%</c>'ya dokunulmaz. Kanıt <c>.calisma/kayit-bitince/</c>.
/// </summary>
public sealed class KayitTeslimBitinceTests
{
    private static readonly string[] Anahtarlar =
    {
        "title", "hint", "nothing", "copy", "shrink-copy", "upload-copy", "copied", "shrinking",
        "shrunk-copied", "link-copied", "shrink-failed", "upload-failed", "failed", "no-clipboard"
    };

    private static string Klasor()
    {
        var yol = Path.Combine(GirdiKanit.Root, ".calisma", "kayit-bitince");
        Directory.CreateDirectory(yol);
        return yol;
    }

    private static void Kapat(params string[] adlar)
    {
        AppHost.Run(() =>
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            return 0;
        });
        KanitKapanisi.Kapat(Klasor(), adlar);
    }

    private static string Dosya(string ad)
    {
        var yol = Path.Combine(Klasor(), ad);
        File.WriteAllBytes(yol, new byte[4096]);
        return yol;
    }

    private static string Metin(string anahtar) => LanguageCatalog.Display(Strings.Get(anahtar));

    private static RecordResult Tam(string yol) => new(true, yol, 1, false, 0, string.Empty, 1);

    private sealed class SahtePano : IRecordingClipboard
    {
        public List<string> Dosyalar { get; } = new();

        public List<string> Metinler { get; } = new();

        public Exception? Atacak { get; set; }

        public Task SetFileAsync(string path)
        {
            if (Atacak is { } hata) throw hata;
            Dosyalar.Add(path);
            return Task.CompletedTask;
        }

        public Task SetTextAsync(string text)
        {
            if (Atacak is { } hata) throw hata;
            Metinler.Add(text);
            return Task.CompletedTask;
        }
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
            return Task.FromResult(ShareResult.Success(new ShareLink(Target.Id, "b1", "https://ornek.test/b1", Path.GetFileName(filePath), DateTimeOffset.UtcNow)));
        }

        public Task<ShareResult> CheckHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ShareResult.Success(new ShareLink(Target.Id, "h", "https://ornek.test/h", "h", DateTimeOffset.UtcNow)));

        public Task<ShareResult> DeleteAsync(ShareLink link, CancellationToken cancellationToken = default)
            => Task.FromResult(ShareResult.Success(link));
    }

    private sealed class Duzenek
    {
        public required RecorderView View { get; init; }

        public required Window Window { get; init; }

        public SahtePano Pano { get; } = new();

        public List<string> Kucultulen { get; } = new();

        public string? KucultmeCiktisi { get; set; }

        public SahteSaglayici? Saglayici { get; set; }

        public int Cagri => Pano.Dosyalar.Count + Pano.Metinler.Count + Kucultulen.Count + (Saglayici?.Yuklenen.Count ?? 0);

        public void Bekle()
        {
            var saat = System.Diagnostics.Stopwatch.StartNew();
            while (!View.FinishRun.IsCompleted && saat.Elapsed.TotalSeconds < 10)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Thread.Sleep(5);
            }
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
    }

    private static Duzenek Kur(string ayar)
    {
        var view = new RecorderView(ayar) { SkipAutoMeasure = true };
        var window = new Window { Width = 1100, Height = 900, Content = view };
        window.Show();
        var duzenek = new Duzenek { View = view, Window = window };
        view.RevealFolder = _ => { };
        view.FinishClipboard = duzenek.Pano;
        view.ShrinkForFinish = yol =>
        {
            duzenek.Kucultulen.Add(yol);
            return Task.FromResult(duzenek.KucultmeCiktisi);
        };
        var ledger = Path.Combine(Klasor(), "paylasimlar.json");
        view.CreateShareFlow = () => new ShareFlow(t => duzenek.Saglayici ??= new SahteSaglayici(t), new ShareLedger(ledger));
        return duzenek;
    }

    /// <summary>
    /// Her seçim kendi eylemini çağırır, ötekileri çağırmaz. Seçim açılır kutudan yapılır, eylem
    /// teslim edilen son dosyada koşar; küçültmede panoya küçültülen çıktı, yüklemede bağlantı gider.
    /// "Hiçbir şey" hiçbir sahteyi çağırmaz.
    /// </summary>
    [Theory]
    [InlineData("Nothing")]
    [InlineData("CopyFile")]
    [InlineData("ShrinkAndCopy")]
    [InlineData("UploadAndCopyLink")]
    public void HerSecimKendiEyleminiCagirir(string ad)
    {
        var secim = Enum.Parse<RecorderFinishAction>(ad);
        var ayar = Path.Combine(Klasor(), $"ayar-{ad}.json");
        var kayit = Dosya($"kayit-{ad}.mp4");
        var cikti = Dosya($"kayit-{ad}-kucuk.mp4");

        var (dosyalar, metinler, kucultulen, yuklenen, satir, hata, beklenen) = AppHost.Run(() =>
        {
            var d = Kur(ayar);
            try
            {
                d.KucultmeCiktisi = cikti;
                d.View.SelectedFinishAction = secim;
                d.View.Deliver(Tam(kayit));
                d.Bekle();
                var beklenenSatir = secim switch
                {
                    RecorderFinishAction.CopyFile => Metin("recorder.finish.copied"),
                    RecorderFinishAction.ShrinkAndCopy => Metin("recorder.finish.shrunk-copied"),
                    RecorderFinishAction.UploadAndCopyLink => Metin("recorder.finish.link-copied"),
                    _ => string.Empty
                };
                return (d.Pano.Dosyalar.ToList(), d.Pano.Metinler.ToList(), d.Kucultulen.ToList(),
                    d.Saglayici?.Yuklenen.ToList() ?? new List<string>(), d.View.FinishStatusText, d.View.FinishStatusIsError, beklenenSatir);
            }
            finally { d.Window.Close(); }
        });

        File.WriteAllText(Path.Combine(Klasor(), $"secim-{ad}.txt"),
            $"dosyalar: {string.Join(", ", dosyalar)}\nmetinler: {string.Join(", ", metinler)}\nkucultulen: {string.Join(", ", kucultulen)}\nyuklenen: {string.Join(", ", yuklenen)}\nsatir: {satir}\n");

        switch (secim)
        {
            case RecorderFinishAction.Nothing:
                Assert.Empty(dosyalar);
                Assert.Empty(metinler);
                Assert.Empty(kucultulen);
                Assert.Empty(yuklenen);
                break;
            case RecorderFinishAction.CopyFile:
                Assert.Equal(new[] { kayit }, dosyalar);
                Assert.Empty(metinler);
                Assert.Empty(kucultulen);
                Assert.Empty(yuklenen);
                break;
            case RecorderFinishAction.ShrinkAndCopy:
                Assert.Equal(new[] { kayit }, kucultulen);
                Assert.Equal(new[] { cikti }, dosyalar);
                Assert.Empty(metinler);
                Assert.Empty(yuklenen);
                break;
            case RecorderFinishAction.UploadAndCopyLink:
                Assert.Equal(new[] { kayit }, yuklenen);
                Assert.Equal(new[] { "https://ornek.test/b1" }, metinler);
                Assert.Empty(dosyalar);
                Assert.Empty(kucultulen);
                break;
        }

        Assert.Equal(beklenen, satir);
        Assert.False(hata);

        Kapat($"ayar-{ad}.json", $"kayit-{ad}.mp4", $"kayit-{ad}-kucuk.mp4", $"secim-{ad}.txt", "paylasimlar.json");
    }

    /// <summary>
    /// Yarım, başarısız, GIF'e çevrilemeyen ve teslim kabına taşınamayan kayıtta üç eylemin hiçbiri
    /// hiçbir sahteyi çağırmaz ve satır yazılmaz. Olumlu kontrol: aynı düzenekte tam kayıt kopyalanır,
    /// yani sahteler bağlı ve sessizlik kapının eseri.
    /// </summary>
    [Fact]
    public void YarimVeBasarisizKayittaHicbirSeyCagrilmaz()
    {
        var ayar = Path.Combine(Klasor(), "ayar-yarim.json");
        var kayit = Dosya("kayit-yarim.mp4");
        var cikti = Dosya("kayit-yarim-kucuk.mp4");

        var (sessiz, satirlar, olumlu) = AppHost.Run(() =>
        {
            var d = Kur(ayar);
            try
            {
                d.KucultmeCiktisi = cikti;
                var sonuclar = new[]
                {
                    new RecordResult(true, kayit, 1, true, 255, string.Empty, 1, Playable: true),
                    new RecordResult(false, kayit, 1, false, 1, string.Empty, 1),
                    new RecordResult(true, kayit, 1, false, 0, string.Empty, 1, MissingGif: kayit + ".gif"),
                    new RecordResult(true, kayit, 1, false, 0, string.Empty, 1, DeliveryError: "kilitli")
                };
                var satirlar = new List<string>();
                foreach (var secim in new[] { RecorderFinishAction.CopyFile, RecorderFinishAction.ShrinkAndCopy, RecorderFinishAction.UploadAndCopyLink })
                {
                    d.View.SelectedFinishAction = secim;
                    foreach (var sonuc in sonuclar)
                    {
                        d.View.Deliver(sonuc);
                        d.Bekle();
                        satirlar.Add(d.View.FinishStatusText);
                    }
                }

                var sessizCagri = d.Cagri;
                d.View.SelectedFinishAction = RecorderFinishAction.CopyFile;
                d.View.Deliver(Tam(kayit));
                d.Bekle();
                return (sessizCagri, satirlar, d.Pano.Dosyalar.ToList());
            }
            finally { d.Window.Close(); }
        });

        Assert.Equal(0, sessiz);
        Assert.All(satirlar, satir => Assert.Equal(string.Empty, satir));
        Assert.Equal(new[] { kayit }, olumlu);

        Kapat("ayar-yarim.json", "kayit-yarim.mp4", "kayit-yarim-kucuk.mp4", "paylasimlar.json");
    }

    /// <summary>
    /// Hata yutulmaz: panonun attığı istisna, çıktısız küçültme ve kapısız küçültme <c>StatusError</c>
    /// temalı satırda görünür. Olumsuz kontrol: başarılı kopya aynı satırı <c>StatusSuccess</c> ile yazar.
    /// </summary>
    [Fact]
    public void HataStatusErrorSatirindaGorunur()
    {
        var ayar = Path.Combine(Klasor(), "ayar-hata.json");
        var kayit = Dosya("kayit-hata.mp4");

        var olcu = AppHost.Run(() =>
        {
            var d = Kur(ayar);
            try
            {
                var satir = d.View.FindControl<TextBlock>("TxtFinishStatus")!;
                Application.Current!.TryFindResource("StatusError", out var hataTemasi);
                Application.Current!.TryFindResource("StatusSuccess", out var basariTemasi);
                var sonuc = new List<(string Kol, string Metin, bool Hata, bool HataTemasi, bool BasariTemasi)>();
                void Oku(string kol) => sonuc.Add((kol, d.View.FinishStatusText, d.View.FinishStatusIsError,
                    ReferenceEquals(satir.Theme, hataTemasi), ReferenceEquals(satir.Theme, basariTemasi)));

                d.Pano.Atacak = new InvalidOperationException("pano-kilitli");
                d.View.SelectedFinishAction = RecorderFinishAction.CopyFile;
                d.View.Deliver(Tam(kayit));
                d.Bekle();
                Oku("pano");

                d.Pano.Atacak = null;
                d.KucultmeCiktisi = null;
                d.View.SelectedFinishAction = RecorderFinishAction.ShrinkAndCopy;
                d.View.Deliver(Tam(kayit));
                d.Bekle();
                Oku("ciktisiz");

                d.View.ShrinkForFinish = null;
                d.View.Deliver(Tam(kayit));
                d.Bekle();
                Oku("kapisiz");

                d.View.SelectedFinishAction = RecorderFinishAction.CopyFile;
                d.View.Deliver(Tam(kayit));
                d.Bekle();
                Oku("basari");

                return (sonuc, Metin("recorder.finish.shrink-failed"));
            }
            finally { d.Window.Close(); }
        });

        var (kollar, kucultmeHatasi) = olcu;
        File.WriteAllLines(Path.Combine(Klasor(), "hata.txt"), kollar.Select(k => $"{k.Kol}: {k.Metin} hata={k.Hata} hataTemasi={k.HataTemasi}"));

        Assert.Contains("pano-kilitli", kollar[0].Metin, StringComparison.Ordinal);
        Assert.Equal(kucultmeHatasi, kollar[1].Metin);
        Assert.Equal(kucultmeHatasi, kollar[2].Metin);
        foreach (var kol in kollar.Take(3))
        {
            Assert.True(kol.Hata, kol.Kol);
            Assert.True(kol.HataTemasi, kol.Kol);
        }

        Assert.False(kollar[3].Hata);
        Assert.False(kollar[3].HataTemasi);
        Assert.True(kollar[3].BasariTemasi);

        Kapat("ayar-hata.json", "kayit-hata.mp4", "hata.txt", "paylasimlar.json");
    }

    /// <summary>
    /// Seçim açılır kutudan <c>recorder-settings.json</c>'a <c>finishAction</c> olarak yazılır ve yeni
    /// görünüm aynı seçimle açılır. Varsayılan "Hiçbir şey": boş ayar dosyası panoya dokunmaz.
    /// </summary>
    [Fact]
    public void SecimAyaraYazilirVeVarsayilanHicbirSey()
    {
        var ayar = Path.Combine(Klasor(), "ayar-kalici.json");
        var bos = Path.Combine(Klasor(), "ayar-bos.json");
        if (File.Exists(ayar)) File.Delete(ayar);
        if (File.Exists(bos)) File.Delete(bos);

        var (varsayilan, geriGelen) = AppHost.Run(() =>
        {
            var ilk = Kur(bos);
            RecorderFinishAction v;
            try { v = ilk.View.SelectedFinishAction; }
            finally { ilk.Window.Close(); }

            var yazan = Kur(ayar);
            try { yazan.View.SelectedFinishAction = RecorderFinishAction.UploadAndCopyLink; }
            finally { yazan.Window.Close(); }

            var okuyan = Kur(ayar);
            try { return (v, okuyan.View.SelectedFinishAction); }
            finally { okuyan.Window.Close(); }
        });

        var json = File.ReadAllText(ayar);

        Assert.Equal(RecorderFinishAction.Nothing, varsayilan);
        Assert.Equal(RecorderFinishAction.Nothing, new RecorderSettings().FinishAction);
        Assert.Contains("\"finishAction\": \"UploadAndCopyLink\"", json, StringComparison.Ordinal);
        Assert.Equal(RecorderFinishAction.UploadAndCopyLink, geriGelen);
        Assert.Equal(RecorderFinishAction.UploadAndCopyLink, RecorderSettings.Load(ayar).FinishAction);

        Kapat("ayar-kalici.json", "ayar-bos.json", "paylasimlar.json");
    }

    /// <summary>On dört anahtar kırk iki dilde dolu; hata metni yer tutucusunu korur.</summary>
    [Fact]
    public void AnahtarlarButunDillerde()
    {
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var dil in Locales.Languages)
        {
            var degerler = Locales.Domain(dil, "recorder");
            foreach (var ad in Anahtarlar)
            {
                var anahtar = "recorder.finish." + ad;
                Assert.True(degerler.TryGetValue(anahtar, out var deger) && !string.IsNullOrWhiteSpace(deger), $"{dil}: {anahtar}");
            }
            Assert.Contains("{0}", degerler["recorder.finish.failed"], StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Küçültme kolu ana pencerenin kuyruğundan geçer (Küçült sekmesinin seçili hedefi), yükleme
    /// kaydedicinin kendi paylaşım oturumundan. İkinci bir kodlayıcı ya da yükleyici yazılmaz.
    /// </summary>
    [Fact]
    public void KucultmeKuyruktanYuklemePaylasimOturumundanGecer()
    {
        var tembel = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "MainWindow.TembelSekme.cs"));
        var bitince = File.ReadAllText(Path.Combine(TipSources.Root, "src", "VidShrink.App", "Recorder", "RecorderView.Bitince.cs"));

        Assert.Contains("_recorderPane.ShrinkForFinish = ShrinkForFinishAsync;", tembel, StringComparison.Ordinal);
        var govde = Regex.Match(tembel, @"ShrinkForFinishAsync\(string path\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline);
        Assert.True(govde.Success);
        Assert.Contains("OpenBatch(", govde.Groups["body"].Value, StringComparison.Ordinal);
        Assert.Contains("JobFinished", govde.Groups["body"].Value, StringComparison.Ordinal);

        Assert.Contains("ShareCoreAsync(", bitince, StringComparison.Ordinal);
        Assert.DoesNotContain("new ShareFlow(", bitince, StringComparison.Ordinal);
        Assert.DoesNotContain("EncodeRunner", bitince, StringComparison.Ordinal);
    }
}
