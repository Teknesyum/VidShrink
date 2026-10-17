using System.Globalization;

namespace VidShrink.Player;

public sealed partial class MpvEngine
{
    private const string SilenceLabel = "vssilence";
    private const string SilenceStartKey = "lavfi.silence_start";
    private const string SilenceEndKey = "lavfi.silence_end";

    private readonly object _silenceGate = new();
    private SilenceSkip _silence = SilenceSkip.Off;
    private volatile bool _silenceOn;
    private bool _rushing;
    private double _restSpeed = 1;
    private string? _seenStart;
    private string? _seenEnd;
    private double _rushAt = double.NaN;

    /// <summary>
    /// Ses zincirinin basina etiketli bir <c>silencedetect</c> halkasi koyar. Suzgecin
    /// <c>af-metadata</c>'ya yazdigi sessizlik baslangicinda hiz <see cref="SilenceSkip.RushSpeed"/>'e
    /// cikar, bitisinde eski hiza doner; atlamak yerine hizlandirmak ses ile goruntunun esligini korur.
    /// </summary>
    public void SetSilenceSkip(SilenceSkip skip)
    {
        var wanted = skip.Clamped();
        lock (_silenceGate)
        {
            _silence = wanted;
            _seenStart = null;
            _seenEnd = null;
            _rushAt = double.NaN;
            CommandSync("af", "remove", "@" + SilenceLabel);
            if (wanted.On)
                CommandSync("af", "pre", "@" + SilenceLabel + ":lavfi=[silencedetect=n=" + F(wanted.ThresholdDb) + "dB:d=" + F(wanted.MinimumSeconds) + "]");
            _silenceOn = wanted.On;
            if (!wanted.On) EndRush();
        }
    }

    public SilenceSkip Silence
    {
        get
        {
            lock (_silenceGate) return _silence;
        }
    }

    public bool RushingSilence
    {
        get
        {
            lock (_silenceGate) return _rushing;
        }
    }

    /// <summary>
    /// Anahtarin degeri degistiyse o olay yeni gelmistir: <c>af-metadata</c> eski olaylari tutabilir,
    /// bu yuzden durum degerlerin buyuklugunden degil degisiminden okunur.
    /// <para>Suzgec oynatmanin onunde kosar; sessizlik bildirildiginde konum henuz sesli kisimda
    /// olabilir. Hizlanma bildirilen baslangica kadar bekler; konuma uymayan bir deger beklemez.</para>
    /// </summary>
    private void WatchSilence()
    {
        if (!_silenceOn) return;
        var start = GetProperty("af-metadata/" + SilenceLabel + "/" + SilenceStartKey);
        var end = GetProperty("af-metadata/" + SilenceLabel + "/" + SilenceEndKey);
        lock (_silenceGate)
        {
            if (!_silence.On) return;
            if (double.IsFinite(_rushAt) && Volatile.Read(ref _position) >= _rushAt) StartRush();
            var started = start is not null && start != _seenStart;
            var ended = end is not null && end != _seenEnd;
            if (started) _seenStart = start;
            if (ended) _seenEnd = end;
            if (!started && !ended) return;
            var silent = started && (!ended || Seconds(start) > Seconds(end));
            if (!silent)
            {
                EndRush();
                return;
            }

            var at = Seconds(start);
            var now = Volatile.Read(ref _position);
            if (double.IsFinite(at) && double.IsFinite(now) && at > now && at - now <= _silence.MinimumSeconds + LeadSeconds) _rushAt = at;
            else StartRush();
        }
    }

    private const double LeadSeconds = 1;

    private void StartRush()
    {
        _rushAt = double.NaN;
        if (_rushing) return;
        _restSpeed = Finite(GetDouble("speed"), 1);
        _rushing = true;
        TrySet("speed", Number(Math.Max(SilenceSkip.RushSpeed, _restSpeed)));
    }

    private void EndRush()
    {
        _rushAt = double.NaN;
        if (!_rushing) return;
        _rushing = false;
        TrySet("speed", Number(_restSpeed));
    }

    private void SilenceSeeked()
    {
        lock (_silenceGate) EndRush();
    }

    private bool SpeedWhileRushing(double speed)
    {
        lock (_silenceGate)
        {
            if (!_rushing) return false;
            _restSpeed = speed;
            TrySet("speed", Number(Math.Max(SilenceSkip.RushSpeed, speed)));
            return true;
        }
    }

    private double RestSpeed
    {
        get
        {
            lock (_silenceGate) return _rushing ? _restSpeed : double.NaN;
        }
    }

    private static double Seconds(string? value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ? seconds : double.NaN;
}
