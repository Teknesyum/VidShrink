using System.Globalization;
using System.Text;

namespace VidShrink.Core;

/// <summary>
/// Kaydedicinin tani gunlugu: her kayit baslarken secilen yakalama, dusus sebebi, DXGI
/// cikislari, HDR kararlari, kodlayici ve tam ffmpeg argumanlari tek dosyaya yazilir. Dosya
/// yalniz son kaydi tutar, her kayitta ustune yazilir.
///
/// <para>Gizlilik <see cref="Gunluk"/> ile ayni: tam yol dosya adina indirilir.</para>
/// </summary>
public static class KayitTanisi
{
    /// <summary>Gunluk klasorundeki dosyanin adi.</summary>
    public const string DosyaAdi = "recorder-last.log";

    /// <summary>Kaydin tani metni; <paramref name="argumanlar"/> yoksa ffmpeg baslamadan dusmustur.</summary>
    public static string Metin(
        RecorderRequest istek,
        IReadOnlyList<string>? argumanlar,
        string uygulamaSurumu,
        DateTimeOffset zaman,
        string? hata = null)
    {
        ArgumentNullException.ThrowIfNull(istek);
        var inv = CultureInfo.InvariantCulture;
        var b = new StringBuilder();
        b.Append("time: ").AppendLine(zaman.ToString("yyyy-MM-dd HH:mm:ss zzz", inv));
        b.Append("app: ").AppendLine(uygulamaSurumu);
        b.Append("platform: ").AppendLine(istek.Platform.ToString());
        b.Append("target: ").AppendLine(istek.Target.ToString());
        b.Append("capture: ").AppendLine(istek.Capture.ToString().ToLowerInvariant());
        b.Append("fallback: ").AppendLine(istek.CaptureFallback.ToString());
        b.Append("outputs: ").AppendLine(istek.DdaOutputs.Count.ToString(inv));
        foreach (var o in istek.DdaOutputs)
            b.AppendLine(string.Format(inv, "  [{0}] {1}x{2} at {3},{4} hdr={5}{6}",
                o.Index, o.Width, o.Height, o.X, o.Y, o.Hdr ? "yes" : "no",
                (o.Display is { } d ? string.Format(inv, " nits={0:0.####}/{1:0.#}/{2:0.#}", d.MinNits, d.MaxNits, d.MaxFullFrameNits) : "")
                + (o.SdrWhiteNits is { } w ? string.Format(inv, " sdr-white={0:0.#}", w) : "")));
        var hedef = RecorderArguments.DdagrabTarget(istek);
        b.Append("target-output: ").AppendLine(hedef is { } h ? h.Output.Index.ToString(inv) : "-");
        b.Append("window-clipped: ").AppendLine(Evet(RecorderArguments.WindowClipped(istek)));
        b.Append("hdr-capture: ").AppendLine(Evet(RecorderArguments.HdrCapture(istek)));
        var statik = RecorderArguments.HdrStatic(istek);
        b.Append("hdr-static: ").AppendLine(statik is null ? "-" : statik.MasterDisplay + " cll=" + statik.ContentLight);
        b.Append("hdr-to-sdr: ").AppendLine(Evet(istek.HdrToSdr));
        b.Append("hdr-tonemap: ").AppendLine(Evet(RecorderArguments.HdrTonemapCapture(istek)));
        if (RecorderArguments.HdrTonemapCapture(istek))
        {
            b.Append("sdr-white: ").Append(RecorderArguments.SdrWhite(istek).ToString("0.#", inv))
                .AppendLine(RecorderArguments.SdrWhiteAssumed(istek) ? " nits (assumed, Windows value unreadable)" : " nits (Windows)");
            b.Append("peak: ").Append(RecorderArguments.PeakNits(istek).ToString("0.#", inv)).AppendLine(" nits");
        }
        b.Append("hdr-tonemap-missing: ").AppendLine(Evet(RecorderArguments.HdrTonemapMissing(istek)));
        b.Append("hdr-lost: ").AppendLine(Evet(RecorderArguments.HdrLost(istek)));
        b.Append("hdr-on-gdigrab: ").AppendLine(Evet(RecorderArguments.HdrOnGdigrab(istek)));
        b.Append("container: ").AppendLine(istek.Container.ToString().ToLowerInvariant());
        b.Append("encoder: ").AppendLine(istek.VideoCodec);
        b.Append("pixel-format: ").AppendLine(istek.PixelFormat);
        b.Append("color-space: ").AppendLine(string.IsNullOrWhiteSpace(istek.ColorSpace) ? "-" : istek.ColorSpace);
        b.Append("color-range: ").AppendLine(string.IsNullOrWhiteSpace(istek.ColorRange) ? "-" : istek.ColorRange);
        b.Append("fps: ").AppendLine(istek.Fps.ToString(inv));
        b.Append("args: ").AppendLine(argumanlar is null ? "-" : Gunluk.YoluIndirge(Birlestir(argumanlar)));
        if (!string.IsNullOrWhiteSpace(hata)) b.Append("error: ").AppendLine(Gunluk.YoluIndirge(hata));
        return b.ToString();
    }

    /// <summary>Metni <paramref name="klasor"/> altindaki dosyaya ustune yazar; yazilamazsa <c>null</c>.</summary>
    public static string? Yaz(string klasor, string metin)
    {
        if (string.IsNullOrWhiteSpace(klasor)) return null;
        try
        {
            Directory.CreateDirectory(klasor);
            var yol = Path.Combine(klasor, DosyaAdi);
            File.WriteAllText(yol, metin, new UTF8Encoding(false));
            return yol;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static string Evet(bool deger) => deger ? "yes" : "no";

    private static string Birlestir(IReadOnlyList<string> argumanlar)
        => string.Join(" ", argumanlar.Select(a => a.Length == 0 || a.Any(char.IsWhiteSpace) ? "\"" + a + "\"" : a));
}
