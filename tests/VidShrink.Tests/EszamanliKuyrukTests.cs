using System.Collections.Concurrent;
using System.Diagnostics;
using Avalonia.Threading;
using VidShrink.App;
using VidShrink.Core;
using VidShrink.Ffmpeg;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// K6: kuyrukta aynı anda kodlanan iş sayısı. Kararlar (<see cref="ParallelJobs"/>) saf ölçülür;
/// pompa (<see cref="JobPump{T}"/>) elle bitirilen sahte koşturucuyla, pencere sahte
/// <c>Kosturucu</c> ve sahte kuyruk sonu eylemiyle. Gerçek ffmpeg tek kolda: iki kısa klip,
/// <c>-threads 2</c>, en çok iki süreç.
/// </summary>
public sealed class EszamanliKuyrukTests
{
    private sealed class Is
    {
        public Is(string ad) => Ad = ad;
        public string Ad { get; }
        public TaskCompletionSource Bitis { get; } = new();
        public bool IptalGordu { get; set; }
    }

    private sealed class Duzenek
    {
        public int Sinir = 1;
        public readonly List<string> Baslayan = new();
        public readonly List<string> Hatalar = new();
        public int Bosaldi;
        public int Kosan;
        public int EnCokKosan;
        public readonly JobPump<Is> Pompa;

        public Duzenek()
        {
            Pompa = new JobPump<Is>(Kostur, () => Sinir);
            Pompa.Drained += () => Interlocked.Increment(ref Bosaldi);
            Pompa.Failed += (is_, ex) => { lock (Hatalar) Hatalar.Add(is_.Ad + ":" + ex.Message); };
        }

        private async Task Kostur(Is is_, CancellationToken ct)
        {
            lock (Baslayan) Baslayan.Add(is_.Ad);
            var simdi = Interlocked.Increment(ref Kosan);
            lock (Baslayan) EnCokKosan = Math.Max(EnCokKosan, simdi);
            using var kayit = ct.Register(() =>
            {
                is_.IptalGordu = true;
                is_.Bitis.TrySetCanceled(ct);
            });
            try { await is_.Bitis.Task; }
            finally { Interlocked.Decrement(ref Kosan); }
        }

        public string[] BaslayanAdlar()
        {
            lock (Baslayan) return Baslayan.ToArray();
        }
    }

    private static void Bekle(Func<bool> kosul, string ne)
    {
        var saat = Stopwatch.StartNew();
        while (!kosul())
        {
            Assert.True(saat.Elapsed < TimeSpan.FromSeconds(10), "beklenen olmadı: " + ne);
            Thread.Sleep(5);
        }
    }

    private static Is[] Isler(params string[] adlar) => adlar.Select(ad => new Is(ad)).ToArray();

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(6, 3)]
    [InlineData(8, 4)]
    [InlineData(16, 4)]
    [InlineData(64, 4)]
    [InlineData(0, 1)]
    public void UstSinirHerIseIkiCekirdekVeTavanDort(int cekirdek, int beklenen)
    {
        Assert.Equal(beklenen, ParallelJobs.Max(cekirdek));
    }

    [Fact]
    public void VarsayilanBirVeIstekSinirlaraCekilir()
    {
        Assert.Equal(1, ParallelJobs.Default);
        Assert.Equal(1, ParallelJobs.Clamp(0, 16));
        Assert.Equal(1, ParallelJobs.Clamp(-3, 16));
        Assert.Equal(3, ParallelJobs.Clamp(3, 16));
        Assert.Equal(4, ParallelJobs.Clamp(9, 16));
        Assert.Equal(2, ParallelJobs.Clamp(4, 4));
        Assert.Equal(1, ParallelJobs.Clamp(4, 2));
    }

    [Theory]
    [InlineData(5, 0, 1, false, 1)]
    [InlineData(5, 1, 1, false, 0)]
    [InlineData(5, 0, 2, false, 2)]
    [InlineData(5, 1, 2, false, 1)]
    [InlineData(5, 2, 2, false, 0)]
    [InlineData(1, 0, 4, false, 1)]
    [InlineData(0, 0, 4, false, 0)]
    [InlineData(5, 3, 2, false, 0)]
    [InlineData(5, 0, 0, false, 1)]
    [InlineData(5, 0, 2, true, 0)]
    public void BaslatilacakSayiSiniriAsmaz(int bekleyen, int kosan, int sinir, bool durakli, int beklenen)
    {
        Assert.Equal(beklenen, ParallelJobs.StartCount(bekleyen, kosan, sinir, durakli));
    }

    [Fact]
    public void KuyrukSonuYalnizHerSeyBitinceVeBirIsBittiyse()
    {
        Assert.True(ParallelJobs.Drained(0, 0, false, 1));
        Assert.False(ParallelJobs.Drained(0, 0, false, 0));
        Assert.False(ParallelJobs.Drained(1, 0, false, 1));
        Assert.False(ParallelJobs.Drained(0, 1, false, 1));
        Assert.False(ParallelJobs.Drained(0, 0, true, 1));
    }

    [Fact]
    public void DonanimYuvasiYalnizDonanimKodlayicisinda()
    {
        Assert.True(ParallelJobs.NeedsHardwareSlot("h264_nvenc"));
        Assert.True(ParallelJobs.NeedsHardwareSlot("hevc_qsv"));
        Assert.True(ParallelJobs.NeedsHardwareSlot("av1_amf"));
        Assert.False(ParallelJobs.NeedsHardwareSlot("libx264"));
        Assert.False(ParallelJobs.NeedsHardwareSlot("libsvtav1"));
        Assert.False(ParallelJobs.NeedsHardwareSlot(null));
        Assert.False(ParallelJobs.NeedsHardwareSlot(""));
        Assert.Equal(2, ParallelJobs.HardwareSlots);
    }

    [Fact]
    public void SinirBirkenIslerSiraylaKosar()
    {
        var d = new Duzenek { Sinir = 1 };
        var isler = Isler("a", "b", "c");
        foreach (var is_ in isler) d.Pompa.Enqueue(is_);

        Assert.Equal(new[] { "a" }, d.BaslayanAdlar());
        Assert.Equal(2, d.Pompa.PendingCount);

        isler[0].Bitis.SetResult();
        Bekle(() => d.BaslayanAdlar().Length == 2, "b başlasın");
        Assert.Equal(1, d.Pompa.RunningCount);
        isler[1].Bitis.SetResult();
        Bekle(() => d.BaslayanAdlar().Length == 3, "c başlasın");
        Assert.Equal(0, Volatile.Read(ref d.Bosaldi));
        isler[2].Bitis.SetResult();
        Bekle(() => Volatile.Read(ref d.Bosaldi) == 1, "kuyruk sonu");

        Assert.Equal(new[] { "a", "b", "c" }, d.BaslayanAdlar());
        Assert.Equal(1, d.EnCokKosan);
    }

    [Fact]
    public void SinirIkiykenIkiIsOrtusurUcuncuBekler()
    {
        var d = new Duzenek { Sinir = 2 };
        var isler = Isler("a", "b", "c");
        foreach (var is_ in isler) d.Pompa.Enqueue(is_);

        Assert.Equal(new[] { "a", "b" }, d.BaslayanAdlar());
        Assert.Equal(2, d.Pompa.RunningCount);
        Assert.Equal(new[] { "c" }, d.Pompa.Pending.Select(i => i.Ad).ToArray());

        isler[1].Bitis.SetResult();
        Bekle(() => d.BaslayanAdlar().Length == 3, "c başlasın");
        Assert.Equal(new[] { "a", "c" }, d.Pompa.Running.Select(i => i.Ad).ToArray());

        isler[0].Bitis.SetResult();
        isler[2].Bitis.SetResult();
        Bekle(() => Volatile.Read(ref d.Bosaldi) == 1, "kuyruk sonu");
        Assert.Equal(2, d.EnCokKosan);
    }

    [Fact]
    public void BiriDusunceOtekiBiterVeKuyrukSonuBirKezGelir()
    {
        var d = new Duzenek { Sinir = 2 };
        var isler = Isler("a", "b", "c");
        foreach (var is_ in isler) d.Pompa.Enqueue(is_);

        isler[0].Bitis.SetException(new InvalidOperationException("bozuk"));
        Bekle(() => d.BaslayanAdlar().Length == 3, "c başlasın");
        Assert.Equal(new[] { "b", "c" }, d.Pompa.Running.Select(i => i.Ad).ToArray());
        Assert.False(isler[1].IptalGordu);
        Assert.Equal(0, Volatile.Read(ref d.Bosaldi));

        isler[1].Bitis.SetResult();
        isler[2].Bitis.SetResult();
        Bekle(() => Volatile.Read(ref d.Bosaldi) == 1, "kuyruk sonu");
        Thread.Sleep(50);

        Assert.Equal(1, Volatile.Read(ref d.Bosaldi));
        Assert.Equal(new[] { "a:bozuk" }, d.Hatalar.ToArray());
    }

    [Fact]
    public void TekIsinIptaliOtekineDokunmaz()
    {
        var d = new Duzenek { Sinir = 2 };
        var isler = Isler("a", "b", "c");
        foreach (var is_ in isler) d.Pompa.Enqueue(is_);

        Assert.False(d.Pompa.Cancel(isler[2]));
        Assert.True(d.Pompa.Cancel(isler[0]));
        Bekle(() => d.BaslayanAdlar().Length == 3, "c başlasın");

        Assert.True(isler[0].IptalGordu);
        Assert.False(isler[1].IptalGordu);
        Assert.False(isler[2].IptalGordu);
        Assert.Empty(d.Hatalar);
        Assert.Equal(new[] { "b", "c" }, d.Pompa.Running.Select(i => i.Ad).ToArray());

        d.Pompa.CancelAll();
        Bekle(() => d.Pompa.RunningCount == 0, "hepsi dursun");
    }

    [Fact]
    public void HepsiniIptalKosanlariDurdururBekleyenleriSiler()
    {
        var d = new Duzenek { Sinir = 2 };
        var isler = Isler("a", "b", "c", "d");
        foreach (var is_ in isler) d.Pompa.Enqueue(is_);

        d.Pompa.CancelAll();
        Bekle(() => d.Pompa.RunningCount == 0, "hepsi dursun");
        Bekle(() => Volatile.Read(ref d.Bosaldi) == 1, "kuyruk sonu");

        Assert.True(isler[0].IptalGordu);
        Assert.True(isler[1].IptalGordu);
        Assert.Equal(new[] { "a", "b" }, d.BaslayanAdlar());
        Assert.Equal(0, d.Pompa.PendingCount);
        Assert.Empty(d.Hatalar);
    }

    [Fact]
    public void DuraklatilanSiraYeniIsBaslatmazSurunceSiniraKadarAlir()
    {
        var d = new Duzenek { Sinir = 2 };
        var isler = Isler("a", "b", "c", "d");
        foreach (var is_ in isler) d.Pompa.Enqueue(is_);

        d.Pompa.Paused = true;
        isler[0].Bitis.SetResult();
        isler[1].Bitis.SetResult();
        Bekle(() => d.Pompa.RunningCount == 0, "koşanlar bitsin");
        Thread.Sleep(50);

        Assert.Equal(new[] { "a", "b" }, d.BaslayanAdlar());
        Assert.Equal(2, d.Pompa.PendingCount);
        Assert.Equal(0, Volatile.Read(ref d.Bosaldi));

        d.Pompa.Paused = false;
        Assert.Equal(new[] { "a", "b", "c", "d" }, d.BaslayanAdlar());
        Assert.Equal(2, d.Pompa.RunningCount);

        isler[2].Bitis.SetResult();
        isler[3].Bitis.SetResult();
        Bekle(() => Volatile.Read(ref d.Bosaldi) == 1, "kuyruk sonu");
    }

    [Fact]
    public void SinirKosarkenDegisirseSonrakiBaslatmadaOkunur()
    {
        var d = new Duzenek { Sinir = 1 };
        var isler = Isler("a", "b", "c");
        foreach (var is_ in isler) d.Pompa.Enqueue(is_);
        Assert.Equal(1, d.Pompa.RunningCount);

        d.Sinir = 3;
        d.Pompa.Pump();
        Assert.Equal(3, d.Pompa.RunningCount);

        d.Pompa.CancelAll();
        Bekle(() => d.Pompa.RunningCount == 0, "hepsi dursun");
    }

    [Fact]
    public async Task DonanimKapisiIkiYuvaVerirUcuncuBeklerYazilimBeklemez()
    {
        var kapi = new EncoderSlots();
        var bir = await kapi.EnterAsync("h264_nvenc");
        var iki = await kapi.EnterAsync("hevc_nvenc");

        var uc = kapi.EnterAsync("av1_nvenc");
        var yazilim = kapi.EnterAsync("libx264");
        Assert.False(uc.IsCompleted);
        Assert.True(yazilim.IsCompletedSuccessfully);

        bir.Dispose();
        bir.Dispose();
        var ucuncu = await uc.WaitAsync(TimeSpan.FromSeconds(10));

        using var iptal = new CancellationTokenSource();
        var dort = kapi.EnterAsync("h264_qsv", iptal.Token);
        Assert.False(dort.IsCompleted);
        iptal.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dort);

        iki.Dispose();
        ucuncu.Dispose();
        (await yazilim).Dispose();

        var bes = kapi.EnterAsync("h264_amf");
        var alti = kapi.EnterAsync("hevc_amf");
        var yedi = kapi.EnterAsync("av1_amf");
        Assert.True(bes.IsCompletedSuccessfully);
        Assert.True(alti.IsCompletedSuccessfully);
        Assert.False(yedi.IsCompleted);
        (await bes).Dispose();
        (await yedi.WaitAsync(TimeSpan.FromSeconds(10))).Dispose();
        (await alti).Dispose();
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(4, 2)]
    [InlineData(8, 4)]
    [InlineData(64, 4)]
    public void AyarListesiHerMakinedeTavanKadarSecenekTasirFazlasiPasif(int cekirdek, int acik)
    {
        var (toplam, etkin, ilkler) = AppHost.Run(() =>
        {
            var secenekler = MainWindow.ParallelJobChoices(cekirdek);
            return (secenekler.Length, secenekler.Count(s => s.IsEnabled),
                secenekler.TakeWhile(s => s.IsEnabled).Count());
        });

        Assert.Equal(ParallelJobs.Ceiling, toplam);
        Assert.Equal(acik, etkin);
        Assert.Equal(acik, ilkler);
    }

    [Fact]
    public void AyniAdaIkiIsDusmez()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "eszamanli-kuyruk", "ad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = Path.Combine(klasor, "klip.mp4");
            var defter = new OutputReservations();

            var ilk = defter.Reserve(alinmis => ShrinkEngine.UniqueOutputPath(kaynak, taken: alinmis));
            var ikinci = defter.Reserve(alinmis => ShrinkEngine.UniqueOutputPath(kaynak, taken: alinmis));
            var defterSiz = ShrinkEngine.UniqueOutputPath(kaynak);

            Assert.Equal(Path.Combine(klasor, "klip_shrunk.mp4"), ilk);
            Assert.Equal(Path.Combine(klasor, "klip_shrunk_2.mp4"), ikinci);
            Assert.Equal(ilk, defterSiz);
            Assert.True(defter.IsHeld(ilk.ToUpperInvariant()));

            defter.Release(ilk);
            Assert.False(defter.IsHeld(ilk));
            Assert.Equal(ilk, defter.Reserve(alinmis => ShrinkEngine.UniqueOutputPath(kaynak, taken: alinmis)));
        }
        finally { Directory.Delete(klasor, true); }
    }

    [Fact]
    public async Task AyirmaYarisindaHerIsAyriAdAlir()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "eszamanli-kuyruk", "yaris-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var kaynak = Path.Combine(klasor, "klip.mp4");
            var defter = new OutputReservations();
            var adlar = new ConcurrentBag<string>();
            using var kapi = new ManualResetEventSlim();
            var isler = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
            {
                kapi.Wait();
                for (var i = 0; i < 20; i++)
                    adlar.Add(defter.Reserve(alinmis => ShrinkEngine.UniqueOutputPath(kaynak, taken: alinmis)));
            })).ToArray();
            kapi.Set();
            await Task.WhenAll(isler).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(40, adlar.Count);
            Assert.Equal(40, adlar.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
        finally { Directory.Delete(klasor, true); }
    }

    [Fact]
    public void AyarGidisDonusVeEskiDosyaBir()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "eszamanli-kuyruk", "ayar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        try
        {
            var uc = Path.Combine(klasor, "uc.json");
            var eski = Path.Combine(klasor, "eski.json");
            var bozuk = Path.Combine(klasor, "bozuk.json");
            new AppSettings { ParallelJobs = 3 }.Save(uc);
            File.WriteAllText(eski, "{\"followRecording\": true}");
            File.WriteAllText(bozuk, "{\"parallelJobs\": -4}");

            Assert.Equal(3, AppSettings.Load(uc).ParallelJobs);
            Assert.Contains("\"parallelJobs\": 3", File.ReadAllText(uc), StringComparison.Ordinal);
            Assert.Equal(1, AppSettings.Load(eski).ParallelJobs);
            Assert.Equal(1, AppSettings.Load(bozuk).ParallelJobs);
            Assert.Equal(1, new AppSettings().ParallelJobs);
        }
        finally { Directory.Delete(klasor, true); }
    }

    private sealed class SahteEylem : IQueueEndActions
    {
        public List<string> Cagrilar { get; } = new();
        public void Reveal(string path) => Cagrilar.Add("reveal:" + path);
        public void Sleep() => Cagrilar.Add("sleep");
        public void PowerOff() => Cagrilar.Add("power-off");
    }

    private sealed class SahteKosum
    {
        private readonly Dictionary<string, TaskCompletionSource> _bitis = new();
        public List<string> Baslayan { get; } = new();
        public List<string> Iptal { get; } = new();

        public Task Kostur(ShrinkRequest istek, CancellationToken ct)
        {
            var bitis = new TaskCompletionSource();
            _bitis[istek.Path] = bitis;
            Baslayan.Add(istek.Path);
            ct.Register(() =>
            {
                Iptal.Add(istek.Path);
                bitis.TrySetCanceled(ct);
            });
            return bitis.Task;
        }

        public void Bitir(string yol)
        {
            _bitis[yol].TrySetResult();
            Dispatcher.UIThread.RunJobs();
        }

        public void Dusur(string yol)
        {
            _bitis[yol].TrySetException(new InvalidOperationException("bozuk"));
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static ShrinkJobWindow Pencere(SahteEylem eylem, SahteKosum kosum, int sinir, params string[] yollar)
        => new(yollar, new PlanOptions { TargetMb = 25 }, false, null)
        {
            Actions = eylem,
            Kosturucu = kosum.Kostur,
            ParallelLimit = sinir
        };

    private static string[] Adlar(IEnumerable<ShrinkRequest> istekler) => istekler.Select(r => r.Path).ToArray();

    /// <summary>Sınır 1 bugünkü davranış: pencerede aynı anda tek iş, sıra kabul sırası.</summary>
    [Fact]
    public void PenceredeSinirBirkenTekIsKosarSiraKorunur()
    {
        var kosum = new SahteKosum();
        var (ilk, sonra) = AppHost.Run(() =>
        {
            var window = Pencere(new SahteEylem(), kosum, 1, "a.mp4", "b.mp4", "c.mp4");
            try
            {
                window.Begin();
                var basta = (Adlar(window.RunningJobs), Adlar(window.Pending));
                kosum.Bitir("a.mp4");
                var ardindan = (Adlar(window.RunningJobs), Adlar(window.Pending));
                return (basta, ardindan);
            }
            finally { window.Close(); }
        });

        Assert.Equal(new[] { "a.mp4" }, ilk.Item1);
        Assert.Equal(new[] { "b.mp4", "c.mp4" }, ilk.Item2);
        Assert.Equal(new[] { "b.mp4" }, sonra.Item1);
        Assert.Equal(new[] { "c.mp4" }, sonra.Item2);
    }

    /// <summary>
    /// Sınır 2: iki iş birlikte koşar. Kapat seçiliyken geri sayım ilk iş bitince değil, hepsi
    /// bitince başlar ve eylem bir kez çalışır; düşen iş ötekileri durdurmaz.
    /// </summary>
    [Fact]
    public void PenceredeKuyrukSonuEylemiButunIslerBitinceBirKez()
    {
        var eylem = new SahteEylem();
        var kosum = new SahteKosum();
        var sonuc = AppHost.Run(() =>
        {
            var window = Pencere(eylem, kosum, 2, "a.mp4", "b.mp4", "c.mp4");
            try
            {
                window.WhenDone = QueueEndChoice.PowerOff;
                window.Begin();
                var birlikte = Adlar(window.RunningJobs);
                var bekleyen = Adlar(window.Pending);

                kosum.Dusur("a.mp4");
                var dusunce = Adlar(window.RunningJobs);
                var sayimDusunce = window.CountdownLeft;

                kosum.Bitir("b.mp4");
                var sayimBirKalmisken = window.CountdownLeft;

                kosum.Bitir("c.mp4");
                var sayimSonda = window.CountdownLeft;
                for (var i = 0; i < ShrinkJobWindow.CountdownSeconds + 5; i++) window.CountdownTick();
                return (birlikte, bekleyen, dusunce, sayimDusunce, sayimBirKalmisken, sayimSonda, iptal: kosum.Iptal.ToArray());
            }
            finally { window.Close(); }
        });

        Assert.Equal(new[] { "a.mp4", "b.mp4" }, sonuc.birlikte);
        Assert.Equal(new[] { "c.mp4" }, sonuc.bekleyen);
        Assert.Equal(new[] { "b.mp4", "c.mp4" }, sonuc.dusunce);
        Assert.Null(sonuc.sayimDusunce);
        Assert.Null(sonuc.sayimBirKalmisken);
        Assert.Equal(ShrinkJobWindow.CountdownSeconds, sonuc.sayimSonda);
        Assert.Empty(sonuc.iptal);
        Assert.Equal(new[] { "power-off" }, eylem.Cagrilar.ToArray());
    }

    /// <summary>Tek işin iptali yalnız onu durdurur; pencere kapanınca kalanlar durur ve eylem çalışmaz.</summary>
    [Fact]
    public void PenceredeTekIptalVeKapanisTumunuDurdururEylemCalismaz()
    {
        var eylem = new SahteEylem();
        var kosum = new SahteKosum();
        string[] tekIptal = Array.Empty<string>();
        string[] kosanlar = Array.Empty<string>();
        AppHost.Run(() =>
        {
            var window = Pencere(eylem, kosum, 2, "a.mp4", "b.mp4", "c.mp4", "d.mp4");
            try
            {
                window.WhenDone = QueueEndChoice.PowerOff;
                window.Begin();
                Assert.True(window.CancelJob(window.RunningJobs[0]));
                Dispatcher.UIThread.RunJobs();
                tekIptal = kosum.Iptal.ToArray();
                kosanlar = Adlar(window.RunningJobs);
            }
            finally { window.Close(); }
            Dispatcher.UIThread.RunJobs();
            for (var i = 0; i < ShrinkJobWindow.CountdownSeconds + 5; i++) window.CountdownTick();
        });

        Assert.Equal(new[] { "a.mp4" }, tekIptal);
        Assert.Equal(new[] { "b.mp4", "c.mp4" }, kosanlar);
        Assert.Equal(new[] { "a.mp4", "b.mp4", "c.mp4" }, kosum.Iptal.ToArray());
        Assert.Equal(new[] { "a.mp4", "b.mp4", "c.mp4" }, kosum.Baslayan.ToArray());
        Assert.Empty(eylem.Cagrilar);
    }

    /// <summary>Duraklatılan pencere koşanları bitirir, sıradakini başlatmaz ve kuyruk sonu eylemine geçmez.</summary>
    [Fact]
    public void PenceredeDuraklatilanSiraSurunceIkiIsBirdenAlir()
    {
        var eylem = new SahteEylem();
        var kosum = new SahteKosum();
        var sonuc = AppHost.Run(() =>
        {
            var window = Pencere(eylem, kosum, 2, "a.mp4", "b.mp4", "c.mp4", "d.mp4");
            try
            {
                window.WhenDone = QueueEndChoice.PowerOff;
                window.Begin();
                window.SetPaused(true);
                kosum.Bitir("a.mp4");
                kosum.Bitir("b.mp4");
                var durakta = (Adlar(window.RunningJobs), Adlar(window.Pending), window.CountdownLeft);
                window.SetPaused(false);
                var surunce = Adlar(window.RunningJobs);
                return (durakta, surunce);
            }
            finally { window.Close(); }
        });

        Assert.Empty(sonuc.durakta.Item1);
        Assert.Equal(new[] { "c.mp4", "d.mp4" }, sonuc.durakta.Item2);
        Assert.Null(sonuc.durakta.Item3);
        Assert.Equal(new[] { "c.mp4", "d.mp4" }, sonuc.surunce);
        Assert.Empty(eylem.Cagrilar);
    }

    /// <summary>Üç anahtar 42 dilde; koşan iş başlığı sayıyı taşır.</summary>
    [Fact]
    public void AnahtarlarButunDillerde()
    {
        var anahtarlar = new[] { "main.shrink-job.running", "settings-tab.parallel-jobs.label", "settings-tab.parallel-jobs.hint" };
        Assert.Equal(42, Locales.Languages.Count);
        foreach (var language in Locales.Languages)
        {
            var values = Locales.Values(language);
            foreach (var key in anahtarlar)
                Assert.True(values.TryGetValue(key, out var metin) && metin.Length > 0, $"{language}: {key}");
            Assert.Contains("{0}", values["main.shrink-job.running"], StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Gerçek ffmpeg: iki kısa klip sınır 2 ile aynı anda iki geçişli kodlanır. Her koşum kendi
    /// geçiş günlüğü önekini alır (geçici klasörde iki ayrı <c>vidshrink_&lt;guid&gt;</c>), çıktılar
    /// ayrı dosyalara iner ve ikisi de okunur. En çok iki süreç, her biri <c>-threads 2</c>.
    /// </summary>
    [FfmpegFact]
    public async Task IkiGercekKodlamaAyriGecisGunlugundeOrtusur()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "eszamanli-kuyruk", "canli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        var onekler = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        using var izleyici = new FileSystemWatcher(Path.GetTempPath(), "vidshrink_*") { IncludeSubdirectories = false };
        izleyici.Created += (_, olay) =>
        {
            var ad = olay.Name ?? "";
            if (ad.Length >= 42 && ad.Contains(".log", StringComparison.Ordinal)) onekler.TryAdd(ad[..42], 0);
        };
        try
        {
            var kaynaklar = new[] { await KaynakYapAsync(klasor, "bir"), await KaynakYapAsync(klasor, "iki") };
            var defter = new OutputReservations();
            var ciktilar = new ConcurrentDictionary<string, EncodeResult>();
            var ikisiDeBasladi = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var kosan = 0;
            var enCok = 0;
            var bosaldi = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var pompa = new JobPump<string>(async (kaynak, ct) =>
            {
                var cikti = defter.Reserve(alinmis => ShrinkEngine.UniqueOutputPath(
                    kaynak, outputDirectory: klasor, baseName: "cikti", taken: alinmis));
                var simdi = Interlocked.Increment(ref kosan);
                InterlockedMax(ref enCok, simdi);
                if (simdi == 2) ikisiDeBasladi.TrySetResult();
                try
                {
                    await ikisiDeBasladi.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
                    ciktilar[kaynak] = await new EncodeRunner().RunAsync(Bilgi(kaynak), KucukPlan(), cikti, targetMb: 1.0, progress: null, ct);
                }
                finally
                {
                    Interlocked.Decrement(ref kosan);
                    defter.Release(cikti);
                }
            }, () => 2);
            var hatalar = new ConcurrentBag<string>();
            pompa.Failed += (_, ex) => hatalar.Add(ex.ToString());
            pompa.Drained += () => bosaldi.TrySetResult();

            izleyici.EnableRaisingEvents = true;
            foreach (var kaynak in kaynaklar) pompa.Enqueue(kaynak);
            await bosaldi.Task.WaitAsync(TimeSpan.FromMinutes(2));
            await Task.Delay(300);
            izleyici.EnableRaisingEvents = false;

            Assert.Empty(hatalar);
            Assert.Equal(2, enCok);
            Assert.Equal(2, ciktilar.Count);
            var yollar = ciktilar.Values.Select(sonuc => sonuc.OutputPath).ToArray();
            Assert.All(ciktilar.Values, sonuc => Assert.True(sonuc.Success, sonuc.Error));
            Assert.Equal(2, yollar.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(
                new[] { Path.Combine(klasor, "cikti.mp4"), Path.Combine(klasor, "cikti_2.mp4") },
                yollar.OrderBy(yol => yol.Length).ToArray());
            foreach (var yol in yollar)
            {
                var okunan = await FfprobeClient.ProbeAsync(yol);
                Assert.Equal(320, okunan.Width);
                Assert.InRange(okunan.DurationSeconds, 1.8, 2.2);
            }
            Assert.True(onekler.Count >= 2, "geçiş günlüğü önekleri: " + string.Join(", ", onekler.Keys));
            foreach (var onek in onekler.Keys)
                Assert.Empty(Directory.GetFiles(Path.GetTempPath(), onek + "*"));
            Assert.Empty(Directory.GetFiles(klasor, "vidshrink_partial_*"));
        }
        finally
        {
            izleyici.EnableRaisingEvents = false;
            Directory.Delete(klasor, true);
        }
    }

    private static void InterlockedMax(ref int hedef, int deger)
    {
        int eski;
        do { eski = Volatile.Read(ref hedef); }
        while (deger > eski && Interlocked.CompareExchange(ref hedef, deger, eski) != eski);
    }

    private static async Task<string> KaynakYapAsync(string klasor, string ad)
    {
        var kaynak = Path.Combine(klasor, ad + ".mp4");
        var psi = new ProcessStartInfo
        {
            FileName = ToolLocator.Ffmpeg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
        {
            "-y", "-threads", "2", "-f", "lavfi", "-i", "testsrc=size=320x240:rate=10:duration=2",
            "-c:v", "libx264", "-threads", "2", "-crf", "18", "-g", "10", "-pix_fmt", "yuv420p", kaynak
        }) psi.ArgumentList.Add(arg);

        using var surec = new Process { StartInfo = psi };
        surec.Start();
        var bosalt = Task.WhenAll(surec.StandardOutput.ReadToEndAsync(), surec.StandardError.ReadToEndAsync());
        await surec.WaitForExitAsync();
        await bosalt;
        Assert.True(File.Exists(kaynak));
        return kaynak;
    }

    private static MediaInfo Bilgi(string kaynak) => new()
    {
        FilePath = kaynak,
        FileSizeBytes = new FileInfo(kaynak).Length,
        DurationSeconds = 2,
        Width = 320,
        Height = 240,
        Fps = 10,
        VideoCodec = "h264",
        TotalBitrateBps = 400_000
    };

    private static EncodePlan KucukPlan() => new()
    {
        Codec = "libx264",
        Mode = "2pass",
        VideoBitrateK = 120,
        AudioCodec = null,
        AudioBitrateK = 0,
        Width = 320,
        Height = 240,
        Fps = 10,
        Preset = "ultrafast",
        ExtraArgs = new List<string> { "-threads", "2" }
    };
}
