using System.Globalization;
using System.Text.RegularExpressions;

namespace VidShrink.Core;

public enum DeinterlaceMode { Auto, Off, On }

public enum DenoiseFilter { Off, NlMeans, Hqdn3d }

public enum FilterStrength { Light, Medium, Strong }

public enum SharpenMode { Off, Light, Medium, Strong }

public enum TransposeMode { None, Clockwise, CounterClockwise, UpsideDown, FlipHorizontal, FlipVertical }

public enum ColorMatrixTarget { Keep, Bt709, Bt601 }

public readonly record struct CropRect(int Width, int Height, int X, int Y)
{
    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture, $"{Width}:{Height}:{X}:{Y}");
}

public readonly record struct PadBorders(int Top, int Bottom, int Left, int Right);

public readonly record struct IdetCounts(int Tff, int Bff, int Progressive, int Undetermined)
{
    public int Total => Tff + Bff + Progressive + Undetermined;

    private static readonly Regex MultiFrame = new(
        @"Multi frame detection:\s*TFF:\s*(\d+)\s*BFF:\s*(\d+)\s*Progressive:\s*(\d+)\s*Undetermined:\s*(\d+)",
        RegexOptions.CultureInvariant);

    public static IdetCounts? Parse(string standardError)
    {
        IdetCounts? last = null;
        foreach (Match m in MultiFrame.Matches(standardError ?? ""))
        {
            var counts = new IdetCounts(
                int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture));
            if (counts.Total > 0) last = counts;
        }
        return last;
    }
}

public sealed record VideoFilterOptions
{
    public static readonly VideoFilterOptions Default = new();

    public DeinterlaceMode Deinterlace { get; init; } = DeinterlaceMode.Auto;
    public bool Detelecine { get; init; }
    public DenoiseFilter Denoise { get; init; } = DenoiseFilter.Off;
    public FilterStrength DenoiseStrength { get; init; } = FilterStrength.Medium;
    public SharpenMode Sharpen { get; init; } = SharpenMode.Off;
    public bool Deblock { get; init; }
    public TransposeMode Transpose { get; init; } = TransposeMode.None;
    public PadBorders? Pad { get; init; }
    public bool Grayscale { get; init; }
    public ColorMatrixTarget ColorMatrix { get; init; } = ColorMatrixTarget.Keep;
    public bool Deband { get; init; }
    public CropRect? Crop { get; init; }

    public bool ChangesPicture =>
        Deinterlace == DeinterlaceMode.On
        || Detelecine
        || Denoise != DenoiseFilter.Off
        || Sharpen != SharpenMode.Off
        || Deblock
        || Transpose != TransposeMode.None
        || Pad is not null
        || Grayscale
        || ColorMatrix != ColorMatrixTarget.Keep
        || Deband
        || Crop is not null;

    public bool ChangesPictureFor(MediaInfo info) => ChangesPicture || VideoFilterChain.Deinterlaces(info, this);

    public VideoFilterOptions WithCrop(CropRect? detected) => this with { Crop = detected };
}

public static class VideoFilterChain
{
    public const string DeinterlaceChain = "idet,bwdif=mode=send_frame:parity=auto:deint=interlaced";
    public const string DetelecineChain = "fieldmatch,yadif=deint=interlaced,decimate";
    public const string GrayscaleFilter = "hue=s=0";
    public const string DeblockFilter = "deblock=filter=strong:block=8";
    public const string DebandFilter = "deband";

    /// <summary>
    /// Anamorfik kaynagin ciktisini kare piksele cevirir. Olcek suzgeci kaynagin SAR
    /// metadata'sini oldugu gibi tasir; sifirlanmazsa oynatici zaten gosterim genisligine
    /// olceklenmis kareyi ikinci kez esnetir.
    /// </summary>
    public const string SquarePixelFilter = "setsar=1";
    public const double DetelecineFpsFactor = 4.0 / 5.0;

    public const double IdetDominantShare = 0.25;
    public const int IdetParityDominance = 10;

    private static readonly HashSet<string> IdetProbeCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "h264", "mpeg2video", "dvvideo"
    };

    public static bool FieldOrderSaysProgressive(string? fieldOrder)
        => string.Equals(fieldOrder, "progressive", StringComparison.OrdinalIgnoreCase);

    public static bool NeedsInterlaceProbe(MediaInfo info, VideoFilterOptions options)
        => options.Deinterlace == DeinterlaceMode.Auto
        && !options.Detelecine
        && !info.IsInterlaced
        && !FieldOrderSaysProgressive(info.FieldOrder)
        && IdetProbeCodecs.Contains(info.VideoCodec);

    public static bool IdetSaysInterlaced(IdetCounts counts)
    {
        if (counts.Total <= 0) return false;
        var dominant = Math.Max(counts.Tff, counts.Bff);
        var minority = Math.Min(counts.Tff, counts.Bff);
        return dominant >= IdetDominantShare * counts.Total
            && minority * IdetParityDominance <= dominant;
    }

    public static VideoFilterOptions ResolveInterlace(VideoFilterOptions options, MediaInfo info, IdetCounts? probe)
    {
        if (options.Deinterlace != DeinterlaceMode.Auto) return options;
        if (info.IsInterlaced) return options with { Deinterlace = DeinterlaceMode.On };
        if (probe is null) return options;
        return options with { Deinterlace = IdetSaysInterlaced(probe.Value) ? DeinterlaceMode.On : DeinterlaceMode.Off };
    }

    public static bool Deinterlaces(MediaInfo info, VideoFilterOptions options)
        => !options.Detelecine && options.Deinterlace switch
        {
            DeinterlaceMode.On => true,
            DeinterlaceMode.Off => false,
            _ => info.IsInterlaced
        };

    public static MediaInfo PlannedSource(MediaInfo info, VideoFilterOptions? options)
    {
        if (options is null) return info;
        var width = info.Width;
        var height = info.Height;
        if (options.Crop is { } crop)
        {
            width = crop.Width;
            height = crop.Height;
        }
        if (options.Transpose is TransposeMode.Clockwise or TransposeMode.CounterClockwise)
            (width, height) = (height, width);
        var fps = options.Detelecine ? info.Fps * DetelecineFpsFactor : info.Fps;
        return width == info.Width && height == info.Height && fps == info.Fps
            ? info
            : info with { Width = width, Height = height, Fps = fps };
    }

    public static IReadOnlyList<string> Validate(MediaInfo info, VideoFilterOptions options)
    {
        var problems = new List<string>();
        if (options.Crop is { } c)
        {
            if (c.Width <= 0 || c.Height <= 0) problems.Add("crop: size must be positive");
            if (c.Width % 2 != 0 || c.Height % 2 != 0) problems.Add("crop: size must be even");
            if (c.X < 0 || c.Y < 0 || c.X + c.Width > info.Width || c.Y + c.Height > info.Height)
                problems.Add("crop: rectangle leaves the source frame");
        }
        if (options.Pad is { } p)
        {
            if (p.Top < 0 || p.Bottom < 0 || p.Left < 0 || p.Right < 0) problems.Add("pad: borders must not be negative");
            if ((p.Top + p.Bottom) % 2 != 0 || (p.Left + p.Right) % 2 != 0) problems.Add("pad: added size must be even");
        }
        return problems;
    }

    public static IReadOnlyList<string> Filters(MediaInfo info, EncodePlan plan)
    {
        var options = plan.Filters ?? VideoFilterOptions.Default;
        var source = PlannedSource(info, options);
        var filters = new List<string>();

        if (options.Detelecine) filters.Add(DetelecineChain);
        if (Deinterlaces(info, options)) filters.Add(DeinterlaceChain);
        if (options.Crop is { } crop) filters.Add($"crop={crop}");
        if (options.Deblock) filters.Add(DeblockFilter);
        if (DenoiseText(options) is string denoise) filters.Add(denoise);
        if (options.Deband) filters.Add(DebandFilter);
        filters.AddRange(TransposeFilters(options.Transpose));
        if (plan.Width != source.Width || plan.Height != source.Height)
            filters.Add($"scale={plan.Width}:{plan.Height}:flags=lanczos");
        if (info.IsAnamorphic) filters.Add(SquarePixelFilter);
        if (SharpenText(options.Sharpen) is string sharpen) filters.Add(sharpen);
        if (!string.IsNullOrEmpty(plan.HdrVideoFilter))
            filters.Add(plan.HdrVideoFilter);
        else if (ColorMatrixFilter(info, options.ColorMatrix) is string matrix)
            filters.Add(matrix);
        if (options.Grayscale) filters.Add(GrayscaleFilter);
        if (options.Pad is { } pad)
            filters.Add(string.Create(CultureInfo.InvariantCulture,
                $"pad=w=iw+{pad.Left + pad.Right}:h=ih+{pad.Top + pad.Bottom}:x={pad.Left}:y={pad.Top}:color=black"));
        if (plan.Fps < source.Fps - 0.01)
            filters.Add($"fps={plan.Fps.ToString("0.###", CultureInfo.InvariantCulture)}");
        return filters;
    }

    public static IReadOnlyList<string> ColorArgs(EncodePlan plan)
    {
        if (!string.IsNullOrEmpty(plan.HdrVideoFilter) || plan.HdrColorArgs.Count > 0) return Array.Empty<string>();
        return (plan.Filters?.ColorMatrix ?? ColorMatrixTarget.Keep) switch
        {
            ColorMatrixTarget.Bt709 => new[] { "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709" },
            ColorMatrixTarget.Bt601 => new[] { "-colorspace", "smpte170m", "-color_primaries", "smpte170m", "-color_trc", "smpte170m" },
            _ => Array.Empty<string>()
        };
    }

    public static string? DenoiseText(VideoFilterOptions options) => options.Denoise switch
    {
        DenoiseFilter.NlMeans => options.DenoiseStrength switch
        {
            FilterStrength.Light => "nlmeans=s=1.0:p=7:r=9",
            FilterStrength.Medium => "nlmeans=s=2.0:p=7:r=9",
            FilterStrength.Strong => "nlmeans=s=4.0:p=7:r=15",
            _ => null
        },
        DenoiseFilter.Hqdn3d => options.DenoiseStrength switch
        {
            FilterStrength.Light => "hqdn3d=2:1:2:3",
            FilterStrength.Medium => "hqdn3d=3:2:2:3",
            FilterStrength.Strong => "hqdn3d=7:7:5:5",
            _ => null
        },
        _ => null
    };

    public static string? SharpenText(SharpenMode mode) => mode switch
    {
        SharpenMode.Light => "unsharp=5:5:0.5:3:3:0.0",
        SharpenMode.Medium => "unsharp=5:5:1.0:3:3:0.0",
        SharpenMode.Strong => "unsharp=5:5:1.5:3:3:0.0",
        _ => null
    };

    public static IReadOnlyList<string> TransposeFilters(TransposeMode mode) => mode switch
    {
        TransposeMode.Clockwise => new[] { "transpose=clock" },
        TransposeMode.CounterClockwise => new[] { "transpose=cclock" },
        TransposeMode.UpsideDown => new[] { "hflip", "vflip" },
        TransposeMode.FlipHorizontal => new[] { "hflip" },
        TransposeMode.FlipVertical => new[] { "vflip" },
        _ => Array.Empty<string>()
    };

    public static string? ColorMatrixFilter(MediaInfo info, ColorMatrixTarget target)
    {
        if (target == ColorMatrixTarget.Keep) return null;
        var source = SourceMatrix(info);
        var wanted = target == ColorMatrixTarget.Bt709 ? "709" : "170m";
        if (source.Matrix == wanted) return null;
        return target == ColorMatrixTarget.Bt709
            ? $"zscale=min={source.Matrix}:tin={source.Transfer}:pin={source.Primaries}:m=709:t=709:p=709"
            : $"zscale=min={source.Matrix}:tin={source.Transfer}:pin={source.Primaries}:m=170m:t=601:p=170m";
    }

    private static (string Matrix, string Transfer, string Primaries) SourceMatrix(MediaInfo info)
        => info.ColorSpace?.ToLowerInvariant() switch
        {
            "bt709" => ("709", "709", "709"),
            "smpte170m" => ("170m", "601", "170m"),
            "bt470bg" => ("470bg", "601", "470bg"),
            _ => info.Height >= 720 ? ("709", "709", "709") : ("170m", "601", "170m")
        };

    public static VideoFilterOptions Parse(string? spec)
    {
        var options = VideoFilterOptions.Default;
        if (string.IsNullOrWhiteSpace(spec)) return options;

        foreach (var raw in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = raw.IndexOf('=');
            var key = (eq < 0 ? raw : raw[..eq]).ToLowerInvariant();
            var value = eq < 0 ? "" : raw[(eq + 1)..].ToLowerInvariant();
            options = key switch
            {
                "deinterlace" => options with { Deinterlace = value switch
                {
                    "auto" => DeinterlaceMode.Auto,
                    "on" => DeinterlaceMode.On,
                    "off" => DeinterlaceMode.Off,
                    _ => throw Bad(raw)
                } },
                "detelecine" => options with { Detelecine = true },
                "denoise" => ParseDenoise(options, value, raw),
                "sharpen" => options with { Sharpen = value switch
                {
                    "light" => SharpenMode.Light,
                    "medium" => SharpenMode.Medium,
                    "strong" => SharpenMode.Strong,
                    _ => throw Bad(raw)
                } },
                "deblock" => options with { Deblock = true },
                "deband" => options with { Deband = true },
                "gray" => options with { Grayscale = true },
                "rotate" => options with { Transpose = value switch
                {
                    "clock" => TransposeMode.Clockwise,
                    "cclock" => TransposeMode.CounterClockwise,
                    "180" => TransposeMode.UpsideDown,
                    "hflip" => TransposeMode.FlipHorizontal,
                    "vflip" => TransposeMode.FlipVertical,
                    _ => throw Bad(raw)
                } },
                "pad" => ParsePad(options, value, raw),
                "colorspace" => options with { ColorMatrix = value switch
                {
                    "bt709" => ColorMatrixTarget.Bt709,
                    "bt601" => ColorMatrixTarget.Bt601,
                    _ => throw Bad(raw)
                } },
                "crop" => ParseCrop(options, value, raw),
                _ => throw Bad(raw)
            };
        }
        return options;
    }

    private static VideoFilterOptions ParseDenoise(VideoFilterOptions options, string value, string raw)
    {
        var parts = value.Split(':');
        var filter = parts[0] switch
        {
            "nlmeans" => DenoiseFilter.NlMeans,
            "hqdn3d" => DenoiseFilter.Hqdn3d,
            _ => throw Bad(raw)
        };
        var strength = parts.Length < 2 ? FilterStrength.Medium : parts[1] switch
        {
            "light" => FilterStrength.Light,
            "medium" => FilterStrength.Medium,
            "strong" => FilterStrength.Strong,
            _ => throw Bad(raw)
        };
        if (parts.Length > 2) throw Bad(raw);
        return options with { Denoise = filter, DenoiseStrength = strength };
    }

    private static VideoFilterOptions ParsePad(VideoFilterOptions options, string value, string raw)
    {
        var n = Numbers(value, 4, raw);
        return options with { Pad = new PadBorders(n[0], n[1], n[2], n[3]) };
    }

    private static VideoFilterOptions ParseCrop(VideoFilterOptions options, string value, string raw)
    {
        var n = Numbers(value, 4, raw);
        return options with { Crop = new CropRect(n[0], n[1], n[2], n[3]) };
    }

    private static int[] Numbers(string value, int count, string raw)
    {
        var parts = value.Split(':');
        if (parts.Length != count) throw Bad(raw);
        var numbers = new int[count];
        for (var i = 0; i < count; i++)
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[i])) throw Bad(raw);
        return numbers;
    }

    private static ArgumentException Bad(string token) => new($"unknown filter option: {token}");
}
