using System.Globalization;

namespace VidShrink.Core;

/// <summary>
/// Sesten video (ARC-20): yalnız ses kaynağı MP4, MKV, MOV ya da WebM'e görüntüyle yazılır. Görüntü ya kapaktır
/// (seçilen kapak dosyası <c>-loop 1</c> ile, yoksa sesin gömülü kapağı <c>loop</c> süzgeciyle; gömülü kapak ikinci
/// girdiden okunur çünkü <c>-ss</c> tek karelik kapak akışını atıyor) ya da <c>showwaves</c> dalga biçimidir.
/// Çıktının süresi <c>-t</c> ile sesin (kırpılmış) süresine bağlanır.
/// </summary>
public static partial class ConversionArguments
{
    /// <summary>Sesten video için varsayılan boyut; plan genişlik ya da yükseklik vermezse.</summary>
    public const int AudioVideoWidth = 1280, AudioVideoHeight = 720;

    /// <summary>Sesten video için varsayılan kare hızı.</summary>
    public const double AudioVideoFps = 25;

    /// <summary>Dalga biçiminin çizgi rengi; görüntünün içeriğidir, arayüz rengi değil.</summary>
    public const string WaveformColor = "white";

    private static readonly string[] AudioVideoContainers = { "mp4", "mkv", "mov", "webm" };

    /// <summary>Plan yalnız ses kaynağından video mu yazıyor: kaynak yalnız ses, görüntü seçili, kap video kabı.</summary>
    public static bool MakesVideoFromAudio(MediaInfo info, ConversionPlan plan)
        => info.IsAudioOnly && plan.AudioVisual != AudioVisual.None && AudioVideoContainers.Contains(plan.Container);

    /// <summary>Görüntü seçimine göre kap kaynağa uyar mı: görüntü seçiliyken ses kaynağına video kapları da açılır.</summary>
    public static bool ContainerFitsSource(string container, MediaInfo? source, AudioVisual visual)
        => ContainerFitsSource(container, source)
           || source is { IsAudioOnly: true } && visual != AudioVisual.None && AudioVideoContainers.Contains(container);

    /// <summary>Kapak görüntüsünün girdisi: seçilen kapak dosyası, yoksa sesin gömülü kapağı; ikisi de yoksa <c>null</c>.</summary>
    public static string? AudioVideoCoverSource(MediaInfo info, ConversionPlan plan)
        => !string.IsNullOrWhiteSpace(plan.CoverPath) ? plan.CoverPath
            : info.Streams.Any(stream => stream.IsAttachedPicture) ? info.FilePath : null;

    /// <summary>Çıktının beklenen süresi, saniye: sesin süresi, kırpma varsa kırpılan aralık.</summary>
    public static double AudioVideoSeconds(MediaInfo info, ConversionPlan plan)
        => Math.Max(0, Math.Min(plan.End?.TotalSeconds ?? info.DurationSeconds, info.DurationSeconds) - (plan.Start?.TotalSeconds ?? 0));

    /// <summary>Çıktı boyutu: iki boyut verilmişse o, yalnız yükseklik verilmişse 16:9 genişlik, hiçbiri yoksa 1280x720.</summary>
    public static (int Width, int Height) AudioVideoSize(ConversionPlan plan)
    {
        if (plan.Width is { } width && plan.Height is { } height) return (width, height);
        if (plan.Height is { } onlyHeight) return ((int)Math.Round(onlyHeight * 16 / 9.0 / 2) * 2, onlyHeight);
        return (AudioVideoWidth, AudioVideoHeight);
    }

    private static IReadOnlyList<string> AudioVideoArgs(MediaInfo info, ConversionPlan plan, string outputPath)
    {
        var a = new List<string> { "-hide_banner", "-y" };
        a.AddRange(CodecModel.DeviceArgs(plan.VideoCodec));
        if (plan.Start is { } start) a.AddRange(new[] { "-ss", FormatTime(start) });
        a.AddRange(new[] { "-i", info.FilePath });
        var (width, height) = AudioVideoSize(plan);
        var fps = (plan.Fps ?? AudioVideoFps).ToString("0.###", CultureInfo.InvariantCulture);
        var size = width.ToString(CultureInfo.InvariantCulture) + ":" + height.ToString(CultureInfo.InvariantCulture);
        var audio = AutoAudio(info);
        var audioSpec = "0:" + (audio?.Index.ToString(CultureInfo.InvariantCulture) ?? "a:0");
        var pixelFormat = CodecModel.OutputPixelFormat(plan.VideoCodec, "yuv420p");
        var tail = new List<string>();
        tail.AddRange(CodecModel.UploadFilters(plan.VideoCodec, pixelFormat));
        string graph;
        if (plan.AudioVisual == AudioVisual.Waveform)
        {
            graph = $"[{audioSpec}]showwaves=s={width}x{height}:mode=cline:rate={fps}:colors={WaveformColor},format=yuv420p";
        }
        else
        {
            string picture;
            if (!string.IsNullOrWhiteSpace(plan.CoverPath))
            {
                a.AddRange(new[] { "-loop", "1", "-i", plan.CoverPath! });
                picture = "[1:v:0]";
            }
            else
            {
                var cover = info.Streams.First(stream => stream.IsAttachedPicture);
                a.AddRange(new[] { "-i", info.FilePath });
                picture = "[1:" + cover.Index.ToString(CultureInfo.InvariantCulture) + "]loop=loop=-1:size=1:start=0,";
            }
            graph = picture + $"fps={fps},scale={size}:force_original_aspect_ratio=decrease,pad={size}:(ow-iw)/2:(oh-ih)/2,setsar=1,format=yuv420p";
        }
        if (tail.Count > 0) graph += "," + string.Join(',', tail);
        a.AddRange(new[] { "-filter_complex", graph + "[v]", "-map", "[v]", "-map", audioSpec });
        a.AddRange(new[] { "-t", FormatTime(TimeSpan.FromSeconds(AudioVideoSeconds(info, plan))) });
        a.AddRange(new[] { "-c:v", plan.VideoCodec });
        a.AddRange(FfmpegArguments.SpeedArgs(plan.VideoCodec, FfmpegArguments.DefaultPreset(plan.VideoCodec)));
        a.AddRange(plan.QualityMode == ConversionQualityMode.Crf
            ? CodecModel.QualityArgs(plan.VideoCodec, plan.Crf)
            : new[] { "-b:v", $"{plan.VideoBitrateK}k" });
        if (CodecModel.TakesPixelFormatFlag(plan.VideoCodec)) a.AddRange(new[] { "-pix_fmt", pixelFormat });
        AddAudio(a, info, plan);
        if (plan.Container is "mp4" or "mov") a.AddRange(new[] { "-movflags", "+faststart" });
        a.AddRange(MetadataArgs(info, plan));
        a.AddRange(TagArgs(plan));
        a.AddRange(plan.ExtraArgs);
        a.Add(outputPath);
        return a;
    }
}
