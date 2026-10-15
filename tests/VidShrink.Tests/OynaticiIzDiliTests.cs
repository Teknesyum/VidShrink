using System.Text.Json.Nodes;
using Avalonia.Controls;
using VidShrink.App.Playback;
using VidShrink.Player;
using Xunit;

namespace VidShrink.Tests;

/// <summary>
/// İngilizce sesi ve İngilizce zorunlu izi olan, açılışta İspanyolca sesi seçili gelen motor.
/// </summary>
internal sealed class IzDiliMotoru : IPlaybackEngine
{
    internal List<string> Cagrilar { get; } = new();

    public string Name => "iz-dili-motoru";

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
        new PlaybackTrack(1, PlaybackTrackKind.Audio, null, "spa", false, true),
        new PlaybackTrack(2, PlaybackTrackKind.Audio, null, "eng", false, false),
        new PlaybackTrack(3, PlaybackTrackKind.Subtitle, null, "eng", false, false),
        new PlaybackTrack(4, PlaybackTrackKind.Subtitle, null, "spa", false, false, true),
        new PlaybackTrack(5, PlaybackTrackKind.Subtitle, null, "eng", false, false, true),
    };

    public Task OpenAsync(string path, CancellationToken ct = default)
    {
        IsOpen = true;
        return Task.CompletedTask;
    }

    public void Play() => IsPaused = false;

    public void Pause() => IsPaused = true;

    public void SetAudioTrack(long id) => Cagrilar.Add("ses " + id);

    public void SetSubtitleTrack(long id) => Cagrilar.Add("altyazi " + id);

    public Task<SeekResult> SeekAsync(double seconds, SeekPrecision precision, CancellationToken ct = default)
    {
        PositionSeconds = seconds;
        return Task.FromResult(new SeekResult(SeekOutcome.Shown, 1));
    }

    public bool TryCopyLatest(ref long seen, FrameCopy copy) => false;

    public void Dispose() => IsOpen = false;
}

/// <summary>
/// OYA-P5: açılışta ses ve altyazı izi dil tercihine göre seçilir. Ses izi tercih edilen dilde
/// olan ilk iz; tam altyazı yalnız duyulan dil tercih edilen dil değilse açılır; tam altyazı
/// açılmadıysa zorunlu (forced) iz kendiliğinden açılır. Tercih kapalıyken yalnız zorunlu iz açılır.
/// </summary>
public sealed class OynaticiIzDiliTests
{
    private static PlaybackTrack Ses(long id, string? dil, bool secili = false) => new(id, PlaybackTrackKind.Audio, null, dil, false, secili);

    private static PlaybackTrack Altyazi(long id, string? dil, bool zorunlu = false, bool dis = false)
        => new(id, PlaybackTrackKind.Subtitle, null, dil, dis, false, zorunlu);

    [Fact]
    public void TercihEdilenDildekiSesSecilir()
    {
        var izler = new[] { Ses(1, "eng", true), Ses(2, "tur") };
        Assert.Equal(2, TrackLanguage.Pick(izler, "tr-TR", null).Audio);
        Assert.Equal(0, TrackLanguage.Pick(izler, "de", null).Audio);
        Assert.Equal(0, TrackLanguage.Pick(izler, null, null).Audio);
    }

    [Fact]
    public void ZatenSeciliSesIcinCagriYok()
        => Assert.Equal(0, TrackLanguage.Pick(new[] { Ses(1, "tur", true), Ses(2, "tur") }, "tr", null).Audio);

    [Fact]
    public void DuyulanDilFarklıysaTercihEdilenAltyaziAcilir()
    {
        var izler = new[] { Ses(1, "eng", true), Altyazi(3, "eng"), Altyazi(4, "tur") };
        Assert.Equal(4, TrackLanguage.Pick(izler, "tr", "tr").Subtitle);
    }

    [Fact]
    public void DuyulanDilTercihEdilenDilseTamAltyaziAcilmaz()
    {
        var izler = new[] { Ses(1, "eng"), Ses(2, "tur"), Altyazi(3, "tur") };
        var secim = TrackLanguage.Pick(izler, "tr", "tr");
        Assert.Equal(2, secim.Audio);
        Assert.Equal(0, secim.Subtitle);
    }

    [Fact]
    public void ZorunluIzKendiligindenAcilir()
    {
        var izler = new[] { Ses(1, "eng", true), Altyazi(3, "eng"), Altyazi(4, "eng", zorunlu: true), Altyazi(5, "fre", zorunlu: true) };
        Assert.Equal(4, TrackLanguage.Pick(izler, null, null).Subtitle);
        Assert.Equal(5, TrackLanguage.Pick(izler, null, "fr").Subtitle);
        Assert.Equal(0, TrackLanguage.Pick(new[] { Ses(1, "eng", true), Altyazi(3, "eng") }, null, null).Subtitle);
    }

    [Fact]
    public void DisAltyaziDilSecimineGirmez()
        => Assert.Equal(0, TrackLanguage.Pick(new[] { Ses(1, "eng", true), Altyazi(3, "tur", dis: true) }, "tr", "tr").Subtitle);

    /// <summary>
    /// Görünüm açılışta seçimi motora yazar (AppHost arayüzü İngilizce açar): İngilizce ses seçilir,
    /// duyulan dil İngilizce olduğu için tam altyazı değil İngilizce zorunlu iz açılır. Tercih kapalıyken
    /// ses değişmez ve zorunlu iz duyulan dilden (İspanyolca) seçilir; olumsuz kontrol.
    /// </summary>
    [Fact]
    public void AcilistaSecimMotoraYazilir()
    {
        var rapor = AppHost.Run(() =>
        {
            var kok = Path.Combine(GirdiKanit.Root, ".calisma", "oynatici-iz-dili", Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(kok);
            try
            {
                var film = Path.Combine(kok, "film.mp4");
                File.WriteAllBytes(film, new byte[16]);
                var acik = Ac(new IzDiliMotoru(), film, tercih: true);
                var kapali = Ac(new IzDiliMotoru(), film, tercih: false);
                return (Acik: acik, Kapali: kapali);
            }
            finally
            {
                OynaticiListeTests.Sil(kok);
            }
        });

        Assert.Equal(new[] { "ses 2", "altyazi 5" }, rapor.Acik.Cagrilar);
        Assert.Contains("audiolang -> 2", rapor.Acik.Iz);
        Assert.Contains("sublang -> 5", rapor.Acik.Iz);
        Assert.Equal(new[] { "altyazi 4" }, rapor.Kapali.Cagrilar);
        Assert.DoesNotContain(rapor.Kapali.Iz, satir => satir.StartsWith("audiolang", StringComparison.Ordinal));
    }

    private static (List<string> Cagrilar, IReadOnlyList<string> Iz) Ac(IzDiliMotoru motor, string film, bool tercih)
    {
        var view = new PlayerView { EngineFactory = () => motor };
        view.Settings.AudioInInterfaceLanguage = tercih;
        view.Settings.SubtitleInInterfaceLanguage = tercih;
        var pencere = new Window { Width = 640, Height = 360, Content = view };
        pencere.Show();
        var acilis = view.OpenAsync(film);
        DenetimSurucu.Pump(view, () => acilis.IsCompleted, 10);
        acilis.GetAwaiter().GetResult();
        var iz = view.Trace;
        pencere.Close();
        return (motor.Cagrilar.ToList(), iz);
    }

    [Fact]
    public void DilTercihiAyarlardaSaklanir()
    {
        var klasor = Path.Combine(GirdiKanit.Root, ".calisma", "oynatici-iz-dili", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(klasor);
        try
        {
            var dosya = Path.Combine(klasor, PlayerSettings.FileName);
            var ilk = PlayerSettings.Load(dosya);
            Assert.True(ilk.AudioInInterfaceLanguage);
            Assert.True(ilk.SubtitleInInterfaceLanguage);
            ilk.AudioInInterfaceLanguage = false;
            ilk.Save(dosya);
            var geri = PlayerSettings.Load(dosya);
            Assert.False(geri.AudioInInterfaceLanguage);
            Assert.True(geri.SubtitleInInterfaceLanguage);
            Assert.False((bool?)JsonNode.Parse(File.ReadAllText(dosya))!["audioInInterfaceLanguage"]);
        }
        finally
        {
            OynaticiListeTests.Sil(klasor);
        }
    }
}
