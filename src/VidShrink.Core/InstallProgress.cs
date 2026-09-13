using System.Globalization;

namespace VidShrink.Core;

/// <summary>Kurulum panelinin üç durumu; renk ve sonuç düğmeleri buna bakar.</summary>
public enum InstallState
{
    /// <summary>İş sürüyor; hiçbir düğme görünmez.</summary>
    Running,

    /// <summary>İş bitti.</summary>
    Done,

    /// <summary>İş hatayla bitti.</summary>
    Failed
}

/// <summary>
/// Kurulum ve güncelleme panelinin iş ile arayüz arasındaki tek köprüsü. İş tarafı ekrana
/// yalnız <see cref="Step"/> ile konuşur; çizen taraf <see cref="Advance"/> ile bir kare
/// ilerletip <see cref="Bar"/>, <see cref="Sentence"/> ve <see cref="Log"/>'u okur.
///
/// <para><b>Tavan kuralı:</b> her adım "şu an buradayım" (<see cref="Percent"/>) ve "en
/// fazla buraya kadar" (<see cref="Ceiling"/>) der. Çubuk yüzdeye hızla yaklaşır
/// (fark × <see cref="Approach"/>, en az <see cref="MinimumStep"/>), yüzde durursa tavana
/// sürünür (fark × <see cref="Creep"/>). Uzayan adımda çubuk yaşar ama sonraki adımın
/// alanını yemez.</para>
///
/// <para>Yüzde asla geri gitmez: geriye yazan bir adım görülen en büyük yüzdede tutulur.
/// Sayı burada uydurulmuyor — katsayılar <c>private/tercihler/guncelleme-paneli.md</c>'nin
/// yazdığı sayılar.</para>
/// </summary>
public sealed class InstallProgress
{
    /// <summary>Ekranda duran günlük satırı sayısı; sonuncusu vurgulu.</summary>
    public const int LogLines = 9;

    /// <summary>Çubuğun yüzdeye yaklaşma oranı.</summary>
    public const double Approach = 0.08;

    /// <summary>Yaklaşmanın en küçük adımı; çok küçük farkta çubuk yine de kımıldar.</summary>
    public const double MinimumStep = 0.2;

    /// <summary>Yüzde durduğunda çubuğun tavana sürünme oranı.</summary>
    public const double Creep = 0.006;

    /// <summary>İki kare arası; panelin yenileme aralığı.</summary>
    public const int FrameMilliseconds = 16;

    private readonly List<string> _log = new();
    private readonly object _gate = new();

    private double _percent;
    private double _ceiling;
    private double _bar;
    private string _sentence = string.Empty;
    private InstallState _state = InstallState.Running;

    /// <summary>Son adımın bildirdiği yüzde; geri gitmez.</summary>
    public double Percent { get { lock (_gate) return _percent; } }

    /// <summary>Son adımın bildirdiği tavan; çubuk bunu geçmez.</summary>
    public double Ceiling { get { lock (_gate) return _ceiling; } }

    /// <summary>Çizilecek çubuk değeri.</summary>
    public double Bar { get { lock (_gate) return _bar; } }

    /// <summary>Ne yapıldığını söyleyen tam cümle.</summary>
    public string Sentence { get { lock (_gate) return _sentence; } }

    /// <summary>Panelin durumu.</summary>
    public InstallState State { get { lock (_gate) return _state; } }

    /// <summary>Ekranda duran son <see cref="LogLines"/> satır, eskiden yeniye.</summary>
    public IReadOnlyList<string> Log
    {
        get
        {
            lock (_gate)
            {
                var start = Math.Max(0, _log.Count - LogLines);
                return _log.GetRange(start, _log.Count - start);
            }
        }
    }

    /// <summary>Başından beri düşen bütün satırlar; diske yazılan günlük budur.</summary>
    public IReadOnlyList<string> History { get { lock (_gate) return _log.ToArray(); } }

    /// <summary>
    /// İş tarafının tek konuşma yolu. Yüzde ve tavan 0–100'e kırpılır, yüzde görülen en
    /// büyüğün altına inmez, tavan yüzdenin altına inmez. Her çağrı günlüğe bir satır düşer.
    /// </summary>
    public void Step(double percent, double ceiling, string sentence)
    {
        lock (_gate)
        {
            _percent = Math.Max(_percent, Clamp(percent));
            _ceiling = Math.Max(_percent, Clamp(ceiling));
            _sentence = sentence ?? string.Empty;
            _log.Add(_sentence);
        }
    }

    /// <summary>
    /// Bir kareyi ilerletir ve yeni çubuk değerini döndürür. Çubuk yüzdenin altındaysa
    /// hızla yaklaşır, yüzdeye vardıysa tavana sürünür; ikisini de geçmez.
    /// </summary>
    public double Advance()
    {
        lock (_gate)
        {
            if (_bar < _percent)
            {
                var step = Math.Max((_percent - _bar) * Approach, MinimumStep);
                _bar = Math.Min(_percent, _bar + step);
            }
            else if (_bar < _ceiling)
            {
                _bar = Math.Min(_ceiling, _bar + (_ceiling - _bar) * Creep);
            }

            return _bar;
        }
    }

    /// <summary>
    /// İşi bitirir: durum değişir, çubuk yüzde ve tavanla birlikte sona çekilir. Sonuç
    /// duyurulduktan sonra düğmeler görünür.
    /// </summary>
    public void Finish(bool succeeded, string sentence)
    {
        lock (_gate)
        {
            _state = succeeded ? InstallState.Done : InstallState.Failed;
            _percent = succeeded ? 100 : _percent;
            _ceiling = _percent;
            _bar = _percent;
            _sentence = sentence ?? string.Empty;
            _log.Add(_sentence);
        }
    }

    /// <summary>Yüzdenin ekrandaki yazımı; sarmayan tek satır için.</summary>
    public string PercentText => Math.Round(Percent).ToString("0", CultureInfo.InvariantCulture) + "%";

    private static double Clamp(double value)
        => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 100);
}
