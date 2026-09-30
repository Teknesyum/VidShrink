using VidShrink.App.Localization;
using VidShrink.Core;
using VidShrink.Player;

namespace VidShrink.App.Playback;

internal partial class PlayerView
{
    private const string StalledKey = "main.player.stalled";
    private const string SeekFailedKey = "main.player.seekfailed";
    private const string RecoveryFailedKey = "main.player.recoveryfailed";

    private readonly OynatmaKurtarici _kurtarici = new();
    private (string Metin, long Taban)? _uyari;
    private string _kurtarmaNedeni = StalledKey;
    private int _kurtarmaSurum;
    private double _sonYoklama;

    internal OynatmaKurtarici Kurtarici => _kurtarici;

    internal string? UyariMetni => _uyari?.Metin;

    internal bool UyariGorunur => TxtStall.IsVisible;

    internal Task KurtarmaIsi { get; private set; } = Task.CompletedTask;

    internal void PollStall(double nowSeconds)
    {
        _sonYoklama = nowSeconds;
        var frames = _engine?.FramesRendered ?? 0;
        var stalled = _stall.Observe(_playing && _engine is { EndReached: false, HasVideo: true }, frames, nowSeconds);

        switch (_kurtarici.Gozle(frames, nowSeconds))
        {
            case KurtarmaAdimi.Kurtuldu:
                _trace.Add("kurtarma -> tamam");
                _uyari = null;
                break;
            case KurtarmaAdimi.YenidenAc:
                YenidenAc();
                break;
            case KurtarmaAdimi.Basarisiz:
                KurtarmaDustu();
                break;
        }

        if (_uyari is { } uyari && frames != uyari.Taban) _uyari = null;
        if (stalled && _uyari is null) TakilmaBasla(StalledKey);
        UyariyiGoster();
    }

    private void SeekFailed(IPlaybackEngine engine, SeekOutcome outcome)
    {
        if (!ReferenceEquals(engine, _engine)) return;
        _trace.Add("seek -> " + outcome);
        if (_uyari is null) TakilmaBasla(SeekFailedKey);
        UyariyiGoster();
    }

    private void TakilmaBasla(string neden)
    {
        if (_engine is not { } engine) return;
        var frames = engine.FramesRendered;
        if (!_kurtarici.Baslat(frames, _sonYoklama)) return;

        _kurtarmaNedeni = neden;
        GunlugeYaz(engine, neden);
        _trace.Add("kurtarma -> ses");
        var yapildi = engine.ReloadAudio();
        if (_kurtarici.SesSonucu(yapildi, _sonYoklama) == KurtarmaAdimi.YenidenAc)
        {
            YenidenAc();
            return;
        }

        if (neden == SeekFailedKey) _seek.GoTo(_seek.Target);
    }

    private void YenidenAc()
    {
        if (_engine is not { } engine) return;
        var surum = ++_kurtarmaSurum;
        _trace.Add("kurtarma -> yeniden ac");
        KurtarmaIsi = YenidenAcAsync(engine, surum, _seek.Target, _playing);
    }

    private async Task YenidenAcAsync(IPlaybackEngine engine, int surum, double at, bool playing)
    {
        bool basarili;
        try
        {
            basarili = await engine.ReopenAsync(at, playing).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException or OperationCanceledException)
        {
            basarili = false;
        }

        if (surum != _kurtarmaSurum || !ReferenceEquals(engine, _engine)) return;
        if (_kurtarici.Acildi(basarili, _sonYoklama) == KurtarmaAdimi.Basarisiz) KurtarmaDustu();
        UyariyiGoster();
    }

    private void KurtarmaDustu()
    {
        _trace.Add("kurtarma -> dustu");
        var frames = _engine?.FramesRendered ?? 0;
        if (_engine is { } engine) GunlugeYaz(engine, "dustu " + _kurtarmaNedeni);
        _uyari = (Strings.Get(_kurtarici.Denendi ? RecoveryFailedKey : _kurtarmaNedeni), frames);
    }

    private void GunlugeYaz(IPlaybackEngine engine, string olay)
    {
        var satirlar = new List<string>
        {
            "hedef=" + Saat.Tani.Konum(_seek.Target) + " konum=" + Saat.Tani.Konum(engine.PositionSeconds)
                + " oynuyor=" + _playing + " sonda=" + engine.EndReached + " kare=" + engine.FramesRendered
        };
        satirlar.AddRange(_trace.Skip(Math.Max(0, _trace.Count - 12)).Select(iz => "iz " + iz));
        satirlar.AddRange(engine.RecentLog);
        TakilmaGunlugu.Yaz(olay, satirlar, DateTime.Now);
    }

    private void KullaniciIslemi()
    {
        if (_uyari is null && _kurtarici.Asama == KurtarmaAsamasi.Bosta) return;
        _uyari = null;
        _kurtarici.Iptal();
        _kurtarmaSurum++;
        TxtStall.IsVisible = false;
    }

    private void KurtarmayiSifirla()
    {
        _uyari = null;
        _kurtarici.Iptal();
        _kurtarmaSurum++;
    }

    private void UyariyiGoster()
    {
        if (_uyari is { } uyari)
        {
            TxtStall.IsVisible = true;
            TxtStall.Text = uyari.Metin;
            return;
        }

        TxtStall.IsVisible = false;
    }
}
