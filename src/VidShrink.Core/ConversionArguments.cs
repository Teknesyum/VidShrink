using System.Globalization;

namespace VidShrink.Core;

public static class ConversionArguments
{
    public const string AnimatedWebpEncoder = "libwebp_anim";
    public const string AnimatedAvifEncoder = "libsvtav1";

    public static IReadOnlyList<string> Validate(MediaInfo info, ConversionPlan plan)
    {
        var errors = new List<string>();
        if (plan.Start is { } startValue && startValue < TimeSpan.Zero) errors.Add("Start time cannot be negative.");
        if (plan.End is { } endValue && endValue <= TimeSpan.Zero) errors.Add("End time must be greater than zero.");
        if (plan.End is { } end && plan.Start is { } start && end <= start) errors.Add("End time must be after start time.");
        if (plan.Start is { } sourceEnd && sourceEnd.TotalSeconds >= info.DurationSeconds) errors.Add("Start time must be before the end of the source.");
        if (plan.Width is <= 0 || plan.Height is <= 0) errors.Add("Resolution dimensions must be positive.");
        if (plan.Width is { } width && width % 2 != 0 || plan.Height is { } height && height % 2 != 0) errors.Add("Resolution dimensions must be even for the selected pixel format.");
        if (plan.Fps is <= 0) errors.Add("Frame rate must be greater than zero.");
        if (plan.VideoCodec == "copy" && (plan.Height is not null || plan.Width is not null || plan.Fps is not null)) errors.Add("Stream copy cannot change resolution or frame rate.");
        if (plan.VideoCodec == "copy" && plan.Container == "gif") errors.Add("GIF requires video encoding and cannot use stream copy.");
        if (plan.VideoCodec == "copy" && plan.ImageSequenceOutput) errors.Add("Image sequence output requires video encoding and cannot use stream copy.");
        if (plan.CarriesAudio && plan.AudioCodec == "copy" && !info.HasAudio) errors.Add("The source has no audio stream to copy.");
        if (plan.AudioOnly && !info.HasAudio) errors.Add("The source has no audio stream to extract.");
        if (info.IsAudioOnly && !plan.AudioOnly) errors.Add("The source has no video stream; choose an audio container.");

        if (!plan.AudioOnly && !plan.Gif && !plan.AnimatedImage && !plan.ImageSequenceOutput && plan.VideoCodec != "copy" && !VideoEncodeCompatible(plan.Container, plan.VideoCodec))
            errors.Add($"The {plan.Container.ToUpperInvariant()} container does not support the selected {plan.VideoCodec} video encoder.");
        if (plan.CarriesAudio && plan.AudioCodec is { } audioCodec && audioCodec != "copy" && !AudioEncodeCompatible(plan.Container, audioCodec))
            errors.Add($"The {plan.Container.ToUpperInvariant()} container does not support the selected {audioCodec} audio encoder.");

        var source = info.VideoCodec.ToLowerInvariant();
        if (plan.VideoCodec == "copy" && !plan.ImageSequenceOutput && !VideoCopyCompatible(plan.Container, source))
            errors.Add($"The {plan.Container.ToUpperInvariant()} container does not support copying the source {source} video stream.");
        if (plan.Start is { } trimStart && plan.End is { } trimEnd && trimEnd <= trimStart)
            errors.Add("The trim end must come after the trim start.");
        if (plan.CarriesAudio && plan.AudioCodec == "copy" && !AudioCopyCompatible(plan.Container, info.AudioCodec))
            errors.Add($"The {plan.Container.ToUpperInvariant()} container does not support copying the source {info.AudioCodec} audio stream.");
        return errors;
    }

    /// <summary>
    /// Kabın istediği kodlayıcı; yalnız seçilen video kodeğinden bağımsız, sabit kodlayıcıyla yazılan
    /// kaplar için ad döner. Öteki kaplarda <c>null</c>.
    /// </summary>
    public static string? RequiredEncoder(string container) => container switch
    {
        "webp" => AnimatedWebpEncoder,
        "avif" => AnimatedAvifEncoder,
        _ => null
    };

    /// <summary>
    /// Kap bu ffmpeg derlemesinde yazılabiliyor mu. Yoklama henüz yoksa (<c>null</c>) kap sunulur:
    /// bilinmeyen "yok" sayılmaz.
    /// </summary>
    public static bool ContainerAvailable(string container, IEncoderAvailability? encoders)
        => encoders is null || RequiredEncoder(container) is not { } encoder || encoders.HasEncoder(encoder);

    /// <summary>
    /// Planın sessizce yok saydığı seçimler. Ölçüm <c>docs/olcumler/webp-bit-hizi.md</c>:
    /// <c>libwebp_anim</c> <c>-b:v</c> değerini okumuyor, çıktı bayt bayt aynı kalıyor.
    /// </summary>
    public static IReadOnlyList<ConversionNote> Notes(ConversionPlan plan)
    {
        var notes = new List<ConversionNote>();
        if (plan.Container == "webp" && plan.QualityMode == ConversionQualityMode.Bitrate)
            notes.Add(ConversionNote.WebpBitrateIgnored);
        if (plan.Gif && plan.TargetBytes > 0)
            notes.Add(ConversionNote.GifFitsTarget);
        return notes;
    }

    /// <summary>Ses-yalnız kaynak yalnız ses kabına çevrilir; görüntülü kaynakta her kap uyar.</summary>
    public static bool ContainerFitsSource(string container, MediaInfo? source)
        => source is not { IsAudioOnly: true } || new ConversionPlan { Container = container }.AudioOnly;

    public static IReadOnlyList<string> Build(MediaInfo info, ConversionPlan plan, string outputPath, string? palettePath = null, IEncoderAvailability? availability = null)
    {
        var errors = Validate(info, plan);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        if (WritesIntermediate(plan) && IntermediateCodecs.Find(plan.VideoCodec, plan.VideoProfile) is null)
            throw new ArgumentException($"Unsupported {plan.VideoCodec} profile: {plan.VideoProfile ?? "none"}", nameof(plan));
        var a = new List<string> { "-hide_banner", "-y" };
        var encodesVideo = !plan.AudioOnly && !plan.Gif && !plan.AnimatedImage && !plan.ImageSequenceOutput && plan.VideoCodec != "copy";
        if (encodesVideo) a.AddRange(CodecModel.DeviceArgs(plan.VideoCodec));
        if (plan.Start is { } start) a.AddRange(new[] { "-ss", FormatTime(start) });
        a.AddRange(info.Sequence is { } sequence ? sequence.InputArgs(info.Fps) : new[] { "-i", info.FilePath });
        var subtitles = MapsStreams(info, plan) ? Subtitles(info, plan) : null;
        foreach (var track in subtitles ?? new List<SubtitleTrack>())
        {
            if (track.InputPath is null) continue;
            if (plan.Start is { } subtitleStart) a.AddRange(new[] { "-ss", FormatTime(subtitleStart) });
            a.AddRange(new[] { "-i", track.InputPath });
        }
        if (plan.End is { } end)
        {
            var trimSeconds = end.TotalSeconds - (plan.Start?.TotalSeconds ?? 0);
            a.AddRange(new[] { "-t", FormatTime(TimeSpan.FromSeconds(trimSeconds)) });
        }
        if (subtitles is not null) a.AddRange(MapArguments(info, plan, subtitles));

        if (plan.AudioOnly)
        {
            a.Add("-vn");
            AddAudio(a, plan);
            a.AddRange(MetadataArgs(info, plan));
            a.Add(outputPath);
            return a;
        }

        var filters = VideoFilters(info, plan);
        var dolbyVision = false;
        if (plan.Gif)
        {
            if (palettePath is null)
                a.AddRange(new[] { "-vf", string.Join(',', filters.Append(plan.GifColors is { } colors ? $"palettegen=stats_mode=diff:max_colors={colors}" : "palettegen=stats_mode=diff")), palettePath = outputPath });
            else
            {
                var graph = filters.Count == 0
                    ? "[0:v][1:v]paletteuse=dither=sierra2_4a"
                    : $"{string.Join(',', filters)}[x];[x][1:v]paletteuse=dither=sierra2_4a";
                a.AddRange(new[] { "-i", palettePath, "-lavfi", graph });
                a.Add(outputPath);
            }
            return a;
        }

        if (plan.AnimatedImage)
        {
            if (filters.Count > 0) a.AddRange(new[] { "-vf", string.Join(',', filters) });
            a.AddRange(AnimatedImageArgs(plan));
            a.AddRange(MetadataArgs(info, plan));
            a.Add(outputPath);
            return a;
        }

        if (plan.ImageSequenceOutput)
        {
            if (filters.Count > 0) a.AddRange(new[] { "-vf", string.Join(',', filters) });
            a.AddRange(ImageOutputArgs(plan));
            a.Add(outputPath);
            return a;
        }

        if (plan.VideoCodec == "copy")
        {
            if (filters.Count > 0) a.AddRange(new[] { "-vf", string.Join(',', filters) });
            a.AddRange(new[] { "-c:v", "copy" });
        }
        else
        {
            var hdr = HdrResolver.Resolve(info, plan.HdrPolicy, plan.VideoCodec, availability,
                dolbyVisionCarriable: FfmpegArguments.SupportsRateLimits(plan.VideoCodec) is false);
            dolbyVision = hdr.DolbyVisionCarried;
            if (!string.IsNullOrEmpty(hdr.VideoFilter)) filters.Add(hdr.VideoFilter);
            var pixelFormat = CodecModel.OutputPixelFormat(plan.VideoCodec, hdr.PixelFormat);
            filters.AddRange(CodecModel.UploadFilters(plan.VideoCodec, pixelFormat));
            if (filters.Count > 0) a.AddRange(new[] { "-vf", string.Join(',', filters) });

            a.AddRange(new[] { "-c:v", plan.VideoCodec });
            if (IntermediateCodecs.Find(plan.VideoCodec, plan.VideoProfile) is { } intermediate)
                a.AddRange(new[] { "-profile:v", intermediate.Profile, "-pix_fmt", intermediate.PixelFormat });
            else
            {
                a.AddRange(FfmpegArguments.SpeedArgs(plan.VideoCodec, FfmpegArguments.DefaultPreset(plan.VideoCodec)));
                a.AddRange(plan.QualityMode == ConversionQualityMode.Crf
                    ? CodecModel.QualityArgs(plan.VideoCodec, plan.Crf)
                    : new[] { "-b:v", $"{plan.VideoBitrateK}k" });
                if (CodecModel.TakesPixelFormatFlag(plan.VideoCodec))
                    a.AddRange(new[] { "-pix_fmt", pixelFormat });
            }
            if (hdr.ColorArgs.Count > 0) a.AddRange(hdr.ColorArgs);
            if (dolbyVision) a.AddRange(HdrResolver.DolbyVisionArgs);
        }
        AddAudio(a, plan);
        if (subtitles is not null)
        {
            a.AddRange(StreamPlan.SubtitleCodecArguments(subtitles));
            a.AddRange(StreamPlan.SubtitleTagArguments(plan.DropMetadata
                ? subtitles.Select(track => track with { Title = null }).ToList()
                : subtitles));
        }
        if (plan.Container is "mp4" or "mov" or "m4a") a.AddRange(new[] { "-movflags", "+faststart" });
        if (dolbyVision && plan.Container is "mp4" or "mov") a.AddRange(HdrResolver.Mp4DolbyVisionArgs);
        a.AddRange(MetadataArgs(info, plan, subtitles));
        a.Add(outputPath);
        return a;
    }

    /// <summary>
    /// Hareketli WebP ve AVIF: ses düşer, döngü sonsuzdur. Kalite, seçili video kodeğinin CRF
    /// aralığındaki konumdan çevrilir: WebP'de 100 (en iyi) ile 0 arası <c>-quality</c>, AVIF'te
    /// SVT-AV1'in kendi CRF aralığı. WebP'nin bit hızı kipi yok; o kipte libwebp varsayılanı kalır ve <see cref="Notes"/> bunu söyler.
    /// </summary>
    private static IReadOnlyList<string> AnimatedImageArgs(ConversionPlan plan)
    {
        var a = new List<string>();
        var (min, max) = CodecModel.CrfRange(plan.VideoCodec);
        var share = Math.Clamp((plan.Crf - min) / (double)(max - min), 0, 1);
        if (plan.Container == "webp")
        {
            a.AddRange(new[] { "-c:v", AnimatedWebpEncoder });
            if (plan.QualityMode == ConversionQualityMode.Crf)
                a.AddRange(new[] { "-quality", Math.Round(100 - share * 100).ToString("0", CultureInfo.InvariantCulture) });
        }
        else
        {
            var (avifMin, avifMax) = CodecModel.CrfRange(AnimatedAvifEncoder);
            a.AddRange(new[] { "-c:v", AnimatedAvifEncoder });
            a.AddRange(FfmpegArguments.SpeedArgs(AnimatedAvifEncoder, FfmpegArguments.DefaultPreset(AnimatedAvifEncoder)));
            a.AddRange(plan.QualityMode == ConversionQualityMode.Crf
                ? CodecModel.QualityArgs(AnimatedAvifEncoder, Math.Round(avifMin + share * (avifMax - avifMin)))
                : new[] { "-b:v", $"{plan.VideoBitrateK}k" });
            a.AddRange(new[] { "-pix_fmt", "yuv420p" });
        }
        a.AddRange(new[] { "-an", "-loop", "0", "-f", plan.Container });
        return a;
    }

    /// <summary>
    /// PNG ya da JPEG dizisi: ses düşer, kareler <c>image2</c> ile 1'den numaralanır. Tek karede yalnız ilk kare
    /// numarasız tek dosyaya yazılır. JPEG kalitesi sabit <c>-q:v 2</c>; kalite kutuları okunmaz.
    /// </summary>
    private static IReadOnlyList<string> ImageOutputArgs(ConversionPlan plan)
    {
        var a = new List<string>();
        a.AddRange(plan.Container == "jpg" ? new[] { "-c:v", "mjpeg", "-q:v", "2" } : new[] { "-c:v", "png" });
        a.Add("-an");
        a.AddRange(plan.SingleFrame
            ? new[] { "-frames:v", "1", "-update", "1" }
            : new[] { "-f", "image2", "-start_number", "1" });
        return a;
    }

    /// <summary>Resim dizisi çıktısının <c>image2</c> deseni: <c>klasör/ad_%05d.uzantı</c>. Addaki <c>%</c> kaçırılır.</summary>
    public static string SequenceOutputPattern(string folder, string stem, string container)
        => Path.Combine(folder, ImageSequence.Escape(stem) + "_%05d." + container);

    /// <summary>
    /// Meta silme argümanları küçültmeyle aynı kaynaktan (<see cref="StreamMapping.MetadataArguments"/>).
    /// Çıktıya giren ses ffmpeg'in seçeceği izdir (<see cref="AutoAudio"/>) ve dili ondan yazılır.
    /// Altyazılar açık eşlendiğinde (<paramref name="mapped"/>) dil her kaynak ize kendi sırasıyla yazılır;
    /// eşlenmeyen planda yalnız kaynakta tek altyazı izi varken yazılır. Kapalıyken hiçbir argüman eklenmez.
    /// </summary>
    private static IReadOnlyList<string> MetadataArgs(MediaInfo info, ConversionPlan plan, IReadOnlyList<SubtitleTrack>? mapped = null)
    {
        if (!plan.DropMetadata) return Array.Empty<string>();
        var audio = plan.CarriesAudio && plan.AudioCodec is not null
            ? new[] { AutoAudio(info)?.Language }
            : Array.Empty<string?>();
        if (mapped is not null)
            return StreamMapping.MetadataArguments(true, audio, mapped.Select(track => track.InputPath is null ? track.Language : null).ToArray());
        var subtitles = info.Streams.Where(stream => stream.Kind == StreamKind.Subtitle).ToArray();
        var subtitle = !plan.AudioOnly && !plan.AnimatedImage && subtitles.Length == 1
            ? new[] { subtitles[0].Language }
            : Array.Empty<string?>();
        return StreamMapping.MetadataArguments(true, audio, subtitle);
    }

    /// <summary>
    /// Altyazi tasiyabilen cikti kabi; ses, GIF, hareketli gorsel, AVI ve MXF'te <c>null</c>.
    /// </summary>
    public static OutputContainer? SubtitleContainer(ConversionPlan plan)
    {
        if (plan.AudioOnly || plan.Gif || plan.AnimatedImage) return null;
        return plan.Container switch
        {
            "mp4" => OutputContainer.Mp4,
            "mov" => OutputContainer.Mov,
            "mkv" => OutputContainer.Mkv,
            "webm" => OutputContainer.WebM,
            _ => null
        };
    }

    /// <summary>
    /// Komut akislari acik <c>-map</c> ile mi secer: altyazi tasiyan kapta kaynakta altyazi izi ya da
    /// eklenmis dis dosya varken. Oteki planlarda komut eskisi gibi ffmpeg'in secimine birakilir.
    /// </summary>
    public static bool MapsStreams(MediaInfo info, ConversionPlan plan)
        => SubtitleContainer(plan) is not null
           && (plan.ExternalSubtitles.Count > 0 || info.Streams.Any(stream => stream.Kind == StreamKind.Subtitle));

    /// <summary>
    /// Ciktiya giden altyazi izleri, kucultmenin akis kuralindan (<see cref="StreamMapping.SubtitleTracks"/>):
    /// kabin tasiyamadigi iz cevrilir ya da duser ve <paramref name="notes"/>'a yazilir.
    /// </summary>
    public static List<SubtitleTrack> Subtitles(MediaInfo info, ConversionPlan plan, List<StreamNote>? notes = null)
    {
        if (SubtitleContainer(plan) is not { } container) return new List<SubtitleTrack>();
        var request = new StreamRequest(ExternalSubtitles: plan.ExternalSubtitles);
        return StreamMapping.SubtitleTracks(info, request, container, 0, notes ?? new List<StreamNote>(),
            AutoAudio(info)?.Language, plan.SubtitleTracks);
    }

    /// <summary>
    /// Acik akis secimi: ilk video, ffmpeg'in <c>-map</c>'siz sececegi ses (<see cref="AutoAudio"/>), sonra altyazilar.
    /// Ses secimi esleme yazilinca degismez.
    /// </summary>
    private static IReadOnlyList<string> MapArguments(MediaInfo info, ConversionPlan plan, IReadOnlyList<SubtitleTrack> subtitles)
    {
        var a = new List<string>();
        var video = info.Streams.FirstOrDefault(stream => stream.Kind == StreamKind.Video && !stream.IsAttachedPicture);
        a.AddRange(new[] { "-map", video is null ? "0:v:0" : "0:" + video.Index.ToString(CultureInfo.InvariantCulture) });
        if (plan.CarriesAudio && plan.AudioCodec is not null)
        {
            if (AutoAudio(info) is { } audio) a.AddRange(new[] { "-map", "0:" + audio.Index.ToString(CultureInfo.InvariantCulture) });
            else if (info.HasAudio) a.AddRange(new[] { "-map", "0:a:0?" });
        }
        foreach (var track in subtitles) a.AddRange(new[] { "-map", track.Map });
        return a;
    }

    /// <summary>
    /// <c>-map</c> verilmeyince ffmpeg'in seçtiği ses izi: varsayılan işaretli iz, yoksa en çok kanallı,
    /// eşitlikte ilk. Ölçüm <c>docs/olcumler/meta-sil-donustur-duzenleyici.md</c>.
    /// </summary>
    public static SourceStream? AutoAudio(MediaInfo info)
    {
        SourceStream? best = null;
        long bestScore = -1;
        foreach (var stream in info.Streams)
        {
            if (stream.Kind != StreamKind.Audio) continue;
            var score = stream.Channels + (stream.IsDefault ? 5_000_000L : 0);
            if (score <= bestScore) continue;
            best = stream;
            bestScore = score;
        }

        return best;
    }

    private static List<string> VideoFilters(MediaInfo info, ConversionPlan plan)
    {
        var filters = new List<string>();
        if (plan.Width is { } width && plan.Height is { } height) filters.Add($"scale={width}:{height}:flags=lanczos");
        else if (plan.Height is { } h && info.IsAnamorphic) filters.Add($"scale=trunc(iw*sar*{h}/ih/2)*2:{h}:flags=lanczos");
        else if (plan.Height is { } h2) filters.Add($"scale=-2:{h2}:flags=lanczos");
        else if (info.IsAnamorphic) filters.Add("scale=trunc(iw*sar/2)*2:ih:flags=lanczos");
        if (info.IsAnamorphic) filters.Add(VideoFilterChain.SquarePixelFilter);
        if (plan.Fps is { } fps && Math.Abs(fps - info.Fps) > 0.01) filters.Add($"fps={fps.ToString("0.###", CultureInfo.InvariantCulture)}");
        return filters;
    }

    private static void AddAudio(List<string> args, ConversionPlan plan)
    {
        if (plan.AudioCodec is null) args.Add("-an");
        else if (plan.AudioCodec == "copy") args.AddRange(new[] { "-c:a", "copy" });
        else args.AddRange(new[] { "-c:a", plan.AudioCodec, "-b:a", $"{plan.AudioBitrateK}k" });
        if (plan.Container == "mxf" && plan.AudioCodec is not null) args.AddRange(MxfAudioArgs);
    }

    /// <summary>
    /// MXF yazicisi yalniz 48 kHz ses kabul eder: 44,1 kHz kaynakta "only 48khz is implemented"
    /// diyerek basligi yazamiyor (olcum <c>docs/olcumler/k12-ara-kodekler.md</c>).
    /// </summary>
    public static readonly IReadOnlyList<string> MxfAudioArgs = new[] { "-ar", "48000" };

    /// <summary>Plan bir ara kodekle video yaziyor mu; ses, GIF ve hareketli gorsel kaplarinda kodek okunmaz.</summary>
    private static bool WritesIntermediate(ConversionPlan plan)
        => !plan.AudioOnly && !plan.Gif && !plan.AnimatedImage && !plan.ImageSequenceOutput && IntermediateCodecs.IsIntermediate(plan.VideoCodec);

    private static bool VideoCopyCompatible(string container, string codec) => container switch
    {
        "mp4" => codec is "h264" or "hevc" or "mpeg4" or "av1",
        "mov" => codec is "h264" or "hevc" or "mpeg4" or "prores" or "dnxhd",
        "mxf" => codec is "dnxhd",
        "webm" => codec is "vp8" or "vp9" or "av1",
        "avi" => codec is "h264" or "mpeg4" or "mpeg2video" or "mjpeg",
        "mkv" => true,
        _ => false
    };

    private static bool AudioCopyCompatible(string container, string? codec) => container switch
    {
        "mp4" => codec is "aac" or "alac" or "mp3" or "flac",
        "mov" or "m4a" => codec is "aac" or "alac" or "mp3",
        "webm" => codec is "opus" or "vorbis",
        "mp3" => codec == "mp3",
        "wav" => codec is "pcm_s16le" or "pcm_s24le" or "pcm_f32le",
        "avi" => codec is "mp3" or "aac" or "pcm_s16le",
        "flac" => codec == "flac",
        "mkv" => true,
        _ => false
    };

    /// <summary>
    /// Kap ile video kodlayicisinin uyumu. Ara kodekler yalniz kurgu kaplarina yazilir: ProRes
    /// <c>mov</c>'a, DNxHR <c>mov</c> ve <c>mxf</c>'e. ffmpeg ProRes'i <c>mxf</c>'e ve
    /// <c>mkv</c>'ye de yaziyor (olcum <c>docs/olcumler/k12-ara-kodekler.md</c>), ama kurgu
    /// yazilimlarinin bekledigi eslesme bu; <c>mkv</c>'nin "her sey olur" satiri onlari kapsamaz.
    /// </summary>
    private static bool VideoEncodeCompatible(string container, string codec) => container switch
    {
        "mp4" => codec is "libx264" or "libx265" or "libsvtav1" or "libvpx-vp9" or "h264_nvenc" or "hevc_nvenc" or "h264_qsv" or "hevc_qsv" or "av1_nvenc" or "av1_qsv" or "h264_amf" or "hevc_amf" or "av1_amf",
        "mov" => codec is "libx264" or "libx265" or "libvpx-vp9" or "h264_nvenc" or "hevc_nvenc" or "h264_qsv" or "hevc_qsv" or "h264_amf" or "hevc_amf" or IntermediateCodecs.ProRes or IntermediateCodecs.DnxHr,
        "mxf" => codec is IntermediateCodecs.DnxHr,
        "webm" => codec is "libvpx-vp9" or "libsvtav1",
        "avi" => codec is "libx264",
        "mkv" => !IntermediateCodecs.IsIntermediate(codec),
        _ => false
    };

    private static bool AudioEncodeCompatible(string container, string codec) => container switch
    {
        "mp4" => codec is "aac" or "libmp3lame" or "flac",
        "m4a" => codec is "aac" or "libmp3lame",
        "mov" => codec is "aac" or "libmp3lame" or "pcm_s16le",
        "mxf" => codec is "pcm_s16le",
        "webm" => codec is "libopus",
        "mp3" => codec is "libmp3lame",
        "wav" => codec is "pcm_s16le",
        "avi" => codec is "libmp3lame" or "pcm_s16le",
        "flac" => codec == "flac",
        "mkv" => codec is "aac" or "libopus" or "libmp3lame" or "pcm_s16le" or "flac",
        _ => false
    };

    private static string FormatTime(TimeSpan value) => Saat.Ffmpeg(value);
}
