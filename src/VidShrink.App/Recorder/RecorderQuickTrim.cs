using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using VidShrink.App.Playback;

namespace VidShrink.App.Recorder;

/// <summary>
/// Sonuç panelindeki hızlı kırpmanın saf kararları. Kesim yeniden kodlamaz: <see cref="ClipExport"/>'un
/// video yolu girişten önce <c>-ss</c>, sonra <c>-t</c> ve <c>-c copy</c> yazar; kopyalanan akış bu yüzden
/// başlangıçtan önceki son anahtar kareden başlar. Başlangıç o kareye çekilir ki ekranda söylenen aralık
/// dosyaya yazılanla aynı olsun.
/// </summary>
internal static class RecorderQuickTrim
{
    internal const string TargetSuffix = "-kirpilmis";

    private const double Tolerance = 0.001;

    /// <summary>Başlangıca eşit ya da ondan önceki en büyük anahtar kare; yoksa 0.</summary>
    internal static double SnapStart(IReadOnlyList<double> keyframes, double start)
    {
        ArgumentNullException.ThrowIfNull(keyframes);
        var snapped = 0d;
        foreach (var keyframe in keyframes)
            if (keyframe <= start + Tolerance && keyframe > snapped) snapped = keyframe;
        return snapped;
    }

    /// <summary>Kaynağın yanında <c>&lt;ad&gt;-kirpilmis&lt;uzantı&gt;</c>; varsa <c>-2</c>, <c>-3</c>…</summary>
    internal static string TargetFor(string source, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        var folder = Path.GetDirectoryName(source) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(source) + TargetSuffix;
        var extension = Path.GetExtension(source);
        var candidate = Path.Combine(folder, stem + extension);
        for (var index = 2; exists(candidate); index++)
            candidate = Path.Combine(folder, stem + "-" + index.ToString(CultureInfo.InvariantCulture) + extension);
        return candidate;
    }

    /// <summary>Anahtar kareye oturmuş başlangıç ve sona kadar süreyle kopya isteği; aralık boşsa <c>null</c>.</summary>
    internal static ClipRequest? Request(string source, string target, IReadOnlyList<double> keyframes, double start, double end)
    {
        var snapped = SnapStart(keyframes, start);
        if (!double.IsFinite(end) || end - snapped <= Tolerance) return null;
        return new ClipRequest(source, target, snapped, end - snapped, ClipKind.Video);
    }
}
