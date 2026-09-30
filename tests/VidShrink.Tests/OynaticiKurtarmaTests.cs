using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using VidShrink.App;
using VidShrink.App.Localization;
using VidShrink.App.Playback;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

public sealed class OynaticiKurtarmaTests
{
    private static string Dosya()
    {
        var klasor = Path.Combine(TestPaths.OutputRoot, "oynatici-kurtarma", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(klasor);
        var yol = Path.Combine(klasor, "klip.mp4");
        File.WriteAllBytes(yol, new byte[16]);
        return yol;
    }

    private static PlayerView Ac(KurtarmaMotoru motor, out Window pencere)
    {
        var view = new PlayerView { EngineFactory = () => motor };
        pencere = new Window { Width = 640, Height = 360, Content = view };
        pencere.Show();
        var acilis = view.OpenAsync(Dosya());
        DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
        acilis.GetAwaiter().GetResult();
        DenetimSurucu.Wait(view, 0.2);
        motor.Cagrilar.Clear();
        return view;
    }

    private static double Simdi() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    private static void Kapat(PlayerView view, Window pencere)
    {
        view.Close();
        pencere.Close();
    }

    [Fact]
    public void PollStallAtlamaHatasiYazisiniSilmez()
    {
        var sonuc = AppHost.Run(() =>
        {
            var motor = new KurtarmaMotoru { Arama = SeekOutcome.Failed, SesYenilenir = false, Acilir = false };
            var view = Ac(motor, out var pencere);
            view.Apply(new PlayerCommand(PlayerCommandKind.TogglePlay, 0));
            DenetimSurucu.Pump(view, () => motor.IsPaused, 3);
            view.Apply(new PlayerCommand(PlayerCommandKind.Seek, 5));
            DenetimSurucu.Pump(view, () => view.Seek.Idle.IsCompleted, 3);

            var t = Simdi();
            var gorunur = new List<bool>();
            for (var i = 0; i < 8; i++)
            {
                view.PollStall(t + i);
                gorunur.Add(view.UyariGorunur);
            }

            var metin = view.UyariMetni;
            var duraklatik = motor.IsPaused;
            motor.Kare++;
            view.PollStall(t + 9);
            var kareSonrasi = view.UyariGorunur;
            var beklenen = Strings.Get("main.player.seekfailed");
            Kapat(view, pencere);
            return (gorunur, metin, duraklatik, kareSonrasi, beklenen);
        });

        Assert.True(sonuc.duraklatik);
        Assert.All(sonuc.gorunur, Assert.True);
        Assert.Equal(sonuc.beklenen, sonuc.metin);
        Assert.False(sonuc.kareSonrasi);
    }

    [Fact]
    public void TakilmadaOnceSesSonraYenidenAcmaDenenirVeBasariUyariBirakmaz()
    {
        var sonuc = AppHost.Run(() =>
        {
            var motor = new KurtarmaMotoru { SesYenilenir = true, Acilir = true, AcilincaKareGelir = true };
            var view = Ac(motor, out var pencere);
            var t = Simdi();
            view.PollStall(t);
            view.PollStall(t + 2.5);
            var sesSonrasi = motor.Cagrilar.ToArray();
            view.PollStall(t + 3.5);
            var beklerken = motor.Cagrilar.ToArray();
            view.PollStall(t + 5);
            var acilisSonrasi = motor.Cagrilar.ToArray();
            view.PollStall(t + 5.5);
            var cikti = (sesSonrasi, beklerken, acilisSonrasi, view.UyariMetni, view.UyariGorunur, view.Trace, view.Kurtarici.Asama);
            Kapat(view, pencere);
            return cikti;
        });

        Assert.Equal(new[] { "ao-reload" }, sonuc.sesSonrasi);
        Assert.Equal(new[] { "ao-reload" }, sonuc.beklerken);
        Assert.Equal(new[] { "ao-reload", "reopen oynuyor=True" }, sonuc.acilisSonrasi);
        Assert.Null(sonuc.UyariMetni);
        Assert.False(sonuc.UyariGorunur);
        Assert.Equal(KurtarmaAsamasi.Bosta, sonuc.Asama);
        Assert.Contains("kurtarma -> tamam", sonuc.Trace);
    }

    [Fact]
    public void KurtarmaDuserseUyariKaliciKalirVeKullaniciIslemiKaldirir()
    {
        var sonuc = AppHost.Run(() =>
        {
            var motor = new KurtarmaMotoru { SesYenilenir = true, Acilir = true, AcilincaKareGelir = false };
            var view = Ac(motor, out var pencere);
            var t = Simdi();
            view.PollStall(t);
            view.PollStall(t + 2.5);
            view.PollStall(t + 5);
            var dusmedenOnce = view.UyariGorunur;
            view.PollStall(t + 7.5);
            var metin = view.UyariMetni;
            var kalici = new List<bool>();
            for (var i = 8; i < 20; i++)
            {
                view.PollStall(t + i);
                kalici.Add(view.UyariGorunur);
            }

            var cagrilar = motor.Cagrilar.ToArray();
            view.Apply(new PlayerCommand(PlayerCommandKind.ResetZoom, 0));
            var islemSonrasi = (view.UyariMetni, view.UyariGorunur);
            var beklenen = Strings.Get("main.player.recoveryfailed");
            Kapat(view, pencere);
            return (dusmedenOnce, metin, kalici, cagrilar, islemSonrasi, beklenen);
        });

        Assert.False(sonuc.dusmedenOnce);
        Assert.Equal(sonuc.beklenen, sonuc.metin);
        Assert.All(sonuc.kalici, Assert.True);
        Assert.Equal(new[] { "ao-reload", "reopen oynuyor=True" }, sonuc.cagrilar);
        Assert.Null(sonuc.islemSonrasi.UyariMetni);
        Assert.False(sonuc.islemSonrasi.UyariGorunur);
    }

    [Fact]
    public void KurtaramayanMotordaUyariTakilmaNedeniniSoyler()
    {
        var sonuc = AppHost.Run(() =>
        {
            var motor = new KurtarmaMotoru { SesYenilenir = false, Acilir = false };
            var view = Ac(motor, out var pencere);
            var t = Simdi();
            view.PollStall(t);
            view.PollStall(t + 2.5);
            var cikti = (view.UyariMetni, motor.Cagrilar.ToArray(), Strings.Get("main.player.stalled"));
            Kapat(view, pencere);
            return cikti;
        });

        Assert.Equal(sonuc.Item3, sonuc.UyariMetni);
        Assert.Equal(new[] { "ao-reload", "reopen oynuyor=True" }, sonuc.Item2);
    }

    [Fact]
    public void TakilmaGunluguYalitilmisAyarKlasorunePathsizYazilir()
    {
        var ayar = Environment.GetEnvironmentVariable("VIDSHRINK_SETTINGS_PATH");
        Assert.False(string.IsNullOrWhiteSpace(ayar));
        var beklenenKlasor = Path.GetDirectoryName(Path.GetFullPath(ayar!))!;
        var gercek = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VidShrink", TakilmaGunlugu.DosyaAdi);
        var gercekOnce = File.Exists(gercek) ? File.GetLastWriteTimeUtc(gercek) : (DateTime?)null;
        if (File.Exists(TakilmaGunlugu.Dosya)) File.Delete(TakilmaGunlugu.Dosya);

        var gizliKlasor = @"C:\Users\birisi\GizliKlasor";
        AppHost.Run(() =>
        {
            var motor = new KurtarmaMotoru { SesYenilenir = true, Acilir = true, AcilincaKareGelir = true };
            motor.Gunluk.Add("[ao/wasapi] warn: device lost " + gizliKlasor + @"\tatil.mp4");
            var view = Ac(motor, out var pencere);
            var t = Simdi();
            view.PollStall(t);
            view.PollStall(t + 2.5);
            Kapat(view, pencere);
            return 0;
        });

        Assert.Equal(beklenenKlasor, TakilmaGunlugu.Klasor, ignoreCase: true);
        Assert.True(File.Exists(TakilmaGunlugu.Dosya));
        var metin = File.ReadAllText(TakilmaGunlugu.Dosya);
        Assert.Contains("main.player.stalled", metin);
        Assert.Contains("device lost tatil.mp4", metin);
        Assert.DoesNotContain(gizliKlasor, metin);
        Assert.Equal(gercekOnce, File.Exists(gercek) ? File.GetLastWriteTimeUtc(gercek) : (DateTime?)null);
        File.Delete(TakilmaGunlugu.Dosya);
    }

    [Fact]
    public void TakilmaGunluguBoyutSiniriniAsmaz()
    {
        if (File.Exists(TakilmaGunlugu.Dosya)) File.Delete(TakilmaGunlugu.Dosya);
        var satirlar = Enumerable.Range(0, 64).Select(i => i + " " + new string('x', 1000)).ToArray();
        for (var i = 0; i < 10; i++)
            Assert.True(TakilmaGunlugu.Yaz("olay-" + i, satirlar, new DateTime(2026, 9, 30, 12, 0, i)));

        var bayt = new FileInfo(TakilmaGunlugu.Dosya).Length;
        var metin = File.ReadAllText(TakilmaGunlugu.Dosya);
        Assert.InRange(bayt, 1, TakilmaGunlugu.SinirBayt);
        Assert.StartsWith("=== ", metin);
        Assert.Contains("olay-9", metin);
        Assert.DoesNotContain("olay-0", metin);
        Assert.DoesNotContain(new string('x', TakilmaGunlugu.SatirSiniri + 1), metin);
        File.Delete(TakilmaGunlugu.Dosya);

        var tekBlok = "=== tek\n" + new string('y', TakilmaGunlugu.SinirBayt * 2);
        Assert.InRange(Encoding.UTF8.GetByteCount(TakilmaGunlugu.Kirp(tekBlok)), 1, TakilmaGunlugu.SinirBayt);
    }

    [Fact]
    public void KurtariciKurtarmaSirasiniIzler()
    {
        var k = new OynatmaKurtarici();
        Assert.True(k.Baslat(10, 0));
        Assert.False(k.Baslat(10, 0));
        Assert.Equal(KurtarmaAdimi.Yok, k.SesSonucu(true, 0));
        Assert.Equal(KurtarmaAdimi.Yok, k.Gozle(10, 1.9));
        Assert.Equal(KurtarmaAdimi.YenidenAc, k.Gozle(10, 2.0));
        Assert.Equal(KurtarmaAdimi.Yok, k.Gozle(10, 9));
        Assert.Equal(KurtarmaAdimi.Yok, k.Acildi(true, 9));
        Assert.Equal(KurtarmaAdimi.Basarisiz, k.Gozle(10, 11));
        Assert.True(k.Denendi);
        Assert.Equal(KurtarmaAdimi.Yok, k.Gozle(10, 50));

        k.Iptal();
        Assert.True(k.Baslat(3, 0));
        Assert.Equal(KurtarmaAdimi.YenidenAc, k.SesSonucu(false, 0));
        Assert.Equal(KurtarmaAdimi.Basarisiz, k.Acildi(false, 0));
        Assert.False(k.Denendi);

        k.Iptal();
        Assert.True(k.Baslat(3, 0));
        k.SesSonucu(true, 0);
        Assert.Equal(KurtarmaAdimi.Kurtuldu, k.Gozle(4, 0.5));
        Assert.Equal(KurtarmaAsamasi.Bosta, k.Asama);
    }
}

public sealed class OynaticiKurtarmaMotorTests
{
    [Fact]
    public async Task YenidenAcmaKonumuHiziSesiVeDuraklatmayiKorur()
    {
        using var engine = new MpvEngine();
        engine.SetProperty("ao", "null");
        await engine.OpenAsync(MotorKlipleri.Kucuk);
        engine.Play();
        engine.SetSpeed(1.5);
        engine.SetVolume(40);
        engine.SetMuted(true);
        await KareArtisiBekle(engine, TimeSpan.FromSeconds(5));

        var once = engine.FramesRendered;
        Assert.True(engine.ReloadAudio(), string.Join(" | ", engine.RecentLog));
        await KareArtisiBekle(engine, TimeSpan.FromSeconds(5), once);

        Assert.True(await engine.ReopenAsync(10, true), string.Join(" | ", engine.RecentLog));
        var acilis = engine.FramesRendered;
        await KareArtisiBekle(engine, TimeSpan.FromSeconds(5), acilis + 5);
        Assert.InRange(engine.PositionSeconds, 10, 15);
        Assert.False(engine.IsPaused);
        Assert.Equal(1.5, engine.Speed, 3);
        Assert.Equal(40, engine.Volume, 3);
        Assert.True(engine.Muted);

        Assert.True(await engine.ReopenAsync(3, false), string.Join(" | ", engine.RecentLog));
        await Task.Delay(500);
        var duraklatik = engine.FramesRendered;
        await Task.Delay(500);
        Assert.True(engine.IsPaused);
        Assert.Equal(3, engine.PositionSeconds, 1);
        Assert.Equal(duraklatik, engine.FramesRendered);
    }

    private static async Task KareArtisiBekle(MpvEngine engine, TimeSpan sinir, long? esik = null)
    {
        var hedef = esik ?? engine.FramesRendered;
        var saat = Stopwatch.StartNew();
        while (engine.FramesRendered <= hedef && saat.Elapsed < sinir) await Task.Delay(20);
        Assert.True(engine.FramesRendered > hedef, $"{sinir.TotalSeconds} sn icinde kare gelmedi; son log: {string.Join(" | ", engine.RecentLog)}");
    }
}

internal sealed class KurtarmaMotoru : IPlaybackEngine
{
    internal List<string> Cagrilar { get; } = new();

    internal List<string> Gunluk { get; } = new();

    internal SeekOutcome Arama { get; set; } = SeekOutcome.Shown;

    internal bool SesYenilenir { get; set; }

    internal bool Acilir { get; set; }

    internal bool AcilincaKareGelir { get; set; }

    internal long Kare { get; set; } = 1;

    public string Name => "kurtarma-motoru";

    public bool IsOpen { get; private set; }

    public double DurationSeconds => 60;

    public bool HasAudio => true;

    public bool IsPaused { get; private set; } = true;

    public bool EndReached => false;

    public double PositionSeconds { get; set; }

    public double AudioVideoOffsetSeconds => 0;

    public long FramesRendered => Kare;

    public IReadOnlyList<string> RecentLog => Gunluk;

    public event EventHandler<PlaybackFault>? Faulted { add { } remove { } }

    public Task OpenAsync(string path, CancellationToken ct = default)
    {
        IsOpen = true;
        return Task.CompletedTask;
    }

    public void Play() => IsPaused = false;

    public void Pause() => IsPaused = true;

    public Task<SeekResult> SeekAsync(double seconds, SeekPrecision precision, CancellationToken ct = default)
    {
        if (Arama == SeekOutcome.Shown) PositionSeconds = seconds;
        return Task.FromResult(new SeekResult(Arama, 1));
    }

    public bool TryCopyLatest(ref long seen, FrameCopy copy) => false;

    public bool ReloadAudio()
    {
        Cagrilar.Add("ao-reload");
        return SesYenilenir;
    }

    public Task<bool> ReopenAsync(double atSeconds, bool playing, CancellationToken ct = default)
    {
        Cagrilar.Add("reopen oynuyor=" + playing);
        if (Acilir && AcilincaKareGelir) Kare++;
        return Task.FromResult(Acilir);
    }

    public void Dispose() => IsOpen = false;
}
