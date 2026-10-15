using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VidShrink.App.Playback;
using VidShrink.Core;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// İki ses ve iki altyazı izi olan, seçilen izi ve gecikmeleri hem tutan hem çağrı olarak kaydeden motor.
/// </summary>
internal sealed class HatirlamaMotoru : IPlaybackEngine
{
    internal List<string> Cagrilar { get; } = new();
    public string Name => "hatirlama-motoru";
    public bool IsOpen { get; private set; }
    public double DurationSeconds => 600;
    public bool HasAudio => true;
    public bool IsPaused { get; private set; } = true;
    public bool EndReached => false;
    public double PositionSeconds { get; private set; }
    public double AudioVideoOffsetSeconds => 0;
    public long FramesRendered => 0;
    public event EventHandler<PlaybackFault>? Faulted { add { } remove { } }
    public IReadOnlyList<PlaybackTrack> Tracks { get; } = new[]
    {
        new PlaybackTrack(1, PlaybackTrackKind.Audio, null, null, false, true),
        new PlaybackTrack(2, PlaybackTrackKind.Audio, null, null, false, false),
        new PlaybackTrack(3, PlaybackTrackKind.Subtitle, null, null, false, false),
        new PlaybackTrack(4, PlaybackTrackKind.Subtitle, null, null, false, false),
    };

    public long AudioTrack { get; private set; } = 1;
    public long SubtitleTrack { get; private set; }
    public long SecondarySubtitleTrack { get; private set; }

    public Task OpenAsync(string path, CancellationToken ct = default)
    {
        IsOpen = true;
        return Task.CompletedTask;
    }

    public void Play() => IsPaused = false;
    public void Pause() => IsPaused = true;

    public void SetAudioTrack(long id)
    {
        AudioTrack = id;
        Cagrilar.Add("ses " + id);
    }

    public void SetSubtitleTrack(long id)
    {
        SubtitleTrack = id;
        Cagrilar.Add("altyazi " + id);
    }

    public void SetSecondarySubtitleTrack(long id)
    {
        SecondarySubtitleTrack = id;
        Cagrilar.Add("altyazi2 " + id);
    }

    public void SetSubtitleDelay(double seconds) => Cagrilar.Add("altgecikme " + Saat.Tani.Konum(seconds));
    public void SetAudioDelay(double seconds) => Cagrilar.Add("sesgecikme " + Saat.Tani.Konum(seconds));
    public void SetSpeed(double speed) => Cagrilar.Add("hiz " + Saat.Tani.Konum(speed));

    public Task<SeekResult> SeekAsync(double seconds, SeekPrecision precision, CancellationToken ct = default)
    {
        PositionSeconds = seconds;
        return Task.FromResult(new SeekResult(SeekOutcome.Shown, 1));
    }

    public bool TryCopyLatest(ref long seen, FrameCopy copy) => false;
    public void Dispose() => IsOpen = false;
}

/// <summary>
/// OYA-05: oynatıcı dosya başına son seçilen ses izini, altyazı izlerini, gecikmeleri ve hızı geçmiş kaydında
/// tutar ve aynı dosya açılınca geri yükler; "tracks" alanı olmayan eski kayıt okunmaya devam eder. Atlama
/// adımları ayar dosyasından okunur; ok tuşları ve şerit düğmeleri aynı değeri kullanır, varsayılan 1, 10, 60, 300 sn.
/// Zamana git kutusu "ss", "dd:ss", "sa:dd:ss" kabul eder. Görünüm testleri kendi geçici geçmiş ve ayar klasörünü kullanır.
/// </summary>
public sealed class OynaticiDosyaHatirlamaTests : IDisposable
{
    private readonly string _kok = Path.Combine(GirdiKanit.Root, ".calisma", "oynatici-dosya-hatirlama", Guid.NewGuid().ToString("N")[..8]);

    public OynaticiDosyaHatirlamaTests() => Directory.CreateDirectory(_kok);

    public void Dispose()
    {
        Keymap.Steps = SeekSteps.Default;
        OynaticiListeTests.Sil(_kok);
    }

    private string Film(string ad)
    {
        var yol = Path.Combine(_kok, ad);
        File.WriteAllBytes(yol, new byte[16]);
        return yol;
    }

    private string Gecmis => Path.Combine(_kok, "history.json");

    [Fact]
    public void IzHafizasiGidisDonusu()
    {
        var film = Film("a.mp4");
        var gecmis = new PlaybackHistory();
        var hafiza = new TrackMemory(2, 4, 3, 0.25, -0.1, 1.5);
        gecmis.Remember(film, 42, false, 600, hafiza);
        gecmis.Save(Gecmis);

        var geri = PlaybackHistory.Load(Gecmis);
        Assert.Equal(hafiza, geri.TracksFor(film, 600));
        Assert.Equal(42, geri.ResumeFor(film, 600), 3);
        Assert.Null(geri.TracksFor(film, 700));
        Assert.Null(geri.TracksFor(Film("b.mp4"), 600));
    }

    [Fact]
    public void IzAlaniOlmayanEskiKayitOkunur()
    {
        var film = Film("a.mp4");
        var gecmis = new PlaybackHistory();
        gecmis.Remember(film, 42, false, 600, new TrackMemory(2, 4, 0, 0, 0, 1));
        gecmis.Save(Gecmis);

        var kok = JsonNode.Parse(File.ReadAllText(Gecmis))!;
        var silinen = 0;
        foreach (var dugum in kok.DescendantsWithTracks())
        {
            dugum.Remove("tracks");
            silinen++;
        }

        Assert.Equal(1, silinen);
        File.WriteAllText(Gecmis, kok.ToJsonString());

        var geri = PlaybackHistory.Load(Gecmis);
        Assert.Equal(42, geri.ResumeFor(film, 600), 3);
        Assert.Null(geri.TracksFor(film, 600));
    }

    [Fact]
    public void AtlamaAdimlariAyarlardaSaklanir()
    {
        var dosya = Path.Combine(_kok, PlayerSettings.FileName);
        var ilk = PlayerSettings.Load(dosya);
        Assert.Equal(SeekSteps.Default, ilk.SeekSteps);
        Assert.Equal(new SeekSteps(1, 10, 60, 300), SeekSteps.Default);

        ilk.SeekSteps = ilk.SeekSteps.With(SeekStep.Small, 5).With(SeekStep.Large, 600);
        ilk.Save(dosya);
        Assert.Equal(new SeekSteps(1, 5, 60, 600), PlayerSettings.Load(dosya).SeekSteps);

        var kok = JsonNode.Parse(File.ReadAllText(dosya))!.AsObject();
        kok["seekSteps"]!["fine"] = -3;
        kok["seekSteps"]!["medium"] = 99999;
        File.WriteAllText(dosya, kok.ToJsonString());
        Assert.Equal(new SeekSteps(1, 5, 60, 600), PlayerSettings.Load(dosya).SeekSteps);

        kok.Remove("seekSteps");
        File.WriteAllText(dosya, kok.ToJsonString());
        Assert.Equal(SeekSteps.Default, PlayerSettings.Load(dosya).SeekSteps);
    }

    [Theory]
    [InlineData("90", 90)]
    [InlineData("1:30", 90)]
    [InlineData("01:02:03", 3723)]
    [InlineData("2:05.5", 125.5)]
    [InlineData("2:05,5", 125.5)]
    [InlineData(" 0:07 ", 7)]
    [InlineData("75:00", 4500)]
    public void ZamanaGitGecerliGiris(string metin, double saniye)
    {
        Assert.True(PlayerView.TryParseGoTo(metin, out var sonuc));
        Assert.Equal(saniye, sonuc, 3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("abc")]
    [InlineData("1:60")]
    [InlineData("1:2:3:4")]
    [InlineData("1::3")]
    [InlineData("-5")]
    [InlineData("1.5:00")]
    public void ZamanaGitBozukGirisReddedilir(string? metin)
        => Assert.False(PlayerView.TryParseGoTo(metin, out _));

    /// <summary>
    /// Ayar dosyasında küçük adım 5 sn iken sağ ok ve şeridin ileri düğmesi 5 sn atlar, ince adım (Shift+sağ)
    /// varsayılan 1 sn kalır. Menüden seçilen adım ayar dosyasına yazılır ve tuşa hemen geçer.
    /// </summary>
    [Fact]
    public void AyarliAdimlaAtlanir()
    {
        var film = Film("a.mp4");
        var ayar = new PlayerSettings { SeekSteps = SeekSteps.Default.With(SeekStep.Small, 5) };
        ayar.Save(Path.Combine(_kok, PlayerSettings.FileName));

        var iz = AppHost.Run(() =>
        {
            var (view, pencere) = Ac(new HatirlamaMotoru(), film);
            try
            {
                view.FeedKey(Key.Right);
                view.BtnSeritForward.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                view.FeedKey(Key.Right, KeyModifiers.Shift);
                view.SetSeekStep(SeekStep.Small, 15);
                view.FeedKey(Key.Left);
                return view.Trace;
            }
            finally
            {
                pencere.Close();
            }
        });

        var atlamalar = iz.Where(satir => satir.StartsWith("seek ", StringComparison.Ordinal)).Select(satir => satir.Split(" -> ")[0]).ToArray();
        Assert.Equal(new[] { "seek 5", "seek 5", "seek 300", "seek -15" }, atlamalar);
        Assert.Contains("seekstep Small -> 15", iz);
        Assert.Equal(15, PlayerSettings.Load(Path.Combine(_kok, PlayerSettings.FileName)).SeekSteps.Small);
    }

    /// <summary>Varsayılan ayarla sağ ok 10 sn atlar; olumsuz kontrol.</summary>
    [Fact]
    public void AyarYokkenVarsayilanAdim()
    {
        var film = Film("a.mp4");
        var iz = AppHost.Run(() =>
        {
            var (view, pencere) = Ac(new HatirlamaMotoru(), film);
            try
            {
                view.FeedKey(Key.Right);
                return view.Trace;
            }
            finally
            {
                pencere.Close();
            }
        });

        Assert.Contains(iz, satir => satir.StartsWith("seek 10 -> ", StringComparison.Ordinal));
    }

    /// <summary>
    /// Görünümde seçilen ses izi, altyazı izleri, gecikmeler ve hız kapanışta kayda girer; aynı dosya yeniden
    /// açılınca motora geri yazılır. Başka dosya kayıttan bir şey almaz; olumsuz kontrol.
    /// </summary>
    [Fact]
    public void IzlerGecikmelerVeHizGeriYuklenir()
    {
        var film = Film("a.mp4");
        var baska = Film("b.mp4");

        var rapor = AppHost.Run(() =>
        {
            var (ilk, ilkPencere) = Ac(new HatirlamaMotoru(), film);
            ilk.SelectAudio(2);
            ilk.SelectSubtitle(4);
            ilk.SelectSecondarySubtitle(3);
            ilk.Apply(new PlayerCommand(PlayerCommandKind.SubtitleDelay, 0.5));
            ilk.Apply(new PlayerCommand(PlayerCommandKind.AudioDelay, -0.2));
            ilk.Apply(new PlayerCommand(PlayerCommandKind.Speed, 0.5));
            ilk.Close();
            ilkPencere.Close();

            var ayar = PlayerSettings.Load(Path.Combine(_kok, PlayerSettings.FileName));
            ayar.SpeedA = 1;
            ayar.Save(Path.Combine(_kok, PlayerSettings.FileName));

            var ayniMotor = new HatirlamaMotoru();
            var (ayni, ayniPencere) = Ac(ayniMotor, film);
            var ayniIz = ayni.Trace;
            ayniPencere.Close();

            var baskaMotor = new HatirlamaMotoru();
            var (diger, digerPencere) = Ac(baskaMotor, baska);
            var digerIz = diger.Trace;
            digerPencere.Close();

            return (Ayni: ayniMotor.Cagrilar.ToList(), AyniIz: ayniIz, Baska: baskaMotor.Cagrilar.ToList(), BaskaIz: digerIz);
        });

        Assert.Equal(new TrackMemory(2, 4, 3, 0.5, -0.2, 1.5), PlaybackHistory.Load(Gecmis).TracksFor(film, 600));
        Assert.Contains("memory -> 2 4 3 0.5 -0.2 1.5", rapor.AyniIz);
        Assert.Equal(new[] { "ses 2", "altyazi 4", "altyazi2 3", "altgecikme 0.5", "sesgecikme -0.2", "hiz 1.5" },
            rapor.Ayni.SkipWhile(c => c != "ses 2").Take(6).ToArray());

        Assert.DoesNotContain(rapor.BaskaIz, satir => satir.StartsWith("memory", StringComparison.Ordinal));
        Assert.DoesNotContain("ses 2", rapor.Baska);
        Assert.DoesNotContain("altyazi 4", rapor.Baska);
        Assert.DoesNotContain("hiz 1.5", rapor.Baska);
    }

    /// <summary>
    /// Ctrl+G zamana git kutusunu açar; yazılan saat konuma uygulanır, bozuk giriş konumu değiştirmez.
    /// </summary>
    [Fact]
    public void ZamanaGitKonumuTasir()
    {
        var film = Film("a.mp4");
        var rapor = AppHost.Run(() =>
        {
            var motor = new HatirlamaMotoru();
            var (view, pencere) = Ac(motor, film);
            try
            {
                view.FeedKey(Key.G, KeyModifiers.Control);
                var acik = view.GoToVisible;
                view.SubmitGoTo("1:30");
                DenetimSurucu.Pump(view, () => Math.Abs(motor.PositionSeconds - 90) < 0.001, 10);
                var konum = motor.PositionSeconds;
                view.ShowGoTo();
                view.SubmitGoTo("abc");
                DenetimSurucu.Pump(view, () => false, 0.3);
                return (Acik: acik, Kapali: view.GoToVisible, Konum: konum, Son: motor.PositionSeconds, Iz: view.Trace);
            }
            finally
            {
                pencere.Close();
            }
        });

        Assert.True(rapor.Acik);
        Assert.False(rapor.Kapali);
        Assert.Contains("goto -> open", rapor.Iz);
        Assert.Contains("goto -> 90", rapor.Iz);
        Assert.Contains("goto -> invalid", rapor.Iz);
        Assert.Equal(90, rapor.Konum, 3);
        Assert.Equal(90, rapor.Son, 3);
    }

    private (PlayerView View, Window Pencere) Ac(HatirlamaMotoru motor, string film)
    {
        var gecmis = Gecmis;
        var view = new PlayerView { EngineFactory = () => motor, HistoryPath = () => gecmis };
        var pencere = new Window { Width = 640, Height = 360, Content = view };
        pencere.Show();
        var acilis = view.OpenAsync(film);
        DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
        acilis.GetAwaiter().GetResult();
        return (view, pencere);
    }
}

internal static class GecmisJsonUzantisi
{
    internal static IEnumerable<JsonObject> DescendantsWithTracks(this JsonNode dugum)
    {
        var bulunan = new List<JsonObject>();
        void Gez(JsonNode? n)
        {
            switch (n)
            {
                case JsonObject o:
                    if (o.ContainsKey("tracks")) bulunan.Add(o);
                    foreach (var (_, deger) in o) Gez(deger);
                    break;
                case JsonArray a:
                    foreach (var deger in a) Gez(deger);
                    break;
            }
        }

        Gez(dugum);
        return bulunan;
    }
}
