using Avalonia.Controls;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Recorder;
using VidShrink.Core.Share;
using VidShrink.Ffmpeg;
using Xunit;
using ShareTarget = VidShrink.Core.Share.ShareTarget;

namespace VidShrink.Tests;

/// <summary>
/// Kaydedicinin Paylaş satırı (EKB-27) ve "küçült, yükle, bağlantıyı kopyala" zinciri (EKB-33).
/// Hedef ve ömür kutusu <c>paylasim-hedefleri.json</c>'dan dolar, seçim
/// <c>recorder-settings.json</c>'a gider ve yüklemeye o hedefle o ömür geçer; Sil yalnız jetonlu
/// bağlantıyı kapatır. Yükleme sahte sağlayıcıyla, ağa çıkmadan ölçülür
/// (<see cref="DuzenleKaydetPaylasTests"/> düzeni). Kanıt <c>.calisma/kaydedici-paylasim-satiri/</c>.
/// </summary>
public sealed class KaydediciPaylasimSatiriTests
{
    private static string Klasor()
    {
        var yol = Path.Combine(GirdiKanit.Root, ".calisma", "kaydedici-paylasim-satiri");
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

    private static string Ayar(string ad)
    {
        var yol = Path.Combine(Klasor(), ad);
        if (File.Exists(yol)) File.Delete(yol);
        return yol;
    }

    private static string Metin(string anahtar) => LanguageCatalog.Display(Strings.Get(anahtar));

    private static string Metin(string anahtar, params object?[] args)
        => string.Format(Strings.Culture, LanguageCatalog.Display(Strings.Get(anahtar)), args);

    private static RecordResult Tam(string yol) => new(true, yol, 1, false, 0, string.Empty, 1);

    private sealed class SahtePano : IRecordingClipboard
    {
        public List<string> Dosyalar { get; } = new();

        public List<string> Metinler { get; } = new();

        public Task SetFileAsync(string path)
        {
            Dosyalar.Add(path);
            return Task.CompletedTask;
        }

        public Task SetTextAsync(string text)
        {
            Metinler.Add(text);
            return Task.CompletedTask;
        }
    }

    private sealed class Kayitci
    {
        public List<(string Hedef, string Dosya, int? Gun)> Yuklenen { get; } = new();

        public List<string> Silinen { get; } = new();

        public bool Jeton { get; set; } = true;

        public bool SilmeDusur { get; set; }
    }

    private sealed class SahteSaglayici : IShareProvider
    {
        private readonly Kayitci _kayit;

        public SahteSaglayici(ShareTarget target, Kayitci kayit)
        {
            Target = target;
            _kayit = kayit;
        }

        public ShareTarget Target { get; }

        public bool CanDelete => Target.CanDelete;

        public Task<ShareResult> UploadAsync(string filePath, int? retentionDays = null, IProgress<UploadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            _kayit.Yuklenen.Add((Target.Id, filePath, retentionDays));
            var no = _kayit.Yuklenen.Count;
            return Task.FromResult(ShareResult.Success(new ShareLink(Target.Id, $"b{no}", $"https://ornek.test/b{no}",
                Path.GetFileName(filePath), DateTimeOffset.UtcNow, null, _kayit.Jeton ? $"jeton{no}" : null)));
        }

        public Task<ShareResult> CheckHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ShareResult.Success(new ShareLink(Target.Id, "h", "https://ornek.test/h", "h", DateTimeOffset.UtcNow)));

        public Task<ShareResult> DeleteAsync(ShareLink link, CancellationToken cancellationToken = default)
        {
            _kayit.Silinen.Add(link.FileId);
            return Task.FromResult(_kayit.SilmeDusur
                ? ShareResult.Failed(new ShareDiagnosis(ShareFailure.Unknown, "sunucu-reddetti"))
                : ShareResult.Success(link));
        }
    }

    private sealed class Duzenek
    {
        public required RecorderView View { get; init; }

        public required Window Window { get; init; }

        public SahtePano Pano { get; } = new();

        public Kayitci Kayit { get; } = new();

        public List<string> Kucultulen { get; } = new();

        public string? KucultmeCiktisi { get; set; }

        public void Bekle(Task gorev)
        {
            var saat = System.Diagnostics.Stopwatch.StartNew();
            while (!gorev.IsCompleted && saat.Elapsed.TotalSeconds < 10)
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
        view.CreateShareFlow = () => new ShareFlow(t => new SahteSaglayici(t, duzenek.Kayit), new ShareLedger(ledger));
        return duzenek;
    }

    /// <summary>
    /// Hedef kutusu tablonun hedeflerini sırasıyla gösterir ve varsayılanla açılır. Ömür seçtiren
    /// hedefte (storage.to) ömür kutusu tablonun gün listesini taşır, Sil görünür ama bağlantı
    /// yokken basılmaz; sabit ömürlü hedefte (uguu.se) ömür kutusu yerine sabit ömür yazılır ve
    /// Sil hiç görünmez. Seçim ayar dosyasına gider, yeni görünüm aynı seçimle açılır.
    /// </summary>
    [Fact]
    public void HedefVeOmurKutusuTablodanGelirVeKalicidir()
    {
        var ayar = Ayar("ayar-kutu.json");
        var tablo = ShareTargetTable.Load();
        var depo = tablo.Targets.ToList().FindIndex(t => t.RetentionDays.Count > 0 && t.CanDelete);
        var sabit = tablo.Targets.ToList().FindIndex(t => t.RetentionDays.Count == 0 && !t.CanDelete && t.FixedRetentionHours is not null);
        Assert.True(depo >= 0 && sabit >= 0);

        var olcu = AppHost.Run(() =>
        {
            var d = Kur(ayar);
            try
            {
                var kutu = d.View.FindControl<ComboBox>("CmbRecShareTarget")!;
                var omur = d.View.FindControl<ComboBox>("CmbRecShareRetention")!;
                var adlar = kutu.ItemsSource!.Cast<string>().ToList();
                var acilis = d.View.ShareTargetIndex;
                var acilisGun = omur.ItemsSource!.Cast<string>().ToList();
                var acilisSil = (d.View.ShareDeleteVisible, d.View.ShareDeleteEnabled, d.View.ShareRetentionVisible);

                d.View.ShareTargetIndex = sabit;
                var sabitHal = (d.View.ShareRetentionVisible, d.View.ShareRetentionFixedText, d.View.ShareDeleteVisible);
                var sabitJson = File.ReadAllText(ayar);

                d.View.ShareTargetIndex = depo;
                d.View.ShareRetentionIndex = 2;
                var depoJson = File.ReadAllText(ayar);
                return (adlar, acilis, acilisGun, acilisSil, sabitHal, sabitJson, depoJson);
            }
            finally { d.Window.Close(); }
        });

        var geri = AppHost.Run(() =>
        {
            var d = Kur(ayar);
            try { return (d.View.ShareTargetIndex, d.View.ShareRetentionIndex, d.View.SelectedShareRetentionDays); }
            finally { d.Window.Close(); }
        });

        File.WriteAllText(Path.Combine(Klasor(), "kutu.txt"),
            $"adlar: {string.Join(", ", olcu.adlar)}\nacilis: {olcu.acilis}\ngunler: {string.Join(", ", olcu.acilisGun)}\nsabit: {olcu.sabitHal}\ngeri: {geri}\n{olcu.depoJson}\n");

        Assert.Equal(tablo.Targets.Select(t => t.DisplayName), olcu.adlar);
        Assert.Equal(tablo.Targets.ToList().FindIndex(t => t.Id == tablo.DefaultTarget!.Id), olcu.acilis);
        var gunler = tablo.Targets[depo].RetentionDays;
        Assert.Equal(gunler.Select(g => g == 1 ? Metin("settings.share.day-one") : Metin("settings.share.days", g)), olcu.acilisGun);
        Assert.Equal((true, false, true), olcu.acilisSil);

        Assert.False(olcu.sabitHal.Item1);
        Assert.Equal(Metin("settings.share.hours-fixed", tablo.Targets[sabit].FixedRetentionHours), olcu.sabitHal.Item2);
        Assert.False(olcu.sabitHal.Item3);
        Assert.Contains($"\"shareTarget\": \"{tablo.Targets[sabit].Id}\"", olcu.sabitJson, StringComparison.Ordinal);
        Assert.DoesNotContain("shareRetentionDays", olcu.sabitJson, StringComparison.Ordinal);

        Assert.Contains($"\"shareTarget\": \"{tablo.Targets[depo].Id}\"", olcu.depoJson, StringComparison.Ordinal);
        Assert.Contains($"\"shareRetentionDays\": {gunler[2]}", olcu.depoJson, StringComparison.Ordinal);
        Assert.Equal((depo, 2, (int?)gunler[2]), geri);
        Assert.Equal(tablo.Targets[depo].Id, RecorderSettings.Load(ayar).ShareTargetId);

        AppHost.Run(() =>
        {
            var d = Kur(ayar);
            try { d.View.ShareTargetIndex = sabit; }
            finally { d.Window.Close(); }
        });
        var geriSabit = AppHost.Run(() =>
        {
            var d = Kur(ayar);
            try { return d.View.ShareTargetIndex; }
            finally { d.Window.Close(); }
        });
        Assert.Equal(sabit, geriSabit);

        Kapat("ayar-kutu.json", "kutu.txt", "paylasimlar.json");
    }

    /// <summary>
    /// Yükleme satırdaki hedefe, satırdaki ömürle gider: storage.to'da üçüncü seçenek seçilince
    /// sağlayıcı o günü alır; sabit ömürlü hedefte ömür boş geçer. Olumsuz kontrol: seçim
    /// değişmeden yüklemede tablonun varsayılan ömrü gider.
    /// </summary>
    [Fact]
    public void YuklemeSeciliHedefVeOmurleGider()
    {
        var ayar = Ayar("ayar-yukle.json");
        var kayit = Dosya("kayit-yukle.mp4");
        var tablo = ShareTargetTable.Load();
        var depo = tablo.Targets.ToList().FindIndex(t => t.RetentionDays.Count > 0);
        var sabit = tablo.Targets.ToList().FindIndex(t => t.RetentionDays.Count == 0);

        var yuklenen = AppHost.Run(() =>
        {
            var d = Kur(ayar);
            try
            {
                d.View.Deliver(Tam(kayit));
                d.View.ShareTargetIndex = depo;
                var ilk = d.View.FinishAsync(RecorderFinishAction.UploadAndCopyLink, kayit);
                d.Bekle(ilk);

                d.View.ShareRetentionIndex = 2;
                var ikinci = d.View.FinishAsync(RecorderFinishAction.UploadAndCopyLink, kayit);
                d.Bekle(ikinci);

                d.View.ShareTargetIndex = sabit;
                var ucuncu = d.View.FinishAsync(RecorderFinishAction.UploadAndCopyLink, kayit);
                d.Bekle(ucuncu);
                return d.Kayit.Yuklenen.ToList();
            }
            finally { d.Window.Close(); }
        });

        File.WriteAllLines(Path.Combine(Klasor(), "yukle.txt"), yuklenen.Select(y => $"{y.Hedef} {Path.GetFileName(y.Dosya)} gun={y.Gun}"));

        Assert.Equal(3, yuklenen.Count);
        Assert.Equal((tablo.Targets[depo].Id, kayit, tablo.Targets[depo].DefaultRetentionDays), yuklenen[0]);
        Assert.Equal((tablo.Targets[depo].Id, kayit, (int?)tablo.Targets[depo].RetentionDays[2]), yuklenen[1]);
        Assert.Equal((tablo.Targets[sabit].Id, kayit, (int?)null), yuklenen[2]);

        Kapat("ayar-yukle.json", "kayit-yukle.mp4", "yukle.txt", "paylasimlar.json");
    }

    /// <summary>
    /// Sil jetonlu bağlantıyı kapatır: sağlayıcının silme çağrısı bir kez gelir, bağlantı satırı
    /// düşer, satır "kapatıldı" der ve düğme kapanır. Sunucu reddederse bağlantı yerinde kalır,
    /// neden yazılır, düğme açık kalır. Jetonsuz bağlantıda düğme basılmaz ve sağlayıcı çağrılmaz.
    /// </summary>
    [Fact]
    public void SilJetonluBaglantiyiKapatir()
    {
        var ayar = Ayar("ayar-sil.json");
        var kayit = Dosya("kayit-sil.mp4");

        var olcu = AppHost.Run(() =>
        {
            var d = Kur(ayar);
            try
            {
                d.View.Deliver(Tam(kayit));
                d.Kayit.Jeton = true;
                d.Kayit.SilmeDusur = true;
                d.Bekle(d.View.FinishAsync(RecorderFinishAction.UploadAndCopyLink, kayit));
                var hazir = d.View.ShareDeleteEnabled;
                var red = d.View.DeleteShareAsync();
                d.Bekle(red);
                var redHal = (red.Result, d.View.ShareLinkText, d.View.ShareStatusText, d.View.ShareDeleteEnabled);

                d.Kayit.SilmeDusur = false;
                var kabul = d.View.DeleteShareAsync();
                d.Bekle(kabul);
                var kabulHal = (kabul.Result, d.View.ShareLinkText, d.View.ShareStatusText, d.View.ShareDeleteEnabled);

                d.Kayit.Jeton = false;
                d.Bekle(d.View.FinishAsync(RecorderFinishAction.UploadAndCopyLink, kayit));
                var jetonsuzHazir = d.View.ShareDeleteEnabled;
                var jetonsuz = d.View.DeleteShareAsync();
                d.Bekle(jetonsuz);
                return (hazir, redHal, kabulHal, jetonsuzHazir, jetonsuz.Result, d.Kayit.Silinen.ToList(), d.View.ShareLinkText);
            }
            finally { d.Window.Close(); }
        });

        File.WriteAllText(Path.Combine(Klasor(), "sil.txt"), $"{olcu}\n");

        Assert.True(olcu.hazir);
        Assert.False(olcu.redHal.Result);
        Assert.Equal("https://ornek.test/b1", olcu.redHal.ShareLinkText);
        Assert.StartsWith(Metin("settings.share.close-failed"), olcu.redHal.ShareStatusText, StringComparison.Ordinal);
        Assert.Contains("sunucu-reddetti", olcu.redHal.ShareStatusText, StringComparison.Ordinal);
        Assert.True(olcu.redHal.ShareDeleteEnabled);

        Assert.True(olcu.kabulHal.Result);
        Assert.Equal(string.Empty, olcu.kabulHal.ShareLinkText);
        Assert.Equal(Metin("settings.share.closed"), olcu.kabulHal.ShareStatusText);
        Assert.False(olcu.kabulHal.ShareDeleteEnabled);

        Assert.False(olcu.jetonsuzHazir);
        Assert.False(olcu.Item5);
        Assert.Equal(new[] { "b1", "b1" }, olcu.Item6);
        Assert.Equal("https://ornek.test/b2", olcu.ShareLinkText);

        Kapat("ayar-sil.json", "kayit-sil.mp4", "sil.txt", "paylasimlar.json");
    }

    /// <summary>
    /// Zincir sırayla koşar: küçültülen çıktı yüklenir (kaydın kendisi değil), satırdaki ömürle,
    /// bağlantı panoya gider. Küçültme çıktı vermezse yükleme hiç çağrılmaz, pano boş kalır ve
    /// satır küçültme hatasını <c>StatusError</c> olarak yazar.
    /// </summary>
    [Fact]
    public void ZincirKucultulenCiktiyiYukler()
    {
        var ayar = Ayar("ayar-zincir.json");
        var kayit = Dosya("kayit-zincir.mp4");
        var cikti = Dosya("kayit-zincir-kucuk.mp4");
        var tablo = ShareTargetTable.Load();
        var depo = tablo.Targets.ToList().FindIndex(t => t.RetentionDays.Count > 3);

        var olcu = AppHost.Run(() =>
        {
            var d = Kur(ayar);
            try
            {
                d.View.ShareTargetIndex = depo;
                d.View.ShareRetentionIndex = 3;
                d.KucultmeCiktisi = null;
                d.View.Deliver(Tam(kayit));
                d.Bekle(d.View.FinishAsync(RecorderFinishAction.ShrinkUploadAndCopyLink, kayit));
                var dusen = (d.Kucultulen.Count, d.Kayit.Yuklenen.Count, d.Pano.Metinler.Count, d.View.FinishStatusText, d.View.FinishStatusIsError);

                d.KucultmeCiktisi = cikti;
                d.Bekle(d.View.FinishAsync(RecorderFinishAction.ShrinkUploadAndCopyLink, kayit));
                return (dusen, d.Kucultulen.ToList(), d.Kayit.Yuklenen.ToList(), d.Pano.Metinler.ToList(), d.Pano.Dosyalar.Count,
                    d.View.FinishStatusText, d.View.FinishStatusIsError, d.View.ShareLinkText);
            }
            finally { d.Window.Close(); }
        });

        File.WriteAllText(Path.Combine(Klasor(), "zincir.txt"), $"{olcu}\n");

        Assert.Equal((1, 0, 0, Metin("recorder.finish.shrink-failed"), true), olcu.dusen);
        Assert.Equal(new[] { kayit, kayit }, olcu.Item2);
        Assert.Equal(new[] { (tablo.Targets[depo].Id, cikti, (int?)tablo.Targets[depo].RetentionDays[3]) }, olcu.Item3);
        Assert.Equal(new[] { "https://ornek.test/b1" }, olcu.Item4);
        Assert.Equal(0, olcu.Item5);
        Assert.Equal(Metin("recorder.finish.link-copied"), olcu.Item6);
        Assert.False(olcu.Item7);
        Assert.Equal("https://ornek.test/b1", olcu.Item8);

        Kapat("ayar-zincir.json", "kayit-zincir.mp4", "kayit-zincir-kucuk.mp4", "zincir.txt", "paylasimlar.json");
    }

    /// <summary>Yeni bitiş seçeneğinin adı kırk iki dilde dolu ve İngilizceden ayrı (en dışında).</summary>
    [Fact]
    public void ZincirAdiButunDillerde()
    {
        Assert.Equal(42, Locales.Languages.Count);
        var en = Locales.Domain("en", "recorder")["recorder.finish.shrink-upload-copy"];
        foreach (var dil in Locales.Languages)
        {
            var degerler = Locales.Domain(dil, "recorder");
            Assert.True(degerler.TryGetValue("recorder.finish.shrink-upload-copy", out var deger) && !string.IsNullOrWhiteSpace(deger), dil);
            if (dil != "en") Assert.NotEqual(en, deger);
        }
        Assert.Equal("recorder.finish.shrink-upload-copy", RecorderView.FinishLabelKey(RecorderFinishAction.ShrinkUploadAndCopyLink));
    }
}
