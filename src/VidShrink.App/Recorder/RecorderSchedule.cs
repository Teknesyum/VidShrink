using System;

namespace VidShrink.App.Recorder;

internal enum RecorderRepeat
{
    Off,
    Once,
    Daily,
    Weekly,
}

/// <summary>
/// EKA-10 saatli kayıt takvimi. Saat dışarıdan verilir; hesap yerel duvar saatiyle yapılır,
/// geçmiş ya da şu anki saat ertesi oluşuma kayar.
/// </summary>
internal static class RecorderSchedule
{
    internal static readonly RecorderRepeat[] Choices = { RecorderRepeat.Off, RecorderRepeat.Once, RecorderRepeat.Daily, RecorderRepeat.Weekly };

    internal static readonly TimeSpan LateLimit = TimeSpan.FromMinutes(1);

    internal static DateTime First(DateTime now, TimeSpan time, RecorderRepeat repeat, DayOfWeek day)
    {
        var candidate = now.Date + time;
        if (repeat == RecorderRepeat.Weekly)
        {
            candidate = candidate.AddDays(((int)day - (int)now.DayOfWeek + 7) % 7);
            return candidate <= now ? candidate.AddDays(7) : candidate;
        }

        return candidate <= now ? candidate.AddDays(1) : candidate;
    }

    internal static DateTime? Upcoming(DateTime anchor, RecorderRepeat repeat, DateTime now)
    {
        if (repeat == RecorderRepeat.Once) return anchor > now ? anchor : null;
        if (Period(repeat) is not { } period) return null;
        if (anchor > now) return anchor;
        var steps = (now - anchor).Ticks / period.Ticks + 1;
        return anchor.AddTicks(steps * period.Ticks);
    }

    internal static DateTime? Latest(DateTime anchor, RecorderRepeat repeat, DateTime now)
    {
        if (repeat == RecorderRepeat.Off || anchor > now) return null;
        if (repeat == RecorderRepeat.Once || Period(repeat) is not { } period) return anchor;
        var steps = (now - anchor).Ticks / period.Ticks;
        return anchor.AddTicks(steps * period.Ticks);
    }

    internal static bool TryParseTime(string? text, out TimeSpan time)
    {
        time = default;
        var parts = (text ?? string.Empty).Trim().Split(':');
        if (parts.Length != 2
            || !int.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var hour)
            || !int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var minute)
            || hour > 23 || minute > 59 || parts[1].Length != 2)
            return false;
        time = new TimeSpan(hour, minute, 0);
        return true;
    }

    private static TimeSpan? Period(RecorderRepeat repeat) => repeat switch
    {
        RecorderRepeat.Daily => TimeSpan.FromDays(1),
        RecorderRepeat.Weekly => TimeSpan.FromDays(7),
        _ => null,
    };
}
