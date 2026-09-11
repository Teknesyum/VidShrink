using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VidShrink.Player;

namespace VidShrink.App.Playback;

internal sealed record SubtitleCodepage(string Value, string LabelKey, string Name);

internal sealed class SubtitleOptions
{
    internal const double SubtitleDelayStep = 0.5;
    internal const double AudioDelayStep = 0.1;
    internal const double ScaleStep = 0.1;
    internal const double MinimumScale = 0.5;
    internal const double MaximumScale = 3;
    internal const double PositionStep = 5;
    internal const double MinimumPosition = 0;
    internal const double MaximumPosition = 150;
    internal const double DefaultPosition = 100;
    internal const string AutoCodepage = "auto";

    internal static readonly PlayerAction AudioCycle = new(PlayerCommandKind.AudioCycle, 0, "player.tracks.cycle", 0);
    internal static readonly PlayerAction SubtitleCycle = new(PlayerCommandKind.SubtitleCycle, 0, "player.subtitle.cycle", 0);
    internal static readonly PlayerAction SubtitleLater = new(PlayerCommandKind.SubtitleDelay, SubtitleDelayStep, "player.subtitle.delay", 0);
    internal static readonly PlayerAction SubtitleEarlier = new(PlayerCommandKind.SubtitleDelay, -SubtitleDelayStep, "player.subtitle.delay", 0);
    internal static readonly PlayerAction AudioLater = new(PlayerCommandKind.AudioDelay, AudioDelayStep, "player.tracks.delay", 0);
    internal static readonly PlayerAction AudioEarlier = new(PlayerCommandKind.AudioDelay, -AudioDelayStep, "player.tracks.delay", 0);

    internal static readonly IReadOnlyList<string> Extensions = new[] { "srt", "ass", "ssa", "vtt", "sub" };

    internal static readonly IReadOnlyList<SubtitleCodepage> Codepages = new SubtitleCodepage[]
    {
        new(AutoCodepage, "player.subtitle.encoding.auto", ""),
        new("utf-8", "player.subtitle.encoding.unicode", "UTF-8"),
        new("cp1254", "player.subtitle.encoding.turkish", "Windows-1254"),
        new("iso-8859-9", "player.subtitle.encoding.turkish", "ISO-8859-9"),
        new("cp1252", "player.subtitle.encoding.western", "Windows-1252"),
        new("cp1250", "player.subtitle.encoding.central", "Windows-1250"),
        new("cp1251", "player.subtitle.encoding.cyrillic", "Windows-1251"),
        new("koi8-r", "player.subtitle.encoding.cyrillic", "KOI8-R"),
        new("cp1253", "player.subtitle.encoding.greek", "Windows-1253"),
        new("cp1255", "player.subtitle.encoding.hebrew", "Windows-1255"),
        new("cp1256", "player.subtitle.encoding.arabic", "Windows-1256"),
        new("cp1257", "player.subtitle.encoding.baltic", "Windows-1257"),
        new("shift_jis", "player.subtitle.encoding.japanese", "Shift_JIS"),
        new("gbk", "player.subtitle.encoding.chinesesimplified", "GBK"),
        new("big5", "player.subtitle.encoding.chinesetraditional", "Big5"),
        new("euc-kr", "player.subtitle.encoding.korean", "EUC-KR")
    };

    internal double SubtitleDelay { get; private set; }

    internal double AudioDelay { get; private set; }

    internal double Scale { get; private set; } = 1;

    internal double Position { get; private set; } = DefaultPosition;

    internal string Codepage { get; private set; } = AutoCodepage;

    internal void ShiftSubtitleDelay(double seconds) => SubtitleDelay = Math.Round(SubtitleDelay + seconds, 3);

    internal void ShiftAudioDelay(double seconds) => AudioDelay = Math.Round(AudioDelay + seconds, 3);

    internal void ResetSubtitleDelay() => SubtitleDelay = 0;

    internal void ResetAudioDelay() => AudioDelay = 0;

    internal void ResetDelays()
    {
        SubtitleDelay = 0;
        AudioDelay = 0;
    }

    internal void Grow(double steps) => Scale = Math.Clamp(Math.Round(Scale + steps * ScaleStep, 2), MinimumScale, MaximumScale);

    internal void Move(double steps) => Position = Math.Clamp(Position + steps * PositionStep, MinimumPosition, MaximumPosition);

    internal void ResetLook()
    {
        Scale = 1;
        Position = DefaultPosition;
    }

    internal void UseCodepage(string value) => Codepage = Codepages.Any(c => c.Value == value) ? value : AutoCodepage;

    internal void ApplyTo(IPlaybackEngine engine)
    {
        engine.SetSubtitleCodepage(Codepage);
        engine.SetSubtitleScale(Scale);
        engine.SetSubtitlePosition(Position);
        engine.SetSubtitleDelay(SubtitleDelay);
        engine.SetAudioDelay(AudioDelay);
    }

    internal static bool IsSubtitleFile(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var extension = System.IO.Path.GetExtension(path).TrimStart('.');
        return Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    internal static long Next(IReadOnlyList<long> ids, long current, bool withOff)
    {
        var ring = withOff ? new[] { 0L }.Concat(ids).ToList() : ids.ToList();
        if (ring.Count == 0) return current;
        return ring[(ring.IndexOf(current) + 1) % ring.Count];
    }

    internal static string Signed(double value)
        => (value < 0 ? "−" : "+") + Math.Abs(value).ToString("0.##", CultureInfo.CurrentCulture);
}
