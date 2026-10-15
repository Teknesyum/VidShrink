using Avalonia.Threading;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// SIK-P9: yarım kalan kuyruk saklanır ve açılışta kendiliğinden, duraklatılmış gelir; soru sorulmaz.
/// Defter hep test klasöründedir: kullanıcı profiline yazılmaz. "Süreci kesmek" defterin
/// kapatılmadan bırakılması ve dosyanın başka süreç kimliğiyle okunmasıdır.
/// </summary>
public sealed class KuyrukSaklamaTests
{
    private static readonly string Kok = Path.Combine(TestPaths.OutputRoot, "kuyruk-saklama");

    private static string TemizKlasor(string ad)
    {
        var klasor = Path.Combine(Kok, ad);
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    private static void Sil(string klasor)
    {
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
    }

    private static string[] Videolar(string klasor, params string[] adlar)
    {
        var yollar = adlar.Select(ad => Path.Combine(klasor, ad)).ToArray();
        foreach (var yol in yollar) File.WriteAllText(yol, "");
        return yollar;
    }

    private sealed class SahteKosum
    {
        private readonly Dictionary<string, TaskCompletionSource> _bitis = new();
        public List<string> Baslayan { get; } = new();

        public Task Kostur(ShrinkRequest istek, CancellationToken ct)
        {
            var bitis = new TaskCompletionSource();
            _bitis[istek.Path] = bitis;
            Baslayan.Add(istek.Path);
            ct.Register(() => bitis.TrySetCanceled(ct));
            return bitis.Task;
        }

        public void Bitir(string yol)
        {
            _bitis[yol].TrySetResult();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static string[] Yollar(IEnumerable<SavedJob> isler) => isler.Select(j => j.Path).ToArray();

    /// <summary>
    /// Yaz, süreci kes, yeniden oku: ölü sürecin dosyası alınır ve silinir, kaynağı kalmayan iş elenir,
    /// tavanlı hedef bayrağıyla döner. Yaşayan sürecin dosyasına dokunulmaz; geçici dosya kalmaz.
    /// </summary>
    [Fact]
    public void YazKesYenidenOku()
    {
        var klasor = TemizKlasor("yaz-kes-oku");
        try
        {
            var defter = Path.Combine(klasor, "queue");
            var (a, b, c) = (Path.Combine(klasor, "a.mp4"), Path.Combine(klasor, "b.mp4"), Path.Combine(klasor, "c.mp4"));
            var olen = new QueueJournal(defter, 101);
            olen.Update(new object(), new[] { new SavedJob(a, 25, false), new SavedJob(b, 0, true), new SavedJob(c, 8.5, false) });
            var yasayan = new QueueJournal(defter, 102);
            yasayan.Update(new object(), new[] { new SavedJob(a, 10, false) });

            var dosyalar = Directory.GetFiles(defter).Select(Path.GetFileName).OrderBy(ad => ad, StringComparer.Ordinal).ToArray();
            var yeni = new QueueJournal(defter, 103);
            var alinan = yeni.TakeOrphans(pid => pid == 102, yol => yol != b);
            var kalan = Directory.GetFiles(defter).Select(Path.GetFileName).ToArray();

            Assert.Equal(new[] { "101.json", "102.json" }, dosyalar);
            Assert.Equal(new[] { new SavedJob(a, 25, false), new SavedJob(c, 8.5, false) }, alinan.ToArray());
            Assert.Equal(new[] { "102.json" }, kalan);

            var tavan = new QueueJournal(defter, 104);
            tavan.Update(new object(), new[] { new SavedJob(b, 0, true) });
            Assert.Equal(new[] { new SavedJob(b, 0, true) }, new QueueJournal(defter, 105).TakeOrphans(pid => pid == 102, _ => true).ToArray());
        }
        finally { Sil(klasor); }
    }

    /// <summary>Bozuk ya da yarım yazılmış dosya boş kuyruk verir, açılışı düşürmez; bozuk dosya silinir.</summary>
    [Fact]
    public void BozukDosyaBosKuyrukVerir()
    {
        var klasor = TemizKlasor("bozuk");
        try
        {
            var defter = Path.Combine(klasor, "queue");
            Directory.CreateDirectory(defter);
            File.WriteAllText(Path.Combine(defter, "201.json"), "{\"jobs\": [ {\"path\": ");
            File.WriteAllText(Path.Combine(defter, "202.json"), "[1, 2, 3]");
            File.WriteAllText(Path.Combine(defter, "203.json.tmp"), "{\"jobs\": []}");

            var alinan = new QueueJournal(defter, 204).TakeOrphans(_ => false, _ => true);
            var acilis = AppHost.Run(() => ShrinkJobWindow.Restore(new QueueJournal(defter, 205), _ => false) is null);

            Assert.Empty(alinan);
            Assert.True(acilis);
            Assert.False(File.Exists(Path.Combine(defter, "201.json")));
            Assert.False(File.Exists(Path.Combine(defter, "202.json")));
        }
        finally { Sil(klasor); }
    }

    /// <summary>
    /// Pencere her değişiklikte yazar: önce koşan, sonra bekleyenler. Biten ve çıkarılan iş düşer,
    /// kuyruk boşalınca dosya silinir.
    /// </summary>
    [Fact]
    public void PencereHerDegisiklikteYazarBosalincaSiler()
    {
        var klasor = TemizKlasor("pencere");
        try
        {
            var yollar = Videolar(klasor, "a.mp4", "b.mp4", "c.mp4", "d.mp4");
            var defter = new QueueJournal(Path.Combine(klasor, "queue"), 301);
            var kosum = new SahteKosum();
            var sonuc = AppHost.Run(() =>
            {
                var window = new ShrinkJobWindow(yollar, new PlanOptions { TargetMb = 12 }, false, null)
                {
                    Kosturucu = kosum.Kostur,
                    ParallelLimit = 1,
                    Defter = defter
                };
                try
                {
                    window.Begin();
                    var basta = QueueJournal.Read(defter.FilePath);
                    window.RemovePending(2);
                    var cikinca = Yollar(QueueJournal.Read(defter.FilePath));
                    kosum.Bitir(yollar[0]);
                    var bitince = Yollar(QueueJournal.Read(defter.FilePath));
                    kosum.Bitir(yollar[1]);
                    kosum.Bitir(yollar[2]);
                    return (basta, cikinca, bitince, varMi: File.Exists(defter.FilePath));
                }
                finally { window.Close(); }
            });

            Assert.Equal(yollar.Select(y => new SavedJob(y, 12, false)).ToArray(), sonuc.basta.ToArray());
            Assert.Equal(new[] { yollar[0], yollar[1], yollar[2] }, sonuc.cikinca);
            Assert.Equal(new[] { yollar[1], yollar[2] }, sonuc.bitince);
            Assert.False(sonuc.varMi);
        }
        finally { Sil(klasor); }
    }

    /// <summary>
    /// Kullanıcının kapattığı pencere kuyruğu defterden siler; uygulama ya da sistem kapanırken kapanan
    /// pencere defteri bırakır ki açılışta geri gelsin.
    /// </summary>
    [Fact]
    public void KullaniciKapatinceSilinirKapanistaKalir()
    {
        var klasor = TemizKlasor("kapanis");
        try
        {
            var yollar = Videolar(klasor, "a.mp4", "b.mp4");
            var kullanici = new QueueJournal(Path.Combine(klasor, "kullanici"), 401);
            var sistem = new QueueJournal(Path.Combine(klasor, "sistem"), 402);
            AppHost.Run(() =>
            {
                var birinci = new ShrinkJobWindow(yollar, new PlanOptions { TargetMb = 12 }, false, null) { Kosturucu = new SahteKosum().Kostur, Defter = kullanici };
                birinci.Begin();
                birinci.Close();
                Dispatcher.UIThread.RunJobs();

                var ikinci = new ShrinkJobWindow(yollar, new PlanOptions { TargetMb = 12 }, false, null) { Kosturucu = new SahteKosum().Kostur, Defter = sistem };
                ikinci.Begin();
                ikinci.CloseJournal(keep: true);
                ikinci.Close();
                Dispatcher.UIThread.RunJobs();
            });

            Assert.False(File.Exists(kullanici.FilePath));
            Assert.Equal(yollar, Yollar(QueueJournal.Read(sistem.FilePath)));
        }
        finally { Sil(klasor); }
    }

    /// <summary>
    /// Açılış: ölü sürecin kuyruğu kendiliğinden, duraklatılmış gelir. Kesilen iş baştan sıraya girer,
    /// hiçbir iş başlamaz, durum satırı geri yüklemeyi söyler ve kuyruk yeni sürecin dosyasına yazılır.
    /// Sürdürünce ilk iş koşar; tavanlı iş bayrağını korur.
    /// </summary>
    [Fact]
    public void AcilistaDuraklatilmisGelir()
    {
        var klasor = TemizKlasor("acilis");
        try
        {
            var yollar = Videolar(klasor, "a.mp4", "b.mp4", "c.mp4");
            var klasorDefter = Path.Combine(klasor, "queue");
            var eski = new QueueJournal(klasorDefter, 501);
            var yeni = new QueueJournal(klasorDefter, 502);
            var kosum = new SahteKosum();
            var sonuc = AppHost.Run(() =>
            {
                var kesilen = new ShrinkJobWindow(yollar, new PlanOptions { TargetMb = 12 }, true, null) { Kosturucu = new SahteKosum().Kostur, Defter = eski };
                kesilen.Begin();
                var kosuyordu = kesilen.RunningJobs.Select(r => r.Path).ToArray();

                var window = ShrinkJobWindow.Restore(yeni, pid => pid != 501)!;
                window.Kosturucu = kosum.Kostur;
                try
                {
                    window.Begin();
                    var durum = (window.Paused, bekleyen: window.Pending.Select(r => r.Path).ToArray(), baslayan: kosum.Baslayan.ToArray(),
                        yazi: window.PausedText, yeniDosya: QueueJournal.Read(yeni.FilePath).ToArray(), eskiVar: File.Exists(eski.FilePath));
                    window.SetPaused(false);
                    return (kosuyordu, durum, surunce: kosum.Baslayan.ToArray(), dil: window.Language);
                }
                finally
                {
                    window.Close();
                    kesilen.CloseJournal(keep: true);
                    kesilen.Close();
                }
            });

            Assert.Equal(new[] { yollar[0] }, sonuc.kosuyordu);
            Assert.True(sonuc.durum.Paused);
            Assert.Equal(yollar, sonuc.durum.bekleyen);
            Assert.Empty(sonuc.durum.baslayan);
            Assert.Equal(LanguageCatalogTitle(sonuc.dil, "main.shrink-job.restored"), sonuc.durum.yazi);
            Assert.Equal(yollar.Select(y => new SavedJob(y, 12, true)).ToArray(), sonuc.durum.yeniDosya);
            Assert.False(sonuc.durum.eskiVar);
            Assert.Equal(new[] { yollar[0] }, sonuc.surunce);
        }
        finally { Sil(klasor); }
    }

    private static string LanguageCatalogTitle(string dil, string anahtar)
        => LanguageCatalog.Title(Strings.GetIn(dil, anahtar), dil);

    /// <summary>Test sürecinde gerçek defter kapalıdır: pencereler kullanıcı profiline yazmaz.</summary>
    [Fact]
    public void TestSurecindeGercekDefterKapali()
    {
        Assert.True(QueueJournal.Disabled);
        Assert.Null(QueueJournal.Shared);
        var sonuc = AppHost.Run(() =>
        {
            var window = new ShrinkJobWindow(new[] { "a.mp4" }, new PlanOptions { TargetMb = 12 }, false, null);
            try { return window.Defter is null; }
            finally { window.Close(); }
        });
        Assert.True(sonuc);
    }
}
