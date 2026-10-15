using System.Globalization;

namespace VidShrink.Core;

public static class GifPalette
{
    public const int MaxFps = 50;

    public static string Filter(int fps, int? width)
    {
        var scale = width is { } w
            ? ",scale=" + w.ToString(CultureInfo.InvariantCulture) + ":-1:flags=lanczos"
            : string.Empty;
        return string.Concat(
            "fps=", fps.ToString(CultureInfo.InvariantCulture),
            scale,
            ",split[a][b];[a]palettegen[p];[b][p]paletteuse");
    }

    public static IReadOnlyList<string> Build(string source, string target, int fps, int? width = null)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Source path is required.", nameof(source));
        if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("Target path is required.", nameof(target));
        return new[]
        {
            "-hide_banner", "-y", "-nostdin",
            "-i", source,
            "-vf", Filter(fps, width),
            "-loop", "0",
            target
        };
    }

    private static readonly double[] FpsCaps = { double.PositiveInfinity, 15, 12, 10, 10, 8, 6, 5 };
    private static readonly double[] Scales = { 1, 1, 0.85, 0.7, 0.55, 0.45, 0.35, 0.25 };
    private static readonly int[] ColorSteps = { 256, 128, 128, 64, 64, 32, 32, 16 };

    /// <summary>Bir basamağın kabaca bayt ağırlığı: kare hızı × alan × renk biti. Basamaklar arası oran tahmini için.</summary>
    public static double Weight(GifStep step) => step.Fps * step.Width * step.Height * Math.Log2(step.Colors);

    /// <summary>
    /// GIF'i hedefe indiren basamaklar: kare hızı, genişlik ve renk sayısı birlikte düşer. İlk basamak verilen
    /// değerlerin kendisidir; her basamak öncekinden kesin hafiftir, boyutlar çifttir.
    /// </summary>
    public static IReadOnlyList<GifStep> Ladder(double fps, int width, int height)
    {
        var steps = new List<GifStep>();
        for (var i = 0; i < FpsCaps.Length; i++)
        {
            var step = new GifStep(
                Math.Min(fps, FpsCaps[i]),
                i == 0 ? width : Even(width * Scales[i]),
                i == 0 ? height : Even(height * Scales[i]),
                ColorSteps[i]);
            if (steps.Count == 0 || Weight(step) < Weight(steps[^1])) steps.Add(step);
        }
        return steps;
    }

    /// <summary>Kaynak ve plandan merdiven: planın kare hızı ve boyu varsa onlardan başlar, kare hızı <see cref="MaxFps"/> ile sınırlı.</summary>
    public static IReadOnlyList<GifStep> Ladder(MediaInfo info, ConversionPlan plan)
    {
        var fps = Math.Min(plan.Fps ?? info.Fps, MaxFps);
        if (fps <= 0) fps = 10;
        int width, height;
        if (plan.Width is { } w && plan.Height is { } h) (width, height) = (w, h);
        else if (plan.Height is { } onlyH && info.Height > 0)
            (width, height) = ((int)Math.Round(info.DisplayWidth * (double)onlyH / info.Height / 2) * 2, onlyH);
        else (width, height) = (info.DisplayWidth, info.Height);
        return Ladder(fps, width, height);
    }

    /// <summary>
    /// Ölçülen boyuttan sonraki basamak: ağırlık oranıyla tahmini hedefin altına inen ilk basamak, en az bir sonraki;
    /// hiçbiri inmiyorsa sonuncusu.
    /// </summary>
    public static int NextStep(IReadOnlyList<GifStep> steps, int current, long measuredBytes, long targetBytes)
    {
        var from = Weight(steps[current]);
        for (var i = current + 1; i < steps.Count; i++)
            if (measuredBytes * Weight(steps[i]) / from <= targetBytes) return i;
        return steps.Count - 1;
    }

    /// <summary>GIF kaynaktan GIF çıktıda hedef bayt; öteki bileşimlerde ve hedefsizken <c>null</c>.</summary>
    public static long? TargetFor(MediaInfo? info, string container, double targetMb)
        => info is not null && info.IsGifSource && container == "gif" && targetMb > 0 ? Megabayt.Tavan(targetMb) : null;

    /// <summary>
    /// Merdiveni iner: her basamakta palet ve kodlama koşar, çıktı ölçülür; hedefin altına inen ilk basamakta durur.
    /// Son basamak da sığmazsa onu teslim eder. Planın renk sayısı basamağın tavanıdır. <paramref name="run"/>'ın ikinci argümanı palet geçişinde <c>true</c>.
    /// </summary>
    public static async Task<GifFit> FitAsync(MediaInfo info, ConversionPlan plan, string outputPath, string palettePath,
        Func<IReadOnlyList<string>, bool, Task> run, Func<string, long> sizeOf, IEncoderAvailability? availability = null)
    {
        var target = plan.TargetBytes ?? long.MaxValue;
        var steps = Ladder(info, plan);
        var current = 0;
        var attempts = 0;
        while (true)
        {
            var step = steps[current];
            var stepPlan = plan with { Fps = step.Fps, Width = step.Width, Height = step.Height, GifColors = Math.Min(step.Colors, plan.GifColors ?? step.Colors) };
            await run(ConversionArguments.Build(info, stepPlan, palettePath, availability: availability), true);
            await run(ConversionArguments.Build(info, stepPlan, outputPath, palettePath, availability), false);
            attempts++;
            var bytes = sizeOf(outputPath);
            if (bytes <= target || current == steps.Count - 1) return new GifFit(step, bytes, attempts);
            current = NextStep(steps, current, bytes, target);
        }
    }

    private static int Even(double value) => Math.Max(16, (int)Math.Round(value / 2) * 2);
}

/// <summary>GIF sığdırma merdiveninin bir basamağı.</summary>
public sealed record GifStep(double Fps, int Width, int Height, int Colors);

/// <summary>Sığdırmanın sonucu: teslim edilen basamak, bayt, kodlama sayısı.</summary>
public sealed record GifFit(GifStep Step, long Bytes, int Attempts);
