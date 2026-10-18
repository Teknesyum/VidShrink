using System.Globalization;
using System.Text;

namespace VidShrink.Core.Editing;

/// <summary>
/// Klip ayarlarinin libavfilter karsiligi. Disa aktarim ve onizleme ayni dizgeyi kullanir.
/// Geometri gorunen piksel uzerinde calisir: anamorfik kaynak once <c>setsar=1</c>'e olceklenir,
/// sonra kirpilir, dondurulur, cevrilir ve ortak tuvale sigdirilir.
/// </summary>
public static class ClipFilters
{
    public static int DisplayWidth(int width, int parNum, int parDen)
        => parNum != parDen && parNum > 0 && parDen > 0
            ? Math.Max(1, (int)Math.Round(width * (double)parNum / parDen))
            : width;

    /// <summary>
    /// Ciktinin tuvali. Geometrisi olan klip yoksa <c>null</c>: eski yol aynen kalir. Varsa butun
    /// kliplerin cikti boyutu ayniysa o boyut, degilse kaynagin gorunen boyutu, iki kenar cift.
    /// </summary>
    public static (int Width, int Height)? Canvas(IReadOnlyList<EditClip> clips, int width, int height, int parNum, int parDen)
    {
        if (!clips.Any(c => c.Effects.HasGeometry)) return null;

        var displayWidth = DisplayWidth(width, parNum, parDen);
        var sizes = clips.Select(c => c.Effects.OutputSize(displayWidth, height)).Distinct().ToList();
        return sizes.Count == 1 ? sizes[0] : (Math.Max(2, displayWidth / 2 * 2), Math.Max(2, height / 2 * 2));
    }

    public static (int Width, int Height)? Canvas(EditTimeline timeline, MediaInfo info)
        => Canvas(timeline.Clips, info.Width, info.Height, info.ParNum, info.ParDen);

    /// <summary>
    /// Tek klibin geometri zinciri, basinda virgul olmadan. Tuval <c>null</c> ise bos. Klibin
    /// boyutu tuvalden farkliysa oran korunarak kuculur ve ortalanarak doldurulur. Sigdirmali klip once kendi oranina
    /// olceklenip <see cref="ClipEffects.PadColor"/> ile doldurulur.
    /// </summary>
    public static string Geometry(ClipEffects effects, int width, int height, int parNum, int parDen, (int Width, int Height)? canvas)
    {
        if (canvas is not { } target) return string.Empty;

        var parts = new List<string>();
        var displayWidth = DisplayWidth(width, parNum, parDen);
        if (displayWidth != width) parts.Add("scale=" + Int(displayWidth) + ":" + Int(height));

        var rect = effects.CropRect(displayWidth, height);
        if (rect != (0, 0, displayWidth, height))
            parts.Add("crop=" + Int(rect.Width) + ":" + Int(rect.Height) + ":" + Int(rect.X) + ":" + Int(rect.Y));

        if (effects.Rotation == 90) parts.Add("transpose=1");
        if (effects.Rotation == 270) parts.Add("transpose=2");
        var half = effects.Rotation == 180;
        if (half != effects.FlipH) parts.Add("hflip");
        if (half != effects.FlipV) parts.Add("vflip");

        if (effects.HasFit)
        {
            var fit = effects.OutputSize(displayWidth, height);
            if (fit != (effects with { FitNum = 0, FitDen = 0 }).OutputSize(displayWidth, height))
            {
                var fw = Int(fit.Width);
                var fh = Int(fit.Height);
                parts.Add("scale=" + fw + ":" + fh + ":force_original_aspect_ratio=decrease:force_divisible_by=2");
                parts.Add("pad=" + fw + ":" + fh + ":(ow-iw)/2:(oh-ih)/2:color=0x" + effects.PadColor);
            }
        }

        if (effects.OutputSize(displayWidth, height) != target)
        {
            var w = Int(target.Width);
            var h = Int(target.Height);
            parts.Add("scale=" + w + ":" + h + ":force_original_aspect_ratio=decrease:force_divisible_by=2");
            parts.Add("pad=" + w + ":" + h + ":(ow-iw)/2:(oh-ih)/2");
        }

        parts.Add("setsar=1");
        return string.Join(",", parts);
    }

    /// <summary>Klip basinda sifirdan baslayan zamanla goruntu solmasi; <paramref name="length"/> cizelge tick'i.</summary>
    public static string VideoFades(ClipEffects effects, long length)
    {
        var (fadeIn, fadeOut) = effects.Fades(length);
        var parts = new List<string>();
        if (fadeIn > 0) parts.Add("fade=t=in:st=0:d=" + Seconds(fadeIn));
        if (fadeOut > 0) parts.Add("fade=t=out:st=" + Seconds(length - fadeOut) + ":d=" + Seconds(fadeOut));
        return string.Join(",", parts);
    }

    /// <summary>
    /// Renk zinciri, basinda virgul olmadan: otomatik renk aciksa <c>normalize</c>, degisen alanlarla <c>eq</c>, sicaklik
    /// notr degilse <c>colortemperature</c>, en sonda <see cref="ClipGrade.Filters"/> egri ve tekerleri. Renk ayari yoksa bos.
    /// </summary>
    public static string Color(ClipEffects effects)
    {
        var parts = new List<string>();
        var auto = ClipGrade.Auto(effects);
        if (auto.Length > 0) parts.Add(auto);
        var eq = new List<string>();
        if (effects.Brightness != 0) eq.Add("brightness=" + Number(effects.Brightness));
        if (effects.Contrast != 1) eq.Add("contrast=" + Number(effects.Contrast));
        if (effects.Saturation != 1) eq.Add("saturation=" + Number(effects.Saturation));
        if (eq.Count > 0) parts.Add("eq=" + string.Join(":", eq));
        if (effects.Temperature != ClipEffects.NeutralTemperature) parts.Add("colortemperature=temperature=" + Int(effects.Temperature));
        var grade = ClipGrade.Filters(effects);
        if (grade.Length > 0) parts.Add(grade);
        return string.Join(",", parts);
    }

    public static string Blur(ClipEffects effects, int width, int height, string label)
    {
        if (!effects.HasBlur) return string.Empty;
        var gblur = "gblur=sigma=" + Number(effects.BlurSigma);
        if (!effects.HasBlurRegion) return gblur;

        var (x, y, w, h) = effects.BlurRect(Math.Max(2, width), Math.Max(2, height));
        var main = "[" + label + "m]";
        var cut = "[" + label + "c]";
        var soft = "[" + label + "s]";
        return "split" + main + cut + ";" + cut + "crop=" + Int(w) + ":" + Int(h) + ":" + Int(x) + ":" + Int(y) + "," + gblur + soft + ";"
            + main + soft + "overlay=" + Int(x) + ":" + Int(y);
    }

    public static (int Width, int Height) Frame(int width, int height, (int Width, int Height)? canvas)
        => canvas ?? (Math.Max(2, width), Math.Max(2, height));

    public const string CompressorFilter = "acompressor=threshold=0.125:ratio=4:attack=20:release=250";

    public const string LimiterFilter = "alimiter=limit=0.89:level=0";

    /// <summary>
    /// Ses zinciri, zaman klip basindan. Sessizse yalniz <c>volume=0</c>; degilse sabit sirayla gurultu azaltma,
    /// uc bantli ekolayzir, kazanc, kompresor, yukseklik esitleme (ardindan 48 kHz'e geri ornekleme), sinirlayici;
    /// en sonda ses solmasi.
    /// </summary>
    public static string Audio(ClipEffects effects, long length)
    {
        var parts = new List<string>();
        if (effects.Muted) parts.Add("volume=0");
        else
        {
            if (effects.Denoise) parts.Add(AudioCaptureArguments.NoiseSuppressionFilterName);
            parts.AddRange(Equalizer(effects));
            if (effects.VolumeDb != 0) parts.Add("volume=" + Number(effects.VolumeDb) + "dB");
            if (effects.Compressor) parts.Add(CompressorFilter);
            if (effects.Loudnorm) parts.Add(StreamMapping.LoudnormFilter + ",aresample=" + Int(StreamMapping.DefaultSampleRate));
            if (effects.Limiter) parts.Add(LimiterFilter);
        }
        var (fadeIn, fadeOut) = effects.Fades(length);
        if (fadeIn > 0) parts.Add("afade=t=in:st=0:d=" + Seconds(fadeIn));
        if (fadeOut > 0) parts.Add("afade=t=out:st=" + Seconds(length - fadeOut) + ":d=" + Seconds(fadeOut));
        return string.Join(",", parts);
    }

    /// <summary>
    /// Onizlemenin sabit zincirleri. EDL zamaninda her parcanin rengi, solmasi ve sesi
    /// <c>enable</c> penceresiyle yalniz o parcada acilir. Hizli parcada pencere hiz kadar
    /// uzar; ters parcada giris solmasi parcanin EDL sonunda cikis solmasi olur. Renk solmadan once gelir.
    /// </summary>
    public static (string? Video, string? Audio) Timed(EdlPreview preview)
    {
        var color = new List<string>();
        var video = new List<string>();
        var audio = new List<string>();
        foreach (var part in preview.Parts)
        {
            var effects = part.Clip.Effects;
            if (effects.IsNeutral) continue;

            var start = EditTime.ToSeconds(part.EdlStart);
            var end = EditTime.ToSeconds(part.EdlEnd);
            var speed = (double)part.Clip.Speed;
            var (fadeIn, fadeOut) = effects.Fades(part.Clip.TimelineLength);
            var head = Math.Min(end - start, EditTime.ToSeconds(fadeIn) * speed);
            var tail = Math.Min(end - start, EditTime.ToSeconds(fadeOut) * speed);

            var window = ":enable='" + Between(start, end) + "'";
            var tone = Color(effects);
            if (tone.Length > 0) color.AddRange(tone.Split(',').Select(f => f + window));

            if (effects.Muted) audio.Add("volume=volume=0:enable='" + Between(start, end) + "'");
            else
            {
                if (effects.Denoise) audio.Add(AudioCaptureArguments.NoiseSuppressionFilterName + "=enable='" + Between(start, end) + "'");
                audio.AddRange(Equalizer(effects).Select(f => f + window));
                if (effects.VolumeDb != 0)
                    audio.Add("volume=volume=" + Number(effects.VolumeDb) + "dB:enable='" + Between(start, end) + "'");
                if (effects.Limiter) audio.Add(LimiterFilter + window);
            }

            if (part.Clip.Reversed)
            {
                if (tail > 0) Window(video, audio, "in", start, tail);
                if (head > 0) Window(video, audio, "out", end - head, head);
            }
            else
            {
                if (head > 0) Window(video, audio, "in", start, head);
                if (tail > 0) Window(video, audio, "out", end - tail, tail);
            }
        }

        video.InsertRange(0, color);
        return (video.Count == 0 ? null : string.Join(",", video), audio.Count == 0 ? null : string.Join(",", audio));
    }

    private static void Window(List<string> video, List<string> audio, string type, double start, double length)
    {
        var fade = "=t=" + type + ":st=" + Number(start) + ":d=" + Number(length) + ":enable='" + Between(start, start + length) + "'";
        video.Add("fade" + fade);
        audio.Add("afade" + fade);
    }

    private static IEnumerable<string> Equalizer(ClipEffects effects)
    {
        if (effects.BassDb != 0) yield return Band(100, effects.BassDb);
        if (effects.MidDb != 0) yield return Band(1000, effects.MidDb);
        if (effects.TrebleDb != 0) yield return Band(8000, effects.TrebleDb);
    }

    private static string Band(int hz, double gain) => "equalizer=f=" + Int(hz) + ":t=o:w=2:g=" + Number(gain);

    private static string Between(double start, double end) => "gte(t," + Number(start) + ")*lt(t," + Number(end) + ")";
    private static string Seconds(long ticks) => Number(EditTime.ToSeconds(ticks));

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}
