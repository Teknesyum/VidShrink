using Avalonia.Controls;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// SIK-P2: kodlama sürerken sistem uyumaz, ekran kapanabilir. İstek ilk iş başlarken alınır, son iş
/// bitince ya da iptalde geri verilir; "bitince uyut" seçiliyse önce bırakılır, sonra uyutulur.
/// Sistem katmanı hep sahtedir: test sürecinde gerçek uyku isteği alınmaz.
/// </summary>
public sealed class UykuEngellemeTests
{
    private sealed class Defter
    {
        public List<string> Olaylar { get; } = new();
        public int Al => Olaylar.Count(o => o == "block");
        public int Birak => Olaylar.Count(o => o == "allow");
    }

    private sealed class SahteEngel : ISleepBlocker
    {
        private readonly Defter _defter;
        public SahteEngel(Defter defter) => _defter = defter;
        public void Block() => _defter.Olaylar.Add("block");
        public void Allow() => _defter.Olaylar.Add("allow");
    }

    private sealed class SahteEylem : IQueueEndActions
    {
        private readonly Defter _defter;
        public SahteEylem(Defter defter) => _defter = defter;
        public void Reveal(string path) => _defter.Olaylar.Add("reveal");
        public void Sleep() => _defter.Olaylar.Add("sleep");
        public void PowerOff() => _defter.Olaylar.Add("power-off");
        public void Quit() => _defter.Olaylar.Add("quit");
    }

    private sealed class SahteKosum
    {
        private readonly Dictionary<string, TaskCompletionSource> _bitis = new();

        public Task Kostur(ShrinkRequest istek, CancellationToken ct)
        {
            var bitis = new TaskCompletionSource();
            _bitis[istek.Path] = bitis;
            ct.Register(() => bitis.TrySetCanceled(ct));
            return bitis.Task;
        }

        public void Bitir(string yol)
        {
            _bitis[yol].TrySetResult();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static ShrinkJobWindow Pencere(Defter defter, SahteKosum kosum, int sinir, params string[] yollar)
        => new(yollar, new PlanOptions { TargetMb = 25 }, false, null)
        {
            Actions = new SahteEylem(defter),
            Kosturucu = kosum.Kostur,
            ParallelLimit = sinir,
            Uyku = new SleepGuard(new SahteEngel(defter)),
            UykuyuEngelle = true
        };

    /// <summary>Sayaç: iki tutuş bir istek alır, son bırakış bir kez geri verir; ikinci Dispose sayılmaz.</summary>
    [Fact]
    public void SayacIlkTutustaAlirSonBirakistaVerir()
    {
        var defter = new Defter();
        var nobet = new SleepGuard(new SahteEngel(defter));

        var bir = nobet.Hold();
        var iki = nobet.Hold();
        var ikiTutus = (defter.Al, defter.Birak, nobet.Holders);
        bir.Dispose();
        bir.Dispose();
        var birKalmisken = (defter.Al, defter.Birak, nobet.Holders);
        iki.Dispose();

        Assert.Equal((1, 0, 2), ikiTutus);
        Assert.Equal((1, 0, 1), birKalmisken);
        Assert.Equal(new[] { "block", "allow" }, defter.Olaylar.ToArray());
        Assert.Equal(0, nobet.Holders);
    }

    /// <summary>Sıradaki işler arasında istek bırakılmaz: üç iş, bir alış, son işten sonra bir bırakış.</summary>
    [Fact]
    public void KuyruktaIstekIlkIsteAlinirSonIsteBirakilir()
    {
        var defter = new Defter();
        var kosum = new SahteKosum();
        var sonuc = AppHost.Run(() =>
        {
            var window = Pencere(defter, kosum, 1, "a.mp4", "b.mp4", "c.mp4");
            try
            {
                var once = defter.Olaylar.ToArray();
                window.Begin();
                var basta = defter.Olaylar.ToArray();
                kosum.Bitir("a.mp4");
                kosum.Bitir("b.mp4");
                var arada = defter.Olaylar.ToArray();
                kosum.Bitir("c.mp4");
                return (once, basta, arada, son: defter.Olaylar.ToArray());
            }
            finally { window.Close(); }
        });

        Assert.Empty(sonuc.once);
        Assert.Equal(new[] { "block" }, sonuc.basta);
        Assert.Equal(new[] { "block" }, sonuc.arada);
        Assert.Equal(new[] { "block", "allow" }, sonuc.son);
        Assert.Equal(new[] { "block", "allow" }, defter.Olaylar.ToArray());
    }

    /// <summary>İki iş birlikte koşarken biri bitince istek sürer; ikincisi bitince bırakılır.</summary>
    [Fact]
    public void EszamanliIslerdeSonBitenBirakir()
    {
        var defter = new Defter();
        var kosum = new SahteKosum();
        var sonuc = AppHost.Run(() =>
        {
            var window = Pencere(defter, kosum, 2, "a.mp4", "b.mp4");
            try
            {
                window.Begin();
                kosum.Bitir("a.mp4");
                var biriBitince = defter.Birak;
                kosum.Bitir("b.mp4");
                return (biriBitince, defter.Al, defter.Birak);
            }
            finally { window.Close(); }
        });

        Assert.Equal((0, 1, 1), sonuc);
    }

    /// <summary>Kapanan pencere koşan işi iptal eder ve isteği geri verir.</summary>
    [Fact]
    public void KapanistaIptalIstegiGeriVerir()
    {
        var defter = new Defter();
        var kosum = new SahteKosum();
        var sonuc = AppHost.Run(() =>
        {
            var window = Pencere(defter, kosum, 1, "a.mp4", "b.mp4");
            window.Begin();
            var kosarken = (defter.Al, defter.Birak);
            window.Close();
            Dispatcher.UIThread.RunJobs();
            return (kosarken, defter.Al, defter.Birak);
        });

        Assert.Equal((1, 0), sonuc.kosarken);
        Assert.Equal(1, sonuc.Al);
        Assert.Equal(1, sonuc.Birak);
    }

    /// <summary>"Bitince uyut": istek önce bırakılır, uyutma ondan sonra gelir.</summary>
    [Fact]
    public void BitinceUyutOnceBirakirSonraUyutur()
    {
        var defter = new Defter();
        var kosum = new SahteKosum();
        AppHost.Run(() =>
        {
            var window = Pencere(defter, kosum, 1, "a.mp4");
            try
            {
                window.WhenDone = QueueEndChoice.Sleep;
                window.Begin();
                kosum.Bitir("a.mp4");
                for (var i = 0; i < ShrinkJobWindow.CountdownSeconds + 5; i++) window.CountdownTick();
            }
            finally { window.Close(); }
        });

        Assert.Equal(new[] { "block", "allow", "sleep" }, defter.Olaylar.ToArray());
    }

    /// <summary>Ayar kapalıyken istek hiç alınmaz.</summary>
    [Fact]
    public void AyarKapaliykenIstekAlinmaz()
    {
        var defter = new Defter();
        var kosum = new SahteKosum();
        AppHost.Run(() =>
        {
            var window = Pencere(defter, kosum, 1, "a.mp4");
            window.UykuyuEngelle = false;
            try
            {
                window.Begin();
                kosum.Bitir("a.mp4");
            }
            finally { window.Close(); }
        });

        Assert.Empty(defter.Olaylar);
    }

    /// <summary>
    /// Ana pencere: kodlama başlayınca istek alınır, bitince bırakılır; Ayarlar'daki kutu kapalıyken
    /// alınmaz. Kutu ayara gidip döner.
    /// </summary>
    [Fact]
    public void AnaPencereKodlamaBoyuncaTutarKutuKapaliysaTutmaz()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "uyku-engelleme", "ana");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        var defter = new Defter();
        try
        {
            var sonuc = AppHost.Run(() =>
            {
                var pencere = new MainWindow { SettingsPathOverride = Path.Combine(klasor, "settings.json"), Uyku = new SleepGuard(new SahteEngel(defter)) };
                try
                {
                    var kutu = pencere.FindControl<CheckBox>("ChkPreventSleep")!;
                    var varsayilan = kutu.IsChecked == true;
                    kutu.IsChecked = true;
                    pencere.ShowEncodeProgressForTest(new EncodeProgress(0.1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(9), 1, "pass 1/1 (attempt 1)"));
                    var kosarken = defter.Olaylar.ToArray();
                    pencere.EndEncodeForTest();
                    var bitince = defter.Olaylar.ToArray();

                    kutu.IsChecked = false;
                    pencere.ShowEncodeProgressForTest(new EncodeProgress(0.1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(9), 1, "pass 1/1 (attempt 1)"));
                    pencere.EndEncodeForTest();
                    var kapali = defter.Olaylar.ToArray();
                    var toplanan = pencere.CaptureAppSettingsForTest().PreventSleepWhileEncoding;
                    pencere.RestoreAppSettingsForTest(new AppSettings { PreventSleepWhileEncoding = true });
                    var geri = kutu.IsChecked == true;
                    return (varsayilan, kosarken, bitince, kapali, toplanan, geri);
                }
                finally { pencere.Close(); }
            });

            Assert.True(sonuc.varsayilan);
            Assert.Equal(new[] { "block" }, sonuc.kosarken);
            Assert.Equal(new[] { "block", "allow" }, sonuc.bitince);
            Assert.Equal(new[] { "block", "allow" }, sonuc.kapali);
            Assert.False(sonuc.toplanan);
            Assert.True(sonuc.geri);
        }
        finally { Directory.Delete(klasor, true); }
    }

    /// <summary>Ayar gidip döner; alanı olmayan eski dosya açık okunur.</summary>
    [Fact]
    public void AyarGidisDonusVeEskiDosyaAcik()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "uyku-engelleme", "ayar");
        if (Directory.Exists(klasor)) Directory.Delete(klasor, true);
        Directory.CreateDirectory(klasor);
        try
        {
            var kapali = Path.Combine(klasor, "kapali.json");
            var eski = Path.Combine(klasor, "eski.json");
            new AppSettings { PreventSleepWhileEncoding = false }.Save(kapali);
            File.WriteAllText(eski, "{\"followRecording\": true}");

            Assert.False(AppSettings.Load(kapali).PreventSleepWhileEncoding);
            Assert.Contains("\"preventSleepWhileEncoding\": false", File.ReadAllText(kapali), StringComparison.Ordinal);
            Assert.True(AppSettings.Load(eski).PreventSleepWhileEncoding);
            Assert.True(new AppSettings().PreventSleepWhileEncoding);
        }
        finally { Directory.Delete(klasor, true); }
    }

    /// <summary>Test sürecinde gerçek katman kapalıdır: varsayılan nöbetin altında kapalı bayrak durur.</summary>
    [Fact]
    public void TestSurecindeGercekKatmanKapali()
    {
        Assert.True(SystemSleepBlocker.Disabled);
        Assert.Same(SystemSleepBlocker.Instance, SleepGuard.Shared.Blocker);
    }
}
