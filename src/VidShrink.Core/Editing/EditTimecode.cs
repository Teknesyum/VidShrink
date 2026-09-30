using System.Globalization;

namespace VidShrink.Core.Editing;

public static class EditTimecode
{
    public static string Format(long time, double fps)
    {
        var rate = Rate.From(fps);
        var negative = time < 0;
        var frames = Math.Abs(time) / rate.FrameTicks;
        var nominal = rate.Nominal;
        var counted = frames;
        if (rate.Drop > 0)
        {
            var perMinute = nominal * 60 - rate.Drop;
            var perTenMinutes = nominal * 600 - 9 * rate.Drop;
            var tens = frames / perTenMinutes;
            var rest = frames % perTenMinutes;
            counted += 9 * rate.Drop * tens;
            if (rest >= rate.Drop) counted += rate.Drop * ((rest - rate.Drop) / perMinute);
        }

        var ff = counted % nominal;
        var totalSeconds = counted / nominal;
        var ss = totalSeconds % 60;
        var mm = totalSeconds / 60 % 60;
        var hh = totalSeconds / 3600;
        var separator = rate.Drop > 0 ? ';' : ':';
        var text = string.Create(CultureInfo.InvariantCulture, $"{hh:00}:{mm:00}:{ss:00}{separator}{ff:00}");
        return negative ? "-" + text : text;
    }

    public static bool TryParse(string? text, double fps, out long time)
    {
        return TryParse(text, fps, 0, out time, allowRelative: false);
    }

    public static bool TryParse(string? text, double fps, long baseTime, out long time)
    {
        return TryParse(text, fps, baseTime, out time, allowRelative: true);
    }

    private static bool TryParse(string? text, double fps, long baseTime, out long time, bool allowRelative)
    {
        time = 0;
        if (string.IsNullOrWhiteSpace(text) || !Rate.TryFrom(fps, out var rate)) return false;
        var s = text.Trim();

        if (s[0] is '+' or '-')
        {
            if (!allowRelative) return false;
            var digits = s.AsSpan(1).Trim();
            if (!IsNumber(digits)) return false;
            var delta = long.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
            var target = baseTime + (s[0] == '-' ? -delta : delta) * rate.FrameTicks;
            if (target < 0) return false;
            time = target;
            return true;
        }

        var fields = new List<long>();
        if (s.IndexOfAny(new[] { ':', ';', '.' }) >= 0)
        {
            foreach (var part in s.Split(':', ';', '.'))
            {
                if (!IsNumber(part)) return false;
                fields.Add(long.Parse(part, NumberStyles.None, CultureInfo.InvariantCulture));
            }
        }
        else
        {
            if (!IsNumber(s)) return false;
            for (var end = s.Length; end > 0; end -= 2)
            {
                var start = Math.Max(0, end - 2);
                fields.Add(long.Parse(s.AsSpan(start, end - start), NumberStyles.None, CultureInfo.InvariantCulture));
            }

            fields.Reverse();
        }

        if (fields.Count > 4) return false;
        while (fields.Count < 4) fields.Insert(0, 0);
        var hh = fields[0];
        var mm = fields[1];
        var ss = fields[2];
        var ff = fields[3];
        if (mm >= 60 || ss >= 60 || ff >= rate.Nominal) return false;

        var totalMinutes = hh * 60 + mm;
        var frames = (totalMinutes * 60 + ss) * rate.Nominal + ff;
        if (rate.Drop > 0)
        {
            if (ss == 0 && mm % 10 != 0 && ff < rate.Drop) return false;
            frames -= rate.Drop * (totalMinutes - totalMinutes / 10);
        }

        time = frames * rate.FrameTicks;
        return true;
    }

    private static bool IsNumber(ReadOnlySpan<char> digits)
    {
        if (digits.Length is 0 or > 9) return false;
        foreach (var c in digits)
            if (c is < '0' or > '9') return false;
        return true;
    }

    private readonly record struct Rate(long FrameTicks, int Nominal, int Drop)
    {
        public static Rate From(double fps)
        {
            if (!TryFrom(fps, out var rate)) throw new ArgumentOutOfRangeException(nameof(fps), fps, "Gecersiz kare hizi");
            return rate;
        }

        public static bool TryFrom(double fps, out Rate rate)
        {
            rate = default;
            if (double.IsNaN(fps) || double.IsInfinity(fps) || fps < 1 || fps > 240) return false;
            var nominal = (int)Math.Round(fps);
            if (Math.Abs(fps - nominal * 1000.0 / 1001.0) < 0.01)
            {
                var ticks = (long)Math.Round(EditTime.TicksPerSecond * 1001.0 / (nominal * 1000.0));
                rate = new Rate(ticks, nominal, nominal is 30 or 60 ? nominal / 15 : 0);
                return true;
            }

            var plain = (long)Math.Round(EditTime.TicksPerSecond / fps);
            if (plain < 1) return false;
            rate = new Rate(plain, nominal, 0);
            return true;
        }
    }
}
