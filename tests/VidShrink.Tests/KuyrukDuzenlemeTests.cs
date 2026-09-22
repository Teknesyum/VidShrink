using VidShrink.App;
using VidShrink.Core;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// C1-2 ve C1-4: kuyruk penceresinde bekleyenler görünür, çıkarılır, kaydırılır; sıra
/// duraklatılır. Sıra boşalınca seçilen eylem çalışır; uyut ve kapat geri sayımla gelir.
/// Sistem çağrısı hep sahtedir: gerçek eylem test sürecinde çalışmaz.
/// </summary>
public sealed class KuyrukDuzenlemeTests
{
    private sealed class SahteEylem : IQueueEndActions
    {
        public List<string> Cagrilar { get; } = new();
        public void Reveal(string path) => Cagrilar.Add("reveal:" + path);
        public void Sleep() => Cagrilar.Add("sleep");
        public void PowerOff() => Cagrilar.Add("power-off");
    }

    private static ShrinkJobWindow Pencere(SahteEylem eylem, params string[] yollar)
    {
        var window = new ShrinkJobWindow(yollar.Length == 0 ? new[] { "a.mp4" } : yollar, new PlanOptions { TargetMb = 25 }, false, null)
        {
            Actions = eylem
        };
        return window;
    }

    private static string[] Adlar(ShrinkJobWindow window) => window.Pending.Select(r => r.Path).ToArray();

    /// <summary>Duraklatılmış sıra başlamaz; taşıma sınırda bir şey yapmaz, çıkarma sayıyı düşürür.</summary>
    [Fact]
    public void BekleyenlerTasinirVeCikarilir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var window = Pencere(new SahteEylem(), "a.mp4", "b.mp4", "c.mp4");
            try
            {
                window.SetPaused(true);
                window.Begin();
                var ilk = Adlar(window);
                window.MovePending(0, 1);
                var tasindi = Adlar(window);
                window.MovePending(0, -1);
                window.MovePending(2, 1);
                var sinir = Adlar(window);
                window.RemovePending(1);
                window.RemovePending(5);
                return (ilk, tasindi, sinir, son: Adlar(window), window.AcceptedCount, window.Paused);
            }
            finally { window.Close(); }
        });

        Assert.Equal(new[] { "a.mp4", "b.mp4", "c.mp4" }, sonuc.ilk);
        Assert.Equal(new[] { "b.mp4", "a.mp4", "c.mp4" }, sonuc.tasindi);
        Assert.Equal(sonuc.tasindi, sonuc.sinir);
        Assert.Equal(new[] { "b.mp4", "c.mp4" }, sonuc.son);
        Assert.Equal(2, sonuc.AcceptedCount);
        Assert.True(sonuc.Paused);
    }

    /// <summary>Kapat seçiliyken sıra boşalınca geri sayım başlar; sıfıra inmeden çağrı yok, inince tek çağrı.</summary>
    [Fact]
    public void KapatGeriSayimdanSonraBirKezCalisir()
    {
        var eylem = new SahteEylem();
        var (basta, sondanBir, sonra, bitti) = AppHost.Run(() =>
        {
            var window = Pencere(eylem);
            try
            {
                window.WhenDone = QueueEndChoice.PowerOff;
                window.QueueDrained();
                var ilk = window.CountdownLeft;
                for (var i = 0; i < ShrinkJobWindow.CountdownSeconds - 1; i++) window.CountdownTick();
                var kalan = eylem.Cagrilar.Count;
                window.CountdownTick();
                for (var i = 0; i < 5; i++) window.CountdownTick();
                return (ilk, kalan, eylem.Cagrilar.ToArray(), window.CountdownLeft);
            }
            finally { window.Close(); }
        });

        Assert.Equal(ShrinkJobWindow.CountdownSeconds, basta);
        Assert.Equal(0, sondanBir);
        Assert.Equal(new[] { "power-off" }, sonra);
        Assert.Null(bitti);
    }

    /// <summary>Uyut geri sayımı vazgeçilince hiç çalışmaz; sonraki saniyeler de tetiklemez.</summary>
    [Fact]
    public void VazgecilenGeriSayimCalismaz()
    {
        var eylem = new SahteEylem();
        var (metin, kalan) = AppHost.Run(() =>
        {
            var window = Pencere(eylem);
            try
            {
                window.WhenDone = QueueEndChoice.Sleep;
                window.QueueDrained();
                window.CountdownTick();
                var yazi = window.CountdownText;
                window.CancelCountdown();
                for (var i = 0; i < ShrinkJobWindow.CountdownSeconds + 1; i++) window.CountdownTick();
                return (yazi, window.CountdownLeft);
            }
            finally { window.Close(); }
        });

        Assert.Contains((ShrinkJobWindow.CountdownSeconds - 1).ToString(), metin, StringComparison.Ordinal);
        Assert.Empty(eylem.Cagrilar);
        Assert.Null(kalan);
    }

    /// <summary>"Hiçbir şey" ya da duraklatılmış sıra geri sayım başlatmaz; çıktısız klasör açma çağrı yapmaz.</summary>
    [Fact]
    public void GeriSayimYalnizBosVeAkanSiradaBaslar()
    {
        var eylem = new SahteEylem();
        var sayimlar = AppHost.Run(() =>
        {
            var hic = Pencere(eylem);
            var duraklatilmis = Pencere(eylem);
            var klasor = Pencere(eylem);
            try
            {
                hic.QueueDrained();

                duraklatilmis.WhenDone = QueueEndChoice.PowerOff;
                duraklatilmis.SetPaused(true);
                duraklatilmis.QueueDrained();

                klasor.WhenDone = QueueEndChoice.OpenFolder;
                klasor.QueueDrained();

                return new[] { hic.CountdownLeft, duraklatilmis.CountdownLeft, klasor.CountdownLeft };
            }
            finally { hic.Close(); duraklatilmis.Close(); klasor.Close(); }
        });

        Assert.All(sayimlar, Assert.Null);
        Assert.Empty(eylem.Cagrilar);
    }

    /// <summary>Uyut ve kapat metinleri ayrı; ikisi de kalan saniyeyi yazar.</summary>
    [Fact]
    public void GeriSayimMetniEylemiSoyler()
    {
        var (uyut, kapat) = AppHost.Run(() =>
        {
            var a = Pencere(new SahteEylem());
            var b = Pencere(new SahteEylem());
            try
            {
                a.WhenDone = QueueEndChoice.Sleep;
                a.QueueDrained();
                b.WhenDone = QueueEndChoice.PowerOff;
                b.QueueDrained();
                return (a.CountdownText, b.CountdownText);
            }
            finally { a.Close(); b.Close(); }
        });

        Assert.NotEqual(uyut, kapat);
        Assert.Contains(ShrinkJobWindow.CountdownSeconds.ToString(), uyut, StringComparison.Ordinal);
        Assert.Contains(ShrinkJobWindow.CountdownSeconds.ToString(), kapat, StringComparison.Ordinal);
    }

    /// <summary>On beş anahtar 42 dilde; sayı taşıyan üçünde yer tutucu var.</summary>
    [Fact]
    public void AnahtarlarButunDillerde()
    {
        var anahtarlar = new[]
        {
            "main.shrink-job.pending", "main.shrink-job.remove", "main.shrink-job.move-up", "main.shrink-job.move-down",
            "main.shrink-job.pause", "main.shrink-job.resume", "main.shrink-job.paused", "main.shrink-job.when-done",
            "main.shrink-job.done.nothing", "main.shrink-job.done.open-folder", "main.shrink-job.done.sleep",
            "main.shrink-job.done.power-off", "main.shrink-job.countdown.sleep", "main.shrink-job.countdown.power-off",
            "main.shrink-job.countdown.cancel"
        };
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            foreach (var key in anahtarlar)
                Assert.True(values.TryGetValue(key, out var metin) && metin.Length > 0, $"{language}: {key}");
            foreach (var key in new[] { "main.shrink-job.pending", "main.shrink-job.countdown.sleep", "main.shrink-job.countdown.power-off" })
                Assert.Contains("{0}", values[key], StringComparison.Ordinal);
        }
    }
}
