using System.Globalization;

namespace VidShrink.Core;

public static class ConversionArguments
{
    public const string AnimatedWebpEncoder = "libwebp_anim";
    public const string AnimatedAvifEncoder = "libsvtav1";

    public static IReadOnlyList<string> Validate(MediaInfo info, ConversionPlan plan)
    {
        plan = ResolveVideo(info, ResolveAudio(info, plan));
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
        if (plan.CarriesAudio && plan.AudioCodec == "copy" && !info.HasAudio && AddedAudio(plan) is null) errors.Add("The source has no audio stream to copy.");
        if (plan.AudioOnly && !info.HasAudio && plan.ExternalAudio is null) errors.Add("The source has no audio stream to extract.");
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
        if (plan.CarriesAudio && plan.AudioCodec == "copy")
        {
            var copied = plan.ExternalAudio is { Replace: true } ? Array.Empty<string?>()
                : plan.AudioTracks is null
                ? plan.ExternalAudio is not null && !info.HasAudio ? Array.Empty<string?>() : new[] { AutoAudio(info)?.Codec ?? info.AudioCodec }
                : SourceAudio(info, plan).Select(stream => (string?)stream.Codec).Distinct().ToArray();
            foreach (var codec in copied.Where(codec => !AudioCopyCompatible(plan.Container, codec)))
                errors.Add($"The {plan.Container.ToUpperInvariant()} container does not support copying the source {codec} audio stream.");
        }
        if (plan.CarriesAudio && plan.AudioCodec is not null && (plan.AudioTracks is not null || plan.ExternalAudio is not null))
        {
            var selected = SourceAudio(info, plan).Count + (AddedAudio(plan) is null ? 0 : 1);
            if (selected > 1 && plan.Container is "mp3" or "wav" or "flac" or "opus" or "ogg" or "aiff" or "ac3")
                errors.Add($"The {plan.Container.ToUpperInvariant()} container carries only one audio track.");
            if (selected == 0 && plan.AudioOnly && info.HasAudio)
                errors.Add("No audio track is selected.");
        }
        if (plan.CarriesAudio && plan.AudioCodec is { } processed && processed != "copy")
        {
            if (plan.AudioChannels is <= 0) errors.Add("Audio channel count must be positive.");
            if (plan.AudioSampleRate is { } rate)
            {
                if (plan.Container == "mxf" && rate != 48000) errors.Add("The MXF container carries only 48000 Hz audio.");
                else if (!StreamMapping.SampleRates.Contains(rate) || StreamMapping.SampleRatesFor(processed) is { } rates && !rates.Contains(rate))
                    errors.Add($"The {processed} audio encoder does not support {rate.ToString(CultureInfo.InvariantCulture)} Hz.");
            }
        }
        if (plan.BurnSubtitle is not null && (plan.AudioOnly || plan.Gif || plan.AnimatedImage || plan.ImageSequenceOutput || plan.VideoCodec == "copy"))
            errors.Add("Burning subtitles requires video encoding.");
        if (plan.BurnSubtitle is not null || !plan.AudioOnly) errors.AddRange(VideoFilterChain.Validate(info, plan.BurnOptions));
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
    public static IReadOnlyList<ConversionNote> Notes(ConversionPlan plan, MediaInfo? info = null)
    {
        var notes = new List<ConversionNote>();
        if (info is not null && plan.VideoCodec == "copy" && FiltersVideo(info, plan))
            notes.Add(ConversionNote.FiltersReencode);
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
        plan = ResolveVideo(info, ResolveAudio(info, plan));
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        if (WritesIntermediate(plan) && IntermediateCodecs.Find(plan.VideoCodec, plan.VideoProfile) is null)
            throw new ArgumentException($"Unsupported {plan.VideoCodec} profile: {plan.VideoProfile ?? "none"}", nameof(plan));
        var a = new List<string> { "-hide_banner", "-y" };
        var encodesVideo = !plan.AudioOnly && !plan.Gif && !plan.AnimatedImage && !plan.ImageSequenceOutput && plan.VideoCodec != "copy";
        if (encodesVideo) a.AddRange(CodecModel.DeviceArgs(plan.VideoCodec));
        if (plan.Start is { } start) a.AddRange(new[] { "-ss", FormatTime(start) });
        a.AddRange(info.Sequence is { } sequence ? sequence.InputArgs(info.Fps) : new[] { "-i", info.FilePath });
        if (AddedAudio(plan) is { } external)
        {
            if (plan.Start is { } audioStart) a.AddRange(new[] { "-ss", FormatTime(audioStart) });
            a.AddRange(new[] { "-i", external.Path });
        }
        var subtitles = MapsStreams(info, plan) ? Subtitles(info, plan) : null;
        var burnsImage = encodesVideo && VideoFilterChain.ImageBurnStream(info, plan.BurnOptions) is not null;
        if (burnsImage) subtitles ??= new List<SubtitleTrack>();
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
        if (subtitles is not null) a.AddRange(MapArguments(info, plan, subtitles, burnsImage));

        if (plan.AudioOnly)
        {
            a.Add("-vn");
            AddAudio(a, info, plan);
            a.AddRange(MetadataArgs(info, plan, subtitles));
            if (plan.EndAtShortest && AddedAudio(plan) is not null) a.Add("-shortest");
            a.Add(outputPath);
            return a;
        }

        var (before, after) = VideoFilters(info, plan);
        var filters = before.Concat(after).ToList();
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
            if (!string.IsNullOrEmpty(hdr.VideoFilter)) after.Add(hdr.VideoFilter);
            var pixelFormat = CodecModel.OutputPixelFormat(plan.VideoCodec, hdr.PixelFormat);
            after.AddRange(CodecModel.UploadFilters(plan.VideoCodec, pixelFormat));
            if (VideoFilterChain.BurnFilter(info, plan.BurnOptions, plan.Start?.TotalSeconds ?? 0) is { } burn) before.Add(burn);
            if (burnsImage)
                a.AddRange(new[] { "-filter_complex", VideoFilterChain.OverlayGraph(info, plan.BurnOptions, "0:" + VideoIndex(info), before, after)! });
            else if (before.Count + after.Count > 0) a.AddRange(new[] { "-vf", string.Join(',', before.Concat(after)) });

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
            var rate = plan.Fps ?? info.Fps;
            a.AddRange(VideoFilterChain.FrameRateArgs(plan.FrameRate, rate, rate));
            if (hdr.ColorArgs.Count > 0) a.AddRange(hdr.ColorArgs);
            if (dolbyVision) a.AddRange(HdrResolver.DolbyVisionArgs);
        }
        AddAudio(a, info, plan);
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
        if (plan.EndAtShortest && AddedAudio(plan) is not null) a.Add("-shortest");
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
    /// Çıktıya giren sesler <see cref="SourceAudio"/>'dan (seçim yokken ffmpeg'in seçeceği iz) ve dilleri çıktı sırasıyla yazılır.
    /// Altyazılar açık eşlendiğinde (<paramref name="mapped"/>) dil her kaynak ize kendi sırasıyla yazılır;
    /// eşlenmeyen planda yalnız kaynakta tek altyazı izi varken yazılır. Kapalıyken hiçbir argüman eklenmez.
    /// </summary>
    private static IReadOnlyList<string> MetadataArgs(MediaInfo info, ConversionPlan plan, IReadOnlyList<SubtitleTrack>? mapped = null)
    {
        var languages = SourceAudio(info, plan).Select(stream => stream.Language).ToList();
        if (AddedAudio(plan) is { } external) languages.Add(external.Language);
        if (!plan.DropMetadata)
            return AddedAudio(plan)?.Language is { } language
                ? new[] { "-metadata:s:a:" + (languages.Count - 1).ToString(CultureInfo.InvariantCulture), "language=" + language }
                : Array.Empty<string>();
        var audio = languages.ToArray();
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
    /// eklenmis dis dosya varken, ya da ses izi secimi verilmisken. Oteki planlarda komut eskisi gibi ffmpeg'in secimine birakilir.
    /// </summary>
    public static bool MapsStreams(MediaInfo info, ConversionPlan plan)
        => SubtitleContainer(plan) is not null
           && (plan.ExternalSubtitles.Count > 0 || info.Streams.Any(stream => stream.Kind == StreamKind.Subtitle))
           || (plan.AudioTracks is not null || plan.ExternalAudio is not null) && plan.CarriesAudio && plan.AudioCodec is not null;

    /// <summary>
    /// Ciktiya giden kaynak ses izleri, kaynak sirasiyla. Secim yokken (<see cref="ConversionPlan.AudioTracks"/>
    /// <c>null</c>) ffmpeg'in sececegi tek iz (<see cref="AutoAudio"/>); secimde aralik disi sira ve tekrar atilir.
    /// Ses tasimayan ya da sesi dusuren planda bos.
    /// </summary>
    public static IReadOnlyList<SourceStream> SourceAudio(MediaInfo info, ConversionPlan plan)
    {
        if (!plan.CarriesAudio || plan.AudioCodec is null || plan.ExternalAudio is { Replace: true }) return Array.Empty<SourceStream>();
        var audio = info.Streams.Where(stream => stream.Kind == StreamKind.Audio).ToList();
        if (plan.AudioTracks is null) return AutoAudio(info) is { } auto ? new[] { auto } : Array.Empty<SourceStream>();
        var chosen = plan.AudioTracks.Where(ordinal => ordinal >= 0 && ordinal < audio.Count).ToHashSet();
        return audio.Where((_, ordinal) => chosen.Contains(ordinal)).ToList();
    }

    /// <summary>
    /// Ciktiya giden altyazi izleri, kucultmenin akis kuralindan (<see cref="StreamMapping.SubtitleTracks"/>):
    /// kabin tasiyamadigi iz cevrilir ya da duser ve <paramref name="notes"/>'a yazilir.
    /// </summary>
    public static List<SubtitleTrack> Subtitles(MediaInfo info, ConversionPlan plan, List<StreamNote>? notes = null)
    {
        if (SubtitleContainer(plan) is not { } container) return new List<SubtitleTrack>();
        var request = new StreamRequest(ExternalSubtitles: plan.ExternalSubtitles, BurnedSubtitle: plan.BurnSubtitle);
        return StreamMapping.SubtitleTracks(info, request, container, AddedAudio(plan) is null ? 0 : 1, notes ?? new List<StreamNote>(),
            AutoAudio(info)?.Language, plan.SubtitleTracks);
    }

    /// <summary>
    /// Acik akis secimi: ilk video (ses kabinda yok), secilen sesler ya da secim yokken ffmpeg'in <c>-map</c>'siz
    /// sececegi ses (<see cref="AutoAudio"/>), sonra altyazilar. Secimsiz planda ses esleme yazilinca degismez.
    /// Resim altyazi yakilirken video bindirme grafiginin ciktisidir.
    /// </summary>
    private static IReadOnlyList<string> MapArguments(MediaInfo info, ConversionPlan plan, IReadOnlyList<SubtitleTrack> subtitles, bool burnsImage)
    {
        var a = new List<string>();
        if (!plan.AudioOnly)
            a.AddRange(new[] { "-map", burnsImage ? VideoFilterChain.OverlayOutput : "0:" + VideoIndex(info) });
        if (plan.AudioTracks is not null || plan.ExternalAudio is { Replace: true })
            foreach (var stream in SourceAudio(info, plan))
                a.AddRange(new[] { "-map", "0:" + stream.Index.ToString(CultureInfo.InvariantCulture) });
        else if (plan.CarriesAudio && plan.AudioCodec is not null)
        {
            if (AutoAudio(info) is { } audio) a.AddRange(new[] { "-map", "0:" + audio.Index.ToString(CultureInfo.InvariantCulture) });
            else if (info.HasAudio) a.AddRange(new[] { "-map", "0:a:0?" });
        }
        if (AddedAudio(plan) is not null) a.AddRange(new[] { "-map", "1:a:0" });
        foreach (var track in subtitles) a.AddRange(new[] { "-map", track.Map });
        return a;
    }

    /// <summary>Ciktiya giren dis ses: ses tasiyan ve sesi dusurmeyen planda <see cref="ConversionPlan.ExternalAudio"/>.</summary>
    private static ExternalAudio? AddedAudio(ConversionPlan plan)
        => plan.CarriesAudio && plan.AudioCodec is not null ? plan.ExternalAudio : null;

    /// <summary>
    /// Dis ses dosyasinin uzantisindan cikan olasi kodekler; <c>null</c> bilinmeyen kodektir ve yalniz her seyi alan kap kopyalar.
    /// Dosya yoklanmaz: kopya ancak her olasi kodek kaba uyarsa secilir, degilse iz yeniden kodlanir.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string?[]> ExternalAudioCodecs = new Dictionary<string, string?[]>
    {
        [".aac"] = new[] { "aac" },
        [".ac3"] = new[] { "ac3" },
        [".eac3"] = new[] { "eac3" },
        [".flac"] = new[] { "flac" },
        [".m4a"] = new[] { "aac", "alac" },
        [".mp3"] = new[] { "mp3" },
        [".ogg"] = new[] { "vorbis", "opus", "flac" },
        [".opus"] = new[] { "opus" },
        [".wav"] = new[] { "pcm_s16le", "pcm_s24le", "pcm_f32le", null },
        [".wma"] = new[] { "wmav2" }
    };

    /// <summary>Kopya kipinde dis ses kopyalanabilir mi (<see cref="ExternalAudioCodecs"/>).</summary>
    public static bool ExternalAudioCopies(string container, ExternalAudio audio)
        => ExternalAudioCodecs.TryGetValue(Path.GetExtension(audio.Path).ToLowerInvariant(), out var codecs)
            ? codecs.All(codec => AudioCopyCompatible(container, codec))
            : AudioCopyCompatible(container, null);

    /// <summary>Kopya kipinde kabin almadigi dis sesin kodlayicisi.</summary>
    private static string FallbackAudioEncoder(string container) => container switch
    {
        "webm" or "mkv" or "opus" or "ogg" => "libopus",
        "mp3" or "avi" => "libmp3lame",
        "wav" or "mxf" => "pcm_s16le",
        "flac" => "flac",
        "aiff" => "pcm_s16be",
        "ac3" => "ac3",
        _ => "aac"
    };

    /// <summary>
    /// Planın gerçekte yazacağı ses kodeği. Ses yüksekliği, kanal ya da örnekleme hızı seçilince kopya kapanır ve
    /// kap kendi kodlayıcısıyla yazar. Ses kabında kabın kendi kodlayıcısı seçiliyken işleme ve dış ses yoksa,
    /// kaynağın kaba sığan sesi yeniden kodlanmadan çıkarılır; M4A yalnız AAC ve ALAC'ı böyle çıkarır, MP3'ü AAC'a kodlar.
    /// </summary>
    public static ConversionPlan ResolveAudio(MediaInfo info, ConversionPlan plan)
    {
        if (!plan.CarriesAudio || plan.AudioCodec is null) return plan;
        if (plan.AudioCodec == "copy")
            return plan.ProcessesAudio ? plan with { AudioCodec = FallbackAudioEncoder(plan.Container) } : plan;
        if (!plan.AudioOnly || plan.ProcessesAudio || plan.ExternalAudio is not null
            || plan.AudioCodec != ConversionPresetPlan.AudioCodecForAudioContainer(plan.Container)) return plan;
        var source = SourceAudio(info, plan);
        return source.Count > 0 && source.All(stream => AudioCopyCompatible(plan.Container, stream.Codec) && (plan.Container != "m4a" || stream.Codec is "aac" or "alac"))
            ? plan with { AudioCodec = "copy" }
            : plan;
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

    private static string VideoIndex(MediaInfo info)
        => info.Streams.FirstOrDefault(stream => stream.Kind == StreamKind.Video && !stream.IsAttachedPicture) is { } video
            ? video.Index.ToString(CultureInfo.InvariantCulture)
            : "v:0";

    /// <summary>
    /// Dönüştür'ün görüntü süzgeçleri iki parça: ölçekten önceki görüntü süzgeçleri (Küçült'ün sırası)
    /// ve ölçek, kare pikseli, kare hızı. Resim altyazı bindirmesi ikisinin arasına girer.
    /// </summary>
    private static (List<string> Before, List<string> After) VideoFilters(MediaInfo info, ConversionPlan plan)
    {
        var before = VideoFilterChain.PictureFilters(info, plan.Filters);
        var after = new List<string>();
        var flags = VideoFilterChain.ScaleFlags(plan.Filters.Scaler);
        if (plan.PadsToFrame)
        {
            if (info.IsAnamorphic)
            {
                after.Add($"scale=trunc(iw*sar/2)*2:ih:flags={flags}");
                after.Add(VideoFilterChain.SquarePixelFilter);
            }
            after.AddRange(VideoFilterChain.LetterboxFilters(plan.Width!.Value, plan.Height!.Value, plan.Filters.Scaler));
        }
        else
        {
            if (plan.Width is { } width && plan.Height is { } height) after.Add($"scale={width}:{height}:flags={flags}");
            else if (plan.Height is { } h && info.IsAnamorphic) after.Add($"scale=trunc(iw*sar*{h}/ih/2)*2:{h}:flags={flags}");
            else if (plan.Height is { } h2) after.Add($"scale=-2:{h2}:flags={flags}");
            else if (info.IsAnamorphic) after.Add($"scale=trunc(iw*sar/2)*2:ih:flags={flags}");
            if (info.IsAnamorphic) after.Add(VideoFilterChain.SquarePixelFilter);
        }
        if (plan.FrameRate != FrameRateMode.Peak && plan.Fps is { } fps && Math.Abs(fps - info.Fps) > 0.01)
            after.Add(plan.Interpolate ? VideoFilterChain.Interpolate(fps) : $"fps={fps.ToString("0.###", CultureInfo.InvariantCulture)}");
        return (before, after);
    }

    /// <summary>
    /// Plan görüntüye dokunan bir seçim taşıyor mu: görüntü süzgeci, kare hızı kipi ya da çerçeveye
    /// sığdırma. Ses, GIF, hareketli görsel ve görüntü dizisi kendi kodlayıcısıyla yazdığı için sayılmaz.
    /// </summary>
    public static bool FiltersVideo(MediaInfo info, ConversionPlan plan)
        => !plan.AudioOnly && !plan.Gif && !plan.AnimatedImage && !plan.ImageSequenceOutput
           && (plan.Filters.ChangesPictureFor(info) || plan.FrameRate != FrameRateMode.Auto || plan.PadsToFrame);

    /// <summary>
    /// Akış kopyası süzgeç taşıyamaz; görüntüye dokunan seçim varsa kopya kabın kendi kodlayıcısına
    /// döner: WebM'de VP9, MXF'te DNxHR HQ, öteki kaplarda x264.
    /// </summary>
    public static ConversionPlan ResolveVideo(MediaInfo info, ConversionPlan plan)
    {
        if (plan.VideoCodec != "copy" || !FiltersVideo(info, plan)) return plan;
        return plan.Container switch
        {
            "webm" => plan with { VideoCodec = "libvpx-vp9" },
            "mxf" => plan with { VideoCodec = IntermediateCodecs.DnxHr, VideoProfile = "dnxhr_hq" },
            _ => plan with { VideoCodec = "libx264" }
        };
    }

    private static void AddAudio(List<string> args, MediaInfo info, ConversionPlan plan)
    {
        var external = AddedAudio(plan);
        if (plan.AudioCodec is null || plan.AudioTracks is not null && SourceAudio(info, plan).Count == 0 && external is null) args.Add("-an");
        else if (plan.AudioCodec == "copy")
        {
            args.AddRange(new[] { "-c:a", "copy" });
            if (external is not null && !ExternalAudioCopies(plan.Container, external))
            {
                var stream = SourceAudio(info, plan).Count.ToString(CultureInfo.InvariantCulture);
                var encoder = FallbackAudioEncoder(plan.Container);
                args.AddRange(new[] { "-c:a:" + stream, encoder });
                if (encoder is not "pcm_s16le" and not "flac") args.AddRange(new[] { "-b:a:" + stream, $"{plan.AudioBitrateK}k" });
            }
        }
        else
        {
            args.AddRange(new[] { "-c:a", plan.AudioCodec, "-b:a", $"{plan.AudioBitrateK}k" });
            if (plan.AudioLoudnorm)
            {
                var rate = plan.AudioSampleRate ?? SourceAudio(info, plan).Select(stream => stream.SampleRate).FirstOrDefault(value => value > 0);
                args.AddRange(new[] { "-af", StreamMapping.LoudnormFilter + ",aresample=" + (rate > 0 ? rate : StreamMapping.DefaultSampleRate).ToString(CultureInfo.InvariantCulture) });
            }
            if (plan.AudioChannels is { } channels) args.AddRange(new[] { "-ac", channels.ToString(CultureInfo.InvariantCulture) });
            if (plan.AudioSampleRate is { } sampleRate) args.AddRange(new[] { "-ar", sampleRate.ToString(CultureInfo.InvariantCulture) });
        }
        if (plan.Container == "mxf" && plan.AudioCodec is not null && !args.Contains("-an") && !args.Contains("-ar")) args.AddRange(MxfAudioArgs);
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
        "mp4" => codec is "aac" or "alac" or "mp3" or "flac" or "ac3" or "eac3" or "opus",
        "mov" or "m4a" => codec is "aac" or "alac" or "mp3",
        "webm" => codec is "opus" or "vorbis",
        "mp3" => codec == "mp3",
        "wav" => codec is "pcm_s16le" or "pcm_s24le" or "pcm_f32le",
        "avi" => codec is "mp3" or "aac" or "pcm_s16le",
        "flac" => codec == "flac",
        "opus" => codec == "opus",
        "ogg" => codec is "opus" or "vorbis" or "flac",
        "aiff" => codec is "pcm_s16be" or "pcm_s24be",
        "ac3" => codec == "ac3",
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
        "mp4" => codec is "aac" or "libmp3lame" or "flac" or "ac3" or "eac3",
        "m4a" => codec is "aac" or "libmp3lame",
        "mov" => codec is "aac" or "libmp3lame" or "pcm_s16le" or "ac3" or "eac3",
        "mxf" => codec is "pcm_s16le",
        "webm" => codec is "libopus",
        "mp3" => codec is "libmp3lame",
        "wav" => codec is "pcm_s16le",
        "avi" => codec is "libmp3lame" or "pcm_s16le",
        "flac" => codec == "flac",
        "opus" => codec is "libopus",
        "ogg" => codec is "libopus" or "flac",
        "aiff" => codec is "pcm_s16be",
        "ac3" => codec is "ac3",
        "mkv" => codec is "aac" or "libopus" or "libmp3lame" or "pcm_s16le" or "flac" or "ac3" or "eac3",
        _ => false
    };

    private static string FormatTime(TimeSpan value) => Saat.Ffmpeg(value);
}
