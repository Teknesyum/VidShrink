using System.Text;

namespace VidShrink.Core.Editing;

public enum AuditKind
{
    Silence,
    Black,
    Scene
}

/// <summary>Denetim listesinin bir satırı; sahne değişimi bir andır, <paramref name="End"/> boştur.</summary>
public sealed record AuditEntry(AuditKind Kind, double Start, double? End);

/// <summary>
/// ARC-05: düzenleyicinin sessizlik, siyah kare ve sahne taramasının ham sonucu zaman kodlu liste
/// olarak. Plan (en kısa süre, pay) uygulanmaz; liste taramanın bulduğunun aynısıdır, başlangıca
/// göre sıralı, eşit başlangıçta tür sırasıyla.
/// </summary>
public static class AuditReport
{
    public static IReadOnlyList<AuditEntry> Build(SilenceScan? scan, IReadOnlyList<double>? sceneCuts)
    {
        var entries = new List<AuditEntry>();
        if (scan is not null)
        {
            entries.AddRange(scan.Silence.Select(s => new AuditEntry(AuditKind.Silence, s.Start, s.End)));
            entries.AddRange(scan.Black.Select(s => new AuditEntry(AuditKind.Black, s.Start, s.End)));
        }
        if (sceneCuts is not null)
            entries.AddRange(sceneCuts.Select(t => new AuditEntry(AuditKind.Scene, t, null)));
        return entries.OrderBy(e => e.Start).ThenBy(e => e.Kind).ToList();
    }

    public static string Line(AuditEntry entry, Func<AuditKind, string> name)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var time = Saat.Ffmpeg(TimeSpan.FromSeconds(entry.Start));
        if (entry.End is { } end) time += " - " + Saat.Ffmpeg(TimeSpan.FromSeconds(end));
        return time + "  " + name(entry.Kind);
    }

    public static string Text(IReadOnlyList<AuditEntry> entries, Func<AuditKind, string> name)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var text = new StringBuilder();
        foreach (var entry in entries) text.AppendLine(Line(entry, name));
        return text.ToString();
    }
}
