namespace VidShrink.Core;

/// <summary>
/// Dönüştür sekmesinin kodek seçiminin kaydı. Seçim adla (liste etiketiyle) saklanır; donanım
/// kodlayıcıları listeye girince sıra numaraları kaydı için <see cref="LegacyOrder"/> eski listenin
/// sırasını tutar ve eski kayıt aynı kodeğe göçer.
/// </summary>
public static class ConversionCodecChoice
{
    public const string Default = "libx264";

    /// <summary>Donanım kodlayıcıları girmeden önceki listenin sırası. Değiştirilmez.</summary>
    public static IReadOnlyList<string> LegacyOrder { get; } =
    [
        "libx264", "libx265", "libvpx-vp9", "libsvtav1", "copy",
        "prores_ks:proxy", "prores_ks:lt", "prores_ks:standard", "prores_ks:hq", "prores_ks:4444",
        "dnxhd:dnxhr_lb", "dnxhd:dnxhr_sq", "dnxhd:dnxhr_hq", "dnxhd:dnxhr_hqx", "dnxhd:dnxhr_444"
    ];

    /// <summary>Listede yalnız yoklamada çalışan makinede görünen donanım kodlayıcıları.</summary>
    public static IReadOnlyList<string> Hardware { get; } =
    [
        "h264_nvenc", "hevc_nvenc", "av1_nvenc",
        "h264_qsv", "hevc_qsv", "av1_qsv",
        "h264_amf", "hevc_amf", "av1_amf"
    ];

    /// <summary>Arşiv kodekleri (<see cref="IntermediateCodecs.ArchiveProfiles"/>); eski listede yoktular, adla saklanır.</summary>
    public static IReadOnlyList<string> Archive { get; } = IntermediateCodecs.ArchiveProfiles.Select(p => p.Id).ToList();

    /// <summary>Kayıtlı ad varsa o, yoksa eski sıra numarasının eski listedeki kodeği.</summary>
    public static string FromSaved(string? name, int legacyIndex)
    {
        if (!string.IsNullOrWhiteSpace(name)) return name;
        return legacyIndex >= 0 && legacyIndex < LegacyOrder.Count ? LegacyOrder[legacyIndex] : Default;
    }

    /// <summary>Kodeğin eski listedeki yeri; eski listede yoksa 0.</summary>
    public static int LegacyIndex(string? tag)
    {
        for (var i = 0; i < LegacyOrder.Count; i++)
            if (LegacyOrder[i] == tag) return i;
        return 0;
    }
}
