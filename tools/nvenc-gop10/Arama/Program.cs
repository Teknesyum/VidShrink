using System.Diagnostics;
using System.Globalization;
using VidShrink.Player;

var inv = CultureInfo.InvariantCulture;
foreach (var path in args)
{
    using var engine = new MpvEngine();
    engine.SetProperty("ao", "null");
    await engine.OpenAsync(path);
    var duration = engine.DurationSeconds;
    engine.Play();
    await Task.Delay(2000);
    var rng = new Random(1);
    var shown = new List<double>();
    var lines = new List<string>();
    for (var i = 0; i < 24; i++)
    {
        var target = 1.0 + rng.NextDouble() * (duration - 4.0);
        var r = await engine.SeekAsync(target, SeekPrecision.Exact);
        if (r.Outcome == SeekOutcome.Shown) shown.Add(r.LatencyMs);
        lines.Add(string.Format(inv, "  {0:0.000} -> {1} {2:0.0}", target, r.Outcome, r.LatencyMs));
        await Task.Delay(400);
    }
    var s = shown.OrderBy(x => x).ToArray();
    double P(double q) => s.Length == 0 ? double.NaN : s[Math.Min(s.Length - 1, (int)Math.Floor(q * (s.Length - 1) + 0.5))];
    var med = s.Length == 0 ? double.NaN : s.Length % 2 == 1 ? s[s.Length / 2] : (s[s.Length / 2 - 1] + s[s.Length / 2]) / 2;
    Console.WriteLine(string.Format(inv, "{0} sure {1:0.0} gosterilen {2}/24 p50 {3:0.0} p90 {4:0.0} max {5:0.0} ms", Path.GetFileName(path), duration, s.Length, med, P(0.9), s.Length == 0 ? double.NaN : s[^1]));
    foreach (var l in lines) Console.WriteLine(l);
}
