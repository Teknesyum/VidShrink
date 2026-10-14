using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VidShrink.Player;

using VidShrink.Core;

using VidShrink.App.Localization;

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

    internal static readonly PlayerAction AudioCycle = new(PlayerCommandKind.AudioCycle, 0, "player.tracks.cycle");
    internal static readonly PlayerAction SubtitleCycle = new(PlayerCommandKind.SubtitleCycle, 0, "player.subtitle.cycle");
    internal static readonly PlayerAction SubtitleLater = new(PlayerCommandKind.SubtitleDelay, SubtitleDelayStep, "player.subtitle.delay");
    internal static readonly PlayerAction SubtitleEarlier = new(PlayerCommandKind.SubtitleDelay, -SubtitleDelayStep, "player.subtitle.delay");
    internal static readonly PlayerAction AudioLater = new(PlayerCommandKind.AudioDelay, AudioDelayStep, "player.tracks.delay");
    internal static readonly PlayerAction AudioEarlier = new(PlayerCommandKind.AudioDelay, -AudioDelayStep, "player.tracks.delay");

    internal static readonly PlayerAction SubtitleVisibility = new(PlayerCommandKind.SubtitleVisibility, 0, "player.subtitle.visibility");
    internal static readonly PlayerAction SubtitlePreviousLine = new(PlayerCommandKind.SubtitleLine, -1, "player.subtitle.lineprev");
    internal static readonly PlayerAction SubtitleNextLine = new(PlayerCommandKind.SubtitleLine, 1, "player.subtitle.linenext");
    internal static readonly PlayerAction SubtitleStepBack = new(PlayerCommandKind.SubtitleStep, -1, "player.subtitle.stepprev");
    internal static readonly PlayerAction SubtitleStepForward = new(PlayerCommandKind.SubtitleStep, 1, "player.subtitle.stepnext");
    internal static readonly PlayerAction SubtitleCopy = new(PlayerCommandKind.SubtitleCopy, 0, "player.subtitle.copy");

    internal static readonly IReadOnlyList<string> Extensions = new[] { "srt", "ass", "ssa", "vtt", "sub", "sup", "idx" };

    internal static readonly IReadOnlyList<string> SubtitleFolders = new[] { "Subs", "Subtitles", "Sub", "Subtitle" };

    private static readonly IReadOnlyList<string> ImageExtensions = new[] { "sup", "idx", "sub" };

    /// <summary>
    /// Videonun altyazilari: ayni klasorde ya da <see cref="SubtitleFolders"/> alt klasorunde adi
    /// videonun adiyla baslayan, ayrica <c>Subs/&lt;video adi&gt;/</c> icindeki her dosya; uzantisi
    /// <see cref="Extensions"/>'ta olmali. Esi <c>.idx</c> olan <c>.sub</c> atlanir (VobSub ciftinin
    /// verisi). Alt klasordekiler once, yandakiler sonra; birebir ayni ad ve metin altyazi en sona
    /// konur, motor son ekleneni sectigi icin o secili kalir.
    /// </summary>
    internal static IReadOnlyList<string> Sidecars(string mediaPath)
    {
        var folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(mediaPath));
        if (folder is null || !System.IO.Directory.Exists(folder)) return Array.Empty<string>();
        var name = System.IO.Path.GetFileNameWithoutExtension(mediaPath);

        var found = new List<(string File, bool Main, bool Exact)>();
        found.AddRange(Named(folder, name).Select(x => (x.File, true, x.Exact)));
        foreach (var sub in SafeDirectories(folder)
                     .Where(dir => SubtitleFolders.Contains(System.IO.Path.GetFileName(dir), StringComparer.OrdinalIgnoreCase)))
        {
            found.AddRange(Named(sub, name).Select(x => (x.File, false, x.Exact)));
            foreach (var own in SafeDirectories(sub)
                         .Where(dir => string.Equals(System.IO.Path.GetFileName(dir), name, StringComparison.OrdinalIgnoreCase)))
                found.AddRange(SubtitleFiles(own).Select(file => (file, false, false)));
        }

        return found
            .DistinctBy(x => x.File, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Main)
            .ThenBy(x => x.Exact)
            .ThenBy(x => !IsImageSubtitle(x.File))
            .ThenBy(x => x.File, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.File)
            .ToList();
    }

    private static IEnumerable<(string File, bool Exact)> Named(string folder, string name)
        => SubtitleFiles(folder)
            .Select(file => (File: file, Stem: System.IO.Path.GetFileNameWithoutExtension(file)))
            .Where(x => x.Stem.StartsWith(name, StringComparison.OrdinalIgnoreCase)
                        && (x.Stem.Length == name.Length || x.Stem[name.Length] is '.' or '-' or '_' or ' '))
            .Select(x => (x.File, x.Stem.Length == name.Length));

    private static IEnumerable<string> SubtitleFiles(string folder)
    {
        var files = SafeFiles(folder).Where(IsSubtitleFile).ToList();
        return files.Where(file => !HasExtension(file, "sub")
                                   || !files.Any(other => HasExtension(other, "idx")
                                                          && string.Equals(System.IO.Path.ChangeExtension(other, null), System.IO.Path.ChangeExtension(file, null), StringComparison.OrdinalIgnoreCase)));
    }

    private static bool HasExtension(string file, string extension)
        => string.Equals(System.IO.Path.GetExtension(file).TrimStart('.'), extension, StringComparison.OrdinalIgnoreCase);

    private static bool IsImageSubtitle(string file) => ImageExtensions.Any(extension => HasExtension(file, extension));

    private static IReadOnlyList<string> SafeFiles(string folder)
    {
        try { return System.IO.Directory.EnumerateFiles(folder).ToList(); }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    private static IReadOnlyList<string> SafeDirectories(string folder)
    {
        try { return System.IO.Directory.EnumerateDirectories(folder).ToList(); }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

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

    internal bool Visible { get; private set; } = true;

    internal void ToggleVisible() => Visible = !Visible;

    internal void SyncSubtitleDelay(double seconds) => SubtitleDelay = Math.Round(seconds, 3);

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
        engine.SetSubtitleVisible(Visible);
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
        => (value < 0 ? "−" : "+") + Saat.Adim(Math.Abs(value), Strings.Culture);
}
